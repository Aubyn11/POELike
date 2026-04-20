## skill-05 技能系统 / 技能栏与快捷键

> 目标：**把“能不能放技能”“底栏显示什么”“输入键位是什么”“冷却怎么显示”拆成一份独立模块。**
> 
> 如果后续问题属于技能链，优先读本模块。

### 技能入口主链

当前较新的技能链路是：

1. [GameSceneManager.cs](../Assets/Scripts/Game/GameSceneManager.cs) 读取输入
2. 将输入写入 [PlayerInputComponent.cs](../Assets/Scripts/ECS/Components/PlayerInputComponent.cs)
3. 通过事件总线发布 `SkillActivateEvent`
4. [SkillSystem.cs](../Assets/Scripts/ECS/Systems/SkillSystem.cs) 处理：
   - 检查槽位是否有技能
   - 检查冷却
   - 检查是否正在施法 / 引导
   - 检查魔力
   - 处理施法前摇 / 立即释放 / 引导
   - 触发技能运行时实体、投射物、范围伤害、移动技能等

### 核心文件

#### [GameSceneManager.cs](../Assets/Scripts/Game/GameSceneManager.cs)

- 当前主要技能输入写入者之一
- 负责：
  - 采集技能键位按下 / 按住 / 松开
  - 左键 `Skill1 / Move / Blocked` 判定分流
  - 部分技能触发前的条件判断

#### [PlayerInputComponent.cs](../Assets/Scripts/ECS/Components/PlayerInputComponent.cs)

- 当前维护三态输入：
  - `SkillInputs`
  - `SkillHeldInputs`
  - `SkillReleasedInputs`
- 技能链不要只看 `SkillInputs`，引导技能需要同时看按住 / 松开

#### [SkillComponent.cs](../Assets/Scripts/ECS/Components/SkillComponent.cs)

- 技能槽与施法状态拥有者
- 关键内容：
  - `SkillSlots`
  - `ActiveSkill`
  - `IsCasting`
  - `CastTimer`
  - `IsChanneling`
  - `ChannelTickTimer`
  - `ActiveChannelRuntime`
- 注意：默认方法签名 `InitializeSlots(int count = 6)` 仍写着 `6`，**但正式运行时已由 `GameSceneManager` / `PlayerController` 初始化为 8 槽**

#### [SkillFactory.cs](../Assets/Scripts/Game/Skills/SkillFactory.cs)

- 负责创建测试技能与从宝石映射 `SkillData`
- 当前是默认技能、支持宝石构造、配置表回退逻辑的重要入口

#### [SkillSystem.cs](../Assets/Scripts/ECS/Systems/SkillSystem.cs)

- 技能真正执行入口
- 当前负责：
  - 冷却推进
  - 前摇施法
  - 引导施法
  - 技能运行时实体创建
  - 投射物生成
  - 范围伤害与伤害类型推导
  - 施法锁移动

### 当前技能槽与键位约定

#### 技能槽容量

- 当前正式玩家技能槽容量：**8 槽**
- 显式入口：
  - [GameSceneManager.cs](../Assets/Scripts/Game/GameSceneManager.cs)
  - [PlayerController.cs](../Assets/Scripts/Game/Character/PlayerController.cs)

#### 默认技能键位

当前 `skill1~8` 默认映射为：

- `skill1 = LMB`
- `skill2 = MMB`
- `skill3 = RMB`
- `skill4 = Q`
- `skill5 = W`
- `skill6 = E`
- `skill7 = R`
- `skill8 = T`

对应入口：

- [GameSceneManager.cs](../Assets/Scripts/Game/GameSceneManager.cs)
- [PlayerController.cs](../Assets/Scripts/Game/Character/PlayerController.cs)
- [CharactorMainPanelController.cs](../Assets/Scripts/Game/UI/CharactorMainPanelController.cs) 的 `ResolveDefaultSkillKey(...)`

#### 药剂键位

- 当前顶排数字键 **`1 / 2 / 3 / 4 / 5`（非小键盘）** 会直接使用对应药剂槽
- 输入采集入口仍在 [GameSceneManager.cs](../Assets/Scripts/Game/GameSceneManager.cs)
- 输入写入 [PlayerInputComponent.cs](../Assets/Scripts/ECS/Components/PlayerInputComponent.cs) 的 `FlaskInputs`
- 每次使用当前都会扣除药剂的 `FlaskChargesPerUse`
- 药剂总充能当前会在生成时同时受**品质百分比**与**稀有度**加成影响
- [CharactorMainPanelController.cs](../Assets/Scripts/Game/UI/CharactorMainPanelController.cs) 当前会让底栏对应药剂槽 `Mask` 按“已消耗充能 / 总充能”显示遮罩；因此剩余充能越少，遮罩越多

### 左键分流约定

#### 当前行为

一次左键按下只会锁定为以下三种意图之一：

- `Skill1`
- `Move`
- `Blocked`

整次按住过程中不会中途切换。

#### 当前判定顺序

1. UI 命中 / 面板关闭 / NPC 名称点击优先阻断
2. 检查 `skill1` 是否满足施放条件
3. 不满足则回退为地面寻路

