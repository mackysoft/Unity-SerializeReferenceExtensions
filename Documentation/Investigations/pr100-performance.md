# PR 100: performance findings and disposition

The first search of a generic field and rebuilding an existing popup are different performance problems. The former spends most of its time enumerating assemblies and applying type policies. The latter repeats attribute lookup, sorting, path processing, and tree construction. PR 100 improves the menu rebuild in isolation, while its generic feature changes make the measured first search slower.

The decision is to **decline PR 100 as submitted**. Keep the constructed-generic policy established in [PR 96](https://github.com/mackysoft/Unity-SerializeReferenceExtensions/pull/96#issuecomment-3783230592), retain the service's public filtering contract, and preserve the proven menu optimization opportunities as a separately scoped proposal below. This investigation does not change the package implementation.

## Revisions, environment, and evidence

- Main: `7c81c920b1c16678748a090c69b877676bb31ab1` (includes merged #104, #106, and #99).
- [PR 100](https://github.com/mackysoft/Unity-SerializeReferenceExtensions/pull/100): `be39689cc83f9ce694a01356314c4485ed1023d5`; its base at inspection was `5fadcb6564f0c6e988641e2e4a4aba4b6b1caafe`. It had no comments or reviews when the investigation began.
- Measurement harness: `b605dd934460c0f78644b1fe49b5ff0544f65adc`, Unity CLI `1.0.0-beta.12`.
- Linux: Ubuntu 22.04, Unity **2021.3.45f2** and **2023.2.22f1**, separate GitHub runners. The CPU models are AMD EPYC 9V74 and EPYC 7763 respectively; these are model names, not the runner's allocated CPU count.
- GUI and additional batch captures: macOS 26.6.2, Apple M1 Max, the same Unity versions.
- [Linux execution and artifacts](https://github.com/mackysoft/Unity-SerializeReferenceExtensions/actions/runs/38716048268): **23/23** EditMode cases on 2021.3, **26/26** on 2023.2, and **Verification passed**. The counts include the opt-in diagnostic case plus the existing package tests. These are not tests of the original PR branch as a whole.

[Reproduction instructions and phase definitions](../../Tools/Performance/pr100/README.md) describe the source copies, counterfactuals, cache resets, calibration, and entry points. [The manifest](pr100-evidence/manifest.json) records dataset hashes and environments. Six compressed JSON files in [pr100-evidence](pr100-evidence) preserve raw time samples, candidate names, and API counters. Read one with `gzip -dc <file.json.gz>`.

The initial Linux inventories contained 22,604 types in 108 assemblies (2021.3) and 28,892 types in 141 assemblies (2023.2). Test discovery and diagnostics can load more assemblies later; the generic API probe observed 149 assembly scans. The generated diagnostic types are part of this finite project, including the supplementary broad-field case. These results do not establish latency in the PR author's project, and do not rule out its reported approximately 500 ms opening time there.

## Paths and behavioral premises

In current main, `SubclassSelectorDrawer.OnGUI` calls `GetTypePopup` only when the dropdown button is activated. The drawer caches a popup by `managedReferenceFieldTypename`; `TypeCandiateService` caches results by base type; the 2023.2 generic provider has a separate base-type cache. The default non-generic provider uses `UnityEditor.TypeCache.GetTypesDerivedFrom` and has no provider cache.

Each `AdvancedDropdown.Show` rebuilds the menu through `BuildRoot` and `AdvancedTypePopup.AddTo`. Main's `AddTo` re-sorts, resolves paths, retrieves attributes, nicifies names, and creates tree nodes on each build. Ordinary non-click repaint uses the drawer's existing type-label cache and does not trigger candidate search or popup rebuilding.

The experiment uses existing `Food`, `ITestBase`, `ConcreteBaseType`, `IContravarianceAction<INetworkActor>`, `ICovarianceAction<INetworkActor>`, and `BaseAction<IActor>` types/fields. The three generic declarations are measured only on 2023.2. Their candidates are existing non-generic concrete types. `object` is a supplementary broad-field stress case, using existing eligible types rather than adding generated generic candidates.

Candidate counts on Linux 2023.2 were 3, 4, 2, 6, 3, 1, and 1,551 respectively. Linux 2021.3 measured the first three and `object` (1,058 candidates). The isolated baseline matched production candidate sets; the policy-only and generation-disabled counterfactuals matched baseline. The full proposal also returned the same sets in these captures. Menu content/order comparisons passed for every measured variant on the baseline candidate arrays. These comparisons establish a fair performance premise for the selected inputs; constructed-generic save/load and type-resolution safety are outside their meaning.

## Initial generic search

The following table uses `IContravarianceAction<INetworkActor>` on Linux 2023.2. Times are median / p95 milliseconds for 15 fresh-provider/service queries. Allocations count main-thread `GC.Alloc` events in separate equivalent invocations, not bytes.

| Subject | Time, ms (median / p95) | Allocation events |
| --- | ---: | ---: |
| Baseline | 66.1484 / 69.5653 | 18,239 |
| PolicyOnly | 81.9326 / 85.4065 | 19,194 |
| ProposalNoGeneration | 85.0265 / 87.7641 | 19,136 |
| Proposal | 96.5266 / 100.0791 | 20,123 |

`PolicyOnly` changes only the intrinsic `IsGenericType` test to `ContainsGenericParameters`. `ProposalNoGeneration` removes only the PR's generic-generation block; it retains the relaxed policy, inference preparation, display helpers, and service-filter removal. It is not a performance-only backport. The proposal's extra inference work is visible even though it adds no selectable candidate for this field.

The baseline's single first observed service call was 131.0981 ms; repeated fresh queries had a 66.1484 ms median. A single first sample includes first-use/JIT effects and is not a reliable speedup comparison. The harness's definition of fresh is an empty provider/service result cache, after assembly inventory; native TypeCache and runtime reflection metadata are not forced cold.

### Concrete API costs

These are **instrumented diagnostic observations**, not additive components of the preceding uninstrumented timings. They identify the API and caller responsible for work; recorder and wrapper overhead changes the execution conditions.

| API | Caller | Calls in one baseline generic query | Instrumented ms | Allocation events |
| --- | --- | ---: | ---: | ---: |
| `Assembly.GetTypes` | `Unity_2023_2_OrNewer_TypeCandiateProvider.EnumerateAllTypesSafely` | 149 | 32.8629 | 298 |
| `Attribute.IsDefined(SerializableAttribute)` | `DefaultIntrinsicTypePolicy.IsAllowed` | 13,180 | 10.5683 | 4,908 |
| `Type.GetGenericArguments` | `Unity_2023_2_OrNewer_GenericVarianceTypeCompatibilityPolicy.IsCompatible` | 4,918 | 7.7771 | 4,918 |
| `Attribute.IsDefined(HideInTypeMenuAttribute)` | `DefaultIntrinsicTypePolicy.IsAllowed` | 2,454 | 2.6216 | 4,904 |
| `Type.ContainsGenericParameters` | generic compatibility policy | 2,452 | 2.2861 | 2,452 |
| `Type.GetInterfaces` | generic compatibility policy | 2,452 | 1.4677 | 738 |
| `Type.GetGenericTypeDefinition` | generic compatibility policy | 2,640 | 1.3307 | 0 |
| `Type.IsGenericType` | intrinsic policy | 16,982 | 1.1362 | 0 |
| `AppDomain.GetAssemblies` | generic provider | 1 | 0.0740 | 1 |

There is one assembly enumeration, followed by one `GetTypes` per assembly, in current main. The PR's description of merging two assembly scans does not describe an additional improvement over this main revision.

There are two kinds of avoidable repetition in the current generic path: different generic field types repeat the domain scan and intrinsic checks, and `IsCompatible` retrieves the same target field's generic definition and argument arrays for each allowed candidate. The APIs have measurable individual overhead, while their call count is determined by these repeated queries. Changing the type-policy guarantees is not required to investigate either source of repetition.

The feature-related intrinsic change is itself costly in this Unity Mono environment: the policy-only probe called `Type.ContainsGenericParameters` 16,982 times, taking 21.1737 instrumented ms and 955 allocation events, replacing a non-allocating `IsGenericType` check. The full proposal also made 1,704 additional serializability checks in its inference branch. Its provider made 222 `GetGenericArguments` calls, including field preparation and interface/base matching, inspected interfaces 220 times, and called `MakeGenericType` once (0.0159 instrumented ms, 3 allocation events). **`MakeGenericType` alone is not the principal added cost here.**

### Service filtering and cache hits

With the generic provider cache populated and a new service, baseline measured 0.0606 ms and 68 allocation events; the policy-only subject measured 0.0621 ms / 68 events, and the generation-disabled PR measured 0.0131 ms / 8 events. This isolates the small result-filtering stage from the 60–100 ms domain search. Only the six returned candidates are rechecked by the service; the full scan and thousands of policy checks remain in the provider.

A warm service query had no probed discovery/reflection calls. Baseline still recorded one allocation event and the simplified PR zero; this is a separate method-level allocation opportunity, not evidence that filtering runs on cache hits. The drawer's existing popup cache also means ordinary repaint does not call this service method at all.

## Popup rebuild, sorting, and attributes

These are 101 rebuilds of an existing popup, on Linux 2023.2. Each time cell is median / p95 ms. The four single-change subjects isolate menu mechanisms; the proposal combines them and the generic display helpers. All candidate arrays here contain eligible non-generic types.

| Subject | Food: time, ms | Food: allocation events | Broad object: time, ms | Broad object: allocation events |
| --- | ---: | ---: | ---: | ---: |
| Baseline | 0.0665 / 0.1221 | 148 | 27.5223 / 32.5443 | 53,815 |
| AttributeOnly | 0.0090 / 0.0103 | 52 | 11.2888 / 16.1964 | 16,527 |
| PathOnly | 0.0355 / 0.0386 | 88 | 21.1488 / 25.4019 | 36,722 |
| NicifyOnly | 0.0656 / 0.0781 | 145 | 27.4589 / 32.7044 | 52,264 |
| PresortOnly | 0.0363 / 0.0496 | 85 | 13.1461 / 15.3252 | 27,436 |
| Proposal | 0.0021 / 0.0028 | 22 | 3.3383 / 6.5971 | 8,760 |

The larger menu demonstrates a meaningful rebuild cost. On Linux 2021.3 its 1,058-candidate rebuild was 14.6832 ms / 35,682 events for baseline and 1.7973 ms / 4,906 events for the combined proposal. Absolute figures differ across runners and Unity versions; they should not be used as a version-to-version speed comparison.

For `Food`, baseline called `Attribute.GetCustomAttribute` **12 times per rebuild** for three candidates: sort keys and path resolution repeatedly request the same metadata. It caused 96 of the rebuild's allocation events. The combined PR requested the attribute three times during initial popup construction/build and zero times on a rebuild. It similarly nicified names three times initially and zero times on rebuild. Baseline nicified them three times on every rebuild.

For the 1,551-candidate menu, baseline made 4,661 attribute lookups per rebuild (17.6442 instrumented ms, 37,288 allocation events) and 1,551 nicify calls (3.6498 instrumented ms, 1,551 events). Caching nicify alone had little uninstrumented wall-time effect in these captures; repeated reflection and sorting were larger opportunities. Cached paths and metadata also avoid array/string processing. Rebuilding still creates dropdown items and searches namespace siblings, so the optimized tree build is not free.

Presorting moves work to popup construction. It does not remove initial sorting cost. The data therefore includes `first-create-tree` (constructor plus build) separately from `first-tree` (build only), as well as standalone sort samples. First-use observations are single samples and are not the basis for the rebuild speedups above.

## Actual OnGUI and Show

The production main drawer ran in a real EditorWindow, using the existing example fields in a disposable preview scene. These are per-call median times / allocation events for 201 calls after Layout, not complete Inspector frame timings.

| Field / path | macOS 2021.3 | macOS 2023.2 |
| --- | ---: | ---: |
| `foodOne` / collapsed | 0.0192 ms / 2 | 0.0304 ms / 4 |
| `foodOne` / expanded, Apple custom drawer | 0.0301 ms / 6 | 0.0441 ms / 9 |
| `foodTwo` / expanded, Peach custom drawer | 0.0658 ms / 14 | 0.0861 ms / 25 |
| `foodThree` / collapsed, ToString label | 0.0222 ms / 4 | 0.0315 ms / 6 |
| `foodThree` / expanded, normal child properties | 0.0866 ms / 28 | 0.1028 ms / 41 |

`managedReferenceFieldTypename` itself measured 0.0018–0.0019 ms and 7 events on 2023.2; `managedReferenceFullTypename` measured 0.0004 ms and 1 event. The former is read on popup access, while the latter participates in ordinary label rendering. They are allocation-bearing APIs, but no measurement here justifies changing their serialization/type-resolution contract.

A single synchronous main popup `Show` call for Food took 9.4109 ms on 2021.3 and 14.4017 ms on 2023.2. These include Unity dropdown/window setup, exclude initial candidate discovery and time to painted presentation, and have no allocation measurement (`-1` in the data). They are not a comparison of main and PR visual opening latency. The small tree-rebuild saving alone cannot explain or promise removal of approximately 500 ms of end-to-end latency.

## Proposal decisions

| Proposal | Disposition | Reason |
| --- | --- | --- |
| Constructed generic generation and generic-only names | Decline | The #96 serialization, restoration, type-resolution, and Editor-stability policy remains in force. The measured selected fields gain no candidate, and the feature adds search work. |
| `IsGenericType` to `ContainsGenericParameters` | Decline | It changes eligibility on the baseline path too and adds measurable query work. It is not an independent performance improvement. |
| Remove service policies / replace its three-argument constructor | Decline | The constructor and injected policies are public. Arbitrary providers do not have a documented guarantee that they enforce those service policies. The default provider only applies intrinsic filtering. The small result-filtering saving does not justify removing the service's guarantees. |
| Static attribute cache | Decline as submitted; retain independent optimization candidate | Repeated reflection is a measured cost, especially for broad menus. It can be removed within a popup's metadata lifetime without coupling the change to generic discovery or service contracts. |
| Static path-array cache | Decline as submitted; retain recomputation-reduction candidate | It improves rebuilds, but the public `GetSplittedTypePath` would now return shared mutable arrays. A caller's mutation could alter later menus. Private immutable popup metadata can capture the benefit while preserving the public boundary. |
| Global string nicify cache | Decline | Its independent wall-time effect was small here. Reusing a popup's already computed label addresses repetition without a separate global string cache. |
| Move sort to `SetTypes`, remove sort from public `AddTo` | Decline as submitted; retain instance-level reuse candidate | Rebuild benefit is measured, but the public static method would stop sorting arbitrary input. Preserve that observable behavior while reusing the instance's prepared order. |
| Merge two assembly scans | No change to adopt | Main already enumerates the domain once per uncached generic field type. |

These decisions do not treat a faster implementation as acceptable when it changes eligibility, public input handling, or menu ordering. The performance-only findings remain usable even though the combined PR is declined.

## Separately scoped improvement proposal

For broad menus, the evidence supports an independent change to **prepare ordered popup display metadata once per `SetTypes` snapshot** and rebuild only dropdown nodes on subsequent opens. Obtain each type's attribute, path, and nicified label once during preparation. Keep the metadata private to the popup; keep the public `AddTo` input-sorting behavior and public path-return behavior. Keep all discovery providers, policies, supported types, selection actions, and Unity version guards unchanged.

The observed target is the 27.5223 ms / 53,815-event broad-menu rebuild, and the combined proposal demonstrates 3.3383 ms / 8,760 events under the measured conditions. Those combined figures are a feasibility reference, not a promised result for a differently implemented patch. The independent change must be measured again and verify menu ordering, namespace/submenu flattening, AddTypeMenu name/order/hidden entries, replacement through `SetTypes`, direct `AddTo` use, and public-array isolation. This investigation records that scope and evidence; it does not ship a cache implementation.

For first generic search, a different investigation should test preparing the target generic metadata once per query and reducing repeated domain/intrinsic enumeration across distinct generic fields. Any TypeCache replacement or cache lifetime design must first demonstrate candidate-set equivalence, including supported variance and non-generic wrappers, and correct discovery after script/domain reload. The popup results alone are not evidence for that provider design.

## Measurement limits

- Allocation figures are calibrated event counts, not bytes. They cannot establish allocation-volume or GC-pause reductions. API time is measured in separate instrumented copies and must not be summed into ordinary elapsed time.
- Fresh caches mean the provider/service or popup caches described above. Assembly inventory, test discovery, static initialization, runtime reflection metadata, and JIT are not all cold. Methods are measured in a fixed sequence; runner noise and outliers are retained in the raw samples.
- The diagnostic copies add types to the finite project. The large `object` input is a supplementary stress case, not evidence that the author's project uses that field. Candidate-count and project-size differences matter.
- The API probe uses large recorder buffers during one test frame; recording has native memory overhead and is not an end-user allocation measurement. Uninstrumented time samples exclude API wrappers, and the GUI capture is separate.
- No production eligibility, public API, UI implementation, or serialization behavior was changed. No claim of constructed-generic save/load support follows from the measurements or passing existing tests.
