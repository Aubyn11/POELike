using System.Collections.Generic;
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

        /// <summary>当前选中的页签索引</summary>
        private int _currentTab = 0;

        /// <summary>页签条目列表（用于切换选中状态）</summary>
        private readonly List<ShopTabItem> _tabItems = new List<ShopTabItem>();

        /// <summary>当前页签实例化的装备 GameObject 列表（用于切换时销毁）</summary>
        private readonly List<GameObject> _equipmentViews = new List<GameObject>();

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
            // 只在打开时建一次格子，切换页签不重建
            if (_bag != null)
            {
                // ShopPanel 自行管理 Equipment 预制体视图，不需要 BagBox 额外创建 ItemView
                _bag.AutoCreateItemView = false;
                _bag.BuildGrid();
            }
            RefreshTabSelection();
            RefreshBag();
        }

        protected override void OnClose_Internal()
        {
            // 清理装备视图
            foreach (var go in _equipmentViews)
                if (go != null) Destroy(go);
            _equipmentViews.Clear();
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

            // 销毁 ShopPanel 自己实例化的装备 GO
            foreach (var go in _equipmentViews)
                if (go != null) Destroy(go);
            _equipmentViews.Clear();

            // 只清空道具视图，保留格子（避免切换页签时重建格子）
            _bag.ClearItems();

            var equipments = _tabEquipments[_currentTab];
            if (equipments == null || equipments.Count == 0) return;

            if (_equipmentPrefab == null)
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

                // 构建 BagItemData
                var bagData = new BagItemData(
                    itemId:     equip.DetailType.EquipmentDetailTypeId,
                    name:       equip.DisplayName,
                    gridWidth:  w,
                    gridHeight: h
                );
                bagData.ItemColor = equip.QualityColor;

                // 尝试放入背包
                if (!_bag.TryAutoPlaceItem(bagData)) continue;

                // 实例化 Equipment 预制体并设置尺寸
                // 必须挂在 GridRoot 下，与格子处于同一坐标系
                var go = Object.Instantiate(_equipmentPrefab,
                    _bag.GridRoot, false);
                _equipmentViews.Add(go);

                var equipItem = go.GetComponent<EquipmentItem>();
                if (equipItem != null)
                {
                    // 使用 GeneratedEquipment 重载，确保 Tips 能获取到完整装备数据
                    equipItem.Init(equip);
                    equipItem.SetupInBag(_bag.CellSize, _bag.CellSpacing,
                        bagData.GridCol, bagData.GridRow);
                }
                else
                {
                    // 没有 EquipmentItem 组件时，直接设置 RectTransform
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

                    // 设置颜色
                    var img = go.GetComponent<UnityEngine.UI.Image>();
                    if (img != null) img.color = equip.QualityColor;
                }

                // 置顶显示（在格子上方）
                go.transform.SetAsLastSibling();
            }
        }
    }
}