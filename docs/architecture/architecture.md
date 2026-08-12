# 玉兔：破阵 (Jade Rabbit: Warsong) — Master Architecture

> 本文件是本项目**唯一权威的整系统技术蓝图**——所有未来门禁（gate-check）、ADR、
> 实现冲刺以此为硬长杆真值。个别点决策记录在 `docs/architecture/adr-d*.md`（ADR）；
> 本文件给出这些 ADR 赖以成立的整系统语境、分层、所有权、数据流与接口边界。
> 与单个 ADR 冲突时，若 ADR 更晚且状态为 Accepted，以 ADR 为准并在此回填。

## Document Status

| 字段 | 值 |
|------|-----|
| **Version** | 1.0 |
| **Last Updated** | 2026-08-12 |
| **Engine** | Unity 6.3 LTS (6000.3.19f1) |
| **Rendering** | Universal Render Pipeline (URP) |
| **GDDs Covered** | F1 concept · F2 audio-beat-clock · F3 input-event-system · F4 so-data-layer · F5 save-persistence · F6 tde-integration · C1 rhythm-core · C2 combo-system · C3 perk-system · C4 combat-director |
| **ADRs Referenced** | D1, D2, D3, D4, D5, D6, D7, D8, D10（全 Accepted）· D12, D13（Accepted）· D14（**Proposed**）。D9/D11 号段从未创建（编号跳空，非悬空依赖） |
| **Traceability** | `traceability-index.md` Rev.10 / `tr-registry.yaml` v3（152 TR — 89 Covered / 17 Partial / 46 Gap = ~58.6%） |
| **Latest Arch Review** | `architecture-review-2026-08-12-coverage.md` — **CONCERNS（健康，0 架构阻塞项）** |
| **Technical Director Sign-Off** | 2026-08-12 — **APPROVED WITH CONDITIONS**（见 §Sign-Off） |
| **Lead Programmer Feasibility** | SKIPPED — Lean review mode（`production/review-mode.txt` = lean，LP-FEASIBILITY 非 PHASE-GATE） |

---

## Engine Knowledge Gap Summary

Unity 6.3 LTS 大幅晚于 LLM 训练截止（约 2022.3 / Unity 2022 LTS）。整个 Unity 6 系列
均为 post-cutoff——以下域的 AI 建议在采纳前**必须**对照 `docs/engine-reference/unity/`。

### HIGH RISK 域（本项目触及）

| 域 | post-cutoff 变更 | 本项目影响面 |
|----|-----------------|-------------|
| **Input System** | Legacy `Input.GetKeyDown()` 废弃；须用 `InputAction.performed` callback 时间戳 | F3 输入管线核心——节奏判定的时间戳精度前提（消除 16.7ms 帧量化误差） |
| **URP Compatibility Mode 移除** | 6.3 完全移除；自定义 Pass 须用 `RecordRenderGraph(RenderGraph, ContextContainer)`，旧 `Execute(...)` 签名作废 | PL2 VFX / 任何自定义渲染 Pass（MVP 暂无，但 Polish 层须注意） |
| **`[SerializeField]` 仅限字段** | 6.3 起作用于属性触发 CS0592 | **全部 SO**（PlayerInfoSO/WeaponSO/PerkSO 等，ADR-D4）+ Odin 字段设计 |

### MEDIUM RISK 域（本项目触及）

| 域 | post-cutoff 变更 | 本项目影响面 |
|----|-----------------|-------------|
| **Addressables** | 6.2+ 加载失败**默认抛异常**（非静默 null） | F4/C2/C3 所有 `LoadAssetAsync<T>()` 须 try/catch（ADR-D4） |
| **Physics** | `autoSyncTransforms` 弃用 → 显式 `Physics.SyncTransforms()`；`defaultSolverIterations` 默认值提升 | TDE Rigidbody 物理（F6）、C6 Hitbox |
| **第三方包（Animancer / FMOD）** | Animancer 8.4.0 `HybridAnimancerComponent`/`PlayController()`；FMOD Core `getDSPClock` 须走 master channel group | ADR-D5 / ADR-D6，已经 M0 Spike 源码验证 |

### 不适用（已排除）

- **DOTS/ECS**：本项目为 TDE + MonoBehaviour 架构，不使用 Entities——HIGH 风险「Entities API 覆盖」与本项目无关。
- **Netcode 2.x**：首发单机无网络，暂不受影响。

> **实现期硬门**：涉及第三方库成员名（TDE 事件枚举、FMOD API、Animancer 方法）时，
> **禁止凭训练数据臆断**。教训见 ADR-D1 `.Respawn` → `.RespawnComplete`（不存在的成员名）。
> ADR-D13 的 TDE 胜利/通关事件成员名仍**未核定**（Verification Required，F6/FE5 实装前硬门）。

---

## System Layer Map

31 个系统（含推断）映射到 5 层业务栈 + 底层平台层。层间依赖严格单向向下（无循环依赖，见 §Data Flow）。

