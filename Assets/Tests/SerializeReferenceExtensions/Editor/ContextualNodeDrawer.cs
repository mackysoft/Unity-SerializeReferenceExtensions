using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace MackySoft.SerializeReferenceExtensions.Tests
{
    [CustomPropertyDrawer(typeof(DrawerContextTestAsset.StatefulNode))]
    public sealed class ContextualNodeDrawer : PropertyDrawer, IDisposable
    {
        public static readonly List<(UnityEngine.Object Target, string PropertyPath)> DisposedContexts = new List<(UnityEngine.Object, string)>();
        private UnityEngine.Object observedTarget;
        private string observedPath;
        private int edits;

        public bool IsDisposed { get; private set; }

        public override float GetPropertyHeight (SerializedProperty property, GUIContent label)
        {
            DrawerContextAttribute context = fieldInfo?.GetCustomAttribute<DrawerContextAttribute>();
            int lines = context?.Lines ?? 1;
            return EditorGUIUtility.singleLineHeight * lines + EditorGUIUtility.standardVerticalSpacing * (lines - 1);
        }

        public override void OnGUI (Rect position, SerializedProperty property, GUIContent label)
        {
            if (observedPath == null)
            {
                observedTarget = property.serializedObject.targetObject;
                observedPath = property.propertyPath;
            }

            DrawerContextAttribute context = fieldInfo?.GetCustomAttribute<DrawerContextAttribute>();
            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            if (context == null)
            {
                EditorGUI.LabelField(line, "Missing declaring-field context");
                return;
            }

            EditorGUI.LabelField(line, context.Caption);
            line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            EditorGUI.LabelField(line, context.Caption + " edits: " + edits);
            line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            EditorGUI.LabelField(line, "Tooltip: " + label.tooltip);
            var button = new Rect(position.x, position.yMax - EditorGUIUtility.singleLineHeight, position.width, EditorGUIUtility.singleLineHeight);
            if (GUI.Button(button, "Edit " + context.Caption))
            {
                using (SerializedProperty amount = property.FindPropertyRelative("amount"))
                {
                    amount.intValue += context.Increment;
                }
                edits++;
                GUI.changed = true;
            }
        }

        public void Dispose ()
        {
            IsDisposed = true;
            if (observedPath != null)
            {
                DisposedContexts.Add((observedTarget, observedPath));
            }
        }
    }
}
