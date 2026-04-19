using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using POELike.ECS.Components;
using POELike.ECS.Core;
using POELike.ECS.Systems;
using POELike.Game.Character;
using POELike.Game.Currency;
using POELike.Game.Items;
using POELike.Game.Skills;
using POELike.Game.UI;
using POELike.Managers;

namespace POELike.Game
{
    /// <summary>
    /// 游戏场景管理器
    /// 挂载在 GameScene 的 GameSceneManager GameObject 上
    /// 负责：构建3D场景环境 → 生成玩家ECS实体 → 启动摄像机跟随
    /// </summary>
    public class GameSceneManager : MonoBehaviour
    {
        private static readonly Vector3 DefaultGameScenePlayerSpawnPoint = new Vector3(2f, 0f, 0f);

        [Header("玩家设置")]
        [SerializeField] private Vector3 _playerSpawnPoint = new Vector3(2f, 0f, 0f);

        [Header("场景环境（运行时自动生成，可留空）")]
        [SerializeField] private bool _autoGenerateEnvironment = true;

        [Header("怪物掉落")]
        [SerializeField] private bool _enableMonsterGroundDrops = true;
        [SerializeField] [Range(0f, 1f)] private float _monsterDropChance = 0.5f;
        [SerializeField] private int _monsterDropItemLevel = 5;
        [SerializeField] [Range(0f, 1f)] private float _monsterCurrencyDropChance = 0.35f;
        [SerializeField] private Vector2Int _monsterCurrencyStackRange = new Vector2Int(1, 3);

        // 玩家ECS实体
        private Entity _playerEntity;

        // NPC实体列表
        private List<Entity> _npcEntities = new List<Entity>();

        // 当前地图内容刷出的怪物实体列表
        private readonly List<Entity> _currentMapMonsterEntities = new List<Entity>();

        // 当前地图刷出的运行时装饰物
        private readonly List<GameObject> _currentMapDecorationObjects = new List<GameObject>();
        private readonly List<Collider> _currentMapBlockingColliders = new List<Collider>();
        private Transform _mapDecorationRoot;

        private const float PlayerCollisionRadius = 0.4f;
        private const float MapDecorationCollisionSkin = 0.02f;
        private const float MapDecorationResolveEpsilon = 0.01f;
        private const int MapDecorationMaxResolvePasses = 6;
        private const float MapDecorationBlockedTargetDotThreshold = -0.05f;
        private bool _movementCancelledByObstacleThisFrame;

        // 玩家输入组件引用（每帧写入）

        private PlayerInputComponent _inputComp;
        private MovementComponent _movementComp;
        private TransformComponent _transformComp;
        private SkillComponent _skillComp;

        // 摄像机控制器
        private CameraController _cameraController;

        // NPC名称点击寻路：缓存NpcMeshRenderer引用（同时负责头顶名称标签）
        private NpcMeshRenderer _npcMeshRenderer;

        // 地面掉落名称点击拾取
        private GroundItemLabelRenderer _groundItemLabelRenderer;

        // GM 面板
        private GMPanel _gmPanel;

        // 客户端技能拓展面板
        private ClientSkillExtensionPanel _clientSkillExtensionPanel;

        // NPC对话框
        private NpcDialogPanel _npcDialogPanel;

        // 商店面板
        private ShopPanel _shopPanel;

        // 传送门面板
        private DoorPanel _doorPanel;

        // 当前地图关卡上下文
        private MapLevelData _currentMapLevel;

        private const float MapTeleportHeight = 0.5f;
        private static readonly Vector3[] MapTeleportAnchors =
        {
            new Vector3(-24f, MapTeleportHeight, 24f),
            new Vector3(24f, MapTeleportHeight, 24f),
            new Vector3(-24f, MapTeleportHeight, -24f),
            new Vector3(24f, MapTeleportHeight, -24f),
            new Vector3(0f, MapTeleportHeight, 28f),
            new Vector3(28f, MapTeleportHeight, 0f),
            new Vector3(0f, MapTeleportHeight, -28f),
            new Vector3(-28f, MapTeleportHeight, 0f),
        };

        // 当前寻路目标NPC实体（点击NPC名称时记录）
        private Entity _targetNpcEntity;

        // 是否正在前往NPC（用于到达检测）
        private bool _walkingToNpc;
        // 上一帧 HasTarget 状态（用于检测到达时机）
        private bool _prevHasTarget;

        private ItemData _pendingGroundPickupItem;
        private Vector3 _pendingGroundPickupPosition;
        private bool _walkingToGroundItem;
        private const float GroundPickupStopDistance = 1.35f;

        // 左键交互意图：一次按下只在“技能 / 移动 / 阻断”里走一条路径
        private enum LeftMouseIntent
        {
            None,
            Blocked,
            Move,
            Skill1,
        }

        private const float LeftClickMonsterSnapPadding = 0.35f;
        private readonly List<Entity> _leftClickMonsterBuffer = new List<Entity>(256);
        private LeftMouseIntent _leftMouseIntent = LeftMouseIntent.None;

        // Input Actions

        private InputAction _skill1Action;
        private InputAction _skill2Action;
        private InputAction _skill3Action;
        private InputAction _skill4Action;
        private InputAction _skill5Action;
        private InputAction _skill6Action;
        private InputAction _skill7Action;
        private InputAction _skill8Action;

        private InputAction _flask1Action;
        private InputAction _flask2Action;
        private InputAction _flask3Action;
        private InputAction _flask4Action;
        private InputAction _flask5Action;

        // 鼠标左键点击寻路
        private InputAction _mouseClickAction;

        private Camera _mainCamera;

        // ── 生命周期 ──────────────────────────────────────────────────

        private void Start()
        {
            var data = SceneLoader.PendingCharacterData;
            if (data == null)
            {
                data = new CharacterSaveData("debug", "调试角色", 1, "本地");
                Debug.LogWarning("[GameSceneManager] 未检测到存档数据，使用默认调试角色");
            }

            SceneLoader.PendingCharacterData = data;

            Debug.Log($"[GameSceneManager] 场景初始化，角色：{data.CharacterName}  Lv.{data.Level}");

            var world = GameManager.Instance?.World;
            if (world != null)
                world.EventBus.Subscribe<EntityDiedEvent>(OnEntityDied);

            _currentMapLevel = SceneLoader.ConsumePendingMapLevelData() ?? _currentMapLevel ?? BuildDefaultMapContext();

            if (_autoGenerateEnvironment)
                BuildEnvironment();

            _playerSpawnPoint = ResolveMapSpawnPoint(_currentMapLevel);

            bool reusedExistingPlayer = TryReuseExistingPlayer();
            if (!reusedExistingPlayer)
            {
                SpawnPlayer(data);
                AssignDefaultSkills();
            }
            else
            {
                MovePlayerToCurrentMapSpawn();
            }

            SetupInputActions();
            SetupGMPanel();
            SetupClientSkillExtensionPanel();
            RefreshCurrentMapDecoration();
            RefreshCurrentMapLayout();
            RefreshCurrentMapContent();
            UIManager.Instance?.RefreshCharactorMainPanel();
            SceneLoader.NotifyGameplaySceneActivated();
        }

        private void Update()
        {
            if (_playerEntity == null || !_playerEntity.IsAlive) return;
            _movementCancelledByObstacleThisFrame = false;
            UpdateInput();
            UpdateMovementSpeed();
            ResolveNPCCollisions();
            ResolveMapDecorationCollisions();
            CheckNpcArrival();
            CheckGroundItemArrival();
        }

        private void OnDestroy()
        {
            _mouseClickAction?.Dispose();
            _skill1Action?.Dispose();
            _skill2Action?.Dispose();
            _skill3Action?.Dispose();
            _skill4Action?.Dispose();
            _skill5Action?.Dispose();
            _skill6Action?.Dispose();
            _skill7Action?.Dispose();
            _skill8Action?.Dispose();
            _flask1Action?.Dispose();

            _flask2Action?.Dispose();
            _flask3Action?.Dispose();
            _flask4Action?.Dispose();
            _flask5Action?.Dispose();

            if (_doorPanel != null)
                _doorPanel.MapSelected -= OnDoorPanelMapSelected;

            if (GameManager.Instance?.World != null)
                GameManager.Instance.World.EventBus.Unsubscribe<EntityDiedEvent>(OnEntityDied);

            bool preservePlayer = SceneLoader.PreservePlayerEntityOnNextGameplaySceneLoad;
            if (!preservePlayer && _playerEntity != null && GameManager.Instance != null)
                GameManager.Instance.World.DestroyEntity(_playerEntity);

            ClearCurrentMapNpcs();

            ClearCurrentMapMonsters();
            ClearCurrentMapDecorations();

            // 销毁 GM 生成的怪物实体

            if (_gmPanel != null && GameManager.Instance != null)
            {
                _gmPanel.DestroyAllSpawnedMonsters(false);
            }

        }

        // ── 场景环境构建 ──────────────────────────────────────────────

