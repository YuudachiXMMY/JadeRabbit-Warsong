# ScriptableObject 数据层 (SO Data Layer)

> **Status**: In Review (Round-5 Revision — Pending Re-review)
> **Author**: Jadyn Wu + Claude Code Agents
> **Last Updated**: 2026-07-15
> **Implements Pillar**: 支柱一：节奏即战斗 / 支柱四：构筑驱动复盘
> **System ID**: F4
> **Layer**: Foundation | **Milestone**: MVP | **Design Order**: 3

---

## Overview

F4 ScriptableObject 数据层是《玉兔：破阵》的数据契约基础设施——它定义所有跨系统游戏数据的**存储 schema 与编辑器接口**，将「硬编码的游戏参数」转变为「可热修改的资产文件」。

本层负责维护 7 类核心 ScriptableObject 类型：`AttackNodeSO`（攻击节点）、`ComboGraphSO`（连招图）、`PerkSO`（心法）、`WeaponSO`（武器）、`EnemyInfoSO`（敌人信息）、`LevelConfigSO`（关卡配置）和 `PlayerInfoSO`（玩家持久化数据）。每个 schema 是对应系统（C2、C3、C5、FE1、F2、F5）的数据契约：下游系统依赖 F4 定义的字段集合完成运行时行为，F4 的 schema 变更须触发下游 GDD 的接口合约更新。

对玩家而言，F4 是「每局心法 build 感觉不同」「武器切换带来截然不同的节奏手感」「设计师可以不改代码就在游戏中调整 BPM 曲线与心法强度」的数据物理基础。F4 的 schema 质量直接决定支柱四（构筑驱动复盘）的设计空间深度——PerkSO 的字段表达能力越强，心法的设计可能性就越丰富。

本 GDD 定义各 SO 类型的**字段规范（必填/可选字段、类型、范围约束）、分类体系、Roguelike 局内克隆规则和 Editor 验证策略**。SO 序列化存档方案（PlayerInfoSO → EasySave 3）见 F5 GDD；Addressables 加载策略见 F6 GDD。

---

## Player Fantasy

**玩家侧**：玩家永远不会知道「F4 数据层」的存在。但他们会在第三局结束时感受到它——这局拿到的三个心法恰好和上局完全不同，苗刀这次可以打 Perfect → Miss → Perfect 的奇怪节奏链，而那是因为某个心法改变了连招取消规则。「每局都感觉不一样」的惊奇感，根植于 PerkSO 的字段表达能力。构筑多样性不是凭空出现的；它是 schema 设计质量的直接产物。

**设计师侧**：F4 是数据驱动开发的物理现实——新增一种心法不需要改代码，只需在 Editor 中填写 PerkSO 资产的字段并配置效果；调整某个房间的 BPM 只需修改 LevelConfigSO 资产。这让「快速迭代手感」从一个美好愿景变成可操作的日常工作流。支柱四（构筑驱动复盘）能否成立，取决于设计师在一个下午内能创造并测试多少新心法——F4 的 schema 设计好坏直接决定这个生产速度。

**缺席型幻想**：F4 正常工作时完全透明。当 F4 出问题——如 PerkSO 字段缺少范围约束，导致某个心法意外将伤害倍率设为 100×——玩家感知到的不是「数据层故障」，而是「游戏坏了」。F4 的健康通过 Odin Validator 的 Editor 验证规则来守护，确保资产投入运行前字段完整性已通过自动核查。

---

## Detailed Design

### Core Rules

**规则 1：SO 类型分类与职责**

F4 定义以下 7 类核心 ScriptableObject，每类对应一个明确的数据职责域：

| SO 类型 | 数据域 | 主要消费系统 | 运行时克隆 |
|---------|--------|------------|-----------|
| `AttackNodeSO` | 单个攻击节点：动画 Clip 引用、Hitbox 激活时机、取消窗口类型、量化偏好、BPM 参考值（ADR-D5） | C2（连招图系统） | 否（只读引用） |
| `ComboGraphSO` | 连招图：节点集合（`List<AttackNodeSO>`）+ 边表（`List<ComboEdge>`，定义输入驱动的分支转换规则）+ 根节点引用 + 武器类型归属 | C2、C5（武器系统） | 否（只读引用） |
| `PerkSO` | 心法：效果类型枚举、效果参数集合、稀有度、UI 描述与图标引用 | C3（心法构筑系统） | **是**（每局开始前克隆） |
| `WeaponSO` | 武器：武器类型枚举、`ComboGraphSO` 引用、基础伤害、动画层配置引用 | C5、FE5（局外成长） | 否（只读引用） |
| `EnemyInfoSO` | 敌人：基础属性、行为档案类型、Beat-Locked 攻击模式描述 | FE1（敌人 AI 系统） | 否（只读引用） |
| `LevelConfigSO` | 关卡/房间：BGM FMOD asset 引用、BPM、拍号、房间类型标签 | F2（音频时钟）、FE2（房间系统） | 否（只读引用） |
| `PlayerInfoSO` | 玩家持久化数据：功勋余额、解锁武器列表、局外升级状态、延迟校准偏移量 | F5（存档系统）、FE5 | 否（不克隆；直接序列化/反序列化） |

**规则 2：只读资产原则**

所有 SO 资产在运行时为只读（`readonly` 语义）。禁止在游戏运行时通过任何代码路径修改 SO 资产文件的字段值。任何需要「在本局内临时修改 SO 数据」的系统，必须先克隆目标 SO 实例，再修改克隆体。违反此规则将导致 Play Mode 修改溢出到 Editor 资产（Unity 的 SO 特有陷阱），造成数据污染。

**规则 3：PerkSO 运行时克隆规则**

`PerkSO` 是唯一在局内需要被修改（如心法叠加层计数、运行时参数覆盖）的 SO 类型。克隆规则：
- 每局开始（进入第一个房间）前，C3 系统按需调用 `Instantiate(perkSO)` 克隆候选 PerkSO 资产
- 克隆体在当局内有效；局结束（死亡/通关）时，C3 销毁所有克隆体，不向持久化层写入
- `PlayerInfoSO` 的「已解锁心法 ID 列表」指向原始资产引用，不持有克隆体

**规则 4：Schema 变更传播合约**

F4 的任意 SO 类型新增/修改/删除字段，均视为「接口变更」，须触发对应下游 GDD 的接口合约节的更新审核。变更须在下游 GDD 的依赖章节中注明版本兼容性。

**规则 5：Odin Editor 验证强制启用**

F4 使用两种互补（而非互替）的 Odin 验证机制：

- **方案 A — Inspector 内联 Attribute**（设计师侧视觉反馈）：在 SO 类文件中用 `#if UNITY_EDITOR` 块包裹 `using Sirenix.OdinInspector;`，然后在字段上标注 `[Required]`、`[MinValue]`、`[MaxValue]`、`[ValidateInput]` 等 Attribute。作用：设计师在 Unity Inspector 中实时看到字段验证状态（红色高亮）。**此方案仅提供 Inspector UX，不驱动 CI 阻断。**

- **方案 B — Editor 程序集外部验证类**（CI 阻断门控，**必须实现**）：在 Editor 专属程序集中使用 Odin Validator 的 `[RegisterValidator(typeof(WeaponSO))]` 外部验证类，执行跨资产约束检查（如重复 ID 检测）。此方案驱动 Odin Validator CI 扫描并阻断构建。

**完整覆盖须同时实现方案 A 和方案 B。** 仅实现方案 A 时，CI 构建不会自动执行字段验证。**禁止**在 `#if UNITY_EDITOR` 保护以外的任何 Runtime 代码中出现 `using Sirenix.*;`（打包时 Odin 程序集不进入包体，会导致打包失败）。

**规则 6：Addressables 引用策略（已定）**

所有 SO 资产须归入 Addressable Group，通过 Addressables API 按需加载，不使用 `Resources/` 目录直接加载。**大型 Unity Object 引用字段（`AnimationClip`、`AudioClip`、`Sprite`）须使用 `AssetReferenceT<T>` 延迟引用**——直接引用会将资产锁入同一 Bundle，在 ScriptableObject 加载时触发隐式全量拉取（单个 ComboGraphSO 加载时可隐式拉取 20 个 AnimationClip）。`AttackNodeSO.animationClip`、`PerkSO.iconSprite` 均已采用 `AssetReferenceT<T>`；`LevelConfigSO.bgmAssetRef` 为 `string`（FMOD Core API 资产路径，非 Addressables 托管——见 ADR-D6）。`List<AttackNodeSO>`（小型元数据 SO）保留直接引用。具体 Bundle 分组策略、加载时机和 Handle 释放规则由 F6 GDD 定义。

---

### States and Transitions

