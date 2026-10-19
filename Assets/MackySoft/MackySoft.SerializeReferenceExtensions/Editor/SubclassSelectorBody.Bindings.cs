using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace MackySoft.SerializeReferenceExtensions.Editor
{
    internal sealed partial class SubclassSelectorBody
    {
        private readonly SerializedObject source;
        private readonly UnityEngine.Object[] targets;

        private static void UnbindSubtree (VisualElement root)
        {
            var elements = new List<VisualElement>();
            CollectElements(root, elements);
            for (int i = elements.Count - 1; i >= 0; i--)
            {
                elements[i].Unbind();
            }
        }

        private static void CollectElements (VisualElement element, List<VisualElement> elements)
        {
            elements.Add(element);
            for (int i = 0; i < element.hierarchy.childCount; i++)
            {
                CollectElements(element.hierarchy[i], elements);
            }
        }

        private sealed class SourcePass : IDisposable
        {
            internal readonly SerializedObject[] Objects;
            private readonly bool ownsObjects;

            internal SourcePass (SubclassSelectorBody body)
            {
                ownsObjects = body.targets.Length > 1;
                Objects = new SerializedObject[body.targets.Length];
                for (int i = 0; i < Objects.Length; i++)
                {
                    Objects[i] = ownsObjects ? new SerializedObject(body.targets[i], body.source.context) : body.source;
                }
            }

            public void Dispose ()
            {
                if (ownsObjects)
                {
                    foreach (SerializedObject value in Objects)
                    {
                        value?.Dispose();
                    }
                }
            }
        }
    }
}
