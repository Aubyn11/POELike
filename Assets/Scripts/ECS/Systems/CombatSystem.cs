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

        private readonly List<Entity> _combatEntities = new List<Entity>(4096);
        
        protected override void OnInitialize()
        {
            // 订阅伤害事件
            World.EventBus.Subscribe<DamageEvent>(OnDamageEvent);
            World.EventBus.Subscribe<AIAttackEvent>(OnAIAttackEvent);
        }
        
        protected override void OnUpdate(float deltaTime)
        {
            UpdateCombatStates(deltaTime);
        }

        private void UpdateCombatStates(float deltaTime)
        {
            World.Query<CombatComponent>(_combatEntities);
            foreach (var entity in _combatEntities)
            {
                var combat = entity.GetComponent<CombatComponent>();
                if (combat == null) continue;

                if (combat.AttackCooldownTimer > 0)
                    combat.AttackCooldownTimer -= deltaTime;

                if (combat.IsInvincible && combat.InvincibleTimer > 0)
                {
                    combat.InvincibleTimer -= deltaTime;
                    if (combat.InvincibleTimer <= 0)
                        combat.IsInvincible = false;
                }

                if (combat.ActiveEffects.Count <= 0)
                    continue;

                var health = entity.GetComponent<HealthComponent>();
                var stats  = entity.GetComponent<StatsComponent>();
                UpdateStatusEffects(entity, combat, health, stats, deltaTime);
            }
        }
        
        /// <summary>
        /// 更新状态效果（Buff/Debuff）
        /// </summary>
        private void UpdateStatusEffects(Entity entity, CombatComponent combat, HealthComponent health, StatsComponent stats, float deltaTime)
        {
            if (health == null) return;

            for (int i = combat.ActiveEffects.Count - 1; i >= 0; i--)
            {
                var effect = combat.ActiveEffects[i];
                effect.RemainingTime -= deltaTime;
                effect.TickTimer -= deltaTime;
                
                // 处理持续伤害效果
                if (effect.Type == StatusEffectType.Ignite || 
                    effect.Type == StatusEffectType.Poison || 
                    effect.Type == StatusEffectType.Bleed)
                {
                    if (effect.TickTimer <= 0)
                    {
                        effect.TickTimer = effect.TickInterval;
                        health.TakeDamage(effect.Value * effect.TickInterval);
                    }
                }
                else if (effect.Type == StatusEffectType.LifeRecovery)
                {
                    if (effect.TickTimer <= 0)
                    {
                        effect.TickTimer = effect.TickInterval;
                        health.Heal(effect.Value * effect.TickInterval);
                    }
                }
                else if (effect.Type == StatusEffectType.ManaRecovery)
                {
                    if (effect.TickTimer <= 0)
                    {
                        effect.TickTimer = effect.TickInterval;
                        health.CurrentMana += effect.Value * effect.TickInterval;
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

        /// <summary>
        /// 将怪物 AI 攻击事件转为正式伤害事件
        /// </summary>
        private void OnAIAttackEvent(AIAttackEvent evt)
        {
            if (evt.Attacker == null || evt.Target == null) return;
            if (!evt.Attacker.IsAlive || !evt.Target.IsAlive) return;
            if (evt.Attacker.Tag != "Monster") return;

            var monster = evt.Attacker.GetComponent<MonsterComponent>();
            if (monster == null) return;

            var stats = evt.Attacker.GetComponent<StatsComponent>();
            float damage = stats != null ? stats.GetStat(StatType.PhysicalDamage) : monster.Attack;
            damage = Mathf.Max(1f, damage);

            World.EventBus.Publish(new DamageEvent
            {
                Source = evt.Attacker,
                Target = evt.Target,
                BaseDamage = damage,
                DamageType = DamageType.Physical
            });
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

                // 纯 ECS 怪物死亡后立即销毁实体，保证 GPU 缓存与渲染列表及时清理
                if (evt.Target.Tag == "Monster")
                    World.DestroyEntity(evt.Target);
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
            World.EventBus.Unsubscribe<AIAttackEvent>(OnAIAttackEvent);
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