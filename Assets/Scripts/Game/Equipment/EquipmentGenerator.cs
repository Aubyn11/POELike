using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using POELike.ECS.Components;

namespace POELike.Game.Equipment
{
    /// <summary>
    /// 装备品质枚举
    /// </summary>
    public enum EquipmentQuality
    {
        /// <summary>白色：无词缀</summary>
        Normal,
        /// <summary>蓝色：1~2个词缀</summary>
        Magic,
        /// <summary>金色：3~6个词缀</summary>
        Rare,
    }

    /// <summary>
    /// 插槽颜色枚举（对应 POE 属性颜色）
    /// </summary>
    public enum SocketColor
    {
        /// <summary>红色：力量属性</summary>
        Red,
        /// <summary>绿色：敏捷属性</summary>
        Green,
        /// <summary>蓝色：智慧属性</summary>
        Blue,
        /// <summary>白色：无属性要求</summary>
        White,
    }

    /// <summary>
    /// 单个插槽数据
    /// </summary>
    public class SocketData
    {
        public SocketColor Color;
    }

    /// <summary>
    /// 生成的装备运行时数据
    /// </summary>
    public class GeneratedEquipment
    {
        public EquipmentDetailTypeData DetailType;
        public EquipmentQuality        Quality;
        public List<RolledMod>         Mods    = new List<RolledMod>();
        public List<SocketData>        Sockets = new List<SocketData>();

        /// <summary>装备名称（含品质前缀）</summary>
        public string DisplayName => DetailType?.EquipmentDetailTypeName ?? "未知装备";

        /// <summary>品质对应的背包显示颜色</summary>
        public Color QualityColor => Quality switch
        {
            EquipmentQuality.Normal => Color.white,
            EquipmentQuality.Magic  => new Color(0.4f, 0.6f, 1f),   // 蓝色
            EquipmentQuality.Rare   => new Color(1f,   0.8f, 0.2f), // 金色
            _                       => Color.white,
        };
    }

    /// <summary>
    /// 生成的药剂运行时数据
    /// </summary>
    public class GeneratedFlask
    {
        public FlaskBaseData BaseData;
        public FlaskKind FlaskType;
        public FlaskUtilityEffectKind UtilityEffectType;
        public List<EquipmentSlot> AllowedSlots = new List<EquipmentSlot>();
        public int GridWidth = 1;
        public int GridHeight = 1;
        public int RequireLevel;
        public int RecoverLife;
        public int RecoverMana;
        public int DurationMs;
        public int MaxCharges;
        public int CurrentCharges;
        public int ChargesPerUse;
        public bool IsInstant;
        public int InstantPercent;
        public int UtilityEffectValue;
        public string EffectDescription;

        public string Code => BaseData?.FlaskCode ?? string.Empty;
        public string DisplayName => BaseData?.FlaskName ?? "未知药剂";

        public Color QualityColor => FlaskType switch
        {
            FlaskKind.Life    => new Color(0.86f, 0.32f, 0.32f),
            FlaskKind.Mana    => new Color(0.34f, 0.55f, 0.95f),
            FlaskKind.Hybrid  => new Color(0.63f, 0.46f, 0.86f),
            FlaskKind.Utility => new Color(0.36f, 0.80f, 0.48f),
            _                 => Color.white,
        };
    }

    /// <summary>
    /// 已随机出的词缀及其数值
    /// </summary>
    public class RolledMod
    {
        public EquipmentModData            Mod;
        public List<RolledModValue>        Values = new List<RolledModValue>();
    }

    /// <summary>
    /// 词缀的一条数值（已随机出具体值）
    /// </summary>
    public class RolledModValue
    {
        public EquipmentModValueData Config;
        public int                   RolledValue;
    }

    /// <summary>
    /// 装备随机生成器
    /// </summary>
    public static class EquipmentGenerator
    {
        // ── 大类别 ID 常量 ────────────────────────────────────────────
        // 1=单手武器  2=双手武器  3=头盔  4=胸甲  5=手套  6=鞋子  7=饰品  8=副手

        public const int FlaskTabIndex = 4;

        /// <summary>
        /// 商店装备页签定义：每个页签对应的大类别 ID 列表
        /// </summary>
        public static readonly int[][] TabCategories = new int[][]
        {
            new[] { 1, 2 },   // Tab0: 武器（单手+双手）
            new[] { 4 },      // Tab1: 胸甲
            new[] { 3, 5, 6 },// Tab2: 头盔+手套+鞋子
            new[] { 7 },      // Tab3: 饰品
        };

