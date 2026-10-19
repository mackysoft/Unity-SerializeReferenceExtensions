using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Asset = MackySoft.SerializeReferenceExtensions.Tests.SubclassSelectorUIToolkitTestAsset;

namespace MackySoft.SerializeReferenceExtensions.Tests
{
    public sealed class SubclassSelectorUIToolkitTests
    {
        private Asset asset;
        private Asset secondAsset;
        private TestWindow window;
        private SerializedObject boundObject;
        private UnityEditor.Editor comparisonEditor;

        [SetUp]
        public void SetUp ()
        {
            Undo.ClearAll();
            Asset.ProbeDecoratorDrawer.DrawnHeadings.Clear();
            Asset.SelfApplyingNodeDrawer.ApplyValue = null;
            Asset.SelfApplyingNodeDrawer.Repainted = false;
            asset = ScriptableObject.CreateInstance<Asset>();
            secondAsset = ScriptableObject.CreateInstance<Asset>();
            window = ScriptableObject.CreateInstance<TestWindow>();
            window.position = new Rect(100, 100, 520, 700);
            window.Show();
        }

        [TearDown]
        public void TearDown ()
        {
            Asset.SelfApplyingNodeDrawer.ApplyValue = null;
            Asset.SelfApplyingNodeDrawer.Repainted = false;
            if (window != null)
            {
                window.rootVisualElement.Unbind();
                window.rootVisualElement.Clear();
                window.Close();
            }
            boundObject?.Dispose();
            boundObject = null;
            if (comparisonEditor != null)
            {
                UnityEngine.Object.DestroyImmediate(comparisonEditor);
                comparisonEditor = null;
            }
            Undo.ClearAll();
            if (asset != null)
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
            if (secondAsset != null)
            {
                UnityEngine.Object.DestroyImmediate(secondAsset);
            }
        }

        [UnityTest]
        public IEnumerator ManagedReferenceBaseDrawer_MatchesNativeChildAndRemainsEditable ()
        {
            asset.reference = new Asset.UnannotatedChildNode { child = new Asset.DerivedDrawerNode { marker = 431 } };
            asset.nativeReference = new Asset.UnannotatedChildNode { child = new Asset.DerivedDrawerNode { marker = 439 } };
            PropertyField host = Bind(nameof(Asset.reference));
            boundObject.FindProperty("nativeReference").isExpanded = true;
            var native = new PropertyField(boundObject.FindProperty("nativeReference"));
            window.rootVisualElement.Add(native);
            native.Bind(boundObject);
            yield return WaitFor(() => IsVisible(Input(native, "Inherited drawer value"))
                && IsVisible(Input(host, "Inherited drawer value")),
                "A managed-reference child must use the same base-type drawer as Unity's native PropertyField.");
            Input(host, "Inherited drawer value").value = 443;
            yield return WaitFor(() => ReadInt(asset, "reference.child.marker") == 443,
                "The inherited drawer must edit the child through its actual property binding.");
            Assert.That(ReadInt(asset, "nativeReference.child.marker"), Is.EqualTo(439));
        }

        [UnityTest]
        public IEnumerator MixedTypeHeader_RestoresTheUnchangedFirstTargetsTypeName ()
        {
            asset.reference = new Asset.PlainNode { value = 449 };
            secondAsset.reference = new Asset.PlainNode { value = 457 };
            PropertyField host = Bind(nameof(Asset.reference), asset, secondAsset);
            PopupField<string> Popup () => host.Q<PopupField<string>>();
            bool ShowsType () => Popup() != null && !Popup().showMixedValue
                && Popup().Query<TextElement>().ToList().Any(text => text.text == "Plain Node");
            yield return WaitFor(ShowsType, "The initial shared type name must be displayed.");
            ReplaceReference(secondAsset, "reference", new Asset.AlternateNode { other = 461 });
            yield return WaitFor(() => Popup() != null && Popup().showMixedValue,
                "Changing only the second target must display mixed types.");
            ReplaceReference(secondAsset, "reference", new Asset.PlainNode { value = 463 });
            yield return WaitFor(ShowsType, "Leaving mixed types must restore the unchanged first target's actual type name.");
            Assert.That(ReadInt(asset, "reference.value"), Is.EqualTo(449));
        }

        [UnityTest]
        public IEnumerator DisabledFoldouts_KeepNativeInspectionWithoutAllowingValueEdits ()
        {
            asset.reference = new Asset.FoldoutStateNode();
            asset.nativeReference = new Asset.FoldoutStateNode();
            PropertyField host = Bind(nameof(Asset.reference));
            boundObject.FindProperty("nativeReference").isExpanded = true;
            var native = new PropertyField(boundObject.FindProperty("nativeReference"));
            window.rootVisualElement.Add(native);
            native.Bind(boundObject);
            Foldout Details (VisualElement root, string prefix) => root.Query<Foldout>().ToList()
                .FirstOrDefault(field => field.bindingPath == prefix + ".details");
            yield return WaitFor(() => Details(host, "reference") != null && Details(native, "nativeReference") != null,
                "Both native and selector foldouts must bind before disabling the Inspector.");
            Details(host, "reference").value = false;
            Details(native, "nativeReference").value = false;
            Selector(host, "reference").value = false;
            native.Q<Foldout>().value = false;
            yield return null;
            host.SetEnabled(false);
            native.SetEnabled(false);
            Toggle Prefix (VisualElement root) => root.Query<Toggle>(className: Foldout.toggleUssClassName).ToList()
                .First(field => IsVisible(field));
            // Establish that this route performs a real native click before comparing
            // disabled behavior (2021.3's own Clickable rejects disabled mouse-up).
            native.SetEnabled(true);
            Click(Prefix(native).Q(className: Toggle.checkmarkUssClassName));
            yield return WaitFor(() => native.Q<Foldout>().value,
                "The input route must open an enabled native foldout.");
            native.Q<Foldout>().value = false;
            native.SetEnabled(false);
            Click(Prefix(native).Q(className: Toggle.checkmarkUssClassName));
            yield return null;
            bool nativeExpanded = native.Q<Foldout>().value;
            Click(Prefix(host).Q(className: Toggle.checkmarkUssClassName));
            yield return null;
            Assert.That(Selector(host, "reference").value, Is.EqualTo(nativeExpanded),
                "A disabled selector must preserve this Editor's native viewing behavior.");

            // Prepare visible child headers independently of the disabled-root outcome.
            native.SetEnabled(true);
            host.SetEnabled(true);
            native.Q<Foldout>().value = true;
            Selector(host, "reference").value = true;
            yield return WaitFor(() => IsVisible(Details(native, "nativeReference")) && IsVisible(Details(host, "reference")),
                "Both child headers must be visible before comparing disabled input.");
            native.SetEnabled(false);
            host.SetEnabled(false);
            Click(Details(native, "nativeReference").Q<Toggle>().Q(className: Toggle.checkmarkUssClassName));
            yield return null;
            nativeExpanded = Details(native, "nativeReference").value;
            Click(Details(host, "reference").Q<Toggle>().Q(className: Toggle.checkmarkUssClassName));
            yield return null;
            Assert.That(Details(host, "reference").value, Is.EqualTo(nativeExpanded),
                "Disabled child viewing must match the native property foldout.");
            Details(native, "nativeReference").value = true;
            Details(host, "reference").value = true;
            ListView Numbers (VisualElement root, string prefix) => root.Query<ListView>().ToList()
                .FirstOrDefault(list => list.bindingPath == prefix + ".details.numbers");
            yield return WaitFor(() => Numbers(native, "nativeReference")?.Q<Foldout>() != null
                && Numbers(host, "reference")?.Q<Foldout>() != null,
                "Native and selector collection headers must be available.");
            Foldout nativeCollection = Numbers(native, "nativeReference").Q<Foldout>();
            Foldout actualCollection = Numbers(host, "reference").Q<Foldout>();
            nativeCollection.value = false;
            actualCollection.value = false;
            yield return null;
            Click(nativeCollection.Q<Toggle>().Q(className: Toggle.checkmarkUssClassName));
            yield return null;
            nativeExpanded = nativeCollection.value;
            Click(actualCollection.Q<Toggle>().Q(className: Toggle.checkmarkUssClassName));
            yield return null;
            Assert.That(actualCollection.value, Is.EqualTo(nativeExpanded),
                "Disabled collection viewing must match the same native collection header.");
            Toggle value = host.Query<Toggle>().ToList().First(field => field.bindingPath == "reference.details.value");
            Click(value.Q(className: Toggle.checkmarkUssClassName));
            yield return null;
            using (var saved = new SerializedObject(asset))
            {
                Assert.That(saved.FindProperty("reference.details.value").boolValue, Is.False,
                    "Allowing view expansion must not enable serialized boolean edits.");
            }
        }

        [UnityTest]
        public IEnumerator NestedExpansion_DoesNotNotifyValuesButBooleanEditsDo ()
        {
            asset.reference = new Asset.BooleanGroupNode();
            PropertyField host = Bind(nameof(Asset.reference));
            Foldout Details () => host.Query<Foldout>().ToList().FirstOrDefault(field => field.bindingPath == "reference.details");
            Toggle Value () => host.Query<Toggle>().ToList().FirstOrDefault(field => field.bindingPath == "reference.details.value");
            yield return WaitFor(() => Details() != null, "The nested view state must be available before observation.");
            Details().value = true;
            yield return WaitFor(() => IsVisible(Value()), "The nested boolean must bind before observing view-only changes.");
            int notifications = 0;
            bool editPublished = false;
            host.RegisterValueChangeCallback(evt =>
            {
                notifications++;
                editPublished |= ((Asset.BooleanGroupNode)asset.reference).details.value;
            });
            // Observe a committed edit first so initial native binding notifications
            // cannot be mistaken for notifications from the following view changes.
            Value().value = true;
            yield return WaitFor(() => editPublished && ((Asset.BooleanGroupNode)asset.reference).details.value,
                "The host must publish the child edit before counting view-only notifications.");
            int before = notifications;
            foreach (bool expanded in new[] { false, true })
            {
                Details().value = expanded;
                yield return WaitFor(() => boundObject.FindProperty("reference.details").isExpanded == expanded
                    && IsVisible(Value()) == expanded,
                    "A child foldout must retain its native expansion binding.");
                for (int frame = 0; frame < 8; frame++)
                {
                    yield return null;
                }
            }
            Assert.That(notifications, Is.EqualTo(before), "Viewing a child must not report a serialized value edit.");
            Value().value = false;
            yield return WaitFor(() => notifications > before && !((Asset.BooleanGroupNode)asset.reference).details.value,
                "Expansion filtering must preserve real child boolean commits and value notifications.");
        }

        [UnityTest]
        public IEnumerator CollectionSelectionAndRemoval_MatchTheCurrentNativeVersion ()
            => VerifyCollectionSelectionAndRemoval("numbers");

        [UnityTest]
        public IEnumerator ListSelectionAndRemoval_MatchTheCurrentNativeVersion ()
            => VerifyCollectionSelectionAndRemoval("list");

        private IEnumerator VerifyCollectionSelectionAndRemoval (string collection)
        {
            asset.reference = new Asset.CollectionSelectionNode();
            asset.nativeReference = new Asset.CollectionSelectionNode();
            PropertyField host = Bind(nameof(Asset.reference));
            boundObject.FindProperty("nativeReference").isExpanded = true;
            var native = new PropertyField(boundObject.FindProperty("nativeReference"));
            window.rootVisualElement.Add(native);
            native.Bind(boundObject);
            ListView Control (VisualElement root, string prefix) => root.Query<ListView>().ToList()
                .FirstOrDefault(list => list.bindingPath == prefix + "." + collection);
            yield return WaitFor(() => Control(host, "reference")?.itemsSource != null
                && Control(native, "nativeReference")?.itemsSource != null,
                "Native and selector collections must bind before selecting rows.");
            foreach (Foldout foldout in window.rootVisualElement.Query<Foldout>().ToList())
            {
                foldout.value = true;
            }
            ListView actual = Control(host, "reference");
            ListView standard = Control(native, "nativeReference");
            Assert.That(actual.selectionType, Is.EqualTo(standard.selectionType),
                "The displayed native collection's public selection mode must be preserved.");
            int selected = standard.selectionType == SelectionType.Multiple ? 2 : 1;
            void SelectRows (ListView list, string prefix)
            {
                // Prepare only the selection permitted by the native mode. Selection
                // mode is asserted separately because SetSelection bypasses that policy.
                list.SetSelection(Enumerable.Range(0, selected).Select(index =>
                    CollectionIndex(list, prefix + "." + collection + ".Array.data[" + index + "]")));
            }
            SelectRows(standard, "nativeReference");
            SelectRows(actual, "reference");
            yield return WaitFor(() => IsVisible(CollectionButton(native, "nativeReference." + collection, "-"))
                && IsVisible(CollectionButton(host, "reference." + collection, "-")), "Both native removal controls must be visible.");
            Submit(CollectionButton(native, "nativeReference." + collection, "-"));
            yield return WaitFor(() => ReadArraySize(asset, "nativeReference." + collection) == 3 - selected,
                "Native removal must establish the expected retained rows.");
            Submit(CollectionButton(host, "reference." + collection, "-"));
            yield return WaitFor(() => ReadArraySize(asset, "reference." + collection) == 3 - selected,
                "The selector must remove the same selected rows as native.");
            for (int i = 0; i < 3 - selected; i++)
            {
                Assert.That(ReadInt(asset, "reference." + collection + ".Array.data[" + i + "]"),
                    Is.EqualTo(ReadInt(asset, "nativeReference." + collection + ".Array.data[" + i + "]")));
            }
        }

        [UnityTest]
        public IEnumerator CollectionRows_MatchNativePropertyFieldLayout ()
            => VerifyCollectionLayout(new InspectorElement());

        [UnityTest]
        public IEnumerator CollectionRows_CustomInspectorMatchesNativePropertyFieldLayout ()
            => VerifyCollectionLayout(new VisualElement());

        private IEnumerator VerifyCollectionLayout (VisualElement inspector)
        {
            asset.reference = new Asset.CollectionLayoutNode();
            asset.nativeReference = new Asset.CollectionLayoutNode();
            boundObject = new SerializedObject(asset);
            using (SerializedProperty property = boundObject.GetIterator())
            {
                while (property.NextVisible(true))
                {
                    property.isExpanded = true;
                }
            }
            // Construct the public UITK host explicitly: the minimum version's normal
            // InspectorElement(source) constructor selects the IMGUI inspector instead.
            inspector.AddToClassList(InspectorElement.ussClassName);
            inspector.AddToClassList(InspectorElement.uIEDefaultVariantUssClassName);
            inspector.AddToClassList(InspectorElement.uIEInspectorVariantUssClassName);
            var nativeReference = new PropertyField(boundObject.FindProperty("nativeReference"));
            inspector.Add(nativeReference);
            inspector.Add(new PropertyField(boundObject.FindProperty("reference")));
            window.rootVisualElement.Add(inspector);
            foreach (PropertyField field in inspector.Query<PropertyField>().ToList())
            {
                field.Bind(boundObject);
            }
            string[] paths = { "reference.details.nativeValue", "reference.details.array.Array.data[0].marker",
                "reference.details.list.Array.data[0].marker", "reference.details.simpleArray.Array.data[0].marker",
                "reference.details.simpleList.Array.data[0].marker" };
            IntegerField Find (string path) => inspector.Query<IntegerField>().ToList()
                .FirstOrDefault(field => field.bindingPath == path && IsVisible(field));
            string NativePath (string path) => "nativeReference" + path.Substring("reference".Length);
            VisualElement InputArea (IntegerField field) => field.Q<VisualElement>(className: BaseField<int>.inputUssClassName);
            bool OpenAndFindRows ()
            {
                foreach (Foldout foldout in inspector.Query<Foldout>().ToList())
                {
                    foldout.value = true;
                }
                return paths.All(path => Find(path) != null && Find(NativePath(path)) != null);
            }
            foreach (float width in new[] { 520f, 700f })
            {
                window.position = new Rect(100, 100, width, 1000);
                yield return WaitFor(() => Mathf.Abs(inspector.worldBound.width - width) < 0.25f && OpenAndFindRows(),
                    "The expanded collection rows must be visible before their layout is compared.", () => DescribeInputs(inspector));
                string previous = null;
                double changedAt = EditorApplication.timeSinceStartup;
                yield return WaitFor(() =>
                {
                    string current = string.Join(";", paths.Select(path => Find(NativePath(path)).labelElement.worldBound.ToString() + InputArea(Find(NativePath(path))).worldBound));
                    if (current != previous)
                    {
                        previous = current;
                        changedAt = EditorApplication.timeSinceStartup;
                    }
                    return EditorApplication.timeSinceStartup - changedAt >= 0.25d;
                }, "The native input and collection labels must finish layout after resizing.");
                for (int i = 0; i < paths.Length; i++)
                {
                    IntegerField field = Find(paths[i]);
                    IntegerField native = Find(NativePath(paths[i]));
                    Assert.That(field.labelElement.worldBound.x,
                        Is.EqualTo(native.labelElement.worldBound.x).Within(0.25f), "Native label indentation: " + paths[i]);
                    Assert.That(InputArea(field).worldBound.x, Is.EqualTo(InputArea(native).worldBound.x).Within(0.25f),
                        "The input must match the same field in Unity's native layout: " + paths[i]);
                }
            }
            Find(paths[3]).value = 929;
            yield return WaitFor(() => ReadInt(asset, paths[3]) == 929,
                "The aligned NonReorderable row must keep its native binding.");
        }

        [UnityTest]
        public IEnumerator OwnedDrawerRows_MatchNativeFoldoutLayoutAcrossResizes ()
        {
            asset.reference = new Asset.DrawerCollectionLayoutNode();
            asset.nativeReference = new Asset.DrawerCollectionLayoutNode();
            comparisonEditor = UnityEditor.Editor.CreateEditor(asset, typeof(SubclassSelectorOnGUIComparisonEditor));
            ((SubclassSelectorOnGUIComparisonEditor)comparisonEditor).Paths = new[] { "nativeReference", "reference" };
            using (SerializedProperty property = comparisonEditor.serializedObject.GetIterator())
            {
                while (property.NextVisible(true))
                {
                    property.isExpanded = true;
                }
            }
            var inspector = new InspectorElement(comparisonEditor);
            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;
            scroll.Add(inspector);
            window.rootVisualElement.Add(scroll);
            string[] collections = { "array", "simpleArray", "simpleList" };
            IntegerField Marker (string prefix, string collection) => Element(inspector, prefix + "." + collection + ".Array.data[0]")
                ?.Query<IntegerField>().ToList().FirstOrDefault(field => field.label == "Marker");
            bool Ready ()
            {
                foreach (Foldout foldout in inspector.Query<Foldout>().ToList()) foldout.value = true;
                return collections.All(path => IsVisible(Marker("nativeReference", path)) && IsVisible(Marker("reference", path)));
            }
            VisualElement InputArea (IntegerField field) => field.Q<VisualElement>(className: BaseField<int>.inputUssClassName);
            foreach (float width in new[] { 520f, 700f })
            {
                window.position = new Rect(100, 100, width, 1000);
                yield return WaitFor(() => Ready() && Mathf.Abs(inspector.worldBound.width - scroll.contentViewport.worldBound.width) < 0.25f,
                    "The same typed drawer must render in native and selector collections before layout comparison.");
                string previous = null;
                int stable = 0;
                yield return WaitFor(() =>
                {
                    string current = string.Join(";", collections.Select(path => Marker("nativeReference", path).worldBound.ToString()
                        + Marker("reference", path).worldBound));
                    stable = current == previous ? stable + 1 : 0;
                    previous = current;
                    return stable >= 3;
                }, "Both real drawer layouts must settle after resizing.");
                foreach (string path in collections)
                {
                    IntegerField actual = Marker("reference", path);
                    IntegerField native = Marker("nativeReference", path);
                    Foldout actualFoldout = actual.GetFirstAncestorOfType<Foldout>();
                    Foldout nativeFoldout = native.GetFirstAncestorOfType<Foldout>();
                    Assert.That(actualFoldout.Q<Toggle>().worldBound.x,
                        Is.EqualTo(nativeFoldout.Q<Toggle>().worldBound.x).Within(0.25f), "Drawer header indentation: " + path);
                    Assert.That(actualFoldout.contentContainer.worldBound.x,
                        Is.EqualTo(nativeFoldout.contentContainer.worldBound.x).Within(0.25f), "Drawer content indentation: " + path);
                    Assert.That(actual.labelElement.worldBound.x,
                        Is.EqualTo(native.labelElement.worldBound.x).Within(0.25f), "Drawer label indentation: " + path);
                    Assert.That(InputArea(actual).worldBound.xMin,
                        Is.EqualTo(InputArea(native).worldBound.xMin).Within(0.25f), "Drawer input column: " + path);
                    Assert.That(InputArea(actual).worldBound.xMax,
                        Is.EqualTo(InputArea(native).worldBound.xMax).Within(0.25f), "Drawer input right margin: " + path);
                }
            }
            Marker("reference", "simpleList").value = 997;
            yield return WaitFor(() => ReadInt(asset, "reference.simpleList.Array.data[0].marker") == 997,
                "The correctly aligned typed drawer must edit its current List element.");
            Assert.That(ReadInt(asset, "nativeReference.simpleList.Array.data[0].marker"), Is.EqualTo(941));
        }

