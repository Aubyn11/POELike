using System;
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
        Currency,
        Misc,
    }

    /// <summary>
    /// 宝石类型
    /// </summary>
    public enum BagGemKind
    {
        Active,
        Support,
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

        /// <summary>物品稀有度</summary>
        public ItemRarity Rarity { get; set; } = ItemRarity.Normal;

        /// <summary>基础描述（通货、杂项等非装备道具使用）</summary>
        public string Description { get; set; }

        /// <summary>是否可堆叠</summary>
        public bool IsStackable { get; set; }

        /// <summary>当前堆叠数量</summary>
        public int StackCount { get; set; } = 1;

        /// <summary>最大堆叠数量</summary>
        public int MaxStackCount { get; set; } = 1;

        /// <summary>通货配置主键</summary>
        public string CurrencyBaseId { get; set; }

        /// <summary>通货代码（如 wisdom_scroll / chaos_orb）</summary>
        public string CurrencyCode { get; set; }

        /// <summary>通货分类 ID</summary>
        public string CurrencyCategoryId { get; set; }

        /// <summary>通货分类名</summary>
        public string CurrencyCategoryName { get; set; }

        /// <summary>通货显示色（十六进制）</summary>
        public string CurrencyDisplayColor { get; set; }

        /// <summary>通货效果类型 ID</summary>
        public string CurrencyEffectTypeId { get; set; }

        /// <summary>通货效果类型名</summary>
        public string CurrencyEffectTypeName { get; set; }

        /// <summary>通货目标描述</summary>
        public string CurrencyTargetDescription { get; set; }

        /// <summary>通货效果描述</summary>
        public string CurrencyEffectDescription { get; set; }

        /// <summary>通货风味文本</summary>
        public string CurrencyFlavorText { get; set; }

        /// <summary>掉落等级</summary>
        public int CurrencyDropLevel { get; set; }

        /// <summary>排序值</summary>
        public int CurrencySortOrder { get; set; }

        /// <summary>使用时是否消耗</summary>
        public bool CurrencyConsumesOnUse { get; set; } = true;

        /// <summary>是否可作用于普通物品</summary>
        public bool CurrencyCanApplyNormal { get; set; }

        /// <summary>是否可作用于魔法物品</summary>
        public bool CurrencyCanApplyMagic { get; set; }

        /// <summary>是否可作用于稀有物品</summary>
        public bool CurrencyCanApplyRare { get; set; }

        /// <summary>是否可作用于传奇物品</summary>
        public bool CurrencyCanApplyUnique { get; set; }

        /// <summary>是否可作用于已腐化物品</summary>
        public bool CurrencyCanApplyCorrupted { get; set; }

        /// <summary>通货允许作用的物品类型</summary>
        public List<ItemType> CurrencyAllowedItemTypes { get; } = new List<ItemType>();

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

        /// <summary>药剂品质（百分比）</summary>
        public int FlaskQualityPercent { get; set; }

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

        /// <summary>宝石类型（仅宝石道具使用）</summary>
        public BagGemKind GemKind { get; set; } = BagGemKind.Active;

        /// <summary>装备自带的插槽列表</summary>
        public List<SocketData> Sockets { get; } = new List<SocketData>();

        /// <summary>装备前缀词条文本（用于提示显示）</summary>
        public List<string> PrefixDescriptions { get; } = new List<string>();

        /// <summary>装备后缀词条文本（用于提示显示）</summary>
        public List<string> SuffixDescriptions { get; } = new List<string>();

        /// <summary>装备已随机出的词条（含数值）。用于装备上身时生成 <see cref="StatModifier"/>。</summary>
        public List<RolledMod> EquipmentMods { get; } = new List<RolledMod>();

        public bool IsEquipment => ItemKind == BagItemKind.Equipment;
        public bool IsGem       => ItemKind == BagItemKind.Gem;
        public bool IsFlask     => ItemKind == BagItemKind.Flask;
        public bool IsCurrency  => ItemKind == BagItemKind.Currency;
        public bool IsEquippable => IsEquipment || IsFlask;
        public bool IsActiveSkillGem => IsGem && GemKind == BagGemKind.Active;
        public bool IsSupportSkillGem => IsGem && GemKind == BagGemKind.Support;
        public int AvailableStackSpace => !IsStackable ? 0 : Mathf.Max(0, MaxStackCount - Mathf.Max(0, StackCount));

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
                Rarity = flask.Rarity,
                ItemColor = flask.QualityColor,
                FlaskType = flask.FlaskType,
                FlaskRequireLevel = flask.RequireLevel,
                FlaskRecoverLife = flask.RecoverLife,
                FlaskRecoverMana = flask.RecoverMana,
                FlaskDurationMs = flask.DurationMs,
                FlaskMaxCharges = flask.MaxCharges,
                FlaskCurrentCharges = flask.CurrentCharges,
                FlaskChargesPerUse = flask.ChargesPerUse,
                FlaskQualityPercent = flask.QualityPercent,
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

        public int ResolveFlaskMaxCharges()
        {
            return RuntimeItemData != null && RuntimeItemData.Type == ItemType.Flask
                ? RuntimeItemData.FlaskMaxCharges
                : FlaskMaxCharges;
        }

        public float ResolveFlaskChargePercent()
        {
            int maxCharges = ResolveFlaskMaxCharges();
            if (maxCharges <= 0)
                return 0f;

            return Mathf.Clamp01(ResolveFlaskCurrentCharges() / (float)maxCharges);
        }

        public void SyncFlaskStateFromRuntime()
        {
            if (!IsFlask || RuntimeItemData == null || RuntimeItemData.Type != ItemType.Flask)
                return;

            Rarity = RuntimeItemData.Rarity;
            FlaskType = RuntimeItemData.FlaskType;
            FlaskRequireLevel = RuntimeItemData.RequiredLevel;
            FlaskRecoverLife = RuntimeItemData.FlaskRecoverLife;
            FlaskRecoverMana = RuntimeItemData.FlaskRecoverMana;
            FlaskDurationMs = RuntimeItemData.FlaskDurationMs;
            FlaskMaxCharges = RuntimeItemData.FlaskMaxCharges;
            FlaskCurrentCharges = RuntimeItemData.FlaskCurrentCharges;
            FlaskChargesPerUse = RuntimeItemData.FlaskChargesPerUse;
            FlaskQualityPercent = RuntimeItemData.FlaskQualityPercent;
            FlaskIsInstant = RuntimeItemData.FlaskIsInstant;
            FlaskInstantPercent = RuntimeItemData.FlaskInstantPercent;
            FlaskUtilityEffectType = RuntimeItemData.FlaskUtilityEffectType;
            FlaskUtilityEffectValue = RuntimeItemData.FlaskUtilityEffectValue;
            FlaskEffectDescription = RuntimeItemData.FlaskEffectDescription;
            AcceptedEquipmentSlot = RuntimeItemData.PrimaryEquipmentSlot;

            if (RuntimeItemData.AllowedEquipmentSlots != null && RuntimeItemData.AllowedEquipmentSlots.Count > 0)
                SetAcceptedEquipmentSlots(RuntimeItemData.AllowedEquipmentSlots);
        }

        public bool CanStackWith(BagItemData other)
        {
            if (other == null || ReferenceEquals(this, other))
                return false;

            if (!IsStackable || !other.IsStackable)
                return false;

            return string.Equals(ResolveStackKey(), other.ResolveStackKey(), StringComparison.OrdinalIgnoreCase);
        }

        public int MergeFrom(BagItemData other)
        {
            if (!CanStackWith(other))
                return 0;

            NormalizeStackState();
            other.NormalizeStackState();

            int moveAmount = Mathf.Min(AvailableStackSpace, Mathf.Max(0, other.StackCount));
            if (moveAmount <= 0)
                return 0;

            StackCount = Mathf.Clamp(StackCount + moveAmount, 1, Mathf.Max(1, MaxStackCount));
            other.StackCount = Mathf.Max(0, other.StackCount - moveAmount);
            return moveAmount;
        }

        public void NormalizeStackState(bool clampToMax = true)
        {
            if (!IsStackable)
            {
                StackCount = 1;
                MaxStackCount = 1;
                return;
            }

            MaxStackCount = Mathf.Max(1, MaxStackCount);
            StackCount = clampToMax
                ? Mathf.Clamp(StackCount, 1, MaxStackCount)
                : Mathf.Max(1, StackCount);
        }

        public BagItemData CloneForStack(int stackCount)
        {
            var clone = new BagItemData(ItemId, Name, GridWidth, GridHeight)
            {
                ItemKind = ItemKind,
                Rarity = Rarity,
                Description = Description,
                IsStackable = IsStackable,
                StackCount = stackCount,
                MaxStackCount = MaxStackCount,
                CurrencyBaseId = CurrencyBaseId,
                CurrencyCode = CurrencyCode,
                CurrencyCategoryId = CurrencyCategoryId,
                CurrencyCategoryName = CurrencyCategoryName,
                CurrencyDisplayColor = CurrencyDisplayColor,
                CurrencyEffectTypeId = CurrencyEffectTypeId,
                CurrencyEffectTypeName = CurrencyEffectTypeName,
                CurrencyTargetDescription = CurrencyTargetDescription,
                CurrencyEffectDescription = CurrencyEffectDescription,
                CurrencyFlavorText = CurrencyFlavorText,
                CurrencyDropLevel = CurrencyDropLevel,
                CurrencySortOrder = CurrencySortOrder,
                CurrencyConsumesOnUse = CurrencyConsumesOnUse,
                CurrencyCanApplyNormal = CurrencyCanApplyNormal,
                CurrencyCanApplyMagic = CurrencyCanApplyMagic,
                CurrencyCanApplyRare = CurrencyCanApplyRare,
                CurrencyCanApplyUnique = CurrencyCanApplyUnique,
                CurrencyCanApplyCorrupted = CurrencyCanApplyCorrupted,
                AcceptedEquipmentSlot = AcceptedEquipmentSlot,
                FlaskType = FlaskType,
                FlaskRequireLevel = FlaskRequireLevel,
                FlaskRecoverLife = FlaskRecoverLife,
                FlaskRecoverMana = FlaskRecoverMana,
                FlaskDurationMs = FlaskDurationMs,
                FlaskMaxCharges = FlaskMaxCharges,
                FlaskCurrentCharges = FlaskCurrentCharges,
                FlaskChargesPerUse = FlaskChargesPerUse,
                FlaskQualityPercent = FlaskQualityPercent,
                FlaskIsInstant = FlaskIsInstant,
                FlaskInstantPercent = FlaskInstantPercent,
                FlaskUtilityEffectType = FlaskUtilityEffectType,
                FlaskUtilityEffectValue = FlaskUtilityEffectValue,
                FlaskEffectDescription = FlaskEffectDescription,
                GemColor = GemColor,
                GemKind = GemKind,
                Icon = Icon,
                ItemColor = ItemColor,
            };

            clone.SetAcceptedEquipmentSlots(AcceptedEquipmentSlots);

            for (int i = 0; i < CurrencyAllowedItemTypes.Count; i++)
                clone.CurrencyAllowedItemTypes.Add(CurrencyAllowedItemTypes[i]);

            for (int i = 0; i < Sockets.Count; i++)
            {
                var socket = Sockets[i];
                if (socket == null)
                    continue;

                clone.Sockets.Add(new SocketData
                {
                    Color = socket.Color,
                    LinkedToPrevious = socket.LinkedToPrevious,
                });
            }

            for (int i = 0; i < PrefixDescriptions.Count; i++)
                clone.PrefixDescriptions.Add(PrefixDescriptions[i]);

            for (int i = 0; i < SuffixDescriptions.Count; i++)
                clone.SuffixDescriptions.Add(SuffixDescriptions[i]);

            for (int i = 0; i < EquipmentMods.Count; i++)
                clone.EquipmentMods.Add(EquipmentMods[i]);

            if (RuntimeItemData != null)
                clone.RuntimeItemData = CloneRuntimeItemData(stackCount);

            clone.NormalizeStackState();
            return clone;
        }

        private ItemData CloneRuntimeItemData(int stackCount)
        {
            if (RuntimeItemData == null)
                return null;

            var item = new ItemData
            {
                Id = RuntimeItemData.Id,
                Name = RuntimeItemData.Name,
                BaseType = RuntimeItemData.BaseType,
                Type = RuntimeItemData.Type,
                Rarity = RuntimeItemData.Rarity,
                ItemLevel = RuntimeItemData.ItemLevel,
                RequiredLevel = RuntimeItemData.RequiredLevel,
                RequiredStrength = RuntimeItemData.RequiredStrength,
                RequiredDexterity = RuntimeItemData.RequiredDexterity,
                RequiredIntelligence = RuntimeItemData.RequiredIntelligence,
                IsStackable = RuntimeItemData.IsStackable,
                StackCount = RuntimeItemData.IsStackable ? Mathf.Max(1, stackCount) : RuntimeItemData.StackCount,
                MaxStackCount = RuntimeItemData.MaxStackCount,
                Description = RuntimeItemData.Description,
                CurrencyBaseId = RuntimeItemData.CurrencyBaseId,
                CurrencyCode = RuntimeItemData.CurrencyCode,
                CurrencyCategoryId = RuntimeItemData.CurrencyCategoryId,
                CurrencyCategoryName = RuntimeItemData.CurrencyCategoryName,
                CurrencyDisplayColor = RuntimeItemData.CurrencyDisplayColor,
                CurrencyEffectTypeId = RuntimeItemData.CurrencyEffectTypeId,
                CurrencyEffectTypeName = RuntimeItemData.CurrencyEffectTypeName,
                CurrencyTargetDescription = RuntimeItemData.CurrencyTargetDescription,
                CurrencyEffectDescription = RuntimeItemData.CurrencyEffectDescription,
                CurrencyFlavorText = RuntimeItemData.CurrencyFlavorText,
                CurrencyDropLevel = RuntimeItemData.CurrencyDropLevel,
                CurrencySortOrder = RuntimeItemData.CurrencySortOrder,
                CurrencyConsumesOnUse = RuntimeItemData.CurrencyConsumesOnUse,
                CurrencyCanApplyNormal = RuntimeItemData.CurrencyCanApplyNormal,
                CurrencyCanApplyMagic = RuntimeItemData.CurrencyCanApplyMagic,
                CurrencyCanApplyRare = RuntimeItemData.CurrencyCanApplyRare,
                CurrencyCanApplyUnique = RuntimeItemData.CurrencyCanApplyUnique,
                CurrencyCanApplyCorrupted = RuntimeItemData.CurrencyCanApplyCorrupted,
                PrimaryEquipmentSlot = RuntimeItemData.PrimaryEquipmentSlot,
                FlaskType = RuntimeItemData.FlaskType,
                FlaskRecoverLife = RuntimeItemData.FlaskRecoverLife,
                FlaskRecoverMana = RuntimeItemData.FlaskRecoverMana,
                FlaskDurationMs = RuntimeItemData.FlaskDurationMs,
                FlaskMaxCharges = RuntimeItemData.FlaskMaxCharges,
                FlaskCurrentCharges = RuntimeItemData.FlaskCurrentCharges,
                FlaskChargesPerUse = RuntimeItemData.FlaskChargesPerUse,
                FlaskQualityPercent = RuntimeItemData.FlaskQualityPercent,
                FlaskIsInstant = RuntimeItemData.FlaskIsInstant,
                FlaskInstantPercent = RuntimeItemData.FlaskInstantPercent,
                FlaskUtilityEffectType = RuntimeItemData.FlaskUtilityEffectType,
                FlaskUtilityEffectValue = RuntimeItemData.FlaskUtilityEffectValue,
                FlaskEffectDescription = RuntimeItemData.FlaskEffectDescription,
            };

            for (int i = 0; i < RuntimeItemData.AllowedEquipmentSlots.Count; i++)
                item.AllowedEquipmentSlots.Add(RuntimeItemData.AllowedEquipmentSlots[i]);

            for (int i = 0; i < RuntimeItemData.CurrencyAllowedItemTypes.Count; i++)
                item.CurrencyAllowedItemTypes.Add(RuntimeItemData.CurrencyAllowedItemTypes[i]);

            for (int i = 0; i < RuntimeItemData.Prefixes.Count; i++)
                item.Prefixes.Add(RuntimeItemData.Prefixes[i]);

            for (int i = 0; i < RuntimeItemData.Suffixes.Count; i++)
                item.Suffixes.Add(RuntimeItemData.Suffixes[i]);

            for (int i = 0; i < RuntimeItemData.ImplicitMods.Count; i++)
                item.ImplicitMods.Add(RuntimeItemData.ImplicitMods[i]);

            return item;
        }

        private string ResolveStackKey()

        {
            if (IsCurrency && !string.IsNullOrWhiteSpace(CurrencyCode))
                return $"currency:{CurrencyCode.Trim()}";

            if (!string.IsNullOrWhiteSpace(ItemId))
                return $"{ItemKind}:{ItemId.Trim()}";

            return $"{ItemKind}:{Name?.Trim()}";
        }

        /// <summary>
        /// 将背包道具转换为 ECS 运行时物品数据。
        /// </summary>

        public ItemData ToItemData()
        {
            NormalizeStackState(clampToMax: false);

            var item = RuntimeItemData ?? new ItemData();

            item.Id = ItemId;
            item.Name = Name;
            item.Description = Description;
            item.Type = ResolveItemType();
            item.Rarity = RuntimeItemData != null ? RuntimeItemData.Rarity : Rarity;
            item.IsStackable = IsStackable;
            item.StackCount = StackCount;
            item.MaxStackCount = MaxStackCount;
            item.PrimaryEquipmentSlot = ResolvePrimaryEquipmentSlot();
            item.AllowedEquipmentSlots.Clear();
            item.CurrencyAllowedItemTypes.Clear();

            if (AcceptedEquipmentSlots != null && AcceptedEquipmentSlots.Count > 0)
            {
                for (int i = 0; i < AcceptedEquipmentSlots.Count; i++)
                {
                    var slot = AcceptedEquipmentSlots[i];
                    if (!item.AllowedEquipmentSlots.Contains(slot))
                        item.AllowedEquipmentSlots.Add(slot);
                }
            }
            else if (AcceptedEquipmentSlot.HasValue)
            {
                item.AllowedEquipmentSlots.Add(AcceptedEquipmentSlot.Value);
            }

            if (IsCurrency)
            {
                item.CurrencyBaseId = CurrencyBaseId;
                item.CurrencyCode = CurrencyCode;
                item.CurrencyCategoryId = CurrencyCategoryId;
                item.CurrencyCategoryName = CurrencyCategoryName;
                item.CurrencyDisplayColor = CurrencyDisplayColor;
                item.CurrencyEffectTypeId = CurrencyEffectTypeId;
                item.CurrencyEffectTypeName = CurrencyEffectTypeName;
                item.CurrencyTargetDescription = CurrencyTargetDescription;
                item.CurrencyEffectDescription = CurrencyEffectDescription;
                item.CurrencyFlavorText = CurrencyFlavorText;
                item.CurrencyDropLevel = CurrencyDropLevel;

                item.CurrencySortOrder = CurrencySortOrder;
                item.CurrencyConsumesOnUse = CurrencyConsumesOnUse;
                item.CurrencyCanApplyNormal = CurrencyCanApplyNormal;
                item.CurrencyCanApplyMagic = CurrencyCanApplyMagic;
                item.CurrencyCanApplyRare = CurrencyCanApplyRare;
                item.CurrencyCanApplyUnique = CurrencyCanApplyUnique;
                item.CurrencyCanApplyCorrupted = CurrencyCanApplyCorrupted;

                for (int i = 0; i < CurrencyAllowedItemTypes.Count; i++)
                {
                    var allowedType = CurrencyAllowedItemTypes[i];
                    if (!item.CurrencyAllowedItemTypes.Contains(allowedType))
                        item.CurrencyAllowedItemTypes.Add(allowedType);
                }
            }

            if (IsFlask)
            {
                bool hasRuntimeFlask = RuntimeItemData != null && RuntimeItemData.Type == ItemType.Flask;
                item.FlaskType = hasRuntimeFlask ? RuntimeItemData.FlaskType : FlaskType;
                item.RequiredLevel = hasRuntimeFlask ? RuntimeItemData.RequiredLevel : FlaskRequireLevel;
                item.FlaskRecoverLife = hasRuntimeFlask ? RuntimeItemData.FlaskRecoverLife : FlaskRecoverLife;
                item.FlaskRecoverMana = hasRuntimeFlask ? RuntimeItemData.FlaskRecoverMana : FlaskRecoverMana;
                item.FlaskDurationMs = hasRuntimeFlask ? RuntimeItemData.FlaskDurationMs : FlaskDurationMs;
                item.FlaskMaxCharges = hasRuntimeFlask ? RuntimeItemData.FlaskMaxCharges : FlaskMaxCharges;
                item.FlaskCurrentCharges = hasRuntimeFlask ? RuntimeItemData.FlaskCurrentCharges : ResolveFlaskCurrentCharges();
                item.FlaskChargesPerUse = hasRuntimeFlask ? RuntimeItemData.FlaskChargesPerUse : FlaskChargesPerUse;
                item.FlaskQualityPercent = hasRuntimeFlask ? RuntimeItemData.FlaskQualityPercent : FlaskQualityPercent;
                item.FlaskIsInstant = hasRuntimeFlask ? RuntimeItemData.FlaskIsInstant : FlaskIsInstant;
                item.FlaskInstantPercent = hasRuntimeFlask ? RuntimeItemData.FlaskInstantPercent : FlaskInstantPercent;
                item.FlaskUtilityEffectType = hasRuntimeFlask ? RuntimeItemData.FlaskUtilityEffectType : FlaskUtilityEffectType;
                item.FlaskUtilityEffectValue = hasRuntimeFlask ? RuntimeItemData.FlaskUtilityEffectValue : FlaskUtilityEffectValue;
                item.FlaskEffectDescription = hasRuntimeFlask ? RuntimeItemData.FlaskEffectDescription : FlaskEffectDescription;
                FlaskCurrentCharges = item.FlaskCurrentCharges;
                FlaskQualityPercent = item.FlaskQualityPercent;
            }

            if (IsEquipment)

            {
                RebuildItemModifiersFromEquipmentMods(item);
            }

            RuntimeItemData = item;
            return item;
        }

        /// <summary>
        /// 根据 <see cref="EquipmentMods"/> 中的词条配置，重建 <see cref="ItemData.Prefixes"/> /
        /// <see cref="ItemData.Suffixes"/> 中的 <see cref="StatModifier"/>，让装备上身后
        /// <c>StatsSystem</c> 能正确聚合到 <c>StatsComponent</c> 上。
        /// </summary>
        private void RebuildItemModifiersFromEquipmentMods(ItemData item)
        {
            // 对于商店/掉落生成的装备，优先根据 EquipmentMods 重建词条；
            // 若当前装备只是手工构造的演示装备（没有 EquipmentMods），则保留 RuntimeItemData 中原有的前后缀。
            if (EquipmentMods == null || EquipmentMods.Count == 0)
                return;

            item.Prefixes.Clear();
            item.Suffixes.Clear();

            foreach (var rolled in EquipmentMods)
            {
                if (rolled?.Mod == null || rolled.Values == null)
                    continue;

                bool isPrefix = rolled.Mod.EquipmentModType == "1";
                string source = string.IsNullOrEmpty(rolled.Mod.EquipmentModName)
                    ? "equipment"
                    : $"equipment:{rolled.Mod.EquipmentModName}";

                foreach (var value in rolled.Values)
                {
                    if (value?.Config == null)
                        continue;

                    if (!TryResolveStatType(rolled.Mod, value.Config, out var statType))
                        continue;

                    var modifierType = ResolveModifierType(rolled.Mod);
                    float statValue = ConvertRolledValueToStatValue(rolled.Mod, value.Config, value.RolledValue);
                    var modifier = new StatModifier(statType, modifierType, statValue, source);

                    if (isPrefix)
                        item.Prefixes.Add(modifier);
                    else
                        item.Suffixes.Add(modifier);
                }
            }
        }

        private static bool TryResolveStatType(EquipmentModData mod, EquipmentModValueData _, out StatType statType)
        {
            statType = default;
            if (mod == null || string.IsNullOrWhiteSpace(mod.EquipmentModStatType))
                return false;

            return System.Enum.TryParse(mod.EquipmentModStatType.Trim(), true, out statType);
        }

        private static ModifierType ResolveModifierType(EquipmentModData mod)
        {
            if (mod == null || string.IsNullOrWhiteSpace(mod.EquipmentModModifierType))
                return ModifierType.Flat;

            return System.Enum.TryParse(mod.EquipmentModModifierType.Trim(), true, out ModifierType parsed)
                ? parsed
                : ModifierType.Flat;
        }

        /// <summary>
        /// 将配置里的原始数值转换为 StatModifier 的实际数值。
        /// 某些词条（例如生命偷取）在配置里以 ‰ 的形式存储（20 表示 2.0%），
        /// 通过识别词缀描述中的 ‰ 占位符做 /10 转换。
        /// </summary>
        private static float ConvertRolledValueToStatValue(EquipmentModData mod, EquipmentModValueData valueConfig, int rolledValue)
        {
            if (valueConfig == null)
                return rolledValue;

            var desc = valueConfig.EquipmentModValueDesc;
            if (!string.IsNullOrEmpty(desc) && desc.IndexOf('‰') >= 0)
                return rolledValue / 10f;

            return rolledValue;
        }

        private ItemType ResolveItemType()
        {
            if (IsFlask)
                return ItemType.Flask;

            if (IsGem)
                return ItemType.Gem;

            if (IsCurrency)
                return ItemType.Currency;

            if (IsEquipment)

            {
                var primarySlot = ResolvePrimaryEquipmentSlot();
                return primarySlot switch
                {
                    EquipmentSlot.MainHand or EquipmentSlot.OffHand => ItemType.Weapon,
                    EquipmentSlot.RingLeft or EquipmentSlot.RingRight or EquipmentSlot.Amulet or EquipmentSlot.Belt => ItemType.Accessory,
                    _ => ItemType.Armour,
                };
            }

            return ItemType.Misc;
        }

        private EquipmentSlot? ResolvePrimaryEquipmentSlot()
        {
            if (AcceptedEquipmentSlots != null && AcceptedEquipmentSlots.Count > 0)
                return AcceptedEquipmentSlots[0];

            return AcceptedEquipmentSlot;
        }

        private static bool IsRingSlot(EquipmentSlot slot)
        {
            return slot == EquipmentSlot.RingLeft || slot == EquipmentSlot.RingRight;
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

                    if (IsRingSlot(AcceptedEquipmentSlots[i]) && IsRingSlot(slot))
                        return true;
                }
            }

            if (!AcceptedEquipmentSlot.HasValue)
                return false;

            if (IsRingSlot(AcceptedEquipmentSlot.Value) && IsRingSlot(slot))
                return true;

            return AcceptedEquipmentSlot.Value == slot;
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