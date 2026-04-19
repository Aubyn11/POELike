using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using POELike.ECS.Components;
using POELike.Game.UI;

namespace POELike.Game.Skills
{
    /// <summary>
    /// 技能工厂
    /// 创建预定义的技能数据
    /// </summary>
    public static class SkillFactory
    {
        /// <summary>
        /// 创建普通攻击
        /// </summary>
        public static SkillData CreateNormalAttack()
        {
            return CreateHeavyStrike();
        }

        public static SkillData CreateHeavyStrike()
        {
            return CreateConfiguredSkill(
                "heavy_strike",
                fallbackType: SkillType.Attack,
                fallbackManaCost: 6f,
                fallbackCooldown: 0f,
                fallbackCastTime: 0f,
                fallbackDamage: 15f,
                fallbackRange: 2f,
                fallbackAreaRadius: 0f,
                fallbackProjectileCount: 0,
                fallbackDuration: 0f,
                fallbackIsChannelingSkill: false,
                fallbackCanMoveWhileCasting: true,
                fallbackName: "重击",

                fallbackDescription: "用沉重一击打击单体目标",
                fallbackEffectName: "HeavyStrikeSlash");
        }
        
        /// <summary>
        /// 创建火球术
        /// </summary>
        public static SkillData CreateFireball()
        {
            return CreateConfiguredSkill(
                "fireball",
                fallbackType: SkillType.Projectile,
                fallbackManaCost: 15f,
                fallbackCooldown: 0.5f,
                fallbackCastTime: 0f,
                fallbackDamage: 22.5f,
                fallbackRange: 15f,
                fallbackAreaRadius: 2f,
                fallbackProjectileCount: 1,
                fallbackDuration: 0f,
                fallbackIsChannelingSkill: false,
                fallbackCanMoveWhileCasting: true,
                fallbackName: "火球术",

                fallbackDescription: "发射一枚火球，命中时爆炸造成范围火焰伤害",
                fallbackEffectName: "FireballCast");
        }
        
        /// <summary>
        /// 创建冰霜新星
        /// </summary>
        public static SkillData CreateFrostNova()
        {
            return CreateConfiguredSkill(
                "frost_nova",
                fallbackType: SkillType.AoE,
                fallbackManaCost: 25f,
                fallbackCooldown: 4f,
                fallbackCastTime: 0.3f,
                fallbackDamage: 32.5f,
                fallbackRange: 0f,
                fallbackAreaRadius: 4f,
                fallbackProjectileCount: 0,
                fallbackDuration: 0f,
                fallbackIsChannelingSkill: false,
                fallbackCanMoveWhileCasting: false,
                fallbackName: "冰霜新星",

                fallbackDescription: "在周围释放冰霜爆炸，冻结附近敌人",
                fallbackEffectName: "FrostNovaBurst");
        }
        
        /// <summary>
        /// 创建闪现
        /// </summary>
        public static SkillData CreateBlink()
        {
            return CreateConfiguredSkill(
                "blink",
                fallbackType: SkillType.Movement,
                fallbackManaCost: 20f,
                fallbackCooldown: 3f,
                fallbackCastTime: 0f,
                fallbackDamage: 0f,
                fallbackRange: 8f,
                fallbackAreaRadius: 0f,
                fallbackProjectileCount: 0,
                fallbackDuration: 0f,
                fallbackIsChannelingSkill: false,
                fallbackCanMoveWhileCasting: true,
                fallbackName: "闪现",

                fallbackDescription: "瞬间移动到目标位置",
                fallbackEffectName: "BlinkFlash");
        }
        
        /// <summary>
        /// 创建闪电链
        /// </summary>
        public static SkillData CreateLightningChain()
        {
            return new SkillData
            {
                Id = "lightning_chain",
                Name = "闪电链",
                Description = "发射闪电，在敌人之间弹跳",
                SkillEffectName = "LightningChainCast",
                Type = SkillType.Projectile,
                ManaCost = 20f,
                Cooldown = 0.8f,
                CastTime = 0f,
                Damage = 15f,
                Range = 20f,
                ProjectileCount = 1,
                IsChannelingSkill = false,
                CanMoveWhileCasting = true,
            };

        }
        
