# AI Studio: "make this into a slide"

`make_ai_slide` (`POST /v1/slideshows/{id}/slides/ai`) designs a new, clean slide in the
workspace's style from what the person gives you. It is not the same as showing a picture: to put
a photo on screen untouched, use `add_media_slide`. And it is not the
same as uploading a PDF, which shows the file page for page.

AI Studio is not on every plan (see Limits).

## 1. Pick the handler from what the person gave you

| The person has | `handler` | Send |
| --- | --- | --- |
| Words only ("a slide saying lunch moved to 1pm") | `prompt` | `prompt` |
| A picture to put behind their text ("use this photo as the background") | `prompt` | `prompt` with the slide's text, plus the picture as `mediaAssetId` or `mediaUrl`; it is used as the background, not read |
| A photo of a poster, whiteboard, menu or flyer | `photo` | `mediaUrl`; `prompt` optional, for direction |
| A link to a page | `url` | `url` (public http/https; pages built with JavaScript cannot be read) |
| A PDF or Word document | `document` | `mediaUrl`; makes a few slides of key points, not the pages themselves |

## 2. Write the prompt the way the person would say it, with every fact in it

AI Studio writes the copy from your prompt and never invents prices, times, phone numbers or
names, so pass them all: "Tonight's special: lamb chops, £28, served from 6pm." Say what kind of
slide it is when it helps (a menu item, an announcement, a welcome), and the tone if the person
gave one. One idea per slide: several separate ideas may come back as several slides. Leave out
layout instructions and colour codes; the template handles layout.

## 3. Branding: set `branding`, do not guess

- `"branding": true` when the person says "on brand", "with our logo", "in our colours". The
  slide features the workspace logo and brand colour. With `handler: prompt`, a workspace with no
  logo yet gets `NeedsClarification` saying where to add one.
- `"branding": false` when they want it plain.
- Omit it otherwise: the brand colour is used subtly and the logo is not featured.

## 4. Background: set `background`

- `"background": "photo"` with `"backgroundDescription": "steaming coffee cup"` for a stock photo
  behind the text. Describe the picture literally (a place, a dish, a mood), never a brand, a
  logo, a named person or text.
- `"background": "none"` for a plain colour. Best for prices, numbers, times and short notices.
- `"background": "auto"` or omit it: a photo only when it strengthens the message.
- With `handler: document`, `background: photo` gives each slide a fitting photo.

## 5. Preview, then insert

The call returns at once (202) with `statusUrl`. Poll `get_ai_slide_job`
(`GET /v1/slideshows/{id}/slides/ai/{jobId}`) at most every 5 seconds; a slide takes about 10 to
60 seconds.

- `Ready`: show the person every `previews[].previewUrl` (a PNG anyone can open). Insert only
  after they approve, with `insert_ai_slides` (`POST /v1/slideshows/{id}/slides/ai/{jobId}/insert`),
  optionally `{"position": 0}`. Nothing is on screen until then.
- `insert: true` on the first call skips the preview. Use it only when the person clearly said to
  put it up without looking.
- `NeedsClarification`: read `question` out and start a new job with the answer in the prompt.
- `Failed`: read `error`. `errorCode: EntitlementRequired` is a plan or credit limit.
- `Ready` with an `error`: inserting stopped part-way, usually storage full. Tell the person how
  many were added (`insertedSlideIds`); after they free space or manage storage, call insert again. It
  adds only the rest.
- Each slide's duration comes from its amount of text; change it with `update_slide` only if the
  person asks.

```json
POST /v1/slideshows/{id}/slides/ai
{ "handler": "prompt",
  "prompt": "Welcome to the autumn open day, Saturday 4 October, 10:00 to 16:00, main hall.",
  "branding": true,
  "background": "photo",
  "backgroundDescription": "autumn leaves on a campus lawn" }
```

## Limits

Same plan and credit limits as AI Studio in the web app. A plan without AI Studio or a
reached credit limit returns `EntitlementRequired`. Tell the person that AI Studio is not
included on their workspace plan, and that a workspace admin can update the plan in InfoSlides
account settings. Jobs are kept for 24 hours.

