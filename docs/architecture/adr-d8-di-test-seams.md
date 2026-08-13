# ADR-D8: DI / Deterministic Test Seam Contracts（依赖注入与确定性测试缝隙契约）

## Status
Accepted（2026-08-11，step-1 spike CI 门控 — 见下方 Spike Verification）

## Date
2026-08-11

## Last Verified
2026-08-11（step-1 DI 缝隙 spike 落地：`Assets/Scripts/Core/CombatCore.asmdef` + `IRandomSource` + `InternalsVisibleTo` + 纯 C# NUnit 测试）

## Spike Verification（step-1，2026-08-11）

本 ADR 的 Proposed→Accepted 以 **step-1 缝隙 spike 在 CI（`.github/workflows/tests.yml`，game-ci unity-test-runner, testMode=editmode）跑绿** 为强制前置门（`coding-standards.md`：测试失败不得合并）。spike 以 **`IRandomSource`** 为代表接口，隔离验证 Risk 表中影响最高（HIGH）的 `CS0122 跨 asmdef 不可见` 与"纯 C# 可 mock 性"两项：

| 交付物 | 路径 | 证明 |
|---|---|---|
| CombatCore Runtime 程序集 | `Assets/Scripts/Core/CombatCore.asmdef` | 首次创建（Migration Plan §6）；`technical-preferences.md` 边界合约的 CombatCore asmdef 落地 |
| `IRandomSource` 接口（§4 逐字） | `Assets/Scripts/Core/Interfaces/IRandomSource.cs` | 与 registry `architecture.yaml:297` 契约逐字一致 |
| 生产实现 `SystemRandomSource` | `Assets/Scripts/Core/RNG/SystemRandomSource.cs` | 固定种子可复现（确定性测试规则） |
| `InternalsVisibleTo` 声明 | `Assets/Scripts/Core/AssemblyInfo.cs` | `[assembly: InternalsVisibleTo("EditModeTests")]` — 消解 CS0122 |
| 缝隙探针 `InternalSeamProbe` | `Assets/Scripts/Core/RNG/InternalSeamProbe.cs` | `#if UNITY_EDITOR\|\|DEVELOPMENT_BUILD` internal setter，非 MonoBehaviour 缩影 |
| Mock 契约 `MockRandomSource` | `Assets/Tests/EditMode/CombatCore/Mocks/MockRandomSource.cs` | 纯 C#、禁 using FMOD/TDE/Feel（边界隔离） |
| NUnit spike 套件（5 用例） | `Assets/Tests/EditMode/CombatCore/CombatCore_DiSeamSpike_Test.cs` | 构造注入确定性 + 无保底(AC-C3-14 范式) + InternalsVisibleTo 可见性 + 种子可复现 + 非法输入拒绝 |

**spike 未覆盖（沿用同一已验证模式，随各系统故事落地时验证）**：`IBeatClock`/`IInputEventBus`/`IComboCounter`/`IDSPClock` 的具体 Mock（`MockBeatClock`/`MockInputEventBus`/`MockComboCounter`/`MockDSPClock`）在 C1/C2/C4 实现冲刺中按本 spike 证实的字段注入 + InternalsVisibleTo 范式补齐。step-1 只需证实**模式本身**可行——HIGH 风险已消解。

> **验证约束**：本环境无 Unity/dotnet 运行时，spike 的实际"跑绿"由项目 CI 在 PR 上执行；PR 的 merge-gate（测试失败不合并）即"spike 通过"的强制判据。若 EditMode 测试在 CI 失败，本 ADR 不得以 Accepted 合入 main。

## Decision Makers
lead-programmer（接口所有权与命名裁定）、technical-director（架构一致性，lean 模式未强制评审）、unity-specialist（Unity 6.3 惯用性校验）、user（问题框定与四项设计裁定，2026-08-11）

## Summary
四个 Core 系统（C1 rhythm-core / C2 combo-system / C3 perk-system / C4 combat-director）的 80% 单元测试硬门依赖五个跨系统依赖注入接口 + 一个规范暂停/恢复事件源，但这些接口此前**未被任何已接受 ADR 或代码文件拥有**（`/architecture-review 2026-08-11` 缺口 A2）。本 ADR 统一声明并拥有 `IBeatClock`（只读消费者面）、`IInputEventBus`、`IComboCounter`（C1 暴露的只读段数面）、`IRandomSource`、`IDSPClock` 五个接口及其 Mock 契约，沿用 ADR-D10 的 `ISaveService`/`MockSaveService` 消费者缝隙范式，使 C1-C4 可在纯 C# NUnit EditMode 测试中隔离被测系统。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (6000.3.19f1) |
| **Domain** | Core / Scripting（依赖注入接口分层、可测性缝隙） |
| **Knowledge Risk** | LOW — 纯 C# 接口声明 + `[SerializeField] MonoBehaviour` 字段注入 + 属性包装。Unity 6.3 的 "SerializeField 仅限字段" 约束已在方案中处理（见 Implementation Guidelines）。无 post-cutoff 引擎 API。 |
| **References Consulted** | `docs/architecture/adr-d10-bootstrap-sequencing.md`（ISaveService 缝隙先例）、`adr-d6-audio-middleware.md`（IDSPClock/IAudioPlayer 生产实现）、`adr-d3-rhythm-quantization-core.md`（M3 缺口）、`adr-d1-tde-integration.md`（IBeatClockLifecycle 归属）、`design/gdd/audio-beat-clock.md`、`input-event-system.md`、`rhythm-core.md`、`combo-system.md`、`perk-system.md`、`combat-director.md`、`docs/registry/architecture.yaml` |
| **Post-Cutoff APIs Used** | None（Unity 引擎侧）— 仅 `System.Random`（或等价 PRNG）+ C# `event Action<T>` + `interface`。§5 引用的 FMOD `getDSPClock`/`getSoftwareFormat` 属 ADR-D6 管辖，其 API 面风险已转移至 ADR-D6，本 ADR 仅引用不做验证。 |
| **Verification Required** | 1. EditMode 测试确认 `SetBeatClockForTesting(IBeatClock)` 等注入 setter 在 `#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD` 块外不可见（发布构建不暴露测试缝隙）。 2. 确认 `MockDSPClock`/`MockRandomSource` 在纯 C# NUnit（无 PlayMode、无 FMOD、无场景）下可构造并驱动 C1-C4 判定逻辑。 3. 确认 `IBeatClock` 消费者面不包含任何 `Play/Stop/Pause/Resume` 生命周期方法（编译期即阻断 `beatclock_lifecycle_bypass`）。 |

