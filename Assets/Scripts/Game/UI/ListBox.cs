using System.Collections.Generic;
using UnityEngine;

namespace POELike.Game.UI
{
    /// <summary>兼容旧版的单轴排列方向</summary>
    public enum ArrangeDirection
    {
        /// <summary>从上到下（垂直列表）</summary>
        TopToBottom,
        /// <summary>从下到上</summary>
        BottomToTop,
        /// <summary>从左到右（水平列表）</summary>
        LeftToRight,
        /// <summary>从右到左</summary>
        RightToLeft,
    }

    /// <summary>主轴方向</summary>
    public enum ArrangeAxis
    {
        Vertical,
        Horizontal,
    }

    /// <summary>横向排列方向</summary>
    public enum HorizontalArrangeDirection
    {
        LeftToRight,
        RightToLeft,
    }

    /// <summary>纵向排列方向</summary>
    public enum VerticalArrangeDirection
    {
        TopToBottom,
        BottomToTop,
    }

    /// <summary>交叉轴对齐方式</summary>
    public enum CrossAxisAlignment
    {
        Start,
        Center,
        End,
    }

    /// <summary>
    /// ListBox —— 动态列表容器，挂载在父物体上。
    /// 通过 <see cref="AddItem"/> 按类型批量创建子条目，
    /// 子条目预制体需挂载继承自 <see cref="ListBoxItem"/> 的组件。
    /// </summary>
    public class ListBox : MonoBehaviour
    {
        [Header("子条目预制体数组（下标即类型 type）")]
        [SerializeField] private GameObject[] _itemPrefabs;

        [Header("主轴方向")]
        [SerializeField] private ArrangeAxis _primaryAxis = ArrangeAxis.Vertical;

        [Header("横向排列方向")]
        [SerializeField] private HorizontalArrangeDirection _horizontalDirection = HorizontalArrangeDirection.LeftToRight;

        [Header("纵向排列方向")]
        [SerializeField] private VerticalArrangeDirection _verticalDirection = VerticalArrangeDirection.TopToBottom;

        [Header("是否启用自动换行 / 换列")]
        [SerializeField] private bool _wrapItems = false;

        [Header("交叉轴对齐方式")]
        [SerializeField] private CrossAxisAlignment _crossAxisAlignment = CrossAxisAlignment.Center;

        [Header("条目间隔 (Left / Right / Bottom / Top)")]
        [SerializeField] private float _spacingLeft   = 0f;
        [SerializeField] private float _spacingRight  = 0f;
        [SerializeField] private float _spacingBottom = 0f;
        [SerializeField] private float _spacingTop    = 0f;

        [Header("整体 Padding (Left / Right / Bottom / Top)")]
        [SerializeField] private float _paddingLeft   = 0f;
        [SerializeField] private float _paddingRight  = 0f;
        [SerializeField] private float _paddingBottom = 0f;
        [SerializeField] private float _paddingTop    = 0f;

        [Header("是否在交叉轴拉伸条目")]
        [SerializeField] private bool _stretchItemsOnCrossAxis = true;

        [SerializeField, HideInInspector] private ArrangeDirection _direction = ArrangeDirection.TopToBottom;
        [SerializeField, HideInInspector] private bool _layoutSettingsInitialized;

        // ── 内部状态 ──────────────────────────────────────────────────
        private readonly List<ListBoxItem> _items = new List<ListBoxItem>();
        private int _nextIndex = 0;

        private sealed class LayoutEntry
        {
            public RectTransform RectTransform;
            public float Width;
            public float Height;
            public float PrimarySize;
            public float CrossSize;
        }

        private sealed class LayoutLine
        {
            public readonly List<LayoutEntry> Entries = new List<LayoutEntry>();
            public float PrimarySpan;
            public float CrossSpan;
        }

        // ── 公开属性（运行时动态修改后调用 RefreshLayout）────────────

        public ArrangeAxis PrimaryAxis
        {
            get => _primaryAxis;
            set
            {
                if (_primaryAxis == value)
                    return;

                _primaryAxis = value;
                _layoutSettingsInitialized = true;
                SyncLegacyDirection();
                RefreshLayout();
            }
        }

