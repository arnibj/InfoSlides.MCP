#!/usr/bin/env bash
# Assembles the .mcpb bundle the MCP registry installs from, and renders server.json with the
# bundle's SHA-256.
#
# A .mcpb is a plain zip: manifest.json at the root plus the files it references. One bundle
# carries all three Native AOT binaries, so there is a single artifact and a single fileSha256 —
# the manifest's platform_overrides pick the right one at runtime.
#
# Usage:  scripts/build-mcpb.sh <version> <staging-dir> [output-dir]
#
#   <version>       Release version without the leading "v", e.g. 1.1.0
#   <staging-dir>   Directory holding the published binaries, one subdirectory per RID:
#                     <staging>/win-x64/infoslides.exe
#                     <staging>/linux-x64/infoslides
#                     <staging>/osx-arm64/infoslides
#   [output-dir]    Where to write the bundle and server.json (default: current directory)
#
# Requires: zip, sha256sum (or shasum on macOS).

set -euo pipefail

VERSION="${1:?Usage: build-mcpb.sh <version> <staging-dir> [output-dir]}"
STAGING="${2:?Usage: build-mcpb.sh <version> <staging-dir> [output-dir]}"
OUTPUT="${3:-$PWD}"

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# The registry requires the bundle URL to contain the string "mcp"; the .mcpb extension satisfies
# that on its own, and "-mcp-" in the file name makes it obvious to a human reading the release.
BUNDLE_NAME="infoslides-mcp-v${VERSION}.mcpb"

BUILD_DIR="$(mktemp -d)"
trap 'rm -rf "$BUILD_DIR"' EXIT

echo "Assembling ${BUNDLE_NAME} (version ${VERSION})"

mkdir -p "$BUILD_DIR/server"

copy_binary() {
    local rid="$1" source_name="$2" target_name="$3"
    local source="$STAGING/$rid/$source_name"

    if [[ ! -f "$source" ]]; then
        echo "error: missing $rid binary at $source" >&2
        echo "       every platform must be present — a bundle with a missing binary installs" >&2
        echo "       cleanly and then fails at first use on that platform." >&2
        exit 1
    fi

    cp "$source" "$BUILD_DIR/server/$target_name"
    chmod +x "$BUILD_DIR/server/$target_name"
    echo "  + server/$target_name  ($(wc -c < "$source" | tr -d ' ') bytes)"
}

copy_binary win-x64   infoslides.exe infoslides-win-x64.exe
copy_binary linux-x64 infoslides     infoslides-linux-x64
copy_binary osx-arm64 infoslides     infoslides-osx-arm64

sed "s/__VERSION__/${VERSION}/g" "$REPO_ROOT/mcpb/manifest.json.template" \
    > "$BUILD_DIR/manifest.json"

# Fail loudly rather than shipping a manifest with an unsubstituted placeholder.
if grep -q "__VERSION__" "$BUILD_DIR/manifest.json"; then
    echo "error: manifest.json still contains __VERSION__ after substitution" >&2
    exit 1
fi

mkdir -p "$OUTPUT"
BUNDLE_PATH="$OUTPUT/$BUNDLE_NAME"
rm -f "$BUNDLE_PATH"

if command -v zip >/dev/null 2>&1; then
    # -X drops extra file attributes so the archive is reproducible across machines. zip also
    # preserves the executable bit, which the release bundle needs on macOS and Linux.
    (cd "$BUILD_DIR" && zip -q -r -X "$BUNDLE_PATH" manifest.json server)
elif command -v python3 >/dev/null 2>&1 || command -v python >/dev/null 2>&1; then
    PY="$(command -v python3 || command -v python)"
    echo "warning: 'zip' not found — falling back to python's zipfile module." >&2
    echo "         The fallback does NOT preserve the executable bit, so the bundle it" >&2
    echo "         produces is fine for local inspection but must not be released." >&2
    (cd "$BUILD_DIR" && "$PY" -m zipfile -c "$BUNDLE_PATH" manifest.json server)
else
    echo "error: need either 'zip' or python to create the bundle." >&2
    exit 1
fi

if command -v sha256sum >/dev/null 2>&1; then
    SHA256="$(sha256sum "$BUNDLE_PATH" | cut -d' ' -f1)"
else
    SHA256="$(shasum -a 256 "$BUNDLE_PATH" | cut -d' ' -f1)"
fi

sed -e "s/__VERSION__/${VERSION}/g" -e "s/__SHA256__/${SHA256}/g" \
    "$REPO_ROOT/server.json.template" > "$OUTPUT/server.json"

if grep -q "__VERSION__\|__SHA256__" "$OUTPUT/server.json"; then
    echo "error: server.json still contains a placeholder after substitution" >&2
    exit 1
fi

echo
echo "Bundle:  $BUNDLE_PATH  ($(wc -c < "$BUNDLE_PATH" | tr -d ' ') bytes)"
echo "SHA-256: $SHA256"
echo "Wrote:   $OUTPUT/server.json"
