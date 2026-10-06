# Shared combat history v1

Pinned native DLL SHA256: `A1F9E653F1E28E4076558FEE1E60D218619CB7E057B887C6417F62C62C6D7A52`.
Source facts were reviewed against this same DLL; older runtime acceptance revisions remain historical.

Portable combat roots now use schema **3**, from `ProtocolConstants.CombatRootSchemaVersion`.
An explicit `combat_history` version **1** payload is required, including empty history.
Schema 1/2 roots and missing history fail closed. Pending wrapper, observation and model-input
schema versions are unchanged. Runtime capability is
`decision-local-native-v7:pending-choice-regeneration:shared-combat-history-v1`.

`CombatHistorySnapshot.cs` captures the ordered 17 pinned entry types with an explicit field
whitelist. Each entry owns its saved Actor, round, side and player-turn dictionary. The local
reference table preserves CardPlay and DamageResult aliases, resident Card/Creature identity,
null targets, resources, result piles, auto-play and replay indices/counts. Finished.WasEthereal
is restored from its stored boolean even when the card has changed since the original play.
Internal reference numbers never become policy features or public hash inputs.

Capture rejects unknown entries and unsupported references. Import validates the descriptor
before reset, then checks bindings before live snapshot mutation. After existing card/entity
restoration, native record constructors create a temporary ordered list; one assignment replaces
History._entries without appending branch successors or firing Changed. No card, attack, summon,
reward or RNG replay reconstructs history. Import and resident fast-path fingerprints include
the payload in addition to the existing public hash, enemy and Osty checks.

Supported references bind to cards in the five existing combat piles, present player/enemy/Osty
creatures, current attached powers/afflictions, current potion slots/orb queue, and unique resident
FSM MoveState instances, plus historical STUNNED/REVIVE moves already expressible by the
existing exact transient-move codec (including original follow-up and performed flag).
Native Power/Orb/Affliction restoration reuses corresponding resident
instances where available. CardPlay and DamageResult are non-effect native record objects with
their complete pinned stored members.

This is bounded history support. Removed cards/creatures, dupes outside the existing card codec,
consumed potions, removed powers/orbs/afflictions, and historical moves outside the existing
FSM/transient codec return `unsupported_history_reference` with the real entry/member path. No simplified ghost
card/monster is created and no unsupported state silently gets an empty history. Initial or
post-action automatic snapshot capture can therefore reject those states too. Reconstructing a
historical-only object requires a separately verified complete codec and is outside this batch.

An exploratory Bygone Effigy root exposed an existing SlowPower `display_amount` mismatch
(10 on source, 0 after import), while the new history payload itself matched. This batch retains
the public-hash rejection and does not claim that power-runtime gap is repaired.

Native acceptance uses real subsequent Rattle damage after one/multiple attacks, portable import,
cross-branch restore without accumulated counts, turn reset, and Finisher's non-Osty history
consumer. It also checks shared CardPlay/DamageResult IDs, real Spiral replay, Apparition followed
by Apotheosis (historical WasEthereal remains true after the native upgrade removes Ethereal),
empty history, old/missing history, unknown type and dangling/wrong-kind references. The original
Osty attack-gap rejection is replaced by its real successful import witness. Focused regression
outcomes and bounded unsupported paths are recorded with this batch's delivery, without
relabeling earlier broad coverage evidence.

Existing focused tests are preserved. The registered-immediate regression reaches
`History.Entries[12].Affliction` for an already-cleared BOUND; the Osty model-input replay
regression reaches `History.Entries[3].Potion` for consumed DUPLICATOR. Both now reject
those uncodable historical references. Neither regression is deleted or relabeled as passing;
re-enabling those paths requires verified detached model codecs in a separate batch.

Delivery checks (2026-10-07): Debug GodotHost build passed with zero warnings/errors.
The final shared-history/Osty-entity/pending/input group reported **33 passed, 1 failed**
in 105.50s; its sole failure is the unchanged DUPLICATOR replay input test above.
The final generic monster group passed its first four tests (including native STUNNED
and REVIVE); its registered-immediate test is the unchanged BOUND failure above.
The enabled pending-memory test passes when run from the Agent repository.
Ruff `check --no-cache src tests` passes. These are bounded acceptance results, not
universal combat certification, and no long training was run.

The final stricter history-only suite adds missing-round and unknown-record-member
preflight rejection and reports **18 passed in 59.73s**. The model/checkpoint/Search
focused unit suite reports **54 passed in 4.02s** (using a fresh task-local pytest
base directory because the shared default temp directory was inaccessible).
