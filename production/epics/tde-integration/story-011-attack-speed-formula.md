# Story 011: 攻击速度缩放公式

> **Epic**: F6 TopDownEngine & Animancer 集成层
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: [fill before sprint planning]
> **Manifest Version**: 2026-08-12
> **Last Updated**: [set by /dev-story when implementation begins]

## Context

**GDD**: `design/gdd/tde-integration.md`
**Requirement**: `TR-tde-001`（攻击层变速——公式半）
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-D5: 动画管线（primary）
**ADR Decision Summary**: 攻击动画播放速度 = 基于当前 BPM 相对基准 BPM 的比值缩放（`speed = bpm / baseBpm`，含钳制上下限），使一次攻击恒占固定拍数而非固定秒数；公式为纯函数，可脱离 Unity 单测。

**Engine**: Unity 6.3 LTS (6000.3.19f1) | **Risk**: LOW
**Engine Notes**: 纯 C# 数学函数，无引擎 API 依赖——无成员名验证风险。缩放结果由 Story 010 写入 `AnimancerState.Speed`。

**Control Manifest Rules (Foundation)**:
- Required: 攻击速度公式为纯函数（无副作用，DI 友好，可 EditMode 单测）
- Required: 速度含上下限钳制（防极端 BPM 下动画过快/过慢破坏可读性）
- Forbidden: 公式内禁读全局单例/`Time`（纯输入 → 输出）

---

## Acceptance Criteria

*From GDD `design/gdd/tde-integration.md`, scoped to this story:*

- [ ] AC-F6-13：攻击速度公式 `speed = f(bpm, baseBpm)` 按 GDD Formulas 段定义计算，一次攻击恒占固定拍数（同拍数下 BPM 翻倍 → speed 翻倍）
- [ ] AC-F6-18：速度结果被钳制在 GDD 定义的 `[minSpeed, maxSpeed]` 区间内（极端 BPM 下不越界）

---

## Implementation Notes

*Derived from ADR-D5 + GDD Formulas 段:*

- 先读 GDD `design/gdd/tde-integration.md` 的 Formulas 段，取 `speed` 公式与 `baseBpm`、`minSpeed`、`maxSpeed` 的确切定义与默认值（勿臆造数值）。
- 实现为纯静态/可注入函数 `float ComputeAttackSpeed(float bpm, float baseBpm, float min, float max)`：核心 `bpm / baseBpm`，再 `Mathf.Clamp(raw, min, max)`。
- 参数（baseBpm/min/max）为数据驱动（Tuning Knob，从 SO 或配置读），不硬编码在逻辑里。
- Story 010 在 `UpdateBpm` 中调用本公式，将结果写入 `AnimancerState.Speed`。

---

## Out of Scope

- Story 010：公式结果的接线与应用（本 story 只出纯公式）
- 动画 clip 本身的制作（art/动画 epic）

---

## QA Test Cases

*Embedded from GDD AC-F6-13/18（lean 模式）。纯函数 → EditMode 单测。*

**AC-1（AC-F6-13，Logic — EditMode）**
  - Given：`baseBpm = 120`（占位，以 GDD 实际值为准）
  - When：`ComputeAttackSpeed(240, 120, min, max)`
  - Then：结果 == 2.0（BPM 翻倍 → speed 翻倍，固定拍数）；`ComputeAttackSpeed(120,120,..) == 1.0`；`ComputeAttackSpeed(60,120,..) == 0.5`
  - Edge cases：边界 BPM（GDD 定义的最小/最大 BPM）代入结果符合预期比值

**AC-2（AC-F6-18，Logic — EditMode）**
  - Given：`min`、`max` 为 GDD 定义值
  - When：代入极高 BPM（超出 max 对应）与极低 BPM（低于 min 对应）
  - Then：结果被钳制 == `max` / `min`，不越界
  - Edge cases：恰好等于 min/max 边界的 BPM → 结果 == 边界值（含端点）；负数或 0 BPM → 按 GDD 定义处理（钳制或抛异常，遵 GDD）

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- `Assets/Tests/EditMode/F6/AttackSpeedFormula_test.cs`（AC-13 比值 + AC-18 钳制，纯函数确定性单测）— 必须存在且通过

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: None（纯公式，可独立于接线实现）
- Unlocks: Story 010（`UpdateBpm` 消费本公式）
