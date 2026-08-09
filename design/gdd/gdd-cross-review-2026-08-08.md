# Cross-GDD Review Report

> **Date**: 2026-08-08
> **Skill**: `/review-all-gdds` (full — consistency + design theory + scenario walkthrough)
> **GDDs Reviewed**: 6 — F2 audio-beat-clock, F3 input-event-system, C1 rhythm-core, F4 so-data-layer, F5 save-persistence, F6 tde-integration
> **Consistency baseline**: `design/registry/entities.yaml` 5 constants — PASS (prior `/consistency-check` same date)
> **Method**: two parallel review agents (Phase 2 consistency / Phase 3+4 design theory + scenario), independently executed then integrated
> **Verdict**: **FAIL** — blocking issues must be resolved before `/create-architecture`

Two independent agents each surfaced the same two top blockers (F2 missing event
definitions; F2↔F5 init race) — corroborated, high confidence. Both were manually
re-verified against source before finalizing this report.

---

## 一、Consistency Issues (Phase 2)

### 🔴 Blocking

#### B1 — F2 未正式定义 C1/F6 硬依赖的 `OnBpmChanged` 与 `OnBeatClockResumed` 事件
- **生产方 F2** (`audio-beat-clock.md`) 事件面只有 `OnBeatLookahead / OnHalfBeatLookahead / OnBeatExact / OnHalfBeatExact / OnAudioUnavailable / OnVisualMetronomeActivated`。两个 BPM/恢复事件仅在 L456 以无名措辞"F2 的 BPM 切换接受事件"提及。
- **消费方 C1** (`rhythm-core.md`) 将其作为具名接口使用：
  - `OnBeatClockResumed`（L118–119，携恢复后首拍 `beat_dsp_time`，作 EarlyWindow 重锚点）
  - `OnBpmChanged`（L350 函数入口、L388 调用时机、L666 AC-C1-20），并列入上游依赖表（L562）
- **F6** (`tde-integration.md` L147) 依赖同一 BPM 事件预加载/换片。
- **影响**：Boss 阶段 BPM 切换后 C1 无法执行 C1-F5 运行时重钳位（高 BPM 判定窗口重叠/失效）；恢复后 C1 无法正确重锚；F6 无法在小节边界换片。
- **修复**：F2 GDD 在接口面正式定义并命名两个事件 —— `OnBpmChanged`（负载：新 BPM + 生效边界）、`OnBeatClockResumed`（负载：恢复后首拍 DSP 时间）；C1/F6 统一引用同名。

#### B2 — F2↔F5 初始化时序 + 未初始化读取行为直接矛盾
- **F5 Rule 14** (`save-persistence.md` L88)：`SaveService.Awake()` **不**调用 `Initialize()`；`Initialize()` 由场景 Bootstrap 的 `Start()` 显式调用，"通过场景加载顺序或 `DefaultExecutionOrder` attribute 保证"。未初始化读取抛 `InvalidOperationException`（状态表 L114；只读 API 亦然）。
- **F2** (`audio-beat-clock.md` L425)：声称"F5 `SaveService.Awake()` → F2 `BeatClock.Start()`，Unity 保证所有 Awake 在第一个 Start 前完成，**无需 Script Execution Order 配置**"，且假设未初始化时 `LoadLatencyCalibration()` 优雅返回 `isCalibrated=false`。
- **矛盾（两处）**：
  1. "Awake 早于第一个 Start" **不**保证一个 `Start()` 早于另一个 `Start()`（F5 数据在 Bootstrap.Start 的 Initialize 后才可用）；F2 说"无需 SEO"与 F5 说"需 DefaultExecutionOrder/场景顺序"相反。
  2. F2 假设优雅返回 vs F5 明确抛异常 —— 对"未初始化时读取"的行为定义完全相反。
- **影响**：若 `BeatClock.Start()` 先于 Bootstrap.Start()，F2 读校准将抛异常或静默回退 0ms → 无线耳机玩家延迟校准失效 → 判定系统性偏移。这是 game-concept 标记的产品级技术关口。
- **修复**：统一为事件驱动 —— F5 暴露 `OnInitialized` 事件，F2 订阅后再读校准；移除 F2"Awake 排序足够/无需 SEO"的错误假设。

### ⚠️ Warning

