using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using UnityEditor;
using Unity.Profiling;

namespace MackySoft.SerializeReferenceExtensions.Performance
{
    public static class ApiProbe
    {
        [Serializable]
        public sealed class Entry
        {
            public string caller;
            public string api;
            public long calls;
            public double milliseconds;
            public long allocationEvents;
        }

        private static readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
        private static ProfilerRecorder allocations;

        private readonly struct Scope : IDisposable
        {
            private readonly Entry entry;
            private readonly int allocationCount;
            private readonly long start;

            public Scope(string caller, string api)
            {
                string key = caller + "/" + api;
                if (!entries.TryGetValue(key, out entry))
                {
                    entry = new Entry { caller = caller, api = api };
                    entries.Add(key, entry);
                }
                allocationCount = allocations.Count;
                start = Stopwatch.GetTimestamp();
            }

            public void Dispose()
            {
                long end = Stopwatch.GetTimestamp();
                long allocated = allocations.Count - allocationCount;
                entry.calls++;
                entry.milliseconds += (end - start) * 1000.0 / Stopwatch.Frequency;
                entry.allocationEvents += allocated;
            }
        }

        public static void Reset()
        {
            if (allocations.Valid) { allocations.Dispose(); }
            entries.Clear();
            allocations = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GC.Alloc", 500000,
                ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
        }

        public static Entry[] Read()
        {
            allocations.Stop();
            if (allocations.WrappedAround || allocations.Count >= allocations.Capacity)
            {
                throw new InvalidOperationException("Allocation probe capacity exhausted.");
            }
            return new List<Entry>(entries.Values).ToArray();
        }

        public static Assembly[] Assemblies(string caller)
        {
            using (new Scope(caller, "AppDomain.GetAssemblies")) { return AppDomain.CurrentDomain.GetAssemblies(); }
        }

        public static Type[] Types(Assembly assembly, string caller)
        {
            using (new Scope(caller, "Assembly.GetTypes")) { return assembly.GetTypes(); }
        }

        public static TypeCache.TypeCollection Derived(Type type, string caller)
        {
            using (new Scope(caller, "TypeCache.GetTypesDerivedFrom")) { return TypeCache.GetTypesDerivedFrom(type); }
        }

        public static bool ContainsGeneric(Type type, string caller)
        {
            using (new Scope(caller, "Type.ContainsGenericParameters")) { return type.ContainsGenericParameters; }
        }

        public static bool IsGeneric(Type type, string caller)
        {
            using (new Scope(caller, "Type.IsGenericType")) { return type.IsGenericType; }
        }

        public static System.Attribute Attribute(Type type, string caller)
        {
            using (new Scope(caller, "Attribute.GetCustomAttribute")) { return System.Attribute.GetCustomAttribute(type, typeof(AddTypeMenuAttribute)); }
        }

        public static bool IsDefined(MemberInfo type, Type attribute, string caller)
        {
            using (new Scope(caller, "Attribute.IsDefined(" + attribute.Name + ")")) { return System.Attribute.IsDefined(type, attribute); }
        }

        public static Type[] GenericArguments(Type type, string caller)
        {
            using (new Scope(caller, "Type.GetGenericArguments")) { return type.GetGenericArguments(); }
        }

        public static Type GenericTypeDefinition(Type type, string caller)
        {
            using (new Scope(caller, "Type.GetGenericTypeDefinition")) { return type.GetGenericTypeDefinition(); }
        }

        public static Type[] Interfaces(Type type, string caller)
        {
            using (new Scope(caller, "Type.GetInterfaces")) { return type.GetInterfaces(); }
        }

        public static bool Assignable(Type target, Type candidate, string caller)
        {
            using (new Scope(caller, "Type.IsAssignableFrom")) { return target.IsAssignableFrom(candidate); }
        }

        public static Type MakeGeneric(Type type, Type[] arguments, string caller)
        {
            using (new Scope(caller, "Type.MakeGenericType")) { return type.MakeGenericType(arguments); }
        }

        public static string Nicify(string name)
        {
            using (new Scope("AdvancedTypePopup/TypeMenuUtility", "ObjectNames.NicifyVariableName")) { return ObjectNames.NicifyVariableName(name); }
        }
    }
}