F4 是纯数据定义层，不持有运行时状态机。相关 SO 实例的生命周期状态如下：

| SO 类型 | 状态序列 | 状态所有者 |
|---------|---------|-----------|
| `PerkSO` | `Unloaded → Loaded (原始资产, 只读) → Cloned (局内实例, 可写) → Destroyed (局结束)` | C3 |
| `PlayerInfoSO` | `Unloaded → Loaded → Active (内存, 读/写) → Serialized (存档) → Unloaded` | F5 |
| 其余 5 类 | `Unloaded → Loaded (只读引用) → Unloaded` | F6/Addressables |

状态转换的触发信号和执行者由各消费系统的 GDD 定义，F4 不持有这些状态。

---

### Interactions with Other Systems

| 方向 | 系统 | F4 提供的数据 | 接口类型 |
|------|------|-------------|---------|
| F4 **→** C2 | 连招图系统 | `ComboGraphSO`（含 `AttackNodeSO` 列表） | Addressables 异步加载引用 |
| F4 **→** C3 | 心法构筑系统 | `PerkSO` 资产（克隆源） | Addressables 加载 + `Instantiate()` 克隆 |
| F4 **→** C5 | 武器系统 | `WeaponSO`（含 `ComboGraphSO` 引用） | Addressables 异步加载引用 |
| F4 **→** FE1 | 敌人 AI 系统 | `EnemyInfoSO` | Addressables 异步加载引用 |
| F4 **→** F2 | 音频时钟系统 | `LevelConfigSO`（BGM Clip、BPM、拍号） | Inspector 直接引用（由 F6 关卡加载时注入） |
| F4 **→** F5 | 存档系统 | `PlayerInfoSO` schema（序列化字段定义） | F5 持有 PlayerInfoSO 实例，使用 EasySave 3 序列化 |
| F4 **→** FE2 | 房间系统 | `LevelConfigSO`（房间类型标签、关卡流控制） | Addressables 异步加载引用 |

#### F4-S7 ↔ F5 DTO 字段映射表

PlayerInfoSO 的字段并非全部参与 F5 的 PlayerProgressDTO 序列化往返。以下映射表是 SO → DTO 提取和 DTO → SO 填充的权威参考：

| F4 PlayerInfoSO 字段 | F5 DTO | DTO 字段名 | 映射方向 | 说明 |
|---------------------|--------|-----------|---------|------|
| `playerMerit` | PlayerProgressDTO (F5-D1) | `merit` | SO ↔ DTO 双向 | ⚠️ 命名不一致（SO 侧 `playerMerit`，DTO 侧 `merit`）；提取/填充代码须显式映射 |
| `unlockedWeaponIds` | PlayerProgressDTO (F5-D1) | `unlockedWeaponIds` | SO ↔ DTO 双向 | SO 侧 `List<string>`，DTO 侧 `string[]`；提取时 `.ToArray()`，填充时 `new List<>(dto.unlockedWeaponIds)` |
| `metaUpgradeLevels` | PlayerProgressDTO (F5-D1) | `metaUpgradeLevels` | SO ↔ DTO 双向 | SO 侧 `List<MetaUpgradeEntry>`，DTO 侧 `MetaUpgradeEntry[]`；同上转换 |
| `latencyCalibrationOffsetMs` | LatencyCalibrationDTO (F5-D3) | `latencyOffsetMs` | **F5 → SO 单向填充** | `[NonSerialized]` 运行时缓存；权威来源为 F5 Global DTO。F2 在 `Start()` 从 F5 加载后写入 SO |
| `audioVolumeMaster` | GameSettingsDTO (F5-D4) | `masterVolume` | **F5 → SO 单向填充** | `[NonSerialized]` 运行时缓存；权威来源为 F5 Global DTO。FE7 启动时从 F5 加载后写入 SO |
| — | PlayerProgressDTO (F5-D1) | `runCount` | **DTO-only** | F5 内部追踪字段；PlayerInfoSO 不持有（无运行时消费系统需要此值） |
| — | PlayerProgressDTO (F5-D1) | `totalPlayTimeSeconds` | **DTO-only** | F5 内部追踪字段；同上 |
| — | PlayerProgressDTO (F5-D1) | `save_version` | **DTO-only** | 存档版本控制元数据；SO 层无对应概念 |

**实现约束**：FE5 在局结算时执行 SO → DTO 提取，须仅提取上表中「SO ↔ DTO 双向」映射的三个字段（`playerMerit → merit`、`unlockedWeaponIds`、`metaUpgradeLevels`），`runCount` 和 `totalPlayTimeSeconds` 由 FE5 自行递增后写入 DTO。`latencyCalibrationOffsetMs` 和 `audioVolumeMaster` **不参与** PlayerProgressDTO 序列化——它们通过独立的 Global DTO 文件管理。

---

## Formulas

F4 不包含计算公式，但定义了每种 SO 类型的**字段规格**——这等价于其他系统的公式约束，是下游系统编程时唯一可信的接口文档。

---

### F4-S1：AttackNodeSO 字段规格

单个攻击节点，`ComboGraphSO` 的基本构成单元。

| 字段 | 类型 | 范围/约束 | 必填 | 描述 |
|------|------|----------|------|------|
| `id` | `string` | 非空，项目内唯一 | 是 | 攻击节点 ID，格式：`weapon_attackType`（如 `miaodao_light1`） |
| `animationClip` | `AssetReferenceT<AnimationClip>` | 非 null | 是 | Animancer 层播放的动画 Clip（Addressables 延迟引用；由 F6 在武器加载时调用 `LoadAssetAsync` 解析，C2 持有句柄）|
| `hitboxStartNormalizedTime` | `float` | [0.0, 1.0) | 是 | Hitbox 激活的动画归一化时间（0=动画开始，接近1.0=动画结束前）；与 Animancer 变速无关，BPM 变化时 Hitbox 相对时机不变。**上界为开区间（不含 1.0）**：当 `start=1.0` 时跨字段约束 `start + duration ≤ 1.0` 要求 `duration ≤ 0`，与 `hitboxDurationNormalized` 的下界约束 `(0.0, 1.0]` 不相容，不存在任何合法值——Odin `[ValidateInput]` 须对 `hitboxStartNormalizedTime ≥ 1.0` 报 Error：「start=1.0 时无任何合法 hitbox duration 值」 |
| `hitboxDurationNormalized` | `float` | (0.0, 1.0] | 是 | Hitbox 持续的动画归一化时长；约束：`hitboxStartNormalizedTime + hitboxDurationNormalized ≤ 1.0` |
| `cancelWindowType` | `CancelWindowType` | `{None, Standard, Advanced}` | 是 | 取消窗口类型：None = 无取消；Standard = 普通取消；Advanced = 仅 Perfect 解锁 |
| `cancelWeight` | `float` | [0.0, 1.0] | 是 | 取消窗口开启的归一化时间点：取消窗口从 `cancelWeight` 处开始，延伸至动画结束（1.0）。`cancelWeight=0.0` = 从动画第 0 帧即可取消；`cancelWeight=0.6` = 前 60% 动画锁定，60% 处取消窗口开启；`cancelWeight=1.0` = 窗口永不开启（等效于 `cancelWindowType=None`）。**注**：当 `cancelWindowType=None` 时，`cancelWeight` 字段的值被 C2 忽略（无论设为何值均无取消窗口）；Odin Validator 在 `cancelWindowType=None` 且 `cancelWeight < 1.0` 时报告 Warning 以提示设计师。 |
| `quantizationHint` | `QuantizationHint` | `{FullBeat, HalfBeat, Free}` | 否 | 本节点在连招树中的量化对齐偏好；C1 参考此值但最终量化由 C1 决定 |
| `authoredBpm` | `float` | [60, 240]，默认 120 | 是 | 动画师创作此 Clip 时使用的参考 BPM。运行时 Animancer 攻击层根据 `state.Speed = BeatClock.CurrentBpm / authoredBpm` 动态缩放动画时长，确保攻击与节拍网格对齐（ADR-D5 定义）。**禁止为 0**（除零异常）；Odin Validator `[MinValue(1f)]` 阻止此配置错误 |

**示例**：苗刀一段轻击 → `hitboxStartNormalizedTime=0.13, hitboxDurationNormalized=0.07, cancelWindowType=Standard, cancelWeight=0.6, authoredBpm=120`（原 @60fps 帧数换算：8/60 ≈ 0.13，4/60 ≈ 0.07；BPM=120 下创作）

---

### F4-S2：ComboGraphSO 字段规格

连招图，武器的完整攻击树容器。

