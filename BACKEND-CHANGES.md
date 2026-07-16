# Required InfoSlides Backend Changes

Checklist of work in the InfoSlides repository to implement [API-CONTRACT.md](API-CONTRACT.md).
Ordered roughly by dependency.

## 1. TenantApiKeys table & key auth

- [ ] New `TenantApiKeys` table: `Id`, `TenantId`, `Name`, `KeyHash` (store hash, never plaintext),
      `KeyPrefix` (first 12 chars for display), `Type` (`Admin` | `DataProvider`),
      `BoundSlideIds` (JSON array; DataProvider only), `CreatedAt`, `LastUsedAt`, `RevokedAt`.
- [ ] Bearer auth handler that resolves `isk_admin_` / `isk_dp_` keys → tenant principal,
      updates `LastUsedAt` (throttled, e.g. once/minute), rejects revoked keys.
- [ ] Scope enforcement middleware: `isk_dp_` keys may only reach
      `POST /v1/slides/{id}/source` and only for their `BoundSlideIds` → `KeyScopeViolation`.

## 2. Gatekeeping middleware

- [ ] State resolution from existing `IsEmailVerified` + `SubscriptionLevel` fields.
- [ ] Enforce the entitlement matrix (contract §3): `EmailNotVerified`, `EntitlementRequired`
      (+ `upgradeUrl` in details), `DeviceLimitReached` (1 active device on free tier).
- [ ] Uniform response envelope (`data`/`warnings`/`error`) + error codes from contract §2.
- [ ] `Idempotency-Key` support on POST creates (24h replay cache).

## 3. Tenant provisioning & auth endpoints

- [ ] `POST /v1/tenants` (anonymous): create tenant + owner profile, send verification email,
      generate + return Primary Admin API Key.
- [ ] `POST /v1/auth/resend-verification`.
- [ ] `GET /v1/tenant` whoami incl. entitlements, device quota, key scope.
- [ ] CLI OAuth loopback broker: `POST /v1/auth/cli/start` + `POST /v1/auth/cli/exchange`
      (PKCE S256, single-use 5-min codes, registered loopback redirect URIs: ports 5000/5013/53682).
      IdP secrets (Google/Microsoft/GitHub) stay server-side — never shipped in the CLI.

## 4. Content endpoints

- [ ] Slideshow CRUD (`/v1/slideshows...`) incl. `resolution` metadata (default 1920x1080,
      portrait support) and clone.
- [ ] Media slide add (`POST /v1/slideshows/{id}/slides`) with `durationSeconds`/`position`,
      aspect-ratio check → `AspectMismatch` warning.
- [ ] `PUT /v1/slides/{id}/conditions` — wire to the **existing** server-side condition
      evaluation engine (time / weekday / data_trigger during HLS rendering).
- [ ] `POST /v1/slides/{id}/source` (+ `?dryRun=true` schema validation) triggering re-render.
- [ ] `GET /v1/slides/{id}/preview.png` — single-frame PNG render (reuse the HLS renderer).
- [ ] Starter gallery: seed decks + `GET /v1/gallery`, `POST /v1/gallery/{id}/clone`.

## 5. Templates

- [ ] `POST /v1/templates` dual-mode: AI Studio (`prompt` + `sampleJson`) XOR code (`html` + `css`
      with `{{field}}` placeholders); Premium-gated; `?dryRun=true`.
- [ ] Template list/get incl. `sampleJson` schema exposure.

## 6. Devices & streaming

- [ ] Device CRUD with `resolution` metadata; free-tier single-device enforcement.
- [ ] Device heartbeat → `GET /v1/devices/{id}/status` (online, lastSeenAt, nowPlaying).
- [ ] `POST /v1/devices/{id}/schedule` with aspect-ratio validation → `AspectMismatch` warning.
- [ ] `GET /v1/devices/{id}/stream` → HLS manifest URL (signed/expiring recommended).

## 7. Billing

- [ ] `POST /v1/billing/checkout` → Paddle checkout link for the tenant.
- [ ] Paddle webhook sync → `SubscriptionLevel` (already partially exists per blueprint).

## Open questions for backend review

1. Timezone semantics for `time`/`weekday` conditions — device-local or tenant-default? The
   contract currently leaves it to the existing engine; document the answer in API-CONTRACT.md.
2. Slide ordering: is `position` on slide-add + `PATCH /v1/slideshows/{id}` slide order enough,
   or does a dedicated reorder endpoint exist already?
3. Media storage: if media assets are first-class (library), add `GET/DELETE /v1/media` in v1.1.
4. Existing REST controllers that already cover any of the above — reuse and alias under `/v1`
   rather than duplicating.
