"""Keeps first-party assembly definitions on GUID references ("Use GUIDs" in the asmdef Inspector).

    python Tools/asmdef_guids.py          # convert name references to GUID references
    python Tools/asmdef_guids.py --check  # fail when any first-party asmdef still references by name

Names resolve through the .meta files of every asmdef in Packages/ and Library/PackageCache/, so it runs without the
Editor. Optional packages a project may not install resolve through EXTERNAL; any other reference to an assembly that
is not installed stays a name and is reported.
"""
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
FIRST_PARTY = [p for p in (ROOT / "Packages").glob("com.threadforge.threadlink*/**/*.asmdef")] + list((ROOT / "Assets/Threadforge").rglob("*.asmdef"))
# Assemblies of optional packages a project may not install, by the GUID of their published asmdef .meta. Unity
# resolves the reference once the package is present. Steamworks.NET keeps this GUID from 2024.8.0 onwards.
EXTERNAL = {"com.rlabrecque.steamworks.net": "68bd7fdb68ef2684e982e8a9825b18a5"}


def guid_of(asmdef: Path):
    meta = Path(str(asmdef) + ".meta")
    if not meta.exists():
        return None
    match = re.search(r"^guid:\s*([0-9a-f]{32})", meta.read_text(encoding="utf-8"), re.M)
    return match.group(1) if match else None


def known_assemblies():
    found = {}
    for base in [ROOT / "Packages", ROOT / "Library/PackageCache", ROOT / "Assets"]:
        if not base.exists():
            continue
        for asmdef in base.rglob("*.asmdef"):
            if "Samples~" in asmdef.parts:
                continue
            try:
                name = json.loads(asmdef.read_text(encoding="utf-8-sig"))["name"]
            except (ValueError, KeyError):
                continue
            guid = guid_of(asmdef)
            if guid:
                found.setdefault(name, guid)
    for name, guid in EXTERNAL.items():
        found.setdefault(name, guid)
    return found


def main():
    check = "--check" in sys.argv
    names = known_assemblies()
    unresolved, changed, named = [], [], []
    for asmdef in FIRST_PARTY:
        text = asmdef.read_text(encoding="utf-8-sig")
        data = json.loads(text)
        refs = data.get("references", [])
        pending = [ref for ref in refs if not ref.startswith("GUID:")]
        if not pending:
            continue
        if check:
            named.append(f"{asmdef.relative_to(ROOT)}: {', '.join(pending)}")
            continue
        start = text.index('"references"')
        open_bracket = text.index("[", start)
        close_bracket = text.index("]", open_bracket)
        block = text[open_bracket:close_bracket]
        for ref in pending:
            if ref in names:
                block = block.replace('"' + ref + '"', '"GUID:' + names[ref] + '"')
            else:
                unresolved.append(f"{asmdef.relative_to(ROOT)}: {ref}")
        new = text[:open_bracket] + block + text[close_bracket:]
        if new != text:
            asmdef.write_text(new, encoding="utf-8")
            changed.append(str(asmdef.relative_to(ROOT)))
    if check:
        if named:
            print("Assembly definitions with name references (run python Tools/asmdef_guids.py):")
            print("\n".join("  " + item for item in named))
            sys.exit(1)
        print(f"OK: {len(FIRST_PARTY)} assembly definitions use GUID references.")
        return
    print(f"Converted {len(changed)} assembly definition(s).")
    for item in unresolved:
        print("  kept by name (not installed; add it to EXTERNAL if it is optional): " + item)


if __name__ == "__main__":
    main()
