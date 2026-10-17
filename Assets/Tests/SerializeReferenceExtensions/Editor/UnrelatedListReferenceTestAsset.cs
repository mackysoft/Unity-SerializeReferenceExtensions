using System;
using System.Collections;
using UnityEngine;

namespace MackySoft.SerializeReferenceExtensions.Tests
{
    public sealed class UnrelatedListReferenceTestAsset : ScriptableObject, IList
    {
        [SerializeReference] public DeferredReferenceTestAsset.INode reference = new DeferredReferenceTestAsset.Alpha(17);
        public Parent parent = new Parent();

        int IList.Add (object value) => ((IList)parent).Add(value);
        void IList.Clear () => ((IList)parent).Clear();
        bool IList.Contains (object value) => ((IList)parent).Contains(value);
        int IList.IndexOf (object value) => ((IList)parent).IndexOf(value);
        void IList.Insert (int index, object value) => ((IList)parent).Insert(index, value);
        void IList.Remove (object value) => ((IList)parent).Remove(value);
        void IList.RemoveAt (int index) => ((IList)parent).RemoveAt(index);
        bool IList.IsFixedSize => ((IList)parent).IsFixedSize;
        bool IList.IsReadOnly => ((IList)parent).IsReadOnly;
        object IList.this[int index] { get => ((IList)parent)[index]; set => ((IList)parent)[index] = value; }
        int ICollection.Count => ((ICollection)parent).Count;
        bool ICollection.IsSynchronized => ((ICollection)parent).IsSynchronized;
        object ICollection.SyncRoot => ((ICollection)parent).SyncRoot;
        void ICollection.CopyTo (Array array, int index) => ((ICollection)parent).CopyTo(array, index);
        IEnumerator IEnumerable.GetEnumerator () => ((IEnumerable)parent).GetEnumerator();

        [Serializable]
        public sealed class Parent : IList
        {
            [SerializeReference] public DeferredReferenceTestAsset.INode reference = new DeferredReferenceTestAsset.Alpha(23);

            int IList.Add (object value) => throw new InvalidOperationException("Unrelated list API is unavailable during Inspector edits.");
            void IList.Clear () => throw new InvalidOperationException("Unrelated list API is unavailable during Inspector edits.");
            bool IList.Contains (object value) => throw new InvalidOperationException("Unrelated list API is unavailable during Inspector edits.");
            int IList.IndexOf (object value) => throw new InvalidOperationException("Unrelated list API is unavailable during Inspector edits.");
            void IList.Insert (int index, object value) => throw new InvalidOperationException("Unrelated list API is unavailable during Inspector edits.");
            void IList.Remove (object value) => throw new InvalidOperationException("Unrelated list API is unavailable during Inspector edits.");
            void IList.RemoveAt (int index) => throw new InvalidOperationException("Unrelated list API is unavailable during Inspector edits.");
            bool IList.IsFixedSize => throw new InvalidOperationException("Unrelated list API is unavailable during Inspector edits.");
            bool IList.IsReadOnly => throw new InvalidOperationException("Unrelated list API is unavailable during Inspector edits.");
            object IList.this[int index]
            {
                get => throw new InvalidOperationException("Unrelated list API is unavailable during Inspector edits.");
                set => throw new InvalidOperationException("Unrelated list API is unavailable during Inspector edits.");
            }
            int ICollection.Count => throw new InvalidOperationException("Unrelated list API is unavailable during Inspector edits.");
            bool ICollection.IsSynchronized => throw new InvalidOperationException("Unrelated list API is unavailable during Inspector edits.");
            object ICollection.SyncRoot => throw new InvalidOperationException("Unrelated list API is unavailable during Inspector edits.");
            void ICollection.CopyTo (Array array, int index) => throw new InvalidOperationException("Unrelated list API is unavailable during Inspector edits.");
            IEnumerator IEnumerable.GetEnumerator () => throw new InvalidOperationException("Unrelated list API is unavailable during Inspector edits.");
        }
    }
}
