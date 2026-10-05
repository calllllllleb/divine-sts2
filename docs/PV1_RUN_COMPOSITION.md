# PV1 native Run composition contract

Author: XuShuxi

Implementation starts at `39bafd9e93c1e7b6922bcc4dd77ea4fcb8ad3449`.
The mechanics authority remains the exact shipped `sts2.dll` and PCK.

## Public observation

Every composed Run boundary carries the same minimal current Run/map surface and
`public_run_memory` schema 1. The latter exposes visited event identities, ordered
resolved rooms and observed encounters, revealed player/shared grab-bag pulls,
shop-removal count, native Unknown-room odds, card-rarity odds, and potion odds.
Pull interception publishes an identity only when its relic face is revealed.
Restoring an earlier replay prefix clears the revelation ledger before replay.
No native queue, RNG counter, save analytics, or mutable object is added to this
public memory. Existing diagnostic raw observation fields retain their original
contract; the Agent public projection must continue whitelisting them.

Deck-choice presentation positions are certified only for the audited shipped
deck screens that call `NCardGrid.SetCards(..., Ascending)`. This preserves visible
slots without changing the completion's set/sequence semantics. Public card faces,
including bundle/deck choices, contain only model, upgrades, resolved cost, X cost,
and enchantment identity/amount. Gold and special-card rewards retain N1 faces.

## Search-private RPC

`export_run_root`, `compose_run_root`, and `export_run_combat_root` are private
mechanics interfaces. Their payloads must never become model input or training
records. Portable roots pin schema, DLL, and PCK. The Agent independently verifies
the composed public root and public action identities before any simulation.

The certified Run composer accepts an initial, non-pending map root with no
history, quest continuation, or private reset overrides. It creates a new native
run from independent search entropy; all encounter/event/relic domains and RNG
are generated anew. It then freezes the already-public current map and boss
identities. A receipt lists the six closed domains and composer version
`pv1-run-composition-v1`. The imported map is checked for unsupported quests as
well, including special endpoints. There is no factual-seed replay fallback.

Nested combat export carries the current native combat substrate only to an
isolated authority. Its reset includes a whitelist of public Act/floor and room
history; full native history analytics and Run future queues are excluded.
Existing combat composition must independently resample draw order, monster
belief, and future RNG before each inner simulation. Enchanted known-draw
constraints distinguish otherwise identical public faces.

## Explicit correctness blocker

General non-initial Run roots are rejected with
`run_conditional_history_unidentifiable`. Native
`RoomSet.EnsureNextEventIsValid` silently skips ineligible, unvisited events, and
`RelicGrabBag.RemoveDisallowedRelicsFromDeques` silently removes relics. Current
public visited/depletion sets and odds do not retain historical eligibility
contexts required to identify their conditional suffix distribution. Suspended
modal/quest continuations also lack a certified portable reconstruction kernel.
Copying the resident factual queues or factual seed is prohibited.

This is a native composition blocker, not a claim that the complete PV1 end-state
is achieved. A full factual run and an initial-root search do not certify general
mid-run composition. Implementing a certified conditional kernel must preserve
the frozen architecture, and must reject domains whose conditioning information
cannot be reconstructed from audited public evidence.

## Headless presentation fixes

The shipped DollRoom ambience path receives an inert audio presentation object.
Sandpit's native test guards skip only Godot positions/music; its decrements and
removal/death commands remain native. SoulNexus's native death handler executes
its unsubscribe and receives a null presentation creature for its Spine update.
No damage, reward, room, death, or eligibility mechanics are replaced.
