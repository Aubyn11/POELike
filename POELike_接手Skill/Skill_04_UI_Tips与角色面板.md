## skill-04 UI / Tips 与角色面板

> 目标：**把“位置 / 层级 / 内容 / 刷新”四类 UI 问题拆开理解。**
> 
> 如果问题属于“显示出来了但位置不对”“内容不对”“被遮挡”“角色面板没刷新”，优先读本模块。

### UI 主入口

#### [UIManager.cs](../Assets/Scripts/Managers/UIManager.cs)

- 游戏内 UI 总入口
- 负责：
  - 面板加载与开关
  - 常驻 `UIRootCanvas`、`UIEventSystem`、`TooltipOverlay`
  - 背包 `I` 键、角色信息面板 `C` 键、`ESC` 统一关闭策略
  - 角色底栏与 Tooltip Overlay 的排序控制

### 装备 Tips 的职责分离

#### [EquipmentItem.cs](../Assets/Scripts/Game/UI/EquipmentItem.cs)

- 负责 **Tips 的显示时机、定位、翻边、跟随、层级挂载**
- 如果问题是“Tips 为什么跑偏 / 被挡住 / 不跟着物品”，优先看这里

#### [EquipmentTips.cs](../Assets/Scripts/Game/UI/EquipmentTips.cs)

- 负责 **Tips 的内容填充与布局刷新**
- 如果问题是“Tips 有框但内容不对 / 漏字段 / 格式乱”，优先看这里

#### [BagItemData.cs](../Assets/Scripts/Game/UI/BagItemData.cs) / [EquipmentGenerator.cs](../Assets/Scripts/Game/Equipment/EquipmentGenerator.cs)

- 提供 Tips 所需的数据源
- 如果 `EquipmentTips` 已经支持某字段，但 Tips 仍然空白，很多时候不是 UI 模板问题，而是数据源没带上来

### 当前 Tips 行为约定

- **Tips 会跟随装备当前所在位置**，而不是停在背包初始坐标
- **Tips 越界会翻到另一侧**
- **装备 Tips 当前挂在 `UIManager.TooltipOverlayRoot` 下**，层级高于角色底栏
- **背包装备 Tips 不显示装备类型与占格尺寸，但显示前后缀词条**
- **药剂 Tips 不显示占格尺寸和可装备槽位，只显示药剂相关信息**
- **`EquipmentItem` 当前还负责可堆叠通货等物品的右下角数量角标；该角标由运行时动态创建的 `TextMeshProUGUI` 显示，初始化时必须先绑定 `TMP_FontAsset`（必要时同步设置 `fontSharedMaterial`），再设置 `outlineWidth / outlineColor`**。因为地面拾取 / GM 入包可能走隐藏背包路径，若先设描边再绑字体，TMP 可能在 `CreateMaterialInstance(new Material(source))` 里因材质源为空抛出 `ArgumentNullException: source`

### 地面掉落名称标签 / NPC 头顶名称

#### [NpcMeshRenderer.cs](../Assets/Scripts/Game/NpcMeshRenderer.cs)

- 负责 **NPC 头顶名称、悬停、点击寻路**
- 如果问题是“NPC 名称为什么不显示 / 为什么不响应鼠标 / 风格为什么变化了”，优先看这里

#### [GroundItemLabelRenderer.cs](../Assets/Scripts/Game/GroundItemLabelRenderer.cs)

- 负责 **怪物死亡后地面掉落装备 / 可堆叠通货名称的绘制、背景底板与鼠标移入高亮**
- 挂在主摄像机上，由 `CameraController` 自动添加并注入 `World`
- 如果问题是“掉落名称为什么没出来 / 背景为什么不亮 / 亮的范围为什么不对”，优先看这里

### 当前地面掉落名称行为约定

- **真实死亡链当前走 `CombatSystem` 发布 `EntityDiedEvent`**
- **`GameSceneManager` 当前会过滤 `Monster` 死亡，并独立按概率尝试生成装备掉落与可堆叠通货掉落；成功生成后会分别发布 `GroundItemDroppedEvent`**
- **`GroundItemLabelRenderer` 会在怪物位置绘制地面掉落名称；若掉落物是通货，名称会带数量**