        /// <summary>
        /// 程序化构建基础3D场景：地面、边界墙、灯光
        /// </summary>
        private void BuildEnvironment()
        {
            var activeScene = SceneManager.GetActiveScene();
            string sceneName = activeScene.name;

            var existingGround = GameObject.Find("Ground");
            if (existingGround == null)
            {
                existingGround = GameObject.CreatePrimitive(PrimitiveType.Plane);
                existingGround.name = "Ground";
            }

            existingGround.transform.localScale = new Vector3(10f, 1f, 10f);
            existingGround.transform.position = Vector3.zero;
            existingGround.layer = LayerMask.NameToLayer("Default");

            Color groundColor = new Color(0.25f, 0.22f, 0.18f);
            Color ambientColor = new Color(0.15f, 0.15f, 0.2f);
            Color sunColor = new Color(1f, 0.95f, 0.8f);

            string cfgId = _currentMapLevel?.CfgID ?? string.Empty;
            string sceneId = _currentMapLevel?.SceneID ?? string.Empty;
            if (sceneName == SceneLoader.SceneMission)
            {
                switch (cfgId)
                {
                    case "1001":
                        groundColor = new Color(0.19f, 0.33f, 0.20f);
                        ambientColor = new Color(0.14f, 0.20f, 0.16f);
                        sunColor = new Color(0.98f, 0.93f, 0.78f);
                        break;
                    case "1002":
                        groundColor = new Color(0.16f, 0.24f, 0.33f);
                        ambientColor = new Color(0.09f, 0.13f, 0.18f);
                        sunColor = new Color(0.72f, 0.86f, 1f);
                        break;
                }

                if (sceneId == "2")
                {
                    groundColor = new Color(0.30f, 0.25f, 0.18f);
                    ambientColor = new Color(0.22f, 0.18f, 0.12f);
                    sunColor = new Color(1.00f, 0.82f, 0.64f);
                }
            }

            SetMaterialColor(existingGround, groundColor);

            GameObject sunGo = GameObject.Find("Sun") ?? GameObject.Find("Directional Light");
            if (sunGo == null)
                sunGo = new GameObject("Sun");
            else
                sunGo.name = "Sun";

            var sun = sunGo.GetComponent<Light>();
            if (sun == null)
                sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = sunColor;
            sun.intensity = 1.2f;
            sunGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = ambientColor;

            Debug.Log($"[GameSceneManager] 场景环境构建完成：{sceneName}，地图={_currentMapLevel?.MapName}，CfgID={cfgId}，SceneID={sceneId}");
        }

        private void SetMaterialColor(GameObject go, Color color)

        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;
            var mat = new Material(renderer.sharedMaterial);
            mat.color = color;
            renderer.material = mat;
        }

        // ── 玩家生成（纯ECS，无GameObject）────────────────────────────

        private bool TryReuseExistingPlayer()
        {
            var world = GameManager.Instance?.World;
            if (world == null)
                return false;

            var existingPlayer = world.FindEntityByTag("Player");
            if (existingPlayer == null || !existingPlayer.IsAlive)
                return false;

            _playerEntity = existingPlayer;
            _movementComp = _playerEntity.GetComponent<MovementComponent>();
            _transformComp = _playerEntity.GetComponent<TransformComponent>();
            _skillComp = _playerEntity.GetComponent<SkillComponent>();
            _inputComp = _playerEntity.GetComponent<PlayerInputComponent>();

            _skillComp?.EnsureSlotCapacity(8);

            if (_movementComp == null || _transformComp == null || _skillComp == null || _inputComp == null)
            {
                Debug.LogWarning("[GameSceneManager] 发现已有玩家实体，但组件不完整，改为重新创建玩家。 ");
                _playerEntity = null;
                _movementComp = null;
                _transformComp = null;
                _skillComp = null;
                _inputComp = null;
                return false;
            }

            SetupCamera();
            Debug.Log("[GameSceneManager] 复用已有玩家 ECS 实体进入新的玩法场景。");
            return true;
        }

        private void MovePlayerToCurrentMapSpawn()
        {
            if (_playerEntity == null || !_playerEntity.IsAlive)
                return;

            if (_transformComp != null)
            {
                _transformComp.Position = _playerSpawnPoint;
                ResolveMapDecorationCollisions(true);
            }

            if (_movementComp != null)
            {
                _movementComp.TargetPosition = _playerSpawnPoint;
                _movementComp.MoveDirection = Vector3.zero;
                _movementComp.Velocity = Vector3.zero;
                _movementComp.HasTarget = false;
            }

            if (_inputComp != null)
            {
                _inputComp.HasClickTarget = false;
                _inputComp.ClickTargetPosition = _playerSpawnPoint;
                _inputComp.MouseWorldPosition = _playerSpawnPoint;
            }

            _walkingToNpc = false;
            _prevHasTarget = false;
            _targetNpcEntity = null;
            _npcMeshRenderer?.SetTalkingNpc(null);
            ClearPendingGroundItemPickup();
        }

        /// <summary>
        /// 直接创建玩家ECS实体，不生成任何GameObject
        /// </summary>
        private void SpawnPlayer(CharacterSaveData data)
        {
            var world = GameManager.Instance.World;

            // 根据存档等级缩放基础属性
            int lv = Mathf.Max(1, data.Level);
            float baseHealth    = 100f + (lv - 1) * 5f;
            float baseMana      = 100f + (lv - 1) * 2f;
            float baseDamage    = 10f  + (lv - 1) * 0.1f;
            float baseMoveSpeed = 5f;

            _playerEntity = world.CreateEntity("Player");

            // 变换组件（纯逻辑，不绑定Unity Transform）
            _playerEntity.AddComponent(new TransformComponent
            {
                Position = _playerSpawnPoint
            });

            // 属性组件
            var statsComp = _playerEntity.AddComponent(new StatsComponent());
            statsComp.SetBaseStat(StatType.Strength,            10f);
            statsComp.SetBaseStat(StatType.Dexterity,           10f);
            statsComp.SetBaseStat(StatType.Intelligence,        10f);
            statsComp.SetBaseStat(StatType.MaxHealth,           baseHealth);
            statsComp.SetBaseStat(StatType.MaxMana,             baseMana);
            statsComp.SetBaseStat(StatType.MovementSpeed,       baseMoveSpeed);
            statsComp.SetBaseStat(StatType.PhysicalDamage,      baseDamage);
            statsComp.SetBaseStat(StatType.HealthRegen,         5f);
            statsComp.SetBaseStat(StatType.ManaRegen,           10f);
            statsComp.SetBaseStat(StatType.Armor,               50f);
            statsComp.SetBaseStat(StatType.FireResistance,      0f);
            statsComp.SetBaseStat(StatType.ColdResistance,      0f);
            statsComp.SetBaseStat(StatType.LightningResistance, 0f);
            statsComp.SetBaseStat(StatType.ChaosResistance,     -60f);
            statsComp.SetBaseStat(StatType.CriticalChance,      5f);
            statsComp.SetBaseStat(StatType.CriticalMultiplier,  150f);

            // 生命值组件
            var healthComp = _playerEntity.AddComponent(new HealthComponent());
            healthComp.MaxHealth = baseHealth;
            healthComp.MaxMana   = baseMana;
            healthComp.FillToMax();
            healthComp.OnDeath += OnPlayerDeath;

            // 移动组件（不绑定CharacterController）
            _movementComp = _playerEntity.AddComponent(new MovementComponent
            {
                BaseSpeed    = baseMoveSpeed,
                CurrentSpeed = baseMoveSpeed
            });

            // 缓存变换组件引用
            _transformComp = _playerEntity.GetComponent<TransformComponent>();

            // 战斗组件
            _playerEntity.AddComponent(new CombatComponent());

            // 技能组件
            _skillComp = _playerEntity.AddComponent(new SkillComponent());
            _skillComp.InitializeSlots(8);

            // 装备 & 背包组件
            _playerEntity.AddComponent(new EquipmentComponent());
            _playerEntity.AddComponent(new InventoryComponent());

            // 输入组件
            _inputComp = _playerEntity.AddComponent(new PlayerInputComponent());

            // 摄像机（跟随逻辑位置）
            SetupCamera();

            Debug.Log($"[GameSceneManager] 玩家ECS实体创建完成: {_playerEntity}  角色：{data.CharacterName}  Lv.{lv}");
        }

        private void AssignDefaultSkills()
        {
            if (_skillComp == null)
                return;

            var slot0 = _skillComp.GetSlot(0);
            if (slot0 != null)
                slot0.SkillData = SkillFactory.CreateHeavyStrike();

            var slot1 = _skillComp.GetSlot(1);
            if (slot1 != null)
            {
                slot1.SkillData = SkillFactory.CreateFireball()
                    .WithSupportGem(SkillFactory.CreateMultiProjectileGem(2))
                    .WithSupportGem(SkillFactory.CreateAddedFireDamageGem(15f));
            }

            var slot2 = _skillComp.GetSlot(2);
            if (slot2 != null)
                slot2.SkillData = SkillFactory.CreateFrostNova();

            var slot3 = _skillComp.GetSlot(3);
            if (slot3 != null)
                slot3.SkillData = SkillFactory.CreateBlink();

            var slot4 = _skillComp.GetSlot(4);
            if (slot4 != null)
                slot4.SkillData = SkillFactory.CreateCyclone();
        }

        // ── 输入驱动 ──────────────────────────────────────────────────

        private void SetupInputActions()

        {
            // 鼠标左键点击寻路
            _mouseClickAction = new InputAction("MouseClick", InputActionType.Button, "<Mouse>/leftButton");
            _mouseClickAction.Enable();

            _skill1Action = new InputAction("Skill1", InputActionType.Button, "<Mouse>/leftButton");

            _skill2Action = new InputAction("Skill2", InputActionType.Button, "<Mouse>/middleButton");
            _skill3Action = new InputAction("Skill3", InputActionType.Button, "<Mouse>/rightButton");
            _skill4Action = new InputAction("Skill4", InputActionType.Button, "<Keyboard>/q");
            _skill5Action = new InputAction("Skill5", InputActionType.Button, "<Keyboard>/w");
            _skill6Action = new InputAction("Skill6", InputActionType.Button, "<Keyboard>/e");
            _skill7Action = new InputAction("Skill7", InputActionType.Button, "<Keyboard>/r");
            _skill8Action = new InputAction("Skill8", InputActionType.Button, "<Keyboard>/t");
            _flask1Action = new InputAction("Flask1", InputActionType.Button, "<Keyboard>/1");

            _flask2Action = new InputAction("Flask2", InputActionType.Button, "<Keyboard>/2");
            _flask3Action = new InputAction("Flask3", InputActionType.Button, "<Keyboard>/3");
            _flask4Action = new InputAction("Flask4", InputActionType.Button, "<Keyboard>/4");
            _flask5Action = new InputAction("Flask5", InputActionType.Button, "<Keyboard>/5");

            _skill1Action.Enable();
            _skill2Action.Enable();
            _skill3Action.Enable();
            _skill4Action.Enable();
            _skill5Action.Enable();
            _skill6Action.Enable();
            _skill7Action.Enable();
            _skill8Action.Enable();
            _flask1Action.Enable();

            _flask2Action.Enable();
            _flask3Action.Enable();
            _flask4Action.Enable();
            _flask5Action.Enable();
        }

