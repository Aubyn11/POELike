using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using POELike.Game.UI;

namespace POELike.Editor.UI
{
    /// <summary>
    /// 角色选择界面 Prefab 生成器
    /// 菜单：POELike → UI → 生成角色选择界面 Prefab
    /// </summary>
    public static class CharacterSelectPrefabBuilder
    {
        private const string PrefabSavePath      = "Assets/Prefabs/UI/CharacterSelectPanel.prefab";
        private const string SlotItemSavePath    = "Assets/Prefabs/UI/CharacterSlotItem.prefab";

        // ── 中文字体（构建时懒加载）────────────────────────────────────
        private static TMP_FontAsset _chineseFont;
        private static TMP_FontAsset ChineseFont
            => _chineseFont != null ? _chineseFont
                : (_chineseFont = ChineseFontCreator.GetOrCreateChineseFontAsset());

        // ── 颜色常量 ──────────────────────────────────────────────────
        private static readonly Color ColPanelBg      = new Color(0.08f, 0.06f, 0.04f, 0.97f);
        private static readonly Color ColSlotNormal   = new Color(0.12f, 0.09f, 0.06f, 0.90f);
        private static readonly Color ColSlotHighlight= new Color(0.98f, 0.78f, 0.25f, 0.25f);
        private static readonly Color ColBtnEnter     = new Color(0.80f, 0.55f, 0.10f, 1.00f);
        private static readonly Color ColBtnCreate    = new Color(0.20f, 0.55f, 0.20f, 1.00f);
        private static readonly Color ColBtnDelete    = new Color(0.55f, 0.15f, 0.15f, 1.00f);
        private static readonly Color ColTextGold     = new Color(0.98f, 0.85f, 0.50f, 1.00f);
        private static readonly Color ColTextWhite    = new Color(0.92f, 0.92f, 0.92f, 1.00f);
        private static readonly Color ColTextGray     = new Color(0.60f, 0.60f, 0.60f, 1.00f);
        private static readonly Color ColDivider      = new Color(0.50f, 0.40f, 0.20f, 0.60f);
        private static readonly Color ColScrollbar    = new Color(0.50f, 0.40f, 0.20f, 0.50f);

        // ─────────────────────────────────────────────────────────────
        [MenuItem("POELike/UI/生成角色选择界面 Prefab")]
        public static void BuildAll()
        {
            EnsureDirectory("Assets/Prefabs/UI");

            var slotItemPrefab = BuildSlotItemPrefab();
            BuildSelectPanelPrefab(slotItemPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CharacterSelectPrefabBuilder] Prefab 生成完毕！");
        }

