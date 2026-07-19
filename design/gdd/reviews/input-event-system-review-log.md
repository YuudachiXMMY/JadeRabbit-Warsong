# Review Log: F3 Input 事件系统 (Input Event System)

---

## Review — 2026-07-18 (Ninth Review) — Verdict: APPROVED
Scope signal: M
Specialists: game-designer, systems-designer, unity-specialist, qa-lead, creative-director
Blocking items: 3（session 内全部修复）| Recommended: 1（M0 spike 自然覆盖）
Summary: 第九轮全量对抗性审查（F6 GDD 今日创建触发——F6 解决了 F3 的 E8 开放问题，文档存在事实滞后）。三项真实阻塞：BLOCK-R9-01 E8 节 Action Map 权威归属仍标注为开放问题，已更新为 F6 GDD Option A 决策（TDELifecycleAdapter 为三个状态切换方法的唯一外部调用者，ADR-D1 Accepted 背书）；BLOCK-R9-02 缺少 UIActive→GameplayActive 方向的 AC（对称覆盖缺口），已新增 AC-F3-21（5 步 PlayMode 测试，暂停恢复场景）；BLOCK-R9-03 AC-F3-03 缺少 chargeStartDspTime 哨兵值守卫断言，已补充 ① chargeStartDspTime > -1.0 前置条件及级联失效说明。附加修复：删除过期 Lean 模式免责注释（文档已完成 8 轮全量审查，此注释已无效且误导）。一项推荐修订（R9-REC-01）：ActionMap.Disable() 同步 canceled 回调行为未经 Unity 6.3 LTS Input System 1.8.x 验证——creative-director 降级为 RECOMMENDED，将由 M0 spike 自然覆盖，不阻塞 F3 文档审批。九轮审查历史：F3 设计稳定，无回归，无设计变更，纯文档同步与 AC 精度修复。
Prior verdict resolved: Yes — 第八轮 4 项阻塞全部落地确认

---

## Review — 2026-07-15 (Eighth Review) — Verdict: APPROVED
Scope signal: M
Specialists: game-designer, systems-designer, unity-specialist, qa-lead, gameplay-programmer, creative-director
Blocking items: 4（session 内全部修复）| Recommended: 9（sprint 并行）
Summary: 第八轮全量对抗性审查（5 专家 + creative-director 综合，35 个原始发现去重为 4 真实阻塞）。BLOCK-R8-01：`DisableAllInput()` 缺少 `_isSwitchingMaps` 保护，过场/死亡/加载期间充能中断走 E3a 路径广播幽灵 LightAttack——Core Rule 5 补充三步时序保护说明；BLOCK-R8-02：触屏 Tap+Hold 双 `canceled` 风险——E3a 补充独立 Action 绑定架构规范；BLOCK-R8-03：`latency_offset_s` 无值域约束——F3-F2 变量表补充设计假设值域 `[-0.5, +0.5]`；BLOCK-R8-04：AC-F3-03 断言 `<` 改为 `≤` 消除 FMOD DSP 粒度边界下的 CI false-fail。设计哲学裁决：维持 `chargeStartDspTime`（按下时刻）作为 ChargedAttack 节奏判定基准（支柱二对齐）；维持 E3a 退化 LightAttack 设计（后续策略属 C1 职责）；ChargedAttack 历史拍点回溯为 C1 GDD 的设计问题（C1 本地维护环形缓冲区），不阻塞 F3。文档状态更新为 Approved — Implementation Ready，F3 正式进入可实现状态。
Prior verdict resolved: Yes — 第七轮 2 项推荐修订均已确认落地

---

## Review — 2026-07-15 (Seventh Review) — Verdict: APPROVED
Scope signal: M
Specialists: unity-specialist, qa-lead, game-designer, systems-designer, creative-director
Blocking items: 2（第六轮遗留推荐项升级为 pre-sprint 必修，session 内全部修复）| Recommended: 7（sprint 并行）
Summary: 第七轮全量审查（4 专家 + creative-director 综合）将第六轮两项推荐修订收尾落地：P1 将 E3a 下游指引中 `chargeStartDspTime > 0` 改为 `> -1.0`，消除与 guard table 的内部矛盾；P2 将 AC-F3-18 断言 3"非零"措辞改为"非哨兵值（即 ≠ -1.0；注：0.0 是合法首帧 FMOD 时间）"，消除语义歧义。两项均在 session 内修复。此外发现 7 项推荐修订（sprint 并行，不阻塞实现）：IL2CPP event Action<T> 装箱风险注记、ActionMap.Disable() 同步假设与包版本锁定要求、IsDegradedFromCharge 便捷属性定义、低端 Android SoC 96ms DSP 颗粒度注记、AC-F3-11 Editor/Development Build 约束、Tuning Knobs 联合约束公式（hold − tap ≥ 0.067s）、E3a 一句设计意图说明。文档状态更新为 Approved — Implementation Ready，F3 正式通过，可进入实现阶段。
Prior verdict resolved: Yes — 第六轮 2 项推荐修订均已落地

