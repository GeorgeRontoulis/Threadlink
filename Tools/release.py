#!/usr/bin/env python3
"""Threadlink release tooling.

Every first-party package shares one version. This tool applies that version and the package metadata, regenerates
the starter compatibility set and the Workspace Hub catalog, checks the result, and packs registry-ready archives.

Usage (from the repository root):
    python Tools/release.py apply 1.0.0        # version, metadata, LICENSE.md, compatibility set, Hub catalog
    python Tools/release.py check              # fails on any inconsistency
    python Tools/release.py pack OUTPUT_DIR    # one npm-style .tgz per package (registry or offline install)
    python Tools/release.py openupm OUTPUT_DIR # OpenUPM metadata files, one per package
    python Tools/release.py tags               # the Git tag of each package's current version

Requires Python 3.8+ and nothing else.
"""

import json
import re
import sys
import tarfile
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PACKAGES = ROOT / "Packages"
PREFIX = "com.threadforge.threadlink"
REPOSITORY = "https://github.com/GeorgeRontoulis/Threadlink"
BRANCH = "main"
DOCS = "https://threadforge.gitbook.io/threadlink-documentation/"
LICENSE_SOURCE = ROOT / "Assets/Threadforge/LICENSE"
UNITY = "6000.6"
# Third-party distributions keep their upstream versions and licenses.
THIRD_PARTY = {"com.threadforge.dependencies.messagepack"}
# OpenUPM topic slugs (github.com/openupm/openupm data/topics.yml) and the GitHub user who submits the packages.
TOPICS = {
    "": ["frameworks", "code-generation"],
    "nexus": ["frameworks"],
    "aura": ["audio"],
    "dextra": ["input-management", "gui"],
    "sentinel": ["services", "data-management"],
    "sentinel.steam": ["integration", "services"],
    "deterministic": ["utilities"],
    "starter": ["frameworks"],
    "showcase": ["frameworks"],
    "dependencies.messagepack": ["data-management"],
}
HUNTER = "GeorgeRontoulis"
IMAGE = f"https://raw.githubusercontent.com/GeorgeRontoulis/Threadlink/{BRANCH}/Threadforge/Threadlink_Banner.png"
# Assemblies of optional packages a project may not install; they cannot be referenced by GUID until present.
OPTIONAL_ASSEMBLIES = {"com.rlabrecque.steamworks.net"}

# name suffix: (display name, description, family, manual page, keywords)
CATALOG = {
    "": ("Threadlink", "The Threadlink core: the subsystem runtime that deploys before the first scene, Iris events, content loading by generated ID through Addressables, Vault typed definitions, generated IDs, engineering settings, memory toolkits and the Editor Workspace with the Runtime monitor, Code Generation and Content pages.",
         "Core", "README.md", ["framework", "architecture", "subsystems", "events", "addressables", "definitions"]),
    "nexus": ("Threadlink Nexus", "Scene transitions that load and prepare the next scene before presenting it and keep the current scene on failure, for Build Profile and Addressables scenes.",
              "Scenes", "engineer/nexus.md", ["scenes", "transitions", "loading"]),
    "aura": ("Threadlink Aura", "Audio cues with variations, categories, voice limits, fades and a crossfading music channel, with scene music as designer data.",
             "Audio", "engineer/aura.md", ["audio", "sound", "music"]),
    "dextra": ("Threadlink Dextra", "Per-player input with saved rebinding and prompts, layered screens that block gameplay input while open, and interaction targeting.",
               "Input and UI", "engineer/dextra.md", ["input", "rebinding", "ui", "screens", "interaction"]),
    "sentinel": ("Threadlink Sentinel", "Accounts and platform capabilities routed to the installed provider, and per-account saves that are transactional, versioned and recoverable.",
                 "Platform", "engineer/sentinel/README.md", ["platform", "accounts", "achievements", "save", "persistence"]),
    "sentinel.steam": ("Threadlink Sentinel Steam", "Steam accounts, achievements, statistics, leaderboards, presence, friends, invites, DLC, join intents and Steam Cloud, through Unity's Steamworks package or Steamworks.NET.",
                       "Platform", "engineer/sentinel/modules.md", ["steam", "steamworks", "platform"]),
    "deterministic": ("Threadlink Deterministic", "Fixed-point numbers, vectors and rotations, and stateless seeded random streams that give identical results on every machine.",
                      "Toolkits", "engineer/deterministic/README.md", ["deterministic", "fixed point", "random", "lockstep"]),
    "starter": ("Threadlink Starter", "A small complete game on Threadlink: menu and gameplay scenes, saves with recovery, settings with rebinding, audio and screens.",
                "Samples", "getting-started/starter.md", ["starter", "template", "sample"]),
    "showcase": ("Threadlink Showcase", "A gallery of runnable demos for every Threadlink feature, opened from the Threadlink Hub.",
                 "Samples", "getting-started/showcase.md", ["showcase", "samples", "demo"]),
}
# Sample compositions are not part of the complete framework install.
NOT_IN_FRAMEWORK = {"starter", "showcase"}
SECTIONS = {"getting-started": "getting-started", "foundations": "foundations", "designer": "designer-reference",
            "engineer": "engineer-reference", "reference": "reference"}


