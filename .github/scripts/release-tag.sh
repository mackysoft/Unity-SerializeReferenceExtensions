#!/usr/bin/env bash
set -euo pipefail

operation="${1:?Expected check or create}"
case "$operation" in
  check|create) ;;
  *) echo "::error::Expected check or create."; exit 1 ;;
esac

git check-ref-format "refs/tags/$VERSION"
remote_tags="$(git ls-remote --tags origin "refs/tags/$VERSION" "refs/tags/$VERSION^{}")"
tag_commit=""
while read -r object ref; do
  case "$ref" in
    "refs/tags/$VERSION") tag_commit="$object" ;;
    # Annotated tags identify a tag object; compare the peeled commit instead.
    "refs/tags/$VERSION^{}") tag_commit="$object"; break ;;
  esac
done <<< "$remote_tags"

if [[ -n "$tag_commit" && "$tag_commit" != "$RELEASE_SHA" ]]; then
  echo "::error::Tag $VERSION belongs to a different commit. Update package.json version through a pull request before releasing."
  exit 1
fi

if [[ "$operation" == create && -z "$tag_commit" ]]; then
  git tag -- "$VERSION" "$RELEASE_SHA"
  # A non-forced tag push rejects a concurrent release for a different commit.
  git push --no-follow-tags --recurse-submodules=no origin "refs/tags/$VERSION"
fi
