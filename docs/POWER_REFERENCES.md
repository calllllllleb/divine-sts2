# Common live Power references v1

Combat-root schema 7 requires every resident PowerSnapshot to contain
`references: {applier_combat_id: <uint or null>, target_combat_id: <uint or null>}`.
Both members are mandatory. Owner is the containing Creature and is checked on
capture and binding. IDs describe Creatures resident in this snapshot: the player,
enemies and supported Osty. They never identify a history-only object or a model.
Nonresident native references reject with the Creature/Power/member path.

Descriptor validation checks every reference before target reset or resource/pile
mutation. After restoring all live entities, the binder assigns the exact native
Creature instances by CombatId and asserts reference equality. It writes explicit
null values too, clearing aliases retained by reused powers from another branch.
The player fingerprint now covers every PowerSnapshot; enemy and Osty fingerprints
already include their full lists. These private references do not enter observations,
state hashes, public vocabulary or policy tensors.

Common references are independent of the five reviewed runtime/DynamicVar carriers.
Those carriers keep AmountOnTurnStart, SkipNextDurationTick, DynamicVars and icon
state, while duplicate Applier/Target members move to the common payload. History
remains v5. Old schema 6 roots reject. This change does not certify other derived
private state, history-only live sources, all cards or all characters.

The normal Ironclad A0 seed42 original 39-action replay, without Search, captures
Shrink with Applier CombatId 1 and null Target. Cross-worker import preserves the
complete PowerSnapshot and passes native instance-equality assertions. The final
real Bash kills the 3HP Shrinker; native AfterDeath removes Shrink in both the Run
parent and detached combat. Public combat agrees: HP51/max80/block5, no player
powers. Run parent reason `none` and ordinary detached reason `combat_victory`
retain their existing scopes. The detached worker restores the prior live branch
with `snapshot_restore`, zero replay, and repeats the same kill/removal/state.

Nine malformed-root variants reject without changing the existing native state:
old schema, absent/null references payload, absent Applier/Target members, dangling
Applier/Target, non-Creature/string reference and an extra Owner alias. Relevant
native regressions also cover reviewed Bash/Vulnerable variables, shared Rattle
history and Osty. No manual RemovePower, hook replay, sync removal or combat-end
power clearing is used to repair hydration.
