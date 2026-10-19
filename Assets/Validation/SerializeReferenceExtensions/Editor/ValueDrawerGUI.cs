using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MackySoft.SerializeReferenceExtensions.Validation
{
    internal static class ValueDrawerGUI
    {
        internal static float Height (SerializedProperty property)
        {
            float height = EditorGUIUtility.singleLineHeight;
            using (SerializedProperty child = property.Copy())
            using (SerializedProperty end = property.GetEndProperty())
            {
                bool enter = true;
                while (child.NextVisible(enter) && !SerializedProperty.EqualContents(child, end))
                {
                    enter = false;
                    height += EditorGUIUtility.standardVerticalSpacing + EditorGUI.GetPropertyHeight(child, true);
                }
            }
            return height;
        }

        internal static void Draw (Rect position, SerializedProperty property, GUIContent label, string entry)
        {
            using (new EditorGUI.PropertyScope(position, label, property))
            using (SerializedProperty child = property.Copy())
            using (SerializedProperty end = property.GetEndProperty())
            {
                position.height = EditorGUIUtility.singleLineHeight;
                EditorGUI.LabelField(position, entry, EditorStyles.boldLabel);
                bool enter = true;
                while (child.NextVisible(enter) && !SerializedProperty.EqualContents(child, end))
                {
                    enter = false;
                    position.y += position.height + EditorGUIUtility.standardVerticalSpacing;
                    position.height = EditorGUI.GetPropertyHeight(child, true);
                    EditorGUI.PropertyField(position, child, true);
                }
            }
        }

        internal static VisualElement Create (SerializedProperty property, string entry)
        {
            var root = new VisualElement();
            root.Add(new Label(entry));
            using (SerializedProperty child = property.Copy())
            using (SerializedProperty end = property.GetEndProperty())
            {
                bool enter = true;
                while (child.NextVisible(enter) && !SerializedProperty.EqualContents(child, end))
                {
                    enter = false;
                    root.Add(new PropertyField(child));
                }
            }
            return root;
        }
    }
}
