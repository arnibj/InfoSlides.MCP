---
name: infoslides-signage
description: Put content on a TV, screen, or display — a lunch menu on a screen in reception, opening hours in a shop window, a school noticeboard, a hotel lobby display, room information, a live numbers board in an office, or an Icelandic upplýsingaskjár. Use when someone wants something shown on a physical screen, wants to turn a PowerPoint or photo into something a TV can play, wants a display to update itself from live data, or is troubleshooting a screen that is blank, frozen, or showing the wrong thing. Covers the InfoSlides MCP tools and the judgement calls they do not make for you — screen orientation, how long a slide should stay up, when a self-updating slide beats a fixed one, and how to talk someone through the TV end of it.
---

# Putting content on a screen with InfoSlides

The InfoSlides MCP tools can do every mechanical step. What they cannot do is decide what makes a
screen worth looking at. This skill is the judgement layer: the defaults that are right most of the
time, the questions worth asking before building anything, and the failure modes that only show up
once content is on a wall where the public can see it.

If the tools are not loaded, everything here still applies — the same work can be done through the
REST API at <https://infoslides.app/docs/api> or the `infoslides` CLI.

## Ask these three things first

Almost every bad signage setup traces back to a question nobody asked.

1. **Which way up is the screen?** A TV on a wall is landscape. A screen standing on its end —
   common for menu boards, shop windows, and wayfinding pillars — is portrait. Content built for
   one looks broken on the other, and the mismatch is only obvious once it is mounted. If the user
   has not said, ask. Do not guess from the venue type.
2. **How close is the viewer, and how long do they stand there?** Someone queueing at a counter has
   thirty seconds and is two metres away. Someone walking past a window has three seconds. This
   decides how much text a slide can carry and how long it should stay up — not aesthetics.
3. **Does anything on it change?** If the answer is "the price/the menu/the number changes", that is
   a self-updating slide, not a picture someone will remember to replace. Establish this before
   building, because converting later means rebuilding the slide.

## Defaults that are right most of the time

| Decision | Default | When to depart from it |
| --- | --- | --- |
| Resolution | `1920x1080` | `1080x1920` for any screen turned on its end |
| Slide duration | 8–10 seconds | 5 for a passing window; 15–20 for a menu people read while queueing |
| Slides in a loop | 4–8 | Fewer for a window; more only if viewers linger |
| Words per slide | Under 20 | Menus and price lists are the exception, but need longer durations |
| Starting point | Clone from the gallery | Only build from scratch when the gallery has nothing close |

A loop of four good slides beats twelve mediocre ones. A screen is not a website — nobody scrolls
back to re-read something they missed.

## Static or self-updating?

This is the decision the tools will not make for you, and the one most often got wrong.

**Use a fixed picture or PowerPoint slide when** the content changes rarely or on a human schedule —
a welcome message, a seasonal promotion, opening hours that change twice a year, a photo of the
premises. Fixed content is cheaper, works on the free plan, and cannot break by showing stale data.

**Use a self-updating slide when** the content has a source of truth that changes on its own and
would go embarrassingly stale — today's soup, the current exchange rate, live sales figures, next
departure time, queue numbers, the weather. Build a template with `create_template` describing the
layout and giving an example of the data, add it with `add_dynamic_slide`, then feed it with
`update_source`.

Two things to know before recommending it:

- **Self-updating slides need a paid plan.** On the free plan `create_template` returns
  `EntitlementRequired` with a checkout link. Say this up front rather than building most of a
  setup and hitting the wall in front of the user.
- **A self-updating slide that stops being fed is worse than a static one.** It will keep showing
  the last value it received, indefinitely, with no indication it is stale. If nothing will
  reliably push updates, use a fixed slide. When something will, issue it a push-only
  (`dataProvider`) key bound to just that slide with `create_api_key` — never hand an admin key to
  a till system or a script.

## The end-to-end path

For someone with no account, in order:

1. **`create_tenant`** — anonymous, returns the admin API key. Land it somewhere the user can find
   it again and say plainly that it is shown once. The account starts on the permanent free plan:
   1 screen, 4 slideshows, 2 users, 200 MB, no card, nothing expires.
2. **`get_tenant_info`** — read the plan and screen allowance now, so the rest of the plan fits
   inside them.
