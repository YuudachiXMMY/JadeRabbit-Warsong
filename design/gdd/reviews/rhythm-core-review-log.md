# Review Log: C1 节奏量化攻击系统 (rhythm-core.md)

---

## Review — 2026-07-21 — Verdict: APPROVED

Scope signal: L
Specialists: lean 模式（无专家子代理）
Blocking items: 0 | Recommended: 2
Prior verdict resolved: Yes（Round 4 的 7 项阻塞已全部解决）

### Summary

第五轮 lean 复查通过，C1 GDD 正式获批。全部 8/8 必需章节完整；Round 4 七项阻塞逐一核验均已正确落地（延迟安全约束、Off 模式 IsStartConfirmed 豁免、AC-C1-02b 边界修正、`_skipToggle` 重置、AC-C1-15 Beat 模式额外断言、AC-C1-19 platformMinPerfectMs 放弃声明、AC-C1-07a/b 拆分、Tuning Knobs 实现指引）。无新增阻塞项。两项推荐修订均为非阻塞：① 补充 LookaheadMs 与 EarlyWindowMs 时序盲区说明（建议在 ADR-D3 中锚定评级到 beat_dsp_time 而非 event receive time）；② 在状态机章节添加 Beat skip 相位屏蔽行为的显式注释。C1 可进入实现 sprint。

---

## Review — 2026-07-21 — Verdict: MAJOR REVISION NEEDED → Revised Pending Re-review

Scope signal: L
Specialists: game-designer, systems-designer, qa-lead, unity-specialist, creative-director
Blocking items: 8 | Recommended: 11
Prior verdict resolved: First review

### Summary

首次全深度评审（full mode，5 专家并行）发现 8 项阻塞问题，主要集中在三个领域：
① **窗口不等式违反**：EarlyWindow 未与 GoodHalfWindowMs_effective 对齐，高 BPM 时接受的输入可能被判为 Miss，直接破坏 Player Fantasy（B5）；
② **Android E7 二次钳位缺失**：坍缩后 GoodHalfWindowMs_effective 可能超出 halfBeatMs/2-1，造成相邻拍窗口重叠（B6）；
③ **新概念 CancelTier 未定义**：Rule 9 引用高级/标准取消窗口但无枚举规范，C2 无法据此实现（B8）。

同次评审发现 ADR-D3 存在三处与 GDD 矛盾：Off 模式分支缺失、LateWindow 使用 realtimeSinceStartup 而非 DSP、struct 缺失 ComboCountAfter/CancelTier 字段。全部已在 GDD 中以"注记 ADR-D3 须同步更新"的方式标注。

本轮修订在同一 session 内完成所有 8 个阻塞项和 11 个推荐项，包括新增 Rule 10 CancelTier 定义、C1-F3 窗口不等式不变量、E7 二次钳位逻辑、M3 接口注入方案、Off 模式无障碍合约三项约束，以及 E2 静默丢弃设计决策（用户确认）。

建议在新 session 运行 /design-review design/gdd/rhythm-core.md 进行正式重审。

---

## Review — 2026-07-21 — Verdict: NEEDS REVISION → Revised Round 2 Pending Re-review

Scope signal: M
Specialists: game-designer, systems-designer, qa-lead, unity-specialist, creative-director
Blocking items: 3 | Recommended: 5
Prior verdict resolved: Yes（Round 1 的 8 项阻塞已于上轮全部解决）

### Summary

第二轮全深度评审（5 专家并行）发现 3 项阻塞问题：① **E3a 跨 GDD 冲突**——C1-F1 对退化充能场景（LightAttack + chargeStartDspTime > -1.0）使用 inputDspTime，与 F3 guard table 规定的 chargeStartDspTime 直接矛盾，量化偏差可达 350ms（Perfect vs Miss）；② **Beat 模式公式遗漏 gridIntervalMs 抽象**——C1-F3 直接使用 halfBeatMs 作为钳位上界，导致 Beat 模式窗口被错误压缩至 HalfBeat 标准；③ **AC-C1-07 完全不可测**——"其余逻辑同 HalfBeat"无法生成 pass/fail 断言。

本轮修订在同 session 内完成全部 3 项阻塞和 5 项推荐修订：引入 gridIntervalMs 变量统一 HalfBeat/Beat 模式钳位公式，修正 C1-F1 条件为纯 chargeStartDspTime 判断，重写 AC-C1-07 为 BPM=120 具体断言，同步修正 C1-F5 Assert 条件编译包裹（#if UNITY_EDITOR || DEVELOPMENT_BUILD）、M3 宏错误（#if DEBUG → DEVELOPMENT_BUILD），以及 Rule 9/10 顺序颠倒问题。

建议运行 /clear 后在新 session 运行 /design-review design/gdd/rhythm-core.md 进行第三轮正式重审。

---

## Review — 2026-07-21 — Verdict: NEEDS REVISION → Revised Round 3 Pending Re-review

Scope signal: L
Specialists: game-designer, systems-designer, qa-lead, unity-specialist, creative-director
Blocking items: 4 | Recommended: 4
Prior verdict resolved: Yes（Round 2 的 3 项阻塞已于上轮全部解决）

### Summary