| 字段 | 类型 | 范围/约束 | 必填 | 描述 |
|------|------|----------|------|------|
| `id` | `string` | 非空，项目内唯一 | 是 | 连招图 ID，格式：`weapon_comboGraph`（如 `miaodao_comboGraph`） |
| `weaponType` | `WeaponType` | `{MiaoDao, Spear, Sword}` | 是 | 归属武器类型 |
| `nodes` | `List<AttackNodeSO>` | 节点数 [1, 20] | 是 | 连招图所有攻击节点；列表索引作为节点 ID 供 edges 中的 fromNodeIndex/toNodeIndex 引用 |
| `rootNodeIndex` | `int` | [0, nodes.Count-1] | 是 | 连招树起始节点在 `nodes` 列表中的索引 |
| `edges` | `List<ComboEdge>` | 边数 [0, 60] | 是 | 节点间转换规则列表；空列表表示单节点无分支连招（仅 rootNodeIndex 节点可用） |

#### ComboEdge 字段规格

每条 `ComboEdge` 定义两个节点之间的输入驱动转换，实现连招分支。`ComboEdge` 为 `[Serializable]` struct，由 C2 在运行时遍历匹配。

| 字段 | 类型 | 范围/约束 | 必填 | 描述 |
|------|------|----------|------|------|
| `fromNodeIndex` | `int` | [0, nodes.Count-1] | 是 | 转换起始节点在 `nodes` 中的索引 |
| `toNodeIndex` | `int` | [0, nodes.Count-1] | 是 | 转换目标节点在 `nodes` 中的索引；不得等于 fromNodeIndex（禁止自环） |
| `requiredInput` | `AttackInputType` | `{Normal, Heavy, Special}` | 是 | 玩家须在取消窗口内按下的攻击键类型 |

**设计约束**：
- 同一 `fromNodeIndex` 可有多条 edges，但各边 `requiredInput` 须唯一（相同起始节点禁止两条边使用同一输入类型）
- `fromNodeIndex == toNodeIndex` 的自环边由 Odin Validator 报告 Error（阻止构建）
- **多节点环路（如 A→B→A）为合法的循环连招设计**，不予禁止——玩家须持续提供输入才能遍历每条边，不会产生无限循环；C2 须实现单局最大遍历深度上限（建议 100 步）防止极端场景
- C2 在取消窗口期遍历 edges 列表，匹配 `fromNodeIndex = 当前节点索引` 且 `requiredInput = 玩家输入`；无匹配时连招重置到 rootNodeIndex

**示例**：苗刀两段连招 → `nodes=[LightStrike1(0), LightStrike2(1), HeavyFinisher(2)]`，`edges=[{from=0, to=1, input=Normal}, {from=1, to=2, input=Heavy}]`，`rootNodeIndex=0`

---

### F4-S3：PerkSO 字段规格

心法，构筑系统的最小单元。

| 字段 | 类型 | 范围/约束 | 必填 | 描述 |
|------|------|----------|------|------|
| `id` | `string` | 非空，项目内唯一 | 是 | 心法 ID（如 `perk_perfect_chain`） |
| `displayName` | `string` | 非空 | 是 | 玩家可见名称（支持本地化 key） |
| `rarity` | `PerkRarity` | `{Common, Uncommon, Rare, Legendary}` | 是 | 影响三选一出池权重 |
| `effectType` | `PerkEffectType` | 枚举（见注） | 是 | 效果分类，C3 用于路由效果处理逻辑 |
| `parameters` | `List<PerkParameter>` | 列表长度 [0, 8] | 否 | 效果数值参数词典；`PerkParameter` 须声明为 `[Serializable] struct`（值类型）以确保 `Instantiate(perkSO)` 时列表元素深拷贝，防止克隆体修改污染原始资产。每条 `PerkParameter: {key: string, value: float, minValue: float, maxValue: float}`；key 由 effectType 语义约定。|
| `triggerCondition` | `PerkTrigger` | `{Passive, OnPerfect, OnGood, OnMiss, OnComboEnd}` | 是 | 效果触发时机：`Passive` = 常时生效（被动）；其余为对应判定结果/事件触发。**⚠️ 权威枚举值以本行定义为准**；Open Questions 节中出现的其他候选值（`OnHit`、`OnKill`、`OnBeat` 等）为 C3 扩展讨论草稿，非当前定义 |
| `maxStacks` | `int` | [1, 10] | 是 | 效果最大叠加层数；`Passive` 心法须设为 1（Odin Validator 强制校验） |
| `stackBehavior` | `StackBehavior` | `{Additive, Capped, Replace}` | 是 | 叠加行为：`Additive` = 每触发一次效果数值累加；`Capped` = 达到 maxStacks 后不再触发；`Replace` = 新触发覆盖旧状态（计时刷新）；`Passive` 心法须设为 `Capped` |
| `descriptionTemplate` | `string` | 含 `{key}` 占位符 | 是 | UI 显示文本模板，运行时将 `parameters` 值代入 |
| `iconSprite` | `AssetReferenceT<Sprite>` | 非 null | 是 | 心法选择 UI 图标（Addressables 延迟引用；由 C3 心法选择界面按需加载，避免全部 Perk Sprite 在池初始化时同步加载）|

*`PerkEffectType` 枚举候选值（C3 GDD 最终确认）：`DamageModifier, ComboModifier, RhythmRuleModifier, MovementModifier, MetaModifier`*

---

### F4-S4：WeaponSO 字段规格

| 字段 | 类型 | 范围/约束 | 必填 | 描述 |
|------|------|----------|------|------|
| `id` | `string` | 非空，项目内唯一 | 是 | 武器 ID（如 `weapon_miaodao`） |
| `weaponType` | `WeaponType` | `{MiaoDao, Spear, Sword}` | 是 | 武器类型 |
| `comboGraph` | `ComboGraphSO` | 非 null | 是 | 本武器使用的连招图资产引用 |
| `baseDamage` | `float` | [1.0, 100.0] | 是 | 武器基础伤害（`final_damage` 公式中的 `base_damage`） |
| `animatorLayerName` | `string` | 非空 | 是 | Animancer 层名称，C5 用于配置攻击动画层；命名格式由 F6/C5 GDD 联合定义 |
| `defaultQuantizationGranularity` | `QuantizationHint` | `{FullBeat, HalfBeat, Free}` | 是 | 本武器连招默认量化粒度；可被单个 `AttackNodeSO.quantizationHint` 覆盖；C1 用于决定该武器的整体节拍对齐偏好 |
| `perfectWindowModifier` | `float` | [0.5, 2.0] | 是 | 本武器 Perfect 判定窗口的倍率修正（1.0 = 基准窗口；>1.0 = 窗口更宽松；<1.0 = 更严格）；默认 1.0；C1 在计算 Perfect 窗口时将此值与基准值相乘 |

---

### F4-S5：EnemyInfoSO 字段规格

| 字段 | 类型 | 范围/约束 | 必填 | 描述 |
|------|------|----------|------|------|
| `id` | `string` | 非空，项目内唯一 | 是 | 敌人 ID（如 `enemy_patrol_hawk`） |
| `displayName` | `string` | 非空 | 是 | 敌人名称 |
| `baseHealth` | `int` | [1, 9999] | 是 | 基础生命值 |
| `baseDamage` | `float` | [1.0, 999.0] | 是 | 基础攻击伤害 |
| `behaviorProfile` | `EnemyBehaviorProfile` | `{Melee_Simple, Ranged_Simple, Elite, Boss}` | 是 | FE1 行为树路由所用的档案类型 |
| `attackWindupBeats` | `float` | [0.5, 8.0] | 是 | Beat-Locked 攻击前摇拍数（支持半拍精度；0.5 = 半拍前摇）；C4 令牌分配使用；BPM=120 时 1 拍 = 500ms |
| `beatPhaseAnchor` | `BeatPhaseAnchor` | `{DownBeat, UpBeat, Free}` | 是 | 攻击起始相位锚点：`DownBeat` = 强拍起；`UpBeat` = 弱拍起；`Free` = 无相位约束；影响 FE1 攻击发起时机的节拍对齐逻辑 |
| `rewardMeritBase` | `int` | [0, 100] | 是 | 击败奖励的基础功勋值 |

---

### F4-S6：LevelConfigSO 字段规格

| 字段 | 类型 | 范围/约束 | 必填 | 描述 |
|------|------|----------|------|------|
| `id` | `string` | 非空，项目内唯一 | 是 | 关卡/房间 ID |
| `roomType` | `RoomType` | `{Entrance, Combat, Elite, Boss, Reward}` | 是 | 房间类型，FE2 路由房间流程 |
| `bgmAssetRef` | `string`（StreamingAssets 相对路径）| 非 null | 是 | 注入 F2 BeatClock 的 BGM 音频文件路径（ADR-D6：FMOD Core API 以 `FMOD_CREATESAMPLE` 全量加载；BGM 音频文件 3–15 MB，F6 在房间进入时通过 FMOD `System.createSound()` 加载；具体引用类型由 F6 GDD 定案，可能替换为 FMOD 原生引用类型）|
| `bpm` | `float` | [80, 240] | 是 | 房间 BPM；F2 clamp 规则在 F2 GDD 中定义 |
| `timeSignatureNumerator` | `int` | `{3, 4, 5}` | 是 | 拍号分子 |
| `timeSignatureDenominator` | `int` | `{4, 8}` | 是 | 拍号分母（5 分子时必须为 8） |

