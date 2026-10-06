// Certified public rehydration, separate from latent generators. Author: XuShuxi.
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Sts2.NativeSim.Protocol;

namespace Sts2.NativeSim.Core;

public sealed partial class PersistentNativeCombatEnvironment
{
    // XuShuxi: Native character initialization is a public producer. Its facts
    // are captured before bootstrap combat; an empty reset deck is not its deck.
    private RunPlayerFacts? _initialCharacterPublicLoadout;

    // XuShuxi: This matrix is intentionally exhaustive over the native RUN
    // producers. A missing producer is an error; it never restores a save blob.
    private sealed record RunBoundaryContract(string Decision, string Owner, string Inputs,
        string Regeneration, string HiddenDependencies, string? FailureReason)
    {
        // XuShuxi: Native reconstruction is always followed by both Agent gates.
        public string EqualityGate => "FullPolicyVisibleState + RunInformationState/action keys exact";
        public string Status => FailureReason is null ? "SUPPORTED" : "EXPLICIT_FAIL_CLOSED_WITH_SOURCE_REASON";
    }

    private static readonly IReadOnlyDictionary<string, RunBoundaryContract> RunBoundaryContracts = new Dictionary<string, RunBoundaryContract> {
        ["map"] = new("map_choice", "RunState.Map", "public topology/visited coordinates/progression", "SavedActMap + AddVisitedMapCoord", "joint encounter/event/relic posterior; independent future RNG", null),
        ["rewards"] = new("room_reward_choice", "CombatRoom/RewardsSet", "public encounter/ordinary reward faces/resolved indices", "MarkPreFinished + EmptyForRoom + native reward constructors", "conditioned encounter prefix; independent future RNG", null),
        ["rest"] = new("rest_choice", "RestSiteRoom/RestSiteSynchronizer", "current public player/progression", "BeginRestSite/GetLocalOptions", "fresh source-native options; no repeated room-entry hooks", null),
        ["shop"] = new("shop_choice", "MerchantRoom/MerchantInventory", "public offers/prices/purchases", "MerchantInventory.CreateForNormalMerchant", "public base-price constraints, purchased/refilled slots and native continuation", "MerchantInventory.CreateForNormalMerchant/OnTryPurchaseWrapper: the current inventory, source base-price posterior and purchase/refill continuation do not yet have a certified reconstruction contract."),
        ["event"] = new("event_choice", "EventRoom/EventModel", "public event identity/current page/option faces/selection prefix", "EventSynchronizer.BeginEvent + specific EventModel producer", "current page variables, source RNG materializations and suspended producer continuation", "Historical ActModel.PullNextEvent eligibility is certified separately. Current EventModel pages/variables and their continuation require a model-specific public producer contract; no raw EventModel state is accepted."),
        ["treasure"] = new("treasure_choice", "TreasureRoomRelicSynchronizer", "public chest/open/revealed relic selection + ordered source pull", "BeginRelicPicking with already-conditioned sampled producer result", "sampled pre-open pull; single-player skip lifecycle", null),
        ["custom_reward"] = new("custom_reward_choice", "RewardsCmd", "public custom rewards and enclosing producer", "OfferCustom", "suspended producer continuation", "RewardsCmd.OfferCustom: the enclosing native producer continuation is not certified."),
        ["generic_choice"] = new("card/bundle/generic outstanding choice", "CardSelectCmd/PlayerChoiceSynchronizer", "public choice faces + enclosing producer", "producer-specific native selection command", "suspended call/selected-prefix continuation", "CardSelectCmd/PlayerChoiceSynchronizer: the enclosing native producer and continuation are not certified.")
    };

    private void ValidateRunBoundary()
    {
        bool smithChoice = _runStage == "rest" && _pendingChoice?.DecisionKind == "card_choice" && _publicRestSelection == "SMITH";
        string stage = _pendingChoice is not null && !smithChoice ? "generic_choice" : _pendingRewardsSet is not null ? "custom_reward" : _runStage;
        if (!_runMode || !RunBoundaryContracts.TryGetValue(stage, out RunBoundaryContract? contract))
            throw new ProtocolException("unsupported_run_boundary", $"No RUN strategic root at stage {stage}.");
        if (contract.FailureReason is not null)
            throw new ProtocolException("run_modal_producer_evidence_missing", $"domain={stage}; {contract.FailureReason}");
        if (_runStage == "rest" && _restSelectionStarted && !smithChoice)
            throw new ProtocolException("run_rest_continuation_evidence_missing", "A rest selection has already started; its producer continuation is not public option identity.");
        if (_runStage == "treasure" && _treasureResolved)
            throw new ProtocolException("unsupported_run_boundary", "Completed treasure is a leave-only transport boundary, not a RUN strategic decision.");
    }

