## skill-03 背包 / 装备 / 宝石交互

> 目标：**把拿起、放下、替换、装备、镶嵌、连结这条链拆清楚。**
> 
> 如果问题属于“道具为什么这样移动 / 为什么不能放 / 为什么替换不对 / 为什么连结显示不对”，优先读本模块。

### 当前交互模型

当前背包系统已经不是拖拽，而是 **点击拿起 / 点击放下**：

1. **第一次点击物品**：拿起物品
2. **物品进入跟随鼠标状态**
3. **再次点击目标位置**：放下物品
4. 目标位置可以是：
   - 背包格子
   - 装备栏槽位
   - 宝石孔

### 当前职责分工

#### [BagItemView.cs](../Assets/Scripts/Game/UI/BagItemView.cs)

- **背包移动状态机核心**
- 负责：
  - 当前正在拖动 / 搬运的物品
  - 父节点切换
  - 跟手显示
  - 放下成功 / 回退原位
  - 被替换物接棒跟手
- 如果问题看起来涉及多个容器，但现象都围绕“拿起 / 放下 / 跟手 / 回位”，先看这里

#### [BagBox.cs](../Assets/Scripts/Game/UI/BagBox.cs)

- **背包网格放置规则拥有者**
- 负责：
  - 占格判断
  - 可放置 / 可替换判断
  - 目标区域占用识别
- 背包内“点击被占用物时是否允许整体替换”，要回到这里确认

#### [EquipmentSlotView.cs](../Assets/Scripts/Game/UI/EquipmentSlotView.cs)

- **装备槽 / 药剂槽接纳逻辑**
- 当前支持：
  - 类型校验后直接替换
  - 被替换物立即进入跟手状态
  - 装备变化后刷新底栏 / 属性面板

#### [SocketItem.cs](../Assets/Scripts/Game/UI/SocketItem.cs)

- **宝石孔接纳逻辑**
- 当前支持：
  - 颜色 / 类型校验
  - 已占位时直接替换
  - 被替换宝石立即进入跟手状态

#### [BagPanel.cs](../Assets/Scripts/Game/UI/BagPanel.cs)

- 更偏**面板控制器 / 聚合入口**
- 负责：
  - 背包整体初始化
  - 递归查找药剂槽
  - 获取已装备物品 / 已插宝石的聚合结果
  - 向角色底栏等 UI 暴露读取入口
  - 地面掉落 / GM 生成物入包前的空间检测与实际落袋
- 注意：它不是背包移动规则的真正核心

### 一个关键桥接对象：`BagItemData`

看 [BagItemData.cs](../Assets/Scripts/Game/UI/BagItemData.cs)：

- 承接 UI 侧物品信息
- 典型内容包括：
  - 名称
  - 图标
  - 占格大小
  - 网格坐标
  - 可装备槽位
  - 插槽信息
  - 前后缀展示文案
  - 运行时装备词条 `EquipmentMods`

### 当前替换规则

#### 装备槽 / 药剂槽

- 当目标槽位已有内容时，只要除了“已占位”之外的接纳条件满足，就会 **直接替换**
- 被替换物会 **立即进入跟手状态**
- 需要特别留意 `RuntimeItemData` 保真，尤其是药剂充能等运行时状态

#### 宝石孔

- 当目标孔位已有宝石时，只要颜色 / 类型等条件满足，就会 **直接替换**
- 被替换宝石会 **立即进入跟手状态**

#### 背包格子

- 目标区域被占用时，只要整个目标区域只被 **同一个道具** 占用，也允许整件替换
- 点击被占用物时，落点优先按 **鼠标当前所在格子** 判断

### 当前已确认的行为约定

- **拿起瞬间会立即贴到鼠标位置**
- **后续跟随鼠标与预览格，会走短时平滑阻尼**
- **从装备栏卸下装备时，会恢复背包原始占格大小**
- **`BagPanel.EnsureInitialized()` 当前带 `_isInitializing` 防重入保护**
- **装备栏 / 宝石孔 / 药剂槽目标已占位时，只要其他条件满足，就直接替换**

### 宝石孔与连结约定

#### 当前真实状态

