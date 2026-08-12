# Epic: F2 音频/拍点时钟系统

> **Layer**: Foundation
> **GDD**: design/gdd/audio-beat-clock.md
> **Architecture Module**: F2 BeatClock（含 F2 生命周期面）
> **Status**: Ready
> **Stories**: Not yet created — run `/create-stories audio-beat-clock`

## Overview

本 epic 实现节奏战斗体系的时序基础设施：持续将音乐拍点（BPM + 拍号）广播为高精度 DSP 时间戳，作为所有下游节奏系统的唯一真值时钟。BeatClock 对外暴露只读消费面 `IBeatClock`（拍点/半拍前瞻与精确事件、拍号变更、Pause/Resume、快照与 `LatencyOffsetMs`），并支持运行时 BPM/拍号切换（Boss 阶段所需）。生命周期控制（Play/Stop/Pause/Resume）经 `IBeatClockLifecycle` 暴露，唯一控制者为 F6 `TDELifecycleAdapter`。底层音频中间件按 ADR-D6 选定 FMOD，GDD 定义与中间件无关的行为规格。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-D6: 音频中间件选型 | 选定 FMOD Studio 2.x；DSP 时钟真值取自 master ChannelGroup，禁用 `AudioSettings.outputSampleRate` | HIGH |
| ADR-D8: DI / 测试缝隙 | `IBeatClock` 声明为接口，支持纯 C# NUnit 测试（`SetBeatClockForTesting` 在 `#if` 块内） | LOW |
| ADR-D10: Bootstrap 顺序 | 单一显式 bootstrap driver 注入初始化顺序；`LatencyOffsetMs` boot 期注入 | LOW |
| ADR-D1: TDE 集成 | BeatClock 生命周期唯一控制者 = F6 `TDELifecycleAdapter` | MEDIUM |

## GDD Requirements

覆盖统计（traceability-index Rev.10）：17 TR 中 1 Covered / 3 Partial / 13 Gap。

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-beatclock-001 | DontDestroyOnLoad 上的 MonoBehaviour 单例 BeatClock | GDD 规范 ❌ 无 ADR |
| TR-beatclock-002 | DSP 时钟时间戳精度 < 0.1ms（锚定公式，无迭代累加） | ADR-D6 ✅ |
| TR-beatclock-003 | 全网格广播：每拍与每半拍事件，量化模式无关 | GDD 规范 ❌ 无 ADR |
| TR-beatclock-007 | `LatencyOffsetMs` boot 期注入 | ADR-D10 ✅（Partial） |
| TR-beatclock-010 | FMOD DSP 时钟采集路径 | ADR-D6 ✅ |
| （其余 12 项 TR-beatclock-004..017） | 拍号切换、快照、Pause/Resume 发布权等应用逻辑 | GDD 即规范（预期内部项，非 ADR 缺口） |

> ⚠️ 13 项 Gap 均为「GDD 即规范」预期内部项——BeatClock 应用逻辑以 GDD 为规范文档，不单列 ADR 缺口。架构评审（2026-08-12）已确认非真实缺口，不阻塞 story 创建。

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/audio-beat-clock.md` are verified
- All Logic and Integration stories have passing test files in `tests/`
- All Visual/Feel and UI stories have evidence docs with sign-off in `production/qa/evidence/`

## Next Step

Run `/create-stories audio-beat-clock` to break this epic into implementable stories.
