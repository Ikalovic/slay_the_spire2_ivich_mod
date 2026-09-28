"""Import, export and validate Ivich resources with the real Godot editor. Never installs."""
import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[1]
VALIDATOR = "ivich-godot-art-v1"
RESOURCE_SUFFIXES = {".png", ".tres", ".tscn"}


def godot_path(explicit=None) -> str:
    candidate = explicit or os.environ.get("IVICH_GODOT") or shutil.which("godot") or shutil.which("godot4")
    if not candidate:
        local = Path("/tmp/ivich-tools/Godot_v4.5.1-stable_linux.x86_64")
        if local.is_file():
            candidate = str(local)
    if not candidate:
        raise FileNotFoundError("Godot editor not found. Set IVICH_GODOT or pass --godot PATH.")
    return str(candidate)


def proof_path(pack: Path) -> Path:
    return pack.with_suffix(pack.suffix + ".validation.json")


def _source_files(project: Path):
    namespace = project / "Ivich"
    if not namespace.is_dir():
        raise FileNotFoundError(f"Missing staged resource namespace: {namespace}. Run tools/stage_art.py first.")
    for path in sorted(namespace.rglob("*")):
        if path.is_symlink():
            raise ValueError(f"Resource symlinks are not supported: {path}")
        if path.is_file():
            if path.suffix not in RESOURCE_SUFFIXES | {".import", ".uid"}:
                raise ValueError(f"Unexpected resource or script in Ivich namespace: {path}")
            yield path


def resource_paths(project: Path) -> list[str]:
    result = []
    for path in _source_files(project):
        if path.suffix not in RESOURCE_SUFFIXES:
            continue
        if path.suffix in {".tres", ".tscn"}:
            text = path.read_text(encoding="utf-8")
            if re.search(r'\btype\s*=\s*"(?:Script|CSharpScript|GDScript)"|\bscript\s*=', text):
                raise ValueError(f"Resource scenes must not depend on scripts: {path}")
            for dependency in re.findall(r'res://[^"\s]+', text):
                relative = dependency.removeprefix("res://")
                if not relative.startswith("Ivich/") or ".." in PurePosixPath(relative).parts:
                    raise ValueError(f"Resource dependency outside Ivich namespace: {dependency}")
                if not (project / relative).is_file():
                    raise FileNotFoundError(f"Missing resource dependency: {dependency}")
        result.append("res://" + path.relative_to(project).as_posix())
    if not result:
        raise ValueError("The Ivich resource project is empty")
    return result


def source_fingerprint(project: Path) -> str:
    resource_paths(project)
    files = list(_source_files(project)) + [project / "project.godot", project / "export_presets.cfg"]
    digest = hashlib.sha256()
    for path in sorted(files):
        if not path.is_file():
            raise FileNotFoundError(f"Missing resource project configuration: {path}")
        digest.update(path.relative_to(project).as_posix().encode("utf-8") + b"\0")
        digest.update(hashlib.sha256(path.read_bytes()).digest())
    return digest.hexdigest()


def _pack_digest(pack: Path) -> str:
    if not pack.is_file():
        raise FileNotFoundError(f"Missing validated PCK: {pack}. Run tools/build.py --art first.")
    with pack.open("rb") as stream:
        if stream.read(4) != b"GDPC" or pack.stat().st_size < 96:
            raise ValueError(f"Invalid PCK header or truncated PCK: {pack}")
        stream.seek(0)
        return hashlib.file_digest(stream, "sha256").hexdigest()


def _check_pack_files(files):
    if not isinstance(files, list) or not files:
        raise ValueError("Validation proof has no packed resource inventory")
    metadata = {"project.binary", ".godot/uid_cache.bin", ".godot/global_script_class_cache.cfg"}
    for name in files:
        if not isinstance(name, str) or ".." in PurePosixPath(name).parts or "\\" in name:
            raise ValueError(f"Unexpected packed path: {name!r}")
        if name.endswith((".cs", ".gd", ".gdc", ".dll")):
            raise ValueError(f"Unexpected script or assembly in resource pack: {name}")
        allowed = name.startswith("Ivich/") or name in metadata or re.fullmatch(r"\.godot/(?:imported|exported)/.+\.(?:ctex|res|scn|md5)", name)
        if not allowed:
            raise ValueError(f"Unexpected foreign namespace in resource pack: {name}")


def require_validated_art(project: Path, pack: Path) -> dict:
    pack_hash = _pack_digest(pack)
    certificate = proof_path(pack)
    if not certificate.is_file():
        raise FileNotFoundError(f"Missing Godot validation proof: {certificate}")
    try:
        record = json.loads(certificate.read_text(encoding="utf-8"))
    except (ValueError, UnicodeError) as error:
        raise ValueError(f"Invalid Godot validation proof: {certificate}") from error
    if not isinstance(record, dict) or record.get("schema") != 1 or record.get("validator") != VALIDATOR or not record.get("godot_version"):
        raise ValueError("Unsupported or incomplete Godot validation proof")
    if record.get("pck_sha256") != pack_hash:
        raise ValueError("PCK hash differs from the pack validated by Godot; rebuild --art")
    if record.get("source_sha256") != source_fingerprint(project):
        raise ValueError("Stale PCK: resource source or import settings changed; rebuild --art")
    resources = resource_paths(project)
    if record.get("resources") != resources:
        raise ValueError("Validation proof does not match the current resource inventory")
    loaded = record.get("loaded_resources", [])
    if not isinstance(loaded, list) or not all(isinstance(item, dict) for item in loaded) or sorted(item.get("path", "") for item in loaded) != sorted(resources):
        raise ValueError("Not every resource was loaded by the Godot validator")
    _check_pack_files(record.get("packed_files"))
    return record


