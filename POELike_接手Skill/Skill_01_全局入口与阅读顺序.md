## skill-01 全局入口与阅读顺序

> 目标：**先建立项目地图，再进入具体模块。**
> 
> 如果你只知道“现象”，还不知道状态归谁、规则归谁、入口归谁，先读本模块。

### 适用场景

- **刚接手项目**：需要先知道主干链路和关键文件分布
- **现象模糊**：只知道“哪里不对”，还不知道先看 ECS、UI 还是配置
- **需要缩小搜索范围**：希望先锁定最小文件集合，再继续深挖

### 先记住这几个事实

- **项目主干不是某个 UI 面板，而是 `GameManager -> World -> Systems -> GameSceneManager -> UIManager`**
- **背包系统核心不是 `BagPanel`，而是 `BagItemView + BagBox + EquipmentSlotView + SocketItem`**
- **技能“能不能释放”和技能“底栏显示什么”是两条链**
- **装备 Tips 的位置 / 层级 与 内容 是分离的**
- **商店更常持有 `GeneratedEquipment`，背包 / 装备 UI 更常使用 `BagItemData`**
- **文档若与当前代码冲突，以当前代码为准**

### 推荐阅读顺序

#### 读全局

1. [GameManager.cs](../Assets/Scripts/Managers/GameManager.cs)
2. [UIManager.cs](../Assets/Scripts/Managers/UIManager.cs)
3. [GameSceneInitializer.cs](../Assets/Scripts/Game/GameSceneInitializer.cs)
4. [GameSceneManager.cs](../Assets/Scripts/Game/GameSceneManager.cs)
5. [World.cs](../Assets/Scripts/ECS/Core/World.cs)

#### 读玩法主干

1. [StatsSystem.cs](../Assets/Scripts/ECS/Systems/StatsSystem.cs)
2. [MovementSystem.cs](../Assets/Scripts/ECS/Systems/MovementSystem.cs)
3. [CombatSystem.cs](../Assets/Scripts/ECS/Systems/CombatSystem.cs)
4. [SkillSystem.cs](../Assets/Scripts/ECS/Systems/SkillSystem.cs)
5. [SkillComponent.cs](../Assets/Scripts/ECS/Components/SkillComponent.cs)
6. [SkillFactory.cs](../Assets/Scripts/Game/Skills/SkillFactory.cs)

#### 读背包 / 装备链路

1. [BagPanel.cs](../Assets/Scripts/Game/UI/BagPanel.cs)
2. [BagBox.cs](../Assets/Scripts/Game/UI/BagBox.cs)
3. [BagItemView.cs](../Assets/Scripts/Game/UI/BagItemView.cs)
4. [EquipmentSlotView.cs](../Assets/Scripts/Game/UI/EquipmentSlotView.cs)
5. [SocketItem.cs](../Assets/Scripts/Game/UI/SocketItem.cs)
6. [EquipmentItem.cs](../Assets/Scripts/Game/UI/EquipmentItem.cs)
7. [EquipmentTips.cs](../Assets/Scripts/Game/UI/EquipmentTips.cs)
8. [BagItemData.cs](../Assets/Scripts/Game/UI/BagItemData.cs)
9. [CharactorMainPanelController.cs](../Assets/Scripts/Game/UI/CharactorMainPanelController.cs)
10. [CharactorMassagePanelController.cs](../Assets/Scripts/Game/UI/CharactorMassagePanelController.cs)

#### 读装备生成 / 商店 / NPC / 配置

1. [EquipmentConfigLoader.cs](../Assets/Scripts/Game/Equipment/EquipmentConfigLoader.cs)
2. [EquipmentGenerator.cs](../Assets/Scripts/Game/Equipment/EquipmentGenerator.cs)
3. [ShopPanel.cs](../Assets/Scripts/Game/UI/ShopPanel.cs)
4. [NpcButtonEventType.cs](../Assets/Scripts/Game/NpcButtonEventType.cs)
5. [NpcConfigLoader.cs](../Assets/Scripts/Game/NpcConfigLoader.cs)
6. [NpcDialogPanel.cs](../Assets/Scripts/Game/UI/NpcDialogPanel.cs)

### 高频修改任务 -> 应先看哪些文件

#### 1. 想改“进入游戏 / 初始化 / 场景搭建”

先看：

- [SceneLoader.cs](../Assets/Scripts/Managers/SceneLoader.cs)
- [CharacterSelectPanel.cs](../Assets/Scripts/Game/UI/CharacterSelectPanel.cs)
- [GameManager.cs](../Assets/Scripts/Managers/GameManager.cs)
- [GameSceneInitializer.cs](../Assets/Scripts/Game/GameSceneInitializer.cs)
- [GameSceneManager.cs](../Assets/Scripts/Game/GameSceneManager.cs)

#### 2. 想改角色属性、战斗数值、装备加成

先看：

