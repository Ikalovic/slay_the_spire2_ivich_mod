"""Package only the mod's assemblies, manifest and documentation."""
import argparse
import hashlib
import json
from pathlib import Path
import zipfile

ROOT = Path(__file__).resolve().parents[1]


def package(build_dir: Path, output_dir: Path, project_root: Path = ROOT) -> Path:
    manifest_bytes = (project_root / "Ivich.json").read_bytes()
    manifest = json.loads(manifest_bytes)
    binary = build_dir / "Ivich.dll"
    if not binary.is_file() or binary.stat().st_size == 0:
        raise FileNotFoundError(f"Missing compiled mod: {binary}. Run tools/build.py first.")
    if manifest["has_pck"]:
        raise ValueError("This packager handles the code preview only; a resource release needs a validated PCK.")
    files = {"Ivich.dll": binary.read_bytes(), "Ivich.json": manifest_bytes}
    core = build_dir / "Ivich.Core.dll"
    if core.is_file():
        files[core.name] = core.read_bytes()
    for name in ("README.md", "docs/implementation-status.md", "docs/playtest-checklist.md",
                 "docs/art-integration.md", "docs/verification.md", "docs/reference/dependencies.md"):
        source = project_root / name
        if source.is_file():
            files[name] = source.read_bytes()
    files["SHA256SUMS"] = ("\n".join(
        f"{hashlib.sha256(data).hexdigest()}  {name}"
        for name, data in sorted(files.items())
    ) + "\n").encode()
    output_dir.mkdir(parents=True, exist_ok=True)
    output = output_dir / f"Ivich-{manifest['version']}.zip"
    temporary = output.with_suffix(".zip.tmp")
    try:
        with zipfile.ZipFile(temporary, "w", zipfile.ZIP_DEFLATED) as archive:
            for name, data in sorted(files.items()):
                info = zipfile.ZipInfo("Ivich/" + name, date_time=(2026, 9, 28, 0, 0, 0))
                info.compress_type = zipfile.ZIP_DEFLATED
                info.external_attr = 0o644 << 16
                archive.writestr(info, data)
        temporary.replace(output)
    finally:
        temporary.unlink(missing_ok=True)
    return output


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--build-dir", type=Path, default=ROOT / "src/Ivich.Mod/bin/Release/net9.0")
    parser.add_argument("--output-dir", type=Path, default=ROOT / "dist")
    args = parser.parse_args()
    print(package(args.build_dir, args.output_dir).resolve())
