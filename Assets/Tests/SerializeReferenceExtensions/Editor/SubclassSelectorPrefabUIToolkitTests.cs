using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Component = MackySoft.SerializeReferenceExtensions.Tests.SubclassSelectorPrefabTestComponent;

namespace MackySoft.SerializeReferenceExtensions.Tests
{
    public sealed class SubclassSelectorPrefabUIToolkitTests
    {
        private TestWindow window;
        private GameObject instance;
        private GameObject source;
        private SerializedObject boundObject;
        private string prefabPath;
        private UnityEditor.Editor inspectorEditor;
        private string nativeControlPath;

        [TearDown]
        public void TearDown ()
        {
            if (window != null)
            {
                window.rootVisualElement.Unbind();
                window.rootVisualElement.Clear();
                window.Close();
            }
            boundObject?.Dispose();
            if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
            if (source != null) UnityEngine.Object.DestroyImmediate(source);
            if (prefabPath != null) AssetDatabase.DeleteAsset(prefabPath);
            if (inspectorEditor != null) UnityEngine.Object.DestroyImmediate(inspectorEditor);
        }

        [UnityTest]
        public IEnumerator PrefabMenu_ForwardsToTheCurrentNativeReferenceBeforeAndAfterApplyAndRevert ()
        {
            source = new GameObject("Subclass selector menu Prefab");
            source.AddComponent<Component>();
            prefabPath = AssetDatabase.GenerateUniqueAssetPath("Assets/SubclassSelectorMenuUIToolkitTest.prefab");
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(source, prefabPath);
            UnityEngine.Object.DestroyImmediate(source);
            source = null;
            instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Component component = instance.GetComponent<Component>();
            using (var edit = new SerializedObject(component))
            {
                edit.FindProperty("reference").managedReferenceValue = new Component.SecondValue { number = 91 };
                edit.ApplyModifiedProperties();
            }
            boundObject = new SerializedObject(component);
            boundObject.FindProperty("reference").isExpanded = true;
            var host = new PropertyField(boundObject.FindProperty("reference"));
            inspectorEditor = UnityEditor.Editor.CreateEditor(component, typeof(SubclassSelectorOnGUIComparisonEditor));
            ((SubclassSelectorOnGUIComparisonEditor)inspectorEditor).Paths = Array.Empty<string>();
            var inspector = new InspectorElement(inspectorEditor);
            inspector.Add(host);
            window = ScriptableObject.CreateInstance<TestWindow>();
            window.position = new Rect(100, 100, 520, 500);
            window.Show();
            window.rootVisualElement.Add(inspector);
            host.Bind(boundObject);

            var requests = new List<(string path, UnityEngine.Object target, long id)>();
            void Observe (SerializedProperty property) => requests.Add((property.propertyPath,
                property.serializedObject.targetObject, property.propertyType == SerializedPropertyType.ManagedReference ? property.managedReferenceId : -1));
            using (SerializedPropertyContextMenu.CaptureNativeRequests(window.rootVisualElement, Observe))
            {
                for (int stage = 0; stage < 4; stage++)
                {
                    if (stage == 1)
                    {
                        using (var edit = new SerializedObject(component))
                        {
                            PrefabUtility.ApplyPropertyOverride(edit.FindProperty("reference"), prefabPath, InteractionMode.AutomatedAction);
                        }
                    }
                    else if (stage == 2)
                    {
                        using (var edit = new SerializedObject(component))
                        {
                            edit.FindProperty("reference").managedReferenceValue = new Component.FirstValue { number = 97 };
                            edit.ApplyModifiedProperties();
                        }
                    }
                    else if (stage == 3)
                    {
                        using (var edit = new SerializedObject(component))
                        {
                            PrefabUtility.RevertPropertyOverride(edit.FindProperty("reference"), InteractionMode.AutomatedAction);
                        }
                    }
                    // A directly bound scalar does not own its parent's reference lifetime.
                    // Build the control from the current property after each native operation.
                    boundObject.UpdateIfRequiredOrScript();
                    var nativeControl = new PropertyField(boundObject.FindProperty("reference.number"), "Native menu control");
                    inspector.Add(nativeControl);
                    nativeControl.Bind(boundObject);
                    var current = Read(component);
                    var savedPrefab = Read(prefab.GetComponent<Component>());
                    VisualElement HeaderInput () => host.Query<PopupField<string>>().ToList().FirstOrDefault()
                        ?.Q(className: PopupField<string>.inputUssClassName);
                    yield return WaitFor(() => Input(host)?.value == current.number && nativeControl.Q<IntegerField>()?.value == current.number
                        && nativeControl.Q<IntegerField>().labelElement.worldBound.width > 0f && HeaderInput()?.worldBound.width > 0f,
                        "The current Prefab data and menu targets must finish layout before a request. Stage=" + stage);
                    foreach (bool control in new[] { true, false })
                    {
                        requests.Clear();
                        window.Focus();
                        if (control)
                        {
                            SerializedPropertyContextMenu.RequestFromUi(nativeControl.Q<IntegerField>().labelElement);
                        }
                        else
                        {
                            SerializedPropertyContextMenu.RequestFromUi(HeaderInput());
                        }
                        yield return WaitFor(() => requests.Count > 0, "The native bound Prefab property did not receive the menu request. Control=" + control + "; stage=" + stage);
                        Assert.That(requests.Count, Is.EqualTo(1), "One request must reach one native bound property.");
                        Assert.That(requests[0].path, Is.EqualTo(control ? "reference.number" : "reference"));
                        Assert.That(requests[0].target, Is.SameAs(component));
                        if (!control)
                        {
                            Assert.That(requests[0].id, Is.EqualTo(current.id), "The menu must use the current reference identity.");
                        }
                        Assert.That(Read(component), Is.EqualTo(current), "Opening a menu must not change the instance data.");
                        Assert.That(Read(prefab.GetComponent<Component>()), Is.EqualTo(savedPrefab), "Opening a menu must not change the Prefab asset.");
                        TestContext.Out.WriteLine("Prefab menu routing: stage=" + stage + "; control=" + control
                            + "; path=" + requests[0].path + "; rootId=" + current.id + "; rootOverride=" + current.isOverride);
                        yield return null;
                    }
                    nativeControl.Unbind();
                    nativeControl.RemoveFromHierarchy();
                }
            }
        }

