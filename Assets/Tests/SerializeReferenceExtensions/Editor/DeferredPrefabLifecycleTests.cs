using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MackySoft.SerializeReferenceExtensions.Tests
{
    public sealed class DeferredPrefabLifecycleTests
    {
        private const string PasteType = "Paste Type and Values";
        private const string PasteValues = "Paste Values Only";
        private string folder;
        private Scene scene;
        private Scene loadedScene;
        private GameObject prefab;
        private GameObject instance;
        private Type componentType;

        [SetUp]
        public void SetUp ()
        {
            Undo.ClearAll();
            folder = "Assets/__DeferredPrefab_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
            scene = SceneManager.GetActiveScene();
            // Reuse the project's public runtime example; asmdef tests cannot reference Assembly-CSharp directly.
            componentType = Type.GetType("Example, Assembly-CSharp", true);
            var template = new GameObject("Deferred prefab consumer " + Guid.NewGuid().ToString("N"));
            try
            {
                Component templateComponent = template.AddComponent(componentType);
                ConfigureFood(templateComponent, "Apple", "Prefab apple", 13f);
                prefab = PrefabUtility.SaveAsPrefabAsset(template, folder + "/Source.prefab");
                Assert.That(prefab, Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(template);
            }
            instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);

            var clipboardSource = new GameObject("Clipboard source");
            try
            {
                Component sourceComponent = clipboardSource.AddComponent(componentType);
                ConfigureFood(sourceComponent, "Grape", "Copied value", 77f);
                using (var serialized = new SerializedObject(sourceComponent))
                using (SerializedProperty property = serialized.FindProperty("foodOne"))
                {
                    SerializedPropertyContextMenu.Execute(SerializedPropertyContextMenu.Open(property), "Copy \"foodOne\" property");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(clipboardSource);
            }
            AssetDatabase.SaveAssets();
            Undo.ClearAll();
        }

        [TearDown]
        public void TearDown ()
        {
            Undo.ClearAll();
            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                Assert.That(EditorSceneManager.CloseScene(loadedScene, true), Is.True);
            }
            if (instance != null)
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
            if (!string.IsNullOrEmpty(folder))
            {
                Assert.That(AssetDatabase.DeleteAsset(folder), Is.True);
            }
        }

        [TestCase(PasteType)]
        [TestCase(PasteValues)]
        public void Paste_RecordsPrefabOverrideAndSupportsUndoRedoAndSavedInstance (string command)
        {
            Component component = instance.GetComponent(componentType);
            object original = ReadReference(component);
            Assert.That(PrefabUtility.HasPrefabInstanceAnyOverrides(instance, false), Is.False);
            using (var serialized = new SerializedObject(component))
            using (SerializedProperty property = serialized.FindProperty("foodOne"))
            {
                SerializedPropertyContextMenu.Execute(SerializedPropertyContextMenu.Open(property), command);
            }
            Undo.FlushUndoRecordObjects();
            AssertFood(component, command == PasteType ? "Grape" : "Apple", "Copied value", 77f);
            Assert.That(ReadReference(component), command == PasteType ? Is.Not.SameAs(original) : Is.SameAs(original));
            Assert.That(PrefabUtility.HasPrefabInstanceAnyOverrides(instance, false), Is.True);
            AssertSourcePrefab();

            Undo.PerformUndo();
            AssertFood(component, "Apple", "Prefab apple", 13f);
            Assert.That(PrefabUtility.HasPrefabInstanceAnyOverrides(instance, false), Is.False);
            Undo.PerformRedo();
            AssertFood(component, command == PasteType ? "Grape" : "Apple", "Copied value", 77f);
            Assert.That(PrefabUtility.HasPrefabInstanceAnyOverrides(instance, false), Is.True);
            AssertSourcePrefab();

            string scenePath = folder + "/Instance.unity";
            // Save a copy and read it in isolation, preserving all currently open scenes.
            Assert.That(EditorSceneManager.SaveScene(scene, scenePath, true), Is.True);
            loadedScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            GameObject loadedInstance = loadedScene.GetRootGameObjects().Single(root => root.name == instance.name);
            component = loadedInstance.GetComponent(componentType);
            Assert.That(PrefabUtility.IsPartOfPrefabInstance(component), Is.True);
            AssertFood(component, command == PasteType ? "Grape" : "Apple", "Copied value", 77f);
            Assert.That(PrefabUtility.HasPrefabInstanceAnyOverrides(loadedInstance, false), Is.True);
            AssertSourcePrefab();
        }

        private void AssertSourcePrefab ()
        {
            AssertFood(prefab.GetComponent(componentType), "Apple", "Prefab apple", 13f);
        }

        private static object ReadReference (Component component)
        {
            return component.GetType().GetField("foodOne").GetValue(component);
        }

        private static void ConfigureFood (Component component, string typeName, string name, float calories)
        {
            object food = Activator.CreateInstance(Type.GetType(typeName + ", Assembly-CSharp", true));
            component.GetType().GetField("foodOne").SetValue(component, food);
            using (var serialized = new SerializedObject(component))
            using (SerializedProperty property = serialized.FindProperty("foodOne"))
            using (SerializedProperty foodName = property.FindPropertyRelative("name"))
            using (SerializedProperty kcal = property.FindPropertyRelative("kcal"))
            {
                foodName.stringValue = name;
                kcal.floatValue = calories;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void AssertFood (Component component, string typeName, string name, float calories)
        {
            Assert.That(ReadReference(component).GetType().Name, Is.EqualTo(typeName));
            using (var serialized = new SerializedObject(component))
            using (SerializedProperty property = serialized.FindProperty("foodOne"))
            using (SerializedProperty foodName = property.FindPropertyRelative("name"))
            using (SerializedProperty kcal = property.FindPropertyRelative("kcal"))
            {
                Assert.That(foodName.stringValue, Is.EqualTo(name));
                Assert.That(kcal.floatValue, Is.EqualTo(calories));
            }
        }
    }
}