    private RunCardFace PublicRootCard(object card, bool validateInstance = true)
    {
        if (validateInstance) ValidatePersistentRunModel(card);
        if (validateInstance && (ReflectionTools.Get(card, "Affliction") is not null
            || ReflectionTools.Get(ReflectionTools.Get(card, "EnergyCost")!, "HasLocalModifiers") is true))
            throw new ProtocolException("run_persistent_card_face_evidence_missing",
                $"model={Entry(card)}; deck affliction/local-cost modifier lifetime is not reconstructed by model/upgrades/enchantment alone.");
        object? enchantment = ReflectionTools.Get(card, "Enchantment");
        if (validateInstance && enchantment is not null) ValidatePersistentRunModel(enchantment);
        return new(Entry(card), Convert.ToInt32(ReflectionTools.Get(card, "CurrentUpgradeLevel")),
            enchantment is null ? null : new(Entry(enchantment), Convert.ToInt32(ReflectionTools.Get(enchantment, "Amount"))));
    }

    private RunPlayerFacts ExportPublicRunPlayer(bool validateInstances = true)
    {
        object creature = ReflectionTools.Get(_player!, "Creature")!;
        object[] relics = ReflectionTools.Enumerate(ReflectionTools.Get(_player!, "Relics")).Select(x => x!).ToArray();
        foreach (object relic in validateInstances ? relics : [])
        {
            ValidatePersistentRunModel(relic);
            if (ReflectionTools.Get(relic, "IsWax") is true || ReflectionTools.Get(relic, "IsMelted") is true)
                throw new ProtocolException("run_persistent_object_evidence_missing", $"{Entry(relic)}.IsWax/IsMelted requires public creation/melt evidence.");
        }
        return new(Convert.ToInt32(ReflectionTools.Get(creature, "CurrentHp")), Convert.ToInt32(ReflectionTools.Get(creature, "MaxHp")),
            Convert.ToInt32(ReflectionTools.Get(_player!, "Gold")), Convert.ToInt32(ReflectionTools.Get(creature, "Block")),
            ReflectionTools.Enumerate(ReflectionTools.Get(ReflectionTools.Get(_player!, "Deck")!, "Cards")).Select(x => PublicRootCard(x!, validateInstances)).ToArray(),
            relics.Select(PublicRootRelic).ToArray(),
            ReflectionTools.Enumerate(ReflectionTools.Get(_player!, "PotionSlots")).Select(x => x is null ? null : Entry(x)).ToArray());
    }

    private RunPlayerFacts InitialPublicRunPlayer()
    {
        ResetRequest initial = _reset!;
        if (initial.UseCharacterStartingLoadout)
            return _initialCharacterPublicLoadout ?? throw new ProtocolException("run_initialization_public_facts_missing", "Native character loadout was not captured before bootstrap combat.");
        int slots = Math.Max(3, (initial.Potions ?? []).Select(potion => potion.Slot + 1).DefaultIfEmpty(0).Max());
        string?[] potions = new string?[slots];
        foreach (PotionSpec potion in initial.Potions ?? []) potions[potion.Slot] = potion.ModelId;
        return new(initial.CurrentHp, initial.MaxHp, initial.Gold, 0,
            initial.Deck.Select(card => new RunCardFace(card.ModelId, card.Upgrades,
                card.Enchantment is null ? null : new(card.Enchantment.ModelId, card.Enchantment.Amount))).ToArray(),
            (initial.Relics ?? []).Select(relic => {
                object model = Mutable("AllRelics", relic.ModelId);
                if (relic.Counter is { } counter) ApplyRelicCounter(model, counter);
                return PublicRootRelic(model);
            }).ToArray(), potions);
    }

