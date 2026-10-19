using System;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MackySoft.SerializeReferenceExtensions.Editor
{
    // One view per binding: ListView rebinds and Inspector rebuilds must not share a property iterator.
    internal sealed class SubclassSelectorField : Foldout
    {
        private const string UssClassName = "mackysoft-subclass-selector";
        private readonly SerializedObject serializedObject;
        private readonly string defaultLabel;
        private readonly bool useToStringAsLabel;
        private readonly string propertyPath;
        private readonly int drawerNestingLevel;
        private readonly Toggle header;
        private readonly IVisualElementScheduledItem refresh;
#if !UNITY_6000_0_OR_NEWER
        private BindableElement bindingGuard;
#endif
#if SERIALIZE_REFERENCE_EXTENSIONS_UNITY_2022_3_MR_TRACKER || UNITY_2023_2_OR_NEWER
        private NativeManagedReferenceTracking nativeReferenceTracking;
#endif

        private SubclassSelectorBody body;
        private SubclassSelectorHeader nativeHeader;
        private VisualElement headerSlot;

        private AdvancedTypePopup typePopup;
        private string displayedType;
        private long displayedId;
        private bool displayedMixedTypes;
        private bool initialized;

        public override VisualElement contentContainer => headerSlot ?? base.contentContainer;

        internal SubclassSelectorField (SerializedProperty property, string label, bool useToStringAsLabel, int drawerNestingLevel)
        {
            serializedObject = property.serializedObject;
            defaultLabel = label;
            this.useToStringAsLabel = useToStringAsLabel;
            this.drawerNestingLevel = drawerNestingLevel;
            propertyPath = property.propertyPath;
            bindingPath = propertyPath;
            tooltip = property.tooltip;
            AddToClassList(UssClassName);
            pickingMode = PickingMode.Ignore;
            this.RegisterValueChangedCallback(OnExpansionChanged);
            FoldoutPropertyNotifications.Install(this);
            header = this.Q<Toggle>(className: Foldout.toggleUssClassName);
            NativePropertyFieldControls.ConfigureFoldout(this);
            // Keep the actual native Toggle. Its binding and Inspector styles are
            // established before the header is placed beside the native PopupField.

            RegisterCallback<AttachToPanelEvent>(OnAttach);
            RegisterCallback<DetachFromPanelEvent>(OnDetach);
            Refresh(property, false);
            // TrackSerializedObjectValue can retain a released updater during ListView recycling
            // in 2021.3. Presentation updates belong to this view's scheduler instead.
            refresh = schedule.Execute(UpdateView).Every(0);
        }

        private void OnExpansionChanged (ChangeEvent<bool> evt)
        {
            // The native binding owns isExpanded. Ancestor custom drawers must not turn
            // this view state into a serialized value-change notification.
            if (evt.target is Foldout)
            {
                evt.StopPropagation();
            }
        }

        private void OnAttach (AttachToPanelEvent evt)
        {
            EditorApplication.delayCall -= ReleaseHeader;
            PrepareReferenceTracking();
            CreateChildBindingGuard();
            // Wait until Unity finishes dispatching Attach to the existing descendants before
            // replacing them; their queued binding callbacks still refer to the old fields.
            refresh.Resume();
        }

        private void OnDetach (DetachFromPanelEvent evt)
        {
            refresh.Pause();
            CancelReferenceTracking();
            ReleaseChildBindingGuard();
            // A reference can change shape while detached. Release child bindings before their
            // property paths disappear, and rebuild them on the next attachment.
            ReleaseDefaultBodyBindings();
            contentContainer.Unbind();
            initialized = false;
            if (nativeHeader != null)
            {
                nativeHeader.style.display = DisplayStyle.None;
            }
            // A parent can be traversing its children during Detach. Release the
            // owned header after that traversal, unless this view is reattached.
            EditorApplication.delayCall -= ReleaseHeader;
            EditorApplication.delayCall += ReleaseHeader;
        }

        private void ReleaseHeader ()
        {
            if (nativeHeader != null && (panel == null || nativeHeader.Host != GetFirstAncestorOfType<PropertyField>()))
            {
                hierarchy.Insert(0, header);
                nativeHeader.RemoveFromHierarchy();
                nativeHeader = null;
            }
        }

        private void EnsureHeader ()
        {
            // Native Foldout binding discovers this physical Toggle and installs its
            // current property menu before we move the prefix into its header slot.
            if (!(header.userData is SerializedProperty))
            {
                return;
            }
            PropertyField host = GetFirstAncestorOfType<PropertyField>();
            if (host == null)
            {
                return;
            }
            if (nativeHeader?.Host != host)
            {
                nativeHeader?.RemoveFromHierarchy();
                nativeHeader = new SubclassSelectorHeader(host, header, ShowTypePopup, HasCurrentMenuProperty);
            }
            // PopupField belongs at the property's depth. Its actual BaseField
            // owns the native label/input split, outside this body Foldout.
            // Stay beside this Foldout inside any decorator wrapper. The wrapper's
            // preceding native decorators must remain above the entire property row.
            VisualElement container = hierarchy.parent;
            int branchIndex = container.hierarchy.IndexOf(this);
            if (nativeHeader.hierarchy.parent != container || container.hierarchy.IndexOf(nativeHeader) != branchIndex - 1)
            {
                // The same host can clear and reattach this view before delayed
                // cleanup runs. Restore its renderer as well as its body.
                nativeHeader.RemoveFromHierarchy();
                container.hierarchy.Insert(container.hierarchy.IndexOf(this), nativeHeader);
            }
            if (header.hierarchy.parent != nativeHeader)
            {
                // Native Add preserves Foldout as logical owner (including Simple
                // ListView's direct-child selectors) while declaring its prefix slot.
                headerSlot = nativeHeader;
                try
                {
                    Insert(0, header);
                }
                finally
                {
                    headerSlot = null;
                }
                nativeHeader.RefreshInspectorStyle();
            }
        }

        private bool HasCurrentMenuProperty ()
        {
            if (panel == null || serializedObject.targetObject == null)
            {
                return false;
            }
            using (SerializedProperty property = serializedObject.FindProperty(propertyPath))
            {
                return property != null && property.propertyType == SerializedPropertyType.ManagedReference;
            }
        }

#if !UNITY_6000_0_OR_NEWER
        private void CreateChildBindingGuard ()
        {
            // Older Editors synchronize cached child properties in IBinding.Update. All
            // PreUpdate calls run first, so invalidate old paths even if a busy scheduler
            // postponed this view's presentation update. This binding is owned, never pooled.
            bindingGuard = new BindableElement { binding = new ChildBindingGuard(this) };
            hierarchy.Add(bindingGuard);
        }

        private void ReleaseChildBindingGuard ()
        {
            // Unbind only releases Unity's SerializedObject bindings, not a custom IBinding.
            bindingGuard.binding.Release();
            bindingGuard.binding = null;
            bindingGuard.RemoveFromHierarchy();
            bindingGuard = null;
        }

        private sealed class ChildBindingGuard : IBinding
        {
            private SubclassSelectorField owner;

            internal ChildBindingGuard (SubclassSelectorField owner) => this.owner = owner;

            public void PreUpdate () => owner?.PrepareChildBindings();
            public void Update () { }
            public void Release () => owner = null;
        }

        private void PrepareChildBindings ()
        {
            if (!initialized || panel == null || serializedObject.targetObject == null)
            {
                return;
            }
            serializedObject.UpdateIfRequiredOrScript();
            using (SerializedProperty property = serializedObject.FindProperty(propertyPath))
            {
                if (property == null)
                {
                    this.Unbind();
                }
                else if (property.managedReferenceFullTypename == displayedType && property.managedReferenceId == displayedId
                    && HasMixedTypes(property) == displayedMixedTypes)
                {
                    PrepareDefaultBodyBindings();
                    return;
                }
                // Do not add new bindings during Unity's traversal. The scheduler builds the new
                // body; the old scalar bindings must be released before their next Update.
                ReleaseDefaultBodyBindings();
                contentContainer.Unbind();
                contentContainer.Clear();
                initialized = false;
            }
        }
#else
        private void CreateChildBindingGuard () { }
        private void ReleaseChildBindingGuard () { }
#endif

#if SERIALIZE_REFERENCE_EXTENSIONS_UNITY_2022_3_MR_TRACKER || UNITY_2023_2_OR_NEWER
        private void PrepareReferenceTracking ()
        {
            nativeReferenceTracking = new NativeManagedReferenceTracking(this);
        }

        private void CancelReferenceTracking ()
        {
            nativeReferenceTracking?.Dispose();
            nativeReferenceTracking = null;
        }
#else
        private void PrepareReferenceTracking () { }
        private void CancelReferenceTracking () { }
#endif

        private void UpdateView ()
        {
            if (panel == null || serializedObject.targetObject == null)
            {
                return;
            }
            EnsureHeader();
            serializedObject.UpdateIfRequiredOrScript();
            using (SerializedProperty property = serializedObject.FindProperty(propertyPath))
            {
                if (property != null && property.propertyType == SerializedPropertyType.ManagedReference)
                {
                    Refresh(property, true);
                    RefreshDefaultBody();
                }
                else if (initialized)
                {
                    // A removed array element can remain in the tree until ListView updates.
                    ReleaseDefaultBodyBindings();
                    this.Unbind();
                    contentContainer.Clear();
                    initialized = false;
                }
            }
        }

        private void Refresh (SerializedProperty property, bool rebind)
        {
            string typeName = property.managedReferenceFullTypename;
            long id = property.managedReferenceId;
            bool mixedTypes = HasMixedTypes(property);
            object instance = property.managedReferenceValue;
            // PropertyDrawer.preferredLabel was added after our minimum Editor version.
            // Read the public host label as well, including an explicitly empty label.
            PropertyField host = GetFirstAncestorOfType<PropertyField>();
            string label = host != null && host.bindingPath == propertyPath && host.label != null ? host.label : defaultLabel;
            text = useToStringAsLabel && !property.hasMultipleDifferentValues && instance != null
                ? instance.ToString() : label;
            if (nativeHeader != null)
            {
                nativeHeader.style.display = resolvedStyle.display;
                nativeHeader.visible = visible;
                nativeHeader.Refresh(property, text, mixedTypes, property.editable && enabledSelf, tooltip);
            }

            if (initialized && displayedType == typeName && displayedId == id && displayedMixedTypes == mixedTypes)
            {
                return;
            }

            ReleaseDefaultBodyBindings();
            contentContainer.Unbind();
            contentContainer.Clear();
            displayedType = typeName;
            displayedId = id;
            displayedMixedTypes = mixedTypes;
            initialized = true;

            if (!mixedTypes && instance != null)
            {
                CreateBody(property);
            }

            // The host binds the initial tree. Later replacements bind from the Foldout so custom
            // drawers keep the parent property context for their relative binding paths.
            if (rebind)
            {
                // Bind is synchronous for an attached root. Restore the real native
                // Toggle for its menu lookup, then return it before the next layout.
                hierarchy.Insert(0, header);
                try
                {
                    this.Bind(serializedObject);
                }
                finally
                {
                    EnsureHeader();
                }
            }
        }

        private bool HasMixedTypes (SerializedProperty property)
        {
            if (!serializedObject.isEditingMultipleObjects || !property.hasMultipleDifferentValues)
            {
                return false;
            }

            string typeName = property.managedReferenceFullTypename;
            foreach (UnityEngine.Object target in serializedObject.targetObjects)
            {
                using (var individualObject = new SerializedObject(target))
                {
                    SerializedProperty individualProperty = individualObject.FindProperty(propertyPath);
                    if (individualProperty == null || individualProperty.managedReferenceFullTypename != typeName)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private void CreateBody (SerializedProperty property)
        {
            PropertyDrawer drawer = UnityPropertyDrawerUtility.GetPropertyDrawer(property, drawerNestingLevel);
            if (drawer != null)
            {
                // A dual drawer's OnGUI is the existing SubclassSelector contract. Use UITK for
                // UITK-only drawers, and limit the IMGUI bridge to an existing drawer's body.
                if (drawer.GetType().GetMethod(nameof(PropertyDrawer.OnGUI), new[] { typeof(Rect), typeof(SerializedProperty), typeof(GUIContent) }).DeclaringType == typeof(PropertyDrawer))
                {
                    var field = new PropertyField(property, text);
                    PropertyField host = GetFirstAncestorOfType<PropertyField>();
                    if (host != null)
                    {
                        // Initial construction is propagated by the host PropertyField. A body
                        // replaced later needs the same native parent context before binding.
                        UnityPropertyDrawerUtility.SetParentProperty(host, field);
                    }
                    Add(field);
                    return;
                }
                Add(new NativePropertyIMGUIField(property, text, drawerNestingLevel, this));
                return;
            }

            CreateDefaultBody(property);
        }

        private void CreateDefaultBody (SerializedProperty property)
        {
            body = new SubclassSelectorBody(property, this);
        }

        private void PrepareDefaultBodyBindings () => body?.PrepareBindings();
        private void ReleaseDefaultBodyBindings ()
        {
            body?.ReleaseBindings();
            body = null;
        }
#if UNITY_6000_0_OR_NEWER
        private void RefreshDefaultBody ()
        {
            body?.PrepareBindings();
            body?.Refresh();
        }
#else
        private void RefreshDefaultBody () => body?.Refresh();
#endif

        private void ShowTypePopup ()
        {
            if (panel == null || serializedObject.targetObject == null || !enabledInHierarchy
                || nativeHeader == null || !nativeHeader.TypeField.enabledInHierarchy)
            {
                return;
            }
            using SerializedProperty property = serializedObject.FindProperty(propertyPath);
            if (property == null || property.propertyType != SerializedPropertyType.ManagedReference)
            {
                return;
            }
            string fieldTypeName = property.managedReferenceFieldTypename;
            DeferredManagedReferenceTarget target = DeferredManagedReferenceTarget.Capture(property);
            if (target == null)
            {
                return;
            }
            Type baseType = ManagedReferenceUtility.GetType(fieldTypeName.Replace('/', '+'));
            var types = TypeSearchService.TypeCandiateService.GetDisplayableTypes(baseType);
            if (typePopup == null)
            {
                typePopup = new AdvancedTypePopup(types, 13, new AdvancedDropdownState());
            }
            else
            {
                typePopup.SetTypes(types);
            }
            typePopup.ShowForSelection(nativeHeader.TypeField.Input.worldBound, item => SelectType(target, item.Type));
        }

        private void SelectType (DeferredManagedReferenceTarget target, Type type)
        {
            // A popup can outlive its Inspector or ListView row.
            if (panel == null || serializedObject.targetObject == null)
            {
                return;
            }
            if (!ManagedReferenceSelection.SelectType(target, type, PrepareTypeChange))
            {
                return;
            }
            using (SerializedProperty property = serializedObject.FindProperty(propertyPath))
            {
                Refresh(property, true);
                NotifyValueChanged(property);
            }
        }

        private void PrepareTypeChange ()
        {
            ReleaseDefaultBodyBindings();
            contentContainer.Unbind();
            initialized = false;
        }

        private void NotifyValueChanged (SerializedProperty property)
        {
            // PropertyField turns custom-drawer ChangeEvents into its public value-change callback.
            using (var evt = ChangeEvent<SerializedProperty>.GetPooled(null, property))
            {
                evt.target = this;
                SendEvent(evt);
            }
        }
    }
}
