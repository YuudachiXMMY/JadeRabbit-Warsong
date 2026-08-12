# 玉兔：破阵 (Jade Rabbit: Warsong) — Master Architecture

## Document Status

| Field | Value |
|-------|-------|
| **Version** | 1.0 |
| **Last Updated** | 2026-08-12 |
| **Engine** | Unity 6.3 LTS (6000.3.19f1) |
| **Author** | technical-director（`/create-architecture` 主笔） |
| **Review Mode** | lean（LP-FEASIBILITY 跳过——非 PHASE-GATE） |
| **GDDs Covered** | F1 game-concept, F2 audio-beat-clock, F3 input-event-system, F4 so-data-layer, F5 save-persistence, F6 tde-integration, C1 rhythm-core, C2 combo-system, C3 perk-system, C4 combat-director（10 份 Approved/Needs-Revision GDD） |
| **ADRs Referenced** | ADR-D1..D8, D10, D12, D13（Accepted）; ADR-D14（Proposed）。**D9/D11 号段从未创建（编号跳空，非悬空依赖）** |
| **Traceability Source** | `docs/architecture/traceability-index.md` (Rev.10) · `docs/architecture/tr-registry.yaml` (152 TR, v3) |
| **Technical Director Sign-Off** | 2026-08-12 — APPROVED WITH CONDITIONS（见 §9） |
| **Lead Programmer Feasibility** | SKIPPED — Lean 模式（非阻塞；Production 前若升 full 模式补跑 LP-FEASIBILITY） |

> **本文档定位**：这是把全部已批准 GDD + 已 Accepted ADR 翻译成**具体技术蓝图**的唯一整合视图，
> 位于设计与实现之间，是所有未来门禁（`/gate-check`、`/story-done`、`/architecture-review`）
> 比对的**硬长杆**。它不引入新的绑定架构决策——每一条决策都可回溯到某个 ADR 或 GDD。
> ADR 记录**单点决策**；本文档给这些 ADR 提供**全系统语境**。

---

## 1. Engine Knowledge Gap Summary

> 派生自 `docs/engine-reference/unity/`（VERSION / breaking-changes / current-best-practices）。
> LLM 训练数据约覆盖到 Unity 2022.3；整个 Unity 6 系列（含 6.3 LTS）为**后截断**，模型知识不可靠。
> 凡触及下列 HIGH/MEDIUM 域的架构建议，实现前**必须**对照 `docs/engine-reference/unity/` 复核。

### HIGH RISK 域（模型知识不可信，逐一对照引擎参考）

| 域 | 后截断关键变更 | 涉及系统 | 缓解 |
|----|--------------|---------|------|
| **音频原生插件（FMOD）** | Unity 6.3 原生插件校验管线变更；FMOD 二进制须针对 6000.3 头文件编译；FMOD 非 UPM 包需手动锁版本；`getDSPClock()` 挂 master channel group 而非 `FMOD.System` | F2, ADR-D6 | M0 Spike 已验证 API 表面 + 编译期二进制确认（C# 编译 ✅ / macOS 原生库加载 ✅ / banks 待构建 ⚠️）；见 `docs/engine-reference/unity/plugins/fmod.md`（2026-08-12） |
| **URP RenderGraph** | 6.3 完全移除 URP Compatibility Mode；`RecordRenderGraph()` 取代 `Execute()` | PL2 VFX（MVP 后期） | 自定义 render pass 一律走 RenderGraph API；MVP 不写自定义 pass 则风险延后 |
| **Input System（默认）** | Legacy `Input` 类弃用；必须用 Input System package 的 `InputAction.performed` callback 时间戳 | F3, C1 | 已在 technical-preferences 强制；`InputSystemManagerEventsBased.BindButton` override（ADR-D1） |
| **Entities/DOTS 重构** | ISystem/IJobEntity 全面替代 ComponentSystem | 本项目**不使用 DOTS**（MonoBehaviour 架构） | 不适用；若未来引入需专项 ADR |

### MEDIUM RISK 域（复核关键 API）

| 域 | 后截断关键变更 | 涉及系统 | 缓解 |
|----|--------------|---------|------|
| **`[SerializeField]` 仅限字段** | 6.3 编译期强制（属性上用报 CS0592） | F4 全部 SO、所有注入字段 | 编码标准硬约束；DI 缝隙用 `[SerializeField] private` 字段注入（ADR-D8） |
| **Addressables 抛异常** | 6.2+ 加载失败抛异常而非返回 null | F4（`AssetReferenceT<T>` 加载）、C2 动画加载 | 加载点一律 try/catch（ADR-D4） |
| **Animancer 第三方包** | Animancer 8.4.0 对 Unity 6.3 兼容性 | F6/ADR-D5 攻击层 | 已 PlayMode 验证（2026-07-18） |
| **Physics（Rigidbody）** | `autoSyncTransforms` 弃用 → 须显式 `Physics.SyncTransforms()`；`defaultSolverIterations` 默认值提升 | F6 TDE Rigidbody 移动、C6 Hitbox 判定 | hitbox 激活前确认 transform 已同步；复核 solver 迭代数（单机仍用物理，**非**「不适用」） |
| **Netcode 2.x / HTTP2** | 均有默认行为变更 | 本项目单机，暂不涉及 | 不适用 |

