using UnityEngine;
using UnityEngine.Rendering;
using POELike.ECS.Core;
using POELike.ECS.Components;
using POELike.Game.UI;

namespace POELike.Game
{
    /// <summary>
    /// 玩家位置标记渲染器
    /// 挂载在摄像机 GameObject 上，不创建任何额外 GameObject。
    /// 使用 RenderPipelineManager.endCameraRendering + GL 在屏幕上绘制玩家标记圆圈。
    /// 兼容 URP / 内置管线。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class PlayerMarkerRenderer : MonoBehaviour
    {
        // ── ECS 引用 ──────────────────────────────────────────────────
        private Entity             _playerEntity;
        private TransformComponent _playerTransform;
        private MovementComponent  _playerMovement;

        // ── 箭头旋转平滑 ──────────────────────────────────────────────
        private float _currentAngleDeg = 0f;   // 当前平滑后的角度（度）
        private const float RotateSmoothSpeed = 720f; // 度/秒，旋转平滑速度

        // ── 渲染资源 ──────────────────────────────────────────────────
        private Material _markerMaterial;
        private Camera   _camera;

        // ── 可调参数 ──────────────────────────────────────────────────
        [Header("标记外观")]
        [SerializeField] private Color _markerColor = new Color(0.2f, 0.8f, 1.0f, 1.0f);
        [SerializeField] private Color _rimColor    = new Color(1.0f, 1.0f, 1.0f, 0.6f);
        [SerializeField] private float _radius      = 18f;
        [SerializeField] private float _lineWidth   = 3f;

        // ── Shader 属性 ID ────────────────────────────────────────────
        private static readonly int PropColor           = Shader.PropertyToID("_Color");
        private static readonly int PropRimColor        = Shader.PropertyToID("_RimColor");
        private static readonly int PropRadius          = Shader.PropertyToID("_Radius");
        private static readonly int PropLineWidth       = Shader.PropertyToID("_LineWidth");
        private static readonly int PropPlayerScreenPos = Shader.PropertyToID("_PlayerScreenPos");
        private static readonly int PropScreenSize      = Shader.PropertyToID("_MarkerScreenSize");
        private static readonly int PropForwardAngle    = Shader.PropertyToID("_ForwardAngle");
        private static readonly int PropTime2           = Shader.PropertyToID("_Time2");

        // ── 初始化 ────────────────────────────────────────────────────

        private void Awake()
        {
            _camera = GetComponent<Camera>();

            // 优先从 Resources 文件夹加载（最可靠）
            var shader = Resources.Load<Shader>("PlayerMarker");
            if (shader == null)
                shader = Shader.Find("POELike/PlayerMarker");

            if (shader == null)
            {
                Debug.LogError("[PlayerMarkerRenderer] 找不到 Shader！请确认 Assets/Resources/PlayerMarker.shader 存在");
                enabled = false;
                return;
            }

            _markerMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        private void OnEnable()
        {
            // URP 下使用 RenderPipelineManager 事件；内置管线下此事件同样会触发
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
        }

        private void OnDestroy()
        {
            if (_markerMaterial != null)
                Destroy(_markerMaterial);
        }

        // ── 公开接口 ──────────────────────────────────────────────────

        public void SetPlayerEntity(Entity entity)
        {
            _playerEntity    = entity;
            _playerTransform = entity?.GetComponent<TransformComponent>();
            _playerMovement  = entity?.GetComponent<MovementComponent>();
            Debug.Log($"[PlayerMarkerRenderer] SetPlayerEntity: entity={entity}, transform={_playerTransform}");
        }

        // ── 渲染回调 ──────────────────────────────────────────────────

        /// <summary>
        /// URP 兼容的摄像机渲染结束回调，只处理本组件所在摄像机
        /// </summary>
        private void OnEndCameraRendering(ScriptableRenderContext context, Camera cam)
        {
            // 只处理本摄像机
            if (cam != _camera) return;

            DrawMarker();
        }

        /// <summary>
        /// 内置管线兼容（如果不是 URP 则 OnPostRender 仍然有效）
        /// </summary>
        private void OnPostRender()
        {
            // URP 下此方法不会被调用，内置管线下作为备用
            DrawMarker();
        }

        // ── 绘制逻辑 ──────────────────────────────────────────────────

        private void DrawMarker()
        {
            if (_markerMaterial == null) return;
            if (_playerEntity == null || !_playerEntity.IsAlive) return;
            if (_playerTransform == null) return;

            // ── 世界坐标 → 视口坐标 ──────────────────────────────────
            Vector3 worldPos  = _playerTransform.Position;
            Vector3 screenPos = _camera.WorldToViewportPoint(worldPos);

            // 玩家在摄像机背后时不绘制
            if (screenPos.z < 0f) return;

            Rect markerRect = GetMarkerScreenRect(screenPos);
            if (UIGamePanelManager.IsScreenRectOverAnyPanel(markerRect))
                return;

            // ── 计算 Forward 在屏幕上的角度 ──────────────────────────
            // 将玩家 forward 方向投影到屏幕，计算相对屏幕 +Y 轴的顺时针角度
            float forwardAngle = 0f;

            // 优先用 UnityTransform.forward，纯逻辑实体则用 MoveDirection
            Vector3 worldForwardXZ = Vector3.zero;
            if (_playerTransform.UnityTransform != null)
            {
                var fwd = _playerTransform.UnityTransform.forward;
                worldForwardXZ = new Vector3(fwd.x, 0f, fwd.z);
            }
            else if (_playerMovement != null && _playerMovement.MoveDirection.sqrMagnitude > 0.0001f)
            {
                var dir = _playerMovement.MoveDirection;
                worldForwardXZ = new Vector3(dir.x, 0f, dir.z);
            }

            if (worldForwardXZ.sqrMagnitude > 0.0001f)
            {
                Vector3 worldFwd  = worldPos + worldForwardXZ.normalized;
                Vector3 screenFwd = _camera.WorldToViewportPoint(worldFwd);
                Vector2 screenDir = new Vector2(screenFwd.x - screenPos.x, screenFwd.y - screenPos.y);
                if (screenDir.sqrMagnitude > 0.0001f)
                {
                    // atan2(x, y)：从 +Y 轴顺时针的角度（转为度数用于平滑插值）
                    float targetAngleDeg = Mathf.Atan2(screenDir.x, screenDir.y) * Mathf.Rad2Deg;
                    // 平滑旋转，避免角度突变
                    _currentAngleDeg = Mathf.MoveTowardsAngle(
                        _currentAngleDeg,
                        targetAngleDeg,
                        RotateSmoothSpeed * Time.deltaTime
                    );
                }
            }
            forwardAngle = _currentAngleDeg * Mathf.Deg2Rad;

            // ── 更新 Shader 参数 ──────────────────────────────────────
            _markerMaterial.SetColor(PropColor,            _markerColor);
            _markerMaterial.SetColor(PropRimColor,         _rimColor);
            _markerMaterial.SetFloat(PropRadius,           _radius);
            _markerMaterial.SetFloat(PropLineWidth,        _lineWidth);
            _markerMaterial.SetVector(PropPlayerScreenPos, new Vector4(screenPos.x, screenPos.y, 0f, 0f));
            _markerMaterial.SetVector(PropScreenSize,      new Vector4(Screen.width, Screen.height, 0f, 0f));
            _markerMaterial.SetFloat(PropForwardAngle,     forwardAngle);
            _markerMaterial.SetFloat(PropTime2,            Time.time);

            // ── GL 绘制全屏 Quad ──────────────────────────────────────
            GL.PushMatrix();
            GL.LoadOrtho();

            _markerMaterial.SetPass(0);

            GL.Begin(GL.QUADS);
            GL.Vertex3(0f, 0f, 0f);
            GL.Vertex3(1f, 0f, 0f);
            GL.Vertex3(1f, 1f, 0f);
            GL.Vertex3(0f, 1f, 0f);
            GL.End();

            GL.PopMatrix();
        }

        private Rect GetMarkerScreenRect(Vector3 viewportPos)
        {
            float centerX = viewportPos.x * Screen.width;
            float centerY = viewportPos.y * Screen.height;
            float extent = _radius + _lineWidth + 4f;
            return Rect.MinMaxRect(
                centerX - extent,
                centerY - extent,
                centerX + extent,
                centerY + extent);
        }
    }
}