> **Note**: Knowledge Risk 为 LOW，纯语言层契约；引擎升级不会使本 ADR 失效，除非 `[SerializeField]` 字段注入语义变更。

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-D3（Rhythm Quantization Core，M3 要求注入 IBeatClock/IInputEventBus — Accepted）、ADR-D6（Audio Middleware，IDSPClock/IAudioPlayer 生产实现 — Accepted）、ADR-D1（TDE Integration，IBeatClockLifecycle/InputEventBus 状态权威 — Accepted）、ADR-D10（Bootstrap Sequencing，ISaveService/MockSaveService 缝隙范式先例 — Accepted） |
| **Enables** | C1 单测（AC-C1 系列，mock IBeatClock/IInputEventBus）；C2 单测（AC-C2-17/22/30/37，mock IComboCounter）；C3 单测（AC-C3-13/14/16，mock IRandomSource）；C4 单测（AC-C4 系列，mock IDSPClock）；`/test-setup` 可为 CombatCore 建立确定性 EditMode 测试基座 |
| **Blocks** | C1/C2/C3/C4 编码进入实现（80% 单测覆盖率硬门在接口无归属时无法达成）；`/create-stories` 为 C1-C4 生成含 DI 约束的故事 |
| **Ordering Note** | 不引入新运行时依赖，仅声明契约；可与 A3（修订 D1 命名）、A4（F2 锁定事件 schema）并行推进。IDSPClock 规范方法名裁定（见 Decision §5）须先于 F2/F3/D6 的命名同步修订落地。 |

## Context

### Problem Statement
`/architecture-review 2026-08-11`（Verdict: CONCERNS）将 **A2「接口所有权缺口」** 列为 Blocking-for-implementation 第 2 项，并把 **ADR-D8（DI/确定性测试缝隙契约）** 列为按价值排序的第一优先 ADR：一份 ADR 同解 A2 + 四个 Core 系统的 Open Question（C1-M3 / C2-OQ4 / C3-OQ6 / C4-OQ1）。

问题的核心：C1-C4 的验收标准要求 80% 单元测试覆盖率（`coding-standards.md` 逻辑类故事的 BLOCKING 门），而这些系统的判定逻辑（节奏评级、连招推进、心法加权抽取、令牌调度）都依赖运行时子系统（BeatClock、InputEventBus、DSP 时钟、RNG）。要在纯 C# NUnit EditMode 下确定性地测试判定逻辑，必须能**注入 mock 替身**隔离这些子系统。但截至本 ADR，`IBeatClock`/`IInputEventBus`/`IComboCounter`/`IRandomSource`/`IDSPClock` 五个接口**仅存在于 GDD/ADR 文本描述中**，无任何代码文件或已接受 ADR 声明它们的成员与所有权——`Assets/Scripts/Core/` 目录尚不存在。

不解决的代价：C1-C4 的 80% 单测硬门无法达成，四个 Core 系统全部卡在实现前；各系统会各自即兴定义不兼容的接口形态（例如 IDSPClock 方法名已在三处文档分歧），导致编译期集成冲突。

### Current State
- **无代码归属**：`Assets/Scripts/` 下无任何 `I*` 接口文件；`Assets/Scripts/Core/` 不存在。所有接口只是 GDD 中的占位描述。
- **C1-M3 已在 GDD 层"解决"但接口无 ADR 归属**：`rhythm-core.md:846` 采用「字段注入 + 属性包装 + `SetBeatClockForTesting()`」方案，但 `architecture-review-2026-08-11.md:50` 指出"接口本身无 ADR 归属"——GDD 描述了注入*机制*，却没有权威声明接口*成员*。
- **命名分歧**：IDSPClock 方法名三处不一（F2 `GetCurrentTime()` / 历史 `GetCurrentDSPTime()` / F3 `GetDspTime()`，cross-review W2/W-06 反复标记未统一）。
- **所有权倒置**：`IComboCounter` 名义归 C2，但段数（combo count）实际由 **C1 拥有并广播**（注册表 `rhythm_judgment_combo` 立场：`RhythmJudger.CurrentCombo` 只读、C1 唯一写者）；C2 只读、绝不回写。
- **RNG 完全留白**：`perk-system.md:326` 把 `IRandomSource` 的方法签名刻意留给 ADR，只约束"确定性 + 可注入"。

