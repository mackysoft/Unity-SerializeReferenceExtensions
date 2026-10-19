using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace MackySoft.SerializeReferenceExtensions.Editor
{
    public static class PropertyDrawerCache
    {

        private static readonly Dictionary<Type, Type> DrawerTypes = new Dictionary<Type, Type>();
#if !UNITY_6000_0_OR_NEWER
        private static readonly Dictionary<Type, Type> DecoratorCaches = new Dictionary<Type, Type>();
        // DecoratorDrawer has no public attribute setter. Owned child views need the same
        // assignment as PropertyHandler so custom decorators receive their actual arguments.
        private static readonly FieldInfo DecoratorAttributeField = typeof(DecoratorDrawer).GetField("m_Attribute", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(DecoratorDrawer).FullName, "m_Attribute");

        private static Type GetDecoratorDrawerType (Type attributeType)
        {
            if (!DecoratorCaches.TryGetValue(attributeType, out Type decoratorType))
            {
                // Unity gives an exact attribute drawer precedence over inherited drawers.
                Type drawerType = GetCustomPropertyDrawerType(attributeType);
                decoratorType = drawerType != null && typeof(DecoratorDrawer).IsAssignableFrom(drawerType) ? drawerType : null;
                DecoratorCaches.Add(attributeType, decoratorType);
            }
            return decoratorType;
        }

        internal static bool HasDecoratorDrawer (Type attributeType) => GetDecoratorDrawerType(attributeType) != null;

        internal static bool HasDecoratorDrawer (FieldInfo field)
        {
            if (field != null)
            {
                foreach (PropertyAttribute attribute in field.GetCustomAttributes(typeof(PropertyAttribute), true))
                {
                    if (HasDecoratorDrawer(attribute.GetType()))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        internal static DecoratorDrawer[] CreateDecoratorDrawers (FieldInfo field)
        {
            var drawers = new List<DecoratorDrawer>();
            foreach (PropertyAttribute attribute in field.GetCustomAttributes<PropertyAttribute>(true).OrderBy(item => item.order))
            {
                Type drawerType = GetDecoratorDrawerType(attribute.GetType());
                if (drawerType == null)
                {
                    continue;
                }
                var drawer = (DecoratorDrawer)Activator.CreateInstance(drawerType);
                DecoratorAttributeField.SetValue(drawer, attribute);
                drawers.Add(drawer);
            }
            return drawers.ToArray();
        }
#endif

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
