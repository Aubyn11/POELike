using System;
using System.Collections.Generic;
using POELike.ECS.Components;
using POELike.Game.Equipment;
using POELike.Game.Skills;
using POELike.Game.UI;
using UnityEngine;

namespace POELike.Game.Items
{
    /// <summary>
    /// GM 物品生成器。
    /// 负责把 GM 面板输入的文本条件转换为可直接进入背包的装备/宝石数据。
    /// </summary>
    public static class GMItemFactory
    {
        private static readonly char[] TokenSeparators = { ',', '，', ';', '；', '|', '/', '、', '\n', '\r' };
        private static int s_generatedCounter;

        public static IReadOnlyList<EquipmentModData> GetAvailablePrefixMods(EquipmentDetailTypeData detail)
        {
            return GetAvailableMods(detail, true);
        }

        public static IReadOnlyList<EquipmentModData> GetAvailableSuffixMods(EquipmentDetailTypeData detail)
        {
            return GetAvailableMods(detail, false);
        }

        public static int GetMaxSocketCountForDetail(EquipmentDetailTypeData detail)
        {
            return GetMaxSocketCount(detail);
        }

        public static SocketColor GetDefaultGemColorForActiveSkill(ActiveSkillStoneConfigData config)
        {
            return ResolveDefaultActiveGemColor(config);
        }

        public static bool TryCreateEquipment(

            string detailQuery,
            string prefixQuery,
            string suffixQuery,
            int socketCount,
            string socketColorsInput,
            string linkInput,
            out BagItemData bagItem,
            out string error)
        {
            bagItem = null;
            error = string.Empty;

            var detail = ResolveEquipmentDetail(detailQuery);
            if (detail == null)
            {
                error = $"未找到装备类别/基础类型：{detailQuery}";
                return false;
            }

            if (socketCount < 0)
            {
                error = "装备孔数不能为负数";
                return false;
            }

            int maxSocketCount = GetMaxSocketCount(detail);
            if (socketCount > maxSocketCount)
            {
                error = $"{detail.EquipmentDetailTypeName} 最多只能生成 {maxSocketCount} 个孔";
                return false;
            }

            if (!TryResolveMods(detail, prefixQuery, true, out var prefixes, out error))
                return false;
            if (!TryResolveMods(detail, suffixQuery, false, out var suffixes, out error))
                return false;

            if (prefixes.Count > 3)
            {
                error = "前缀最多只能指定 3 个";
                return false;
            }

            if (suffixes.Count > 3)
            {
                error = "后缀最多只能指定 3 个";
                return false;
            }

            int totalMods = prefixes.Count + suffixes.Count;
            if (totalMods > 6)
            {
                error = "总词缀数量不能超过 6 个";
                return false;
            }

            if (!TryBuildSockets(detail, socketCount, socketColorsInput, linkInput, out var sockets, out error))
                return false;

            var generated = new GeneratedEquipment
            {
                DetailType = detail,
                Quality = ResolveQuality(totalMods),
                Sockets = sockets,
            };

            for (int i = 0; i < prefixes.Count; i++)
                generated.Mods.Add(prefixes[i]);
            for (int i = 0; i < suffixes.Count; i++)
                generated.Mods.Add(suffixes[i]);

            bagItem = EquipmentBagDataFactory.CreateFromGeneratedEquipment(
                generated,
                GenerateItemId("gm_equipment", detail.EquipmentDetailTypeId));

            if (bagItem == null)
            {
                error = "生成装备背包数据失败";
                return false;
            }

            return true;
        }

