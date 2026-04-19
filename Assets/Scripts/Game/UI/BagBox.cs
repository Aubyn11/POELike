using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace POELike.Game.UI
{
    /// <summary>
    /// 背包容器
    /// 管理 Cols × Rows 个 BagCell 格子，支持放置/移除任意尺寸（宽×高）的道具。
    /// 挂载在背包面板根节点上，格子预制体需挂载 <see cref="BagCell"/> 组件。
    /// </summary>
    public class BagBox : MonoBehaviour
    {
        // ── 序列化字段 ────────────────────────────────────────────────

        [Header("格子预制体（需挂载 BagCell 组件）")]
        [SerializeField] private GameObject _cellPrefab;

        [Header("背包尺寸")]
        [SerializeField] private int _cols = 12;
        [SerializeField] private int _rows = 5;

        [Header("格子尺寸与间距")]
        [SerializeField] private float _cellSize    = 50f;
        [SerializeField] private float _cellSpacing = 2f;

        [Header("背包容器（格子的父节点，不填则使用自身）")]
        [SerializeField] private RectTransform _gridRoot;

        // ── 运行时数据 ────────────────────────────────────────────────

        /// <summary>二维格子数组 [col, row]</summary>
        private BagCell[,] _cells;

        /// <summary>当前背包中所有已放置的道具</summary>
        private readonly List<BagItemData> _items = new List<BagItemData>();

        /// <summary>道具对应的显示 GameObject（图标层）</summary>
        private readonly Dictionary<BagItemData, GameObject> _itemViews
            = new Dictionary<BagItemData, GameObject>();

        // ── 事件 ──────────────────────────────────────────────────────

        /// <summary>道具被放置时触发（item, col, row）</summary>
        public event Action<BagItemData, int, int> OnItemPlaced;

        /// <summary>道具被移除时触发（item）</summary>
        public event Action<BagItemData> OnItemRemoved;

        /// <summary>格子被点击时触发（cell, eventData）</summary>
        public event Action<BagCell, PointerEventData> OnCellClicked;

        // ── 属性 ──────────────────────────────────────────────────────

        public int Cols => _cols;
        public int Rows => _rows;

        /// <summary>单个格子像素尺寸</summary>
        public float CellSize => _cellSize;

        /// <summary>格子间距</summary>
        public float CellSpacing => _cellSpacing;

        /// <summary>格子的实际父节点（_gridRoot 不为空时返回 _gridRoot，否则返回自身 transform）</summary>
        public RectTransform GridRoot => _gridRoot != null ? _gridRoot : (RectTransform)transform;

        /// <summary>当前背包中的道具列表（只读副本）</summary>
        public IReadOnlyList<BagItemData> Items => _items;

        /// <summary>
        /// 是否自动创建 ItemView 图标节点（默认 true）。
        /// 当外部自行管理道具视图（如 ShopPanel 使用 Equipment 预制体）时，设为 false 可避免重复创建节点。
        /// </summary>
        public bool AutoCreateItemView { get; set; } = true;

        // ── 初始化 ────────────────────────────────────────────────────

        private void Awake()
        {
            EnsureGridBuilt();
        }

        /// <summary>
        /// 确保格子网格已构建。仅当当前网格不存在或尺寸变化时才重建。
        /// </summary>
        public void EnsureGridBuilt()
        {
            if (_cells != null &&
                _cells.GetLength(0) == _cols &&
                _cells.GetLength(1) == _rows &&
                _cells[0, 0] != null)
            {
                return;
            }

            BuildGrid();
        }

        /// <summary>
        /// 构建格子网格，销毁旧格子后重新生成
        /// </summary>
        public void BuildGrid()
        {
            // 清理旧道具视图和占用状态，避免重建网格时残留对象
            ClearItems();

            // 清理旧格子
            if (_cells != null)
            {
                foreach (var cell in _cells)
                    if (cell != null) Destroy(cell.gameObject);
            }
            _cells = new BagCell[_cols, _rows];

            var root = _gridRoot != null ? _gridRoot : (RectTransform)transform;

            for (int row = 0; row < _rows; row++)
            {
                for (int col = 0; col < _cols; col++)
                {
                    var go   = Instantiate(_cellPrefab, root);
                    var cell = go.GetComponent<BagCell>();
                    if (cell == null)
                    {
                        Debug.LogError("[BagBox] 格子预制体上未找到 BagCell 组件！");
                        Destroy(go);
                        continue;
                    }

                    cell.Setup(col, row, this);

                    // 定位格子
                    var rt = go.GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0f, 1f);
                    rt.anchorMax = new Vector2(0f, 1f);
                    rt.pivot     = new Vector2(0f, 1f);
                    rt.sizeDelta = new Vector2(_cellSize, _cellSize);
                    rt.anchoredPosition = new Vector2(
                        col * (_cellSize + _cellSpacing),
                       -row * (_cellSize + _cellSpacing)
                    );

                    _cells[col, row] = cell;
                }
            }

            // 自动调整容器尺寸
            var rootRt = root;
            rootRt.sizeDelta = new Vector2(
                _cols * _cellSize + (_cols - 1) * _cellSpacing,
                _rows * _cellSize + (_rows - 1) * _cellSpacing
            );
        }

        // ── 道具放置 ──────────────────────────────────────────────────

        /// <summary>
        /// 尝试将道具放置到指定位置（左上角格子坐标）。
        /// </summary>
        /// <param name="item">要放置的道具数据</param>
        /// <param name="col">左上角列（从 0 开始）</param>
        /// <param name="row">左上角行（从 0 开始）</param>
        /// <returns>放置成功返回 true</returns>
        public bool TryPlaceItem(BagItemData item, int col, int row)
        {
            if (item == null) return false;
            if (!CanPlaceItem(item, col, row)) return false;

            // 若道具已在背包中，先移除旧位置
            if (item.IsPlaced)
                RemoveItemFromCells(item);

            // 更新道具位置
            item.GridCol = col;
            item.GridRow = row;

            // 标记所有占用格子
            OccupyCells(item, col, row);

            if (!_items.Contains(item))
                _items.Add(item);

            // 创建/更新道具视图（外部自行管理视图时可关闭）
            if (AutoCreateItemView)
                RefreshItemView(item);

            OnItemPlaced?.Invoke(item, col, row);
            return true;
        }

        /// <summary>
        /// 自动寻找第一个可放置的位置并放置道具。
        /// </summary>
        /// <returns>放置成功返回 true</returns>
        public bool TryAutoPlaceItem(BagItemData item)
        {
            if (item == null) return false;

            for (int row = 0; row <= _rows - item.GridHeight; row++)
            {
                for (int col = 0; col <= _cols - item.GridWidth; col++)
                {
                    if (CanPlaceItem(item, col, row))
                        return TryPlaceItem(item, col, row);
                }
            }

            Debug.LogWarning($"[BagBox] 背包已满，无法放置道具：{item.Name}");
            return false;
        }

        /// <summary>
        /// 清空所有道具和视图，但保留格子网格（切换页签时使用，避免重建格子）
        /// </summary>
        public void ClearItems()
        {
            // 销毁所有道具视图
            foreach (var kv in _itemViews)
                if (kv.Value != null) Destroy(kv.Value);
            _itemViews.Clear();

            // 清空格子占用状态
            if (_cells != null)
                foreach (var cell in _cells)
                    cell?.ClearOccupied();

            _items.Clear();
        }

        /// <summary>
        /// 移除指定道具（从背包中取出）
        /// </summary>
        public bool RemoveItem(BagItemData item)
        {
            if (item == null || !_items.Contains(item)) return false;

            RemoveItemFromCells(item);
            _items.Remove(item);

            // 销毁视图
            if (_itemViews.TryGetValue(item, out var view))
            {
                if (view != null) Destroy(view);
                _itemViews.Remove(item);
            }

            item.GridCol = -1;
            item.GridRow = -1;

            OnItemRemoved?.Invoke(item);
            return true;
        }

        /// <summary>
        /// 移除指定格子上的道具
        /// </summary>
        public bool RemoveItemAt(int col, int row)
        {
            var cell = GetCell(col, row);
            if (cell == null || cell.IsEmpty) return false;
            return RemoveItem(cell.OccupiedItem);
        }

        // ── 查询 ──────────────────────────────────────────────────────

        /// <summary>
        /// 检查道具能否放置到指定位置（不越界、不重叠）
        /// </summary>
        public bool CanPlaceItem(BagItemData item, int col, int row)
        {
            if (item == null) return false;
            if (col < 0 || row < 0) return false;
            if (col + item.GridWidth  > _cols) return false;
            if (row + item.GridHeight > _rows) return false;

            for (int r = row; r < row + item.GridHeight; r++)
            {
                for (int c = col; c < col + item.GridWidth; c++)
                {
                    var cell = _cells[c, r];
                    if (cell == null) return false;
                    // 允许道具覆盖自身原来的格子（移动场景）
                    if (!cell.IsEmpty && cell.OccupiedItem != item)
                        return false;
                }
            }
            return true;
        }

        public bool CanPlaceOrReplaceItem(BagItemData item, int col, int row)
        {
            return CanPlaceItem(item, col, row) ||
                   TryGetReplaceCandidate(item, col, row, out _);
        }

        public bool TryGetReplaceCandidate(BagItemData item, int col, int row, out BagItemData replacedItem)
        {
            replacedItem = null;

            if (item == null) return false;
            if (col < 0 || row < 0) return false;
            if (col + item.GridWidth  > _cols) return false;
            if (row + item.GridHeight > _rows) return false;

            BagItemData candidate = null;

            for (int r = row; r < row + item.GridHeight; r++)
            {
                for (int c = col; c < col + item.GridWidth; c++)
                {
                    var cell = _cells[c, r];
                    if (cell == null)
                        return false;

                    var occupiedItem = cell.OccupiedItem;
                    if (occupiedItem == null || occupiedItem == item)
                        continue;

                    if (candidate == null)
                    {
                        candidate = occupiedItem;
                        continue;
                    }

                    if (candidate != occupiedItem)
                        return false;
                }
            }

            if (candidate == null)
                return false;

            replacedItem = candidate;
            return true;
        }

        /// <summary>
        /// 获取指定坐标的格子
        /// </summary>
        public BagCell GetCell(int col, int row)
        {
            if (col < 0 || col >= _cols || row < 0 || row >= _rows) return null;
            return _cells[col, row];
        }

        /// <summary>
        /// 获取指定格子上的道具（null = 空）
        /// </summary>
        public BagItemData GetItemAt(int col, int row)
        {
            return GetCell(col, row)?.OccupiedItem;
        }

        /// <summary>
        /// 背包是否还有空间放置指定道具
        /// </summary>
        public bool HasSpaceFor(BagItemData item)
        {
            if (item == null) return false;
            for (int row = 0; row <= _rows - item.GridHeight; row++)
                for (int col = 0; col <= _cols - item.GridWidth; col++)
                    if (CanPlaceItem(item, col, row)) return true;
            return false;
        }

        public bool TryGetAutoPlacementPositions(BagItemData item, int requiredPlacementCount, out List<Vector2Int> positions)
        {
            positions = new List<Vector2Int>();
            if (item == null || requiredPlacementCount <= 0)
                return false;

            EnsureGridBuilt();

            var occupiedSnapshot = new bool[_cols, _rows];
            for (int row = 0; row < _rows; row++)
            {
                for (int col = 0; col < _cols; col++)
                {
                    occupiedSnapshot[col, row] = _cells[col, row] != null && !_cells[col, row].IsEmpty;
                }
            }

            for (int placementIndex = 0; placementIndex < requiredPlacementCount; placementIndex++)
            {
                if (!TryFindPlacementInSnapshot(item, occupiedSnapshot, out int col, out int row))
                {
                    positions.Clear();
                    return false;
                }

                positions.Add(new Vector2Int(col, row));
                OccupySnapshot(item, occupiedSnapshot, col, row);
            }

            return true;
        }

        /// <summary>
        /// 根据屏幕坐标获取背包中的格子坐标。
        /// </summary>

        public bool TryGetCellCoordFromScreenPoint(Vector2 screenPoint, Camera eventCamera, out int col, out int row)
        {
            col = -1;
            row = -1;

            var root = GridRoot;
            if (root == null)
                return false;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenPoint, eventCamera, out var localPoint))
                return false;

            var rect = root.rect;
            float xFromLeft = localPoint.x + rect.width * root.pivot.x;
            float yFromTop  = rect.height * (1f - root.pivot.y) - localPoint.y;

            if (xFromLeft < 0f || yFromTop < 0f)
                return false;

            float step = _cellSize + _cellSpacing;
            col = Mathf.FloorToInt(xFromLeft / step);
            row = Mathf.FloorToInt(yFromTop  / step);

            if (col < 0 || col >= _cols || row < 0 || row >= _rows)
            {
                col = -1;
                row = -1;
                return false;
            }

            float xInCell = xFromLeft - col * step;
            float yInCell = yFromTop  - row * step;
            if (xInCell >= _cellSize || yInCell >= _cellSize)
            {
                col = -1;
                row = -1;
                return false;
            }

            return true;
        }

        // ── 高亮预览 ──────────────────────────────────────────────────

        /// <summary>
        /// 高亮显示道具放置预览区域
        /// </summary>
        /// <param name="item">要预览的道具</param>
        /// <param name="col">左上角列</param>
        /// <param name="row">左上角行</param>
        public void ShowPlacementPreview(BagItemData item, int col, int row)
        {
            ClearAllHighlights();
            if (item == null) return;

            bool canPlace = CanPlaceOrReplaceItem(item, col, row);

            int endCol = Mathf.Min(col + item.GridWidth,  _cols);
            int endRow = Mathf.Min(row + item.GridHeight, _rows);

            for (int r = Mathf.Max(0, row); r < endRow; r++)
                for (int c = Mathf.Max(0, col); c < endCol; c++)
                    _cells[c, r]?.SetHighlight(canPlace);
        }

        /// <summary>
        /// 清除所有格子的高亮状态
        /// </summary>
        public void ClearAllHighlights()
        {
            if (_cells == null) return;
            foreach (var cell in _cells)
                cell?.ClearHighlight();
        }

        // ── 内部辅助 ──────────────────────────────────────────────────

        private void OccupyCells(BagItemData item, int col, int row)
        {
            for (int r = row; r < row + item.GridHeight; r++)
                for (int c = col; c < col + item.GridWidth; c++)
                    _cells[c, r]?.SetOccupied(item);
        }

        private bool TryFindPlacementInSnapshot(BagItemData item, bool[,] occupiedSnapshot, out int foundCol, out int foundRow)
        {
            foundCol = -1;
            foundRow = -1;

            if (item == null || occupiedSnapshot == null)
                return false;

            for (int row = 0; row <= _rows - item.GridHeight; row++)
            {
                for (int col = 0; col <= _cols - item.GridWidth; col++)
                {
                    if (!CanPlaceInSnapshot(item, occupiedSnapshot, col, row))
                        continue;

                    foundCol = col;
                    foundRow = row;
                    return true;
                }
            }

            return false;
        }

        private bool CanPlaceInSnapshot(BagItemData item, bool[,] occupiedSnapshot, int col, int row)
        {
            if (item == null || occupiedSnapshot == null)
                return false;
            if (col < 0 || row < 0)
                return false;
            if (col + item.GridWidth > _cols || row + item.GridHeight > _rows)
                return false;

            for (int r = row; r < row + item.GridHeight; r++)
            {
                for (int c = col; c < col + item.GridWidth; c++)
                {
                    if (occupiedSnapshot[c, r])
                        return false;
                }
            }

            return true;
        }

        private static void OccupySnapshot(BagItemData item, bool[,] occupiedSnapshot, int col, int row)
        {
            if (item == null || occupiedSnapshot == null)
                return;

            for (int r = row; r < row + item.GridHeight; r++)
            {
                for (int c = col; c < col + item.GridWidth; c++)
                    occupiedSnapshot[c, r] = true;
            }
        }

        private void RemoveItemFromCells(BagItemData item)

        {
            if (!item.IsPlaced) return;
            for (int r = item.GridRow; r < item.GridRow + item.GridHeight; r++)
                for (int c = item.GridCol; c < item.GridCol + item.GridWidth; c++)
                {
                    var cell = GetCell(c, r);
                    if (cell != null && cell.OccupiedItem == item)
                        cell.ClearOccupied();
                }
        }

        /// <summary>
        /// 创建或刷新道具的图标视图（覆盖在格子上方）
        /// </summary>
        private void RefreshItemView(BagItemData item)
        {
            var root = _gridRoot != null ? _gridRoot : (RectTransform)transform;

            // 若已有视图则复用，否则新建
            if (!_itemViews.TryGetValue(item, out var viewGo) || viewGo == null)
            {
                viewGo = new GameObject($"ItemView_{item.ItemId}", typeof(RectTransform), typeof(Image));
                viewGo.transform.SetParent(root, false);
                _itemViews[item] = viewGo;
            }

            var rt = viewGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 1f);

            float w = item.GridWidth  * _cellSize + (item.GridWidth  - 1) * _cellSpacing;
            float h = item.GridHeight * _cellSize + (item.GridHeight - 1) * _cellSpacing;
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(
                item.GridCol * (_cellSize + _cellSpacing),
               -item.GridRow * (_cellSize + _cellSpacing)
            );

            // 设置图标和颜色
            var img = viewGo.GetComponent<Image>();
            img.color  = item.ItemColor;
            img.sprite = item.Icon;
            if (item.Icon == null)
                img.color = new Color(item.ItemColor.r, item.ItemColor.g, item.ItemColor.b, 0.6f);

            // 道具视图层级置顶（显示在格子上方）
            viewGo.transform.SetAsLastSibling();
        }

        // ── BagCell 回调（由 BagCell 调用）──────────────────────────

        internal void OnCellPointerEnter(BagCell cell)
        {
            var draggingItem = BagItemView.CurrentDraggingItem;
            if (draggingItem == null || draggingItem.Data == null)
                return;

            ShowPlacementPreview(draggingItem.Data, cell.Col, cell.Row);
        }

        internal void OnCellPointerExit(BagCell cell)
        {
            if (BagItemView.CurrentDraggingItem != null)
                ClearAllHighlights();
        }

        internal void OnCellClick(BagCell cell, PointerEventData eventData)
        {
            var movingItem = BagItemView.CurrentDraggingItem;
            if (movingItem != null && movingItem.Data != null)
            {
                if (!movingItem.TryDropToBag(this, cell.Col, cell.Row, eventData))
                    ShowPlacementPreview(movingItem.Data, cell.Col, cell.Row);
                else
                    ClearAllHighlights();

                eventData?.Use();
                return;
            }

            OnCellClicked?.Invoke(cell, eventData);
        }

        internal void OnCellDrop(BagCell cell, PointerEventData eventData)
        {
            var draggingItem = BagItemView.CurrentDraggingItem;
            if (draggingItem == null || draggingItem.Data == null)
                return;

            if (!draggingItem.TryDropToBag(this, cell.Col, cell.Row, eventData))
                ShowPlacementPreview(draggingItem.Data, cell.Col, cell.Row);
            else
                ClearAllHighlights();
        }

        private void OnDisable()
        {
            ClearAllHighlights();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _cols = Mathf.Max(1, _cols);
            _rows = Mathf.Max(1, _rows);
            _cellSize    = Mathf.Max(1f, _cellSize);
            _cellSpacing = Mathf.Max(0f, _cellSpacing);
        }
#endif
    }
}