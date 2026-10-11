#!/usr/bin/env python3
"""Build the offline, single-file Threadlink manual from the GitBook space.

The GitBook space in Gitbook/manual is the source of truth. This script concatenates its pages in
SUMMARY.md order into one Markdown file, shifting headings down a level and rewriting links between
pages into links to anchors within the file, so it reads correctly on its own (for example inside a
Unity project, where it ships as Assets/Threadforge/MarkdownDocumentation.md).

Usage (from the repository root):
    python Gitbook/tools/build_single_file.py            # writes Threadforge/MarkdownDocumentation.md
    python Gitbook/tools/build_single_file.py --check    # exits 1 if that file is out of date
    python Gitbook/tools/build_single_file.py -o out.md  # writes elsewhere

Requires Python 3.8+ and nothing else.
"""

import argparse
import os
import re
import sys

REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
MANUAL_ROOT = os.path.join(REPO_ROOT, "Gitbook", "manual")
DEFAULT_OUTPUT = os.path.join(REPO_ROOT, "Threadforge", "MarkdownDocumentation.md")
TITLE = "Threadlink Framework — Reference Manual"

# Images in the GitBook space mapped to files that sit beside the single-file manual.
ASSET_MAP = {
    "threadlink-banner.png": "Threadlink_Banner.png",
}

SUMMARY_ENTRY = re.compile(r'^(\s*)\*\s+\[([^\]]+)\]\(([^)\s]+)(?:\s+"[^"]*")?\)\s*$')
SUMMARY_GROUP = re.compile(r"^##\s+(.+?)\s*$")
HEADING = re.compile(r"^(#{1,6})\s+(.*?)\s*$")
LINK = re.compile(r'(!?)\[([^\]]*)\]\(([^)\s]+)((?:\s+"[^"]*")?)\)')
FENCE = re.compile(r"^\s*(```|~~~)")


def slugify(text):
    """GitHub's heading anchor: lower case, punctuation dropped, spaces to hyphens."""
    text = text.replace("`", "").strip().lower()
    text = re.sub(r"[^\w\- ]", "", text)
    return text.replace(" ", "-")


def read_summary():
    """Ordered items: ("group", title) or ("page", relative_path)."""
    items = []
    with open(os.path.join(MANUAL_ROOT, "SUMMARY.md"), encoding="utf-8") as summary:
        for line in summary:
            group = SUMMARY_GROUP.match(line)
            if group:
                items.append(("group", group.group(1)))
                continue
            entry = SUMMARY_ENTRY.match(line)
            if entry:
                items.append(("page", os.path.normpath(entry.group(3))))
    return items


def split_code(lines):
    """Yield (line, in_code) for each line, tracking fenced code blocks."""
    in_code = False
    for line in lines:
        if FENCE.match(line):
            yield line, True
            in_code = not in_code
            continue
        yield line, in_code


class Anchors:
    """Assigns document-wide unique anchors in reading order, as GitHub does."""

    def __init__(self):
        self.counts = {}

    def add(self, text):
        base = slugify(text)
        count = self.counts.get(base, 0)
        self.counts[base] = count + 1
        return base if count == 0 else f"{base}-{count}"


def page_title(page):
    with open(os.path.join(MANUAL_ROOT, page), encoding="utf-8") as source:
        for line, in_code in split_code(source.read().splitlines()):
            heading = HEADING.match(line) if not in_code else None
            if heading and len(heading.group(1)) == 1:
                return heading.group(2)
    return None


