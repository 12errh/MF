using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mate.Bootstrap.EditorTools
{
    /// <summary>
    /// Editor tool that creates the entry scene for the runtime: a Camera with
    /// AudioListener, a MateBootstrap GameObject, and registers the scene in
    /// EditorBuildSettings so a player build has an entry point.
    /// </summary>
    public static class MateSceneBuilder
    {
        public const string MenuPath = "Tools/Mate/Create Bootstrap Scene";
        private const string ScenePath = "Assets/MateFramework/Scenes/Bootstrap.unity";

        [MenuItem(MenuPath)]
        public static void CreateBootstrapScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // NewSceneSetup.DefaultGameObjects already adds a Main Camera;
            // ensure it has an AudioListener and the bootstrap object exists.
            if (Camera.main == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            else if (Camera.main.GetComponent<AudioListener>() == null)
            {
                Camera.main.gameObject.AddComponent<AudioListener>();
            }

            if (Object.FindFirstObjectByType<MateBootstrap>() == null)
            {
                var bootGo = new GameObject("MateBootstrap");
                bootGo.AddComponent<MateBootstrap>();
            }

            EnsureDirectory();
            EditorSceneManager.SaveScene(scene, ScenePath);

            // Register as a build scene (append if not present).
            var buildScenes = EditorBuildSettings.scenes;
            var alreadyPresent = false;
            foreach (var bs in buildScenes)
            {
                if (bs.path == ScenePath)
                {
                    alreadyPresent = true;
                    break;
                }
            }
            if (!alreadyPresent)
            {
                var updated = new EditorBuildSettingsScene[buildScenes.Length + 1];
                global::System.Array.Copy(buildScenes, updated, buildScenes.Length);
                updated[buildScenes.Length] = new EditorBuildSettingsScene(ScenePath, true);
                EditorBuildSettings.scenes = updated;
            }

            AssetDatabase.SaveAssets();
            // Skip the dialog in batchmode (headless CI/CLI) — it would block
            // waiting for a click.
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog(
                    "Mate Bootstrap Scene",
                    $"Created {ScenePath} with Camera + MateBootstrap and added it to Build Settings.",
                    "OK");
            }
        }

        private static void EnsureDirectory()
        {
            const string dir = "Assets/MateFramework/Scenes";
            if (!AssetDatabase.IsValidFolder(dir))
            {
                if (!AssetDatabase.IsValidFolder("Assets/MateFramework"))
                    AssetDatabase.CreateFolder("Assets", "MateFramework");
                AssetDatabase.CreateFolder("Assets/MateFramework", "Scenes");
            }
        }
    }
}
