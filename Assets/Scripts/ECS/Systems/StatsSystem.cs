using System.Collections.Generic;
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

        private readonly List<Entity> _regenEntities = new List<Entity>(8);
        private readonly List<Entity> _queryBuffer   = new List<Entity>(32);
        private bool _regenCacheDirty = true;
        
        protected override void OnInitialize()
        {
            World.EventBus.Subscribe<EquipmentChangedEvent>(OnEquipmentChanged);
            World.EventBus.Subscribe<EntityCreatedEvent>(OnEntityCreated);
            World.EventBus.Subscribe<EntityDestroyedEvent>(OnEntityDestroyed);
        }
        
        protected override void OnUpdate(float deltaTime)
        {
            SyncRegenCache();
            UpdateRegeneration(deltaTime);
        }

        private void OnEntityCreated(EntityCreatedEvent evt)
        {
            _regenCacheDirty = true;
        }

        private void OnEntityDestroyed(EntityDestroyedEvent evt)
        {
            _regenCacheDirty = true;
        }

        private void SyncRegenCache()
        {
            if (!_regenCacheDirty) return;

            _regenCacheDirty = false;
            _regenEntities.Clear();
            World.Query<HealthComponent, StatsComponent>(_queryBuffer);

            foreach (var entity in _queryBuffer)
            {
                var stats = entity.GetComponent<StatsComponent>();
                if (stats == null) continue;
                if (!HasAnyRegeneration(stats)) continue;
                _regenEntities.Add(entity);
            }
        }

        private static bool HasAnyRegeneration(StatsComponent stats)
        {
            return stats.GetStat(StatType.HealthRegen) > 0.001f
                || stats.GetStat(StatType.ManaRegen) > 0.001f
                || stats.GetStat(StatType.EnergyShieldRegen) > 0.001f;
        }
        
        /// <summary>
        /// 处理生命/魔力/护盾回复
        /// </summary>
        private void UpdateRegeneration(float deltaTime)
        {
            foreach (var entity in _regenEntities)
            {
                var health = entity.GetComponent<HealthComponent>();
                var stats  = entity.GetComponent<StatsComponent>();
                if (health == null || stats == null) continue;
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

            _regenCacheDirty = true;
        }
        
        protected override void OnDispose()
        {
            World.EventBus.Unsubscribe<EquipmentChangedEvent>(OnEquipmentChanged);
            World.EventBus.Unsubscribe<EntityCreatedEvent>(OnEntityCreated);
            World.EventBus.Unsubscribe<EntityDestroyedEvent>(OnEntityDestroyed);
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
