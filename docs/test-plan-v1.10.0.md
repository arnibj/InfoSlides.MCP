# Test plan: InfoSlides MCP 1.10.0

What this covers: everything in [releases/v1.10.0.md](releases/v1.10.0.md), namely the evening version tools, the wider `check_slideshow`, the `list_takeovers` page shape and the `tenant create --save` key fix, on the CLI, the local MCP server and the hosted server. The website's own screens are tested from their release notes in the infoslides repo (`kit-06-evening-version-for-agents.md`, `plat-02-slideshow-preflight.md`, `plat-07-takeover-paging-and-close.md`).

## Who runs each test

| Who | Meaning |
| --- | --- |
| **Agent** | An agent can run the test and judge the result by itself, given the access listed below. |
| **Manual** | A person has to do it. The test says why: a physical TV, your own sign-in, a client app's screens, a step only you take, or a machine the agent does not have. |

An agent can read rendered slides as images (`preview_slide`, `get_now_slide_png`, `infoslides device now`), so a check of how a slide looks is an Agent test. How it reads on a real TV across a room is Manual.

## What the agent needs

- The local stack running (API on port 5001, Docker PostgreSQL and Redis) with `main` of the infoslides repo.
- A test workspace, local or a dedicated one on production, and its admin API key in `INFOSLIDES_API_KEY`. Never a customer's workspace. A second workspace key for the isolation test.
- A home folder of its own for the CLI (`HOME` and `USERPROFILE` pointing at a temp folder), so a test cannot touch your real `~/.infoslides`.
- For the free plan: 1 screen and 4 slideshows. That limit is what test B13 needs.

`check_slideshow` has no CLI verb. Call it through the MCP server (`infoslides --mcp`, or the hosted `/mcp` with an API key).

## State on 2026-11-09

| Surface | State |
| --- | --- |
| Local `main`, MCP repo | 1.10.0 work staged, not committed. A1 passed on it (298 tests). |
| Website API on production | Has the slideshow check route. Has no `evening-version` route yet. |
| Hosted server | Server card says 1.9.0 with 50 tools, no `make_evening_version`. |
| GitHub release | Latest is v1.9.0. |

## Results on 2026-11-09 (stages A and B, agent tests)

All 35 agent tests pass, against the staged 1.10.0 work and local `main` of the infoslides repo. Two failed on the first run and were fixed the same day. The manual tests B14 and K4 are still to do.

| ID | Result | Detail |
| --- | --- | --- |
| A6 | Pass after fix | `agents.md` line 300 said 49. It now says 50. |
| B6 | Pass after fix | First run: photo not dimmed, because the legibility pass lowered the value and three layouts only darkened the lower part. Retest on a real render: 70% over the whole photo, plain colours and text right, uploaded and live data slides identical to the original. See `evening-photo-dimming.md` in the infoslides release notes. |
| B8 | Pass | `nowPlaying` switched to the copy and back. The PNG was dark on a slideshow with a solid colour designed slide. |
| P4 | Pass | Took 53.6 s while other tests were running. |
| P8 | Pass | Stopped at the 45 second limit: 19 of 27 slides measured. |

The rest of A1 to A5, B1 to B13, B15, B16, P1 to P9, T1, T2 and K1 to K3 passed as written or with the setup changes below. Test workspaces were raised to a paid plan with more slideshows in the local database, and their owner email marked verified.

Setup that differs from the test as first written:

- **B3, B5:** "midnight" shows as `23:59`. The stored end is the last moment of the day, so there is no gap.
- **P2:** `delete_media` refuses a file a slide uses. Mark the media asset deleted in the database, or remove its file on disk, to get `MediaMissing`.
- **P3:** `push_data` has no expiry. Set `ExpiresAt` on the stored `ContentItems` row.
- **P6:** a designed slide refuses text that does not fit, so it never overflows. Use a live data slide whose text element has a fixed height. The finding carries `slideId`; the `/v1` response has no slide number.
- **P7:** a designed slide has no font size. Use a live data slide with 10 px text, and the 8 pt PowerPoint.
- **K2:** with a saved key the API requires `ownerEmail` to be that key's owner, so use the saved workspace's owner address.

## Order of work

The website API goes first: until it is live, the new `check_slideshow` description promises checks the API does not run, and the evening tools answer `NotFound`.

