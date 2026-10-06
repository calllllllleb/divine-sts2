"""Bounded real-card witnesses; controlled scenarios do not produce training labels."""
import os
from copy import deepcopy

import pytest
from sts2_native_sim import NativeSimError, NativeWorker

pytestmark = pytest.mark.skipif(os.environ.get("RUN_NATIVE_CARD_DYNAMIC_RUNTIME") != "1", reason="requires reviewed fixed native DLL")


def _spec(models, character="REGENT"):
    cards = [{"instance_id": f"dynamic-{i}", "model_id": model} for i, model in enumerate(models)]
    return {
        "game_build": {}, "rng_counters": {}, "character": character, "ascension": 0,
        "current_hp": 80, "max_hp": 80, "gold": 99, "seed": "CARD-DYNAMIC-RUNTIME-V1",
        "encounter": "NIBBITS_WEAK", "deck": cards, "initial_hand": [c["instance_id"] for c in cards],
        "enemies": [{"model_id": "NIBBIT", "current_hp": 1000, "max_hp": 1000}],
        "relics": [], "potions": [], "energy": 20, "use_character_starting_loadout": False,
    }


def _play(worker, state, index):
    return worker.step(next(a["action_id"] for a in state["legal_actions"] if a["kind"] == "play_card"
                            and a["parameters"].get("instance_id") == f"dynamic-{index}"))


def _hp(state):
    return sum(c["hp"] for c in state["observation"]["combat"]["creatures"] if c["side"] == "Enemy")


def _card(root, index):
    return next(c for pile in ("hand", "draw_pile", "discard_pile", "exhaust_pile", "play_pile")
                for c in root["combat_snapshot"][pile] if c["instance_id"] == f"dynamic-{index}")


def _value(card, name):
    return next(v["base_value"] for v in card["dynamic_runtime"]["dynamic_vars"]["variables"] if v["name"] == name)


def test_real_forge_cross_worker_damage_and_zero_replay_old_branch():
    with NativeWorker(request_timeout=30) as source, NativeWorker(request_timeout=30) as target:
        state = source.reset(_spec(["SOVEREIGN_BLADE", "BULWARK", "BULWARK"]))
        assert _value(_card(source.export_combat_root(), 0), "Damage") == "10"
        state = _play(source, state, 1)  # Native Bulwark -> ForgeCmd.Forge(10).
        root = source.export_combat_root()
        frozen = deepcopy(root)
        assert root["schema_version"] == 7 and source.hello()["card_dynamic_runtime_version"] == 1
        blade = _card(root, 0)
        assert _value(blade, "Damage") == blade["dynamic_runtime"]["sovereign_blade"]["current_damage"] == "20"
        assert blade["saved_properties"] == {}
        handle = source.fork()
        imported = target.import_combat_root(root)
        assert imported["transition"]["replayed_actions"] == 0
        assert target.export_combat_root()["combat_snapshot"] == root["combat_snapshot"]
        assert _hp(imported) - _hp(_play(target, imported, 0)) == 20
        changed = _play(source, state, 2)
        assert _value(_card(source.export_combat_root(), 0), "Damage") == "30"
        assert root == frozen  # Later Forge never mutates the old payload.
        later = source.fork()
        restored = source.restore(handle)
        assert restored["transition"]["kind"] == "snapshot_restore" and restored["transition"]["replayed_actions"] == 0
        assert source.export_combat_root()["combat_snapshot"] == root["combat_snapshot"]
        assert _hp(restored) - _hp(_play(source, restored, 0)) == 20
        late_restored = source.restore(later)
        assert late_restored["transition"]["replayed_actions"] == 0
        assert _hp(late_restored) - _hp(_play(source, late_restored, 0)) == 30
        assert changed["state_hash"] != restored["state_hash"]


def test_claw_real_growth_uses_common_numeric_channel_without_upgrade():
    # Claw's separate private downgrade accumulator is OUTSIDE this witness/codec.
    # This verifies immediate real damage and continuation in a no-upgrade scenario.
    with NativeWorker(request_timeout=30) as source, NativeWorker(request_timeout=30) as target:
        state = source.reset(_spec(["CLAW", "CLAW", "CLAW"], "DEFECT"))
        state = _play(source, state, 0)
        root = source.export_combat_root()
        assert _value(_card(root, 1), "Damage") == "5"
        handle = source.fork()
        imported = target.import_combat_root(root)
        assert _hp(imported) - _hp(_play(target, imported, 1)) == 5
        changed = _play(source, state, 2)
        assert _value(_card(source.export_combat_root(), 1), "Damage") == "7"
        restored = source.restore(handle)
        assert restored["transition"]["replayed_actions"] == 0
        assert _hp(restored) - _hp(_play(source, restored, 1)) == 5
        assert changed["state_hash"] != restored["state_hash"]


def test_body_slam_native_formula_uses_restored_owner_and_current_block():
    with NativeWorker(request_timeout=30) as source, NativeWorker(request_timeout=30) as target:
        state = source.reset(_spec(["DEFEND_IRONCLAD", "BODY_SLAM", "DEFEND_IRONCLAD"], "IRONCLAD"))
        state = _play(source, state, 0)
        root = source.export_combat_root()
        imported = target.import_combat_root(root)
        assert _hp(imported) - _hp(_play(target, imported, 1)) == 5
        handle = source.fork()
        state = _play(source, state, 2)
        assert _hp(state) - _hp(_play(source, state, 1)) == 10
        restored = source.restore(handle)
        assert restored["transition"]["replayed_actions"] == 0
        assert _hp(restored) - _hp(_play(source, restored, 1)) == 5


def test_invalid_card_dynamic_payload_rejects_before_target_reset():
    with NativeWorker(request_timeout=30) as worker:
        worker.reset(_spec(["SOVEREIGN_BLADE", "BULWARK"]))
        original = worker.export_combat_root()
        for mutation in ("old_schema", "missing", "version", "type", "missing_variable", "duplicate", "variable_type", "foreign_name", "decimal", "missing_owner", "formula_owner", "blade_private", "unknown_member"):
            root = deepcopy(original)
            card = _card(root, 0)
            runtime = card["dynamic_runtime"]
            variables = runtime["dynamic_vars"]["variables"]
            if mutation == "old_schema": root["schema_version"] = 6
            elif mutation == "missing": del card["dynamic_runtime"]
            elif mutation == "version": runtime["version"] = 0
            elif mutation == "type": runtime["native_type"] = "foreign.Card"
            elif mutation == "missing_variable": variables.pop()
            elif mutation == "duplicate": variables[-1] = variables[0]
            elif mutation == "variable_type": variables[0]["native_type"] = "foreign.Variable"
            elif mutation == "foreign_name": variables[0]["name"] = "FutureVariable"
            elif mutation == "decimal": variables[0]["base_value"] = "NaN"
            elif mutation == "missing_owner": del variables[0]["has_owner"]
            elif mutation == "formula_owner": variables[0]["has_owner"] = False
            elif mutation == "blade_private": runtime["sovereign_blade"]["current_damage"] = "99"
            else: runtime["future_field"] = True
            with pytest.raises(NativeSimError): worker.import_combat_root(root)
            assert worker.export_combat_root() == original
