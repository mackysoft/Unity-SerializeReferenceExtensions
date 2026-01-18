using System;

namespace MackySoft.SerializeReferenceExtensions.Editor
{
    public interface IIntrinsicTypePolicy
    {
        bool IsAllowed (Type candiateType);
    }

    public sealed class DefaultIntrinsicTypePolicy : IIntrinsicTypePolicy
    {

        public static readonly DefaultIntrinsicTypePolicy Instance = new DefaultIntrinsicTypePolicy();

        public bool IsAllowed (Type candiateType)
        {
            return
                (candiateType.IsPublic || candiateType.IsNestedPublic || candiateType.IsNestedPrivate) &&
                !candiateType.IsAbstract &&
                !candiateType.IsGenericType &&
                !candiateType.IsPrimitive &&
                !candiateType.IsEnum &&
                !typeof(UnityEngine.Object).IsAssignableFrom(candiateType) &&
                Attribute.IsDefined(candiateType, typeof(SerializableAttribute)) &&
                !Attribute.IsDefined(candiateType, typeof(HideInTypeMenuAttribute));
        }
    }
}