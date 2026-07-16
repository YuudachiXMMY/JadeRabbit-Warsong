# Cross-GDD Review Report — 2026-07-14

**Scope**: `/review-all-gdds full` — Consistency + Design Theory + Scenario Walkthrough  
**GDDs Reviewed**: F1 (game-concept), F2 (audio-beat-clock), F3 (input-event-system), F4 (so-data-layer), F5 (save-persistence)  
**Registry Baseline**: `design/registry/entities.yaml` — 4 constants registered  
**Verdict**: **CONDITIONAL PASS** — ~~5~~ 1 BLOCKER remaining (B4) / 17 WARNING / 8 INFO — B1, B2, B3, B5 已修复

---

## Fix Priority

**B5 > B1 > B3 > B4 > B2**

| Priority | ID | Reason |
|----------|----|--------|
| 1 | B5 | DTO↔SO 字段不匹配会导致存档数据丢失——实现阶段最先触发 |
| 2 | B1 | 延迟补偿规则矛盾会导致 C1 实现者无所适从 |
| 3 | B3 | F2 依赖声明错误阻塞架构初始化顺序设计 |
| 4 | B4 | Merit 无 Sink 影响经济循环但非即时实现阻塞 |
| 5 | B2 | ADR 编号冲突是文档管理问题，修复成本最低 |

---

## BLOCKER Issues (5)

### ~~B1: F2 Core Rule 7 与 F3-F2 公式矛盾 — 延迟补偿责任归属~~ ✅ FIXED

**来源**: Phase 2 (2b — Rule Contradictions)  
**受影响 GDD**: `audio-beat-clock.md` (F2), `input-event-system.md` (F3)

**矛盾描述**:
- F2 Core Rule 7: "上层系统无需自行处理延迟补偿——BeatClock 在广播 BeatEvent 时已内化 latencyOffset"
- F3 公式 F3-F2: `beatPosition = (inputDspTime + latency_offset_s - lastBeatDspTime) / beatInterval`  
  → C1（判定系统）必须手动将 `latency_offset_s` 加到 F3 的 `inputDspTime` 上，才能与 F2 的 beat 时间对齐

**影响**: C1 实现者若信任 Rule 7 不做补偿 → 判定漂移；若按 F3-F2 做补偿 → 双重补偿（BeatClock 已加一次）。

**修复建议**:
- 选项 A：F2 BeatClock 在 `BeatEvent` 中同时广播 **原始 DSP 时间** 和 **已补偿时间**，删除 Rule 7 的绝对表述，改为"BeatEvent.adjustedTime 已含补偿；与 F3 裸 DSP 时间比较时须用 BeatEvent.rawDspTime"
- 选项 B：F3 InputEvent 在发布时即加上 offset，使下游永远拿到已补偿时间 → 此时 Rule 7 成立
- 无论哪个选项，F2 和 F3 必须明确约定：**谁负责加 offset**，且只加一次

---

### ~~B2: ADR-D6 编号冲突~~ ✅ FIXED

**来源**: Phase 2 (2d — Ownership Conflicts)  
**受影响 GDD**: `audio-beat-clock.md` (F2), `save-persistence.md` (F5)

**矛盾描述**:
- F2 Dependencies: "ADR-D6 选定 FMOD Studio 2.x"（`docs/architecture/adr-d6-audio-middleware.md` 已存在）
- F5 Dependencies: "ADR-D6 将正式选定 EasySave 3 vs 自定义 JSON 方案"

两个完全不同的架构决策使用了同一个 ADR 编号。

**修复建议**: F5 的 EasySave 3 决策改为 **ADR-D7**，更新 F5 GDD 和 `technical-preferences.md` 中的引用。

---

### ~~B3: F2 依赖声明错误——声称"零依赖"但 Start() 调用 F5~~ ✅ FIXED

**来源**: Phase 4 (Scenario S2 — 首次启动初始化链)  
**受影响 GDD**: `audio-beat-clock.md` (F2), `save-persistence.md` (F5)

**矛盾描述**:
- F2 Dependencies: "零依赖——BeatClock 可独立运行"
- F5 Section 3.3: "F2 在 Start() 中调用 SaveService.LoadLatencyCalibration() 读取校准偏移量"

如果 F2 真的零依赖，那 latency offset 初始化流程未定义；如果 F5 描述为准，则 F2 必须声明对 F5 的运行时依赖，且 SaveService 必须在 F2 的 Start() 之前完成 Awake() 初始化。

**修复建议**:
- F2 Dependencies 新增: "Runtime optional dependency: F5 SaveService（latency offset 加载）"
- 补充初始化顺序: F5 SaveService.Awake() → F2 BeatClock.Start() → 调用 F5 加载校准值
- 若 F5 未就绪（首次游玩），F2 使用 offset = 0 默认值