        /// <summary>
        /// 创建旋风斩
        /// </summary>
        public static SkillData CreateCyclone()
        {
            return new SkillData
            {
                Id = "cyclone",
                Name = "旋风斩",
                Description = "持续旋转攻击周围敌人",
                SkillEffectName = "CycloneSpin",
                Type = SkillType.Channeling,
                ManaCost = 5f,
                Cooldown = 0f,
                CastTime = 0f,
                Damage = 8f,
                Range = 0f,
                AreaRadius = 2.5f,
                Duration = 0.3f,    // 每次触发间隔
                IsChannelingSkill = true,
                CanMoveWhileCasting = true,
            };

        }

        private static SkillData CreateConfiguredSkill(
            string skillCode,
            SkillType fallbackType,
            float fallbackManaCost,
            float fallbackCooldown,
            float fallbackCastTime,
            float fallbackDamage,
            float fallbackRange,
            float fallbackAreaRadius,
            int fallbackProjectileCount,
            float fallbackDuration,
            bool fallbackIsChannelingSkill,
            bool fallbackCanMoveWhileCasting,
            string fallbackName,
            string fallbackDescription,
            string fallbackEffectName)

        {
            var config = SkillConfigLoader.GetActiveSkillByCode(skillCode);
            return new SkillData
            {
                Id = string.IsNullOrWhiteSpace(skillCode) ? fallbackName : skillCode,
                Name = string.IsNullOrWhiteSpace(config?.ActiveSkillStoneName) ? fallbackName : config.ActiveSkillStoneName,
                Description = string.IsNullOrWhiteSpace(config?.ActiveSkillStoneDesc) ? fallbackDescription : config.ActiveSkillStoneDesc,
                SkillEffectName = string.IsNullOrWhiteSpace(config?.SkillEffectName) ? fallbackEffectName : config.SkillEffectName,
                Type = fallbackType,
                ManaCost = fallbackManaCost,
                Cooldown = fallbackCooldown,
                CastTime = fallbackCastTime,
                Damage = fallbackDamage,
                Range = fallbackRange,
                AreaRadius = fallbackAreaRadius,
                ProjectileCount = Mathf.Max(1, fallbackProjectileCount),
                Duration = fallbackDuration,
                IsChannelingSkill = ReadConfigBool(config?.IsChannelingSkill, fallbackIsChannelingSkill || fallbackType == SkillType.Channeling),
                CanMoveWhileCasting = ReadConfigBool(config?.CanMoveWhileCasting, fallbackCanMoveWhileCasting),
            };

        }

        public static SkillData TryCreateSkillFromGem(BagItemData gemData)
        {
            if (gemData == null || !gemData.IsActiveSkillGem)
                return null;

            string key = NormalizeGemKey(gemData.ItemId, gemData.Name);
            if (string.IsNullOrWhiteSpace(key))
                return null;

            if (ContainsAny(key, "heavy", "strike", "重击", "攻击"))
                return CreateHeavyStrike();
            if (ContainsAny(key, "fireball", "fire", "火球"))
                return CreateFireball();
            if (ContainsAny(key, "frost_nova", "frost", "nova", "冰霜新星", "冰环"))
                return CreateFrostNova();
            if (ContainsAny(key, "blink", "闪现"))
                return CreateBlink();
            if (ContainsAny(key, "cyclone", "旋风"))
                return CreateCyclone();
            if (ContainsAny(key, "lightning_chain", "lightning", "闪电链"))
                return CreateLightningChain();

            var activeSkills = SkillConfigLoader.ActiveSkills;
            for (int i = 0; i < activeSkills.Count; i++)
            {
                var config = activeSkills[i];
                if (config == null)
                    continue;

                if (ContainsAny(key, config.ActiveSkillStoneCode, config.ActiveSkillStoneName, config.ActiveSkillStoneId))
                    return CreateSkillByCode(config.ActiveSkillStoneCode, config.ActiveSkillStoneName);
            }

            return null;
        }

