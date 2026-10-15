using System;
using MackySoft.SerializeReferenceExtensions.Editor;
using NUnit.Framework;

namespace MackySoft.SerializeReferenceExtensions.Tests
{
    public sealed class TypeCandidateContractTests
    {
        [Test]
        public void AbstractBase_OffersConcreteDescendantsAndExcludesUnrelatedTypes ()
        {
            var candidates = TypeSearchService.TypeCandiateService.GetDisplayableTypes(typeof(AbstractCommand));

            Assert.That(candidates, Does.Contain(typeof(ConcreteCommand)));
            Assert.That(candidates, Does.Contain(typeof(IndirectCommand)));
            Assert.That(candidates, !Does.Contain(typeof(AbstractCommand)));
            Assert.That(candidates, !Does.Contain(typeof(HiddenCommand)));
            Assert.That(candidates, !Does.Contain(typeof(PublicSerializableClass)));
        }

#if UNITY_2023_2_OR_NEWER
        [Test]
        public void ConstructedAbstractGenericBase_OffersOnlyMatchingNongenericDescendants ()
        {
            var candidates = TypeSearchService.TypeCandiateService.GetDisplayableTypes(typeof(GenericCommand<int>));

            Assert.That(candidates, Does.Contain(typeof(IntCommand)));
            Assert.That(candidates, Does.Contain(typeof(IndirectIntCommand)));
            Assert.That(candidates, !Does.Contain(typeof(StringCommand)));
            Assert.That(candidates, !Does.Contain(typeof(GenericCommand<int>)));
            Assert.That(candidates, !Does.Contain(typeof(OpenCommand<>)));
            Assert.That(candidates, !Does.Contain(typeof(OpenCommand<int>)));
        }

        [Test]
        public void InheritedGenericInterface_IsDiscoveredThroughItsNongenericParent ()
        {
            var candidates = TypeSearchService.TypeCandiateService.GetDisplayableTypes(typeof(IInvariant<int>));

            Assert.That(candidates, Does.Contain(typeof(InheritedInterfaceCommand)));
            Assert.That(candidates, !Does.Contain(typeof(StringInvariantCommand)));
        }

        [Test]
        public void CombinedVariance_RequiresBothInputAndOutputToBeCompatible ()
        {
            var candidates = TypeSearchService.TypeCandiateService.GetDisplayableTypes(typeof(IConversion<INetworkActor, IActor>));

            Assert.That(candidates, Does.Contain(typeof(CompatibleConversion)));
            Assert.That(candidates, !Does.Contain(typeof(NarrowInputConversion)));
            Assert.That(candidates, !Does.Contain(typeof(WideOutputConversion)));
        }

        [Serializable] public abstract class GenericCommand<T> { }
        [Serializable] public sealed class IntCommand : GenericCommand<int> { }
        [Serializable] public abstract class IntBridge : GenericCommand<int> { }
        [Serializable] public sealed class IndirectIntCommand : IntBridge { }
        [Serializable] public sealed class StringCommand : GenericCommand<string> { }
        [Serializable] public sealed class OpenCommand<T> : GenericCommand<T> { }
        public interface IIntCommand : IInvariant<int> { }
        [Serializable] public sealed class InheritedInterfaceCommand : IIntCommand { }
        [Serializable] public sealed class StringInvariantCommand : IInvariant<string> { }
        public interface IConversion<in TInput, out TOutput> { }
        [Serializable] public sealed class CompatibleConversion : IConversion<IActor, INetworkActor> { }
        [Serializable] public sealed class NarrowInputConversion : IConversion<IDerivedNetworkActor, INetworkActor> { }
        [Serializable] public sealed class WideOutputConversion : IConversion<IActor, object> { }
#endif

        [Serializable] public abstract class AbstractCommand { }
        [Serializable] public sealed class ConcreteCommand : AbstractCommand { }
        [Serializable] public abstract class IntermediateCommand : AbstractCommand { }
        [Serializable] public sealed class IndirectCommand : IntermediateCommand { }
        [Serializable, HideInTypeMenu] public sealed class HiddenCommand : AbstractCommand { }
    }
}
