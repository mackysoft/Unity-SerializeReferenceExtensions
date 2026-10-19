using System;
using UnityEngine;

namespace MackySoft.SerializeReferenceExtensions.Tests
{
    public sealed class SubclassSelectorPrefabTestComponent : MonoBehaviour
    {
        [SerializeReference, SubclassSelector] public IValue reference = new FirstValue { number = 7 };

        public interface IValue { }

        [Serializable]
        [AddTypeMenu("First type")]
        public sealed class FirstValue : IValue
        {
            public int number;
        }

        [Serializable]
        [AddTypeMenu("Second type")]
        public sealed class SecondValue : IValue
        {
            public int number;
        }

        [Serializable]
        public sealed class TrackedValue : IValue
        {
            public int number;
        }

        [Serializable]
        public sealed class CompositeValue : IValue
        {
            public int nativeValue = 101;
            [SerializeReference] public IValue child = new TrackedValue { number = 7 };
        }
    }
}
