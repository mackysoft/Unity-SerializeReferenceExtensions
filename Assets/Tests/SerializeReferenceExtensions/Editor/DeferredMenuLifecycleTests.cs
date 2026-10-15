using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Asset = MackySoft.SerializeReferenceExtensions.Tests.DeferredReferenceTestAsset;

namespace MackySoft.SerializeReferenceExtensions.Tests
{
    public sealed class DeferredMenuLifecycleTests
    {
        private const string NewInstance = "New Instance";
        private const string ResetInstance = "Reset and New Instance";
        private const string PasteType = "Paste Type and Values";
        private const string PasteValues = "Paste Values Only";
        private Asset target;
        private Asset other;
        private Asset source;
        private string assetPath;

        [SetUp]
        public void SetUp ()
        {
            Undo.ClearAll();
            assetPath = null;
            target = ScriptableObject.CreateInstance<Asset>();
            other = ScriptableObject.CreateInstance<Asset>();
            source = ScriptableObject.CreateInstance<Asset>();
            source.reference = new Asset.Alpha(31);
            using (var serialized = new SerializedObject(source))
            {
                SerializedPropertyContextMenu.Execute(Open(serialized, "reference"), CopyLabel("reference"));
            }
        }

        [TearDown]
        public void TearDown ()
        {
            Undo.ClearAll();
            if (!string.IsNullOrEmpty(assetPath))
            {
                Assert.That(AssetDatabase.DeleteAsset(assetPath), Is.True);
            }
            else if (target != null)
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
            if (other != null)
            {
                UnityEngine.Object.DestroyImmediate(other);
            }
            if (source != null)
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void OpeningAndCancellingMenu_PreservesCallerEditsWithoutReplacingReference ()
        {
            Asset.INode original = target.reference;
            using (var serialized = new SerializedObject(target))
            {
                StageEdits(serialized);
                Open(serialized, "reference");
                Assert.That(target.reference, Is.SameAs(original));
                serialized.ApplyModifiedProperties();
                AssertAlpha(target.reference, 93, 94);
                Assert.That(target.unrelated, Is.EqualTo(88));
            }
        }

        [TestCase(NewInstance, false)]
        [TestCase(NewInstance, true)]
        [TestCase(ResetInstance, false)]
        [TestCase(ResetInstance, true)]
        public void InstanceCommand_PreservesEditsStagedBeforeOrAfterOpening (string command, bool afterOpening)
        {
            Asset.INode original = target.reference;
            using (var serialized = new SerializedObject(target))
            {
                if (!afterOpening)
                {
                    StageEdits(serialized);
                }
                GenericMenu menu = Open(serialized, "reference");
                if (afterOpening)
                {
                    StageEdits(serialized);
                }
                SerializedPropertyContextMenu.Execute(menu, command);
                Assert.That(target.reference, Is.Not.SameAs(original));
                AssertAlpha(target.reference, command == NewInstance ? 93 : 7, command == NewInstance ? 94 : 8);
                Assert.That(target.unrelated, Is.EqualTo(88));
                using (SerializedProperty property = serialized.FindProperty("unrelated"))
                {
                    Assert.That(property.intValue, Is.EqualTo(88), "The caller's wrapper remains usable and refreshed.");
                }
            }
        }

        [TestCase("elements.Array.data[1]", 23)]
        [TestCase("parent.reference", 17)]
        [TestCase("inlineItems.Array.data[0].reference", 19)]
        [TestCase("slots.Array.data[0].reference", 41)]
        [TestCase("inherited.reference", 19)]
        public void UnchangedNestedTarget_NewInstanceReplacesOnlyItsValue (string path, int amount)
        {
            using (var serialized = new SerializedObject(target))
            {
                Asset.INode original = ValueAt(serialized, path);
                SerializedPropertyContextMenu.Execute(Open(serialized, path), NewInstance);
                Asset.INode replacement = ValueAt(serialized, path);
                Assert.That(replacement, Is.Not.SameAs(original));
                AssertAlpha(replacement, amount, amount + 1);
            }
            Assert.That(target.unrelated, Is.EqualTo(81));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UnchangedNullCollectionOrInlinePrefix_AcceptsPaste (bool nullInlinePrefix)
        {
            string path = "elements.Array.data[0]";
            if (nullInlinePrefix)
            {
                target.parent = null;
                path = "parent.reference";
            }
            using (var serialized = new SerializedObject(target))
            {
                SerializedPropertyContextMenu.Execute(Open(serialized, path), PasteType);
                AssertAlpha(ValueAt(serialized, path), 31, 32);
            }
            Assert.That(target.elements[2], Is.Null);
            Assert.That(target.unrelated, Is.EqualTo(81));
        }

        [TestCase(NewInstance)]
        [TestCase(ResetInstance)]
        [TestCase(PasteValues)]
        [TestCase(PasteType)]
        public void ReorderedReferences_DeferredEditCancelsWithoutEditingEitherElement (string command)
        {
            Asset.INode first = target.elements[1];
            Asset.INode second = target.elements[3];
            using (var serialized = new SerializedObject(target))
            {
                GenericMenu menu = Open(serialized, "elements.Array.data[1]");
                target.elements[1] = second;
                target.elements[3] = first;
                SerializedPropertyContextMenu.Execute(menu, command);
            }
            Assert.That(target.elements[1], Is.SameAs(second));
            Assert.That(target.elements[3], Is.SameAs(first));
            AssertAlpha(first, 23, 24);
            Assert.That(((Asset.Beta)second).amount, Is.EqualTo(47));
            Assert.That(target.unrelated, Is.EqualTo(81));
        }

        [TestCase("removed element")]
        [TestCase("replacement collection")]
        [TestCase("inline ancestor")]
        [TestCase("managed ancestor")]
        [TestCase("equal target")]
        [TestCase("changed type")]
        [TestCase("inline reorder")]
        [TestCase("value reorder")]
        public void LostCorrespondence_NewInstanceSafelyCancels (string change)
        {
            string path = "reference";
            Action mutate;
            Action assertRetained;
            switch (change)
            {
                case "removed element":
                    path = "elements.Array.data[3]";
                    Asset.INode survivor = target.elements[1];
                    mutate = () => target.elements.RemoveAt(3);
                    assertRetained = () =>
                    {
                        Assert.That(target.elements, Has.Count.EqualTo(3));
                        Assert.That(target.elements[1], Is.SameAs(survivor));
                    };
                    break;
                case "replacement collection":
                    path = "elements.Array.data[1]";
                    var collection = new List<Asset.INode>(target.elements);
                    Asset.INode element = collection[1];
                    mutate = () => target.elements = collection;
                    assertRetained = () =>
                    {
                        Assert.That(target.elements, Is.SameAs(collection));
                        Assert.That(target.elements[1], Is.SameAs(element));
                    };
                    break;
                case "inline ancestor":
                    path = "parent.reference";
                    var inline = new Asset.Parent { reference = target.parent.reference };
                    Asset.INode inlineValue = inline.reference;
                    mutate = () => target.parent = inline;
                    assertRetained = () =>
                    {
                        Assert.That(target.parent, Is.SameAs(inline));
                        Assert.That(target.parent.reference, Is.SameAs(inlineValue));
                    };
                    break;
                case "managed ancestor":
                    path = "managedParent.reference";
                    var managed = new Asset.Parent { reference = target.managedParent.reference };
                    Asset.INode managedValue = managed.reference;
                    mutate = () => target.managedParent = managed;
                    assertRetained = () =>
                    {
                        Assert.That(target.managedParent, Is.SameAs(managed));
                        Assert.That(target.managedParent.reference, Is.SameAs(managedValue));
                    };
                    break;
                case "equal target":
                    var equal = new Asset.Alpha(17);
                    mutate = () => target.reference = equal;
                    assertRetained = () => Assert.That(target.reference, Is.SameAs(equal));
                    break;
                case "changed type":
                    var changedType = new Asset.Beta(47);
                    mutate = () => target.reference = changedType;
                    assertRetained = () => Assert.That(target.reference, Is.SameAs(changedType));
                    break;
                case "inline reorder":
                    path = "inlineItems.Array.data[0].reference";
                    Asset.Parent first = target.inlineItems[0];
                    Asset.Parent second = target.inlineItems[1];
                    Asset.INode secondValue = second.reference;
                    mutate = () => { target.inlineItems[0] = second; target.inlineItems[1] = first; };
                    assertRetained = () =>
                    {
                        Assert.That(target.inlineItems[0], Is.SameAs(second));
                        Assert.That(second.reference, Is.SameAs(secondValue));
                        AssertAlpha(second.reference, 29, 30);
                        Assert.That(target.inlineItems[1], Is.SameAs(first));
                    };
                    break;
                case "value reorder":
                    path = "slots.Array.data[0].reference";
                    var shared = new Asset.Alpha(41);
                    target.slots = new[] { new Asset.Slot { marker = 1, reference = shared }, new Asset.Slot { marker = 2, reference = shared } };
                    mutate = () => Array.Reverse(target.slots);
                    assertRetained = () =>
                    {
                        Assert.That(target.slots[0].marker, Is.EqualTo(2));
                        Assert.That(target.slots[1].marker, Is.EqualTo(1));
                        Assert.That(target.slots[0].reference, Is.SameAs(shared));
                        Assert.That(target.slots[1].reference, Is.SameAs(shared));
                    };
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(change), change, "Unknown correspondence change.");
            }
            using (var serialized = new SerializedObject(target))
            {
                GenericMenu menu = Open(serialized, path);
                mutate();
                SerializedPropertyContextMenu.Execute(menu, NewInstance);
            }
            assertRetained();
            Assert.That(target.unrelated, Is.EqualTo(81));
        }

        [Test]
        public void NullElementReorderCancellation_LeavesUnrelatedCallerBufferUnappliedAndIntact ()
        {
            Asset.INode first = target.elements[1];
            Asset.INode second = target.elements[3];
            using (var serialized = new SerializedObject(target))
            {
                GenericMenu menu = Open(serialized, "elements.Array.data[0]");
                using (SerializedProperty pending = serialized.FindProperty("unrelated"))
                {
                    pending.intValue = 105;
                }
                target.elements[1] = second;
                target.elements[3] = first;
                SerializedPropertyContextMenu.Execute(menu, PasteType);

                Assert.That(target.elements[0], Is.Null);
                Assert.That(target.elements[1], Is.SameAs(second));
                Assert.That(target.elements[3], Is.SameAs(first));
                Assert.That(target.unrelated, Is.EqualTo(81));
                Assert.That(serialized.hasModifiedProperties, Is.True);
                using (SerializedProperty pending = serialized.FindProperty("unrelated"))
                {
                    Assert.That(pending.intValue, Is.EqualTo(105));
                }
            }
        }

        [Test]
        public void StaleCopy_DoesNotReplaceThePreviousClipboardSnapshot ()
        {
            using (var serialized = new SerializedObject(target))
            {
                GenericMenu menu = Open(serialized, "elements.Array.data[1]");
                target.elements[1] = new Asset.Beta(66);
                SerializedPropertyContextMenu.Execute(menu, CopyLabel("elements.Array.data[1]"));
            }
            using (var serialized = new SerializedObject(other))
            {
                SerializedPropertyContextMenu.Execute(Open(serialized, "reference"), PasteType);
            }
            AssertAlpha(other.reference, 31, 32);
            Assert.That(((Asset.Beta)target.elements[1]).amount, Is.EqualTo(66));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DisposedOriginOrDestroyedHost_DeferredCommandCancels (bool destroyHost)
        {
            Asset.INode original = target.reference;
            var serialized = new SerializedObject(target);
            try
            {
                GenericMenu menu = Open(serialized, "reference");
                if (destroyHost)
                {
                    UnityEngine.Object.DestroyImmediate(target);
                }
                else
                {
                    serialized.Dispose();
                    serialized = null;
                }
                SerializedPropertyContextMenu.Execute(menu, NewInstance);
                if (!destroyHost)
                {
                    Assert.That(target.reference, Is.SameAs(original));
                }
                AssertAlpha(original, 17, 18);
                Assert.That(other.unrelated, Is.EqualTo(81));
            }
            finally
            {
                serialized?.Dispose();
            }
        }

        [TestCase(NewInstance)]
        [TestCase(ResetInstance)]
        public void HeterogeneousHosts_KeepTheirOwnTypeValuesAndIndependentInstances (string command)
        {
            target.reference = new Asset.Alpha(23);
            other.reference = new Asset.Beta(47);
            Asset.INode original = target.reference;
            Asset.INode otherOriginal = other.reference;
            using (var serialized = new SerializedObject(new UnityEngine.Object[] { target, other }))
            {
                using (SerializedProperty pending = serialized.FindProperty("unrelated"))
                {
                    pending.intValue = 88;
                }
                SerializedPropertyContextMenu.Execute(Open(serialized, "reference"), command);
            }
            Assert.That(target.reference, Is.Not.SameAs(original));
            Assert.That(other.reference, Is.Not.SameAs(otherOriginal));
            Assert.That(target.reference, Is.Not.SameAs(other.reference));
            AssertAlpha(target.reference, command == NewInstance ? 23 : 7, command == NewInstance ? 24 : 8);
            Assert.That(other.reference, Is.TypeOf<Asset.Beta>());
            Assert.That(((Asset.Beta)other.reference).amount, Is.EqualTo(command == NewInstance ? 47 : 11));
            Assert.That(((Asset.Beta)other.reference).payload.value, Is.EqualTo(command == NewInstance ? 48 : 12));
            Assert.That(target.unrelated, Is.EqualTo(88));
            Assert.That(other.unrelated, Is.EqualTo(88));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OneInvalidHost_CancelsTheWholeCommand (bool destroyHost)
        {
            Asset.INode original = target.reference;
            var replacement = new Asset.Beta(47);
            using (var serialized = new SerializedObject(new UnityEngine.Object[] { target, other }))
            {
                GenericMenu menu = Open(serialized, "reference");
                if (destroyHost)
                {
                    UnityEngine.Object.DestroyImmediate(other);
                }
                else
                {
                    other.reference = replacement;
                }
                SerializedPropertyContextMenu.Execute(menu, NewInstance);
            }
            Assert.That(target.reference, Is.SameAs(original));
            AssertAlpha(target.reference, 17, 18);
            if (!destroyHost)
            {
                Assert.That(other.reference, Is.SameAs(replacement));
            }
        }

        [Test]
        public void NewInstance_PersistsBufferedScalarAndChildValuesForAssetReload ()
        {
            assetPath = "Assets/__DeferredInstance_" + Guid.NewGuid().ToString("N") + ".asset";
            AssetDatabase.CreateAsset(target, assetPath);
            AssetDatabase.SaveAssets();
            Asset.INode original = target.reference;
            using (var serialized = new SerializedObject(target))
            {
                StageEdits(serialized);
                SerializedPropertyContextMenu.Execute(Open(serialized, "reference"), NewInstance);
            }
            Assert.That(target.reference, Is.Not.SameAs(original));
            AssetDatabase.SaveAssets();
            Resources.UnloadAsset(target);
            target = AssetDatabase.LoadAssetAtPath<Asset>(assetPath);
            Assert.That(target, Is.Not.Null);
            AssertAlpha(target.reference, 93, 94);
            Assert.That(target.unrelated, Is.EqualTo(88));
        }

        [TestCase("reference", 17)]
        [TestCase("parent.reference", 23)]
        public void OrdinaryHostOrInlineOwnerWithUnrelatedListApi_CanEditSerializedReference (string path, int amount)
        {
            var owner = ScriptableObject.CreateInstance<UnrelatedListReferenceTestAsset>();
            try
            {
                using (var serialized = new SerializedObject(owner))
                {
                    Asset.INode original = ValueAt(serialized, path);
                    SerializedPropertyContextMenu.Execute(Open(serialized, path), NewInstance);
                    Asset.INode replacement = ValueAt(serialized, path);
                    Assert.That(replacement, Is.Not.SameAs(original));
                    AssertAlpha(replacement, amount, amount + 1);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        private static void StageEdits (SerializedObject serialized)
        {
            using (SerializedProperty amount = serialized.FindProperty("reference.amount"))
            using (SerializedProperty child = serialized.FindProperty("reference.payload.value"))
            using (SerializedProperty unrelated = serialized.FindProperty("unrelated"))
            {
                amount.intValue = 93;
                child.intValue = 94;
                unrelated.intValue = 88;
            }
        }

        private static GenericMenu Open (SerializedObject serialized, string path)
        {
            using (SerializedProperty property = serialized.FindProperty(path))
            {
                return SerializedPropertyContextMenu.Open(property);
            }
        }

        private static Asset.INode ValueAt (SerializedObject serialized, string path)
        {
            using (SerializedProperty property = serialized.FindProperty(path))
            {
                return (Asset.INode)property.managedReferenceValue;
            }
        }

        private static string CopyLabel (string path) => "Copy \"" + path + "\" property";

        private static void AssertAlpha (Asset.INode value, int amount, int child)
        {
            Assert.That(value, Is.TypeOf<Asset.Alpha>());
            Assert.That(((Asset.Alpha)value).amount, Is.EqualTo(amount));
            Assert.That(((Asset.Alpha)value).payload.value, Is.EqualTo(child));
        }
    }
}