        public static readonly string[] TabNames = new[]
        {
            "武器", "胸甲", "防具", "饰品", "药剂"
        };

        // ── 品质规则 ──────────────────────────────────────────────────
        // 无词缀=白色，1~2个词缀=蓝色，3~6个词缀=金色
        // 前缀最多3个，后缀最多3个

        private const int MaxPrefix = 3;
        private const int MaxSuffix = 3;

        /// <summary>
        /// 为指定大类别列表随机生成一批装备（每个细节类型生成1件）
        /// </summary>
        /// <param name="categoryIds">大类别 ID 列表</param>
        /// <returns>生成的装备列表</returns>
        public static List<GeneratedEquipment> GenerateForCategories(int[] categoryIds)
        {
            var result = new List<GeneratedEquipment>();

            // 找出属于这些大类别的所有小类别 ID
            var subCatIds = new HashSet<int>();
            foreach (var catId in categoryIds)
            {
                foreach (var sub in EquipmentConfigLoader.SubCategories)
                {
                    if (int.TryParse(sub.EquipmentCategoryId, out int cid) && cid == catId)
                    {
                        if (int.TryParse(sub.EquipmentSubCategoryId, out int sid))
                            subCatIds.Add(sid);
                    }
                }
            }

            // 找出属于这些小类别的所有细节类型
            var detailTypes = EquipmentConfigLoader.DetailTypes
                .Where(d =>
                {
                    if (d.EquipmentTypes == null) return false;
                    foreach (var tid in d.EquipmentTypes)
                        if (subCatIds.Contains(tid))
                            return true;
                    return false;
                })
                .ToList();

            foreach (var detail in detailTypes)
                result.Add(GenerateOne(detail));

            return result;
        }

        /// <summary>
        /// 为单个细节类型随机生成一件装备
        /// </summary>
        public static GeneratedEquipment GenerateOne(EquipmentDetailTypeData detail)
        {
            var equip = new GeneratedEquipment { DetailType = detail };

            // 获取该装备可用的词缀（通过小类别匹配）
            var availableMods = GetAvailableMods(detail);

            // 随机决定词缀数量（0~6，前后缀各最多3）
            int totalMods = UnityEngine.Random.Range(0, 7); // 0~6
            int prefixCount = 0;
            int suffixCount = 0;

            var prefixPool = availableMods.Where(m => m.EquipmentModType == "1").ToList();
            var suffixPool = availableMods.Where(m => m.EquipmentModType == "2").ToList();

            var rolledPrefixes = RollMods(prefixPool, Mathf.Min(totalMods, MaxPrefix), ref prefixCount);
            int remaining = totalMods - rolledPrefixes.Count;
            var rolledSuffixes = RollMods(suffixPool, Mathf.Min(remaining, MaxSuffix), ref suffixCount);

            equip.Mods.AddRange(rolledPrefixes);
            equip.Mods.AddRange(rolledSuffixes);

            // 决定品质
            int modCount = equip.Mods.Count;
            equip.Quality = modCount == 0 ? EquipmentQuality.Normal
                          : modCount <= 2 ? EquipmentQuality.Magic
                          :                 EquipmentQuality.Rare;

            // 生成插槽
            equip.Sockets = GenerateSockets(detail);

            return equip;
        }

        /// <summary>
        /// 按配置生成单个药剂运行时数据。
        /// </summary>
        public static GeneratedFlask GenerateFlask(FlaskBaseData baseData)
        {
            if (baseData == null)
                return null;

            return new GeneratedFlask
            {
                BaseData = baseData,
                FlaskType = ParseFlaskKind(baseData.FlaskType),
                UtilityEffectType = ParseFlaskUtilityEffectKind(baseData.FlaskUtilityEffectType),
                AllowedSlots = ParseAllowedSlots(baseData.FlaskAllowedSlots),
                GridWidth = Mathf.Max(1, ParseInt(baseData.FlaskWidth, 1)),
                GridHeight = Mathf.Max(1, ParseInt(baseData.FlaskHeight, 1)),
                RequireLevel = ParseInt(baseData.FlaskRequireLevel),
                RecoverLife = Mathf.Max(0, ParseInt(baseData.FlaskRecoverLife)),
                RecoverMana = Mathf.Max(0, ParseInt(baseData.FlaskRecoverMana)),
                DurationMs = Mathf.Max(0, ParseInt(baseData.FlaskDurationMs)),
                MaxCharges = Mathf.Max(0, ParseInt(baseData.FlaskMaxCharges)),
                CurrentCharges = Mathf.Max(0, ParseInt(baseData.FlaskMaxCharges)),
                ChargesPerUse = Mathf.Max(0, ParseInt(baseData.FlaskChargesPerUse)),
                IsInstant = ParseBool(baseData.FlaskIsInstant),
                InstantPercent = Mathf.Clamp(ParseInt(baseData.FlaskInstantPercent), 0, 100),
                UtilityEffectValue = ParseInt(baseData.FlaskUtilityEffectValue),
                EffectDescription = baseData.FlaskEffectDesc ?? string.Empty,
            };
        }

