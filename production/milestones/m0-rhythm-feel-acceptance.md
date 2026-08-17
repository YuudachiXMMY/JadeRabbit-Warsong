# M0 里程碑计划 — 节奏手感 PASS 标准（量化档位定案）

| 字段 | 值 |
|------|-----|
| **文档类型** | 里程碑计划 / 验收判据（Acceptance Criteria） |
| **里程碑** | M0（原型 Spike + Vertical Slice 手感验证） |
| **Status** | **Locked**（2026-08-16 定案；Spike / vertical slice 构建前须锁定） |
| **Owner** | Producer（判据） · game-designer（手感裁定） · technical-director（技术真值） |
| **来源** | `/gate-check` pre-production 2026-08-13 残留 burndown #5（PR-C2/C3）；`design/gdd/game-concept.md:310`「Spike PASS 条件将在 M0 里程碑计划文档中于 Spike 开始前定案」 |
| **触发时机** | `/vertical-slice` 构建前锁定本判据；playtest 后据本判据裁决 |

> **为什么先有本文档**：`game-concept.md:310` 与 `:426-430` 明确把量化档位（Off / HalfBeat / Beat）「哪个手感最好」及 `perfect_window_ms` / `good_window_ms` 的具体值列为 **M0 Spike 输出**，并要求 PASS 条件「在 Spike 开始前定案，不在 concept 文档预设」。本文档即这份前置判据——它把 pre-production gate 的 PR-C2 顾虑（「主观 playtest 不可行动」）转化为**可测、可裁决**的验收线，使 vertical slice 的 playtest 有明确 Go/No-Go 依据。

---

## 1. 范围与目标

**验证对象**：MVP 核心假设——「踩拍攻击 × 武侠连招 × 节奏反馈三合一，能否让玩家在 15-20 分钟内自发地『再来一局』」（`game-concept.md:320`），且**节奏层感觉像『奖励』而非『附加摩擦』**（`game-concept.md:265,288` / `systems-index.md:189`）。

**平台范围**：PC（Windows / macOS），完整手柄支持（权威范围 `game-concept.md:61`）。移动端窗口下限（iOS/Android）仅作前向记录，不纳入 M0 PASS。

**本文档定案三件事**：
1. Off / HalfBeat / Beat 三档的**候选窗口值**（Spike 起始参数）。
2. **reward-not-friction 验收线**（判定手感是否成立的操作性标准）。
3. **可测 PASS 判据 + 测试协议 + 决策输出**（Spike 跑完产出什么）。

**不在本文档范围**：Core 层实现细节（见 `design/gdd/rhythm-core.md` C1）、经济/结算（FE5）、完整构筑体验（Vertical Slice 阶段验证，`game-concept.md:322`）。

---

## 2. 候选量化档位与窗口值（Spike 起始参数）

三档语义权威源 `design/gdd/rhythm-core.md:47-49` / `docs/architecture/adr-d3-rhythm-quantization-core.md:192`。窗口创作默认值 `adr-d3:220-223`。

### 2.1 名义 BPM=120 下的窗口（PC 目标平台）

| 档位 | gridIntervalMs | Perfect 半窗（有效） | Good 半窗（有效） | Early | Late | 语义 |
|------|----------------|----------------------|-------------------|-------|------|------|
| **Off**（无障碍） | N/A | 恒 Perfect（offset 强制 0） | — | — | — | 输入即 `EmitAndClear(0)`，绕过拍点等待（`rhythm-core.md:60,111`）；**永不作默认档** |
| **HalfBeat**（默认） | 250ms | **±50ms** | **±120ms** | 120ms | 100ms | 攻击吸附最近半拍网格（`game-concept.md:359`） |
| **Beat** | 500ms | ±50ms | ±120ms | 120ms | 100ms | 全拍网格；@120BPM 有效窗**同 HalfBeat**，「更易」源于攻击密度减半 + 高 BPM 下 Beat 不触发 Good 钳窄（HalfBeat ≥124 起收窄，`rhythm-core.md:48,317`） |

