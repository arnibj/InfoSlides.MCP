#!/usr/bin/env python3
# ── Copyright notice ──────────────────────────────────────────────────────────────────
# (c) 2026 Arni Bjorgvinsson. All rights reserved.
# ─────────────────────────────────────────────────────────────────────────────────
"""Sync the Digital Signage Assistant skill into InfoSlides.MCP.

Copies and surface-processes the skill from the sibling `infoslides` repo if present,
or downloads and extracts the published zip from https://infoslides.app/skills/infoslides-assistant.zip.

Usage:
  python scripts/sync-skills.py
"""
import io
import pathlib
import re
import sys
import urllib.request
import zipfile

REPO_ROOT = pathlib.Path(__file__).resolve().parent.parent
TARGET_SKILL_DIR = REPO_ROOT / "skills" / "infoslides-assistant"
SIBLING_SRC = REPO_ROOT.parent / "infoslides" / "src" / "InfoSlides.Web" / "wwwroot" / "skills" / "infoslides-assistant"
PUBLISHED_ZIP_URL = "https://infoslides.app/skills/infoslides-assistant.zip"


def process_surface_markdown(text: str) -> str:
    """Strip hosted-only fences and unwrap local-only fences for local MCP / CLI skill."""
    text = re.sub(r"<!--\s*surface:hosted-only\s*-->.*?<!--\s*/surface\s*-->\r?\n?", "", text, flags=re.DOTALL)
    text = re.sub(r"<!--\s*surface:local-only\s*-->\r?\n?", "", text)
    text = re.sub(r"<!--\s*/surface\s*-->\r?\n?", "", text)
    return text


def sync_from_local(source_dir: pathlib.Path) -> None:
    TARGET_SKILL_DIR.mkdir(parents=True, exist_ok=True)
    files = sorted(p for p in source_dir.rglob("*") if p.is_file())
    for path in files:
        rel = path.relative_to(source_dir)
        dest = TARGET_SKILL_DIR / rel
        dest.parent.mkdir(parents=True, exist_ok=True)
        if path.suffix == ".md":
            content = process_surface_markdown(path.read_text(encoding="utf-8"))
            dest.write_text(content, encoding="utf-8")
        else:
            dest.write_bytes(path.read_bytes())
    print(f"Synced {len(files)} files from local repository: {source_dir} -> {TARGET_SKILL_DIR}")


def sync_from_remote(url: str) -> None:
    TARGET_SKILL_DIR.mkdir(parents=True, exist_ok=True)
    print(f"Fetching published skill zip from {url}...")
    req = urllib.request.Request(url, headers={"User-Agent": "InfoSlides-MCP-SkillSync/1.0"})
    with urllib.request.urlopen(req) as resp:
        zip_bytes = resp.read()

    count = 0
    with zipfile.ZipFile(io.BytesIO(zip_bytes)) as zf:
        for info in zf.infolist():
            if info.is_dir():
                continue
            name = info.filename
            if name.startswith("infoslides-assistant/"):
                rel_name = name[len("infoslides-assistant/"):]
            else:
                rel_name = name
            dest = TARGET_SKILL_DIR / rel_name
            dest.parent.mkdir(parents=True, exist_ok=True)
            with zf.open(info) as src_file:
                dest.write_bytes(src_file.read())
            count += 1
    print(f"Extracted {count} skill files into {TARGET_SKILL_DIR}")


def main() -> None:
    # If invoked directly inside infoslides repo, TARGET is InfoSlides.MCP/skills/infoslides-assistant
    target_repo_mcp = REPO_ROOT.parent / "InfoSlides.MCP"
    if target_repo_mcp.exists() and REPO_ROOT.name == "infoslides":
        global TARGET_SKILL_DIR
        TARGET_SKILL_DIR = target_repo_mcp / "skills" / "infoslides-assistant"
        local_src = REPO_ROOT / "src" / "InfoSlides.Web" / "wwwroot" / "skills" / "infoslides-assistant"
        if local_src.exists():
            sync_from_local(local_src)
            return

    if SIBLING_SRC.exists() and (SIBLING_SRC / "SKILL.md").exists():
        sync_from_local(SIBLING_SRC)
    else:
        try:
            sync_from_remote(PUBLISHED_ZIP_URL)
        except Exception as e:
            print(f"Error fetching from remote: {e}", file=sys.stderr)
            sys.exit(1)


if __name__ == "__main__":
    main()
