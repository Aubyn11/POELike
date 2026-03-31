using POELike.ECS.Core;

namespace POELike.ECS.Components
{
    /// <summary>
    /// 怪物组件
    /// 标记实体为怪物，并存储怪物配置数据
    /// </summary>
    public class MonsterComponent : IComponent
    {
        public bool IsEnabled { get; set; } = true;

        /// <summary>怪物唯一ID（来自MonstDataConf）</summary>
        public int MonsterID { get; set; }

        /// <summary>怪物 Mesh 预制体名称（对应 Resources/Monsters/ 下的 Bundle，如 TT_Halberdier）</summary>
        public string MonsterMesh { get; set; } = "";

        /// <summary>怪物最大HP</summary>
        public float MaxHp { get; set; } = 100f;

        /// <summary>怪物攻击力</summary>
        public float Attack { get; set; } = 10f;

        /// <summary>怪物护甲</summary>
        public float Defense { get; set; } = 0f;

        /// <summary>怪物移动速度</summary>
        public float MoveSpeed { get; set; } = 3.5f;

        /// <summary>怪物攻击半径</summary>
        public float AttackRange { get; set; } = 1.5f;

        /// <summary>怪物当前朝向角（Y 轴，度）</summary>
        public float FaceYaw { get; set; } = 0f;

        public void Reset()
        {
            MonsterID    = 0;
            MonsterMesh  = "";
            MaxHp        = 100f;
            Attack       = 10f;
            Defense      = 0f;
            MoveSpeed    = 3.5f;
            AttackRange  = 1.5f;
            FaceYaw      = 0f;
        }
    }
}
