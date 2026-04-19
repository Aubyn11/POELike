## POELike 项目记忆

> 本文档用于给未来的开发者 / AI 助手快速恢复项目上下文。内容基于当前仓库结构、关键脚本命名以及最近一段时间已经确认并落地的行为修改。

### 项目定位

`POELike` 是一个 **Unity + C#** 的类 POE ARPG 原型项目。

当前代码结构已经形成几个比较清晰的层次：

- **管理层**：负责游戏启动、场景切换、UI 管理
- **ECS 层**：负责实体、组件、系统以及战斗/属性/技能等核心逻辑
- **Game 层**：负责场景初始化、装备生成、NPC、背包、商店等具体玩法
- **UI 层**：负责背包、装备栏、Tips、对话框、商店等可视交互
- **工具链**：负责 Excel / 配置转运行时数据
- **UI 层级约定**：装备 Tips 使用独立最高层 `TooltipOverlay`，层级高于角色底部状态栏

### 环境与依赖

- Unity 版本以 [ProjectVersion.txt](ProjectSettings/ProjectVersion.txt) 为准
- 包依赖以 [manifest.json](Packages/manifest.json) 为准
- 当前代码可确认使用到：
  - Unity UI
  - TextMeshPro
  - 新输入系统（如 `UnityEngine.InputSystem`）

### 启动总链路

```mermaid
flowchart TD
    A[应用启动] --> B[SceneLoader / 初始场景]
    B --> C[角色选择界面 CharacterSelectPanel]
    C --> D[进入游戏场景]
    D --> E[GameManager 创建 ECS World]
    E --> F[注册 Systems]
    F --> G[GameSceneInitializer 初始化运行时内容]
    G --> H[GameSceneManager 驱动主玩法]
    H --> I[UIManager 管理面板]
```

### 目录与关键文件索引

#### 管理器

- [GameManager.cs](Assets/Scripts/Managers/GameManager.cs)
  - 游戏主入口之一
  - 负责 ECS World 初始化与系统注册
  - 负责在启动时调整 Input System 更新模式，并确保 `UIManager` 常驻
- [UIManager.cs](Assets/Scripts/Managers/UIManager.cs)
  - 负责 UI 面板加载、打开、关闭、缓存/池化
  - 负责创建常驻 `UIRootCanvas`、`UIEventSystem`、`TooltipOverlay`
  - 负责 `I` 键背包开关、`C` 键角色属性页开关与角色底栏排序控制
- [SceneLoader.cs](Assets/Scripts/Managers/SceneLoader.cs)
  - 负责场景切换入口

#### 场景与游戏流程

- [GameSceneManager.cs](Assets/Scripts/Game/GameSceneManager.cs)
  - 运行时主控制器之一
  - 串起玩家、怪物、NPC、UI 交互
  - 当前也是主要的**技能输入写入者**与**药剂热键处理入口**
- [GameSceneInitializer.cs](Assets/Scripts/Game/GameSceneInitializer.cs)
  - 负责进入游戏场景后的初始化
  - 当前会自动分配一套测试装备与测试技能
  - 通常适合查找运行时对象生成、环境布置、初始实体准备

#### ECS

- [World.cs](Assets/Scripts/ECS/Core/World.cs)
  - ECS 世界核心
  - 负责实体存储、查询、系统执行基础设施
- [StatsSystem.cs](Assets/Scripts/ECS/Systems/StatsSystem.cs)
  - 属性重算入口
- [MovementSystem.cs](Assets/Scripts/ECS/Systems/MovementSystem.cs)
  - 移动系统
  - 当前是**玩家 CPU 移动 + 怪物 GPU ComputeShader 并行模拟**的混合架构
- [CombatSystem.cs](Assets/Scripts/ECS/Systems/CombatSystem.cs)
  - 战斗结算
- [SkillSystem.cs](Assets/Scripts/ECS/Systems/SkillSystem.cs)
  - 技能释放 / 冷却 / 技能事件流
- [EquipmentComponent.cs](Assets/Scripts/ECS/Components/EquipmentComponent.cs)
  - 装备数据与角色属性挂接的重要组件
- [SkillComponent.cs](Assets/Scripts/ECS/Components/SkillComponent.cs)
  - 技能槽、施法状态、运行时技能数据
- [MonsterComponent.cs](Assets/Scripts/ECS/Components/MonsterComponent.cs)
  - 怪物运行时数据入口之一

#### 装备 / 配置 / 商店 / NPC

- [EquipmentGenerator.cs](Assets/Scripts/Game/Equipment/EquipmentGenerator.cs)
  - 运行时装备生成核心
  - `GeneratedEquipment`、词缀、插槽、商店货品通常都从这里延伸
- [EquipmentConfigLoader.cs](Assets/Scripts/Game/Equipment/EquipmentConfigLoader.cs)
  - 装备配置加载器
- [ShopPanel.cs](Assets/Scripts/Game/UI/ShopPanel.cs)
  - 商店 UI
- [NpcConfigLoader.cs](Assets/Scripts/Game/NpcConfigLoader.cs)
  - NPC / 对话 / 按钮配置加载
- [NpcButtonEventType.cs](Assets/Scripts/Game/NpcButtonEventType.cs)
  - NPC 对话按钮事件枚举
- [NpcDialogPanel.cs](Assets/Scripts/Game/UI/NpcDialogPanel.cs)
  - NPC 对话面板

#### 背包 / 装备 UI

- [BagPanel.cs](Assets/Scripts/Game/UI/BagPanel.cs)
  - 背包主面板控制器
  - 当前带 `_isInitializing` 防重入保护
- [BagBox.cs](Assets/Scripts/Game/UI/BagBox.cs)
  - 背包格子容器、占用与放置规则
- [BagCell.cs](Assets/Scripts/Game/UI/BagCell.cs)
  - 单个格子点击 / 悬停响应
- [BagItemView.cs](Assets/Scripts/Game/UI/BagItemView.cs)
  - 道具在背包 / 装备栏 / 插槽之间移动的核心状态机
- [BagItemData.cs](Assets/Scripts/Game/UI/BagItemData.cs)
  - UI 侧道具数据模型
- [EquipmentItem.cs](Assets/Scripts/Game/UI/EquipmentItem.cs)
  - 装备图标可视层、插槽显示、Tips 入口
  - 当前负责宝石槽动态布局、动态缩放、自动连接线
- [PlayerInputComponent.cs](Assets/Scripts/ECS/Components/PlayerInputComponent.cs)
  - 当前 `SkillInputs` 已扩容到 8
- 8 个技能槽位真实快捷键约定为：`LMB / MMB / RMB / Q / W / E / R / T`

- [CharactorMainPanelController.cs](Assets/Scripts/Game/UI/CharactorMainPanelController.cs)
  - 真实技能栏按钮标签已与上述键位同步
  - 最后两个扩展槽位当前显示为 `X` / `V`
