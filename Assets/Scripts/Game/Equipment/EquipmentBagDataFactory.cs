using System;
using System.Collections.Generic;
using POELike.ECS.Components;
using POELike.Game.UI;
using UnityEngine;

namespace POELike.Game.Equipment
{
    /// <summary>
    /// 生成装备 -> 背包道具数据转换工具。
    /// 供商店、GM 和后续掉落逻辑共享，避免各处重复拼装 BagItemData。
    /// </summary>
    public static class EquipmentBagDataFactory
    {
        public static BagItemData CreateFromGeneratedEquipment(GeneratedEquipment equip, string itemId = null)
        {
            if (equip?.DetailType == null)
                return null;

            int width = Mathf.Max(1, ParseInt(equip.DetailType.EquipmentWidth, 1));
            int height = Mathf.Max(1, ParseInt(equip.DetailType.EquipmentHeight, 1));

            var bagData = new BagItemData(
                itemId: string.IsNullOrWhiteSpace(itemId) ? equip.DetailType.EquipmentDetailTypeId : itemId,
                name: equip.DisplayName,
                gridWidth: width,
                gridHeight: height)
            {
                ItemKind = BagItemKind.Equipment,
                ItemColor = equip.QualityColor,
            };

            PopulateEquipmentBagData(bagData, equip);
            return bagData;
        }

        public static void PopulateEquipmentBagData(BagItemData bagData, GeneratedEquipment equip)
        {
            if (bagData == null || equip == null)
                return;

            bagData.ItemKind = BagItemKind.Equipment;

            bagData.Sockets.Clear();
            if (equip.Sockets != null)
            {
                for (int i = 0; i < equip.Sockets.Count; i++)
                {
                    var socket = equip.Sockets[i];
                    if (socket == null)
                        continue;

                    bagData.Sockets.Add(new SocketData
                    {
                        Color = socket.Color,
                        LinkedToPrevious = socket.LinkedToPrevious,
                    });
                }
            }

            bagData.EquipmentMods.Clear();
            bagData.PrefixDescriptions.Clear();
            bagData.SuffixDescriptions.Clear();

            if (equip.Mods != null)
            {
                for (int i = 0; i < equip.Mods.Count; i++)
                {
                    var mod = equip.Mods[i];
                    if (mod?.Mod == null)
                        continue;

                    bagData.EquipmentMods.Add(mod);
                    bool isPrefix = mod.Mod.EquipmentModType == "1";
                    var targetDescriptions = isPrefix ? bagData.PrefixDescriptions : bagData.SuffixDescriptions;

                    if (mod.Values != null && mod.Values.Count > 0)
                    {
                        for (int valueIndex = 0; valueIndex < mod.Values.Count; valueIndex++)
                        {
                            string desc = BuildRolledModDescription(mod, mod.Values[valueIndex]);
                            if (!string.IsNullOrWhiteSpace(desc))
                                targetDescriptions.Add(desc);
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(mod.Mod.EquipmentModName))
                    {
                        targetDescriptions.Add(mod.Mod.EquipmentModName);
                    }
                }
            }

            var slots = ResolveEquipmentSlotsFromDetail(equip.DetailType);
            if (slots.Count > 0)
            {
                bagData.SetAcceptedEquipmentSlots(slots);
                bagData.AcceptedEquipmentSlot = slots[0];
            }
        }

        public static List<EquipmentSlot> ResolveEquipmentSlotsFromDetail(EquipmentDetailTypeData detail)
        {
            var result = new List<EquipmentSlot>();
            if (detail == null)
                return result;

            switch (ParseInt(detail.EquipmentPart, -1))
            {
                case 1:
                    result.Add(EquipmentSlot.MainHand);
                    return result;
                case 2:
                    result.Add(EquipmentSlot.OffHand);
                    return result;
                case 3:
                    result.Add(EquipmentSlot.Helmet);
                    return result;
                case 4:
                    result.Add(EquipmentSlot.BodyArmour);
                    return result;
                case 5:
                    result.Add(EquipmentSlot.Gloves);
                    return result;
                case 6:
                    result.Add(EquipmentSlot.Boots);
                    return result;
                case 7:
                    return ResolveAccessorySlots(detail);
            }

            return result;
        }

        private static List<EquipmentSlot> ResolveAccessorySlots(EquipmentDetailTypeData detail)
        {
            var result = new List<EquipmentSlot>();
            if (detail?.EquipmentTypes != null)
            {
                for (int i = 0; i < detail.EquipmentTypes.Count; i++)
                {
                    var subCategory = FindSubCategory(detail.EquipmentTypes[i]);
                    string subName = subCategory?.EquipmentSubCategoryName?.Trim();
                    if (string.IsNullOrWhiteSpace(subName))
                        continue;

                    if (subName.IndexOf("戒", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        subName.IndexOf("ring", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        result.Add(EquipmentSlot.RingLeft);
                        result.Add(EquipmentSlot.RingRight);
                        return result;
                    }

                    if (subName.IndexOf("项链", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        subName.IndexOf("护身符", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        subName.IndexOf("amulet", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        result.Add(EquipmentSlot.Amulet);
                        return result;
                    }

                    if (subName.IndexOf("腰带", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        subName.IndexOf("belt", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        result.Add(EquipmentSlot.Belt);
                        return result;
                    }
                }
            }

            result.Add(EquipmentSlot.RingLeft);
            result.Add(EquipmentSlot.RingRight);
            result.Add(EquipmentSlot.Amulet);
            result.Add(EquipmentSlot.Belt);
            return result;
        }

        private static EquipmentSubCategoryData FindSubCategory(int subCategoryId)
        {
            var subCategories = EquipmentConfigLoader.SubCategories;
            for (int i = 0; i < subCategories.Count; i++)
            {
                var sub = subCategories[i];
                if (sub != null && ParseInt(sub.EquipmentSubCategoryId, -1) == subCategoryId)
                    return sub;
            }

            return null;
        }

        private static string BuildRolledModDescription(RolledMod mod, RolledModValue value)
        {
            if (mod?.Mod == null)
                return string.Empty;

            if (value?.Config == null)
                return mod.Mod.EquipmentModName ?? string.Empty;

            if (string.IsNullOrWhiteSpace(value.Config.EquipmentModValueDesc))
                return mod.Mod.EquipmentModName ?? string.Empty;

            return $"{value.Config.EquipmentModValueDesc} {value.RolledValue}";
        }

        private static int ParseInt(string value, int fallback = 0)
        {
            return int.TryParse(value, out var parsed) ? parsed : fallback;
        }
    }
}