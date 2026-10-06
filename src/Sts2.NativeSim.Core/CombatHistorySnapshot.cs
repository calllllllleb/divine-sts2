using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sts2.NativeSim.Protocol;

namespace Sts2.NativeSim.Core;

public sealed partial class PersistentNativeCombatEnvironment
{
    // Internal reference numbers are local to this payload, never policy features.
    private const int CombatHistoryContractVersion = 5;
    private const string HistoryDllSha256 = "A1F9E653F1E28E4076558FEE1E60D218619CB7E057B887C6417F62C62C6D7A52";
    private const string HistoryConsumerContract = "pinned-singleplayer-history-read-v1:" + HistoryDllSha256;
    private const string HistoryEntryNamespace = "MegaCrit.Sts2.Core.Combat.History.Entries.";
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record CombatHistorySnapshot([property: JsonRequired] int Version,
        [property: JsonRequired] string ConsumerContract, [property: JsonRequired] uint PlayerCombatId, [property: JsonRequired] ulong PlayerNetId,
        [property: JsonRequired] List<HistoryObject> Objects, [property: JsonRequired] List<HistoryEntry> Entries);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record HistoryObject([property: JsonRequired] string Kind, [property: JsonRequired] string NativeType,
        [property: JsonRequired] string? Binding, [property: JsonRequired] string? ModelId,
        [property: JsonRequired] Dictionary<string, JsonElement> Fields,
        HistoricalPotion? Potion = null, HistoricalAffliction? Affliction = null, HistoricalPower? Power = null,
        HistoricalOrb? Orb = null, HistoricalCreature? Creature = null, HistoricalMonster? Monster = null, HistoricalMove? Move = null);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record HistoricalCreature([property: JsonRequired] uint CombatId,
        [property: JsonRequired] int Monster, [property: JsonRequired] string Side,
        [property: JsonRequired] int CurrentHp, [property: JsonRequired] int MaxHp, [property: JsonRequired] int Block,
        [property: JsonRequired] string? SlotName, [property: JsonRequired] int? MonsterMaxHpBeforeModification,
        [property: JsonRequired] string HpDisplay, [property: JsonRequired] int? Player,
        [property: JsonRequired] int? PetOwner, [property: JsonRequired] bool HasCombatState,
        [property: JsonRequired] List<int> Powers);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record HistoricalMonster([property: JsonRequired] int Creature);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record HistoricalMove([property: JsonRequired] int Monster, [property: JsonRequired] string MoveId);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record HistoricalPotion([property: JsonRequired] int? Owner,
        [property: JsonRequired] bool IsQueued, [property: JsonRequired] bool HasBeenRemovedFromState);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record HistoricalOrb([property: JsonRequired] int? Owner);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record HistoricalAffliction([property: JsonRequired] int? Card, [property: JsonRequired] int Amount);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record HistoricalPower([property: JsonRequired] int? Owner,
        [property: JsonRequired] int? Applier, [property: JsonRequired] int? Target,
        [property: JsonRequired] int Amount, [property: JsonRequired] int AmountOnTurnStart,
        [property: JsonRequired] bool SkipNextDurationTick, [property: JsonRequired] PowerDynamicVarSet? DynamicVars,
        [property: JsonRequired] string? ResolvedBigIconPath);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record HistoryEntry([property: JsonRequired] string Type, [property: JsonRequired] int? Actor,
        [property: JsonRequired] int RoundNumber, [property: JsonRequired] string CurrentSide,
        [property: JsonRequired] Dictionary<ulong, int> PlayerTurnNumbers,
        [property: JsonRequired] Dictionary<string, JsonElement> Fields);

    // The 17 entry records and executable Card/DamageResult carriers remain exact.
    // History-only model fields follow the fixed-DLL consumer contract, not a private-state clone.
    private static readonly Dictionary<string, Dictionary<string, string>> HistoryEntryFields = new(StringComparer.Ordinal)
    {
        ["BlockGainedEntry"] = new() { ["Amount"] = "int", ["Props"] = "props", ["CardPlay"] = "CardPlay?" },
        ["CardAfflictedEntry"] = new() { ["Card"] = "Card", ["Affliction"] = "Affliction" },
        ["CardDiscardedEntry"] = new() { ["Card"] = "Card" },
        ["CardDrawnEntry"] = new() { ["Card"] = "Card", ["FromHandDraw"] = "bool" },
        ["CardExhaustedEntry"] = new() { ["Card"] = "Card" },
        ["CardGeneratedEntry"] = new() { ["Card"] = "Card", ["Creator"] = "Player?" },
        ["CardPlayStartedEntry"] = new() { ["CardPlay"] = "CardPlay" },
        ["CardPlayFinishedEntry"] = new() { ["CardPlay"] = "CardPlay", ["WasEthereal"] = "bool" },
        ["CreatureAttackedEntry"] = new() { ["DamageResults"] = "DamageResult[]" },
        ["DamageReceivedEntry"] = new() { ["Result"] = "DamageResult", ["Dealer"] = "Creature?", ["CardSource"] = "Card?" },
        ["EnergySpentEntry"] = new() { ["Amount"] = "int" },
        ["MonsterPerformedMoveEntry"] = new() { ["Monster"] = "Monster", ["Move"] = "Move", ["Targets"] = "Creature[]?" },
        ["OrbChanneledEntry"] = new() { ["Orb"] = "Orb" },
        ["PotionUsedEntry"] = new() { ["Potion"] = "Potion", ["Target"] = "Creature?" },
        ["PowerReceivedEntry"] = new() { ["Power"] = "Power", ["Amount"] = "decimal", ["Applier"] = "Creature?" },
        ["StarsModifiedEntry"] = new() { ["Amount"] = "int" },
        ["SummonedEntry"] = new() { ["Amount"] = "int" },
    };
    private static readonly Dictionary<string, string> HistoryCardPlayFields = new()
    {
        ["Card"] = "Card", ["Target"] = "Creature?", ["ResultPile"] = "pile",
        ["EnergySpent"] = "int", ["EnergyValue"] = "int", ["StarsSpent"] = "int", ["StarValue"] = "int",
        ["IsAutoPlay"] = "bool", ["PlayIndex"] = "int", ["PlayCount"] = "int",
    };
    private static readonly Dictionary<string, string> HistoryDamageFields = new()
    {
        ["Receiver"] = "Creature", ["Props"] = "props", ["BlockedDamage"] = "int", ["UnblockedDamage"] = "int",
        ["OverkillDamage"] = "int", ["WasBlockBroken"] = "bool", ["WasFullyBlocked"] = "bool", ["WasTargetKilled"] = "bool",
    };

    private object NativeCombatHistory => ReflectionTools.Get(_manager!, "History")
        ?? throw new ProtocolException("unsupported_combat_history", "Missing native CombatManager.History.");
    private static ProtocolException HistoryReferenceError(string path, string detail) =>
        new("unsupported_history_reference", $"{path}: {detail}");

    // Bounded protection for RNG, live membership/resources and the existing history tail.
    // This deliberately does not claim arbitrary private-field or reflection coverage.
    private string HistoryIsolationStamp()
    {
        int Identity(object? value) => value is null ? 0 : RuntimeHelpers.GetHashCode(value);
        object Members(object? values) => ReflectionTools.Enumerate(values).Select(Identity).ToArray();
        return JsonSerializer.Serialize(new {
            Rng = RunRngCounters(), Energy = ReflectionTools.Get(_pcs!, "Energy"), Stars = ReflectionTools.Get(_pcs!, "Stars"),
            Creatures = ReflectionTools.Enumerate(ReflectionTools.Get(_combat!, "Creatures")).Select(c => new {
                Identity = Identity(c), Hp = ReflectionTools.Get(c!, "CurrentHp"), Block = ReflectionTools.Get(c!, "Block"),
                Powers = ReflectionTools.Enumerate(ReflectionTools.Get(c!, "Powers")).Select(p => new { Identity = Identity(p), Amount = ReflectionTools.Get(p!, "Amount") }),
                MonsterRng = ReflectionTools.Get(c!, "Monster") is { } m && ReflectionTools.Get(m, "_rng") is { } r ? ReflectionTools.Get(r, "Counter") : null,
                NextMove = ReflectionTools.Get(c!, "Monster") is { } monster ? Identity(ReflectionTools.Get(monster, "NextMove")) : 0
            }), Enemies = Members(ReflectionTools.Get(_combat!, "Enemies")), Potions = Members(ReflectionTools.Get(_player!, "PotionSlots")),
            Orbs = ReflectionTools.Get(_pcs!, "OrbQueue") is { } q ? Members(ReflectionTools.Get(q, "Orbs")) : null,
            Piles = new[] { "Hand", "DrawPile", "DiscardPile", "ExhaustPile", "PlayPile" }
                .Select(pile => Members(ReflectionTools.Get(ReflectionTools.Get(_pcs!, pile)!, "Cards")))
        });
    }

