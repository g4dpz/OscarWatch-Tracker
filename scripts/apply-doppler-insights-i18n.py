#!/usr/bin/env python3
"""Insert Doppler Pass Insights translations into satellite Strings.*.resx files."""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
RES = ROOT / "OscarWatch" / "Resources"
TRANS_PATH = Path(__file__).resolve().parent / "doppler-insights-translations.json"
ANCHOR = "Window.Sunlight.Title"

LOCALE_FILES = {
    "es": RES / "Strings.es.resx",
    "ja": RES / "Strings.ja.resx",
    "pt-BR": RES / "Strings.pt-BR.resx",
    "zh-CN": RES / "Strings.zh-CN.resx",
}


def extract_english_keys() -> list[tuple[str, str]]:
    text = (RES / "Strings.resx").read_text(encoding="utf-8")
    pattern = re.compile(
        r'<data name="((?:Window\.DopplerInsights|DopplerInsights)[^"]+)">\s*<value>(.*?)</value>',
        re.DOTALL,
    )
    return [(name, value.strip()) for name, value in pattern.findall(text)]


def format_entry(name: str, value: str) -> str:
    escaped = (
        value.replace("&", "&amp;")
        .replace("<", "&lt;")
        .replace(">", "&gt;")
    )
    return f'  <data name="{name}">\n    <value>{escaped}</value>\n  </data>\n'


def main() -> None:
    if not TRANS_PATH.exists():
        print(f"Missing {TRANS_PATH}", file=sys.stderr)
        sys.exit(1)

    translations: dict[str, dict[str, str]] = json.loads(
        TRANS_PATH.read_text(encoding="utf-8")
    )
    english = dict(extract_english_keys())
    if not english:
        print("No Doppler Insights keys found in Strings.resx", file=sys.stderr)
        sys.exit(1)

    for locale, path in LOCALE_FILES.items():
        if locale not in translations:
            print(f"Missing locale block: {locale}", file=sys.stderr)
            sys.exit(1)

        locale_map = translations[locale]
        missing = [k for k in english if k not in locale_map]
        if missing:
            print(f"{locale}: missing {len(missing)} keys, e.g. {missing[:5]}", file=sys.stderr)
            sys.exit(1)

        text = path.read_text(encoding="utf-8")
        if "DopplerInsights.Header" in text:
            print(f"{locale}: already contains DopplerInsights keys, skipping insert")
            continue

        block = "".join(format_entry(key, locale_map[key]) for key in english)
        anchor = f'  <data name="{ANCHOR}">'
        if anchor not in text:
            print(f"{locale}: anchor {ANCHOR} not found", file=sys.stderr)
            sys.exit(1)

        text = text.replace(anchor, block + anchor, 1)
        path.write_text(text, encoding="utf-8", newline="\n")
        print(f"{locale}: inserted {len(english)} keys into {path.name}")


if __name__ == "__main__":
    main()
