# PV1 native mid-run Run composition contract

Author: XuShuxi

Baseline: `d69604b519b2fc238bc5b73c927b004070825783`.
Branch: `pv1-general-midrun-composition-closure`; main is not merged.
Mechanics authority: shipped STS2 v0.107.1, build
`v0.107.1_a1f9e653f1e2_42520eb8b091`. Source evidence and the exact saved-state
census are in [PV1_MIDRUN_SOURCE_AUDIT.md](PV1_MIDRUN_SOURCE_AUDIT.md).
General mid-run composition is **not globally certified**.

## One public owner, two composition layers

Agent PolicyVisibleState remains the only model fact owner. Its existing
PolicyVisibleRunMemory receives schema-3 public event-selection, ordered relic
operation and quest-marker evidence. No second Agent Run state is introduced.

PortableRunRoot schema 4 separates `initialization` from `current_player`.
Initialization records character/ascension and public initial loadout facts.
Default character loadouts are captured from Player.CreateForNewRun/SetUpTest
before bootstrap combat; fresh worlds call that producer again and verify its
public output. Empty reset deck does not mean an empty default starting deck.
The current public HP/maxHP/gold/block, card model/upgrades/enchantment,
relic model/display counter, ordered potion belt, act/floor, topology, visited
coordinates and public room/progression history are installed separately.
`public_context` contains exactly act_index, act_floor, public_odds and room_history.

All Run DTOs reject unmapped fields; required fields and non-null public
collections are checked before sampling. Roots cannot express seed, RNG counters,
NativeState, native identity, future queues, private mutable state or a handle.
`public_map` has a strict completed-topology whitelist; the private generation-time
CanBeModified flag is omitted. Quest installation uses public ownership and the
source census, never marker boolean as a guessed identity.

## Joint posterior and finite rejection

For each independent search entropy, every candidate creates a new native Run
and executes the complete RunManager.GenerateRooms prior. The shared Ancient
subset allocation, shuffled events, weighted no-repeat encounter generation,
relic bags, tutorial modifications and DoubleBoss behavior stay native.

`Z ~ native prior`; accept exactly when replay is consistent with relevant public
history H. Accepted worlds therefore condition this joint prior on H. No resident
factual suffix, factual seed or factual counter participates. Future randomness
belongs to this independent search-local world.

Encounter replay calls PullNextEncounter/MarkRoomVisited in public act/category
order and matches every observed model; public current boss/second boss also match.
Event evidence records historical public eligibility ids, floor, LanternKey
presence, observed final id and chronological operation position. Native cyclic
scan, visited exclusion, hook fold and repetition fallback execute during replay.
LuminousChoir is handled by its public gold predicate and that sampled world's
native bag-availability query, preserving the joint event/bag dependency.

Relic evidence records ordered shared/player operations, producer, floor/act,
source rarity roll or requested rarity, front/back direction, audited filter and
public blacklist, causal removal reference and the revealed model id. Unrevealed
pull identity stays null; only the sampled native world materializes it. Replay
executes native filtering, refresh, rarity fallback and pull/remove behavior at
that historical public context. The old unordered depletion is a derived view.
Unknown filters/RNG-override producers reject with their named source reason.
Unaudited third bag observations are quarantined for root export, without throwing
inside factual mechanics or inventing a player bag owner. Revealed depletion in a
known player/shared bag remains public even if its composition producer rejects.
Factual reset-private overrides, including nonempty supplied RNG counters, reject
at root export and are never transferred into initialization parameters.

Maximum attempts per composition request: **8192**. Exhaustion returns
`run_composition_attempts_exhausted` with domain, attempt count, first contradicted
public evidence and rejection counts. It never becomes a death outcome, factual
fallback or ignored evidence. The Python client uses a separate finite composition
watchdog (default 600 seconds); ordinary request timeouts retain their own limit.
Rare long observed prefixes can exhaust this budget. Faster correct conditioning
is performance work; deleting prefix constraints is not an optimization.

## Producer-specific current boundary regeneration

The code matrix is `RunBoundaryContracts` in RunPublicRehydration.cs. All imported
worlds must pass Agent's independent exact FullPolicyVisibleState and
RunInformationState/public action-key gates before RunSearch can use them.

