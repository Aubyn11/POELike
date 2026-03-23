using System;
using System.Collections.Generic;
using UnityEngine;

namespace POELike.Game.UI
{
    /// <summary>
    /// NPC对话框中的按钮选项
    /// </summary>
    [Serializable]
    public class NpcDialogOption
    {
        public string Label;
        public Action OnClick;

        public NpcDialogOption(string label, Action onClick)
        {
            Label   = label;
            OnClick = onClick;
        }
    }

    /// <summary>
    /// NPC对话框面板（纯OnGUI实现）
    /// 左侧：对话内容文本
    /// 右侧：多条可点击的TextButton选项
    /// 点击对话框外部时关闭
    /// </summary>
    public class NpcDialogPanel : MonoBehaviour
    {
        // ── 外部回调 ──────────────────────────────────────────────────
        /// <summary>点击对话框外部时触发（用于关闭并恢复寻路）</summary>
        public Action OnClickOutside;

        // ── 状态 ──────────────────────────────────────────────────────
        private bool   _isOpen;
        private string _npcName    = "";
        private string _dialogText = "";
        private List<NpcDialogOption> _options = new List<NpcDialogOption>();

        // ── 样式缓存 ──────────────────────────────────────────────────
        private GUIStyle _panelStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _contentStyle;
        private GUIStyle _dividerStyle;
        private GUIStyle _optionBtnStyle;
        private GUIStyle _optionHoverStyle;
        private bool     _stylesInitialized;

        // ── 布局常量 ──────────────────────────────────────────────────
        private const float PanelW      = 640f;
        private const float PanelH      = 320f;
        private const float LeftRatio   = 0.58f;   // 左侧占比
        private const float Padding     = 18f;
        private const float TitleH      = 36f;
        private const float OptionH     = 38f;
        private const float OptionGap   = 6f;

        // ── 公开接口 ──────────────────────────────────────────────────

        /// <summary>
        /// 打开对话框
        /// </summary>
        public void Open(string npcName, string dialogText, List<NpcDialogOption> options)
        {
            _npcName    = npcName    ?? "";
            _dialogText = dialogText ?? "";
            _options    = options    ?? new List<NpcDialogOption>();
            _isOpen     = true;
        }

        /// <summary>
        /// 关闭对话框
        /// </summary>
        public void Close()
        {
            _isOpen = false;
        }

        public bool IsOpen => _isOpen;

        // ── Unity 生命周期 ────────────────────────────────────────────

        private void OnGUI()
        {
            if (!_isOpen) return;

            // depth 越小越靠前，NpcMarkerRenderer 默认 depth=0，对话框设为 -1 确保覆盖在 NPC 标记上方
            GUI.depth = -1;

            InitStyles();

            // 面板居中于屏幕下方 1/3 处
            float panelX = (Screen.width  - PanelW) * 0.5f;
            float panelY = Screen.height  * 0.55f;
            Rect  panelRect = new Rect(panelX, panelY, PanelW, PanelH);

            // ── 点击外部检测 ──────────────────────────────────────────
            if (Event.current.type == EventType.MouseDown)
            {
                if (!panelRect.Contains(Event.current.mousePosition))
                {
                    _isOpen = false;
                    OnClickOutside?.Invoke();
                    Event.current.Use();
                    return;
                }
            }

            // ── 背景面板 ──────────────────────────────────────────────
            GUI.Box(panelRect, GUIContent.none, _panelStyle);

            float leftW  = PanelW * LeftRatio;
            float rightW = PanelW - leftW;

            // ── 左侧：NPC名称 + 对话内容 ──────────────────────────────
            Rect leftRect = new Rect(panelX, panelY, leftW, PanelH);
            DrawLeftPanel(leftRect);

            // ── 分割线 ────────────────────────────────────────────────
            Rect divRect = new Rect(panelX + leftW, panelY + Padding, 1f, PanelH - Padding * 2f);
            GUI.Box(divRect, GUIContent.none, _dividerStyle);

            // ── 右侧：选项按钮 ────────────────────────────────────────
            Rect rightRect = new Rect(panelX + leftW + 1f, panelY, rightW - 1f, PanelH);
            DrawRightPanel(rightRect);
        }

        // ── 私有绘制方法 ──────────────────────────────────────────────