    private void ValidatePersistentRunModel(object model)
    {
        // XuShuxi: These two source-proven quest owners are reconstructed from
        // public marker history / AfterCreated's constant, never SavedProperty.
        if (Entry(model) is "FUR_COAT" or "SPOILS_MAP" || PublicRunCounterContracts.ContainsKey(Entry(model))) return;
        // XuShuxi: Both exact-build classes reset every mutable gameplay flag in
        // AfterCombatEnd. Every portable RUN boundary is outside combat, so the
        // constructor zeros are their source-proven settled state.
        if (Entry(model) is "ART_OF_WAR" or "PAELS_TEARS") return;
        // SavedProperties.From includes structured ModelId/SerializableCard values
        // that SavedNativeState omits. Inspect the source-backed property contract,
        // not a lossy saved scalar dictionary and never a factual field value.
        PropertyInfo[] properties = model.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(property => property.GetCustomAttributes(true).Any(attribute => attribute.GetType().Name == "SavedPropertyAttribute")).ToArray();
        if (properties.Length > 0)
            throw new ProtocolException("run_persistent_object_evidence_missing",
                $"model={Entry(model)}; fields={string.Join(',', properties.Select(property => property.Name))}; certified public face/history reconstruction is required; NativeState is forbidden.");
        FieldInfo[] unaudited = model.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(field => !field.IsInitOnly && !field.IsLiteral && !field.IsStatic).ToArray();
        if (unaudited.Length > 0)
            // XuShuxi: Absence of SavedProperty does not prove a field transient.
            // In particular PaelsEye has an unsaved last-turn participation flag.
            // Reject this instance family rather than silently resetting it.
            throw new ProtocolException("run_object_lifecycle_evidence_missing",
                $"model={Entry(model)}; fields={string.Join(',', unaudited.Select(field => field.Name))}; an audited reset/irrelevance or public-history reconstruction is required.");
        if (ReflectionTools.Get(model, "IsStackable") is true)
            throw new ProtocolException("run_persistent_object_evidence_missing", $"model={Entry(model)}; field=StackCount; public stack reconstruction is required.");
    }

    private void ValidateRunPlayer(RunPlayerFacts player)
    {
        if (player.CurrentHp <= 0 || player.MaxHp < player.CurrentHp || player.Gold < 0 || player.Block < 0 || player.Deck.Count == 0 || player.Potions.Count > 64)
            throw new ProtocolException("invalid_public_run_player", "Invalid living player public resources.");
        foreach (RunCardFace card in player.Deck)
        {
            ValidatePersistentRunModel(Mutable("AllCards", card.ModelId));
            if (card.Upgrades < 0 || card.Enchantment?.Amount < 0)
                throw new ProtocolException("invalid_public_run_player", "Invalid public card face.");
            if (card.Enchantment is not null) ValidatePersistentRunModel(Mutable("DebugEnchantments", card.Enchantment.ModelId));
        }
        foreach (RunRelicFace relic in player.Relics)
        {
            ValidatePersistentRunModel(Mutable("AllRelics", relic.ModelId));
            ValidatePublicRunCounter(relic);
        }
        foreach (string? potion in player.Potions) if (potion is not null) ValidatePersistentRunModel(Mutable("AllPotions", potion));
    }

    private ResetRequest IndependentRunReset(RunInitializationFacts initialization, long entropy, int attempt)
    {
        RunPlayerFacts loadout = initialization.Loadout;
        return new(new(_productVersion, _assemblyHash, _pckHash), $"run-search-{entropy:X16}-{attempt:X8}", null,
            initialization.Character, initialization.Ascension, "first", loadout.CurrentHp, loadout.MaxHp,
            initialization.UseCharacterStartingLoadout ? [] : loadout.Deck.Select((card, i) => new CardSpec($"public-initial-{i}", card.ModelId, card.Upgrades, null,
                card.Enchantment is null ? null : new(card.Enchantment.ModelId, card.Enchantment.Amount))).ToArray(), [],
            initialization.UseCharacterStartingLoadout ? [] : loadout.Relics.Select(face => new RelicSpec(face.ModelId,
                PublicRunCounterContracts.ContainsKey(face.ModelId) ? NativePublicRunCounter(face) : null)).ToArray(),
            initialization.UseCharacterStartingLoadout ? [] : loadout.Potions.Select((id, slot) => id is null ? null : new PotionSpec(id, slot)).Where(x => x is not null).Select(x => x!).ToArray(),
            loadout.Gold, UseCharacterStartingLoadout: initialization.UseCharacterStartingLoadout);
    }

