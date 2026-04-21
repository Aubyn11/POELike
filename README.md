## POELike

> **Unity 6 + 自定义 ECS + Excel 配置驱动** 的类 Path of Exile 风格 ARPG 原型项目。  
> 这个仓库聚焦于 **战斗、掉落、装备构筑、NPC 交互、任务地图切换** 的整体玩法闭环实现。

`Unity 6000.4.0a4` `URP` `Input System` `Custom ECS` `Excel -> PB`

### 这是什么

`POELike` 是一个偏系统验证和玩法原型方向的 ARPG 项目。

它当前已经串起了一条可运行的主流程：

- **角色选择**：从 `CharacterSelectScene` 进入主流程
- **城镇阶段**：进行 NPC 交互、商店购买、背包整理、装备调整
- **任务地图**：通过门或地图入口进入 `MissionScene`
- **战斗与掉落**：怪物战斗、地面掉落、拾取入包、角色成长
- **配置驱动**：地图、NPC、装备、怪物、技能石等内容由 Excel 配置导出驱动

如果你想看的是一个“能跑起来、能形成玩法回路、且代码结构清晰”的类 POE 原型，这个项目就是在做这件事。

### 项目亮点

- **完整的 ARPG 原型闭环**：城镇、任务地图、战斗、掉落、背包、装备、技能已经串联起来
- **自定义 ECS 主链**：不是基于 Unity DOTS，而是项目自建的 `World + Systems` 运行时结构
- **技能栏与药剂键位就位**：支持 8 槽技能栏与 `1~5` 药剂热键
- **装备与构筑表达更完整**：背包、装备栏、孔位、连结、技能石、通货交互已接入主要链路
- **配置驱动内容生产**：Excel -> `.pb` -> Loader -> Gameplay / UI，适合快速迭代数值与内容
- **接手成本较低**：仓库内已经补齐模块化接手文档，适合继续开发或做作品展示

### 一眼看懂玩法闭环

```mermaid
flowchart LR
    A[CharacterSelectScene] --> B[LoadingScene]
    B --> C[GameScene / 城镇]
    C --> D[NPC 对话 / 商店 / 背包整理]
    D --> E[DoorPanel / 地图入口]
    E --> F[LoadingScene]
    F --> G[MissionScene / 任务地图]
    G --> H[战斗 / 释放技能 / 怪物掉落]
    H --> I[拾取 / 装备 / 继续构筑]
```

### 现在能看到什么

| 模块 | 当前可展示内容 |
| --- | --- |
| **战斗系统** | 技能释放、移动、目标交互、怪物战斗、伤害流程 |
| **技能系统** | 8 槽技能栏、快捷键、冷却、技能工厂、支持宝石接入链路 |
| **掉落与拾取** | 地面掉落、点击名称寻路靠近、自动拾取进背包 |
| **背包与装备** | 背包格子、装备栏、拖拽交互、Tips、通货堆叠与拆分 |
| **装备构筑** | 装备孔位、连结、技能石与支持系统、属性生效主链 |
| **城镇交互** | NPC 对话、商店购买、地图门/地图面板入口 |
| **地图系统** | `LoadingScene -> GameScene / MissionScene` 切换，任务地图由配置生成 |
| **配置工具链** | Excel 源文件、schema 描述、`.pb` 导出、运行时 Loader 消费 |

### 快速体验

#### 1. 拉取子模块

仓库包含 `Tools/excelConvert` 子模块。首次拉取后建议先执行：

```bash
git submodule update --init --recursive
```

#### 2. 用 Unity Hub 打开项目

- **Unity 版本**：`6000.4.0a4`
- **推荐平台**：Windows

#### 3. 从角色选择场景启动

推荐直接打开：

- `Assets/Scenes/CharacterSelectScene.unity`

然后点击 Play，进入角色选择并开始主流程。

### 当前主要场景

- **`CharacterSelectScene`**：角色选择入口
- **`LoadingScene`**：统一加载过渡场景
- **`GameScene`**：默认主玩法/城镇场景
- **`MissionScene`**：任务地图场景，主要承载战斗与掉落内容
- **`SampleScene`**：示例场景，不属于当前核心流程

