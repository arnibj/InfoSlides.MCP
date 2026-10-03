# Making a slide: design it yourself

"Make a slide saying the lunch moved to 1pm", "put up a welcome for the Acme visitors", "a slide
for Friday's quiz night". In most cases you design the slide yourself, show it to the person,
change it until they are happy, and only then put it on the screen they meant. You know what they
asked for, you can iterate in the conversation, and nothing reaches the screen before they have
seen it.

Use AI Studio ([ai-studio.md](ai-studio.md)) instead when the person asks for it, when they want
many slides made from a document, or when you cannot produce an image at all.

## Contents

1. Find where it will play, and its size
2. Design it
3. Render it
4. Iterate with the person
5. Put it on the screen
6. Confirm

## 1. Find where it will play, and its size

A slide is only right for one shape of screen, so start from the screen, not the design.

1. **Which screen?** The person usually says ("on the lobby screen"). `list_devices` with `q`
   finds it, with `nowPlayingSlideshowId`. If they did not say and there are several screens, ask.
2. **Which slideshow?** Normally the one that screen plays now. If the screen plays several over
   the day (`get_schedule`), ask which part of the day the slide belongs to.
3. **Which size?** `get_slideshow` (`GET /v1/slideshows/{id}`) returns `resolution`
   (`width` and `height`). Design at exactly that size: `1920x1080` is landscape, `1080x1920` is a
   screen on its end. A slide in the wrong shape is stretched or cropped on the wall.
4. **Does the screen show a ticker or a clock?** `get_slideshow` has `ticker` and `clock`. Keep the
   bottom tenth free under a ticker, and the clock's corner clear.

## 2. Design it

A screen is read from across a room, for a few seconds, by someone doing something else.

- **One message per slide**, under about 20 words. A headline people can read in two seconds,
  and at most a line or two under it.
- **Big text.** The smallest text around 2 to 3 percent of the screen height (about 30 pixels on a
  1080-pixel-high screen); headlines four or five times that.
- **Strong contrast**: light text on a dark background or the reverse, never text over a busy
  photo without a dark overlay behind it.
- **Margins** of about 5 percent on every side; TVs can crop the edges.
- **Every fact from the person, none invented.** Prices, times, dates, names and phone numbers
  come from what they told you. If one is missing, ask; do not fill it in.
- **Their look, if you know it.** Colours and logo from their existing slides (look at the
  `thumbnailUrl`s) keep the new slide from looking out of place.

More: <https://infoslides.app/blog/the-squint-test-designing-for-distance>, and for portrait
screens <https://infoslides.app/blog/beyond-16-9-mastering-vertical-and-custom-ratios>.

## 3. Render it

Produce a PNG at the slideshow's exact size, whichever way suits you:

- **Your own image tools**, if you have them, at `width x height`.
- **HTML and CSS rendered by InfoSlides**: `preview_new_template` (`POST /v1/templates/preview`)
  with `layoutHtml`, `layoutCss`, `"sampleData": {}` and `aspectRatio` (`"16:9"`, `"9:16"` or
  `"1:1"`, matching the slideshow) returns the PNG and saves nothing. It works on every plan for a
  content manager, at up to 10 renders a minute. Write the text straight into the HTML, size it in
  `vh` and `vw`, plain CSS only (no scripts, no `@import`).

## 4. Iterate with the person

Show the PNG and ask one plain question: "Here's the slide. Anything to change?" Change what they
ask for and show it again. Keep going until they say it's good. Changing a picture costs nothing;
changing the wall in front of customers does.

## 5. Put it on the screen

1. `upload_media` (`POST /v1/media`) with the PNG. Keep the returned id.
2. `add_media_slide` (`POST /v1/slideshows/{id}/slides`) with `mediaAssetId`, and:
   - `position`: where it goes, `0` for first. Ask if it matters ("first, so people see it
     straight away?"); an announcement usually goes first.
   - `durationSeconds`: about 5 seconds plus 1 for every 3 words, between 6 and 30.
3. **If it is temporary** ("until Friday", "for today"), put the date on it straight away with
   `set_slide_conditions` (`PUT /v1/slides/{id}/conditions`), for example
   `{"conditions": [{"type": "date", "value": "..2026-10-03"}]}`. Do not promise to take it down
   later yourself.
4. **Only on this one screen, right now, for a while?** A takeover fits better than a slide in the
   loop: `show_media_on_device` (`POST /v1/devices/{id}/show`) with the PNG and `until`.

## 6. Confirm

Wait for `renderStatus` `Completed` on `get_slideshow`, then check it where the person expects it:
`get_now_slide_png` (`GET /v1/devices/{id}/now.png`) when the slide should be up now, and the
screen's `playerUrl` for them to look at.

Say it the way they asked it: "The lunch notice is the first slide on the lobby screen now, until
Friday."
