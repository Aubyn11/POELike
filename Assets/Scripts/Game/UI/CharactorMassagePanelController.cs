using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using POELike.ECS.Components;
using POELike.Managers;

namespace POELike.Game.UI
{
    [DisallowMultipleComponent]
    public class CharactorMassagePanelController : MonoBehaviour
    {
        private enum StatCategory
        {
            Damage,
            Defense,
            Other,
        }

        private struct StatDisplayEntry
        {
            public string Description;
            public string Value;

            public StatDisplayEntry(string description, string value)
            {
                Description = description;
                Value = value;
            }
        }

        private static readonly StatType[] AllStatTypes = (StatType[])Enum.GetValues(typeof(StatType));

        private static readonly StatType[] DamageStatTypes =
        {
            StatType.PhysicalDamage,
            StatType.FireDamage,
            StatType.ColdDamage,
            StatType.LightningDamage,
            StatType.ChaosDamage,
            StatType.AttackSpeed,
            StatType.CriticalChance,
            StatType.CriticalMultiplier,
        };

        private static readonly StatType[] DefenseStatTypes =
        {
            StatType.MaxHealth,
            StatType.HealthRegen,
            StatType.LifeLeech,
            StatType.MaxEnergyShield,
            StatType.EnergyShieldRegen,
            StatType.Armor,
            StatType.Evasion,
            StatType.BlockChance,
            StatType.FireResistance,
            StatType.ColdResistance,
            StatType.LightningResistance,
            StatType.ChaosResistance,
        };

        private static readonly StatType[] OtherStatTypes =
        {
            StatType.Strength,
            StatType.Dexterity,
            StatType.Intelligence,
            StatType.MaxMana,
            StatType.ManaRegen,
            StatType.ManaLeech,
            StatType.MovementSpeed,
            StatType.SkillCooldownReduction,
            StatType.AreaOfEffect,
            StatType.ProjectileSpeed,
            StatType.Duration,
        };

        private static readonly string[] DefenseCategoryKeywords =
        {
            "Health",
            "Life",
            "Shield",
            "Armor",
            "Armour",
            "Evasion",
            "Block",
            "Resist",
            "Resistance",
            "Ward",
            "Guard",
            "Barrier",
            "Fortify",
            "Mitigation",
            "Reduction",
            "Recovery",
        };

        private static readonly string[] DamageCategoryKeywords =
        {
            "Damage",
            "Attack",
            "Critical",
            "Crit",
            "Accuracy",
            "Penetration",
            "Projectile",
            "Cast",
            "Strike",
            "Hit",
        };

        private readonly List<StatDisplayEntry> _entries = new List<StatDisplayEntry>(24);

        private Text _nameText;
        private Text _levelText;
        private Text _powerNumText;
        private Text _brainNumText;
        private Text _speedNumText;
        private Button _damageBtn;
        private Button _defenceBtn;
        private Button _otherBtn;
        private ListBox _massageArr;
        private bool _initialized;
        private StatCategory _activeCategory = StatCategory.Damage;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            RefreshFromCurrentState();
        }

        public void RefreshFromCurrentState()
        {
            EnsureInitialized();

            var saveData = SceneLoader.PendingCharacterData;
            var stats = ResolvePlayerStats();

            ApplyHeader(saveData, stats);
            RebuildEntries(stats);
        }

        private void EnsureInitialized()
        {
            if (_initialized)
                return;

            _nameText = FindChildRecursive(transform, "Name")?.GetComponent<Text>();
            _levelText = FindChildRecursive(transform, "Level")?.GetComponent<Text>();
            _powerNumText = FindChildRecursive(transform, "PowerNum")?.GetComponent<Text>();
            _brainNumText = FindChildRecursive(transform, "BrainNum")?.GetComponent<Text>();
            _speedNumText = FindChildRecursive(transform, "SpeedNum")?.GetComponent<Text>();
            _damageBtn = FindChildRecursive(transform, "DamageBtn")?.GetComponent<Button>();
            _defenceBtn = FindChildRecursive(transform, "DefenceBtn")?.GetComponent<Button>();
            _otherBtn = FindChildRecursive(transform, "OtherBtn")?.GetComponent<Button>();
            _massageArr = FindChildRecursive(transform, "MassageArr")?.GetComponent<ListBox>();

            if (_damageBtn != null)
            {
                _damageBtn.onClick.RemoveAllListeners();
                _damageBtn.onClick.AddListener(OnDamageButtonClicked);
            }

            if (_defenceBtn != null)
            {
                _defenceBtn.onClick.RemoveAllListeners();
                _defenceBtn.onClick.AddListener(OnDefenceButtonClicked);
            }

            if (_otherBtn != null)
            {
                _otherBtn.onClick.RemoveAllListeners();
                _otherBtn.onClick.AddListener(OnOtherButtonClicked);
            }

            _initialized = true;
        }

