using System;
using System.Collections;
using System.Linq;
using MackySoft.SerializeReferenceExtensions.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.TestTools;
using Asset = MackySoft.SerializeReferenceExtensions.Tests.DrawerContextTestAsset;

namespace MackySoft.SerializeReferenceExtensions.Tests
{
    public sealed class SubclassSelectorDrawerContextTests
    {
        private Asset[] targets;
        private DrawerContextTestEditor[] editors;
        private SelectorInspectorWindow[] windows;

        [UnitySetUp]
        public IEnumerator SetUp ()
        {
            ContextualNodeDrawer.DisposedContexts.Clear();
            targets = new Asset[2];
            editors = new DrawerContextTestEditor[2];
            windows = new SelectorInspectorWindow[2];
            for (int i = 0; i < targets.Length; i++)
            {
                targets[i] = ScriptableObject.CreateInstance<Asset>();
                targets[i].name = "Drawer context host " + i;
                targets[i].primary.amount += i * 200;
                targets[i].secondary.amount += i * 200;
                editors[i] = (DrawerContextTestEditor)UnityEditor.Editor.CreateEditor(targets[i]);
                windows[i] = ScriptableObject.CreateInstance<SelectorInspectorWindow>();
                windows[i].position = new Rect(100f + i * 20f, 100f + i * 20f, 600f, 500f);
                // InspectorElement supplies Unity's normal per-Editor handler context and ownership.
                windows[i].rootVisualElement.Add(new InspectorElement(editors[i]));
                windows[i].ShowUtility();
            }
            yield return null;
            yield return null;
        }

        [TearDown]
        public void TearDown ()
        {
            for (int i = 0; i < targets.Length; i++)
            {
                ReleaseEditor(i);
                if (windows[i] != null)
                {
                    windows[i].Close();
                }
                if (targets[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(targets[i]);
                }
            }
        }

        [Test]
        public void RegisteredCustomDrawer_UsesFieldMetadataAndIndependentHostFieldState ()
        {
            AssertContext(0, "primary", "Primary selector", 0);
            Click(0, "Edit Primary selector");
            Assert.That(targets[0].primary.amount, Is.EqualTo(108));
            Assert.That(targets[0].secondary.amount, Is.EqualTo(201));

            AssertContext(0, "secondary", "Secondary selector", 0);
            Click(0, "Edit Secondary selector");
            Assert.That(targets[0].secondary.amount, Is.EqualTo(214));
            Assert.That(targets[0].primary.amount, Is.EqualTo(108));

            AssertContext(1, "primary", "Primary selector", 0);
            Click(1, "Edit Primary selector");
            Assert.That(targets[1].primary.amount, Is.EqualTo(308));
            Assert.That(targets[1].secondary.amount, Is.EqualTo(401));
            AssertContext(0, "primary", "Primary selector", 1);
            AssertContext(0, "secondary", "Secondary selector", 1);
            AssertContext(1, "primary", "Primary selector", 1);
        }

        [Test]
        public void NativeEditorDisposal_ReleasesSelectorAndOrdinaryCustomDrawerResources ()
        {
            AssertContext(0, "native", "Native reference", 0);
            AssertContext(0, "primary", "Primary selector", 0);
            int hostId = targets[0].GetInstanceID();
            Assert.That(ContextualNodeDrawer.DisposedContexts.Any(context => context.TargetId == hostId && context.PropertyPath == "native"), Is.False);
            Assert.That(ContextualNodeDrawer.DisposedContexts.Any(context => context.TargetId == hostId && context.PropertyPath == "primary"), Is.False);

            ReleaseEditor(0);

            Assert.That(ContextualNodeDrawer.DisposedContexts.Any(context => context.TargetId == hostId && context.PropertyPath == "native"), Is.True);
            Assert.That(ContextualNodeDrawer.DisposedContexts.Any(context => context.TargetId == hostId && context.PropertyPath == "primary"), Is.True);
        }

        [Test]
        public void CustomDrawer_ForwardingTheSamePropertyReachesDefaultRendering ()
        {
            editors[0].PropertyPath = "forwarding";
            InspectorDrawing.DrawnContent[] contents = InspectorDrawing.Capture(windows[0]);
            Assert.That(contents.Select(item => item.Text), Does.Contain("Number"));
            Assert.That(contents.Select(item => item.Text), Does.Contain("29"));
            AssertFits(contents, "Number");
        }

        [Test]
        public void TypeOnlyLookup_ReturnsIndependentUnboundCallerOwnedDrawers ()
        {
            PropertyDrawer first = null;
            PropertyDrawer second = null;
            try
            {
                Assert.That(PropertyDrawerCache.TryGetPropertyDrawer(typeof(Asset.StatefulNode), out first), Is.True);
                Assert.That(PropertyDrawerCache.TryGetPropertyDrawer(typeof(Asset.StatefulNode), out second), Is.True);
                Assert.That(first, Is.TypeOf<ContextualNodeDrawer>());
                Assert.That(second, Is.TypeOf<ContextualNodeDrawer>());
                Assert.That(second, Is.Not.SameAs(first));
                Assert.That(first.fieldInfo, Is.Null);
                Assert.That(second.fieldInfo, Is.Null);

                ((IDisposable)first).Dispose();
                Assert.That(((ContextualNodeDrawer)second).IsDisposed, Is.False);
            }
            finally
            {
                (first as IDisposable)?.Dispose();
                (second as IDisposable)?.Dispose();
            }
        }

        private void AssertContext (int owner, string path, string caption, int edits)
        {
            editors[owner].PropertyPath = path;
            InspectorDrawing.DrawnContent[] contents = InspectorDrawing.Capture(windows[owner]);
            string[] labels = contents.Select(item => item.Text).ToArray();
            Assert.That(labels, Does.Contain(caption));
            Assert.That(labels, Does.Contain(caption + " edits: " + edits));
            Assert.That(labels, Does.Contain("Tooltip: " + DrawerContextTestEditor.Tooltip));
            AssertFits(contents, caption, caption + " edits: " + edits, "Edit " + caption);
        }

        private static void AssertFits (InspectorDrawing.DrawnContent[] contents, params string[] labels)
        {
            Rect following = contents.Single(item => item.Text == DrawerContextTestEditor.FollowingLabel).Rect;
            foreach (string label in labels)
            {
                Assert.That(contents.Single(item => item.Text == label).Rect.yMax, Is.LessThanOrEqualTo(following.yMin));
            }
        }

        private void Click (int owner, string text)
        {
            Rect button = InspectorDrawing.Capture(windows[owner]).Single(item => item.Text == text).Rect;
            windows[owner].Focus();
            windows[owner].SendEvent(new Event { type = EventType.MouseDown, mousePosition = button.center, button = 0 });
            windows[owner].SendEvent(new Event { type = EventType.MouseUp, mousePosition = button.center, button = 0 });
        }

        private void ReleaseEditor (int owner)
        {
            if (windows[owner] != null)
            {
                windows[owner].rootVisualElement.Clear();
            }
            if (editors[owner] != null)
            {
                UnityEngine.Object.DestroyImmediate(editors[owner]);
                editors[owner] = null;
            }
        }
    }
}
