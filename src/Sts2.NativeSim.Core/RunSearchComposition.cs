// Native mechanics and minimal public Run facts. Author: XuShuxi.
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Sts2.NativeSim.Protocol;

namespace Sts2.NativeSim.Core;

public sealed partial class PersistentNativeCombatEnvironment
{
    // XuShuxi: These are audited public scalars, never RNG state or future queues.
    private JsonElement PublicRunOddsSnapshot()
    {
        object unknown = ReflectionTools.Get(ReflectionTools.Get(_run!, "Odds")!, "UnknownMapPoint")!;
        object odds = ReflectionTools.Get(_player!, "PlayerOdds")!;
        return JsonSerializer.SerializeToElement(new {
            card_shop_removals_used = ReflectionTools.Get(ReflectionTools.Get(_player!, "ExtraFields")!, "CardShopRemovalsUsed"),
            unknown_monster_odds = ReflectionTools.Get(unknown, "MonsterOdds"),
            unknown_elite_odds = ReflectionTools.Get(unknown, "EliteOdds"),
            unknown_treasure_odds = ReflectionTools.Get(unknown, "TreasureOdds"),
            unknown_shop_odds = ReflectionTools.Get(unknown, "ShopOdds"),
            card_rarity_odds = ReflectionTools.Get(ReflectionTools.Get(odds, "CardRarity")!, "CurrentValue"),
            potion_reward_odds = ReflectionTools.Get(ReflectionTools.Get(odds, "PotionReward")!, "CurrentValue")
        });
    }

    private void RestorePublicRunOdds(JsonElement facts)
    {
        if (!facts.EnumerateObject().Select(pair => pair.Name).Order().SequenceEqual(new[] {
            "card_rarity_odds", "card_shop_removals_used", "potion_reward_odds", "unknown_elite_odds",
            "unknown_monster_odds", "unknown_shop_odds", "unknown_treasure_odds" }))
            throw new ProtocolException("unsupported_public_run_odds", "Unaudited public odds field.");
        int removals = facts.GetProperty("card_shop_removals_used").GetInt32();
        if (removals < 0 || facts.EnumerateObject().Where(pair => pair.Name != "card_shop_removals_used")
            .Any(pair => !float.IsFinite(pair.Value.GetSingle())))
            throw new ProtocolException("unsupported_public_run_odds", "Invalid public odds scalar.");
        object unknown = ReflectionTools.Get(ReflectionTools.Get(_run!, "Odds")!, "UnknownMapPoint")!;
        foreach (var (json, native) in new[] { ("unknown_monster_odds", "MonsterOdds"),
            ("unknown_elite_odds", "EliteOdds"), ("unknown_treasure_odds", "TreasureOdds"), ("unknown_shop_odds", "ShopOdds") })
            ReflectionTools.Set(unknown, native, facts.GetProperty(json).GetSingle());
        object odds = ReflectionTools.Get(_player!, "PlayerOdds")!;
        ReflectionTools.Invoke(ReflectionTools.Get(odds, "CardRarity")!, "OverrideCurrentValue", facts.GetProperty("card_rarity_odds").GetSingle());
        ReflectionTools.Invoke(ReflectionTools.Get(odds, "PotionReward")!, "OverrideCurrentValue", facts.GetProperty("potion_reward_odds").GetSingle());
        ReflectionTools.Set(ReflectionTools.Get(_player!, "ExtraFields")!, "CardShopRemovalsUsed", removals);
    }

