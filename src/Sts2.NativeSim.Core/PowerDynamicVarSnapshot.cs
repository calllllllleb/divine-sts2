using System.Globalization;
using System.Text.Json.Serialization;

namespace Sts2.NativeSim.Core;

public sealed partial class PersistentNativeCombatEnvironment
{
    private const string ConstantPowerVarType = "MegaCrit.Sts2.Core.Localization.DynamicVars.DynamicVar";
    private const string PowerVarSetType = "MegaCrit.Sts2.Core.Localization.DynamicVars.DynamicVarSet";

    // Decimal text survives Python JSON transport without conversion to binary floats.
    // Null set means uninitialized; an allocated set with no entries means initialized empty.
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record PowerDynamicVarSet([property: JsonRequired] string NativeType,
        [property: JsonRequired] List<PowerDynamicVar> Variables);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record PowerDynamicVar([property: JsonRequired] string Name,
        [property: JsonRequired] string NativeType, [property: JsonRequired] bool HasOwner,
        [property: JsonRequired] string BaseValue, [property: JsonRequired] string EnchantedValue,
        [property: JsonRequired] string PreviewValue, [property: JsonRequired] bool WasJustUpgraded);

    private void ValidatePowerVarAbi()
    {
        ExactInstanceFields(T(PowerVarSetType), ["_vars"], PowerVarSetType);
        ExactInstanceFields(T(ConstantPowerVarType), ["_owner", "_baseValue", "_enchantedValue", "_previewValue",
            "<Name>k__BackingField", "<WasJustUpgraded>k__BackingField"], ConstantPowerVarType);
    }

    private static decimal PowerVarDecimal(string? text, string path)
    {
        if (text is null || !decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out decimal value)
            || value.ToString(CultureInfo.InvariantCulture) != text)
            throw HistoryReferenceError(path, "Exact invariant decimal text is required.");
        return value;
    }

    private void ValidatePowerDynamicVars(string modelId, PowerDynamicVarSet? saved, string path)
    {
        ValidatePowerVarAbi();
        if (saved is null) return;
        if (saved.NativeType != PowerVarSetType || saved.Variables is null)
            throw HistoryReferenceError(path, "Unknown DynamicVarSet native type/collection.");
        string? allowedName = modelId switch
        {
            "WEAK_POWER" => "DamageDecrease",
            "VULNERABLE_POWER" => "DamageIncrease",
            "DUPLICATION_POWER" or "MINION_POWER" or "STRENGTH_POWER" => null,
            _ => throw HistoryReferenceError(path, $"Power {modelId} has no reviewed DynamicVar carrier.")
        };
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (PowerDynamicVar variable in saved.Variables)
        {
            if (variable is null || variable.NativeType != ConstantPowerVarType || variable.Name != allowedName
                || !names.Add(variable.Name))
                throw HistoryReferenceError(path, "Unknown or duplicate constant DynamicVar native type/name.");
            _ = PowerVarDecimal(variable.BaseValue, path + "." + variable.Name + ".BaseValue");
            _ = PowerVarDecimal(variable.EnchantedValue, path + "." + variable.Name + ".EnchantedValue");
            _ = PowerVarDecimal(variable.PreviewValue, path + "." + variable.Name + ".PreviewValue");
        }
    }

    private PowerDynamicVarSet? CapturePowerDynamicVars(object power, string path)
    {
        ValidatePowerVarAbi();
        object? set = ReflectionTools.Get(power, "_dynamicVars");
        if (set is null) return null;
        if (set.GetType() != T(PowerVarSetType))
            throw HistoryReferenceError(path, $"Unreviewed DynamicVarSet type {set.GetType().FullName}.");
        var variables = new List<PowerDynamicVar>();
        foreach (object? pair in ReflectionTools.Enumerate(set))
        {
            string key = (string)ReflectionTools.Get(pair!, "Key")!;
            object variable = ReflectionTools.Get(pair!, "Value")!;
            if (variable.GetType() != T(ConstantPowerVarType))
                throw HistoryReferenceError(path + "." + key, $"Unreviewed DynamicVar type {variable.GetType().FullName}; derived fields/delegates need an explicit carrier.");
            object? owner = ReflectionTools.Get(variable, "_owner");
            if (owner is not null && !ReferenceEquals(owner, power))
                throw HistoryReferenceError(path + "." + key + ".Owner", "Foreign DynamicVar owner alias.");
            if (key != (string)ReflectionTools.Get(variable, "Name")!)
                throw HistoryReferenceError(path + "." + key, "Dictionary key and native variable name differ.");
            string Value(string field) => ((decimal)ReflectionTools.Get(variable, field)!).ToString(CultureInfo.InvariantCulture);
            variables.Add(new(key, variable.GetType().FullName!, owner is not null,
                Value("_baseValue"), Value("_enchantedValue"), Value("_previewValue"),
                (bool)ReflectionTools.Get(variable, "WasJustUpgraded")!));
        }
        var saved = new PowerDynamicVarSet(set.GetType().FullName!, variables);
        ValidatePowerDynamicVars(Entry(power), saved, path);
        return saved;
    }

    private void RestorePowerDynamicVars(object power, PowerDynamicVarSet? saved, string path)
    {
        ValidatePowerDynamicVars(Entry(power), saved, path);
        if (saved is null) { ReflectionTools.Set(power, "_dynamicVars", null); return; }
        Array variables = Array.CreateInstance(T(ConstantPowerVarType), saved.Variables.Count);
        for (int i = 0; i < saved.Variables.Count; i++)
        {
            PowerDynamicVar v = saved.Variables[i];
            object variable = ReflectionTools.Create(T(ConstantPowerVarType), v.Name, PowerVarDecimal(v.BaseValue, path));
            // Direct native hydration keeps previews/flags distinct from BaseValue's resetting setter.
            ReflectionTools.Set(variable, "_baseValue", PowerVarDecimal(v.BaseValue, path));
            ReflectionTools.Set(variable, "_enchantedValue", PowerVarDecimal(v.EnchantedValue, path));
            ReflectionTools.Set(variable, "_previewValue", PowerVarDecimal(v.PreviewValue, path));
            ReflectionTools.Set(variable, "WasJustUpgraded", v.WasJustUpgraded);
            ReflectionTools.Set(variable, "_owner", v.HasOwner ? power : null);
            variables.SetValue(variable, i);
        }
        ReflectionTools.Set(power, "_dynamicVars", ReflectionTools.Create(T(PowerVarSetType), variables));
    }

    private static bool PlayerPowerRuntimeFingerprintMatches(CombatSnapshot expected, CombatSnapshot actual)
    {
        // Enemy/Osty fingerprints already cover their complete PowerSnapshot lists.
        // The player needs the same exact guard for these reviewed runtime carriers.
        object Reviewed(CombatSnapshot snapshot) => snapshot.PlayerPowers
            .Select((power, index) => new { Index = index, Power = power })
            .Where(p => HasReviewedPowerRuntime(p.Power.ModelId)).ToArray();
        return System.Text.Json.JsonSerializer.Serialize(Reviewed(expected), PortableRootJson)
            == System.Text.Json.JsonSerializer.Serialize(Reviewed(actual), PortableRootJson);
    }
}
