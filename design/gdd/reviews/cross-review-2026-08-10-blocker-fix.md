# Cross-Review 2026-08-10 — Blocker Fix Log

> 对应报告：`design/gdd/gdd-cross-review-2026-08-10.md`（Verdict: FAIL）
> Owner 决策：**BD-1 = Off perk 封顶**；**BD-2 = 适应窗口宽容期**（见 `reviews/cross-review-2026-08-10-owner-decisions.md`）
> 修订日期：2026-08-10 ｜ 范围：7 个 GDD + 1 索引

## 阻塞集处置（FAIL 判词要求清零项）

| 阻塞项 | 处置 | 落地文件 |
|--------|------|---------|
| BC-1 | Perfect 倍率 1.5→1.0（前序 PR #15 已修） | combo-system.md |
| BC-2 | C1 定义 `IRhythmModifierProvider` + C1↔C3 接线（PR #15 已修） | rhythm-core.md / perk-system.md |
| BC-3 | `EnemyInfoSO` 补 `timeoutBeats`（per-enemy）；新增 **F4-S8 `CombatDirectorConfigSO`** 承载 `capacityTable`/威胁权重/`starvation*`/`subdivisionMode`/`bpmRef`（导演级全局，原误挂 EnemyInfoSO）；C4 依赖表/Tuning Knobs 宿主改指 F4-S8/F4-S5 | so-data-layer.md / combat-director.md |
| BC-4 | 非法拍号 6/8→5/8、3/8→3/4（PR #15 已修） | combat-director.md / perk-system.md |
| BC-5 | `ComboEdge.requiredInput` 枚举由 `AttackInputType {Normal,Heavy,Special}` 统一为 F3 权威 `AttackType {LightAttack, HeavyAttack, ChargedAttack}`；C2 直接比较无需映射 | so-data-layer.md / combo-system.md |
| BC-6 | F2 权威定义 `OnBeatClockPaused`/`OnBeatClockResumed` + `IAudioPlayer.Pause()/Resume()` + `BeatClockPauseEventData` + AC-F2-28；C1 resume 重锚+清 `_pendingInput`（Rule 8/AC-C1-10b）；C3 事件名对齐 | audio-beat-clock.md / rhythm-core.md / perk-system.md |
| BD-1 | **Off perk 封顶**：`QuantizationMode==Off` 时聚合 `perk_multiplier` 独立 clamp ≤ 1.0（节奏模式独占护栏）；OQ-C3-3/Q3 循环阻塞解除；AC-C1-26 / AC-C3-15(b) 由阻塞桩改为可判定 | rhythm-core.md / perk-system.md |
| BD-2 | **适应窗口**：`OnMeterChanged` 生效后 `AdaptationWindowBeats`=2 拍内，C1 判定窗 + C2 取消窗毫秒宽度取 `max(变更前, 变更后)`（较宽者，防骤缩）；C4 `EffectiveSlotCapacity=round(capacityTable×bpmRef/bpm)` 按每秒令牌速率归一化；三文档 Formulas 互引 | rhythm-core.md(C1-F5b) / combo-system.md(F7) / combat-director.md(F-1b) |
| SC-1 | F6 `TDELifecycleAdapter` 订阅 F2 `OnAudioUnavailable`，同帧 `GameManager.Pause()` + `InputEventBus.DisableAllInput()` 承接降级；对称恢复；AC-F6-24 | tde-integration.md / audio-beat-clock.md |
| SC-2 | C1 订阅 F6 `PlayerDeath`/`RespawnComplete`，复活清空过期 `_pendingInput` 与评级状态（E10/AC-C1-22e），防复活幽灵评级 | rhythm-core.md |
| B3 | F2 补 `ScheduleBGMTransition(nextBgm, boundaryDspTimeRaw)` 方法契约（F6 调用方，消解 W-2c-01） | audio-beat-clock.md |

## 跨文件一致性核验（本轮已确认）
- `AttackType` 枚举：F3 权威 → F4/C1/C2 全链一致，无 `AttackInputType` 活引用（仅废弃注记）。
- `MeterChangeEventData.predictedEffectiveDspTimeRaw`：C1-F5b 适应窗口计时基准对齐 F2 既有字段（不自造 `boundaryBeatDspTime`）。
- SC-2：F6 已广播 `TopDownEngineEvent(PlayerDeath/RespawnComplete)`，C1 订阅方对齐。
- `AdaptationWindowBeats`：C1/C2 共享同一 `RhythmConfigSO` Tuning Knob 语义（须先于 FE4 Boss GDD 由 owner 联合锁定）。

## 残余非阻塞项（留待重审 INFO/WARNING，不阻断）
- 恢复事件命名 `OnAudioRestored`：F6 已对冲为「以 F2 定义为准 / 可复用 `OnBeatClockResumed`」；F2 未单独定义设备重连事件。
- `AdaptationWindowBeats`（C1 PascalCase）vs `adaptationWindowBeats`（C2 camelCase）：语义已互引对齐，命名统一可在实现期定。
- BD-3（连击只增不减）、SC-3/SC-4（accept/boundary 发散窗、Boss 切换丢充能）为 WARNING，未纳入本轮阻塞集，待下一轮。

## 下一步
- 重跑 `/review-all-gdds`（新 session）验证 FAIL→PASS。
- owner 先于 FE4 Boss GDD 联合锁定 `AdaptationWindowBeats`（C1/C2/C4）。