        [UnityTest]
        public IEnumerator PrefabRevert_UpdatesHeaderWeightAndKeepsChildEditing ()
        {
            source = new GameObject("Subclass selector Prefab");
            source.AddComponent<Component>();
            prefabPath = AssetDatabase.GenerateUniqueAssetPath("Assets/SubclassSelectorUIToolkitTest.prefab");
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(source, prefabPath);
            UnityEngine.Object.DestroyImmediate(source);
            source = null;
            instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Component component = instance.GetComponent<Component>();

            WriteNumber(component, 91);
            boundObject = new SerializedObject(component);
            boundObject.FindProperty("reference").isExpanded = true;
            var host = new PropertyField(boundObject.FindProperty("reference"), "Reference");
            window = ScriptableObject.CreateInstance<TestWindow>();
            window.position = new Rect(100, 100, 520, 500);
            window.Show();
            window.rootVisualElement.Add(CreateInspector(component, host, "reference.number"));
            host.Bind(boundObject);
            yield return WaitFor(() => Input(host)?.value == 91 && HeaderHasWeight(true),
                "The overridden reference was not displayed with its native bold header.",
                () => Describe(host, component, "reference"));

            using (var edit = new SerializedObject(component))
            {
                edit.FindProperty("reference").managedReferenceValue = new Component.SecondValue { number = 91 };
                edit.ApplyModifiedProperties();
            }
            yield return WaitFor(() => window.rootVisualElement.Query<PopupField<string>>().ToList().Any(field => field.text == "Second type")
                && Input(host)?.value == 91,
                "The replacement reference was not displayed.");

            for (int pass = 0; pass < 2 && Read(component).isOverride; pass++)
            {
                using (var edit = new SerializedObject(component))
                {
                    PrefabUtility.RevertPropertyOverride(edit.FindProperty("reference"), InteractionMode.AutomatedAction);
                }
                var after = Read(component);
                yield return WaitFor(() => Input(host)?.value == after.number,
                    "The displayed child did not follow Prefab Revert.");
            }

            var reverted = Read(component);
            Assert.That(reverted.isOverride, Is.False, "The test precondition did not reach the reverted Prefab state.");
            Assert.That(reverted.number, Is.EqualTo(Read(prefab.GetComponent<Component>()).number));
            yield return WaitFor(() => HeaderHasWeight(false),
                "The header stayed bold after the reference's Prefab override was removed.");

            window.Focus();
            Input(host).Focus();
            yield return WaitFor(() => HasInputFocus(host), "The reverted child input could not receive focus.");
            Input(host).value = 17;
            yield return WaitFor(() => Read(component).number == 17 && HeaderHasWeight(true) && HasInputFocus(host),
                "A new override must update the header and keep the child input bound and focused.");

            host.Unbind();
            host.Bind(boundObject);
            yield return WaitFor(() => Input(host)?.value == 17, "The host's full rebind did not restore the child input.");
            Input(host).value = 19;
            yield return WaitFor(() => Read(component).number == 19, "The child stopped editing after the host's full rebind.");
            Assert.That(Read(prefab.GetComponent<Component>()).number, Is.EqualTo(7), "Editing the instance changed its Prefab asset.");
        }

