# Story 003: Play/Stop 生命周期 + DSP 起始锚定协议 + 配置校验

> **Epic**: F2 音频/拍点时钟系统
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: M
> **Manifest Version**: 2026-08-12
> **Last Updated**: [set by /dev-story when implementation begins]

## Context

**GDD**: `design/gdd/audio-beat-clock.md`
**Requirement**: `TR-beatclock-013`（生命周期唯一控制者）, `TR-beatclock-009`（起始/降级 · 部分）, `TR-beatclock-001`（单例配置校验 · 部分）
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-D1: TDE 集成（primary）；secondary: ADR-D6（`PlayScheduled` DSP 锚定）、ADR-D10（bootstrap 顺序与 Play 触发点）
**ADR Decision Summary**: BeatClock 生命周期唯一控制者 = F6 `TDELifecycleAdapter`，经 `IBeatClockLifecycle` 调用 `Play()`/`Stop()`/`ConfirmDSPStart()`；消费系统禁触生命周期（`beatclock_lifecycle_bypass`）。

**Engine**: Unity 6.3 LTS (6000.3.19f1) | **Risk**: MEDIUM
**Engine Notes**: `Play()` 须以 `IAudioPlayer.PlayScheduled(dspTimeRaw)` 提交未来 DSP 起点（不可用 `Time.time`）；起点须留调度前瞻裕量（避免以已过 DSP 时刻调度导致丢首拍）。

**Control Manifest Rules (this layer)**:
- Required: 生命周期方法仅存在于 `IBeatClockLifecycle`；`TDELifecycleAdapter` 为唯一调用者（ADR-D1）
- Forbidden: `beatclock_lifecycle_bypass` —— 任何消费系统直接调 `Play`/`Stop`
- Guardrail: `Play()` 提交的 `dsp_start_time` 为 raw 坐标；起点 = `GetDspTime() + 前瞻裕量`

---

## Acceptance Criteria

*From GDD `design/gdd/audio-beat-clock.md` Core Rule 2/5 + States/Transitions，scoped to this story:*

- [ ] `Play()` 经 `IAudioPlayer.PlayScheduled()` 提交未来 DSP 起点，`dsp_start_time` 记录为 raw 坐标；从 Idle → Playing
- [ ] `ConfirmDSPStart()` 在音频实际起播回调后落定 `dsp_start_time`，`IsStartConfirmed` 转 true，`GetConfirmedDSPStart()` 返回该值
- [ ] `Stop()` 立即停止（映射 `FMOD_STUDIO_STOP_IMMEDIATE`），状态回 Idle，清空调度状态；可重复 `Stop()` 幂等
- [ ] 起播配置（BPM / 拍号）在 `Play()` 前校验；非法值（BPM ∉ [80,240]、拍号非支持）拒绝起播并记录明确错误
- [ ] `Play()` 前若时钟源/音频源未注入，进入音频不可用降级路径（详见 Story 010）而非崩溃

---

## Implementation Notes

*Derived from ADR-D1 + ADR-D6 + ADR-D10 Implementation Guidelines:*

- `Play()` 不得以「当前 DSP」为起点直接起播——须 `dsp_start = GetDspTime() + lookahead_margin`，`PlayScheduled(dsp_start)` 提交，保证调度器有前瞻窗口锚定首拍。
- `IsStartConfirmed` 门控：起点确认前，所有拍点广播/快照须以「未确认」姿态处理（下游 HUD 不显进度）。确认回调落定后才开始广播——防止以推测起点误播首拍。
- `Stop()` 语义为立即停止；渐出（fade-out）属 F6/PL2，不在 BeatClock 契约内。
- 配置校验在 `Play()` 入口做一次；BPM/拍号运行时切换的校验归 Story 007。
- `TDELifecycleAdapter`（F6）为唯一调用点——本故事只实现 `IBeatClockLifecycle` 方法体，不实现 adapter（adapter 属 F6 epic）。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 004: 起点确认后的调度器广播循环
- Story 007: 运行时 BPM/拍号切换校验
- Story 009: Pause/Resume（区别于 Stop 的 DSP 重锚）
- Story 010: 音频不可用降级的完整状态机与恢复
- F6: `TDELifecycleAdapter` 本体

---

## QA Test Cases

*嵌自 GDD Acceptance Criteria（Given/When/Then），Integration 自动化规格。*

- **AC-F2-08（Play 提交未来 DSP 起点）** `[EditMode]`
  - Given: `MockDSPClock.dsp_now = 5.000`，BPM=120，拍号 4/4
  - When: `lifecycle.Play()`
  - Then: `MockAudioPlayer` 记录一次 `PlayScheduled(dsp_start)` 且 `dsp_start > 5.000`（含前瞻裕量）；状态 = Playing
  - Edge cases: 未注入音频源 → 走降级路径（Story 010），不抛未捕获异常

- **AC-F2-09（ConfirmDSPStart 落定起点）** `[EditMode]`
  - Given: `Play()` 已调用，起播回调触发 `ConfirmDSPStart(dsp_start_actual)`
  - When: 查询 `IsStartConfirmed` 与 `GetConfirmedDSPStart()`
  - Then: `IsStartConfirmed == true`；返回 `dsp_start_actual`；确认前 `IsStartConfirmed == false` 且不广播拍点

- **AC-F2-10（Stop 立即停止 + 幂等）** `[EditMode]`
  - Given: 状态 Playing
  - When: `lifecycle.Stop()` 调用两次
  - Then: 首次 `MockAudioPlayer.Stop()` 触发、状态 = Idle、调度状态清空；第二次幂等无副作用/无异常
  - Edge cases: Idle 状态直接 `Stop()` 幂等

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- `Assets/Tests/EditMode/F2/BeatClockLifecycleTests.cs` — 必须存在并通过（BLOCKING）

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001（`IBeatClockLifecycle` / `IAudioPlayer` 缝隙）
- Unlocks: Story 004（调度器需要已确认的 `dsp_start_time`）、Story 009（Pause/Resume 复用起点锚定）
