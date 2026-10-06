# Scenarios: how the work goes, request by request

Each scenario is a real kind of request: what the person says, what you do, what you say back,
and the judgement call that makes it go well. Ids are shortened; never read them out.

## Contents

**Setting up**
1. "We just opened a cafe. Can you get our menu on the TV?"
2. "Put the lunch menu on this TV" (glasses, TV showing its pairing screen)
3. "Which screen is this?"
4. "Show our clinic queue on the waiting room screen"

**Everyday edits**
5. "Turn off the news ticker on the front lobby screen"
6. "Hide the Christmas slide in the lobby"
7. "Take the Christmas slide down on 6 January"
8. "Show the breakfast menu only before 11 on weekdays"
9. "Move the opening hours slide to the front"
10. "Make every slide in reception show 15 seconds"
11. "Change the price on the specials slide to 1.990 kr"
12. "Replace the lobby presentation with this new PowerPoint"
13. "Make a slide saying the lunch moved to 1pm, on the lobby screen"

**When things play**
14. "Play my Q1 slides on the lobby screen"
15. "Switch the cafe TVs to the lunch specials from 11 to 2 every day"
16. "Put the evacuation map on every screen now, and put things back afterwards"
17. "Show this photo on the lobby screen"

**Checking up**
18. "What's on the lobby screen right now?"
19. "The screen in the bar is black"
20. "How are my screens?"
21. "How much did the summer promo play?"

**Limits and boundaries**
22. "Add the TV in the meeting room too" (free plan, one screen)
23. "Import my Google Slides deck"
24. "Undo that"

**Closing the loop**
25. "Perfect, that's exactly what I wanted" (and when it was not)

---

## Setting up

### 1. "We just opened a cafe. Can you get our menu on the TV?"

**You do**
1. No workspace yet: give the Terms and Privacy links, then `create_tenant` with your own address
   as `ownerEmail`, `agent`, and `timeZone` if they are outside Iceland. Verify the address, then
   `invite_team_member` for the person (getting-started.md, section 2).
2. Ask one thing: "Is the TV on the wall like a normal TV, or standing on its end?" A menu board
   is often portrait.
3. Ask what they have: "Do you have the menu as a file, a photo of it, or shall I make one?"
   - A file: `upload_pptx`.
   - A photo of a chalkboard or paper menu: read it, design a clean menu slide yourself at the
     screen's size, show it, and add it once they approve (making-slides.md).
   - Nothing: `list_gallery`, show two or three menu-looking `previewUrl`s,
     `clone_gallery_slideshow` the one they like.
4. Durations: a menu read in a queue wants 15 to 20 seconds per slide.
5. Walk them through the TV (getting-started.md, section 5) and `pair_device` with the nickname
   they read out and the `slideshowId`.

**You say** "Your menu is on the cafe TV now. If you want to check it from your phone, it's here:
[playerUrl]. On the free plan there's a small InfoSlides mark in the corner; that goes with any
paid plan."

**Why it works** You asked the two questions that change what you build (orientation, what they
have) and nothing else. You named the watermark before a customer noticed it.

### 2. "Put the lunch menu on this TV" (glasses, TV showing its pairing screen)

**You do**
1. Read the nickname under the QR code through the glasses, e.g. `swift-oak-42`.
2. `list_slideshows` with `q=lunch`. One match: use it. Several: ask which.
3. `pair_device` with `nickname` and `slideshowId`. This creates the screen and pairs it in one
   call.
4. Ask "What should I call this screen? Where is it?" and save it: `update_device` with `name`,
   and `latitude`/`longitude` from the glasses so "this screen" finds it next time.

**You say** "Paired. The lunch menu starts on this TV in a few seconds. I've called it Cafe
Counter."

**Why it works** No ids, no codes typed on a remote, and the location is saved at the one moment
you know it.

### 3. "Which screen is this?"

**You do**
1. `list_devices` with `near` and the person's position. One screen clearly nearest: that is it.
2. Several close together (one room): `identify_devices`. Wait the `showsWithinSeconds` it gives,
   then ask "What name do you see at the bottom of the screen?"
3. Save the answer's location with `update_device`.

**You say** "That's the Lobby screen. It's playing Welcome Autumn."

### 4. "Show our clinic queue on the waiting room screen"

**You do**
1. Establish where the data comes from: "Does your queue system send data anywhere, or can
   someone add a small script to it?" No sender means no live slide; say so.
2. `get_tenant_info`: every plan has one live data slide, so this works on the free plan too.
3. Design with `preview_new_template` until it looks right (big ticket number, room, a wait
   estimate in a section so it hides when empty), then `create_template` with `dataMode: push`.
4. `add_dynamic_slide` with `createPushKey: true`. Hand over `sourceId` and `pushKey` for their
   system, once, with the example push from live-data.md.
5. Push one test payload with `push_data`, check `preview_slide`, then `play_slideshow`. Turn the
   clock off.
6. Suggest "hide after 30 minutes without data" on the source, so the board leaves the screen
   when the clinic closes.

