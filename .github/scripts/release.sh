#!/usr/bin/env bash
set -euo pipefail

operation="${1:?Expected prepare or create}"
case "$operation" in
  prepare|create) ;;
  *) echo "::error::Expected prepare or create."; exit 1 ;;
esac

# Package versions follow SemVer, including prerelease and build identifiers.
number='(0|[1-9][0-9]*)'
prerelease='(0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*)'
if [[ ! "$VERSION" =~ ^${number}\.${number}\.${number}(-${prerelease}(\.${prerelease})*)?(\+[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?$ ]]; then
  echo "::error::Release version must follow SemVer (for example 1.7.1 or 1.8.0-preview.1)."
  exit 1
fi
git check-ref-format "refs/tags/$VERSION"

if [[ "$operation" == prepare ]]; then
  if [[ "$RELEASE_REF" != refs/heads/* ]]; then
    echo "::error::Dispatch Release from a branch."
    exit 1
  fi

  source_sha="$(git rev-parse HEAD)"
  current_version="$(jq -er '.version | select(type == "string" and length > 0)' "$TARGET_FILE")"
  if [[ "$current_version" != "$VERSION" ]]; then
    manifest="$(mktemp "$RUNNER_TEMP/release-package-XXXXXX")"
    trap 'rm -f "$manifest"' EXIT
    jq --arg version "$VERSION" '.version = $version' "$TARGET_FILE" > "$manifest"
    cat "$manifest" > "$TARGET_FILE"
    git add -- "$TARGET_FILE"
    git commit --only -m "chore(release): update package version to $VERSION" -- "$TARGET_FILE"
  fi
  RELEASE_SHA="$(git rev-parse HEAD)"
fi

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
  echo "::error::Tag $VERSION belongs to a different commit. Choose a new release version."
  exit 1
fi

if [[ "$operation" == prepare ]]; then
  if [[ "$current_version" != "$VERSION" ]]; then
    # Require a fast-forward from the checked-out commit and an unchanged remote
    # branch. The lease also rejects deletion or rollback during preparation.
    git merge-base --is-ancestor "$source_sha" "$RELEASE_SHA"
    git push --no-follow-tags --recurse-submodules=no \
      --force-with-lease="$RELEASE_REF:$source_sha" origin "$RELEASE_SHA:$RELEASE_REF"
  fi
  echo "version=$VERSION" >> "$GITHUB_OUTPUT"
  echo "sha=$RELEASE_SHA" >> "$GITHUB_OUTPUT"
elif [[ -z "$tag_commit" ]]; then
  git tag -- "$VERSION" "$RELEASE_SHA"
  # A non-forced tag push rejects a concurrent release for a different commit.
  git push --no-follow-tags --recurse-submodules=no origin "refs/tags/$VERSION"
fi
