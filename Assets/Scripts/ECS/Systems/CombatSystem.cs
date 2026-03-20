using System.Collections.Generic;
using UnityEngine;
using POELike.ECS.Core;
using POELike.ECS.Components;

namespace POELike.ECS.Systems
{
    /// <summary>
    /// 战斗系统
    /// 处理伤害计算、状态效果等
    /// 优先级：200
    /// </summary>
    public class CombatSystem : SystemBase
    {
        public override int Priority => 200;
        
        protected override void OnInitialize()
        {
            // 订阅伤害事件
            World.EventBus.Subscribe<DamageEvent>(OnDamageEvent);
        }
        
        protected override void OnUpdate(float deltaTime)
        {
            UpdateCooldowns(deltaTime);
            UpdateStatusEffects(deltaTime);
            UpdateInvincibility(deltaTime);
        }
        
        /// <summary>
        /// 更新攻击冷却
        /// </summary>
        private void UpdateCooldowns(float deltaTime)
        {
            var entities = World.Query<CombatComponent>();
            foreach (var entity in entities)
            {
                var combat = entity.GetComponent<CombatComponent>();
                if (combat.AttackCooldownTimer > 0)
                    combat.AttackCooldownTimer -= deltaTime;
            }
        }
        
        /// <summary>
        /// 更新状态效果（Buff/Debuff）
        /// </summary>
        private void UpdateStatusEffects(float deltaTime)
        {
            var entities = World.Query<CombatComponent, HealthComponent>();
            foreach (var entity in entities)
            {
                var combat = entity.GetComponent<CombatComponent>();
                var health = entity.GetComponent<HealthComponent>();
                var stats = entity.GetComponent<StatsComponent>();
                
                for (int i = combat.ActiveEffects.Count - 1; i >= 0; i--)
                {
                    var effect = combat.ActiveEffects[i];
                    effect.RemainingTime -= deltaTime;
                    
                    // 处理持续伤害效果
                    if (effect.Type == StatusEffectType.Ignite || 
                        effect.Type == StatusEffectType.Poison || 
                        effect.Type == StatusEffectType.Bleed)
                    {
                        effect.TickTimer -= deltaTime;
                        if (effect.TickTimer <= 0)
                        {
                            effect.TickTimer = effect.TickInterval;
                            health.TakeDamage(effect.Value * effect.TickInterval);
                        }
                    }
                    
                    // 移除过期效果
                    if (effect.IsExpired)
                    {
                        combat.ActiveEffects.RemoveAt(i);
                        // 移除该效果对应的属性修改器
                        stats?.RemoveModifiersFromSource(effect.Id);
                        World.EventBus.Publish(new StatusEffectRemovedEvent { Entity = entity, Effect = effect });
                    }
                }
            }
        }
        
        /// <summary>
        /// 更新无敌时间
        /// </summary>
        private void UpdateInvincibility(float deltaTime)
        {
            var entities = World.Query<CombatComponent>();
            foreach (var entity in entities)
            {
                var combat = entity.GetComponent<CombatComponent>();
                if (combat.IsInvincible && combat.InvincibleTimer > 0)
                {
                    combat.InvincibleTimer -= deltaTime;
                    if (combat.InvincibleTimer <= 0)
                        combat.IsInvincible = false;
                }
            }
        }
        
        /// <summary>
        /// 处理伤害事件
        /// </summary>
        private void OnDamageEvent(DamageEvent evt)
        {
            if (evt.Target == null || !evt.Target.IsAlive) return;
            
            var health = evt.Target.GetComponent<HealthComponent>();
            var combat = evt.Target.GetComponent<CombatComponent>();
            var stats = evt.Target.GetComponent<StatsComponent>();
            
            if (health == null) return;
            if (combat != null && combat.IsInvincible) return;
            
            // 计算最终伤害
            float finalDamage = CalculateFinalDamage(evt, stats);
            
            // 应用伤害
            health.TakeDamage(finalDamage);
            
            // 发布伤害结果事件
            World.EventBus.Publish(new DamageResultEvent
            {
                Source = evt.Source,
                Target = evt.Target,
                FinalDamage = finalDamage,
                IsCritical = evt.IsCritical,
                DamageType = evt.DamageType
            });
            
            // 检查死亡
            if (!health.IsAlive)
            {
                World.EventBus.Publish(new EntityDiedEvent { Entity = evt.Target, Killer = evt.Source });
            }
        }
        
        /// <summary>
        /// 计算最终伤害（含护甲、抗性等减免）
        /// </summary>
        private float CalculateFinalDamage(DamageEvent evt, StatsComponent targetStats)
        {
            float damage = evt.BaseDamage;
            
            if (targetStats == null) return damage;
            
            switch (evt.DamageType)
            {
                case DamageType.Physical:
                    // 护甲减伤公式（类似POE）
                    float armor = targetStats.GetStat(StatType.Armor);
                    damage = PhysicalDamageReduction(damage, armor);
                    break;
                    
                case DamageType.Fire:
                    float fireRes = Mathf.Clamp(targetStats.GetStat(StatType.FireResistance), -100f, 75f);
                    damage *= (1f - fireRes / 100f);
                    break;
                    
                case DamageType.Cold:
                    float coldRes = Mathf.Clamp(targetStats.GetStat(StatType.ColdResistance), -100f, 75f);
                    damage *= (1f - coldRes / 100f);
                    break;
                    
                case DamageType.Lightning:
                    float lightRes = Mathf.Clamp(targetStats.GetStat(StatType.LightningResistance), -100f, 75f);
                    damage *= (1f - lightRes / 100f);
                    break;
                    
                case DamageType.Chaos:
                    float chaosRes = Mathf.Clamp(targetStats.GetStat(StatType.ChaosResistance), -100f, 75f);
                    damage *= (1f - chaosRes / 100f);
                    break;
            }
            
            return Mathf.Max(1f, damage); // 最低1点伤害
        }
        
        /// <summary>
        /// POE风格的物理伤害减免公式
        /// 减免 = 护甲 / (护甲 + 10 * 伤害)
        /// </summary>
        private float PhysicalDamageReduction(float damage, float armor)
        {
            if (armor <= 0) return damage;
            float reduction = armor / (armor + 10f * damage);
            return damage * (1f - reduction);
        }
        
        protected override void OnDispose()
        {
            World.EventBus.Unsubscribe<DamageEvent>(OnDamageEvent);
        }
    }
    
    // 战斗相关事件
    public enum DamageType { Physical, Fire, Cold, Lightning, Chaos, True }
    
    public struct DamageEvent
    {
        public Entity Source;
        public Entity Target;
        public float BaseDamage;
        public DamageType DamageType;
        public bool IsCritical;
    }
    
    public struct DamageResultEvent
    {
        public Entity Source;
        public Entity Target;
        public float FinalDamage;
        public bool IsCritical;
        public DamageType DamageType;
    }
    
    public struct EntityDiedEvent
    {
        public Entity Entity;
        public Entity Killer;
    }
    
    public struct StatusEffectRemovedEvent
    {
        public Entity Entity;
        public StatusEffect Effect;
    }
}
