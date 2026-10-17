using UnityEngine;

namespace MackySoft.SerializeReferenceExtensions.Tests
{
    public class DeferredReferenceTestAssetBase : ScriptableObject
    {
        [SerializeField] private DeferredReferenceTestAsset.Parent inherited = new DeferredReferenceTestAsset.Parent
        {
            reference = new DeferredReferenceTestAsset.Alpha(19)
        };

        public DeferredReferenceTestAsset.INode InheritedReference => inherited.reference;
    }
}
