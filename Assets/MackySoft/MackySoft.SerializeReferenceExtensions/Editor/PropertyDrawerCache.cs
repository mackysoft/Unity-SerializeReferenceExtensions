using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;

namespace MackySoft.SerializeReferenceExtensions.Editor
{
    public static class PropertyDrawerCache
    {
        private static readonly Dictionary<Type, Type> DrawerTypes = new Dictionary<Type, Type>();

        /// <summary> Creates an unbound custom drawer registered for the specified type. </summary>
        /// <param name="type"> The value type whose drawer registration is resolved. </param>
        /// <param name="drawer"> A fresh drawer when a registration exists; otherwise, null. The caller owns this instance and must release it if it implements <see cref="IDisposable"/>. </param>
        /// <returns> True when a custom drawer registration exists. </returns>
        /// <remarks> A type-only lookup supplies neither declaring-field context nor Unity handler ownership. </remarks>
        public static bool TryGetPropertyDrawer (Type type, out PropertyDrawer drawer)
        {
            Type drawerType = GetPropertyDrawerType(type);
            drawer = drawerType != null ? (PropertyDrawer)Activator.CreateInstance(drawerType) : null;
            return drawer != null;
        }

        private static Type GetPropertyDrawerType (Type type)
        {
            if (!DrawerTypes.TryGetValue(type, out Type drawerType))
            {
                drawerType = GetCustomPropertyDrawerType(type);
                if (drawerType != null && !typeof(PropertyDrawer).IsAssignableFrom(drawerType))
                {
                    drawerType = null;
                }
                DrawerTypes.Add(type, drawerType);
            }
            return drawerType;
        }

        private static Type GetCustomPropertyDrawerType (Type type)
        {
            // Resolve the most specific class first, then an inherited interface drawer.
            // TypeCache enumeration order must not let a base drawer hide an exact match.
            for (Type candidate = type; candidate != null; candidate = candidate.BaseType)
            {
                Type drawerType = GetExactDrawerType(candidate, candidate != type);
                if (drawerType == null && candidate.IsGenericType && !candidate.IsGenericTypeDefinition)
                {
                    drawerType = GetExactDrawerType(candidate.GetGenericTypeDefinition(), candidate != type);
                }
                if (drawerType != null)
                {
                    return drawerType;
                }
            }
            foreach (Type interfaceType in type.GetInterfaces())
            {
                Type drawerType = GetExactDrawerType(interfaceType, true);
                if (drawerType != null)
                {
                    return drawerType;
                }
            }
            return null;
        }

        private static Type GetExactDrawerType (Type type, bool requireUseForChildren)
        {
            foreach (Type drawerType in TypeCache.GetTypesWithAttribute<CustomPropertyDrawer>())
            {
                foreach (CustomPropertyDrawer attribute in drawerType.GetCustomAttributes(typeof(CustomPropertyDrawer), true))
                {
                    var typeField = typeof(CustomPropertyDrawer).GetField("m_Type", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (typeField?.GetValue(attribute) as Type != type)
                    {
                        continue;
                    }
                    var childrenField = typeof(CustomPropertyDrawer).GetField("m_UseForChildren", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (!requireUseForChildren || childrenField?.GetValue(attribute) is true)
                    {
                        return drawerType;
                    }
                }
            }
            return null;
        }
    }
}
