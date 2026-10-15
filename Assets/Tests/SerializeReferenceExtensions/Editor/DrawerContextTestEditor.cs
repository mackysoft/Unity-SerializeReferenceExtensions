using UnityEditor;
using UnityEngine;

namespace MackySoft.SerializeReferenceExtensions.Tests
{
    [CustomEditor(typeof(DrawerContextTestAsset))]
    public sealed class DrawerContextTestEditor : UnityEditor.Editor
    {
        public const string FollowingLabel = "Following context field";
        public const string Tooltip = "Consumer context tooltip";
        public string PropertyPath { get; set; } = "primary";

        public override void OnInspectorGUI ()
        {
            serializedObject.Update();
            using (SerializedProperty property = serializedObject.FindProperty(PropertyPath))
            {
                property.isExpanded = true;
                var label = new GUIContent(property.displayName, Tooltip);
                float height = EditorGUI.GetPropertyHeight(property, label, true);
                Rect position = GUILayoutUtility.GetRect(0f, height, GUILayout.ExpandWidth(true));
                EditorGUI.PropertyField(position, property, label, true);
            }
            EditorGUILayout.LabelField(FollowingLabel);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
