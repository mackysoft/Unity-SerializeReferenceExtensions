using System;
using System.Collections;
using System.Linq;
using System.Linq.Expressions;
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
            public string StyleName { get; }
            public string Tooltip { get; }

            public DrawnContent (string text, Rect rect, string styleName, string tooltip)
            {
                Text = text;
                Rect = rect;
                StyleName = styleName;
                Tooltip = tooltip;
            }
        }

        public static DrawnContent[] Capture (SelectorInspectorWindow window)
            => CaptureAll(window).Where(item => !string.IsNullOrEmpty(item.Text)).ToArray();

        public static (string Text, bool IsBold)[] CaptureText (EditorWindow window)
        {
            // A saved GUIStyle does not retain the default-font flag restored by EndProperty.
            // Observe the actual text draw before Unity renders it, without suppressing drawing.
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            FieldInfo drawHandler = typeof(GUIStyle).GetField("onDraw", flags);
            MethodInfo boldFont = typeof(EditorGUIUtility).GetMethod("GetBoldDefaultFont", flags);
            Assert.That(drawHandler, Is.Not.Null, "Unity GUI observation adapter: text draw callback unavailable.");
            Assert.That(boldFont, Is.Not.Null, "Unity GUI observation adapter: default-font state unavailable.");
            var original = (Delegate)drawHandler.GetValue(null);
            var drawn = new System.Collections.Generic.List<(string Text, bool IsBold)>();
            Action<GUIStyle, GUIContent> observe = (style, content) =>
            {
                if (!string.IsNullOrEmpty(content.text))
                {
                    bool bold = (bool)boldFont.Invoke(null, null) || style.fontStyle == FontStyle.Bold
                        || style.fontStyle == FontStyle.BoldAndItalic;
                    drawn.Add((content.text, bold));
                }
            };
            // The last callback argument is an internal Unity struct. Match its actual
            // delegate signature instead of copying that type into the test assembly.
            ParameterExpression[] parameters = drawHandler.FieldType.GetMethod("Invoke").GetParameters()
                .Select(parameter => Expression.Parameter(parameter.ParameterType, parameter.Name)).ToArray();
            Expression paint = original == null ? (Expression)Expression.Constant(false)
                : Expression.Invoke(Expression.Constant(original, drawHandler.FieldType), parameters);
            // Unity's StylePainter may render text itself and return true. Observe
            // before that painter as well, then preserve its original return value.
            var callback = Expression.Lambda(drawHandler.FieldType, Expression.Block(
                    Expression.Invoke(Expression.Constant(observe), parameters[0], parameters[2]), paint),
                parameters).Compile();
            drawHandler.SetValue(null, callback);
            try
            {
                window.SendEvent(new Event { type = EventType.Layout });
                window.SendEvent(new Event { type = EventType.Repaint });
                return drawn.ToArray();
            }
            finally
            {
                drawHandler.SetValue(null, original);
            }
        }

        public static DrawnContent[] CaptureAll (EditorWindow window, bool unclipped = false)
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
                window.SendEvent(new Event { type = EventType.Layout });
                window.SendEvent(new Event { type = EventType.Repaint });
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
                FieldInfo styleField = instructionType.GetField("usedGUIStyle", flags);
                var viewRects = new System.Collections.Generic.Dictionary<int, Rect>();
                if (unclipped)
                {
                    // Raw draw rectangles belong to separate GUIClip contexts. Unity's
                    // unified debugger records the actual GUIView-space rectangle.
                    Type unifiedType = editorAssembly.GetType("UnityEditor.IMGUIInstruction", true);
                    var unified = (IList)Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(unifiedType));
                    MethodInfo getUnified = debugger.GetMethod("GetUnifiedInstructions", flags);
                    getUnified.Invoke(null, getUnified.GetParameters().Length == 1
                        ? new object[] { unified } : new object[] { unified, false });
                    foreach (object instruction in unified)
                    {
                        if (unifiedType.GetField("type", flags).GetValue(instruction).ToString() == "kStyleDraw")
                        {
                            int index = (int)unifiedType.GetField("typeInstructionIndex", flags).GetValue(instruction);
                            viewRects[index] = (Rect)unifiedType.GetField("unclippedRect", flags).GetValue(instruction);
                        }
                    }
                }
                return instructions.Cast<object>()
                    .Select((item, index) => new DrawnContent(((GUIContent)contentField.GetValue(item)).text,
                        unclipped ? viewRects[index] : (Rect)rectField.GetValue(item), ((GUIStyle)styleField.GetValue(item)).name,
                        ((GUIContent)contentField.GetValue(item)).tooltip))
                    .ToArray();
            }
            finally
            {
                debugger.GetMethod("StopDebugging", flags).Invoke(null, null);
            }
        }
    }
}