| Stage / native owner | Regeneration / current support | Explicit limit |
| --- | --- | --- |
| map / RunState.Map | frozen public map, history and coordinates | only certified inventory/history families |
| ordinary room rewards / CombatRoom + RewardsSet | pre-finished combat room, EmptyForRoom, native ordinary reward constructors and public results/resolved indices | ExtraRewards, stolen/linked/special producer provenance reject |
| rest / RestSiteRoom + RestSiteSynchronizer | BeginRestSite/GetLocalOptions, no duplicate AfterRoomEntered effects | started continuations other than certified Smith reject |
| Smith outstanding card choice / SmithRestSiteOption | call native OnSelect again; suspends before mutation and retains genuine upgrade/AfterRestSiteSmith continuation | other CardSelectCmd producers reject |
| treasure / TreasureRoomRelicSynchronizer | native BeginRelicPicking consumes this accepted world's already-materialized rarity/relic; public opened/options freeze | suppression/modified producers require their own evidence; completed leave-only boundary is transport |
| shop / MerchantInventory | no portable regeneration | CreateForNormalMerchant price posterior, current purchases/refills and continuation not certified |
| event / EventRoom + EventModel | historical selection is certified; current page regeneration is not | current page variables, rendered dynamic values and selected-prefix continuation need model-specific evidence |
| custom reward / RewardsCmd.OfferCustom | no generic suspended-task restore | enclosing event/relic producer continuation not certified |
| generic choice other than Smith / CardSelectCmd | no generic mutable-state restore | source producer, selected prefix and continuation not certified |

Public current event hover-tip card/relic items are projected as existing public
choice-item faces; hidden event candidates or page-private fields are not exposed.
Public rest `selected` marks the actually clicked option and uses the existing
Agent chosen-option field.

## Persistent object and quest limits

Seven settled public counter families reconstruct through RunPublicCounters.cs:
HappyFlower, Nunchaku, IronClub, BookOfFiveRings, JossPaper, EmberTea and WingedBoots.
ArtOfWar/PaelsTears combat flags reset at native AfterCombatEnd. Other declared
saved/mutable fields reject with exact model/field names until their public
reconstruction or lifecycle is certified. Deck afflictions/local cost modifiers,
relic wax/melt and stack state reject separately; constructor defaults never
silently replace an uncertified persistent instance.

The map-marker denominator is exactly FUR_COAT and SPOILS_MAP. Ordered public
marker facts permit source-specific owner installation. Missing old FurCoat
coordinates, duplicate owners or unowned markers reject. Custom initial
SpoilsMap is specifically rejected because CreateForTest skips AfterCreated and
leaves SpoilsActIndex=-1, whereas real acquisition sets 1. Positive acquired-quest
native certification is still outstanding. LanternKey's historical selection
hook is replayed; ByrdonisEgg/Hatch's resulting Byrdpip has separately guarded
Skin state. This is not a blanket `unsupported_run_quest_domain` claim.

## Information and factual boundaries

Search-private RPCs are export_run_root, compose_run_root and export_run_combat_root.
The composition-v4 receipt has attempt count and sampled suffix commitments only
for private acceptance diagnostics. It certifies the six named generator domains
only for an accepted supported root, not every vanilla inventory/modal family.
No receipt, commitment, entropy or native root enters model input or teacher data.
Composed worlds cannot fork/restore a factual reset/history branch.

Nested combat exports to an isolated authority and uses existing FR1B2 belief-safe
composition from the then-visible combat boundary. Public act/history/odds migrate;
parent Run hidden combat future does not. The shared PUCT mathematics, model,
Combat belief/runtime and factual mechanics authority are unchanged.

Native public movement evidence retains the earlier pile-command contract:
visible-source advertised top/bottom insertions only; unknown/random changes
invalidate known positions. The existing DollRoom, Sandpit and SoulNexus headless
presentation seams remain presentation-only.

## Delivery verification and pause

Work pauses after the user's requested minimal verified delivery. The main native
regression run passed 44 gates (including FR1C/SR1B/FR0/PV1 and new mid-run gates).
The ordinary character-starting-loadout initial/mid-run root and strict protocol
negatives passed separately; the factual three-Act path remains invariant and
completed victory at 339 boundaries / 45 rooms / 27 encounters.

The Treasure current-screen/continuation gate passed on real seed-74 public
history: search entropy 95007 accepted after 7120 native candidates, pre-open and
opened public state/actions matched, and native obtain/leave/map completed. Only
one independent entropy witness is certified here. The preceding two-world target
failed: eight bounded requests yielded one accepted world and seven 8192-attempt
exhaustions. This failure is not converted into a diversity certificate. General
mid-run composition remains unclosed; the Agent report lists each producer and
instance gap rather than a compressed single blocker.
