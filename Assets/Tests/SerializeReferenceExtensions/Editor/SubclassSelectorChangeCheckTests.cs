using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.TestTools;

namespace MackySoft.SerializeReferenceExtensions.Tests
{
    public sealed class SubclassSelectorChangeCheckTests
    {
        private ChangeCheckTarget target;
        private ChangeCheckWindow window;
        private EditorWindow popup;

        [UnitySetUp]
        public IEnumerator SetUp ()
        {
            target = ScriptableObject.CreateInstance<ChangeCheckTarget>();
            window = ScriptableObject.CreateInstance<ChangeCheckWindow>();
            window.Initialize(target);
            window.ShowUtility();
            yield return null;
            window.DrawNow();
            window.ClearChanges();
        }

        [TearDown]
        public void TearDown ()
        {
            if (popup != null)
            {
                popup.Close();
            }
            if (window != null)
            {
                window.Close();
            }
            if (target != null)
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [UnityTest]
        public IEnumerator SelectingType_NotifiesTheOwningPropertyChangeCheck ()
        {
            OpenPopup(1);
            SelectItem("Beta");
            yield return null;
            window.DrawNow();

            Assert.That(window.GetValue(1), Is.TypeOf<Beta>());
            Assert.That(window.Changed[1], Is.True, "The caller must observe the type selection through EndChangeCheck.");
            Assert.That(window.GetValue(0), Is.TypeOf<Alpha>());
            Assert.That(window.Changed[0], Is.False, "Drawing another element must not consume or receive the selected element's change.");
        }

        [UnityTest]
        public IEnumerator SelectingNull_NotifiesTheOwningPropertyChangeCheck ()
        {
            OpenPopup(1);
            SelectItem("<null>");
            yield return null;
            window.DrawNow();

            Assert.That(window.GetValue(1), Is.Null);
            Assert.That(window.Changed[1], Is.True, "Clearing a reference must notify the caller through EndChangeCheck.");
            Assert.That(window.GetValue(0), Is.TypeOf<Alpha>());
            Assert.That(window.Changed[0], Is.False);
        }

        [UnityTest]
        public IEnumerator CancellingThePopup_DoesNotNotifyPropertyChangeChecks ()
        {
            OpenPopup(1);
            popup.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape });
            yield return null;
            window.DrawNow();

            Assert.That(window.GetValue(0), Is.TypeOf<Alpha>());
            Assert.That(window.GetValue(1), Is.TypeOf<Alpha>());
            Assert.That(window.Changed, Is.All.False, "Opening and cancelling the selector does not edit either property.");
        }

        private void OpenPopup (int index)
        {
            window.Focus();
            window.DrawNow();
            Rect field = window.FieldRects[index];
            window.SendEvent(new Event
            {
                type = EventType.MouseDown,
                mousePosition = new Vector2(field.xMax - 30f, field.y + EditorGUIUtility.singleLineHeight * 0.5f),
                button = 0
            });
            popup = Resources.FindObjectsOfTypeAll<EditorWindow>()
                .SingleOrDefault(candidate => candidate.GetType().Name == "AdvancedDropdownWindow");
            Assert.That(popup, Is.Not.Null, "Clicking the property selector must open its type popup.");
        }

        private void SelectItem (string name)
        {
            // Unity exposes popup input through SendEvent, but its visible rows and keyboard selection are internal.
            // Read those solely to navigate the real control; do not invoke the drawer's selection callback.
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var tree = (AdvancedDropdownItem)popup.GetType().GetField("m_CurrentlyRenderedTree", flags).GetValue(popup);
            object state = popup.GetType().GetField("m_State", flags).GetValue(popup);
            MethodInfo getSelectedIndex = state.GetType().GetMethod("GetSelectedIndex", flags);
            AdvancedDropdownItem[] rows = tree.children.ToArray();
            int targetIndex = Array.FindIndex(rows, row => row.name == name);
            Assert.That(targetIndex, Is.GreaterThanOrEqualTo(0), "Popup item missing: " + name);

            for (int step = 0; step <= rows.Length; step++)
            {
                if ((int)getSelectedIndex.Invoke(state, new object[] { tree }) == targetIndex)
                {
                    popup.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Return });
                    return;
                }
                popup.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.DownArrow });
            }
            Assert.Fail("Keyboard navigation did not reach popup item: " + name);
        }

        public interface IValue { }

        [Serializable, AddTypeMenu("Alpha")]
        public sealed class Alpha : IValue { }

        [Serializable, AddTypeMenu("Beta")]
        public sealed class Beta : IValue { }

        public sealed class ChangeCheckTarget : ScriptableObject
        {
            [SerializeReference, SubclassSelector]
            private IValue[] values = { new Alpha(), new Alpha() };
        }

        public sealed class ChangeCheckWindow : EditorWindow
        {
            private SerializedObject serializedTarget;

            public bool[] Changed { get; } = new bool[2];
            public Rect[] FieldRects { get; } = new Rect[2];

            public void Initialize (ChangeCheckTarget value)
            {
                serializedTarget = new SerializedObject(value);
                position = new Rect(100f, 100f, 500f, 160f);
            }

            public void DrawNow ()
            {
                SendEvent(new Event { type = EventType.Layout });
                SendEvent(new Event { type = EventType.Repaint });
            }

            public void ClearChanges ()
            {
                Array.Clear(Changed, 0, Changed.Length);
            }

            public object GetValue (int index)
            {
                serializedTarget.Update();
                return serializedTarget.FindProperty("values").GetArrayElementAtIndex(index).managedReferenceValue;
            }

            private void OnGUI ()
            {
                if (serializedTarget == null)
                {
                    return;
                }

                serializedTarget.Update();
                SerializedProperty values = serializedTarget.FindProperty("values");
                float y = 12f;
                for (int index = 0; index < values.arraySize; index++)
                {
                    SerializedProperty property = values.GetArrayElementAtIndex(index);
                    var label = new GUIContent("Value " + index);
                    var rect = new Rect(12f, y, position.width - 24f, EditorGUI.GetPropertyHeight(property, label, true));
                    FieldRects[index] = rect;

                    // Each caller scope must see only edits made through its own property control.
                    EditorGUI.BeginChangeCheck();
                    EditorGUI.PropertyField(rect, property, label, true);
                    Changed[index] |= EditorGUI.EndChangeCheck();
                    y = rect.yMax + 8f;
                }
                serializedTarget.ApplyModifiedProperties();
            }

            private void OnDisable ()
            {
                if (serializedTarget != null)
                {
                    serializedTarget.Dispose();
                    serializedTarget = null;
                }
            }
        }
    }
}
