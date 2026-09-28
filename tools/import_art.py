"""Copy explicitly selected, game-sized PNGs to a separate Godot resource project.

Never picks a latest candidate, crops an image, or changes the art source directory.
"""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import struct

ROOT = Path(__file__).resolve().parents[1]
SIZES = {"portrait": {(250, 190), (250, 350)}, "large": {(1000, 760), (500, 380), (606, 852)}}


def prepare(mapping: Path):
    data = json.loads(mapping.read_text(encoding="utf-8"))
    known = {card["id"] for card in json.loads((ROOT / "content/cards.json").read_text())["cards"]}
    if not data.get("cards"):
        raise ValueError("The mapping must select at least one card.")
    entries = []
    for card_id, paths in data["cards"].items():
        if card_id not in known or set(paths) != {"portrait", "large"}:
            raise ValueError(f"{card_id}: supply a known ID and both portrait/large PNG paths")
        shapes = {}
        for slot, path in paths.items():
            source = Path(path).expanduser()
            if not source.is_absolute():
                source = mapping.parent / source
            source = source.resolve(strict=True)
            with source.open("rb") as file:
                header = file.read(24)
            if len(header) != 24 or header[:8] != b"\x89PNG\r\n\x1a\n" or header[12:16] != b"IHDR":
                raise ValueError(f"Not a PNG: {source}")
            size = struct.unpack(">II", header[16:24])
            if size not in SIZES[slot]:
                raise ValueError(f"{card_id}/{slot}: size {size}; prepare one of {sorted(SIZES[slot])} first")
            shapes[slot] = size[0] < size[1]
            destination = Path("Ivich/images/card_portraits") / ("big" if slot == "large" else "") / f"{card_id.lower()}.png"
            entries.append((source, destination, size))
        if shapes["portrait"] != shapes["large"]:
            raise ValueError(f"{card_id}: portrait and large must both use normal art or both use full art")
    return entries


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mapping", type=Path)
    parser.add_argument("--check", action="store_true", help="Validate selection without copying")
    args = parser.parse_args()
    entries = prepare(args.mapping.resolve())
    for source, destination, size in entries:
        print(f"{destination}: {size[0]}×{size[1]} <- {source}")
    if args.check:
        return
    output = ROOT / "assets/imported"
    output.mkdir(parents=True, exist_ok=True)
    manifest = {}
    previous = output / "import-manifest.json"
    if previous.is_file():
        manifest = json.loads(previous.read_text())
    for source, relative, size in entries:
        target = output / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, target)
        manifest[str(relative)] = {"source": str(source), "size": size,
                                   "sha256": hashlib.sha256(target.read_bytes()).hexdigest()}
    for name in ("project.godot", "export_presets.cfg"):
        target = output / name
        if not target.exists():
            shutil.copyfile(ROOT / "assets" / (name + ".template"), target)
    previous.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n")
    print(f"Staged at {output}. Import/export with MegaDot before using these resources in game.")


if __name__ == "__main__":
    main()
