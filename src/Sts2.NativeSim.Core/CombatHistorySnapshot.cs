using System.Collections;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sts2.NativeSim.Protocol;

namespace Sts2.NativeSim.Core;

public sealed partial class PersistentNativeCombatEnvironment
{
    // Internal reference numbers are local to this payload, never policy features.
    private const int CombatHistoryContractVersion = 1;
    private const string HistoryEntryNamespace = "MegaCrit.Sts2.Core.Combat.History.Entries.";
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record CombatHistorySnapshot([property: JsonRequired] int Version,
        [property: JsonRequired] List<HistoryObject> Objects, [property: JsonRequired] List<HistoryEntry> Entries);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record HistoryObject([property: JsonRequired] string Kind, [property: JsonRequired] string NativeType,
        [property: JsonRequired] string? Binding, [property: JsonRequired] string? ModelId,
        [property: JsonRequired] Dictionary<string, JsonElement> Fields, TransientMoveSnapshot? TransientMove = null);
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
                        throw HistoryReferenceError(path, $"Creature {ReflectionTools.Get(value, "CombatId")} is removed from the supported combat snapshot.");
                    binding = CreatureIdentity(value).ToString(System.Globalization.CultureInfo.InvariantCulture); break;
                case "Player":
                    if (!players.Any(p => ReferenceEquals(p, value))) throw HistoryReferenceError(path, "Player is outside this combat.");
                    binding = ((ulong)ReflectionTools.Get(value, "NetId")!).ToString(System.Globalization.CultureInfo.InvariantCulture); break;
                case "Monster":
                    object creature = ReflectionTools.Get(value, "Creature")!;
                    if (!creatures.Any(c => ReferenceEquals(c, creature))) throw HistoryReferenceError(path, "Removed monster.");
                    binding = CreatureIdentity(creature).ToString(System.Globalization.CultureInfo.InvariantCulture); modelId = Entry(value); break;
                case "Power":
                    object owner = ReflectionTools.Get(value, "Owner")!;
                    int powerIndex = ReferenceIndex(ReflectionTools.Get(owner, "Powers"), value);
                    if (!creatures.Any(c => ReferenceEquals(c, owner)) || powerIndex < 0)
                        throw HistoryReferenceError(path, $"Power {Entry(value)} is history-only; no complete detached-power codec.");
                    binding = $"{CreatureIdentity(owner)}/{powerIndex}"; modelId = Entry(value); break;
                case "Orb":
                    int orbIndex = ReferenceIndex(ReflectionTools.Get(ReflectionTools.Get(_pcs!, "OrbQueue")!, "Orbs"), value);
                    if (orbIndex < 0 || !ReferenceEquals(ReflectionTools.Get(value, "Owner"), _player))
                        throw HistoryReferenceError(path, $"Orb {Entry(value)} is history-only or foreign-owned.");
                    binding = orbIndex.ToString(System.Globalization.CultureInfo.InvariantCulture); modelId = Entry(value); break;
                case "Potion":
                    int potionIndex = ReferenceIndex(ReflectionTools.Get(_player!, "PotionSlots"), value);
                    if (potionIndex < 0) throw HistoryReferenceError(path, $"Potion {Entry(value)} has been consumed/removed; detached codec unavailable.");
                    binding = potionIndex.ToString(System.Globalization.CultureInfo.InvariantCulture); modelId = Entry(value); break;
                case "Affliction":
                    object afflicted = ReflectionTools.Get(value, "Card")!;
                    if (!cards.Contains(afflicted) || !ReferenceEquals(ReflectionTools.Get(afflicted, "Affliction"), value))
                        throw HistoryReferenceError(path, $"Affliction {Entry(value)} is no longer attached to its supported card.");
                    binding = GetCardInstanceId(afflicted); modelId = Entry(value); break;
                case "Move":
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
            objects[id - 1] = new(kind, value.GetType().FullName!, binding, modelId, fields, transientMove);
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
        return new(CombatHistoryContractVersion, objects, entries);
    }

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
        var creatureIds = snapshot.Enemies.Select(e => e.CombatId).ToHashSet();
        if (snapshot.OstyEntity?.Entity is { } osty) creatureIds.Add(osty.CombatId);
        // Native single-player initialization always uses this actual player binding.
        uint playerId = _player is null ? 0 : CreatureIdentity(ReflectionTools.Get(_player, "Creature")!);
        ulong netId = _player is null ? 1 : (ulong)ReflectionTools.Get(_player, "NetId")!;
        creatureIds.Add(playerId);
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
        try
        {
            for (int i = 0; i < saved.Objects.Count; i++)
            {
                HistoryObject o = saved.Objects[i]; string path = $"History.Objects[{i + 1}]";
                if (o is null || o.Fields is null || o.Kind != "Move" && o.TransientMove is not null) throw new ProtocolException("invalid_combat_history", path);
                if (o.Kind is "CardPlay" or "DamageResult")
                {
                    string type = o.Kind == "CardPlay" ? "MegaCrit.Sts2.Core.Entities.Cards.CardPlay" : "MegaCrit.Sts2.Core.Entities.Creatures.DamageResult";
                    if (o.NativeType != type || o.Binding is not null || o.ModelId is not null) throw new ProtocolException("invalid_combat_history", path);
                    Fields(o.Fields, o.Kind == "CardPlay" ? HistoryCardPlayFields : HistoryDamageFields, path);
                    if (o.Kind == "CardPlay" && (o.Fields["PlayCount"].GetInt32() < 1 || o.Fields["PlayIndex"].GetInt32() < 0 || o.Fields["PlayIndex"].GetInt32() >= o.Fields["PlayCount"].GetInt32()))
                        throw new ProtocolException("invalid_combat_history", path + ": invalid replay series.");
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
                        valid = moveParts.Length == 2 && uint.TryParse(moveParts[0], out uint monster) && creatureIds.Contains(monster) && o.ModelId is null;
                        if (o.TransientMove is { } transient && (moveParts.Length != 2 || transient.MoveId != moveParts[1]))
                            valid = false;
                        if (valid && _combat is not null) _ = BindHistoryObject(o); // FSM state ABI preflight before live mutation.
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
            }
        }
        catch (ProtocolException) { throw; }
        catch (Exception ex) { throw new ProtocolException("invalid_combat_history", "Invalid history descriptor: " + ex.Message); }
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
        // Resident objects first, then non-effect record objects; sharing uses the same table slot.
        for (int i = 0; i < saved.Objects.Count; i++)
            if (saved.Objects[i].Kind is not ("CardPlay" or "DamageResult")) objects[i] = BindHistoryObject(saved.Objects[i]);
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
            object entry = ReflectionTools.Create(T(HistoryEntryNamespace + e.Type), prefix.Concat(suffix).ToArray());
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
