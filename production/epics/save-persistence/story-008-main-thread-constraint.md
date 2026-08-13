# Story 008: 主线程约束保护（非主线程调用缓存 + flush）

> **Epic**: F5 存档/持久化系统
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: S
> **Manifest Version**: 2026-08-12
> **Last Updated**: [set by /dev-story when implementation begins]

## Context

**GDD**: `design/gdd/save-persistence.md`
**Requirement**: `TR-save-006`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR: N/A — 主线程约束保护由 GDD Rule 13 直接规范，无独立架构决策；文件放置遵循 ADR-D7 的 `Core/Persistence/` 约定。
**ADR Decision Summary**: 不适用。GDD Rule 13 规范：非主线程调用持久化 API 时，不执行 I/O、不抛异常，将错误描述入 `ConcurrentQueue` 缓存，主线程 `Update()` flush 为 `Debug.LogError`。

**Engine**: Unity 6.3 LTS (6000.3.19f1) | **Risk**: LOW
**Engine Notes**: `Thread.CurrentThread.ManagedThreadId` 与 story 002 在 `Awake()` 捕获的 `_mainThreadId` 比对判定主线程（不用 `IsBackground`/`Name`）。PlayMode 测试用 `Task.Run`/`new Thread` 制造非主线程上下文。无 post-cutoff API。

**Control Manifest Rules (this layer)**:
- Required: 主线程 ID 在 `Awake()` 用 `Thread.CurrentThread.ManagedThreadId` 捕获（Rule 13）
- Required: 非主线程调用错误经 `ConcurrentQueue` 缓存、主线程 flush（Rule 13）
- Guardrail: 非主线程调用**绝不**触碰 `IFileStorage`，不抛异常（Rule 13）

> **注**：control-manifest ADR-D6 禁止 BeatClock/F2 使用 `ConcurrentQueue<BeatEventData>`；此处 F5 Rule 13 **要求** `ConcurrentQueue` 做错误延迟——不同系统、不同用途，无冲突。

---

## Acceptance Criteria

*From GDD `design/gdd/save-persistence.md`, scoped to this story:*

- [ ] 持久化 API 入口比对 `Thread.CurrentThread.ManagedThreadId` 与 `_mainThreadId`；非主线程时提前返回，不执行任何 `IFileStorage` 操作
- [ ] 非主线程调用将错误描述（含 "main thread" 字样）入 `ConcurrentQueue<string>` 缓存，不抛异常
- [ ] 主线程 `Update()` 每帧 flush 队列，对每条缓存错误调用 `Debug.LogError`
- [ ] **AC-F5-12**：从后台线程（`Task.Run`/`new Thread`）调用 `SaveSlotProgress(0, dto)` —
  - ① 调用方不抛异常（后台线程不崩）
  - ② 经过 1 帧 flush 后，出现一条 `LogType.Error` 日志，内容含 "main thread"（或等效描述）
  - ③ `LoadSlotProgress(0)` 数据不变（`IFileStorage.Save` 未被调用）
  - ④ `state` 保持 `Idle`（非主线程调用不改变状态机）

---

## Implementation Notes

*Derived from GDD Rule 13:*

- 主线程判定在**所有**对外持久化 API（`SaveSlotProgress`/`LoadSlotProgress`/`SaveSettings`/`SaveLatencyCalibration`/`DeleteSlot` 等）入口统一执行：`if (Thread.CurrentThread.ManagedThreadId != _mainThreadId) { _errorQueue.Enqueue(desc); return; }`（读方法返回默认或缓存快照）。
- `_errorQueue` 为 `ConcurrentQueue<string>`（线程安全入队）；`Update()` 中 `while (_errorQueue.TryDequeue(out var msg)) Debug.LogError(msg)`。
- 非主线程调用是**编程错误**（持久化只允许主线程），本 story 是防御性护栏，不做真正的跨线程 I/O，也不静默——通过 `Debug.LogError` 在主线程暴露给开发者。
- `_mainThreadId` 由 story 002 在 `Awake()` 捕获并由静态重置方法归零；本 story 只消费该字段。
- 队列 flush 是主线程 `Update()`，因此本 story 为 PlayMode 测试（需真实帧循环 + Unity 主线程）。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002：`_mainThreadId` 的 `Awake()` 捕获与 Domain Reload 静态重置（本 story 依赖其存在）
- Story 006：两级 I/O 错误模型（`OnSaveError`/`Error` 状态）——主线程约束错误**不**走 `OnSaveError`，只 `Debug.LogError`，两者独立
- 真正的异步/多线程存档（GDD 明确不支持，非 MVP）

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

- **AC-1 (AC-F5-12)（非主线程调用缓存 + flush）**:
  - Given: `Initialize()` 完成（`Idle`），记录 `LoadSlotProgress(0)` 当前值；订阅 `Application.logMessageReceived` 或用 `LogAssert.Expect`
  - When: 在 `Task.Run`/`new Thread` 中调用 `SaveSlotProgress(0, {merit=999})`，等待后台调用返回，再 `yield return null`（推进 1 帧触发 `Update()` flush）
  - Then: ① 后台调用不抛异常；② flush 后有一条 `LogType.Error` 含 "main thread"；③ `LoadSlotProgress(0)` 值与调用前一致；④ `state==Idle`
  - Edge cases: 多次后台调用各自入队，flush 后逐条 `LogError`；主线程正常调用同一 API 不受影响、不产生该错误日志

---

## Test Evidence

**Story Type**: Integration（PlayMode — 需真实帧循环与 Unity 主线程）
**Required evidence**:
- `Assets/Tests/PlayMode/Save/MainThreadConstraintTests.cs` — AC-F5-12（非主线程缓存 + 1 帧 flush + LogError 断言 + 数据/状态不变），必须通过

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 002（DONE）
- Unlocks: None
