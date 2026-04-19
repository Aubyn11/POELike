using System.Collections.Generic;
using UnityEngine;
using POELike.ECS.Core;
using POELike.ECS.Components;
using POELike.Managers;

namespace POELike.ECS.Systems
{
    /// <summary>
    /// 技能 GPU 系统。
    /// 负责维护技能运行时实体，并将范围命中检测委托给 GPU。
    /// </summary>
    public class SkillGpuSystem : SystemBase
    {
        private const int HitStride = sizeof(uint);
        private const float DefaultTickInterval = 0.15f;

        private readonly List<Entity> _skillEntities = new List<Entity>(128);
        private readonly List<Entity> _hitTargets = new List<Entity>(512);

        private ComputeShader _rangeHitCompute;
        private int _kernelRangeHit = -1;
        private ComputeBuffer _hitResultBuffer;
        private uint[] _hitResults;
        private int _hitCapacity;

        private static readonly int ID_MonsterPositions = Shader.PropertyToID("_MonsterPositions");

        private static readonly int ID_HitResults = Shader.PropertyToID("_HitResults");
        private static readonly int ID_MonsterCount = Shader.PropertyToID("_MonsterCount");
        private static readonly int ID_SkillCenter = Shader.PropertyToID("_SkillCenter");
        private static readonly int ID_SkillRadius = Shader.PropertyToID("_SkillRadius");

        public override int Priority => 170;

        protected override void OnInitialize()
        {
            _rangeHitCompute = Resources.Load<ComputeShader>("Shaders/SkillRangeHitCompute");
            if (_rangeHitCompute != null)
                _kernelRangeHit = _rangeHitCompute.FindKernel("CSRangeHit");
            else
                Debug.LogWarning("[SkillGpuSystem] 找不到 SkillRangeHitCompute，将回退到 CPU 范围检测");
        }

        protected override void OnUpdate(float deltaTime)
        {
            World.Query<SkillRuntimeComponent>(_skillEntities);
            if (_skillEntities.Count == 0)
                return;

            var movementSystem = GameManager.Instance?.MovementSystem;
            for (int i = 0; i < _skillEntities.Count; i++)
            {
                var entity = _skillEntities[i];
                var runtime = entity.GetComponent<SkillRuntimeComponent>();
                var transform = entity.GetComponent<TransformComponent>();
                if (runtime == null || transform == null || !runtime.IsEnabled)
                    continue;

                UpdateSkillTransform(runtime, transform);
                runtime.RemainingTime -= deltaTime;

                if (runtime.WarmupRemaining > 0f)
                {
                    runtime.WarmupRemaining -= deltaTime;
                    if (runtime.WarmupRemaining > 0f)
                        continue;
                }

                runtime.TickTimer -= deltaTime;
                if (runtime.TickTimer > 0f && (!runtime.SingleImpact || runtime.HasTriggered))
                {
                    if (runtime.RemainingTime <= 0f)
                        World.DestroyEntity(entity);
                    continue;
                }

                ResolveHits(entity, runtime, transform, movementSystem);
                runtime.TickTimer = Mathf.Max(DefaultTickInterval, runtime.TickInterval);

                if (runtime.SingleImpact)
                    runtime.HasTriggered = true;

                if (runtime.RemainingTime <= 0f || (runtime.SingleImpact && runtime.HasTriggered))
                    World.DestroyEntity(entity);
            }
        }

        private void UpdateSkillTransform(SkillRuntimeComponent runtime, TransformComponent transform)
        {
            if (!runtime.FollowCaster || runtime.Caster == null || !runtime.Caster.IsAlive)
                return;

            var casterTransform = runtime.Caster.GetComponent<TransformComponent>();
            if (casterTransform == null)
                return;

            transform.Position = casterTransform.Position;
        }

        private void ResolveHits(Entity skillEntity, SkillRuntimeComponent runtime, TransformComponent transform, MovementSystem movementSystem)
        {
            runtime.LastResolvedCenter = transform.Position;
            _hitTargets.Clear();

            if (movementSystem != null
                && movementSystem.GpuPositionBuffer != null
                && movementSystem.MonsterCount > 0
                && _rangeHitCompute != null
                && _kernelRangeHit >= 0)
            {
                ResolveHitsByGpu(skillEntity, runtime, movementSystem);
                return;
            }

            ResolveHitsByCpu(skillEntity, runtime);
        }

