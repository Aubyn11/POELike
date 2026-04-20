using UnityEngine;
using POELike.ECS.Core;
using POELike.ECS.Components;
using POELike.Game;

namespace POELike.ECS.Systems
{
    /// <summary>
    /// AI系统
    /// 处理敌人AI行为（状态机）
    /// 优先级：50（较早执行，为移动系统提供方向）
    /// </summary>
    public class AISystem : SystemBase
    {
        public override int Priority => 50;

        // 玩家实体引用
        private Entity             _playerEntity;
        private TransformComponent _playerTransform; // 缓存，避免每帧 GetComponent

        // 分桶更新：将实体分成 BucketCount 组，每帧只更新当前桶
        // 10000 实体 / 8 桶 = 每帧约 1250 实体
        private const int BucketCount = 8;
        private int _bucketIndex = 0;

        // ── 追击名额限制（基于距离阈值，全局稳定）──────────────────────
        // 每帧全量扫描距离，只让最近的 MaxChasers 只怪物追击
        // 全量扫描只读 float，极快（10000只约0.1ms）
        private const int MaxChasers = 30;          // 同时允许追击的最大数量
        private float _chaseDistThresholdSq = 0f;   // 第N近怪物的距离平方阈值

        // 追击阈值更新降频：每 N 帧全量扫描一次
        private const int ThresholdInterval = 3;
        private int _thresholdFrame = 0;

        // 编队分配只影响近距离围攻表现，不需要每帧都全量扫描全部 AI
        private const int FormationUpdateInterval = 2;
        private int _formationFrame = 0;

        // ── 巡逻激活范围（只让接近玩家的怪物巡逻，远处怪物保持静止）────

        private const float PatrolEnterMargin = 6f;   // 进入巡逻：检测圈外再放大 6m
        private const float PatrolExitMargin  = 12f;  // 退出巡逻：给更大的退出范围避免频繁切换

        // 用于求第N小值的临时缓冲（复用，无GC）
        private readonly System.Collections.Generic.List<float> _distBuffer
            = new System.Collections.Generic.List<float>(512);

        // 复用的 top-N 数组（避免每帧 new float[]）
        private readonly float[] _topNBuffer = new float[MaxChasers];

        // 零 GC Query 缓冲
        private readonly System.Collections.Generic.List<Entity> _queryBuffer
            = new System.Collections.Generic.List<Entity>(4096);

        // 追击编队：只对少量主动追击/攻击玩家的怪物分配站位，避免围攻时重叠
        private readonly System.Collections.Generic.List<ChaserFormationEntry> _formationEntries
            = new System.Collections.Generic.List<ChaserFormationEntry>(64);
        private readonly System.Collections.Generic.List<ChaserFormationEntry> _ringEntries
            = new System.Collections.Generic.List<ChaserFormationEntry>(32);
        private readonly System.Collections.Generic.HashSet<int> _assignedFormationEntities
            = new System.Collections.Generic.HashSet<int>();
        private int[] _ringSlotEntryIndices = new int[32];
        private bool[] _ringSlotUsed = new bool[32];

        private struct ChaserFormationEntry
        {
            public int                EntityId;
            public AIComponent        AI;
            public MonsterComponent   Monster;
            public MovementComponent  Movement;
            public TransformComponent Transform;
            public float              Angle;
            public float              SqDistToPlayer;
        }

        private sealed class ChaserDistanceComparer : System.Collections.Generic.IComparer<ChaserFormationEntry>
        {
            public static readonly ChaserDistanceComparer Instance = new ChaserDistanceComparer();

            public int Compare(ChaserFormationEntry x, ChaserFormationEntry y)
            {
                int distCompare = x.SqDistToPlayer.CompareTo(y.SqDistToPlayer);
                return distCompare != 0 ? distCompare : x.EntityId.CompareTo(y.EntityId);
            }
        }

        private sealed class ChaserAngleComparer : System.Collections.Generic.IComparer<ChaserFormationEntry>
        {
            public static readonly ChaserAngleComparer Instance = new ChaserAngleComparer();

            public int Compare(ChaserFormationEntry x, ChaserFormationEntry y)
            {
                int angleCompare = x.Angle.CompareTo(y.Angle);
                return angleCompare != 0 ? angleCompare : x.EntityId.CompareTo(y.EntityId);
            }
        }

        // ── AI 组件缓存列表（事件驱动，消除每帧 Query + GetComponent）──
        // 直接按下标访问，避免字典查找
        private readonly System.Collections.Generic.List<Entity>      _aiEntities = new System.Collections.Generic.List<Entity>(4096);
        private readonly System.Collections.Generic.List<AIComponent> _aiCache    = new System.Collections.Generic.List<AIComponent>(4096);
        private readonly System.Collections.Generic.HashSet<Entity>   _aiRegistered = new System.Collections.Generic.HashSet<Entity>();
        private bool _aiCacheDirty = true; // 首帧强制同步

        protected override void OnInitialize()
        {
            World.EventBus.Subscribe<EntityCreatedEvent>(OnEntityCreated);
            World.EventBus.Subscribe<EntityDestroyedEvent>(OnEntityDestroyed);
        }

        private void OnEntityCreated(EntityCreatedEvent evt)
        {
            if (evt.Entity.Tag == "Player")
            {
                _playerEntity    = evt.Entity;
                _playerTransform = evt.Entity.GetComponent<TransformComponent>();
            }
            // 新怪物创建时标记需要同步
            if (evt.Entity.Tag == "Monster") _aiCacheDirty = true;
        }