---

### B4: Merit（功勋）无 Sink——经济循环断裂

**来源**: Phase 3 (3d — Economic Loop Analysis)  
**受影响 GDD**: `so-data-layer.md` (F4), `game-concept.md` (F1)

**问题描述**:
- F1 Pillar 4: "构筑驱动复盘——功勋是跨 Run 的唯一硬通货"
- F4 PlayerInfoSO: `playerMerit` (uint) — 功勋累积字段
- **无任何 GDD 定义功勋的消耗方式**（FE5 Meta Upgrade 系统尚未编写）

Merit 是一个只有 Source（每 Run 结算获得）没有 Sink（消耗途径）的资源。无限累积会导致：
1. 解锁所有 Meta Upgrade 后功勋变为无意义数字
2. 新手和老玩家之间功勋差距无限拉大
3. Pillar 4 "构筑驱动复盘"的正反馈循环断裂

**修复建议**:
- **非阻塞但需追踪**: 在 F4 Open Questions 中已记录 "FE5 Merit Sink 消耗表 → 等 FE5 GDD（P0 Pillar 4）"
- FE5 GDD 编写时必须定义至少 2 种 Merit Sink（推荐：Meta Upgrade 消耗 + 高阶解锁门槛）
- 考虑 Merit 上限或递减收益曲线防止无限累积

---

### ~~B5: PlayerInfoSO 字段与 PlayerProgressDTO 不匹配~~ ✅ FIXED

**来源**: Phase 4 (Scenario S5 — 存档读写往返)  
**受影响 GDD**: `so-data-layer.md` (F4), `save-persistence.md` (F5)

**矛盾描述**:

| 字段 | F4 PlayerInfoSO | F5 PlayerProgressDTO | 状态 |
|------|-----------------|----------------------|------|
| merit / playerMerit | ✅ `playerMerit` | ✅ `merit` | ⚠️ 命名不一致 |
| unlockedWeaponIds | ✅ | ✅ | ✅ 一致 |
| metaUpgradeLevels | ✅ `Dictionary<string,int>` | ❌ 缺失 | 🔴 DTO 丢失数据 |
| runCount | ❌ 缺失 | ✅ | ⚠️ SO 不持有 |
| totalPlayTimeSeconds | ❌ 缺失 | ✅ | ⚠️ SO 不持有 |
| latencyCalibrationOffsetMs | ✅ | ✅ (LatencyCalibrationDTO) | ⚠️ 双存储风险 |
| audioVolumeMaster | ✅ | ✅ (GameSettingsDTO) | ⚠️ 双存储风险 |

**关键问题**:
1. `metaUpgradeLevels` 在 F4 SO 中定义但 F5 DTO 中缺失 → 存档时数据丢失
2. `runCount` / `totalPlayTimeSeconds` 在 F5 DTO 中定义但 F4 SO 中无对应字段 → 加载后无处写入
3. `latencyCalibrationOffsetMs` 和 `audioVolumeMaster` 同时出现在 PlayerInfoSO 和独立 DTO 中 → 双存储 → 不一致风险

**修复建议**:
- F4 和 F5 共同定义一份 **字段映射表**（authoritative field mapping），明确每个字段的所有权（SO owns vs DTO owns）
- `metaUpgradeLevels` 必须加入 PlayerProgressDTO
- `latencyCalibrationOffsetMs` / `audioVolumeMaster` 选择单一存储位置（推荐：设置类字段归 Global DTO 所有，PlayerInfoSO 仅作运行时缓存并标记 `[NonSerialized]`）

---

## WARNING Issues (17)

### W1: F5 Tuning Knobs 延迟校准范围不一致
**Phase 2 (2e)** | F5 vs F2/F4/Registry  
F5 Tuning Knobs 列出 `latency_calibration_range: [-500, 500]`，但 F2/F4/Registry 均为 `[-200, 200]`。  
→ 更新 F5 Tuning Knobs 为 `[-200, 200]` 与其他文档对齐。

### ~~W2: F1 Edge Case "Miss也算Good" 与 Rule 5 矛盾~~ ✅ FIXED
**Phase 2 (2b)** | F1 内部  
Edge Case 示例声称 Miss 判定也算 Good，但 Rule 5 明确定义 Miss 为窗口外判定、不触发攻击。  
→ ~~修正 Edge Case 示例或澄清 Rule 5 的边界条件。~~ 已修正：示例改为「Miss 伤害提升至 60%」，处理规则追加 Anti-Pillar Rule 1 引用。

### W3: F3 Dependencies 未列出 F2 作为 dependent
**Phase 2 (2a)** | F3 ↔ F2  
F2 Dependencies 列出 F3 作为上游消费者，但 F3 Dependencies 未反向列出 F2。  
→ F3 Dependencies 追加 "Depended on by: F2 (BeatClock 消费 InputEvent.dspTime)"。