- **标签默认带深色背景，鼠标移入时会按名称实际宽高整块高亮背景，同时文字提亮为白色**
- **点击 NPC 名称或地面掉落名称时，当前会先锁定为交互意图，避免被同一左键的普通地面移动覆盖**
- **点击地面掉落名称时，当前不会直接秒捡，而是先由 `GameSceneManager` 驱动角色走近目标掉落**
- **进入拾取范围后，当前掉落会立即做背包容量检测；若背包放不下，则在屏幕中下方提示“背包放不下了”**
- **只有背包放得下时，当前掉落才会真正转换为 `BagItemData` 入包，并从地面标签列表移除**
- **点击 NPC 名称后，进入交互距离时会立即停下并打开对话框**
- **多个同点或近距离掉落标签当前仍会做有限的屏幕空间避让，但标签位置现已改为缓存布局：只会在刚掉落时，或后续当前位置第一次被背包等 UI 面板遮挡时才重新排位。其余时间只保持原有避让槽位并跟随真实世界投影移动，不会再每帧自行换位置；被面板遮住时会按重叠区域裁剪遮挡，未被遮住的部分继续显示，超出屏幕时按真实投影直接裁切；并且当前布局避让只按真正可见的标签片段参与，被面板遮住或已经裁到屏幕外的不可见区域不会再影响附近标签布局**

- **掉落标签的显示位置就算因为避让而发生变化，点击后的寻路目标仍然始终使用原始掉落世界坐标**

### 角色主面板 / 角色信息面板

#### [CharactorMainPanelController.cs](../Assets/Scripts/Game/UI/CharactorMainPanelController.cs)

- **角色底栏显示控制器**
- 当前负责：
  - 技能栏显示
  - 药剂栏显示
  - 技能冷却 Mask
  - 血蓝遮罩百分比显示
- 若问题是“底栏 UI 为什么这样显示”，先看这里

#### [CharactorMassagePanelController.cs](../Assets/Scripts/Game/UI/CharactorMassagePanelController.cs)

- **角色信息 / 属性详情面板**
- 当前负责：
  - 名称 / 等级显示
  - 力量 / 智力 / 敏捷显示
  - 伤害 / 防御 / 其他属性分类列表
  - 从 `StatsComponent` 与已装备装备词条中组合出可展示属性

### 当前角色面板行为约定

#### `CharactorMassagePanel`

- 当前不再进入 `GameScene` 自动弹出
- 由 `UIManager` 用 **`C` 键** 开关
- 数据来源：
  - 名称 / 等级：`SceneLoader.PendingCharacterData`
  - 力量 / 智力 / 敏捷 与聚合属性：玩家实体 `StatsComponent`
  - 额外装备词条：已装备装备的 `RolledMod`

#### 页签行为

- `DamageBtn`：伤害类
- `DefenceBtn`：防御类
- `OtherBtn`：其他类

#### 词条分类补充

- 即使某些词条没有写入 `StatsComponent`，仍可能按配置表中的显示分类展示在面板上
- 如果“数值没有参与计算，但面板有展示”，先区分这是**展示层补充**还是**聚合值结果**

### 角色主面板血蓝遮罩约定

看 [CharactorMainPanelController.cs](../Assets/Scripts/Game/UI/CharactorMainPanelController.cs)：

- 当前会绑定：
  - `Hp/Mask`
  - `Mp/Mask`
- 从玩家 `HealthComponent` 读取：
  - `HealthPercent`
  - `ManaPercent`
- 当前实现使用：
  - `Image.Type.Filled`
  - `Vertical`
  - 自下而上显示
- 面板激活后还会订阅血蓝变化，因此技能耗蓝、受伤、回血时会实时刷新

### 角色主面板药剂遮罩约定

同样看 [CharactorMainPanelController.cs](../Assets/Scripts/Game/UI/CharactorMainPanelController.cs)：

- `PotionArr` 下每个药剂槽当前都会使用子节点 `Mask` 作为充能遮罩；若旧 prefab 缺失该节点，运行时会自动补一个覆盖层
- 当前药剂遮罩使用：
  - `Image.Type.Filled`
  - `Vertical`
  - 自上而下显示“已消耗部分”