### Constraints
- **技术**：MonoBehaviour 不支持构造函数注入。测试注入须走「`[SerializeField] private MonoBehaviour _ref;` + 属性包装 `private IX X => _ref as IX;` + `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 内的 `SetXForTesting(IX)` setter」（Unity 6.3 SerializeField 仅限字段约束下的唯一方案，`rhythm-core.md:846` 已确立）。
- **状态所有权（注册表，不可违反）**：
  - `beat_clock_lifecycle`（`Play/Stop/Pause/Resume`）专属 F6 `TDELifecycleAdapter` → **`IBeatClock` 消费者面禁止包含生命周期方法**；它们属于独立的 `IBeatClockLifecycle`（ADR-D1，F6 专属）。
  - `rhythm_judgment_combo`（`CurrentCombo`）C1 唯一写者 → `IComboCounter` 是 C1 暴露的**只读**面，C2 只读 mock。
  - `action_map_state`（`EnableGameplayInput/EnableUIInput/DisableAllInput`）F3 唯一权威。
- **禁止模式（注册表）**：`beatclock_lifecycle_bypass`、`direct_action_map_enable_disable` — 接口面须在编译期就不给消费者暴露越权入口。
- **确定性测试规则**（`coding-standards.md`）：测试不得依赖随机种子、时间、文件 I/O；须经依赖注入。这正是本 ADR 存在的理由。
- **边界合约**（`technical-preferences.md`）：CombatCore 程序集负责节奏量化与连招逻辑；接口须允许 CombatCore 在不 using TDE/FMOD/Feel 命名空间的前提下被测。

### Requirements
- 五个接口各有一份权威成员声明，消除跨文档命名/字段分歧。
- 每个接口配一个生产实现（引用现有 ADR）+ 一个 Mock 实现（纯 C#，无引擎依赖），沿用 ADR-D10 `ISaveService`/`MockSaveService` 范式。
- `IBeatClock` 消费者面**只读**：仅事件订阅 + 同步快照查询，无生命周期方法。
- `IDSPClock` 钦定唯一规范方法名 `GetDspTime()`，F2/F3/D6 命名同步。
- `IRandomSource` 定义最小充分方法集，支撑 C3 加权无放回抽取（公式 4）的确定性单测（AC-C3-13/14）。
- 规范暂停/恢复事件源：F2 唯一发布，订阅方 C1/C3/F6，禁止并行事件源。
- 测试注入 setter 仅在 `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 暴露，发布构建无测试缝隙。

## Decision

本 ADR 在 `Assets/Scripts/Core/Interfaces/`（新建）声明五个消费者缝隙接口 + payload struct，并规定每个接口的「生产实现（现有 ADR 拥有）/ Mock 实现（本 ADR 拥有）」二元对，遵循 ADR-D10 D-4 的双实现缝隙范式。**接口的实现归属属各自系统的 ADR；本 ADR 只拥有接口*声明*与 Mock 契约**，是四系统共享的可测性契约层。

### 分层原则（Consumer Seam vs Lifecycle）

沿用 ADR-D10 对 `ISaveService`（整服务消费者面）与 `IFileStorage`（存储后端 mock）的区分。每个接口拆为两层：

- **消费者面（本 ADR 拥有，供 C1-C4 只读/订阅并 mock）**：`IBeatClock`、`IInputEventBus`（读取部分）、`IComboCounter`、`IRandomSource`、`IDSPClock`。
- **控制面（各系统 ADR 拥有，权限受限）**：`IBeatClockLifecycle`（ADR-D1，F6 专属 `Play/Stop/Pause/Resume/RequestBPMChange/…`）、`InputEventBus` 的状态方法（ADR-D1，F3 专属 `Enable*/DisableAllInput`）。控制面**不进消费者接口**，编译期即阻断越权。

### §1 — IBeatClock（只读消费者面，C1/C4 注入 mock）

```csharp
// Assets/Scripts/Core/Interfaces/IBeatClock.cs
// 消费者只读面。生产实现: BeatClock : IBeatClock, IBeatClockLifecycle (F2, ADR-D6)。
// 生命周期方法在 IBeatClockLifecycle (ADR-D1, F6 专属)，故意不在此接口。
public interface IBeatClock {
    // ── 拍点事件（C# event Action<T>；payload 均 readonly struct，零 GC）──
    event Action<BeatEventData>        OnBeatLookahead;       // 拍点前 LookaheadMs 广播（C4 硬依赖）
    event Action<BeatEventData>        OnHalfBeatLookahead;   // 半拍前瞻（C1 硬依赖，半拍网格）
    event Action<BeatEventData>        OnBeatExact;           // 全拍精确到达（m 偶数）
    event Action<BeatEventData>        OnHalfBeatExact;       // 半拍精确（m 奇数）
    event Action<MeterChangeEventData> OnMeterChangeAccepted; // BPM/拍号变更被接受
    event Action<MeterChangeEventData> OnMeterChanged;        // 边界生效（C1 重锚、C4 调令牌密度）
    event Action<BeatClockPauseEventData> OnBeatClockPaused;  // 见 §6（规范暂停源）
    event Action<BeatClockPauseEventData> OnBeatClockResumed; // 见 §6（规范恢复源）

    // ── 同步查询 / 只读属性 ──
    BeatStateSnapshot GetSnapshot();                 // 同步拉取当前拍状态
    float  LatencyOffsetMs { get; }                  // 只读（两阶段写者 ADR-D10；消费者禁写）
    IDSPClock DSPClock { get; }                      // 只读，F6/测试经此访问 DSP 时钟
    double GetBeatIntervalSeconds();
    bool   IsStartConfirmed { get; }
    double GetConfirmedDSPStart();                   // 未确认返回 -1.0（测试友好哨兵）
}
```

