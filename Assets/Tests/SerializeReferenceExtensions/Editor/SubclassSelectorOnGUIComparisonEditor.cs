using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace MackySoft.SerializeReferenceExtensions.Tests
{
    // Selected explicitly so unrelated Inspectors keep their normal editor.
    internal sealed class SubclassSelectorOnGUIComparisonEditor : UnityEditor.Editor
    {
        internal string NativePath { get; set; }
        internal string SelectorPath { get; set; }
        internal string[] Paths { get; set; }

        public override VisualElement CreateInspectorGUI ()
        {
            var root = new VisualElement();
            foreach (string path in Paths ?? new[] { NativePath, SelectorPath })
            {
                root.Add(new PropertyField(serializedObject.FindProperty(path)));
            }
            return root;
        }
    }
}
