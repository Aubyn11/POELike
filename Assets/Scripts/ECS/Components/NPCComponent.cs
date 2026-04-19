using POELike.ECS.Core;

namespace POELike.ECS.Components
{
    /// <summary>
    /// NPC组件
    /// 标记实体为NPC，并存储NPC配置数据
    /// </summary>
    public class NPCComponent : IComponent
    {
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// NPC唯一ID（来自NPCDataConf）
        /// </summary>
        public int NPCID { get; set; }

        /// <summary>
        /// NPC名称
        /// </summary>
        public string NPCName { get; set; } = "";

        /// <summary>
        /// NPC Mesh 预制体名称（对应 Resources/Prefabs/ 下的预制体，如 TT_Archer）
        /// </summary>
        public string NPCMesh { get; set; } = "";

        /// <summary>
        /// NPC 所属场景名称（来自 NPCDataConf.SceneName）
        /// </summary>
        public string SceneName { get; set; } = "";

        /// <summary>
        /// 鼠标是否悬停在NPC上（由NpcMarkerRenderer每帧更新）
        /// </summary>
        public bool IsHovered { get; set; } = false;

        /// <summary>
        /// NPC 当前朝向角（Y 轴，度）。由 NpcMeshRenderer 每帧平滑更新。
        /// </summary>
        public float FaceYaw { get; set; } = 0f;

        public void Reset()
        {
            NPCID     = 0;
            NPCName   = "";
            NPCMesh   = "";
            SceneName = "";
            IsHovered = false;
            FaceYaw   = 0f;
        }
    }
}
