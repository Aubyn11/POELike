using POELike.ECS.Core;
using POELike.ECS.Components;

namespace POELike.ECS.Systems
{
    /// <summary>
    /// 属性系统
    /// 负责在装备变化时重新计算角色属性
    /// 优先级：10（最先执行，确保其他系统使用最新属性）
    /// </summary>
    public class StatsSystem : SystemBase
    {
        public override int Priority => 10;
        
        protected override void OnInitialize()
        {
            World.EventBus.Subscribe<EquipmentChangedEvent>(OnEquipmentChanged);
        }
        
        protected override void OnUpdate(float deltaTime)
        {
            // 处理生命/魔力回复
            UpdateRegeneration(deltaTime);
        }
        
        /// <summary>
        /// 处理生命/魔力/护盾回复
        /// </summary>
        private void UpdateRegeneration(float deltaTime)
        {
            var entities = World.Query<HealthComponent, StatsComponent>();
            foreach (var entity in entities)
            {
                var health = entity.GetComponent<HealthComponent>();
                var stats = entity.GetComponent<StatsComponent>();
                
                if (!health.IsAlive) continue;
                
                // 生命回复
                float hpRegen = stats.GetStat(StatType.HealthRegen);
                if (hpRegen > 0 && health.CurrentHealth < health.MaxHealth)
                    health.Heal(hpRegen * deltaTime);
                
                // 魔力回复
                float mpRegen = stats.GetStat(StatType.ManaRegen);
                if (mpRegen > 0 && health.CurrentMana < health.MaxMana)
                    health.CurrentMana += mpRegen * deltaTime;
                
                // 能量护盾回复（脱战后）
                float esRegen = stats.GetStat(StatType.EnergyShieldRegen);
                if (esRegen > 0 && health.CurrentEnergyShield < health.MaxEnergyShield)
                    health.CurrentEnergyShield += esRegen * deltaTime;
            }
        }
        
        /// <summary>
        /// 装备变化时重新计算属性
        /// </summary>
        private void OnEquipmentChanged(EquipmentChangedEvent evt)
        {
            var entity = evt.Entity;
            var stats = entity.GetComponent<StatsComponent>();
            var equipment = entity.GetComponent<EquipmentComponent>();
            var health = entity.GetComponent<HealthComponent>();
            
            if (stats == null || equipment == null) return;
            
            // 移除所有装备词缀
            stats.RemoveModifiersFromSource("equipment");
            
            // 重新添加所有装备词缀
            foreach (var mod in equipment.GetAllEquipmentModifiers())
            {
                var newMod = new StatModifier(mod.StatType, mod.ModifierType, mod.Value, "equipment");
                stats.AddModifier(newMod);
            }
            
            // 更新生命值上限
            if (health != null)
            {
                float newMaxHp = stats.GetStat(StatType.MaxHealth);
                float newMaxMp = stats.GetStat(StatType.MaxMana);
                float newMaxEs = stats.GetStat(StatType.MaxEnergyShield);
                
                if (newMaxHp > 0) health.MaxHealth = newMaxHp;
                if (newMaxMp > 0) health.MaxMana = newMaxMp;
                if (newMaxEs > 0) health.MaxEnergyShield = newMaxEs;
            }
        }
        
        protected override void OnDispose()
        {
            World.EventBus.Unsubscribe<EquipmentChangedEvent>(OnEquipmentChanged);
        }
    }
    
    public struct EquipmentChangedEvent
    {
        public Entity Entity;
        public EquipmentSlot Slot;
        public ItemData OldItem;
        public ItemData NewItem;
    }
}