    private void ValidateHistoryConsumerBuild()
    {
        if (!_assemblyHash.Equals(HistoryDllSha256, StringComparison.OrdinalIgnoreCase))
            throw new ProtocolException("unsupported_history_contract_build", "History read contract requires the reviewed fixed DLL.");
        Type entry = T("MegaCrit.Sts2.Core.Combat.History.CombatHistoryEntry");
        ExactInstanceFields(entry, ["_playerTurnNumbers", "<Actor>k__BackingField", "<RoundNumber>k__BackingField", "<CurrentSide>k__BackingField", "<History>k__BackingField"], "History.Entry");
        foreach (var (name, fields) in HistoryEntryFields)
            ExactInstanceFields(T(HistoryEntryNamespace + name), fields.Keys.Select(field => "<" + field + ">k__BackingField"), "History." + name);
    }

    private CombatHistorySnapshot CaptureCombatHistory()
    {
        ValidateHistoryConsumerBuild();
        string before = HistoryIsolationStamp();
        object?[] entries = ReflectionTools.Enumerate(ReflectionTools.Get(NativeCombatHistory, "Entries")).ToArray();
        CombatHistorySnapshot saved = CaptureCombatHistoryCore();
        if (before != HistoryIsolationStamp() || !entries.SequenceEqual(ReflectionTools.Enumerate(ReflectionTools.Get(NativeCombatHistory, "Entries")), ReferenceEqualityComparer.Instance))
            throw HistoryReferenceError("History.Capture", "History capture changed live state/RNG/history membership.");
        return saved;
    }

