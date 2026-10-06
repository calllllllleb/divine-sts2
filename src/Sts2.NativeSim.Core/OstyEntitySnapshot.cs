using System.Collections;
using System.Text.Json;
using Sts2.NativeSim.Protocol;

namespace Sts2.NativeSim.Core;

public sealed partial class PersistentNativeCombatEnvironment
{
    // Entity-only contract. Shared combat history has an independent codec and contract.
    private const int OstyEntityContractVersion = 1;
    private sealed record OstyEntityPayload(int Version, OstyEntitySnapshot? Entity);
    private sealed record OstyEntitySnapshot(
        ulong OwnerNetId, uint CombatId, string ModelId, string Side,
        int Hp, int MaxHp, int Block, string? SlotName, List<PowerSnapshot> Powers,
        bool InAllies, bool InPets, List<MonsterRuntimeEntry> MonsterRuntimeState,
        string CurrentStateId, bool PerformedFirstMove, List<string> StateLog,
        uint MonsterRngSeed, int MonsterRngCounter, string NextMoveId);

    private static uint CreatureIdentity(object creature) => ReflectionTools.Get(creature, "CombatId") is uint id
        ? id : throw new ProtocolException("invalid_creature_identity", "Attached creatures require a UInt32 CombatId.");

    private object? ResidentOsty()
    {
        IReadOnlyList<object?> players = ReflectionTools.Enumerate(ReflectionTools.Get(_combat!, "Players"));
        if (players.Count != 1 || !ReferenceEquals(players[0], _player))
            throw new ProtocolException("unsupported_osty_entity", "The entity contract requires the local single player.");
        object playerCreature = ReflectionTools.Get(_player!, "Creature")!;
        IReadOnlyList<object?> allies = ReflectionTools.Enumerate(ReflectionTools.Get(_combat!, "Allies"));
        IReadOnlyList<object?> pets = ReflectionTools.Enumerate(ReflectionTools.Get(_pcs!, "Pets"));
        HashSet<uint> ids = [];
        foreach (object? creature in ReflectionTools.Enumerate(ReflectionTools.Get(_combat!, "Creatures")))
            if (creature is null || !ids.Add(CreatureIdentity(creature))
                || !ReferenceEquals(ReflectionTools.Get(creature, "CombatState"), _combat))
                throw new ProtocolException("invalid_creature_identity", "Combat creature identities/ownership must be unique.");
        if (allies.Count(c => ReferenceEquals(c, playerCreature)) != 1 || pets.Count > 1
            || allies.Count != pets.Count + 1)
            throw new ProtocolException("unsupported_osty_entity", "Unknown or inconsistent allied/pet composition.");
        if (pets.Count == 0)
        {
            if (ReflectionTools.Get(_player!, "Osty") is not null)
                throw new ProtocolException("invalid_osty_binding", "Empty pets have an Osty reference.");
            return null;
        }
        object osty = pets[0] ?? throw new ProtocolException("invalid_osty_binding", "Null pet.");
        object? monster = ReflectionTools.Get(osty, "Monster");
        if (monster is null || monster.GetType() != T("MegaCrit.Sts2.Core.Models.Monsters.Osty")
            || Entry(monster) != "OSTY" || ReflectionTools.Get(osty, "Side")?.ToString() != "Player"
            || !ReferenceEquals(ReflectionTools.Get(monster, "Creature"), osty)
            || !ReferenceEquals(ReflectionTools.Get(osty, "PetOwner"), _player)
            || !ReferenceEquals(ReflectionTools.Get(osty, "CombatState"), _combat)
            || !ReferenceEquals(ReflectionTools.Get(_player!, "Osty"), osty)
            || allies.Count(c => ReferenceEquals(c, osty)) != 1)
            throw new ProtocolException("invalid_osty_binding", "Osty must belong to both the local Pets and combat Allies.");
        return osty;
    }

