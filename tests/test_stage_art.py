import hashlib
from functools import lru_cache
import importlib.util
import json
from pathlib import Path
import struct
import subprocess
import sys
import tempfile
import unittest
import zlib


ROOT = Path(__file__).resolve().parents[1]


@lru_cache(maxsize=3)
def png(width, height, rgba=False):
    """Make small valid test textures without using or editing production art."""
    def chunk(name, payload):
        return struct.pack(">I", len(payload)) + name + payload + struct.pack(">I", zlib.crc32(name + payload))

    channels = 4 if rgba else 3
    rows = (b"\0" + b"\xff" * width * channels) * height
    return (b"\x89PNG\r\n\x1a\n"
            + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6 if rgba else 2, 0, 0, 0))
            + chunk(b"IDAT", zlib.compress(rows)) + chunk(b"IEND", b""))


class StageArtTests(unittest.TestCase):
    def tool(self):
        path = ROOT / "tools/stage_art.py"
        self.assertTrue(path.is_file(), "art staging tool has not been implemented")
        spec = importlib.util.spec_from_file_location("stage_art", path)
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        return module

    def fixture(self, root):
        root = root / "source"
        mapping = json.loads((ROOT / "assets/selected-art.json").read_text())
        for group in ("cards", "characters", "relics"):
            for key, item in mapping[group].items():
                size = (1024, 1536) if group == "cards" and key == "B03" else (1254, 1254)
                data = png(*size, rgba=group == "relics")
                target = root / item["source"]
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(data)
                item["sha256"] = hashlib.sha256(data).hexdigest()
        path = root / "selected.json"
        path.write_text(json.dumps(mapping))
        return path, mapping

    def test_exact_selection_covers_catalog_and_excludes_archived_versions(self):
        self.assertTrue((ROOT / "assets/selected-art.json").is_file(), "precise art selection is missing")
        mapping = json.loads((ROOT / "assets/selected-art.json").read_text())
        known = {card["id"] for card in json.loads((ROOT / "content/cards.json").read_text())["cards"]}
        self.assertEqual(len(mapping["cards"]), 82)
        self.assertEqual(set(mapping["cards"]), known)
        self.assertEqual(mapping["cards"]["C04"]["source"], "art/masters/cards/card_C04_v005.png")
        self.assertEqual(mapping["cards"]["B01"]["source"], "art/masters/cards/card_B01_v008.png")
        self.assertEqual(set(mapping["characters"]), {"initial", "mage", "dragon"})
        self.assertEqual(set(mapping["relics"]), {"moon_eye_scythe"})
        for group in ("cards", "characters", "relics"):
            for item in mapping[group].values():
                self.assertRegex(item["sha256"], "^[a-f0-9]{64}$")
                self.assertTrue(item["source"].startswith("art/masters/"))

    def test_import_size_uses_godot_integer_rounding(self):
        tool = self.tool()
        self.assertEqual(tool.imported_size((1254, 1254), 256), (256, 256))
        self.assertEqual(tool.imported_size((1024, 1536), 256), (170, 256))
        self.assertEqual(tool.imported_size((1024, 1536), 1000), (666, 1000))
        self.assertEqual(tool.imported_size((1254, 1254), 0), (1254, 1254))

    def test_atlas_margin_preserves_image_with_exact_card_aspect(self):
        tool = self.tool()
        for size, canvas, expected in [
            ((256, 256), (350, 266), (47, 5, 94, 10)),
            ((170, 256), (350, 266), (90, 5, 180, 10)),
            ((1000, 1000), (1325, 1007), (162.5, 3.5, 325, 7)),
            ((666, 1000), (1325, 1007), (329.5, 3.5, 659, 7)),
            ((1254, 1254), (1650, 1254), (198, 0, 396, 0)),
        ]:
            with self.subTest(size=size):
                margin = tool.card_margin(size)
                self.assertEqual(margin, expected)
                self.assertEqual((size[0] + margin[2], size[1] + margin[3]), canvas)
                self.assertEqual(canvas[0] * 19, canvas[1] * 25)

    def test_validation_rejects_stale_hash_missing_card_archive_and_invalid_png(self):
        tool = self.tool()
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            selection, clean = self.fixture(root)
            for failure in ("hash", "missing", "archive", "png"):
                mapping = json.loads(json.dumps(clean))
                if failure == "hash":
                    mapping["cards"]["B01"]["sha256"] = "0" * 64
                elif failure == "missing":
                    del mapping["cards"]["B01"]
                elif failure == "archive":
                    mapping["cards"]["B01"]["source"] = "废案/卡牌/card_B01_v001.png"
                else:
                    broken = root / "source" / mapping["cards"]["B01"]["source"]
                    broken.write_bytes(b"not a png")
                    mapping["cards"]["B01"]["sha256"] = hashlib.sha256(broken.read_bytes()).hexdigest()
                selection.write_text(json.dumps(mapping))
                with self.subTest(failure=failure), self.assertRaises(ValueError):
                    tool.stage(selection, root / "source", root / "output", runtime_root=root / "runtime")
                self.assertFalse((root / "output").exists(), "validation must finish before writes")

    def test_staging_copies_original_bytes_adds_atlases_and_preserves_other_outputs(self):
        tool = self.tool()
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            selection, mapping = self.fixture(root)
            output = root / "output"
            runtime = root / "runtime"
            scene = runtime / "scenes/character_select.tscn"
            scene.parent.mkdir(parents=True)
            scene.write_text('[gd_scene format=3]\n[node name="Ivich" type="Node2D"]\n')
            sentinel = output / "Ivich/scenes/existing.tscn"
            sentinel.parent.mkdir(parents=True)
            sentinel.write_text("existing scene")
            tool.stage(selection, root / "source", output, runtime_root=runtime)
            base = output / "Ivich/images"
            for folder in ("cards", "card_thumbnails"):
                for card_id in ("B01", "B03"):
                    target = base / folder / f"{card_id.lower()}.png"
                    self.assertEqual(target.read_bytes(), (root / "source" / mapping["cards"][card_id]["source"]).read_bytes())
                settings = (base / folder / "b03.png.import").read_text()
                self.assertIn("compress/mode=0", settings)
                self.assertIn("mipmaps/generate=false", settings)
                self.assertIn(f"process/size_limit={256 if folder == 'card_thumbnails' else 1000}", settings)
            self.assertIn("process/size_limit=0", (base / "characters/initial.png.import").read_text())
            thumb = (base / "card_portraits/b03.tres").read_text()
            self.assertIn("region = Rect2(0, 0, 170, 256)", thumb)
            self.assertIn("margin = Rect2(90, 5, 180, 10)", thumb)
            large = (base / "card_portraits/big/b03.tres").read_text()
            self.assertIn("region = Rect2(0, 0, 666, 1000)", large)
            self.assertIn("margin = Rect2(329.5, 3.5, 659, 7)", large)
            self.assertIn("region = Rect2(0, 0, 1254, 1254)", (base / "card_portraits/forms/mage.tres").read_text())
            self.assertEqual((output / "Ivich/scenes/character_select.tscn").read_bytes(), scene.read_bytes())
            self.assertEqual(sentinel.read_text(), "existing scene")
            manifest = json.loads((output / "import-manifest.json").read_text())
            self.assertEqual(manifest["Ivich/images/cards/b03.png"]["sha256"], mapping["cards"]["B03"]["sha256"])
            self.assertEqual(manifest["Ivich/images/cards/b03.png"]["imported_size"], [666, 1000])
            self.assertEqual(manifest["Ivich/scenes/character_select.tscn"]["sha256"], hashlib.sha256(scene.read_bytes()).hexdigest())

    def test_check_cli_does_not_create_output(self):
        self.tool()
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            selection, _ = self.fixture(root)
            output = root / "not-created"
            result = subprocess.run([sys.executable, str(ROOT / "tools/stage_art.py"), "--source", str(root / "source"),
                                     "--mapping", str(selection), "--output", str(output), "--check"],
                                    text=True, capture_output=True)
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertIn("82", result.stdout)
            self.assertFalse(output.exists())

    def test_runtime_resources_cannot_replace_generated_card_resources(self):
        tool = self.tool()
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            selection, _ = self.fixture(root)
            runtime = root / "runtime"
            collision = runtime / "images/card_portraits/b01.tres"
            collision.parent.mkdir(parents=True)
            collision.write_text("must not overwrite generated atlas")
            with self.assertRaisesRegex(ValueError, "collision"):
                tool.stage(selection, root / "source", root / "output", runtime_root=runtime)
            self.assertFalse((root / "output").exists())

    def test_output_cannot_be_inside_original_source_tree(self):
        tool = self.tool()
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            selection, _ = self.fixture(root)
            source = root / "source"
            output = source / "accidental-output"
            with self.assertRaisesRegex(ValueError, "source tree"):
                tool.stage(selection, source, output, runtime_root=root / "runtime")
            self.assertFalse(output.exists())


if __name__ == "__main__":
    unittest.main()
