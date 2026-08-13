# Story 010: 音频不可用降级 + 恢复（SC-1）

> **Epic**: F2 音频/拍点时钟系统
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: M
> **Manifest Version**: 2026-08-12
> **Last Updated**: [set by /dev-story when implementation begins]

## Context

**GDD**: `design/gdd/audio-beat-clock.md`
**Requirement**: `TR-beatclock-009`（音频不可用降级 · 部分）
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-D1: TDE 集成（primary）；secondary: ADR-D6（FMOD 初始化失败/设备丢失语义）
**ADR Decision Summary**: 音频设备不可用（无输出设备、FMOD 初始化失败、运行中设备拔出）时，BeatClock 进入降级状态而非崩溃；游戏可无声继续（拍网格可选回退或冻结），设备恢复后重新起播（场景 SC-1）。

**Engine**: Unity 6.3 LTS (6000.3.19f1) | **Risk**: MEDIUM
**Engine Notes**: FMOD 无输出设备时 `PlayScheduled` 会失败——须捕获并转降级，不让异常冒泡崩游戏。设备恢复检测经 FMOD 系统回调或轮询（`IAudioPlayer.IsPlaying` / 起播返回码）。

**Control Manifest Rules (this layer)**:
- Required: 降级路径不抛未捕获异常；状态明确（Degraded），可查询
- Forbidden: 音频失败时静默继续假装 Playing（掩盖状态导致下游误判）
- Guardrail: 恢复重起播复用 Story 003 的起点锚定协议（重锚，不续接旧起点）

---

## Acceptance Criteria

*From GDD `design/gdd/audio-beat-clock.md` Edge Cases（音频不可用）+ 场景 SC-1，scoped to this story:*

- [ ] `Play()` 时 `IAudioPlayer.PlayScheduled` 失败（无设备/初始化失败）→ 进入 Degraded 状态，不抛异常，记录明确诊断日志
- [ ] Degraded 状态可经快照/属性查询（下游据此决定是否隐藏节奏 UI）
- [ ] 运行中设备丢失（拔出）→ 从 Playing 转 Degraded，停止广播 Exact/Lookahead（或按 GDD 定义回退），BC-6/事件契约不撕裂
- [ ] 设备恢复后经 SC-1 路径重新 `Play()`（起点重锚），恢复 Playing 与广播
- [ ] 降级/恢复全程无未捕获异常、无崩溃

---

## Implementation Notes

*Derived from ADR-D1 + ADR-D6 Implementation Guidelines:*

- `PlayScheduled` 返回码/异常在 BeatClock 层捕获 → 置 Degraded；`IAudioPlayer` 生产实现（ADR-D6）负责把 FMOD 错误码翻成布尔/异常，BeatClock 只消费。
- Degraded 与 Paused 区分：Paused 是主动、可 Resume 续接；Degraded 是被动故障、恢复走完整重起播（SC-1，起点重锚而非平移）。
- 恢复检测：可轮询 `IAudioPlayer` 设备就绪标志；就绪后调用生命周期重 `Play()`（唯一控制者仍为 F6，本故事提供 Degraded→Play 的可恢复钩子）。
- 下游据 Degraded 标志隐藏节奏 HUD——本故事只暴露状态，不改 UI（UI 归 FE3/PL2）。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 003: 正常 `Play()`/起点锚定（本故事复用其重起播）
- Story 009: 主动 Pause/Resume（区别于故障降级）
- Story 013: 移动端挂起（区别于音频设备丢失）
- ADR-D6: `IAudioPlayer` 生产实现的 FMOD 错误码翻译

---

## QA Test Cases

*嵌自 GDD Acceptance Criteria（Given/When/Then），Integration 自动化规格。*

- **AC-F2-16b（音频不可用降级不崩溃 + SC-1 恢复）** `[EditMode]`
  - Given: `MockAudioPlayer` 配置为 `PlayScheduled` 失败（模拟无设备）
  - When: `Play()`
  - Then: 状态 = Degraded、无未捕获异常、诊断日志记录；快照可查 Degraded
  - When（恢复）: `MockAudioPlayer` 切为可用，触发 SC-1 重起播
  - Then: 状态回 Playing、起点重锚（新 `dsp_start_time`）、广播恢复
  - Edge cases: 运行中（Playing）设备丢失 → 转 Degraded 且不撕裂事件契约；连续两次失败不叠加异常

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- `Assets/Tests/EditMode/F2/AudioDegradationTests.cs` — 降级 + SC-1 恢复，必须通过（BLOCKING）

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 003（起点锚定/重起播）、Story 001（`IAudioPlayer` 缝隙）
- Unlocks: FE3/PL2 节奏 UI 的降级隐藏逻辑
