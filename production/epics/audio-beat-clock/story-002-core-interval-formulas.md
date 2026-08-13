# Story 002: 核心间隔与调整时间戳公式（F2-F1~F5, F7）

> **Epic**: F2 音频/拍点时钟系统
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: 2026-08-12
> **Last Updated**: [set by /dev-story when implementation begins]

## Context

**GDD**: `design/gdd/audio-beat-clock.md`
**Requirement**: `TR-beatclock-002`（锚定公式、无迭代累加 · 部分），`TR-beatclock-007`（延迟偏移数学 · 部分）
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-D6: 音频中间件选型
**ADR Decision Summary**: DSP 时钟真值取自 FMOD master ChannelGroup（经 `IDSPClock.GetDspTime()`），所有拍点计算的 C# 数学层与中间件无关；禁用 `AudioSettings.outputSampleRate`。

**Engine**: Unity 6.3 LTS (6000.3.19f1) | **Risk**: MEDIUM
**Engine Notes**: 本故事为纯 C# 数学，EditMode 用 `MockDSPClock` 确定性验证，不触真实 FMOD。DSP 时间戳类型统一 `double`（避免 float 精度丢失）；毫秒仅用于配置输入，内部统一换算为秒。

**Control Manifest Rules (this layer)**:
- Required: DSP 时钟换算使用 `getSoftwareFormat()` 采样率，**禁** `AudioSettings.outputSampleRate`（本故事经 `IDSPClock` 抽象，不直接触及）
- Forbidden: 迭代累加式计算拍点时刻（`accumulator += beat_interval_s`）—— 1000 拍后累积约 0.5ms 误差，违反 0.1ms 承诺
- Guardrail: F2 时间戳精度 < 0.1ms（相对理论值）

---

## Acceptance Criteria

*From GDD `design/gdd/audio-beat-clock.md` Formulas F2-F1~F5、F7，scoped to this story:*

- [ ] `beat_interval_s = 60.0 / bpm`（F2-F1），输出范围 [0.25, 0.75]
- [ ] `half_beat_interval_s = beat_interval_s / 2.0 = 30.0 / bpm`（F2-F2），范围 [0.125, 0.375]
- [ ] `nth_beat_dsp_time = dsp_start_time + (n × beat_interval_s)`（F2-F3），**锚定形式，禁迭代累加**
- [ ] `nth_half_beat_dsp_time = dsp_start_time + (m × half_beat_interval_s)`（F2-F4）
- [ ] `adjusted_beat_time = raw_beat_dsp_time + (latency_offset_ms / 1000.0)`（F2-F5），正值 = 时间戳后移
- [ ] `beat_progress = clamp((adjusted_dsp_now − last_beat_time) / beat_interval_s, 0.0, 1.0)`（F2-F7），`adjusted_dsp_now = IDSPClock.GetDspTime() + latency_offset_ms/1000.0`；`last_beat_time` 为 adjusted 坐标；负值被 clamp 至 0.0

---

## Implementation Notes

*Derived from ADR-D6 Implementation Guidelines + GDD Formulas 节:*

- **坐标系铁律（Core Rule 13）**：raw（未含偏移，音频硬件调度用）与 adjusted（已含偏移，玩家判定/呈现用）必须命名后缀区分，**禁裸 `dspTime` 字段名**。F2-F7 的 `adjusted_dsp_now` 与 `last_beat_time` 必须同为 adjusted 坐标——若以原始 DSP 直接相减引入最大 ±200ms 误差。
- **锚定 vs 累加**：调度器一律 `dsp_start_time + n × beat_interval_s`，禁 `accumulator +=` 循环。这是 TR-beatclock-002 的核心（500 拍漂移验证在 Story 004）。
- BeatClock 须**分别维护** `last_raw_beat_dsp_time`（供暂停恢复公式 Story 009）与 `last_adjusted_beat_dsp_time`（供 F2-F7 HUD），两者独立存储。
- `latency_offset_ms` 本故事作为**已持有值**参与运算；其注入拓扑（两阶段写者、禁自 load）见 Story 011，本故事只消费该字段做数学。
- F2-F7 初始化：`last_beat_time` 在 `Play()` 时初始化为 `dsp_start_time`；首拍前 `dsp_now < dsp_start_time` 则结果为负被 clamp 至 0.0（HUD 保持 0 进度，不卡满格）。
- BPM 切换时 `last_beat_time` 须同步更新为新段生效边界的 adjusted 时间戳（否则切换后首帧 clamp 卡 1.0 数帧）——该更新逻辑在 Story 007 触发，本故事保证公式对 `last_beat_time` 变更正确响应。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 004: 调度器 Update 循环、Exact 事件广播、500 拍漂移测试（本故事只提供公式函数）
- Story 005: F2-F6 Lookahead 触发时间公式
- Story 007: BPM 切换时 `last_beat_time` 的重锚触发
- Story 011: `latency_offset_ms` 的注入拓扑

---

## QA Test Cases

*嵌自 GDD Acceptance Criteria（已 Given/When/Then 格式），EditMode 自动化规格。*

- **AC-F2-01（节拍间隔精度）** `[EditMode]`
  - Given: BPM = 120
  - When: 调用 `BeatClock.GetBeatIntervalSeconds()`
  - Then: 返回值 ∈ [0.49999, 0.50001]（误差 < 0.01ms）

- **AC-F2-02（半拍间隔精度）** `[EditMode]`
  - Given: BPM = 180
  - When: 调用 `BeatClock.GetHalfBeatIntervalSeconds()`
  - Then: 返回值 ∈ [0.16666, 0.16668]（误差 < 0.01ms）

- **AC-F2-03（第 N 拍 DSP 时间戳精度）** `[EditMode]`
  - Given: `dsp_start = 10.000`，BPM = 120，n = 3
  - When: 计算 `nth_beat_dsp_time`
  - Then: 结果为 11.500（误差 < 0.001ms）
  - Edge cases: n=0 → dsp_start；大 n（500）漂移验证归 Story 004

- **AC-F2-04（延迟偏移方向 · 正值）** `[EditMode]`
  - Given: `raw_beat_dsp_time = 11.500 s`，`latency_offset_ms = 80`
  - When: 计算 `adjusted_beat_time`
  - Then: `adjusted = 11.580 s`（`raw + offset/1000`）

- **AC-F2-05（延迟偏移方向 · 负值）** `[EditMode]`
  - Given: `raw_beat_dsp_time = 11.500 s`，`latency_offset_ms = −80`
  - When: 计算 `adjusted_beat_time`
  - Then: `adjusted = 11.420 s`

- **AC-F2-20（节拍进度公式范围）** `[EditMode]`
  - Given: BPM = 120，`last_beat_time = 11.420`
  - When: `dsp_now`（经 MockDSPClock）从 11.420 递进至 11.920
  - Then: `beat_progress` 从 0.0 线性增至 1.0；任何时刻 ∈ [0.0, 1.0]
  - Edge cases: `dsp_now < last_beat_time`（暂停恢复后）→ clamp 至 0.0，不出负值

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- `Assets/Tests/EditMode/F2/BeatClockFormulasTests.cs` — 必须存在并通过（BLOCKING）

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001（DI 缝隙 + `IDSPClock` 注入，供 F2-F7 用 `MockDSPClock`）
- Unlocks: Story 004（调度器复用这些公式）、Story 006（快照 `nextBeatDspTime` 用 F2-F3/F5）