        [UnityTest]
        public IEnumerator PrefabApply_UpdatesHeaderWeightAndKeepsCustomChildEditable ()
        {
            source = new GameObject("Tracked subclass selector Prefab");
            Component sourceComponent = source.AddComponent<Component>();
            var original = new Component.CompositeValue();
            sourceComponent.reference = original;
            Assert.That(SetReferenceId(sourceComponent, original, 1000), Is.True);
            Assert.That(SetReferenceId(sourceComponent, original.child, 2000), Is.True);
            prefabPath = AssetDatabase.GenerateUniqueAssetPath("Assets/SubclassSelectorTrackedUIToolkitTest.prefab");
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(source, prefabPath);
            UnityEngine.Object.DestroyImmediate(source);
            source = null;
            instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Component component = instance.GetComponent<Component>();
            var replacement = new Component.CompositeValue();
            component.reference = replacement;
            Assert.That(SetReferenceId(component, replacement, 5000), Is.True);
            Assert.That(SetReferenceId(component, replacement.child, 6000), Is.True);
            EditorUtility.SetDirty(component);
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);

            boundObject = new SerializedObject(component);
            boundObject.FindProperty("reference").isExpanded = true;
            var host = new PropertyField(boundObject.FindProperty("reference"), "Reference");
            window = ScriptableObject.CreateInstance<TestWindow>();
            window.position = new Rect(100, 100, 520, 500);
            window.Show();
            window.rootVisualElement.Add(CreateInspector(component, host, "reference.child.number"));
            host.Bind(boundObject);
            yield return WaitFor(() => Input(host)?.value == 7 && Tracks(host, 7)
                && HeaderHasWeight(true),
                "The overridden reference must display its custom input, tracker and native bold header.",
                () => Describe(host, component, "reference.child"));
            const string valuePath = "reference.child";
            var initial = Read(component, valuePath);
            Assert.That(initial.isOverride, Is.True);

            window.Focus();
            Input(host).Focus();
            yield return WaitFor(() => HasInputFocus(host), "The custom Prefab input could not receive focus.");
            Input(host).SelectAll();
            TypeText("9");
            yield return WaitFor(() => Read(component, valuePath).number == 9 && Read(component, valuePath).isOverride
                && Tracks(host, 9) && HeaderHasWeight(true),
                "The first key must save the value and update the custom drawer's tracker.",
                () => Describe(host, component, valuePath));
            yield return null;
            var overridden = Read(component, valuePath);
            Assert.That(overridden.id, Is.EqualTo(initial.id), "The edit must keep the same reference identity.");
            Assert.That(overridden.typeName, Is.EqualTo(initial.typeName));
            Assert.That(overridden.valueId, Is.EqualTo(initial.valueId), "The edit must preserve the child reference identity.");
            Assert.That(HasInputFocus(host), Is.True,
                "Editing the overridden Prefab must preserve input focus. " + Describe(host, component, valuePath));