        public static bool TryCreateGem(
            string gemQuery,
            string gemColorInput,
            bool isSupportGem,
            out BagItemData bagItem,
            out string error)
        {
            bagItem = null;
            error = string.Empty;

            if (isSupportGem)
            {
                var supportConfig = ResolveSupportSkill(gemQuery);
                if (supportConfig == null)
                {
                    error = $"未找到被动/辅助宝石：{gemQuery}";
                    return false;
                }

                if (!TryResolveGemColor(gemColorInput, SocketColor.Green, out var gemColor))
                {
                    error = $"宝石颜色格式错误：{gemColorInput}";
                    return false;
                }

                bagItem = new BagItemData(
                    GenerateItemId("gm_support_gem", supportConfig.SupportSkillStoneCode ?? supportConfig.SupportSkillStoneId),
                    string.IsNullOrWhiteSpace(supportConfig.SupportSkillStoneName) ? gemQuery?.Trim() : supportConfig.SupportSkillStoneName,
                    1,
                    1)
                {
                    ItemKind = BagItemKind.Gem,
                    GemKind = BagGemKind.Support,
                    GemColor = gemColor,
                    ItemColor = ResolveGemDisplayColor(gemColor),
                };

                return true;
            }

            var activeConfig = ResolveActiveSkill(gemQuery);
            if (activeConfig == null)
            {
                error = $"未找到主动宝石：{gemQuery}";
                return false;
            }

            if (!TryResolveGemColor(gemColorInput, ResolveDefaultActiveGemColor(activeConfig), out var activeGemColor))
            {
                error = $"宝石颜色格式错误：{gemColorInput}";
                return false;
            }

            bagItem = new BagItemData(
                GenerateItemId("gm_active_gem", activeConfig.ActiveSkillStoneCode ?? activeConfig.ActiveSkillStoneId),
                string.IsNullOrWhiteSpace(activeConfig.ActiveSkillStoneName) ? gemQuery?.Trim() : activeConfig.ActiveSkillStoneName,
                1,
                1)
            {
                ItemKind = BagItemKind.Gem,
                GemKind = BagGemKind.Active,
                GemColor = activeGemColor,
                ItemColor = ResolveGemDisplayColor(activeGemColor),
            };

            return true;
        }

        private static EquipmentDetailTypeData ResolveEquipmentDetail(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return null;

            string normalized = query.Trim();
            var details = EquipmentConfigLoader.DetailTypes;

            for (int i = 0; i < details.Count; i++)
            {
                var detail = details[i];
                if (detail != null && string.Equals(detail.EquipmentDetailTypeId, normalized, StringComparison.OrdinalIgnoreCase))
                    return detail;
            }

            for (int i = 0; i < details.Count; i++)
            {
                var detail = details[i];
                if (detail != null && string.Equals(detail.EquipmentDetailTypeName, normalized, StringComparison.OrdinalIgnoreCase))
                    return detail;
            }

            for (int i = 0; i < details.Count; i++)
            {
                var detail = details[i];
                if (detail == null)
                    continue;

                if (ContainsIgnoreCase(detail.EquipmentDetailTypeName, normalized))
                    return detail;

                var subCategoryNames = GetSubCategoryNames(detail);
                for (int subIndex = 0; subIndex < subCategoryNames.Count; subIndex++)
                {
                    if (ContainsIgnoreCase(subCategoryNames[subIndex], normalized))
                        return detail;
                }

                string partLabel = ResolveEquipmentPartLabel(detail.EquipmentPart);
                if (ContainsIgnoreCase(partLabel, normalized))
                    return detail;
            }

            return null;
        }

        private static bool TryResolveMods(
            EquipmentDetailTypeData detail,
            string query,
            bool isPrefix,
            out List<RolledMod> result,
            out string error)
        {
            result = new List<RolledMod>();
            error = string.Empty;

            var tokens = SplitTokens(query);
            if (tokens.Count == 0)
                return true;

            var availableMods = GetAvailableMods(detail, isPrefix);
            var resolvedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < tokens.Count; i++)
            {
                string token = tokens[i];
                var mod = ResolveEquipmentMod(availableMods, token);
                if (mod == null)
                {
                    error = $"未找到{(isPrefix ? "前缀" : "后缀")}：{token}";
                    return false;
                }

                if (!resolvedIds.Add(mod.EquipmentModId ?? mod.EquipmentModName ?? token))
                {
                    error = $"重复指定了同一个{(isPrefix ? "前缀" : "后缀")}：{token}";
                    return false;
                }

                result.Add(CreateMaxRolledMod(mod));
            }