- [SkillEffectPool.cs](Assets/Scripts/Game/Skills/SkillEffectPool.cs)
  - 运行时兜底粒子模板创建时，必须先禁用模板对象，并在配置粒子参数前先 `Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear)`
  - 否则刚 `AddComponent<ParticleSystem>()` 的系统可能还处于默认播放态，随后改 `main.duration` / `startLifetime` 会触发 Unity 警告
- [ActiveSkillStoneConf.pb](Assets/Cfg/ActiveSkillStoneConf.pb) / [SkillConfigLoader.cs](Assets/Scripts/Game/Skills/SkillConfigLoader.cs)
  - 主动技能表新增 `IsChannelingSkill` 与 `CanMoveWhileCasting`
  - 当前已为 `fireball / frost_nova / blink / heavy_strike` 补值，并新增 `cyclone` 条目
- [SkillComponent.cs](Assets/Scripts/ECS/Components/SkillComponent.cs)
  - `SkillData` 已新增 `IsChannelingSkill` 与 `CanMoveWhileCasting`
  - `SkillComponent` 已新增引导态字段：`IsChanneling` / `ChannelTickTimer` / `ActiveChannelRuntime`
- [PlayerInputComponent.cs](Assets/Scripts/ECS/Components/PlayerInputComponent.cs)
  - 技能输入现在不只有 `SkillInputs`，还包括 `SkillHeldInputs` 与 `SkillReleasedInputs`
  - `GameSceneManager` / `PlayerController` 都已统一采集 8 槽技能键的按下、按住、松开三态
- [SkillSystem.cs](Assets/Scripts/ECS/Systems/SkillSystem.cs)
  - 已补完整引导状态机：按下开始、按住维持、松手结束、缺蓝停止
  - `IsChannelingSkill=true` 且 `SkillType=Channeling` 时，会维持单个持续运行时实体；其它引导技能则按间隔重复释放
  - 普通施法与引导施法都会在 `CanMoveWhileCasting=false` 时给移动组件加锁
- [MovementComponent.cs](Assets/Scripts/ECS/Components/MovementComponent.cs) / [MovementSystem.cs](Assets/Scripts/ECS/Systems/MovementSystem.cs)
  - 新增 `IsMovementLockedByCasting`
  - 施法锁移动期间会清空寻路目标与移动方向，防止施法结束后补走旧路径

- [EquipmentSlotView.cs](Assets/Scripts/Game/UI/EquipmentSlotView.cs)
  - 装备栏槽位
- [SocketItem.cs](Assets/Scripts/Game/UI/SocketItem.cs)
  - 宝石孔槽位
- [EquipmentTips.cs](Assets/Scripts/Game/UI/EquipmentTips.cs)
  - 装备提示框
- [CharactorMainPanelController.cs](Assets/Scripts/Game/UI/CharactorMainPanelController.cs)
  - 角色底栏显示控制器
  - 当前从 `BagPanel` 刷药剂与主动技能宝石显示
- [CharactorMassagePanelController.cs](Assets/Scripts/Game/UI/CharactorMassagePanelController.cs)
  - 角色信息 / 属性详情面板控制器
  - 当前负责角色名称、等级、力量、智力、敏捷，以及伤害 / 防御 / 其他属性分类列表刷新
  - 当前会在保留预置属性顺序的基础上，把新增属性按三类自动归类追加到面板列表
  - 当前还会从 `BagPanel.GetEquippedItemData(slot)` 读取所有已装备装备的 `RolledMod`，按 `EquipmentModData.EquipmentModDisplayTab` 显式分类追加到对应页签

#### 角色选择 / 存档 / 调试

- [CharacterSelectPanel.cs](Assets/Scripts/Game/UI/CharacterSelectPanel.cs)
  - 角色选择与进游戏入口
- [GMPanel.cs](Assets/Scripts/Game/UI/GMPanel.cs)
  - 调试 / GM 功能面板，具体能力以源码为准

#### 工具链

- [Program.cs](Tools/GenEquipmentExcel/Program.cs)
  - Excel 转装备配置工具入口
- [启动ExcelConvert.bat](启动ExcelConvert.bat)
  - 工具链批处理入口

### ECS 架构记忆

#### 核心理解

项目的 ECS 主体可以按下列思路理解：

- `World` 是运行时容器
- Entity 负责标识实体
- Component 负责承载数据
- System 负责扫描实体并执行业务逻辑

#### 已知关键系统职责

- **`StatsSystem`**
  - 负责基础属性、装备属性、修饰器的综合重算
  - 改属性问题时优先看这里
- **`MovementSystem`**
  - 负责实体移动
  - 当前不是纯 CPU 简单遍历，而是：
    - 玩家移动：CPU 处理
    - 怪物移动：ComputeShader + AsyncGPUReadback + CPU 回退
- **`CombatSystem`**
  - 负责伤害、死亡、战斗事件
- **`SkillSystem`**
  - 负责技能触发、技能状态、冷却与技能事件流

#### ECS 与玩法层的关系

- 装备、NPC、玩家、怪物等可见对象，通常最终都要落回 ECS 世界或由 ECS 状态驱动
- 场景初始化负责把“场景对象 / 数据配置”接到 ECS
- UI 更多是 ECS / Game 数据的展示和操作入口，而不是最终权威数据源

### 游戏场景与运行时职责

`GameSceneManager` 与 `GameSceneInitializer` 是接手运行时逻辑时最值得先读的两个文件：

- `GameSceneInitializer`
  - 更偏初始化与布置
  - 适合查“进入场景时做了什么”
  - 当前会自动给玩家挂测试装备，并分配测试技能
- `GameSceneManager`
  - 更偏运行时总控
  - 适合查“玩家/怪物/NPC/UI 怎么串起来”
  - 当前还负责：
    - 鼠标地面寻路
    - NPC 标签点击寻路与到达后对话
    - 技能输入写入
    - 药剂快捷键使用

可把它们理解为：

- **Initializer**：把世界搭起来
- **SceneManager**：让世界运行起来

### UI 架构记忆

#### `UIManager` 的定位

`UIManager` 负责：

- 面板加载
- 面板打开 / 关闭
- 可能的实例复用 / 池化
- 场景切换时 UI 生命周期协调
- 游戏内 UI 排序层控制（包括角色底栏与 Tooltip Overlay）
- 常驻 `UIRootCanvas` / `UIEventSystem` / `TooltipOverlay` 的自动创建
- `I` 键开关背包，并通过帧去重避免同帧重复切换
- `C` 键开关 `CharactorMassagePanel`，并通过帧去重避免同帧重复切换

当你要改任意 UI 面板的打开方式时，优先检查：

- `UIManager`
- 面板预制体路径
- 面板脚本的初始化入口

#### 游戏玩法 UI 的几类典型面板

- 背包：`BagPanel`
- 商店：`ShopPanel`
- NPC 对话：`NpcDialogPanel`
- 调试：`GMPanel`
- 角色选择：`CharacterSelectPanel`
- 角色底栏：`CharactorMainPanelController`

### 背包 / 装备 / 宝石系统记忆

#### 当前交互模型（非常重要）

