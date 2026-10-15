using UnityEditor;
using UnityEngine;

namespace MackySoft.SerializeReferenceExtensions.Tests
{
    [CustomPropertyDrawer(typeof(TraversalTestAsset.ResizableCollection))]
    public sealed class TraversalCollectionDrawer : PropertyDrawer
    {
        public const string ResizeButton = "Add child items";

        public override float GetPropertyHeight (SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight * 2f + EditorGUIUtility.standardVerticalSpacing;
        }

        public override void OnGUI (Rect position, SerializedProperty property, GUIContent label)
        {
            var header = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(header, label);
            var button = new Rect(position.x, header.yMax + EditorGUIUtility.standardVerticalSpacing, position.width, EditorGUIUtility.singleLineHeight);
            if (GUI.Button(button, ResizeButton))
            {
                using (SerializedProperty values = property.FindPropertyRelative("values"))
                {
                    values.arraySize += 2;
                    property.serializedObject.ApplyModifiedProperties();
                }
                GUI.changed = true;
            }
        }
    }
}
