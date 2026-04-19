using System.Collections.Generic;
using POELike.ECS.Components;
using POELike.Game.Equipment;
using UnityEngine;
using UnityEngine.UI;

namespace POELike.Game.UI
{
    /// <summary>
    /// 商店面板，继承自 <see cref="UIGamePanel"/>。
    /// 挂载在 CustomPanel 预制体根节点上。
    /// 打开时随机生成装备，通过 TogArr 页签切换不同类别。
    /// </summary>
    public class ShopPanel : UIGamePanel
    {
        // ── UI 引用 ───────────────────────────────────────────────────
        [Header("UI 引用")]
        [SerializeField] private Button  _closeBtn;
        [SerializeField] private ListBox  _togArr;   // 页签容器（ListBox 负责水平布局）
        [SerializeField] private BagBox   _bag;       // 商品展示背包

        [Header("装备预制体")]
        [SerializeField] private GameObject _equipmentPrefab; // Equipment.prefab

        // ── 运行时数据 ────────────────────────────────────────────────

        /// <summary>每个页签对应的已生成装备列表</summary>
        private readonly List<GeneratedEquipment>[] _tabEquipments =
            new List<GeneratedEquipment>[4];

        /// <summary>药剂页签对应的已生成药剂列表</summary>
        private readonly List<GeneratedFlask> _flaskTabItems = new List<GeneratedFlask>();

        /// <summary>当前选中的页签索引</summary>
        private int _currentTab = 0;

        /// <summary>页签条目列表（用于切换选中状态）</summary>
        private readonly List<ShopTabItem> _tabItems = new List<ShopTabItem>();

        /// <summary>当前页签正在使用的装备视图</summary>
        private readonly List<GameObject> _activeEquipmentViews = new List<GameObject>();

        /// <summary>可复用的装备视图对象池</summary>
        private readonly Stack<GameObject> _pooledEquipmentViews = new Stack<GameObject>();

        // ── 生命周期 ──────────────────────────────────────────────────

        protected override void Awake()
        {
            base.Awake();

            // 自动查找子节点（Inspector 未赋值时兜底）
            if (_togArr == null)
            {
                var t = transform.Find("TogArr");
                if (t != null) _togArr = t.GetComponent<ListBox>();
                if (_togArr == null) Debug.LogWarning("[ShopPanel] 未找到子节点 TogArr 或其上没有 ListBox 组件，请检查层级。");
            }
            if (_bag == null)
            {
                var t = transform.Find("bag");
                if (t != null) _bag = t.GetComponent<BagBox>();
                else Debug.LogWarning("[ShopPanel] 未找到子节点 bag，请检查层级名称。");
            }
            if (_closeBtn == null)
            {
                var t = transform.Find("CloseBtn");
                if (t != null) _closeBtn = t.GetComponent<Button>();
            }

            _closeBtn?.onClick.AddListener(Close);
        }

        // ── 公开接口 ──────────────────────────────────────────────────

        public new void Open()
        {
            base.Open();
        }

        /// <summary>
        /// 页签被点击时由 <see cref="ShopTabItem"/> 调用
        /// </summary>
        public void OnTabSelected(int tabIndex)
        {
            if (tabIndex == _currentTab) return;
            _currentTab = tabIndex;
            RefreshTabSelection();
            RefreshBag();
        }

        // ── 基类钩子 ──────────────────────────────────────────────────

        protected override void OnOpen()
        {
            GenerateAllTabEquipments();
            BuildTabs();
            _currentTab = 0;
            if (_bag != null)
            {
                // ShopPanel 自行管理 Equipment 预制体视图，不需要 BagBox 额外创建 ItemView
                _bag.AutoCreateItemView = false;
                _bag.EnsureGridBuilt();
            }
            RefreshTabSelection();
            RefreshBag();
        }

