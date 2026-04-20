## skill-06 装备生成 / 商店 / NPC 与配置工具链

> 目标：**把“配置 -> Loader -> Generator -> UI 展示”与 NPC / 商店数据链拆清楚。**
> 
> 如果问题属于“配置改了为什么没生效”“商店正常但进背包异常”“NPC 按钮行为要改”，优先读本模块。

### 装备生成主链

#### [EquipmentConfigLoader.cs](../Assets/Scripts/Game/Equipment/EquipmentConfigLoader.cs)

- 装备配置读取入口
- 新增装备基底 / 词缀字段时，通常第一站在这里

#### [EquipmentGenerator.cs](../Assets/Scripts/Game/Equipment/EquipmentGenerator.cs)

- 运行时装备生成核心
- 常见职责包括：
  - 生成 `GeneratedEquipment`
  - 前后缀词条
  - 孔位与颜色
  - 稀有度 / 名称 / 展示信息
  - 当前 `SocketData.LinkedToPrevious`

#### [EquipmentBagDataFactory.cs](../Assets/Scripts/Game/Equipment/EquipmentBagDataFactory.cs)

- 共享 `GeneratedEquipment -> BagItemData` 转换层
- 商店、GM、后续掉落都应优先复用这层，不要各自重复拼字段

### 商店链路

#### [ShopPanel.cs](../Assets/Scripts/Game/UI/ShopPanel.cs)

- 商店 UI 主入口
- 是观察装备展示、购买后进背包映射是否完整的关键位置
- 如果“商店显示正常，但进背包后内容丢了”，要优先检查这里到 `BagItemData` 的转换链

### NPC 链路

#### [NpcButtonEventType.cs](../Assets/Scripts/Game/NpcButtonEventType.cs)

- NPC 对话按钮事件枚举
- 改按钮行为前先看这里现有事件是否够用

#### [NpcConfigLoader.cs](../Assets/Scripts/Game/NpcConfigLoader.cs)

- NPC / 对话 / 按钮配置读取入口

#### [MapLevelConfigLoader.cs](../Assets/Scripts/Game/MapLevelConfigLoader.cs)

- 传送门地图关卡配置读取入口
- 当前读取 `MapLevelConf.pb`

#### [MapLayoutConfigLoader.cs](../Assets/Scripts/Game/MapLayoutConfigLoader.cs)

- 地图布局配置读取入口
- 当前读取 `MapLayoutConf.pb`
- 按 `CfgID` 返回玩家出生偏移与 NPC 组合 / 布局
- 当前 A1 测试数据：`CfgID=1001` 会刷 `NPCID=1001 + 1002`，`CfgID=1002` 会刷 `NPCID=1001 + 1003`

#### [MapDecorationConfigLoader.cs](../Assets/Scripts/Game/MapDecorationConfigLoader.cs)

- 地图装饰配置读取入口
- 当前读取 `MapDecorationConf.pb`
- 按 `CfgID` 返回地图装饰布局
- 当前 A2 测试数据：`CfgID=1001` 会刷柱子 + 祭坛布局，`CfgID=1002` 会刷箱体 + 标记物布局

#### [MapContentConfigLoader.cs](../Assets/Scripts/Game/MapContentConfigLoader.cs)

- 地图内容配置读取入口
- 当前读取 `MapContentConf.pb`
- 按 `CfgID` 返回当前地图应刷新的怪物组

#### [DoorPanel.cs](../Assets/Scripts/Game/UI/DoorPanel.cs)

- 传送门面板入口
- 打开时按 `MapLevelConf.pb` 条目数量动态创建地图按钮
- 每个条目的文本当前显示 `MapName`
- 点击地图按钮后当前会把目标 `MapLevelData` 交给 `SceneLoader`，切入 `MissionScene`；`MissionScene` 启动后由 `GameSceneManager` 按对应 `CfgID` 构建玩家出生布局、地图装饰布局、NPC 组合、NPC 布局与地图内容
- 当前切图会复用已有玩家 ECS 实体，因此角色状态、装备与技能不会因进入 `MissionScene` 而被重置
- 如某张地图在 `MapLayoutConf.pb` 中没有保留可打开 `DoorPanel` 的 NPC，`GameSceneManager` 当前会输出明确警告；当前测试数据里的入口 NPC 是 `NPCID=1001`

#### [NpcDialogPanel.cs](../Assets/Scripts/Game/UI/NpcDialogPanel.cs)

- NPC 对话面板
- 配置如何落到 UI 按钮与点击行为，通常要在这里核对

#### [GameSceneManager.cs](../Assets/Scripts/Game/GameSceneManager.cs)

- NPC 交互运行时入口
- 包含 NPC 点击、接近、打开对话等串联逻辑

### 配置工具链

```mermaid
flowchart LR
    A[Excel / 原始配置] --> B[转换工具]
    B --> C[运行时配置文件]
    C --> D[ConfigLoader 读取]
    D --> E[Generator / Gameplay 消费]
    E --> F[UI 展示]
```

#### 关键文件

