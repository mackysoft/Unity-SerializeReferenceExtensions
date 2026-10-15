using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MackySoft.SerializeReferenceExtensions.Tests
{
    internal static class InspectorDrawing
    {
        internal readonly struct DrawnContent
        {
            public string Text { get; }
            public Rect Rect { get; }

            public DrawnContent (string text, Rect rect)
            {
                Text = text;
                Rect = rect;
            }
        }

        public static DrawnContent[] Capture (SelectorInspectorWindow window)
        {
            // Unity has no public API for reading rendered IMGUI text. Adapt its GUI debugger
            // here; package drawers and their registrations execute through PropertyField.
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Assembly editorAssembly = typeof(EditorWindow).Assembly;
            Type debugger = editorAssembly.GetType("UnityEditor.GUIViewDebuggerHelper", true);
            FieldInfo parentField = typeof(EditorWindow).GetField("m_Parent", flags);
            Assert.That(parentField, Is.Not.Null, "Unity GUI observation adapter: EditorWindow parent unavailable.");
            object view = parentField.GetValue(window);
            Assert.That(view, Is.Not.Null, "A graphics-capable Editor view is required for Inspector tests.");

            debugger.GetMethod("DebugWindow", flags).Invoke(null, new[] { view });
            try
            {
                window.DrawNow();
                Type instructionType = editorAssembly.GetType("UnityEditor.IMGUIDrawInstruction", true);
                Type listType = typeof(System.Collections.Generic.List<>).MakeGenericType(instructionType);
                var instructions = (IList)Activator.CreateInstance(listType);
                MethodInfo getInstructions = debugger.GetMethod("GetDrawInstructions", flags);
                // Newer Editors add an optional stack-trace flag; it is irrelevant to text observation.
                object[] arguments = getInstructions.GetParameters().Length == 1
                    ? new object[] { instructions }
                    : new object[] { instructions, false };
                getInstructions.Invoke(null, arguments);
                FieldInfo contentField = instructionType.GetField("usedGUIContent", flags);
                FieldInfo rectField = instructionType.GetField("rect", flags);
                return instructions.Cast<object>()
                    .Select(item => new DrawnContent(((GUIContent)contentField.GetValue(item)).text, (Rect)rectField.GetValue(item)))
                    .Where(item => !string.IsNullOrEmpty(item.Text))
                    .ToArray();
            }
            finally
            {
                debugger.GetMethod("StopDebugging", flags).Invoke(null, null);
            }
        }
    }
}
