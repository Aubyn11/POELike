using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using POELike.ECS.Core;
using POELike.ECS.Components;

namespace POELike.Game.UI
{
    /// <summary>
    /// 游戏内 GM 面板（IMGUI 实现，无需预制体）
    /// 按 F1 键切换显示/隐藏
    ///
    /// 功能：
    ///   - 输入 MonsterID 和数量，点击「生成」按钮在玩家附近创建怪物实体
    ///   - 显示当前已生成的怪物列表（ID、Mesh、HP）
    ///   - 支持「清除所有怪物」
    /// </summary>
    public class GMPanel : MonoBehaviour
    {
        // ── 外部依赖（由 GameSceneManager 注入）──────────────────────
        private World  _world;
        private Entity _playerEntity;

        // ── 怪物实体列表（由本面板管理）──────────────────────────────
        private readonly List<Entity> _monsterEntities = new List<Entity>();

        // ── IMGUI 状态 ────────────────────────────────────────────────
        private bool   _isVisible   = false;
        private string _monsterIdInput = "1001";
        private string _countInput     = "1";
        private string _statusMsg      = "";
        private Vector2 _scrollPos     = Vector2.zero;

        // ── 面板尺寸 ──────────────────────────────────────────────────
        private Rect _windowRect = new Rect(10f, 10f, 320f, 420f);

        // ── 快捷键 ────────────────────────────────────────────────────
        private InputAction _toggleAction;

        // ── 配置缓存（用于显示名称）──────────────────────────────────
        private Dictionary<int, string> _meshNameCache;

        // ── 初始化 ────────────────────────────────────────────────────

        private void Awake()
        {
            _toggleAction = new InputAction("GMToggle", InputActionType.Button, "<Keyboard>/f1");
            _toggleAction.Enable();
        }

        private void OnDestroy()
        {
            _toggleAction?.Dispose();
        }

        // ── 公开接口 ──────────────────────────────────────────────────

        /// <summary>
        /// 由 GameSceneManager 调用，注入 ECS 世界和玩家实体
        /// </summary>
        public void Init(World world, Entity playerEntity)
        {
            _world         = world;
            _playerEntity  = playerEntity;

            // 预加载配置名称缓存
            _meshNameCache = new Dictionary<int, string>();
            var configs = MonsterSpawner.GetAllConfigs();
            foreach (var kv in configs)
                _meshNameCache[kv.Key] = kv.Value.MonsterMesh;
        }

        /// <summary>
        /// 获取当前由 GM 生成的怪物实体列表（供 GameSceneManager 管理生命周期）
        /// </summary>
        public List<Entity> GetMonsterEntities() => _monsterEntities;

        // ── 每帧更新 ──────────────────────────────────────────────────

        private void Update()
        {
            if (_toggleAction.WasPressedThisFrame())
                _isVisible = !_isVisible;

            // 清理已销毁的实体引用
            _monsterEntities.RemoveAll(e => e == null || !e.IsAlive);
        }

        // ── IMGUI 渲染 ────────────────────────────────────────────────

        private void OnGUI()
        {
            if (!_isVisible) return;

            _windowRect = GUI.Window(9999, _windowRect, DrawWindow, "GM 面板  [F1 关闭]");
        }

        private void DrawWindow(int windowId)
        {
            GUILayout.Space(4f);

            // ── 生成怪物区域 ──────────────────────────────────────────
            GUILayout.Label("── 生成怪物 ──────────────────");

            GUILayout.BeginHorizontal();
            GUILayout.Label("MonsterID:", GUILayout.Width(80f));
            _monsterIdInput = GUILayout.TextField(_monsterIdInput, GUILayout.Width(80f));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("数量:", GUILayout.Width(80f));
            _countInput = GUILayout.TextField(_countInput, GUILayout.Width(80f));
            GUILayout.EndHorizontal();

            GUILayout.Space(4f);

            // ── 可用 ID 提示 ──────────────────────────────────────────
            if (_meshNameCache != null && _meshNameCache.Count > 0)
            {
                GUILayout.Label("可用 ID：");
                foreach (var kv in _meshNameCache)
                    GUILayout.Label($"  {kv.Key}  →  {kv.Value}", GUI.skin.label);
            }

            GUILayout.Space(6f);

            // ── 生成按钮 ──────────────────────────────────────────────
            if (GUILayout.Button("生成怪物", GUILayout.Height(28f)))
                OnSpawnClicked();

            // ── 清除按钮 ──────────────────────────────────────────────
            if (GUILayout.Button("清除所有怪物", GUILayout.Height(28f)))
                OnClearClicked();

            // ── 状态消息 ──────────────────────────────────────────────
            if (!string.IsNullOrEmpty(_statusMsg))
            {
                GUILayout.Space(4f);
                GUILayout.Label(_statusMsg);
            }

            // ── 怪物列表 ──────────────────────────────────────────────
            GUILayout.Space(6f);
            GUILayout.Label($"── 当前怪物（{_monsterEntities.Count} 只）──────");

            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(120f));
            foreach (var entity in _monsterEntities)
            {
                if (entity == null || !entity.IsAlive) continue;
                var mc = entity.GetComponent<MonsterComponent>();
                var hc = entity.GetComponent<HealthComponent>();
                var tc = entity.GetComponent<TransformComponent>();
                if (mc == null) continue;

                float hp    = hc != null ? hc.CurrentHealth : 0f;
                float maxHp = hc != null ? hc.MaxHealth : mc.MaxHp;
                string pos  = tc != null ? $"({tc.Position.x:F1},{tc.Position.z:F1})" : "";
                GUILayout.Label($"ID={mc.MonsterID} {mc.MonsterMesh}  HP:{hp:F0}/{maxHp:F0}  {pos}");
            }
            GUILayout.EndScrollView();

            // ── 允许拖动窗口 ──────────────────────────────────────────
            GUI.DragWindow(new Rect(0f, 0f, _windowRect.width, 20f));
        }

        // ── 按钮逻辑 ──────────────────────────────────────────────────

        private void OnSpawnClicked()
        {
            if (_world == null)
            {
                _statusMsg = "❌ 未初始化（world 为空）";
                return;
            }

            if (!int.TryParse(_monsterIdInput.Trim(), out int monsterId) || monsterId <= 0)
            {
                _statusMsg = "❌ MonsterID 格式错误";
                return;
            }

            if (!int.TryParse(_countInput.Trim(), out int count) || count <= 0)
            {
                _statusMsg = "❌ 数量格式错误";
                return;
            }

            // 获取玩家位置作为生成中心
            Vector3 center = Vector3.zero;
            if (_playerEntity != null && _playerEntity.IsAlive)
            {
                var tc = _playerEntity.GetComponent<TransformComponent>();
                if (tc != null) center = tc.Position;
            }

            var newEntities = MonsterSpawner.SpawnMonsters(_world, monsterId, count, center);
            _monsterEntities.AddRange(newEntities);

            if (newEntities.Count > 0)
                _statusMsg = $"✅ 成功生成 {newEntities.Count} 只（ID={monsterId}）";
            else
                _statusMsg = $"❌ 生成失败，请检查 MonsterID={monsterId} 是否存在";
        }

        private void OnClearClicked()
        {
            if (_world == null) return;

            int count = 0;
            foreach (var entity in _monsterEntities)
            {
                if (entity != null && entity.IsAlive)
                {
                    _world.DestroyEntity(entity);
                    count++;
                }
            }
            _monsterEntities.Clear();
            _statusMsg = $"🗑 已清除 {count} 只怪物";
        }
    }
}
