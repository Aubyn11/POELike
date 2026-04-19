using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace POELike.Game.Currency
{
    [Serializable]
    public class CurrencyCategoryData
    {
        public string CurrencyCategoryId;
        public string CurrencyCategoryCode;
        public string CurrencyCategoryName;
        public string CurrencyCategoryDesc;
        public string CurrencyCategorySortOrder;
    }

    [Serializable]
    public class CurrencyEffectTypeData
    {
        public string CurrencyEffectTypeId;
        public string CurrencyEffectTypeCode;
        public string CurrencyEffectTypeName;
        public string CurrencyEffectTypeDesc;
        public string CurrencyConsumesOnUse;
        public string CurrencyCanApplyNormal;
        public string CurrencyCanApplyMagic;
        public string CurrencyCanApplyRare;
        public string CurrencyCanApplyUnique;
        public string CurrencyCanApplyCorrupted;
        public List<string> CurrencyAllowedItemTypes = new List<string>();
    }

    [Serializable]
    public class CurrencyBaseData
    {
        public string CurrencyBaseId;
        public string CurrencyCode;
        public string CurrencyName;
        public string CurrencyCategoryId;
        public string CurrencyEffectTypeId;
        public string CurrencyStackSize;
        public string CurrencyDropLevel;
        public string CurrencySortOrder;
        public string CurrencyGridWidth;
        public string CurrencyGridHeight;
        public string CurrencyDescription;
        public string CurrencyTargetDescription;
        public string CurrencyEffectDescription;
        public string CurrencyFlavorText;
        public string CurrencyDisplayColor;
        public string CurrencyIconPath;
    }

    [Serializable]
    class CurrencyCategoryWrapper { public List<CurrencyCategoryData> CurrencyCategoryConf; }
    [Serializable]
    class CurrencyEffectTypeWrapper { public List<CurrencyEffectTypeData> CurrencyEffectTypeConf; }
    [Serializable]
    class CurrencyBaseWrapper { public List<CurrencyBaseData> CurrencyBaseConf; }

    public static class CurrencyConfigLoader
    {
        private static List<CurrencyCategoryData> _categories;
        private static List<CurrencyEffectTypeData> _effectTypes;
        private static List<CurrencyBaseData> _baseCurrencies;
        private static readonly Dictionary<string, CurrencyBaseData> _currencyById = new Dictionary<string, CurrencyBaseData>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, CurrencyBaseData> _currencyByCode = new Dictionary<string, CurrencyBaseData>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, CurrencyCategoryData> _categoryById = new Dictionary<string, CurrencyCategoryData>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, CurrencyEffectTypeData> _effectTypeById = new Dictionary<string, CurrencyEffectTypeData>(StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyList<CurrencyCategoryData> Categories => _categories ??= LoadCategories();
        public static IReadOnlyList<CurrencyEffectTypeData> EffectTypes => _effectTypes ??= LoadEffectTypes();
        public static IReadOnlyList<CurrencyBaseData> BaseCurrencies => _baseCurrencies ??= LoadBaseCurrencies();

        public static CurrencyBaseData FindCurrencyById(string currencyBaseId)
        {
            if (string.IsNullOrWhiteSpace(currencyBaseId))
                return null;

            EnsureLookupCaches();
            _currencyById.TryGetValue(currencyBaseId.Trim(), out var result);
            return result;
        }

        public static CurrencyBaseData FindCurrencyByCode(string currencyCode)
        {
            if (string.IsNullOrWhiteSpace(currencyCode))
                return null;

            EnsureLookupCaches();
            _currencyByCode.TryGetValue(currencyCode.Trim(), out var result);
            return result;
        }

        public static CurrencyCategoryData FindCategoryById(string categoryId)
        {
            if (string.IsNullOrWhiteSpace(categoryId))
                return null;

            EnsureLookupCaches();
            _categoryById.TryGetValue(categoryId.Trim(), out var result);
            return result;
        }

        public static CurrencyEffectTypeData FindEffectTypeById(string effectTypeId)
        {
            if (string.IsNullOrWhiteSpace(effectTypeId))
                return null;

            EnsureLookupCaches();
            _effectTypeById.TryGetValue(effectTypeId.Trim(), out var result);
            return result;
        }

        public static void ClearCache()
        {
            _categories = null;
            _effectTypes = null;
            _baseCurrencies = null;
            _currencyById.Clear();
            _currencyByCode.Clear();
            _categoryById.Clear();
            _effectTypeById.Clear();
        }

        private static void EnsureLookupCaches()
        {
            _ = Categories.Count;
            _ = EffectTypes.Count;
            _ = BaseCurrencies.Count;

            if (_currencyById.Count == 0)
            {

                for (int i = 0; i < _baseCurrencies.Count; i++)
                {
                    var currency = _baseCurrencies[i];
                    if (currency == null)
                        continue;

                    if (!string.IsNullOrWhiteSpace(currency.CurrencyBaseId) && !_currencyById.ContainsKey(currency.CurrencyBaseId))
                        _currencyById.Add(currency.CurrencyBaseId, currency);

                    if (!string.IsNullOrWhiteSpace(currency.CurrencyCode) && !_currencyByCode.ContainsKey(currency.CurrencyCode))
                        _currencyByCode.Add(currency.CurrencyCode, currency);
                }
            }

            if (_categoryById.Count == 0)
            {
                for (int i = 0; i < _categories.Count; i++)
                {
                    var category = _categories[i];
                    if (category != null && !string.IsNullOrWhiteSpace(category.CurrencyCategoryId) && !_categoryById.ContainsKey(category.CurrencyCategoryId))
                        _categoryById.Add(category.CurrencyCategoryId, category);
                }
            }

            if (_effectTypeById.Count == 0)
            {
                for (int i = 0; i < _effectTypes.Count; i++)
                {
                    var effectType = _effectTypes[i];
                    if (effectType != null && !string.IsNullOrWhiteSpace(effectType.CurrencyEffectTypeId) && !_effectTypeById.ContainsKey(effectType.CurrencyEffectTypeId))
                        _effectTypeById.Add(effectType.CurrencyEffectTypeId, effectType);
                }
            }
        }

        private static string ReadCfg(string fileName)
        {
            var ta = Resources.Load<TextAsset>($"Cfg/{Path.GetFileNameWithoutExtension(fileName)}");
            if (ta != null) return ta.text;

            string path = Path.Combine(Application.dataPath, "Cfg", fileName);
            if (File.Exists(path)) return File.ReadAllText(path);

            Debug.LogError($"[CurrencyConfigLoader] 找不到配置文件: {fileName}");
            return null;
        }

        private static List<CurrencyCategoryData> LoadCategories()
        {
            var json = ReadCfg("CurrencyCategoryConf.pb");
            if (json == null) return new List<CurrencyCategoryData>();
            try
            {
                var w = JsonUtility.FromJson<CurrencyCategoryWrapper>(json);
                return w?.CurrencyCategoryConf ?? new List<CurrencyCategoryData>();
            }
            catch (Exception e)
            {
                Debug.LogError($"[CurrencyConfigLoader] 解析CurrencyCategoryConf失败: {e.Message}");
                return new List<CurrencyCategoryData>();
            }
        }

        private static List<CurrencyEffectTypeData> LoadEffectTypes()
        {
            var json = ReadCfg("CurrencyEffectTypeConf.pb");
            if (json == null) return new List<CurrencyEffectTypeData>();
            try
            {
                var w = JsonUtility.FromJson<CurrencyEffectTypeWrapper>(json);
                return w?.CurrencyEffectTypeConf ?? new List<CurrencyEffectTypeData>();
            }
            catch (Exception e)
            {
                Debug.LogError($"[CurrencyConfigLoader] 解析CurrencyEffectTypeConf失败: {e.Message}");
                return new List<CurrencyEffectTypeData>();
            }
        }

        private static List<CurrencyBaseData> LoadBaseCurrencies()
        {
            var json = ReadCfg("CurrencyBaseConf.pb");
            if (json == null) return new List<CurrencyBaseData>();
            try
            {
                var w = JsonUtility.FromJson<CurrencyBaseWrapper>(json);
                return w?.CurrencyBaseConf ?? new List<CurrencyBaseData>();
            }
            catch (Exception e)
            {
                Debug.LogError($"[CurrencyConfigLoader] 解析CurrencyBaseConf失败: {e.Message}");
                return new List<CurrencyBaseData>();
            }
        }
    }
}