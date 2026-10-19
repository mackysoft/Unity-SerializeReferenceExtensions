using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.UIElements;

namespace MackySoft.SerializeReferenceExtensions.Editor
{

    [CustomPropertyDrawer(typeof(SubclassSelectorAttribute))]
    public class SubclassSelectorDrawer : PropertyDrawer
    {

        private struct TypePopupCache
        {
            public AdvancedTypePopup TypePopup { get; }
            public AdvancedDropdownState State { get; }
            public TypePopupCache (AdvancedTypePopup typePopup, AdvancedDropdownState state)
            {
                TypePopup = typePopup;
                State = state;
            }
        }

        private sealed class TypeChangeState
        {
            public bool Pending;
        }

        private const int MaxTypePopupLineCount = 13;

        private static readonly GUIContent NullDisplayName = new GUIContent(TypeMenuUtility.NullDisplayName);
        private static readonly GUIContent IsNotManagedReferenceLabel = new GUIContent("The property type is not manage reference.");
        private static readonly GUIContent TempChildLabel = new GUIContent();

        private readonly Dictionary<string, TypePopupCache> typePopups = new Dictionary<string, TypePopupCache>();
        private readonly Dictionary<string, GUIContent> typeNameCaches = new Dictionary<string, GUIContent>();

        public override VisualElement CreatePropertyGUI (SerializedProperty property)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference)
            {
                return new HelpBox(IsNotManagedReferenceLabel.text, HelpBoxMessageType.Error);
            }

            // A drawer can be shared by several properties. Each binding owns its view and popup target.
            var selector = new SubclassSelectorField(property, GetPropertyLabel(property), ((SubclassSelectorAttribute)attribute).UseToStringAsLabel,
                UnityPropertyDrawerUtility.CaptureNextNestingLevel(property));
            return DecoratorGUI.CreateGUI(selector, property);
        }

#if UNITY_2023_2_OR_NEWER
        private string GetPropertyLabel (SerializedProperty property) => preferredLabel ?? property.displayName;
#else
        private string GetPropertyLabel (SerializedProperty property) => property.displayName;
#endif

        public override void OnGUI (Rect position, SerializedProperty property, GUIContent label)
        {
            int controlID = GUIUtility.GetControlID(property.propertyPath.GetHashCode(), FocusType.Passive);
            var typeChangeState = (TypeChangeState)GUIUtility.GetStateObject(typeof(TypeChangeState), controlID);
            bool isManagedReference = property.propertyType == SerializedPropertyType.ManagedReference;
            // Prepare the label before BeginProperty captures it for rendering.
            if (isManagedReference)
            {
                UpdatePropertyLabel(property, label);
            }

            EditorGUI.BeginProperty(position, label, property);
            try
            {
                // A type change can recreate the drawer. Keep its pending notification with the owning GUI control.
                if (typeChangeState.Pending)
                {
                    typeChangeState.Pending = false;
                    GUI.changed = true;
                }

                if (isManagedReference)
                {
                    // Render label first to avoid label overlap for lists
                    Rect foldoutLabelRect = new Rect(position);
                    foldoutLabelRect.height = EditorGUIUtility.singleLineHeight;

                    // NOTE: IndentedRect should be disabled as it causes extra indentation.
                    //foldoutLabelRect = EditorGUI.IndentedRect(foldoutLabelRect);
                    Rect popupPosition = EditorGUI.PrefixLabel(foldoutLabelRect, label);

                    // Draw the subclass selector popup.
                    if (EditorGUI.DropdownButton(popupPosition, GetTypeName(property), FocusType.Keyboard))
                    {
                        TypePopupCache popup = GetTypePopup(property);
                        DeferredManagedReferenceTarget target = DeferredManagedReferenceTarget.Capture(property);
                        if (target != null)
                        {
                            popup.TypePopup.ShowForSelection(popupPosition, item => SelectType(target, item.Type, typeChangeState));
                        }
                        return;
                    }

                    // Draw the foldout.
                    if (!string.IsNullOrEmpty(property.managedReferenceFullTypename))
                    {
                        Rect foldoutRect = GetFoldoutRect(position);

                        property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, GUIContent.none, true);
                    }

                    // Draw property if expanded.
                    if (property.isExpanded)
                    {
                        using (new EditorGUI.IndentLevelScope())
                        {
                            if (UnityPropertyDrawerUtility.HasNextPropertyDrawer(property))
                            {
                                // Let Unity invoke the next drawer with its declaring-field context and native ownership.
                                Rect indentedRect = position;
                                float foldoutDifference = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                                indentedRect.height = EditorGUI.GetPropertyHeight(property, label, true);
                                indentedRect.y += foldoutDifference;
                                EditorGUI.PropertyField(indentedRect, property, label, true);
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
                                    using (childProperty)
                                    {
                                        float height = EditorGUI.GetPropertyHeight(childProperty, new GUIContent(childProperty.displayName, childProperty.tooltip), true);
                                        childPosition.height = height;
                                        using (var changeCheck = new EditorGUI.ChangeCheckScope())
                                        {
                                            EditorGUI.PropertyField(childPosition, childProperty, true);
                                            // A child can replace its structure and invalidate the active iterator.
                                            if (changeCheck.changed)
                                            {
                                                break;
                                            }
                                        }

                                        childPosition.y += height + EditorGUIUtility.standardVerticalSpacing;
                                    }
                                }
                            }
                        }

                        CompleteIMGUI();
                    }
                }
                else
                {
                    EditorGUI.LabelField(position, label, IsNotManagedReferenceLabel);
                }
            }
            finally
            {
                EditorGUI.EndProperty();
            }
        }

