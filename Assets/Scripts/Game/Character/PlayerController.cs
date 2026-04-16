using UnityEngine;
using UnityEngine.InputSystem;
using POELike.ECS.Core;
using POELike.ECS.Components;
using POELike.ECS.Systems;
using POELike.Managers;
using POELike.Game.UI;

namespace POELike.Game.Character
{
    /// <summary>
    /// 玩家控制器
    /// 桥接Unity Input System与ECS
    /// 挂载到玩家GameObject上
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("角色基础属性")]
        [SerializeField] private float _baseHealth = 100f;
        [SerializeField] private float _baseMana = 100f;
        [SerializeField] private float _baseMoveSpeed = 5f;
        [SerializeField] private float _basePhysicalDamage = 10f;

        [Header("摄像机")]
        [SerializeField] private Camera _mainCamera;

        // 存档数据（由 GameSceneManager 在 AddComponent 后立即调用 InitFromSaveData 赋值）
        private CharacterSaveData _saveData;
        
        // ECS实体
        public Entity PlayerEntity { get; private set; }
        
        // 组件引用
        private PlayerInputComponent _inputComp;
        private MovementComponent _movementComp;
        private SkillComponent _skillComp;
        
        // Unity Input System
        private InputAction _moveAction;
        private InputAction _skill1Action;
        private InputAction _skill2Action;
        private InputAction _skill3Action;
        private InputAction _skill4Action;
        private InputAction _skill5Action;
        private InputAction _skill6Action;
        
        private void Start()
        {
            if (_mainCamera == null)
                _mainCamera = Camera.main;

            // 若未通过 InitFromSaveData 设置存档，则使用 Inspector 默认值
            CreatePlayerEntity();
            SetupInputActions();
        }

        /// <summary>
        /// 由 GameSceneManager 在 AddComponent 后立即调用，传入选中的角色存档数据
        /// 必须在 Start() 之前调用（AddComponent 后同帧调用即可）
        /// </summary>
        public void InitFromSaveData(CharacterSaveData data)
        {
            _saveData = data;
            if (data == null) return;

            // 根据存档等级缩放基础属性（每级 +5 血量，+2 魔力，+0.1 伤害）
            int lv = Mathf.Max(1, data.Level);
            _baseHealth          = 100f + (lv - 1) * 5f;
            _baseMana            = 100f + (lv - 1) * 2f;
            _basePhysicalDamage  = 10f  + (lv - 1) * 0.1f;
            _baseMoveSpeed       = 5f;

            Debug.Log($"[PlayerController] 从存档初始化：{data.CharacterName}  Lv.{lv}" +
                      $"  HP:{_baseHealth}  MP:{_baseMana}");
        }
        
        /// <summary>
        /// 创建玩家ECS实体
        /// </summary>
        private void CreatePlayerEntity()
        {
            var world = GameManager.Instance.World;
            PlayerEntity = world.CreateEntity("Player");
            
            // 变换组件
            var transformComp = PlayerEntity.AddComponent(new TransformComponent
            {
                UnityTransform = transform
            });
            
            // 属性组件
            var statsComp = PlayerEntity.AddComponent(new StatsComponent());
            statsComp.SetBaseStat(StatType.Strength, 10f);
            statsComp.SetBaseStat(StatType.Dexterity, 10f);
            statsComp.SetBaseStat(StatType.Intelligence, 10f);
            statsComp.SetBaseStat(StatType.MaxHealth, _baseHealth);
            statsComp.SetBaseStat(StatType.MaxMana, _baseMana);
            statsComp.SetBaseStat(StatType.MovementSpeed, _baseMoveSpeed);
            statsComp.SetBaseStat(StatType.PhysicalDamage, _basePhysicalDamage);
            statsComp.SetBaseStat(StatType.HealthRegen, 5f);
            statsComp.SetBaseStat(StatType.ManaRegen, 10f);
            statsComp.SetBaseStat(StatType.Armor, 50f);
            statsComp.SetBaseStat(StatType.FireResistance, 0f);
            statsComp.SetBaseStat(StatType.ColdResistance, 0f);
            statsComp.SetBaseStat(StatType.LightningResistance, 0f);
            statsComp.SetBaseStat(StatType.ChaosResistance, -60f); // POE默认混沌抗性-60%
            statsComp.SetBaseStat(StatType.CriticalChance, 5f);
            statsComp.SetBaseStat(StatType.CriticalMultiplier, 150f);
            
            // 生命值组件
            var healthComp = PlayerEntity.AddComponent(new HealthComponent());
            healthComp.MaxHealth = _baseHealth;
            healthComp.MaxMana = _baseMana;
            healthComp.FillToMax();
            healthComp.OnDeath += OnPlayerDeath;
            
            // 移动组件
            _movementComp = PlayerEntity.AddComponent(new MovementComponent
            {
                CharacterController = GetComponent<CharacterController>(),
                BaseSpeed = _baseMoveSpeed,
                CurrentSpeed = _baseMoveSpeed
            });
            
            // 战斗组件
            PlayerEntity.AddComponent(new CombatComponent());
            
            // 技能组件
            _skillComp = PlayerEntity.AddComponent(new SkillComponent());
            _skillComp.InitializeSlots(6);
            
            // 装备组件
            PlayerEntity.AddComponent(new EquipmentComponent());
            PlayerEntity.AddComponent(new InventoryComponent());
            
            // 输入组件
            _inputComp = PlayerEntity.AddComponent(new PlayerInputComponent());
            
            Debug.Log($"[PlayerController] 玩家实体创建完成: {PlayerEntity}");
        }
        
        /// <summary>
        /// 设置输入动作
        /// </summary>
        private void SetupInputActions()
        {
            // 使用Unity Input System
            _moveAction = InputSystem.actions.FindAction("Move");
            _skill1Action = InputSystem.actions.FindAction("Attack");
            
            // 也可以直接创建InputAction
            _skill2Action = new InputAction("Skill2", InputActionType.Button, "<Keyboard>/e");
            _skill3Action = new InputAction("Skill3", InputActionType.Button, "<Keyboard>/r");
            _skill4Action = new InputAction("Skill4", InputActionType.Button, "<Keyboard>/t");
            _skill5Action = new InputAction("Skill5", InputActionType.Button, "<Keyboard>/f");
            _skill6Action = new InputAction("Skill6", InputActionType.Button, "<Keyboard>/g");
            
            _skill2Action.Enable();
            _skill3Action.Enable();
            _skill4Action.Enable();
            _skill5Action.Enable();
            _skill6Action.Enable();
        }
        
        private void Update()
        {
            if (PlayerEntity == null || !PlayerEntity.IsAlive) return;
            
            UpdateInput();
            UpdateMovementSpeed();
        }
        
        /// <summary>
        /// 更新输入状态到ECS组件
        /// </summary>
        private void UpdateInput()
        {
            // 移动输入
            Vector2 moveInput = _moveAction?.ReadValue<Vector2>() ?? Vector2.zero;
            _inputComp.MoveInput = moveInput;
            
            // 转换为3D移动方向（等距视角）
            Vector3 moveDir = new Vector3(moveInput.x, 0, moveInput.y).normalized;
            _movementComp.MoveDirection = moveDir;
            
            // 鼠标位置
            _inputComp.MouseScreenPosition = Mouse.current?.position.ReadValue() ?? Vector2.zero;
            _inputComp.MouseWorldPosition = GetMouseWorldPosition();
            
            // 技能输入
            _inputComp.SkillInputs[0] = _skill1Action?.WasPressedThisFrame() ?? false;
            _inputComp.SkillInputs[1] = _skill2Action.WasPressedThisFrame();
            _inputComp.SkillInputs[2] = _skill3Action.WasPressedThisFrame();
            _inputComp.SkillInputs[3] = _skill4Action.WasPressedThisFrame();
            _inputComp.SkillInputs[4] = _skill5Action.WasPressedThisFrame();
            _inputComp.SkillInputs[5] = _skill6Action.WasPressedThisFrame();
            
            // 处理技能激活
            for (int i = 0; i < _inputComp.SkillInputs.Length; i++)
            {
                if (_inputComp.SkillInputs[i])
                {
                    var slot = _skillComp.GetSlot(i);
                    if (slot != null)
                    {
                        GameManager.Instance.World.EventBus.Publish(new SkillActivateEvent
                        {
                            Caster = PlayerEntity,
                            Slot = slot
                        });
                    }
                }
            }
        }
        
        /// <summary>
        /// 根据属性更新移动速度
        /// </summary>
        private void UpdateMovementSpeed()
        {
            var stats = PlayerEntity.GetComponent<StatsComponent>();
            if (stats != null)
                _movementComp.CurrentSpeed = stats.GetStat(StatType.MovementSpeed);
        }
        
        /// <summary>
        /// 获取鼠标在世界坐标中的位置
        /// </summary>
        private Vector3 GetMouseWorldPosition()
        {
            if (_mainCamera == null) return Vector3.zero;
            
            var mousePos = Mouse.current?.position.ReadValue() ?? Vector2.zero;
            Ray ray = _mainCamera.ScreenPointToRay(new Vector3(mousePos.x, mousePos.y, 0));
            
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                return hit.point;
            
            // 如果没有碰撞，计算与Y=0平面的交点
            if (ray.direction.y != 0)
            {
                float t = -ray.origin.y / ray.direction.y;
                return ray.origin + ray.direction * t;
            }
            
            return Vector3.zero;
        }
        
        private void OnPlayerDeath()
        {
            Debug.Log("[PlayerController] 玩家死亡！");
            GameManager.Instance.World.EventBus.Publish(new PlayerDiedEvent { Player = PlayerEntity });
        }
        
        private void OnDestroy()
        {
            _skill2Action?.Dispose();
            _skill3Action?.Dispose();
            _skill4Action?.Dispose();
            _skill5Action?.Dispose();
            _skill6Action?.Dispose();
            
            if (PlayerEntity != null && GameManager.Instance != null)
                GameManager.Instance.World.DestroyEntity(PlayerEntity);
        }
    }
    
    public struct PlayerDiedEvent
    {
        public Entity Player;
    }
}
