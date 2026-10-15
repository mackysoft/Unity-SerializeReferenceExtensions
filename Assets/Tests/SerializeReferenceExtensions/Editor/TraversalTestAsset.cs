using System;
using System.Collections.Generic;
using UnityEngine;

namespace MackySoft.SerializeReferenceExtensions.Tests
{
    public sealed class TraversalTestAsset : ScriptableObject
    {
        [SerializeReference, SubclassSelector] public object reference;
        public Branch[] array = { new Branch(), new Branch() };
        public List<Branch> list = new List<Branch> { new Branch(), new Branch() };

        [Serializable]
        public sealed class Cycle
        {
            public int visibleValue = 17;
            [SerializeReference, SubclassSelector] public Cycle next;
            public int tailValue = 71;
        }

        [Serializable]
        public sealed class NestedRoot
        {
            public int visibleValue = 17;
            public Branch branch = new Branch();
            [HideInInspector] public int hiddenValue;
            public int tailValue = 71;
        }

        [Serializable]
        public sealed class Branch
        {
            public int value = 23;
            public Leaf leaf = new Leaf();
        }

        [Serializable]
        public sealed class Leaf
        {
            public int value = 29;
        }

        [Serializable]
        public sealed class CollectionRoot
        {
            public int visibleValue = 17;
            public ResizableCollection collection = new ResizableCollection();
            public int tailValue = 71;
        }

        [Serializable]
        public sealed class ResizableCollection
        {
            public int[] values = { 31 };
        }

        [Serializable]
        public sealed class ClosedBranches
        {
            public int visibleValue = 17;
            [SerializeReference, SubclassSelector] public Cycle[] entries;
            public int tailValue = 71;
        }
    }
}
