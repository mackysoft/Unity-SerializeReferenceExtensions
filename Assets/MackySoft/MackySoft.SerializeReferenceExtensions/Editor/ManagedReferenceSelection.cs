using System;
using UnityEditor;

namespace MackySoft.SerializeReferenceExtensions.Editor
{
    internal static class ManagedReferenceSelection
    {
        internal static bool SelectType (DeferredManagedReferenceTarget target, Type type, Action beforeApply)
        {
            return target.TryExecute(properties =>
            {
                for (int i = 0; i < properties.Length; i++)
                {
                    Type fieldType = ManagedReferenceUtility.GetType(properties[i].managedReferenceFieldTypename.Replace('/', '+'));
                    if (!properties[i].editable || (type != null && (fieldType == null || !fieldType.IsAssignableFrom(type))))
                    {
                        return false;
                    }
                }

                var values = new object[properties.Length];
                for (int i = 0; i < properties.Length; i++)
                {
                    values[i] = ManagedReferenceUtility.CreateManagedReference(properties[i].managedReferenceValue, type);
                }
                // Release UI bindings only after every target and replacement is valid, while
                // the old child paths still exist. IMGUI has no retained bindings to release.
                beforeApply?.Invoke();
                ManagedReferenceUtility.ApplyManagedReferences(properties, values, "Change Managed Reference Type", true);
                return true;
            });
        }
    }
}
