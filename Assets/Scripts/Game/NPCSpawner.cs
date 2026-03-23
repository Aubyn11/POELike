using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using POELike.ECS.Core;
using POELike.ECS.Components;

namespace POELike.Game
{
    /// <summary>
    /// NPC生成器
    /// 从 Assets/Cfg/NPCDataConf.pb 读取NPC配置，创建ECS实体
    /// NPC实体：TransformComponent + NPCComponent（无GameObject，纯逻辑）
    /// 角色与NPC产生碰撞（通过碰撞半径检测，在MovementSystem中处理）
    /// </summary>
    public static class NPCSpawner
    {
        // NPC碰撞半径（世界单位）
        public const float CollisionRadius = 0.8f;

        /// <summary>
        /// 从配置文件加载NPC数据并在ECS世界中创建实体
        /// </summary>
        public static List<Entity> SpawnAllNPCs(World world)
        {
            var entities = new List<Entity>();

            // 读取 NPCDataConf.pb（JSON格式）
            var npcDataList = LoadNPCData();
            if (npcDataList == null || npcDataList.Count == 0)
            {
                Debug.LogWarning("[NPCSpawner] NPCDataConf.pb 中没有NPC数据");
                return entities;
            }

            foreach (var data in npcDataList)
            {
                var entity = CreateNPCEntity(world, data);
                if (entity != null)
                    entities.Add(entity);
            }

            Debug.Log($"[NPCSpawner] 共创建 {entities.Count} 个NPC实体");
            return entities;
        }

        /// <summary>
        /// 创建单个NPC的ECS实体
        /// </summary>
        private static Entity CreateNPCEntity(World world, NPCData data)
        {
            var entity = world.CreateEntity("NPC");

            // 变换组件（纯逻辑，不绑定Unity Transform）
            entity.AddComponent(new TransformComponent
            {
                Position = data.Position
            });

            // NPC组件
            entity.AddComponent(new NPCComponent
            {
                NPCID   = data.NPCIDInt,
                NPCName = data.NPCName
            });

            Debug.Log($"[NPCSpawner] 创建NPC实体: ID={data.NPCIDInt}, 名称={data.NPCName}, 位置={data.Position}");
            return entity;
        }

        /// <summary>
        /// 从 Assets/Cfg/NPCDataConf.pb 读取NPC配置数据
        /// </summary>
        private static List<NPCData> LoadNPCData()
        {
            string json = null;

            // 优先从 Resources/Cfg 加载（需要文件在 Resources/Cfg/ 目录下）
            var textAsset = Resources.Load<TextAsset>("Cfg/NPCDataConf");
            if (textAsset != null)
            {
                json = textAsset.text;
            }
            else
            {
                // 直接读取 Assets/Cfg/NPCDataConf.pb
                string path = Path.Combine(Application.dataPath, "Cfg", "NPCDataConf.pb");
                if (!File.Exists(path))
                {
                    Debug.LogError($"[NPCSpawner] 找不到配置文件: {path}");
                    return null;
                }
                json = File.ReadAllText(path);
            }

            try
            {
                var wrapper = JsonUtility.FromJson<NPCDataWrapper>(json);
                return wrapper?.NPCDataConf ?? new List<NPCData>();
            }
            catch (Exception e)
            {
                Debug.LogError($"[NPCSpawner] 解析NPCDataConf.pb失败: {e.Message}");
                return null;
            }
        }

        // ── 数据模型 ──────────────────────────────────────────────────

        [Serializable]
        private class NPCDataWrapper
        {
            public List<NPCData> NPCDataConf;
        }

        [Serializable]
        private class NPCData
        {
            public string NPCID;
            public string NPCName;
            public string NPCPosition;

            /// <summary>
            /// 将 NPCID 字符串转为 int
            /// </summary>
            public int NPCIDInt => int.TryParse(NPCID, out int id) ? id : 0;

            /// <summary>
            /// 将 "x,y,z" 字符串解析为 Vector3
            /// </summary>
            public Vector3 Position
            {
                get
                {
                    if (string.IsNullOrEmpty(NPCPosition)) return Vector3.zero;
                    var parts = NPCPosition.Split(',');
                    if (parts.Length < 3) return Vector3.zero;
                    float.TryParse(parts[0].Trim(), out float x);
                    float.TryParse(parts[1].Trim(), out float y);
                    float.TryParse(parts[2].Trim(), out float z);
                    // NPC站在地面上，y固定为0.5（与玩家同高）
                    return new Vector3(x, 0.5f, z);
                }
            }
        }
    }
}
