// NOTE: managedReferenceValue getter is available only in Unity 2021.3 or later.
#if UNITY_2021_3_OR_NEWER
using System;
using UnityEditor;
using UnityEngine;

namespace MackySoft.SerializeReferenceExtensions.Editor
{
    public static class ManagedReferenceContextualPropertyMenu
    {

        private const string ClipboardKey = "SerializeReferenceExtensions.ManagedReferenceClipboard";

        private static readonly GUIContent PasteValuesOnlyContent = new GUIContent("Paste Values Only");
        private static readonly GUIContent PasteTypeAndValuesContent = new GUIContent("Paste Type and Values");
        private static readonly GUIContent NewInstanceContent = new GUIContent("New Instance");
        private static readonly GUIContent ResetAndNewInstanceContent = new GUIContent("Reset and New Instance");

        [Serializable]
        private sealed class ClipboardSnapshot
        {
            public string sourcePropertyPath;
            public string typeName;
            public string json;
        }

        [InitializeOnLoadMethod]
        private static void Initialize ()
        {
            EditorApplication.contextualPropertyMenu += OnContextualPropertyMenu;
        }

        private static void OnContextualPropertyMenu (GenericMenu menu, SerializedProperty property)
        {
            if (property.propertyType == SerializedPropertyType.ManagedReference)
            {
                // NOTE: When the callback function is called, the SerializedProperty is rewritten to the property that was being moused over at the time,
                // so a new SerializedProperty instance must be created.
                SerializedProperty clonedProperty = property.Copy();
                // Resolve clipboard targets by path because a property iterator can become invalid while the menu is open.
                object clipboardTarget = (property.serializedObject, property.propertyPath);

                var copyContent = new GUIContent($"Copy \"{property.propertyPath}\" property");
                if (CanCopy(clonedProperty))
                {
                    menu.AddItem(copyContent, false, Copy, clipboardTarget);
                }
                else
                {
                    menu.AddDisabledItem(copyContent);
                }

                ClipboardSnapshot snapshot = ReadClipboard();
                if (CanPasteValuesOnly(clonedProperty, snapshot))
                {
                    menu.AddItem(new GUIContent(PasteValuesOnlyContent.text, $"Copy matching serialized fields from \"{snapshot.sourcePropertyPath}\"."), false, PasteValuesOnly, clipboardTarget);
                }
                else
                {
                    menu.AddDisabledItem(PasteValuesOnlyContent);
                }

                if (CanPasteTypeAndValues(clonedProperty, snapshot, out _))
                {
                    menu.AddItem(new GUIContent(PasteTypeAndValuesContent.text, $"Replace this reference with a copy of \"{snapshot.sourcePropertyPath}\"."), false, PasteTypeAndValues, clipboardTarget);
                }
                else
                {
                    menu.AddDisabledItem(PasteTypeAndValuesContent);
                }

                menu.AddSeparator("");

                bool hasInstance = clonedProperty.managedReferenceValue != null;
                if (hasInstance)
                {
                    menu.AddItem(NewInstanceContent, false, NewInstance, clonedProperty);
                    menu.AddItem(ResetAndNewInstanceContent, false, ResetAndNewInstance, clonedProperty);
                }
                else
                {
                    menu.AddDisabledItem(NewInstanceContent);
                    menu.AddDisabledItem(ResetAndNewInstanceContent);
                }
            }
        }

        private static void Copy (object customData)
        {
            if (!TryPrepareSingleTargetProperty(customData, out SerializedProperty property) || !CanCopy(property))
            {
                return;
            }

            object value = property.managedReferenceValue;
            var snapshot = new ClipboardSnapshot
            {
                sourcePropertyPath = property.propertyPath,
                typeName = value?.GetType().AssemblyQualifiedName,
                json = (value != null) ? JsonUtility.ToJson(value) : string.Empty
            };
            SessionState.SetString(ClipboardKey, JsonUtility.ToJson(snapshot));
        }

        private static void PasteValuesOnly (object customData)
        {
            ClipboardSnapshot snapshot = ReadClipboard();
            if (!TryPrepareSingleTargetProperty(customData, out SerializedProperty property) || !CanPasteValuesOnly(property, snapshot))
            {
                return;
            }

            UnityEngine.Object targetObject = property.serializedObject.targetObject;
            Undo.RecordObject(targetObject, PasteValuesOnlyContent.text);
            JsonUtility.FromJsonOverwrite(snapshot.json, property.managedReferenceValue);

            // The live instance was changed without a SerializedProperty setter, so record persistence and refresh the Inspector cache.
            PrefabUtility.RecordPrefabInstancePropertyModifications(targetObject);
            if (targetObject is ScriptableObject)
            {
                EditorUtility.SetDirty(targetObject);
            }
            property.serializedObject.Update();
        }

