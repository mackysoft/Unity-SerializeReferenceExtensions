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
I use GameCI `unity-test-runner`.
- Always run EditMode tests.
- Run a Unity matrix that includes:
  - 2021.3.x (minimum baseline)
  - 2023.2+ (generic/variance feature gate)

## How to run tests locally
### EditMode
Run Unity in batchmode:
- `Unity -batchmode -nographics -quit -projectPath . -runTests -testPlatform editmode -testResults ./TestResults/editmode.xml`

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