    private OstyEntityPayload CaptureOstyEntity(Func<object, List<PowerSnapshot>> powers)
    {
        object? osty = ResidentOsty();
        if (osty is null) return new(OstyEntityContractVersion, null);
        object monster = ReflectionTools.Get(osty, "Monster")!;
        object machine = ReflectionTools.Get(monster, "MoveStateMachine")!;
        object rng = ReflectionTools.Get(monster, "Rng")!;
        return new(OstyEntityContractVersion, new(
            (ulong)ReflectionTools.Get(_player!, "NetId")!, CreatureIdentity(osty), Entry(monster),
            ReflectionTools.Get(osty, "Side")!.ToString()!,
            Convert.ToInt32(ReflectionTools.Get(osty, "CurrentHp")),
            Convert.ToInt32(ReflectionTools.Get(osty, "MaxHp")),
            Convert.ToInt32(ReflectionTools.Get(osty, "Block")),
            ReflectionTools.Get(osty, "SlotName") as string, powers(osty), true, true,
            CaptureMonsterRuntimeState(monster),
            (string)ReflectionTools.Get(ReflectionTools.Get(machine, "_currentState")!, "Id")!,
            (bool)ReflectionTools.Get(machine, "_performedFirstMove")!,
            ReflectionTools.Enumerate(ReflectionTools.Get(machine, "StateLog"))
                .Select(state => (string)ReflectionTools.Get(state!, "Id")!).ToList(),
            (uint)ReflectionTools.Get(rng, "Seed")!, (int)ReflectionTools.Get(rng, "Counter")!,
            (string)ReflectionTools.Get(ReflectionTools.Get(monster, "NextMove")!, "Id")!));
    }

    private void ValidateOstyEntityDescriptor(CombatSnapshot snapshot)
    {
        if (snapshot.OstyEntity is not { Version: OstyEntityContractVersion } payload)
            throw new ProtocolException("unsupported_osty_entity_contract", "Missing/version-mismatched entity payload; empty Osty requires an explicit payload.");
        if (snapshot.Enemies.Select(enemy => enemy.CombatId).Distinct().Count() != snapshot.Enemies.Count
            || snapshot.Enemies.Any(enemy => enemy.CombatId >= snapshot.NextCreatureId
                || !Enum.IsDefined(T("MegaCrit.Sts2.Core.Entities.Creatures.HpDisplay"), enemy.HpDisplay)))
            throw new ProtocolException("invalid_creature_identity", "Snapshot enemy IDs must be unique and below next-id.");
        if (payload.Entity is not { } saved) return;
        if (saved.ModelId != "OSTY" || saved.Side != "Player" || !saved.InAllies || !saved.InPets
            || saved.CombatId >= snapshot.NextCreatureId || saved.Hp < 0 || saved.MaxHp < saved.Hp
            || saved.MaxHp <= 0 || saved.Block < 0 || saved.Powers is null
            || saved.Powers.Any(power => power is null || string.IsNullOrWhiteSpace(power.ModelId) || power.SavedProperties is null)
            || saved.MonsterRuntimeState is null || saved.StateLog is null || saved.MonsterRngCounter < 0
            || saved.CurrentStateId != "NOTHING_MOVE" || saved.StateLog.Any(id => id != "NOTHING_MOVE")
            || saved.NextMoveId is not ("NOTHING_MOVE" or "UNSET_MOVE"))
            throw new ProtocolException("invalid_osty_entity", "Invalid or unsupported Osty entity resources/runtime descriptor.");
        if (snapshot.Enemies.Any(enemy => enemy.CombatId == saved.CombatId))
            throw new ProtocolException("invalid_creature_identity", "Snapshot creature IDs overlap.");
    }

