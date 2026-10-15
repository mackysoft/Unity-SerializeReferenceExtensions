# PR 100 performance investigation

This is an opt-in diagnostic experiment, outside the Unity package and normal test suite. It compares pinned source revisions without changing the production implementation. The generated copies deliberately preserve the PR's behavior so its tradeoffs can be measured; they are not proposed package code.

## Reproduce

Use a disposable worktree with a licensed Unity Editor. Fetch both subjects, then generate the isolated copies in the existing Editor test assembly:

```sh
git fetch --no-tags origin 7c81c920b1c16678748a090c69b877676bb31ab1 be39689cc83f9ce694a01356314c4485ed1023d5
python3 Tools/Performance/pr100/prepare.py
unity --non-interactive test . --editor-version 2021.3.45f2 \
  --mode EditMode --output Logs/TestResults/performance.xml --timeout 900 \
  -- -nographics -logFile Logs/performance.log
```

Repeat in a separate disposable worktree with `--editor-version 2023.2.22f1`. Using separate worktrees keeps Unity's project upgrades and Library caches separate. `Logs/Performance/pr100-<version>-batch.json` contains raw samples, candidates, API counts, and instrumented API timings. `prepare.py` requires the pinned Git objects and fails if they are unavailable; it never fetches or switches branches itself.

For real GUI measurements, launch the same generated project with Unity CLI:

```sh
unity --non-interactive projects open . --editor-version 2023.2.22f1 --wait \
  --args '-executeMethod MackySoft.SerializeReferenceExtensions.Performance.Pr100PerformanceInvestigation.RunInteractive -logFile /absolute/path/to/gui.log'
```

Dismiss any Unity package notification. The temporary EditorWindow exercises `Example.foodOne`, `foodTwo`, and `foodThree`, writes the interactive JSON, closes its preview scene, and exits its Editor. Use this entry point only in the disposable worktree. Its results cover actual `OnGUI` calls and synchronous `AdvancedDropdown.Show`, not time until the popup has painted.

Generated files live only in `Assets/MackySoft/MackySoft.SerializeReferenceExtensions/Tests/Editor/Pr100Generated`. The experiment does not modify the package implementation. Keep generated files, project upgrades, and logs out of production changes.

## Subjects and measurement meaning

| Subject | Meaning |
| --- | --- |
| `Baseline` | Main `7c81c92`, renamed namespace only. Candidate sets are compared with production main. |
| `PolicyOnly` | Main with PR 100's intrinsic `ContainsGenericParameters` change only. |
| `ProposalNoGeneration` | PR `be39689` with only the candidate-generation block removed. It retains the policy change, inference preparation, display helpers, and service-filter removal. It is not a performance-only backport. |
| `Proposal` | PR `be39689` candidate-search and menu source, renamed namespace only. GUI measurements use the production main drawer; the PR's drawer changes (generic formatting and inline-label nicify caching) are outside this comparison. |
| `AttributeOnly`, `PathOnly`, `NicifyOnly`, `PresortOnly` | Main with one menu proposal at a time. The presort subject preserves the PR's changed public `AddTo` behavior for diagnosis. |
| `Probe*` | Separate copies with API wrappers; never used for the ordinary elapsed-time samples. |

`first-service` and `fresh-provider-service` use a newly constructed provider and service. The singleton generic provider is not reused. `warm-provider-new-service` fills the generic provider cache before measuring a new service query; the default non-generic provider has no cache, so that path still enumerates TypeCache candidates. `warm-service` queries the cached result.

`first-create-tree` includes popup construction and the first tree build. Menu cache entries and dictionary capacity are reset before each invocation. Static field initialization, JIT, Unity's native TypeCache, and runtime reflection caches are outside this definition of cold. `first-tree` excludes popup construction, so the PR's presorting has already happened. `repeat-tree` rebuilds the same popup's tree. Sorting is measured separately too.

The harness takes 15 fresh-provider samples, 21 warm-provider/new-service samples, and 101 warm-service, tree-rebuild, and sort samples. The first-query/tree observations are single samples and should not support statistical speedup claims. Real GUI repaint batches use 201 calls after Layout, for a stable input/property state.

Wall-time and allocation captures use separate invocations with the corresponding starting state. Allocation data counts main-thread `GC.Alloc` events through `ProfilerRecorder`, following [Unity's recorder example](https://docs.unity3d.com/2021.3/Documentation/ScriptReference/Unity.Profiling.ProfilerRecorderOptions.CollectOnlyOnCurrentThread.html); a `byte[4096]` allocation must produce an event, and an empty action must produce none. These figures are **allocation counts**, not allocated bytes or GC pause durations. GUI `Show` is invoked once and reports `-1` for allocations because a second invocation would have different window state.

API probes report call counts, local instrumented durations, and allocation events for individual API calls. Do not add their times to the uninstrumented wall time or subtract one from the other: wrappers, recording, and GC conditions differ. Nested runtime work is included in the API call that causes it. Nicify calls from the popup and utility are grouped under their shared label.

Candidate snapshots and popup content/order comparisons protect the comparison's behavioral premise. They do not establish constructed-generic serialization safety. The existing package tests remain the behavioral checks; the benchmark has no timing pass/fail threshold and is not an ongoing performance gate.