> `halfBeatMs = (30 / bpm) × 1000`（120 BPM → 250ms）；`fullBeatMs = halfBeatMs × 2`；`gridIntervalMs = Beat ? fullBeatMs : halfBeatMs`（`rhythm-core.md:270-274`）。

### 2.2 窗口钳位级联（随 BPM 收窄，权威 `rhythm-core.md:277-293`）

```
GoodHalfWindowMs_effective    = min(GoodHalfWindowMs_authored=120, gridIntervalMs/2 - 1)
PerfectHalfWindowMs_effective = min(PerfectHalfWindowMs_authored=50, GoodHalfWindowMs_effective)
PerfectHalfWindowMs_effective = max(PerfectHalfWindowMs_effective, platformMinPerfectMs)
EarlyWindowMs_effective       = min(EarlyWindowMs_authored=120, GoodHalfWindowMs_effective)
不变量：Perfect_eff ≤ Good_eff ≤ gridIntervalMs/2 - 1；Early_eff ≤ Good_eff
```

- **PC platformMinPerfectMs = 16.7ms**（60fps 帧时间，`rhythm-core.md:299-303` / `adr-d3:224-226`）——Perfect 有效半窗永不低于此。
- **临界 BPM**：HalfBeat 档 BPM ≥ 124 时 Good 窗开始被 `gridIntervalMs` 钳窄（`rhythm-core.md:317`）；MVP 战斗 BPM 若跨过须在 playtest 覆盖。
- 移动端下限（前向记录，非 M0 PASS）：iOS 39ms / Android 80ms；Android BPM ≥ 186 评级退化为二元 Perfect/Miss（`rhythm-core.md:299-303,664`）。

### 2.3 评级后果（reward-not-friction 的结构性体现）

权威 `rhythm-core.md:129-131` / `entities.yaml:279`：

| 评级 | 判据 | 伤害倍率 | 连击变化 | 取消窗口（CancelTier） |
|------|------|----------|----------|------------------------|
| **Perfect** | \|offset\| ≤ Perfect 半窗 | 1.0× | +2 | Advanced（全开）+ Perfect Kinetics |
| **Good** | ≤ Good 半窗 | 0.75× | +1 | Standard |
| **Miss** | > Good 半窗 | **0.5×（攻击仍执行）** | **不增长，但不清零** | Closed |

> 「Miss 仍 50% 伤害 + 连击不归零」是**容错式设计**——踩失不重罚，这是「奖励而非摩擦」在规则层的落地（`game-concept.md:373-375`）。creative-director 已裁定「激励真空」为可接受的「宽容但有梯度」模型，与 Hi-Fi Rush 先例一致（`design/gdd/reviews/rhythm-core-review-log.md:88`）。

---

## 3. reward-not-friction 验收线

> **判据宣言**：M0 手感 PASS 当且仅当——**熟练玩家把节奏层体验为「精通的回报」，新手把它体验为「宽容的引导」，且没有任何玩家把它体验为「附加摩擦」或说出「我明明踩了那一拍，为什么是 Miss」**（失败态原话 `rhythm-core.md:33`；失信信号 `rhythm-core.md:29`「玩家不会说判定有 bug，而会说手感很差」）。

三条不可妥协的哲学锚点（转化为 §4 判据）：

1. **奖励而非摩擦**（支柱一 `game-concept.md:184-186`）：每一种强力体验都是节奏精通的回报，不存在绕过节奏的破坏性收益（反支柱 `game-concept.md:213`）。→ 判据 B 组。
2. **手感不可妥协**（支柱二 `game-concept.md:190-194`）：先保武侠动作手感，节奏反馈通过音效/特效强化，不牺牲动画时长。→ 判据 A 组 + Perfect Kinetics 体感。
3. **Off 是无障碍补偿，不是主导策略**（Off 五项合约 `rhythm-core.md:53-58`）：Off 恒 Perfect 但四重隔离（功勋 ×0.5 + CancelTier 封顶 Standard + 排行成就隔离 + perk 封顶 1.0），进阶连招深度仍须真实节奏精准度赢取（不可豁免 `game-concept.md:402`）。→ 判据 B 组。

