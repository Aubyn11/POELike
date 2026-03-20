using System.Collections.Generic;
using UnityEngine;
using POELike.ECS.Components;

namespace POELike.Game.Items
{
    /// <summary>
    /// 物品工厂
    /// 负责创建各种物品数据
    /// </summary>
    public static class ItemFactory
    {
        private static int _itemIdCounter = 0;
        
        /// <summary>
        /// 创建随机物品（类似POE的掉落系统）
        /// </summary>
        public static ItemData CreateRandomItem(int itemLevel, ItemType type = ItemType.Weapon)
        {
            var item = new ItemData
            {
                Id = $"item_{_itemIdCounter++}",
                ItemLevel = itemLevel,
                Type = type,
                Rarity = RollRarity(itemLevel)
            };
            
            // 根据类型设置基础属性
            switch (type)
            {
                case ItemType.Weapon:
                    SetupWeapon(item, itemLevel);
                    break;
                case ItemType.Armour:
                    SetupArmour(item, itemLevel);
                    break;
                case ItemType.Accessory:
                    SetupAccessory(item, itemLevel);
                    break;
            }
            
            // 根据稀有度添加词缀
            AddModsBasedOnRarity(item, itemLevel);
            
            return item;
        }
        
        /// <summary>
        /// 创建指定武器
        /// </summary>
        public static ItemData CreateWeapon(string name, int itemLevel, float minDamage, float maxDamage, float attackSpeed = 1.2f)
        {
            var item = new ItemData
            {
                Id = $"weapon_{_itemIdCounter++}",
                Name = name,
                BaseType = "单手剑",
                Type = ItemType.Weapon,
                Rarity = ItemRarity.Normal,
                ItemLevel = itemLevel,
                RequiredLevel = Mathf.Max(1, itemLevel - 5),
            };
            
            // 隐式词缀：武器基础伤害
            float avgDamage = (minDamage + maxDamage) / 2f;
            item.ImplicitMods.Add(new StatModifier(StatType.PhysicalDamage, ModifierType.Flat, avgDamage, "implicit"));
            item.ImplicitMods.Add(new StatModifier(StatType.AttackSpeed, ModifierType.Flat, attackSpeed, "implicit"));
            
            return item;
        }
        
        /// <summary>
        /// 创建指定护甲
        /// </summary>
        public static ItemData CreateArmour(string name, int itemLevel, float armor, EquipmentSlot slot)
        {
            var item = new ItemData
            {
                Id = $"armour_{_itemIdCounter++}",
                Name = name,
                BaseType = "板甲",
                Type = ItemType.Armour,
                Rarity = ItemRarity.Normal,
                ItemLevel = itemLevel,
                RequiredLevel = Mathf.Max(1, itemLevel - 5),
            };
            
            item.ImplicitMods.Add(new StatModifier(StatType.Armor, ModifierType.Flat, armor, "implicit"));
            
            return item;
        }
        
        private static void SetupWeapon(ItemData item, int itemLevel)
        {
            item.BaseType = "单手剑";
            item.Name = "铁剑";
            float baseDamage = 5f + itemLevel * 2f;
            item.ImplicitMods.Add(new StatModifier(StatType.PhysicalDamage, ModifierType.Flat, baseDamage, "implicit"));
            item.ImplicitMods.Add(new StatModifier(StatType.AttackSpeed, ModifierType.Flat, 1.2f, "implicit"));
        }
        
        private static void SetupArmour(ItemData item, int itemLevel)
        {
            item.BaseType = "板甲";
            item.Name = "铁甲";
            float baseArmor = 10f + itemLevel * 3f;
            item.ImplicitMods.Add(new StatModifier(StatType.Armor, ModifierType.Flat, baseArmor, "implicit"));
        }
        
        private static void SetupAccessory(ItemData item, int itemLevel)
        {
            item.BaseType = "护身符";
            item.Name = "铜制护身符";
        }
        
        /// <summary>
        /// 根据物品等级随机稀有度
        /// </summary>
        private static ItemRarity RollRarity(int itemLevel)
        {
            float roll = Random.value;
            if (roll < 0.01f + itemLevel * 0.001f) return ItemRarity.Unique;
            if (roll < 0.10f + itemLevel * 0.005f) return ItemRarity.Rare;
            if (roll < 0.35f) return ItemRarity.Magic;
            return ItemRarity.Normal;
        }
        
        /// <summary>
        /// 根据稀有度添加词缀
        /// </summary>
        private static void AddModsBasedOnRarity(ItemData item, int itemLevel)
        {
            int prefixCount = 0, suffixCount = 0;
            
            switch (item.Rarity)
            {
                case ItemRarity.Magic:
                    prefixCount = Random.Range(0, 2);
                    suffixCount = Random.Range(0, 2);
                    if (prefixCount + suffixCount == 0) prefixCount = 1;
                    break;
                case ItemRarity.Rare:
                    prefixCount = Random.Range(1, 4);
                    suffixCount = Random.Range(1, 4);
                    break;
                case ItemRarity.Unique:
                    // 传奇物品有固定词缀，这里简单处理
                    prefixCount = 3;
                    suffixCount = 3;
                    break;
            }
            
            // 添加随机前缀
            for (int i = 0; i < prefixCount && item.CanAddPrefix; i++)
                item.Prefixes.Add(RollRandomMod(itemLevel, true));
            
            // 添加随机后缀
            for (int i = 0; i < suffixCount && item.CanAddSuffix; i++)
                item.Suffixes.Add(RollRandomMod(itemLevel, false));
            
            // 根据稀有度设置名称
            if (item.Rarity == ItemRarity.Magic)
                item.Name = $"魔法{item.BaseType}";
            else if (item.Rarity == ItemRarity.Rare)
                item.Name = GenerateRareName();
            else if (item.Rarity == ItemRarity.Unique)
                item.Name = $"传奇{item.BaseType}";
        }
        
        private static StatModifier RollRandomMod(int itemLevel, bool isPrefix)
        {
            // 简化的词缀池
            var prefixPool = new[]
            {
                (StatType.PhysicalDamage, ModifierType.PercentAdd, 10f + itemLevel * 2f),
                (StatType.MaxHealth, ModifierType.Flat, 20f + itemLevel * 5f),
                (StatType.Armor, ModifierType.Flat, 15f + itemLevel * 3f),
                (StatType.MaxMana, ModifierType.Flat, 15f + itemLevel * 3f),
            };
            
            var suffixPool = new[]
            {
                (StatType.AttackSpeed, ModifierType.PercentAdd, 5f + itemLevel * 0.5f),
                (StatType.CriticalChance, ModifierType.Flat, 1f + itemLevel * 0.1f),
                (StatType.FireResistance, ModifierType.Flat, 10f + itemLevel * 1f),
                (StatType.ColdResistance, ModifierType.Flat, 10f + itemLevel * 1f),
                (StatType.LightningResistance, ModifierType.Flat, 10f + itemLevel * 1f),
                (StatType.MovementSpeed, ModifierType.PercentAdd, 5f),
            };
            
            var pool = isPrefix ? prefixPool : suffixPool;
            var (statType, modType, value) = pool[Random.Range(0, pool.Length)];
            
            return new StatModifier(statType, modType, value);
        }
        
        private static readonly string[] _rareNameParts = { "血腥", "暗影", "烈焰", "寒冰", "雷霆", "混沌", "神圣", "腐化" };
        private static string GenerateRareName()
        {
            return _rareNameParts[Random.Range(0, _rareNameParts.Length)] + "之" + 
                   _rareNameParts[Random.Range(0, _rareNameParts.Length)];
        }
    }
}
