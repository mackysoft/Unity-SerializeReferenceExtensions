using UnityEditor;
using UnityEngine;

namespace MackySoft.SerializeReferenceExtensions.Validation
{
    [CustomPropertyDrawer(typeof(IMGUIValue), true)]
    public sealed class IMGUIValueDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight (SerializedProperty property, GUIContent label) => ValueDrawerGUI.Height(property);

        public override void OnGUI (Rect position, SerializedProperty property, GUIContent label)
            => ValueDrawerGUI.Draw(position, property, label, "IMGUI drawer (including derived types)");
    }
}