        protected override void OnClose_Internal()
        {
            ReleaseActiveEquipmentViews();
            _bag?.ClearItems();

            // 清理页签
            _togArr?.Clear();
            _tabItems.Clear();
        }

        private void OnDestroy()
        {
            _closeBtn?.onClick.RemoveListener(Close);
        }

        // ── 内部逻辑 ──────────────────────────────────────────────────

        /// <summary>
        /// 为4个页签各自随机生成一批装备
        /// </summary>
        private void GenerateAllTabEquipments()
        {
            for (int i = 0; i < EquipmentGenerator.TabCategories.Length; i++)
                _tabEquipments[i] = EquipmentGenerator.GenerateForCategories(
                    EquipmentGenerator.TabCategories[i]);

            _flaskTabItems.Clear();
            _flaskTabItems.AddRange(EquipmentGenerator.GenerateFlasksForShop());
        }

        /// <summary>
        /// 在 TogArr（ListBox）中生成4个页签
        /// </summary>
        private void BuildTabs()
        {
            if (_togArr == null)
            {
                Debug.LogError("[ShopPanel] _togArr 未赋值，或 TogArr 节点上缺少 ListBox 组件！");
                return;
            }

            // 清空旧页签
            _togArr.Clear();
            _tabItems.Clear();

            // 一次性创建所有页签
            int tabCount = EquipmentGenerator.TabNames.Length;
            _togArr.AddItem(0, tabCount);

            if (_togArr.Count != tabCount)
            {
                Debug.LogError($"[ShopPanel] 期望创建 {tabCount} 个页签，实际只创建了 {_togArr.Count} 个。" +
                               "请确认 TogArr 的 _itemPrefabs[0] 已配置 Tog.prefab，且 Tog.prefab 上挂有 ListBoxItem 派生组件。");
                return;
            }

            for (int i = 0; i < tabCount; i++)
            {
                var listItem = _togArr.GetItemByIndex(i);
                if (listItem == null) continue;

                var tabItem = listItem.GetCtrl().GetComponent<ShopTabItem>();
                if (tabItem == null)
                    tabItem = listItem.GetCtrl().AddComponent<ShopTabItem>();

                tabItem.SetupTab(i, EquipmentGenerator.TabNames[i], this);
                _tabItems.Add(tabItem);
            }
        }

        /// <summary>
        /// 刷新页签选中状态
        /// </summary>
        private void RefreshTabSelection()
        {
            for (int i = 0; i < _tabItems.Count; i++)
                _tabItems[i]?.SetSelected(i == _currentTab);
        }

        private GameObject AcquireEquipmentView()
        {
            while (_pooledEquipmentViews.Count > 0)
            {
                var go = _pooledEquipmentViews.Pop();
                if (go == null) continue;

                go.transform.SetParent(_bag.GridRoot, false);
                go.SetActive(true);
                return go;
            }

            return Object.Instantiate(_equipmentPrefab, _bag.GridRoot, false);
        }

        private void ReleaseActiveEquipmentViews()
        {
            foreach (var go in _activeEquipmentViews)
            {
                if (go == null) continue;
                go.SetActive(false);
                _pooledEquipmentViews.Push(go);
            }

            _activeEquipmentViews.Clear();
        }

