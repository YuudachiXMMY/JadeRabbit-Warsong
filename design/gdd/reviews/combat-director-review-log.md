# Combat Director (C4) — Review Log

## Review — 2026-08-10 — Verdict: NEEDS REVISION
Scope signal: XL
Specialists: game-designer, systems-designer, ai-programmer, qa-lead, unity-specialist, creative-director (senior synthesis)
Blocking items: 12 | Recommended: 10
Summary: 骨架与硬数学扎实、8/8 章齐全、F2 事件引用真实无悬空——但当前不可实现。头号阻塞是「幽灵数据契约」：C4 全部 9 个非-W Knob（TimeoutBeats/CapacityTable/D_near/D_mid/W_near/W_mid/W_far/starvationWeight/starvationCeiling/subdivisionMode）在 F4-S5 EnemyInfoSO 与 F4-S6 LevelConfigSO 中全部缺席，工程师无 SO 字段可读。其余阻塞：F-1 演示用的「6/8」拍号在 F4 枚举 {3,4,5} 中不可达（死配置）；DownbeatOnly+默认 TimeoutBeats 在任何合法拍号下 E11 恒失败（出货即死）；约束(4) starvationCeiling<TimeoutBeats 是跨阶段比较两个不同计时器的假不变式（须删除，非调数值），且默认值 8 vs 2/4 自我违反；CapacityTable 长度无跨资产校验 → 主线程 IndexOutOfRangeException 硬崩；F-6 GrantSelection 同线程重入崩溃（AC-C4-22 补位场景直接触发，须定死快照-延迟提交）；F-5 targetLandingBeat: float 浮点确定性动摇「落拍数学必然」（改整数细分）；AC-C4-04 标签/范围造假（Logic 误标 Integration，须拆真集成占位 AC）；AC-C4-29 依赖不存在的 FE1（拆 29a/29b）；「听出方向」幻想承诺无归属无测试（C2/F6 软依赖须升硬契约或收窄文案）；Status.Running 阻塞语义未定义→敌人罚站围观；beatPhaseAnchor(F4) vs subdivisionMode(C4) 双头权威。需新建 ADR（重入纪律+相位权威）、修订 ADR-D10（订阅生命周期）、修订 F4 GDD（补 SO schema，新建 CombatDirectorConfigSO）。creative-director 裁决不到 MAJOR：核心构想健全，每条阻塞有界可修。
Prior verdict resolved: First review

Specialist disagreement (需用户后续拍板): 事件订阅生命周期（unity B4）严重度——unity-specialist 主张并列 Blocking，creative-director 降为 ADR-D10 修订项。creative-director 明言此为尺度问题非对错。

Follow-up (前置解封顺序): (1) F4 GDD 补 SO schema → 先解封；(2) 删约束(4)、换 6/8 拍号、修默认容量表加 0 容量退潮拍；(3) F-6 定死重入纪律、拍表示改整数细分；(4) C2/F6 升硬契约+交付方向 AC；(5) AC 清理；(6) 开 ADR（重入+相位权威、ADR-D10 生命周期）。
Report path: 本条目即摘要；完整专家发现见 2026-08-10 会话记录。修订约定另起会话进行。
