# Cross-GDD Review Report — 2026-07-15 (Rev.2)

**Scope**: `/review-all-gdds full` — Consistency + Design Theory + Scenario Walkthrough
**GDDs Reviewed**: F1 (game-concept), F2 (audio-beat-clock), F3 (input-event-system), F4 (so-data-layer), F5 (save-persistence)
**Registry Baseline**: `design/registry/entities.yaml` — 4 constants registered
**Previous Review**: 2026-07-15 Rev.1 — 4 BLOCKER / 16 WARNING / 9 INFO → CONCERNS
**Verdict**: **CONCERNS** — 1 BLOCKER / 19 WARNING / 15 INFO

---

## Comparison with Previous Review (Rev.1)

| Metric | Rev.1 (2026-07-15) | Rev.2 (本次) | Delta |
|--------|-------------------|-------------|-------|
| BLOCKER | 4 | 1 | -3 (B1 fixed; B3/B4 降级为下游约束; B2 保留) |
| WARNING | 16 | 19 | +3 (更精细场景走查发现新交互边界问题) |
| INFO | 9 | 15 | +6 (补充设计完整性标注) |

### 前次 BLOCKER 状态追踪

| ID | 问题 | 状态 | 说明 |
|----|------|------|------|
| B1 | F1↔F2 音频降级矛盾 | **FIXED** | F1 边缘案例表已拆分为 BPM=0 和音频设备不可用两行 |
| B2 | Merit 无消耗汇 | **OPEN → 本次 3d-1** | FE5 GDD 未创建，问题保留 |
| B3 | Boss 阶段 BPM 无 SO schema | **OPEN → 降级 WARNING** | 属 FE4 GDD 职责，非当前基础层阻塞 |
| B4 | F3-F2 公式无强制执行 | **OPEN → 降级 WARNING** | 公式正确，执行由 C1 GDD AC 承接 |

### Rev.1 WARNING 修复状态

| Rev.1 ID | 状态 | 说明 |
|----------|------|------|
| W2 (F2 OQ-2 过期) | **FIXED** | OQ-2 已用删除线标记并注明"已解决" |
| W10 (Stop() offset 保留) | **FIXED** | F2 Core Rule 10 已添加 "LatencyOffsetMs 不受 Stop() 影响" |
| W13 (LatencyOffsetMs 一次性) | **FIXED** | F2 Interactions 表已修正为"可随时重写" |
| 其余 13 项 | **OPEN** | 属下游 GDD 职责，当前基础层无法修复 |

---

## BLOCKER Issues (1)

### 🔴 B1: 功勋(Merit)无消耗定义 — 经济循环断裂

**来源**: Phase 3 (3d-1)
**受影响 GDD**: F1 (game-concept), F4 (so-data-layer), F5 (save-persistence)
**前次编号**: Rev.1 B2

`playerMerit` 有明确来源（`EnemyInfoSO.rewardMeritBase [0,100]`、通关结算）但在所有 5 个已审查 GDD 中无任何消耗目的地。F4 Open Questions 已标记此问题并传递至 FE5。F5 `PlayerProgressDTO.merit` 为 `[0, +∞)` 单调递增。

**影响**: Pillar 4（构筑驱动复盘）将因无进程摩擦力而失效——玩家在 5-10 局后积累足够 Merit 解锁一切，长线驱动力消失。

**修复**: FE5 (meta-progression) GDD 必须定义 Merit 消耗曲线。建议注册 `meta_upgrade_cost` 常量至 entities.yaml。非即时阻塞（FE5 排序在 C3/C5 之后），但在 FE5 设计前不可进入 FE5 实现。

---

## WARNING Issues (19)

### Phase 2: 一致性警告 (8)

#### ⚠️ W1 [2a-1]: F3↔F4 攻击类型枚举隐式映射未声明

F3 `AttackType {LightAttack, HeavyAttack, ChargedAttack}` vs F4 `AttackInputType {Normal, Heavy, Special}`。隐式映射未在 Dependencies 节声明。C2 GDD 设计时须显式定义映射表。

#### ⚠️ W2 [2a-4]: F2 Dependencies 未列 F4 为间接数据来源

F2 接收 `LevelConfigSO` 数据（通过 F6 注入），但 F2 Dependencies 仅列 F5 为运行时软依赖，未提及 F4。建议补充说明性注记。