第三轮全深度评审（5 专家并行）发现 4 项阻塞问题：① **P1 C1-F5 Assert 顺序错误**——`_effectivePerfectMs = max(...)` 提升 Perfect 后未内联 E7 坍缩逻辑，Assert `_effectivePerfectMs <= _effectiveGoodMs` 在 Android BPM ≥ 186 时误报 false；② **P2 AC-C1-07 与 F2 Core Rule 3 矛盾**——AC-C1-07 声称"F2 在 Beat 模式每 500ms 广播一次"，而 F2 Core Rule 3 明确 F2 始终广播完整半拍/全拍网格不感知量化模式；③ **P3 AC-C1-06 遗漏 CancelTier=Advanced**——Off 模式断言缺少 CancelTier 字段；④ **P4 RhythmJudgmentEvent 无规范性定义**——字段散落 4 处，ComboCountAfter/CancelTier/HeavyAttack 均缺失。

本轮修订在同 session 内完成全部 4 项阻塞和 4 项推荐修订：C1-F5 内联 E7 二步坍缩逻辑（Assert 顺序修正），C1-F3 不变量措辞改为"经 E7 坍缩修复后始终成立"；Rule 1 Beat 行触发条件改为 C1 skip toggle 说明，AC-C1-07 重写为含初始相位和可测断言的具体描述；AC-C1-06 补充 CancelTier=Advanced；新增 C1-F6 RhythmJudgmentEvent 规范性字段定义节（含完整字段表和 ADR-D3 同步需求）；顺带修复 C1-F1 变量表 HeavyAttack 遗漏。

建议运行 /clear 后在新 session 运行 /design-review design/gdd/rhythm-core.md 进行第四轮正式重审。

---

## Review — 2026-07-21 — Verdict: NEEDS REVISION → Revised Round 4 Pending Re-review

Scope signal: L
Specialists: game-designer, systems-designer, qa-lead, unity-specialist, creative-director
Blocking items: 7 | Recommended: 5
Prior verdict resolved: Yes（Round 3 的 4 项阻塞已于上轮全部解决）

### Summary

第四轮全深度评审（5 专家并行）发现 7 项阻塞问题，集中于三类：
① **数学安全约束缺失**：latency_offset_s 允许范围 ±200ms，超过 GoodHalfWindowMs_effective(120ms) 时完美踩拍也是 Miss（S1）；AC-C1-19 未明确极高 BPM 下 platformMinPerfectMs 承诺被放弃（S2）；
② **Off 模式与全局规则交互孤岛**：Rule 7 无 Off 模式豁免条款，IsStartConfirmed==false 时 Off 模式输入被永久挂起直至 2s 超时，无障碍合约被破坏（G2/Q3/U2）；
③ **Beat 模式 skip toggle 生命周期管理**：C1-F5 OnBpmChanged 未重置 skip toggle，Boss 阶段 BPM 切换后首拍可能被跳过（P3）；AC-C1-07 拆分前不可测（P5）；AC-C1-02b 边界点与 AC-C1-04 矛盾（P2）；platformMinPerfectMs 存储机制未定义（P6）。

creative-director 裁定：game-designer 提出的"激励真空"（Rule 5+9 联动）为可接受的设计选择——"宽容但有梯度"模型服务 Player Fantasy，与 Hi-Fi Rush 先例一致，不列为阻塞。

本轮修订在同 session 内完成全部 7 项阻塞和 1 项推荐（U4 Debug.Assert 注释修正）：C1-F2 新增延迟安全约束（FE7 层拦截方案）；Rule 7 新增 Off 模式豁免条款；AC-C1-02b 边界条件改严格小于；C1-F5 末尾加 `_skipToggle = false`；AC-C1-15 补充 Beat 模式断言；AC-C1-19 明确 platformMinPerfectMs 放弃声明；AC-C1-07 拆分为 07a/07b；Tuning Knobs 新增 platformMinPerfectMs 实现指引。

建议运行 /clear 后在新 session 运行 /design-review design/gdd/rhythm-core.md 进行第五轮正式重审（lean mode 复查即可）。

---

## Carry-Forward Notes — 2026-08-08 — 第五轮复查再确认（C1 保持 APPROVED）

lean 模式稳定性复查：8/8 章节完整，边界值逐一复算通过（C1-F3 临界 BPM 124、AC-C1-13 92.75ms、E7 坍缩 BPM≥186、AC-C1-19 二次钳位 61.5ms 均一致），Round 4 七项阻塞全部核验落地，无新增阻塞。追踪记录已是最新，未改动。

以下为非阻塞推荐项，落点在 FE7 / ADR-D3，不影响 C1 进入实现：

- **[FE7]** R1 — C1-F2 延迟安全约束 `|latency_offset_ms| < GoodHalfWindowMs_effective`
  的上限随 BPM 收缩（BPM=160 时仅 92.75ms）。FE7 校准界面拦截时须针对关卡 BPM 跨度
  内的最小有效 Good 窗口（或保守固定上限）判定，而非 authored 默认 120ms。FE7 GDD
  须锚定该数值来源；Tuning Knobs 表 latency_offset_s 范围 [-0.2,0.2] 需加一致性注解。
- **[ADR-D3]** R3 — 明确评级锚定到 beat_dsp_time 而非事件接收时刻（GDD 已正确，ADR 伪代码待同步）。
- **[C1 文档-可选]** R2 — States and Transitions 表可补一条 Beat 模式 skip 相位屏蔽注释（纯可读性）。