        private void OnEntityDestroyed(EntityDestroyedEvent evt)
        {
            if (evt.Entity.Tag != "Monster") return;
            if (!_aiRegistered.Contains(evt.Entity)) return;
            // 立即 swap-remove
            _aiRegistered.Remove(evt.Entity);
            int idx = _aiEntities.IndexOf(evt.Entity);
            if (idx < 0) return;
            int last = _aiEntities.Count - 1;
            _aiEntities[idx] = _aiEntities[last];
            _aiCache[idx]    = _aiCache[last];
            _aiEntities.RemoveAt(last);
            _aiCache.RemoveAt(last);
        }

        protected override void OnUpdate(float deltaTime)
        {
            // 延迟获取玩家（确保玩家已创建）
            // 注意：EntityCreatedEvent 在 CreateEntity 时触发，此时 TransformComponent 还未 AddComponent，
            // 所以 OnEntityCreated 里拿到的 _playerTransform 可能为 null，需要在此补充重试。
            if (_playerEntity == null)
            {
                _playerEntity = World.FindEntityByTag("Player");
            }
            if (_playerEntity != null && _playerTransform == null)
            {
                _playerTransform = _playerEntity.GetComponent<TransformComponent>();
            }

            // 分桶 deltaTime 补偿（当前桶实际跨越了 BucketCount 帧）
            float aiDelta = deltaTime * BucketCount;

            // ── 增量同步 AI 缓存（事件驱动，只在有新怪物时才执行 Query）──
            if (_aiCacheDirty)
            {
                _aiCacheDirty = false;
                World.Query<AIComponent>(_queryBuffer);
                foreach (var e in _queryBuffer)
                {
                    if (e.Tag != "Monster") continue;
                    if (_aiRegistered.Contains(e)) continue;
                    var ai = e.GetComponent<AIComponent>();
                    if (ai == null) continue;
                    _aiEntities.Add(e);
                    _aiCache.Add(ai);
                    _aiRegistered.Add(e);
                }
            }

            int total = _aiEntities.Count;
            if (total == 0) return;

            // ── 全量扫描：计算追击距离阈值（降频，每3帧一次）──────────
            _thresholdFrame++;
            if (_thresholdFrame >= ThresholdInterval)
            {
                _thresholdFrame = 0;
                UpdateChaseDistThreshold(total);
            }

            // 计算当前桶的范围
            int bucketSize  = (total + BucketCount - 1) / BucketCount;
            int startIdx    = _bucketIndex * bucketSize;
            int endIdx      = System.Math.Min(startIdx + bucketSize, total);
            _bucketIndex    = (_bucketIndex + 1) % BucketCount;

            for (int i = startIdx; i < endIdx; i++)
            {
                var entity    = _aiEntities[i];
                // 直接从缓存列表取，无字典查找
                var ai        = _aiCache[i];
                if (ai == null || !ai.IsEnabled) continue;

                // ── 首次访问时初始化组件缓存（只调用一次 GetComponent）──
                if (ai.CachedMovement == null)
                {
                    ai.CachedMovement  = entity.GetComponent<MovementComponent>();
                    ai.CachedTransform = entity.GetComponent<TransformComponent>();
                    ai.CachedHealth    = entity.GetComponent<HealthComponent>();
                    ai.CachedMonster   = entity.GetComponent<MonsterComponent>();
                }

                var movement  = ai.CachedMovement;
                var transform = ai.CachedTransform;
                var health    = ai.CachedHealth;

                if (movement == null || transform == null) continue;

                // 死亡检查
                if (health != null && !health.IsAlive)
                {
                    ai.CurrentState        = AIState.Dead;
                    movement.MoveDirection = Vector3.zero;
                    continue;
                }

                // 更新冷却
                if (ai.AttackCooldownTimer > 0)
                    ai.AttackCooldownTimer -= aiDelta;

                if (ai.AttackCooldownTimer < 0f)
                    ai.AttackCooldownTimer = 0f;

                ai.StateTimer += aiDelta;

                // 状态机更新
                switch (ai.CurrentState)
                {
                    case AIState.Idle:
                        UpdateIdleState(entity, ai, movement, transform, aiDelta);
                        break;
                    case AIState.Patrol:
                        UpdatePatrolState(entity, ai, movement, transform, aiDelta);
                        break;
                    case AIState.Chase:
                        UpdateChaseState(entity, ai, movement, transform, aiDelta);
                        break;
                    case AIState.Attack:
                        UpdateAttackState(entity, ai, movement, transform, aiDelta);
                        break;
                    case AIState.Flee:
                        UpdateFleeState(entity, ai, movement, transform, aiDelta);
                        break;
                    case AIState.Stunned:
                        movement.MoveDirection = Vector3.zero;
                        if (ai.StateTimer >= 1f)
                            TransitionTo(ai, AIState.Idle);
                        break;
                }
            }

            // 追击编队：只隔帧更新一次，减少 10000 怪时的全量 AI 扫描成本
            _formationFrame++;
            if (_formationFrame >= FormationUpdateInterval)
            {
                _formationFrame = 0;
                ApplyPlayerChaserFormation(deltaTime * FormationUpdateInterval);
            }
        }

