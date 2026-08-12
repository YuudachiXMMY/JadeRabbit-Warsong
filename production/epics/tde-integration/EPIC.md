# Epic: F6 TopDownEngine & Animancer 集成层

> **Layer**: Foundation
> **GDD**: design/gdd/tde-integration.md
> **Architecture Module**: F6 TDELifecycleAdapter
> **Status**: Ready
> **Stories**: Not yet created — run `/create-stories tde-integration`

## Overview

本 epic 实现引擎整合地基：在 TopDownEngine（TDE）提供的移动/生命/关卡/反馈基础设施与项目自建 Foundation 系统（F2 BeatClock、F3 InputEventBus、F4 LevelConfigSO）之间建立明确合约边界，并定义 Animancer 攻击动画层与 TDE 原生 Animator 层的分工协议，使攻击动画时长随 BPM 动态缩放。由两个组件构成：`JadeRabbitInputManager`（`InputSystemManagerEventsBased` 子类，代理 Action Map 给 F3，保留 TDE `AbilityInputAuthorization`）与 `TDELifecycleAdapter`（DontDestroyOnLoad Manager 上的适配器，是 `BeatClock.Play/Stop/Pause/Resume` 的唯一控制者，并在每次单场景加载后注入 `LevelConfigSO` 的 BPM/拍号）。run-end 统一边界：单一出口 `EndRun(RunOutcome)` + 每局一次性 latch，同帧执行 `BeatClock.Stop()` + `InputEventBus.DisableAllInput()` + 广播 `OnRunEnded(outcome)`；死亡（TDE PlayerDeath 桥接）与胜利路径均汇入。暂停/恢复/死亡/重生原子序列确保 BeatClock 与输入状态始终同步（无「死亡前幽灵攻击」）。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-D1: TDE 集成 | `JadeRabbitInputManager` + `TDELifecycleAdapter` 双组件；BeatClock 生命周期唯一控制者；`.Respawn`→`.RespawnComplete` 事件名核对先例 | MEDIUM |
| ADR-D5: 动画管线 | Animancer `AnimancerState.Speed = currentBpm / node.authoredBpm`，无硬编码帧数 | MEDIUM |
| ADR-D10: Bootstrap 顺序 | boot 期 offset 注入 + 单场景加载后 `LevelConfigSO` 注入路径 | LOW |
| ADR-D12: 死亡 & run-lifecycle | run-ends-on-death 边界；死亡经 `EndRun()` 单一出口，`OnRunEnded(Death)` 取代旧 `OnPlayerDeath`（无别名） | LOW |
| ADR-D13: run-end 胜利统一 | 死亡与胜利经同一 `EndRun(RunOutcome)`，`OnRunEnded(RunOutcome)` 对称；胜利路径闭合 double-Play/令牌孤儿 | LOW |

## GDD Requirements

覆盖统计（traceability-index Rev.10）：14 TR 中 9 Covered / 1 Partial / 4 Gap。

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-tde-001/002/003/006/007 | 双组件架构、生命周期唯一控制、`LevelConfigSO` 注入 | ADR-D1 ✅ |
| TR-tde-004/005 | Animancer 攻击层 BPM 变速、无硬编码帧数 | ADR-D5 ✅ |
| TR-tde-013 | 统一 run-end 边界 `EndRun(RunOutcome)` 单一出口 + latch + 同帧 Stop/Disable/广播 | ADR-D12 + ADR-D13 ✅ |
| TR-tde-014 | TDE 胜利/通关事件成员名 **须先核对引擎参考** 再实装桥接 | ADR-D13 VR1 ⚠️ Partial |
| （其余未来 GDD 缺口） | Post-MVP TDE 扩展等 | 未来 GDD |

> ⚠️ **实装前硬门禁**：TR-tde-014——TDE 胜利/通关事件成员名必须先对照 `docs/engine-reference/unity/plugins/top-down-engine.md` 核对（严禁从训练数据推断，参 ADR-D1 `.Respawn`→`.RespawnComplete` 先例）；若无原生事件，胜利由 FE4/FE2/FE5 经 `EndRun(RunOutcome.Victory)` 显式触发。**涉及胜利桥接的 story 将标 Blocked 直至事件名核对完成**（已登记于 entities.yaml pre_implementation_checks）。

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/tde-integration.md` are verified
- All Logic and Integration stories have passing test files in `tests/`
- All Visual/Feel and UI stories have evidence docs with sign-off in `production/qa/evidence/`
- TR-tde-014 TDE 胜利事件成员名已对照引擎参考核对（胜利桥接 story 解除 Blocked 的前置）

## Next Step

Run `/create-stories tde-integration` to break this epic into implementable stories.
