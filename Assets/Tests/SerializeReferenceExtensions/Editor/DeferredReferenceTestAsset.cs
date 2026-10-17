using System;
using System.Collections.Generic;
using UnityEngine;

namespace MackySoft.SerializeReferenceExtensions.Tests
{
    public sealed class DeferredReferenceTestAsset : DeferredReferenceTestAssetBase
    {
        [SerializeReference, SubclassSelector] public INode reference = new Alpha(17);
        [SerializeReference, SubclassSelector] public List<INode> elements = new List<INode> { null, new Alpha(23), null, new Beta(47) };
        public Parent parent = new Parent { reference = new Alpha(17) };
        [SerializeReference] public Parent managedParent = new Parent { reference = new Alpha(37) };
        public Parent[] inlineItems = { new Parent { reference = new Alpha(19) }, new Parent { reference = new Alpha(29) } };
        public Slot[] slots = { new Slot { marker = 1, reference = new Alpha(41) }, new Slot { marker = 2, reference = new Alpha(51) } };
        public int unrelated = 81;

        public interface INode { }

        [Serializable]
        public sealed class Payload
        {
            public int value;
        }

        [Serializable, AddTypeMenu("Deferred Alpha")]
        public sealed class Alpha : INode
        {
            public int amount = 7;
            public Payload payload = new Payload { value = 8 };

            public Alpha () { }

            public Alpha (int value)
            {
                amount = value;
                payload.value = value + 1;
            }
        }

        [Serializable, AddTypeMenu("Deferred Beta")]
        public sealed class Beta : INode
        {
            public int amount = 11;
            public Payload payload = new Payload { value = 12 };

            public Beta () { }

            public Beta (int value)
            {
                amount = value;
                payload.value = value + 1;
            }
        }

        [Serializable]
        public sealed class Parent
        {
            [SerializeReference, SubclassSelector] public INode reference;
        }

        [Serializable]
        public struct Slot
        {
            public int marker;
            [SerializeReference, SubclassSelector] public INode reference;

            public override bool Equals (object obj) => true;
            public override int GetHashCode () => 0;
        }
    }
}
