using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace POELike.Game.Skills
{
    [Serializable]
    public class ActiveSkillStoneConfigData
    {
        public string ActiveSkillStoneId;
        public string ActiveSkillStoneCode;
        public string ActiveSkillStoneName;
        public string ActiveSkillStoneDesc;
        public string SkillEffectName;
        public string SkillColor;
        public string SkillCastType;
        public string IsChannelingSkill;
        public string CanMoveWhileCasting;
        public List<int> SkillTags = new List<int>();
        public string SkillShapeType;

    }

    [Serializable]
    public class SupportSkillStoneConfigData
    {
        public string SupportSkillStoneId;
        public string SupportSkillStoneCode;
        public string SupportSkillStoneName;
        public string SupportSkillStoneDesc;
    }

    [Serializable]
    internal class ActiveSkillStoneConfigWrapper
    {
        public List<ActiveSkillStoneConfigData> ActiveSkillStoneConf;
    }

    [Serializable]
    internal class SupportSkillStoneConfigWrapper
    {
        public List<SupportSkillStoneConfigData> SupportSkillStoneConf;
    }

    /// <summary>
    /// 技能配置加载器。
    /// 当前负责读取主动技能与支持宝石的基础配置。
    /// </summary>
    public static class SkillConfigLoader
    {
        private static List<ActiveSkillStoneConfigData> _activeSkills;
        private static Dictionary<string, ActiveSkillStoneConfigData> _activeSkillsByCode;
        private static List<SupportSkillStoneConfigData> _supportSkills;
        private static Dictionary<string, SupportSkillStoneConfigData> _supportSkillsByCode;

        public static IReadOnlyList<ActiveSkillStoneConfigData> ActiveSkills => _activeSkills ??= LoadActiveSkills();
        public static IReadOnlyList<SupportSkillStoneConfigData> SupportSkills => _supportSkills ??= LoadSupportSkills();

        public static ActiveSkillStoneConfigData GetActiveSkillByCode(string skillCode)
        {
            if (string.IsNullOrWhiteSpace(skillCode))
                return null;

            EnsureActiveLookup();
            _activeSkillsByCode.TryGetValue(skillCode.Trim(), out var result);
            return result;
        }

        public static SupportSkillStoneConfigData GetSupportSkillByCode(string skillCode)
        {
            if (string.IsNullOrWhiteSpace(skillCode))
                return null;

            EnsureSupportLookup();
            _supportSkillsByCode.TryGetValue(skillCode.Trim(), out var result);
            return result;
        }

        public static void ClearCache()
        {
            _activeSkills = null;
            _activeSkillsByCode = null;
            _supportSkills = null;
            _supportSkillsByCode = null;
        }

        private static void EnsureActiveLookup()
        {
            if (_activeSkillsByCode != null)
                return;

            _activeSkillsByCode = new Dictionary<string, ActiveSkillStoneConfigData>(StringComparer.OrdinalIgnoreCase);
            var configs = _activeSkills ??= LoadActiveSkills();
            for (int i = 0; i < configs.Count; i++)
            {
                var config = configs[i];
                if (config == null || string.IsNullOrWhiteSpace(config.ActiveSkillStoneCode))
                    continue;

                _activeSkillsByCode[config.ActiveSkillStoneCode.Trim()] = config;
            }
        }

        private static void EnsureSupportLookup()
        {
            if (_supportSkillsByCode != null)
                return;

            _supportSkillsByCode = new Dictionary<string, SupportSkillStoneConfigData>(StringComparer.OrdinalIgnoreCase);
            var configs = _supportSkills ??= LoadSupportSkills();
            for (int i = 0; i < configs.Count; i++)
            {
                var config = configs[i];
                if (config == null || string.IsNullOrWhiteSpace(config.SupportSkillStoneCode))
                    continue;

                _supportSkillsByCode[config.SupportSkillStoneCode.Trim()] = config;
            }
        }

        private static List<ActiveSkillStoneConfigData> LoadActiveSkills()
        {
            var json = ReadCfg("ActiveSkillStoneConf.pb");
            if (string.IsNullOrWhiteSpace(json))
                return new List<ActiveSkillStoneConfigData>();

            try
            {
                var wrapper = JsonUtility.FromJson<ActiveSkillStoneConfigWrapper>(json);
                return wrapper?.ActiveSkillStoneConf ?? new List<ActiveSkillStoneConfigData>();
            }
            catch (Exception e)
            {
                Debug.LogError($"[SkillConfigLoader] 解析ActiveSkillStoneConf失败: {e.Message}");
                return new List<ActiveSkillStoneConfigData>();
            }
        }

        private static List<SupportSkillStoneConfigData> LoadSupportSkills()
        {
            var json = ReadCfg("SupportSkillStoneConf.pb");
            if (string.IsNullOrWhiteSpace(json))
                return new List<SupportSkillStoneConfigData>();

            try
            {
                var wrapper = JsonUtility.FromJson<SupportSkillStoneConfigWrapper>(json);
                return wrapper?.SupportSkillStoneConf ?? new List<SupportSkillStoneConfigData>();
            }
            catch (Exception e)
            {
                Debug.LogError($"[SkillConfigLoader] 解析SupportSkillStoneConf失败: {e.Message}");
                return new List<SupportSkillStoneConfigData>();
            }
        }

        private static string ReadCfg(string fileName)
        {

            var resourceName = Path.GetFileNameWithoutExtension(fileName);
            var asset = Resources.Load<TextAsset>($"Cfg/{resourceName}");
            if (asset != null)
                return asset.text;

            string path = Path.Combine(Application.dataPath, "Cfg", fileName);
            if (File.Exists(path))
                return File.ReadAllText(path);

            Debug.LogError($"[SkillConfigLoader] 找不到配置文件: {fileName}");
            return null;
        }
    }
}