# Live data: slides that update themselves

## Contents

1. Fixed or live? Decide first
2. Pushed data: the loop
3. Templates, data fields and pulled sources: set up in the web app
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
  conditions). Not on every plan.

A live slide that stops being fed goes stale. If nothing will reliably send updates, build a
fixed slide instead, and say why.

## 2. Pushed data: the loop

1. **Pick the template.** New live data templates are designed in the InfoSlides web app. `list_gallery` shows ready-made
   ones, or use the one the person built there; you need its `templateId`.
3. **Add it to a slideshow:** `add_dynamic_slide` (`POST /v1/slideshows/{id}/slides/dynamic`) with the `templateId`.
   The answer carries `sourceId` (where data goes). The system that will send the data needs a push key; push keys
   are created in the InfoSlides web app, never from here.
4. **Push data:** `push_data` (`POST /v1/sources/{sourceId}/data`) with a JSON object of the
   template's fields, the whole object each time. `?dryRun=true` checks without storing. The
   answer has `receivedAt`. `update_source` (`POST /v1/slides/{slideId}/source`) lands on the same
   source.
5. **Check it:** `get_source_status` (`GET /v1/sources/{sourceId}`) shows `lastReceivedAt`,
   `isShowingData` and `hideAfterMinutes` (for a fetched source such as RSS, `lastFetchedAt`); `preview_slide` shows the slide with the real values.
   Real values are often longer than the example: look before the person does.
6. **Play it:** `play_slideshow_find_device`, then the screen's `playerUrl`. A live data
   board rarely wants the clock: `update_slideshow` with `{"clock": {"enabled": false}}`.

How it behaves:

- The source keeps only the latest payload; every push replaces it.
- The slide stays hidden until the first push, so nobody sees an empty layout while the person
  wires up their system. Push one payload before you play it: until then a screen has nothing to
  show, and `get_device_diagnosis` answers `WaitingForData`.
- With `hideAfterMinutes` set on the source (in the dashboard, under Sources), the slide hides
  after that long without data and returns with the next push. Suggest it for anything that stops
  overnight, like a queue.
- Payloads are checked against the template: types right, and any field the template marks required
  present. A failure is `ValidationFailed` per field. Extra fields are ignored. The fields of a
  code template come from its placeholders and are all optional, so a push without some of them is
  accepted (a dry run too) and the answer carries a `FieldsNotSent` warning naming them (not fields the layout shows only inside a `{{#field}}` section, which are meant to be left out): they show
  blank on the slide. Read the warning out and push again with the missing fields if that is not
  what the person wanted.

The worked example, a clinic queue board with its HTML, CSS and a curl push:
<https://infoslides.app/blog/agents-guide-to-the-infoslides-galaxy>.

## 3. Templates, data fields and pulled sources: set up in the web app

New live data templates, their data fields and pulled sources (weather, news, a calendar) are designed in the
InfoSlides web app. You work with what exists: `list_sources` and `get_source_status` show the sources,
`add_dynamic_slide` puts a template on a slideshow, and `push_data` or `update_source` sends data.


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
- **Pulled sources, AI-designed templates (`prompt`), `overrideData`:** not on every plan.
- Going past the allowance returns `EntitlementRequired`. Explain that a workspace admin can
  change the plan in InfoSlides account settings.

