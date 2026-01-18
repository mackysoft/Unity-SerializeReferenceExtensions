#if UNITY_2023_2_OR_NEWER
using System;
using System.Reflection;

namespace MackySoft.SerializeReferenceExtensions.Editor
{
    public sealed class Unity_2023_2_OrNewer_GenericVarianceTypeCompatibilityPolicy : ITypeCompatibilityPolicy
    {

        public static readonly Unity_2023_2_OrNewer_GenericVarianceTypeCompatibilityPolicy Instance = new Unity_2023_2_OrNewer_GenericVarianceTypeCompatibilityPolicy();

        public bool IsCompatible (Type baseType, Type candiateType)
        {
            if (baseType == null)
            {
                throw new ArgumentNullException(nameof(baseType));
            }
            if (candiateType == null)
            {
                throw new ArgumentNullException(nameof(candiateType));
            }

            // If not generic, fall back to standard assignability check
            if (!baseType.IsGenericType)
            {
                return baseType.IsAssignableFrom(candiateType);
            }

            if (baseType.IsGenericTypeDefinition || baseType.ContainsGenericParameters)
            {
                return false;
            }

            // NOTE: baseType is constructed generic type
            Type genericTypeDefinition = baseType.GetGenericTypeDefinition();
            Type[] targetTypeArguments = baseType.GetGenericArguments();
            Type[] genericTypeParameters = genericTypeDefinition.GetGenericArguments();

            // Check interfaces
            foreach (Type interfaceType in candiateType.GetInterfaces())
            {
                if (!interfaceType.IsGenericType)
                {
                    continue;
                }
                if (interfaceType.GetGenericTypeDefinition() != genericTypeDefinition)
                {
                    continue;
                }

                Type[] sourceTypeArguments = interfaceType.GetGenericArguments();
                if (AreAllGenericArgumentsCompatible(genericTypeParameters, targetTypeArguments, sourceTypeArguments))
                {
                    return true;
                }
            }

            // Check base types
            for (Type t = candiateType; t != null && t != typeof(object); t = t.BaseType)
            {
                if (!t.IsGenericType)
                {
                    continue;
                }
                if (t.GetGenericTypeDefinition() != genericTypeDefinition)
                {
                    continue;
                }

                Type[] sourceTypeArguments = t.GetGenericArguments();
                if (AreAllGenericArgumentsCompatible(genericTypeParameters, targetTypeArguments, sourceTypeArguments))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool AreAllGenericArgumentsCompatible (Type[] genericTypeParameters, Type[] targetTypeArguments, Type[] sourceTypeArguments)
        {
            if (genericTypeParameters.Length != targetTypeArguments.Length)
            {
                return false;
            }
            if (sourceTypeArguments.Length != targetTypeArguments.Length)
            {
                return false;
            }

            for (int i = 0; i < genericTypeParameters.Length; i++)
            {
                var variance = genericTypeParameters[i].GenericParameterAttributes & GenericParameterAttributes.VarianceMask;

                Type sourceTypeArgument = sourceTypeArguments[i];
                Type targetTypeArgument = targetTypeArguments[i];

                if (variance == GenericParameterAttributes.Contravariant)
                {
                    // NOTE: in T = source must be able to accept the target.
                    if (!sourceTypeArgument.IsAssignableFrom(targetTypeArgument))
                    {
                        return false;
                    }
                }
                else if (variance == GenericParameterAttributes.Covariant)
                {
                    // NOTE: out T = target must be able to accept the source.
                    if (!targetTypeArgument.IsAssignableFrom(sourceTypeArgument))
                    {
                        return false;
                    }
                }
                else
                {
                    // invariant
                    if (sourceTypeArgument != targetTypeArgument)
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
#endif