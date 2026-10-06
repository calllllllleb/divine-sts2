"""Real native Power-card retirement; serial tests use at most two workers."""
import os
from copy import deepcopy

import pytest
from sts2_native_sim import NativeSimError, NativeWorker

pytestmark = pytest.mark.skipif(os.environ.get("RUN_NATIVE_RETIRED_COMBAT_CARDS") != "1", reason="requires reviewed fixed native DLL")


def _spec():
    cards = [{"instance_id": "power-a", "model_id": "JUGGLING"},
             {"instance_id": "power-b", "model_id": "JUGGLING", "upgrades": 1},
             {"instance_id": "strike", "model_id": "STRIKE_SILENT"}]
    return {"game_build": {}, "rng_counters": {}, "character": "SILENT", "ascension": 0,
            "current_hp": 80, "max_hp": 80, "gold": 99, "seed": "RETIRED-COMBAT-CARDS",
            "encounter": "NIBBITS_WEAK", "deck": cards, "initial_hand": [c["instance_id"] for c in cards],
            "enemies": [{"model_id": "NIBBIT", "current_hp": 1000, "max_hp": 1000}],
            "relics": [], "potions": [], "energy": 20, "use_character_starting_loadout": False}


def _play(worker, state, identity):
    return worker.step(next(a["action_id"] for a in state["legal_actions"] if a["kind"] == "play_card"
                            and a["parameters"].get("instance_id") == identity))


def _hp(state):
    return sum(c["hp"] for c in state["observation"]["combat"]["creatures"] if c["side"] == "Enemy")


def test_same_model_retired_power_cards_keep_distinct_deck_aliases_and_native_continuation():
    with NativeWorker(request_timeout=30) as source, NativeWorker(request_timeout=30) as target:
        state = _play(source, source.reset(_spec()), "power-a")
        first = source.export_combat_root()
        handle = source.fork()
        state = _play(source, state, "power-b")
        root = source.export_combat_root()
        assert root["schema_version"] == 8
        assert source.hello()["retired_combat_card_version"] == 1
        retired = root["combat_snapshot"]["retired_cards"]
        assert [(r["card"]["instance_id"], r["card"]["upgrades"], r["deck_version_alias"]) for r in retired] == [
            ("power-a", 0, "power-a"), ("power-b", 1, "power-b")]
        assert len({r["card"]["combat_card_id"] for r in retired}) == 2
        assert all(r["pile_is_null"] and r["has_been_removed_from_state"] and r["in_combat_registry"] and r["in_net_combat_card_db"] for r in retired)
        imported = target.import_combat_root(root)
        assert target.export_combat_root()["combat_snapshot"] == root["combat_snapshot"]
        assert not any(c["instance_id"].startswith("power-") for p in imported["observation"]["combat"]["piles"] for c in p["cards"])
        actual = _play(target, imported, "strike")
        expected = _play(source, state, "strike")
        assert _hp(imported) - _hp(actual) == _hp(state) - _hp(expected) == 6
        assert actual["state_hash"] == expected["state_hash"]
        restored = source.restore(handle)
        assert restored["transition"]["replayed_actions"] == 0
        assert source.export_combat_root()["combat_snapshot"] == first["combat_snapshot"]
        assert any(c["instance_id"] == "power-b" for p in restored["observation"]["combat"]["piles"] for c in p["cards"])


def test_invalid_retired_membership_identity_alias_and_runtime_reject_before_reset():
    with NativeWorker(request_timeout=30) as worker:
        state = _play(worker, worker.reset(_spec()), "power-a")
        original = worker.export_combat_root()
        for mutation in ("schema", "missing_retired", "null_retired", "registry", "owner", "pile", "removed", "db", "version",
                         "duplicate", "missing_alias", "foreign_alias", "wrong_model_alias", "dynamic", "extra"):
            root = deepcopy(original)
            snap = root["combat_snapshot"]
            retired = snap["retired_cards"][0]
            if mutation == "schema": root["schema_version"] = 7
            elif mutation == "missing_retired": del snap["retired_cards"]
            elif mutation == "null_retired": snap["retired_cards"] = None
            elif mutation == "registry": snap["combat_card_registry"].append("foreign-card")
            elif mutation == "owner": retired["owner_net_id"] = 2
            elif mutation == "pile": retired["pile_is_null"] = False
            elif mutation == "removed": retired["card"]["has_been_removed_from_state"] = False
            elif mutation == "db": retired["in_net_combat_card_db"] = False
            elif mutation == "version": retired["version"] = 0
            elif mutation == "duplicate": snap["retired_cards"].append(deepcopy(retired))
            elif mutation == "missing_alias": del retired["deck_version_alias"]
            elif mutation == "foreign_alias": retired["deck_version_alias"] = "foreign-deck"
            elif mutation == "wrong_model_alias": retired["deck_version_alias"] = "strike"
            elif mutation == "dynamic": retired["card"]["dynamic_runtime"]["version"] = 0
            elif mutation == "extra": retired["future_graph"] = {"owner": "foreign"}
            before = worker.observe()
            with pytest.raises(NativeSimError): worker.import_combat_root(root)
            assert worker.observe() == before
            assert worker.export_combat_root() == original