当前背包系统已经不是传统拖拽，而是 **点击搬运**：

1. **第一次点击物品**：拿起物品
2. **物品进入跟随鼠标状态**
3. **再次点击目标位置**：放下物品
4. 目标位置可为：
   - 背包格子
   - 装备栏槽位
   - 宝石孔

这个行为约定来自最近确认过的改动，后续不要再误判为“仍是拖拽系统”。

#### 当前替换规则（非常重要）

近期已经确认并落地的替换链路如下：

- **装备槽 / 药剂槽**：当目标槽位已有内容时，只要槽位类型等其他接纳条件满足，就会 **直接替换**，被替换物会 **立即跟随鼠标移动**
- **宝石孔**：当目标孔位已有宝石时，只要颜色等其他接纳条件满足，就会 **直接替换**，被替换宝石会 **立即跟随鼠标移动**
- **背包格子**：当目标区域已被占用时，只要整个目标区域只被 **同一个道具** 占用，也允许整件替换；点击被占用物时，落点优先按 **鼠标当前所在格子** 判断
- **状态保真**：从装备槽替换下来的物品会保留原 `RuntimeItemData`，尤其是药剂充能这类运行时状态

#### 当前职责分工

- **`BagItemView`**
  - 全局“正在移动的物品”状态
  - 父节点切换
  - 放置完成 / 放回原容器
  - 背包大小恢复
  - 点击被占用目标时，把放置尝试转发给目标容器
- **`BagBox`**
  - 负责网格放置规则与占用判断
  - 点击格子时尝试放下当前物品
  - 负责识别“可放置”与“可替换”的差异
- **`BagCell`**
  - 单格点击与悬停视觉
- **`EquipmentSlotView`**
  - 负责装备槽 / 药剂槽接纳装备
  - 点击已装备物品时，也能再次拿起移动
  - 合法替换时会让旧物品接棒拖拽
  - 清空槽位时会触发角色底栏刷新
- **`SocketItem`**
  - 负责宝石孔接纳宝石
  - 合法替换时会让旧宝石接棒拖拽
- **`BagPanel`**
  - 当前会递归查找并注册 `Potion1 ~ Potion5`，因此药剂槽可放在装备区下方的嵌套层级
  - 当前 `EnsureInitialized()` 使用 `_isInitializing` 防重入，避免角色底栏刷新链再次回调自身

#### 一个关键桥接对象：`BagItemData`

`BagItemData` 是 UI 侧非常关键的数据承载层，用来承接：

- 名称
- 图标
- 背包占格尺寸
- 当前网格坐标
- 装备槽位限制
- 插槽信息
- 当前已确认加入的前后缀显示文案

近期已经确认：

- `BagItemData` 中新增了前后缀文案承载能力
- 相关字段用于让背包装备 Tips 直接显示词缀，而不是只显示尺寸 / 类型

#### 装备卸下尺寸恢复

这是一个已经修复过的已知历史问题：

- 装备放进装备栏后，会被槽位视觉拉伸
- 过去从装备栏拿起时会沿用“槽位拉伸后的尺寸”
- 现在已修正为：**从装备栏卸下并进入移动状态时，恢复背包原始占格尺寸**

后续若再次出现“拿起尺寸不对”，优先检查：

- `BagItemView.BindToBag(...)`
- `BagItemView.ReparentForDragging()`
- 背包格子尺寸缓存逻辑

#### 宝石槽显示的新约定

近期已确认并落地：

- `EquipmentItem` 不再只显示固定大小的宝石孔
- 宝石槽区域现在会根据装备当前显示尺寸动态计算：
  - 插槽尺寸
  - 插槽间距
  - 面板总尺寸
  - 连接线粗细
- 装备放在装备栏里变大后，**宝石槽、宝石显示、连接线都会一起放大**
- 当前连接线由 `EquipmentItem` 按**相邻索引规则**生成，只认 `index-1` / `index+1`
- `EquipmentItem` 当前提供：
  - `AreSocketsLinked(...)`
  - `TryGetLinkedSocketIndices(...)`
  - `GetLinkedGems(...)`
- `BagPanel` 当前提供项目级聚合入口：
  - `GetLinkedGems(EquipmentSlot slot, int socketIndex, List<BagItemData> results)`

一个非常重要的限制：

- [EquipmentGenerator.cs](Assets/Scripts/Game/Equipment/EquipmentGenerator.cs) 中的 `SocketData` **当前只有 `Color` 字段**
- 也就是说，项目里**还没有真正的数据驱动插槽连接拓扑**
- 现在看到的连接线是 **基于插槽索引规则的 UI 推导关系**，不是配置关系

### 装备 Tips 系统记忆

#### `EquipmentTips` 的职责

`EquipmentTips` 负责装备提示内容本身。

#### 当前 Tips 行为约定（非常重要）

近期已确认并落地的规则：

- Tips 会 **跟随装备当前所在位置** 显示，而不是固定在背包初始位置
- 当 Tips 超出屏幕边界时，会 **翻到装备另一侧打开**
- 背包装备 Tips 已调整为：
  - **不显示装备类型与占用尺寸**
  - **显示前缀 / 后缀词条**
- 药剂 Tips 已调整为：
  - **不显示占用尺寸**
  - **不显示可装备槽位**
  - **显示药剂类型、当前/最大充能、每次使用消耗、需求等级、恢复/持续/功能效果**
- 装备 Tips 渲染层级已调整为：
  - **Tips 最高层**
  - **状态栏次之**
  - 具体实现依赖 `UIManager.TooltipOverlayRoot` 与 `TooltipOverlaySortingOrder`

#### 当前职责分工

- **`EquipmentItem`**
  - 负责 Tips 的显示时机、定位、翻边、跟随
  - 负责把 Tips 挂到 `UIManager.TooltipOverlayRoot`
- **`EquipmentTips`**
  - 负责 Tips 的内容填充与布局刷新
- **`BagItemData` / `GeneratedEquipment`**
  - 负责提供 Tips 所需的数据
- **`UIManager`**
  - 负责创建 `TooltipOverlay` 最高层 Canvas
  - 负责保证其排序高于角色主界面底栏

#### 两条数据路径

项目里至少存在两类装备 Tips 数据来源：

- **完整装备数据**：通常来自 `GeneratedEquipment`
- **背包基础 UI 数据**：通常来自 `BagItemData`

这意味着你改 Tips 时要注意：

- 商店 / 掉落 / 生成装备可能有完整运行时词缀数据
- 背包演示 / UI 中转物品可能只带 `BagItemData`
- 所以 Tips 常常需要同时兼容两种数据来源

### 技能系统记忆

#### 当前技能数据模型

看 [SkillComponent.cs](Assets/Scripts/ECS/Components/SkillComponent.cs)：

- `SkillComponent`：技能槽与施法状态
- `SkillData`：技能运行时定义
- `SkillType`：技能类别
- `SupportGem`：支持宝石数据

当前 `SkillComponent.InitializeSlots(6)` 会给玩家初始化 **6 个技能槽**。

#### 当前技能触发主链