### 操作说明

| 按键 | 行为 |
| --- | --- |
| **`LMB`** | 左键意图分流，当前会落到 `Skill1 / Move / Blocked` 之一 |
| **`MMB`** | `Skill2` |
| **`RMB`** | `Skill3` |
| **`Q / W / E / R / T`** | `Skill4 ~ Skill8` |
| **`1 / 2 / 3 / 4 / 5`** | `Flask1 ~ Flask5` |
| **点击 NPC / 地面掉落名称** | 角色先自动走近，再执行交互或拾取 |

### 技术侧卖点

#### 自定义 ECS 运行时主链

项目的核心不是单一 UI 或单个玩法脚本，而是一条完整的运行时主链：

- `GameManager`
- `World`
- `ECS Systems`
- `GameSceneManager`
- `UIManager`
- `SceneLoader`

这意味着：

- **玩法逻辑集中在系统层推进**
- **场景切换与运行时管理职责明确**
- **适合继续扩展角色、怪物、技能、地图与经济系统**

#### 配置驱动内容生产

```mermaid
flowchart LR
    A[common/excel/xls/*.xlsx] --> B[启动ExcelConvert.bat]
    B --> C[Assets/Cfg/*.pb]
    C --> D[ConfigLoader]
    D --> E[Generator / Runtime]
    E --> F[UI / Gameplay]
```

当前仓库包含：

- **Excel 源文件**：`common/excel/xls/`
- **字段/schema 描述**：`common/cfg/`
- **导出产物**：`Assets/Cfg/`
- **导表工具链**：`Tools/excelConvert/`、`Tools/GenEquipmentExcel/`

这套链路很适合原型项目快速验证：改表 -> 导出 -> 进游戏看表现。

### 项目结构

```text
POELike/
├─ Assets/
│  ├─ Cfg/                     运行时导出的 .pb 配置
│  ├─ Prefabs/                 预制体资源
│  ├─ Resources/               Resources 资源
│  ├─ Scenes/                  游戏场景
│  └─ Scripts/
│     ├─ ECS/                  自定义 ECS：Components / Core / Systems
│     ├─ Game/                 玩法层：角色、技能、装备、地图、UI 等
│     ├─ Managers/             全局管理器：GameManager / SceneLoader / UIManager
│     └─ Editor/               编辑器工具与构建脚本
├─ common/
│  ├─ cfg/                     Excel schema / 字段描述文件
│  └─ excel/xls/               Excel 配置源文件
├─ Tools/
│  ├─ excelConvert/            导表工具子模块
│  └─ GenEquipmentExcel/       equipment.xlsx 刷新工具
├─ POELike_接手Skill/          接手拆分文档
├─ POELike_接手Skill.md        接手总索引
└─ 启动ExcelConvert.bat        导表入口
```

### 核心代码入口

如果你不只是想看效果，还想继续读代码，推荐先从下面这些入口开始：

#### 运行时主链

- [`GameManager.cs`](Assets/Scripts/Managers/GameManager.cs)：创建 `World`、注册系统、维持关键运行时管理器
- [`SceneLoader.cs`](Assets/Scripts/Managers/SceneLoader.cs)：统一处理 `LoadingScene -> GameScene / MissionScene` 的异步切换
- [`LoadingSceneController.cs`](Assets/Scripts/Managers/LoadingSceneController.cs)：加载界面与进度展示
- [`UIManager.cs`](Assets/Scripts/Managers/UIManager.cs)：面板生命周期、层级和 Tooltip Overlay 管理
- [`World.cs`](Assets/Scripts/ECS/Core/World.cs)：ECS 容器、实体/组件/查询/系统执行核心
- [`GameSceneManager.cs`](Assets/Scripts/Game/GameSceneManager.cs)：玩家输入、交互、拾取、技能输入、地图切换等运行时总控入口
- [`GameSceneInitializer.cs`](Assets/Scripts/Game/GameSceneInitializer.cs)：进入场景后的初始化器，当前会预热部分资源并分配测试内容