    private object RehydratePublicCard(RunCardFace face)
    {
        ValidatePersistentRunModel(Mutable("AllCards", face.ModelId));
        object card = Mutable("AllCards", face.ModelId);
        if (face.ModelId == "SPOILS_MAP") ReflectionTools.Set(card, "SpoilsActIndex", 1);
        if (face.Enchantment is not null)
        {
            object enchantment = Mutable("DebugEnchantments", face.Enchantment.ModelId);
            ReflectionTools.Invoke(card, "EnchantInternal", enchantment, Convert.ToDecimal(face.Enchantment.Amount));
            ReflectionTools.Invoke(enchantment, "ModifyCard");
        }
        for (int upgrade = 0; upgrade < face.Upgrades; upgrade++)
        {
            ReflectionTools.Invoke(card, "UpgradeInternal"); ReflectionTools.Invoke(card, "FinalizeUpgradeInternal");
        }
        ReflectionTools.Invoke(_run!, "AddCard", card, _player!);
        return card;
    }

    private void InstallPublicRunPlayer(RunPlayerFacts facts)
    {
        object deck = ReflectionTools.Get(_player!, "Deck")!, creature = ReflectionTools.Get(_player!, "Creature")!;
        foreach (object? card in ReflectionTools.Enumerate(ReflectionTools.Get(deck, "Cards")))
            if (card is not null) ReflectionTools.Invoke(_run!, "RemoveCard", card);
        ReflectionTools.Invoke(deck, "Clear", true);
        _cardInstanceIds.Clear();
        foreach (RunCardFace card in facts.Deck) ReflectionTools.Invoke(deck, "AddInternal", RehydratePublicCard(card), -1, true);
        foreach (object? relic in ReflectionTools.Enumerate(ReflectionTools.Get(_player!, "Relics")))
            if (relic is not null) ReflectionTools.Invoke(_player!, "RemoveRelicInternal", relic, true);
        foreach (RunRelicFace face in facts.Relics)
        {
            object relic = Mutable("AllRelics", face.ModelId);
            if (PublicRunCounterContracts.TryGetValue(face.ModelId, out var contract))
                ReflectionTools.Set(relic, contract.Property, NativePublicRunCounter(face));
            ReflectionTools.Invoke(_player!, "AddRelicInternal", relic, -1, true);
        }
        foreach (object? potion in ReflectionTools.Enumerate(ReflectionTools.Get(_player!, "PotionSlots")))
            if (potion is not null) ReflectionTools.Invoke(_player!, "DiscardPotionInternal", potion, true);
        ReflectionTools.Invoke(_player!, "SetMaxPotionCountInternal", facts.Potions.Count);
        for (int slot = 0; slot < facts.Potions.Count; slot++)
            if (facts.Potions[slot] is { } id) ReflectionTools.Invoke(_player!, "AddPotionInternal", Mutable("AllPotions", id), slot, true);
        ReflectionTools.Set(creature, "CurrentHp", facts.CurrentHp); ReflectionTools.Set(creature, "MaxHp", facts.MaxHp);
        ReflectionTools.Set(creature, "Block", facts.Block); ReflectionTools.Set(_player!, "Gold", facts.Gold);
    }

    private void ValidateRunHistoryDomain(IReadOnlyDictionary<string, JsonElement> context)
    {
        if (!context.Keys.Order().SequenceEqual(new[] { "act_floor", "act_index", "public_odds", "room_history" }))
            throw new ProtocolException("unsupported_combat_run_context", "Unaudited Run context field.");
        foreach (JsonElement point in context["room_history"].EnumerateArray())
        {
            if (!point.EnumerateObject().Select(pair => pair.Name).Order().SequenceEqual(new[] { "act", "point_type", "rooms" }))
                throw new ProtocolException("unsupported_combat_run_context", "Unaudited public history point.");
            JsonElement[] rooms = point.GetProperty("rooms").EnumerateArray().ToArray();
            if (rooms.Length != 1)
                throw new ProtocolException("run_nested_room_producer_evidence_missing", "An explicit/nested encounter cannot be consumed as an Act encounter-sequence pull.");
            string type = rooms[0].GetProperty("room_type").GetString()!;
            if (type is not ("Monster" or "Elite" or "Boss" or "RestSite" or "Event" or "Shop" or "Treasure"))
                throw new ProtocolException("unsupported_run_history_producer", $"Unaudited history producer {type}.");
        }
    }

