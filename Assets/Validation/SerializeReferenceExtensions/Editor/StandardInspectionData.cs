using System.Collections.Generic;
using UnityEngine;

namespace MackySoft.SerializeReferenceExtensions.Validation
{
    /// <summary>Window-owned comparison copy with the root SubclassSelector attributes removed.</summary>
    internal sealed class StandardInspectionData : ScriptableObject
    {
        // Keep field names, declared types and native attributes equal to InspectionTarget.
        // CopySerializedManagedFieldsOnly supplies values; native drawers still own rendering.
        [Header("Unity standard")]
        [SerializeField] private int nativeNumber;
        [SerializeField] private KeyCode nativePopup;
        [SerializeReference] private IInspectionValue nativeReference;
        [SerializeReference] private IInspectionValue[] nativeArray;
        [SerializeReference] private List<IInspectionValue> nativeList;
        [SerializeReference, NonReorderable] private IInspectionValue[] nativeSimpleArray;
        [SerializeReference, NonReorderable] private List<IInspectionValue> nativeSimpleList;

        [Header("SubclassSelector")]
        [SerializeReference] private IInspectionValue reference;
        [SerializeReference] private IInspectionValue nullReference;
        [SerializeReference] private IInspectionValue emptyValue;
        [SerializeReference] private IInspectionValue described;
        [SerializeReference] private IInspectionValue[] array;
        [SerializeReference] private List<IInspectionValue> list;
        [SerializeReference, NonReorderable] private IInspectionValue[] simpleArray;
        [SerializeReference, NonReorderable] private List<IInspectionValue> simpleList;
        [SerializeReference] private IInspectionValue[] emptyArray;
        [SerializeReference] private List<IInspectionValue> emptyList;
        [SerializeReference] private IInspectionValue nested;

        [Header("Other property drawers")]
        [SerializeReference] private IInspectionValue imguiDrawer;
        [SerializeReference] private IInspectionValue uitoolkitDrawer;
        [SerializeReference] private IInspectionValue dualDrawer;
        [SerializeReference] private IInspectionValue inheritedDrawer;
        [SerializeReference] private IInspectionValue[] drawerArray;

        [Header("Header / Space / Tooltip / TextArea")]
        [Space, Tooltip("Change type, edit TextArea, then Undo and Redo.")]
        [SerializeReference] private IInspectionValue decorated;
    }
}
