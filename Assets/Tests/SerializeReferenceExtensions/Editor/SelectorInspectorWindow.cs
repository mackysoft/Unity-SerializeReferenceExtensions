using UnityEditor;
using UnityEngine;

namespace MackySoft.SerializeReferenceExtensions.Tests
{
    public sealed class SelectorInspectorWindow : EditorWindow
    {
        public const string FieldLabel = "Fixture field";
        public const string FollowingLabel = "Following field";
        private SerializedObject serializedTarget;
        private string propertyPath;
        private bool expanded;

        public void Initialize (UnityEngine.Object target, string path, bool isExpanded)
        {
            serializedTarget = new SerializedObject(target);
            propertyPath = path;
            expanded = isExpanded;
            position = new Rect(100f, 100f, 600f, 300f);
        }

        public void SetProperty (string path, bool isExpanded)
        {
            propertyPath = path;
            expanded = isExpanded;
        }

        public void DrawNow ()
        {
            SendEvent(new Event { type = EventType.Layout });
            SendEvent(new Event { type = EventType.Repaint });
        }

        private void OnGUI ()
        {
            if (serializedTarget == null || serializedTarget.targetObject == null)
            {
                return;
            }

            serializedTarget.Update();
            SerializedProperty property = serializedTarget.FindProperty(propertyPath);
            property.isExpanded = expanded;
            var label = new GUIContent(FieldLabel);
            var rect = new Rect(16f, 16f, position.width - 32f, EditorGUI.GetPropertyHeight(property, label, true));
            EditorGUI.PropertyField(rect, property, label, true);
            EditorGUI.LabelField(new Rect(rect.x, rect.yMax + 8f, rect.width, EditorGUIUtility.singleLineHeight), FollowingLabel);
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
