using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MackySoft.SerializeReferenceExtensions.Validation
{
    /// <summary>Opens the manual validation scene and creates its initial assets only when absent.</summary>
    public static class ValidationScene
    {
        public const string RootPath = "Assets/Validation/SerializeReferenceExtensions";
        public const string ScenePath = RootPath + "/Scenes/InspectorValidation.unity";
        public const string PrefabPath = RootPath + "/Prefabs/InspectorValidation.prefab";
        private const string UIToolkitObjectName = "01 UI Toolkit Inspector";

        [MenuItem("Tools/SerializeReference Extensions/Validation/Open Scene")]
        public static void Open ()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            Scene scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null
                ? Create() : EditorSceneManager.OpenScene(ScenePath);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == UIToolkitObjectName)
                {
                    Selection.activeGameObject = root;
                    break;
                }
            }
            EditorApplication.ExecuteMenuItem("Window/General/Inspector");
        }

        [MenuItem("Tools/SerializeReference Extensions/Validation/Select Multiple UI Toolkit Objects")]
        private static void SelectMultiple ()
        {
            Open();
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                return;
            }
            var targets = new System.Collections.Generic.List<Object>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name.StartsWith("04 ") || root.name.StartsWith("05 "))
                {
                    targets.Add(root);
                }
            }
            Selection.objects = targets.ToArray();
        }

        private static Scene Create ()
        {
            EnsureFolder(RootPath + "/Scenes");
            EnsureFolder(RootPath + "/Prefabs");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject(UIToolkitObjectName).AddComponent<UIToolkitInspectionTarget>();
            new GameObject("02 IMGUI Inspector").AddComponent<IMGUIInspectionTarget>();

            // The Prefab is real persistent data; native Apply/Revert owns its overrides.
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                var template = new GameObject("Inspector validation");
                template.AddComponent<UIToolkitInspectionTarget>();
                prefab = PrefabUtility.SaveAsPrefabAsset(template, PrefabPath);
                Object.DestroyImmediate(template);
            }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = "03 Prefab instance (with override)";
            SetReference(instance.GetComponent<UIToolkitInspectionTarget>(), new NumberValue(99, "Prefab override"));
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.GetComponent<UIToolkitInspectionTarget>());

            new GameObject("04 Multi selection A").AddComponent<UIToolkitInspectionTarget>();
            var other = new GameObject("05 Multi selection B").AddComponent<UIToolkitInspectionTarget>();
            SetReference(other, new TextValue());
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new System.InvalidOperationException("Could not save the Inspector validation scene.");
            }
            return scene;
        }

        private static void SetReference (InspectionTarget target, IInspectionValue value)
        {
            using (var serialized = new SerializedObject(target))
            {
                serialized.FindProperty("reference").managedReferenceValue = value;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void EnsureFolder (string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }
            int separator = path.LastIndexOf('/');
            string parent = path.Substring(0, separator);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(separator + 1));
        }
    }
}
