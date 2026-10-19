using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MackySoft.SerializeReferenceExtensions.Tests
{
    internal static class SerializedPropertyContextMenu
    {
        // GenericMenu has no public command enumeration or execution API. Bridge only Unity's
        // menu storage; package registration, availability, and callbacks run unchanged.
        public static GenericMenu Open (SerializedProperty property)
        {
            var menu = new GenericMenu();
            Assert.That(EditorApplication.contextualPropertyMenu, Is.Not.Null);
            EditorApplication.contextualPropertyMenu(menu, property);
            return menu;
        }

        public static bool Contains (GenericMenu menu, string label)
        {
            return FindItem(menu, label) != null;
        }

        public static bool IsEnabled (GenericMenu menu, string label)
        {
            object item = FindItem(menu, label);
            Assert.That(item, Is.Not.Null, $"Missing context-menu command: {label}");
            return GetField<GenericMenu.MenuFunction>(item, "func") != null ||
                GetField<GenericMenu.MenuFunction2>(item, "func2") != null;
        }

        public static void Execute (GenericMenu menu, string label)
        {
            Assert.That(IsEnabled(menu, label), Is.True, $"Disabled context-menu command: {label}");
            object item = FindItem(menu, label);
            GenericMenu.MenuFunction2 callback = GetField<GenericMenu.MenuFunction2>(item, "func2");
            if (callback != null)
            {
                callback(GetField<object>(item, "userData"));
            }
            else
            {
                GetField<GenericMenu.MenuFunction>(item, "func")();
            }
        }

        public static IDisposable CaptureNativeRequests (VisualElement root, Action<SerializedProperty> observe)
        {
            return new NativeRequestCapture(root, observe);
        }

#if UNITY_2022_2_OR_NEWER
        public static void RequestFromUi (VisualElement input)
        {
            var systemEvent = new Event { type = EventType.MouseUp, button = 1, mousePosition = input.worldBound.center };
            using (var evt = PointerUpEvent.GetPooled(systemEvent))
            {
                evt.target = input;
                input.SendEvent(evt);
            }
        }
#else
        public static void RequestFromUi (VisualElement input)
        {
            var systemEvent = new Event { type = EventType.MouseUp, button = 1, mousePosition = input.worldBound.center };
            using (var evt = MouseUpEvent.GetPooled(systemEvent))
            {
                evt.target = input;
                input.SendEvent(evt);
            }
        }
#endif

        private sealed class NativeRequestCapture : IDisposable
        {
            private readonly VisualElement root;
            private readonly Action<SerializedProperty> observe;

            internal NativeRequestCapture (VisualElement root, Action<SerializedProperty> observe)
            {
                this.root = root;
                this.observe = observe;
                Register();
            }

#if UNITY_2022_2_OR_NEWER
            private void Register () => root.RegisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
            public void Dispose () => root.UnregisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
            private void OnPointerUp (PointerUpEvent evt) => Capture(evt, evt.button);
#else
            private void Register () => root.RegisterCallback<MouseUpEvent>(OnMouseUp, TrickleDown.TrickleDown);
            public void Dispose () => root.UnregisterCallback<MouseUpEvent>(OnMouseUp, TrickleDown.TrickleDown);
            private void OnMouseUp (MouseUpEvent evt) => Capture(evt, evt.button);
#endif

            private void Capture (EventBase evt, int button)
            {
                // Unity's binding stores the current property on its native menu receiver:
                // a label in older Editors, or the containing BaseField in recent Editors.
                // Observe that handoff before the native callback presents an OS menu;
                // menu presentation and command execution have separate GUI coverage.
                if (button != (int)MouseButton.RightMouse || !(evt.target is VisualElement element))
                {
                    return;
                }
                for (VisualElement receiver = element; receiver != null && receiver != root; receiver = receiver.hierarchy.parent)
                {
                    if (receiver.userData is SerializedProperty property
                        && (receiver == element || receiver.ClassListContains(BaseField<bool>.ussClassName)))
                    {
                        evt.PreventDefault();
                        evt.StopImmediatePropagation();
                        observe(property);
                        return;
                    }
                }
            }
        }

        private static object FindItem (GenericMenu menu, string label)
        {
            foreach (object item in GetField<IList>(menu, "m_MenuItems"))
            {
                if (GetField<GUIContent>(item, "content").text == label)
                {
                    return item;
                }
            }
            return null;
        }

        private static T GetField<T> (object instance, string name)
        {
            FieldInfo field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Unity menu adapter cannot find {name} on {instance.GetType()}.");
            return (T)field.GetValue(instance);
        }
    }
}