        private void UpdateIdleState(Entity entity, AIComponent ai, MovementComponent movement, TransformComponent transform, float deltaTime)
        {
            movement.MoveDirection = Vector3.zero;

            if (CanDetectPlayer(ai, transform))
            {
                ai.Target = _playerEntity;
                TransitionTo(ai, AIState.Chase);
                return;
            }

            // 优化：远离玩家的怪物不再随机巡逻，避免 10000 怪时产生大规模无意义移动
            if (ai.StateTimer > 3f && CanPatrol(ai, transform))
            {
                ai.PatrolTarget = ai.SpawnPoint + new Vector3(
                    Random.Range(-5f, 5f), 0, Random.Range(-5f, 5f));
                TransitionTo(ai, AIState.Patrol);
            }
        }

        private void UpdatePatrolState(Entity entity, AIComponent ai, MovementComponent movement, TransformComponent transform, float deltaTime)
        {
            if (CanDetectPlayer(ai, transform))
            {
                ai.Target = _playerEntity;
                TransitionTo(ai, AIState.Chase);
                return;
            }

            // 玩家离得太远时直接回到 Idle，避免远处怪物长期游走占用 Movement/GPU 预算
            if (!CanPatrol(ai, transform))
            {
                movement.MoveDirection = Vector3.zero;
                TransitionTo(ai, AIState.Idle);
                return;
            }

            float dx = ai.PatrolTarget.x - transform.Position.x;
            float dz = ai.PatrolTarget.z - transform.Position.z;

            if (dx * dx + dz * dz < 0.25f) // 0.5² = 0.25
            {
                TransitionTo(ai, AIState.Idle);
                return;
            }

            float len = Mathf.Sqrt(dx * dx + dz * dz);
            movement.MoveDirection = new Vector3(dx / len, 0f, dz / len);
        }

        // 怪物碰撞直径（与 MovementSystem 保持一致）
        private const float MonsterDiam = MonsterSpawner.CollisionRadius * 2f;
        private const float PlayerBodyRadius        = 0.4f;
        private const float FormationSpacing        = MonsterDiam * 1.05f;
        private const float FormationRadiusInset    = MonsterDiam * 0.10f;
        private const float FormationRingGap        = MonsterDiam * 1.00f;
        private const float FormationStopDist       = 0.20f;
        private const float FormationEngageExtra    = MonsterDiam * 2.20f;
        private const float FormationMaxOutwardShift= MonsterDiam * 0.20f;
        private const float FormationApproachNear   = MonsterDiam * 0.35f;
        private const float FormationApproachFar    = MonsterDiam * 1.80f;
        private const float FormationMinSlotWeight  = 0.16f;
        private const float FormationMaxSlotWeight  = 0.48f;
        private const float FormationAttackSlotTolerance = MonsterDiam * 0.38f;
        private const float FormationAttackCountTolerance = MonsterDiam * 0.52f;
        private const float FormationAttackHoldTolerance  = MonsterDiam * 0.72f;
        private const float FormationRadialRecoverTolerance = MonsterDiam * 0.18f;
        private const float FormationAttackBreakExtraDist = MonsterDiam * 0.55f;
        private const float ChaseStopExtraDist      = MonsterDiam * 0.12f;
        private const float AttackPushExtraDist     = MonsterDiam * 0.05f;

        private void UpdateChaseState(Entity entity, AIComponent ai, MovementComponent movement, TransformComponent transform, float deltaTime)
        {
            if (ai.Target == null || !ai.Target.IsAlive)
            {
                TransitionTo(ai, AIState.Idle);
                return;
            }

            // 直接用缓存的玩家 TransformComponent，避免 GetComponent
            var targetTransform = (ai.Target == _playerEntity)
                ? _playerTransform
                : ai.Target.GetComponent<TransformComponent>();
            if (targetTransform == null) return;

            float dx     = transform.Position.x - targetTransform.Position.x;
            float dz     = transform.Position.z - targetTransform.Position.z;
            float sqDist = dx * dx + dz * dz;

            float chaseRangeSq  = ai.ChaseRange  * ai.ChaseRange;
            float attackRangeSq = ai.AttackRange * ai.AttackRange;

            if (sqDist > chaseRangeSq)
            {
                ai.Target = null;
                TransitionTo(ai, AIState.Idle);
                return;
            }

            if (sqDist <= attackRangeSq)
            {
                if (CanEnterAttackState(ai, transform))
                {
                    movement.MoveDirection = Vector3.zero;
                    TransitionTo(ai, AIState.Attack);
                    return;
                }
            }

            // ── 停止圈：已经足够近，停下等待 ──────────────────────────
            float stopDist   = ai.AttackRange + ChaseStopExtraDist;
            float stopDistSq = stopDist * stopDist;

            if (sqDist <= stopDistSq)
            {
                movement.MoveDirection = Vector3.zero;
                return;
            }

            // ── 距离阈值限制：只有最近的 MaxChasers 只怪物才能追击 ────
            // 加滞后区间（hysteresis）：进入追击需要 < 阈值，退出追击需要 > 阈值 * 1.3
            // 避免怪物在阈值边缘每帧反复切换追击/停止导致闪烁
            if (_chaseDistThresholdSq > 0f && _chaseDistThresholdSq < float.MaxValue)
            {
                bool currentlyMoving = movement.MoveDirection.sqrMagnitude > 0.001f;
                float exitThresholdSq = _chaseDistThresholdSq * 1.3f * 1.3f; // 退出阈值放大1.3倍
                if (currentlyMoving)
                {
                    // 已在追击中：超过退出阈值才停止（滞后）
                    if (sqDist > exitThresholdSq)
                    {
                        movement.MoveDirection = Vector3.zero;
                        return;
                    }
                }
                else
                {
                    // 未在追击：必须小于进入阈值才开始追击
                    if (sqDist > _chaseDistThresholdSq)
                    {
                        movement.MoveDirection = Vector3.zero;
                        return;
                    }
                }
            }

            float len = Mathf.Sqrt(sqDist);
            movement.MoveDirection = new Vector3(-dx / len, 0f, -dz / len);
        }

