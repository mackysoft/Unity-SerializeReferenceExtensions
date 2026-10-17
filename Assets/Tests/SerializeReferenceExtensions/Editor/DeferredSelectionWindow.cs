using UnityEditor;
using UnityEngine;

namespace MackySoft.SerializeReferenceExtensions.Tests
{
    public sealed class DeferredSelectionWindow : EditorWindow
    {
        private SerializedObject serializedTarget;
        private string propertyPath;

        public Rect FieldRect { get; private set; }
        public bool Changed { get; private set; }

        public void Initialize (SerializedObject caller, string path)
        {
            serializedTarget = caller;
            propertyPath = path;
            position = new Rect(100f, 100f, 600f, 160f);
        }

        public void DrawNow ()
        {
            SendEvent(new Event { type = EventType.Layout });
            SendEvent(new Event { type = EventType.Repaint });
        }

        public void ClearChanges ()
        {
            Changed = false;
        }

        private void OnGUI ()
        {
            if (serializedTarget == null || serializedTarget.targetObject == null)
            {
                return;
            }

            // The caller owns Update/Apply so the selector receives its genuine pending buffer.
            using (SerializedProperty property = serializedTarget.FindProperty(propertyPath))
            {
                if (property == null)
                {
                    return;
                }
                property.isExpanded = false;
                var label = new GUIContent("Deferred selection");
                FieldRect = new Rect(16f, 16f, position.width - 32f, EditorGUI.GetPropertyHeight(property, label, true));
                using (var change = new EditorGUI.ChangeCheckScope())
                {
                    EditorGUI.PropertyField(FieldRect, property, label, true);
                    Changed |= change.changed;
                }
            }
        }

        private void OnDisable ()
        {
            serializedTarget = null;
        }
    }
}