```
┌──────────────────────────────────────────────────────────────────────┐
│ PRESENTATION   P1 HUD · P2 心法选择 · P3 局结算 · P4 局外成长菜单       │  订阅节奏事件总线
│                P5 暂停 · P6 校准UI · P7 主菜单 · P8 设置菜单             │  只读游戏状态
├──────────────────────────────────────────────────────────────────────┤
│ POLISH         PL2 VFX与节奏反馈 · PL3 教程引导 · PL4 无障碍设置         │  订阅评级/节奏事件
├──────────────────────────────────────────────────────────────────────┤
│ FEATURE        FE1 敌人AI · FE2 房间 · FE3 延迟校准 · FE4 Boss战         │  完整单局流程
│                FE5 局外成长 · FE7 设置 · PL1 相机                        │
├──────────────────────────────────────────────────────────────────────┤
│ CORE           C1 节奏量化攻击 · C2 连招图 · C3 心法构筑                 │  30秒核心循环
│                C4 战斗导演 · C5 武器 · C6 Hitbox判定                     │  [CombatCore.asmdef]
├──────────────────────────────────────────────────────────────────────┤
│ FOUNDATION     F2 音频/拍点时钟 · F3 Input事件 · F4 SO数据层             │  技术地基，无业务依赖
│                F5 存档持久化 · F6 TDE&Animancer集成                      │
├──────────────────────────────────────────────────────────────────────┤
│ PLATFORM       Unity 6.3 LTS · URP · FMOD Studio 2.x · TopDownEngine    │  引擎/中间件 API 表面
│                4.5 · Animancer 8.4 · EasySave 3 · Unity Behavior 1.0    │
└──────────────────────────────────────────────────────────────────────┘
```

### 层级职责与状态

| 系统 | 层 | 里程碑 | 状态 | 主导 ADR |
|------|-----|--------|------|---------|
| F1 Game Concept | Foundation | — | Approved | — |
| F2 音频/拍点时钟 | Foundation | MVP | **Approved** | D6, D10 |
| F3 Input 事件 | Foundation | MVP | **Approved** | D1（控制面）, D8（读取面） |
| F4 SO 数据层 | Foundation | MVP | **Approved** | D4 |
| F5 存档持久化 | Foundation | MVP | **Approved** | D7, D10 |
| F6 TDE & Animancer 集成 | Foundation | MVP | **Needs Revision** | D1, D5, D10, D12, D13 |
| C1 节奏量化攻击 | Core | MVP | **Needs Revision** | D3, D8, D12, D13 |
| C2 连招图 | Core | MVP | **Approved** | D4, D5, D8, D14(Proposed) |
| C3 心法构筑 | Core | MVP | **Approved** | D4, D8 |
| C4 战斗导演 | Core | MVP | **Approved** | D2, D6, D8, D10, D12, D13 |
| C5 武器 / C6 Hitbox | Core | MVP | Not Started | D4（schema） |
| FE1 敌人 AI | Feature | MVP | Not Started | D2 |
| FE3 延迟校准 | Feature | MVP | Not Started | D6, D10（offset 写者） |
| FE2/FE4/FE5/FE7/PL1 | Feature | MVP | Not Started | — |
| P1–P8 | Presentation | MVP | Not Started | — |
| PL2/PL3/PL4 | Polish | MVP–Full | Not Started | — |

> **边界合约（technical-preferences.md 强制）**：`CombatCore.asmdef`（Core 层）负责节奏量化与
> 连招逻辑，**不 `using` TopDownEngine / FMOD / Feel 命名空间**；TDE 负责移动/生命/关卡/反馈。
> 两者只通过 F6 桥接的**纯 C# 域事件**（payload 不带 TDE 类型）通信。

---

## Module Ownership

格式：**Owns**（独占的数据/状态）· **Exposes**（对外暴露的接口/事件）· **Consumes**（读取的外部接口）· **Engine/Plugin API**（风险标注）。

### Foundation Layer

