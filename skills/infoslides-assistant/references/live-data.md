# Live data: slides that update themselves

## Contents

1. Fixed or live? Decide first
2. Pushed data: the loop
3. Designing a template that reads well on a screen
4. The data schema
5. Pulled sources: weather, news, calendars
6. How often it reaches the screen
7. Plans

## 1. Fixed or live? Decide first

**A fixed slide** (a picture, a PowerPoint or PDF page, an AI-designed slide) is right when the
content changes rarely or on a human schedule: a welcome message, opening hours, a seasonal offer.
It is cheaper, works on every plan and cannot go stale.

**A live slide** is right when the content has a source of truth that changes by itself and would
be embarrassing out of date: a queue number, today's soup, sales figures, the next departure,
scores, the weather.

Two kinds of live slide:

- **Pushed:** the person's own system knows the data (a queue system, a till, meters, a
  scoreboard, a spreadsheet script) and sends it to InfoSlides. Every plan includes one.
- **Pulled:** InfoSlides fetches it from a public service (weather, RSS news, a calendar, road
  conditions). Paid plans only.

A live slide that stops being fed goes stale. If nothing will reliably send updates, build a
fixed slide instead, and say why.

## 2. Pushed data: the loop

1. **Design and preview the template.** `preview_new_template` (`POST /v1/templates/preview`)
   with `layoutHtml`, `layoutCss` and `sampleData` returns a PNG and saves nothing. Iterate here.
2. **Save it:** `create_template` (`POST /v1/templates`) with `title`, `html`, `css` and
   `"dataMode": "push"`. Every `{{field}}` in the HTML becomes a data field.
3. **Add it to a slideshow:** `add_dynamic_slide` (`POST /v1/slideshows/{id}/slides/dynamic`) with
   the `templateId` and `"createPushKey": true`. The answer carries `sourceId` (where data goes)
   and `pushKey` (`isk_dp_...`), shown once. Give the push key to the system that will send the
   data; it can do nothing else. Keep the admin key private. More keys later:
   `create_source_key` (`POST /v1/sources/{id}/keys`).
4. **Push data:** `push_data` (`POST /v1/sources/{sourceId}/data`) with a JSON object of the
   template's fields, the whole object each time. `?dryRun=true` checks without storing. The
   answer has `receivedAt`. `update_source` (`POST /v1/slides/{slideId}/source`) lands on the same
   source.
5. **Check it:** `get_source_status` (`GET /v1/sources/{sourceId}`) shows `lastReceivedAt`,
   `isShowingData` and `hideAfterMinutes` (for a fetched source such as RSS, `lastFetchedAt`); `preview_slide` shows the slide with the real values.
   Real values are often longer than the example: look before the person does.
6. **Play it:** `play_slideshow` or `play_slideshow_find_device`, then the `playerUrl`. A live data
   board rarely wants the clock: `update_slideshow` with `{"clock": {"enabled": false}}`.

How it behaves:

- The source keeps only the latest payload; every push replaces it.
- The slide stays hidden until the first push, so nobody sees an empty layout while the person
  wires up their system. Push one payload before you play it: until then a screen has nothing to
  show, and `get_device_diagnosis` answers `WaitingForData`.
- With `hideAfterMinutes` set on the source (in the dashboard, under Sources), the slide hides
  after that long without data and returns with the next push. Suggest it for anything that stops
  overnight, like a queue.
- Payloads are checked against the template: required fields present, types right. Extra fields
  are ignored. A failure is `ValidationFailed` per field.

The worked example, a clinic queue board with its HTML, CSS and a curl push:
<https://infoslides.app/blog/agents-guide-to-the-infoslides-galaxy>.

## 3. Designing a template that reads well on a screen

- Design one 1920 x 1080 screen in viewport units (`vh`, `vw`) so it scales to any display. Keep
  the smallest text around 2vh; it is read from across a room.
- Fields are text. Send values already formatted the way they should read (`"1,240"`, `"A-142"`,
  `"1.990 kr"`).
