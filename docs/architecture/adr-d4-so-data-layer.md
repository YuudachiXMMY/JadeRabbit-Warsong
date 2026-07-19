# ADR-D4: SO Data Layer Architecture

## Status
Proposed

## Date
2026-07-19

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (6000.3.19f1) |
| **Domain** | Core / Scripting + Asset Management |
| **Knowledge Risk** | LOW — ScriptableObject API 自 Unity 2020 以来稳定；2 项 post-cutoff 变更已验证（见下） |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`; `docs/engine-reference/unity/breaking-changes.md`; `docs/engine-reference/unity/plugins/odin.md`; `docs/engine-reference/unity/plugins/addressables.md`; `docs/engine-reference/unity/plugins/easysave-3.md`; `docs/engine-reference/unity/plugins/animancer.md`; engine-specialist review 2026-07-19 |
| **Post-Cutoff APIs Used** | `AssetReferenceT<T>` (Addressables 2.x，稳定 API，Unity 6.3 已验证); `[NonSerialized]` 在 ScriptableObject 公开字段上（Unity 6.3 行为已验证） |
| **Verification Required** | (1) `[SerializeField]` 仅用于字段（非属性）——Unity 6.3 编译期强制，实现时须 lint 检查；(2) Addressables 加载失败现抛异常（Unity 6.2+），持有 `AssetReferenceT<T>` 的所有 `LoadAssetAsync` 调用须包裹 try/catch；(3) EasySave 3 不直接序列化 `PlayerInfoSO` 实例（须通过 DTO 提取——见 ADR-D7 验证要求 3） |

> **Engine Specialist Review (2026-07-19)**: CONCERNS — 无 BLOCKING 问题，5 项 CONCERN 已并入本 ADR 解决。
> - C1: AnimationClip 加载合约未声明所有者 → 本 ADR 已明确：C2 在房间加载时预加载，退出时释放
> - C2: Addressables 分组同捆策略需显式化 → 本 ADR 已明确共捆约束，详细分组延至 F6 GDD
> - C3: PlayerInfoSO 禁止被 EasySave 3 直接序列化 → 本 ADR 已新增明确约束并交叉引用 ADR-D7
> - C4: PerkParameter struct 字段须为值类型 → 本 ADR 已添加不变量
> - C5: `bgmAssetRef` 命名含 "Ref" 误导类型 → 本 ADR 已重命名为 `bgmFilePath`

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-D6 (Accepted) — `LevelConfigSO.bgmFilePath` 格式由 FMOD Core API 决定（StreamingAssets 相对路径）; ADR-D5 (Accepted) — `AttackNodeSO.animationClip` 须为 `AssetReferenceT<AnimationClip>`（Animancer HybridAnimancerComponent 攻击层驱动）; ADR-D1 (Accepted) — TDE 边界合约（F6 负责向 F2 注入 LevelConfigSO 参数） |
| **Enables** | C2 GDD（连招图系统，消费 ComboGraphSO/AttackNodeSO schema）; C3 GDD（心法构筑系统，消费 PerkSO schema + 克隆规则）; C5 GDD（武器系统，消费 WeaponSO schema）; FE1 GDD（敌人 AI，消费 EnemyInfoSO schema） |
| **Blocks** | C2 实现 sprint（须 AttackNodeSO/ComboGraphSO schema 锁定后方可开始）; C3 实现 sprint（须 PerkSO schema + PerkEffectType 枚举锁定后方可开始）; FE1 实现 sprint（须 EnemyInfoSO schema 锁定后方可开始） |
| **Ordering Note** | 须先于 C2/C3/C5/FE1 GDD 完成。F4 GDD（In Review）与本 ADR 并行——本 ADR 锁定 schema 架构，F4 GDD 锁定字段细节。F6 GDD 须在本 ADR 后定案 Addressables 分组策略（含 AttackNodeSO 同捆约束）。 |

---

## Context

### Problem Statement

《玉兔：破阵》的 7 个核心系统（C2 连招图、C3 心法构筑、C5 武器、FE1 敌人 AI、F2 音频时钟、F5 存档、FE2 房间系统）各自需要访问不同类型的游戏配置数据。若无统一的数据契约层，每个系统将独立定义数据结构、加载逻辑和验证规则，导致：

- 7+ 套不兼容的数据格式（无法跨系统复用）
- 无编辑器验证——错误配置在运行时才被发现
- 设计师无法不改代码就调整游戏参数（违反「快速迭代手感」设计原则）
- 支柱四（构筑驱动复盘）的 PerkSO 表达能力直接受制于 schema 设计质量

本 ADR 决定如何构建统一的 ScriptableObject 数据层（System F4），为所有下游系统提供一致的数据访问接口和编辑器工作流。

### Constraints

- **TDE 禁止修改约束**（ADR-D1）：LevelConfigSO 数据须通过 F6 TDELifecycleAdapter 注入 F2，不得绕过 TDE 生命周期
- **FMOD Core API 约束**（ADR-D6）：LevelConfigSO 的 BGM 参数须以 FMOD `System.createSound(path)` 加载，不使用 Addressables/AudioClip
- **Animancer 攻击层约束**（ADR-D5）：AttackNodeSO 的动画 Clip 须以 `AssetReferenceT<AnimationClip>` 延迟引用，由 C2 在房间加载时预解析后传入 `AnimancerAttackPlayer.Play(loadedClip)`
- **EasySave 3 约束**（ADR-D7）：PlayerInfoSO 须符合 EasySave 3 序列化约束（无 UnityEngine.Object 子类字段，无 Dictionary 字段）；禁止直接将 SO 实例传入 `ES3.Save()`
- **Odin Editor-Only 约束**：Odin Inspector/Validator 在打包时不进包体；Runtime 代码禁止 `using Sirenix.*`
- **Unity 6.3 [SerializeField] 约束**：`[SerializeField]` 只能用于字段，用于属性会触发 CS0592 编译错误
- **Addressables 加载异常约束**（Unity 6.2+）：`LoadAssetAsync()` 失败现在抛异常，所有加载调用须包裹 try/catch
- **目标平台**：PC/Console/Mobile；SO 加载性能须在 Mobile 帧预算（33ms）内完成

### Requirements

- 7 个核心 SO 类型须覆盖全部下游系统的数据需求（无硬编码游戏参数）
- 设计师须能在 Unity Inspector 中创建/编辑 SO 资产并获得实时字段验证反馈
- CI 构建须在发现非法 SO 资产配置时阻断（不静默通过）
- SO 资产在运行时须为只读（不污染资产文件）
- 新增一个心法须不修改 C# 代码（仅创建/编辑 PerkSO 资产）
- PerkSO 须支持局内克隆（多实例互相独立）

---

## Decision

使用 **Unity ScriptableObjects + Addressables + Odin Editor 验证**作为统一数据层，定义 7 个核心 SO 类型，遵循运行时只读原则，PerkSO 通过 `Instantiate()` 克隆支持局内临时修改。

### 核心架构规则

**Rule D4-1：运行时只读原则**
所有 SO 资产在游戏运行时为只读。任何运行时代码路径禁止修改 SO 资产文件的序列化字段。需要「局内临时修改数据」的系统必须先克隆目标 SO，再修改克隆体。违反此规则将导致 Play Mode 修改落入 Editor 资产（Unity ScriptableObject 特有陷阱），造成数据污染。

**Rule D4-2：PerkSO 是唯一可变模式**
`PerkSO` 是 7 类 SO 中唯一需要运行时修改的类型（心法叠加层计数、参数覆盖）。克隆规则由 C3（心法构筑系统）执行：
- 通过 `Instantiate(originalPerkSO)` 创建克隆体
- 克隆体在当局有效；局结束时 C3 销毁所有克隆体
- `PlayerInfoSO.unlockedPerkIds` 持有原始资产引用，不持有克隆体

**Rule D4-3：大型 Unity Object 引用须使用 AssetReferenceT<T>**
`AnimationClip`、`Sprite` 等大型 Unity Object 引用字段**必须**使用 `AssetReferenceT<T>`（Addressables 延迟引用），不使用直接引用。直接引用会将资产锁入同一 Bundle，SO 加载时触发隐式全量拉取（单个 ComboGraphSO 加载时可隐式拉取 20 个 AnimationClip）。

例外：`List<AttackNodeSO>`（小型元数据 SO）在 `ComboGraphSO` 中保留直接引用（见 Rule D4-6 同捆约束）。

**Rule D4-4：AnimationClip 加载合约（解决专家 C1）**
`AttackNodeSO.animationClip: AssetReferenceT<AnimationClip>` 的加载生命周期由 **C2（连招图系统）** 统一管理：
- C2 在房间加载阶段对当前 `ComboGraphSO` 的所有 `AttackNodeSO.animationClip` 调用 `LoadAssetAsync<AnimationClip>()`，完成后将 `AnimationClip` 传入 `AnimancerAttackPlayer.Play(loadedClip)` (ADR-D5 API)
- C2 在房间退出时对所有已加载句柄调用 `Addressables.Release(handle)`
- C2 在调用 `LoadAssetAsync` 时须包裹 try/catch（Unity 6.2+ 加载失败抛异常）
- **禁止**在攻击执行时（即时路径）触发 `LoadAssetAsync`（会导致首次攻击帧加载卡顿）

**Rule D4-5：Addressables 加载，禁止 Resources/**
全部 7 类 SO 资产须归入 Addressable Group，通过 Addressables API 加载。禁止使用 `Resources.Load()` 加载任何 F4 SO 资产。

**Rule D4-6：AttackNodeSO 同捆约束（解决专家 C2）**
`ComboGraphSO` 通过 `List<AttackNodeSO>` 持有对 `AttackNodeSO` 的直接引用。Addressables 处理直接引用的方式取决于分组：
- `AttackNodeSO` 资产须与 `ComboGraphSO` 归属**同一 Addressable Group**，避免 Bundle 重复或跨 Bundle 隐式依赖
- 具体 Group 名称、分组策略（按武器类型 vs 按加载时机）由 F6 GDD 定案
- F6 GDD 在定义分组时须遵守本约束：ComboGraphSO 与其所有直接引用的 AttackNodeSO 必须在同一 Group 内

**Rule D4-7：Odin 双模式验证（Editor-only）**
F4 使用两种互补验证机制，须同时实现：
- **方案 A（Inspector 内联 Attribute）**：在 SO 类文件中以 `#if UNITY_EDITOR` 块保护 `using Sirenix.OdinInspector;`，在字段上标注 `[Required]`、`[MinValue]`、`[MaxValue]`、`[ValidateInput]` 等 Attribute。提供设计师实时反馈，**不驱动 CI 阻断**。
- **方案 B（外部 Validator 类）**：在 Editor 专属程序集中使用 `[RegisterValidator(typeof(XxxSO))]` 外部验证类执行跨资产约束检查（重复 ID、索引越界、跨字段约束等）。**这是 CI 阻断的唯一驱动机制，必须实现**。
- **禁止**在任何 Runtime 代码中出现 `using Sirenix.*`（会导致非 Editor 平台构建失败，CS0592 或 TypeLoadException）