- [Program.cs](../Tools/GenEquipmentExcel/Program.cs)
- [export_excel_to_cfg.py](../Tools/excelConvert/export_excel_to_cfg.py)
- [启动ExcelConvert.bat](../启动ExcelConvert.bat)
  - 当前会优先启动内部 `ExcelExporter / excelConvert` 发布产物；若仓库里未提供这些内部工具，则自动走仓库内降级链：先刷新 `equipment.xlsx`，再把 `common/excel/xls/*.xlsx` 全量导出到 `Assets/Cfg/*.pb`
- 各类 `ConfigLoader`

### 当前已知数据链约定

#### 装备词条 -> 角色属性链

当前这条链已经打通：

- `EquipmentModConf` 提供：
  - `EquipmentModDisplayTab`
  - `EquipmentModStatType`
  - `EquipmentModModifierType`
- `BagItemData.EquipmentMods` 承载运行时词条
- `BagItemData.ToItemData()` 把词条转成 `StatModifier`
- 装备上身后由 `StatsSystem` 聚合进 `StatsComponent`
- 角色面板再从 `StatsComponent` 与装备 `RolledMod` 双路展示

#### 商店 -> 背包映射

当前需要确保以下字段被正确带入：

- `EquipmentMods`
- `Sockets`
- 前后缀描述
- 可装备槽位信息

如果“商店里看到是对的，买下来不对”，通常是中间映射漏字段

#### GM / 调试链路

- [GMPanel.cs](../Assets/Scripts/Game/UI/GMPanel.cs) 当前支持生成装备和宝石到背包
- [GMItemFactory.cs](../Assets/Scripts/Game/Items/GMItemFactory.cs) 负责把 GM 输入转成 `BagItemData`
- 当前孔位连结输入也会落到 `SocketData.LinkedToPrevious`

### 当前需要特别注意的现实

#### 配置改了但游戏没变，常见原因不是 UI

优先依次排查：

1. 是否重新执行了 Excel / 配置转换
2. 生成产物是否更新
3. 对应 `ConfigLoader` 是否读取了新字段
4. `Generator` / `Factory` 是否消费了该字段
5. UI 是否展示该字段

#### 怪物配置字段语义要特别注意

- 当前 [MonsterSpawner.cs](../Assets/Scripts/Game/MonsterSpawner.cs) **不能再把 `MonstDataConf.pb` 里的 `MonsterRadius` 直接当成 `AttackRange`**
- 目前 `MonsterSpawner` 会读取显式 `MonsterAttackRange`；若配置里没有该字段，则回退默认近战攻击距离 `1.5f`
- 当前怪物攻击节奏还新增了 `MonsterAttackDuration` 与 `MonsterAttackInterval`：两者都会由 `MonsterSpawner` 读入并写到 `AIComponent`，用于控制怪物进入攻击态后要在原地锁多久
- 当前真实运行时语义是：怪物进入攻击态后，会先完整走完 `MonsterAttackDuration + MonsterAttackInterval`，在这两个时间都结束前不会恢复追踪；结束后才按玩家当前位置决定继续追击还是进入下一轮攻击
- 如果再次把 `MonsterRadius` 误接到 `AttackRange`，怪物会在离玩家过远时就进入围攻停位 / 攻击圈，典型现象就是围在角色上、右、下等固定方位持续抖动

#### 商店数据和背包数据不是一回事

- 商店更常持有完整 `GeneratedEquipment`
- 背包 / 装备 UI 更常使用 `BagItemData`
- 所以装备展示改动通常要验证两条路径，而不是只看一种来源

### 常见排查入口

#### 问题：配置改了但没生效

优先看：

- [Program.cs](../Tools/GenEquipmentExcel/Program.cs)
- [启动ExcelConvert.bat](../启动ExcelConvert.bat)
- 对应 `ConfigLoader`
- 对应 `Generator` / `Factory`

#### 问题：商店显示正常，进背包后不正常

优先看：

- [ShopPanel.cs](../Assets/Scripts/Game/UI/ShopPanel.cs)
- [EquipmentBagDataFactory.cs](../Assets/Scripts/Game/Equipment/EquipmentBagDataFactory.cs)
- [BagItemData.cs](../Assets/Scripts/Game/UI/BagItemData.cs)

#### 问题：NPC 按钮行为不对

优先看：

- [NpcButtonEventType.cs](../Assets/Scripts/Game/NpcButtonEventType.cs)
- [NpcConfigLoader.cs](../Assets/Scripts/Game/NpcConfigLoader.cs)
- [NpcDialogPanel.cs](../Assets/Scripts/Game/UI/NpcDialogPanel.cs)
- [GameSceneManager.cs](../Assets/Scripts/Game/GameSceneManager.cs)

### 从本模块跳到哪里

- **要看全局地图和阅读顺序**：转 [skill-01](./Skill_01_全局入口与阅读顺序.md)
- **要看背包 / 装备 / 宝石交互**：转 [skill-03](./Skill_03_背包_装备_宝石交互.md)
- **要看 SOP / 排错与高风险点**：转 [skill-07](./Skill_07_SOP_排错与高风险点.md)