    // XuShuxi: Only audited public history crosses the detached combat reset.
    // No analytics, RNG, future room sets, or grab-bag queues are accepted here.
    private void ApplyPublicCombatRunContext(IReadOnlyDictionary<string, JsonElement>? context)
    {
        if (context is null) return;
        if (!context.Keys.Order().SequenceEqual(new[] { "act_floor", "act_index", "public_odds", "room_history" }))
            throw new ProtocolException("unsupported_combat_run_context", "Unaudited Run context field.");
        int currentAct = context["act_index"].GetInt32(), floor = context["act_floor"].GetInt32();
        if (currentAct < 0 || currentAct >= ReflectionTools.Enumerate(ReflectionTools.Get(_run!, "Acts")).Count || floor < 0)
            throw new ProtocolException("unsupported_combat_run_context", "Invalid public Act/floor.");
        foreach (JsonElement point in context["room_history"].EnumerateArray())
        {
            if (!point.EnumerateObject().Select(pair => pair.Name).Order().SequenceEqual(new[] { "act", "point_type", "rooms" }))
                throw new ProtocolException("unsupported_combat_run_context", "Unaudited public history point.");
            int act = point.GetProperty("act").GetInt32();
            if (act < 0 || act > currentAct) throw new ProtocolException("unsupported_combat_run_context", "Future public history.");
            ReflectionTools.Set(_run!, "CurrentActIndex", act);
            bool first = true;
            foreach (JsonElement room in point.GetProperty("rooms").EnumerateArray())
            {
                if (!room.EnumerateObject().Select(pair => pair.Name).Order().SequenceEqual(new[] { "model_id", "room_type" }))
                    throw new ProtocolException("unsupported_combat_run_context", "Unaudited public room fact.");
                object pointType = Enum.Parse(T("MegaCrit.Sts2.Core.Map.MapPointType"), point.GetProperty("point_type").GetString()!);
                object roomType = Enum.Parse(T("MegaCrit.Sts2.Core.Rooms.RoomType"), room.GetProperty("room_type").GetString()!);
                JsonElement modelJson = room.GetProperty("model_id");
                object? model = modelJson.ValueKind == JsonValueKind.Null ? null
                    : modelJson.Deserialize(T("MegaCrit.Sts2.Core.Models.ModelId"), PortableRootJson);
                if (first) ReflectionTools.Invoke(_run!, "AppendToMapPointHistory", pointType, roomType, model);
                else
                {
                    object entry = ReflectionTools.Get(_run!, "CurrentMapPointHistoryEntry")!;
                    object nativeRoom = ReflectionTools.Create(T("MegaCrit.Sts2.Core.Runs.History.MapPointRoomHistoryEntry"));
                    ReflectionTools.Set(nativeRoom, "RoomType", roomType); ReflectionTools.Set(nativeRoom, "ModelId", model);
                    ReflectionTools.Invoke(ReflectionTools.Get(entry, "Rooms")!, "Add", nativeRoom);
                }
                first = false;
            }
            if (first) throw new ProtocolException("unsupported_combat_run_context", "Empty public room history point.");
        }
        ReflectionTools.Set(_run!, "CurrentActIndex", currentAct);
        ReflectionTools.Set(_run!, "ActFloor", floor);
        RestorePublicRunOdds(context["public_odds"]);
    }

    private IReadOnlyDictionary<string, JsonElement> PublicCombatRunContext()
    {
        var history = ReflectionTools.Enumerate(ReflectionTools.Get(_run!, "MapPointHistory"))
            .SelectMany((points, act) => ReflectionTools.Enumerate(points).Select(point => new {
                act, point_type = ReflectionTools.Get(point!, "MapPointType")!.ToString(),
                rooms = ReflectionTools.Enumerate(ReflectionTools.Get(point!, "Rooms")).Select(room => new {
                    room_type = ReflectionTools.Get(room!, "RoomType")!.ToString(), model_id = ReflectionTools.Get(room!, "ModelId")
                }).ToArray()
            })).ToArray();
        return new Dictionary<string, JsonElement> {
            ["act_index"] = JsonSerializer.SerializeToElement(ReflectionTools.Get(_run!, "CurrentActIndex")),
            ["act_floor"] = JsonSerializer.SerializeToElement(ReflectionTools.Get(_run!, "ActFloor")),
            ["public_odds"] = PublicRunOddsSnapshot(),
            ["room_history"] = JsonSerializer.SerializeToElement(history, PortableRootJson)
        };
    }
    private readonly HashSet<(string Bag, string Model)> _publicRunPulls = [];
    private readonly HashSet<(string Bag, string Model)> _pendingPublicPulls = [];
    private readonly Dictionary<(int Act, int Index), (int Col, int Row)> _runRoomCoords = [];

    private void EnsureNoRunQuestDomain()
    {
        // XuShuxi: Include special endpoints as well as GetAllMapPoints.
        JsonElement map = JsonSerializer.SerializeToElement(PublicMapSnapshot());
        if (map.GetProperty("points").EnumerateArray().Any(point => point.GetProperty("has_quest_marker").GetBoolean()))
            throw new ProtocolException("unsupported_run_quest_domain", "Quest continuation needs a certified conditional root.");
    }