| # | 问题 | 涉及文档 | 摘要 |
|---|------|---------|------|
| W1 | BGM 引用类型三方不一致 | F4/F6/F2 | F4 权威 `bgmFilePath: string`（StreamingAssets+FMOD Core，so-data-layer L242/L80）；F6 用 `BgmAssetReference`（Addressables，L131/L147）；F2 泛称"FMOD asset 引用"。以 F4+ADR-D6 为准统一。 |
| W2 | `IDSPClock` 取时方法名分歧 | F2/F3/F6 | F2 `GetCurrentTime()`(L286)/`GetCurrentDSPTime()`(L99)；F3/F6 `GetDspTime()`。F3 强制同一实现，须统一方法名。 |
| W3 | C1 引用的 Animancer 攻击方法与 F6 不符 | C1/F6 | C1 `Play(attackType, beatDspTime)`(L183/L576)；F6 `PlayAttack(node, clip, onComplete)` 且调用方为 C2 非 C1(L64/L294/L307)。名称/签名/调用方三重不一致。 |
| W4 | F4 指派 F2 写 `PlayerInfoSO` 缓存职责，F2 未认领 | F4/F2 | so-data-layer L119/L261 指派 F2 写 `latencyCalibrationOffsetMs`；F2 全文只赋值自身 `LatencyOffsetMs`；该缓存字段疑似无读者。 |
| W5 | 注册延迟范围超 C1 判定安全上界 | 注册表/C1 | 上游硬限 [-200,200]；C1 安全约束 `|latency_offset_ms| < GoodHalfWindowMs_effective`（默认 120ms，L251）。150–200ms 合法值会使 C1 全判 Miss。须在 FE3/FE7 约束"可用范围 ≤ min(GoodHalfWindow)"。 |
| W6 | `LookaheadMs` 双重所有权 | F2/C1 | F2 全局配置(L478) 与 C1 `RhythmConfigSO.LookaheadMs`(L596) 各存一份；发散会使 EarlyWindow 起点与实际 Lookahead 错位。F2 应为唯一真值源。 |

### ℹ️ Info
- **I1** `authoredBpm`(F4 L148) vs `AuthoredBpm`(F6/注册表) 大小写；按项目规范公开字段应 PascalCase。
- **I2** F2 未正式暴露 `DSPClock` public getter（F6 AC-F6-19 L538 依赖）。
- **I3** `ScheduleBGMTransition` 已被 F6(L151) 标为 F2 待补项（与 W1 联动）。
- **I4** 延迟更新时机措辞：F2(L98)"可随时重写" vs F3(L331)"仅 UIActive"；实际均在菜单，建议措辞对齐。

---

## 二、Game Design Issues (Phase 3)

### 🔴 Blocking

#### D1 (3c-1) — Off 无障碍模式未隔离功勋/进度，击穿反支柱护栏
Off 模式始终 Perfect（1.0× 伤害、+2 连击、CancelTier=Advanced 全开）。其无障碍合约只隔离排行榜+节奏成就，**未隔离 merit 与局外解锁进度**。玩家切 Off 即可零节奏技巧、满伤害、满连招地推进永久进度，且比 HalfBeat 更快 —— 违背反支柱"不存在绕过节奏系统的破坏性技能"（进度维度）。
- **修复**：Off 合约补第 4 项"进度/连招深度隔离或折算"，**须在 C5/FE5 GDD 成文前锁定**（否则经济系统建在泄漏前提上）。
- **性质**：C5/FE5 尚未成文，属"前提锁定"型阻塞 —— 不阻塞现有 6 GDD 的架构，但必须在下游内容 GDD 前定案。

### ⚠️ Warning
- **D2 (3c-2) 量化档位双重控制权 + Beat 主导**：C1 定义 `QuantizationMode{Off,HalfBeat,Beat}` 为玩家运行时可切换设置；F4 定义 `WeaponSO.defaultQuantizationGranularity`/`AttackNodeSO.quantizationHint{FullBeat,HalfBeat,Free}` 为内容数据。术语不对齐、冲突时优先级未定义；若各档奖励等价，Beat 更易 Perfect → 严格优于 HalfBeat。
- **D3 (3e-1) Boss 阶段多维难度尖峰叠加**：切换边界同帧发生 BPM↑ + 拍号变短 + C1 窗口收紧 + F6 动画加速，四变量同向同时跳变，无护栏。
- **D4 (3e-2) 跨平台难度天花板发散**：Android BPM≥186 时 C1 评级坍缩为 Perfect/Miss（E7），F3 建议 Android ≤150；PC 无限制。BPM 递进曲线未按平台设上限。

### ℹ️ Info / N/A
- **3a** 单一主资源（merit）无循环竞争 — 无问题。
- **3b** 核心战斗约 4 个主动系统（踩拍/取消决策/预读前摇/走位）— 已在 game-concept §Risks 记录并有缓解。
- **3d** 经济平衡 — **N/A**（faucet/sink 在未成文的 FE1/FE5/C5）。
- **3f** 支柱对齐良好，Off 例外需监控；**3g** 玩家幻想连贯统一（"兔武侠以武器演奏破阵乐"）。