| 模块 | Owns | Exposes | Consumes | Engine/Plugin API（风险） |
|------|------|---------|----------|--------------------------|
| **BeatClock**（F2, ADR-D6） | BPM→拍间隔→事件调度数学；`LatencyOffsetMs`（被注入的持有者） | `IBeatClock`（8 事件 + snapshot，见 §API）· `IBeatClockLifecycle`（Play/Pause/Resume/Stop，F6 专属） | `IDSPClock.GetDspTime()`, `IAudioPlayer` | FMOD `ChannelGroup.getDSPClock`（post-cutoff, master group）· `getSoftwareFormat`（除数）· `Channel.setDelay`（HIGH，非 setPosition） |
| **FMODDSPClockImpl / FMODAudioPlayerImpl**（F2, ADR-D6） | `_channel`/`_sound`（`FMOD_CREATESAMPLE` 预加载） | `IDSPClock` · `IAudioPlayer.PlayScheduled/Stop/IsPlaying` | `FMODUnity.RuntimeManager.CoreSystem` | FMOD Core API（**禁止**原始 `fmod.cs` 绑定，只走 RuntimeManager） |
| **InputEventBus**（F3, ADR-D1/D8） | `CurrentState`（Gameplay/UI/Disabled 输入状态机） | `IInputEventBus`（读取面）· 控制面 `EnableGameplayInput/EnableUIInput/DisableAllInput`（F6 专属） | Input System callback | Input System `InputAction.performed`（HIGH，须用 callback 时间戳非 Update） |
| **JadeRabbitInputManager**（F6, ADR-D1） | `BindButton` 覆写形成的输入门控 | `protected override BindButton(...)` | `InputEventBus.CurrentState`（只读） | TDE `InputSystemManagerEventsBased`（唯一 virtual 方法；**禁移除**，破坏 AbilityInputAuthorization） |
| **7 类 SO**（F4, ADR-D4） | 各自序列化字段（运行时**只读**；`PerkSO` 唯一可 `Instantiate` 克隆） | 字段访问器（见 §API SO schema） | — | `AssetReferenceT<T>`（Addressables 2.x，MEDIUM，加载抛异常须 try/catch）· Odin `#if UNITY_EDITOR`（**禁 Runtime `using Sirenix.*`**） |
| **SaveService / ES3Storage**（F5, ADR-D7/D10） | 状态机 `Uninitialized→Idle→Saving/Loading→Error`；主线程 ID 校验；AES-128 配置 | `ISaveService`（同步 `Initialize()`, `LoadLatencyCalibration()` 等）· `IFileStorage`（存储后端抽象） | — | EasySave 3 `ES3.Save/Load/...`（稳定，**只在 ES3Storage 内引用**；**禁 ES3Cloud**） |
| **TDELifecycleAdapter**（F6, ADR-D1/D10/D12/D13） | `BeatClock.Play/Pause/Resume/Stop` **唯一**控制权；`_fmodVerified`/`_runEnded`/`_offsetInjected` latch | `RunOutcome` enum · `event Action<RunOutcome> OnRunEnded` · `void EndRun(RunOutcome)` · `event Action OnRespawnComplete`（MVP 休眠） | `ISaveService.IsInitialized/LoadLatencyCalibration`, `IBeatClockLifecycle`, `IInputEventBus`(控制面), `LevelConfigSO`, `RuntimeManager.IsInitialized` | TDE `MMEventListener<TopDownEngineEvent>`（post-cutoff, M0 验证）· `SceneManager.sceneLoaded` |
| **AnimancerAttackPlayer**（F6/Core, ADR-D5） | `_currentState`/`_pendingOnComplete`/缓存委托（零 per-attack GC） | `PlayAttack(node, clip, onComplete)` · `StopAttack()` · `UpdateBpm(float)` | `BeatClock.CurrentBpm`（只读拉取）；C2 调用 PlayAttack | Animancer 8.4.0 `HybridAnimancerComponent.PlayController()`（MEDIUM，**禁 `Stop()` 交还控制**）· `AnimancerState.Speed/Events.OnEnd`（已验证） |

### Core Layer（`CombatCore.asmdef`）

| 模块 | Owns | Exposes | Consumes | Engine/Plugin API（风险） |
|------|------|---------|----------|--------------------------|
| **RhythmJudger**（C1, ADR-D3/D8） | `_pendingInput`（单槽缓冲）· `_lateWindowActive`/expiry · `CurrentCombo`（唯一写者）· 判定网格锚（绑 `OnMeterChanged`） | `RhythmJudgmentEvent`（readonly struct）· `event OnRhythmJudgment` · `QuantizationMode Mode`（FE7 注入）· `IComboCounter`（C2 读段数） | `IBeatClock`（lookahead/exact/paused 事件、IsStartConfirmed）· `IInputEventBus.OnAttackInput` · `IAnimancerAttackPlayer.Play` · `RhythmConfigSO` · `OnRunEnded`（SC-2 清缓冲） | `Update()` + `Time.realtimeSinceStartup`（LateWindow 过期，M2: 30fps 移动端 33ms 误差，post-MVP 切 DSP 域）；无 post-cutoff API（LOW） |
| **ComboPlayer**（C2, ADR-D4/D5/D14） | 连招图运行时游标；BD-2 取消窗计算 | 连招推进 API（C2 GDD） | `ComboGraphSO`/`AttackNodeSO`（Addressables 预加载）· `IComboCounter.CurrentCombo`（评级控制取消窗）· `IBeatClock.OnMeterChangeAccepted/OnMeterChanged`（BD-2）· `AnimancerAttackPlayer.PlayAttack` | Addressables `LoadAssetAsync<AnimationClip>`（MEDIUM，退出 `Release`）；**禁即时路径 Load**（ADR-D4 Rule D4-4） |
| **PerkRuntime**（C3, ADR-D4/D8） | `PerkSO` 克隆实例（`Instantiate`，局结束 `Destroy`+`Release`）；心法冻结栈计时 | 心法效果注入（修改 C1 节奏规则） | `PerkSO`（schema）· `IComboCounter`/`IBeatClock`（规则修改）· `IRandomSource`（可复现随机）· `OnBeatClockPaused/Resumed`（冻结栈） | Addressables；`System.Random`（经 `IRandomSource`，固定种子可复现，LOW） |
| **CombatDirector**（C4, ADR-D2/D6/D10） | 拍槽分配（SlotCapacity）· 令牌 Pending/Granted 队列 | 攻击令牌协议 `RequestAttackToken()`/`OnTokenGranted()`（C4 GDD） | `IBeatClock`（拍槽/拍点时序）· `IDSPClock`（绝对拍）· `EnemyInfoSO.attackWindupBeats` · `OnRunEnded`（SC-3 令牌回收） | 无 post-cutoff（LOW）；绝对拍跨 `Stop()` 须失锚防护 |

