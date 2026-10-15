using System;
using System.Collections.Generic;
using UnityEngine;

namespace MackySoft.SerializeReferenceExtensions.Tests
{
    public sealed class SelectorTestAsset : ScriptableObject
    {
        [SerializeReference, SubclassSelector] public INode reference;
        [SerializeReference, SubclassSelector(UseToStringAsLabel = true)] public INode labelled;
        [SerializeReference, SubclassSelector] public INode[] array = new INode[2];
        [SerializeReference, SubclassSelector] public List<INode> list = new List<INode> { null, null };
        [SerializeReference, SubclassSelector(UseToStringAsLabel = true)] public INode[] labelledArray = new INode[2];
        [SerializeReference, SubclassSelector(UseToStringAsLabel = true)] public List<INode> labelledList = new List<INode> { null, null };
        public Parent parent = new Parent();
        public int unrelated = 81;

        public interface INode { }

        [Serializable]
        public sealed class Parent
        {
            [SerializeReference, SubclassSelector] public INode reference;
        }

        [Serializable]
        public sealed class Payload
        {
            public int value;
        }

        [Serializable, AddTypeMenu("First Node")]
        public sealed class FirstNode : INode
        {
            public int amount = 7;
            [SerializeField] private string m_Title = "first";
            public Payload payload = new Payload { value = 8 };
            public int[] numbers = { 9 };

            public string Title => m_Title;

            public FirstNode () { }

            public FirstNode (int value, string title)
            {
                amount = value;
                m_Title = title;
                payload.value = value + 1;
                numbers[0] = value + 2;
            }

            public override string ToString () => m_Title;
        }

        [Serializable, AddTypeMenu("Commands/Renamed Node")]
        public sealed class SecondNode : INode
        {
            public int amount = 11;
            [SerializeField] private string m_Title = "second";
            public Payload payload = new Payload { value = 12 };
            public int[] numbers = { 13 };

            public string Title => m_Title;

            public override string ToString () => m_Title;
        }

        [Serializable]
        public class DrawnNode : INode
        {
            public int amount;
        }

        [Serializable]
        public sealed class DerivedDrawnNode : DrawnNode { }
    }
}
