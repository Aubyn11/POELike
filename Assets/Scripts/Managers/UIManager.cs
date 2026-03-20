using UnityEngine;
using UnityEngine.SceneManagement;
using POELike.Game.UI;

namespace POELike.Managers
{
    /// <summary>
    /// UI 管理器
    /// 负责动态加载、显示和销毁各类 UI 面板
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("UI Prefab 路径（相对 Resources 目录）")]
        [SerializeField] private string _characterSelectPanelPath = "UI/CharacterSelectPanel";

        // 运行时引用
        private Canvas                _rootCanvas;
        private CharacterSelectPanel  _characterSelectPanel;

        // ── 生命周期 ──────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            EnsureRootCanvas();
        }

        // ── 公开接口 ──────────────────────────────────────────────────

        /// <summary>
        /// 显示角色选择面板（若已存在则直接激活）
        /// </summary>
        public void ShowCharacterSelectPanel()
        {
            if (_characterSelectPanel != null)
            {
                _characterSelectPanel.gameObject.SetActive(true);
                return;
            }

            // 从 Resources 动态加载 Prefab
            var prefab = Resources.Load<GameObject>(_characterSelectPanelPath);
            if (prefab == null)
            {
                Debug.LogError($"[UIManager] 未找到 Prefab：Resources/{_characterSelectPanelPath}\n" +
                               "请将 CharacterSelectPanel.prefab 放到 Assets/Resources/UI/ 目录下。");
                return;
            }

            var go = Instantiate(prefab, _rootCanvas.transform);
            _characterSelectPanel = go.GetComponent<CharacterSelectPanel>();

            if (_characterSelectPanel == null)
            {
                Debug.LogError("[UIManager] CharacterSelectPanel 组件未找到，请检查 Prefab。");
                return;
            }

            // 绑定事件
            _characterSelectPanel.OnEnterGame       = OnEnterGame;
            _characterSelectPanel.OnCreateCharacter = OnCreateCharacter;
            _characterSelectPanel.OnDeleteCharacter = data =>
                Debug.Log($"[UIManager] 角色已删除：{data.CharacterName}");

            Debug.Log("[UIManager] CharacterSelectPanel 加载完成");
        }

        /// <summary>
        /// 隐藏角色选择面板
        /// </summary>
        public void HideCharacterSelectPanel()
        {
            if (_characterSelectPanel != null)
                _characterSelectPanel.gameObject.SetActive(false);
        }

        // ── 事件处理 ──────────────────────────────────────────────────

        private void OnEnterGame(CharacterSaveData data)
        {
            Debug.Log($"[UIManager] 进入游戏，角色：{data.CharacterName}  Lv.{data.Level}");
            HideCharacterSelectPanel();

            // 加载游戏场景，存档数据通过 SceneLoader.PendingCharacterData 传递
            SceneLoader.LoadGameScene(data);
        }

        private void OnCreateCharacter()
        {
            Debug.Log("[UIManager] 打开创建角色界面");
            // TODO：显示创建角色面板
        }

        // ── 内部辅助 ──────────────────────────────────────────────────

        /// <summary>
        /// 确保场景中存在一个持久化的根 Canvas
        /// </summary>
        private void EnsureRootCanvas()
        {
            // 优先复用场景中已有的 Canvas
            _rootCanvas = FindFirstObjectByType<Canvas>();
            if (_rootCanvas != null) return;

            var go = new GameObject("UIRootCanvas");
            DontDestroyOnLoad(go);

            _rootCanvas = go.AddComponent<Canvas>();
            _rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _rootCanvas.sortingOrder = 0;

            var scaler = go.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode         = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight  = 0.5f;

            go.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            Debug.Log("[UIManager] 已自动创建 UIRootCanvas");
        }
    }
}