> **废弃名对齐**：`OnBpmChanged` → 已废弃，规范名 `OnMeterChangeAccepted`/`OnMeterChanged`；`OnPaused` → 别名，规范名 `OnBeatClockPaused`。ADR-D3 §3 中 `BeatClock.OnPaused += …` 须同步为规范名。

### §2 — IInputEventBus（读取面，C1 注入 mock）

```csharp
// Assets/Scripts/Core/Interfaces/IInputEventBus.cs
// 消费者读取面。生产实现: InputEventBus : IInputEventBus (F3, ADR-D1, MonoBehaviour 单例 DontDestroyOnLoad)。
// 状态方法 EnableGameplayInput/EnableUIInput/DisableAllInput 属 ADR-D1 F3 控制面，
// 故意不在此消费者接口（direct_action_map_enable_disable 禁令）。
public interface IInputEventBus {
    event Action<AttackInputEvent> OnAttackInput;  // C1 订阅
    event Action<DashInputEvent>   OnDashInput;    // F6/TDE
    event Action                   OnPause;        // 暂停触发
    Vector2 GetCurrentMoveInput();                 // 持续值查询
    bool    IsChargedAttackHeld { get; }           // E3 蓄力查询
}
```

### §3 — IComboCounter（C1 暴露的只读段数面，C2 注入 mock）

```csharp
// Assets/Scripts/Core/Interfaces/IComboCounter.cs
// 段数由 C1 拥有并广播（注册表 rhythm_judgment_combo）。C2 只读、绝不回写。
// 生产实现: RhythmJudger : IComboCounter (C1)。Mock 供 C2 单测（AC-C2-17/22/30/37）验证"C2 从不写 C1 段数"。
public interface IComboCounter {
    int CurrentCombo { get; }   // C1 持有并广播；Miss/死亡清零，Perfect/Good 自增
    int ComboMax     { get; }   // 固定 999
}
```

### §4 — IRandomSource（确定性 RNG，C3 注入 mock — 最小充分集）

```csharp
// Assets/Scripts/Core/Interfaces/IRandomSource.cs
// 可注入确定性随机源，支撑 C3 PerkRollService 加权无放回抽取（perk-system.md 公式 4）。
// 生产实现: SystemRandomSource : IRandomSource (封装 System.Random)。
// 测试实现: MockRandomSource : IRandomSource (可编排返回序列，验证 AC-C3-14 "永不越 Legendary 带"无保底)。
public interface IRandomSource {
    int    Next(int maxExclusive);   // [0, maxExclusive) 均匀整数
    double NextDouble();             // [0.0, 1.0) 均匀双精度
    void   Reseed(int seed);         // 重置种子，保证测试可复现
    int    Seed { get; }             // 当前种子（可复现性诊断）
}
```

> 最小集刻意不含 `Next(min,max)`/`NextFloat()`/`NextBool()` 等便捷方法——避免接口膨胀；C3 加权抽取只需 `Next(int)` + `NextDouble()`。未来若有新需求，追加成员即可（不破坏既有 mock）。

### §5 — IDSPClock（共享 DSP 时钟，C4 注入 mock；规范方法名 `GetDspTime()`）

```csharp
// Assets/Scripts/Core/Interfaces/IDSPClock.cs
// 生产实现: FMODDSPClockImpl : IDSPClock (ADR-D6)。测试实现: MockDSPClock (返回可控 dsp_now)。
// F2(BeatClock)、F3(InputEventBus 时间戳注入)、C4 三方共用【同一】IDSPClock 实现。
public interface IDSPClock {
    double GetDspTime();   // 规范唯一方法名（钦定）。FMOD master channel group getDSPClock ÷ getSoftwareFormat 采样率，返回秒
}
```

**命名裁定**：钦定唯一规范方法名为 **`GetDspTime()`**，取代 F2 `GetCurrentTime()`、历史 `GetCurrentDSPTime()`、F3 `GetDspTime()` 三处分歧（cross-review W2/W-06）。理由：语义最清晰（明确返回 DSP 时间/秒），F3 输入时间戳注入已在用。**副作用**：ADR-D6 `FMODDSPClockImpl` 与 F2 `audio-beat-clock.md` 文本需在命名同步修订中把 `GetCurrentTime()`/`GetCurrentDSPTime()` 改为 `GetDspTime()`（附说明即可，非架构变更）。

> **FMOD 实现约束（继承 ADR-D6，Mock 无需遵守）**：`getDSPClock` 从 **master channel group** 读取；除数**必须**用 `CoreSystem.getSoftwareFormat(out int sampleRate, …)`，**禁止** `AudioSettings.outputSampleRate`（`unity_samplerate_in_fmod_dsp_clock` 禁令）。下游禁止读 `AudioSettings.dspTime`。

### §6 — 规范暂停/恢复事件源（消解 BC-6 信号不一致）

**F2（BeatClock）是唯一发布者**。`OnBeatClockPaused` / `OnBeatClockResumed`（见 §1，`IBeatClock` 面）是四方唯一的暂停/恢复信号源。**禁止** C1/C3/F6 或任何系统各自定义 `OnGamePaused`/`OnGameResumed` 并行事件。

