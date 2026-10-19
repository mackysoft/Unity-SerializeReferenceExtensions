using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace MackySoft.SerializeReferenceExtensions.Editor
{
    internal static class UnityPropertyDrawerUtility
    {
        private const BindingFlags NativeMembers = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly Type UtilityType = typeof(EditorGUI).Assembly.GetType("UnityEditor.ScriptAttributeUtility", true);
        private static readonly Type HandlerType = typeof(EditorGUI).Assembly.GetType("UnityEditor.PropertyHandler", true);
        private static readonly MethodInfo GetHandler = RequireMethod(UtilityType, "GetHandler", typeof(SerializedProperty));
        private static readonly MethodInfo IncrementContext = RequireMethod(HandlerType, "IncrementNestingContext");
        private static readonly MethodInfo ApplyContext = RequireMethod(HandlerType, "ApplyNestingContext", typeof(int));
        private static readonly MethodInfo DisposeContext = RequireMethod(ApplyContext.ReturnType, nameof(IDisposable.Dispose));
        private static readonly FieldInfo NestingLevel = HandlerType.GetField("m_NestingLevel", NativeMembers)
            ?? throw new MissingFieldException(HandlerType.FullName, "m_NestingLevel");
        private static readonly PropertyInfo Drawer = HandlerType.GetProperty("propertyDrawer", NativeMembers)
            ?? throw new MissingMemberException(HandlerType.FullName, "propertyDrawer");

        private static readonly Func<SerializedProperty, bool> HasNextDrawer = BindHasNextDrawer();
        private static readonly Func<SerializedProperty, int> NextLevel = BindNextLevel();
        private static readonly Func<SerializedProperty, int, PropertyDrawer> DrawerAtLevel = BindDrawerAtLevel();
#if !UNITY_6000_0_OR_NEWER
        private static readonly Func<SerializedProperty, List<DecoratorDrawer>> Decorators = BindDecorators();
#endif
        private static readonly Action<PropertyField, VisualElement> PropagateParent = BindPropagateParent();

        internal static bool HasNextPropertyDrawer (SerializedProperty property)
        {
            return HasNextDrawer(property);
        }

        // Capture while Unity is invoking this selector's CreatePropertyGUI. Later refreshes
        // must enter that same native level, including selectors preceded by another drawer.
        internal static int CaptureNextNestingLevel (SerializedProperty property) => NextLevel(property);

        internal static PropertyDrawer GetPropertyDrawer (SerializedProperty property, int nestingLevel)
            => DrawerAtLevel(property, nestingLevel);

#if !UNITY_6000_0_OR_NEWER
        // PropertyHandler owns ordering and collection-element exclusions. Capture
        // its actual drawers for this view instead of rediscovering field attributes.
        internal static DecoratorDrawer[] GetDecoratorDrawers (SerializedProperty property)
            => Decorators(property)?.ToArray() ?? Array.Empty<DecoratorDrawer>();
#endif

        internal static void SetParentProperty (PropertyField parent, PropertyField child) => PropagateParent(parent, child);

        private static Func<SerializedProperty, bool> BindHasNextDrawer ()
        {
            PropertyInfo hasDrawer = HandlerType.GetProperty("hasPropertyDrawer", NativeMembers);
            if (hasDrawer == null)
            {
                throw new MissingMemberException(HandlerType.FullName, "hasPropertyDrawer");
            }

            // Unity's current nesting level determines the next drawer and keeps its context and lifetime native-owned.
            var property = Expression.Parameter(typeof(SerializedProperty), "property");
            return Expression.Lambda<Func<SerializedProperty, bool>>(
                Expression.Property(Expression.Call(GetHandler, property), hasDrawer), property).Compile();
        }

        private static Func<SerializedProperty, int> BindNextLevel ()
        {
            var property = Expression.Parameter(typeof(SerializedProperty), "property");
            return BindNativeCall<Func<SerializedProperty, int>>(new[] { property },
                handler => Expression.Call(handler, IncrementContext),
                handler => Expression.Field(handler, NestingLevel));
        }

        private static Func<SerializedProperty, int, PropertyDrawer> BindDrawerAtLevel ()
        {
            var property = Expression.Parameter(typeof(SerializedProperty), "property");
            var level = Expression.Parameter(typeof(int), "nestingLevel");
            return BindNativeCall<Func<SerializedProperty, int, PropertyDrawer>>(new[] { property, level },
                handler => Expression.Call(handler, ApplyContext, level),
                handler => Expression.Property(handler, Drawer));
        }

#if !UNITY_6000_0_OR_NEWER
        private static Func<SerializedProperty, List<DecoratorDrawer>> BindDecorators ()
        {
            FieldInfo decorators = HandlerType.GetField("m_DecoratorDrawers", NativeMembers)
                ?? throw new MissingFieldException(HandlerType.FullName, "m_DecoratorDrawers");
            var property = Expression.Parameter(typeof(SerializedProperty), "property");
            return Expression.Lambda<Func<SerializedProperty, List<DecoratorDrawer>>>(
                Expression.Field(Expression.Call(GetHandler, property), decorators), property).Compile();
        }
#endif

        private static T BindNativeCall<T> (ParameterExpression[] parameters,
            Func<Expression, Expression> enterContext, Func<Expression, Expression> operation) where T : Delegate
        {
            var handler = Expression.Variable(HandlerType, "handler");
            var context = Expression.Variable(ApplyContext.ReturnType, "context");
            return Expression.Lambda<T>(Expression.Block(new[] { handler, context },
                Expression.Assign(handler, Expression.Call(GetHandler, parameters[0])),
                Expression.Assign(context, enterContext(handler)),
                Expression.TryFinally(operation(handler), Expression.Call(context, DisposeContext))), parameters).Compile();
        }

        private static Action<PropertyField, VisualElement> BindPropagateParent ()
        {
            var parent = Expression.Parameter(typeof(PropertyField), "parent");
            var child = Expression.Parameter(typeof(VisualElement), "child");
            MethodInfo propagate = RequireMethod(typeof(PropertyField), "PropagateNestingLevel", typeof(VisualElement));
            return Expression.Lambda<Action<PropertyField, VisualElement>>(Expression.Call(parent, propagate, child), parent, child).Compile();
        }

        private static MethodInfo RequireMethod (Type type, string name, params Type[] parameters)
            => type.GetMethod(name, NativeMembers, null, parameters, null)
                ?? throw new MissingMethodException(type.FullName, name);
    }
}
