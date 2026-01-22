#if UNITY_2023_2_OR_NEWER
using System.Linq;
using MackySoft.SerializeReferenceExtensions.Editor;
using NUnit.Framework;
using UnityEngine;

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

        [Test]
        public void GenericClass_ParticleSystem_IsSupported ()
        {
            var set = TypeSearchService.TypeCandiateService.GetDisplayableTypes(typeof(IObjectHolder<ParticleSystem>)).ToHashSet();

            Assert.That(set, Does.Contain(typeof(ObjectHolder<ParticleSystem>)));
            Assert.That(set, Does.Contain(typeof(ParticleSystemHolder)));
        }

        [Test]
        public void GenericClass_GameObject_IsSupported ()
        {
            var set = TypeSearchService.TypeCandiateService.GetDisplayableTypes(typeof(IObjectHolder<GameObject>)).ToHashSet();

            Assert.That(set, Does.Contain(typeof(ObjectHolder<GameObject>)));
            Assert.That(set, !Does.Contain(typeof(ParticleSystemHolder)));
        }

        [Test]
        public void GenericClass_Integer_IsSupported ()
        {
            var set = TypeSearchService.TypeCandiateService.GetDisplayableTypes(typeof(IObjectHolder<int>)).ToHashSet();

            Assert.That(set, Does.Contain(typeof(ObjectHolder<int>)));
            Assert.That(set, !Does.Contain(typeof(ParticleSystemHolder)));
        }
    }
}
#endif