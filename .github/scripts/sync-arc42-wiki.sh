#!/usr/bin/env bash
# Mirrors Docs/arc42 into a GitHub Wiki checkout.
# Usage: sync-arc42-wiki.sh <source-dir> <wiki-checkout-dir>
set -euo pipefail

src="${1:?source dir required}"
wiki="${2:?wiki checkout dir required}"

[ -d "$src" ] || { echo "source dir '$src' not found" >&2; exit 1; }
[ -d "$wiki/.git" ] || { echo "'$wiki' is not a git checkout" >&2; exit 1; }

# Full mirror: remove pages that no longer exist in the source.
find "$wiki" -maxdepth 1 -name '*.md' -type f -delete

for file in "$src"/*.md; do
  name="$(basename "$file")"
  [ "$name" = "README.md" ] && continue
  # Strip the .md extension from links between arc42 pages so they resolve
  # as wiki page links. Absolute URLs and links to other folders are untouched.
  sed -E 's/\]\(([0-9]{2}-[A-Za-z0-9-]+|Home)\.md(#[^)]*)?\)/](\1\2)/g' "$file" > "$wiki/$name"
done

echo "Synced $(ls "$wiki"/*.md | wc -l) pages into $wiki"
