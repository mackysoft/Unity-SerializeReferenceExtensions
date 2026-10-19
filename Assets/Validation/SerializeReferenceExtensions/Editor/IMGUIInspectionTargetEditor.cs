using UnityEditor;

namespace MackySoft.SerializeReferenceExtensions.Validation
{
    [CustomEditor(typeof(IMGUIInspectionTarget)), CanEditMultipleObjects]
    public sealed class IMGUIInspectionTargetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI () => DrawDefaultInspector();
    }
}