### LOW RISK 域（训练数据内，大体可靠）

- 纯 C# 逻辑（节奏判定、连招图、心法、DI 接口）——无引擎 API 面。
- ScriptableObject / MonoBehaviour 生命周期、`SceneManager.sceneLoaded`、EasySave 3（Moodkie 官方声明 6.x 兼容，无 post-cutoff 顾虑）。

**结论**：唯一 HIGH 风险真正落在 MVP 关键路径上的是 **FMOD 音频中间件（F2/D6）**——已由 M0 Spike 消解 API 表面风险，残留仅编译期二进制确认。其余 HIGH 域（RenderGraph）延后至 PL2。

---

## 2. System Layer Map

> 五层标准游戏架构。每个系统来自 `design/gdd/systems-index.md`（31 个系统），
> 按依赖方向自底向上分层。**下层不得依赖上层**。

```
┌──────────────────────────────────────────────────────────────────────┐
│  PRESENTATION LAYER                                                    │
│  P1 HUD · P2 心法选择UI · P3 结算 · P4 成长菜单 · P5 暂停 · P6 校准UI   │
│  P7 主菜单 · P8 设置菜单                （全部订阅节奏事件总线，只读）   │
├──────────────────────────────────────────────────────────────────────┤
│  POLISH LAYER（横跨 Presentation/Feature，感官强化）                    │
│  PL1 相机 · PL2 VFX与节奏反馈 · PL3 教程 · PL4 无障碍                    │
├──────────────────────────────────────────────────────────────────────┤
│  FEATURE LAYER                                                         │
│  FE1 敌人AI · FE2 房间 · FE3 延迟校准 · FE4 Boss战 · FE5 局外成长        │
│  FE6 程序化生成 · FE7 设置系统                                          │
├──────────────────────────────────────────────────────────────────────┤
│  CORE LAYER（30 秒循环）                                                │
│  C1 节奏量化攻击 · C2 连招图 · C3 心法构筑 · C4 战斗导演                 │
│  C5 武器 · C6 Hitbox判定           【CombatCore 程序集，纯 C# + Mono】  │
├──────────────────────────────────────────────────────────────────────┤
│  FOUNDATION LAYER                                                      │
│  F2 音频/拍点时钟 · F3 Input事件 · F4 SO数据层 · F5 存档 · F6 TDE集成    │
├──────────────────────────────────────────────────────────────────────┤
│  PLATFORM LAYER                                                        │
│  Unity 6.3 · TopDownEngine · Animancer · FMOD Studio 2.x · EasySave 3  │
│  Unity Behavior 1.0.16 · Addressables · Input System · MMFeedbacks/Feel │
└──────────────────────────────────────────────────────────────────────┘
```

### 层内系统 → 层归属 + 引擎风险标注

| 层 | 系统 | 独占拥有（Owns） | 触及 HIGH/MED 引擎域 |
|----|------|-----------------|---------------------|
| **Foundation** | F2 音频/拍点时钟 | DSP 时钟真值、拍点事件、`LatencyOffsetMs` | ⚠️ **HIGH** — FMOD 原生插件（ADR-D6） |
| | F3 Input 事件 | Action Map 状态、输入时间戳管线 | ⚠️ **HIGH** — Input System（ADR-D1） |
| | F4 SO 数据层 | 7 类 SO schema | ⚠️ **MED** — SerializeField 仅字段 / Addressables 抛异常（ADR-D4） |
| | F5 存档 | SaveService 状态机、落盘布局 | LOW — EasySave 3（ADR-D7） |
| | F6 TDE 集成 | run-end 边界、Animancer 攻击层、boot 注入 | ⚠️ **MED** — Animancer 8.4.0 / TDE 边界（ADR-D1/D5） |
| **Core** | C1 节奏量化攻击 | 判定评级逻辑、`CurrentCombo` 唯一写者、输入缓冲 | LOW — 纯 C#（ADR-D3/D8） |
| | C2 连招图 | 连招图运行时、取消窗、动画加载生命周期 | ⚠️ **MED** — Addressables（ADR-D4/D14） |
| | C3 心法构筑 | PerkSO 克隆规则、心法冻结栈 | LOW — 纯 C#（ADR-D4/D8） |
| | C4 战斗导演 | 拍槽分配、令牌落拍、SlotCapacity | LOW — 纯 C#（ADR-D2/D6） |
| | C5/C6 武器·Hitbox | 武器容器、hitbox 激活时机 | LOW（Not Started） |
| **Feature** | FE1 敌人 AI | 行为决策 | ⚠️ **MED** — Unity Behavior 1.0.16（ADR-D2，LOW 已验证） |
| | FE3 延迟校准 | runtime 期 `LatencyOffsetMs` 唯一写者 | LOW（ADR-D10） |
| | 其余 FE | — | 见各未来 GDD |
| **Presentation/Polish** | 全部 | UI 视图状态（只读订阅） | PL2 触 URP RenderGraph（HIGH，延后） |

> **CombatCore 程序集边界（强制）**：C1-C6 编入独立 `CombatCore` 程序集，只读订阅 F6 域事件，
> **绝不** 直接调用 TDE 内部类或 `BeatClock` 生命周期方法。跨程序集测试可见性由
> `[assembly: InternalsVisibleTo("CombatCore.Tests.EditMode")]` 声明（ADR-D8）。

