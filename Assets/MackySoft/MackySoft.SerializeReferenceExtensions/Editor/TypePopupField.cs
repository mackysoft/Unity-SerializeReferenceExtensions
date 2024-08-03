#if UNITY_2019_3_OR_NEWER
using UnityEditor;
using UnityEngine.UIElements;

namespace MackySoft.SerializeReferenceExtensions.Editor
{
	public sealed class TypePopupField : BaseField<object>
	{

		public new static readonly string ussClassName = "unity-base-popup-field";

		public static readonly string textUssClassName = ussClassName + "__text";

		public static readonly string arrowUssClassName = ussClassName + "__arrow";

		public new static readonly string labelUssClassName = ussClassName + "__label";

		public new static readonly string inputUssClassName = ussClassName + "__input";

		readonly SerializedProperty m_Property;

		readonly Toggle m_Toggle;

		readonly VisualElement m_ArrowElement;
		readonly Label m_TextElement;

		public TypePopupField (SerializedProperty property, VisualElement visualInput) : base(property.displayName, visualInput)
		{
			m_Property = property;

			style.flexDirection = FlexDirection.Row;
			style.flexShrink = 0;
			style.flexGrow	= 1;

			AddToClassList(ussClassName);
			AddToClassList("unity-base-field__aligned");
			AddToClassList("unity-base-field__inspector-field");
			
			labelElement.AddToClassList(labelUssClassName);
			labelElement.AddToClassList("unity-popup-field__label");
			labelElement.AddToClassList("unity-property-field__label");

			m_TextElement = new Label(property.displayName)
			{
				pickingMode = PickingMode.Ignore
			};
			m_TextElement.AddToClassList(textUssClassName);
			visualInput.AddToClassList(inputUssClassName);
			visualInput.Add(m_TextElement);
			visualInput.pickingMode = PickingMode.Ignore;

			m_ArrowElement = new VisualElement();
			m_ArrowElement.AddToClassList(arrowUssClassName);
			m_ArrowElement.pickingMode = PickingMode.Ignore;
			visualInput.Add(m_ArrowElement);
		}
	}
}
#endif