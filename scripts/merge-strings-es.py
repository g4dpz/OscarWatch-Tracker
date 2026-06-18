#!/usr/bin/env python3
"""Rebuild Strings.es.resx from en-GB keys + contributor Spanish translations."""
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
RES = ROOT / "OscarWatch" / "Resources"
EN_PATH = RES / "Strings.resx"
ES_CONTRIB_PATH = RES / "Strings.es.resx"
OUT_PATH = RES / "Strings.es.resx"

HEADER = """<?xml version='1.0' encoding='utf-8'?>
<root>
  <resheader name="resmimetype">text/microsoft-resx</resheader>
  <resheader name="version">2.0</resheader>
  <resheader name="reader">System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</resheader>
  <resheader name="writer">System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</resheader>
"""


def parse_resx(path: Path) -> dict[str, str]:
    text = path.read_text(encoding="utf-8")
    return dict(
        re.findall(
            r'<data name="([^"]+)"[^>]*>\s*<value>(.*?)</value>',
            text,
            re.DOTALL,
        )
    )


def key_order(path: Path) -> list[str]:
    return re.findall(r'<data name="([^"]+)"', path.read_text(encoding="utf-8"))


def escape_value(value: str) -> str:
  # Values in source resx are already entity-safe; preserve as-is.
    return value


def main() -> None:
    en_order = key_order(EN_PATH)
    en = parse_resx(EN_PATH)
    contrib = parse_resx(ES_CONTRIB_PATH)
    ai_extra = Path(__file__).resolve().parent / "es-ai-translations.json"
    if ai_extra.exists():
        contrib.update(json.loads(ai_extra.read_text(encoding="utf-8")))

    lines = [HEADER.rstrip()]
    spanish = 0
    fallback = 0
    for key in en_order:
        if key in contrib:
            value = contrib[key]
            spanish += 1
        else:
            value = en[key]
            fallback += 1
        if key == "Settings.Language.Spanish":
            value = "Español"
        lines.append(f'  <data name="{key}">')
        lines.append(f"    <value>{escape_value(value)}</value>")
        lines.append("  </data>")
    lines.append("</root>")
    lines.append("")

    OUT_PATH.write_text("\n".join(lines), encoding="utf-8", newline="\n")
    print(f"Wrote {OUT_PATH.name}: {len(en_order)} keys ({spanish} Spanish, {fallback} en-GB fallback)")


if __name__ == "__main__":
    main()