    private static void CapturePublicRelicPull(object __instance, object? __result)
    {
        var environment = _activeEnvironment;
        if (environment is null || !environment._runMode || __result is null) return;
        string bag = ReferenceEquals(__instance, ReflectionTools.Get(environment._run!, "SharedRelicGrabBag")) ? "shared" : "player";
        environment._pendingPublicPulls.Add((bag, Entry(__result)));
    }

    private object AugmentPublicRun(object observation)
    {
        if (!_runMode) return observation;
        JsonObject result = JsonSerializer.SerializeToNode(observation)!.AsObject();
        result["run"] = JsonSerializer.SerializeToNode(CurrentPublicRunSnapshot(ReflectionTools.Get(_player!, "Deck")!));
        result["map"] = JsonSerializer.SerializeToNode(PublicMapSnapshot());
        HashSet<string> revealed = [];
        void Visit(JsonNode? node)
        {
            if (node is JsonObject obj)
            {
                if (obj["model_id"] is JsonValue value && value.TryGetValue<string>(out string? id) && id is not null) revealed.Add(id);
                foreach (var pair in obj) Visit(pair.Value);
            }
            else if (node is JsonArray array) foreach (var item in array) Visit(item);
        }
        Visit(result);
        foreach (var pull in _pendingPublicPulls.Where(pull => revealed.Contains(pull.Model)).ToArray())
        {
            _publicRunPulls.Add(pull);
            _pendingPublicPulls.Remove(pull);
        }
        result["public_run_memory"] = JsonSerializer.SerializeToNode(PublicRunMemorySnapshot());
        return result;
    }

    private object PublicRunMemorySnapshot()
    {
        object[] histories = ReflectionTools.Enumerate(ReflectionTools.Get(_run!, "MapPointHistory")).Where(x => x is not null).Select(x => x!).ToArray();
        int currentAct = Convert.ToInt32(ReflectionTools.Get(_run!, "CurrentActIndex"));
        object? currentCoord = ReflectionTools.Get(_run!, "CurrentMapCoord");
        if (currentCoord is not null && currentAct < histories.Length)
        {
            int index = ReflectionTools.Enumerate(histories[currentAct]).Count - 1;
            if (index >= 0) _runRoomCoords[(currentAct, index)] = (Convert.ToInt32(ReflectionTools.Get(currentCoord, "col")), Convert.ToInt32(ReflectionTools.Get(currentCoord, "row")));
        }
        List<object> rooms = [], encounters = [];
        for (int act = 0; act < histories.Length; act++)
        {
            int index = 0;
            foreach (object? entry in ReflectionTools.Enumerate(histories[act]))
            {
                if (entry is null) throw new ProtocolException("invalid_public_run_history", "Null map history entry.");
                bool known = _runRoomCoords.TryGetValue((act, index), out var coord);
                string? pointType = ReflectionTools.Get(entry, "MapPointType")?.ToString();
                foreach (object? room in ReflectionTools.Enumerate(ReflectionTools.Get(entry, "Rooms")))
                {
                    if (room is null) continue;
                    string roomType = ReflectionTools.Get(room, "RoomType")!.ToString()!;
                    object? model = ReflectionTools.Get(room, "ModelId");
                    string? id = model is null ? null : Convert.ToString(ReflectionTools.Get(model, "Entry"));
                    rooms.Add(new { kind = "room", model_id = id ?? roomType, room_type = roomType, act, col = known ? (int?)coord.Col : null,
                                    row = known ? (int?)coord.Row : null, original_point_type = pointType });
                    if (roomType is "Monster" or "Elite" or "Boss" && id is not null)
                        encounters.Add(new { kind = "encounter", model_id = id, act, col = known ? (int?)coord.Col : null,
                                             row = known ? (int?)coord.Row : null, original_point_type = pointType });
                }
                index++;
            }
        }
        object unknown = ReflectionTools.Get(ReflectionTools.Get(_run!, "Odds")!, "UnknownMapPoint")!;
        object odds = ReflectionTools.Get(_player!, "PlayerOdds")!;
        return new
        {
            schema_version = 1, complete = true,
            visited_event_ids = ReflectionTools.Enumerate(ReflectionTools.Get(_run!, "VisitedEventIds")).Where(x => x is not null).Select(x => ReflectionTools.Get(x!, "Entry")).ToArray(),
            rooms, encounters,
            relic_pulls = _publicRunPulls.OrderBy(x => x.Bag).ThenBy(x => x.Model).Select(x => new { kind = "relic_pull", model_id = x.Model, act = 0, bag = x.Bag }).ToArray(),
            card_shop_removals_used = ReflectionTools.Get(ReflectionTools.Get(_player!, "ExtraFields")!, "CardShopRemovalsUsed"),
            unknown_monster_odds = ReflectionTools.Get(unknown, "MonsterOdds"), unknown_elite_odds = ReflectionTools.Get(unknown, "EliteOdds"),
            unknown_treasure_odds = ReflectionTools.Get(unknown, "TreasureOdds"), unknown_shop_odds = ReflectionTools.Get(unknown, "ShopOdds"),
            card_rarity_odds = ReflectionTools.Get(ReflectionTools.Get(odds, "CardRarity")!, "CurrentValue"),
            potion_reward_odds = ReflectionTools.Get(ReflectionTools.Get(odds, "PotionReward")!, "CurrentValue")
        };
    }