### W4: F4 Dependencies 未列出 F5 作为 dependent
**Phase 2 (2a)** | F4 ↔ F5  
F5 声明 SO→DTO 序列化依赖 F4 的 SO 结构，但 F4 Dependencies 未反向列出 F5。  
→ F4 Dependencies 追加 "Depended on by: F5 (SaveService 序列化 PlayerInfoSO)"。

### W5: BPM 突变时攻击动画缩放断裂
**Phase 4 (S1)** | F2 + F6/Animancer  
BPM 在 measure boundary 切换时（F2 Formula F2-F5），正在播放的攻击动画 `AnimancerState.Speed` 需要即时更新。F2 定义了 BPM 切换事件但未约定动画层监听协议。  
→ F2 新增事件 `OnBpmChanged(float oldBpm, float newBpm)` 或在 F6 GDD 中明确 Animancer 层的 BPM 订阅契约。

### W6: F3 ChargedAttack 的 chargeStartDspTime 跨 BPM 变速
**Phase 4 (S1)** | F3 + F2  
蓄力攻击开始时记录 `chargeStartDspTime`，若蓄力期间 BPM 变化，charge duration 以 beat 为单位计算时结果会偏移。  
→ F3 或 C1 需定义：蓄力时长以 DSP 秒为准还是以 beat 数为准？若按 beat 数，需在 BPM 变化时重新计算剩余 beat。

### W7: 首次启动 SaveService 返回空存档时的 SO 默认值
**Phase 4 (S2)** | F5 + F4  
首次游玩时 F5 LoadSlot 返回空/默认 DTO。F4 SO 的默认值（如 `playerMerit = 0`）是否与 F5 DTO 默认值一致未明确约定。  
→ 在 F4 或 F5 中新增一张 **默认值对照表**。

### W8: ActionMap 切换与存档 I/O 竞态
**Phase 4 (S3)** | F3 + F5  
自动存档触发时若 F3 ActionMap 正在切换（如 Gameplay→UI），存档写入可能捕获到中间状态。  
→ F5 存档触发应检查 ActionMap 是否在稳定状态，或存档数据不包含 ActionMap 瞬态。

### W9: 3 个同时活跃系统在战斗中——接近认知负荷上限
**Phase 3 (3b)** | F1 + F2 + F3  
核心战斗循环中玩家同时管理：(1) 节拍判定窗口 (2) 连招输入序列 (3) 走位/闪避。  
当前 3 个系统恰好在舒适范围上限。随 C2/C3/C4 GDD 加入，可能突破 4 个并发活跃系统。  
→ 后续 GDD 评审时持续监控，C4 战斗导演应以降低玩家认知负荷为设计目标之一。

### W10: Perfect 判定 → 资源正反馈循环无明确上限
**Phase 3 (3d)** | F1  
高技巧玩家持续 Perfect → 高连击倍率 → 更多功勋 → 更强 Meta Upgrade → 更容易 Perfect。  
正反馈循环无衰减或上限定义。  
→ C2 GDD 应定义连击倍率 cap；FE5 GDD 应定义 Meta Upgrade 边际收益递减。

### W11: 难度曲线缩放方向可能冲突
**Phase 3 (3e)** | F1 + F4  
F4 `EnemyInfoSO.attackWindupBeats` 范围 [0.5, 8.0] 用于敌人压迫感分级。若 C4 战斗导演按区域线性缩小 windup，而 C2 连招难度按区域指数增长，曲线可能在中后期交叉导致体验断崖。  
→ C4 + C2 GDD 编写时须联合定义难度缩放曲线并检查交叉点。

### W12: Pillar 4 "构筑驱动复盘" 尚无支撑系统 GDD
**Phase 3 (3f)** | F1  
四大 Pillar 中 Pillar 4 是唯一没有已编写 GDD 直接支撑的。FE5（Meta Progression）标记为 Feature Enhancement 层，尚未开始。  
→ FE5 优先级应提升，或在 C3（Perk/构筑）GDD 中提前定义 Merit 消耗接口。

### W13: F1 Anti-Pillar "NOT 绕过节奏的无敌技" 需 C3 GDD 验证
**Phase 3 (3f)** | F1  
Perk 系统（C3）可能产生绕过节拍判定的效果（如"自动 Perfect"、"免疫 Miss 惩罚"）。  
→ C3 GDD Perk 设计时必须逐一检查 Anti-Pillar 合规性。