            return true;
        }

        private static List<EquipmentModData> GetAvailableMods(EquipmentDetailTypeData detail, bool isPrefix)
        {
            var result = new List<EquipmentModData>();
            if (detail?.EquipmentTypes == null)
                return result;

            var allMods = EquipmentConfigLoader.Mods;
            string expectedType = isPrefix ? "1" : "2";

            for (int i = 0; i < allMods.Count; i++)
            {
                var mod = allMods[i];
                if (mod == null || mod.EquipmentModType != expectedType)
                    continue;

                if (mod.EquipmentModSubCategories == null || mod.EquipmentModSubCategories.Count == 0)
                    continue;

                for (int detailTypeIndex = 0; detailTypeIndex < detail.EquipmentTypes.Count; detailTypeIndex++)
                {
                    if (mod.EquipmentModSubCategories.Contains(detail.EquipmentTypes[detailTypeIndex]))
                    {
                        result.Add(mod);
                        break;
                    }
                }
            }

            return result;
        }

        private static EquipmentModData ResolveEquipmentMod(List<EquipmentModData> candidates, string token)
        {
            if (candidates == null || candidates.Count == 0 || string.IsNullOrWhiteSpace(token))
                return null;

            string normalized = token.Trim();

            for (int i = 0; i < candidates.Count; i++)
            {
                var mod = candidates[i];
                if (mod != null && string.Equals(mod.EquipmentModId, normalized, StringComparison.OrdinalIgnoreCase))
                    return mod;
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                var mod = candidates[i];
                if (mod != null && string.Equals(mod.EquipmentModName, normalized, StringComparison.OrdinalIgnoreCase))
                    return mod;
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                var mod = candidates[i];
                if (mod != null && ContainsIgnoreCase(mod.EquipmentModName, normalized))
                    return mod;
            }

            return null;
        }

        private static RolledMod CreateMaxRolledMod(EquipmentModData mod)
        {
            var rolled = new RolledMod { Mod = mod };
            if (mod == null)
                return rolled;

            var valueConfigs = EquipmentConfigLoader.ModValues;
            for (int i = 0; i < valueConfigs.Count; i++)
            {
                var valueConfig = valueConfigs[i];
                if (valueConfig == null || !string.Equals(valueConfig.EquipmentModId, mod.EquipmentModId, StringComparison.OrdinalIgnoreCase))
                    continue;

                int minValue = ParseInt(valueConfig.EquipmentModMinValue);
                int maxValue = ParseInt(valueConfig.EquipmentModMaxValue, minValue);
                rolled.Values.Add(new RolledModValue
                {
                    Config = valueConfig,
                    RolledValue = Mathf.Max(minValue, maxValue),
                });
            }

            return rolled;
        }

        private static bool TryBuildSockets(
            EquipmentDetailTypeData detail,
            int socketCount,
            string socketColorsInput,
            string linkInput,
            out List<SocketData> sockets,
            out string error)
        {
            sockets = new List<SocketData>();
            error = string.Empty;

            if (socketCount == 0)
                return true;

            if (!TryResolveSocketColors(socketCount, socketColorsInput, out var colors, out error))
                return false;

            if (!TryResolveLinkStates(socketCount, linkInput, out var linkedToPrevious, out error))
                return false;

            for (int i = 0; i < socketCount; i++)
            {
                sockets.Add(new SocketData
                {
                    Color = colors[i],
                    LinkedToPrevious = i > 0 && linkedToPrevious[i - 1],
                });
            }

            return true;
        }

