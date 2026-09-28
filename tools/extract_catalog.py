#!/usr/bin/env python3
"""Extract the current Ivich design tables without treating catalog entries as runtime cards."""
from __future__ import annotations

import argparse
import hashlib
import json
import re
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CARD_RE = re.compile(r"^\| ([BCURTA]\d{2}) \|")
RARITIES = {"B": "basic", "C": "common", "U": "uncommon", "R": "rare", "T": "generated", "A": "ancient"}
TYPES = {"攻击": "attack", "技能": "skill", "能力": "power"}
ENABLED = [*(f"B{i:02}" for i in range(1, 7)), *(f"C{i:02}" for i in range(1, 29)), *(f"U{i:02}" for i in range(1, 29)), *(f"R{i:02}" for i in range(1, 17)), *(f"T{i:02}" for i in range(1, 4)), "A01"]


def extract(source: Path) -> dict:
    raw = source.read_bytes()
    cards = []
    for line_no, line in enumerate(raw.decode("utf-8-sig").splitlines(), 1):
        if not CARD_RE.match(line):
            continue
        cells = [cell.strip() for cell in line.strip("|").split("|")]
        if len(cells) != 6:
            raise ValueError(f"Unexpected table width at line {line_no}: {cells!r}")
        code, name, tagged_type, costs, effect, upgrade = cells
        tags = tagged_type.split("·")
        card_type = next((TYPES[tag] for tag in tags if tag in TYPES), None)
        if card_type is None:
            raise ValueError(f"No card type for {code}")
        resources = costs.split("／")
        if len(resources) != 3:
            raise ValueError(f"Expected energy/mana/rage for {code}")
        values = ["X" if val == "X" else int(val) for val in resources]
        form = "initial_or_mage" if "魔法" in tags else "dragon" if values[2] != 0 else "any"
        cards.append({
            "id": code,
            "name_zh": name,
            "type": card_type,
            "rarity": RARITIES[code[0]],
            "tags_zh": [tag for tag in tags if tag not in TYPES and tag != "先古"],
            "cost": dict(zip(("energy", "mana", "rage"), values)),
            "form_gate": form,
            "effect_zh": effect,
            "upgrade_changes_zh": upgrade,
            "reward_pool": code[0] in "CUR",
            "source_line": line_no,
        })
    ids = [card["id"] for card in cards]
    expected = [*(f"B{i:02}" for i in range(1, 7)), *(f"C{i:02}" for i in range(1, 29)),
                *(f"U{i:02}" for i in range(1, 29)), *(f"R{i:02}" for i in range(1, 17)),
                *(f"T{i:02}" for i in range(1, 4)), "A01"]
    if ids != expected:
        raise ValueError(f"Expected 82 unique ordered card IDs, received {ids!r}")
    rewards = [card for card in cards if card["reward_pool"]]
    if Counter(card["type"] for card in rewards) != {"attack": 27, "skill": 28, "power": 17}:
        raise ValueError("Reward type counts differ from the current design")
    return {
        "schema_version": 1,
        "design_version": "cards-v0.4-mechanics-v0.5",
        "source_name": source.name,
        "source_sha256": hashlib.sha256(raw).hexdigest(),
        "notice_zh": "完整82张策划目录；运行时接入列表见 runtime-enabled.json。数值未经游戏平衡验证。",
        "starter_deck": [{"id": "B01", "count": 3}, {"id": "B02", "count": 3},
                         *({"id": f"B{i:02}", "count": 1} for i in range(3, 7)), {"id": "C07", "count": 1}],
        "cards": cards,
    }


def serialize(value: dict) -> str:
    return json.dumps(value, ensure_ascii=False, indent=2) + "\n"


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path, nargs="?", default=ROOT / "content" / "source" / "卡牌.md",
                        help="Current 卡牌.md; defaults to the checked-in source snapshot")
    parser.add_argument("--output", type=Path, default=ROOT / "content" / "cards.json")
    parser.add_argument("--check", action="store_true", help="Compare source extraction against the committed catalog")
    args = parser.parse_args()
    expected = serialize(extract(args.source))
    enabled_path = args.output.parent / "runtime-enabled.json"
    enabled = serialize({"schema_version": 1, "enabled_card_ids": ENABLED,
                         "notice_zh": "仅列出已有实际效果实现的卡牌；完整策划目录不自动加入奖励。"})
    if args.check:
        if args.output.read_text() != expected:
            raise SystemExit("Catalog differs from current source; regenerate and review changes")
        if enabled_path.read_text() != enabled:
            raise SystemExit("Runtime card IDs differ from implemented scope")
        print("Catalog verified: 82 designs, 72 reward designs, 82 enabled implementations, 11-card starter deck")
    else:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(expected)
        enabled_path.write_text(enabled)
        print(f"Wrote {args.output} and {enabled_path}")


if __name__ == "__main__":
    main()
