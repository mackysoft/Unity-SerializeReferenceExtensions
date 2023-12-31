#if UNITY_2019_3_OR_NEWER
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace MackySoft.SerializeReferenceExtensions.Editor {
	public static class ManagedReferenceUtility {

		public static object SetManagedReference (this SerializedProperty property,Type type) {
			object result;
			if (property.managedReferenceValue != null)
			{
				// Restore an previous values from json.
				string json = JsonUtility.ToJson(property.managedReferenceValue);
				result = JsonUtility.FromJson(json, type);
			}
			else
			{
				result = (type != null) ? Activator.CreateInstance(type) : null;
			}
			
			property.managedReferenceValue = result;
			return result;

		}

		public static Type GetType (string typeName) {
			if (string.IsNullOrEmpty(typeName))
			{
				return null;
			}

			int splitIndex = typeName.IndexOf(' ');
			var assembly = Assembly.Load(typeName.Substring(0,splitIndex));
			return assembly.GetType(typeName.Substring(splitIndex + 1));
		}

	}
}
#endif