1. Stage A and B, locally.
2. You commit and push, and deploy the website API. Then stage C.
3. You tag and approve the release. Then stage D.
4. You deploy the hosted server. Then stage E.
5. Stage F, cleanup.

Stop at a failed test in stages A to D and fix it before the next step. A release that is already out needs a new patch version, since tags cannot be moved.

## Stage A: before you commit

| ID | Who | Test | Expected |
| --- | --- | --- | --- |
| A1 | Agent | `dotnet test` in the MCP repo. | All pass: Core 99, McpServer 59, Mcp 140. |
| A2 | Agent | `./scripts/smoke-test-cli.ps1 -Mcp` (offline checks and the stdio handshake). | Summary shows `FAIL: 0`. |
| A3 | Agent | `infoslides --version`, then read the version in `plugin.json`, `gemini-extension.json`, `plugins/claude/.claude-plugin/plugin.json` and the README install pin. | All say 1.10.0. |
| A4 | Agent | `infoslides slideshow --help` and `infoslides slideshow evening --help`. | Lists `evening-offer` and `evening`; `evening` has `--evening-start` and `--morning-end`. |
| A5 | Agent | Search both copies of the skill (`skills/` and `plugins/claude/skills/`) for "does not look at text size". | No match. The "Is this slideshow ready?" row describes the new checks. |
| A6 | Agent | Read line 300 of `src/InfoSlides.Web/wwwroot/agents.md` in the infoslides repo. | Says 50 everyday tools plus `get_user_profile`. **Known failure today:** it says 49. |

## Stage B: end to end against the local API

Run with `--api-url https://localhost:5001` or the MCP server pointed at the same API. Each test starts from the state the previous one left unless it says otherwise. "Nothing created" means `list_slideshows` and the screen's `get_schedule` are the same before and after.

### Evening version

Set up one slideshow with at least one designed slide with a photo, one uploaded image slide and one live data slide, playing on one screen created with `create_device`. No physical TV is needed here.

| ID | Who | Test | Expected |
| --- | --- | --- | --- |
| B1 | Agent | `infoslides slideshow evening-offer <id>`. | `designerSlideCount` is above 0, `devices` names the screen, `atSlideshowLimit` and `brandLocked` are false. Nothing created. |
| B2 | Agent | The offer on a slideshow with only uploaded slides. | `designerSlideCount` is 0. |
| B3 | Agent | `infoslides slideshow evening <id>` with no times. | A slideshow "{Title} (evening)". Two schedule entries on the screen, 17:00 to midnight and midnight to 08:00, in the workspace timezone. The output prints an undo token. |
| B4 | Agent | Undo B3 with `infoslides undo <token>`, then `evening <id> --evening-start 18:00 --morning-end 07:00`. | After the undo the copy and its entries are gone and the original plays. Then new entries use 18:00 and 07:00. |
| B5 | Agent | Undo, then `evening <id> --morning-end 00:00`. | One window per screen, from 17:00 to midnight. No empty 00:00 to 00:00 entry. |
| B6 | Agent | Look at each slide of the copy with `preview_slide`. | Designed slide is dark (`#0E0F13` plain colours, `#F4F1EA` text, photo dimmed to 70%). Uploaded and live data slides look the same as in the original. |
| B7 | Agent | Change the original's designed slide, then read the copy. | The copy did not change. |
| B8 | Agent | Undo. Make a version with a window around the current time, for example it starts 2 minutes ago and ends in 5 minutes. Read `get_device_status` and `infoslides device now` during the window and after it. | During: `nowPlaying` is the copy and the PNG is dark. After: `nowPlaying` is the original. |
| B9 | Agent | `evening` on a slideshow with no designed slides. | `ValidationFailed` with a reason in plain words. Nothing created. |
| B10 | Agent | `--evening-start 09:00 --morning-end 09:00`. | `ValidationFailed` about equal times. Nothing created. |
| B11 | Agent | `--evening-start 25:00`, then `--morning-end abc`. | `ValidationFailed` naming `eveningStart`, then `morningEnd`. Nothing created. |
| B12 | Agent | Add a timed schedule entry at 19:00 on the screen, then `evening`. | `ValidationFailed` naming the screen. Nothing created. |
| B13 | Agent | Free plan workspace with 4 slideshows: run the offer, then `evening`. | Offer has `atSlideshowLimit` true. `evening` is refused. Nothing created. |
| B14 | Manual | Confirm and lock a brand guide on the Brand page, then run the offer and `evening`. Manual because a person has to confirm a guide on that page; agents can only draft one. | Offer has `brandLocked` true. `evening` is refused. Nothing created. |
| B15 | Agent | Run `evening` on a slideshow id from the other workspace. | `NotFound`. Nothing created in either workspace. |
| B16 | Agent | Run `evening` with a push key from `create_source_key` in place of the admin key. | Refused. Nothing created. |