当前连结规则不是纯“相邻索引自动连”，而是：

- **只支持相邻索引之间存在连结关系**
- **是否真正连结由 `SocketData.LinkedToPrevious` 控制**

也就是说：

- 结构上仍只支持 `index-1 / index+1` 这种相邻链
- 但是否连上，已经不再是 UI 自己默认推断，而是读取数据字段

#### 核心文件

- [EquipmentGenerator.cs](../Assets/Scripts/Game/Equipment/EquipmentGenerator.cs)
  - `SocketData.LinkedToPrevious`
- [EquipmentItem.cs](../Assets/Scripts/Game/UI/EquipmentItem.cs)
  - `AreSocketsLinked(...)`
  - `TryGetLinkedSocketIndices(...)`
  - `GetLinkedGems(...)`
- [BagPanel.cs](../Assets/Scripts/Game/UI/BagPanel.cs)
  - 项目级 `GetLinkedGems(...)` 聚合入口
- [GMItemFactory.cs](../Assets/Scripts/Game/Items/GMItemFactory.cs)
  - 生成装备时写入连结状态

#### 一个重要提醒

旧文档里曾长期写过“连接线完全由 UI 按相邻索引推导、没有数据字段”，**这已经不是当前完整事实**。
当前更准确的说法是：

- **拓扑仍然只支持相邻索引**
- **但连结开关已经是数据驱动：`LinkedToPrevious`**

### 与角色底栏的关系

- 角色底栏技能显示不会直接只看 `SkillComponent.SkillSlots`
- `CharactorMainPanelController` 会通过 `BagPanel` 读取当前已插入的主动技能宝石与其连结的辅助宝石
- 如果“装备上的宝石没问题，但底栏没更新”，要同时看：
  - [BagPanel.cs](../Assets/Scripts/Game/UI/BagPanel.cs)
  - [CharactorMainPanelController.cs](../Assets/Scripts/Game/UI/CharactorMainPanelController.cs)

### 常见排查入口

#### 问题：物品点了拿不起来

优先看：

- [BagItemView.cs](../Assets/Scripts/Game/UI/BagItemView.cs)
- 当前是否已有 `CurrentDraggingItem`
- 点击是否被其他 UI 截获

#### 问题：物品拿起来了但放不下

优先看：

- [BagBox.cs](../Assets/Scripts/Game/UI/BagBox.cs)
- [EquipmentSlotView.cs](../Assets/Scripts/Game/UI/EquipmentSlotView.cs)
- [SocketItem.cs](../Assets/Scripts/Game/UI/SocketItem.cs)
- 当前 `BagItemData` 是否缺失或类型不匹配

#### 问题：目标位置已有物品，但没有替换

优先看：

- [BagItemView.cs](../Assets/Scripts/Game/UI/BagItemView.cs)
- [BagBox.cs](../Assets/Scripts/Game/UI/BagBox.cs)
- [EquipmentSlotView.cs](../Assets/Scripts/Game/UI/EquipmentSlotView.cs)
- [SocketItem.cs](../Assets/Scripts/Game/UI/SocketItem.cs)

#### 问题：宝石连结显示 / 支持宝石识别不对

优先看：

- [EquipmentItem.cs](../Assets/Scripts/Game/UI/EquipmentItem.cs)
- [BagPanel.cs](../Assets/Scripts/Game/UI/BagPanel.cs)
- [EquipmentGenerator.cs](../Assets/Scripts/Game/Equipment/EquipmentGenerator.cs)
- [GMItemFactory.cs](../Assets/Scripts/Game/Items/GMItemFactory.cs)

### 从本模块跳到哪里

- **要看 UI / Tips / 角色主面板**：转 [skill-04](./Skill_04_UI_Tips与角色面板.md)
- **要看技能系统 / 技能底栏 / 冷却 Mask**：转 [skill-05](./Skill_05_技能系统_技能栏与快捷键.md)
- **要看装备生成 / 商店链路**：转 [skill-06](./Skill_06_装备生成_商店_NPC与配置工具链.md)
- **要看 SOP / 排错表**：转 [skill-07](./Skill_07_SOP_排错与高风险点.md)
