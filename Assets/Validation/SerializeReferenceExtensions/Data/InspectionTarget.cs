using System;
using System.Collections.Generic;
using UnityEngine;

namespace MackySoft.SerializeReferenceExtensions.Validation
{
    /// <summary>Persistent data for manual Inspector operations; both hosts inherit the same fields.</summary>
    public abstract class InspectionTarget : MonoBehaviour
    {
        [Header("Unity standard")]
        [SerializeField] private int nativeNumber;
        [SerializeField] private KeyCode nativePopup;
        [SerializeReference] private IInspectionValue nativeReference = new NumberValue();
        [SerializeReference] private IInspectionValue[] nativeArray = { new NumberValue(101, "first"), null, new NumberValue(103, "last") };
        [SerializeReference] private List<IInspectionValue> nativeList = new List<IInspectionValue> { new NumberValue(201, "first"), null, new NumberValue(203, "last") };
        [SerializeReference, NonReorderable] private IInspectionValue[] nativeSimpleArray = { new NumberValue(301, "first"), null, new NumberValue(303, "last") };
        [SerializeReference, NonReorderable] private List<IInspectionValue> nativeSimpleList = new List<IInspectionValue> { new NumberValue(401, "first"), null, new NumberValue(403, "last") };

        [Header("SubclassSelector")]
        [SerializeReference, SubclassSelector] private IInspectionValue reference = new NumberValue();
        [SerializeReference, SubclassSelector] private IInspectionValue nullReference;
        [SerializeReference, SubclassSelector] private IInspectionValue emptyValue = new EmptyValue();
        [SerializeReference, SubclassSelector(UseToStringAsLabel = true)] private IInspectionValue described = new NumberValue(51, "ToString label");
        [SerializeReference, SubclassSelector] private IInspectionValue[] array = { new NumberValue(101, "first"), null, new NumberValue(103, "last") };
        [SerializeReference, SubclassSelector(UseToStringAsLabel = true)] private List<IInspectionValue> list = new List<IInspectionValue> { new NumberValue(201, "first"), null, new NumberValue(203, "last") };
        [SerializeReference, SubclassSelector, NonReorderable] private IInspectionValue[] simpleArray = { new NumberValue(301, "first"), null, new NumberValue(303, "last") };
        [SerializeReference, SubclassSelector, NonReorderable] private List<IInspectionValue> simpleList = new List<IInspectionValue> { new NumberValue(401, "first"), null, new NumberValue(403, "last") };
        [SerializeReference, SubclassSelector] private IInspectionValue[] emptyArray = Array.Empty<IInspectionValue>();
        [SerializeReference, SubclassSelector] private List<IInspectionValue> emptyList = new List<IInspectionValue>();
        [SerializeReference, SubclassSelector] private IInspectionValue nested = new NestedValue();

        [Header("Other property drawers")]
        [SerializeReference, SubclassSelector] private IInspectionValue imguiDrawer = new IMGUIValue();
        [SerializeReference, SubclassSelector] private IInspectionValue uitoolkitDrawer = new UIToolkitValue();
        [SerializeReference, SubclassSelector] private IInspectionValue dualDrawer = new DualValue();
        [SerializeReference, SubclassSelector] private IInspectionValue inheritedDrawer = new InheritedDrawerValue();
        [SerializeReference, SubclassSelector] private IInspectionValue[] drawerArray = { new IMGUIValue(), new UIToolkitValue(), new DualValue(), null };

        [Header("Header / Space / Tooltip / TextArea")]
        [Space, Tooltip("Change type, edit TextArea, then Undo and Redo.")]
        [SerializeReference, SubclassSelector] private IInspectionValue decorated = new TextValue();
    }
}