### W14: F3 ActionMap 状态机与 UI 系统边界未定义
**Phase 2 (2c)** | F3  
F3 定义了 4 个 ActionMap（Gameplay, UI, Cutscene, Disabled）但 UI 系统 GDD 尚未编写，UI ActionMap 的输入绑定和切换条件未明确。  
→ FE2（UI 系统）GDD 编写时须与 F3 联合定义 ActionMap 切换协议。

### W15: Odin Validator 双轨方案（Inspector UX + CI 阻断）执行边界模糊
**Phase 2 (2f)** | F4  
F4 AC 中 Inspector Warning（Option A）和 CI 阻断（Option B）的选择标准未量化——哪些字段用 Warning，哪些用 Error？  
→ F4 重审时建议新增一张 Validation Severity 分类表。

### W16: F2 AC 中 "BPM 切换在 measure boundary 执行" 与实时节拍游戏预期可能冲突
**Phase 2 (2f)** | F2  
如果关卡设计要求 BPM 在 beat boundary（而非 measure boundary）切换，F2 当前 AC 会阻断该需求。  
→ 标记为 INFO 级别待 C4/Level Design GDD 确认需求后决定是否放宽。

### W17: F5 存档版本迁移策略（save_version）与 F4 SO schema 变更联动
**Phase 4 (S5)** | F4 + F5  
F4 SO 新增/删除字段时，F5 DTO 和 save_version 迁移逻辑需同步更新。当前无跨文档变更联动协议。  
→ 建议在 `design/registry/entities.yaml` 或专用文档中维护 SO↔DTO 字段版本映射。

---

## INFO Issues (8)

### I1: Entity Registry 尚未登记任何 entities / items / formulas
**Phase 1b** | Registry  
`entities.yaml` 仅有 4 个 constants，entities/items/formulas 为空。当 C2/C3 GDD 引入敌人、武器、Perk 时需及时注册。

### I2: F2 "DSP 精度 <0.1ms" 声明未附引用来源
**Phase 2** | F2  
BeatClock 的核心精度声明应附 Unity/FMOD 官方文档链接或实测数据。

### I3: 当前仅 5 个 GDD，28 个系统中 23 个尚未编写
**Phase 1a** | 全局  
Cross-GDD Review 覆盖范围有限，后续 GDD 编写后应重新运行 `/review-all-gdds`。

### I4: F4 LevelConfigSO.bpm 与 F2 BPM 范围已通过 Registry 对齐
**Phase 2 (2e)** | F2 + F4  
Registry `bpm_range [80, 240]` 两端对齐，无问题。记录为已验证一致。

### I5: F3 InputEventBus 单例与 F2 BeatClock 单例的 DontDestroyOnLoad 生命周期
**Phase 4 (S2)** | F2 + F3  
两个 MonoBehaviour 单例都使用 DontDestroyOnLoad，场景切换时须确保不重复创建。  
→ 实现时使用标准单例模式（检查 Instance != null 则 Destroy(gameObject)）。

### I6: F1 "混合量化"（Hybrid Quantization）术语在 F2/F3 中未被引用
**Phase 2 (2c)** | F1 → F2/F3  
F1 定义的核心概念 "Hybrid Quantization" 在 F2/F3 中以具体公式替代，术语本身未出现。不影响实现但可能影响团队沟通一致性。

### I7: F4 PerkSO clone 深拷贝语义与 F5 存档序列化交互
**Phase 4 (S5)** | F4 + F5  
PerkSO 使用 `Instantiate()` 深拷贝防止运行时状态污染模板。F5 存档不应序列化 clone 实例（应序列化 Perk ID + 参数）。当前 F5 未明确提及 PerkSO 序列化策略。  
→ C3 GDD 编写时须定义 Perk 存档方案。

### I8: Phase 4 场景覆盖率
**Phase 4** | 全局  
本次 walkthrough 覆盖 5 个多系统场景（BPM 切换战斗、首次启动初始化、暂停/恢复、蓄力攻击跨拍、存档往返）。更多场景（如 Perk 触发链、死亡/重生、Meta Upgrade 购买）需等 C2/C3/C4/FE5 GDD 后补充。

---

## Verdict

**CONDITIONAL PASS** — 可以继续推进下一批 GDD 编写（C1/C2），但 5 个 BLOCKER 必须在对应 GDD 进入 Approved 状态前修复：

| 阶段 | 动作 |
|------|------|
| **立即修复** | B2（ADR 编号重命名）、B3（F2 依赖声明修正） |
| **F4/F5 重审前修复** | B5（DTO↔SO 字段对齐表） |
| **C1 GDD 编写前修复** | B1（延迟补偿责任归属决策） |
| **FE5 GDD 编写时修复** | B4（Merit Sink 定义） |

---

<!-- REVIEW-META: type=cross-gdd | date=2026-07-14 | gdds=5 | blocker=5 | warning=17 | info=8 | verdict=CONDITIONAL_PASS -->
