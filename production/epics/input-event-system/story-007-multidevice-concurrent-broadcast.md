# Story 007: 多设备并发广播

> **Epic**: F3 Input 事件系统
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: [fill before sprint planning]
> **Manifest Version**: 2026-08-12
> **Last Updated**: [set by /dev-story when implementation begins]

## Context

**GDD**: `design/gdd/input-event-system.md`
**Requirement**: `TR-input-007`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-D1: TDE 集成架构（primary，设备并发与 TDE 输入管理器边界）；ADR-D6: 音频中间件（次要，各设备事件独立 DSP 时间戳）
**ADR Decision Summary**: F3 对设备来源透明——键鼠、手柄、触屏并发时各设备的输入独立采样 DSP 时间戳并广播，事件携带 `deviceType`/`deviceId` 标识来源；F3 不做设备仲裁（不"锁定"单一活动设备），仲裁若需要归下游。

**Engine**: Unity 6.3 LTS (6000.3.19f1) | **Risk**: MEDIUM
**Engine Notes**: `InputAction.CallbackContext.control.device` 提供触发设备；`device.deviceId`（int）与 `device`（`Keyboard`/`Gamepad`/`Touchscreen` 类型）映射到 `InputDeviceType` enum + `deviceId`。默认 Input System 会将多设备事件合流到同一 Action（除非用 `InputUser`/PlayerInput 分流）——本单人游戏不分流，各设备事件按到达顺序独立触发同一 Action 回调。多设备须实机验证（PlayMode）。

**Control Manifest Rules (this layer)**:
- Required: 事件携带 `deviceType`/`deviceId`；各设备输入独立采样 DSP 时间戳
- Forbidden: F3 层设备仲裁/锁定（对设备来源透明）；跨设备共享/复用时间戳
- Guardrail: 多设备并发广播路径零 GC

---

## Acceptance Criteria

*From GDD `design/gdd/input-event-system.md`, scoped to this story:*

- [ ] AC-F3-13：键鼠、手柄、触屏并发输入时，各设备的输入分别广播，事件 `deviceType`/`deviceId` 正确标识来源设备；同一 Action 被不同设备触发时各自独立采样 `inputDspTime`（不复用），不发生事件合并或丢失

---

## Implementation Notes

*Derived from GDD Core Rule 4（设备透明化）与 ADR-D1：*

- **设备标识**：`performed` 回调内从 `ctx.control.device` 解析 `InputDeviceType`（`Keyboard`/`Gamepad`/`Touchscreen`）与 `deviceId`（`device.deviceId`），写入事件负载。映射逻辑尽量下沉纯 C#（输入 `device` 类型 → enum），跑 EditMode；多设备并发触发本身须 PlayMode 验证。
- **独立时间戳**：不同设备触发同一 Action 时，每次 `performed` 各自同帧双读（Story 003 规则），**不**跨设备复用上次时间戳。快速交替设备输入时每个事件时间戳独立正确。
- **无设备仲裁**：F3 不"锁定活动设备"、不因设备切换丢弃事件——所有设备事件平等广播。若玩法需要单一活动设备语义，由下游依 `deviceType`/`deviceId` 自行仲裁。
- **合流行为**：本单人游戏不使用 `InputUser`/`PlayerInput` 分流，多设备合流到同一 Action。验证并发时事件不合并（两设备同帧触发 → 两次独立广播）、不丢失。
- **零 GC**：并发广播复用 `readonly struct` + 方法组订阅（Story 001/008 约束）。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 003/005：单设备下 Attack/Dash/Move 的广播逻辑（本 story 验证多设备并发正确性）
- Story 008：零 GC 的 `ProfilerRecorder` 断言（本 story 复用其零分配保证，不重复测量）
- 本地多人分屏/多 `InputUser` 分流（本项目单人，不在范围）

---

## QA Test Cases

*Lean 模式无 qa-lead 生成；以下转写自 GDD AC-F3-13 的「验证方法」，实现时对齐这些用例。*

- **AC-1 (AC-F3-13)**: 多设备并发独立广播
  - Setup: PlayMode，`GameplayActive`，用 `InputTestFixture` 创建虚拟 Keyboard + Gamepad（+ Touchscreen）
  - When: (1) 键盘触发 Attack；(2) 手柄触发 Attack；(3) 两设备同帧交替触发
  - Then: 每次广播事件 `deviceType`/`deviceId` 正确标识来源；各事件 `inputDspTime` 独立采样（不复用）；无事件合并/丢失
  - Edge cases: 同帧两设备触发同一 Action → 两次独立广播；快速交替设备时间戳各自正确

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- `Assets/Tests/PlayMode/input/multidevice_concurrent_test.cs` — 覆盖 AC-F3-13 的 PlayMode 测试（用 `InputTestFixture` 虚拟多设备），必须存在且通过

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 003（事件广播与同帧双读时间戳）
- Unlocks: None
