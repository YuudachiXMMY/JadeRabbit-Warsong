# Story 009: Character Prefab 装配 + Root Motion 策略

> **Epic**: F6 TopDownEngine & Animancer 集成层
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: [fill before sprint planning]
> **Manifest Version**: 2026-08-12
> **Last Updated**: [set by /dev-story when implementation begins]

## Context

**GDD**: `design/gdd/tde-integration.md`
**Requirement**: `TR-tde-001`（双组件挂载 — 角色 Prefab 侧），Animancer 集成前置
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-D5: 动画管线（primary）；ADR-D1: TDE 集成
**ADR Decision Summary**: 角色 Prefab 挂载 TDE `Character` + `HybridAnimancerComponent`（攻击层）+ `JadeRabbitInputManager`；Root Motion 由 TDE 移动系统权威控制位移，Animancer 攻击层不驱动位移（避免与 TDE `CharacterMovement` 双重驱动 Transform）。

**Engine**: Unity 6.3 LTS (6000.3.19f1) | **Risk**: MEDIUM
**Engine Notes**: TDE `Character`/`CharacterMovement` 与 Animancer `HybridAnimancerComponent` 均为后-cutoff 插件 API——挂载点/Root Motion 开关须对照 `docs/engine-reference/unity/plugins/top-down-engine.md` 与 `plugins/animancer.md`，严禁臆测成员名。

**Control Manifest Rules (Foundation)**:
- Required: 位移由 TDE 移动系统权威（Root Motion 不驱动 Transform 位移）
- Forbidden: Animancer 攻击层与 TDE `CharacterMovement` 双重驱动同一 Transform
- Guardrail: 角色 Prefab 组件数受控，无冗余 Animator 层冲突

---

## Acceptance Criteria

*From GDD `design/gdd/tde-integration.md`, scoped to this story:*

- [ ] AC-F6-14：角色 Prefab 正确装配 TDE `Character` + `HybridAnimancerComponent` + `JadeRabbitInputManager`，无组件冲突（进 Play 无 error/warning）
- [ ] AC-F6-22：Root Motion 位移权威归 TDE 移动系统——攻击动画播放期间角色位移仅由 TDE 控制，Animancer 攻击层不产生额外位移

---

## Implementation Notes

*Derived from ADR-D5 + ADR-D1 + GDD 组件边界合约:*

- 角色 Prefab 层级：TDE `Character`（含 `CharacterMovement` 等 Ability）+ `HybridAnimancerComponent`（攻击层专用）+ `JadeRabbitInputManager`（Story 001）。
- Root Motion：Animancer 攻击层 clip 的 Root Motion 应用须关闭对 Transform 的位移写入（`Animator.applyRootMotion` 与 Animancer Root Motion 设置先查 `animancer.md`）；位移全权交 TDE `CharacterMovement`。
- TDE 角色 Animator 层保持不变（移动/生命层），Animancer 仅接管攻击层——两者 Avatar Mask / 层不冲突（F6 GDD 接口合约）。
- 装配为 Prefab 配置 + 少量 wiring 代码；核心是无冲突与 Root Motion 归属正确。

---

## Out of Scope

- Story 010：Animancer 攻击层 `PlayController()` + `UpdateBpm` 速度接线（本 story 只装配 + Root Motion 策略）
- Story 011：攻击速度缩放公式
- Story 001：`JadeRabbitInputManager` 实现（本 story 只挂载）

---

## QA Test Cases

*Embedded from GDD AC-F6-14/22（lean 模式）。*

**AC-1（AC-F6-14，Logic/Integration — PlayMode）**
  - Given：装配完成的角色 Prefab 实例化入场景
  - When：进入 Play 并触发一次攻击动画
  - Then：无 Console error/warning；`Character`、`HybridAnimancerComponent`、`JadeRabbitInputManager` 三组件均存在且启用；Animancer 攻击层与 TDE Animator 层无重复驱动报错
  - Edge cases：Prefab 缺任一必需组件 → 装配校验测试失败（明确断言组件存在）

**AC-2（AC-F6-22，Logic — PlayMode）**
  - Given：角色静止站立，攻击动画含 Root Motion 位移曲线
  - When：触发攻击动画播放（无移动输入）
  - Then：角色世界坐标位移 == 0（或仅 TDE 授权的位移）；攻击 clip 的 Root Motion 未写入 Transform
  - Edge cases：攻击期间同时有移动输入 → 位移量 == TDE 移动系统计算值，无 Animancer 叠加

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- `Assets/Tests/PlayMode/F6/CharacterPrefabAssembly_test.cs`（AC-14 组件装配校验 + AC-22 Root Motion 位移归属）— 必须存在且通过

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001（`JadeRabbitInputManager` 挂载）
- Unlocks: Story 010（Animancer 攻击层接线依赖已装配的 `HybridAnimancerComponent`）