---

### F4-S7：PlayerInfoSO 字段规格

| 字段 | 类型 | 范围/约束 | 必填 | 描述 |
|------|------|----------|------|------|
| `playerMerit` | `int` | [0, +∞) | 是 | 功勋（跨局永久货币） |
| `unlockedWeaponIds` | `List<string>` | 长度 [1, 10]；首元素固定为 `weapon_miaodao` | 是 | 已解锁武器 ID 列表 |
| `metaUpgradeLevels` | `List<MetaUpgradeEntry>` | 长度 [0, ∞)；entry.level [0, 10] | 否 | 局外升级项列表；`MetaUpgradeEntry` 为 `[Serializable]` struct：`{id: string, level: int}`；Unity 原生可序列化，EasySave 3 兼容（`Dictionary<>` 不被 Unity 原生序列化系统支持） |
| `latencyCalibrationOffsetMs` | `float` | [-200, 200] | 是 | **运行时缓存**（`[NonSerialized]`）：延迟校准偏移（F2 使用；单位 ms）；**权威来源：F5 LatencyCalibrationDTO**（`global/calibration.es3`）。F2 在 `Start()` 中从 F5 加载后写入此字段供运行时快速访问；PlayerInfoSO 序列化时此字段**不参与** PlayerProgressDTO 往返——EasySave 3 不序列化 `[NonSerialized]` 字段。默认 0 |
| `audioVolumeMaster` | `float` | [0.0, 1.0] | 是 | **运行时缓存**（`[NonSerialized]`）：主音量设置；**权威来源：F5 GameSettingsDTO**（`global/settings.es3`）。FE7 在启动时从 F5 加载后写入此字段供运行时快速访问；PlayerInfoSO 序列化时此字段**不参与** PlayerProgressDTO 往返。默认 1.0 |

*注：`PlayerInfoSO` 字段仅含**纯数据类型**（int、float、string、List、自定义 `[Serializable]` struct），无 Unity Object 引用，无 `Dictionary<>` 类型（Unity 不原生序列化 Dictionary，EasySave 3 对 Dictionary 的支持不稳定）——这是 Unity 序列化系统与 EasySave 3 的双重约束（技术首选项）。*

**⚠️ [NonSerialized] 字段初始化时序约束**：`latencyCalibrationOffsetMs` 和 `audioVolumeMaster` 为运行时缓存字段，**不存在于 SO 资产文件中**。Addressables 加载 PlayerInfoSO 时这两个字段将返回 C# 默认值（`0.0f` / `1.0f`），直至 F5 初始化完成后由 F2.Start()（写入 latency 值）和 FE7.Start()（写入 volume 值）填充。**消费系统须等待 F5 加载完成事件后再读取这两个字段**——直接在 Awake() 或早期 Start() 读取将静默使用默认值，导致延迟校准不生效或音量异常。F2 GDD 和 F5 GDD 须在各自的初始化协议章节中明确声明此时序依赖（建议通过事件/回调而非 Script Execution Order 保证）。

---

## Edge Cases

- **如果 `ComboGraphSO.nodes` 列表为空（节点数 = 0）**：Odin Validator 在 Editor 构建时报错并阻止构建；运行时 C2 检测到空节点列表应抛出 `InvalidDataException` 并记录 Error 日志（不静默失败），游戏进入错误状态要求设计师修复资产，不尝试回退到空连招。

- **如果 `ComboGraphSO.rootNodeIndex` 超出 `nodes` 列表边界**：同上，Odin Validator 阻止构建；运行时 C2 clamp 到 0 并记录 Error 日志。

- **如果 `ComboGraphSO.edges` 中某条边的 `fromNodeIndex == toNodeIndex`（自环）**：Odin Validator 报告 Error 并阻止构建；自环会导致 C2 陷入无限循环，属硬性缺陷。

- **如果同一 `fromNodeIndex` 存在两条 `requiredInput` 相同的 edges**：Odin Validator 报告 Error 并阻止构建；运行时 C2 取第一条匹配边执行，但此情况属配置错误须在构建期消除。

- **如果 `AttackNodeSO.hitboxStartNormalizedTime + hitboxDurationNormalized > 1.0`**：Odin Validator 报告 Error（Hitbox 会超出动画时长）；运行时 C2 将 hitboxDurationNormalized clamp 到 `1.0 - hitboxStartNormalizedTime` 并记录 Error 日志。

- **如果 `PerkSO.triggerCondition = Passive` 但 `maxStacks > 1`**：被动心法无叠加语义；Odin Validator 报告 Error（`maxStacks` 须为 1 when `triggerCondition = Passive`）；运行时 C3 强制 clamp 到 1 并记录 Warning。

- **如果 `PerkSO` 克隆发生在局内而非局前**（如中途获得新心法）：C3 在心法奖励分发时按需克隆，不限于局开始时批量克隆；局结束时统一清理所有该局克隆体。F4 不规定克隆时机，只规定克隆责任归属 C3。

- **如果两个 `PerkSO` 资产的 `id` 字段相同**：Odin Validator 在 Editor 运行时检测重复 ID 并报告警告；运行时 C3 使用字典管理心法池，重复 ID 后者覆盖前者并记录 Error 日志。

- **如果 `LevelConfigSO.bpm` 为 0 或超出 [80, 240] 范围**：F2 BeatClock 的 clamp 规则处理此情况（BPM=0 → 120，超界 → clamp）；F4 通过 Odin Validator 的 `[MinValue(80)]`/`[MaxValue(240)]` 在 Editor 层拦截，防止资产带错误值进入运行时。

- **如果 `LevelConfigSO.timeSignatureNumerator=5` 但 `timeSignatureDenominator=4`**（5/4 拍不支持）：Odin 验证规则 `[ValidateInput]` 检查此组合并报错；运行时 F2 不处理 5/4 拍（F2 GDD 只支持 {3, 4, 5} 分子搭配 {4, 8} 分母），若绕过验证进入运行时，F2 将以 fallback 4/4 处理并记录 Error。

- **如果 `PlayerInfoSO` 包含 Unity Object 引用字段**（违反 EasySave 3 约束）：这是 F4 的设计禁止项，不会在运行时发生；通过 Odin Validator 的 `[ValidateInput]` 在 Editor 层检测并阻止。如在 `PlayerInfoSO` 中错误地声明了 `AudioClip` 等引用字段，构建验证直接失败。

- **如果 `PerkSO.parameters` 中某个 `PerkParameter.value` 超出该条目的 `[minValue, maxValue]`**：Odin Validator 在 Editor 中报告警告；运行时 C3 在应用参数时 clamp 到范围内并记录 Warning（不崩溃，允许降级）。

- **如果 `WeaponSO.comboGraph` 引用的 `ComboGraphSO.weaponType` 与 `WeaponSO.weaponType` 不一致**：类型不匹配属于资产配置错误；Odin `[ValidateInput]` 检查此引用一致性并报告 Error；运行时 C5 记录 Warning 但不崩溃（继续使用不匹配的连招图，等设计师修复）。

- **如果 `AttackNodeSO.cancelWindowType = None` 但 `cancelWeight < 1.0`**：语义冲突——`cancelWindowType=None` 表示无任何取消窗口，此时 `cancelWeight` 的值被 C2 完全忽略。Odin Validator 在此组合下报告 Warning（非 Error，不阻断构建）；设计师应将 `cancelWeight` 设为 1.0 保持语义一致。运行时 C2 以 `cancelWindowType` 为最终判定：`None` 时不开取消窗口，无论 `cancelWeight` 为何值。

- **如果 `AttackNodeSO.hitboxDurationNormalized = 0.0`**（违反下界开区间约束 `(0.0, 1.0]`）：零持续时长 Hitbox 在任何帧都不会激活——攻击动画播放但永远不判定命中，是最危险的静默失效路径（不崩溃，不报错，武器完全失效）。字段规格已声明范围为 `(0.0, 1.0]`（下界**开区间**），须在 Odin `[ValidateInput]` 中使用 `value > 0.0f` 严格比较（而非 `[MinValue(0.0)]` 会允许等于 0.0 的值）。对应 AC：AC-F4-32。