        // ══════════════════════════════════════════════════════════════
        //  CharacterSlotItem Prefab
        // ══════════════════════════════════════════════════════════════
        private static GameObject BuildSlotItemPrefab()
        {
            // ── 根节点 ────────────────────────────────────────────────
            var root = new GameObject("CharacterSlotItem");
            var rootRect = root.AddComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(0, 100);

            // LayoutElement：让 VerticalLayoutGroup 能正确识别条目高度
            var le = root.AddComponent<LayoutElement>();
            le.preferredHeight = 100;
            le.minHeight       = 100;

            // 透明 Image：IPointerClickHandler 必须有 Graphic 组件才能被 EventSystem 射线检测到
            var rootImg = root.AddComponent<Image>();
            rootImg.color = Color.clear;
            rootImg.raycastTarget = true;  // ← 关键：允许接收点击事件

            // 普通背景
            var normalBg = CreateImage(root, "NormalBg", ColSlotNormal);
            StretchFull(normalBg.rectTransform);

            // 高亮背景（叠在上面，默认透明）
            var highlightBg = CreateImage(root, "HighlightBg", Color.clear);
            StretchFull(highlightBg.rectTransform);

            // 左侧分隔竖线
            var divider = CreateImage(root, "LeftDivider", ColDivider);
            var divRect = divider.rectTransform;
            divRect.anchorMin = new Vector2(0, 0);
            divRect.anchorMax = new Vector2(0, 1);
            divRect.offsetMin = new Vector2(0, 4);
            divRect.offsetMax = new Vector2(4, -4);

            // ── 头像 ──────────────────────────────────────────────────
            var avatarBg = CreateImage(root, "AvatarBg", new Color(0.05f, 0.05f, 0.05f, 1f));
            var avatarBgRect = avatarBg.rectTransform;
            avatarBgRect.anchorMin = new Vector2(0, 0.5f);
            avatarBgRect.anchorMax = new Vector2(0, 0.5f);
            avatarBgRect.pivot     = new Vector2(0, 0.5f);
            avatarBgRect.anchoredPosition = new Vector2(12, 0);
            avatarBgRect.sizeDelta = new Vector2(76, 76);

            var avatarImg = CreateImage(avatarBg.gameObject, "AvatarImage", Color.white);
            StretchFull(avatarImg.rectTransform, 3);
            avatarImg.preserveAspect = true;

            // 头像边框
            var avatarBorder = CreateImage(avatarBg.gameObject, "AvatarBorder", ColDivider);
            StretchFull(avatarBorder.rectTransform);
            avatarBorder.type = Image.Type.Sliced;
            // 让边框只显示轮廓（覆盖在头像上，用 Outline 效果模拟）
            avatarBorder.color = new Color(ColDivider.r, ColDivider.g, ColDivider.b, 0.8f);
            avatarBorder.raycastTarget = false;

            // ── 文字区域 ──────────────────────────────────────────────
            var textArea = new GameObject("TextArea");
            textArea.transform.SetParent(root.transform, false);
            var textAreaRect = textArea.AddComponent<RectTransform>();
            textAreaRect.anchorMin = new Vector2(0, 0);
            textAreaRect.anchorMax = new Vector2(1, 1);
            textAreaRect.offsetMin = new Vector2(100, 0);
            textAreaRect.offsetMax = new Vector2(-12, 0);

            // 角色名称
            var nameText = CreateTMP(textArea, "NameText", "", 20, ColTextGold, FontStyles.Bold);
            var nameRect = nameText.rectTransform;
            nameRect.anchorMin = new Vector2(0, 0.55f);
            nameRect.anchorMax = new Vector2(0.65f, 1f);
            nameRect.offsetMin = new Vector2(0, 0);
            nameRect.offsetMax = new Vector2(0, -8);
            nameText.alignment = TextAlignmentOptions.BottomLeft;

            // 等级
            var levelText = CreateTMP(textArea, "LevelText", "", 15, ColTextWhite, FontStyles.Normal);
            var levelRect = levelText.rectTransform;
            levelRect.anchorMin = new Vector2(0.65f, 0.55f);
            levelRect.anchorMax = new Vector2(1f,    1f);
            levelRect.offsetMin = new Vector2(0, 0);
            levelRect.offsetMax = new Vector2(0, -8);
            levelText.alignment = TextAlignmentOptions.BottomRight;

            // 区服
            var serverText = CreateTMP(textArea, "ServerText", "", 13, ColTextGray, FontStyles.Normal);
            var serverRect = serverText.rectTransform;
            serverRect.anchorMin = new Vector2(0, 0);
            serverRect.anchorMax = new Vector2(0.65f, 0.52f);
            serverRect.offsetMin = new Vector2(0, 8);
            serverRect.offsetMax = new Vector2(0, 0);
            serverText.alignment = TextAlignmentOptions.TopLeft;

            // 最后游玩时间
            var lastPlayText = CreateTMP(textArea, "LastPlayText", "", 11, ColTextGray, FontStyles.Normal);
            var lastPlayRect = lastPlayText.rectTransform;
            lastPlayRect.anchorMin = new Vector2(0.65f, 0);
            lastPlayRect.anchorMax = new Vector2(1f,    0.52f);
            lastPlayRect.offsetMin = new Vector2(0, 8);
            lastPlayRect.offsetMax = new Vector2(0, 0);
            lastPlayText.alignment = TextAlignmentOptions.TopRight;

            // ── 底部分割线 ────────────────────────────────────────────
            var bottomLine = CreateImage(root, "BottomLine", ColDivider);
            var blRect = bottomLine.rectTransform;
            blRect.anchorMin = new Vector2(0, 0);
            blRect.anchorMax = new Vector2(1, 0);
            blRect.offsetMin = new Vector2(8, 0);
            blRect.offsetMax = new Vector2(-8, 1);

            // ── 挂载脚本 ──────────────────────────────────────────────
            var slotScript = root.AddComponent<CharacterSlotItem>();
            slotScript.AvatarImage   = avatarImg;
            slotScript.NameText      = nameText;
            slotScript.LevelText     = levelText;
            slotScript.ServerText    = serverText;
            slotScript.LastPlayText  = lastPlayText;
            slotScript.HighlightBg   = highlightBg;
            slotScript.NormalBg      = normalBg;

            // ── 保存 Prefab ───────────────────────────────────────
            var prefab = SavePrefab(root, SlotItemSavePath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ══════════════════════════════════════════════════════════════
        //  CharacterSelectPanel Prefab
        // ══════════════════════════════════════════════════════════════
        private static void BuildSelectPanelPrefab(GameObject slotItemPrefab)
        {
            // ── 根节点（全屏遮罩层）──────────────────────────────────
            var root = new GameObject("CharacterSelectPanel");
            var rootRect = root.AddComponent<RectTransform>();
            StretchFull(rootRect);

            // 半透明黑色遮罩
            var overlay = CreateImage(root, "Overlay", new Color(0, 0, 0, 0.6f));
            StretchFull(overlay.rectTransform);
            overlay.raycastTarget = true;

            // ── 主面板容器 ────────────────────────────────────────────
            var panel = new GameObject("Panel");
            panel.transform.SetParent(root.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot     = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(700, 600);

            // 面板背景
            var panelBg = CreateImage(panel, "PanelBg", ColPanelBg);
            StretchFull(panelBg.rectTransform);

            // 面板边框（金色描边感）
            var panelBorder = CreateImage(panel, "PanelBorder", ColDivider);
            StretchFull(panelBorder.rectTransform);
            panelBorder.raycastTarget = false;

            // ── 标题栏 ────────────────────────────────────────────────
            var titleBar = new GameObject("TitleBar");
            titleBar.transform.SetParent(panel.transform, false);
            var titleBarRect = titleBar.AddComponent<RectTransform>();
            titleBarRect.anchorMin = new Vector2(0, 1);
            titleBarRect.anchorMax = new Vector2(1, 1);
            titleBarRect.pivot     = new Vector2(0.5f, 1);
            titleBarRect.offsetMin = new Vector2(2, -60);
            titleBarRect.offsetMax = new Vector2(-2, -2);

            var titleBg = CreateImage(titleBar, "TitleBg", new Color(0.05f, 0.04f, 0.02f, 1f));
            StretchFull(titleBg.rectTransform);

            var titleText = CreateTMP(titleBar, "TitleText", "选择角色", 24, ColTextGold, FontStyles.Bold);
            StretchFull(titleText.rectTransform, 0, 0, 8, 8);
            titleText.alignment = TextAlignmentOptions.Center;

            // 标题下分割线
            var titleLine = CreateImage(titleBar, "TitleLine", ColDivider);
            var tlRect = titleLine.rectTransform;
            tlRect.anchorMin = new Vector2(0, 0);
            tlRect.anchorMax = new Vector2(1, 0);
            tlRect.offsetMin = new Vector2(0, 0);
            tlRect.offsetMax = new Vector2(0, 2);

            // ── ScrollView ────────────────────────────────────────────
            var scrollGo = new GameObject("ScrollView");
            scrollGo.transform.SetParent(panel.transform, false);
            var scrollRect = scrollGo.AddComponent<ScrollRect>();
            var scrollRectTf = scrollGo.GetComponent<RectTransform>();
            scrollRectTf.anchorMin = new Vector2(0, 0);
            scrollRectTf.anchorMax = new Vector2(1, 1);
            scrollRectTf.offsetMin = new Vector2(2, 70);   // 底部留按钮空间
            scrollRectTf.offsetMax = new Vector2(-2, -62); // 顶部留标题空间

            // Viewport
            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollGo.transform, false);
            var vpRect = viewport.AddComponent<RectTransform>();
            StretchFull(vpRect);
            var vpImg = viewport.AddComponent<Image>();
            vpImg.color = Color.white;  // Mask 需要 alpha > 0 才能正确裁剪子节点
            var vpMask = viewport.AddComponent<Mask>();
            vpMask.showMaskGraphic = false;  // 不显示白色背景，但保持 Mask 有效

            // Content
            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot     = new Vector2(0.5f, 1);
            contentRect.offsetMin = new Vector2(0, 0);
            contentRect.offsetMax = new Vector2(0, 0);

            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.spacing            = 4;
            vlg.padding            = new RectOffset(8, 8, 8, 8);
            vlg.childControlWidth  = true;
            vlg.childControlHeight = true;   // 由 LayoutElement.preferredHeight 驱动
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;

            var csf = content.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 无存档提示
            var hintText = CreateTMP(content, "HintText", "暂无角色存档\n点击「创建角色」开始冒险", 16, ColTextGray, FontStyles.Normal);
            hintText.rectTransform.sizeDelta = new Vector2(0, 120);
            hintText.alignment = TextAlignmentOptions.Center;
            hintText.gameObject.SetActive(false);

            // ScrollRect 配置
            scrollRect.content    = contentRect;
            scrollRect.viewport   = vpRect;
            scrollRect.horizontal = false;
            scrollRect.vertical   = true;
            scrollRect.scrollSensitivity = 30;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            // 滚动条
            var scrollbarGo = new GameObject("Scrollbar");
            scrollbarGo.transform.SetParent(scrollGo.transform, false);
            var sbRect = scrollbarGo.GetComponent<RectTransform>();
            if (sbRect == null) sbRect = scrollbarGo.AddComponent<RectTransform>();
            sbRect.anchorMin = new Vector2(1, 0);
            sbRect.anchorMax = new Vector2(1, 1);
            sbRect.pivot     = new Vector2(1, 0.5f);
            sbRect.offsetMin = new Vector2(-10, 0);
            sbRect.offsetMax = new Vector2(0, 0);

            var sbBg = scrollbarGo.AddComponent<Image>();
            sbBg.color = new Color(0.1f, 0.1f, 0.1f, 0.5f);

            var sbComp = scrollbarGo.AddComponent<Scrollbar>();
            sbComp.direction = Scrollbar.Direction.BottomToTop;

            var sbHandle = new GameObject("Handle");
            sbHandle.transform.SetParent(scrollbarGo.transform, false);
            var sbHandleRect = sbHandle.AddComponent<RectTransform>();
            StretchFull(sbHandleRect, 2, 2, 2, 2);
            var sbHandleImg = sbHandle.AddComponent<Image>();
            sbHandleImg.color = ColScrollbar;
            sbComp.handleRect = sbHandleRect;
            sbComp.targetGraphic = sbHandleImg;

            scrollRect.verticalScrollbar = sbComp;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;

            // ── 底部按钮区 ────────────────────────────────────────────
            var btnBar = new GameObject("ButtonBar");
            btnBar.transform.SetParent(panel.transform, false);
            var btnBarRect = btnBar.AddComponent<RectTransform>();
            btnBarRect.anchorMin = new Vector2(0, 0);
            btnBarRect.anchorMax = new Vector2(1, 0);
            btnBarRect.pivot     = new Vector2(0.5f, 0);
            btnBarRect.offsetMin = new Vector2(2, 2);
            btnBarRect.offsetMax = new Vector2(-2, 66);

            var btnBg = CreateImage(btnBar, "BtnBarBg", new Color(0.05f, 0.04f, 0.02f, 1f));
            StretchFull(btnBg.rectTransform);

            // 按钮区分割线
            var btnLine = CreateImage(btnBar, "BtnLine", ColDivider);
            var btnLineRect = btnLine.rectTransform;
            btnLineRect.anchorMin = new Vector2(0, 1);
            btnLineRect.anchorMax = new Vector2(1, 1);
            btnLineRect.offsetMin = new Vector2(0, -2);
            btnLineRect.offsetMax = new Vector2(0, 0);

            var hlg = btnBar.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing               = 12;
            hlg.padding               = new RectOffset(16, 16, 10, 10);
            hlg.childControlWidth     = true;
            hlg.childControlHeight    = true;
            hlg.childForceExpandWidth = true;

            var btnCreate = BuildButton(btnBar, "BtnCreateCharacter", "创建角色", ColBtnCreate);
            var btnEnter  = BuildButton(btnBar, "BtnEnterGame",       "进入游戏", ColBtnEnter);
            var btnDelete = BuildButton(btnBar, "BtnDeleteCharacter",  "删除角色", ColBtnDelete);

            // ── 挂载面板脚本 ──────────────────────────────────────────
            var panelScript = root.AddComponent<CharacterSelectPanel>();

            // 通过 SerializedObject 赋值私有字段
            var so = new SerializedObject(panelScript);
            so.FindProperty("_scrollContent")      .objectReferenceValue = contentRect.transform;
            so.FindProperty("_slotItemPrefab")     .objectReferenceValue = slotItemPrefab;
            so.FindProperty("_scrollRect")         .objectReferenceValue = scrollRect;
            so.FindProperty("_btnEnterGame")       .objectReferenceValue = btnEnter.GetComponent<Button>();
            so.FindProperty("_btnCreateCharacter") .objectReferenceValue = btnCreate.GetComponent<Button>();
            so.FindProperty("_btnDeleteCharacter") .objectReferenceValue = btnDelete.GetComponent<Button>();
            so.FindProperty("_btnEnterText")       .objectReferenceValue = btnEnter.GetComponentInChildren<TextMeshProUGUI>();
            so.FindProperty("_btnCreateText")      .objectReferenceValue = btnCreate.GetComponentInChildren<TextMeshProUGUI>();
            so.FindProperty("_btnDeleteText")      .objectReferenceValue = btnDelete.GetComponentInChildren<TextMeshProUGUI>();
            so.FindProperty("_hintText")           .objectReferenceValue = hintText;
            so.ApplyModifiedPropertiesWithoutUndo();

            // ── 保存 Prefab ───────────────────────────────────────
            SavePrefab(root, PrefabSavePath);
            Object.DestroyImmediate(root);
        }

        // ══════════════════════════════════════════════════════════════
        //  辅助方法
        // ══════════════════════════════════════════════════════════════

        /// <summary>创建带 Image 的子节点</summary>
        private static Image CreateImage(GameObject parent, string name, Color color)
        {
            var go   = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<RectTransform>();
            var img  = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>创建 TextMeshProUGUI 子节点</summary>
        private static TextMeshProUGUI CreateTMP(GameObject parent, string name, string text,
            float fontSize, Color color, FontStyles style)
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<RectTransform>();
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text      = text;
            tmp.fontSize  = fontSize;
            tmp.color     = color;
            tmp.fontStyle = style;
            tmp.raycastTarget = false;
            // 应用中文字体，避免中文显示为方块
            if (ChineseFont != null)
                tmp.font = ChineseFont;
            return tmp;
        }

        /// <summary>创建按钮</summary>
        private static GameObject BuildButton(GameObject parent, string name, string label, Color bgColor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<RectTransform>();

            var bg  = go.AddComponent<Image>();
            bg.color = bgColor;

            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor      = bgColor;
            colors.highlightedColor = new Color(bgColor.r + 0.15f, bgColor.g + 0.15f, bgColor.b + 0.15f, 1f);
            colors.pressedColor     = new Color(bgColor.r - 0.15f, bgColor.g - 0.15f, bgColor.b - 0.15f, 1f);
            colors.disabledColor    = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            btn.colors = colors;
            btn.targetGraphic = bg;

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            var labelRect = labelGo.AddComponent<RectTransform>();
            StretchFull(labelRect);
            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.text      = label;
            tmp.fontSize  = 16;
            tmp.color     = Color.white;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            // 应用中文字体，避免中文显示为方块
            if (ChineseFont != null)
                tmp.font = ChineseFont;

            return go;
        }

        /// <summary>拉伸填满父节点</summary>
        private static void StretchFull(RectTransform rt,
            float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left,   bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>保存 Prefab 到磁盘</summary>
        private static GameObject SavePrefab(GameObject go, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Debug.Log($"[Builder] 已保存: {path}");
            return prefab;
        }

        /// <summary>确保目录存在</summary>
        private static void EnsureDirectory(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                var parts  = path.Split('/');
                var parent = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    var full = parent + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(full))
                        AssetDatabase.CreateFolder(parent, parts[i]);
                    parent = full;
                }
            }
        }

        // ── 测试菜单 ──────────────────────────────────────────────────
        [MenuItem("POELike/UI/写入测试存档")]
        public static void WriteTestSaves()
        {
            CharacterSaveManager.CreateTestSaves();
        }
    }
}
