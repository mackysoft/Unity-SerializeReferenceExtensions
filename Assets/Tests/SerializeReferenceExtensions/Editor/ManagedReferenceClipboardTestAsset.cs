using System;
using System.Collections.Generic;
using UnityEngine;

namespace MackySoft.SerializeReferenceExtensions.Tests
{
    public sealed class ManagedReferenceClipboardTestAsset : ScriptableObject
    {
        [SerializeReference] public INode reference;
        [SerializeReference] public IAlternate alternate;
        [SerializeField] public Parent parent = new Parent();
        [SerializeReference] public List<INode> elements = new List<INode>();
        [SerializeField] public int unrelated;

        public interface INode { }
        public interface IAlternate { }

        [Serializable]
        public sealed class Parent
        {
            [SerializeReference] public INode reference;
        }

        [Serializable]
        public sealed class Payload
        {
            public int value;
        }

        [Serializable]
        public sealed class SourceNode : INode
        {
            public int shared;
            [SerializeField] private string m_Label;
            public Payload payload;
            public int[] numbers;

            public string Label => m_Label;

            public SourceNode (int value, string label)
            {
                shared = value;
                m_Label = label;
                payload = new Payload { value = value + 1 };
                numbers = new[] { value + 2 };
            }
        }

        [Serializable]
        public sealed class DestinationNode : INode
        {
            public int shared;
            public int destinationOnly;
        }

        [Serializable]
        public sealed class AlternateNode : IAlternate
        {
            public int shared;
            public int destinationOnly;
        }

        [Serializable, HideInTypeMenu]
        public sealed class HiddenNode : INode
        {
            public int value;

            private HiddenNode (int initialValue)
            {
                value = initialValue;
            }

            public static HiddenNode Create (int value)
            {
                return new HiddenNode(value);
            }
        }
    }
}