---

## Review — 2026-07-15 (Sixth Review) — Verdict: APPROVED
Scope signal: M
Specialists: lean 模式（无子 Agent）
Blocking items: 0 | Recommended: 2
Summary: 第六轮审查（Lean 模式，跨章节一致性精查）未发现新阻塞项。第五轮 10 项阻塞全部落地确认。发现 2 项推荐修订：(1) E3a 下游指引（第 260 行）使用 `chargeStartDspTime > 0` 与文档自身哨兵设计理由（选 -1.0 而非 0.0 的原因：FMOD 首帧可返回 0.0）存在内部矛盾，建议改为 `chargeStartDspTime > -1.0` 与 guard table 对齐；(2) Tuning Knobs 安全范围表中 touch_tap_max_duration_s [0.15, 0.5] 与 charged_attack_hold_duration_s [0.3, 1.0] 区间重叠，两参数同时在各自"安全"范围内仍可违反跨参数不变量，建议补充联合约束说明。两项均为文档层精度问题，不阻塞实现。F3 整体设计扎实，IDSPClock 抽象、零 GC 结构体、双时间戳、E3/E3a/E3b 完整状态机、20 条 AC 覆盖均达到实现级规格。
Prior verdict resolved: Yes — 第五轮 10 项阻塞全部落地

---

## Review — 2026-07-15 (Fifth Review) — Verdict: NEEDS REVISION → Revised In-Session
Scope signal: M
Specialists: `unity-specialist`, `qa-lead`, `game-designer`, `systems-designer`, `creative-director`
Blocking items: 10 (deduplicated from 18 raw findings, 4 duplicates merged) | Recommended: 4
Summary: 第五轮评审（5 专家，含 creative-director 合成）发现 10 项阻塞（全为文档精度修订，无结构变更），分两级处理。**Tier A（生产 Bug 风险，4 项）**：BLOCK-1 E6 边缘情况在 FMOD 架构下事实错误——AudioListener.pause 不影响 FMOD DSP 时钟（FMOD 独立音频线程），重写 E6 为 FMOD 同步模式保护说明；BLOCK-2 chargeStartDspTime 哨兵值 0.0 与会话首帧合法 DSP 时间冲突，统一为 -1.0（与 F2 GetConfirmedDSPStart 对齐），影响 struct 注释、guard table、E3b 清零、AC-F3-03/18 共 6 处；BLOCK-5 ProfilerRecorder marker 名称存在第 4 轮与第 5 轮直接矛盾（"GC Alloc" vs "GC.Alloc"），改为自验证方案——测试 setup 先用已知分配路径断言 recorder 有效，绕过文档层争议；BLOCK-10 F3-F2 变量表中 beat_dsp_time 值域未明确标注"已含 LatencyOffsetMs"，补充 F2 Core Rule 7 引用防止双重计算。**Tier B（测试空缺，6 项）**：BLOCK-3 AC-F3-14 仅测输出不测 _isSwitchingMaps 机制，新增标志状态断言；BLOCK-4 DontDestroyOnLoad 单例无重复实例防御，新增 AC-F3-19（重复自销毁）+ AC-F3-20（AllDisabled 初始状态）；BLOCK-6/7 Tuning Knobs 缺少 deadzone_inner<outer 和 tap_max<hold_duration 跨参数不变量约束；BLOCK-12 E3a 使用 chargeStartDspTime 的设计理由未文档化，补充 Pillar 2 对齐说明。3 项由 creative-director 降级为 Advisory：BLOCK-9（F3-F2 示例计算已存在，仅缺 ChargedAttack 变体）、BLOCK-11（latency 竞态由 Action Map 状态机自然阻止，新增一句约束）、BLOCK-13/14（C1 责任范围 / 产品决策非 F3 缺陷）。所有 10 项阻塞均在 session 内修复。附加修订：Dependencies 节新增延迟校准更新时序约束、DeferredAC-F3-16 重写为 FMOD 异步模式保护。
Prior verdict resolved: Yes — 第四轮 6 项阻塞已全部解决

---

