using System;
using System.Linq.Expressions;
using System.Reflection;
using UnityEngine.UIElements;

namespace MackySoft.SerializeReferenceExtensions.Editor
{
    // Match PropertyField's control setup without changing native layout or binding.
    internal static class NativePropertyFieldControls
    {
        private static readonly Action<Toggle> EnableDisabledClicks = BindEnableDisabledClicks();

        internal static void ConfigureFoldout (Foldout foldout)
            => EnableDisabledClicks(foldout.Q<Toggle>(className: Foldout.toggleUssClassName));

        internal static void ConfigureListView (ListView list)
        {
            list.selectionType = CollectionSelectionType;
            ConfigureCollectionFoldout(list);
        }

        // PropertyField first enables multiple selection in 2022.3.5, 2023.1.2
        // and 2023.2. Keep the same patch-level boundaries as Unity's implementation.
        private static SelectionType CollectionSelectionType
        {
            get
            {
#if SERIALIZE_REFERENCE_EXTENSIONS_MULTIPLE_SELECTION || UNITY_2023_2_OR_NEWER
                return SelectionType.Multiple;
#else
                return SelectionType.Single;
#endif
            }
        }

        private static void ConfigureCollectionFoldout (ListView list)
        {
#if SERIALIZE_REFERENCE_EXTENSIONS_COLLECTION_DISABLED_CLICKS || UNITY_2022_3_OR_NEWER
            // ConfigureListView enables its header's disabled clicks from 2022.2.16.
            ConfigureFoldout(list.Q<Foldout>());
#endif
        }

        private static Action<Toggle> BindEnableDisabledClicks ()
        {
            const BindingFlags members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            FieldInfo clickable = typeof(Toggle).BaseType.GetField("m_Clickable", members)
                ?? throw new MissingFieldException(typeof(Toggle).BaseType.FullName, "m_Clickable");
            PropertyInfo acceptClicks = typeof(Clickable).GetProperty("acceptClicksIfDisabled", members)
                ?? throw new MissingMemberException(typeof(Clickable).FullName, "acceptClicksIfDisabled");
            var toggle = Expression.Parameter(typeof(Toggle), "toggle");
            return Expression.Lambda<Action<Toggle>>(
                Expression.Assign(Expression.Property(Expression.Field(toggle, clickable), acceptClicks), Expression.Constant(true)), toggle).Compile();
        }
    }
}
