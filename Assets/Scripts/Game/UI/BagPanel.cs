using System;
using System.Collections.Generic;
using POELike.ECS.Components;
using POELike.Game.Equipment;
using POELike.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace POELike.Game.UI
{
    /// <summary>
    /// 背包面板控制器。
    /// 挂载在 [Bag.prefab] 上，负责初始化背包格子、装备槽位和测试物品。
    /// </summary>
    public class BagPanel : MonoBehaviour
    {
        [Header("运行时引用（可留空自动查找）")]
        [SerializeField] private BagBox _bag;
        [SerializeField] private RectTransform _equipmentRoot;
        [SerializeField] private GameObject _equipmentPrefab;

        [Header("测试宝石颜色")]
        [SerializeField] private SocketColor _primaryGemColor   = SocketColor.Red;
        [SerializeField] private SocketColor _secondaryGemColor = SocketColor.Blue;
        [SerializeField] private SocketColor _supportGemColor   = SocketColor.Green;

        private readonly Dictionary<EquipmentSlot, EquipmentSlotView> _slotViews =
            new Dictionary<EquipmentSlot, EquipmentSlotView>();

        private readonly List<GameObject> _runtimeItemViews = new List<GameObject>();

        private bool _initialized;
        private bool _isInitializing;

        public bool IsInitializing => _isInitializing;

        public void EnsureInitialized()
        {
            if (_initialized || _isInitializing)
                return;

            _isInitializing = true;
            try
            {
                CacheReferences();
                BuildEquipmentSlots();
                PopulateDemoItems();
                _initialized = true;
            }
            finally
            {
                _isInitializing = false;
            }
        }

        private void OnEnable()
        {
            if (_initialized)
                return;

            EnsureInitialized();
        }

        private void OnDisable()
        {
            _bag?.ClearAllHighlights();
        }

        private void CacheReferences()
        {
            if (_bag == null)
            {
                _bag = GetComponentInChildren<BagBox>(true);
                if (_bag == null)
                    _bag = GetComponent<BagBox>();
            }

            if (_equipmentRoot == null)
            {
                var equipment = transform.Find("Equipment");
                if (equipment != null)
                    _equipmentRoot = equipment as RectTransform;
            }

            if (_equipmentPrefab == null)
                _equipmentPrefab = Resources.Load<GameObject>("UI/Equipment");

            if (_bag != null)
            {
                _bag.AutoCreateItemView = false;
                _bag.EnsureGridBuilt();
            }
        }

        private void BuildEquipmentSlots()
        {
            _slotViews.Clear();
            if (_equipmentRoot == null)
                return;

            RegisterSlot("Helmet",       EquipmentSlot.Helmet);
            RegisterSlot("Armor",        EquipmentSlot.BodyArmour);
            RegisterSlot("Gloves",       EquipmentSlot.Gloves);
            RegisterSlot("Shoes",        EquipmentSlot.Boots);
            RegisterSlot("Belt",         EquipmentSlot.Belt);
            RegisterSlot("Necklace",     EquipmentSlot.Amulet);
            RegisterSlot("LeftRing",     EquipmentSlot.RingLeft);
            RegisterSlot("RightRing",    EquipmentSlot.RingRight);
            RegisterSlot("Weapon",       EquipmentSlot.MainHand);
            RegisterSlot("DeputyWeapon", EquipmentSlot.OffHand);
            RegisterSlot("Potion1",      EquipmentSlot.Flask1);
            RegisterSlot("Potion2",      EquipmentSlot.Flask2);
            RegisterSlot("Potion3",      EquipmentSlot.Flask3);
            RegisterSlot("Potion4",      EquipmentSlot.Flask4);
            RegisterSlot("Potion5",      EquipmentSlot.Flask5);
        }

        private void RegisterSlot(string nodeName, EquipmentSlot slot)
        {
            var node = FindChildRecursive(_equipmentRoot, nodeName);
            if (node == null)
                return;

            var slotView = node.GetComponent<EquipmentSlotView>();
            if (slotView == null)
                slotView = node.gameObject.AddComponent<EquipmentSlotView>();

            slotView.Setup(slot);
            _slotViews[slot] = slotView;
        }

        public BagItemData GetEquippedItemData(EquipmentSlot slot)
        {
            if (!_initialized)
            {
                if (_isInitializing)
                    return null;

                EnsureInitialized();
            }

            return _slotViews.TryGetValue(slot, out var slotView)
                ? slotView?.PlacedItem?.Data
                : null;
        }

        public void FillEquippedPotions(BagItemData[] results)
        {
            if (results == null || results.Length == 0)
                return;

            Array.Clear(results, 0, results.Length);

            if (results.Length > 0) results[0] = GetEquippedItemData(EquipmentSlot.Flask1);
            if (results.Length > 1) results[1] = GetEquippedItemData(EquipmentSlot.Flask2);
            if (results.Length > 2) results[2] = GetEquippedItemData(EquipmentSlot.Flask3);
            if (results.Length > 3) results[3] = GetEquippedItemData(EquipmentSlot.Flask4);
            if (results.Length > 4) results[4] = GetEquippedItemData(EquipmentSlot.Flask5);
        }

        public void GetSocketedActiveGems(List<BagItemData> results)
        {
            if (results == null)
                return;

            results.Clear();
            if (_isInitializing)
                return;
            if (!_initialized)
                EnsureInitialized();
            if (_equipmentRoot == null)
                return;

            var equippedItems = _equipmentRoot.GetComponentsInChildren<EquipmentItem>(true);
            for (int i = 0; i < equippedItems.Length; i++)
                equippedItems[i].GetSocketedActiveGems(results);
        }

        /// <summary>
        /// 获取指定装备槽位中某个插槽的相邻连结宝石。
        /// 当前连结规则固定为只返回 `socketIndex - 1` 和 `socketIndex + 1`。
        /// </summary>
        public void GetLinkedGems(EquipmentSlot slot, int socketIndex, List<BagItemData> results)
        {
            if (results == null)
                return;

            results.Clear();
            if (_isInitializing)
                return;
            if (!_initialized)
                EnsureInitialized();
            if (_equipmentRoot == null)
                return;
            if (!_slotViews.TryGetValue(slot, out var slotView) || slotView?.PlacedItem == null)
                return;

            var equipmentItem = slotView.PlacedItem.GetComponent<EquipmentItem>();
            if (equipmentItem == null)
                return;

            equipmentItem.GetLinkedGems(socketIndex, results);
        }

        public bool CanAddItemToBag(BagItemData data, out string failureReason)
        {
            failureReason = string.Empty;
            if (data == null)
            {
                failureReason = "道具数据为空";
                return false;
            }

            if (_isInitializing)
            {
                failureReason = "背包仍在初始化中";
                return false;
            }

            if (!_initialized)
                EnsureInitialized();

            if (_bag == null)
            {
                failureReason = "背包容器未初始化";
                return false;
            }

            data.NormalizeStackState(clampToMax: false);

            if (data.IsStackable)
            {

                int incomingCount = Mathf.Max(0, data.StackCount);
                int stackableSpace = GetStackableSpaceFor(data);
                int remainingCount = Mathf.Max(0, incomingCount - stackableSpace);
                if (remainingCount <= 0)
                    return true;

                int maxStackCount = Mathf.Max(1, data.MaxStackCount);
                int requiredPlacements = Mathf.CeilToInt(remainingCount / (float)maxStackCount);
                if (requiredPlacements <= 0)
                    return true;

                if (!_bag.TryGetAutoPlacementPositions(data, requiredPlacements, out _))
                {
                    failureReason = $"背包空间不足：{data.Name}";
                    return false;
                }

                return true;
            }

            if (!_bag.HasSpaceFor(data))
            {
                failureReason = $"背包空间不足：{data.Name}";
                return false;
            }

            return true;
        }

        public bool TryAddItemToBag(BagItemData data, out string failureReason)
        {
            if (!CanAddItemToBag(data, out failureReason))
                return false;

            data.NormalizeStackState(clampToMax: false);

            if (data.IsStackable)
            {

                int remainingCount = Mathf.Max(0, data.StackCount);
                remainingCount = MergeIntoExistingStacks(data, remainingCount);
                if (remainingCount <= 0)
                {
                    UIManager.Instance?.RefreshCharactorMainPanel();
                    return true;
                }

                int maxStackCount = Mathf.Max(1, data.MaxStackCount);
                while (remainingCount > 0)
                {
                    int placementCount = Mathf.Min(maxStackCount, remainingCount);
                    var stackItem = data.CloneForStack(placementCount);
                    stackItem.NormalizeStackState();

                    if (!_bag.TryAutoPlaceItem(stackItem))
                    {
                        failureReason = $"背包空间不足：{data.Name}";
                        return false;
                    }

                    var stackView = CreateRuntimeItemView(stackItem);
                    if (stackView == null)
                    {
                        _bag.RemoveItem(stackItem);
                        failureReason = $"创建道具视图失败：{data.Name}";
                        return false;
                    }

                    stackView.BindToBag(_bag);
                    remainingCount -= placementCount;
                }

                UIManager.Instance?.RefreshCharactorMainPanel();
                return true;
            }

            if (!_bag.TryAutoPlaceItem(data))
            {
                failureReason = $"背包空间不足：{data.Name}";
                return false;
            }

            var view = CreateRuntimeItemView(data);
            if (view == null)
            {
                _bag.RemoveItem(data);
                failureReason = $"创建道具视图失败：{data.Name}";
                return false;
            }

            view.BindToBag(_bag);
            UIManager.Instance?.RefreshCharactorMainPanel();
            return true;
        }

        private int GetStackableSpaceFor(BagItemData data)
        {
            if (data == null || !data.IsStackable || _bag == null)
                return 0;

            int totalSpace = 0;
            var items = _bag.Items;
            for (int i = 0; i < items.Count; i++)
            {
                var existing = items[i];
                if (existing == null || !existing.CanStackWith(data))
                    continue;

                totalSpace += existing.AvailableStackSpace;
            }

            return totalSpace;
        }

        private int MergeIntoExistingStacks(BagItemData data, int remainingCount)
        {
            if (data == null || !data.IsStackable || _bag == null)
                return remainingCount;

            var items = _bag.Items;
            for (int i = 0; i < items.Count && remainingCount > 0; i++)
            {
                var existing = items[i];
                if (existing == null || !existing.CanStackWith(data))
                    continue;

                int moveAmount = Mathf.Min(existing.AvailableStackSpace, remainingCount);
                if (moveAmount <= 0)
                    continue;

                existing.StackCount += moveAmount;
                existing.NormalizeStackState();
                if (existing.RuntimeItemData != null)
                    existing.RuntimeItemData.StackCount = existing.StackCount;

                var existingView = BagItemView.FindByData(existing);
                existingView?.RefreshView();

                remainingCount -= moveAmount;

            }

            return remainingCount;
        }

        private static Transform FindChildRecursive(Transform root, string nodeName)
        {

            if (root == null || string.IsNullOrWhiteSpace(nodeName))
                return null;

            if (root.name == nodeName)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindChildRecursive(root.GetChild(i), nodeName);
                if (found != null)
                    return found;
            }

            return null;
        }

        private void PopulateDemoItems()
        {
            if (_bag == null)
                return;

            ClearRuntimeItems();
            _bag.ClearItems();

            var chestData = CreateEquipmentData(
                itemId: "equip_body_1",
                name: "流亡者胸甲",
                slot: EquipmentSlot.BodyArmour,
                width: 2,
                height: 3,
                color: new Color(0.40f, 0.60f, 1.00f),
                sockets: new[] { SocketColor.Red, SocketColor.Blue, SocketColor.Green },
                prefixes: new[] { "最大生命 +32", "护甲提高 24%" },
                suffixes: new[] { "火焰抗性 +18%", "冰霜抗性 +12%" },
                prefixModifiers: new[]
                {
                    new StatModifier(StatType.MaxHealth, ModifierType.Flat, 32f, "equipment:demo"),
                    new StatModifier(StatType.Armor, ModifierType.PercentAdd, 24f, "equipment:demo"),
                },
                suffixModifiers: new[]
                {
                    new StatModifier(StatType.FireResistance, ModifierType.Flat, 18f, "equipment:demo"),
                    new StatModifier(StatType.ColdResistance, ModifierType.Flat, 12f, "equipment:demo"),
                }
            );
            SpawnItemInBag(chestData, 0, 0);

            var swordData = CreateEquipmentData(
                itemId: "equip_weapon_1",
                name: "训练长剑",
                slot: EquipmentSlot.MainHand,
                width: 1,
                height: 3,
                color: new Color(1.00f, 0.82f, 0.28f),
                sockets: new[] { SocketColor.Red, SocketColor.Green },
                prefixes: new[] { "附加 3 - 7 点物理伤害", "物理伤害提高 19%" },
                suffixes: new[] { "攻击速度提高 8%", "命中值 +42" },
                prefixModifiers: new[]
                {
                    new StatModifier(StatType.PhysicalDamage, ModifierType.Flat, 5f, "equipment:demo"),
                    new StatModifier(StatType.PhysicalDamage, ModifierType.PercentAdd, 19f, "equipment:demo"),
                },
                suffixModifiers: new[]
                {
                    new StatModifier(StatType.AttackSpeed, ModifierType.PercentAdd, 8f, "equipment:demo"),
                }
            );
            SpawnItemInBag(swordData, 3, 0);

            var helmData = CreateEquipmentData(
                itemId: "equip_helmet_1",
                name: "旅者头盔",
                slot: EquipmentSlot.Helmet,
                width: 2,
                height: 2,
                color: new Color(0.85f, 0.85f, 0.92f),
                sockets: new[] { SocketColor.Blue },
                prefixes: new[] { "护甲提高 15%" },
                suffixes: new[] { "智慧 +12", "闪电抗性 +14%" },
                prefixModifiers: new[]
                {
                    new StatModifier(StatType.Armor, ModifierType.PercentAdd, 15f, "equipment:demo"),
                },
                suffixModifiers: new[]
                {
                    new StatModifier(StatType.Intelligence, ModifierType.Flat, 12f, "equipment:demo"),
                    new StatModifier(StatType.LightningResistance, ModifierType.Flat, 14f, "equipment:demo"),
                }
            );
            SpawnItemInBag(helmData, 5, 0);

            var bootsData = CreateEquipmentData(
                itemId: "equip_boots_1",
                name: "皮靴",
                slot: EquipmentSlot.Boots,
                width: 2,
                height: 2,
                color: new Color(0.82f, 0.62f, 0.38f),
                prefixes: new[] { "最大生命 +18" },
                suffixes: new[] { "移动速度提高 10%", "混沌抗性 +9%" },
                prefixModifiers: new[]
                {
                    new StatModifier(StatType.MaxHealth, ModifierType.Flat, 18f, "equipment:demo"),
                },
                suffixModifiers: new[]
                {
                    new StatModifier(StatType.MovementSpeed, ModifierType.PercentAdd, 10f, "equipment:demo"),
                    new StatModifier(StatType.ChaosResistance, ModifierType.Flat, 9f, "equipment:demo"),
                }
            );
            SpawnItemInBag(bootsData, 7, 0);

            var ringData = CreateEquipmentData(
                itemId: "equip_ring_1",
                name: "铜戒指",
                slot: EquipmentSlot.RingLeft,
                width: 1,
                height: 1,
                color: new Color(1.00f, 0.92f, 0.48f),
                prefixes: new[] { "最大魔力 +18" },
                suffixes: new[] { "火焰抗性 +20%" },
                prefixModifiers: new[]
                {
                    new StatModifier(StatType.MaxMana, ModifierType.Flat, 18f, "equipment:demo"),
                },
                suffixModifiers: new[]
                {
                    new StatModifier(StatType.FireResistance, ModifierType.Flat, 20f, "equipment:demo"),
                }
            );
            ringData.SetAcceptedEquipmentSlots(new[] { EquipmentSlot.RingLeft, EquipmentSlot.RingRight });
            SpawnItemInBag(ringData, 9, 0);

            SpawnFlaskDemoItem("life_medium", 0, 3);
            SpawnFlaskDemoItem("mana_medium", 1, 3);
            SpawnFlaskDemoItem("utility_quicksilver", 2, 3);
            SpawnFlaskDemoItem("utility_granite", 3, 3);
            SpawnFlaskDemoItem("hybrid_medium", 4, 3);

            var gem1 = CreateGemData("gem_fireball", "火球术", _primaryGemColor, new Color(0.92f, 0.30f, 0.24f));
            SpawnItemInBag(gem1, 10, 0);

            var gem2 = CreateGemData("gem_frost", "冰霜新星", _secondaryGemColor, new Color(0.35f, 0.58f, 1.00f));
            SpawnItemInBag(gem2, 11, 0);

            var gem3 = CreateGemData("gem_support", "多重投射", _supportGemColor, new Color(0.25f, 0.86f, 0.38f), BagGemKind.Support);
            SpawnItemInBag(gem3, 12, 0);
        }

        private BagItemData CreateEquipmentData(
            string itemId,
            string name,
            EquipmentSlot slot,
            int width,
            int height,
            Color color,
            IEnumerable<SocketColor> sockets = null,
            IEnumerable<string> prefixes = null,
            IEnumerable<string> suffixes = null,
            IEnumerable<StatModifier> prefixModifiers = null,
            IEnumerable<StatModifier> suffixModifiers = null)
        {
            var data = new BagItemData(itemId, name, width, height)
            {
                ItemKind = BagItemKind.Equipment,
                AcceptedEquipmentSlot = slot,
                ItemColor = color,
            };

            if (sockets != null)
            {
                foreach (var socketColor in sockets)
                    data.Sockets.Add(new SocketData { Color = socketColor });
            }

            if (prefixes != null)
            {
                foreach (var prefix in prefixes)
                {
                    if (!string.IsNullOrWhiteSpace(prefix))
                        data.PrefixDescriptions.Add(prefix);
                }
            }

            if (suffixes != null)
            {
                foreach (var suffix in suffixes)
                {
                    if (!string.IsNullOrWhiteSpace(suffix))
                        data.SuffixDescriptions.Add(suffix);
                }
            }

            var runtimeItem = new ItemData();
            bool hasRuntimeModifiers = false;

            if (prefixModifiers != null)
            {
                foreach (var modifier in prefixModifiers)
                {
                    runtimeItem.Prefixes.Add(modifier);
                    hasRuntimeModifiers = true;
                }
            }

            if (suffixModifiers != null)
            {
                foreach (var modifier in suffixModifiers)
                {
                    runtimeItem.Suffixes.Add(modifier);
                    hasRuntimeModifiers = true;
                }
            }

            if (hasRuntimeModifiers)
                data.RuntimeItemData = runtimeItem;

            return data;
        }

        private BagItemData CreateGemData(string itemId, string name, SocketColor gemColor, Color color, BagGemKind gemKind = BagGemKind.Active)
        {
            return new BagItemData(itemId, name, 1, 1)
            {
                ItemKind = BagItemKind.Gem,
                GemColor = gemColor,
                GemKind = gemKind,
                ItemColor = color,
            };
        }

        private void SpawnFlaskDemoItem(string flaskCode, int col, int row)
        {
            var flask = EquipmentGenerator.GenerateFlaskByCode(flaskCode);
            var data = BagItemData.CreateFromGeneratedFlask(flask);
            if (data == null)
                return;

            SpawnItemInBag(data, col, row);
        }

        private void SpawnItemInBag(BagItemData data, int col, int row)
        {
            if (_bag == null || data == null)
                return;

            if (!_bag.TryPlaceItem(data, col, row))
                return;

            var view = CreateRuntimeItemView(data);
            if (view == null)
                return;

            view.BindToBag(_bag);
        }

        private BagItemView CreateRuntimeItemView(BagItemData data)
        {
            GameObject go;

            if ((data.IsEquipment || data.IsFlask || data.IsCurrency) && _equipmentPrefab != null)
            {
                go = Instantiate(_equipmentPrefab, _bag.GridRoot, false);
                var equipmentItem = go.GetComponent<EquipmentItem>();
                if (equipmentItem != null)
                {
                    equipmentItem.Init(data);
                }
            }

            else
            {
                go = new GameObject($"Gem_{data.ItemId}", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
                go.transform.SetParent(_bag.GridRoot, false);
                var image = go.GetComponent<Image>();
                image.raycastTarget = true;
                image.sprite = data.Icon;
                image.color  = data.ItemColor;
            }

            if (go.GetComponent<CanvasGroup>() == null)
                go.AddComponent<CanvasGroup>();

            var itemView = go.GetComponent<BagItemView>();
            if (itemView == null)
                itemView = go.AddComponent<BagItemView>();

            itemView.Setup(data);
            _runtimeItemViews.Add(go);
            return itemView;
        }

        private void ClearRuntimeItems()
        {
            for (int i = 0; i < _runtimeItemViews.Count; i++)
            {
                if (_runtimeItemViews[i] != null)
                    Destroy(_runtimeItemViews[i]);
            }

            _runtimeItemViews.Clear();

            foreach (var slotView in _slotViews.Values)
            {
                if (slotView?.PlacedItem != null)
                    slotView.ClearPlacedItem();
            }
        }
    }
}