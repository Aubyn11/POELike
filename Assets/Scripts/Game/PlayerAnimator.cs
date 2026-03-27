using UnityEngine;

namespace POELike.Game
{
    /// <summary>
    /// 玩家程序化动画状态
    /// </summary>
    public enum PlayerAnimState
    {
        Idle,       // 待机（呼吸浮动）
        Walk,       // 移动（左右摇摆 + 前倾）
        Jump,       // 跳跃（抛物线）
        Attack,     // 攻击（前冲 + 回弹）
        Hit,        // 受击（后退抖动）
        Death,      // 死亡（倒地）
    }

    /// <summary>
    /// 玩家程序化动画控制器
    /// 不依赖 Animator/骨骼，通过对 Matrix4x4 施加位移/旋转/缩放偏移来模拟动作。
    /// 由 PlayerMeshRenderer 在每帧渲染前调用 <see cref="Evaluate"/> 获取当前动画偏移矩阵。
    /// </summary>
    public class PlayerAnimator : MonoBehaviour
    {
        // ── 可调参数 ──────────────────────────────────────────────────

        [Header("Idle 呼吸")]
        [Tooltip("呼吸上下浮动幅度（米）")]
        [SerializeField] private float _idleBobAmplitude  = 0.04f;
        [Tooltip("呼吸频率（次/秒）")]
        [SerializeField] private float _idleBobFrequency  = 1.2f;

        [Header("移动摇摆")]
        [Tooltip("移动时左右摇摆角度（度）")]
        [SerializeField] private float _walkSwayAngle     = 4f;
        [Tooltip("移动时前倾角度（度）")]
        [SerializeField] private float _walkLeanAngle     = 6f;
        [Tooltip("移动时上下弹跳幅度（米）")]
        [SerializeField] private float _walkBobAmplitude  = 0.05f;
        [Tooltip("移动弹跳频率（步频，次/秒）")]
        [SerializeField] private float _walkBobFrequency  = 3.5f;

        [Header("跳跃")]
        [Tooltip("跳跃高度（米）")]
        [SerializeField] private float _jumpHeight        = 1.2f;
        [Tooltip("跳跃持续时间（秒）")]
        [SerializeField] private float _jumpDuration      = 0.5f;
        [Tooltip("跳跃起跳时前倾角度（度）")]
        [SerializeField] private float _jumpLeanAngle     = 15f;

        [Header("攻击")]
        [Tooltip("攻击前冲距离（米）")]
        [SerializeField] private float _attackLungeDistance = 0.25f;
        [Tooltip("攻击前冲持续时间（秒）")]
        [SerializeField] private float _attackDuration    = 0.35f;
        [Tooltip("攻击时旋转角度（度，Z轴）")]
        [SerializeField] private float _attackTiltAngle   = 20f;

        [Header("受击")]
        [Tooltip("受击后退距离（米）")]
        [SerializeField] private float _hitKnockback      = 0.15f;
        [Tooltip("受击抖动持续时间（秒）")]
        [SerializeField] private float _hitDuration       = 0.25f;

        [Header("死亡")]
        [Tooltip("死亡倒地旋转角度（度，X轴）")]
        [SerializeField] private float _deathTiltAngle    = 90f;
        [Tooltip("死亡动画持续时间（秒）")]
        [SerializeField] private float _deathDuration     = 0.8f;

        // ── 状态 ──────────────────────────────────────────────────────

        private PlayerAnimState _state     = PlayerAnimState.Idle;
        private float           _stateTime = 0f;   // 当前状态已经过的时间
        private float           _globalTime = 0f;  // 全局时间（用于周期性动画）

        // 攻击/受击方向（世界空间，XZ平面）
        private Vector3 _actionDir = Vector3.forward;

        // ── 公开接口 ──────────────────────────────────────────────────

        /// <summary>当前动画状态</summary>
        public PlayerAnimState State => _state;

        /// <summary>
        /// 切换到指定状态。
        /// 若已处于该状态（非一次性动作），则忽略。
        /// </summary>
        public void SetState(PlayerAnimState newState, Vector3 actionDir = default)
        {
            // 死亡状态不可被覆盖
            if (_state == PlayerAnimState.Death) return;

            // 一次性动作（攻击/受击/跳跃/死亡）：只有在当前动作播完后才能被 Idle/Walk 覆盖
            bool currentIsOneShot = IsOneShot(_state);
            bool newIsOneShot     = IsOneShot(newState);

            if (currentIsOneShot && !newIsOneShot)
            {
                // 一次性动作未播完，不允许被 Idle/Walk 打断
                return;
            }

            if (_state == newState && !newIsOneShot) return;

            _state     = newState;
            _stateTime = 0f;

            if (actionDir != default && actionDir.sqrMagnitude > 0.001f)
                _actionDir = actionDir.normalized;
        }

        /// <summary>
        /// 每帧由 PlayerMeshRenderer 调用，返回当前帧的动画偏移矩阵（相对于角色根节点）。
        /// </summary>
        /// <param name="deltaTime">Time.deltaTime</param>
        /// <returns>TRS 偏移矩阵，叠加到角色世界矩阵上</returns>
        public Matrix4x4 Evaluate(float deltaTime)
        {
            _globalTime += deltaTime;
            _stateTime  += deltaTime;

            // 一次性动作播完后自动回到 Idle
            if (IsOneShot(_state) && _stateTime >= GetOneShotDuration(_state))
            {
                _state     = PlayerAnimState.Idle;
                _stateTime = 0f;
            }

            return _state switch
            {
                PlayerAnimState.Idle   => EvaluateIdle(),
                PlayerAnimState.Walk   => EvaluateWalk(),
                PlayerAnimState.Jump   => EvaluateJump(),
                PlayerAnimState.Attack => EvaluateAttack(),
                PlayerAnimState.Hit    => EvaluateHit(),
                PlayerAnimState.Death  => EvaluateDeath(),
                _                      => Matrix4x4.identity,
            };
        }

