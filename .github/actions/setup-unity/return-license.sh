#!/usr/bin/env bash
set -euo pipefail

editor_root="$(unity --non-interactive editors path "$UNITY_EDITOR_VERSION")"
licensing_client="$editor_root/Editor/Data/Resources/Licensing/Client/Unity.Licensing.Client"

umask 077
return_directory="$(mktemp -d "$RUNNER_TEMP/unity-license-return-XXXXXX")"
return_log="$return_directory/return.log"
trap 'rm -rf "$return_directory"' EXIT

result=0
timeout 120 "$licensing_client" --return-ulf \
  --username "$UNITY_EMAIL" --password "$UNITY_PASSWORD" \
  > "$return_log" 2>&1 || result=$?

# Entitlement licenses have no ULF file; the Editor returns those with the
# account credentials. A scratch project keeps cleanup out of the real project.
if [[ "$result" != 0 ]] && grep -qi 'Ulf license file not found' "$return_log"; then
  result=0
  timeout 120 "$editor_root/Editor/Unity" \
    -batchmode -nographics -quit -returnlicense \
    -username "$UNITY_EMAIL" -password "$UNITY_PASSWORD" \
    -projectPath "$return_directory/project" -logFile /dev/stdout \
    > "$return_log" 2>&1 || result=$?
fi

# The Editor may return the entitlement and then exit nonzero while also
# attempting a ULF return. Its explicit success message confirms the return.
if grep -qE 'Successfully returned ULF license|Successfully returned the entitlement license|License has been returned' "$return_log"; then
  result=0
fi

if [[ "$result" != 0 ]]; then
  echo "::error::Unity license return failed (exit $result)."
  exit 1
fi
echo "Unity license returned."
