#if SERIALIZE_REFERENCE_EXTENSIONS_UNITY_2022_3_MR_TRACKER || UNITY_2023_2_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace MackySoft.SerializeReferenceExtensions.Editor
{
    // Unity rebinds custom managed-reference drawers on every content/mixed-value
    // revision. Preserve the native tracker, but restrict its rebuild to changes in UI shape.
    internal sealed class NativeManagedReferenceTracking : IDisposable
    {
        private static readonly ConditionalWeakTable<object, Registration> Registrations = new ConditionalWeakTable<object, Registration>();
        private static readonly ConditionalWeakTable<VisualElement, OwnerEpoch> OwnerEpochs = new ConditionalWeakTable<VisualElement, OwnerEpoch>();

        private readonly VisualElement root;
        private readonly IPanel panel;
        private readonly Delegate hierarchyChanged;
        private readonly Dictionary<VisualElement, IDisposable> subscriptions = new Dictionary<VisualElement, IDisposable>();
        private readonly HashSet<VisualElement> ancestors = new HashSet<VisualElement>();
        private readonly HashSet<Registration> registrations = new HashSet<Registration>();
        private bool disposed;

        internal NativeManagedReferenceTracking (VisualElement root)
        {
            this.root = root;
            panel = root.panel;
            hierarchyChanged = NativeAccess.SubscribeHierarchy(panel, this);
            try
            {
                SubscribeTree(root);
                SynchronizeAncestors();
                RepairTree(root);
                RepairAncestors(root.parent);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Dispose ()
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            NativeAccess.UnsubscribeHierarchy(panel, hierarchyChanged);
            foreach (IDisposable subscription in subscriptions.Values)
            {
                subscription.Dispose();
            }
            subscriptions.Clear();
            ancestors.Clear();
            foreach (Registration registration in registrations)
            {
                registration.Release(this);
            }
            registrations.Clear();
        }

        private static bool IsWithin (VisualElement element, VisualElement ancestor)
        {
            for (; element != null; element = element.parent)
            {
                if (element == ancestor)
                {
                    return true;
                }
            }
            return false;
        }

        private void Subscribe (VisualElement element)
        {
            if (!subscriptions.ContainsKey(element))
            {
                subscriptions.Add(element, NativeAccess.SubscribeBinding(element, this));
            }
        }

        private void SubscribeTree (VisualElement element)
        {
            Subscribe(element);
            for (int i = 0; i < element.hierarchy.childCount; i++)
            {
                SubscribeTree(element.hierarchy[i]);
            }
        }

        private void RemoveTree (VisualElement element)
        {
            if (subscriptions.TryGetValue(element, out IDisposable subscription))
            {
                subscription.Dispose();
                subscriptions.Remove(element);
            }
            for (int i = 0; i < element.hierarchy.childCount; i++)
            {
                RemoveTree(element.hierarchy[i]);
            }
        }

        private void SynchronizeAncestors ()
        {
            var current = new HashSet<VisualElement>();
            for (VisualElement ancestor = root.parent; ancestor != null; ancestor = ancestor.parent)
            {
                current.Add(ancestor);
            }
            foreach (VisualElement ancestor in ancestors)
            {
                if (!current.Contains(ancestor) && !IsWithin(ancestor, root))
                {
                    subscriptions[ancestor].Dispose();
                    subscriptions.Remove(ancestor);
                }
            }
            ancestors.Clear();
            foreach (VisualElement ancestor in current)
            {
                Subscribe(ancestor);
                ancestors.Add(ancestor);
            }
            ReleaseUnownedRegistrations();
        }

        private void ReleaseUnownedRegistrations ()
        {
            var removed = new List<Registration>();
            foreach (Registration registration in registrations)
            {
                if (!Owns(registration.Field, registration.Owner) || !registration.IsCurrent())
                {
                    registration.Release(this);
                    removed.Add(registration);
                }
            }
            foreach (Registration registration in removed)
            {
                registrations.Remove(registration);
            }
        }

        private void OnHierarchyChanged<TChange> (VisualElement element, TChange change)
        {
            if (disposed)
            {
                return;
            }
            if (Equals(change, NativeAccess.RemoveHierarchyChange))
            {
                // Remove precedes the parent change. Reordering uses the same Remove/Add
                // pair, so panel Detach remains the authority for this scope's disposal.
                if (!IsWithin(root, element) && IsWithin(element, root))
                {
                    RemoveTree(element);
                    var removed = new List<Registration>();
                    foreach (Registration registration in registrations)
                    {
                        if (IsWithin(registration.Owner, element))
                        {
                            registration.Release(this);
                            removed.Add(registration);
                        }
                    }
                    foreach (Registration registration in removed)
                    {
                        registrations.Remove(registration);
                    }
                }
            }
            else if (Equals(change, NativeAccess.AddHierarchyChange)
                && (IsWithin(element, root) || IsWithin(root, element)))
            {
                // Same-panel reparenting must replace the old ancestor ownership before
                // BindTree can register or dispatch against the new custom-drawer chain.
                SynchronizeAncestors();
                VisualElement added = IsWithin(element, root) ? element : root;
                SubscribeTree(added);
                RepairTree(added);
                RepairAncestors(root.parent);
            }
        }

        private void OnObjectBind (VisualElement element)
        {
            if (!disposed)
            {
                SynchronizeAncestors();
                // ObjectBind is dispatched at the target before Unity can delay child binding.
                // The custom root has been added and its extra native tracker is now registered.
                RepairAncestors(element);
            }
        }

        private void RepairTree (VisualElement element)
        {
            if (element is PropertyField field)
            {
                Repair(field);
            }
            for (int i = 0; i < element.hierarchy.childCount; i++)
            {
                RepairTree(element.hierarchy[i]);
            }
        }

        private void RepairAncestors (VisualElement element)
        {
            for (; element != null; element = element.parent)
            {
                if (element is PropertyField field)
                {
                    Repair(field);
                }
            }
        }

        private bool Owns (PropertyField field, VisualElement owner)
            => !disposed && root.panel == panel && owner.panel == panel && IsWithin(owner, field)
                && (IsWithin(field, root) || IsWithin(root, owner));

        private void Repair (PropertyField field)
        {
            VisualElement owner = NativeAccess.GetTrackedRoot(field);
            if (owner == null || !Owns(field, owner))
            {
                return;
            }
            SerializedProperty property = NativeAccess.GetProperty(field);
            if (property == null || property.propertyType != SerializedPropertyType.ManagedReference)
            {
                return;
            }
            object context = NativeAccess.GetContext(owner);
            if (context == null || !ReferenceEquals(NativeAccess.GetSource(context), property.serializedObject))
            {
                return;
            }

            foreach (object value in NativeAccess.GetCallbackSlots(owner, context, property))
            {
                if (!Registrations.TryGetValue(value, out Registration registration) || !registration.TryInstall())
                {
                    Action<SerializedProperty> callback = registration != null
                        ? registration.GetNativeCallback(field, owner, context, property.serializedObject, property.propertyPath)
                        : NativeAccess.GetCallback(value);
                    if (!NativeAccess.IsNativeCallback(callback, field))
                    {
                        continue;
                    }
                    registration = new Registration(value, owner, field, context, property.serializedObject, property.propertyPath, callback);
                    Registrations.Remove(value);
                    Registrations.Add(value, registration);
                }
                if (registrations.Add(registration))
                {
                    registration.Retain(this);
                }
            }
        }

        // This weak owner metadata outlives individual scopes so a queued callback can
        // distinguish a reattached element from the attachment that produced it. The
        // marker and its callback retain no registration, SerializedObject, scope or owner.
        private sealed class OwnerEpoch
        {
            internal ulong Value { get; private set; }

            private OwnerEpoch (VisualElement owner)
                => owner.RegisterCallback<DetachFromPanelEvent>(OnDetach, TrickleDown.TrickleDown);

            internal static OwnerEpoch Create (VisualElement owner) => new OwnerEpoch(owner);

            private void OnDetach (DetachFromPanelEvent evt)
            {
                if (evt.target == evt.currentTarget)
                {
                    Value++;
                }
            }
        }

        private sealed class Registration
        {
            private readonly object value;
            private readonly PropertyField field;
            private readonly object context;
            private readonly SerializedObject source;
            private readonly string path;
            private readonly IPanel panel;
            private readonly OwnerEpoch ownerEpoch;
            private readonly ulong attachment;
            private readonly Action<SerializedProperty> original;
            private readonly Action<SerializedProperty> callback;
            private readonly HashSet<NativeManagedReferenceTracking> scopes = new HashSet<NativeManagedReferenceTracking>();
            private Shape shape;

            internal VisualElement Owner { get; }
            internal PropertyField Field => field;
            private bool IsInstalled => NativeAccess.GetCallback(value) == callback;

            internal Registration (object value, VisualElement owner, PropertyField field, object context, SerializedObject source,
                string path, Action<SerializedProperty> original)
            {
                this.value = value;
                Owner = owner;
                this.field = field;
                this.context = context;
                this.source = source;
                this.path = path;
                panel = owner.panel;
                ownerEpoch = OwnerEpochs.GetValue(owner, OwnerEpoch.Create);
                attachment = ownerEpoch.Value;
                this.original = original;
                shape = Shape.Read(source, path);
                callback = OnChange;
                NativeAccess.SetCallback(value, callback);
            }

            internal bool IsCurrent ()
            {
                if (ownerEpoch.Value != attachment || Owner.panel != panel || !IsWithin(Owner, field)
                    || NativeAccess.GetTrackedRoot(field) != Owner)
                {
                    return false;
                }
                SerializedProperty property = NativeAccess.GetProperty(field);
                if (property == null || property.propertyType != SerializedPropertyType.ManagedReference
                    || property.propertyPath != path || !ReferenceEquals(property.serializedObject, source)
                    || !ReferenceEquals(NativeAccess.GetContext(Owner), context)
                    || !ReferenceEquals(NativeAccess.GetSource(context), source)
                    || !NativeAccess.Contains(Owner, context, property, value))
                {
                    return false;
                }
                Action<SerializedProperty> installed = NativeAccess.GetCallback(value);
                return installed == callback || installed == original;
            }

            internal bool TryInstall ()
            {
                if (!IsCurrent())
                {
                    return false;
                }
                // A reorder can release and reacquire this exact native registration before
                // its queued callback runs. Keep its shape history and delegate unchanged.
                if (!IsInstalled)
                {
                    NativeAccess.SetCallback(value, callback);
                }
                return true;
            }

            internal Action<SerializedProperty> GetNativeCallback (PropertyField currentField, VisualElement currentOwner,
                object currentContext, SerializedObject currentSource, string currentPath)
            {
                Action<SerializedProperty> installed = NativeAccess.GetCallback(value);
                return field == currentField && Owner == currentOwner && ReferenceEquals(context, currentContext)
                    && ReferenceEquals(source, currentSource) && path == currentPath
                    && (installed == callback || installed == original) ? original : installed;
            }

            internal void Retain (NativeManagedReferenceTracking scope) => scopes.Add(scope);

            internal void Release (NativeManagedReferenceTracking scope)
            {
                scopes.Remove(scope);
                if (scopes.Count == 0 && IsInstalled && IsCurrent())
                {
                    // Restore a still-live native owner. A detached attachment keeps the
                    // guard in its old closure so a queued native wrapper cannot revive it.
                    NativeAccess.SetCallback(value, original);
                }
            }

            private void OnChange (SerializedProperty property)
            {
                if (!IsCurrent())
                {
                    return;
                }
                bool active = false;
                foreach (NativeManagedReferenceTracking scope in scopes)
                {
                    if (scope.Owns(field, Owner))
                    {
                        active = true;
                        break;
                    }
                }
                if (!active || !NativeAccess.IsValid(property)
                    || property.propertyPath != path || !ReferenceEquals(property.serializedObject, source))
                {
                    original(property);
                    return;
                }

                Shape current = Shape.Read(source, path);
                if (!shape.Matches(current))
                {
                    shape = current;
                    original(property);
                }
            }
        }

        private sealed class Shape
        {
            private readonly List<Entry> entries = new List<Entry>();

            private struct Entry : IEquatable<Entry>
            {
                internal UnityEngine.Object Target;
                internal string Path;
                internal SerializedPropertyType Kind;
                internal string Type;
                internal long Id;
                internal int Size;

                public bool Equals (Entry other)
                    => Target == other.Target && Path == other.Path && Kind == other.Kind
                        && Type == other.Type && Id == other.Id && Size == other.Size;
            }

            internal static Shape Read (SerializedObject source, string path)
            {
                var shape = new Shape();
                // The binding context owns its cache. Fresh per-target objects keep mixed values
                // and shape reads independent without updating or invalidating that cache.
                foreach (UnityEngine.Object target in source.targetObjects)
                {
                    shape.entries.Add(new Entry { Target = target, Path = path, Size = -1 });
                    if (target == null)
                    {
                        continue;
                    }
                    using (var individual = new SerializedObject(target, source.context))
                    using (SerializedProperty property = individual.FindProperty(path))
                    {
                        if (property == null)
                        {
                            continue;
                        }
                        var visited = new HashSet<long>();
                        using (SerializedProperty end = property.GetEndProperty(true))
                        {
                            bool enterChildren;
                            do
                            {
                                SerializedPropertyType kind = property.propertyType;
                                bool reference = kind == SerializedPropertyType.ManagedReference;
                                bool array = property.isArray && kind != SerializedPropertyType.String;
                                if (reference || array || property.propertyPath == path)
                                {
                                    shape.entries.Add(new Entry
                                    {
                                        Target = target,
                                        Path = property.propertyPath,
                                        Kind = kind,
                                        Type = reference ? property.managedReferenceFullTypename : null,
                                        Id = reference ? property.managedReferenceId : 0,
                                        Size = array ? property.arraySize : -1
                                    });
                                }
                                enterChildren = kind != SerializedPropertyType.String
                                    && (!reference || (!string.IsNullOrEmpty(property.managedReferenceFullTypename)
                                        && visited.Add(property.managedReferenceId)));
                            }
                            while (property.Next(enterChildren) && !SerializedProperty.EqualContents(property, end));
                        }
                    }
                }
                return shape;
            }

            internal bool Matches (Shape other)
            {
                if (entries.Count != other.entries.Count)
                {
                    return false;
                }
                for (int i = 0; i < entries.Count; i++)
                {
                    if (!entries[i].Equals(other.entries[i]))
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        // Unity's legacy and data-binding implementations meet at this private boundary.
        // Wrap the captured native action, not a registration: the same action is used
        // before activation, after activation and by already queued native notifications.
        private static class NativeAccess
        {
            private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            private static readonly Assembly EditorAssembly = typeof(PropertyField).Assembly;
            private static readonly Assembly UIAssembly = typeof(VisualElement).Assembly;
            private static readonly Assembly BindingAssembly = Assembly.Load("UnityEditor.UIElementsModule");
            private static readonly Type ContextType = BindingAssembly.GetType("UnityEditor.UIElements.Bindings.SerializedObjectBindingContext", true);
            private static readonly Type UpdaterType = BindingAssembly.GetType("UnityEditor.UIElements.Bindings.SerializedObjectBindingContextUpdater", true);
            private static readonly Type ImplementationType = BindingAssembly.GetType("UnityEditor.UIElements.Bindings.DefaultSerializedObjectBindingImplementation", true);
            private static readonly Type TrackedValueType = ContextType.GetNestedType("TrackedValue", BindingFlags.NonPublic);
            private static readonly FieldInfo Property = RequireField(typeof(PropertyField), "m_SerializedProperty");
            private static readonly PropertyInfo PropertyIsValid = RequireProperty(typeof(SerializedProperty), "isValid");
            private static readonly FieldInfo CustomRoot = RequireField(typeof(PropertyField), "m_CustomPropertyGUI");
            private static readonly FieldInfo ChildRoot = RequireField(typeof(PropertyField), "m_ChildField");
            private static readonly Func<object, VisualElement> ReadUpdaterOwner = BindUpdaterOwnerReader();
            private static readonly Func<VisualElement, object> ReadUpdater = BindUpdaterReader();
            private static readonly PropertyInfo BindingContext = RequireProperty(UpdaterType.BaseType, "bindingContext");
            private static readonly PropertyInfo Source = RequireProperty(ContextType, "serializedObject");
            private static readonly FieldInfo ValueTracker = RequireField(ContextType, "m_ValueTracker");
            private static readonly FieldInfo TrackedValues = RequireField(ValueTracker.FieldType, "m_TrackedValues");
            private static readonly FieldInfo Cookie = RequireField(TrackedValueType, "cookie");
            private static readonly FieldInfo Kind = RequireField(TrackedValueType, "originalPropType");
            private static readonly FieldInfo OnChange = RequireField(TrackedValueType, "onChangeCallback");
            private static readonly FieldInfo OnUpdate = RequireField(TrackedValueType, "onUpdateCallback");
            private static readonly Func<SerializedProperty, int> PropertyHash = (Func<SerializedProperty, int>)Delegate.CreateDelegate(
                typeof(Func<SerializedProperty, int>), RequireProperty(typeof(SerializedProperty), "hashCodeForPropertyPath").GetGetMethod(true));
            private static readonly PropertyInfo PendingTracking = UpdaterType.GetProperty("trackedProperties", Instance);
            private static readonly Type PendingRegistrationType = PendingTracking?.PropertyType.GetGenericArguments()[0];
            private static readonly FieldInfo PendingPath = PendingRegistrationType?.GetField("propertyPath", Instance);
            private static readonly FieldInfo PendingCallback = PendingRegistrationType?.GetField("callback", Instance);
            private static readonly EventInfo HierarchyChanged = UIAssembly.GetType("UnityEngine.UIElements.BaseVisualElementPanel", true)
                .GetEvent("hierarchyChanged", Instance);
            private static readonly Type ChangeType = HierarchyChanged.EventHandlerType.GetMethod("Invoke").GetParameters()[1].ParameterType;
            private static readonly MethodInfo SubscribeBindingMethod = typeof(NativeAccess).GetMethod(nameof(SubscribeBindingEvent), BindingFlags.Static | BindingFlags.NonPublic)
                .MakeGenericMethod(EditorAssembly.GetType("UnityEditor.UIElements.SerializedObjectBindEvent", true));

            internal static readonly object AddHierarchyChange = ReadHierarchyChange("Add", "AddedToParent");
            internal static readonly object RemoveHierarchyChange = ReadHierarchyChange("Remove", "RemovedFromParent");

            private static FieldInfo RequireField (Type type, string name)
                => type.GetField(name, Instance) ?? throw new MissingFieldException(type.FullName, name);

            private static PropertyInfo RequireProperty (Type type, string name)
                => type.GetProperty(name, Instance) ?? throw new MissingMemberException(type.FullName, name);

            private static Func<object, VisualElement> BindUpdaterOwnerReader ()
            {
                var updater = Expression.Parameter(typeof(object), "updater");
                var instance = Expression.Convert(updater, UpdaterType);
                FieldInfo field = UpdaterType.GetField("owner", Instance);
                Expression read = field != null ? Expression.Field(instance, field)
                    : Expression.Property(instance, RequireProperty(UpdaterType, "owner"));
                return Expression.Lambda<Func<object, VisualElement>>(read, updater).Compile();
            }

            private static Func<VisualElement, object> BindUpdaterReader ()
            {
                var element = Expression.Parameter(typeof(VisualElement), "element");
                MethodInfo legacy = UIAssembly.GetType("UnityEngine.UIElements.VisualTreeBindingsUpdater", true)
                    .GetMethod("GetAdditionalBinding", BindingFlags.Static | BindingFlags.Public);
                Expression read;
                if (legacy != null && legacy.ReturnType.IsAssignableFrom(UpdaterType))
                {
                    read = Expression.Call(legacy, element);
                }
                else
                {
                    Type id = UIAssembly.GetType("UnityEngine.UIElements.BindingId", true);
                    MethodInfo get = typeof(VisualElement).GetMethod("GetBinding", new[] { id })
                        ?? throw new MissingMethodException(typeof(VisualElement).FullName, "GetBinding");
                    string key = (string)typeof(BindingExtensions).GetField("s_SerializedBindingContextUpdaterId",
                        BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                    read = Expression.Call(element, get, Expression.New(id.GetConstructor(new[] { typeof(string) }), Expression.Constant(key)));
                }
                return Expression.Lambda<Func<VisualElement, object>>(Expression.Convert(read, typeof(object)), element).Compile();
            }

            private static object ReadHierarchyChange (string legacy, string current)
                => Enum.Parse(ChangeType, Enum.IsDefined(ChangeType, current) ? current : legacy);

            internal static Delegate SubscribeHierarchy (IPanel panel, NativeManagedReferenceTracking scope)
            {
                MethodInfo method = typeof(NativeManagedReferenceTracking).GetMethod(nameof(OnHierarchyChanged), Instance).MakeGenericMethod(ChangeType);
                ParameterInfo[] signature = HierarchyChanged.EventHandlerType.GetMethod("Invoke").GetParameters();
                var parameters = new ParameterExpression[signature.Length];
                for (int i = 0; i < parameters.Length; i++)
                {
                    parameters[i] = Expression.Parameter(signature[i].ParameterType, signature[i].Name);
                }
                // Newer hierarchy events also provide batch context; Add/Remove still
                // identify the affected element, which is all this scope owns.
                Delegate callback = Expression.Lambda(HierarchyChanged.EventHandlerType,
                    Expression.Call(Expression.Constant(scope), method, parameters[0], parameters[1]), parameters).Compile();
                HierarchyChanged.GetAddMethod(true).Invoke(panel, new object[] { callback });
                return callback;
            }

            internal static void UnsubscribeHierarchy (IPanel panel, Delegate callback)
                => HierarchyChanged.GetRemoveMethod(true).Invoke(panel, new object[] { callback });

            internal static IDisposable SubscribeBinding (VisualElement element, NativeManagedReferenceTracking scope)
                => (IDisposable)SubscribeBindingMethod.Invoke(null, new object[] { element, scope });

            private static IDisposable SubscribeBindingEvent<TEvent> (VisualElement element, NativeManagedReferenceTracking scope)
                where TEvent : EventBase<TEvent>, new()
                => new BindingSubscription<TEvent>(element, scope);

            private sealed class BindingSubscription<TEvent> : IDisposable where TEvent : EventBase<TEvent>, new()
            {
                private readonly VisualElement element;
                private readonly EventCallback<TEvent> callback;

                internal BindingSubscription (VisualElement element, NativeManagedReferenceTracking scope)
                {
                    this.element = element;
                    callback = evt => scope.OnObjectBind((VisualElement)evt.target);
                    element.RegisterCallback(callback, TrickleDown.TrickleDown);
                }

                public void Dispose () => element.UnregisterCallback(callback, TrickleDown.TrickleDown);
            }

            internal static VisualElement GetTrackedRoot (PropertyField field)
                => (VisualElement)ChildRoot.GetValue(field) ?? (VisualElement)CustomRoot.GetValue(field);

            internal static bool IsValid (SerializedProperty property) => property != null && (bool)PropertyIsValid.GetValue(property);

            internal static SerializedProperty GetProperty (PropertyField field)
            {
                var property = (SerializedProperty)Property.GetValue(field);
                return IsValid(property) ? property : null;
            }

            internal static object GetContext (VisualElement owner)
            {
                object updater = ReadUpdater(owner);
                return updater != null && updater.GetType() == UpdaterType && ReferenceEquals(ReadUpdaterOwner(updater), owner)
                    ? BindingContext.GetValue(updater) : null;
            }

            internal static SerializedObject GetSource (object context) => (SerializedObject)Source.GetValue(context);
            internal static IDictionary GetTrackedValues (object context) => (IDictionary)TrackedValues.GetValue(ValueTracker.GetValue(context));

            internal static IEnumerable<object> GetCallbackSlots (VisualElement owner, object context, SerializedProperty property)
            {
                var yielded = new HashSet<object>();
                // Modern context updaters defer registration until activation. Inspect
                // their existing callbacks without changing the pending list or its order.
                if (PendingTracking != null)
                {
                    object updater = ReadUpdater(owner);
                    if (updater != null && updater.GetType() == UpdaterType)
                    {
                        foreach (object registration in (IEnumerable)PendingTracking.GetValue(updater))
                        {
                            if ((string)PendingPath.GetValue(registration) == property.propertyPath)
                            {
                                object slot = GetCallbackSlot((Action<object, SerializedProperty>)PendingCallback.GetValue(registration));
                                if (slot != null && yielded.Add(slot))
                                {
                                    yield return slot;
                                }
                            }
                        }
                    }
                }
                IEnumerable values = (IEnumerable)GetTrackedValues(context)[PropertyHash(property)];
                if (values == null)
                {
                    yield break;
                }
                foreach (object value in values)
                {
                    if (value.GetType() == TrackedValueType && ReferenceEquals(Cookie.GetValue(value), owner)
                        && (SerializedPropertyType)Kind.GetValue(value) == SerializedPropertyType.ManagedReference && OnUpdate.GetValue(value) == null)
                    {
                        object slot = GetCallbackSlot((Action<object, SerializedProperty>)OnChange.GetValue(value));
                        if (slot != null && yielded.Add(slot))
                        {
                            yield return slot;
                        }
                    }
                }
            }

            internal static bool Contains (VisualElement owner, object context, SerializedProperty property, object value)
            {
                foreach (object current in GetCallbackSlots(owner, context, property))
                {
                    if (ReferenceEquals(current, value))
                    {
                        return true;
                    }
                }
                return false;
            }

            internal static Action<SerializedProperty> GetCallback (object value)
                => (Action<SerializedProperty>)RequireField(value.GetType(), "callback").GetValue(value);

            internal static void SetCallback (object value, Action<SerializedProperty> callback)
                => RequireField(value.GetType(), "callback").SetValue(value, callback);

            private static object GetCallbackSlot (Action<object, SerializedProperty> callback)
            {
                if (callback == null || callback.GetInvocationList().Length != 1 || callback.Target == null)
                {
                    return null;
                }
                Type closure = callback.Target.GetType();
                if ((closure.DeclaringType != ImplementationType && closure.DeclaringType != typeof(BindingExtensions)) || callback.Method.DeclaringType != closure
                    || !closure.IsDefined(typeof(CompilerGeneratedAttribute), false))
                {
                    return null;
                }
                FieldInfo capturedCallback = closure.GetField("callback", Instance);
                if (capturedCallback == null || capturedCallback.FieldType != typeof(Action<SerializedProperty>))
                {
                    return null;
                }
                return callback.Target;
            }

            internal static bool IsNativeCallback (Action<SerializedProperty> callback, PropertyField field)
            {
                return callback != null && callback.GetInvocationList().Length == 1 && ReferenceEquals(callback.Target, field)
                    && callback.Method.DeclaringType == typeof(PropertyField)
                    && callback.Method.IsDefined(typeof(CompilerGeneratedAttribute), false)
                    && (callback.Method.Name.StartsWith("<Reset>", StringComparison.Ordinal)
                        || callback.Method.Name.StartsWith("<ResetInternal>", StringComparison.Ordinal));
            }
        }
    }
}
#endif
