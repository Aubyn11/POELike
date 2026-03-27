using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace POELike.Game.UI
{
    /// <summary>
    /// 背包单个格子
    /// 负责显示格子状态（空/占用/高亮），以及接收拖拽事件。
    /// 挂载在格子预制体根节点上。
    /// </summary>
    public class BagCell : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerClickHandler,
        IDropHandler
    {
        // ── 序列化字段 ────────────────────────────────────────────────

        [Header("格子背景图")]
        [SerializeField] private Image _bgImage;

        [Header("格子颜色配置")]
        [SerializeField] private Color _colorEmpty    = new Color(0.15f, 0.15f, 0.15f, 0.8f);
        [SerializeField] private Color _colorOccupied = new Color(0.25f, 0.25f, 0.25f, 0.8f);
        [SerializeField] private Color _colorHover    = new Color(0.40f, 0.40f, 0.40f, 0.9f);
        [SerializeField] private Color _colorValid    = new Color(0.20f, 0.60f, 0.20f, 0.8f);
        [SerializeField] private Color _colorInvalid  = new Color(0.60f, 0.20f, 0.20f, 0.8f);

        // ── 运行时状态 ────────────────────────────────────────────────

        /// <summary>格子所在列（由 BagBox 初始化）</summary>
        public int Col { get; private set; }

        /// <summary>格子所在行（由 BagBox 初始化）</summary>
        public int Row { get; private set; }

        /// <summary>当前占用此格子的道具（null = 空格子）</summary>
        public BagItemData OccupiedItem { get; private set; }

        /// <summary>是否为空格子</summary>
        public bool IsEmpty => OccupiedItem == null;

        /// <summary>所属背包容器</summary>
        private BagBox _owner;

        // ── 初始化 ────────────────────────────────────────────────────

        /// <summary>
        /// 由 BagBox 调用，设置格子坐标和所属容器
        /// </summary>
        public void Setup(int col, int row, BagBox owner)
        {
            Col    = col;
            Row    = row;
            _owner = owner;
            RefreshVisual();
        }

        // ── 道具占用 ──────────────────────────────────────────────────

        /// <summary>
        /// 标记此格子被某道具占用（由 BagBox 批量调用）
        /// </summary>
        public void SetOccupied(BagItemData item)
        {
            OccupiedItem = item;
            RefreshVisual();
        }

        /// <summary>
        /// 清除占用状态
        /// </summary>
        public void ClearOccupied()
        {
            OccupiedItem = null;
            RefreshVisual();
        }

        // ── 高亮控制（拖拽预览）──────────────────────────────────────

        /// <summary>
        /// 设置高亮状态（拖拽时由 BagBox 批量调用）
        /// </summary>
        /// <param name="valid">true = 绿色（可放置），false = 红色（不可放置）</param>
        public void SetHighlight(bool valid)
        {
            if (_bgImage == null) return;
            _bgImage.color = valid ? _colorValid : _colorInvalid;
        }

        /// <summary>
        /// 清除高亮，恢复正常状态
        /// </summary>
        public void ClearHighlight()
        {
            RefreshVisual();
        }

        // ── 视觉刷新 ──────────────────────────────────────────────────

        private void RefreshVisual()
        {
            if (_bgImage == null) return;
            _bgImage.color = IsEmpty ? _colorEmpty : _colorOccupied;
        }

        // ── 指针事件 ──────────────────────────────────────────────────

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_bgImage == null) return;
            // 拖拽中由 BagBox 控制高亮，不在此覆盖
            if (eventData.dragging) return;
            _bgImage.color = _colorHover;
            _owner?.OnCellPointerEnter(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            RefreshVisual();
            _owner?.OnCellPointerExit(this);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _owner?.OnCellClick(this, eventData);
        }

        public void OnDrop(PointerEventData eventData)
        {
            _owner?.OnCellDrop(this, eventData);
        }
    }
}