### Feature Layer（选摘 MVP 关键）

| 模块 | Owns | Exposes | Consumes | Engine/Plugin API（风险） |
|------|------|---------|----------|--------------------------|
| **敌人 AI**（FE1, ADR-D2） | `BehaviorGraphAgent` 行为决策；`BeatClockBridge` 写 Blackboard | 自定义 Action 节点（`CombatDirectorTokenAction` 等） | `BeatClock.OnBeatSnapshot`（**经 Bridge 注入 Blackboard**，非直接）· C4 令牌协议 · TDE `CharacterMovement.SetMovement(Vector2)` | Unity Behavior `com.unity.behavior` 1.0.16（LOW，M0 0 BLOCKER）；节点须 `partial class + [GeneratePropertyBag]` |
| **延迟校准**（FE3, ADR-D6/D10） | 校准流程 UI 状态 | 校准偏移写入 | `BeatClock.LatencyOffsetMs`（**唯一运行时写者**，ADR-D10 D-5）· `ISaveService`（偏移持久化） | 无 post-cutoff |

---

## Data Flow

四个关键场景。所有跨模块通信为**主线程内**同步调用或纯 C# event（零跨线程 marshal，ADR-D6 线程模型）。

### 1. 初始化 / 启动时序（ADR-D10，权威有序序列）

```
[Awake 阶段 — Unity 保证所有 Awake() 先于任何 Start()]
  1. SaveService.Awake()          注册单例，不加载（Rule 14）
  2. BeatClock.Awake()            注册单例，DontDestroyOnLoad
  3. TDELifecycleAdapter.Awake()  持有 ISaveService + IBeatClockLifecycle 引用

[Start 阶段 — 单一显式 bootstrap driver 强制顺序（禁用 Script Execution Order）]
  4. SaveServiceBootstrap.Start(): saveService.Initialize()  ← D-4 同步阻塞，加载校准 DTO
  5. TDELifecycleAdapter.Start():  VerifyFMOD() / VerifyFMODAsyncMode() → 缓存 _fmodVerified（只读探测）

[OnEnable/OnDisable — ADR-D1 权威订阅点（早于 Start，仅连线不触发）]
     MMEventStartListening<TopDownEngineEvent>() ; SceneManager.sceneLoaded += OnSceneLoaded

[首次 OnSceneLoaded(LoadSceneMode.Single) — ADR-D1 拥有 Play()/EnableGameplayInput()]
  6. if (mode != Single) return;                                   附加加载过滤
  7. if (!_fmodVerified || !saveService.IsInitialized){LogFatal;return;}  release-active 早退 guard（非 Debug.Assert）
  8. if (!_offsetInjected){ beatClock.LatencyOffsetMs = saveService.LoadLatencyCalibration().offsetMs;
                            _offsetInjected = true; }              D-5 boot-inject（先于首次 Play）
  9. beatClock.Play(clip, bpm, timeSig)                            ADR-D1 权威位置
 10. EnableGameplayInput()                                         时钟启动+offset 注入之后
```

**关键不变式**：`Initialize()` 全同步 → 步骤 4 完全先于步骤 5 读取，无需 async/超时；offset 注入保证先于首次 `Play()` / 首次 `OnBeatExact` 广播 / 输入启用——不存在被评估的「stale offset=0」拍子。

### 2. 帧内战斗路径（Input → 判定 → 动画）

```
FMODUnity.RuntimeManager.Update()   （主线程，自动 hook，先于 BeatClock.Update）
        ▼  新鲜 DSP clock
Input System InputAction.performed callback  →  InputEventBus.OnAttackInput(AttackInputEvent{ inputDspTime, ... })
        ▼
BeatClock.Update(): dspNow = IDSPClock.GetDspTime()
        │  若 dspNow >= nextBeatDspTime → 直接 DispatchBeatEvent()（无队列、无 ConcurrentQueue）
        ▼  OnHalfBeatLookahead / OnBeatExact / ...
RhythmJudger: 缓冲 _pendingInput → lookahead 扫描评级 → 计算 offsetMs vs 拍点
        ▼  OnRhythmJudgment(RhythmJudgmentEvent{ Rating, OffsetMs, ComboCountBefore })
   ┌────────────┬───────────────┬──────────────┐
   ▼            ▼               ▼              ▼
 C2 连招推进   C1.CurrentCombo   P1 HUD 评级    PL2 VFX 节奏反馈
 AttackPlayer  自增/清零          显示           (Feel/MMFeedbacks 订阅)
 .PlayAttack(node, clip, onComplete)
        ▼
 AnimancerState.Speed = CurrentBpm / AuthoredBpm   （BPM 自适应变速，ADR-D5）
```

