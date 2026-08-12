# Story 009: Pause/Resume + DSP 重锚 + BC-6 暂停恢复事件

> **Epic**: F2 音频/拍点时钟系统
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: [fill before sprint planning]
> **Manifest Version**: 2026-08-12
> **Last Updated**: [set by /dev-story when implementation begins]

## Context

**GDD**: `design/gdd/audio-beat-clock.md`
**Requirement**: `TR-beatclock-008`（暂停/恢复与 DSP 重锚）
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-D1: TDE 集成（primary）；secondary: ADR-D6（`IAudioPlayer.Pause/Resume` DSP 语义）
**ADR Decision Summary**: 暂停/恢复经 `IBeatClockLifecycle.Pause()`/`Resume()`（唯一控制者 F6）；恢复时须重锚 `dsp_start_time` 补偿暂停期间流逝的 DSP 时间，保证拍序号连续。BC-6 契约：`OnBeatClockPaused`/`OnBeatClockResumed` 成对广播，payload 为 `readonly struct`。

**Engine**: Unity 6.3 LTS (6000.3.19f1) | **Risk**: MEDIUM
**Engine Notes**: FMOD DSP 时钟在 `Pause()` 后可能继续前进（取决于 pause 语义）——恢复重锚须以「暂停时刻」与「恢复时刻」的 DSP 差值补偿 `dsp_start_time`，不可假设 DSP 冻结。

**Control Manifest Rules (this layer)**:
- Required: Pause/Resume 经 `IBeatClockLifecycle`（唯一控制者）；BC-6 双事件成对
- Forbidden: `freeze_pauses_beatclock` —— Perfect 命中冻帧**不得**暂停 BeatClock（冻帧是表现层，时钟继续走）
- Guardrail: 恢复后拍序号连续、`last_raw_beat_dsp_time` 与 `dsp_start_time` 一致重锚

---

## Acceptance Criteria

*From GDD `design/gdd/audio-beat-clock.md` Core Rule 8 + BeatClockPauseEventData 结构，scoped to this story:*

- [ ] `Pause()`：状态 Playing → Paused，记录 `pause_dsp_time`（raw），调 `IAudioPlayer.Pause()`，暂停调度广播
- [ ] `Resume()`：状态 Paused → Playing，按 `resume_dsp − pause_dsp` 差值重锚 `dsp_start_time`，拍序号从暂停处连续，调 `IAudioPlayer.Resume()`
- [ ] `OnBeatClockPaused` 于 `Pause()` 广播，`OnBeatClockResumed` 于 `Resume()` 广播（BC-6 成对，`readonly struct` payload）
- [ ] 暂停期间 `GetSnapshot()` 的 `IsPaused=true`，不推进拍序、不广播 Exact/Lookahead
- [ ] 冻帧（Perfect 命中）不调用 `Pause()`——BeatClock 时钟不受冻帧影响（`freeze_pauses_beatclock` 禁令）

---

## Implementation Notes

*Derived from ADR-D1 + ADR-D6 Implementation Guidelines:*

- **重锚公式**：`Resume()` 时 `dsp_start_time += (resume_dsp_raw − pause_dsp_raw)`。这样 `nth_beat_dsp_time(n)` 对所有 n 平移暂停时长，拍序号与 `last_raw_beat_dsp_time` 保持连续（暂停不丢拍、不跳拍）。
- DSP 不假设冻结：即使 FMOD 在 pause 后 DSP 继续走，重锚用实际 `GetDspTime()` 差值补偿，故正确。`last_adjusted_beat_dsp_time` 同步平移。
- BC-6 契约：两事件配对，payload `BeatClockPauseEventData`（含暂停/恢复 raw 时刻）。禁只发其一。
- **冻帧隔离**：Perfect 命中的画面定格由 PL2/表现层处理，绝不经 BeatClock `Pause()`——`freeze_pauses_beatclock` 是 registry 注册的 forbidden pattern。本故事测试须验证冻帧场景下时钟仍推进（负向断言）。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 003: `Stop()`（区别于 Pause——Stop 清状态，Pause 保状态）
- Story 010: 音频不可用降级（区别于主动 Pause）
- Story 013: 移动端挂起的 DSP 不连续检测（区别于显式 Pause）
- PL2: 冻帧表现层本体

---

## QA Test Cases

*嵌自 GDD Acceptance Criteria（Given/When/Then），Integration 自动化规格。*

- **AC-F2-16（Pause 停止推进 + 事件）** `[EditMode]`
  - Given: 状态 Playing，`dsp_now=2.0`
  - When: `Pause()`
  - Then: 状态 Paused、`IsPaused=true`、`MockAudioPlayer.Pause()` 触发、`OnBeatClockPaused` 广播；此后推进 `dsp_now` 不广播 Exact

- **AC-F2-17（Resume 重锚拍序连续）** `[EditMode]`
  - Given: 在 `dsp_now=2.0` 暂停（此时第 4 拍），`dsp_now` 推进到 5.0 后 `Resume()`
  - Then: `dsp_start_time` 补偿 +3.0；恢复后下一个 `OnBeatExact` 为第 5 拍（不因暂停期跳到第 10 拍）；`OnBeatClockResumed` 广播
  - Edge cases: 暂停期极长（30s）恢复后仍拍序连续

- **AC-F2-18（BC-6 成对语义）** `[EditMode]`
  - Given: 一次 Pause→Resume 循环
  - Then: Paused 与 Resumed 各恰一次；未 Pause 时 `Resume()` 幂等无广播

- **AC-F2-28（冻帧不暂停时钟）** `[EditMode]`
  - Given: 模拟 Perfect 命中冻帧信号（表现层）
  - When: 冻帧期间推进 `dsp_now`
  - Then: BeatClock **未** 进入 Paused、`OnBeatExact` 继续广播（证明 `freeze_pauses_beatclock` 未被违反）

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- `Assets/Tests/EditMode/F2/PauseResumeTests.cs` — 重锚 + BC-6 + 冻帧负向断言，必须通过（BLOCKING）

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 003（起点锚定）、Story 004（调度器广播）
- Unlocks: F6 生命周期 adapter 的 Pause/Resume 转发
