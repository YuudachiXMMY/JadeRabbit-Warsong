# Story 014: 5/8 拍号支持（Post-MVP · ADVISORY）

> **Epic**: F2 音频/拍点时钟系统
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: [fill before sprint planning]
> **Manifest Version**: 2026-08-12
> **Last Updated**: [set by /dev-story when implementation begins]

## Context

**GDD**: `design/gdd/audio-beat-clock.md`
**Requirement**: `TR-beatclock-014`（复合/不规则拍号 5/8 · Post-MVP）
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*
> **范围提示**: GDD 将 5/8 标为 Post-MVP / ADVISORY。本故事非 MVP 阻塞项，可在核心 4/4·3/4 稳定后再排期。

**ADR Governing Implementation**: ADR-D6: 音频中间件选型
**ADR Decision Summary**: 支持不规则拍号（5/8）时，小节内拍序与半拍网格须按拍号分母（8 分音符为拍）正确划分；BeatClock 拍网格公式泛化至任意 `beats_per_bar` / 拍号分母。

**Engine**: Unity 6.3 LTS (6000.3.19f1) | **Risk**: LOW
**Engine Notes**: 纯 C# 拍网格泛化，无引擎特性风险。5/8 下「拍」的时值 = 8 分音符时长，与 4/4 的 4 分音符拍时值不同，公式须以拍号分母参数化。

**Control Manifest Rules (this layer)**:
- Required: 拍号泛化不破坏 4/4·3/4 现有行为（回归安全）
- Guardrail: 5/8 下 `beat_in_bar` 取值 0..4，半拍网格正确

---

## Acceptance Criteria

*From GDD `design/gdd/audio-beat-clock.md` Acceptance Criteria AC-F2-21/22（ADVISORY），scoped to this story:*

- [ ] 拍号支持 5/8：`beats_per_bar=5`，「拍」时值按 8 分音符（拍号分母=8）计算
- [ ] 5/8 下 `beat_in_bar` 在 0..4 正确回绕，小节边界对齐正确
- [ ] 5/8 下半拍网格（F2-F2/F4）按拍时值正确细分
- [ ] 引入 5/8 不改变 4/4·3/4 的既有拍网格行为（回归通过）
- [ ] 5/8 拍号可经 Story 007 的边界切换路径动态启用（与 4/4 互切）

---

## Implementation Notes

*Derived from ADR-D6 Implementation Guidelines:*

- 拍时值泛化：`beat_interval_s` 当前假设 4 分音符为拍（`60/bpm`）。5/8 下拍为 8 分音符，须以拍号分母参数化——`beat_interval_s = (60/bpm) × (4 / denominator)`（denominator=8 时拍时值减半）。**须与 game-designer 确认 BPM 在 5/8 下的语义**（BPM 指 4 分音符还是 8 分音符每分钟）——OQ 未定则按 GDD 现有定义，缺失则标记待澄清。
- `beats_per_bar` 从拍号分子取（5）；`beat_in_bar` 取模基准随之。
- 回归安全：4/4·3/4 走 denominator=4 路径，数值与现有一致（`4/4=1` 系数）。
- 本故事为 ADVISORY——测试证据为非阻塞门（`production/qa/evidence/` 或 EditMode 均可），不阻塞 epic MVP 完成。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002/004: 4/4 基础公式与调度（本故事泛化其拍号参数）
- Story 007: 拍号切换状态机（本故事只新增 5/8 为合法目标拍号）
- 其他复合拍号（7/8 等）——本故事只交付 5/8，泛化框架为后续留口

---

## QA Test Cases

*嵌自 GDD Acceptance Criteria AC-F2-21/22（Given/When/Then），EditMode 规格（ADVISORY 门）。*

- **AC-F2-21（5/8 小节内拍序）** `[EditMode]` `[ADVISORY]`
  - Given: 拍号 5/8，BPM 按 GDD 定义
  - When: 一小节推进
  - Then: `beat_in_bar` 依次 0,1,2,3,4 后回绕 0；每小节 5 拍
  - Edge cases: 5/8 → 4/4 切换后拍序基准正确回退

- **AC-F2-22（5/8 半拍网格 + 4/4 回归）** `[EditMode]` `[ADVISORY]`
  - Given: 拍号 5/8
  - When: 广播半拍事件
  - Then: 半拍按 5/8 拍时值正确细分（每拍 2 半拍，共 10 半拍/小节）
  - Regression: 同测试类验证 4/4 行为与 Story 004 一致（denominator=4 系数=1，无回归）

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- `Assets/Tests/EditMode/F2/CompoundMeterTests.cs` — 5/8 网格 + 4/4 回归（ADVISORY，非阻塞门）

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 004（调度器）、Story 006（`beat_in_bar`）、Story 007（拍号切换）
- Unlocks: 复合拍号 Boss 阶段音乐设计（Post-MVP）
