using System;
using UnityEngine;

namespace MackySoft.SerializeReferenceExtensions.Validation
{
    [Serializable, AddTypeMenu("Values/Number", 0)]
    public class NumberValue : IInspectionValue
    {
        [SerializeField] private int number = 42;
        [SerializeField] private string text = "preserve this value when changing type";

        public NumberValue () { }

        public NumberValue (int number, string text)
        {
            this.number = number;
            this.text = text;
        }

        public override string ToString () => $"{number}: {text}";
    }
}