    public PortableRunRoot ExportRunRoot()
    {
        ThrowIfPoisoned(); EnsureReset();
        // No copied factual ordering is permitted. Non-initial roots require a certified
        // conditional history kernel, including silent eligibility skips/deletions.
        if (!_runMode || _runStage != "map" || _history.Count != 0 || _pendingChoice is not null)
            throw new ProtocolException("run_conditional_history_unidentifiable",
                "This root needs native conditional event/relic history and current-continuation reconstruction; factual replay is prohibited.");
        if (_reset!.RunContext is not null || _reset.Enemies is not null || _reset.InitialDrawPile is not null
            || _reset.Deck.Any(card => card.NativeState is not null) || (_reset.Relics ?? []).Any(relic => relic.NativeState is not null)
            || (_reset.Potions ?? []).Any(potion => potion.NativeState is not null))
            throw new ProtocolException("unsupported_run_root_domain", "Unaudited reset-private domains cannot be copied into a Run world.");
        EnsureNoRunQuestDomain();
        object map = ReflectionTools.Get(_run!, "Map")!;
        object serialized = ReflectionTools.InvokeStatic(T("MegaCrit.Sts2.Core.Saves.Runs.SerializableActMap"), "FromActMap", map)!;
        object act = ReflectionTools.Get(_run!, "Act")!;
        return new(1, new(_productVersion, _assemblyHash, _pckHash),
            _reset with { Seed = "SEARCH_PRIVATE", RngCounters = null, InitialHand = [],
                          Turn = 1, Energy = null, Stars = null, InvokeCombatEntryHooks = false },
            JsonSerializer.SerializeToElement(serialized, PortableRootJson),
            Entry(ReflectionTools.Get(act, "BossEncounter")!),
            ReflectionTools.Get(act, "SecondBossEncounter") is { } second ? Entry(second) : null);
    }

    public object ComposeRunRoot(ComposeRunRootRequest request)
    {
        PortableRunRoot root = request.Root;
        if (request.SearchEntropy < 0)
            throw new ProtocolException("invalid_search_entropy", "Search entropy must be nonnegative and independent.");
        if (root.SchemaVersion != 1 || root.GameBuild.Version != _productVersion || root.GameBuild.AssemblySha256 != _assemblyHash || root.GameBuild.PckSha256 != _pckHash)
            throw new ProtocolException("run_root_version_mismatch", "Run schema/build mismatch.");
        // XuShuxi: Composer checks are independent of exporter checks. Handcrafted
        // requests cannot sneak a copied seed/counter or mutable hidden override in.
        ResetRequest reset = root.PublicReset;
        if (reset.Seed != "SEARCH_PRIVATE" || reset.RngCounters is not null || reset.RunContext is not null
            || reset.Enemies is not null || reset.InitialDrawPile is not null
            || reset.Deck.Any(card => card.NativeState is not null)
            || (reset.Relics ?? []).Any(relic => relic.NativeState is not null)
            || (reset.Potions ?? []).Any(potion => potion.NativeState is not null))
            throw new ProtocolException("unsupported_run_root_domain", "Run public reset contains an unaudited private domain.");
        // All native hidden domains are generated anew from independent search entropy.
        // Current public map/boss facts are then frozen, with Agent equality validation.
        RunReset(root.PublicReset with { Seed = $"run-search-{request.SearchEntropy:X16}", RngCounters = null });
        object mapSave = root.PublicMap.Deserialize(T("MegaCrit.Sts2.Core.Saves.Runs.SerializableActMap"), PortableRootJson)
            ?? throw new ProtocolException("invalid_public_map", "Public map deserialization failed.");
        ReflectionTools.Set(_run!, "Map", ReflectionTools.Create(T("MegaCrit.Sts2.Core.Map.SavedActMap"), mapSave));
        EnsureNoRunQuestDomain();
        object act = ReflectionTools.Get(_run!, "Act")!;
        object rooms = ReflectionTools.Get(act, "_rooms")!;
        object db = ReflectionTools.GetStatic(T("MegaCrit.Sts2.Core.Models.ModelDb"), "AllEncounters")!;
        ReflectionTools.Set(rooms, "Boss", Find(db, root.Boss));
        if (root.SecondBoss is not null) ReflectionTools.Set(rooms, "SecondBoss", Find(db, root.SecondBoss));
        return new { state = Capture(new { kind = "run_belief_composition" }),
                     composition = new { version = "pv1-run-composition-v1", closed_domains = new[] { "public", "public_derived", "encounters", "events", "relics", "future_rng" } } };
    }