当前较新的主链路为：

1. [GameSceneManager.cs](Assets/Scripts/Game/GameSceneManager.cs) 在 `UpdateInput()` 中读取输入
2. 将输入写入 `PlayerInputComponent.SkillInputs`
3. 发布 `SkillActivateEvent`
4. [SkillSystem.cs](Assets/Scripts/ECS/Systems/SkillSystem.cs) 订阅并处理该事件
5. `SkillSystem` 负责：
   - 冷却扣减
   - 施法状态推进
   - 魔力消耗检查
   - 立即释放 / 延迟施法
   - 发布 `SkillCastStartEvent`
   - 执行完后发布 `SkillExecutedEvent`

#### 当前技能热键

以 [GameSceneManager.cs](Assets/Scripts/Game/GameSceneManager.cs) 为准：

- 普攻：项目级 `Attack` Action
- 技能 2~6：`E` / `R` / `T` / `F` / `G`
- 药剂 1~5：`1` / `2` / `3` / `4` / `5`

#### `GameSceneInitializer` 当前自动分配的测试技能

- 槽位 0：重击（当前替代原普通攻击，已接配置表特效）
- 槽位 1：火球术 + 多重投射支持宝石 + 附加火焰伤害支持宝石
- 槽位 2：冰霜新星
- 槽位 3：闪现
- 槽位 4：旋风斩

补充记忆：

- `SkillData` 现已新增 `SkillEffectName`
- `Assets/Cfg/ActiveSkillStoneConf.pb` / `common/cfg/skillStone.txt` 中的 `ActiveSkillStoneConf` 现已新增 `SkillEffectName` 字段
- 新增 [SkillConfigLoader.cs](Assets/Scripts/Game/Skills/SkillConfigLoader.cs) 负责读取主动技能基础配置，并按 `ActiveSkillStoneCode` 查询技能特效名
- 新增 [SkillEffectPool.cs](Assets/Scripts/Game/Skills/SkillEffectPool.cs) 负责技能特效池化；进入游戏时会先预加载配置表中的技能特效，再按玩家当前 `SkillSlot` 里的技能再预热一次
- [SkillSystem.cs](Assets/Scripts/ECS/Systems/SkillSystem.cs) 现在会在 `ExecuteSkill(...)` 里统一调用 `SkillEffectPool.PlaySkillEffect(...)`
- 当前特效资源路径约定为 `Resources/Effects/Skills/<SkillEffectName>.prefab`；若 prefab 缺失，会自动回退为运行时生成的占位粒子，便于先联调技能逻辑链路

#### GM 面板与物品生成链路补充

- [GMPanel.cs](Assets/Scripts/Game/UI/GMPanel.cs) 现已支持生成装备和宝石到当前背包，入口仍是 `F1`
- [BagPanel.cs](Assets/Scripts/Game/UI/BagPanel.cs) 已补充 `POELike.Managers` 命名空间引用，修复 `UIManager` 未识别的编译错误
- 新增客户端技能拓展入口：[Assets/Scripts/Game/UI/ClientSkillExtensionPanel.cs](D:/Learning/POELike/Assets/Scripts/Game/UI/ClientSkillExtensionPanel.cs) 使用 `F2` 打开 IMGUI 面板，点击临时技能 Prefab 按钮可直接触发技能
- 已修复 [Assets/Scripts/ECS/Systems/SkillGpuSystem.cs](D:/Learning/POELike/Assets/Scripts/ECS/Systems/SkillGpuSystem.cs) 中未使用的 `AsyncGPUReadbackRequest` 残留字段导致的编译错误，当前实现仅保留同步 `GetData` 读回
- 已将 [Assets/Scripts/Game/UI/CharactorMainPanelController.cs](D:/Learning/POELike/Assets/Scripts/Game/UI/CharactorMainPanelController.cs) 从展示型底栏升级为真实技能栏：UI 槽位同步玩家 `SkillComponent`，支持从装备中的主动/支持宝石还原技能并可点击施法
- 新增 [Assets/Scripts/Game/UI/SkillBarSlotButton.cs](D:/Learning/POELike/Assets/Scripts/Game/UI/SkillBarSlotButton.cs) 底栏点击组件；[Assets/Scripts/Game/Skills/SkillFactory.cs](D:/Learning/POELike/Assets/Scripts/Game/Skills/SkillFactory.cs) 已补充从宝石名称/代码解析 `SkillData` 与 `SupportGem` 的方法
- [Assets/Scripts/ECS/Components/SkillComponent.cs](D:/Learning/POELike/Assets/Scripts/ECS/Components/SkillComponent.cs)、[Assets/Scripts/Game/GameSceneManager.cs](D:/Learning/POELike/Assets/Scripts/Game/GameSceneManager.cs)、[Assets/Scripts/Game/Character/PlayerController.cs](D:/Learning/POELike/Assets/Scripts/Game/Character/PlayerController.cs) 已统一为 8 槽技能栏容量，技能栏点击与键盘输入共用同一套 ECS 施法链

- 新增技能运行时 ECS/GPU 闭环：[Assets/Scripts/ECS/Components/SkillRuntimeComponent.cs](D:/Learning/POELike/Assets/Scripts/ECS/Components/SkillRuntimeComponent.cs)、[Assets/Scripts/ECS/Systems/SkillGpuSystem.cs](D:/Learning/POELike/Assets/Scripts/ECS/Systems/SkillGpuSystem.cs)、[Assets/Resources/Shaders/SkillRangeHitCompute.compute](D:/Learning/POELike/Assets/Resources/Shaders/SkillRangeHitCompute.compute) 负责技能范围命中 GPU 计算与 ECS 伤害回写
- 新增技能范围可视化：[Assets/Scripts/Game/SkillRuntimeRenderer.cs](D:/Learning/POELike/Assets/Scripts/Game/SkillRuntimeRenderer.cs) 和 [Assets/Resources/SkillRuntimeOverlay.shader](D:/Learning/POELike/Assets/Resources/SkillRuntimeOverlay.shader) 在主摄像机上绘制技能运行时圆环
- [Assets/Scripts/ECS/Systems/SkillSystem.cs](D:/Learning/POELike/Assets/Scripts/ECS/Systems/SkillSystem.cs) 已扩展为创建技能运行时实体； [Assets/Scripts/Game/GameSceneManager.cs](D:/Learning/POELike/Assets/Scripts/Game/GameSceneManager.cs) 现会分配默认技能并初始化客户端技能拓展面板； [Assets/Scripts/ECS/Systems/CombatSystem.cs](D:/Learning/POELike/Assets/Scripts/ECS/Systems/CombatSystem.cs) 已补充纯 ECS 怪物死亡回收

- GM 面板中的装备/宝石参数选择已枚举化：装备基础、前缀、后缀、宝石内容都改成运行时下拉式选择
- GM 装备生成区现使用“候选下拉 + 已选列表”管理前后缀，最多各 3 条；孔数使用数值按钮，孔颜色和连接状态使用枚举按钮
- GM 宝石生成区现使用主动/辅助切换 + 宝石内容下拉 + 宝石颜色枚举按钮，不再依赖手工输入名称/code/ID

