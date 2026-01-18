#if UNITY_2023_2_OR_NEWER
using System.Linq;
using MackySoft.SerializeReferenceExtensions.Editor;
using NUnit.Framework;

namespace MackySoft.SerializeReferenceExtensions.Tests
{
    [TestFixture]
    public sealed class TypeCandidateService_GenericVarianceTests
    {
        [Test]
        public void Contravariance_IsSupported ()
        {
            // baseType: IContravariance<INetworkActor>
            var set = TypeSearchService.TypeCandiateService.GetDisplayableTypes(typeof(IContravariant<INetworkActor>)).ToHashSet();

            Assert.That(set, Does.Contain(typeof(Contravariant_NetworkActor)), "Exact match should be included.");
            Assert.That(set, Does.Contain(typeof(Contravariant_Actor)), "Contravariant match should be included.");
            Assert.That(set, !Does.Contain(typeof(Contravariant_DerivedNetworkActor)), "Narrower type argument should be excluded.");
        }

        [Test]
        public void Covariance_IsSupported ()
        {
            // baseType: ICovariant<IActor>
            var set = TypeSearchService.TypeCandiateService.GetDisplayableTypes(typeof(ICovariant<IActor>)).ToHashSet();

            Assert.That(set, Does.Contain(typeof(Covariant_Actor)), "Exact match should be included.");
            Assert.That(set, Does.Contain(typeof(Covariant_NetworkActor)), "Covariant match should be included.");
            Assert.That(set, !Does.Contain(typeof(Covariant_Object)), "Wider type argument should be excluded.");
        }

        [Test]
        public void Invariance_RemainsStrict ()
        {
            // baseType: IInvariant<IActor>
            var set = TypeSearchService.TypeCandiateService.GetDisplayableTypes(typeof(IInvariant<IActor>)).ToHashSet();

            Assert.That(set, Does.Contain(typeof(Invariant_Actor)));
            Assert.That(set, !Does.Contain(typeof(Invariant_NetworkActor)));
        }
    }
}
#endif