---

## 3. Module Ownership

> 每个模块的 **Owns**（独占数据/状态）/ **Exposes**（对外读或调用面）/ **Consumes**（读入）/
> **Engine APIs**（直接调用的引擎/插件类，含风险）。引擎 API 逐条对照 `docs/engine-reference/unity/modules/`。

### Foundation Layer

| 模块 | Owns | Exposes | Consumes | Engine APIs（风险） |
|------|------|---------|----------|--------------------|
| **F2 BeatClock** (ADR-D6/D8) | DSP 时钟真值、拍点事件、`LatencyOffsetMs`、Pause/Resume 唯一发布权 | `IBeatClock`（只读消费面：`OnBeatLookahead/OnHalfBeatLookahead/OnBeatExact/OnHalfBeatExact`、`OnMeterChangeAccepted/OnMeterChanged`、`OnBeatClockPaused/OnBeatClockResumed`、`GetSnapshot()`、`LatencyOffsetMs`、`DSPClock`、`GetBeatIntervalSeconds()`、`IsStartConfirmed`、`GetConfirmedDSPStart()`） | `IDSPClock.GetDspTime()`、`IAudioPlayer.PlayScheduled(double)` | ⚠️ `FMOD.ChannelGroup.getDSPClock()`、`FMOD.Channel.setDelay()`、`coreSystem.getSoftwareFormat()`（**HIGH**，master group 上取，禁 `AudioSettings.outputSampleRate`） |
| **F2 生命周期面** (ADR-D1) | — | `IBeatClockLifecycle.Play/Stop/Pause/Resume`（**唯一控制者 = F6 `TDELifecycleAdapter`**） | — | — |
| **F3 InputEventBus** (ADR-D1/D8) | Action Map 状态唯一权威、输入时间戳 | `IInputEventBus`（`OnAttackInput/OnDashInput/OnPause`、`GetCurrentMoveInput()`、`IsChargedAttackHeld`）；控制面 `EnableGameplayInput/EnableUIInput/DisableAllInput`（不入接口） | Input System callback | ⚠️ `InputSystemManagerEventsBased.BindButton`（**HIGH** Input System，唯一 override 虚方法） |
| **F4 SO 数据层** (ADR-D4) | 7 类 SO schema：`AttackNodeSO`（`AuthoredBpm`/`animationClip`）、`ComboGraphSO`(+`ComboEdge`)、`PerkSO`(+`PerkParameter`，仅值类型/string)、`WeaponSO`、`EnemyInfoSO`、`LevelConfigSO`(`_bgmFilePath`)、`PlayerInfoSO`(`[NonSerialized] latencyCalibrationOffsetMs`) | 只读 SO 资产 + `AssetReferenceT<T>` | — | ⚠️ `AssetReferenceT<T>`（**MED** Addressables 2.x，加载抛异常须 try/catch）、`[SerializeField]` 仅字段 |
| **F5 SaveService** (ADR-D7/D10) | 状态机（Uninitialized→Idle→Saving/Loading→Error）、落盘布局（`persistentDataPath/saves/`） | `IFileStorage {Save<T>,Load<T>,KeyExists,FileExists,DeleteFile,RenameFile}`；`ISaveService {Initialize(),IsInitialized,LoadLatencyCalibration(),SaveLatencyCalibration(dto)}` | — | `ES3.Save<T>()/Load<T>()`、`ES3.RenameFile()`、`ES3Settings.EncryptionType.AES`（LOW；禁 ES3Cloud；`Initialize()` 同步阻塞 <50ms） |
| **F6 TDELifecycleAdapter** (ADR-D1/D5/D10/D12/D13) | **run-end 边界唯一执行者**、`LevelConfigSO` 注入路径、boot 期 offset 注入、Animancer 攻击层 | `EndRun(RunOutcome)`（单一出口）、`event Action<RunOutcome> OnRunEnded`、`event Action OnRespawnComplete`（MVP 休眠）、`IBeatClockLifecycle` 控制 | `IBeatClock`、`ISaveService.IsInitialized`、`LevelConfigSO`、`AnimancerAttackPlayer` | ⚠️ `MMEventListener<TopDownEngineEvent>`、`TopDownEngineEventTypes.Pause/UnPause/PlayerDeath/RespawnComplete`、`HybridAnimancerComponent`+`AnimancerState.Speed/.Events.OnEnd`（**MED**，已 M0/PlayMode 验证） |

### Core Layer（CombatCore 程序集）