        private void UpdateAttackState(Entity entity, AIComponent ai, MovementComponent movement, TransformComponent transform, float deltaTime)
        {
            if (ai.Target == null || !ai.Target.IsAlive)
            {
                TransitionTo(ai, AIState.Idle);
                return;
            }

            var targetTransform = (ai.Target == _playerEntity)
                ? _playerTransform
                : ai.Target.GetComponent<TransformComponent>();
            if (targetTransform == null) return;

            float dx     = transform.Position.x - targetTransform.Position.x;
            float dz     = transform.Position.z - targetTransform.Position.z;
            float sqDist = dx * dx + dz * dz;
            float chaseRangeSq = ai.ChaseRange * ai.ChaseRange;

            movement.MoveDirection = Vector3.zero;

            if (!ai.HasAppliedAttackThisCycle)
            {
                ai.HasAppliedAttackThisCycle = true;
                World.EventBus.Publish(new AIAttackEvent { Attacker = entity, Target = ai.Target });
            }

            if (!HasAttackCycleCompleted(ai))
                return;

            if (sqDist > chaseRangeSq)
            {
                ai.Target = null;
                TransitionTo(ai, AIState.Idle);
                return;
            }

            TransitionTo(ai, AIState.Chase);
        }

        private void UpdateFleeState(Entity entity, AIComponent ai, MovementComponent movement, TransformComponent transform, float deltaTime)
        {
            if (ai.Target == null)
            {
                TransitionTo(ai, AIState.Idle);
                return;
            }

            var targetTransform = (ai.Target == _playerEntity)
                ? _playerTransform
                : ai.Target.GetComponent<TransformComponent>();
            if (targetTransform == null) return;

            float dx = transform.Position.x - targetTransform.Position.x;
            float dz = transform.Position.z - targetTransform.Position.z;
            float len = Mathf.Sqrt(dx * dx + dz * dz);
            if (len < 0.001f) return;
            movement.MoveDirection = new Vector3(dx / len, 0f, dz / len);
        }

        private void ApplyPlayerChaserFormation(float deltaTime)
        {
            if (_playerTransform == null) return;

            _formationEntries.Clear();
            Vector3 playerPos = _playerTransform.Position;
            float maxAttackRange = 0f;

            for (int i = 0; i < _aiCache.Count; i++)
            {
                var ai = _aiCache[i];
                if (ai == null || ai.Target != _playerEntity) continue;
                if (ai.CurrentState != AIState.Chase && ai.CurrentState != AIState.Attack) continue;

                var movement  = ai.CachedMovement;
                var transform = ai.CachedTransform;
                if (movement == null || transform == null) continue;
                if (!movement.IsEnabled || movement.IsImmobilized) continue;
                if (!ShouldParticipateInPlayerFormation(ai, movement, transform, playerPos)) continue;

                float dx = transform.Position.x - playerPos.x;
                float dz = transform.Position.z - playerPos.z;
                float sqDist = dx * dx + dz * dz;

                _formationEntries.Add(new ChaserFormationEntry
                {
                    EntityId      = _aiEntities[i].Id,
                    AI            = ai,
                    Monster       = ai.CachedMonster,
                    Movement      = movement,
                    Transform     = transform,
                    Angle         = Mathf.Atan2(dx, dz),
                    SqDistToPlayer = sqDist,
                });

                if (ai.AttackRange > maxAttackRange)
                    maxAttackRange = ai.AttackRange;
            }

            int activeCount = _formationEntries.Count;
            if (activeCount == 0) return;

            for (int i = 0; i < activeCount; i++)
            {
                var ai = _formationEntries[i].AI;
                ai.PreviousFormationRingIndex = ai.FormationRingIndex;
                ai.PreviousFormationSlotIndex = ai.FormationSlotIndex;
                ai.PreviousFormationRingCount = ai.FormationRingCount;
                ai.FormationRingIndex = -1;
                ai.FormationSlotIndex = -1;
                ai.FormationRingCount = 0;
            }

            if (activeCount == 1)
            {
                _formationEntries[0].AI.FormationRingIndex = 0;
                _formationEntries[0].AI.FormationSlotIndex = 0;
                _formationEntries[0].AI.FormationRingCount = 1;
                return;
            }

            _formationEntries.Sort(ChaserDistanceComparer.Instance);
            _assignedFormationEntities.Clear();

            float baseRadius = Mathf.Max(GetAttackRingRadius(maxAttackRange), MonsterDiam);
            int assignedCount = 0;
            int ringIndex = 0;

            while (assignedCount < activeCount)
            {
                float ringRadius = baseRadius + ringIndex * FormationRingGap;
                int ringCapacity = Mathf.Max(1, Mathf.FloorToInt((2f * Mathf.PI * ringRadius) / FormationSpacing));

                _ringEntries.Clear();

                if (ringIndex == 0)
                {
                    for (int i = 0; i < activeCount && _ringEntries.Count < ringCapacity; i++)
                    {
                        var entry = _formationEntries[i];
                        if (_assignedFormationEntities.Contains(entry.EntityId)) continue;
                        if (entry.AI.CurrentState != AIState.Attack) continue;

                        float attackBreakDist = entry.AI.AttackRange + FormationAttackBreakExtraDist;
                        if (entry.SqDistToPlayer > attackBreakDist * attackBreakDist) continue;

                        _ringEntries.Add(entry);
                        _assignedFormationEntities.Add(entry.EntityId);
                        entry.AI.FormationRingIndex = ringIndex;
                    }
                }

                for (int i = 0; i < activeCount && _ringEntries.Count < ringCapacity; i++)
                {
                    var entry = _formationEntries[i];
                    if (_assignedFormationEntities.Contains(entry.EntityId)) continue;
                    if (entry.AI.PreviousFormationRingIndex != ringIndex) continue;

                    _ringEntries.Add(entry);
                    _assignedFormationEntities.Add(entry.EntityId);
                    entry.AI.FormationRingIndex = ringIndex;
                }

                for (int i = 0; i < activeCount && _ringEntries.Count < ringCapacity; i++)
                {
                    var entry = _formationEntries[i];
                    if (_assignedFormationEntities.Contains(entry.EntityId)) continue;

                    _ringEntries.Add(entry);
                    _assignedFormationEntities.Add(entry.EntityId);
                    entry.AI.FormationRingIndex = ringIndex;
                }

                int ringCount = _ringEntries.Count;
                if (ringCount == 0)
                    break;

                _ringEntries.Sort(ChaserAngleComparer.Instance);

                float angleStep = (Mathf.PI * 2f) / ringCount;
                float startAngle = GetRingStartAngle(ringIndex, ringCount);
                AssignStableRingSlots(ringIndex, ringCount, startAngle);

                for (int slotIndex = 0; slotIndex < ringCount; slotIndex++)
                {
                    int ringEntryIndex = _ringSlotEntryIndices[slotIndex];
                    if (ringEntryIndex < 0) continue;

                    float slotAngle = ringCount == 1
                        ? startAngle
                        : startAngle + slotIndex * angleStep;

                    var entry = _ringEntries[ringEntryIndex];
                    if (ringIndex > 0 && entry.AI.CurrentState == AIState.Attack)
                    {
                        if (HasAttackCycleCompleted(entry.AI))
                            TransitionTo(entry.AI, AIState.Chase);
                    }

                    float currentRadius = Mathf.Sqrt(entry.SqDistToPlayer);
                    float slotRadius = ringRadius;
                    if (slotRadius > currentRadius)
                        slotRadius = Mathf.Min(slotRadius, currentRadius + FormationMaxOutwardShift);

                    Vector3 slotPos = new Vector3(
                        playerPos.x + Mathf.Sin(slotAngle) * slotRadius,
                        entry.Transform.Position.y,
                        playerPos.z + Mathf.Cos(slotAngle) * slotRadius);

                    ApplyFormationSlot(entry, slotPos, playerPos, deltaTime);
                }

                assignedCount += ringCount;
                ringIndex++;
            }
        }

