#if UNITY_2023_2_OR_NEWER
using System;
using MackySoft.SerializeReferenceExtensions.Editor;
using NUnit.Framework;

namespace MackySoft.SerializeReferenceExtensions.Tests
{
    public sealed class GenericValueTypeVarianceReproductionTests
    {
        // Known product defect: boxing does not make a variant interface conversion legal.
        // Run explicitly until the product's compatibility policy is corrected.
        [Test, Explicit("Known product defect: value-type arguments must remain invariant.")]
        public void Covariance_DoesNotBoxValueTypeArguments ()
        {
            var candidates = TypeSearchService.TypeCandiateService.GetDisplayableTypes(typeof(ICovariant<object>));

            Assert.That(candidates, !Does.Contain(typeof(IntProducer)));
        }

        [Test, Explicit("Known product defect: value-type arguments must remain invariant.")]
        public void Contravariance_DoesNotBoxValueTypeArguments ()
        {
            var candidates = TypeSearchService.TypeCandiateService.GetDisplayableTypes(typeof(IContravariant<int>));

            Assert.That(candidates, !Does.Contain(typeof(ObjectConsumer)));
        }

        [Serializable]
        public sealed class IntProducer : ICovariant<int>
        {
            public int Create () => 0;
        }

        [Serializable]
        public sealed class ObjectConsumer : IContravariant<object> { }
    }
}
#endif
