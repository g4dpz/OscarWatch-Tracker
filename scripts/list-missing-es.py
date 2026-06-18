#!/usr/bin/env python3
import json
import re
from pathlib import Path

RES = Path(__file__).resolve().parent.parent / "OscarWatch" / "Resources"

# Keys that should stay identical to en-GB (brands, codes, proper nouns).
KEEP_ENGLISH = {
    "About.Copyright",
    "About.Github",
    "About.Paypal",
    "About.Version",
    "Settings.Language.English",
    "Settings.Language.Japanese",
    "Settings.Language.PortugueseBrazil",
    "Settings.Language.SimplifiedChinese",
    "Settings.Language.Spanish",
    "Settings.Tab.Cloudlog",
    "Settings.Tab.HamsAt",
    "Settings.Rig.IcomIc705",
    "Settings.Rig.YaesuFt847",
    "Settings.Rig.YaesuFt991",
    "Settings.Rig.YaesuFtDx",
    "Settings.Rotator.Gs232",
    "Main.Pass.Recording.Format",
}


def parse_resx(path: Path) -> dict[str, str]:
    text = path.read_text(encoding="utf-8")
    return dict(
        re.findall(
            r'<data name="([^"]+)"[^>]*>\s*<value>(.*?)</value>',
            text,
            re.DOTALL,
        )
    )


def main() -> None:
    en = parse_resx(RES / "Strings.resx")
    es = parse_resx(RES / "Strings.es.resx")
    missing = {
        k: en[k]
        for k in sorted(en)
        if k in es and en[k] == es[k] and k not in KEEP_ENGLISH
    }
    out = RES.parent.parent / "scripts" / "missing-es-keys.json"
    out.write_text(json.dumps(missing, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Need translation: {len(missing)} keys -> {out}")


if __name__ == "__main__":
    main()
