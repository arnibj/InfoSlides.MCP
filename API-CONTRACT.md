# InfoSlides Agent API Contract (v1)

This document is the single source of truth for the agent-facing REST API consumed by the
`infoslides` CLI / MCP server. The InfoSlides backend implements this contract; the client in
this repository is generated against it. Changes go through PR review in both repos.

Status: **implemented** — the InfoSlides backend implements every endpoint below (2026-07-18,
branch `claude/mcp-endpoints-audit-83z4sm` in `arnibj/infoslides`). Rows marked ✅ are live.
The reconciliation against the real backend and all implementation decisions live in the
InfoSlides repo under `docs/mcp-agent-api/` (GAP-ANALYSIS.md, PROGRESS.md). Wire-visible
decisions recorded in this revision are all compatible with the already-shipped client
(nullable fields, tightened server-side validation, defined string grammars, additive
warnings) — no client release is required.

## 1. Conventions

- Base URL: `https://infoslides.app` (override: `INFOSLIDES_API_URL` / `--api-url`). Agent-API (`/v1/...`)
  controllers are mounted at the domain root — no `api.` subdomain and no `/api` path prefix (that
  prefix is reserved for the web-facing and TV-facing controllers).
- All endpoints are prefixed with `/v1`.
- Authentication: `Authorization: Bearer <credential>` where the credential is either
  - a **session token** obtained via the CLI OAuth loopback flow, or
  - a **tenant API key** (`isk_admin_...` or `isk_dp_...`) from the `TenantApiKeys` table.
- JSON bodies use **camelCase** property names. Timestamps are RFC 3339 UTC.
- **Idempotency**: all `POST` create endpoints honor an optional `Idempotency-Key` header
  (client-generated, ≤128 chars). Replays within 24h return the original response with
  `Idempotent-Replay: true`.
- The only anonymous endpoint is `POST /v1/tenants`. Everything else returns
  `401 Unauthorized` without a valid credential.

## 2. Response envelope

Success (2xx):

```json
{
  "data": { },
  "warnings": [
    { "code": "AspectMismatch", "message": "Slideshow is 1920x1080 but device is 1080x1920." }
  ]
}
```

Error (4xx/5xx):

```json
{
  "error": {
    "code": "EntitlementRequired",
    "message": "AI template generation requires a Premium subscription.",
    "details": { "upgradeUrl": "https://checkout.paddle.com/..." }
  }
}
```

`warnings` is optional and may be omitted when empty. Clients MUST pass unknown warning codes
through untouched (agents react to them).

### Error codes

| Code | HTTP | Meaning |
| --- | --- | --- |
| `Unauthorized` | 401 | Missing/invalid credential. |
| `Forbidden` | 403 | Valid credential, insufficient rights. |
| `EmailNotVerified` | 403 | Owner email not verified; remediation: `resend-verification`. |
| `EntitlementRequired` | 403 | Needs Premium; `details.upgradeUrl` carries a Paddle checkout link. |
| `DeviceLimitReached` | 403 | Free tier allows exactly 1 active device. |
| `KeyScopeViolation` | 403 | Data-provider key used outside `update_source` / its bound slide ids. |
| `ValidationFailed` | 400 | Schema/field validation error; `details.fields` lists offenders. |
| `NotFound` | 404 | Resource does not exist in this tenant. |
| `RateLimited` | 429 | Too many requests (also AI credit caps on template generation); `Retry-After` header set when rate-limited. |
| `InternalError` | 500 | Unexpected server error. |

### Warning codes

| Code | Meaning |
| --- | --- |
| `AspectMismatch` | Scheduled/uploaded content aspect ratio differs from target device. Request still succeeds. |
| `StreamNotReady` | Stream link returned, but no slideshow is assigned or its render hasn't completed — the manifest 404s until a render finishes. |

## 3. Credentials, user states & entitlements

API keys are rows in the backend `TenantApiKeys` table:

| Key type | Prefix | Scope |
| --- | --- | --- |
| Admin | `isk_admin_` | Full tenant access: devices, schedules, content, keys, settings. |
| Data Provider | `isk_dp_` | Hard-bound to specific `slideIds`; may ONLY call `POST /v1/slides/{id}/source`. |

Gatekeeping (backend mapping): email verification lives on the tenant owner's identity
(`Identity.EmailVerified`; OAuth-created owners count as verified since the IdP asserted the
email). `subscriptionLevel` is the license tier name: `Starter` (free), `Professional` (PRO),
`Business` (ENTERPRISE). Device limits are per-tenant (`deviceQuota.max`; 1 on the free tier).
`update_source` requires Professional+; templates (both modes) require the AI Studio
entitlement (Premium plans).

| State | Condition | Allowed |
| --- | --- | --- |
| ANONYMOUS | no credential | `POST /v1/tenants` only |
| VERIFIED FREE | owner email verified, `Starter` tier | content + 1 active device + streaming |
| PRO / ENTERPRISE | active Paddle subscription (`Professional` / `Business`) | everything, unlimited devices, AI templates, `update_source` |

