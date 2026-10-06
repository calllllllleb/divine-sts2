using System.Reflection;
using Sts2.NativeSim.Protocol;
using System.Text.Json.Serialization;

namespace Sts2.NativeSim.Core;

public sealed partial class PersistentNativeCombatEnvironment
{
    // Common PowerModel references only. This does not certify derived private state.
    // Owner is the containing Creature, never an independent transport alias.
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record PowerReferences(
        [property: JsonRequired] uint? ApplierCombatId,
        [property: JsonRequired] uint? TargetCombatId);

    private void ValidatePowerReferenceAbi()
    {
        Type root = T("MegaCrit.Sts2.Core.Models.PowerModel");
        Type creature = T("MegaCrit.Sts2.Core.Entities.Creatures.Creature");
        foreach (string name in new[] { "_owner", "_applier", "_target" })
        {
            FieldInfo? field = root.GetField(name, BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.NonPublic);
            if (field is null || field.FieldType != creature || field.IsInitOnly)
                throw new ProtocolException("unsupported_power_reference", $"PowerModel.{name} Creature reference ABI mismatch.");
        }
    }

    private PowerReferences CapturePowerReferences(object power, object owner, string path)
    {
        ValidatePowerReferenceAbi();
        if (!ReferenceEquals(ReflectionTools.Get(power, "Owner"), owner))
            throw new ProtocolException("invalid_power_reference", path + ".Owner: Power must belong to its containing Creature.");
        object[] residents = ReflectionTools.Enumerate(ReflectionTools.Get(_combat!, "Creatures")).Cast<object>().ToArray();
        uint? Id(object? value, string member)
        {
            if (value is null) return null;
            if (value.GetType() != T("MegaCrit.Sts2.Core.Entities.Creatures.Creature")
                || !residents.Any(c => ReferenceEquals(c, value)))
                throw new ProtocolException("unsupported_power_reference", path + "." + member
                    + $": Nonresident {value.GetType().FullName} is outside the common live reference contract.");
            uint id = CreatureIdentity(value);
            if (residents.Count(c => CreatureIdentity(c) == id) != 1)
                throw new ProtocolException("invalid_power_reference", path + "." + member + ": Ambiguous Creature CombatId.");
            return id;
        }
        return new(Id(ReflectionTools.Get(power, "Applier"), "ApplierCombatId"),
            Id(ReflectionTools.Get(power, "Target"), "TargetCombatId"));
    }

    private void ValidateResidentPowerDescriptors(CombatSnapshot snapshot, HashSet<uint> creatureIds)
    {
        ValidatePowerReferenceAbi();
        void Validate(List<PowerSnapshot> powers, string path)
        {
            for (int i = 0; i < powers.Count; i++)
            {
                PowerSnapshot power = powers[i];
                string item = path + $"[{i}]({power.ModelId})";
                if (power.References is not { } refs)
                    throw new ProtocolException("invalid_power_reference", item + ".References: Explicit payload required, including null references.");
                void Ref(uint? id, string member)
                {
                    if (id is uint value && !creatureIds.Contains(value))
                        throw new ProtocolException("invalid_power_reference", item + ".References." + member
                            + $": Creature CombatId {value} is absent from the saved resident Creatures.");
                }
                Ref(refs.ApplierCombatId, "ApplierCombatId");
                Ref(refs.TargetCombatId, "TargetCombatId");
                if (HasReviewedPowerRuntime(power.ModelId) != (power.Runtime is not null))
                    throw new ProtocolException("unsupported_power_runtime", item + ": Reviewed Power requires its exact runtime state.");
                if (power.Runtime is not { } runtime) continue;
                object model = Mutable("AllPowers", power.ModelId);
                ValidateResidentPowerAbi(model, "Power", item);
                ValidatePowerDynamicVars(power.ModelId, runtime.DynamicVars, item + ".Runtime.DynamicVars");
            }
        }
        Validate(snapshot.PlayerPowers, "PlayerPowers");
        for (int i = 0; i < snapshot.Enemies.Count; i++) Validate(snapshot.Enemies[i].Powers, $"Enemies[{i}].Powers");
        if (snapshot.OstyEntity?.Entity is { } osty) Validate(osty.Powers, "OstyEntity.Entity.Powers");
    }

    private void RestoreResidentPowerReferences(CombatSnapshot snapshot)
    {
        Dictionary<uint, object> creatures = ReflectionTools.Enumerate(ReflectionTools.Get(_combat!, "Creatures"))
            .Cast<object>().ToDictionary(CreatureIdentity);
        void Bind(object creature, List<PowerSnapshot> snapshots)
        {
            var powers = ReflectionTools.Enumerate(ReflectionTools.Get(creature, "Powers"));
            if (powers.Count != snapshots.Count) throw new ProtocolException("invalid_power_reference", "Restored Power count differs.");
            for (int i = 0; i < snapshots.Count; i++)
            {
                object power = powers[i]!;
                if (Entry(power) != snapshots[i].ModelId || !ReferenceEquals(ReflectionTools.Get(power, "Owner"), creature))
                    throw new ProtocolException("invalid_power_reference", $"Restored Power[{i}] model/Owner binding differs.");
                PowerReferences refs = snapshots[i].References;
                // Always write null too: reused resident powers must not retain another branch's aliases.
                ReflectionTools.Set(power, "_applier", refs.ApplierCombatId is uint a ? creatures[a] : null);
                ReflectionTools.Set(power, "_target", refs.TargetCombatId is uint t ? creatures[t] : null);
                if (!ReferenceEquals(ReflectionTools.Get(power, "Applier"), refs.ApplierCombatId is uint applier ? creatures[applier] : null)
                    || !ReferenceEquals(ReflectionTools.Get(power, "Target"), refs.TargetCombatId is uint target ? creatures[target] : null))
                    throw new ProtocolException("invalid_power_reference", "Restored Power exact native references differ.");
            }
        }
        Bind(creatures[snapshot.CombatHistory!.PlayerCombatId], snapshot.PlayerPowers);
        foreach (EnemySnapshot enemy in snapshot.Enemies) Bind(creatures[enemy.CombatId], enemy.Powers);
        if (snapshot.OstyEntity?.Entity is { } osty) Bind(creatures[osty.CombatId], osty.Powers);
    }
}
