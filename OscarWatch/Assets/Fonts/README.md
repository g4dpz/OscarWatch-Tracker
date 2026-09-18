# Embedded UI fonts

## Japanese

Shipped file: **`NotoSansCJKjp-Regular.otf`** (Noto Sans CJK JP, OFL license).

OscarWatch loads it from `avares://OscarWatch/Assets/Fonts` when Japanese UI is selected (Settings → Appearance → Language → 日本語, then restart). Latin text still uses Inter via fallbacks.

**Simplified Chinese (`zh-CN`)** uses system fonts first (Microsoft YaHei, SimHei, PingFang SC, Noto Sans CJK SC) with the embedded Noto CJK JP as a fallback for missing glyphs. Restart after changing language.

To replace or upgrade, use the Japanese OTF from [Noto CJK](https://github.com/notofonts/noto-cjk/tree/main/Sans/OTF/Japanese) and keep the same file name, or update `EmbeddedJapaneseFontFileName` in `AppFontConfiguration.cs`.

If the file is removed, Japanese mode falls back to system CJK fonts (Yu Gothic, Meiryo, Hiragino, etc.).

## Thai

Shipped file: **`NotoSansThai-Regular.ttf`** (Noto Sans Thai, OFL license).

OscarWatch loads it when Thai UI is selected (Settings → Appearance → Language → ไทย, then restart), and uses it as a fallback so the language picker can show Thai script in other locales. System fonts (Leelawadee UI, Tahoma, Thonburi) are tried as well.

To replace or upgrade, use the Regular TTF from [Noto Thai](https://github.com/notofonts/thai) and keep the same file name, or update `EmbeddedThaiFontFileName` in `AppFontConfiguration.cs`.
