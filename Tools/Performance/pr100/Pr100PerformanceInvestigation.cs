using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using MackySoft.SerializeReferenceExtensions.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using Unity.Profiling;
using Debug = UnityEngine.Debug;

namespace MackySoft.SerializeReferenceExtensions.Performance
{
    public sealed class Pr100PerformanceInvestigation
    {
        private const string SubjectNamespace = "MackySoft.SerializeReferenceExtensions.Performance.";
        private static object sink;
        private static Report report;

        public interface IPopup
        {
            AdvancedDropdownItem Build();
            void Show(Rect rect);
        }

        [Serializable]
        public sealed class Measurement
        {
            public string operation;
            public int iterations;
            public double medianMs;
            public double p95Ms;
            public long medianAllocationEvents;
            public double[] milliseconds;
            public long[] allocationEvents;
        }

        [Serializable]
        public sealed class ProbeRun
        {
            public string operation;
            public int candidates;
            public ApiProbe.Entry[] apis;
        }

        [Serializable]
        public sealed class CandidateSet
        {
            public string subject;
            public string field;
            public string[] types;
        }

        [Serializable]
        public sealed class Report
        {
            public string main = "7c81c920b1c16678748a090c69b877676bb31ab1";
            public string pr = "be39689cc83f9ce694a01356314c4485ed1023d5";
            public string unity;
            public string os;
            public string cpu;
            public string utc;
            public int assemblies;
            public int loadedTypes;
            public List<Measurement> measurements = new List<Measurement>();
            public List<ProbeRun> probes = new List<ProbeRun>();
            public List<CandidateSet> candidates = new List<CandidateSet>();
            public List<string> observations = new List<string>();
        }

        private sealed class Search
        {
            public Func<Type, IReadOnlyList<Type>> get;
            public Func<Type, IEnumerable<Type>> provide;
        }

        [Test, Order(-1000)]
        public void RecordPerformance()
        {
            RecordBatch();
        }

        public static void RecordBatch()
        {
            report = new Report { unity = Application.unityVersion, os = SystemInfo.operatingSystem,
                cpu = SystemInfo.processorType, utc = DateTime.UtcNow.ToString("O") };
            // Read type inventory outside timings; Unity test discovery has already loaded assemblies.
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            report.assemblies = assemblies.Length;
            report.loadedTypes = assemblies.Sum(a => SafeTypes(a).Length);
            // Unity's Mono can return zero for GetAllocatedBytesForCurrentThread.
            // Calibrate the supported GC.Alloc event recorder instead of reporting false zero bytes.
            using (var calibration = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GC.Alloc", 1,
                ProfilerRecorderOptions.SumAllSamplesInFrame | ProfilerRecorderOptions.CollectOnlyOnCurrentThread))
            {
                sink = new byte[4096];
                calibration.Stop();
                Assert.That(calibration.Count, Is.EqualTo(1), "allocation recorder unavailable");
                Assert.That(calibration.GetSample(0).Count, Is.GreaterThan(0), "allocation recorder missed calibration allocation");
                report.observations.Add("Allocation calibration byte[4096]: " + calibration.GetSample(0).Count + " GC.Alloc events. Allocation figures count events, not bytes.");
            }
            Measure("harness/empty", 101, () => { });

            var fields = new List<Type> { FindType("Food"), FindType("MackySoft.SerializeReferenceExtensions.Tests.ITestBase"),
                FindType("MackySoft.SerializeReferenceExtensions.Tests.ConcreteBaseType") };
#if UNITY_2023_2_OR_NEWER
            fields.Add(FindType("IContravarianceAction`1").MakeGenericType(FindType("INetworkActor")));
            fields.Add(FindType("ICovarianceAction`1").MakeGenericType(FindType("INetworkActor")));
            fields.Add(FindType("BaseAction`1").MakeGenericType(FindType("IActor")));
#endif
            fields.Add(typeof(object)); // Supplementary broad-field stress case, no new candidate definitions.

