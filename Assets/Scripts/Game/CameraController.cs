using UnityEngine;
using POELike.ECS.Core;
using POELike.ECS.Components;

namespace POELike.Game
{
    /// <summary>
    /// 等距跟随摄像机控制器
    /// 类似流放之路的俯视角跟随摄像机
    /// 自身 GameObject 上同时挂载 Camera 和 PlayerMarkerRenderer
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        [Header("跟随目标")]
        [SerializeField] private Transform _target;

        // ECS实体跟随（优先级高于Transform）
        private Entity _playerEntity;
        private TransformComponent _playerTransform;

        // 玩家位置标记渲染器（Shader绘制，无额外GameObject）
        private PlayerMarkerRenderer _markerRenderer;

        // 玩家角色 Mesh 渲染器（Graphics.DrawMesh，无额外GameObject）
        private PlayerMeshRenderer _meshRenderer;

        // NPC标记渲染器
        private NpcMarkerRenderer _npcMarkerRenderer;

        // NPC Mesh 渲染器（Graphics.DrawMesh，无额外GameObject）
        private NpcMeshRenderer _npcMeshRenderer;

        // 怪物 Mesh 渲染器（Graphics.DrawMesh，无额外GameObject）
        private MonsterMeshRenderer _monsterMeshRenderer;

        // 地面掉落名称渲染器
        private GroundItemLabelRenderer _groundItemLabelRenderer;

        // 技能运行时范围渲染器
        private SkillRuntimeRenderer _skillRuntimeRenderer;

        [Header("摄像机参数")]
        [SerializeField] private float  _distance    = 25f;   // 摄像机到角色的距离
        [SerializeField] private float  _angle       = 55f;   // 俯视角度（X轴旋转）
        [SerializeField] private float  _smoothSpeed = 8f;    // 跟随平滑速度
        [SerializeField] private float  _rotationY   = 0f;    // 水平旋转角（0=正北）

        // 摄像机引用
        private Camera _camera;

        // 目标偏移（等距视角的固定偏移）
        private Vector3    _offset;
        // 固定旋转（由角度参数决定，不随目标变化）
        private Quaternion _fixedRotation;

        // ── 初始化 ────────────────────────────────────────────────────

        private void Awake()
        {
            // 获取或添加 Camera 组件
            _camera = GetComponent<Camera>();
            if (_camera == null)
                _camera = gameObject.AddComponent<Camera>();

            // 配置摄像机基础参数
            _camera.clearFlags    = CameraClearFlags.Skybox;
            _camera.fieldOfView   = 40f;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane  = 500f;
            _camera.tag           = "MainCamera";

            // 在同一 GameObject 上挂载玩家标记渲染器
            _markerRenderer = gameObject.GetComponent<PlayerMarkerRenderer>();
            if (_markerRenderer == null)
                _markerRenderer = gameObject.AddComponent<PlayerMarkerRenderer>();

            // 在同一 GameObject 上挂载玩家角色 Mesh 渲染器
            _meshRenderer = gameObject.GetComponent<PlayerMeshRenderer>();
            if (_meshRenderer == null)
                _meshRenderer = gameObject.AddComponent<PlayerMeshRenderer>();

            // 在同一 GameObject 上挂载NPC标记渲染器
            _npcMarkerRenderer = gameObject.GetComponent<NpcMarkerRenderer>();
            if (_npcMarkerRenderer == null)
                _npcMarkerRenderer = gameObject.AddComponent<NpcMarkerRenderer>();

            // 在同一 GameObject 上挂载NPC Mesh渲染器
            _npcMeshRenderer = gameObject.GetComponent<NpcMeshRenderer>();
            if (_npcMeshRenderer == null)
                _npcMeshRenderer = gameObject.AddComponent<NpcMeshRenderer>();

            // 在同一 GameObject 上挂载怪物 Mesh 渲染器
            _monsterMeshRenderer = gameObject.GetComponent<MonsterMeshRenderer>();
            if (_monsterMeshRenderer == null)
                _monsterMeshRenderer = gameObject.AddComponent<MonsterMeshRenderer>();

            // 在同一 GameObject 上挂载地面掉落名称渲染器
            _groundItemLabelRenderer = gameObject.GetComponent<GroundItemLabelRenderer>();
            if (_groundItemLabelRenderer == null)
                _groundItemLabelRenderer = gameObject.AddComponent<GroundItemLabelRenderer>();

            // 在同一 GameObject 上挂载技能运行时范围渲染器
            _skillRuntimeRenderer = gameObject.GetComponent<SkillRuntimeRenderer>();

            if (_skillRuntimeRenderer == null)
                _skillRuntimeRenderer = gameObject.AddComponent<SkillRuntimeRenderer>();

            RecalculateOffset();

        }

