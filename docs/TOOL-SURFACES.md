# Which tools each surface has

APX-70. A new `/v1` route gets an MCP tool and a CLI verb at once, but the hosted directories (ChatGPT, Claude,
Microsoft 365 Copilot, Muse) only get a tool when someone adds it to `HostedToolProfile.ToolNames` with hosted
wording. This file is the record of every tool that is **not** hosted and why, so a gap is always a decision.
`ToolSurfaceTrackingTests` fails when a registered tool is neither hosted nor a row below, when a row names a tool
that does not exist or is already hosted, or when a row's reason is not one of the keys in the next table.

## Surfaces

| Surface | Gets |
| --- | --- |
| Local stdio server and CLI | Every tool. They have no list to keep in step. |
| Hosted (ChatGPT, Claude, Copilot) | `HostedToolProfile.ToolNames`, with hosted descriptions and `hosted-rules.json`. |
| Muse | The hosted list minus `HostedToolProfile.MuseExcluded` (the three AI Studio tools). |
| Skill text | One source: the infoslides repo's `skill-source`. `python scripts/sync-skills.py` copies it to `skills/` (local wording) and `plugins/claude/skills/` (hosted wording); never edit those copies by hand. |

## Reasons a tool is left out of the hosted profile

| Key | Meaning |
| --- | --- |
| `ai` | Calls another AI model behind the scenes to make slides. |
| `billing` | Touches billing, payment or a trial; directories reject software that executes financial transactions or promotes products. |
| `credentials` | Gives direct API access or creates a credential that outlives the connection. |
| `account` | Creates an account or changes who has access to a workspace. |
| `local-file` | Reads a file from the caller's disk; a hosted caller has none. |
| `planned` | Decided to be hosted later, for every hosted agent; waits for hosted wording and skill text. |
| `undecided` | Not yet given hosted wording and nobody has decided. Each is a decision for the product owner; moving one into the hosted profile follows the checklist in [PUBLISHING.md](PUBLISHING.md#adding-or-changing-a-tool). |

## Tools that are local server and CLI only

Hosted wording goes through `docs/hosted-directory-rules.md`. When a tool in a row below reaches the hosted profile,
delete its row in the same change.

| Tool | Reason | Note |
| --- | --- | --- |
| `create_api_key` | `credentials` | |
| `list_api_keys` | `credentials` | |
| `revoke_api_key` | `credentials` | |
| `create_source_key` | `credentials` | A push key outlives the connection, so a hosted caller may not mint one. |
| `get_billing` | `billing` | Read-only plan, renewal and usage. Left out because it is billing. |
| `upgrade_subscription` | `billing` | |
| `start_ai_studio_trial` | `billing` | |
| `leave_testimonial` | `billing` | Promotional; excluded with the commerce tools (OAI-06). |
| `create_tenant` | `account` | |
| `resend_verification_email` | `account` | |
| `invite_team_member` | `account` | |
| `remove_team_member` | `account` | |
| `list_team` | `account` | |
| `revoke_team_invitation` | `account` | |
| `deactivate_member` | `account` | |
| `reactivate_member` | `account` | |
| `upload_media` | `local-file` | |
| `upload_pptx` | `local-file` | |
| `upload_slideshow` | `local-file` | The alias of `upload_pptx`. |
| `replace_slideshow_file` | `local-file` | |
| `upload_brand_logo` | `local-file` | |
| `fetch_source_now` | `planned` | APX-59, decided 2026-10-16. |
| `get_source_items` | `planned` | APX-59, decided 2026-10-16. |
| `test_source_config` | `planned` | APX-59, decided 2026-10-16. |
| `get_brand_guide` | `undecided` | Good candidate: a hosted agent that makes slides should read the guide. Hosting it also updates `BrandGuide.razor` (APX-70). |
| `preview_brand` | `undecided` | Good candidate, same reason. |
| `update_brand_guide` | `undecided` | Drafts only; an agent cannot confirm. |
| `check_brand` | `undecided` | |
| `list_brand_previews` | `undecided` | |
| `add_weather_alert_timeline` | `undecided` | |
| `clone_template` | `undecided` | |
| `create_broadcast_device` | `undecided` | |
| `create_media_folder` | `undecided` | |
| `create_source` | `undecided` | |
| `create_template` | `undecided` | |
| `delete_device` | `undecided` | |
| `delete_media` | `undecided` | |
| `delete_media_folder` | `undecided` | |
| `delete_source` | `undecided` | |
| `delete_template` | `undecided` | |
| `get_audit_trail` | `undecided` | |
| `get_device_plays` | `undecided` | |
| `get_error_log` | `undecided` | |
| `get_media` | `undecided` | |
| `get_offline_alerts` | `undecided` | |
| `get_slide_design` | `undecided` | |
| `get_slideshow_plays` | `undecided` | |
| `get_source_fields` | `undecided` | |
| `get_stream_link` | `undecided` | The link opens the stream without signing in. |
| `get_template` | `undecided` | |
| `get_template_usage` | `undecided` | |
| `get_upload_limits` | `undecided` | |
| `import_google_slides` | `undecided` | Takes the caller's own Google access token. |
| `import_slide` | `undecided` | |
| `list_adapters` | `undecided` | |
| `list_media` | `undecided` | |
| `list_media_folders` | `undecided` | |
| `list_shared_slideshows` | `undecided` | |
| `list_takeovers` | `undecided` | |
| `list_template_categories` | `undecided` | |
| `list_templates` | `undecided` | |
| `preview_new_template` | `undecided` | |
| `regenerate_template_thumbnail` | `undecided` | |
| `rename_media_folder` | `undecided` | |
| `report_issue` | `undecided` | |
| `restore_slideshow` | `undecided` | |
| `search_stock_images` | `undecided` | Unsplash attribution and download-ping rules apply; `add_designed_slide` hides `stockPhoto` until this is hosted. |
| `set_device_offline_alerts` | `undecided` | |
| `set_workspace_offline_alerts` | `undecided` | |
| `update_media` | `undecided` | |
| `update_schedule_entry` | `undecided` | |
| `update_slide_design` | `undecided` | |
| `update_source_settings` | `undecided` | |
| `update_template` | `undecided` | |
| `update_tenant` | `undecided` | |