        private void OnDamageButtonClicked()
        {
            _activeCategory = StatCategory.Damage;
            RefreshFromCurrentState();
        }

        private void OnDefenceButtonClicked()
        {
            _activeCategory = StatCategory.Defense;
            RefreshFromCurrentState();
        }

        private void OnOtherButtonClicked()
        {
            _activeCategory = StatCategory.Other;
            RefreshFromCurrentState();
        }

        private void ApplyHeader(CharacterSaveData saveData, StatsComponent stats)
        {
            if (_nameText != null)
                _nameText.text = saveData != null && !string.IsNullOrWhiteSpace(saveData.CharacterName)
                    ? saveData.CharacterName
                    : "未命名角色";

            if (_levelText != null)
            {
                int level = saveData != null ? Mathf.Max(1, saveData.Level) : 1;
                _levelText.text = $"等级：{level}";
            }

            if (_powerNumText != null)
                _powerNumText.text = FormatSimpleValue(GetStatValue(stats, StatType.Strength));

            if (_brainNumText != null)
                _brainNumText.text = FormatSimpleValue(GetStatValue(stats, StatType.Intelligence));

            if (_speedNumText != null)
                _speedNumText.text = FormatSimpleValue(GetStatValue(stats, StatType.Dexterity));
        }

        private void RebuildEntries(StatsComponent stats)
        {
            if (_massageArr == null)
                return;

            _entries.Clear();
            AppendEntriesForCurrentCategory(stats, _entries);

            if (_entries.Count == 0)
                _entries.Add(new StatDisplayEntry("暂无属性", "--"));

            _massageArr.Clear();
            _massageArr.AddItem(0, _entries.Count);

            for (int i = 0; i < _entries.Count; i++)
                ApplyEntryToItem(_massageArr.GetItemByIndex(i), _entries[i]);

            _massageArr.RefreshLayout();
        }

        private void AppendEntriesForCurrentCategory(StatsComponent stats, List<StatDisplayEntry> results)
        {
            var addedTypes = new HashSet<StatType>();
            AppendEntriesForStatTypes(stats, ResolveStatTypes(_activeCategory), results, addedTypes);

            for (int i = 0; i < AllStatTypes.Length; i++)
            {
                var statType = AllStatTypes[i];
                if (addedTypes.Contains(statType))
                    continue;

                if (ClassifyStatType(statType) != _activeCategory)
                    continue;

                addedTypes.Add(statType);
                TryAppendEntry(stats, statType, results);
            }
        }

        private void AppendEntriesForStatTypes(StatsComponent stats, StatType[] statTypes, List<StatDisplayEntry> results, HashSet<StatType> addedTypes)
        {
            for (int i = 0; i < statTypes.Length; i++)
            {
                var statType = statTypes[i];
                if (!addedTypes.Add(statType))
                    continue;

                TryAppendEntry(stats, statType, results);
            }
        }

        private void TryAppendEntry(StatsComponent stats, StatType statType, List<StatDisplayEntry> results)
        {
            float value = GetStatValue(stats, statType);
            if (!ShouldDisplayStat(statType, value))
                return;

            results.Add(new StatDisplayEntry(
                ResolveStatDescription(statType),
                FormatStatValue(statType, value)));
        }

        private static StatType[] ResolveStatTypes(StatCategory category)
        {
            switch (category)
            {
                case StatCategory.Defense:
                    return DefenseStatTypes;
                case StatCategory.Other:
                    return OtherStatTypes;
                default:
                    return DamageStatTypes;
            }
        }

        private static StatCategory ClassifyStatType(StatType statType)
        {
            if (ContainsStatType(DamageStatTypes, statType))
                return StatCategory.Damage;

            if (ContainsStatType(DefenseStatTypes, statType))
                return StatCategory.Defense;

            if (ContainsStatType(OtherStatTypes, statType))
                return StatCategory.Other;

            string statName = statType.ToString();
            if (ContainsAnyKeyword(statName, DefenseCategoryKeywords))
                return StatCategory.Defense;

            if (ContainsAnyKeyword(statName, DamageCategoryKeywords))
                return StatCategory.Damage;

            return StatCategory.Other;
        }

        private static bool ContainsStatType(StatType[] statTypes, StatType statType)
        {
            for (int i = 0; i < statTypes.Length; i++)
            {
                if (statTypes[i] == statType)
                    return true;
            }

            return false;
        }

