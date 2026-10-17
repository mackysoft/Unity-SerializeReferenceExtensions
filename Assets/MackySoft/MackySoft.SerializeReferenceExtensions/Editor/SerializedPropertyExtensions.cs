using System.Collections.Generic;
using UnityEditor;

namespace MackySoft.SerializeReferenceExtensions.Editor
{
    public static class SerializedPropertyExtensions
    {
        /// <summary> Enumerates visible child properties without moving the parent iterator. </summary>
        /// <param name="parent"> The parent whose iterator position is preserved. </param>
        /// <param name="depth"> The maximum descendant depth. Nonpositive values return no children. </param>
        /// <returns> Independent property copies that the caller must dispose. </returns>
        /// <remarks> Array parents return their elements without entering element descendants, regardless of positive depth. </remarks>
        public static IEnumerable<SerializedProperty> GetChildProperties (this SerializedProperty parent, int depth = 1)
        {
            if (depth <= 0)
            {
                yield break;
            }

            using (SerializedProperty cursor = parent.Copy())
            {
                if (cursor.isArray)
                {
                    for (int i = 0; i < cursor.arraySize; i++)
                    {
                        yield return cursor.GetArrayElementAtIndex(i);
                    }
                    yield break;
                }

                int depthOfParent = cursor.depth;
                long maximumDepth = (long)depthOfParent + depth;
                using (SerializedProperty end = cursor.GetEndProperty())
                {
                    // Skip descendants at the requested depth so cycles and closed branches remain bounded.
                    while (cursor.NextVisible(cursor.depth < maximumDepth) &&
                        !SerializedProperty.EqualContents(cursor, end) && cursor.depth > depthOfParent)
                    {
                        yield return cursor.Copy();
                    }
                }
            }
        }
    }
}
