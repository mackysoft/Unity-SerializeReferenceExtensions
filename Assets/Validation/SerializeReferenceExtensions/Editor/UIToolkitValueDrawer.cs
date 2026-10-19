using UnityEditor;
using UnityEngine.UIElements;

namespace MackySoft.SerializeReferenceExtensions.Validation
{
    [CustomPropertyDrawer(typeof(UIToolkitValue))]
    public sealed class UIToolkitValueDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI (SerializedProperty property)
            => ValueDrawerGUI.Create(property, "UI Toolkit only drawer");
    }
}
