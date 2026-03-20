using UnityEngine;
using POELike.ECS.Core;
using POELike.ECS.Components;

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
        private Entity _playerEntity;
        
        protected override void OnInitialize()
        {
            World.EventBus.Subscribe<EntityCreatedEvent>(OnEntityCreated);
        }
        
        private void OnEntityCreated(EntityCreatedEvent evt)
        {
            // 检测玩家实体
            if (evt.Entity.Tag == "Player")
                _playerEntity = evt.Entity;
        }
        
        protected override void OnUpdate(float deltaTime)
        {
            // 延迟获取玩家（确保玩家已创建）
            if (_playerEntity == null)
                _playerEntity = World.FindEntityByTag("Player");
            
            var entities = World.Query<AIComponent, MovementComponent, TransformComponent>();
            
            foreach (var entity in entities)
            {
                var ai = entity.GetComponent<AIComponent>();
                var movement = entity.GetComponent<MovementComponent>();
                var transform = entity.GetComponent<TransformComponent>();
                var health = entity.GetComponent<HealthComponent>();
                
                // 死亡检查
                if (health != null && !health.IsAlive)
                {
                    ai.CurrentState = AIState.Dead;
                    movement.MoveDirection = Vector3.zero;
                    continue;
                }
                
                // 更新冷却
                if (ai.AttackCooldownTimer > 0)
                    ai.AttackCooldownTimer -= deltaTime;
                
                ai.StateTimer += deltaTime;
                
                // 状态机更新
                switch (ai.CurrentState)
                {
                    case AIState.Idle:
                        UpdateIdleState(entity, ai, movement, transform, deltaTime);
                        break;
                    case AIState.Patrol:
                        UpdatePatrolState(entity, ai, movement, transform, deltaTime);
                        break;
                    case AIState.Chase:
                        UpdateChaseState(entity, ai, movement, transform, deltaTime);
                        break;
                    case AIState.Attack:
                        UpdateAttackState(entity, ai, movement, transform, deltaTime);
                        break;
                    case AIState.Flee:
                        UpdateFleeState(entity, ai, movement, transform, deltaTime);
                        break;
                    case AIState.Stunned:
                        movement.MoveDirection = Vector3.zero;
                        if (ai.StateTimer >= 1f) // 眩晕1秒
                            TransitionTo(ai, AIState.Idle);
                        break;
                }
            }
        }
        
        private void UpdateIdleState(Entity entity, AIComponent ai, MovementComponent movement, TransformComponent transform, float deltaTime)
        {
            movement.MoveDirection = Vector3.zero;
            
            // 检测玩家
            if (CanDetectPlayer(ai, transform))
            {
                ai.Target = _playerEntity;
                TransitionTo(ai, AIState.Chase);
                return;
            }
            
            // 随机进入巡逻
            if (ai.StateTimer > 3f)
            {
                ai.PatrolTarget = ai.SpawnPoint + new Vector3(
                    Random.Range(-5f, 5f), 0, Random.Range(-5f, 5f));
                TransitionTo(ai, AIState.Patrol);
            }
        }
        
        private void UpdatePatrolState(Entity entity, AIComponent ai, MovementComponent movement, TransformComponent transform, float deltaTime)
        {
            // 检测玩家
            if (CanDetectPlayer(ai, transform))
            {
                ai.Target = _playerEntity;
                TransitionTo(ai, AIState.Chase);
                return;
            }
            
            // 移向巡逻点
            Vector3 dir = (ai.PatrolTarget - transform.Position);
            dir.y = 0;
            
            if (dir.magnitude < 0.5f)
            {
                TransitionTo(ai, AIState.Idle);
                return;
            }
            
            movement.MoveDirection = dir.normalized;
        }
        
        private void UpdateChaseState(Entity entity, AIComponent ai, MovementComponent movement, TransformComponent transform, float deltaTime)
        {
            if (ai.Target == null || !ai.Target.IsAlive)
            {
                TransitionTo(ai, AIState.Idle);
                return;
            }
            
            var targetTransform = ai.Target.GetComponent<TransformComponent>();
            if (targetTransform == null) return;
            
            float dist = Vector3.Distance(transform.Position, targetTransform.Position);
            
            // 超出追击范围，放弃
            if (dist > ai.ChaseRange)
            {
                ai.Target = null;
                TransitionTo(ai, AIState.Idle);
                return;
            }
            
            // 进入攻击范围
            if (dist <= ai.AttackRange)
            {
                movement.MoveDirection = Vector3.zero;
                TransitionTo(ai, AIState.Attack);
                return;
            }
            
            // 追击
            Vector3 dir = (targetTransform.Position - transform.Position);
            dir.y = 0;
            movement.MoveDirection = dir.normalized;
        }
        
        private void UpdateAttackState(Entity entity, AIComponent ai, MovementComponent movement, TransformComponent transform, float deltaTime)
        {
            if (ai.Target == null || !ai.Target.IsAlive)
            {
                TransitionTo(ai, AIState.Idle);
                return;
            }
            
            var targetTransform = ai.Target.GetComponent<TransformComponent>();
            if (targetTransform == null) return;
            
            float dist = Vector3.Distance(transform.Position, targetTransform.Position);
            
            // 目标跑远了，重新追击
            if (dist > ai.AttackRange * 1.5f)
            {
                TransitionTo(ai, AIState.Chase);
                return;
            }
            
            movement.MoveDirection = Vector3.zero;
            
            // 攻击
            if (ai.AttackCooldownTimer <= 0)
            {
                ai.AttackCooldownTimer = ai.AttackCooldown;
                
                // 发布攻击事件
                World.EventBus.Publish(new AIAttackEvent { Attacker = entity, Target = ai.Target });
            }
        }
        
        private void UpdateFleeState(Entity entity, AIComponent ai, MovementComponent movement, TransformComponent transform, float deltaTime)
        {
            if (ai.Target == null)
            {
                TransitionTo(ai, AIState.Idle);
                return;
            }
            
            var targetTransform = ai.Target.GetComponent<TransformComponent>();
            if (targetTransform == null) return;
            
            // 逃离方向
            Vector3 fleeDir = (transform.Position - targetTransform.Position);
            fleeDir.y = 0;
            movement.MoveDirection = fleeDir.normalized;
        }
        
        private bool CanDetectPlayer(AIComponent ai, TransformComponent transform)
        {
            if (_playerEntity == null) return false;
            var playerTransform = _playerEntity.GetComponent<TransformComponent>();
            if (playerTransform == null) return false;
            
            float dist = Vector3.Distance(transform.Position, playerTransform.Position);
            return dist <= ai.DetectionRange;
        }
        
        private void TransitionTo(AIComponent ai, AIState newState)
        {
            ai.CurrentState = newState;
            ai.StateTimer = 0f;
        }
        
        protected override void OnDispose()
        {
            World.EventBus.Unsubscribe<EntityCreatedEvent>(OnEntityCreated);
        }
    }
    
    public struct AIAttackEvent
    {
        public Entity Attacker;
        public Entity Target;
    }
}