#if UNITY_2021_3_OR_NEWER
        private void UpdatePropertyLabel (SerializedProperty property, GUIContent label)
        {
            if (((SubclassSelectorAttribute)attribute).UseToStringAsLabel && !property.hasMultipleDifferentValues)
            {
                object value = property.managedReferenceValue;
                if (value != null)
                {
                    label.text = value.ToString();
                }
            }
        }
#else
        private void UpdatePropertyLabel (SerializedProperty property, GUIContent label) { }
#endif

#if UNITY_2022_2_OR_NEWER && !UNITY_6000_0_OR_NEWER && !UNITY_2022_3
        private static Rect GetFoldoutRect (Rect position) => new Rect(position.x - 12, position.y, position.width, EditorGUIUtility.singleLineHeight);
#else
        private static Rect GetFoldoutRect (Rect position) => new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
#endif

#if UNITY_6000_0_OR_NEWER
        private static void CompleteIMGUI ()
        {
            // Resolve TextArea's deferred prefix label before UI Toolkit starts another context.
            GUIUtility.GetControlID(FocusType.Passive);
        }
#else
        private static void CompleteIMGUI () { }
#endif

        private TypePopupCache GetTypePopup (SerializedProperty property)
        {
            // Cache this string. This property internally call Assembly.GetName, which result in a large allocation.
            string managedReferenceFieldTypename = property.managedReferenceFieldTypename;

            if (!typePopups.TryGetValue(managedReferenceFieldTypename, out TypePopupCache result))
            {
                var state = new AdvancedDropdownState();

                Type baseType = ManagedReferenceUtility.GetType(managedReferenceFieldTypename);
                var types = TypeSearchService.TypeCandiateService.GetDisplayableTypes(baseType);
                var popup = new AdvancedTypePopup(
                    types,
                    MaxTypePopupLineCount,
                    state
                );
                result = new TypePopupCache(popup, state);
                typePopups.Add(managedReferenceFieldTypename, result);
            }
            return result;
        }

        private static void SelectType (DeferredManagedReferenceTarget target, Type type, TypeChangeState changeState)
        {
            if (ManagedReferenceSelection.SelectType(target, type, null))
            {
                changeState.Pending = true;
            }
        }

        private GUIContent GetTypeName (SerializedProperty property)
        {
            // Cache this string.
            string managedReferenceFullTypename = property.managedReferenceFullTypename;

            if (string.IsNullOrEmpty(managedReferenceFullTypename))
            {
                return NullDisplayName;
            }
            if (typeNameCaches.TryGetValue(managedReferenceFullTypename, out GUIContent cachedTypeName))
            {
                return cachedTypeName;
            }

            Type type = ManagedReferenceUtility.GetType(managedReferenceFullTypename);
            GUIContent result = new GUIContent(TypeMenuUtility.GetTypeDisplayName(type));
            typeNameCaches.Add(managedReferenceFullTypename, result);
            return result;
        }

        public override float GetPropertyHeight (SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference)
            {
                return EditorGUIUtility.singleLineHeight;
            }
            if (!property.isExpanded || string.IsNullOrEmpty(property.managedReferenceFullTypename))
            {
                return EditorGUIUtility.singleLineHeight;
            }

            float height = EditorGUIUtility.singleLineHeight;
            height += EditorGUIUtility.standardVerticalSpacing;

            if (UnityPropertyDrawerUtility.HasNextPropertyDrawer(property))
            {
                height += EditorGUI.GetPropertyHeight(property, label, true);
                return height;
            }

            height += GetChildrenHeight(property);

            return height;
        }

        private static float GetChildrenHeight (SerializedProperty property)
        {
            float height = 0f;
            bool first = true;

            foreach (SerializedProperty child in property.GetChildProperties())
            {
                using (child)
                {
                    if (!first)
                    {
                        height += EditorGUIUtility.standardVerticalSpacing;
                    }
                    first = false;

                    TempChildLabel.text = child.displayName;
                    TempChildLabel.tooltip = child.tooltip;

                    height += EditorGUI.GetPropertyHeight(child, TempChildLabel, true);
                }
            }

            return height;
        }

    }
}