        public static SupportGem TryCreateSupportGemFromGem(BagItemData gemData)
        {
            if (gemData == null || !gemData.IsSupportSkillGem)
                return null;

            string key = NormalizeGemKey(gemData.ItemId, gemData.Name);
            if (string.IsNullOrWhiteSpace(key))
                return null;

            if (ContainsAny(key, "multi", "projectile", "多重投射"))
                return CreateMultiProjectileGem(2);
            if (ContainsAny(key, "gmp", "大范围多重投射"))
            {
                return new SupportGem
                {
                    Id = "gmp",
                    Name = "高阶多重投射",
                    Type = SupportGemType.GMP,
                    Value = 4,
                };
            }
            if (ContainsAny(key, "lmp", "小范围多重投射"))
            {
                return new SupportGem
                {
                    Id = "lmp",
                    Name = "低阶多重投射",
                    Type = SupportGemType.LMP,
                    Value = 2,
                };
            }
            if (ContainsAny(key, "added_fire", "附加火", "附加火焰"))
                return CreateAddedFireDamageGem(15f);
            if (ContainsAny(key, "added_cold", "附加冰", "附加冰冷"))
            {
                return new SupportGem
                {
                    Id = "added_cold_damage",
                    Name = "附加冰冷伤害",
                    Type = SupportGemType.AddedColdDamage,
                    Value = 15f,
                };
            }
            if (ContainsAny(key, "added_lightning", "附加雷", "附加闪电"))
            {
                return new SupportGem
                {
                    Id = "added_lightning_damage",
                    Name = "附加闪电伤害",
                    Type = SupportGemType.AddedLightningDamage,
                    Value = 15f,
                };
            }
            if (ContainsAny(key, "aoe", "范围", "增加范围"))
            {
                return new SupportGem
                {
                    Id = "increased_aoe",
                    Name = "增加范围",
                    Type = SupportGemType.IncreasedAoE,
                    Value = 30f,
                };
            }
            if (ContainsAny(key, "concentrated", "集中"))
            {
                return new SupportGem
                {
                    Id = "concentrated_effect",
                    Name = "集中效应",
                    Type = SupportGemType.ConcentratedEffect,
                    Value = 20f,
                };
            }

            var supportSkills = SkillConfigLoader.SupportSkills;
            for (int i = 0; i < supportSkills.Count; i++)
            {
                var config = supportSkills[i];
                if (config == null)
                    continue;

                if (ContainsAny(key, config.SupportSkillStoneCode, config.SupportSkillStoneName, config.SupportSkillStoneId))
                    return CreateSupportGemByCode(config.SupportSkillStoneCode, config.SupportSkillStoneName);
            }

            return null;
        }
        
        /// <summary>
        /// 为技能添加支持宝石
        /// </summary>
        public static SkillData WithSupportGem(this SkillData skill, SupportGem gem)
        {
            skill.SupportGems.Add(gem);
            return skill;
        }
        
        /// <summary>
        /// 创建多重投射支持宝石
        /// </summary>
        public static SupportGem CreateMultiProjectileGem(int extraProjectiles = 2)
        {
            return new SupportGem
            {
                Id = "multi_projectile",
                Name = "多重投射",
                Type = SupportGemType.MultiProjectile,
                Value = extraProjectiles
            };
        }
        
        /// <summary>
        /// 创建附加火焰伤害支持宝石
        /// </summary>
        public static SupportGem CreateAddedFireDamageGem(float damage = 10f)
        {
            return new SupportGem
            {
                Id = "added_fire_damage",
                Name = "附加火焰伤害",
                Type = SupportGemType.AddedFireDamage,
                Value = damage
            };
        }

        private static SkillData CreateSkillByCode(string skillCode, string fallbackName = null)
        {
            if (string.IsNullOrWhiteSpace(skillCode))
                return null;

            string normalized = skillCode.Trim().ToLowerInvariant();
            return normalized switch
            {
                "heavy_strike" => CreateHeavyStrike(),
                "fireball" => CreateFireball(),
                "frost_nova" => CreateFrostNova(),
                "blink" => CreateBlink(),
                "cyclone" => CreateCyclone(),
                "lightning_chain" => CreateLightningChain(),
                _ => CreateConfiguredFallbackSkill(skillCode, fallbackName),
            };
        }

