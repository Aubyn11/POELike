## skill-02 ECS 与运行时主链

> 目标：**搞清楚运行时是谁在创建世界、谁在驱动系统、谁在处理输入与场景逻辑。**
> 
> 如果问题属于“进入场景后为什么会这样运行”，优先读本模块。

### 主链概览

```mermaid
flowchart TD
    A[应用启动] --> B[SceneLoader / 初始场景]
    B --> C[CharacterSelectPanel]
    C --> D[进入 GameScene]
    D --> E[GameManager 创建 ECS World]
    E --> F[注册 Systems]
    F --> G[GameSceneInitializer 初始化测试内容 / 场景运行时资源]
    G --> H[GameSceneManager 驱动玩家输入 / NPC / 寻路 / 快捷键 / 技能输入 / 怪物掉落桥接]
    H --> I[UIManager 管理游戏内 UI]
```

### 核心文件与职责

#### [GameManager.cs](../Assets/Scripts/Managers/GameManager.cs)

- **ECS 世界总入口之一**
- 负责创建 `World`
- 负责注册 / 初始化系统
- 负责保证关键运行时管理器存在

#### [World.cs](../Assets/Scripts/ECS/Core/World.cs)

- **ECS 运行时容器**
- 管理实体、组件索引、查询与系统执行
- 若遇到“某组件明明挂了但系统没扫到”，优先回到这里确认查询链路

#### [GameSceneInitializer.cs](../Assets/Scripts/Game/GameSceneInitializer.cs)

- **进入场景后的初始化器**
- 当前负责：
  - 预热技能特效池
  - 自动分配测试装备
  - 自动分配测试技能
  - 打印玩家属性调试信息
- 若问题是“为什么开局就自带这些内容”，先看这里

#### [GameSceneManager.cs](../Assets/Scripts/Game/GameSceneManager.cs)

- **运行时总控入口之一**
- 当前主要负责：
  - 玩家 / 怪物 / NPC / UI 主链串联
  - 鼠标地面寻路
  - NPC 名称点击 / 对话入口
  - 技能输入采集与写入 `PlayerInputComponent`
  - 药剂快捷键处理
  - 左键 `Skill1 / Move / Blocked` 判定分流
  - 监听 `EntityDiedEvent` 并桥接怪物地面掉落到 `GroundItemDroppedEvent`
- 如果现象与“玩家输入后发生了什么”有关，优先查这里

### 系统层阅读顺序

#### 1. [StatsSystem.cs](../Assets/Scripts/ECS/Systems/StatsSystem.cs)

- 属性聚合 / 重算入口
- 改装备词条、角色面板数值、伤害基础值时优先看这里

#### 2. [MovementSystem.cs](../Assets/Scripts/ECS/Systems/MovementSystem.cs)

- 负责实体移动
- 与 `GameSceneManager` 的输入写入、`MovementComponent` 的状态共同决定玩家移动
- 施法锁移动也会影响这里

#### 3. [CombatSystem.cs](../Assets/Scripts/ECS/Systems/CombatSystem.cs)

- 负责伤害、死亡、战斗事件
- 若技能“执行了但没伤害”或怪物死亡清理异常，常需要同时看这里

#### 4. [SkillSystem.cs](../Assets/Scripts/ECS/Systems/SkillSystem.cs)

- 负责技能触发、冷却、施法前摇、引导、技能运行时实体创建
- 输入链到了这里，才算真正进入技能执行域

### 关键组件阅读顺序

#### [PlayerInputComponent.cs](../Assets/Scripts/ECS/Components/PlayerInputComponent.cs)

- 玩家输入缓冲区
- 当前包含：
  - `SkillInputs`
  - `SkillHeldInputs`
  - `SkillReleasedInputs`
- 这些输入由 `GameSceneManager` / `PlayerController` 写入，再由系统消费

#### [SkillComponent.cs](../Assets/Scripts/ECS/Components/SkillComponent.cs)

