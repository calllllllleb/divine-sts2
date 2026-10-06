using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2.NativeSim.Protocol;

public static class ProtocolConstants { public const int Version = 1; public const int ObservationSchemaVersion = 2; public const int CombatRootSchemaVersion = 6; }
public sealed record RpcRequest([property: JsonPropertyName("id")] string Id, [property: JsonPropertyName("method")] string Method, [property: JsonPropertyName("params")] JsonElement Parameters);
public sealed record RpcResponse([property: JsonPropertyName("id")] string Id, [property: JsonPropertyName("ok")] bool Ok, [property: JsonPropertyName("result")] object? Result = null, [property: JsonPropertyName("error")] ProtocolError? Error = null);
public sealed record ProtocolError([property: JsonPropertyName("code")] string Code, [property: JsonPropertyName("message")] string Message, [property: JsonPropertyName("details")] object? Details = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record GameBuildSpec([property: JsonPropertyName("version")] string? Version = null, [property: JsonPropertyName("assembly_sha256")] string? AssemblySha256 = null, [property: JsonPropertyName("pck_sha256")] string? PckSha256 = null);
public sealed record EnchantmentSpec([property: JsonPropertyName("model_id")] string ModelId, [property: JsonPropertyName("amount")] int Amount = 1);
public sealed record CardSpec([property: JsonPropertyName("instance_id")] string InstanceId, [property: JsonPropertyName("model_id")] string ModelId, [property: JsonPropertyName("upgrades")] int Upgrades = 0, [property: JsonPropertyName("native_state")] IReadOnlyDictionary<string, JsonElement>? NativeState = null, [property: JsonPropertyName("enchantment")] EnchantmentSpec? Enchantment = null);
public sealed record RelicSpec([property: JsonPropertyName("model_id")] string ModelId, [property: JsonPropertyName("counter")] int? Counter = null, [property: JsonPropertyName("native_state")] IReadOnlyDictionary<string, JsonElement>? NativeState = null);
public sealed record PotionSpec([property: JsonPropertyName("model_id")] string ModelId, [property: JsonPropertyName("slot")] int Slot, [property: JsonPropertyName("native_state")] IReadOnlyDictionary<string, JsonElement>? NativeState = null);
public sealed record EnemySpec(
    [property: JsonPropertyName("model_id")] string ModelId,
    [property: JsonPropertyName("current_hp")] int CurrentHp,
    [property: JsonPropertyName("max_hp")] int MaxHp,
    [property: JsonPropertyName("next_move_id")] string? NextMoveId = null,
    [property: JsonPropertyName("move_history")] IReadOnlyList<string>? MoveHistory = null,
    [property: JsonPropertyName("block")] int? Block = null);
public sealed record ResetRequest(
    [property: JsonPropertyName("game_build")] GameBuildSpec GameBuild,
    [property: JsonPropertyName("seed")] string Seed,
    [property: JsonPropertyName("rng_counters")] IReadOnlyDictionary<string, int>? RngCounters,
    [property: JsonPropertyName("character")] string Character,
    [property: JsonPropertyName("ascension")] int Ascension,
    [property: JsonPropertyName("encounter")] string Encounter,
    [property: JsonPropertyName("current_hp")] int CurrentHp,
    [property: JsonPropertyName("max_hp")] int MaxHp,
    [property: JsonPropertyName("deck")] IReadOnlyList<CardSpec> Deck,
    [property: JsonPropertyName("initial_hand")] IReadOnlyList<string>? InitialHand,
    [property: JsonPropertyName("relics")] IReadOnlyList<RelicSpec>? Relics,
    [property: JsonPropertyName("potions")] IReadOnlyList<PotionSpec>? Potions,
    [property: JsonPropertyName("gold")] int Gold,
    [property: JsonPropertyName("turn")] int Turn = 1,
    [property: JsonPropertyName("energy")] int? Energy = null,
    [property: JsonPropertyName("run_context")] IReadOnlyDictionary<string, JsonElement>? RunContext = null,
    [property: JsonPropertyName("stars")] int? Stars = null,
    [property: JsonPropertyName("enemies")] IReadOnlyList<EnemySpec>? Enemies = null,
    [property: JsonPropertyName("initial_draw_pile")] IReadOnlyList<string>? InitialDrawPile = null,
    [property: JsonPropertyName("invoke_combat_entry_hooks")] bool InvokeCombatEntryHooks = false,
    [property: JsonPropertyName("capture_orbs")] bool CaptureOrbs = true,
    [property: JsonPropertyName("use_character_starting_loadout")] bool UseCharacterStartingLoadout = false);
// XuShuxi: The private snapshot travels as JSON, never as a worker-local handle.
public sealed record PortableCombatRoot(
    [property: JsonPropertyName("schema_version")] int SchemaVersion,
    [property: JsonPropertyName("game_build")] GameBuildSpec GameBuild,
    [property: JsonPropertyName("base_reset")] ResetRequest BaseReset,
    [property: JsonPropertyName("combat_snapshot")] JsonElement CombatSnapshot,
    [property: JsonPropertyName("expected_state_hash")] string ExpectedStateHash);
public sealed record StepRequest([property: JsonPropertyName("action_id")] string ActionId);
// Search-private Run composition payload. Author: XuShuxi.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PortableRunRoot(
    [property: JsonPropertyName("schema_version"), JsonRequired] int SchemaVersion,
    [property: JsonPropertyName("game_build"), JsonRequired] GameBuildSpec GameBuild,
    [property: JsonPropertyName("initialization"), JsonRequired] RunInitializationFacts Initialization,
    [property: JsonPropertyName("current_player"), JsonRequired] RunPlayerFacts CurrentPlayer,
    [property: JsonPropertyName("public_context"), JsonRequired] IReadOnlyDictionary<string, JsonElement> PublicContext,
    [property: JsonPropertyName("public_relic_pulls"), JsonRequired] IReadOnlyList<string> PublicRelicPulls,
    [property: JsonPropertyName("event_selections"), JsonRequired] IReadOnlyList<RunEventSelectionFacts> EventSelections,
    [property: JsonPropertyName("relic_operations"), JsonRequired] IReadOnlyList<RunRelicOperationFacts> RelicOperations,
    [property: JsonPropertyName("quest_markers"), JsonRequired] IReadOnlyList<RunQuestMarkerFacts> QuestMarkers,
    [property: JsonPropertyName("visited_coords"), JsonRequired] IReadOnlyList<RunCoordinate> VisitedCoords,
    [property: JsonPropertyName("history_coords"), JsonRequired] IReadOnlyList<RunHistoryCoordinate> HistoryCoords,
    [property: JsonPropertyName("stage"), JsonRequired] string Stage,
    [property: JsonPropertyName("treasure")] RunTreasureFacts? Treasure,
    [property: JsonPropertyName("rest_selection")] string? RestSelection,
    [property: JsonPropertyName("rewards"), JsonRequired] IReadOnlyList<RunRewardFacts> Rewards,
    [property: JsonPropertyName("public_map"), JsonRequired] JsonElement PublicMap,
    [property: JsonPropertyName("boss"), JsonRequired] string Boss,
    [property: JsonPropertyName("second_boss")] string? SecondBoss);
