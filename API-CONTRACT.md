# InfoSlides Agent API Contract (v1)

This document is the single source of truth for the agent-facing REST API consumed by the
`infoslides` CLI / MCP server. The InfoSlides backend implements this contract; the client in
this repository is generated against it. Changes go through PR review in both repos.

Status: **draft for backend review** — endpoints marked ⏳ do not exist in the backend yet.

## 1. Conventions

- Base URL: `https://api.infoslides.com` (override: `INFOSLIDES_API_URL` / `--api-url`).
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
| `RateLimited` | 429 | Too many requests; `Retry-After` header set. |

### Warning codes

| Code | Meaning |
| --- | --- |
| `AspectMismatch` | Scheduled/uploaded content aspect ratio differs from target device. Request still succeeds. |

## 3. Credentials, user states & entitlements

API keys are rows in the backend `TenantApiKeys` table:

| Key type | Prefix | Scope |
| --- | --- | --- |
| Admin | `isk_admin_` | Full tenant access: devices, schedules, content, keys, settings. |
| Data Provider | `isk_dp_` | Hard-bound to specific `slideIds`; may ONLY call `POST /v1/slides/{id}/source`. |

Gatekeeping uses the pre-existing `IsEmailVerified` and `SubscriptionLevel` fields:

| State | Condition | Allowed |
| --- | --- | --- |
| ANONYMOUS | no credential | `POST /v1/tenants` only |
| VERIFIED FREE | `IsEmailVerified = true` | content + 1 active device + streaming |
| PRO / ENTERPRISE | active Paddle subscription | everything, unlimited devices, AI templates, `update_source` |

The backend is the sole enforcement point; the client exposes every tool regardless of state and
surfaces backend errors/warnings verbatim.

## 4. Endpoints

### 4.1 Tenants & auth

| Method & path | Auth | Notes |
| --- | --- | --- |
| `POST /v1/tenants` ⏳ | none | Body `{ "tenantName", "ownerEmail" }` → `{ "tenantId", "apiKey": "isk_admin_...", "verificationEmailSent": true }`. Honors `Idempotency-Key`. |
| `POST /v1/auth/resend-verification` ⏳ | any | Re-sends the owner verification email. |
| `GET /v1/tenant` ⏳ | any | "whoami": `{ "tenantId", "name", "ownerEmail", "isEmailVerified", "subscriptionLevel", "deviceQuota": { "used", "max" }, "keyScope": { "type", "slideIds" } }`. |
| `GET /v1/auth/cli/start` ⏳ | none | Browser entry (the CLI opens this URL, so it must accept GET). Query: `provider=google\|microsoft\|github`, `state`, `codeChallenge` (PKCE S256), `redirectUri` (loopback; registered set: `http://localhost:5000/callback`, `:5013`, `:53682`). Backend brokers the IdP and 302s to `redirectUri?code=...&state=...`. Codes are single-use, 5-minute TTL. |
| `POST /v1/auth/cli/exchange` ⏳ | none | Body `{ "code", "codeVerifier" }` → `{ "sessionToken", "expiresAt", "tenantId", "email" }`. |

### 4.2 Slideshows & slides

