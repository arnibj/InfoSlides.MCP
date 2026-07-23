# InfoSlides.MCP

Agent-native access to [InfoSlides](https://infoslides.app) digital signage: a single
dependency-free executable `infoslides` that is both a **developer CLI** and, with `--mcp`, an
**MCP server** (stdio) for AI agents like Claude Code and Cursor.

An agent can take a user from zero to a live signage stream in minutes:
anonymous `create_tenant` → admin API key → upload content, register a screen, assign a
schedule → live HLS stream — with a Paddle upgrade unlocking AI templates and unlimited devices.

## Install

Download the binary for your platform from the releases page (`infoslides-<version>-<rid>.tar.gz`
/ `.zip`), or build from source:

```sh
dotnet publish src/InfoSlides.Cli -c Release -r linux-x64   # or win-x64 / osx-arm64
```

Requires nothing at runtime — the binary is Native AOT compiled.

## MCP server

Hook the server into your MCP client automatically:

```sh
infoslides mcp install --client claude-code      # or claude-desktop / cursor
```

Or configure manually:

```json
{
  "mcpServers": {
    "infoslides": {
      "command": "/path/to/infoslides",
      "args": ["--mcp"],
      "env": { "INFOSLIDES_API_KEY": "isk_admin_..." }
    }
  }
}
```

24 tools are exposed: tenant provisioning (`create_tenant` is anonymous and returns the admin
key), slideshows, media slides, visibility conditions (time / weekday / data triggers), dynamic
templates (AI prompt or raw HTML/CSS), live data pushes (`update_source`), devices, schedules
(with `AspectMismatch` warnings), HLS stream links, slide PNG previews, API keys (admin and
push-only data-provider keys) and Paddle upgrade links. The backend enforces all entitlements.

## CLI

```sh
infoslides login                          # OAuth in the browser (Google/Microsoft/GitHub)
infoslides tenant create "Acme Cafe" owner@acme.test --save
infoslides gallery list
infoslides slideshow clone <gallery-id> --from-gallery
infoslides device create "Lobby screen" --width 1080 --height 1920
infoslides schedule assign <device-id> <slideshow-id>
infoslides stream link <device-id>
```

Global options: `--api-url`, `--api-key`, `--json`. Credential precedence: flags →
`INFOSLIDES_API_KEY` / `INFOSLIDES_API_URL` → `~/.infoslides/` → defaults.

## Repository layout

| Path | Purpose |
| --- | --- |
| `API-CONTRACT.md` | The agent-facing REST contract the InfoSlides backend implements. |
| `BACKEND-CHANGES.md` | Checklist of InfoSlides-side work (TenantApiKeys table, gatekeeping, …). |
| `src/InfoSlides.Core` | Shared API client, models, AOT JSON context, config, auth. |
| `src/InfoSlides.Cli` | The `infoslides` executable: CLI verbs + MCP server. |
| `tests/` | Unit tests and end-to-end MCP stdio smoke tests. |

## Development

```sh
dotnet build          # AOT analyzers run as errors — keep it warning-free
dotnet test           # unit + MCP end-to-end tests (no network needed)
```

The MCP SDK tools are registered via the AOT-safe `WithTools<T>()` path; every wire type must be
listed in `InfoSlidesJsonContext` (a test fails if one is missing). In `--mcp` mode stdout is the
protocol — log only to stderr.