**Rule D4-8：PlayerInfoSO EasySave 3 序列化约束（解决专家 C3）**
- `PlayerInfoSO` 的字段只允许纯数据类型：`int`、`float`、`string`、`List<T>`、`[Serializable]` struct（无 UnityEngine.Object 子类引用，无 Dictionary）
- **禁止直接将 `PlayerInfoSO` 实例传入 EasySave 3**（即禁止 `ES3.Save("key", playerInfoSO)`）。F5 必须从 SO 中提取 DTO 后序列化 DTO（见 ADR-D7 & F5 GDD 的 DTO 映射表）
- `latencyCalibrationOffsetMs` 和 `audioVolumeMaster` 须标注 `[NonSerialized]`（运行时缓存字段，权威来源为 F5 DTO；F2 和 FE7 在 F5 加载完成后写入）

**Rule D4-9：PerkParameter struct 值类型不变量（解决专家 C4）**
`PerkParameter` 的所有字段**必须为值类型或 string**（`float`、`int`、`enum`、`string`）。禁止在 `PerkParameter` 中添加任何 C# 引用类型字段（class、数组对象、接口引用等）。原因：`Instantiate(perkSO)` 对序列化数据做深拷贝，但对非序列化的引用类型字段做浅拷贝——克隆体与原始资产将共享同一引用，克隆体的修改会污染原始资产。

