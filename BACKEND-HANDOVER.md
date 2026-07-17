# Backend Implementation Handover — InfoSlides Agent API

**Audience:** the session/agent implementing the `/v1` agent API in the `arnibj/InfoSlides`
repository. That session should start with this document, [API-CONTRACT.md](API-CONTRACT.md)
(the wire-level source of truth) and [BACKEND-CHANGES.md](BACKEND-CHANGES.md) (the work
checklist). This document adds everything those two don't carry: current state, exact client
behavior the backend must match, a step-by-step verification recipe using the already-built
client, open decisions, and the protocol for changing the contract.

---

## 1. Where things stand

- `arnibj/InfoSlides.MCP` (`main`) contains a **finished, tested client**: one Native AOT
  executable `infoslides` that is a developer CLI and, with `--mcp`, an MCP stdio server
  exposing **24 tools**. 39 tests pass, including end-to-end MCP tests against a fake backend.
  CI builds linux-x64 / win-x64 / osx-arm64 binaries; tagging `v*` publishes a GitHub release.
- The **backend endpoints do not exist yet** — every endpoint in API-CONTRACT.md is marked ⏳.
  The client fails gracefully against the real backend until they ship.
- The MCP/CLI side needs **no changes** to go live, *except* whatever falls out of the gap
  analysis below (§2) and the contract-change protocol (§8).

The product blueprint (GTM/PRD "infoslides_complete_gtm_blueprint_with_conditions") defined the
journey this enables: anonymous `create_tenant` → admin API key → content + device + schedule →
live HLS stream, with Paddle upgrade unlocking AI templates, `update_source` and unlimited
devices. The backend is the **sole enforcement point** for all of that — the client exposes
every tool in every state and surfaces backend errors/warnings verbatim.

## 2. Step 0 — gap analysis against the real code (do this first)

The contract was written **without access to the InfoSlides source** (repo access was blocked
in the authoring session). Before implementing anything, reconcile it against reality:

1. **Slide ordering & duration** — does the data model already have per-slide duration and an
   ordering mechanism? Contract assumes `durationSeconds` + `position` on slide-add and slide
   order via `PATCH /v1/slideshows/{id}`.
2. **Condition engine** — what are the *actual* serialized shapes of time / weekday /
   data-trigger conditions? Contract assumes `{ "type": "time"|"weekday"|"data_trigger",
   "value": "08:00-11:00" }`. Map the contract shape onto the real engine; don't invent a
   second engine.
3. **Timezone semantics** for time/weekday conditions — device-local or tenant-default?
   Document the answer in API-CONTRACT.md §4.2 whichever way it goes.
4. **Media library** — are media assets first-class (own table, reusable across slides)? If
   yes, consider `GET/DELETE /v1/media` for v1.1 and whether `mediaUrl` on slide-add should
   accept a media id.
5. **Devices** — do device groups / playlists / recurrence schedules exist? Contract v1 only
   models a flat `slideshowIds` assignment per device.
6. **Existing REST controllers** — anything already covering these surfaces should be reused /
   aliased under `/v1` behind the new envelope, not duplicated.
7. **HLS URL format** — what does a real stream URL look like, and can it be signed/expiring
   (`expiresAt` in the contract)?
8. **Paddle + user state** — confirm `IsEmailVerified` and `SubscriptionLevel` field names and
   where the Paddle webhook already syncs subscription state.

Where the real code contradicts the contract, **prefer changing the contract** (it's a draft
for backend review) — but every contract change must flow back into the client (§8).

## 3. Wire-level behavior the client already implements

These are hard requirements. The client is built and tested against exactly this; deviations
break it. When in doubt, `tests/InfoSlides.Mcp.Tests/FakeBackend.cs` in this repo is an
executable specification of the shapes the client expects.

### 3.1 Requests

- `Authorization: Bearer <credential>` on everything except the anonymous endpoints
  (`POST /v1/tenants`, `POST /v1/auth/cli/exchange`; `GET /v1/auth/cli/start` is browser
  navigation). Without a credential the client fails those calls **client-side** — the backend
  will simply never see unauthenticated calls to protected endpoints from this client.
