#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace MackySoft.SerializeReferenceExtensions.Editor
{
    internal sealed partial class SubclassSelectorBody
    {
        private readonly List<NativeChild> children = new List<NativeChild>();
        private readonly string rootPath;

        internal SubclassSelectorBody (SerializedProperty root, VisualElement container)
        {
            source = root.serializedObject;
            targets = source.targetObjects;
            rootPath = root.propertyPath;
            using (var pass = new SourcePass(this))
            {
                using (SerializedProperty child = root.Copy())
                using (SerializedProperty end = root.GetEndProperty())
                {
                    bool enterChildren = true;
                    while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
                    {
                        var view = new NativeChild(this, child, pass.Objects);
                        children.Add(view);
                        container.Add(view.View);
                        enterChildren = false;
                    }
                }
            }
        }

        internal void PrepareBindings ()
        {
            using (var pass = new SourcePass(this))
            {
                foreach (NativeChild child in children)
                {
                    child.Prepare(pass.Objects);
                }
            }
        }

        internal void Refresh ()
        {
            using (var pass = new SourcePass(this))
            {
                foreach (NativeChild child in children)
                {
                    child.Refresh(pass.Objects);
                }
            }
        }

        internal void ReleaseBindings ()
        {
            foreach (NativeChild child in children)
            {
                child.ReleaseBindings();
            }
        }

        private struct ReferenceShape : IEquatable<ReferenceShape>
        {
            internal string Path;
            internal string TypeName;
            internal long Id;

            public bool Equals (ReferenceShape other) => Path == other.Path && TypeName == other.TypeName && Id == other.Id;
        }

        private sealed class Shape
        {
            private readonly bool exists;
            private readonly SerializedPropertyType kind;
            private readonly ReferenceShape[] references;

            private Shape (bool exists, SerializedPropertyType kind, ReferenceShape[] references)
            {
                this.exists = exists;
                this.kind = kind;
                this.references = references;
            }

            internal static Shape Read (SerializedObject source, string path, string rootPath)
            {
                using (SerializedProperty property = source.FindProperty(path))
                {
                    if (property == null)
                    {
                        return new Shape(false, default, Array.Empty<ReferenceShape>());
                    }
                    var references = new List<ReferenceShape>();
                    var visited = new HashSet<long>();
                    // Another target can replace its root while this body remains attached.
                    using (SerializedProperty root = source.FindProperty(rootPath))
                    {
                        if (root != null)
                        {
                            visited.Add(root.managedReferenceId);
                        }
                    }
                    SerializedPropertyType kind = property.propertyType;
                    // Include hidden serialized descendants: a custom drawer may expose them.
                    using (SerializedProperty end = property.GetEndProperty(true))
                    {
                        bool enterChildren;
                        do
                        {
                            enterChildren = true;
                            if (property.propertyType == SerializedPropertyType.ManagedReference)
                            {
                                var reference = new ReferenceShape
                                {
                                    Path = property.propertyPath,
                                    TypeName = property.managedReferenceFullTypename,
                                    Id = property.managedReferenceId
                                };
                                references.Add(reference);
                                enterChildren = !string.IsNullOrEmpty(reference.TypeName) && visited.Add(reference.Id);
                            }
                        }
                        while (property.Next(enterChildren) && !SerializedProperty.EqualContents(property, end));
                    }
                    return new Shape(true, kind, references.ToArray());
                }
            }

            internal bool Matches (Shape other)
            {
                if (exists != other.exists || kind != other.kind || references.Length != other.references.Length)
                {
                    return false;
                }
                for (int i = 0; i < references.Length; i++)
                {
                    if (!references[i].Equals(other.references[i]))
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        private sealed class NativeChild
        {
            private readonly SubclassSelectorBody owner;
            private readonly string path;
            private Shape[] shapes;
            private bool dirty;
            private bool released;
            private PropertyField view;

            internal VisualElement View => view;

            internal NativeChild (SubclassSelectorBody owner, SerializedProperty property, SerializedObject[] objects)
            {
                this.owner = owner;
                path = property.propertyPath;
                Build(property, objects);
            }

            private Shape[] Capture (SerializedObject[] objects)
            {
                var result = new Shape[objects.Length];
                for (int i = 0; i < result.Length; i++)
                {
                    result[i] = Shape.Read(objects[i], path, owner.rootPath);
                }
                return result;
            }

            private void Build (SerializedProperty property, SerializedObject[] objects)
            {
                shapes = Capture(objects);
                dirty = false;
                released = false;
                view = new PropertyField(property.Copy());
                view.RegisterCallback<DetachFromPanelEvent>(OnDetach);
            }

            internal void Prepare (SerializedObject[] objects)
            {
                if (!dirty)
                {
                    Shape[] current = Capture(objects);
                    for (int i = 0; i < current.Length; i++)
                    {
                        if (!shapes[i].Matches(current[i]))
                        {
                            ReleaseBindings();
                            break;
                        }
                    }
                }
                if (dirty)
                {
                    view.Clear();
                }
            }

            internal void Refresh (SerializedObject[] objects)
            {
                if (!dirty || view.panel == null)
                {
                    return;
                }
                PropertyField previous = view;
                VisualElement container = previous.parent;
                int index = container.IndexOf(previous);
                using (SerializedProperty property = owner.source.FindProperty(path))
                {
                    if (property == null)
                    {
                        return;
                    }
                    Build(property, objects);
                }
                previous.UnregisterCallback<DetachFromPanelEvent>(OnDetach);
                previous.RemoveFromHierarchy();
                container.Insert(index, view);
                view.Bind(owner.source);
            }

            internal void ReleaseBindings ()
            {
                dirty = true;
                if (!released)
                {
                    released = true;
                    UnbindSubtree(view);
                }
            }

            private void OnDetach (DetachFromPanelEvent evt)
            {
                if (evt.target == view)
                {
                    ReleaseBindings();
                }
            }
        }
    }
}
#endif
