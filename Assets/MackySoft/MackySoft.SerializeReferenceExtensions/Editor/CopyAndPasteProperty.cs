// NOTE: managedReferenceValue getter is available only in Unity 2021.3 or later.
#if UNITY_2021_3_OR_NEWER
using UnityEditor;
using UnityEngine;

namespace MackySoft.SerializeReferenceExtensions.Editor
{
	public static class CopyAndPasteProperty
	{

		const string kCopiedPropertyPathKey = "SerializeReferenceExtensions.CopiedPropertyPath";
		const string kClipboardKey = "SerializeReferenceExtensions.CopyAndPasteProperty";

		static readonly GUIContent kPasteContent = new GUIContent("Paste Property");

		[InitializeOnLoadMethod]
		static void Initialize ()
		{
			EditorApplication.contextualPropertyMenu += OnContextualPropertyMenu;
		}

		static void OnContextualPropertyMenu (GenericMenu menu, SerializedProperty property)
		{
			if (property.propertyType == SerializedPropertyType.ManagedReference)
			{
				// NOTE: When the callback function is called, the SerializedProperty is rewritten to the property that was being moused over at the time,
				// so a new SerializedProperty instance must be created.
				SerializedProperty clonedProperty = property.Copy();

				menu.AddItem(new GUIContent($"Copy \"{property.propertyPath}\" property"), false, Copy, clonedProperty);

				string copiedPropertyPath = SessionState.GetString(kCopiedPropertyPathKey, string.Empty);
				if (!string.IsNullOrEmpty(copiedPropertyPath))
				{
					menu.AddItem(new GUIContent($"Paste \"{copiedPropertyPath}\" property"), false, Paste, clonedProperty);
				}
				else
				{
					menu.AddDisabledItem(kPasteContent);
				}
			}
		}

		static void Copy (object customData)
		{
			SerializedProperty property = (SerializedProperty)customData;
			string json = JsonUtility.ToJson(property.managedReferenceValue);
			SessionState.SetString(kCopiedPropertyPathKey, property.propertyPath);
			SessionState.SetString(kClipboardKey, json);
		}

		static void Paste (object customData)
		{
			SerializedProperty property = (SerializedProperty)customData;
			string json = SessionState.GetString(kClipboardKey, string.Empty);
			if (string.IsNullOrEmpty(json))
			{
				return;
			}

			Undo.RecordObject(property.serializedObject.targetObject, "Paste Property");
			JsonUtility.FromJsonOverwrite(json, property.managedReferenceValue);
			property.serializedObject.ApplyModifiedProperties();
		}
	}
}
#endif