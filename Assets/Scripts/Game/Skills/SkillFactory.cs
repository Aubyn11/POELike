using System.Collections.Generic;
using UnityEngine;
using POELike.ECS.Components;

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
            return new SkillData
            {
                Id = "normal_attack",
                Name = "普通攻击",
                Description = "对目标造成物理伤害",
                Type = SkillType.Attack,
                ManaCost = 0f,
                Cooldown = 0f,
                CastTime = 0f,
                Damage = 0f,    // 使用角色基础伤害
                Range = 2f,
            };
        }
        
        /// <summary>
        /// 创建火球术
        /// </summary>
        public static SkillData CreateFireball()
        {
            return new SkillData
            {
                Id = "fireball",
                Name = "火球术",
                Description = "发射一枚火球，命中时爆炸造成范围火焰伤害",
                Type = SkillType.Projectile,
                ManaCost = 15f,
                Cooldown = 0.5f,
                CastTime = 0f,
                Damage = 20f,
                Range = 15f,
                AreaRadius = 2f,
                ProjectileCount = 1,
            };
        }
        
        /// <summary>
        /// 创建冰霜新星
        /// </summary>
        public static SkillData CreateFrostNova()
        {
            return new SkillData
            {
                Id = "frost_nova",
                Name = "冰霜新星",
                Description = "在周围释放冰霜爆炸，冻结附近敌人",
                Type = SkillType.AoE,
                ManaCost = 25f,
                Cooldown = 4f,
                CastTime = 0.3f,
                Damage = 30f,
                Range = 0f,
                AreaRadius = 4f,
            };
        }
        
        /// <summary>
        /// 创建闪现
        /// </summary>
        public static SkillData CreateBlink()
        {
            return new SkillData
            {
                Id = "blink",
                Name = "闪现",
                Description = "瞬间移动到目标位置",
                Type = SkillType.Movement,
                ManaCost = 20f,
                Cooldown = 3f,
                CastTime = 0f,
                Damage = 0f,
                Range = 8f,
            };
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
                Type = SkillType.Projectile,
                ManaCost = 20f,
                Cooldown = 0.8f,
                CastTime = 0f,
                Damage = 15f,
                Range = 20f,
                ProjectileCount = 1,
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
                Type = SkillType.Channeling,
                ManaCost = 5f,
                Cooldown = 0f,
                CastTime = 0f,
                Damage = 8f,
                Range = 0f,
                AreaRadius = 2.5f,
                Duration = 0.3f,    // 每次触发间隔
            };
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
    }
}
