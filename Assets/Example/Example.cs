using System.Collections.Generic;
using System;
using UnityEditor;
using UnityEngine;

[Serializable]
public abstract class Food
{
	public string name;

	public float kcal;
}

[Serializable]
public class Apple : Food
{
	public Apple ()
	{
		name = "Apple";
		kcal = 100f;
	}
}

[Serializable]
public class Peach : Food
{
	public Peach ()
	{
		name = "Peach";
		kcal = 100f;
	}
}

[Serializable]
public class Grape : Food
{
	public Grape ()
	{
		name = "Grape";
		kcal = 100f;
	}
}

[Serializable]
[HideInTypeMenu]
public class Banana : Food
{
	public Banana ()
	{
		name = "Banana";
		kcal = 100f;
	}
}

public class Example : MonoBehaviour
{

	[SerializeReference]
	public Food food1 = new Apple();

	[SerializeReference]
	public Food food2 = new Peach();

	[SerializeReference]
	public Food food3 = new Grape();

	[SerializeReference, SubclassSelector]
	public Food foodOne = new Apple();

	[SerializeReference, SubclassSelector]
	public Food foodTwo = new Peach();

	// UseToStringAsLabel support on UNITY_2021_3_OR_NEWER
	[SerializeReference, SubclassSelector(UseToStringAsLabel = true)]
	public Food foodThree = new Grape();

	[SerializeReference]
	public List<Food> foodsOne = new List<Food>
	{
		new Apple(),
		new Peach(),
		new Grape()
	};

	[SerializeReference, SubclassSelector]
	public List<Food> foodsTwo = new List<Food>
	{
		new Apple(),
		new Peach(),
		new Grape()
	};
}

#if UNITY_EDITOR

/// These classes are in a folder named "Editor" in the project

[CustomPropertyDrawer(typeof(Peach), true)]
public class PeachDrawer : PropertyDrawer
{
	public override void OnGUI (Rect position, SerializedProperty property, GUIContent label)
	{
		position.height = EditorGUIUtility.singleLineHeight;
		EditorGUI.PropertyField(position, property.FindPropertyRelative("name"));

		position.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
		EditorGUI.PropertyField(position, property.FindPropertyRelative("kcal"));
	}

	public override float GetPropertyHeight (SerializedProperty property, GUIContent label)
	{
		return EditorGUIUtility.singleLineHeight * 2 + EditorGUIUtility.standardVerticalSpacing * 1;
	}
}

[CustomPropertyDrawer(typeof(Apple), true)]
public class AppleDrawer : PropertyDrawer
{
	public override void OnGUI (Rect position, SerializedProperty property, GUIContent label)
	{
		EditorGUI.LabelField(position, "I'm an apple!");
	}

	public override float GetPropertyHeight (SerializedProperty property, GUIContent label)
	{
		return EditorGUIUtility.singleLineHeight;
	}
}
#endif