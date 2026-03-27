using UnityEngine;

namespace POELike.Game.UI
{
    /// <summary>
    /// 背包道具数据
    /// 描述一个道具在背包格子中的占位信息（宽 × 高个格子）
    /// </summary>
    public class BagItemData
    {
        // ── 道具标识 ──────────────────────────────────────────────────

        /// <summary>道具唯一 ID</summary>
        public string ItemId { get; set; }

        /// <summary>道具显示名称</summary>
        public string Name { get; set; }

        // ── 格子占用尺寸 ──────────────────────────────────────────────

        /// <summary>占用格子列数（X 方向宽度，最小为 1）</summary>
        public int GridWidth { get; set; } = 1;

        /// <summary>占用格子行数（Y 方向高度，最小为 1）</summary>
        public int GridHeight { get; set; } = 1;

        // ── 在背包中的位置 ────────────────────────────────────────────

        /// <summary>道具左上角所在列（从 0 开始）</summary>
        public int GridCol { get; set; } = -1;

        /// <summary>道具左上角所在行（从 0 开始）</summary>
        public int GridRow { get; set; } = -1;

        /// <summary>是否已放置到背包中</summary>
        public bool IsPlaced => GridCol >= 0 && GridRow >= 0;

        // ── 显示资源 ──────────────────────────────────────────────────

        /// <summary>道具图标（可为 null）</summary>
        public Sprite Icon { get; set; }

        /// <summary>道具背景颜色（用于稀有度区分等）</summary>
        public Color ItemColor { get; set; } = Color.white;

        // ── 构造 ──────────────────────────────────────────────────────

        public BagItemData() { }

        public BagItemData(string itemId, string name, int gridWidth = 1, int gridHeight = 1)
        {
            ItemId     = itemId;
            Name       = name;
            GridWidth  = Mathf.Max(1, gridWidth);
            GridHeight = Mathf.Max(1, gridHeight);
        }

        public override string ToString()
            => $"[BagItemData] {Name} ({GridWidth}×{GridHeight}) @ ({GridCol},{GridRow})";
    }
}
