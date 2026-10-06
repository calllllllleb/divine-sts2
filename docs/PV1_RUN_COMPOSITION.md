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

## Corrected production composition: current mechanical root + hidden-future sanitization

The October 6 architecture audit supersedes **whole-Run rejection from the
initial prior** as the production mid-run strategy.

The existing implementation remains valuable as a source-backed reference oracle:
it generates the exact native joint prior and can verify small-root conditional
distributions. It must not be optimized into the long-term production path by
raising attempt budgets or weakening public evidence.

The production path is now:

```text
current live Run boundary
    ↓
public PortableRunRoot
+
composer-private current mechanical substrate
    ↓
import into isolated native search authority
    ↓
sanitize factual hidden future
    ↓
reconstruct/condition native joint latent generator
    ↓
replace all future RNG
    ↓
producer-specific current-boundary regeneration
    ↓
public state/actions exact gate
    ↓
RunWorld
```

### Mechanical substrate is private, not model state

`PortableRunRoot` remains a strict public/public-derived DTO. It must continue to
reject seed, RNG counters, NativeState bags, native identities, hidden queues and
worker handles.

A separate composer-private substrate may carry the current mechanical anchor
needed to avoid replaying the entire factual run from floor 1. This is analogous
to Combat's private snapshot substrate.

It must never enter:

```text
PolicyVisibleState
DecisionTable / tensors
RunInformationState identity
training records
teacher provenance
model features
```

and it must not be stepped until the hidden-future sanitization denominator is
closed for that root family.

### Required field classification

Every future-relevant field reachable from the imported current substrate must be
classified as exactly one of:

```text
COPY_SAFE_CURRENT
PUBLIC_REBUILD
JOINT_CONDITIONAL_RESAMPLE
FUTURE_RNG_REPLACE
PRODUCER_REGENERATE
TRANSIENT_IGNORE
UNSUPPORTED
```

The current public resource/map/history rehydration already provides much of
`COPY_SAFE_CURRENT` and `PUBLIC_REBUILD`.

`UNSUPPORTED` must remain fail closed. A raw factual private value is never a
valid substitute for a missing reconstruction contract.

### Joint UpFront latent generator

Do **not** rewrite the latent future as independent encounter/event/relic marginals.

For this exact build, source evidence shows an ordered `RunRng.UpFront` generation
graph spanning at least:

```text
shared relic-bag population
player relic-bag population
RunManager.GenerateRooms / ActModel.GenerateRooms
encounter/event/ancient/boss upfront materialization
```

The composer must preserve the native joint law.

Conceptually:

```text
search-local UpFront
→ native ordered latent generation
→ condition on public encounter/event/relic evidence
→ retain only a consistent hidden suffix
```

Bounded rejection/constraint sampling is allowed **inside this joint latent
generator kernel** where exact conditioning requires it.

What is retired from production is:

```text
fresh initial Run
→ regenerate all current mechanics
→ require the complete historical prefix to reoccur
→ whole-world rejection up to 8192 attempts
```

Already-public boss identity, map topology, current resources and completed room
history are not probabilistic targets and must not be re-discovered by chance.

### Future RNG replacement

The imported current substrate cannot keep factual RNG continuation.

Replace all future-relevant named streams with search-local continuation,
including the applicable streams in:

```text
RunRngSet
PlayerRngSet
```

Examples include UnknownMapPoint, TreasureRoomRelics, reward/shop/transform RNG
and later combat RNG.

Nested combat continues to use FR1B2/FR1C from the then-public combat boundary.

### Safe map treatment

The completed public map topology is a safe current mechanical fact and should be
retained.

An unresolved Unknown map point does not contain a public future room result. Its
future resolution must consume the search world's replacement
`RunRng.UnknownMapPoint` and current public odds/history.

### Current producer treatment

Current screen/continuation owners remain producer-specific. A copied resident
producer object is not automatically safe because it may already contain an
unrevealed factual result.

The existing certified ordinary reward, Rest/Smith and ordinary Treasure
regeneration contracts remain valid and should be preserved.

Shop, current Event, custom reward and generic choice remain fail closed until
their own producer-specific public reconstruction contracts are certified.

A pre-open Treasure is the canonical negative example: the public chest may be
copied/rebuilt, but any resident unrevealed relic materialization must be replaced
by the sampled world's result before the producer is regenerated.

### Mandatory hidden-future isolation gates

New production composition is not CLOSED without both:

```text
Gate A:
same public root
+ same search entropy
+ deliberately different factual hidden futures
→ same composed latent commitment / same search-world semantics

Gate B:
same public root
+ different search entropy
→ exact same public root/actions
→ hidden commitments show diversity
```

Gate A detects any factual hidden field that survived sanitization. Gate B proves
that the composer is not cloning a single future.

The existing public equality and factual invariance gates remain mandatory.

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
opened public state/actions matched, and native obtain/leave/map completed. This
remains valid evidence for the producer regeneration contract.

The preceding two-world target failed: eight bounded requests yielded one accepted
world and seven 8192-attempt exhaustions. After the architecture audit this failure
is treated as evidence that whole-prior rejection is the wrong production
mid-run substrate, not merely as a request to increase the attempt budget.

General mid-run composition remains unclosed. The next implementation phase is
the current-root import/sanitization kernel before returning to the remaining
producer/instance closure groups.
