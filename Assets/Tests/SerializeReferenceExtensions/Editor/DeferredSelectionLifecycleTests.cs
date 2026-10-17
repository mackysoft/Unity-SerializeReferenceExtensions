using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Asset = MackySoft.SerializeReferenceExtensions.Tests.DeferredReferenceTestAsset;

namespace MackySoft.SerializeReferenceExtensions.Tests
{
    public sealed class DeferredSelectionLifecycleTests
    {
        private Asset target;
        private Asset other;
        private SerializedObject caller;
        private DeferredSelectionWindow window;
        private EditorWindow popup;
        private string assetPath;

        [UnitySetUp]
        public IEnumerator SetUp ()
        {
            Undo.ClearAll();
            assetPath = null;
            target = ScriptableObject.CreateInstance<Asset>();
            other = ScriptableObject.CreateInstance<Asset>();
            caller = new SerializedObject(target);
            ShowWindow("reference");
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
            caller?.Dispose();
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
            Undo.ClearAll();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Selection_PreservesEditsStagedBeforeOrAfterOpening (bool afterOpening)
        {
            Asset.INode original = target.reference;
            if (!afterOpening)
            {
                StageEdits();
            }
            OpenPopup();
            Assert.That(window.Changed, Is.False);
            if (afterOpening)
            {
                StageEdits();
            }
            TypePopupInteraction.SelectVisibleItem(popup, "Deferred Beta");
            window.DrawNow();

            AssertBeta(target.reference, 93, 94);
            Assert.That(target.reference, Is.Not.SameAs(original));
            Assert.That(target.unrelated, Is.EqualTo(88));
            Assert.That(window.Changed, Is.True);
            using (SerializedProperty preserved = caller.FindProperty("reference.amount"))
            {
                Assert.That(preserved.intValue, Is.EqualTo(93));
            }
        }

        [Test]
        public void CancellingPopup_PreservesBufferedEditsWithoutReplacingOrNotifying ()
        {
            Asset.INode original = target.reference;
            StageEdits();
            OpenPopup();
            popup.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape });
            caller.ApplyModifiedProperties();
            window.DrawNow();

            Assert.That(target.reference, Is.SameAs(original));
            Assert.That(((Asset.Alpha)target.reference).amount, Is.EqualTo(93));
            Assert.That(((Asset.Alpha)target.reference).payload.value, Is.EqualTo(94));
            Assert.That(target.unrelated, Is.EqualTo(88));
            Assert.That(window.Changed, Is.False);
        }

        [TestCase("elements.Array.data[0]", 11, 12)]
        [TestCase("inlineItems.Array.data[0].reference", 19, 20)]
        [TestCase("slots.Array.data[0].reference", 41, 42)]
        public void UnchangedNullInlineAndValueTypeTargets_AcceptSelection (string path, int amount, int child)
        {
            ShowWindow(path);
            OpenPopup();
            TypePopupInteraction.SelectVisibleItem(popup, "Deferred Beta");
            window.DrawNow();
            using (SerializedProperty property = caller.FindProperty(path))
            {
                AssertBeta((Asset.INode)property.managedReferenceValue, amount, child);
            }
            Assert.That(window.Changed, Is.True);
            Assert.That(target.elements[2], Is.Null);
            Assert.That(target.unrelated, Is.EqualTo(81));
        }