        /// <summary>
        /// 按配置编码生成指定药剂。
        /// </summary>
        public static GeneratedFlask GenerateFlaskByCode(string flaskCode)
        {
            if (string.IsNullOrWhiteSpace(flaskCode))
                return null;

            var baseData = EquipmentConfigLoader.FlaskBases
                .FirstOrDefault(f => string.Equals(f?.FlaskCode, flaskCode, StringComparison.OrdinalIgnoreCase));
            return GenerateFlask(baseData);
        }

        /// <summary>
        /// 为商店随机挑选一批药剂。
        /// </summary>
        public static List<GeneratedFlask> GenerateFlasksForShop(int maxCount = 15)
        {
            var pool = EquipmentConfigLoader.FlaskBases
                .Where(f => f != null)
                .OrderBy(_ => UnityEngine.Random.value)
                .Take(Mathf.Max(1, maxCount))
                .ToList();

            var result = new List<GeneratedFlask>(pool.Count);
            foreach (var baseData in pool)
            {
                var generated = GenerateFlask(baseData);
                if (generated != null)
                    result.Add(generated);
            }

            result.Sort((a, b) => a.RequireLevel.CompareTo(b.RequireLevel));
            return result;
        }

        // ── 内部辅助 ──────────────────────────────────────────────────

        private static int ParseInt(string value, int fallback = 0)
        {
            return int.TryParse(value, out var parsed) ? parsed : fallback;
        }

        private static bool ParseBool(string value)
        {
            if (bool.TryParse(value, out var parsed))
                return parsed;

            return value == "1";
        }

        private static FlaskKind ParseFlaskKind(string value)
        {
            int kindValue = ParseInt(value);
            return System.Enum.IsDefined(typeof(FlaskKind), kindValue)
                ? (FlaskKind)kindValue
                : FlaskKind.Life;
        }

        private static FlaskUtilityEffectKind ParseFlaskUtilityEffectKind(string value)
        {
            int kindValue = ParseInt(value);
            return System.Enum.IsDefined(typeof(FlaskUtilityEffectKind), kindValue)
                ? (FlaskUtilityEffectKind)kindValue
                : FlaskUtilityEffectKind.None;
        }

        private static List<EquipmentSlot> ParseAllowedSlots(List<int> slotIds)
        {
            var result = new List<EquipmentSlot>();
            if (slotIds == null)
                return result;

            foreach (var slotId in slotIds)
            {
                if (System.Enum.IsDefined(typeof(EquipmentSlot), slotId))
                    result.Add((EquipmentSlot)slotId);
            }

            return result;
        }

        /// <summary>
        /// 根据装备尺寸和小类别配置随机生成插槽列表。
        /// 最终最大插槽数 = min(gridW * gridH * 2, 小类别配置上限)；
        /// 若小类别未配置，则回退到旧规则（单手武器=3，其它=6）。
        /// 插槽颜色权重：力量需求→红，智慧需求→蓝，其余→绿，无需求→白。
        /// </summary>
        private static List<SocketData> GenerateSockets(EquipmentDetailTypeData detail)
        {
            int.TryParse(detail.EquipmentWidth,  out int w); w = Mathf.Max(1, w);
            int.TryParse(detail.EquipmentHeight, out int h); h = Mathf.Max(1, h);

            int maxSockets = GetMaxSocketCount(detail, w, h);
            int socketCount = UnityEngine.Random.Range(0, maxSockets + 1);

            // 属性需求权重（决定插槽颜色概率）
            int.TryParse(detail.EquipmentDemandStrength,     out int str);
            int.TryParse(detail.EquipmentDemandIntelligence, out int intel);
            int.TryParse(detail.EquipmentDemandWisdom,       out int wis);

            // 无任何属性需求时插槽为白色
            bool noReq = str == 0 && intel == 0 && wis == 0;

            var sockets = new List<SocketData>(socketCount);
            for (int i = 0; i < socketCount; i++)
            {
                SocketColor color;
                if (noReq)
                {
                    color = SocketColor.White;
                }
                else
                {
                    // 按属性需求权重随机颜色
                    int total = str + intel + wis;
                    int rand  = UnityEngine.Random.Range(0, total);
                    if (rand < str)               color = SocketColor.Red;
                    else if (rand < str + intel)  color = SocketColor.Blue;
                    else                          color = SocketColor.Green;
                }
                sockets.Add(new SocketData { Color = color });
            }
            return sockets;
        }

