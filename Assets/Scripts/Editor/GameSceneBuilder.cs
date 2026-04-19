using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.InputSystem.UI;

namespace POELike.Editor
{
    /// <summary>
    /// GameScene 场景生成器
    /// 菜单：POELike → Scene → 生成 GameScene
    /// </summary>
    public static class GameSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/GameScene.unity";
        private const string MissionScenePath = "Assets/Scenes/MissionScene.unity";
        private const string LoadingScenePath = "Assets/Scenes/LoadingScene.unity";
        private const string CharacterSelectScenePath = "Assets/Scenes/CharacterSelectScene.unity";

        [MenuItem("POELike/Scene/生成 GameScene")]
        public static void BuildGameScene()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var gmGo = new GameObject("GameManager");
            gmGo.AddComponent<POELike.Managers.GameManager>();

            var uiGo = new GameObject("UIManager");
            uiGo.AddComponent<POELike.Managers.UIManager>();

            var gsmGo = new GameObject("GameSceneManager");
            gsmGo.AddComponent<POELike.Game.GameSceneManager>();

            // ── EventSystem（使用新版 Input System）────────────────────
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGo.AddComponent<InputSystemUIInputModule>();

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags    = CameraClearFlags.Skybox;
            cam.fieldOfView   = 60f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane  = 500f;
            camGo.transform.position = new Vector3(0f, 20f, -15f);
            camGo.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
            camGo.AddComponent<AudioListener>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            RegisterSceneForPlayMode(ScenePath);

            Debug.Log($"[GameSceneBuilder] GameScene 已生成：{ScenePath}");
            EditorUtility.DisplayDialog("生成成功", $"GameScene 已生成并加入场景列表\n路径：{ScenePath}", "确定");
        }

        [MenuItem("POELike/Scene/生成 MissionScene")]
        public static void BuildMissionScene()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var gmGo = new GameObject("GameManager");
            gmGo.AddComponent<POELike.Managers.GameManager>();

            var uiGo = new GameObject("UIManager");
            uiGo.AddComponent<POELike.Managers.UIManager>();

            var gsmGo = new GameObject("GameSceneManager");
            gsmGo.AddComponent<POELike.Game.GameSceneManager>();

            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGo.AddComponent<InputSystemUIInputModule>();

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags    = CameraClearFlags.Skybox;
            cam.fieldOfView   = 60f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane  = 500f;
            camGo.transform.position = new Vector3(0f, 20f, -15f);
            camGo.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
            camGo.AddComponent<AudioListener>();

            EditorSceneManager.SaveScene(scene, MissionScenePath);
            AssetDatabase.Refresh();
            RegisterSceneForPlayMode(MissionScenePath);

            Debug.Log($"[GameSceneBuilder] MissionScene 已生成：{MissionScenePath}");
            EditorUtility.DisplayDialog("生成成功", $"MissionScene 已生成并加入场景列表\n路径：{MissionScenePath}", "确定");
        }

        [MenuItem("POELike/Scene/生成 LoadingScene")]
        public static void BuildLoadingScene()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var gmGo = new GameObject("GameManager");
            gmGo.AddComponent<POELike.Managers.GameManager>();

            var uiGo = new GameObject("UIManager");
            uiGo.AddComponent<POELike.Managers.UIManager>();

            var loadingGo = new GameObject("LoadingSceneController");
            loadingGo.AddComponent<POELike.Managers.LoadingSceneController>();

            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGo.AddComponent<InputSystemUIInputModule>();

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.08f);
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 1000f;
            camGo.AddComponent<AudioListener>();

            EditorSceneManager.SaveScene(scene, LoadingScenePath);
            AssetDatabase.Refresh();
            RegisterSceneForPlayMode(LoadingScenePath);

            Debug.Log($"[GameSceneBuilder] LoadingScene 已生成：{LoadingScenePath}");
            EditorUtility.DisplayDialog("生成成功", $"LoadingScene 已生成并加入场景列表\n路径：{LoadingScenePath}", "确定");
        }

        [MenuItem("POELike/Scene/生成 CharacterSelectScene")]
        public static void BuildCharacterSelectScene()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var gmGo = new GameObject("GameManager");
            gmGo.AddComponent<POELike.Managers.GameManager>();

            var uiGo = new GameObject("UIManager");
            uiGo.AddComponent<POELike.Managers.UIManager>();

            // ── EventSystem（使用新版 Input System）────────────────────
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGo.AddComponent<InputSystemUIInputModule>();

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags      = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.08f);
            camGo.AddComponent<AudioListener>();

            EditorSceneManager.SaveScene(scene, CharacterSelectScenePath);
            AssetDatabase.Refresh();
            RegisterSceneForPlayMode(CharacterSelectScenePath);

            Debug.Log($"[GameSceneBuilder] CharacterSelectScene 已生成：{CharacterSelectScenePath}");
            EditorUtility.DisplayDialog("生成成功", $"CharacterSelectScene 已生成并加入场景列表\n路径：{CharacterSelectScenePath}", "确定");
        }

        [MenuItem("POELike/Scene/生成全部场景")]
        public static void BuildAllScenes()
        {
            BuildCharacterSelectScene();
            BuildLoadingScene();
            BuildGameScene();
            BuildMissionScene();
        }

        private static void RegisterSceneForPlayMode(string scenePath)
        {
            AddSceneToBuildSettings(scenePath);
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes;
            foreach (var s in scenes)
                if (s.path == scenePath) return;

            var newScenes = new EditorBuildSettingsScene[scenes.Length + 1];
            System.Array.Copy(scenes, newScenes, scenes.Length);
            newScenes[scenes.Length] = new EditorBuildSettingsScene(scenePath, true);
            EditorBuildSettings.scenes = newScenes;

            Debug.Log($"[GameSceneBuilder] 已将 {scenePath} 加入 Build Settings");
        }
    }
}