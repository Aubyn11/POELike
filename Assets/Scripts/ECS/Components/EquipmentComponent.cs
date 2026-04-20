using System.Collections.Generic;
using POELike.ECS.Core;

namespace POELike.ECS.Components
{
    /// <summary>
    /// 装备槽位类型
    /// </summary>
    public enum EquipmentSlot
    {
        Helmet,         // 头盔
        BodyArmour,     // 胸甲
        Gloves,         // 手套
        Boots,          // 靴子
        Belt,           // 腰带
        Amulet,         // 护身符
        RingLeft,       // 左戒指
        RingRight,      // 右戒指
        MainHand,       // 主手武器
        OffHand,        // 副手（武器/盾牌）
        Flask1,         // 药剂1
        Flask2,         // 药剂2
        Flask3,         // 药剂3
        Flask4,         // 药剂4
        Flask5,         // 药剂5
    }
    
    /// <summary>
    /// 物品稀有度
    /// </summary>
    public enum ItemRarity
    {
        Normal,         // 普通（白色）
        Magic,          // 魔法（蓝色）
        Rare,           // 稀有（黄色）
        Unique,         // 传奇（橙色）
    }
    
    /// <summary>
    /// 物品类型
    /// </summary>
    public enum ItemType
    {
        Weapon,
        Armour,
        Accessory,
        Flask,
        Gem,
        Currency,
        Map,
        Misc,
    }

    /// <summary>
    /// 药剂类型
    /// </summary>
    public enum FlaskKind
    {
        Life,
        Mana,
        Hybrid,
        Utility,
    }

    /// <summary>
    /// 功能药剂效果类型
    /// </summary>
    public enum FlaskUtilityEffectKind
    {
        None,
        MoveSpeed,
        Armour,
        Evasion,
        FireResistance,
        ColdResistance,
        LightningResistance,
        ChaosResistance,
        PhysicalDamageReduction,
        ConsecratedGround,
        Phasing,
        Onslaught,
    }
    
    /// <summary>
    /// 物品数据
    /// </summary>
    public class ItemData
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string BaseType { get; set; }    // 基础类型（如"短剑"）
        public ItemType Type { get; set; }
        public ItemRarity Rarity { get; set; }
        public int ItemLevel { get; set; }
        public int RequiredLevel { get; set; }
        public int RequiredStrength { get; set; }
        public int RequiredDexterity { get; set; }
        public int RequiredIntelligence { get; set; }

        // 可堆叠道具字段（当前主要供通货使用）
        public bool IsStackable { get; set; }
        public int StackCount { get; set; } = 1;
        public int MaxStackCount { get; set; } = 1;
        public string Description { get; set; }

        // 通货字段（为后续通货系统与策划配置预留）
        public string CurrencyBaseId { get; set; }
        public string CurrencyCode { get; set; }
        public string CurrencyCategoryId { get; set; }
        public string CurrencyCategoryName { get; set; }
        public string CurrencyDisplayColor { get; set; }
        public string CurrencyEffectTypeId { get; set; }
        public string CurrencyEffectTypeName { get; set; }
        public string CurrencyTargetDescription { get; set; }
        public string CurrencyEffectDescription { get; set; }
        public string CurrencyFlavorText { get; set; }
        public int CurrencyDropLevel { get; set; }

        public int CurrencySortOrder { get; set; }
        public bool CurrencyConsumesOnUse { get; set; } = true;
        public bool CurrencyCanApplyNormal { get; set; }
        public bool CurrencyCanApplyMagic { get; set; }
        public bool CurrencyCanApplyRare { get; set; }
        public bool CurrencyCanApplyUnique { get; set; }
        public bool CurrencyCanApplyCorrupted { get; set; }
        public List<ItemType> CurrencyAllowedItemTypes { get; } = new List<ItemType>();

        // 精确可装备槽位（用于地面掉落转背包、Tips 展示与装备栏校验）
        public EquipmentSlot? PrimaryEquipmentSlot { get; set; }
        public List<EquipmentSlot> AllowedEquipmentSlots { get; } = new List<EquipmentSlot>();

