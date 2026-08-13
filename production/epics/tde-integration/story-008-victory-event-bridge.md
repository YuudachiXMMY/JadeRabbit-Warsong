# Story 008: [BLOCKED] TDE 原生胜利/关卡完成事件桥接

> **Epic**: F6 TopDownEngine & Animancer 集成层
> **Status**: Blocked
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: M
> **Manifest Version**: 2026-08-12
> **Last Updated**: [set by /dev-story when implementation begins]

> **⚠ BLOCKED — TR-tde-014 HARD GATE**: TDE 原生胜利/关卡完成事件的成员名尚未对照 `docs/engine-reference/unity/plugins/top-down-engine.md` 验证。实现前必须先完成成员名验证（ADR-D13 VR1 硬门），严禁从训练数据推断 TDE 事件枚举/成员名。验证通过后将本 story 改为 `Ready`。

## Context

**GDD**: `design/gdd/tde-integration.md`
**Requirement**: `TR-tde-014`（TDE 胜利事件桥接，PARTIAL — ADR-D13 VR1 硬门，成员名未验证）
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-D13: run-end 胜利边界（primary）；ADR-D12: 死亡/换新局生命周期
**ADR Decision Summary**: TDE 原生关卡完成/胜利事件到达时，桥接至 Story 007 的统一出口 `EndRun(RunOutcome.Victory)`，复用同一 `_runEnded` 闩锁与 `Stop()`/广播序列，闭合胜利路径的 double-Play 与令牌孤儿风险。前置硬门：TDE 事件成员名须先经引擎参考文档验证。

**Engine**: Unity 6.3 LTS (6000.3.19f1) | **Risk**: HIGH
**Engine Notes**: **TR-tde-014 HARD GATE** — TDE 关卡完成/胜利事件（可能是 `TopDownEngineEvent` 某枚举成员或 `MMGameEvent`/`CorgiEngineEvent` 类比）成员名**必须**先对照 `docs/engine-reference/unity/plugins/top-down-engine.md` 确认存在与签名，严禁臆测。

**Control Manifest Rules (Foundation)**:
- Required: 胜利经 Story 007 的 `EndRun(RunOutcome.Victory)` 统一出口（禁止另起 `Stop()`/广播路径）
- Forbidden: 未验证 TDE 事件成员名不得写入实现代码（TR-tde-014 硬门）
- Required: `RunOutcome.Victory` 复用纯 C# enum，桥接层不泄漏 TDE 类型进 `OnRunEnded`

---

## Acceptance Criteria

*From GDD `design/gdd/tde-integration.md` + ADR-D13 VR1，scoped to this story:*

- [ ] **前置**：TDE 胜利/关卡完成事件成员名经 `top-down-engine.md` 验证并记录（解除 BLOCKED 的必要条件）
- [ ] TDE 原生胜利事件 → `EndRun(RunOutcome.Victory)`，同帧 `BeatClock.Stop()` == 1 + `InputEventBus.DisableAllInput()` == 1 + `OnRunEnded(Victory)` 广播恰 1 次
- [ ] 胜利与死亡同 run 竞争时，`_runEnded` 闩锁保证仅首个 outcome 生效（无 double-Stop/double 广播）

---

## Implementation Notes

*Derived from ADR-D13 + Story 007 EndRun 出口。实现前先解 BLOCKED：*

1. **解门步骤（先做）**：读 `docs/engine-reference/unity/plugins/top-down-engine.md`，定位关卡完成/胜利事件的确切类型与枚举成员名，记录到本 story 或 ADR-D13。若参考文档缺该事件，须先补录引擎参考（勿臆测）。
2. 订阅该 TDE 事件（`MMEventListener<T>` 或对应监听接口）。
3. 事件回调内调 Story 007 的 `EndRun(RunOutcome.Victory)`——不重复 `Stop()`/广播逻辑，纯桥接。
4. 桥接层不将 TDE 事件对象传入 `OnRunEnded`（只传 `RunOutcome.Victory`）。

---

## Out of Scope

- Story 007：`EndRun` / `_runEnded` / `OnRunEnded` 出口本身（本 story 复用，不重写）
- 结算经济学 / 胜利奖励（defer FE5 + game-designer）
- 死亡路径（Story 007 已覆盖）

---

## QA Test Cases

*Embedded from GDD + ADR-D13 VR1（lean 模式）。BLOCKED 期间不实现测试，仅记录规格。*

**AC-1（胜利桥接，Integration — PlayMode，解门后）**
  - Given：BeatClock=Playing，`_runEnded=false`，TDE 胜利事件成员名已验证
  - When：发布已验证的 TDE 关卡完成/胜利事件
  - Then：`EndRun(Victory)` 触发；`BeatClock.Stop()` == 1，`InputEventBus.DisableAllInput()` == 1，`OnRunEnded` 收到 `Victory` 恰 1 次
  - Edge cases：胜利事件后再来死亡事件 → 被 `_runEnded` 拦（outcome 仍 Victory）

**AC-2（死亡/胜利竞争，Logic — PlayMode，解门后）**
  - Given：同帧死亡事件与胜利事件均到达
  - When：两事件在同一帧派发
  - Then：仅首个 outcome 生效，`Stop()` 累计 == 1，`OnRunEnded` 广播 == 1
  - Edge cases：无

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- `Assets/Tests/PlayMode/F6/VictoryBridge_test.cs`（胜利桥接 + 竞争闩锁）— 解 BLOCKED 后必须存在且通过

**Status**: [ ] Blocked — 待 TR-tde-014 成员名验证解门后创建

---

## Dependencies

- Depends on: Story 007（`EndRun(Victory)` 出口）；**硬门**：TR-tde-014 TDE 胜利事件成员名验证（`top-down-engine.md`）
- Unlocks: None（run-end 边界闭合的最后一块）