        // ── 各状态动画计算 ────────────────────────────────────────────

        /// <summary>Idle：上下呼吸浮动</summary>
        private Matrix4x4 EvaluateIdle()
        {
            float y = Mathf.Sin(_globalTime * _idleBobFrequency * Mathf.PI * 2f) * _idleBobAmplitude;
            return Matrix4x4.Translate(new Vector3(0f, y, 0f));
        }

        /// <summary>Walk：上下弹跳 + 左右摇摆 + 前倾</summary>
        private Matrix4x4 EvaluateWalk()
        {
            // 上下弹跳（绝对值，每步两次）
            float bobY = Mathf.Abs(Mathf.Sin(_globalTime * _walkBobFrequency * Mathf.PI)) * _walkBobAmplitude;

            // 左右摇摆（Z轴旋转）
            float swayZ = Mathf.Sin(_globalTime * _walkBobFrequency * Mathf.PI) * _walkSwayAngle;

            // 前倾（X轴旋转，固定值）
            float leanX = -_walkLeanAngle;

            Vector3    pos = new Vector3(0f, bobY, 0f);
            Quaternion rot = Quaternion.Euler(leanX, 0f, swayZ);
            return Matrix4x4.TRS(pos, rot, Vector3.one);
        }

        /// <summary>Jump：抛物线高度 + 起跳前倾</summary>
        private Matrix4x4 EvaluateJump()
        {
            float t = Mathf.Clamp01(_stateTime / _jumpDuration);

            // 抛物线：y = 4h * t * (1-t)
            float y = 4f * _jumpHeight * t * (1f - t);

            // 起跳前倾：前半段前倾，后半段回正
            float leanX = t < 0.5f
                ? Mathf.Lerp(0f, -_jumpLeanAngle, t * 2f)
                : Mathf.Lerp(-_jumpLeanAngle, 0f, (t - 0.5f) * 2f);

            Vector3    pos = new Vector3(0f, y, 0f);
            Quaternion rot = Quaternion.Euler(leanX, 0f, 0f);
            return Matrix4x4.TRS(pos, rot, Vector3.one);
        }

        /// <summary>Attack：前冲 + 回弹 + Z轴倾斜</summary>
        private Matrix4x4 EvaluateAttack()
        {
            float t = Mathf.Clamp01(_stateTime / _attackDuration);

            // 前冲曲线：快速前冲（0→0.3），缓慢回弹（0.3→1）
            float lungeT;
            if (t < 0.3f)
                lungeT = t / 0.3f;                      // 0→1
            else
                lungeT = 1f - (t - 0.3f) / 0.7f;       // 1→0

            // 使用 EaseOut 让前冲更有力
            lungeT = 1f - (1f - lungeT) * (1f - lungeT);

            Vector3 offset = _actionDir * (_attackLungeDistance * lungeT);

            // 攻击时 Z 轴倾斜（挥砍感）
            float tiltZ = Mathf.Sin(t * Mathf.PI) * _attackTiltAngle;

            Quaternion rot = Quaternion.Euler(0f, 0f, tiltZ);
            return Matrix4x4.TRS(offset, rot, Vector3.one);
        }

        /// <summary>Hit：后退抖动</summary>
        private Matrix4x4 EvaluateHit()
        {
            float t = Mathf.Clamp01(_stateTime / _hitDuration);

            // 后退衰减
            float knockback = _hitKnockback * (1f - t);
            Vector3 offset  = -_actionDir * knockback;

            // 抖动（高频震动）
            float shake = Mathf.Sin(t * Mathf.PI * 8f) * 0.03f * (1f - t);
            offset.x += shake;

            return Matrix4x4.Translate(offset);
        }

        /// <summary>Death：倒地（X轴旋转 + 缩放消失）</summary>
        private Matrix4x4 EvaluateDeath()
        {
            float t = Mathf.Clamp01(_stateTime / _deathDuration);

            // EaseIn 倒地
            float eased = t * t;
            float tiltX = Mathf.Lerp(0f, _deathTiltAngle, eased);

            // 后期淡出缩放（0.8→1 时间段内缩小到0）
            float scale = t > 0.8f ? Mathf.Lerp(1f, 0f, (t - 0.8f) / 0.2f) : 1f;

            Quaternion rot = Quaternion.Euler(tiltX, 0f, 0f);
            return Matrix4x4.TRS(Vector3.zero, rot, Vector3.one * scale);
        }

        // ── 辅助 ──────────────────────────────────────────────────────

        private static bool IsOneShot(PlayerAnimState state) =>
            state == PlayerAnimState.Jump   ||
            state == PlayerAnimState.Attack ||
            state == PlayerAnimState.Hit    ||
            state == PlayerAnimState.Death;

        private float GetOneShotDuration(PlayerAnimState state) => state switch
        {
            PlayerAnimState.Jump   => _jumpDuration,
            PlayerAnimState.Attack => _attackDuration,
            PlayerAnimState.Hit    => _hitDuration,
            PlayerAnimState.Death  => _deathDuration * 1.2f, // 死亡多留一点时间
            _                      => 0f,
        };
    }
}
