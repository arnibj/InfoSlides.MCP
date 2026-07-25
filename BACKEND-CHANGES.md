# Required InfoSlides Backend Changes

Checklist of work in the InfoSlides repository to implement [API-CONTRACT.md](API-CONTRACT.md).
Ordered roughly by dependency.

> **Status: all items implemented** (2026-07-18, branch `claude/mcp-endpoints-audit-83z4sm`
> in `arnibj/infoslides`; ~330 new tests, suite green). Stage-by-stage record:
> `docs/mcp-agent-api/PROGRESS.md`; contract reconciliation: `docs/mcp-agent-api/GAP-ANALYSIS.md`
> (both in the InfoSlides repo). `GET /v1/auth/cli/start` supports google|microsoft|github
> (2026-07-24: GitHub OAuth app registered, `AspNet.Security.OAuth.GitHub` wired in).

## 1. TenantApiKeys table & key auth

- [x] New `TenantApiKeys` table: `Id`, `TenantId`, `Name`, `KeyHash` (store hash, never plaintext),
      `KeyPrefix` (first 12 chars for display), `Type` (`Admin` | `DataProvider`),
      `BoundSlideIds` (JSON array; DataProvider only), `CreatedAt`, `LastUsedAt`, `RevokedAt`.
- [x] Bearer auth handler that resolves `isk_admin_` / `isk_dp_` keys → tenant principal,
      updates `LastUsedAt` (throttled, e.g. once/minute), rejects revoked keys.
- [x] Scope enforcement middleware: `isk_dp_` keys may only reach
      `POST /v1/slides/{id}/source` and only for their `BoundSlideIds` → `KeyScopeViolation`.

## 2. Gatekeeping middleware

- [x] State resolution from existing `IsEmailVerified` + `SubscriptionLevel` fields.
- [x] Enforce the entitlement matrix (contract §3): `EmailNotVerified`, `EntitlementRequired`
      (+ `upgradeUrl` in details), `DeviceLimitReached` (1 active device on free tier).
- [x] Uniform response envelope (`data`/`warnings`/`error`) + error codes from contract §2.
- [x] `Idempotency-Key` support on POST creates (24h replay cache).

## 3. Tenant provisioning & auth endpoints

- [x] `POST /v1/tenants` (anonymous): create tenant + owner profile, send verification email,
      generate + return Primary Admin API Key.
- [x] `POST /v1/auth/resend-verification`.
- [x] `GET /v1/tenant` whoami incl. entitlements, device quota, key scope.
- [x] CLI OAuth loopback broker: `POST /v1/auth/cli/start` + `POST /v1/auth/cli/exchange`
      (PKCE S256, single-use 5-min codes, registered loopback redirect URIs: ports 5000/5013/53682).
      IdP secrets (Google/Microsoft/GitHub) stay server-side — never shipped in the CLI.

## 4. Content endpoints

- [x] Slideshow CRUD (`/v1/slideshows...`) incl. `resolution` metadata (default 1920x1080,
      portrait support) and clone.
- [x] `POST /v1/slideshows/pptx` (2026-07-24) — multipart `.pptx` upload → slideshow, same
      parse/thumbnail/stream pipeline as the web app's upload.
- [x] `POST /v1/media` (2026-07-24) — multipart file upload directly into the tenant's media
      library, closing the "slide-add by existing media id" item from open question 3 below.
- [x] Media slide add (`POST /v1/slideshows/{id}/slides`) with `durationSeconds`/`position`,
      aspect-ratio check → `AspectMismatch` warning; accepts `mediaUrl` (download) or
      `mediaAssetId` (existing asset, e.g. from `POST /v1/media`) — exactly one of the two.
- [x] `POST /v1/slideshows/{id}/slides/dynamic` (2026-07-24) — dynamic (template-driven) slide
      add, closing the gap where a template could be created (`POST /v1/templates`) and pushed to
      (`POST /v1/slides/{id}/source`) but never actually instantiated as a slide via the agent API.
      No content-source endpoint was needed: the new slide starts with empty override data and
      `POST /v1/slides/{id}/source` already writes straight into it.
- [x] `PUT /v1/slides/{id}/conditions` — wire to the **existing** server-side condition
      evaluation engine (time / weekday / data_trigger during HLS rendering).
- [x] `POST /v1/slides/{id}/source` (+ `?dryRun=true` schema validation) triggering re-render.
- [x] `GET /v1/slides/{id}/preview.png` — single-frame PNG render (reuse the HLS renderer).
- [x] Starter gallery: seed decks + `GET /v1/gallery`, `POST /v1/gallery/{id}/clone`.

## 5. Templates

- [x] `POST /v1/templates` dual-mode: AI Studio (`prompt` + `sampleJson`) XOR code (`html` + `css`
      with `{{field}}` placeholders); Premium-gated; `?dryRun=true`.
- [x] Template list/get incl. `sampleJson` schema exposure.

## 6. Devices & streaming

- [x] Device CRUD with `resolution` metadata; free-tier single-device enforcement.
- [x] Device heartbeat → `GET /v1/devices/{id}/status` (online, lastSeenAt, nowPlaying).
- [x] `POST /v1/devices/{id}/schedule` with aspect-ratio validation → `AspectMismatch` warning.
- [x] `GET /v1/devices/{id}/stream` → HLS manifest URL (signed/expiring recommended).

## 7. Billing

- [x] `POST /v1/billing/checkout` → Paddle checkout link for the tenant.
- [x] Paddle webhook sync → `SubscriptionLevel` (already partially exists per blueprint).

## Open questions for backend review — ANSWERED (by the backend implementation)

1. Timezone semantics for `time`/`weekday` conditions: **tenant-default** (the tenant's
   workspace timezone; the engine supports per-condition IANA overrides but v1 doesn't expose
   them). Documented in API-CONTRACT.md §4.2.
2. Slide ordering: `position` on slide-add + `PATCH /v1/slideshows/{id}` `slideOrder` are
   implemented over the backend's unified slide sequence (integer `SlideIndex`); no dedicated
   reorder endpoint existed or was needed.
3. Media assets ARE first-class in the backend (own table, folders, quotas) — `GET/DELETE
   /v1/media` remains a v1.1 candidate; slide-add by existing media id is another.
4. Existing controllers were reused via services (device/schedule/media/template/slide-rule
   services, provisioning, Paddle webhook sync); `/v1` controllers are separate and thin —
   no existing `api/*` routes were aliased or changed behaviorally.
