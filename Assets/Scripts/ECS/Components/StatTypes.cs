using POELike.ECS.Core;

namespace POELike.ECS.Components
{
    /// <summary>
    /// 属性修改器类型
    /// </summary>
    public enum ModifierType
    {
        Flat,           // 固定值加成（+10攻击力）
        PercentAdd,     // 百分比叠加（+10%攻击力，多个叠加）
        PercentMore,    // 百分比增加（+10%更多攻击力，多个相乘）
    }
    
    /// <summary>
    /// 属性修改器
    /// 类似POE的词缀系统
    /// </summary>
    public struct StatModifier
    {
        public StatType StatType;
        public ModifierType ModifierType;
        public float Value;
        public string Source; // 来源（装备名、技能名等）
        
        public StatModifier(StatType statType, ModifierType modType, float value, string source = "")
        {
            StatType = statType;
            ModifierType = modType;
            Value = value;
            Source = source;
        }
    }
    
    /// <summary>
    /// 属性类型枚举
    /// </summary>
    public enum StatType
    {
        // 基础属性
        Strength,           // 力量
        Dexterity,          // 敏捷
        Intelligence,       // 智慧
        
        // 生命相关
        MaxHealth,          // 最大生命值
        HealthRegen,        // 生命回复
        LifeLeech,          // 生命偷取
        
        // 魔法相关
        MaxMana,            // 最大魔力值
        ManaRegen,          // 魔力回复
        ManaLeech,          // 魔力偷取
        
        // 护盾相关
        MaxEnergyShield,    // 最大能量护盾
        EnergyShieldRegen,  // 能量护盾回复
        
        // 攻击相关
        PhysicalDamage,     // 物理伤害
        FireDamage,         // 火焰伤害
        ColdDamage,         // 冰冷伤害
        LightningDamage,    // 闪电伤害
        ChaosDamage,        // 混沌伤害
        AttackSpeed,        // 攻击速度
        CriticalChance,     // 暴击率
        CriticalMultiplier, // 暴击倍率
        
        // 防御相关
        Armor,              // 护甲
        Evasion,            // 闪避
        BlockChance,        // 格挡率
        
        // 抗性
        FireResistance,     // 火焰抗性
        ColdResistance,     // 冰冷抗性
        LightningResistance,// 闪电抗性
        ChaosResistance,    // 混沌抗性
        
        // 移动
        MovementSpeed,      // 移动速度
        
        // 技能相关
        SkillCooldownReduction, // 技能冷却缩减
        AreaOfEffect,           // 技能范围
        ProjectileSpeed,        // 投射物速度
        Duration,               // 持续时间
    }
}
