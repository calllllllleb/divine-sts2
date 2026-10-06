# PV1 mid-run source evidence and certification limits

Author: XuShuxi

The current-root kernel's additional targeted native field/RNG census and
replacement denominator are recorded in
[PV1_CURRENT_ROOT_KERNEL.md](PV1_CURRENT_ROOT_KERNEL.md). This does not expand
the persistent/quest or producer families certified below.

This is a targeted audit for STS2 v0.107.1, build
`v0.107.1_a1f9e653f1e2_42520eb8b091`. It reuses the exact-build extraction in
`calllllllleb/sts2-rl-core@13d6fbee40d176aa5018ca402345259e3e274a0f`; it does
not repeat the 3425-file source-universe audit. The shipped DLL remains the runtime
mechanics authority. The simulator is evidence, never a replacement backend.

## 2026-10-06 architecture conclusion from this source audit

This audit now has an explicit architecture consequence.

The source evidence supports **current-root mechanical import followed by complete
hidden-future sanitization** as the production Run composition direction. It does
not support continuing to rebuild the entire Run from its initial prior and
rejecting whole worlds until the already-observed historical prefix reappears.

The replacement matrix is:

| Native domain | Current-root treatment |
| --- | --- |
| HP/max HP/gold/current certified deck/relic/potion state | COPY_SAFE_CURRENT / PUBLIC_REBUILD |
| completed public map topology/current boss/visited coords/room history | COPY_SAFE_CURRENT / PUBLIC_REBUILD |
| public unknown-room/card-rarity/potion odds and shop removal count | PUBLIC_REBUILD |
| unresolved Unknown room future | FUTURE_RNG_REPLACE using search-local UnknownMapPoint |
| encounter/event/ancient/boss hidden suffix | JOINT_CONDITIONAL_RESAMPLE |
| shared/player relic-bag hidden suffix | JOINT_CONDITIONAL_RESAMPLE |
| factual RunRngSet / PlayerRngSet continuation | FUTURE_RNG_REPLACE |
| ordinary reward / Rest / Smith / ordinary Treasure current producer | PRODUCER_REGENERATE |
| Shop / current Event / custom reward / generic choice | UNSUPPORTED until producer-specific closure |
| unaudited persistent Card/Relic/Potion state | PUBLIC_REBUILD / TRANSIENT_IGNORE only when source-proven; otherwise UNSUPPORTED |
| FUR_COAT / SPOILS_MAP quest state | PUBLIC_REBUILD from certified marker/history; otherwise UNSUPPORTED |
| combat-local state | existing Combat composition owner |

A critical source constraint is that `RunRng.UpFront` is shared by an ordered
native generation graph that includes relic-bag population and RunManager/Act room
generation. Therefore encounter, event and relic latent suffixes must not be
replaced by independent marginal samplers unless a future source proof establishes
the needed conditional independence.

The old joint whole-prior rejection implementation remains useful as a bounded
reference oracle for small-root distribution checks because it naturally preserves
this correlation. It is not the production mid-run strategy.

The production close condition is stronger than public equality:

```text
same public root
+ same search entropy
+ different factual hidden future
→ same composed latent commitment
```

This gate must pass before a current-root importer is considered belief-safe.

## Rules used

The three generated contracts are `sts2_env/data/generated/encounter_event_rules.json`,
`reward_treasure_rules.json`, and `run_initialization_rules.json`. Their native
ordering and lifecycle interpretation was checked against
`sts2_env/simulator/core/encounter_event_runtime.py`, `relic_grab_bag.py`, and the
`tools/game_data/global_rules_encounter_event_*` / `global_rules_reward_treasure_*`
source extractors at that revision. Native source checks are confined to the owners
listed below and persistent card/relic/potion/enchantment instances.