    private bool ReplayPublicEncounters(PortableRunRoot root, out string contradiction)
    {
        object[] acts = ReflectionTools.Enumerate(ReflectionTools.Get(_run!, "Acts")).Select(x => x!).ToArray();
        int currentAct = root.PublicContext["act_index"].GetInt32();
        if (currentAct < 0 || currentAct >= acts.Length) throw new ProtocolException("invalid_public_run_progression", "Invalid act index.");
        contradiction = "public_boss_identity";
        if (Entry(ReflectionTools.Get(acts[currentAct], "BossEncounter")!) != root.Boss
            || (ReflectionTools.Get(acts[currentAct], "SecondBossEncounter") is { } second ? Entry(second) : null) != root.SecondBoss) return false;
        Dictionary<int, int> historyIndices = [];
        foreach (JsonElement point in root.PublicContext["room_history"].EnumerateArray())
        {
            int act = point.GetProperty("act").GetInt32();
            if (act < 0 || act > currentAct) throw new ProtocolException("invalid_public_run_progression", "Future history act.");
            ReflectionTools.Set(_run!, "CurrentActIndex", act);
            int historyIndex = historyIndices.GetValueOrDefault(act);
            historyIndices[act] = historyIndex + 1;
            foreach (JsonElement room in point.GetProperty("rooms").EnumerateArray())
            {
                string type = room.GetProperty("room_type").GetString()!;
                if (type is "RestSite" or "Shop" or "Treasure" || type == "Event" && point.GetProperty("point_type").GetString() != "Ancient") continue;
                JsonElement model = room.GetProperty("model_id");
                if (model.ValueKind != JsonValueKind.Object || !model.EnumerateObject().Select(pair => pair.Name).Order().SequenceEqual(new[] { "category", "entry" }))
                    throw new ProtocolException("invalid_public_encounter_evidence", "Encounter identity must contain only category and entry.");
                object roomType = Enum.Parse(T("MegaCrit.Sts2.Core.Rooms.RoomType"), type);
                object candidate;
                if (type == "Event")
                {
                    if (point.GetProperty("point_type").GetString() == "Ancient")
                        candidate = ReflectionTools.Invoke(acts[act], "PullAncient")!;
                    else throw new ProtocolException("invalid_public_event_evidence", "Normal Events must be replayed in the joint public operation order.");
                }
                else candidate = ReflectionTools.Invoke(acts[act], "PullNextEncounter", roomType)!;
                contradiction = type == "Event" ? "observed_Event_selection" : $"observed_{type}_encounter:act={act}:history_index={historyIndex}";
                if (Entry(candidate) != model.GetProperty("entry").GetString()) return false;
                ReflectionTools.Invoke(acts[act], "MarkRoomVisited", roomType);
            }
        }
        return true;
    }

