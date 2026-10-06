# Shared combat history v4

Pinned native DLL SHA256: `A1F9E653F1E28E4076558FEE1E60D218619CB7E057B887C6417F62C62C6D7A52`
(game v0.107.1). Native source/ABI facts below were reviewed against this DLL.

Portable combat roots require schema **5** and an explicit `combat_history` version **4**,
including empty history and source player combat/net IDs. Older roots/history fail closed.
Observation, model-input, pending-wrapper and Osty entity contract versions are unchanged.
Runtime capability is `decision-local-native-v10:pending-choice-regeneration:shared-combat-history-v4`.

The existing ordered 17 native entry types retain their Actor, round, side and player-turn
dictionary. A shared reference table preserves CardPlay/DamageResult aliases, exact native
type/model identity, nullable targets, resources, result piles and replay/auto-play values.
Finished.WasEthereal remains its historical stored value. Internal object IDs are never policy
features. The complete serialized payload participates in import and resident fingerprints.

## Detached dead enemies

The reference table now has mutually exclusive resident and history-only Creature, Monster
and Move forms. Supported dead Monster types are exactly native `TorchHeadAmalgam` and
`Nibbit`; their FSM allocation factories and concrete fields have been reviewed. This is a
bounded native carrier, not an arbitrary object serializer or general removed-monster claim.

Capture reads the actual objects after native death removal: Creature has HP 0, its original
CombatId/side/slot/block/max HP/HP display, null CombatState/Player/PetOwner and its retained
Power list. Its native Monster retains its reciprocal Creature, SavedNativeState, existing
MonsterRuntimeState, original NextMove, actual null FSM, nonexecuting flag, nullable own RNG
seed/counter and nullable shared run-RNG binding. Concrete readonly storage is checked too;
unclassified fields reject with their native type/member path. Live enemy snapshots also
retain MonsterMaxHpBeforeModification and HpDisplay so restoring and killing the earlier
alive branch yields the same corpse payload.

Move ownership comes from actual MonsterPerformedMoveEntry.Monster/Move associations, with
Monster.NextMove as another validated association. Capture does not depend on the dead
Monster's empty FSM. Each Move saves its original native behavior method identity, MoveId,
follow-up, performed/must-perform flags and ordered native intents. Reviewed constant attack
intent closures retain damage, repeat count, method identity and animation cache; the
reviewed Buff/Defend intents have no additional storage. Other intent/delegate storage fails
closed. No Move behavior or damage delegate is executed to reconstruct history.

Restore first allocates all native models, then directly constructs detached native Creatures,
hydrates scalar state/RNG and reciprocal references, and finally constructs ordered entries.
Original Move instances come from the reviewed native allocation-only FSM factory, binding
the real native methods on the reconstructed Monster. Its FSM property remains null and
NextMove aliases the table's native Move. Construction never calls SetUpForCombat, RollMove,
PerformMove, death, summon, entry, damage or Power application/removal hooks, and never
advances future RNG. Detached instances are not added to Enemies/Creatures, StateTracker,
live power collections or policy observations. One assignment replaces History._entries;
it does not append a successor tail or fire Changed.

Descriptor validation checks versions, mutually exclusive forms, exact types/models, native
Move/method/intent identities and all references before resetting the target. It uses metadata
and saved state, without generating an FSM or reading the target's previous encounter or
ascension. Resident binding validation runs after live entities and RNG are restored; detached
Move semantics are checked against the real factory after the correct base is restored.
Missing snapshot-created owners are not mistaken for invalid previous-battle bindings.

## Historical powers and existing carriers

Reviewed exact removed Power types are `DuplicationPower`, `MinionPower`, `StrengthPower`,
`WeakPower` and `VulnerablePower`. All five concrete classes have no instance storage beyond
the explicitly checked base ABI.
Their shared carrier preserves actual Owner/Applier/Target aliases, Amount, AmountOnTurnStart,
duration flag, actual variable-cache state, icon cache and SavedNativeState. Private internal
data or unreviewed concrete storage still fail closed. Native ToMutable(0) allocation followed by
direct hydration leaves these objects detached; retained corpse powers only populate the
corpse's private list. The same reviewed runtime fields and live Applier/Target IDs are saved
for resident instances, preserving later corpse capture after a prior alive-branch restore.

HistoricalPower and ResidentPowerRuntime use the same bounded DynamicVar descriptor,
validation, capture and restoration in PowerDynamicVarSnapshot.cs. Only the exact native
DynamicVarSet and constant base DynamicVar type are supported. The readonly `_vars`
dictionary is inspected and its ordered entries saved; unknown container/variable subclasses,
extra instance fields or delegate-bearing variables are rejected. Weak's reviewed variable
name is DamageDecrease; Vulnerable's is DamageIncrease; the other three reviewed powers
have empty variables. Actual uninitialized null and initialized empty sets remain distinct.

