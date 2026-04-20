using POELike.ECS.Core;

namespace POELike.ECS.Components
{
    /// <summary>
    /// AI组件
    /// 控制敌人的AI行为
    /// </summary>
    public class AIComponent : IComponent
    {
        public bool IsEnabled { get; set; } = true;

        /// <summary>AI状态机当前状态</summary>
        public AIState CurrentState { get; set; } = AIState.Idle;

        /// <summary>检测范围（发现玩家的距离）</summary>
        public float DetectionRange { get; set; } = 10f;

        /// <summary>攻击范围</summary>
        public float AttackRange { get; set; } = 2f;

        /// <summary>单次攻击持续时间</summary>
        public float AttackDuration { get; set; } = 0.5f;

        /// <summary>两次攻击之间的间隔时间</summary>
        public float AttackInterval { get; set; } = 1f;

        /// <summary>追击范围（超出此范围放弃追击）</summary>
        public float ChaseRange { get; set; } = 20f;

        /// <summary>当前目标</summary>
        public Entity Target { get; set; }

        /// <summary>巡逻目标点</summary>
        public UnityEngine.Vector3 PatrolTarget { get; set; }

        /// <summary>出生点（用于重置位置）</summary>
        public UnityEngine.Vector3 SpawnPoint { get; set; }

        /// <summary>状态计时器</summary>
        public float StateTimer { get; set; } = 0f;

        /// <summary>攻击冷却</summary>
        public float AttackCooldown { get; set; } = 1f;

        /// <summary>攻击冷却计时器</summary>
        public float AttackCooldownTimer { get; set; } = 0f;

        /// <summary>当前攻击轮次是否已经触发过伤害事件</summary>
        public bool HasAppliedAttackThisCycle { get; set; } = false;

        /// <summary>上一帧分配的编队环索引（-1 表示未参与）</summary>
        public int PreviousFormationRingIndex { get; set; } = -1;

        /// <summary>当前帧分配的编队环索引（-1 表示未参与）</summary>
        public int FormationRingIndex { get; set; } = -1;

        /// <summary>上一帧分配的环内槽位索引（-1 表示未参与）</summary>
        public int PreviousFormationSlotIndex { get; set; } = -1;

        /// <summary>当前帧分配的环内槽位索引（-1 表示未参与）</summary>
        public int FormationSlotIndex { get; set; } = -1;

        /// <summary>上一帧所在编队环的槽位总数</summary>
        public int PreviousFormationRingCount { get; set; } = 0;

        /// <summary>当前帧所在编队环的槽位总数</summary>
        public int FormationRingCount { get; set; } = 0;

        // ── 组件缓存（AISystem 首次访问时初始化，避免每帧 GetComponent 字典查找）──
        public MovementComponent CachedMovement  { get; set; }
        public TransformComponent CachedTransform { get; set; }
        public HealthComponent    CachedHealth    { get; set; }
        public MonsterComponent   CachedMonster   { get; set; }

        public void Reset()
        {
            CurrentState        = AIState.Idle;
            Target              = null;
            StateTimer          = 0f;
            AttackCooldownTimer = 0f;
            HasAppliedAttackThisCycle = false;
            PreviousFormationRingIndex = -1;
            FormationRingIndex  = -1;
            PreviousFormationSlotIndex = -1;
            FormationSlotIndex  = -1;
            PreviousFormationRingCount = 0;
            FormationRingCount  = 0;
            CachedMovement      = null;
            CachedTransform     = null;
            CachedHealth        = null;
            CachedMonster       = null;
        }
    }

    /// <summary>AI状态枚举</summary>
    public enum AIState
    {
        Idle,       // 待机
        Patrol,     // 巡逻
        Chase,      // 追击
        Attack,     // 攻击
        Flee,       // 逃跑
        Dead,       // 死亡
        Stunned,    // 眩晕
    }
}