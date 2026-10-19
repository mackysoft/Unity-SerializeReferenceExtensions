using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MackySoft.SerializeReferenceExtensions.Validation
{
    [CustomPropertyDrawer(typeof(DualValue))]
    public sealed class DualValueDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight (SerializedProperty property, GUIContent label) => ValueDrawerGUI.Height(property);

        public override void OnGUI (Rect position, SerializedProperty property, GUIContent label)
            => ValueDrawerGUI.Draw(position, property, label, "Dual drawer: OnGUI");

        public override VisualElement CreatePropertyGUI (SerializedProperty property)
            => ValueDrawerGUI.Create(property, "Dual drawer: CreatePropertyGUI");
    }
}
