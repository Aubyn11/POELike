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
        Flask,
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

        /// <summary>装备可放入的目标槽位列表（用于药剂等可进入多个槽位的道具）</summary>
        public List<EquipmentSlot> AcceptedEquipmentSlots { get; } = new List<EquipmentSlot>();

        /// <summary>药剂类型（仅药剂道具使用）</summary>
        public FlaskKind? FlaskType { get; set; }

        /// <summary>药剂需求等级</summary>
        public int FlaskRequireLevel { get; set; }

        /// <summary>生命恢复量</summary>
        public int FlaskRecoverLife { get; set; }

        /// <summary>魔力恢复量</summary>
        public int FlaskRecoverMana { get; set; }

        /// <summary>持续时间（毫秒）</summary>
        public int FlaskDurationMs { get; set; }

        /// <summary>最大充能数</summary>
        public int FlaskMaxCharges { get; set; }

        /// <summary>当前充能数</summary>
        public int FlaskCurrentCharges { get; set; }

        /// <summary>每次使用消耗的充能数</summary>
        public int FlaskChargesPerUse { get; set; }

        /// <summary>是否为瞬回药剂</summary>
        public bool FlaskIsInstant { get; set; }

        /// <summary>瞬回比例（百分比）</summary>
        public int FlaskInstantPercent { get; set; }

        /// <summary>功能药剂效果类型</summary>
        public FlaskUtilityEffectKind FlaskUtilityEffectType { get; set; } = FlaskUtilityEffectKind.None;

        /// <summary>功能药剂效果数值</summary>
        public int FlaskUtilityEffectValue { get; set; }

        /// <summary>药剂效果描述</summary>
        public string FlaskEffectDescription { get; set; }

        /// <summary>运行时 ECS 物品数据引用（用于药剂充能等状态同步）</summary>
        public ItemData RuntimeItemData { get; set; }

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
        public bool IsFlask     => ItemKind == BagItemKind.Flask;
        public bool IsEquippable => IsEquipment || IsFlask;

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
        /// 从运行时药剂数据构建背包道具数据。
        /// </summary>
        public static BagItemData CreateFromGeneratedFlask(GeneratedFlask flask)
        {
            if (flask == null)
                return null;

            var data = new BagItemData(flask.Code, flask.DisplayName, flask.GridWidth, flask.GridHeight)
            {
                ItemKind = BagItemKind.Flask,
                ItemColor = flask.QualityColor,
                FlaskType = flask.FlaskType,
                FlaskRequireLevel = flask.RequireLevel,
                FlaskRecoverLife = flask.RecoverLife,
                FlaskRecoverMana = flask.RecoverMana,
                FlaskDurationMs = flask.DurationMs,
                FlaskMaxCharges = flask.MaxCharges,
                FlaskCurrentCharges = flask.CurrentCharges,
                FlaskChargesPerUse = flask.ChargesPerUse,
                FlaskIsInstant = flask.IsInstant,
                FlaskInstantPercent = flask.InstantPercent,
                FlaskUtilityEffectType = flask.UtilityEffectType,
                FlaskUtilityEffectValue = flask.UtilityEffectValue,
                FlaskEffectDescription = flask.EffectDescription,
            };

            data.SetAcceptedEquipmentSlots(flask.AllowedSlots);
            return data;
        }

        /// <summary>
        /// 获取药剂当前充能数；若已映射到运行时物品，则优先读取运行时状态。
        /// </summary>
        public int ResolveFlaskCurrentCharges()
        {
            return RuntimeItemData != null && RuntimeItemData.Type == ItemType.Flask
                ? RuntimeItemData.FlaskCurrentCharges
                : FlaskCurrentCharges;
        }

        /// <summary>
        /// 将背包道具转换为 ECS 运行时物品数据。
        /// </summary>
        public ItemData ToItemData()
        {
            var item = RuntimeItemData ?? new ItemData();
            item.Id = ItemId;
            item.Name = Name;
            item.Type = ResolveItemType();

            if (IsFlask)
            {
                item.FlaskType = FlaskType;
                item.RequiredLevel = FlaskRequireLevel;
                item.FlaskRecoverLife = FlaskRecoverLife;
                item.FlaskRecoverMana = FlaskRecoverMana;
                item.FlaskDurationMs = FlaskDurationMs;
                item.FlaskMaxCharges = FlaskMaxCharges;
                item.FlaskCurrentCharges = ResolveFlaskCurrentCharges();
                item.FlaskChargesPerUse = FlaskChargesPerUse;
                item.FlaskIsInstant = FlaskIsInstant;
                item.FlaskInstantPercent = FlaskInstantPercent;
                item.FlaskUtilityEffectType = FlaskUtilityEffectType;
                item.FlaskUtilityEffectValue = FlaskUtilityEffectValue;
                item.FlaskEffectDescription = FlaskEffectDescription;
                FlaskCurrentCharges = item.FlaskCurrentCharges;
            }

            RuntimeItemData = item;
            return item;
        }

        private ItemType ResolveItemType()
        {
            if (IsFlask)
                return ItemType.Flask;

            if (IsGem)
                return ItemType.Gem;

            if (IsEquipment)
            {
                return AcceptedEquipmentSlot switch
                {
                    EquipmentSlot.MainHand or EquipmentSlot.OffHand => ItemType.Weapon,
                    EquipmentSlot.RingLeft or EquipmentSlot.RingRight or EquipmentSlot.Amulet or EquipmentSlot.Belt => ItemType.Accessory,
                    _ => ItemType.Armour,
                };
            }

            return ItemType.Misc;
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

        /// <summary>
        /// 判断当前道具是否可装备到指定槽位。
        /// </summary>
        public bool CanEquipToSlot(EquipmentSlot slot)
        {
            if (AcceptedEquipmentSlots != null && AcceptedEquipmentSlots.Count > 0)
            {
                for (int i = 0; i < AcceptedEquipmentSlots.Count; i++)
                {
                    if (AcceptedEquipmentSlots[i] == slot)
                        return true;
                }
            }

            return AcceptedEquipmentSlot.HasValue && AcceptedEquipmentSlot.Value == slot;
        }

        /// <summary>
        /// 设置当前道具允许装备到的槽位列表。
        /// </summary>
        public void SetAcceptedEquipmentSlots(IEnumerable<EquipmentSlot> slots)
        {
            AcceptedEquipmentSlots.Clear();
            if (slots == null)
                return;

            foreach (var slot in slots)
                AcceptedEquipmentSlots.Add(slot);
        }

        public override string ToString()
            => $"[BagItemData] {Name} <{ItemKind}> ({GridWidth}×{GridHeight}) @ ({GridCol},{GridRow})";
    }
}