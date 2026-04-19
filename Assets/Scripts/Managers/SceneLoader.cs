using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using POELike.Game;
using POELike.Game.UI;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace POELike.Managers
{
    /// <summary>
    /// 场景加载器
    /// 负责异步加载场景，并通过静态字段在场景间传递数据
    /// </summary>
    public static class SceneLoader
    {
        /// <summary>场景名称常量</summary>
        public const string SceneCharacterSelect = "CharacterSelectScene";
        public const string SceneGame            = "GameScene";
        public const string SceneMission         = "MissionScene";
        public const string SceneLoading         = "LoadingScene";

        private const float VisibleLoadingSceneProgress = 0.1f;
        private static bool _isGameplaySceneLoading;

        /// <summary>
        /// 跨场景传递的角色存档数据（在加载玩法场景前赋值）
        /// </summary>
        public static CharacterSaveData PendingCharacterData { get; set; }

        /// <summary>
        /// 跨场景传递的目标地图上下文（在加载 MissionScene 前赋值）
        /// </summary>
        public static MapLevelData PendingMapLevelData { get; private set; }

        /// <summary>
        /// 标记下一次场景切换是否需要保留现有玩家 ECS 实体。
        /// </summary>
        public static bool PreservePlayerEntityOnNextGameplaySceneLoad { get; private set; }

        /// <summary>
        /// 当前 LoadingScene 想要加载的目标场景。
        /// </summary>
        public static string PendingTargetSceneName { get; private set; }

        /// <summary>
        /// 当前 LoadingScene 的标准化加载进度（0~1）。
        /// </summary>
        public static float CurrentLoadingProgress { get; private set; }

        /// <summary>
        /// 异步加载初始游戏场景。
        /// </summary>
        public static void LoadGameScene(CharacterSaveData data)
        {
            PendingCharacterData = data;
            PendingMapLevelData = null;
            PreservePlayerEntityOnNextGameplaySceneLoad = false;
            BeginLoadingGameplayScene(SceneGame);
        }

        /// <summary>
        /// 异步加载任务场景，并携带目标地图上下文。
        /// </summary>
        public static void LoadMissionScene(CharacterSaveData data, MapLevelData mapLevel)
        {
            PendingCharacterData = data;
            PendingMapLevelData = mapLevel;
            PreservePlayerEntityOnNextGameplaySceneLoad = true;
            BeginLoadingGameplayScene(SceneMission);
        }

        public static bool IsGameplayScene(string sceneName)
        {
            return sceneName == SceneGame || sceneName == SceneMission;
        }

        public static bool IsLoadingScene(string sceneName)
        {
            return sceneName == SceneLoading;
        }

        public static MapLevelData ConsumePendingMapLevelData()
        {
            var data = PendingMapLevelData;
            PendingMapLevelData = null;
            return data;
        }

        public static void NotifyGameplaySceneActivated()
        {
            PreservePlayerEntityOnNextGameplaySceneLoad = false;
            PendingTargetSceneName = null;
            CurrentLoadingProgress = 1f;
            _isGameplaySceneLoading = false;
        }

        private static void BeginLoadingGameplayScene(string targetSceneName)
        {
            if (_isGameplaySceneLoading)
            {
                Debug.LogWarning($"[SceneLoader] 正在加载场景 {PendingTargetSceneName}，忽略新的切换请求：{targetSceneName}");
                return;
            }

            PendingTargetSceneName = targetSceneName;
            CurrentLoadingProgress = 0f;
            _isGameplaySceneLoading = true;

            if (GameManager.Instance == null)
            {
                Debug.LogError($"[SceneLoader] 无法加载场景 {targetSceneName}：GameManager.Instance 为空。");
                _isGameplaySceneLoading = false;
                return;
            }

            GameManager.Instance.StartCoroutine(LoadGameplaySceneTransitionAsync());
        }

        private static IEnumerator LoadGameplaySceneTransitionAsync()
        {
            Debug.Log($"[SceneLoader] 进入 LoadingScene，目标场景：{PendingTargetSceneName}");
            CurrentLoadingProgress = 0f;

            var loadingSceneOp = LoadSceneAsyncForCurrentEnvironment(SceneLoading);
            if (loadingSceneOp == null)
            {
                Debug.LogError("[SceneLoader] 加载 LoadingScene 失败。");
                CurrentLoadingProgress = 0f;
                _isGameplaySceneLoading = false;
                yield break;
            }

            while (!loadingSceneOp.isDone)
                yield return null;

            CurrentLoadingProgress = VisibleLoadingSceneProgress;
            yield return null;

            string targetSceneName = PendingTargetSceneName;
            if (string.IsNullOrEmpty(targetSceneName))
            {
                Debug.LogWarning("[SceneLoader] LoadingScene 中没有待加载的目标场景。");
                CurrentLoadingProgress = 0f;
                _isGameplaySceneLoading = false;
                yield break;
            }

            Debug.Log($"[SceneLoader] 开始异步加载目标场景：{targetSceneName}");

            var targetSceneOp = LoadSceneAsyncForCurrentEnvironment(targetSceneName);
            if (targetSceneOp == null)
            {
                Debug.LogError($"[SceneLoader] 创建异步加载操作失败：{targetSceneName}");
                CurrentLoadingProgress = 0f;
                _isGameplaySceneLoading = false;
                yield break;
            }

            targetSceneOp.allowSceneActivation = false;

            while (targetSceneOp.progress < 0.9f)
            {
                float normalizedProgress = Mathf.Clamp01(targetSceneOp.progress / 0.9f);
                CurrentLoadingProgress = Mathf.Lerp(VisibleLoadingSceneProgress, 0.95f, normalizedProgress);
                yield return null;
            }

            CurrentLoadingProgress = 1f;
            yield return null;

            Debug.Log($"[SceneLoader] 目标场景加载完成，准备激活：{targetSceneName}");
            targetSceneOp.allowSceneActivation = true;

            while (!targetSceneOp.isDone)
                yield return null;
        }

        private static AsyncOperation LoadSceneAsyncForCurrentEnvironment(string sceneName)
        {
#if UNITY_EDITOR
            string scenePath = GetScenePath(sceneName);
            if (!string.IsNullOrEmpty(scenePath))
            {
                return EditorSceneManager.LoadSceneAsyncInPlayMode(
                    scenePath,
                    new LoadSceneParameters(LoadSceneMode.Single));
            }
#endif
            return SceneManager.LoadSceneAsync(sceneName);
        }

        private static string GetScenePath(string sceneName)
        {
            switch (sceneName)
            {
                case SceneCharacterSelect:
                    return "Assets/Scenes/CharacterSelectScene.unity";
                case SceneGame:
                    return "Assets/Scenes/GameScene.unity";
                case SceneMission:
                    return "Assets/Scenes/MissionScene.unity";
                case SceneLoading:
                    return "Assets/Scenes/LoadingScene.unity";
                default:
                    return null;
            }
        }
    }
}
