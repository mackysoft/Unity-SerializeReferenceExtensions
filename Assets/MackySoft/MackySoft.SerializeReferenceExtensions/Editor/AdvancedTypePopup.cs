using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace MackySoft.SerializeReferenceExtensions.Editor
{

    public class AdvancedTypePopupItem : AdvancedDropdownItem
    {

        public Type Type { get; }

        public AdvancedTypePopupItem (Type type, string name) : base(name)
        {
            Type = type;
        }

    }

    /// <summary>
    /// A type popup with a fuzzy finder.
    /// </summary>
    public class AdvancedTypePopup : AdvancedDropdown
    {

        private const int MaxNamespaceNestCount = 16;

        private readonly struct TypeMenuItem
        {

            public Type Type { get; }
            public string[] Path { get; }
            public string Name { get; }

            public TypeMenuItem (Type type)
            {
                Type = type;
                Path = TypeMenuUtility.GetSplittedTypePath(type);
                Name = Path.Length > 0 ? ObjectNames.NicifyVariableName(Path[Path.Length - 1]) : string.Empty;
            }

        }

        private sealed class TypeMenu
        {

            public CultureInfo Culture { get; }
            public TypeMenuItem[] Items { get; }
            public bool IsSingleNamespace { get; }

            public TypeMenu (IEnumerable<Type> types)
            {
                Culture = CultureInfo.CurrentCulture;
                Items = types.OrderByType().Select(type => new TypeMenuItem(type)).ToArray();
                IsSingleNamespace = HasSingleNamespace(Items);
            }

        }

        private sealed class SelectionSession : AdvancedDropdown
        {
            private readonly AdvancedTypePopup owner;
            private Action<AdvancedTypePopupItem> callback;

            internal SelectionSession (AdvancedTypePopup owner, AdvancedDropdownState state, Action<AdvancedTypePopupItem> callback) : base(state)
            {
                this.owner = owner;
                this.callback = callback;
                minimumSize = owner.minimumSize;
            }

            protected override AdvancedDropdownItem BuildRoot ()
            {
                return owner.BuildRoot();
            }

            protected override void ItemSelected (AdvancedDropdownItem item)
            {
                try
                {
                    if (item is AdvancedTypePopupItem typeItem)
                    {
                        callback?.Invoke(typeItem);
                    }
                }
                finally
                {
                    callback = null;
                }
            }
        }

        private static bool HasSingleNamespace (TypeMenuItem[] items)
        {
            // Flatten an unbranched namespace unless an explicit menu requests a category.
            string[] namespaces = new string[MaxNamespaceNestCount];
            foreach (TypeMenuItem item in items)
            {
                string[] splittedTypePath = item.Path;
                if (splittedTypePath.Length <= 1)
                {
                    continue;
                }
                if (TypeMenuUtility.GetAttribute(item.Type) != null)
                {
                    return false;
                }
                for (int k = 0; (splittedTypePath.Length - 1) > k; k++)
                {
                    string ns = namespaces[k];
                    if (ns == null)
                    {
                        namespaces[k] = splittedTypePath[k];
                    }
                    else if (ns != splittedTypePath[k])
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public static void AddTo (AdvancedDropdownItem root, IEnumerable<Type> types)
        {
            // Direct callers still get a freshly sorted menu for their input.
            AddTo(root, new TypeMenu(types));
        }

        private static void AddTo (AdvancedDropdownItem root, TypeMenu menu)
        {
            int itemCount = 0;

            // Add null item.
            var nullItem = new AdvancedTypePopupItem(null, TypeMenuUtility.NullDisplayName)
            {
                id = itemCount++
            };
            root.AddChild(nullItem);

            // Add type items.
            foreach (TypeMenuItem menuItem in menu.Items)
            {
                string[] splittedTypePath = menuItem.Path;
                if (splittedTypePath.Length == 0)
                {
                    continue;
                }

                AdvancedDropdownItem parent = root;

                // Add namespace items.
                if (!menu.IsSingleNamespace)
                {
                    for (int k = 0; (splittedTypePath.Length - 1) > k; k++)
                    {
                        AdvancedDropdownItem foundItem = GetItem(parent, splittedTypePath[k]);
                        if (foundItem != null)
                        {
                            parent = foundItem;
                        }
                        else
                        {
                            var newItem = new AdvancedDropdownItem(splittedTypePath[k])
                            {
                                id = itemCount++,
                            };
                            parent.AddChild(newItem);
                            parent = newItem;
                        }
                    }
                }

                // Add type item.
                var item = new AdvancedTypePopupItem(menuItem.Type, menuItem.Name)
                {
                    id = itemCount++
                };
                parent.AddChild(item);
            }
        }

        private static AdvancedDropdownItem GetItem (AdvancedDropdownItem parent, string name)
        {
#if UNITY_6000_5_OR_NEWER // AdvancedDropdownItem.children is obsolete in Unity 6.5+, use more optimized childList
            foreach (AdvancedDropdownItem item in parent.childList)
#else
            foreach (AdvancedDropdownItem item in parent.children)
#endif
            {
                if (item.name == name)
                {
                    return item;
                }
            }
            return null;
        }

        private static readonly float HeaderHeight = EditorGUIUtility.singleLineHeight * 2f;

        private Type[] types;
        private TypeMenu menu;
        private readonly AdvancedDropdownState state;

        public event Action<AdvancedTypePopupItem> OnItemSelected;

        public AdvancedTypePopup (IEnumerable<Type> types, int maxLineCount, AdvancedDropdownState state) : base(state)
        {
            this.state = state;
            SetTypes(types);
            minimumSize = new Vector2(minimumSize.x, EditorGUIUtility.singleLineHeight * maxLineCount + HeaderHeight);
        }

        public void SetTypes (IEnumerable<Type> types)
        {
            this.types = types.ToArray();
            menu = null;
        }

        internal void ShowForSelection (Rect position, Action<AdvancedTypePopupItem> callback)
        {
            // Only the native window owns this session. The cached metadata owner retains no target callback.
            new SelectionSession(this, state, callback).Show(position);
        }

        protected override AdvancedDropdownItem BuildRoot ()
        {
            var root = new AdvancedDropdownItem("Select Type");

            // Keep display metadata private to this snapshot. Ordering follows the current culture.
            if (menu == null || !ReferenceEquals(menu.Culture, CultureInfo.CurrentCulture))
            {
                menu = new TypeMenu(types);
            }
            AddTo(root, menu);
            return root;
        }

        protected override void ItemSelected (AdvancedDropdownItem item)
        {
            base.ItemSelected(item);
            if (item is AdvancedTypePopupItem typePopupItem)
            {
                OnItemSelected?.Invoke(typePopupItem);
            }
        }

    }
}