    private string SampledEncounterSuffixCommitment()
    {
        // XuShuxi: Acceptance witness for sampled worlds only. Neither factual
        // commitments nor hidden queues are exported into public observations.
        object[] suffixes = ReflectionTools.Enumerate(ReflectionTools.Get(_run!, "Acts")).Select(act => {
            object rooms = ReflectionTools.Get(act!, "_rooms")!;
            return (object)new {
                normal = ReflectionTools.Enumerate(ReflectionTools.Get(rooms, "normalEncounters"))
                    .Skip(Convert.ToInt32(ReflectionTools.Get(rooms, "normalEncountersVisited"))).Select(x => Entry(x!)).ToArray(),
                elite = ReflectionTools.Enumerate(ReflectionTools.Get(rooms, "eliteEncounters"))
                    .Skip(Convert.ToInt32(ReflectionTools.Get(rooms, "eliteEncountersVisited"))).Select(x => Entry(x!)).ToArray()
            };
        }).ToArray();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(suffixes))));
    }

    private static void ValidatePublicMap(JsonElement map)
    {
        void Only(JsonElement value, params string[] names)
        {
            if (value.ValueKind != JsonValueKind.Object || value.EnumerateObject().Any(pair => !names.Contains(pair.Name)))
                throw new ProtocolException("invalid_public_map", "Unaudited field in public map.");
        }
        void Coord(JsonElement value) { Only(value, "col", "row"); _ = value.GetProperty("col").GetInt32(); _ = value.GetProperty("row").GetInt32(); }
        void Point(JsonElement point)
        {
            Only(point, "coord", "type", "children"); Coord(point.GetProperty("coord"));
            if (point.TryGetProperty("children", out JsonElement children) && children.ValueKind != JsonValueKind.Null)
                foreach (JsonElement child in children.EnumerateArray()) Coord(child);
        }
        Only(map, "points", "boss", "second_boss", "start", "start_coords", "width", "height");
        foreach (JsonElement point in map.GetProperty("points").EnumerateArray()) Point(point);
        Point(map.GetProperty("boss")); Point(map.GetProperty("start"));
        if (map.TryGetProperty("second_boss", out JsonElement second) && second.ValueKind != JsonValueKind.Null) Point(second);
        if (map.TryGetProperty("start_coords", out JsonElement starts) && starts.ValueKind != JsonValueKind.Null)
            foreach (JsonElement coord in starts.EnumerateArray()) Coord(coord);
    }

    private RunRewardFacts[] ExportPublicRoomRewards()
    {
        if (_runStage != "rewards") return [];
        object room = ReflectionTools.Get(_run!, "CurrentRoom")!;
        if (ReflectionTools.Enumerate(ReflectionTools.Get(room, "ExtraRewards")).Count != 0)
            throw new ProtocolException("run_extra_reward_producer_evidence_missing", "CombatRoom.ExtraRewards may carry stolen-gold/card provenance not certified by ordinary room rewards.");
        return ReflectionTools.Enumerate(ReflectionTools.Get(_roomRewardsSet!, "Rewards")).Select((reward, index) => {
            string type = reward!.GetType().Name;
            if (type is not ("GoldReward" or "PotionReward" or "CardReward" or "RelicReward"))
                throw new ProtocolException("run_reward_producer_evidence_missing", $"{type} requires its specific generation and continuation evidence.");
            return new RunRewardFacts(type, type == "GoldReward" ? Convert.ToInt32(ReflectionTools.Get(reward, "Amount")) : null,
                type is "PotionReward" or "RelicReward" ? Entry(ReflectionTools.Get(reward, type == "PotionReward" ? "Potion" : "Relic")!) : null,
                type == "CardReward" ? ReflectionTools.Enumerate(ReflectionTools.Get(reward, "Cards")).Select(card => PublicRootCard(card!)).ToArray() : [],
                _resolvedRoomRewards.Contains(index) || ReflectionTools.Get(reward, "SuccessfullySelected") is true);
        }).ToArray();
    }

    private void RegenerateRunBoundary(PortableRunRoot root)
    {
        while (Convert.ToInt32(ReflectionTools.Get(_run!, "CurrentRoomCount")) > 0) ReflectionTools.Invoke(_run!, "PopCurrentRoom");
        _runStage = root.Stage;
        if (root.Stage == "map") return;
        if (root.Stage == "treasure") { RegeneratePublicTreasure(root); return; }
        JsonElement[] history = root.PublicContext["room_history"].EnumerateArray().ToArray();
        if (history.Length == 0)
            throw new ProtocolException("invalid_run_modal_evidence", "A native modal requires its public room producer.");
        string lastRoomType = history[^1].GetProperty("rooms")[0].GetProperty("room_type").GetString()!;
        if (root.Stage == "rest" ? lastRoomType != "RestSite" : lastRoomType is not ("Monster" or "Elite" or "Boss"))
            throw new ProtocolException("invalid_run_modal_evidence", "Modal stage differs from its public native room owner.");
        if (root.Stage == "rest")
        {
            object room = ReflectionTools.Create(T("MegaCrit.Sts2.Core.Rooms.RestSiteRoom"));
            ReflectionTools.Invoke(_run!, "PushRoom", room);
            // Regenerate options without repeating AfterRoomEntered hooks: the
            // installed public resources already include their factual effects.
            object synchronizer = ReflectionTools.Get(ReflectionTools.GetStatic(T("MegaCrit.Sts2.Core.Runs.RunManager"), "Instance")!, "RestSiteSynchronizer")!;
            ReflectionTools.Invoke(synchronizer, "BeginRestSite"); ReflectionTools.Set(room, "_synchronizer", synchronizer);
            _restOptions = ReflectionTools.Enumerate(ReflectionTools.Get(room, "Options")).Select(x => x!).ToArray();
            _restMode = true; _restSelectionStarted = false;
            _publicRestSelection = null;
            if (root.RestSelection is not null)
            {
                // Re-enter the source producer, which suspends before mutation.
                // The newly generated TaskCompletionSource retains the genuine
                // upgrade/AfterRestSiteSmith continuation in this sampled world.
                ChooseRestAsync(root.RestSelection).GetAwaiter().GetResult();
                if (_pendingChoice?.DecisionKind != "card_choice")
                    throw new ProtocolException("run_rest_regeneration_mismatch", "Smith producer did not recreate its native card selector boundary.");
            }
            return;
        }
        JsonElement last = root.PublicContext["room_history"].EnumerateArray().Last().GetProperty("rooms")[0];
        string encounterId = last.GetProperty("model_id").GetProperty("entry").GetString()!;
        object combatRoom = ReflectionTools.Create(T("MegaCrit.Sts2.Core.Rooms.CombatRoom"), Mutable("AllEncounters", encounterId), _run!);
        ReflectionTools.Invoke(combatRoom, "MarkPreFinished"); ReflectionTools.Invoke(_run!, "PushRoom", combatRoom);
        _roomRewardsSet = ReflectionTools.Create(T("MegaCrit.Sts2.Core.Rewards.RewardsSet"), _player!, null);
        ReflectionTools.Invoke(_roomRewardsSet, "EmptyForRoom", combatRoom);
        object rewards = ReflectionTools.Get(_roomRewardsSet, "Rewards")!;
        object roomType = ReflectionTools.Get(combatRoom, "RoomType")!;
        object options = ReflectionTools.InvokeStatic(T("MegaCrit.Sts2.Core.Runs.CardCreationOptions"), "ForRoom", _player!, roomType)!;
        for (int index = 0; index < root.Rewards.Count; index++)
        {
            RunRewardFacts face = root.Rewards[index];
            object reward = face.Kind switch {
                "GoldReward" when face.Amount is >= 0 && face.ModelId is null && face.Cards.Count == 0 => ReflectionTools.Create(T("MegaCrit.Sts2.Core.Rewards.GoldReward"), face.Amount.Value, _player!, false),
                "PotionReward" when face.ModelId is not null && face.Amount is null && face.Cards.Count == 0 => ReflectionTools.Create(T("MegaCrit.Sts2.Core.Rewards.PotionReward"), Mutable("AllPotions", face.ModelId), _player!),
                "RelicReward" when face.ModelId is not null && face.Amount is null && face.Cards.Count == 0 => RegeneratePublicRelicReward(face),
                "CardReward" when face.Amount is null && face.ModelId is null && face.Cards.Count > 0 => RegenerateOrdinaryCardReward(face, options),
                _ => throw new ProtocolException("run_reward_producer_evidence_missing", "Unaudited public reward producer.")
            };
            ReflectionTools.Invoke(rewards, "Add", reward);
            if (face.Resolved) _resolvedRoomRewards.Add(index);
        }
        object rewardSynchronizer = ReflectionTools.Get(ReflectionTools.GetStatic(T("MegaCrit.Sts2.Core.Runs.RunManager"), "Instance")!, "RewardsSetSynchronizer")!;
        EnsureRewardsSetViewing(rewardSynchronizer, _roomRewardsSet);
    }

    private object RegeneratePublicRelicReward(RunRewardFacts face)
    {
        // XuShuxi: The bag pull was already conditioned by the ordered journal.
        // Regenerate the ordinary reward owner, without a second hidden pull.
        object reward = ReflectionTools.Create(T("MegaCrit.Sts2.Core.Rewards.RelicReward"), _player!);
        ReflectionTools.Set(reward, "_relic", Mutable("AllRelics", face.ModelId!));
        return reward;
    }

    private object RegenerateOrdinaryCardReward(RunRewardFacts face, object options)
    {
        // XuShuxi: Use the ordinary producer constructor, preserving IsCardReward
        // options and RelicObtained subscription. The manual-card constructor has
        // different modification semantics and is not a room-reward substitute.
        object reward = ReflectionTools.Create(T("MegaCrit.Sts2.Core.Rewards.CardReward"), options, face.Cards.Count, _player!, null);
        object creations = ReflectionTools.Get(reward, "_cards")!;
        foreach (RunCardFace card in face.Cards)
            ReflectionTools.Invoke(creations, "Add", ReflectionTools.Create(T("MegaCrit.Sts2.Core.Entities.Cards.CardCreationResult"), RehydratePublicCard(card)));
        // Driftwood is the sole native CanReroll producer. No reroll action is
        // exposed by this ordinary room-reward transport; no past reroll is lost.
        ReflectionTools.Set(reward, "CanReroll", ReflectionTools.Enumerate(ReflectionTools.Get(_player!, "Relics")).Any(relic => Entry(relic!) == "DRIFTWOOD"));
        return reward;
    }
}