| Native owner | Source-proven rule | Consequence |
| --- | --- | --- |
| RunManager.GenerateRooms | shared Ancient subset allocation precedes all Act generators; all use UpFront; tutorial and DoubleBoss adjustments are native | Invoke the complete native prior; never sample independent marginal queues |
| ActModel.GenerateRooms | shuffled events; weighted no-repeat encounter bags; boss/ancient selection | Keep the accepted joint world and its remaining suffix |
| RoomSet | category cursor modulo sequence count; MarkVisited consumes normal/elite/event/boss separately | Replay public encounter pulls through PullNextEncounter + MarkRoomVisited |
| RoomSet.EnsureNextEventIsValid | cyclic scan; IsAllowed and not visited; exhaustion permits repetition | Visited ids alone cannot condition a historical Event selection |
| ActModel.PullNextEvent | eligibility scan, ModifyNextEvent hook fold, then final visited id | Need ordered selection-time public eligibility and hook evidence |
| LanternKey.ModifyNextEvent | Act 2 redirects to WAR_HISTORIAN_REPY | Public card identity is required at the historical selection, not merely now |
| LuminousChoir.IsAllowed | invokes RelicGrabBag.HasAvailableRelics | Evaluating all event eligibility on factual state can mutate bags; do not do this during root capture |
| RelicGrabBag | independent rarity shuffles; GetAvailableDeque filters every deque, refreshes, follows fallback chain, then front/back pull | Unordered `(bag, model)` depletion is insufficient posterior evidence |
| RelicGrabBag.HasAvailableRelics | invokes the same mutating filter/refresh kernel | Historical queries and removals must be ordered with public pulls |
| CardReward | ordinary constructor installs reward flags and RelicObtained subscription; manual constructor differs | Rebuild ordinary rewards with the ordinary constructor and public card creation results |
| RestSiteRoom | BeginRestSite regenerates options; AfterRoomEntered applies effects | Regenerate current options without applying factual entry effects twice |
| StandardActMap/MapPathPruning | CanBeModified is used during generation-time repair | Omit this private flag from the frozen completed public topology |

## Persistent instance state

The scoped source inventory contains 578 card, 298 relic, 64 potion and 23
enchantment declaration files. Five card and forty relic declarations contain
SavedProperty; no potion or enchantment declaration does. RelicModel additionally
owns conditional saved IsWax/IsMelted and stack state. **This inventory does not
prove that all unsaved fields are transient.** PaelsEye has an unsaved last-turn
participation flag; ArtOfWar and PaelsTears instead reset their combat flags at
AfterCombatEnd. A global absence-of-latent-state claim is not made.

The composer reads property/field metadata only, never factual private values.
Seven counter families and the two map-quest owners have source-specific public
reconstruction contracts. Other declared saved-property owners are rejected with
model and exact field names. Declared mutable fields without an audited lifecycle
are also rejected.
Wax, melted and stackable relic instances reject separately. Thus uncertified
instance state cannot silently acquire constructor defaults in an accepted root.
This conservative scope includes some reconstructible public state and some
irrelevant state; it is not evidence that those families are intrinsically latent.

| Source example | Classification from source | Current certified treatment |
| --- | --- | --- |
| HP/max HP/gold/deck model/upgrades/enchantment/potion slots | PUBLIC | typed current public rehydration |
| unknown-room/card-rarity/potion odds; shop removals; floors | PUBLIC_DERIVED | existing public memory/context restore |
| GeneticAlgorithm.CurrentBlock / IncreasedBlock | PUBLIC + DETERMINISTIC_FROM_PUBLIC_HISTORY (`CurrentBlock = 1 + IncreasedBlock`) | reject: the certified face lacks the displayed block fact |
| HappyFlower.TurnsSeen | PUBLIC counter at a settled boundary | reconstruct displayed residue 0..2 |
| Nunchaku / IronClub / BookOfFiveRings / JossPaper | PUBLIC counter residue; JossPaper.EtherealCount resets at combat end; activation flags are presentation only | reconstruct residues modulo 10 / 4 / 5 / 5; reject animation-peak counter |
| EmberTea.CombatsLeft | PUBLIC displayed remaining combats | reconstruct 0..5 |
| WingedBoots.TimesUsed | PUBLIC_DERIVED from displayed remaining uses | reconstruct 3 minus remaining; absent exhausted counter maps to 3, equivalent for all source gameplay tests `TimesUsed < 3`; no private count copied |
| ArchaicTooth starter/transcended faces | PUBLIC hover tips + DETERMINISTIC_FROM_PUBLIC_HISTORY; source preserves upgrades/enchantment | reject: current root does not certify those historical faces |
| DustyTome.AncientCard | PUBLIC title/hover tip after setup; RANDOM before reveal | reject: public root omits the revealed generated face |
| PaelsTooth.SerializableCards | chosen removal faces/history are public; remaining identities follow public returned cards | reject: current depletion/counter is not the required ordered card-face evidence |
| SeaGlass.CharacterId | PUBLIC title/description variant | reject: current relic face contains id/counter only |
| TouchOfOrobas starter/upgraded relic ids | PUBLIC hover tips and deterministic starter replacement | reject: certified relic face/history omits the pair |
| Byrdpip/PaelsLegion.Skin | presentation variant; no gameplay conclusion is inferred solely from SavedProperty | reject conservatively pending lifecycle/irrelevance certification |
| ArtOfWar/PaelsTears combat flags | TRANSIENT at the proven AfterCombatEnd reset | fresh constructor defaults at settled non-combat RUN boundaries |
| SneckoOil test override; Ashwater readonly tint | test/presentation fields in their source lifecycle | no claim that a potion NativeState blob is safe |
| Unclassified saved/mutable instance field | NOT CERTIFIED, rather than guessed LATENT | explicit fail closed with model/field names |