#### ⚠️ W3 [2b-1]: F3 latency_offset 设计假设 ±500ms 与 F2/F5 硬限 ±200ms 不一致

F3-F2 公式注释使用"设计假设值域 `[-0.5, +0.5]`（±500ms）"，但 F2/F5/entities.yaml 均以 ±200ms 为硬限。不构成运行时矛盾（FE3 UI 层截断），但文档层面数值不一致。

**Quick fix**: F3 注释修改为"实际由 FE3 GDD 定案，当前 F2/F5 硬限为 ±200ms"。

#### ⚠️ W4 [2c-2]: systems-index F4 状态与 GDD 头部不一致

systems-index 标 F4 为 "Approved"，但 F4 GDD 头部声明 "In Review (Round-5 Revision)"。

**Quick fix**: 更新 systems-index 中 F4 状态为 "In Review"，或在 F4 审查通过后更新。

#### ⚠️ W5 [2d-1]: latency_offset entities.yaml source 归属不精确

entities.yaml 将 `latency_calibration_offset_ms` 的 source 指向 F4（so-data-layer.md），但 F4 标注此字段为 `[NonSerialized]` 运行时缓存。实际数据流：FE3 → F5（持久化）→ F2（消费）。值域数值一致不存在运行时冲突，仅 source 归属不精确。

**建议**: 等 FE3 GDD 创建后将 source 指向 FE3。

#### ⚠️ W6 [2e-1]: F1 base_damage 范围标 TBD，但 F4 已定义

F1 伤害公式中 `base_damage` 来源标为 "TBD（rhythm-core GDD）"，但 F4 WeaponSO.baseDamage 已定义范围 `[1.0, 100.0]`。

**Quick fix**: F1 Formulas 节将 `base_damage` 范围更新为 `[1.0, 100.0]（F4 WeaponSO.baseDamage）`。

#### ⚠️ W7 [2e-2]: F3 误差上限 vs F2 精度量级差异，C1 须分平台设窗口

F2 时间戳精度 < 0.1ms，F3 误差上限 ≤16.7ms@60fps。F3 已文档化此约束并提供平台误差占比表。C1 GDD 须据此设定分平台最小 Perfect 半窗口。

#### ⚠️ W8 [2f-4]: F5 AC 测试数据不遵循 F4 命名约定

F5 AC-F5-02 使用 `sword_iron`，但 F4 WeaponSO.id 命名格式为 `weapon_[name]`。

**Quick fix**: 改为 `weapon_miaodao`。

### Phase 3: 设计整体性警告 (7)

#### ⚠️ W9 [3b]: 战斗注意力达 4 系统阈值

30 秒核心循环中同时需主动关注 4 系统（节奏时机、空间位移、连招选择、敌攻预读），处于认知负荷安全线边界。C3 若引入需主动追踪叠层的心法将推至 5 系统。

**C3 约束**: 须包含"注意力成本"评级，限制局内同时激活的"主动追踪型"心法上限（建议 1 个）。

#### ⚠️ W10 [3c-1]: Perfect 严格支配 Good/Miss，无权衡取舍

Perfect 在伤害(100%)、连击(+2)、取消窗口(全开)、反馈四个维度均优于 Good/Miss，无策略理由选择非 Perfect。降低构筑多样性（Pillar 4）。

**C3 约束**: 须包含至少 1 个 MVP 心法展示非 Perfect 策略价值（如 OnMiss 触发护盾）。

#### ⚠️ W11 [3c-2 + 3d-2]: 连击正反馈雪球，缺乏追赶机制和衰减

Miss 不清零连击（F1 规则二），局内无负反馈（衰减、上限效应）。combo_multiplier（TBD）若为线性则差距只增不减。

**C1/C2 约束**: 须定义至少一项——combo_multiplier 边际递减公式 / combo 衰减曲线 / 追赶机制。

#### ⚠️ W12 [3e-1]: BPM 递增同时缩减判定窗口和前摇绝对时间

BPM 100→180 时半拍窗口 -44%、前摇绝对时间 -44%，双重难度放大。玩家伤害提升仅数值补偿，不缓解时序压力。

**C1/FE1 约束**: 评估 BPM 相关判定窗口扩展（如 BPM>160 时 Perfect 窗口 +15%）或高 BPM 房间敌人增加前摇拍数。

#### ⚠️ W13 [3e-2]: 跨平台难度发散严重