// XuShuxi: Whitelisted public faces. These contracts deliberately cannot express
// a native instance id, seed, counter set, saved property bag, queue, or handle.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RunCardFace(
    [property: JsonPropertyName("model_id"), JsonRequired] string ModelId,
    [property: JsonPropertyName("upgrades"), JsonRequired] int Upgrades,
    [property: JsonPropertyName("enchantment")] RunEnchantmentFace? Enchantment);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RunEnchantmentFace(
    [property: JsonPropertyName("model_id"), JsonRequired] string ModelId,
    [property: JsonPropertyName("amount"), JsonRequired] int Amount);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RunPlayerFacts(
    [property: JsonPropertyName("current_hp"), JsonRequired] int CurrentHp,
    [property: JsonPropertyName("max_hp"), JsonRequired] int MaxHp,
    [property: JsonPropertyName("gold"), JsonRequired] int Gold,
    [property: JsonPropertyName("block"), JsonRequired] int Block,
    [property: JsonPropertyName("deck"), JsonRequired] IReadOnlyList<RunCardFace> Deck,
    [property: JsonPropertyName("relics"), JsonRequired] IReadOnlyList<RunRelicFace> Relics,
    [property: JsonPropertyName("potions"), JsonRequired] IReadOnlyList<string?> Potions);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RunRelicFace(
    [property: JsonPropertyName("model_id"), JsonRequired] string ModelId,
    [property: JsonPropertyName("display_amount")] int? DisplayAmount);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RunInitializationFacts(
    [property: JsonPropertyName("character"), JsonRequired] string Character,
    [property: JsonPropertyName("ascension"), JsonRequired] int Ascension,
    [property: JsonPropertyName("use_character_starting_loadout"), JsonRequired] bool UseCharacterStartingLoadout,
    [property: JsonPropertyName("loadout"), JsonRequired] RunPlayerFacts Loadout);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RunCoordinate(
    [property: JsonPropertyName("col"), JsonRequired] int Col, [property: JsonPropertyName("row")] int Row);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RunHistoryCoordinate(
    [property: JsonPropertyName("act"), JsonRequired] int Act, [property: JsonPropertyName("index")] int Index,
    [property: JsonPropertyName("coord"), JsonRequired] RunCoordinate Coord);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RunQuestMarkerFacts(
    [property: JsonPropertyName("model_id"), JsonRequired] string ModelId,
    [property: JsonPropertyName("act"), JsonRequired] int Act,
    [property: JsonPropertyName("coordinates"), JsonRequired] IReadOnlyList<RunCoordinate> Coordinates);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RunRewardFacts(
    [property: JsonPropertyName("kind"), JsonRequired] string Kind,
    [property: JsonPropertyName("amount")] int? Amount,
    [property: JsonPropertyName("model_id")] string? ModelId,
    [property: JsonPropertyName("cards"), JsonRequired] IReadOnlyList<RunCardFace> Cards,
    [property: JsonPropertyName("resolved"), JsonRequired] bool Resolved);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RunTreasureFacts(
    [property: JsonPropertyName("opened"), JsonRequired] bool Opened,
    [property: JsonPropertyName("relic_options"), JsonRequired] IReadOnlyList<string> RelicOptions);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RunEventSelectionFacts(
    [property: JsonPropertyName("ordinal"), JsonRequired] int Ordinal,
    [property: JsonPropertyName("act"), JsonRequired] int Act,
    [property: JsonPropertyName("history_index"), JsonRequired] int HistoryIndex,
    [property: JsonPropertyName("operation_ordinal"), JsonRequired] int OperationOrdinal,
    [property: JsonPropertyName("total_floor"), JsonRequired] int TotalFloor,
    [property: JsonPropertyName("model_id"), JsonRequired] string ModelId,
    [property: JsonPropertyName("eligible_event_ids"), JsonRequired] IReadOnlyList<string> EligibleEventIds,
    [property: JsonPropertyName("has_lantern_key"), JsonRequired] bool HasLanternKey,
    [property: JsonPropertyName("luminous_gold_eligible"), JsonRequired] bool LuminousGoldEligible,
    [property: JsonPropertyName("failure_reason")] string? FailureReason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RunRelicOperationFacts(
    [property: JsonPropertyName("ordinal"), JsonRequired] int Ordinal,
    [property: JsonPropertyName("act"), JsonRequired] int Act,
    [property: JsonPropertyName("total_floor"), JsonRequired] int TotalFloor,
    [property: JsonPropertyName("bag"), JsonRequired] string Bag,
    [property: JsonPropertyName("producer"), JsonRequired] string Producer,
    [property: JsonPropertyName("operation"), JsonRequired] string Operation,
    [property: JsonPropertyName("rarity_source")] string? RaritySource,
    [property: JsonPropertyName("filter")] string? Filter,
    [property: JsonPropertyName("blacklist"), JsonRequired] IReadOnlyList<string> Blacklist,
    [property: JsonPropertyName("pull_ordinal")] int? PullOrdinal,
    [property: JsonPropertyName("model_id")] string? ModelId,
    [property: JsonPropertyName("failure_reason")] string? FailureReason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ComposeRunRootRequest(
    [property: JsonPropertyName("root"), JsonRequired] PortableRunRoot Root,
    [property: JsonPropertyName("search_entropy"), JsonRequired] long SearchEntropy,
    [property: JsonPropertyName("mechanical_root")] RunMechanicalSnapshot? MechanicalRoot = null);
// XuShuxi: One private mechanical anchor, bound to the existing public owner.
// This is deliberately not a save/NativeState/RNG bag. No native identity survives.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RunMechanicalSnapshot(
    [property: JsonPropertyName("schema_version"), JsonRequired] int SchemaVersion,
    [property: JsonPropertyName("public_root_sha256"), JsonRequired] string PublicRootSha256,
    [property: JsonPropertyName("base_max_energy"), JsonRequired] int BaseMaxEnergy,
    [property: JsonPropertyName("base_orb_slot_count"), JsonRequired] int BaseOrbSlotCount,
    [property: JsonPropertyName("started_with_neow"), JsonRequired] bool StartedWithNeow);
public sealed record EventResetRequest(
    [property: JsonPropertyName("state")] ResetRequest State,
    [property: JsonPropertyName("event_id")] string EventId);
public sealed record ItemRewardResetRequest(
    [property: JsonPropertyName("state")] ResetRequest State,
    [property: JsonPropertyName("reward_kind")] string RewardKind,
    [property: JsonPropertyName("model_id")] string? ModelId = null);
public sealed record CustomRewardResetRequest(
    [property: JsonPropertyName("state")] ResetRequest State,
    [property: JsonPropertyName("reward_kinds")] IReadOnlyList<string> RewardKinds,
    [property: JsonPropertyName("linked")] bool Linked = false);
// XuShuxi: Constraints contain public card faces only; native identity fields are rejected.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record VisibleDrawCardConstraint(
    [property: JsonPropertyName("model_id"), JsonRequired] string ModelId,
    [property: JsonPropertyName("upgrades"), JsonRequired] int Upgrades,
    [property: JsonPropertyName("current_cost"), JsonRequired] int? CurrentCost,
    [property: JsonPropertyName("costs_x"), JsonRequired] bool? CostsX,
    [property: JsonPropertyName("enchantment_model_id")] string? EnchantmentModelId = null,
    [property: JsonPropertyName("enchantment_amount")] int EnchantmentAmount = 0);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DrawOrderConstraints(
    [property: JsonPropertyName("known_draw_top"), JsonRequired] IReadOnlyList<VisibleDrawCardConstraint> KnownDrawTop,
    [property: JsonPropertyName("known_draw_bottom"), JsonRequired] IReadOnlyList<VisibleDrawCardConstraint> KnownDrawBottom);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ResampleDrawOrderRequest(
    [property: JsonPropertyName("state_handle"), JsonRequired] string StateHandle,
    [property: JsonPropertyName("search_entropy"), JsonRequired] string SearchEntropy,
    [property: JsonPropertyName("constraints"), JsonRequired] DrawOrderConstraints Constraints);
