using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace POELike.Game
{
    [Serializable]
    public class MapContentData
    {
        public string CfgID;
        public string GroupName;
        public string MonsterID;
        public string MonsterCount;
        public string Count;
        public string OffsetX;
        public string OffsetZ;

        public int CfgIDInt => int.TryParse(CfgID, out int id) ? id : 0;
        public int MonsterIDInt => int.TryParse(MonsterID, out int id) ? id : 0;
        public int MonsterCountInt => ParseNonNegativeInt(MonsterCount, Count);
        public int CountInt => MonsterCountInt;
        public float OffsetXFloat => ParseFloat(OffsetX);
        public float OffsetZFloat => ParseFloat(OffsetZ);

        private static int ParseNonNegativeInt(params string[] values)
        {
            if (values == null)
                return 0;

            foreach (var value in values)
            {
                if (int.TryParse(value, out int parsed))
                    return Mathf.Max(0, parsed);
            }

            return 0;
        }

        private static float ParseFloat(string value)
        {
            return float.TryParse(value, out float parsed) ? parsed : 0f;
        }
    }

    [Serializable]
    internal class MapContentWrapper
    {
        public List<MapContentData> MapContentConf;
    }

    /// <summary>
    /// 地图内容配置加载器。
    /// 负责读取 MapContentConf.pb，并按 CfgID 返回地图怪物组配置。
    /// </summary>
    public static class MapContentConfigLoader
    {
        private static List<MapContentData> _contents;
        private static readonly Dictionary<int, List<MapContentData>> _contentsByCfgId = new Dictionary<int, List<MapContentData>>();
        private static readonly List<MapContentData> EmptyContents = new List<MapContentData>();

        public static IReadOnlyList<MapContentData> Contents => _contents ??= LoadContents();

        public static void Reload()
        {
            _contents = null;
            _contentsByCfgId.Clear();
        }

        public static IReadOnlyList<MapContentData> GetByCfgId(string cfgId)
        {
            if (!int.TryParse(cfgId, out int parsedCfgId))
                return EmptyContents;

            _contents ??= LoadContents();

            if (_contentsByCfgId.TryGetValue(parsedCfgId, out var cached))
                return cached;

            var result = new List<MapContentData>();
            foreach (var content in _contents)
            {
                if (content != null && content.CfgIDInt == parsedCfgId)
                    result.Add(content);
            }

            _contentsByCfgId[parsedCfgId] = result;
            return result;
        }

        private static List<MapContentData> LoadContents()
        {
            var json = ReadCfg("MapContentConf.pb");
            if (string.IsNullOrWhiteSpace(json))
                return new List<MapContentData>();

            try
            {
                var wrapper = JsonUtility.FromJson<MapContentWrapper>(json);
                return wrapper?.MapContentConf ?? new List<MapContentData>();
            }
            catch (Exception e)
            {
                Debug.LogError($"[MapContentConfigLoader] 解析 MapContentConf 失败: {e.Message}");
                return new List<MapContentData>();
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

            Debug.LogWarning($"[MapContentConfigLoader] 找不到配置文件: {fileName}");
            return null;
        }
    }
}
