using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEditor;

namespace MackySoft.SerializeReferenceExtensions.Editor
{
    internal sealed class DeferredManagedReferenceTarget
    {
        private sealed class HostObservation
        {
            internal UnityEngine.Object Host;
            internal long ReferenceId;
            internal string TypeName;
            internal string FieldTypeName;
            internal RuntimePath Path;

            internal bool MatchesProperty (SerializedProperty property)
            {
                return property != null && property.propertyType == SerializedPropertyType.ManagedReference &&
                    property.managedReferenceId == ReferenceId && property.managedReferenceFullTypename == TypeName &&
                    property.managedReferenceFieldTypename == FieldTypeName;
            }
        }

        private sealed class PathStep
        {
            internal FieldInfo Field;
            internal int Index;

            internal object Read (object container)
            {
                if (container == null)
                {
                    return null;
                }
                return Field != null ? Field.GetValue(container) : ((IList)container)[Index];
            }
        }

        private sealed class ContainerObservation
        {
            private readonly object value;
            private readonly object[] elements;

            internal ContainerObservation (object value, bool captureSequence)
            {
                this.value = SnapshotValue(value);
                if (captureSequence && value is IList list)
                {
                    elements = new object[list.Count];
                    for (int i = 0; i < elements.Length; i++)
                    {
                        elements[i] = SnapshotValue(list[i]);
                    }
                }
            }

            internal bool Matches (object current)
            {
                if (!SameValue(value, current))
                {
                    return false;
                }
                if (elements != null)
                {
                    var list = (IList)current;
                    if (list.Count != elements.Length)
                    {
                        return false;
                    }
                    for (int i = 0; i < elements.Length; i++)
                    {
                        if (!SameValue(elements[i], list[i]))
                        {
                            return false;
                        }
                    }
                }
                return true;
            }
        }

        private sealed class RuntimePath
        {
            private readonly PathStep[] steps;
            private readonly ContainerObservation[] containers;
            private readonly object value;

            private RuntimePath (PathStep[] steps, ContainerObservation[] containers, object value)
            {
                this.steps = steps;
                this.containers = containers;
                this.value = SnapshotValue(value);
            }

            internal static RuntimePath Capture (UnityEngine.Object host, string propertyPath)
            {
                string[] parts = propertyPath.Split('.');
                var steps = new List<PathStep>();
                var containers = new List<ContainerObservation>();
                object current = host;
                Type currentType = host.GetType();
                for (int i = 0; i < parts.Length; i++)
                {
                    PathStep step;
                    if (parts[i] == "Array" && i + 1 < parts.Length && parts[i + 1].StartsWith("data[", StringComparison.Ordinal))
                    {
                        string element = parts[++i];
                        if (!element.EndsWith("]", StringComparison.Ordinal) ||
                            !int.TryParse(element.Substring(5, element.Length - 6), out int index) || index < 0)
                        {
                            return null;
                        }
                        Type elementType = GetElementType(currentType);
                        if (elementType == null || (current != null && (current is not IList list || index >= list.Count)))
                        {
                            return null;
                        }
                        step = new PathStep { Index = index };
                        currentType = elementType;
                    }
                    else
                    {
                        FieldInfo field = FindField(currentType, parts[i]);
                        if (field == null)
                        {
                            return null;
                        }
                        step = new PathStep { Field = field };
                        currentType = field.FieldType;
                    }

                    // A field owner may implement IList for unrelated purposes; only index steps observe its sequence.
                    containers.Add(new ContainerObservation(current, step.Field == null));
                    steps.Add(step);
                    current = step.Read(current);
                    if (current != null)
                    {
                        currentType = current.GetType();
                    }
                }
                return new RuntimePath(steps.ToArray(), containers.ToArray(), current);
            }

            internal bool Matches (UnityEngine.Object host)
            {
                object current = host;
                for (int i = 0; i < steps.Length; i++)
                {
                    if (!containers[i].Matches(current))
                    {
                        return false;
                    }
                    current = steps[i].Read(current);
                }
                return SameValue(value, current);
            }

            private static FieldInfo FindField (Type type, string name)
            {
                for (; type != null; type = type.BaseType)
                {
                    FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    if (field != null)
                    {
                        return field;
                    }
                }
                return null;
            }

            private static Type GetElementType (Type type)
            {
                if (type.IsArray)
                {
                    return type.GetElementType();
                }
                for (; type != null; type = type.BaseType)
                {
                    if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                    {
                        return type.GetGenericArguments()[0];
                    }
                }
                return null;
            }
        }

        private static readonly Func<SerializedObject, IntPtr> NativePointer = BindNativePointer();

        private SerializedObject origin;
        private string propertyPath;
        private HostObservation[] hosts;
        private bool consumed;

        private DeferredManagedReferenceTarget (SerializedObject origin, string propertyPath, HostObservation[] hosts)
        {
            this.origin = origin;
            this.propertyPath = propertyPath;
            this.hosts = hosts;
        }

