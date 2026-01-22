#if UNITY_2023_2_OR_NEWER
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;

namespace MackySoft.SerializeReferenceExtensions.Editor
{
    public sealed class Unity_2023_2_OrNewer_TypeCandiateProvider : ITypeCandiateProvider
    {

        public static readonly Unity_2023_2_OrNewer_TypeCandiateProvider Instance = new Unity_2023_2_OrNewer_TypeCandiateProvider(
            DefaultIntrinsicTypePolicy.Instance,
            Unity_2023_2_OrNewer_GenericVarianceTypeCompatibilityPolicy.Instance
        );

        private readonly Dictionary<Type, List<Type>> typeCache = new Dictionary<Type, List<Type>>();

        private readonly IIntrinsicTypePolicy intrinsicTypePolicy;
        private readonly ITypeCompatibilityPolicy typeCompatibilityPolicy;

        private Unity_2023_2_OrNewer_TypeCandiateProvider (
            IIntrinsicTypePolicy intrinsicTypePolicy,
            ITypeCompatibilityPolicy typeCompatibilityPolicy
        )
        {
            this.intrinsicTypePolicy = intrinsicTypePolicy ?? throw new ArgumentNullException(nameof(intrinsicTypePolicy));
            this.typeCompatibilityPolicy = typeCompatibilityPolicy ?? throw new ArgumentNullException(nameof(typeCompatibilityPolicy));
        }

        public IEnumerable<Type> GetTypeCandidates (Type baseType)
        {
            if (!baseType.IsGenericType)
            {
                return DefaultTypeCandiateProvider.Instance.GetTypeCandidates(baseType);
            }

            return GetTypesWithGeneric(baseType);
        }

        private IEnumerable<Type> GetTypesWithGeneric (Type baseType)
        {
            if (typeCache.TryGetValue(baseType, out List<Type> result))
            {
                return result;
            }

            result = new List<Type>();

            // 
            var isGenericBaseType = baseType.IsGenericType;
            var genericTypeDefinition = isGenericBaseType ? baseType.GetGenericTypeDefinition() : baseType;
            var targetTypeArguments = isGenericBaseType ? baseType.GetGenericArguments() : Type.EmptyTypes;
            IEnumerable<Type> types = TypeCache.GetTypesDerivedFrom(genericTypeDefinition);
            foreach (Type type in types)
            {
                // If the type is Generic, create a MakeGenericType from the Arguments of the baseType.
                var targetType = type.IsGenericType ? type.MakeGenericType(targetTypeArguments) : type;

                if (!intrinsicTypePolicy.IsAllowed(targetType, targetType != type))
                {
                    continue;
                }
                if (!typeCompatibilityPolicy.IsCompatible(baseType, targetType))
                {
                    continue;
                }

                result.Add(targetType);
            }

            // Include the base type itself if allowed
            if (intrinsicTypePolicy.IsAllowed(baseType, false) && typeCompatibilityPolicy.IsCompatible(baseType, baseType))
            {
                result.Add(baseType);
            }

            typeCache.Add(baseType, result);
            return result;
        }

        private static IEnumerable<Type> EnumerateAllTypesSafely ()
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = asm.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types;
                }

                if (types == null)
                {
                    continue;
                }

                foreach (var t in types)
                {
                    if (t != null)
                    {
                        yield return t;
                    }
                }
            }
        }
    }
}
#endif
