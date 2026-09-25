---
name: infoslides-signage
description: Put content on a TV, screen, or display — a lunch menu on a screen in reception, opening hours in a shop window, a school noticeboard, a hotel lobby display, room information, a live numbers board in an office, or an Icelandic upplýsingaskjár. Use when someone wants something shown on a physical screen, wants to turn a PowerPoint or photo into something a TV can play, wants a display to update itself from live data, wants to change something already playing (turn off the news ticker, hide or delete a slide, change how long slides show, replace a presentation), or is troubleshooting a screen that is blank, frozen, or showing the wrong thing. Covers the InfoSlides MCP tools and the judgement calls they do not make for you — screen orientation, how long a slide should stay up, when a self-updating slide beats a fixed one, and how to talk someone through the TV end of it.
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

**Use a fixed picture, PowerPoint, or PDF slide when** the content changes rarely or on a human schedule —
a welcome message, a seasonal promotion, opening hours that change twice a year, a photo of the
premises. Fixed content is cheaper, works on the free plan, and cannot break by showing stale data.

**Use a self-updating slide when** the content has a source of truth that changes on its own and
would go embarrassingly stale — today's soup, the current exchange rate, live sales figures, next
departure time, queue numbers, the weather.

When the data comes from the user's own system (a queue, a till, meters, a scoreboard), build it
on a **push source**:

1. `create_template` with finished `html` and `css` and `dataMode: "push"`. Every `{{field}}` in the
   HTML becomes a field the system will send.
2. `add_dynamic_slide` with `createPushKey: true`. The result carries `sourceId`, where the data
   goes, and `pushKey`, a key that can push to that source and nothing else. It is shown once:
   hand it to the user for their system straight away.
3. `push_data` with a JSON object of the template's fields, whenever something changes.
4. `get_source_status` to confirm data is arriving, `preview_slide` to see the result.

The slide stays hidden until the first data arrives, so nobody sees an empty layout while the
user wires up their system. `update_source` on the slide still works and lands on the same source.

Things to know before recommending it:

- **Every plan includes one live data slide**, the free plan too: one push source feeding one
  slide, with one template designed in code (`html` + `css`). More, and templates generated from a
  `prompt`, need a paid plan; the tools return `EntitlementRequired` with a checkout link. Say so
  up front rather than hitting the wall in front of the user.
- **A self-updating slide that stops being fed goes stale.** A push source can hide its slide when
  no data has arrived for a set time (the user sets this on the source in the dashboard); without
  that, the slide keeps the last values. If nothing will reliably push updates, use a fixed slide.
  Never hand an admin key to a till system or a script: give it the push key.

## Designing a template that reads well on a screen

The full guide, with a worked example: https://infoslides.app/blog/agents-guide-to-the-infoslides-galaxy

- Design one 1920 x 1080 screen, in viewport units (`vh`, `vw`) so it scales to any display. Keep
  the smallest text around 2vh; it is read from across a room.
- Fields are text. Send values already formatted the way they should read (`"1,240"`, `"A-142"`).
- Wrap optional parts in a section, `{{#field}} ... {{/field}}`: it shows only when the field has a
  value that is not empty, `"0"` or `"false"`.
- Let data drive visuals through CSS: `style="--p:0{{percent}}"` with `width: calc(var(--p) * 1%)`
  draws a bar (the leading `0` keeps it empty, not full, when the value is missing).
- Send states, not colours: `class="room is-{{status}}"` and let the CSS style `busy`, `ready` and
  so on.
- Lists are numbered fields (`next1` to `next4`); there are no loops.
- `{{TenantLogo}}` and `{{AccentColor}}` are filled in from the workspace automatically.
- No scripts and no `@import`; plain CSS only. Send plain text in values, never HTML.
- If the screen shows a news ticker, leave the bottom tenth of the slide free.

## The end-to-end path

For someone with no account, in order:

1. **`create_tenant`** — anonymous, returns the admin API key. Land it somewhere the user can find
   it again and say plainly that it is shown once. The account starts on the permanent free plan:
   1 screen, 4 slideshows, 2 users, 200 MB, no card, nothing expires.
2. **`get_tenant_info`** — read the plan and screen allowance now, so the rest of the plan fits
   inside them.
3. **Content.** `upload_pptx` if they already have a deck — PowerPoint or PDF, either works.
   `clone_slideshow(fromGallery=true)` if they have nothing and want something presentable fast —
   check `list_gallery` first. `upload_slideshow` plus `add_media_slide` when building from their
   own photos.
