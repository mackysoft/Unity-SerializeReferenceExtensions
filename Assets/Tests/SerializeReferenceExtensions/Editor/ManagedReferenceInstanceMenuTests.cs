using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Asset = MackySoft.SerializeReferenceExtensions.Tests.SelectorTestAsset;

namespace MackySoft.SerializeReferenceExtensions.Tests
{
    public sealed class ManagedReferenceInstanceMenuTests
    {
        private const string NewInstance = "New Instance";
        private const string ResetInstance = "Reset and New Instance";
        private Asset target;
        private string assetPath;

        [SetUp]
        public void SetUp ()
        {
            target = ScriptableObject.CreateInstance<Asset>();
            Undo.ClearAll();
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
            else if (target != null)
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [TestCase(NewInstance)]
        [TestCase(ResetInstance)]
        public void NullReference_DisablesInstanceCommands (string command)
        {
            using (var serialized = new SerializedObject(target))
            {
                GenericMenu menu = SerializedPropertyContextMenu.Open(serialized.FindProperty("reference"));
                Assert.That(SerializedPropertyContextMenu.IsEnabled(menu, command), Is.False);
            }
        }

        [TestCase("reference")]
        [TestCase("parent.reference")]
        [TestCase("array.Array.data[1]")]
        [TestCase("list.Array.data[1]")]
        public void NewInstance_ReplacesOnlySelectedReferenceWithAnIndependentInstance (string path)
        {
            var original = new Asset.FirstNode(37, "original");
            var sibling = new Asset.FirstNode(52, "sibling");
            target.array[0] = sibling;
            target.list[0] = sibling;
            using (var serialized = new SerializedObject(target))
            {
                serialized.FindProperty(path).managedReferenceValue = original;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                SerializedPropertyContextMenu.Execute(SerializedPropertyContextMenu.Open(serialized.FindProperty(path)), NewInstance);
                serialized.Update();
                object replacement = serialized.FindProperty(path).managedReferenceValue;

                Assert.That(replacement, Is.TypeOf<Asset.FirstNode>());
                Assert.That(replacement, Is.Not.SameAs(original));
            }
            Assert.That(original.amount, Is.EqualTo(37));
            Assert.That(original.Title, Is.EqualTo("original"));
            Assert.That(target.array[0], Is.SameAs(sibling));
            Assert.That(target.list[0], Is.SameAs(sibling));
            Assert.That(target.unrelated, Is.EqualTo(81));
        }

        [Test]
        public void ResetInstance_RestoresInitialValuesAndSupportsUndoRedoAndAssetReload ()
        {
            target.reference = new Asset.FirstNode(67, "edited");
            assetPath = "Assets/__SelectorReset_" + Guid.NewGuid().ToString("N") + ".asset";
            AssetDatabase.CreateAsset(target, assetPath);
            AssetDatabase.SaveAssets();
            Undo.ClearAll();
            Undo.IncrementCurrentGroup();
            object original = target.reference;

            using (var serialized = new SerializedObject(target))
            {
                SerializedPropertyContextMenu.Execute(SerializedPropertyContextMenu.Open(serialized.FindProperty("reference")), ResetInstance);
            }
            Undo.FlushUndoRecordObjects();
            AssertResetState();
            Assert.That(target.reference, Is.Not.SameAs(original));

            Undo.PerformUndo();
            Assert.That(target.reference, Is.TypeOf<Asset.FirstNode>());
            Assert.That(((Asset.FirstNode)target.reference).amount, Is.EqualTo(67));
            Assert.That(((Asset.FirstNode)target.reference).Title, Is.EqualTo("edited"));
            Undo.PerformRedo();
            AssertResetState();

            AssetDatabase.SaveAssets();
            Resources.UnloadAsset(target);
            target = AssetDatabase.LoadAssetAtPath<Asset>(assetPath);
            Assert.That(target, Is.Not.Null);
            AssertResetState();
        }

        private void AssertResetState ()
        {
            Assert.That(target.reference, Is.TypeOf<Asset.FirstNode>());
            var node = (Asset.FirstNode)target.reference;
            Assert.That(node.amount, Is.EqualTo(7));
            Assert.That(node.Title, Is.EqualTo("first"));
            Assert.That(node.payload.value, Is.EqualTo(8));
            Assert.That(node.numbers, Is.EqualTo(new[] { 9 }));
            Assert.That(target.unrelated, Is.EqualTo(81));
        }
    }
}
