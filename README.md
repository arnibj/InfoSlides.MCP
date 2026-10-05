# InfoSlides: put content on a TV from your terminal or your agent

**Turn a PowerPoint, a PDF, a photo, or a live data feed into a video stream playing on any smart TV.**
The lunch menu on the screen in reception. Opening hours in a shop window. A noticeboard in a
school corridor. Room information in a hotel lobby. A live numbers board in an office.

This repo is the `infoslides` binary: one dependency-free executable that is both a **developer
CLI** and an **[MCP](https://modelcontextprotocol.io) server** for AI agents like Claude Code,
Claude Desktop, and Cursor. The same tools also run as a **hosted server** at
`https://infoslides.app/mcp`, for assistants like ChatGPT and Claude on the web (see
[Hosted server](#hosted-server)).

The interesting part is that `create_tenant` is anonymous. An agent with this server installed can
take someone from *no account at all* to content playing on a physical screen: provision the
workspace, upload the deck, register the display, assign the schedule, hand back the stream link,
without anyone opening a dashboard. As far as we know, no other digital signage platform can be
driven that way.

New workspaces land on a **permanent free plan**: 1 screen, 4 slideshows, 2 users, 200 MB. No
credit card, no trial clock, nothing expires. The whole flow below costs nothing and keeps running.

## Zero to a live screen

```sh
# 1. Install (macOS/Linux; see Install for Windows and MCP clients)
#    Version-pinned. Check /releases/latest for the current one.
curl -L https://github.com/arnibj/InfoSlides.MCP/releases/download/v1.7.3/infoslides-v1.7.3-linux-x64.tar.gz | tar xz

# 2. Create a workspace (anonymous; prints an admin API key and saves it to ~/.infoslides)
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

Open that URL in the TV's browser or the InfoSlides TV app. It plays the content whichever way
this screen is set up to play it.

https://github.com/user-attachments/assets/8da62e05-59f4-4a70-9351-dfa3fd47ce4e

## As an MCP server

Hook it into an MCP client:

```sh
infoslides mcp install --client claude-code      # or claude-desktop / cursor
```

Or install as a **Gemini CLI extension** or **Antigravity plugin**:

```sh
# In Gemini CLI
gemini extensions install https://github.com/arnibj/InfoSlides.MCP

# In Antigravity CLI (install agy first, see below), from a clone of this repository
git clone https://github.com/arnibj/InfoSlides.MCP
agy plugin install ./InfoSlides.MCP
```

Antigravity installs plugins from a folder, so it needs a copy of this repository; the repository
root is the plugin (`plugin.json`, `mcp_config.json` and `skills/`). Install `agy` itself first:
`irm https://antigravity.google/cli/install.ps1 | iex` on Windows, or
`curl -fsSL https://antigravity.google/cli/install.sh | bash` on macOS and Linux. Both routes run
the local `infoslides` program, so install that first and put it on your PATH. (`agy plugin import
gemini` only imports extensions already installed in Gemini CLI and takes no URL.)

Or add the **Claude plugin** (it uses the hosted server, nothing to install first):

```sh
claude plugin marketplace add arnibj/InfoSlides.MCP
claude plugin install infoslides@infoslides
```

Or install the `.mcpb` bundle from the [latest release](https://github.com/arnibj/InfoSlides.MCP/releases/latest)
in any client that supports MCP bundles. It carries binaries for all three platforms and needs no
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

`INFOSLIDES_API_KEY` is optional. Leave it out and the agent can create a workspace from scratch.

### Hosted server

Assistants that run in the browser or the cloud connect to the hosted server instead of a local binary:

```
https://infoslides.app/mcp
```

- **Transport:** streamable HTTP.
- **Sign-in:** OAuth with your InfoSlides account. The assistant opens an InfoSlides consent page,
  you approve it, and it acts as you in that workspace. A bearer `isk_` API key also works.
- **Where to add it:** as a custom connector in ChatGPT, Claude or Gemini, or through the Claude
  plugin above.
- **Disconnecting:** revoke any connection at
  [infoslides.app/settings/connected-apps](https://infoslides.app/settings/connected-apps).

For ChatGPT, Claude and other hosted assistants the server offers a curated set of tools for
everyday screen work (a Gemini connection gets a wider set): what is playing, slides,
schedules, tickers, live data, devices and AI Studio slides. It leaves out billing and checkout,
workspace creation, API key management and file uploads from a local path, which only make sense on
your own machine or in the dashboard. Every call goes to the InfoSlides API with your own
credential, and the server stores none. It loads the hosted variant of the assistant skill
([zip](https://infoslides.app/skills/hosted/infoslides-assistant.zip)).

### What the agent gets

**The tools** cover the whole path: workspace provisioning (`create_tenant`, anonymous, returns
the admin key), slideshows including direct `.pptx`/`.pdf` upload (`upload_pptx`), media slides by URL or
direct file upload (`upload_media`), visibility conditions (time of day, weekday, date range, data triggers; show or hide, all or any),
edits to what is already playing (`update_slide` for a slide's duration, hidden flag and live data;
`update_slideshow` for the news ticker, clock, default duration and sharing; `delete_slide`,
`delete_slideshow`, `replace_slideshow_file`; `list_sources` for the ticker's sources, and
`list_adapters`, `create_source`, `update_source_settings`, `delete_source` for fetched feeds),
self-updating template slides driven by live data pushes (`update_source`, and push sources fed by
the user's own system: `push_data`, `get_source_status`, `create_source_key`), devices, schedules with
`AspectMismatch` warnings, playback-mode control (`update_slideshow`'s `playbackMode` switches a
slideshow between the rendered-video stream and a live HTML/CSS loop), playable stream links that
work whichever mode a screen is in, pairing the TV in front of the person (`pair_device`),
"play this there" in one call (`play_slideshow_find_device`), timed schedules (`get_schedule`, `add_schedule_entry`,
`delete_schedule_entry`), temporary takeovers (`create_takeover`, `list_takeovers`, `end_takeover`),
play-time reports (`get_slideshow_plays`, `get_device_plays`), screen locations (`update_device`, `list_devices` near a
point), the team (`list_team`, `invite_team_member`, `revoke_team_invitation`, `remove_team_member`),
workspace time zone and locale (`update_tenant`), "which screen is this?"
(`identify_devices`), what is on a screen and why it is black (`get_now_slide_png`, `get_device_diagnosis`),
a photo or video on a screen in a minute (`show_media_on_device`, `get_show_status`), renaming a screen
(`update_device`), one workspace health summary and offline-alert emails (`get_workspace_health`,
`get_offline_alerts`, `set_workspace_offline_alerts`, `set_device_offline_alerts`), AI Studio slides
(`make_ai_slide`, `get_ai_slide_job`, `insert_ai_slides`, `start_ai_studio_trial`), previewing a
template before saving it (`preview_new_template`), undoing a change (`undo_change`), feedback to the
InfoSlides team (`report_issue` when it gets stuck, `leave_testimonial` after a job went well; the
team may pin a testimonial to the landing page, credited to the agent), PNG slide previews for
self-verification, API keys including push-only keys scoped to a single slide, and Paddle checkout
links for a chosen plan and billing period. The backend enforces every plan
limit, and the tool layer cannot bypass them.

**Designing your own live data slide:** the [agent's guide to templates and pushed data](https://infoslides.app/blog/agents-guide-to-the-infoslides-galaxy)
covers how to write a template that reads well on a screen and how to connect a system that pushes
data to it.

**A [Skill](SKILL.md)**, which is the judgement the tools do not carry: when a self-updating slide
beats a fixed one, how to pick between landscape and portrait, how long a slide should stay up for
someone queueing versus someone walking past, how to find a screen by name, edit what is on it and
confirm the change reached the wall (`renderStatus`), and how to talk a person through the TV end of it
while they are holding a remote.

**The InfoSlides Digital Signage Assistant skill** covers the conversation side: it turns what a
person says ("turn off the ticker in the lobby", "breakfast menu before 11") into the right tool
calls in the right order, says what to tell them back, and includes worked scenarios, designing a
slide yourself, and when to report an issue or leave a testimonial. The tool names are the same.
Read it at <https://infoslides.app/skills/infoslides-assistant/SKILL.md> or download
[infoslides-assistant.zip](https://infoslides.app/skills/infoslides-assistant.zip) to install it in
Claude or another agent that loads skills.

## CLI

```sh
infoslides login                          # OAuth in the browser (Google/Microsoft/GitHub)
infoslides tenant create "Acme Cafe" owner@acme.test --save
infoslides gallery list
infoslides slideshow clone <gallery-id> --from-gallery
infoslides slideshow upload-pptx ./deck.pptx           # or ./deck.pdf; or: media upload + slide add-media
infoslides media upload ./logo.png
infoslides slide add-media <slideshow-id> --asset-id <media-id> --duration 8
infoslides slide set-conditions <slide-id> --condition time=08:00-11:00
infoslides slide set-conditions <slide-id> --condition date=2026-12-01..2026-12-26 --mode hide
infoslides slide update <slide-id> --hidden true                       # or --duration 15; dynamic: --override-data '{"price":"1.990 kr"}'
infoslides slide delete <slide-id>
infoslides slideshow update <slideshow-id> --ticker false --default-duration 15 --clock true
infoslides slideshow replace-file <slideshow-id> ./new-deck.pptx
infoslides slideshow delete <slideshow-id>
infoslides source list                                                 # ids for --ticker-source
infoslides template create "Weather" --html ./weather.html --css ./weather.css
infoslides slide add-dynamic <slideshow-id> <template-id>
infoslides source update <slide-id> --data '{"tempC": 12}'
infoslides template create "Queue" --html ./queue.html --css ./queue.css --data-mode push
infoslides slide add-dynamic <slideshow-id> <template-id> --create-push-key   # prints sourceId + a push key
infoslides source push <source-id> --data '{"nowServing": "A-142"}'
infoslides source status <source-id>
infoslides device create "Lobby screen" --width 1080 --height 1920
infoslides schedule assign <device-id> <slideshow-id>
infoslides stream link <device-id>
```

Screens, schedules and the workspace:

```sh
infoslides device pair --nickname swift-oak-42 --slideshow-id <slideshow-id>   # or --qr <url>; --device-id alone prints a code to type on the TV
infoslides device play <slideshow-id> --device-id <device-id> --until 2026-10-01T17:00   # --dry-run to see what would happen
infoslides device list --q lobby --near 64.1466,-21.9426
infoslides device set-location <device-id> 64.1466 -21.9426 --label "Front lobby"
infoslides device identify                                  # each online screen shows its name for ~90 s
infoslides device now <device-id> --output lobby.png        # what the screen should show right now
infoslides device diagnose <device-id>                      # why is it black?
infoslides device show <device-id> --file ./photo.jpg --until 2026-10-01T17:00   # or --url; default 30 minutes
infoslides device show-status <device-id> <media-asset-id> <slideshow-id>
infoslides device update <device-id> --name "Lobby TV" --width 1080 --height 1920
infoslides device offline-alerts <device-id> false          # true --quiet-start 22:00 --quiet-end 07:00 for own quiet hours
infoslides device plays <device-id> --from 2026-09-01 --to 2026-09-30   # slideshow plays <slideshow-id> for the other way round
infoslides schedule show <device-id>
infoslides schedule add <device-id> <slideshow-id> 06:00 11:00 --priority 1
infoslides schedule remove <device-id> <entry-id>
infoslides takeover create <slideshow-id> --device <id>,<id> --minutes 30   # or --starts-at/--ends-at
infoslides takeover list --active
infoslides takeover end <takeover-id>
infoslides source adapters
infoslides source create RssFeed "RÚV news" --config '{"feedUrl":"https://..."}' --interval 900
infoslides source edit <source-id> --enabled false
infoslides source delete <source-id>
infoslides team invite colleague@acme.test --role TenantAdmin
infoslides team list                                        # team revoke <invitation-id>, team remove <member-id>
infoslides workspace settings --time-zone Europe/London --locale en-GB
infoslides workspace health
infoslides workspace offline-alerts
infoslides workspace set-offline-alerts --quiet-start 22:00 --quiet-end 07:00   # neither option: alert at any hour
infoslides billing upgrade --plan Starter --period annual
```

AI Studio, template previews and undo:

```sh
infoslides ai make <slideshow-id> prompt --prompt "Lunch special: soup and bread 1.990 kr, 11-14"
infoslides ai make <slideshow-id> photo --media-asset-id <media-id>   # also: url --url <page>, document --media-asset-id <pdf-id>
infoslides ai status <slideshow-id> <job-id>                # poll until Ready, then look at the previews
infoslides ai insert <slideshow-id> <job-id>
infoslides ai trial                                         # the free trial, once per workspace
infoslides template preview @./menu.html --css @./menu.css --sample-data @./sample.json --output menu.png
infoslides undo <token>                                     # --force overwrites a later change
```

A command that changes something prints `undo with: infoslides undo <token>` to stderr; the token
works for 24 hours. When the API needs a decision (`NeedsClarification`), the question and its
choices are printed to stderr; run the command again with the chosen id.

Global options: `--api-url`, `--api-key`, `--json`. Credential precedence: flags →
`INFOSLIDES_API_KEY` / `INFOSLIDES_API_URL` → `~/.infoslides/` → defaults.

### Update notices

No install route notifies you of a new release on its own, so the binary checks for one itself: at
most once a day, in the background, cached to `~/.infoslides/update-check.json`. The notice goes to
**stderr** in CLI mode (so `--json` output stays clean and pipeable) and into the server
instructions in `--mcp` mode, where an agent can pass it on.

The check never sits on the critical path: the notice you see comes from the cache and the refresh
is for next time, so no command is slower for it and being offline costs nothing. Set
`INFOSLIDES_NO_UPDATE_CHECK=1` to switch it off; it also disables itself when `CI` is set.

## Install

Download from the [latest release](https://github.com/arnibj/InfoSlides.MCP/releases/latest):

| Platform              | Artefact                                 |
| --------------------- | ---------------------------------------- |
| Windows               | `infoslides-v<version>-win-x64.zip`      |
| Linux                 | `infoslides-v<version>-linux-x64.tar.gz` |
| macOS (Apple Silicon) | `infoslides-v<version>-osx-arm64.tar.gz` |
| MCP clients           | `infoslides-mcp-v<version>.mcpb`         |

Every release ships `sha256sums.txt`. Nothing is needed at runtime: the binary is Native AOT
compiled. Or build from source:

```sh
dotnet publish src/InfoSlides.Cli -c Release -r linux-x64   # or win-x64 / osx-arm64
```

## Links

- **InfoSlides**: <https://infoslides.app>
- **Hosted MCP server**: `https://infoslides.app/mcp` (OAuth; see [Hosted server](#hosted-server))
- **REST API reference**: <https://infoslides.app/docs/api> (the `/v1` surface these tools wrap)
- **Guide for AI agents**: <https://infoslides.app/agents.md>
- **Blog**: <https://infoslides.app/blog> (guides on templates, live data, screen design and agents)
- **Digital Signage Assistant skill**: <https://infoslides.app/skills/infoslides-assistant/SKILL.md> ([zip](https://infoslides.app/skills/infoslides-assistant.zip))

## Repository layout

| Path                                 | Purpose                                                                  |
| ------------------------------------ | ------------------------------------------------------------------------ |
| `SKILL.md`                           | The Skill: signage judgement to pair with the tools.                     |
| `API-CONTRACT.md`                    | The agent-facing REST contract the InfoSlides backend implements.        |
| `BACKEND-CHANGES.md`                 | Checklist of InfoSlides-side work (TenantApiKeys table, gatekeeping, …). |
| `docs/PUBLISHING.md`                 | Release, MCPB bundling, and MCP registry publishing runbook.             |
| `mcpb/`                              | MCPB bundle manifest template.                                           |
| `src/InfoSlides.Core`                | Shared API client, models, AOT JSON context, config, auth.               |
| `src/InfoSlides.Mcp.Tools`           | The MCP tools, shared by the local and the hosted server.                |
| `src/InfoSlides.Cli`                 | The `infoslides` executable: CLI verbs + local (stdio) MCP server.       |
| `src/InfoSlides.McpServer`           | The hosted MCP server (streamable HTTP, OAuth) at `infoslides.app/mcp`.  |
| `skills/`                            | The Digital Signage Assistant skill, local variant.                      |
| `plugins/claude/`                    | The Claude plugin (hosted server + hosted skill variant).                |
| `gemini-extension.json`, `GEMINI.md` | The Gemini CLI extension.                                                |
| `plugin.json`, `mcp_config.json`     | The Antigravity plugin (the repository root is the plugin).              |
| `deploy/`                            | Hosted server deployment: systemd unit, nginx, deploy script.            |
| `tests/`                             | Unit tests, end-to-end MCP stdio smoke tests and hosted server tests.    |

## Development

```sh
dotnet build          # AOT analyzers run as errors; keep it warning-free
dotnet test           # unit + MCP end-to-end tests (no network needed)
```

The MCP SDK tools are registered via the AOT-safe `WithTools<T>()` path; every wire type must be
listed in `InfoSlidesJsonContext` (a test fails if one is missing). In `--mcp` mode stdout is the
protocol, so log only to stderr.

Tool descriptions are trigger text, not documentation: a model matches them against what the user
just said, so they lead with the situation ("register the physical screen: the TV in reception,
the menu board above the counter") rather than the API operation. Tests in
`McpStdioSmokeTests` pin that vocabulary so it cannot quietly regress.

## Privacy Policy

InfoSlides processes presentation content, media assets, and screen device metadata solely for rendering and delivering digital signage streams to authorized displays. InfoSlides does not sell user data, train foundation AI models on user content, or track users for third-party advertising.

For full details regarding OAuth authentication security, data retention, and data subject rights, read our [Privacy Policy](https://infoslides.app/privacy).

## Licence

MIT.
