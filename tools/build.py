"""Run checks, compile against the real game, then optionally package. Never installs."""
import argparse
import os
from pathlib import Path
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]


def dotnet_path() -> str:
    path = os.environ.get("IVICH_DOTNET") or shutil.which("dotnet")
    if path:
        return path
    local = Path.home() / ".dotnet" / ("dotnet.exe" if os.name == "nt" else "dotnet")
    if local.is_file():
        return str(local)
    raise SystemExit("Install the .NET 9 SDK, or set IVICH_DOTNET to the dotnet executable.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--package", action="store_true")
    parser.add_argument("--art", action="store_true", help="Import, export and validate the staged Godot resource pack")
    parser.add_argument("--godot", help="Godot editor executable (or set IVICH_GODOT)")
    parser.add_argument("--rules-only", action="store_true", help="Run independent tests without game DLLs")
    args = parser.parse_args()
    if args.package and args.rules_only:
        parser.error("--package requires the actual mod build; omit --rules-only")
    if args.godot and not args.art:
        parser.error("--godot is used together with --art")
    dotnet = dotnet_path()
    environment = os.environ.copy()
    environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1"
    commands = [
        [sys.executable, "-m", "unittest", "discover", "-s", "tests", "-p", "test_*.py", "-v"],
        [dotnet, "run", "--project", "tests/Ivich.Core.Tests", "--configuration", "Release"],
        [dotnet, "run", "--project", "tests/Ivich.Spells.Tests", "--configuration", "Release"],
    ]
    if not args.rules_only:
        commands.append([dotnet, "build", "src/Ivich.Mod", "--configuration", "Release", "--nologo"])
        commands.append([dotnet, "run", "--project", "tests/Ivich.Mod.Smoke", "--configuration", "Release"])
    for command in commands:
        print("+ " + " ".join(command), flush=True)
        subprocess.run(command, cwd=ROOT, env=environment, check=True)
    art = None
    if args.art:
        from build_art import build_art
        art = build_art(godot=args.godot)
        print(f"Validated resource pack: {art}", flush=True)
    if args.package:
        from package_release import package
        print(package(ROOT / "src/Ivich.Mod/bin/Release/net9.0", ROOT / "dist", art_path=art))


if __name__ == "__main__":
    try:
        main()
    except subprocess.CalledProcessError as error:
        sys.exit(error.returncode)