### Architecture Diagram

```
Assets/Data/              ← Addressables 管理的 SO 资产文件
├── Attack/               ← AttackNodeSO 资产（与 ComboGraphSO 同 Group）
├── Combo/                ← ComboGraphSO 资产
├── Perks/                ← PerkSO 资产（原始资产，C3 在局前克隆）
├── Weapons/              ← WeaponSO 资产
├── Enemies/              ← EnemyInfoSO 资产
├── Levels/               ← LevelConfigSO 资产
└── Player/               ← PlayerInfoSO 资产（单例风格）

Runtime 访问路径：
  F6（房间加载）→ Addressables.LoadAssetAsync<LevelConfigSO>()
                → F2.InitBeatClock(levelConfig.bpm, levelConfig.bgmFilePath)
  C2（房间加载）→ Addressables.LoadAssetAsync<ComboGraphSO>()
                → 对每个 AttackNodeSO.animationClip 调用 LoadAssetAsync<AnimationClip>()
                → AnimancerAttackPlayer.Play(loadedClip)
  C3（局开始）  → Addressables.LoadAssetAsync<PerkSO>()
                → Instantiate(perkSO)  ← 克隆，局内修改克隆体
                → 局结束时 Destroy(clone) + Addressables.Release(handle)

Editor 验证路径（不进 Player Build）：
  Inspector Attribute → 实时字段高亮（设计师 UX）
  Odin Validator CI  → RegisterValidator 外部类 → 阻断构建
```

