using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using POELike.ECS.Core;
using POELike.ECS.Components;

namespace POELike.Game
{
    public struct NPCSpawnRequest
    {
        public int NPCID;
        public Vector3 Position;
        public bool UseFixedConfigPosition;
    }

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
        /// 从配置文件加载当前场景可见的 NPC，并按配置固定坐标创建实体
        /// </summary>
        public static List<Entity> SpawnAllNPCs(World world, string sceneName)
        {
            var entities = new List<Entity>();
            if (world == null)
                return entities;

            var npcDataList = LoadNPCData();
            if (npcDataList == null || npcDataList.Count == 0)
            {
                Debug.LogWarning("[NPCSpawner] NPCDataConf.pb 中没有NPC数据");
                return entities;
            }

            foreach (var data in npcDataList)
            {
                if (!MatchesScene(data, sceneName))
                    continue;

                var entity = CreateNPCEntity(world, data);
                if (entity != null)
                    entities.Add(entity);
            }

            Debug.Log($"[NPCSpawner] 场景 {sceneName} 共创建 {entities.Count} 个NPC实体");
            return entities;
        }

        /// <summary>
        /// 根据 NPCID 模板与指定坐标创建当前场景中的 NPC 实体。
        /// 当请求显式要求使用固定配置坐标时，会优先采用 NPCDataConf 中的 NPCPosition。
        /// </summary>
        public static List<Entity> SpawnNPCs(World world, IReadOnlyList<NPCSpawnRequest> requests, string sceneName)
        {
            var entities = new List<Entity>();
            if (world == null || requests == null || requests.Count == 0)
                return entities;

            var npcDataList = LoadNPCData();
            if (npcDataList == null || npcDataList.Count == 0)
            {
                Debug.LogWarning("[NPCSpawner] NPCDataConf.pb 中没有NPC数据");
                return entities;
            }

            var templateMap = new Dictionary<int, NPCData>();
            foreach (var data in npcDataList)
            {
                if (data == null || data.NPCIDInt <= 0)
                    continue;

                templateMap[data.NPCIDInt] = data;
            }

            foreach (var request in requests)
            {
                if (!templateMap.TryGetValue(request.NPCID, out var template))
                {
                    Debug.LogWarning($"[NPCSpawner] 找不到 NPCID={request.NPCID} 的模板配置，已跳过地图布局刷 NPC 请求");
                    continue;
                }

                if (!MatchesScene(template, sceneName))
                {
                    Debug.Log($"[NPCSpawner] NPCID={request.NPCID} 属于场景 {template.SceneName}，当前场景 {sceneName} 已跳过。");
                    continue;
                }

                Vector3? overridePosition = request.UseFixedConfigPosition ? null : request.Position;
                var entity = CreateNPCEntity(world, template, overridePosition);
                if (entity != null)
                    entities.Add(entity);
            }

            Debug.Log($"[NPCSpawner] 场景 {sceneName} 按请求创建 {entities.Count} 个NPC实体");
            return entities;
        }

        private static bool MatchesScene(NPCData data, string sceneName)
        {
            if (data == null)
                return false;

            if (string.IsNullOrWhiteSpace(data.SceneName) || string.IsNullOrWhiteSpace(sceneName))
                return true;

            string[] sceneNames = data.SceneName.Split(',');
            foreach (string candidate in sceneNames)
            {
                string normalized = candidate?.Trim();
                if (string.IsNullOrWhiteSpace(normalized))
                    continue;

                if (string.Equals(normalized, "All", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(normalized, sceneName.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 创建单个NPC的ECS实体
        /// </summary>
        private static Entity CreateNPCEntity(World world, NPCData data, Vector3? overridePosition = null)
        {
            Vector3 spawnPosition = overridePosition ?? data.Position;
            var entity = world.CreateEntity("NPC");

            entity.AddComponent(new TransformComponent
            {
                Position = spawnPosition
            });

            entity.AddComponent(new NPCComponent
            {
                NPCID = data.NPCIDInt,
                NPCName = data.NPCName,
                NPCMesh = data.NPCMesh ?? "",
                SceneName = data.SceneName ?? ""
            });

            Debug.Log($"[NPCSpawner] 创建NPC实体: ID={data.NPCIDInt}, 名称={data.NPCName}, 场景={data.SceneName}, 位置={spawnPosition}");
            return entity;
        }

        /// <summary>
        /// 从 Assets/Cfg/NPCDataConf.pb 读取NPC配置数据
        /// </summary>
        private static List<NPCData> LoadNPCData()
        {
            string json = null;

            var textAsset = Resources.Load<TextAsset>("Cfg/NPCDataConf");
            if (textAsset != null)
            {
                json = textAsset.text;
            }
            else
            {
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
            public string NPCMesh;
            public string SceneName;

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
                    return new Vector3(x, 0.5f, z);
                }
            }
        }
    }
}
