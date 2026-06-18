#!/usr/bin/env python3
"""Apply AI-generated Spanish translations to Strings.es.resx."""
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
RES = ROOT / "OscarWatch" / "Resources"
ES_PATH = RES / "Strings.es.resx"
TRANS_PATH = Path(__file__).resolve().parent / "es-ai-translations.json"


def main() -> None:
    if not TRANS_PATH.exists():
        print(f"Missing {TRANS_PATH}", file=sys.stderr)
        sys.exit(1)

    translations: dict[str, str] = json.loads(TRANS_PATH.read_text(encoding="utf-8"))
    text = ES_PATH.read_text(encoding="utf-8")
    applied = 0
    missing = []

    for key, value in translations.items():
        pattern = (
            rf'(<data name="{re.escape(key)}">\s*<value>)(.*?)(</value>)'
        )

        def repl(m: re.Match[str], v: str = value) -> str:
            return m.group(1) + v + m.group(3)

        new_text, n = re.subn(pattern, repl, text, count=1, flags=re.DOTALL)
        if n:
            text = new_text
            applied += 1
        else:
            missing.append(key)

    ES_PATH.write_text(text, encoding="utf-8", newline="\n")
    print(f"Applied {applied} translations")
    if missing:
        print(f"Keys not found in resx ({len(missing)}):", missing[:10])


if __name__ == "__main__":
    main()
