using System;

namespace MackySoft.SerializeReferenceExtensions.Tests
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class DrawerContextAttribute : Attribute
    {
        public string Caption { get; }
        public int Increment { get; }
        public int Lines { get; }

        public DrawerContextAttribute (string caption, int increment, int lines)
        {
            Caption = caption;
            Increment = increment;
            Lines = lines;
        }
    }
}