### Check a slideshow

Use a fresh healthy slideshow for each finding, or undo the previous change first. Start with P1 so a clean result is known.

| ID | Who | Test | Expected |
| --- | --- | --- | --- |
| P1 | Agent | `check_slideshow` on a healthy slideshow. | `ready` is true, no findings. Timed: an answer within about a minute. |
| P2 | Agent | Delete with `delete_media` a media file a slide uses, then check. | `MediaMissing`, severity error, `ready` false, with a `message` and a `fix`. |
| P3 | Agent | Push items with a past expiry to the source of a live data slide, then check. | `ContentExpired` warning naming the source. `ready` stays true. |
| P4 | Agent | `set_slide_conditions` with `date=2026-10-01..2026-10-31`, mode show, then check. | `SlideExpired` warning. |
| P5 | Agent | Set the screen playing a landscape slideshow to 1080x1920 with `update_device`, then check. | `AspectMismatch` warning naming the screen and both sizes. |
| P6 | Agent | Put a very long title on a designed slide, then check. | `TextOverflow` warning with the slide number. |
| P7 | Agent | Make a designed slide's text small, and separately upload a PowerPoint with 8 pt text, then check each. | `TextTooSmall` warning for each. |
| P8 | Agent | Check a slideshow with more than 25 designed or live data slides. | `TextNotFullyChecked` warning saying how many were measured. |
| P9 | Agent | Read the `check_slideshow` description in `tools/list`. | Lists the new checks and says it can take some seconds. Does not say it skips text size. |

### Takeover list

| ID | Who | Test | Expected |
| --- | --- | --- | --- |
| T1 | Agent | Create two takeovers, end one, call `list_takeovers`. | `items`, `total` 2, `page` 1, `pageSize` 50, `totalPages` 1. Latest first. |
| T2 | Agent | `infoslides takeover list`, then with `--active`, each with and without `--json`. | No crash. `--active` shows only the running takeover and its `total` is 1. |

### Saved sign-in

Each test uses a new empty home folder and creates a real workspace on the local API.

| ID | Who | Test | Expected |
| --- | --- | --- | --- |
| K1 | Agent | `infoslides tenant create "Test" owner@test.test --save` with nothing saved. | `credentials.json` holds the new key. No "was kept" message. |
| K2 | Agent | Save an API key first, then run K1. | `credentials.json` is unchanged. `credentials-{tenantId}.json` holds the new key. Standard error names that path and does not contain the key. |
| K3 | Agent | After K2, run `infoslides tenant info`, then again with `--api-key` set to the key from the new file. | First shows the old workspace, second shows the new one. |
| K4 | Manual | On your own machine, sign in with `infoslides login`, run `tenant create ... --save`, then `tenant info`. Manual because the login happens in your browser with your account. | Still signed in to your workspace. The message names the new file. |

## Stage C: after the website API is deployed

The hosted server and every released client are still 1.9.0 here. Use a test workspace.

| ID | Who | Test | Expected |
| --- | --- | --- | --- |
| C1 | Agent | Read `https://infoslides.app/v1/openapi.json`. | Has the `evening-version` paths. |
| C2 | Agent | With the v1.9.0 release binary, run `slideshow list`, `takeover list` and `tenant info`. | All work. `takeover list` returns the new page shape. |
| C3 | Agent | Through the 1.9.0 hosted server, call `check_slideshow` and `list_takeovers`. | Both work and show the new findings and shape. |
| C4 | Agent | Repeat B3, B9 and an undo against production with the API directly (`POST /v1/slideshows/{id}/evening-version`). | Same results as locally. |
| C5 | Manual | On a real TV playing in HLS, set an evening window a few minutes ahead with `evening`. Watch it. Manual because it needs a physical screen. | Switches to the dark copy at the start time and back at the end, with no black gap beyond a normal slide change. |
| C6 | Manual | Same on a screen in HTML mode (browser pair page, Android 1.2.0+ or LG 1.3.0+). | Same result. |
| C7 | Manual | Stand where a viewer would and read the dark slides on the TV, at evening light if you can. Manual because it is a judgement by eye. | Text is readable and photos are not too dark. |

