
using System;
using System.Collections.Generic;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Enum | AttributeTargets.Interface | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class AutoTypeMenuAttribute : Attribute
{
    public AutoTypeMenuAttribute(){}

    static Dictionary<Type, (string[], bool)> _cachePath = new ();

    public static bool TryGetPathIfThisOrAnyParentContainAutoTypeMenuAttribute(Type type, Type baseType, out string[] path) {
        if (_cachePath.TryGetValue(type, out (string[], bool) cachedPath)) {
            path = cachedPath.Item1;
            return cachedPath.Item2;
        }

        Type currentType = type;
        Stack<string> pathStack = new();
        while (currentType != null) {
            if(currentType != baseType) pathStack.Push(currentType.Name);
            AutoTypeMenuAttribute autoPath = Attribute.GetCustomAttribute(currentType, typeof(AutoTypeMenuAttribute)) as AutoTypeMenuAttribute;
            if (autoPath != null) {
                path = StackToReversedArray(pathStack);
                _cachePath[type] = (path, true);
                return true;
            }
            currentType = currentType.BaseType;
        }
        _cachePath[type] = (null, false);
        path = null;
        return false;
    }

    static string[] StackToReversedArray(Stack<string> stack) {
        string[] arr = new string[stack.Count];
        int i = 0;
        foreach (var item in stack) {
            arr[i] = item;
            i++;
        }
        return arr;
    }
}