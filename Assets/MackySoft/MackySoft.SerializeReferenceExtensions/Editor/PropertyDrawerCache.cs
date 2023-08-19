#if UNITY_2019_3_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEditor;
using System.Reflection;

namespace MackySoft.SerializeReferenceExtensions.Editor
{
	public static class PropertyDrawerCache
	{

		static readonly Dictionary<Type, PropertyDrawer> s_Caches = new Dictionary<Type, PropertyDrawer>();

		public static bool TryGetPropertyDrawer (Type type,out PropertyDrawer drawer)
		{
			if (!s_Caches.TryGetValue(type,out drawer))
			{
				Type drawerType = GetCustomPropertyDrawerType(type);
				drawer = (drawerType != null) ? (PropertyDrawer)Activator.CreateInstance(drawerType) : null;

				s_Caches.Add(type, drawer);
			}
			return (drawer != null);
		}

		static Type GetCustomPropertyDrawerType (Type type)
		{
			foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				foreach (Type drawerType in assembly.GetTypes())
				{
					var customPropertyDrawerAttributes = drawerType.GetCustomAttributes(typeof(CustomPropertyDrawer), true);
					foreach (CustomPropertyDrawer customPropertyDrawer in customPropertyDrawerAttributes)
					{
						var field = customPropertyDrawer.GetType().GetField("m_Type", BindingFlags.NonPublic | BindingFlags.Instance);
						if (field != null)
						{
							var fieldType = field.GetValue(customPropertyDrawer) as Type;
							if (fieldType != null && fieldType == type)
							{
								return drawerType;
							}
						}
					}
				}
			}
			return null;
		}

	}
}
#endif