// XuShuxi: Search entropy is independent of factual seeds/counters.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ForkFutureRngRequest(
    [property: JsonPropertyName("state_handle"), JsonRequired] string StateHandle,
    [property: JsonPropertyName("search_entropy"), JsonRequired] string SearchEntropy);
// XuShuxi: Search supplies a complete public-history-derived move log, never a factual move answer.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MonsterMoveSelection(
    [property: JsonPropertyName("combat_id"), JsonRequired] uint CombatId,
    [property: JsonPropertyName("state_log"), JsonRequired] IReadOnlyList<string> StateLog,
    [property: JsonPropertyName("next_move_id"), JsonRequired] string NextMoveId,
    [property: JsonPropertyName("performed_first_move"), JsonRequired] bool PerformedFirstMove,
    [property: JsonPropertyName("transient_follow_up_state_id")] string? TransientFollowUpStateId = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReconstructMonsterMovesRequest(
    [property: JsonPropertyName("state_handle"), JsonRequired] string StateHandle,
    [property: JsonPropertyName("selections"), JsonRequired] IReadOnlyList<MonsterMoveSelection> Selections);
// XuShuxi: Empty selections request structure only; evaluated rules require a complete hypothetical world.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DescribeMonsterMoveRulesRequest(
    [property: JsonPropertyName("state_handle"), JsonRequired] string StateHandle,
    [property: JsonPropertyName("selections"), JsonRequired] IReadOnlyList<MonsterMoveSelection> Selections);
