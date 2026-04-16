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
        [SerializeField] private string _charactorMainPanelPath   = "UI/CharactorMainPanel";
        [SerializeField] private string _charactorMassagePanelPath = "UI/CharactorMassagePanel";

        private const int CharactorMainPanelSortingOrder = 1000;
        private const int TooltipOverlaySortingOrder = 2000;

        // ── 内部引用 ──────────────────────────────────────────────────
        private Canvas               _rootCanvas;
        private UIPool               _uiPool;
        private RectTransform        _tooltipOverlay;

        // 运行时面板引用
        private CharacterSelectPanel _characterSelectPanel;
        private NpcDialogPanel       _chatPanel;
        private GameObject           _bagPanel;
        private GameObject           _charactorMainPanel;
        private GameObject           _charactorMassagePanel;
        private InputAction          _bagToggleAction;
        private InputAction          _charactorMassageToggleAction;
        private int                  _lastBagToggleFrame = -1;
        private int                  _lastCharactorMassageToggleFrame = -1;

        public BagPanel CurrentBagPanel
        {
            get
            {
                if (_bagPanel == null)
                    return null;

                EnsureBagPanelController();
                return _bagPanel.GetComponent<BagPanel>();
            }
        }

        public RectTransform TooltipOverlayRoot
        {
            get
            {
                if (_rootCanvas == null)
                    EnsureRootCanvas();

                EnsureTooltipOverlay();
                return _tooltipOverlay;
            }
        }

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
            HandleCharactorMassageHotkey();
        }

        private void OnGUI()
        {
            var currentEvent = Event.current;
            if (currentEvent == null || currentEvent.type != EventType.KeyDown)
                return;

            if (currentEvent.keyCode == KeyCode.I)
            {
                TryToggleBagPanel();
                currentEvent.Use();
                return;
            }

            if (currentEvent.keyCode == KeyCode.C)
            {
                TryToggleCharactorMassagePanel();
                currentEvent.Use();
            }
        }

        private void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;

            if (_bagToggleAction != null)
            {
                _bagToggleAction.performed -= OnBagTogglePerformed;
                _bagToggleAction.Dispose();
            }

            if (_charactorMassageToggleAction != null)
            {
                _charactorMassageToggleAction.performed -= OnCharactorMassageTogglePerformed;
                _charactorMassageToggleAction.Dispose();
            }

            if (Instance == this)
            {
                HideBagPanel();
                HideCharactorMainPanel();
                _uiPool?.Clear();
                Instance = null;
            }
        }

        private void SetupInputActions()
        {
            _bagToggleAction = new InputAction("BagToggle", InputActionType.Button, "<Keyboard>/i");
            _bagToggleAction.performed += OnBagTogglePerformed;
            _bagToggleAction.Enable();

            _charactorMassageToggleAction = new InputAction("CharactorMassageToggle", InputActionType.Button, "<Keyboard>/c");
            _charactorMassageToggleAction.performed += OnCharactorMassageTogglePerformed;
            _charactorMassageToggleAction.Enable();
        }

        private void OnBagTogglePerformed(InputAction.CallbackContext context)
        {
            TryToggleBagPanel();
        }

        private void OnCharactorMassageTogglePerformed(InputAction.CallbackContext context)
        {
            TryToggleCharactorMassagePanel();
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

        private void TryToggleCharactorMassagePanel()
        {
            if (!CanUseGameplayPanels())
                return;

            if (_lastCharactorMassageToggleFrame == Time.frameCount)
                return;

            _lastCharactorMassageToggleFrame = Time.frameCount;
            ToggleCharactorMassagePanel();
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
                RefreshCharactorMainPanel();
                return;
            }

            _bagPanel = GetUI(_bagPanelPath);
            if (_bagPanel == null) return;

            EnsureBagPanelController();
            RegisterBagOccluder();
            RefreshCharactorMainPanel();
            Debug.Log("[UIManager] Bag 已打开");
        }

        /// <summary>显示角色主界面底栏</summary>
        public void ShowCharactorMainPanel()
        {
            if (SceneManager.GetActiveScene().name != SceneLoader.SceneGame)
                return;

            if (_charactorMainPanel == null)
            {
                _charactorMainPanel = GetUI(_charactorMainPanelPath);
                if (_charactorMainPanel == null)
                    return;
            }

            _charactorMainPanel.SetActive(true);
            ConfigureCharactorMainPanel(_charactorMainPanel);
            RegisterCharactorMainPanelOccluder();
            RefreshCharactorMainPanel();
            Debug.Log("[UIManager] CharactorMainPanel 已显示并置顶");
        }

        public void ShowCharactorMassagePanel()
        {
            if (SceneManager.GetActiveScene().name != SceneLoader.SceneGame)
                return;

            if (_charactorMassagePanel == null)
            {
                _charactorMassagePanel = GetUI(_charactorMassagePanelPath);
                if (_charactorMassagePanel == null)
                    return;
            }

            _charactorMassagePanel.SetActive(true);
            ConfigureCharactorMassagePanel(_charactorMassagePanel);
            RegisterCharactorMassagePanelOccluder();
            RefreshCharactorMassagePanel();
            Debug.Log("[UIManager] CharactorMassagePanel 已显示");
        }

        /// <summary>隐藏角色主界面底栏（归还到池）</summary>
        public void HideCharactorMainPanel()
        {
            if (_charactorMainPanel != null)
            {
                UnregisterCharactorMainPanelOccluder();
                ReturnUI(_charactorMainPanelPath, _charactorMainPanel);
                _charactorMainPanel = null;
            }

            HideCharactorMassagePanel();
        }

        public void HideCharactorMassagePanel()
        {
            if (_charactorMassagePanel == null)
                return;

            UnregisterCharactorMassagePanelOccluder();
            ReturnUI(_charactorMassagePanelPath, _charactorMassagePanel);
            _charactorMassagePanel = null;
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

        public void ToggleCharactorMassagePanel()
        {
            if (_charactorMassagePanel != null)
            {
                HideCharactorMassagePanel();
                return;
            }

            ShowCharactorMassagePanel();
        }

        public void RefreshCharactorMainPanel()
        {
            if (_charactorMainPanel != null)
            {
                var controller = _charactorMainPanel.GetComponent<CharactorMainPanelController>();
                if (controller == null)
                    controller = _charactorMainPanel.AddComponent<CharactorMainPanelController>();

                controller.RefreshFromCurrentState();
            }

            RefreshCharactorMassagePanel();
        }

        private void RefreshCharactorMassagePanel()
        {
            if (_charactorMassagePanel == null)
                return;

            var controller = _charactorMassagePanel.GetComponent<CharactorMassagePanelController>();
            if (controller == null)
                controller = _charactorMassagePanel.AddComponent<CharactorMassagePanelController>();

            controller.RefreshFromCurrentState();
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
            if (newScene.name == SceneLoader.SceneGame)
            {
                ShowCharactorMainPanel();
                return;
            }

            HideBagPanel();
            HideCharactorMainPanel();
        }

        // ── 内部辅助 ──────────────────────────────────────────────────

        private void HandleBagHotkey()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.iKey.wasPressedThisFrame)
                return;

            TryToggleBagPanel();
        }

        private void HandleCharactorMassageHotkey()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.cKey.wasPressedThisFrame)
                return;

            TryToggleCharactorMassagePanel();
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

        private void ConfigureCharactorMainPanel(GameObject panel)
        {
            if (panel == null)
                return;

            if (_rootCanvas == null)
                EnsureRootCanvas();

            panel.transform.SetParent(_rootCanvas.transform, false);

            var canvas = panel.GetComponent<Canvas>();
            if (canvas == null)
                canvas = panel.AddComponent<Canvas>();

            canvas.overrideSorting = true;
            canvas.sortingOrder = CharactorMainPanelSortingOrder;

            if (panel.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
                panel.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            panel.transform.SetAsLastSibling();
        }

        private void ConfigureCharactorMassagePanel(GameObject panel)
        {
            if (panel == null)
                return;

            if (_rootCanvas == null)
                EnsureRootCanvas();

            panel.transform.SetParent(_rootCanvas.transform, false);

            var canvas = panel.GetComponent<Canvas>();
            if (canvas == null)
                canvas = panel.AddComponent<Canvas>();

            canvas.overrideSorting = true;
            canvas.sortingOrder = CharactorMainPanelSortingOrder;

            if (panel.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
                panel.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            panel.transform.SetAsLastSibling();
        }

        private void EnsureTooltipOverlay()
        {
            if (_tooltipOverlay != null)
            {
                _tooltipOverlay.transform.SetAsLastSibling();
                return;
            }

            if (_rootCanvas == null)
                return;

            var go = new GameObject("UITooltipOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            go.transform.SetParent(_rootCanvas.transform, false);
            go.layer = _rootCanvas.gameObject.layer;

            _tooltipOverlay = go.GetComponent<RectTransform>();
            _tooltipOverlay.anchorMin = Vector2.zero;
            _tooltipOverlay.anchorMax = Vector2.one;
            _tooltipOverlay.pivot = new Vector2(0.5f, 0.5f);
            _tooltipOverlay.offsetMin = Vector2.zero;
            _tooltipOverlay.offsetMax = Vector2.zero;

            var canvas = go.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingLayerID = _rootCanvas.sortingLayerID;
            canvas.sortingOrder = TooltipOverlaySortingOrder;

            var canvasGroup = go.GetComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            canvasGroup.ignoreParentGroups = true;

            go.transform.SetAsLastSibling();
            Debug.Log("[UIManager] 已创建 TooltipOverlay 最高层 Canvas");
        }

        private void RegisterCharactorMainPanelOccluder()
        {
            if (_charactorMainPanel == null) return;

            var rectTransform = _charactorMainPanel.GetComponent<RectTransform>();
            if (rectTransform != null)
                UIGamePanelManager.RegisterOccluder(rectTransform);
        }

        private void UnregisterCharactorMainPanelOccluder()
        {
            if (_charactorMainPanel == null) return;

            var rectTransform = _charactorMainPanel.GetComponent<RectTransform>();
            if (rectTransform != null)
                UIGamePanelManager.UnregisterOccluder(rectTransform);
        }

        private void RegisterCharactorMassagePanelOccluder()
        {
            if (_charactorMassagePanel == null)
                return;

            var rectTransform = _charactorMassagePanel.GetComponent<RectTransform>();
            if (rectTransform != null)
                UIGamePanelManager.RegisterOccluder(rectTransform);
        }

        private void UnregisterCharactorMassagePanelOccluder()
        {
            if (_charactorMassagePanel == null)
                return;

            var rectTransform = _charactorMassagePanel.GetComponent<RectTransform>();
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
            EnsureTooltipOverlay();
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