### 3. 事件/信号路径（解耦总线，零 GC）

- **唯一时钟真值**：`AudioSettings.dspTime` 域（**禁混入 `Time.time` 采样**，ADR-D14 陷阱 1）。
- **唯一暂停/恢复发布者**：BeatClock（F2）经 `OnBeatClockPaused`/`OnBeatClockResumed`——**禁止** C1/C3/F6 各自定义并行的 `OnGamePaused/OnGameResumed`（ADR-D8 禁止模式）。
- **零 GC 广播**：所有 payload 为 `readonly struct`；**禁 `GetInvocationList()`**、**禁**逐帧 lambda 动态订阅（ADR-D8）。
- **CombatCore 隔离**：C1–C4 只订阅 F6 桥接的纯 C# 事件，**禁 `using TopDownEngine`**。

### 4. Run-End 统一路径（死亡 + 胜利，ADR-D12 + D13）

```
触发源 A: TDE 关卡完成事件（MMEventListener，成员名待核实 — VR1 硬门）
触发源 B: FE4(Boss死) / FE2(清场) 显式调用
触发源 C: TDE PlayerDeath 事件（MMEventListener，ADR-D1 既有桥接）
        └──────────────┬──────────────┘
                       ▼
    F6 TDELifecycleAdapter.EndRun(RunOutcome outcome)
                       │  [_runEnded latch — 已闭锁则 no-op 早退]
                       ▼  （首次进入，同一 Update 帧内）
    1. BeatClock.Stop()                    时钟停止 owner = F6（唯一，归零 global_beat_index）
    2. InputEventBus.DisableAllInput()
    3. 广播 OnRunEnded(RunOutcome outcome)   outcome ∈ {Death, Victory}
        ┌──────────┬──────────┬──────────────┐
        ▼          ▼          ▼              ▼
    C1 SC-2     C4 SC-3      FE5           未来订阅者
    清缓冲       令牌回收      merit 折算/结算UI（按 outcome 分支）
                       ▼
    「下一局」= 新 LoadSceneMode.Single → OnSceneLoaded → offset guard(已注入跳过) → Play() → EnableGameplayInput()
    （Stop() 已归零 + 清 Pending → 干净起局，无 double-Play，无残留拍序）
```

**Owner 总结**：`Stop()` = F6（唯一，Death/Victory 对称）· 缓冲清空 = C1 自订阅 · 令牌回收 = C4 自订阅 · 重复触发防护 = F6 `_runEnded` 一次性闭锁。`RespawnComplete`（MVP 若误触发）：仅 `[F6-FATAL]` 日志 + 早退，**绝不静默 `Play()`**（fail-loud）。

### 依赖有向性

无循环依赖（systems-index 逐条核验）。跨切面 ADR 依赖链：D12 依赖 D1+D10；D13 Extends（非 Supersede）D12，依赖 D12+D1+D10；D14 依赖 D3+D8。帧预算合计 ≈0.73ms/帧 ≪ 16.6ms（60fps）。

---

## API Boundaries

模块间公共契约。CombatCore 消费者面接口在 `Assets/Scripts/Core/Interfaces/`（ADR-D8）。

### 五个 DI 消费者缝隙接口（ADR-D8，实现/Mock 二元对，解锁 80% 单测硬门）

```csharp
// §1 IBeatClock.cs — 只读消费者面（生命周期方法归 IBeatClockLifecycle，编译期隔离）
public interface IBeatClock {
    event Action<BeatEventData>          OnBeatLookahead;
    event Action<BeatEventData>          OnHalfBeatLookahead;
    event Action<BeatEventData>          OnBeatExact;
    event Action<BeatEventData>          OnHalfBeatExact;
    event Action<MeterChangeEventData>   OnMeterChangeAccepted;
    event Action<MeterChangeEventData>   OnMeterChanged;
    event Action<BeatClockPauseEventData> OnBeatClockPaused;
    event Action<BeatClockPauseEventData> OnBeatClockResumed;
    BeatStateSnapshot GetSnapshot();
    float     LatencyOffsetMs { get; }
    IDSPClock DSPClock { get; }
    double    GetBeatIntervalSeconds();
    bool      IsStartConfirmed { get; }
    double    GetConfirmedDSPStart();   // 未确认返回 -1.0
}

// §2 IInputEventBus.cs — 读取面（控制面 Enable*/Disable* 归 F3，F6 专属）
public interface IInputEventBus {
    event Action<AttackInputEvent> OnAttackInput;
    event Action<DashInputEvent>   OnDashInput;
    event Action                   OnPause;
    Vector2 GetCurrentMoveInput();
    bool    IsChargedAttackHeld { get; }
}

// §3 IComboCounter.cs — C1(RhythmJudger) 拥有的只读段数面
public interface IComboCounter { int CurrentCombo { get; } int ComboMax { get; } }  // ComboMax 固定 999

// §4 IRandomSource.cs — 可复现随机（C3 消费）
public interface IRandomSource { int Next(int maxExclusive); double NextDouble(); void Reseed(int seed); int Seed { get; } }

// §5 IDSPClock.cs — 规范方法名钦定 GetDspTime（取代历史 GetCurrentTime/GetCurrentDSPTime 三处分歧）
public interface IDSPClock { double GetDspTime(); }
```