            using (var edit = new SerializedObject(component))
            {
                PrefabUtility.ApplyPropertyOverride(edit.FindProperty("reference"), prefabPath, InteractionMode.AutomatedAction);
            }
            // Some Editors replace reference IDs during Apply. Classify the API result before
            // the view's scheduler runs so it cannot determine its own acceptance condition.
            var nativeApplied = Read(component, valuePath);
            bool sameIdentity = nativeApplied.id == initial.id && nativeApplied.valueId == initial.valueId;
            TestContext.Out.WriteLine($"Apply preserved reference identity: {sameIdentity}; root {initial.id}->{nativeApplied.id}, child {initial.valueId}->{nativeApplied.valueId}");
            yield return WaitFor(() => Read(component, valuePath).number == 9 && !Read(component, valuePath).isOverride
                && Input(host)?.value == 9 && Tracks(host, 9) && HeaderHasWeight(false),
                "Apply must keep the current custom input and tracker and remove the native header weight.",
                () => Describe(host, component, valuePath));
            yield return null;
            var applied = Read(component, valuePath);
            Assert.That(applied.id, Is.EqualTo(nativeApplied.id), "The view must not change the root identity produced by Apply.");
            Assert.That(applied.typeName, Is.EqualTo(initial.typeName));
            Assert.That(applied.valueId, Is.EqualTo(nativeApplied.valueId), "The view must not change the child identity produced by Apply.");
            if (sameIdentity)
            {
                Assert.That(HasInputFocus(host), Is.True,
                    "A metadata-only Apply must preserve the custom input focus. " + Describe(host, component, valuePath));
                TypeText("9");
            }
            else
            {
                // A replaced reference may create a new input. Prepare it for a fresh edit.
                Input(host).Focus();
                yield return WaitFor(() => HasInputFocus(host), "The input for Apply's replacement reference must accept focus.");
                Input(host).SelectAll();
                TypeText("99");
            }

            yield return WaitFor(() => Read(component, valuePath).number == 99 && Tracks(host, 99),
                "The current custom input must save the edit after Apply and update its own tracker.");
            yield return null;
            Assert.That(HasInputFocus(host), Is.True, "Continued input must retain focus.");
            Assert.That(Read(prefab.GetComponent<Component>(), valuePath).number, Is.EqualTo(9));

            using (var edit = new SerializedObject(component))
            {
                PrefabUtility.RevertPropertyOverride(edit.FindProperty(valuePath + ".number"), InteractionMode.AutomatedAction);
            }
            yield return WaitFor(() => Read(component, valuePath).number == 9 && !Read(component, valuePath).isOverride
                && Input(host)?.value == 9 && Tracks(host, 9) && HeaderHasWeight(false),
                "Reverting the scalar override must restore the applied input and tracker without stale header weight.");
            var reverted = Read(component, valuePath);
            Assert.That(reverted.id, Is.EqualTo(applied.id));
            Assert.That(reverted.typeName, Is.EqualTo(applied.typeName));
            Assert.That(reverted.valueId, Is.EqualTo(applied.valueId));
            using (var savedInstance = new SerializedObject(component))
            using (var savedPrefab = new SerializedObject(prefab.GetComponent<Component>()))
            {
                Assert.That(savedInstance.FindProperty("reference.nativeValue").intValue, Is.EqualTo(101));
                Assert.That(savedPrefab.FindProperty("reference.nativeValue").intValue, Is.EqualTo(101));
            }
        }

        private IEnumerator WaitFor (Func<bool> condition, string failure, Func<string> diagnostic = null)
        {
            double deadline = EditorApplication.timeSinceStartup + 8d;
            while (!condition())
            {
                if (EditorApplication.timeSinceStartup >= deadline)
                {
                    Assert.Fail(failure + (diagnostic == null ? string.Empty : " " + diagnostic()));
                }
                window.Repaint();
                yield return null;
            }
        }

        private static IntegerField Input (VisualElement root) => root.Query<IntegerField>().ToList()
            .FirstOrDefault(field => field.label == "Number" && field.worldBound.height > 0f);

        private VisualElement CreateInspector (Component component, PropertyField host, string controlPath)
        {
            nativeControlPath = controlPath;
            inspectorEditor = UnityEditor.Editor.CreateEditor(component, typeof(SubclassSelectorOnGUIComparisonEditor));
            ((SubclassSelectorOnGUIComparisonEditor)inspectorEditor).Paths = Array.Empty<string>();
            var inspector = new InspectorElement(inspectorEditor);
            inspector.Add(host);
            inspector.Add(new IMGUIContainer(() =>
            {
                using (var read = new SerializedObject(component))
                {
                    EditorGUILayout.PropertyField(read.FindProperty(controlPath), new GUIContent("Native Number"));
                }
            }));
            return inspector;
        }