### Key Interfaces

#### 7 个核心 SO 类型 schema（字段级 ADR 约束）

```csharp
// AttackNodeSO — 单个攻击节点
[CreateAssetMenu(fileName = "AttackNode", menuName = "JadeRabbit/Attack Node")]
public class AttackNodeSO : ScriptableObject {
    [SerializeField] private string _id;                              // 项目内唯一，格式：weapon_attackType
    [SerializeField] private AssetReferenceT<AnimationClip> _animationClip; // 延迟引用，C2 预加载
    [SerializeField] private float _hitboxStartNormalizedTime;        // [0.0, 1.0) 开区间上界
    [SerializeField] private float _hitboxDurationNormalized;         // (0.0, 1.0] 开区间下界（禁止 0.0）
    [SerializeField] private CancelWindowType _cancelWindowType;      // {None, Standard, Advanced}
    [SerializeField] private float _cancelWeight;                     // [0.0, 1.0]
    [SerializeField] private QuantizationHint _quantizationHint;      // {FullBeat, HalfBeat, Free}（可选）
    [SerializeField] private float _authoredBpm;                      // [60, 240]，禁止 0（除零）
}

// ComboGraphSO — 连招树容器
[CreateAssetMenu(fileName = "ComboGraph", menuName = "JadeRabbit/Combo Graph")]
public class ComboGraphSO : ScriptableObject {
    [SerializeField] private string _id;                              // 项目内唯一
    [SerializeField] private WeaponType _weaponType;                  // {MiaoDao, Spear, Sword}
    [SerializeField] private List<AttackNodeSO> _nodes;               // 直接引用（须同 Addressable Group）
    [SerializeField] private int _rootNodeIndex;                      // [0, nodes.Count-1]
    [SerializeField] private List<ComboEdge> _edges;                  // 边数 [0, 60]
}

// ComboEdge — 连招转换边（[Serializable] struct，C2 运行时遍历）
[Serializable]
public struct ComboEdge {
    public int fromNodeIndex;       // [0, nodes.Count-1]，禁止自环（from == to）
    public int toNodeIndex;         // [0, nodes.Count-1]
    public AttackInputType requiredInput;  // {Normal, Heavy, Special}；同起始节点禁止重复输入类型
}

// PerkSO — 心法（唯一可变 SO：C3 在局前克隆）
[CreateAssetMenu(fileName = "Perk", menuName = "JadeRabbit/Perk")]
public class PerkSO : ScriptableObject {
    [SerializeField] private string _id;                              // 项目内唯一
    [SerializeField] private string _displayName;                     // 非空
    [SerializeField] private PerkRarity _rarity;                      // {Common, Uncommon, Rare, Legendary}
    [SerializeField] private PerkEffectType _effectType;              // 枚举（C3 GDD 最终确认）
    [SerializeField] private List<PerkParameter> _parameters;         // 长度 [0, 8]；元素须为 [Serializable] struct
    [SerializeField] private PerkTrigger _triggerCondition;           // {Passive, OnPerfect, OnGood, OnMiss, OnComboEnd}
    [SerializeField] private int _maxStacks;                          // [1, 10]；Passive 须为 1
    [SerializeField] private StackBehavior _stackBehavior;            // {Additive, Capped, Replace}；Passive 须为 Capped
    [SerializeField] private string _descriptionTemplate;             // 含 {key} 占位符
    [SerializeField] private AssetReferenceT<Sprite> _iconSprite;     // 延迟引用
}

// PerkParameter — 效果数值参数（Rule D4-9：字段只允许值类型或 string）
[Serializable]
public struct PerkParameter {
    public string key;          // string（C# 不可变，Instantiate 安全）
    public float value;         // float（值类型，Instantiate 深拷贝）
    public float minValue;      // float
    public float maxValue;      // float
    // ⚠️ 禁止在此 struct 新增 class、数组对象、接口等引用类型字段（见 Rule D4-9）
}

// WeaponSO — 武器
[CreateAssetMenu(fileName = "Weapon", menuName = "JadeRabbit/Weapon")]
public class WeaponSO : ScriptableObject {
    [SerializeField] private string _id;
    [SerializeField] private WeaponType _weaponType;
    [SerializeField] private ComboGraphSO _comboGraph;                // 非 null；weaponType 须与 comboGraph.weaponType 一致
    [SerializeField] private float _baseDamage;                       // [1.0, 100.0]
    [SerializeField] private string _animatorLayerName;               // 命名规范由 F6/C5 GDD 定义
    [SerializeField] private QuantizationHint _defaultQuantizationGranularity;
    [SerializeField] private float _perfectWindowModifier;            // [0.5, 2.0]，默认 1.0
}

// EnemyInfoSO — 敌人（FE1 行为树路由依赖，攻击节拍锚点由 C4 令牌分配使用）
[CreateAssetMenu(fileName = "EnemyInfo", menuName = "JadeRabbit/Enemy Info")]
public class EnemyInfoSO : ScriptableObject {
    [SerializeField] private string _id;
    [SerializeField] private string _displayName;
    [SerializeField] private int _baseHealth;                         // [1, 9999]
    [SerializeField] private float _baseDamage;                       // [1.0, 999.0]
    [SerializeField] private EnemyBehaviorProfile _behaviorProfile;   // {Melee_Simple, Ranged_Simple, Elite, Boss}
    [SerializeField] private float _attackWindupBeats;                // [0.5, 8.0]，C4 令牌分配输入
    [SerializeField] private BeatPhaseAnchor _beatPhaseAnchor;        // {DownBeat, UpBeat, Free}
    [SerializeField] private int _rewardMeritBase;                    // [0, 100]
}

// LevelConfigSO — 关卡/房间（bgmAssetRef 已重命名为 bgmFilePath，见专家 C5）
[CreateAssetMenu(fileName = "LevelConfig", menuName = "JadeRabbit/Level Config")]
public class LevelConfigSO : ScriptableObject {
    [SerializeField] private string _id;
    [SerializeField] private RoomType _roomType;                      // {Entrance, Combat, Elite, Boss, Reward}
    [SerializeField] private string _bgmFilePath;                     // StreamingAssets 相对路径（FMOD createSound 路径，非 AssetReference）
    [SerializeField] private float _bpm;                              // [80, 240]
    [SerializeField] private int _timeSignatureNumerator;             // {3, 4, 5}
    [SerializeField] private int _timeSignatureDenominator;           // {4, 8}；分子=5 时须为 8
}
// ⚠️ 命名变更：GDD F4-S6 中的 bgmAssetRef 字段在本 ADR 中重命名为 bgmFilePath
//    原因：字段名含 "Ref" 暗示 AssetReference，但实际类型为 string（FMOD 文件路径）
//    F4 GDD 须同步更新此字段名（Rule 4 变更传播）

// PlayerInfoSO — 玩家持久化数据（Rule D4-8：禁止 UnityEngine.Object 引用，禁止直接 ES3.Save）
[CreateAssetMenu(fileName = "PlayerInfo", menuName = "JadeRabbit/Player Info")]
public class PlayerInfoSO : ScriptableObject {
    [SerializeField] private int _playerMerit;                        // [0, +∞)
    [SerializeField] private List<string> _unlockedWeaponIds;         // 长度 [1, 10]；首元素固定 weapon_miaodao
    [SerializeField] private List<MetaUpgradeEntry> _metaUpgradeLevels; // [Serializable] struct，entry.level [0, 10]

    // 运行时缓存字段（[NonSerialized]）：权威来源为 F5 DTO；禁止参与 ES3 序列化
    [NonSerialized] public float latencyCalibrationOffsetMs = 0f;    // F2.Start() 从 F5 加载后写入
    [NonSerialized] public float audioVolumeMaster = 1f;             // FE7.Start() 从 F5 加载后写入
    // ⚠️ 消费系统须等待 F5 加载完成事件后再读取 [NonSerialized] 字段（Awake/早期 Start 读取返回默认值）
}

[Serializable]
public struct MetaUpgradeEntry {
    public string id;       // 升级项 ID，须唯一
    public int level;       // [0, 10]
}
```