The backend is the sole enforcement point; the client exposes every tool regardless of state and
surfaces backend errors/warnings verbatim.

## 4. Endpoints

### 4.1 Tenants & auth

| Method & path | Auth | Notes |
| --- | --- | --- |
| `POST /v1/tenants` ✅ | none | Body `{ "tenantName", "ownerEmail" }` → `{ "tenantId", "apiKey": "isk_admin_...", "verificationEmailSent": true }`. Honors `Idempotency-Key`. |
| `POST /v1/auth/resend-verification` ✅ | any | Re-sends the owner verification email. |
| `GET /v1/tenant` ✅ | any | "whoami": `{ "tenantId", "name", "ownerEmail", "isEmailVerified", "subscriptionLevel", "deviceQuota": { "used", "max" }, "keyScope": { "type", "slideIds" } }`. `keyScope` = `{ "type": "admin", "slideIds": null }` for API-key callers, `null` for session tokens. |
| `GET /v1/auth/cli/start` ✅ | none | Browser entry (**GET**, not POST — the client always navigated a GET URL; the earlier POST in this table was an error). Query: `provider=google\|microsoft\|github` (`apple` additive when configured), `state`, `codeChallenge` (PKCE S256), `redirectUri` (loopback; registered set: `http://localhost:5000/callback`, `:5013`, `:53682`; `127.0.0.1` equivalents accepted). Backend brokers the IdP and 302s to `redirectUri?code=...&state=...`. Codes are single-use, 5-minute TTL. |
| `POST /v1/auth/cli/exchange` ✅ | none | Body `{ "code", "codeVerifier" }` → `{ "sessionToken", "expiresAt", "tenantId", "email" }`. |

### 4.2 Slideshows & slides