        private bool HeaderHasWeight (bool expected)
        {
            var drawn = InspectorDrawing.CaptureText(window);
            var header = window.rootVisualElement.Query<Label>().ToList().Where(label => label.text == "Reference"
                && label.ClassListContains(Toggle.textUssClassName) && label.worldBound.height > 0f).ToArray();
            var control = drawn.Where(text => text.Text == "Native Number").ToArray();
            using (var read = new SerializedObject(instance.GetComponent<Component>()))
            {
                bool controlOverride = read.FindProperty(nativeControlPath).prefabOverride;
                return header.Length > 0 && control.Length > 0 && header.All(label => (label.resolvedStyle.unityFontStyleAndWeight == FontStyle.Bold
                    || label.resolvedStyle.unityFontStyleAndWeight == FontStyle.BoldAndItalic) == expected)
                    && control.All(text => text.IsBold == controlOverride);
            }
        }

        private static bool HasInputFocus (VisualElement root)
        {
            IntegerField input = Input(root);
            VisualElement focused = root.panel?.focusController.focusedElement as VisualElement;
            return input != null && focused != null && (input == focused || input.Contains(focused));
        }

        private void TypeText (string text)
        {
            foreach (char character in text)
            {
                // Let the native keyboard dispatcher choose the current leaf focus.
                // EditorWindow's IMGUI event route can stop delivering after Prefab Apply.
                using (var pressed = KeyDownEvent.GetPooled(character, KeyCode.None, EventModifiers.None))
                {
                    window.rootVisualElement.SendEvent(pressed);
                }
                using (var released = KeyUpEvent.GetPooled(character, KeyCode.None, EventModifiers.None))
                {
                    window.rootVisualElement.SendEvent(released);
                }
            }
        }

        private static bool Tracks (VisualElement root, int value) => root.Query<Label>().ToList()
            .Any(label => label.text == "Tracked number: " + value && label.worldBound.height > 0f);

        private string Describe (VisualElement root, Component component, string valuePath)
        {
            var current = Read(component, valuePath);
            var focused = root.panel?.focusController.focusedElement as VisualElement;
            using (var read = new SerializedObject(component))
            {
                bool valueOverride = read.FindProperty(valuePath).prefabOverride;
                return $"saved={current.number}, rootOverride={current.isOverride}, valueOverride={valueOverride}, "
                    + $"input={Input(root)?.value}, tracker={Tracks(root, current.number)}, header/control={string.Join(",", InspectorDrawing.CaptureText(window).Where(text => text.Text == "Reference" || text.Text == "Native Number").Select(text => text.Text + ":" + text.IsBold))}, "
                    + $"focused={focused?.GetType().Name}:{focused?.name}, rootId={current.id}, childId={current.valueId}";
            }
        }

        private static (int number, bool isOverride, long id, string typeName, long valueId) Read (Component component, string valuePath = "reference")
        {
            using (var read = new SerializedObject(component))
            {
                SerializedProperty reference = read.FindProperty("reference");
                using (SerializedProperty value = read.FindProperty(valuePath))
                {
                    return (value.FindPropertyRelative("number").intValue, reference.prefabOverride,
                        reference.managedReferenceId, reference.managedReferenceFullTypename, value.managedReferenceId);
                }
            }
        }

        private static void WriteNumber (Component component, int value)
        {
            using (var edit = new SerializedObject(component))
            {
                edit.FindProperty("reference.number").intValue = value;
                edit.ApplyModifiedProperties();
            }
        }

#if UNITY_2022_3_OR_NEWER
        private static bool SetReferenceId (Component target, object value, long id)
            => UnityEngine.Serialization.ManagedReferenceUtility.SetManagedReferenceIdForObject(target, value, id);
#else
        private static bool SetReferenceId (Component target, object value, long id)
            => SerializationUtility.SetManagedReferenceIdForObject(target, value, id);
#endif

        public sealed class TestWindow : EditorWindow { }

        [CustomPropertyDrawer(typeof(Component.TrackedValue))]
        public sealed class TrackedValueDrawer : PropertyDrawer
        {
            public override VisualElement CreatePropertyGUI (SerializedProperty property)
            {
                var root = new VisualElement();
                root.Add(new IntegerField("Number") { bindingPath = "number" });
                using (SerializedProperty number = property.FindPropertyRelative("number"))
                {
                    var tracked = new Label("Tracked number: " + number.intValue);
                    tracked.TrackPropertyValue(number, current => tracked.text = "Tracked number: " + current.intValue);
                    root.Add(tracked);
                }
                return root;
            }
        }
    }
}
