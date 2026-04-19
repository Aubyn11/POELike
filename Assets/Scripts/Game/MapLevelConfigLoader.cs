using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace POELike.Game
{
    [Serializable]
    public class MapLevelData
    {
        public string MapID;
        public string SceneID;
        public string CfgID;
        public string MapName;
    }

    [Serializable]
    internal class MapLevelWrapper
    {
        public List<MapLevelData> MapLevelConf;
    }

    /// <summary>
    /// 地图关卡配置加载器。
    /// 负责读取 MapLevelConf.pb 并提供 DoorPanel 使用的地图列表。
    /// </summary>
    public static class MapLevelConfigLoader
    {
        private static List<MapLevelData> _levels;

        public static IReadOnlyList<MapLevelData> Levels => _levels ??= LoadLevels();

        public static void Reload()
        {
            _levels = null;
        }

        private static List<MapLevelData> LoadLevels()
        {
            var json = ReadCfg("MapLevelConf.pb");
            if (string.IsNullOrWhiteSpace(json))
                return new List<MapLevelData>();

            try
            {
                var wrapper = JsonUtility.FromJson<MapLevelWrapper>(json);
                return wrapper?.MapLevelConf ?? new List<MapLevelData>();
            }
            catch (Exception e)
            {
                Debug.LogError($"[MapLevelConfigLoader] 解析 MapLevelConf 失败: {e.Message}");
                return new List<MapLevelData>();
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

            Debug.LogWarning($"[MapLevelConfigLoader] 找不到配置文件: {fileName}");
            return null;
        }
    }
}
