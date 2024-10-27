using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using System.Reflection;

namespace MackySoft.SerializeReferenceExtensions.Editor
{
	public static class TypeSearch
	{

#if UNITY_2023_2_OR_NEWER
		static readonly Dictionary<Type, List<Type>> m_TypeCache = new Dictionary<Type, List<Type>>();
#endif

		public static IEnumerable<Type> GetTypes (Type baseType)
		{
#if UNITY_2023_2_OR_NEWER
			// NOTE: This is a generics solution for Unity 2023.2 and later.
			// 2023.2 because SerializeReference supports generic type instances and because the behaviour is stable.
			if (baseType.IsGenericType)
			{
				return GetTypesWithGeneric(baseType);
			}
			else
			{
				return GetTypesUsingTypeCache(baseType);
			}
#else
			return GetTypesUsingTypeCache(baseType);
#endif
		}

		static IEnumerable<Type> GetTypesUsingTypeCache (Type baseType)
		{
			return TypeCache.GetTypesDerivedFrom(baseType)
				.Append(baseType)
				.Where(IsValidType);
		}

#if UNITY_2023_2_OR_NEWER
		static IEnumerable<Type> GetTypesWithGeneric (Type baseType)
		{
			if (m_TypeCache.TryGetValue(baseType, out List<Type> result))
			{
				return result;
			}

			result = new List<Type>();
			Type genericTypeDefinition = null;
			Type[] targetTypeArguments = null;
			Type[] genericTypeParameters = null;

			if (baseType.IsGenericType)
			{
				genericTypeDefinition = baseType.GetGenericTypeDefinition();
				targetTypeArguments = baseType.GetGenericArguments();
				genericTypeParameters = genericTypeDefinition.GetGenericArguments();
			}
			else
			{
				genericTypeDefinition = baseType;
				targetTypeArguments = Type.EmptyTypes;
				genericTypeParameters = Type.EmptyTypes;
			}

			IEnumerable<Type> types = AppDomain.CurrentDomain.GetAssemblies()
				.SelectMany(x => x.GetTypes())
				.Where(IsValidType);

			foreach (Type type in types)
			{
				Type[] interfaceTypes = type.GetInterfaces();
				foreach (Type interfaceType in interfaceTypes)
				{
					if (!interfaceType.IsGenericType || interfaceType.GetGenericTypeDefinition() != genericTypeDefinition)
					{
						continue;
					}

					Type[] sourceTypeArguments = interfaceType.GetGenericArguments();

					bool allParametersMatch = true;

					for (int i = 0; i < genericTypeParameters.Length; i++)
					{
						var variance = genericTypeParameters[i].GenericParameterAttributes & GenericParameterAttributes.VarianceMask;

						Type sourceTypeArgument = sourceTypeArguments[i];
						Type targetTypeArgument = targetTypeArguments[i];

						if (variance == GenericParameterAttributes.Contravariant)
						{
							if (!sourceTypeArgument.IsAssignableFrom(targetTypeArgument))
							{
								allParametersMatch = false;
								break;
							}
						}
						else if (variance == GenericParameterAttributes.Covariant)
						{
							if (!targetTypeArgument.IsAssignableFrom(sourceTypeArgument))
							{
								allParametersMatch = false;
								break;
							}
						}
						else
						{
							if (sourceTypeArgument != targetTypeArgument)
							{
								allParametersMatch = false;
								break;
							}
						}
					}

					if (allParametersMatch)
					{
						result.Add(type);
						break;
					}
				}
			}

			m_TypeCache.Add(baseType, result);
			return result;
		}
#endif

		static bool IsValidType (Type type)
		{
			return
				(type.IsPublic || type.IsNestedPublic || type.IsNestedPrivate) &&
				!type.IsAbstract &&
				!type.IsGenericType &&
				!typeof(UnityEngine.Object).IsAssignableFrom(type) &&
				Attribute.IsDefined(type, typeof(SerializableAttribute)) &&
				!Attribute.IsDefined(type, typeof(HideInTypeMenuAttribute));
		}
	}
}