**Payload structs（全 `readonly struct`，零 GC）**：`BeatEventData{ BeatDspTime, BeatIndexInMeasure, MeasureIndex, GlobalBeatIndex, Bpm }` · `BeatStateSnapshot{ Bpm, Numerator, BeatProgress, ...NextBeatDspTime, ClockState }` · `MeterChangeEventData{ OldBpm, NewBpm, OldNumerator, NewNumerator, EffectiveDspTime }` · `BeatClockPauseEventData{ IsResume, ResumeFirstBeatDspTimeRaw, ..., FractionalProgressAtPause }` · `AttackInputEvent{ attackType, inputDspTime, chargeStartDspTime, inputGameTime, deviceType, deviceId }`。枚举：`AttackType{LightAttack,HeavyAttack,ChargedAttack}` · `RhythmRating{Perfect,Good,Miss}` · `QuantizationMode{Off,HalfBeat,Beat}` · `BeatClockState{Stopped,Playing,Paused,PendingBPMChange,PendingTimeSignatureChange}`。

### F2 生命周期控制面（ADR-D6，F6 专属）

```csharp
public interface IBeatClockLifecycle { void Play(string bgmPath, float bpm, TimeSignature ts); void Pause(); void Resume(); void Stop(); }
public interface IDSPClock  { double GetDspTime(); }
public interface IAudioPlayer { void PlayScheduled(double dspTimeSeconds); void Stop(); bool IsPlaying { get; } }
```
> **不变式**：除 `TDELifecycleAdapter` 外**任何系统不得调用** Play/Pause/Resume/Stop（`beatclock_lifecycle_bypass` 禁止模式，编译期由消费者面不含这些方法保证）。

### F5 存档服务面（ADR-D7/D10）

```csharp
public interface ISaveService {
    void Initialize();                              // 同步阻塞、主线程
    bool IsInitialized { get; }
    LatencyCalibrationDTO LoadLatencyCalibration(); // 缺失默认 {0f, isCalibrated=false}
    void SaveLatencyCalibration(LatencyCalibrationDTO dto);
    // + LoadSettings() / LoadSlotProgress(int) / GetActiveSlotIndex()
}
public interface IFileStorage {                     // 存储后端抽象（ES3Storage / MockFileStorage）
    void Save<T>(string path, string key, T value);
    T    Load<T>(string path, string key, T defaultValue);
    bool KeyExists(string path, string key); bool FileExists(string path);
    void DeleteFile(string path);                   // 幂等
    void RenameFile(string src, string dst);        // 覆盖式（损坏文件 → .es3.corrupt）
}
```

### F6 Run-End 域契约（ADR-D12/D13）

```csharp
public enum RunOutcome { Death, Victory }           // 纯 C# enum，禁引用 TDE 类型；完整取值 defer FE5
public event Action<RunOutcome> OnRunEnded;         // 取代 OnPlayerDeath（完全替代，非并存别名）
public void EndRun(RunOutcome outcome);             // 单一入口，_runEnded latch 保护，Death/Victory 汇入
```

### F4 SO Schema（ADR-D4，运行时只读，字段 `snake_case _id`）

`AttackNodeSO`{ _id, _animationClip(`AssetReferenceT<AnimationClip>`), _hitboxStartNormalizedTime, _hitboxDurationNormalized, _cancelWindowType, _cancelWeight, _quantizationHint, **_authoredBpm**(ADR-D5) } · `ComboGraphSO`{ _weaponType, _nodes(同 Addressable Group), _rootNodeIndex, _edges } · `PerkSO`（**唯一可变**）{ _rarity, _effectType, _parameters(`List<PerkParameter>` 值类型不变量 D4-9), _maxStacks, _stackBehavior } · `WeaponSO` · `EnemyInfoSO`{ _attackWindupBeats, _beatPhaseAnchor, _rewardMeritBase } · `LevelConfigSO`{ **_bgmFilePath**(FMOD 路径非 AssetRef), _bpm, _timeSignature* } · `PlayerInfoSO`{ _playerMerit, _unlockedWeaponIds, `[NonSerialized]` latencyCalibrationOffsetMs / audioVolumeMaster }。

