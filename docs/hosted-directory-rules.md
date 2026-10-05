# Rules for the hosted tool text

The hosted server (`https://infoslides.app/mcp`) is listed in assistant directories. Their review rules shape what tool text may say and how tool parameters are described. This file records the rules we follow, the code that enforces them, and why. The local stdio server and the plain API are not affected.

## Where the rules live

| What | Where |
|---|---|
| Which tools a hosted caller sees | `HostedToolProfile.ToolNames` in `src/InfoSlides.Mcp.Tools/ToolProfile.cs`, mirrored in `Instructions/hosted-rules.json` |
| Hosted wording for each tool | `HostedToolProfile.DescriptionOverrides` (same file) |
| Hosted wording for single parameters | `HostedToolProfile.ParameterDescriptionOverrides` |
| Applied per request | `Decorate` in `src/InfoSlides.McpServer/ToolProfileFilters.cs` |
| Words the hosted text may not use | `forbiddenTerms` and `forbiddenPatterns` in `Instructions/hosted-rules.json` |
| Workflow guidance for the model | `Instructions/hosted.txt` (server instructions) and the hosted skill |

## Rule 1: every parameter has a JSON schema type

The Claude directory scan reports "Add a type to this parameter" for any parameter whose schema has no `type`. A `JsonElement` parameter produces a typeless `{}` schema, so tool parameters that carry JSON use `JsonObject` (`type: object`) or `JsonArray` (`type: array`) instead.

- Changed: `push_data.data`, `update_source.data`, `update_slide` (`fieldMapping`, `overrideData`, `countdown`), `create_template.sampleJson`, `create_source.config`, `update_source_settings.config`, `preview_new_template.sampleData`.
- `countdown` is now an array only. It used to accept a single object as well, and the description said so. A client that sent one object must wrap it in an array.
- The API client still takes a `JsonElement`. `ToolResults.ToElement` converts the typed argument. The CLI parses `--config` and `--sample-data` with `CliContext.ParseJsonObject`, which rejects anything that is not a JSON object.
- Guard: `ToolSchemaTypesTests` builds every tool's schema and fails if a parameter has none of `type`, `anyOf` or `enum`.

## Rule 2: hosted descriptions describe, they do not instruct

The directory submission requires the statement that tool descriptions contain no instructions about model behaviour, other tools or external instruction sources. The shared descriptions are written for the local server and include phrases such as "confirm with the person first", "use delete_slide instead", "poll every 5 seconds" and "say so, and suggest replacing the file". So the hosted profile overrides the description of every hosted tool with plain text: what the tool does, what it takes, what it returns, and what its error codes mean.

- A hosted description may not name another tool, and may not address the model ("you", "ask", "tell", "confirm", "poll", "should", "must", "please").
- Parameter texts follow the same rule. Where a shared parameter text named a tool ("ids from list_sources"), the hosted text is reworded.
- Behaviour guidance such as asking before a delete, offering undo, and polling AI slide jobs stays in the hosted server instructions and the skill, which are separate surfaces from tool descriptions.
- `list_devices` no longer mentions the `near` parameter. That parameter is hidden on the hosted profile, so the description pointed at something the model could not send.
- Guard: `HostedDescriptions_NameNoOtherToolAndGiveNoInstructions` in `McpHostProfileTests` lists the hosted tools over HTTP and fails on either rule.

## Adding or changing a hosted tool

1. Add the tool to `ToolNames` and `hosted-rules.json` (a test compares them).
2. Add its hosted wording to `DescriptionOverrides` if the shared description names another tool or gives the model instructions. The guard test tells you.
3. Give every JSON parameter a `JsonObject` or `JsonArray` type, never `JsonElement`.
4. Nothing in hosted text may mention prices, trials, billing or upgrades (`hosted-rules.json`).

## What is not covered

The hosted server instructions (`hosted.txt`) do tell the model how to work, because that is their purpose. If a directory reviewer reads the attestation as covering them too, trim them and move the guidance into the skill.