        private bool ShouldParticipateInPlayerFormation(AIComponent ai, MovementComponent movement, TransformComponent transform, Vector3 playerPos)
        {
            if (ai.CurrentState == AIState.Attack) return true;
            if (ai.CurrentState != AIState.Chase) return false;

            float engageRadius = ai.AttackRange + FormationEngageExtra;
            float dx = transform.Position.x - playerPos.x;
            float dz = transform.Position.z - playerPos.z;
            return dx * dx + dz * dz <= engageRadius * engageRadius;
        }

        private bool CanEnterAttackState(AIComponent ai, TransformComponent transform)
        {
            if (ai.Target != _playerEntity || _playerTransform == null)
                return true;

            float dx = transform.Position.x - _playerTransform.Position.x;
            float dz = transform.Position.z - _playerTransform.Position.z;
            float radius = Mathf.Sqrt(dx * dx + dz * dz);
            float attackHoldDist = ai.AttackRange + AttackPushExtraDist;
            float attackRingRadius = GetAttackRingRadius(ai.AttackRange);
            float attackEnterTolerance = Mathf.Max(FormationAttackSlotTolerance, FormationAttackHoldTolerance);

            // 已经进入攻击距离时不再强制要求贴合编队环，否则会表现成“明明已经够近却一直追”
            if (radius > attackHoldDist && Mathf.Abs(radius - attackRingRadius) > attackEnterTolerance)
                return false;

            int attackCapacity = Mathf.Max(1, Mathf.FloorToInt((2f * Mathf.PI * attackRingRadius) / FormationSpacing));
            int attackerCount = 0;

            for (int i = 0; i < _aiCache.Count; i++)
            {
                var other = _aiCache[i];
                if (other == null || other == ai) continue;
                if (other.Target != _playerEntity || other.CurrentState != AIState.Attack) continue;

                var otherTransform = other.CachedTransform;
                if (otherTransform == null) continue;

                float odx = otherTransform.Position.x - _playerTransform.Position.x;
                float odz = otherTransform.Position.z - _playerTransform.Position.z;
                float otherRadius = Mathf.Sqrt(odx * odx + odz * odz);
                float otherAttackRingRadius = GetAttackRingRadius(other.AttackRange);
                float otherAttackHoldDist = other.AttackRange + AttackPushExtraDist;
                if (otherRadius <= otherAttackHoldDist
                    || Mathf.Abs(otherRadius - otherAttackRingRadius) <= FormationAttackCountTolerance)
                {
                    attackerCount++;
                    if (attackerCount >= attackCapacity)
                        return false;
                }
            }

            return true;
        }

