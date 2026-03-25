using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace POELike.Game.UI
{
    /// <summary>
    /// 游戏内 UI 面板管理器（静态单例）。
    /// 所有继承 <see cref="UIGamePanel"/> 的面板在 Open/Close 时自动注册/注销。
    /// 外部系统（如 GameSceneManager）通过 <see cref="IsPointerOverAnyPanel"/> 
    /// 判断鼠标是否悬停在任意已打开的面板上，无需硬编码具体面板引用。
    /// </summary>
    public static class UIGamePanelManager
    {
        private static readonly List<UIGamePanel> _openPanels = new List<UIGamePanel>();

        // ── 注册 / 注销（由 UIGamePanel 内部调用）────────────────────

        internal static void Register(UIGamePanel panel)
        {
            if (panel != null && !_openPanels.Contains(panel))
                _openPanels.Add(panel);
        }

        internal static void Unregister(UIGamePanel panel)
        {
            _openPanels.Remove(panel);
        }

        // ── 公开查询 ──────────────────────────────────────────────────

        /// <summary>
        /// 判断给定屏幕坐标是否落在任意已打开面板的矩形范围内。
        /// 精确命中检测，不依赖 EventSystem.IsPointerOverGameObject()。
        /// </summary>
        public static bool IsPointerOverAnyPanel(Vector2 screenPoint)
        {
            for (int i = _openPanels.Count - 1; i >= 0; i--)
            {
                var panel = _openPanels[i];
                // 清理已销毁的引用
                if (panel == null)
                {
                    _openPanels.RemoveAt(i);
                    continue;
                }
                if (panel.ContainsScreenPoint(screenPoint))
                    return true;
            }
            return false;
        }

        /// <summary>当前已打开的面板数量</summary>
        public static int OpenCount => _openPanels.Count;

        /// <summary>是否有任意面板处于打开状态</summary>
        public static bool AnyOpen => _openPanels.Count > 0;

        /// <summary>关闭所有已打开的面板</summary>
        public static void CloseAll()
        {
            // 倒序遍历，因为 Close() 会触发 Unregister 修改列表
            for (int i = _openPanels.Count - 1; i >= 0; i--)
            {
                var panel = _openPanels[i];
                if (panel != null)
                    panel.Close();
            }
            _openPanels.Clear();
        }
    }
}
