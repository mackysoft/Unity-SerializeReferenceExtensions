using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

namespace MackySoft.SerializeReferenceExtensions.Editor
{

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
		
		static readonly GUIContent k_NullDisplayName = new GUIContent(TypeMenuUtility.k_NullDisplayName);
		static readonly GUIContent k_IsNotManagedReferenceLabel = new GUIContent("The property type is not manage reference.");

		readonly Dictionary<string,TypePopupCache> m_TypePopups = new Dictionary<string,TypePopupCache>();
		readonly Dictionary<string,GUIContent> m_TypeNameCaches = new Dictionary<string,GUIContent>();

		SerializedProperty m_TargetProperty;

		public override void OnGUI (Rect position, SerializedProperty property, GUIContent label)
		{
			EditorGUI.BeginProperty(position, label, property);

			if (property.propertyType == SerializedPropertyType.ManagedReference)
			{
				// Render label first to avoid label overlap for lists
				Rect foldoutLabelRect = new Rect(position);
				foldoutLabelRect.height = EditorGUIUtility.singleLineHeight;

				// NOTE: IndentedRect should be disabled as it causes extra indentation.
				//foldoutLabelRect = EditorGUI.IndentedRect(foldoutLabelRect);
				Rect popupPosition = EditorGUI.PrefixLabel(foldoutLabelRect, label);

#if UNITY_2021_3_OR_NEWER
				// Override the label text with the ToString() of the managed reference.
				var subclassSelectorAttribute = (SubclassSelectorAttribute)attribute;
				if (subclassSelectorAttribute.UseToStringAsLabel && !property.hasMultipleDifferentValues)
				{
					object managedReferenceValue = property.managedReferenceValue;
					if (managedReferenceValue != null)
					{
						label.text = managedReferenceValue.ToString();
					}
				}
#endif

				// Draw the subclass selector popup.
				if (EditorGUI.DropdownButton(popupPosition, GetTypeName(property), FocusType.Keyboard))
				{
					TypePopupCache popup = GetTypePopup(property);
					m_TargetProperty = property;
					popup.TypePopup.Show(popupPosition);
				}

				// Draw the foldout.
				if (!string.IsNullOrEmpty(property.managedReferenceFullTypename))
				{
					Rect foldoutRect = new Rect(position);
					foldoutRect.height = EditorGUIUtility.singleLineHeight;

#if UNITY_2022_2_OR_NEWER && !UNITY_6000_0_OR_NEWER
					// NOTE: Position x must be adjusted.
					// FIXME: Is there a more essential solution...?
					// The most promising is UI Toolkit, but it is currently unable to reproduce all of SubclassSelector features. (Complete provision of contextual menu, e.g.)
					// 2021.3: No adjustment
					// 2022.1: No adjustment
					// 2022.2: Adjustment required
					// 2022.3: Adjustment required
					// 2023.1: Adjustment required
					// 2023.2: Adjustment required
					// 6000.0: No adjustment
					foldoutRect.x -= 12;
#endif

					property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, GUIContent.none, true);
				}

				// Draw property if expanded.
				if (property.isExpanded)
				{
					using (new EditorGUI.IndentLevelScope())
					{
						// Check if a custom property drawer exists for this type.
						PropertyDrawer customDrawer = GetCustomPropertyDrawer(property);
						if (customDrawer != null)
						{
							// Draw the property with custom property drawer.
							Rect indentedRect = position;
							float foldoutDifference = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
							indentedRect.height = customDrawer.GetPropertyHeight(property, label);
							indentedRect.y += foldoutDifference;
							customDrawer.OnGUI(indentedRect, property, label);
						}
						else
						{
							// Draw the properties of the child elements.
							// NOTE: In the following code, since the foldout layout isn't working properly, I'll iterate through the properties of the child elements myself.
							// EditorGUI.PropertyField(position, property, GUIContent.none, true);

							Rect childPosition = position;
							childPosition.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
							foreach (SerializedProperty childProperty in property.GetChildProperties())
							{
								float height = EditorGUI.GetPropertyHeight(childProperty, new GUIContent(childProperty.displayName, childProperty.tooltip), true);
								childPosition.height = height;
								EditorGUI.PropertyField(childPosition, childProperty, true);

								childPosition.y += height + EditorGUIUtility.standardVerticalSpacing;
							}
						}
					}
				}
			}
			else
			{
				EditorGUI.LabelField(position, label, k_IsNotManagedReferenceLabel);
			}

			EditorGUI.EndProperty();
		}

		PropertyDrawer GetCustomPropertyDrawer (SerializedProperty property)
		{
			Type propertyType = ManagedReferenceUtility.GetType(property.managedReferenceFullTypename);
			if (propertyType != null && PropertyDrawerCache.TryGetPropertyDrawer(propertyType, out PropertyDrawer drawer))
			{
				return drawer;
			}
			return null;
		}

		TypePopupCache GetTypePopup (SerializedProperty property) {
			// Cache this string. This property internally call Assembly.GetName, which result in a large allocation.
			string managedReferenceFieldTypename = property.managedReferenceFieldTypename;

			if (!m_TypePopups.TryGetValue(managedReferenceFieldTypename,out TypePopupCache result)) {
				var state = new AdvancedDropdownState();

				Type baseType = ManagedReferenceUtility.GetType(managedReferenceFieldTypename);
				var popup = new AdvancedTypePopup(
					TypeSearch.GetTypes(baseType),
					k_MaxTypePopupLineCount,
					state
				);
				popup.OnItemSelected += item => {
					Type type = item.Type;

					// Apply changes to individual serialized objects.
					foreach (var targetObject in m_TargetProperty.serializedObject.targetObjects) {
						SerializedObject individualObject = new SerializedObject(targetObject);
						SerializedProperty individualProperty = individualObject.FindProperty(m_TargetProperty.propertyPath);
						object obj = individualProperty.SetManagedReference(type);
						individualProperty.isExpanded = (obj != null);

						individualObject.ApplyModifiedProperties();
						individualObject.Update();
					}
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

		public override float GetPropertyHeight (SerializedProperty property,GUIContent label) {
			PropertyDrawer customDrawer = GetCustomPropertyDrawer(property);
			if (customDrawer != null)
			{
				return property.isExpanded ? EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing +  customDrawer.GetPropertyHeight(property,label):EditorGUIUtility.singleLineHeight;
			}
			else
			{
				return property.isExpanded ? EditorGUI.GetPropertyHeight(property,true) : EditorGUIUtility.singleLineHeight;
			}
		}

	}
}