- 新增 [GMItemFactory.cs](Assets/Scripts/Game/Items/GMItemFactory.cs) 负责把 GM 面板输入解析为 `BagItemData`
- 新增 [EquipmentBagDataFactory.cs](Assets/Scripts/Game/Equipment/EquipmentBagDataFactory.cs) 负责共享 `GeneratedEquipment -> BagItemData` 转换逻辑；[ShopPanel.cs](Assets/Scripts/Game/UI/ShopPanel.cs) 已切到复用这层能力
- [BagPanel.cs](Assets/Scripts/Game/UI/BagPanel.cs) 现已新增 `TryAddItemToBag(...)` 供 GM / 后续掉落 / 调试功能直接塞包
- [SkillConfigLoader.cs](Assets/Scripts/Game/Skills/SkillConfigLoader.cs) 现已同时支持主动技能和辅助宝石配置读取（`ActiveSkillStoneConf.pb` + `SupportSkillStoneConf.pb`）
- `SocketData` 现已新增 `LinkedToPrevious`，装备孔连结状态不再默认由“相邻孔”隐式决定，而是由每个孔显式记录
- [EquipmentItem.cs](Assets/Scripts/Game/UI/EquipmentItem.cs) 中 `AreSocketsLinked(...)` / `TryGetLinkedSocketIndices(...)` / `GetLinkedGems(...)` 均已改为读取 `SocketData.LinkedToPrevious`
- `EquipmentPart` 配置当前为数值编码：`1=主手, 2=副手, 3=头盔, 4=胸甲, 5=手套, 6=鞋子, 7=饰品`；其中饰品再通过 `EquipmentSubCategoryConf` 细分为腰带 / 戒指 / 项链

#### 当前技能实现状态的一个关键事实

- [SkillFactory.cs](Assets/Scripts/Game/Skills/SkillFactory.cs) 中 `CreateCyclone()` 会创建 `SkillType.Channeling`
- 但 [SkillSystem.cs](Assets/Scripts/ECS/Systems/SkillSystem.cs) 当前 `ExecuteSkill(...)` 只处理：
  - `Projectile`
  - `AoE`
  - `Attack`
  - `Movement`
- **`Channeling` 当前没有执行分支**

这意味着：

- 旋风斩现在更像是“已分配到测试槽位，但逻辑尚未补全”的状态
- 后续若有人反馈“旋风斩图标/初始化都在，但按键没效果”，这是非常可能的根因
- 当前 `SkillSystem` 仍主要消费 `SkillData.SupportGems`，还没有自动把“装备孔位里的支持宝石连结关系”装配进运行时技能
- 后续若要接这条链，优先复用 [EquipmentItem.cs](Assets/Scripts/Game/UI/EquipmentItem.cs) 与 [BagPanel.cs](Assets/Scripts/Game/UI/BagPanel.cs) 新增的相邻连结查询接口

#### 角色底栏与技能的关系

看 [CharactorMainPanelController.cs](Assets/Scripts/Game/UI/CharactorMainPanelController.cs)：

- 底栏技能区不是直接从 `SkillComponent.SkillSlots` 取图标
- `RefreshFromCurrentState()` 会从 `BagPanel.GetSocketedActiveGems(...)` 获取当前已镶嵌的主动技能宝石
- `RefreshFromCurrentState()` 会先调用 `SyncSkillSlotAssignments()`
- `SyncSkillSlotAssignments()` 会清掉已卸下的主动技能石，再把新出现的主动技能补到空槽
- `ApplySkills()` 当前根据 `_skillSlotAssignments` 渲染技能槽，而不是每次按 `_socketedActiveGems` 顺序压缩重排
- 每个技能槽下新增的 `SkillSlot` `ListBox` 当前用于显示**与该主动宝石已连接的辅助宝石**
  - `CharactorMainPanelController` 会遍历已装备物品的插槽连结关系，找到包含该主动宝石的相邻连结宝石，再过滤出 `BagGemKind.Support`
  - 每颗已连接辅助宝石都会生成一个 `FZSlot`
  - `FZSlot/Bg` 使用辅助宝石的 `ItemColor`
  - `FZSlot/Color` 使用 `R` / `G` / `B` 标记辅助宝石颜色（白色宝石显示 `W`）
  - 角色底栏里的这些 `FZSlot` 通过 `ListBox.StretchItemsOnCrossAxis = false` 保持 prefab 原始尺寸，不做横向拉伸，只更新显示内容
  - `ListBox` 现已支持双轴布局：`PrimaryAxis` 控制先按行还是先按列排，`HorizontalDirection` / `VerticalDirection` 分别控制横纵方向；如需真正使用两个方向同时排布，需要开启 `WrapItems`
  - 旧 `Direction` 仍保留为兼容旧 prefab 的入口，但后续新界面优先使用双轴字段
  - `CharactorMainPanelController` 这里依赖的 `EquipmentSlot` 实际定义在 `POELike.ECS.Components`；如果只引 `POELike.Game.Equipment` 会触发 `CS0246`

因此当前底栏默认具备“稳定槽位”行为：

- 多个技能装配后，未被卸下的主动技能石会保持原槽位
- 卸下其中某个技能石时，只会空出它原本占用的那个槽位
- 当前稳定映射依赖 `BagItemData` 引用识别同一颗宝石；若未来改为复制 / 重建 `BagItemData`，需要同步改映射键

因此要牢记：

- **技能能否释放**：主链在 `GameSceneManager` / `SkillSystem` / `SkillComponent`
- **底栏技能图标显示什么**：主链在 `BagPanel` / `CharactorMainPanelController`

### 装备生成系统记忆

#### `EquipmentGenerator`

该文件是装备运行时生成的核心来源之一，通常包含：

- `GeneratedEquipment`
- 装备基底类型
- 品质 / 名称 / 颜色
- 前后缀词条
- 插槽与颜色
- 商店货品或掉落物生成逻辑

#### `EquipmentConfigLoader`

负责把配置表读入运行时缓存。后续新增装备时，通常不是直接在生成器里硬编码，而是遵循：

1. 先补配置
2. 再由加载器读入
3. 再由生成器消费配置
4. 最终由 UI 展示

#### Shop 与装备生成的关系

`ShopPanel` 是一个很重要的“参考实现”：

- 它往往展示的是完整 `GeneratedEquipment`
- 也因此很适合用来追装备名、品质色、词缀、插槽在 UI 中的展示方式
- 若购买后转背包，则通常需要把展示所需信息拷贝到 `BagItemData`

### NPC / 对话 / 商店系统记忆

#### NPC 配置

- `NpcConfigLoader` 负责读 NPC 对话 / 按钮 / 行为配置
- `NpcButtonEventType` 记录按钮事件类型
- `NpcDialogPanel` 负责实际展示

#### 接手建议

如果要改 NPC 交互，顺序建议是：