        private void ResolveHitsByGpu(Entity skillEntity, SkillRuntimeComponent runtime, MovementSystem movementSystem)
        {
            int monsterCount = movementSystem.MonsterCount;
            EnsureHitCapacity(monsterCount);

            _rangeHitCompute.SetBuffer(_kernelRangeHit, ID_MonsterPositions, movementSystem.GpuPositionBuffer);
            _rangeHitCompute.SetBuffer(_kernelRangeHit, ID_HitResults, _hitResultBuffer);
            _rangeHitCompute.SetInt(ID_MonsterCount, monsterCount);
            _rangeHitCompute.SetVector(ID_SkillCenter, runtime.LastResolvedCenter);
            _rangeHitCompute.SetFloat(ID_SkillRadius, Mathf.Max(0.1f, runtime.AreaRadius));
            _rangeHitCompute.Dispatch(_kernelRangeHit, (monsterCount + 63) / 64, 1, 1);

            _hitResultBuffer.GetData(_hitResults, 0, 0, monsterCount);
            ApplyHitResults(skillEntity, runtime, movementSystem, monsterCount);
        }

        private void ResolveHitsByCpu(Entity skillEntity, SkillRuntimeComponent runtime)
        {
            var monsters = World.Query<MonsterComponent, TransformComponent>();
            for (int i = 0; i < monsters.Count; i++)
            {
                var target = monsters[i];
                if (target == null || !target.IsAlive || target == runtime.Caster)
                    continue;

                var targetTransform = target.GetComponent<TransformComponent>();
                if (targetTransform == null)
                    continue;

                Vector3 targetPos = targetTransform.Position;
                Vector2 delta = new Vector2(targetPos.x - runtime.LastResolvedCenter.x, targetPos.z - runtime.LastResolvedCenter.z);
                if (delta.sqrMagnitude <= runtime.AreaRadius * runtime.AreaRadius)
                    _hitTargets.Add(target);
            }

            PublishDamageEvents(skillEntity, runtime, _hitTargets);
        }

        private void ApplyHitResults(Entity skillEntity, SkillRuntimeComponent runtime, MovementSystem movementSystem, int monsterCount)
        {
            _hitTargets.Clear();
            var monsterEntities = movementSystem.MonsterEntitiesShared;

            for (int i = 0; i < monsterCount; i++)
            {
                if (_hitResults[i] == 0u)
                    continue;

                var targetEntity = i < monsterEntities.Count ? monsterEntities[i] : null;
                if (targetEntity == null || !targetEntity.IsAlive || targetEntity == runtime.Caster)
                    continue;

                _hitTargets.Add(targetEntity);
            }

            PublishDamageEvents(skillEntity, runtime, _hitTargets);
        }

        private void PublishDamageEvents(Entity skillEntity, SkillRuntimeComponent runtime, List<Entity> targets)
        {
            if (targets == null || targets.Count == 0)
                return;

            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target == null || !target.IsAlive)
                    continue;

                World.EventBus.Publish(new DamageEvent
                {
                    Source = runtime.Caster ?? skillEntity,
                    Target = target,
                    BaseDamage = Mathf.Max(1f, runtime.Damage),
                    DamageType = runtime.DamageType,
                });
            }
        }

        private void EnsureHitCapacity(int count)
        {
            if (_hitCapacity >= count)
                return;

            int capacity = Mathf.Max(64, Mathf.NextPowerOfTwo(count));
            _hitResultBuffer?.Release();
            _hitResultBuffer = new ComputeBuffer(capacity, HitStride);
            _hitResults = new uint[capacity];
            _hitCapacity = capacity;
        }

        protected override void OnDispose()
        {
            _hitResultBuffer?.Release();
            _hitResultBuffer = null;
            _hitResults = null;
            _hitCapacity = 0;
        }
    }
}
