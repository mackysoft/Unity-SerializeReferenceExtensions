using System;
using UnityEngine;

namespace MackySoft.SerializeReferenceExtensions.Tests
{
    public sealed class DrawerContextTestAsset : ScriptableObject
    {
        [SerializeReference, SubclassSelector, DrawerContext("Primary selector", 7, 4)]
        public StatefulNode primary = new StatefulNode { amount = 101 };

        [SerializeReference, SubclassSelector, DrawerContext("Secondary selector", 13, 5)]
        public StatefulNode secondary = new StatefulNode { amount = 201 };

        [SerializeReference, DrawerContext("Native reference", 17, 4)]
        public StatefulNode native = new StatefulNode { amount = 301 };

        [SerializeReference, SubclassSelector]
        public ForwardingNode forwarding = new ForwardingNode();

        [Serializable]
        public sealed class StatefulNode
        {
            public int amount;
        }

        [Serializable]
        public sealed class ForwardingNode
        {
            public int number = 29;
        }
    }
}