Each actual variable saves native type/name, own-Power or null owner alias, `_baseValue`,
`_enchantedValue`, `_previewValue` and WasJustUpgraded. The three decimal values use exact
invariant decimal strings, avoiding binary-float rounding in Python JSON transport. Native
constant-variable construction is followed by direct member hydration from the saved values;
canonical 0.75/1.5 defaults are not used as a substitute. No SetOwner virtual call, preview
hooks, delegate invocation or RNG replay is needed. Extra/foreign owner aliases fail closed.
Enemy/Osty fingerprints already include the complete PowerSnapshot lists; reviewed player
Power runtime is now explicitly compared on portable import and resident/fallback restore.

History-only consumed Potion and cleared Affliction carriers retain exact model/type,
Owner/Card references and reviewed flags/native saved state. They do not refill potion slots
or reattach afflictions. Distinct instances of one model stay distinct; aliases stay shared.
The live ChainsOfBindingPower carrier additionally preserves its exact private
`Data.boundCardPlayed` boolean, needed to recover prior legal BOUND actions after a successor
branch. Other private object graphs are not covered. Existing STUNNED/REVIVE transient Move
restoration retains its original native follow-up and performed flags.

Terminal combat still returns normally. Explicit export of a decided battle rejects with
`unsupported_combat_root`. Restoring a previously captured active branch after terminal
cleanup rebuilds its native base and directly applies its snapshot with zero action replay.

## Real native witnesses and remaining limits

The original registered-immediate regression remains unchanged in
`tests/test_fr1b2_generic_native.py` (the requested old filename is absent from this checkout).
Its Queen component survives `play:bludgeon-1:target:1`: Creature 1 Torch Head Amalgam
dies from 15/199 HP while Queen stays active at 400 HP, and both original native branch
assertions run. The complete same test now also passes its Waterfall Giant component through
the native RegisteredImmediate result ABOUT_TO_BLOW_MOVE and unchanged-root assertion.
The previously blocking `History.Entries[19].Power` WEAK_POWER expiry path retains its actual
DamageDecrease variable after native AfterSideTurnEnd -> PowerCmd.TickDownDuration removal.

The new Bash witness executes the real card for 8 damage and applies Vulnerable amount 2,
then imports cross-worker and executes Strike for 9 damage. Two actual enemy turns remove
Vulnerable; its history-only object retains amount 0, the same constant-variable payload,
Owner/Applier aliases and historical PowerReceivedEntry identity. A portable import after
expiry continues with a real Strike for 6 damage. History has **6 -> 28 -> 33** entries after
Bash, expiry and continuation. Restoring the pre-expiry branch has zero action replay and
exact Power runtime/history; replaying the same real actions yields identical expiry history.
One unknown variable-type descriptor on the same worker rejects before reset.

A controlled decimal branch changes the saved multiplier to
`1.234567890123456789012345678`, with distinct 1.25 enchanted and 1.375 preview values and
an upgrade flag. Its stale public hash is correctly rejected. Re-exporting the resulting actual
native state produces a valid native root; cross-worker import retains the exact strings and
real Strike deals 7 damage. This witnesses actual captured state winning over canonical
defaults and display previews; it does not claim a naturally occurring game modifier.

The additional bounded Queen witness imports the post-kill root into another worker whose
previous battle is at ascension 10, preserves the full shared history, then executes a real
end turn and a real card for **24 damage** to Queen. Only enemy CombatId 2 remains. History
has **153 -> 158 -> 176** entries before death, after death and after continuation. Restoring
the earlier active branch recovers its exact 153-entry history with zero replay; repeating
the kill reproduces the original 158-entry payload, including powers and native Move aliases.

The ordinary `NIBBITS_NORMAL` two-enemy witness kills Creature 1 with the second Rattle
(7 then **14 damage**), imports cross-worker, executes the third Rattle for **21 damage** to
Creature 2 and ends the turn. Osty count is 3 then resets to 0. Creature 1 never returns to
the live enemy set. History has **7 -> 13 -> 28** entries. Earlier alive-branch restoration
is exact with zero replay/no tail; repeating the kill yields the identical 13-entry payload.

Directly affected witnesses also cover Rattle cross-branch/next-turn behavior, Finisher,
native replay aliases, two consumed DUPLICATOR/power instances, cleared BOUND followed by
a real card, Move history after a different previous combat, terminal victory/loss restoration,
Osty entity/input contracts, and STUNNED/REVIVE. Prepared pending native acceptance passed.
GodotHost Debug builds have zero warnings/errors; Agent Ruff and both diffchecks pass.
Divine's unchanged Python Ruff baseline has 660 violations; this C# repair does not alter it.
The public-tree script's Protocol/Core Release builds also pass without warnings, but its
source scan rejects the existing machine-specific Steam defaults in Directory.Build.props and
TraceExporterSmoke.csproj. Those baseline configuration files are unchanged; no game binaries
or generated native-source review files are included in this change.

Removed cards, unsupported dead Monster types, other removed powers/orbs, and extra
Potion/Affliction/intent storage still fail at their concrete history member path. Earlier
Bygone Effigy evidence also exposed an independent resident SlowPower display_amount
mismatch (source 10, imported 0); it is not repaired or recertified here. These limits prevent
claiming a complete general training baseline. No long training, main merge or force push.
