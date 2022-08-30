#if UNITY_2019_3_OR_NEWER
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

namespace MackySoft.SerializeReferenceExtensions.Editor {

	public class AdvancedTypePopupItem : AdvancedDropdownItem {

		public Type Type { get; }

		public AdvancedTypePopupItem (Type type,string name) : base(name) {
			Type = type;
		}

	}

	/// <summary>
	/// A type popup with a fuzzy finder.
	/// </summary>
	public class AdvancedTypePopup : AdvancedDropdown {

		const int kMaxNamespaceNestCount = 16;

		public static void AddTo (AdvancedDropdownItem root,IEnumerable<Type> types) {
			int itemCount = 0;

			// Add null item.
			var nullItem = new AdvancedTypePopupItem(null,TypeMenuUtility.k_NullDisplayName) {
				id = itemCount++
			};
			root.AddChild(nullItem);

			Type[] typeArray = types.OrderByType().ToArray();

			// Single namespace if the root has one namespace and the nest is unbranched.
			bool isSingleNamespace = true;
			string[] namespaces = new string[kMaxNamespaceNestCount];
			foreach (Type type in typeArray) {
				string[] splittedTypePath = TypeMenuUtility.GetSplittedTypePath(type);
				if (splittedTypePath.Length <= 1) {
					continue;
				}
				// If they explicitly want sub category, let them do.
				if (TypeMenuUtility.GetAttribute(type) != null) {
					isSingleNamespace = false;
					break;
				}
				for (int k = 0;(splittedTypePath.Length - 1) > k;k++) {
					string ns = namespaces[k];
					if (ns == null) {
						namespaces[k] = splittedTypePath[k];
					}
					else if (ns != splittedTypePath[k]) {
						isSingleNamespace = false;
						break;
					}
				}

				if (!isSingleNamespace) {
					break;
				}
			}

			// Add type items.
			foreach (Type type in typeArray) {
				string[] splittedTypePath = TypeMenuUtility.GetSplittedTypePath(type);
				if (splittedTypePath.Length == 0) {
					continue;
				}

				AdvancedDropdownItem parent = root;

				// Add namespace items.
				if (!isSingleNamespace) {
					for (int k = 0;(splittedTypePath.Length - 1) > k;k++) {
						AdvancedDropdownItem foundItem = GetItem(parent,splittedTypePath[k]);
						if (foundItem != null) {
							parent = foundItem;
						}
						else {
							var newItem = new AdvancedDropdownItem(splittedTypePath[k]) {
								id = itemCount++,
							};
							parent.AddChild(newItem);
							parent = newItem;
						}
					}
				}

				// Add type item.
				var item = new AdvancedTypePopupItem(type,ObjectNames.NicifyVariableName(splittedTypePath[splittedTypePath.Length - 1])) {
					id = itemCount++
				};
				parent.AddChild(item);
			}
		}

		static AdvancedDropdownItem GetItem (AdvancedDropdownItem parent,string name) {
			foreach (AdvancedDropdownItem item in parent.children) {
				if (item.name == name) {
					return item;
				}
			}
			return null;
		}

		static readonly float k_HeaderHeight = EditorGUIUtility.singleLineHeight * 2f;

		Type[] m_Types;

		public event Action<AdvancedTypePopupItem> OnItemSelected;
		
		public AdvancedTypePopup (IEnumerable<Type> types,int maxLineCount,AdvancedDropdownState state) : base(state) {
			SetTypes(types);
			minimumSize = new Vector2(minimumSize.x,EditorGUIUtility.singleLineHeight * maxLineCount + k_HeaderHeight);
		}

		public void SetTypes (IEnumerable<Type> types) {
			m_Types = types.ToArray();
		}

		protected override AdvancedDropdownItem BuildRoot () {
			var root = new AdvancedDropdownItem("Select Type");
			AddTo(root,m_Types);
			return root;
		}

		protected override void ItemSelected (AdvancedDropdownItem item) {
			base.ItemSelected(item);
			if (item is AdvancedTypePopupItem typePopupItem) {
				OnItemSelected?.Invoke(typePopupItem);
			}
		}

	}
}
#endif