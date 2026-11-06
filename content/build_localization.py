#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""从 content/*.md 生成 src/AdmirerLona/Localization/zh.csv。

用法（仓库根目录）：
    python content/build_localization.py

md 约定：
    # 标题行            分节，只起分隔作用，会被忽略
    ## lona_xxx         一个键的开头，键名只允许字母/数字/下划线
    正文                跟在键后面，直到下一个 # / ## 行
    空行                分段，输出时变成字面量 \\n

输出格式与游戏一致：键,文本；文本里的换行写成字面 \\n；
文本含逗号或双引号时整段用 CSV 引号包起来（"" 转义）。
"""

import io
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONTENT = os.path.join(ROOT, "content")
OUT_DIR = os.path.join(ROOT, "src", "AdmirerLona", "Localization")
OUT_CSV = os.path.join(OUT_DIR, "zh.csv")

FILES = [
    "lona_notes_zh.md",
    "lona_first_zh.md",
    "lona_story_zh.md",
    "lona_phone_zh.md",
]

KEY_RE = re.compile(r"^##\s+([A-Za-z0-9_]+)\s*$")


def parse_md(path):
    """返回 [(key, text), ...]，按出现顺序。"""
    entries = []
    key = None
    body = []

    def flush():
        if key and body:
            text = "\n".join(body).strip("\n")
            if text:
                entries.append((key, text))

    with io.open(path, encoding="utf-8") as handle:
        for raw in handle:
            line = raw.rstrip("\n")
            if line.startswith("# ") or line.startswith("## ") and not KEY_RE.match(line):
                flush()
                key = None
                body = []
                continue
            match = KEY_RE.match(line)
            if match:
                flush()
                key = match.group(1)
                body = []
                continue
            if key is None:
                continue
            body.append(line)
    flush()
    return entries


def to_csv_field(text):
    text = text.replace("\n", "\\n")
    if "," in text or '"' in text:
        return '"' + text.replace('"', '""') + '"'
    return text


def main():
    all_entries = []
    seen = {}

    for name in FILES:
        path = os.path.join(CONTENT, name)
        if not os.path.isfile(path):
            continue
        for key, text in parse_md(path):
            if key in seen:
                sys.stderr.write("重复的键：%s（%s 与 %s）\n" % (key, seen[key], name))
                return 1
            seen[key] = name
            all_entries.append((key, text))

    if not all_entries:
        sys.stderr.write("没有解析到任何文案\n")
        return 1

    if not os.path.isdir(OUT_DIR):
        os.makedirs(OUT_DIR)

    with io.open(OUT_CSV, "w", encoding="utf-8", newline="\n") as handle:
        handle.write("# AdmirerLona localization (zh) — generated from content/*.md\n")
        for key, text in all_entries:
            handle.write("%s,%s\n" % (key, to_csv_field(text)))

    print("生成 %s：%d 条" % (os.path.relpath(OUT_CSV, ROOT), len(all_entries)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
