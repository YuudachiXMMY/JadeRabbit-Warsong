# Story 004: ChargedAttack Hold 状态机（E3 / E3a / E3b 三路检查）

> **Epic**: F3 Input 事件系统
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: [fill before sprint planning]
> **Manifest Version**: 2026-08-12
> **Last Updated**: [set by /dev-story when implementation begins]

## Context

**GDD**: `design/gdd/input-event-system.md`
**Requirement**: `TR-input-005`, `TR-input-011`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-D1: TDE 集成架构（primary，`_isSwitchingMaps` 门与 E3）；ADR-D6: 音频中间件（次要，`chargeStartDspTime` 采样）；ADR-D8: DI 缝隙（次要，Mock 时钟 + 内部状态可观测）
**ADR Decision Summary**: F3 `_isSwitchingMaps` 门必须在 `gameplayMap.Disable()` 之前置位，使 Action Map 切换期间的 `canceled` 回调走 E3 静默丢失路径；被动同步会产生不可保护的幽灵攻击（ADR-D1 拒绝 Alternative 2 的核心理由）。

**Engine**: Unity 6.3 LTS (6000.3.19f1) | **Risk**: HIGH
**Engine Notes**: Unity Input System 在 `InputActionMap.Disable()` 调用栈内**同步**触发所有 active interaction（含 Hold）的 `canceled` 回调，回调在 `Disable()` 返回前完成。触屏须将 Tap 与 Hold 绑定到**独立 InputAction**，避免中间区松开同时触发 Tap.canceled + Hold.canceled 造成双重 LightAttack（须实机验证）。

**Control Manifest Rules (this layer)**:
- Required: `_isSwitchingMaps` 三步时序保护；`chargeStartDspTime` 在 `Hold.started` 采样，作为 C1 节奏判定基准
- Forbidden: `Never let TDE be the Action Map authority with F3 passively syncing`（E3 幽灵攻击根因）；禁止用 `> 0` 判断退化 LightAttack（须 `> -1.0`）
- Guardrail: 事件广播路径零 GC；DSP 采样在主线程

---

## Acceptance Criteria

*From GDD `design/gdd/input-event-system.md`, scoped to this story:*

- [ ] AC-F3-03（充能段）：`Hold.performed` 广播的 `AttackInputEvent` 同时含 `inputDspTime`（完成时刻）与 `chargeStartDspTime`（`Hold.started` 缓存），且 ① `chargeStartDspTime > -1.0`（哨兵值守卫）；② `chargeStartDspTime ≤ inputDspTime`
- [ ] AC-F3-14（E3 切图中断静默丢失）：`EnableUIInput()` 致 Gameplay Map 禁用期间 ChargedAttack 正在 Hold，`canceled` 回调静默丢失（不广播），且可观测 `_isSwitchingMaps` 在回调执行期间为 `true`（走路径①）
- [ ] AC-F3-17（E3b 完成后松键不产生幽灵）：`Hold.performed` 后松键触发 `Hold.canceled`（`_isSwitchingMaps == false`），静默丢失；`OnAttackInput` 总共仅触发一次（ChargedAttack），`_holdPerformed` 末尾清除为 `false`；二次完整充能第二次 `performed` 正常广播（无残留）
- [ ] AC-F3-18（E3a 早松开退化 LightAttack）：`Hold.started` 后在 `performed` 前松键（`_isSwitchingMaps == false`，`_holdPerformed == false`）→ `OnAttackInput` 恰好 1 次、`attackType == LightAttack`、`chargeStartDspTime == expectedChargeStart`（≠ -1.0）、`inputDspTime == canceled 时刻`；回调后 `_chargeStartDspTime` 重置为 `-1.0`

---

## Implementation Notes

*Derived from GDD Core Rule 5 / E3 / E3a / E3b：*

