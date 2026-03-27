using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using POELike.ECS.Core;
using POELike.ECS.Components;

namespace POELike.Game
{
    /// <summary>
    /// 怪物生成器
    /// 从 Assets/Cfg/MonstDataConf.pb 读取怪物配置，创建 ECS 实体
    /// 怪物实体：TransformComponent + MonsterComponent + HealthComponent
    /// </summary>
    public static class MonsterSpawner
    {
        // 怪物碰撞半径（世界单位）
        public const float CollisionRadius = 0.8f;

        // ── 配置缓存 ──────────────────────────────────────────────────
        private static Dictionary<int, MonsterData> _configCache;

        /// <summary>
        /// 获取所有怪物配置（懒加载）
        /// </summary>
        public static Dictionary<int, MonsterData> GetAllConfigs()
        {
            if (_configCache != null) return _configCache;

            _configCache = new Dictionary<int, MonsterData>();
            var list = LoadMonsterData();
            if (list == null) return _configCache;

            foreach (var data in list)
                _configCache[data.MonsterIDInt] = data;

            return _configCache;
        }

        /// <summary>
        /// 根据 MonsterID 和数量，在玩家附近生成怪物实体
        /// </summary>
        /// <param name="world">ECS 世界</param>
        /// <param name="monsterId">怪物配置 ID</param>
        /// <param name="count">生成数量</param>
        /// <param name="centerPos">生成中心位置（通常为玩家位置）</param>
        /// <returns>生成的实体列表</returns>
        public static List<Entity> SpawnMonsters(World world, int monsterId, int count, Vector3 centerPos)
        {
            var entities = new List<Entity>();

            var configs = GetAllConfigs();
            if (!configs.TryGetValue(monsterId, out var data))
            {
                Debug.LogWarning($"[MonsterSpawner] 找不到 MonsterID={monsterId} 的配置");
                return entities;
            }

            // 使用向日葵螺旋分布，保持均匀密度，不随数量无限扩散
            // 每圈约 6.28 个单位面积放一只，间距约 1.2f
            const float spacing = 1.2f;
            const float goldenAngle = 137.508f * Mathf.Deg2Rad; // 黄金角

            for (int i = 0; i < count; i++)
            {
                float r     = spacing * Mathf.Sqrt(i + 0.5f);
                float angle = i * goldenAngle;
                Vector3 offset = new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
                Vector3 spawnPos = centerPos + offset;
                spawnPos.y = 0.5f;

                var entity = CreateMonsterEntity(world, data, spawnPos);
                if (entity != null)
                    entities.Add(entity);
            }

            Debug.Log($"[MonsterSpawner] 生成 {entities.Count} 只怪物（ID={monsterId}, Mesh={data.MonsterMesh}）");
            return entities;
        }

        /// <summary>
        /// 创建单个怪物 ECS 实体
        /// </summary>
        private static Entity CreateMonsterEntity(World world, MonsterData data, Vector3 position)
        {
            var entity = world.CreateEntity("Monster");

            entity.AddComponent(new TransformComponent
            {
                Position = position
            });

            entity.AddComponent(new MonsterComponent
            {
                MonsterID   = data.MonsterIDInt,
                MonsterMesh = data.MonsterMesh ?? "",
                MaxHp       = data.MonsterHpFloat,
                FaceYaw     = UnityEngine.Random.Range(0f, 360f)
            });

            var healthComp = entity.AddComponent(new HealthComponent());
            healthComp.MaxHealth = data.MonsterHpFloat;
            healthComp.FillToMax();

            return entity;
        }

        /// <summary>
        /// 从 Assets/Cfg/MonstDataConf.pb 读取怪物配置数据
        /// </summary>
        private static List<MonsterData> LoadMonsterData()
        {
            string json = null;

            var textAsset = Resources.Load<TextAsset>("Cfg/MonstDataConf");
            if (textAsset != null)
            {
                json = textAsset.text;
            }
            else
            {
                string path = Path.Combine(Application.dataPath, "Cfg", "MonstDataConf.pb");
                if (!File.Exists(path))
                {
                    Debug.LogError($"[MonsterSpawner] 找不到配置文件: {path}");
                    return null;
                }
                json = File.ReadAllText(path);
            }

            try
            {
                var wrapper = JsonUtility.FromJson<MonsterDataWrapper>(json);
                return wrapper?.MonstDataConf ?? new List<MonsterData>();
            }
            catch (Exception e)
            {
                Debug.LogError($"[MonsterSpawner] 解析 MonstDataConf.pb 失败: {e.Message}");
                return null;
            }
        }

        // ── 数据模型 ──────────────────────────────────────────────────

        [Serializable]
        private class MonsterDataWrapper
        {
            public List<MonsterData> MonstDataConf;
        }

        [Serializable]
        public class MonsterData
        {
            public string MonsterID;
            public string MonsterMesh;
            public string MonsterHp;

            public int MonsterIDInt => int.TryParse(MonsterID, out int id) ? id : 0;
            public float MonsterHpFloat => float.TryParse(MonsterHp, out float hp) ? hp : 100f;
        }
    }
}
