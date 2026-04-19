using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace POELike.Game
{
    [Serializable]
    public class MapPlayerSpawnData
    {
        public string CfgID;
        public string OffsetX;
        public string OffsetZ;

        public int CfgIDInt => int.TryParse(CfgID, out int id) ? id : 0;
        public float OffsetXFloat => ParseFloat(OffsetX);
        public float OffsetZFloat => ParseFloat(OffsetZ);

        private static float ParseFloat(string value)
        {
            return float.TryParse(value, out float parsed) ? parsed : 0f;
        }
    }

    [Serializable]
    public class MapNpcLayoutData
    {
        public string CfgID;
        public string NPCID;
        public string OffsetX;
        public string OffsetZ;

        public int CfgIDInt => int.TryParse(CfgID, out int id) ? id : 0;
        public int NPCIDInt => int.TryParse(NPCID, out int id) ? id : 0;
        public float OffsetXFloat => ParseFloat(OffsetX);
        public float OffsetZFloat => ParseFloat(OffsetZ);

        private static float ParseFloat(string value)
        {
            return float.TryParse(value, out float parsed) ? parsed : 0f;
        }
    }

    [Serializable]
    internal class MapLayoutWrapper
    {
        public List<MapPlayerSpawnData> MapPlayerSpawnConf;
        public List<MapNpcLayoutData> MapNpcLayoutConf;
    }

    /// <summary>
    /// 地图布局配置加载器。
    /// 负责读取 MapLayoutConf.pb，并按 CfgID 返回玩家出生偏移与 NPC 布局。
    /// </summary>
    public static class MapLayoutConfigLoader
    {
        private static List<MapPlayerSpawnData> _playerSpawns;
        private static List<MapNpcLayoutData> _npcLayouts;
        private static readonly Dictionary<int, MapPlayerSpawnData> _playerSpawnByCfgId = new Dictionary<int, MapPlayerSpawnData>();
        private static readonly Dictionary<int, List<MapNpcLayoutData>> _npcLayoutsByCfgId = new Dictionary<int, List<MapNpcLayoutData>>();
        private static readonly List<MapNpcLayoutData> EmptyNpcLayouts = new List<MapNpcLayoutData>();

        public static void Reload()
        {
            _playerSpawns = null;
            _npcLayouts = null;
            _playerSpawnByCfgId.Clear();
            _npcLayoutsByCfgId.Clear();
        }

        public static Vector3 GetPlayerSpawnOffset(string cfgId)
        {
            if (!int.TryParse(cfgId, out int parsedCfgId))
                return Vector3.zero;

            EnsureLoaded();

            if (_playerSpawnByCfgId.TryGetValue(parsedCfgId, out var cached))
                return cached != null
                    ? new Vector3(cached.OffsetXFloat, 0f, cached.OffsetZFloat)
                    : Vector3.zero;

            MapPlayerSpawnData result = null;
            foreach (var spawn in _playerSpawns)
            {
                if (spawn != null && spawn.CfgIDInt == parsedCfgId)
                {
                    result = spawn;
                    break;
                }
            }

            _playerSpawnByCfgId[parsedCfgId] = result;
            return result != null
                ? new Vector3(result.OffsetXFloat, 0f, result.OffsetZFloat)
                : Vector3.zero;
        }

        public static IReadOnlyList<MapNpcLayoutData> GetNpcLayoutsByCfgId(string cfgId)
        {
            if (!int.TryParse(cfgId, out int parsedCfgId))
                return EmptyNpcLayouts;

            EnsureLoaded();

            if (_npcLayoutsByCfgId.TryGetValue(parsedCfgId, out var cached))
                return cached;

            var result = new List<MapNpcLayoutData>();
            foreach (var layout in _npcLayouts)
            {
                if (layout != null && layout.CfgIDInt == parsedCfgId)
                    result.Add(layout);
            }

            _npcLayoutsByCfgId[parsedCfgId] = result;
            return result;
        }

        private static void EnsureLoaded()
        {
            if (_playerSpawns != null && _npcLayouts != null)
                return;

            var json = ReadCfg("MapLayoutConf.pb");
            if (string.IsNullOrWhiteSpace(json))
            {
                _playerSpawns = new List<MapPlayerSpawnData>();
                _npcLayouts = new List<MapNpcLayoutData>();
                return;
            }

            try
            {
                var wrapper = JsonUtility.FromJson<MapLayoutWrapper>(json);
                _playerSpawns = wrapper?.MapPlayerSpawnConf ?? new List<MapPlayerSpawnData>();
                _npcLayouts = wrapper?.MapNpcLayoutConf ?? new List<MapNpcLayoutData>();
            }
            catch (Exception e)
            {
                Debug.LogError($"[MapLayoutConfigLoader] 解析 MapLayoutConf 失败: {e.Message}");
                _playerSpawns = new List<MapPlayerSpawnData>();
                _npcLayouts = new List<MapNpcLayoutData>();
            }
        }

        private static string ReadCfg(string fileName)
        {
            string resourceName = Path.GetFileNameWithoutExtension(fileName);
            var asset = Resources.Load<TextAsset>($"Cfg/{resourceName}");
            if (asset != null)
                return asset.text;

            string path = Path.Combine(Application.dataPath, "Cfg", fileName);
            if (File.Exists(path))
                return File.ReadAllText(path);

            Debug.LogWarning($"[MapLayoutConfigLoader] 找不到配置文件: {fileName}");
            return null;
        }
    }
}
