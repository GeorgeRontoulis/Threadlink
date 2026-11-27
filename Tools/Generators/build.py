#!/usr/bin/env python3
"""Builds the Threadlink source generator and copies it into com.threadforge.threadlink/Runtime/Generators.

Usage: python Tools/Generators/build.py [--offline]
--offline restores only from the local NuGet package cache.
"""

import shutil
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PROJECT = ROOT / "Tools/Generators/Threadlink.Generators/Threadlink.Generators.csproj"
TARGET = ROOT / "Packages/com.threadforge.threadlink/Runtime/Generators/Threadforge.Threadlink.Generators.dll"


def main():
    restore = ["dotnet", "restore", str(PROJECT)]
    if "--offline" in sys.argv:
        restore += ["--source", str(Path.home() / ".nuget/packages")]
    subprocess.run(restore, check=True)
    subprocess.run(["dotnet", "build", str(PROJECT), "-c", "Release", "--no-restore", "-nologo"], check=True)
    built = PROJECT.parent / "bin/Release/netstandard2.0/Threadforge.Threadlink.Generators.dll"
    TARGET.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(built, TARGET)
    print("copied", TARGET.relative_to(ROOT))


if __name__ == "__main__":
    main()
