using System;
using System.Collections.Generic;
using UnityEngine;

namespace POELike.Game
{
    /// <summary>
    /// 单个 NPC 预制体中一个部件的 Mesh + Material 引用
    /// </summary>
    [Serializable]
    public class NpcMeshPart
    {
        [Tooltip("部件名称（来自预制体子 GameObject 名称）")]
        public string PartName;

        [Tooltip("部件 Mesh")]
        public Mesh Mesh;

        [Tooltip("部件材质")]
        public Material Material;

        [Tooltip("部件相对于预制体根节点的本地变换矩阵（保留各部件的位置/旋转/缩放偏移）")]
        public Matrix4x4 LocalMatrix;
    }

    /// <summary>
    /// NPC Mesh 数据包（ScriptableObject）
    /// 由编辑器工具从预制体中提取，存放在 Resources/Prefabs/ 目录下。
    /// 运行时 NpcMeshRenderer 通过 Resources.Load 加载此资产，
    /// 只包含 Mesh + Material 引用，不含动画、碰撞体等冗余数据，节省内存。
    /// </summary>
    [CreateAssetMenu(fileName = "NpcMeshBundle", menuName = "POELike/NPC Mesh Bundle")]
    public class NpcMeshBundle : ScriptableObject
    {
        [Tooltip("对应的预制体名称，如 TT_Archer")]
        public string PrefabName;

        [Tooltip("该 NPC 的所有渲染部件")]
        public List<NpcMeshPart> Parts = new List<NpcMeshPart>();
    }
}
