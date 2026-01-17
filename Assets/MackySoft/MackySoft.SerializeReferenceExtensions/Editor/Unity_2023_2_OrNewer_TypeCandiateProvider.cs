#if UNITY_2023_2_OR_NEWER
using System;
using System.Collections.Generic;
using System.Reflection;

namespace MackySoft.SerializeReferenceExtensions.Editor
{
    public sealed class Unity_2023_2_OrNewer_TypeCandiateProvider : ITypeCandiateProvider
    {

        public static readonly Unity_2023_2_OrNewer_TypeCandiateProvider Instance = new Unity_2023_2_OrNewer_TypeCandiateProvider(
            DefaultIntrinsicTypePolicy.Instance,
            Unity_2023_2_OrNewer_GenericVarianceTypeCompatibilityPolicy.Instance
        );

        private Dictionary<Type, List<Type>> m_TypeCache = new Dictionary<Type, List<Type>>();

        private readonly IIntrinsicTypePolicy m_IntrinsicTypePolicy;
        private readonly ITypeCompatibilityPolicy m_TypeCompatibilityPolicy;

        private Unity_2023_2_OrNewer_TypeCandiateProvider (
            IIntrinsicTypePolicy intrinsicTypePolicy,
            ITypeCompatibilityPolicy typeCompatibilityPolicy
        )
        {
            m_IntrinsicTypePolicy = intrinsicTypePolicy ?? throw new ArgumentNullException(nameof(intrinsicTypePolicy));
            m_TypeCompatibilityPolicy = typeCompatibilityPolicy ?? throw new ArgumentNullException(nameof(typeCompatibilityPolicy));
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
            if (m_TypeCache.TryGetValue(baseType, out List<Type> result))
            {
                return result;
            }

            result = new List<Type>();

            IEnumerable<Type> types = EnumerateAllTypesSafely();
            foreach (Type type in types)
            {
                if (!m_IntrinsicTypePolicy.IsAllowed(type))
                {
                    continue;
                }
                if (!m_TypeCompatibilityPolicy.IsCompatible(baseType, type))
                {
                    continue;
                }

                result.Add(type);
            }

            // Include the base type itself if allowed
            if (m_IntrinsicTypePolicy.IsAllowed(baseType) && m_TypeCompatibilityPolicy.IsCompatible(baseType, baseType))
            {
                result.Add(baseType);
            }

            m_TypeCache.Add(baseType, result);
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
