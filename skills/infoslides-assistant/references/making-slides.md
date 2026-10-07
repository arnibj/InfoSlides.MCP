# Making a slide

"Make a slide saying the lunch moved to 1pm", "put up a welcome for the Acme visitors", "a slide
for Friday's quiz night". There are two ways to do it. Start with the first, and move to the
second only when the person wants something the first cannot do.

## First choice: `add_designed_slide`

`add_designed_slide` (`POST /v1/slideshows/{id}/slides/design`) makes a finished slide from a
`heading`, optional `text` and `cta`, and a background:
`backgroundColor` (hex; the workspace accent colour when omitted), `mediaAssetId` (an image from `upload_media`) or `mediaUrl`. A picture beside the text (a dish, a product, a person) is `imageMediaAssetId` or `imageUrl`, which picks the picture-right layout (`layout: "heading-image-left"` flips it), and `branding: true` adds the workspace logo (nothing is added for a workspace without one). It draws the
slide at the slideshow's size with the words exactly as given, so there is nothing to render or
upload yourself, and it uses no AI and no credits. `durationSeconds` is 1 to 99. The answer has a `previewUrl` to show the person, and `undo`.

**It shows what it is given, and nothing checks it.** The words, prices, times and phone numbers
go on the slide exactly as you send them. The tool cannot know whether a price is right or a time
is true, so the facts come from the person, you read them back before the slide goes on a screen,
and you never fill in one they did not give you.

How much text fits depends on the layout and the screen. Text over it is refused with the field and the
number that fits, never clipped, so shorten it and send again, or tell the person what you cut. Characters,
in mixed case (text mostly in capitals fits about 30% fewer):

| Layout and screen | `heading` | `text` | `cta` |
|---|---|---|---|
| `heading` | 60 | none | none |
| `heading-text` | 60 | 200 | none |
| `heading-text-cta` | 60 | 160 | 55 |
| picture layouts, 16:9 | 75 | 260 | 45 |
| picture layouts, 4:3 | 60 | 215 | 40 |
| picture layouts, square | 50 | 175 | 30 |
| picture layouts, portrait | 80 | 275 | 50 |

## When the layouts are not flexible enough: an image you make yourself

The five layouts carry a heading, some text, a call to action and one picture. When the person wants
more than that (columns, a table, a menu with many items, a logo wall, a map, text in a particular
place, a look the layouts do not have), or more text than a layout fits, design the slide as an
image yourself, show it to the person, change it until they are happy, and add it as a media slide:
`upload_media`, then `add_media_slide`. You know what they asked for, you can iterate in the
conversation, and nothing reaches the screen before they have seen it. The rest of this file is how to
do that well. A media slide is shown exactly as it is, and nothing checks what is on it either.

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
  come from what they told you. If one is missing, ask; do not fill it in. Nothing in InfoSlides checks
  that a figure on an image is right, so read the prices and times back to them before it goes on a screen.
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
