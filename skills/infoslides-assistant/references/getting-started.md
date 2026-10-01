# Getting started: workspace, content, screen

For a person starting from nothing, or adding a screen. The order below is the order that works;
each step says why it is there.

## Contents

1. The path in one list
2. Creating a workspace for someone
3. Getting content in
4. Creating the screen
5. Getting it onto the actual TV (the walk-through)
6. The pairing call in detail
7. The stream link

## 1. The path in one list

1. `create_tenant` (`POST /v1/tenants`): only if they have no workspace. See section 2.
2. `get_tenant_info` (`GET /v1/tenant`): read the plan and the screen allowance now, so the rest
   fits inside it. The free plan has 1 screen, 4 slideshows, 2 users and 200 MB, forever, with a
   small watermark on the stream.
3. Set the time zone if the screens are outside Iceland: `update_tenant` (`PATCH /v1/tenant`)
   with `timeZone` (an IANA id such as `Europe/London`) and `locale`. It decides when schedules
   switch, what the clock shows and when time conditions apply.
4. Content in (section 3). Wait for `renderStatus` `Completed`.
5. Look at it: `preview_slide` (`GET /v1/slides/{id}/preview.png`) for the slides that matter.
   Text written for a projector or for print is usually far too small from across a room.
6. The screen (sections 4 and 5).
7. Hand over the link (section 7) and confirm it plays.

Steps 1 to 6 are invisible to the person. The link and a screen that plays are the whole point, so
end there, not with a list of what you did.

## 2. Creating a workspace for someone

`create_tenant` (`POST /v1/tenants`) is the only call that needs no key. It returns the admin key
once; store it where you will find it again.