// XuShuxi: A roll event is a private timing token, not a factual move answer.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DescribeMonsterRollRulesRequest(
    [property: JsonPropertyName("state_handle"), JsonRequired] string StateHandle,
    [property: JsonPropertyName("event_index"), JsonRequired] int EventIndex,
    [property: JsonPropertyName("selections"), JsonRequired] IReadOnlyList<MonsterMoveSelection> Selections);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DescribeMonsterImmediateRuleRequest(
    [property: JsonPropertyName("state_handle"), JsonRequired] string StateHandle,
    [property: JsonPropertyName("event_index"), JsonRequired] int EventIndex,
    [property: JsonPropertyName("selections"), JsonRequired] IReadOnlyList<MonsterMoveSelection> Selections);
public sealed record RestoreRequest([property: JsonPropertyName("state_handle")] string StateHandle);
public sealed record LegalAction([property: JsonPropertyName("action_id")] string ActionId, [property: JsonPropertyName("kind")] string Kind, [property: JsonPropertyName("parameters")] IReadOnlyDictionary<string, object?> Parameters);
public sealed record EnvironmentResult(
    [property: JsonPropertyName("observation")] object Observation,
    [property: JsonPropertyName("state_hash")] string StateHash,
    [property: JsonPropertyName("legal_actions")] IReadOnlyList<LegalAction> LegalActions,
    [property: JsonPropertyName("terminated")] bool Terminated,
    [property: JsonPropertyName("victory")] bool Victory,
    [property: JsonPropertyName("state_handle")] string StateHandle,
    [property: JsonPropertyName("transition")] object? Transition = null,
    [property: JsonPropertyName("scoring_features")] object? ScoringFeatures = null,
    [property: JsonPropertyName("public_card_events")] object[]? PublicCardEvents = null);