1. 先看 `NpcButtonEventType` 有哪些事件枚举
2. 再看 `NpcConfigLoader` 怎么把配置转成运行时数据
3. 再看 `NpcDialogPanel` 怎么展示按钮和响应点击
4. 最后再看 `GameSceneManager` 或场景交互层怎么触发 NPC 面板

#### 商店系统

- `ShopPanel` 是装备展示、购买与 UI 转换的重要节点
- 改商品展示样式、购买后进背包、从 `GeneratedEquipment` 到 `BagItemData` 的映射时，优先从这里入手

### 角色选择 / 存档 / 场景切换记忆

#### 当前入口链路

- `SceneLoader`：负责场景切换
- `CharacterSelectPanel`：角色选择 / 进入游戏
- `GameManager`：进入游戏后创建 ECS 世界

#### 接手时的理解方式

可以把它理解成：

- **角色选择阶段**：选择“加载哪个角色数据”
- **进游戏阶段**：把角色信息带进主场景
- **游戏运行阶段**：由 `GameManager + GameSceneManager + World` 驱动

若未来要改“进游戏时带入哪些角色属性 / 背包 / 装备 / 存档字段”，一般需要同时检查：

- `CharacterSelectPanel`
- 存档读取逻辑
- `GameManager`
- `GameSceneManager`

### 配置与工具链记忆

配置链路中至少有以下已知节点：

- [Program.cs](Tools/GenEquipmentExcel/Program.cs)
- [启动ExcelConvert.bat](启动ExcelConvert.bat)
- 各类 ConfigLoader

推荐理解为：

```mermaid
flowchart LR
    A[Excel / 原始配置] --> B[转换工具 GenEquipmentExcel]
    B --> C[运行时配置文件]
    C --> D[ConfigLoader 读取]
    D --> E[Generator / Gameplay 使用]
    E --> F[UI 展示]
```

如果后续遇到“配置改了但游戏里没变”，优先检查：

- 是否重新执行了 Excel 转换
- 产物有没有更新
- Loader 是否加载了新字段
- Generator / UI 是否消费了该字段

### 最近已确认的行为修改

这些是**已经作为当前项目行为约定存在**的内容：

- **背包移动方式**：从拖拽改为点击拿起 / 点击放下
- **背包点击平滑跟手**：`BagItemView` 当前使用 `DragFollowSmoothTime` + `SmoothDamp` 追随鼠标与背包预览格；首次拿起仍立即贴到鼠标，避免起手迟滞
- **角色信息面板接入**：`CharactorMassagePanel` 当前不再随进入 `GameScene` 自动弹出，而是由 `UIManager` 使用 `C` 键开关
- **游戏内 ESC 关闭策略**：当前由 `UIManager` 接管 `ESC`，一次关闭所有当前已打开的可关闭游戏内 UI，不再依赖 Unity EventSystem 默认 `Cancel`
  - 当前覆盖范围包括：背包、`CharactorMassagePanel`、以及继承 `UIGamePanel` 的窗口
  - 当前实现会先调用 `UIGamePanelManager.CloseAll()`，再隐藏背包与 `CharactorMassagePanel`
- **默认 Cancel 已禁用**：当前 `UIManager` 会把 `InputSystemUIInputModule.cancel` 置空，避免 `ESC` 被派发给当前焦点按钮后引发多个 UI 连锁关闭
- **角色信息面板数据来源**：`CharactorMassagePanelController` 当前从 `SceneLoader.PendingCharacterData` 读取角色名称 / 等级，从玩家实体 `StatsComponent` 读取力量 / 智力 / 敏捷及属性明细
- **角色信息面板实时刷新**：穿戴 / 卸下装备、插入 / 取下宝石后，`EquipmentSlotView` / `SocketItem` 会继续调用 `UIManager.RefreshCharactorMainPanel()`，`CharactorMassagePanel` 会同步刷新最新属性
- **角色信息面板按钮分类**：
  - `DamageBtn`：显示伤害类属性
  - `DefenceBtn`：显示防御类属性
  - `OtherBtn`：显示非前两类的其他属性
- **角色信息面板动态归类**：若装备提供了原先预置数组中没有的新 `StatType`，`CharactorMassagePanelController` 会先按名称关键字判断属于伤害 / 防御 / 其他中的哪一类，再追加到对应分类列表
- **角色信息面板列表实现**：当前复用 `MassageArr` 的 `ListBox` 与 `Massage.prefab`，条目左侧 `Text` 为描述，右侧 `Value` 为数值
- **玩家基础三维属性默认值**：`GameSceneManager` / `PlayerController` 当前都会给玩家初始化 `力量 / 敏捷 / 智力 = 10`
- **Tips 跟随位置**：Tips 跟随装备当前所在位置，而不是停在背包旧坐标
- **Tips 越界翻边**：超出屏幕时自动显示到另一侧
- **Tips 内容**：背包装备不显示类型与尺寸，显示前后缀词条
- **Tips 层级**：装备 Tips 使用独立 `TooltipOverlay` 最高层，保证显示在状态栏之上
- **卸下恢复尺寸**：从装备栏拿起时恢复背包原始大小
- **背包初始化防重入**：`BagPanel.EnsureInitialized()` 使用 `_isInitializing` 保护，角色底栏刷新遇到初始化期会直接返回
- **装备底栏刷新保护**：`CharactorMainPanelController.RefreshFromCurrentState()` 会在 `bagPanel == null` 或 `bagPanel.IsInitializing` 时跳过刷新
- **技能栏稳定槽位**：`CharactorMainPanelController` 通过 `_skillSlotAssignments` / `SyncSkillSlotAssignments()` 保留主动技能宝石原槽位；卸下某颗技能石时只清空对应槽位，其余技能不再自动左移，新技能补进空槽
- **文档同步约定**：后续每次有效开发步骤完成后，都要同步更新 `POELike_接手Skill.md` 与 `POELike_项目记忆.md`
- **宝石槽布局升级**：装备宝石槽改为动态布局、动态缩放、自动连接线，装备栏放大时宝石区域同步放大
- **宝石连线限制**：当前连接线仍然是 UI 推导，不是数据驱动拓扑
- **历史编译问题**：曾发生过误把补丁标记写进源码，导致 `BagItemView.cs` 报 `CS0106`，后续若出现类似问题先检查源码里是否混入了 `+` / `-` / `@@`

#### 装备词条 → 角色属性链路（**已打通**）

近期已确认并落地：

- `EquipmentModConf` 新增三个字段：
  - **`EquipmentModDisplayTab`**：`"1"` / `"2"` / `"3"` 表示词条显示在 **伤害 / 防御 / 其他** 哪个页签
  - **`EquipmentModStatType`**：对应 `StatType` 枚举名。留空表示该词条不写入 `StatsComponent`
  - **`EquipmentModModifierType`**：`Flat` / `PercentAdd` / `PercentMore`，默认 `Flat`
