using System;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace MackySoft.SerializeReferenceExtensions.Validation
{
    /// <summary>Side-by-side native Inspectors for manual layout and operation comparisons.</summary>
    public sealed class InspectorComparisonWindow : EditorWindow
    {
        private enum Comparison
        {
            UnityStandardAndSubclassSelector,
            IMGUIAndUIToolkit
        }

        private enum Backend
        {
            IMGUI,
            UIToolkit
        }

        [SerializeField] private InspectionTarget[] sources = Array.Empty<InspectionTarget>();
        [SerializeField] private Comparison comparison;
        [SerializeField] private Backend backend = Backend.UIToolkit;

        private StandardInspectionData[] standardCopies = Array.Empty<StandardInspectionData>();
        private UnityEditor.Editor leftEditor;
        private UnityEditor.Editor rightEditor;

        [MenuItem("Tools/SerializeReference Extensions/Validation/Open Comparison")]
        public static void Open ()
        {
            var window = GetWindow<InspectorComparisonWindow>(
                "SerializeReference comparison", true, typeof(SceneView));
            window.UseSelection();
            window.Show();
        }

        public void CreateGUI () => Rebuild();

        private void OnDisable ()
        {
            ReleaseViews();
            ReleaseCopies();
        }

        private void UseSelection ()
        {
            InspectionTarget[] selected = Selection.objects
                .Select(value => value is GameObject gameObject
                    ? gameObject.GetComponent<InspectionTarget>() : value as InspectionTarget)
                .Where(value => value != null).Distinct().ToArray();
            if (selected.Length == 0)
            {
                Rebuild();
                return;
            }
            if (selected.Any(value => value.GetType() != selected[0].GetType()))
            {
                Debug.LogWarning("Select validation components of the same type for multi-object inspection.");
                return;
            }
            sources = selected;
            RefreshStandard();
        }

        private void RefreshStandard ()
        {
            ReleaseViews();
            ReleaseCopies();
            sources = sources.Where(value => value != null).ToArray();
            standardCopies = new StandardInspectionData[sources.Length];
            for (int i = 0; i < sources.Length; i++)
            {
                StandardInspectionData copy = CreateInstance<StandardInspectionData>();
                standardCopies[i] = copy;
                copy.hideFlags = HideFlags.DontSave;
                copy.name = sources[i].name + " (standard comparison copy)";
                EditorUtility.CopySerializedManagedFieldsOnly(sources[i], copy);
                CopyExpansion(sources[i], copy);
            }
            Rebuild();
        }

        private static void CopyExpansion (Object source, Object destination)
        {
            using (var from = new SerializedObject(source))
            using (var to = new SerializedObject(destination))
            using (SerializedProperty property = from.GetIterator())
            {
                while (property.NextVisible(true))
                {
                    using (SerializedProperty other = to.FindProperty(property.propertyPath))
                    {
                        if (other != null)
                        {
                            other.isExpanded = property.isExpanded;
                        }
                    }
                }
            }
        }

        private void Rebuild ()
        {
            ReleaseViews();
            AddToolbar();
            if (sources.Length == 0 || sources.Any(value => value == null))
            {
                rootVisualElement.Add(new HelpBox(
                    "Open the validation scene, select its objects, then choose Use Selection.",
                    HelpBoxMessageType.Info));
                return;
            }
            if (standardCopies.Length != sources.Length || standardCopies.Any(value => value == null))
            {
                RefreshStandard();
                return;
            }

            var columns = new VisualElement { name = "comparison-columns" };
            columns.style.flexDirection = FlexDirection.Row;
            columns.style.flexGrow = 1;
            rootVisualElement.Add(columns);

            bool compareBackends = comparison == Comparison.IMGUIAndUIToolkit;
            Backend leftBackend = compareBackends ? Backend.IMGUI : backend;
            Backend rightBackend = compareBackends ? Backend.UIToolkit : backend;
            Object[] leftTargets = compareBackends ? (Object[])sources : standardCopies;
            leftEditor = CreateEditor(leftTargets, leftBackend);
            rightEditor = CreateEditor(sources, rightBackend);

            AddInspector(columns, compareBackends ? "SubclassSelector / IMGUI" : "Unity standard / " + backend,
                compareBackends ? "Scene / Prefab objects" : "Temporary copy; root SubclassSelector attributes removed",
                leftEditor, "left-inspector");
            AddInspector(columns, "SubclassSelector / " + rightBackend,
                "Scene / Prefab objects (edits are saved with the scene)", rightEditor, "right-inspector");
        }

        private void AddToolbar ()
        {
            var actions = new Toolbar();
            actions.Add(new ToolbarButton(UseSelection) { text = "Use Selection" });
            actions.Add(new ToolbarButton(() =>
            {
                ValidationScene.Open();
                UseSelection();
            }) { text = "Open Scene" });
            actions.Add(new ToolbarButton(RefreshStandard) { text = "Refresh Standard Copy" });
            actions.Add(new Label(sources.Length == 1 && sources[0] != null
                ? sources[0].name : sources.Length + " objects"));
            rootVisualElement.Add(actions);

            var modes = new VisualElement();
            modes.style.flexDirection = FlexDirection.Row;
            var compare = new EnumField("Compare", comparison);
            compare.RegisterValueChangedCallback(change =>
            {
                comparison = (Comparison)change.newValue;
                rootVisualElement.schedule.Execute(Rebuild);
            });
            modes.Add(compare);
            var drawing = new EnumField("Backend", backend);
            drawing.SetEnabled(comparison == Comparison.UnityStandardAndSubclassSelector);
            drawing.RegisterValueChangedCallback(change =>
            {
                backend = (Backend)change.newValue;
                rootVisualElement.schedule.Execute(Rebuild);
            });
            modes.Add(drawing);
            rootVisualElement.Add(modes);
        }

        private static UnityEditor.Editor CreateEditor (Object[] targets, Backend drawing)
        {
            Type editorType = drawing == Backend.IMGUI
                ? typeof(IMGUIInspectionTargetEditor) : typeof(UIToolkitInspectionTargetEditor);
            return UnityEditor.Editor.CreateEditor(targets, editorType);
        }

        private static void AddInspector (VisualElement columns, string caption, string description,
            UnityEditor.Editor editor, string name)
        {
            var column = new VisualElement();
            column.style.flexGrow = 1;
            column.style.flexBasis = 0;
            columns.Add(column);
            column.Add(new Label(caption));
            column.Add(new Label(description));
            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;
            column.Add(scroll);
            var inspector = new InspectorElement(editor) { name = name };
            scroll.Add(inspector);

            // Scope the whole IMGUI Inspector to its column's real viewport. Unity
            // derives label width from GUIClip; nested property fallbacks own their scope.
            IMGUIContainer imgui = inspector.hierarchy.Children().OfType<IMGUIContainer>().FirstOrDefault();
            if (imgui != null)
            {
                Action drawInspector = imgui.onGUIHandler;
                imgui.onGUIHandler = () =>
                {
                    GUI.BeginGroup(imgui.contentRect);
                    try
                    {
                        drawInspector();
                    }
                    finally
                    {
                        GUI.EndGroup();
                    }
                };
            }
        }

        private void ReleaseViews ()
        {
            // Unbind and detach before releasing the Editors referenced by native views.
            rootVisualElement.Unbind();
            rootVisualElement.Clear();
            if (leftEditor != null)
            {
                DestroyImmediate(leftEditor);
            }
            if (rightEditor != null)
            {
                DestroyImmediate(rightEditor);
            }
            leftEditor = null;
            rightEditor = null;
        }

        private void ReleaseCopies ()
        {
            foreach (StandardInspectionData copy in standardCopies)
            {
                if (copy != null)
                {
                    DestroyImmediate(copy);
                }
            }
            standardCopies = Array.Empty<StandardInspectionData>();
        }
    }
}