1. **First give the person the links** to the [Terms of Service](https://infoslides.app/terms) and
   the [Privacy Policy](https://infoslides.app/privacy). They apply to the workspace you make for
   them, and it is theirs to agree to.
2. **Use an address you can open right now as `ownerEmail`**, ideally your own. Never invent one
   or use a placeholder: it decides whether anyone can ever get in.
3. Send `tenantName`, `ownerEmail`, `source` (`api` or `mcp`), `agent` (your product name, e.g.
   `"Muse"`), and `timeZone` and `locale` when you know them.
4. **Verify the address.** Adding a screen fails with `EmailNotVerified` until one of these
   happens: sign in once at <https://infoslides.app/login> with Google or Microsoft using that
   address (quickest), open the verification email, or set a password through "Forgot your
   password?". `get_tenant_info` reports `isEmailVerified`; `resend_verification_email`
   (`POST /v1/auth/resend-verification`) sends the email again.
5. **Invite the person as an admin:** `invite_team_member` (`POST /v1/team/invitations`) with
   their email. They sign in however they like (Google, Microsoft, GitHub or a password).
   `list_team` shows members and pending invitations.

"Email already registered" means the address already has an account. Call `create_tenant` again
with your admin key and the same `ownerEmail` to add a second workspace to that account.

## 3. Getting content in

Every new slideshow renders first. It plays once `renderStatus` on `get_slideshow` is `Completed`.

| The person has or wants | Call |
| --- | --- |
| A PowerPoint or PDF, shown as it is | `upload_pptx` (`POST /v1/slideshows/pptx`) or `upload_pdf` (`POST /v1/slideshows/pdf`); both take either. Multipart with a `file` part, up to 100 MB, optional `title`. One slide per page |
| Photos or videos in turn | `create_slideshow` (`POST /v1/slideshows`) with `{"title": "Lunch menu", "slides": [{"mediaUrl": "https://...", "durationSeconds": 10}]}`. Their own files: `upload_media` (`POST /v1/media`) first, then `add_media_slide` (`POST /v1/slideshows/{id}/slides`) with `mediaAssetId` |
| Nothing yet ("make me something to start with") | `list_gallery` (`GET /v1/gallery`); show the `previewUrl`s; `clone_gallery_slideshow` (`POST /v1/gallery/{id}/clone`) with the one they pick. Or design one with AI Studio (ai-studio.md) |
| A copy to try changes on | `clone_slideshow` (`POST /v1/slideshows/{id}/clone`). No screen plays the copy until you put it on one |
| A live data slide | See live-data.md |
| Their Google Slides deck | Not possible through the API: ask them to download it as `.pptx` or `.pdf`, or to import it in the web app |

A gallery clone is the fastest way to something presentable. Build from scratch only when
nothing in the gallery is close.

## 4. Creating the screen

- **TV already showing its pairing screen:** skip this; `pair_device` creates and pairs the screen
  in one call (section 6).
- **Otherwise:** `create_device` (`POST /v1/devices`) with a name for where it is ("Reception
  TV", not "Device 1"), plus `"resolution": {"width": 1080, "height": 1920}` for a portrait
  screen. Then pair it (section 5).
- Put content on it: `play_slideshow` (`POST /v1/devices/{id}/play`) with `slideshowId`. It
  checks the render, the screen and conflicts first. A warning `AspectMismatch` means the content
  will be stretched or cropped: fix the resolution rather than shipping it.

## 5. Getting it onto the actual TV

The person is standing in front of the TV holding a remote, not reading docs. When the content is
ready, walk them through it one step at a time and wait for each answer.

1. **Turn it on.** Ask them to switch on the TV where it should play.
2. **Find out what it is.** A smart TV (which brand?), or a plain screen with a box or stick
   plugged in (Android TV box, Fire TV, a computer). The brand is usually on the frame or the
   remote.
3. **Open the player:**

   | Screen | What the person does |
   | --- | --- |
   | Android TV / Google TV, or an Android box | Install **InfoSlides** from Google Play and open it |
   | LG (webOS) | Install **InfoSlides** from the LG Content Store and open it |
   | Samsung (Tizen) | The app is awaiting store approval: open the TV's web browser at `https://infoslides.app/pair.html` |
   | Anything else with a browser | Open `https://infoslides.app/pair.html`, full screen |

   HTML playback (live pushed data) plays only in the browser page and the Android app from 1.2.0
   for now; the LG and Samsung versions that play it are in store review. An automatic slideshow
   handles this by itself (it stays a video stream for screens that cannot play HTML). If a
   slideshow was forced to HTML, use the browser page unless the screen runs an up-to-date Android
   app. `supportsHtml` on `list_devices` says what each screen can play.
4. **Pair it.** The screen shows a QR code, a short nickname (like `swift-oak-42`) and a box for
   a 6-character code. Whichever works:
   - They scan the QR code with their phone. If the phone's browser is signed in to InfoSlides, a
     device picker opens; a workspace with one screen pairs straight away.
   - They read you the QR code or the nickname, or you see it through their glasses:
     `pair_device` with `qr` or `nickname` (section 6).
   - Nothing to scan with: `pair_device` with `deviceId` alone returns a `code` to read out; they
     type it on the screen before `expiresAt`.
5. **Save where it is.** Right after pairing, `update_device` (`PATCH /v1/devices/{id}`) with
   `latitude`, `longitude` and optionally `locationName`, so "this screen" finds it next time.
6. **Confirm it plays.** Ask whether the slideshow is on the screen. "No slideshow" on a screen:
   check `supportsHtml` against the HTML note above.

Phrase instructions for a remote: "press the Home button, open the web browser, and type this
address". Long URLs are miserable to type with a remote; the app is kinder where it exists.

## 6. The pairing call in detail

`pair_device` (`POST /v1/pairings`) needs a device manager or an admin.

- **A TV showing its pairing screen:** send `qr` (the scanned URL, `/pair-screen?qr={sessionId}`,
  with or without `utm_*` parameters, or just the session id) or `nickname`. Add exactly one of:
  - `slideshowId`: creates a new screen playing it.
  - `deviceId`: binds the TV to that existing screen instead (whatever TV was paired to it before
    stops playing).
  - Neither: the answer is `NeedsClarification` listing the slideshows and screens as ready-made
    choices; send one back with the same `qr` or `nickname`.
- **An existing screen, no TV in front of you:** `deviceId` alone returns a 6-character `code` and
  `expiresAt`.
- An expired or used pairing screen answers with a plain message: tell the person to restart the
  app and scan again.
- Over the screen allowance, the answer is `NeedsClarification` whose recommended choice is a
  checkout (see the money rule in SKILL.md).

- `?dryRun=true` checks without pairing or creating anything.

## 7. The stream link

`get_stream_link` (`GET /v1/devices/{id}/stream`) returns `playerUrl`: hand this out. It plays in
any browser, in either playback mode, and stays the same as the content changes. `hlsUrl` is a
raw HLS link for equipment that only speaks HLS; it is `null` when the screen plays HTML, so check
it before handing it out.

A `StreamNotReady` warning means the video is still being built. That is normal right after an
upload: tell the person to wait a minute rather than sending them to a black screen.