    private object? ValidateOstyEntityBinding(CombatSnapshot snapshot)
    {
        ValidateOstyEntityDescriptor(snapshot);
        object? resident = ResidentOsty();
        if (snapshot.Enemies.Any(enemy => enemy.CombatId == CreatureIdentity(ReflectionTools.Get(_player!, "Creature")!)))
            throw new ProtocolException("invalid_creature_identity", "Snapshot enemy ID overlaps the player.");
        OstyEntitySnapshot? saved = snapshot.OstyEntity!.Entity;
        if (saved is null)
        {
            if (resident is not null)
                throw new ProtocolException("unsupported_osty_entity_binding", "This batch does not remove a resident Osty.");
            return null;
        }
        if (resident is null || CreatureIdentity(resident) != saved.CombatId
            || (ulong)ReflectionTools.Get(_player!, "NetId")! != saved.OwnerNetId
            || !StringComparer.Ordinal.Equals(ReflectionTools.Get(resident, "SlotName") as string, saved.SlotName)
            || saved.CombatId == CreatureIdentity(ReflectionTools.Get(_player!, "Creature")!))
            throw new ProtocolException("unsupported_osty_entity_binding", "An exact resident Osty is required; creation/removal/replacement is unsupported.");
        object monster = ReflectionTools.Get(resident, "Monster")!;
        object machine = ReflectionTools.Get(monster, "MoveStateMachine")!;
        Dictionary<string, object> states = OstyStates(machine);
        if (!states.ContainsKey(saved.CurrentStateId) || saved.StateLog.Any(id => !states.ContainsKey(id))
            || saved.NextMoveId != "UNSET_MOVE" && !states.ContainsKey(saved.NextMoveId))
            throw new ProtocolException("unsupported_osty_entity", "Resident Osty FSM cannot bind the saved states.");
        // Reuse existing exact-DLL codecs on detached model values to preflight the
        // descriptor before any live creature/resource/pile mutation.
        try
        {
            // Native pets are canonical generic models, absent from ModelDb.Monsters.
            object canonical = T("MegaCrit.Sts2.Core.Models.ModelDb")
                .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Single(method => method.Name == "Monster" && method.IsGenericMethodDefinition
                    && method.GetGenericArguments().Length == 1 && method.GetParameters().Length == 0)
                .MakeGenericMethod(T("MegaCrit.Sts2.Core.Models.Monsters.Osty")).Invoke(null, null)!;
            ApplyMonsterRuntimeState(ReflectionTools.Invoke(canonical, "ToMutable")!, saved.MonsterRuntimeState);
            foreach (PowerSnapshot power in saved.Powers)
                ApplyNativeProperties(Mutable("AllPowers", power.ModelId), power.SavedProperties);
        }
        catch (ProtocolException) { throw; }
        catch (Exception ex) { throw new ProtocolException("invalid_osty_entity", $"Invalid native entity payload: {ex.Message}"); }
        return resident;
    }

    private static Dictionary<string, object> OstyStates(object machine) =>
        ReflectionTools.Enumerate(ReflectionTools.Get(machine, "States")).ToDictionary(
            pair => (string)ReflectionTools.Get(pair!, "Key")!,
            pair => ReflectionTools.Get(pair!, "Value")!, StringComparer.Ordinal);

    private void RestoreOstyEntity(object? resident, OstyEntitySnapshot? saved)
    {
        if (saved is null) return;
        object osty = resident!;
        ReflectionTools.Set(osty, "MaxHp", saved.MaxHp);
        ReflectionTools.Set(osty, "CurrentHp", saved.Hp);
        ReflectionTools.Set(osty, "Block", saved.Block);
        RestoreCreaturePowers(osty, saved.Powers);
        object monster = ReflectionTools.Get(osty, "Monster")!;
        ApplyMonsterRuntimeState(monster, saved.MonsterRuntimeState);
        object machine = ReflectionTools.Get(monster, "MoveStateMachine")!;
        Dictionary<string, object> states = OstyStates(machine);
        ReflectionTools.Set(machine, "_currentState", states[saved.CurrentStateId]);
        ReflectionTools.Set(machine, "_performedFirstMove", saved.PerformedFirstMove);
        IList log = (IList)ReflectionTools.Get(machine, "StateLog")!;
        log.Clear();
        foreach (string id in saved.StateLog) log.Add(states[id]);
        ReflectionTools.Set(monster, "Rng", ReflectionTools.Create(
            T("MegaCrit.Sts2.Core.Random.Rng"), saved.MonsterRngSeed, saved.MonsterRngCounter));
        ReflectionTools.Set(monster, "NextMove", saved.NextMoveId == "UNSET_MOVE"
            ? ReflectionTools.Create(T("MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine.MoveState"))
            : states[saved.NextMoveId]);
    }

    private void RebuildCombatCreatureIdentities()
    {
        Dictionary<uint, object> identities = [];
        foreach (object? creature in ReflectionTools.Enumerate(ReflectionTools.Get(_combat!, "Creatures")))
            if (creature is null || !identities.TryAdd(CreatureIdentity(creature), creature))
                throw new ProtocolException("invalid_creature_identity", "Restored combat creature IDs must be unique.");
        _combatCreaturesById.Clear();
        foreach ((uint id, object creature) in identities) _combatCreaturesById.Add(id, creature);
    }

    private static bool OstyEntityFingerprintMatches(CombatSnapshot expected, CombatSnapshot actual) =>
        JsonSerializer.Serialize(expected.OstyEntity, PortableRootJson)
            == JsonSerializer.Serialize(actual.OstyEntity, PortableRootJson);
}
