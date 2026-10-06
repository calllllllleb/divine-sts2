# PV1 current-root Run composition kernel

Author: XuShuxi

Baseline: `65f2d726c105f808935d955d8d9e20539303945b`, branch
`pv1-general-midrun-composition-closure`. This delivery changes the composition
kernel, not general producer/instance coverage. STS2 v0.107.1, exact build
`v0.107.1_a1f9e653f1e2_42520eb8b091` remains the mechanics authority.

**CURRENT-ROOT RUN COMPOSITION KERNEL → CLOSED**, within the existing certified
adapter/root families. **GENERAL MID-RUN RUN COMPOSITION → STILL OPEN**.
Whole-prior production rejection is RETIRED / REFERENCE-ONLY.

## Owners and execution

PortableRunRoot **schema 4 is unchanged and public only**. There is one new
private owner: `RunMechanicalSnapshot` schema 1. Its fields are the public-root
binding digest, character base energy/orb capacity, and StartedWithNeow. It shares
all current inventory/map/history facts with the existing public owner instead
of duplicating them into a second state schema. It is a typed mechanical import
anchor, not SerializableRun, an opaque save, or a native object graph. It cannot
express seed/counters/queues/NativeState/handles. Unmapped fields fail closed.

```text
export_run_root + export_run_mechanical_root
  → isolated worker / validate public binding and mechanical profile
  → Construct(current public loadout, mechanical import)
  → InitializeShared / InitializeRunLobby (skip InitializeNewRun)
  → joint native latent generation + bounded evidence conditioning
  → search-local future RNG replacement / reference rebinding
  → existing public rehydration and producer regeneration
  → Agent FullPolicyVisibleState + RunInformationState/actions exact
  → RunWorld / unchanged macro runtime and nested Combat resolver
```

Current HP, inventory, topology and history are never chance targets. Native
mechanical owners/services are constructed once from the **current** loadout.
The local generator trials replace only RunRng/PlayerRng, Act room sets, bags,
visited-event selection state and source eligibility replay context. They do not
reset/reconstruct a Run, generate a map, run combat or reapply ascension/starter
hooks per trial. Historical event eligibility temporarily installs its certified
LanternKey context; final current inventory is restored by the existing public
owner, without importing any historical native object.

No composition owner was deleted. `ComposeValidatedRunWorld` and
`IndependentRunReset`'s initial-prior use are **reference-only**, exposed by the
explicit `compose_run_root_reference` RPC. Production `compose_run_root` requires
the mechanical anchor. Agent additionally requires v5, algorithm
`current_root_joint_kernel`, exactly one mechanical import and the six existing
domain certifications. There is no automatic reference fallback.

## Native joint law and bounded rejection

Candidate seeds use the same independent entropy/attempt derivation as the
reference oracle. Each candidate executes the shipped ordering:

1. shared bag Populate(unlocked shared pool, UpFront);
2. player bag Populate(shared + character pool, the **same** UpFront);
3. RunManager.GenerateRooms shared-ancient allocation;
4. each Act.GenerateRooms event shuffle, no-repeat-tag normal/elite grab bags,
   boss and ancient draws; applicable second boss draw.

All public encounter pulls, event selections, ordered relic operations and
currently public boss constraints condition this one native joint sample.
Already-public boss mechanics are not regenerated as a whole-world target;
the boss draw remains a constraint **inside the latent kernel**, because
overwriting an incompatible UpFront draw would change the joint law.

Formally: generate Z from the native joint prior using independent search-local
entropy; accept iff every certified evidence constraint H matches. Accepted
samples retain the reference kernel's conditional law P(Z | H). There are no
independently sampled encounter/event/relic marginals or resident suffixes.
The maximum remains 8192 **latent kernel** trials. Exhaustion returns
`run_composition_attempts_exhausted`, domain `joint_upfront_kernel`, attempt
count, first contradiction and per-evidence rejection counts. It is never death,
evidence deletion, a factual restore, or an ignored failed simulation.

This delivery removes repeated complete Run construction. It does **not** prove
constant-time conditioning: rare long public histories can still require many
kernel trials or exhaust the bound. Constraint sampling/caching and native
generation/reflection costs remain performance work; the public constraints
and exhaustion semantics must not be weakened.

## Replacement denominator

The import format is a strict five-field whitelist. No factual mutable native
graph is traversed or copied into the worker. The following covers the resulting
native graph; unsupported families are checked before it can step.