        private float GetAttackRingRadius(float attackRange)
        {
            float minRadius = PlayerBodyRadius + MonsterSpawner.CollisionRadius + MonsterDiam * 0.08f;
            float desiredRadius = attackRange - FormationRadiusInset;
            return Mathf.Max(minRadius, desiredRadius);
        }

        private float GetRingStartAngle(int ringIndex, int ringCount)
        {
            if (ringCount <= 1) return 0f;
            return (ringIndex & 1) == 0 ? 0f : Mathf.PI / ringCount;
        }

        private void AssignStableRingSlots(int ringIndex, int ringCount, float startAngle)
        {
            EnsureRingSlotBuffers(ringCount);
            for (int i = 0; i < ringCount; i++)
            {
                _ringSlotEntryIndices[i] = -1;
                _ringSlotUsed[i] = false;
            }

            for (int entryIndex = 0; entryIndex < _ringEntries.Count; entryIndex++)
            {
                var ai = _ringEntries[entryIndex].AI;
                if (ai.PreviousFormationRingIndex != ringIndex || ai.PreviousFormationSlotIndex < 0)
                    continue;

                int preferredSlot = RemapRingSlotIndex(
                    ai.PreviousFormationSlotIndex,
                    ai.PreviousFormationRingCount,
                    ringCount);
                int assignedSlot = FindNearestFreeRingSlot(preferredSlot, ringCount);
                _ringSlotUsed[assignedSlot] = true;
                _ringSlotEntryIndices[assignedSlot] = entryIndex;
                ai.FormationSlotIndex = assignedSlot;
                ai.FormationRingCount = ringCount;
            }

            for (int entryIndex = 0; entryIndex < _ringEntries.Count; entryIndex++)
            {
                var entry = _ringEntries[entryIndex];
                var ai = entry.AI;
                if (ai.FormationSlotIndex >= 0)
                    continue;

                int preferredSlot = GetPreferredRingSlotIndex(entry.Angle, startAngle, ringCount);
                int assignedSlot = FindNearestFreeRingSlot(preferredSlot, ringCount);
                _ringSlotUsed[assignedSlot] = true;
                _ringSlotEntryIndices[assignedSlot] = entryIndex;
                ai.FormationSlotIndex = assignedSlot;
                ai.FormationRingCount = ringCount;
            }
        }

        private void EnsureRingSlotBuffers(int ringCount)
        {
            if (_ringSlotEntryIndices.Length >= ringCount)
                return;

            int newSize = _ringSlotEntryIndices.Length;
            while (newSize < ringCount)
                newSize *= 2;

            System.Array.Resize(ref _ringSlotEntryIndices, newSize);
            System.Array.Resize(ref _ringSlotUsed, newSize);
        }

        private int RemapRingSlotIndex(int previousSlotIndex, int previousRingCount, int ringCount)
        {
            if (ringCount <= 1)
                return 0;
            if (previousRingCount <= 1)
                return Mathf.Clamp(previousSlotIndex, 0, ringCount - 1);

            float normalized = ((float)previousSlotIndex + 0.5f) / previousRingCount;
            int mapped = Mathf.FloorToInt(normalized * ringCount);
            if (mapped >= ringCount)
                mapped = ringCount - 1;
            return Mathf.Clamp(mapped, 0, ringCount - 1);
        }

        private int GetPreferredRingSlotIndex(float angle, float startAngle, int ringCount)
        {
            if (ringCount <= 1)
                return 0;

            float angleStep = (Mathf.PI * 2f) / ringCount;
            float normalizedAngle = NormalizeAnglePositive(angle - startAngle);
            int slotIndex = Mathf.RoundToInt(normalizedAngle / angleStep) % ringCount;
            return slotIndex < 0 ? slotIndex + ringCount : slotIndex;
        }

        private int FindNearestFreeRingSlot(int preferredSlot, int ringCount)
        {
            if (ringCount <= 1)
                return 0;

            preferredSlot %= ringCount;
            if (preferredSlot < 0)
                preferredSlot += ringCount;

            if (!_ringSlotUsed[preferredSlot])
                return preferredSlot;

            for (int offset = 1; offset < ringCount; offset++)
            {
                int plus = preferredSlot + offset;
                if (plus >= ringCount)
                    plus -= ringCount;
                if (!_ringSlotUsed[plus])
                    return plus;

                int minus = preferredSlot - offset;
                if (minus < 0)
                    minus += ringCount;
                if (!_ringSlotUsed[minus])
                    return minus;
            }

            return preferredSlot;
        }

        private float NormalizeAnglePositive(float angle)
        {
            float twoPi = Mathf.PI * 2f;
            angle %= twoPi;
            if (angle < 0f)
                angle += twoPi;
            return angle;
        }

        private void UpdateMonsterFacing(ChaserFormationEntry entry, float playerDx, float playerDz, float deltaTime)
        {
            if (entry.Monster == null) return;

            float sqDist = playerDx * playerDx + playerDz * playerDz;
            if (sqDist <= 0.0001f) return;

            float targetYaw = Mathf.Atan2(playerDx, playerDz) * 57.29578f;
            float deltaYaw = Mathf.DeltaAngle(entry.Monster.FaceYaw, targetYaw);
            float step = 540f * deltaTime;

            if (deltaYaw > step) entry.Monster.FaceYaw += step;
            else if (deltaYaw < -step) entry.Monster.FaceYaw -= step;
            else entry.Monster.FaceYaw = targetYaw;
        }