            foreach (Type field in fields)
            {
                string fieldName = field.ToString();
                Type[] baseline = null;
                foreach (string subject in new[] { "Baseline", "PolicyOnly", "ProposalNoGeneration", "Proposal" })
                {
                    Search first = NewSearch(subject);
                    MeasurePrepared(subject + "/" + fieldName + "/first-service", 1,
                        () => { Search s = NewSearch(subject); return () => sink = s.get(field); });
                    var types = first.get(field).ToArray();
                    report.candidates.Add(new CandidateSet { subject = subject, field = fieldName,
                        types = types.Select(t => t.AssemblyQualifiedName).OrderBy(s => s).ToArray() });
                    if (subject == "Baseline")
                    {
                        baseline = types;
                        Assert.That(TypeSearchService.TypeCandiateService.GetDisplayableTypes(field), Is.EquivalentTo(types), "isolated baseline differs from production");
                    }
                    else if (subject == "PolicyOnly" || subject == "ProposalNoGeneration")
                    {
                        Assert.That(types, Is.EquivalentTo(baseline), subject + " changed the existing candidate set");
                    }
                    Measure(subject + "/" + fieldName + "/warm-service", 101, () => sink = first.get(field));
                    MeasurePrepared(subject + "/" + fieldName + "/fresh-provider-service", 15,
                        () => { Search s = NewSearch(subject); return () => sink = s.get(field); });
                    MeasurePrepared(subject + "/" + fieldName + "/warm-provider-new-service", 21,
                        () => { Search s = NewSearch(subject); s.provide(field).ToArray(); return () => sink = s.get(field); });
                }

                foreach (string subject in new[] { "Baseline", "AttributeOnly", "PathOnly", "NicifyOnly", "PresortOnly", "Proposal" })
                {
                    var factory = (Func<Type[], IPopup>)Delegate.CreateDelegate(typeof(Func<Type[], IPopup>), FindType(SubjectNamespace + subject + ".BenchPopup").GetMethod("Create"));
                    MeasurePrepared(subject + "/" + fieldName + "/first-create-tree", 1,
                        () => { ResetMenu(subject); return () => sink = factory(baseline).Build(); });
                    MeasurePrepared(subject + "/" + fieldName + "/first-tree", 1,
                        () => { ResetMenu(subject); IPopup p = factory(baseline); return () => sink = p.Build(); });
                    object popup = NewPopup(subject, baseline);
                    var build = (Func<AdvancedDropdownItem>)Delegate.CreateDelegate(typeof(Func<AdvancedDropdownItem>), popup, "Build");
                    string signature = TreeSignature(build());
                    if (subject == "Baseline")
                    {
                        report.observations.Add(fieldName + " baseline tree: " + signature);
                    }
                    else
                    {
                        string expected = TreeSignature(((Func<AdvancedDropdownItem>)Delegate.CreateDelegate(typeof(Func<AdvancedDropdownItem>), NewPopup("Baseline", baseline), "Build"))());
                        Assert.That(signature, Is.EqualTo(expected), subject + " changed existing popup contents/order");
                    }
                    Measure(subject + "/" + fieldName + "/repeat-tree", 101, () => sink = build());
                    var utility = FindType(SubjectNamespace + subject + ".TypeMenuUtility");
                    var sort = (Func<IEnumerable<Type>, IEnumerable<Type>>)Delegate.CreateDelegate(typeof(Func<IEnumerable<Type>, IEnumerable<Type>>), utility.GetMethod("OrderByType"));
                    Measure(subject + "/" + fieldName + "/sort", 101, () => sink = sort(baseline).ToArray());
                }

                foreach (string subject in new[] { "ProbeBaseline", "ProbePolicyOnly", "ProbeProposalNoGeneration", "ProbeProposal" })
                {
                    Search s = NewSearch(subject);
                    ApiProbe.Reset();
                    var types = s.get(field);
                    report.probes.Add(new ProbeRun { operation = subject + "/" + fieldName + "/fresh-search", candidates = types.Count, apis = ApiProbe.Read() });
                    ApiProbe.Reset();
                    s.get(field);
                    report.probes.Add(new ProbeRun { operation = subject + "/" + fieldName + "/warm-search", candidates = types.Count, apis = ApiProbe.Read() });
                    ResetMenu(subject);
                    ApiProbe.Reset();
                    object popup = NewPopup(subject, baseline);
                    var build = (Func<AdvancedDropdownItem>)Delegate.CreateDelegate(typeof(Func<AdvancedDropdownItem>), popup, "Build");
                    build();
                    report.probes.Add(new ProbeRun { operation = subject + "/" + fieldName + "/first-create-tree", candidates = baseline.Length, apis = ApiProbe.Read() });
                    ApiProbe.Reset();
                    build();
                    report.probes.Add(new ProbeRun { operation = subject + "/" + fieldName + "/repeat-tree", candidates = baseline.Length, apis = ApiProbe.Read() });
                }
            }
            Write("batch");
        }

