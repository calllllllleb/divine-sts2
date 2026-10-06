# Retired combat cards v1

Portable combat schema **8** composes resident Power references v1, card dynamic
runtime v1, and explicit retired combat cards. Shared history remains **v5** and
the fixed-DLL consumer contract is unchanged. Schemas 6/7 reject before reset.
The reviewed DLL is `A1F9E653F1E28E4076558FEE1E60D218619CB7E057B887C6417F62C62C6D7A52`.

## Native collection and identity contract

Playing an ordinary Power card can legitimately leave an executable native card
with no pile. A real Juggling play confirms Owner=self, IsDupe=false,
CloneOf/DupeOf=null, Pile=null and HasBeenRemovedFromState=true. Its CombatState
getter is null, but CardScope resolves to the owner's current combat.
CombatState._allCards still contains it, and NetCombatCardDb retains both ID
directions. PlayerCombatState.AllCards only enumerates piles and does not contain
the retired card. The old combined removed/foreign/dupe error did not establish
that the source card was a dupe.

The explicit combat-card domain is the five piles plus CombatState's registry.
`retired_cards` contains the same complete CardSnapshot codec as pile members,
including actual DynamicVars, cost/upgrade/attachments and native combat ID.
Each retired record explicitly carries version, owner net ID, null-pile/removed
flags, actual registry/NetDb membership, and nullable DeckVersion alias.
`combat_card_registry` preserves the actual native registry order. Native card
IDs and instance IDs must be one-to-one, below the saved allocator next ID.
DB entries outside this reviewed domain reject rather than disappearing.

The accepted retired profile is self-owned, non-dupe, no CloneOf/DupeOf lineage,
actually removed, with null Pile/CombatState and executable owner-derived
CardScope. It retains membership in both CombatState's registry and NetDb.
The native card is a Power subtype without additional declared private fields,
with no X/local/temporary costs or non-default play/replay/cost scratch state.
Other profiles reject with the specific missing condition.

## Exact DeckVersion aliases

DeckVersion is null or an actual self-owned native Deck member explicitly
identified in base_reset.Deck. Capture uses reference identity against the reset
ID-to-native-Deck mapping, then checks model, upgrade, enchantment and supplied
saved properties against that exact CardSpec. Matching model names is never a
fallback; repeated Juggling cards remain separate native Deck objects.

Construct retains its existing deckVersions association before populating
combat. ExportRunCombatRoot temporarily associates the generated base_reset IDs
with those exact current native Deck objects, then restores the prior bookkeeping.
Import prevalidates alias existence/model before resetting the target. Restore
resolves aliases through the destination's actual reset objects before card
hydration, writes the reference directly, and checks ReferenceEquals. It does
not clone cards or execute gameplay hooks to rebuild a Deck alias. Foreign,
non-reset or ambiguous aliases reject.

## Restore, history and branch behavior

Phase one hydrates every needed executable native card and its instance/native
ID mapping. Phase two restores the captured registry order and exact pile
membership. Retired cards never enter Hand/Draw/Discard/Exhaust/Play. The source
membership is preserved; no blanket retirement removal from AllCards is used.
Silent existing pile internals preserve the normal native subscriptions, without
replaying play/generation/movement commands. Card/Creator/CardPlay history binds
the same actual instances from this explicit domain after restoration.

All retired dynamic runtimes are validated and fingerprinted. The full retired
payload, Deck aliases, registry order, native next ID and instance allocator
participate in import and branch fingerprints, branch keys and passive shortcuts.
An unsupported clone successor still rejects capture/export, but a comparison
with an earlier certified branch returns unequal so that the known branch can
restore without replay. History replacement removes the successor tail.
These mechanical objects, IDs and aliases are private snapshot data; no new
policy-visible fields or vocabulary are introduced.

## Bounded real native acceptance

Each witness uses at most two workers, with explicit source imports and newly
built GodotHost assemblies. No training, fake Owner/dupe fields or ID-only
historical CardModel carriers are used.

- A legal Juggling play retires the actual source card. Cross-worker capture is
  exact; a real continued Strike deals **6** in both workers. Earlier branches
  restore with zero replay and the remembered source card reference is unchanged.
- The imported historical CardPlay selects that full native source object. Its
  real CreateClone and CardCmd.AutoPlay execute one CardPlayFinished and increase
  JugglingPower **1 -> 2**. The source remains removed and pile-null. Capturing the
  clone lineage rejects precisely. Restoring the earlier branch then restores
  its native IDs, source identity, membership and history with **zero replay**.
- Two same-model Juggling cards retain upgrades **0/1**, native IDs **0/1** and
  distinct exact Deck aliases. History **4 -> 8 -> 4** after a later actual Power
  play and old-branch restore has no accumulated tail.
- Native CombatState.CreateCard plus AddGeneratedCardToCombat creates a genuine
  Juggling with null DeckVersion/no clone lineage. Its legal play and imported
  CardGenerated/CardPlay history retain the same executable null-alias profile.
- Twenty malformed domain/runtime/alias variants reject before target reset,
  preserving both observation and resident native object identity.
- Ordinary Inflame now imports successfully and real Strike deals **8**, replacing
  the obsolete removed-card rejection test. Forge retains **20/20/30** damage
  across worker/old/later branches. History5/Osty and terminal restoration pass.
- The frozen normal starter seed42 SHRINK witness replays **39** factual actions
  with no Search/training. Its exact resident applier reference survives import;
  real kill removes Shrink in both workers, zero-replay restore and repeated kill
  agree, and nine malformed reference payloads reject before reset.

The focused suite passes **39** native tests. Divine fast tests pass **10** with
**10** opt-in skips. GodotHost Debug and Protocol/Core Release build with zero
warnings/errors. The public-tree scan remains blocked by the unchanged two
machine-specific Steam defaults; no game binaries/decompiled source are added.

HistoryCourse filters Attack/Skill and excludes Power cards. The Juggling
CreateClone/AutoPlay witness is therefore a controlled native execution consumer,
not a HistoryCourse candidate or normal-run/training closure claim.

JugglingPower/Toric private counters belong to a separate task and are not
extended here. This does not certify their complete lifecycle. Foreign owners,
arbitrary dupe/clone graphs, unregistered/un-IDed no-pile cards, additional card
private state and unreviewed costs/scratch remain unsupported. The preexisting
cached upgrade rollback and broader card/power/run boundaries remain unchanged.