        /// <summary> Applies the caller's pending edits and captures a one-shot managed-reference menu target. </summary>
        /// <remarks> The caller retains ownership of its property and SerializedObject. The target retains no temporary native wrappers. </remarks>
        internal static DeferredManagedReferenceTarget Capture (SerializedProperty property)
        {
            SerializedObject origin = property.serializedObject;
            string path = property.propertyPath;
            if (!IsAlive(origin))
            {
                return null;
            }
            UnityEngine.Object[] targets = origin.targetObjects;
            if (targets.Length == 0)
            {
                return null;
            }
            foreach (UnityEngine.Object target in targets)
            {
                if (target == null)
                {
                    return null;
                }
            }

            // Commit the caller's buffered edits before observing live instances. The incoming iterator is no longer used.
            origin.ApplyModifiedProperties();
            var observations = new HostObservation[targets.Length];
            for (int i = 0; i < targets.Length; i++)
            {
                using (var serialized = new SerializedObject(targets[i]))
                using (SerializedProperty fresh = serialized.FindProperty(path))
                {
                    if (fresh == null || fresh.propertyType != SerializedPropertyType.ManagedReference)
                    {
                        return null;
                    }
                    RuntimePath runtimePath = RuntimePath.Capture(targets[i], path);
                    if (runtimePath == null)
                    {
                        return null;
                    }
                    observations[i] = new HostObservation
                    {
                        Host = targets[i],
                        ReferenceId = fresh.managedReferenceId,
                        TypeName = fresh.managedReferenceFullTypename,
                        FieldTypeName = fresh.managedReferenceFieldTypename,
                        Path = runtimePath
                    };
                }
            }
            return new DeferredManagedReferenceTarget(origin, path, observations);
        }

        /// <summary> Reads current corresponding properties to determine menu availability. </summary>
        /// <param name="inspect"> Receives properties borrowed only for this callback; this target releases their wrappers. </param>
        internal bool TryInspect (Action<SerializedProperty[]> inspect)
        {
            if (consumed || !HasCorrespondence())
            {
                return false;
            }
            return WithProperties(properties =>
            {
                inspect(properties);
                return true;
            });
        }

        /// <summary> Runs one command only after the caller and every host still identify the captured target. </summary>
        /// <param name="execute"> Receives scoped properties and returns true after executing the command, or false when it is unavailable. </param>
        /// <remarks> Consumes and releases the captured observations on success, cancellation, or exception. </remarks>
        internal bool TryExecute (Func<SerializedProperty[], bool> execute)
        {
            if (consumed)
            {
                return false;
            }
            consumed = true;
            try
            {
                // Check runtime identity first: a stale caller buffer must not restore a removed or shifted target.
                if (!HasCorrespondence())
                {
                    return false;
                }
                origin.ApplyModifiedProperties();
                bool executed = WithProperties(execute);
                if (executed)
                {
                    origin.Update();
                }
                return executed;
            }
            finally
            {
                origin = null;
                propertyPath = null;
                hosts = null;
            }
        }

        private bool HasCorrespondence ()
        {
            if (!IsAlive(origin))
            {
                return false;
            }
            foreach (HostObservation host in hosts)
            {
                if (host.Host == null || !host.Path.Matches(host.Host))
                {
                    return false;
                }
            }
            return true;
        }

        private bool WithProperties (Func<SerializedProperty[], bool> execute)
        {
            var objects = new SerializedObject[hosts.Length];
            var properties = new SerializedProperty[hosts.Length];
            try
            {
                for (int i = 0; i < hosts.Length; i++)
                {
                    if (hosts[i].Host == null)
                    {
                        return false;
                    }
                    objects[i] = new SerializedObject(hosts[i].Host);
                    properties[i] = objects[i].FindProperty(propertyPath);
                }
                // Finish opening every native view before the final all-host correspondence check.
                if (!HasCorrespondence())
                {
                    return false;
                }
                for (int i = 0; i < hosts.Length; i++)
                {
                    if (!hosts[i].MatchesProperty(properties[i]))
                    {
                        return false;
                    }
                }
                return execute(properties);
            }
            finally
            {
                for (int i = properties.Length - 1; i >= 0; i--)
                {
                    properties[i]?.Dispose();
                    objects[i]?.Dispose();
                }
            }
        }

        private static object SnapshotValue (object value)
        {
            // An interface/object can hold a mutable boxed struct; retain its value rather than that mutable box.
            return value != null && value.GetType().IsValueType ? RuntimeHelpers.GetObjectValue(value) : value;
        }

        private static bool SameValue (object expected, object current)
        {
            if (ReferenceEquals(expected, current))
            {
                return true;
            }
            if (expected == null || current == null || expected.GetType() != current.GetType())
            {
                return false;
            }
            Type type = expected.GetType();
            if (!type.IsValueType)
            {
                return false;
            }
            if (type.IsPrimitive || type.IsEnum)
            {
                return expected.Equals(current);
            }
            // Follow value-type fields only. Reference fields retain identity without visiting their graphs or user Equals.
            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!SameValue(field.GetValue(expected), field.GetValue(current)))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool IsAlive (SerializedObject serialized)
        {
            return serialized != null && NativePointer(serialized) != IntPtr.Zero;
        }

        private static Func<SerializedObject, IntPtr> BindNativePointer ()
        {
            FieldInfo field = typeof(SerializedObject).GetField("m_NativeObjectPtr", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new MissingFieldException(typeof(SerializedObject).FullName, "m_NativeObjectPtr");
            }
            var serialized = Expression.Parameter(typeof(SerializedObject), "serialized");
            return Expression.Lambda<Func<SerializedObject, IntPtr>>(Expression.Field(serialized, field), serialized).Compile();
        }
    }
}
