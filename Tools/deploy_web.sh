#!/bin/bash
# Publish the web build to GitHub Pages.
#
#   1. Unity: Tools ▸ LQ Farm ▸ Build Web (GitHub Pages)   → Builds/WebGL/MATUFarm/
#   2. Tools/deploy_web.sh
#   3. (once) GitHub ▸ repo Settings ▸ Pages ▸ Source: Deploy from a branch ▸ gh-pages ▸ / (root)
#
# The gh-pages branch holds ONLY the build, as a single commit that is force-pushed every time:
# a 40 MB build per deploy in history would bloat every clone of the repo. main is never touched.
#
# Site: https://<owner>.github.io/<repo>/

set -euo pipefail
cd "$(dirname "$0")/.."

BUILD="Builds/WebGL/MATUFarm"
[ -f "$BUILD/index.html" ] || { echo "Chưa có bản build web ở $BUILD — build trong Unity trước."; exit 1; }

REMOTE=$(git remote get-url origin)
AUTHOR_NAME=$(git log -1 --format='%an')
AUTHOR_EMAIL=$(git log -1 --format='%ae')
STAMP=$(date '+%Y-%m-%d %H:%M')

TMP=$(mktemp -d -t matu-pages)
trap 'rm -rf "$TMP"' EXIT
cp -R "$BUILD/." "$TMP/"
touch "$TMP/.nojekyll"

cd "$TMP"
git init -q
git checkout -q -b gh-pages
git add -A
git -c user.name="$AUTHOR_NAME" -c user.email="$AUTHOR_EMAIL" commit -q -m "Deploy MATU Farm web build ($STAMP)

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
git remote add origin "$REMOTE"
git push -f origin gh-pages

echo "Đã đẩy lên gh-pages ($STAMP)."
