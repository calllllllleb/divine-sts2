"""Small public terminal-to-active snapshot contract. Author: XuShuxi."""
import os

import pytest
from sts2_native_sim import NativeWorker


@pytest.mark.skipif(os.environ.get("RUN_NATIVE_TERMINAL_RESTORE") != "1", reason="requires exact native game")
@pytest.mark.parametrize("turns,move", [(0, "BUTT_MOVE"), (1, "SLICE_MOVE"), (2, "HISS_MOVE")])
def test_terminal_branch_restores_active_registered_enemy(turns, move):
    """Removed enemies recover exact registered moves without replay. Author: XuShuxi."""
    with NativeWorker() as worker:
        worker.reset({
            "game_build": {}, "rng_counters": {}, "ascension": 0,
            "relics": [], "potions": [], "gold": 99,
            "seed": "terminal-restore-contract", "character": "IRONCLAD",
            "encounter": "NIBBITS_WEAK", "current_hp": 500, "max_hp": 500,
            "deck": [{"instance_id": f"strike-{i}", "model_id": "STRIKE_IRONCLAD", "upgrades": 1} for i in range(10)],
            "initial_hand": [f"strike-{i}" for i in range(5)],
            "use_character_starting_loadout": False,
        })
        for _ in range(turns):
            worker.step("end_turn")
        source = worker.observe()
        handle = worker.fork()
        rules = worker.describe_monster_move_rules(handle, selections=[])[0]
        assert move in {s["state_id"] for s in rules["states"] if s["kind"] == "MoveState"}
        enemy = next(c for c in source["observation"]["combat"]["creatures"] if c["side"] == "Enemy")
        assert enemy["alive"] and enemy["next_move"]["id"] == move
        state = source
        # XuShuxi: This bounded legal sequence exercises real death/removal, not private state.
        for _ in range(12):
            action = next((a for a in state["legal_actions"] if a["kind"] == "play_card"), None)
            state = worker.step(action["action_id"] if action else "end_turn")
            if state["terminated"]:
                break
        assert state["terminated"] and state["victory"]
        assert not any(c["side"] == "Enemy" for c in state["observation"]["combat"]["creatures"])
        restored = worker.restore(handle)
        assert restored["state_hash"] == source["state_hash"]
        assert restored["observation"] == source["observation"]
        assert restored["legal_actions"] == source["legal_actions"]
        assert restored["transition"]["replayed_actions"] == 0
        rules = worker.describe_monster_move_rules(restored["state_handle"], selections=[])[0]
        assert move in {s["state_id"] for s in rules["states"] if s["kind"] == "MoveState"}