The full saved-property inventory follows. It is a source denominator, not a second
Run state schema and not a serialized native-state interface.

| Family | Model | Declared SavedProperty fields |
| --- | --- | --- |
| Cards | GeneticAlgorithm | `CurrentBlock` (int), `IncreasedBlock` (int) |
| Cards | Guilty | `CombatsSeen` (int) |
| Cards | MadScience | `TinkerTimeType` (CardType), `TinkerTimeRider` (TinkerTime.RiderEffect) |
| Cards | SpoilsMap | `SpoilsActIndex` (int) |
| Cards | TheScythe | `CurrentDamage` (int), `IncreasedDamage` (int) |
| Relics | ArchaicTooth | `StarterCard` (SerializableCard?), `AncientCard` (SerializableCard?) |
| Relics | BoneTea | `CombatsLeft` (int) |
| Relics | BookOfFiveRings | `CardsAdded` (int) |
| Relics | Byrdpip | `Skin` (string) |
| Relics | DustyTome | `AncientCard` (ModelId?) |
| Relics | EmberTea | `CombatsLeft` (int) |
| Relics | FakeHappyFlower | `TurnsSeen` (int) |
| Relics | FakeVenerableTeaSet | `GainEnergyInNextCombat` (bool) |
| Relics | FishingRod | `CombatsSeen` (int) |
| Relics | FurCoat | `FurCoatActIndex` (int), `FurCoatCoordCols` (int[]), `FurCoatCoordRows` (int[]), `FurCoatCoordsSet` (bool) |
| Relics | GalacticDust | `StarsSpent` (int) |
| Relics | Girya | `TimesLifted` (int) |
| Relics | GoldenCompass | `GoldenPathAct` (int) |
| Relics | HappyFlower | `TurnsSeen` (int) |
| Relics | IronClub | `CardsPlayed` (int) |
| Relics | JossPaper | `CardsExhausted` (int) |
| Relics | LastingCandy | `CombatsSeen` (int) |
| Relics | LavaLamp | `TookDamageThisCombat` (bool) |
| Relics | LavaRock | `HasTriggered` (bool) |
| Relics | LizardTail | `WasUsed` (bool) |
| Relics | MawBank | `HasItemBeenBought` (bool) |
| Relics | Nunchaku | `AttacksPlayed` (int) |
| Relics | PaelsLegion | `Skin` (string) |
| Relics | PaelsTooth | `SerializableCards` (List<SerializableCard>) |
| Relics | PaelsWing | `RewardsSacrificed` (int) |
| Relics | Pendulum | `TurnsSeen` (int) |
| Relics | PenNib | `AttacksPlayed` (int) |
| Relics | PollinousCore | `TurnsSeen` (int) |
| Relics | PumpkinCandle | `KindleCount` (int) |
| Relics | SeaGlass | `CharacterId` (ModelId?) |
| Relics | SilkenTress | `IsUsed` (bool) |
| Relics | SilverCrucible | `TimesUsed` (int), `TreasureRoomsEntered` (int) |
| Relics | SwordOfStone | `ElitesDefeated` (int) |
| Relics | TeaOfDiscourtesy | `CombatsLeft` (int) |
| Relics | TouchOfOrobas | `StarterRelic` (ModelId?), `UpgradedRelic` (ModelId?) |
| Relics | ToyBox | `CombatsSeen` (int) |
| Relics | TuningFork | `SkillsPlayed` (int) |
| Relics | VenerableTeaSet | `GainEnergyInNextCombat` (bool) |
| Relics | WingedBoots | `TimesUsed` (int) |
| Relics | WongosMysteryTicket | `CombatsFinished` (int), `GaveRelic` (bool) |

## Quest denominator

The source AddQuest call-site census identifies exactly two map-marker owners:
FUR_COAT and SPOILS_MAP. The other gameplay quest-card continuations checked here
are LANTERN_KEY and BYRDONIS_EGG. A boolean marker is never used as an identity.