    private CombatHistorySnapshot CaptureCombatHistoryCore()
    {
        object history = NativeCombatHistory;
        var objects = new List<HistoryObject>();
        var identities = new Dictionary<object, int>(ReferenceEqualityComparer.Instance);
        var cards = new HashSet<object>(ReferenceEqualityComparer.Instance);
        foreach (string pile in new[] { "Hand", "DrawPile", "DiscardPile", "ExhaustPile", "PlayPile" })
            foreach (object? card in ReflectionTools.Enumerate(ReflectionTools.Get(ReflectionTools.Get(_pcs!, pile)!, "Cards")))
                if (card is not null) cards.Add(card);
        object[] creatures = ReflectionTools.Enumerate(ReflectionTools.Get(_combat!, "Creatures")).Cast<object>().ToArray();
        object[] players = ReflectionTools.Enumerate(ReflectionTools.Get(_combat!, "Players")).Cast<object>().ToArray();
        var moveOwners = new Dictionary<object, object>(ReferenceEqualityComparer.Instance);
        void MoveOwner(object move, object monster, string path)
        {
            if (moveOwners.TryGetValue(move, out object? prior) && !ReferenceEquals(prior, monster))
                throw HistoryReferenceError(path, "Move has conflicting native Monster associations.");
            moveOwners[move] = monster;
        }
        foreach (object? entry in ReflectionTools.Enumerate(ReflectionTools.Get(history, "Entries")))
            if (entry?.GetType() == T(HistoryEntryNamespace + "MonsterPerformedMoveEntry"))
                MoveOwner(ReflectionTools.Get(entry, "Move")!, ReflectionTools.Get(entry, "Monster")!, "MonsterPerformedMoveEntry.Move");
        int? Reference(object? value, string kind, string path)
        {
            if (value is null) return null;
            if (identities.TryGetValue(value, out int existing))
            {
                if (objects[existing - 1].Kind != kind) throw HistoryReferenceError(path, "Conflicting reference kinds.");
                return existing;
            }
            int id = objects.Count + 1;
            identities.Add(value, id);
            objects.Add(new(kind, value.GetType().FullName!, null, null, new()));
            string? binding = null, modelId = null;
            HistoricalPotion? historicalPotion = null;
            HistoricalAffliction? historicalAffliction = null;
            HistoricalPower? historicalPower = null;
            HistoricalOrb? historicalOrb = null;
            HistoricalCreature? historicalCreature = null;
            HistoricalMonster? historicalMonster = null;
            HistoricalMove? historicalMove = null;
            var fields = new Dictionary<string, JsonElement>();
            switch (kind)
            {
                case "Card":
                    if (!cards.Contains(value) || !ReferenceEquals(ReflectionTools.Get(value, "Owner"), _player)
                        || Convert.ToBoolean(ReflectionTools.Get(value, "IsDupe")))
                        throw HistoryReferenceError(path, $"Card {Entry(value)} is removed, foreign-owned, or a dupe outside the existing card codec.");
                    binding = GetCardInstanceId(value); modelId = Entry(value); break;
                case "Creature":
                    if (!creatures.Any(c => ReferenceEquals(c, value)))
                    {
                        ValidateHistoricalCreatureAbi(value, path);
                        object? removedMonster = ReflectionTools.Get(value, "Monster");
                        if (removedMonster is null || ReflectionTools.Get(value, "CombatState") is not null
                            || ReflectionTools.Get(value, "Player") is not null || ReflectionTools.Get(value, "PetOwner") is not null
                            || (int)ReflectionTools.Get(value, "CurrentHp")! != 0 || ReflectionTools.Get(value, "Side")!.ToString() != "Enemy")
                            throw HistoryReferenceError(path, "Only actual detached dead enemy Creatures are supported.");
                        historicalCreature = new((uint)ReflectionTools.Get(value, "CombatId")!,
                            Reference(removedMonster, "Monster", path + ".Monster")!.Value,
                            ReflectionTools.Get(value, "Side")!.ToString()!, (int)ReflectionTools.Get(value, "CurrentHp")!,
                            (int)ReflectionTools.Get(value, "MaxHp")!, (int)ReflectionTools.Get(value, "Block")!,
                            (string?)ReflectionTools.Get(value, "SlotName"), (int?)ReflectionTools.Get(value, "MonsterMaxHpBeforeModification"),
                            ReflectionTools.Get(value, "HpDisplay")!.ToString()!, null, null, false,
                            ReflectionTools.Enumerate(ReflectionTools.Get(value, "Powers"))
                                .Select((p, i) => Reference(p, "Power", path + $".Powers[{i}]")!.Value).ToList());
                    }
                    else binding = CreatureIdentity(value).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "Player":
                    if (!players.Any(p => ReferenceEquals(p, value))) throw HistoryReferenceError(path, "Player is outside this combat.");
                    binding = ((ulong)ReflectionTools.Get(value, "NetId")!).ToString(System.Globalization.CultureInfo.InvariantCulture); break;
                case "Monster":
                    object creature = ReflectionTools.Get(value, "Creature")!;
                    modelId = Entry(value);
                    if (!creatures.Any(c => ReferenceEquals(c, creature)))
                    {
                        ValidateHistoryModelIdentity(value, kind, path);
                        if (ReflectionTools.Get(value, "MoveStateMachine") is not null || (bool)ReflectionTools.Get(value, "IsPerformingMove")!)
                            throw HistoryReferenceError(path, "Detached Monster must have its torn-down FSM and no executing move.");
                        historicalMonster = new(Reference(creature, "Creature", path + ".Creature")!.Value);
                    }
                    else binding = CreatureIdentity(creature).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "Power":
                    object? owner = ReflectionTools.Get(value, "Owner");
                    int powerIndex = ReferenceIndex(owner is null ? null : ReflectionTools.Get(owner, "Powers"), value);
                    modelId = Entry(value);
                    if (owner is not null && creatures.Any(c => ReferenceEquals(c, owner)) && powerIndex >= 0)
                        binding = $"{CreatureIdentity(owner)}/{powerIndex}";
                    else
                    {
                        ValidateHistoryModelIdentity(value, kind, path);
                        historicalPower = new(Reference(owner, "Creature", path + ".Owner"),
                            Reference(ReflectionTools.Get(value, "Applier"), "Creature", path + ".Applier"),
                            Reference(ReflectionTools.Get(value, "Target"), "Creature", path + ".Target"),
                            (int)ReflectionTools.Get(value, "Amount")!, (int)ReflectionTools.Get(value, "AmountOnTurnStart")!,
                            (bool)ReflectionTools.Get(value, "SkipNextDurationTick")!,
                            HasReviewedPowerRuntime(modelId) ? CapturePowerDynamicVars(value, path + ".DynamicVars") : null,
                            (string?)ReflectionTools.Get(value, "_resolvedBigIconPath"));
                    }
                    break;
                case "Orb":
                    int orbIndex = ReferenceIndex(ReflectionTools.Get(ReflectionTools.Get(_pcs!, "OrbQueue")!, "Orbs"), value);
                    modelId = Entry(value);
                    if (orbIndex >= 0 && ReferenceEquals(ReflectionTools.Get(value, "Owner"), _player))
                        binding = orbIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    else {
                        ValidateHistoryModelIdentity(value, kind, path);
                        historicalOrb = new(Reference(ReflectionTools.Get(value, "Owner"), "Player", path + ".Owner"));
                    }
                    break;
                case "Potion":
                    int potionIndex = ReferenceIndex(ReflectionTools.Get(_player!, "PotionSlots"), value);
                    modelId = Entry(value);
                    if (potionIndex >= 0) binding = potionIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    else
                    {
                        ValidateHistoryModelIdentity(value, kind, path);
                        historicalPotion = new(Reference(ReflectionTools.Get(value, "Owner"), "Player", path + ".Owner"),
                            (bool)ReflectionTools.Get(value, "IsQueued")!, (bool)ReflectionTools.Get(value, "HasBeenRemovedFromState")!);
                    }
                    break;
                case "Affliction":
                    object? afflicted = ReflectionTools.Get(value, "Card");
                    modelId = Entry(value);
                    if (afflicted is not null && cards.Contains(afflicted) && ReferenceEquals(ReflectionTools.Get(afflicted, "Affliction"), value))
                        binding = GetCardInstanceId(afflicted);
                    else
                    {
                        ValidateHistoryModelIdentity(value, kind, path);
                        historicalAffliction = new(Reference(afflicted, "Card", path + ".Card"),
                            (int)ReflectionTools.Get(value, "Amount")!);
                    }
                    break;
                case "Move":
                    Type moveType = T("MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine.MoveState");
                    if (value.GetType() != moveType) throw HistoryReferenceError(path, "Unknown native MoveState subclass.");
                    object[] matches = creatures.Where(c => ReflectionTools.Get(c, "Monster") is { } m
                        && ReflectionTools.Get(m, "MoveStateMachine") is { } machine
                        && ReflectionTools.Enumerate(ReflectionTools.Get(machine, "States"))
                            .Any(pair => ReferenceEquals(ReflectionTools.Get(pair!, "Value"), value))).ToArray();
                    if (matches.Length > 1) throw HistoryReferenceError(path, "Move has ambiguous resident owners.");
                    if (matches.Length == 1) {
                        object residentOwner = ReflectionTools.Get(matches[0], "Monster")!;
                        MoveOwner(value, residentOwner, path);
                        binding = $"{CreatureIdentity(matches[0])}/{ReflectionTools.Get(value, "Id")}";
                    }
                    else {
                        if (!moveOwners.TryGetValue(value, out object? originalOwner))
                            throw HistoryReferenceError(path, "Move has no original Monster association.");
                        historicalMove = new(Reference(originalOwner, "Monster", path + ".Monster")!.Value,
                            (string)ReflectionTools.Get(value, "Id")!);
                    }
                    break;
                case "CardPlay":
                    fields = CaptureFields(value, HistoryCardPlayFields, path); break;
                case "DamageResult":
                    fields = CaptureFields(value, HistoryDamageFields, path); break;
                default: throw HistoryReferenceError(path, $"Unknown object kind {kind}.");
            }
            objects[id - 1] = new(kind, value.GetType().FullName!, binding, modelId, fields,
                historicalPotion, historicalAffliction, historicalPower, historicalOrb, historicalCreature, historicalMonster, historicalMove);
            return id;
        }
        Dictionary<string, JsonElement> CaptureFields(object native, Dictionary<string, string> schema, string path)
        {
            var fields = new Dictionary<string, JsonElement>();
            foreach ((string name, string shape) in schema)
            {
                object source = native.GetType() == T("MegaCrit.Sts2.Core.Entities.Cards.CardPlay")
                    && name is "EnergySpent" or "EnergyValue" or "StarsSpent" or "StarValue"
                    ? ReflectionTools.Get(native, "Resources")! : native;
                object? value = ReflectionTools.Get(source, name);
                string fieldPath = $"{path}.{name}";
                if (shape is "int" or "bool" or "decimal") fields[name] = JsonSerializer.SerializeToElement(value);
                else if (shape == "props") fields[name] = JsonSerializer.SerializeToElement(Convert.ToInt64(value));
                else if (shape == "pile") fields[name] = JsonSerializer.SerializeToElement(value!.ToString());
                else if (shape.Contains("[]", StringComparison.Ordinal))
                    fields[name] = value is null ? JsonSerializer.SerializeToElement<object?>(null)
                        : JsonSerializer.SerializeToElement(ReflectionTools.Enumerate(value).Select((v, i) => Reference(v, shape.Split('[')[0], $"{fieldPath}[{i}]")).ToArray());
                else fields[name] = JsonSerializer.SerializeToElement(Reference(value, shape.TrimEnd('?'), fieldPath));
            }
            return fields;
        }
        var entries = new List<HistoryEntry>();
        foreach (object? native in ReflectionTools.Enumerate(ReflectionTools.Get(history, "Entries")))
        {
            string path = $"History.Entries[{entries.Count}]";
            if (native is null || !HistoryEntryFields.TryGetValue(native.GetType().Name, out var schema)
                || native.GetType() != T(HistoryEntryNamespace + native.GetType().Name))
                throw new ProtocolException("unsupported_history_entry", $"{path}: unknown native entry {native?.GetType().FullName}.");
            if (!ReferenceEquals(ReflectionTools.Get(native, "History"), history))
                throw new ProtocolException("invalid_combat_history", $"{path}: different History owner.");
            entries.Add(new(native.GetType().Name, Reference(ReflectionTools.Get(native, "Actor"), "Creature", path + ".Actor"),
                (int)ReflectionTools.Get(native, "RoundNumber")!, ReflectionTools.Get(native, "CurrentSide")!.ToString()!,
                new((Dictionary<ulong, int>)ReflectionTools.Get(native, "_playerTurnNumbers")!), CaptureFields(native, schema, path)));
        }
        return new(CombatHistoryContractVersion, HistoryConsumerContract, CreatureIdentity(ReflectionTools.Get(_player!, "Creature")!),
            (ulong)ReflectionTools.Get(_player!, "NetId")!, objects, entries);
    }

    private static void ExactInstanceFields(Type type, IEnumerable<string> names, string path)
    {
        var expected = names.ToHashSet(StringComparer.Ordinal);
        var actual = type.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (actual.Length != expected.Count || actual.Any(f => !expected.Contains(f.Name)))
            throw HistoryReferenceError(path, "Unclassified native storage: " + string.Join(",", actual.Select(f => f.Name)));
    }

    private void ValidateHistoricalCreatureAbi(object creature, string path)
    {
        Type type = T("MegaCrit.Sts2.Core.Entities.Creatures.Creature");
        if (creature.GetType() != type) throw HistoryReferenceError(path, "Unknown native Creature subclass.");
        ExactInstanceFields(type, ["_block", "_currentHp", "_maxHp", "_powers", "_petOwner",
            "<MonsterMaxHpBeforeModification>k__BackingField", "<CombatId>k__BackingField", "<Monster>k__BackingField",
            "<Player>k__BackingField", "<Side>k__BackingField", "<CombatState>k__BackingField", "<HpDisplay>k__BackingField",
            "<SlotName>k__BackingField", "BlockChanged", "CurrentHpChanged", "MaxHpChanged", "PowerApplied", "PowerIncreased",
            "PowerDecreased", "PowerRemoved", "Died", "Revived"], path);
    }

