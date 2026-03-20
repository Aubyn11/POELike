using UnityEngine;
using UnityEngine.AI;
using POELike.ECS.Core;
using POELike.ECS.Components;
using POELike.ECS.Systems;
using POELike.Managers;

namespace POELike.Game.Character
{
    /// <summary>
    /// 敌人控制器
    /// 桥接Unity NavMesh与ECS AI系统
    /// 挂载到敌人GameObject上
    /// </summary>
    public class EnemyController : MonoBehaviour
    {
        [Header("敌人属性")]
        [SerializeField] private string _enemyName = "普通怪物";
        [SerializeField] private float _maxHealth = 50f;
        [SerializeField] private float _moveSpeed = 3f;
        [SerializeField] private float _attackDamage = 8f;
        [SerializeField] private float _attackRange = 2f;
        [SerializeField] private float _detectionRange = 10f;
        [SerializeField] private float _attackCooldown = 1.5f;
        
        [Header("掉落")]
        [SerializeField] private int _experienceReward = 10;
        
        // ECS实体
        public Entity EnemyEntity { get; private set; }
        
        private NavMeshAgent _navAgent;
        private AIComponent _aiComp;
        private MovementComponent _movementComp;
        
        private void Start()
        {
            _navAgent = GetComponent<NavMeshAgent>();
            CreateEnemyEntity();
            
            // 订阅AI攻击事件
            GameManager.Instance.World.EventBus.Subscribe<AIAttackEvent>(OnAIAttack);
            // 订阅死亡事件
            GameManager.Instance.World.EventBus.Subscribe<EntityDiedEvent>(OnEntityDied);
        }
        
        private void CreateEnemyEntity()
        {
            var world = GameManager.Instance.World;
            EnemyEntity = world.CreateEntity("Enemy");
            
            // 变换组件
            EnemyEntity.AddComponent(new TransformComponent { UnityTransform = transform });
            
            // 属性组件
            var statsComp = EnemyEntity.AddComponent(new StatsComponent());
            statsComp.SetBaseStat(StatType.MaxHealth, _maxHealth);
            statsComp.SetBaseStat(StatType.MovementSpeed, _moveSpeed);
            statsComp.SetBaseStat(StatType.PhysicalDamage, _attackDamage);
            statsComp.SetBaseStat(StatType.Armor, 20f);
            
            // 生命值组件
            var healthComp = EnemyEntity.AddComponent(new HealthComponent());
            healthComp.MaxHealth = _maxHealth;
            healthComp.FillToMax();
            
            // 移动组件
            _movementComp = EnemyEntity.AddComponent(new MovementComponent
            {
                BaseSpeed = _moveSpeed,
                CurrentSpeed = _moveSpeed
            });
            
            // 战斗组件
            EnemyEntity.AddComponent(new CombatComponent { AttackRange = _attackRange });
            
            // AI组件
            _aiComp = EnemyEntity.AddComponent(new AIComponent
            {
                DetectionRange = _detectionRange,
                AttackRange = _attackRange,
                ChaseRange = _detectionRange * 2f,
                AttackCooldown = _attackCooldown,
                SpawnPoint = transform.position
            });
        }
        
        private void Update()
        {
            if (EnemyEntity == null || !EnemyEntity.IsAlive) return;
            
            // 将ECS移动方向同步到NavMeshAgent
            if (_navAgent != null && _navAgent.enabled)
            {
                if (_aiComp.CurrentState == AIState.Chase || _aiComp.CurrentState == AIState.Patrol)
                {
                    Vector3 targetPos = _aiComp.CurrentState == AIState.Chase && _aiComp.Target != null
                        ? _aiComp.Target.GetComponent<TransformComponent>()?.Position ?? transform.position
                        : _aiComp.PatrolTarget;
                    
                    _navAgent.SetDestination(targetPos);
                    _navAgent.speed = _movementComp.CurrentSpeed;
                }
                else
                {
                    _navAgent.ResetPath();
                }
            }
        }
        
        private void OnAIAttack(AIAttackEvent evt)
        {
            if (evt.Attacker != EnemyEntity) return;
            
            var stats = EnemyEntity.GetComponent<StatsComponent>();
            float damage = stats?.GetStat(StatType.PhysicalDamage) ?? _attackDamage;
            
            GameManager.Instance.World.EventBus.Publish(new DamageEvent
            {
                Source = EnemyEntity,
                Target = evt.Target,
                BaseDamage = damage,
                DamageType = DamageType.Physical
            });
        }
        
        private void OnEntityDied(EntityDiedEvent evt)
        {
            if (evt.Entity != EnemyEntity) return;
            
            Debug.Log($"[EnemyController] {_enemyName} 死亡！经验奖励: {_experienceReward}");
            
            // 发布经验奖励事件
            GameManager.Instance.World.EventBus.Publish(new EnemyDiedEvent
            {
                Enemy = EnemyEntity,
                Killer = evt.Killer,
                ExperienceReward = _experienceReward,
                Position = transform.position
            });
            
            // 延迟销毁GameObject
            Destroy(gameObject, 2f);
        }
        
        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.World.EventBus.Unsubscribe<AIAttackEvent>(OnAIAttack);
                GameManager.Instance.World.EventBus.Unsubscribe<EntityDiedEvent>(OnEntityDied);
                
                if (EnemyEntity != null)
                    GameManager.Instance.World.DestroyEntity(EnemyEntity);
            }
        }
    }
    
    public struct EnemyDiedEvent
    {
        public Entity Enemy;
        public Entity Killer;
        public int ExperienceReward;
        public Vector3 Position;
    }
}
