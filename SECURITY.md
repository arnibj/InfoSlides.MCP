# Security Policy

## Reporting Security Issues

Please do **not** open public GitHub issues for security vulnerabilities.

Email security concerns to: admin@infoslides.app

That includes a release you suspect we did not build: a file that fails the checks below, a binary
that behaves unexpectedly, or a release you did not expect. Say which file and where you got it.

## Verifying a release

The `infoslides` program runs with your own permissions, so check what you downloaded before you run
it. Releases are built only by `.github/workflows/release.yml` in this repository, and only after
the owner approves the run.

Every archive, the `.mcpb` bundle and `server.json` carry a build provenance attestation. With the
[GitHub CLI](https://cli.github.com):

```sh
gh attestation verify <file> --repo arnibj/InfoSlides.MCP
```

It passes only for a file this repository's release workflow built. A file with even one byte
changed fails.

Without `gh`, compare the file with `sha256sums.txt` from the same release:

```sh
sha256sum -c --ignore-missing sha256sums.txt        # Linux
shasum -a 256 -c --ignore-missing sha256sums.txt    # macOS
```

```powershell
# Windows: compare the output with the file's line in sha256sums.txt
(Get-FileHash infoslides-v<version>-win-x64.zip -Algorithm SHA256).Hash
```

The checksum only proves the file matches the release page. The attestation also proves where it
was built.

## Runbook: a bad release

For the maintainer, when a release may not be ours or ships something harmful. Work through it in
order.

1. **Delete the GitHub release and its tag** so nobody downloads it:
   `gh release delete vX.Y.Z --repo arnibj/InfoSlides.MCP --cleanup-tag --yes`.
   The tag ruleset blocks deleting `v*` tags, so the owner runs this with the ruleset bypass.
2. **Unpublish the registry version**, so registry clients and the directories that poll the
   registry stop offering it:
   `mcp-publisher login github`, then
   `mcp-publisher status --status deleted --message "Withdrawn, see SECURITY.md" io.github.arnibj/infoslides X.Y.Z`.
3. **Post a platform notice** in the InfoSlides admin naming the version and what to do.
   `get_platform_notices` delivers it to every agent that uses the server.
4. **Find out how it happened** before the next tag: the workflow run that built it, who approved
   the `release` environment, and any change to the workflows or the pinned actions.
5. **Publish a fixed version** under a new version number (never reuse the bad one), following the
   release checklist in [docs/PUBLISHING.md](docs/PUBLISHING.md).
