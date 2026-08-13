# Story 008: Meter 双事件（Accepted/Changed）+ ScheduleBGMTransition

> **Epic**: F2 音频/拍点时钟系统
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: M
> **Manifest Version**: 2026-08-12
> **Last Updated**: [set by /dev-story when implementation begins]

## Context

**GDD**: `design/gdd/audio-beat-clock.md`
**Requirement**: `TR-beatclock-006`（拍号切换事件侧）
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-D6: 音频中间件选型（primary）；secondary: ADR-D1（BGM 过渡调度归属）
**ADR Decision Summary**: 拍号变更采用双阶段事件契约：`OnMeterChangeAccepted`（请求被接受、边界待生效时广播）与 `OnMeterChanged`（边界实际生效时广播），供下游区分「已排期」与「已生效」。BGM 过渡在 DSP 边界调度。

**Engine**: Unity 6.3 LTS (6000.3.19f1) | **Risk**: MEDIUM
**Engine Notes**: `ScheduleBGMTransition` 经 `IAudioPlayer.PlayScheduled` 在生效边界的 raw DSP 时刻提交，保证音乐段落切换与拍网格对齐。

**Control Manifest Rules (this layer)**:
- Required: 双事件成对广播（Accepted 先，Changed 后）；payload 为 `readonly struct MeterChangeEventData`
- Forbidden: 只广播其一（下游依赖成对语义区分排期/生效）
- Guardrail: 事件零 GC；BGM 过渡时刻为 raw 坐标

---

## Acceptance Criteria

*From GDD `design/gdd/audio-beat-clock.md` Core Rule 14 + MeterChangeEventData 结构，scoped to this story:*

- [ ] 拍号切换请求被接受进入 Pending 时广播 `OnMeterChangeAccepted`（payload 含旧拍号 / 新拍号 / 生效边界拍序号）
- [ ] 切换在小节边界实际生效时广播 `OnMeterChanged`（payload 含生效拍号 / 生效 raw+adjusted 时刻）
- [ ] 两事件成对出现：每个被接受的切换恰对应一次 Accepted + 一次 Changed
- [ ] `ScheduleBGMTransition` 在生效边界的 raw DSP 时刻经 `IAudioPlayer.PlayScheduled` 提交音乐段过渡
- [ ] 被拒切换（非法值）不广播任何 Meter 事件

---

## Implementation Notes

*Derived from ADR-D6 + ADR-D1 Implementation Guidelines:*

- 双事件挂接 Story 007 状态机：`RequestMeterChange` 接受即广播 Accepted；调度器边界应用那帧广播 Changed。两者共享同一 `MeterChangeEventData`（`readonly struct`）。
- `ScheduleBGMTransition`：生效边界拍的 raw 时刻 = `nth_beat_dsp_time(boundary_n)`（raw），提交给 `IAudioPlayer.PlayScheduled`；BGM 过渡与拍切换对齐，避免音乐撕裂。
- BGM 过渡的**具体音乐资源选择**属 F6/FE4，本故事只负责在正确 DSP 时刻发起调度调用（边界隔离——不 using Feel/FMOD 事件命名）。
- 被拒请求路径（Story 007 校验失败）直接返回，不触发任何广播。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 007: Pending 状态机与网格重锚（本故事挂其广播）
- F6/FE4: BGM 资源选择与音乐层混音
- BPM 变更事件（GDD 未定义 BPM 专属双事件，BPM 走 Story 007 状态切换）

---

## QA Test Cases

*嵌自 GDD Acceptance Criteria（Given/When/Then），Integration 自动化规格。*

- **AC-F2-24（Accepted 于接受时广播）** `[EditMode]`
  - Given: 拍号 4/4，`beat_in_bar=1`
  - When: `RequestMeterChange(3/4)` 被接受
  - Then: 立即广播 `OnMeterChangeAccepted`，payload 旧=4/4、新=3/4、生效边界拍序号 = 下一小节首拍

- **AC-F2-25（Changed 于生效时广播）** `[EditMode]`
  - Given: 上述 Pending 切换
  - When: 调度越过小节边界
  - Then: 广播 `OnMeterChanged`，payload 生效拍号=3/4、生效 raw 时刻 = 边界拍 raw；发生在 Accepted 之后

- **AC-F2-26（成对语义）** `[EditMode]`
  - Given: 一次被接受的拍号切换
  - When: 完整跨边界
  - Then: Accepted 与 Changed 各恰一次；被拒切换两者均 0 次

- **AC-F2-27（BGM 过渡边界调度）** `[EditMode]`
  - Given: 拍号切换生效边界 raw 时刻已知
  - When: 切换生效
  - Then: `MockAudioPlayer` 记录一次 `PlayScheduled(boundary_raw)`，时刻 == 边界拍 raw 时刻
  - Edge cases: 被拒切换不触发 `ScheduleBGMTransition`

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- `Assets/Tests/EditMode/F2/MeterEventTests.cs` — 双事件成对 + BGM 调度，必须通过（BLOCKING）

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 007（切换状态机）、Story 001（`IAudioPlayer`）
- Unlocks: FE4 音乐层 / 下游拍号响应消费者
