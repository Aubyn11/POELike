using System.Collections.Generic;
using POELike.Game.Equipment;
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

        /// <summary>每个插槽格子的尺寸（像素）</summary>
        private const float SocketSize    = 35f;
        /// <summary>插槽之间的间距（像素）</summary>
        private const float SocketSpacing = 5f;
        /// <summary>装备与 Tips 之间的水平间距（像素）</summary>
        private const float TipsOffset = 8f;
        /// <summary>Tips 距离屏幕边缘的最小留白（像素）</summary>
        private const float TipsScreenPadding = 8f;

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

        /// <summary>Tips 定位时复用的世界坐标角点数组</summary>
        private readonly Vector3[] _tipsWorldCorners = new Vector3[4];

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
            _rt      = GetComponent<RectTransform>();
            _bgImage = GetComponent<Image>();

            // 若根节点 Image 没有 sprite，则复用共享白底 Sprite，避免每个物体单独创建运行时资源
            if (_bgImage != null && _bgImage.sprite == null)
                _bgImage.sprite = SharedWhiteSprite;

            // _icon 优先使用 Inspector 赋值的子节点图标，未赋值时回退到根节点 Image
            _icon ??= _bgImage;

            // 自动查找子节点上的 SocketPanel（Inspector 未赋值时兜底）
            if (_socketPanel == null)
            {
                // 尝试按名称查找
                var child = transform.Find("SocketPanel");
                if (child != null) _socketPanel = child.GetComponent<RectTransform>();
            }

            // 默认隐藏插槽面板
            if (_socketPanel != null)
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

        public void Init(int detailTypeId, int gridWidth, int gridHeight,
                         string itemName, Sprite icon = null, Color? itemColor = null,
                         List<SocketData> sockets = null)
        {
            DetailTypeId = detailTypeId;
            GridWidth    = Mathf.Max(1, gridWidth);
            GridHeight   = Mathf.Max(1, gridHeight);
            _sockets     = sockets;
            _generatedEquipment = null;

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

            Color col = itemColor ?? Color.white;

            // 设置根节点背景色（始终生效，确保色块可见）
            if (_bgImage != null)
                _bgImage.color = col;

            // 设置图标（若 _icon 与 _bgImage 不同，则单独设置）
            if (_icon != null && _icon != _bgImage)
            {
                _icon.sprite = icon;
                _icon.color  = col;
            }

            HideSockets();
            HideTips();
            _tipsShown = false;
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

            // SocketPanel 不跟随装备大小变化，保持自身固定尺寸
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

            // 排列规则：
            //   宽度 = 1 → 每列 1 个，从上到下
            //   宽度 ≥ 2 → 每行最多 2 个，从左到右，超出换行
            int cols = GridWidth >= 2 ? 2 : 1;
            EnsureSocketViews(_sockets.Count);

            for (int i = 0; i < _socketViews.Count; i++)
            {
                var go = _socketViews[i];
                if (go == null) continue;

                bool active = i < _sockets.Count;
                go.SetActive(active);
                if (!active) continue;

                int c = i % cols;
                int r = i / cols;

                var rt = go.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin        = new Vector2(0f, 1f);
                    rt.anchorMax        = new Vector2(0f, 1f);
                    rt.pivot            = new Vector2(0f, 1f);
                    rt.sizeDelta        = new Vector2(SocketSize, SocketSize);
                    rt.anchoredPosition = new Vector2(
                         c * (SocketSize + SocketSpacing),
                        -r * (SocketSize + SocketSpacing)
                    );
                }

                var socketItem = go.GetComponent<SocketItem>();
                if (socketItem != null)
                    socketItem.SetSocket(_sockets[i].Color);
            }

            int totalRows = Mathf.CeilToInt((float)_sockets.Count / cols);
            float panelW  = cols * SocketSize + (cols - 1) * SocketSpacing;
            float panelH  = totalRows * SocketSize + (totalRows - 1) * SocketSpacing;

            _socketPanel.anchorMin        = new Vector2(0.5f, 0.5f);
            _socketPanel.anchorMax        = new Vector2(0.5f, 0.5f);
            _socketPanel.pivot            = new Vector2(0.5f, 0.5f);
            _socketPanel.anchoredPosition = Vector2.zero;
            _socketPanel.sizeDelta        = new Vector2(panelW, panelH);
            _socketPanel.gameObject.SetActive(true);

            // 将自身置顶，确保插槽面板不被同级的 ItemView 节点遮挡
            transform.SetAsLastSibling();
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
                if (bagData == null || !bagData.IsEquipment)
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
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                var rootCanvas = canvas.rootCanvas != null ? canvas.rootCanvas : canvas;
                if (rootCanvas.transform is RectTransform rootRt)
                    return rootRt;
            }

            return transform.parent as RectTransform ?? RT;
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

            float xFromLeft = itemRightXFromLeft + TipsOffset;
            if (xFromLeft + tipsWidth > parentWidth - TipsScreenPadding)
                xFromLeft = itemLeftXFromLeft - TipsOffset - tipsWidth;

            float maxXFromLeft = Mathf.Max(TipsScreenPadding, parentWidth - tipsWidth - TipsScreenPadding);
            float maxYFromTop = Mathf.Max(TipsScreenPadding, parentHeight - tipsHeight - TipsScreenPadding);

            xFromLeft = Mathf.Clamp(xFromLeft, TipsScreenPadding, maxXFromLeft);
            float yFromTop = Mathf.Clamp(itemTopYFromTop, TipsScreenPadding, maxYFromTop);

            tipsRt.anchorMin = new Vector2(0f, 1f);
            tipsRt.anchorMax = new Vector2(0f, 1f);
            tipsRt.pivot     = new Vector2(0f, 1f);
            tipsRt.anchoredPosition = new Vector2(xFromLeft, -yFromTop);
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