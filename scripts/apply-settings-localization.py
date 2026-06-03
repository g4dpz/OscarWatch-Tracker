import json
import re
import xml.sax.saxutils as x
from pathlib import Path

root = Path(__file__).resolve().parents[1]
data = json.loads((root / "scripts/settings-localization-keys.json").read_text(encoding="utf-8"))


def merge_resx(path: Path, keys: dict[str, str]) -> int:
    text = path.read_text(encoding="utf-8")
    existing = set(re.findall(r'name="([^"]+)"', text))
    inserts = []
    for k, v in keys.items():
        if k in existing:
            continue
        inserts.append(f'  <data name="{k}">\n    <value>{x.escape(v)}</value>\n  </data>')
    if not inserts:
        return 0
    path.write_text(text.replace("</root>", "\n".join(inserts) + "\n</root>"), encoding="utf-8")
    return len(inserts)


def localize_axaml(path: Path, mapping: dict[str, str]) -> None:
    ax = path.read_text(encoding="utf-8")
    for literal, key in sorted(mapping.items(), key=lambda item: -len(item[0])):
        for attr in ("Text", "Content", "Watermark", "PlaceholderText"):
            old = f'{attr}="{literal}"'
            new = f'{attr}="{{loc:Localize Key={key}}}"'
            ax = ax.replace(old, new)
    ax = ax.replace(
        '<DataTemplate x:DataType="models:AppThemePreference">\n'
        "                  <TextBlock Text=\"{Binding}\" />",
        '<DataTemplate x:DataType="vm:ThemeOption">\n'
        '                  <TextBlock Text="{Binding Label}" />',
    )
    path.write_text(ax, encoding="utf-8")


if __name__ == "__main__":
    en = merge_resx(root / "OscarWatch/Resources/Strings.resx", data["en"])
    ja = merge_resx(root / "OscarWatch/Resources/Strings.ja.resx", data["ja"])
    print(f"en added {en}, ja added {ja}")
    localize_axaml(root / "OscarWatch/Views/SettingsWindow.axaml", data["axaml"])
    print("axaml updated")
