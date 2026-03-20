using System.Collections.Generic;
using UnityEngine;
using POELike.ECS.Core;
using POELike.ECS.Components;

namespace POELike.ECS.Systems
{
    /// <summary>
    /// 技能系统
    /// 处理技能施放、冷却、效果
    /// 优先级：150
    /// </summary>
    public class SkillSystem : SystemBase
    {
        public override int Priority => 150;
        
        protected override void OnInitialize()
        {
            World.EventBus.Subscribe<SkillActivateEvent>(OnSkillActivate);
        }
        
        protected override void OnUpdate(float deltaTime)
        {
            UpdateSkillCooldowns(deltaTime);
            UpdateCasting(deltaTime);
        }
        
        /// <summary>
        /// 更新技能冷却
        /// </summary>
        private void UpdateSkillCooldowns(float deltaTime)
        {
            var entities = World.Query<SkillComponent>();
            foreach (var entity in entities)
            {
                var skillComp = entity.GetComponent<SkillComponent>();
                foreach (var slot in skillComp.SkillSlots)
                {
                    if (slot.CooldownTimer > 0)
                        slot.CooldownTimer -= deltaTime;
                }
            }
        }
        
        /// <summary>
        /// 更新施法进度
        /// </summary>
        private void UpdateCasting(float deltaTime)
        {
            var entities = World.Query<SkillComponent>();
            foreach (var entity in entities)
            {
                var skillComp = entity.GetComponent<SkillComponent>();
                if (!skillComp.IsCasting) continue;
                
                skillComp.CastTimer -= deltaTime;
                if (skillComp.CastTimer <= 0)
                {
                    // 施法完成，执行技能效果
                    ExecuteSkill(entity, skillComp.ActiveSkill);
                    skillComp.IsCasting = false;
                    skillComp.ActiveSkill = null;
                }
            }
        }
        
        /// <summary>
        /// 处理技能激活事件
        /// </summary>
        private void OnSkillActivate(SkillActivateEvent evt)
        {
            var entity = evt.Caster;
            var skillComp = entity.GetComponent<SkillComponent>();
            var health = entity.GetComponent<HealthComponent>();
            
            if (skillComp == null || evt.Slot == null || !evt.Slot.HasSkill) return;
            if (evt.Slot.IsOnCooldown) return;
            if (skillComp.IsCasting) return;
            
            var skill = evt.Slot.SkillData;
            
            // 检查魔力消耗
            if (health != null && !health.ConsumeMana(skill.ManaCost)) return;
            
            // 开始施法
            if (skill.CastTime > 0)
            {
                skillComp.IsCasting = true;
                skillComp.CastTimer = skill.CastTime;
                skillComp.ActiveSkill = evt.Slot;
            }
            else
            {
                // 瞬发技能
                ExecuteSkill(entity, evt.Slot);
            }
            
            // 设置冷却
            evt.Slot.CooldownTimer = skill.Cooldown;
            
            World.EventBus.Publish(new SkillCastStartEvent { Caster = entity, Slot = evt.Slot });
        }
        
        /// <summary>
        /// 执行技能效果
        /// </summary>
        private void ExecuteSkill(Entity caster, SkillSlot slot)
        {
            if (slot?.SkillData == null) return;
            var skill = slot.SkillData;
            var transform = caster.GetComponent<TransformComponent>();
            var input = caster.GetComponent<PlayerInputComponent>();
            
            Vector3 targetPos = input != null ? input.MouseWorldPosition : 
                                (transform != null ? transform.Position + transform.Forward * skill.Range : Vector3.zero);
            
            switch (skill.Type)
            {
                case SkillType.Projectile:
                    SpawnProjectiles(caster, skill, targetPos);
                    break;
                case SkillType.AoE:
                    ExecuteAoESkill(caster, skill, targetPos);
                    break;
                case SkillType.Attack:
                    ExecuteAttackSkill(caster, skill, targetPos);
                    break;
                case SkillType.Movement:
                    ExecuteMovementSkill(caster, skill, targetPos);
                    break;
            }
            
            World.EventBus.Publish(new SkillExecutedEvent { Caster = caster, Slot = slot, TargetPosition = targetPos });
        }
        
