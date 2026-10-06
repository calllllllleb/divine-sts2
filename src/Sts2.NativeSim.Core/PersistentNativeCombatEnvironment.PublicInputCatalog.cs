using System.Collections;
using System.Reflection;

namespace Sts2.NativeSim.Core;

public sealed partial class PersistentNativeCombatEnvironment
{
    private object[] PublicInputModelTypes(Type db)
    {
        string[] families = ["Character", "Card", "Monster", "Encounter", "Relic", "Potion", "Power", "Orb",
                            "Enchantment", "Affliction", "Event"];
        List<object> entries = [];
        foreach (string family in families)
        {
            Type baseType = T($"MegaCrit.Sts2.Core.Models.{family}Model");
            foreach (Type type in _context.Assembly.GetTypes()
                         .Where(type => type.IsVisible && !type.IsAbstract && !type.ContainsGenericParameters && baseType.IsAssignableFrom(type))
                         .OrderBy(type => type.FullName, StringComparer.Ordinal))
            {
                string id = (string)ReflectionTools.InvokeStatic(db, "GetEntry", type)!;
                entries.Add(new { family = family.ToLowerInvariant(), model_id = id, runtime_type = type.FullName });
            }
        }
        return entries.ToArray();
    }

    private object[] PublicInputEnums() => _context.Assembly.GetTypes()
        .Where(type => type.IsEnum && type.IsVisible)
        .OrderBy(type => type.FullName, StringComparer.Ordinal)
        .Select(type => (object)new { runtime_type = type.FullName, values = Enum.GetNames(type).Order(StringComparer.Ordinal).ToArray() })
        .ToArray();

    private object[] PublicInputLocalizationKeys()
    {
        // These are static player-facing localization assets, not event instances/options.
        // Read keys only; do not render strings or invoke dynamic variable formatting.
        object manager = ReflectionTools.GetStatic(T("MegaCrit.Sts2.Core.Localization.LocManager"), "Instance")!;
        object? english = ReflectionTools.Get(manager, "_engTables");
        if (english is null && ReflectionTools.Get(manager, "Language")?.ToString() != "eng")
            throw new ProtocolException("public_english_localization_unavailable", "English static table keys are unavailable.");
        IDictionary tables = (IDictionary)(english ?? ReflectionTools.Get(manager, "_tables")!);
        return tables.Keys.Cast<string>().Order(StringComparer.Ordinal)
            .Select(name => (object)new
            {
                table = name,
                keys = ReflectionTools.Enumerate(ReflectionTools.Get(tables[name]!, "Keys"))
                    .Cast<string>().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()
            }).ToArray();
    }

    private string[] PublicInputRestOptionIds()
    {
        Type baseType = T("MegaCrit.Sts2.Core.Entities.RestSite.RestSiteOption");
        return _context.Assembly.GetTypes().Where(type => !type.IsAbstract && baseType.IsAssignableFrom(type))
            .Select(type =>
            {
                MethodInfo getter = type.GetProperty("OptionId")!.GetMethod!;
                byte[] code = getter.GetMethodBody()!.GetILAsByteArray()!;
                // The pinned shipped getter is exactly ldstr <literal>; ret.
                // Decode metadata, never construct an option or call Generate/OnSelect.
                if (code.Length != 6 || code[0] != 0x72 || code[5] != 0x2a)
                    throw new ProtocolException("public_option_id_not_static_literal", type.FullName!);
                return getter.Module.ResolveString(BitConverter.ToInt32(code, 1));
            }).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }
}