- **如果 `ComboGraphSO.edges` 中某条边的 `fromNodeIndex` 或 `toNodeIndex` 超出 `nodes` 列表边界**：边表索引越界比 `rootNodeIndex` 越界更常见（边是手动填写的 struct 列表，设计师在 Inspector 中逐条输入数字，极易误填）。运行时 C2 遍历 edges 时执行 `nodes[fromNodeIndex]` 将抛出 `IndexOutOfRangeException` 崩溃。Odin Validator 须通过 `RegisterValidator` 外部验证类（需同时读取 `edges` 列表和 `nodes.Count`）对所有边执行索引越界检查，发现越界报告 Error 并阻断构建。运行时 C2 须在遍历 edges 前对每条边的两个索引执行边界防御检查，发现越界跳过该边并记录 Error 日志（不崩溃）。对应 AC：AC-F4-31。

---

## Dependencies

### 上游依赖（F4 依赖的系统）

F4 是 Foundation 层系统，**无上游依赖**。F4 的 SO schema 定义不依赖任何其他游戏系统的运行时行为，可以独立设计和实现。

### 下游依赖（依赖 F4 的系统）

| 下游系统 | 依赖类型 | 从 F4 获取的 SO 类型 | 接口说明 |
|---------|---------|-------------------|---------|
| **C2** 连招图系统 | **硬依赖** | `ComboGraphSO`、`AttackNodeSO` | C2 运行时完全依赖 F4 schema 解析连招树，无 SO 则无法初始化 |
| **C3** 心法构筑系统 | **硬依赖** | `PerkSO` | C3 从心法池加载、克隆、应用 PerkSO；无 PerkSO 则三选一无法运作 |
| **C5** 武器系统 | **硬依赖** | `WeaponSO`（含 `ComboGraphSO` 引用） | C5 从 WeaponSO 初始化武器实例；武器选择界面依赖 WeaponSO 列表 |
| **FE1** 敌人 AI 系统 | **硬依赖** | `EnemyInfoSO` | FE1 从 EnemyInfoSO 初始化敌人属性和行为档案 |
| **F2** 音频时钟系统 | **软依赖** | `LevelConfigSO`（BGM Clip、BPM、拍号） | F2 接受注入的 BGM 参数，LevelConfigSO 由 F6 提供；F2 本身无直接加载 SO 的依赖 |
| **F5** 存档系统 | **硬依赖** | `PlayerInfoSO` schema | F5 持有 PlayerInfoSO 实例并负责序列化；PlayerInfoSO 字段变更须 F5 同步更新序列化逻辑 |
| **FE5** 局外成长系统 | **硬依赖** | `PlayerInfoSO`（读取功勋/升级状态）、`WeaponSO`（读取解锁列表） | FE5 读取 PlayerInfoSO 的功勋值和解锁状态，通过 F5 写入 |
| **FE2** 房间系统 | **软依赖** | `LevelConfigSO`（房间类型、关卡参数） | FE2 根据 roomType 路由房间流程 |

### 双向一致性注记

- **C2 GDD** 须引用 F4-S1 和 F4-S2 作为连招节点和连招图的数据接口来源
- **C3 GDD** 须引用 F4-S3（PerkSO schema）和 F4 的「规则 3：PerkSO 运行时克隆规则」
- **C5 GDD** 须引用 F4-S4（WeaponSO schema）并说明 `comboGraph` 字段的引用关系
- **FE1 GDD** 须引用 F4-S5（EnemyInfoSO schema）和 `attackWindupBeats` 字段作为 C4 令牌分配的输入
- **F5 GDD** 须引用 F4-S7（PlayerInfoSO schema）并声明该 schema 变更会触发序列化迁移
- **F6 GDD** 须说明向 F2 注入 `LevelConfigSO` 数据的时机和职责

---

## Tuning Knobs

F4 是 schema 定义层，其「可调参数」是各 SO 类型中的**数值约束范围**和**枚举集合**——调整这些值等价于扩展或收窄游戏设计空间。

| 参数 | 默认值/当前值 | 安全范围 | 影响的游戏体验 | 说明 |
|------|------------|---------|-------------|------|
| `ComboGraphSO.nodes` 最大节点数 | 20 | [4, 40] | 连招树复杂度天花板；超过 20 个节点时设计可读性下降 | 增大上限允许更深的连招分支，但 Editor 预览和 QA 测试成本线性增加 |
| `PerkSO.parameters` 最大条目数 | 8 | [1, 16] | 单个心法的效果参数复杂度；超过 8 条时 UI 描述文本难以展示 | 心法效果越复杂，可读性越低；建议 MVP 阶段保持 ≤ 4 条 |
| `AttackNodeSO.cancelWeight` 精度 | float [0.0, 1.0] | 设计值 {0.0, 0.3, 0.5, 0.7, 1.0} | 取消窗口的精细度；过细导致手感差异不可感知 | 建议设计师使用离散取值而非任意 float，以便快速 A/B 测试 |
| `WeaponSO.baseDamage` 范围 | [1.0, 100.0] | MVP 建议 [10.0, 40.0] | 武器基础伤害差异；范围过大导致弱武器被废弃 | MVP 阶段苗刀参考值 25.0；后续武器调整时须同步更新 `EnemyInfoSO.baseHealth` |
| `EnemyInfoSO.baseHealth` 范围 | [1, 9999] | MVP 普通敌人 [50, 200] | 战斗时长；过高 HP 导致单房间战斗超过 90 秒 | MVP 阶段普通敌人参考值 80；精英敌人参考值 200 |
| `EnemyInfoSO.attackWindupBeats` 范围 | [0.5, 8.0] | MVP 普通敌人 [1.0, 4.0] | 敌人攻击「预读难度」；0.5 拍前摇是极限反应时间（BPM=120 时 = 250ms） | BPM=120 下 1 拍 = 500ms，多数玩家可反应下限；精英/Boss 可使用 0.5~1.0 拍增加压迫感；MVP 阶段普通敌人建议 ≥ 2.0 拍 |
| `LevelConfigSO.bpm` 设计范围 | MVP 入口房 100 | [80, 240]（F2 硬限） | 节奏紧张感；BPM > 180 时 half_beat_interval < 167ms，Perfect 窗口极窄 | **F4 定义数据字段**，F2 负责运行时 clamp 与兜底规则（BPM=0 → 120，超界 → clamp）；BPM 递进曲线由关卡设计（未来 GDD）规定 |
| `WeaponSO.perfectWindowModifier` 范围 | 1.0 | [0.5, 2.0] | 各武器 Perfect 难度差异；<0.8 时窗口极严，>1.5 时窗口过宽降低技能上限 | MVP 阶段所有武器默认 1.0；苗刀等入门武器可设 1.2 降低学习曲线；C1 将此值与基准 Perfect 窗口相乘 |
| `PerkRarity` 权重分配 | Common:60%, Uncommon:30%, Rare:8%, Legendary:2% | 总和 = 100% | 三选一构筑多样性；Legendary 太高导致每局 build 趋同 | 权重参考值，最终数值在 C3 GDD 中精确定义；F4 只定义枚举，不持有权重 |

---

## Visual/Audio Requirements

F4 是纯数据定义层，无直接视觉或音频资产需求。所有 SO 类型在运行时不触发任何视觉/音效输出。SO 资产的图标（如 `PerkSO.iconSprite`）作为数据字段存在，视觉规格由 P2（心法选择 UI）GDD 定义。

---

## UI Requirements

F4 无运行时 UI。

唯一与 UI 相关的 Editor 需求：所有 F4 SO 类型须在 Unity Inspector 中通过 Odin Inspector 的自定义绘制展示字段验证状态（通过/失败标识），供设计师在 Editor 中一眼确认资产完整性，不进入正式构建。

---

## Acceptance Criteria

F4 的验收标准按 **BLOCKING**（须自动化测试通过，Done 前必须全部绿）和 **ADVISORY**（Odin Validator / CI / 手动检查，ADVISORY 失败不阻止 Done 但须记录）两个门控级别组织。

---

### BLOCKING — Unity Test Runner（EditMode / PlayMode）

