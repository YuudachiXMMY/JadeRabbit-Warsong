---
name: C1 Rhythm Quantization System — AC Adversarial Review
description: Round 4 (2026-07-21): 17 BLOCKING total (13 carried + 4 new: B-18a LateWindowMs边界点矛盾, B-18 skip-toggle纯事件不可验证, B-19a Off×IsStartConfirmed盲区, B-19b BPM变化skip-toggle相位未定义); C1 sprint blocked
type: project
---

Updated 2026-07-21. Round 4 adversarial pass (针对 6 个具体分析任务的深审，基于 Round 3 的 13 条 BLOCKING).

## Gate Status: BLOCKED — 17 BLOCKING findings; all must resolve before C1 implementation sprint

---

## BLOCKING Findings

### Carried from Round 3 (unresolved)

**B-02 — AC-C1-17: 2s 超时机制仍无可测试接口**
GDD E2 描述了 2s 超时，但 Formulas、ADR-D3、Core Rules 均无 ITimeProvider 定义。
测试员无法在单元测试中控制"2秒"流逝。
Fix: 在 C1 GDD Formulas 或 ADR-D3 中定义 ITimeProvider 注入接口；AC-C1-17 须说明通过注入时钟验证超时。
额外：AC-C1-17 未说明如何用 LogAssert 验证 Warning 日志。

**B-03 — AC-C1-19: Android 坍缩 AC 在 PC CI 不可验证**
platformMinPerfectMs 为"平台配置（非 SO）"，PC CI Runner 执行 PC 路径永不触发 Android 坍缩逻辑。
Fix: platformMinPerfectMs 改为 RhythmConfigSO 可注入字段；AC-C1-19 指定注入 80ms 测试。

**B-04 — AC-C1-22, AC-C1-23: 集成测试与单元测试混合**
AC 文本未拆分，单元层字段验证（C1 职责）与集成层效果验证（PL2/C2 职责）混合。
Fix: 各拆为 AC-C1-22a/22b 和 AC-C1-23a/23b；C1 单元测试只断言 RhythmJudgmentEvent 字段；集成 AC 标注 [DEFERRED: PL2/C2 GDD]。

**B-08 — AC-C1-11, AC-C1-18: 与 F3 GDD E3a 判定锚点矛盾**
C1 GDD 已改为使用 chargeStartDspTime，但 F3 GDD 是否同步未知。
两个 GDD 的设计意图是否仍对立，需 game-designer ADR 裁决。
B-14（E3a Beat 模式）依赖此裁决。

**B-09 — AC-C1-01: PerfectHalfWindowMs 未区分 authored/effective**
AC 文本用 "PerfectHalfWindowMs" 但未指定是 authored 还是 effective 值。
BPM ≥ 124（HalfBeat）或 platformMinPerfectMs 介入时两者不同，导致断言基准歧义。
Round 4 扩展：AC-C1-02 同样未提供具体 BPM 测试用例，与 B-09 合并要求。
Fix: 统一改为 PerfectHalfWindowMs_effective，并为 AC-C1-01 和 AC-C1-02 各提供具体 BPM 场景数值用例。

**B-10 — AC-C1-06: Off 模式未断言 CancelTier=Advanced**
AC 只断言 Rating=Perfect，未覆盖 CancelTier 字段。
Off 模式下错误的 CancelTier（如 Standard/null）会导致 C2 连招系统行为错误但测试通过。
Fix: AC-C1-06 增加 CancelTier=Advanced 断言；OffsetMs=0 的写入方式须明确（强制写入 vs 计算结果）。

**B-11 — AC-C1-07: PerfectHalfWindowMs_effective 具体值缺失；Beat 模式 EarlyWindow 无 AC**
B-06 fix 提供了 Good 窗口数值用例，但 Perfect 窗口值仍为变量。
Beat 模式 EarlyWindow 缓冲行为（缓冲起点、评级触发时机）完全无 AC。
F2 在 Beat 模式下的事件名称（OnHalfBeatLookahead vs OnBeatLookahead）未明确。
Fix: AC-C1-07 补充 PerfectHalfWindowMs_effective=50ms（基于 authored=50ms, BPM=120）；新增 AC-C1-34 覆盖 Beat 模式 EarlyWindow 缓冲行为。