        [UnityTest]
        public IEnumerator OwnedRows_RebindNativeInputsAndSwitchDrawerModes ()
        {
            asset.reference = new Asset.DrawerCollectionLayoutNode();
            PropertyField host = Bind(nameof(Asset.reference));
            ListView Numbers () => host.Query<ListView>().ToList().FirstOrDefault(list => list.bindingPath == "reference.numbers");
            IntegerField Number (int index) => Element(host, "reference.numbers.Array.data[" + index + "]")?.Q<IntegerField>();
            void Expand ()
            {
                foreach (Foldout foldout in host.Query<Foldout>().ToList()) foldout.value = true;
            }
            yield return WaitFor(() =>
            {
                Expand();
                return Numbers()?.itemsSource != null && IsVisible(Number(0)) && IsVisible(Number(1));
            }, "The native scalar rows must bind before reuse.");
            Numbers().RefreshItems();
            yield return WaitFor(() => IsVisible(Number(0)) && Number(0).value == 11 && IsVisible(Number(1)) && Number(1).value == 13,
                "Rebinding same-type native rows must retain both displayed inputs.");
            Number(1).value = 31;
            yield return WaitFor(() => ReadInt(asset, "reference.numbers.Array.data[1]") == 31,
                "The reused native input must still edit its current scalar element.");
            Assert.That(ReadInt(asset, "reference.numbers.Array.data[0]"), Is.EqualTo(11));

            IntegerField Drawn (int index) => Element(host, "reference.mixed.Array.data[" + index + "]")
                ?.Query<IntegerField>().ToList().FirstOrDefault(field => field.label == "Marker");
            IntegerField Plain (int index) => Element(host, "reference.mixed.Array.data[" + index + "].value")?.Q<IntegerField>();
            yield return WaitFor(() =>
            {
                Expand();
                return IsVisible(Plain(0)) && Plain(0).value == 17 && IsVisible(Drawn(1)) && Drawn(1).value == 19;
            }, "Owned and native typed-drawer rows must both bind before changing their order.");
            using (var external = new SerializedObject(asset))
            {
                external.FindProperty("reference.mixed").MoveArrayElement(0, 1);
                external.ApplyModifiedProperties();
            }
            yield return WaitFor(() =>
            {
                Expand();
                return IsVisible(Drawn(0)) && Drawn(0).value == 19 && IsVisible(Plain(1)) && Plain(1).value == 17;
            }, "Rows changing between native and owned drawing must show their current type and values.");
            Drawn(0).value = 43;
            Plain(1).value = 47;
            yield return WaitFor(() => ReadInt(asset, "reference.mixed.Array.data[0].marker") == 43
                && ReadInt(asset, "reference.mixed.Array.data[1].value") == 47,
                "Both renderer modes must edit their current element after the swap.");
        }

        [UnityTest]
        public IEnumerator NativeHeader_FollowsVisibilityEnabledStateAndDetachOfItsProperty ()
        {
            asset.reference = new Asset.PlainNode { value = 17 };
            PropertyField host = Bind(nameof(Asset.reference));
            Foldout foldout = null;
            VisualElement popup = null;
            yield return WaitFor(() => (foldout = Selector(host, "reference")) != null
                && (popup = Header(host, "reference")) != null && popup.worldBound.height > 0f,
                "Both native header controls must attach before changing their property's presentation.");
            foldout.tooltip = "Current property tooltip";
            yield return WaitFor(() => Header(host, "reference").GetFirstAncestorOfType<PopupField<string>>().tooltip == foldout.tooltip,
                "The drawn header must retain the property's tooltip.");
            foldout.visible = false;
            yield return WaitFor(() => !popup.visible, "Hiding the property must hide its type selector.");
            foldout.visible = true;
            yield return WaitFor(() => popup.visible, "Showing the property must restore its type selector.");
            foldout.style.display = DisplayStyle.None;
            yield return WaitFor(() => !IsVisible(popup), "Removing the property from layout must hide its type selector.");
            foldout.style.display = DisplayStyle.Flex;
            yield return WaitFor(() => IsVisible(popup), "Returning the property to layout must restore its type selector.");
            foldout.SetEnabled(false);
            yield return WaitFor(() => !popup.enabledInHierarchy, "Disabling the property must disable its type selector.");
            foldout.SetEnabled(true);
            yield return WaitFor(() => popup.enabledInHierarchy, "Enabling the property must restore its type selector.");
            foldout.RemoveFromHierarchy();
            yield return WaitFor(() => popup.panel == null, "Removing the property must detach its type selector.");
            host.Add(foldout);
            yield return WaitFor(() => (popup = Header(host, "reference")) != null && popup.panel != null
                && VisibleInput(host, "Value", 17) != null,
                "Reattaching the property must restore both controls and the current subtype binding.");
            host.Clear();
            host.Add(foldout);
            yield return WaitFor(() => (popup = Header(host, "reference")) != null && popup.panel != null
                && IsVisible(popup) && VisibleInput(host, "Value", 17) != null,
                "Rebuilding the same host in one frame must restore the visible header and current subtype input.");
        }

