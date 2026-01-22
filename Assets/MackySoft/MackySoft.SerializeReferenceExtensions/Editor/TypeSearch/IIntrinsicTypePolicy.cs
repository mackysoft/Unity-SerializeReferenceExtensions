using System;

namespace MackySoft.SerializeReferenceExtensions.Editor
{
    public interface IIntrinsicTypePolicy
    {
        bool IsAllowed (Type candiateType, bool ignoreGenericTypeCheck);
    }
}