4. **`preview_slide`** — look at it before anyone else does. See "Preview before it is public".
5. **`create_device`** — one per physical screen, named for where it is ("Reception TV", not
   "Device 1"). Match its resolution to the answer from question 1.
6. **`assign_schedule`** — connect the content to the screen. Read the response: an
   `AspectMismatch` warning here means it will be stretched or cropped. Fix it, do not ship it.
7. **`get_stream_link`** — the URL that makes it appear. This is what the user actually needs.

The first six steps are invisible to the user. Step seven is the whole point, so do not bury it —
end by handing over the link and telling them what to do with it.

## Everyday edits to a screen that is already running

Most requests after setup are edits: "turn off the news ticker in the lobby", "hide the Christmas
slide", "make every slide show 15 seconds". People name a screen or a slide, never an id, so the job
is to find it, change it, and confirm the change reached the wall.

1. **Find the screen.** `list_devices` shows each screen with `nowPlayingTitle` and
   `nowPlayingSlideshowId`. Match the person's words ("the lobby", "the cafe TV") to the screen name.
   Several matches or none: ask, do not guess. A screen with nothing playing has no slideshow to edit.
2. **Read the slideshow.** `get_slideshow` with that id shows every slide (its `type`, `hidden`,
   duration, `thumbnailUrl` and `rules`), the ticker, clock and default duration, and the `screens`
   playing it. To find "the lunch menu slide", look at the slides with `preview_slide`; the
   thumbnail is the same picture.
3. **Change it.**

   | Person says | Tool |
   | --- | --- |
   | turn the news ticker or the clock on or off, "every slide 15 seconds" | `update_slideshow` (`tickerEnabled`, `clockEnabled`, `defaultDurationSeconds`) |
   | hide or show one slide, give one slide longer | `update_slide` (`hidden`, `durationSeconds`) |
   | "only before 11 on weekdays", "only in December" | `set_slide_conditions` (`time`, `weekday`, `date`; `mode: "hide"` to hide instead) |
   | change the price or text on a live-data slide | `update_slide` with `overrideData`, or `update_source` |
   | move a slide | `update_slideshow` with the full `slideOrder` |
   | remove a slide for good | `delete_slide` |
   | replace the whole presentation with a new file | `replace_slideshow_file` (keeps the screens and schedule) |
   | scroll headlines from a feed | `list_sources`, then `update_slideshow` with `tickerSourceIds` |

4. **Wait for it to land.** After an edit the slideshow re-renders. `get_slideshow` until
   `renderStatus` is `Completed`; `Failed` means it did not reach the screen, say so.
5. **Report it.** Tell the person what changed and give them the `playerUrl` of the screen from
   `screens`, so they can look for themselves.

Two judgement calls. Hiding is reversible and deleting is not: when a slide might come back (a
seasonal offer), hide it, and confirm before `delete_slide` or `delete_slideshow` unless the person
clearly asked. And text inside an uploaded PowerPoint page cannot be edited through these tools: say
so and offer `replace_slideshow_file` with their updated file, rather than pretending to.

## Getting it onto the actual TV

The person at this end of the job is standing in a lobby holding a TV remote, not reading API docs.
Two routes:

- **The stream link.** Open `playerUrl` from `get_stream_link` in the TV's browser or the InfoSlides
  TV app — it plays the content correctly whichever mode the screen is set up for. The link is
  stable — it keeps working as the content changes, so it only has to be entered once. `hlsUrl` is
  a raw HLS manifest link for something that only speaks HLS; it comes back `null` for a screen
  playing an HTML loop (see `update_slideshow`'s `playbackMode`), so do not hand it out without
  checking it is actually present.
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
- The content came from a PowerPoint or PDF built for a projector or for print, where fonts are
  usually far too small for a screen viewed from across a room.

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
- **Stale numbers on a self-updating slide** — whatever should be sending data has stopped.
  `get_source_status` shows when data last arrived; `list_api_keys` shows each key's last use.
- **`EmailNotVerified`** — the owner has not clicked the link. `resend_verification_email`, and
  tell them to check spam.

## Free plan boundaries, stated honestly

Worth naming before they are hit rather than after:

- **1 screen.** A second `create_device` fails with `DeviceLimitReached` and an upgrade link.
- **4 slideshows, 200 MB.**
- **A small "Free plan" watermark on the stream.** Mention it before a user puts a screen in front
  of paying customers — better a heads-up than a surprise.
- **One live data slide is included; more self-updating slides and advanced scheduling need a
  paid plan.**

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
