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
    private const int CombatHistoryContractVersion = 3;
    private const string HistoryEntryNamespace = "MegaCrit.Sts2.Core.Combat.History.Entries.";
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record CombatHistorySnapshot([property: JsonRequired] int Version,
        [property: JsonRequired] uint PlayerCombatId, [property: JsonRequired] ulong PlayerNetId,
        [property: JsonRequired] List<HistoryObject> Objects, [property: JsonRequired] List<HistoryEntry> Entries);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record HistoryObject([property: JsonRequired] string Kind, [property: JsonRequired] string NativeType,
        [property: JsonRequired] string? Binding, [property: JsonRequired] string? ModelId,
        [property: JsonRequired] Dictionary<string, JsonElement> Fields, TransientMoveSnapshot? TransientMove = null,
        HistoricalPotion? Potion = null, HistoricalAffliction? Affliction = null, HistoricalPower? Power = null,
        HistoricalCreature? Creature = null, HistoricalMonster? Monster = null, HistoricalMove? Move = null);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record HistoricalCreature([property: JsonRequired] uint CombatId,
        [property: JsonRequired] int Monster, [property: JsonRequired] string Side,
        [property: JsonRequired] int CurrentHp, [property: JsonRequired] int MaxHp, [property: JsonRequired] int Block,
        [property: JsonRequired] string? SlotName, [property: JsonRequired] int? MonsterMaxHpBeforeModification,
        [property: JsonRequired] string HpDisplay, [property: JsonRequired] int? Player,
        [property: JsonRequired] int? PetOwner, [property: JsonRequired] bool HasCombatState,
        [property: JsonRequired] List<int> Powers);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record HistoricalMonster([property: JsonRequired] int Creature,
        [property: JsonRequired] bool HasStateMachine, [property: JsonRequired] bool HasRunRng,
        [property: JsonRequired] uint? RngSeed, [property: JsonRequired] int? RngCounter,
        [property: JsonRequired] int? NextMove, [property: JsonRequired] bool IsPerformingMove,
        [property: JsonRequired] IReadOnlyDictionary<string, object?> SavedProperties,
        [property: JsonRequired] List<MonsterRuntimeEntry> RuntimeState);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record HistoricalMove([property: JsonRequired] int Monster,
        [property: JsonRequired] string MoveId, [property: JsonRequired] string BehaviorMethod,
        [property: JsonRequired] string? FollowUpStateId, [property: JsonRequired] string? FollowUpState,
        [property: JsonRequired] bool MustPerformOnce, [property: JsonRequired] bool PerformedAtLeastOnce,
        [property: JsonRequired] List<HistoricalIntent> Intents);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record HistoricalIntent([property: JsonRequired] string NativeType,
        [property: JsonRequired] int? Damage, [property: JsonRequired] int? Repeats,
        [property: JsonRequired] string? DamageMethod,
        [property: JsonRequired] string? CachedAnimationName);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record HistoricalPotion([property: JsonRequired] int? Owner,
        [property: JsonRequired] bool IsQueued, [property: JsonRequired] bool HasBeenRemovedFromState,
        [property: JsonRequired] bool DynamicVarsInitialized,
        [property: JsonRequired] IReadOnlyDictionary<string, object?> SavedProperties);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record HistoricalAffliction([property: JsonRequired] int? Card,
        [property: JsonRequired] int Amount, [property: JsonRequired] IReadOnlyDictionary<string, object?> SavedProperties);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record HistoricalPower([property: JsonRequired] int? Owner,
        [property: JsonRequired] int? Applier, [property: JsonRequired] int? Target,
        [property: JsonRequired] int Amount, [property: JsonRequired] int AmountOnTurnStart,
        [property: JsonRequired] bool SkipNextDurationTick, [property: JsonRequired] bool DynamicVarsInitialized,
        [property: JsonRequired] string? ResolvedBigIconPath,
        [property: JsonRequired] IReadOnlyDictionary<string, object?> SavedProperties);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record HistoryEntry([property: JsonRequired] string Type, [property: JsonRequired] int? Actor,
        [property: JsonRequired] int RoundNumber, [property: JsonRequired] string CurrentSide,
        [property: JsonRequired] Dictionary<ulong, int> PlayerTurnNumbers,
        [property: JsonRequired] Dictionary<string, JsonElement> Fields);

    // A complete, explicit pinned-ABI whitelist. No delegate or arbitrary graph traversal.
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

    private CombatHistorySnapshot CaptureCombatHistory()
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
            TransientMoveSnapshot? transientMove = null;
            HistoricalPotion? historicalPotion = null;
            HistoricalAffliction? historicalAffliction = null;
            HistoricalPower? historicalPower = null;
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
                        ValidateHistoricalMonsterAbi(value, path);
                        if (ReflectionTools.Get(value, "MoveStateMachine") is not null || (bool)ReflectionTools.Get(value, "IsPerformingMove")!)
                            throw HistoryReferenceError(path, "Detached Monster must have its actual torn-down FSM and no executing move.");
                        object? next = ReflectionTools.Get(value, "NextMove");
                        if (next is not null) MoveOwner(next, value, path + ".NextMove");
                        object? rng = ReflectionTools.Get(value, "_rng");
                        object? runRng = ReflectionTools.Get(value, "_runRng");
                        if (runRng is not null && !ReferenceEquals(runRng, ReflectionTools.Get(_run!, "Rng")))
                            throw HistoryReferenceError(path + ".RunRng", "Foreign run RNG binding.");
                        historicalMonster = new(Reference(creature, "Creature", path + ".Creature")!.Value,
                            false, runRng is not null, rng is null ? null : (uint)ReflectionTools.Get(rng, "Seed")!,
                            rng is null ? null : (int)ReflectionTools.Get(rng, "Counter")!,
                            Reference(next, "Move", path + ".NextMove"), false, SavedNativeState(value), CaptureMonsterRuntimeState(value));
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
                        ValidateHistoricalModelAbi(value, kind, path);
                        object? powerVars = ReflectionTools.Get(value, "_dynamicVars");
                        if (ReflectionTools.Get(value, "_internalData") is not null || powerVars is not null && ReflectionTools.Enumerate(powerVars).Count != 0)
                            throw HistoryReferenceError(path, "Historical Power private data/nonempty variables require a separate codec.");
                        historicalPower = new(Reference(owner, "Creature", path + ".Owner"),
                            Reference(ReflectionTools.Get(value, "Applier"), "Creature", path + ".Applier"),
                            Reference(ReflectionTools.Get(value, "Target"), "Creature", path + ".Target"),
                            (int)ReflectionTools.Get(value, "Amount")!, (int)ReflectionTools.Get(value, "AmountOnTurnStart")!,
                            (bool)ReflectionTools.Get(value, "SkipNextDurationTick")!, powerVars is not null,
                            (string?)ReflectionTools.Get(value, "_resolvedBigIconPath"), SavedNativeState(value));
                    }
                    break;
                case "Orb":
                    int orbIndex = ReferenceIndex(ReflectionTools.Get(ReflectionTools.Get(_pcs!, "OrbQueue")!, "Orbs"), value);
                    if (orbIndex < 0 || !ReferenceEquals(ReflectionTools.Get(value, "Owner"), _player))
                        throw HistoryReferenceError(path, $"Orb {Entry(value)} is history-only or foreign-owned.");
                    binding = orbIndex.ToString(System.Globalization.CultureInfo.InvariantCulture); modelId = Entry(value); break;
                case "Potion":
                    int potionIndex = ReferenceIndex(ReflectionTools.Get(_player!, "PotionSlots"), value);
                    modelId = Entry(value);
                    if (potionIndex >= 0) binding = potionIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    else
                    {
                        ValidateHistoricalModelAbi(value, kind, path);
                        object? vars = ReflectionTools.Get(value, "_dynamicVars");
                        if (vars is not null && ReflectionTools.Enumerate(vars).Count != 0)
                            throw HistoryReferenceError(path + "._dynamicVars", "Nonempty mutable potion variables have no existing history codec.");
                        historicalPotion = new(Reference(ReflectionTools.Get(value, "Owner"), "Player", path + ".Owner"),
                            (bool)ReflectionTools.Get(value, "IsQueued")!, (bool)ReflectionTools.Get(value, "HasBeenRemovedFromState")!,
                            vars is not null, SavedNativeState(value));
                    }
                    break;
                case "Affliction":
                    object? afflicted = ReflectionTools.Get(value, "Card");
                    modelId = Entry(value);
                    if (afflicted is not null && cards.Contains(afflicted) && ReferenceEquals(ReflectionTools.Get(afflicted, "Affliction"), value))
                        binding = GetCardInstanceId(afflicted);
                    else
                    {
                        ValidateHistoricalModelAbi(value, kind, path);
                        historicalAffliction = new(Reference(afflicted, "Card", path + ".Card"),
                            (int)ReflectionTools.Get(value, "Amount")!, SavedNativeState(value));
                    }
                    break;
                case "Move":
                    if (moveOwners.TryGetValue(value, out object? historicalOwner)
                        && !creatures.Any(c => ReferenceEquals(c, ReflectionTools.Get(historicalOwner, "Creature"))))
                    {
                        ValidateHistoricalMonsterAbi(historicalOwner, path + ".Monster");
                        historicalMove = CaptureHistoricalMove(value, historicalOwner,
                            Reference(historicalOwner, "Monster", path + ".Monster")!.Value, path);
                        break;
                    }
                    var matches = creatures.Where(c => ReflectionTools.Get(c, "Monster") is { } m
                        && ReflectionTools.Enumerate(ReflectionTools.Get(ReflectionTools.Get(m, "MoveStateMachine")!, "States"))
                            .Any(pair => ReferenceEquals(ReflectionTools.Get(pair!, "Value"), value))).ToArray();
                    if (matches.Length == 0)
                    {
                        matches = ReflectionTools.Enumerate(ReflectionTools.Get(history, "Entries"))
                            .Where(entry => entry?.GetType() == T(HistoryEntryNamespace + "MonsterPerformedMoveEntry")
                                && ReferenceEquals(ReflectionTools.Get(entry!, "Move"), value))
                            .Select(entry => ReflectionTools.Get(ReflectionTools.Get(entry!, "Monster")!, "Creature")!)
                            .Distinct(ReferenceEqualityComparer.Instance).ToArray();
                        if (matches.Length == 1 && creatures.Any(c => ReferenceEquals(c, matches[0])))
                        {
                            object moveMonster = ReflectionTools.Get(matches[0], "Monster")!;
                            try
                            {
                                transientMove = CaptureTransientMove(matches[0], moveMonster, ReflectionTools.Get(moveMonster, "MoveStateMachine")!, value)
                                    ?? throw HistoryReferenceError(path, "Historical move has no existing transient descriptor.");
                            }
                            catch (ProtocolException ex) { throw HistoryReferenceError(path, ex.Message); }
                        }
                        else throw HistoryReferenceError(path, "Historical move's original monster is absent or ambiguous.");
                    }
                    if (matches.Length != 1) throw HistoryReferenceError(path, "Move has no unique native owner.");
                    binding = $"{CreatureIdentity(matches[0])}/{ReflectionTools.Get(value, "Id")}"; break;
                case "CardPlay":
                    fields = CaptureFields(value, HistoryCardPlayFields, path); break;
                case "DamageResult":
                    fields = CaptureFields(value, HistoryDamageFields, path); break;
                default: throw HistoryReferenceError(path, $"Unknown object kind {kind}.");
            }
            objects[id - 1] = new(kind, value.GetType().FullName!, binding, modelId, fields, transientMove,
                historicalPotion, historicalAffliction, historicalPower, historicalCreature, historicalMonster, historicalMove);
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
        return new(CombatHistoryContractVersion, CreatureIdentity(ReflectionTools.Get(_player!, "Creature")!),
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

    private void ValidateHistoricalMonsterAbi(object monster, string path)
    {
        // Exact-DLL source review: these factories allocate native states/intents and bind
        // native methods. They do not roll, invoke predicates, hooks, or any RNG stream.
        if (monster.GetType().FullName is not ("MegaCrit.Sts2.Core.Models.Monsters.TorchHeadAmalgam"
            or "MegaCrit.Sts2.Core.Models.Monsters.Nibbit"))
            throw HistoryReferenceError(path, $"Historical native FSM construction is not certified for {monster.GetType().FullName}.");
        ExactInstanceFields(T("MegaCrit.Sts2.Core.Models.MonsterModel"), ["_rng", "_runRng", "_creature", "_moveStateMachine",
            "<NextMove>k__BackingField", "_canonicalInstance", "_spawnedThisTurn", "_isPerformingMove"], path);
        var fields = MonsterRuntimeFields(monster.GetType()).Select(pair => pair.Field).ToHashSet();
        for (Type? type = monster.GetType(); type is not null && type != T("MegaCrit.Sts2.Core.Models.MonsterModel"); type = type.BaseType)
            foreach (FieldInfo field in type.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                if (!field.IsLiteral && !fields.Contains(field))
                    throw HistoryReferenceError(path + "." + field.Name, "Historical Monster storage requires a reviewed native carrier, including readonly references.");
    }

    private static string HistoryMethodIdentity(MethodInfo method) => method.DeclaringType!.FullName + "." + method;

    private HistoricalMove CaptureHistoricalMove(object move, object monster, int owner, string path)
    {
        Type moveType = T("MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine.MoveState");
        if (move.GetType() != moveType) throw HistoryReferenceError(path, "Unknown native MoveState subclass.");
        ExactInstanceFields(moveType, ["_performedAtLeastOnce", "_onPerform", "<Intents>k__BackingField", "<StateId>k__BackingField",
            "<MustPerformOnceBeforeTransitioning>k__BackingField", "<FollowUpStateId>k__BackingField", "<FollowUpState>k__BackingField"], path);
        ExactInstanceFields(moveType.BaseType!, [], path);
        if (ReflectionTools.Get(move, "_onPerform") is not Delegate behavior || !ReferenceEquals(behavior.Target, monster)
            || behavior.GetInvocationList().Length != 1 || behavior.Method.DeclaringType != monster.GetType())
            throw HistoryReferenceError(path + "._onPerform", "Historical Move must bind its original native Monster method.");
        var intents = new List<HistoricalIntent>();
        foreach (object? item in ReflectionTools.Enumerate(ReflectionTools.Get(move, "Intents")))
        {
            if (item is null) throw HistoryReferenceError(path, "Null native intent.");
            string name = item.GetType().FullName!;
            int? damage = null, repeats = null; string? damageMethod = null;
            if (name is "MegaCrit.Sts2.Core.MonsterMoves.Intents.SingleAttackIntent" or "MegaCrit.Sts2.Core.MonsterMoves.Intents.MultiAttackIntent")
            {
                ExactInstanceFields(item.GetType(), name.EndsWith("MultiAttackIntent", StringComparison.Ordinal) ? ["_repeat", "_repeatCalc"] : [], path);
                ExactInstanceFields(item.GetType().BaseType!, ["<DamageCalc>k__BackingField"], path);
                if (ReflectionTools.Get(item, "DamageCalc") is not Delegate calc || calc.Target is null
                    || calc.Method.DeclaringType != calc.Target.GetType()
                    || !calc.Target.GetType().FullName!.StartsWith(name + "+<>c__DisplayClass", StringComparison.Ordinal))
                    throw HistoryReferenceError(path, "Historical attack intent requires its native constant-damage closure.");
                ExactInstanceFields(calc.Target.GetType(), ["damage"], path);
                if (ReflectionTools.Get(calc.Target, "damage") is not int constant)
                    throw HistoryReferenceError(path, "Unknown native damage closure value.");
                damage = constant; damageMethod = HistoryMethodIdentity(calc.Method);
                if (name.EndsWith("MultiAttackIntent", StringComparison.Ordinal))
                {
                    if (ReflectionTools.Get(item, "_repeatCalc") is not null) throw HistoryReferenceError(path, "Dynamic repeat closure is outside the bounded historical Move codec.");
                    repeats = (int)ReflectionTools.Get(item, "_repeat")!;
                }
                else repeats = 1;
                ExactInstanceFields(item.GetType().BaseType!.BaseType!, ["_cachedAnimationName"], path);
            }
            else if (name is "MegaCrit.Sts2.Core.MonsterMoves.Intents.BuffIntent" or "MegaCrit.Sts2.Core.MonsterMoves.Intents.DefendIntent")
            {
                ExactInstanceFields(item.GetType(), [], path);
                ExactInstanceFields(item.GetType().BaseType!, ["_cachedAnimationName"], path);
            }
            else throw HistoryReferenceError(path, "Historical intent needs a reviewed native carrier: " + name);
            intents.Add(new(name, damage, repeats, damageMethod, (string?)ReflectionTools.Get(item, "_cachedAnimationName")));
        }
        object? followUp = ReflectionTools.Get(move, "FollowUpState");
        return new(owner, (string)ReflectionTools.Get(move, "Id")!, HistoryMethodIdentity(behavior.Method),
            (string?)ReflectionTools.Get(move, "FollowUpStateId"), followUp is null ? null : (string)ReflectionTools.Get(followUp, "Id")!,
            (bool)ReflectionTools.Get(move, "MustPerformOnceBeforeTransitioning")!, (bool)ReflectionTools.Get(move, "_performedAtLeastOnce")!, intents);
    }

    private object HistoricalFsm(object monster)
    {
        ValidateHistoricalMonsterAbi(monster, "History.Monster");
        // Invoke the native allocation factory only. Never SetUpForCombat/RollMove/PerformMove.
        return ReflectionTools.Invoke(monster, "GenerateMoveStateMachine")!;
    }

    private void ValidateHistoricalMoveDescriptor(Type monsterType, HistoricalMove move)
    {
        // Source-reviewed fixed identities; no factory/getter may inspect the
        // destination's old RunState/ascension before the import reset.
        var states = monsterType.Name == "TorchHeadAmalgam"
            ? new Dictionary<string, (string Method, string Next, string[] Intents, int Repeat)>
            {
                ["TACKLE_MOVE"] = ("TackleMove", "TACKLE_2_MOVE", ["SingleAttackIntent"], 1),
                ["TACKLE_2_MOVE"] = ("TackleMove", "BEAM_MOVE", ["SingleAttackIntent"], 1),
                ["BEAM_MOVE"] = ("SoulBeamMove", "TACKLE_3_MOVE", ["MultiAttackIntent"], 3),
                ["TACKLE_3_MOVE"] = ("WeakTackleMove", "TACKLE_4_MOVE", ["SingleAttackIntent"], 1),
                ["TACKLE_4_MOVE"] = ("WeakTackleMove", "BEAM_MOVE", ["SingleAttackIntent"], 1),
            }
            : new Dictionary<string, (string Method, string Next, string[] Intents, int Repeat)>
            {
                ["BUTT_MOVE"] = ("ButtMove", "SLICE_MOVE", ["SingleAttackIntent"], 1),
                ["SLICE_MOVE"] = ("SliceMove", "HISS_MOVE", ["SingleAttackIntent", "DefendIntent"], 1),
                ["HISS_MOVE"] = ("HissMove", "BUTT_MOVE", ["BuffIntent"], 0),
            };
        if (!states.TryGetValue(move.MoveId, out var state) || move.FollowUpState != state.Next || move.FollowUpStateId is not null
            || move.Intents.Count != state.Intents.Length)
            throw HistoryReferenceError(move.MoveId, "Unknown original native Move identity/follow-up/intent coverage.");
        MethodInfo method = monsterType.GetMethod(state.Method, BindingFlags.Instance | BindingFlags.NonPublic)!;
        if (move.BehaviorMethod != HistoryMethodIdentity(method))
            throw HistoryReferenceError(move.MoveId, "Original native Monster method identity mismatch.");
        for (int i = 0; i < state.Intents.Length; i++)
        {
            HistoricalIntent intent = move.Intents[i];
            string typeName = "MegaCrit.Sts2.Core.MonsterMoves.Intents." + state.Intents[i];
            if (intent is null || intent.NativeType != typeName)
                throw HistoryReferenceError(move.MoveId, "Original intent native type/order mismatch.");
            if (state.Intents[i] is "SingleAttackIntent" or "MultiAttackIntent")
            {
                var methods = T(typeName).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(t => t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) is var fields
                        && fields.Length == 1 && fields[0].Name == "damage" && fields[0].FieldType == typeof(int))
                    .SelectMany(t => t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    .Where(m => m.ReturnType == typeof(decimal) && m.GetParameters().Length == 0)
                    .Select(HistoryMethodIdentity);
                if (intent.Damage is not >= 0 || intent.Repeats != state.Repeat || !methods.Contains(intent.DamageMethod, StringComparer.Ordinal))
                    throw HistoryReferenceError(move.MoveId, "Original constant intent carrier/method identity mismatch.");
            }
            else if (intent.Damage is not null || intent.Repeats is not null || intent.DamageMethod is not null)
                throw HistoryReferenceError(move.MoveId, "Nonattack intent has foreign attack state.");
        }
    }

    private object RestoreHistoricalMove(object monster, object machine, HistoricalMove saved)
    {
        object move = ReflectionTools.Enumerate(ReflectionTools.Get(machine, "States"))
            .Where(pair => (string)ReflectionTools.Get(pair!, "Key")! == saved.MoveId)
            .Select(pair => ReflectionTools.Get(pair!, "Value")!).SingleOrDefault()
            ?? throw HistoryReferenceError(saved.MoveId, "Original move is absent from the certified native FSM.");
        HistoricalMove native = CaptureHistoricalMove(move, monster, saved.Monster, saved.MoveId);
        static object Semantics(HistoricalMove value) => new { value.MoveId, value.BehaviorMethod, value.FollowUpStateId, value.FollowUpState,
            Intents = value.Intents.Select(i => new { i.NativeType, i.Damage, i.Repeats, i.DamageMethod }) };
        if (JsonSerializer.Serialize(Semantics(native)) != JsonSerializer.Serialize(Semantics(saved)))
            throw HistoryReferenceError(saved.MoveId, "Stored Move semantics differ from the original native FSM factory.");
        ReflectionTools.Set(move, "MustPerformOnceBeforeTransitioning", saved.MustPerformOnce);
        ReflectionTools.Set(move, "_performedAtLeastOnce", saved.PerformedAtLeastOnce);
        var intents = ReflectionTools.Enumerate(ReflectionTools.Get(move, "Intents"));
        for (int i = 0; i < intents.Count; i++) ReflectionTools.Set(intents[i]!, "_cachedAnimationName", saved.Intents[i].CachedAnimationName);
        return move;
    }

    private void ValidateHistoricalModelAbi(object model, string kind, string path)
    {
        if (kind == "Power" && (Entry(model), model.GetType().FullName) is not
            (("DUPLICATION_POWER", "MegaCrit.Sts2.Core.Models.Powers.DuplicationPower")
            or ("MINION_POWER", "MegaCrit.Sts2.Core.Models.Powers.MinionPower")
            or ("STRENGTH_POWER", "MegaCrit.Sts2.Core.Models.Powers.StrengthPower")))
            throw HistoryReferenceError(path, $"Power {Entry(model)} has no reviewed historical carrier.");
        Type root = T("MegaCrit.Sts2.Core.Models." + kind + "Model");
        string[] owned = kind == "Potion" ? ["_owner", "_dynamicVars", "_canonicalInstance", "<IsQueued>k__BackingField", "<HasBeenRemovedFromState>k__BackingField", "BeforeUse"]
            : kind == "Power" ? ["_resolvedBigIconPath", "_amount", "_amountOnTurnStart", "_skipNextDurationTick", "_owner", "_applier", "_target", "_dynamicVars", "_internalData", "_canonicalInstance", "PulsingStarted", "PulsingStopped", "Flashed", "DisplayAmountChanged", "Removed"]
            : ["_card", "_amount", "_canonicalInstance", "AmountChanged"];
        foreach (FieldInfo field in root.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            if (!owned.Contains(field.Name, StringComparer.Ordinal))
                throw HistoryReferenceError(path, $"Unclassified {kind} base member {field.Name}.");
        // These families have bounded native value carriers, not an object-graph codec.
        // Extra concrete mutable storage requires its own reviewed codec, even if default-valued.
        for (Type? type = model.GetType(); type is not null && type != root; type = type.BaseType)
            foreach (FieldInfo field in type.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                if (!field.IsLiteral)
                    throw HistoryReferenceError(path + "." + field.Name, "Concrete model storage is outside the historical Potion/Affliction codec.");
    }

    private object MutableHistoricalModel(HistoryObject o) => o.Kind == "Power"
        ? ReflectionTools.Invoke(Find(ReflectionTools.GetStatic(T("MegaCrit.Sts2.Core.Models.ModelDb"), "AllPowers")!, o.ModelId!), "ToMutable", 0)!
        : Mutable(o.Kind == "Potion" ? "AllPotions" : "DebugAfflictions", o.ModelId!);

    private static int ReferenceIndex(object? sequence, object target)
    {
        var values = ReflectionTools.Enumerate(sequence);
        for (int i = 0; i < values.Count; i++) if (ReferenceEquals(values[i], target)) return i;
        return -1;
    }

    private void ValidateCombatHistory(CombatSnapshot snapshot)
    {
        CombatHistorySnapshot saved = snapshot.CombatHistory
            ?? throw new ProtocolException("unsupported_combat_history", "An explicit shared history payload is required, including empty history.");
        if (saved.Version != CombatHistoryContractVersion || saved.Objects is null || saved.Entries is null)
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
            ValidateHistoricalModelAbi(model, "Power", "Resident.Power");
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
        var historicalMoves = new HashSet<(int, string)>();
        var descriptorMonsters = new Dictionary<int, object>();
        object DescriptorMonster(int id)
        {
            if (descriptorMonsters.TryGetValue(id, out object? cached)) return cached;
            HistoryObject owner = saved.Objects[id - 1];
            HistoricalMonster monster = owner.Monster ?? throw HistoryReferenceError("History.Move", "Owner must be a historical Monster descriptor.");
            HistoricalCreature creature = saved.Objects[monster.Creature - 1].Creature
                ?? throw HistoryReferenceError("History.Monster", "Owner must reference a historical Creature descriptor.");
            object model = Mutable("Monsters", owner.ModelId!);
            ValidateHistoricalMonsterAbi(model, "History.Monster");
            ApplyNativeProperties(model, monster.SavedProperties);
            ApplyMonsterRuntimeState(model, monster.RuntimeState);
            ValidateHistoricalCreatureAbi(RuntimeHelpers.GetUninitializedObject(T("MegaCrit.Sts2.Core.Entities.Creatures.Creature")), "History.Creature");
            descriptorMonsters[id] = model;
            return model;
        }
        try
        {
            for (int i = 0; i < saved.Objects.Count; i++)
            {
                HistoryObject o = saved.Objects[i]; string path = $"History.Objects[{i + 1}]";
                if (o is null || o.Fields is null || o.Kind != "Move" && o.TransientMove is not null
                    || o.Kind != "Potion" && o.Potion is not null || o.Kind != "Affliction" && o.Affliction is not null
                    || o.Kind != "Power" && o.Power is not null || o.Kind != "Creature" && o.Creature is not null
                    || o.Kind != "Monster" && o.Monster is not null || o.Kind != "Move" && o.Move is not null)
                    throw new ProtocolException("invalid_combat_history", path);
                int historicalForms = (o.Potion is null ? 0 : 1) + (o.Affliction is null ? 0 : 1) + (o.Power is null ? 0 : 1)
                    + (o.Creature is null ? 0 : 1) + (o.Monster is null ? 0 : 1) + (o.Move is null ? 0 : 1);
                if (historicalForms > 1 || historicalForms == 1 && (o.Binding is not null || o.Fields.Count != 0 || o.TransientMove is not null))
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
                    if (saved.Objects[removed.Creature - 1].Creature?.Monster != i + 1 || o.ModelId is null
                        || removed.HasStateMachine || removed.IsPerformingMove || removed.SavedProperties is null || removed.RuntimeState is null
                        || removed.RngSeed.HasValue != removed.RngCounter.HasValue || removed.RngCounter < 0)
                        throw new ProtocolException("invalid_combat_history", path + ": invalid historical Monster state/relationship.");
                    object model = DescriptorMonster(i + 1);
                    if (model.GetType().FullName != o.NativeType) throw new ProtocolException("invalid_combat_history", path + ": Monster model/type mismatch.");
                    Ref(JsonSerializer.SerializeToElement(removed.NextMove), "Move", true, path + ".Monster.NextMove");
                    if (removed.NextMove is int next && saved.Objects[next - 1].Move?.Monster != i + 1)
                        throw new ProtocolException("invalid_combat_history", path + ": NextMove owner mismatch.");
                    continue;
                }
                if (o.Move is { } originalMove)
                {
                    Ref(JsonSerializer.SerializeToElement(originalMove.Monster), "Monster", false, path + ".Move.Monster");
                    if (o.NativeType != "MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine.MoveState" || o.ModelId is not null
                        || originalMove.Intents is null || !historicalMoves.Add((originalMove.Monster, originalMove.MoveId)))
                        throw new ProtocolException("invalid_combat_history", path + ": historical Move native type/alias mismatch.");
                    ValidateHistoricalMoveDescriptor(DescriptorMonster(originalMove.Monster).GetType(), originalMove);
                    continue;
                }
                if (o.Potion is not null || o.Affliction is not null || o.Power is not null)
                {
                    if (o.Binding is not null || o.Fields.Count != 0 || o.TransientMove is not null || o.ModelId is null)
                        throw new ProtocolException("invalid_combat_history", path + ": resident and historical forms are mutually exclusive.");
                    object model = MutableHistoricalModel(o);
                    if (model.GetType().FullName != o.NativeType) throw new ProtocolException("invalid_combat_history", path + ": model/type mismatch.");
                    ValidateHistoricalModelAbi(model, o.Kind, path);
                    if (o.Potion is { } potion)
                    {
                        Ref(JsonSerializer.SerializeToElement(potion.Owner), "Player", true, path + ".Potion.Owner");
                        if (potion.SavedProperties is null) throw new ProtocolException("invalid_combat_history", path);
                        ApplyNativeProperties(model, potion.SavedProperties);
                        if (potion.DynamicVarsInitialized && ReflectionTools.Enumerate(ReflectionTools.Get(model, "DynamicVars")).Count != 0)
                            throw HistoryReferenceError(path + ".Potion.DynamicVars", "Nonempty variables require a separate codec.");
                    }
                    else if (o.Affliction is { } affliction)
                    {
                        Ref(JsonSerializer.SerializeToElement(affliction.Card), "Card", true, path + ".Affliction.Card");
                        if (affliction.SavedProperties is null) throw new ProtocolException("invalid_combat_history", path);
                        ApplyNativeProperties(model, affliction.SavedProperties);
                    }
                    else if (o.Power is { } power)
                    {
                        Ref(JsonSerializer.SerializeToElement(power.Owner), "Creature", true, path + ".Power.Owner");
                        Ref(JsonSerializer.SerializeToElement(power.Applier), "Creature", true, path + ".Power.Applier");
                        Ref(JsonSerializer.SerializeToElement(power.Target), "Creature", true, path + ".Power.Target");
                        if (power.SavedProperties is null) throw new ProtocolException("invalid_combat_history", path);
                        ApplyNativeProperties(model, power.SavedProperties);
                        if (ReflectionTools.Get(model, "_internalData") is not null || ReflectionTools.Enumerate(ReflectionTools.Get(model, "DynamicVars")).Count != 0)
                            throw HistoryReferenceError(path + ".Power", "Private data/nonempty variables require a separate codec.");
                    }
                    continue;
                }
                if (o.Binding is null || o.Fields.Count != 0 || o.TransientMove is null && !bindings.Add(o.Kind + ":" + o.Binding))
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
                        if (o.TransientMove is { } transient && (moveParts.Length != 2 || transient.MoveId != moveParts[1]))
                            valid = false;
                        if (o.NativeType != "MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine.MoveState") valid = false;
                        if (valid && o.TransientMove is { } descriptor)
                        {
                            if (!descriptor.MustPerformOnce || string.IsNullOrWhiteSpace(descriptor.FollowUpStateId)
                                || descriptor.IntentTypeNames is not { Length: > 0 }) valid = false;
                            if (descriptor.BehaviorOwner == "Power")
                            {
                                if (descriptor.PowerIndex is not int pi || Powers(monster) is not { } sourcePowers || pi < 0 || pi >= sourcePowers.Count) valid = false;
                                else ApplyNativeProperties(Mutable("AllPowers", sourcePowers[pi].ModelId), sourcePowers[pi].SavedProperties);
                            }
                            else if (descriptor.BehaviorOwner is not ("Monster" or "NoOp")) valid = false;
                            if (descriptor.BehaviorOwner == "NoOp" && (descriptor.BehaviorMethod is not null || descriptor.PowerIndex is not null)) valid = false;
                            if (descriptor.BehaviorOwner != "NoOp" && string.IsNullOrWhiteSpace(descriptor.BehaviorMethod)) valid = false;
                            foreach (string intent in descriptor.IntentTypeNames ?? [])
                                if (!T("MegaCrit.Sts2.Core.MonsterMoves.Intents.AbstractIntent").IsAssignableFrom(T(intent))) valid = false;
                        }
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
                        "Card" => "AllCards", "Power" => "AllPowers", "Orb" => "AllOrbs",
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
                    if (monster.Monster is not null
                        ? move.Move?.Monster != e.Fields["Monster"].GetInt32() || e.Actor != monster.Monster.Creature
                        : move.Move is not null || move.Binding!.Split('/')[0] != monster.Binding)
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
            if (move.TransientMove is { } transient)
            {
                List<PowerSnapshot> powers = snapshot.Enemies.SingleOrDefault(e => e.CombatId == id)?.Powers
                    ?? snapshot.OstyEntity!.Entity!.Powers;
                object[] detached = powers.Select(power =>
                {
                    object value = Mutable("AllPowers", power.ModelId);
                    ReflectionTools.Set(value, "_amount", power.Amount);
                    ApplyNativeProperties(value, power.SavedProperties);
                    return value;
                }).ToArray();
                _ = BuildTransientMove(creature, monster, transient, detached);
            }
            else _ = BindHistoryObject(move);
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
            "Move" when saved.TransientMove is { } transient => BuildTransientMove(Creature(uint.Parse(b.Split('/')[0])), ReflectionTools.Get(Creature(uint.Parse(b.Split('/')[0])), "Monster")!, transient),
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
        var objects = new object?[saved.Objects.Count];
        object? Ref(JsonElement value) => value.ValueKind == JsonValueKind.Null ? null : objects[value.GetInt32() - 1];
        // Allocate model identities before hydrating any cross-reference. Historical
        // Creatures use the native non-effect constructor, never CombatState.CreateCreature.
        for (int i = 0; i < saved.Objects.Count; i++)
        {
            HistoryObject o = saved.Objects[i];
            if (o.Monster is not null) objects[i] = Mutable("Monsters", o.ModelId!);
            else if (o.Potion is not null || o.Affliction is not null || o.Power is not null) objects[i] = MutableHistoricalModel(o);
            else if (o.Creature is null && o.Move is null && o.Kind is not ("CardPlay" or "DamageResult" or "Move"))
                objects[i] = BindHistoryObject(o);
        }
        for (int i = 0; i < saved.Objects.Count; i++)
        {
            if (saved.Objects[i].Creature is not { } dead) continue;
            object creature = ReflectionTools.Create(T(saved.Objects[i].NativeType), objects[dead.Monster - 1],
                Enum.Parse(T("MegaCrit.Sts2.Core.Combat.CombatSide"), dead.Side), dead.SlotName);
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
            if (saved.Objects[i].Monster is not { } removed) continue;
            object monster = objects[i]!;
            ApplyNativeProperties(monster, removed.SavedProperties);
            ApplyMonsterRuntimeState(monster, removed.RuntimeState);
            ReflectionTools.Set(monster, "_runRng", removed.HasRunRng ? ReflectionTools.Get(_run!, "Rng") : null);
            object? rng = removed.RngSeed is uint seed ? ReflectionTools.Create(T("MegaCrit.Sts2.Core.Random.Rng"), seed, removed.RngCounter!.Value) : null;
            ReflectionTools.Set(monster, "_rng", rng);
        }
        for (int i = 0; i < saved.Objects.Count; i++)
        {
            HistoryObject o = saved.Objects[i]; var f = o.Fields;
            if (o.Potion is { } potion)
            {
                object model = objects[i]!;
                ApplyNativeProperties(model, potion.SavedProperties);
                ReflectionTools.Set(model, "_owner", potion.Owner is int owner ? objects[owner - 1] : null);
                ReflectionTools.Set(model, "IsQueued", potion.IsQueued);
                ReflectionTools.Set(model, "HasBeenRemovedFromState", potion.HasBeenRemovedFromState);
                ReflectionTools.Set(model, "_dynamicVars", null);
                if (potion.DynamicVarsInitialized) _ = ReflectionTools.Get(model, "DynamicVars");
                objects[i] = model;
            }
            else if (o.Affliction is { } affliction)
            {
                object model = objects[i]!;
                ApplyNativeProperties(model, affliction.SavedProperties);
                ReflectionTools.Set(model, "_card", affliction.Card is int card ? objects[card - 1] : null);
                ReflectionTools.Set(model, "_amount", affliction.Amount);
                objects[i] = model;
            }
            else if (o.Power is { } power)
            {
                object model = objects[i]!;
                ApplyNativeProperties(model, power.SavedProperties);
                ReflectionTools.Set(model, "_owner", power.Owner is int owner ? objects[owner - 1] : null);
                ReflectionTools.Set(model, "_applier", power.Applier is int applier ? objects[applier - 1] : null);
                ReflectionTools.Set(model, "_target", power.Target is int target ? objects[target - 1] : null);
                ReflectionTools.Set(model, "_amount", power.Amount);
                ReflectionTools.Set(model, "_amountOnTurnStart", power.AmountOnTurnStart);
                ReflectionTools.Set(model, "_skipNextDurationTick", power.SkipNextDurationTick);
                ReflectionTools.Set(model, "_resolvedBigIconPath", power.ResolvedBigIconPath);
                if (power.DynamicVarsInitialized) _ = ReflectionTools.Get(model, "DynamicVars");
                else ReflectionTools.Set(model, "_dynamicVars", null);
                objects[i] = model;
            }
        }
        var machines = new Dictionary<int, object>();
        for (int i = 0; i < saved.Objects.Count; i++)
        {
            HistoryObject o = saved.Objects[i];
            if (o.Move is { } move)
            {
                object monster = objects[move.Monster - 1]!;
                if (!machines.TryGetValue(move.Monster, out object? machine)) machines[move.Monster] = machine = HistoricalFsm(monster);
                objects[i] = RestoreHistoricalMove(monster, machine, move);
            }
            else if (o.Kind == "Move") objects[i] = BindHistoryObject(o);
        }
        for (int i = 0; i < saved.Objects.Count; i++)
        {
            if (saved.Objects[i].Monster is { } removed)
                ReflectionTools.Set(objects[i]!, "<NextMove>k__BackingField", removed.NextMove is int next ? objects[next - 1] : null);
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
        object players = ReflectionTools.Get(_combat!, "Players")!;
        object? ArrayRefs(JsonElement value, Type type)
        {
            if (value.ValueKind == JsonValueKind.Null) return null;
            var values = value.EnumerateArray().Select(Ref).ToArray(); Array array = Array.CreateInstance(type, values.Length);
            for (int i = 0; i < values.Length; i++) array.SetValue(values[i], i); return array;
        }
        foreach (HistoryEntry e in saved.Entries)
        {
            object? actor = e.Actor is int id ? objects[id - 1] : null; var f = e.Fields;
            object? R(string field) => Ref(f[field]); int I(string field) => f[field].GetInt32();
            object props = f.TryGetValue("Props", out var encoded) ? Enum.ToObject(T("MegaCrit.Sts2.Core.ValueProps.ValueProp"), encoded.GetInt64()) : 0;
            object?[] prefix = e.Type switch
            {
                "BlockGainedEntry" => [I("Amount"), props, R("CardPlay"), actor],
                "CardAfflictedEntry" => [R("Card"), R("Affliction")],
                "CardDiscardedEntry" or "CardExhaustedEntry" => [R("Card")],
                "CardGeneratedEntry" => [R("Card"), R("Creator")],
                "CardPlayStartedEntry" or "CardPlayFinishedEntry" => [R("CardPlay")],
                "CreatureAttackedEntry" => [actor, ArrayRefs(f["DamageResults"], T("MegaCrit.Sts2.Core.Entities.Creatures.DamageResult"))],
                "DamageReceivedEntry" => [R("Result"), actor, R("Dealer"), R("CardSource")],
                "EnergySpentEntry" or "StarsModifiedEntry" or "SummonedEntry" => [I("Amount"), _player],
                "MonsterPerformedMoveEntry" => [R("Monster"), R("Move"), ArrayRefs(f["Targets"], T("MegaCrit.Sts2.Core.Entities.Creatures.Creature"))],
                "OrbChanneledEntry" => [R("Orb")], "PotionUsedEntry" => [R("Potion"), R("Target")],
                "PowerReceivedEntry" => [R("Power"), f["Amount"].GetDecimal(), R("Applier")],
                "CardDrawnEntry" => [R("Card")],
                _ => throw new ProtocolException("unsupported_history_entry", e.Type)
            };
            object side = Enum.Parse(T("MegaCrit.Sts2.Core.Combat.CombatSide"), e.CurrentSide);
            object?[] suffix = e.Type == "CardDrawnEntry" ? [e.RoundNumber, side, f["FromHandDraw"].GetBoolean(), history, players] : [e.RoundNumber, side, history, players];
            object entry;
            if (e.Type == "PotionUsedEntry" && ReflectionTools.Get(R("Potion")!, "Owner") is null)
            {
                // Native constructor infers Actor from Owner. A subsequently cleared Owner
                // needs direct hydration of this fully enumerated native record, not a fake owner.
                entry = RuntimeHelpers.GetUninitializedObject(T(HistoryEntryNamespace + e.Type));
                ReflectionTools.Set(entry, "<Potion>k__BackingField", R("Potion"));
                ReflectionTools.Set(entry, "<Target>k__BackingField", R("Target"));
                ReflectionTools.Set(entry, "<RoundNumber>k__BackingField", e.RoundNumber);
                ReflectionTools.Set(entry, "<CurrentSide>k__BackingField", side);
                ReflectionTools.Set(entry, "<History>k__BackingField", history);
                ReflectionTools.Set(entry, "_playerTurnNumbers", new Dictionary<ulong, int>());
            }
            else entry = ReflectionTools.Create(T(HistoryEntryNamespace + e.Type), prefix.Concat(suffix).ToArray());
            // Constructors infer current owners/turns; historical values must win.
            ReflectionTools.Set(entry, "<Actor>k__BackingField", actor);
            var turns = (Dictionary<ulong, int>)ReflectionTools.Get(entry, "_playerTurnNumbers")!; turns.Clear();
            foreach (var turn in e.PlayerTurnNumbers) turns.Add(turn.Key, turn.Value);
            if (e.Type == "CardPlayFinishedEntry") ReflectionTools.Set(entry, "<WasEthereal>k__BackingField", f["WasEthereal"].GetBoolean());
            if (!ReferenceEquals(ReflectionTools.Get(entry, "Actor"), actor) || !ReferenceEquals(ReflectionTools.Get(entry, "History"), history)
                || (int)ReflectionTools.Get(entry, "RoundNumber")! != e.RoundNumber || ReflectionTools.Get(entry, "CurrentSide")!.ToString() != e.CurrentSide)
                throw new ProtocolException("invalid_combat_history", "Native history base ABI failed restoration.");
            entries.Add(entry);
        }
        // One publication; no Changed hook, execution, RNG, reward, or branch-tail append.
        ReflectionTools.Set(history, "_entries", entries);
    }

    private static bool CombatHistoryFingerprintMatches(CombatSnapshot expected, CombatSnapshot actual) =>
        JsonSerializer.Serialize(expected.CombatHistory, PortableRootJson) == JsonSerializer.Serialize(actual.CombatHistory, PortableRootJson);
}
