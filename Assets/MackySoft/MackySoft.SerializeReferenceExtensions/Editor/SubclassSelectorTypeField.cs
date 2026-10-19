using System;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MackySoft.SerializeReferenceExtensions.Editor
{
    // Keep the native PopupField hierarchy and theme; replace only menu activation.
    internal sealed class SubclassSelectorTypeField : PopupField<string>
    {
        private readonly Action showPopup;

        internal VisualElement Input { get; }

        internal SubclassSelectorTypeField (Action showPopup)
        {
            this.showPopup = showPopup;
            // Only the popup input picks. Its prefix belongs to the real Foldout
            // Toggle, including Simple ListView arrows inside the popup root bounds.
            pickingMode = PickingMode.Ignore;
            AddToClassList(alignedFieldUssClassName);
            labelElement.AddToClassList(PropertyField.labelUssClassName);
            Input = this.Q<VisualElement>(className: PopupField<string>.inputUssClassName);
            Input.AddToClassList(PropertyField.inputUssClassName);
            Input.pickingMode = PickingMode.Position;
            RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
            RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            RegisterCallback<NavigationSubmitEvent>(OpenPopup, TrickleDown.TrickleDown);
        }

        private void OnPointerDown (PointerDownEvent evt)
        {
            if (evt.button == (int)MouseButton.LeftMouse && Input.worldBound.Contains(evt.position))
            {
                OpenPopup(evt);
            }
        }

        private void OnPointerMove (PointerMoveEvent evt)
        {
            if (evt.button == (int)MouseButton.LeftMouse && (evt.pressedButtons & 1) != 0
                && Input.worldBound.Contains(evt.position))
            {
                OpenPopup(evt);
            }
        }

        private void OnKeyDown (KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Space || evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                OpenPopup(evt);
            }
        }

        private void OpenPopup (EventBase evt)
        {
            if (!enabledInHierarchy)
            {
                return;
            }
            evt.PreventDefault();
            evt.StopImmediatePropagation();
            Input.Focus();
            showPopup();
        }
    }
}
