using System.Collections.Generic;
using System.Linq;
using MackySoft.SerializeReferenceExtensions.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Asset = MackySoft.SerializeReferenceExtensions.Tests.TraversalTestAsset;

namespace MackySoft.SerializeReferenceExtensions.Tests
{
    public sealed class SerializedPropertyChildTraversalTests
    {
        private Asset target;

        [SetUp]
        public void SetUp ()
        {
            target = ScriptableObject.CreateInstance<Asset>();
        }

        [TearDown]
        public void TearDown ()
        {
            if (target != null)
            {
                Object.DestroyImmediate(target);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CyclicReferences_ReturnTheRequestedFiniteDepth (bool mutual)
        {
            var first = new Asset.Cycle();
            var second = mutual ? new Asset.Cycle() : first;
            first.next = second;
            second.next = first;
            target.reference = first;

            using (var serialized = new SerializedObject(target))
            using (SerializedProperty parent = serialized.FindProperty("reference"))
            using (SerializedProperty next = parent.FindPropertyRelative("next"))
            {
                parent.isExpanded = true;
                next.isExpanded = false;
                Assert.That(Paths(parent, 2), Is.EqualTo(new[]
                {
                    "reference.visibleValue", "reference.next", "reference.next.visibleValue",
                    "reference.next.next", "reference.next.tailValue", "reference.tailValue"
                }));
            }
        }

        [Test]
        public void DirectChildren_ClosedCollectionDoesNotEnterCyclicElements ()
        {
            var entries = new Asset.Cycle[128];
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = new Asset.Cycle();
                entry.next = entry;
                entries[i] = entry;
            }
            target.reference = new Asset.ClosedBranches { entries = entries };

            using (var serialized = new SerializedObject(target))
            using (SerializedProperty parent = serialized.FindProperty("reference"))
            using (SerializedProperty collection = parent.FindPropertyRelative("entries"))
            {
                parent.isExpanded = true;
                collection.isExpanded = false;
                Assert.That(Paths(parent), Is.EqualTo(new[]
                {
                    "reference.visibleValue", "reference.entries", "reference.tailValue"
                }));
            }
        }

        [Test]
        public void RequestedDepth_IncludesVisibleChildrenAndStopsBeforeGrandchildren ()
        {
            target.reference = new Asset.NestedRoot();
            using (var serialized = new SerializedObject(target))
            using (SerializedProperty parent = serialized.FindProperty("reference"))
            {
                Assert.That(Paths(parent, 2), Is.EqualTo(new[]
                {
                    "reference.visibleValue", "reference.branch", "reference.branch.value",
                    "reference.branch.leaf", "reference.tailValue"
                }));
            }
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void NonpositiveDepth_ReturnsNoChildren (int depth)
        {
            var cycle = new Asset.Cycle();
            cycle.next = cycle;
            target.reference = cycle;
            using (var serialized = new SerializedObject(target))
            using (SerializedProperty parent = serialized.FindProperty("reference"))
            {
                Assert.That(Paths(parent, depth), Is.Empty);
                Assert.That(parent.propertyPath, Is.EqualTo("reference"));
            }
        }

        [TestCase("array")]
        [TestCase("list")]
        public void ArrayParents_ReturnElementsWithoutTheirDescendants (string path)
        {
            using (var serialized = new SerializedObject(target))
            using (SerializedProperty parent = serialized.FindProperty(path))
            {
                Assert.That(Paths(parent, 3), Is.EqualTo(new[]
                {
                    path + ".Array.data[0]", path + ".Array.data[1]"
                }));
                Assert.That(parent.propertyPath, Is.EqualTo(path));
            }
        }

        [Test]
        public void ReturnedCopies_RemainIndependentAndPreserveParentPosition ()
        {
            target.reference = new Asset.NestedRoot();
            using (var serialized = new SerializedObject(target))
            using (SerializedProperty parent = serialized.FindProperty("reference"))
            {
                SerializedProperty[] children = parent.GetChildProperties().ToArray();
                try
                {
                    Assert.That(children.Select(child => child.propertyPath), Is.EqualTo(new[]
                    {
                        "reference.visibleValue", "reference.branch", "reference.tailValue"
                    }));
                    Assert.That(children[0].NextVisible(false), Is.True);
                    Assert.That(children[1].propertyPath, Is.EqualTo("reference.branch"));
                    Assert.That(children[2].propertyPath, Is.EqualTo("reference.tailValue"));
                    children[0].NextVisible(false);
                    Assert.That(children[1].propertyPath, Is.EqualTo("reference.branch"));
                    Assert.That(parent.propertyPath, Is.EqualTo("reference"));
                    Assert.That(parent.managedReferenceValue, Is.SameAs(target.reference));
                }
                finally
                {
                    foreach (SerializedProperty child in children)
                    {
                        child.Dispose();
                    }
                }

                using (IEnumerator<SerializedProperty> iterator = parent.GetChildProperties().GetEnumerator())
                {
                    Assert.That(iterator.MoveNext(), Is.True);
                    using (SerializedProperty first = iterator.Current)
                    {
                        Assert.That(first.propertyPath, Is.EqualTo("reference.visibleValue"));
                    }
                }
                Assert.That(parent.propertyPath, Is.EqualTo("reference"));
            }
        }

        private static string[] Paths (SerializedProperty parent, int depth = 1)
        {
            var paths = new List<string>();
            foreach (SerializedProperty child in parent.GetChildProperties(depth))
            {
                using (child)
                {
                    paths.Add(child.propertyPath);
                }
            }
            return paths.ToArray();
        }
    }
}
