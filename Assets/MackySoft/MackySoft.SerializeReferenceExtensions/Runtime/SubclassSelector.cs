using System;
using UnityEngine;

[Serializable]
public class SubclassSelector<T> where T : class
{
    [SerializeReference] private T _value;

    public T Value
    {
        get => _value as T;
        set => _value = value;
    }
}