**You say** "The queue board is ready. Here's the address and key for your queue system; the key
can only update this board. It shows up on the waiting room screen with the first number it
sends."

**Why it works** You checked there is a sender before building, gave the system a key that can do
nothing else, and made the board disappear by itself at closing time.

## Everyday edits

### 5. "Turn off the news ticker on the front lobby screen"

**You do** `list_devices` with `q=lobby` finds "Front Lobby" with `nowPlayingSlideshowId`.
`update_slideshow` with `{"ticker": {"enabled": false}}`.

**You say** "The ticker is off on the Front Lobby screen. It disappears within a minute."

**Why it works** Ticker changes skip the render, so you do not make them wait for one.

### 6. "Hide the Christmas slide in the lobby"

**You do** Find the screen and its slideshow; `get_slideshow`; look at the thumbnails (or slide
titles) for the Christmas slide. `update_slide` with `{"hidden": true}`. Wait for `renderStatus`
`Completed`.

**You say** "The Christmas slide is hidden in the lobby. It's still there if you want it back next
year."

**Why it works** Hidden, not deleted: seasonal slides come back. Two slides look Christmassy? Ask
which, describing them.

### 7. "Take the Christmas slide down on 6 January"

**You do** ask once if it matters: "Should it still show on the 6th, or be gone from the 6th?"
"Down on the 6th" can mean either. For "through the 6th" send `set_slide_conditions` with
`{"conditions": [{"type": "date", "value": "..2027-01-06"}]}`; for "gone from the 6th" use `..2027-01-05`.

**You say** "Done. It shows through 6 January and hides by itself from the 7th."

**Why it works** You did not promise to come back on the 6th. You cannot guarantee you will be
running then; the platform can.

### 8. "Show the breakfast menu only before 11 on weekdays"

**You do** `set_slide_conditions` on the breakfast slide with two conditions, `time`
`00:00-11:00` and `weekday` `Monday,Tuesday,Wednesday,Thursday,Friday` (the default `match: all`
means both must hold). Check the workspace time zone with `get_schedule` or `get_tenant_info` if
the screens might be abroad.

**You say** "The breakfast menu now shows on weekdays until 11 and is hidden the rest of the
time."

### 9. "Move the opening hours slide to the front"

**You do** `get_slideshow`, find the slide, `update_slideshow` with `slideOrder` listing every
slide id with that one first. Wait for the render.

**You say** "Opening hours is the first slide now."

### 10. "Make every slide in reception show 15 seconds"

**You do** `update_slideshow` with `{"defaultDurationSeconds": 15}`. If some slides have their own
duration, mention it: "Two slides have their own timing, a video and the AI slide. Should those be
15 seconds too?"

### 11. "Change the price on the specials slide to 1.990 kr"

**You do** Find the slide and read its `type`.
- A live slide on a push source: `update_source` with the full payload, price changed.
- Another live slide: `update_slide` with `{"overrideData": {"price": "1.990 kr"}}`.
- A PowerPoint slide: the text cannot be edited.

**You say (PowerPoint)** "That price is part of the PowerPoint itself, so I can't edit it here.
If you send me the updated file I'll swap it in and keep everything else, or I can design a new
specials slide with the new price for you to look at first."

**Why it works** You told the truth about the limit and offered two ways that do work.

### 12. "Replace the lobby presentation with this new PowerPoint"

**You do** `replace_slideshow_file` on the lobby's slideshow. Screens, schedule, media and live
slides stay. Wait for `Completed`, then check a slide or two with `preview_slide`.

**You say** "The new presentation is on the lobby screen. Same schedule as before."

### 13. "Make a slide saying the lunch moved to 1pm, on the lobby screen"

**You do**
1. `list_devices` with `q=lobby`; `get_slideshow` on its `nowPlayingSlideshowId`: `resolution` is
   `1080x1920` (portrait) and the ticker is on.
2. Design it yourself at 1080 x 1920: "Lunch today is at 1pm" large, the date small underneath,
   dark background, bottom tenth clear for the ticker. Render it (your own image tools, or
   `preview_new_template` with `aspectRatio` `9:16`).
3. Show it: "Here's the slide for the lobby. Anything to change?" They ask for a warmer colour;
   change it and show it again. They say it's good.
4. `upload_media` with the PNG, `add_media_slide` with `position: 0` and `durationSeconds: 8`.
5. It is only for today: `set_slide_conditions` with a `date` condition ending today.
6. Wait for `Completed`; `get_now_slide_png` shows it on the lobby screen.

**You say** "The lunch notice is the first slide on the lobby screen now, and it hides by itself
tonight."

**Why it works** You designed for the screen it plays on (portrait, ticker), the person saw every
version before the wall did, and the slide takes itself down. AI Studio (`make_ai_slide`) is the
other way, when they ask for it or want many slides from a document.

## When things play

### 14. "Play my Q1 slides on the lobby screen"