        private static Search NewSearch(string subject)
        {
            string ns = SubjectNamespace + subject + ".";
            object intrinsic = FindType(ns + "DefaultIntrinsicTypePolicy").GetField("Instance").GetValue(null);
#if UNITY_2023_2_OR_NEWER
            object compatibility = FindType(ns + "Unity_2023_2_OrNewer_GenericVarianceTypeCompatibilityPolicy").GetField("Instance").GetValue(null);
            Type providerType = FindType(ns + "Unity_2023_2_OrNewer_TypeCandiateProvider");
            object provider = Activator.CreateInstance(providerType, BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { intrinsic, compatibility }, null);
#else
            object compatibility = FindType(ns + "DefaultTypeCompatibilityPolicy").GetField("Instance").GetValue(null);
            Type providerType = FindType(ns + "DefaultTypeCandiateProvider");
            object provider = Activator.CreateInstance(providerType, new[] { intrinsic });
#endif
            Type serviceType = FindType(ns + "TypeCandiateService");
            object service = Activator.CreateInstance(serviceType, subject.Contains("Proposal") ? new[] { provider } : new[] { provider, intrinsic, compatibility });
            return new Search {
                get = (Func<Type, IReadOnlyList<Type>>)Delegate.CreateDelegate(typeof(Func<Type, IReadOnlyList<Type>>), service, "GetDisplayableTypes"),
                provide = (Func<Type, IEnumerable<Type>>)Delegate.CreateDelegate(typeof(Func<Type, IEnumerable<Type>>), provider, "GetTypeCandidates")
            };
        }

        private static object NewPopup(string subject, Type[] types)
        {
            return Activator.CreateInstance(FindType(SubjectNamespace + subject + ".BenchPopup"), new object[] { types });
        }

        private static void ResetMenu(string subject)
        {
            foreach (var field in FindType(SubjectNamespace + subject + ".TypeMenuUtility").GetFields(BindingFlags.NonPublic | BindingFlags.Static))
            {
                if (field.GetValue(null) is IDictionary dictionary) { dictionary.Clear(); }
            }
        }

        private static void Measure(string operation, int count, Action action)
        {
            MeasurePrepared(operation, count, () => action);
        }

        private static void MeasurePrepared(string operation, int count, Func<Action> prepare)
        {
            var milliseconds = new double[count];
            var events = new long[count];
            using (var allocations = new ProfilerRecorder(ProfilerCategory.Internal, "GC.Alloc", 1,
                ProfilerRecorderOptions.SumAllSamplesInFrame | ProfilerRecorderOptions.CollectOnlyOnCurrentThread))
            {
            for (int i = 0; i < count; i++)
            {
                Action action = prepare();
                long start = Stopwatch.GetTimestamp();
                action();
                long end = Stopwatch.GetTimestamp();
                milliseconds[i] = (end - start) * 1000.0 / Stopwatch.Frequency;
                // Allocation capture is an independent invocation, outside the wall-time sample.
                Action allocationAction = prepare();
                allocations.Reset();
                allocations.Start();
                allocationAction();
                allocations.Stop();
                events[i] = allocations.Count == 0 ? 0 : allocations.GetSample(0).Count;
            }
            }
            double[] sorted = milliseconds.OrderBy(v => v).ToArray();
            long[] sortedEvents = events.OrderBy(v => v).ToArray();
            report.measurements.Add(new Measurement { operation = operation, iterations = count,
                medianMs = sorted[count / 2], p95Ms = sorted[(int)Math.Floor((count - 1) * 0.95)],
                medianAllocationEvents = sortedEvents[count / 2], milliseconds = milliseconds, allocationEvents = events });
        }

