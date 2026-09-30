"""Regression tests for Divine's runtime-owned Power presentation surface."""

from __future__ import annotations

from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PERSISTENT = ROOT / "src" / "Sts2.NativeSim.Core" / "PersistentNativeCombatEnvironment.cs"
TRACE = ROOT / "src" / "Sts2.NativeSim.TraceExporter" / "TraceExporterMod.cs"


def _source(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig")


def _assert_power_snapshot_contract(source: str) -> None:
    assert 'amount = ' in source and '"Amount"' in source
    assert 'is_visible = Convert.ToBoolean(' in source
    assert '"IsVisible"' in source
    assert 'string stackType = ' in source
    assert '"StackType"' in source
    assert 'StringComparer.Ordinal.Equals(stackType, "Counter")' in source
    assert 'Convert.ToInt32(' in source
    assert '"DisplayAmount"' in source
    assert ': (int?)null' in source


def test_persistent_environment_exposes_runtime_power_display_surface() -> None:
    source = _source(PERSISTENT)

    _assert_power_snapshot_contract(source)
    assert 'Select(power => PowerObservationSnapshot(power!))' in source
    assert 'Select(x => PowerObservationSnapshot(x!))' in source


def test_trace_exporter_exposes_same_runtime_power_display_surface() -> None:
    source = _source(TRACE)

    _assert_power_snapshot_contract(source)
    assert ".Select(PowerObservationSnapshot).ToArray();" in source


def test_power_snapshot_preserves_raw_amount_and_does_not_filter_invisible_powers() -> None:
    persistent = _source(PERSISTENT)
    trace = _source(TRACE)

    for source in (persistent, trace):
        helper = source[source.index("PowerObservationSnapshot(object power)"):]
        assert 'amount = ' in helper
        assert '"Amount"' in helper
        assert 'is_visible = Convert.ToBoolean(' in helper
        assert 'display_amount = StringComparer.Ordinal.Equals(stackType, "Counter")' in helper

    assert '.Where(power => power is not null).Select(power => PowerObservationSnapshot(power!))' in persistent
    assert 'Enumerate(GetMember(creature, "Powers"))\n            .Select(PowerObservationSnapshot)' in trace