**B-12 — AC-C1-09: 评级时序未指定（M-C 升级为 BLOCKING）**
AC 说"首拍确认后统一评级"，但未说明是 IsStartConfirmed 变 true 立即评级，还是等下一个 OnHalfBeatLookahead 到来。
两种实现使用不同的 beat_dsp_time，导致截然不同的 OffsetMs 值。
Fix: AC-C1-09 明确"等待第一个 OnHalfBeatLookahead 事件，使用该事件的 beat_dsp_time 计算偏移"。

**B-13 — AC-C1-15: BPM 变化时无广播的负面断言；pendingInput+lateWindowActive 并存场景无测试**
AC 只测状态清除（清空+关窗口），没有断言"不广播 Miss"。
BPM 变化同时存在 lateWindowActive+pendingInput 时，AC 不覆盖竞态场景。
Fix: AC-C1-15 增加"BPM 变化后不产生任何 RhythmJudgmentEvent"的负面断言；新增 AC-C1-36 覆盖并存场景。

**B-14 — AC-C1-18: E3a 在 Beat 模式下的行为无 AC（依赖 B-08 裁决）**
E3a 充能中途松开时，按下时刻（chargeStartDspTime）在 Beat 模式下可能对应已过期的 EarlyWindow。
此场景判定行为未定义，与 Beat 模式 EarlyWindow 行为（B-11）相互交叉。
Fix: 先解决 B-08 ADR 裁决，再新增 AC-C1-35 覆盖 E3a × Beat 模式场景。

**B-15 — Off 模式 CancelTier 无独立 AC（AC-C1-32 缺失）**
AC-C1-06 和 AC-C1-21 均未断言 Off 模式广播的 CancelTier 字段。
Fix: 新增 AC-C1-32：Off 模式攻击 → RhythmJudgmentEvent { Rating=Perfect, CancelTier=Advanced }。

**B-16 — RhythmJudgmentEvent 结构完整性无 AC（AC-C1-33 缺失）**
没有任何 AC 验证事件结构中所有必需字段均存在且非 null/default。
字段：Rating, CancelTier, OffsetMs, ComboCountBefore, ComboCountAfter, AttackType。
Fix: 新增 AC-C1-33：对每种评级（Perfect/Good/Miss）断言事件包含全部字段。

**B-17 — BPM 变化时 pendingInput+lateWindowActive 并存场景无 AC（AC-C1-36 缺失）**
AC-C1-15 只测了单状态清除，未覆盖两者同时激活时的行为。
Fix: 新增 AC-C1-36：BPM 变化事件到达时，_pendingInput≠null 且 _lateWindowActive=true → 两者均清除，不广播 RhythmJudgmentEvent。

### New Round 4 Findings

**B-18a — AC-C1-02b vs AC-C1-04: LateWindowMs 精确边界点行为矛盾**
AC-C1-02b 说"偏移量 > GoodHalfWindowMs_effective 且 ≤ LateWindowMs → Miss"，AC-C1-04 说"LateWindow 过期后到达的输入 → 不产生 RhythmJudgmentEvent"。
当 beat_offset_ms 恰好等于 LateWindowMs 时，AC-C1-02b 声明 Miss，AC-C1-04 声明丢弃——两个 AC 同时拥有同一个边界点，行为相互矛盾。
Fix: AC-C1-02b 改为严格小于："> GoodHalfWindowMs_effective 且 < LateWindowMs → Miss"；AC-C1-04 明确持有 == LateWindowMs 的精确边界点（丢弃，不产生事件）。

**B-18 — AC-C1-07: skip toggle 在纯事件观察下不可验证**
当测试不注入攻击输入时，4 次 OnHalfBeatLookahead 事件均不产生 RhythmJudgmentEvent（0 次），"至多 2 次事件"断言永远成立，无法区分 skip 正确工作与"完全没有拍点处理"。
_lateWindowActive 是私有字段，若无测试接口不可直接断言。
Fix: 将 AC-C1-07 拆为：
  - AC-C1-07a（正向）：第 1、3 次 OnHalfBeatLookahead 到来前注入攻击输入 → 产生 RhythmJudgmentEvent（验证窗口开放）
  - AC-C1-07b（负向）：第 2、4 次 OnHalfBeatLookahead 到来前注入攻击输入 → 不产生 RhythmJudgmentEvent（验证窗口被跳过）
