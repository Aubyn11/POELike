using System.Collections.Generic;
using UnityEngine;

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
        private static readonly List<RectTransform> _extraOccluderRects = new List<RectTransform>();
        private static readonly Vector3[] _worldCorners = new Vector3[4];

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

        public static void RegisterOccluder(RectTransform rectTransform)
        {
            if (rectTransform != null && !_extraOccluderRects.Contains(rectTransform))
                _extraOccluderRects.Add(rectTransform);
        }

        public static void UnregisterOccluder(RectTransform rectTransform)
        {
            _extraOccluderRects.Remove(rectTransform);
        }

        // ── 公开查询 ──────────────────────────────────────────────────

        /// <summary>
        /// 判断给定屏幕坐标是否落在任意已打开 UI 的可见范围内。
        /// 既包含继承 <see cref="UIGamePanel"/> 的面板，也包含额外注册的 UGUI 遮挡区域与 IMGUI 窗口。
        /// </summary>
        public static bool IsPointerOverAnyPanel(Vector2 screenPoint)
        {
            if (IsPointOverRegisteredPanels(screenPoint))
                return true;

            if (IsPointOverExtraOccluders(screenPoint))
                return true;

            if (GMPanel.TryGetVisibleScreenRect(out var gmPanelRect)
                && gmPanelRect.Contains(screenPoint))
            {
                return true;
            }

            return ClientSkillExtensionPanel.TryGetVisibleScreenRect(out var clientSkillPanelRect)
                && clientSkillPanelRect.Contains(screenPoint);

        }

        /// <summary>
        /// 判断给定屏幕矩形是否与任意已打开 UI 的遮挡范围发生重叠。
        /// 传入的 <paramref name="screenRect"/> 使用屏幕坐标系（左下为原点）。
        /// </summary>
        public static bool IsScreenRectOverAnyPanel(Rect screenRect)
        {
            if (screenRect.width <= 0f || screenRect.height <= 0f)
                return false;

            if (IsScreenRectOverRegisteredPanels(screenRect))
                return true;

            if (IsScreenRectOverExtraOccluders(screenRect))
                return true;

            if (GMPanel.TryGetVisibleScreenRect(out var gmPanelRect)
                && gmPanelRect.Overlaps(screenRect, true))
            {
                return true;
            }

            return ClientSkillExtensionPanel.TryGetVisibleScreenRect(out var clientSkillPanelRect)
                && clientSkillPanelRect.Overlaps(screenRect, true);

        }

        /// <summary>
        /// 将 IMGUI 的 GUI 坐标矩形（左上为原点）转换为屏幕坐标矩形（左下为原点）。
        /// </summary>
        public static Rect GuiRectToScreenRect(Rect guiRect)
        {
            return new Rect(
                guiRect.xMin,
                Screen.height - guiRect.yMax,
                guiRect.width,
                guiRect.height);
        }

        /// <summary>
        /// 收集当前所有会遮挡游戏内绘制与点击的屏幕矩形（左下为原点）。
        /// </summary>
        public static void GetScreenOccluderRects(List<Rect> results)
        {
            if (results == null)
                return;

            results.Clear();

            AppendRegisteredPanelRects(results);
            AppendExtraOccluderRects(results);

            if (GMPanel.TryGetVisibleScreenRect(out var gmPanelRect))
                results.Add(gmPanelRect);

            if (ClientSkillExtensionPanel.TryGetVisibleScreenRect(out var clientSkillPanelRect))
                results.Add(clientSkillPanelRect);
        }

        /// <summary>
        /// 将屏幕坐标矩形（左下为原点）转换为 IMGUI 的 GUI 坐标矩形（左上为原点）。
        /// </summary>
        public static Rect ScreenRectToGuiRect(Rect screenRect)
        {
            return new Rect(
                screenRect.xMin,
                Screen.height - screenRect.yMax,
                screenRect.width,
                screenRect.height);
        }

        private static void AppendRegisteredPanelRects(List<Rect> results)
        {
            for (int i = _openPanels.Count - 1; i >= 0; i--)
            {
                var panel = _openPanels[i];
                if (panel == null)
                {
                    _openPanels.RemoveAt(i);
                    continue;
                }

                if (TryGetScreenRect(panel.transform as RectTransform, out var panelRect))
                    results.Add(panelRect);
            }
        }

        private static void AppendExtraOccluderRects(List<Rect> results)
        {
            for (int i = _extraOccluderRects.Count - 1; i >= 0; i--)
            {
                var rectTransform = _extraOccluderRects[i];
                if (rectTransform == null || !rectTransform.gameObject.activeInHierarchy)
                {
                    _extraOccluderRects.RemoveAt(i);
                    continue;
                }

                if (TryGetScreenRect(rectTransform, out var occluderRect))
                    results.Add(occluderRect);
            }
        }

        private static bool IsPointOverRegisteredPanels(Vector2 screenPoint)

        {
            for (int i = _openPanels.Count - 1; i >= 0; i--)
            {
                var panel = _openPanels[i];
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

        private static bool IsPointOverExtraOccluders(Vector2 screenPoint)
        {
            for (int i = _extraOccluderRects.Count - 1; i >= 0; i--)
            {
                var rectTransform = _extraOccluderRects[i];
                if (rectTransform == null || !rectTransform.gameObject.activeInHierarchy)
                {
                    _extraOccluderRects.RemoveAt(i);
                    continue;
                }

                if (RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPoint, null))
                    return true;
            }

            return false;
        }

        private static bool IsScreenRectOverRegisteredPanels(Rect screenRect)
        {
            for (int i = _openPanels.Count - 1; i >= 0; i--)
            {
                var panel = _openPanels[i];
                if (panel == null)
                {
                    _openPanels.RemoveAt(i);
                    continue;
                }

                if (TryGetScreenRect(panel.transform as RectTransform, out var panelRect)
                    && panelRect.Overlaps(screenRect, true))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsScreenRectOverExtraOccluders(Rect screenRect)
        {
            for (int i = _extraOccluderRects.Count - 1; i >= 0; i--)
            {
                var rectTransform = _extraOccluderRects[i];
                if (rectTransform == null || !rectTransform.gameObject.activeInHierarchy)
                {
                    _extraOccluderRects.RemoveAt(i);
                    continue;
                }

                if (TryGetScreenRect(rectTransform, out var occluderRect)
                    && occluderRect.Overlaps(screenRect, true))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetScreenRect(RectTransform rectTransform, out Rect screenRect)
        {
            screenRect = default;

            if (rectTransform == null || !rectTransform.gameObject.activeInHierarchy)
                return false;

            rectTransform.GetWorldCorners(_worldCorners);

            float xMin = float.MaxValue;
            float yMin = float.MaxValue;
            float xMax = float.MinValue;
            float yMax = float.MinValue;

            for (int i = 0; i < _worldCorners.Length; i++)
            {
                Vector2 corner = RectTransformUtility.WorldToScreenPoint(null, _worldCorners[i]);
                xMin = Mathf.Min(xMin, corner.x);
                yMin = Mathf.Min(yMin, corner.y);
                xMax = Mathf.Max(xMax, corner.x);
                yMax = Mathf.Max(yMax, corner.y);
            }

            screenRect = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
            return true;
        }

        /// <summary>当前已打开的面板数量</summary>
        public static int OpenCount => _openPanels.Count;

        /// <summary>是否有任意面板处于打开状态</summary>
        public static bool AnyOpen => _openPanels.Count > 0;

        /// <summary>
        /// 获取当前最上层的已打开面板。
        /// 默认按打开顺序处理：最后注册的面板视为最上层面板。
        /// </summary>
        public static UIGamePanel GetTopmostOpenPanel()
        {
            for (int i = _openPanels.Count - 1; i >= 0; i--)
            {
                var panel = _openPanels[i];
                if (panel == null)
                {
                    _openPanels.RemoveAt(i);
                    continue;
                }

                if (!panel.IsOpen || !panel.gameObject.activeInHierarchy)
                {
                    _openPanels.RemoveAt(i);
                    continue;
                }

                return panel;
            }

            return null;
        }

        /// <summary>
        /// 关闭当前最上层的已打开面板。
        /// </summary>
        public static bool CloseTopmost()
        {
            var panel = GetTopmostOpenPanel();
            if (panel == null)
                return false;

            panel.Close();
            return true;
        }

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