        [Test]
        public void ReorderedElement_SelectionCancelsWithoutEditingOrNotifying ()
        {
            ShowWindow("elements.Array.data[1]");
            Asset.INode first = target.elements[1];
            Asset.INode second = target.elements[3];
            OpenPopup();
            target.elements[1] = second;
            target.elements[3] = first;
            TypePopupInteraction.SelectVisibleItem(popup, "Deferred Beta");
            caller.Update();
            window.DrawNow();

            Assert.That(target.elements[1], Is.SameAs(second));
            Assert.That(target.elements[3], Is.SameAs(first));
            Assert.That(((Asset.Alpha)first).amount, Is.EqualTo(23));
            Assert.That(((Asset.Beta)second).amount, Is.EqualTo(47));
            Assert.That(window.Changed, Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DisposedOriginOrDestroyedHost_SelectionSafelyCancels (bool destroyHost)
        {
            Asset.INode original = target.reference;
            OpenPopup();
            if (destroyHost)
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
            else
            {
                window.Close();
                caller.Dispose();
                caller = null;
            }
            TypePopupInteraction.SelectVisibleItem(popup, "Deferred Beta");
            if (!destroyHost)
            {
                Assert.That(target.reference, Is.SameAs(original));
            }
            Assert.That(((Asset.Alpha)original).amount, Is.EqualTo(17));
            Assert.That(other.reference, Is.TypeOf<Asset.Alpha>());
        }

        [Test]
        public void MultipleHosts_SelectionPreservesEachHostsCurrentValues ()
        {
            target.reference = new Asset.Alpha(23);
            other.reference = new Asset.Beta(47);
            caller.Dispose();
            caller = new SerializedObject(new UnityEngine.Object[] { target, other });
            ShowWindow("reference");
            using (SerializedProperty unrelated = caller.FindProperty("unrelated"))
            {
                unrelated.intValue = 88;
            }
            OpenPopup();
            TypePopupInteraction.SelectVisibleItem(popup, "Deferred Beta");
            window.DrawNow();

            AssertBeta(target.reference, 23, 24);
            AssertBeta(other.reference, 47, 48);
            Assert.That(target.reference, Is.Not.SameAs(other.reference));
            Assert.That(target.unrelated, Is.EqualTo(88));
            Assert.That(other.unrelated, Is.EqualTo(88));
            Assert.That(window.Changed, Is.True);
        }

        [Test]
        public void OneChangedHost_SelectionCancelsForEveryHost ()
        {
            Asset.INode original = target.reference;
            caller.Dispose();
            caller = new SerializedObject(new UnityEngine.Object[] { target, other });
            ShowWindow("reference");
            OpenPopup();
            var replacement = new Asset.Beta(47);
            other.reference = replacement;
            TypePopupInteraction.SelectVisibleItem(popup, "Deferred Beta");
            caller.Update();
            window.DrawNow();

            Assert.That(target.reference, Is.SameAs(original));
            Assert.That(other.reference, Is.SameAs(replacement));
            Assert.That(((Asset.Alpha)original).amount, Is.EqualTo(17));
            Assert.That(window.Changed, Is.False);
        }

        [Test]
        public void PopupTypeChange_SupportsUndoRedoAndAssetReloadWithBufferedValues ()
        {
            assetPath = "Assets/__DeferredSelection_" + Guid.NewGuid().ToString("N") + ".asset";
            AssetDatabase.CreateAsset(target, assetPath);
            AssetDatabase.SaveAssets();
            Undo.ClearAll();
            StageEdits();
            OpenPopup();
            TypePopupInteraction.SelectVisibleItem(popup, "Deferred Beta");
            Undo.FlushUndoRecordObjects();
            AssertBeta(target.reference, 93, 94);

            Undo.PerformUndo();
            Assert.That(target.reference, Is.TypeOf<Asset.Alpha>());
            Assert.That(((Asset.Alpha)target.reference).amount, Is.EqualTo(93));
            Assert.That(((Asset.Alpha)target.reference).payload.value, Is.EqualTo(94));
            Assert.That(target.unrelated, Is.EqualTo(88));
            Undo.PerformRedo();
            AssertBeta(target.reference, 93, 94);

            window.Close();
            caller.Dispose();
            caller = null;
            AssetDatabase.SaveAssets();
            Resources.UnloadAsset(target);
            target = AssetDatabase.LoadAssetAtPath<Asset>(assetPath);
            Assert.That(target, Is.Not.Null);
            AssertBeta(target.reference, 93, 94);
            Assert.That(target.unrelated, Is.EqualTo(88));
        }

        private void ShowWindow (string path)
        {
            if (window != null)
            {
                window.Close();
            }
            window = ScriptableObject.CreateInstance<DeferredSelectionWindow>();
            window.Initialize(caller, path);
            window.ShowUtility();
        }

        private void OpenPopup ()
        {
            window.Focus();
            window.DrawNow();
            window.ClearChanges();
            Rect rect = window.FieldRect;
            window.SendEvent(new Event
            {
                type = EventType.MouseDown,
                mousePosition = new Vector2(rect.xMax - 30f, rect.y + EditorGUIUtility.singleLineHeight * 0.5f),
                button = 0
            });
            popup = Resources.FindObjectsOfTypeAll<EditorWindow>()
                .SingleOrDefault(candidate => candidate.GetType().Name == "AdvancedDropdownWindow");
            Assert.That(popup, Is.Not.Null, "Clicking the registered selector opens a real type popup.");
            window.SendEvent(new Event
            {
                type = EventType.MouseUp,
                mousePosition = new Vector2(rect.xMax - 30f, rect.y + EditorGUIUtility.singleLineHeight * 0.5f),
                button = 0
            });
        }

        private void StageEdits ()
        {
            using (SerializedProperty amount = caller.FindProperty("reference.amount"))
            using (SerializedProperty child = caller.FindProperty("reference.payload.value"))
            using (SerializedProperty unrelated = caller.FindProperty("unrelated"))
            {
                amount.intValue = 93;
                child.intValue = 94;
                unrelated.intValue = 88;
            }
        }

        private static void AssertBeta (Asset.INode value, int amount, int child)
        {
            Assert.That(value, Is.TypeOf<Asset.Beta>());
            Assert.That(((Asset.Beta)value).amount, Is.EqualTo(amount));
            Assert.That(((Asset.Beta)value).payload.value, Is.EqualTo(child));
        }
    }
}
