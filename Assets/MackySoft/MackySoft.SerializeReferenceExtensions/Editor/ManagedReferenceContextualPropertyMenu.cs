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
            if (property.propertyType != SerializedPropertyType.ManagedReference)
            {
                return;
            }
            string path = property.propertyPath;
            DeferredManagedReferenceTarget target = DeferredManagedReferenceTarget.Capture(property);
            ClipboardSnapshot snapshot = ReadClipboard();
            bool canCopy = false;
            bool canPasteValues = false;
            bool canPasteType = false;
            bool hasInstances = false;
            target?.TryInspect(properties =>
            {
                if (properties.Length == 1)
                {
                    canCopy = CanCopy(properties[0]);
                    canPasteValues = CanPasteValuesOnly(properties[0], snapshot);
                    canPasteType = CanPasteTypeAndValues(properties[0], snapshot, out _);
                }
                hasInstances = true;
                foreach (SerializedProperty current in properties)
                {
                    if (!current.editable || current.managedReferenceValue == null)
                    {
                        hasInstances = false;
                        break;
                    }
                }
            });

            var copyContent = new GUIContent($"Copy \"{path}\" property");
            if (canCopy)
            {
                menu.AddItem(copyContent, false, Copy, target);
            }
            else
            {
                menu.AddDisabledItem(copyContent);
            }
            if (canPasteValues)
            {
                menu.AddItem(new GUIContent(PasteValuesOnlyContent.text, $"Copy matching serialized fields from \"{snapshot.sourcePropertyPath}\"."), false, PasteValuesOnly, target);
            }
            else
            {
                menu.AddDisabledItem(PasteValuesOnlyContent);
            }
            if (canPasteType)
            {
                menu.AddItem(new GUIContent(PasteTypeAndValuesContent.text, $"Replace this reference with a copy of \"{snapshot.sourcePropertyPath}\"."), false, PasteTypeAndValues, target);
            }
            else
            {
                menu.AddDisabledItem(PasteTypeAndValuesContent);
            }

            menu.AddSeparator("");
            if (hasInstances)
            {
                menu.AddItem(NewInstanceContent, false, NewInstance, target);
                menu.AddItem(ResetAndNewInstanceContent, false, ResetAndNewInstance, target);
            }
            else
            {
                menu.AddDisabledItem(NewInstanceContent);
                menu.AddDisabledItem(ResetAndNewInstanceContent);
            }
        }

        private static void Copy (object customData)
        {
            ((DeferredManagedReferenceTarget)customData).TryExecute(properties =>
            {
                if (properties.Length != 1 || !CanCopy(properties[0]))
                {
                    return false;
                }

                SerializedProperty property = properties[0];
                object value = property.managedReferenceValue;
                var snapshot = new ClipboardSnapshot
                {
                    sourcePropertyPath = property.propertyPath,
                    typeName = value?.GetType().AssemblyQualifiedName,
                    json = (value != null) ? JsonUtility.ToJson(value) : string.Empty
                };
                SessionState.SetString(ClipboardKey, JsonUtility.ToJson(snapshot));
                return true;
            });
        }

        private static void PasteValuesOnly (object customData)
        {
            ((DeferredManagedReferenceTarget)customData).TryExecute(properties =>
            {
                ClipboardSnapshot snapshot = ReadClipboard();
                if (properties.Length != 1 || !CanPasteValuesOnly(properties[0], snapshot))
                {
                    return false;
                }

                SerializedProperty property = properties[0];
                UnityEngine.Object targetObject = property.serializedObject.targetObject;
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(PasteValuesOnlyContent.text);
                try
                {
                    Undo.RecordObject(targetObject, PasteValuesOnlyContent.text);
                    JsonUtility.FromJsonOverwrite(snapshot.json, property.managedReferenceValue);

                    // A live overwrite needs explicit persistence because it bypasses SerializedProperty assignment.
                    PrefabUtility.RecordPrefabInstancePropertyModifications(targetObject);
                    if (targetObject is ScriptableObject)
                    {
                        EditorUtility.SetDirty(targetObject);
                    }
                    property.serializedObject.Update();
                }
                finally
                {
                    Undo.CollapseUndoOperations(group);
                }
                return true;
            });
        }

        private static void PasteTypeAndValues (object customData)
        {
            ((DeferredManagedReferenceTarget)customData).TryExecute(properties =>
            {
                ClipboardSnapshot snapshot = ReadClipboard();
                if (properties.Length != 1 || !CanPasteTypeAndValues(properties[0], snapshot, out Type copiedType))
                {
                    return false;
                }

                // A copied null has no type name; an unresolvable non-null type never reaches this assignment.
                object value = (copiedType != null) ? JsonUtility.FromJson(snapshot.json, copiedType) : null;
                ManagedReferenceUtility.ApplyManagedReferences(properties, new[] { value }, PasteTypeAndValuesContent.text, false);
                return true;
            });
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

        private static void NewInstance (object customData)
        {
            ReplaceInstances((DeferredManagedReferenceTarget)customData, false);
        }

        private static void ResetAndNewInstance (object customData)
        {
            ReplaceInstances((DeferredManagedReferenceTarget)customData, true);
        }

        private static void ReplaceInstances (DeferredManagedReferenceTarget target, bool reset)
        {
            target.TryExecute(properties =>
            {
                foreach (SerializedProperty property in properties)
                {
                    if (!property.editable || property.managedReferenceValue == null)
                    {
                        return false;
                    }
                }
                var values = new object[properties.Length];
                for (int i = 0; i < properties.Length; i++)
                {
                    object current = properties[i].managedReferenceValue;
                    values[i] = reset ? Activator.CreateInstance(current.GetType()) :
                        ManagedReferenceUtility.CreateManagedReference(current, current.GetType());
                }
                ManagedReferenceUtility.ApplyManagedReferences(properties, values,
                    reset ? ResetAndNewInstanceContent.text : NewInstanceContent.text, false);
                return true;
            });
        }
    }
}
#endif