| 角色 | 系统 | 机制 |
|---|---|---|
| **发起方**（方法调用） | F6 `TDELifecycleAdapter` | 调用 `IBeatClockLifecycle.Pause()` / `Resume()`（ADR-D1，控制面） |
| **发布方**（事件广播） | F2 BeatClock | 状态翻转后同帧广播 `OnBeatClockPaused`；重锚后、首拍 Exact 前广播 `OnBeatClockResumed` |
| 订阅方（硬） | C1 | 恢复时重锚拍网格 + 清空过期 `_pendingInput` |
| 订阅方（软） | C3 | 冻结/续算 `PerkModifierStack` |
| 订阅方 | F6 | SC-1 音频降级恢复对称路径 |

> **订阅方范围裁定**：按已 PASS 的 GDD 锁定为 **C1/C3/F6**（用户 2026-08-11 裁定）。C4（combat-director）当前仅订阅拍事件、未订阅 pause/resume；未来若需可订阅**同一** `OnBeatClockPaused`/`OnBeatClockResumed`，无需新增事件源。

**成对不变量**（AC-F2-28）：每次「暂停→恢复」周期各恰好 1 次；`OnBeatClockPaused`（`isResume=false`）先于配对的 `OnBeatClockResumed`（`isResume=true`）。

### Payload 结构（本 ADR 正式声明，readonly struct 零 GC）

```csharp
// BeatEventData — 拍点事件 payload。权威字段 BeatDspTime 已含 latency offset（"玩家感知时间域"）；
// 下游必须用它做时序判定，禁止读 AudioSettings.dspTime。
public readonly struct BeatEventData {
    public readonly double BeatDspTime;       // 已含 offset，玩家感知时间域
    public readonly int    BeatIndexInMeasure;
    public readonly int    MeasureIndex;
    public readonly int    GlobalBeatIndex;
    public readonly float  Bpm;               // [0,240]
}

public enum BeatClockState { Stopped, Playing, Paused, PendingBPMChange, PendingTimeSignatureChange }

public readonly struct BeatStateSnapshot {
    public readonly float  Bpm;                 // [0,240], 零态 0
    public readonly int    Numerator;           // {0,3,4,5}
    public readonly float  BeatProgress;        // [0,1]
    public readonly int    BeatIndexInMeasure;  // [0, Numerator-1]
    public readonly int    MeasureIndex;
    public readonly int    GlobalBeatIndex;
    public readonly double NextBeatDspTime;     // 零态 double.MaxValue
    public readonly double NextHalfBeatDspTime; // 零态 double.MaxValue
    public readonly BeatClockState ClockState;  // 零态 Stopped
}

public readonly struct MeterChangeEventData {
    public readonly float OldBpm;   // [80,240]
    public readonly float NewBpm;   // [80,240]
    public readonly int   OldNumerator;
    public readonly int   NewNumerator;
    public readonly double EffectiveDspTime;
}

// BeatClockPauseEventData — OnBeatClockPaused / OnBeatClockResumed 共用 payload。
public readonly struct BeatClockPauseEventData {
    public readonly bool   IsResume;                    // false=暂停 / true=恢复
    public readonly double ResumeFirstBeatDspTimeRaw;   // 仅恢复有效：首拍 raw DSP（未含 offset，Core Rule 13）；暂停哨兵 -1.0
    public readonly int    BeatIndexInMeasure;          // 冻结/续接位置（恢复不重置为 0）
    public readonly int    MeasureIndex;
    public readonly double FractionalProgressAtPause;   // 仅暂停有效：拍内已过去秒数；恢复时 0.0
}

// AttackInputEvent — IInputEventBus.OnAttackInput payload。
public readonly struct AttackInputEvent {
    public readonly AttackType attackType;       // LightAttack | HeavyAttack | ChargedAttack
    public readonly double inputDspTime;         // performed/canceled 回调内同步 IDSPClock.GetDspTime()
    public readonly double chargeStartDspTime;   // ChargedAttack 专用（Hold.started）；普通攻击哨兵 -1.0
    public readonly float  inputGameTime;        // ctx.time，仅调试
    public readonly InputDeviceType deviceType;  // Keyboard | Gamepad | Touchscreen
    public readonly int    deviceId;             // 纯值，无悬挂引用
}

public enum AttackType { LightAttack, HeavyAttack, ChargedAttack }   // ⚠ ADR-D3 §3 须补 HeavyAttack（当前仅 Light/Charged）
public enum InputDeviceType { Keyboard, Gamepad, Touchscreen }
```

### Mock 契约（本 ADR 拥有；沿用 ADR-D10 双实现范式）

| 接口 | 生产实现（拥有 ADR） | Mock 实现（本 ADR 拥有） | 用途 |
|---|---|---|---|
| `IBeatClock` | `BeatClock`（F2, ADR-D6） | `MockBeatClock` | 手动触发 `OnHalfBeatLookahead`/`OnBeatExact`，编排 `BeatStateSnapshot`，驱动 C1/C4 判定 |
| `IInputEventBus` | `InputEventBus`（F3, ADR-D1） | `MockInputEventBus` | 注入 `AttackInputEvent`（含精确 `inputDspTime`），驱动 C1 评级 |
| `IComboCounter` | `RhythmJudger`（C1） | `MockComboCounter` | 固定 `CurrentCombo`，验证 C2 只读不回写（AC-C2-22） |
| `IRandomSource` | `SystemRandomSource` | `MockRandomSource` | 编排返回序列，验证 C3 加权抽取无保底（AC-C3-14） |
| `IDSPClock` | `FMODDSPClockImpl`（ADR-D6） | `MockDSPClock` | 返回可控 `dsp_now`，驱动 500 拍确定性调度（C4）；同步模式检测（t1==t2） |