        public HorizontalArrangeDirection HorizontalDirection
        {
            get => _horizontalDirection;
            set
            {
                if (_horizontalDirection == value)
                    return;

                _horizontalDirection = value;
                _layoutSettingsInitialized = true;
                SyncLegacyDirection();
                RefreshLayout();
            }
        }

        public VerticalArrangeDirection VerticalDirection
        {
            get => _verticalDirection;
            set
            {
                if (_verticalDirection == value)
                    return;

                _verticalDirection = value;
                _layoutSettingsInitialized = true;
                SyncLegacyDirection();
                RefreshLayout();
            }
        }

        public bool WrapItems
        {
            get => _wrapItems;
            set
            {
                if (_wrapItems == value)
                    return;

                _wrapItems = value;
                _layoutSettingsInitialized = true;
                SyncLegacyDirection();
                RefreshLayout();
            }
        }

        public CrossAxisAlignment ItemCrossAxisAlignment
        {
            get => _crossAxisAlignment;
            set
            {
                if (_crossAxisAlignment == value)
                    return;

                _crossAxisAlignment = value;
                _layoutSettingsInitialized = true;
                SyncLegacyDirection();
                RefreshLayout();
            }
        }

        public ArrangeDirection Direction
        {
            get
            {
                EnsureLayoutSettingsInitialized();
                return BuildLegacyDirection();
            }
            set
            {
                _layoutSettingsInitialized = true;
                ApplyLegacyDirection(value);
                SyncLegacyDirection();
                RefreshLayout();
            }
        }

        public bool StretchItemsOnCrossAxis
        {
            get => _stretchItemsOnCrossAxis;
            set
            {
                if (_stretchItemsOnCrossAxis == value)
                    return;

                _stretchItemsOnCrossAxis = value;
                _layoutSettingsInitialized = true;
                SyncLegacyDirection();
                RefreshLayout();
            }
        }

        /// <summary>条目间隔（Left/Right/Bottom/Top）</summary>
        public void SetSpacing(float left, float right, float bottom, float top)
        {
            _spacingLeft   = left;
            _spacingRight  = right;
            _spacingBottom = bottom;
            _spacingTop    = top;
            _layoutSettingsInitialized = true;
            SyncLegacyDirection();
            RefreshLayout();
        }

        /// <summary>整体 Padding（Left/Right/Bottom/Top）</summary>
        public void SetPadding(float left, float right, float bottom, float top)
        {
            _paddingLeft   = left;
            _paddingRight  = right;
            _paddingBottom = bottom;
            _paddingTop    = top;
            _layoutSettingsInitialized = true;
            SyncLegacyDirection();
            RefreshLayout();
        }

        // ── 公开接口 ──────────────────────────────────────────────────

        /// <summary>
        /// 按类型和数量批量创建子条目，挂载在本物体下。
        /// </summary>
        /// <param name="type">预制体类型，对应 _itemPrefabs 数组下标</param>
        /// <param name="count">创建数量</param>
        public void AddItem(int type, int count)
        {
            if (_itemPrefabs == null || type < 0 || type >= _itemPrefabs.Length)
            {
                Debug.LogWarning($"[ListBox] 类型 {type} 超出预制体数组范围，请检查 _itemPrefabs 配置。");
                return;
            }

            var prefab = _itemPrefabs[type];
            if (prefab == null)
            {
                Debug.LogWarning($"[ListBox] 类型 {type} 对应的预制体为空。");
                return;
            }

            for (int i = 0; i < count; i++)
            {
                var go   = Instantiate(prefab, transform);
                var item = go.GetComponent<ListBoxItem>();

                if (item == null)
                {
                    // 预制体上没有 ListBoxItem 组件时自动添加，保证能被 ListBox 管理
                    item = go.AddComponent<ListBoxItem>();
                }

                item.Setup(_nextIndex++, type);
                _items.Add(item);

                // 触发初始化回调
                item.OnItemInit();
                // 默认激活显示
                item.OnItemShow();
            }

            RefreshLayout();
        }