#### 当前 `skill1` 触发条件

必须同时满足：

- 槽位 0 有技能
- 未冷却
- 蓝量足够
- 未被其它施法状态阻塞
- 鼠标附近吸附到存活怪物
- 目标距离在由 `SkillData.Range / AreaRadius` 推导出的有效范围内

#### 当前选怪方式

- 不是 Physics 射线命中怪物
- 而是 `GameSceneManager` 基于 ECS 怪物列表，从鼠标落点附近吸附最近存活怪物

#### 关键入口

- `ResolveLeftMouseIntent()`
- `TryBeginLeftClickSkill()`
- `FindMonsterUnderCursor()`
- `ResolveLeftClickSkillRange()`

### 当前测试技能分配

看 [GameSceneInitializer.cs](../Assets/Scripts/Game/GameSceneInitializer.cs)：

- 槽位 0：重击
- 槽位 1：火球术 + 多重投射 + 附加火焰伤害
- 槽位 2：冰霜新星
- 槽位 3：闪现
- 槽位 4：旋风斩

### 配置表与技能工厂约定

#### 技能配置

- [SkillConfigLoader.cs](../Assets/Scripts/Game/Skills/SkillConfigLoader.cs) 当前负责读取：
  - `ActiveSkillStoneConf.pb`
  - `SupportSkillStoneConf.pb`

#### `SkillData` 当前关键字段

- `SkillEffectName`
- `IsChannelingSkill`
- `CanMoveWhileCasting`
- `SupportGems`

#### 技能特效

- [SkillEffectPool.cs](../Assets/Scripts/Game/Skills/SkillEffectPool.cs) 负责特效池化
- 当前约定优先读取：
  - `Resources/Effects/Skills/<SkillEffectName>.prefab`
- 缺失时回退为运行时生成的占位特效

### 技能栏显示链

#### 一个关键事实

- **技能能不能释放**：主链在 `GameSceneManager / SkillSystem / SkillComponent`
- **技能栏显示什么**：主链在 `BagPanel / CharactorMainPanelController`

不要把这两条链混成一条。

#### [CharactorMainPanelController.cs](../Assets/Scripts/Game/UI/CharactorMainPanelController.cs)

当前负责：

- 技能槽默认标签
- 从 `BagPanel` 读取当前插入的主动技能宝石
- 稳定槽位映射 `_skillSlotAssignments`
- 显示与主动技能已连接的辅助宝石标记
- 技能冷却遮罩 `Mask`

#### 当前底栏稳定映射

- 卸下某颗主动技能石时，只清空该槽位
- 其他未卸下技能不会自动左移
- 新装上的主动技能会补到第一个空槽

### 冷却 Mask 约定

- `SkillSlotArr` 每个技能槽会缓存子节点 `Mask` 的 `Image`
- 当前冷却遮罩使用：
  - `Image.Type.Filled`
  - `Radial360`
  - 顶部起点
  - `fillClockwise = true`
- 效果为：**顺时针方向收缩**

### 施法锁移动约定

- 当技能 `CanMoveWhileCasting = false` 时，`SkillSystem` 会给 `MovementComponent.IsMovementLockedByCasting` 加锁
- `MovementSystem` 与 `GameSceneManager` 会阻止继续移动或积压点击寻路

### 常见排查入口

#### 问题：技能图标有了，但按键没反应

优先看：

- [GameSceneManager.cs](../Assets/Scripts/Game/GameSceneManager.cs)
- [SkillSystem.cs](../Assets/Scripts/ECS/Systems/SkillSystem.cs)
- [SkillComponent.cs](../Assets/Scripts/ECS/Components/SkillComponent.cs)

#### 问题：键位显示不对

优先看：

- [CharactorMainPanelController.cs](../Assets/Scripts/Game/UI/CharactorMainPanelController.cs)
- `ResolveDefaultSkillKey(...)`
- [GameSceneManager.cs](../Assets/Scripts/Game/GameSceneManager.cs)
- [PlayerController.cs](../Assets/Scripts/Game/Character/PlayerController.cs)

#### 问题：左键行为异常

优先看：

- [GameSceneManager.cs](../Assets/Scripts/Game/GameSceneManager.cs)
- 左键分流相关方法
- `skill1` 当前技能类型是否仍适合左键近战判定

#### 问题：技能冷却遮罩不对 / 方向不对

优先看：

- [CharactorMainPanelController.cs](../Assets/Scripts/Game/UI/CharactorMainPanelController.cs)
- `UpdateSkillCooldownMasks()`
- `ApplySkillCooldownMask()`

### 从本模块跳到哪里

- **要看运行时主链 / ECS 总体职责**：转 [skill-02](./Skill_02_ECS与运行时主链.md)
- **要看技能宝石、装备孔与辅助宝石连结**：转 [skill-03](./Skill_03_背包_装备_宝石交互.md)
- **要看 UI 展示 / 角色底栏 / 角色面板**：转 [skill-04](./Skill_04_UI_Tips与角色面板.md)
- **要看 SOP / 排错与高风险点**：转 [skill-07](./Skill_07_SOP_排错与高风险点.md)