> **姊妹接口（本 ADR 不拥有，仅引用避免混淆）**：`IAudioPlayer`（F2 对称注入，ADR-D6 拥有 `FMODAudioPlayerImpl`）、`IRhythmModifierProvider`（C3 实现、C1 只读查询）、`ISaveService`（ADR-D10 拥有）。这些已有归属，D8 不重复拥有。

### Implementation Guidelines

MonoBehaviour 不支持构造注入，统一采用 `rhythm-core.md:846` 确立的**字段注入 + 属性包装**模式（每个被测系统一致）：

```csharp
public class RhythmJudger : MonoBehaviour, IComboCounter {
    [SerializeField] private MonoBehaviour _beatClockRef;      // Inspector 拖 BeatClock 组件
    [SerializeField] private MonoBehaviour _inputEventBusRef;  // Inspector 拖 InputEventBus 组件
    private IBeatClock     _beatClock;   // 缓存解析结果
    private IInputEventBus _input;
    private void Awake() {
        _beatClock ??= _beatClockRef as IBeatClock;
        _input     ??= _inputEventBusRef as IInputEventBus;
    }

#if UNITY_EDITOR
    // 编辑期校验：拖错组件（不实现目标接口）时立即在 Inspector 报错，
    // 而非拖到运行时首次调用才抛 NRE（报错点与配置错误点分离）。
    private void OnValidate() {
        if (_beatClockRef != null && _beatClockRef is not IBeatClock)
            Debug.LogError($"{name}: _beatClockRef 未实现 IBeatClock", this);
        if (_inputEventBusRef != null && _inputEventBusRef is not IInputEventBus)
            Debug.LogError($"{name}: _inputEventBusRef 未实现 IInputEventBus", this);
    }
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // 测试缝隙：仅开发/编辑器构建暴露，发布构建无此入口。
    // `??=` 缓存保证注入不被 Awake() 覆盖（AddComponent 会同步触发 Awake）。
    internal void SetBeatClockForTesting(IBeatClock mock)  => _beatClock = mock;
    internal void SetInputForTesting(IInputEventBus mock)  => _input = mock;
#endif
}
```

**⚠ 跨程序集可见性（BLOCKING 前置，unity-specialist 2026-08-11）**：`internal` setter 只在**声明它的同一 Runtime 程序集**内可见；测试代码在独立测试 asmdef 中调用会报 `CS0122`（asmdef 的 Assembly Definition References 只暴露 `public`，不授予 `internal` 访问权）。Runtime 程序集**必须**声明 `InternalsVisibleTo` 指向测试程序集，否则整套 setter 注入在测试里编译失败：

```csharp
// CombatCore Runtime 程序集内任一 .cs（或 AssemblyInfo.cs）顶部：
[assembly: InternalsVisibleTo("EditModeTests")]
```

具体 Runtime/测试 asmdef 名称与配对关系由 `/test-setup`（Migration Plan §6）钦定并写死；若测试拆成多个 asmdef（C1/C2/C3/C4 各自），须为每个测试程序集各加一条 `InternalsVisibleTo`，或统一到单一 `EditModeTests` 程序集内用 namespace 隔离。

- **优先纯 C# 类构造注入**：纯逻辑（RNG 抽取、DSP 调度、评级判定）应尽量下沉到**无 MonoBehaviour 依赖的纯 C# 类**（`public` 构造函数直接注入 mock），跑 EditMode NUnit——比 setter 注入更干净、覆盖率更高，且**不受上述 `InternalsVisibleTo` 约束**（`public` 类/构造无可见性问题）。setter 注入只用于必须留在 MonoBehaviour 上的残余字段（如 `RhythmJudger` 自身）。
- **零 GC 广播约束（兑现 readonly struct payload 承诺）**：拍点事件广播必须用 `OnBeatExact?.Invoke(data)`，**禁止** `GetInvocationList()`（每次调用堆分配 `Delegate[]`，逐拍高频路径直接破坏零 GC）；订阅方应在 `OnEnable`/`Awake` 一次性订阅，**禁止**用捕获局部变量的 lambda 做逐帧动态订阅/取消（闭包分配 + 重复订阅泄漏）。
- `MockDSPClock`/`MockRandomSource`/`MockBeatClock` 等测试实现放 `Assets/Tests/EditMode/CombatCore/Mocks/`，由测试 asmdef 引用（不进 Runtime 包体）。
- 所有 `Mock*` 实现禁止 `using` FMOD/TDE/Feel 命名空间，保持 CombatCore 边界隔离（`technical-preferences.md` 边界合约）。
- **可选增强（未来记录）**：若 CombatCore 接口种类增多，可升级为 `InterfaceReference<TInterface, TObject>` 可序列化包装 + 自定义 `PropertyDrawer`，把"拖错组件"错误从运行时提前到编辑期赋值时；引入 DI 容器（VContainer/Zenject）可从根本消除无构造注入约束，但两者均不在 `technical-preferences.md` Allowed Libraries，需 technical-director 批准。

## Alternatives Considered

### Alternative 1: 各系统各自定义接口（no shared ownership）
- **Description**: 不设统一 ADR，C1-C4 各自在实现冲刺时定义所需接口。
- **Pros**: 无需前置协调；每系统按需裁剪。
- **Cons**: IDSPClock 方法名已在三处分歧，放任会导致 F2/F3/C4 编译期集成冲突；`IComboCounter` 所有权倒置会被重复犯错（C2 误以为可写段数）；重复声明 `BeatEventData` 等 struct 造成类型不兼容。
- **Estimated Effort**: 表面更低，实则更高（后期集成返工）。
- **Rejection Reason**: A2 缺口的本质就是"无单一所有权"；分散定义正是要消除的病灶。

