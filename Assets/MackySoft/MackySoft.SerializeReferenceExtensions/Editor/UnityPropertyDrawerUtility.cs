using System;
using System.Linq.Expressions;
using System.Reflection;
using UnityEditor;

namespace MackySoft.SerializeReferenceExtensions.Editor
{
    internal static class UnityPropertyDrawerUtility
    {
        private static readonly Func<SerializedProperty, bool> HasNextDrawer = BindHasNextDrawer();

        internal static bool HasNextPropertyDrawer (SerializedProperty property)
        {
            return HasNextDrawer(property);
        }

        private static Func<SerializedProperty, bool> BindHasNextDrawer ()
        {
            Assembly editorAssembly = typeof(EditorGUI).Assembly;
            Type utilityType = editorAssembly.GetType("UnityEditor.ScriptAttributeUtility", true);
            Type handlerType = editorAssembly.GetType("UnityEditor.PropertyHandler", true);
            MethodInfo getHandler = utilityType.GetMethod("GetHandler", BindingFlags.Static | BindingFlags.NonPublic,
                null, new[] { typeof(SerializedProperty) }, null);
            PropertyInfo hasDrawer = handlerType.GetProperty("hasPropertyDrawer", BindingFlags.Instance | BindingFlags.Public);
            if (getHandler == null)
            {
                throw new MissingMethodException(utilityType.FullName, "GetHandler");
            }
            if (hasDrawer == null)
            {
                throw new MissingMemberException(handlerType.FullName, "hasPropertyDrawer");
            }

            // Unity's current nesting level determines the next drawer and keeps its context and lifetime native-owned.
            var property = Expression.Parameter(typeof(SerializedProperty), "property");
            return Expression.Lambda<Func<SerializedProperty, bool>>(
                Expression.Property(Expression.Call(getHandler, property), hasDrawer), property).Compile();
        }
    }
}
