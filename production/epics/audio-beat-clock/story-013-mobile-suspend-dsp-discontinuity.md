# Story 013: 移动端挂起/恢复 DSP 不连续检测

> **Epic**: F2 音频/拍点时钟系统
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: M
> **Manifest Version**: 2026-08-12
> **Last Updated**: [set by /dev-story when implementation begins]

## Context

**GDD**: `design/gdd/audio-beat-clock.md`
**Requirement**: `TR-beatclock-017`（移动端挂起 DSP 不连续处理）
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*
> 来源：GDD Edge Cases（移动端 suspend/resume DSP discontinuity），无独立编号 AC——本故事的验收从 Edge Case 描述提炼。

**ADR Governing Implementation**: ADR-D6: 音频中间件选型
**ADR Decision Summary**: 移动端应用挂起（后台）再恢复时，FMOD DSP 时钟可能出现大跳变或冻结不连续——BeatClock 须检测异常 DSP delta 并按暂停语义重锚，避免恢复瞬间补发数千拍。

**Engine**: Unity 6.3 LTS (6000.3.19f1) | **Risk**: MEDIUM
**Engine Notes**: `OnApplicationPause(true/false)` / `OnApplicationFocus` 是移动端挂起信号；DSP 时钟行为跨平台不一致（iOS/Android 差异）。不可假设 DSP 在后台冻结或连续——须以实测 delta 判定。

**Control Manifest Rules (this layer)**:
- Required: 检测异常 DSP delta（远超单帧合理范围）→ 走重锚路径，不补发积压拍
- Forbidden: 恢复时按 `dsp_start + n×interval` 直接补发挂起期全部拍（会瞬间广播数千 Exact）
- Guardrail: 恢复后拍序连续、单帧广播拍数有界

---

## Acceptance Criteria

*From GDD `design/gdd/audio-beat-clock.md` Edge Cases（移动端挂起），scoped to this story:*

- [ ] 监听 `OnApplicationPause`——挂起时按暂停语义处理（复用 Story 009 Pause 或等价冻结）
- [ ] 恢复时检测 DSP delta；若 delta 超过阈值（异常跳变）→ 重锚 `dsp_start_time` 而非补发积压拍
- [ ] 恢复后单帧广播拍数有界（不因挂起 10 分钟而瞬间广播上万拍）
- [ ] 恢复后拍序号连续、快照进度不跳变、无未捕获异常
- [ ] 桌面端（无挂起信号）行为不受本逻辑影响（阈值检测不误触发）

---

## Implementation Notes

*Derived from ADR-D6 Implementation Guidelines + Edge Cases:*

- 挂起路径：`OnApplicationPause(true)` → 记录 `suspend_dsp`（若可读）并进入暂停/冻结；`OnApplicationPause(false)` → 读 `resume_dsp`，计算 delta。
- **异常 delta 判定**：正常单帧 DSP delta ≈ 帧时长；若 delta ≫ 阈值（如 > 1s，说明经历后台），判为挂起恢复 → 重锚 `dsp_start_time += delta`（同 Story 009 重锚公式），拍序从挂起点连续，绝不逐拍补发。
- 与 Story 009 显式 Pause 的区别：显式 Pause 有配对 `Pause()`/`Resume()` 调用；移动端挂起可能无显式调用、只有平台回调 + DSP 跳变——本故事补检测层，兜底重锚。
- 有界广播：即便不判为挂起，调度器补拍循环也应有单帧上限（防御性），超限则重锚跳过积压。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 009: 显式 Pause/Resume 与 BC-6 事件（本故事复用其重锚，不重复实现）
- Story 010: 音频设备丢失降级（区别于挂起——挂起 DSP 仍可能有效）
- 平台特定音频会话管理（iOS AVAudioSession 等，归 ADR-D6 生产实现）

---

## QA Test Cases

*从 GDD Edge Case 提炼（Given/When/Then），Integration 自动化规格。*

- **AC（挂起恢复 DSP 跳变重锚不补发积压拍）** `[EditMode]`
  - Given: BeatClock Playing，BPM=120，`dsp_now=2.0`（第 4 拍）
  - When: 模拟 `OnApplicationPause(true)`；`MockDSPClock` 跳进到 602.0（模拟后台 10 分钟，delta=600s）；`OnApplicationPause(false)`
  - Then: 检测异常 delta → 重锚 `dsp_start_time += 600`；恢复后下一个 `OnBeatExact` 为第 5 拍（**不** 一次广播 ~1200 拍）；单帧广播拍数有界
  - Edge cases: 正常小 delta（0.016s，桌面单帧）不触发重锚、正常广播；delta 恰在阈值边界不误判

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- `Assets/Tests/EditMode/F2/MobileSuspendTests.cs` — 异常 delta 重锚 + 有界广播 + 桌面不误触，必须通过（BLOCKING）

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 009（重锚公式）、Story 004（调度器补拍上限）
- Unlocks: 移动端稳定发布（PC 端非阻塞）
