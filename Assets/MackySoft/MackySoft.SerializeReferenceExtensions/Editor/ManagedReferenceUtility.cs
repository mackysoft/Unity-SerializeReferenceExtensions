using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace MackySoft.SerializeReferenceExtensions.Editor
{

    public static class ManagedReferenceUtility
    {

        public static object SetManagedReference (this SerializedProperty property, Type type)
        {
#if UNITY_2021_3_OR_NEWER
            object result = CreateManagedReference(property.managedReferenceValue, type);
#else
            object result = (type != null) ? Activator.CreateInstance(type) : null;
#endif
            property.managedReferenceValue = result;
            return result;
        }

        internal static object CreateManagedReference (object previousValue, Type type)
        {
            if (type == null)
            {
                return null;
            }
            if (previousValue != null)
            {
                // Keep the existing field-name matching semantics for both type changes and same-type replacement.
                object restored = JsonUtility.FromJson(JsonUtility.ToJson(previousValue), type);
                if (restored != null)
                {
                    return restored;
                }
            }
            return Activator.CreateInstance(type);
        }

        /// <summary> Applies previously validated and constructed replacements as one Undo operation. </summary>
        /// <remarks> Properties and their SerializedObjects are borrowed from the enclosing command scope. </remarks>
        internal static void ApplyManagedReferences (SerializedProperty[] properties, object[] values, string undoName, bool expandAssignedValues)
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(undoName);
            try
            {
                // Incremental diffs can lose subtype data after collection reordering or
                // Prefab reference-ID reuse. One complete snapshot owns Undo for this edit.
                var targets = new UnityEngine.Object[properties.Length];
                for (int i = 0; i < properties.Length; i++)
                {
                    targets[i] = properties[i].serializedObject.targetObject;
                }
                Undo.RegisterCompleteObjectUndo(targets, undoName);
                for (int i = 0; i < properties.Length; i++)
                {
                    properties[i].managedReferenceValue = values[i];
                    if (expandAssignedValues)
                    {
                        properties[i].isExpanded = values[i] != null;
                    }
                    properties[i].serializedObject.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            finally
            {
                Undo.CollapseUndoOperations(group);
            }
        }

        public static Type GetType (string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                return null;
            }

            int splitIndex = typeName.IndexOf(' ');
            var assembly = Assembly.Load(typeName.Substring(0, splitIndex));
            return assembly.GetType(typeName.Substring(splitIndex + 1));
        }

    }
}
