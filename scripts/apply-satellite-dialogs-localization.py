import json
import re
import xml.sax.saxutils as x
from pathlib import Path

root = Path(__file__).resolve().parents[1]
data = json.loads((root / "scripts/satellite-dialogs-localization.json").read_text(encoding="utf-8"))


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
    if 'xmlns:loc="using:OscarWatch.Localization"' not in ax:
        ax = ax.replace(
            'xmlns:vm="using:OscarWatch.ViewModels"',
            'xmlns:vm="using:OscarWatch.ViewModels"\n        xmlns:loc="using:OscarWatch.Localization"',
            1,
        )
    for literal, key in sorted(mapping.items(), key=lambda item: -len(item[0])):
        for attr in ("Title", "Text", "Content", "Header", "Watermark", "ToolTip.Tip"):
            old = f'{attr}="{literal}"'
            new = f'{attr}="{{loc:Localize Key={key}}}"'
            ax = ax.replace(old, new)
    path.write_text(ax, encoding="utf-8")


def patch_merge_title(path: Path) -> None:
    ax = path.read_text(encoding="utf-8")
    ax = ax.replace(
        'Title="{loc:Localize Key=DbMerge.Title.Remote}"',
        'Title="{Binding WindowTitle}"',
        1,
    )
    path.write_text(ax, encoding="utf-8")


def patch_merge_yours(path: Path) -> None:
    ax = path.read_text(encoding="utf-8")
    ax = ax.replace(
        "Text=\"{Binding LocalSummary, StringFormat='Yours: {0}'}\"",
        'Text="{Binding YoursSummaryLine}"',
        1,
    )
    path.write_text(ax, encoding="utf-8")


if __name__ == "__main__":
    en = merge_resx(root / "OscarWatch/Resources/Strings.resx", data["en"])
    ja = merge_resx(root / "OscarWatch/Resources/Strings.ja.resx", data["ja"])
    print(f"resx: en +{en}, ja +{ja}")
    localize_axaml(root / "OscarWatch/Views/SatellitePickerWindow.axaml", data["picker_axaml"])
    localize_axaml(root / "OscarWatch/Views/SatelliteDatabaseWindow.axaml", data["db_axaml"])
    localize_axaml(root / "OscarWatch/Views/SatelliteDatabaseMergeWindow.axaml", data["merge_axaml"])
    patch_merge_title(root / "OscarWatch/Views/SatelliteDatabaseMergeWindow.axaml")
    patch_merge_yours(root / "OscarWatch/Views/SatelliteDatabaseMergeWindow.axaml")
    localize_axaml(root / "OscarWatch/Views/AddSatelliteFromTleWindow.axaml", data["add_axaml"])
    print("axaml updated")
