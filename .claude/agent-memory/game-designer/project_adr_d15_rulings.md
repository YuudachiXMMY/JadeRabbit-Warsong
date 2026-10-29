---
name: adr-d15-rulings
description: ADR-D15 两项设计裁决的结论与推理锚点（Early 侧 Miss 可达性 + 钳位降级优先序），含新发现 SPEC-4
metadata:
  type: project
---

2026-10-22 为 ADR-D15（节奏判定窗缺陷）出具两项设计裁决，裁决文本由主会话落盘。

**裁决 1 — Early 侧 Miss 不可达**：判定为「第三种解读」——`rhythm-core.md:287` 的
`EarlyWindowMs_effective = min(EarlyWindowMs_authored, GoodHalfWindowMs_effective)` 是
「刻意的微决策 + 未审计的宏观后果」。处置 = **(c) 部分对称化**（Early 侧获得结构性非空 Miss 带 +
新增 `EarlyLenienceRatio` 旋钮默认 1.0=对称）。关键论据：Early 侧的失败模式是**静默吞输入**
（无事件、无攻击、无 early/late 方向提示），正是 `rhythm-core.md:29` 明令禁止的失败态，比 Miss 更糟。

**裁决 2 — 钳位降级优先序**：修正主会话的前提。P1(不混叠) > P2(platformMinPerfect) > P3(Miss 非空)，
但关键是 **P2 与 P3 的可行性边界数学上同一**（均为 `platformMinPerfectMs > gridIntervalMs/2 − 1`），
所以「三者不可兼得」只对 `MissHeadroomMs` 的**绝对毫秒下限**形式成立。处置 = 把 MissHeadroom 改为
**剩余预算占比**（`min(authored, (C − PerfectEff) × MissBudgetShare)`），并把 E7 从「补丁式 pass」
重写为单一有序预算分配函数。`MissBudgetShare < 1` 可数学保证三档在边界以下恒非空。

**Why:** 两项裁决都源于苗刀切片实证（`_goodAuthoredMs` 120→100 翻转 Miss 可达性），且审阅中发现
**SPEC-4**（更严重）：ADR-D3 伪代码只在 `OnHalfBeatLookahead` 时刻扫描预缓冲，落在
`(T − LookaheadMs, T)` 的输入永不被评级 → 默认值下 **整个 Early 侧 Perfect 区结构性不可达**。

**How to apply:** 再碰判定窗/延迟补偿话题前，先确认 SPEC-4 是否已修（建议统一为签名偏移单路径
`TryEvaluate`，不让 F2 的 `LookaheadMs` 决定 C1 的评级几何）。另：无线音频残余校准误差符号方向是
**late（正偏移）**——校准链三处截断（±200ms clamp / 最近邻折叠 / `rhythm-core.md:259` 要求钳到
<120ms）全部单向压向补偿不足，所以「Early 宽容=无障碍特性」的假设方向是反的。
还有两处待独立裁决的矛盾：ADR-D3`:115`「先到优先」vs `rhythm-core.md:83-87`「后到优先」；
E7`:661-662`「超出 → Miss」在 Late 窗被钳到 C 之后不再成立。

相关：[[project_jade_rabbit_core]]、[[project_design_review_concept]]