| Method & path | Auth | Notes |
| --- | --- | --- |
| `POST /v1/slideshows` ⏳ | admin | Create/upload. Body `{ "title", "resolution": {"width","height"}, "slides": [...] }`. Default resolution 1920x1080; portrait (e.g. 1080x1920) supported. |
| `GET /v1/slideshows` ⏳ | admin | List (paged: `?page=&pageSize=`). |
| `GET /v1/slideshows/{id}` ⏳ | admin | Full slideshow incl. slides, conditions, resolution. |
| `PATCH /v1/slideshows/{id}` ⏳ | admin | Partial update (title, resolution, slide order). |
| `POST /v1/slideshows/{id}/clone` ⏳ | admin | Clone an existing slideshow. |
| `POST /v1/slideshows/{id}/slides` ⏳ | admin | Add media slide. Body `{ "mediaUrl" }` or multipart upload; optional `durationSeconds`, `position`. May return `AspectMismatch`. |
| `PUT /v1/slides/{id}/conditions` ⏳ | admin | Replace visibility conditions. Body `{ "conditions": [{ "type": "time"\|"weekday"\|"data_trigger", "value": "08:00-11:00" }] }`. Evaluated server-side during HLS rendering. |
| `POST /v1/slides/{id}/source` ⏳ | admin or data-provider (bound) | Push JSON matching the template's `sampleJson` schema; triggers server-side re-render. `?dryRun=true` validates without rendering. **Only endpoint valid for `isk_dp_` keys.** |
| `GET /v1/slides/{id}/preview.png` ⏳ | admin | Rendered PNG of the slide's current state (agent self-verification). |

### 4.3 Gallery

| Method & path | Auth | Notes |
| --- | --- | --- |
| `GET /v1/gallery` ⏳ | any | Starter gallery of pre-built decks: `[{ "id", "title", "description", "previewUrl", "resolution" }]`. |
| `POST /v1/gallery/{id}/clone` ⏳ | admin | Clone a gallery deck into the tenant → returns the new slideshow. |

### 4.4 Templates

| Method & path | Auth | Notes |
| --- | --- | --- |
| `POST /v1/templates` ⏳ | admin, Premium | Body is `{ "title", "prompt", "sampleJson" }` (AI Studio) **XOR** `{ "title", "html", "css" }` (code; `{{field}}` placeholders). `title` required. `?dryRun=true` validates only. |
| `GET /v1/templates` ⏳ | admin | List templates with their `sampleJson` schemas. |
| `GET /v1/templates/{id}` ⏳ | admin | Single template. |

### 4.5 Devices, schedules & streams

| Method & path | Auth | Notes |
| --- | --- | --- |
| `POST /v1/devices` ⏳ | admin | Body `{ "name", "resolution" }`. Free tier: max 1 active (else `DeviceLimitReached`). Honors `Idempotency-Key`. |
| `GET /v1/devices` ⏳ | admin | List devices. |
| `GET /v1/devices/{id}/status` ⏳ | admin | `{ "online", "lastSeenAt", "nowPlaying": { "slideshowId", "slideId" } }`. |
| `POST /v1/devices/{id}/schedule` ⏳ | admin | Assign slideshow(s): `{ "slideshowIds": [...] }`. Returns `AspectMismatch` warning when device/slideshow ratios differ (still succeeds). |
| `GET /v1/devices/{id}/stream` ⏳ | admin | `{ "hlsUrl", "expiresAt" }` — live HLS manifest link. |

### 4.6 API keys & billing

| Method & path | Auth | Notes |
| --- | --- | --- |
| `POST /v1/apikeys` ⏳ | admin | Body `{ "type": "admin"\|"dataProvider", "name", "slideIds": [...] }` (`slideIds` required for dataProvider). Plaintext key returned **once**. |
| `GET /v1/apikeys` ⏳ | admin | List: id, name, type, prefix, `slideIds`, `createdAt`, `lastUsedAt`, `revokedAt`. Never returns full keys. |
| `DELETE /v1/apikeys/{id}` ⏳ | admin | Revoke. |
| `POST /v1/billing/checkout` ⏳ | admin | → `{ "checkoutUrl" }` (Paddle). Subscription state syncs back via Paddle webhooks. |

## 5. Client behavior rules

- Surface `warnings[]` in MCP tool results and CLI output so agents can self-correct.
- Treat unknown fields/warning codes as forward-compatible pass-through.
- Send `Idempotency-Key` (random UUID) on every create automatically.
- Without a credential, fail fast client-side for all non-anonymous calls with guidance to run
  `infoslides login` / set `INFOSLIDES_API_KEY` — do not emit a confusing 401 round-trip.