| 模块 | Owns | Exposes | Consumes | Engine APIs |
|------|------|---------|----------|-------------|
| **C1 RhythmJudger** (ADR-D3/D8/D12/D13) | 判定评级逻辑、单槽输入缓冲、`CurrentCombo`（唯一写者/广播）、SC-2（run-end 清 `_pendingInput`/`_lateWindowActive`） | `event Action<RhythmJudgmentEvent> OnRhythmJudgment`、`QuantizationMode Mode`、`IComboCounter`（`CurrentCombo`/`ComboMax=999`）、`int CurrentCombo{get;private set;}` | `IBeatClock`、`IInputEventBus`、`IAnimancerAttackPlayer`、`RhythmConfigSO`；`OnRunEnded` | 无 post-cutoff（纯 C# + Mono）；`RhythmJudgmentEvent` readonly struct（`AttackType/Rating/OffsetMs/BeatDspTime/Mode/ComboCountBefore`）；`enum RhythmRating{Perfect,Good,Miss}`、`QuantizationMode{Off,HalfBeat,Beat}` |
| **C2 连招图** (ADR-D4/D5/D14) | 连招图运行时容器、取消窗保护（`max(cancelWindowMs(bpm_before),cancelWindowMs(bpm_after))`）、`AttackNodeSO.animationClip` Addressables 加载/释放生命周期 | 连招推进 API | `AttackNodeSO/ComboGraphSO`、`IComboCounter`（C1 只读绝不回写）、`IBeatClock.OnMeterChangeAccepted`（保护窗起点，ADR-D14 Proposed）+`OnMeterChanged`（尾端）、Animancer 攻击层 | ⚠️ Addressables 加载（MED，try/catch） |
| **C3 心法构筑** (ADR-D4/D8) | PerkSO 克隆规则（`Instantiate()`/`Destroy()`）、心法冻结栈计时 | 心法运行时效果 | `PerkSO`、`IRandomSource`（`Next/NextDouble/Reseed/Seed`）、`IBeatClock.OnBeatClockPaused/Resumed`（软依赖，BC-6） | 无 post-cutoff |
| **C4 战斗导演** (ADR-D2/D6/D12/D13) | 拍槽分配、令牌落拍计算、SlotCapacity、SC-3（run-end 令牌回收，Death+Victory 对称） | 攻击令牌授予（`OnTokenGranted()`） | `IBeatClock`、`EnemyInfoSO.attackWindupBeats`、`IDSPClock`、`OnRunEnded` | 无 post-cutoff |
| **C5 武器 / C6 Hitbox** | 武器容器 / hitbox 激活时机（Not Started） | — | `WeaponSO`+C2 / C2 攻击帧+F6 碰撞 | TDE 碰撞检测（待 GDD） |

### Feature / Presentation / Polish Layer

| 模块 | Owns | Consumes | Engine APIs（风险） |
|------|------|----------|--------------------|
| **FE1 敌人 AI** (ADR-D2) | 行为决策（`BehaviorGraphAgent`）；移动委托 TDE `CharacterMovement`、攻击门控 `CombatDirectorTokenAction` | `BeatClock.OnBeatSnapshot`（经 `BeatClockBridge`）、`EnemyInfoSO` | ⚠️ `BehaviorGraphAgent.SetVariableValue<T>()`（Unity Behavior 1.0.16，LOW 已验证）、`CharacterMovement.SetMovement(Vector2)`（TDE 4.5）；`[DefaultExecutionOrder(-50)]` |
| **FE3 延迟校准** (ADR-D10) | runtime 期 `LatencyOffsetMs` **唯一写者** | F2 音频时钟测量、F5 offset 存档 | LOW |
| **P1..P8 UI** | UI 视图状态（只读订阅节奏事件总线） | C1 评级 / C2 连击 / C3 心法 / F2 节奏 / FE5 结算数据 | UI Toolkit（推荐）/UGUI；d-pad 导航 + 触摸非 hover 等价（technical-preferences） |
| **PL2 VFX/节奏反馈** | 反馈序列（订阅 CombatCore 评级事件，经 MMFeedbacks/Feel 播放） | C1 评级事件、F6 Feel 基础 | ⚠️ URP RenderGraph（**HIGH**，自定义 pass 时；MVP 后期） |

### ASCII 依赖图（简化，箭头 = 依赖方向）

```
              [P1..P8 UI]        [PL2 VFX]
                  │  订阅            │ 订阅
                  ▼                 ▼
   ┌──── C1 RhythmJudger ◄──── C2 连招图 ──► C5 武器 ──► C6 Hitbox
   │        │ IComboCounter        │                        │
   │        │(只读→C2)              │ OnMeterChangeAccepted  │
   │        ▼                      ▼                        │
   │      C3 心法 ────► (FE2 房间奖励)                       │
   │        │                                               │
   │      C4 战斗导演 ◄──────────── FE1 敌人AI ──► FE4 Boss   │
   │        │  OnTokenGranted            │(委托移动)         │
   └────────┼────────────────┬──────────┘                   │
   IBeatClock│    OnRunEnded  │ IBeatClockLifecycle(唯一控制) │
            ▼                ▼                               ▼
   ┌── F2 BeatClock    F6 TDELifecycleAdapter ◄── F5 SaveService(ISaveService)
   │   (FMOD DSP)      (run-end边界/boot注入/Animancer)      ▲
   │       ▲                │ LevelConfigSO / AttackNodeSO   │ offset boot-inject
   │  IDSPClock             ▼                                │
   │   F3 InputEventBus   F4 SO数据层 ──────────────────────┘
   └───────────────────────────────────────────────
     无循环依赖（systems-index §Circular Dependencies 已核）
```

---

## 4. Data Flow

### 4.1 帧更新路径（Input → Core → State → 反馈）

