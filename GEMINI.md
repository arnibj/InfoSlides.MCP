# InfoSlides for Gemini CLI & Antigravity

This extension connects Google Gemini CLI and Antigravity to **InfoSlides**, enabling agents to put presentations, images, videos, and live data feeds on physical smart TVs and digital signage displays.

## Prerequisite: Install the `infoslides` Binary

This extension runs the local `infoslides` executable in MCP mode (`infoslides --mcp`). The extension cannot download or compile binaries on its own, so please ensure `infoslides` is installed on your system PATH first.

### Install

Download the archive for your system from <https://github.com/arnibj/InfoSlides.MCP/releases/latest>, extract the
`infoslides` executable, and put it in a folder on your PATH (for example `/usr/local/bin` on macOS and Linux, or a
folder you add to PATH on Windows). The README has the exact steps for each platform. Check it with
`infoslides --version`.

## Antigravity

Antigravity installs this repository as a plugin from a folder: `git clone https://github.com/arnibj/InfoSlides.MCP`, then `agy plugin install ./InfoSlides.MCP`. Install the `agy` program first (see the README). The plugin reads `plugin.json`, `mcp_config.json` and `skills/` from the repository root and runs `infoslides --mcp`, so the prerequisite above applies there too.

## Authentication

InfoSlides supports anonymous workspace creation via the `create_tenant` tool, which creates a permanent free workspace without requiring a credit card or upfront credentials.

If you already have an InfoSlides workspace, set your API key:
```sh
export INFOSLIDES_API_KEY="isk_admin_..."
```
or log in via the CLI:
```sh
infoslides auth login
```

## Available Capabilities & Skill

This extension bundles the **Digital Signage Assistant** (`infoslides-assistant`) skill and exposes the InfoSlides MCP tools, covering:
- Creating workspaces, uploading slides (.pptx, .pdf, images, video)
- Registering screens, assigning schedules, temporary takeovers
- Managing tickers, clocks, and live content data feeds
- Screen diagnosis, status checks, and 24-hour instant undo (`undo_change`)
