namespace MackySoft.SerializeReferenceExtensions.Editor
{
    public static class TypeSearchService
    {

        public static readonly IIntrinsicTypePolicy IntrinsicTypePolicy;
        public static readonly ITypeCompatibilityPolicy TypeCompatibilityPolicy;
        public static readonly ITypeCandiateProvider TypeCandiateProvider;
        public static readonly TypeCandiateService TypeCandiateService;

        static TypeSearchService ()
        {
            IntrinsicTypePolicy = DefaultIntrinsicTypePolicy.Instance;

#if UNITY_2023_2_OR_NEWER
            TypeCompatibilityPolicy = Unity_2023_2_OrNewer_GenericVarianceTypeCompatibilityPolicy.Instance;
            TypeCandiateProvider = Unity_2023_2_OrNewer_TypeCandiateProvider.Instance;
#else
            TypeCompatibilityPolicy = DefaultTypeCompatibilityPolicy.Instance;
            TypeCandiateProvider = DefaultTypeCandiateProvider.Instance;
#endif

            TypeCandiateService = new TypeCandiateService(TypeCandiateProvider, IntrinsicTypePolicy, TypeCompatibilityPolicy);
        }
    }
}