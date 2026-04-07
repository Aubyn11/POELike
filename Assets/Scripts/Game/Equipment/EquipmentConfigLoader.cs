using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace POELike.Game.Equipment
{
    // ── 数据模型 ──────────────────────────────────────────────────────

    [Serializable]
    public class EquipmentDetailTypeData
    {
        public string EquipmentDetailTypeId;
        public string EquipmentDetailTypeName;
        public string EquipmentDemandLevel;
        public string EquipmentHeight;
        public string EquipmentWidth;
        public string EquipmentBaseValueId;
        public string EquipmentPart;
        public List<int> EquipmentTypes = new List<int>();
        public string EquipmentDemandWisdom;
        public string EquipmentDemandStrength;
        public string EquipmentDemandIntelligence;
    }

    [Serializable]
    public class EquipmentModData
    {
        public string EquipmentModId;
        public string EquipmentModName;
        public string EquipmentModType;   // 1=前缀 2=后缀
        public string EquipmentModTier;
        public string EquipmentModRequireLevel;
        public List<int> EquipmentModSubCategories = new List<int>();
        public string EquipmentModWeight;
    }

    [Serializable]
    public class EquipmentModValueData
    {
        public string EquipmentModValueId;
        public string EquipmentModId;
        public string EquipmentModValueIndex;
        public string EquipmentModMinValue;
        public string EquipmentModMaxValue;
        public string EquipmentModValueDesc;
    }

    [Serializable]
    public class EquipmentSubCategoryData
    {
        public string EquipmentSubCategoryId;
        public string EquipmentSubCategoryName;
        public string EquipmentCategoryId;
    }

    [Serializable]
    public class EquipmentBaseValueData
    {
        public string EquipmentBaseValueId;
        public string EquipmentBaseMinValue;
        public string EquipmentBaseMaxValue;
        public string EquipmentBaseValueDesc;
    }

    [Serializable]
    public class FlaskBaseData
    {
        public string FlaskBaseId;
        public string FlaskCode;
        public string FlaskName;
        public string FlaskType;
        public string FlaskRequireLevel;
        public string FlaskWidth;
        public string FlaskHeight;
        public string FlaskRecoverLife;
        public string FlaskRecoverMana;
        public string FlaskDurationMs;
        public string FlaskMaxCharges;
        public string FlaskChargesPerUse;
        public string FlaskIsInstant;
        public string FlaskInstantPercent;
        public string FlaskUtilityEffectType;
        public string FlaskUtilityEffectValue;
        public List<int> FlaskAllowedSlots = new List<int>();
        public string FlaskEffectDesc;
    }

    // ── Wrapper ───────────────────────────────────────────────────────

    [Serializable]
    class DetailTypeWrapper   { public List<EquipmentDetailTypeData>  EquipmentDetailTypeConf; }
    [Serializable]
    class ModWrapper          { public List<EquipmentModData>          EquipmentModConf; }
    [Serializable]
    class ModValueWrapper     { public List<EquipmentModValueData>     EquipmentModValueConf; }
    [Serializable]
    class SubCategoryWrapper  { public List<EquipmentSubCategoryData>  EquipmentSubCategoryConf; }
    [Serializable]
    class BaseValueWrapper    { public List<EquipmentBaseValueData>    EquipmentBaseValueConf; }
    [Serializable]
    class FlaskBaseWrapper    { public List<FlaskBaseData>             FlaskBaseConf; }

    /// <summary>
    /// 装备配置加载器（静态单例，首次访问时自动加载）
    /// </summary>
    public static class EquipmentConfigLoader
    {
        // ── 缓存 ──────────────────────────────────────────────────────

        private static List<EquipmentDetailTypeData>  _detailTypes;
        private static List<EquipmentModData>          _mods;
        private static List<EquipmentModValueData>     _modValues;
        private static List<EquipmentSubCategoryData>  _subCategories;
        private static List<EquipmentBaseValueData>    _baseValues;
        private static List<FlaskBaseData>             _flaskBases;

        public static IReadOnlyList<EquipmentDetailTypeData>  DetailTypes    => _detailTypes  ??= LoadDetailTypes();
        public static IReadOnlyList<EquipmentModData>          Mods           => _mods         ??= LoadMods();
        public static IReadOnlyList<EquipmentModValueData>     ModValues      => _modValues    ??= LoadModValues();
        public static IReadOnlyList<EquipmentSubCategoryData>  SubCategories  => _subCategories ??= LoadSubCategories();
        public static IReadOnlyList<EquipmentBaseValueData>    BaseValues     => _baseValues   ??= LoadBaseValues();
        public static IReadOnlyList<FlaskBaseData>             FlaskBases     => _flaskBases   ??= LoadFlaskBases();

        // ── 加载方法 ──────────────────────────────────────────────────

        private static string ReadCfg(string fileName)
        {
            // 优先从 Resources/Cfg 加载
            var ta = Resources.Load<TextAsset>($"Cfg/{Path.GetFileNameWithoutExtension(fileName)}");
            if (ta != null) return ta.text;

            // 回退到 Assets/Cfg/
            string path = Path.Combine(Application.dataPath, "Cfg", fileName);
            if (File.Exists(path)) return File.ReadAllText(path);

            Debug.LogError($"[EquipmentConfigLoader] 找不到配置文件: {fileName}");
            return null;
        }

        private static List<EquipmentDetailTypeData> LoadDetailTypes()
        {
            var json = ReadCfg("EquipmentDetailTypeConf.pb");
            if (json == null) return new List<EquipmentDetailTypeData>();
            try
            {
                var w = JsonUtility.FromJson<DetailTypeWrapper>(json);
                return w?.EquipmentDetailTypeConf ?? new List<EquipmentDetailTypeData>();
            }
            catch (Exception e)
            {
                Debug.LogError($"[EquipmentConfigLoader] 解析EquipmentDetailTypeConf失败: {e.Message}");
                return new List<EquipmentDetailTypeData>();
            }
        }

        private static List<EquipmentModData> LoadMods()
        {
            var json = ReadCfg("EquipmentModConf.pb");
            if (json == null) return new List<EquipmentModData>();
            try
            {
                var w = JsonUtility.FromJson<ModWrapper>(json);
                return w?.EquipmentModConf ?? new List<EquipmentModData>();
            }
            catch (Exception e)
            {
                Debug.LogError($"[EquipmentConfigLoader] 解析EquipmentModConf失败: {e.Message}");
                return new List<EquipmentModData>();
            }
        }

        private static List<EquipmentModValueData> LoadModValues()
        {
            var json = ReadCfg("EquipmentModValueConf.pb");
            if (json == null) return new List<EquipmentModValueData>();
            try
            {
                var w = JsonUtility.FromJson<ModValueWrapper>(json);
                return w?.EquipmentModValueConf ?? new List<EquipmentModValueData>();
            }
            catch (Exception e)
            {
                Debug.LogError($"[EquipmentConfigLoader] 解析EquipmentModValueConf失败: {e.Message}");
                return new List<EquipmentModValueData>();
            }
        }

        private static List<EquipmentSubCategoryData> LoadSubCategories()
        {
            var json = ReadCfg("EquipmentSubCategoryConf.pb");
            if (json == null) return new List<EquipmentSubCategoryData>();
            try
            {
                var w = JsonUtility.FromJson<SubCategoryWrapper>(json);
                return w?.EquipmentSubCategoryConf ?? new List<EquipmentSubCategoryData>();
            }
            catch (Exception e)
            {
                Debug.LogError($"[EquipmentConfigLoader] 解析EquipmentSubCategoryConf失败: {e.Message}");
                return new List<EquipmentSubCategoryData>();
            }
        }

        private static List<EquipmentBaseValueData> LoadBaseValues()
        {
            var json = ReadCfg("EquipmentBaseValueConf.pb");
            if (json == null) return new List<EquipmentBaseValueData>();
            try
            {
                var w = JsonUtility.FromJson<BaseValueWrapper>(json);
                return w?.EquipmentBaseValueConf ?? new List<EquipmentBaseValueData>();
            }
            catch (Exception e)
            {
                Debug.LogError($"[EquipmentConfigLoader] 解析EquipmentBaseValueConf失败: {e.Message}");
                return new List<EquipmentBaseValueData>();
            }
        }

        private static List<FlaskBaseData> LoadFlaskBases()
        {
            var json = ReadCfg("FlaskBaseConf.pb");
            if (json == null) return new List<FlaskBaseData>();
            try
            {
                var w = JsonUtility.FromJson<FlaskBaseWrapper>(json);
                return w?.FlaskBaseConf ?? new List<FlaskBaseData>();
            }
            catch (Exception e)
            {
                Debug.LogError($"[EquipmentConfigLoader] 解析FlaskBaseConf失败: {e.Message}");
                return new List<FlaskBaseData>();
            }
        }

        /// <summary>清除缓存，下次访问时重新加载</summary>
        public static void ClearCache()
        {
            _detailTypes   = null;
            _mods          = null;
            _modValues     = null;
            _subCategories = null;
            _baseValues    = null;
            _flaskBases    = null;
        }
    }
}