        private static bool ContainsAnyKeyword(string value, string[] keywords)
        {
            if (string.IsNullOrWhiteSpace(value) || keywords == null)
                return false;

            for (int i = 0; i < keywords.Length; i++)
            {
                if (value.IndexOf(keywords[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static bool ShouldDisplayStat(StatType statType, float value)
        {
            if (statType == StatType.Strength || statType == StatType.Dexterity || statType == StatType.Intelligence)
                return true;

            return Mathf.Abs(value) > 0.01f;
        }

        private static float GetStatValue(StatsComponent stats, StatType statType)
        {
            return stats != null ? stats.GetStat(statType) : 0f;
        }

        private static string ResolveStatDescription(StatType statType)
        {
            switch (statType)
            {
                case StatType.Strength:
                    return "力量";
                case StatType.Dexterity:
                    return "敏捷";
                case StatType.Intelligence:
                    return "智力";
                case StatType.MaxHealth:
                    return "最大生命值";
                case StatType.HealthRegen:
                    return "生命回复";
                case StatType.LifeLeech:
                    return "生命偷取";
                case StatType.MaxMana:
                    return "最大魔力值";
                case StatType.ManaRegen:
                    return "魔力回复";
                case StatType.ManaLeech:
                    return "魔力偷取";
                case StatType.MaxEnergyShield:
                    return "最大能量护盾";
                case StatType.EnergyShieldRegen:
                    return "能量护盾回复";
                case StatType.PhysicalDamage:
                    return "物理伤害";
                case StatType.FireDamage:
                    return "火焰伤害";
                case StatType.ColdDamage:
                    return "冰冷伤害";
                case StatType.LightningDamage:
                    return "闪电伤害";
                case StatType.ChaosDamage:
                    return "混沌伤害";
                case StatType.AttackSpeed:
                    return "攻击速度";
                case StatType.CriticalChance:
                    return "暴击率";
                case StatType.CriticalMultiplier:
                    return "暴击倍率";
                case StatType.Armor:
                    return "护甲";
                case StatType.Evasion:
                    return "闪避";
                case StatType.BlockChance:
                    return "格挡率";
                case StatType.FireResistance:
                    return "火焰抗性";
                case StatType.ColdResistance:
                    return "冰冷抗性";
                case StatType.LightningResistance:
                    return "闪电抗性";
                case StatType.ChaosResistance:
                    return "混沌抗性";
                case StatType.MovementSpeed:
                    return "移动速度";
                case StatType.SkillCooldownReduction:
                    return "技能冷却缩减";
                case StatType.AreaOfEffect:
                    return "范围效果";
                case StatType.ProjectileSpeed:
                    return "投射物速度";
                case StatType.Duration:
                    return "持续时间";
                default:
                    return statType.ToString();
            }
        }

        private static string FormatStatValue(StatType statType, float value)
        {
            string numeric = FormatSimpleValue(value);
            if (IsPercentStat(statType))
                return $"{numeric}%";

            return numeric;
        }

        private static bool IsPercentStat(StatType statType)
        {
            switch (statType)
            {
                case StatType.LifeLeech:
                case StatType.ManaLeech:
                case StatType.CriticalChance:
                case StatType.CriticalMultiplier:
                case StatType.BlockChance:
                case StatType.FireResistance:
                case StatType.ColdResistance:
                case StatType.LightningResistance:
                case StatType.ChaosResistance:
                case StatType.SkillCooldownReduction:
                    return true;
                default:
                    return false;
            }
        }

        private static string FormatSimpleValue(float value)
        {
            if (Mathf.Abs(value - Mathf.Round(value)) <= 0.01f)
                return Mathf.RoundToInt(value).ToString();

            return value.ToString("0.##");
        }

        private void ApplyEntryToItem(ListBoxItem item, StatDisplayEntry entry)
        {
            if (item == null)
                return;

            var itemTransform = item.transform;
            var text = itemTransform.Find("Text")?.GetComponent<TextMeshProUGUI>();
            var value = itemTransform.Find("Value")?.GetComponent<TextMeshProUGUI>();

            if (text != null)
                text.text = entry.Description;

            if (value != null)
                value.text = entry.Value;
        }

        private static StatsComponent ResolvePlayerStats()
        {
            var world = GameManager.Instance != null ? GameManager.Instance.World : null;
            if (world == null)
                return null;

            var playerEntity = world.FindEntityByTag("Player");
            if (playerEntity == null || !playerEntity.IsAlive)
                return null;

            return playerEntity.GetComponent<StatsComponent>();
        }

        private static Transform FindChildRecursive(Transform root, string nodeName)
        {
            if (root == null || string.IsNullOrWhiteSpace(nodeName))
                return null;

            if (root.name == nodeName)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindChildRecursive(root.GetChild(i), nodeName);
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}