# Card dynamic runtime v1

Portable combat root schema **7** requires an explicit `dynamic_runtime` on
every resident CardSnapshot. Schema 6 and missing/old/unknown carriers reject
before target reset. Shared combat history remains **v5** with the unchanged
`pinned-singleplayer-history-read-v1` fixed-DLL consumer contract.
The reviewed DLL SHA256 is
`A1F9E653F1E28E4076558FEE1E60D218619CB7E057B887C6417F62C62C6D7A52`.

## Numeric storage and native behavior

The carrier saves the actual lazy `_dynamicVars` state, distinguishing null
from an initialized empty set. Each initialized variable retains its name,
exact native type, BaseValue, EnchantedValue, PreviewValue, WasJustUpgraded,
and null/current-card owner relationship. Decimal values use invariant strings
and survive Python JSON without binary-float conversion. The native EnergyVar
ColorPrefix scratch string is also retained because description formatting
writes it. Observation preview now preserves preexisting numeric/scratch storage.

Variable names/types must match that card's ModelDb factory structure. Reviewed
native variable classes have exact declared-field ABI checks. Source capture
reads the existing set without initializing it; it obtains separate native
factory descriptors without executing formulas, preview hooks, commands, or
ToMutable. Captured common values never come from those descriptors.

Restore keeps the existing card instance, native ID, subscriptions, variables
and formula. A freshly imported card obtains variables through its own native
initialization. Direct field hydration preserves distinct preview/enchanted
values; the resetting BaseValue setter is not used. CalculatedVar formulas must
match the native factory's stateless delegate and have the actual card as owner.
No delegate is serialized or invoked to infer a stored value. Modified Props /
IsFromOsty configurations and unreviewed variable subtypes fail closed rather
than being silently reset or reflected as object graphs.

SovereignBlade additionally retains `_currentDamage`, `_currentRepeats`, and
`_createdThroughForge`. Its private decimal values must agree with the actual
Damage/Repeat BaseValues. The private values are restored before any subsequent
native downgrade, whose AfterDowngraded callback restores those values into a
new native variable set. No temporary Forge counter or display-only repair is
used. All new fields participate in the internal exact card fingerprint for
imports and resident branch shortcuts. The carrier is private simulator data
and is never added to PolicyVisibleState.

## Bounded native acceptance

`RUN_NATIVE_CARD_DYNAMIC_RUNTIME=1` enables four real-card tests. Each test
uses at most two native workers and no training:

- Bulwark invokes native Forge: SovereignBlade **10 -> 20 -> 30**. Cross-worker
  play deals **20**; an old branch restores with zero replay and deals **20**;
  the later branch restores and deals **30**. The source root remains frozen.
- Claw plays increase another Claw **3 -> 5 -> 7**. Cross-worker and old-branch
  real plays deal **5**, through the shared numeric codec. This witness excludes
  upgrades/downgrades and does not close Claw's separate private accumulator.
- BodySlam's original native formula reads its restored owner/current Block:
  real damage **5 -> 10 -> 5**, with zero-replay restoration.
- Old schema, missing/version/type/name/decimal/owner/consistency errors and
  unknown members reject without resetting the target state.

A separate local native fixture invokes the shipped ForgeCmd, SetRepeats and
DowngradeInternal APIs. Imported **20 x 3** survives native downgrade and causes
**three actual DamageReceived entries / 60 damage**. A later **25 x 4** mutation
restores to **20 x 3 / 60** with zero replay. Card/set identities, owner links,
native card IDs and history CardPlay aliases remain intact. Same public hash
and action history with changed private previews forces snapshot restoration.
Exact decimal `10.12345678901234567890123456`, null/initialized-empty sets and
Energy scratch roundtrip. Capture preserves RNG, HP/block, energy/stars and the
live set reference in the checked scenarios. This is a controlled mechanism
witness, not a normal run or a training-label producer; no normal gameplay
producer of SetRepeats was established.

The final focused run passes **47 tests**: the new card witnesses, the existing
history5/Osty continuation tests and native terminal restoration. A normal
80HP Ironclad starter seed42 executes one map selection and six actual combat
actions; its imported combat continues identically and its earlier branch
restores with zero replay. Run/plain combat JSON differ only in explicit-null
versus omitted-null fields in this witness. The private carrier is absent from
PolicyVisibleState. GodotHost Debug and Protocol/Core Release build with zero
warnings/errors. The unchanged two Steam path defaults still block the existing
public-tree source scan; no game binaries or decompiled native source are added.

## Remaining boundaries

This is a common numeric-storage codec, not a general CardModel private-state
clone or an all-card closure claim. In the fixed native source, Claw has
`_extraDamageFromClawPlays`: BuffFromClawPlay changes both Damage.BaseValue and
that field; AfterDowngraded adds the private field back. Rampage similarly has
`_extraDamageFromPlays`, changed by OnPlay and consumed by AfterDowngraded.
Those private fields are outside this change. Their no-downgrade numeric
witness must not be promoted to lifecycle closure. A controlled native
counterexample starts at Claw Damage **5** / private extra **2**: the imported
private extra remains **0**, and native downgrade followed by a real play deals
**3**. After a later source play increments the private extra to **4**, restoring
the old numeric payload and downgrading deals **7**, rather than the source's
**5**. This portion is explicitly left open; no private accumulator repair was
silently added. Other unreviewed card state
continues to require its own concrete mechanism and carrier review.

The preexisting resident upgrade rollback, enchantment replacement/clearing and
energy-modifier restoration are independent paths. Cached upgrades above the
saved level reject the snapshot path; this change does not rewrite upgrade,
enchantment or cost systems. General training/full-run closure remains open.
Existing bounded pending-choice and Run composition contracts are unchanged.
