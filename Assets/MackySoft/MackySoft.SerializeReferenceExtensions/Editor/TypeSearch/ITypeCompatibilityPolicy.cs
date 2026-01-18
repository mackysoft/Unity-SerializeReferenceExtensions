using System;

namespace MackySoft.SerializeReferenceExtensions.Editor
{
    public interface ITypeCompatibilityPolicy
    {
        bool IsCompatible (Type baseType, Type candiateType);
    }
}