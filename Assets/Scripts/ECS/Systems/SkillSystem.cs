using System.Collections.Generic;
using UnityEngine;
using POELike.ECS.Core;
using POELike.ECS.Components;
using POELike.Game.Skills;

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

        private readonly List<Entity> _skillEntities = new List<Entity>(8);
        
        protected override void OnInitialize()
        {
            World.EventBus.Subscribe<SkillActivateEvent>(OnSkillActivate);
        }
        
        protected override void OnUpdate(float deltaTime)
        {
            UpdateSkills(deltaTime);
        }

        private void UpdateSkills(float deltaTime)
        {
            World.Query<SkillComponent>(_skillEntities);
            foreach (var entity in _skillEntities)
            {
                var skillComp = entity.GetComponent<SkillComponent>();
                if (skillComp == null)
                    continue;

                for (int i = 0; i < skillComp.SkillSlots.Count; i++)
                {
                    var slot = skillComp.SkillSlots[i];
                    if (slot != null && slot.CooldownTimer > 0f)
                        slot.CooldownTimer = Mathf.Max(0f, slot.CooldownTimer - deltaTime);
                }

                if (skillComp.IsChanneling)
                    UpdateChanneling(entity, skillComp, deltaTime);
                else if (skillComp.IsCasting)
                    UpdateCasting(entity, skillComp, deltaTime);
                else
                    ReleaseMovementLock(entity, skillComp);
            }
        }
        
        /// <summary>
        /// 处理技能激活事件
        /// </summary>
        private void OnSkillActivate(SkillActivateEvent evt)
        {
            var entity = evt.Caster;
            var skillComp = entity?.GetComponent<SkillComponent>();
            var health = entity?.GetComponent<HealthComponent>();
            
            if (entity == null || skillComp == null || evt.Slot == null || !evt.Slot.HasSkill)
                return;
            if (evt.Slot.IsOnCooldown)
                return;

            var skill = evt.Slot.SkillData;
            if (skill == null)
                return;

            if (skillComp.IsChanneling)
            {
                if (skillComp.ActiveSkill == evt.Slot)
                    return;

                StopChanneling(entity, skillComp);
            }

            if (skillComp.IsCasting)
                return;

            if (skill.IsChannelingSkill)
            {
                StartChanneling(entity, skillComp, evt.Slot, health);
                return;
            }

            if (health != null && !health.ConsumeMana(skill.ManaCost))
                return;

            if (skill.CastTime > 0f)
            {
                skillComp.IsCasting = true;
                skillComp.CastTimer = skill.CastTime;
                skillComp.ActiveSkill = evt.Slot;
                ApplyMovementLock(entity, skillComp, skill);
            }
            else
            {
                ExecuteSkill(entity, evt.Slot);
                ReleaseMovementLock(entity, skillComp);
            }

            evt.Slot.CooldownTimer = skill.Cooldown;
            World.EventBus.Publish(new SkillCastStartEvent { Caster = entity, Slot = evt.Slot });
        }

        
        private void UpdateCasting(Entity caster, SkillComponent skillComp, float deltaTime)
        {
            if (skillComp?.ActiveSkill?.SkillData == null)
            {
                ResetCastingState(caster, skillComp);
                return;
            }

            skillComp.CastTimer -= deltaTime;
            if (skillComp.CastTimer > 0f)
            {
                ApplyMovementLock(caster, skillComp, skillComp.ActiveSkill.SkillData);
                return;
            }

            ExecuteSkill(caster, skillComp.ActiveSkill);
            ResetCastingState(caster, skillComp);
        }

        private void UpdateChanneling(Entity caster, SkillComponent skillComp, float deltaTime)
        {
            var slot = skillComp?.ActiveSkill;
            var skill = slot?.SkillData;
            if (skill == null)
            {
                StopChanneling(caster, skillComp);
                return;
            }

            var input = caster?.GetComponent<PlayerInputComponent>();
            if (input == null)
            {
                StopChanneling(caster, skillComp);
                return;
            }

            int slotIndex = slot.SlotIndex;
            bool stillHolding = slotIndex >= 0
                && slotIndex < input.SkillHeldInputs.Length
                && input.SkillHeldInputs[slotIndex]
                && !input.SkillReleasedInputs[slotIndex];
            if (!stillHolding)
            {
                StopChanneling(caster, skillComp);
                return;
            }

            ApplyMovementLock(caster, skillComp, skill);
            EnsureChannelRuntime(caster, skillComp, slot);

            var health = caster.GetComponent<HealthComponent>();
            skillComp.ChannelTickTimer -= deltaTime;
            if (skillComp.ChannelTickTimer > 0f)
                return;

            if (health != null && !health.ConsumeMana(skill.ManaCost))
            {
                StopChanneling(caster, skillComp);
                return;
            }

            skillComp.ChannelTickTimer = ResolveChannelInterval(skill);

            if (!IsPersistentChannelRuntime(skill))
                ExecuteSkill(caster, slot);
        }

        private void StartChanneling(Entity caster, SkillComponent skillComp, SkillSlot slot, HealthComponent health)
        {
            if (caster == null || skillComp == null || slot?.SkillData == null)
                return;

            var skill = slot.SkillData;
            if (health != null && !health.ConsumeMana(skill.ManaCost))
                return;

            StopChanneling(caster, skillComp);

            skillComp.ActiveSkill = slot;
            skillComp.ActiveChannelRuntime = null;
            skillComp.IsCasting = false;
            skillComp.CastTimer = 0f;
            skillComp.IsChanneling = true;
            skillComp.ChannelTickTimer = ResolveChannelInterval(skill);

            slot.CooldownTimer = skill.Cooldown;
            ApplyMovementLock(caster, skillComp, skill);
            World.EventBus.Publish(new SkillCastStartEvent { Caster = caster, Slot = slot });

            if (IsPersistentChannelRuntime(skill))
                skillComp.ActiveChannelRuntime = ExecuteSkill(caster, slot);
            else
                ExecuteSkill(caster, slot);
        }

        private void StopChanneling(Entity caster, SkillComponent skillComp)
        {
            if (skillComp == null)
                return;

            if (skillComp.ActiveChannelRuntime != null && skillComp.ActiveChannelRuntime.IsAlive)
                World.DestroyEntity(skillComp.ActiveChannelRuntime);

            skillComp.ActiveChannelRuntime = null;
            skillComp.IsChanneling = false;
            skillComp.ChannelTickTimer = 0f;
            skillComp.ActiveSkill = null;
            skillComp.IsCasting = false;
            skillComp.CastTimer = 0f;
            ReleaseMovementLock(caster, skillComp);
        }

        private void EnsureChannelRuntime(Entity caster, SkillComponent skillComp, SkillSlot slot)
        {
            if (caster == null || skillComp == null || slot?.SkillData == null)
                return;

            if (!IsPersistentChannelRuntime(slot.SkillData))
                return;

            if (skillComp.ActiveChannelRuntime != null && skillComp.ActiveChannelRuntime.IsAlive)
                return;

            skillComp.ActiveChannelRuntime = ExecuteSkill(caster, slot);
        }

        private void ResetCastingState(Entity caster, SkillComponent skillComp)
        {
            if (skillComp == null)
                return;

            skillComp.IsCasting = false;
            skillComp.CastTimer = 0f;
            skillComp.ActiveSkill = null;
            ReleaseMovementLock(caster, skillComp);
        }

        private static float ResolveChannelInterval(SkillData skill)
        {
            if (skill == null)
                return 0.15f;

            if (skill.Duration > 0.01f)
                return Mathf.Max(0.12f, skill.Duration);

            return 0.15f;
        }

        private static bool IsPersistentChannelRuntime(SkillData skill)
        {
            return skill != null && skill.Type == SkillType.Channeling;
        }

        private static void ApplyMovementLock(Entity caster, SkillComponent skillComp, SkillData skill)
        {
            var movement = caster?.GetComponent<MovementComponent>();
            if (movement == null)
                return;

            bool shouldLockMovement = (skillComp?.IsCasting ?? false) || (skillComp?.IsChanneling ?? false);
            shouldLockMovement &= skill != null && !skill.CanMoveWhileCasting;
            movement.IsMovementLockedByCasting = shouldLockMovement;
            if (!shouldLockMovement)
                return;

            movement.HasTarget = false;
            movement.MoveDirection = Vector3.zero;
        }

        private static void ReleaseMovementLock(Entity caster, SkillComponent skillComp)
        {
            if (skillComp != null && (skillComp.IsCasting || skillComp.IsChanneling))
                return;

            var movement = caster?.GetComponent<MovementComponent>();
            if (movement == null)
                return;

            movement.IsMovementLockedByCasting = false;
        }

        /// <summary>
        /// 执行技能效果
        /// </summary>
        private Entity ExecuteSkill(Entity caster, SkillSlot slot)

        {
            if (slot?.SkillData == null)
                return null;

            Vector3 targetPosition = ResolveSkillTargetPosition(caster, slot.SkillData);
            Entity runtimeEntity = ExecuteSkillAtTarget(caster, slot.SkillData, targetPosition);
            World.EventBus.Publish(new SkillExecutedEvent
            {
                Caster = caster,
                Slot = slot,
                TargetPosition = targetPosition
            });
            return runtimeEntity;
        }

        public Entity ExecuteSkillAtTarget(Entity caster, SkillData skill, Vector3 targetPos, string runtimePrefabKey = "")
        {
            if (caster == null || skill == null)
                return null;

            Entity runtimeEntity = null;
            var transform = caster.GetComponent<TransformComponent>();
            Vector3 casterPosition = transform != null ? transform.Position : Vector3.zero;
            SkillEffectPool.PlaySkillEffect(skill, casterPosition, targetPos);

            switch (skill.Type)
            {
                case SkillType.Projectile:
                    SpawnProjectiles(caster, skill, targetPos, runtimePrefabKey);
                    break;
                case SkillType.AoE:
                    runtimeEntity = CreateRuntimeSkillEntity(caster, skill, targetPos, runtimePrefabKey);
                    break;
                case SkillType.Channeling:
                    runtimeEntity = CreateRuntimeSkillEntity(caster, skill, casterPosition, runtimePrefabKey);
                    break;
                case SkillType.Attack:
                    ExecuteAttackSkill(caster, skill, targetPos);
                    break;
                case SkillType.Movement:
                    ExecuteMovementSkill(caster, skill, targetPos);
                    break;
                default:
                    if (skill.AreaRadius > 0.05f || skill.Duration > 0.05f)
                        runtimeEntity = CreateRuntimeSkillEntity(caster, skill, targetPos, runtimePrefabKey);
                    else
                        ExecuteAttackSkill(caster, skill, targetPos);
                    break;
            }

            return runtimeEntity;
        }

        private Vector3 ResolveSkillTargetPosition(Entity caster, SkillData skill)
        {
            var transform = caster?.GetComponent<TransformComponent>();
            var input = caster?.GetComponent<PlayerInputComponent>();

            if (input != null && input.MouseWorldPosition != Vector3.zero)
                return input.MouseWorldPosition;

            if (transform != null)
                return transform.Position + transform.Forward * Mathf.Max(1f, skill?.Range ?? 1f);

            return Vector3.zero;
        }

        private void SpawnProjectiles(Entity caster, SkillData skill, Vector3 targetPos, string runtimePrefabKey)
        {
            var transform = caster.GetComponent<TransformComponent>();
            if (transform == null)
                return;

            int count = Mathf.Max(1, skill.ProjectileCount);
            foreach (var gem in skill.SupportGems)
            {
                if (gem.Type == SupportGemType.MultiProjectile || gem.Type == SupportGemType.GMP)
                    count += Mathf.Max(0, (int)gem.Value);
            }

            Vector3 baseDir = (targetPos - transform.Position).normalized;
            if (baseDir.sqrMagnitude <= 0.0001f)
                baseDir = transform.Forward.sqrMagnitude > 0.0001f ? transform.Forward.normalized : Vector3.forward;

            float spreadAngle = count > 1 ? 15f : 0f;
            for (int i = 0; i < count; i++)
            {
                float angle = count > 1 ? Mathf.Lerp(-spreadAngle, spreadAngle, (float)i / (count - 1)) : 0f;
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * baseDir;
                Vector3 projectileTargetPos = transform.Position + dir * Mathf.Max(1f, skill.Range);

                CreateRuntimeSkillEntity(caster, skill, projectileTargetPos, string.IsNullOrWhiteSpace(runtimePrefabKey) ? "ProjectileTempPrefab" : runtimePrefabKey);
                World.EventBus.Publish(new SpawnProjectileEvent
                {
                    Caster = caster,
                    Skill = skill,
                    Origin = transform.Position,
                    Direction = dir,
                    Speed = Mathf.Max(6f, skill.Range * 3f)
                });
            }
        }

        private Entity CreateRuntimeSkillEntity(Entity caster, SkillData skill, Vector3 targetPos, string runtimePrefabKey)
        {
            if (caster == null || skill == null)
                return null;

            var casterTransform = caster.GetComponent<TransformComponent>();
            Vector3 casterPosition = casterTransform != null ? casterTransform.Position : Vector3.zero;
            Vector3 spawnPosition = ResolveRuntimeSpawnPosition(skill, casterPosition, targetPos);

            var runtimeEntity = World.CreateEntity("SkillRuntime");
            runtimeEntity.AddComponent(new TransformComponent
            {
                Position = spawnPosition
            });

            runtimeEntity.AddComponent(new SkillRuntimeComponent
            {
                Caster = caster,
                Skill = skill,
                DamageType = ResolveDamageType(skill),
                Damage = ResolveRuntimeDamage(caster, skill),
                AreaRadius = ResolveRuntimeRadius(skill),
                RemainingTime = ResolveRuntimeLifetime(skill),
                TotalLifetime = ResolveRuntimeLifetime(skill),
                TickInterval = ResolveRuntimeTickInterval(skill),
                TickTimer = 0f,
                WarmupRemaining = ResolveRuntimeWarmup(skill, casterPosition, targetPos),
                SingleImpact = skill.Type != SkillType.Channeling,
                HasTriggered = false,
                FollowCaster = skill.Type == SkillType.Channeling,
                DisplayColor = ResolveSkillDisplayColor(skill),
                RuntimePrefabKey = string.IsNullOrWhiteSpace(runtimePrefabKey) ? ResolveRuntimePrefabKey(skill) : runtimePrefabKey,
                LastResolvedCenter = spawnPosition,
            });

            return runtimeEntity;
        }

        private static Vector3 ResolveRuntimeSpawnPosition(SkillData skill, Vector3 casterPosition, Vector3 targetPos)
        {
            return skill.Type == SkillType.Channeling ? casterPosition : targetPos;
        }

        private static float ResolveRuntimeLifetime(SkillData skill)
        {
            if (skill == null)
                return 0.35f;

            if (skill.Type == SkillType.Channeling)
                return Mathf.Max(1.2f, skill.Duration > 0.01f ? skill.Duration * 4f : 1.2f);

            return Mathf.Max(0.35f, skill.Duration > 0.01f ? skill.Duration : 0.35f);
        }

        private static float ResolveRuntimeTickInterval(SkillData skill)
        {
            if (skill == null)
                return 0.15f;

            if (skill.Type == SkillType.Channeling)
                return Mathf.Max(0.12f, skill.Duration > 0.01f ? skill.Duration : 0.18f);

            return Mathf.Max(0.12f, skill.Duration > 0.01f ? skill.Duration : 0.15f);
        }

        private static float ResolveRuntimeWarmup(SkillData skill, Vector3 casterPosition, Vector3 targetPos)
        {
            if (skill == null || skill.Type != SkillType.Projectile)
                return 0f;

            float distance = Vector3.Distance(casterPosition, targetPos);
            float speed = Mathf.Max(6f, skill.Range * 3f);
            return Mathf.Clamp(distance / speed, 0.05f, 0.45f);
        }

        private static float ResolveRuntimeRadius(SkillData skill)
        {
            if (skill == null)
                return 1f;

            float radius = skill.AreaRadius > 0.01f
                ? skill.AreaRadius
                : Mathf.Max(1.2f, skill.Range * 0.12f);

            foreach (var gem in skill.SupportGems)
            {
                if (gem.Type == SupportGemType.IncreasedAoE)
                    radius *= 1f + Mathf.Max(0f, gem.Value) / 100f;
                else if (gem.Type == SupportGemType.ConcentratedEffect)
                    radius *= 0.8f;
            }

            return Mathf.Max(0.8f, radius);
        }

        private static float ResolveRuntimeDamage(Entity caster, SkillData skill)
        {
            var stats = caster?.GetComponent<StatsComponent>();
            float damage = stats != null ? stats.GetStat(StatType.PhysicalDamage) + skill.Damage : skill.Damage;

            foreach (var gem in skill.SupportGems)
            {
                if (gem.Type == SupportGemType.AddedFireDamage
                    || gem.Type == SupportGemType.AddedColdDamage
                    || gem.Type == SupportGemType.AddedLightningDamage)
                {
                    damage += gem.Value;
                }
            }

            return Mathf.Max(1f, damage);
        }

        private static DamageType ResolveDamageType(SkillData skill)
        {
            string keyword = $"{skill?.Id} {skill?.Name} {skill?.SkillEffectName}".ToLowerInvariant();
            if (keyword.Contains("fire") || keyword.Contains("火"))
                return DamageType.Fire;
            if (keyword.Contains("frost") || keyword.Contains("ice") || keyword.Contains("cold") || keyword.Contains("冰"))
                return DamageType.Cold;
            if (keyword.Contains("lightning") || keyword.Contains("雷"))
                return DamageType.Lightning;
            if (keyword.Contains("chaos") || keyword.Contains("混沌"))
                return DamageType.Chaos;
            return DamageType.Physical;
        }

        private static Color ResolveSkillDisplayColor(SkillData skill)
        {
            return ResolveDamageType(skill) switch
            {
                DamageType.Fire => new Color(1.00f, 0.34f, 0.12f, 0.35f),
                DamageType.Cold => new Color(0.28f, 0.68f, 1.00f, 0.35f),
                DamageType.Lightning => new Color(1.00f, 0.92f, 0.30f, 0.35f),
                DamageType.Chaos => new Color(0.70f, 0.30f, 1.00f, 0.35f),
                _ => new Color(1.00f, 1.00f, 1.00f, 0.30f),
            };
        }

        private static string ResolveRuntimePrefabKey(SkillData skill)
        {
            if (skill == null)
                return "SkillTempPrefab";

            return skill.Type switch
            {
                SkillType.Projectile => "ProjectileTempPrefab",
                SkillType.AoE => "AoETempPrefab",
                SkillType.Channeling => "ChannelSkillTempPrefab",
                _ => "SkillTempPrefab",
            };
        }
        
        private void ExecuteAttackSkill(Entity caster, SkillData skill, Vector3 targetPos)
        {
            var combat = caster.GetComponent<CombatComponent>();
            if (combat?.CurrentTarget == null)
                return;
            
            World.EventBus.Publish(new DamageEvent
            {
                Source = caster,
                Target = combat.CurrentTarget,
                BaseDamage = ResolveRuntimeDamage(caster, skill),
                DamageType = ResolveDamageType(skill)
            });
        }
        
        private void ExecuteMovementSkill(Entity caster, SkillData skill, Vector3 targetPos)
        {
            var transform = caster.GetComponent<TransformComponent>();
            if (transform == null)
                return;
            
            Vector3 dir = (targetPos - transform.Position).normalized;
            if (dir.sqrMagnitude <= 0.0001f)
                return;

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