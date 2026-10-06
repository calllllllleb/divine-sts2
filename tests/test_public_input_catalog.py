"""One opt-in static catalog invariance witness; no gameplay breadth census."""
import os

import pytest
from sts2_native_sim import NativeWorker


@pytest.mark.skipif(os.environ.get("RUN_NATIVE_PUBLIC_INPUT_CATALOG") != "1", reason="requires exact native game and explicit built project")
def test_public_catalog_is_independent_of_an_initialized_run():
    with NativeWorker(project=os.environ["DIVINE_PUBLIC_CATALOG_PROJECT"],
                      godot=os.environ["GODOT"], assembly=os.environ["STS2_ASSEMBLY"]) as worker:
        before = worker.catalog()
        worker.reset({"game_build": {}, "seed": "public-catalog-invariance", "rng_counters": {},
            "character": "IRONCLAD", "ascension": 0, "encounter": "NIBBITS_WEAK",
            "current_hp": 80, "max_hp": 80, "gold": 99, "deck": [], "initial_hand": [],
            "relics": [], "potions": [], "use_character_starting_loadout": True})
        assert worker.catalog() == before == worker.catalog()
        assert before["game_build"] == worker.build
        assert before["public_input_catalog_version"] == 1
        assert before["provenance"]["run_state_read"] is False
        assert before["provenance"]["event_options_invoked"] is False
        assert {entry["family"] for entry in before["model_types"]} == {
            "character", "card", "monster", "encounter", "relic", "potion", "power", "orb", "enchantment", "affliction", "event"}
        assert "SMITH" in before["rest_option_ids"]
        assert any(table["table"] == "events" and table["keys"] for table in before["localization_keys"])
