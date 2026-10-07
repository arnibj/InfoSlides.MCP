# Making a slide

For "make a slide saying the lunch moved to 1pm", start with `add_designed_slide`: a `heading`, optional `text` and
`cta`, a background (`backgroundColor`, a hex colour, or `mediaUrl`), and optionally a picture beside the text
(`imageUrl`) and `branding`. The words appear exactly as given, and nothing checks them: the facts come from the
person, so read prices and times back to them before the slide goes on a screen. A heading fits about 60 characters and text
about 160 to 200 (the picture layouts fit more or less depending on the screen, and capitals take about 30%
more room), and text that is too long is refused with the field and the number that fits rather than clipped.
`durationSeconds` is 1 to 99.

When the layouts do not offer enough flexibility (columns, a table, a menu with many items, a particular look),
there are two other routes. If the person already has the finished slide as an image at a public address, add it with
`add_media_slide` and its `mediaUrl`; it is shown exactly as it is, and nothing checks what is on it either. For a
slide designed for them, use AI Studio ([ai-studio.md](ai-studio.md)): `make_ai_slide`, show the previews to the
person, change the request until they are happy, then `insert_ai_slides`. Designing and uploading your own slide
images is not possible from here.
