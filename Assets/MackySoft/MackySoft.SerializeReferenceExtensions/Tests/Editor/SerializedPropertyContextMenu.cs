using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

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