        private static void PasteTypeAndValues (object customData)
        {
            ClipboardSnapshot snapshot = ReadClipboard();
            if (!TryPrepareSingleTargetProperty(customData, out SerializedProperty property) || !CanPasteTypeAndValues(property, snapshot, out Type copiedType))
            {
                return;
            }

            // A copied null has no type name; an unresolvable non-null type never reaches this assignment.
            object value = (copiedType != null) ? JsonUtility.FromJson(snapshot.json, copiedType) : null;
            Undo.SetCurrentGroupName(PasteTypeAndValuesContent.text);
            property.managedReferenceValue = value;
            property.serializedObject.ApplyModifiedProperties();
            property.serializedObject.Update();
        }

        private static ClipboardSnapshot ReadClipboard ()
        {
            string json = SessionState.GetString(ClipboardKey, string.Empty);
            return !string.IsNullOrEmpty(json) ? JsonUtility.FromJson<ClipboardSnapshot>(json) : null;
        }

        private static bool CanCopy (SerializedProperty property)
        {
            // Unity 2021.3 exposes missing references with the same null value, ID and type name as an empty field.
            // Only copy null when the host has no missing types, so unresolved data cannot become a null clipboard value.
            return !property.serializedObject.isEditingMultipleObjects &&
                (property.managedReferenceValue != null || !SerializationUtility.HasManagedReferencesWithMissingTypes(property.serializedObject.targetObject));
        }

        private static bool CanPasteValuesOnly (SerializedProperty property, ClipboardSnapshot snapshot)
        {
            return !property.serializedObject.isEditingMultipleObjects && property.editable && snapshot != null &&
                !string.IsNullOrEmpty(snapshot.json) && property.managedReferenceValue != null;
        }

        private static bool CanPasteTypeAndValues (SerializedProperty property, ClipboardSnapshot snapshot, out Type copiedType)
        {
            copiedType = null;
            if (property.serializedObject.isEditingMultipleObjects || !property.editable || snapshot == null)
            {
                return false;
            }
            if (string.IsNullOrEmpty(snapshot.typeName))
            {
                return true;
            }

            copiedType = Type.GetType(snapshot.typeName);
            if (copiedType == null || string.IsNullOrEmpty(snapshot.json))
            {
                return false;
            }

            // Unity uses '/' for nested types in its static field type name; reflection uses '+'.
            Type fieldType = ManagedReferenceUtility.GetType(property.managedReferenceFieldTypename.Replace('/', '+'));
            return fieldType != null && fieldType.IsAssignableFrom(copiedType);
        }

        private static bool TryPrepareSingleTargetProperty (object customData, out SerializedProperty property)
        {
            var (serializedObject, propertyPath) = ((SerializedObject, string))customData;
            property = null;
            if (serializedObject.targetObject == null || serializedObject.isEditingMultipleObjects)
            {
                return false;
            }

            // Apply pending Inspector edits before reading the live instance or refreshing the serialized cache.
            serializedObject.ApplyModifiedProperties();
            serializedObject.Update();
            property = serializedObject.FindProperty(propertyPath);
            return property != null && property.propertyType == SerializedPropertyType.ManagedReference;
        }

        private static void NewInstance (object customData)
        {
            SerializedProperty property = (SerializedProperty)customData;
            string json = JsonUtility.ToJson(property.managedReferenceValue);

            Undo.RecordObject(property.serializedObject.targetObject, "New Instance");
            property.managedReferenceValue = JsonUtility.FromJson(json, property.managedReferenceValue.GetType());
            property.serializedObject.ApplyModifiedProperties();

            Debug.Log($"Create new instance of \"{property.propertyPath}\".");
        }

        private static void ResetAndNewInstance (object customData)
        {
            SerializedProperty property = (SerializedProperty)customData;

            Undo.RecordObject(property.serializedObject.targetObject, "Reset and New Instance");
            property.managedReferenceValue = Activator.CreateInstance(property.managedReferenceValue.GetType());
            property.serializedObject.ApplyModifiedProperties();

            Debug.Log($"Reset property and created new instance of \"{property.propertyPath}\".");
        }
    }
}
#endif