- `BagItemData` 新增 `EquipmentMods : List<RolledMod>`，承载运行时词条数据
- `BagItemData.ToItemData()` 根据 `EquipmentMods` 重新构建 `ItemData.Prefixes` / `ItemData.Suffixes` 中的 `StatModifier`，使装备上身后 `StatsSystem` 能正确聚合到 `StatsComponent`
- `ShopPanel.PopulateEquipmentBagData(...)` 把 `GeneratedEquipment.Mods` / `Sockets` / 前后缀描述 / 可装备槽位一并填入 `BagItemData`，解决了此前"商店装备进背包后词条丢失"的问题
- `CharactorMassagePanelController` 的每次刷新会：
  1. 先按 `StatsComponent` 聚合值渲染当前页签下的属性
  2. 再遍历所有已装备装备的 `RolledMod`，按配置表的 `DisplayTab` 显式追加词条条目
- 若装备词条的 `StatType` 留空（如命中值），该词条不会影响角色属性，但仍能按 `DisplayTab` 显示到对应页签下
- **演示装备额外注意**：`BagPanel.PopulateDemoItems()` 手工创建的演示装备不走 `EquipmentMods` 生成链路，必须同时写入真实 `RuntimeItemData.Prefixes` / `Suffixes`
  - 仅填写 `PrefixDescriptions` / `SuffixDescriptions` 只会影响 Tips 文本，不会影响 `StatsSystem`
  - `BagItemData.RebuildItemModifiersFromEquipmentMods(...)` 已修复为：当 `EquipmentMods` 为空时保留现有前后缀，不再误清空演示装备的真实属性

### 维护时的高风险点

#### 1. `BagItemView` 是背包系统的“交通枢纽”

很多问题看似出在背包格子、装备槽、Tips，实际上根因在 `BagItemView`：

- 当前容器记录错
- 拿起 / 放下时机错
- 父节点切换错
- 视觉尺寸恢复错

#### 2. Tips 位置与内容不是一个文件能解决的

如果你发现 Tips 有问题，不要只看 `EquipmentTips.cs`，通常至少要一起看：

- `EquipmentItem.cs`
- `EquipmentTips.cs`
- `BagItemData.cs`
- `EquipmentGenerator.cs`
- `UIManager.cs`（尤其是 Tooltip Overlay 与状态栏排序）

#### 3. 商店与背包的数据模型不完全等价

- 商店常有完整 `GeneratedEquipment`
- 背包常使用 `BagItemData`

任何关于词缀、品质色、插槽、名称的展示改动，都要确认这两条路径是否都覆盖到了。

#### 4. 角色底栏展示链与技能释放链不是一回事

当前底栏技能区主要显示的是“已插入的主动技能宝石”，而不是直接映射 `SkillComponent.SkillSlots`。

这意味着：

- 底栏显示正常，不代表技能逻辑一定可用
- 技能逻辑正常，也不代表底栏一定会自动更新

#### 5. 测试技能不等于完整技能

当前典型例子：`旋风斩` 已经被分配进测试技能槽，但 `SkillSystem` 仍未处理 `Channeling`。

#### 6. 宝石连接线不等于真实拓扑数据

现在的连线只是 UI 层按布局推导的结果。若后续要做真正的 POE 孔位连接拓扑，需要先扩展 `SocketData`。

#### 7. 底栏稳定槽位依赖 `BagItemData` 引用

- `CharactorMainPanelController` 当前用 `BagItemData` 引用判断“是不是同一颗宝石”
- 如果后续 `BagPanel.GetSocketedActiveGems(...)` 改成重新 new / clone 数据对象，槽位稳定性会失效，需要引入更稳定的实例标识

### 推荐接手顺序

#### 想快速理解全局

建议阅读顺序：

1. [GameManager.cs](Assets/Scripts/Managers/GameManager.cs)
2. [UIManager.cs](Assets/Scripts/Managers/UIManager.cs)
3. [GameSceneInitializer.cs](Assets/Scripts/Game/GameSceneInitializer.cs)
4. [GameSceneManager.cs](Assets/Scripts/Game/GameSceneManager.cs)
5. [World.cs](Assets/Scripts/ECS/Core/World.cs)

#### 想改背包 / 装备

建议阅读顺序：

1. [BagPanel.cs](Assets/Scripts/Game/UI/BagPanel.cs)
2. [BagBox.cs](Assets/Scripts/Game/UI/BagBox.cs)
3. [BagItemView.cs](Assets/Scripts/Game/UI/BagItemView.cs)
4. [EquipmentSlotView.cs](Assets/Scripts/Game/UI/EquipmentSlotView.cs)
5. [SocketItem.cs](Assets/Scripts/Game/UI/SocketItem.cs)
6. [EquipmentItem.cs](Assets/Scripts/Game/UI/EquipmentItem.cs)
7. [EquipmentTips.cs](Assets/Scripts/Game/UI/EquipmentTips.cs)
8. [CharactorMainPanelController.cs](Assets/Scripts/Game/UI/CharactorMainPanelController.cs)

#### 想改装备生成 / 商店

建议阅读顺序：

1. [EquipmentConfigLoader.cs](Assets/Scripts/Game/Equipment/EquipmentConfigLoader.cs)
2. [EquipmentGenerator.cs](Assets/Scripts/Game/Equipment/EquipmentGenerator.cs)
3. [ShopPanel.cs](Assets/Scripts/Game/UI/ShopPanel.cs)
4. [BagItemData.cs](Assets/Scripts/Game/UI/BagItemData.cs)

#### 想改技能 / 支持宝石 / 技能底栏

建议阅读顺序：

1. [GameSceneManager.cs](Assets/Scripts/Game/GameSceneManager.cs)
2. [SkillSystem.cs](Assets/Scripts/ECS/Systems/SkillSystem.cs)
3. [SkillComponent.cs](Assets/Scripts/ECS/Components/SkillComponent.cs)
4. [SkillFactory.cs](Assets/Scripts/Game/Skills/SkillFactory.cs)
5. [GameSceneInitializer.cs](Assets/Scripts/Game/GameSceneInitializer.cs)
6. [CharactorMainPanelController.cs](Assets/Scripts/Game/UI/CharactorMainPanelController.cs)

#### 想改 NPC / 对话 / 商店入口

建议阅读顺序：

1. [NpcButtonEventType.cs](Assets/Scripts/Game/NpcButtonEventType.cs)
2. [NpcConfigLoader.cs](Assets/Scripts/Game/NpcConfigLoader.cs)
3. [NpcDialogPanel.cs](Assets/Scripts/Game/UI/NpcDialogPanel.cs)
4. [GameSceneManager.cs](Assets/Scripts/Game/GameSceneManager.cs)

### 结论

如果只能记住三句话，请记住：

1. **项目主干是 `GameManager + World + Systems + GameSceneManager + UIManager`。**
2. **背包主干是 `BagItemView`，Tips 主干是 `EquipmentItem + EquipmentTips`，角色底栏主干是 `CharactorMainPanelController`。**
3. **技能释放链与底栏技能显示链要分开理解；装备展示也要同时注意 `GeneratedEquipment` 与 `BagItemData` 两条数据路径。**

