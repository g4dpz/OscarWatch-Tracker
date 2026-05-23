#!/usr/bin/env python3
"""Generate OscarWatch.Tests/Fixtures/radio_golden.json (optional external sat_utils checkout)."""
import json
import sys
from pathlib import Path

# Dev-only: clone a Python sat_utils package alongside the repo if regenerating fixtures.
_reference_roots = (
    Path(__file__).resolve().parents[2].parent / "QTrigdoppler",
    Path(__file__).resolve().parents[1].parent / "QTrigdoppler",
)
REFERENCE_ROOT = next((p for p in _reference_roots if p.exists()), _reference_roots[0])

sys.path.insert(0, str(REFERENCE_ROOT))

from lib import sat_utils  # noqa: E402

C = 299792458.0


def encode_frequency_hz(hz: int) -> str:
    freq = ("0000000000" + str(hz))[-10:]
    b = bytes(
        [
            5,
            int(freq[8:10], 16),
            int(freq[6:8], 16),
            int(freq[4:6], 16),
            int(freq[2:4], 16),
            int(freq[0:2], 16),
        ]
    )
    return b.hex()


class FakeEphem:
    def __init__(self, range_velocity_mps: float):
        self.range_velocity = range_velocity_mps

    def compute(self, _loc):
        pass


def main():
    out_path = Path(__file__).resolve().parents[1] / "OscarWatch.Tests" / "Fixtures" / "radio_golden.json"
    out_path.parent.mkdir(parents=True, exist_ok=True)

    ephem = FakeEphem(7500.0)
    loc = object()

    fixtures = {
        "frequencyEncode": [
            {"hz": 145950000, "payloadHex": encode_frequency_hz(145950000)},
            {"hz": 432146000, "payloadHex": encode_frequency_hz(432146000)},
        ],
        "rigSatMode": [
            {"downlinkKHz": 145950, "uplinkKHz": 432146, "useMainSub": abs(145950 - 432146) > 10_000},
            {"downlinkKHz": 436795, "uplinkKHz": 145850, "useMainSub": abs(436795 - 145850) > 10_000},
        ],
        "setupVfos": [
            {"downlinkMode": "FMN", "thresholdHz": 200, "interactive": False},
            {"downlinkMode": "USB", "thresholdHz": 50, "interactive": True},
            {"downlinkMode": "DATA-USB", "thresholdHz": 0, "interactive": False},
        ],
        "doppler": [],
    }

    for nominal_hz, rv in [(145950000, 7500.0), (432146000, -5000.0)]:
        ephem.range_velocity = rv
        rx = sat_utils.rx_dopplercalc(ephem, nominal_hz, loc)
        tx = sat_utils.tx_dopplercalc(ephem, nominal_hz, loc)
        fixtures["doppler"].append(
            {
                "nominalHz": nominal_hz,
                "rangeVelocityMps": rv,
                "rxHz": rx,
                "txHz": tx,
            }
        )

    out_path.write_text(json.dumps(fixtures, indent=2), encoding="utf-8")
    print(f"Wrote {out_path}")


if __name__ == "__main__":
    main()