        private static SkillData CreateConfiguredFallbackSkill(string skillCode, string fallbackName)
        {
            var config = SkillConfigLoader.GetActiveSkillByCode(skillCode);
            if (config == null)
                return null;

            string castType = config.SkillCastType?.Trim().ToLowerInvariant() ?? string.Empty;
            SkillType skillType = castType switch
            {
                "projectile" => SkillType.Projectile,
                "aoe" => SkillType.AoE,
                "movement" => SkillType.Movement,
                "channeling" => SkillType.Channeling,
                _ => SkillType.Spell,
            };

            return new SkillData
            {
                Id = string.IsNullOrWhiteSpace(config.ActiveSkillStoneCode) ? skillCode : config.ActiveSkillStoneCode,
                Name = string.IsNullOrWhiteSpace(config.ActiveSkillStoneName) ? fallbackName ?? skillCode : config.ActiveSkillStoneName,
                Description = config.ActiveSkillStoneDesc,
                SkillEffectName = config.SkillEffectName,
                Type = skillType,
                ManaCost = 10f,
                Cooldown = skillType == SkillType.Movement ? 3f : 0.5f,
                CastTime = skillType == SkillType.AoE ? 0.2f : 0f,
                Damage = skillType == SkillType.Channeling ? 8f : 20f,
                Range = skillType == SkillType.Movement ? 8f : 12f,
                AreaRadius = skillType == SkillType.AoE ? 3f : 0f,
                ProjectileCount = 1,
                Duration = skillType == SkillType.Channeling ? 0.3f : 0f,
                IsChannelingSkill = ReadConfigBool(config.IsChannelingSkill, skillType == SkillType.Channeling),
                CanMoveWhileCasting = ReadConfigBool(config.CanMoveWhileCasting, skillType != SkillType.AoE),
            };

        }

        private static SupportGem CreateSupportGemByCode(string skillCode, string fallbackName = null)
        {
            if (string.IsNullOrWhiteSpace(skillCode))
                return null;

            string normalized = skillCode.Trim().ToLowerInvariant();
            if (normalized.Contains("multi") || normalized.Contains("projectile"))
                return CreateMultiProjectileGem(2);
            if (normalized.Contains("fire"))
                return CreateAddedFireDamageGem(15f);
            if (normalized.Contains("cold"))
            {
                return new SupportGem
                {
                    Id = skillCode,
                    Name = string.IsNullOrWhiteSpace(fallbackName) ? "附加冰冷伤害" : fallbackName,
                    Type = SupportGemType.AddedColdDamage,
                    Value = 15f,
                };
            }
            if (normalized.Contains("lightning"))
            {
                return new SupportGem
                {
                    Id = skillCode,
                    Name = string.IsNullOrWhiteSpace(fallbackName) ? "附加闪电伤害" : fallbackName,
                    Type = SupportGemType.AddedLightningDamage,
                    Value = 15f,
                };
            }
            if (normalized.Contains("aoe") || normalized.Contains("area"))
            {
                return new SupportGem
                {
                    Id = skillCode,
                    Name = string.IsNullOrWhiteSpace(fallbackName) ? "增加范围" : fallbackName,
                    Type = SupportGemType.IncreasedAoE,
                    Value = 30f,
                };
            }
            if (normalized.Contains("concentrated"))
            {
                return new SupportGem
                {
                    Id = skillCode,
                    Name = string.IsNullOrWhiteSpace(fallbackName) ? "集中效应" : fallbackName,
                    Type = SupportGemType.ConcentratedEffect,
                    Value = 20f,
                };
            }

            return null;
        }

        private static string NormalizeGemKey(string itemId, string itemName)
        {
            string combined = $"{itemId} {itemName}".Trim();
            return string.IsNullOrWhiteSpace(combined) ? string.Empty : combined.ToLowerInvariant();
        }

        private static bool ContainsAny(string source, params string[] keywords)
        {
            if (string.IsNullOrWhiteSpace(source) || keywords == null)
                return false;

            for (int i = 0; i < keywords.Length; i++)
            {
                var keyword = keywords[i];
                if (!string.IsNullOrWhiteSpace(keyword) && source.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static bool ReadConfigBool(string rawValue, bool fallback)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
                return fallback;

            string normalized = rawValue.Trim();
            if (bool.TryParse(normalized, out bool boolValue))
                return boolValue;
            if (int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue))
                return intValue != 0;

            if (string.Equals(normalized, "yes", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "y", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "on", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(normalized, "no", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "n", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "off", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return fallback;
        }

    }
}