---

## 4. M0 PASS 判据（全部满足方 PASS）

### A 组｜手感即奖励（默认档 HalfBeat 核心）

- **A1 手感评分**：测试者对默认档 HalfBeat 打 Likert 1-5 分（问「节奏层感觉像奖励(5) 还是摩擦(1)」），**中位数 ≥ 4，且无一 ≤ 2**。
- **A2 失败态零出现**：在**已完成延迟校准 + 有线耳机**的测试者中，「我明明踩了那一拍却 Miss」（`rhythm-core.md:33`）出现次数 **= 0**。
- **A3「再来一局」信号**：≥ **60%** 测试者在无外部提示下自发开始第二局（核心假设 `game-concept.md:320`）。
- **A4 武侠手感在场**：Perfect Kinetics（冻帧 2-3 帧 + 硬直 ×1.3 + 编钟共鸣 + 摄像机微切，`game-concept.md:377-384`）「打得更爽(5) / 干扰(1)」Likert **中位数 ≥ 4 且无一 ≤ 2**（可测门）。附：刀落重量 / 刀气冲击 / 连段流畅度的定性评估为 **ADVISORY**（lead sign-off；Visual/Feel 证据按 `coding-standards.md` 不单独阻塞）。

### B 组｜反支柱护栏（Off 不得主导）

- **B1 Off 合约逐项勾验**：Off 五项无障碍合约（`rhythm-core.md:53-58`）逐条可见——仅无障碍面板可开、HUD 常显无障碍标识、排行/成就隔离、功勋 ×0.5、CancelTier 恒 Standard。
- **B2 Off 不主导**：**组内对照**——同一测试者先在 HalfBeat 达 Perfect 率 ≥ 70% 的场次，再跑 Off 同房间/Boss；其 Off 档「每分钟功勋 × 0.5 折算后」**不高于自身 HalfBeat 值**。（以组内对照消除跨人群/技巧混淆；「熟练」操作化 = HalfBeat 场次 Perfect 率 ≥ 70%。）Off 不解锁 Advanced 取消。
- **B3 进阶连招是节奏回报**：Advanced 取消仅在 HalfBeat/Beat 的真实 Perfect 下获得（`game-concept.md:402` 不可豁免）。
- **B4 Off perk 封顶**：Off 下心法聚合 `perk_multiplier` **≤ 1.0**（BD-1 战力漏洞 / C1-F5c / AC-C1-26，`rhythm-core.md`）——伤害日志或代码验证；对照 HalfBeat 同心法组合 `perk_multiplier` 应可达 2.0（挂钩 §5「Off vs HalfBeat DPS」字段）。

### C 组｜无线音频条件（PR-C3，vertical slice playtest 必含）

- **C1 无线场景覆盖**：每次 PASS 判定的 playtest **至少含 1 场无线耳机 / 蓝牙条件**（真实或注入 100-300ms 延迟，`game-concept.md:294`）。
- **C2 校准 gate**（复用无障碍 A11Y-6，`design/accessibility-requirements.md:144`）：注入 **100ms** 人工延迟并完成校准后，**Perfect 命中率 ≥ 未延迟时的 80%**。
- **C3 校准不劝退**：测试者首次校准 ≤ **60s** 完成，且无人因校准体验放弃（首分钟劝退风险 `game-concept.md:290`）。超校准范围时校准界面须提示「建议有线耳机」并允许手动输入偏移（`game-concept.md:449`）。

> **C 组判定逻辑（覆盖全 100-300ms 区间）**：100ms 为**可校准区间的代理测点**（`|latency_offset|` 须 < Good 有效窗 ~120ms，`rhythm-core.md:259`）——C2 阈值仅对此区间有效。**≥ ~120ms（超校准）会话不设 Perfect 命中率阈值**，PASS 降级为 C3 兜底 UI（「建议有线耳机」+ 手动偏移出现即可测）。A2 失败态零出现**仅约束「有线 + 已校准」测试者**，无线劣化不计入 A2。建议附 1 项 200ms 观察记录（非阻塞）。

