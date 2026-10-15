using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MackySoft.SerializeReferenceExtensions.Editor;
using NUnit.Framework;
using UnityEditor.IMGUI.Controls;

namespace MackySoft.SerializeReferenceExtensions.Tests
{
    [TestFixture]
    public sealed class AdvancedTypePopupTests
    {

        private sealed class Popup : AdvancedTypePopup
        {

            public Popup (IEnumerable<Type> types) : base(types, 13, new AdvancedDropdownState())
            {
            }

            public AdvancedDropdownItem Build () => BuildRoot();

            public void Select (AdvancedDropdownItem item) => ItemSelected(item);

        }

        [AddTypeMenu("Commands/Alpha", 10)]
        private sealed class Alpha { }

        [AddTypeMenu("Commands/Zeta", -10)]
        private sealed class Zeta { }

        [AddTypeMenu("Renamed")]
        private sealed class RenamedType { }

        [AddTypeMenu("m_Apple")]
        private sealed class PrefixedApple { }

        [AddTypeMenu("Banana")]
        private sealed class PlainBanana { }

        [AddTypeMenu("Same")]
        private sealed class FirstTie { }

        [AddTypeMenu("Same")]
        private sealed class SecondTie { }

        [AddTypeMenu("ä")]
        private sealed class Accent { }

        [AddTypeMenu("z")]
        private sealed class Latin { }

        [AddTypeMenu(null)]
        private sealed class NullMenu { }

        [AddTypeMenu("")]
        private sealed class EmptyMenu { }

        [AddTypeMenu("  ")]
        private sealed class WhitespaceMenu { }

        [AddTypeMenu("///")]
        private sealed class SeparatorsMenu { }

        [Test]
        public void AddTo_AppendsNullAndOrderedTypesToExistingRoot ()
        {
            var root = new AdvancedDropdownItem("Existing root");
            root.AddChild(new AdvancedDropdownItem("Existing item"));

            AdvancedTypePopup.AddTo(root, new[] { typeof(Zeta), typeof(RenamedType), typeof(Alpha) });

            Assert.That(Children(root).Select(item => item.name), Is.EqualTo(new[] { "Existing item", "<null>", "Commands", "Renamed" }));
            Assert.That(Children(Children(root)[2]).Select(item => item.name), Is.EqualTo(new[] { "Zeta", "Alpha" }));
            Assert.That(Leaves(root).Select(item => item.Type), Is.EqualTo(new[] { null, typeof(Zeta), typeof(Alpha), typeof(RenamedType) }));
        }

        [Test]
        public void RepeatedBuilds_PreserveMenuAndIsolateMutableDropdownItems ()
        {
            var popup = new Popup(new[] { typeof(Zeta), typeof(RenamedType), typeof(Alpha) });
            AdvancedDropdownItem first = popup.Build();
            var initialIds = Leaves(first).Select(item => item.id).ToArray();

            Children(first)[1].name = "Changed category";
            Leaves(first).First(item => item.Type == typeof(Alpha)).name = "Changed label";
            first.AddChild(new AdvancedDropdownItem("Extra item"));
            AdvancedDropdownItem second = popup.Build();

            Assert.That(Children(second).Select(item => item.name), Is.EqualTo(new[] { "<null>", "Commands", "Renamed" }));
            Assert.That(Children(Children(second)[1]).Select(item => item.name), Is.EqualTo(new[] { "Zeta", "Alpha" }));
            Assert.That(Leaves(second).Select(item => item.id), Is.EqualTo(initialIds));
        }

        [Test]
        public void SetTypes_ReplacesPreviouslyDisplayedCandidatesAndSnapshotsInput ()
        {
            var types = new List<Type> { typeof(Alpha) };
            var popup = new Popup(types);
            types.Add(typeof(Zeta));

            Assert.That(Leaves(popup.Build()).Select(item => item.Type), Is.EqualTo(new[] { null, typeof(Alpha) }));

            popup.SetTypes(new[] { typeof(RenamedType), typeof(Zeta) });
            Assert.That(Leaves(popup.Build()).Select(item => item.Type), Is.EquivalentTo(new[] { null, typeof(RenamedType), typeof(Zeta) }));

            popup.SetTypes(Array.Empty<Type>());
            Assert.That(Leaves(popup.Build()).Select(item => item.Type), Is.EqualTo(new Type[] { null }));
        }

        [Test]
        public void SingleNamespace_IsFlattenedWithSingleSegmentMenuOverride ()
        {
            var popup = new Popup(new[] { typeof(SerializableStruct), typeof(RenamedType), typeof(PublicSerializableClass) });

            AdvancedDropdownItem root = popup.Build();

            Assert.That(Children(root).Select(item => item.name), Is.EqualTo(new[] { "<null>", "Public Serializable Class", "Renamed", "Serializable Struct" }));
            Assert.That(Children(root).All(item => !Children(item).Any()), Is.True);
        }