        /// <summary>
        /// 根据当前页签刷新背包中的装备
        /// </summary>
        private void RefreshBag()
        {
            if (_bag == null)
            {
                Debug.LogError("[ShopPanel] _bag 未赋值！");
                return;
            }

            // 回收当前页的装备视图，避免切页时反复 Destroy / Instantiate
            ReleaseActiveEquipmentViews();

            // 只清空道具占用数据，保留格子
            _bag.ClearItems();

            if (_currentTab == EquipmentGenerator.FlaskTabIndex)
            {
                RefreshFlaskBag();
                return;
            }

            var equipments = _tabEquipments[_currentTab];
            if (equipments == null || equipments.Count == 0) return;

            if (_equipmentPrefab == null && _pooledEquipmentViews.Count == 0)
            {
                Debug.LogError("[ShopPanel] _equipmentPrefab 未赋值！");
                return;
            }

            foreach (var equip in equipments)
            {
                if (equip.DetailType == null) continue;

                int.TryParse(equip.DetailType.EquipmentWidth,  out int w);
                int.TryParse(equip.DetailType.EquipmentHeight, out int h);
                w = Mathf.Max(1, w);
                h = Mathf.Max(1, h);

                var bagData = EquipmentBagDataFactory.CreateFromGeneratedEquipment(
                    equip,
                    equip.DetailType.EquipmentDetailTypeId);
                if (bagData == null)
                    continue;

                if (!_bag.TryAutoPlaceItem(bagData)) continue;

                var go = AcquireEquipmentView();
                _activeEquipmentViews.Add(go);

                var equipItem = go.GetComponent<EquipmentItem>();
                if (equipItem != null)
                {
                    equipItem.Init(equip);
                    equipItem.SetupInBag(_bag.CellSize, _bag.CellSpacing,
                        bagData.GridCol, bagData.GridRow);
                }
                else
                {
                    var rt = go.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        const float pad = 2f;
                        float pw = w * _bag.CellSize + (w - 1) * _bag.CellSpacing - pad * 2f;
                        float ph = h * _bag.CellSize + (h - 1) * _bag.CellSpacing - pad * 2f;
                        rt.anchorMin        = new Vector2(0f, 1f);
                        rt.anchorMax        = new Vector2(0f, 1f);
                        rt.pivot            = new Vector2(0f, 1f);
                        rt.sizeDelta        = new Vector2(pw, ph);
                        rt.anchoredPosition = new Vector2(
                             bagData.GridCol * (_bag.CellSize + _bag.CellSpacing) + pad,
                            -bagData.GridRow * (_bag.CellSize + _bag.CellSpacing) - pad
                        );
                    }

                    var img = go.GetComponent<Image>();
                    if (img != null) img.color = equip.QualityColor;
                }

                go.transform.SetAsLastSibling();
            }
        }

        /// <summary>
        /// 将 <see cref="GeneratedEquipment"/> 的运行时数据填充进 <see cref="BagItemData"/>，
        /// 让装备被放入背包 / 装备栏后仍保留词条、插槽与可装备槽位信息。
        /// </summary>
        private static void PopulateEquipmentBagData(BagItemData bagData, GeneratedEquipment equip)
        {
            if (bagData == null || equip == null)
                return;

            bagData.ItemKind = BagItemKind.Equipment;

            // 插槽
            if (equip.Sockets != null)
            {
                bagData.Sockets.Clear();
                foreach (var socket in equip.Sockets)
                {
                    if (socket == null) continue;
                    bagData.Sockets.Add(new SocketData { Color = socket.Color });
                }
            }

            // 词条（同时构建 Tips 显示文本与运行时 RolledMod）
            bagData.EquipmentMods.Clear();
            bagData.PrefixDescriptions.Clear();
            bagData.SuffixDescriptions.Clear();

            if (equip.Mods != null)
            {
                foreach (var mod in equip.Mods)
                {
                    if (mod?.Mod == null) continue;

                    bagData.EquipmentMods.Add(mod);

                    bool isPrefix = mod.Mod.EquipmentModType == "1";
                    if (mod.Values != null)
                    {
                        foreach (var value in mod.Values)
                        {
                            if (value?.Config == null) continue;
                            string desc = string.IsNullOrEmpty(value.Config.EquipmentModValueDesc)
                                ? mod.Mod.EquipmentModName
                                : $"{value.Config.EquipmentModValueDesc} {value.RolledValue}";

                            if (isPrefix)
                                bagData.PrefixDescriptions.Add(desc);
                            else
                                bagData.SuffixDescriptions.Add(desc);
                        }
                    }
                }
            }

            // 可装备槽位（从 DetailType 的 EquipmentPart 推导）
            var slots = ResolveEquipmentSlotsFromPart(equip.DetailType?.EquipmentPart);
            if (slots.Count > 0)
            {
                bagData.SetAcceptedEquipmentSlots(slots);
                bagData.AcceptedEquipmentSlot = slots[0];
            }
        }

