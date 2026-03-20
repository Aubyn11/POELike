using System.Collections.Generic;
using POELike.ECS.Core;

namespace POELike.ECS.Components
{
    /// <summary>
    /// 战斗组件
    /// 管理战斗相关状态
    /// </summary>
    public class CombatComponent : IComponent
    {
        public bool IsEnabled { get; set; } = true;
        
        /// <summary>
        /// 攻击冷却计时器
        /// </summary>
        public float AttackCooldownTimer { get; set; } = 0f;
        
        /// <summary>
        /// 攻击范围
        /// </summary>
        public float AttackRange { get; set; } = 2f;
        
        /// <summary>
        /// 当前目标实体
        /// </summary>
        public Entity CurrentTarget { get; set; }
        
        /// <summary>
        /// 是否正在攻击
        /// </summary>
        public bool IsAttacking { get; set; } = false;
        
        /// <summary>
        /// 是否无敌
        /// </summary>
        public bool IsInvincible { get; set; } = false;
        
        /// <summary>
        /// 无敌计时器
        /// </summary>
        public float InvincibleTimer { get; set; } = 0f;
        
        /// <summary>
        /// 当前激活的状态效果列表
        /// </summary>
        public List<StatusEffect> ActiveEffects { get; } = new List<StatusEffect>();
        
        /// <summary>
        /// 击退力度
        /// </summary>
        public float KnockbackForce { get; set; } = 0f;
        
        /// <summary>
        /// 击退方向
        /// </summary>
        public UnityEngine.Vector3 KnockbackDirection { get; set; }
        
        public void Reset()
        {
            AttackCooldownTimer = 0f;
            CurrentTarget = null;
            IsAttacking = false;
            IsInvincible = false;
            InvincibleTimer = 0f;
            ActiveEffects.Clear();
        }
    }
    
    /// <summary>
    /// 状态效果（Buff/Debuff）
    /// </summary>
    public class StatusEffect
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public StatusEffectType Type { get; set; }
        public float Duration { get; set; }
        public float RemainingTime { get; set; }
        public float Value { get; set; }       // 效果数值（如每秒伤害）
        public float TickInterval { get; set; } = 1f; // 触发间隔
        public float TickTimer { get; set; } = 0f;
        public Entity Source { get; set; }     // 施加者
        
        public bool IsExpired => RemainingTime <= 0;
    }
    
    /// <summary>
    /// 状态效果类型
    /// </summary>
    public enum StatusEffectType
    {
        // Buff
        Haste,          // 加速
        Fortify,        // 强化（减少受到的伤害）
        Onslaught,      // 猛攻（增加攻击速度和移动速度）
        
        // Debuff
        Ignite,         // 点燃（持续火焰伤害）
        Chill,          // 冰缓（降低移动速度）
        Freeze,         // 冰冻（无法行动）
        Shock,          // 感电（增加受到的伤害）
        Poison,         // 中毒（持续混沌伤害）
        Bleed,          // 流血（持续物理伤害）
        Curse,          // 诅咒
        Stun,           // 眩晕
    }
}