- JSON bodies are camelCase; the client **omits null properties** entirely (e.g. a
  `CreateTemplateRequest` in prompt-mode carries no `html`/`css` keys at all).
- `Idempotency-Key: <random GUID>` is sent automatically on these POSTs:
  `/v1/tenants`, `/v1/slideshows`, `/v1/slideshows/{id}/slides`, `/v1/slideshows/{id}/clone`,
  `/v1/gallery/{id}/clone`, `/v1/templates`, `/v1/devices`, `/v1/apikeys`. Replays within 24h
  must return the original response.
- `dryRun=true` is a **query parameter** on `POST /v1/templates?dryRun=true` and
  `POST /v1/slides/{id}/source?dryRun=true`.

### 3.2 Responses

- Envelope on every JSON endpoint: success `{ "data": ..., "warnings": [...] }`, error
  `{ "error": { "code", "message", "details": { ... } } }`. `warnings` may be omitted.
- For action endpoints with nothing to return (`resend-verification`, `conditions`, `source`,
  `apikeys DELETE`), the client accepts `{ "data": {} }`, `{}`, or a missing `data` — all are
  treated as success.
- `error.details.upgradeUrl` is surfaced to agents on `EntitlementRequired` — always include a
  live Paddle checkout URL there.
- `AspectMismatch` is a **warning on a 2xx response**, never an error. Unknown warning codes
  are passed through to agents untouched, so new warnings can be added backend-first.
- `GET /v1/slides/{id}/preview.png` returns **raw `image/png` bytes, not the JSON envelope**
  (errors from it still use the JSON error envelope with a 4xx/5xx status).
- Timestamps RFC 3339 UTC; property names camelCase.

### 3.3 Exact endpoint surface the client calls

| Client call | Method & path |
| --- | --- |
| create_tenant | `POST /v1/tenants` (anonymous) |
| get_tenant_info | `GET /v1/tenant` |
| resend_verification_email | `POST /v1/auth/resend-verification` |
| login (browser) | `GET /v1/auth/cli/start?provider&state&codeChallenge&redirectUri` |
| login (exchange) | `POST /v1/auth/cli/exchange` (anonymous) |
| upload_slideshow | `POST /v1/slideshows` |
| list_slideshows | `GET /v1/slideshows` |
| get_slideshow | `GET /v1/slideshows/{id}` |
| update_slideshow | `PATCH /v1/slideshows/{id}` |
| clone_slideshow | `POST /v1/slideshows/{id}/clone` |
| clone_slideshow (fromGallery) | `POST /v1/gallery/{id}/clone` |
| list_gallery | `GET /v1/gallery` |
| add_media_slide | `POST /v1/slideshows/{id}/slides` |
| set_slide_conditions | `PUT /v1/slides/{id}/conditions` |
| update_source | `POST /v1/slides/{id}/source[?dryRun=true]` |
| preview_slide | `GET /v1/slides/{id}/preview.png` |
| create_template | `POST /v1/templates[?dryRun=true]` |
| list_templates | `GET /v1/templates` |
| create_device | `POST /v1/devices` |
| list_devices | `GET /v1/devices` |
| get_device_status | `GET /v1/devices/{id}/status` |
| assign_schedule | `POST /v1/devices/{id}/schedule` |
| get_stream_link | `GET /v1/devices/{id}/stream` |
| create_api_key | `POST /v1/apikeys` |
| list_api_keys | `GET /v1/apikeys` |
| revoke_api_key | `DELETE /v1/apikeys/{id}` |
| upgrade_subscription | `POST /v1/billing/checkout` |

Exact request/response record shapes: `src/InfoSlides.Core/Models/*.cs` (sealed records, one
file per domain). Treat those files as the authoritative field lists.

### 3.4 CLI OAuth loopback flow (contract §4.1)

