# Shared combat history v5

Native DLL SHA256: `A1F9E653F1E28E4076558FEE1E60D218619CB7E057B887C6417F62C62C6D7A52`
(game v0.107.1). This contract applies to the fixed-DLL single-player native
headless profile without loaded external mods. It is a history consumption
contract, not arbitrary native private-object cloning.

Portable combat schema **8** requires explicit history **5**, including empty
history, original player IDs and
`consumer_contract = pinned-singleplayer-history-read-v1:<DLL SHA256>`.
Older schemas, versions and consumption contracts reject before target reset.
The implementation additionally checks the actual DLL hash. Observation/model
input, pending wrapper and Osty entity schemas are unchanged. Agent capability
is `decision-local-native-v13:pending-choice-regeneration:shared-combat-history-v5:resident-power-references-v1:card-dynamic-runtime-v1:retired-combat-cards-v1`.
Schema 8 composes common Power references, [card dynamic runtime v1](CARD_DYNAMIC_RUNTIME.md) and retired combat cards; the history
consumer contract remains unchanged.

## Two different restoration responsibilities

Live Creature/Monster/FSM/Power restoration keeps its existing executable
snapshot, future RNG, native fields and transient STUNNED/REVIVE behavior.
Resident references bind to the restored actual instances. Reviewed live
Weak/Vulnerable constant DynamicVars retain exact decimal base/enchanted/preview
values, owners and upgrade flags; their history-only counterparts retain the
same codec. Resident ChainsOfBindingPower still restores its real private
`boundCardPlayed` flag. This change does not relax those live contracts.

CardModel also stays executable. HistoryCourse selects an actual historical
card and calls CreateDupe/CreateClone/AutoPlay. Cards therefore retain the full
existing resident card codec; removed, foreign-owned or unsupported dupe cards
still reject. CardPlay and DamageResult shared instances retain all existing
fields, resource values, nullable targets, result piles and replay flags.

Only detached history-only Monster/Move/Power/Orb/Potion/Affliction references
use native type/ID value carriers. They preserve shared reference identity,
the associations below and fields needed by the fixed consumer contract.
They do not promise executable future AI, private Data, derived variables,
behavior closures, intents or arbitrary reflected state.

## Fixed-DLL consumption evidence

Member-token traversal scanned **48,970 method bodies**, **1,395,618 IL
instructions**, and **9,410 types including nested types**. It covered getter
calls and other method references, direct field reads/addresses/writes. The
history property fields are read only by their own getters and written by their
constructors. This establishes these static consumers in this DLL only; it
does not cover reflection/dynamic dispatch discovery, other assemblies or mods.

| Historical reference | Fixed-DLL consumption requiring preservation |
| --- | --- |
| MonsterPerformedMove.Monster/Move/Targets | Entry Description reads Monster.Id, Move.Id and target display identity |
| PowerReceived.Power | Description reads Id; DeathsDoor uses `is DoomPower`; Entry Amount/Applier/Actor remain exact |
| OrbChanneled.Orb | Description reads Id; Voltaic uses `is LightningOrb` and the original Actor.Player |
| PotionUsed.Potion | Description reads Id and original Actor/Target display identity |
| CardAfflicted.Affliction | ChainsOfBindingPower uses `is Bound` and original Actor identity |
| Actor / CardPlay.Target / DamageReceived Receiver/Dealer | Identity predicates and display ID; GangUp also reads Dealer.Side |
| CardPlay.Card | Owner/type/tags/identity and native clone/autoplay behavior; never reduced to a read-only carrier |

The Creature review includes 29 CombatHistoryEntry.Actor getter sites, six
DamageReceivedEntry.Dealer sites, and two PotionUsedEntry.Target sites.
CardPlay.Target has 487 getter sites, mostly active card execution; they are
not all accesses to a historical CardPlay. DamageResult.Receiver's 16 sites
also include active damage hooks. Existing static history-query source review
and the corresponding IL call windows distinguish those execution paths from
the historical identity/display/Side consumers. No new historical execution
consumer requiring a detached Monster FSM was found.

## Ordered native entry contract

All 17 entry types retain original order, Actor, round, side and the player's
turn dictionary. Exact known entry/base backing-field coverage is checked
against the fixed ABI. Unknown entries, extra descriptor members, foreign native
types, dangling or wrong-kind references and nonreciprocal links reject.

| Entry type | Exact entry-specific fields |
| --- | --- |
| BlockGained | Amount, Props, CardPlay |
| CardAfflicted | Card, Affliction |
| CardDiscarded / CardExhausted | Card |
| CardDrawn | Card, FromHandDraw |
| CardGenerated | Card, Creator |
| CardPlayStarted | CardPlay |
| CardPlayFinished | CardPlay, historical WasEthereal |
| CreatureAttacked | ordered DamageResults |
| DamageReceived | Result, Dealer, CardSource |
| EnergySpent / StarsModified / Summoned | Amount |
| MonsterPerformedMove | Monster, Move, ordered nullable Targets |
| OrbChanneled | Orb |
| PotionUsed | Potion, nullable Target |
| PowerReceived | Power, Amount, nullable Applier |

## Detached carriers and isolation