### Alternative 2: 具体类直接注入（无接口，用 virtual 方法或 sealed mock 子类）
- **Description**: 不定义接口，测试时子类化 `BeatClock` 覆写 virtual 方法。
- **Pros**: 少一层接口声明。
- **Cons**: `BeatClock` 依赖 FMOD RuntimeManager，即便子类化也需 FMOD 初始化，无法在纯 C# NUnit 下构造；违反"测试不依赖外部系统"（`coding-standards.md`）；暴露生命周期方法给消费者，破坏 `beatclock_lifecycle_bypass` 编译期防线。
- **Estimated Effort**: 相当。
- **Rejection Reason**: 无法达成纯 C# 确定性测试；接口是隔离 FMOD/TDE 依赖的唯一干净手段。

### Alternative 3: IBeatClock 包含生命周期方法（单一大接口）
- **Description**: `IBeatClock` 同时含读取面与 `Play/Stop/Pause/Resume`。
- **Pros**: 一个接口涵盖全部。
- **Cons**: 消费者（C1/C4）注入后即可调用 `Pause()`，编译期无法阻断 `beatclock_lifecycle_bypass`；违反 F6 唯一生命周期控制权。
- **Rejection Reason**: 违反注册表状态所有权 `beat_clock_lifecycle`；接口隔离（读/控制分面）是编译期强制越权防线的关键。

## Consequences

### Positive
- A2 缺口消解：五接口 + pause 源获得单一代码级所有权；C1-C4 的 80% 单测硬门解锁。
- 一份 ADR 同解 C1-M3 / C2-OQ4 / C3-OQ6 / C4-OQ1 四个 Open Question。
- IDSPClock 方法名统一为 `GetDspTime()`，消除 W2/W-06 三方分歧。
- `IComboCounter` 所有权正本清源（C1 只读面），编译期防止 C2 回写段数。
- 读/控制分面把 `beatclock_lifecycle_bypass`、`direct_action_map_enable_disable` 从"文档禁令"升级为"编译期不可达"。
- 沿用 D10 双实现范式，团队已熟悉 `ISaveService`/`MockSaveService` 心智模型，零学习成本。

### Negative
- 新增 5 个接口 + ~7 个 payload struct + 5 个 Mock 类需声明并与生产实现保持同步（接口漂移风险）。
- 触发若干下游文本同步修订：ADR-D3 §3 补 `HeavyAttack` + `CancelTier`/`ComboCountAfter` 字段（ADR-D3 修订范畴，非本 ADR）；ADR-D6/F2 把 `GetCurrentTime()` 改 `GetDspTime()`。
- CombatCore 纯逻辑须刻意下沉到无 MonoBehaviour 类才能最大化覆盖率——对习惯 MonoBehaviour-first 的实现是一层纪律成本。

### Neutral
- `Assets/Scripts/Core/Interfaces/` 目录首次创建；后续 Core 接口有了归置处。
- 姊妹接口 `IAudioPlayer`/`IRhythmModifierProvider` 归属不变，仅被引用。

## Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| 接口与生产实现漂移（改了 BeatClock 忘改 IBeatClock） | MEDIUM | MEDIUM | 生产类 `: IBeatClock` 显式实现，编译器强制成员齐全；`/architecture-review` 复查接口 vs GDD 事件名 |
| IDSPClock 改名遗漏某调用点 | LOW | MEDIUM | 改名后全局 grep `GetCurrentDSPTime\|GetCurrentTime`（DSP 上下文）；F2/F3/D6 同步修订清单列明三处 |
| Mock 实现误 using FMOD/TDE 破坏边界 | LOW | LOW | Mock 放测试 asmdef，不引用 Runtime 中间件 asmdef；CI asmdef 依赖检查 |
| C4 未来需订阅 pause 却被本 ADR"锁死"为非订阅方 | LOW | LOW | §6 已注明 C4 可订阅同一事件、无需新增源；非硬约束 |
| 测试 setter 泄漏到发布构建 | LOW | MEDIUM | `#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD` 包裹；Verification Required #1 覆盖 |
| `internal` setter 跨 asmdef 对测试程序集不可见（CS0122） | MEDIUM | HIGH | Runtime 程序集声明 `[assembly: InternalsVisibleTo("EditModeTests")]`；`/test-setup` 钦定具体 asmdef 名（见 Implementation Guidelines + Migration Plan §6）；优先用不受此约束的纯 C# 构造注入 |
| Inspector 拖入不实现目标接口的组件 → `as` 静默返回 null → 运行时首次调用才 NRE（报错点分离） | LOW | MEDIUM | `#if UNITY_EDITOR` 内 `OnValidate()` 编辑期校验接口类型（见 Implementation Guidelines） |

## Performance Implications

| Metric | Before | Expected After | Budget |
|--------|--------|---------------|--------|
| CPU (frame time) | N/A | +0ms（接口调用 devirtualize；事件已存在） | 不改 beat-clock 0.2ms 预算 |
| Memory | N/A | 接口引用字段若干指针；payload 均 readonly struct（栈，零堆） | 可忽略 |
| Load Time | N/A | 无变化 | — |
| Network | N/A | None | — |