---

## 三、Cross-System Scenario Issues (Phase 4)

走查场景：6

### 🔴 Blockers
- **场景2（暂停→恢复→再攻击）** — F6/F2/C1：C1 恢复重锚依赖未定义的 `OnBeatClockResumed`（= B1 运行时体现）。
- **场景3（无线耳机 latency 下踩拍 + 启动时序）** — FE3/F5/F2/F3/C1：稳态计算正确（+80ms → beat_offset +12ms），但启动竞态使校准可能抛异常/静默丢失 → 全 Miss（= B2 运行时体现）。

### ⚠️ Warnings
- **场景1（Boss 边界玩家正充能）** — F2/F3/C1：`OnBpmChanged` 触发 C1 `_pendingInput=null`（C1-F5），玩家已做的节奏承诺（含 `chargeStartDspTime`）被静默丢弃、无 `RhythmJudgmentEvent` —— 违背"按键有分量"。
- **场景4（Off 模式评级/连击/进度）** — FE7/C1：CancelTier=Advanced 恒开，零技巧解锁最强连招路径（D1 的连招深度维度补充）。
- **场景5（战斗中音频设备断开降级）** — F2/F6/F3/TDE：F2 只能暂停自身时钟并发 `OnAudioUnavailable`，但**无系统订阅它去暂停 TDE + 禁用 F3 输入**（F2 单向不调 F6；F6 只订阅 TDE 事件）。"1 帧内暂停游戏"承诺跨系统未兑现。须指派 F6/降级协调器订阅 `OnAudioUnavailable` 同帧 `TDE Pause + F3.DisableAllInput()`。

### ℹ️ Info
- **场景6（UpdateBpm + OnEnd 同帧 → 连招双推进）** — F6/C2：F6 EC-03 已识别，将幂等契约下推 C2（`_lastAdvanceFrame == Time.frameCount` 门控）；待 C2 成文时确认接受。

---

## 四、GDDs Flagged for Revision

| GDD | 原因 | 类型 | 优先级 |
|-----|------|------|--------|
| **audio-beat-clock.md (F2)** | B1 缺 `OnBpmChanged`/`OnBeatClockResumed` 事件定义；B2 初始化时序声明矛盾；W1 BGM 类型；W2 方法名；I2/I3 | 一致性 | **Blocking** |
| **save-persistence.md (F5)** | B2 初始化协议与 F2 矛盾（建议暴露 `OnInitialized`） | 一致性 | **Blocking** |
| **rhythm-core.md (C1)** | 场景2 依赖 B1；D1 Off 进度隔离；W3 Animancer 方法名；W6 LookaheadMs；场景1 充能丢弃 | 一致性+设计 | High |
| **tde-integration.md (F6)** | W1 BGM 类型；W3 方法/调用方所有权；场景5 降级订阅缺位 | 一致性 | High |
| **so-data-layer.md (F4)** | W4 指派 F2 未认领职责；D2 量化档位术语/所有权 | 一致性+设计 | Medium |
| **input-event-system.md (F3)** | W2 方法名对齐；I4 延迟更新措辞 | 一致性 | Low |

> **本次仅将 F2、F5（Blocking）在 systems-index.md 标为 Needs Revision**（用户裁定）。C1/F6 保持 Approved，其修订项在各自 review log 中跟进。

---

## Verdict: **FAIL**

架构生成前必须解决的三件事（若只做三件）：
1. **补齐 F2 事件接口面** `OnBeatClockResumed`（携恢复首拍 DSP 时间）+ `OnBpmChanged`（携新 BPM + 生效边界）—— 解 B1/场景2，连带修 C1 上游表、F6 换片。
2. **统一 F2↔F5 校准初始化为事件驱动**（F5 暴露 `OnInitialized`，F2 订阅后再读校准；移除 F2"无需 SEO"假设）—— 解 B2/场景3，这是产品级技术关口。
3. **在 Off 无障碍合约锁定"进度/连招深度隔离或折算"**（先于 C5/FE5 成文）—— 解 D1/场景4。

### Required actions before re-running `/review-all-gdds`
- `design/gdd/audio-beat-clock.md`：定义 `OnBpmChanged` / `OnBeatClockResumed` 事件（接口面 + 负载）；修正初始化章节改为订阅 F5 `OnInitialized`；统一 `IDSPClock` 方法名；明确 BGM 引用类型（对齐 F4）；补 `DSPClock` getter 与 `ScheduleBGMTransition`。
- `design/gdd/save-persistence.md`：新增 `OnInitialized` 事件到接口面，供 F2 等下游订阅；与 F2 对齐初始化协议措辞。