        private static bool TryResolveSocketColors(int socketCount, string input, out List<SocketColor> colors, out string error)
        {
            colors = new List<SocketColor>(socketCount);
            error = string.Empty;

            if (socketCount <= 0)
                return true;

            var tokens = SplitTokens(input);
            if (tokens.Count > socketCount)
            {
                error = $"孔颜色数量不能超过孔数（当前孔数 {socketCount}）";
                return false;
            }

            for (int i = 0; i < tokens.Count; i++)
            {
                if (!TryParseSocketColor(tokens[i], out var color))
                {
                    error = $"无法识别的孔颜色：{tokens[i]}（支持 R/G/B/W、红/绿/蓝/白）";
                    return false;
                }

                colors.Add(color);
            }

            while (colors.Count < socketCount)
                colors.Add(SocketColor.White);

            return true;
        }

        private static bool TryResolveLinkStates(int socketCount, string input, out bool[] linkedToPrevious, out string error)
        {
            linkedToPrevious = new bool[Mathf.Max(0, socketCount - 1)];
            error = string.Empty;

            if (linkedToPrevious.Length == 0)
                return true;

            if (string.IsNullOrWhiteSpace(input) || IsFullLinkKeyword(input))
            {
                for (int i = 0; i < linkedToPrevious.Length; i++)
                    linkedToPrevious[i] = true;
                return true;
            }

            if (IsNoLinkKeyword(input))
                return true;

            var tokens = SplitTokens(input);
            for (int i = 0; i < tokens.Count; i++)
            {
                if (!TryParseLinkRange(tokens[i], out int startIndex, out int endIndex))
                {
                    error = $"无法识别的连结格式：{tokens[i]}（示例：1-2,2-3 或 1-4）";
                    return false;
                }

                if (startIndex < 1 || endIndex < 1 || startIndex > socketCount || endIndex > socketCount)
                {
                    error = $"连结范围越界：{tokens[i]}（当前孔数 {socketCount}）";
                    return false;
                }

                int min = Mathf.Min(startIndex, endIndex);
                int max = Mathf.Max(startIndex, endIndex);
                for (int linkIndex = min; linkIndex < max; linkIndex++)
                    linkedToPrevious[linkIndex - 1] = true;
            }

            return true;
        }

        private static bool TryParseLinkRange(string token, out int startIndex, out int endIndex)
        {
            startIndex = -1;
            endIndex = -1;
            if (string.IsNullOrWhiteSpace(token))
                return false;

            string normalized = token.Trim()
                .Replace("－", "-")
                .Replace("—", "-")
                .Replace("–", "-")
                .Replace("~", "-")
                .Replace("～", "-")
                .Replace("至", "-")
                .Replace("到", "-");

            var parts = normalized.Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
                return false;

            return int.TryParse(parts[0].Trim(), out startIndex) && int.TryParse(parts[1].Trim(), out endIndex);
        }

        private static int GetMaxSocketCount(EquipmentDetailTypeData detail)
        {
            int width = Mathf.Max(1, ParseInt(detail?.EquipmentWidth, 1));
            int height = Mathf.Max(1, ParseInt(detail?.EquipmentHeight, 1));
            int areaCap = Mathf.Min(width * height * 2, 6);
            int configuredCap = -1;

            if (detail?.EquipmentTypes != null)
            {
                var subCategories = EquipmentConfigLoader.SubCategories;
                for (int i = 0; i < detail.EquipmentTypes.Count; i++)
                {
                    int typeId = detail.EquipmentTypes[i];
                    for (int subIndex = 0; subIndex < subCategories.Count; subIndex++)
                    {
                        var sub = subCategories[subIndex];
                        if (sub == null || ParseInt(sub.EquipmentSubCategoryId, -1) != typeId)
                            continue;

                        int fallback = GetLegacyJewelrySocketCap(typeId);
                        configuredCap = Mathf.Max(configuredCap, ParseInt(sub.EquipmentMaxSlot, fallback));
                        break;
                    }
                }
            }

            if (configuredCap < 0)
                configuredCap = IsSingleHandWeapon(detail) ? 3 : 6;

            return Mathf.Min(areaCap, configuredCap);
        }

