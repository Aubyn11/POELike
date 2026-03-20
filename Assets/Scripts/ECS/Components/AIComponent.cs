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
        
        /// <summary>
        /// AI状态机当前状态
        /// </summary>
        public AIState CurrentState { get; set; } = AIState.Idle;
        
        /// <summary>
        /// 检测范围（发现玩家的距离）
        /// </summary>
        public float DetectionRange { get; set; } = 10f;
        
        /// <summary>
        /// 攻击范围
        /// </summary>
        public float AttackRange { get; set; } = 2f;
        
        /// <summary>
        /// 追击范围（超出此范围放弃追击）
        /// </summary>
        public float ChaseRange { get; set; } = 20f;
        
        /// <summary>
        /// 当前目标
        /// </summary>
        public Entity Target { get; set; }
        
        /// <summary>
        /// 巡逻目标点
        /// </summary>
        public UnityEngine.Vector3 PatrolTarget { get; set; }
        
        /// <summary>
        /// 出生点（用于重置位置）
        /// </summary>
        public UnityEngine.Vector3 SpawnPoint { get; set; }
        
        /// <summary>
        /// 状态计时器
        /// </summary>
        public float StateTimer { get; set; } = 0f;
        
        /// <summary>
        /// 攻击冷却
        /// </summary>
        public float AttackCooldown { get; set; } = 1f;
        
        /// <summary>
        /// 攻击冷却计时器
        /// </summary>
        public float AttackCooldownTimer { get; set; } = 0f;
        
        public void Reset()
        {
            CurrentState = AIState.Idle;
            Target = null;
            StateTimer = 0f;
            AttackCooldownTimer = 0f;
        }
    }
    
    /// <summary>
    /// AI状态枚举
    /// </summary>
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
