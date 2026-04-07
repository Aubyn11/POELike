using System.Collections.Generic;
using System.Text;
using POELike.ECS.Components;
using POELike.Game.Equipment;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace POELike.Game.UI
{
    /// <summary>
    /// 装备提示框视图组件
    /// 挂载在 EquipmentTips 预制体根节点上。
    /// 调用 <see cref="Setup"/> 填充数据，Tips 高度根据 TMP 实际渲染高度自适应（含自动换行）。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class EquipmentTips : MonoBehaviour
    {
        // ── 序列化字段 ────────────────────────────────────────────────

        [Header("装备名称")]
        [SerializeField] private TextMeshProUGUI _nameText;

        [Header("装备基底")]
        [SerializeField] private TextMeshProUGUI _baseValueText;

        [Header("装备需求")]
        [SerializeField] private TextMeshProUGUI _donationText;

        [Header("装备词缀")]
        [SerializeField] private TextMeshProUGUI _valueText;

        [Header("背景图片（子节点 Image，留空则按名称自动查找）")]
        [SerializeField] private Image _bgImage;

        // ── 布局常量 ──────────────────────────────────────────────────

        /// <summary>各区块之间的垂直间距（像素）</summary>
        private const float SectionSpacing = 8f;
        /// <summary>Tips 固定宽度（像素）</summary>
        private const float TipsWidth = 300f;
        /// <summary>Tips 左右内边距（像素）</summary>
        private const float PaddingH = 20f;
        /// <summary>Tips 上下内边距（像素）</summary>
        private const float PaddingV = 16f;

        // ── 属性 ──────────────────────────────────────────────────────

        private RectTransform _rt;
        private RectTransform RT => _rt ??= GetComponent<RectTransform>();

        // ── 初始化 ────────────────────────────────────────────────────

        private void Awake()
        {
            _rt = GetComponent<RectTransform>();

            // Inspector 未赋值时按子节点名称自动查找
            if (_nameText      == null) _nameText      = FindChildText("EquipmentName");
            if (_baseValueText == null) _baseValueText = FindChildText("EquipmentBaseValue");
            if (_donationText  == null) _donationText  = FindChildText("EquipmentDonation");
            if (_valueText     == null) _valueText     = FindChildText("EquipmentValue");

            // 查找背景 Image：优先 Inspector 赋值，否则按名称查找子节点
            if (_bgImage == null)
            {
                var bgChild = transform.Find("Image");
                if (bgChild != null) _bgImage = bgChild.GetComponent<Image>();
            }

            // 确保背景 Image 始终在最底层（第一个子节点），文字在其上方渲染
            if (_bgImage != null)
                _bgImage.transform.SetAsFirstSibling();
        }

        private TextMeshProUGUI FindChildText(string childName)
        {
            var child = transform.Find(childName);
            return child != null ? child.GetComponent<TextMeshProUGUI>() : null;
        }

        // ── 公共接口 ──────────────────────────────────────────────────

        /// <summary>
        /// 填充装备数据并自适应高度。
        /// </summary>
        /// <param name="equip">生成的装备运行时数据</param>
        public void Setup(GeneratedEquipment equip)
        {
            if (equip == null) return;

            var detail = equip.DetailType;

            // ── 装备名称 ──────────────────────────────────────────────
            if (_nameText != null)
                _nameText.text = equip.DisplayName;

            // ── 装备基底 ──────────────────────────────────────────────
            if (_baseValueText != null)
            {
                string baseText = BuildBaseValueText(detail);
                _baseValueText.text = baseText;
            }

            // ── 装备需求 ──────────────────────────────────────────────
            if (_donationText != null)
            {
                string reqText = BuildRequirementText(detail);
                _donationText.text = reqText;
            }

            // ── 装备词缀 ──────────────────────────────────────────────
            if (_valueText != null)
            {
                string modText = BuildModText(equip.Mods);
                _valueText.text = modText;
            }

            // ── 自适应高度 ────────────────────────────────────────────
            RefreshLayout();
        }

        /// <summary>
        /// 使用背包基础数据填充装备提示。
        /// 适用于尚未生成完整词缀数据的背包装备。
        /// </summary>
        public void Setup(BagItemData item)
        {
            if (item == null) return;

            if (_nameText != null)
                _nameText.text = string.IsNullOrWhiteSpace(item.Name) ? "未知装备" : item.Name;

            if (_baseValueText != null)
                _baseValueText.text = item.IsFlask ? BuildFlaskBaseText(item) : string.Empty;

            if (_donationText != null)
                _donationText.text = item.IsFlask ? BuildFlaskRequirementText(item) : string.Empty;

            if (_valueText != null)
                _valueText.text = item.IsFlask ? BuildFlaskEffectText(item) : BuildBagItemModText(item);

            RefreshLayout();
        }

        // ── 文本构建 ──────────────────────────────────────────────────

        private static string BuildBaseValueText(EquipmentDetailTypeData detail)
        {
            if (detail == null) return string.Empty;

            // 从配置加载基底数值
            var baseValues = EquipmentConfigLoader.BaseValues;
            if (baseValues == null) return string.Empty;

            var sb = new StringBuilder();
            foreach (var bv in baseValues)
            {
                if (bv.EquipmentBaseValueId == detail.EquipmentBaseValueId)
                {
                    sb.AppendLine($"{bv.EquipmentBaseValueDesc}：{bv.EquipmentBaseMinValue} - {bv.EquipmentBaseMaxValue}");
                }
            }
            return sb.ToString().TrimEnd();
        }

        private static string BuildRequirementText(EquipmentDetailTypeData detail)
        {
            if (detail == null) return string.Empty;

            var sb = new StringBuilder();

            if (!string.IsNullOrEmpty(detail.EquipmentDemandLevel) && detail.EquipmentDemandLevel != "0")
                sb.AppendLine($"需求等级：{detail.EquipmentDemandLevel}");

            if (!string.IsNullOrEmpty(detail.EquipmentDemandStrength) && detail.EquipmentDemandStrength != "0")
                sb.AppendLine($"力量需求：{detail.EquipmentDemandStrength}");

            if (!string.IsNullOrEmpty(detail.EquipmentDemandIntelligence) && detail.EquipmentDemandIntelligence != "0")
                sb.AppendLine($"智慧需求：{detail.EquipmentDemandIntelligence}");

            if (!string.IsNullOrEmpty(detail.EquipmentDemandWisdom) && detail.EquipmentDemandWisdom != "0")
                sb.AppendLine($"敏捷需求：{detail.EquipmentDemandWisdom}");

            return sb.ToString().TrimEnd();
        }

        private static string BuildModText(List<RolledMod> mods)
        {
            if (mods == null || mods.Count == 0) return string.Empty;

            var sb = new StringBuilder();
            foreach (var mod in mods)
            {
                if (mod.Mod == null) continue;

                // 词缀类型标签
                string typeTag = mod.Mod.EquipmentModType == "1" ? "[前缀]" : "[后缀]";

                // 拼接词缀描述（含随机数值）
                if (mod.Values != null && mod.Values.Count > 0)
                {
                    foreach (var val in mod.Values)
                    {
                        if (val.Config == null) continue;
                        string desc = val.Config.EquipmentModValueDesc ?? mod.Mod.EquipmentModName;
                        // 将描述中的占位符替换为实际数值（若有）
                        desc = desc.Replace("{value}", val.RolledValue.ToString());
                        sb.AppendLine($"{typeTag} {desc}（{val.RolledValue}）");
                    }
                }
                else
                {
                    sb.AppendLine($"{typeTag} {mod.Mod.EquipmentModName}");
                }
            }
            return sb.ToString().TrimEnd();
        }

        private static string BuildBagItemModText(BagItemData item)
        {
            if (item == null)
                return string.Empty;

            var sb = new StringBuilder();
            AppendAffixLines(sb, item.PrefixDescriptions, "[前缀]");
            AppendAffixLines(sb, item.SuffixDescriptions, "[后缀]");
            return sb.ToString().TrimEnd();
        }

        private static string BuildFlaskBaseText(BagItemData item)
        {
            if (item == null || !item.IsFlask)
                return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine($"药剂类型：{GetFlaskKindLabel(item.FlaskType)}");
            sb.AppendLine($"当前充能：{item.ResolveFlaskCurrentCharges()} / {item.FlaskMaxCharges}");
            sb.AppendLine($"每次使用消耗：{item.FlaskChargesPerUse}");
            return sb.ToString().TrimEnd();
        }

        private static string BuildFlaskRequirementText(BagItemData item)
        {
            if (item == null || !item.IsFlask)
                return string.Empty;

            return item.FlaskRequireLevel > 0
                ? $"需求等级：{item.FlaskRequireLevel}"
                : string.Empty;
        }

        private static string BuildFlaskEffectText(BagItemData item)
        {
            if (item == null || !item.IsFlask)
                return string.Empty;

            var sb = new StringBuilder();

            if (item.FlaskRecoverLife > 0)
                sb.AppendLine(item.FlaskDurationMs > 0
                    ? $"恢复生命：{item.FlaskRecoverLife}（{item.FlaskDurationMs / 1000f:F2} 秒）"
                    : $"恢复生命：{item.FlaskRecoverLife}");

            if (item.FlaskRecoverMana > 0)
                sb.AppendLine(item.FlaskDurationMs > 0
                    ? $"恢复魔力：{item.FlaskRecoverMana}（{item.FlaskDurationMs / 1000f:F2} 秒）"
                    : $"恢复魔力：{item.FlaskRecoverMana}");

            if (item.FlaskIsInstant)
                sb.AppendLine($"瞬间恢复比例：{item.FlaskInstantPercent}%");

            if (item.FlaskUtilityEffectType != FlaskUtilityEffectKind.None)
                sb.AppendLine($"功能效果：{GetFlaskUtilityEffectLabel(item.FlaskUtilityEffectType, item.FlaskUtilityEffectValue)}");

            if (!string.IsNullOrWhiteSpace(item.FlaskEffectDescription))
                sb.AppendLine(item.FlaskEffectDescription);

            return sb.ToString().TrimEnd();
        }

        private static void AppendAffixLines(StringBuilder sb, List<string> descriptions, string tag)
        {
            if (sb == null || descriptions == null)
                return;

            for (int i = 0; i < descriptions.Count; i++)
            {
                var description = descriptions[i];
                if (string.IsNullOrWhiteSpace(description))
                    continue;

                sb.AppendLine($"{tag} {description}");
            }
        }

        private static string BuildBagItemBaseText(BagItemData item)
        {
            if (item == null)
                return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine($"占用尺寸：{item.GridWidth} × {item.GridHeight}");
            sb.AppendLine($"道具类型：{GetItemKindLabel(item)}");
            return sb.ToString().TrimEnd();
        }

        private static string BuildBagItemRequirementText(BagItemData item)
        {
            if (item == null || !item.AcceptedEquipmentSlot.HasValue)
                return string.Empty;

            return $"装备槽位：{GetEquipmentSlotLabel(item.AcceptedEquipmentSlot.Value)}";
        }

        private static string BuildBagItemExtraText(BagItemData item)
        {
            if (item == null)
                return string.Empty;

            var sb = new StringBuilder();
            if (item.Sockets != null && item.Sockets.Count > 0)
            {
                sb.Append("插槽：");
                for (int i = 0; i < item.Sockets.Count; i++)
                {
                    if (i > 0)
                        sb.Append(" / ");

                    sb.Append(GetSocketColorLabel(item.Sockets[i].Color));
                }
            }

            return sb.ToString().TrimEnd();
        }

        private static string GetItemKindLabel(BagItemData item)
        {
            if (item == null)
                return string.Empty;

            return item.ItemKind switch
            {
                BagItemKind.Equipment => "装备",
                BagItemKind.Flask     => "药剂",
                BagItemKind.Gem       => "宝石",
                BagItemKind.Misc      => "杂项",
                _                     => "未知",
            };

        }

        private static string GetEquipmentSlotLabel(EquipmentSlot slot)
        {
            return slot switch
            {
                EquipmentSlot.Helmet      => "头盔",
                EquipmentSlot.BodyArmour  => "胸甲",
                EquipmentSlot.Gloves      => "手套",
                EquipmentSlot.Boots       => "鞋子",
                EquipmentSlot.Belt        => "腰带",
                EquipmentSlot.Amulet      => "项链",
                EquipmentSlot.RingLeft    => "左戒指",
                EquipmentSlot.RingRight   => "右戒指",
                EquipmentSlot.MainHand    => "主手",
                EquipmentSlot.OffHand     => "副手",
                EquipmentSlot.Flask1      => "药剂槽1",
                EquipmentSlot.Flask2      => "药剂槽2",
                EquipmentSlot.Flask3      => "药剂槽3",
                EquipmentSlot.Flask4      => "药剂槽4",
                EquipmentSlot.Flask5      => "药剂槽5",
                _                         => slot.ToString(),
            };
        }

        private static string GetSocketColorLabel(SocketColor color)
        {
            return color switch
            {
                SocketColor.Red   => "红孔",
                SocketColor.Green => "绿孔",
                SocketColor.Blue  => "蓝孔",
                SocketColor.White => "白孔",
                _                 => color.ToString(),
            };
        }

        private static string GetFlaskKindLabel(FlaskKind? kind)
        {
            return kind switch
            {
                FlaskKind.Life    => "生命药剂",
                FlaskKind.Mana    => "魔力药剂",
                FlaskKind.Hybrid  => "复合药剂",
                FlaskKind.Utility => "功能药剂",
                _                 => "未知药剂",
            };
        }

        private static string GetFlaskUtilityEffectLabel(FlaskUtilityEffectKind effectType, int value)
        {
            return effectType switch
            {
                FlaskUtilityEffectKind.MoveSpeed               => $"移动速度提高 {value}%",
                FlaskUtilityEffectKind.Armour                  => $"获得 +{value} 护甲",
                FlaskUtilityEffectKind.Evasion                 => $"获得 +{value} 闪避",
                FlaskUtilityEffectKind.FireResistance          => $"火焰抗性 +{value}%",
                FlaskUtilityEffectKind.ColdResistance          => $"冰霜抗性 +{value}%",
                FlaskUtilityEffectKind.LightningResistance     => $"闪电抗性 +{value}%",
                FlaskUtilityEffectKind.ChaosResistance         => $"混沌抗性 +{value}%",
                FlaskUtilityEffectKind.PhysicalDamageReduction => $"承受的物理伤害额外降低 {value}%",
                FlaskUtilityEffectKind.ConsecratedGround       => "制造奉献地面",
                FlaskUtilityEffectKind.Phasing                 => "获得穿相",
                FlaskUtilityEffectKind.Onslaught               => "获得猛攻",
                _                                              => effectType.ToString(),
            };
        }

        // ── 布局自适应 ────────────────────────────────────────────────

        /// <summary>
        /// 根据各区块 TMP 实际渲染高度重新计算并设置 Tips 总高度（含自动换行）。
        /// 各区块 RectTransform 从上到下依次排列，间距为 <see cref="SectionSpacing"/>。
        /// </summary>
        private void RefreshLayout()
        {
            // 所有文字区块（含无内容的），统一先隐藏/移出，再按需显示
            var allTexts = new TextMeshProUGUI[] { _nameText, _baseValueText, _donationText, _valueText };
            foreach (var t in allTexts)
            {
                if (t == null) continue;
                var rt = t.GetComponent<RectTransform>();
                if (rt == null) continue;
                if (string.IsNullOrWhiteSpace(t.text))
                {
                    // 无内容：尺寸归零，移到不可见位置，避免与其他区块重叠
                    rt.sizeDelta        = Vector2.zero;
                    rt.anchoredPosition = new Vector2(0f, 9999f);
                    continue;
                }
            }

            // 收集有内容的区块（按从上到下顺序）
            var sections = new List<(RectTransform rt, TextMeshProUGUI tmp)>();
            AddSection(sections, _nameText);
            AddSection(sections, _baseValueText);
            AddSection(sections, _donationText);
            AddSection(sections, _valueText);

            // 使用 Tips 根节点的原始宽度
            float tipsWidth = RT.rect.width;
            float totalHeight = PaddingV;
            float sectionW    = tipsWidth - PaddingH * 2f;

            for (int i = 0; i < sections.Count; i++)
            {
                var (sectionRt, tmp) = sections[i];

                // 用 TMP 计算在指定宽度下的实际渲染高度（含自动换行）
                Vector2 preferred = tmp.GetPreferredValues(tmp.text, sectionW, 0f);
                float sectionH = preferred.y;

                // 固定锚点到左上角，用 sizeDelta 直接指定宽高
                sectionRt.anchorMin        = new Vector2(0f, 1f);
                sectionRt.anchorMax        = new Vector2(0f, 1f);
                sectionRt.pivot            = new Vector2(0f, 1f);
                sectionRt.sizeDelta        = new Vector2(sectionW, sectionH);
                sectionRt.anchoredPosition = new Vector2(PaddingH, -totalHeight);

                totalHeight += sectionH;
                if (i < sections.Count - 1)
                    totalHeight += SectionSpacing;
            }

            totalHeight += PaddingV;

            // 只更新 Tips 根节点的高度，宽度保持原始值不变
            RT.sizeDelta = new Vector2(RT.sizeDelta.x, totalHeight);

            // 背景 Image：重置 anchor/pivot 为左上角固定点，再设置与 Tips 根节点相同的 sizeDelta
            if (_bgImage != null)
            {
                var bgRt = _bgImage.GetComponent<RectTransform>();
                if (bgRt != null)
                {
                    bgRt.anchorMin        = new Vector2(0f, 1f);
                    bgRt.anchorMax        = new Vector2(0f, 1f);
                    bgRt.pivot            = new Vector2(0f, 1f);
                    bgRt.anchoredPosition = Vector2.zero;
                    bgRt.sizeDelta        = new Vector2(RT.sizeDelta.x, totalHeight);
                }
            }
        }

        private static void AddSection(List<(RectTransform, TextMeshProUGUI)> list, TextMeshProUGUI tmp)
        {
            if (tmp == null) return;
            if (string.IsNullOrWhiteSpace(tmp.text)) return;
            var rt = tmp.GetComponent<RectTransform>();
            if (rt != null) list.Add((rt, tmp));
        }


    }
}