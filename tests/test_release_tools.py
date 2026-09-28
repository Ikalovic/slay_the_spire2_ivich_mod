import importlib.util
import inspect
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
import zipfile
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))


class ReleaseTests(unittest.TestCase):
    def load_tool(self):
        path = ROOT / "tools/package_release.py"
        self.assertTrue(path.is_file(), "release packager has not been implemented")
        spec = importlib.util.spec_from_file_location("package_release", path)
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        return module

    def test_package_excludes_game_dependencies_and_has_matching_hashes(self):
        tool = self.load_tool()
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            build = root / "build"
            build.mkdir()
            (build / "Ivich.dll").write_bytes(b"test mod assembly")
            (build / "Ivich.Core.dll").write_bytes(b"test rules assembly")
            (build / "sts2.dll").write_bytes(b"must never redistribute game")
            (build / "BaseLib.dll").write_bytes(b"must never redistribute dependency")
            archive = tool.package(build, root / "out", ROOT)
            with zipfile.ZipFile(archive) as release:
                names = release.namelist()
                self.assertIn("Ivich/Ivich.dll", names)
                self.assertIn("Ivich/Ivich.Core.dll", names)
                self.assertFalse(any("sts2.dll" in x or "BaseLib.dll" in x for x in names))
                manifest = json.loads(release.read("Ivich/Ivich.json"))
                self.assertFalse(manifest["has_pck"])
                self.assertTrue(archive.name.endswith("-code.zip"))
                sums = release.read("Ivich/SHA256SUMS").decode()
                import hashlib
                for line in sums.splitlines():
                    digest, name = line.split("  ", 1)
                    self.assertEqual(digest, hashlib.sha256(release.read("Ivich/" + name)).hexdigest())

    def test_missing_binary_fails_before_creating_a_release(self):
        tool = self.load_tool()
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            with self.assertRaises(FileNotFoundError):
                tool.package(root, root / "out", ROOT)
            self.assertFalse((root / "out").exists())

    def test_art_manifest_cannot_publish_missing_or_unvalidated_pack(self):
        tool = self.load_tool()
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            build = root / "build"
            build.mkdir()
            (build / "Ivich.dll").write_bytes(b"mod")
            (root / "Ivich.json").write_text(json.dumps({"id": "Ivich", "version": "test", "has_pck": True}))
            with self.assertRaisesRegex((FileNotFoundError, ValueError), "Missing.*PCK|validat|proof"):
                tool.package(build, root / "out", root)
            self.assertFalse((root / "out").exists())

    def test_validated_art_package_sets_manifest_without_mutating_code_manifest(self):
        tool = self.load_tool()
        self.assertIn("art_path", inspect.signature(tool.package).parameters, "art package support is missing")
        import build_art
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            build = root / "build"
            build.mkdir()
            (build / "Ivich.dll").write_bytes(b"test assembly")
            original = json.dumps({"id": "Ivich", "version": "0.3.0", "has_pck": False})
            (root / "Ivich.json").write_text(original)
            project = root / "assets/imported"
            image = project / "Ivich/B01.png"
            image.parent.mkdir(parents=True)
            image.write_bytes(b"test source fixture")
            (project / "project.godot").write_text("config_version=5\n")
            (project / "export_presets.cfg").write_text("[preset.0]\n")
            pack = root / "Ivich.pck"
            pack.write_bytes(b"GDPC" + bytes(100))
            proof = {"schema": 1, "validator": build_art.VALIDATOR, "godot_version": "4.5.1.stable",
                     "source_sha256": build_art.source_fingerprint(project),
                     "pck_sha256": hashlib.sha256(pack.read_bytes()).hexdigest(),
                     "resources": ["res://Ivich/B01.png"],
                     "loaded_resources": [{"path": "res://Ivich/B01.png", "type": "CompressedTexture2D"}],
                     "packed_files": ["Ivich/B01.png.import", ".godot/imported/B01.ctex"]}
            build_art.proof_path(pack).write_text(json.dumps(proof))
            archive = tool.package(build, root / "out", root, art_path=pack)
            self.assertEqual("Ivich-0.3.0.zip", archive.name)
            self.assertEqual(original, (root / "Ivich.json").read_text())
            with zipfile.ZipFile(archive) as release:
                self.assertTrue(json.loads(release.read("Ivich/Ivich.json"))["has_pck"])
                self.assertEqual(pack.read_bytes(), release.read("Ivich/Ivich.pck"))
                self.assertFalse(any(name.endswith(".png") or "assets/imported" in name for name in release.namelist()))


if __name__ == "__main__":
    unittest.main()