PC@60fps, BPM=180: F3 误差占半拍窗口 10%（可接受）。Android@30fps, BPM=180: 误差占 41-47%（不可玩）。

**C1 约束**: 分平台 BPM 上限——PC≤240, iOS≤200, Android≤150。FE2 须按最严格目标平台约束 BPM 递进上限。

#### ⚠️ W14 [3g]: Pillar 1/2 张力为存在性产品风险

节奏量化（攻击吸附半拍网格）可能与 Animancer 变速动画的武侠手感产生冲突。F1 设计验证题已表态（Pillar 2 优先），但技术可行性未经 M0 Spike 验证。

**M0/C1/F6 约束**: M0 Spike 必须同时验证节奏精度感知和武侠动画手感的融合。

#### ⚠️ W15 [2f-1]: F2 AC-F2-19b 依赖 F6 未存在的 ConfirmDSPStart

F2 AC-F2-19b 的 PlayMode 测试须先调用 `ConfirmDSPStart()`，此方法由 F6 调用。F6 GDD 未创建。当前可用 Mock 路径测试。

### Phase 4: 场景走查警告 (4)

#### ⚠️ W16 [S1-W2]: F3/F2 拍点查询缺乏同步接口

F3 `inputDspTime` 来自 `InputAction.performed` callback，F2 `beat_dsp_time` 来自上一次 Update 推送。时序差 ≤1帧（16.7ms@60fps）。C1 应使用 F2 的 `GetNearestBeatDspTime(dspTime)` 查询接口而非仅依赖事件流。

**C1/F2 约束**: F2 须提供查询接口；C1 须使用查询而非快照。

#### ⚠️ W17 [S1-W3]: latency_offset 双重加入风险

F3-F2 公式展开证明 offset 项代数抵消，但 C1 naive 实现可能双重加入。F3 worked example 已说明风险。

**C1 约束**: AC 须包含 "latencyOffsetMs=80 时 beat_offset_ms 仍为 0±1ms" 验证项。

#### ⚠️ W18 [S2-W1]: F6 不存在导致 F2 三步协议无实现方

F2 定义了 Initialize→Play→ConfirmDSPStart 三步启动协议，但 F6 GDD 未设计，协议无实现方。F6 设计优先级应与 C1 同级。

#### ⚠️ W19 [S3-W1]: latency offset 注入路径三 GDD 各述无唯一权威

F2 说"FE3 注入"，F2 Interactions 说"F5 Start() 读取"，F4 定义运行时 cache。三种路径在不同 GDD 各有提及但无唯一权威描述。

**F6/FE3 约束**: 统一定义注入路径（建议 F5.Load() → FE3 cache → F2.LatencyOffsetMs）。

---

## INFO Issues (15)

| # | 来源 | 说明 |
|---|------|------|
| I1 | 2a-2 | F1 下游引用在 F2/F3/F4/F5 中均有反向声明 ✅ |
| I2 | 2a-3 | F2↔F5 双向依赖已完整对齐 ✅ |
| I3 | 2b-3 | F1 Miss 惩罚规则与 F4 PerkSO OnMiss 触发器兼容 |
| I4 | 2c-4 | F4↔F5 DTO 映射表完整，命名不一致已显式标注 |
| I5 | 2d-2 | bpm_range 所有权清晰，F2 为权威 ✅ |
| I6 | 2d-3 | weapon_base_damage / enemy_attack_windup_beats 无冲突 ✅ |
| I7 | 2e-3 | F2-F5 与 F3-F2 公式代数兼容（offset 展开后抵消）✅ |
| I8 | 2f-2 | F3/F2 精度 AC 测试不同层级，不矛盾 ✅ |
| I9 | 2f-3 | F4 AC-F4-08 Odin 约束与全项目一致 ✅ |
| I10 | 3a | 进程循环分层清晰，无竞争 ✅ |
| I11 | 3c-3 | 武器平衡前瞻设计合理（perfectWindowModifier, quantizationGranularity）|
| I12 | 3c-4 | 心法绕过节奏的 3 条防护栏存在，C3 须转化为 BLOCKING AC |
| I13 | 3f | 所有系统至少服务一个支柱，Anti-Pillar 检查全部通过 ✅ |
| I14 | S4-W1 | Merit 结算公式属 C4/FE5 职责，Open Questions 已正确标记 |
| I15 | S4-W2 | F5 不保存局内状态，PerkSO 克隆体销毁无竞态风险 ✅ |

