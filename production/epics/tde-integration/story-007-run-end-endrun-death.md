# Story 007: run-end 统一边界 EndRun + Death 路径 + RespawnComplete 守卫

> **Epic**: F6 TopDownEngine & Animancer 集成层
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: [fill before sprint planning]
> **Manifest Version**: 2026-08-12
> **Last Updated**: [set by /dev-story when implementation begins]

## Context

**GDD**: `design/gdd/tde-integration.md`
**Requirement**: `TR-tde-013`（统一 run-end 边界，COVERED D12+D13）
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-D12: 死亡/换新局生命周期（primary）；ADR-D13: run-end 胜利边界
**ADR Decision Summary**: run 结束经单一入口 `EndRun(RunOutcome outcome)`，一次性闩锁 `_runEnded` 防重入；同帧执行 `BeatClock.Stop()` + `InputEventBus.DisableAllInput()` + 广播 `OnRunEnded(outcome)`。`RunOutcome` 为纯 C# enum `{Death, Victory}`，不含任何 TDE 类型。MVP 下 `RespawnComplete` 休眠（死亡=换新局，不原地复活）。

**Engine**: Unity 6.3 LTS (6000.3.19f1) | **Risk**: MEDIUM
**Engine Notes**: TDE 玩家死亡事件成员名须对照 `docs/engine-reference/unity/plugins/top-down-engine.md`；`RunOutcome` enum 与 `OnRunEnded` 为本项目纯 C# 定义，无引擎依赖。

**Control Manifest Rules (Foundation)**:
- Required: `EndRun(RunOutcome)` 为 run 结束唯一出口；`_runEnded` 一次性闩锁（重入不重复广播）
- Required: `RunOutcome` 纯 C# enum `{Death, Victory}`，禁含 TDE 类型
- Required: MVP `RespawnComplete` 休眠守卫——不原地复活，死亡=换新局 boot
- Forbidden: 死亡/胜利各自直接 `Stop()`/广播（必须经 `EndRun` 统一出口）

---

## Acceptance Criteria

*From GDD `design/gdd/tde-integration.md`, scoped to this story:*

- [ ] AC-F6-10：TDE 玩家死亡事件 → `EndRun(RunOutcome.Death)`，同帧 `BeatClock.Stop()` == 1 + `InputEventBus.DisableAllInput()` == 1 + `OnRunEnded(Death)` 广播恰 1 次
- [ ] AC-F6-11：`EndRun` 重入（同 run 内二次触发）被 `_runEnded` 闩锁拦截，`Stop()`/广播不重复
- [ ] AC-F6-25：MVP 下收到 TDE `RespawnComplete` 不触发原地复活（守卫拦截，run 已 end，等换新局 boot）

---

## Implementation Notes

*Derived from ADR-D12 + ADR-D13 + Control Manifest run-end rules:*

- 定义纯 C# `enum RunOutcome { Death, Victory }` 与 `event Action<RunOutcome> OnRunEnded`（`TDELifecycleAdapter` 或专属 run-end 组件上）。
- `EndRun(RunOutcome outcome)`：入口先检 `_runEnded`，已置位则 `return`；否则置 `_runEnded = true`，同帧顺序 `BeatClock.Stop()` → `InputEventBus.DisableAllInput()` → `OnRunEnded?.Invoke(outcome)`。
- 死亡路径：订阅 TDE 玩家死亡事件（成员名先查 `top-down-engine.md`）→ 调 `EndRun(RunOutcome.Death)`。
- 显式胜利路径（非 TDE 事件桥接的那一半）：任何已知胜利判定可直接调 `EndRun(RunOutcome.Victory)`——TDE 原生关卡完成事件的桥接留待 Story 008（TR-tde-014 硬门）。
- `RespawnComplete` 守卫：MVP 下收到 TDE 复活完成事件时，若 `_runEnded` 已置位则忽略（不原地复活）；`_runEnded` 于新局 boot 时重置。

---

## Out of Scope

- Story 008：TDE 原生胜利/关卡完成事件 → `EndRun(Victory)` 的桥接（BLOCKED，成员名未验证 TR-tde-014）
- 结算经济学 / 胜利奖励（defer FE5 + game-designer）
- 换新局 boot 序列复用 Story 002（本 story 只管 `_runEnded` 重置钩子）

---

## QA Test Cases

*Embedded from GDD AC-F6-10/11/25（lean 模式）。*

**AC-1（AC-F6-10，Logic — PlayMode）**
  - Given：BeatClock=Playing，InputEventBus=GameplayActive，`_runEnded=false`
  - When：发布 TDE 玩家死亡事件
  - Then：同帧 `EndRun(Death)`；`BeatClock.Stop()` == 1，`InputEventBus.DisableAllInput()` == 1，`OnRunEnded` 收到 `Death` 恰 1 次
  - Edge cases：死亡事件与胜利判定同帧到达 → 先到者 latch，第二者被拦（outcome 唯一）

**AC-2（AC-F6-11，Logic — EditMode/PlayMode）**
  - Given：`EndRun(Death)` 已执行（`_runEnded=true`）
  - When：再次调 `EndRun(Death)` 或 `EndRun(Victory)`
  - Then：`Stop()` 不新增（累计仍 == 1），`OnRunEnded` 不再广播（累计仍 == 1）
  - Edge cases：连续 3 次重入均被拦

**AC-3（AC-F6-25，Logic — PlayMode）**
  - Given：`_runEnded=true`（run 已因死亡结束）
  - When：发布 TDE `RespawnComplete` 事件
  - Then：无原地复活（角色未重置到关卡内），BeatClock 保持 Stopped，等待换新局 boot
  - Edge cases：新局 boot 后 `_runEnded` 重置为 false，后续死亡可再次正常 `EndRun`

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- `Assets/Tests/PlayMode/F6/RunEndBoundary_death_test.cs`（AC-10/11/25，死亡事件 + 重入闩锁 + RespawnComplete 守卫）— 必须存在且通过
- `Assets/Tests/EditMode/F6/EndRun_latch_test.cs`（AC-11 纯逻辑闩锁幂等）— 必须存在且通过

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 002（boot 骨架 + BeatClock/InputEventBus 接线）
- Unlocks: Story 008（胜利桥接复用同一 `EndRun(Victory)` 出口）
