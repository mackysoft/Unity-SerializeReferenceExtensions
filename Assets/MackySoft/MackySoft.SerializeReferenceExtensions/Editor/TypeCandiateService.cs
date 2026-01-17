using System;
using System.Collections.Generic;
using System.Linq;

namespace MackySoft.SerializeReferenceExtensions.Editor
{
    public sealed class TypeCandiateService
    {

        private readonly ITypeCandiateProvider typeCandiateProvider;
        private readonly IIntrinsicTypePolicy intrinsicTypePolicy;
        private readonly ITypeCompatibilityPolicy typeCompatibilityPolicy;

        private readonly Dictionary<Type, Type[]> typeCache = new Dictionary<Type, Type[]>();

        public TypeCandiateService (ITypeCandiateProvider typeCandiateProvider, IIntrinsicTypePolicy intrinsicTypePolicy, ITypeCompatibilityPolicy typeCompatibilityPolicy)
        {
            this.typeCandiateProvider = typeCandiateProvider ?? throw new ArgumentNullException(nameof(typeCandiateProvider));
            this.intrinsicTypePolicy = intrinsicTypePolicy ?? throw new ArgumentNullException(nameof(intrinsicTypePolicy));
            this.typeCompatibilityPolicy = typeCompatibilityPolicy ?? throw new ArgumentNullException(nameof(typeCompatibilityPolicy));
        }

        public IReadOnlyList<Type> GetDisplayableTypes (Type baseType)
        {
            if (baseType == null)
            {
                throw new ArgumentNullException(nameof(baseType));
            }
            if (typeCache.TryGetValue(baseType, out Type[] cachedTypes))
            {
                return cachedTypes;
            }

            var candiateTypes = typeCandiateProvider.GetTypeCandidates(baseType);
            var result = candiateTypes
                .Where(intrinsicTypePolicy.IsAllowed)
                .Where(t => typeCompatibilityPolicy.IsCompatible(baseType, t))
                .Distinct()
                .ToArray();

            typeCache.Add(baseType, result);
            return result;
        }
    }
}