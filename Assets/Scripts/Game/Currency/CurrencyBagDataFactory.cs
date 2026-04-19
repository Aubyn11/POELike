using System;
using POELike.ECS.Components;
using POELike.Game.UI;
using UnityEngine;

namespace POELike.Game.Currency
{
    public static class CurrencyBagDataFactory
    {
        public static BagItemData CreateById(string currencyBaseId, int stackCount = 1, string itemId = null)
        {
            var config = CurrencyConfigLoader.FindCurrencyById(currencyBaseId);
            return CreateFromConfig(config, stackCount, itemId);
        }

        public static BagItemData CreateByCode(string currencyCode, int stackCount = 1, string itemId = null)
        {
            var config = CurrencyConfigLoader.FindCurrencyByCode(currencyCode);
            return CreateFromConfig(config, stackCount, itemId);
        }

        public static BagItemData CreateFromConfig(CurrencyBaseData config, int stackCount = 1, string itemId = null)
        {
            if (config == null)
                return null;

            var category = CurrencyConfigLoader.FindCategoryById(config.CurrencyCategoryId);
            var effectType = CurrencyConfigLoader.FindEffectTypeById(config.CurrencyEffectTypeId);

            int gridWidth = Mathf.Max(1, ParseInt(config.CurrencyGridWidth, 1));
            int gridHeight = Mathf.Max(1, ParseInt(config.CurrencyGridHeight, 1));
            int maxStackSize = Mathf.Max(1, ParseInt(config.CurrencyStackSize, 1));
            int requestedStackCount = Mathf.Max(1, stackCount);

            var bagData = new BagItemData(
                string.IsNullOrWhiteSpace(itemId) ? BuildGeneratedItemId(config.CurrencyCode ?? config.CurrencyBaseId, requestedStackCount) : itemId,
                string.IsNullOrWhiteSpace(config.CurrencyName) ? config.CurrencyCode : config.CurrencyName,
                gridWidth,
                gridHeight)
            {
                ItemKind = BagItemKind.Currency,
                Description = config.CurrencyDescription,
                IsStackable = true,
                StackCount = requestedStackCount,
                MaxStackCount = maxStackSize,

                CurrencyBaseId = config.CurrencyBaseId,
                CurrencyCode = config.CurrencyCode,
                CurrencyCategoryId = config.CurrencyCategoryId,
                CurrencyCategoryName = category?.CurrencyCategoryName,
                CurrencyDisplayColor = config.CurrencyDisplayColor,
                CurrencyEffectTypeId = config.CurrencyEffectTypeId,
                CurrencyEffectTypeName = effectType?.CurrencyEffectTypeName,
                CurrencyTargetDescription = config.CurrencyTargetDescription,
                CurrencyEffectDescription = config.CurrencyEffectDescription,
                CurrencyFlavorText = config.CurrencyFlavorText,
                CurrencyDropLevel = ParseInt(config.CurrencyDropLevel, 1),
                CurrencySortOrder = ParseInt(config.CurrencySortOrder, ParseInt(category?.CurrencyCategorySortOrder, 0)),
                CurrencyConsumesOnUse = ParseBool(effectType?.CurrencyConsumesOnUse, true),
                CurrencyCanApplyNormal = ParseBool(effectType?.CurrencyCanApplyNormal, false),
                CurrencyCanApplyMagic = ParseBool(effectType?.CurrencyCanApplyMagic, false),
                CurrencyCanApplyRare = ParseBool(effectType?.CurrencyCanApplyRare, false),
                CurrencyCanApplyUnique = ParseBool(effectType?.CurrencyCanApplyUnique, false),
                CurrencyCanApplyCorrupted = ParseBool(effectType?.CurrencyCanApplyCorrupted, false),
                ItemColor = ResolveDisplayColor(config.CurrencyDisplayColor),
                Icon = LoadIcon(config.CurrencyIconPath),
            };

            if (effectType?.CurrencyAllowedItemTypes != null)
            {
                for (int i = 0; i < effectType.CurrencyAllowedItemTypes.Count; i++)
                {
                    if (TryParseItemType(effectType.CurrencyAllowedItemTypes[i], out var itemType) && !bagData.CurrencyAllowedItemTypes.Contains(itemType))
                        bagData.CurrencyAllowedItemTypes.Add(itemType);
                }
            }

            bagData.RuntimeItemData = bagData.ToItemData();
            return bagData;
        }

        private static Sprite LoadIcon(string iconPath)
        {
            if (string.IsNullOrWhiteSpace(iconPath))
                return null;

            return Resources.Load<Sprite>(iconPath.Trim());
        }

        private static string BuildGeneratedItemId(string key, int stackCount)
        {
            return $"currency_{Sanitize(key)}_{stackCount}_{Guid.NewGuid():N}";
        }

        private static string Sanitize(string value)
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

        private static int ParseInt(string value, int fallback = 0)
        {
            return int.TryParse(value, out var parsed) ? parsed : fallback;
        }

        private static bool ParseBool(string value, bool fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            if (bool.TryParse(value, out var parsed))
                return parsed;

            return value == "1" ? true : value == "0" ? false : fallback;
        }

        private static bool TryParseItemType(string value, out ItemType itemType)
        {
            itemType = default;
            return !string.IsNullOrWhiteSpace(value) && Enum.TryParse(value.Trim(), true, out itemType);
        }

        private static Color ResolveDisplayColor(string htmlColor)
        {
            if (!string.IsNullOrWhiteSpace(htmlColor) && ColorUtility.TryParseHtmlString(htmlColor, out var parsed))
                return parsed;

            return new Color(0.82f, 0.72f, 0.34f);
        }
    }
}