        private static string TreeSignature(AdvancedDropdownItem root)
        {
            return string.Join("|", root.children.Select(child => child.name + "{" + TreeSignature(child) + "}"));
        }

        private static Type[] SafeTypes(Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null).ToArray(); }
        }

        private static Type FindType(string name)
        {
            return AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
        }

        private static void Write(string kind)
        {
            string directory = Path.GetFullPath("Logs/Performance");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "pr100-" + Application.unityVersion + "-" + kind + ".json");
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            Debug.Log("PR100_PERFORMANCE " + path);
        }

        public static void RunInteractive()
        {
            report = new Report { unity = Application.unityVersion, os = SystemInfo.operatingSystem,
                cpu = SystemInfo.processorType, utc = DateTime.UtcNow.ToString("O") };
            var window = ScriptableObject.CreateInstance<GuiMeasurementWindow>();
            window.titleContent = new GUIContent("PR100 performance probe");
            window.position = new Rect(100, 100, 700, 450);
            window.Show();
            window.Focus();
        }

        public sealed class GuiMeasurementWindow : EditorWindow
        {
            private GameObject target;
            private SerializedObject serialized;
            private SerializedProperty property;
            private SubclassSelectorDrawer drawer;
            private int phase;
            private int frames;
            private readonly GUIContent label = new GUIContent("Food");

            private void OnEnable()
            {
                target = new GameObject("Temporary PR100 probe");
                var component = target.AddComponent(FindType("Example"));
                serialized = new SerializedObject(component);
                property = serialized.FindProperty("foodOne");
                property.isExpanded = false;
                drawer = new SubclassSelectorDrawer();
                typeof(PropertyDrawer).GetField("m_Attribute", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(drawer, new SubclassSelectorAttribute());
                typeof(PropertyDrawer).GetField("m_FieldInfo", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(drawer, component.GetType().GetField("foodOne"));
            }

            private void OnGUI()
            {
                if (property == null) { return; }
                var rect = new Rect(10, 15, 650, 350);
                if (Event.current.type != EventType.Repaint) { drawer.OnGUI(rect, property, label); return; }
                try
                {
                    if (phase == 0)
                    {
                        Measure("OnGUI/Food/collapsed-first-repaint-after-layout", 1, () => drawer.OnGUI(rect, property, label));
                        Measure("OnGUI/Food/collapsed-repeat-repaint", 201, () => drawer.OnGUI(rect, property, label));
                        property.isExpanded = true;
                        phase++;
                    }
                    else if (phase == 1)
                    {
                        Measure("OnGUI/Food/expanded-first-repaint-after-layout", 1, () => drawer.OnGUI(rect, property, label));
                        Measure("OnGUI/Food/expanded-repeat-repaint", 201, () => drawer.OnGUI(rect, property, label));
                        Measure("SerializedProperty/managedReferenceFieldTypename", 201, () => sink = property.managedReferenceFieldTypename);
                        Measure("SerializedProperty/managedReferenceFullTypename", 201, () => sink = property.managedReferenceFullTypename);
                        phase++;
                    }
                    else if (phase == 2)
                    {
                        var popup = new Baseline.BenchPopup(TypeSearchService.TypeCandiateService.GetDisplayableTypes(FindType("Food")));
                        Measure("AdvancedDropdown/Food/first-Show", 1, () => popup.Show(new Rect(10, 30, 600, 18)));
                        report.observations.Add("Real EditorWindow OnGUI and AdvancedDropdown.Show executed for existing Example.foodOne; timing is synchronous Show work only.");
                        phase++;
                    }
                    else if (++frames > 5)
                    {
                        Write("interactive");
                        EditorApplication.delayCall += () => { Close(); EditorApplication.Exit(0); };
                        phase = -100;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    report.observations.Add(e.ToString());
                    Write("interactive-error");
                    EditorApplication.delayCall += () => EditorApplication.Exit(1);
                }
                Repaint();
            }

            private void OnDestroy()
            {
                serialized?.Dispose();
                if (target != null) { DestroyImmediate(target); }
            }
        }
    }
}
