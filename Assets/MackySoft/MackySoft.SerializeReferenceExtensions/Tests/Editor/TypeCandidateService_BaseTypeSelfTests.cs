using System.Linq;
using MackySoft.SerializeReferenceExtensions.Editor;
using NUnit.Framework;

namespace MackySoft.SerializeReferenceExtensions.Tests
{
    [TestFixture]
    public sealed class TypeCandidateService_BaseTypeSelfTests
    {
        [Test]
        public void ConcreteBaseType_IsIncluded ()
        {
            var set = TypeSearchService.TypeCandiateService.GetDisplayableTypes(typeof(ConcreteBaseType)).ToHashSet();

            Assert.That(set, Does.Contain(typeof(ConcreteBaseType)));
            Assert.That(set, Does.Contain(typeof(ConcreteDerivedType)));
            Assert.That(set, !Does.Contain(typeof(ConcreteInternalDerivedType)));
        }
    }
}