### C2 BD-2 取消窗契约（ADR-D14，**Proposed**）

保护窗 `[t_accept, t_boundary + AdaptationWindowBeats×fullBeatMs]` 内 `cancelWindowMs_effective = max(cancelWindowMs(bpm_before), cancelWindowMs(bpm_after))`；窗外取 `bpm_after`；拍号-only 变更不启用。C2 订阅点前移至 `OnMeterChangeAccepted`（C1 判定网格重锚仍绑 `OnMeterChanged`，**不变**）。复用 `RhythmConfigSO.AdaptationWindowBeats`（=2），禁引入平行常量。

---

## ADR Audit

12 个 ADR（D1–D8, D10, D12, D13, D14）。审计基准：`architecture-review-2026-08-12-coverage.md`。

| ADR | 标题 | Status | 引擎兼容段 | GDD 链接 | 冲突 | 有效 |
|-----|------|--------|-----------|---------|------|------|
| D1 | TDE Integration | Accepted | ✅ (M0 Spike) | F6 | 无（A1 已消解） | ✅ |
| D2 | Enemy AI (Unity Behavior) | Accepted | ✅ (M0 Spike) | FE1, C4 | 无 | ✅ |
| D3 | Rhythm Quantization Core | Accepted | ✅ | C1 | 无 | ✅ |
| D4 | SO Data Layer | Accepted | ✅ | F4 全覆盖 | 无 | ✅ |
| D5 | Animation Pipeline | Accepted | ✅ (PlayMode 验证) | F6, C2 | 无（A5 [60,240] 统一） | ✅ |
| D6 | Audio Middleware (FMOD) | Accepted | ✅ (M0 Spike) | F2 | 无 | ✅ |
| D7 | Save/Persistence (ES3) | Accepted | ✅ (M0 Spike) | F5 | 无 | ✅ |
| D8 | DI / Test Seams | Accepted | ✅ (step-1 spike + CI) | C1–C4 | 无（A2/A3/A4 已归属） | ✅ |
| D10 | Bootstrap Sequencing | Accepted | ✅ | F2/F5/F6 | 无（A1 让步 handler 早退 guard） | ✅ |
| D12 | Death & Run-Lifecycle | Accepted | ✅ | F6/C1/C4 | 无（闭合 SG-2） | ✅ |
| D13 | Run-End Victory Unification | Accepted (TD+GD 会签) | ⚠️ VR1（TDE 胜利事件成员名待核） | F6/C1/C4/FE5 | 无（闭合 W-F） | ✅ |
| D14 | BD-2 Cancel-Window Transient | **Proposed** | ✅ (LOW) | C2 | 无 | ⚠️ 待 Accept |

**跨 ADR 冲突（本审计）**：**0 blocking**。历史张力 A1（D1/D10 生命周期）→ 真实消解（D1 订阅唯一权威，D10 让步 handler 早退 guard，逐字一致代码）；A2（接口所有权）→ D8 归属；A3/A4（命名/schema）→ D8 §1/§6 权威化；A5（authoredBpm 边界）→ D4/D5 统一 [60,240]。残留 LOW 文档漂移：D3 未同步 `OnBeatClockPaused` 规范名 + `AttackType.HeavyAttack`（D8 Migration §2 已登记）；E1（D2 同帧 beat 保证）仍 advisory。

**Traceability 覆盖**：152 TR — **89 Covered / 17 Partial / 46 Gap（~58.6%）**。Core 四系统（C1–C4）Gap = **0**（跨切面 ADR + D8 DI 缝隙 + D12/D13 run-end 全承接）。46 Gap 中 37 项为 Foundation GDD=规范项（beatclock 13 / input 13 / save 11，内部设计无需单独 ADR，**预期缺口**），真实架构/未来 GDD 缺口仅 concept 5 + tde 4 = 9 条。

---

## Required ADRs

按优先级（Foundation 先）。当前**无新 Foundation/Core 层 ADR 缺口**——run-end 生命周期已由 D12/D13 承接。

**唯一可行动项：**
- **ADR-D14 → Accepted**（BD-2 取消窗瞬态）— 已 Proposed，待会签。C2 BD-2 故事在 D14 Accepted 前自动阻塞（C2 实装未开工，**不阻塞当前 C1 冲刺**）。

**未来 GDD 承接（非当前架构阻塞）：**
- `TR-concept-011` 延迟校准 → **FE3 GDD**（P2）
- `TR-concept-014` 多武器 ComboGraph → **C5 GDD**（P2）
- `PlayerInfoSO.unlockedPerkIds`（C3-OQ1）→ **F4 schema 补充**（P2）
- FE5 承接（D12/D13 Open Follow-up，无 GDD 故非缺口计数）：结算经济学（胜利 vs 死亡 merit 折算差异，game-designer 会签要求「显式设计，不得默认对称」）· `RunOutcome` 完整取值（Death/Victory/Retreat/Quit?）· 防退出套利 · 胜利判定唯一 fire owner（FE4/FE2）。