#### SO 资产 ID 命名规范

所有 SO 类型的 `_id` 字段须遵循 `snake_case` 命名规范：
- `AttackNodeSO`：`{weaponType}_{attackType}_{index}`，如 `miaodao_light1`
- `ComboGraphSO`：`{weaponType}_comboGraph`，如 `miaodao_comboGraph`
- `PerkSO`：`perk_{effectKeyword}`，如 `perk_perfect_chain`
- `WeaponSO`：`weapon_{weaponType}`，如 `weapon_miaodao`
- `EnemyInfoSO`：`enemy_{role}`，如 `enemy_patrol_hawk`
- `LevelConfigSO`：`level_{areaId}_{roomId}`，如 `level_01_entrance`

ID 字段唯一性由 Odin Validator `[RegisterValidator]` 外部类在 CI 中强制检查（AC-F4-24/26/27/28/29/30）。ID 值**不作为 Addressables address**——Addressable address 由 F6 GDD 独立定义（通常为资产相对路径或手动分配的 label key）。

---

## Alternatives Considered

### Alternative A：运行时 JSON/CSV 外部数据文件

- **Description**: 将攻击节点、连招图、心法等数据以 JSON/CSV 存储在 StreamingAssets 或服务器，运行时解析
- **Pros**: 跨平台热更新；设计师可用 Excel 编辑
- **Cons**: 无 Unity Inspector 集成；无类型安全；运行时解析耗时；无 Editor 验证；资产引用（AnimationClip、Sprite）无法直接序列化为 JSON 对象，须额外 Addressables key 映射层
- **Rejection Reason**: 项目目标是「设计师可在 Unity 中直接编辑并获得实时验证反馈」——JSON/CSV 工作流与 Unity Editor 分离，Odin Inspector 的 UX 优势完全丧失。MVP 阶段无热更新需求，引入复杂性不值得。