## Review — 2026-07-14 (Fourth Review) — Verdict: NEEDS REVISION → Revised In-Session
Scope signal: M
Specialists: `unity-specialist`, `qa-lead`, `gameplay-programmer`, `game-designer`, `creative-director`
Blocking items: 6 | Recommended: 6
Summary: 第四轮评审发现 6 项阻塞（全为文档精度修订，无结构变更）：B1 AC-F3-01"构造函数注入"措辞错误（MonoBehaviour 不支持），改为属性/方法注入；B2 States 表格缺少初始状态规范，补充 Awake()→AllDisabled 规范；B3 `_isSwitchingMaps` 清除时序不精确（Unity Input System `Disable()` 同步触发 `canceled` 回调，标志须在 `Disable()` 返回后清零），补充严格时序说明；B4 Core Rule 2 及 Overview/Formulas/Dependencies 节仍引用已禁用 API `AudioSettings.dspTime`（违反 ADR-D6 与 architecture.yaml），统一更新为 `FMODDSPClockImpl`；B5 E3a 早松开退化路径经三轮评审仍无 AC，新增 AC-F3-18（5 项断言）；B6 `_chargeStartDspTime` 生命周期无规范，新增防残留协议（set/consume/clear + Debug 断言）。Advisory 修订：ProfilerRecorder marker `"GC.Alloc"` 改为 `"GC Alloc"`（Unity 6 正确名称）、方法组约束提升为 Core Rule 9、`_holdPerformed` 注释措辞修正、E3a 补 C1/C2 下游说明及触屏中间区文档、Open Questions 添加 MoveChangedEvent C2 依赖问题。E3a LightAttack 退化设计由 creative-director 裁决维持原方案，game-designer 的 ChargeAbortedEvent 建议降级为 Advisory（F3 仅传递事实，C1/C2 负责语义解释）。
Prior verdict resolved: Yes — 第三轮 2 项阻塞已全部解决

---

## Review — 2026-07-14 — Verdict: NEEDS REVISION → 阻塞已修复，待重审
Scope signal: M
Specialists: game-designer, systems-designer, qa-lead, gameplay-programmer, unity-specialist, creative-director
Blocking items: 2（本 session 内发现并已全部修复）| Recommended: 6
Summary: 第三轮审查（6 专家）发现 2 项新阻塞：(1) Hold.performed 后松键触发 canceled 回调，E3a 路径错误广播幽灵 LightAttack——缺少 `_holdPerformed` 内部标志；(2) AC-F3-11 ProfilerRecorder 在 CI -batchmode 下静默假阳性。两项均在 session 内修复：新增 E3b 章节（_holdPerformed 双重清除规范）+更新 E3 三路优先级检查+Core Rule 3 表格+AC-F3-17；AC-F3-11 补 Profiler.enabled 前置条件。附加修订：Android C1 约束 ≥70ms→≥80ms（最坏情况）；E3a 退化逻辑补充设计理由。文档状态改为「Revised — Pending Re-review」，建议在新 session 中运行 /design-review 重审。
Prior verdict resolved: Yes — 2026-07-14 APPROVED 后第三轮发现 2 项新阻塞，已修复

---

## Review — 2026-07-14 — Verdict: APPROVED
Scope signal: M
Specialists: game-designer, systems-designer, qa-lead, gameplay-programmer, audio-director, creative-director
Blocking items: 0（本 session 内发现 3 项新阻塞并已全部修订完成）| Recommended: 3
Summary: 前次 8 项阻塞已全部落地：chargeStartDspTime 双字段、C1 端 latency_offset_s 修正公式、平台误差对比表、deviceType/deviceId 值类型替换、E8 TDE Action Map 冲突记录均已写入。本次再审新发现 3 项阻塞——E3a 早松开退化 LightAttack 的判定时刻合约不明、sentinel 值（chargeStartDspTime == 0.0）被误用将导致约 −session_time_ms 灾难性偏差、AC-F3-16 集成测试不独立可测——已在 session 内全部修订完成：E3a 合约写入 AttackInputEvent 结构体注释及 Deferred ACs 节，guard 表补充至 F3-F2 公式块，AC-F3-16 迁出主列表改为 Deferred ACs 并注明 F5 负责。设计整体扎实，IDSPClock 抽象、零 GC 结构体、双时间戳设计符合技术要求。
Prior verdict resolved: Yes — 2026-07-14 NEEDS REVISION 的 8 项阻塞已全部落地

---

## Review — 2026-07-14 — Verdict: NEEDS REVISION
Scope signal: M
Specialists: game-designer, systems-designer, qa-lead, audio-director, gameplay-programmer, creative-director
Blocking items: 8 | Recommended: 3
Summary: F3 在整体架构（IDSPClock 抽象、Action Map 状态机、零 GC 结构）上设计完善，但在 ChargedAttack 时间戳语义、LatencyOffset 参考系不对称、Android 误差预算和 InputDevice 悬挂引用等细节存在 8 个实现阻塞项。修订后全部落地：引入 chargeStartDspTime 双字段、C1 端 latency_offset_s 修正公式、平台误差对比表（含 Android BPM ≤ 150 建议）、deviceType/deviceId 值类型替换，以及 E8 TDE Action Map 冲突记录（标记为 F6 GDD P0 阻塞项）。AC 节从 13 条扩充至 16 条，含 3 条新增边界情况测试（E3 切图静默丢失 / E5 冲突双播 / E6 DSP 冻结架构约束），AC-F3-02 从不可证伪精度断言改写为可测的采样时序约束。
Prior verdict resolved: No — First review
