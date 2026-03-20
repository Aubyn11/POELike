using UnityEngine;
using POELike.ECS.Core;
using POELike.ECS.Systems;
using POELike.Managers;

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

            InitializeECS();
            InitializeUI();
        }

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
            World.RegisterSystem(new MovementSystem());     // 优先级 100
            World.RegisterSystem(new SkillSystem());        // 优先级 150
            World.RegisterSystem(new CombatSystem());       // 优先级 200
            
            // 初始化世界
            World.Initialize();
            
            Debug.Log("[GameManager] ECS世界初始化完成");
        }
        
        private void Update()
        {
            World?.Update(Time.deltaTime);
        }
        
        private void FixedUpdate()
        {
            World?.FixedUpdate(Time.fixedDeltaTime);
        }
        
        private void OnDestroy()
        {
            if (Instance == this)
            {
                World?.Dispose();
                Instance = null;
            }
        }
        
        private void OnGUI()
        {
            if (!_showDebugInfo) return;
            
            GUILayout.BeginArea(new Rect(10, 10, 200, 100));
            GUILayout.Label($"实体数量: {World?.EntityCount ?? 0}");
            GUILayout.Label($"FPS: {(1f / Time.deltaTime):F1}");
            GUILayout.EndArea();
        }
    }
}
