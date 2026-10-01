# Everyday edits: find it, change it, confirm it

Most requests after setup are edits to something already playing: "turn off the news ticker in
the lobby", "hide the Christmas slide", "make every slide 15 seconds". The person names a screen
or a slide, never an id.

## Contents

1. Find it
2. Change it: slides and slideshow settings
3. Change it: when things play (schedules, takeovers, showing a photo)
4. Screens, reports and alerts
5. Confirm it
6. Undo

## 1. Find it

- **Screens:** `list_devices` (`GET /v1/devices`) lists every screen with `nowPlayingSlideshowId`
  and `nowPlayingTitle`. Match the screen the person named here first. `q` filters by name,
  ignoring case and accents ("lobby" finds "Lobby Screen").
- **"This screen", "the screen in front of me":** `list_devices` with `q` finds screens by name.
  When several screens exist and the name is uncertain, use `identify_devices`
  (`POST /v1/devices/identify`) to show each screen's name on it, then ask which name they see.
- **Slideshows:** `list_slideshows` (`GET /v1/slideshows`) with `renderStatus` and `screenCount`;
  `q` filters by title ("q1" finds "Q1 Results 2026").
- **Everything about one slideshow:** `get_slideshow` (`GET /v1/slideshows/{id}`):
  - `slides` in play order, each with `id`, `type` (`pptx`, `media`, `dynamic`),
    `durationSeconds`, `hidden`, `rules`, `templateName` and `thumbnailUrl`.
  - `rules`: every visibility rule on the slide, with a plain-English `summary`. A rule set in the
    web app covering several slides has `readOnly: true`: tell the person about it rather than
    working around it.
  - `thumbnailUrl` is how you see what is on a slide ("the lunch menu slide"). PowerPoint and
    dynamic slides point to `preview_slide` (`GET /v1/slides/{id}/preview.png`), which needs your
    key.
  - `ticker`, `clock`, `defaultDurationSeconds`, `shared`: the settings.
  - `screens`: the screens playing it now, each with its `playerUrl`.
  - `renderStatus`, and `renderProgress` (`percent`, `phase`, `etaSeconds`) while rendering.

A screen with nothing playing has no slideshow to edit. Several matches or none: ask.

## 2. Change it: slides and slideshow settings

| The person asks | Call |
| --- | --- |
| Hide or show a slide | `update_slide` (`PATCH /v1/slides/{id}`) with `{"hidden": true}` or `false` |
| Show a slide longer or shorter | `update_slide` with `{"durationSeconds": 15}` (1 to 300). Only to override: AI-designed slides are timed from their text, a video plays its own length, other slides use the slideshow default |
| Every slide the same length | `update_slideshow` (`PATCH /v1/slideshows/{id}`) with `{"defaultDurationSeconds": 12}` |
| Move a slide | `update_slideshow` with `slideOrder`: every slide id, in the new order |
| Remove a slide for good | `delete_slide` (`DELETE /v1/slides/{id}`); its conditions go with it. Hide instead if it may come back |
| Turn the news ticker off or on | `update_slideshow` with `{"ticker": {"enabled": false}}` (turning it off clears its sources) or `{"ticker": {"enabled": true, "sourceIds": [...]}}` |
| Show, move or recolour the clock | `update_slideshow` with `{"clock": {"enabled": true, "position": "BottomRight", "showDate": false, "backgroundColor": "#000000", "textColor": "#ffffff"}}`, only the fields that change |
| Turn off the clock | `{"clock": {"enabled": false}}`. A new slideshow follows the workspace default, which may be on; a live data board rarely wants one |
| Only at certain times | `set_slide_conditions` (`PUT /v1/slides/{id}/conditions`) with `{"conditions": [{"type": "time", "value": "06:00-11:00"}]}`. Types: `time` (`HH:mm-HH:mm`), `weekday` (`Monday,Tuesday`), `date` (`2026-12-01..2026-12-24`, inclusive, either end open) and `data_trigger` |
| "Take it down on 6 January", "until Friday" | `set_slide_conditions` with `{"conditions": [{"type": "date", "value": "..2027-01-06"}]}` (shown through the 6th, hidden from the 7th) |
| "Only in December" | `{"conditions": [{"type": "date", "value": "2026-12-01..2026-12-31"}]}` |
| Hide at certain times instead | the same call with `"mode": "hide"` |
| When any one of several things holds | add `"match": "any"` (default `all`) |
| A new version of the presentation | Done in the InfoSlides web app: say so. Media and live slides, settings and screens are kept |
| Change the price or text on a live slide | A slide on a push source: `update_source` (`POST /v1/slides/{id}/source`) with the full payload. Another live slide: `update_slide` with `{"overrideData": {"price": "1.990 kr"}}`; only the fields sent change, `null` removes one (not on every plan) |
| Text inside an uploaded PowerPoint | Not editable. Say so; say the updated file is uploaded in the InfoSlides web app |
| A live slide's design or data source | `update_slide` with `templateId` and/or `sourceId` |
| A countdown on a live slide | `update_slide` with `{"countdown": {"overlayId": "...", "mode": "daily", "dailyTime": "17:00"}}` (`fixed` takes `targetTime`, `source` takes `sourceField`; `[]` removes it) |
| Delete a whole slideshow | `delete_slideshow` (`DELETE /v1/slideshows/{id}`). Screens playing it go blank and its schedules are removed: check `screens` and confirm first. Not undoable |

