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
1. No workspace yet: guide them to connect their InfoSlides account or sign in at https://infoslides.app (getting-started.md, section 2).
2. Ask one thing: "Is the TV on the wall like a normal TV, or standing on its end?" A menu board
   is often portrait.
3. Ask what they have: "Do you have the menu as a file, a photo of it, or shall I make one?"
   - A file: uploaded in the InfoSlides web app (you cannot upload from here).
- A photo of a chalkboard or paper menu: `make_ai_slide` with handler `photo` and the photo's `mediaUrl`
        (ai-studio.md); show the previews and `insert_ai_slides` once they approve.
   - Nothing: `list_gallery`, show two or three menu-looking `previewUrl`s,
     `clone_slideshow` the one they like.
4. Durations: a menu read in a queue wants 15 to 20 seconds per slide.
5. Walk them through the TV (getting-started.md, section 5) and `pair_device` with the nickname
   they read out and the `slideshowId`.

**You say** "Your menu is on the cafe TV now. If you want to check it from your phone, it's here:
[playerUrl]."

**Why it works** You asked the two questions that change what you build (orientation, what they
have) and nothing else. You named the watermark before a customer noticed it.

### 2. "Put the lunch menu on this TV" (glasses, TV showing its pairing screen)

**You do**
1. Read the nickname under the QR code through the glasses, e.g. `swift-oak-42`.
2. `list_slideshows` with `q=lunch`. One match: use it. Several: ask which.
3. `pair_device` with `nickname` and `slideshowId`. This creates the screen and pairs it in one
   call.
4. Ask "What should I call this screen?" and save it: `update_device` with `name` so "this screen" or its name finds it next time.

**You say** "Paired. The lunch menu starts on this TV in a few seconds. I've called it Cafe
Counter."

**Why it works** No ids and no codes typed on a remote.

### 3. "Which screen is this?"

**You do**
1. If you're not sure which screen they mean: `identify_devices`. Wait the `showsWithinSeconds` it gives,
   then ask "What name do you see at the bottom of the screen?"
2. When they reply with the screen name, look it up with `list_devices` with `q`.

**You say** "That's the Lobby screen. It's playing Welcome Autumn."

### 4. "Show our clinic queue on the waiting room screen"

**You do**
1. Establish where the data comes from: "Does your queue system send data anywhere, or can
   someone add a small script to it?" No sender means no live slide; say so.
2. `get_tenant_info`: every plan has one live data slide, so this works on the free plan too.
3. The live data template is designed in the InfoSlides web app; `list_gallery` shows ready-made ones. Ask which one
   they want.
4. `add_dynamic_slide` with the template. Hand over `sourceId`; the push key for their system is created in the
   InfoSlides web app.
5. Push one test payload with `push_data`, check `preview_slide`, then `play_slideshow_find_device`. Turn the
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

**You do** nothing here: replacing the file is done in the InfoSlides web app. When the person does it, screens,
schedule, media and live slides stay.

**You say** "The new presentation is on the lobby screen. Same schedule as before."

### 13. "Make a slide saying the lunch moved to 1pm, on the lobby screen"

**You do**
1. `list_devices` with `q=lobby`; `get_slideshow` on its `nowPlayingSlideshowId`: `resolution` is
   `1080x1920` (portrait) and the ticker is on.
2. `add_designed_slide` on that slideshow with the heading "Lunch today is at 1pm", a dark
   `backgroundColor`, `position: 0` and `durationSeconds: 8`. It draws the slide at the screen's own
   size (1080 x 1920 here) with the words exactly as the person said them, and answers with a
   `previewUrl` and `undo`. The heading is short, so it fits the layout.
3. Show it: "Here's the slide for the lobby. Anything to change?" It is already in the slideshow, so
   say that, and that you can take it out. They ask for a warmer colour: undo it, add it again with
   the new colour and show that. They say it's good.
   If they had wanted something the layouts cannot do (a table of the day's dishes, say), you would use
   `add_media_slide` with the `mediaUrl` of an image they already have, or `make_ai_slide` and `insert_ai_slides`.
4. It is only for today: `set_slide_conditions` with a `date` condition ending today.
5. Wait for `Completed`; `get_now_slide_png` shows it on the lobby screen.

**You say** "The lunch notice is the first slide on the lobby screen now, and it hides by itself
tonight."

**Why it works** `add_designed_slide` is the quickest path for a slide that is only words: it
draws for the screen it plays on without you rendering anything, and it shows exactly what the
person said. Nothing checks that the words are right, so every fact comes from them. The person saw
the slide and could take it out, and it takes itself down. When the layouts are not flexible enough,
a finished image added as a media slide is the better fit; AI Studio (`make_ai_slide`) is the other
way, when they ask for it or want many slides from a document.

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

**You do** add the photo as a slide using `add_media_slide` with its `mediaUrl`, to the slideshow playing on the lobby screen, and optionally set a temporary condition with `set_slide_conditions`.

**You say** "Your photo is on the lobby screen now."

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

**You do** nothing here: play-time reports are in the InfoSlides web app. Say so.

## Limits and boundaries

### 22. "Add the TV in the meeting room too" (free plan, one screen)

**You do** You checked `get_tenant_info` earlier and know the free plan has one screen. Say it
before trying.
Explain the plan screen limit neutrally and direct them to their InfoSlides account settings if they want to connect more screens.

**You say** "Your current plan includes 1 screen, which is currently in use. To connect additional screens, you can adjust your plan in your InfoSlides account settings at https://infoslides.app."

### 23. "Import my Google Slides deck"

**You say** "I can't open Google Slides from here. Download it as PowerPoint (File, Download,
.pptx) and send it to me, or use Import from Google Slides in the InfoSlides web app."


### 24. "Undo that"

**You do** Send the `undo` request you kept from the last change: `undo_change` with its token.
If it answers `NeedsClarification` (someone changed it again since), read the question out and
overwrite only if they say so.

**You say** "Put back: the lobby shows the Welcome slides again."

## Closing the loop

### 25. "Perfect, that's exactly what I wanted" (and when it was not)

**You do, after a success**
Thank the person and confirm everything is running smoothly.

**You say** "Glad it's working! Let me know if you'd like to adjust any slides or schedules."

**You do, when something could not be done** Tell the person plainly, and offer what does work: "I can't open
Google Slides from here. If you download it as a PowerPoint and upload it in the InfoSlides web app, I'll put it
on the lobby TV."