### Alternative B：Unity DOTS BlobAssets

- **Description**: 使用 DOTS Entities 的 BlobAsset 系统存储只读游戏数据，提供极高运行时读取性能
- **Pros**: 缓存友好的内存布局；零 GC；Burst 编译兼容
- **Cons**: 与 TopDownEngine（基于 MonoBehaviour/ECS 混合不支持）完全不兼容（见技术首选项 forbidden patterns）；BlobAsset 须手动分配和释放 BlobBuilder；Designer Inspector 工具链为零——Odin Inspector 不支持 BlobAsset
- **Rejection Reason**: 本项目以 TDE 作为移动/生命/关卡基础设施，DOTS 架构与之不兼容。且无帧级每帧大量查询需求（不需要 BlobAsset 的性能天花板），ScriptableObject 的性能完全满足项目需求。

---

## Consequences

### Positive

- 设计师可在 Unity Inspector 中创建/修改所有游戏参数，无需改代码
- Odin Validator CI 在构建期捕获字段错误（而非运行时崩溃）
- SO 只读语义 + 克隆规则防止 Play Mode 数据污染
- `AssetReferenceT<T>` 延迟引用将动画和图标资产的加载时机掌控权交给系统（C2/C3），避免 SO 加载触发全量资产拉取
- 7 类 SO 类型形成完整的数据契约，所有下游系统依赖统一接口，无独立数据格式

