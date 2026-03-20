using System.Collections.Generic;
using POELike.ECS.Core;

namespace POELike.ECS.Components
{
    /// <summary>
    /// 技能组件
    /// 管理角色拥有的技能
    /// </summary>
    public class SkillComponent : IComponent
    {
        public bool IsEnabled { get; set; } = true;
        
        /// <summary>
        /// 技能槽位列表（对应快捷键1-6）
        /// </summary>
        public List<SkillSlot> SkillSlots { get; } = new List<SkillSlot>();
        
        /// <summary>
        /// 当前正在施放的技能
        /// </summary>
        public SkillSlot ActiveSkill { get; set; }
        
        /// <summary>
        /// 是否正在施法
        /// </summary>
        public bool IsCasting { get; set; } = false;
        
        /// <summary>
        /// 施法计时器
        /// </summary>
        public float CastTimer { get; set; } = 0f;
        
        /// <summary>
        /// 初始化技能槽位
        /// </summary>
        public void InitializeSlots(int count = 6)
        {
            SkillSlots.Clear();
            for (int i = 0; i < count; i++)
                SkillSlots.Add(new SkillSlot { SlotIndex = i });
        }
        
        /// <summary>
        /// 获取指定槽位的技能
        /// </summary>
        public SkillSlot GetSlot(int index)
        {
            if (index >= 0 && index < SkillSlots.Count)
                return SkillSlots[index];
            return null;
        }
        
        public void Reset()
        {
            ActiveSkill = null;
            IsCasting = false;
            CastTimer = 0f;
            foreach (var slot in SkillSlots)
                slot.CooldownTimer = 0f;
        }
    }
    
    /// <summary>
    /// 技能槽位
    /// </summary>
    public class SkillSlot
    {
        public int SlotIndex { get; set; }
        public SkillData SkillData { get; set; }
        public float CooldownTimer { get; set; } = 0f;
        public bool IsOnCooldown => CooldownTimer > 0f;
        public bool HasSkill => SkillData != null;
    }
    
    /// <summary>
    /// 技能数据（ScriptableObject数据的运行时表示）
    /// </summary>
    public class SkillData
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public SkillType Type { get; set; }
        public float ManaCost { get; set; }
        public float Cooldown { get; set; }
        public float CastTime { get; set; }     // 施法时间（0=瞬发）
        public float Damage { get; set; }
        public float Range { get; set; }
        public float AreaRadius { get; set; }
        public int ProjectileCount { get; set; } = 1;
        public float Duration { get; set; }     // 持续时间（用于持续技能）
        
        // 技能支持宝石（类似POE的技能宝石系统）
        public List<SupportGem> SupportGems { get; } = new List<SupportGem>();
    }
    
    /// <summary>
    /// 技能类型
    /// </summary>
    public enum SkillType
    {
        Attack,         // 攻击技能
        Spell,          // 法术技能
        Projectile,     // 投射物技能
        AoE,            // 范围技能
        Movement,       // 移动技能（闪现、冲刺等）
        Aura,           // 光环技能
        Totem,          // 图腾技能
        Mine,           // 地雷技能
        Trap,           // 陷阱技能
        Summon,         // 召唤技能
        Channeling,     // 引导技能
    }
    
    /// <summary>
    /// 支持宝石（类似POE的支持宝石系统）
    /// </summary>
    public class SupportGem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public SupportGemType Type { get; set; }
        public float Value { get; set; }
    }
    
    /// <summary>
    /// 支持宝石类型
    /// </summary>
    public enum SupportGemType
    {
        MultiProjectile,    // 多重投射
        Fork,               // 分叉
        Chain,              // 连锁
        Pierce,             // 穿透
        SpellEcho,          // 法术回响
        GMP,                // 大范围多重投射
        LMP,                // 小范围多重投射
        IncreasedAoE,       // 增加范围
        ConcentratedEffect, // 集中效果
        AddedFireDamage,    // 附加火焰伤害
        AddedColdDamage,    // 附加冰冷伤害
        AddedLightningDamage, // 附加闪电伤害
        Faster,             // 加速施法/攻击
        CriticalStrike,     // 暴击
        Culling,            // 斩杀
    }
}
