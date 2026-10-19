#if !UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MackySoft.SerializeReferenceExtensions.Editor
{
    // Own the default subtree without taking over a custom drawer's internal hierarchy.
    internal sealed partial class SubclassSelectorBody
    {
        private readonly List<Node> roots = new List<Node>();
        private readonly string rootPath;

        internal SubclassSelectorBody (SerializedProperty root, VisualElement container)
        {
            source = root.serializedObject;
            targets = source.targetObjects;
            rootPath = root.propertyPath;
            using (var pass = new SourcePass(this))
            {
                CreateChildren(root, root.managedReferenceValue.GetType(), null, container, roots, pass.Objects);
            }
        }

        internal void PrepareBindings ()
        {
            using (var pass = new SourcePass(this))
            {
                foreach (Node root in roots)
                {
                    root.Prepare(pass.Objects);
                }
            }
        }

        internal void Refresh ()
        {
            using (var pass = new SourcePass(this))
            {
                foreach (Node root in roots)
                {
                    root.Refresh(pass.Objects);
                }
            }
        }

        internal void ReleaseBindings ()
        {
            foreach (Node root in roots)
            {
                root.ReleaseBindings();
            }
        }

        private void CreateChildren (SerializedProperty property, Type type, Node parent, VisualElement container, List<Node> children, SerializedObject[] objects)
        {
            using (SerializedProperty child = property.Copy())
            using (SerializedProperty end = property.GetEndProperty())
            {
                bool enterChildren = true;
                while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
                {
                    FieldInfo field = GetField(type, child.name);
                    var node = new Node(this, parent, child.propertyPath, field, field?.FieldType, false);
                    children.Add(node);
                    node.Build(child, objects);
                    container.Add(node.View);
                    enterChildren = false;
                }
            }
        }

        private static FieldInfo GetField (Type type, string name)
        {
            for (; type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    return field;
                }
            }
            return null;
        }

        private struct Observation : IEquatable<Observation>
        {
            internal bool Exists;
            internal SerializedPropertyType Kind;
            internal string TypeName;
            internal long Id;
            internal int Size;

            internal static Observation Read (SerializedObject source, string path)
            {
                using (SerializedProperty property = source.FindProperty(path))
                {
                    if (property == null)
                    {
                        return default;
                    }
                    return new Observation
                    {
                        Exists = true,
                        Kind = property.propertyType,
                        TypeName = property.propertyType == SerializedPropertyType.ManagedReference ? property.managedReferenceFullTypename : null,
                        Id = property.propertyType == SerializedPropertyType.ManagedReference ? property.managedReferenceId : 0,
                        Size = property.isArray ? property.arraySize : -1
                    };
                }
            }

            public bool Equals (Observation other)
                => Exists == other.Exists && Kind == other.Kind && TypeName == other.TypeName && Id == other.Id && Size == other.Size;
        }

        private sealed class Node
        {
            private readonly SubclassSelectorBody owner;
            private readonly Node parent;
            private readonly string path;
            private readonly FieldInfo field;
            private readonly Type declaredType;
            private readonly bool isElement;
            private readonly List<Node> children = new List<Node>();
            private Observation[] observations;
            private VisualElement view;
            private Row row;
            private ListView collectionView;
            private IVisualElementScheduledItem rebuildRows;
            private bool monitorsShape;
            private bool reference;
            private bool dirty;
            private bool released;
            private bool disposed;
            private bool detached;

            internal VisualElement View => view;
            private bool UsesRowRoot => row != null && ReferenceEquals(view, row);

            internal Node (SubclassSelectorBody owner, Node parent, string path, FieldInfo field, Type declaredType, bool isElement)
            {
                this.owner = owner;
                this.parent = parent;
                this.path = path;
                this.field = field;
                this.declaredType = declaredType;
                this.isElement = isElement;
            }

            internal void Build (SerializedProperty property, SerializedObject[] objects)
                => Build(property, objects, null);

            private bool Build (SerializedProperty property, SerializedObject[] objects, Row row)
            {
                reference = property.propertyType == SerializedPropertyType.ManagedReference;
                bool selector = field != null && field.IsDefined(typeof(SubclassSelectorAttribute), true);
                bool collection = property.isArray && property.propertyType != SerializedPropertyType.String;
                monitorsShape = reference && !selector;
                observations = monitorsShape ? Capture(objects) : null;
                Type groupType = reference ? (CommonReferenceType() ? property.managedReferenceValue?.GetType() : null) : declaredType;
                // Unity's property context includes managed-reference base-drawer fallback;
                // a type-only lookup cannot decide whether this child owns a custom drawer.
                bool native = selector || field == null || property.isFixedBuffer
                    || (!collection && (UnityPropertyDrawerUtility.GetPropertyDrawer(property, 0) != null
                        || (!reference && property.propertyType != SerializedPropertyType.Generic)));
                if (row != null && !row.PrepareMode(native))
                {
                    return false;
                }
                this.row = row;
                dirty = false;
                released = false;

                if (native)
                {
                    view = CreateNative(property);
                }
                else if (collection)
                {
                    monitorsShape = true;
                    observations = Capture(objects);
                    view = CreateCollection(property);
                }
                else
                {
                    view = CreateGroup(property, groupType, objects);
                }
                view.RegisterCallback<DetachFromPanelEvent>(OnDetach);
                view.RegisterCallback<AttachToPanelEvent>(OnAttach);
                return true;
            }

            private Observation[] Capture (SerializedObject[] objects)
            {
                var result = new Observation[objects.Length];
                for (int i = 0; i < objects.Length; i++)
                {
                    result[i] = Observation.Read(objects[i], path);
                }
                return result;
            }

            private bool CommonReferenceType ()
            {
                if (observations == null || observations.Length == 0 || string.IsNullOrEmpty(observations[0].TypeName))
                {
                    return false;
                }
                foreach (Observation value in observations)
                {
                    if (!value.Exists || value.Kind != SerializedPropertyType.ManagedReference || value.TypeName != observations[0].TypeName)
                    {
                        return false;
                    }
                }
                return true;
            }

            private bool IsCycle (SerializedObject[] objects)
            {
                if (!reference || observations == null)
                {
                    return false;
                }
                for (int i = 0; i < observations.Length; i++)
                {
                    long id = observations[i].Id;
                    if (id < 0)
                    {
                        continue;
                    }
                    // A different target can change its root ID without recreating this body.
                    using (SerializedProperty root = objects[i].FindProperty(owner.rootPath))
                    {
                        if (root != null && id == root.managedReferenceId)
                        {
                            return true;
                        }
                    }
                    for (Node ancestor = parent; ancestor != null; ancestor = ancestor.parent)
                    {
                        if (ancestor.reference && ancestor.observations != null && ancestor.observations[i].Id == id)
                        {
                            return true;
                        }
                    }
                }
                return false;
            }

            private VisualElement CreateNative (SerializedProperty property)
            {
                PropertyField result = row ?? new PropertyField(property.Copy());
                if (row != null)
                {
                    result.bindingPath = property.propertyPath;
                }
                // An annotated scalar selector owns its decorators, and an array element
                // must never repeat the collection field's decorators.
                return isElement || (reference && field != null && field.IsDefined(typeof(SubclassSelectorAttribute), true))
                    ? result : DecoratorGUI.CreateGUI(result, property);
            }

            private VisualElement CreateGroup (SerializedProperty property, Type type, SerializedObject[] objects)
            {
                var group = new Foldout { bindingPath = path, text = property.displayName, tooltip = property.tooltip };
                NativePropertyFieldControls.ConfigureFoldout(group);
                if (type != null && !IsCycle(objects))
                {
                    owner.CreateChildren(property, type, this, group, children, objects);
                }
                return isElement ? group : DecoratorGUI.CreateOwnedGUI(group, property);
            }

            private VisualElement CreateCollection (SerializedProperty property)
            {
                bool reorderable = field == null || !field.IsDefined(typeof(NonReorderableAttribute), true);
                var list = new ListView
                {
                    bindingPath = path,
                    headerTitle = property.displayName,
                    tooltip = property.tooltip,
                    viewDataKey = "mackysoft-subclass-selector-" + path,
                    showBorder = true,
                    showAddRemoveFooter = true,
                    showBoundCollectionSize = true,
                    showFoldoutHeader = true,
                    virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
                    showAlternatingRowBackgrounds = AlternatingRowBackground.None,
                    reorderable = reorderable,
                    reorderMode = reorderable ? ListViewReorderMode.Animated : ListViewReorderMode.Simple
                };
                NativePropertyFieldControls.ConfigureListView(list);
                Type elementType = declaredType.IsArray ? declaredType.GetElementType() : declaredType.GetGenericArguments()[0];
                list.makeItem = () => new Row(this);
                list.bindItem = (element, index) =>
                {
                    var row = (Row)element;
                    row.Release();
                    if (disposed || dirty || list.itemsSource == null || index < 0 || index >= list.itemsSource.Count)
                    {
                        dirty = !disposed;
                        return;
                    }
                    if (!(list.itemsSource[index] is SerializedProperty item))
                    {
                        return;
                    }
                    string itemPath = item.propertyPath;
                    using (SerializedProperty current = owner.source.FindProperty(itemPath))
                    {
                        if (current == null)
                        {
                            dirty = true;
                            return;
                        }
                        using (var pass = new SourcePass(owner))
                        {
                            row.Assign(new Node(owner, this, itemPath, field, elementType, true), current, pass.Objects);
                        }
                    }
                };
                list.unbindItem = (element, index) => ((Row)element).Release();
                list.destroyItem = element => ((Row)element).Release();
                list.itemsAdded += OnItemsChanged;
                list.itemsRemoved += OnItemsChanged;
                list.itemIndexChanged += OnItemIndexChanged;
                collectionView = list;
                return DecoratorGUI.CreateOwnedGUI(list, property);
            }

            private void OnItemsChanged (IEnumerable<int> indices) => InvalidateRows();
            private void OnItemIndexChanged (int sourceIndex, int destinationIndex) => InvalidateRows();

            private void RequestRowsRebuild ()
            {
                if (disposed || dirty || collectionView == null || rebuildRows != null)
                {
                    return;
                }
                ListView list = collectionView;
                // Bind/unbind callbacks can be traversing the active rows. Let the
                // public controller recreate their native hosts after that traversal.
                rebuildRows = list.schedule.Execute(() =>
                {
                    rebuildRows = null;
                    if (!disposed && !dirty && collectionView == list)
                    {
                        list.Rebuild();
                    }
                });
            }

            private void InvalidateRows ()
            {
                dirty = true;
                // The controller still owns its current itemsSource during this callback.
                // Stop row binding before its immediate refresh; release the list later.
                foreach (Node child in children.ToArray())
                {
                    child.ReleaseBindings();
                }
            }

            internal void Prepare (SerializedObject[] objects)
            {
                if (disposed)
                {
                    return;
                }
                if (!dirty && monitorsShape)
                {
                    for (int i = 0; i < objects.Length; i++)
                    {
                        if (!observations[i].Equals(Observation.Read(objects[i], path)))
                        {
                            dirty = true;
                            break;
                        }
                    }
                }
                if (dirty)
                {
                    ReleaseBindings();
                    children.Clear();
                    if (UsesRowRoot)
                    {
                        row.RequestRebuild();
                    }
                    else
                    {
                        view.Clear();
                    }
                    return;
                }
                for (int i = 0; i < children.Count; i++)
                {
                    children[i].Prepare(objects);
                }
            }

            internal void Refresh (SerializedObject[] objects)
            {
                if (disposed || view.panel == null)
                {
                    return;
                }
                if (dirty)
                {
                    if (UsesRowRoot)
                    {
                        return;
                    }
                    ReleaseBindings();
                    children.Clear();
                    VisualElement previous = view;
                    VisualElement container = previous.parent;
                    int index = container.IndexOf(previous);
                    using (SerializedProperty property = owner.source.FindProperty(path))
                    {
                        if (property == null)
                        {
                            return;
                        }
                        if (!Build(property, objects, row))
                        {
                            return;
                        }
                    }
                    previous.UnregisterCallback<DetachFromPanelEvent>(OnDetach);
                    previous.UnregisterCallback<AttachToPanelEvent>(OnAttach);
                    previous.RemoveFromHierarchy();
                    container.Insert(index, view);
                    view.Bind(owner.source);
                }
                for (int i = 0; i < children.Count; i++)
                {
                    children[i].Refresh(objects);
                }
            }

            internal void ReleaseBindings ()
            {
                dirty = true;
                rebuildRows?.Pause();
                rebuildRows = null;
                if (released)
                {
                    return;
                }
                released = true;
                if (collectionView != null)
                {
                    collectionView.itemsAdded -= OnItemsChanged;
                    collectionView.itemsRemoved -= OnItemsChanged;
                    collectionView.itemIndexChanged -= OnItemIndexChanged;
                    collectionView = null;
                }
                foreach (Node child in children.ToArray())
                {
                    child.ReleaseBindings();
                }
                // A bound root can keep descendant trackers during an ordinary rebind.
                // This subtree is being discarded, so release every registration first.
                UnbindSubtree(view);
            }

            private void Destroy ()
            {
                disposed = true;
                ReleaseBindings();
                children.Clear();
                view.UnregisterCallback<DetachFromPanelEvent>(OnDetach);
                view.UnregisterCallback<AttachToPanelEvent>(OnAttach);
                if (!UsesRowRoot)
                {
                    view.RemoveFromHierarchy();
                }
            }

            private void OnDetach (DetachFromPanelEvent evt)
            {
                if (evt.target == view)
                {
                    detached = true;
                    ReleaseBindings();
                }
            }

            private void OnAttach (AttachToPanelEvent evt)
            {
                if (evt.target == view && detached && !disposed)
                {
                    detached = false;
                    released = false;
                    dirty = true;
                }
            }

            private sealed class Row : PropertyField
            {
                private readonly Node collection;
                private Node current;
                private bool? nativeMode;

                internal Row (Node collection)
                {
                    this.collection = collection;
                }

                internal void Assign (Node node, SerializedProperty property, SerializedObject[] objects)
                {
                    if (!node.Build(property, objects, this))
                    {
                        return;
                    }
                    current = node;
                    collection.children.Add(node);
                    if (!ReferenceEquals(node.View, this))
                    {
                        Add(node.View);
                    }
                    node.View.Bind(collection.owner.source);
                }

                internal bool PrepareMode (bool native)
                {
                    if (nativeMode.HasValue && nativeMode.Value != native)
                    {
                        // A native host can retain scheduled notifications and child
                        // caches. A different renderer needs a fresh host, not Clear().
                        RequestRebuild();
                        return false;
                    }
                    nativeMode = native;
                    return true;
                }

                internal void RequestRebuild () => collection.RequestRowsRebuild();

                internal void Release ()
                {
                    Node previous = current;
                    current = null;
                    if (previous != null)
                    {
                        collection.children.Remove(previous);
                        previous.Destroy();
                    }
                    if (nativeMode == true)
                    {
                        // Match native ListView unbinding: its PropertyField owns
                        // cached children and must be allowed to reuse them on Bind.
                        this.Unbind();
                        bindingPath = null;
                    }
                    else
                    {
                        Clear();
                    }
                }
            }
        }
    }
}
#endif