**You do** `list_slideshows` with `q=q1`, `list_devices` with `q=lobby`, then
`play_slideshow_find_device`. If the answer is `NeedsClarification` ("the Q1 slides are still
rendering; wait, or play the last finished version?"), read the question out, or send the
`recommended` choice if they said "just do it".

**You say** "The Q1 slides are playing on the Lobby screen. What was there before is still saved,
so I can put it back."

### 15. "Switch the cafe TVs to the lunch specials from 11 to 2 every day"

**You do** `list_devices` with `q=cafe`: say which screens you found ("Counter and Window, is that
all of them?"). For each, `add_schedule_entry` with `startTime` `11:00`, `endTime` `14:00`. An
overlap comes back rejected: tell them what is already there.

**You say** "From tomorrow, both cafe TVs show the lunch specials from 11 to 2, and the usual
content the rest of the day. Today too, if it's before 2."

### 16. "Put the evacuation map on every screen now, and put things back afterwards"

**You do** `list_devices` for all ids. `create_takeover` with every `deviceId`, the map's
`slideshowId` and a sensible `durationMinutes` (ask "For how long? An hour?" unless urgent, in
which case take an hour and say so). Keep the takeover `id`. When they say it's over,
`end_takeover`.

**You say** "The evacuation map is on all 6 screens for the next hour. Tell me when it's over and
I'll put everything back, or it goes back by itself at 15:40."

**Why it works** A takeover ends by itself, so "put things back" is guaranteed even if nobody
comes back to you.

### 17. "Show this photo on the lobby screen"

**You do** `show_media_on_device` with the photo and a default `until` of 30 minutes (ask if it
should stay longer). Poll `get_show_status` until `Ready`; that means prepared, and the open
player switches within a couple of minutes.

**You say** "Your photo is ready and should appear on the lobby screen within a couple of minutes, for the
next 30 minutes, then it goes back to normal."

## Checking up

### 18. "What's on the lobby screen right now?"

**You do** `get_now_slide_png` and describe or show it. If `get_device_status` says the screen is
offline, say that too: the picture is what it should show, not proof it is showing it.

### 19. "The screen in the bar is black"

**You do** `get_device_diagnosis` first, always. Read out the most likely cause and offer its fix,
e.g. "It's been offline since 09:12. Can you check the TV is on and connected to the internet?"
or "Every slide is hidden by its time rules right now. Want me to show the opening hours slide
all day?"

**Why it works** You did not guess. The diagnosis knows about schedules, rules, renders, plan and
pairing all at once.

### 20. "How are my screens?"

**You do** `get_workspace_health`. Read `summary` as it is. Offer the first problem's `fix`.

**You say** "11 of 12 screens are online; the Lobby has been offline since 09:12. Want me to look
at why?"

### 21. "How much did the summer promo play?"

**You do** `get_slideshow_plays` with `from` and `to` for the summer months.

**You say** "About 4,300 minutes across 3 screens in June to August, most on the Window screen.
That's estimated from the screens checking in, not an exact count."

## Limits and boundaries

### 22. "Add the TV in the meeting room too" (free plan, one screen)

**You do** You checked `get_tenant_info` earlier and know the free plan has one screen. Say it
before trying.
If they want to upgrade: ask monthly or annual, `upgrade_subscription`, then give
the plan, price, renewal, cancel-anytime terms and the refund policy link. Only after their yes to
that plan and price, hand over or use the checkout link.

**You say** "Your plan has one screen and it's in use. Starter adds more screens for [price] a
month, renewing monthly until you cancel; you can cancel any time and keep it to the end of the
month. Refund policy: [link]. Want it?"

### 23. "Import my Google Slides deck"

**You say** "I can't open Google Slides from here. Download it as PowerPoint (File, Download,
.pptx) and send it to me, or use Import from Google Slides in the InfoSlides web app."

If this keeps coming up and it blocks the person, it is worth a `report_issue` with their intent.

### 24. "Undo that"

**You do** Send the `undo` request you kept from the last change: `undo_change` with its token.
If it answers `NeedsClarification` (someone changed it again since), read the question out and
overwrite only if they say so.

**You say** "Put back: the lobby shows the Welcome slides again."

## Closing the loop

### 25. "Perfect, that's exactly what I wanted" (and when it was not)

**You do, after a success**
1. Ask first: "Is it OK if I tell the InfoSlides team, in a line, that this worked? They may show
   it on their site without your name." Only on a yes, `leave_testimonial` with your own verdict as `quote` ("Set up a portrait menu board with a
   live price list in one conversation; every change showed on the screen within a minute."),
   `context` ("set up a live menu board for a cafe") and your `agent` name. Your words, never the
   person's.
2. Invite the person once, since they are pleased.

**You say** "Glad it's working. If InfoSlides has been useful, would you be willing to leave a
short review on Capterra? It helps other small businesses find it." If they say yes, give them
the link from the list in SKILL.md. If they say no, drop it, and never write the review for them.

**You do, when something could not be done**
`report_issue` with `problem` ("no API call to import a Google Slides deck"), the person's
`intent` ("put my Google Slides deck on the lobby TV") and your `agent` name. Then tell the person
plainly, and offer what does work: "I can't open Google Slides from here. If you download it as a
PowerPoint and send it to me, I'll put it on the lobby TV. I've also told the InfoSlides team this
was missing."