- **E3 三路优先级检查**（`canceled` 回调）：(1) `_isSwitchingMaps == true` → 静默丢失（切图中断）；(2) `_holdPerformed == true` → 静默丢失（充能已完成，正常松键，E3b）；(3) 否则（`_holdPerformed == false` 且非切图）→ 退化为 LightAttack 广播（E3a）。三路检查顺序不可换：切图门优先于完成门，否则切图中断的已完成充能会漏判。
- **`_holdPerformed` 双重清除**：`Hold.started` 置 `false`（防御性重置）；`Hold.performed` 置 `true`；`Hold.canceled` 末尾无论走哪路径都置 `false`（消耗后重置）。
- **`_chargeStartDspTime` 生命周期**：`Hold.started` 赋值 `IDSPClock.GetDspTime()`；`Hold.performed` 写入广播事件、**不清零**；`Hold.canceled` 末尾统一重置为 `-1.0`。Debug 构建：在广播普通 Light/Heavy 的非 E3a 路径 Assert `_chargeStartDspTime == -1.0`（防残留污染）。
- **E3a 判定锚点**：退化 LightAttack 以 `chargeStartDspTime`（按下/意图时刻）而非 `inputDspTime`（松开时刻）为节奏基准——支柱二保障。C1 以 `attackType == LightAttack && chargeStartDspTime > -1.0` 区分退化 vs 普通轻攻击（须 `> -1.0` 而非 `> 0`）。
- **触屏中间区**（`touch_tap_max_duration_s` < 时长 < `charged_attack_hold_duration_s`，默认 300–500ms）：Tap 已超时、Hold 未 performed，`canceled` 以 `_holdPerformed == false` 触发 → 走 E3a。Tap/Hold 须绑独立 InputAction，防双重 LightAttack。
- **测试可观测内部状态**：AC-F3-14 须通过 Test Double 或反射确认 `_isSwitchingMaps` 在 `canceled` 执行期间为 `true`（防不同 bug 产生相同可观测输出）。经 Story 001 缝隙注入 Mock 时钟。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002：`EnableUIInput()`/`DisableAllInput()` 的 `_isSwitchingMaps` 三步时序**设置**（本 story 消费该标志做 E3 判定，验证切换期间标志为 `true`）
- Story 003：普通 Attack1/Attack2 的 `performed` 广播与哨兵值 `-1.0`
- C1/C2：退化 LightAttack 的下游判定路径选择（GDD 标为 C1/C2 设计决策）

---

## QA Test Cases

*Lean 模式无 qa-lead 生成；以下转写自 GDD 对应 AC 的「验证方法」，实现时对齐这些用例。*

- **AC-1 (AC-F3-03 充能段)**: ChargedAttack 广播完整性
  - Given: `GameplayActive`，Mock 时钟已注入
  - When: 触发 `Hold.started` → 步进时钟 → `Hold.performed`
  - Then: `OnAttackInput` 广播 `ChargedAttack`，`chargeStartDspTime > -1.0` 且 `chargeStartDspTime ≤ inputDspTime`
  - Edge cases: 两者相等属正常边界（同一 `InputSystem.Update()` 周期；Android DSP 粒度 46ms 内可能相同）
- **AC-2 (AC-F3-14 E3)**: 切图中断静默丢失
  - Given: ChargedAttack 正在 Hold
  - When: 调用 `EnableUIInput()`（Gameplay Map 禁用，同步触发 `canceled`）
  - Then: ① `OnAttackInput` 未触发；② 反射/Test Double 确认 `_isSwitchingMaps` 在 `canceled` 期间为 `true`
- **AC-3 (AC-F3-17 E3b)**: 完成后松键不产生幽灵
  - Given: `_isSwitchingMaps == false`
  - When: `Hold.started` → `Hold.performed`（正常完成）→ `Hold.canceled`
  - Then: `OnAttackInput` 总共仅 1 次（ChargedAttack）；`_holdPerformed` 末尾为 `false`；再跑一次完整充能，第二次 `performed` 正常广播（无残留）
- **AC-4 (AC-F3-18 E3a)**: 早松开退化 LightAttack
  - Given: `_isSwitchingMaps == false`，`_holdPerformed == false`
  - When: `Hold.started`（记 `expectedChargeStart`）→ 步进时钟 → `Hold.canceled`（跳过 `performed`）
  - Then: `OnAttackInput` 恰 1 次；`attackType == LightAttack`；`chargeStartDspTime == expectedChargeStart`（≠ -1.0）；`inputDspTime == canceled 时刻`；回调后 `_chargeStartDspTime == -1.0`（经后续普通 LightAttack 断言其 `chargeStartDspTime == -1.0` 验证）
  - Edge cases: guard 以 `> -1.0` 判断（`0.0` 为合法首帧 FMOD 时间）

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- `Assets/Tests/EditMode/input/chargedattack_hold_test.cs` — 覆盖 AC-F3-03（充能段）/14/17/18 的 EditMode 单元测试，必须存在且通过

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 002（`_isSwitchingMaps` 三步时序）、Story 003（Attack 广播与时间戳采样）
- Unlocks: None