        private static bool IsSingleHandWeapon(EquipmentDetailTypeData detail)
        {
            if (detail?.EquipmentTypes == null)
                return false;

            var subCategories = EquipmentConfigLoader.SubCategories;
            for (int i = 0; i < detail.EquipmentTypes.Count; i++)
            {
                int typeId = detail.EquipmentTypes[i];
                for (int subIndex = 0; subIndex < subCategories.Count; subIndex++)
                {
                    var sub = subCategories[subIndex];
                    if (sub == null || ParseInt(sub.EquipmentSubCategoryId, -1) != typeId)
                        continue;

                    return ParseInt(sub.EquipmentCategoryId, -1) == 1;
                }
            }

            return false;
        }

        private static int GetLegacyJewelrySocketCap(int subCategoryId)
        {
            return subCategoryId switch
            {
                18 => 2,
                19 => 1,
                20 => 0,
                _ => -1,
            };
        }

        private static EquipmentQuality ResolveQuality(int modCount)
        {
            return modCount switch
            {
                <= 0 => EquipmentQuality.Normal,
                <= 2 => EquipmentQuality.Magic,
                _ => EquipmentQuality.Rare,
            };
        }

        private static ActiveSkillStoneConfigData ResolveActiveSkill(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return null;

            string normalized = query.Trim();
            var activeSkills = SkillConfigLoader.ActiveSkills;

            for (int i = 0; i < activeSkills.Count; i++)
            {
                var skill = activeSkills[i];
                if (skill != null && string.Equals(skill.ActiveSkillStoneId, normalized, StringComparison.OrdinalIgnoreCase))
                    return skill;
            }

            for (int i = 0; i < activeSkills.Count; i++)
            {
                var skill = activeSkills[i];
                if (skill != null && string.Equals(skill.ActiveSkillStoneCode, normalized, StringComparison.OrdinalIgnoreCase))
                    return skill;
            }

            for (int i = 0; i < activeSkills.Count; i++)
            {
                var skill = activeSkills[i];
                if (skill != null && string.Equals(skill.ActiveSkillStoneName, normalized, StringComparison.OrdinalIgnoreCase))
                    return skill;
            }

            for (int i = 0; i < activeSkills.Count; i++)
            {
                var skill = activeSkills[i];
                if (skill != null && (ContainsIgnoreCase(skill.ActiveSkillStoneCode, normalized) || ContainsIgnoreCase(skill.ActiveSkillStoneName, normalized)))
                    return skill;
            }

            return null;
        }

        private static SupportSkillStoneConfigData ResolveSupportSkill(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return null;

            string normalized = query.Trim();
            var supportSkills = SkillConfigLoader.SupportSkills;

            for (int i = 0; i < supportSkills.Count; i++)
            {
                var skill = supportSkills[i];
                if (skill != null && string.Equals(skill.SupportSkillStoneId, normalized, StringComparison.OrdinalIgnoreCase))
                    return skill;
            }

            for (int i = 0; i < supportSkills.Count; i++)
            {
                var skill = supportSkills[i];
                if (skill != null && string.Equals(skill.SupportSkillStoneCode, normalized, StringComparison.OrdinalIgnoreCase))
                    return skill;
            }

            for (int i = 0; i < supportSkills.Count; i++)
            {
                var skill = supportSkills[i];
                if (skill != null && string.Equals(skill.SupportSkillStoneName, normalized, StringComparison.OrdinalIgnoreCase))
                    return skill;
            }

            for (int i = 0; i < supportSkills.Count; i++)
            {
                var skill = supportSkills[i];
                if (skill != null && (ContainsIgnoreCase(skill.SupportSkillStoneCode, normalized) || ContainsIgnoreCase(skill.SupportSkillStoneName, normalized)))
                    return skill;
            }

            return null;
        }