        private void UpdateInput()
        {
            var mouseScreenPos = Mouse.current?.position.ReadValue() ?? Vector2.zero;
            bool pointerOverUI = UIGamePanelManager.IsPointerOverAnyPanel(mouseScreenPos);
            var mouseWorldPos = GetMouseWorldPosition();

            _inputComp.MouseScreenPosition = mouseScreenPos;
            if (!pointerOverUI && mouseWorldPos != Vector3.zero)
                _inputComp.MouseWorldPosition = mouseWorldPos;

            bool leftHeld = Mouse.current?.leftButton.isPressed ?? false;
            bool leftDown = Mouse.current?.leftButton.wasPressedThisFrame ?? false;
            bool leftUp = Mouse.current?.leftButton.wasReleasedThisFrame ?? false;
            bool npcClickConsumed = _npcMeshRenderer != null && _npcMeshRenderer.ClickConsumedThisFrame;
            bool groundItemClickConsumed = _groundItemLabelRenderer != null && _groundItemLabelRenderer.ClickConsumedThisFrame;

            _inputComp.MouseLeftHeld = leftHeld;

            if (leftDown)
                ResolveLeftMouseIntent(pointerOverUI, npcClickConsumed || groundItemClickConsumed, mouseWorldPos);

            if (_leftMouseIntent == LeftMouseIntent.Move
                && !_movementComp.IsMovementLockedByCasting
                && leftHeld
                && !pointerOverUI
                && !UIGamePanelManager.AnyOpen)
            {
                UpdateClickToMove(mouseWorldPos);
            }
            else
            {
                _inputComp.HasClickTarget = false;
            }

            // ── 技能输入 ──────────────────────────────────────────────
            UpdateLeftClickSkillInputState();
            UpdateSkillInputState(1, _skill2Action);
            UpdateSkillInputState(2, _skill3Action);
            UpdateSkillInputState(3, _skill4Action);
            UpdateSkillInputState(4, _skill5Action);
            UpdateSkillInputState(5, _skill6Action);
            UpdateSkillInputState(6, _skill7Action);
            UpdateSkillInputState(7, _skill8Action);
            _inputComp.FlaskInputs[0] = _flask1Action.WasPressedThisFrame();

            _inputComp.FlaskInputs[1] = _flask2Action.WasPressedThisFrame();
            _inputComp.FlaskInputs[2] = _flask3Action.WasPressedThisFrame();
            _inputComp.FlaskInputs[3] = _flask4Action.WasPressedThisFrame();
            _inputComp.FlaskInputs[4] = _flask5Action.WasPressedThisFrame();

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
                            Caster = _playerEntity,
                            Slot   = slot
                        });
                    }
                }
            }

            HandleFlaskInputs();

            if (leftUp)
                _leftMouseIntent = LeftMouseIntent.None;
        }

        private void ResolveLeftMouseIntent(bool pointerOverUI, bool npcClickConsumed, Vector3 mouseWorldPos)
        {
            if (pointerOverUI)
            {
                _leftMouseIntent = LeftMouseIntent.Blocked;
                return;
            }

            if (UIGamePanelManager.AnyOpen)
            {
                UIGamePanelManager.CloseAll();
                _leftMouseIntent = LeftMouseIntent.Blocked;
                return;
            }

            if (npcClickConsumed)
            {
                _leftMouseIntent = LeftMouseIntent.Blocked;
                return;
            }

            if (TryBeginLeftClickSkill(mouseWorldPos))
            {
                _leftMouseIntent = LeftMouseIntent.Skill1;
                return;
            }

            if (_movementComp != null && !_movementComp.IsMovementLockedByCasting)
            {
                _leftMouseIntent = LeftMouseIntent.Move;
                UpdateClickToMove(mouseWorldPos);
                return;
            }

            _leftMouseIntent = LeftMouseIntent.Blocked;
        }

        private void UpdateClickToMove(Vector3 clickPos)
        {
            if (clickPos == Vector3.zero)
                return;

            _movementComp.TargetPosition = clickPos;
            _movementComp.HasTarget = true;

            _targetNpcEntity = null;
            _walkingToNpc = false;
            ClearPendingGroundItemPickup();

            var combat = _playerEntity?.GetComponent<CombatComponent>();
            if (combat != null)
                combat.CurrentTarget = null;

            _inputComp.ClickTargetPosition = clickPos;
            _inputComp.HasClickTarget = true;
        }

        private bool TryBeginLeftClickSkill(Vector3 mouseWorldPos)
        {
            var slot = _skillComp?.GetSlot(0);
            if (!CanTriggerLeftClickSkill(slot, mouseWorldPos, out var targetMonster))
                return false;

            var combat = _playerEntity?.GetComponent<CombatComponent>();
            if (combat != null)
                combat.CurrentTarget = targetMonster;

            var targetTransform = targetMonster?.GetComponent<TransformComponent>();
            if (targetTransform != null)
                _inputComp.MouseWorldPosition = targetTransform.Position;

            _movementComp.HasTarget = false;
            _movementComp.MoveDirection = Vector3.zero;
            _targetNpcEntity = null;
            _walkingToNpc = false;
            ClearPendingGroundItemPickup();
            _inputComp.HasClickTarget = false;
            return true;
        }

        private bool CanTriggerLeftClickSkill(SkillSlot slot, Vector3 mouseWorldPos, out Entity targetMonster)
        {
            targetMonster = null;
            if (slot == null || !slot.HasSkill || slot.IsOnCooldown)
                return false;

            var skill = slot.SkillData;
            if (skill == null || skill.Type == SkillType.Movement)
                return false;

            if (_skillComp != null)
            {
                if (_skillComp.IsCasting)
                    return false;
                if (_skillComp.IsChanneling && _skillComp.ActiveSkill != slot)
                    return false;
            }

            var health = _playerEntity?.GetComponent<HealthComponent>();
            if (health != null && health.CurrentMana < skill.ManaCost)
                return false;

            if (mouseWorldPos == Vector3.zero)
                return false;

            targetMonster = FindMonsterUnderCursor(mouseWorldPos);
            return targetMonster != null && IsTargetWithinLeftClickSkillRange(targetMonster, skill);
        }

        private Entity FindMonsterUnderCursor(Vector3 mouseWorldPos)
        {
            var world = GameManager.Instance?.World;
            if (world == null || mouseWorldPos == Vector3.zero)
                return null;

            world.Query<MonsterComponent>(_leftClickMonsterBuffer);

            Entity closest = null;
            float bestDistSq = float.MaxValue;
            float snapRadius = MonsterSpawner.CollisionRadius + LeftClickMonsterSnapPadding;
            float snapRadiusSq = snapRadius * snapRadius;
            var cursorPos = new Vector2(mouseWorldPos.x, mouseWorldPos.z);

            foreach (var monster in _leftClickMonsterBuffer)
            {
                if (monster == null || !monster.IsAlive)
                    continue;

                var health = monster.GetComponent<HealthComponent>();
                if (health != null && !health.IsAlive)
                    continue;

                var transform = monster.GetComponent<TransformComponent>();
                if (transform == null)
                    continue;

                var delta = new Vector2(transform.Position.x - cursorPos.x, transform.Position.z - cursorPos.y);
                float distSq = delta.sqrMagnitude;
                if (distSq > snapRadiusSq || distSq >= bestDistSq)
                    continue;

                closest = monster;
                bestDistSq = distSq;
            }

            return closest;
        }

        private bool IsTargetWithinLeftClickSkillRange(Entity targetMonster, SkillData skill)
        {
            if (_transformComp == null || targetMonster == null || skill == null)
                return false;

            var targetTransform = targetMonster.GetComponent<TransformComponent>();
            if (targetTransform == null)
                return false;

            float effectiveRange = ResolveLeftClickSkillRange(skill);
            if (effectiveRange <= 0f)
                return false;

            var playerPos = new Vector2(_transformComp.Position.x, _transformComp.Position.z);
            var targetPos = new Vector2(targetTransform.Position.x, targetTransform.Position.z);
            return Vector2.Distance(playerPos, targetPos) <= effectiveRange;
        }

        private static float ResolveLeftClickSkillRange(SkillData skill)
        {
            if (skill == null)
                return 0f;

            float baseRange = skill.Type switch
            {
                SkillType.Attack => skill.Range,
                SkillType.AoE => skill.Range > 0.01f ? skill.Range : skill.AreaRadius,
                SkillType.Channeling => skill.Range > 0.01f ? skill.Range : skill.AreaRadius,
                _ => skill.Range > 0.01f ? skill.Range : skill.AreaRadius,
            };

            return Mathf.Max(0f, baseRange) + MonsterSpawner.CollisionRadius + 0.25f;
        }

        private void UpdateLeftClickSkillInputState()
        {
            bool allowSkillInput = _leftMouseIntent == LeftMouseIntent.Skill1;
            _inputComp.SkillInputs[0] = allowSkillInput && (_skill1Action?.WasPressedThisFrame() ?? false);
            _inputComp.SkillHeldInputs[0] = allowSkillInput && (_skill1Action?.IsPressed() ?? false);
            _inputComp.SkillReleasedInputs[0] = allowSkillInput && (_skill1Action?.WasReleasedThisFrame() ?? false);
        }

        private void UpdateSkillInputState(int index, InputAction action)
        {
            if (_inputComp == null || index < 0 || index >= _inputComp.SkillInputs.Length)
                return;

            _inputComp.SkillInputs[index] = action?.WasPressedThisFrame() ?? false;
            _inputComp.SkillHeldInputs[index] = action?.IsPressed() ?? false;
            _inputComp.SkillReleasedInputs[index] = action?.WasReleasedThisFrame() ?? false;
        }

        private void HandleFlaskInputs()
        {
            var equipment = _playerEntity?.GetComponent<EquipmentComponent>();

            var health = _playerEntity?.GetComponent<HealthComponent>();
            var stats = _playerEntity?.GetComponent<StatsComponent>();
            var combat = _playerEntity?.GetComponent<CombatComponent>();
            if (equipment == null || health == null || stats == null || combat == null)
                return;

            for (int i = 0; i < _inputComp.FlaskInputs.Length; i++)
            {
                if (!_inputComp.FlaskInputs[i])
                    continue;

                UseFlask((EquipmentSlot)((int)EquipmentSlot.Flask1 + i), equipment, health, stats, combat);
            }
        }

        private void UseFlask(EquipmentSlot slot, EquipmentComponent equipment, HealthComponent health, StatsComponent stats, CombatComponent combat)
        {
            var flask = equipment.GetEquipped(slot);
            if (flask == null || flask.Type != ItemType.Flask)
                return;

            int chargeCost = Mathf.Max(1, flask.FlaskChargesPerUse);
            if (flask.FlaskCurrentCharges < chargeCost)
            {
                Debug.Log($"[Flask] {slot} 充能不足：{flask.Name}");
                return;
            }

            flask.FlaskCurrentCharges -= chargeCost;

            ApplyRecoveryFlask(flask, health, combat);
            ApplyUtilityFlask(flask, stats, combat);

            Debug.Log($"[Flask] 使用 {flask.Name}，剩余充能 {flask.FlaskCurrentCharges}/{flask.FlaskMaxCharges}");
        }

        private void ApplyRecoveryFlask(ItemData flask, HealthComponent health, CombatComponent combat)
        {
            if (flask == null || health == null || combat == null)
                return;

            float durationSeconds = Mathf.Max(0f, flask.FlaskDurationMs / 1000f);

            if (flask.FlaskRecoverLife > 0)
            {
                float instantLife = flask.FlaskIsInstant
                    ? flask.FlaskRecoverLife * Mathf.Clamp01(flask.FlaskInstantPercent / 100f)
                    : 0f;
                if (instantLife > 0f)
                    health.Heal(instantLife);

                float remainingLife = Mathf.Max(0f, flask.FlaskRecoverLife - instantLife);
                if (remainingLife > 0f)
                    ApplyRecoveryEffect(combat, $"flask_life_{flask.Id}", flask.Name, StatusEffectType.LifeRecovery, remainingLife, durationSeconds);
            }

            if (flask.FlaskRecoverMana > 0)
            {
                float instantMana = flask.FlaskIsInstant
                    ? flask.FlaskRecoverMana * Mathf.Clamp01(flask.FlaskInstantPercent / 100f)
                    : 0f;
                if (instantMana > 0f)
                    health.CurrentMana += instantMana;

                float remainingMana = Mathf.Max(0f, flask.FlaskRecoverMana - instantMana);
                if (remainingMana > 0f)
                    ApplyRecoveryEffect(combat, $"flask_mana_{flask.Id}", flask.Name, StatusEffectType.ManaRecovery, remainingMana, durationSeconds);
            }
        }

        private void ApplyRecoveryEffect(CombatComponent combat, string effectId, string effectName, StatusEffectType effectType, float totalRecovery, float durationSeconds)
        {
            combat.ActiveEffects.RemoveAll(e => e != null && e.Id == effectId);

            float safeDuration = Mathf.Max(0.25f, durationSeconds > 0f ? durationSeconds : 0.25f);
            combat.ActiveEffects.Add(new StatusEffect
            {
                Id = effectId,
                Name = effectName,
                Type = effectType,
                Duration = safeDuration,
                RemainingTime = safeDuration,
                Value = totalRecovery / safeDuration,
                TickInterval = 0.1f,
                TickTimer = 0f,
                Source = _playerEntity,
            });
        }

        private void ApplyUtilityFlask(ItemData flask, StatsComponent stats, CombatComponent combat)
        {
            if (flask == null || stats == null || combat == null)
                return;

            if (flask.FlaskUtilityEffectType == FlaskUtilityEffectKind.None)
                return;

            string effectId = $"flask_utility_{flask.Id}";
            stats.RemoveModifiersFromSource(effectId);
            combat.ActiveEffects.RemoveAll(e => e != null && e.Id == effectId);

            AddUtilityModifier(stats, effectId, flask.FlaskUtilityEffectType, flask.FlaskUtilityEffectValue);

            float durationSeconds = Mathf.Max(0.25f, flask.FlaskDurationMs / 1000f);
            combat.ActiveEffects.Add(new StatusEffect
            {
                Id = effectId,
                Name = flask.Name,
                Type = StatusEffectType.UtilityFlask,
                Duration = durationSeconds,
                RemainingTime = durationSeconds,
                Value = flask.FlaskUtilityEffectValue,
                TickInterval = durationSeconds,
                TickTimer = durationSeconds,
                Source = _playerEntity,
            });
        }

        private void AddUtilityModifier(StatsComponent stats, string source, FlaskUtilityEffectKind effectType, int value)
        {
            switch (effectType)
            {
                case FlaskUtilityEffectKind.MoveSpeed:
                    stats.AddModifier(new StatModifier(StatType.MovementSpeed, ModifierType.PercentAdd, value, source));
                    break;
                case FlaskUtilityEffectKind.Armour:
                    stats.AddModifier(new StatModifier(StatType.Armor, ModifierType.Flat, value, source));
                    break;
                case FlaskUtilityEffectKind.Evasion:
                    stats.AddModifier(new StatModifier(StatType.Evasion, ModifierType.Flat, value, source));
                    break;
                case FlaskUtilityEffectKind.FireResistance:
                    stats.AddModifier(new StatModifier(StatType.FireResistance, ModifierType.Flat, value, source));
                    break;
                case FlaskUtilityEffectKind.ColdResistance:
                    stats.AddModifier(new StatModifier(StatType.ColdResistance, ModifierType.Flat, value, source));
                    break;
                case FlaskUtilityEffectKind.LightningResistance:
                    stats.AddModifier(new StatModifier(StatType.LightningResistance, ModifierType.Flat, value, source));
                    break;
                case FlaskUtilityEffectKind.ChaosResistance:
                    stats.AddModifier(new StatModifier(StatType.ChaosResistance, ModifierType.Flat, value, source));
                    break;
                case FlaskUtilityEffectKind.PhysicalDamageReduction:
                    stats.AddModifier(new StatModifier(StatType.Armor, ModifierType.PercentMore, value, source));
                    break;
                case FlaskUtilityEffectKind.Onslaught:
                    stats.AddModifier(new StatModifier(StatType.MovementSpeed, ModifierType.PercentAdd, 20f, source));
                    stats.AddModifier(new StatModifier(StatType.AttackSpeed, ModifierType.PercentAdd, 20f, source));
                    break;
                case FlaskUtilityEffectKind.ConsecratedGround:
                    stats.AddModifier(new StatModifier(StatType.HealthRegen, ModifierType.Flat, 6f, source));
                    break;
                case FlaskUtilityEffectKind.Phasing:
                    stats.AddModifier(new StatModifier(StatType.MovementSpeed, ModifierType.PercentAdd, 10f, source));
                    break;
            }
        }

        private void UpdateMovementSpeed()
        {
            var stats = _playerEntity.GetComponent<StatsComponent>();
            if (stats != null)
                _movementComp.CurrentSpeed = stats.GetStat(StatType.MovementSpeed);
        }

        private Vector3 GetMouseWorldPosition()
        {
            if (_mainCamera == null) return Vector3.zero;

            var mousePos = Mouse.current?.position.ReadValue() ?? Vector2.zero;
            Ray ray = _mainCamera.ScreenPointToRay(new Vector3(mousePos.x, mousePos.y, 0));

            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                return hit.point;

            if (ray.direction.y != 0)
            {
                float t = -ray.origin.y / ray.direction.y;
                return ray.origin + ray.direction * t;
            }

            return Vector3.zero;
        }

        // ── NPC 生成 ──────────────────────────────────────────────────

        /// <summary>
        /// 清理当前地图的 NPC 实体
        /// </summary>
        private void ClearCurrentMapNpcs()
        {
            var world = GameManager.Instance?.World;
            if (world == null)
            {
                _npcEntities.Clear();
                return;
            }

            foreach (var npc in _npcEntities)
            {
                if (npc != null && npc.IsAlive)
                    world.DestroyEntity(npc);
            }

            _npcEntities.Clear();
        }

        // ── NPC 碰撞检测 ──────────────────────────────────────────────

        /// <summary>
        /// 每帧检测玩家与NPC的碰撞，阻止玩家穿越NPC
        /// </summary>
        private void ResolveNPCCollisions()
        {
            if (_transformComp == null || _movementComp == null) return;

            Vector3 playerPos = _transformComp.Position;
            float   playerR   = PlayerCollisionRadius;

            foreach (var npcEntity in _npcEntities)
            {
                if (npcEntity == null || !npcEntity.IsAlive) continue;

                var npcTransform = npcEntity.GetComponent<TransformComponent>();
                if (npcTransform == null) continue;

                Vector3 npcPos = npcTransform.Position;
                float   minDist = playerR + NPCSpawner.CollisionRadius;

                // 只在XZ平面检测碰撞
                Vector3 diff = new Vector3(playerPos.x - npcPos.x, 0f, playerPos.z - npcPos.z);
                float   dist = diff.magnitude;

                if (dist < minDist && dist > 0.001f)
                {
                    // 只阻止玩家继续走入 NPC，不直接修改玩家位置（避免被推着走）
                    if (_movementComp.HasTarget)
                    {
                        Vector3 toTarget = _movementComp.TargetPosition - npcPos;
                        toTarget.y = 0f;
                        if (toTarget.magnitude < minDist)
                        {
                            _movementComp.HasTarget     = false;
                            _movementComp.MoveDirection = Vector3.zero;
                        }
                    }
                    else
                    {
                        // 玩家已停止但仍重叠（被推进来的），将玩家推出
                        Vector3 pushDir   = diff.normalized;
                        Vector3 corrected = npcPos + pushDir * minDist;
                        _transformComp.Position = new Vector3(corrected.x, playerPos.y, corrected.z);
                        playerPos = _transformComp.Position; // 更新本帧后续迭代用的 playerPos
                    }
                }
            }
        }

        private void ResolveMapDecorationCollisions(bool forcePushOut = false)
        {
            if (_transformComp == null)
                return;

            Vector3 playerPos = _transformComp.Position;
            bool isClickToMove = _movementComp != null && _movementComp.HasTarget;

            for (int i = _currentMapBlockingColliders.Count - 1; i >= 0; i--)
            {
                Collider blocker = _currentMapBlockingColliders[i];
                if (blocker == null)
                {
                    _currentMapBlockingColliders.RemoveAt(i);
                    continue;
                }

                if (!TryResolveMapDecorationOverlap(blocker, ref playerPos, forcePushOut, out Vector3 pushOutDirection))
                    continue;

                _transformComp.Position = playerPos;

                if (isClickToMove && _movementComp != null && ShouldCancelMovementIntoBlocker(playerPos, _movementComp.TargetPosition, pushOutDirection))
                {
                    _movementComp.HasTarget = false;
                    _movementComp.MoveDirection = Vector3.zero;
                    _movementComp.Velocity = Vector3.zero;
                    _movementCancelledByObstacleThisFrame = true;
                    isClickToMove = false;
                }
            }
        }

        private bool TryResolveMapDecorationOverlap(Collider blocker, ref Vector3 playerPos, bool forcePushOut, out Vector3 pushOutDirection)
        {
            pushOutDirection = Vector3.zero;
            bool moved = false;
            float requiredDistance = PlayerCollisionRadius + MapDecorationCollisionSkin;

            for (int pass = 0; pass < MapDecorationMaxResolvePasses; pass++)
            {
                Vector3 closest = blocker.ClosestPoint(playerPos);
                Vector3 planarOffset = new Vector3(playerPos.x - closest.x, 0f, playerPos.z - closest.z);
                float planarDistance = planarOffset.magnitude;
                bool overlapping = planarDistance < requiredDistance - MapDecorationResolveEpsilon;

                if (!overlapping)
                {
                    if (!forcePushOut || planarDistance > 0.0001f)
                        break;
                }

                Vector3 direction;
                float pushDistance;

                if (planarDistance <= 0.0001f)
                {
                    direction = GetMapDecorationPushOutDirection(blocker, playerPos);
                    pushDistance = requiredDistance;
                }
                else
                {
                    direction = planarOffset / planarDistance;
                    pushDistance = requiredDistance - planarDistance;
                    if (pushDistance <= 0.0001f)
                        break;
                }

                playerPos += direction * (pushDistance + MapDecorationResolveEpsilon);
                playerPos.y = _transformComp.Position.y;
                pushOutDirection = direction;
                moved = true;
                forcePushOut = false;
            }

            return moved;
        }

        private static Vector3 GetMapDecorationPushOutDirection(Collider blocker, Vector3 playerPos)
        {
            Bounds bounds = blocker.bounds;
            Vector3 fallback = playerPos - bounds.center;
            fallback.y = 0f;
            if (fallback.sqrMagnitude > 0.0001f)
                return fallback.normalized;

            Vector3 right = blocker.transform.right;
            right.y = 0f;
            if (right.sqrMagnitude > 0.0001f)
                return right.normalized;

            return Vector3.forward;
        }

        private static bool ShouldCancelMovementIntoBlocker(Vector3 playerPos, Vector3 targetPosition, Vector3 pushOutDirection)
        {
            if (pushOutDirection.sqrMagnitude <= 0.0001f)
                return true;

            Vector3 toTarget = targetPosition - playerPos;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude <= 0.0001f)
                return true;

            return Vector3.Dot(toTarget.normalized, pushOutDirection) < MapDecorationBlockedTargetDotThreshold;
        }

        private void OnPlayerDeath()

        {
            Debug.Log("[GameSceneManager] 玩家死亡！");
            GameManager.Instance.World.EventBus.Publish(new PlayerDiedEvent { Player = _playerEntity });
        }

        private void OnEntityDied(EntityDiedEvent evt)
        {
            if (evt.Entity == null || evt.Entity.Tag != "Monster")
                return;

            if (!_enableMonsterGroundDrops)
                return;

            var transform = evt.Entity.GetComponent<TransformComponent>();
            if (transform == null)
                return;

            TryPublishGroundDrop(CreateRandomDroppedEquipmentOrNull(), transform.Position, "装备");
            TryPublishGroundDrop(CreateRandomDroppedCurrencyOrNull(), transform.Position, "通货");
        }

        private ItemData CreateRandomDroppedEquipmentOrNull()
        {
            if (Random.value > _monsterDropChance)
                return null;

            ItemType itemType = Random.Range(0, 3) switch
            {
                0 => ItemType.Weapon,
                1 => ItemType.Armour,
                _ => ItemType.Accessory,
            };

            return ItemFactory.CreateRandomItem(Mathf.Max(1, _monsterDropItemLevel), itemType);
        }

        private ItemData CreateRandomDroppedCurrencyOrNull()
        {
            if (Random.value > _monsterCurrencyDropChance)
                return null;

            var currencies = CurrencyConfigLoader.BaseCurrencies;
            if (currencies == null || currencies.Count == 0)
                return null;

            int dropLevel = Mathf.Max(1, _monsterDropItemLevel);
            var candidates = new List<CurrencyBaseData>();
            for (int i = 0; i < currencies.Count; i++)
            {
                var currency = currencies[i];
                if (currency == null)
                    continue;

                int currencyDropLevel = ParseCurrencyDropLevel(currency.CurrencyDropLevel);
                if (currencyDropLevel <= dropLevel)
                    candidates.Add(currency);
            }

            if (candidates.Count == 0)
                return null;

            var selectedCurrency = candidates[Random.Range(0, candidates.Count)];
            int minStack = Mathf.Max(1, Mathf.Min(_monsterCurrencyStackRange.x, _monsterCurrencyStackRange.y));
            int maxStack = Mathf.Max(minStack, Mathf.Max(_monsterCurrencyStackRange.x, _monsterCurrencyStackRange.y));
            int stackCount = Random.Range(minStack, maxStack + 1);
            var bagItem = CurrencyBagDataFactory.CreateFromConfig(selectedCurrency, stackCount);
            return bagItem?.ToItemData();
        }

        private static int ParseCurrencyDropLevel(string value)
        {
            return int.TryParse(value, out var parsed) ? Mathf.Max(1, parsed) : 1;
        }

        private void TryPublishGroundDrop(ItemData droppedItem, Vector3 position, string dropKind)
        {
            if (droppedItem == null)
                return;

            GameManager.Instance?.World?.EventBus.Publish(new GroundItemDroppedEvent
            {
                Item = droppedItem,
                Position = position,
            });

            string rarityText = droppedItem.Type == ItemType.Currency ? "Currency" : droppedItem.Rarity.ToString();
            string stackText = droppedItem.IsStackable ? $" x{Mathf.Max(1, droppedItem.StackCount)}" : string.Empty;
            Debug.Log($"[GameSceneManager] 怪物掉落{dropKind}: [{rarityText}] {droppedItem.Name}{stackText}");
        }

        /// <summary>
        /// 点击NPC名称标签时，寻路到该NPC附近
        /// </summary>
        private void OnNpcLabelClicked(Vector3 npcWorldPos)

        {
            if (_movementComp == null) return;

            _leftMouseIntent = LeftMouseIntent.Blocked;
            if (_inputComp != null)
                _inputComp.HasClickTarget = false;

            // 关闭已有对话框
            _npcDialogPanel?.Close();
            ClearPendingGroundItemPickup();

            // 找到对应的NPC实体
            _targetNpcEntity = FindNpcEntityByWorldPos(npcWorldPos);
            if (_targetNpcEntity == null)
            {
                _walkingToNpc = false;
                return;
            }

            // 计算NPC附近的目标点：在玩家与NPC连线方向上，距NPC一个碰撞半径+玩家半径的位置
            const float playerR = 0.4f;
            float stopDist = NPCSpawner.CollisionRadius + playerR + 0.1f;

            Vector3 playerPos = _transformComp?.Position ?? Vector3.zero;
            Vector3 dir = npcWorldPos - playerPos;
            dir.y = 0f;

            Vector3 targetPos;
            if (dir.magnitude > stopDist)
            {
                // 目标点在NPC前方 stopDist 处
                targetPos = npcWorldPos - dir.normalized * stopDist;
            }
            else
            {
                // 已经很近了，直接打开对话框
                _movementComp.HasTarget = false;
                _movementComp.MoveDirection = Vector3.zero;
                OpenNpcDialog(_targetNpcEntity);
                return;
            }

            targetPos.y = playerPos.y;
            _movementComp.TargetPosition = targetPos;
            _movementComp.HasTarget      = true;
            _walkingToNpc                = true;
            _prevHasTarget               = true;
        }

        private void OnGroundItemLabelClicked(ItemData item, Vector3 itemWorldPos)
        {
            if (_movementComp == null || _groundItemLabelRenderer == null || item == null)
                return;

            _leftMouseIntent = LeftMouseIntent.Blocked;
            if (_inputComp != null)
                _inputComp.HasClickTarget = false;

            _npcDialogPanel?.Close();
            _npcMeshRenderer?.SetTalkingNpc(null);
            _targetNpcEntity = null;
            _walkingToNpc = false;

            if (!_groundItemLabelRenderer.ContainsItem(item))
            {
                ClearPendingGroundItemPickup();
                return;
            }

            Vector3 playerPos = _transformComp?.Position ?? Vector3.zero;
            Vector3 flattenedPlayerPos = new Vector3(playerPos.x, 0f, playerPos.z);
            Vector3 flattenedItemPos = new Vector3(itemWorldPos.x, 0f, itemWorldPos.z);
            float distance = Vector3.Distance(flattenedPlayerPos, flattenedItemPos);

            if (distance <= GroundPickupStopDistance)
            {
                _movementComp.HasTarget = false;
                _movementComp.MoveDirection = Vector3.zero;
                _groundItemLabelRenderer.TryPickupItem(item);
                ClearPendingGroundItemPickup();
                return;
            }

            _pendingGroundPickupItem = item;
            _pendingGroundPickupPosition = itemWorldPos;
            _walkingToGroundItem = true;
            _prevHasTarget = true;

            Vector3 direction = itemWorldPos - playerPos;
            direction.y = 0f;
            Vector3 targetPos = itemWorldPos;
            if (direction.sqrMagnitude > 0.0001f)
                targetPos = itemWorldPos - direction.normalized * Mathf.Max(0.05f, GroundPickupStopDistance - 0.1f);

            targetPos.y = playerPos.y;
            _movementComp.TargetPosition = targetPos;
            _movementComp.HasTarget = true;
        }

        private void CheckGroundItemArrival()
        {
            if (!_walkingToGroundItem || _groundItemLabelRenderer == null || _pendingGroundPickupItem == null)
                return;

            if (!_groundItemLabelRenderer.ContainsItem(_pendingGroundPickupItem))
            {
                ClearPendingGroundItemPickup();
                return;
            }

            if (_transformComp == null || _movementComp == null)
                return;

            Vector3 playerPos = _transformComp.Position;
            float distance = Vector3.Distance(
                new Vector3(playerPos.x, 0f, playerPos.z),
                new Vector3(_pendingGroundPickupPosition.x, 0f, _pendingGroundPickupPosition.z));

            if (distance <= GroundPickupStopDistance)
            {
                _movementComp.HasTarget = false;
                _movementComp.MoveDirection = Vector3.zero;
                _groundItemLabelRenderer.TryPickupItem(_pendingGroundPickupItem);
                ClearPendingGroundItemPickup();
                return;
            }

            if (!_movementComp.HasTarget && _prevHasTarget)
            {
                if (_movementCancelledByObstacleThisFrame)
                {
                    _prevHasTarget = false;
                    return;
                }

                _groundItemLabelRenderer.TryPickupItem(_pendingGroundPickupItem);
                ClearPendingGroundItemPickup();
                return;
            }

            _prevHasTarget = _movementComp.HasTarget;
        }


        private void ClearPendingGroundItemPickup()
        {
            _pendingGroundPickupItem = null;
            _pendingGroundPickupPosition = Vector3.zero;
            _walkingToGroundItem = false;
        }

        /// <summary>
        /// 每帧检测是否到达目标NPC（基于距离检测，进入交互范围后立即停下并打开对话）
        /// </summary>
        private void CheckNpcArrival()
        {
            if (!_walkingToNpc || _targetNpcEntity == null) return;
            if (_transformComp == null || _movementComp == null) return;

            var npcTransform = _targetNpcEntity.GetComponent<TransformComponent>();
            if (npcTransform == null) return;

            const float playerR = 0.4f;
            float interactDist = NPCSpawner.CollisionRadius + playerR + 0.3f;

            Vector3 playerPos = _transformComp.Position;
            Vector3 npcPos    = npcTransform.Position;

            float dist = Vector3.Distance(
                new Vector3(playerPos.x, 0f, playerPos.z),
                new Vector3(npcPos.x,    0f, npcPos.z));

            if (dist <= interactDist)
            {
                _movementComp.HasTarget = false;
                _movementComp.MoveDirection = Vector3.zero;
                _walkingToNpc = false;
                OpenNpcDialog(_targetNpcEntity);
                return;
            }

            if (!_movementComp.HasTarget && _prevHasTarget)
            {
                if (_movementCancelledByObstacleThisFrame)
                {
                    _walkingToNpc = false;
                    _prevHasTarget = false;
                    return;
                }

                _walkingToNpc = false;
                OpenNpcDialog(_targetNpcEntity);
                return;
            }

            _prevHasTarget = _movementComp.HasTarget;
        }

        /// <summary>
        /// 打开NPC对话框
        /// </summary>
        private void OpenNpcDialog(Entity npcEntity)
        {
            if (_npcDialogPanel == null || npcEntity == null) return;

            var npcComp = npcEntity.GetComponent<NPCComponent>();
            if (npcComp == null) return;

            string npcName = npcComp.NPCName;
            int    npcId   = npcComp.NPCID;

            // 从配置文件读取对话内容
            string dialog = NpcConfigLoader.GetDialog(npcId);

            // 从配置文件读取按钮列表
            var buttons = NpcConfigLoader.GetButtons(npcId);
            var options = new List<NpcDialogOption>();

            foreach (var (btnName, eventId) in buttons)
            {
                // 捕获局部变量，避免闭包问题
                string capturedName    = btnName;
                string capturedEventId = eventId;

                options.Add(new NpcDialogOption(capturedName, () =>
                {
                    OnNpcButtonClicked(npcName, capturedName, capturedEventId);
                }));
            }

            _npcDialogPanel.Open(npcName, dialog, options);
            // 通知 NpcMeshRenderer 当前对话的 NPC，使其平滑转向玩家
            _npcMeshRenderer?.SetTalkingNpc(npcEntity);
            // 对话框打开（NpcMeshRenderer 头顶标签仍正常显示）
        }

        /// <summary>
        /// NPC按钮点击处理（根据 EventID 分发逻辑）
        /// </summary>
        private void OnNpcButtonClicked(string npcName, string btnName, string eventId)
        {
            Debug.Log($"[NpcDialog] {npcName} 点击按钮: {btnName}（EventID={eventId}）");

            // 将 eventId 字符串解析为枚举
            if (!System.Enum.TryParse(eventId, out NpcButtonEventType eventType))
                eventType = NpcButtonEventType.None;

            switch (eventType)
            {
                case NpcButtonEventType.CloseDialog:
                    _npcDialogPanel?.Close();
                    _npcMeshRenderer?.SetTalkingNpc(null);
                    _targetNpcEntity = null;
                    _walkingToNpc    = false;
                    ClearPendingGroundItemPickup();
                    break;

                case NpcButtonEventType.EnhanceEquipment:
                    // TODO: 打开强化装备界面
                    Debug.Log("[NpcDialog] 强化装备");
                    break;

                case NpcButtonEventType.OpenShop:
                    OpenShop();
                    break;

                case NpcButtonEventType.OpenDoorPanel:
                    OpenDoorPanel();
                    break;

                default:
                    Debug.LogWarning($"[NpcDialog] 未处理的 EventID: {eventId}");
                    break;
            }
        }

        /// <summary>
        /// 根据世界坐标找到最近的NPC实体
        /// </summary>
        private Entity FindNpcEntityByWorldPos(Vector3 worldPos)
        {
            Entity closest = null;
            float  minDist = float.MaxValue;

            foreach (var npc in _npcEntities)
            {
                if (npc == null || !npc.IsAlive) continue;
                var t = npc.GetComponent<TransformComponent>();
                if (t == null) continue;

                float d = Vector3.Distance(
                    new Vector3(t.Position.x, 0f, t.Position.z),
                    new Vector3(worldPos.x,   0f, worldPos.z));
                if (d < minDist)
                {
                    minDist = d;
                    closest = npc;
                }
            }
            return closest;
        }

        /// <summary>
        /// 点击对话框外部时：关闭对话框，清除目标NPC
        /// </summary>
        private void OnDialogClickOutside()
        {
            _npcMeshRenderer?.SetTalkingNpc(null);
            _targetNpcEntity = null;
            _walkingToNpc    = false;
            ClearPendingGroundItemPickup();
        }

        // ── 摄像机 ────────────────────────────────────────────────────

        /// <summary>
        /// 设置等距跟随摄像机（跟随ECS实体的逻辑位置）
        /// </summary>
        private void SetupCamera()
        {
            Debug.Log("[GameSceneManager] SetupCamera 开始");

            // 销毁场景中已有的 Camera（避免多摄像机冲突）
            // 使用 DestroyImmediate 确保立即销毁，避免同帧内两个 MainCamera 冲突
            var existingCam = Camera.main;
            if (existingCam != null)
            {
                Debug.Log($"[GameSceneManager] 销毁已有摄像机: {existingCam.gameObject.name}");
                DestroyImmediate(existingCam.gameObject);
            }

            // 创建摄像机 GameObject，CameraController 的 Awake 会自动添加 Camera 和 PlayerMarkerRenderer
            var camGo = new GameObject("GameCamera");
            Debug.Log("[GameSceneManager] 创建 GameCamera GameObject");

            _cameraController = camGo.AddComponent<CameraController>();
            Debug.Log($"[GameSceneManager] CameraController 已添加: {_cameraController}");

            // 缓存摄像机引用（供鼠标射线检测使用）
            _mainCamera = camGo.GetComponent<Camera>();
            Debug.Log($"[GameSceneManager] _mainCamera = {_mainCamera}");

            // 设置跟随实体（CameraController.Awake 已执行，_markerRenderer 已就绪）
            _cameraController.SetPlayerEntity(_playerEntity);

            // 缓存 NpcMeshRenderer 引用，订阅NPC名称点击事件
            _npcMeshRenderer = camGo.GetComponent<NpcMeshRenderer>();
            if (_npcMeshRenderer != null)
            {
                _npcMeshRenderer.OnNpcLabelClicked += OnNpcLabelClicked;
                _npcMeshRenderer.SetPlayerTransform(_transformComp);
            }

            _groundItemLabelRenderer = camGo.GetComponent<GroundItemLabelRenderer>();
            if (_groundItemLabelRenderer != null)
                _groundItemLabelRenderer.OnGroundItemLabelClicked += OnGroundItemLabelClicked;

            // 从 Resources 加载 ChatPanel 预制体，确保 Inspector 绑定的引用有效

            var chatPanelGo = UIManager.Instance.GetUI("UI/ChatPanel");
            if (chatPanelGo != null)
            {
                _npcDialogPanel = chatPanelGo.GetComponent<NpcDialogPanel>();
                if (_npcDialogPanel != null)
                {
                    _npcDialogPanel.OnClose += OnDialogClickOutside;
                    // 进入场景时确保对话框处于关闭状态
                    _npcDialogPanel.Close();
                }
                else
                    Debug.LogError("[GameSceneManager] ChatPanel 预制体上未找到 NpcDialogPanel 组件。");
            }
            else
            {
                Debug.LogError("[GameSceneManager] 未能加载 UI/ChatPanel，请检查 Resources 路径。");
            }

            // 从 Resources 加载 CustomPanel 预制体（商店面板）
            var shopPanelGo = UIManager.Instance.GetUI("UI/CustomPanel");
            if (shopPanelGo != null)
            {
                _shopPanel = shopPanelGo.GetComponent<ShopPanel>();
                if (_shopPanel != null)
                {
                    // 进入场景时确保商店处于关闭状态
                    _shopPanel.Close();
                }
                else
                    Debug.LogError("[GameSceneManager] CustomPanel 预制体上未找到 ShopPanel 组件。");
            }
            else
            {
                Debug.LogError("[GameSceneManager] 未能加载 UI/CustomPanel，请检查 Resources 路径。");
            }

            // 从 Resources 加载 DoorPanel 预制体（传送门面板）
            var doorPanelGo = UIManager.Instance.GetUI("UI/DoorPanel");
            if (doorPanelGo != null)
            {
                _doorPanel = doorPanelGo.GetComponent<DoorPanel>();
                if (_doorPanel == null)
                    _doorPanel = doorPanelGo.AddComponent<DoorPanel>();

                if (_doorPanel != null)
                {
                    _doorPanel.MapSelected -= OnDoorPanelMapSelected;
                    _doorPanel.MapSelected += OnDoorPanelMapSelected;

                    if (_currentMapLevel == null)
                        _currentMapLevel = BuildDefaultMapContext();

                    // 进入场景时确保传送门面板处于关闭状态
                    _doorPanel.Close();
                }
                else
                    Debug.LogError("[GameSceneManager] DoorPanel 预制体初始化失败。");

            }
            else
            {
                Debug.LogError("[GameSceneManager] 未能加载 UI/DoorPanel，请检查 Resources 路径。");
            }

            Debug.Log("[GameSceneManager] SetupCamera 完成");

        }

        // ── GM 面板 ────────────────────────────────────────────────

        /// <summary>
        /// 初始化 GM 面板（挂载在 GameSceneManager 自身 GameObject 上）
        /// </summary>
        private void SetupGMPanel()
        {
            _gmPanel = gameObject.GetComponent<GMPanel>();
            if (_gmPanel == null)
                _gmPanel = gameObject.AddComponent<GMPanel>();

            _gmPanel.Init(GameManager.Instance.World, _playerEntity);
        }

        private void SetupClientSkillExtensionPanel()
        {
            _clientSkillExtensionPanel = gameObject.GetComponent<ClientSkillExtensionPanel>();
            if (_clientSkillExtensionPanel == null)
                _clientSkillExtensionPanel = gameObject.AddComponent<ClientSkillExtensionPanel>();

            _clientSkillExtensionPanel.Init(GameManager.Instance.World, _playerEntity);
        }

        /// <summary>
        /// 打开商店面板
        /// </summary>
        private void OpenShop()

        {
            if (_shopPanel == null) return;
            // 先关闭对话框，再打开商店，避免两个面板同时打开时 CloseAll 误关商店
            _npcDialogPanel?.Close();
            _npcMeshRenderer?.SetTalkingNpc(null);
            _shopPanel.Open();
        }

        /// <summary>
        /// 打开传送门面板
        /// </summary>
        private void OpenDoorPanel()
        {
            if (_doorPanel == null) return;
            _npcDialogPanel?.Close();
            _npcMeshRenderer?.SetTalkingNpc(null);
            _doorPanel.Open();
        }

        private void OnDoorPanelMapSelected(MapLevelData mapLevel)
        {
            if (mapLevel == null)
                return;

            _currentMapLevel = mapLevel;
            _doorPanel?.Close();
            _npcDialogPanel?.Close();
            _npcMeshRenderer?.SetTalkingNpc(null);
            ClearPendingGroundItemPickup();

            var saveData = SceneLoader.PendingCharacterData;
            if (saveData == null)
                saveData = new CharacterSaveData("debug", "调试角色", 1, "本地");

            Debug.Log($"[DoorPanel] 即将进入任务场景 {SceneLoader.SceneMission}，地图：{mapLevel.MapName}（MapID={mapLevel.MapID}, SceneID={mapLevel.SceneID}, CfgID={mapLevel.CfgID}）");
            SceneLoader.LoadMissionScene(saveData, mapLevel);
        }

        private void TeleportPlayerToCurrentMap()
        {
            if (_currentMapLevel == null || _playerEntity == null || !_playerEntity.IsAlive)
                return;

            MapLayoutConfigLoader.Reload();
            Vector3 targetPosition = ResolveMapSpawnPoint(_currentMapLevel);
            _playerSpawnPoint = targetPosition;

            if (_transformComp != null)
                _transformComp.Position = targetPosition;

            if (_movementComp != null)
            {
                _movementComp.TargetPosition = targetPosition;
                _movementComp.MoveDirection = Vector3.zero;
                _movementComp.Velocity = Vector3.zero;
                _movementComp.HasTarget = false;
            }

            if (_inputComp != null)
            {
                _inputComp.HasClickTarget = false;
                _inputComp.ClickTargetPosition = targetPosition;
                _inputComp.MouseWorldPosition = targetPosition;
            }

            _walkingToNpc = false;
            _prevHasTarget = false;
            _targetNpcEntity = null;
            _npcMeshRenderer?.SetTalkingNpc(null);
            ClearPendingGroundItemPickup();

            Debug.Log($"[DoorPanel] 已传送到 {_currentMapLevel.MapName}（MapID={_currentMapLevel.MapID}, SceneID={_currentMapLevel.SceneID}, CfgID={_currentMapLevel.CfgID}），出生点：{targetPosition}");
        }

        private void RefreshCurrentMapLayout()
        {
            ClearCurrentMapNpcs();

            var world = GameManager.Instance?.World;
            if (world == null)
                return;

            string activeSceneName = SceneManager.GetActiveScene().name;
            if (activeSceneName == SceneLoader.SceneGame)
            {
                _npcEntities = NPCSpawner.SpawnAllNPCs(world, activeSceneName);
                Debug.Log($"[GameSceneManager] {SceneLoader.SceneGame} 使用 NPCDataConf 场景过滤 + 固定坐标布局，NPC 实体数：{_npcEntities.Count}");
                return;
            }

            if (_currentMapLevel == null)
                return;

            MapLayoutConfigLoader.Reload();
            IReadOnlyList<MapNpcLayoutData> npcLayouts = MapLayoutConfigLoader.GetNpcLayoutsByCfgId(_currentMapLevel.CfgID);
            if (npcLayouts == null || npcLayouts.Count == 0)
            {
                _npcEntities = NPCSpawner.SpawnAllNPCs(world, activeSceneName);
                Debug.LogWarning($"[GameSceneManager] 地图 {_currentMapLevel.MapName}（CfgID={_currentMapLevel.CfgID}）未配置 NPC 布局，已回退到 NPCDataConf 默认布局。");
                return;
            }

            WarnIfMapLayoutMissingDoorPanelNpc(npcLayouts);

            Vector3 layoutOrigin = _playerSpawnPoint;
            var requests = new List<NPCSpawnRequest>(npcLayouts.Count);

            foreach (var layout in npcLayouts)
            {
                if (layout == null || layout.NPCIDInt <= 0)
                {
                    Debug.LogWarning($"[GameSceneManager] 跳过非法地图 NPC 布局：CfgID={layout?.CfgID}, NPCID={layout?.NPCID}");
                    continue;
                }

                Vector3 npcPosition = layoutOrigin + new Vector3(layout.OffsetXFloat, 0f, layout.OffsetZFloat);
                requests.Add(new NPCSpawnRequest
                {
                    NPCID = layout.NPCIDInt,
                    Position = npcPosition,
                    UseFixedConfigPosition = false,
                });

            }

            _npcEntities = NPCSpawner.SpawnNPCs(world, requests, activeSceneName);
            Debug.Log($"[GameSceneManager] 已按 CfgID={_currentMapLevel.CfgID} 刷新地图布局，当前地图：{_currentMapLevel.MapName}，NPC 实体数：{_npcEntities.Count}，玩家出生点：{_playerSpawnPoint}");
        }

        private void WarnIfMapLayoutMissingDoorPanelNpc(IReadOnlyList<MapNpcLayoutData> npcLayouts)
        {
            if (npcLayouts == null || npcLayouts.Count == 0)
                return;

            foreach (var layout in npcLayouts)
            {
                if (layout == null || layout.NPCIDInt <= 0)
                    continue;

                List<(string name, string eventId)> buttons = NpcConfigLoader.GetButtons(layout.NPCIDInt);
                if (buttons == null || buttons.Count == 0)
                    continue;

                foreach (var button in buttons)
                {
                    if (int.TryParse(button.eventId, out int eventId) && eventId == (int)NpcButtonEventType.OpenDoorPanel)
                        return;
                }
            }

            Debug.LogWarning($"[GameSceneManager] 当前地图 {_currentMapLevel.MapName}（CfgID={_currentMapLevel.CfgID}）的 NPC 布局中不存在可打开 DoorPanel 的 NPC；如需继续切图，请至少保留一个带 EventID=1004 的 NPC。");
        }

        private void RefreshCurrentMapContent()

        {

            ClearCurrentMapMonsters();

            string activeSceneName = SceneManager.GetActiveScene().name;
            if (activeSceneName != SceneLoader.SceneMission)
            {
                Debug.Log($"[GameSceneManager] {activeSceneName} 不刷地图怪物，已跳过地图内容构建。");
                return;
            }

            if (_currentMapLevel == null)
                return;

            var world = GameManager.Instance?.World;
            if (world == null)
                return;

            MapContentConfigLoader.Reload();
            IReadOnlyList<MapContentData> contents = MapContentConfigLoader.GetByCfgId(_currentMapLevel.CfgID);
            if (contents == null || contents.Count == 0)
            {
                Debug.LogWarning($"[GameSceneManager] 地图 {_currentMapLevel.MapName}（CfgID={_currentMapLevel.CfgID}）未配置地图内容。");
                return;
            }

            Vector3 playerPosition = _transformComp != null ? _transformComp.Position : _playerSpawnPoint;

            foreach (var content in contents)
            {
                if (content == null)
                    continue;

                if (content.MonsterIDInt <= 0 || content.CountInt <= 0)
                {
                    Debug.LogWarning($"[GameSceneManager] 跳过非法地图内容配置：CfgID={content.CfgID}, MonsterID={content.MonsterID}, Count={content.Count}");
                    continue;
                }

                Vector3 spawnCenter = playerPosition + new Vector3(content.OffsetXFloat, 0f, content.OffsetZFloat);
                var spawned = MonsterSpawner.SpawnMonsters(world, content.MonsterIDInt, content.CountInt, spawnCenter);
                if (spawned == null || spawned.Count == 0)
                    continue;

                _currentMapMonsterEntities.AddRange(spawned);
            }

            Debug.Log($"[GameSceneManager] 已按 CfgID={_currentMapLevel.CfgID} 刷新地图内容，当前地图：{_currentMapLevel.MapName}，怪物实体数：{_currentMapMonsterEntities.Count}");
        }

        private void ClearCurrentMapMonsters()
        {
            var world = GameManager.Instance?.World;
            if (world == null)
            {
                _currentMapMonsterEntities.Clear();
                return;
            }

            foreach (var monster in _currentMapMonsterEntities)
            {
                if (monster != null && monster.IsAlive)
                    world.DestroyEntity(monster);
            }

            _currentMapMonsterEntities.Clear();
        }

        private void RefreshCurrentMapDecoration()
        {
            ClearCurrentMapDecorations();

            if (!_autoGenerateEnvironment || _currentMapLevel == null)
                return;

            MapDecorationConfigLoader.Reload();
            IReadOnlyList<MapDecorationData> decorations = MapDecorationConfigLoader.GetByCfgId(_currentMapLevel.CfgID);
            if (decorations == null || decorations.Count == 0)
            {
                Debug.LogWarning($"[GameSceneManager] 地图 {_currentMapLevel.MapName}（CfgID={_currentMapLevel.CfgID}）未配置地图装饰布局。");
                return;
            }

            Transform root = EnsureMapDecorationRoot();
            Vector3 layoutOrigin = _playerSpawnPoint;

            foreach (var decoration in decorations)
            {
                if (decoration == null || string.IsNullOrWhiteSpace(decoration.DecorationType))
                {
                    Debug.LogWarning($"[GameSceneManager] 跳过非法地图装饰配置：CfgID={decoration?.CfgID}, DecorationType={decoration?.DecorationType}");
                    continue;
                }

                GameObject decorationObject = CreateDecorationObject(decoration, layoutOrigin, root);
                if (decorationObject != null)
                    _currentMapDecorationObjects.Add(decorationObject);
            }

            Debug.Log($"[GameSceneManager] 已按 CfgID={_currentMapLevel.CfgID} 刷新地图装饰，当前地图：{_currentMapLevel.MapName}，装饰物数量：{_currentMapDecorationObjects.Count}");
        }

        private void ClearCurrentMapDecorations()
        {
            foreach (var decorationObject in _currentMapDecorationObjects)
            {
                if (decorationObject != null)
                    Destroy(decorationObject);
            }

            _currentMapDecorationObjects.Clear();
            _currentMapBlockingColliders.Clear();
        }

        private Transform EnsureMapDecorationRoot()
        {
            if (_mapDecorationRoot != null)
                return _mapDecorationRoot;

            var root = new GameObject("MapDecorations");
            root.transform.SetParent(transform, false);
            _mapDecorationRoot = root.transform;
            return _mapDecorationRoot;
        }

        private GameObject CreateDecorationObject(MapDecorationData decoration, Vector3 layoutOrigin, Transform parent)
        {
            PrimitiveType primitiveType;
            string objectName;
            Color color;
            Vector3 defaultScale;
            float baseHeight;

            switch (decoration.DecorationType.Trim().ToLowerInvariant())
            {
                case "pillar":
                    primitiveType = PrimitiveType.Cylinder;
                    objectName = "MapPillar";
                    color = new Color(0.42f, 0.39f, 0.34f);
                    defaultScale = new Vector3(1f, 2f, 1f);
                    baseHeight = 2f;
                    break;
                case "crate":
                    primitiveType = PrimitiveType.Cube;
                    objectName = "MapCrate";
                    color = new Color(0.45f, 0.27f, 0.14f);
                    defaultScale = new Vector3(1.6f, 1f, 1.6f);
                    baseHeight = 1f;
                    break;
                case "marker":
                    primitiveType = PrimitiveType.Sphere;
                    objectName = "MapMarker";
                    color = new Color(0.22f, 0.62f, 0.83f);
                    defaultScale = new Vector3(1.2f, 1.2f, 1.2f);
                    baseHeight = 1f;
                    break;
                case "shrine":
                    primitiveType = PrimitiveType.Capsule;
                    objectName = "MapShrine";
                    color = new Color(0.72f, 0.67f, 0.4f);
                    defaultScale = new Vector3(1.5f, 2f, 1.5f);
                    baseHeight = 2f;
                    break;
                default:
                    Debug.LogWarning($"[GameSceneManager] 未知地图装饰类型：{decoration.DecorationType}，已跳过。可选值：Pillar / Crate / Marker / Shrine");
                    return null;
            }

            var decorationObject = GameObject.CreatePrimitive(primitiveType);
            decorationObject.name = objectName;
            decorationObject.transform.SetParent(parent, false);

            Vector3 scale = new Vector3(
                decoration.ScaleXFloat > 0f ? decoration.ScaleXFloat : defaultScale.x,
                decoration.ScaleYFloat > 0f ? decoration.ScaleYFloat : defaultScale.y,
                decoration.ScaleZFloat > 0f ? decoration.ScaleZFloat : defaultScale.z);

            decorationObject.transform.localScale = scale;
            decorationObject.transform.rotation = Quaternion.Euler(0f, decoration.RotationYFloat, 0f);

            Vector3 position = layoutOrigin + new Vector3(decoration.OffsetXFloat, 0f, decoration.OffsetZFloat);
            position.y = baseHeight * scale.y * 0.5f;
            decorationObject.transform.position = position;

            var collider = decorationObject.GetComponent<Collider>();
            if (collider != null)
            {
                collider.isTrigger = false;
                _currentMapBlockingColliders.Add(collider);
            }

            SetMaterialColor(decorationObject, color);
            return decorationObject;
        }

        private static Vector3 ResolveMapSpawnPoint(MapLevelData mapLevel)
        {
            if (mapLevel == null)
                return DefaultGameScenePlayerSpawnPoint;

            string activeSceneName = SceneManager.GetActiveScene().name;
            if (activeSceneName == SceneLoader.SceneGame)
                return MapLayoutConfigLoader.GetGameSceneSpawnPoint(mapLevel.CfgID, DefaultGameScenePlayerSpawnPoint);

            int anchorIndex = 0;
            if (!string.IsNullOrWhiteSpace(mapLevel.MapID) && int.TryParse(mapLevel.MapID, out int parsedMapId))
                anchorIndex = Mathf.Abs(parsedMapId - 1) % MapTeleportAnchors.Length;

            Vector3 anchor = MapTeleportAnchors[anchorIndex];
            Vector3 layoutOffset = MapLayoutConfigLoader.GetPlayerSpawnOffset(mapLevel.CfgID);
            return anchor + layoutOffset;
        }

        private static MapLevelData BuildDefaultMapContext()
        {
            return new MapLevelData
            {
                MapID = "1",
                SceneID = "1",
                CfgID = "1001",
                MapName = "遗忘之林"
            };
        }

    }
}