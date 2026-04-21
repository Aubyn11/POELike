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
    /// 怪物实体：TransformComponent + MonsterComponent + StatsComponent + HealthComponent + AIComponent + MovementComponent
    /// </summary>
    public static class MonsterSpawner

    {
        // 怪物碰撞半径（世界单位）
        public const float CollisionRadius = 0.8f;

        private const float DefaultHp          = 100f;
        private const float DefaultAttack      = 10f;
        private const float DefaultDefense     = 0f;
        private const float DefaultMoveSpeed   = 3.5f;
        private const float DefaultAttackRange = 1.5f;
        private const float DefaultDetectionRange = 10f;
        private const float DefaultAttackDuration = 0.5f;
        private const float DefaultAttackInterval = 1.5f;

        // ── 配置缓存 ──────────────────────────────────────────────────
        private static Dictionary<int, MonsterData> _configCache;

        /// <summary>
        /// 获取所有怪物配置（懒加载）
        /// </summary>
        public static Dictionary<int, MonsterData> GetAllConfigs(bool forceReload = false)
        {
            if (!forceReload && _configCache != null) return _configCache;

            _configCache = new Dictionary<int, MonsterData>();
            var list = LoadMonsterData();
            if (list == null) return _configCache;

            foreach (var data in list)
                _configCache[data.MonsterIDInt] = data;

            return _configCache;
        }

        public static bool TryGetConfig(int monsterId, out MonsterData data, bool forceReload = false)
        {
            return GetAllConfigs(forceReload).TryGetValue(monsterId, out data);
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

            // 生成时强制刷新配置，确保运行中修改 MonstDataConf.pb 后下一次生成即可生效
            if (!TryGetConfig(monsterId, out var data, forceReload: true))
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
            float maxHp       = Mathf.Max(1f, data.MonsterHpFloat);
            float attack      = Mathf.Max(1f, data.MonsterAttackFloat);
            float defense     = Mathf.Max(0f, data.MonsterDefenseFloat);
            float moveSpeed   = Mathf.Max(0.1f, data.MonsterSpeedFloat);
            // `MonsterRadius` 是配置里的体型/半径字段，不应直接映射为 AI 攻击距离。
            // 如果没有显式攻击距离配置，当前回退默认近战距离，避免怪物在玩家周围远距离停住并抖动。
            float attackRange = Mathf.Max(0.1f, data.MonsterAttackRangeFloat);
            float detectionRange = Mathf.Max(0f, data.MonsterDetectionRangeFloat);
            float attackDuration = Mathf.Max(0f, data.MonsterAttackDurationFloat);
            float attackInterval = Mathf.Max(0f, data.MonsterAttackIntervalFloat);

            var entity = world.CreateEntity("Monster");

            entity.AddComponent(new TransformComponent
            {
                Position = position
            });

            entity.AddComponent(new MonsterComponent
            {
                MonsterID   = data.MonsterIDInt,
                MonsterMesh = data.MonsterMesh ?? "",
                MaxHp       = maxHp,
                Attack      = attack,
                Defense     = defense,
                MoveSpeed   = moveSpeed,
                AttackRange = attackRange,
                AttackDuration = attackDuration,
                AttackInterval = attackInterval,
                FaceYaw     = UnityEngine.Random.Range(0f, 360f)
            });

            var statsComp = entity.AddComponent(new StatsComponent());
            statsComp.SetBaseStat(StatType.MaxHealth, maxHp);
            statsComp.SetBaseStat(StatType.PhysicalDamage, attack);
            statsComp.SetBaseStat(StatType.Armor, defense);
            statsComp.SetBaseStat(StatType.MovementSpeed, moveSpeed);

            var healthComp = entity.AddComponent(new HealthComponent());
            healthComp.MaxHealth = maxHp;
            healthComp.FillToMax();

            // AI 组件：检测到玩家后追击，超出范围随机巡逻
            entity.AddComponent(new AIComponent
            {

                DetectionRange = detectionRange,
                ChaseRange     = 100f,  // 追击放弃距离
                AttackRange    = attackRange,
                AttackDuration = attackDuration,
                AttackInterval = attackInterval,
                AttackCooldown = attackDuration + attackInterval,
                SpawnPoint     = position,
            });

            // 移动组件：纯逻辑移动（无 CharacterController / Rigidbody）
            entity.AddComponent(new MovementComponent
            {
                BaseSpeed    = moveSpeed,
                CurrentSpeed = moveSpeed,
                UseGravity   = false,   // GPU 渲染怪物不需要物理重力
            });

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
            public string MonsterAttack;
            public string MonsterDefense;
            public string MonsterSpeed;
            public string MonsterRadius;
            public string MonsterAttackRange;
            public string MonsterDetectionRange;
            public string MonsterAttackDuration;
            public string MonsterAttackInterval;
            public string MonsterSpawnTriggerRadius;

            public int MonsterIDInt => int.TryParse(MonsterID, out int id) ? id : 0;
            public float MonsterHpFloat => ParseFloat(MonsterHp, DefaultHp);
            public float MonsterAttackFloat => ParseFloat(MonsterAttack, DefaultAttack);
            public float MonsterDefenseFloat => ParseFloat(MonsterDefense, DefaultDefense);
            public float MonsterSpeedFloat => ParseFloat(MonsterSpeed, DefaultMoveSpeed);
            public float MonsterRadiusFloat => ParseFloat(MonsterRadius, CollisionRadius);
            public float MonsterAttackRangeFloat => ParseFloat(MonsterAttackRange, DefaultAttackRange);
            public float MonsterDetectionRangeFloat => ParseFloat(MonsterDetectionRange, DefaultDetectionRange);
            public float MonsterAttackDurationFloat => ParseFloat(MonsterAttackDuration, DefaultAttackDuration);
            public float MonsterAttackIntervalFloat => ParseFloat(MonsterAttackInterval, DefaultAttackInterval);
            public float MonsterSpawnTriggerRadiusFloat => ParseFloat(MonsterSpawnTriggerRadius, MonsterDetectionRangeFloat);

            private static float ParseFloat(string value, float fallback)
            {
                return float.TryParse(value, out float parsed) ? parsed : fallback;
            }
        }

    }
}