        private void SpawnProjectiles(Entity caster, SkillData skill, Vector3 targetPos)
        {
            var transform = caster.GetComponent<TransformComponent>();
            if (transform == null) return;
            
            int count = skill.ProjectileCount;
            // 应用多重投射支持宝石
            foreach (var gem in skill.SupportGems)
            {
                if (gem.Type == SupportGemType.MultiProjectile || gem.Type == SupportGemType.GMP)
                    count += (int)gem.Value;
            }
            
            Vector3 baseDir = (targetPos - transform.Position).normalized;
            float spreadAngle = count > 1 ? 15f : 0f;
            
            for (int i = 0; i < count; i++)
            {
                float angle = count > 1 ? Mathf.Lerp(-spreadAngle, spreadAngle, (float)i / (count - 1)) : 0f;
                Vector3 dir = Quaternion.Euler(0, angle, 0) * baseDir;
                
                World.EventBus.Publish(new SpawnProjectileEvent
                {
                    Caster = caster,
                    Skill = skill,
                    Origin = transform.Position,
                    Direction = dir,
                    Speed = skill.Range * 3f
                });
            }
        }
        
        private void ExecuteAoESkill(Entity caster, SkillData skill, Vector3 targetPos)
        {
            // 查找范围内的敌人并造成伤害
            var enemies = World.Query<HealthComponent, TransformComponent>();
            var stats = caster.GetComponent<StatsComponent>();
            float damage = stats != null ? stats.GetStat(StatType.PhysicalDamage) + skill.Damage : skill.Damage;
            
            foreach (var enemy in enemies)
            {
                if (enemy == caster) continue;
                var enemyTransform = enemy.GetComponent<TransformComponent>();
                float dist = Vector3.Distance(targetPos, enemyTransform.Position);
                
                if (dist <= skill.AreaRadius)
                {
                    World.EventBus.Publish(new DamageEvent
                    {
                        Source = caster,
                        Target = enemy,
                        BaseDamage = damage,
                        DamageType = DamageType.Physical
                    });
                }
            }
        }
        
        private void ExecuteAttackSkill(Entity caster, SkillData skill, Vector3 targetPos)
        {
            var combat = caster.GetComponent<CombatComponent>();
            if (combat?.CurrentTarget == null) return;
            
            var stats = caster.GetComponent<StatsComponent>();
            float damage = stats != null ? stats.GetStat(StatType.PhysicalDamage) + skill.Damage : skill.Damage;
            
            World.EventBus.Publish(new DamageEvent
            {
                Source = caster,
                Target = combat.CurrentTarget,
                BaseDamage = damage,
                DamageType = DamageType.Physical
            });
        }
        
        private void ExecuteMovementSkill(Entity caster, SkillData skill, Vector3 targetPos)
        {
            var transform = caster.GetComponent<TransformComponent>();
            if (transform == null) return;
            
            // 闪现到目标位置
            Vector3 dir = (targetPos - transform.Position).normalized;
            transform.Position += dir * skill.Range;
        }
        
        protected override void OnDispose()
        {
            World.EventBus.Unsubscribe<SkillActivateEvent>(OnSkillActivate);
        }
    }
    
    // 技能相关事件
    public struct SkillActivateEvent
    {
        public Entity Caster;
        public SkillSlot Slot;
    }
    
    public struct SkillCastStartEvent
    {
        public Entity Caster;
        public SkillSlot Slot;
    }
    
    public struct SkillExecutedEvent
    {
        public Entity Caster;
        public SkillSlot Slot;
        public Vector3 TargetPosition;
    }
    
    public struct SpawnProjectileEvent
    {
        public Entity Caster;
        public SkillData Skill;
        public Vector3 Origin;
        public Vector3 Direction;
        public float Speed;
    }
}
