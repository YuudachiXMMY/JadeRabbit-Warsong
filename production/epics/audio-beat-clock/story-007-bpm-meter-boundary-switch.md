# Story 007: BPM/拍号小节边界切换 + 合并 + Pending 状态机

> **Epic**: F2 音频/拍点时钟系统
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: M
> **Manifest Version**: 2026-08-12
> **Last Updated**: [set by /dev-story when implementation begins]

## Context

**GDD**: `design/gdd/audio-beat-clock.md`
**Requirement**: `TR-beatclock-006`（拍号/BPM 切换状态侧）
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-D6: 音频中间件选型（primary）；secondary: ADR-D1（切换请求来源与边界对齐）
**ADR Decision Summary**: 运行时 BPM/拍号切换须在小节边界对齐生效（Boss 阶段所需），切换请求进入 Pending 状态直至下一小节边界原子应用；DSP 时钟不中断。

**Engine**: Unity 6.3 LTS (6000.3.19f1) | **Risk**: MEDIUM
**Engine Notes**: 切换生效时须重锚 `last_beat_time`（adjusted 坐标）与前瞻窗口缓存（F2-F6 clamp 重算），否则切换后首帧 F2-F7 进度卡满格、前瞻错序。

**Control Manifest Rules (this layer)**:
- Required: 切换在小节边界原子生效（不跨拍撕裂）；生效后重算间隔/前瞻缓存
- Forbidden: 立即（非边界对齐）改 BPM 导致拍网格突变
- Guardrail: 切换生效重锚后拍序连续、进度不跳变

---

## Acceptance Criteria

*From GDD `design/gdd/audio-beat-clock.md` Core Rule 6 + States/Transitions，scoped to this story:*

- [ ] `RequestBpmChange(newBpm)` / `RequestMeterChange(newMeter)` 进入 Pending，不立即改网格
- [ ] Pending 切换在下一小节边界原子生效：同时更新 BPM/拍号、重锚 `last_beat_time`（adjusted）、重算前瞻 clamp、更新 `beats_per_bar`
- [ ] 同一小节内多次切换请求合并——只保留最后一次（后请求覆盖前 Pending）
- [ ] 切换生效后拍序号连续推进（不重置为 0）、快照进度不跳变
- [ ] 非法切换请求（BPM ∉ [80,240]、拍号非支持）被拒并保留当前 Pending/生效值

---

## Implementation Notes

*Derived from ADR-D6 + ADR-D1 Implementation Guidelines:*

- Pending 状态机：`{None, PendingBpm, PendingMeter, PendingBoth}`；请求写入 Pending 槽，调度器在检测到跨小节边界（`beat_in_bar` 归 0 的那拍）时原子应用并清 Pending。
- **重锚三件套**（切换生效同帧全做）：① `beat_interval_s = 60/newBpm` 重算 ② `last_adjusted_beat_dsp_time` 设为生效边界拍的 adjusted 时刻（供 F2-F7）③ 前瞻 `effective_lookahead_s` 按新间隔 clamp 重算（F2-F6）。
- 合并语义：新请求直接覆盖同类 Pending 槽——不排队，只保留最新，避免一小节多次切换叠加。
- 拍号变更改 `beats_per_bar`，影响 Story 006 的 `beat_in_bar` 取模基准与边界判定；须保证生效那一拍即为新小节第 0 拍。
- 事件广播（`OnMeterChangeAccepted`/`OnMeterChanged` 双事件）归 Story 008——本故事只做状态机与网格重锚。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 008: Meter 双事件广播 + BGM 过渡调度
- Story 005: 前瞻公式本体（本故事只触发其重算）
- Story 009: Pause/Resume 重锚（区别于切换重锚）

---

## QA Test Cases

*嵌自 GDD Acceptance Criteria（Given/When/Then），Integration 自动化规格。*

- **AC-F2-13（BPM 切换小节边界生效）** `[EditMode]`
  - Given: BPM=120，拍号 4/4，运行至小节中段（`beat_in_bar=1`）
  - When: `RequestBpmChange(180)`
  - Then: 当前小节保持 120；下一小节边界起 `beat_interval_s=60/180`，`OnBeatExact` 间隔变 0.333；拍序号连续
  - Edge cases: 边界那拍即新间隔，前一拍仍旧间隔（不撕裂）

- **AC-F2-14（拍号切换边界生效 + beats_per_bar 更新）** `[EditMode]`
  - Given: 拍号 4/4
  - When: `RequestMeterChange(3/4)`，跨边界
  - Then: 新小节 `beats_per_bar=3`，`beat_in_bar` 在 0..2 回绕

- **AC-F2-15（同小节多次请求合并）** `[EditMode]`
  - Given: 小节内 `RequestBpmChange(180)` 后同小节再 `RequestBpmChange(200)`
  - When: 跨边界
  - Then: 生效值 = 200（仅最后一次），180 被覆盖丢弃

- **AC-F2-23（切换后进度不跳变）** `[EditMode]`
  - Given: 切换 BPM 生效边界
  - When: 生效后首帧 `GetSnapshot()`
  - Then: `beat_progress` 从新边界拍平滑起算（≈0），不卡 1.0（证明 `last_beat_time` 已重锚）
  - Edge cases: 非法 BPM=300 请求被拒，保留生效值

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- `Assets/Tests/EditMode/F2/MeterBpmSwitchTests.cs` — Pending/合并/重锚，必须通过（BLOCKING）

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 004（调度器）、Story 006（`beat_in_bar` 边界判定）
- Unlocks: Story 008（切换事件广播依赖本状态机）
