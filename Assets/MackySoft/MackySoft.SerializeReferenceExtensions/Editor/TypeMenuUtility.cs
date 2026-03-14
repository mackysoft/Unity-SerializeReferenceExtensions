using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace MackySoft.SerializeReferenceExtensions.Editor
{
    public static class TypeMenuUtility
    {

        public const string NullDisplayName = "<null>";

        private static readonly Dictionary<Type, string[]> splittedTypePathCache = new Dictionary<Type, string[]>();
        private static readonly Dictionary<Type, AddTypeMenuAttribute> attributeCache = new Dictionary<Type, AddTypeMenuAttribute>();
        private static readonly Dictionary<string, string> nicifyCache = new Dictionary<string, string>();

        
        public static AddTypeMenuAttribute GetAttribute (Type type)
        {
            if (type == null)
            {
                return null;
            }

            if (attributeCache.TryGetValue(type, out AddTypeMenuAttribute cached))
            {
                return cached;
            }

            var result = Attribute.GetCustomAttribute(type, typeof(AddTypeMenuAttribute)) as AddTypeMenuAttribute;
            attributeCache.Add(type, result);
            return result;
        }
        
        public static string[] GetSplittedTypePath(Type type)
        {
            if (splittedTypePathCache.TryGetValue(type, out string[] cached))
            {
                return cached;
            }

            string[] result;

            AddTypeMenuAttribute typeMenu = GetAttribute(type);
            if (typeMenu != null)
            {
                result = typeMenu.GetSplittedMenuName();
            }
            else
            {
                string fullName = GetNiceGenericFullName(type);
                int splitIndex = fullName.LastIndexOf('.');
                if (splitIndex >= 0)
                {
                    result = new string[] { fullName.Substring(0, splitIndex), fullName.Substring(splitIndex + 1) };
                }
                else
                {
                    result = new string[] { GetNiceGenericName(type) };
                }
            }

            splittedTypePathCache.Add(type, result);
            return result;
        }

        public static IEnumerable<Type> OrderByType(this IEnumerable<Type> source)
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

                return GetAttribute(type)?.MenuName ?? GetNiceGenericName(type);
            });
        }

        public static string GetNiceGenericName(Type type)
        {
            if (!type.IsGenericType)
            {
                return type.Name;
            }

            string baseName = type.Name;
            int backtickIndex = baseName.IndexOf('`');
            if (backtickIndex > 0)
            {
                baseName = baseName.Substring(0, backtickIndex);
            }

            Type[] args = type.GetGenericArguments();
            string argsJoined = string.Join(", ", args.Select(a => GetNiceGenericName(a)));
            return $"{baseName}<{argsJoined}>";
        }

        private static string GetNiceGenericFullName(Type type)
        {
            if (!type.IsGenericType)
            {
                return type.FullName ?? type.Name;
            }

            string ns = type.Namespace;
            string niceName = GetNiceGenericName(type);
            return string.IsNullOrEmpty(ns) ? niceName : $"{ns}.{niceName}";
        }
        
        public static string CachedNicifyVariableName (string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return name;
            }

            if (nicifyCache.TryGetValue(name, out string cached))
            {
                return cached;
            }

            string result = ObjectNames.NicifyVariableName(name);
            nicifyCache.Add(name, result);
            return result;
        }
    }
}