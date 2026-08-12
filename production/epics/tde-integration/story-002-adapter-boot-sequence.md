# Story 002: TDELifecycleAdapter 骨架 + boot 序列

> **Epic**: F6 TopDownEngine & Animancer 集成层
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: [fill before sprint planning]
> **Manifest Version**: 2026-08-12
> **Last Updated**: [set by /dev-story when implementation begins]

## Context

**GDD**: `design/gdd/tde-integration.md`
**Requirement**: `TR-tde-001`（双组件之适配器半），`TR-tde-003`（boot 顺序），`TR-tde-006`（BeatClock 生命周期唯一调用者），`TR-tde-010`（订阅者幂等）
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-D1: TDE 集成（primary）；ADR-D10: Bootstrap 顺序（次要）
**ADR Decision Summary**: `TDELifecycleAdapter` 是 DontDestroyOnLoad Manager 上的 `MonoBehaviour + MMEventListener<TopDownEngineEvent>`，独家控制 BeatClock 生命周期；`OnSceneLoaded(Single)` 经 `scene.GetRootGameObjects()` 取 `LevelConfigSO` 注入 `BeatClock.Play()`，随后 `EnableGameplayInput()`；忽略 Additive。

**Engine**: Unity 6.3 LTS (6000.3.19f1) | **Risk**: MEDIUM
**Engine Notes**: 标准 `MonoBehaviour` 生命周期 + `SceneManager.sceneLoaded`（无后-cutoff API）；TDE `MMEventListener<TopDownEngineEvent>` 订阅点须对照 `plugins/top-down-engine.md`。

**Control Manifest Rules (Foundation)**:
- Required: `TDELifecycleAdapter` 是 `BeatClock.Play/Pause/Resume/Stop` 唯一控制者——无其他系统可调用（ADR-D1）；boot 阶段 `OnSceneLoaded → LevelConfigSO → BeatClock.Play → EnableGameplayInput` 严格顺序（ADR-D1/D10）
- Forbidden: 禁止 `FindObjectOfType<LevelBootstrap>()` 取 `LevelConfigSO`（DontDestroyOnLoad 上下文场景边界不稳定）——必须 `scene.GetRootGameObjects()` 遍历
- Guardrail: `TDELifecycleAdapter` 全事件驱动，无 `Update()` 成本，< 0.01ms/frame 均值（ADR-D1）

---

## Acceptance Criteria

*From GDD `design/gdd/tde-integration.md`, scoped to this story:*

- [ ] AC-F6-03：`TDELifecycleAdapter` 是 BeatClock 生命周期唯一调用者（静态分析 + 运行时 Spy 全部来源为该类型）
- [ ] AC-F6-06：Single 场景加载后进入 Gameplay Active（`Play()` 恰 1 次且参数匹配 SO，`EnableGameplayInput()` 恰 1 次）
- [ ] AC-F6-07：Additive 场景加载不触发 BeatClock 重启（`Play()` 新增 == 0，BPM 不变）
- [ ] AC-F6-17：5 次连续 Single 场景切换后 `OnSceneLoaded` 回调不累积（== 5，无重复注册）

---

## Implementation Notes

*Derived from ADR-D1 + GDD Core Rule 4/6/10 + EC-02/EC-07:*

- 适配器挂在 DontDestroyOnLoad Manager 上；`OnEnable()` 注册 `MMEventListener<TopDownEngineEvent>` + `SceneManager.sceneLoaded`，`OnDisable()` 取消注册（幂等，防 `SetActive` 往返重复注册）。
- BeatClock 生命周期经 `IBeatClockLifecycle`（F6-only seam，ADR-D8）访问——`Play/Stop/Pause/Resume` 不暴露在 `IBeatClock` 消费面。
- `OnSceneLoaded(scene, mode)`：仅响应 `LoadSceneMode.Single`；`Additive` 直接 `return`（房间拼接 BPM 变化走 `RequestBPMChange()`，不重调 `Play()`）。
- `LevelConfigSO` 获取：`scene.GetRootGameObjects()` 遍历搜 `LevelBootstrap`（或等效持有组件）；未找到 → fatal `[F6-FATAL] LevelConfigSO not found in scene — BeatClock start blocked` 并中止。
- boot 顺序：`OnSceneLoaded(Single) → 取 LevelConfigSO → Play(bgm,bpm,timeSig) → EnableGameplayInput()`（遵 ADR-D1，`Play()`/输入仍在 `OnSceneLoaded`，未迁至 `Start()`）。
- FMOD 就绪门禁与异步验证在 Story 003；F5 boot-inject 在 Story 004——本 story 建立骨架，`Play()` 的实际门禁分支由 003/004 接入。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 003：FMOD 就绪 + 异步模式验证门禁（Play() 前置守卫）
- Story 004：F5 初始化 gate + LatencyOffsetMs boot-inject
- Story 005：Pause/UnPause 同帧协调
- Story 007：run-end 边界 EndRun / OnRunEnded

---

## QA Test Cases

*Embedded from GDD AC-F6-03/06/07/17（lean 模式：GDD AC 即测试规格）。*

**AC-1（AC-F6-03，Logic — EditMode 静态 + PlayMode Spy）**
  - Given：全部非 `TDELifecycleAdapter` 类型
  - When：EditMode Reflection/Roslyn 枚举，运行时注入实现 `IBeatClockLifecycle` 的 mock，记录每次 `Play/Stop/Pause/Resume` 的调用来源类型（`new StackTrace().GetFrame(1).GetMethod().DeclaringType`）
  - Then：静态无越权调用；运行完整序列（加载→暂停→恢复→死亡→复活）后所有调用来源均为 `TDELifecycleAdapter`
  - Edge cases：无

**AC-2（AC-F6-06，Integration — PlayMode）**
  - Given：含有效 `LevelConfigSO`（BPM=120, TimeSig=4/4）的场景
  - When：以 `LoadSceneMode.Single` 加载
  - Then：`BeatClock.Play()` 恰 1 次且参数与 SO 一致；`EnableGameplayInput()` 恰 1 次；BeatClock=Playing，InputEventBus=GameplayActive
  - Edge cases：无有效 SO → fatal 日志 + 场景冻结（`Play()` == 0）

**AC-3（AC-F6-07，Logic — PlayMode）**
  - Given：BeatClock=Playing（currentBPM=120）
  - When：以 `LoadSceneMode.Additive` 加载房间子场景
  - Then：`BeatClock.Play()` 新增 == 0；继续 Playing 且 BPM 不变；InputEventBus 状态不变
  - Edge cases：无

**AC-4（AC-F6-17，Logic — PlayMode）**
  - Given：`OnSceneLoaded` Spy
  - When：连续切换 5 个不同 Single 测试场景（每次等加载完成）
  - Then：`OnSceneLoaded` 总调用 == 5（每次恰 +1）；`BeatClock.Play()` 总调用 == 5
  - Edge cases：`SetActive(false)→(true)` 往返后注册/取消注册幂等（不累积监听器）

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- `Assets/Tests/PlayMode/F6/TDELifecycleAdapter_boot_test.cs`（AC-06/07/17 + AC-03 运行时 Spy）— 必须存在且通过
- `Assets/Tests/EditMode/F6/BeatClockLifecycle_soleCaller_test.cs`（AC-03 静态越权调用扫描）— 必须存在且通过

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: None（本 epic 内地基）；跨 epic 需 F2 `IBeatClockLifecycle`、F3 `InputEventBus`、F4 `LevelConfigSO` 接口就绪
- Unlocks: Story 003, 004, 005, 006, 007
