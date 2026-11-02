#!/usr/bin/env bash
# Builds the production images on this workstation. Never run it on the production host (too little RAM).
# Tag = short git sha of HEAD. The "latest" tag is deliberately not produced.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TAG="$(git -C "$ROOT" rev-parse --short HEAD)"

if [[ -n "$(git -C "$ROOT" status --porcelain)" ]]; then
  echo "warning: the working tree has uncommitted changes: the images do not match the tag exactly." >&2
fi

docker build -t "signalchat-api:$TAG" -f "$ROOT/backend/Dockerfile" "$ROOT/backend"

if [[ -f "$ROOT/frontend/Dockerfile" ]]; then
  docker build -t "signalchat-web:$TAG" -f "$ROOT/frontend/Dockerfile" "$ROOT/frontend"
else
  echo "warning: frontend/Dockerfile does not exist yet: the web image was not built." >&2
fi

echo "API_IMAGE=signalchat-api:$TAG"
echo "WEB_IMAGE=signalchat-web:$TAG"
