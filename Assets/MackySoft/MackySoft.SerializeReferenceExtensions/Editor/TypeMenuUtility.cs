using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace MackySoft.SerializeReferenceExtensions.Editor
{
    public static class TypeMenuUtility
    {

        public const string NullDisplayName = "<null>";

        public static AddTypeMenuAttribute GetAttribute (Type type)
        {
            return Attribute.GetCustomAttribute(type, typeof(AddTypeMenuAttribute)) as AddTypeMenuAttribute;
        }

        public static string[] GetSplittedTypePath (Type type)
        {
            AddTypeMenuAttribute typeMenu = GetAttribute(type);
            if (typeMenu != null)
            {
                return typeMenu.GetSplittedMenuName();
            }
            else
            {
                // In the case of Generic, type information is included as shown below, so it must be extracted.
                // TestNamespace.TestClass`1[[UnityEngine.GameObject, UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]
                var name = type.IsGenericType ? type.FullName.Substring(0, type.FullName.IndexOf("[")) : type.FullName;

                int splitIndex = name.LastIndexOf('.');
                if (splitIndex >= 0)
                {
                    return new string[] { name.Substring(0, splitIndex), name.Substring(splitIndex + 1) };
                }
                else
                {
                    return new string[] { type.Name };
                }
            }
        }

        public static IEnumerable<Type> OrderByType (this IEnumerable<Type> source)
        {
            return source.OrderBy(type =>
            {
                if (type == null)
                {
                    return -999;
                }
                return GetAttribute(type)?.Order ?? 0;
            }).ThenBy(type =>
            {
                if (type == null)
                {
                    return null;
                }
                return GetAttribute(type)?.MenuName ?? type.Name;
            });
        }

    }
}