| Native owner / fields or domain | Classification | Treatment / source contract |
| --- | --- | --- |
| Player Character, Creature HP/maxHP/block, Gold, Deck, Relics, PotionSlots | PUBLIC_REBUILD | existing RunPlayerFacts and native face constructors; public counter/quest contracts retained |
| Player MaxEnergy/BaseOrbSlotCount | COPY_SAFE_CURRENT | snapshot scalars, certified character base values; other base mutations reject rather than copy unknown private state |
| Player NetId / player slot / IsActiveForHooks | PUBLIC_REBUILD | single player slot 0 / local net id 1; alive from positive public HP |
| Player UnlockState, RunState UnlockState, GameMode, Modifiers, Acts identity | UNSUPPORTED outside fixed profile | existing Construct profile: UnlockState.none, GameMode.None, no modifiers, default acts, one player; export explicitly checks it |
| Player CanRemovePotions | TRANSIENT_IGNORE when settled | true outside enclosing Event continuation; false explicitly rejects |
| RunState Map | PUBLIC_REBUILD | strict completed public topology → SavedActMap; no copied StandardActMap._rng or future room realization |
| RunState _visitedMapCoords, CurrentMapCoord, _mapPointHistory, CurrentActIndex, ActFloor | PUBLIC_REBUILD | current public context/coordinates; TotalFloor derives from ordered history |
| RunState NextRoomId / room identity | PRODUCER_REGENERATE | new native room stack; next travel resets native room id; no resident identity copied |
| Current public boss/second boss | PUBLIC_REBUILD | frozen public identities, constrained joint kernel; future-Act unseen bosses remain sampled |
| UnknownMapPointOdds scalars | PUBLIC_REBUILD | public odds installed after replacement, referencing fresh UnknownMapPoint RNG |
| RunRngSet._rngs: all 12 enum streams | FUTURE_RNG_REPLACE | new search-local sets; accepted search-local UpFront retained; enum census is checked fail closed |
| PlayerRngSet._rngs: Rewards/Shops/Transformations | FUTURE_RNG_REPLACE | InitializeSeed(search future seed), rebuilding PlayerOdds references |
| AbstractOdds._rng / RunOddsSet / PlayerOddsSet | PUBLIC_REBUILD | reconstruct both odds owners with replacement RNGs, then restore public scalar odds |
| MapSelectionSynchronizer._multiplayerMapPointSelection | FUTURE_RNG_REPLACE | search-local seed/name/counter-zero object; native selection continuation rebuilt |
| EventSynchronizer._multiplayerOptionSelectionRng | FUTURE_RNG_REPLACE | search-local seed/name/counter-zero object; no current event is imported |
| Future StandardActMap/SpoilsActMap generation RNG | FUTURE_RNG_REPLACE | future map constructors read search RunRng.Seed; existing completed topology is frozen |
| ActModel._rooms normal/elite sequences and cursors | JOINT_CONDITIONAL_RESAMPLE | fresh mutable default Acts → native generation → exact public prefix pulls/MarkRoomVisited |
| ActModel._rooms events/eventsVisited | JOINT_CONDITIONAL_RESAMPLE | native upfront shuffle plus ordered source eligibility/scan/hook/visited replay |
| ActModel._sharedAncientSubset / RoomSet ancient / boss / second boss / boss cursor | JOINT_CONDITIONAL_RESAMPLE | native GenerateRooms allocation and pulls, relevant public evidence exact |
| SharedRelicGrabBag / Player.RelicGrabBag _deques/_originalRelics/_refreshAllowed/_mpFallbackDequeue | JOINT_CONDITIONAL_RESAMPLE | fresh source constructors and Populate in native order; ordered native filter/refresh/fallback/removal replay; no loaded factual deques |
| VisitedEventIds | PUBLIC_REBUILD | source-native event selection replay derives the public visited set; no candidate queue imported |
| Public card/potion reward odds and shop removal count | PUBLIC_REBUILD | existing seven-field public odds contract; no private counters |
| RunState _currentRooms / stage | PRODUCER_REGENERATE | existing stage-specific native producer, not a serialized continuation |
| Ordinary CombatRoom / RewardsSet / public reward faces/resolved indices | PRODUCER_REGENERATE | existing prefinished native room/ordinary constructors; extra/linked/stolen/special owners reject |
| RestSiteRoom / RestSiteSynchronizer | PRODUCER_REGENERATE | existing BeginRestSite/GetLocalOptions; no duplicate room-entry effects |
| Smith outstanding selector/continuation | PRODUCER_REGENERATE | real native Smith path suspends before mutation; existing upgrade/AfterRestSiteSmith continuation |
| TreasureRoomRelicSynchronizer _sharedGrabBag/_rng/_currentRelics/_votes/skip state | PRODUCER_REGENERATE | new synchronizer after replacing bags/RNG; BeginRelicPicking consumes accepted-world materialization, never resident hidden chest contents |
| MerchantInventory / current EventModel / custom reward / non-Smith pending Task/TCS | UNSUPPORTED | existing producer-specific fail-closed reasons, no raw JSON/modal restore |
| CardModel/RelicModel/PotionModel/EnchantmentModel persistent SavedProperty or declared mutable state | UNSUPPORTED unless already certified | existing native family guards and public counter contracts; NativeState/private property bags are forbidden, even in the private snapshot |
| FurCoat/SpoilsMap quest owners | PUBLIC_REBUILD within existing contract | certified public marker history/constants only; missing journal/custom initial SpoilsMap/unowned markers retain precise guards; no new positive quest certification |
| RunState _allCards / card ids / inventory registry | PUBLIC_REBUILD | regenerated current and visible producer cards; floating hidden factual cards, handles and pointers never copied |
| Card/Relic FloorAddedToDeck metadata | TRANSIENT_IGNORE | source readers are save/history/presentation; no future gameplay reader in the pinned census |
| RunState ExtraFields.StartedWithNeow | COPY_SAFE_CURRENT | private scalar; fixed UnlockState.none profile proves false and importer enforces it |
| ExtraFields.TestSubjectKills/FreedRepy; Player discoveries/MaxAscensionWhenRunStarted and non-removal ExtraPlayerFields; BadgeModels | TRANSIENT_IGNORE | post-run progress/badges or presentation only; FreedRepy reader is NQueenRepyBgVfx; native current-run gameplay cannot read a copied aggregate |
| MultiplayerScalingModel | PUBLIC_REBUILD | native singleplayer CreateShared initialization; no multiplayer latent state imported |
| Active CombatState/PlayerCombatState/monster private state/local card cost/power/pile order | UNSUPPORTED at RUN root | future combat executes fresh native producer, then exports its then-public boundary to unchanged FR1B2/FR1C composition |
| Native callbacks/services/tasks/branch caches/handles | TRANSIENT_IGNORE | fresh worker/lifetime setup; no factual owner reference crosses RPC |

