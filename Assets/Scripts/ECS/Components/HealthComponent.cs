using System;
using POELike.ECS.Core;

namespace POELike.ECS.Components
{
    /// <summary>
    /// 生命值组件
    /// 管理实体的生命值、魔力值、能量护盾
    /// </summary>
    public class HealthComponent : IComponent
    {
        public bool IsEnabled { get; set; } = true;
        
        #region 生命值
        
        private float _currentHealth;
        private float _maxHealth;
        
        public float MaxHealth
        {
            get => _maxHealth;
            set => _maxHealth = Math.Max(0, value);
        }
        
        public float CurrentHealth
        {
            get => _currentHealth;
            set
            {
                float clamped = Math.Clamp(value, 0, MaxHealth);
                if (Math.Abs(_currentHealth - clamped) > 0.001f)
                {
                    float old = _currentHealth;
                    _currentHealth = clamped;
                    OnHealthChanged?.Invoke(old, _currentHealth);
                    
                    if (_currentHealth <= 0)
                        OnDeath?.Invoke();
                }
            }
        }
        
        public float HealthPercent => MaxHealth > 0 ? CurrentHealth / MaxHealth : 0f;
        public bool IsAlive => CurrentHealth > 0;
        
        #endregion
        
        #region 魔力值
        
        private float _currentMana;
        private float _maxMana;
        
        public float MaxMana
        {
            get => _maxMana;
            set => _maxMana = Math.Max(0, value);
        }
        
        public float CurrentMana
        {
            get => _currentMana;
            set
            {
                float clamped = Math.Clamp(value, 0, MaxMana);
                if (Math.Abs(_currentMana - clamped) > 0.001f)
                {
                    float old = _currentMana;
                    _currentMana = clamped;
                    OnManaChanged?.Invoke(old, _currentMana);
                }
            }
        }
        
        public float ManaPercent => MaxMana > 0 ? CurrentMana / MaxMana : 0f;
        
        #endregion
        
        #region 能量护盾
        
        private float _currentEnergyShield;
        private float _maxEnergyShield;
        
        public float MaxEnergyShield
        {
            get => _maxEnergyShield;
            set => _maxEnergyShield = Math.Max(0, value);
        }
        
        public float CurrentEnergyShield
        {
            get => _currentEnergyShield;
            set
            {
                float clamped = Math.Clamp(value, 0, MaxEnergyShield);
                _currentEnergyShield = clamped;
            }
        }
        
        public float EnergyShieldPercent => MaxEnergyShield > 0 ? CurrentEnergyShield / MaxEnergyShield : 0f;
        
        #endregion
        
        // 事件
        public event Action<float, float> OnHealthChanged;   // (oldHP, newHP)
        public event Action<float, float> OnManaChanged;     // (oldMP, newMP)
        public event Action OnDeath;
        
        /// <summary>
        /// 受到伤害（先扣护盾，再扣血）
        /// </summary>
        public void TakeDamage(float damage)
        {
            if (damage <= 0) return;
            
            // 先消耗能量护盾
            if (_currentEnergyShield > 0)
            {
                float shieldAbsorb = Math.Min(_currentEnergyShield, damage);
                CurrentEnergyShield -= shieldAbsorb;
                damage -= shieldAbsorb;
            }
            
            // 剩余伤害扣血
            if (damage > 0)
                CurrentHealth -= damage;
        }
        
        /// <summary>
        /// 恢复生命值
        /// </summary>
        public void Heal(float amount)
        {
            if (amount <= 0) return;
            CurrentHealth += amount;
        }
        
        /// <summary>
        /// 消耗魔力
        /// </summary>
        public bool ConsumeMana(float amount)
        {
            if (_currentMana < amount) return false;
            CurrentMana -= amount;
            return true;
        }
        
        /// <summary>
        /// 初始化为满状态
        /// </summary>
        public void FillToMax()
        {
            _currentHealth = _maxHealth;
            _currentMana = _maxMana;
            _currentEnergyShield = _maxEnergyShield;
        }
        
        public void Reset()
        {
            _currentHealth = _maxHealth;
            _currentMana = _maxMana;
            _currentEnergyShield = _maxEnergyShield;
        }
    }
}