        // 药剂字段（对齐 POE 的基础药剂结构）
        public FlaskKind? FlaskType { get; set; }
        public int FlaskRecoverLife { get; set; }
        public int FlaskRecoverMana { get; set; }
        public int FlaskDurationMs { get; set; }
        public int FlaskMaxCharges { get; set; }
        public int FlaskCurrentCharges { get; set; }
        public int FlaskChargesPerUse { get; set; }
        public int FlaskQualityPercent { get; set; }
        public bool FlaskIsInstant { get; set; }
        public int FlaskInstantPercent { get; set; }
        public FlaskUtilityEffectKind FlaskUtilityEffectType { get; set; } = FlaskUtilityEffectKind.None;
        public int FlaskUtilityEffectValue { get; set; }
        public string FlaskEffectDescription { get; set; }
        
        // 词缀（类似POE的词缀系统）
        public List<StatModifier> Prefixes { get; } = new List<StatModifier>(); // 前缀（最多3个）
        public List<StatModifier> Suffixes { get; } = new List<StatModifier>(); // 后缀（最多3个）
        
        // 隐式词缀（基础类型自带）
        public List<StatModifier> ImplicitMods { get; } = new List<StatModifier>();
        
        /// <summary>
        /// 获取所有词缀
        /// </summary>
        public IEnumerable<StatModifier> GetAllMods()
        {
            foreach (var mod in ImplicitMods) yield return mod;
            foreach (var mod in Prefixes) yield return mod;
            foreach (var mod in Suffixes) yield return mod;
        }
        
        public int PrefixCount => Prefixes.Count;
        public int SuffixCount => Suffixes.Count;
        public bool CanAddPrefix => Prefixes.Count < 3;
        public bool CanAddSuffix => Suffixes.Count < 3;
    }
    
    /// <summary>
    /// 装备组件
    /// 管理角色装备的物品
    /// </summary>
    public class EquipmentComponent : IComponent
    {
        public bool IsEnabled { get; set; } = true;
        
        private readonly Dictionary<EquipmentSlot, ItemData> _equippedItems = new Dictionary<EquipmentSlot, ItemData>();
        
        /// <summary>
        /// 装备物品
        /// </summary>
        public bool Equip(EquipmentSlot slot, ItemData item)
        {
            _equippedItems[slot] = item;
            return true;
        }
        
        /// <summary>
        /// 卸下装备
        /// </summary>
        public ItemData Unequip(EquipmentSlot slot)
        {
            if (_equippedItems.TryGetValue(slot, out var item))
            {
                _equippedItems.Remove(slot);
                return item;
            }
            return null;
        }
        
        /// <summary>
        /// 获取指定槽位的装备
        /// </summary>
        public ItemData GetEquipped(EquipmentSlot slot)
        {
            return _equippedItems.TryGetValue(slot, out var item) ? item : null;
        }
        
        /// <summary>
        /// 是否有装备在指定槽位
        /// </summary>
        public bool HasEquipped(EquipmentSlot slot)
        {
            return _equippedItems.ContainsKey(slot);
        }
        
        /// <summary>
        /// 获取所有装备的词缀修改器
        /// </summary>
        public IEnumerable<StatModifier> GetAllEquipmentModifiers()
        {
            foreach (var item in _equippedItems.Values)
            {
                if (item == null) continue;
                foreach (var mod in item.GetAllMods())
                    yield return mod;
            }
        }
        
        public void Reset()
        {
            _equippedItems.Clear();
        }
    }
    
    /// <summary>
    /// 背包组件
    /// </summary>
    public class InventoryComponent : IComponent
    {
        public bool IsEnabled { get; set; } = true;
        
        // 背包格子（12x5 类似POE）
        public int Width { get; set; } = 12;
        public int Height { get; set; } = 5;
        
        private readonly List<InventoryItem> _items = new List<InventoryItem>();
        
        public IReadOnlyList<InventoryItem> Items => _items;
        
        public void AddItem(ItemData item, int x, int y)
        {
            _items.Add(new InventoryItem { Item = item, GridX = x, GridY = y });
        }
        
        public bool RemoveItem(ItemData item)
        {
            return _items.RemoveAll(i => i.Item == item) > 0;
        }
        
        public void Reset()
        {
            _items.Clear();
        }
    }
    
    public class InventoryItem
    {
        public ItemData Item { get; set; }
        public int GridX { get; set; }
        public int GridY { get; set; }
    }
}