### Negative

- **Odin 约束管理成本**：须持续维护 `#if UNITY_EDITOR` 边界；团队成员须了解 Editor-only 约束，否则易误引用 Sirenix 命名空间进 Runtime
- **Addressables 分组学习曲线**：`List<AttackNodeSO>` 的同捆约束须在 F6 GDD 中正确实施，分组错误导致 Bundle 重复或 Missing Reference
- **AnimationClip 加载合约的跨系统依赖**：C2 必须在房间加载时预加载全部 Clip 并妥善释放；若 C2 GDD 设计时忘记此约束，首次攻击帧会触发即时加载（卡顿）

### Risks

| 风险 | 可能性 | 影响 | 缓解 |
|------|--------|------|------|
| Odin Runtime 污染（误引用 Sirenix.* 进 Runtime asmdef）| 中 | 非 Editor 平台构建失败 | AC-F4-08 的 grep CI 检查；培训新成员了解 Editor-only 约束 |
| `LevelConfigSO.bgmFilePath` 字段重命名导致 GDD 与代码不同步 | 低 | 设计师误用旧字段名创建 SO 资产 | F4 GDD 须同步更新此字段名（本 ADR 触发 Rule 4 变更传播） |
| `PerkParameter` 被误添加引用类型字段 | 中 | 克隆体修改污染原始资产（静默 bug） | Rule D4-9 在 ADR 中明确；Odin Validator 扩展 struct 字段类型检查 |
| AnimationClip 未释放导致内存泄漏 | 低 | 房间切换后内存持续增长 | C2 GDD 明确 Handle 释放时机；ADR-D4 Rule D4-4 已指定 C2 为所有者 |
| `[SerializeField]` 误用于属性（Unity 6.3 编译错误）| 低 | 构建失败 | 编译期即发现；CI 在首次构建时捕获 |

---

## GDD Requirements Addressed

| GDD 系统 | 需求 | 本 ADR 如何满足 |
|---------|------|----------------|
| `so-data-layer.md` (F4) | 7 类 SO 类型 schema 定义及接口文档 | Rule D4-1~D4-9 + Key Interfaces 节定义全部 7 类 SO 的字段级规格 |
| `so-data-layer.md` (F4) Rule 2 | 所有 SO 资产运行时为只读 | Rule D4-1 运行时只读原则 |
| `so-data-layer.md` (F4) Rule 3 | PerkSO 运行时克隆规则 | Rule D4-2 PerkSO 克隆规则；Rule D4-9 PerkParameter 值类型不变量 |
| `so-data-layer.md` (F4) Rule 5 | Odin 双模式验证强制启用 | Rule D4-7 Odin 双模式验证（A=Inspector UX，B=CI 门控） |
| `so-data-layer.md` (F4) Rule 6 | Addressables 引用策略，AssetReferenceT<T> 延迟引用 | Rule D4-3 延迟引用策略；Rule D4-4 AnimationClip 加载合约；Rule D4-5/D4-6 Addressables 组约束 |
| `audio-beat-clock.md` (F2) | F2 须接收 LevelConfigSO 注入的 BGM 参数 | Rule D4-1（只读）+ Key Interfaces `LevelConfigSO._bgmFilePath` 字段；注入责任归 F6（ADR-D1） |
| `save-persistence.md` (F5) / ADR-D7 | PlayerInfoSO 须满足 EasySave 3 序列化约束 | Rule D4-8 PlayerInfoSO EasySave 3 约束（禁直接 ES3.Save，禁 UnityEngine.Object 引用，禁 Dictionary） |
| `game-concept.md` (F1) | 支柱四（构筑驱动复盘）：设计师须不改代码就能新增心法 | Rule D4-2 PerkSO 克隆规则 + Rule D4-7 Odin 验证确保资产完整性 |