        private void DrawLeftPanel(Rect area)
        {
            // NPC 名称标题
            Rect titleRect = new Rect(
                area.x + Padding,
                area.y + Padding,
                area.width - Padding * 2f,
                TitleH);
            GUI.Label(titleRect, _npcName, _titleStyle);

            // 分割线（标题下方）
            Rect titleDivRect = new Rect(area.x + Padding, area.y + Padding + TitleH + 4f,
                area.width - Padding * 2f, 1f);
            GUI.Box(titleDivRect, GUIContent.none, _dividerStyle);

            // 对话内容
            Rect contentRect = new Rect(
                area.x + Padding,
                area.y + Padding + TitleH + 12f,
                area.width - Padding * 2f,
                area.height - Padding * 2f - TitleH - 12f);
            GUI.Label(contentRect, _dialogText, _contentStyle);
        }

        private void DrawRightPanel(Rect area)
        {
            if (_options == null || _options.Count == 0)
            {
                // 无选项时显示提示
                Rect emptyRect = new Rect(
                    area.x + Padding,
                    area.y + PanelH * 0.5f - 12f,
                    area.width - Padding * 2f,
                    24f);
                GUI.Label(emptyRect, "（暂无选项）", _contentStyle);
                return;
            }

            // 选项列表从顶部开始排列
            float startY = area.y + Padding;
            for (int i = 0; i < _options.Count; i++)
            {
                var opt = _options[i];
                Rect btnRect = new Rect(
                    area.x + Padding,
                    startY + i * (OptionH + OptionGap),
                    area.width - Padding * 2f,
                    OptionH);

                // 悬停高亮
                bool hovered = btnRect.Contains(Event.current.mousePosition);
                GUIStyle style = hovered ? _optionHoverStyle : _optionBtnStyle;

                if (GUI.Button(btnRect, opt.Label, style))
                {
                    opt.OnClick?.Invoke();
                }
            }
        }

        // ── 样式初始化 ────────────────────────────────────────────────

        private void InitStyles()
        {
            if (_stylesInitialized) return;
            _stylesInitialized = true;

            // 面板背景（深色半透明）
            _panelStyle = new GUIStyle(GUI.skin.box);
            _panelStyle.normal.background = MakeTex(2, 2, new Color(0.08f, 0.07f, 0.06f, 0.93f));
            _panelStyle.border = new RectOffset(4, 4, 4, 4);

            // NPC名称标题
            _titleStyle = new GUIStyle(GUI.skin.label);
            _titleStyle.fontSize  = 18;
            _titleStyle.fontStyle = FontStyle.Bold;
            _titleStyle.normal.textColor = new Color(0.95f, 0.80f, 0.30f, 1f); // 金色
            _titleStyle.alignment = TextAnchor.MiddleLeft;

            // 对话内容
            _contentStyle = new GUIStyle(GUI.skin.label);
            _contentStyle.fontSize  = 14;
            _contentStyle.wordWrap  = true;
            _contentStyle.normal.textColor = new Color(0.88f, 0.85f, 0.80f, 1f);
            _contentStyle.alignment = TextAnchor.UpperLeft;

            // 分割线
            _dividerStyle = new GUIStyle(GUI.skin.box);
            _dividerStyle.normal.background = MakeTex(2, 2, new Color(0.5f, 0.45f, 0.35f, 0.6f));
            _dividerStyle.border = new RectOffset(0, 0, 0, 0);

            // 选项按钮（普通）
            _optionBtnStyle = new GUIStyle(GUI.skin.button);
            _optionBtnStyle.fontSize  = 14;
            _optionBtnStyle.fontStyle = FontStyle.Normal;
            _optionBtnStyle.alignment = TextAnchor.MiddleLeft;
            _optionBtnStyle.padding   = new RectOffset(12, 8, 0, 0);
            _optionBtnStyle.normal.background   = MakeTex(2, 2, new Color(0.15f, 0.13f, 0.10f, 0.85f));
            _optionBtnStyle.hover.background    = MakeTex(2, 2, new Color(0.28f, 0.24f, 0.16f, 0.95f));
            _optionBtnStyle.active.background   = MakeTex(2, 2, new Color(0.35f, 0.30f, 0.18f, 1.00f));
            _optionBtnStyle.normal.textColor    = new Color(0.85f, 0.82f, 0.75f, 1f);
            _optionBtnStyle.hover.textColor     = new Color(1.00f, 0.92f, 0.60f, 1f);
            _optionBtnStyle.active.textColor    = new Color(1.00f, 1.00f, 0.80f, 1f);
            _optionBtnStyle.border = new RectOffset(2, 2, 2, 2);

            // 选项按钮（悬停，与 hover 一致，用于手动判断）
            _optionHoverStyle = new GUIStyle(_optionBtnStyle);
        }

        private static Texture2D MakeTex(int w, int h, Color col)
        {
            var tex = new Texture2D(w, h);
            var pixels = new Color[w * h];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = col;
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
    }
}
