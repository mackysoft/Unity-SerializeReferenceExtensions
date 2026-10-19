using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace MackySoft.SerializeReferenceExtensions.Validation
{
    [CustomEditor(typeof(UIToolkitInspectionTarget)), CanEditMultipleObjects]
    public sealed class UIToolkitInspectionTargetEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI ()
        {
            var root = new VisualElement();
            InspectorElement.FillDefaultInspector(root, serializedObject, this);
            return root;
        }
    }
}
