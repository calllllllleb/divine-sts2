using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sts2.NativeSim.Protocol;

namespace Sts2.NativeSim.Core;

public sealed partial class PersistentNativeCombatEnvironment
{
    private const int CardDynamicRuntimeVersion = 1;
    private const string CardVarNamespace = "MegaCrit.Sts2.Core.Localization.DynamicVars.";
    private const string SovereignBladeType = "MegaCrit.Sts2.Core.Models.Cards.SovereignBlade";
    private readonly Dictionary<Type, Dictionary<string, object>> _cardVarStructures = [];
    private readonly HashSet<Type> _validatedCardVarTypes = [];
    private bool _cardVarAbiValidated;

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record CardDynamicRuntime([property: JsonRequired] int Version,
        [property: JsonRequired] string NativeType, [property: JsonRequired] CardDynamicVarSet? DynamicVars,
        [property: JsonRequired] SovereignBladeRuntime? SovereignBlade);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record CardDynamicVarSet([property: JsonRequired] string NativeType,
        [property: JsonRequired] List<CardDynamicVar> Variables);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record CardDynamicVar([property: JsonRequired] string Name,
        [property: JsonRequired] string NativeType, [property: JsonRequired] bool HasOwner,
        [property: JsonRequired] string BaseValue, [property: JsonRequired] string EnchantedValue,
        [property: JsonRequired] string PreviewValue, [property: JsonRequired] bool WasJustUpgraded,
        [property: JsonRequired] string? EnergyColorPrefix);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record SovereignBladeRuntime([property: JsonRequired] string CurrentDamage,
        [property: JsonRequired] string CurrentRepeats, [property: JsonRequired] bool CreatedThroughForge);

    private static ProtocolException CardVarError(string path, string detail) =>
        new("unsupported_card_dynamic_runtime", $"{path}: {detail}");

