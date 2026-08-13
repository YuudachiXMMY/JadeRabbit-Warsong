# Story 005: Dash 与 Move 输入广播

> **Epic**: F3 Input 事件系统
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: 2026-08-12
> **Last Updated**: [set by /dev-story when implementation begins]

## Context

**GDD**: `design/gdd/input-event-system.md`
**Requirement**: `TR-input-002`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-D1: TDE 集成架构（primary，移动输入与 TDE 边界）；ADR-D6: 音频中间件（次要，Dash `inputDspTime` 采样）；ADR-D8: DI 缝隙（次要，`GetCurrentMoveInput()` 消费面）
**ADR Decision Summary**: Dash 为离散 `performed` 事件（同帧双读 DSP 时间戳，与 Attack 一致）；Move 为持续模拟值，经 `GetCurrentMoveInput()` 拉取式读取而非高频广播，避免每帧事件分配。

**Engine**: Unity 6.3 LTS (6000.3.19f1) | **Risk**: MEDIUM
**Engine Notes**: Move 由 `InputAction`（Value/Vector2）驱动；F3 缓存最新 `Vector2` 供 `GetCurrentMoveInput()` 拉取。`MoveChangedEvent` 广播仅在**值发生变化**时触发（去抖），**不含** `inputDspTime`（移动非节奏判定输入，AC-F3-05）。Dash `performed` 时间戳采样与 Story 003 同帧双读规则一致，主线程执行。

**Control Manifest Rules (this layer)**:
- Required: 离散 Dash 同帧双读 DSP 时间戳；Move 持续值经消费面拉取（`GetCurrentMoveInput()`），零每帧事件分配
- Forbidden: `Never use Legacy Input Manager / Update() 轮询`；Move 广播不得携带 DSP 时间戳（非节奏输入）
- Guardrail: Move 值变化广播路径零 GC；无每帧堆分配

---

## Acceptance Criteria

*From GDD `design/gdd/input-event-system.md`, scoped to this story:*

- [ ] AC-F3-04：Dash `performed` 触发时，`OnDashInput` 广播 `DashInputEvent`，含 `inputDspTime`（同帧双读，= 按下时刻）、`inputGameTime`、`deviceType`/`deviceId`
- [ ] AC-F3-05：`MoveChangedEvent` 结构**不含** `inputDspTime` 字段（移动非节奏判定输入）；移动方向变化时 `OnMoveChanged` 广播新 `Vector2`
- [ ] AC-F3-06：`GetCurrentMoveInput()` 返回当前帧最新移动向量；无输入时返回 `Vector2.zero`；斜向输入正确返回归一化/原始向量（按 GDD 定义）

---

## Implementation Notes

*Derived from GDD Core Rule 2/3（离散 vs 持续输入）与 ADR-D8 消费面：*

- **Dash（离散）**：`Dash.performed` 回调内同帧双读 `inputDspTime = IDSPClock.GetDspTime()` + `inputGameTime = (float)ctx.time`，构造 `DashInputEvent` 广播。与 Story 003 的 Attack 双读完全同规则（Dash 亦可参与节奏判定，故保留 DSP 时间戳）。
- **Move（持续）**：`Move` InputAction（Value/Vector2）值更新时缓存最新 `Vector2` 到私有字段；`GetCurrentMoveInput()`（`IInputEventBus` 消费面方法）拉取该缓存。**不**每帧广播；仅在值变化（跨去抖阈值）时经 `OnMoveChanged` 广播 `MoveChangedEvent`。
- **`MoveChangedEvent` 无 DSP 时间戳**：移动不参与节奏量化判定（AC-F3-05），结构仅含 `Vector2 moveDirection` + `deviceType`/`deviceId`，**不含** `inputDspTime`/`chargeStartDspTime`。Story 001 的编译期结构断言已覆盖此约束。
- **零 GC**：`MoveChangedEvent` 为 `readonly struct`（Story 001 建立），值变化广播走方法组订阅（非捕获 lambda），无堆分配。
- **拉取 vs 推送边界**：下游需要"当前移动向量"（如 TDE `CharacterMovement`）用 `GetCurrentMoveInput()` 拉取；需要"移动开始/停止/转向事件"用 `OnMoveChanged` 订阅。二者并存不冲突。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 003：Attack1/Attack2 时间戳双读与广播（本 story 复用其同帧双读规则于 Dash）
- Story 002：Gameplay Map 启用（本 story 测试假定 `GameplayActive`）
- Story 001：`DashInputEvent`/`MoveChangedEvent` 的 `readonly struct` 结构定义与编译期断言

---

## QA Test Cases

*Lean 模式无 qa-lead 生成；以下转写自 GDD 对应 AC 的「验证方法」，实现时对齐这些用例。*

- **AC-1 (AC-F3-04)**: Dash 广播完整性
  - Given: `GameplayActive`，Mock 时钟已注入
  - When: 触发 Dash `performed`
  - Then: `OnDashInput` 广播一次，`inputDspTime` = 按下时刻（同帧双读）、`inputGameTime` 正确、`deviceType`/`deviceId` 正确
  - Edge cases: 快速连续 Dash 每次独立广播，时间戳各自采样
- **AC-2 (AC-F3-05)**: MoveChangedEvent 无 DSP 时间戳
  - Given: `MoveChangedEvent` 结构定义
  - When: 反射/编译期检查字段
  - Then: 无 `inputDspTime` 字段；移动方向变化时 `OnMoveChanged` 广播新 `Vector2`
  - Edge cases: 移动值未变化时不重复广播（去抖）
- **AC-3 (AC-F3-06)**: GetCurrentMoveInput 拉取
  - Given: `GameplayActive`
  - When: (1) 无输入调用；(2) 施加方向输入后调用；(3) 施加斜向输入后调用
  - Then: (1) 返回 `Vector2.zero`；(2) 返回对应方向向量；(3) 斜向向量按 GDD 定义（归一化或原始）正确返回
  - Edge cases: 松开输入后返回 `Vector2.zero`

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- `Assets/Tests/EditMode/input/dash_move_broadcast_test.cs` — 覆盖 AC-F3-04/05/06 的 EditMode 单元测试，必须存在且通过

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 003（同帧双读时间戳规则；`DashInputEvent`/`MoveChangedEvent` 负载经 Story 001 定义）
- Unlocks: None