**AC-F4-02a 只读资产：非 PerkSO 类型序列化字段不通过反射被改写（EditMode）**
- Given 通过 `AssetDatabase.LoadAssetAtPath` 加载一个 `AttackNodeSO` 资产实例；用反射遍历所有可序列化字段（`BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance`），对每个字段记录当前值快照 `fieldSnapshot`（仅记录 float/int/string 基础类型字段）
- When 对每个字段调用 `SetValue` 写入一个与原值不同的测试值，然后再次反射读取同一字段值
- Then 逐字段对比写入后的值与 `fieldSnapshot`：若**任何字段的写入后读取值与快照不同**（说明字段可被反射改写且无保护），测试失败并报告字段名；测试结束时调用 `AssetDatabase.DiscardAllChanges()` 确保资产不落盘。**⚠️ 注意**：`EditorUtility.IsDirty()` 在无 `Undo.RecordObject()` 注册时对反射写入始终返回 false，不得用于此验证——须以字段快照对比作为唯一检测手段
- **实现意图**：确认 F4 SO 类型的序列化字段均不存在「无保护的公开写入路径」（测试不需要 C2/C5 存在即可运行）
- 测试位置：`Assets/Tests/EditMode/F4DataLayer/SO_ImmutabilityTest.cs`

**AC-F4-02b 只读资产：真实游戏代码路径不修改原始资产（PlayMode 集成）**
- **前提条件**：须在 C2（连招图系统）和 C5（武器系统）实现后方可执行
- Given 一个 `AttackNodeSO` 原始资产已加载，测试通过反射记录所有 float/int/string 字段快照 `fieldSnapshot`
- When PlayMode 运行进入第一个战斗房间并触发至少一次攻击判定（真实 C2 代码路径）
- Then 攻击判定后，重新读取同一 SO 实例的相同字段并与 `fieldSnapshot` 逐字段对比，所有字段值 delta = 0
- 测试位置：`Assets/Tests/PlayMode/F4DataLayer/SO_ImmutabilityIntegrationTest.cs`

**AC-F4-03 PerkSO：Instantiate 克隆不修改原始资产（EditMode）**
- Given 通过 `ScriptableObject.CreateInstance<PerkSO>()` 创建原始 PerkSO 实例，设置 `parameters[0].value = 1.0f`；**前置断言**：`Assert.IsTrue(typeof(PerkParameter).IsValueType, "PerkParameter must remain a [Serializable] struct — changing to class breaks clone isolation")`
- When 调用 `Instantiate(originalPerkSO)` 得到克隆体，执行以下**两条**突变路径：
  - 路径 A（槽位替换）：`clone.parameters[0] = new PerkParameter { value = 99.0f, key = clone.parameters[0].key, ... }`
  - 路径 B（struct 字段原地修改）：`var p = clone.parameters[0]; p.value = 88.0f; clone.parameters[0] = p;`（仅当 PerkParameter 是 struct 时路径 B 才能验证值拷贝语义；若 PerkParameter 被错误改为 class，路径 B 修改克隆槽位不影响原始资产——此路径专门检测此退化情况）
- Then `Object.ReferenceEquals(originalPerkSO, clone)` == false；路径 A 执行后 `originalPerkSO.parameters[0].value` 仍为 1.0f；路径 B 执行后 `originalPerkSO.parameters[0].value` 仍为 1.0f（两路径均须通过，任意一路径失败则测试失败）
- 测试位置：`Assets/Tests/EditMode/F4DataLayer/PerkSO_CloneImmutabilityTest.cs`

**AC-F4-04 PerkSO 克隆：Instantiate 为 3 个候选心法各产生独立副本（EditMode）**
- Given 3 个 PerkSO 原始资产（N=3，对应三选一每次提供的候选数量）
- When 对每个原始资产调用 `Instantiate(perkSO)` 得到 3 个克隆体
- Then 克隆体数量 = 3；每个克隆体与对应原始资产 `ReferenceEquals` = false；每个克隆体的 `id`、`rarity`、`effectType` 字段值与原始一致；修改克隆体 `parameters[0].value` 后，原始资产 `parameters[0].value` 不变（验证 struct 值语义）
- 测试位置：`Assets/Tests/EditMode/F4DataLayer/PerkSO_CloneLifecycleTest.cs`

**AC-F4-05 PerkSO 克隆：Destroy 后 C3 注册表清空** ⟶ *ADVISORY（迁移至 C3 GDD）*
- **注**：此 AC 测试 C3 系统的克隆生命周期管理，不属于 F4 schema 验证范畴，须在 C3 GDD 中重新定义为 BLOCKING AC。F4 层面的 Instantiate+Destroy 原子行为已由 AC-F4-03/04 覆盖。`UnityEngine.Object.FindObjectsByType<PerkSO>()` 对 ScriptableObject 无效（SO 不存在于场景中，Destroy 后立即返回 0），不得用于此验证——**须以 C3 自持的 `_activeClones` 注册表作为唯一验证手段**，并添加前置断言确认注册表在测试期间曾注册 ≥1 个克隆（防止 C3 从未克隆时测试形成永真通过）。
- Given C3 通过 `_activeClones` 注册表跟踪所有局内 PerkSO 克隆体，局内已克隆 ≥1 个（前置断言：`Assert.IsTrue(cloneCountDuringRun >= 1, "No clones registered — test is vacuous")`）
- When 局结束事件触发，C3 完成清理逻辑
- Then `_activeClones.Count == 0`
- 测试位置：`Assets/Tests/PlayMode/C3PerkSystem/PerkCloneCleanupTest.cs`（C3 GDD 中定义）

**AC-F4-12 ComboGraphSO.rootNodeIndex 越界：运行时 clamp** ⟶ *ADVISORY（迁移至 C2 GDD）*
- **注**：此 AC 测试 C2 系统的运行时 clamp 行为，而非 F4 schema 本身；F4 层面由 Odin Validator（AC-F4-11）在构建期阻止越界值进入运行时。本 AC 降为 ADVISORY 集成烟测，须在 C2 GDD 中重新定义为 BLOCKING AC。
- Given `rootNodeIndex=5`，`nodes.Count=3`
- When C2 系统运行时读取 rootNodeIndex
- Then 值被 clamp 到 0；Console 输出 LogError 含 "rootNodeIndex out of range, clamped to 0"

**AC-F4-14 PerkParameter.value 超范围：运行时 clamp** ⟶ *ADVISORY（迁移至 C3 GDD）*
- **注**：此 AC 测试 C3 系统的运行时 clamp 行为，而非 F4 schema 本身；F4 层面由 Odin Validator 在构建期检测 value 超范围（AC-F4-13 覆盖必填字段）。本 AC 降为 ADVISORY，须在 C3 GDD 中重新定义为 BLOCKING AC。
- Given `parameters[0].value=2.5, minValue=0.5, maxValue=2.0`
- When C3 系统应用该 parameter
- Then C3 将 value clamp 到 2.0；输出 LogWarning 含 "PerkParameter value clamped"

**AC-F4-19 PlayerInfoSO.unlockedWeaponIds：首元素固定为 weapon_miaodao** ⟶ *ADVISORY（迁移至 F5 GDD）*
- **注**：此 AC 测试 F5 存档系统的新存档初始化业务规则（新玩家首次存档时确保苗刀在解锁列表首位），不属于 F4 schema 验证范畴。F4 层面的职责是声明 `unlockedWeaponIds` 字段的约束（非空、长度 [1,10]），已由 AC-F4-17 的 Odin 字段约束覆盖。此 AC 须在 F5 GDD 中重新定义为 BLOCKING AC（在 SaveService 新存档创建逻辑的单元测试中验证），并明确初始化责任方为 F5 `SaveService.CreateNewSave()`。
- Given F5 `SaveService.CreateNewSave()` 被调用
- When 创建完成后读取 `PlayerInfoSO.unlockedWeaponIds`
- Then `unlockedWeaponIds[0] == "weapon_miaodao"`；`unlockedWeaponIds.Count >= 1`
- 测试位置：`Assets/Tests/EditMode/F5SaveSystem/SaveService_NewSaveTest.cs`（F5 GDD 中定义）

**AC-F4-22 重复 PerkSO id：运行时后者覆盖前者并记录错误** ⟶ *ADVISORY（迁移至 C3 GDD）*
- **注**：此 AC 测试 C3 心法池注册行为，而非 F4 schema；F4 层面的重复 ID 检测由 Odin Validator 在构建期执行（见 AC-F4-24）。本 AC 须在 C3 GDD 中重新定义为 BLOCKING AC。
- Given 两个 PerkSO 资产 id 均为 `"perk_haste"`（绕过 Validator 进入运行时的容灾场景）
- When C3 构建心法池注册两个资产
- Then 后加载者覆盖前者；Console 输出 LogError 含 "Duplicate perk id detected"

