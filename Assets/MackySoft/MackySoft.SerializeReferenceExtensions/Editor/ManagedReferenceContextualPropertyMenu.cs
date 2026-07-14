// NOTE: managedReferenceValue getter is available only in Unity 2021.3 or later.
#if UNITY_2021_3_OR_NEWER
using System;
using UnityEditor;
using UnityEngine;

namespace MackySoft.SerializeReferenceExtensions.Editor
{
    public static class ManagedReferenceContextualPropertyMenu
    {

        private const string CopiedPropertyPathKey = "SerializeReferenceExtensions.CopiedPropertyPath";
        private const string ClipboardKey = "SerializeReferenceExtensions.CopyAndPasteProperty";
        private const string CopiedPropertyType = "SerializeReferenceExtensions.CopiedPropertyType";

        private static readonly GUIContent PasteContent = new GUIContent("Paste Property");
        private static readonly GUIContent NewInstanceContent = new GUIContent("New Instance");
        private static readonly GUIContent ResetAndNewInstanceContent = new GUIContent("Reset and New Instance");
        
        private enum ValuePasteState
        {
            Unavailable, // No copied value in clipboard
            Allowed, // No issues detected
            WillChangeType, // Pasting will overwrite type currently assigned to property
            IncompatibleType, // The copied type isn't compatible with the target property
            TypeNotFound, // The copied data referenced a type that cannot be found or is invalid
            WillNullify // The copied value is null, will nullify/delete the property value
        }

        [InitializeOnLoadMethod]
        private static void Initialize ()
        {
            EditorApplication.contextualPropertyMenu += OnContextualPropertyMenu;
        }

        private static ValuePasteState GetValuePasteState (SerializedProperty property)
        {
            string copiedPropertyPath = SessionState.GetString(CopiedPropertyPathKey, string.Empty);
            
            if (string.IsNullOrEmpty(copiedPropertyPath))
            {
                return ValuePasteState.Unavailable;
            }

            string copiedValueTypeName = SessionState.GetString(CopiedPropertyType, string.Empty);
            Type copiedValueType = Type.GetType(copiedValueTypeName);

            object currentPropertyValue = property.managedReferenceValue;
            
            if (copiedValueType == null)
            {
                if (!string.IsNullOrEmpty(copiedValueTypeName)) return ValuePasteState.TypeNotFound;
                
                return currentPropertyValue == null
                    ? ValuePasteState.Unavailable
                    : ValuePasteState.WillNullify;
            }

            if (currentPropertyValue == null)
            {
                return IsValidTypeFor(property, copiedValueType)
                    ? ValuePasteState.Allowed
                    : ValuePasteState.IncompatibleType;
            }
            
            if (copiedValueType == currentPropertyValue.GetType())
            {
                return ValuePasteState.Allowed;
            }

            return IsValidTypeFor(property, copiedValueType)
                ? ValuePasteState.WillChangeType
                : ValuePasteState.IncompatibleType;
        }

        private static bool IsValidTypeFor (SerializedProperty property, Type candidateType)
        {
            Type baseType = ManagedReferenceUtility.GetType(property.managedReferenceFieldTypename);
            if (baseType == null) return false;
            
            return TypeSearchService.TypeCandiateService.IsCandidateQualified(baseType, candidateType);
        }
        
        private static void OnContextualPropertyMenu (GenericMenu menu, SerializedProperty property)
        {
            if (property.propertyType == SerializedPropertyType.ManagedReference)
            {
                // NOTE: When the callback function is called, the SerializedProperty is rewritten to the property that was being moused over at the time,
                // so a new SerializedProperty instance must be created.
                SerializedProperty clonedProperty = property.Copy();

                menu.AddItem(new GUIContent($"Copy \"{property.propertyPath}\" property"), false, Copy, clonedProperty);

                string typeName = SessionState.GetString(CopiedPropertyType, string.Empty);
                
                string copiedPropertyPath = SessionState.GetString(CopiedPropertyPathKey, string.Empty);

                Type targetType = Type.GetType(typeName);
                
                switch (GetValuePasteState(clonedProperty))
                {
                    case ValuePasteState.Allowed:
                        menu.AddItem(new GUIContent($"Paste \"{copiedPropertyPath}\" property"), false, Paste, clonedProperty);
                        break;
                    case ValuePasteState.WillChangeType: 
                        menu.AddItem(
                            new GUIContent(
                                $"Paste \"{copiedPropertyPath}\" property (⚠️ Will overwrite type with {targetType?.Name})"
                            ),
                            false,
                            Paste,
                            clonedProperty
                        );
                        break;
                    case ValuePasteState.WillNullify:
                        menu.AddItem(new GUIContent($"Paste \"{copiedPropertyPath}\" property (⚠️ Will set to null)"), false, Paste, clonedProperty);
                        break;
                    case ValuePasteState.IncompatibleType:
                        menu.AddDisabledItem(new GUIContent($"Paste \"{copiedPropertyPath}\" property (❌ Incompatible type {targetType?.FullName})"));
                        break;
                    case ValuePasteState.TypeNotFound:
                        menu.AddDisabledItem(new GUIContent($"Paste \"{copiedPropertyPath}\" property (❌ Invalid type)"));
                        break;
                    default:
                        menu.AddDisabledItem(PasteContent);
                        break;
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
            SerializedProperty property = (SerializedProperty)customData;
            string json = JsonUtility.ToJson(property.managedReferenceValue);
            SessionState.SetString(CopiedPropertyPathKey, property.propertyPath);
            SessionState.SetString(ClipboardKey, json);
            if (property.managedReferenceValue == null)
            {
                SessionState.SetString(CopiedPropertyType, string.Empty);
            }
            else
            {
                string typeName = property.managedReferenceValue.GetType().AssemblyQualifiedName;
                SessionState.SetString(CopiedPropertyType, typeName);
            }
        }

        private static void Paste (object customData)
        {
            SerializedProperty property = (SerializedProperty)customData;
            string json = SessionState.GetString(ClipboardKey, string.Empty);
            
            string typeName = SessionState.GetString(CopiedPropertyType, string.Empty);
            Type targetType = Type.GetType(typeName);
            
            if (string.IsNullOrEmpty(json) && targetType != null)
            {
                return;
            }
            Undo.RecordObject(property.serializedObject.targetObject, "Paste Property");
            
            // targetType can be null under two conditions:
            // 1) The type was actually null (empty typeName)
            // 2) The type was not found (non-empty typename)
            if (targetType == null && !string.IsNullOrEmpty(typeName))
            {
                Debug.LogError($"Paste Failed: Could not find type {typeName}");
                return;
            }
            
            object currentTargetValue = property.managedReferenceValue;

            if (targetType == null)
            {
                property.managedReferenceValue = null;
            }
            else if (currentTargetValue == null || targetType != currentTargetValue.GetType())
            {
                object newInstance = Activator.CreateInstance(targetType);
                JsonUtility.FromJsonOverwrite(json, newInstance);
                property.managedReferenceValue = newInstance;
            }
            else
            {
                JsonUtility.FromJsonOverwrite(json, property.managedReferenceValue);
            }
            property.serializedObject.ApplyModifiedProperties();
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