Conditions stay on their slide when slides move, and they are applied by the platform at the
right time. Never promise to come back and change something later: put the date on the slide.

## 3. Change it: when things play

| The person asks | Call |
| --- | --- |
| "Play the Q1 slides on the lobby screen" | `play_slideshow_find_device` (`POST /v1/play`) with `slideshowId` and `deviceId`. `deviceId` may be left out when the workspace has one screen, or the slideshow already plays on exactly one; otherwise the answer lists the screens. Conflicts (still rendering, failed, empty, screen offline, already scheduled) come back as `NeedsClarification` |
| The same, but only for a while | add `"until": "..."`: a temporary takeover; the normal schedule returns afterwards |
| See a screen's schedule | `get_schedule` (`GET /v1/devices/{id}/schedule`): timed windows, the default, and the workspace `timeZone` |
| A slideshow in a daily window ("lunch specials 11 to 2") | `add_schedule_entry` (`POST /v1/devices/{id}/schedule/entries`) with `{"slideshowId": "...", "startTime": "11:00", "endTime": "14:00", "priority": 1}`. Overlaps are rejected. Several screens: one call per screen |
| Remove a window | `delete_schedule_entry` (`DELETE /v1/devices/{id}/schedule/entries/{entryId}`) |
| A screen's default slideshow | `assign_schedule` (`POST /v1/devices/{id}/schedule`) with `{"slideshowIds": ["..."]}` (exactly one id), or `play_slideshow_find_device`, which checks for conflicts first |
| Take screens over ("evacuation map everywhere now", "boardroom for two hours") | `create_takeover` (`POST /v1/takeovers`) with `deviceIds`, `slideshowId`, and `endsAt` or `durationMinutes` (optional `startsAt`). Times without a zone are workspace time; crossing midnight is fine. A slideshow that has never rendered takes over once its first render completes |
| End a takeover early ("put things back") | `end_takeover` (`DELETE /v1/takeovers/{id}`) |

"For a while" and "from now until" are takeovers. "Every day at" is a schedule entry. "From now
on" is the default slideshow.

## 4. Screens, reports and alerts

| The person asks | Call |
| --- | --- |
| Rename a screen, change its resolution | `update_device` (`PATCH /v1/devices/{id}`) with `name` or `resolution` (`{"width": 1080, "height": 1920}`, snapped to the nearest preset) |

| "What's on the lobby screen?" | `get_now_slide_png` (`GET /v1/devices/{id}/now.png`): the slide the screen should be showing now. An offline TV may not actually be showing it |
| "Is it on?" | `get_device_status` (`GET /v1/devices/{id}/status`): `online`, `lastSeenAt`, `nowPlaying` |
| "Why is it black?" | `get_device_diagnosis` (`GET /v1/devices/{id}/diagnosis`) first, always. Every cause that applies, most likely first, each with a sentence to read out and often a ready-made fix. No causes: check `get_now_slide_png` |
| "How are my screens?" | `get_workspace_health` (`GET /v1/workspace/health`). Read `summary` as it is. `problems` each has a `message` and a `fix`. Empty means all is well: say so in one sentence |

Play time is estimated from screen heartbeats per UTC day and has no per-slide counts. Say so
when you report it.

## 5. Confirm it

- Changes to slides, order, durations, hiding, resolution or playback mode render first. Wait for
  `renderStatus` `Completed` on `get_slideshow` (at most every 5 seconds). `Failed` means it did
  not reach the screen: say so.
- Ticker, clock, title and sharing changes skip the render and reach screens within about a
  minute.
- Then give the person the screen's `playerUrl` from `screens`, so they can look for themselves.

## 6. Undo

Every changing call returns `undo` next to `data`: a complete request that puts the previous state
back. It is `null` when nothing changed (a dry run, a value that was already set).

- **Offer undo after any change the person did not ask for word for word.** "Hide the Christmas
  slide" is word for word; picking a slide, a screen or a time window from "tidy up the lobby" is
  not. Say what you did in one sentence and offer to put it back.
- When they say yes, send it as returned: `undo_change` (`POST /v1/undo`) with
  `{"token": "..."}`. It works for 24 hours; keep it until then, nothing else stores it.
- Covered: slide edits, hide and show, delete (the slide comes back), conditions, slideshow
  settings and order, schedule entries, play, takeovers and pairing.
- Not covered: pushed live data (the next push replaces it anyway), deleting a slideshow,
  a screen's name or resolution, and which TV was paired before a re-pairing.
- If the same thing was changed again since, undo answers `NeedsClarification` asking whether to
  overwrite the later change. Read it out; send the overwrite choice only if the person says so.
