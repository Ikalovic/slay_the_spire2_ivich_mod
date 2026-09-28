"""Packaging must use the exact source and PCK checked by the real Godot validator."""
import hashlib
import importlib.util
import json
from pathlib import Path
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))


def load_art():
    path = ROOT / "tools/build_art.py"
    if not path.is_file():
        raise AssertionError("The independent Godot resource pipeline has not been implemented")
    spec = importlib.util.spec_from_file_location("build_art", path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class ArtBuildTests(unittest.TestCase):
    def fixture(self, directory):
        root = Path(directory)
        project = root / "assets/imported"
        image = project / "Ivich/images/cards/B01.png"
        image.parent.mkdir(parents=True)
        image.write_bytes(b"source image fixture")
        (project / "project.godot").write_text("config_version=5\n")
        (project / "export_presets.cfg").write_text("[preset.0]\nname=\"Resources\"\n")
        pack = root / "artifacts/art/Ivich.pck"
        pack.parent.mkdir(parents=True)
        pack.write_bytes(b"GDPC" + bytes(100))
        return project, pack, image

    def proof(self, tool, project, pack):
        # Certificate fixture only: build_art produces this envelope after Godot has actually loaded it.
        record = {
            "schema": 1, "validator": "ivich-godot-art-v1", "godot_version": "4.5.1.stable",
            "pck_sha256": hashlib.sha256(pack.read_bytes()).hexdigest(),
            "source_sha256": tool.source_fingerprint(project),
            "resources": tool.resource_paths(project),
            "packed_files": ["Ivich/images/cards/B01.png.import", ".godot/imported/B01.png-fixture.ctex"],
            "loaded_resources": [{"path": "res://Ivich/images/cards/B01.png", "type": "CompressedTexture2D", "width": 256, "height": 256}],
        }
        tool.proof_path(pack).write_text(json.dumps(record))
        return record

    def test_missing_validation_record_is_rejected(self):
        tool = load_art()
        with tempfile.TemporaryDirectory() as temporary:
            project, pack, _ = self.fixture(temporary)
            with self.assertRaisesRegex((ValueError, FileNotFoundError), "validat|proof"):
                tool.require_validated_art(project, pack)

    def test_pack_and_source_changes_invalidate_previous_engine_proof(self):
        tool = load_art()
        with tempfile.TemporaryDirectory() as temporary:
            project, pack, image = self.fixture(temporary)
            self.proof(tool, project, pack)
            tool.require_validated_art(project, pack)
            image.write_bytes(b"new artwork")
            with self.assertRaisesRegex(ValueError, "source|stale"):
                tool.require_validated_art(project, pack)
            self.proof(tool, project, pack)
            pack.write_bytes(b"GDPC" + b"different pack" * 10)
            with self.assertRaisesRegex(ValueError, "PCK|digest|hash"):
                tool.require_validated_art(project, pack)

    def test_import_settings_are_part_of_source_fingerprint(self):
        tool = load_art()
        with tempfile.TemporaryDirectory() as temporary:
            project, _, image = self.fixture(temporary)
            settings = image.with_suffix(".png.import")
            settings.write_text("[params]\nprocess/size_limit=256\n")
            previous = tool.source_fingerprint(project)
            settings.write_text("[params]\nprocess/size_limit=1000\n")
            self.assertNotEqual(previous, tool.source_fingerprint(project))

    def test_incomplete_load_proof_and_foreign_namespace_are_rejected(self):
        tool = load_art()
        with tempfile.TemporaryDirectory() as temporary:
            project, pack, _ = self.fixture(temporary)
            record = self.proof(tool, project, pack)
            record["loaded_resources"] = []
            tool.proof_path(pack).write_text(json.dumps(record))
            with self.assertRaisesRegex(ValueError, "loaded|resource"):
                tool.require_validated_art(project, pack)
            record = self.proof(tool, project, pack)
            record["packed_files"].append("OtherMod/secret.png")
            tool.proof_path(pack).write_text(json.dumps(record))
            with self.assertRaisesRegex(ValueError, "namespace|foreign|unexpected"):
                tool.require_validated_art(project, pack)

    def test_scripted_scenes_and_cross_project_dependencies_are_rejected(self):
        tool = load_art()
        with tempfile.TemporaryDirectory() as temporary:
            project, _, _ = self.fixture(temporary)
            scene = project / "Ivich/actor.tscn"
            scene.write_text('[gd_scene load_steps=2 format=3]\n[ext_resource type="Script" path="res://Other/actor.gd" id="1"]\n')
            with self.assertRaisesRegex(ValueError, "script|namespace|dependency"):
                tool.resource_paths(project)


if __name__ == "__main__":
    unittest.main()
