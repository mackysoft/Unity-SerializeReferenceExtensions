# Specification tests

The EditMode suite exercises the package through its public editor APIs, registered
property drawers and registered context-menu commands. Tests live in
`Assets/Tests/SerializeReferenceExtensions/Editor`, outside both the UPM package and
the `Assets/MackySoft` unitypackage export tree.

## Contract sources

- [README](../README.md): supported features, type eligibility, generic version
  gates, collection elements and clipboard semantics.
- [Runtime attributes](../Assets/MackySoft/MackySoft.SerializeReferenceExtensions/Runtime):
  `SubclassSelector`, `AddTypeMenu` and `HideInTypeMenu`, including the documented
  menu-path helpers.
- [Accepted UseToStringAsLabel fix (#99)](https://github.com/mackysoft/Unity-SerializeReferenceExtensions/pull/99):
  the instance string is the visible field label, including collection elements.
  The override must affect the label actually drawn by Unity.
- [Release 1.3.0](https://github.com/mackysoft/Unity-SerializeReferenceExtensions/releases/tag/1.3.0):
  context-menu commands for initializing and resetting managed-reference instances.
- Unity [2021.3 SerializeReference](https://docs.unity3d.com/2021.3/Documentation/ScriptReference/SerializeReference.html)
  and [2023.2 SerializeReference](https://docs.unity3d.com/2023.2/Documentation/ScriptReference/SerializeReference.html):
  compatible serializable reference objects, null, nested references and collection
  elements. Unity owns the underlying serialization; tests exercise package edits
  before observing Undo or saved assets.
- [C# variance](https://learn.microsoft.com/en-us/dotnet/standard/generics/covariance-and-contravariance):
  reference conversions for `in` and `out`; value-type arguments remain invariant.

The implementation baseline for this work is merged commit `9587592`. Tests for
these established features require no product implementation changes. Ongoing
TriInspector/xNode compatibility work and proposed UI Toolkit changes are outside
this baseline.

## Detection responsibilities

| User-visible contract | Existing tests retained | Additional responsibility |
| --- | --- | --- |
| Eligible public and nested implementations; hidden, abstract, nonserializable, Unity object and generic candidates excluded | `TypeCandidateService_NonGenericTests`, `DefaultIntrinsicTypePolicyTests` | Open generic candidates, unrelated implementations and abstract-base inheritance |
| Concrete field type itself can be selected | `TypeCandidateService_BaseTypeSelfTests` | Retain existing coverage |
| Generic field compatibility on Unity 2023.2+ | `TypeCandidateService_GenericVarianceTests` | Abstract generic bases, inherited interfaces and combined input/output variance |
| `AddTypeMenu` paths, names and ordering; null item; selection events | `AdvancedTypePopupTests` | Public attribute path helpers and displayed Inspector labels |
| Selecting, replacing or clearing one managed reference | No value-transition tests | Real dropdown input, concrete type, matching serialized values, null, nested fields, array/List elements, unaffected siblings, Undo/Redo and asset reload |
| `UseToStringAsLabel` | No regression test for #99 | Actual drawn text, fresh instance values, null fallback and collection labels |
| Custom subtype property drawers | No tests | Registered drawer output, height and editing through `EditorGUI.PropertyField` |
| Copy / Paste Values Only / Paste Type and Values | `ManagedReferenceContextualPropertyMenuTests` | Retain #107 coverage, including independent snapshots, availability, pending edits, Undo/Redo and asset reload |
| New Instance / Reset and New Instance | No tests | Registered menu availability, independent replacement, reset to initial values, Undo/Redo and asset reload |

Expected values come from those sources and literal fixture inputs. Tests compare
observable types, fields, menu availability and drawn text. They do not assert
private package members, cache contents, reflection scans or callback order.
Unity menu/GUI adapters bridge observation APIs that Unity does not expose publicly;
they invoke the package's normal registrations rather than private package methods.

## Boundaries and remaining work

- Fuzzy search is supplied by Unity's `AdvancedDropdown`. Menu contents and
  selection are tested; search ranking is not a package guarantee.
- Generic candidate construction, whole-parent/whole-collection clipboard commands,
  third-party inspectors and a UI Toolkit implementation are outside the adopted
  feature contract.
- Boxed structs are described as version-dependent in the README. Existing discovery
  tests are retained; they do not establish a serialization guarantee for structs.
- Clipboard survival across an actual script reload, prefab override persistence,
  missing-type recovery and type-renaming migration need dedicated session fixtures.
- Mixed-value labels and multi-object dropdown edits are not exercised by the
  single-object Inspector fixture. Clipboard multi-selection availability has
  existing coverage.
- Value-type variance has a confirmed product defect in the baseline. Both cases
  in `GenericValueTypeVarianceReproductionTests` fail their candidate-exclusion
  assertions on Unity 2023.2.22f1. They are `Explicit` reproductions, skipped in
  the continuous suite, and do not establish passing coverage for that contract.

`New Instance` tests guarantee same-type independent replacement, without fixing
its value-copying details. The reset command is checked against literal initial
fixture values, and against a different edited state through Undo/Redo and reload.

## Running

Use the versions and commands in [AGENTS.md](../AGENTS.md). Inspector tests require
a graphics-capable Editor. The Linux CI matrix uses a virtual display and software
OpenGL for Unity 2021.3.45f2 and 2023.2.22f1, and its final `Verification` job requires
both applicable EditMode jobs to succeed.

To reproduce the known covariance defect explicitly:

```sh
unity --non-interactive test . --editor-version 2023.2.22f1 --mode EditMode \
  --filter MackySoft.SerializeReferenceExtensions.Tests.GenericValueTypeVarianceReproductionTests.Covariance_DoesNotBoxValueTypeArguments \
  --output Logs/TestResults/value-variance.xml --timeout 900 \
  -- -logFile Logs/value-variance.log
```

Use `Contravariance_DoesNotBoxValueTypeArguments` as the method name for the other
case. An assertion failure is the current expected reproduction result; after a
product fix, remove `Explicit` and include these exclusions in ordinary CI.
