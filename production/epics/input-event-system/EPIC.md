# Epic: F3 Input 事件系统

> **Layer**: Foundation
> **GDD**: design/gdd/input-event-system.md
> **Architecture Module**: F3 InputEventBus
> **Status**: Ready
> **Stories**: Not yet created — run `/create-stories input-event-system`

## Overview

本 epic 实现输入管线基础设施：将玩家原始硬件输入（键鼠/手柄/触屏）转化为携带精确 DSP 时间戳的语义输入事件，经 C# 事件总线 `IInputEventBus` 广播给下游。攻击输入时间戳在 `InputAction.performed` 回调中通过「同帧双读」策略同步捕获——`IDSPClock.GetDspTime()`（DSP 时钟域，与 F2 共用同一 `IDSPClock` 接口，ADR-D6 已选定 FMOD）与 `ctx.time`（游戏时间域，调试用）。ChargedAttack 额外在 `Hold.started` 捕获 `chargeStartDspTime`。F3 不知晓量化模式/BPM/拍号，只负责精确捕获与传递；控制方案切换对下游透明，键位重绑定经 Input System 原生支持、绑定数据由 F5 持久化。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-D1: TDE 集成 | `JadeRabbitInputManager` 继承 `InputSystemManagerEventsBased`，代理 Action Map 状态给 F3，保留 TDE `AbilityInputAuthorization` 管线 | HIGH |
| ADR-D8: DI / 测试缝隙 | `IInputEventBus` 声明为接口，支持纯 C# NUnit 测试 | LOW |
| ADR-D6: 音频中间件选型 | F3 通过与 F2 相同的 `IDSPClock` 接口保持同一 DSP 时钟域（生产实现 `FMODDSPClockImpl`） | HIGH |

## GDD Requirements

覆盖统计（traceability-index Rev.10）：15 TR 中 2 Covered / 0 Partial / 13 Gap。

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-input-010 | Action Map 状态代理给 F3，保留 TDE 授权管线 | ADR-D1 ✅ |
| TR-input-014 | `InputSystemManagerEventsBased.BindButton` 唯一 override 虚方法 | ADR-D1 ✅ |
| TR-input-015 | 输入时间戳同帧双读（DSP + 游戏时间域） | ADR-D1 ✅ |
| （其余 12 项 TR-input-001..013） | InputEvent 结构、设备透明化、移动持续值传递等应用逻辑 | GDD 即规范（预期内部项，非 ADR 缺口） |

> ⚠️ 13 项 Gap 均为「GDD 即规范」预期内部项——输入管线应用逻辑以 GDD 为规范文档。架构评审（2026-08-12）已确认非真实缺口，不阻塞 story 创建。

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/input-event-system.md` are verified
- All Logic and Integration stories have passing test files in `tests/`
- All Visual/Feel and UI stories have evidence docs with sign-off in `production/qa/evidence/`

## Next Step

Run `/create-stories input-event-system` to break this epic into implementable stories.