```
[FMOD 音频线程]  RuntimeManager.Update()  ──同帧先于──►  BeatClock.Update()[主线程]
                                                              │ 分发拍点事件（单一主线程分发）
                     ┌────────────────────────────────────────┼──────────────────┐
                     ▼                                         ▼                  ▼
[Input System]  InputAction.performed(时间戳)         C4 拍槽分配           P1 HUD 节奏指示
     │ AttackInputEvent                                    │ 落拍令牌            (只读)
     ▼                                                     ▼
  F3 InputEventBus.OnAttackInput ──► C1 RhythmJudger 判定（锚点+预缓冲）
                                          │ 比对 BeatDspTime vs 输入时间戳
                                          ▼
                              RhythmJudgmentEvent{Rating,OffsetMs,ComboCountBefore}
                              ├─► C1 CurrentCombo 更新（唯一写者）
                              ├─► C2 连招推进 / 取消窗（读 IComboCounter）
                              ├─► AnimancerAttackPlayer.PlayAttack()（Speed=currentBpm/authoredBpm）
                              └─► PL2 Feel 反馈 + P1 HUD（订阅事件，同步）
```
- **传输数据**：`AttackInputEvent`（时间戳）、`RhythmJudgmentEvent`（评级）。
- **生产者→消费者**：F3→C1（event）、C1→C2/PL2/P1（event，`OnRhythmJudgment`）。
- **同步性**：全部 C# event 同步分发，**仅在 Unity 主线程**（ADR-D6 Threading Addendum：禁 `ConcurrentQueue`/`OnAudioFilterRead`）。
- **跨线程边界**：仅 FMOD 音频线程 ↔ 主线程一处，由 `RuntimeManager.Update()` 保证同帧先行；C# 侧零跨线程。

### 4.2 事件/信号路径（解耦通信）

- **节奏事件总线 = F2 BeatClock 唯一发布**：`OnBeat*`、`OnMeterChange*`、`OnBeatClockPaused/Resumed`。
  所有 Core/Presentation/Polish **订阅**，保证「节拍驱动视效」视觉一致。
- **Pause/Resume 规范源唯一性（ADR-D8）**：F2 是唯一发布者；**禁止**其他系统定义并行 `OnGamePaused/OnGameResumed`。硬依赖 C1，软依赖 C3、F6。
- **run-end 域事件（ADR-D12/D13）**：F6 `EndRun(RunOutcome)` 单一出口广播 `OnRunEnded(RunOutcome)`，负载为纯 C# `enum RunOutcome{Death,Victory,...}`（**不含任何 TDE 类型**，机制域已钉死，取值集合待 FE5 扩展）。
- **BPM 变更（ADR-D14 Proposed）**：C2 取消窗保护起点从 `OnMeterChanged` 前移到 `OnMeterChangeAccepted`；C1 判定网格重锚仍绑 `OnMeterChanged`（C1-F5b 硬约束不可动）。

### 4.3 存档/加载路径

```
FE3/FE5/FE7/PL4 ──► SaveService（公开接口，同步主线程）──► IFileStorage(ES3Storage) ──► *.es3
                        │ 状态机 + MigrateIfNeeded<T>()               ▲
                        └── boot：TDELifecycleAdapter 读 LatencyCalibrationDTO ──► BeatClock.LatencyOffsetMs
```
- **序列化所有权**：`SaveService` 拥有；下游**禁止**直接调 `ES3.*`。仅序列化纯数据 DTO（无 MonoBehaviour 引用），`[NonSerialized]` 字段（如 `latencyCalibrationOffsetMs`）不落盘。
- **同步性**：`SaveService.Initialize()` 同步阻塞（ADR-D7/D10 前提）。

### 4.4 初始化顺序（ADR-D10，单一显式 bootstrap driver，非 Script Execution Order）

```
[Awake]  SaveService / BeatClock / TDELifecycleAdapter  ── 仅注册单例，不加载、不订阅
   │
[Start]  ① SaveServiceBootstrap.Start() → saveService.Initialize()（同步阻塞）
         ② TDELifecycleAdapter.Start()   → 仅缓存 _fmodVerified，不订阅、不 Play
   │
[首次 OnSceneLoaded(Single)]  ← A1 裁定：OnEnable/OnDisable 是订阅唯一权威位置
   │  guard: !_fmodVerified || !saveService.IsInitialized  → 早退
   │  if !_offsetInjected → 加载并注入 BeatClock.LatencyOffsetMs（一次性，先于首次 Play）
   ▼
   beatClock.Play()  ──►  EnableGameplayInput()
```
- **run 复用**：下一局（死亡换局，ADR-D12）复用同一 boot 路径 `OnSceneLoaded(Single) → offset guard → Play() → EnableGameplayInput()`。
- **写权模型**：`LatencyOffsetMs` boot 期唯一写者 = F6（一次性）；runtime 期唯一写者 = FE3（ADR-D10 D-5）。

---

## 5. API Boundaries

> 模块间公共契约。CombatCore 消费者缝隙接口统一声明于 `Assets/Scripts/Core/Interfaces/`（ADR-D8 拥有），
> 生产实现归各系统 ADR，Mock 实现供 EditMode 测试。以下为程序员实现所依据的合约。

### 5.1 DI / 测试缝隙接口（ADR-D8，逐字契约）