def build():
    items = read_summary()
    pages = [path for kind, path in items if kind == "page"]
    intro = pages[0] if pages and pages[0] == "README.md" else None

    # Pass 1: lay out headings and record, per page, its original anchors -> document anchors.
    anchors = Anchors()
    anchors.add(TITLE)
    page_anchor = {}      # page -> anchor of its title
    heading_map = {}      # (page, original anchor) -> document anchor
    folded = set()        # pages whose title is their group's heading
    group, group_anchor, first_in_group = None, None, False
    for kind, value in items:
        if kind == "group":
            group, group_anchor, first_in_group = value, anchors.add(value), True
            continue
        fold = first_in_group and page_title(value) == group
        first_in_group = False
        if fold:
            folded.add(value)
        local_counts = {}
        with open(os.path.join(MANUAL_ROOT, value), encoding="utf-8") as page:
            lines = page.read().splitlines()
        for line, in_code in split_code(lines):
            heading = HEADING.match(line) if not in_code else None
            if not heading:
                continue
            level, text = len(heading.group(1)), heading.group(2)
            base = slugify(text)
            n = local_counts.get(base, 0)
            local_counts[base] = n + 1
            original = base if n == 0 else f"{base}-{n}"
            if value == intro and level == 1:
                document = slugify(TITLE)
            elif value in folded and level == 1 and value not in page_anchor:
                document = group_anchor
            else:
                document = anchors.add(text)
            heading_map[(value, original)] = document
            if level == 1 and value not in page_anchor:
                page_anchor[value] = document

    out_dir = os.path.dirname(DEFAULT_OUTPUT)

    def rewrite_link(match, page):
        bang, text, target, title = match.groups()
        if re.match(r"^[a-z]+:", target) or target.startswith("//"):
            return match.group(0)
        path, _, fragment = target.partition("#")
        if bang:
            name = os.path.basename(path)
            mapped = ASSET_MAP.get(name)
            if mapped is None:
                source = os.path.normpath(os.path.join(MANUAL_ROOT, os.path.dirname(page), path))
                mapped = os.path.relpath(source, out_dir).replace(os.sep, "/")
            return f"{bang}[{text}]({mapped}{title})"
        linked = os.path.normpath(os.path.join(os.path.dirname(page), path)) if path else page
        if not linked.endswith(".md"):
            return match.group(0)
        if fragment:
            anchor = heading_map.get((linked, fragment))
        else:
            anchor = page_anchor.get(linked)
        if anchor is None:
            raise SystemExit(f"{page}: cannot resolve link '{target}'")
        return f"[{text}](#{anchor})"

    # Pass 2: emit.
    output = [
        "<!-- GENERATED FILE. Do not edit: the source is the GitBook manual in Gitbook/manual of the Threadlink"
        " repository. Regenerate with: python Gitbook/tools/build_single_file.py -->",
        "",
        f"# {TITLE}",
        "",
        "> This is the offline, single-file edition of the Threadlink reference manual, generated from the GitBook"
        " space in the repository's `Gitbook/manual` folder.",
        "",
    ]
    for kind, value in items:
        if kind == "group":
            output += ["---", "", f"# {value}", ""]
            continue
        with open(os.path.join(MANUAL_ROOT, value), encoding="utf-8") as page:
            lines = page.read().splitlines()
        for line, in_code in split_code(lines):
            if not in_code:
                heading = HEADING.match(line)
                if heading:
                    level = len(heading.group(1))
                    if level == 1 and (value == intro or value in folded):
                        continue  # the document title, or the group heading, stands in for the page's
                    shift = 0 if value == intro else 1  # the introduction sits directly under the title
                    line = "#" * min(level + shift, 6) + " " + heading.group(2)
                else:
                    line = LINK.sub(lambda m: rewrite_link(m, value), line)
            output.append(line)
        if output[-1] != "":
            output.append("")
    return "\n".join(output).rstrip("\n") + "\n"


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("-o", "--output", default=DEFAULT_OUTPUT, help="file to write")
    parser.add_argument("--check", action="store_true", help="fail if the output file is out of date")
    args = parser.parse_args()

    text = build()
    if args.check:
        try:
            with open(args.output, encoding="utf-8") as existing:
                current = existing.read().replace("\r\n", "\n")
        except FileNotFoundError:
            current = None
        if current != text:
            print(f"{os.path.relpath(args.output, REPO_ROOT)} is out of date; run build_single_file.py", file=sys.stderr)
            return 1
        print("Single-file manual is up to date.")
        return 0

    with open(args.output, "w", encoding="utf-8", newline="\n") as target:
        target.write(text)
    print(f"Wrote {os.path.relpath(args.output, REPO_ROOT)} ({len(text.splitlines())} lines)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
