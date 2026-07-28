# Publishing a release

Two artefacts go out with every release: the plain per-platform archives that direct CLI users
download, and a `.mcpb` bundle that the MCP registry and MCP clients install from. Both come out of
the same tag push; only the registry submission is manual.

## What gets built

`.github/workflows/release.yml` runs on a `v*` tag and produces, in the GitHub release:

| Artefact | For |
| --- | --- |
| `infoslides-v<version>-{win-x64,linux-x64,osx-arm64}.{zip,tar.gz}` | Direct CLI users |
| `infoslides-mcp-v<version>.mcpb` | MCP registry and MCP clients |
| `server.json` | The registry submission |
| `sha256sums.txt` | Verification for all of the above |

The plain archives are kept deliberately. The dependency-free Native AOT binary is a real selling
point, and MCPB is an installation channel, not a replacement for it.

## The `.mcpb` bundle

A `.mcpb` is a plain zip: `manifest.json` at the root plus the files it references. One bundle
carries **all three** Native AOT binaries under `server/`, and `platform_overrides` in the manifest
picks the right one at runtime. That is why there is a single artefact and a single `fileSha256`,
which is the shape the registry expects.

`scripts/build-mcpb.sh` assembles it and renders `server.json` with the bundle's hash. It is run by
CI, but works locally too:

```bash
scripts/build-mcpb.sh 1.1.0 ./staging ./out
```

`./staging` must contain `win-x64/infoslides.exe`, `linux-x64/infoslides`, and
`osx-arm64/infoslides`. The script refuses to build with a platform missing — a bundle short one
binary installs cleanly and then fails at first use on that platform, which is the worst possible
failure shape.

Locally on Windows the script falls back to Python's `zipfile` when `zip` is absent. That fallback
does not preserve the executable bit, so it is fine for inspection and **must not** be released.
CI runs on Ubuntu, where `zip` is present.

### Templates

`mcpb/manifest.json.template` and `server.json.template` carry `__VERSION__` and `__SHA256__`
placeholders. The build substitutes them and fails if any placeholder survives. Both files are
deliberately comment-free — they are published artefacts, and a stray `"//"` key is a needless bet
on how strictly a downstream consumer parses them.

Two constraints worth knowing before editing either template:

- **`description` in `server.json` is capped at 100 characters** by the registry schema. It is a
  listing line, not a pitch. Keep the trigger words (TV, screen, menu board); the README carries
  the rest.
- **The bundle URL must contain the string `mcp`.** The `.mcpb` extension satisfies this on its
  own; the `-mcp-` in the file name makes it obvious to a human reading the release too.

### Validating before you tag

Both schemas are public, and both are worth checking — a bad `server.json` is rejected after the
release already exists.

```bash
npx @anthropic-ai/mcpb validate ./out/manifest.json
```

```bash
curl -fsSL https://static.modelcontextprotocol.io/schemas/2025-12-11/server.schema.json -o schema.json
python -c "import json,jsonschema; jsonschema.validate(json.load(open('out/server.json')), json.load(open('schema.json')))"
```

## Release checklist

1. Bump `<Version>` in `src/InfoSlides.Cli/InfoSlides.Cli.csproj` **and** `VersionInfo.Version` —
   they are two separate constants and drift silently.
2. `dotnet test`.
3. Tag and push: `git tag v1.1.0 && git push origin v1.1.0`. CI builds everything above.
4. **Install the `.mcpb` from the release in a real MCP client and use it.** Not optional — this is
   the only step that exercises the manifest's command paths and platform overrides end to end. A
   manifest that validates can still point at the wrong file.
5. Publish to the registry (below).
6. Submit to the third-party directories (below).

## Publishing to the MCP registry

The namespace is `io.github.arnibj/infoslides`, verified by GitHub OIDC — no secret to manage, but
it does mean the workflow must run in this repository.

Publishing is a **manual** `workflow_dispatch`: run **Publish to MCP Registry** from the Actions
tab and give it the version. It is deliberately not automatic on tag, because the listing is public,
awkward to retract, and seeds the downstream directories that poll the registry. Step 4 above
should have happened first.

The workflow pulls `server.json` from the release rather than rebuilding it, then re-downloads the
bundle and checks the hash still matches before publishing. A mismatch there means clients would
refuse to install, so it fails rather than listing a broken package.

To publish by hand instead:

```bash
curl -L "https://github.com/modelcontextprotocol/registry/releases/latest/download/mcp-publisher_$(uname -s | tr '[:upper:]' '[:lower:]')_$(uname -m | sed 's/x86_64/amd64/;s/aarch64/arm64/').tar.gz" | tar xz mcp-publisher
./mcp-publisher login github
./mcp-publisher publish
```

Run it from a directory containing the release's `server.json`.

## Third-party directories

Publishing to the official registry seeds the mirrors, but the larger directories are separately
indexed and separately crawled, so each is worth a direct submission in the same week:

- **Smithery** — <https://smithery.ai>
- **Glama** — <https://glama.ai/mcp/servers>
- **mcp.so** — <https://mcp.so>
- **PulseMCP** — <https://www.pulsemcp.com>

Digital signage has no MCP server in any of them today, so the category is unclaimed. Lead each
submission with the outcome — an agent can put content on a real TV — rather than the tool list.
