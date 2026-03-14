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

            // Prepare generic inference data upfront
            bool baseIsConstructedGeneric = baseType.IsGenericType && !baseType.IsGenericTypeDefinition && !baseType.ContainsGenericParameters;
            Type baseGenericDef = baseIsConstructedGeneric ? baseType.GetGenericTypeDefinition() : null;
            Type[] baseTypeArgs = baseIsConstructedGeneric ? baseType.GetGenericArguments() : null;

            // Single pass over all types
            foreach (Type type in EnumerateAllTypesSafely())
            {
                // Existing: check closed/non-generic candidates
                if (intrinsicTypePolicy.IsAllowed(type) && typeCompatibilityPolicy.IsCompatible(baseType, type))
                {
                    result.Add(type);
                    continue;
                }

                // New: try to close open generic candidates
                if (baseIsConstructedGeneric && type.IsGenericTypeDefinition && Attribute.IsDefined(type, typeof(SerializableAttribute)))
                {
                    Type closedType = TryCloseGenericType(type, baseGenericDef, baseTypeArgs);
                    if (closedType != null && intrinsicTypePolicy.IsAllowed(closedType))
                    {
                        result.Add(closedType);
                    }
                }
            }

            if (intrinsicTypePolicy.IsAllowed(baseType) && typeCompatibilityPolicy.IsCompatible(baseType, baseType))
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
        private static Type TryCloseGenericType (Type openCandidateType, Type baseGenericDef, Type[] baseTypeArgs)
        {
            // openCandidateType is e.g. ConstantValueProvider<T>
            // baseGenericDef is e.g. IValueProvider<> 
            // baseTypeArgs is e.g. [int]

            Type[] candidateGenericParams = openCandidateType.GetGenericArguments();
            Type[] resolvedArgs = new Type[candidateGenericParams.Length];

            // Walk the candidate's interfaces to find one matching the base generic definition
            foreach (Type iface in openCandidateType.GetInterfaces())
            {
                if (!iface.IsGenericType) continue;
                if (iface.GetGenericTypeDefinition() != baseGenericDef) continue;

                Type[] ifaceArgs = iface.GetGenericArguments();
                if (ifaceArgs.Length != baseTypeArgs.Length) continue;

                if (TryMapTypeArguments(candidateGenericParams, ifaceArgs, baseTypeArgs, resolvedArgs))
                {
                    return TryMakeGenericTypeSafe(openCandidateType, resolvedArgs);
                }
            }

            // Walk base class chain
            for (Type t = openCandidateType.BaseType; t != null && t != typeof(object); t = t.BaseType)
            {
                if (!t.IsGenericType) continue;
                if (t.GetGenericTypeDefinition() != baseGenericDef) continue;

                Type[] tArgs = t.GetGenericArguments();
                if (tArgs.Length != baseTypeArgs.Length) continue;

                if (TryMapTypeArguments(candidateGenericParams, tArgs, baseTypeArgs, resolvedArgs))
                {
                    return TryMakeGenericTypeSafe(openCandidateType, resolvedArgs);
                }
            }

            return null;
        }

        private static bool TryMapTypeArguments (Type[] candidateGenericParams, Type[] ifaceArgs, Type[] baseTypeArgs, Type[] resolvedArgs)
        {
            // Reset
            for (int i = 0; i < resolvedArgs.Length; i++)
            {
                resolvedArgs[i] = null;
            }

            // For each type argument in the interface/base, map it back to the candidate's generic parameter
            // e.g. IValueProvider<T> has ifaceArgs=[T], baseTypeArgs=[int]
            // We need to find that T is candidateGenericParams[0], so resolvedArgs[0] = int
            for (int i = 0; i < ifaceArgs.Length; i++)
            {
                Type ifaceArg = ifaceArgs[i];
                Type targetArg = baseTypeArgs[i];

                if (ifaceArg.IsGenericParameter)
                {
                    int position = ifaceArg.GenericParameterPosition;
                    if (position < 0 || position >= resolvedArgs.Length) return false;

                    if (resolvedArgs[position] != null && resolvedArgs[position] != targetArg)
                    {
                        // Conflicting mapping for the same parameter
                        return false;
                    }
                    resolvedArgs[position] = targetArg;
                }
                else
                {
                    // The interface argument is already concrete — it must match exactly
                    if (ifaceArg != targetArg) return false;
                }
            }

            // Check all parameters were resolved
            for (int i = 0; i < resolvedArgs.Length; i++)
            {
                if (resolvedArgs[i] == null) return false;
            }

            return true;
        }

        private static Type TryMakeGenericTypeSafe (Type openType, Type[] typeArgs)
        {
            try
            {
                return openType.MakeGenericType(typeArgs);
            }
            catch (ArgumentException)
            {
                // Constraint violation (e.g. where T : struct but we passed a class)
                return null;
            }
        }
    }
}
#endif
