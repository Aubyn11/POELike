using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace POELike.Game.UI
{
    /// <summary>
    /// 游戏内 UI 面板基类。
    /// 提供统一的 Open/Close 生命周期、IsOpen 状态、OnClose 回调，
    /// 以及 <see cref="ContainsScreenPoint"/> 精确命中检测（避免全屏 Canvas 误拦截）。
    /// 子类重写 <see cref="OnOpen"/> / <see cref="OnClose_Internal"/> 实现具体逻辑。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public abstract class UIGamePanel : MonoBehaviour
    {
        // ── 状态 ──────────────────────────────────────────────────────
        /// <summary>面板当前是否处于打开状态</summary>
        public bool IsOpen { get; private set; }

        // ── 外部回调 ──────────────────────────────────────────────────
        /// <summary>面板关闭时触发</summary>
        public Action OnClose;

        // ── 缓存 ──────────────────────────────────────────────────────
        private RectTransform _rectTransform;

        protected virtual void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
        }

        // ── 公开接口 ──────────────────────────────────────────────────

        /// <summary>打开面板（激活 GameObject，触发 OnOpen）</summary>
        public void Open()
        {
            gameObject.SetActive(true);
            IsOpen = true;
            UIGamePanelManager.Register(this);
            OnOpen();
        }

        /// <summary>关闭面板（停用 GameObject，触发 OnClose 回调）</summary>
        public void Close()
        {
            IsOpen = false;
            gameObject.SetActive(false);
            UIGamePanelManager.Unregister(this);
            OnClose_Internal();
            OnClose?.Invoke();
        }

        /// <summary>
        /// 判断屏幕坐标是否落在本面板的 RectTransform 矩形内。
        /// 用于替代 EventSystem.IsPointerOverGameObject()，避免全屏 Canvas 误拦截。
        /// </summary>
        public bool ContainsScreenPoint(Vector2 screenPoint)
        {
            if (_rectTransform == null) return false;
            return RectTransformUtility.RectangleContainsScreenPoint(_rectTransform, screenPoint, null);
        }

        // ── 子类钩子 ──────────────────────────────────────────────────

        /// <summary>面板打开时的子类逻辑（在 GameObject 激活后调用）</summary>
        protected virtual void OnOpen() { }

        /// <summary>面板关闭时的子类逻辑（在 GameObject 停用前调用）</summary>
        protected virtual void OnClose_Internal() { }
    }
}
