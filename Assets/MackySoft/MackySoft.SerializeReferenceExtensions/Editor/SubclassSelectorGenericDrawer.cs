﻿using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

namespace MackySoft.SerializeReferenceExtensions.Editor
{
    [CustomPropertyDrawer(typeof(SubclassSelector<>), true)]
    public class SubclassSelectorGenericDrawer : PropertyDrawer
    {
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
		static readonly GUIContent k_NullDisplayName = new GUIContent(TypeMenuUtility.k_NullDisplayName);
		static readonly GUIContent k_IsNotManagedReferenceLabel = new GUIContent("The property type is not manage reference.");

		readonly Dictionary<string,TypePopupCache> m_TypePopups = new Dictionary<string,TypePopupCache>();
		readonly Dictionary<string,GUIContent> m_TypeNameCaches = new Dictionary<string,GUIContent>();

		SerializedProperty m_TargetProperty;

		public override float GetPropertyHeight (SerializedProperty property,GUIContent label) {
			var targetProperty = property.FindPropertyRelative("_value");
			return EditorGUI.GetPropertyHeight(targetProperty,true);
		}

		public override void OnGUI (Rect position,SerializedProperty property,GUIContent label)
		{
			EditorGUI.BeginProperty(position,label,property);
			m_TargetProperty = property.FindPropertyRelative("_value");
			
			if (m_TargetProperty.propertyType == SerializedPropertyType.ManagedReference) {
				// Draw the subclass selector popup.
				Rect popupPosition = new Rect(position);
				popupPosition.width -= EditorGUIUtility.labelWidth;
				popupPosition.x += EditorGUIUtility.labelWidth;
				popupPosition.height = EditorGUIUtility.singleLineHeight;

				if (EditorGUI.DropdownButton(popupPosition,GetTypeName(m_TargetProperty),FocusType.Keyboard)) {
					TypePopupCache popup = GetTypePopup(m_TargetProperty);
					popup.TypePopup.Show(popupPosition);
				}

				// Draw the managed reference property.
				EditorGUI.PropertyField(position,m_TargetProperty,label,true);
			} else {
				EditorGUI.LabelField(position,label,k_IsNotManagedReferenceLabel);
			}

			EditorGUI.EndProperty();
		}

		TypePopupCache GetTypePopup (SerializedProperty property) {
			// Cache this string. This property internally call Assembly.GetName, which result in a large allocation.
			string managedReferenceFieldTypename = property.managedReferenceFieldTypename;

			if (!m_TypePopups.TryGetValue(managedReferenceFieldTypename,out TypePopupCache result)) {
				var state = new AdvancedDropdownState();
				
				Type baseType = ManagedReferenceUtility.GetType(managedReferenceFieldTypename);
				var popup = new AdvancedTypePopup(
					TypeCache.GetTypesDerivedFrom(baseType).Append(baseType).Where(p =>
						(p.IsPublic || p.IsNestedPublic) &&
						!p.IsAbstract &&
						!p.IsGenericType &&
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
					m_TargetProperty.serializedObject.Update();
				};

				result = new TypePopupCache(popup, state);
				m_TypePopups.Add(managedReferenceFieldTypename, result);
			}
			return result;
		}

		GUIContent GetTypeName (SerializedProperty property) {
			// Cache this string.
			string managedReferenceFullTypename = property.managedReferenceFullTypename;

			if (string.IsNullOrEmpty(managedReferenceFullTypename)) {
				return k_NullDisplayName;
			}
			if (m_TypeNameCaches.TryGetValue(managedReferenceFullTypename,out GUIContent cachedTypeName)) {
				return cachedTypeName;
			}

			Type type = ManagedReferenceUtility.GetType(managedReferenceFullTypename);
			string typeName = null;

			AddTypeMenuAttribute typeMenu = TypeMenuUtility.GetAttribute(type);
			if (typeMenu != null) {
				typeName = typeMenu.GetTypeNameWithoutPath();
				if (!string.IsNullOrWhiteSpace(typeName)) {
					typeName = ObjectNames.NicifyVariableName(typeName);
				}
			}

			if (string.IsNullOrWhiteSpace(typeName)) {
				typeName = ObjectNames.NicifyVariableName(type.Name);
			}

			GUIContent result = new GUIContent(typeName);
			m_TypeNameCaches.Add(managedReferenceFullTypename,result);
			return result;
		}
    }
}