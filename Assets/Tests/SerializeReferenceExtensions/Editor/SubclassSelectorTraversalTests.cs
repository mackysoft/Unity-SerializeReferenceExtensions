using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Asset = MackySoft.SerializeReferenceExtensions.Tests.TraversalTestAsset;

namespace MackySoft.SerializeReferenceExtensions.Tests
{
    public sealed class SubclassSelectorTraversalTests
    {
        private Asset target;
        private SelectorInspectorWindow window;

        [UnitySetUp]
        public IEnumerator SetUp ()
        {
            target = ScriptableObject.CreateInstance<Asset>();
            window = ScriptableObject.CreateInstance<SelectorInspectorWindow>();
            window.Initialize(target, "reference", true);
            window.ShowUtility();
            yield return null;
        }

        [TearDown]
        public void TearDown ()
        {
            if (window != null)
            {
                window.Close();
            }
            if (target != null)
            {
                Object.DestroyImmediate(target);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CyclicReferences_InspectorDrawsAndEditsVisibleChildren (bool mutual)
        {
            var first = new Asset.Cycle();
            var second = mutual ? new Asset.Cycle { visibleValue = 43 } : first;
            first.next = second;
            second.next = first;
            target.reference = first;
            SetExpanded("reference.next", false);

            InspectorDrawing.DrawnContent[] contents = InspectorDrawing.Capture(window);
            AssertChildrenFit(contents, "Visible Value", "Next", "Tail Value");
            ReplaceNumber(contents, "17", "42");

            Assert.That(((Asset.Cycle)target.reference).visibleValue, Is.EqualTo(42));
            Assert.That(((Asset.Cycle)target.reference).tailValue, Is.EqualTo(71));
            if (mutual)
            {
                Assert.That(((Asset.Cycle)target.reference).next.visibleValue, Is.EqualTo(43));
            }
            AssertChildrenFit(InspectorDrawing.Capture(window), "Visible Value", "Next", "Tail Value");
        }

        [Test]
        public void ChildControlResizesArray_UpdatesValuesAndFollowingLayout ()
        {
            target.reference = new Asset.CollectionRoot();
            InspectorDrawing.DrawnContent[] contents = InspectorDrawing.Capture(window);
            Rect resize = contents.Single(item => item.Text == TraversalCollectionDrawer.ResizeButton).Rect;
            window.Focus();
            Click(resize.center);

            var value = (Asset.CollectionRoot)target.reference;
            Assert.That(value.collection.values, Has.Length.EqualTo(3));
            Assert.That(value.collection.values[0], Is.EqualTo(31));
            Assert.That(value.visibleValue, Is.EqualTo(17));
            Assert.That(value.tailValue, Is.EqualTo(71));
            AssertChildrenFit(InspectorDrawing.Capture(window), "Visible Value", "Collection", TraversalCollectionDrawer.ResizeButton, "Tail Value");
        }

        private void SetExpanded (string path, bool expanded)
        {
            using (var serialized = new SerializedObject(target))
            using (SerializedProperty property = serialized.FindProperty(path))
            {
                property.isExpanded = expanded;
            }
        }

        private static void AssertChildrenFit (InspectorDrawing.DrawnContent[] contents, params string[] labels)
        {
            Rect following = contents.Single(item => item.Text == SelectorInspectorWindow.FollowingLabel).Rect;
            foreach (string label in labels)
            {
                Rect child = contents.Single(item => item.Text == label).Rect;
                Assert.That(child.yMax, Is.LessThanOrEqualTo(following.yMin), label + " must fit before the following field.");
            }
        }

        private void Click (Vector2 position)
        {
            window.SendEvent(new Event { type = EventType.MouseDown, mousePosition = position, button = 0 });
            window.SendEvent(new Event { type = EventType.MouseUp, mousePosition = position, button = 0 });
        }

        private void ReplaceNumber (InspectorDrawing.DrawnContent[] contents, string current, string replacement)
        {
            Rect input = contents.First(item => item.Text == current).Rect;
            window.Focus();
            Click(input.center);
            EventModifiers modifier = Application.platform == RuntimePlatform.OSXEditor ? EventModifiers.Command : EventModifiers.Control;
            window.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.A, modifiers = modifier });
            foreach (char character in replacement)
            {
                window.SendEvent(new Event { type = EventType.KeyDown, character = character });
            }
            window.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Return, character = '\n' });
        }
    }
}
