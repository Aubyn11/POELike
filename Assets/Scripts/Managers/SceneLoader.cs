using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using POELike.Game.UI;

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

        /// <summary>
        /// 跨场景传递的角色存档数据（在加载 GameScene 前赋值）
        /// </summary>
        public static CharacterSaveData PendingCharacterData { get; set; }

        /// <summary>
        /// 异步加载游戏场景
        /// </summary>
        public static void LoadGameScene(CharacterSaveData data)
        {
            PendingCharacterData = data;
            // 通过 GameManager 协程执行异步加载
            GameManager.Instance.StartCoroutine(LoadSceneAsync(SceneGame));
        }

        private static IEnumerator LoadSceneAsync(string sceneName)
        {
            // 显示加载提示（可后续扩展为进度条）
            Debug.Log($"[SceneLoader] 开始加载场景：{sceneName}");

            var op = SceneManager.LoadSceneAsync(sceneName);
            op.allowSceneActivation = false;

            while (op.progress < 0.9f)
            {
                Debug.Log($"[SceneLoader] 加载进度：{op.progress * 100:F0}%");
                yield return null;
            }

            Debug.Log("[SceneLoader] 场景加载完成，正在激活...");
            op.allowSceneActivation = true;
        }
    }
}
