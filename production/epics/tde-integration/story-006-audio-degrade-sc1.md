# Story 006: 音频降级 SC-1 handoff

> **Epic**: F6 TopDownEngine & Animancer 集成层
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: 2026-08-12
> **Last Updated**: [set by /dev-story when implementation begins]

## Context

**GDD**: `design/gdd/tde-integration.md`
**Requirement**: `TR-beatclock-009`（音频运行期降级 SC-1 交接）
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-D10: Bootstrap 顺序（primary）；ADR-D1: TDE 集成
**ADR Decision Summary**: 运行期 FMOD 不可用时（设备丢失/bank 卸载），`TDELifecycleAdapter` 经 `OnAudioUnavailable` 交接至无音频降级模式（节拍时钟切内部时基维持判定），恢复时 `OnAudioRestored` 重新对齐音频相位。

**Engine**: Unity 6.3 LTS (6000.3.19f1) | **Risk**: HIGH
**Engine Notes**: FMOD 设备丢失/恢复回调为第三方插件 API——须对照 `docs/engine-reference/unity/plugins/fmod.md`，严禁从训练数据推断成员名。

**Control Manifest Rules (Foundation)**:
- Required: 音频降级经 `OnAudioUnavailable`/`OnAudioRestored` 交接（SC-1 契约）
- Required: 降级期 BeatClock 判定不中断（切内部时基维持节奏窗口）
- Forbidden: 音频丢失时禁止直接 `Stop()` BeatClock（会中断 run，降级≠结束）

---

## Acceptance Criteria

*From GDD `design/gdd/tde-integration.md`, scoped to this story:*

- [ ] AC-F6-24：运行期 FMOD 不可用 → `OnAudioUnavailable` 触发，BeatClock 切内部时基继续判定（不 Stop）；FMOD 恢复 → `OnAudioRestored` 触发，音频相位重对齐
- [ ] AC-F6-30：重复 `OnAudioUnavailable` 不叠加降级（降级标志单一，内部时基切换每丢失周期至多 1 次）；非降级态收 `OnAudioRestored` 无副作用；全程 `BeatClock.Stop()` 调用累计==0
- [ ] AC-F6-31：降级期 `BeatClock` 保持 `Playing`、节拍序号持续单调递增；`OnAudioRestored` 重对齐后节拍相位与 DSP 相位偏差 ≤ 判定窗口容差，多周期测量无残留永久偏移

---

## Implementation Notes

*Derived from ADR-D10 SC-1 + GDD EC (audio device loss):*

- `TDELifecycleAdapter` 订阅 FMOD 设备状态回调（成员名先查 `fmod.md`），发布 `OnAudioUnavailable`/`OnAudioRestored`。
- 降级：BeatClock 从音频时基切至内部时基（`Time`/`AudioSettings.dspTime` 兜底——具体时基源由 F2 BeatClock 提供接口），继续推进节拍判定，不 `Stop()`。
- 恢复：重新对齐音频相位（避免降级期漂移累积成永久偏移）。
- 交接为幂等：重复 Unavailable 不叠加，Restored 未降级态无副作用。

---

## Out of Scope

- Story 003：启动期 FMOD 就绪门禁（本 story 只管运行期降级）
- F2 BeatClock 内部时基实现（属 F2 epic，本 story 消费其切换接口）
- BGM crossfade（TR-tde-012，Post-MVP，不在本 epic）

---

## QA Test Cases

*Embedded from GDD AC-F6-24（lean 模式）。*

**AC-1（AC-F6-24，Logic — PlayMode）**
  - Given：BeatClock=Playing（音频时基），run 进行中
  - When：FMOD mock 报告设备丢失
  - Then：`OnAudioUnavailable` 触发；BeatClock 未 `Stop()`（Stop 调用 == 0），切内部时基仍 Playing，节拍判定继续
  - Edge cases：丢失→恢复→再丢失序列，每次交接幂等；恢复后 `OnAudioRestored` 触发且相位重对齐（无累积漂移）

**AC-2（AC-F6-30，Logic — PlayMode）**
  - Given：BeatClock=Playing，run 进行中
  - When：(1) 连续两次 `OnAudioUnavailable`；(2) 未降级态直接 `OnAudioRestored`
  - Then：(1) 降级切换只 1 次、标志单一；(2) 无状态变更；全程 `Stop()`==0
  - Edge cases：Unavailable→Restored→Unavailable 每次独立正确交接，无残留污染

**AC-3（AC-F6-31，Logic — PlayMode）**
  - Given：BeatClock=Playing，节拍序号归零基准
  - When：(1) `OnAudioUnavailable` 后采样序号 N 周期；(2) `OnAudioRestored` 后采样相位偏差 3+ 周期
  - Then：(1) 序号单调递增无中断；(2) 偏差 ≤ 容差且不随周期增长
  - Edge cases：降级跨多个节拍周期后恢复偏差仍收敛

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- `Assets/Tests/PlayMode/F6/AudioDegradeSC1_test.cs`（AC-24，FMOD 设备丢失/恢复 mock）— 必须存在且通过
- `Assets/Tests/PlayMode/F6/AudioDegradeSC1_test.cs`（新增覆盖 AC-F6-30 幂等 + AC-F6-31 连续性/相位收敛）— 必须存在且通过

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 002（boot 骨架）、Story 003（FMOD 交互路径）；跨 epic 需 F2 BeatClock 时基切换接口
- Unlocks: None