    public PortableCombatRoot ExportRunCombatRoot()
    {
        ThrowIfPoisoned(); EnsureReset();
        if (!_runMode || _runStage != "combat" || _pendingChoice is not null)
            throw new ProtocolException("unsupported_run_combat_boundary", "A non-pending native combat boundary is required.");
        bool mode = _runMode;
        string hash = _hash;
        string? handle = _currentBranchHandle;
        string stage = _runStage;
        ResetRequest reset = _reset!;
        try
        {
            // XuShuxi: This is an opaque query substrate. It is imported only into
            // an isolated authority; existing Combat composition resamples its
            // draw/monster/future-RNG domains before every inner simulation.
            object creature = ReflectionTools.Get(_player!, "Creature")!;
            CardSpec[] deck = ReflectionTools.Enumerate(ReflectionTools.Get(ReflectionTools.Get(_player!, "Deck")!, "Cards"))
                .Where(card => card is not null).Select((value, index) =>
                {
                    object card = value!;
                    string id = _cardInstanceIds.Where(pair => ReferenceEquals(ReflectionTools.Get(pair.Key, "DeckVersion"), card))
                        .Select(pair => pair.Value).FirstOrDefault() ?? $"run-deck-{index}";
                    object? enchantment = ReflectionTools.Get(card, "Enchantment");
                    return new CardSpec(id, Entry(card), Convert.ToInt32(ReflectionTools.Get(card, "CurrentUpgradeLevel")),
                        SavedNativeState(card).ToDictionary(pair => pair.Key, pair => JsonSerializer.SerializeToElement(pair.Value)),
                        enchantment is null ? null : new EnchantmentSpec(Entry(enchantment), Convert.ToInt32(ReflectionTools.Get(enchantment, "Amount"))));
                }).ToArray();
            RelicSpec[] relics = ReflectionTools.Enumerate(ReflectionTools.Get(_player!, "Relics"))
                .Where(value => value is not null).Select(value => new RelicSpec(Entry(value!),
                    null, SavedNativeState(value!).ToDictionary(pair => pair.Key, pair => JsonSerializer.SerializeToElement(pair.Value)))).ToArray();
            _reset = reset with { Encounter = Entry(ReflectionTools.Get(_combat!, "Encounter")!),
                Deck = deck, Relics = relics, UseCharacterStartingLoadout = false,
                CurrentHp = Convert.ToInt32(ReflectionTools.Get(creature, "CurrentHp")),
                MaxHp = Convert.ToInt32(ReflectionTools.Get(creature, "MaxHp")),
                Gold = Convert.ToInt32(ReflectionTools.Get(_player!, "Gold")), InitialHand = [],
                InitialDrawPile = null, Enemies = null, InvokeCombatEntryHooks = false,
                RunContext = PublicCombatRunContext() };
            _runMode = false;
            _runStage = "map";
            return ExportCombatRoot();
        }
        finally { _reset = reset; _runStage = stage; _runMode = mode; _hash = hash; _currentBranchHandle = handle; }
    }
}