The fixed-profile limitation is intentional. The existing adapter's source passes
GameMode enum zero, **None**, into CreateForTest; this round does not silently
switch factual semantics to Standard. A Standard/Daily/Custom or unlock-profile
adapter needs its own public prior contract before importing such a root.

## Evidence and gates

Source evidence: sts2-rl-core revision
`13d6fbee40d176aa5018ca402345259e3e274a0f`, frozen generated
run_initialization_rules (20 contracts), encounter_event_rules (27 selection
contracts / 35 eligibility overrides) and reward_treasure_rules (85 executable
contracts), plus its encounter_event_runtime/relic_grab_bag owners. The targeted
exact-build source reads are RunState/CreateShared/CreateForTest, Player,
RunManager/InitializeShared/InitializeNewRun/GenerateRooms, ActModel, RoomSet,
RelicGrabBag, all RunRngType/PlayerRngType entries, odds and synchronizer owners.
This extends the prior focused census; it is not another 3425-file universe audit.

Gate A uses two real mid-run runs with identical public history. An explicit,
process-opt-in test seam reverses only hidden future suffixes/deques and advances
factual RNG counters. It is disabled in ordinary workers and unadvertised in
hello. Roots/public faces remain identical; same search entropy yields identical
five private commitments (including all Act room sets/bosses/ancients) and actual
future native consequences. The seam is an
adversarial hidden fixture, not a gameplay generator or production fallback.

Gate B: 20 actual mid-run entropy worlds preserve FullPolicyVisibleState/actions;
20 encounter commitments and 3 actual next-encounter identities witnessed.
Event and player-bag tests retain their multiple conditional suffix witnesses.
All factual observation/hash/actions/handle/PolicyVisibleState invariants pass.

Three small-root oracle comparisons have identical accepted trial number and all
five commitments (encounters/events/relics/future RNG/joint room sets). Final current vs
reference times in milliseconds: 101.69/109.96 (4 trials), 286.85/420.21 (17),
65.86/143.26 (6). These are correctness witnesses and local measurements, not a
strength benchmark or a bound on deeper-root cost.

Final test/build/commit results are recorded in the Agent implementation report.
The full producer matrix in PV1_RUN_COMPOSITION.md remains authoritative.
General mid-run coverage remains OPEN. In particular shop, current Event, custom
reward, non-Smith choice, remaining persistent/quest, nested/special and unaudited
relic producers were not expanded by this round. Shared Treasure two-entropy
diversity certification is also still outstanding.
