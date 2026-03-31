using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using POELike.ECS.Core;
using POELike.ECS.Components;
using POELike.Game;

namespace POELike.Game.UI
{
    /// <summary>
    /// 游戏内 GM 面板（IMGUI 实现，无需预制体）
    /// 按 F1 键切换显示/隐藏
    ///
    /// 功能：
    ///   - 输入 MonsterID 和数量，点击「生成」按钮在玩家附近创建怪物实体
    ///   - 点击「清除所有怪物」销毁当前由 GM 生成的怪物
    ///   - 不再展示全量怪物详情，避免打开/关闭面板时卡顿
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
        private string _entityIdInput  = "";

        // ── 面板尺寸 ──────────────────────────────────────────────────
        private Rect _windowRect = new Rect(10f, 10f, 300f, 300f);

        // ── 快捷键 ────────────────────────────────────────────────────
        private InputAction _toggleAction;

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
        }

        /// <summary>
        /// 销毁当前由 GM 生成的所有怪物
        /// </summary>
        public void DestroyAllSpawnedMonsters(bool updateStatus = true)
        {
            if (_world == null)
            {
                if (updateStatus)
                    _statusMsg = "❌ 未初始化（world 为空）";
                return;
            }

            CompactMonsterEntities();

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

            if (updateStatus)
                _statusMsg = count > 0 ? $"🗑 已清除 {count} 只怪物" : "🗑 当前没有可清除的怪物";
        }

        // ── 每帧更新 ──────────────────────────────────────────────────

        private void Update()
        {
            if (_toggleAction.WasPressedThisFrame())
            {
                _isVisible = !_isVisible;
                if (_isVisible)
                    CompactMonsterEntities();
            }
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

            GUILayout.Space(6f);
            GUILayout.Label("仅保留生成 / 销毁功能，已移除全量怪物详情显示。");
            GUILayout.Label($"当前 GM 管理怪物：{_monsterEntities.Count} 只");

            GUILayout.Space(6f);

            // ── 实体销毁区域 ──────────────────────────────────────────
            GUILayout.Label("── 销毁实体 ──────────────────");

            GUILayout.BeginHorizontal();
            GUILayout.Label("实体ID:", GUILayout.Width(80f));
            _entityIdInput = GUILayout.TextField(_entityIdInput, GUILayout.Width(80f));
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);

            // ── 生成按钮 ──────────────────────────────────────────────
            if (GUILayout.Button("生成怪物", GUILayout.Height(28f)))
                OnSpawnClicked();

            if (GUILayout.Button("销毁实体", GUILayout.Height(28f)))
                OnDestroyEntityClicked();

            // ── 清除按钮 ──────────────────────────────────────────────
            if (GUILayout.Button("清除所有怪物", GUILayout.Height(28f)))
                OnClearClicked();

            // ── 状态消息 ──────────────────────────────────────────────
            if (!string.IsNullOrEmpty(_statusMsg))
            {
                GUILayout.Space(4f);
                GUILayout.Label(_statusMsg);
            }

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

            CompactMonsterEntities();

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
            DestroyAllSpawnedMonsters();
        }

        private void OnDestroyEntityClicked()
        {
            if (_world == null)
            {
                _statusMsg = "❌ 未初始化（world 为空）";
                return;
            }

            if (!int.TryParse(_entityIdInput.Trim(), out int entityId) || entityId < 0)
            {
                _statusMsg = "❌ 实体ID 格式错误";
                return;
            }

            var entity = _world.FindEntityById(entityId);
            if (entity == null)
            {
                _statusMsg = $"❌ 未找到实体 ID={entityId}";
                return;
            }

            _world.DestroyEntity(entity);
            _monsterEntities.Remove(entity);
            _statusMsg = $"🗑 已销毁实体 ID={entityId}（Tag={entity.Tag}）";
        }

        private void CompactMonsterEntities()
        {
            _monsterEntities.RemoveAll(e => e == null || !e.IsAlive);
        }
    }
}