using System;
using UnityEngine;

namespace MackySoft.SerializeReferenceExtensions.Validation
{
    [Serializable, AddTypeMenu("Values/Text", 1)]
    public sealed class TextValue : IInspectionValue
    {
        [SerializeField] private int number = 84;
        [SerializeField, TextArea] private string text = "TextArea\nSecond line";

        public override string ToString () => $"{number}: {text}";
    }
}
