# Story 005: Lookahead 前瞻广播 + 高 BPM clamp（F2-F6）

> **Epic**: F2 音频/拍点时钟系统
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: [fill before sprint planning]
> **Manifest Version**: 2026-08-12
> **Last Updated**: [set by /dev-story when implementation begins]

## Context

**GDD**: `design/gdd/audio-beat-clock.md`
**Requirement**: `TR-beatclock-005`（前瞻调度广播）
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-D6: 音频中间件选型
**ADR Decision Summary**: 主线程调度轮询 DSP 时钟；前瞻事件为「拍点到达前 N 秒的预告广播」，与 Exact（精确到达）分离，供下游预备动画/音效调度。

**Engine**: Unity 6.3 LTS (6000.3.19f1) | **Risk**: MEDIUM
**Engine Notes**: 前瞻窗口在高 BPM 下须 clamp，避免窗口 > 拍间隔导致同一拍前瞻与上一拍 Exact 重叠错序。

**Control Manifest Rules (this layer)**:
- Required: 前瞻时刻锚定式计算（`nth_beat - lookahead_seconds`）
- Forbidden: `OnAudioFilterRead` / `ConcurrentQueue` 派发（同 Story 004 线程铁律）
- Guardrail: 前瞻窗口 clamp 后 ≤ `beat_interval_s`；事件零 GC

---

## Acceptance Criteria

*From GDD `design/gdd/audio-beat-clock.md` Core Rule 4 + Formula F2-F6，scoped to this story:*

- [ ] 每拍在其 DSP 到达时刻**之前** `lookahead_seconds` 广播 `OnBeatLookahead`（`readonly struct` payload，含目标拍序号 + 目标 raw/adjusted 时间戳）
- [ ] 半拍前瞻 `OnHalfBeatLookahead` 同理
- [ ] `effective_lookahead_s = min(lookahead_seconds, beat_interval_s)`（F2-F6）——高 BPM 下窗口被 clamp 至不超过一个拍间隔
- [ ] Lookahead 与 Exact 事件对同一拍保持顺序：Lookahead 先于 Exact 广播

---

## Implementation Notes

*Derived from ADR-D6 Implementation Guidelines:*

- 前瞻触发时刻 = `nth_beat_dsp_time(n) − effective_lookahead_s`（raw 坐标）。调度器每 `Update()` 比较 `raw_now` 是否越过前瞻时刻，越过则广播并推进前瞻游标。
- **clamp 铁律（F2-F6）**：BPM=240 时 `beat_interval_s=0.25`，若 `lookahead_seconds=0.3` 会使前瞻窗口跨越上一拍，须 clamp 至 0.25。clamp 在每次 BPM 变更后重算一次缓存。
- 前瞻与 Exact 用各自独立游标（`next_lookahead_n` / `next_exact_n`），保证顺序不因补拍错乱。
- payload 为 `readonly struct`，直接 `Invoke`。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 004: Exact 广播与调度器主循环（本故事挂载其上）
- Story 007: BPM 切换后前瞻窗口重算的触发（本故事保证 clamp 公式正确，触发归 007）

---

## QA Test Cases

*嵌自 GDD Acceptance Criteria（Given/When/Then），EditMode 自动化规格。*

- **AC-F2-06a（拍前瞻提前广播）** `[EditMode]`
  - Given: `dsp_start=0`，BPM=120（interval=0.5），`lookahead_seconds=0.1`
  - When: `dsp_now` 越过 0.4（= 第 1 拍 0.5 − 0.1）
  - Then: `OnBeatLookahead` 广播，payload 目标拍序号=1、目标时刻=0.5

- **AC-F2-06b（前瞻先于 Exact）** `[EditMode]`
  - Given: 同上
  - When: `dsp_now` 从 0.4 推进越过 0.5
  - Then: 该拍的 `OnBeatLookahead`（在 0.4）严格早于 `OnBeatExact`（在 0.5）

- **AC-F2-11（高 BPM clamp）** `[EditMode]`
  - Given: BPM=240（interval=0.25），`lookahead_seconds=0.30`
  - When: 计算 `effective_lookahead_s`
  - Then: 结果 = 0.25（被 clamp 至 `beat_interval_s`），前瞻不跨越上一拍
  - Edge cases: `lookahead_seconds < beat_interval_s` 时不 clamp，原值透传

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- `Assets/Tests/EditMode/F2/BeatLookaheadTests.cs` — 前瞻时序 + clamp，必须通过（BLOCKING）

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 004（调度器主循环）
- Unlocks: 下游预备动画/音效调度消费者（C1/FE4）
