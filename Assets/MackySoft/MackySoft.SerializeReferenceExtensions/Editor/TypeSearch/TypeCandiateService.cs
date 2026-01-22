using System;
using System.Collections.Generic;
using System.Linq;

namespace MackySoft.SerializeReferenceExtensions.Editor
{
    public sealed class TypeCandiateService
    {

        private readonly ITypeCandiateProvider typeCandiateProvider;

        private readonly Dictionary<Type, Type[]> typeCache = new Dictionary<Type, Type[]>();

        public TypeCandiateService (ITypeCandiateProvider typeCandiateProvider)
        {
            this.typeCandiateProvider = typeCandiateProvider ?? throw new ArgumentNullException(nameof(typeCandiateProvider));
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
                .Distinct()
                .ToArray();

            typeCache.Add(baseType, result);
            return result;
        }
    }
}