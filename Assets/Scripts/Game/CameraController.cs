using UnityEngine;

namespace POELike.Game
{
    /// <summary>
    /// 等距跟随摄像机控制器
    /// 类似流放之路的俯视角跟随摄像机
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("跟随目标")]
        [SerializeField] private Transform _target;

        [Header("摄像机参数")]
        [SerializeField] private float  _height      = 20f;   // 摄像机高度
        [SerializeField] private float  _distance    = 15f;   // 水平距离
        [SerializeField] private float  _angle       = 55f;   // 俯视角度
        [SerializeField] private float  _smoothSpeed = 8f;    // 跟随平滑速度
        [SerializeField] private float  _rotationY   = 0f;    // 水平旋转角（0=正北）

        // 摄像机引用（可能是外部传入的 Camera.main）
        private Camera _camera;

        // 目标偏移（等距视角的固定偏移）
        private Vector3 _offset;

        // ── 初始化 ────────────────────────────────────────────────────

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            RecalculateOffset();
        }

        /// <summary>
        /// 由 GameSceneManager 设置跟随目标
        /// </summary>
        public void SetTarget(Transform target)
        {
            _target = target;
            RecalculateOffset();
        }

        /// <summary>
        /// 由 GameSceneManager 将已有的 Camera 组件关联到此控制器
        /// </summary>
        public void AssignCamera(Camera cam)
        {
            _camera = cam;
            cam.transform.SetParent(transform);
            cam.transform.localPosition = Vector3.zero;
            cam.transform.localRotation = Quaternion.identity;
            RecalculateOffset();
        }

        // ── 每帧更新 ──────────────────────────────────────────────────

        private void LateUpdate()
        {
            if (_target == null) return;

            // 目标位置 + 偏移
            Vector3 desiredPos = _target.position + _offset;

            // 平滑插值
            transform.position = Vector3.Lerp(transform.position, desiredPos, _smoothSpeed * Time.deltaTime);

            // 始终朝向目标
            transform.LookAt(_target.position + Vector3.up * 1f);
        }

        // ── 内部辅助 ──────────────────────────────────────────────────

        /// <summary>
        /// 根据角度和距离重新计算偏移向量
        /// </summary>
        private void RecalculateOffset()
        {
            float rad = _angle * Mathf.Deg2Rad;
            float rotRad = _rotationY * Mathf.Deg2Rad;

            float horizontal = _distance * Mathf.Cos(rad);
            float vertical   = _distance * Mathf.Sin(rad) + _height * 0.5f;

            _offset = new Vector3(
                horizontal * Mathf.Sin(rotRad),
                vertical,
                -horizontal * Mathf.Cos(rotRad)
            );
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            RecalculateOffset();
        }
#endif
    }
}
