# Story 003: 同帧双读时间戳与 Attack1/Attack2 广播

> **Epic**: F3 Input 事件系统
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: [fill before sprint planning]
> **Manifest Version**: 2026-08-12
> **Last Updated**: [set by /dev-story when implementation begins]

## Context

**GDD**: `design/gdd/input-event-system.md`
**Requirement**: `TR-input-002`, `TR-input-005`（部分——普通攻击段）, `TR-input-012`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-D1: TDE 集成架构（primary）；ADR-D6: 音频中间件（次要，`IDSPClock.GetDspTime()`）；ADR-D8: DI 缝隙（次要，`IDSPClock` mock 注入）
**ADR Decision Summary**: F3 在 `InputAction.performed` 回调中同步采样 `IDSPClock.GetDspTime()`（DSP 时钟域）与 `ctx.time`（游戏时间域）。ADR-D8 钦定 `IDSPClock` 规范方法名 `GetDspTime()`，F2/F3/D6 同名；生产实现 `FMODDSPClockImpl`。

**Engine**: Unity 6.3 LTS (6000.3.19f1) | **Risk**: MEDIUM
**Engine Notes**: 输入时间戳采样必须在 Unity 主线程执行；`InputSystem.settings.updateMode` 须为 `ProcessEventsInDynamicUpdate`（默认），确保 `performed` 回调在主线程 PlayerLoop 内触发，禁止后台线程输入处理模式。`IDSPClock.GetDspTime()` 返回 double。

**Control Manifest Rules (this layer)**:
- Required: 离散输入同帧双读（`inputDspTime = IDSPClock.GetDspTime()` + `inputGameTime = (float)ctx.time`）；`inputDspTime` 与 F2 `beat_dsp_time` 必须来自同一 `IDSPClock` 实现
- Forbidden: `Never use Legacy Input Manager / Update() 轮询做时序判定`（technical-preferences 禁止项）；不得提前/延迟/跨帧缓存 DSP 采样
- Guardrail: F3 时间戳误差上限 ≤ 输入处理间隔（60fps ≤ 16.7ms）——架构约束，非可测项

---

## Acceptance Criteria

*From GDD `design/gdd/input-event-system.md`, scoped to this story:*

- [ ] AC-F3-01：`performed` 回调中 `AttackInputEvent.inputDspTime == IDSPClock.GetDspTime()` 返回值，`inputGameTime == ctx.time`
- [ ] AC-F3-02：`inputDspTime` 是回调执行**当时**同步调用 `GetDspTime()` 的值——不等于回调前或回调后的时钟值，不缓存自上一帧
- [ ] AC-F3-03（普通攻击段）：Attack1/Attack2 的 `performed` 触发时，`OnAttackInput` 广播 `AttackInputEvent`，`attackType` 正确、`inputDspTime` = 按下时刻、`inputGameTime` 正确、`deviceType`/`deviceId` 正确、`chargeStartDspTime == -1.0`（哨兵值）

---

## Implementation Notes

*Derived from GDD Core Rule 2 / Formula F3-F1 与 ADR-D8 §5：*

- 同帧双读（GDD Core Rule 2）：在每个离散 `performed` 回调内**同步**执行 `inputDspTime = IDSPClock.GetDspTime()` 与 `inputGameTime = (float)ctx.time`。Attack1（LightAttack）、Attack2（HeavyAttack）为 Button/performed，`inputDspTime` = 按下时刻。
- 普通 Light/Heavy 攻击的 `chargeStartDspTime` 必须为哨兵值 `-1.0`（**非 0.0**——FMOD DSP 时钟从 0 初始化，会话首帧 `GetDspTime()` 可返回 ~0.0，`0.0` 与合法充能时刻不可区分；`-1.0` 永不为合法 DSP 时间，与 F2 `GetConfirmedDSPStart()` 哨兵一致）。
- 线程约束：所有 DSP 采样在主线程；`InputSystem.settings.updateMode = ProcessEventsInDynamicUpdate`。
- 测试用 `MockDSPClock`（返回可控 `dsp_now`，禁止 `using` FMOD/TDE 命名空间）：AC-F3-02 在 `performed` 回调触发前后步进不同时钟值，断言 `inputDspTime` 等于回调执行时刻的值。`IDSPClock` 经 Story 001 建立的字段注入缝隙替换（属性/方法注入，测试 Setup 须在 `Awake` 前注入 Mock）。
- 纯逻辑（时间戳组装、`AttackInputEvent` 构造）尽量下沉到无 MonoBehaviour 依赖的纯 C# 类，跑 EditMode NUnit（ADR-D8「优先纯 C# 构造注入」）。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 004：ChargedAttack 的 `Hold.started/performed/canceled` 状态机、`chargeStartDspTime` 捕获、E3/E3a/E3b（本 story 只处理普通 Attack1/Attack2 与哨兵值 `-1.0`）
- Story 005：Dash 时间戳、Move 广播
- Story 002：Gameplay Map 启用（本 story 测试假定 `GameplayActive`）
- **非本 story 覆盖**：TR-input-013（Android DSP 粒度 23–46ms 平台约束）——非可测项，属 C1 判定窗口调参

---

## QA Test Cases

*Lean 模式无 qa-lead 生成；以下转写自 GDD 对应 AC 的「验证方法」，实现时对齐这些用例。*

- **AC-1 (AC-F3-01)**: 同帧双读时间戳捕获
  - Given: 注入实现 `IDSPClock` 的 Mock 时钟（可控返回值）
  - When: 触发 Mock InputAction 的 `performed` 回调
  - Then: `inputDspTime == mockClock.GetDspTime()` 且 `inputGameTime == ctx.time`
  - Edge cases: Mock 在 `Awake` 前注入（MonoBehaviour 无构造注入）
- **AC-2 (AC-F3-02)**: DSP 采样时序约束
  - Given: 注入受控 Mock 时钟
  - When: 在 `performed` 回调触发前后将时钟步进不同值
  - Then: `inputDspTime` 等于回调执行时刻的 `mockClock.GetDspTime()`，不等于回调前/回调后的值
  - Edge cases: 不得缓存上一帧时钟值
- **AC-3 (AC-F3-03 普通攻击段)**: AttackInputEvent 广播完整性（Light/Heavy）
  - Given: `GameplayActive`，Mock 时钟已注入
  - When: 分别模拟 Attack1、Attack2 的 `performed`
  - Then: `OnAttackInput` 各广播一次，`attackType` 正确、`inputDspTime` = 按下时刻、`inputGameTime` 正确、`deviceType`/`deviceId` 正确、`chargeStartDspTime == -1.0`
  - Edge cases: 哨兵值须为 `-1.0` 而非 `0.0`

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- `Assets/Tests/EditMode/input/dualread_timestamp_test.cs` — 覆盖 AC-F3-01/02/03（普通攻击段）的 EditMode 单元测试，必须存在且通过

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001（`IInputEventBus`/`AttackInputEvent`/`IDSPClock` 缝隙）、Story 002（Gameplay Map 启用）
- Unlocks: Story 004、Story 005、Story 006、Story 007、Story 008