```csharp
// 归属：ADR-D8 声明；生产实现 F2/ADR-D6；调用方：C1/C4/FE1
public interface IBeatClock {
    event Action<BeatEventData> OnBeatLookahead, OnHalfBeatLookahead, OnBeatExact, OnHalfBeatExact;
    event Action<MeterChangeEventData> OnMeterChangeAccepted, OnMeterChanged;
    event Action<BeatClockPauseEventData> OnBeatClockPaused, OnBeatClockResumed;
    BeatStateSnapshot GetSnapshot();
    float LatencyOffsetMs { get; }
    IDSPClock DSPClock { get; }
    double GetBeatIntervalSeconds();
    bool IsStartConfirmed { get; }
    double GetConfirmedDSPStart();
}
// 生命周期面独立分离——唯一控制者 = F6 TDELifecycleAdapter（ADR-D1）
public interface IBeatClockLifecycle { void Play(); void Stop(); void Pause(); void Resume(); }

// 归属：ADR-D8；生产实现 F3/ADR-D1；调用方：C1
public interface IInputEventBus {
    event Action<AttackInputEvent> OnAttackInput;
    event Action<DashInputEvent> OnDashInput;
    event Action OnPause;
    Vector2 GetCurrentMoveInput();
    bool IsChargedAttackHeld { get; }
}

// 归属：ADR-D8；生产实现 C1 RhythmJudger（唯一写者/广播）；调用方：C2（只读绝不回写）
public interface IComboCounter { int CurrentCombo { get; } int ComboMax { get; } }  // ComboMax 固定 999

// 归属：ADR-D8；消费方：C3 心法
public interface IRandomSource { int Next(int maxExclusive); double NextDouble(); void Reseed(int seed); int Seed { get; } }

// 归属：ADR-D8；规范方法名 GetDspTime()（钦定取代 GetCurrentTime()/GetCurrentDSPTime()）；F2/F3/D6/C4 共用
public interface IDSPClock { double GetDspTime(); }
```

