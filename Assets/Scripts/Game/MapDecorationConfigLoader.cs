using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace POELike.Game
{
    [Serializable]
    public class MapDecorationData
    {
        public string CfgID;
        public string DecorationType;
        public string OffsetX;
        public string OffsetZ;
        public string ScaleX;
        public string ScaleY;
        public string ScaleZ;
        public string RotationY;

        public int CfgIDInt => int.TryParse(CfgID, out int id) ? id : 0;
        public float OffsetXFloat => ParseFloat(OffsetX);
        public float OffsetZFloat => ParseFloat(OffsetZ);
        public float ScaleXFloat => ParseFloat(ScaleX);
        public float ScaleYFloat => ParseFloat(ScaleY);
        public float ScaleZFloat => ParseFloat(ScaleZ);
        public float RotationYFloat => ParseFloat(RotationY);

        private static float ParseFloat(string value)
        {
            return float.TryParse(value, out float parsed) ? parsed : 0f;
        }
    }

    [Serializable]
    internal class MapDecorationWrapper
    {
        public List<MapDecorationData> MapDecorationConf;
    }

    /// <summary>
    /// 地图装饰配置加载器。
    /// 负责读取 MapDecorationConf.pb，并按 CfgID 返回地图装饰布局。
    /// </summary>
    public static class MapDecorationConfigLoader
    {
        private static List<MapDecorationData> _decorations;
        private static readonly Dictionary<int, List<MapDecorationData>> _decorationsByCfgId = new Dictionary<int, List<MapDecorationData>>();
        private static readonly List<MapDecorationData> EmptyDecorations = new List<MapDecorationData>();

        public static IReadOnlyList<MapDecorationData> Decorations => _decorations ?? (_decorations = LoadDecorations());

        public static void Reload()
        {
            _decorations = null;
            _decorationsByCfgId.Clear();
        }

        public static IReadOnlyList<MapDecorationData> GetByCfgId(string cfgId)
        {
            if (!int.TryParse(cfgId, out int parsedCfgId))
                return EmptyDecorations;

            if (_decorations == null)
                _decorations = LoadDecorations();

            if (_decorationsByCfgId.TryGetValue(parsedCfgId, out var cached))
                return cached;

            var result = new List<MapDecorationData>();
            foreach (var decoration in _decorations)
            {
                if (decoration != null && decoration.CfgIDInt == parsedCfgId)
                    result.Add(decoration);
            }

            _decorationsByCfgId[parsedCfgId] = result;
            return result;
        }

        private static List<MapDecorationData> LoadDecorations()
        {
            var json = ReadCfg("MapDecorationConf.pb");
            if (string.IsNullOrWhiteSpace(json))
                return new List<MapDecorationData>();

            try
            {
                var wrapper = JsonUtility.FromJson<MapDecorationWrapper>(json);
                return wrapper?.MapDecorationConf ?? new List<MapDecorationData>();
            }
            catch (Exception e)
            {
                Debug.LogError($"[MapDecorationConfigLoader] 解析 MapDecorationConf 失败: {e.Message}");
                return new List<MapDecorationData>();
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

            Debug.LogWarning($"[MapDecorationConfigLoader] 找不到配置文件: {fileName}");
            return null;
        }
    }
}
