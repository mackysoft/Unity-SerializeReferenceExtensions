using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MackySoft.SerializeReferenceExtensions.Editor
{
    internal static class DecoratorGUI
    {
#if !UNITY_2022_2_OR_NEWER
        internal static VisualElement CreateGUI (VisualElement propertyGUI, SerializedProperty property) => CreateOwnedGUI(propertyGUI, property);
#else
        internal static VisualElement CreateGUI (VisualElement propertyGUI, SerializedProperty property) => propertyGUI;
#endif

#if !UNITY_6000_0_OR_NEWER
        internal static VisualElement CreateOwnedGUI (VisualElement propertyGUI, SerializedProperty property)
        {
            DecoratorDrawer[] decorators = UnityPropertyDrawerUtility.GetDecoratorDrawers(property);
            if (decorators.Length == 0)
            {
                return propertyGUI;
            }

            var result = new VisualElement();
            int firstIMGUI = 0;
            for (int i = 0; i < decorators.Length; i++)
            {
                VisualElement gui = CreateDecoratorGUI(decorators[i]);
                if (gui == null)
                {
                    continue;
                }
                if (i > firstIMGUI)
                {
                    result.Add(CreateIMGUI(decorators, firstIMGUI, i));
                }
                result.Add(gui);
                firstIMGUI = i + 1;
            }
            if (firstIMGUI < decorators.Length)
            {
                result.Add(CreateIMGUI(decorators, firstIMGUI, decorators.Length));
            }
            result.Add(propertyGUI);
            return result;
        }

        private static IMGUIContainer CreateIMGUI (DecoratorDrawer[] decorators, int first, int end)
        {
            return new IMGUIContainer(() =>
            {
                float height = 0;
                for (int i = first; i < end; i++)
                {
                    height += decorators[i].GetHeight();
                }
                Rect position = GUILayoutUtility.GetRect(0, height, GUILayout.ExpandWidth(true));
                for (int i = first; i < end; i++)
                {
                    DecoratorDrawer decorator = decorators[i];
                    position.height = decorator.GetHeight();
                    float labelWidth = EditorGUIUtility.labelWidth;
                    float fieldWidth = EditorGUIUtility.fieldWidth;
                    try
                    {
                        decorator.OnGUI(position);
                    }
                    finally
                    {
                        EditorGUIUtility.labelWidth = labelWidth;
                        EditorGUIUtility.fieldWidth = fieldWidth;
                    }
                    position.y += position.height;
                }
            });
        }
#else
        internal static VisualElement CreateOwnedGUI (VisualElement propertyGUI, SerializedProperty property) => propertyGUI;
#endif

#if UNITY_2022_2_OR_NEWER && !UNITY_6000_0_OR_NEWER
        private static VisualElement CreateDecoratorGUI (DecoratorDrawer drawer) => drawer.CreatePropertyGUI();
#elif !UNITY_6000_0_OR_NEWER
        private static VisualElement CreateDecoratorGUI (DecoratorDrawer drawer) => null;
#endif
    }
}