## Stage D: release files

Run after the tag has built and you have approved the release.

| ID | Who | Test | Expected |
| --- | --- | --- | --- |
| D1 | Manual | Tag, push and approve the `release` environment. Manual because commits, pushes and approvals are yours. | Release v1.10.0 exists with all files listed in `PUBLISHING.md`. |
| D2 | Agent | Download the release files and run `sha256sum -c sha256sums.txt`. | Every file matches. |
| D3 | Agent | `gh attestation verify <file> --repo arnibj/InfoSlides.MCP` for the archives, bundle and `server.json`. | All verified. |
| D4 | Agent | `npx @anthropic-ai/mcpb validate` on the bundle's `manifest.json`. | Valid. |
| D5 | Agent | With the published Windows binary, repeat B3, an undo, P1 and K2. The binary is Native AOT, so it can fail where the `dotnet test` build passes. | Same results as locally. |
| D6 | Agent | Same with the Linux binary in a Docker container, plus `ls -l` on `~/.infoslides/credentials-*.json`. | Same results. File mode is `-rw-------`. |
| D7 | Manual | Same with the macOS arm64 binary. Manual because it needs a Mac. | Same results. |
| D8 | Manual | Install the `.mcpb` from the release in a real MCP client and ask it to make an evening version. Manual because installing happens in the client's own screens. `PUBLISHING.md` makes this step mandatory. | The server starts and the tool runs. |

## Stage E: hosted server

Run after `./deploy/deploy-mcp.ps1`. The tool count in `deploy/README.md` (72) is out of date; use the numbers below.

| ID | Who | Test | Expected |
| --- | --- | --- | --- |
| E1 | Agent | The unauthenticated `initialize` from `deploy/README.md`. | 401 with `WWW-Authenticate` and `resource_metadata`. |
| E2 | Agent | `./scripts/check-release-sync.ps1` with `INFOSLIDES_API_KEY` set. | Every line PASS, after the registry publish in E10. Before it, only the registry line may fail. |
| E3 | Agent | Read `https://infoslides.app/.well-known/mcp/server-card.json`. | Version 1.10.0, 51 tools, has `make_evening_version`, no `get_evening_version_offer`. |
| E4 | Agent | `tools/list` on `/mcp` with an API key. | 118 tools, with both evening tools. |
| E5 | Manual | Refresh or reconnect the InfoSlides connector in Claude and sign in. Manual because the sign-in is yours. | The connector lists `make_evening_version`, not the offer tool. |
| E6 | Agent | After E5, in a new Claude session on a test workspace: run `make_evening_version` and `undo_change` through the connector. | Result names the copy and the times on each screen. The undo removes the copy. |
| E7 | Agent | In a new session with only the connector or the skill, ask in plain words: "Make a dark version of the lunch menu that shows after closing." | It reaches for `make_evening_version` and does not clone and schedule by hand. |
| E8 | Manual | In ChatGPT, connect and make an evening version. Manual because it runs in ChatGPT's app with your account. | Tool is listed and works. |
| E9 | Manual | Same in Microsoft 365 Copilot, and in Muse. | Tool is listed and works. |
| E10 | Manual | Run **Publish to MCP Registry** and refresh the tool list and descriptions in the Claude, ChatGPT and Microsoft submissions. Manual because these are public listings in your accounts. | E2 passes in full. Listings show `make_evening_version` and the new `check_slideshow` text. |

## Stage F: cleanup

| ID | Who | Test | Expected |
| --- | --- | --- | --- |
| F1 | Agent | Delete the test slideshows, screens and media, and the temp home folders. | Test workspaces are empty. |
| F2 | Manual | Delete the test workspaces from the admin panel, production ones in particular. Manual because there is no agent route for it. | They are gone from `/admin/tenants`. |

## Summary

| Stage | Agent | Manual |
| --- | --- | --- |
| A. Before commit | 6 | 0 |
| B. Local end to end | 29 | 2 |
| C. After website deploy | 4 | 3 |
| D. Release files | 5 | 3 |
| E. Hosted server | 6 | 4 |
| F. Cleanup | 1 | 1 |
| **Total** | **51** | **13** |
