namespace MackySoft.SerializeReferenceExtensions.Editor
{
    public static class TypeSearchService
    {

        public static readonly ITypeCandiateProvider TypeCandiateProvider;
        public static readonly TypeCandiateService TypeCandiateService;

        static TypeSearchService ()
        {
#if UNITY_2023_2_OR_NEWER
            TypeCandiateProvider = Unity_2023_2_OrNewer_TypeCandiateProvider.Instance;
#else
            TypeCandiateProvider = DefaultTypeCandiateProvider.Instance;
#endif

            TypeCandiateService = new TypeCandiateService(TypeCandiateProvider);
        }
    }
}