Actual detached dead enemies retain CombatId, Side, zero HP, max HP, block,
slot, MonsterMaxHpBeforeModification, HpDisplay, reciprocal native Monster and
the retained corpse Power aliases. The currently accepted detached Enemy
profile has actual null Player/PetOwner/CombatState. Other removed Creature
profiles still reject explicitly. Monster keeps its type/ID and Creature link;
its restored FSM, NextMove and RNG are absent. No dead-monster whitelist or
per-monster historical FSM/state dictionary remains.

A history-only Move keeps actual native MoveState type, ID and original Monster
association. Distinct instances with the same ID remain distinct; shared
instances remain shared. A live FSM member still binds to its native live Move.
Historical Moves absent from live FSMs, including old transient Moves, receive
an explicit delegate throwing `unsupported_history_execution`. No FSM factory,
intent getter, behavior delegate, follow-up transition or RNG is run to
reconstruct them. These carriers are never assigned to a live FSM or NextMove.

Historical Power preserves Owner/Applier/Target, native type/ID, amounts and
basic duration/icon values. The five previously reviewed Power families retain
their exact bounded DynamicVar codec; other history-only powers intentionally
do not inspect unused variables or private Data. Potion retains actual nullable
Owner and queue/removal flags; Affliction retains actual Card and amount; Orb
retains actual nullable Owner. Model type/ID must match the fixed native ModelDb
family (`Monsters`, `AllPowers`, `Orbs`, `AllPotions`, `DebugAfflictions`).
Models absent from those catalogs and external types remain unsupported.

Restoration allocates each native model identity once without constructors,
ToMutable/DeepCloneFields/AfterCloned, spawn/death/application hooks or HP getters.
It hydrates explicitly listed fields, then reciprocal links and known entries.
Creature event fields stay null: no StateTracker subscription is installed.
Carriers cannot refill live potion slots, powers, orb queues or afflictions.

Bounded before/after guards protect run/private-monster RNG counters, live
Creature/Enemy membership, HP/block, Power amounts, energy/stars, potion/orb and
card-pile membership, and the existing entry sequence. Before publication,
reference checks reject carrier leakage into live Creature/Monster/Move/Power/
Orb/Potion/Affliction membership. One assignment replaces History._entries.
No Changed event or successor-tail append occurs. These checks and the native
before/after witnesses do not claim exhaustive private-field monitoring.

## Current real native acceptance

Normal seed 42 used actual 80/80 HP, 5 Strike, 4 Defend, 1 Bash and Burning Blood,
the frozen untrained shared model, 4 simulations/depth 2/node budget 64, one
lane, 8 macro decisions and 64 actions per combat. It recorded **47 successful
live-parent steps**, **35 combat teachers**, eight POLICY_SAMPLE and four
FORCED_TRANSPORT actions, with zero native failures. Both SHRINKER_BEETLE_WEAK
and SLIMES_WEAK won: HP **80 -> 70 -> 54**. The second fight contains
TwigSlimeS/TwigSlimeM/LeafSlimeS. The episode ends at the macro budget with
null full-run outcome; it is neither a full-run victory nor an infrastructure
interruption. The local acceptance driver reads the separate trajectory
collector worktree without modifying it and uses this original Divine host.

A further normal-path witness executes the recorded legal choices to native
decision 27, where actual Strike kills **4/10 HP TwigSlimeS**. Only enemies 2/3
remain. Cross-worker imports retain history **10 -> 15 -> 29**, then execute an
actual end turn and Strike for **6 damage** to enemy 2. The earlier active branch
restores with **zero replay**; repeating the kill gives the identical corpse
history. The dead Twig never returns to the later live enemy set.

Real Blight Strike deals **8**, applies Doom, then Strike deals **6** and kills
its enemy. No Doom remains in the live enemies. Cross-worker DeathsDoor sees
the native historical DoomPower type and gains **18 block**, versus **6** on
the zero-history restored branch; a subsequent real Strike deals **6**.
This is the selected type-predicate witness; no all-types execution claim.

Queen retains the **24 damage** continuation and history **153 -> 158 -> 176**.
Two Nibbits retain **14** kill / **21** continued Rattle damage, Osty 3 -> 0
and history **7 -> 13 -> 28**. Bash/Vulnerable retains **8/9/6** damage and
history **6 -> 28 -> 33**, including exact decimal/preview-state restoration.
All earlier live branches restore without replay or accumulated history tails.
The unchanged Queen/Waterfall regression and native STUNNED/REVIVE gates pass.

The 33 directly affected native tests pass. GodotHost Debug and Protocol/Core
Release builds have zero warnings/errors. Divine Python tests: 10 pass, three
opt-in tests skip. The public-tree source scan remains blocked by the unchanged
machine-specific Steam defaults in Directory.Build.props and the existing
TraceExporterSmoke project; this change contains no game binaries or native
decompiled source. A separate allocation-only native MoveState witness confirms
that PerformMove throws `unsupported_history_execution`.

General training/full-run closure remains open: unsupported removed/dupe cards,
other detached Creature profiles, models outside the native catalogs, unreviewed
live private-state paths and the previously observed resident SlowPower display
mismatch are not certified here. Pending choice and Run composition retain
their existing bounded scopes. No long training, main merge or force push.
