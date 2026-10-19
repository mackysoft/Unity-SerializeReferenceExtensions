using System;
using System.Collections.Generic;
using UnityEngine;

namespace MackySoft.SerializeReferenceExtensions.Validation
{
    [Serializable, AddTypeMenu("Values/Nested")]
    public sealed class NestedValue : IInspectionValue
    {
        [SerializeField] private int nativeNumber;
        [SerializeReference, SubclassSelector] private IInspectionValue child = new NumberValue();
        [SerializeReference, SubclassSelector] private IInspectionValue[] array = { new NumberValue(), null, new TextValue() };
        [SerializeReference, SubclassSelector] private List<IInspectionValue> list = new List<IInspectionValue> { new NumberValue(), null, new TextValue() };
        [SerializeReference, SubclassSelector, NonReorderable] private IInspectionValue[] simpleArray = { new NumberValue(), null, new TextValue() };
        [SerializeReference, SubclassSelector, NonReorderable] private List<IInspectionValue> simpleList = new List<IInspectionValue> { new NumberValue(), null, new TextValue() };
    }
}
