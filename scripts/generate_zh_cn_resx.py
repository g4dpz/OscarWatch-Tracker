#!/usr/bin/env python3
"""Generate OscarWatch/Resources/Strings.zh-CN.resx from Strings.resx."""
from __future__ import annotations

import re
import sys
import time
import xml.etree.ElementTree as ET
from pathlib import Path

try:
    from deep_translator import GoogleTranslator
except ImportError:
    print("Run: pip install deep-translator", file=sys.stderr)
    sys.exit(1)

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "OscarWatch" / "Resources" / "Strings.resx"
TARGET = ROOT / "OscarWatch" / "Resources" / "Strings.zh-CN.resx"

# Keep English (brands, protocols, callsign patterns, URLs)
KEEP_AS_IS = re.compile(
    r"^(OscarWatch|Cloudlog|PayPal|GitHub|AGPL|CAT|CTCSS|TSQL|D-STAR|FMN?|USB|LSB|CW|"
    r"RX|TX|VFO|SATL?|TLE|NORAD|SGP4|SDP4|RTS|COM|CI-V|FT-\d|IC-\d+|TS-2000|"
    r"GS-232|SPID|HRD|SatPC32|MM9SQL|github\.com|tle\.oscarwatch\.org|"
    r"\d+\s*(Hz|kHz|MHz|ms|W|°|m)\b.*)$",
    re.I,
)

# Phrase-level overrides (English substring -> Chinese); longer first
GLOSSARY: list[tuple[str, str]] = [
    ("OscarWatch", "OscarWatch"),
    ("Doppler", "多普勒"),
    ("doppler", "多普勒"),
    ("satellite", "卫星"),
    ("Satellite", "卫星"),
    ("Satellites", "卫星"),
    ("pass", "过境"),
    ("Pass", "过境"),
    ("passes", "过境"),
    ("Passes", "过境"),
    ("downlink", "下行"),
    ("Downlink", "下行"),
    ("uplink", "上行"),
    ("Uplink", "上行"),
    ("transponder", "转发器"),
    ("Transponder", "转发器"),
    ("rotator", "转台"),
    ("Rotator", "转台"),
    ("amateur radio", "业余无线电"),
    ("Amateur radio", "业余无线电"),
    ("Settings", "设置"),
    ("settings", "设置"),
    ("File", "文件"),
    ("Cancel", "取消"),
    ("Close", "关闭"),
    ("Save", "保存"),
    ("Delete", "删除"),
    ("Refresh", "刷新"),
    ("Browse", "浏览"),
    ("About", "关于"),
    ("Help", "帮助"),
    ("English", "英语"),
    ("Japanese", "日语"),
    ("Portuguese", "葡萄牙语（巴西）"),
    ("Simplified Chinese", "简体中文"),
]

translator = GoogleTranslator(source="en", target="zh-CN")
cache: dict[str, str] = {}


def protect_placeholders(text: str) -> tuple[str, list[str]]:
    tokens: list[str] = []

    def repl(m: re.Match[str]) -> str:
        tokens.append(m.group(0))
        return f"@@{len(tokens) - 1}@@"

    protected = re.sub(r"\{[0-9]+\}", repl, text)
    return protected, tokens


def restore_placeholders(text: str, tokens: list[str]) -> str:
    for i, tok in enumerate(tokens):
        text = text.replace(f"@@{i}@@", tok)
    return text


def apply_glossary(text: str) -> str:
    for en, zh in GLOSSARY:
        text = text.replace(en, zh)
    return text


def translate_value(text: str) -> str:
    if not text or not text.strip():
        return text
    if text in cache:
        return cache[text]
    if KEEP_AS_IS.match(text.strip()):
        cache[text] = text
        return text

    protected, tokens = protect_placeholders(text)
    try:
        zh = translator.translate(protected)
        time.sleep(0.05)
    except Exception as e:
        print(f"WARN translate failed: {text[:60]!r} -> {e}", file=sys.stderr)
        zh = protected

    zh = restore_placeholders(zh, tokens)
    zh = apply_glossary(zh)
    cache[text] = zh
    return zh


def main() -> None:
    tree = ET.parse(SOURCE)
    root = tree.getroot()
    count = 0
    for data in root.findall("data"):
        name = data.get("name")
        if not name:
            continue
        value_el = data.find("value")
        if value_el is None or value_el.text is None:
            continue
        original = value_el.text
        if name == "Settings.Language.SimplifiedChinese":
            value_el.text = "简体中文"
            count += 1
            continue
        value_el.text = translate_value(original)
        count += 1
        if count % 50 == 0:
            print(f"Translated {count}...", flush=True)

    tree.write(TARGET, encoding="utf-8", xml_declaration=True)
    print(f"Wrote {TARGET} ({count} strings)")


if __name__ == "__main__":
    main()
