using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using POELike.Game.UI;

namespace POELike.Managers
{
    /// <summary>
    /// UI 管理器（单例）。
    /// 所有 UI 面板的加载优先从 <see cref="UIPool"/> 中取，
    /// 池中没有时才从 Resources 动态加载并实例化。
    /// 关闭面板时调用 <see cref="ReturnUI"/> 将其归还到池中，而非直接销毁。
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("UI Prefab 路径（相对 Resources 目录）")]
        [SerializeField] private string _characterSelectPanelPath = "UI/CharacterSelectPanel";
        [SerializeField] private string _chatPanelPath            = "UI/ChatPanel";
        [SerializeField] private string _bagPanelPath             = "UI/Bag";

        // ── 内部引用 ──────────────────────────────────────────────────
        private Canvas               _rootCanvas;
        private UIPool               _uiPool;

        // 运行时面板引用
        private CharacterSelectPanel _characterSelectPanel;
        private NpcDialogPanel       _chatPanel;
        private GameObject           _bagPanel;
        private InputAction          _bagToggleAction;
        private int                  _lastBagToggleFrame = -1;

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
            InitPool();
            SetupInputActions();
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        private void Update()
        {
            HandleBagHotkey();
        }

        private void OnGUI()
        {
            var currentEvent = Event.current;
            if (currentEvent == null || currentEvent.type != EventType.KeyDown || currentEvent.keyCode != KeyCode.I)
                return;

            TryToggleBagPanel();
            currentEvent.Use();
        }

        private void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;

            if (_bagToggleAction != null)
            {
                _bagToggleAction.performed -= OnBagTogglePerformed;
                _bagToggleAction.Dispose();
            }

            if (Instance == this)
            {
                HideBagPanel();
                _uiPool?.Clear();
                Instance = null;
            }
        }

        private void SetupInputActions()
        {
            _bagToggleAction = new InputAction("BagToggle", InputActionType.Button, "<Keyboard>/i");
            _bagToggleAction.performed += OnBagTogglePerformed;
            _bagToggleAction.Enable();
        }

        private void OnBagTogglePerformed(InputAction.CallbackContext context)
        {
            TryToggleBagPanel();
        }

        private void TryToggleBagPanel()
        {
            if (!CanUseGameplayPanels())
                return;

            if (_lastBagToggleFrame == Time.frameCount)
                return;

            _lastBagToggleFrame = Time.frameCount;
            ToggleBagPanel();
        }

        // ── 通用 UI 加载接口 ──────────────────────────────────────────

        /// <summary>
        /// 从 UIPool 或 Resources 获取一个 UI GameObject 并挂到根 Canvas 下。
        /// 优先从池中取；池中没有时从 Resources 加载并实例化。
        /// </summary>
        /// <param name="path">Resources 相对路径，例如 "UI/CharacterSelectPanel"</param>
        /// <returns>激活后的 GameObject，失败返回 null</returns>
        public GameObject GetUI(string path)
        {
            // 场景切换后 _rootCanvas 可能已被销毁，需重新创建
            if (_rootCanvas == null)
            {
                Debug.LogWarning("[UIManager] _rootCanvas 已被销毁，重新创建 UIRootCanvas。");
                EnsureRootCanvas();
            }

            // 1. 先尝试从池中取
            var go = _uiPool.Get(path);
            if (go != null)
            {
                go.transform.SetParent(_rootCanvas.transform, false);
                return go;
            }

            // 2. 池中没有，从 Resources 加载
            var prefab = Resources.Load<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError($"[UIManager] Resources 中未找到预制体：{path}");
                return null;
            }

            go = Instantiate(prefab, _rootCanvas.transform);
            Debug.Log($"[UIManager] 从 Resources 加载并实例化：{path}");
            return go;
        }

        /// <summary>
        /// 将 UI GameObject 归还到 UIPool（隐藏但不销毁），供下次复用。
        /// </summary>
        /// <param name="path">与 <see cref="GetUI"/> 传入的路径一致</param>
        /// <param name="go">要归还的 GameObject</param>
        public void ReturnUI(string path, GameObject go)
        {
            _uiPool.Return(path, go);
        }

        /// <summary>
        /// 彻底销毁并清除指定路径在池中的所有缓存。
        /// </summary>
        public void DestroyUI(string path)
        {
            _uiPool.Clear(path);
        }

        // ── 具体面板接口 ──────────────────────────────────────────────

        /// <summary>显示角色选择面板</summary>
        public void ShowCharacterSelectPanel()
        {
            if (_characterSelectPanel != null)
            {
                _characterSelectPanel.gameObject.SetActive(true);
                return;
            }

            var go = GetUI(_characterSelectPanelPath);
            if (go == null) return;

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
        /// 显示对话框面板（ChatPanel）
        /// </summary>
        /// <param name="npcName">NPC 名称</param>
        /// <param name="dialogText">对话内容</param>
        /// <param name="options">选项列表</param>
        public void ShowChatPanel(string npcName, string dialogText,
            System.Collections.Generic.List<NpcDialogOption> options = null)
        {
            if (_chatPanel == null)
            {
                var go = GetUI(_chatPanelPath);
                if (go == null) return;

                _chatPanel = go.GetComponent<NpcDialogPanel>();
                if (_chatPanel == null)
                {
                    Debug.LogError("[UIManager] NpcDialogPanel 组件未找到，请检查 ChatPanel Prefab。");
                    return;
                }
            }

            _chatPanel.gameObject.SetActive(true);
            _chatPanel.Open(npcName, dialogText, options ?? new System.Collections.Generic.List<NpcDialogOption>());
            Debug.Log($"[UIManager] ChatPanel 已打开：{npcName}");
        }

        /// <summary>显示背包面板</summary>
        public void ShowBagPanel()
        {
            if (!CanUseGameplayPanels())
                return;

            if (_bagPanel != null)
            {
                _bagPanel.SetActive(true);
                EnsureBagPanelController();
                RegisterBagOccluder();
                return;
            }

            _bagPanel = GetUI(_bagPanelPath);
            if (_bagPanel == null) return;

            EnsureBagPanelController();
            RegisterBagOccluder();
            Debug.Log("[UIManager] Bag 已打开");
        }

        /// <summary>隐藏背包面板（归还到池）</summary>
        public void HideBagPanel()
        {
            if (_bagPanel == null) return;
            UnregisterBagOccluder();
            ReturnUI(_bagPanelPath, _bagPanel);
            _bagPanel = null;
        }

        /// <summary>切换背包面板显示状态</summary>
        public void ToggleBagPanel()
        {
            if (_bagPanel != null)
            {
                HideBagPanel();
                return;
            }

            ShowBagPanel();
        }

        /// <summary>隐藏对话框面板（归还到池）</summary>
        public void HideChatPanel()
        {
            if (_chatPanel == null) return;
            _chatPanel.Close();
            ReturnUI(_chatPanelPath, _chatPanel.gameObject);
            _chatPanel = null;
        }

        /// <summary>隐藏角色选择面板（归还到池）</summary>
        public void HideCharacterSelectPanel()
        {
            if (_characterSelectPanel == null) return;
            ReturnUI(_characterSelectPanelPath, _characterSelectPanel.gameObject);
            _characterSelectPanel = null;
        }

        // ── 事件处理 ──────────────────────────────────────────────────

        private void OnEnterGame(CharacterSaveData data)
        {
            Debug.Log($"[UIManager] 进入游戏，角色：{data.CharacterName}  Lv.{data.Level}");
            HideCharacterSelectPanel();
            SceneLoader.LoadGameScene(data);
        }

        private void OnCreateCharacter()
        {
            Debug.Log("[UIManager] 打开创建角色界面");
            // TODO：显示创建角色面板
        }

        private void OnActiveSceneChanged(Scene previousScene, Scene newScene)
        {
            if (newScene.name != SceneLoader.SceneGame)
                HideBagPanel();
        }

        // ── 内部辅助 ──────────────────────────────────────────────────

        private void HandleBagHotkey()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.iKey.wasPressedThisFrame)
                return;

            TryToggleBagPanel();
        }

        private bool CanUseGameplayPanels()
        {
            if (SceneManager.GetActiveScene().name != SceneLoader.SceneGame)
                return false;

            if (_characterSelectPanel != null && _characterSelectPanel.gameObject.activeInHierarchy)
                return false;

            return true;
        }

        private void EnsureBagPanelController()
        {
            if (_bagPanel == null)
                return;

            var bagPanel = _bagPanel.GetComponent<BagPanel>();
            if (bagPanel == null)
                bagPanel = _bagPanel.AddComponent<BagPanel>();

            bagPanel.EnsureInitialized();
        }

        private void RegisterBagOccluder()
        {
            if (_bagPanel == null) return;

            var rectTransform = _bagPanel.GetComponent<RectTransform>();
            if (rectTransform != null)
                UIGamePanelManager.RegisterOccluder(rectTransform);
        }

        private void UnregisterBagOccluder()
        {
            if (_bagPanel == null) return;

            var rectTransform = _bagPanel.GetComponent<RectTransform>();
            if (rectTransform != null)
                UIGamePanelManager.UnregisterOccluder(rectTransform);
        }

        /// <summary>确保场景中存在一个持久化的根 Canvas</summary>

        private void EnsureRootCanvas()
        {
            // 不使用 FindFirstObjectByType，避免找到场景中会随场景销毁的 Canvas
            // 直接创建专属的持久化 UIRootCanvas
            var go = new GameObject("UIRootCanvas");
            DontDestroyOnLoad(go);

            _rootCanvas = go.AddComponent<Canvas>();
            _rootCanvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            _rootCanvas.sortingOrder = 0;

            var scaler = go.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode         = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight  = 0.5f;

            go.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            Debug.Log("[UIManager] 已自动创建 UIRootCanvas");

            // 确保场景中存在持久化的 EventSystem，防止场景切换后 UI 无法点击
            EnsureEventSystem();
        }

        /// <summary>确保场景中存在持久化的 EventSystem</summary>
        private void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;

            var esGo = new GameObject("UIEventSystem");
            DontDestroyOnLoad(esGo);
            esGo.AddComponent<EventSystem>();
            // 优先使用新版 Input System 模块，若未安装则回退到旧版
#if ENABLE_INPUT_SYSTEM
            esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif
            Debug.Log("[UIManager] 已自动创建持久化 EventSystem");
        }

        /// <summary>初始化 UIPool，创建隐藏的池根节点</summary>
        private void InitPool()
        {
            var poolRoot = new GameObject("UIPoolRoot");
            poolRoot.SetActive(false);
            DontDestroyOnLoad(poolRoot);
            _uiPool = new UIPool(poolRoot.transform);
            Debug.Log("[UIManager] UIPool 初始化完成");
        }
    }
}