**AC-F4-08 Odin Attribute 仅在 Editor 程序集（⚠️ BLOCKING — 违反导致非 Editor 平台构建失败）**
- **严重性说明**：`using Sirenix.*` 出现在 Runtime 程序集是**编译错误**，不是风格问题。Odin Inspector/Validator 程序集不打入 Player Build，Runtime 代码引用将导致非 Editor 平台（iOS/Android/Console）构建失败，属硬性构建门控。
- Given 项目启动 Player Build 流程
- When 执行以下两项静态验证：(1) `grep -rn "using Sirenix" Assets/Scripts/` 排除 `Assets/Editor/` 目录——预期 0 条匹配（**注**：不使用 `^` 行首锚点，以覆盖 `#if UNITY_EDITOR` 块内有前导空格的缩进 using 语句）；(2) 验证所有 F4 SO 类 `.cs` 文件的 asmdef 引用中无 `com.sirenix.odinspector.editor` 依赖——预期所有引用均指向 Runtime asmdef
- Then 两项检查均通过；如发现 Runtime 代码引用 Sirenix 命名空间，立即停止构建并报告 Error，注明违规文件路径

---

### ADVISORY — Odin Validator / CI / 手动检查

**AC-F4-01 SO 类型完整性**
- Given CI 执行静态扫描
- When 执行 `grep -rl "class AttackNodeSO\|class ComboGraphSO\|class PerkSO\|class WeaponSO\|class EnemyInfoSO\|class LevelConfigSO\|class PlayerInfoSO" Assets/Scripts/` 并统计匹配文件数
- Then 恰好返回 7 条文件路径（每个 F4 SO 类型对应一个 `.cs` 文件）；任何新增或删除都须更新此检查中的类名列表

**AC-F4-06 Schema 变更触发下游审核** *(过程规则 — 非 QA 可自动验证项，应移至 PR checklist 模板)*
- Given 开发者对任意 F4 SO 类型新增/修改/删除字段
- When 变更提交 PR
- Then PR 描述明确列出受影响下游 GDD；含"版本兼容性"条目说明是否为 breaking change
- **注**：此项为人工流程检查，无法由 QA 或 CI 自动验证。建议在 `.github/pull_request_template.md` 中添加对应 checkbox，或实现 CI hook 检测 F4 SO 文件变更后请求人工确认，而非将其列为可验收 AC

**AC-F4-07 Odin Validator：CI 中自动运行并阻断 Error**
- Given CI 构建已配置 Odin Validator 步骤
- When 构建执行
- Then 日志含 "Odin Validator scan complete"；任何 Error 级条目导致构建返回非零退出码