        private static int GetMaxSocketCount(EquipmentDetailTypeData detail, int width, int height)
        {
            int areaCap = Mathf.Min(width * height * 2, 6);
            int configuredCap = -1;

            if (detail.EquipmentTypes != null)
            {
                foreach (var typeId in detail.EquipmentTypes)
                {
                    var sub = EquipmentConfigLoader.SubCategories
                        .FirstOrDefault(s => ParseInt(s.EquipmentSubCategoryId, -1) == typeId);
                    if (sub == null)
                        continue;

                    int cap = ParseInt(sub.EquipmentMaxSlot, GetLegacyJewelrySocketCap(typeId));
                    if (cap >= 0)
                        configuredCap = Mathf.Max(configuredCap, cap);
                }
            }

            if (configuredCap < 0)
                configuredCap = IsSingleHandWeapon(detail) ? 3 : 6;

            return Mathf.Min(areaCap, configuredCap);
        }

        private static int GetLegacyJewelrySocketCap(int subCategoryId)
        {
            return subCategoryId switch
            {
                18 => 2,
                19 => 1,
                20 => 0,
                _  => -1,
            };
        }

        private static bool IsSingleHandWeapon(EquipmentDetailTypeData detail)
        {
            if (detail.EquipmentTypes == null)
                return false;

            foreach (var typeId in detail.EquipmentTypes)
            {
                var sub = EquipmentConfigLoader.SubCategories
                    .FirstOrDefault(s => ParseInt(s.EquipmentSubCategoryId, -1) == typeId);
                if (sub != null && ParseInt(sub.EquipmentCategoryId, -1) == 1)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 获取装备可用的词缀列表（通过小类别匹配）
        /// </summary>
        private static List<EquipmentModData> GetAvailableMods(EquipmentDetailTypeData detail)
        {
            if (detail.EquipmentTypes == null || detail.EquipmentTypes.Count == 0)
                return new List<EquipmentModData>();

            var typeIds = new HashSet<int>(detail.EquipmentTypes);

            return EquipmentConfigLoader.Mods
                .Where(m =>
                {
                    if (m.EquipmentModSubCategories == null) return false;
                    foreach (var scid in m.EquipmentModSubCategories)
                        if (typeIds.Contains(scid))
                            return true;
                    return false;
                })
                .ToList();
        }

        /// <summary>
        /// 按权重随机抽取词缀，不重复
        /// </summary>
        private static List<RolledMod> RollMods(List<EquipmentModData> pool, int count, ref int rolledCount)
        {
            var result = new List<RolledMod>();
            var remaining = new List<EquipmentModData>(pool);

            for (int i = 0; i < count && remaining.Count > 0; i++)
            {
                // 按权重随机选取
                int totalWeight = 0;
                foreach (var m in remaining)
                    if (int.TryParse(m.EquipmentModWeight, out int w)) totalWeight += w;

                if (totalWeight <= 0) break;

                int rand = UnityEngine.Random.Range(0, totalWeight);
                int cumulative = 0;
                EquipmentModData chosen = null;
                foreach (var m in remaining)
                {
                    if (int.TryParse(m.EquipmentModWeight, out int w))
                    {
                        cumulative += w;
                        if (rand < cumulative) { chosen = m; break; }
                    }
                }

                if (chosen == null) break;
                remaining.Remove(chosen);

                // 随机词缀数值
                var rolled = new RolledMod { Mod = chosen };
                if (int.TryParse(chosen.EquipmentModId, out int modId))
                {
                    var valueConfigs = EquipmentConfigLoader.ModValues
                        .Where(v => v.EquipmentModId == chosen.EquipmentModId)
                        .ToList();

                    foreach (var vc in valueConfigs)
                    {
                        int.TryParse(vc.EquipmentModMinValue, out int minVal);
                        int.TryParse(vc.EquipmentModMaxValue, out int maxVal);
                        rolled.Values.Add(new RolledModValue
                        {
                            Config      = vc,
                            RolledValue = UnityEngine.Random.Range(minVal, maxVal + 1),
                        });
                    }
                }

                result.Add(rolled);
                rolledCount++;
            }

            return result;
        }
    }
}