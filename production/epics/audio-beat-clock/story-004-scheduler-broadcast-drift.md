# Story 004: 拍点调度器 + Exact/全网格广播 + 500 拍漂移

> **Epic**: F2 音频/拍点时钟系统
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: [fill before sprint planning]
> **Manifest Version**: 2026-08-12
> **Last Updated**: [set by /dev-story when implementation begins]

## Context

**GDD**: `design/gdd/audio-beat-clock.md`
**Requirement**: `TR-beatclock-002`（锚定精度）, `TR-beatclock-003`（全网格广播）, `TR-beatclock-016`（零 GC 事件卫生 · 部分）, `TR-beatclock-010`（FMOD DSP 采集路径）
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-D6: 音频中间件选型
**ADR Decision Summary**: FMOD Studio 2.x；DSP 真值取自 master ChannelGroup。**线程模型铁律**：禁 `OnAudioFilterRead` 与 `ConcurrentQueue`——所有拍点比较与广播在主线程 `Update()` 完成（见下方 Addendum 说明）。

**Engine**: Unity 6.3 LTS (6000.3.19f1) | **Risk**: HIGH
**Engine Notes**: 调度在主线程 `Update()` 轮询 `GetDspTime()`，比较锚定拍点时刻并广播 Exact 事件——不得开音频线程回调派发。DSP 时钟经 `getSoftwareFormat()` 采样率换算，禁 `AudioSettings.outputSampleRate`。

**Control Manifest Rules (this layer)**:
- Required: 拍点时刻一律锚定式 `dsp_start + n × beat_interval_s`（禁迭代累加）
- Forbidden: `OnAudioFilterRead` / `ConcurrentQueue` 做跨线程派发 —— ADR-D6 Addendum 明确禁止（**注意 TR-beatclock-010 旧文本提及 ConcurrentQueue 已过时，以 ADR-D6 Addendum 为准**）
- Forbidden: `Update()` 时间戳做节拍判定（须用 DSP 时钟，非 `Time.time`）
- Guardrail: 500 拍后累积漂移 < 0.1ms；事件 `Invoke` 零堆分配（`readonly struct` payload）

---

## Acceptance Criteria

*From GDD `design/gdd/audio-beat-clock.md` Core Rule 3/5 + Formulas，scoped to this story:*

- [ ] 每拍到达时广播 `OnBeatExact`（`readonly struct` payload），量化模式无关（全网格，不因判定模式裁剪）
- [ ] 每半拍到达时广播 `OnHalfBeatExact`
- [ ] Exact 事件在拍点 DSP 时刻**之后最近的一帧**广播（精确到达语义），payload 含拍序号 + raw/adjusted 时间戳
- [ ] 调度用锚定公式定位下一拍时刻，不迭代累加；500 拍后误差 < 0.1ms
- [ ] 主线程 `Update()` 轮询，绝不在音频线程/`OnAudioFilterRead` 派发事件

---

## Implementation Notes

*Derived from ADR-D6 Implementation Guidelines + Addendum:*

- 调度器每 `Update()`：读 `raw_now = GetDspTime()`，若 `raw_now >= nth_beat_dsp_time(next_n)` 则广播该拍并推进 `next_n`（可能一帧补多拍——低帧率下须循环补发不漏拍）。
- **锚定定位**：`next_beat_time = dsp_start + next_n × beat_interval_s`，从不 `+=`。500 拍漂移测试通过 `MockDSPClock` 跳进到 `dsp_start + 500×interval` 验证第 500 拍时刻误差 < 0.1ms。
- **线程铁律**：TR-beatclock-010 旧注提及 ConcurrentQueue，已被 ADR-D6 Addendum 推翻——本项目一切派发在主线程完成，无跨线程队列。若 FMOD 起播回调在非主线程触发，只置原子标志，主线程 `Update()` 消费（`ConfirmDSPStart` 归 Story 003 的确认门控）。
- 事件 payload 为 `readonly struct`，`OnBeatExact?.Invoke(data)` 直接调用，禁 `GetInvocationList()` 遍历（零 GC）。
- 补多拍时按序号升序逐个广播，保证订阅者收到连续拍序（不跳号）。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: 间隔/时间戳公式本体（本故事复用）
- Story 005: Lookahead（前瞻）广播——本故事只做 Exact（精确到达）
- Story 006: `GetSnapshot()` 同步查询
- Story 012: 订阅者生命周期合约与退订卫生

---

## QA Test Cases

*嵌自 GDD Acceptance Criteria（Given/When/Then），EditMode 自动化规格。*

- **AC-F2-07a（每拍 Exact 广播）** `[EditMode]`
  - Given: `dsp_start=0`，BPM=120，`MockDSPClock` 从 0 推进至 2.0s
  - When: 逐帧推进 `dsp_now` 并调用调度器 `Tick()`
  - Then: `OnBeatExact` 恰广播 4 次（0.0/0.5/1.0/1.5），每次 payload 拍序号递增
  - Edge cases: 一帧跨 2 拍（大 Δt）→ 两拍均补发不漏

- **AC-F2-07b（每半拍 Exact 广播）** `[EditMode]`
  - Given: BPM=120，推进至 1.0s
  - When: `Tick()` 逐帧
  - Then: `OnHalfBeatExact` 广播 4 次（0.0/0.25/0.5/0.75）

- **AC-F2-07c（Exact 精确到达时刻）** `[EditMode]`
  - Given: `dsp_start=10.0`，BPM=120
  - When: `dsp_now` 越过 10.5（第 1 拍）
  - Then: `OnBeatExact` 在越过后最近一次 `Tick` 广播，payload `raw_beat_dsp_time == 10.5`

- **AC-F2-19a（全网格量化模式无关）** `[EditMode]`
  - Given: 任意量化模式设置
  - When: 一小节推进
  - Then: 广播的拍/半拍事件数量与时刻不随量化模式改变（广播是全网格，量化在消费侧）

- **AC-F2-19b（500 拍零漂移）** `[EditMode]`
  - Given: `dsp_start=0`，BPM=120
  - When: `MockDSPClock` 跳进至第 500 拍理论时刻
  - Then: 第 500 拍广播时刻与 `dsp_start + 500×0.5` 误差 < 0.1ms（证明锚定非累加）

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- `Assets/Tests/EditMode/F2/BeatSchedulerTests.cs` — 500 拍漂移 + 补拍 + 全网格，必须通过（BLOCKING）

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 002（公式）、Story 003（已确认 `dsp_start_time`）
- Unlocks: Story 005（Lookahead 复用调度器）、Story 006（快照读取下一拍）、Story 007（BPM 切换重锚调度器）
