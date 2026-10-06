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
    private IEnumerable<(string Bag, string Model)> PublicObservedRunPulls()
        => _runRelicOperations.Where(fact => fact.Bag is "shared" or "player" && fact.Operation is "front" or "back" && fact.ModelId is not null)
            .Select(fact => (fact.Bag, fact.ModelId!)).Distinct();
    private readonly Dictionary<(int Act, int Index), (int Col, int Row)> _runRoomCoords = [];
    // XuShuxi: Sequential worlds cannot reuse factual reset/history branches.
    private bool _composedRunWorld;

    private void EnsureNoRunQuestDomain()
    {
        CapturePublicQuestMarkers();
        // XuShuxi: CreateForTest does not invoke Card.AfterCreated. This public
        // initialization producer differs from acquiring SpoilsMap during a run.
        if (_reset!.Deck.Any(card => card.ModelId == "SPOILS_MAP"))
            throw new ProtocolException("run_quest_initialization_producer_missing",
                "SPOILS_MAP in a custom initial deck: CreateForTest skips AfterCreated, so SpoilsActIndex is -1 rather than the acquired-card constant 1. A creation/removal journal is required before this initialization family can compose.");
        if (_questEvidenceFailure is not null) throw new ProtocolException("run_quest_evidence_missing", _questEvidenceFailure);
        if (ReflectionTools.Enumerate(ReflectionTools.Get(_player!, "Relics")).Any(relic => Entry(relic!) == "FUR_COAT")
            && !_runQuestMarkers.Any(fact => fact.ModelId == "FUR_COAT"))
            throw new ProtocolException("run_quest_evidence_missing",
                "FUR_COAT: no observed marked-coordinate journal; BeforeCombatStart reads old coordinates even after the owning act. Current marker absence cannot reconstruct that history.");
    }

    private object AugmentPublicRun(object observation)
    {
        if (!_runMode) return observation;
        JsonObject result = JsonSerializer.SerializeToNode(observation)!.AsObject();
        result["run"] = JsonSerializer.SerializeToNode(CurrentPublicRunSnapshot(ReflectionTools.Get(_player!, "Deck")!));
        result["map"] = JsonSerializer.SerializeToNode(PublicMapSnapshot());
        CapturePublicQuestMarkers();
        HashSet<string> revealed = [];
        void RelicFace(JsonNode? node)
        {
            if (node is JsonObject obj && obj["model_id"] is JsonValue value
                && value.TryGetValue<string>(out string? id) && id is not null) revealed.Add(id);
        }
        // XuShuxi: Visibility is producer-specific. An old equipped relic with
        // the same id never reveals an unopened chest or an unrelated old pull.
        void Rewards(JsonNode? node)
        {
            if (node is JsonArray array) foreach (JsonNode? item in array) Rewards(item);
            else if (node is JsonObject obj)
            {
                if (obj["kind"]?.GetValue<string>() == "relic") RelicFace(obj);
                foreach (string child in new[] { "rewards", "children", "reward" }) if (obj[child] is { } items) Rewards(items);
            }
        }
        Rewards(result["room_rewards"]); Rewards(result["outstanding_rewards"]); Rewards(result["custom_rewards"]);
        RevealPublicRelicOperations(revealed, fact => fact.Producer.Contains("RelicReward"));
        revealed.Clear();
        foreach (JsonNode? entry in result["shop"]?["entries"]?.AsArray() ?? [])
            if (entry?["kind"]?.GetValue<string>() == "relic") RelicFace(entry);
        RevealPublicRelicOperations(revealed, fact => fact.Producer.Contains("Merchant"));
        revealed.Clear();
        foreach (JsonNode? entry in result["treasure"]?["relic_options"]?.AsArray() ?? []) RelicFace(entry);
        RevealPublicRelicOperations(revealed, fact => fact.Producer.Contains("Treasure") || fact.Producer == "treasure_tutorial_first_chest");
        revealed.Clear();
        foreach (JsonNode? option in result["event"]?["options"]?.AsArray() ?? [])
            foreach (JsonNode? item in option?["items"]?.AsArray() ?? [])
                if (item?["kind"]?.GetValue<string>() == "relic") RelicFace(item);
        string? eventType = _event?.GetType().Name;
        RevealPublicRelicOperations(revealed, fact => eventType is not null && fact.Producer.Contains(eventType));
        RevealPublicRelicOperations(_publicRelicAcquisitions, fact => eventType is not null && fact.Producer.Contains(eventType));
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
            schema_version = 3, complete = true,
            event_selections = _runEventSelections,
            relic_operations = _runRelicOperations.Where(fact => fact.FailureReason is null),
            quest_markers = _runQuestMarkers,
            visited_event_ids = ReflectionTools.Enumerate(ReflectionTools.Get(_run!, "VisitedEventIds")).Where(x => x is not null).Select(x => ReflectionTools.Get(x!, "Entry")).ToArray(),
            rooms, encounters,
            relic_pulls = PublicObservedRunPulls().OrderBy(x => x.Bag).ThenBy(x => x.Model).Select(x => new { kind = "relic_pull", model_id = x.Model, act = 0, bag = x.Bag }).ToArray(),
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
        ValidateRunBoundary();
        if (_reset!.RunContext is not null || _reset.RngCounters is { Count: > 0 } || _reset.Enemies is not null || _reset.InitialDrawPile is not null
            || _reset.Deck.Any(card => card.NativeState is not null) || (_reset.Relics ?? []).Any(relic => relic.NativeState is not null)
            || (_reset.Potions ?? []).Any(potion => potion.NativeState is not null))
            throw new ProtocolException("unsupported_run_root_domain", "Unaudited reset-private domains cannot be copied into a Run world.");
        EnsureNoRunQuestDomain();
        if (_runRelicOperations.FirstOrDefault(fact => fact.FailureReason is not null) is { } unsupported)
            throw new ProtocolException("run_relic_producer_evidence_missing", unsupported.FailureReason!);
        ValidateRunHistoryDomain(PublicCombatRunContext());
        RunPlayerFacts current = ExportPublicRunPlayer();
        RunPlayerFacts initial = InitialPublicRunPlayer();
        ValidateRunPlayer(current); ValidateRunPlayer(initial);
        object map = ReflectionTools.Get(_run!, "Map")!;
        object serialized = ReflectionTools.InvokeStatic(T("MegaCrit.Sts2.Core.Saves.Runs.SerializableActMap"), "FromActMap", map)!;
        JsonObject publicMap = JsonSerializer.SerializeToNode(serialized, PortableRootJson)!.AsObject();
        // XuShuxi: CanBeModified is read only by generation-time MapPathPruning.
        // A completed, frozen public topology does not need that private flag.
        foreach (JsonNode? point in publicMap["points"]!.AsArray()) point!.AsObject().Remove("can_modify");
        foreach (string endpoint in new[] { "start", "boss", "second_boss" })
            if (publicMap[endpoint] is JsonObject point) point.Remove("can_modify");
        object act = ReflectionTools.Get(_run!, "Act")!;
        // Initialization and current public resources have distinct owners. In
        // particular _reset never supplies mid-run HP, inventory, or progression.
        PortableRunRoot root = new(4, new(_productVersion, _assemblyHash, _pckHash),
            new(_reset.Character, _reset.Ascension, _reset.UseCharacterStartingLoadout, initial),
            current, PublicCombatRunContext(), PublicObservedRunPulls().OrderBy(pull => pull.Bag).ThenBy(pull => pull.Model).Select(pull => $"{pull.Bag}:{pull.Model}").ToArray(),
            _runEventSelections.ToArray(), _runRelicOperations.ToArray(), _runQuestMarkers.ToArray(),
            ReflectionTools.Enumerate(ReflectionTools.Get(_run!, "VisitedMapCoords")).Select(coord =>
                new RunCoordinate(Convert.ToInt32(ReflectionTools.Get(coord!, "col")), Convert.ToInt32(ReflectionTools.Get(coord!, "row")))).ToArray(),
            _runRoomCoords.OrderBy(pair => pair.Key).Select(pair => new RunHistoryCoordinate(pair.Key.Act, pair.Key.Index,
                new(pair.Value.Col, pair.Value.Row))).ToArray(),
            _runStage, ExportPublicTreasure(), _runStage == "rest" && _pendingChoice is not null ? _publicRestSelection : null, ExportPublicRoomRewards(),
            JsonSerializer.SerializeToElement(publicMap, PortableRootJson),
            Entry(ReflectionTools.Get(act, "BossEncounter")!),
            ReflectionTools.Get(act, "SecondBossEncounter") is { } second ? Entry(second) : null);
        ValidatePublicEventEvidence(root);
        ValidatePublicRelicEvidence(root);
        ValidatePublicQuestEvidence(root);
        ValidatePublicTreasure(root);
        return root;
    }

    public object ComposeRunRoot(ComposeRunRootRequest request)
    {
        // XuShuxi: A malformed public contract is rejected before native work.
        // This validation traverses only the protocol DTO, never a native owner.
        ValidateRequiredPublicContract(request, new NullabilityInfoContext());
        return ComposeValidatedRunRoot(request);
    }

    private static void ValidateRequiredPublicContract(object value, NullabilityInfoContext metadata)
    {
        foreach (PropertyInfo property in value.GetType().GetProperties())
        {
            object? child = property.GetValue(value);
            NullabilityInfo contract = metadata.Create(property);
            if (child is null)
            {
                if (contract.ReadState == NullabilityState.NotNull)
                    throw new ProtocolException("invalid_public_run_root", $"Required public field {value.GetType().Name}.{property.Name} is null.");
                continue;
            }
            if (child.GetType().Namespace == typeof(PortableRunRoot).Namespace)
                ValidateRequiredPublicContract(child, metadata);
            else if (child is System.Collections.IEnumerable items && child is not string)
            {
                foreach (object? item in items)
                {
                    if (item is null && contract.GenericTypeArguments.FirstOrDefault()?.ReadState == NullabilityState.NotNull)
                        throw new ProtocolException("invalid_public_run_root", $"Null item in required public collection {property.Name}.");
                    if (item?.GetType().Namespace == typeof(PortableRunRoot).Namespace)
                        ValidateRequiredPublicContract(item!, metadata);
                }
            }
        }
    }

    private object ComposeValidatedRunRoot(ComposeRunRootRequest request)
    {
        PortableRunRoot root = request.Root;
        try { ValidateRunCompositionInputs(request); }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
        {
            // Only input validation is translated. Native sampling/regeneration
            // failures keep their real diagnostic rather than masquerading as JSON.
            throw new ProtocolException("invalid_public_run_root", error.Message);
        }
        ValidateMechanicalRoot(root, request.MechanicalRoot);
        return ComposeCurrentRunKernel(root, request.MechanicalRoot!, request.SearchEntropy);
    }

    // XuShuxi: Explicit regression oracle. Production acquire never calls this.
    public object ComposeRunRootReference(ComposeRunRootRequest request)
    {
        ValidateRequiredPublicContract(request, new NullabilityInfoContext());
        ValidateRunCompositionInputs(request);
        var wall = System.Diagnostics.Stopwatch.StartNew();
        return ComposeValidatedRunWorld(request.Root, request.SearchEntropy, wall);
    }

    private void ValidateRunCompositionInputs(ComposeRunRootRequest request)
    {
        PortableRunRoot root = request.Root;
        if (request.SearchEntropy < 0)
            throw new ProtocolException("invalid_search_entropy", "Search entropy must be nonnegative and independent.");
        if (root.SchemaVersion != 4 || root.GameBuild.Version != _productVersion || root.GameBuild.AssemblySha256 != _assemblyHash || root.GameBuild.PckSha256 != _pckHash)
            throw new ProtocolException("run_root_version_mismatch", "Run schema/build mismatch.");
        // XuShuxi: Composer checks are independent of exporter checks. Handcrafted
        // requests cannot sneak a copied seed/counter or mutable hidden override in.
        ValidateRunPlayer(root.Initialization.Loadout); ValidateRunPlayer(root.CurrentPlayer);
        if (root.Initialization.Loadout.Deck.Any(card => card.ModelId == "SPOILS_MAP"))
            throw new ProtocolException("run_quest_initialization_producer_missing", "SPOILS_MAP custom initialization does not execute AfterCreated; acquiring the card and initializing the card are distinct public producers.");
        ValidateRunHistoryDomain(root.PublicContext);
        ValidatePublicEventEvidence(root);
        ValidatePublicRelicEvidence(root);
        ValidatePublicQuestEvidence(root);
        ValidatePublicMap(root.PublicMap);
        if (root.Stage is not ("map" or "rewards" or "rest" or "treasure"))
            throw new ProtocolException("run_modal_producer_evidence_missing", $"Stage {root.Stage} has no certified native regeneration contract.");
        if (root.Stage != "rewards" && root.Rewards.Count != 0)
            throw new ProtocolException("invalid_run_modal_evidence", "Rewards outside their certified native owner.");
        ValidatePublicTreasure(root);
        if (root.RestSelection is not null && (root.Stage != "rest" || root.RestSelection != "SMITH"))
            throw new ProtocolException("run_rest_continuation_evidence_missing", "Only SmithRestSiteOption's pre-selection continuation is certified: it has no mutation before FromDeckForUpgrade.");
    }

    private object ComposeValidatedRunWorld(PortableRunRoot root, long searchEntropy, System.Diagnostics.Stopwatch wall)
    {
        // XuShuxi: Rejection samples the complete native prior jointly, then
        // consumes only certified public encounter evidence. No resident suffix
        // participates in the sampler. Exhaustion is a protocol error, not death.
        const int maxAttempts = 8192;
        string contradiction = "encounter";
        string? firstContradiction = null;
        Dictionary<string, int> rejectedEvidence = [];
        int attempts;
        for (attempts = 1; attempts <= maxAttempts; attempts++)
        {
            // XuShuxi: Keep a finite rejection request's obsolete native owners
            // collectable without reinstating the costly per-trial collection.
            if (attempts % 256 == 0) GC.Collect(2, GCCollectionMode.Optimized, blocking: true, compacting: false);
            ResetCore(IndependentRunReset(root.Initialization, searchEntropy, attempts), runPriorOnly: true);
            if (root.Initialization.UseCharacterStartingLoadout && JsonSerializer.Serialize(_initialCharacterPublicLoadout, PortableRootJson)
                    != JsonSerializer.Serialize(root.Initialization.Loadout, PortableRootJson))
                throw new ProtocolException("invalid_run_initialization_public_facts", "The native character/ascension producer does not reproduce the supplied starting public loadout.");
            _runMode = true;
            ReflectionTools.Invoke(_manager!, "Reset", true);
            ReflectionTools.Invoke(ReflectionTools.GetStatic(T("MegaCrit.Sts2.Core.Runs.RunManager"), "Instance")!, "GenerateRooms");
            if (ReplayPublicEncounters(root, out contradiction) && ReplayPublicGeneratorOperations(root, out contradiction)) break;
            firstContradiction ??= contradiction;
            rejectedEvidence[contradiction] = rejectedEvidence.GetValueOrDefault(contradiction) + 1;
        }
        if (attempts > maxAttempts)
            throw new ProtocolException("run_composition_attempts_exhausted",
                $"domain=encounter_event_relic_joint; attempts={maxAttempts}; first_contradicted_public_evidence={firstContradiction}",
                new { domain = "encounter_event_relic_joint", attempts = maxAttempts, first_contradicted_public_evidence = firstContradiction,
                    rejected_public_evidence_counts = rejectedEvidence });
        double kernelMs = wall.Elapsed.TotalMilliseconds;
        ReplaceRunFutureRng(searchEntropy);
        return FinishComposedRunWorld(root, attempts, "whole_prior_reference", wall, 0, kernelMs, rejectedEvidence);
    }

    private object FinishComposedRunWorld(PortableRunRoot root, int attempts, string algorithm,
        System.Diagnostics.Stopwatch wall, double importMs, double kernelMs, IReadOnlyDictionary<string, int> rejectedEvidence)
    {
        InstallPublicRunPlayer(root.CurrentPlayer);
        _runEventSelections.AddRange(root.EventSelections);
        _runRelicOperations.AddRange(root.RelicOperations);
        _runQuestMarkers.AddRange(root.QuestMarkers);
        _runEvidenceOrdinal = root.EventSelections.Count + root.RelicOperations.Count;
        ApplyPublicCombatRunContext(root.PublicContext);
        foreach (RunCoordinate coord in root.VisitedCoords)
            if (!(bool)ReflectionTools.Invoke(_run!, "AddVisitedMapCoord", ReflectionTools.Create(T("MegaCrit.Sts2.Core.Map.MapCoord"), coord.Col, coord.Row))!)
                throw new ProtocolException("invalid_public_run_progression", "Repeated visited coordinate.");
        foreach (RunHistoryCoordinate coordinate in root.HistoryCoords)
            _runRoomCoords[(coordinate.Act, coordinate.Index)] = (coordinate.Coord.Col, coordinate.Coord.Row);
        object mapSave = root.PublicMap.Deserialize(T("MegaCrit.Sts2.Core.Saves.Runs.SerializableActMap"), PortableRootJson)
            ?? throw new ProtocolException("invalid_public_map", "Public map deserialization failed.");
        ReflectionTools.Set(_run!, "Map", ReflectionTools.Create(T("MegaCrit.Sts2.Core.Map.SavedActMap"), mapSave));
        InstallPublicQuestOwners(root);
        EnsureNoRunQuestDomain();
        RegenerateRunBoundary(root);
        _composedRunWorld = true;
        return new { state = Capture(new { kind = "run_belief_composition" }),
                     composition = new { version = "pv1-run-composition-v5", algorithm, attempts,
                         mechanical_imports = algorithm == "current_root_joint_kernel" ? 1 : attempts,
                         elapsed_ms = wall.Elapsed.TotalMilliseconds, import_ms = importMs, kernel_ms = kernelMs,
                         rejected_public_evidence_counts = rejectedEvidence,
                         encounter_suffix_commitment = SampledEncounterSuffixCommitment(),
                         event_suffix_commitment = SampledEventSuffixCommitment(),
                         relic_suffix_commitment = SampledRelicSuffixCommitment(),
                         future_rng_commitment = SampledRunFutureRngCommitment(),
                         joint_latent_commitment = SampledRunJointLatentCommitment(),
                         closed_domains = new[] { "public", "public_derived", "encounters", "events", "relics", "future_rng" } } };
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
        var priorDeckBindings = new Dictionary<string, object>(_baseResetDeckCards, StringComparer.Ordinal);
        try
        {
            // XuShuxi: This is an opaque query substrate. It is imported only into
            // an isolated authority; existing Combat composition resamples its
            // draw/monster/future-RNG domains before every inner simulation.
            object creature = ReflectionTools.Get(_player!, "Creature")!;
            var currentDeckBindings = new Dictionary<string, object>(StringComparer.Ordinal);
            CardSpec[] deck = ReflectionTools.Enumerate(ReflectionTools.Get(ReflectionTools.Get(_player!, "Deck")!, "Cards"))
                .Where(card => card is not null).Select((value, index) =>
                {
                    object card = value!;
                    string id = _cardInstanceIds.Where(pair => ReferenceEquals(ReflectionTools.Get(pair.Key, "DeckVersion"), card))
                        .Select(pair => pair.Value).FirstOrDefault() ?? $"run-deck-{index}";
                    if (!currentDeckBindings.TryAdd(id, card))
                        throw RetiredCardError("BaseReset.Deck", "Ambiguous exact native Deck instance alias.");
                    object? enchantment = ReflectionTools.Get(card, "Enchantment");
                    return new CardSpec(id, Entry(card), Convert.ToInt32(ReflectionTools.Get(card, "CurrentUpgradeLevel")),
                        SavedNativeState(card).ToDictionary(pair => pair.Key, pair => JsonSerializer.SerializeToElement(pair.Value)),
                        enchantment is null ? null : new EnchantmentSpec(Entry(enchantment), Convert.ToInt32(ReflectionTools.Get(enchantment, "Amount"))));
                }).ToArray();
            _baseResetDeckCards.Clear();
            foreach ((string id, object card) in currentDeckBindings) _baseResetDeckCards.Add(id, card);
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
        finally {
            _baseResetDeckCards.Clear();
            foreach ((string id, object card) in priorDeckBindings) _baseResetDeckCards.Add(id, card);
            _reset = reset; _runStage = stage; _runMode = mode; _hash = hash; _currentBranchHandle = handle;
        }
    }
}
