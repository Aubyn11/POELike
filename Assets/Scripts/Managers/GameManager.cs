using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using POELike.ECS.Core;
using POELike.ECS.Systems;
using POELike.Managers;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace POELike.Managers
{
    /// <summary>
    /// 游戏管理器
    /// Unity入口点，负责初始化ECS世界和所有系统
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        // 单例
        public static GameManager Instance { get; private set; }
        
        [Header("游戏设置")]
        [SerializeField] private bool _showDebugInfo = true;
        
        /// <summary>
        /// ECS世界引用
        /// </summary>
        public World World { get; private set; }

        /// <summary>MovementSystem 引用（供 MonsterMeshRenderer 直读 GPU 位置 Buffer）</summary>
        public MovementSystem MovementSystem { get; private set; }

        // 调试信息限频缓存（每0.2秒更新一次，避免IMGUI每帧重建Mesh）
        private string _debugEntityCount = "";
        private string _debugFps         = "";
        private float  _debugTimer       = 0f;
        private int    _debugFrameCount  = 0;
        private float  _debugFrameTimeSum = 0f;
        private const float DebugUpdateInterval = 0.2f;

#if UNITY_EDITOR
        private bool _hasCachedEditorInputSettings;
        private InputSettings.UpdateMode _cachedUpdateMode;
        private InputSettings.BackgroundBehavior _cachedBackgroundBehavior;
        private InputSettings.EditorInputBehaviorInPlayMode _cachedEditorInputBehavior;
#endif
        
        private void Awake()
        {
            // 单例处理
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            SceneManager.sceneLoaded += OnSceneLoaded;

            ConfigureInputSystem();
            InitializeECS();
            InitializeUI();
            FocusGameViewInEditor();
        }

        private void ConfigureInputSystem()
        {
            var inputSettings = InputSystem.settings;
            if (inputSettings != null)
            {
#if UNITY_EDITOR
                if (!_hasCachedEditorInputSettings)
                {
                    _cachedUpdateMode = inputSettings.updateMode;
                    _cachedBackgroundBehavior = inputSettings.backgroundBehavior;
                    _cachedEditorInputBehavior = inputSettings.editorInputBehaviorInPlayMode;
                    _hasCachedEditorInputSettings = true;
                }
#endif

                // 当前项目的所有键盘轮询都发生在 Update 里，因此必须使用 Dynamic Update。
                inputSettings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;

#if UNITY_EDITOR
                // 解决 Editor 下 GameView 未聚焦时键盘热键（如 F1 / I）不进入游戏的问题。
                inputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                inputSettings.editorInputBehaviorInPlayMode =
                    InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            }

            // 确保项目级 Input Actions 资源处于启用状态，避免 Move / Attack 一直读到默认值。
            InputSystem.actions?.Enable();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            FocusGameViewInEditor();
        }

#if UNITY_EDITOR
        private void FocusGameViewInEditor()
        {
            EditorApplication.delayCall -= FocusGameViewWindow;
            EditorApplication.delayCall += FocusGameViewWindow;
        }

        private void FocusGameViewWindow()
        {
            var gameViewType = Type.GetType("UnityEditor.GameView, UnityEditor");
            if (gameViewType == null)
                return;

            var gameViewWindow = EditorWindow.GetWindow(gameViewType);
            gameViewWindow?.Focus();
        }

        private void RestoreEditorInputSystemSettings()
        {
            EditorApplication.delayCall -= FocusGameViewWindow;

            if (!_hasCachedEditorInputSettings)
                return;

            var inputSettings = InputSystem.settings;
            if (inputSettings != null)
            {
                inputSettings.updateMode = _cachedUpdateMode;
                inputSettings.backgroundBehavior = _cachedBackgroundBehavior;
                inputSettings.editorInputBehaviorInPlayMode = _cachedEditorInputBehavior;
            }

            _hasCachedEditorInputSettings = false;
        }
#endif

        private void InitializeUI()
        {
            // 确保 UIManager 存在（若场景中已挂载则复用，否则自动创建）
            if (UIManager.Instance == null)
            {
                var uiManagerGo = new GameObject("UIManager");
                DontDestroyOnLoad(uiManagerGo);
                uiManagerGo.AddComponent<UIManager>();
            }

            // 游戏启动后显示角色选择面板
            UIManager.Instance.ShowCharacterSelectPanel();
            Debug.Log("[GameManager] UI初始化完成，已显示角色选择面板");
        }
        
        private void InitializeECS()
        {
            // 创建ECS世界
            World = World.Instance;
            
            // 注册所有系统（按优先级自动排序）
            World.RegisterSystem(new StatsSystem());        // 优先级 10
            World.RegisterSystem(new AISystem());           // 优先级 50
            MovementSystem = new MovementSystem();
            World.RegisterSystem(MovementSystem);           // 优先级 100
            World.RegisterSystem(new SkillSystem());        // 优先级 150
            World.RegisterSystem(new SkillGpuSystem());     // 优先级 170
            World.RegisterSystem(new CombatSystem());       // 优先级 200

            
            // 初始化世界
            World.Initialize();
            
            Debug.Log("[GameManager] ECS世界初始化完成");
        }
        
        private void Update()
        {
            World?.Update(Time.deltaTime);

            // 限频更新调试文本（每0.2秒一次，避免IMGUI每帧重建Mesh）
            if (_showDebugInfo)
            {
                float frameDelta = Time.unscaledDeltaTime;
                _debugTimer += frameDelta;
                _debugFrameCount++;
                _debugFrameTimeSum += frameDelta;

                if (_debugTimer >= DebugUpdateInterval)
                {
                    float averageDelta = _debugFrameCount > 0
                        ? _debugFrameTimeSum / _debugFrameCount
                        : 0f;
                    float fps = averageDelta > 0.0001f ? 1f / averageDelta : 0f;

                    _debugTimer = 0f;
                    _debugFrameCount = 0;
                    _debugFrameTimeSum = 0f;
                    _debugEntityCount = $"实体数量: {World?.EntityCount ?? 0}";
                    _debugFps         = $"FPS: {fps:F1} ({averageDelta * 1000f:F2} ms)";
                }
            }
        }
        
        private void FixedUpdate()
        {
            World?.FixedUpdate(Time.fixedDeltaTime);
        }
        
        private void OnDestroy()
        {
            if (Instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
#if UNITY_EDITOR
                RestoreEditorInputSystemSettings();
#endif
                World?.Dispose();
                Instance = null;
            }
        }
        
        private void OnGUI()
        {
            if (!_showDebugInfo) return;
            
            GUILayout.BeginArea(new Rect(10, 10, 260, 100));
            GUILayout.Label(_debugEntityCount);
            GUILayout.Label(_debugFps);
            GUILayout.EndArea();
        }
    }
}
