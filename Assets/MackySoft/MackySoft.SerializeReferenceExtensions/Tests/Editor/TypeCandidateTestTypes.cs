using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace MackySoft.SerializeReferenceExtensions.Tests
{

    public interface ITestBase { }

    [Serializable]
    public sealed class PublicSerializableClass : ITestBase { }

    [Serializable]
    internal sealed class InternalSerializableClass : ITestBase { }

    [Serializable]
    public abstract class AbstractSerializableClass : ITestBase { }

    public sealed class NonSerializableClass : ITestBase { }

    [Serializable, HideInTypeMenu]
    public sealed class HiddenSerializableClass : ITestBase { }

    [Serializable]
    public sealed class UnityObjectDerived : ScriptableObject, ITestBase { }

    public sealed class Outer
    {
        [Serializable]
        public sealed class NestedPublicSerializableClass : ITestBase { }

        [Serializable]
        private sealed class NestedPrivateSerializableClass : ITestBase { }

        public static Type NestedPrivateType => typeof(Outer).GetNestedType(nameof(NestedPrivateSerializableClass), BindingFlags.NonPublic);
    }

    [Serializable]
    public sealed class GenericCandidate<T> : ITestBase { }

    [Serializable]
    public struct SerializableStruct : ITestBase { }

    [Serializable]
    public class ConcreteBaseType { }

    [Serializable]
    public sealed class ConcreteDerivedType : ConcreteBaseType { }

    [Serializable]
    internal sealed class ConcreteInternalDerivedType : ConcreteBaseType { }

    // ------------------------
    // Generic variance test types (2023.2+)
    // ------------------------
    public interface IActor { }
    public interface INetworkActor : IActor { }
    public interface IDerivedNetworkActor : INetworkActor { }

    public interface IContravariant<in T> { }
    public interface ICovariant<out T> { T Create (); }
    public interface IInvariant<T> { }

    [Serializable]
    public sealed class Contravariant_Actor : IContravariant<IActor> { }

    [Serializable]
    public sealed class Contravariant_NetworkActor : IContravariant<INetworkActor> { }

    [Serializable]
    public sealed class Contravariant_DerivedNetworkActor : IContravariant<IDerivedNetworkActor> { }

    [Serializable]
    public sealed class Covariant_Actor : ICovariant<IActor> {
        public IActor Create () => null;
    }

    [Serializable]
    public sealed class Covariant_NetworkActor : ICovariant<INetworkActor> {
        public INetworkActor Create () => null;
    }
    
    [Serializable]
    public sealed class Covariant_Object : ICovariant<object> {
        public object Create () => null;
    }

    [Serializable]
    public sealed class Invariant_Actor : IInvariant<IActor> { }

    [Serializable]
    public sealed class Invariant_NetworkActor : IInvariant<INetworkActor> { }
}