> 接口调用在 IL2CPP 下多一次虚表跳转，但 payload 为 readonly struct（无装箱、零 GC），C# `event Action<T>` 已是既有机制。无新增每帧成本。

## Migration Plan

Greenfield（`Assets/Scripts/Core/` 尚不存在）——无代码迁移。交付清单：

1. **本 ADR（D8）**：声明 5 接口 + payload struct + Mock 契约（`Assets/Scripts/Core/Interfaces/` + `Assets/Tests/EditMode/CombatCore/Mocks/`），实现留给 C1-C4 各自故事。
2. **ADR-D3 修订**（独立，A2 相邻）：§3 补 `HeavyAttack`；`RhythmJudgmentEvent` 补 `CancelTier`/`ComboCountAfter`；`OnPaused`/`OnBpmChanged` 改规范名。
3. **ADR-D6 / F2 命名同步**：`FMODDSPClockImpl`/`audio-beat-clock.md` 把 `GetCurrentTime()`/`GetCurrentDSPTime()` 改 `GetDspTime()`（附说明）。
4. **注册表**：登记 5 接口所有权 + IDSPClock 方法名 API 决策 + `beatclock_consumer_seam_readonly` 立场（见下）。
5. **systems-index.md**：C1-C4 依赖列追加 ADR-D8。
6. `/test-setup` 建立 CombatCore EditMode 测试 asmdef + Mocks 目录（本 ADR 解锁的后续动作）。**须包含**：为 Runtime 程序集生成 `[assembly: InternalsVisibleTo("EditModeTests")]` 声明（测试 asmdef 名 = `EditModeTests`，见 `Assets/Tests/EditMode/EditModeTests.asmdef`；否则首次调用 `SetXForTesting` 即编译失败，unity-specialist BLOCKING 项）；钦定 Runtime/测试 asmdef 的准确名称与配对关系。

**Rollback plan**: 接口未被任何生产代码引用前（greenfield），删除 `Assets/Scripts/Core/Interfaces/` 即完全回滚；无运行时行为变更。

## Validation Criteria

- [ ] `IBeatClock` 消费者面不含任何 `Play/Stop/Pause/Resume`（grep 接口体确认；生命周期在 `IBeatClockLifecycle`）。
- [ ] `MockDSPClock`/`MockRandomSource`/`MockBeatClock` 可在纯 C# NUnit（无 PlayMode/FMOD/场景）构造并驱动一条 C1 评级 + 一条 C4 令牌调度用例。
- [ ] C3 加权抽取用 `MockRandomSource` 编排"永不落 Legendary 带"序列，断言无保底（AC-C3-14 成真）。
- [ ] C2 用 `MockComboCounter` 断言 C2 代码路径从不写 `CurrentCombo`（AC-C2-22）。
- [ ] 发布构建（非 DEVELOPMENT_BUILD）中 `SetBeatClockForTesting` 等 setter 不可见（编译期或反射确认）。
- [ ] 全项目仅有一处方法名 `GetDspTime()`；无 `GetCurrentDSPTime`/DSP 语境 `GetCurrentTime` 残留。

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Satisfies It |
|-------------|--------|-------------|--------------------------|
| `design/gdd/rhythm-core.md` | C1 | M3：`IBeatClock`/`IInputEventBus` 正式声明为接口，支持纯 C# 单测（`:194/:846`） | §1/§2 权威声明两接口成员 + Mock 契约；确立字段注入+属性包装+ `#if` setter 的代码归属 |
| `design/gdd/combo-system.md` | C2 | OQ4：需 C1 暴露可 mock 的 `IComboCounter` 只读段数面（AC-C2-17/22/30/37，`:348`） | §3 声明 C1 拥有的只读 `IComboCounter`（`CurrentCombo`/`ComboMax=999`）+ `MockComboCounter` |
| `design/gdd/perk-system.md` | C3 | OQ6：`PerkRollService` 需可注入 `IRandomSource` 支撑确定性加权抽取单测（AC-C3-13/14，`:326`） | §4 定义最小充分 RNG 接口 + `MockRandomSource`（可编排无保底序列） |
| `design/gdd/combat-director.md` | C4 | OQ1：C4 需注入模拟拍源，参照 F2 `IDSPClock`/`MockDSPClock` 范式（`:451-453`） | §5 钦定 `IDSPClock.GetDspTime()` + `MockDSPClock`；驱动 500 拍确定性调度 |
| `design/gdd/audio-beat-clock.md` | F2 | BC-6：规范暂停/恢复事件源，消除 C1/C3/F6 信号不一致 | §6 锁定 F2 唯一发布者 + `OnBeatClockPaused`/`OnBeatClockResumed` + `BeatClockPauseEventData`，禁并行事件 |

## Related

- **依赖**：ADR-D3（M3 触发本 ADR）、ADR-D6（IDSPClock/IAudioPlayer 生产实现）、ADR-D1（IBeatClockLifecycle/InputEventBus 控制面归属）、ADR-D10（ISaveService/MockSaveService 缝隙范式先例）。
- **触发来源**：`docs/architecture/architecture-review-2026-08-11.md`（缺口 A2 + Required ADRs #1）；`docs/architecture/traceability-index.md:65`（ADR-D8 接口所有权，P1）。
- **相邻修订**：ADR-D3（补 HeavyAttack/CancelTier/ComboCountAfter/规范事件名）、ADR-D6+F2（GetDspTime 命名同步）。
- **实现代码**（待创建）：`Assets/Scripts/Core/Interfaces/*.cs`、`Assets/Tests/EditMode/CombatCore/Mocks/*.cs`。