---

## Architecture Principles

管辖本项目所有技术决策的 5 条原则（源自游戏支柱 + GDD + technical-preferences）：

1. **节奏层是物理底层，不可绕过。** 攻击量化到拍点执行，判定基于 `AudioSettings.dspTime` 采样级时钟。**禁止 `Update()` 轮询做节拍判定**（60fps 下 16.7ms 误差 > 判定窗容差）——必须用 Input System callback 时间戳。禁止用固定帧数攻击动画（须 `AnimancerState.Speed` 按 BPM 缩放）。

2. **TDE 是租户，不是地基。** 禁止修改 TDE 源码（升级会覆盖；v1 留下 582 行覆写陷阱）。只允许继承 override + 事件订阅。`CombatCore.asmdef` 不 `using` TDE/FMOD/Feel，只通过 F6 桥接的纯 C# 域事件通信（payload 不带 TDE 类型）。

3. **单一权威 + 单一写者。** BeatClock 生命周期唯一控制者 = F6 TDELifecycleAdapter；暂停/恢复事件唯一发布者 = F2；`LatencyOffsetMs` 运行时唯一写者 = FE3；run-end 唯一入口 = F6 `EndRun()`（latch 防重复）；`CurrentCombo` 唯一写者 = C1。消除竞态与双触发。

4. **可测缝隙优先于单例。** 五个 DI 消费者接口（IBeatClock/IInputEventBus/IComboCounter/IRandomSource/IDSPClock）+ Mock 二元对，解锁 80% 单测硬门。纯逻辑下沉为无 MonoBehaviour 依赖的纯 C# 类（构造函数注入），规避 `InternalsVisibleTo`/CS0122。测试注入 setter 仅在 `#if UNITY_EDITOR || DEVELOPMENT_BUILD`。

5. **数据驱动 + 零运行时 GC。** 玩法数值全走 SO（运行时只读，`PerkSO` 唯一可克隆）；大型资产走 `AssetReferenceT<T>` + Addressables（禁 `Resources.Load`）；所有事件 payload 为 `readonly struct`，禁 `GetInvocationList()` / 逐帧 lambda 订阅。Odin 仅限 Editor（禁 Runtime `using Sirenix.*`）。

---

## Open Questions

| ID | 摘要 | 优先级 | 解决路径 |
|----|------|--------|---------|
| QQ-01 | ADR-D14 仍 Proposed → C2 BD-2 故事自动阻塞 | High | D14 会签转 Accepted（C2 实装前） |
| QQ-02 | TDE 胜利/通关事件成员名未核定（`LevelComplete`/`GameOver`?） | High | F6/FE5 实装前核对 `plugins/top-down-engine.md`，回填 `entities.yaml pre_implementation_checks`（VR1 硬门） |
| QQ-03 | C1 / F6 死亡语义清理（Needs Revision，D12/D13 传播残留 WARNING） | Medium | C1/F6 小修订随 D12/D13 落地（死亡语义清理应在实现前完成） |
| QQ-04 | 结算经济学（胜利 vs 死亡 merit 折算差异） | Medium | FE5 GDD（game-designer 会签，显式设计不得默认对称） |
| QQ-05 | 胜利判定唯一 fire owner（FE4 Boss 击败 / FE2 清场） | Medium | FE4/FE2 GDD（D13-5 闭锁为兜底，owner 唯一化为正解） |
| QQ-06 | 音频断开降级承接者（1 帧内暂停 + 视觉节拍指示器备用） | Low | F2/FE7 实装 Edge Case |

---

## Technical Director Sign-Off

**Gate TD-ARCHITECTURE（自审，2026-08-12）— APPROVED WITH CONDITIONS**

| 准则 | 判定 | 依据 |
|------|------|------|
| 每个 GDD 系统有分层归属 | ✅ | 31 系统全部映射（§System Layer Map） |
| 关键跨系统数据流已定义 | ✅ | 4 场景（启动/帧战斗/事件总线/run-end），owner 明确无竞态 |
| API 边界有权威契约 | ✅ | 5 DI 接口逐字（ADR-D8）+ 生命周期/存档/run-end/SO schema |
| 无未消解的跨 ADR blocking 冲突 | ✅ | 0 blocking（A1–A5 全消解，coverage review 确认） |

**Conditions（不阻塞 pre-production gate，须在相应实装前清除）：**
1. ADR-D14 转 Accepted（C2 BD-2 实装前门）。
2. TDE 胜利事件成员名核对（QQ-02，F6/FE5 实装前硬门）。
3. C1/F6 死亡语义清理（QQ-03，Needs Revision → 实现前完成）。

**LP-FEASIBILITY**：SKIPPED — Lean review mode（非 PHASE-GATE）。

---

*本文件由 `/create-architecture`（technical-director 主笔）综合 12 个 Accepted/Proposed ADR、
10 个 GDD、152 条 TR 生成。后续 ADR 或 GDD 修订应同步回填本文件并递增 Version。*