    private static string HistoryModelCollection(string kind) => kind switch {
        "Card" => "AllCards", "Monster" => "Monsters", "Power" => "AllPowers", "Orb" => "Orbs",
        "Potion" => "AllPotions", "Affliction" => "DebugAfflictions",
        _ => throw HistoryReferenceError("History.Model", "Unknown native model family " + kind)
    };

    private object HistoryCanonicalModel(string kind, string modelId) =>
        Find(ReflectionTools.GetStatic(T("MegaCrit.Sts2.Core.Models.ModelDb"), HistoryModelCollection(kind))!, modelId);

    private void ValidateHistoryModelIdentity(object model, string kind, string path)
    {
        object canonical = HistoryCanonicalModel(kind, Entry(model));
        if (model.GetType() != canonical.GetType() || model.GetType().Assembly != T("MegaCrit.Sts2.Core.Models.AbstractModel").Assembly)
            throw HistoryReferenceError(path, "External or noncanonical native model type.");
    }

    // A native type/ID value carrier, explicitly not an executable private-state clone.
    // Bypass constructors, ToMutable/DeepCloneFields/AfterCloned and HP/intent getters.
    private object MutableHistoricalModel(HistoryObject saved)
    {
        object canonical = HistoryCanonicalModel(saved.Kind, saved.ModelId!);
        if (canonical.GetType().FullName != saved.NativeType || canonical.GetType().Assembly != T("MegaCrit.Sts2.Core.Models.AbstractModel").Assembly)
            throw HistoryReferenceError("History.Model", "External or noncanonical native model type.");
        object model = RuntimeHelpers.GetUninitializedObject(canonical.GetType());
        foreach (string name in new[] { "Id", "CategorySortingId", "EntrySortingId" })
            ReflectionTools.Set(model, "<" + name + ">k__BackingField", ReflectionTools.Get(canonical, name));
        ReflectionTools.Set(model, "<IsMutable>k__BackingField", true);
        ReflectionTools.Set(model, "_canonicalInstance", canonical);
        return model;
    }

    private object BuildHistoricalMove(HistoricalMove saved)
    {
        Type move = T("MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine.MoveState");
        Type targets = typeof(IReadOnlyList<>).MakeGenericType(T("MegaCrit.Sts2.Core.Entities.Creatures.Creature"));
        Type behavior = typeof(Func<,>).MakeGenericType(targets, typeof(Task));
        var arg = System.Linq.Expressions.Expression.Parameter(targets, "targets");
        var guard = System.Linq.Expressions.Expression.Lambda(behavior,
            System.Linq.Expressions.Expression.Call(typeof(PersistentNativeCombatEnvironment).GetMethod(nameof(RejectHistoricalMoveExecution), BindingFlags.Static | BindingFlags.NonPublic)!), arg).Compile();
        return ReflectionTools.Create(move, saved.MoveId, guard, ReflectionTools.EmptyArray(T("MegaCrit.Sts2.Core.MonsterMoves.Intents.AbstractIntent")));
    }

    private static Task RejectHistoricalMoveExecution() =>
        throw new ProtocolException("unsupported_history_execution", "History-only Move cannot execute or enter a live FSM.");