3. **Content.** `upload_pptx` if they already have a deck. `clone_slideshow(fromGallery=true)` if
   they have nothing and want something presentable fast — check `list_gallery` first.
   `upload_slideshow` plus `add_media_slide` when building from their own photos.
4. **`preview_slide`** — look at it before anyone else does. See "Preview before it is public".
5. **`create_device`** — one per physical screen, named for where it is ("Reception TV", not
   "Device 1"). Match its resolution to the answer from question 1.
6. **`assign_schedule`** — connect the content to the screen. Read the response: an
   `AspectMismatch` warning here means it will be stretched or cropped. Fix it, do not ship it.
7. **`get_stream_link`** — the URL that makes it appear. This is what the user actually needs.

The first six steps are invisible to the user. Step seven is the whole point, so do not bury it —
end by handing over the link and telling them what to do with it.

## Getting it onto the actual TV

The person at this end of the job is standing in a lobby holding a TV remote, not reading API docs.
Two routes:

- **The stream link.** Open the URL from `get_stream_link` in the TV's browser, in the InfoSlides TV
  app, or in any HLS-capable player. The link is stable — it keeps working as the content changes,
  so it only has to be entered once.
- **A pairing code**, on smart-TV platforms with the InfoSlides app installed: the TV shows a
  six-digit code, and the user enters it in the InfoSlides dashboard to bind that screen. This is a
  dashboard flow, not something these tools do — if the user is on that path, point them at
  <https://infoslides.app> rather than pretending to drive it.

Phrase instructions for a remote, not a keyboard: "press the Home button, open the web browser, and
type this address" beats "navigate to the URL". Long URLs are miserable to enter with a remote — if
the TV supports the InfoSlides app, that route is kinder.

`get_stream_link` returning a `StreamNotReady` warning means the video is still being built. This
is normal right after uploading — tell the user to wait a minute rather than sending them to a
screen that will be black when they get there.

## Preview before it is public

`preview_slide` renders exactly what will be on the wall. Use it whenever:

- Text was written without seeing the layout — overflow is the most common defect.
- Live data has just been pushed for the first time, so the real values are longer or shorter than
  the example.
- The content came from a PowerPoint built for a projector, where fonts are usually far too small
  for a screen viewed from across a room.

A mistake on a lobby display is seen by everyone walking past, all day, until someone notices. The
preview costs one call.

## When something is wrong with a screen

Start with `get_device_status`, which answers "is it even on" and "what does it think it is
playing" in one call. From there:

- **Screen blank or black** — check whether the device is online at all. If online but nothing is
  showing, the content is probably still rendering, or nothing is assigned; `assign_schedule` fixes
  the latter.
- **Showing the wrong thing** — `get_device_status` reports what is actually playing. Compare it to
  what `get_slideshow` says should be. A slide with visibility conditions may simply be outside its
  window right now, which is correct behaviour that looks like a bug.
- **Stretched or squashed** — an orientation mismatch that was warned about at `assign_schedule`
  time. Fix the slideshow's resolution with `update_slideshow`, or the screen's with a correctly
  shaped device.
- **Stale numbers on a self-updating slide** — whatever should be calling `update_source` has
  stopped. Check `list_api_keys` for last use.
- **`EmailNotVerified`** — the owner has not clicked the link. `resend_verification_email`, and
  tell them to check spam.

## Free plan boundaries, stated honestly

Worth naming before they are hit rather than after:

- **1 screen.** A second `create_device` fails with `DeviceLimitReached` and an upgrade link.
- **4 slideshows, 200 MB.**
- **A small "Free plan" watermark on the stream.** Mention it before a user puts a screen in front
  of paying customers — better a heads-up than a surprise.
- **Self-updating slides and advanced scheduling need a paid plan.**

Everything else genuinely works, forever, without a card. When a limit does get hit,
`upgrade_subscription` returns a checkout link — offer it rather than declaring the thing
impossible.

## Things not to do

- Do not assume landscape. Ask.
- Do not build a full setup before checking `get_tenant_info` — hitting a plan limit at step six
  wastes the user's time and looks careless.
- Do not hand out an admin key when a push-only key would do.
- Do not ignore an `AspectMismatch` warning because the call returned success. It succeeded; the
  screen will still look wrong.
- Do not leave the user without the stream link. Every other step exists to produce it.