**调用方须遵守的不变量**：
1. `IComboCounter.CurrentCombo` 由 C1 `RhythmJudger` **唯一写者**；C2 只读，**绝不回写**。
2. `IBeatClock` 是**只读消费面**——生命周期 `Play/Pause/Resume/Stop` 在独立 `IBeatClockLifecycle`，唯一控制者 F6；CombatCore **禁止**调用。
3. Pause/Resume 事件唯一发布者 = F2；订阅方禁止定义并行暂停事件。
4. 测试缝隙 setter（`SetBeatClockForTesting(IBeatClock)` 等）仅在 `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 暴露；跨程序集可见性靠 `[assembly: InternalsVisibleTo("CombatCore.Tests.EditMode")]`（声明于 CombatCore Runtime asmdef）。
5. 两事件时间戳须同源 `AudioSettings.dspTime` 域，**禁混 `Time.time`**（ADR-D14 实装陷阱）。

**模块须向调用方保证**：`IBeatClock` 事件仅主线程分发；`ISaveService.Initialize()` 同步返回后 `IsInitialized==true`；`AnimancerState.Speed` 仅影响后续帧不回溯已播放 `NormalizedTime`。

### 5.2 run-end 生命周期边界（ADR-D12/D13）

```csharp
// 归属：F6 TDELifecycleAdapter（唯一 run-end 入口 + run-scoped latch 一次性闭锁）
public enum RunOutcome { Death, Victory /*, 取值集合待 FE5 扩展*/ }
void EndRun(RunOutcome outcome);           // 内部：BeatClock.Stop() → InputEventBus.DisableAllInput() → 广播
event Action<RunOutcome> OnRunEnded;       // 取代旧 OnPlayerDeath；OnPlayerDeath ≡ OnRunEnded(Death)
```
- 消费者：C1（SC-2 清缓冲）、C4（SC-3 令牌回收），Death 与 Victory **对称**触发。
- `RunOutcome` 归 CombatCore 可见的纯 C# 域程序集，**不得引用 TopDownEngine 类型**。
- ⚠️ D13-2 胜利桥接：TDE 胜利/通关事件成员名**未核对**（参照 D1 `.Respawn`→`.RespawnComplete` 教训，实现前查引擎参考）。

### 5.3 存档消费者契约（ADR-D7/D10）

```csharp
// 消费者面（mock seam）——下游 FE 系统只经此读写，禁止直接 ES3.*
public interface ISaveService {
    void Initialize();                                 // 同步阻塞 <50ms PC / <100ms Mobile
    bool IsInitialized { get; }
    LatencyCalibrationDTO LoadLatencyCalibration();
    void SaveLatencyCalibration(LatencyCalibrationDTO dto);
}
// 内部存储面（F5 私有）
public interface IFileStorage { /* Save<T>, Load<T>, KeyExists, FileExists, DeleteFile, RenameFile */ }
```

---

## 6. ADR Audit

> 对照 §2-§5 架构 + `tr-registry.yaml`（152 TR）。详见 `traceability-index.md` Rev.10 与
> `architecture-review-2026-08-12-coverage.md`。

### 6.1 ADR 质量检查

| ADR | Engine Compat | 版本记录 | post-cutoff 标注 | GDD 链接 | 与本文冲突 | 有效 |
|-----|:---:|:---:|:---:|:---:|------|:---:|
| ADR-D1 TDE Integration | ✅ | ✅ | ✅ | ✅ | 无 | ✅ |
| ADR-D2 Enemy AI | ✅ | ✅ | ✅ | ✅ | 无 | ✅ |
| ADR-D3 Rhythm Quantization | ✅ | ✅ | ✅ | ✅ | 无 | ✅ |
| ADR-D4 SO Data Layer | ✅ | ✅ | ✅ | ✅ | 无 | ✅ |
| ADR-D5 Animation Pipeline | ✅ | ✅ | ✅ | ✅ | 无 | ✅ |
| ADR-D6 Audio Middleware | ✅ | ✅ | ✅（FMOD HIGH） | ✅ | 无 | ✅ |
| ADR-D7 Save/Persistence | ✅ | ✅ | ✅ | ✅ | 无 | ✅ |
| ADR-D8 DI/Test Seams | ✅ | ✅ | ✅ | ✅ | 无 | ⚠️ 见 §9-H1 文档漂移 |
| ADR-D10 Bootstrap Sequencing | ✅ | ✅ | ✅ | ✅ | 无 | ✅ |
| ADR-D12 Death & Run-Lifecycle | ✅ | ✅ | ✅ | ✅ | 无 | ✅ |
| ADR-D13 Run-End Victory | ✅ | ✅ | ✅（TDE 胜利事件名未核对） | ✅ | 无 | ⚠️ D13-2 待引擎核对 |
| ADR-D14 BD-2 Cancel-Window | ✅ | ✅ | ✅ | ✅ | 无 | ⚠️ **Proposed** — 阻塞 C2 BD-2 故事 |

> **无跨 ADR blocking conflict**。上轮 A1-A5 冲突全部消解（A1 D1 唯一裁定；A2 D8 归属；A3 命名；A4 事件 schema；A5 authoredBpm 边界统一 [60,240]）。残留 LOW 文档漂移见 §9。合计 CPU ≈0.73ms/帧 ≪ 16.6ms 预算，无预算冲突。

### 6.2 追溯覆盖检查（派生自 traceability-index Rev.10）

| Layer | Total TR | Covered | Partial | Gap | 判定 |
|-------|:---:|:---:|:---:|:---:|------|
| Foundation (F2-F6) | 71 | 25 | 5 | 41 | Gap 多为 GDD=规范内部项（beatclock/input/save 37 项预期） |
| Core-concept | 15 | 6 | 4 | 5 | 5 真实缺口 → 未来 GDD |
| **Core (C1-C4)** | 66 | 58 | 8 | **0** | ✅ 跨切面 ADR + D8 DI 缝隙 + D12/D13 全覆盖 |
| Feature/Presentation | 0 | — | — | — | 尚无 GDD |
| **Total** | **152** | **89** | **17** | **46** | CONCERNS（健康，0 阻塞） |

**关键结论**：**Core 四系统 Gap = 0**——MVP 核心循环的架构承接完整，DI 硬门已由 ADR-D8（Accepted, step-1 spike + CI 门控）解锁。46 项 Gap 中，37 为 Foundation「GDD 即规范」预期项，仅 9 项（concept 5 + tde 4）为真实的未来 GDD 缺口。

---

## 7. Required ADRs

> §2-§6 建立的架构中，尚无对应 ADR 或需转正的决策。按层分组，Foundation/阻塞项优先。

### 必须在相关系统实现前处理

**阻塞 C2 连招实装（非 C1 关键路径）：**
- **ADR-D14 转 Accepted** → 覆盖 TR-combo-015；当前 Proposed 门控阻塞 C2 BD-2「BPM 变更瞬态取消窗」故事。C2 未开工，不阻塞 C1 冲刺。

**阻塞 D13-2 胜利桥接实装：**
- **核对 TDE 胜利/通关事件成员名** → 补入 ADR-D13 或引擎参考（`docs/engine-reference/unity/plugins/`）；机制域已 Accepted，仅实装桥接待核对。

### 应在相关系统 GDD 前完成

**Feature 层（未来 GDD，非当前阻塞）：**
- `/architecture-decision "延迟校准系统架构"` → 覆盖 TR-concept-011（FE3 GDD 前置）
- `/architecture-decision "武器系统与 ComboGraph 多武器容器"` → 覆盖 TR-concept-014（C5 GDD 前置）
- **F4 schema 补充** `PlayerInfoSO.unlockedPerkIds`（C3-OQ1）→ 无需新 ADR，F4 补字段

### 可延后至实现

- PL2 URP RenderGraph 自定义 pass 技术选型（MVP 后期）
- FE6 程序化房间生成（Alpha）
- `RunOutcome` 取值集合扩展（defer FE5 结算经济学）

---

## 8. Architecture Principles

支配本项目所有技术决策的 5 条原则（派生自 game-concept + GDD + technical-preferences）：

1. **单一真值时钟（Single Source of Truth Clock）**：F2 BeatClock 的 FMOD DSP 时钟是节奏判定/量化/调度的唯一物理底层。所有节奏关联系统订阅同一事件总线，禁止各自采时。判定禁用 `Update()` 轮询（16.7ms 帧量化误差 > 判定窗容差），一律用 Input System callback 时间戳。

2. **CombatCore 与 TDE 边界隔离（Boundary Contract）**：TDE 负责移动/生命/关卡/反馈；CombatCore 程序集负责节奏量化与连招逻辑。**禁止修改 TDE 源码**（v1 的 582 行覆写陷阱教训），只允许继承 override 或事件扩展；覆写 TDE 内部类须走 ADR 审批。CombatCore 只读订阅 F6 域事件，负载不含 TDE 类型。

3. **接口缝隙可测（Dependency Injection over Singletons）**：所有 Core 系统跨模块通信经 ADR-D8 声明的消费者接口（`IBeatClock`/`IInputEventBus`/`IComboCounter`/`IRandomSource`/`IDSPClock`），生产/Mock 双实现，`InternalsVisibleTo` 暴露测试缝隙。判定公式、状态机、AI 必须可 EditMode 单测（≥80% 覆盖，确定性无随机种子）。

4. **数据驱动、运行时只读（Data-Driven, Read-Only Runtime）**：玩法数值经 F4 ScriptableObject 外部配置，绝不硬编码；运行时 SO 只读（PerkSO 唯一可克隆）。Odin 验证仅限 Editor 程序集，Runtime **禁止**引用 `Sirenix.*`。攻击动画时长经 Animancer `AnimancerState.Speed` 按 BPM 动态缩放，禁止绑死帧数。

5. **显式生命周期与边界（Explicit Lifecycle & Boundaries）**：启动经单一 bootstrap driver 排序（非 Script Execution Order），release-active 运行时 guard 兜底。死亡与胜利经单一 F6 `EndRun(RunOutcome)` 出口统一 run-end 边界（Death+Victory 对称清理）。所有音频/拍点事件仅主线程分发，跨线程边界仅限 FMOD↔主线程一处。

---

## 9. Technical Director Sign-Off

**Gate TD-ARCHITECTURE 自评（`/create-architecture` 主笔以 technical-director 身份）：**

| 准则 | 判定 | 依据 |
|------|:---:|------|
| 全部已批准 GDD 系统均落入某一架构层且有明确所有权 | ✅ | §2 层图覆盖 31 系统；§3 所有权表 |
| 每条 Core 层 TR 有 ADR 承接（Gap=0） | ✅ | §6.2 Core Gap=0；152 TR 追溯 |
| 无跨 ADR blocking conflict，无循环依赖，符合帧预算 | ✅ | §6.1 A1-A5 消解；≈0.73ms ≪ 16.6ms |
| HIGH 风险引擎域全程标注并给缓解 | ✅ | §1 知识缺口；§3 逐条 API 风险 |

**判定：APPROVED WITH CONDITIONS**

**Conditions（非阻塞，实现前处理）：**
- **H1（文档漂移，LOW）**：ADR-D8 三处 `InternalsVisibleTo` 目标程序集名历史上曾写 `"CombatCore.Tests.EditMode"` 与实际跑绿代码 `"EditModeTests"` 需统一（D8:318/379/402），防 C1-C4 Mock 照抄重触 CS0122。本文 §5.1 采用 D8 权威名 `"CombatCore.Tests.EditMode"`。
- **D3 文本漂移（LOW）**：D3 未同步 `OnBeatClockPaused` 规范名 + `AttackType.HeavyAttack`（D8 Migration §2 已登记）。
- **ADR-D14 仍 Proposed**：C2 BD-2 实装受 Proposed 门控阻塞（不阻塞 C1 冲刺）。
- **D13-2 胜利事件名待引擎核对**（不阻塞 D13 机制域 Accept）。

**Lead Programmer Feasibility：SKIPPED — Lean 模式**（非 PHASE-GATE）。若 Production 前升 full 模式，补跑 gate LP-FEASIBILITY。

---

## 10. Open Questions

| ID | 摘要 | 优先级 | 解决路径 |
|----|------|:---:|---------|
| QQ-01 | ADR-D14（BD-2 取消窗瞬态）转 Accepted | Medium | `/architecture-decision` 复核 → Accept 后解阻塞 C2 BD-2 故事 |
| QQ-02 | TDE 胜利/通关事件成员名核对（D13-2 桥接） | Medium | 查 `docs/engine-reference/unity/plugins/` 或安装源码 → 落 ADR-D13 |
| QQ-03 | `PlayerInfoSO.unlockedPerkIds`（C3-OQ1） | Medium | F4 schema 补字段，无需新 ADR |
| QQ-04 | `RunOutcome` 取值集合 + 结算经济学 | Low | defer FE5 GDD + game-designer |
| QQ-05 | 胜利换局边界时钟 Stop 归属（W-F） | Low | ADR-D13:138 已自认；→ FE5 |
| QQ-06 | C1 死亡语义文本清理（F6 措辞残留 W-A/W-B） | Low | C1/F6 GDD 小修订 |
| ~~QQ-07~~ | ~~FMOD 6000.3 二进制编译确认 + `plugins/fmod.md`~~ | ~~Medium~~ | **已闭合 (2026-08-12)** — C# 编译 ✅ / macOS arm64 原生库加载 ✅ / `plugins/fmod.md` 已补；残留仅 FMOD Studio banks 待首次 Build（接入音频内容前） |

---

**门禁就绪提示**：本文档确立后，`/gate-check pre-production` 的架构长杆已具备。
剩余前置见 §7（ADR-D14 转 Accepted 仅阻塞 C2，非 C1 关键路径）+ `/test-setup` + `/ux-design`。