    private void ValidateResidentPowerAbi(object model, string kind, string path)
    {
        if (kind == "Power" && (Entry(model), model.GetType().FullName) is not
            (("DUPLICATION_POWER", "MegaCrit.Sts2.Core.Models.Powers.DuplicationPower")
            or ("MINION_POWER", "MegaCrit.Sts2.Core.Models.Powers.MinionPower")
            or ("STRENGTH_POWER", "MegaCrit.Sts2.Core.Models.Powers.StrengthPower")
            or ("WEAK_POWER", "MegaCrit.Sts2.Core.Models.Powers.WeakPower")
            or ("VULNERABLE_POWER", "MegaCrit.Sts2.Core.Models.Powers.VulnerablePower")))
            throw HistoryReferenceError(path, $"Power {Entry(model)} has no reviewed resident runtime carrier.");
        Type root = T("MegaCrit.Sts2.Core.Models." + kind + "Model");
        string[] owned = kind == "Potion" ? ["_owner", "_dynamicVars", "_canonicalInstance", "<IsQueued>k__BackingField", "<HasBeenRemovedFromState>k__BackingField", "BeforeUse"]
            : kind == "Power" ? ["_resolvedBigIconPath", "_amount", "_amountOnTurnStart", "_skipNextDurationTick", "_owner", "_applier", "_target", "_dynamicVars", "_internalData", "_canonicalInstance", "PulsingStarted", "PulsingStopped", "Flashed", "DisplayAmountChanged", "Removed"]
            : ["_card", "_amount", "_canonicalInstance", "AmountChanged"];
        foreach (FieldInfo field in root.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            if (!owned.Contains(field.Name, StringComparer.Ordinal))
                throw HistoryReferenceError(path, $"Unclassified {kind} base member {field.Name}.");
        // These families have bounded native value carriers, not an object-graph codec.
        // Extra concrete storage, including readonly containers, requires a reviewed carrier.
        for (Type? type = model.GetType(); type is not null && type != root; type = type.BaseType)
            foreach (FieldInfo field in type.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                if (!field.IsLiteral)
                    throw HistoryReferenceError(path + "." + field.Name, $"Concrete model storage is outside the historical {kind} carrier.");
    }

    private static int ReferenceIndex(object? sequence, object target)
    {
        var values = ReflectionTools.Enumerate(sequence);
        for (int i = 0; i < values.Count; i++) if (ReferenceEquals(values[i], target)) return i;
        return -1;
    }

    private void ValidateCombatHistory(CombatSnapshot snapshot)
    {
        ValidateHistoryConsumerBuild();
        CombatHistorySnapshot saved = snapshot.CombatHistory
            ?? throw new ProtocolException("unsupported_combat_history", "An explicit shared history payload is required, including empty history.");
        if (saved.Version != CombatHistoryContractVersion || saved.ConsumerContract != HistoryConsumerContract || saved.Objects is null || saved.Entries is null)
            throw new ProtocolException("unsupported_combat_history", "History contract version/collections mismatch.");
        var cards = snapshot.Hand.Concat(snapshot.DrawPile).Concat(snapshot.DiscardPile).Concat(snapshot.ExhaustPile).Concat(snapshot.PlayPile).ToDictionary(c => c.InstanceId);
        foreach (PowerSnapshot power in snapshot.PlayerPowers.Concat(snapshot.Enemies.SelectMany(e => e.Powers))
            .Concat(snapshot.OstyEntity?.Entity?.Powers ?? []))
        {
            ValidateBoundPowerSnapshot(power);
            if (power.BoundCardPlayed.HasValue) _ = BoundPowerData(Mutable("AllPowers", power.ModelId));
        }
        var creatureIds = snapshot.Enemies.Select(e => e.CombatId).ToHashSet();
        if (snapshot.OstyEntity?.Entity is { } osty) creatureIds.Add(osty.CombatId);
        // Descriptor-only: never inspect the destination worker's previous combat.
        uint playerId = saved.PlayerCombatId;
        ulong netId = saved.PlayerNetId;
        if (playerId != 0 || netId != 1 || creatureIds.Contains(playerId))
            throw new ProtocolException("invalid_combat_history", "History player binding is outside the pinned single-player initialization profile.");
        creatureIds.Add(playerId);
        foreach (PowerSnapshot power in snapshot.PlayerPowers.Concat(snapshot.Enemies.SelectMany(e => e.Powers))
            .Concat(snapshot.OstyEntity?.Entity?.Powers ?? []))
        {
            if (HasReviewedPowerRuntime(power.ModelId) != (power.Runtime is not null))
                throw new ProtocolException("unsupported_power_runtime", "Reviewed resident Power requires its exact basic runtime state.");
            if (power.Runtime is not { } runtime) continue;
            if (runtime.ApplierCombatId is uint a && !creatureIds.Contains(a)
                || runtime.TargetCombatId is uint t && !creatureIds.Contains(t))
                throw new ProtocolException("invalid_history_reference", "Resident Power Applier/Target is absent from the saved Creatures.");
            object model = ReflectionTools.Invoke(Find(ReflectionTools.GetStatic(T("MegaCrit.Sts2.Core.Models.ModelDb"), "AllPowers")!, power.ModelId), "ToMutable", 0)!;
            ValidateResidentPowerAbi(model, "Power", "Resident.Power");
            ValidatePowerDynamicVars(power.ModelId, runtime.DynamicVars, "Resident.Power.DynamicVars");
        }
        List<PowerSnapshot>? Powers(uint id) => id == playerId ? snapshot.PlayerPowers
            : snapshot.OstyEntity?.Entity is { } pet && pet.CombatId == id ? pet.Powers
            : snapshot.Enemies.SingleOrDefault(e => e.CombatId == id)?.Powers;
        void Ref(JsonElement value, string kind, bool nullable, string path)
        {
            if (nullable && value.ValueKind == JsonValueKind.Null) return;
            if (!value.TryGetInt32(out int id) || id < 1 || id > saved.Objects.Count || saved.Objects[id - 1]?.Kind != kind)
                throw new ProtocolException("invalid_history_reference", $"{path}: dangling or wrong-kind {kind} reference.");
        }
        void Fields(Dictionary<string, JsonElement>? fields, Dictionary<string, string> schema, string path)
        {
            if (fields is null || fields.Count != schema.Count || schema.Keys.Any(k => !fields.ContainsKey(k)))
                throw new ProtocolException("invalid_combat_history", $"{path}: exact field coverage required.");
            foreach ((string name, string shape) in schema)
            {
                JsonElement value = fields[name]; string p = path + "." + name;
                switch (shape)
                {
                    case "int": value.GetInt32(); break;
                    case "bool": value.GetBoolean(); break;
                    case "decimal": value.GetDecimal(); break;
                    case "props": Enum.ToObject(T("MegaCrit.Sts2.Core.ValueProps.ValueProp"), value.GetInt64()); break;
                    case "pile":
                        if (!Enum.IsDefined(T("MegaCrit.Sts2.Core.Entities.Cards.PileType"), value.GetString()!)) throw new ProtocolException("invalid_combat_history", p);
                        break;
                    default:
                        bool nullable = shape.EndsWith('?');
                        if (shape.Contains("[]", StringComparison.Ordinal))
                        {
                            if (nullable && value.ValueKind == JsonValueKind.Null) break;
                            foreach (JsonElement reference in value.EnumerateArray()) Ref(reference, shape.Split('[')[0], false, p);
                        }
                        else Ref(value, shape.TrimEnd('?'), nullable, p);
                        break;
                }
            }
        }
        var bindings = new HashSet<string>(StringComparer.Ordinal);
        var historicalIds = new HashSet<uint>();
        try
        {
            for (int i = 0; i < saved.Objects.Count; i++)
            {
                HistoryObject o = saved.Objects[i]; string path = $"History.Objects[{i + 1}]";
                if (o is null || o.Fields is null
                    || o.Kind != "Potion" && o.Potion is not null || o.Kind != "Affliction" && o.Affliction is not null
                    || o.Kind != "Orb" && o.Orb is not null || o.Kind != "Power" && o.Power is not null || o.Kind != "Creature" && o.Creature is not null
                    || o.Kind != "Monster" && o.Monster is not null || o.Kind != "Move" && o.Move is not null)
                    throw new ProtocolException("invalid_combat_history", path);
                int historicalForms = (o.Potion is null ? 0 : 1) + (o.Affliction is null ? 0 : 1) + (o.Power is null ? 0 : 1)
                    + (o.Orb is null ? 0 : 1) + (o.Creature is null ? 0 : 1) + (o.Monster is null ? 0 : 1) + (o.Move is null ? 0 : 1);
                if (historicalForms > 1 || historicalForms == 1 && (o.Binding is not null || o.Fields.Count != 0))
                    throw new ProtocolException("invalid_combat_history", path + ": resident and historical forms are mutually exclusive.");
                if (o.Kind is "CardPlay" or "DamageResult")
                {
                    string type = o.Kind == "CardPlay" ? "MegaCrit.Sts2.Core.Entities.Cards.CardPlay" : "MegaCrit.Sts2.Core.Entities.Creatures.DamageResult";
                    if (o.NativeType != type || o.Binding is not null || o.ModelId is not null) throw new ProtocolException("invalid_combat_history", path);
                    Fields(o.Fields, o.Kind == "CardPlay" ? HistoryCardPlayFields : HistoryDamageFields, path);
                    if (o.Kind == "CardPlay" && (o.Fields["PlayCount"].GetInt32() < 1 || o.Fields["PlayIndex"].GetInt32() < 0 || o.Fields["PlayIndex"].GetInt32() >= o.Fields["PlayCount"].GetInt32()))
                        throw new ProtocolException("invalid_combat_history", path + ": invalid replay series.");
                    continue;
                }
                if (o.Creature is { } dead)
                {
                    if (o.NativeType != "MegaCrit.Sts2.Core.Entities.Creatures.Creature" || o.ModelId is not null
                        || creatureIds.Contains(dead.CombatId) || dead.CombatId >= snapshot.NextCreatureId || !historicalIds.Add(dead.CombatId)
                        || dead.CurrentHp != 0 || dead.MaxHp < 1 || dead.Block < 0 || dead.Side != "Enemy"
                        || dead.Player is not null || dead.PetOwner is not null || dead.HasCombatState || dead.Powers is null
                        || !Enum.IsDefined(T("MegaCrit.Sts2.Core.Entities.Creatures.HpDisplay"), dead.HpDisplay))
                        throw new ProtocolException("invalid_combat_history", path + ": invalid detached Creature state/identity.");
                    Ref(JsonSerializer.SerializeToElement(dead.Monster), "Monster", false, path + ".Creature.Monster");
                    if (saved.Objects[dead.Monster - 1].Monster?.Creature != i + 1)
                        throw new ProtocolException("invalid_combat_history", path + ": nonreciprocal Creature/Monster references.");
                    if (dead.Powers.Distinct().Count() != dead.Powers.Count)
                        throw new ProtocolException("invalid_combat_history", path + ": duplicate corpse Power membership.");
                    foreach (int power in dead.Powers)
                    {
                        Ref(JsonSerializer.SerializeToElement(power), "Power", false, path + ".Creature.Powers");
                        if (saved.Objects[power - 1].Power?.Owner != i + 1)
                            throw new ProtocolException("invalid_combat_history", path + ": corpse Power owner mismatch.");
                    }
                    continue;
                }
                if (o.Monster is { } removed)
                {
                    Ref(JsonSerializer.SerializeToElement(removed.Creature), "Creature", false, path + ".Monster.Creature");
                    if (saved.Objects[removed.Creature - 1].Creature?.Monster != i + 1 || o.ModelId is null)
                        throw new ProtocolException("invalid_combat_history", path + ": nonreciprocal Monster/Creature reference.");
                    object canonical = HistoryCanonicalModel(o.Kind, o.ModelId);
                    ValidateHistoryModelIdentity(canonical, o.Kind, path);
                    if (canonical.GetType().FullName != o.NativeType) throw new ProtocolException("invalid_combat_history", path + ": model/type mismatch.");
                    continue;
                }
                if (o.Move is { } move)
                {
                    Ref(JsonSerializer.SerializeToElement(move.Monster), "Monster", false, path + ".Move.Monster");
                    if (o.NativeType != "MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine.MoveState" || o.ModelId is not null || string.IsNullOrWhiteSpace(move.MoveId))
                        throw new ProtocolException("invalid_combat_history", path + ": historical Move native type/ID mismatch.");
                    continue;
                }
                if (o.Potion is not null || o.Affliction is not null || o.Power is not null || o.Orb is not null)
                {
                    if (o.ModelId is null) throw new ProtocolException("invalid_combat_history", path + ": missing model ID.");
                    object canonical = HistoryCanonicalModel(o.Kind, o.ModelId);
                    ValidateHistoryModelIdentity(canonical, o.Kind, path);
                    if (canonical.GetType().FullName != o.NativeType) throw new ProtocolException("invalid_combat_history", path + ": model/type mismatch.");
                    if (o.Potion is { } potion) Ref(JsonSerializer.SerializeToElement(potion.Owner), "Player", true, path + ".Potion.Owner");
                    if (o.Orb is { } orb) Ref(JsonSerializer.SerializeToElement(orb.Owner), "Player", true, path + ".Orb.Owner");
                    if (o.Affliction is { } affliction) Ref(JsonSerializer.SerializeToElement(affliction.Card), "Card", true, path + ".Affliction.Card");
                    if (o.Power is { } power) {
                        Ref(JsonSerializer.SerializeToElement(power.Owner), "Creature", true, path + ".Power.Owner");
                        Ref(JsonSerializer.SerializeToElement(power.Applier), "Creature", true, path + ".Power.Applier");
                        Ref(JsonSerializer.SerializeToElement(power.Target), "Creature", true, path + ".Power.Target");
                        if (HasReviewedPowerRuntime(o.ModelId)) ValidatePowerDynamicVars(o.ModelId, power.DynamicVars, path + ".Power.DynamicVars");
                        else if (power.DynamicVars is not null) throw HistoryReferenceError(path, "Read-only Power has no executable DynamicVar carrier.");
                    }
                    continue;
                }
                if (o.Binding is null || o.Fields.Count != 0 || !bindings.Add(o.Kind + ":" + o.Binding))
                    throw new ProtocolException("invalid_combat_history", path + ": ambiguous binding.");
                bool valid;
                switch (o.Kind)
                {
                    case "Card": valid = cards.TryGetValue(o.Binding, out var card) && card.ModelId == o.ModelId; break;
                    case "Creature": valid = uint.TryParse(o.Binding, out uint c) && creatureIds.Contains(c); break;
                    case "Player": valid = ulong.TryParse(o.Binding, out ulong p) && p == netId; break;
                    case "Monster": valid = uint.TryParse(o.Binding, out uint m) && (snapshot.Enemies.Any(e => e.CombatId == m && e.ModelId == o.ModelId) || snapshot.OstyEntity?.Entity is { } pet && pet.CombatId == m && pet.ModelId == o.ModelId); break;
                    case "Power":
                        string[] powerParts = o.Binding.Split('/');
                        valid = powerParts.Length == 2 && uint.TryParse(powerParts[0], out uint owner) && int.TryParse(powerParts[1], out int index)
                            && Powers(owner) is { } powers && index >= 0 && index < powers.Count && powers[index].ModelId == o.ModelId; break;
                    case "Orb": valid = int.TryParse(o.Binding, out int orb) && snapshot.Orbs is { } queue && orb >= 0 && orb < queue.Orbs.Count && queue.Orbs[orb].ModelId == o.ModelId; break;
                    case "Potion": valid = int.TryParse(o.Binding, out int potion) && potion >= 0 && potion < snapshot.PotionSlots.Count && snapshot.PotionSlots[potion] == o.ModelId; break;
                    case "Affliction": valid = cards.TryGetValue(o.Binding, out var afflicted) && afflicted.AfflictionModelId == o.ModelId; break;
                    case "Move":
                        string[] moveParts = o.Binding.Split('/', 2);
                        uint monster = 0;
                        valid = moveParts.Length == 2 && uint.TryParse(moveParts[0], out monster) && creatureIds.Contains(monster) && o.ModelId is null;
                        if (o.NativeType != "MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine.MoveState") valid = false;
                        break;
                    default: throw new ProtocolException("unsupported_history_reference", path + ": unknown kind " + o.Kind);
                }
                if (!valid) throw HistoryReferenceError(path, $"Cannot bind {o.Kind} {o.Binding} ({o.ModelId}) to the saved resident state.");
                string baseType = o.Kind switch
                {
                    "Creature" => "MegaCrit.Sts2.Core.Entities.Creatures.Creature", "Player" => "MegaCrit.Sts2.Core.Entities.Players.Player",
                    "Move" => "MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine.MoveState", _ => "MegaCrit.Sts2.Core.Models." + o.Kind + "Model"
                };
                if (!T(baseType).IsAssignableFrom(T(o.NativeType))) throw new ProtocolException("invalid_combat_history", path + ": native type mismatch.");
                if (o.ModelId is not null)
                {
                    string collection = o.Kind switch
                    {
                        "Card" => "AllCards", "Power" => "AllPowers", "Orb" => "Orbs",
                        "Potion" => "AllPotions", "Affliction" => "DebugAfflictions", "Monster" => "Monsters",
                        _ => throw new ProtocolException("invalid_combat_history", path)
                    };
                    Type modelType = o.Kind == "Monster" && o.ModelId == "OSTY" ? T("MegaCrit.Sts2.Core.Models.Monsters.Osty")
                        : Find(ReflectionTools.GetStatic(T("MegaCrit.Sts2.Core.Models.ModelDb"), collection)!, o.ModelId).GetType();
                    if (o.NativeType != modelType.FullName) throw new ProtocolException("invalid_combat_history", path + ": canonical model/type mismatch.");
                }
                else if (o.Kind is "Creature" or "Player" && o.NativeType != T(baseType).FullName)
                    throw new ProtocolException("invalid_combat_history", path + ": native binding type mismatch.");
            }
            foreach (HistoryEntry e in saved.Entries)
            {
                if (e is null || !HistoryEntryFields.TryGetValue(e.Type, out var schema)) throw new ProtocolException("unsupported_history_entry", "Unknown history entry type.");
                if (e.RoundNumber < 0 || !Enum.IsDefined(T("MegaCrit.Sts2.Core.Combat.CombatSide"), e.CurrentSide)
                    || e.PlayerTurnNumbers is null || e.PlayerTurnNumbers.Count != 1 || !e.PlayerTurnNumbers.TryGetValue(netId, out int turn) || turn < 0)
                    throw new ProtocolException("invalid_combat_history", "Invalid historical round/side/player turn markers.");
                Ref(JsonSerializer.SerializeToElement(e.Actor), "Creature", true, e.Type + ".Actor");
                Fields(e.Fields, schema, e.Type);
                if (e.Type == "MonsterPerformedMoveEntry")
                {
                    HistoryObject monster = saved.Objects[e.Fields["Monster"].GetInt32() - 1];
                    HistoryObject move = saved.Objects[e.Fields["Move"].GetInt32() - 1];
                    HistoryObject? actor = e.Actor is int actorId ? saved.Objects[actorId - 1] : null;
                    if (actor is null || (monster.Monster is { } removedOwner ? e.Actor != removedOwner.Creature : actor.Binding != monster.Binding))
                        throw new ProtocolException("invalid_combat_history", "MonsterPerformedMoveEntry Actor differs from its original Monster.");
                    if (move.Move is { } historicalMove
                        ? historicalMove.Monster != e.Fields["Monster"].GetInt32()
                        : monster.Binding is null || move.Binding!.Split('/')[0] != monster.Binding)
                        throw new ProtocolException("invalid_combat_history", "MonsterPerformedMoveEntry must retain its original Monster/Move/Actor association.");
                }
            }
        }
        catch (ProtocolException) { throw; }
        catch (Exception ex) { throw new ProtocolException("invalid_combat_history", "Invalid history descriptor: " + ex.Message); }
    }

    private void ValidateCombatHistoryBindings(CombatSnapshot snapshot)
    {
        CombatHistorySnapshot saved = snapshot.CombatHistory!;
        if (CreatureIdentity(ReflectionTools.Get(_player!, "Creature")!) != saved.PlayerCombatId
            || (ulong)ReflectionTools.Get(_player!, "NetId")! != saved.PlayerNetId)
            throw HistoryReferenceError("History.Player", "Restoration base does not match the descriptor player.");
        foreach (HistoryObject move in saved.Objects.Where(o => o.Kind == "Move" && o.Move is null))
        {
            uint id = uint.Parse(move.Binding!.Split('/')[0]);
            object creature = ReflectionTools.Enumerate(ReflectionTools.Get(_combat!, "Creatures"))
                .SingleOrDefault(c => c is not null && CreatureIdentity(c) == id)
                ?? throw HistoryReferenceError(move.Binding!, "Move owner is absent from the restoration base.");
            object monster = ReflectionTools.Get(creature, "Monster")!;
            string? modelId = snapshot.Enemies.SingleOrDefault(e => e.CombatId == id)?.ModelId
                ?? (snapshot.OstyEntity?.Entity is { } pet && pet.CombatId == id ? pet.ModelId : null);
            if (modelId is null || Entry(monster) != modelId) throw HistoryReferenceError(move.Binding!, "Move owner model differs from the saved state.");
            _ = BindHistoryObject(move);
        }
    }

    private object BindHistoryObject(HistoryObject saved)
    {
        object Creature(uint id) => ReflectionTools.Enumerate(ReflectionTools.Get(_combat!, "Creatures"))
            .SingleOrDefault(c => c is not null && CreatureIdentity(c) == id)
            ?? throw HistoryReferenceError(saved.Binding!, "Creature is absent from restored combat.");
        object Card(string id) => _cardInstanceIds.SingleOrDefault(pair => pair.Value == id).Key
            ?? throw HistoryReferenceError(id, "Card is absent from restored card bindings.");
        string b = saved.Binding!;
        object result = saved.Kind switch
        {
            "Creature" => Creature(uint.Parse(b)), "Player" => _player!, "Card" => Card(b),
            "Monster" => ReflectionTools.Get(Creature(uint.Parse(b)), "Monster")!,
            "Power" => ReflectionTools.Enumerate(ReflectionTools.Get(Creature(uint.Parse(b.Split('/')[0])), "Powers"))[int.Parse(b.Split('/')[1])]!,
            "Orb" => ReflectionTools.Enumerate(ReflectionTools.Get(ReflectionTools.Get(_pcs!, "OrbQueue")!, "Orbs"))[int.Parse(b)]!,
            "Potion" => ReflectionTools.Enumerate(ReflectionTools.Get(_player!, "PotionSlots"))[int.Parse(b)]!,
            "Affliction" => ReflectionTools.Get(Card(b), "Affliction")!,
            "Move" => ReflectionTools.Enumerate(ReflectionTools.Get(ReflectionTools.Get(ReflectionTools.Get(Creature(uint.Parse(b.Split('/')[0])), "Monster")!, "MoveStateMachine")!, "States"))
                .Where(pair => (string)ReflectionTools.Get(pair!, "Key")! == b.Split('/', 2)[1]).Select(pair => ReflectionTools.Get(pair!, "Value")!).SingleOrDefault()
                ?? throw HistoryReferenceError(b, "Move is absent from resident FSM."),
            _ => throw HistoryReferenceError(b, "Unknown resident reference kind.")
        };
        if (result.GetType().FullName != saved.NativeType || saved.ModelId is not null && Entry(result) != saved.ModelId)
            throw HistoryReferenceError(b, "Restored reference native type/model mismatch.");
        return result;
    }

    private void RestoreCombatHistory(CombatHistorySnapshot saved)
    {
        object history = NativeCombatHistory;
        string before = HistoryIsolationStamp();
        object?[] priorEntries = ReflectionTools.Enumerate(ReflectionTools.Get(history, "Entries")).ToArray();
        var objects = new object?[saved.Objects.Count];
        object? Ref(JsonElement value) => value.ValueKind == JsonValueKind.Null ? null : objects[value.GetInt32() - 1];
        // Allocate model identities before hydrating any cross-reference. Historical
        // Creatures use explicit native field hydration, never CombatState.CreateCreature.
        for (int i = 0; i < saved.Objects.Count; i++)
        {
            HistoryObject o = saved.Objects[i];
            if (o.Monster is not null) objects[i] = MutableHistoricalModel(o);
            else if (o.Potion is not null || o.Affliction is not null || o.Power is not null || o.Orb is not null) objects[i] = MutableHistoricalModel(o);
            else if (o.Creature is null && o.Move is null && o.Kind is not ("CardPlay" or "DamageResult" or "Move"))
                objects[i] = BindHistoryObject(o);
        }
        for (int i = 0; i < saved.Objects.Count; i++)
        {
            if (saved.Objects[i].Creature is not { } dead) continue;
            object creature = RuntimeHelpers.GetUninitializedObject(T(saved.Objects[i].NativeType));
            ReflectionTools.Set(creature, "<Monster>k__BackingField", objects[dead.Monster - 1]);
            ReflectionTools.Set(objects[dead.Monster - 1]!, "_creature", creature);
            ReflectionTools.Set(creature, "<Side>k__BackingField", Enum.Parse(T("MegaCrit.Sts2.Core.Combat.CombatSide"), dead.Side));
            ReflectionTools.Set(creature, "<SlotName>k__BackingField", dead.SlotName);
            ReflectionTools.Set(creature, "_powers", Activator.CreateInstance(typeof(List<>).MakeGenericType(T("MegaCrit.Sts2.Core.Models.PowerModel"))));
            ReflectionTools.Set(creature, "CombatId", (uint?)dead.CombatId);
            ReflectionTools.Set(creature, "_currentHp", dead.CurrentHp);
            ReflectionTools.Set(creature, "_maxHp", dead.MaxHp);
            ReflectionTools.Set(creature, "_block", dead.Block);
            ReflectionTools.Set(creature, "MonsterMaxHpBeforeModification", dead.MonsterMaxHpBeforeModification);
            ReflectionTools.Set(creature, "HpDisplay", Enum.Parse(T("MegaCrit.Sts2.Core.Entities.Creatures.HpDisplay"), dead.HpDisplay));
            objects[i] = creature;
        }
        for (int i = 0; i < saved.Objects.Count; i++)
        {
            HistoryObject o = saved.Objects[i]; var f = o.Fields;
            if (o.Potion is { } potion)
            {
                object model = objects[i]!;
                ReflectionTools.Set(model, "_owner", potion.Owner is int owner ? objects[owner - 1] : null);
                ReflectionTools.Set(model, "IsQueued", potion.IsQueued);
                ReflectionTools.Set(model, "HasBeenRemovedFromState", potion.HasBeenRemovedFromState);
                ReflectionTools.Set(model, "_dynamicVars", null);
                objects[i] = model;
            }
            else if (o.Orb is { } orb)
            {
                ReflectionTools.Set(objects[i]!, "_owner", orb.Owner is int owner ? objects[owner - 1] : null);
            }
            else if (o.Affliction is { } affliction)
            {
                object model = objects[i]!;
                ReflectionTools.Set(model, "_card", affliction.Card is int card ? objects[card - 1] : null);
                ReflectionTools.Set(model, "_amount", affliction.Amount);
                objects[i] = model;
            }
            else if (o.Power is { } power)
            {
                object model = objects[i]!;
                ReflectionTools.Set(model, "_owner", power.Owner is int owner ? objects[owner - 1] : null);
                ReflectionTools.Set(model, "_applier", power.Applier is int applier ? objects[applier - 1] : null);
                ReflectionTools.Set(model, "_target", power.Target is int target ? objects[target - 1] : null);
                ReflectionTools.Set(model, "_amount", power.Amount);
                ReflectionTools.Set(model, "_amountOnTurnStart", power.AmountOnTurnStart);
                ReflectionTools.Set(model, "_skipNextDurationTick", power.SkipNextDurationTick);
                ReflectionTools.Set(model, "_resolvedBigIconPath", power.ResolvedBigIconPath);
                if (HasReviewedPowerRuntime(o.ModelId!)) RestorePowerDynamicVars(model, power.DynamicVars, "History.Power.DynamicVars");
                objects[i] = model;
            }
        }
        for (int i = 0; i < saved.Objects.Count; i++)
        {
            HistoryObject o = saved.Objects[i];
            if (o.Move is { } move) objects[i] = BuildHistoricalMove(move);
            else if (o.Kind == "Move") objects[i] = BindHistoryObject(o);
        }
        for (int i = 0; i < saved.Objects.Count; i++)
        {
            if (saved.Objects[i].Creature is not { } dead) continue;
            object creature = objects[i]!, monster = objects[dead.Monster - 1]!;
            IList powers = (IList)ReflectionTools.Get(creature, "_powers")!;
            foreach (int power in dead.Powers) powers.Add(objects[power - 1]);
            if (!ReferenceEquals(ReflectionTools.Get(creature, "Monster"), monster) || !ReferenceEquals(ReflectionTools.Get(monster, "Creature"), creature)
                || ReflectionTools.Get(creature, "CombatState") is not null || ReflectionTools.Get(monster, "MoveStateMachine") is not null
                || ReflectionTools.Enumerate(ReflectionTools.Get(_combat!, "Creatures")).Any(c => ReferenceEquals(c, creature))
                || ReflectionTools.Enumerate(ReflectionTools.Get(_combat!, "Creatures")).Any(c =>
                    ReflectionTools.Enumerate(ReflectionTools.Get(c!, "Powers")).Any(p => powers.Contains(p))))
                throw HistoryReferenceError("History.Creature", "Historical identity/alias leaked into the live combat.");
        }
        for (int i = 0; i < saved.Objects.Count; i++)
        {
            HistoryObject o = saved.Objects[i]; var f = o.Fields;
            if (o.Kind == "CardPlay")
            {
                object resources = Activator.CreateInstance(T("MegaCrit.Sts2.Core.Entities.Cards.ResourceInfo"))!;
                foreach (string field in new[] { "EnergySpent", "EnergyValue", "StarsSpent", "StarValue" }) ReflectionTools.Set(resources, field, f[field].GetInt32());
                object play = ReflectionTools.Create(T(o.NativeType));
                ReflectionTools.Set(play, "Card", Ref(f["Card"])); ReflectionTools.Set(play, "Target", Ref(f["Target"]));
                ReflectionTools.Set(play, "ResultPile", Enum.Parse(T("MegaCrit.Sts2.Core.Entities.Cards.PileType"), f["ResultPile"].GetString()!));
                ReflectionTools.Set(play, "Resources", resources); ReflectionTools.Set(play, "IsAutoPlay", f["IsAutoPlay"].GetBoolean());
                ReflectionTools.Set(play, "PlayIndex", f["PlayIndex"].GetInt32()); ReflectionTools.Set(play, "PlayCount", f["PlayCount"].GetInt32()); objects[i] = play;
            }
            else if (o.Kind == "DamageResult")
            {
                object result = ReflectionTools.Create(T(o.NativeType), Ref(f["Receiver"]), Enum.ToObject(T("MegaCrit.Sts2.Core.ValueProps.ValueProp"), f["Props"].GetInt64()));
                foreach (string field in new[] { "BlockedDamage", "UnblockedDamage", "OverkillDamage" }) ReflectionTools.Set(result, field, f[field].GetInt32());
                foreach (string field in new[] { "WasBlockBroken", "WasFullyBlocked", "WasTargetKilled" }) ReflectionTools.Set(result, field, f[field].GetBoolean()); objects[i] = result;
            }
        }
        for (int i = 0; i < saved.Objects.Count; i++)
        {
            HistoryObject descriptor = saved.Objects[i];
            object native = objects[i] ?? throw HistoryReferenceError($"History.Objects[{i + 1}]", "Unallocated native identity.");
            if (native.GetType().FullName != descriptor.NativeType || descriptor.ModelId is not null && Entry(native) != descriptor.ModelId)
                throw HistoryReferenceError($"History.Objects[{i + 1}]", "Hydrated object native type/model mismatch.");
        }
        IList entries = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(T("MegaCrit.Sts2.Core.Combat.History.CombatHistoryEntry")))!;
        object? ArrayRefs(JsonElement value, Type type)
        {
            if (value.ValueKind == JsonValueKind.Null) return null;
            var values = value.EnumerateArray().Select(Ref).ToArray(); Array array = Array.CreateInstance(type, values.Length);
            for (int i = 0; i < values.Length; i++) array.SetValue(values[i], i); return array;
        }
        foreach (HistoryEntry e in saved.Entries)
        {
            object? actor = e.Actor is int id ? objects[id - 1] : null; var f = e.Fields;
            object entry = RuntimeHelpers.GetUninitializedObject(T(HistoryEntryNamespace + e.Type));
            ReflectionTools.Set(entry, "<Actor>k__BackingField", actor);
            ReflectionTools.Set(entry, "<RoundNumber>k__BackingField", e.RoundNumber);
            ReflectionTools.Set(entry, "<CurrentSide>k__BackingField", Enum.Parse(T("MegaCrit.Sts2.Core.Combat.CombatSide"), e.CurrentSide));
            ReflectionTools.Set(entry, "<History>k__BackingField", history);
            ReflectionTools.Set(entry, "_playerTurnNumbers", new Dictionary<ulong, int>(e.PlayerTurnNumbers));
            foreach ((string field, string shape) in HistoryEntryFields[e.Type]) {
                JsonElement value = f[field];
                object? native = shape switch {
                    "int" => value.GetInt32(), "bool" => value.GetBoolean(), "decimal" => value.GetDecimal(),
                    "props" => Enum.ToObject(T("MegaCrit.Sts2.Core.ValueProps.ValueProp"), value.GetInt64()),
                    "Creature[]?" => ArrayRefs(value, T("MegaCrit.Sts2.Core.Entities.Creatures.Creature")),
                    "DamageResult[]" => ArrayRefs(value, T("MegaCrit.Sts2.Core.Entities.Creatures.DamageResult")),
                    _ => Ref(value)
                };
                ReflectionTools.Set(entry, "<" + field + ">k__BackingField", native);
            }
            if (!ReferenceEquals(ReflectionTools.Get(entry, "Actor"), actor) || !ReferenceEquals(ReflectionTools.Get(entry, "History"), history)
                || (int)ReflectionTools.Get(entry, "RoundNumber")! != e.RoundNumber || ReflectionTools.Get(entry, "CurrentSide")!.ToString() != e.CurrentSide)
                throw new ProtocolException("invalid_combat_history", "Native history base ABI failed restoration.");
            entries.Add(entry);
        }
        var live = new HashSet<object>(ReferenceEqualityComparer.Instance);
        void Add(object? value) { if (value is not null) live.Add(value); }
        foreach (object? creature in ReflectionTools.Enumerate(ReflectionTools.Get(_combat!, "Creatures"))) {
            Add(creature);
            foreach (object? power in ReflectionTools.Enumerate(ReflectionTools.Get(creature!, "Powers"))) Add(power);
            if (ReflectionTools.Get(creature!, "Monster") is { } monster) {
                Add(monster); Add(ReflectionTools.Get(monster, "NextMove"));
                if (ReflectionTools.Get(monster, "MoveStateMachine") is { } machine)
                    foreach (object? pair in ReflectionTools.Enumerate(ReflectionTools.Get(machine, "States"))) Add(ReflectionTools.Get(pair!, "Value"));
            }
        }
        foreach (object? potion in ReflectionTools.Enumerate(ReflectionTools.Get(_player!, "PotionSlots"))) Add(potion);
        if (ReflectionTools.Get(_pcs!, "OrbQueue") is { } queue)
            foreach (object? orb in ReflectionTools.Enumerate(ReflectionTools.Get(queue, "Orbs"))) Add(orb);
        foreach (object? card in _cardInstanceIds.Keys) Add(ReflectionTools.Get(card!, "Affliction"));
        for (int i = 0; i < saved.Objects.Count; i++) {
            HistoryObject o = saved.Objects[i];
            if ((o.Creature is not null || o.Monster is not null || o.Move is not null || o.Power is not null || o.Orb is not null || o.Potion is not null || o.Affliction is not null)
                && live.Contains(objects[i]!)) throw HistoryReferenceError("History.Restore", "Read-only carrier leaked into live state.");
        }
        if (before != HistoryIsolationStamp() || !priorEntries.SequenceEqual(ReflectionTools.Enumerate(ReflectionTools.Get(history, "Entries")), ReferenceEqualityComparer.Instance)) throw HistoryReferenceError("History.Restore", "Read-only restoration changed live state/RNG/resources.");
        // One publication; no Changed hook, execution, RNG, reward, or branch-tail append.
        ReflectionTools.Set(history, "_entries", entries);
    }

    private static bool CombatHistoryFingerprintMatches(CombatSnapshot expected, CombatSnapshot actual) =>
        JsonSerializer.Serialize(expected.CombatHistory, PortableRootJson) == JsonSerializer.Serialize(actual.CombatHistory, PortableRootJson);
}
