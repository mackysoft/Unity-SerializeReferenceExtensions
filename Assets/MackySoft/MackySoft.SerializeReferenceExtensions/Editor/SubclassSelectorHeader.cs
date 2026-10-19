using System;
using System.Linq.Expressions;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace MackySoft.SerializeReferenceExtensions.Editor
{
    // PopupField owns the line's flow and native input column. Foldout's actual
    // Toggle owns the prefix; neither control supplies measurements to the other.
    internal sealed class SubclassSelectorHeader : VisualElement
    {
        private static readonly Action<VisualElement, SerializedProperty> UpdatePrefabStyle = BindPrefabStyle();
        private static readonly Action<VisualElement> UpdateInspectorStyle = BindInspectorStyle();

        private readonly Toggle toggle;
        private readonly VisualElement checkmark;

        internal PropertyField Host { get; }
        internal SubclassSelectorTypeField TypeField { get; }

        internal SubclassSelectorHeader (PropertyField host, Toggle toggle, Action showPopup, Func<bool> hasCurrentProperty)
        {
            Host = host;
            this.toggle = toggle;
            toggle.showMixedValue = false;
            checkmark = toggle.Q(className: Toggle.checkmarkUssClassName);
            pickingMode = PickingMode.Ignore;
            style.justifyContent = Justify.Center;
            TypeField = new SubclassSelectorTypeField(showPopup);
            TypeField.SetValueWithoutNotify(TypeMenuUtility.NullDisplayName);
            // The popup's real label region still participates in native alignment.
            // The Foldout Toggle renders the visible prefix, including its arrow.
            TypeField.labelElement.style.visibility = Visibility.Hidden;
            TypeField.Input.AddManipulator(new FoldoutPropertyMenuManipulator(toggle, hasCurrentProperty));
            Add(TypeField);
            toggle.style.position = Position.Absolute;
            toggle.style.left = 0;
            toggle.style.right = 0;
            RegisterCallback<AttachToPanelEvent>(evt => RefreshInspectorStyle());
        }

        internal void RefreshInspectorStyle () => UpdateInspectorStyle?.Invoke(toggle);

        internal void Refresh (SerializedProperty property, string label, bool mixedTypes, bool enabled, string tooltip)
        {
            TypeField.label = label;
            TypeField.tooltip = tooltip;
            toggle.tooltip = tooltip;
            TypeField.SetEnabled(enabled);
            toggle.SetEnabled(enabled);
            // Foldout's bool is expansion state. Mixed serialized child values must
            // not replace its label or checkmark with Toggle's mixed-value content.
            toggle.showMixedValue = false;
            checkmark.style.visibility = mixedTypes || string.IsNullOrEmpty(property.managedReferenceFullTypename)
                ? Visibility.Hidden : Visibility.Visible;
            bool wasMixed = TypeField.showMixedValue;
            TypeField.showMixedValue = mixedTypes;
            string displayName = TypeMenuUtility.GetTypeDisplayName(property.managedReferenceValue?.GetType());
            // Clearing native mixed presentation does not restore its text. Recent
            // PopupFields retain the value, so the transition also needs a refresh.
            if (!mixedTypes && (wasMixed || TypeField.value != displayName))
            {
                TypeField.SetValueWithoutNotify(displayName);
            }
            // Older bindings discover a Foldout header with a physical Q<Toggle>.
            // This header is still the bound Toggle, but now occupies the prefix slot.
            UpdatePrefabStyle(toggle, property);
        }

        private static Action<VisualElement, SerializedProperty> BindPrefabStyle ()
        {
            Assembly assembly = typeof(PropertyField).Assembly;
            Type helpers = assembly.GetType("UnityEditor.UIElements.BindingsStyleHelpers")
                ?? assembly.GetType("UnityEditor.UIElements.Bindings.BindingsStyleHelpers");
            if (helpers == null)
            {
                // Some Editors put PropertyField in CoreModule while binding styles
                // remain in UIElementsModule. Both are native Editor-owned assemblies.
                Assembly bindings = Assembly.Load("UnityEditor.UIElementsModule");
                helpers = bindings.GetType("UnityEditor.UIElements.BindingsStyleHelpers")
                    ?? bindings.GetType("UnityEditor.UIElements.Bindings.BindingsStyleHelpers", true);
            }
            MethodInfo method = helpers.GetMethod("UpdatePrefabStateStyle", BindingFlags.Static | BindingFlags.NonPublic,
                null, new[] { typeof(VisualElement), typeof(SerializedProperty) }, null)
                ?? throw new MissingMethodException(helpers.FullName, "UpdatePrefabStateStyle");
            return (Action<VisualElement, SerializedProperty>)Delegate.CreateDelegate(typeof(Action<VisualElement, SerializedProperty>), method);
        }

        private static Action<VisualElement> BindInspectorStyle ()
        {
            // Recent Foldout themes select Inspector styles by physical ancestry.
            // Reapply the same native hook after its real Toggle changes header slots.
            FieldInfo inspectorClass = typeof(Foldout).GetField("toggleInspectorUssClassNameUnique", BindingFlags.Static | BindingFlags.NonPublic);
            if (inspectorClass == null)
            {
                return null;
            }
            Type utilities = typeof(Foldout).Assembly.GetType("UnityEngine.UIElements.VisualElementUtils", true);
            MethodInfo method = utilities.GetMethod("AssignInspectorStyleIfNecessary", BindingFlags.Static | BindingFlags.NonPublic,
                null, new[] { typeof(VisualElement), inspectorClass.FieldType }, null)
                ?? throw new MissingMethodException(utilities.FullName, "AssignInspectorStyleIfNecessary");
            var element = Expression.Parameter(typeof(VisualElement), "element");
            return Expression.Lambda<Action<VisualElement>>(Expression.Call(method, element, Expression.Field(null, inspectorClass)), element).Compile();
        }
    }
}