- 技能槽与施法状态拥有者
- 当前关键字段包括：
  - `SkillSlots`
  - `ActiveSkill`
  - `IsCasting`
  - `CastTimer`
  - `IsChanneling`
  - `ChannelTickTimer`
  - `ActiveChannelRuntime`
- 注意：`InitializeSlots(int count = 6)` 默认参数仍是 `6`，**但当前正式玩家初始化由 `GameSceneManager` 与 `PlayerController` 显式调用 `InitializeSlots(8)`**

#### [MovementComponent.cs](../Assets/Scripts/ECS/Components/MovementComponent.cs)

- 移动目标、方向、施法锁移动状态
- `IsMovementLockedByCasting` 是施法期间停止寻路 / 停止积压输入的重要标记

### 当前运行时能力约定

#### 玩家技能槽容量

- 当前正式运行时按 **8 槽** 工作
- 显式入口：
  - [GameSceneManager.cs](../Assets/Scripts/Game/GameSceneManager.cs)
  - [PlayerController.cs](../Assets/Scripts/Game/Character/PlayerController.cs)
- 不要只看 `SkillComponent.InitializeSlots()` 的默认参数就误判系统仍然是 6 槽

#### 当前默认测试技能

看 [GameSceneInitializer.cs](../Assets/Scripts/Game/GameSceneInitializer.cs)：

- 槽位 0：重击
- 槽位 1：火球术 + 多重投射 + 附加火焰伤害
- 槽位 2：冰霜新星
- 槽位 3：闪现
- 槽位 4：旋风斩

如果有人反馈“开局为什么就有这些技能”，根因通常不在 UI，而在初始化流程

#### 左键判定分流

看 [GameSceneManager.cs](../Assets/Scripts/Game/GameSceneManager.cs)：

- 当前一次左键按下会锁定为：
  - `Skill1`
  - `Move`
  - `Blocked`
- 整次按住过程中不会中途切换
- 若要改 POE 风格左键行为，优先查：
  - `ResolveLeftMouseIntent()`
  - `TryBeginLeftClickSkill()`
  - `FindMonsterUnderCursor()`
  - `ResolveLeftClickSkillRange()`

### 常见排查入口

#### 问题：进入场景后状态就不对

优先看：

- [GameSceneInitializer.cs](../Assets/Scripts/Game/GameSceneInitializer.cs)
- [GameManager.cs](../Assets/Scripts/Managers/GameManager.cs)
- [World.cs](../Assets/Scripts/ECS/Core/World.cs)

#### 问题：按键输入了，但没有后续动作

优先看：

- [GameSceneManager.cs](../Assets/Scripts/Game/GameSceneManager.cs)
- [PlayerInputComponent.cs](../Assets/Scripts/ECS/Components/PlayerInputComponent.cs)
- [SkillSystem.cs](../Assets/Scripts/ECS/Systems/SkillSystem.cs)
- [MovementSystem.cs](../Assets/Scripts/ECS/Systems/MovementSystem.cs)

#### 问题：施法时移动 / 寻路行为异常

优先看：

- [SkillSystem.cs](../Assets/Scripts/ECS/Systems/SkillSystem.cs)
- [MovementComponent.cs](../Assets/Scripts/ECS/Components/MovementComponent.cs)
- [MovementSystem.cs](../Assets/Scripts/ECS/Systems/MovementSystem.cs)
- [GameSceneManager.cs](../Assets/Scripts/Game/GameSceneManager.cs)

### 从本模块跳到哪里

- **要看技能释放、技能栏、快捷键、冷却显示**：转 [skill-05](./Skill_05_技能系统_技能栏与快捷键.md)
- **要看背包 / 装备 / 宝石链路**：转 [skill-03](./Skill_03_背包_装备_宝石交互.md)
- **要看 UI / Tips / 面板展示**：转 [skill-04](./Skill_04_UI_Tips与角色面板.md)
- **要看 SOP / 常见坑**：转 [skill-07](./Skill_07_SOP_排错与高风险点.md)