        /// <summary>
        /// 根据装备配置的部位字段推导出它可装备到的 <see cref="EquipmentSlot"/> 列表。
        /// 若该装备的 EquipmentPart 字段与 <see cref="EquipmentSlot"/> 枚举名一致（大小写不敏感）则直接映射；
        /// 对于戒指等可进入多个槽位的部位，这里做一次特殊展开。
        /// </summary>
        private static List<EquipmentSlot> ResolveEquipmentSlotsFromPart(string equipmentPart)
        {
            var result = new List<EquipmentSlot>();
            if (string.IsNullOrWhiteSpace(equipmentPart))
                return result;

            string normalized = equipmentPart.Trim();

            // 戒指可装备到左手 / 右手两个槽位
            if (string.Equals(normalized, "Ring", System.StringComparison.OrdinalIgnoreCase))
            {
                result.Add(EquipmentSlot.RingLeft);
                result.Add(EquipmentSlot.RingRight);
                return result;
            }

            if (System.Enum.TryParse(normalized, true, out EquipmentSlot slot))
            {
                result.Add(slot);
                return result;
            }

            // 兼容常见中文 / 别名
            switch (normalized)
            {
                case "MainHand":
                case "Weapon":
                    result.Add(EquipmentSlot.MainHand);
                    break;
                case "OffHand":
                case "Shield":
                    result.Add(EquipmentSlot.OffHand);
                    break;
                case "Chest":
                case "Armor":
                case "Armour":
                    result.Add(EquipmentSlot.BodyArmour);
                    break;
            }

            return result;
        }

        private void RefreshFlaskBag()
        {
            if (_flaskTabItems.Count == 0)
                return;

            if (_equipmentPrefab == null && _pooledEquipmentViews.Count == 0)
            {
                Debug.LogError("[ShopPanel] _equipmentPrefab 未赋值！");
                return;
            }

            foreach (var flask in _flaskTabItems)
            {
                if (flask == null)
                    continue;

                var bagData = BagItemData.CreateFromGeneratedFlask(flask);
                if (bagData == null)
                    continue;

                if (!_bag.TryAutoPlaceItem(bagData))
                    continue;

                var go = AcquireEquipmentView();
                _activeEquipmentViews.Add(go);

                var equipItem = go.GetComponent<EquipmentItem>();
                if (equipItem != null)
                {
                    equipItem.Init(bagData);
                    equipItem.SetupInBag(_bag.CellSize, _bag.CellSpacing,
                        bagData.GridCol, bagData.GridRow);
                }
                else
                {
                    var rt = go.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        const float pad = 2f;
                        float pw = bagData.GridWidth * _bag.CellSize + (bagData.GridWidth - 1) * _bag.CellSpacing - pad * 2f;
                        float ph = bagData.GridHeight * _bag.CellSize + (bagData.GridHeight - 1) * _bag.CellSpacing - pad * 2f;
                        rt.anchorMin        = new Vector2(0f, 1f);
                        rt.anchorMax        = new Vector2(0f, 1f);
                        rt.pivot            = new Vector2(0f, 1f);
                        rt.sizeDelta        = new Vector2(pw, ph);
                        rt.anchoredPosition = new Vector2(
                             bagData.GridCol * (_bag.CellSize + _bag.CellSpacing) + pad,
                            -bagData.GridRow * (_bag.CellSize + _bag.CellSpacing) - pad
                        );
                    }

                    var img = go.GetComponent<Image>();
                    if (img != null) img.color = bagData.ItemColor;
                }

                go.transform.SetAsLastSibling();
            }
        }
    }
}