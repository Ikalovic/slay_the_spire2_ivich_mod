"""Stage the pinned original PNGs and Godot display resources without editing art.

The PNGs stay byte-for-byte identical to their sources. Godot's lossless texture
import supplies the display resolutions; AtlasTexture.margin supplies transparent
letterboxing at display time. No raster crop, resample, or redraw runs here.
"""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import shutil
import struct


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_MAPPING = ROOT / "assets/selected-art.json"
DEFAULT_RUNTIME = ROOT / "assets/runtime/Ivich"
PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"


def imported_size(size, limit):
    """Mirror Godot's integer division in ResourceImporterTexture::import.

    https://github.com/godotengine/godot/blob/4.5/editor/import/resource_importer_texture.cpp
    """
    width, height = size
    if limit <= 0 or max(size) <= limit:
        return width, height
    if width >= height:
        return limit, height * limit // width
    return width * limit // height, limit


def card_margin(size):
    """Center the complete image in the smallest integer 25:19 canvas.

    AtlasTexture reports integer dimensions, so a fractional-width canvas would
    lose the exact aspect ratio. The small vertical margin avoids that rounding.
    Rect2 position is the left/top inset; size is the total added width/height.
    """
    width, height = size
    unit = max((width + 24) // 25, (height + 18) // 19)
    extra_width, extra_height = unit * 25 - width, unit * 19 - height
    return extra_width / 2, extra_height / 2, extra_width, extra_height


def _png_size(source):
    with source.open("rb") as file:
        header = file.read(33)
    if (len(header) != 33 or header[:8] != PNG_SIGNATURE
            or header[8:16] != b"\x00\x00\x00\rIHDR"):
        raise ValueError(f"Invalid PNG header: {source}")
    size = struct.unpack(">II", header[16:24])
    if min(size) <= 0:
        raise ValueError(f"Invalid PNG dimensions: {source}")
    return size


def prepare(mapping, source_root):
    """Validate the complete selection before any staging directory is touched."""
    data = json.loads(Path(mapping).read_text(encoding="utf-8"))
    source_root = Path(source_root).resolve(strict=True)
    known = {card["id"] for card in json.loads((ROOT / "content/cards.json").read_text())["cards"]}
    required = {"cards": known, "characters": {"initial", "mage", "dragon"},
                "relics": {"moon_eye_scythe"}}
    if data.get("schema_version") != 1:
        raise ValueError("Unsupported art mapping schema_version")
    entries = []
    for group, expected in required.items():
        if not isinstance(data.get(group), dict) or set(data[group]) != expected:
            actual = set(data.get(group, {}))
            raise ValueError(f"{group} coverage mismatch: missing={sorted(expected - actual)}, extra={sorted(actual - expected)}")
        for key, item in sorted(data[group].items()):
            relative = item.get("source", "")
            path = PurePosixPath(relative)
            directory = "cards" if group == "cards" else "characters"
            if (path.is_absolute() or ".." in path.parts or "\\" in relative
                    or path.parts[:3] != ("art", "masters", directory) or len(path.parts) != 4
                    or path.suffix != ".png"):
                raise ValueError(f"{group}/{key}: source must be a PNG directly inside art/masters/{directory}; archive paths are forbidden")
            if group == "cards" and not re.fullmatch(rf"card_{re.escape(key)}_v\d{{3}}\.png", path.name):
                raise ValueError(f"{key}: card source filename does not match its ID")
            source = (source_root / relative).resolve(strict=True)
            if not source.is_relative_to(source_root / "art/masters" / directory):
                raise ValueError(f"{key}: resolved source escapes art/masters/{directory}")
            digest = hashlib.sha256(source.read_bytes()).hexdigest()
            if digest != item.get("sha256"):
                raise ValueError(f"{group}/{key}: SHA-256 mismatch for {relative}")
            size = _png_size(source)
            slots = [(f"images/cards/{key.lower()}.png", 1000),
                     (f"images/card_thumbnails/{key.lower()}.png", 256)] if group == "cards" else [
                         (f"images/{group}/{key}.png", 0)]
            for destination, limit in slots:
                entries.append({"source_path": source, "source": relative, "sha256": digest,
                                "size": size, "destination": f"Ivich/{destination}",
                                "size_limit": limit, "imported_size": imported_size(size, limit)})
    return entries


def _atlas(texture, size):
    numbers = lambda values: ", ".join(f"{value:g}" for value in values)
    return ('[gd_resource type="AtlasTexture" load_steps=2 format=3]\n\n'
            f'[ext_resource type="Texture2D" path="res://{texture}" id="1"]\n\n'
            '[resource]\n'
            'atlas = ExtResource("1")\n'
            f'region = Rect2(0, 0, {size[0]}, {size[1]})\n'
            f'margin = Rect2({numbers(card_margin(size))})\n'
            'filter_clip = true\n')


def _import_settings(path, limit):
    """Preserve Godot's remap/UID metadata when restaging an imported project."""
    text = path.read_text(encoding="utf-8") if path.exists() else (
        '[remap]\nimporter="texture"\ntype="CompressedTexture2D"\n\n[params]\n')
    marker = "[params]"
    if marker not in text:
        text += "\n[params]\n"
    header, params = text.split(marker, 1)
    settings = {"compress/mode": "0", "mipmaps/generate": "false",
                "process/size_limit": str(limit), "detect_3d/compress_to": "0",
                "process/fix_alpha_border": "false", "process/premult_alpha": "false"}
    for name, value in settings.items():
        pattern = rf"(?m)^{re.escape(name)}=.*$"
        if re.search(pattern, params):
            params = re.sub(pattern, f"{name}={value}", params)
        else:
            params = params.rstrip() + f"\n{name}={value}\n"
    return header + marker + params


def stage(mapping, source_root, output, *, check=False, runtime_root=DEFAULT_RUNTIME):
    entries = prepare(mapping, source_root)
    output = Path(output).resolve()
    if output.is_relative_to(Path(source_root).resolve()):
        raise ValueError("Staging output must be outside the original source tree")
    generated = {}
    for entry in entries:
        destination = entry["destination"]
        filename = Path(destination).stem
        if "/card_thumbnails/" in destination:
            generated[f"Ivich/images/card_portraits/{filename}.tres"] = _atlas(destination, entry["imported_size"])
        elif "/cards/" in destination:
            generated[f"Ivich/images/card_portraits/big/{filename}.tres"] = _atlas(destination, entry["imported_size"])
        elif "/characters/" in destination and filename in {"mage", "dragon"}:
            generated[f"Ivich/images/card_portraits/forms/{filename}.tres"] = _atlas(destination, entry["imported_size"])
    reserved = set(generated)
    for entry in entries:
        reserved.update({entry["destination"], entry["destination"] + ".import"})
    runtime = []
    runtime_root = Path(runtime_root).resolve()
    if runtime_root.exists():
        for path in sorted(runtime_root.rglob("*")):
            if not path.is_file():
                continue
            if not path.resolve().is_relative_to(runtime_root):
                raise ValueError(f"Runtime source escapes its directory: {path}")
            relative = path.relative_to(runtime_root)
            destination = "Ivich/" + relative.as_posix()
            if destination in reserved:
                raise ValueError(f"Runtime resource collision with generated art: {destination}")
            runtime.append((path, destination))
    if check:
        return entries

    output.mkdir(parents=True, exist_ok=True)
    manifest_path = output / "import-manifest.json"
    manifest = json.loads(manifest_path.read_text()) if manifest_path.exists() else {}
    for entry in entries:
        destination = entry["destination"]
        target = output / destination
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(entry["source_path"], target)
        if hashlib.sha256(target.read_bytes()).hexdigest() != entry["sha256"]:
            raise ValueError(f"Source changed during staging: {entry['source']}")
        settings_path = target.with_suffix(".png.import")
        settings_path.write_text(_import_settings(settings_path, entry["size_limit"]), encoding="utf-8")
        manifest[destination] = {key: value for key, value in entry.items() if key not in {"source_path", "destination"}}
    for destination, text in generated.items():
        target = output / destination
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(text, encoding="utf-8")
        manifest[destination] = {"generated_by": "tools/stage_art.py",
                                 "sha256": hashlib.sha256(target.read_bytes()).hexdigest()}
    for source, destination in runtime:
        target = output / destination
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, target)
        manifest[destination] = {"source": "assets/runtime/Ivich/" + source.relative_to(runtime_root).as_posix(),
                                 "sha256": hashlib.sha256(target.read_bytes()).hexdigest()}
    for name in ("project.godot", "export_presets.cfg"):
        shutil.copyfile(ROOT / "assets" / f"{name}.template", output / name)
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return entries


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, default=Path("/home/ika/temp/slay_mod"), help="Root containing the original art/masters directory")
    parser.add_argument("--mapping", type=Path, default=DEFAULT_MAPPING)
    parser.add_argument("--output", type=Path, default=ROOT / "assets/imported")
    parser.add_argument("--check", action="store_true", help="Validate pinned hashes, PNG headers, and complete coverage without copying")
    args = parser.parse_args()
    try:
        entries = stage(args.mapping, args.source, args.output, check=args.check)
    except (ValueError, OSError, KeyError, TypeError) as error:
        parser.exit(1, f"Art staging failed: {error}\n")
    action = "Validated" if args.check else "Staged"
    print(f"{action} 82 cards, 3 character masters, and 1 relic source ({len(entries)} PNG destinations).")
    if not args.check:
        print(f"Original PNGs copied unchanged to {args.output}; Godot import supplies display sizes.")


if __name__ == "__main__":
    main()
