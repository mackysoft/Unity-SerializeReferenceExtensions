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
            if (!DrawerTypes.TryGetValue(type, out Type drawerType))
            {
                drawerType = GetCustomPropertyDrawerType(type);
                DrawerTypes.Add(type, drawerType);
            }
            drawer = (drawerType != null) ? (PropertyDrawer)Activator.CreateInstance(drawerType) : null;
            return (drawer != null);
        }

        private static Type GetCustomPropertyDrawerType (Type type)
        {
            Type[] interfaceTypes = type.GetInterfaces();

            var types = TypeCache.GetTypesWithAttribute<CustomPropertyDrawer>();
            foreach (Type drawerType in types)
            {
                var customPropertyDrawerAttributes = drawerType.GetCustomAttributes(typeof(CustomPropertyDrawer), true);
                foreach (CustomPropertyDrawer customPropertyDrawer in customPropertyDrawerAttributes)
                {
                    var field = customPropertyDrawer.GetType().GetField("m_Type", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        var fieldType = field.GetValue(customPropertyDrawer) as Type;
                        if (fieldType != null)
                        {
                            if (fieldType == type)
                            {
                                return drawerType;
                            }

                            // If the property drawer also allows for being applied to child classes, check if they match
                            var useForChildrenField = customPropertyDrawer.GetType().GetField("m_UseForChildren", BindingFlags.NonPublic | BindingFlags.Instance);
                            if (useForChildrenField != null)
                            {
                                object useForChildrenValue = useForChildrenField.GetValue(customPropertyDrawer);
                                if (useForChildrenValue is bool && (bool)useForChildrenValue)
                                {
                                    // Check interfaces
                                    if (Array.Exists(interfaceTypes, interfaceType => interfaceType == fieldType))
                                    {
                                        return drawerType;
                                    }

                                    // Check derived types
                                    Type baseType = type.BaseType;
                                    while (baseType != null)
                                    {
                                        if (baseType == fieldType)
                                        {
                                            return drawerType;
                                        }

                                        baseType = baseType.BaseType;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return null;
        }

    }
}
