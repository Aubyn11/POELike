using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using POELike.ECS.Core;
using POELike.ECS.Components;
using POELike.Game;
using POELike.Game.Equipment;
using POELike.Game.Items;
using POELike.Game.Skills;
using POELike.Managers;

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
        private enum GMDropdown
        {
            None,
            EquipmentDetail,
            Prefix,
            Suffix,
            GemContent,
        }

        private static readonly SocketColor[] SocketColorOptions =
        {
            SocketColor.Red,
            SocketColor.Green,
            SocketColor.Blue,
            SocketColor.White,
        };

        private static readonly string[] SocketColorOptionLabels = { "红", "绿", "蓝", "白" };
        private static readonly string[] GemTypeOptionLabels = { "主动宝石", "辅助宝石" };
        private static readonly string[] LinkStateOptionLabels = { "已连", "未连" };

        private static GMPanel _activeInstance;

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
        private Vector2 _panelScrollPosition;
        private Vector2 _equipmentDropdownScroll;
        private Vector2 _prefixDropdownScroll;
        private Vector2 _suffixDropdownScroll;
        private Vector2 _gemDropdownScroll;
        private GMDropdown _openDropdown = GMDropdown.None;
        private bool _hasInitializedSelections;
        private EquipmentDetailTypeData _selectedEquipmentDetail;
        private readonly List<EquipmentModData> _selectedPrefixes = new List<EquipmentModData>();
        private readonly List<EquipmentModData> _selectedSuffixes = new List<EquipmentModData>();
        private readonly List<SocketColor> _selectedSocketColors = new List<SocketColor>();
        private readonly List<bool> _selectedSocketLinks = new List<bool>();
        private int _selectedSocketCount = 3;
        private ActiveSkillStoneConfigData _selectedActiveGem;
        private SupportSkillStoneConfigData _selectedSupportGem;
        private SocketColor _selectedGemColor = SocketColor.Blue;
        private bool _generateSupportGem;

        // ── 面板尺寸 ──────────────────────────────────────────────────
        private Rect _windowRect = new Rect(10f, 10f, 560f, 760f);

        // ── 快捷键 ────────────────────────────────────────────────────
        private InputAction _toggleAction;
        private int _lastToggleFrame = -1;

        // ── 初始化 ────────────────────────────────────────────────────

        private void Awake()
        {
            _activeInstance = this;
            _toggleAction = new InputAction("GMToggle", InputActionType.Button, "<Keyboard>/f1");
            _toggleAction.performed += OnTogglePerformed;
            _toggleAction.Enable();
            EnsureSelectionStateInitialized();
        }

        private void OnDestroy()
        {
            if (_activeInstance == this)
                _activeInstance = null;

            if (_toggleAction != null)
            {
                _toggleAction.performed -= OnTogglePerformed;
                _toggleAction.Dispose();
            }
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

        public static bool TryGetVisibleScreenRect(out Rect screenRect)
        {
            screenRect = default;

            if (_activeInstance == null || !_activeInstance._isVisible)
                return false;

            var guiRect = _activeInstance._windowRect;
            screenRect = new Rect(
                guiRect.xMin,
                Screen.height - guiRect.yMax,
                guiRect.width,
                guiRect.height);

            return true;
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
            if (_toggleAction != null && _toggleAction.WasPressedThisFrame())
                ToggleVisibility();
        }

        private void OnTogglePerformed(InputAction.CallbackContext context)
        {
            ToggleVisibility();
        }

        private void ToggleVisibility()
        {
            if (_lastToggleFrame == Time.frameCount)
                return;

            _lastToggleFrame = Time.frameCount;
            _isVisible = !_isVisible;
            _openDropdown = GMDropdown.None;
            if (_isVisible)
            {
                CompactMonsterEntities();
                EnsureSelectionStateInitialized();
            }
        }

        // ── IMGUI 渲染 ────────────────────────────────────────────────

        private void OnGUI()
        {
            var currentEvent = Event.current;
            if (currentEvent != null && currentEvent.type == EventType.KeyDown && currentEvent.keyCode == KeyCode.F1)
            {
                ToggleVisibility();
                currentEvent.Use();
            }

            if (!_isVisible) return;

            _windowRect = GUI.Window(9999, _windowRect, DrawWindow, "GM 面板  [F1 关闭]");
        }

        private void DrawWindow(int windowId)
        {
            EnsureSelectionStateInitialized();

            _panelScrollPosition = GUILayout.BeginScrollView(
                _panelScrollPosition,
                false,
                true,
                GUILayout.Width(_windowRect.width - 16f),
                GUILayout.Height(_windowRect.height - 38f));

            GUILayout.Space(4f);
            DrawMonsterSection();

            GUILayout.Space(8f);
            DrawEquipmentSection();

            GUILayout.Space(8f);
            DrawGemSection();

            GUILayout.Space(8f);
            DrawDestroySection();

            if (!string.IsNullOrEmpty(_statusMsg))
            {
                GUILayout.Space(6f);
                GUILayout.Label(_statusMsg);
            }

            GUILayout.EndScrollView();
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

        private void OnGenerateEquipmentClicked()
        {
            EnsureSelectionStateInitialized();

            if (_selectedEquipmentDetail == null)
            {
                _statusMsg = "❌ 当前没有可用的装备基础";
                return;
            }

            if (!GMItemFactory.TryCreateEquipment(
                    _selectedEquipmentDetail.EquipmentDetailTypeId,
                    BuildModQuery(_selectedPrefixes),
                    BuildModQuery(_selectedSuffixes),
                    _selectedSocketCount,
                    BuildSocketColorInput(),
                    BuildLinkInput(),
                    out var bagItem,
                    out var error))
            {
                _statusMsg = $"❌ 生成装备失败：{error}";
                return;
            }

            if (!TryAddBagItem(bagItem, out error))
            {
                _statusMsg = $"❌ 装备入包失败：{error}";
                return;
            }

            _statusMsg = $"✅ 已生成装备：{bagItem.Name}";
        }

        private void OnGenerateGemClicked()
        {
            EnsureSelectionStateInitialized();

            string gemQuery = GetCurrentGemQuery();
            if (string.IsNullOrWhiteSpace(gemQuery))
            {
                _statusMsg = _generateSupportGem ? "❌ 当前没有可选辅助宝石" : "❌ 当前没有可选主动宝石";
                return;
            }

            if (!GMItemFactory.TryCreateGem(
                    gemQuery,
                    ResolveSocketColorToken(_selectedGemColor),
                    _generateSupportGem,
                    out var bagItem,
                    out var error))
            {
                _statusMsg = $"❌ 生成宝石失败：{error}";
                return;
            }

            if (!TryAddBagItem(bagItem, out error))
            {
                _statusMsg = $"❌ 宝石入包失败：{error}";
                return;
            }

            _statusMsg = $"✅ 已生成宝石：{bagItem.Name}";
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

        private bool TryAddBagItem(BagItemData bagItem, out string error)
        {
            error = string.Empty;
            var uiManager = UIManager.Instance;
            if (uiManager == null)
            {
                error = "UIManager 未初始化";
                return false;
            }

            var bagPanel = uiManager.GetOrCreateBagPanel(true);
            if (bagPanel == null)
            {
                error = "当前背包面板不可用";
                return false;
            }

            return bagPanel.TryAddItemToBag(bagItem, out error);
        }

        private void DrawMonsterSection()
        {
            GUILayout.Label("── 生成怪物 ──────────────────");

            GUILayout.BeginHorizontal();
            GUILayout.Label("MonsterID:", GUILayout.Width(110f));
            _monsterIdInput = GUILayout.TextField(_monsterIdInput, GUILayout.Width(120f));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("数量:", GUILayout.Width(110f));
            _countInput = GUILayout.TextField(_countInput, GUILayout.Width(120f));
            GUILayout.EndHorizontal();

            GUILayout.Space(4f);
            GUILayout.Label($"当前 GM 管理怪物：{_monsterEntities.Count} 只");

            if (GUILayout.Button("生成怪物", GUILayout.Height(28f)))
                OnSpawnClicked();
        }

        private void DrawEquipmentSection()
        {
            GUILayout.Label("── 生成装备 ──────────────────");
            DrawDropdownButton("装备基础:", GetEquipmentDetailDisplayName(_selectedEquipmentDetail), GMDropdown.EquipmentDetail);
            if (_openDropdown == GMDropdown.EquipmentDetail)
                DrawEquipmentDetailDropdown();

            DrawSelectedModList("已选前缀", _selectedPrefixes);
            if (_selectedPrefixes.Count < 3)
            {
                DrawDropdownButton("添加前缀:", "点击选择前缀", GMDropdown.Prefix);
                if (_openDropdown == GMDropdown.Prefix)
                    DrawModDropdown(GMItemFactory.GetAvailablePrefixMods(_selectedEquipmentDetail), _selectedPrefixes, ref _prefixDropdownScroll);
            }

            DrawSelectedModList("已选后缀", _selectedSuffixes);
            if (_selectedSuffixes.Count < 3)
            {
                DrawDropdownButton("添加后缀:", "点击选择后缀", GMDropdown.Suffix);
                if (_openDropdown == GMDropdown.Suffix)
                    DrawModDropdown(GMItemFactory.GetAvailableSuffixMods(_selectedEquipmentDetail), _selectedSuffixes, ref _suffixDropdownScroll);
            }

            DrawSocketCountSelector();
            DrawSocketColorSelectors();
            DrawLinkSelectors();

            if (GUILayout.Button("生成装备到背包", GUILayout.Height(28f)))
                OnGenerateEquipmentClicked();
        }

        private void DrawGemSection()
        {
            GUILayout.Label("── 生成宝石 ──────────────────");

            GUILayout.BeginHorizontal();
            GUILayout.Label("宝石类型:", GUILayout.Width(110f));
            int currentGemTypeIndex = _generateSupportGem ? 1 : 0;
            int selectedGemTypeIndex = GUILayout.Toolbar(currentGemTypeIndex, GemTypeOptionLabels, GUILayout.Width(300f));
            GUILayout.EndHorizontal();

            if (selectedGemTypeIndex != currentGemTypeIndex)
            {
                _generateSupportGem = selectedGemTypeIndex == 1;
                _openDropdown = GMDropdown.None;

                if (_generateSupportGem)
                {
                    if (_selectedSupportGem == null)
                        _selectedSupportGem = FindFirstSupportGem();
                    _selectedGemColor = SocketColor.Green;
                }
                else
                {
                    if (_selectedActiveGem == null)
                        _selectedActiveGem = FindFirstActiveGem();
                    if (_selectedActiveGem != null)
                        _selectedGemColor = GMItemFactory.GetDefaultGemColorForActiveSkill(_selectedActiveGem);
                }
            }

            DrawDropdownButton("宝石内容:", GetCurrentGemDisplayName(), GMDropdown.GemContent);
            if (_openDropdown == GMDropdown.GemContent)
                DrawGemDropdown();

            _selectedGemColor = DrawSocketColorToolbar("宝石颜色:", _selectedGemColor, 300f);

            if (GUILayout.Button("生成宝石到背包", GUILayout.Height(28f)))
                OnGenerateGemClicked();
        }

        private void DrawDestroySection()
        {
            GUILayout.Label("── 销毁实体 ──────────────────");

            GUILayout.BeginHorizontal();
            GUILayout.Label("实体ID:", GUILayout.Width(110f));
            _entityIdInput = GUILayout.TextField(_entityIdInput, GUILayout.Width(120f));
            GUILayout.EndHorizontal();

            GUILayout.Space(4f);
            if (GUILayout.Button("销毁实体", GUILayout.Height(28f)))
                OnDestroyEntityClicked();

            if (GUILayout.Button("清除所有怪物", GUILayout.Height(28f)))
                OnClearClicked();
        }

        private void DrawDropdownButton(string label, string currentValue, GMDropdown dropdown)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(110f));
            string arrow = _openDropdown == dropdown ? "▲" : "▼";
            if (GUILayout.Button($"{currentValue}  {arrow}", GUILayout.Width(380f)))
                ToggleDropdown(dropdown);
            GUILayout.EndHorizontal();
        }

        private void DrawEquipmentDetailDropdown()
        {
            GUILayout.BeginVertical("box");
            _equipmentDropdownScroll = GUILayout.BeginScrollView(_equipmentDropdownScroll, GUILayout.Height(180f));

            var details = EquipmentConfigLoader.DetailTypes;
            for (int i = 0; i < details.Count; i++)
            {
                var detail = details[i];
                if (detail == null)
                    continue;

                if (GUILayout.Button(GetEquipmentDetailDisplayName(detail), GUILayout.Height(24f)))
                {
                    SelectEquipmentDetail(detail);
                    _openDropdown = GMDropdown.None;
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawModDropdown(IReadOnlyList<EquipmentModData> availableMods, List<EquipmentModData> selectedMods, ref Vector2 scrollPosition)
        {
            GUILayout.BeginVertical("box");
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(160f));

            bool hasOption = false;
            for (int i = 0; i < availableMods.Count; i++)
            {
                var mod = availableMods[i];
                if (mod == null || IsModSelected(selectedMods, mod))
                    continue;

                hasOption = true;
                if (GUILayout.Button(GetModDisplayName(mod), GUILayout.Height(24f)))
                {
                    selectedMods.Add(mod);
                    _openDropdown = GMDropdown.None;
                }
            }

            if (!hasOption)
                GUILayout.Label("当前没有更多可选词缀");

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawGemDropdown()
        {
            GUILayout.BeginVertical("box");
            _gemDropdownScroll = GUILayout.BeginScrollView(_gemDropdownScroll, GUILayout.Height(180f));

            if (_generateSupportGem)
            {
                var supportSkills = SkillConfigLoader.SupportSkills;
                bool hasOption = false;
                for (int i = 0; i < supportSkills.Count; i++)
                {
                    var supportSkill = supportSkills[i];
                    if (supportSkill == null)
                        continue;

                    hasOption = true;
                    if (GUILayout.Button(GetSupportGemDisplayName(supportSkill), GUILayout.Height(24f)))
                    {
                        SelectSupportGem(supportSkill);
                        _openDropdown = GMDropdown.None;
                    }
                }

                if (!hasOption)
                    GUILayout.Label("当前没有可用的辅助宝石配置");
            }
            else
            {
                var activeSkills = SkillConfigLoader.ActiveSkills;
                bool hasOption = false;
                for (int i = 0; i < activeSkills.Count; i++)
                {
                    var activeSkill = activeSkills[i];
                    if (activeSkill == null)
                        continue;

                    hasOption = true;
                    if (GUILayout.Button(GetActiveGemDisplayName(activeSkill), GUILayout.Height(24f)))
                    {
                        SelectActiveGem(activeSkill);
                        _openDropdown = GMDropdown.None;
                    }
                }

                if (!hasOption)
                    GUILayout.Label("当前没有可用的主动宝石配置");
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawSelectedModList(string label, List<EquipmentModData> selectedMods)
        {
            GUILayout.Label($"{label}（{selectedMods.Count}/3）");
            if (selectedMods.Count == 0)
            {
                GUILayout.Label("暂无");
                return;
            }

            int removeIndex = -1;
            for (int i = 0; i < selectedMods.Count; i++)
            {
                var mod = selectedMods[i];
                GUILayout.BeginHorizontal();
                GUILayout.Label(GetModDisplayName(mod), GUILayout.Width(320f));
                if (GUILayout.Button("移除", GUILayout.Width(60f)))
                    removeIndex = i;
                GUILayout.EndHorizontal();
            }

            if (removeIndex >= 0 && removeIndex < selectedMods.Count)
                selectedMods.RemoveAt(removeIndex);
        }

        private void DrawSocketCountSelector()
        {
            int maxSocketCount = _selectedEquipmentDetail == null ? 0 : GMItemFactory.GetMaxSocketCountForDetail(_selectedEquipmentDetail);
            GUILayout.BeginHorizontal();
            GUILayout.Label("孔数:", GUILayout.Width(110f));

            if (maxSocketCount <= 0)
            {
                GUILayout.Label("当前装备不支持孔");
                GUILayout.EndHorizontal();
                return;
            }

            int selectedCount = GUILayout.SelectionGrid(
                _selectedSocketCount,
                BuildSocketCountLabels(maxSocketCount),
                Mathf.Min(4, maxSocketCount + 1),
                GUILayout.Width(300f));

            if (selectedCount != _selectedSocketCount)
            {
                _selectedSocketCount = selectedCount;
                NormalizeSocketSelections();
            }

            GUILayout.EndHorizontal();
        }

        private void DrawSocketColorSelectors()
        {
            if (_selectedSocketCount <= 0)
                return;

            GUILayout.Label("孔颜色:");
            for (int i = 0; i < _selectedSocketColors.Count; i++)
                _selectedSocketColors[i] = DrawSocketColorToolbar($"孔 {i + 1}:", _selectedSocketColors[i], 300f);
        }

        private void DrawLinkSelectors()
        {
            if (_selectedSocketLinks.Count <= 0)
                return;

            GUILayout.Label("连接状态:");
            for (int i = 0; i < _selectedSocketLinks.Count; i++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{i + 1}-{i + 2}:", GUILayout.Width(110f));
                int currentIndex = _selectedSocketLinks[i] ? 0 : 1;
                int selectedIndex = GUILayout.Toolbar(currentIndex, LinkStateOptionLabels, GUILayout.Width(180f));
                _selectedSocketLinks[i] = selectedIndex == 0;
                GUILayout.EndHorizontal();
            }
        }

        private SocketColor DrawSocketColorToolbar(string label, SocketColor currentColor, float width)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(110f));
            int currentIndex = GetSocketColorIndex(currentColor);
            int selectedIndex = GUILayout.Toolbar(currentIndex, SocketColorOptionLabels, GUILayout.Width(width));
            GUILayout.EndHorizontal();
            return SocketColorOptions[Mathf.Clamp(selectedIndex, 0, SocketColorOptions.Length - 1)];
        }

        private void ToggleDropdown(GMDropdown dropdown)
        {
            _openDropdown = _openDropdown == dropdown ? GMDropdown.None : dropdown;
        }

        private void EnsureSelectionStateInitialized()
        {
            if (_selectedEquipmentDetail == null)
                _selectedEquipmentDetail = FindPreferredEquipmentDetail("胸甲") ?? FindFirstEquipmentDetail();

            if (_selectedActiveGem == null)
                SelectActiveGem(FindPreferredActiveGem("火球") ?? FindFirstActiveGem(), false);

            if (_selectedSupportGem == null)
                SelectSupportGem(FindPreferredSupportGem("多重") ?? FindFirstSupportGem(), false);

            if (!_hasInitializedSelections)
            {
                if (_selectedActiveGem != null)
                {
                    _generateSupportGem = false;
                    _selectedGemColor = GMItemFactory.GetDefaultGemColorForActiveSkill(_selectedActiveGem);
                }
                else if (_selectedSupportGem != null)
                {
                    _generateSupportGem = true;
                    _selectedGemColor = SocketColor.Green;
                }

                _hasInitializedSelections = _selectedEquipmentDetail != null || _selectedActiveGem != null || _selectedSupportGem != null;
            }

            NormalizeSocketSelections();
        }

        private void SelectEquipmentDetail(EquipmentDetailTypeData detail)
        {
            if (detail == null)
                return;

            bool changed = _selectedEquipmentDetail != detail;
            _selectedEquipmentDetail = detail;
            if (changed)
            {
                _selectedPrefixes.Clear();
                _selectedSuffixes.Clear();
            }

            int maxSocketCount = GMItemFactory.GetMaxSocketCountForDetail(detail);
            if (_selectedSocketCount <= 0 && maxSocketCount > 0)
                _selectedSocketCount = Mathf.Min(3, maxSocketCount);

            _selectedSocketCount = Mathf.Clamp(_selectedSocketCount, 0, maxSocketCount);
            NormalizeSocketSelections();
        }

        private void SelectActiveGem(ActiveSkillStoneConfigData activeGem, bool applyColor = true)
        {
            if (activeGem == null)
                return;

            _selectedActiveGem = activeGem;
            if (applyColor)
                _selectedGemColor = GMItemFactory.GetDefaultGemColorForActiveSkill(activeGem);
        }

        private void SelectSupportGem(SupportSkillStoneConfigData supportGem, bool applyColor = true)
        {
            if (supportGem == null)
                return;

            _selectedSupportGem = supportGem;
            if (applyColor)
                _selectedGemColor = SocketColor.Green;
        }

        private void NormalizeSocketSelections()
        {
            if (_selectedEquipmentDetail == null)
            {
                _selectedSocketCount = 0;
                _selectedSocketColors.Clear();
                _selectedSocketLinks.Clear();
                return;
            }

            int maxSocketCount = GMItemFactory.GetMaxSocketCountForDetail(_selectedEquipmentDetail);
            _selectedSocketCount = Mathf.Clamp(_selectedSocketCount, 0, maxSocketCount);

            while (_selectedSocketColors.Count < _selectedSocketCount)
                _selectedSocketColors.Add(GetDefaultSocketColorForIndex(_selectedSocketColors.Count));
            while (_selectedSocketColors.Count > _selectedSocketCount)
                _selectedSocketColors.RemoveAt(_selectedSocketColors.Count - 1);

            int linkCount = Mathf.Max(0, _selectedSocketCount - 1);
            while (_selectedSocketLinks.Count < linkCount)
                _selectedSocketLinks.Add(true);
            while (_selectedSocketLinks.Count > linkCount)
                _selectedSocketLinks.RemoveAt(_selectedSocketLinks.Count - 1);
        }

        private EquipmentDetailTypeData FindPreferredEquipmentDetail(string keyword)
        {
            var details = EquipmentConfigLoader.DetailTypes;
            for (int i = 0; i < details.Count; i++)
            {
                var detail = details[i];
                if (detail != null && ContainsIgnoreCase(detail.EquipmentDetailTypeName, keyword))
                    return detail;
            }

            return null;
        }

        private EquipmentDetailTypeData FindFirstEquipmentDetail()
        {
            var details = EquipmentConfigLoader.DetailTypes;
            for (int i = 0; i < details.Count; i++)
            {
                if (details[i] != null)
                    return details[i];
            }

            return null;
        }

        private ActiveSkillStoneConfigData FindPreferredActiveGem(string keyword)
        {
            var activeSkills = SkillConfigLoader.ActiveSkills;
            for (int i = 0; i < activeSkills.Count; i++)
            {
                var activeSkill = activeSkills[i];
                if (activeSkill == null)
                    continue;

                if (ContainsIgnoreCase(activeSkill.ActiveSkillStoneName, keyword) || ContainsIgnoreCase(activeSkill.ActiveSkillStoneCode, keyword))
                    return activeSkill;
            }

            return null;
        }

        private ActiveSkillStoneConfigData FindFirstActiveGem()
        {
            var activeSkills = SkillConfigLoader.ActiveSkills;
            for (int i = 0; i < activeSkills.Count; i++)
            {
                if (activeSkills[i] != null)
                    return activeSkills[i];
            }

            return null;
        }

        private SupportSkillStoneConfigData FindPreferredSupportGem(string keyword)
        {
            var supportSkills = SkillConfigLoader.SupportSkills;
            for (int i = 0; i < supportSkills.Count; i++)
            {
                var supportSkill = supportSkills[i];
                if (supportSkill == null)
                    continue;

                if (ContainsIgnoreCase(supportSkill.SupportSkillStoneName, keyword) || ContainsIgnoreCase(supportSkill.SupportSkillStoneCode, keyword))
                    return supportSkill;
            }

            return null;
        }

        private SupportSkillStoneConfigData FindFirstSupportGem()
        {
            var supportSkills = SkillConfigLoader.SupportSkills;
            for (int i = 0; i < supportSkills.Count; i++)
            {
                if (supportSkills[i] != null)
                    return supportSkills[i];
            }

            return null;
        }

        private string BuildModQuery(List<EquipmentModData> mods)
        {
            if (mods == null || mods.Count == 0)
                return string.Empty;

            var tokens = new List<string>();
            for (int i = 0; i < mods.Count; i++)
            {
                var mod = mods[i];
                if (mod == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(mod.EquipmentModId))
                    tokens.Add(mod.EquipmentModId);
                else if (!string.IsNullOrWhiteSpace(mod.EquipmentModName))
                    tokens.Add(mod.EquipmentModName);
            }

            return string.Join(",", tokens);
        }

        private string BuildSocketColorInput()
        {
            if (_selectedSocketColors.Count == 0)
                return string.Empty;

            var tokens = new List<string>(_selectedSocketColors.Count);
            for (int i = 0; i < _selectedSocketColors.Count; i++)
                tokens.Add(ResolveSocketColorToken(_selectedSocketColors[i]));

            return string.Join(",", tokens);
        }

        private string BuildLinkInput()
        {
            if (_selectedSocketLinks.Count == 0)
                return string.Empty;

            bool anyLinked = false;
            bool allLinked = true;
            var tokens = new List<string>();
            for (int i = 0; i < _selectedSocketLinks.Count; i++)
            {
                if (_selectedSocketLinks[i])
                {
                    anyLinked = true;
                    tokens.Add($"{i + 1}-{i + 2}");
                }
                else
                {
                    allLinked = false;
                }
            }

            if (!anyLinked)
                return "无";
            if (allLinked)
                return "全连";

            return string.Join(",", tokens);
        }

        private string GetCurrentGemQuery()
        {
            if (_generateSupportGem)
                return GetSupportGemQuery(_selectedSupportGem);

            return GetActiveGemQuery(_selectedActiveGem);
        }

        private string GetCurrentGemDisplayName()
        {
            if (_generateSupportGem)
                return GetSupportGemDisplayName(_selectedSupportGem);

            return GetActiveGemDisplayName(_selectedActiveGem);
        }

        private static string GetEquipmentDetailDisplayName(EquipmentDetailTypeData detail)
        {
            if (detail == null)
                return "请选择装备基础";

            string name = string.IsNullOrWhiteSpace(detail.EquipmentDetailTypeName) ? detail.EquipmentDetailTypeId : detail.EquipmentDetailTypeName;
            if (string.IsNullOrWhiteSpace(detail.EquipmentDetailTypeId))
                return name;

            return $"{name} [{detail.EquipmentDetailTypeId}]";
        }

        private static string GetModDisplayName(EquipmentModData mod)
        {
            if (mod == null)
                return "<空词缀>";

            string name = string.IsNullOrWhiteSpace(mod.EquipmentModName) ? mod.EquipmentModId : mod.EquipmentModName;
            if (string.IsNullOrWhiteSpace(mod.EquipmentModId))
                return name;

            return $"{name} [{mod.EquipmentModId}]";
        }

        private static string GetActiveGemDisplayName(ActiveSkillStoneConfigData activeGem)
        {
            if (activeGem == null)
                return "请选择主动宝石";

            string name = string.IsNullOrWhiteSpace(activeGem.ActiveSkillStoneName) ? activeGem.ActiveSkillStoneCode : activeGem.ActiveSkillStoneName;
            if (string.IsNullOrWhiteSpace(activeGem.ActiveSkillStoneCode))
                return name;

            return $"{name} [{activeGem.ActiveSkillStoneCode}]";
        }

        private static string GetSupportGemDisplayName(SupportSkillStoneConfigData supportGem)
        {
            if (supportGem == null)
                return "请选择辅助宝石";

            string name = string.IsNullOrWhiteSpace(supportGem.SupportSkillStoneName) ? supportGem.SupportSkillStoneCode : supportGem.SupportSkillStoneName;
            if (string.IsNullOrWhiteSpace(supportGem.SupportSkillStoneCode))
                return name;

            return $"{name} [{supportGem.SupportSkillStoneCode}]";
        }

        private static string GetActiveGemQuery(ActiveSkillStoneConfigData activeGem)
        {
            if (activeGem == null)
                return string.Empty;

            if (!string.IsNullOrWhiteSpace(activeGem.ActiveSkillStoneCode))
                return activeGem.ActiveSkillStoneCode;
            if (!string.IsNullOrWhiteSpace(activeGem.ActiveSkillStoneId))
                return activeGem.ActiveSkillStoneId;
            return activeGem.ActiveSkillStoneName ?? string.Empty;
        }

        private static string GetSupportGemQuery(SupportSkillStoneConfigData supportGem)
        {
            if (supportGem == null)
                return string.Empty;

            if (!string.IsNullOrWhiteSpace(supportGem.SupportSkillStoneCode))
                return supportGem.SupportSkillStoneCode;
            if (!string.IsNullOrWhiteSpace(supportGem.SupportSkillStoneId))
                return supportGem.SupportSkillStoneId;
            return supportGem.SupportSkillStoneName ?? string.Empty;
        }

        private static bool IsModSelected(List<EquipmentModData> selectedMods, EquipmentModData mod)
        {
            if (selectedMods == null || mod == null)
                return false;

            for (int i = 0; i < selectedMods.Count; i++)
            {
                var selectedMod = selectedMods[i];
                if (selectedMod == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(selectedMod.EquipmentModId) &&
                    !string.IsNullOrWhiteSpace(mod.EquipmentModId) &&
                    string.Equals(selectedMod.EquipmentModId, mod.EquipmentModId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static int GetSocketColorIndex(SocketColor color)
        {
            for (int i = 0; i < SocketColorOptions.Length; i++)
            {
                if (SocketColorOptions[i] == color)
                    return i;
            }

            return 0;
        }

        private static SocketColor GetDefaultSocketColorForIndex(int index)
        {
            return index switch
            {
                0 => SocketColor.Red,
                1 => SocketColor.Blue,
                2 => SocketColor.Green,
                _ => SocketColor.White,
            };
        }

        private static string[] BuildSocketCountLabels(int maxSocketCount)
        {
            var labels = new string[maxSocketCount + 1];
            for (int i = 0; i <= maxSocketCount; i++)
                labels[i] = i.ToString();
            return labels;
        }

        private static string ResolveSocketColorToken(SocketColor color)
        {
            return color switch
            {
                SocketColor.Red => "R",
                SocketColor.Green => "G",
                SocketColor.Blue => "B",
                _ => "W",
            };
        }

        private static bool ContainsIgnoreCase(string source, string value)
        {
            return !string.IsNullOrWhiteSpace(source) &&
                   !string.IsNullOrWhiteSpace(value) &&
                   source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void CompactMonsterEntities()
        {
            _monsterEntities.RemoveAll(e => e == null || !e.IsAlive);
        }

    }
}