    private static decimal CardVarDecimal(string? text, string path)
    {
        if (text is null || !decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out decimal value) || value.ToString(CultureInfo.InvariantCulture) != text
            || value > 999999999m)
            throw CardVarError(path, "Exact invariant native decimal text is required.");
        return value;
    }

    // Only the reviewed variable ABI is inspected. No CardModel object graph is traversed.
    // Non-numeric configuration belongs to the native factory; divergent configuration rejects.
    private void ValidateCardVarType(Type type, string path)
    {
        if (_validatedCardVarTypes.Contains(type)) return;
        if (type.Assembly != _context.Assembly) throw CardVarError(path, "Foreign DynamicVar type.");
        Type definition = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
        string name = definition.FullName ?? "";
        string[] fields = name switch
        {
            CardVarNamespace + "DynamicVar" => ["_owner", "_baseValue", "_enchantedValue", "_previewValue", "<Name>k__BackingField", "<WasJustUpgraded>k__BackingField"],
            CardVarNamespace + "CalculatedVar" => ["_multiplierCalc"],
            CardVarNamespace + "DamageVar" or CardVarNamespace + "OstyDamageVar" or CardVarNamespace + "BlockVar"
                or CardVarNamespace + "CalculatedBlockVar" => ["<Props>k__BackingField"],
            CardVarNamespace + "CalculatedDamageVar" => ["<Props>k__BackingField", "<IsFromOsty>k__BackingField"],
            CardVarNamespace + "ExtraDamageVar" => ["<IsFromOsty>k__BackingField"],
            CardVarNamespace + "EnergyVar" => ["<ColorPrefix>k__BackingField"],
            CardVarNamespace + "BoolVar" or CardVarNamespace + "CalculationBaseVar" or CardVarNamespace + "CalculationExtraVar"
                or CardVarNamespace + "CardsVar" or CardVarNamespace + "ForgeVar" or CardVarNamespace + "GoldVar"
                or CardVarNamespace + "HealVar" or CardVarNamespace + "HpLossVar" or CardVarNamespace + "IntVar"
                or CardVarNamespace + "MaxHpVar" or CardVarNamespace + "PowerVar`1" or CardVarNamespace + "RepeatVar"
                or CardVarNamespace + "StarsVar" or CardVarNamespace + "SummonVar" => [],
            _ => throw CardVarError(path, $"Unreviewed variable subtype {name}; additional state requires a separate codec.")
        };
        ExactInstanceFields(definition, fields, path);
        if (definition != T(ConstantPowerVarType)) ValidateCardVarType(type.BaseType!, path);
        _validatedCardVarTypes.Add(type);
    }

    private Dictionary<string, object> CardVarStructure(object card, string path)
    {
        if (_assemblyHash != HistoryDllSha256) throw CardVarError(path, "The reviewed fixed DLL is required.");
        if (!_cardVarAbiValidated) { ValidatePowerVarAbi(); _cardVarAbiValidated = true; }
        if (_cardVarStructures.TryGetValue(card.GetType(), out var cached)) return cached;
        object canonical = Find(ReflectionTools.GetStatic(T("MegaCrit.Sts2.Core.Models.ModelDb"), "AllCards")!, Entry(card));
        if (canonical.GetType() != card.GetType()) throw CardVarError(path, "Card native type and ModelDb identity differ.");
        var structure = new Dictionary<string, object>(StringComparer.Ordinal);
        // Factory descriptors are independent of the live lazy set. No formula, preview,
        // command, ToMutable or owner initialization is executed while capturing.
        foreach (object? variable in ReflectionTools.Enumerate(ReflectionTools.Get(canonical, "CanonicalVars")))
        {
            if (variable is null) throw CardVarError(path, "Null native variable descriptor.");
            ValidateCardVarType(variable.GetType(), path);
            string name = (string)ReflectionTools.Get(variable, "Name")!;
            if (!structure.TryAdd(name, variable)) throw CardVarError(path, "Duplicate native variable descriptor.");
        }
        _cardVarStructures.Add(card.GetType(), structure);
        return structure;
    }

    private static Dictionary<string, object> CardVars(object set) => ReflectionTools.Enumerate(set)
        .ToDictionary(pair => (string)ReflectionTools.Get(pair!, "Key")!, pair => ReflectionTools.Get(pair!, "Value")!, StringComparer.Ordinal);

    private void ValidateNativeCardVar(object variable, object descriptor, object card, string path)
    {
        if (variable.GetType() != descriptor.GetType()) throw CardVarError(path, "Native variable type differs from card factory.");
        ValidateCardVarType(variable.GetType(), path);
        foreach (string property in new[] { "Props", "IsFromOsty" })
            if (!Equals(ReflectionTools.Get(variable, property), ReflectionTools.Get(descriptor, property)))
                throw CardVarError(path, $"Mutable subtype configuration {property} differs from native initialization; it is outside this numeric codec.");
        object? owner = ReflectionTools.Get(variable, "_owner");
        if (owner is not null && !ReferenceEquals(owner, card)) throw CardVarError(path, "Foreign owner alias.");
        if (T(CardVarNamespace + "CalculatedVar").IsAssignableFrom(variable.GetType()))
        {
            if (owner is null || ReflectionTools.Get(variable, "_multiplierCalc") is not Delegate formula
                || ReflectionTools.Get(descriptor, "_multiplierCalc") is not Delegate native
                || formula.Method != native.Method || formula.GetInvocationList().Length != 1
                || formula.Target?.GetType() != native.Target?.GetType()
                || (formula.Target is not null && formula.Target.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Length != 0))
                throw CardVarError(path, "Formula must be the native stateless factory delegate with this card as variable owner.");
        }
    }

    private void ValidateCardDynamicRuntime(CardSnapshot saved, string path)
    {
        CardDynamicRuntime runtime = saved.DynamicRuntime ?? throw CardVarError(path, "Missing versioned card dynamic runtime.");
        object canonical = Find(ReflectionTools.GetStatic(T("MegaCrit.Sts2.Core.Models.ModelDb"), "AllCards")!, saved.ModelId);
        var structure = CardVarStructure(canonical, path);
        if (runtime.Version != CardDynamicRuntimeVersion || runtime.NativeType != canonical.GetType().FullName)
            throw CardVarError(path, "Unknown card dynamic runtime version/native type.");
        if (runtime.DynamicVars is { } set)
        {
            if (set.NativeType != PowerVarSetType || set.Variables is null
                || set.Variables.Count != structure.Count || set.Variables.Any(v => v is null)
                || set.Variables.Any(v => string.IsNullOrWhiteSpace(v.Name))
                || set.Variables.Select(v => v.Name).Distinct(StringComparer.Ordinal).Count() != set.Variables.Count)
                throw CardVarError(path, "Invalid native variable name collection.");
            foreach (CardDynamicVar v in set.Variables)
            {
                if (!structure.TryGetValue(v.Name, out object? descriptor) || v.NativeType != descriptor.GetType().FullName)
                    throw CardVarError(path, "Variable name/type differs from native card structure.");
                _ = CardVarDecimal(v.BaseValue, path + "." + v.Name + ".BaseValue");
                _ = CardVarDecimal(v.EnchantedValue, path + "." + v.Name + ".EnchantedValue");
                _ = CardVarDecimal(v.PreviewValue, path + "." + v.Name + ".PreviewValue");
                if ((descriptor.GetType() == T(CardVarNamespace + "EnergyVar")) != (v.EnergyColorPrefix is not null)
                    || (!v.HasOwner && T(CardVarNamespace + "CalculatedVar").IsAssignableFrom(descriptor.GetType())))
                    throw CardVarError(path, "Invalid subtype scratch/owner state.");
            }
        }
        if ((runtime.NativeType == SovereignBladeType) != (runtime.SovereignBlade is not null))
            throw CardVarError(path, "Missing or foreign SovereignBlade private carrier.");
        if (runtime.SovereignBlade is { } blade)
        {
            ExactInstanceFields(T(SovereignBladeType), ["_currentDamage", "_currentRepeats", "_createdThroughForge"], path);
            decimal damage = CardVarDecimal(blade.CurrentDamage, path + ".CurrentDamage");
            decimal repeats = CardVarDecimal(blade.CurrentRepeats, path + ".CurrentRepeats");
            if (runtime.DynamicVars is { } vars && (damage != CardVarDecimal(vars.Variables.Single(v => v.Name == "Damage").BaseValue, path)
                || repeats != CardVarDecimal(vars.Variables.Single(v => v.Name == "Repeat").BaseValue, path)))
                throw CardVarError(path, "SovereignBlade private values and actual Damage/Repeat disagree.");
            if (runtime.DynamicVars is null && (damage != 10m || repeats != 1m))
                throw CardVarError(path, "Changed SovereignBlade values require the initialized native variable set.");
        }
    }

    private CardDynamicRuntime CaptureCardDynamicRuntime(object card, string path)
    {
        var structure = CardVarStructure(card, path);
        object? set = ReflectionTools.Get(card, "_dynamicVars");
        CardDynamicVarSet? payload = null;
        if (set is not null)
        {
            if (set.GetType() != T(PowerVarSetType)) throw CardVarError(path, "Foreign native variable set.");
            var actual = CardVars(set);
            if (!actual.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(structure.Keys)) throw CardVarError(path, "Native variable name set changed.");
            var variables = new List<CardDynamicVar>();
            foreach ((string key, object variable) in actual.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                if (key != (string)ReflectionTools.Get(variable, "Name")!) throw CardVarError(path, "Variable key and name differ.");
                ValidateNativeCardVar(variable, structure[key], card, path + "." + key);
                string Value(string field) => ((decimal)ReflectionTools.Get(variable, field)!).ToString(CultureInfo.InvariantCulture);
                variables.Add(new(key, variable.GetType().FullName!, ReflectionTools.Get(variable, "_owner") is not null,
                    Value("_baseValue"), Value("_enchantedValue"), Value("_previewValue"), (bool)ReflectionTools.Get(variable, "WasJustUpgraded")!,
                    variable.GetType() == T(CardVarNamespace + "EnergyVar") ? (string)ReflectionTools.Get(variable, "ColorPrefix")! : null));
            }
            payload = new(set.GetType().FullName!, variables);
        }
        string ValueOf(string field) => ((decimal)ReflectionTools.Get(card, field)!).ToString(CultureInfo.InvariantCulture);
        SovereignBladeRuntime? blade = card.GetType().FullName == SovereignBladeType
            ? new(ValueOf("_currentDamage"), ValueOf("_currentRepeats"), (bool)ReflectionTools.Get(card, "_createdThroughForge")!) : null;
        return new(CardDynamicRuntimeVersion, card.GetType().FullName!, payload, blade);
    }

    private void RestoreCardDynamicRuntime(object card, CardSnapshot saved, string path)
    {
        ValidateCardDynamicRuntime(saved, path);
        CardDynamicRuntime runtime = saved.DynamicRuntime;
        if (runtime.SovereignBlade is { } blade)
        {
            ReflectionTools.Set(card, "_currentDamage", CardVarDecimal(blade.CurrentDamage, path));
            ReflectionTools.Set(card, "_currentRepeats", CardVarDecimal(blade.CurrentRepeats, path));
            ReflectionTools.Set(card, "_createdThroughForge", blade.CreatedThroughForge);
        }
        if (runtime.DynamicVars is null) { ReflectionTools.Set(card, "_dynamicVars", null); return; }
        // Keep the current card and its own native variables/formulas. Never replace cards,
        // synthesize generic variables, serialize a delegate, or replay commands.
        object set = ReflectionTools.Get(card, "DynamicVars")!;
        var actual = CardVars(set);
        var structure = CardVarStructure(card, path);
        if (set.GetType() != T(PowerVarSetType) || !actual.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(structure.Keys))
            throw CardVarError(path, "Resident native variable structure differs.");
        foreach (CardDynamicVar v in runtime.DynamicVars.Variables)
        {
            object variable = actual[v.Name];
            ValidateNativeCardVar(variable, structure[v.Name], card, path + "." + v.Name);
            ReflectionTools.Set(variable, "_owner", v.HasOwner ? card : null);
            ReflectionTools.Set(variable, "_baseValue", CardVarDecimal(v.BaseValue, path));
            ReflectionTools.Set(variable, "_enchantedValue", CardVarDecimal(v.EnchantedValue, path));
            ReflectionTools.Set(variable, "_previewValue", CardVarDecimal(v.PreviewValue, path));
            ReflectionTools.Set(variable, "WasJustUpgraded", v.WasJustUpgraded);
            if (v.EnergyColorPrefix is not null) ReflectionTools.Set(variable, "ColorPrefix", v.EnergyColorPrefix);
        }
    }

    private static IEnumerable<CardSnapshot> AllSnapshotCards(CombatSnapshot snapshot) => snapshot.Hand.Concat(snapshot.DrawPile)
        .Concat(snapshot.DiscardPile).Concat(snapshot.ExhaustPile).Concat(snapshot.PlayPile);
    private void ValidateCardDynamicRuntimes(CombatSnapshot snapshot)
    {
        foreach (CardSnapshot card in AllSnapshotCards(snapshot)) ValidateCardDynamicRuntime(card, "Card." + card.InstanceId);
    }
    private static string CardDynamicRuntimeFingerprint(CombatSnapshot snapshot) =>
        JsonSerializer.Serialize(AllSnapshotCards(snapshot).Select(c => new { c.InstanceId, c.CombatCardId, c.DynamicRuntime }), PortableRootJson);
    private static bool CardDynamicRuntimeFingerprintMatches(CombatSnapshot expected, CombatSnapshot actual) =>
        CardDynamicRuntimeFingerprint(expected) == CardDynamicRuntimeFingerprint(actual);

    private string? CurrentCardDynamicRuntimeFingerprint()
    {
        if (_runMode || _mapMode || _rewardMode || _restMode || _eventMode || _customRewardMode
            || _combat is null || _pcs is null || _player is null || _reset is null || !PlayerAlive() || !Alive("Enemies")) return null;
        object db = ReflectionTools.GetStatic(T("MegaCrit.Sts2.Core.GameActions.Multiplayer.NetCombatCardDb"), "Instance")!;
        var cards = new List<object>();
        foreach (string pile in new[] { "Hand", "DrawPile", "DiscardPile", "ExhaustPile", "PlayPile" })
            foreach (object? card in ReflectionTools.Enumerate(ReflectionTools.Get(ReflectionTools.Get(_pcs, pile)!, "Cards")))
                if (card is not null) cards.Add(new { InstanceId = GetCardInstanceId(card),
                    CombatCardId = (uint)ReflectionTools.Invoke(db, "GetCardId", card)!,
                    DynamicRuntime = CaptureCardDynamicRuntime(card, "Card." + GetCardInstanceId(card)) });
        return JsonSerializer.Serialize(cards, PortableRootJson);
    }

    // Native preview has scratch setters (and a lazy set getter). Preserve the actual
    // pre-preview storage without invoking formulas or touching the gameplay values.
    private static Action PreserveCardVarPreview(object card)
    {
        object? original = ReflectionTools.Get(card, "_dynamicVars");
        if (original is null) return () => ReflectionTools.Set(card, "_dynamicVars", null);
        var values = CardVars(original).Values.Select(v => new { Variable = v,
            Base = ReflectionTools.Get(v, "_baseValue"), Enchanted = ReflectionTools.Get(v, "_enchantedValue"),
            Preview = ReflectionTools.Get(v, "_previewValue"), Upgraded = ReflectionTools.Get(v, "WasJustUpgraded"),
            Color = ReflectionTools.Get(v, "ColorPrefix") }).ToArray();
        return () => { foreach (var value in values) {
            ReflectionTools.Set(value.Variable, "_baseValue", value.Base);
            ReflectionTools.Set(value.Variable, "_enchantedValue", value.Enchanted);
            ReflectionTools.Set(value.Variable, "_previewValue", value.Preview);
            ReflectionTools.Set(value.Variable, "WasJustUpgraded", value.Upgraded);
            if (value.Color is not null) ReflectionTools.Set(value.Variable, "ColorPrefix", value.Color);
        } };
    }
}