        [UnityTest]
        public IEnumerator NativePopup_OpensTypeSearchFromKeyboardAndPointer_AndKeepsUndo ()
        {
            asset.reference = new Asset.PlainNode { value = 17 };
            comparisonEditor = UnityEditor.Editor.CreateEditor(asset, typeof(SubclassSelectorOnGUIComparisonEditor));
            ((SubclassSelectorOnGUIComparisonEditor)comparisonEditor).Paths = new[] { nameof(Asset.reference) };
            comparisonEditor.serializedObject.FindProperty(nameof(Asset.reference)).isExpanded = true;
            var inspector = new InspectorElement(comparisonEditor);
            window.rootVisualElement.Add(inspector);
            PropertyField host = Element(inspector, nameof(Asset.reference));
            var existingWindows = new HashSet<EditorWindow>(Resources.FindObjectsOfTypeAll<EditorWindow>());
            try
            {
                foreach (bool usePointer in new[] { false, true })
                {
                    yield return WaitFor(() => VisibleInput(host, "Value", 17) != null
                        && Header(host, "reference") is VisualElement header && header.worldBound.height > 0f,
                        "The header and selected subtype must finish binding before input.");
                    VisualElement input = Header(host, "reference");
                    if (usePointer)
                    {
                        Vector2 point = input.worldBound.center;
                        window.SendEvent(new Event { type = EventType.MouseDown, mousePosition = point, button = 0, clickCount = 1 });
                        window.SendEvent(new Event { type = EventType.MouseUp, mousePosition = point, button = 0 });
                    }
                    else
                    {
                        input.Focus();
                        window.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Space, character = ' ' });
                    }
                    TestContext.Out.WriteLine("Type menu activation pointer=" + usePointer + ";focus="
                        + window.rootVisualElement.panel.focusController.focusedElement + ";header=" + input.worldBound);
                    EditorWindow popup = null;
                    yield return WaitFor(() => (popup = Resources.FindObjectsOfTypeAll<EditorWindow>()
                        .FirstOrDefault(value => !existingWindows.Contains(value) && value.GetType().Name == "AdvancedDropdownWindow")) != null,
                        "Activating the native popup must open SubclassSelector's searchable type menu. Pointer=" + usePointer,
                        () => "Focus=" + window.rootVisualElement.panel.focusController.focusedElement
                            + "; windows=" + string.Join(",", Resources.FindObjectsOfTypeAll<EditorWindow>().Select(value => value.GetType().Name)));
                    Undo.IncrementCurrentGroup();
                    TypePopupInteraction.SelectVisibleItem(popup, "<null>");
                    yield return WaitFor(() => asset.reference == null, "The displayed type popup must select null.");
                    Undo.PerformUndo();
                    yield return WaitFor(() => asset.reference is Asset.PlainNode node && node.value == 17
                        && VisibleInput(host, "Value", 17) != null, "Undo must restore the selected subtype and its bound input.");
                }
            }
            finally
            {
                foreach (EditorWindow opened in Resources.FindObjectsOfTypeAll<EditorWindow>())
                {
                    if (!existingWindows.Contains(opened))
                    {
                        opened.Close();
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator InspectorHeader_ResizesWithNativeColumnsAndIgnoresAmbientIMGUISettings ()
        {
            asset.array = new Asset.INode[] { new Asset.PlainNode { value = 11 } };
            asset.simpleArray = new Asset.INode[] { new Asset.PlainNode { value = 11 } };
            asset.nativePopupArray = new[] { Asset.LayoutKind.PlainNode };
            asset.nativePopupSimpleArray = new[] { Asset.LayoutKind.PlainNode };
            comparisonEditor = UnityEditor.Editor.CreateEditor(asset, typeof(SubclassSelectorOnGUIComparisonEditor));
            var editor = (SubclassSelectorOnGUIComparisonEditor)comparisonEditor;
            editor.Paths = new[] { "nativePopupArray", "nativePopupSimpleArray", "array", "simpleArray" };
            foreach (string path in editor.Paths)
            {
                using (SerializedProperty property = editor.serializedObject.FindProperty(path))
                {
                    property.isExpanded = true;
                    property.GetArrayElementAtIndex(0).isExpanded = true;
                }
            }
            var layout = new VisualElement();
            layout.style.flexDirection = FlexDirection.Row;
            var inspectors = new[] { new InspectorElement(comparisonEditor), new InspectorElement(comparisonEditor) };
            foreach (InspectorElement inspector in inspectors)
            {
                inspector.style.flexGrow = 1;
                inspector.style.flexBasis = 0;
                layout.Add(inspector);
            }
            window.rootVisualElement.Add(layout);
            string[] paths = { "array.Array.data[0]", "simpleArray.Array.data[0]" };
            string[] nativePaths = { "nativePopupArray.Array.data[0]", "nativePopupSimpleArray.Array.data[0]" };
            bool previousMode = EditorGUIUtility.hierarchyMode;
            var rawLabelWidth = typeof(EditorGUIUtility).GetField("s_LabelWidth",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            float previousWidth = (float)rawLabelWidth.GetValue(null);
            try
            {
                foreach (float width in new[] { 800f, 1000f })
                {
                    window.position = new Rect(100, 100, width, 700);
                    yield return WaitFor(() => Mathf.Abs(layout.worldBound.width - width) < 0.25f
                        && inspectors.All(root => paths.All(path => Header(root, path)?.worldBound.height > 0f)),
                        "Both Inspector columns must finish layout before comparison.");
                    // The controls still use Unity's real Inspector width when an
                    // adjacent IMGUI drawer changes these process-wide GUI settings.
                    foreach (var ambient in new[] { (false, 72f), (true, 240f), (false, 0f), (true, 0f) })
                    {
                        EditorGUIUtility.hierarchyMode = ambient.Item1;
                        EditorGUIUtility.labelWidth = ambient.Item2;
                        window.Repaint();
                        yield return null;
                        foreach (InspectorElement inspector in inspectors)
                        {
                            for (int i = 0; i < paths.Length; i++)
                            {
                                VisualElement expected = Element(inspector, nativePaths[i]).Q(className: BaseField<Enum>.inputUssClassName);
                                AssertNativeInput(Header(inspector, paths[i]), expected, paths[i]);
                            }
                        }
                    }
                }
            }
            finally
            {
                EditorGUIUtility.hierarchyMode = previousMode;
                EditorGUIUtility.labelWidth = previousWidth;
            }
        }

        [UnityTest]
        public IEnumerator InspectorResize_KeepsNativeColumnsAtPaintAndRetainsEditingFocus ()
        {
            asset.nativeSimpleArray = new Asset.INode[] { new Asset.PlainNode { value = 11 }, null };
            asset.array = new Asset.INode[] { new Asset.PlainNode { value = 11 }, null };
            asset.simpleArray = new Asset.INode[] { new Asset.PlainNode { value = 11 }, null };
            asset.nativePopupArray = new[] { Asset.LayoutKind.PlainNode, Asset.LayoutKind.PlainNode };
            asset.nativePopupSimpleArray = new[] { Asset.LayoutKind.PlainNode, Asset.LayoutKind.PlainNode };
            comparisonEditor = UnityEditor.Editor.CreateEditor(asset, typeof(SubclassSelectorOnGUIComparisonEditor));
            var editor = (SubclassSelectorOnGUIComparisonEditor)comparisonEditor;
            editor.Paths = new[] { "nativeSimpleArray", "nativePopupArray", "nativePopupSimpleArray", "array", "simpleArray" };
            foreach (string path in editor.Paths)
            {
                using (SerializedProperty property = editor.serializedObject.FindProperty(path))
                {
                    property.isExpanded = true;
                    if (property.isArray) property.GetArrayElementAtIndex(0).isExpanded = true;
                }
            }
            var inspector = new InspectorElement(comparisonEditor);
            window.rootVisualElement.Add(inspector);
            string[] collections = { "array", "simpleArray" };
            string[] nativeCollections = { "nativePopupArray", "nativePopupSimpleArray" };
            string[] focusPaths = { "nativeSimpleArray.Array.data[0]", "simpleArray.Array.data[0]" };
            yield return WaitFor(() => focusPaths.All(path => VisibleInput(Element(inspector, path), "Value", 11) != null)
                && collections.All(path => Enumerable.Range(0, 2).All(index =>
                    IsVisible(Header(inspector, path + ".Array.data[" + index + "]")))),
                "Native controls and selector rows must finish binding before the resize begins.");

            foreach (string focusPath in focusPaths)
            {
                window.Focus();
                IntegerField input = Input(Element(inspector, focusPath), "Value");
                bool focused = false;
                // ListView is a composite focus root: the panel's focusedElement
                // is retargeted outside that root. Observe the real input events.
                input.RegisterCallback<FocusInEvent>(evt => focused = true);
                input.RegisterCallback<FocusOutEvent>(evt => focused = false);
                input.Focus();
                yield return WaitFor(() => focused, "The input must receive focus before resizing.");
                input.SelectAll();
                TypeText("5");
                yield return WaitFor(() => ReadInt(asset, focusPath + ".value") == 5 && focused,
                    "The first character must commit without ending the edit.");

                int paints = 0;
                var failures = new List<string>();
                var observations = new List<string>();
                // Sample during Unity's actual repaint, including the first paint at
                // each new width. Waiting for matching bounds would hide a bad frame.
                Action<MeshGenerationContext> observePaint = _ =>
                {
                    paints++;
                    for (int collection = 0; collection < collections.Length; collection++)
                    {
                        for (int index = 0; index < 2; index++)
                        {
                            string suffix = ".Array.data[" + index + "]";
                            string path = collections[collection] + suffix;
                            VisualElement actual = Header(inspector, path);
                            VisualElement expected = Element(inspector, nativeCollections[collection] + suffix)
                                ?.Q(className: BaseField<Enum>.inputUssClassName);
                            if (!IsVisible(actual) || !IsVisible(expected))
                            {
                                failures.Add("Missing visible input at paint " + paints + ": " + path);
                                continue;
                            }
                            Rect actualBounds = actual.worldBound;
                            Rect nativeBounds = expected.worldBound;
                            if (Mathf.Abs(actualBounds.xMin - nativeBounds.xMin) > 0.25f
                                || Mathf.Abs(actualBounds.xMax - nativeBounds.xMax) > 0.25f
                                || Mathf.Abs(actualBounds.height - nativeBounds.height) > 0.25f)
                            {
                                failures.Add("Native column mismatch at paint " + paints + ": " + path
                                    + ";actual=" + actualBounds + ";native=" + nativeBounds);
                            }
                        }
                    }
                    if (!focused)
                    {
                        failures.Add("Editing focus was lost at paint " + paints + ": " + focusPath);
                    }
                    observations.Add("Resize paint: " + paints + ";editing=" + focusPath
                        + ";width=" + inspector.worldBound.width + ";failures=" + failures.Count);
                };
                inspector.generateVisualContent += observePaint;
                try
                {
                    const int stepsPerDirection = 12;
                    for (int step = 0; step <= stepsPerDirection * 2; step++)
                    {
                        float phase = step <= stepsPerDirection ? step : stepsPerDirection * 2 - step;
                        float width = Mathf.Lerp(520f, 700f, phase / stepsPerDirection);
                        int previousPaints = paints;
                        window.position = new Rect(100, 100, width, 700);
                        inspector.MarkDirtyRepaint();
                        yield return WaitFor(() => paints > previousPaints,
                            "The resized Inspector must paint before advancing to the next width.");
                    }
                }
                finally
                {
                    inspector.generateVisualContent -= observePaint;
                    foreach (string observation in observations) TestContext.Out.WriteLine(observation);
                }
                Assert.That(failures, Is.Empty, string.Join("\n", failures));
                TypeText("9");
                yield return WaitFor(() => ReadInt(asset, focusPath + ".value") == 59,
                    "Typing must continue in the same edit after resizing.");
            }
        }

        [UnityTest]
        public IEnumerator SelectorHeaders_MatchNativeUIToolkitInputsAndFoldoutsAcrossResizes ()
        {
            asset.reference = new Asset.PlainNode { value = 11 };
            asset.nativeReference = new Asset.PlainNode { value = 11 };
            asset.array = new Asset.INode[] { new Asset.PlainNode { value = 11 }, null };
            asset.simpleArray = new Asset.INode[] { new Asset.PlainNode { value = 11 }, null };
            asset.nativeArray = new Asset.INode[] { new Asset.PlainNode { value = 11 }, null };
            asset.nativeSimpleArray = new Asset.INode[] { new Asset.PlainNode { value = 11 }, null };
            asset.nativePopupArray = new[] { Asset.LayoutKind.PlainNode, Asset.LayoutKind.PlainNode };
            asset.nativePopupSimpleArray = new[] { Asset.LayoutKind.PlainNode, Asset.LayoutKind.PlainNode };
            asset.list.AddRange(asset.array);
            asset.simpleList.AddRange(asset.simpleArray);
            asset.nativeList.AddRange(asset.nativeArray);
            asset.nativeSimpleList.AddRange(asset.nativeSimpleArray);
            asset.nativePopupList.AddRange(asset.nativePopupArray);
            asset.nativePopupSimpleList.AddRange(asset.nativePopupSimpleArray);
            foreach (var paths in new[]
                { new[] { "reference", "nativeReference", "nativePopupReference" },
                  new[] { "array", "nativeArray", "nativePopupArray" },
                  new[] { "simpleArray", "nativeSimpleArray", "nativePopupSimpleArray" },
                  new[] { "list", "nativeList", "nativePopupList" },
                  new[] { "simpleList", "nativeSimpleList", "nativePopupSimpleList" } })
            {
                comparisonEditor = UnityEditor.Editor.CreateEditor(asset, typeof(SubclassSelectorOnGUIComparisonEditor));
                var editor = (SubclassSelectorOnGUIComparisonEditor)comparisonEditor;
                editor.Paths = paths;
                foreach (string path in paths)
                {
                    using (SerializedProperty property = editor.serializedObject.FindProperty(path))
                    {
                        property.isExpanded = true;
                        if (property.isArray) property.GetArrayElementAtIndex(0).isExpanded = true;
                    }
                }
                var inspector = new InspectorElement(comparisonEditor);
                window.rootVisualElement.Add(inspector);
                bool collection = paths[0] != "reference";
                string path0 = paths[0] + (collection ? ".Array.data[0]" : "");
                string native0 = paths[1] + (collection ? ".Array.data[0]" : "");
                foreach (float width in new[] { 520f, 700f })
                {
                    window.position = new Rect(100, 100, width, 700);
                    yield return WaitFor(() => Mathf.Abs(inspector.worldBound.width - width) < 0.25f
                        && Enumerable.Range(0, collection ? 2 : 1).All(index =>
                            Header(inspector, paths[0] + (collection ? ".Array.data[" + index + "]" : ""))?.worldBound.height > 0f
                            && Element(inspector, paths[2] + (collection ? ".Array.data[" + index + "]" : ""))
                                ?.Q(className: BaseField<Enum>.inputUssClassName)?.worldBound.height > 0f)
                        && VisibleInput(Element(inspector, path0), "Value", 11) != null,
                        "The native reference and selector must bind before comparing their visible controls.");
                    for (int frame = 0; frame < 3; frame++) yield return null;
                    for (int index = 0; index < (collection ? 2 : 1); index++)
                    {
                        string suffix = collection ? ".Array.data[" + index + "]" : "";
                        string path = paths[0] + suffix;
                        VisualElement expected = Element(inspector, paths[2] + suffix).Q(className: BaseField<Enum>.inputUssClassName);
                        AssertNativeInput(Header(inspector, path), expected, path);
                    }
                    Toggle actual = HeaderToggle(inspector, path0);
                    Toggle native = HeaderToggle(inspector, native0);
                    VisualElement arrow = actual.Q(className: Toggle.checkmarkUssClassName);
                    VisualElement nativeArrow = native.Q(className: Toggle.checkmarkUssClassName);
                    Label label = actual.Q<Label>(className: Toggle.textUssClassName);
                    Label nativeLabel = native.Q<Label>(className: Toggle.textUssClassName);
                    Assert.That(IsVisible(arrow), Is.True, "The expansion arrow must be visible.");
                    Assert.That(arrow.worldBound.xMin, Is.EqualTo(nativeArrow.worldBound.xMin).Within(0.25f), "Native arrow indentation: " + path0);
                    Assert.That(label.worldBound.xMin, Is.EqualTo(nativeLabel.worldBound.xMin).Within(0.25f), "Native label indentation: " + path0);
                    Assert.That(arrow.worldBound.center.y - label.worldBound.center.y,
                        Is.EqualTo(nativeArrow.worldBound.center.y - nativeLabel.worldBound.center.y).Within(0.25f), "Native arrow/text vertical alignment.");
                    Assert.That(arrow.resolvedStyle.backgroundImage, Is.EqualTo(nativeArrow.resolvedStyle.backgroundImage), "The native expansion icon must be rendered.");
                    VisualElement picked = inspector.panel.Pick(arrow.worldBound.center);
                    Assert.That(picked == actual || actual.Contains(picked), Is.True,
                        "The visible expansion arrow must remain a real, unclipped input target: " + path0);
                    if (collection)
                    {
                        string nullPath = paths[0] + ".Array.data[1]";
                        Label nullLabel = HeaderToggle(inspector, nullPath).Q<Label>(className: Toggle.textUssClassName);
                        Assert.That(IsVisible(nullLabel) && nullLabel.text == "Element 1", Is.True, "Null elements must retain their visible element label.");
                    }
                    TestContext.Out.WriteLine("Native header: " + path0 + ";width=" + width + ";input=" + Header(inspector, path0).worldBound
                        + ";nativeInput=" + Element(inspector, paths[2] + (collection ? ".Array.data[0]" : "")).Q(className: BaseField<Enum>.inputUssClassName).worldBound
                        + ";arrow=" + arrow.worldBound + ";nativeArrow=" + nativeArrow.worldBound);
                }
                Toggle expansion = HeaderToggle(inspector, path0);
                Vector2 point = expansion.Q(className: Toggle.checkmarkUssClassName).worldBound.center;
                window.SendEvent(new Event { type = EventType.MouseDown, mousePosition = point, button = 0 });
                window.SendEvent(new Event { type = EventType.MouseUp, mousePosition = point, button = 0 });
                yield return WaitFor(() => !Selector(inspector, path0).value, "Clicking the real expansion arrow must close the body.");
                window.rootVisualElement.Clear();
                UnityEngine.Object.DestroyImmediate(comparisonEditor);
                comparisonEditor = null;
            }
        }

        private static void AssertNativeInput (VisualElement actual, VisualElement expected, string path)
        {
            Assert.That(actual.worldBound.xMin, Is.EqualTo(expected.worldBound.xMin).Within(0.25f), "Native input column: " + path);
            Assert.That(actual.worldBound.xMax, Is.EqualTo(expected.worldBound.xMax).Within(0.25f), "Native input right margin: " + path);
            Assert.That(actual.worldBound.height, Is.EqualTo(expected.worldBound.height).Within(0.25f), "Native dropdown height: " + path);
        }

        [UnityTest]
        public IEnumerator Collections_MatchNativePropertyFieldLayoutAcrossResizes ()
        {
            Asset.INode[] Nodes () => new Asset.INode[] { new Asset.PlainNode { value = 11 }, new Asset.PlainNode { value = 33 }, null };
            asset.array = Nodes();
            asset.list.AddRange(Nodes());
            asset.simpleArray = Nodes();
            asset.simpleList.AddRange(Nodes());
            asset.nativeArray = Nodes();
            asset.nativeList.AddRange(Nodes());
            asset.nativeSimpleArray = Nodes();
            asset.nativeSimpleList.AddRange(Nodes());
            comparisonEditor = UnityEditor.Editor.CreateEditor(asset, typeof(SubclassSelectorOnGUIComparisonEditor));
            var editor = (SubclassSelectorOnGUIComparisonEditor)comparisonEditor;
            var pairs = new[] { ("array", "nativeArray"), ("list", "nativeList"),
                ("simpleArray", "nativeSimpleArray"), ("simpleList", "nativeSimpleList") };
            editor.Paths = pairs.SelectMany(pair => new[] { pair.Item1, pair.Item2 }).ToArray();
            using (SerializedProperty property = editor.serializedObject.GetIterator())
            {
                while (property.NextVisible(true))
                {
                    property.isExpanded = !property.propertyPath.EndsWith(".Array.data[1]") && !property.propertyPath.EndsWith(".Array.data[2]");
                }
            }
            var inspector = new InspectorElement(comparisonEditor);
            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;
            scroll.Add(inspector);
            window.rootVisualElement.Add(scroll);
            string Path (string collection, int index) => collection + ".Array.data[" + index + "]";
            IntegerField Child (string collection) => Element(inspector, Path(collection, 0))?.Q<IntegerField>();
            VisualElement InputArea (IntegerField field) => field.Q<VisualElement>(className: BaseField<int>.inputUssClassName);
            bool Ready ()
            {
                foreach (Foldout foldout in inspector.Query<Foldout>().ToList())
                {
                    string path = foldout.bindingPath ?? string.Empty;
                    if (!path.EndsWith(".Array.data[1]") && !path.EndsWith(".Array.data[2]"))
                    {
                        foldout.value = true;
                    }
                }
                return pairs.All(pair => Child(pair.Item1) != null && Child(pair.Item2) != null
                    && Child(pair.Item2).worldBound.width > 0f);
            }
            foreach (float width in new[] { 520f, 700f })
            {
                window.position = new Rect(100, 100, width, 700);
                yield return WaitFor(() => Mathf.Abs(inspector.worldBound.width - scroll.contentViewport.worldBound.width) < 0.25f && Ready(),
                    "Paired native and selector collections must bind before comparing their bodies.", () => DescribeInputs(inspector));
                string previous = null;
                int stableFrames = 0;
                yield return WaitFor(() =>
                {
                    string current = string.Join(";", pairs.Select(pair => Child(pair.Item2).labelElement.worldBound.ToString()
                        + InputArea(Child(pair.Item2)).worldBound));
                    stableFrames = current == previous ? stableFrames + 1 : 0;
                    previous = current;
                    return stableFrames >= 3;
                }, "The native collection body must settle before comparison.");
                foreach (var pair in pairs)
                {
                    Assert.That(Child(pair.Item1).labelElement.worldBound.x,
                        Is.EqualTo(Child(pair.Item2).labelElement.worldBound.x).Within(0.25f), "Native child indentation: " + pair.Item1);
                    Assert.That(InputArea(Child(pair.Item1)).worldBound.x,
                        Is.EqualTo(InputArea(Child(pair.Item2)).worldBound.x).Within(0.25f), "Native child input column: " + pair.Item1);
                    Assert.That(InputArea(Child(pair.Item1)).worldBound.width,
                        Is.EqualTo(InputArea(Child(pair.Item2)).worldBound.width).Within(0.25f), "Native child width and right margin: " + pair.Item1);
                }
            }
            foreach (string path in new[] { "array", "list" })
            {
                string elementPath = Path(path, 1);
                Selector(inspector, elementPath).value = true;
                yield return WaitFor(() => VisibleInput(Element(inspector, elementPath), "Value", 33) != null,
                    "A closed collection element must become editable with one expand action.");
                VisibleInput(Element(inspector, elementPath), "Value", 33).value = 37;
                yield return WaitFor(() => ReadInt(asset, elementPath + ".value") == 37,
                    "The expanded row must retain its binding after resizing.");
                Assert.That(ReadInt(asset, Path(path, 0) + ".value"), Is.EqualTo(11));
                using (var read = new SerializedObject(asset))
                {
                    Assert.That(read.FindProperty(Path(path, 2)).managedReferenceValue, Is.Null);
                }
            }
            Child("simpleArray").value = 23;
            yield return WaitFor(() => ReadInt(asset, "simpleArray.Array.data[0].value") == 23,
                "The native-aligned selector must retain its binding after resizing.");
            Assert.That(ReadInt(asset, "simpleArray.Array.data[1].value"), Is.EqualTo(33));
        }

        [UnityTest]
        public IEnumerator PopupMenu_ForwardsToTheCurrentNativePropertyAfterReplacementReattachmentAndDisable ()
        {
            asset.reference = new Asset.PlainNode { value = 17 };
            asset.nativeReference = new Asset.PlainNode { value = 7 };
            PropertyField host = Bind(nameof(Asset.reference));
            var nativeControl = new PropertyField(boundObject.FindProperty("nativeReference.value"), "Native menu control");
            window.rootVisualElement.Add(nativeControl);
            nativeControl.Bind(boundObject);
            yield return WaitFor(() => VisibleInput(host, "Value", 17) != null && IsVisible(Header(host, "reference"))
                && Input(nativeControl, "Native menu control") != null,
                "The selector and native menu control must bind before requesting a menu.");

            var paths = new List<string>();
            var targets = new List<UnityEngine.Object>();
            void Observe (SerializedProperty property)
            {
                paths.Add(property.propertyPath);
                targets.Add(property.serializedObject.targetObject);
            }
            using (SerializedPropertyContextMenu.CaptureNativeRequests(window.rootVisualElement, Observe))
            {
                for (int stage = 0; stage < 4; stage++)
                {
                    if (stage == 1)
                    {
                        ReplaceReference(asset, "reference", new Asset.AlternateNode { other = 19 });
                        yield return WaitFor(() => VisibleInput(host, "Other", 19) != null && IsVisible(Header(host, "reference")),
                            "The replacement must finish binding.");
                    }
                    else if (stage == 2)
                    {
                        host.RemoveFromHierarchy();
                        yield return null;
                        window.rootVisualElement.Add(host);
                        yield return WaitFor(() => VisibleInput(host, "Other", 19) != null && IsVisible(Header(host, "reference")),
                            "The reattached selector must finish binding.");
                    }
                    else if (stage == 3)
                    {
                        host.SetEnabled(false);
                        nativeControl.SetEnabled(false);
                    }
                    foreach (bool control in new[] { true, false })
                    {
                        paths.Clear();
                        targets.Clear();
                        VisualElement input = control ? Input(nativeControl, "Native menu control").labelElement
                            : Header(host, "reference");
                        Assert.That(IsVisible(input), Is.True, "The requested menu target must be visible.");
                        window.Focus();
                        SerializedPropertyContextMenu.RequestFromUi(input);
                        yield return WaitFor(() => paths.Count > 0,
                            "The native bound property did not receive the menu request. Control=" + control + "; stage=" + stage);
                        string expectedPath = control ? "nativeReference.value" : "reference";
                        Assert.That(paths, Is.EqualTo(new[] { expectedPath }), "One request must reach the current native bound property.");
                        Assert.That(targets, Is.EqualTo(new UnityEngine.Object[] { asset }), "The request must use the bound asset.");
                        TestContext.Out.WriteLine("Menu routing: stage=" + stage + "; control=" + control + "; path=" + paths[0]);
                        yield return null;
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator NormalFields_EditSerializedValuesAndNotifyTheirHost ()
        {
            asset.reference = new Asset.PlainNode { value = 11 };
            PropertyField host = Bind(nameof(Asset.reference));
            int? notifiedValue = null;
            host.RegisterValueChangeCallback(evt => notifiedValue = ReadInt(asset, "reference.value"));

            yield return WaitFor(() => VisibleInput(host, "Value", 11) != null, "The selected subtype's native input was not displayed.");

            Input(host, "Value").value = 23;

            yield return WaitFor(() => ReadInt(asset, "reference.value") == 23 && notifiedValue == 23,
                "Editing a child must commit its value and notify the enclosing PropertyField.");
        }

        [UnityTest]
        public IEnumerator ToStringLabel_FollowsInputAndExternalValueChanges ()
        {
            asset.described = new Asset.PlainNode { value = 13 };
            PropertyField host = Bind(nameof(Asset.described));

            yield return WaitFor(() => VisibleInput(host, "Value", 13) != null && Selector(host, "described")?.text == "Value: 13",
                "UseToStringAsLabel did not label the expanded field.");

            Input(host, "Value").value = 29;
            yield return WaitFor(() => Selector(host, "described")?.text == "Value: 29",
                "The label did not follow an input edit.");

            WriteInt(asset, "described.value", 37);
            yield return WaitFor(() => VisibleInput(host, "Value", 37) != null && Selector(host, "described")?.text == "Value: 37",
                "The input and ToString label did not follow an external serialized edit.");
        }

        [UnityTest]
        public IEnumerator PropertyFieldLabel_CanBeCustomizedOrHidden ()
        {
            asset.reference = new Asset.PlainNode { value = 13 };
            PropertyField host = Bind(nameof(Asset.reference));
            host.label = "Custom reference";
            yield return WaitFor(() => Selector(host, "reference")?.text == "Custom reference"
                && VisibleInput(host, "Value", 13) != null,
                "The selector did not use its PropertyField's custom label.");

            host.label = string.Empty;
            yield return WaitFor(() => Selector(host, "reference") != null && string.IsNullOrEmpty(Selector(host, "reference").text)
                && VisibleInput(host, "Value", 13) != null,
                "An explicitly empty label must hide the selector label and keep its inputs.");
            Input(host, "Value").value = 17;
            yield return WaitFor(() => ReadInt(asset, "reference.value") == 17,
                "Changing the host label disconnected its editable fields.");
        }

        [UnityTest]
        public IEnumerator DecoratedInheritedFields_DrawTheirDecoratorAndKeepOtherFieldsNative ()
        {
            asset.reference = new Asset.DecoratedNode();
            int drawsBefore = Asset.ProbeDecoratorDrawer.DrawCount;
            PropertyField host = Bind(nameof(Asset.reference));
            int? notifiedValue = null;
            host.RegisterValueChangeCallback(evt => notifiedValue = ReadInt(asset, "reference.nativeValue"));
            yield return WaitFor(() => Asset.ProbeDecoratorDrawer.DrawCount > drawsBefore
                && Asset.ProbeDecoratorDrawer.LastHeading == "Inherited heading"
                && VisibleInput(host, "Native Value", 223) != null && VisibleInput(host, "Relative value", 227) != null
                && VisibleInput(host, "Generic value", 279) != null,
                "A decorator on an inherited private field must render without replacing its siblings' native or UITK-only drawers.");

            Input(host, "Native Value").value = 229;
            yield return WaitFor(() => ReadInt(asset, "reference.nativeValue") == 229 && notifiedValue == 229,
                "Editing a native sibling of a decorated field must commit and notify its host.");
            Input(host, "Relative value").value = 233;
            yield return WaitFor(() => ReadInt(asset, "reference.relative.value") == 233,
                "A decorated sibling must not disconnect a UITK-only drawer's relative input.");
            Input(host, "Generic value").value = 281;
            yield return WaitFor(() => ReadInt(asset, "reference.generic.value") == 281,
                "A decorated sibling must preserve an open-generic UITK-only drawer.");
            Assert.That(ReadInt(asset, "reference.decoratedValue"), Is.EqualTo(211));

            ReplaceReference(asset, "reference", new Asset.PlainNode { value = 239 });
            yield return WaitFor(() => VisibleInput(host, "Value", 239) != null,
                "A decorated subtype did not release its old fields after replacement.");
            drawsBefore = Asset.ProbeDecoratorDrawer.DrawCount;
            ReplaceReference(asset, "reference", new Asset.DecoratedNode { nativeValue = 241 });
            yield return WaitFor(() => Asset.ProbeDecoratorDrawer.DrawCount > drawsBefore
                && VisibleInput(host, "Native Value", 241) != null && VisibleInput(host, "Relative value", 227) != null,
                "Recreating a decorated subtype lost its decorator or native siblings.");
        }

        [UnityTest]
        public IEnumerator DecoratorsInInitiallyEmptyCompositeChildren_RenderAfterTheirContentsChange ()
        {
            asset.reference = new Asset.NestedDecoratedNode();
            PropertyField host = Bind(nameof(Asset.reference));
            yield return WaitFor(() => VisibleInput(host, "Native Value", 251) != null,
                "The native sibling of the empty child subtrees was not displayed.");

            yield return WaitFor(() => host.Query<Label>().ToList().Any(label => label.text == "Null reference drawer" && IsVisible(label)),
                "An attribute drawer must render an unannotated null reference.");

            using (var external = new SerializedObject(asset))
            {
                SerializedProperty details = external.FindProperty("reference.details");
                details.arraySize = 1;
                details.GetArrayElementAtIndex(0).FindPropertyRelative("value").intValue = 257;
                details.GetArrayElementAtIndex(0).FindPropertyRelative("relative.value").intValue = 521;
                SerializedProperty unannotated = external.FindProperty("reference.unannotated");
                unannotated.managedReferenceValue = new Asset.RuntimeDecoratedNode();
                external.ApplyModifiedProperties();
            }
            // Expansion is Inspector state on its owning SerializedObject, rather than asset
            // data to copy from the separate object used for the external edit.
            boundObject.Update();
            boundObject.FindProperty("reference.details").isExpanded = true;
            boundObject.FindProperty("reference.details.Array.data[0]").isExpanded = true;
            boundObject.FindProperty("reference.unannotated").isExpanded = true;
            yield return WaitFor(() => Asset.ProbeDecoratorDrawer.DrawnHeadings.Contains("Nested heading")
                && Asset.ProbeDecoratorDrawer.DrawnHeadings.Contains("Runtime heading")
                && Asset.ProbeDecoratorDrawer.DrawnHeadings.Contains("Nested UITK heading")
                && VisibleInput(Element(host, "reference.details.Array.data[0].relative"), "Relative value", 521) != null
                && VisibleInput(Element(host, "reference.unannotated.relative"), "Relative value", 523) != null
                && VisibleInput(host, "Native Value", 251) != null,
                "New list entries and runtime reference subtypes must render their decorators without replacing the parent reference.",
                () => "Rendered headings: " + string.Join(", ", Asset.ProbeDecoratorDrawer.DrawnHeadings)
                    + "; foldouts: " + string.Join(", ", host.Query<Foldout>().ToList().Select(f => f.bindingPath + "=" + f.value))
                    + "; IMGUI: " + string.Join(", ", host.Query<IMGUIContainer>().ToList().Select(c => c.worldBound.ToString())));

            Input(host, "Native Value").value = 269;
            yield return WaitFor(() => ReadInt(asset, "reference.nativeValue") == 269,
                "Composite child changes disconnected the native sibling.");
            Assert.That(ReadInt(asset, "reference.details.Array.data[0].value"), Is.EqualTo(257));
            Assert.That(ReadInt(asset, "reference.unannotated.value"), Is.EqualTo(263));
            Input(Element(host, "reference.details.Array.data[0].relative"), "Relative value").value = 547;
            Input(Element(host, "reference.unannotated.relative"), "Relative value").value = 557;
            yield return WaitFor(() => ReadInt(asset, "reference.details.Array.data[0].relative.value") == 547
                && ReadInt(asset, "reference.unannotated.relative.value") == 557,
                "UITK-only descendants of a collection row and an unannotated reference must save independently.");

            using (var external = new SerializedObject(asset))
            {
                external.FindProperty("reference.details.Array.data[0].reference").managedReferenceValue = new Asset.DeepRuntimeDecoratedNode();
                external.ApplyModifiedProperties();
            }
            boundObject.Update();
            boundObject.FindProperty("reference.details.Array.data[0].reference").isExpanded = true;
            yield return WaitFor(() => Asset.ProbeDecoratorDrawer.DrawnHeadings.Contains("Deep runtime heading")
                && VisibleInput(Element(host, "reference.details.Array.data[0].reference.relative"), "Relative value", 541) != null
                && VisibleInput(host, "Native Value", 269) != null,
                "A null reference deeper in a list entry must display its new subtype without changing its parent's type or collection size.");
            Assert.That(ReadInt(asset, "reference.details.Array.data[0].reference.value"), Is.EqualTo(283));
            Input(Element(host, "reference.details.Array.data[0].reference.relative"), "Relative value").value = 563;
            yield return WaitFor(() => ReadInt(asset, "reference.details.Array.data[0].reference.relative.value") == 563,
                "A UITK-only descendant added under a deep null reference must bind to its new instance.");
            ReplaceReference(asset, "reference.details.Array.data[0].reference", new Asset.DeepRuntimeDecoratedNode
            {
                relative = new Asset.RelativeNode { value = 569 }
            });
            yield return WaitFor(() => VisibleInput(Element(host, "reference.details.Array.data[0].reference.relative"), "Relative value", 569) != null,
                "A deep same-type replacement must recreate the current UITK-only binding.");
            Input(Element(host, "reference.details.Array.data[0].reference.relative"), "Relative value").value = 571;
            yield return WaitFor(() => ReadInt(asset, "reference.details.Array.data[0].reference.relative.value") == 571,
                "The deep replacement's UITK-only input wrote to its previous instance.");
        }

        [UnityTest]
        public IEnumerator CompositeCollections_AddRemoveAndUndoKeepUIToolkitRowsBound ()
        {
            asset.reference = new Asset.CompositeCollectionsNode();
            PropertyField host = Bind(nameof(Asset.reference));
            string[] paths = { "reference.array", "reference.list" };
            yield return WaitFor(() => paths.All(path => host.Query<ListView>().ToList().Any(list => list.bindingPath == path && list.itemsSource != null)),
                "Both composite collections must bind before their controls are operated.");
            for (int i = 0; i < paths.Length; i++)
            {
                string path = paths[i];
                int offset = 100 * i;
                boundObject.FindProperty(path).isExpanded = true;
                foreach (string otherPath in paths.Where(value => value != path))
                {
                    boundObject.FindProperty(otherPath).isExpanded = false;
                }
                foreach (ListView list in host.Query<ListView>().ToList())
                {
                    Foldout header = list.Q<Foldout>(className: ListView.foldoutHeaderUssClassName);
                    if (header != null)
                    {
                        header.value = list.bindingPath == path;
                    }
                }
                for (int count = 1; count <= 2; count++)
                {
                    yield return WaitFor(() => host.Query<ListView>().ToList().Any(list => list.bindingPath == path && list.itemsSource != null)
                        && CollectionButton(host, path, "+") is Button button && IsVisible(button) && button.enabledInHierarchy,
                        "The bound collection's add button must be available: " + path + ", adding row " + count,
                        () => "Size: " + ReadArraySize(asset, path) + "; lists: "
                            + string.Join(", ", host.Query<ListView>().ToList().Select(list => list.bindingPath + ": bound=" + (list.itemsSource != null)
                                + ", add visible=" + IsVisible(CollectionButton(host, list.bindingPath, "+")))));
                    Undo.IncrementCurrentGroup();
                    Submit(CollectionButton(host, path, "+"));
                    yield return WaitFor(() => ReadArraySize(asset, path) == count,
                        "The native add button did not add a serialized row.");
                }
                using (var external = new SerializedObject(asset))
                {
                    external.FindProperty(path + ".Array.data[0].relative.value").intValue = 601 + offset;
                    external.FindProperty(path + ".Array.data[1].relative.value").intValue = 607 + offset;
                    external.ApplyModifiedProperties();
                }
                boundObject.Update();
                boundObject.FindProperty(path).isExpanded = true;
                yield return WaitFor(() => Selector(host, path + ".Array.data[0]") != null
                    && Selector(host, path + ".Array.data[1]") != null,
                    "The collection did not create its composite row foldouts.");
                boundObject.FindProperty(path + ".Array.data[0]").isExpanded = true;
                boundObject.FindProperty(path + ".Array.data[1]").isExpanded = true;
                Selector(host, path + ".Array.data[0]").value = true;
                Selector(host, path + ".Array.data[1]").value = true;
                yield return WaitFor(() => VisibleInput(Element(host, path + ".Array.data[0].relative"), "Relative value", 601 + offset) != null
                    && VisibleInput(Element(host, path + ".Array.data[1].relative"), "Relative value", 607 + offset) != null,
                    "Both added composite rows must display their UITK-only descendant.",
                    () => DescribeInputs(host));
                // Reorder through a separate SerializedObject, as another editor tool would.
                using (var external = new SerializedObject(asset))
                {
                    external.FindProperty(path).MoveArrayElement(0, 1);
                    external.ApplyModifiedProperties();
                }
                yield return WaitFor(() => VisibleInput(Element(host, path + ".Array.data[0].relative"), "Relative value", 607 + offset) != null
                    && VisibleInput(Element(host, path + ".Array.data[1].relative"), "Relative value", 601 + offset) != null,
                    "Reordering must rebind composite rows to their current serialized paths.");
                Input(Element(host, path + ".Array.data[1].relative"), "Relative value").value = 613 + offset;
                yield return WaitFor(() => ReadInt(asset, path + ".Array.data[1].relative.value") == 613 + offset,
                    "The reordered UITK-only descendant did not edit its current row.");
                Assert.That(ReadInt(asset, path + ".Array.data[0].relative.value"), Is.EqualTo(607 + offset));
                Undo.IncrementCurrentGroup();
                ListView control = host.Query<ListView>().ToList().First(list => list.bindingPath == path);
                control.SetSelection(CollectionIndex(control, path + ".Array.data[0]"));
                Submit(CollectionButton(host, path, "-"));
                yield return WaitFor(() => ReadArraySize(asset, path) == 1
                    && VisibleInput(Element(host, path + ".Array.data[0].relative"), "Relative value", 613 + offset) != null,
                    "Removing a row must keep the surviving UITK-only descendant bound.");
                Undo.PerformUndo();
                yield return WaitFor(() => ReadArraySize(asset, path) == 2
                    && ReadInt(asset, path + ".Array.data[0].relative.value") == 607 + offset
                    && ReadInt(asset, path + ".Array.data[1].relative.value") == 613 + offset
                    && VisibleInput(Element(host, path + ".Array.data[0].relative"), "Relative value", 607 + offset) != null
                    && VisibleInput(Element(host, path + ".Array.data[1].relative"), "Relative value", 613 + offset) != null,
                    "Undo must restore the removed row and both current UITK-only bindings.");
            }
            Assert.That(ReadInt(asset, "reference.array.Array.data[1].relative.value"), Is.EqualTo(613));
            Assert.That(ReadInt(asset, "reference.list.Array.data[1].relative.value"), Is.EqualTo(713));
        }

        [UnityTest]
        public IEnumerator CompositeDrawer_DiscardedDescendantStopsTrackingValues ()
        {
            asset.reference = new Asset.TrackedCompositeNode();
            PropertyField host = Bind(nameof(Asset.reference));
            yield return WaitFor(() => host.Query<Label>().ToList().Any(label => label.text == "Detail value: 751"),
                "The child drawer's descendant tracker did not bind.");
            Label previous = host.Query<Label>().ToList().First(label => label.text == "Detail value: 751");
            ReplaceReference(asset, "reference", new Asset.TrackedCompositeNode { details = new Asset.TrackedDetails { value = 757 } });
            yield return WaitFor(() => previous.panel == null
                && host.Query<Label>().ToList().Any(label => label.text == "Detail value: 757"),
                "Replacing the parent must discard its old drawer and bind the new descendant tracker.");
            string releasedText = previous.text;
            WriteInt(asset, "reference.details.value", 761);
            yield return WaitFor(() => ReadInt(asset, "reference.details.value") == 761
                && host.Query<Label>().ToList().Any(label => label.text == "Detail value: 761"),
                "The current drawer must observe its current serialized value.");
            Assert.That(previous.text, Is.EqualTo(releasedText),
                "A discarded descendant must stop receiving serialized value changes.");
        }

#if UNITY_2022_2_OR_NEWER
        [UnityTest]
        public IEnumerator CompositeDecorators_KeepUIToolkitHeadingsAndRelativeInputs ()
        {
            asset.reference = new Asset.UIToolkitDecoratedCompositeNode();
            PropertyField host = Bind(nameof(Asset.reference));
            boundObject.FindProperty("reference.details").isExpanded = true;
            boundObject.FindProperty("reference.rows").isExpanded = true;
            boundObject.FindProperty("reference.rows.Array.data[0]").isExpanded = true;
            yield return WaitFor(() => Selector(host, "reference.details") != null
                && Selector(host, "reference.rows.Array.data[0]") != null,
                "The decorated composite groups must be available before opening them.");
            Selector(host, "reference.details").value = true;
            Selector(host, "reference.rows.Array.data[0]").value = true;
            yield return WaitFor(() => host.Query<Label>().ToList().Any(label => label.text == "UITK group heading")
                && host.Query<Label>().ToList().Any(label => label.text == "UITK collection heading")
                && VisibleInput(Element(host, "reference.details.relative"), "Relative value", 521) != null
                && VisibleInput(Element(host, "reference.rows.Array.data[0].relative"), "Relative value", 521) != null,
                "Owned groups and collections must retain UITK-only decorator headings and relative child inputs.",
                () => DescribeInputs(host) + "; labels: " + string.Join(", ", host.Query<Label>().ToList().Select(label => label.text)));
            Input(Element(host, "reference.details.relative"), "Relative value").value = 733;
            Input(Element(host, "reference.rows.Array.data[0].relative"), "Relative value").value = 739;
            yield return WaitFor(() => ReadInt(asset, "reference.details.relative.value") == 733
                && ReadInt(asset, "reference.rows.Array.data[0].relative.value") == 739,
                "UITK-only decorators must not disturb their composite children's relative bindings.");
        }
#endif

        [UnityTest]
        public IEnumerator DecoratorsOnTheSelector_KeepUIToolkitOnlyInputsBound ()
        {
            asset.decoratedReference = new Asset.RelativeNode { value = 293 };
            int drawsBefore = Asset.ProbeDecoratorDrawer.DrawCount;
            PropertyField host = Bind(nameof(Asset.decoratedReference));
            host.label = "Decorated UITK selector";
            int? notifiedValue = null;
            host.RegisterValueChangeCallback(evt => notifiedValue = ReadInt(asset, "decoratedReference.value"));
            yield return WaitFor(() => Asset.ProbeDecoratorDrawer.DrawCount > drawsBefore
                && Asset.ProbeDecoratorDrawer.LastHeading == "Selector heading"
                && Selector(host, "decoratedReference")?.text == "Decorated UITK selector"
                && VisibleInput(host, "Relative value", 293) != null,
                "The selector's decorator and custom label must preserve a UITK-only subtype drawer.");

            Input(host, "Relative value").value = 307;
            yield return WaitFor(() => ReadInt(asset, "decoratedReference.value") == 307 && notifiedValue == 307,
                "A decorated selector's UITK-only input must commit and notify its host.");

            ReplaceReference(asset, "decoratedReference", new Asset.PlainNode { value = 311 });
            yield return WaitFor(() => VisibleInput(host, "Value", 311) != null,
                "A decorated selector did not replace its UITK-only subtype with native inputs.");
            drawsBefore = Asset.ProbeDecoratorDrawer.DrawCount;
            ReplaceReference(asset, "decoratedReference", new Asset.RelativeNode { value = 313 });
            yield return WaitFor(() => Asset.ProbeDecoratorDrawer.DrawCount > drawsBefore
                && VisibleInput(host, "Relative value", 313) != null,
                "Returning to a UITK-only subtype must retain its decorator and relative binding.");
            Input(host, "Relative value").value = 317;
            yield return WaitFor(() => ReadInt(asset, "decoratedReference.value") == 317,
                "The recreated UITK-only drawer lost its editable binding.");
        }

        [UnityTest]
        public IEnumerator DecoratorsOnTheSelector_KeepNestedUIToolkitOnlyInputsBound ()
        {
            asset.reference = new Asset.DecoratedSelectorNode { child = new Asset.RelativeNode { value = 331 } };
            int drawsBefore = Asset.ProbeDecoratorDrawer.DrawCount;
            PropertyField host = Bind(nameof(Asset.reference));
            yield return WaitFor(() => Selector(host, "reference.child") != null && IsVisible(Selector(host, "reference.child")),
                "The nested selector's header was not displayed.");
            // This case verifies the decorated body's binding, rather than startup expansion
            // synchronization. Establish the opened state on the public control and property.
            boundObject.FindProperty("reference.child").isExpanded = true;
            Selector(host, "reference.child").value = true;
            yield return WaitFor(() => Asset.ProbeDecoratorDrawer.DrawCount > drawsBefore
                && Asset.ProbeDecoratorDrawer.LastHeading == "Nested selector heading"
                && VisibleInput(host, "Relative value", 331) != null,
                "A nested selector's decorator must preserve its UITK-only subtype drawer.",
                () => "Rendered headings: " + string.Join(", ", Asset.ProbeDecoratorDrawer.DrawnHeadings)
                    + "; expanded property: " + boundObject.FindProperty("reference.child").isExpanded
                    + "; foldouts: " + string.Join(", ", host.Query<Foldout>().ToList().Select(field => field.bindingPath + "=" + field.value + " " + field.worldBound))
                    + "; inputs: " + string.Join(", ", host.Query<IntegerField>().ToList().Select(field => field.label + "=" + field.value + ", visible=" + IsVisible(field)))
                    + "; property fields: " + string.Join(", ", host.Query<PropertyField>().ToList().Select(field => field.bindingPath)));

            Input(host, "Relative value").value = 337;
            yield return WaitFor(() => ReadInt(asset, "reference.child.value") == 337,
                "The decorated nested selector lost its relative input binding.");
            ReplaceReference(asset, "reference.child", new Asset.RelativeNode { value = 347 });
            yield return WaitFor(() => VisibleInput(host, "Relative value", 347) != null,
                "A decorated nested selector did not follow replacement of its subtype instance.");
            Input(host, "Relative value").value = 349;
            yield return WaitFor(() => ReadInt(asset, "reference.child.value") == 349,
                "The replacement nested drawer did not save to its current property path.");
        }

        [UnityTest]
        public IEnumerator DecoratedChildCollections_KeepUIToolkitOnlyInputsAfterAddingReplacingAndReordering ()
        {
            asset.reference = new Asset.DecoratedCollectionNode();
            PropertyField host = Bind(nameof(Asset.reference));
            yield return WaitFor(() => Asset.ProbeDecoratorDrawer.DrawnHeadings.Contains("Array selector heading")
                && Asset.ProbeDecoratorDrawer.DrawnHeadings.Contains("List selector heading")
                && Selector(host, "reference.array") != null && Selector(host, "reference.list") != null,
                "Both initially empty child collections must display their own decorator and native foldout.");

            string[] collections = { "reference.array", "reference.list" };
            for (int i = 0; i < collections.Length; i++)
            {
                string collectionPath = collections[i];
                string firstPath = collectionPath + ".Array.data[0]";
                string secondPath = collectionPath + ".Array.data[1]";
                int offset = i * 100;
                boundObject.FindProperty(collectionPath).isExpanded = true;
                Selector(host, collectionPath).value = true;
                using (var external = new SerializedObject(asset))
                {
                    SerializedProperty collection = external.FindProperty(collectionPath);
                    collection.arraySize = 2;
                    collection.GetArrayElementAtIndex(0).managedReferenceValue = new Asset.RelativeNode { value = 367 + offset };
                    collection.GetArrayElementAtIndex(1).managedReferenceValue = new Asset.PlainNode { value = 373 + offset };
                    external.ApplyModifiedProperties();
                }
                yield return WaitFor(() => Selector(host, firstPath) != null && Selector(host, secondPath) != null,
                    "Adding to an empty decorated collection did not create its element selectors.");
                boundObject.FindProperty(firstPath).isExpanded = true;
                boundObject.FindProperty(secondPath).isExpanded = true;
                Selector(host, firstPath).value = true;
                Selector(host, secondPath).value = true;
                yield return WaitFor(() => VisibleInput(Element(host, firstPath), "Relative value", 367 + offset) != null
                    && VisibleInput(Element(host, secondPath), "Value", 373 + offset) != null,
                    "Decorated collections must keep both UITK-only and native element inputs.");

                int? notifiedValue = null;
                host.RegisterValueChangeCallback(evt => notifiedValue = ReadInt(asset, firstPath + ".value"));
                Input(Element(host, firstPath), "Relative value").value = 379 + offset;
                yield return WaitFor(() => ReadInt(asset, firstPath + ".value") == 379 + offset && notifiedValue == 379 + offset,
                    "The added UITK-only element must save its value and notify the selector host.");
                Assert.That(ReadInt(asset, secondPath + ".value"), Is.EqualTo(373 + offset));

                ReplaceReference(asset, firstPath, new Asset.RelativeNode { value = 389 + offset });
                yield return WaitFor(() => VisibleInput(Element(host, firstPath), "Relative value", 389 + offset) != null,
                    "The collection's UITK-only element did not follow external replacement.");
                Input(Element(host, firstPath), "Relative value").value = 397 + offset;
                yield return WaitFor(() => ReadInt(asset, firstPath + ".value") == 397 + offset,
                    "The replacement UITK-only element lost its editable binding.");

                using (var external = new SerializedObject(asset))
                {
                    external.FindProperty(collectionPath).MoveArrayElement(0, 1);
                    external.ApplyModifiedProperties();
                }
                yield return WaitFor(() => VisibleInput(Element(host, firstPath), "Value", 373 + offset) != null
                    && VisibleInput(Element(host, secondPath), "Relative value", 397 + offset) != null,
                    "Reordering must update each element selector and its current subtype input.");
                Input(Element(host, secondPath), "Relative value").value = 401 + offset;
                yield return WaitFor(() => ReadInt(asset, secondPath + ".value") == 401 + offset,
                    "The reordered UITK-only drawer wrote to its previous row.");
                Assert.That(ReadInt(asset, firstPath + ".value"), Is.EqualTo(373 + offset));
            }

            Assert.That(ReadInt(asset, "reference.array.Array.data[0].value"), Is.EqualTo(373));
            Assert.That(ReadInt(asset, "reference.array.Array.data[1].value"), Is.EqualTo(401));
            Assert.That(ReadInt(asset, "reference.list.Array.data[0].value"), Is.EqualTo(473));
            Assert.That(ReadInt(asset, "reference.list.Array.data[1].value"), Is.EqualTo(501));
        }

        [UnityTest]
        public IEnumerator DecoratedChildDrawers_KeepUIToolkitTypeGenericAndAttributeInputsBound ()
        {
            asset.reference = new Asset.DecoratedUIToolkitNode();
            PropertyField host = Bind(nameof(Asset.reference));
            int? notifiedValue = null;
            host.RegisterValueChangeCallback(evt => notifiedValue = ReadInt(asset, "reference.relative.value"));
            yield return WaitFor(() => Asset.ProbeDecoratorDrawer.DrawnHeadings.Contains("UITK type heading")
                && Asset.ProbeDecoratorDrawer.DrawnHeadings.Contains("UITK generic heading")
                && Asset.ProbeDecoratorDrawer.DrawnHeadings.Contains("UITK attribute heading")
                && VisibleInput(Element(host, "reference.relative"), "Relative value", 409) != null
                && VisibleInput(Element(host, "reference.generic"), "Generic value", 419) != null
                && VisibleInput(Element(host, "reference.annotated"), "UITK attribute value", 421) != null,
                "Decorators must preserve UITK-only type, generic type and primitive attribute drawers together.");

            Input(Element(host, "reference.relative"), "Relative value").value = 431;
            Input(Element(host, "reference.generic"), "Generic value").value = 433;
            Input(Element(host, "reference.annotated"), "UITK attribute value").value = 439;
            yield return WaitFor(() => ReadInt(asset, "reference.relative.value") == 431 && notifiedValue == 431
                && ReadInt(asset, "reference.generic.value") == 433 && ReadInt(asset, "reference.annotated") == 439,
                "Each decorated UITK-only child input must save to its own property and notify the host.");

            WriteInt(asset, "reference.relative.value", 457);
            yield return WaitFor(() => VisibleInput(Element(host, "reference.relative"), "Relative value", 457) != null,
                "The decorated UITK-only child did not follow an external value update.");
            Input(Element(host, "reference.relative"), "Relative value").value = 463;
            yield return WaitFor(() => ReadInt(asset, "reference.relative.value") == 463,
                "The decorated child lost its editable binding after an external update.");
            Assert.That(ReadInt(asset, "reference.generic.value"), Is.EqualTo(433));
            Assert.That(ReadInt(asset, "reference.annotated"), Is.EqualTo(439));

            ReplaceReference(asset, "reference", new Asset.DecoratedUIToolkitNode
            {
                relative = new Asset.RelativeNode { value = 479 },
                generic = new Asset.GenericNode<int> { value = 487 },
                annotated = 491
            });
            yield return WaitFor(() => VisibleInput(Element(host, "reference.relative"), "Relative value", 479) != null
                && VisibleInput(Element(host, "reference.generic"), "Generic value", 487) != null
                && VisibleInput(Element(host, "reference.annotated"), "UITK attribute value", 491) != null,
                "All decorated UITK-only drawers must bind to the replacement parent instance.");
            Input(Element(host, "reference.relative"), "Relative value").value = 499;
            Input(Element(host, "reference.generic"), "Generic value").value = 503;
            Input(Element(host, "reference.annotated"), "UITK attribute value").value = 509;
            yield return WaitFor(() => ReadInt(asset, "reference.relative.value") == 499
                && ReadInt(asset, "reference.generic.value") == 503 && ReadInt(asset, "reference.annotated") == 509,
                "Recreated decorated child drawers must keep independent current bindings.");
        }

        [UnityTest]
        public IEnumerator DecoratorsOnTheSelector_RenderAndKeepTheExactSubtypeDrawerCurrent ()
        {
            asset.decoratedReference = new Asset.ObservedNode { value = 271 };
            PropertyField host = Bind(nameof(Asset.decoratedReference));
            host.label = "Decorated selector";
            yield return WaitFor(() => Asset.ProbeDecoratorDrawer.DrawnHeadings.Contains("Selector heading")
                && Asset.ObservedNodeDrawer.LastValue == 271 && Asset.ObservedNodeDrawer.LastLabel == "Decorated selector",
                "The selector's decorator, custom label and exact subtype drawer must render together.");

            ReplaceReference(asset, "decoratedReference", new Asset.ObservedNode { value = 277 });
            yield return WaitFor(() => Asset.ObservedNodeDrawer.LastValue == 277,
                "The decorated selector's drawer did not follow a same-type replacement.");
            Undo.PerformUndo();
            yield return WaitFor(() => Asset.ObservedNodeDrawer.LastValue == 271,
                "Undo did not refresh the decorated selector's subtype drawer.");
        }

        [UnityTest]
        public IEnumerator IMGUIDrawer_MatchesTheNativeOnGUIBodyAcrossInspectorResizes ()
        {
            asset.nativeReference = new Asset.NativeInputLayoutNode { body = new Asset.InputLayoutNode { value = 17 } };
            asset.reference = new Asset.InputLayoutNode { value = 17 };
            boundObject = new SerializedObject(asset);
            boundObject.FindProperty("nativeReference").isExpanded = true;
            boundObject.FindProperty("reference").isExpanded = true;
            yield return CompareNativeOnGUIBody("nativeReference", "nativeReference.body", "reference", "reference");
        }

        [UnityTest]
        public IEnumerator IMGUIDrawer_SimpleListMatchesTheNativeOnGUIBodyAcrossInspectorResizes ()
        {
            asset.nativeSimpleList.AddRange(new Asset.INode[]
            {
                new Asset.PlainNode { value = 11 }, new Asset.PlainNode { value = 22 },
                new Asset.NativeInputLayoutNode { body = new Asset.InputLayoutNode { value = 33 } }
            });
            asset.simpleList.AddRange(new Asset.INode[]
            {
                new Asset.PlainNode { value = 11 }, new Asset.PlainNode { value = 22 }, new Asset.InputLayoutNode { value = 33 }
            });
            boundObject = new SerializedObject(asset);
            foreach (string path in new[] { "nativeSimpleList", "simpleList" })
            {
                SerializedProperty collection = boundObject.FindProperty(path);
                collection.isExpanded = true;
                collection.GetArrayElementAtIndex(0).isExpanded = true;
                collection.GetArrayElementAtIndex(1).isExpanded = false;
                collection.GetArrayElementAtIndex(2).isExpanded = true;
            }
            yield return CompareNativeOnGUIBody("nativeSimpleList", "nativeSimpleList.Array.data[2].body", "simpleList", "simpleList.Array.data[2]");
        }

        private IEnumerator CompareNativeOnGUIBody (string nativeCollection, string nativeBody, string selectorCollection, string selectorBody)
        {
            comparisonEditor = UnityEditor.Editor.CreateEditor(asset, typeof(SubclassSelectorOnGUIComparisonEditor));
            var editor = (SubclassSelectorOnGUIComparisonEditor)comparisonEditor;
            editor.NativePath = nativeCollection;
            editor.SelectorPath = selectorCollection;
            var inspector = new InspectorElement(comparisonEditor);
            window.rootVisualElement.Add(inspector);

            foreach (float width in new[] { 520f, 700f })
            {
                Asset.InputLayoutNodeDrawer.InputScreenRects.Clear();
                Asset.InputLayoutNodeDrawer.LabelScreenRects.Clear();
                window.position = new Rect(100, 100, width, 700);
                yield return WaitFor(() => Asset.InputLayoutNodeDrawer.InputScreenRects.ContainsKey(nativeBody)
                    && Asset.InputLayoutNodeDrawer.InputScreenRects.ContainsKey(selectorBody)
                    && Mathf.Abs(inspector.worldBound.width - width) < 0.25f,
                    "Both instances of the same OnGUI drawer must render inside their native Foldout bodies.");
                Rect previous = Rect.zero;
                int stableFrames = 0;
                yield return WaitFor(() =>
                {
                    Rect current = Asset.InputLayoutNodeDrawer.InputScreenRects[nativeBody];
                    stableFrames = current == previous ? stableFrames + 1 : 0;
                    previous = current;
                    return stableFrames >= 3;
                }, "The unmodified native OnGUI reference must finish layout before comparison.");

                // The reference is generated by Unity from an unannotated managed-reference
                // Foldout and the same OnGUI-only drawer. It receives no layout overrides.
                Rect expected = Asset.InputLayoutNodeDrawer.InputScreenRects[nativeBody];
                Rect actual = Asset.InputLayoutNodeDrawer.InputScreenRects[selectorBody];
                Assert.That(actual.xMin, Is.EqualTo(expected.xMin).Within(0.25f),
                    "The input must keep the native OnGUI column at Inspector width " + width);
                Assert.That(actual.xMax, Is.EqualTo(expected.xMax).Within(0.25f),
                    "The input must keep the native OnGUI right margin at Inspector width " + width);
                Assert.That(actual.height, Is.EqualTo(expected.height).Within(0.25f));
                Assert.That(Asset.InputLayoutNodeDrawer.LabelScreenRects[selectorBody].x,
                    Is.EqualTo(Asset.InputLayoutNodeDrawer.LabelScreenRects[nativeBody].x).Within(0.25f),
                    "The label must keep the native OnGUI indentation at Inspector width " + width);
            }
        }

        [UnityTest]
        public IEnumerator IMGUIDrawer_KeepsItsLabelPositionAfterAnotherIMGUIEditorDraws ()
        {
            asset.reference = new Asset.InputLayoutNode { value = 17 };
            Asset.InputLayoutNodeDrawer.DrawCount = 0;
            int originalIndent = EditorGUI.indentLevel;
            int otherIndent = 0;
            var otherEditor = new IMGUIContainer(() =>
            {
                GUILayout.Label("Another native editor");
                EditorGUI.indentLevel = otherIndent;
            });
            try
            {
                window.rootVisualElement.Insert(0, otherEditor);
                PropertyField host = Bind(nameof(Asset.reference));
                yield return WaitFor(() => Asset.InputLayoutNodeDrawer.DrawCount > 0,
                    "Both editors must render with the initial GUI state before comparing labels.");
                IMGUIContainer body = Body(host, "reference");
                float originalX = body.LocalToWorld(Asset.InputLayoutNodeDrawer.LabelRect.position).x;
                int previousDraws = Asset.InputLayoutNodeDrawer.DrawCount;
                otherIndent = 1;
                otherEditor.MarkDirtyRepaint();
                yield return WaitFor(() => Asset.InputLayoutNodeDrawer.DrawCount > previousDraws,
                    "The subtype label must redraw after the other editor changes its GUI state.");
                Assert.That(body.LocalToWorld(Asset.InputLayoutNodeDrawer.LabelRect.position).x,
                    Is.EqualTo(originalX).Within(0.25f),
                    "A separate IMGUI editor must not change the subtype label's indentation.");
            }
            finally
            {
                otherEditor.RemoveFromHierarchy();
                EditorGUI.indentLevel = originalIndent;
            }
        }

        [UnityTest]
        public IEnumerator IMGUIDrawerButton_CommitsAndNotifiesCurrentValues_ThenUndoes ()
        {
            asset.described = new Asset.InputNode { value = 271 };
            Asset.InputNodeDrawer.ScreenRect = Rect.zero;
            PropertyField host = Bind(nameof(Asset.described));
            bool afterClick = false;
            var notifications = new List<(int reported, int saved, bool afterClick)>();
            host.RegisterValueChangeCallback(evt => notifications.Add((
                evt.changedProperty.FindPropertyRelative("value").intValue, ReadInt(asset, "described.value").Value, afterClick)));
            yield return WaitFor(() => Asset.InputNodeDrawer.ScreenRect.width > 0f && Selector(host, "described")?.text == "Input: 271",
                "The IMGUI button did not render.");

            Rect input = Body(host, "described").worldBound;
            Vector2 point = input.center;
            Undo.IncrementCurrentGroup();
            afterClick = true;
            window.SendEvent(new Event { type = EventType.MouseDown, mousePosition = point, button = 0, clickCount = 1 });
            window.SendEvent(new Event { type = EventType.MouseUp, mousePosition = point, button = 0 });
            yield return WaitFor(() => ReadInt(asset, "described.value") == 281 && Selector(host, "described")?.text == "Input: 281"
                && notifications.Contains((281, 281, true)),
                "The IMGUI button did not commit, refresh its label and notify the PropertyField.");
            Assert.That(notifications.All(value => value.reported == value.saved), Is.True,
                "Every host callback must report its current saved value: " + string.Join(", ", notifications));

            Undo.PerformUndo();
            yield return WaitFor(() => ReadInt(asset, "described.value") == 271 && Selector(host, "described")?.text == "Input: 271",
                "Undo did not restore the value edited through IMGUI.");
        }

        [UnityTest]
        public IEnumerator IMGUIChangeNotification_NativeDrawerKeepsWriterEditsVisibleToAncestors ()
            => CheckIMGUIChangeNotification(nameof(Asset.nativeReference));

        [UnityTest]
        public IEnumerator IMGUIChangeNotification_SelectorKeepsWriterEditsVisibleToAncestors ()
            => CheckIMGUIChangeNotification(nameof(Asset.reference));

        private IEnumerator CheckIMGUIChangeNotification (string path)
        {
            foreach (bool removeBody in new[] { false, true })
            {
                window.rootVisualElement.Unbind();
                window.rootVisualElement.Clear();
                boundObject?.Dispose();
                boundObject = null;
                Asset.SelfApplyingNodeDrawer.ApplyValue = null;
                Asset.SelfApplyingNodeDrawer.Repainted = false;
                using (var setup = new SerializedObject(asset))
                {
                    setup.FindProperty(nameof(Asset.reference)).managedReferenceValue = new Asset.SelfApplyingNode { value = 271 };
                    setup.FindProperty(nameof(Asset.nativeReference)).managedReferenceValue = new Asset.SelfApplyingNode { value = 271 };
                    setup.ApplyModifiedPropertiesWithoutUndo();
                }
                PropertyField host = Bind(path);
                bool editedDuringDelivery = false;
                var observations = new List<(int reported, int saved)>();
                host.RegisterValueChangeCallback(evt =>
                {
                    using (SerializedProperty value = evt.changedProperty.FindPropertyRelative("value"))
                    {
                        if (editedDuringDelivery || value.intValue != 281)
                        {
                            return;
                        }
                    }
                    // A consumer invokes the same cache supplied to the public OnGUI drawer.
                    // Later receivers must observe that committed edit, including after detach.
                    editedDuringDelivery = true;
                    Asset.SelfApplyingNodeDrawer.ApplyValue(293);
                    if (removeBody)
                    {
                        host.Clear();
                    }
                });
                EventCallback<SerializedPropertyChangeEvent> observe = evt =>
                {
                    if (evt.target != host || !editedDuringDelivery)
                    {
                        return;
                    }
                    using (SerializedProperty value = evt.changedProperty.FindPropertyRelative("value"))
                    {
                        observations.Add((value.intValue, ReadInt(asset, path + ".value").Value));
                    }
                };
                window.rootVisualElement.RegisterCallback(observe);
                try
                {
                    yield return WaitFor(() => Asset.SelfApplyingNodeDrawer.ApplyValue != null && Asset.SelfApplyingNodeDrawer.Repainted
                        && Body(host, path) is IMGUIContainer body && IsVisible(body) && body.worldBound.height > 0f,
                        "The self-applying OnGUI drawer did not render: " + path);
                    Vector2 point = Body(host, path).worldBound.center;
                    window.SendEvent(new Event { type = EventType.MouseDown, mousePosition = point, button = 0, clickCount = 1 });
                    window.SendEvent(new Event { type = EventType.MouseUp, mousePosition = point, button = 0 });
                    yield return WaitFor(() => editedDuringDelivery && observations.Count > 0,
                        "The committed OnGUI edit did not reach both consumers: " + path);
                    Assert.That(ReadInt(asset, path + ".value"), Is.EqualTo(293));
                    Assert.That(observations.All(item => item.reported == item.saved && item.saved == 293), Is.True,
                        "Ancestor payloads must keep the writer's committed edit visible (remove body: "
                        + removeBody + "): " + string.Join(", ", observations));
                }
                finally
                {
                    window.rootVisualElement.UnregisterCallback(observe);
                    Asset.SelfApplyingNodeDrawer.ApplyValue = null;
                    Asset.SelfApplyingNodeDrawer.Repainted = false;
                }
            }
        }

        [UnityTest]
        public IEnumerator IMGUIDrawer_ResolvesExposedReferencesThroughTheHostContext_AfterReattachment ()
        {
            var context = ScriptableObject.CreateInstance<ExposedContext>();
            var first = new GameObject("First exposed reference");
            var second = new GameObject("Second exposed reference");
            PropertyField host = null;
            try
            {
                context.reference = first;
                asset.reference = new Asset.ContextNode
                {
                    reference = new ExposedReference<GameObject> { exposedName = new PropertyName("context-reference") }
                };
                Assert.That(((Asset.ContextNode)asset.reference).reference.Resolve(context), Is.SameAs(first),
                    "The supplied context must resolve the fixture before binding.");
                Asset.ContextNodeDrawer.LastResolved = null;
                boundObject = new SerializedObject(asset, context);
                SerializedProperty property = boundObject.FindProperty(nameof(Asset.reference));
                property.isExpanded = true;
                host = new PropertyField(property);
                window.rootVisualElement.Add(host);
                host.Bind(boundObject);
                yield return WaitFor(() => Selector(host, "reference") != null, "The context field did not bind.");
                Selector(host, "reference").value = true;
                yield return WaitFor(() => Asset.ContextNodeDrawer.LastResolved == first,
                    "The IMGUI subtype drawer lost the host's exposed-reference context.");

                host.RemoveFromHierarchy();
                context.reference = second;
                Asset.ContextNodeDrawer.LastResolved = null;
                yield return null;
                window.rootVisualElement.Add(host);
                yield return WaitFor(() => Asset.ContextNodeDrawer.LastResolved == second,
                    "The reattached IMGUI drawer did not resolve the current context value.");
            }
            finally
            {
                host?.RemoveFromHierarchy();
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
                UnityEngine.Object.DestroyImmediate(context);
            }
        }

        [UnityTest]
        public IEnumerator BooleanChild_NotifiesItsHost_AfterSelectorExpansionAndRebinding ()
        {
            asset.reference = new Asset.BooleanNode();
            PropertyField host = Bind(nameof(Asset.reference));
            Toggle InputToggle () => host.Query<Toggle>().ToList().FirstOrDefault(field => field.label == "Value");
            yield return WaitFor(() => InputToggle() != null && IsVisible(InputToggle()),
                "The selected subtype's boolean input was not displayed.");

            Selector(host, "reference").value = false;
            yield return WaitFor(() => !IsVisible(InputToggle()) && !boundObject.FindProperty("reference").isExpanded,
                "Closing the selector did not update its bound expansion state.");
            host.Unbind();
            host.Bind(boundObject);
            yield return WaitFor(() => Selector(host, "reference") is Foldout selector && !selector.value
                && !IsVisible(InputToggle()),
                "Rebinding did not preserve the closed selector.");

            Selector(host, "reference").value = true;
            yield return WaitFor(() => InputToggle() != null && IsVisible(InputToggle())
                && boundObject.FindProperty("reference").isExpanded,
                "Reopening the selector did not preserve its bound expansion state.");
            bool notified = false;
            host.RegisterValueChangeCallback(evt =>
            {
                if (evt.target != host)
                {
                    return;
                }
                using (SerializedProperty reported = evt.changedProperty.FindPropertyRelative("value"))
                using (var saved = new SerializedObject(asset))
                {
                    notified |= reported.boolValue && saved.FindProperty("reference.value").boolValue;
                }
            });
            InputToggle().value = true;
            yield return WaitFor(() => ((Asset.BooleanNode)asset.reference).value && notified,
                "Editing a child boolean did not commit its value and notify the host.");
        }

        [UnityTest]
        public IEnumerator SelectorExpansion_PreservesViewCallbacksWithoutReportingAssetChanges ()
        {
            asset.reference = new Asset.PlainNode { value = 15 };
            PropertyField host = Bind(nameof(Asset.reference));
            int valueNotifications = 0;
            bool editPublished = false;
            host.RegisterValueChangeCallback(evt =>
            {
                valueNotifications++;
                if (evt.target == host && evt.changedProperty.propertyPath == "reference"
                    && ReadInt(asset, "reference.value") == 17)
                {
                    editPublished = true;
                }
            });
            yield return WaitFor(() => VisibleInput(host, "Value", 15) != null,
                "The selector must bind before its expansion state is changed.");
            // Observe a completed data edit before starting the view-only operations.
            // A fixed frame delay can expire before Unity publishes binding notifications.
            Input(host, "Value").value = 17;
            yield return WaitFor(() => editPublished && VisibleInput(host, "Value", 17) != null,
                "The host must publish the initial edit before counting view-only notifications.");
            int publishedNotifications = valueNotifications;

            Foldout selector = Selector(host, "reference");
            int viewChanges = 0;
            selector.RegisterValueChangedCallback(evt =>
            {
                if (evt.target == selector && evt.previousValue != evt.newValue)
                {
                    viewChanges++;
                }
            });
            foreach (bool expanded in new[] { false, true })
            {
                selector.value = expanded;
                yield return WaitFor(() => boundObject.FindProperty("reference").isExpanded == expanded
                    && IsVisible(Input(host, "Value")) == expanded,
                    "The native expansion binding must preserve the requested view state.");
                for (int frame = 0; frame < 8; frame++)
                {
                    yield return null;
                }
            }
            Assert.That(viewChanges, Is.EqualTo(2), "Consumers must still receive the Foldout's two view changes.");
            Assert.That(valueNotifications, Is.EqualTo(publishedNotifications), "Opening and closing a Foldout must not report serialized data edits.");
            Assert.That(ReadInt(asset, "reference.value"), Is.EqualTo(17));
        }

        [UnityTest]
        public IEnumerator ASelectedSubtype_OpensAndClosesWithOneFoldout ()
        {
            asset.reference = new Asset.PlainNode { value = 17 };
            PropertyField host = Bind(nameof(Asset.reference));
            yield return WaitFor(() => VisibleInput(host, "Value", 17) != null, "The selected subtype was not displayed.");

            Foldout selector = Selector(host, "reference");
            Assert.That(selector, Is.Not.Null);
            selector.value = false;
            yield return WaitFor(() => !IsVisible(Input(host, "Value")), "Closing the selector did not hide its fields.");

            selector.value = true;
            yield return WaitFor(() => VisibleInput(host, "Value", 17) != null,
                "One expand action must expose the subtype's editable fields.");
            Input(host, "Value").value = 19;
            yield return WaitFor(() => ReadInt(asset, "reference.value") == 19, "The reopened field was not editable.");
        }

        [UnityTest]
        public IEnumerator ExternalTypeAndNullChanges_AndUndo_RefreshTheLiveFields ()
        {
            asset.reference = new Asset.PlainNode { value = 31 };
            PropertyField host = Bind(nameof(Asset.reference));
            yield return WaitFor(() => VisibleInput(host, "Value", 31) != null, "The original subtype was not displayed.");

            ReplaceReference(asset, "reference", new Asset.AlternateNode { other = 43 });
            yield return WaitFor(() => VisibleInput(host, "Other", 43) != null && Input(host, "Value") == null,
                "Replacing the type did not replace its editable fields.");

            Undo.PerformUndo();
            yield return WaitFor(() => VisibleInput(host, "Value", 31) != null && Input(host, "Other") == null,
                "Undo did not restore the original subtype's fields and values.");

            ReplaceReference(asset, "reference", null);
            yield return WaitFor(() => Input(host, "Value") == null && Input(host, "Other") == null,
                "Clearing a reference left stale editable fields behind.");
            Assert.That(asset.reference, Is.Null);

            Undo.PerformUndo();
            yield return WaitFor(() => VisibleInput(host, "Value", 31) != null, "Undo of clear did not restore editable fields.");
            Input(host, "Value").value = 47;
            yield return WaitFor(() => ReadInt(asset, "reference.value") == 47, "Restored fields were bound to a stale instance.");
        }

        [UnityTest]
        public IEnumerator UIToolkitOnlySubtype_RelativeInputsStayBoundAfterNewInstanceAndTypeChanges ()
        {
            var original = new Asset.RelativeNode { value = 53 };
            string originalLabel = original.ToString();
            asset.described = original;
            PropertyField host = Bind(nameof(Asset.described));
            int? notifiedValue = null;
            host.RegisterValueChangeCallback(evt => notifiedValue = ReadInt(asset, "described.value"));
            yield return WaitFor(() => VisibleInput(host, "Relative value", 53) != null && Selector(host, "described")?.text == originalLabel,
                "The subtype drawer's relative binding path was not resolved.");

            using (var external = new SerializedObject(asset))
            {
                SerializedProperty property = external.FindProperty("described");
                SerializedPropertyContextMenu.Execute(SerializedPropertyContextMenu.Open(property), "New Instance");
            }
            Assert.That(asset.described, Is.Not.SameAs(original));
            Assert.That(((Asset.RelativeNode)asset.described).value, Is.EqualTo(53));
            string replacementLabel = asset.described.ToString();
            Assert.That(replacementLabel, Is.Not.EqualTo(originalLabel), "The fixture labels must distinguish the original and replacement instances.");
            yield return WaitFor(() => Selector(host, "described")?.text == replacementLabel && VisibleInput(host, "Relative value", 53) != null,
                "The same-type replacement was not reflected in the visible label and input.");

            Input(host, "Relative value").value = 59;
            yield return WaitFor(() => ReadInt(asset, "described.value") == 59 && notifiedValue == 59,
                "A same-type New Instance disconnected the input or its host notification.");
            Assert.That(original.value, Is.EqualTo(53), "Editing the new instance must not change the replaced instance.");

            ReplaceReference(asset, "described", new Asset.AlternateNode { other = 61 });
            yield return WaitFor(() => VisibleInput(host, "Other", 61) != null, "The intermediate subtype was not displayed.");
            ReplaceReference(asset, "described", new Asset.RelativeNode { value = 67 });
            yield return WaitFor(() => VisibleInput(host, "Relative value", 67) != null,
                "Recreating a subtype drawer lost its relative binding scope.");

            Input(host, "Relative value").value = 71;
            yield return WaitFor(() => ReadInt(asset, "described.value") == 71, "The recreated relative input did not edit the replacement.");
        }

        [UnityTest]
        public IEnumerator UnannotatedChild_DrawersEditReplaceUndoAndClearWithoutChangingTheSibling ()
        {
            asset.reference = new Asset.UnannotatedChildNode();
            PropertyField host = Bind(nameof(Asset.reference));
            const string childPath = "reference.child";
            yield return WaitFor(() => VisibleInput(host, "Native Value", 811) != null,
                "The parent must display its native sibling while the child reference is null.");

            ReplaceReference(asset, childPath, new Asset.RelativeNode { value = 823 });
            yield return WaitFor(() => VisibleInput(Element(host, childPath), "Relative value", 823) != null,
                "An unannotated child's UITK-only subtype must bind its relative input.");
            Input(Element(host, childPath), "Relative value").value = 827;
            yield return WaitFor(() => ReadInt(asset, childPath + ".value") == 827,
                "The unannotated child's relative input did not save its value.");

            ReplaceReference(asset, childPath, new Asset.RelativeNode { value = 839 });
            yield return WaitFor(() => VisibleInput(Element(host, childPath), "Relative value", 839) != null,
                "A same-type child replacement must show its new instance's value.");
            Undo.IncrementCurrentGroup();
            Input(Element(host, childPath), "Relative value").value = 853;
            yield return WaitFor(() => ReadInt(asset, childPath + ".value") == 853,
                "The replacement input did not edit the current child.");
            Undo.PerformUndo();
            yield return WaitFor(() => ReadInt(asset, childPath + ".value") == 839
                && VisibleInput(Element(host, childPath), "Relative value", 839) != null,
                "Undo must restore the current child's displayed and saved value.");
            Undo.PerformRedo();
            yield return WaitFor(() => ReadInt(asset, childPath + ".value") == 853
                && VisibleInput(Element(host, childPath), "Relative value", 853) != null,
                "Redo must restore the edited child value.");

            Asset.InputNodeDrawer.ScreenRect = Rect.zero;
            ReplaceReference(asset, childPath, new Asset.InputNode { value = 857 });
            yield return WaitFor(() => Asset.InputNodeDrawer.ScreenRect.width > 0f
                && Body(Element(host, childPath), childPath) is IMGUIContainer gui && IsVisible(gui),
                "Switching the child to an OnGUI drawer must display its existing button.");
            Vector2 point = Body(Element(host, childPath), childPath).worldBound.center;
            window.SendEvent(new Event { type = EventType.MouseDown, mousePosition = point, button = 0, clickCount = 1 });
            window.SendEvent(new Event { type = EventType.MouseUp, mousePosition = point, button = 0 });
            yield return WaitFor(() => ReadInt(asset, childPath + ".value") == 281,
                "The unannotated child's OnGUI button must save to its current serialized path.");

            ReplaceReference(asset, childPath, new Asset.RelativeNode { value = 863 });
            yield return WaitFor(() => VisibleInput(Element(host, childPath), "Relative value", 863) != null,
                "Switching back from an OnGUI drawer must restore the UITK-only child's input.");
            Input(Element(host, childPath), "Relative value").value = 877;
            yield return WaitFor(() => ReadInt(asset, childPath + ".value") == 877,
                "The restored UITK-only input must edit the current child.");
            ReplaceReference(asset, childPath, null);
            yield return WaitFor(() => Element(host, childPath) is PropertyField child
                && !IsVisible(Input(child, "Relative value")),
                "Clearing the unannotated reference must remove its previous editable body.");
            using (var saved = new SerializedObject(asset))
            {
                Assert.That(saved.FindProperty(childPath).managedReferenceValue, Is.Null);
            }
            Assert.That(ReadInt(asset, "reference.nativeValue"), Is.EqualTo(811));
        }

        [UnityTest]
        public IEnumerator UnannotatedChild_MixedTargetsKeepTypingFocusAndDrawerTracking ()
        {
            var first = new Asset.TrackedNode { value = 7, marker = "First" };
            var second = new Asset.TrackedNode { value = 3, marker = "Second" };
            asset.reference = new Asset.UnannotatedChildNode { child = first };
            secondAsset.reference = new Asset.UnannotatedChildNode { child = second };
            Assert.That(SetReferenceId(asset, asset.reference, 2000), Is.True);
            Assert.That(SetReferenceId(secondAsset, secondAsset.reference, 2000), Is.True);
            Assert.That(SetReferenceId(asset, first, 1000), Is.True);
            Assert.That(SetReferenceId(secondAsset, second, 1000), Is.True);
            PropertyField host = Bind(nameof(Asset.reference), asset, secondAsset);
            const string childPath = "reference.child";
            yield return WaitFor(() => Input(Element(host, childPath), "Tracked value") is IntegerField input
                && input.showMixedValue && IsVisible(input),
                "The unannotated common subtype must expose its mixed UITK-only input.");
            window.Focus();
            Input(Element(host, childPath), "Tracked value").Focus();
            yield return WaitFor(() => HasFocus(Input(Element(host, childPath), "Tracked value")),
                "The child's mixed input must accept focus.");
            Input(Element(host, childPath), "Tracked value").SelectAll();
            TypeText("9");
            yield return WaitFor(() => ReadInt(asset, childPath + ".value") == 9 && ReadInt(secondAsset, childPath + ".value") == 9
                && host.Query<Label>().ToList().Any(label => label.text == "Tracked value: 9"),
                "The first key must edit both children and update the drawer's own tracker.");
            yield return null;
            Assert.That(HasFocus(Input(Element(host, childPath), "Tracked value")), Is.True,
                "The first input must retain focus after native binding updates.");
            TypeText("9");
            yield return WaitFor(() => ReadInt(asset, childPath + ".value") == 99 && ReadInt(secondAsset, childPath + ".value") == 99
                && host.Query<Label>().ToList().Any(label => label.text == "Tracked value: 99"),
                "The next key must continue editing both current children.");
            yield return null;
            Assert.That(HasFocus(Input(Element(host, childPath), "Tracked value")), Is.True,
                "Continued input must retain focus.");
            using (var firstObject = new SerializedObject(asset))
            using (var secondObject = new SerializedObject(secondAsset))
            {
                Assert.That(firstObject.FindProperty(childPath + ".marker").stringValue, Is.EqualTo("First"));
                Assert.That(secondObject.FindProperty(childPath + ".marker").stringValue, Is.EqualTo("Second"));
            }
            Assert.That(ReadInt(asset, "reference.nativeValue"), Is.EqualTo(811));
            Assert.That(ReadInt(secondAsset, "reference.nativeValue"), Is.EqualTo(811));
        }

#if UNITY_2022_2_OR_NEWER
        [UnityTest]
        public IEnumerator ChainedDrawers_KeepMetadataTrackingAndLateRowInput ()
        {
            Asset.DrawerChainDrawer.ReferenceChanges.Clear();
            Asset.ChainedTrackedNodeDrawer.ReferenceChanges.Clear();
            Asset.DrawerChainDrawer.ValueChanges.Clear();
            Asset.ChainedTrackedNodeDrawer.ValueChanges.Clear();
            var first = new Asset.ChainedTrackedNode
            {
                value = 0, marker = "Kept", next = new Asset.ChainedTrackedNode { value = 0, moveOnEdit = true }
            };
            var second = new Asset.ChainedTrackedNode
            {
                value = 0, marker = "Kept", next = new Asset.ChainedTrackedNode { value = 0, moveOnEdit = true }
            };
            asset.reference = new Asset.ChainedChildNode { child = first };
            secondAsset.reference = new Asset.ChainedChildNode { child = second };
            Assert.That(SetReferenceId(asset, asset.reference, 2000), Is.True);
            Assert.That(SetReferenceId(secondAsset, secondAsset.reference, 2000), Is.True);
            Assert.That(SetReferenceId(asset, first, 1000), Is.True);
            Assert.That(SetReferenceId(secondAsset, second, 1000), Is.True);
            Assert.That(SetReferenceId(asset, first.next, 3000), Is.True);
            Assert.That(SetReferenceId(secondAsset, second.next, 3000), Is.True);
            PropertyField host = Bind(nameof(Asset.reference), asset, secondAsset);
            const string childPath = "reference.child";
            yield return WaitFor(() => HasLabel(host, "Chain metadata: child argument/child")
                && HasLabel(host, "Type metadata: child/Chained input"),
                "Native attribute/type nesting must preserve the argument, fieldInfo and preferredLabel.");
            PropertyField child = Element(host, childPath);
            yield return WaitFor(() => IsVisible(Input(child, "Chained value"))
                && !Input(child, "Chained value").showMixedValue,
                "The initial common input must be bound before making it mixed.");
            PrepareMixedValues(childPath + ".value", 7, 3);
            // A managed-reference tracker need not report edits to its scalar descendants
            // (native 2023.2 does not). Require the edited value's own root-owned trackers.
            yield return WaitFor(() => Asset.DrawerChainDrawer.ValueChanges.Contains(childPath + ".value=7")
                && Asset.ChainedTrackedNodeDrawer.ValueChanges.Contains(childPath + ".value=7")
                && ReferenceIsMixed(childPath),
                "Both root-owned trackers must be active before the first edit.", () => DescribeTrackers(childPath));
            yield return EditMixedNines(child, childPath + ".value", "Chained value",
                value => HasLabel(child, "Chain tracked: " + value) && HasLabel(child, "Type tracked: " + value)
                    && !ReferenceIsMixed(childPath));

            const string latePath = childPath + ".next";
            Button show = child.Query<Button>().ToList().First(button => button.text == "Show child");
            Submit(show);
            yield return WaitFor(() => HasLabel(child, "Type metadata: next/Late child")
                && IsVisible(Input(Element(child, latePath), "Chained value")),
                "Adding a native PropertyField after binding must retain the type drawer's metadata.");
            PropertyField late = Element(child, latePath);
            yield return WaitFor(() => !Input(late, "Chained value").showMixedValue,
                "The late row must initially bind the common value.");
            PrepareMixedValues(latePath + ".value", 11, 13);
            yield return WaitFor(() => Asset.ChainedTrackedNodeDrawer.ValueChanges.Contains(latePath + ".value=11")
                && ReferenceIsMixed(latePath),
                "The late row's user trackers must be active before its first edit.", () => DescribeTrackers(latePath));
            yield return EditMixedNines(late, latePath + ".value", "Chained value",
                value => HasLabel(late, "Type tracked: " + value + "; row moved")
                    && !ReferenceIsMixed(latePath));

            ReplaceBothWithNewIds(latePath, new Asset.RelativeNode { value = 19 }, new Asset.RelativeNode { value = 23 });
            yield return WaitFor(() => IsVisible(Input(Element(host, latePath), "Relative value")),
                "A late row must display a different subtype after the native registration is reused.",
                () => DescribeInputs(host) + "; reference callbacks=" + string.Join(",", Asset.ChainedTrackedNodeDrawer.ReferenceChanges));
            yield return EditMixedNines(Element(host, latePath), latePath + ".value", "Relative value");
            using (var a = new SerializedObject(asset))
            using (var b = new SerializedObject(secondAsset))
            {
                Assert.That(a.FindProperty(childPath + ".marker").stringValue, Is.EqualTo("Kept"));
                Assert.That(b.FindProperty(childPath + ".marker").stringValue, Is.EqualTo("Kept"));
            }
            Assert.That(ReadInt(asset, "reference.nativeValue"), Is.EqualTo(811));
            Assert.That(ReadInt(secondAsset, "reference.nativeValue"), Is.EqualTo(811));

            ReplaceBothWithNewIds(childPath,
                new Asset.ChainedTrackedNode { value = 0, marker = "Replacement first" },
                new Asset.ChainedTrackedNode { value = 0, marker = "Replacement second" });
            yield return WaitFor(() => VisibleInput(Element(host, childPath), "Chained value", 0) is IntegerField input
                && !input.showMixedValue,
                "The replacement reference must first display the common new value.");
            PrepareMixedValues(childPath + ".value", 29, 31);
            yield return WaitFor(() => Asset.DrawerChainDrawer.ValueChanges.Contains(childPath + ".value=29")
                && Asset.ChainedTrackedNodeDrawer.ValueChanges.Contains(childPath + ".value=29"),
                "Replacing a same-type reference must display new inputs and activate their trackers.");
            child = Element(host, childPath);
            yield return EditMixedNines(child, childPath + ".value", "Chained value",
                value => HasLabel(child, "Chain tracked: " + value) && HasLabel(child, "Type tracked: " + value));
            using (var a = new SerializedObject(asset))
            using (var b = new SerializedObject(secondAsset))
            {
                Assert.That(a.FindProperty(childPath + ".marker").stringValue, Is.EqualTo("Replacement first"));
                Assert.That(b.FindProperty(childPath + ".marker").stringValue, Is.EqualTo("Replacement second"));
            }
        }

        [UnityTest]
        public IEnumerator ForeignNativeField_KeepsTypingTrackingAndSubtypeChanges ()
        {
            Asset.ChainedTrackedNodeDrawer.ReferenceChanges.Clear();
            Asset.ChainedTrackedNodeDrawer.ValueChanges.Clear();
            asset.reference = new Asset.ForeignFieldNode();
            secondAsset.reference = new Asset.ForeignFieldNode();
            asset.nativeReference = new Asset.ChainedTrackedNode { value = 0, marker = "Kept" };
            secondAsset.nativeReference = new Asset.ChainedTrackedNode { value = 0, marker = "Kept" };
            Assert.That(SetReferenceId(asset, asset.reference, 2000), Is.True);
            Assert.That(SetReferenceId(secondAsset, secondAsset.reference, 2000), Is.True);
            Assert.That(SetReferenceId(asset, asset.nativeReference, 1000), Is.True);
            Assert.That(SetReferenceId(secondAsset, secondAsset.nativeReference, 1000), Is.True);
            PropertyField host = Bind(nameof(Asset.reference), asset, secondAsset);
            yield return WaitFor(() => HasLabel(host, "Type metadata: nativeReference/Foreign reference"),
                "A drawer may display a native field outside the selected reference's property path.");
            yield return WaitFor(() => IsVisible(Input(host, "Chained value"))
                && !Input(host, "Chained value").showMixedValue,
                "The foreign field must initially bind its common value.");
            PrepareMixedValues("nativeReference.value", 7, 3);
            yield return WaitFor(() => Asset.ChainedTrackedNodeDrawer.ValueChanges.Contains("nativeReference.value=7")
                && ReferenceIsMixed("nativeReference"),
                "The foreign field's user trackers must be active before typing.", () => DescribeTrackers("nativeReference"));
            yield return EditMixedNines(host, "nativeReference.value", "Chained value",
                value => HasLabel(host, "Type tracked: " + value)
                    && !ReferenceIsMixed("nativeReference"));
            using (var a = new SerializedObject(asset))
            using (var b = new SerializedObject(secondAsset))
            {
                Assert.That(a.FindProperty("nativeReference.marker").stringValue, Is.EqualTo("Kept"));
                Assert.That(b.FindProperty("nativeReference.marker").stringValue, Is.EqualTo("Kept"));
            }
            ReplaceBothWithNewIds("nativeReference", new Asset.RelativeNode { value = 19 }, new Asset.RelativeNode { value = 23 });
            yield return WaitFor(() => IsVisible(Input(host, "Relative value")),
                "A foreign property's subtype change must still invoke its native rebuild.",
                () => DescribeInputs(host) + "; reference callbacks=" + string.Join(",", Asset.ChainedTrackedNodeDrawer.ReferenceChanges));
            yield return EditMixedNines(host, "nativeReference.value", "Relative value");
            using (var a = new SerializedObject(asset))
            using (var b = new SerializedObject(secondAsset))
            {
                Assert.That(a.FindProperty("reference").managedReferenceValue, Is.TypeOf<Asset.ForeignFieldNode>());
                Assert.That(b.FindProperty("reference").managedReferenceValue, Is.TypeOf<Asset.ForeignFieldNode>());
            }
        }

        [UnityTest]
        public IEnumerator SharedNativeAncestor_KeepsRemainingInputAndPendingReplacement ()
        {
            Asset.SharedSelectorsNodeDrawer.RemovedLiveSelectorOnTypeChange = false;
            asset.reference = new Asset.PlainNode { value = 811 };
            secondAsset.reference = new Asset.PlainNode { value = 813 };
            var first = new Asset.SharedSelectorsNode
            {
                left = new Asset.PlainNode { value = 41 }, right = new Asset.PlainNode { value = 7 }
            };
            var second = new Asset.SharedSelectorsNode
            {
                left = new Asset.PlainNode { value = 43 }, right = new Asset.PlainNode { value = 3 }
            };
            asset.nativeReference = first;
            secondAsset.nativeReference = second;
            Assert.That(SetReferenceId(asset, first, 2000), Is.True);
            Assert.That(SetReferenceId(secondAsset, second, 2000), Is.True);
            Assert.That(SetReferenceId(asset, first.left, 3000), Is.True);
            Assert.That(SetReferenceId(secondAsset, second.left, 3000), Is.True);
            Assert.That(SetReferenceId(asset, first.right, 1000), Is.True);
            Assert.That(SetReferenceId(secondAsset, second.right, 1000), Is.True);
            PropertyField host = Bind(nameof(Asset.nativeReference), asset, secondAsset);
            yield return WaitFor(() => Selector(host, "nativeReference.left") != null
                && Selector(host, "nativeReference.right") != null,
                "Both library selectors must be displayed inside one native custom drawer.");
            foreach (Foldout foldout in host.Query<Foldout>().ToList())
            {
                foldout.value = true;
            }
            yield return WaitFor(() => IsVisible(Input(Element(host, "nativeReference.right"), "Value")),
                "The right selector must expose its editable value.");
            Submit(host.Query<Button>().ToList().First(button => button.text == "Hide left"));
            yield return WaitFor(() => !IsVisible(Input(Element(host, "nativeReference.left"), "Value")),
                "The left selector must be removed without changing its serialized value.");
            yield return EditMixedNines(Element(host, "nativeReference.right"), "nativeReference.right.value", "Value");
            Assert.That(ReadInt(asset, "nativeReference.left.value"), Is.EqualTo(41));
            Assert.That(ReadInt(secondAsset, "nativeReference.left.value"), Is.EqualTo(43));

            // The user tracker removes the final selector during notification delivery.
            // The still-live native parent must receive its own pending subtype update.
            ReplaceReference(asset, "nativeReference", new Asset.AlternateNode { other = 19 });
            ReplaceReference(secondAsset, "nativeReference", new Asset.AlternateNode { other = 23 });
            yield return WaitFor(() => IsVisible(Input(host, "Other")) && Input(host, "Other").showMixedValue,
                "Removing the last selector in a user callback must not swallow the native parent's pending rebuild.");
            Assert.That(Asset.SharedSelectorsNodeDrawer.RemovedLiveSelectorOnTypeChange, Is.True,
                "This case must remove the last selector while its native parent is still live.");
            Input(host, "Other").value = 79;
            yield return WaitFor(() => ReadInt(asset, "nativeReference.other") == 79
                && ReadInt(secondAsset, "nativeReference.other") == 79,
                "The replacement native input must edit both current targets.");
            Assert.That(ReadInt(asset, "reference.value"), Is.EqualTo(811));
            Assert.That(ReadInt(secondAsset, "reference.value"), Is.EqualTo(813));
        }

        private IEnumerator EditMixedNines (VisualElement root, string valuePath, string label, Func<int, bool> tracked = null)
        {
            string Observe () => "Saved=" + ReadInt(asset, valuePath) + "/" + ReadInt(secondAsset, valuePath)
                + "; input=" + Input(root, label)?.value + "; mixed=" + Input(root, label)?.showMixedValue
                + "; focus=" + HasFocus(Input(root, label)) + "; labels="
                + string.Join(", ", root.Query<Label>().ToList().Select(value => value.text)
                    .Where(text => text != null && (text.Contains("tracked:") || text.Contains("metadata:"))));
            yield return WaitFor(() => Input(root, label) is IntegerField input && input.showMixedValue && IsVisible(input),
                "The common input must display mixed values before typing: " + valuePath);
            window.Focus();
            Input(root, label).Focus();
            yield return WaitFor(() => HasFocus(Input(root, label)), "The mixed input must accept focus: " + valuePath);
            Input(root, label).SelectAll();
            TypeText("9");
            yield return WaitFor(() => ReadInt(asset, valuePath) == 9 && ReadInt(secondAsset, valuePath) == 9
                && (tracked == null || tracked(9)), "The first key must save to both targets and notify the drawer: " + valuePath, Observe);
            yield return null;
            Assert.That(HasFocus(Input(root, label)), Is.True, "The first key must retain focus: " + valuePath);
            TypeText("9");
            yield return WaitFor(() => ReadInt(asset, valuePath) == 99 && ReadInt(secondAsset, valuePath) == 99
                && (tracked == null || tracked(99)), "The next key must continue editing and notifying: " + valuePath, Observe);
            yield return null;
            Assert.That(HasFocus(Input(root, label)), Is.True, "Continued typing must retain focus: " + valuePath);
        }

        private static bool HasLabel (VisualElement root, string text)
            => root.Query<Label>().ToList().Any(label => label.text == text && IsVisible(label));

        private bool ReferenceIsMixed (string path)
        {
            using (var observed = new SerializedObject(new UnityEngine.Object[] { asset, secondAsset }))
            using (SerializedProperty property = observed.FindProperty(path))
            {
                return property.hasMultipleDifferentValues;
            }
        }

        private string DescribeTrackers (string path)
            => "Saved=" + ReadInt(asset, path + ".value") + "/" + ReadInt(secondAsset, path + ".value")
                + "; bound MR mixed=" + boundObject.FindProperty(path).hasMultipleDifferentValues
                + "; independent MR mixed=" + ReferenceIsMixed(path)
                + "; bound value mixed=" + boundObject.FindProperty(path + ".value").hasMultipleDifferentValues
                + "; attribute values=" + string.Join(",", Asset.DrawerChainDrawer.ValueChanges)
                + "; type values=" + string.Join(",", Asset.ChainedTrackedNodeDrawer.ValueChanges)
                + "; attribute references=" + string.Join(",", Asset.DrawerChainDrawer.ReferenceChanges)
                + "; type references=" + string.Join(",", Asset.ChainedTrackedNodeDrawer.ReferenceChanges);

        private void PrepareMixedValues (string path, int first, int second)
        {
            window.Focus();
            WriteInt(asset, path, first);
            WriteInt(secondAsset, path, second);
            // This test owns the multi-target SerializedObject. Refresh its different-value
            // cache after writing through a separate, single-target SerializedObject.
            boundObject.SetIsDifferentCacheDirty();
            boundObject.Update();
        }

        private void ReplaceBothWithNewIds (string path, Asset.INode first, Asset.INode second)
        {
            long firstId;
            long secondId;
            using (var a = new SerializedObject(asset))
            using (var b = new SerializedObject(secondAsset))
            {
                firstId = a.FindProperty(path).managedReferenceId;
                secondId = b.FindProperty(path).managedReferenceId;
            }
            ReplaceReference(asset, path, first);
            ReplaceReference(secondAsset, path, second);
            using (var a = new SerializedObject(asset))
            using (var b = new SerializedObject(secondAsset))
            {
                long current = a.FindProperty(path).managedReferenceId;
                Assert.That(current, Is.Not.EqualTo(firstId));
                Assert.That(b.FindProperty(path).managedReferenceId, Is.Not.EqualTo(secondId));
            }
        }
#endif

        [UnityTest]
        public IEnumerator UnannotatedChildren_OtherTargetReferenceIdReuseKeepsDeepInputsEditable ()
        {
            var firstValue = new Asset.RelativeNode { value = 7 };
            var secondValue = new Asset.RelativeNode { value = 3 };
            var firstChild = new Asset.UnannotatedChildNode { child = firstValue };
            var secondChild = new Asset.UnannotatedChildNode { child = secondValue };
            var firstRoot = new Asset.UnannotatedChildNode { child = firstChild };
            var secondRoot = new Asset.UnannotatedChildNode { child = secondChild };
            asset.reference = firstRoot;
            secondAsset.reference = secondRoot;
            Assert.That(SetReferenceId(asset, firstRoot, 2000), Is.True);
            Assert.That(SetReferenceId(secondAsset, secondRoot, 2000), Is.True);
            Assert.That(SetReferenceId(asset, firstChild, 3000), Is.True);
            Assert.That(SetReferenceId(secondAsset, secondChild, 3000), Is.True);
            Assert.That(SetReferenceId(asset, firstValue, 4000), Is.True);
            Assert.That(SetReferenceId(secondAsset, secondValue, 4000), Is.True);
            PropertyField host = Bind(nameof(Asset.reference), asset, secondAsset);
            const string childPath = "reference.child.child";

            bool Ready (int? value = null)
            {
                // Expansion prepares the deep input; its binding is the behavior under test.
                foreach (Foldout foldout in host.Query<Foldout>().ToList())
                {
                    foldout.value = true;
                }
                IntegerField input = Input(Element(host, childPath), "Relative value");
                return IsVisible(input) && (!value.HasValue || input.value == value.Value);
            }

            yield return WaitFor(() => Ready() && Input(Element(host, childPath), "Relative value").showMixedValue,
                "Both targets must expose the common deep UITK-only input before reference IDs change.");

            // IDs belong to serialized instances. Prune the old graph before reusing its ID.
            secondAsset.reference = null;
            EditorJsonUtility.ToJson(secondAsset);
            var replacementChild = new Asset.UnannotatedChildNode { child = new Asset.RelativeNode { value = 17 } };
            var replacementRoot = new Asset.UnannotatedChildNode { child = replacementChild };
            secondAsset.reference = replacementRoot;
            Assert.That(SetReferenceId(secondAsset, replacementRoot, 5000), Is.True);
            Assert.That(SetReferenceId(secondAsset, replacementChild, 2000), Is.True);
            firstValue.value = 17;
            EditorUtility.SetDirty(asset);
            EditorUtility.SetDirty(secondAsset);
            using (var changed = new SerializedObject(secondAsset))
            {
                Assert.That(changed.FindProperty("reference").managedReferenceId, Is.EqualTo(5000));
                Assert.That(changed.FindProperty("reference.child").managedReferenceId, Is.EqualTo(2000));
            }

            yield return WaitFor(() => Ready(17) && !Input(Element(host, childPath), "Relative value").showMixedValue,
                "Reusing another target's previous root ID for its child must not hide the deep input.", () => DescribeInputs(host));
            Input(Element(host, childPath), "Relative value").value = 29;
            yield return WaitFor(() => ReadInt(asset, childPath + ".value") == 29
                && ReadInt(secondAsset, childPath + ".value") == 29 && Ready(29),
                "The deep input must keep editing both current targets after reference ID reuse.");
            Assert.That(ReadInt(asset, "reference.nativeValue"), Is.EqualTo(811));
            Assert.That(ReadInt(secondAsset, "reference.nativeValue"), Is.EqualTo(811));
            Assert.That(ReadInt(asset, "reference.child.nativeValue"), Is.EqualTo(811));
            Assert.That(ReadInt(secondAsset, "reference.child.nativeValue"), Is.EqualTo(811));
            Assert.That(secondValue.value, Is.EqualTo(3), "Editing the replacement must leave the old instance unchanged.");
        }

        [UnityTest]
        public IEnumerator DeepUnannotatedChildren_KeepDrawersAndEditsThroughCollectionChanges ()
        {
            asset.reference = new Asset.CompositeChildGraphNode();
            PropertyField host = Bind(nameof(Asset.reference));

            bool Ready (string path, int value)
            {
                // Expansion is a preparation step; this case tests the child bindings.
                foreach (Foldout foldout in host.Query<Foldout>().ToList())
                {
                    foldout.value = true;
                }
                return VisibleInput(Element(host, path), "Relative value", value) != null;
            }

            string[] paths = { "reference.details.array", "reference.details.list" };
            for (int i = 0; i < paths.Length; i++)
            {
                string path = paths[i];
                string firstChild = path + ".Array.data[0].child";
                string secondChild = path + ".Array.data[1].child";
                int offset = 100 * i;
                ReplaceReference(asset, firstChild, new Asset.RelativeNode { value = 911 + offset });
                yield return WaitFor(() => Ready(firstChild, 911 + offset),
                    "A deep null reference must expose its new UITK-only drawer.", () => DescribeInputs(host));
                Input(Element(host, firstChild), "Relative value").value = 917 + offset;
                yield return WaitFor(() => ReadInt(asset, firstChild + ".value") == 917 + offset,
                    "The deep relative input must edit its current child.");

                ReplaceReference(asset, firstChild, new Asset.ObservedNode { value = 919 + offset });
                yield return WaitFor(() => Asset.ObservedNodeDrawer.LastValue == 919 + offset,
                    "The deep OnGUI subtype must render its current value.");
                ReplaceReference(asset, firstChild, new Asset.RelativeNode { value = 929 + offset });
                yield return WaitFor(() => Ready(firstChild, 929 + offset),
                    "An OnGUI-to-UITK switch must recover with the same parent type and collection size.", () => DescribeInputs(host));
                Input(Element(host, firstChild), "Relative value").value = 943 + offset;
                yield return WaitFor(() => ReadInt(asset, firstChild + ".value") == 943 + offset,
                    "The recreated deep input must save to the current reference.");

                using (var external = new SerializedObject(asset))
                {
                    SerializedProperty collection = external.FindProperty(path);
                    collection.arraySize = 2;
                    collection.GetArrayElementAtIndex(1).FindPropertyRelative("child").managedReferenceValue = new Asset.RelativeNode { value = 953 + offset };
                    collection.GetArrayElementAtIndex(1).FindPropertyRelative("marker").intValue = 1101 + offset;
                    external.ApplyModifiedProperties();
                }
                yield return WaitFor(() => Ready(firstChild, 943 + offset) && Ready(secondChild, 953 + offset),
                    "A newly added row must bind independently from its existing sibling.", () => DescribeInputs(host));
                using (var external = new SerializedObject(asset))
                {
                    external.FindProperty(path).MoveArrayElement(0, 1);
                    external.ApplyModifiedProperties();
                }
                yield return WaitFor(() => Ready(firstChild, 953 + offset) && Ready(secondChild, 943 + offset),
                    "Reordered deep references must display their current row values.");
                Input(Element(host, secondChild), "Relative value").value = 967 + offset;
                yield return WaitFor(() => ReadInt(asset, secondChild + ".value") == 967 + offset,
                    "The reordered input must edit its current serialized path.");
                Assert.That(ReadInt(asset, firstChild + ".value"), Is.EqualTo(953 + offset));
                Assert.That(ReadInt(asset, path + ".Array.data[0].marker"), Is.EqualTo(1101 + offset));
                using (var external = new SerializedObject(asset))
                {
                    external.FindProperty(path).DeleteArrayElementAtIndex(0);
                    external.ApplyModifiedProperties();
                }
                yield return WaitFor(() => ReadArraySize(asset, path) == 1 && Ready(firstChild, 967 + offset),
                    "Removing a deep row must preserve the surviving reference's input.");
                Input(Element(host, firstChild), "Relative value").value = 979 + offset;
                yield return WaitFor(() => ReadInt(asset, firstChild + ".value") == 979 + offset,
                    "The surviving deep input must remain editable.");
            }
            Assert.That(ReadInt(asset, "reference.details.array.Array.data[0].child.value"), Is.EqualTo(979));
            Assert.That(ReadInt(asset, "reference.details.list.Array.data[0].child.value"), Is.EqualTo(1079));
            Assert.That(ReadInt(asset, "reference.details.nativeValue"), Is.EqualTo(907));
        }

        [UnityTest]
        public IEnumerator ReorderingAnArrayOfTheSameSubtype_KeepsEditsOnTheDisplayedElement ()
        {
            return ReorderAndEdit(nameof(Asset.array));
        }

        [UnityTest]
        public IEnumerator ReorderingAListOfTheSameSubtype_KeepsEditsOnTheDisplayedElement ()
        {
            return ReorderAndEdit(nameof(Asset.list));
        }

        [UnityTest]
        public IEnumerator RemovingListRows_ThenUndoingAndReplacingTypes_KeepsTheInspectorUsable ()
        {
            asset.list.Add(new Asset.PlainNode { value = 149 });
            asset.list.Add(new Asset.RelativeNode { value = 151 });
            PropertyField host = Bind(nameof(Asset.list));
            const string firstPath = "list.Array.data[0]";
            const string secondPath = "list.Array.data[1]";
            int relativeValue = 151;
            yield return WaitFor(() => VisibleInput(Element(host, secondPath), "Relative value", relativeValue) != null,
                "The second list row was not displayed.");

            // Keep the same window alive while ListView removes, restores and reuses its rows.
            for (int iteration = 0; iteration < 3; iteration++)
            {
                Undo.IncrementCurrentGroup();
                using (var external = new SerializedObject(asset))
                {
                    external.FindProperty("list").DeleteArrayElementAtIndex(0);
                    external.ApplyModifiedProperties();
                }
                yield return WaitFor(() => VisibleInput(Element(host, firstPath), "Relative value", relativeValue) != null
                    && !IsVisible(Input(Element(host, secondPath), "Relative value")),
                    "Removing a row did not move the remaining reference into the first row.");

                Undo.PerformUndo();
                yield return WaitFor(() => VisibleInput(Element(host, firstPath), "Value", 149) != null
                    && VisibleInput(Element(host, secondPath), "Relative value", relativeValue) != null,
                    "Undo did not restore both list rows and their inputs.");
                Undo.PerformRedo();
                yield return WaitFor(() => VisibleInput(Element(host, firstPath), "Relative value", relativeValue) != null,
                    "Redo of row removal left the wrong reference in the first row.");
                Undo.PerformUndo();
                yield return WaitFor(() => VisibleInput(Element(host, secondPath), "Relative value", relativeValue) != null,
                    "The second Undo did not restore the removed row.");

                ReplaceReference(asset, secondPath, new Asset.AlternateNode { other = 163 });
                yield return WaitFor(() => VisibleInput(Element(host, secondPath), "Other", 163) != null,
                    "A restored row did not display its replacement subtype.");
                Undo.PerformUndo();
                yield return WaitFor(() => VisibleInput(Element(host, secondPath), "Relative value", relativeValue) != null,
                    "Undo of the type replacement did not restore the subtype drawer.");

                relativeValue = 173 + iteration;
                Input(Element(host, secondPath), "Relative value").value = relativeValue;
                yield return WaitFor(() => ReadInt(asset, secondPath + ".value") == relativeValue,
                    "The restored subtype drawer stopped committing input after row recycling.");
                Assert.That(ReadInt(asset, firstPath + ".value"), Is.EqualTo(149),
                    "Editing a restored row must leave its sibling unchanged.");
            }
        }

        [UnityTest]
        public IEnumerator NestedReferences_EditTheNestedValueWithoutChangingTheSibling ()
        {
            asset.reference = new Asset.PlainNode { value = 73 };
            asset.parent.reference = new Asset.PlainNode { value = 79 };
            PropertyField host = Bind("parent.reference");
            yield return WaitFor(() => VisibleInput(host, "Value", 79) != null, "The nested reference was not displayed.");

            Input(host, "Value").value = 83;
            yield return WaitFor(() => ReadInt(asset, "parent.reference.value") == 83, "The nested input did not edit its reference.");
            Assert.That(((Asset.PlainNode)asset.reference).value, Is.EqualTo(73));
        }

        [UnityTest]
        public IEnumerator ReattachingAfterDetachedChanges_RestoresInputAndContinuesTracking ()
        {
            asset.reference = new Asset.PlainNode { value = 89 };
            PropertyField host = Bind(nameof(Asset.reference));
            yield return WaitFor(() => VisibleInput(host, "Value", 89) != null, "The initial field was not displayed.");

            host.RemoveFromHierarchy();
            ReplaceReference(asset, "reference", new Asset.RelativeNode { value = 97 });
            window.rootVisualElement.Add(host);
            yield return WaitFor(() => VisibleInput(host, "Relative value", 97) != null,
                "Reattaching did not observe a type change made while detached.");
            Input(host, "Relative value").value = 101;
            yield return WaitFor(() => ReadInt(asset, "reference.value") == 101, "The reattached subtype input was not bound.");

            host.RemoveFromHierarchy();
            ReplaceReference(asset, "reference", new Asset.PlainNode { value = 103 });
            window.rootVisualElement.Add(host);
            yield return WaitFor(() => VisibleInput(host, "Value", 103) != null, "A second attachment kept the previous subtype's UI.");

            ReplaceReference(asset, "reference", new Asset.PlainNode { value = 107 });
            yield return WaitFor(() => VisibleInput(host, "Value", 107) != null,
                "Object tracking stopped after detaching and reattaching.");
            Input(host, "Value").value = 109;
            yield return WaitFor(() => ReadInt(asset, "reference.value") == 109, "The current same-type instance was not editable.");
        }

        [UnityTest]
        public IEnumerator SameSubtype_MixedTargets_KeepEditingFocusAndDrawerTracking ()
        {
            asset.reference = new Asset.TrackedNode { value = 7, marker = "First" };
            secondAsset.reference = new Asset.TrackedNode { value = 3, marker = "Second" };
            Assert.That(SetReferenceId(asset, asset.reference, 1000), Is.True);
            Assert.That(SetReferenceId(secondAsset, secondAsset.reference, 1000), Is.True);
            PropertyField host = Bind(nameof(Asset.reference), asset, secondAsset);
            yield return WaitFor(() => Input(host, "Tracked value") != null && Input(host, "Tracked value").showMixedValue
                && IsVisible(Input(host, "Tracked value")), "The common subtype's mixed input was not displayed.");

            window.Focus();
            Input(host, "Tracked value").Focus();
            yield return WaitFor(() => HasFocus(Input(host, "Tracked value")), "The displayed input could not receive focus.");
            Input(host, "Tracked value").SelectAll();
            Undo.IncrementCurrentGroup();
            int editGroup = Undo.GetCurrentGroup();
            TypeText("9");
            yield return WaitFor(() => ReadInt(asset, "reference.value") == 9 && ReadInt(secondAsset, "reference.value") == 9
                && host.Query<Label>().ToList().Any(label => label.text == "Tracked value: 9"),
                "The first edit must update both targets and the UITK drawer's property tracker.");
            yield return null;
            Assert.That(HasFocus(Input(host, "Tracked value")), Is.True,
                "Binding updates interrupted the active input after the first edit.");

            TypeText("9");
            yield return WaitFor(() => ReadInt(asset, "reference.value") == 99 && ReadInt(secondAsset, "reference.value") == 99
                && host.Query<Label>().ToList().Any(label => label.text == "Tracked value: 99"),
                "The second edit must reach both targets and the retained drawer tracker.");
            yield return null;
            Assert.That(HasFocus(Input(host, "Tracked value")), Is.True,
                "Binding updates interrupted continued editing.");
            Undo.CollapseUndoOperations(editGroup);
            Undo.PerformUndo();
            yield return WaitFor(() => ReadInt(asset, "reference.value") == 7 && ReadInt(secondAsset, "reference.value") == 3,
                "Undo did not restore each target's original value.");
            Undo.PerformRedo();
            yield return WaitFor(() => ReadInt(asset, "reference.value") == 99 && ReadInt(secondAsset, "reference.value") == 99,
                "Redo did not restore the common edited value.");

            host.Unbind();
            host.Bind(boundObject);
            yield return WaitFor(() => VisibleInput(host, "Tracked value", 99) != null,
                "The common input did not return after the host was rebound.");
            Input(host, "Tracked value").Focus();
            yield return WaitFor(() => HasFocus(Input(host, "Tracked value")), "The rebound input could not receive focus.");
            Input(host, "Tracked value").SelectAll();
            TypeText("101");
            yield return WaitFor(() => ReadInt(asset, "reference.value") == 101 && ReadInt(secondAsset, "reference.value") == 101
                && host.Query<Label>().ToList().Any(label => label.text == "Tracked value: 101"),
                "Rebinding lost the UITK-only drawer's input or property tracker.");
            yield return null;
            Assert.That(HasFocus(Input(host, "Tracked value")), Is.True,
                "Rebinding reintroduced the interruption during editing.");
            using (var first = new SerializedObject(asset))
            using (var second = new SerializedObject(secondAsset))
            {
                Assert.That(first.FindProperty("reference.marker").stringValue, Is.EqualTo("First"));
                Assert.That(second.FindProperty("reference.marker").stringValue, Is.EqualTo("Second"));
            }
        }

        [UnityTest]
        public IEnumerator MixedTypes_BecomingOneSubtype_ExposeAnInputThatEditsAllTargets ()
        {
            asset.reference = new Asset.PlainNode { value = 113 };
            secondAsset.reference = new Asset.AlternateNode { other = 127 };
            PropertyField host = Bind(nameof(Asset.reference), asset, secondAsset);
            yield return WaitFor(() => Selector(host, "reference") != null, "The mixed selection was not displayed.");
            IntegerField firstTargetInput = Input(host, "Value");
            bool canEditFirstTarget = firstTargetInput != null && IsVisible(firstTargetInput) && firstTargetInput.enabledInHierarchy && !firstTargetInput.isReadOnly;
            Assert.That(canEditFirstTarget, Is.False, "A mixed-type selection must not offer an input that edits only the first target.");

            WriteInt(asset, "reference.value", 131);
            ReplaceReference(secondAsset, "reference", new Asset.PlainNode { value = 137 });
            yield return WaitFor(() => Input(host, "Value") != null && Input(host, "Value").showMixedValue && IsVisible(Input(host, "Value")),
                "The shared subtype did not expose its mixed-value input.");

            Input(host, "Value").value = 139;
            yield return WaitFor(() => ReadInt(asset, "reference.value") == 139 && ReadInt(secondAsset, "reference.value") == 139,
                "Editing the shared subtype must update both selected targets.");
        }

        private IEnumerator ReorderAndEdit (string collectionPath)
        {
            asset.array = new Asset.INode[] { new Asset.PlainNode { value = 149 }, new Asset.PlainNode { value = 151 } };
            asset.list.Add(new Asset.PlainNode { value = 149 });
            asset.list.Add(new Asset.PlainNode { value = 151 });
            PropertyField host = Bind(collectionPath);
            string firstPath = collectionPath + ".Array.data[0]";
            yield return WaitFor(() => VisibleInput(Element(host, firstPath), "Value", 149) != null,
                "The first collection element was not displayed.");

            using (var external = new SerializedObject(asset))
            {
                external.FindProperty(collectionPath).MoveArrayElement(0, 1);
                external.ApplyModifiedProperties();
            }
            yield return WaitFor(() => VisibleInput(Element(host, firstPath), "Value", 151) != null,
                "The first row did not show the element moved into it.");

            Input(Element(host, firstPath), "Value").value = 157;
            yield return WaitFor(() => ReadInt(asset, firstPath + ".value") == 157,
                "Editing a reordered row wrote to its old element.");
            Assert.That(ReadInt(asset, collectionPath + ".Array.data[1].value"), Is.EqualTo(149),
                "The element moved out of the first row must remain unchanged.");
        }

        private PropertyField Bind (string path, params Asset[] targets)
        {
            UnityEngine.Object[] objects = targets.Length == 0 ? new UnityEngine.Object[] { asset } : targets.Cast<UnityEngine.Object>().ToArray();
            boundObject = new SerializedObject(objects);
            SerializedProperty property = boundObject.FindProperty(path);
            property.isExpanded = true;
            if (property.isArray)
            {
                for (int i = 0; i < property.arraySize; i++)
                {
                    property.GetArrayElementAtIndex(i).isExpanded = true;
                }
            }
            var host = new PropertyField(property);
            window.rootVisualElement.Add(host);
            host.Bind(boundObject);
            return host;
        }

        private IEnumerator WaitFor (Func<bool> condition, string failure, Func<string> observation = null)
        {
            double deadline = EditorApplication.timeSinceStartup + 8d;
            while (!condition())
            {
                if (EditorApplication.timeSinceStartup >= deadline)
                {
                    Assert.Fail(observation == null ? failure : failure + "\n" + observation());
                }
                window.Repaint();
                yield return null;
            }
        }

        private static IntegerField Input (VisualElement root, string label)
        {
            return root?.Query<IntegerField>().ToList().FirstOrDefault(field => field.label == label);
        }

        private static IntegerField VisibleInput (VisualElement root, string label, int value)
        {
            IntegerField input = Input(root, label);
            return input != null && input.value == value && IsVisible(input) ? input : null;
        }

        private static bool HasFocus (VisualElement input)
        {
            var focused = input?.panel?.focusController.focusedElement as VisualElement;
            return focused != null && (focused == input || input.Contains(focused));
        }

        private void TypeText (string text)
        {
            foreach (char character in text)
            {
                window.SendEvent(Event.KeyboardEvent(character.ToString()));
                var released = Event.KeyboardEvent(character.ToString());
                released.type = EventType.KeyUp;
                window.SendEvent(released);
            }
        }

        private void Click (VisualElement element)
        {
            Assert.That(IsVisible(element), Is.True, "The native click target must be visible.");
            Vector2 point = element.worldBound.center;
            window.SendEvent(new Event { type = EventType.MouseMove, mousePosition = point });
            // Keep disabled controls in the UITK event route; the EditorWindow's
            // IMGUI bridge can discard them before their native Clickable receives input.
            using (EventBase down = ClickEvent(new Event
                { type = EventType.MouseDown, mousePosition = point, button = 0, clickCount = 1 }))
            {
                down.target = element;
                element.SendEvent(down);
            }
            using (EventBase up = ClickEvent(new Event
                { type = EventType.MouseUp, mousePosition = point, button = 0 }))
            {
                up.target = element;
                element.SendEvent(up);
            }
        }

        private static EventBase ClickEvent (Event input)
        {
#if UNITY_2023_2_OR_NEWER
            return input.type == EventType.MouseDown
                ? (EventBase)PointerDownEvent.GetPooled(input) : PointerUpEvent.GetPooled(input);
#else
            return input.type == EventType.MouseDown
                ? (EventBase)MouseDownEvent.GetPooled(input) : MouseUpEvent.GetPooled(input);
#endif
        }

        private static void Submit (Button button)
        {
            // Editor navigation is dispatched to the panel's focused element.
            button.Focus();
            using (NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled())
            {
                evt.target = button;
                button.SendEvent(evt);
            }
        }

        private static Button CollectionButton (VisualElement root, string path, string text)
        {
            ListView list = root.Query<ListView>().ToList().FirstOrDefault(value => value.bindingPath == path);
            // Newer Editors render these native buttons as icons with no text. Use Unity's
            // own names; the minimum Editor exposes the same identifiers internally.
            var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.FlattenHierarchy;
            string name = (string)typeof(ListView).GetField(text == "+" ? "footerAddButtonName" : "footerRemoveButtonName", flags).GetValue(null);
            return list?.Q<VisualElement>(className: ListView.footerUssClassName)?.Q<Button>(name);
        }

        private static string DescribeInputs (VisualElement root)
            => "Inputs: " + string.Join(", ", root.Query<IntegerField>().ToList().Select(input => input.label + "=" + input.value + " visible=" + IsVisible(input)))
                + "; foldouts: " + string.Join(", ", root.Query<Foldout>().ToList().Select(foldout => foldout.bindingPath + "=" + foldout.value));

#if UNITY_2022_3_OR_NEWER
        private static bool SetReferenceId (Asset target, object value, long id)
            => UnityEngine.Serialization.ManagedReferenceUtility.SetManagedReferenceIdForObject(target, value, id);
#else
        private static bool SetReferenceId (Asset target, object value, long id)
            => SerializationUtility.SetManagedReferenceIdForObject(target, value, id);
#endif

        private static VisualElement Header (VisualElement root, string path)
            => Selector(root, path)?.GetFirstAncestorOfType<PropertyField>().hierarchy.Children()
                .Select(field => field.Q<PopupField<string>>()).FirstOrDefault(value => value != null)
                ?.Q(className: PopupField<string>.inputUssClassName);

        private static Toggle HeaderToggle (VisualElement root, string path)
        {
            Foldout selector = Selector(root, path);
            return selector?.GetFirstAncestorOfType<PropertyField>().Query<Toggle>().ToList()
                .FirstOrDefault(toggle => toggle.parent == selector && toggle.ClassListContains(Foldout.toggleUssClassName));
        }

        private static IMGUIContainer Body (VisualElement root, string path)
        {
            Foldout selector = Selector(root, path);
            return (selector != null ? selector.contentContainer : root)?.Q<IMGUIContainer>();
        }

        private static Foldout Selector (VisualElement root, string path)
        {
            return root.Query<Foldout>().ToList().FirstOrDefault(foldout => foldout.bindingPath == path);
        }

        private static PropertyField Element (VisualElement root, string path)
        {
            return root.Query<PropertyField>().ToList().FirstOrDefault(field => field.bindingPath == path);
        }

        private static bool IsVisible (VisualElement element)
        {
            if (element == null || element.panel == null || !(element.worldBound.width > 0f && element.worldBound.height > 0f))
            {
                return false;
            }
            for (VisualElement current = element; current != null; current = current.parent)
            {
                if (current.resolvedStyle.display == DisplayStyle.None || current.resolvedStyle.visibility == Visibility.Hidden)
                {
                    return false;
                }
            }
            return true;
        }

        private static int? ReadInt (Asset target, string path)
        {
            using (var serialized = new SerializedObject(target))
            {
                return serialized.FindProperty(path)?.intValue;
            }
        }

        private static int CollectionIndex (ListView control, string path)
        {
            return Enumerable.Range(0, control.itemsSource.Count).First(index =>
                control.itemsSource[index] is SerializedProperty property && property.propertyPath == path);
        }

        private static int ReadArraySize (Asset target, string path)
        {
            using (var serialized = new SerializedObject(target))
            {
                return serialized.FindProperty(path).arraySize;
            }
        }

        private static void WriteInt (Asset target, string path, int value)
        {
            using (var serialized = new SerializedObject(target))
            {
                serialized.FindProperty(path).intValue = value;
                serialized.ApplyModifiedProperties();
            }
        }

        private static void ReplaceReference (Asset target, string path, Asset.INode instance)
        {
            Undo.IncrementCurrentGroup();
            using (var serialized = new SerializedObject(target))
            {
                serialized.FindProperty(path).managedReferenceValue = instance;
                serialized.ApplyModifiedProperties();
            }
        }

        public sealed class TestWindow : EditorWindow { }

        public sealed class ExposedContext : ScriptableObject, IExposedPropertyTable
        {
            public UnityEngine.Object reference;

            public void SetReferenceValue (PropertyName id, UnityEngine.Object value) => reference = value;
            public UnityEngine.Object GetReferenceValue (PropertyName id, out bool idValid)
            {
                idValid = id == new PropertyName("context-reference");
                return idValid ? reference : null;
            }
            public void ClearReferenceValue (PropertyName id) => reference = null;
        }
    }
}
