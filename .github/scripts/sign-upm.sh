#!/usr/bin/env bash
set -euo pipefail

package_dir="${1:?Expected package directory}"
destination="${2:?Expected output directory}"
: "${VERSION:?Expected release version}"
: "${UPM_ORG_ID:?Expected signing organization ID}"

package_name="$(jq -er '.name' "$package_dir/package.json")"
package_version="$(jq -er '.version' "$package_dir/package.json")"
if [[ "$package_version" != "$VERSION" ]]; then
  echo "::error::UPM package version does not match the release version."
  exit 1
fi

upm pack "$package_dir" --organization-id "$UPM_ORG_ID" --destination "$destination"

shopt -s nullglob
archives=("$destination"/*.tgz "$destination"/*.tar.gz)
if [[ "${#archives[@]}" != 1 ]]; then
  echo "::error::Expected exactly one signed UPM archive."
  exit 1
fi
archive="${archives[0]}"

if ! tar -xOzf "$archive" package/package.json | jq --exit-status \
  --arg name "$package_name" --arg version "$VERSION" \
  '.name == $name and .version == $version' > /dev/null; then
  echo "::error::Signed UPM archive has an unexpected package name or version."
  exit 1
fi

signature_size="$(tar -xOzf "$archive" package/.attestation.p7m | wc -c)"
if [[ "$signature_size" -eq 0 ]]; then
  echo "::error::UPM archive has no signing attestation."
  exit 1
fi

# Rename only; repacking would change the archive produced by Unity's signer.
release_archive="$destination/$package_name-$VERSION.tgz"
if [[ "$archive" != "$release_archive" ]]; then
  mv -- "$archive" "$release_archive"
fi
printf 'Signed UPM package: %s\n' "$release_archive"
