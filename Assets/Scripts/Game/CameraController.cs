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

        // NPC标记渲染器
        private NpcMarkerRenderer _npcMarkerRenderer;

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
            _camera.fieldOfView   = 60f;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane  = 500f;
            _camera.tag           = "MainCamera";

            // 在同一 GameObject 上挂载玩家标记渲染器
            _markerRenderer = gameObject.GetComponent<PlayerMarkerRenderer>();
            if (_markerRenderer == null)
                _markerRenderer = gameObject.AddComponent<PlayerMarkerRenderer>();

            // 在同一 GameObject 上挂载NPC标记渲染器
            _npcMarkerRenderer = gameObject.GetComponent<NpcMarkerRenderer>();
            if (_npcMarkerRenderer == null)
                _npcMarkerRenderer = gameObject.AddComponent<NpcMarkerRenderer>();

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

            // 同步给标记渲染器
            if (_markerRenderer != null)
                _markerRenderer.SetPlayerEntity(entity);

            // 同步ECS世界给NPC标记渲染器
            if (_npcMarkerRenderer != null && Managers.GameManager.Instance != null)
                _npcMarkerRenderer.SetWorld(Managers.GameManager.Instance.World);
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
