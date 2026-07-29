# InfoSlides — put content on a TV from your terminal or your agent

**Turn a PowerPoint, a photo, or a live data feed into a video stream playing on any smart TV.**
The lunch menu on the screen in reception. Opening hours in a shop window. A noticeboard in a
school corridor. Room information in a hotel lobby. A live numbers board in an office.

This repo is the `infoslides` binary: one dependency-free executable that is both a **developer
CLI** and an **[MCP](https://modelcontextprotocol.io) server** for AI agents like Claude Code,
Claude Desktop, and Cursor.

The interesting part is that `create_tenant` is anonymous. An agent with this server installed can
take someone from *no account at all* to content playing on a physical screen — provision the
workspace, upload the deck, register the display, assign the schedule, hand back the stream link —
without anyone opening a dashboard. As far as we know, no other digital signage platform can be
driven that way.

New workspaces land on a **permanent free plan**: 1 screen, 4 slideshows, 2 users, 200 MB. No
credit card, no trial clock, nothing expires. The whole flow below costs nothing and keeps running.

## Zero to a live screen

```sh
# 1. Install (macOS/Linux; see Install for Windows and MCP clients)
#    Version-pinned — check /releases/latest for the current one.
curl -L https://github.com/arnibj/InfoSlides.MCP/releases/download/v1.2.0/infoslides-v1.2.0-linux-x64.tar.gz | tar xz

# 2. Create a workspace — anonymous, prints an admin API key, saves it to ~/.infoslides
./infoslides tenant create "Acme Cafe" owner@acme.test --save

# 3. Grab a ready-made deck from the starter gallery
./infoslides gallery list
./infoslides slideshow clone <gallery-id> --from-gallery

# 4. Register the screen and tell it what to play
./infoslides device create "Reception TV"
./infoslides schedule assign <device-id> <slideshow-id>

# 5. The link to open on the TV
./infoslides stream link <device-id>
```

Open that URL in the TV's browser, the InfoSlides TV app, or any HLS-capable player.

https://github.com/user-attachments/assets/8da62e05-59f4-4a70-9351-dfa3fd47ce4e

## As an MCP server

Hook it into an MCP client:

```sh
infoslides mcp install --client claude-code      # or claude-desktop / cursor
```

Or install the `.mcpb` bundle from the [latest release](https://github.com/arnibj/InfoSlides.MCP/releases/latest)
in any client that supports MCP bundles — it carries binaries for all three platforms and needs no
runtime and no API key to get started.

Or configure it by hand (stdio transport):

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

`INFOSLIDES_API_KEY` is optional — leave it out and the agent can create a workspace from scratch.

### What the agent gets

**27 tools**, covering the whole path: workspace provisioning (`create_tenant`, anonymous, returns
the admin key), slideshows including direct `.pptx` upload (`upload_pptx`), media slides by URL or
direct file upload (`upload_media`), visibility conditions (time of day, weekday, data triggers),
self-updating template slides driven by live data pushes (`update_source`), devices, schedules with
`AspectMismatch` warnings, HLS stream links, PNG slide previews for self-verification, API keys
including push-only keys scoped to a single slide, and Paddle upgrade links. The backend enforces
every plan limit — the tool layer cannot bypass them.

**A [Skill](SKILL.md)**, which is the judgement the tools do not carry: when a self-updating slide
beats a fixed one, how to pick between landscape and portrait, how long a slide should stay up for
someone queueing versus someone walking past, and how to talk a person through the TV end of it
while they are holding a remote.

## CLI

```sh
infoslides login                          # OAuth in the browser (Google/Microsoft/GitHub)
infoslides tenant create "Acme Cafe" owner@acme.test --save
infoslides gallery list
infoslides slideshow clone <gallery-id> --from-gallery
infoslides slideshow upload-pptx ./deck.pptx           # or: media upload + slide add-media
infoslides media upload ./logo.png
infoslides slide add-media <slideshow-id> --asset-id <media-id> --duration 8
infoslides slide set-conditions <slide-id> --condition time=08:00-11:00
infoslides template create "Weather" --html ./weather.html --css ./weather.css
infoslides slide add-dynamic <slideshow-id> <template-id>
infoslides source update <slide-id> --data '{"tempC": 12}'
infoslides device create "Lobby screen" --width 1080 --height 1920
infoslides schedule assign <device-id> <slideshow-id>
infoslides stream link <device-id>
```

Global options: `--api-url`, `--api-key`, `--json`. Credential precedence: flags →
`INFOSLIDES_API_KEY` / `INFOSLIDES_API_URL` → `~/.infoslides/` → defaults.

### Update notices

No install route notifies you of a new release on its own, so the binary checks for one itself: at
most once a day, in the background, cached to `~/.infoslides/update-check.json`. The notice goes to
**stderr** in CLI mode (so `--json` output stays clean and pipeable) and into the server
instructions in `--mcp` mode, where an agent can pass it on.

The check never sits on the critical path — the notice you see comes from the cache and the refresh
is for next time — so no command is slower for it and being offline costs nothing. Set
`INFOSLIDES_NO_UPDATE_CHECK=1` to switch it off; it also disables itself when `CI` is set.

## Install

Download from the [latest release](https://github.com/arnibj/InfoSlides.MCP/releases/latest):

| Platform | Artefact |
| --- | --- |
| Windows | `infoslides-v<version>-win-x64.zip` |
| Linux | `infoslides-v<version>-linux-x64.tar.gz` |
| macOS (Apple Silicon) | `infoslides-v<version>-osx-arm64.tar.gz` |
| MCP clients | `infoslides-mcp-v<version>.mcpb` |

Every release ships `sha256sums.txt`. Nothing is needed at runtime — the binary is Native AOT
compiled. Or build from source:

```sh
dotnet publish src/InfoSlides.Cli -c Release -r linux-x64   # or win-x64 / osx-arm64
```

## Links

- **InfoSlides** — <https://infoslides.app>
- **REST API reference** — <https://infoslides.app/docs/api> (the `/v1` surface these tools wrap)
- **Guide for AI agents** — <https://infoslides.app/agents.md>

## Repository layout

| Path | Purpose |
| --- | --- |
| `SKILL.md` | The Skill: signage judgement to pair with the tools. |
| `API-CONTRACT.md` | The agent-facing REST contract the InfoSlides backend implements. |
| `BACKEND-CHANGES.md` | Checklist of InfoSlides-side work (TenantApiKeys table, gatekeeping, …). |
| `docs/PUBLISHING.md` | Release, MCPB bundling, and MCP registry publishing runbook. |
| `mcpb/` | MCPB bundle manifest template. |
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

Tool descriptions are trigger text, not documentation: a model matches them against what the user
just said, so they lead with the situation ("register the physical screen — the TV in reception,
the menu board above the counter") rather than the API operation. Tests in
`McpStdioSmokeTests` pin that vocabulary so it cannot quietly regress.

## Licence

MIT.
