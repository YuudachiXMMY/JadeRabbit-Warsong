# Story 010: Animancer 攻击层接线 + UpdateBpm 速度联动

> **Epic**: F6 TopDownEngine & Animancer 集成层
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: M
> **Manifest Version**: 2026-08-12
> **Last Updated**: [set by /dev-story when implementation begins]

## Context

**GDD**: `design/gdd/tde-integration.md`
**Requirement**: `TR-tde-001`（Animancer 攻击层集成）
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-D5: 动画管线（primary）
**ADR Decision Summary**: 攻击动画经 Animancer `HybridAnimancerComponent.PlayController()` 播放；攻击层动画时长随当前 BPM 动态缩放——不绑死帧数，经 `AnimancerState.Speed` 属性在 `UpdateBpm(newBpm)` 时重算并应用，保证节奏手感随变速一致。

**Engine**: Unity 6.3 LTS (6000.3.19f1) | **Risk**: MEDIUM
**Engine Notes**: Animancer 8.4 `HybridAnimancerComponent.PlayController()` / `AnimancerState.Speed` 为后-cutoff 插件 API——须对照 `docs/engine-reference/unity/plugins/animancer.md`，严禁从训练数据推断成员签名。

**Control Manifest Rules (Foundation)**:
- Required: 攻击动画速度经 `AnimancerState.Speed` 按 BPM 缩放（禁绑死帧数）
- Required: `UpdateBpm` 订阅 BeatClock BPM 变更，重算并应用攻击层速度
- Forbidden: 攻击层不直接改 TDE 角色 Animator 层速度（只接管攻击层）

---

## Acceptance Criteria

*From GDD `design/gdd/tde-integration.md`, scoped to this story:*

- [ ] AC-F6-12：攻击动画经 `HybridAnimancerComponent.PlayController()` 播放（非固定帧 Animator state）
- [ ] AC-F6-16：`UpdateBpm(newBpm)` 被调用时，攻击层 `AnimancerState.Speed` 按新 BPM 重算并应用（动画时长随 BPM 变速）
- [ ] AC-F6-15：BPM 变更后新触发的攻击使用更新后的速度；变更时已在播的攻击按策略处理（当拍完成或即时缩放，遵 GDD 定义）

---

## Implementation Notes

*Derived from ADR-D5 + GDD 变速合约:*

- 攻击触发：调 `HybridAnimancerComponent.PlayController()`（或 `Play(clip)` — 先查 `animancer.md` 确认攻击层 API），取回 `AnimancerState`。
- BPM 联动：组件订阅 BeatClock 的 BPM 变更事件，回调 `UpdateBpm(newBpm)` 内按 Story 011 公式算出 `speed` 并写 `attackState.Speed`。
- 已在播攻击的处理策略遵 GDD AC-F6-15（当拍完成 vs 即时缩放）——先读 GDD 该 AC 的确切定义再实现。
- 攻击层与 TDE 移动/生命 Animator 层隔离（Story 009 已建立边界），本 story 不触碰 TDE 层速度。

---

## Out of Scope

- Story 011：`speed = f(bpm)` 缩放公式本身（本 story 消费公式结果，接线播放/联动）
- Story 009：Prefab 装配 + Root Motion（前置）
- 连招图逻辑（C2 epic，跨 epic）

---

## QA Test Cases

*Embedded from GDD AC-F6-12/16/15（lean 模式）。*

**AC-1（AC-F6-12，Integration — PlayMode）**
  - Given：装配好的角色（Story 009）
  - When：触发一次攻击
  - Then：`PlayController()` 被调用恰 1 次，攻击 clip 经 Animancer 播放（非 Animator state 跳转）；返回有效 `AnimancerState`
  - Edge cases：无

**AC-2（AC-F6-16，Integration — PlayMode）**
  - Given：攻击层已接线，当前 BPM=120，基准 `AnimancerState.Speed` 已知
  - When：调 `UpdateBpm(180)`
  - Then：攻击层 `AnimancerState.Speed` == 公式(180) 的结果（如 180/120 = 1.5× 基准，具体按 Story 011 公式）
  - Edge cases：`UpdateBpm` 传入与当前相同 BPM → speed 不变（无冗余写入）

**AC-3（AC-F6-15，Integration — PlayMode）**
  - Given：BPM=120 下触发攻击后，播放中途 `UpdateBpm(180)`
  - When：变速发生
  - Then：按 GDD AC-F6-15 策略（当拍完成或即时缩放）——正在播的攻击行为符合定义；变速后新触发攻击用 180 对应速度
  - Edge cases：变速与攻击触发同帧 → 新攻击用新 BPM 速度

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- `Assets/Tests/PlayMode/F6/AnimancerAttackLayer_test.cs`（AC-12 PlayController + AC-16/15 UpdateBpm 速度联动）— 必须存在且通过

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 009（`HybridAnimancerComponent` 已装配）、Story 011（速度公式）；跨 epic 需 F2 BeatClock BPM 变更事件
- Unlocks: None
