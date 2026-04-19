using System.Collections.Generic;
using POELike.Game.Equipment;
using POELike.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace POELike.Game.UI
{
    /// <summary>
    /// 装备道具视图组件
    /// 挂载在 Equipment 预制体上，放入背包时调用 <see cref="SetupInBag"/> 设置尺寸。
    /// 宽高 = 格子数 × 格子尺寸 + (格子数 - 1) × 间距
    /// 鼠标悬停时通过内部 ListBox 显示插槽列表。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(Image))]
    public class EquipmentItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        // ── 序列化字段 ────────────────────────────────────────────────

        [Header("装备图标")]
        [SerializeField] private Image _icon;

        [Header("插槽面板（挂在子节点上）")]
        [SerializeField] private RectTransform _socketPanel;

        [Header("插槽预制体（SocketItem prefab）")]
        [SerializeField] private GameObject _socketItemPrefab;

        [Header("装备提示框预制体（EquipmentTips prefab）")]
        [SerializeField] private GameObject _equipmentTipsPrefab;

        [Header("Tips 字体行高（像素，用于自适应高度）")]
        [SerializeField] private float _tipsLineHeight = 36f;

        /// <summary>每个插槽的基础尺寸（像素）</summary>
        private const float SocketSize = 35f;
        /// <summary>插槽之间的基础间距（像素）</summary>
        private const float SocketSpacing = 10f;
        /// <summary>插槽连接线的基础粗细（像素）</summary>
        private const float SocketLinkThickness = 6f;
        /// <summary>插槽布局在装备内部保留的安全边距（像素）</summary>
        private const float SocketLayoutPadding = 8f;
        /// <summary>插槽显示的最大放大倍率</summary>
        private const float MaxSocketDisplayScale = 2.2f;
        /// <summary>装备与 Tips 之间的水平间距（像素）</summary>
        private const float TipsOffset = 8f;
        /// <summary>Tips 距离屏幕边缘的最小留白（像素）</summary>
        private const float TipsScreenPadding = 8f;

        /// <summary>插槽连接线颜色</summary>
        private static readonly Color SocketLinkColor = new Color(0.82f, 0.72f, 0.42f, 0.95f);

        /// <summary>插槽布局测量结果</summary>
        private struct SocketLayoutMetrics
        {
            public int Cols;
            public int Rows;
            public float SocketSize;
            public float SocketSpacing;
            public float LineThickness;
            public float PanelWidth;
            public float PanelHeight;
        }

        /// <summary>共享的白底纹理，避免每个装备对象各自创建运行时资源。</summary>
        private static Texture2D s_sharedWhiteTexture;

        /// <summary>共享的白底 Sprite，供所有装备背景复用。</summary>
        private static Sprite s_sharedWhiteSprite;

        // ── 运行时数据 ────────────────────────────────────────────────

        /// <summary>对应的背包道具数据（由外部赋值）</summary>
        public BagItemData BagData { get; private set; }

        /// <summary>用于悬停提示的背包道具数据。</summary>
        private BagItemData _tooltipBagData;

        /// <summary>装备细节类别 ID（对应 EquipmentDetailTypeConf）</summary>
        public int DetailTypeId { get; private set; }

        /// <summary>装备占用格子列数（宽）</summary>
        public int GridWidth { get; private set; } = 1;

        /// <summary>装备占用格子行数（高）</summary>
        public int GridHeight { get; private set; } = 1;

        /// <summary>插槽数据列表</summary>
        private List<SocketData> _sockets;

        /// <summary>生成的装备运行时数据（用于填充 Tips）</summary>
        private GeneratedEquipment _generatedEquipment;

        /// <summary>当前显示的 Tips 实例</summary>
        private EquipmentTips _tipsInstance;

        /// <summary>当前 Tips 是否已显示</summary>
        private bool _tipsShown;

        /// <summary>复用的插槽视图列表</summary>
        private readonly List<GameObject> _socketViews = new List<GameObject>();

        /// <summary>复用的插槽连接线视图列表</summary>
        private readonly List<GameObject> _socketLinkViews = new List<GameObject>();

        /// <summary>已镶嵌到各个插槽中的宝石数据。</summary>
        private readonly List<BagItemData> _socketedGems = new List<BagItemData>();

        /// <summary>最近一次背包显示下的装备宽度，用于推导装备栏中的放大倍率。</summary>
        private float _lastBagVisualWidth = -1f;

        /// <summary>最近一次背包显示下的装备高度，用于推导装备栏中的放大倍率。</summary>
        private float _lastBagVisualHeight = -1f;

        /// <summary>Tips 定位时复用的世界坐标角点数组</summary>
        private readonly Vector3[] _tipsWorldCorners = new Vector3[4];

        private TextMeshProUGUI _stackCountText;
        private TMP_FontAsset _stackCountFont;

        // ── 属性 ──────────────────────────────────────────────────────

        private RectTransform _rt;
        public RectTransform RT => _rt ??= GetComponent<RectTransform>();

        private static Sprite SharedWhiteSprite
        {
            get
            {
                if (s_sharedWhiteSprite != null)
                    return s_sharedWhiteSprite;

                if (s_sharedWhiteTexture == null)
                {
                    s_sharedWhiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                    s_sharedWhiteTexture.SetPixel(0, 0, Color.white);
                    s_sharedWhiteTexture.Apply();
                    s_sharedWhiteTexture.hideFlags = HideFlags.HideAndDontSave;
                }

                s_sharedWhiteSprite = Sprite.Create(
                    s_sharedWhiteTexture,
                    new Rect(0, 0, 1, 1),
                    new Vector2(0.5f, 0.5f));
                s_sharedWhiteSprite.hideFlags = HideFlags.HideAndDontSave;

                return s_sharedWhiteSprite;
            }
        }

        // ── 初始化 ────────────────────────────────────────────────────

        /// <summary>根节点 Image（用于显示装备背景色块）</summary>
        private Image _bgImage;

        private void Awake()
        {
            EnsureCachedReferences();

            if (BagData != null)
            {
                ApplyItemVisuals(BagData.Icon, BagData.ItemColor);
                RefreshStackCountVisual();
            }
        }

        private void EnsureCachedReferences()
        {
            _rt ??= GetComponent<RectTransform>();
            _bgImage ??= GetComponent<Image>();

            if (_lastBagVisualWidth <= 0f)
                _lastBagVisualWidth = Mathf.Max(1f, _rt != null ? _rt.rect.width : 1f);
            if (_lastBagVisualHeight <= 0f)
                _lastBagVisualHeight = Mathf.Max(1f, _rt != null ? _rt.rect.height : 1f);

            if (_bgImage != null && _bgImage.sprite == null)
                _bgImage.sprite = SharedWhiteSprite;

            _icon ??= _bgImage;

            if (_socketPanel == null)
            {
                var child = transform.Find("SocketPanel");
                if (child != null)
                    _socketPanel = child.GetComponent<RectTransform>();
            }

            if (_socketPanel != null && !_tipsShown)
                _socketPanel.gameObject.SetActive(false);
        }

        /// <summary>
        /// 初始化装备基础信息（在生成装备时调用，传入完整的 GeneratedEquipment 数据）
        /// </summary>
        public void Init(GeneratedEquipment equip, Sprite icon = null)
        {
            if (equip == null) return;

            var detail = equip.DetailType;
            int.TryParse(detail?.EquipmentWidth,  out int w); w = Mathf.Max(1, w);
            int.TryParse(detail?.EquipmentHeight, out int h); h = Mathf.Max(1, h);

            Init(
                detailTypeId: int.TryParse(detail?.EquipmentDetailTypeId, out int id) ? id : 0,
                gridWidth:    w,
                gridHeight:   h,
                itemName:     equip.DisplayName,
                icon:         icon,
                itemColor:    equip.QualityColor,
                sockets:      equip.Sockets
            );

            _generatedEquipment = equip;

            if (BagData != null)
                _tooltipBagData = BagData;
        }

        /// <summary>
        /// 使用 UI 层道具数据初始化视图。
        /// 适用于背包装备、药剂等已经具备完整展示数据的物品。
        /// </summary>
        public void Init(BagItemData itemData, Sprite icon = null)
        {
            EnsureCachedReferences();

            if (itemData == null)
                return;

            DetailTypeId = int.TryParse(itemData.ItemId, out int id) ? id : 0;
            GridWidth    = Mathf.Max(1, itemData.GridWidth);
            GridHeight   = Mathf.Max(1, itemData.GridHeight);
            _sockets     = itemData.Sockets;
            _generatedEquipment = null;
            _socketedGems.Clear();
            EnsureSocketedGemCapacity();

            BagData = itemData;
            if (icon != null)
                BagData.Icon = icon;

            _tooltipBagData = BagData;
            ApplyItemVisuals(BagData.Icon, BagData.ItemColor);
            RefreshStackCountVisual();
            HideSockets();
            HideTips();
            _tipsShown = false;
        }

        public void Init(int detailTypeId, int gridWidth, int gridHeight,
                         string itemName, Sprite icon = null, Color? itemColor = null,
                         List<SocketData> sockets = null)
        {
            EnsureCachedReferences();

            DetailTypeId = detailTypeId;
            GridWidth    = Mathf.Max(1, gridWidth);
            GridHeight   = Mathf.Max(1, gridHeight);
            _sockets     = sockets;
            _generatedEquipment = null;
            _socketedGems.Clear();
            EnsureSocketedGemCapacity();

            // 构建 BagItemData
            BagData = new BagItemData(
                itemId:     detailTypeId.ToString(),
                name:       itemName,
                gridWidth:  GridWidth,
                gridHeight: GridHeight
            );
            BagData.Icon      = icon;
            BagData.ItemColor = itemColor ?? Color.white;
            if (sockets != null)
                BagData.Sockets.AddRange(sockets);
            _tooltipBagData = BagData;

            ApplyItemVisuals(BagData.Icon, BagData.ItemColor);
            RefreshStackCountVisual();

            HideSockets();
            HideTips();
            _tipsShown = false;
        }

        public void RefreshFromBagData(BagItemData itemData)
        {
            EnsureCachedReferences();
            if (itemData == null)
                return;

            BagData = itemData;
            GridWidth = Mathf.Max(1, itemData.GridWidth);
            GridHeight = Mathf.Max(1, itemData.GridHeight);
            _sockets = itemData.Sockets;
            _tooltipBagData = itemData;
            EnsureSocketedGemCapacity();

            ApplyItemVisuals(itemData.Icon, itemData.ItemColor);
            RefreshStackCountVisual();
        }

        private void ApplyItemVisuals(Sprite icon, Color col)
        {
            // 设置根节点背景色（始终生效，确保色块可见）
            if (_bgImage != null)
                _bgImage.color = col;

            // 设置图标（若 _icon 与 _bgImage 不同，则单独设置）
            if (_icon != null && _icon != _bgImage)
            {
                _icon.sprite = icon;
                _icon.color  = col;
            }
        }

        private void RefreshStackCountVisual()
        {
            if (BagData == null || !BagData.IsStackable || BagData.StackCount <= 1)
            {
                if (_stackCountText != null)
                    _stackCountText.gameObject.SetActive(false);
                return;
            }

            var countText = EnsureStackCountText();
            if (countText == null)
                return;

            countText.gameObject.SetActive(true);
            countText.text = BagData.StackCount.ToString();
            countText.fontSize = Mathf.Clamp(Mathf.Min(RT.rect.width, RT.rect.height) * 0.28f, 14f, 28f);
        }

        private TextMeshProUGUI EnsureStackCountText()
        {
            if (_stackCountText != null)
                return _stackCountText;

            var font = ResolveStackCountFont();
            if (font == null)
                return null;

            var go = new GameObject("StackCountText", typeof(RectTransform));
            go.transform.SetParent(transform, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-4f, 4f);
            rt.sizeDelta = new Vector2(44f, 24f);

            _stackCountText = go.AddComponent<TextMeshProUGUI>();
            _stackCountText.raycastTarget = false;
            _stackCountText.alignment = TextAlignmentOptions.BottomRight;
            _stackCountText.enableWordWrapping = false;
            _stackCountText.overflowMode = TextOverflowModes.Overflow;
            _stackCountText.font = font;
            if (font.material != null)
                _stackCountText.fontSharedMaterial = font.material;
            _stackCountText.color = Color.white;
            if (_stackCountText.fontSharedMaterial != null)
            {
                _stackCountText.outlineColor = Color.black;
                _stackCountText.outlineWidth = 0.2f;
            }
            _stackCountText.transform.SetAsLastSibling();
            return _stackCountText;
        }

        private TMP_FontAsset ResolveStackCountFont()
        {
            if (_stackCountFont != null)
                return _stackCountFont;

            _stackCountFont = TMP_Settings.defaultFontAsset;
            if (_stackCountFont == null)
                _stackCountFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

            if (_stackCountFont == null)
            {
                var loadedFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                for (int i = 0; i < loadedFonts.Length; i++)
                {
                    var font = loadedFonts[i];
                    if (font == null)
                        continue;

                    _stackCountFont = font;
                    if (font.material != null)
                        break;
                }
            }

            return _stackCountFont;
        }

        /// <summary>
        /// 放入背包时调用，根据格子尺寸和间距设置自身 RectTransform 大小。
        /// </summary>
        private const float ItemPadding = 2f;

        public void SetupInBag(float cellSize, float cellSpacing, int col, int row)
        {
            float w = GridWidth  * cellSize + (GridWidth  - 1) * cellSpacing - ItemPadding * 2f;
            float h = GridHeight * cellSize + (GridHeight - 1) * cellSpacing - ItemPadding * 2f;

            RT.anchorMin        = new Vector2(0f, 1f);
            RT.anchorMax        = new Vector2(0f, 1f);
            RT.pivot            = new Vector2(0f, 1f);
            RT.sizeDelta        = new Vector2(w, h);
            RT.anchoredPosition = new Vector2(
                 col * (cellSize + cellSpacing) + ItemPadding,
                -row * (cellSize + cellSpacing) - ItemPadding
            );

            _lastBagVisualWidth = w;
            _lastBagVisualHeight = h;
            RefreshStackCountVisual();

            // SocketPanel 在悬停时按当前显示尺寸动态重算
        }

        // ── 鼠标悬停 ──────────────────────────────────────────────────

        public void OnPointerEnter(PointerEventData eventData) { }
        public void OnPointerExit(PointerEventData eventData) { }

        private void Update()
        {
            var itemView = GetComponent<BagItemView>();
            if (itemView != null && BagItemView.CurrentDraggingItem == itemView)
            {
                if (_tipsShown)
                {
                    HideSockets();
                    HideTips();
                    _tipsShown = false;
                }

                return;
            }

            // 直接检测鼠标是否在装备 RectTransform 范围内，避免子节点/兄弟节点边界误触发
            var cam = GetComponentInParent<Canvas>()?.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : Camera.main;
            var mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 mousePos = mouse.position.ReadValue();
            bool over = RectTransformUtility.RectangleContainsScreenPoint(RT, mousePos, cam);

            if (over)
            {
                if (!_tipsShown)
                {
                    ShowSockets();
                    ShowTips();
                    _tipsShown = true;
                }
                else
                {
                    UpdateTipsPosition();
                }
            }
            else if (_tipsShown)
            {
                HideSockets();
                HideTips();
                _tipsShown = false;
            }
        }

        // ── 插槽显示 ──────────────────────────────────────────────────

        private void ShowSockets()
        {
            if (_socketPanel == null) return;

            if (_sockets == null || _sockets.Count == 0 || _socketItemPrefab == null)
            {
                HideSockets();
                return;
            }

            var layout = ResolveSocketLayoutMetrics(_sockets.Count);
            var visibleSocketRects = new RectTransform[_sockets.Count];

            EnsureSocketViews(_sockets.Count);
            EnsureSocketLinkViews(Mathf.Max(0, _sockets.Count - 1));

            for (int i = 0; i < _socketViews.Count; i++)
            {
                var go = _socketViews[i];
                if (go == null) continue;

                bool active = i < _sockets.Count;
                go.SetActive(active);
                if (!active) continue;

                var rt = go.GetComponent<RectTransform>();
                if (rt != null)
                {
                    GetSocketGridPosition(i, layout.Cols, out int c, out int r);
                    ConfigureSocketRect(rt, layout.SocketSize, layout.SocketSpacing, c, r);
                    visibleSocketRects[i] = rt;
                }

                var socketItem = go.GetComponent<SocketItem>();
                if (socketItem != null)
                    socketItem.SetupSocket(this, i, _sockets[i].Color);
            }

            UpdateSocketLinkViews(visibleSocketRects, layout.LineThickness);

            _socketPanel.anchorMin        = new Vector2(0.5f, 0.5f);
            _socketPanel.anchorMax        = new Vector2(0.5f, 0.5f);
            _socketPanel.pivot            = new Vector2(0.5f, 0.5f);
            _socketPanel.localScale       = Vector3.one;
            _socketPanel.localRotation    = Quaternion.identity;
            _socketPanel.anchoredPosition = Vector2.zero;
            _socketPanel.sizeDelta        = new Vector2(layout.PanelWidth, layout.PanelHeight);
            _socketPanel.gameObject.SetActive(true);

            // 将自身置顶，确保插槽面板不被同级的 ItemView 节点遮挡
            transform.SetAsLastSibling();
        }

        private SocketLayoutMetrics ResolveSocketLayoutMetrics(int socketCount)
        {
            var layout = new SocketLayoutMetrics
            {
                Cols = GridWidth >= 2 ? 2 : 1,
            };
            layout.Rows = Mathf.CeilToInt((float)socketCount / layout.Cols);

            float desiredScale = ResolveSocketDisplayScale();
            float availableWidth = Mathf.Max(SocketSize, RT.rect.width - SocketLayoutPadding * 2f);
            float availableHeight = Mathf.Max(SocketSize, RT.rect.height - SocketLayoutPadding * 2f);

            float basePanelWidth = layout.Cols * SocketSize + (layout.Cols - 1) * SocketSpacing;
            float basePanelHeight = layout.Rows * SocketSize + (layout.Rows - 1) * SocketSpacing;

            float fitScaleX = basePanelWidth > 0f ? availableWidth / basePanelWidth : desiredScale;
            float fitScaleY = basePanelHeight > 0f ? availableHeight / basePanelHeight : desiredScale;
            float finalScale = Mathf.Min(desiredScale, fitScaleX, fitScaleY);
            if (float.IsNaN(finalScale) || float.IsInfinity(finalScale) || finalScale <= 0f)
                finalScale = 1f;

            finalScale = Mathf.Min(finalScale, MaxSocketDisplayScale);

            layout.SocketSize = SocketSize * finalScale;
            layout.SocketSpacing = SocketSpacing * finalScale;
            layout.LineThickness = Mathf.Clamp(SocketLinkThickness * finalScale, 3f, 14f);
            layout.PanelWidth = layout.Cols * layout.SocketSize + (layout.Cols - 1) * layout.SocketSpacing;
            layout.PanelHeight = layout.Rows * layout.SocketSize + (layout.Rows - 1) * layout.SocketSpacing;
            return layout;
        }

        private float ResolveSocketDisplayScale()
        {
            float currentWidth = Mathf.Max(1f, RT.rect.width);
            float currentHeight = Mathf.Max(1f, RT.rect.height);
            float baseWidth = _lastBagVisualWidth > 0f ? _lastBagVisualWidth : currentWidth;
            float baseHeight = _lastBagVisualHeight > 0f ? _lastBagVisualHeight : currentHeight;

            float scaleX = currentWidth / Mathf.Max(1f, baseWidth);
            float scaleY = currentHeight / Mathf.Max(1f, baseHeight);
            return Mathf.Clamp(Mathf.Min(scaleX, scaleY), 1f, MaxSocketDisplayScale);
        }

        private static void GetSocketGridPosition(int socketIndex, int cols, out int col, out int row)
        {
            if (cols <= 1)
            {
                col = 0;
                row = socketIndex;
                return;
            }

            row = socketIndex / cols;
            int indexInRow = socketIndex % cols;
            bool leftToRight = row % 2 == 0;
            col = leftToRight ? indexInRow : cols - 1 - indexInRow;
        }

        private static void ConfigureSocketRect(RectTransform rt, float socketSize, float socketSpacing, int col, int row)
        {
            if (rt == null)
                return;

            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            rt.sizeDelta = new Vector2(socketSize, socketSize);
            rt.anchoredPosition = new Vector2(
                 col * (socketSize + socketSpacing),
                -row * (socketSize + socketSpacing)
            );
        }

        private void EnsureSocketLinkViews(int count)
        {
            while (_socketLinkViews.Count < count)
            {
                var go = new GameObject($"SocketLink_{_socketLinkViews.Count}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(_socketPanel, false);

                var image = go.GetComponent<Image>();
                image.sprite = SharedWhiteSprite;
                image.color = SocketLinkColor;
                image.raycastTarget = false;

                _socketLinkViews.Add(go);
            }
        }

        private void UpdateSocketLinkViews(RectTransform[] socketRectsByIndex, float lineThickness)
        {
            int requiredLinks = socketRectsByIndex != null && socketRectsByIndex.Length > 1
                ? socketRectsByIndex.Length - 1
                : 0;

            for (int i = 0; i < _socketLinkViews.Count; i++)
            {
                var go = _socketLinkViews[i];
                if (go == null)
                    continue;

                bool active = i < requiredLinks;
                go.SetActive(active);
                if (!active)
                    continue;

                go.transform.SetAsFirstSibling();

                var lineRt = go.transform as RectTransform;
                if (lineRt == null)
                    continue;

                if (socketRectsByIndex == null ||
                    i < 0 ||
                    i + 1 >= socketRectsByIndex.Length ||
                    socketRectsByIndex[i] == null ||
                    socketRectsByIndex[i + 1] == null)
                {
                    go.SetActive(false);
                    continue;
                }

                ConfigureSocketLinkRect(lineRt, socketRectsByIndex[i], socketRectsByIndex[i + 1], lineThickness);
            }
        }

        /// <summary>
        /// 判断两个插槽是否连结。
        /// 当前只支持相邻插槽连结；是否真正连结由 `SocketData.LinkedToPrevious` 控制。
        /// </summary>
        public bool AreSocketsLinked(int firstIndex, int secondIndex)
        {
            EnsureSocketedGemCapacity();
            if (_sockets == null)
                return false;
            if (firstIndex < 0 || secondIndex < 0)
                return false;
            if (firstIndex >= _sockets.Count || secondIndex >= _sockets.Count)
                return false;
            if (firstIndex == secondIndex)
                return false;

            if (Mathf.Abs(firstIndex - secondIndex) != 1)
                return false;

            int higherIndex = Mathf.Max(firstIndex, secondIndex);
            return _sockets[higherIndex]?.LinkedToPrevious ?? false;
        }

        /// <summary>
        /// 获取与指定插槽连结的相邻插槽索引。
        /// 只返回当前明确处于连结状态的前一个和后一个索引。
        /// </summary>
        public bool TryGetLinkedSocketIndices(int socketIndex, out int previousIndex, out int nextIndex)
        {
            previousIndex = -1;
            nextIndex = -1;

            EnsureSocketedGemCapacity();
            if (_sockets == null || socketIndex < 0 || socketIndex >= _sockets.Count)
                return false;

            if (socketIndex - 1 >= 0 && AreSocketsLinked(socketIndex, socketIndex - 1))
                previousIndex = socketIndex - 1;
            if (socketIndex + 1 < _sockets.Count && AreSocketsLinked(socketIndex, socketIndex + 1))
                nextIndex = socketIndex + 1;

            return previousIndex >= 0 || nextIndex >= 0;
        }

        /// <summary>
        /// 获取与指定插槽相连的宝石数据。
        /// 当前只会返回左右两侧明确连结且已经镶嵌的宝石。
        /// </summary>
        public void GetLinkedGems(int socketIndex, List<BagItemData> results)
        {
            if (results == null)
                return;

            results.Clear();
            EnsureSocketedGemCapacity();

            if (_socketedGems.Count == 0 || socketIndex < 0 || socketIndex >= _socketedGems.Count)
                return;

            if (socketIndex - 1 >= 0 && AreSocketsLinked(socketIndex, socketIndex - 1))
            {
                var previousGem = _socketedGems[socketIndex - 1];
                if (previousGem != null)
                    results.Add(previousGem);
            }

            if (socketIndex + 1 < _socketedGems.Count && AreSocketsLinked(socketIndex, socketIndex + 1))
            {
                var nextGem = _socketedGems[socketIndex + 1];
                if (nextGem != null)
                    results.Add(nextGem);
            }
        }

        private static void ConfigureSocketLinkRect(RectTransform lineRt, RectTransform from, RectTransform to, float thickness)
        {
            if (lineRt == null || from == null || to == null)
                return;

            Vector2 fromCenter = GetSocketCenter(from);
            Vector2 toCenter = GetSocketCenter(to);
            Vector2 delta = toCenter - fromCenter;
            float length = delta.magnitude;

            lineRt.anchorMin = new Vector2(0f, 1f);
            lineRt.anchorMax = new Vector2(0f, 1f);
            lineRt.pivot = new Vector2(0.5f, 0.5f);
            lineRt.localScale = Vector3.one;
            lineRt.sizeDelta = new Vector2(length, thickness);
            lineRt.anchoredPosition = (fromCenter + toCenter) * 0.5f;
            lineRt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        private static Vector2 GetSocketCenter(RectTransform socketRt)
        {
            return new Vector2(
                socketRt.anchoredPosition.x + socketRt.rect.width * 0.5f,
                socketRt.anchoredPosition.y - socketRt.rect.height * 0.5f
            );
        }

        private void EnsureSocketViews(int count)

        {
            for (int i = 0; i < _socketViews.Count; i++)
            {
                if (_socketViews[i] != null) continue;

                var recreated = Instantiate(_socketItemPrefab, _socketPanel);
                recreated.SetActive(false);
                _socketViews[i] = recreated;
            }

            while (_socketViews.Count < count)
            {
                var go = Instantiate(_socketItemPrefab, _socketPanel);
                go.SetActive(false);
                _socketViews.Add(go);
            }
        }

        private void HideSockets()
        {
            if (_socketPanel == null) return;
            _socketPanel.gameObject.SetActive(false);
        }

        public void SetSocketedGem(int socketIndex, BagItemData gemData)
        {
            EnsureSocketedGemCapacity();
            if (socketIndex < 0 || socketIndex >= _socketedGems.Count)
                return;

            _socketedGems[socketIndex] = gemData;
        }

        public void GetSocketedActiveGems(List<BagItemData> results)
        {
            if (results == null)
                return;

            EnsureSocketedGemCapacity();
            for (int i = 0; i < _socketedGems.Count; i++)
            {
                var gemData = _socketedGems[i];
                if (gemData == null || !gemData.IsActiveSkillGem)
                    continue;

                results.Add(gemData);
            }
        }

        private void EnsureSocketedGemCapacity()
        {
            int socketCount = _sockets != null ? _sockets.Count : 0;

            while (_socketedGems.Count < socketCount)
                _socketedGems.Add(null);

            while (_socketedGems.Count > socketCount)
                _socketedGems.RemoveAt(_socketedGems.Count - 1);
        }

        private BagItemData ResolveTooltipBagData()
        {
            var itemView = GetComponent<BagItemView>();
            if (itemView != null && itemView.Data != null)
                return itemView.Data;

            return _tooltipBagData ?? BagData;
        }

        // ── Tips 显示 ─────────────────────────────────────────────────

        private void ShowTips()
        {
            if (_equipmentTipsPrefab == null) return;

            var tipsParent = ResolveTipsParent();

            // 若无实例则创建（挂载到同级父节点下，避免被装备自身的 Mask 裁剪）
            if (_tipsInstance == null)
            {
                var go = Instantiate(_equipmentTipsPrefab, tipsParent);
                _tipsInstance = go.GetComponent<EquipmentTips>();
                if (_tipsInstance == null)
                    _tipsInstance = go.AddComponent<EquipmentTips>();
            }
            else if (_tipsInstance.transform.parent != tipsParent)
            {
                _tipsInstance.transform.SetParent(tipsParent, false);
            }

            // 先 Setup 计算好高度
            if (_generatedEquipment != null)
            {
                _tipsInstance.Setup(_generatedEquipment);
            }
            else
            {
                var bagData = ResolveTooltipBagData();
                if (bagData == null || (!bagData.IsEquipment && !bagData.IsFlask && !bagData.IsCurrency))
                    return;

                _tipsInstance.Setup(bagData);

            }

            UpdateTipsPosition();

            // 最后激活并置顶
            _tipsInstance.gameObject.SetActive(true);
            _tipsInstance.transform.SetAsLastSibling();
        }

        private RectTransform ResolveTipsParent()
        {
            if (UIManager.Instance != null)
            {
                var overlay = UIManager.Instance.TooltipOverlayRoot;
                if (overlay != null)
                    return overlay;
            }

            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                var rootCanvas = canvas.rootCanvas != null ? canvas.rootCanvas : canvas;
                if (rootCanvas.transform is RectTransform rootRt)
                    return rootRt;
            }

            return transform.parent as RectTransform ?? RT;
        }

        private bool ShouldPlaceTipsAtUpperCorner()
        {
            var itemView = GetComponent<BagItemView>();
            return itemView != null && itemView.CurrentSlot != null;
        }

        private void UpdateTipsPosition()
        {
            if (_tipsInstance == null)
                return;

            var tipsRt = _tipsInstance.GetComponent<RectTransform>();
            if (tipsRt == null)
                return;

            var tipsParent = tipsRt.parent as RectTransform;
            if (tipsParent == null)
                return;

            Canvas.ForceUpdateCanvases();

            RT.GetWorldCorners(_tipsWorldCorners);
            Vector3 itemTopLeft = _tipsWorldCorners[1];
            Vector3 itemTopRight = _tipsWorldCorners[2];
            Vector3 itemTopLeftLocal = tipsParent.InverseTransformPoint(itemTopLeft);
            Vector3 itemTopRightLocal = tipsParent.InverseTransformPoint(itemTopRight);
            var parentRect = tipsParent.rect;
            float parentWidth = parentRect.width;
            float parentHeight = parentRect.height;
            float itemLeftXFromLeft = itemTopLeftLocal.x + parentWidth * tipsParent.pivot.x;
            float itemRightXFromLeft = itemTopRightLocal.x + parentWidth * tipsParent.pivot.x;
            float itemTopYFromTop = parentHeight * (1f - tipsParent.pivot.y) - itemTopRightLocal.y;
            float tipsWidth = tipsRt.rect.width;
            float tipsHeight = tipsRt.rect.height;

            float xFromLeft;
            float yFromTop;

            if (ShouldPlaceTipsAtUpperCorner())
            {
                ResolveUpperCornerTipsPosition(
                    itemLeftXFromLeft,
                    itemRightXFromLeft,
                    itemTopYFromTop,
                    tipsWidth,
                    tipsHeight,
                    parentWidth,
                    parentHeight,
                    out xFromLeft,
                    out yFromTop);
            }
            else
            {
                ResolveSideTipsPosition(
                    itemLeftXFromLeft,
                    itemRightXFromLeft,
                    itemTopYFromTop,
                    tipsWidth,
                    tipsHeight,
                    parentWidth,
                    parentHeight,
                    out xFromLeft,
                    out yFromTop);
            }

            tipsRt.anchorMin = new Vector2(0f, 1f);
            tipsRt.anchorMax = new Vector2(0f, 1f);
            tipsRt.pivot     = new Vector2(0f, 1f);
            tipsRt.anchoredPosition = new Vector2(xFromLeft, -yFromTop);
        }

        private static void ResolveSideTipsPosition(
            float itemLeftXFromLeft,
            float itemRightXFromLeft,
            float itemTopYFromTop,
            float tipsWidth,
            float tipsHeight,
            float parentWidth,
            float parentHeight,
            out float xFromLeft,
            out float yFromTop)
        {
            xFromLeft = itemRightXFromLeft + TipsOffset;
            if (xFromLeft + tipsWidth > parentWidth - TipsScreenPadding)
                xFromLeft = itemLeftXFromLeft - TipsOffset - tipsWidth;

            float maxXFromLeft = Mathf.Max(TipsScreenPadding, parentWidth - tipsWidth - TipsScreenPadding);
            float maxYFromTop = Mathf.Max(TipsScreenPadding, parentHeight - tipsHeight - TipsScreenPadding);

            xFromLeft = Mathf.Clamp(xFromLeft, TipsScreenPadding, maxXFromLeft);
            yFromTop = Mathf.Clamp(itemTopYFromTop, TipsScreenPadding, maxYFromTop);
        }

        private static void ResolveUpperCornerTipsPosition(
            float itemLeftXFromLeft,
            float itemRightXFromLeft,
            float itemTopYFromTop,
            float tipsWidth,
            float tipsHeight,
            float parentWidth,
            float parentHeight,
            out float xFromLeft,
            out float yFromTop)
        {
            float rightCandidateX = itemRightXFromLeft + TipsOffset;
            float leftCandidateX = itemLeftXFromLeft - TipsOffset - tipsWidth;
            bool canPlaceRight = rightCandidateX + tipsWidth <= parentWidth - TipsScreenPadding;
            bool canPlaceLeft = leftCandidateX >= TipsScreenPadding;

            if (canPlaceRight)
            {
                xFromLeft = rightCandidateX;
            }
            else if (canPlaceLeft)
            {
                xFromLeft = leftCandidateX;
            }
            else
            {
                float maxXFromLeft = Mathf.Max(TipsScreenPadding, parentWidth - tipsWidth - TipsScreenPadding);
                float preferredX = itemRightXFromLeft <= parentWidth * 0.5f ? rightCandidateX : leftCandidateX;
                xFromLeft = Mathf.Clamp(preferredX, TipsScreenPadding, maxXFromLeft);
            }

            float preferredYFromTop = itemTopYFromTop - TipsOffset - tipsHeight;
            float maxYFromTop = Mathf.Max(TipsScreenPadding, parentHeight - tipsHeight - TipsScreenPadding);
            yFromTop = Mathf.Clamp(preferredYFromTop, TipsScreenPadding, maxYFromTop);
        }

        private void HideTips()
        {
            if (_tipsInstance != null)
                _tipsInstance.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            HideSockets();
            HideTips();
            _tipsShown = false;
        }

        private void OnDestroy()
        {
            if (_tipsInstance != null)
                Destroy(_tipsInstance.gameObject);
        }
    }
}