        [Test]
        public void DifferentNamespaces_RemainSeparateCategories ()
        {
            var popup = new Popup(new[] { typeof(string), typeof(PublicSerializableClass) });

            AdvancedDropdownItem root = popup.Build();

            Assert.That(Children(root).Select(item => item.name), Is.EqualTo(new[] { "<null>", "MackySoft.SerializeReferenceExtensions.Tests", "System" }));
            Assert.That(Leaves(root).Select(item => item.Type), Is.EqualTo(new[] { null, typeof(PublicSerializableClass), typeof(string) }));
        }

        [Test]
        public void EmptyMenuPaths_AreNotDisplayed ()
        {
            var popup = new Popup(new[] { typeof(NullMenu), typeof(EmptyMenu), typeof(WhitespaceMenu), typeof(SeparatorsMenu), typeof(RenamedType) });

            Assert.That(Children(popup.Build()).Select(item => item.name), Is.EqualTo(new[] { "<null>", "Renamed" }));
            Assert.That(Children(popup.Build()).Select(item => item.name), Is.EqualTo(new[] { "<null>", "Renamed" }));
        }

        [Test]
        public void SortOrder_UsesMenuNameBeforeNicifyingTheLabel ()
        {
            var popup = new Popup(new[] { typeof(PrefixedApple), typeof(PlainBanana) });

            AdvancedDropdownItem root = popup.Build();

            Assert.That(Children(root).Select(item => item.name), Is.EqualTo(new[] { "<null>", "Banana", "Apple" }));
            Assert.That(Leaves(root).Select(item => item.Type), Is.EqualTo(new[] { null, typeof(PlainBanana), typeof(PrefixedApple) }));
        }

        [Test]
        public void EqualSortKeys_PreserveInputOrderAfterReplacement ()
        {
            var popup = new Popup(new[] { typeof(SecondTie), typeof(FirstTie) });
            Assert.That(Leaves(popup.Build()).Select(item => item.Type), Is.EqualTo(new[] { null, typeof(SecondTie), typeof(FirstTie) }));

            popup.SetTypes(new[] { typeof(FirstTie), typeof(SecondTie) });
            Assert.That(Leaves(popup.Build()).Select(item => item.Type), Is.EqualTo(new[] { null, typeof(FirstTie), typeof(SecondTie) }));
        }

        [Test]
        public void CultureChange_UsesCurrentCultureOrdering ()
        {
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                var popup = new Popup(new[] { typeof(Latin), typeof(Accent) });
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
                Assert.That(Leaves(popup.Build()).Select(item => item.Type), Is.EqualTo(new[] { null, typeof(Accent), typeof(Latin) }));

                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("sv-SE");
                Assert.That(Leaves(popup.Build()).Select(item => item.Type), Is.EqualTo(new[] { null, typeof(Latin), typeof(Accent) }));

                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
                Assert.That(Leaves(popup.Build()).Select(item => item.Type), Is.EqualTo(new[] { null, typeof(Accent), typeof(Latin) }));
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [Test]
        public void PublicPathArrays_CannotChangeLaterMenus ()
        {
            var popup = new Popup(new[] { typeof(Alpha) });
            popup.Build();

            string[] path = TypeMenuUtility.GetSplittedTypePath(typeof(Alpha));
            path[0] = "Changed category";
            path[1] = "Changed label";

            Assert.That(TypeMenuUtility.GetSplittedTypePath(typeof(Alpha)), Is.EqualTo(new[] { "Commands", "Alpha" }));
            Assert.That(Children(popup.Build())[1].name, Is.EqualTo("Commands"));
            Assert.That(Children(Children(popup.Build())[1])[0].name, Is.EqualTo("Alpha"));

            var otherPopup = new Popup(new[] { typeof(Alpha) });
            Assert.That(Children(otherPopup.Build())[1].name, Is.EqualTo("Commands"));
        }

        [Test]
        public void SelectionAfterRebuild_ReportsTypeAndNullItem ()
        {
            var popup = new Popup(new[] { typeof(Alpha), typeof(Zeta) });
            popup.Build();
            var selectedTypes = new List<Type>();
            popup.OnItemSelected += item => selectedTypes.Add(item.Type);

            AdvancedDropdownItem root = popup.Build();
            popup.Select(Leaves(root).Single(item => item.Type == typeof(Zeta)));
            popup.Select(Leaves(root).Single(item => item.Type == null));

            Assert.That(selectedTypes, Is.EqualTo(new[] { typeof(Zeta), null }));
        }

        private static AdvancedDropdownItem[] Children (AdvancedDropdownItem parent)
        {
#if UNITY_6000_5_OR_NEWER
            return parent.childList.ToArray();
#else
            return parent.children.ToArray();
#endif
        }

        private static IEnumerable<AdvancedTypePopupItem> Leaves (AdvancedDropdownItem root)
        {
            foreach (AdvancedDropdownItem child in Children(root))
            {
                if (child is AdvancedTypePopupItem item)
                {
                    yield return item;
                }
                foreach (AdvancedTypePopupItem descendant in Leaves(child))
                {
                    yield return descendant;
                }
            }
        }

    }
}
