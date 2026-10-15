using System.Collections;
using System.Linq;
using MackySoft.SerializeReferenceExtensions.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Asset = MackySoft.SerializeReferenceExtensions.Tests.SelectorTestAsset;

namespace MackySoft.SerializeReferenceExtensions.Tests
{
    public sealed class SubclassSelectorInspectorTests
    {
        private Asset target;
        private SelectorInspectorWindow window;
        private EditorWindow popup;

        [UnitySetUp]
        public IEnumerator SetUp ()
        {
            target = ScriptableObject.CreateInstance<Asset>();
            window = ScriptableObject.CreateInstance<SelectorInspectorWindow>();
            window.Initialize(target, "reference", false);
            window.ShowUtility();
            yield return null;
        }

        [TearDown]
        public void TearDown ()
        {
            if (popup != null)
            {
                popup.Close();
            }
            if (window != null)
            {
                window.Close();
            }
            if (target != null)
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [UnityTest]
        public IEnumerator DropdownSelection_ChangesTypePreservesValuesAndCanSelectNull ()
        {
            target.reference = new Asset.SecondNode { amount = 46 };
            OpenPopup("Renamed Node");
            TypePopupInteraction.SelectVisibleItem(popup, "First Node");
            yield return null;

            Assert.That(target.reference, Is.TypeOf<Asset.FirstNode>());
            Assert.That(((Asset.FirstNode)target.reference).amount, Is.EqualTo(46));
            Assert.That(((Asset.FirstNode)target.reference).Title, Is.EqualTo("second"));
            Assert.That(Text(), Does.Contain("First Node"));

            OpenPopup("First Node");
            TypePopupInteraction.SelectVisibleItem(popup, "<null>");
            yield return null;

            Assert.That(target.reference, Is.Null);
            Assert.That(Text(), Does.Contain("<null>"));
            Assert.That(target.unrelated, Is.EqualTo(81));
        }

        [Test]
        public void TypeLabel_UsesMenuLeafAndShowsNullAfterClearing ()
        {
            target.reference = new Asset.SecondNode();
            Assert.That(Text(), Does.Contain("Renamed Node"));
            Assert.That(Text(), Does.Contain(SelectorInspectorWindow.FieldLabel));

            using (var serialized = new SerializedObject(target))
            {
                serialized.FindProperty("reference").SetManagedReference(null);
                serialized.ApplyModifiedProperties();
            }
            Assert.That(Text(), Does.Contain("<null>"));
            Assert.That(Text(), Does.Not.Contain("Renamed Node"));
        }

        [Test]
        public void UseToStringAsLabel_DrawsCurrentInstanceTextAndFallsBackForNull ()
        {
            window.SetProperty("labelled", false);
            target.labelled = new Asset.FirstNode(20, "Player command");

            Assert.That(Text(), Does.Contain("Player command"));
            Assert.That(Text(), Does.Not.Contain(SelectorInspectorWindow.FieldLabel));

            using (var serialized = new SerializedObject(target))
            {
                serialized.FindProperty("labelled.m_Title").stringValue = "Edited command";
                serialized.ApplyModifiedProperties();
            }
            Assert.That(Text(), Does.Contain("Edited command"));
            Assert.That(Text(), Does.Not.Contain("Player command"));

            target.labelled = new Asset.FirstNode(30, "Updated command");
            Assert.That(Text(), Does.Contain("Updated command"));
            Assert.That(Text(), Does.Not.Contain("Player command"));

            target.labelled = null;
            Assert.That(Text(), Does.Contain(SelectorInspectorWindow.FieldLabel));
            Assert.That(Text(), Does.Contain("<null>"));
        }

        [TestCase("labelledArray")]
        [TestCase("labelledList")]
        public void CollectionElements_DrawTheirOwnInstanceLabels (string collection)
        {
            target.labelledArray[0] = new Asset.FirstNode(1, "First element");
            target.labelledArray[1] = new Asset.FirstNode(2, "Second element");
            target.labelledList[0] = new Asset.FirstNode(1, "First element");
            target.labelledList[1] = new Asset.FirstNode(2, "Second element");
            window.SetProperty(collection + ".Array.data[0]", false);
            Assert.That(Text(), Does.Contain("First element"));
            Assert.That(Text(), Does.Not.Contain("Second element"));

            window.SetProperty(collection + ".Array.data[1]", false);
            Assert.That(Text(), Does.Contain("Second element"));
            Assert.That(Text(), Does.Not.Contain("First element"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CustomSubtypeDrawer_RendersFitsAndEditsSelectedInstance (bool derived)
        {
            target.reference = derived ? new Asset.DerivedDrawnNode() : new Asset.DrawnNode();
            window.SetProperty("reference", true);
            InspectorDrawing.DrawnContent[] contents = InspectorDrawing.Capture(window);
            Assert.That(contents.Select(item => item.Text), Does.Contain(SelectorCustomPropertyDrawer.Marker));
            Assert.That(contents.Select(item => item.Text), Does.Contain(derived ? "Derived Drawn Node" : "Drawn Node"));
            Rect button = contents.First(item => item.Text == SelectorCustomPropertyDrawer.EditButton).Rect;
            Rect following = contents.First(item => item.Text == SelectorInspectorWindow.FollowingLabel).Rect;
            Assert.That(button.yMax, Is.LessThanOrEqualTo(following.yMin), "The custom drawer must fit before the following field.");

            window.SendEvent(new Event { type = EventType.MouseDown, mousePosition = button.center, button = 0 });
            window.SendEvent(new Event { type = EventType.MouseUp, mousePosition = button.center, button = 0 });
            Assert.That(((Asset.DrawnNode)target.reference).amount, Is.EqualTo(59));

            window.SetProperty("reference", false);
            Assert.That(Text(), Does.Not.Contain(SelectorCustomPropertyDrawer.Marker));
            Assert.That(Text(), Does.Not.Contain(SelectorCustomPropertyDrawer.EditButton));
        }

        [Test]
        public void ExpandedReference_DrawsSerializedChildrenBeforeFollowingField ()
        {
            target.reference = new Asset.FirstNode(34, "child fields");
            window.SetProperty("reference", true);
            InspectorDrawing.DrawnContent[] contents = InspectorDrawing.Capture(window);
            if (!contents.Any(item => item.Text == "Value"))
            {
                Rect payload = contents.First(item => item.Text == "Payload").Rect;
                window.SendEvent(new Event { type = EventType.MouseDown, mousePosition = payload.center, button = 0 });
                window.SendEvent(new Event { type = EventType.MouseUp, mousePosition = payload.center, button = 0 });
                contents = InspectorDrawing.Capture(window);
            }
            string[] labels = contents.Select(item => item.Text).ToArray();
            Assert.That(labels, Does.Contain("Amount"));
            Assert.That(labels, Does.Contain("Title"));
            Assert.That(labels, Does.Contain("Payload"));
            Assert.That(labels, Does.Contain("Value"));
            Assert.That(labels, Does.Contain("Numbers"));
            Rect following = contents.First(item => item.Text == SelectorInspectorWindow.FollowingLabel).Rect;
            foreach (string name in new[] { "Amount", "Title", "Payload", "Value", "Numbers" })
            {
                Assert.That(contents.First(item => item.Text == name).Rect.yMax, Is.LessThanOrEqualTo(following.yMin), name);
            }
        }

        private string[] Text ()
        {
            return InspectorDrawing.Capture(window).Select(item => item.Text).ToArray();
        }

        private void OpenPopup (string currentTypeLabel)
        {
            window.Focus();
            Rect button = InspectorDrawing.Capture(window).First(item => item.Text == currentTypeLabel).Rect;
            window.SendEvent(new Event { type = EventType.MouseDown, mousePosition = button.center, button = 0 });
            popup = Resources.FindObjectsOfTypeAll<EditorWindow>()
                .SingleOrDefault(candidate => candidate.GetType().Name == "AdvancedDropdownWindow");
            Assert.That(popup, Is.Not.Null, "Clicking the registered selector must open its dropdown.");
        }
    }
}