---

## Cross-System Scenario Issues

**场景走查**: 4 scenarios

| # | 场景 | 涉及系统 | 说明 |
|---|------|---------|------|
| S1 | 玩家踩拍攻击 → 节奏量化 | F3→F2→C1 | W16: 拍点查询接口缺失; W17: offset 双重加入风险 |
| S2 | 关卡初始化 → BeatClock 启动 | F4→F2→F6 | W18: F6 不存在; I: Addressables 异步加载为 F6 职责 |
| S3 | 存档加载 → 延迟校准注入 | F5→F4→F2 | W19: 注入路径无唯一权威 |
| S4 | 玩家死亡 → 功勋结算存档 | F4→F5 | I14+I15: 数据流正确，无竞态 |

---

## GDDs Flagged for Revision

| GDD | 修订项 | 类型 | 优先级 |
|-----|--------|------|--------|
| F4 (so-data-layer.md) | W4: systems-index 状态 "Approved" 与 GDD 头部 "In Review" 不一致 | 一致性 | Quick fix |

**说明**: 与 Rev.1 相比，修订标记大幅减少。F1 B1 已修复，F2 W2/W10/W13 已修复。剩余 WARNING 多属下游 GDD 职责（C1/C2/C3/FE5/F6），当前基础层 GDD 文本无需大改。

---

## Downstream GDD Constraints — 约束传递矩阵

| 下游 GDD | 须承接的发现 | 优先级 |
|----------|-------------|--------|
| **C1 (rhythm-core)** | W7 分平台判定窗口, W11 combo衰减/cap, W12 BPM窗口扩展, W13 分平台BPM上限, W16 GetNearestBeatDspTime, W17 offset双重加入AC | HIGH |
| **C2 (combo-system)** | W11 combo追赶机制 | MEDIUM |
| **C3 (perk-system)** | W9 注意力成本评级, W10 非Perfect策略价值, I12 防护栏AC | MEDIUM |
| **FE5 (meta-progression)** | B1 Merit消耗曲线 (**BLOCKER**) | HIGH |
| **FE1 (enemy-ai)** | W12 高BPM前摇拍数补偿 | MEDIUM |
| **FE2 (room-system)** | W13 按目标平台约束BPM递进 | MEDIUM |
| **F6 (TDE-integration)** | W14 Pillar1/2融合方案, W15 ConfirmDSPStart实现, W18 三步协议, W19 latency注入路径 | HIGH |

---

## Quick Fix Summary

以下 4 项 WARNING 为简单文本编辑（≤5 分钟），可在本次审查后立即修复：

| ID | GDD | 修改内容 | 预估工作量 |
|----|-----|---------|-----------|
| W3 | F3 input-event-system.md | 将 latency_offset 设计假设注释从 ±500ms 改为 "硬限 ±200ms（F2/F5）" | 30 秒 |
| W4 | systems-index.md | F4 状态更新为 "In Review" | 30 秒 |
| W6 | F1 game-concept.md | base_damage 范围从 TBD 改为 `[1.0, 100.0]（F4 WeaponSO）` | 30 秒 |
| W8 | F5 save-persistence.md | AC-F5-02 测试数据从 `sword_iron` 改为 `weapon_miaodao` | 30 秒 |

---

## Verdict

**CONCERNS** — 1 BLOCKER（Merit 无消耗汇，属下游 FE5 GDD 职责），19 WARNING（4 个为 quick fix，11 个为下游 GDD 约束传递，4 个为场景走查交互边界问题）。

与 Rev.1 相比：B1 已修复，B3/B4 降级为下游约束，3 个 Rev.1 WARNING 已修复。基础层 GDD (F2-F5) 质量显著提升。

**推荐下一步**:
1. 修复 4 个 Quick Fix（W3/W4/W6/W8）
2. 设计 F6 GDD（TDE 集成层）— 承接 4 个 WARNING
3. 设计 C1 GDD（节奏核心）— 承接 6 个 WARNING
4. 通过后运行 `/gate-check` 验证系统设计阶段门禁

<!-- CROSS-GDD-REVIEW: 2026-07-15-rev2 | GDDs reviewed: 5 | BLOCKER: 1 | WARNING: 19 | INFO: 15 | Verdict: CONCERNS -->