**AC-F4-09 Addressables：所有 SO 不在 Resources/**
- Given 项目已配置 Addressables
- When 审查 Addressables Groups 窗口并 Glob 扫描 `Assets/Resources/`
- Then 全部 7 类 SO 资产均归属至少一个 Addressable Group；`Assets/Resources/` 中无任何 F4 SO 资产

**AC-F4-10 AttackNodeSO 字段约束**
- Given `id`=""、`animationClip`=null、`hitboxStartNormalizedTime`=1.1（超过上限 1.0）、`cancelWeight`=1.5（超过上限 1.0）、`authoredBpm`=0（除零风险，ADR-D5）
- When Odin Validator 扫描
- Then 报告 5 条 Error；构建阻止

**AC-F4-11 ComboGraphSO.nodes 为空时阻止构建**
- Given `nodes` 为空列表（长度 = 0）
- When Odin Validator 扫描
- Then 报告 Error "nodes 列表不得为空"；构建阻止

**AC-F4-13 PerkSO 必填字段验证**
- Given `displayName`=""、`iconSprite`=null、`descriptionTemplate`=""
- When Odin Validator 扫描
- Then 报告 3 条 Error；构建阻止

**AC-F4-15 WeaponSO：weaponType 与 comboGraph.weaponType 一致**
- Given `WeaponSO.weaponType=MiaoDao`，`comboGraph.weaponType=Spear`
- When Odin Validator 扫描
- Then 报告 Error "weaponType 不匹配"；构建阻止

**AC-F4-16 EnemyInfoSO 数值范围**
- Given `baseHealth`=0、`baseDamage`=1000（超过 999.0 上限）、`attackWindupBeats`=0.4（低于 0.5 最小值）、`rewardMeritBase`=101（超过 100 上限）
- When Odin Validator 扫描
- Then 报告 4 条 Error；构建阻止

**AC-F4-17 LevelConfigSO.bpm 下界**
- Given `bpm`=79（低于最小值 80）
- When Odin Validator 扫描
- Then 报告 Error；`bpm`=80 时通过

**AC-F4-18 5/8 拍号：分母必须为 8**
- Given `timeSignatureNumerator`=5，`timeSignatureDenominator`=4
- When Odin Validator 扫描
- Then 报告 Error；改为 `timeSignatureDenominator`=8 后通过

**AC-F4-20 PlayerInfoSO 无 Unity Object 引用**
- Given PlayerInfoSO 中声明了 `GameObject` 等 UnityEngine.Object 子类字段
- When Odin Validator 扫描
- Then 报告 Error "含有 Unity Object 引用"；构建阻止

**AC-F4-21 PlayerInfoSO 数值字段范围**
- Given `latencyCalibrationOffsetMs`=-201、`audioVolumeMaster`=1.1
- When Odin Validator 扫描
- Then 报告 2 条 Error；构建阻止

**AC-F4-24 重复 PerkSO id：Validator 报 Error 并阻止构建**
- Given 项目中两个 PerkSO id 均为 `"perk_haste"`
- When Odin Validator 扫描
- Then 报告 **Error**（不得为 Warning——重复 ID 会导致 C3 心法池行为不确定，属硬性缺陷）；构建阻止；Error 消息注明两个冲突资产的路径

**AC-F4-23 WeaponSO.comboGraph 不得为 null**
- Given `comboGraph`=null
- When Odin Validator 扫描
- Then 报告 Error "comboGraph 不得为 null"；构建阻止

**AC-F4-25 AttackNodeSO 跨字段约束：hitboxStart + hitboxDuration ≤ 1.0**
- Given `hitboxStartNormalizedTime=0.8`，`hitboxDurationNormalized=0.3`（0.8 + 0.3 = 1.1，超出动画范围）
- When Odin Validator 扫描
- Then 报告 Error "hitboxStartNormalizedTime + hitboxDurationNormalized 超出 1.0"；构建阻止
- 对比：`hitboxStartNormalizedTime=0.7, hitboxDurationNormalized=0.3`（0.7 + 0.3 = 1.0，边界合法值）须通过验证无 Error
- **注**：此约束须在 RegisterValidator 外部验证类中实现（需同时读取两个字段值），不能仅用单字段 `[MaxValue]` Attribute 覆盖

**AC-F4-26 重复 AttackNodeSO.id：Validator 报 Error 并阻止构建** *(ADVISORY)*
- Given 项目中两个 AttackNodeSO 的 `id` 均为相同字符串（如 `"atk_slash_01"`）
- When RegisterValidator 执行 `AssetDatabase.FindAssets("t:AttackNodeSO")` 并比较所有 id 字段
- Then 报告 Error，注明冲突资产路径；构建阻止
- **背景**：重复 AttackNodeSO.id 会导致 ComboGraphSO 节点引用歧义，C2 行为不确定

**AC-F4-27 重复 ComboGraphSO.id：Validator 报 Error 并阻止构建** *(ADVISORY)*
- Given 项目中两个 ComboGraphSO 的 `id` 均为相同字符串
- When RegisterValidator 执行 `AssetDatabase.FindAssets("t:ComboGraphSO")` 并比较所有 id 字段
- Then 报告 Error，注明冲突资产路径；构建阻止

**AC-F4-28 重复 WeaponSO.id：Validator 报 Error 并阻止构建** *(ADVISORY)*
- Given 项目中两个 WeaponSO 的 `id` 均为相同字符串
- When RegisterValidator 执行 `AssetDatabase.FindAssets("t:WeaponSO")` 并比较所有 id 字段
- Then 报告 Error，注明冲突资产路径；构建阻止
- **背景**：重复 WeaponSO.id 会导致 PlayerInfoSO.unlockedWeaponIds 解析到错误资产，存档静默损坏

**AC-F4-29 重复 EnemyInfoSO.id：Validator 报 Error 并阻止构建** *(ADVISORY)*
- Given 项目中两个 EnemyInfoSO 的 `id` 均为相同字符串
- When RegisterValidator 执行 `AssetDatabase.FindAssets("t:EnemyInfoSO")` 并比较所有 id 字段
- Then 报告 Error，注明冲突资产路径；构建阻止

**AC-F4-30 重复 LevelConfigSO.id：Validator 报 Error 并阻止构建** *(ADVISORY)*
- Given 项目中两个 LevelConfigSO 的 `id` 均为相同字符串
- When RegisterValidator 执行 `AssetDatabase.FindAssets("t:LevelConfigSO")` 并比较所有 id 字段
- Then 报告 Error，注明冲突资产路径；构建阻止

**AC-F4-31 ComboEdge 索引越界：Validator 报 Error 并阻止构建** *(BLOCKING)*
- Given `ComboGraphSO.nodes` 含 3 个节点，某 `ComboEdge.fromNodeIndex = 3`（合法范围 0–2）
- When RegisterValidator 执行跨字段检查：对每条 edge，验证 `fromNodeIndex` 和 `toNodeIndex` 均 < `nodes.Count`
- Then 报告 Error "ComboEdge.fromNodeIndex 超出 nodes 范围"；构建阻止
- **背景**：C2 运行时直接使用 `nodes[edge.fromNodeIndex]` 索引，越界导致 IndexOutOfRangeException 崩溃
- **注**：C2 GDD 须额外添加 BLOCKING 运行时测试覆盖此路径（Validator 仅覆盖资产创作期）

**AC-F4-32 hitboxDurationNormalized 须严格大于 0** *(BLOCKING)*
- Given `hitboxDurationNormalized = 0.0`
- When Odin Validator 扫描
- Then 报告 Error "hitboxDurationNormalized 须 > 0；当前值 0.0 表示零持续时长 hitbox，攻击判定永远不触发"；构建阻止
- 对比：`hitboxDurationNormalized = 0.01`（最小有效持续时长）须通过验证无 Error
- **背景**：0.0 值合法通过 `[MinValue(0)]` 约束，但会导致攻击静默失效；须以开区间 (0, 1] 替代闭区间 [0, 1]

---

## Open Questions

- **`PerkEffectType` 枚举最终值**：当前为候选列表（`DamageModifier, ComboModifier, RhythmRuleModifier, MovementModifier, MetaModifier`），须 C3 GDD 完成后确认并锁定。F4 的 `PerkSO.effectType` 字段类型依赖此枚举定义。

- **`WeaponSO.animatorLayerName` 命名规范**：Animancer 层名称格式须 F6（TDE 集成层）GDD 与 C5（武器系统）GDD 联合定义，F4 只持有字段不规定命名格式。F6/C5 GDD 锁定命名规范后，须在 F4 的 RegisterValidator 外部验证类中新增 `[ValidateInput]` 规则检查字段值是否符合约定格式（否则该字段为无验证的自由字符串，运行时静默失效风险）。

- **Addressables Group 分组策略**：7 类 SO 的 Bundle 分组（按系统分组 vs 按加载时机分组）由 F6 GDD 决定。F4 已明确：大型资产引用（`animationClip`、`iconSprite`）使用 `AssetReferenceT<T>` 延迟引用，`bgmAssetRef` 为 FMOD 资产路径（见规则 6、ADR-D6）；`List<AttackNodeSO>` 保留直接引用（元数据小型 SO）。F6 在分组时须考虑这一 Bundle 拓扑。

- **`EnemyBehaviorProfile` 枚举扩展性**：当前候选值 `{Melee_Simple, Ranged_Simple, Elite, Boss}` 是否足够 MVP？FE1 GDD 设计期间若需新增档案类型，须同步更新 F4-S5 并触发 Rule 4 变更传播。

- **C3 GDD 须决策的 PerkSO 行为扩展**（传递至 C3 作者）：以下功能在 F4 schema 层面暂不添加字段，待 C3 设计 Perk 执行模型后确定是否需要 F4 新增字段（通过 Rule 4 变更传播更新）：
  - `stackResetCondition`：连续事件重置条件（如「Miss 时重置叠加层」）；若需数据驱动须在 PerkSO 新增字段
  - `activateOnMaxStacks: bool`：「达到最大叠加层时触发爆发」语义；若需数据驱动须新增字段
  - `buildTags`：构筑主题标签（供 C3 三选一时向现有 build 倾斜权重）；可在 C3 设计时确定
  - `exclusivePerkIds: List<string>`：互斥心法 ID 列表（防止矛盾组合）；可在 C3 设计时确定
  - **Legendary 旱期风险**：当前 Legendary 2% 概率下，单次完整游戏局次 61.5% 概率无 Legendary 出现。C3 GDD 须评估是否引入 Pity 计数器或保底机制，以确保 Pillar 4（构筑驱动复盘）的最高表达力层次对玩家可见。
  - **[Rule 4 传递] `effectScope` 字段缺失**：当前 PerkSO 无法区分 Perk 效果作用于自身、当前目标还是范围——C3 执行系统须先决定此语义，再评估是否需要 F4 新增 `effectScope: PerkScope` 枚举字段（`Self / SingleTarget / AreaOfEffect`）。
  - **[Rule 4 传递] `effectDurationType` 字段缺失**：`PerkParameter.value` 的作用持续时间（永久 vs 计时 vs 单次）在 F4 中未定义。若 C3 需要区分「永久增益 Perk」与「限时 buff Perk」，须由 F4 新增 `durationType: PerkDuration` 枚举及 `durationValue: float` 字段。
  - **[Rule 4 传递] 复合效果缺失**：当前 PerkSO 每个 Perk 只能携带一条 `effectType`（单一效果）。若 C3 设计需要一张心法同时修改多个系统参数（如「Perfect 后：+20% 攻击 AND 下次连招起手节奏加宽」），须 F4 将 `effectType + parameters` 改为 `List<PerkEffect>` 复合效果列表。此为 schema 层 breaking change，须走 Rule 4 全量下游审查。
  - **[Rule 4 传递] `PerkTrigger` 枚举扩展**：当前权威枚举值（见 F4-S3 表定义）为 `{Passive, OnPerfect, OnGood, OnMiss, OnComboEnd}`。以下为**扩展候选（非当前定义，待 C3 裁定后通过 Rule 4 更新）**：`OnHit`（任意命中触发）、`OnKill`（击杀触发）、`OnBeat`（任意拍点触发，不限 Perfect/Good）、`OnComboStart`（连招起手触发）、`OnComboBreak`（连招断链触发，用于惩罚/补偿机制）、`OnBeatWindow`（任意节拍窗口开启时触发）。C3 GDD 须确认触发点完整性后回传 F4，更新 F4-S3 表枚举值并触发 RegisterValidator 规则扩展。
  - **[Rule 4 传递] `PerkParameter` 类型单一**：当前 `PerkParameter.value: float` 无法携带整型计数参数（如「最大叠加层数是整数，不是浮点」）或布尔开关（如「是否无视防御」）。若 C3 需要强类型参数，须 F4 将 `value: float` 改为 `union` 或扩展为 `floatValue/intValue/boolValue` 三字段结构（此为 breaking change）。替代方案：保持 float 并在注释中约定 int 用 `Mathf.RoundToInt` 读取，由 C3 GDD 正式裁定。

- **C2 GDD 须决策的 ComboGraphSO 行为扩展**（传递至 C2 作者）：
  - `comboResetTimeoutBeats`：玩家在取消窗口内无操作时，C2 重置到 `rootNodeIndex` 的等待时长。若为武器差异化参数须加入 `WeaponSO`/`ComboGraphSO`；若为全局常量须注册到 `entities.yaml`
  - `ComboEdge.requiredMinimumRating`：节奏精度门控连招分支（如「仅 Perfect 可进入 HeavyFinisher」）。若支持此设计，C2 实现后须由 F4 通过 Rule 4 在 ComboEdge struct 中新增 `requiredMinimumRating: RatingType` 字段

- **FE5 GDD 须定义功勋消耗汇（Merit Sink）**（传递至 FE5 作者）：`PlayerInfoSO.playerMerit` 字段当前为纯来源——所有 GDD 中均无消耗目的地定义。FE5 必须在其 GDD 中定义 Meta 升级成本曲线（Sink），否则功勋无上限累积，Pillar 4（构筑驱动复盘）将失去进程摩擦力。建议 FE5 在设计阶段同步注册 `meta_upgrade_cost` 常量至 `entities.yaml`。

- **`MetaUpgradeEntry.id` 唯一性约束**：`PlayerInfoSO.metaUpgradeLevels` 列表中当前无 ID 唯一性检测（与 `PerkSO.id` 有 AC-F4-24 保护不同）。重复 ID 条目可能导致 FE5 读取升级状态时叠加错误（level 超过 maxLevel）。须在 RegisterValidator 外部验证类中新增全局扫描规则检测 `metaUpgradeLevels` 内 ID 重复，并补充对应 ADVISORY AC。

- **`unlockedWeaponIds` 上限与 WeaponType 枚举不一致**：`unlockedWeaponIds` 最大长度为 10，而 `WeaponType` 枚举当前仅有 3 值 `{MiaoDao, Spear, Sword}`。上限 10 隐含扩展至 10 把武器的内容路线图，但未在任何 GDD 中正式承诺。FE5 GDD 在设计武器解锁成本曲线时须参照实际计划武器数量，并与此上限对齐（或在 MVP 阶段将上限改为 3 并添加扩展注释）。
