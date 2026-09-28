"""Integrity tests for the design catalog and the complete runtime surface."""
import importlib.util
import json
import re
import unittest
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("extract_catalog", ROOT / "tools" / "extract_catalog.py")
extract_catalog = importlib.util.module_from_spec(spec)
spec.loader.exec_module(extract_catalog)


class CatalogTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.catalog = json.loads((ROOT / "content" / "cards.json").read_text())
        cls.cards = cls.catalog["cards"]
        cls.by_id = {card["id"]: card for card in cls.cards}
        cls.enabled = json.loads((ROOT / "content" / "runtime-enabled.json").read_text())["enabled_card_ids"]

    def test_exact_source_fidelity(self):
        self.assertEqual(self.catalog, extract_catalog.extract(ROOT / "content" / "source" / "卡牌.md"))

    def test_complete_catalog_and_reward_breakdown(self):
        self.assertEqual(len(self.cards), 82)
        self.assertEqual(len(self.by_id), 82)
        self.assertEqual(Counter(card["id"][0] for card in self.cards), dict(B=6, C=28, U=28, R=16, T=3, A=1))
        rewards = [card for card in self.cards if card["reward_pool"]]
        self.assertEqual(len(rewards), 72)
        self.assertEqual(Counter(card["type"] for card in rewards), dict(attack=27, skill=28, power=17))
        self.assertEqual(sum("魔法" in card["tags_zh"] for card in rewards), 35)

    def test_source_effects_and_upgrade_differences_are_preserved(self):
        for card in self.cards:
            self.assertTrue(card["effect_zh"], card["id"])
            self.assertTrue(card["upgrade_changes_zh"], card["id"])
            self.assertGreater(card["source_line"], 0)
        self.assertEqual(self.by_id["B01"]["upgrade_changes_zh"], "伤害9。")
        self.assertIn("目标死亡后", self.by_id["R15"]["effect_zh"])
        self.assertIn("首次", self.by_id["T03"]["upgrade_changes_zh"])

    def test_x_cost_resource_is_unambiguous(self):
        x_cards = {card["id"]: resource for card in self.cards for resource, cost in card["cost"].items() if cost == "X"}
        self.assertEqual(x_cards, dict(R13="energy", R14="mana", R15="rage"))
        self.assertEqual(self.by_id["R13"]["cost"]["mana"], 3)
        for card in self.cards:
            self.assertEqual(set(card["cost"]), {"energy", "mana", "rage"})
            for cost in card["cost"].values():
                self.assertTrue(cost == "X" or isinstance(cost, int) and cost >= 0)

    def test_form_gates_survive_zero_cost_changes(self):
        self.assertEqual(self.by_id["B05"]["form_gate"], "initial_or_mage")
        self.assertEqual(self.by_id["C10"]["form_gate"], "dragon")
        self.assertEqual(self.by_id["T01"]["form_gate"], "any")
        self.assertEqual(self.by_id["C06"]["form_gate"], "any")
        for card in self.cards:
            if "魔法" in card["tags_zh"]:
                self.assertEqual(card["form_gate"], "initial_or_mage")

    def test_starter_is_eleven_cards_and_contains_no_rage_cost(self):
        deck = self.catalog["starter_deck"]
        self.assertEqual(sum(card["count"] for card in deck), 11)
        self.assertEqual({card["id"]: card["count"] for card in deck},
                         dict(B01=3, B02=3, B03=1, B04=1, B05=1, B06=1, C07=1))
        self.assertTrue(all(self.by_id[card["id"]]["cost"]["rage"] == 0 for card in deck))

    def test_each_form_has_an_enabled_shop_power(self):
        enabled_powers = [self.by_id[card] for card in self.enabled if self.by_id[card]["type"] == "power"]
        for form in ("initial_or_mage", "dragon"):
            self.assertTrue(any(card["form_gate"] in ("any", form) for card in enabled_powers), form)

    def test_enabled_ids_match_actual_card_classes(self):
        source = "\n".join(path.read_text() for path in (ROOT / "src" / "Ivich.Mod" / "Cards").glob("*.cs"))
        actual = re.findall(r'public sealed class \w+\(\) : IvichCard\("([BCURTA]\d{2})"\)', source)
        self.assertEqual(len(actual), 82)
        self.assertEqual(len(set(actual)), 82)
        self.assertEqual(set(actual), set(self.enabled))
        self.assertEqual(set(self.enabled), set(extract_catalog.ENABLED))
        definitions = re.findall(r'new\("([BCURTA]\d{2})",',
                                 "\n".join(path.read_text() for path in (ROOT / "src" / "Ivich.Mod" / "Cards").glob("*CardDefinitions.cs")))
        self.assertEqual(set(actual), set(definitions))
        self.assertEqual(len(definitions), 82)
        self.assertEqual(set(self.enabled), set(self.by_id))


if __name__ == "__main__":
    unittest.main()
