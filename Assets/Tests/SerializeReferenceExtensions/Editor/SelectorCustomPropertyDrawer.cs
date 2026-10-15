using UnityEditor;
using UnityEngine;

namespace MackySoft.SerializeReferenceExtensions.Tests
{
    [CustomPropertyDrawer(typeof(SelectorTestAsset.DrawnNode), true)]
    public sealed class SelectorCustomPropertyDrawer : PropertyDrawer
    {
        public const string Marker = "Custom subtype editor";
        public const string EditButton = "Set subtype value";

        public override float GetPropertyHeight (SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight * 4f;
        }

        public override void OnGUI (Rect position, SerializedProperty property, GUIContent label)
        {
            var header = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(header, Marker);
            var button = new Rect(position.x, position.yMax - EditorGUIUtility.singleLineHeight, position.width, EditorGUIUtility.singleLineHeight);
            if (GUI.Button(button, EditButton))
            {
                property.FindPropertyRelative("amount").intValue = 59;
            }
        }
    }
}
