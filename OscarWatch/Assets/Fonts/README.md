# Embedded Japanese font

Shipped file: **`NotoSansCJKjp-Regular.otf`** (Noto Sans CJK JP, OFL license).

OscarWatch loads it from `avares://OscarWatch/Assets/Fonts` when Japanese UI is selected (Settings → Appearance → Language → 日本語, then restart). Latin text still uses Inter via fallbacks.

**Simplified Chinese (`zh-CN`)** uses system fonts first (Microsoft YaHei, SimHei, PingFang SC, Noto Sans CJK SC) with the embedded Noto CJK JP as a fallback for missing glyphs. Restart after changing language.

To replace or upgrade, use the Japanese OTF from [Noto CJK](https://github.com/notofonts/noto-cjk/tree/main/Sans/OTF/Japanese) and keep the same file name, or update `EmbeddedJapaneseFontFileName` in `AppFontConfiguration.cs`.

If the file is removed, Japanese mode falls back to system CJK fonts (Yu Gothic, Meiryo, Hiragino, etc.).