额外要求：lead-programmer 须提供 skip toggle 状态的测试接口或说明纯事件观察路径。

**B-19a — AC-C1-09 × Off 模式: IsStartConfirmed==false 时 Off 模式攻击路径未定义**
AC-C1-09 说"IsStartConfirmed==false 时输入到达 → 缓冲，首拍确认后评级"。
Off 模式下无 OnHalfBeatLookahead 事件，缓冲排空机制不存在——若 Off 模式也走此路径则进入死锁（缓冲永不排空）。
三条可能路径（Off 模式绕过时钟检查 / Off 模式也缓冲但 IsStartConfirmed 变 true 时立即排空 / Off 模式禁止在时钟未就绪时攻击）均未在 GDD 或 AC 中定义。
Fix: game-designer 裁决 Off 模式 × IsStartConfirmed==false 的语义；AC-C1-09 加入 Off 模式排除条件或专门分支；新增 AC-C1-40 覆盖此场景。

**B-19b — AC-C1-15: Beat 模式 BPM 变化后 skip toggle 相位未定义**
AC-C1-15 和 C1-F5 伪代码均未提及 skip toggle 重置。
BPM 变化时若 toggle 处于"跳过"相位，则变化后第一个 OnHalfBeatLookahead 被跳过，等效延迟 1 个全拍（最长 750ms @ BPM=80）才能接受输入——在 Boss 阶段 BPM 转换时造成可感知的手感断裂，但无测试覆盖此缺陷。
Fix: C1-F5 伪代码补充"_skipToggle = false（重置为处理相位）"；AC-C1-15 增加断言"BPM 变化后 Beat 模式 skip toggle 重置为处理相位（首个 OnHalfBeatLookahead 不被跳过）"；若设计意图是不重置，需明确说明并记录设计理由。

---

## Major Findings (须在 Sprint 内解决)

- M-A: AC-C1-13 须加 epsilon 容差说明（建议 ±0.01ms 或 Approximately 断言）和 halfBeatMs 字段类型声明
- M-B: AC-C1-08 未断言第二次输入的 OffsetMs（只测事件数量）
- M-D: AC-C1-25 Rule 8 OnBeatClockResumed 恢复后第一拍锚点（仍未写入 GDD）
- NEW R3: AC-C1-12 缺少负 latency_offset 数字用例
- NEW R3: AC-C1-03 前置条件含糊（EarlyWindow 内偏差超 Good 窗口是否可能，GDD 未在 AC 层说明已被 EarlyWindowMs 钳位排除）
- NEW R3: AC-C1-20 负 BPM（-1, -120）场景无 AC（E8 涵盖但 AC 只测了 0）
- NEW R4: AC-C1-17/19 Warning 断言方式未指定 — 须补充 LogAssert.Expect + 消息模式 [RECOMMENDED]
- NEW R4: AC-C1-14 ComboMax 边界测试建议注入小值（如 5）而非依赖临时默认值 50 [RECOMMENDED]

---

## AC Testability Summary (24 original + proposed new = 40+ target)

| Rating | ACs |
|--------|-----|
| YES | AC-C1-04, 05, 10, 12, 14, 16, 20 |
| BORDERLINE | AC-C1-02, 02b, 08, 21, 24 |
| NO/BLOCKER | AC-C1-01, 03, 06, 07, 09, 11, 13, 15, 17, 18, 19, 22, 23 |

Key changes from Round 3:
- AC-C1-02b: YES → BORDERLINE (LateWindowMs 边界点与 AC-C1-04 矛盾，B-18a)
- AC-C1-07: BLOCKER 升级（skip 在纯事件观察下不可测，需拆为 07a/07b，B-18）

---

## Missing ACs (19 total: 7 from Round 2 + 8 from Round 3 + 4 from Round 4)

