using UnityEngine;
using POELike.ECS.Core;
using POELike.ECS.Systems;

namespace POELike.ECS.Components
{
    /// <summary>
    /// 技能运行时组件。
    /// 由施法事件生成，驱动 GPU 范围显示与命中结算。
    /// </summary>
    public class SkillRuntimeComponent : IComponent
    {
        public bool IsEnabled { get; set; } = true;

        /// <summary>施法者实体</summary>
        public Entity Caster { get; set; }

        /// <summary>技能数据快照</summary>
        public SkillData Skill { get; set; }

        /// <summary>该运行时技能造成的伤害类型</summary>
        public DamageType DamageType { get; set; } = DamageType.Physical;

        /// <summary>单次命中基础伤害</summary>
        public float Damage { get; set; } = 1f;

        /// <summary>命中半径（XZ 平面）</summary>
        public float AreaRadius { get; set; } = 1f;

        /// <summary>剩余生命周期</summary>
        public float RemainingTime { get; set; } = 0.3f;

        /// <summary>总生命周期</summary>
        public float TotalLifetime { get; set; } = 0.3f;

        /// <summary>持续技能的命中间隔</summary>
        public float TickInterval { get; set; } = 0.15f;

        /// <summary>下一次命中倒计时</summary>
        public float TickTimer { get; set; } = 0f;

        /// <summary>预热时间（例如投射物飞行后再爆炸）</summary>
        public float WarmupRemaining { get; set; } = 0f;

        /// <summary>是否只在生命周期内结算一次命中</summary>
        public bool SingleImpact { get; set; } = true;

        /// <summary>单次命中技能是否已经结算过</summary>
        public bool HasTriggered { get; set; } = false;

        /// <summary>是否跟随施法者移动（如旋风斩）</summary>
        public bool FollowCaster { get; set; } = false;

        /// <summary>GPU 显示颜色</summary>
        public Color DisplayColor { get; set; } = new Color(1.0f, 0.45f, 0.15f, 0.35f);

        /// <summary>运行时临时 Prefab 标识，仅用于客户端拓展调试与展示</summary>
        public string RuntimePrefabKey { get; set; } = string.Empty;

        /// <summary>最近一次用于 GPU 命中结算的中心点</summary>
        public Vector3 LastResolvedCenter { get; set; } = Vector3.zero;

        public void Reset()
        {
            Caster = null;
            Skill = null;
            DamageType = DamageType.Physical;
            Damage = 1f;
            AreaRadius = 1f;
            RemainingTime = 0.3f;
            TotalLifetime = 0.3f;
            TickInterval = 0.15f;
            TickTimer = 0f;
            WarmupRemaining = 0f;
            SingleImpact = true;
            HasTriggered = false;
            FollowCaster = false;
            DisplayColor = new Color(1.0f, 0.45f, 0.15f, 0.35f);
            RuntimePrefabKey = string.Empty;
            LastResolvedCenter = Vector3.zero;
        }
    }
}
