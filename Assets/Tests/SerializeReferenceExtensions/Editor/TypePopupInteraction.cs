using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace MackySoft.SerializeReferenceExtensions.Tests
{
    internal static class TypePopupInteraction
    {
        public static void SelectVisibleItem (EditorWindow popup, string label)
        {
            // Unity keeps dropdown rows and the keyboard selection internal. Read them only
            // to navigate the real popup with keys; package selection callbacks run normally.
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var tree = (AdvancedDropdownItem)popup.GetType().GetField("m_CurrentlyRenderedTree", flags).GetValue(popup);
            object state = popup.GetType().GetField("m_State", flags).GetValue(popup);
            MethodInfo selectedIndex = state.GetType().GetMethod("GetSelectedIndex", flags);
#if UNITY_6000_5_OR_NEWER
            AdvancedDropdownItem[] rows = tree.childList.ToArray();
#else
            AdvancedDropdownItem[] rows = tree.children.ToArray();
#endif
            int targetIndex = Array.FindIndex(rows, row => row.name == label);
            Assert.That(targetIndex, Is.GreaterThanOrEqualTo(0), "Visible popup item missing: " + label);
            for (int step = 0; step <= rows.Length; step++)
            {
                if ((int)selectedIndex.Invoke(state, new object[] { tree }) == targetIndex)
                {
                    popup.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Return });
                    return;
                }
                popup.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.DownArrow });
            }
            Assert.Fail("Keyboard navigation did not reach popup item: " + label);
        }
    }
}
