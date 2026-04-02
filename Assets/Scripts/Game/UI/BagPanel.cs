using System.Collections.Generic;
using POELike.ECS.Components;
using POELike.Game.Equipment;
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

        public void EnsureInitialized()
        {
            if (_initialized)
                return;

            CacheReferences();
            BuildEquipmentSlots();
            PopulateDemoItems();
            _initialized = true;
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
            var node = _equipmentRoot.Find(nodeName);
            if (node == null)
                return;

            var slotView = node.GetComponent<EquipmentSlotView>();
            if (slotView == null)
                slotView = node.gameObject.AddComponent<EquipmentSlotView>();

            slotView.Setup(slot);
            _slotViews[slot] = slotView;
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
                suffixes: new[] { "火焰抗性 +18%", "冰霜抗性 +12%" }
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
                suffixes: new[] { "攻击速度提高 8%", "命中值 +42" }
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
                suffixes: new[] { "智慧 +12", "闪电抗性 +14%" }
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
                suffixes: new[] { "移动速度提高 10%", "混沌抗性 +9%" }
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
                suffixes: new[] { "火焰抗性 +20%" }
            );
            SpawnItemInBag(ringData, 9, 0);

            var gem1 = CreateGemData("gem_fireball", "火球术", _primaryGemColor, new Color(0.92f, 0.30f, 0.24f));
            SpawnItemInBag(gem1, 10, 0);

            var gem2 = CreateGemData("gem_frost", "冰霜新星", _secondaryGemColor, new Color(0.35f, 0.58f, 1.00f));
            SpawnItemInBag(gem2, 11, 0);

            var gem3 = CreateGemData("gem_support", "多重投射", _supportGemColor, new Color(0.25f, 0.86f, 0.38f));
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
            IEnumerable<string> suffixes = null)
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

            return data;
        }

        private BagItemData CreateGemData(string itemId, string name, SocketColor gemColor, Color color)
        {
            return new BagItemData(itemId, name, 1, 1)
            {
                ItemKind = BagItemKind.Gem,
                GemColor = gemColor,
                ItemColor = color,
            };
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

            if (data.IsEquipment && _equipmentPrefab != null)
            {
                go = Instantiate(_equipmentPrefab, _bag.GridRoot, false);
                var equipmentItem = go.GetComponent<EquipmentItem>();
                if (equipmentItem != null)
                {
                    equipmentItem.Init(
                        detailTypeId: 0,
                        gridWidth: data.GridWidth,
                        gridHeight: data.GridHeight,
                        itemName: data.Name,
                        icon: data.Icon,
                        itemColor: data.ItemColor,
                        sockets: data.Sockets
                    );
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
                slotView?.ClearPlacedItem();
        }
    }
}