using System.Collections.Generic;
using POELike.ECS.Components;
using POELike.Game.Equipment;
using UnityEngine;

namespace POELike.Game.UI
{
    /// <summary>
    /// 背包道具类型
    /// </summary>
    public enum BagItemKind
    {
        Equipment,
        Gem,
        Misc,
    }

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

        /// <summary>背包道具类型</summary>
        public BagItemKind ItemKind { get; set; } = BagItemKind.Equipment;

        /// <summary>装备可放入的目标槽位（仅装备道具使用）</summary>
        public EquipmentSlot? AcceptedEquipmentSlot { get; set; }

        /// <summary>宝石颜色（仅宝石道具使用）</summary>
        public SocketColor? GemColor { get; set; }

        /// <summary>装备自带的插槽列表</summary>
        public List<SocketData> Sockets { get; } = new List<SocketData>();

        /// <summary>装备前缀词条文本（用于提示显示）</summary>
        public List<string> PrefixDescriptions { get; } = new List<string>();

        /// <summary>装备后缀词条文本（用于提示显示）</summary>
        public List<string> SuffixDescriptions { get; } = new List<string>();

        public bool IsEquipment => ItemKind == BagItemKind.Equipment;
        public bool IsGem       => ItemKind == BagItemKind.Gem;

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

        /// <summary>
        /// 判断当前宝石是否能放入指定颜色的插槽。
        /// 白孔可接受任意宝石，白色宝石也可放入任意插槽。
        /// </summary>
        public bool CanFitSocket(SocketColor socketColor)
        {
            if (!IsGem || !GemColor.HasValue)
                return false;

            return socketColor == SocketColor.White ||
                   GemColor.Value == SocketColor.White ||
                   GemColor.Value == socketColor;
        }

        public override string ToString()
            => $"[BagItemData] {Name} <{ItemKind}> ({GridWidth}×{GridHeight}) @ ({GridCol},{GridRow})";
    }
}