        private static bool TryResolveGemColor(string input, SocketColor fallback, out SocketColor color)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                color = fallback;
                return true;
            }

            return TryParseSocketColor(input, out color);
        }

        private static SocketColor ResolveDefaultActiveGemColor(ActiveSkillStoneConfigData config)
        {
            return ParseInt(config?.SkillColor, -1) switch
            {
                0 => SocketColor.Red,
                1 => SocketColor.Green,
                2 => SocketColor.Blue,
                _ => SocketColor.White,
            };
        }

        private static bool TryParseSocketColor(string token, out SocketColor color)
        {
            color = SocketColor.White;
            if (string.IsNullOrWhiteSpace(token))
                return false;

            switch (token.Trim().ToLowerInvariant())
            {
                case "r":
                case "red":
                case "红":
                case "红色":
                    color = SocketColor.Red;
                    return true;
                case "g":
                case "green":
                case "绿":
                case "绿色":
                    color = SocketColor.Green;
                    return true;
                case "b":
                case "blue":
                case "蓝":
                case "蓝色":
                    color = SocketColor.Blue;
                    return true;
                case "w":
                case "white":
                case "白":
                case "白色":
                    color = SocketColor.White;
                    return true;
                default:
                    return false;
            }
        }

        private static Color ResolveGemDisplayColor(SocketColor color)
        {
            return color switch
            {
                SocketColor.Red => new Color(0.92f, 0.30f, 0.24f),
                SocketColor.Green => new Color(0.25f, 0.86f, 0.38f),
                SocketColor.Blue => new Color(0.35f, 0.58f, 1.00f),
                _ => new Color(0.92f, 0.92f, 0.92f),
            };
        }

        private static List<string> GetSubCategoryNames(EquipmentDetailTypeData detail)
        {
            var result = new List<string>();
            if (detail?.EquipmentTypes == null)
                return result;

            var subCategories = EquipmentConfigLoader.SubCategories;
            for (int i = 0; i < detail.EquipmentTypes.Count; i++)
            {
                int typeId = detail.EquipmentTypes[i];
                for (int subIndex = 0; subIndex < subCategories.Count; subIndex++)
                {
                    var sub = subCategories[subIndex];
                    if (sub != null && ParseInt(sub.EquipmentSubCategoryId, -1) == typeId && !string.IsNullOrWhiteSpace(sub.EquipmentSubCategoryName))
                    {
                        result.Add(sub.EquipmentSubCategoryName);
                        break;
                    }
                }
            }

            return result;
        }

        private static string ResolveEquipmentPartLabel(string equipmentPart)
        {
            return ParseInt(equipmentPart, -1) switch
            {
                1 => "主手",
                2 => "副手",
                3 => "头盔",
                4 => "胸甲",
                5 => "手套",
                6 => "鞋子",
                7 => "饰品",
                _ => string.Empty,
            };
        }

        private static bool IsFullLinkKeyword(string input)
        {
            string normalized = input.Trim().ToLowerInvariant();
            return normalized == "全连" || normalized == "全鏈" || normalized == "full" || normalized == "all" || normalized == "full_link";
        }

        private static bool IsNoLinkKeyword(string input)
        {
            string normalized = input.Trim().ToLowerInvariant();
            return normalized == "无" || normalized == "不连" || normalized == "none" || normalized == "no" || normalized == "0";
        }

        private static List<string> SplitTokens(string input)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(input))
                return result;

            var parts = input.Split(TokenSeparators, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string token = parts[i]?.Trim();
                if (!string.IsNullOrWhiteSpace(token))
                    result.Add(token);
            }

            return result;
        }

        private static bool ContainsIgnoreCase(string source, string value)
        {
            return !string.IsNullOrWhiteSpace(source) &&
                   !string.IsNullOrWhiteSpace(value) &&
                   source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int ParseInt(string value, int fallback = 0)
        {
            return int.TryParse(value, out var parsed) ? parsed : fallback;
        }

        private static string GenerateItemId(string prefix, string key)
        {
            s_generatedCounter++;
            return $"{prefix}_{SanitizeToken(key)}_{s_generatedCounter}";
        }

        private static string SanitizeToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "item";

            var chars = value.Trim().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_')
                    chars[i] = '_';
            }

            return new string(chars);
        }
    }
}