using System;
using UnityEngine;

namespace MackySoft.SerializeReferenceExtensions.Editor
{
    public sealed class DefaultIntrinsicTypePolicy : IIntrinsicTypePolicy
    {

        public static readonly DefaultIntrinsicTypePolicy Instance = new DefaultIntrinsicTypePolicy();

        public bool IsAllowed (Type candiateType, bool ignoreGenericTypeCheck)
        {
            return
                (candiateType.IsPublic || candiateType.IsNestedPublic || candiateType.IsNestedPrivate) &&
                !candiateType.IsAbstract &&
                (ignoreGenericTypeCheck || !candiateType.IsGenericType) &&
                !candiateType.IsPrimitive &&
                !candiateType.IsEnum &&
                !typeof(UnityEngine.Object).IsAssignableFrom(candiateType) &&
                Attribute.IsDefined(candiateType, typeof(SerializableAttribute)) &&
                !Attribute.IsDefined(candiateType, typeof(HideInTypeMenuAttribute));
        }
    }
}