| Method & path | Auth | Notes |
| --- | --- | --- |
| `POST /v1/slideshows` ✅ | admin | Create/upload. Body `{ "title", "resolution": {"width","height"}, "slides": [...] }`. Default resolution 1920x1080; portrait supported. `resolution` (here and on devices) is snapped to the nearest supported preset — 1920x1080, 1280x720, 1080x1920, 720x1280, 1080x1080 — and responses echo the snapped value. `durationSeconds` are whole seconds; fractional input is rounded. Does **not** accept a raw `.pptx` file — use `POST /v1/slideshows/pptx` for that. |
| `POST /v1/slideshows/pptx` ✅ | admin | Creates a slideshow from an uploaded `.pptx`. `multipart/form-data`: a `file` part plus an optional `title` form field (falls back to the file name). Parses slide count and native resolution server-side and queues thumbnail/stream rendering, same pipeline as the web app's upload. Honors `Idempotency-Key`. |
| `GET /v1/slideshows` ✅ | admin | List (paged: `?page=&pageSize=`). |
| `GET /v1/slideshows/{id}` ✅ | admin | Full slideshow incl. slides, conditions, resolution. |
| `PATCH /v1/slideshows/{id}` ✅ | admin | Partial update (title, resolution, slide order). |
| `POST /v1/slideshows/{id}/clone` ✅ | admin | Clone an existing slideshow. |
| `POST /v1/slideshows/{id}/slides` ✅ | admin | Add media slide. Body `{ "mediaUrl" }` (downloaded server-side) **or** `{ "mediaAssetId" }` (an id already in the tenant's media library, e.g. from `POST /v1/media` — no download involved); exactly one of the two, plus optional `durationSeconds`, `position`. May return `AspectMismatch`. |
| `POST /v1/media` ✅ | admin | Uploads a file directly into the tenant's media library. `multipart/form-data`: a single `file` part. → `{ "id", "fileType": "image"\|"video"\|"document", "width", "height" }` (`width`/`height` only for images). Pass `id` as `mediaAssetId` to `POST /v1/slideshows/{id}/slides` to add it as a slide without a publicly reachable URL. Honors `Idempotency-Key`. |
| `PUT /v1/slides/{id}/conditions` ✅ | admin | Replace visibility conditions. Body `{ "conditions": [{ "type": "time"\|"weekday"\|"data_trigger", "value": "..." }] }`. Value grammars: `time` = `"HH:mm-HH:mm"`; `weekday` = comma-separated English day names (`"Monday,Tuesday"`); `data_trigger` = `"{contentSourceId}"` (has items) or `"{contentSourceId}:contains:{text}"` (the source must be linked to the slideshow). `time`/`weekday` evaluate in the **tenant's workspace timezone** (device-local timezones are not supported). Evaluated server-side during HLS rendering. |
| `POST /v1/slideshows/{id}/slides/dynamic` ✅ | admin | Add a template-driven dynamic slide to a slideshow. Body `{ "templateId", "durationSeconds"?, "position"? }`. `templateId` must reference a visible (global or tenant-owned) template — see `POST /v1/templates`. The new slide starts with no content source and empty override data; push initial/ongoing values with `POST /v1/slides/{id}/source`. Honors `Idempotency-Key`. |
| `POST /v1/slides/{id}/source` ✅ | admin or data-provider (bound) | Push JSON matching the template's data schema; triggers server-side re-render. `?dryRun=true` validates without rendering. Requires Professional+ (`EntitlementRequired` otherwise). Only dynamic (template-driven) slides accept data. **Only endpoint valid for `isk_dp_` keys.** |
| `GET /v1/slides/{id}/preview.png` ✅ | admin | Rendered PNG of the slide's current state (agent self-verification). Non-PNG image slides are re-encoded to PNG; video/document slides return `ValidationFailed`. |

### 4.3 Gallery

| Method & path | Auth | Notes |
| --- | --- | --- |
| `GET /v1/gallery` ✅ | any | Starter gallery of pre-built decks: `[{ "id", "title", "description", "previewUrl", "resolution" }]`. `description` is always null in v1 (no such field on decks yet). |
| `POST /v1/gallery/{id}/clone` ✅ | admin | Clone a gallery deck into the tenant → returns the new slideshow. |

### 4.4 Templates

| Method & path | Auth | Notes |
| --- | --- | --- |
| `POST /v1/templates` ✅ | admin, Premium | Body is `{ "title", "prompt", "sampleJson" }` (AI Studio) **XOR** `{ "title", "html", "css" }` (code; `{{field}}` placeholders). `title` required. `?dryRun=true` validates only. AI mode may return `RateLimited` (429) when the tenant's AI credit caps are exhausted. |
| `GET /v1/templates` ✅ | admin | List templates. `sampleJson` in responses is synthesized from the template's stored data schema (type-appropriate sample values), not a stored literal. |
| `GET /v1/templates/{id}` ✅ | admin | Single template. |

### 4.5 Devices, schedules & streams

| Method & path | Auth | Notes |
| --- | --- | --- |
| `POST /v1/devices` ✅ | admin | Body `{ "name", "resolution" }`. Unverified owner email → `EmailNotVerified`; at limit → `DeviceLimitReached` (limit is per-tenant `deviceQuota.max`, 1 on free). `resolution` snapped to the supported presets (see §4.2); response echoes the effective value. Honors `Idempotency-Key`. |
| `GET /v1/devices` ✅ | admin | List active devices (incl. broadcast channels). |
| `GET /v1/devices/{id}/status` ✅ | admin | `{ "online", "lastSeenAt", "nowPlaying": { "slideshowId", "slideId" } }`. `online` = heartbeat within the server's offline threshold (broadcast devices always report online). `nowPlaying.slideId` is **always null** in v1 — HLS playback is slideshow-granular; the backend cannot know the on-screen slide. |
| `POST /v1/devices/{id}/schedule` ✅ | admin | Assign a slideshow: `{ "slideshowIds": [...] }` must contain **exactly one id** in v1 (the backend has no playlist model; more → `ValidationFailed`). Becomes the device's always-on default. Returns `AspectMismatch` warning when the device has an explicit resolution differing in orientation from the slideshow (still succeeds). |
| `GET /v1/devices/{id}/stream` ✅ | admin | `{ "hlsUrl", "expiresAt" }` — live HLS manifest link. `expiresAt` is **always null** in v1 (stable long-lived token URLs; rotation is the invalidation mechanism). A `StreamNotReady` warning accompanies links whose slideshow isn't assigned or rendered yet. |

### 4.6 API keys & billing

| Method & path | Auth | Notes |
| --- | --- | --- |
| `POST /v1/apikeys` ✅ | admin | Body `{ "type": "admin"\|"dataProvider", "name", "slideIds": [...] }` (`slideIds` required for dataProvider). Plaintext key returned **once**. |
| `GET /v1/apikeys` ✅ | admin | List: id, name, type, prefix, `slideIds`, `createdAt`, `lastUsedAt`, `revokedAt`. Never returns full keys. |
| `DELETE /v1/apikeys/{id}` ✅ | admin | Revoke. |
| `POST /v1/billing/checkout` ✅ | admin | → `{ "checkoutUrl" }`. In v1 this is the web app's billing page (one click from Paddle checkout) — the same URL used in `EntitlementRequired.details.upgradeUrl`. A raw `checkout.paddle.com` transaction URL requires adding a plan/price field to this endpoint's body (v1.1 candidate; the backend's Paddle API client already exists). Subscription state syncs back via Paddle webhooks. |

## 5. Client behavior rules

- Surface `warnings[]` in MCP tool results and CLI output so agents can self-correct.
- Treat unknown fields/warning codes as forward-compatible pass-through.
- Send `Idempotency-Key` (random UUID) on every create automatically.
- Without a credential, fail fast client-side for all non-anonymous calls with guidance to run
  `infoslides login` / set `INFOSLIDES_API_KEY` — do not emit a confusing 401 round-trip.