| Family | Continuation state | What is public / what is missing | Status |
| --- | --- | --- | --- |
| FUR_COAT | FurCoatActIndex, marked coordinate set and initialization flag; marked encounters become 1 HP | public ownership plus the two-owner census and Monster/Elite marker topology identify the owner; durable ordered marker facts retain old-act coordinates because BeforeCombatStart tests coordinates without checking act | source-specific installation implemented; missing marker journal, duplicate owners or unowned markers FAIL-CLOSED; no positive acquired-quest native certification yet |
| SPOILS_MAP | SpoilsActIndex, SpoilsCoord; marked treasure pays 600 gold and removes the card | acquired-card AfterCreated sets act index 1; public act-1 treasure marker identifies coordinate; CreateForTest custom initial deck skips AfterCreated, leaving -1 | acquired-card constant/marker installation implemented; custom initial SpoilsMap fails `run_quest_initialization_producer_missing`; duplicate owner or missing ownership evidence rejects; no positive acquired-quest native certification yet |
| LANTERN_KEY | Act-index-2 event-selection hook (third act) | historical public card presence is captured with each event eligibility signature | native hook fold replayed in chronological event/relic operation order |
| BYRDONIS_EGG | native Hatch rest option obtains Byrdpip | current card model identity regenerates Hatch; obtained Byrdpip has a separate saved Skin owner | public card instance supported; future stateful relic root remains guarded |

No source-backed proof of an intrinsically non-identifiable vanilla domain is
claimed. The remaining gaps are missing certified public evidence/reconstruction
contracts in this implementation. They cannot be disguised as one mathematical
impossibility or eliminated by copying factual NativeState/queues.

## Base-class and producer limits

`CardModel.ToSerializable/FromSerializable` persists model, upgrades, enchantment,
saved properties and FloorAddedToDeck. The scoped FloorAddedToDeck read census
finds serialization, UI and logging uses, and command writes; no future gameplay
reader was found. This does not authorize copying the other mutable CardModel
fields. A deck affliction or a persistent local-cost modifier has no certified
portable face here and must reject rather than reset silently.

`Player.CreateForNewRun` owns starting deck/relics/potions and base HP/gold.
`RunManager.SetUpTest` applies ascension before the initialization public facts
are captured. Default starting-loadout reset intentionally contains no explicit
deck. Composition calls this native producer again; current public state is then
installed separately. The pre-bootstrap public initialization facts are never
used as a replacement for the current mid-run inventory.

`SmithRestSiteOption.OnSelect` suspends at `FromDeckForUpgrade` before any card
mutation. The public clicked SMITH option therefore regenerates an actual new
native selector and its upgrade/AfterRestSiteSmith continuation. No suspended
Task, native selector object or completion source is copied.

`TreasureRoomRelicSynchronizer.BeginRelicPicking` rolls rarity before the
first-chest tutorial GORGET constant. The constant removes GORGET from the shared
bag; the draw still belongs to the sampled RNG chronology. Normal chests pull
from the shared bag. Regeneration consumes only the accepted world's privately
materialized rarity/relic and recreates the native vote/skip lifecycle without a
second draw/depletion. Single-player skip uses its native skip flag, not a guessed
MoveToFallback. Suppression/modified producers such as SilverCrucible remain
guarded. Current treasure public faces do not expose its pre-open relic.

`MerchantInventory.CreateForNormalMerchant` populates card/relic/potion entries;
`OnTryPurchaseWrapper` mutates purchases/refills. Visible price is a hook-modified
native random base price. An arbitrary displayed price is not an invertible base
cost under discounts. Current shop inventory/price posterior and refill/purchase
continuation are not reconstructed and explicitly fail closed.

Historical Event selection and current Event continuation are separate domains.
The former is replayed natively from the public eligibility mask, floor, Lantern
Key presence and LuminousChoir's public gold predicate plus sampled bag query.
The latter still needs model-specific page/option variables and selected-prefix
evidence. For example WhisperingHollow.CalculateVars rolls the displayed gold
cost, GOLD suspends in OfferCustom(two PotionRewards), and HUG suspends in card
transformation. The current certified event face does not carry every rendered
dynamic variable or either continuation anchor. These roots reject; historical
eligibility certification cannot substitute for a page regeneration contract.

RelicFactory RNG overrides (for example CrystalSphere's event RNG), unaudited
filter producers, extra/stolen rewards and nested explicit room encounters also
remain guarded by named producer errors. They are missing certified source
chronology/continuation contracts, not proven mathematical non-identifiability.