Round 2 carry-over:
- AC-C1-25: Rule 8 OnBeatClockResumed 恢复后第一拍锚点 (M-D)
- AC-C1-26: E4 双击跨窗口（EarlyWindow→LateWindow）(M-02)
- AC-C1-27: EarlyWindow 最早边界精确值 (M-03)
- AC-C1-28: Off 模式 + 暂停/恢复后第一次攻击 (M-04)
- AC-C1-29: Android BPM 坍缩解除后 Good 评级恢复（依赖 B-03 fix）(M-05)
- AC-C1-30: chargeStartDspTime == -1.0 精确哨兵边界值 (M-06)
- AC-C1-31: LateWindow 上限精确边界 (M-07) — 与 B-18a 相关，须同步更新

Round 3 new:
- AC-C1-32: Off 模式 CancelTier=Advanced（BLOCKING）
- AC-C1-33: RhythmJudgmentEvent 结构完整性（BLOCKING）
- AC-C1-34: Beat 模式 EarlyWindow 缓冲行为（BLOCKING，依赖 B-11 fix）
- AC-C1-35: E3a × Beat 模式场景（BLOCKING，依赖 B-08 + B-14）
- AC-C1-36: BPM 变化时 pendingInput+lateWindowActive 并存场景（BLOCKING）
- AC-C1-37: latency_offset_s 负值场景数字用例 (WARNING)
- AC-C1-38: Android E7 坍缩后 ComboCountAfter 字段正确性 (WARNING)
- AC-C1-39: 负 BPM OnBpmChanged 行为 (INFO)

Round 4 new:
- AC-C1-07a: Beat 模式 skip toggle — 第 1、3 次 HalfBeatLookahead 开放窗口（正向，BLOCKING）
- AC-C1-07b: Beat 模式 skip toggle — 第 2、4 次 HalfBeatLookahead 不开放窗口（负向，BLOCKING）
- AC-C1-40: Off 模式 × IsStartConfirmed==false 攻击路径（BLOCKING，依赖 game-designer 裁决）
- AC-C1-41: Beat 模式 BPM 变化后 skip toggle 重置为处理相位（BLOCKING）

---

## Cross-GDD Conflict (B-08, unresolved)

E3a (LightAttack + chargeStartDspTime > -1.0) judgment anchor:
- C1 GDD current position: use chargeStartDspTime (press time, "rhythm commitment at press")
- F3 GDD position: unknown if updated to match — ADR not yet issued
Requires game-designer ADR before implementation. B-14 and AC-C1-18 both blocked on this.

---

## Priority Order for Resolution (Updated Round 4)

1. B-08 (E3a anchor ADR) — unblocks B-14, AC-C1-11, AC-C1-18, AC-C1-35
2. B-19a (Off×IsStartConfirmed design decision) — new, needs game-designer ruling; unblocks AC-C1-40
3. B-19b (Beat mode skip toggle reset) — C1-F5 pseudocode update; unblocks AC-C1-41
4. B-18a (LateWindowMs boundary point) — simple AC text fix; fixes AC-C1-02b and AC-C1-31
5. B-18 (AC-C1-07 split to 07a/07b) — requires lead-programmer test interface decision
6. B-04 (AC split) — unblocks test scope clarity for all of AC-C1-22/23
7. B-12 (AC-C1-09 timing) — state machine foundation
8. B-10 + B-15 (Off mode CancelTier) — simple fix, high C2 integration risk if skipped
9. B-09 (authored/effective terminology) — affects AC-C1-01, AC-C1-02, AC-C1-07
10. B-16 (struct completeness AC) — foundational, should be first test written
11. B-11 (Beat mode EarlyWindow) — unblocks AC-C1-34
12. B-13 (AC-C1-15 negative assertion) — prevents silent BPM-change Miss bug
13. B-17 (race condition AC) — AC-C1-36
14. B-02, B-03 (ITimeProvider, platformMinPerfectMs) — infrastructure changes, longer lead time

---

## How to Apply

Before C1 implementation sprint:
1. game-designer must resolve B-08 (E3a ADR) — highest priority, blocks multiple items
2. game-designer must rule on B-19a (Off × IsStartConfirmed design decision)
3. lead-programmer must confirm skip toggle test interface for B-18 (AC-C1-07a/07b)
4. Fix all 17 BLOCKERs before implementation begins
5. Write missing ACs AC-C1-25 through AC-C1-41 into GDD
6. Address Major findings M-A, M-B, M-D, and new R3/R4 RECOMMENDED within sprint
7. Confirm F3 GDD sync on E3a after ADR issued
