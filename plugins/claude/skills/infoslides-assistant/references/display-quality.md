# Display quality: make it fit the screen and keep it on

For when the picture on the TV is not right, and for the check right after a slideshow plays on a
screen for the first time. Most of these problems are in the TV's own settings, not in InfoSlides,
so the work is mostly asking the right question and walking the person through their remote.

## Contents

1. Ask once, after the first play
2. What the person sees, and what usually causes it
3. Is it the slideshow? Check the resolution first
4. Is it the TV? Find the brand
5. Find the setting for that TV
6. The TV turns itself off
7. What you cannot see from here

## 1. Ask once, after the first play

When a slideshow plays on a screen for the first time (after pairing, after
`play_slideshow_find_device`, or on a new screen), ask one question before moving on:

> "Is it on the screen now? Does everything fit, or is anything cut off at the edges, stretched
> or squeezed?"

`get_now_slide_png` shows what InfoSlides sends, not what the TV makes of it, so only the person
can answer this. Ask it once per screen. If they say it looks good, drop the subject. If not,
work through the sections below with them.

## 2. What the person sees, and what usually causes it

| The person says | Usually | Go to |
| --- | --- | --- |
| "The edges are cut off", "the clock is squeezed into the corner", "the bottom of the ticker is missing" | The TV zooms the picture (overscan). Older TVs do it by default | Section 4 |
| "There are black bars on the sides / top and bottom" | The slideshow's shape does not match the screen: a 4:3 deck on a 16:9 TV, or landscape content on a portrait screen | Section 3 |
| "Everything looks stretched" or "squashed" | The TV's picture size is set to stretch (often called 16:9, Wide or Full), or the slideshow has the wrong shape | Section 3, then 4 |
| "It's sideways" / "it's tiny in the middle of a tall screen" | A portrait screen showing a landscape slideshow, or the other way round | Section 3 |
| "It's blurry" or "the text is fuzzy" | The slideshow renders at 720p, or the TV upscales and sharpens. Text far too small for the viewing distance looks fuzzy too | Section 3 |
| "It switches off after a few hours" / "it's off every morning" | The TV's auto power off or energy saving | Section 6 |
| "The picture is dim" | Energy saving mode, or a picture preset for a dark room | Section 6 |

Cut-off edges and a TV that turns itself off are by far the most common. Neither is fixed in
InfoSlides.

## 3. Is it the slideshow? Check the resolution first

Rule out the cause you can see from here before you send the person into menus.

1. `get_slideshow` (`GET /v1/slideshows/{id}`): read its `resolution` (`width` and `height`).
2. `list_devices` (`GET /v1/devices`): read the screen's `resolution`.
3. Ask which way up the screen is if you do not know: on a wall like a normal TV (landscape), or
   standing on its end (portrait).

A slideshow renders at its own resolution: one set on it explicitly, otherwise the size of the
uploaded file. The screen's resolution is a setting you give it, because TVs do not report their
panel size. It is what the `AspectMismatch` warning compares against, so set it to match the
real screen.

| The real screen | Slideshow and screen resolution |
| --- | --- |
| A normal TV or monitor on a wall | `1920x1080` |
| A TV standing on its end | `1080x1920` |
| A square display | `1080x1080` |
| An old 4:3 deck the person does not want redesigned | `1440x1080` plays at its own shape with bars on the sides; that is expected |

`1280x720` and `720x1280` exist for slow connections. Do not choose them to fix a fit problem.
They are softer on a large TV.

When the slideshow is the wrong shape for the screen:

- `update_slideshow` (`PATCH /v1/slideshows/{id}`) with `width` and `height` (MCP) or
  `resolution: {width, height}` (REST). This re-renders the slideshow: wait for `renderStatus`
  `Completed`. Uploaded PowerPoint or PDF pages keep their own shape inside the new frame, so a
  landscape deck on a portrait setting gets bars above and below. If the content itself is the
  wrong shape, say so: the real fix is a version of the file made for that screen.
- `update_device` (`PATCH /v1/devices/{id}`) with `width` and `height` (MCP) or
  `resolution: {width, height}` (REST), so the screen's setting matches the real panel.
  Changing it alone does not change the picture; it changes which warnings you get.

`update_slideshow` returns `undo`; `update_device` does not, so note the screen's old resolution
before you change it. Change either only after the person has told you which way up the screen
is. Never guess it from the venue.

If both resolutions are already right and the problem is cut-off edges, stretching or the TV
switching off, the cause is in the TV. Go on to section 4.

## 4. Is it the TV? Find the brand

TV settings differ by brand and model, so ask:

> "What make of TV is it? The name is usually on the front of the frame, below the screen, or on
> the remote. It also often appears for a moment when the TV is switched on."

If they are not sure, offer those three places one at a time. The model number helps too, and is
on a sticker on the back, but the brand is enough to start.

If a stick or box drives the screen (a Fire TV Stick, an Android TV box), the cropping still
comes from the TV, or the monitor, it is plugged into. Ask for that brand. Some boxes have their
own display or screen-size setting as well; mention it only if the TV's settings do not help.

A computer monitor rarely crops, but it may have a picture size setting (often "aspect" or
"1:1") in its own on-screen menu.

## 5. Find the setting for that TV

Once you know the brand, search the web for that brand's setting, if you can search: for example
"Samsung TV picture size screen fit", "LG TV just scan", "Sony TV display area full pixel",
"Philips TV picture format unscaled". Prefer the maker's own support pages. Then walk the person
through it on the remote, one step at a time, and wait for each answer.

What to look for, by the names makers commonly use:

- **Cut-off edges:** the picture size, aspect ratio or picture format setting, set to the option
  that shows the whole image: Just Scan, Screen Fit, Fit to Screen, Full Pixel, 1:1, Original,
  Unscaled or Dot by Dot. Some TVs have a separate overscan switch; turn it off.
- **Stretched picture:** the same setting, away from Wide, Zoom or Stretch.
- **On an older TV the option may only appear while the HDMI input is showing,** not on the
  TV's home screen. Ask the person to open the setting while InfoSlides is playing.

If you cannot search the web, describe the setting with the names above and ask the person to
look under Picture, Picture size or Aspect ratio in the TV's settings. Use `search_articles` for
InfoSlides' own help pages.

After the change, ask again whether everything fits. If nothing in the TV fixes cut-off edges,
the slideshow can keep its important content away from the edges: a margin of about 5% on every
side is safe on any TV.

## 6. The TV turns itself off

Most consumer TVs switch themselves off after a few hours without a button press on the remote
(auto power off, often after 2 to 4 hours), and an energy saving or eco mode can dim the picture
or put the TV to sleep. A signage screen gets no remote input all day, so these settings end its
day early.

Ask for the brand as in section 4, search for that brand's setting the same way, and walk the
person through turning off:

- auto power off, auto standby or the no-signal / no-operation timer,
- any sleep timer or off timer,
- energy saving or eco mode, or set it to low.

Ask whether the screen should run all day or only during opening hours. For opening hours, many
TVs have an on timer and an off timer that can switch the screen on and off at set times; that
saves power without the screen dying at noon.

If the screen goes off but the TV is still on (a black InfoSlides screen), that is not the TV:
run `get_device_diagnosis` first.

## 7. What you cannot see from here

- `get_now_slide_png` shows the slide InfoSlides sends. It does not show overscan, stretching,
  brightness or a TV that is switched off. For those, trust what the person tells you.
- InfoSlides cannot change the TV's settings, the remote is the only way. Say so plainly
  rather than suggesting a fix that does not exist.
- Presentations play without sound, so the TV's sound settings never matter here.