### D 组｜技术真值（判定不被帧/时钟污染）

- **D1 时间戳来源**：判定链使用 Input System `InputAction.performed` 回调时间戳，**禁** `Update()` 轮询判定（架构原则 `architecture.md:389` / `game-concept.md:295`）——代码审查确认。
- **D2 PC 精度下限**：60fps 下 Perfect 有效半窗 ≥ 16.7ms（platformMinPerfectMs，`rhythm-core.md:299-303`）。
- **D3 FMOD 异步模式**：FMOD 以 `FMOD_INIT_NORMAL` 初始化，**禁** `FMOD_INIT_SYNCHRONOUS_UPDATE`（否则同步模式下产生「零偏移」重复时间戳，`input-event-system.md:295,457`）。

---

## 5. 测试协议

| 项 | 要求 |
|----|------|
| **场次** | ≥ 3 场 playtest（gate 建议 3+，`pre-production-2026-08-13.md:148`） |
| **样本** | ≥ 5 名不同测试者，含 ≥ 2 名非节奏游戏玩家（测准入门槛 / 学习曲线风险 `game-concept.md:289`） |
| **档位覆盖** | HalfBeat（默认）**必测全程**；Beat 与 Off 各 ≥ 1 房间对照 |
| **硬件** | ≥ 1 场有线耳机基线 + ≥ 1 场无线 / 100-300ms 延迟条件 |
| **记录字段** | Likert 手感分、「再来一局」自发率、失败态原话计数、Off vs HalfBeat 每分钟功勋/DPS、校准耗时、Perfect 命中率（延迟前/后） |
| **报告落点** | `/playtest-report` → `production/qa/evidence/` |

---

## 6. 决策输出（Spike 交付物）

Spike 跑完，据 §4 判据产出以下三项，回填下游文档：

- **D-1 默认档裁定**：HalfBeat 通过 A 组 → 保持默认。若不过 → 主 fallback = 放宽 Perfect/Good 窗后复测，或提升 Beat 为默认。**Off 永不作默认**（支柱一「节奏层不可关闭」`game-concept.md:184`）。
- **D-2 锁定窗口值**：将通过的 Perfect/Good 半窗（起始候选 **±50 / ±120ms @120BPM**）回填 `game-concept.md:426-430` 的 `TBD` 占位，并与 `rhythm-core.md` 权威值 + `entities.yaml` registry 对齐。
- **D-3 Go/No-Go**：全判据 PASS → 解锁 Core（C1-C4）实现冲刺；否则列出未过判据 + 调参方向，复测后重裁。

---

## 7. 依赖与签字

**上游依赖**（须先就绪）：`/vertical-slice` 原型（含 MVP 苗刀轻/重/冲刺连招 + 1 敌人 + Perfect Kinetics 反馈 + 延迟校准雏形 + 节奏指示器 HUD）。

**下游解锁**：本判据 PASS 是 `game-concept.md:310` 量化档位定案 + `:426-430` 窗口值定案的前置，亦是 Core 层 epics/stories 规划（`/create-epics layer:core`）的手感前提。

**关联风险**：见 `production/risk-ledger.md` R-FEEL-01（节奏手感未验证）/ R-AUDIO-01（无线音频延迟）/ R-FMOD-01（FMOD 原生加载）——本判据是三者的主要缓解载体。

| 角色 | 签字 | 日期 |
|------|------|------|
| Producer（判据 owner） | ⏳ 待签 | — |
| game-designer（手感裁定） | ⏳ 待签 | — |
| technical-director（技术真值 D 组） | ⏳ 待签 | — |

<!-- M0-ACCEPTANCE: 2026-08-16 | Locked | Modes: Off/HalfBeat(默认)/Beat | Windows@120BPM: Perfect±50ms/Good±120ms | PC platformMin 16.7ms | PASS groups: A手感 / B反支柱 / C无线音频 / D技术真值 | Source: gate PR-C2/C3, game-concept:310 -->
