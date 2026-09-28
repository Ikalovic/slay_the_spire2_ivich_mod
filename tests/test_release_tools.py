import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
import zipfile

ROOT = Path(__file__).resolve().parents[1]


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


if __name__ == "__main__":
    unittest.main()