        /// <summary>
        /// 由 GameSceneManager 设置跟随目标（Unity Transform）
        /// </summary>
        public void SetTarget(Transform target)
        {
            _target = target;
            RecalculateOffset();
        }

        /// <summary>
        /// 由 GameSceneManager 设置跟随的ECS玩家实体
        /// </summary>
        public void SetPlayerEntity(Entity entity)
        {
            _playerEntity    = entity;
            _playerTransform = entity?.GetComponent<TransformComponent>();
            RecalculateOffset();

            // 同步给角色 Mesh 渲染器
            if (_meshRenderer != null)
                _meshRenderer.SetPlayerEntity(entity);

            // 有真实角色 Mesh 时禁用箭头标记渲染器，避免全屏 Quad 遮挡角色
            bool hasMesh = _meshRenderer != null && _meshRenderer.HasMesh;
            if (_markerRenderer != null)
            {
                _markerRenderer.enabled = !hasMesh;
                if (!hasMesh)
                    _markerRenderer.SetPlayerEntity(entity);
            }

            // NpcMarkerRenderer 已由 NpcMeshRenderer 的头顶标签取代，禁用圆形标记点
            if (_npcMarkerRenderer != null)
                _npcMarkerRenderer.enabled = false;

            // 同步ECS世界给NPC Mesh渲染器（同时负责名称标签和点击检测）
            if (_npcMeshRenderer != null && Managers.GameManager.Instance != null)
                _npcMeshRenderer.SetWorld(Managers.GameManager.Instance.World);

            // 同步ECS世界给怪物 Mesh 渲染器
            if (_monsterMeshRenderer != null && Managers.GameManager.Instance != null)
            {
                _monsterMeshRenderer.SetWorld(Managers.GameManager.Instance.World);
                // 注入 MovementSystem，启用 GPU 位置直读（零 CPU 中转，消除每帧10000次随机堆访问）
                _monsterMeshRenderer.SetMovementSystem(Managers.GameManager.Instance.MovementSystem);
            }

            // 同步 ECS 世界给地面掉落名称渲染器
            if (_groundItemLabelRenderer != null && Managers.GameManager.Instance != null)
                _groundItemLabelRenderer.SetWorld(Managers.GameManager.Instance.World);
        }

        // ── 每帧更新 ──────────────────────────────────────────────────

        private void LateUpdate()
        {
            Vector3 targetPos;

            // 优先使用ECS实体的逻辑位置
            if (_playerEntity != null && _playerEntity.IsAlive && _playerTransform != null)
            {
                targetPos = _playerTransform.Position;
            }
            else if (_target != null)
            {
                targetPos = _target.position;
            }
            else
            {
                return;
            }

            // 目标位置 + 偏移
            Vector3 desiredPos = targetPos + _offset;

            // 平滑插值位置，旋转保持固定（不跟随目标旋转）
            transform.position = Vector3.Lerp(transform.position, desiredPos, _smoothSpeed * Time.deltaTime);
            transform.rotation = _fixedRotation;
        }

        // ── 内部辅助 ──────────────────────────────────────────────────

        /// <summary>
        /// 根据角度和距离重新计算偏移向量
        /// 偏移方向 = 摄像机朝向的反方向，确保角色始终在屏幕中心
        /// </summary>
        private void RecalculateOffset()
        {
            // 固定旋转：绕X轴俯视角 + 绕Y轴水平旋转
            _fixedRotation = Quaternion.Euler(_angle, _rotationY, 0f);

            // 偏移 = 摄像机朝向的反方向 × 距离
            _offset = _fixedRotation * Vector3.back * _distance;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            RecalculateOffset();
        }
#endif
    }
}
