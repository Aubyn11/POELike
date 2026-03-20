using System;
using System.Collections.Generic;
using POELike.ECS.Core;

namespace POELike.ECS.Components
{
    /// <summary>
    /// 属性组件
    /// 管理角色的所有属性值和修改器（类似POE的属性系统）
    /// </summary>
    public class StatsComponent : IComponent
    {
        public bool IsEnabled { get; set; } = true;
        
        // 基础属性值
        private readonly Dictionary<StatType, float> _baseStats = new Dictionary<StatType, float>();
        // 属性修改器列表
        private readonly List<StatModifier> _modifiers = new List<StatModifier>();
        // 计算后的最终属性缓存
        private readonly Dictionary<StatType, float> _cachedStats = new Dictionary<StatType, float>();
        // 缓存是否有效
        private bool _isDirty = true;
        
        // 属性变化事件
        public event Action<StatType, float, float> OnStatChanged; // (statType, oldValue, newValue)
        
        /// <summary>
        /// 设置基础属性值
        /// </summary>
        public void SetBaseStat(StatType type, float value)
        {
            _baseStats[type] = value;
            _isDirty = true;
        }
        
        /// <summary>
        /// 获取基础属性值
        /// </summary>
        public float GetBaseStat(StatType type)
        {
            return _baseStats.TryGetValue(type, out var val) ? val : 0f;
        }
        
        /// <summary>
        /// 添加属性修改器
        /// </summary>
        public void AddModifier(StatModifier modifier)
        {
            _modifiers.Add(modifier);
            _isDirty = true;
        }
        
        /// <summary>
        /// 移除来自指定来源的所有修改器
        /// </summary>
        public void RemoveModifiersFromSource(string source)
        {
            int removed = _modifiers.RemoveAll(m => m.Source == source);
            if (removed > 0) _isDirty = true;
        }
        
        /// <summary>
        /// 移除指定类型的所有修改器
        /// </summary>
        public void RemoveModifiers(StatType type)
        {
            int removed = _modifiers.RemoveAll(m => m.StatType == type);
            if (removed > 0) _isDirty = true;
        }
        
        /// <summary>
        /// 获取最终属性值（含所有修改器）
        /// 计算公式：(Base + FlatBonus) * (1 + PercentAdd) * PercentMore1 * PercentMore2...
        /// </summary>
        public float GetStat(StatType type)
        {
            if (_isDirty) RecalculateAll();
            return _cachedStats.TryGetValue(type, out var val) ? val : GetBaseStat(type);
        }
        
        /// <summary>
        /// 重新计算所有属性
        /// </summary>
        private void RecalculateAll()
        {
            _cachedStats.Clear();
            
            // 收集所有涉及的属性类型
            var allTypes = new HashSet<StatType>(_baseStats.Keys);
            foreach (var mod in _modifiers)
                allTypes.Add(mod.StatType);
            
            foreach (var type in allTypes)
            {
                float oldValue = _cachedStats.TryGetValue(type, out var old) ? old : 0f;
                float newValue = CalculateStat(type);
                _cachedStats[type] = newValue;
                
                if (Math.Abs(oldValue - newValue) > 0.001f)
                    OnStatChanged?.Invoke(type, oldValue, newValue);
            }
            
            _isDirty = false;
        }
        
        /// <summary>
        /// 计算单个属性的最终值
        /// </summary>
        private float CalculateStat(StatType type)
        {
            float baseValue = GetBaseStat(type);
            float flatBonus = 0f;
            float percentAdd = 0f;
            float percentMore = 1f;
            
            foreach (var mod in _modifiers)
            {
                if (mod.StatType != type) continue;
                
                switch (mod.ModifierType)
                {
                    case ModifierType.Flat:
                        flatBonus += mod.Value;
                        break;
                    case ModifierType.PercentAdd:
                        percentAdd += mod.Value;
                        break;
                    case ModifierType.PercentMore:
                        percentMore *= (1f + mod.Value / 100f);
                        break;
                }
            }
            
            return (baseValue + flatBonus) * (1f + percentAdd / 100f) * percentMore;
        }
        
        /// <summary>
        /// 强制标记属性为脏（需要重新计算）
        /// </summary>
        public void MarkDirty() => _isDirty = true;
        
        public void Reset()
        {
            _baseStats.Clear();
            _modifiers.Clear();
            _cachedStats.Clear();
            _isDirty = true;
        }
    }
}
