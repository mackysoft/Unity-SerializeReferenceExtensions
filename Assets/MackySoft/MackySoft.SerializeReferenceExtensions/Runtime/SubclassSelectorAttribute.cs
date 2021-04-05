#if UNITY_2019_3_OR_NEWER
using System;
using UnityEngine;

/// <summary>
/// Attribute to specify the type of the field serialized by the SerializeReference attribute in the inspector.
/// </summary>
[AttributeUsage(AttributeTargets.Field,AllowMultiple = false)]
public sealed class SubclassSelectorAttribute : PropertyAttribute {
	
}
#endif