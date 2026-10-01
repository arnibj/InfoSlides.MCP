# InfoSlides for Gemini CLI & Antigravity

This extension connects Google Gemini CLI and Antigravity to **InfoSlides**, enabling agents to put presentations, images, videos, and live data feeds on physical smart TVs and digital signage displays.

## Prerequisite: Install the `infoslides` Binary

This extension runs the local `infoslides` executable in MCP mode (`infoslides --mcp`). The extension cannot download or compile binaries on its own, so please ensure `infoslides` is installed on your system PATH first.

### Quick Install:
- **macOS / Linux**:
  ```sh
  curl -L https://github.com/arnibj/InfoSlides.MCP/releases/download/v1.6.0/infoslides-v1.6.0-linux-x64.tar.gz | tar xz
  sudo mv infoslides /usr/local/bin/
  ```
- **Windows** (PowerShell):
  ```powershell
  # Download from https://github.com/arnibj/InfoSlides.MCP/releases/latest
  # Extract infoslides.exe and place in a folder on your PATH (e.g. C:\Users\<user>\AppData\Local\Microsoft\WindowsApps)
  ```

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

This extension bundles the **Digital Signage Assistant** (`infoslides-assistant`) skill and exposes 72 MCP tools covering:
- Creating workspaces, uploading slides (.pptx, .pdf, images, video)
- Registering screens, assigning schedules, temporary takeovers
- Managing tickers, clocks, and live content data feeds
- Screen diagnosis, status checks, and 24-hour instant undo (`apply_undo`)