VALIDATION_SCRIPT = '''extends SceneTree

func files_below(path: String) -> Array:
    var result: Array = []
    var directory := DirAccess.open(path)
    if directory == null:
        return result
    directory.include_hidden = true
    directory.list_dir_begin()
    var name := directory.get_next()
    while name != "":
        if name != "." and name != "..":
            var child := path.path_join(name)
            if directory.current_is_dir():
                result.append_array(files_below(child))
            else:
                result.append(child.trim_prefix("res://"))
        name = directory.get_next()
    directory.list_dir_end()
    return result

func script_free(node: Node) -> bool:
    if node.get_script() != null:
        return false
    for child in node.get_children():
        if not script_free(child):
            return false
    return true

func fail(message: String) -> void:
    push_error(message)
    quit(1)

func _initialize() -> void:
    var request = JSON.parse_string(FileAccess.get_file_as_string(OS.get_cmdline_user_args()[0]))
    var before := files_below("res://")
    if not ProjectSettings.load_resource_pack(request.pack):
        fail("Cannot mount the exported Ivich PCK")
        return
    var inventory: Array = []
    for path in files_below("res://"):
        if path not in before:
            inventory.append(path)
    inventory.sort()
    var loaded: Array = []
    for path in request.resources:
        var resource := ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE)
        if resource == null:
            fail("Cannot load packed resource: " + path)
            return
        var item := {"path": path, "type": resource.get_class()}
        if resource is Texture2D:
            if resource.get_width() <= 0 or resource.get_height() <= 0:
                fail("Packed texture has no dimensions: " + path)
                return
            item.width = resource.get_width()
            item.height = resource.get_height()
        if resource is AtlasTexture and resource.atlas == null:
            fail("AtlasTexture has no atlas: " + path)
            return
        if resource is PackedScene:
            var node: Node = (resource as PackedScene).instantiate()
            if node == null or not script_free(node):
                fail("Packed scene cannot instantiate without scripts: " + path)
                return
            node.free()
        loaded.append(item)
    var output := FileAccess.open(request.report, FileAccess.WRITE)
    if output == null:
        fail("Cannot write validation report")
        return
    output.store_string(JSON.stringify({"loaded_resources": loaded, "packed_files": inventory}, "  "))
    output.close()
    print("IVICH_ART_VALIDATED " + str(loaded.size()))
    quit(0)
'''


def _run(command: list[str], project: Path):
    print("+ " + " ".join(command), flush=True)
    subprocess.run(command, cwd=project, check=True)


def validate_pack(godot: str, project: Path, pack: Path) -> dict:
    _pack_digest(pack)
    resources = resource_paths(project)
    with tempfile.TemporaryDirectory(prefix="ivich-pck-validation-") as temporary:
        isolated = Path(temporary)
        (isolated / "project.godot").write_text('config_version=5\n[application]\nconfig/name="Ivich PCK Validation"\n[rendering]\nrenderer/rendering_method="gl_compatibility"\n')
        (isolated / "validate.gd").write_text(VALIDATION_SCRIPT, encoding="utf-8")
        report = isolated / "report.json"
        request = isolated / "request.json"
        request.write_text(json.dumps({"pack": str(pack.resolve()), "resources": resources, "report": str(report)}))
        _run([godot, "--headless", "--path", str(isolated), "--script", "validate.gd", "--", str(request)], isolated)
        if not report.is_file():
            raise ValueError("Godot exited without a complete resource validation report")
        result = json.loads(report.read_text())
        if sorted(item["path"] for item in result.get("loaded_resources", [])) != sorted(resources):
            raise ValueError("Godot did not load every requested resource")
        _check_pack_files(result.get("packed_files"))
        return result


def build_art(project: Path = ROOT / "assets/imported", output: Path = ROOT / "artifacts/art/Ivich.pck", godot=None, project_root: Path = ROOT) -> Path:
    project = project.resolve()
    output = output.resolve()
    executable = godot_path(godot)
    resources = resource_paths(project)
    if not (project / "project.godot").is_file():
        raise FileNotFoundError(f"Missing Godot project: {project / 'project.godot'}")
    preset = (project_root / "assets/export_presets.cfg.template").read_text()
    preset = preset.replace("export_files=PackedStringArray()", "export_files=PackedStringArray(" + ", ".join(json.dumps(path) for path in resources) + ")")
    (project / "export_presets.cfg").write_text(preset, encoding="utf-8")
    _run([executable, "--headless", "--path", str(project), "--editor", "--import"], project)
    source_hash = source_fingerprint(project)
    version = subprocess.run([executable, "--version"], check=True, capture_output=True, text=True).stdout.strip()
    output.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="ivich-art-", dir=output.parent) as temporary:
        pending = Path(temporary) / "Ivich.pck"
        _run([executable, "--headless", "--path", str(project), "--export-pack", "Resources", str(pending)], project)
        result = validate_pack(executable, project, pending)
        if source_fingerprint(project) != source_hash:
            raise ValueError("Resource source changed during export/validation; rebuild --art")
        record = dict(result, schema=1, validator=VALIDATOR, godot_version=version,
                      pck_sha256=_pack_digest(pending), source_sha256=source_hash, resources=resource_paths(project))
        pending_proof = proof_path(pending)
        pending_proof.write_text(json.dumps(record, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        require_validated_art(project, pending)
        pending.replace(output)
        pending_proof.replace(proof_path(output))
    return output


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--godot")
    parser.add_argument("--project-dir", type=Path, default=ROOT / "assets/imported")
    parser.add_argument("--output", type=Path, default=ROOT / "artifacts/art/Ivich.pck")
    args = parser.parse_args()
    print(build_art(args.project_dir, args.output, args.godot))