1. CLI generates `state` + PKCE verifier, computes S256 `codeChallenge`
   (base64url, **no padding**), starts an `HttpListener` on
   `http://localhost:{port}/callback` — port 5000 default, fallbacks **5013** and **53682**
   (macOS AirPlay squats 5000). All three redirect URIs must be registered/allowed.
2. Browser opens `GET {api}/v1/auth/cli/start?provider=google|microsoft|github&state=...&codeChallenge=...&redirectUri=...`.
   The backend brokers the IdP (secrets stay server-side) and 302s back to
   `{redirectUri}?code=...&state=...`. Codes are single-use, 5-minute TTL.
3. CLI validates `state`, then `POST /v1/auth/cli/exchange` with
   `{ "code", "codeVerifier" }` → `{ "sessionToken", "expiresAt", "tenantId", "email" }`.
   The client ignores expired session tokens locally, so `expiresAt` must be accurate.

### 3.5 API keys & gatekeeping

- Key format: `isk_admin_<random>` / `isk_dp_<random>`. Store only a hash; `KeyPrefix` (first
  12 chars) is what `GET /v1/apikeys` returns for display. Plaintext is returned exactly once,
  from `POST /v1/tenants` (primary admin key) and `POST /v1/apikeys`.
- `isk_dp_` keys: only `POST /v1/slides/{id}/source`, only for their `BoundSlideIds`;
  everything else → `KeyScopeViolation` (403).
- State gates (from existing `IsEmailVerified` / `SubscriptionLevel`):
  ANONYMOUS → `POST /v1/tenants` only; VERIFIED FREE → content + **1 active device** +
  streaming; PRO/ENTERPRISE → AI templates, `update_source`, unlimited devices. Error codes:
  `EmailNotVerified`, `EntitlementRequired` (+ `upgradeUrl`), `DeviceLimitReached`.

## 4. Suggested implementation order

BACKEND-CHANGES.md is the checklist; this ordering keeps every step verifiable with the client:

1. **Foundation** — envelope middleware (+ error codes), `TenantApiKeys` table + bearer auth
   handler + scope middleware, `Idempotency-Key` replay cache.
   *Verify:* any stub endpoint returns the envelope; bogus key → `Unauthorized`.
2. **Tenancy** — `POST /v1/tenants`, `GET /v1/tenant`, `POST /v1/auth/resend-verification`,
   `POST|GET|DELETE /v1/apikeys`. *Verify:* CLI flow in §5 steps 1–4 works end to end.
3. **Content** — slideshows CRUD/clone, media slides, conditions (wired to the existing
   engine), gallery seed + clone.
4. **Live data** — `POST /v1/slides/{id}/source` (+ dryRun schema validation against the
   template's `sampleJson`) triggering re-render; `GET /v1/slides/{id}/preview.png` (single
   frame from the existing HLS renderer); templates (Premium-gated, dual-mode).
5. **Devices & streams** — device CRUD + free-tier limit, heartbeat → status, schedule assign
   with aspect check → `AspectMismatch` warning, stream link.
6. **Billing** — `POST /v1/billing/checkout` + Paddle webhook sync (partially exists).
7. **OAuth CLI broker** — `GET /v1/auth/cli/start` + `POST /v1/auth/cli/exchange`. Last on
   purpose: API keys make everything else usable without it.

## 5. Verification recipe (use the real client, not curl)

Build the client from `arnibj/InfoSlides.MCP` (or grab a CI artifact):

```sh
dotnet publish src/InfoSlides.Cli -c Release -r linux-x64   # → publish/infoslides
export INFOSLIDES_API_URL=http://localhost:5013             # your local backend
```

Then walk the golden path — this exact sequence was dogfooded against the fake backend:

