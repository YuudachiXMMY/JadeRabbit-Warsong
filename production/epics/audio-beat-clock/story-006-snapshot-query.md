# Story 006: BeatStateSnapshot 同步查询 + 小节内拍序（F2-F8）

> **Epic**: F2 音频/拍点时钟系统
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: S
> **Manifest Version**: 2026-08-12
> **Last Updated**: [set by /dev-story when implementation begins]

## Context

**GDD**: `design/gdd/audio-beat-clock.md`
**Requirement**: `TR-beatclock-004`（快照查询侧）, `TR-beatclock-015`（小节内拍序）
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-D8: DI / 测试缝隙
**ADR Decision Summary**: `IBeatClock.GetSnapshot()` 为只读同步拉取面，返回 `readonly struct BeatStateSnapshot`，供不便订阅事件的消费者（HUD 每帧拉取）；与事件推送互补。

**Engine**: Unity 6.3 LTS (6000.3.19f1) | **Risk**: LOW
**Engine Notes**: `GetSnapshot()` 每次返回值类型（栈上），零堆分配；不缓存可变引用。

**Control Manifest Rules (this layer)**:
- Required: 快照为 `readonly struct`，`GetSnapshot()` 返回值拷贝（零 GC）
- Guardrail: 未起播/未确认起点时返回明确定义的零状态（不抛异常）

---

## Acceptance Criteria

*From GDD `design/gdd/audio-beat-clock.md` BeatStateSnapshot 结构 + Formula F2-F8，scoped to this story:*

- [ ] `GetSnapshot()` 返回 `BeatStateSnapshot`（9 字段：当前拍序号 / 小节内拍序 / 拍进度 / 下一拍 raw+adjusted 时间戳 / BPM / 拍号 / 是否已确认起点 / 是否暂停）
- [ ] 小节内拍序 `beat_in_bar = (global_beat_index mod beats_per_bar)`（F2-F8），4/4 下取值 0..3
- [ ] 拍进度字段复用 F2-F7（adjusted 坐标，clamp [0,1]）
- [ ] 未起播 / `IsStartConfirmed==false` 时返回零状态快照（拍序=0、进度=0、`IsStartConfirmed=false`），不抛异常

---

## Implementation Notes

*Derived from ADR-D8 Implementation Guidelines:*

- 快照字段由调度器当前状态 + 公式（F2-F3/F5/F7/F8）实时组装；下一拍时间戳 = `nth_beat_dsp_time(next_n)` 的 raw 与 adjusted 双坐标（Core Rule 13 命名后缀）。
- `beat_in_bar` 用全局拍序号对 `beats_per_bar` 取模；拍号变更后 `beats_per_bar` 更新，取模基准随之改变（拍号切换边界对齐归 Story 007）。
- 零状态定义置于结构默认值；调用方靠 `IsStartConfirmed` 字段判定有效性，不靠 null。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 004: 拍序号推进逻辑（本故事读取其状态）
- Story 007: 拍号切换对 `beats_per_bar` 的更新
- Story 009: 暂停状态字段的写入（本故事只读该标志）

---

## QA Test Cases

*嵌自 GDD Acceptance Criteria（Given/When/Then），EditMode 自动化规格。*

- **AC-F2-12（快照字段正确性 + 小节内拍序）** `[EditMode]`
  - Given: `dsp_start=0`，BPM=120，拍号 4/4，`dsp_now=1.25`（第 2.5 拍位置）
  - When: `GetSnapshot()`
  - Then: 当前拍序号=2、`beat_in_bar=2`、进度≈0.5、下一拍 raw 时间戳=1.5、BPM=120、拍号=4/4、`IsStartConfirmed=true`
  - Edge cases: 第 4 拍（global=4）`beat_in_bar` 回绕至 0；未起播时全零状态、`IsStartConfirmed=false` 且不抛异常

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- `Assets/Tests/EditMode/F2/BeatSnapshotTests.cs` — 快照字段 + 取模 + 零状态，必须通过（BLOCKING）

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 002（F2-F7/F8 公式）、Story 004（拍序状态）
- Unlocks: HUD/消费者同步拉取（FE3/PL2）
