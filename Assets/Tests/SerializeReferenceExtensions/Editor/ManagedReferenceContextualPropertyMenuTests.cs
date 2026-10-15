using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Asset = MackySoft.SerializeReferenceExtensions.Tests.ManagedReferenceClipboardTestAsset;

namespace MackySoft.SerializeReferenceExtensions.Tests
{
    public sealed class ManagedReferenceContextualPropertyMenuTests
    {
        private const string PasteValues = "Paste Values Only";
        private const string PasteType = "Paste Type and Values";
        private Asset m_Source;
        private Asset m_Destination;
        private string m_AssetFolder;

        [SetUp]
        public void SetUp ()
        {
            Undo.ClearAll();
            m_Source = ScriptableObject.CreateInstance<Asset>();
            m_Destination = ScriptableObject.CreateInstance<Asset>();
            m_Source.reference = new Asset.SourceNode(23, "copied label");
            m_Destination.reference = new Asset.DestinationNode { shared = 3, destinationOnly = 91 };
            Execute(m_Source, "reference", CopyLabel("reference"));
        }

        [TearDown]
        public void TearDown ()
        {
            Undo.ClearAll();
            if (m_Source != null && !EditorUtility.IsPersistent(m_Source))
            {
                UnityEngine.Object.DestroyImmediate(m_Source);
            }
            if (m_Destination != null && !EditorUtility.IsPersistent(m_Destination))
            {
                UnityEngine.Object.DestroyImmediate(m_Destination);
            }
            if (!string.IsNullOrEmpty(m_AssetFolder))
            {
                Assert.That(AssetDatabase.DeleteAsset(m_AssetFolder), Is.True);
                Assert.That(AssetDatabase.IsValidFolder(m_AssetFolder), Is.False);
                m_AssetFolder = null;
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PasteTypeAndValues_ReplacesNullOrExistingReferenceWithIndependentCopySnapshot (bool hasDestination)
        {
            var source = (Asset.SourceNode)m_Source.reference;
            if (!hasDestination)
            {
                m_Destination.reference = null;
                AssertEnabled(m_Destination, "reference", PasteValues, false);
            }
            source.shared = 100;
            source.payload.value = 101;
            source.numbers[0] = 102;

            Execute(m_Destination, "reference", PasteType);

            var pasted = m_Destination.reference as Asset.SourceNode;
            Assert.That(pasted, Is.Not.Null);
            Assert.That(pasted, Is.Not.SameAs(source));
            Assert.That(pasted.shared, Is.EqualTo(23));
            Assert.That(pasted.Label, Is.EqualTo("copied label"));
            Assert.That(pasted.payload.value, Is.EqualTo(24));
            Assert.That(pasted.numbers, Is.EqualTo(new[] { 25 }));
            Assert.That(pasted.payload, Is.Not.SameAs(source.payload));
            Assert.That(pasted.numbers, Is.Not.SameAs(source.numbers));

            pasted.payload.value = 200;
            Execute(m_Destination, "parent.reference", PasteType);
            Assert.That(((Asset.SourceNode)m_Destination.parent.reference).payload.value, Is.EqualTo(24));
            Assert.That(source.payload.value, Is.EqualTo(101));
        }

        [Test]
        public void PasteValuesOnly_AcrossIncompatibleTypes_PreservesDestinationTypeIdentityAndUnmatchedFields ()
        {
            var destination = new Asset.AlternateNode { shared = 3, destinationOnly = 91 };
            m_Destination.alternate = destination;
            using (var serialized = new SerializedObject(m_Destination))
            {
                SerializedProperty property = serialized.FindProperty("alternate");
                long referenceId = property.managedReferenceId;
                GenericMenu menu = SerializedPropertyContextMenu.Open(property);
                Assert.That(SerializedPropertyContextMenu.IsEnabled(menu, PasteType), Is.False);
                SerializedPropertyContextMenu.Execute(menu, PasteValues);
                Assert.That(m_Destination.alternate, Is.SameAs(destination));
                Assert.That(destination.shared, Is.EqualTo(23));
                Assert.That(destination.destinationOnly, Is.EqualTo(91));
                Assert.That(serialized.FindProperty("alternate").managedReferenceId, Is.EqualTo(referenceId));
            }
        }

        [Test]
        public void CopiedNull_EnablesTypedClearAndDisablesValuesOnly ()
        {
            m_Source.reference = null;
            Execute(m_Source, "reference", CopyLabel("reference"));
            AssertEnabled(m_Destination, "reference", PasteValues, false);
            Execute(m_Destination, "reference", PasteType);
            Assert.That(m_Destination.reference, Is.Null);
        }

        [Test]
        public void MissingSourceType_DisablesCopyAndStaleCopyPreservesPreviousClipboard ()
        {
            CreateAssetFolder();
            m_Source.parent.reference = new Asset.DestinationNode { shared = 71 };
            string path = m_AssetFolder + "/Source.asset";
            AssetDatabase.CreateAsset(m_Source, path);
            AssetDatabase.SaveAssets();
            using (var serialized = new SerializedObject(m_Source))
            {
                GenericMenu staleMenu = SerializedPropertyContextMenu.Open(serialized.FindProperty("reference"));
                Assert.That(SerializedPropertyContextMenu.IsEnabled(staleMenu, CopyLabel("reference")), Is.True);

                // A saved asset whose source type disappeared reproduces Unity's retained missing
                // reference state without manufacturing the package's clipboard representation.
                string savedAsset = File.ReadAllText(path);
                string missingTypeAsset = savedAsset.Replace(nameof(Asset.SourceNode), "UnavailableClipboardSourceNode");
                Assert.That(missingTypeAsset, Is.Not.EqualTo(savedAsset), "The fixture must replace the saved source type.");
                File.WriteAllText(path, missingTypeAsset);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                serialized.Update();
                Assert.That(SerializationUtility.HasManagedReferencesWithMissingTypes(m_Source), Is.True);
                using (var freshSerialized = new SerializedObject(m_Source))
                {
                    SerializedProperty freshProperty = freshSerialized.FindProperty("reference");
                    Assert.That(freshProperty.managedReferenceValue, Is.Null);
                    GenericMenu freshMenu = SerializedPropertyContextMenu.Open(freshProperty);
                    Assert.That(SerializedPropertyContextMenu.IsEnabled(freshMenu, CopyLabel("reference")), Is.False);
                    GenericMenu nullSiblingMenu = SerializedPropertyContextMenu.Open(freshSerialized.FindProperty("alternate"));
                    Assert.That(SerializedPropertyContextMenu.IsEnabled(nullSiblingMenu, CopyLabel("alternate")), Is.False);
                    GenericMenu availableSiblingMenu = SerializedPropertyContextMenu.Open(freshSerialized.FindProperty("parent.reference"));
                    Assert.That(SerializedPropertyContextMenu.IsEnabled(availableSiblingMenu, CopyLabel("parent.reference")), Is.True);
                }
                SerializedPropertyContextMenu.Execute(staleMenu, CopyLabel("reference"));
            }

            Execute(m_Destination, "reference", PasteType);
            Assert.That(m_Destination.reference, Is.TypeOf<Asset.SourceNode>());
            Assert.That(((Asset.SourceNode)m_Destination.reference).shared, Is.EqualTo(23));
        }

        [Test]
        public void HiddenTypeWithoutPublicDefaultConstructor_CanBePastedIntoAssignableField ()
        {
            m_Source.reference = Asset.HiddenNode.Create(42);
            Execute(m_Source, "reference", CopyLabel("reference"));
            Execute(m_Destination, "reference", PasteType);
            Assert.That(m_Destination.reference, Is.TypeOf<Asset.HiddenNode>());
            Assert.That(((Asset.HiddenNode)m_Destination.reference).value, Is.EqualTo(42));
            Assert.That(m_Destination.reference, Is.Not.SameAs(m_Source.reference));
        }

        [Test]
        public void ReferenceInsideOrdinarySerializableParent_CanBeCopiedAndPasted ()
        {
            m_Source.parent.reference = new Asset.SourceNode(51, "nested");
            Execute(m_Source, "parent.reference", CopyLabel("parent.reference"));
            Execute(m_Destination, "parent.reference", PasteType);
            var pasted = m_Destination.parent.reference as Asset.SourceNode;
            Assert.That(pasted, Is.Not.Null);
            Assert.That(pasted.shared, Is.EqualTo(51));
            Assert.That(pasted.Label, Is.EqualTo("nested"));
            Assert.That(pasted, Is.Not.SameAs(m_Source.parent.reference));
        }

        [Test]
        public void IndividualCollectionElement_CopyAndPasteChangesOnlySelectedElement ()
        {
            m_Source.elements.Add(new Asset.SourceNode(64, "element"));
            var untouched = new Asset.DestinationNode { shared = 7, destinationOnly = 8 };
            m_Destination.elements.Add(untouched);
            m_Destination.elements.Add(null);
            Execute(m_Source, "elements.Array.data[0]", CopyLabel("elements.Array.data[0]"));
            Execute(m_Destination, "elements.Array.data[1]", PasteType);
            Assert.That(m_Destination.elements.Count, Is.EqualTo(2));
            Assert.That(m_Destination.elements[0], Is.SameAs(untouched));
            Assert.That(untouched.shared, Is.EqualTo(7));
            var pasted = m_Destination.elements[1] as Asset.SourceNode;
            Assert.That(pasted, Is.Not.Null);
            Assert.That(pasted.shared, Is.EqualTo(64));
            Assert.That(pasted.Label, Is.EqualTo("element"));
            Assert.That(pasted, Is.Not.SameAs(m_Source.elements[0]));
        }

        [Test]
        public void OrdinaryParentWholeCollectionAndScalar_DoNotOfferManagedReferenceCommands ()
        {
            using (var serialized = new SerializedObject(m_Destination))
            {
                foreach (string path in new[] { "parent", "elements", "unrelated" })
                {
                    GenericMenu menu = SerializedPropertyContextMenu.Open(serialized.FindProperty(path));
                    Assert.That(SerializedPropertyContextMenu.Contains(menu, CopyLabel(path)), Is.False, path);
                    Assert.That(SerializedPropertyContextMenu.Contains(menu, PasteType), Is.False, path);
                    Assert.That(SerializedPropertyContextMenu.Contains(menu, PasteValues), Is.False, path);
                }
            }
        }

        [Test]
        public void MultipleSelectedTargets_DisableCopyAndBothPasteModes ()
        {
            using (var serialized = new SerializedObject(new UnityEngine.Object[] { m_Source, m_Destination }))
            {
                GenericMenu menu = SerializedPropertyContextMenu.Open(serialized.FindProperty("reference"));
                Assert.That(SerializedPropertyContextMenu.IsEnabled(menu, CopyLabel("reference")), Is.False);
                Assert.That(SerializedPropertyContextMenu.IsEnabled(menu, PasteValues), Is.False);
                Assert.That(SerializedPropertyContextMenu.IsEnabled(menu, PasteType), Is.False);
            }
        }

        [TestCase(PasteValues)]
        [TestCase(PasteType)]
        public void CopyAndPaste_ApplyPendingInspectorEditsWithoutLosingUnrelatedValues (string command)
        {
            using (var serialized = new SerializedObject(m_Source))
            {
                serialized.FindProperty("reference.shared").intValue = 77;
                GenericMenu menu = SerializedPropertyContextMenu.Open(serialized.FindProperty("reference"));
                SerializedPropertyContextMenu.Execute(menu, CopyLabel("reference"));
            }
            using (var serialized = new SerializedObject(m_Destination))
            {
                serialized.FindProperty("unrelated").intValue = 88;
                GenericMenu menu = SerializedPropertyContextMenu.Open(serialized.FindProperty("reference"));
                SerializedPropertyContextMenu.Execute(menu, command);
            }
            int shared = m_Destination.reference is Asset.SourceNode source ? source.shared : ((Asset.DestinationNode)m_Destination.reference).shared;
            Assert.That(shared, Is.EqualTo(77));
            Assert.That(m_Destination.unrelated, Is.EqualTo(88));
        }

        [TestCase(PasteValues)]
        [TestCase(PasteType)]
        public void Paste_CanBeUndoneRedoneAndSavedForAssetReload (string command)
        {
            CreateAssetFolder();
            string path = m_AssetFolder + "/Destination.asset";
            AssetDatabase.CreateAsset(m_Destination, path);
            AssetDatabase.SaveAssets();
            Undo.ClearAll();
            Undo.IncrementCurrentGroup();

            Execute(m_Destination, "reference", command);
            Undo.FlushUndoRecordObjects();
            AssertPastedState(command);
            Undo.PerformUndo();
            Assert.That(m_Destination.reference, Is.TypeOf<Asset.DestinationNode>());
            Assert.That(((Asset.DestinationNode)m_Destination.reference).shared, Is.EqualTo(3));
            Assert.That(((Asset.DestinationNode)m_Destination.reference).destinationOnly, Is.EqualTo(91));
            Undo.PerformRedo();
            AssertPastedState(command);

            AssetDatabase.SaveAssets();
            Resources.UnloadAsset(m_Destination);
            m_Destination = AssetDatabase.LoadAssetAtPath<Asset>(path);
            Assert.That(m_Destination, Is.Not.Null);
            AssertPastedState(command);
        }

        private void AssertPastedState (string command)
        {
            if (command == PasteType)
            {
                Assert.That(m_Destination.reference, Is.TypeOf<Asset.SourceNode>());
                var node = (Asset.SourceNode)m_Destination.reference;
                Assert.That(node.shared, Is.EqualTo(23));
                Assert.That(node.Label, Is.EqualTo("copied label"));
                Assert.That(node.payload.value, Is.EqualTo(24));
            }
            else
            {
                Assert.That(m_Destination.reference, Is.TypeOf<Asset.DestinationNode>());
                var node = (Asset.DestinationNode)m_Destination.reference;
                Assert.That(node.shared, Is.EqualTo(23));
                Assert.That(node.destinationOnly, Is.EqualTo(91));
            }
        }

        private void CreateAssetFolder ()
        {
            m_AssetFolder = "Assets/__ManagedReferenceClipboardTests_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", m_AssetFolder.Substring("Assets/".Length));
        }

        private static string CopyLabel (string path)
        {
            return $"Copy \"{path}\" property";
        }

        private static void Execute (Asset target, string path, string command)
        {
            using (var serialized = new SerializedObject(target))
            {
                SerializedPropertyContextMenu.Execute(SerializedPropertyContextMenu.Open(serialized.FindProperty(path)), command);
            }
        }

        private static void AssertEnabled (Asset target, string path, string command, bool expected)
        {
            using (var serialized = new SerializedObject(target))
            {
                Assert.That(SerializedPropertyContextMenu.IsEnabled(SerializedPropertyContextMenu.Open(serialized.FindProperty(path)), command), Is.EqualTo(expected));
            }
        }
    }
}
