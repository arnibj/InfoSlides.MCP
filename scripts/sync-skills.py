#!/usr/bin/env python3
# ── Copyright notice ──────────────────────────────────────────────────────────────────
# (c) 2026 Arni Bjorgvinsson. All rights reserved.
# ─────────────────────────────────────────────────────────────────────────────────
"""Sync the Digital Signage Assistant skill into InfoSlides.MCP.

Copies and surface-processes the skill from the sibling `infoslides` repo if present,
or downloads and extracts the published zip from https://infoslides.app/skills/infoslides-assistant.zip.

The Claude plugin talks to the hosted server, so it gets the hosted variant of the skill (plugins/claude/skills/),
never the local one. It also copies what the hosted MCP server needs from the same build: the server instructions (full and hosted)
and hosted-rules.json (the hosted tool list), into src/InfoSlides.Mcp.Tools/Instructions/.

Usage:
  python scripts/sync-skills.py
"""
import io
import pathlib
import re
import shutil
import sys
import urllib.request
import zipfile

REPO_ROOT = pathlib.Path(__file__).resolve().parent.parent
TARGET_SKILL_DIR = REPO_ROOT / "skills" / "infoslides-assistant"
SIBLING_SRC = REPO_ROOT.parent / "infoslides" / "src" / "InfoSlides.Web" / "wwwroot" / "skills" / "infoslides-assistant"
PUBLISHED_ZIP_URL = "https://infoslides.app/skills/infoslides-assistant.zip"
HOSTED_ZIP_URL = "https://infoslides.app/skills/hosted/infoslides-assistant.zip"
PLUGIN_SKILL_DIR = REPO_ROOT / "plugins" / "claude" / "skills" / "infoslides-assistant"


def process_surface_markdown(text: str) -> str:
    """Strip hosted-only fences and unwrap local-only fences for local MCP / CLI skill."""
    text = re.sub(r"<!--\s*surface:hosted-only\s*-->.*?<!--\s*/surface\s*-->\r?\n?", "", text, flags=re.DOTALL)
    text = re.sub(r"<!--\s*surface:local-only\s*-->\r?\n?", "", text)
    text = re.sub(r"<!--\s*/surface\s*-->\r?\n?", "", text)
    return text


INSTRUCTION_FILES = {
    "full.txt": "https://infoslides.app/skills/instructions/full.txt",
    "hosted.txt": "https://infoslides.app/skills/instructions/hosted.txt",
    "hosted-rules.json": "https://infoslides.app/skills/hosted/hosted-rules.json",
}


def sync_instructions(wwwroot: pathlib.Path | None, mcp_root: pathlib.Path) -> None:
    """Copy the server instructions and the hosted tool list next to the tools that embed and test them."""
    target = mcp_root / "src" / "InfoSlides.Mcp.Tools" / "Instructions"
    target.mkdir(parents=True, exist_ok=True)
    local = {
        "full.txt": wwwroot / "skills" / "instructions" / "full.txt" if wwwroot else None,
        "hosted.txt": wwwroot / "skills" / "instructions" / "hosted.txt" if wwwroot else None,
        "hosted-rules.json": wwwroot / "skills" / "hosted" / "hosted-rules.json" if wwwroot else None,
    }
    for name, url in INSTRUCTION_FILES.items():
        source = local[name]
        if source is not None and source.exists():
            (target / name).write_bytes(source.read_bytes())
        else:
            req = urllib.request.Request(url, headers={"User-Agent": "InfoSlides-MCP-SkillSync/1.0"})
            with urllib.request.urlopen(req) as resp:
                (target / name).write_bytes(resp.read())
    print(f"Synced server instructions and hosted rules into {target}")


def sync_hosted_from_local(wwwroot: pathlib.Path) -> None:
    """Copy the already-processed hosted skill into the Claude plugin folder."""
    source = wwwroot / "skills" / "hosted" / "infoslides-assistant"
    shutil.rmtree(PLUGIN_SKILL_DIR, ignore_errors=True)
    for path in sorted(p for p in source.rglob("*") if p.is_file()):
        dest = PLUGIN_SKILL_DIR / path.relative_to(source)
        dest.parent.mkdir(parents=True, exist_ok=True)
        dest.write_bytes(path.read_bytes())
    print(f"Synced hosted skill into {PLUGIN_SKILL_DIR}")


def sync_hosted_from_remote() -> None:
    """Extract the published hosted skill zip into the Claude plugin folder."""
    req = urllib.request.Request(HOSTED_ZIP_URL, headers={"User-Agent": "InfoSlides-MCP-SkillSync/1.0"})
    with urllib.request.urlopen(req) as resp:
        data = resp.read()
    shutil.rmtree(PLUGIN_SKILL_DIR, ignore_errors=True)
    with zipfile.ZipFile(io.BytesIO(data)) as zf:
        for info in zf.infolist():
            if not info.is_dir():
                dest = PLUGIN_SKILL_DIR / info.filename.removeprefix("infoslides-assistant/")
                dest.parent.mkdir(parents=True, exist_ok=True)
                dest.write_bytes(zf.read(info))
    print(f"Extracted hosted skill into {PLUGIN_SKILL_DIR}")


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
    # source_dir is <web>/wwwroot/skills/infoslides-assistant; the instructions live next to it.
    sync_instructions(source_dir.parent.parent, TARGET_SKILL_DIR.parent.parent)
    sync_hosted_from_local(source_dir.parent.parent)


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
    sync_instructions(None, TARGET_SKILL_DIR.parent.parent)
    sync_hosted_from_remote()


def main() -> None:
    # If invoked directly inside infoslides repo, TARGET is InfoSlides.MCP/skills/infoslides-assistant
    target_repo_mcp = REPO_ROOT.parent / "InfoSlides.MCP"
    if target_repo_mcp.exists() and REPO_ROOT.name == "infoslides":
        global TARGET_SKILL_DIR
        global PLUGIN_SKILL_DIR
        TARGET_SKILL_DIR = target_repo_mcp / "skills" / "infoslides-assistant"
        PLUGIN_SKILL_DIR = target_repo_mcp / "plugins" / "claude" / "skills" / "infoslides-assistant"
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