- Hide what has no value with a section: `{{#field}} ... {{/field}}` shows only when the field is
  not empty, `"0"` or `"false"`. Sections nest, so a card can hide as a whole
  (`{{#name1}}`) and still hide an optional badge inside it (`{{#left1}}`); do not nest a section
  inside one with the same field.
- Let data drive visuals through CSS: `style="--p:0{{percent}}"` with `width: calc(var(--p) * 1%)`
  draws a bar; the leading `0` keeps it empty rather than full when the value is missing.
- Send states, not colours: `class="room is-{{status}}"`, and let the CSS style `busy`, `ready`.
- Lists are numbered fields (`next1` to `next4`), each in its own section; there are no loops.
- `{{TenantLogo}}` and `{{AccentColor}}` are filled in from the workspace. They are not data fields.
- No scripts and no `@import`: plain CSS only (CSS animation works in HTML playback). Never send
  HTML inside a value.
- If the screen shows a news ticker, leave the bottom tenth of the slide free.

More on legibility: <https://infoslides.app/blog/the-squint-test-designing-for-distance>.
Portrait and odd ratios: <https://infoslides.app/blog/beyond-16-9-mastering-vertical-and-custom-ratios>.

## 4. The data schema

Through the API you do not send a schema. In code mode (`html` + `css`) it is built from the
`{{placeholders}}` in the HTML; in AI mode (`prompt`, paid plans) from the top-level fields of
`sampleJson`. Existing templates: `list_templates` (`GET /v1/templates`) shows each one's
`sampleJson`, `get_template` (`GET /v1/templates/{id}`) one in full.

When you help a person working in the web template editor instead, give them the schema to paste.
The editor's **Paste schema** takes four formats and recognises which one it is; a sample of the
data is easiest, because it also fills the preview:

```json
{ "ticket": "A-142", "room": "Room 2", "waiting": 7, "open": true, "next": ["A-143", "A-144"] }
```

The other three (a list of fields, a JSON Schema, the InfoSlides format), the four types and the
rules: <https://infoslides.app/blog/infoslides-dynamic-template-data-schema>. Field names must
match the placeholders exactly, including case.

## 5. Pulled sources: weather, news, calendars

Paid plans only (the free plan gets only the push adapter).

| The person asks | Call |
| --- | --- |
| What can be pulled | `list_adapters` (`GET /v1/adapters`), with each adapter's `configFields` form |
| The workspace's sources | `list_sources` (`GET /v1/sources`) |
| A new feed | `create_source` (`POST /v1/sources`) with `{"adapterType": "RssFeed", "name": "News", "config": {"feedUrl": "..."}, "fetchIntervalSeconds": 300}` |
| Change it | `update_source_settings` (`PATCH /v1/sources/{id}`): `name`, `config`, `fetchIntervalSeconds` (at least 300), `isEnabled`. Shared sources are read-only |
| Remove it | `delete_source` (`DELETE /v1/sources/{id}`); fails while a slide or ticker uses it |
| Headlines in the ticker | `update_slideshow` with `{"ticker": {"enabled": true, "sourceIds": ["..."]}}` |
| A slide from the source | `list_templates`, `add_dynamic_slide` with the `templateId`, then `update_slide` on the new slide with `sourceId` (add takes no `sourceId`, and an unknown field is rejected) |

A pulled source refreshes on its interval (weather every 15 minutes by default) and the slide
re-renders when the data changes.

## 6. How often it reaches the screen

Every push is stored at once. What differs is the screen:

- **Video stream:** a changed payload is rendered at most about once a minute per slideshow, and
  that render has the newest data. Pushing more often than once a minute gains nothing.
- **HTML playback:** the live slide picks up each push without a render; only the slide appearing
  or hiding needs one. New workspaces choose the mode automatically (HTML unless a screen needs a
  video stream); leave `playbackMode` alone unless the person asks.

## 7. Plans

- **Every plan, the free plan too:** one live data slide, meaning one push source feeding one
  slide, with one push template designed in code (`html` + `css`).
- **Professional and above:** no limit on push sources, slides or templates.
- **Pulled sources, AI-designed templates (`prompt`), `overrideData`:** paid plans.
- Going past the allowance returns `EntitlementRequired` with an upgrade link. Say so up front
  rather than hitting it in front of the person.

