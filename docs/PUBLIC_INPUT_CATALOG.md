# Public input catalog v1

Incremental baseline: `e2aa6d7ba605fbc0a6953a56cad1dcaea22d3517`.
This branch adds static metadata only. Gameplay, history restoration and current
observations retain their existing contracts.

`NativeWorker.catalog()` keeps the original collections and adds:

- `public_input_catalog_version: 1`;
- `powers` (`ModelDb.AllPowers`), `orbs` (`ModelDb.Orbs`), and `afflictions`
  (`ModelDb.DebugAfflictions`);
- `model_types`: all public, concrete fixed-DLL subtypes of the eleven supported
  model families. `ModelDb.GetEntry(Type)` supplies IDs without instance creation.
  This includes generated/debug IDs absent from pool-based collections;
- `enums`: names of fixed-DLL public enums;
- `localization_keys`: English static table names and keys, without rendered text;
- `rest_option_ids`: the nine OptionId literals, decoded from the exact shipped
  getter IL (`ldstr; ret`). A changed getter fails with
  `public_option_id_not_static_literal` rather than executing it;
- `provenance`: the static sources, with run-state reads and Event option calls
  explicitly false.

The catalog does not create events, call GetOptions/GenerateInitialOptions,
generate a rest screen, draw from a bag, inspect a current run pool/queue/RNG, or
populate an observation with unoffered content. English fallback reads the loaded
English table dictionary; when English is active it reads the active English
dictionary. Unavailable English assets fail closed.

Consumer text-key derivation must account for `EventModel.GetOptionTitle(key)`
and `GetOptionDescription(key)` appending `.title` and `.description`. Those asset
key stems and static model IDs cover the corresponding option keys. Custom
runtime-composed keys without a corresponding asset/model identity remain an
explicit public supplement domain; this API does not predict generated options.

The pinned installed v0.107.1 DLL/PCK produced 1558 public model type identities,
139 public enums and 46 localization tables. The original pool collections contain
578 cards, 80 encounters, 102 monsters, 297 relics, 65 potions, five characters,
24 enchantments and 57 events; the new collections contain 273 powers, four orbs
and ten afflictions. These counts describe this exact build, not future versions.

Validation: isolated Debug GodotHost build, zero warnings/errors; repeated catalog
reads are identical, including before/after a real normal-starting-loadout reset.
The opt-in regression `tests/test_public_input_catalog.py` requires
`RUN_NATIVE_PUBLIC_INPUT_CATALOG=1`, `DIVINE_PUBLIC_CATALOG_PROJECT`, `GODOT` and
`STS2_ASSEMBLY`, so verification explicitly selects the caller's built worktree.
