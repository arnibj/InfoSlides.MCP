# InfoSlides.MCP - Project Context for Claude Code

## Git workflow

The GitHub plan has limited Actions minutes and CI runs on pushes, so work stays local until the
user pushes it. The `infoslides` repository follows the same rules.

- **Never push.** No `git push` (branches or tags) and no pull requests. The user pushes local
  `main` manually every now and then.
- **Work on a local branch**, created from local `main`: `feature/brief-description` or
  `fix/brief-description`.
- **Finished work always ends on local `main`.** When a fix or feature is done:
  1. Commit on the branch, staging only the files this work changed by path (other sessions may
     have edits in this checkout).
  2. `git checkout main`, then `git merge --no-ff <branch>`.
  3. If the checkout or merge is blocked by conflicts or someone else's uncommitted changes, stop
     and ask.
- **Never `git reset`** unless explicitly asked.
- Finish this before starting the next piece of work, so each one branches from an up-to-date
  `main`.