```sh
infoslides tenant create "Acme Cafe" owner@acme.test --save   # 1 anonymous create; key saved
infoslides tenant info                                        # 2 whoami: quota, entitlements
infoslides key create --name ci --type admin                  # 3 key mgmt
infoslides key list                                           # 4 shows prefix + lastUsedAt
infoslides gallery list                                       # 5
infoslides slideshow clone <gallery-id> --from-gallery        # 6
infoslides device create "Lobby" --width 1080 --height 1920   # 7
infoslides schedule assign <device-id> <slideshow-id>         # 8 expect AspectMismatch warning
                                                              #   on stderr, exit code 0
infoslides stream link <device-id> --json                     # 9 hlsUrl plays in a player
infoslides device status <device-id>                          # 10 online/lastSeen/nowPlaying
infoslides slide preview <slide-id> --output slide.png        # 11 a real PNG
infoslides source update <slide-id> --data @data.json --dry-run   # 12 validation only
infoslides billing upgrade                                    # 13 live Paddle checkout URL
```

Negative paths to verify explicitly:

- `isk_dp_` key calling anything but `source update` → `KeyScopeViolation`.
- `isk_dp_` key pushing to a slide outside its `BoundSlideIds` → `KeyScopeViolation`.
- Unverified tenant creating a device → `EmailNotVerified`.
- Free tenant creating a 2nd device → `DeviceLimitReached`.
- Free tenant creating a template → `EntitlementRequired` with a working `upgradeUrl`.
- Replayed `Idempotency-Key` on `POST /v1/devices` → same device, `Idempotent-Replay: true`.

MCP mode: `infoslides mcp install --client claude-code` inside a project, then from Claude
Code run `create_tenant` → … → `get_stream_link` conversationally. The MCP tool results embed
the same envelope, so any backend deviation shows up immediately as a malformed tool result.

## 6. Things the backend must NOT do

- Don't return errors for aspect mismatches — it's a warning on success (agents self-correct).
- Don't return the JSON envelope from `preview.png` success responses (raw PNG bytes).
- Don't return full API keys from `GET /v1/apikeys` — prefix + metadata only.
- Don't ship IdP client secrets to the CLI — the backend brokers Google/Microsoft/GitHub.
- Don't rename/repurpose contract fields silently — see §8.

## 7. Open product decisions (need the owner's call)

1. Timezone semantics for time/weekday conditions (device-local vs tenant-default) — §2.3.
2. Slide reordering: `PATCH /v1/slideshows/{id}` with a full `slideOrder` array vs a dedicated
   reorder endpoint, depending on what the model supports — §2.1.
3. Media library exposure in v1.1 (`GET/DELETE /v1/media`) — §2.4.
4. Stream link security: signed/expiring URLs vs stable public URLs (`expiresAt` is nullable
   in the client model, so both work wire-wise).

## 8. Contract-change protocol

API-CONTRACT.md lives in `arnibj/InfoSlides.MCP` and is the single source of truth. When the
gap analysis (or implementation reality) forces a change:

1. PR the change to API-CONTRACT.md **first**, with the reason.
2. Mirror it in the client, all in this repo: the record in `src/InfoSlides.Core/Models/`,
   a `[JsonSerializable]` registration in `Serialization/InfoSlidesJsonContext.cs` for any
   **new** type (a coverage test fails if forgotten), `Api/InfoSlidesApiClient.cs`, the tool in
   `src/InfoSlides.Cli/Tools/`, the CLI verb in `Commands/CommandTree.cs`, and the expected
   shapes in `tests/InfoSlides.Mcp.Tests/FakeBackend.cs`.
3. Client-side AOT rules when touching this repo: never use reflection-based JSON APIs, never
   `WithToolsFromAssembly()`; `dotnet build` treats AOT analyzer warnings as errors, and CI
   publishes + smoke-tests native binaries on all three OSes.

Purely additive backend changes (new optional response fields, new warning codes) need no
client release — the client passes unknowns through.

## 9. Suggested kickoff prompt for the backend session

> Read BACKEND-HANDOVER.md, API-CONTRACT.md and BACKEND-CHANGES.md from the
> `arnibj/InfoSlides.MCP` repo (main branch). First run the §2 gap analysis against this
> codebase and report findings + any contract changes you propose, with answers to the §7
> open questions where the code already decides them. Then implement the `/v1` agent API in
> the §4 order, verifying each stage with the recipe in §5.