- [EquipmentComponent.cs](../Assets/Scripts/ECS/Components/EquipmentComponent.cs)
- [StatsSystem.cs](../Assets/Scripts/ECS/Systems/StatsSystem.cs)
- [CombatSystem.cs](../Assets/Scripts/ECS/Systems/CombatSystem.cs)
- [SkillSystem.cs](../Assets/Scripts/ECS/Systems/SkillSystem.cs)

#### 3. 想改背包拿起 / 放下 / 替换 / 镶嵌

先看：

- [BagItemView.cs](../Assets/Scripts/Game/UI/BagItemView.cs)
- [BagBox.cs](../Assets/Scripts/Game/UI/BagBox.cs)
- [BagCell.cs](../Assets/Scripts/Game/UI/BagCell.cs)
- [EquipmentSlotView.cs](../Assets/Scripts/Game/UI/EquipmentSlotView.cs)
- [SocketItem.cs](../Assets/Scripts/Game/UI/SocketItem.cs)

#### 4. 想改装备 Tips 的位置 / 翻边 / 跟随 / 层级

先看：

- [EquipmentItem.cs](../Assets/Scripts/Game/UI/EquipmentItem.cs)
- [EquipmentTips.cs](../Assets/Scripts/Game/UI/EquipmentTips.cs)
- [UIManager.cs](../Assets/Scripts/Managers/UIManager.cs)

#### 5. 想改装备 Tips 的内容

先看：

- [EquipmentTips.cs](../Assets/Scripts/Game/UI/EquipmentTips.cs)
- [BagItemData.cs](../Assets/Scripts/Game/UI/BagItemData.cs)
- [EquipmentGenerator.cs](../Assets/Scripts/Game/Equipment/EquipmentGenerator.cs)
- [ShopPanel.cs](../Assets/Scripts/Game/UI/ShopPanel.cs)

#### 6. 想改技能释放、支持宝石、快捷键或技能栏显示

先看：

- [GameSceneManager.cs](../Assets/Scripts/Game/GameSceneManager.cs)
- [SkillSystem.cs](../Assets/Scripts/ECS/Systems/SkillSystem.cs)
- [SkillComponent.cs](../Assets/Scripts/ECS/Components/SkillComponent.cs)
- [SkillFactory.cs](../Assets/Scripts/Game/Skills/SkillFactory.cs)
- [GameSceneInitializer.cs](../Assets/Scripts/Game/GameSceneInitializer.cs)
- [CharactorMainPanelController.cs](../Assets/Scripts/Game/UI/CharactorMainPanelController.cs)

#### 7. 想改 NPC 对话或商店入口

先看：

- [NpcButtonEventType.cs](../Assets/Scripts/Game/NpcButtonEventType.cs)
- [NpcConfigLoader.cs](../Assets/Scripts/Game/NpcConfigLoader.cs)
- [NpcDialogPanel.cs](../Assets/Scripts/Game/UI/NpcDialogPanel.cs)
- [GameSceneManager.cs](../Assets/Scripts/Game/GameSceneManager.cs)

### 状态拥有者思路

不要看到 UI 问题就只改 UI 表象。先判断状态真正归谁管：

- **位置 / 是否显示**：通常归视图控制器管
- **数据内容**：通常归数据模型、生成器或运行时组件管
- **放置是否合法**：通常归容器 / 规则对象管
- **能否触发技能**：通常归输入写入者 + `SkillSystem` 管

在本项目里，常见对应关系是：

- **`BagItemView`**：背包移动状态拥有者
- **`BagBox`**：背包格子放置规则拥有者
- **`EquipmentItem`**：装备 UI 行为拥有者
- **`EquipmentTips`**：提示内容拥有者
- **`UIManager`**：游戏内 UI 层级与面板生命周期拥有者
- **`SkillSystem`**：技能释放、冷却、施法状态拥有者
- **`CharactorMainPanelController`**：角色底栏展示拥有者

### 从本模块跳到哪里

- **要看 ECS / 场景运行时主链**：转 [skill-02](./Skill_02_ECS与运行时主链.md)
- **要看背包 / 装备 / 宝石交互**：转 [skill-03](./Skill_03_背包_装备_宝石交互.md)
- **要看 UI / Tips / 角色面板**：转 [skill-04](./Skill_04_UI_Tips与角色面板.md)
- **要看技能系统 / 技能栏 / 快捷键**：转 [skill-05](./Skill_05_技能系统_技能栏与快捷键.md)
- **要看装备生成 / 商店 / NPC / 配置工具链**：转 [skill-06](./Skill_06_装备生成_商店_NPC与配置工具链.md)
- **要看 SOP / 排错 / 高风险点**：转 [skill-07](./Skill_07_SOP_排错与高风险点.md)

### 最后一句话

接手这个项目时，**不要从“某个面板长什么样”开始理解，而要从“谁拥有状态、谁负责规则、谁只是展示”开始理解。**
