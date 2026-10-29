#!/usr/bin/env bash
# Saves the images built by build-images.sh into dist-images/ as gzip tarballs, each with a SHA-256 file.
# Copy the tarballs and their .sha256 files to the server, check them with `sha256sum -c`, then `docker load`.
# Usage: scripts/export-images.sh [tag]   (default: short git sha of HEAD)
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TAG="${1:-$(git -C "$ROOT" rev-parse --short HEAD)}"
OUT_DIR="$ROOT/dist-images"
mkdir -p "$OUT_DIR"

if command -v sha256sum >/dev/null 2>&1; then
  SHA256="sha256sum"
else
  SHA256="shasum -a 256"
fi

for NAME in signalchat-api signalchat-web; do
  IMAGE="$NAME:$TAG"
  GZ="$OUT_DIR/$NAME-$TAG.tar.gz"
  docker save "$IMAGE" | gzip -9 > "$GZ"
  (cd "$OUT_DIR" && $SHA256 "$(basename "$GZ")" > "$(basename "$GZ").sha256")
  echo "$GZ"
done
