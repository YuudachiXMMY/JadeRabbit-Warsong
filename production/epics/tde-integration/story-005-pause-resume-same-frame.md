# Story 005: Pause/UnPause 同帧协调

> **Epic**: F6 TopDownEngine & Animancer 集成层
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: S
> **Manifest Version**: 2026-08-12
> **Last Updated**: [set by /dev-story when implementation begins]

## Context

**GDD**: `design/gdd/tde-integration.md`
**Requirement**: `TR-tde-007`（暂停/恢复同帧协调）
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-D1: TDE 集成
**ADR Decision Summary**: TDE `TopDownEngineEvent`（Pause/UnPause）到达时，`TDELifecycleAdapter` 同帧协调 `BeatClock.Pause()/Resume()` 与 `InputEventBus` 输入门控，保证节拍时钟与输入状态原子切换，无跨帧撕裂。

**Engine**: Unity 6.3 LTS (6000.3.19f1) | **Risk**: MEDIUM
**Engine Notes**: TDE `TopDownEngineEvent` 的暂停/恢复枚举成员名须对照 `docs/engine-reference/unity/plugins/top-down-engine.md`，严禁从训练数据推断。

**Control Manifest Rules (Foundation)**:
- Required: `TDELifecycleAdapter` 独家调用 `BeatClock.Pause/Resume`（ADR-D1）
- Required: Pause/Resume 时 BeatClock 与 InputEventBus 同帧切换（原子性）
- Guardrail: 事件驱动，无 `Update()` 轮询

---

## Acceptance Criteria

*From GDD `design/gdd/tde-integration.md`, scoped to this story:*

- [ ] AC-F6-08：收到 TDE Pause 事件 → 同帧内 `BeatClock.Pause()` 恰 1 次 + `InputEventBus` 切至暂停态（输入门控）
- [ ] AC-F6-09：收到 TDE UnPause 事件 → 同帧内 `BeatClock.Resume()` 恰 1 次 + `InputEventBus` 恢复 Gameplay 态

---

## Implementation Notes

*Derived from ADR-D1 + GDD Core Rule (pause atomicity):*

- `TDELifecycleAdapter` 的 `MMEventListener<TopDownEngineEvent>.OnMMEvent` 内 switch 处理 Pause/UnPause（成员名先查 `top-down-engine.md`）。
- Pause 分支：同一回调内先 `BeatClock.Pause()` 再切 `InputEventBus`（或反序——但须在同帧、无 yield）。
- UnPause 分支：对称 `BeatClock.Resume()` + `InputEventBus` 恢复。
- 幂等：重复 Pause 事件不叠加（已暂停态再 Pause 无副作用），重复 UnPause 同理。

---

## Out of Scope

- Story 002：adapter 骨架（本 story 只加 Pause/UnPause 分支）
- Story 007：run-end EndRun（死亡/胜利经不同出口，非暂停）
- TDE 暂停 UI / 菜单本身（属 UI epic）

---

## QA Test Cases

*Embedded from GDD AC-F6-08/09（lean 模式）。*

**AC-1（AC-F6-08，Logic — PlayMode）**
  - Given：BeatClock=Playing，InputEventBus=GameplayActive
  - When：发布 TDE Pause 事件
  - Then：同帧 `BeatClock.Pause()` == 1，BeatClock=Paused，InputEventBus=Paused（输入被门控）
  - Edge cases：连续两次 Pause → `Pause()` 仅生效 1 次，无二次副作用

**AC-2（AC-F6-09，Logic — PlayMode）**
  - Given：BeatClock=Paused，InputEventBus=Paused
  - When：发布 TDE UnPause 事件
  - Then：同帧 `BeatClock.Resume()` == 1，BeatClock=Playing，InputEventBus=GameplayActive
  - Edge cases：未暂停态收到 UnPause → 无副作用（不误 Resume）；Pause→UnPause→Pause 序列状态一致

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- `Assets/Tests/PlayMode/F6/PauseResumeSameFrame_test.cs`（AC-08/09，BeatClock + InputEventBus Spy 同帧断言）— 必须存在且通过

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 002（boot 骨架 + BeatClock 生命周期接线）
- Unlocks: None