        private void ApplyFormationSlot(ChaserFormationEntry entry, Vector3 slotPos, Vector3 playerPos, float deltaTime)
        {
            float slotDx = slotPos.x - entry.Transform.Position.x;
            float slotDz = slotPos.z - entry.Transform.Position.z;
            float slotSqDist = slotDx * slotDx + slotDz * slotDz;

            float playerDx = playerPos.x - entry.Transform.Position.x;
            float playerDz = playerPos.z - entry.Transform.Position.z;
            float playerSqDist = playerDx * playerDx + playerDz * playerDz;

            float playerLen = Mathf.Sqrt(playerSqDist);
            if (playerLen < 0.001f)
            {
                entry.Movement.MoveDirection = Vector3.zero;
                return;
            }

            UpdateMonsterFacing(entry, playerDx, playerDz, deltaTime);

            Vector3 directDir = new Vector3(playerDx / playerLen, 0f, playerDz / playerLen);
            bool isAttackRingMember = entry.AI.FormationRingIndex == 0;

            if (entry.AI.CurrentState == AIState.Attack)
            {
                float attackHoldDist = entry.AI.AttackRange + AttackPushExtraDist;
                float attackBreakDist = entry.AI.AttackRange + FormationAttackBreakExtraDist;
                float attackHoldSlotTol = Mathf.Max(FormationAttackSlotTolerance, FormationAttackHoldTolerance);
                float attackHoldSlotTolSq = attackHoldSlotTol * attackHoldSlotTol;

                if (playerSqDist <= attackHoldDist * attackHoldDist)
                {
                    entry.Movement.MoveDirection = Vector3.zero;
                    return;
                }

                if (isAttackRingMember && playerSqDist <= attackBreakDist * attackBreakDist)
                {
                    entry.Movement.MoveDirection = Vector3.zero;
                    return;
                }

                if (playerSqDist <= attackBreakDist * attackBreakDist && slotSqDist <= attackHoldSlotTolSq)
                {
                    entry.Movement.MoveDirection = Vector3.zero;
                    return;
                }

                if (entry.AI.AttackCooldownTimer > 0f)
                {
                    entry.Movement.MoveDirection = Vector3.zero;
                    return;
                }

                TransitionTo(entry.AI, AIState.Chase);
            }

            float chaseHoldDist = entry.AI.AttackRange + ChaseStopExtraDist;
            float attackEnterDist = entry.AI.AttackRange + AttackPushExtraDist;

            if (slotSqDist <= 0.0001f)
            {
                if (entry.AI.CurrentState == AIState.Chase
                    && playerSqDist <= attackEnterDist * attackEnterDist
                    && CanEnterAttackState(entry.AI, entry.Transform))
                {
                    entry.Movement.MoveDirection = Vector3.zero;
                    TransitionTo(entry.AI, AIState.Attack);
                    return;
                }

                entry.Movement.MoveDirection = Vector3.zero;
                return;
            }

            float slotLen = Mathf.Sqrt(slotSqDist);
            Vector3 slotDir = new Vector3(slotDx / slotLen, 0f, slotDz / slotLen);

            float slotPlayerDx = slotPos.x - playerPos.x;
            float slotPlayerDz = slotPos.z - playerPos.z;
            float slotRadius = Mathf.Sqrt(slotPlayerDx * slotPlayerDx + slotPlayerDz * slotPlayerDz);
            bool shouldRecoverToSlot = slotRadius > playerLen + FormationRadialRecoverTolerance;
            float slotStopDist = isAttackRingMember ? FormationStopDist : FormationStopDist * 1.35f;
            float slotStopDistSq = slotStopDist * slotStopDist;

            if (entry.AI.CurrentState == AIState.Chase && playerSqDist <= chaseHoldDist * chaseHoldDist)
            {
                if (playerSqDist <= attackEnterDist * attackEnterDist
                    && CanEnterAttackState(entry.AI, entry.Transform))
                {
                    entry.Movement.MoveDirection = Vector3.zero;
                    TransitionTo(entry.AI, AIState.Attack);
                    return;
                }

                // 大量怪物时，未拿到攻击名额的怪物不能停在玩家附近，必须回到分配的外环站位排队
                if (shouldRecoverToSlot || slotSqDist > slotStopDistSq)
                {
                    entry.Movement.MoveDirection = slotDir;
                    return;
                }

                entry.Movement.MoveDirection = Vector3.zero;
                return;
            }

            if (!isAttackRingMember)
            {
                // 外环只追随自己的站位，不再混入朝玩家方向的推进，避免向内挤压后又被分离弹回形成震动
                if (slotSqDist <= slotStopDistSq)
                {
                    entry.Movement.MoveDirection = Vector3.zero;
                    return;
                }

                entry.Movement.MoveDirection = slotDir;
                return;
            }

            if (slotSqDist <= slotStopDistSq)
            {
                if (playerSqDist <= chaseHoldDist * chaseHoldDist)
                {
                    entry.Movement.MoveDirection = Vector3.zero;
                    return;
                }

                entry.Movement.MoveDirection = directDir;
                return;
            }

            // 如果当前位置比目标环更靠里，优先往站位回收，避免朝玩家方向的权重把中心挤塌
            if (shouldRecoverToSlot)
            {
                entry.Movement.MoveDirection = slotDir;
                return;
            }

            float farDist = entry.AI.AttackRange + FormationApproachFar;
            float nearDist = entry.AI.AttackRange + FormationApproachNear;
            float slotWeight = 1f - Mathf.Clamp01((playerLen - nearDist) / Mathf.Max(0.001f, farDist - nearDist));
            slotWeight = Mathf.Lerp(FormationMinSlotWeight, FormationMaxSlotWeight, slotWeight);

            Vector3 desiredDir = directDir * (1f - slotWeight) + slotDir * slotWeight;
            if (desiredDir.sqrMagnitude <= 0.0001f)
            {
                entry.Movement.MoveDirection = directDir;
                return;
            }

            desiredDir.Normalize();
            if (playerLen <= farDist && Vector3.Dot(desiredDir, directDir) < 0.05f)
                desiredDir = (slotDir * 0.65f + directDir * 0.75f).normalized;

            entry.Movement.MoveDirection = desiredDir;
        }

