using System;
using System.Reflection;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace MackySoft.SerializeReferenceExtensions.Editor
{
    internal static class FoldoutPropertyNotifications
    {
        private static readonly MethodInfo NativeBooleanChange = FindNativeBooleanChange();

        internal static void Install (Foldout field)
        {
            // PropertyField registers its custom-drawer callbacks before attaching the view.
            field.RegisterCallback<AttachToPanelEvent>(OnFirstAttach);
        }

        private static void OnFirstAttach (AttachToPanelEvent evt)
        {
            var field = (Foldout)evt.currentTarget;
            field.UnregisterCallback<AttachToPanelEvent>(OnFirstAttach);
            PropertyField owner = field.parent as PropertyField;
            if (owner == null)
            {
                return;
            }

            var native = (EventCallback<ChangeEvent<bool>>)Delegate.CreateDelegate(
                typeof(EventCallback<ChangeEvent<bool>>), owner, NativeBooleanChange);
            // Delegate identity selects only Unity's value-notification callback. The native
            // expansion binding and consumer callbacks on this Foldout remain registered.
            field.UnregisterCallback(native);
            field.RegisterCallback<ChangeEvent<bool>>(change =>
            {
                if (!(change.target is Foldout))
                {
                    native(change);
                }
            });
        }

        private static MethodInfo FindNativeBooleanChange ()
        {
            foreach (MethodInfo method in typeof(PropertyField).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic))
            {
                if (!method.Name.StartsWith("<RegisterPropertyChangesOnCustomDrawerElement>", StringComparison.Ordinal))
                {
                    continue;
                }
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType == typeof(ChangeEvent<bool>))
                {
                    return method;
                }
            }
            throw new MissingMethodException(typeof(PropertyField).FullName, "RegisterPropertyChangesOnCustomDrawerElement: bool callback");
        }
    }
}
