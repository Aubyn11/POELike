using UnityEngine;
using POELike.ECS.Components;
using POELike.ECS.Systems;
using POELike.Game.Character;
using POELike.Game.Items;
using POELike.Game.Skills;
using POELike.Managers;

namespace POELike.Game
{
    /// <summary>
    /// 游戏场景初始化器
    /// 用于测试和演示ECS框架
    /// 挂载到场景中的空GameObject上
    /// </summary>
    public class GameSceneInitializer : MonoBehaviour
    {
        [Header("测试设置")]
        [SerializeField] private bool _autoEquipTestItems = true;
        [SerializeField] private bool _autoAssignTestSkills = true;
        
        private void Start()
        {
            // 等待GameManager和PlayerController初始化完成
            Invoke(nameof(InitializeGameScene), 0.1f);
        }
        
        private void InitializeGameScene()
        {
            var world = GameManager.Instance?.World;
            if (world == null)
            {
                Debug.LogError("[GameSceneInitializer] GameManager未找到！");
                return;
            }
            
            // 订阅事件
            world.EventBus.Subscribe<EnemyDiedEvent>(OnEnemyDied);
            world.EventBus.Subscribe<DamageResultEvent>(OnDamageResult);
            world.EventBus.Subscribe<PlayerDiedEvent>(OnPlayerDied);
            
            // 获取玩家实体
            var playerEntity = world.FindEntityByTag("Player");
            if (playerEntity == null)
            {
                Debug.LogWarning("[GameSceneInitializer] 玩家实体未找到，请确保场景中有PlayerController");
                return;
            }
            
            // 自动装备测试物品
            if (_autoEquipTestItems)
                EquipTestItems(playerEntity);
            
            // 自动分配测试技能
            if (_autoAssignTestSkills)
                AssignTestSkills(playerEntity);
            
            Debug.Log("[GameSceneInitializer] 游戏场景初始化完成！");
            PrintPlayerStats(playerEntity);
        }
        
        /// <summary>
        /// 为玩家装备测试物品
        /// </summary>
        private void EquipTestItems(ECS.Core.Entity playerEntity)
        {
            var equipment = playerEntity.GetComponent<EquipmentComponent>();
            var stats = playerEntity.GetComponent<StatsComponent>();
            if (equipment == null) return;
            
            // 创建一把测试武器
            var sword = ItemFactory.CreateWeapon("铁剑", 5, 10f, 15f, 1.3f);
            sword.Rarity = ItemRarity.Magic;
            sword.Prefixes.Add(new StatModifier(StatType.PhysicalDamage, ModifierType.PercentAdd, 30f));
            sword.Suffixes.Add(new StatModifier(StatType.AttackSpeed, ModifierType.PercentAdd, 10f));
            
            // 创建测试护甲
            var armour = ItemFactory.CreateArmour("铁甲", 5, 80f, EquipmentSlot.BodyArmour);
            armour.Rarity = ItemRarity.Magic;
            armour.Prefixes.Add(new StatModifier(StatType.MaxHealth, ModifierType.Flat, 50f));
            
            // 装备
            equipment.Equip(EquipmentSlot.MainHand, sword);
            equipment.Equip(EquipmentSlot.BodyArmour, armour);
            
            // 触发装备变化事件，让StatsSystem重新计算属性
            GameManager.Instance.World.EventBus.Publish(new EquipmentChangedEvent
            {
                Entity = playerEntity,
                Slot = EquipmentSlot.MainHand,
                NewItem = sword
            });
            
            Debug.Log($"[GameSceneInitializer] 已装备测试物品");
        }
        
        /// <summary>
        /// 为玩家分配测试技能
        /// </summary>
        private void AssignTestSkills(ECS.Core.Entity playerEntity)
        {
            var skillComp = playerEntity.GetComponent<SkillComponent>();
            if (skillComp == null) return;
            
            // 槽位0：普通攻击
            var slot0 = skillComp.GetSlot(0);
            if (slot0 != null) slot0.SkillData = SkillFactory.CreateNormalAttack();
            
            // 槽位1：火球术（带多重投射支持宝石）
            var slot1 = skillComp.GetSlot(1);
            if (slot1 != null)
            {
                slot1.SkillData = SkillFactory.CreateFireball()
                    .WithSupportGem(SkillFactory.CreateMultiProjectileGem(2))
                    .WithSupportGem(SkillFactory.CreateAddedFireDamageGem(15f));
            }
            
            // 槽位2：冰霜新星
            var slot2 = skillComp.GetSlot(2);
            if (slot2 != null) slot2.SkillData = SkillFactory.CreateFrostNova();
            
            // 槽位3：闪现
            var slot3 = skillComp.GetSlot(3);
            if (slot3 != null) slot3.SkillData = SkillFactory.CreateBlink();
            
            // 槽位4：旋风斩
            var slot4 = skillComp.GetSlot(4);
            if (slot4 != null) slot4.SkillData = SkillFactory.CreateCyclone();
            
            Debug.Log("[GameSceneInitializer] 已分配测试技能");
        }
        
        /// <summary>
        /// 打印玩家属性（调试用）
        /// </summary>
        private void PrintPlayerStats(ECS.Core.Entity playerEntity)
        {
            var stats = playerEntity.GetComponent<StatsComponent>();
            var health = playerEntity.GetComponent<HealthComponent>();
            if (stats == null) return;
            
            Debug.Log("=== 玩家属性 ===");
            Debug.Log($"生命值: {health?.CurrentHealth:F0}/{health?.MaxHealth:F0}");
            Debug.Log($"魔力值: {health?.CurrentMana:F0}/{health?.MaxMana:F0}");
            Debug.Log($"物理伤害: {stats.GetStat(StatType.PhysicalDamage):F1}");
            Debug.Log($"攻击速度: {stats.GetStat(StatType.AttackSpeed):F2}");
            Debug.Log($"护甲: {stats.GetStat(StatType.Armor):F0}");
            Debug.Log($"移动速度: {stats.GetStat(StatType.MovementSpeed):F1}");
            Debug.Log($"火焰抗性: {stats.GetStat(StatType.FireResistance):F0}%");
            Debug.Log($"暴击率: {stats.GetStat(StatType.CriticalChance):F1}%");
            Debug.Log("================");
        }
        
        private void OnEnemyDied(EnemyDiedEvent evt)
        {
            Debug.Log($"[游戏] 击杀敌人！获得 {evt.ExperienceReward} 经验");
            
            // 随机掉落物品
            if (Random.value < 0.5f)
            {
                var droppedItem = ItemFactory.CreateRandomItem(5);
                Debug.Log($"[游戏] 掉落物品: [{droppedItem.Rarity}] {droppedItem.Name}");
            }
        }
        
        private void OnDamageResult(DamageResultEvent evt)
        {
            string critText = evt.IsCritical ? " (暴击!)" : "";
            Debug.Log($"[战斗] {evt.Source?.Tag} -> {evt.Target?.Tag}: {evt.FinalDamage:F1} {evt.DamageType}伤害{critText}");
        }
        
        private void OnPlayerDied(PlayerDiedEvent evt)
        {
            Debug.Log("[游戏] 玩家死亡！游戏结束");
        }
        
        private void OnDestroy()
        {
            if (GameManager.Instance?.World != null)
            {
                GameManager.Instance.World.EventBus.Unsubscribe<EnemyDiedEvent>(OnEnemyDied);
                GameManager.Instance.World.EventBus.Unsubscribe<DamageResultEvent>(OnDamageResult);
                GameManager.Instance.World.EventBus.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);
            }
        }
    }
}