        /// <summary>
        /// 全量扫描所有 Chase 状态怪物的距离，用快速选择找第 MaxChasers 近的距离作为阈值
        /// 直接访问 AIComponent 缓存，不调用 GetComponent，10000只约 0.05ms
        /// </summary>
        private void UpdateChaseDistThreshold(int total)
        {
            if (_playerTransform == null)
            {
                _chaseDistThresholdSq = 0f;
                return;
            }

            _distBuffer.Clear();
            Vector3 playerPos = _playerTransform.Position;

            // 只扫描当前桶已初始化缓存的实体（CachedTransform != null 说明已被处理过）
            // 注意：这里直接遍历全量 _queryBuffer，但只读 AIComponent 的缓存字段，不调用 GetComponent
            for (int i = 0; i < total; i++)
            {
                // 直接用缓存列表，无字典查找
                var ai = _aiCache[i];
                if (ai == null || ai.CurrentState != AIState.Chase) continue;

                var tc = ai.CachedTransform;
                if (tc == null) continue;

                float dx = tc.Position.x - playerPos.x;
                float dz = tc.Position.z - playerPos.z;
                _distBuffer.Add(dx * dx + dz * dz);
            }

            int count = _distBuffer.Count;
            if (count == 0)
            {
                _chaseDistThresholdSq = 0f;
                return;
            }

            if (count <= MaxChasers)
            {
                // 追击怪物数量未超限，全部允许追击
                _chaseDistThresholdSq = float.MaxValue;
                return;
            }

            // 用部分排序找第 MaxChasers 小的值（插入排序，只维护前N个最小值）
            // 对于 MaxChasers=30，内层最多30次比较，总体 O(N*30) 约30万次，极快
            // 复用成员数组，避免每帧 GC 分配
            var top = _topNBuffer;
            int filled = 0;
            float maxInTop = float.MinValue;
            int maxIdx = 0;

            for (int i = 0; i < count; i++)
            {
                float d = _distBuffer[i];
                if (filled < MaxChasers)
                {
                    top[filled] = d;
                    if (d > maxInTop) { maxInTop = d; maxIdx = filled; }
                    filled++;
                }
                else if (d < maxInTop)
                {
                    top[maxIdx] = d;
                    // 重新找最大值
                    maxInTop = float.MinValue;
                    for (int j = 0; j < MaxChasers; j++)
                        if (top[j] > maxInTop) { maxInTop = top[j]; maxIdx = j; }
                }
            }
            _chaseDistThresholdSq = maxInTop;
        }

        private bool CanDetectPlayer(AIComponent ai, TransformComponent transform)
        {
            if (_playerTransform == null) return false;

            return GetSqDistanceToPlayer(transform) <= ai.DetectionRange * ai.DetectionRange;
        }

        private bool CanPatrol(AIComponent ai, TransformComponent transform)
        {
            // 没有玩家时保持原有行为，允许怪物巡逻
            if (_playerTransform == null) return true;

            float patrolRange = ai.CurrentState == AIState.Patrol
                ? ai.DetectionRange + PatrolExitMargin
                : ai.DetectionRange + PatrolEnterMargin;

            return GetSqDistanceToPlayer(transform) <= patrolRange * patrolRange;
        }

        private float GetSqDistanceToPlayer(TransformComponent transform)
        {
            if (_playerTransform == null) return float.MaxValue;

            float dx = transform.Position.x - _playerTransform.Position.x;
            float dz = transform.Position.z - _playerTransform.Position.z;
            return dx * dx + dz * dz;
        }

        private void TransitionTo(AIComponent ai, AIState newState)
        {
            ai.CurrentState = newState;
            ai.StateTimer   = 0f;

            if (newState == AIState.Attack)
                StartAttackCycle(ai);
            else
                ai.HasAppliedAttackThisCycle = false;
        }

        private void StartAttackCycle(AIComponent ai)
        {
            ai.CurrentState = AIState.Attack;
            ai.StateTimer = 0f;
            ai.HasAppliedAttackThisCycle = false;
            ai.AttackCooldownTimer = Mathf.Max(0f, ai.AttackDuration) + Mathf.Max(0f, ai.AttackInterval);
        }

        private static bool HasAttackCycleCompleted(AIComponent ai)
        {
            return ai == null || ai.AttackCooldownTimer <= 0f;
        }

        protected override void OnDispose()
        {
            World.EventBus.Unsubscribe<EntityCreatedEvent>(OnEntityCreated);
            World.EventBus.Unsubscribe<EntityDestroyedEvent>(OnEntityDestroyed);
            _aiCache.Clear();
            _aiEntities.Clear();
            _aiRegistered.Clear();
        }
    }

    public struct AIAttackEvent
    {
        public Entity Attacker;
        public Entity Target;
    }
}