using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MackySoft.SerializeReferenceExtensions.Editor
{
    // Reuse PropertyField's complete IMGUI path, including each Editor version's layout.
    // The adapter only owns the writer cache and the continuation past SubclassSelector.
    internal sealed class NativePropertyIMGUIField : PropertyField
    {
        private const BindingFlags NativeInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly Func<PropertyField, VisualElement> CreateContainer = (Func<PropertyField, VisualElement>)Delegate.CreateDelegate(
            typeof(Func<PropertyField, VisualElement>), typeof(PropertyField).GetMethod("CreatePropertyIMGUIContainer", NativeInstance));
        private static readonly FieldInfo NativeProperty = RequireField("m_SerializedProperty");
        private static readonly FieldInfo NativeObject = typeof(PropertyField).GetField("m_SerializedObject", NativeInstance);
        private static readonly FieldInfo NativeNestingLevel = RequireField("m_DrawNestingLevel");
        private static readonly FieldInfo NativeIMGUIChild = typeof(PropertyField).GetField("m_imguiChildField", NativeInstance);
        private static readonly FieldInfo NativeLabel = typeof(PropertyField).GetField("m_Label", NativeInstance);
        private static readonly string NativeIMGUIClass = (string)typeof(PropertyField)
            .GetField("imguiContainerPropertyUssClassName", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null);

        private readonly SerializedObject source;
        private readonly string path;
        private readonly Type type;
        private readonly Foldout selector;
        private readonly Action drawNative;
        private SerializedObject writer;
        private SerializedProperty property;
        private bool published;

        internal NativePropertyIMGUIField (SerializedProperty sourceProperty, string label, int nestingLevel, Foldout selector)
        {
            source = sourceProperty.serializedObject;
            path = sourceProperty.propertyPath;
            type = sourceProperty.managedReferenceValue.GetType();
            this.selector = selector;
            this.label = label;
            NativeNestingLevel.SetValue(this, nestingLevel);
            if (NativeIMGUIClass != null)
            {
                AddToClassList(NativeIMGUIClass);
            }

            var container = (IMGUIContainer)CreateContainer(this);
            NativeIMGUIChild?.SetValue(this, container);
            drawNative = container.onGUIHandler;
            container.onGUIHandler = Draw;
            hierarchy.Add(container);
            RegisterCallback<SerializedPropertyChangeEvent>(ForwardChange);
            RegisterCallback<DetachFromPanelEvent>(OnDetach);
        }

        private void Draw ()
        {
            if (panel == null || source.targetObject == null)
            {
                return;
            }
            if (writer == null)
            {
                writer = new SerializedObject(source.targetObjects, source.context);
                property = writer.FindProperty(path);
                NativeProperty.SetValue(this, property);
                NativeObject?.SetValue(this, writer);
                published = false;
            }
            writer.forceChildVisibility = source.forceChildVisibility;
            writer.maxArraySizeForMultiEditing = source.maxArraySizeForMultiEditing;
            writer.UpdateIfRequiredOrScript();
            if (property == null || property.propertyType != SerializedPropertyType.ManagedReference
                || property.managedReferenceValue?.GetType() != type)
            {
                return;
            }
            if (label != selector.text)
            {
                // Updating the native label must not start a new binding at this continuation.
                if (NativeLabel != null)
                {
                    NativeLabel.SetValue(this, selector.text);
                }
                else
                {
                    label = selector.text;
                }
            }
            // Start this IMGUI context at the Inspector's neutral indent. PropertyField
            // adds the actual UITK Foldout depth; a separate container's GUI state does not.
            using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel))
            {
                drawNative();
                CompleteIMGUI();
            }
        }

        private void ForwardChange (SerializedPropertyChangeEvent evt)
        {
            if (evt.target != this)
            {
                return;
            }
            // The public selector host must expose the same writer cache as its OnGUI drawer.
            // Retaining it also keeps edits made by earlier receivers visible after detach.
            evt.StopPropagation();
            published = true;
            VisualElement target = selector.GetFirstAncestorOfType<PropertyField>();
            target ??= selector;
            using (var forwarded = SerializedPropertyChangeEvent.GetPooled(evt.changedProperty.Copy()))
            {
                forwarded.target = target;
                target.SendEvent(forwarded);
            }
        }

        private void OnDetach (DetachFromPanelEvent evt)
        {
            if (evt.target != this)
            {
                return;
            }
            NativeProperty.SetValue(this, null);
            NativeObject?.SetValue(this, null);
            if (!published)
            {
                property?.Dispose();
                writer?.Dispose();
            }
            // Published properties retain the writer for queued and later event receivers.
            property = null;
            writer = null;
            published = false;
        }

#if UNITY_6000_0_OR_NEWER
        private static void CompleteIMGUI () => GUIUtility.GetControlID(FocusType.Passive);
#else
        private static void CompleteIMGUI () { }
#endif

        private static FieldInfo RequireField (string name)
            => typeof(PropertyField).GetField(name, NativeInstance)
                ?? throw new MissingFieldException(typeof(PropertyField).FullName, name);
    }
}