---

## Performance Implications

- **CPU**: SO 加载为 Addressables 异步路径（房间加载时），不占主线程帧时间。运行时读取 SO 字段为内存访问（< 0.01ms）
- **Memory**: 7 类 SO 主数据轻量（元数据字段，无大型资产直接引用）。`AssetReferenceT<AnimationClip>` 仅在 C2 预加载后才占 AnimationClip 内存；房间退出后释放，内存按需
- **Load Time**: `List<AttackNodeSO>` 与 `ComboGraphSO` 同捆（Rule D4-6），房间加载时单次 Bundle 请求即可获取全部连招节点元数据。大型资产（AnimationClip、Sprite）延迟加载，不影响房间进入首帧
- **Network**: 无（SO 资产本地 Bundle）

---

## Migration Plan

F4 SO 类型为新建，无存量代码迁移。

`LevelConfigSO.bgmFilePath` 字段名来自本 ADR 的重命名决定（原 GDD 草稿使用 `bgmAssetRef`）。**F4 GDD 须在本 ADR 被接受后同步更新字段名**（F4 GDD Rule 4 变更传播机制覆盖此变更）。其他下游 GDD 暂无此字段引用，无需联动更新。

---

## Validation Criteria

1. `Assets/Scripts/` 目录下的 Runtime 代码中无 `using Sirenix.*`（grep CI 检查，AC-F4-08）
2. Odin Validator CI 扫描通过（全部 Error 级别问题已消除，AC-F4-07）
3. 全部 7 类 SO 资产归属至少一个 Addressable Group（AC-F4-09）
4. `AttackNodeSO` 和 `ComboGraphSO` 归属同一 Addressable Group（F6 GDD 验证约束）
5. `PlayerInfoSO` 在 EasySave 3 路径中无直接 SO 实例序列化调用（ADR-D7 Verification Required 3）
6. `PerkSO` 克隆体修改后原始资产字段值不变（AC-F4-03 EditMode 测试）
7. 7 类 SO 类文件各一个，共 7 个（AC-F4-01 CI 检查）
8. `LevelConfigSO._bgmFilePath` 字段在 F4 GDD 中已同步更新（人工检查）

---

## Related Decisions

- [ADR-D1: TDE Integration](adr-d1-tde-integration.md) — LevelConfigSO 注入 F2 的职责归 F6 TDELifecycleAdapter
- [ADR-D5: Animation Pipeline](adr-d5-animation-pipeline.md) — AttackNodeSO.animationClip 须为 AssetReferenceT<AnimationClip>；C2 持有加载句柄
- [ADR-D6: Audio Middleware](adr-d6-audio-middleware.md) — LevelConfigSO.bgmFilePath 须为 FMOD createSound 可直接使用的路径
- [ADR-D7: Save Persistence Middleware](adr-d7-save-persistence-middleware.md) — PlayerInfoSO 字段设计须满足 EasySave 3 DTO 提取约束
- [F4 GDD: so-data-layer.md](../../design/gdd/so-data-layer.md) — 7 类 SO 类型的完整字段规格（本 ADR 为架构层决策，GDD 为字段级细节）