#### 玩法核心

- [`SkillSystem.cs`](Assets/Scripts/ECS/Systems/SkillSystem.cs)：技能执行入口
- [`StatsSystem.cs`](Assets/Scripts/ECS/Systems/StatsSystem.cs)：属性聚合与装备属性生效入口
- [`MovementSystem.cs`](Assets/Scripts/ECS/Systems/MovementSystem.cs)：移动、寻路与施法期移动控制
- [`CombatSystem.cs`](Assets/Scripts/ECS/Systems/CombatSystem.cs)：战斗与伤害流程
- [`SkillFactory.cs`](Assets/Scripts/Game/Skills/SkillFactory.cs)：默认技能、技能石映射和测试技能构造入口
- [`EquipmentGenerator.cs`](Assets/Scripts/Game/Equipment/EquipmentGenerator.cs)：装备生成核心
- [`BagPanel.cs`](Assets/Scripts/Game/UI/BagPanel.cs)：背包聚合入口
- [`EquipmentTips.cs`](Assets/Scripts/Game/UI/EquipmentTips.cs)：装备/通货 Tips 入口
- [`ShopPanel.cs`](Assets/Scripts/Game/UI/ShopPanel.cs)：商店主入口
- [`NpcDialogPanel.cs`](Assets/Scripts/Game/UI/NpcDialogPanel.cs)：NPC 对话面板入口
- [`DoorPanel.cs`](Assets/Scripts/Game/UI/DoorPanel.cs)：任务地图传送门面板入口

### 配置与导表

#### 当前配置源文件示例

- `button.xlsx`
- `currency.xlsx`
- `equipment.xlsx`
- `map.xlsx`
- `monster.xlsx`
- `npc.xlsx`
- `skillStone.xlsx`

#### 导表入口

直接运行根目录的：

- [`启动ExcelConvert.bat`](启动ExcelConvert.bat)

它当前支持：

- **一键批量导出所有 Excel**
- **打开 ExcelConvert 图形界面**
- **必要时自动切换到降级导表链**

> 建议不要手改 `Assets/Cfg/*.pb`，而是优先修改 `common/excel/xls/` 与 `common/cfg/` 后重新导出。

### 项目当前状态

- **这是一个原型工程，不是完整商业项目**
- **运行链路、玩法系统和配置管线已经具备较强展示价值**
- **`GameSceneInitializer` 当前会自动分配测试装备、测试技能，并预热部分资源**
- **如果文档与当前代码冲突，请以代码为准**

### 更多资料

如果你是接手开发、二次扩展或深入理解结构，建议继续阅读：

- [`POELike_接手Skill.md`](POELike_接手Skill.md)
- [`Skill_01_全局入口与阅读顺序.md`](POELike_接手Skill/Skill_01_全局入口与阅读顺序.md)
- [`Skill_02_ECS与运行时主链.md`](POELike_接手Skill/Skill_02_ECS与运行时主链.md)
- [`Skill_03_背包_装备_宝石交互.md`](POELike_接手Skill/Skill_03_背包_装备_宝石交互.md)
- [`Skill_04_UI_Tips与角色面板.md`](POELike_接手Skill/Skill_04_UI_Tips与角色面板.md)
- [`Skill_05_技能系统_技能栏与快捷键.md`](POELike_接手Skill/Skill_05_技能系统_技能栏与快捷键.md)
- [`Skill_06_装备生成_商店_NPC与配置工具链.md`](POELike_接手Skill/Skill_06_装备生成_商店_NPC与配置工具链.md)
- [`Skill_07_SOP_排错与高风险点.md`](POELike_接手Skill/Skill_07_SOP_排错与高风险点.md)

### 一句话概括

这是一个 **已经具备可玩主循环、具备构筑表达、并且结构清晰易继续扩展** 的 Unity ARPG 原型项目；如果你希望展示一个“玩法系统 + 技术架构 + 配置工具链”三者同时在线的作品，它已经很接近一个合格的展示样本。