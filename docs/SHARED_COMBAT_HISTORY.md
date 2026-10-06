# Shared combat history v2

Pinned native DLL SHA256: `A1F9E653F1E28E4076558FEE1E60D218619CB7E057B887C6417F62C62C6D7A52`.
Source facts were reviewed against this same DLL; earlier acceptance revisions remain historical.

Portable combat roots use schema **3**, from `ProtocolConstants.CombatRootSchemaVersion`.
An explicit `combat_history` version **2** payload is required, including empty history and
the source player combat/net IDs. Schema 1/2 roots, missing history and history contract 1
fail closed. Pending wrapper, observation and model-input schema versions are unchanged.
Runtime capability is `decision-local-native-v8:pending-choice-regeneration:shared-combat-history-v2`.

`CombatHistorySnapshot.cs` captures the ordered 17 pinned entry types with an explicit field
whitelist. Each entry owns its saved Actor, round, side and player-turn dictionary. The local
reference table preserves CardPlay and DamageResult aliases, resident Card/Creature identity,
null targets, resources, result piles, auto-play and replay indices/counts. Finished.WasEthereal
is restored from its stored boolean even when the card has changed since the original play.
Internal reference numbers never become policy features or public hash inputs.

Capture rejects unknown entries and unsupported references. Import validates descriptors
without reading the destination worker's previous battle. Native Move bindings are checked
against the correct restoration base after reset, before snapshot mutation; transient Power
dependencies use the snapshot's saved power models/properties. After card/entity restoration,
native record constructors create a temporary ordered list; one assignment replaces
History._entries without appending successors or firing Changed. No card, attack, summon,
reward or RNG replay reconstructs history. Import and resident fast-path fingerprints include
the payload in addition to the existing public hash, enemy and Osty checks.

Resident references bind to cards in the five existing combat piles, present player/enemy/Osty
creatures, attached powers/afflictions, potion slots/orb queue, and unique native FSM MoveState
instances. Historical STUNNED/REVIVE moves reuse the existing exact transient-move codec,
including original follow-up and performed flags. Native Power/Orb/Affliction restoration
reuses corresponding resident instances. CardPlay and DamageResult are non-effect native
record objects with their complete pinned stored members.

History-only Potion and Affliction payloads are separate from resident bindings and retain
actual native type/model, nullable Owner/Card references, amount/native flags and existing
SavedNativeState. Extra mutable concrete storage and nonempty potion variables fail closed.
Consumed DUPLICATOR and cleared BOUND are verified real paths. Hydration uses native mutable
models, then direct native members; it never refills potion slots or reattaches afflictions.
Objects with the same model ID retain separate table entries and sharing follows actual aliases.

The only supported removed Power is the exact pinned `DuplicationPower`/`DUPLICATION_POWER`.
Its concrete class has no extra instance fields. The payload saves its actual Owner/Applier/
Target references, Amount, AmountOnTurnStart, duration flag, empty variable-cache state, icon
cache and SavedNativeState. Private internal data/nonempty variables fail closed. Restore
uses native canonical ToMutable(0), hydrates members and leaves the object outside live Powers;
it never invokes ApplyInternal, SetAmount, removal or combat hooks. This is not general removed
Power support.

Terminal combat results do not require a future executable combat snapshot. Explicit portable
export of a decided battle returns `unsupported_combat_root`. Restoring a previously captured
active branch after terminal cleanup recreates its original native base without clearing
branch handles, then applies its snapshot directly, with zero player-action replay.

Removed cards/creatures, dupes outside the card codec, other removed powers/orbs and historical
moves outside the existing FSM/transient codec remain `unsupported_history_reference`, with
the actual entry/member path. Extra Potion/Affliction native state outside the bounded carrier
also fails closed. No simplified ghost or optional empty-history fallback is used. Active
post-action automatic capture can reject those states. The unchanged Queen registered-immediate
regression currently reaches `History.Entries[0].Actor` after `play:bludgeon-1:target:1`:
Torch Head Amalgam (Creature 1) has been removed, while Queen (Creature 2, HP 400), player and
native combat remain active. This remaining Creature boundary is not a terminal-capture issue.

An exploratory Bygone Effigy root exposed the existing SlowPower `display_amount` mismatch
(10 on source, 0 after import), while shared history matched. Public-hash rejection remains;
that independent power-runtime gap is not repaired.

Native witnesses cover subsequent Rattle damage, one/multiple attacks, portable import,
cross-branch restore without accumulated counts, turn reset, Finisher, real Spiral replay,
and Apparition followed by Apotheosis. They also cover two consumed DUPLICATOR instances and
two removed DuplicationPower instances followed by actual Rattle damage; cleared BOUND import
followed by a real card; Move-history import after a different previous encounter; victory/
loss with explicit export rejection and restoration of earlier active branches. Fail-closed
boundary checks share one worker rather than adding one startup/test per record field.
The original Osty model-input DUPLICATOR regression is unchanged and passes. The original
Queen regression remains intact and exposes the active removed-Creature boundary above.
No long training, merge or force-push is part of this batch.

Focused verification (2026-10-07, before committing this repair): GodotHost Debug build has
zero warnings/errors. History/Osty/input/STUNNED/REVIVE group reports **34 passed, 1 deselected**
in 118.47s; the deselected unchanged Queen test was run separately and fails at the exact
active removed-Creature path above. Unchanged DUPLICATOR input passes. Enabled Prepared
pending acceptance reports **1 passed** in 7.69s. Harness/checkpoint/pin focused checks report
**20 passed** in 2.90s. Ruff and both repository diffchecks pass. This is a review candidate;
the unchanged Queen failure remains open pending active historical-Creature support.
