# Unity-SerializeReferenceExtensions — AGENTS.md

## Project overview
This repository provides editor tooling for Unity's [SerializeReference], including `SubclassSelector`
to pick concrete implementations of interfaces / abstract types in the Inspector.

Primary risk area: type discovery & filtering (what types appear in the selector).

## Repository layout
- Repository URL: `https://github.com/mackysoft/Unity-SerializeReferenceExtensions`
- Package source: `Assets/MackySoft/MackySoft.SerializeReferenceExtensions`
- Editor code: `.../Editor/**`
- Tests: `.../Tests/**`

## Unity compatibility (critical)
- Minimum supported Unity: 2021.3 (baseline for development/testing).
- Unity 2023.2+ has enhanced generic type support (variance, etc.). Changes must not break 2021.3 behavior and guarded by `UNITY_2023_2_OR_NEWER`.

## CI (GitHub Actions)
Use Unity CLI through `.github/actions/setup-unity`.
- Run EditMode tests when Unity source, assets, project settings, or the test setup changes. Documentation-only changes can skip Unity tests.
- Run tests on Linux.
- Always report the final `Verification` check on pull requests. It passes only when change detection succeeds and all required EditMode matrix jobs succeed, or Unity tests are not required and are skipped.
- Build the example player for Linux 64-bit and export the package only in the Release workflow.
- Release accepts a version tag, updates `package.json` through the MackySoft Release GitHub App, and builds that exact commit. Configure `RELEASE_BOT_APP_ID`, `RELEASE_BOT_PRIVATE_KEY`, and the App's bypass for the pull request rule before releasing.
- Run a Unity matrix that includes:
  - 2021.3.x (minimum baseline)
  - 2023.2+ (generic/variance feature gate)

## How to run tests locally
### EditMode
Run tests through Unity CLI (which manages batch mode):
```
unity --non-interactive test . --editor-version 2021.3.45f2 \
  --mode EditMode --output Logs/TestResults/editmode.xml --timeout 900 \
  -- -logFile Logs/editmode.log
```
Repeat with `--editor-version 2023.2.22f1` for the generic/variance feature gate.
GUI interaction tests require a graphics device; do not pass `-nographics`. On headless Linux,
run the command under `xvfb-run` with software OpenGL, as configured in the Tests workflow.

## Architecture guardrails
- Runtime surface area should remain minimal (mainly attributes / data structures).
- Editor implementation (PropertyDrawer/UI/type search) must stay under `Editor/`.
- Avoid introducing UnityEditor references into Runtime assemblies.

## Coding conventions
- Prefer `UnityEditor.TypeCache` for type discovery. Avoid full AppDomain scans unless necessary.
- Keep allocations low in IMGUI paths (e.g., `OnGUI`).
- Keep public API stable; if changing type filtering behavior, add/adjust tests.
- As a general rule, you should follow the restrictions on SerializeReference in Unity's official documentation.
  - https://docs.unity3d.com/ScriptReference/SerializeReference.html