        /// <summary>隐藏指定索引的条目（不销毁）</summary>
        public void HideItem(int index)
        {
            var item = GetItemByIndex(index);
            if (item == null) return;
            item.gameObject.SetActive(false);
            item.OnItemHide();
            RefreshLayout();
        }

        /// <summary>显示指定索引的条目</summary>
        public void ShowItem(int index)
        {
            var item = GetItemByIndex(index);
            if (item == null) return;
            item.gameObject.SetActive(true);
            item.OnItemShow();
            RefreshLayout();
        }

        /// <summary>销毁指定索引的条目</summary>
        public void RemoveItem(int index)
        {
            var item = GetItemByIndex(index);
            if (item == null) return;
            _items.Remove(item);
            Destroy(item.gameObject);
            RefreshLayout();
        }

        /// <summary>清空所有条目</summary>
        public void Clear()
        {
            foreach (var item in _items)
            {
                if (item != null)
                    Destroy(item.gameObject);
            }
            _items.Clear();
            _nextIndex = 0;
        }

        /// <summary>获取当前条目总数</summary>
        public int Count => _items.Count;

        /// <summary>通过索引获取 ListBoxItem</summary>
        public ListBoxItem GetItemByIndex(int index)
        {
            return _items.Find(item => item != null && item.GetIndex() == index);
        }

        // ── 布局 ──────────────────────────────────────────────────────

        /// <summary>
        /// 根据主轴、横纵方向、Spacing 和 Padding 重新计算所有可见条目的位置。
        /// 仅处理激活状态的条目，隐藏条目不参与布局计算。
        /// </summary>
        public void RefreshLayout()
        {
            EnsureLayoutSettingsInitialized();

            bool primaryIsVertical = _primaryAxis == ArrangeAxis.Vertical;
            bool leftToRight       = _horizontalDirection == HorizontalArrangeDirection.LeftToRight;
            bool topToBottom       = _verticalDirection == VerticalArrangeDirection.TopToBottom;

            var entries = CollectVisibleEntries(primaryIsVertical);
            if (entries.Count == 0)
                return;

            var rectTransform = GetComponent<RectTransform>();
            float availablePrimary = primaryIsVertical
                ? GetAvailableHeight(rectTransform)
                : GetAvailableWidth(rectTransform);
            float availableCross = primaryIsVertical
                ? GetAvailableWidth(rectTransform)
                : GetAvailableHeight(rectTransform);

            bool wrapItems = _wrapItems && rectTransform != null && availablePrimary > 0f;
            bool stretchOnCrossAxis = _stretchItemsOnCrossAxis && !wrapItems;

            var lines = BuildLayoutLines(entries, primaryIsVertical, wrapItems, availablePrimary);
            PrepareLineCrossSpan(lines, wrapItems, stretchOnCrossAxis, availableCross);

            float secondaryCursor = primaryIsVertical
                ? (leftToRight ? _paddingLeft : _paddingRight)
                : (topToBottom ? _paddingTop : _paddingBottom);

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];

                if (i > 0)
                    secondaryCursor += primaryIsVertical ? _spacingLeft : _spacingTop;

                LayoutEntriesInLine(line, primaryIsVertical, stretchOnCrossAxis, secondaryCursor, leftToRight, topToBottom);

