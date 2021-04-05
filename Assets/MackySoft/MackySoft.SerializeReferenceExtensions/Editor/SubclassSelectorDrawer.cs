#if UNITY_2019_3_OR_NEWER
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

namespace MackySoft.SerializeReferenceExtensions.Editor {

	[CustomPropertyDrawer(typeof(SubclassSelectorAttribute))]
	public class SubclassSelectorDrawer : PropertyDrawer {

		struct TypePopupCache {
			public AdvancedTypePopup TypePopup { get; }
			public AdvancedDropdownState State { get; }
			public TypePopupCache (AdvancedTypePopup typePopup,AdvancedDropdownState state) {
				TypePopup = typePopup;
				State = state;
			}
		}

		const int k_MaxTypePopupLineCount = 13;
		static readonly Type k_UnityObjectType = typeof(UnityEngine.Object);
		static readonly GUIContent k_IsNotManagedReferenceLabel = new GUIContent("The property type is not manage reference.");

		readonly Dictionary<string,TypePopupCache> m_TypePopups = new Dictionary<string,TypePopupCache>();

		SerializedProperty m_TargetProperty;
		
		public override void OnGUI (Rect position,SerializedProperty property,GUIContent label) {
			EditorGUI.BeginProperty(position,label,property);

			if (property.propertyType == SerializedPropertyType.ManagedReference) {
				TypePopupCache popup = GetTypePopup(property);

				// Draw the subclass selector popup.
				Rect popupPosition = new Rect(position);
				popupPosition.width -= EditorGUIUtility.labelWidth;
				popupPosition.x += EditorGUIUtility.labelWidth;
				popupPosition.height = EditorGUIUtility.singleLineHeight;

				if (EditorGUI.DropdownButton(popupPosition,new GUIContent(GetTypeName(property)),FocusType.Keyboard)) {
					m_TargetProperty = property;
					popup.TypePopup.Show(popupPosition);
				}

				// Draw the managed reference property.
				EditorGUI.PropertyField(position,property,label,true);
			} else {
				EditorGUI.LabelField(position,label,k_IsNotManagedReferenceLabel);
			}

			EditorGUI.EndProperty();
		}

		TypePopupCache GetTypePopup (SerializedProperty property) {
			if (!m_TypePopups.TryGetValue(property.managedReferenceFieldTypename,out TypePopupCache result)) {
				var state = new AdvancedDropdownState();

				Type baseType = property.GetManagedReferenceFieldType();
				var popup = new AdvancedTypePopup(
					AppDomain.CurrentDomain.GetAssemblies()
						.SelectMany(assembly => assembly.GetTypes())
						.Where(p =>
							p.IsClass &&
							(p.IsPublic || p.IsNestedPublic) &&
							!p.IsAbstract &&
							!p.IsGenericType &&
							baseType.IsAssignableFrom(p) &&
							!k_UnityObjectType.IsAssignableFrom(p) &&
							Attribute.IsDefined(p,typeof(SerializableAttribute))
						),
					k_MaxTypePopupLineCount,
					state
				);
				popup.OnItemSelected += item => {
					Type type = item.Type;
					object obj = m_TargetProperty.SetManagedReference(type);
					m_TargetProperty.isExpanded = (obj != null);
					m_TargetProperty.serializedObject.ApplyModifiedProperties();
				};

				m_TypePopups.Add(property.managedReferenceFieldTypename,new TypePopupCache(popup,state));
			}
			return result;
		}

		static string GetTypeName (SerializedProperty property) {
			if (string.IsNullOrEmpty(property.managedReferenceFullTypename)) {
				return TypeMenuUtility.k_NullDisplayName;
			}

			Type type = property.GetManagedReferenceType();

			AddTypeMenuAttribute typeMenu = TypeMenuUtility.GetAttribute(type);
			if (typeMenu != null) {
				string typeName = typeMenu.GetTypeNameWithoutPath();
				if (!string.IsNullOrWhiteSpace(typeName)) {
					return ObjectNames.NicifyVariableName(typeName);
				}
			}

			return ObjectNames.NicifyVariableName(type.Name);
		}

		public override float GetPropertyHeight (SerializedProperty property,GUIContent label) {
			return EditorGUI.GetPropertyHeight(property,true);
		}

	}
}
#endif