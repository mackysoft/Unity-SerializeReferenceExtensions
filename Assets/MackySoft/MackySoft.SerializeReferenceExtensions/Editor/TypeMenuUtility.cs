using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;

namespace MackySoft.SerializeReferenceExtensions.Editor {
	public static class TypeMenuUtility {

		public const string k_NullDisplayName = "<null>";
		static readonly Type k_UnityObjectType = typeof(UnityEngine.Object);

		public static IEnumerable<Type> GetTypes (Type baseType)
		{
			return TypeCache.GetTypesDerivedFrom(baseType).Append(baseType).Where(p =>
				(p.IsPublic || p.IsNestedPublic || p.IsNestedPrivate) &&
				!p.IsAbstract &&
				!p.IsGenericType &&
				!k_UnityObjectType.IsAssignableFrom(p) &&
				Attribute.IsDefined(p, typeof(SerializableAttribute)) &&
				!Attribute.IsDefined(p, typeof(HideInTypeMenuAttribute))
			);
		}

		public static AddTypeMenuAttribute GetAttribute (Type type) {
			return Attribute.GetCustomAttribute(type,typeof(AddTypeMenuAttribute)) as AddTypeMenuAttribute;
		}

		public static string[] GetSplittedTypePath (Type type) {
			AddTypeMenuAttribute typeMenu = GetAttribute(type);
			if (typeMenu != null) {
				return typeMenu.GetSplittedMenuName();
			} else {
				int splitIndex = type.FullName.LastIndexOf('.');
				if (splitIndex >= 0) {
					return new string[] { type.FullName.Substring(0,splitIndex),type.FullName.Substring(splitIndex + 1) };
				} else {
					return new string[] { type.Name };
				}
			}
		}

		public static IEnumerable<Type> OrderByType (this IEnumerable<Type> source) {
			return source.OrderBy(type => {
				if (type == null) {
					return -999;
				}
				return GetAttribute(type)?.Order ?? 0;
			}).ThenBy(type => {
				if (type == null) {
					return null;
				}
				return GetAttribute(type)?.MenuName ?? type.Name;
			});
		}

	}
}