                secondaryCursor += line.CrossSpan + (primaryIsVertical ? _spacingRight : _spacingBottom);
            }
        }

        private List<LayoutEntry> CollectVisibleEntries(bool primaryIsVertical)
        {
            var entries = new List<LayoutEntry>(_items.Count);
            foreach (var item in _items)
            {
                if (item == null || !item.gameObject.activeSelf)
                    continue;

                var rt = item.GetCtrl().GetComponent<RectTransform>();
                if (rt == null)
                    continue;

                float width = rt.rect.width;
                float height = rt.rect.height;
                entries.Add(new LayoutEntry
                {
                    RectTransform = rt,
                    Width = width,
                    Height = height,
                    PrimarySize = primaryIsVertical ? height : width,
                    CrossSize = primaryIsVertical ? width : height,
                });
            }

            return entries;
        }

        private List<LayoutLine> BuildLayoutLines(List<LayoutEntry> entries, bool primaryIsVertical, bool wrapItems, float availablePrimary)
        {
            var lines = new List<LayoutLine>();
            var currentLine = new LayoutLine();

            float primaryPrefix = primaryIsVertical ? _spacingTop : _spacingLeft;
            float primarySuffix = primaryIsVertical ? _spacingBottom : _spacingRight;

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];

                float requiredSpan = (currentLine.Entries.Count > 0 ? primaryPrefix : 0f)
                    + entry.PrimarySize
                    + primarySuffix;

                if (wrapItems
                    && currentLine.Entries.Count > 0
                    && currentLine.PrimarySpan + requiredSpan > availablePrimary)
                {
                    lines.Add(currentLine);
                    currentLine = new LayoutLine();
                    requiredSpan = entry.PrimarySize + primarySuffix;
                }

                currentLine.Entries.Add(entry);
                currentLine.PrimarySpan += requiredSpan;
                currentLine.CrossSpan = Mathf.Max(currentLine.CrossSpan, entry.CrossSize);
            }

            if (currentLine.Entries.Count > 0)
                lines.Add(currentLine);

            return lines;
        }

        private void PrepareLineCrossSpan(List<LayoutLine> lines, bool wrapItems, bool stretchOnCrossAxis, float availableCross)
        {
            if (lines.Count == 0 || availableCross <= 0f)
                return;

            if (stretchOnCrossAxis || !wrapItems)
            {
                for (int i = 0; i < lines.Count; i++)
                    lines[i].CrossSpan = availableCross;
            }
        }

        private void LayoutEntriesInLine(LayoutLine line, bool primaryIsVertical, bool stretchOnCrossAxis, float secondaryCursor, bool leftToRight, bool topToBottom)
        {
            if (primaryIsVertical)
                LayoutColumn(line, stretchOnCrossAxis, secondaryCursor, leftToRight, topToBottom);
            else
                LayoutRow(line, stretchOnCrossAxis, secondaryCursor, leftToRight, topToBottom);
        }

        private void LayoutColumn(LayoutLine line, bool stretchOnCrossAxis, float columnCursor, bool leftToRight, bool topToBottom)
        {
            float primaryCursor = topToBottom ? _paddingTop : _paddingBottom;

            for (int i = 0; i < line.Entries.Count; i++)
            {
                var entry = line.Entries[i];

                if (i > 0)
                    primaryCursor += _spacingTop;

                float y = topToBottom ? -primaryCursor : primaryCursor;
                if (stretchOnCrossAxis)
                {
                    ApplyVerticalStretchLayout(entry.RectTransform, y, topToBottom);
                }
                else
                {
                    float xOffset = ResolveAlignmentOffset(line.CrossSpan, entry.Width);
                    float x = leftToRight
                        ? columnCursor + xOffset
                        : -(columnCursor + xOffset);

                    ApplyFixedLayout(entry.RectTransform, x, y, leftToRight, topToBottom);
                }

                primaryCursor += entry.Height + _spacingBottom;
            }
        }

        private void LayoutRow(LayoutLine line, bool stretchOnCrossAxis, float rowCursor, bool leftToRight, bool topToBottom)
        {
            float primaryCursor = leftToRight ? _paddingLeft : _paddingRight;

            for (int i = 0; i < line.Entries.Count; i++)
            {
                var entry = line.Entries[i];

                if (i > 0)
                    primaryCursor += _spacingLeft;

                float x = leftToRight ? primaryCursor : -primaryCursor;
                if (stretchOnCrossAxis)
                {
                    ApplyHorizontalStretchLayout(entry.RectTransform, x, leftToRight);
                }
                else
                {
                    float yOffset = ResolveAlignmentOffset(line.CrossSpan, entry.Height);
                    float y = topToBottom
                        ? -(rowCursor + yOffset)
                        : (rowCursor + yOffset);

                    ApplyFixedLayout(entry.RectTransform, x, y, leftToRight, topToBottom);
                }

                primaryCursor += entry.Width + _spacingRight;
            }
        }

        private float ResolveAlignmentOffset(float availableSpan, float itemSpan)
        {
            float remaining = Mathf.Max(0f, availableSpan - itemSpan);
            return _crossAxisAlignment switch
            {
                CrossAxisAlignment.End => remaining,
                CrossAxisAlignment.Center => remaining * 0.5f,
                _ => 0f,
            };
        }

        private void EnsureLayoutSettingsInitialized()
        {
            if (!_layoutSettingsInitialized)
            {
                ApplyLegacyDirection(_direction);
                _layoutSettingsInitialized = true;
            }

            SyncLegacyDirection();
        }

        private void ApplyLegacyDirection(ArrangeDirection direction)
        {
            switch (direction)
            {
                case ArrangeDirection.BottomToTop:
                    _primaryAxis = ArrangeAxis.Vertical;
                    _verticalDirection = VerticalArrangeDirection.BottomToTop;
                    break;
                case ArrangeDirection.LeftToRight:
                    _primaryAxis = ArrangeAxis.Horizontal;
                    _horizontalDirection = HorizontalArrangeDirection.LeftToRight;
                    break;
                case ArrangeDirection.RightToLeft:
                    _primaryAxis = ArrangeAxis.Horizontal;
                    _horizontalDirection = HorizontalArrangeDirection.RightToLeft;
                    break;
                default:
                    _primaryAxis = ArrangeAxis.Vertical;
                    _verticalDirection = VerticalArrangeDirection.TopToBottom;
                    break;
            }
        }

        private ArrangeDirection BuildLegacyDirection()
        {
            return _primaryAxis == ArrangeAxis.Vertical
                ? (_verticalDirection == VerticalArrangeDirection.TopToBottom
                    ? ArrangeDirection.TopToBottom
                    : ArrangeDirection.BottomToTop)
                : (_horizontalDirection == HorizontalArrangeDirection.LeftToRight
                    ? ArrangeDirection.LeftToRight
                    : ArrangeDirection.RightToLeft);
        }

        private void SyncLegacyDirection()
        {
            _direction = BuildLegacyDirection();
        }

        private float GetAvailableWidth(RectTransform rectTransform)
        {
            if (rectTransform == null)
                return 0f;

            return Mathf.Max(0f, rectTransform.rect.width - _paddingLeft - _paddingRight);
        }

        private float GetAvailableHeight(RectTransform rectTransform)
        {
            if (rectTransform == null)
                return 0f;

            return Mathf.Max(0f, rectTransform.rect.height - _paddingTop - _paddingBottom);
        }

        private static void ApplyFixedLayout(RectTransform rt, float x, float y, bool leftToRight, bool topToBottom)
        {
            float anchorX = leftToRight ? 0f : 1f;
            float anchorY = topToBottom ? 1f : 0f;

            rt.anchorMin = new Vector2(anchorX, anchorY);
            rt.anchorMax = new Vector2(anchorX, anchorY);
            rt.pivot = new Vector2(anchorX, anchorY);
            rt.anchoredPosition = new Vector2(x, y);
        }

        private static void ApplyVerticalStretchLayout(RectTransform rt, float y, bool topToBottom)
        {
            float anchorY = topToBottom ? 1f : 0f;

            rt.anchorMin = new Vector2(0f, anchorY);
            rt.anchorMax = new Vector2(1f, anchorY);
            rt.pivot = new Vector2(0.5f, anchorY);
            rt.anchoredPosition = new Vector2(0f, y);
        }

        private static void ApplyHorizontalStretchLayout(RectTransform rt, float x, bool leftToRight)
        {
            float anchorX = leftToRight ? 0f : 1f;

            rt.anchorMin = new Vector2(anchorX, 0f);
            rt.anchorMax = new Vector2(anchorX, 1f);
            rt.pivot = new Vector2(anchorX, 0.5f);
            rt.anchoredPosition = new Vector2(x, 0f);
        }

        private void Reset()
        {
            _layoutSettingsInitialized = true;
            SyncLegacyDirection();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            EnsureLayoutSettingsInitialized();

            // Inspector 中修改参数时实时预览布局
            if (Application.isPlaying)
                RefreshLayout();
        }
#endif
    }
}