- 对应药剂槽剩余充能百分比 = `当前充能 / 总充能`
- 槽位遮罩显示的是 `1 - 剩余充能百分比`，因此**药剂剩余充能越少，遮罩越多；满充能时不显示遮罩**
- 药剂使用后，`GameSceneManager` 会立即刷新角色主面板，所以底栏药剂遮罩会同步变化

### 技能栏显示约定

#### 默认键位标签

- 当前底栏默认技能标签为：
  - `LMB / MMB / RMB / Q / W / E / R / T`
- 入口：`ResolveDefaultSkillKey(...)`

#### 冷却 Mask

- 技能槽子节点 `Mask` 的 `Image` 用于冷却遮罩
- 当前冷却遮罩使用：
  - `Image.Type.Filled`
  - `Radial360`
  - 从顶部开始
  - `fillClockwise = true`
- 视觉效果为：**顺时针收缩**

### `ESC` 与面板关闭策略

当前由 [UIManager.cs](../Assets/Scripts/Managers/UIManager.cs) 统一接管：

- 会一次关闭所有当前已打开的可关闭游戏内 UI
- 覆盖范围包括：
  - 背包
  - `CharactorMassagePanel`
  - 继承 `UIGamePanel` 的窗口
- 不再依赖 Unity EventSystem 默认 `Cancel`

### 常见排查入口

#### 问题：Tips 出现在错误位置 / 被状态栏挡住

优先看：

- [EquipmentItem.cs](../Assets/Scripts/Game/UI/EquipmentItem.cs)
- [UIManager.cs](../Assets/Scripts/Managers/UIManager.cs)
- 当前是否挂在 `TooltipOverlayRoot`

#### 问题：Tips 文案不对

优先看：

- [EquipmentTips.cs](../Assets/Scripts/Game/UI/EquipmentTips.cs)
- [BagItemData.cs](../Assets/Scripts/Game/UI/BagItemData.cs)
- [EquipmentGenerator.cs](../Assets/Scripts/Game/Equipment/EquipmentGenerator.cs)
- [ShopPanel.cs](../Assets/Scripts/Game/UI/ShopPanel.cs)

#### 问题：角色信息面板数据不刷新

优先看：

- [UIManager.cs](../Assets/Scripts/Managers/UIManager.cs)
- [CharactorMassagePanelController.cs](../Assets/Scripts/Game/UI/CharactorMassagePanelController.cs)
- [EquipmentSlotView.cs](../Assets/Scripts/Game/UI/EquipmentSlotView.cs)
- [SocketItem.cs](../Assets/Scripts/Game/UI/SocketItem.cs)

#### 问题：主面板血蓝或技能冷却不动

优先看：

- [CharactorMainPanelController.cs](../Assets/Scripts/Game/UI/CharactorMainPanelController.cs)
- [HealthComponent.cs](../Assets/Scripts/ECS/Components/HealthComponent.cs)
- [SkillComponent.cs](../Assets/Scripts/ECS/Components/SkillComponent.cs)

#### 问题：怪物死亡后掉落名称没显示 / 背景不高亮 / 点击后没拾取

优先看：

- [GroundItemLabelRenderer.cs](../Assets/Scripts/Game/GroundItemLabelRenderer.cs)
- [GameSceneManager.cs](../Assets/Scripts/Game/GameSceneManager.cs)
- [CameraController.cs](../Assets/Scripts/Game/CameraController.cs)
- [CombatSystem.cs](../Assets/Scripts/ECS/Systems/CombatSystem.cs)
- [GameSceneInitializer.cs](../Assets/Scripts/Game/GameSceneInitializer.cs)（仅负责测试初始化，不再负责掉落桥接）

### 从本模块跳到哪里

- **要看背包 / 装备 / 宝石移动链**：转 [skill-03](./Skill_03_背包_装备_宝石交互.md)
- **要看技能系统 / 技能底栏 / 快捷键**：转 [skill-05](./Skill_05_技能系统_技能栏与快捷键.md)
- **要看 SOP / 排错表**：转 [skill-07](./Skill_07_SOP_排错与高风险点.md)