- **角色主面板血蓝遮罩**：`CharactorMainPanelController` 当前会绑定 `CharactorMainPanel` 里的 `Hp/Mask` 与 `Mp/Mask`，从玩家 `HealthComponent` 的 `HealthPercent / ManaPercent` 驱动 `Image.fillAmount`
- **角色主面板血蓝实时刷新**：当前不仅在 `UIManager.RefreshCharactorMainPanel()` 时更新一次血蓝，还会在面板激活后订阅 `HealthComponent.OnHealthChanged / OnManaChanged`，因此技能耗蓝、受伤、回血时遮罩会实时变更
- **角色主面板血蓝显示方式**：当前 `Hp/Mask` 与 `Mp/Mask` 使用 `Image.Type.Filled + Vertical + Bottom Origin` 作为百分比显示实现；若视觉方向需要调整，优先修改 [CharactorMainPanelController.cs](D:/Learning/POELike/Assets/Scripts/Game/UI/CharactorMainPanelController.cs) 的 `ApplyMaskFill()`，不要先改技能或战斗逻辑
- **技能默认键位约定**：当前 `skill1~8` 默认映射为 `LMB / MMB / RMB / Q / W / E / R / T`，代码实现位于 [PlayerController.cs](D:/Learning/POELike/Assets/Scripts/Game/Character/PlayerController.cs) 与 [GameSceneManager.cs](D:/Learning/POELike/Assets/Scripts/Game/GameSceneManager.cs)
- **技能栏默认标签同步**：`CharactorMainPanelController.ResolveDefaultSkillKey()` 当前必须与实际输入映射保持一致，现底栏显示为 `LMB / MMB / RMB / Q / W / E / R / T`
- **左键分流约定**：当前 [GameSceneManager.cs](D:/Learning/POELike/Assets/Scripts/Game/GameSceneManager.cs) 会把一次左键按下锁定为 `Skill1 / Move / Blocked` 三种意图之一，整次按住过程不会在中途切换路径
- **左键触发 skill1 的条件**：仅当 `skill1` 槽位有技能、未冷却、蓝量足够、未被其它施法状态阻塞，且鼠标附近吸附到存活怪物、目标距离在由 `SkillData.Range / AreaRadius` 推导出的有效范围内时，当前左键交互才视为 `skill1`
- **左键移动回退**：若任一技能条件不满足，则当前左键交互回退为普通地面寻路；点击 UI、关闭面板、点击 NPC 名称标签时会进入 `Blocked`，直到松开前都不会误触技能或移动
- **左键分流实现入口**：当前鼠标左键 `skill1 / 移动 / 阻断` 判定集中在 [GameSceneManager.cs](D:/Learning/POELike/Assets/Scripts/Game/GameSceneManager.cs) 的 `ResolveLeftMouseIntent()`、`TryBeginLeftClickSkill()`、`FindMonsterUnderCursor()`、`ResolveLeftClickSkillRange()`；后续若要改 POE 左键体验，优先改这里
- **左键选怪方式**：当前不是 Physics 射线命中怪物，而是根据鼠标落点在 ECS 怪物列表中吸附最近存活怪物；因此命中手感主要受 `LeftClickMonsterSnapPadding`、`MonsterSpawner.CollisionRadius` 和技能有效距离推导影响
- **技能栏冷却 Mask 约定**：当前 [CharactorMainPanelController.cs](D:/Learning/POELike/Assets/Scripts/Game/UI/CharactorMainPanelController.cs) 会读取 `SkillSlotArr` 每个技能槽子节点 `Mask` 的 `Image`，并按 `SkillSlot.CooldownTimer / SkillData.Cooldown` 驱动冷却遮罩显示
- **技能栏冷却方向**：当前技能冷却遮罩使用 `Image.Type.Filled + Radial360 + Top Origin + fillClockwise=true`，视觉效果为从顶部开始按**顺时针**方向收缩；若后续视觉要改方向，优先修改 `ApplySkillCooldownMask()`
- **接手文档已模块化拆分**：根目录的 [POELike_接手Skill.md](POELike_接手Skill.md) 现在只保留为**总索引入口**，详细内容已拆到 `POELike_接手Skill/Skill_01 ~ Skill_07` 多个子文档中
- **后续推荐读取方式**：先读 [POELike_接手Skill.md](POELike_接手Skill.md) 判断问题归属，再按需定向读取对应子模块，而不是一次性读取整份长文
- **当前子 skill 模块划分**：`skill-01=全局入口与阅读顺序`、`skill-02=ECS与运行时主链`、`skill-03=背包/装备/宝石交互`、`skill-04=UI/Tips与角色面板`、`skill-05=技能系统/技能栏与快捷键`、`skill-06=装备生成/商店/NPC与配置工具链`、`skill-07=SOP/排错与高风险点`
- **怪物死亡地面掉落名称链**：当前真实实现不是 `EnemyController` 旧桥接链，而是 [CombatSystem.cs](Assets/Scripts/ECS/Systems/CombatSystem.cs) 发布 `EntityDiedEvent`，由 [GameSceneManager.cs](Assets/Scripts/Game/GameSceneManager.cs) 过滤 `Monster` 死亡并按概率创建 `ItemData`，随后发布 `GroundItemDroppedEvent`

- **地面掉落名称渲染入口**：新增 [GroundItemLabelRenderer.cs](Assets/Scripts/Game/GroundItemLabelRenderer.cs) 挂在主摄像机上，由 [CameraController.cs](Assets/Scripts/Game/CameraController.cs) 自动添加并注入 `World`；负责把掉落装备名称绘制在怪物死亡位置
- **地面掉落名称悬停规则**：当前标签默认带深色背景，鼠标移入时会按名称实际宽高整块高亮背景与文字，而不是只改文字色；多个同点掉落标签会向上错层排列
- **地面掉落点击拾取规则**：当前 [GroundItemLabelRenderer.cs](Assets/Scripts/Game/GroundItemLabelRenderer.cs) 会先把掉落转换成 `BagItemData` 并调用 [BagPanel.cs](Assets/Scripts/Game/UI/BagPanel.cs) 的 `CanAddItemToBag(...)` 做背包空间检测；放不下时固定提示“背包放不下了”，放得下时才真正 `TryAddItemToBag(...)` 并移除地面标签
- **地面掉落点击消费规则**：当前 [GameSceneManager.cs](Assets/Scripts/Game/GameSceneManager.cs) 左键意图分流已把 `GroundItemLabelRenderer.ClickConsumedThisFrame` 纳入阻断条件，点击掉落名称后不会再继续触发地面移动或左键技能
- **`StatModifier` 类型约束**：当前 [StatTypes.cs](Assets/Scripts/ECS/Components/StatTypes.cs) 中的 `StatModifier` 是 `struct` 而不是 `class`，因此像 [GroundItemLabelRenderer.cs](Assets/Scripts/Game/GroundItemLabelRenderer.cs) 这类构造词条描述的代码不能写 `modifier == null` 判空，否则会触发 `CS0019`
