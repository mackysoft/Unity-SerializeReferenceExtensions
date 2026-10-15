using System;
using MackySoft.SerializeReferenceExtensions.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Asset = MackySoft.SerializeReferenceExtensions.Tests.SelectorTestAsset;

namespace MackySoft.SerializeReferenceExtensions.Tests
{
    public sealed class ManagedReferenceSelectionTests
    {
        private Asset target;
        private string assetPath;

        [SetUp]
        public void SetUp ()
        {
            target = ScriptableObject.CreateInstance<Asset>();
        }

        [TearDown]
        public void TearDown ()
        {
            Undo.ClearAll();
            if (!string.IsNullOrEmpty(assetPath))
            {
                Assert.That(AssetDatabase.DeleteAsset(assetPath), Is.True);
                assetPath = null;
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void SelectingAConcreteTypeForNull_CreatesAnInstance ()
        {
            ChangeType("reference", typeof(Asset.FirstNode));

            Assert.That(target.reference, Is.TypeOf<Asset.FirstNode>());
            Assert.That(((Asset.FirstNode)target.reference).amount, Is.EqualTo(7));
            Assert.That(((Asset.FirstNode)target.reference).Title, Is.EqualTo("first"));
        }

        [Test]
        public void ChangingType_RestoresMatchingSerializedFields ()
        {
            var original = new Asset.FirstNode(29, "user edited");
            target.reference = original;

            ChangeType("reference", typeof(Asset.SecondNode));

            Assert.That(target.reference, Is.TypeOf<Asset.SecondNode>());
            var changed = (Asset.SecondNode)target.reference;
            Assert.That(changed.amount, Is.EqualTo(29));
            Assert.That(changed.Title, Is.EqualTo("user edited"));
            Assert.That(changed.payload.value, Is.EqualTo(30));
            Assert.That(changed.numbers, Is.EqualTo(new[] { 31 }));
            Assert.That(original.amount, Is.EqualTo(29));
            Assert.That(target.unrelated, Is.EqualTo(81));
        }

        [Test]
        public void TypeChange_CanBeUndoneRedoneAndSavedForReload ()
        {
            target.reference = new Asset.FirstNode(43, "saved selection");
            assetPath = "Assets/__SelectorSelection_" + Guid.NewGuid().ToString("N") + ".asset";
            AssetDatabase.CreateAsset(target, assetPath);
            AssetDatabase.SaveAssets();
            Undo.ClearAll();
            Undo.IncrementCurrentGroup();

            ChangeType("reference", typeof(Asset.SecondNode));
            Undo.FlushUndoRecordObjects();
            Assert.That(target.reference, Is.TypeOf<Asset.SecondNode>());
            Undo.PerformUndo();
            Assert.That(target.reference, Is.TypeOf<Asset.FirstNode>());
            Assert.That(((Asset.FirstNode)target.reference).amount, Is.EqualTo(43));
            Undo.PerformRedo();
            Assert.That(target.reference, Is.TypeOf<Asset.SecondNode>());

            AssetDatabase.SaveAssets();
            Resources.UnloadAsset(target);
            target = AssetDatabase.LoadAssetAtPath<Asset>(assetPath);
            Assert.That(target, Is.Not.Null);
            Assert.That(target.reference, Is.TypeOf<Asset.SecondNode>());
            Assert.That(((Asset.SecondNode)target.reference).amount, Is.EqualTo(43));
            Assert.That(((Asset.SecondNode)target.reference).Title, Is.EqualTo("saved selection"));
            Assert.That(target.unrelated, Is.EqualTo(81));
        }

        [TestCase("reference")]
        [TestCase("parent.reference")]
        [TestCase("array.Array.data[1]")]
        [TestCase("list.Array.data[1]")]
        public void SelectingAndClearingAProperty_ChangesOnlyThatReference (string path)
        {
            var untouched = new Asset.FirstNode(41, "untouched");
            target.array[0] = untouched;
            target.list[0] = untouched;

            ChangeType(path, typeof(Asset.SecondNode));
            using (var serialized = new SerializedObject(target))
            {
                Assert.That(serialized.FindProperty(path).managedReferenceValue, Is.TypeOf<Asset.SecondNode>());
            }
            ChangeType(path, null);

            using (var serialized = new SerializedObject(target))
            {
                Assert.That(serialized.FindProperty(path).managedReferenceValue, Is.Null);
            }
            Assert.That(target.array.Length, Is.EqualTo(2));
            Assert.That(target.list.Count, Is.EqualTo(2));
            Assert.That(target.array[0], Is.SameAs(untouched));
            Assert.That(target.list[0], Is.SameAs(untouched));
            Assert.That(untouched.amount, Is.EqualTo(41));
            Assert.That(target.unrelated, Is.EqualTo(81));
        }

        private void ChangeType (string path, Type type)
        {
            using (var serialized = new SerializedObject(target))
            {
                serialized.FindProperty(path).SetManagedReference(type);
                serialized.ApplyModifiedProperties();
            }
        }
    }
}