def package_name(suffix):
    return PREFIX + ("." + suffix if suffix else "")


def docs_url(page):
    if page == "README.md":
        return DOCS
    parts = page[:-3].split("/")
    parts[0] = SECTIONS[parts[0]]
    if parts[-1] == "README":
        parts = parts[:-1]
    return DOCS + "/".join(parts)


def folders():
    return {json.loads((f / "package.json").read_text(encoding="utf-8"))["name"]: f
            for f in sorted(PACKAGES.iterdir()) if (f / "package.json").exists()}


def first_party():
    return {name: folder for name, folder in folders().items()
            if name == PREFIX or name.startswith(PREFIX + ".")}


def suffix_of(name):
    return "" if name == PREFIX else name[len(PREFIX) + 1:]


def write_json(path, data):
    path.write_text(json.dumps(data, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def apply(version):
    if not re.fullmatch(r"\d+\.\d+\.\d+", version):
        raise SystemExit("Use a release version such as 1.0.0.")
    packages = first_party()
    unknown = [n for n in packages if suffix_of(n) not in CATALOG]
    if unknown:
        raise SystemExit("Missing catalog entries: " + ", ".join(unknown))
    license_text = LICENSE_SOURCE.read_text(encoding="utf-8")
    for name, folder in packages.items():
        suffix = suffix_of(name)
        display, description, family, page, keywords = CATALOG[suffix]
        old = json.loads((folder / "package.json").read_text(encoding="utf-8"))
        dependencies = {}
        for dependency, value in old.get("dependencies", {}).items():
            dependencies[dependency] = version if dependency.startswith(PREFIX) else value
        data = {
            "name": name,
            "version": version,
            "displayName": display,
            "description": description,
            "unity": UNITY,
            "author": {"name": "Threadforge", "url": REPOSITORY},
            "license": "MIT",
            "keywords": ["threadlink"] + keywords,
            "documentationUrl": docs_url(page),
            "changelogUrl": f"{REPOSITORY}/blob/{BRANCH}/Packages/{name}/CHANGELOG.md",
            "licensesUrl": f"{REPOSITORY}/blob/{BRANCH}/Packages/{name}/LICENSE.md",
            "dependencies": dependencies,
        }
        for optional in ("samples", "hideInEditor", "type"):
            if optional in old:
                data[optional] = old[optional]
        write_json(folder / "package.json", data)
        (folder / "LICENSE.md").write_text(license_text, encoding="utf-8")
    starter = packages.get(package_name("starter"))
    if starter:
        write_json(starter / "compatibility.json", {
            "set": f"threadlink-{version}",
            "unity": (ROOT / "ProjectSettings/ProjectVersion.txt").read_text().splitlines()[0].split(": ")[1],
            "packages": [{"name": n, "version": version} for n in sorted(packages)],
        })
    workspace = packages.get(PREFIX)
    if workspace:
        entries = []
        for suffix, (display, description, family, page, _) in CATALOG.items():
            name = package_name(suffix)
            if name not in packages:
                continue
            manifest = json.loads((packages[name] / "package.json").read_text(encoding="utf-8"))
            entries.append({
                "name": name, "displayName": display, "description": description, "family": family,
                "documentationUrl": docs_url(page),
                "dependencies": sorted(d for d in manifest["dependencies"] if d.startswith(PREFIX)),
                "samples": [s["displayName"] for s in manifest.get("samples", [])],
            })
        catalog = workspace / "Editor/Workspace/Hub/catalog.json"
        catalog.parent.mkdir(parents=True, exist_ok=True)
        write_json(catalog, {"version": version, "framework": PREFIX, "registry": "https://package.openupm.com",
                             "scopes": ["com.threadforge", "com.cysharp"], "framework_excludes": sorted(package_name(s) for s in NOT_IN_FRAMEWORK),
                             "packages": entries})
    print(f"Applied {version} to {len(packages)} packages.")


def check():
    problems = []
    packages = first_party()
    versions = set()
    for name, folder in packages.items():
        data = json.loads((folder / "package.json").read_text(encoding="utf-8"))
        versions.add(data["version"])
        for field in ("displayName", "description", "unity", "author", "license", "keywords", "documentationUrl", "changelogUrl", "licensesUrl"):
            if not data.get(field):
                problems.append(f"{name}: missing {field}")
        for dependency, value in data.get("dependencies", {}).items():
            if dependency.startswith(PREFIX) and value != data["version"]:
                problems.append(f"{name}: depends on {dependency} {value}, not {data['version']}")
            if dependency.startswith(PREFIX) and dependency not in packages:
                problems.append(f"{name}: depends on missing {dependency}")
        if not (folder / "LICENSE.md").exists():
            problems.append(f"{name}: missing LICENSE.md")
        changelog = (folder / "CHANGELOG.md").read_text(encoding="utf-8") if (folder / "CHANGELOG.md").exists() else ""
        if f"## [{data['version']}]" not in changelog:
            problems.append(f"{name}: CHANGELOG.md has no [{data['version']}] entry")
        for item in folder.rglob("*"):
            relative = item.relative_to(folder)
            if any(part.endswith("~") or part.startswith(".") for part in relative.parts) or item.suffix == ".meta":
                continue
            if not Path(str(item) + ".meta").exists():
                problems.append(f"{name}: no .meta for {relative.as_posix()}")
        # Assembly definitions reference by GUID ("Use GUIDs"); optional wrappers stay named until installed.
        for asmdef in folder.rglob("*.asmdef"):
            named = [ref for ref in json.loads(asmdef.read_text(encoding="utf-8-sig")).get("references", [])
                     if not ref.startswith("GUID:") and ref not in OPTIONAL_ASSEMBLIES]
            if named:
                problems.append(f"{name}: {asmdef.name} references {', '.join(named)} by name (run python Tools/asmdef_guids.py)")
    if len(versions) > 1:
        problems.append("Packages have different versions: " + ", ".join(sorted(versions)))
    for name in folders():
        if name not in packages and name not in THIRD_PARTY:
            problems.append(f"{name}: not a Threadlink package or an approved third-party distribution")
    print("\n".join(problems) or f"OK: {len(packages)} packages at {', '.join(versions)}.")
    return 1 if problems else 0


def pack(output):
    output = Path(output)
    output.mkdir(parents=True, exist_ok=True)
    archives = []
    for name, folder in folders().items():
        version = json.loads((folder / "package.json").read_text(encoding="utf-8"))["version"]
        archive = output / f"{name}-{version}.tgz"
        with tarfile.open(archive, "w:gz") as bundle:
            for item in sorted(folder.rglob("*")):
                relative = item.relative_to(folder)
                if any(part.startswith(".") for part in relative.parts):
                    continue
                bundle.add(item, arcname="package/" + relative.as_posix(), recursive=False)
        archives.append(archive.name)
    print(f"Packed {len(archives)} archives into {output}.")


def tag(name, version):
    """The Git tag OpenUPM builds a package version from. Each package has its own prefix, since the repository holds
    several packages and the MessagePack distribution keeps its upstream version."""
    return f"{name}/{version}"


def tags():
    for name, folder in folders().items():
        print(tag(name, json.loads((folder / "package.json").read_text(encoding="utf-8"))["version"]))


def openupm(output):
    """One metadata file per package for github.com/openupm/openupm (data/packages/<name>.yml). OpenUPM merges a
    submission automatically when its pull request holds one file titled "chore(data): new package <name>"."""
    output = Path(output)
    output.mkdir(parents=True, exist_ok=True)
    # OpenUPM requires the submission time in epoch milliseconds, and licenseName equal to the SPDX license's name
    # (the MessagePack package's third-party notices ship inside the package).
    created = int(time.time() * 1000)
    for name, folder in folders().items():
        data = json.loads((folder / "package.json").read_text(encoding="utf-8"))
        suffix = "dependencies.messagepack" if name in THIRD_PARTY else suffix_of(name)
        readme = f"{BRANCH}:Packages/{name}/README.md" if (folder / "README.md").exists() else f"{BRANCH}:README.md"
        lines = [
            f"name: {name}",
            "aliases: []",
            f"displayName: {data['displayName']}",
            "description: >-",
            f"  {data['description']}",
            f"repoUrl: '{REPOSITORY}'",
            "parentRepoUrl: null",
            "licenseSpdxId: MIT",
            "licenseName: MIT License",
            "topics:",
            *[f"  - {topic}" for topic in TOPICS.get(suffix, ["utilities"])],
            f"hunter: {HUNTER}",
            f"gitTagPrefix: '{name}/'",
            "gitTagIgnore: ''",
            "trackingMode: git",
            f"image: '{IMAGE}'",
            "imageFit: contain",
            f"readme: '{readme}'",
            f"createdAt: {created}",
        ]
        (output / f"{name}.yml").write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"Wrote OpenUPM metadata for {len(folders())} packages into {output}.")


if __name__ == "__main__":
    command = sys.argv[1] if len(sys.argv) > 1 else ""
    if command == "apply" and len(sys.argv) == 3:
        apply(sys.argv[2])
    elif command == "check":
        sys.exit(check())
    elif command == "pack" and len(sys.argv) == 3:
        pack(sys.argv[2])
    elif command == "openupm" and len(sys.argv) == 3:
        openupm(sys.argv[2])
    elif command == "tags":
        tags()
    else:
        print(__doc__)
        sys.exit(2)
