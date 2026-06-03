import json
import re
import xml.sax.saxutils as x
from pathlib import Path

root = Path(__file__).resolve().parents[1]
data = json.loads((root / "scripts/pass-dialogs-localization.json").read_text(encoding="utf-8"))


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
            'xmlns:vm="using:OscarWatch.ViewModels"\n             xmlns:loc="using:OscarWatch.Localization"',
            1,
        )
    for literal, key in sorted(mapping.items(), key=lambda item: -len(item[0])):
        for attr in ("Text", "Content", "Header", "Watermark", "ToolTip.Tip"):
            old = f'{attr}="{literal}"'
            new = f'{attr}="{{loc:Localize Key={key}}}"'
            ax = ax.replace(old, new)
    # Mutual grid has duplicate Satellite header - planner already done
    path.write_text(ax, encoding="utf-8")


def patch_time_combo(path: Path) -> None:
    ax = path.read_text(encoding="utf-8")
    old = """              <ComboBox HorizontalAlignment="Stretch"
                        SelectedIndex="{Binding TimeDisplayIndex, Mode=TwoWay}">
                <ComboBoxItem Content="{loc:Localize Key=Pass.Time.Local}" />
                <ComboBoxItem Content="{loc:Localize Key=Pass.Time.Utc}" />
              </ComboBox>"""
    new = """              <ComboBox HorizontalAlignment="Stretch"
                        ItemsSource="{Binding TimeDisplayLabels}"
                        SelectedIndex="{Binding TimeDisplayIndex, Mode=TwoWay}">
                <ComboBox.ItemTemplate>
                  <DataTemplate>
                    <TextBlock Text="{Binding}" />
                  </DataTemplate>
                </ComboBox.ItemTemplate>
              </ComboBox>"""
    if old not in ax:
        old2 = """            <ComboBox Grid.Column="1"
                      HorizontalAlignment="Left"
                      MinWidth="140"
                      SelectedIndex="{Binding TimeDisplayIndex, Mode=TwoWay}">
              <ComboBoxItem Content="{loc:Localize Key=Pass.Time.Local}" />
              <ComboBoxItem Content="{loc:Localize Key=Pass.Time.Utc}" />
            </ComboBox>"""
        new2 = """            <ComboBox Grid.Column="1"
                      HorizontalAlignment="Left"
                      MinWidth="140"
                      ItemsSource="{Binding TimeDisplayLabels}"
                      SelectedIndex="{Binding TimeDisplayIndex, Mode=TwoWay}">
              <ComboBox.ItemTemplate>
                <DataTemplate>
                  <TextBlock Text="{Binding}" />
                </DataTemplate>
              </ComboBox.ItemTemplate>
            </ComboBox>"""
        if old2 in ax:
            ax = ax.replace(old2, new2)
    else:
        ax = ax.replace(old, new)
    path.write_text(ax, encoding="utf-8")


if __name__ == "__main__":
    en = merge_resx(root / "OscarWatch/Resources/Strings.resx", data["en"])
    ja = merge_resx(root / "OscarWatch/Resources/Strings.ja.resx", data["ja"])
    print(f"resx: en +{en}, ja +{ja}")
    localize_axaml(root / "OscarWatch/Views/PassPlanningWindow.axaml", data["planner_axaml"])
    localize_axaml(root / "OscarWatch/Views/MutualPassWindow.axaml", data["mutual_axaml"])
    # Fix mutual Satellite column (second occurrence)
    mp = (root / "OscarWatch/Views/MutualPassWindow.axaml").read_text(encoding="utf-8")
    mp = mp.replace(
        'Header="{loc:Localize Key=Planner.Col.Satellite}"',
        'Header="{loc:Localize Key=Mutual.Col.Satellite}"',
        1,
    )
    (root / "OscarWatch/Views/MutualPassWindow.axaml").write_text(mp, encoding="utf-8")
    localize_axaml(root / "OscarWatch/Views/SunlightPredictionWindow.axaml", data["sunlight_axaml"])
    patch_time_combo(root / "OscarWatch/Views/PassPlanningWindow.axaml")
    patch_time_combo(root / "OscarWatch/Views/MutualPassWindow.axaml")
    print("axaml updated")
