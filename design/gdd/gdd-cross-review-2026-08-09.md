# Cross-GDD Review Report

> **Date**: 2026-08-09
> **Skill**: `/review-all-gdds` (full — consistency + design theory + scenario walkthrough)
> **GDDs Reviewed**: 6 — F2 audio-beat-clock, F3 input-event-system, F4 so-data-layer, F5 save-persistence, F6 tde-integration, C1 rhythm-core
> **Consistency baseline**: `design/registry/entities.yaml` 5 constants — 本轮无新增冲突
> **Method**: 三个并行代理独立执行 Phase 2（一致性）/ Phase 3（设计整体性）/ Phase 4（场景走查），整合后由主线复核关键阻塞项
> **Prior review**: `design/gdd/gdd-cross-review-2026-08-08.md`（FAIL — B1/B2/D1）
> **Verdict**: **FAIL** — 阻塞项仍在。顶层阻塞（B2、`OnBeatClockResumed`）为团队**有意延后、待 technical-director ADR 裁定**状态；C1 悬空引用与 D1 为可立即行动项。

主线对 D1 做了独立文本核实（两轮设计代理判定不一致）：确认 Off 无障碍合约**仍无功勋/进度隔离**，D1 为完全未解决，非部分解决。

---

## 上次三大阻塞项回归结果

| 编号 | 上次 | 本轮 | 依据 |
|---|---|---|---|
| **B1** F2 事件缺失 | 🔴 Blocking | 🟠 **部分解决，仍 Blocking** | F2 已定义 `OnMeterChangeAccepted`/`OnMeterChanged`（负载 `MeterChangeEventData` 含 `newBpm`/`predictedEffectiveDspTimeRaw`，`audio-beat-clock.md:150-159`），F6 已反向引用（`tde-integration.md:147-148,193`）。**但**：(a) C1 仍订阅已被删除的 `OnBpmChanged`（悬空引用，`rhythm-core.md:350,388,562,666`；F2 明确「单纯 OnBpmChanged 会漏拍号-only 变更」`:72`）；(b) `OnBeatClockResumed` 仍未定义（F2 OQ-5 本轮延后 `:757`），而 C1 Rule 8 仍依赖它读取恢复后首拍（`rhythm-core.md:118-119`）。 |
| **B2** F2↔F5 初始化时序 | 🔴 Blocking | 🔴 **未解决** | F5 未新增 `OnInitialized`（全文无；Rule 14 仍靠 Bootstrap.Start 顺序 `save-persistence.md:88`）。F2 初始化章节保留错误假设「无需 SEO / 未初始化优雅返回 isCalibrated=false」（`audio-beat-clock.md:458`），与 F5 `AC-F5-21`「Initialize() 前调用只读 API 抛 `InvalidOperationException`」（`save-persistence.md:504-505`）直接矛盾。双方 OQ-4/OQ-5 明文延后，待 technical-director 裁定 D-4（Initialize 同步/异步）+ D-5（LatencyOffsetMs 写者归属）后三文档同轮修订。 |
| **D1** Off 模式进度泄漏 | 🔴 Blocking | 🔴 **未解决** | Off 无障碍合约（`rhythm-core.md:53-56`）仅三项约束：①入口限制 ②HUD 持续提示 ③排行/成就隔离——**无功勋(merit)/元进度/连招深度隔离**。E9（`:544-552`）确认 Off 恒 1.0× 伤害、+2 连击，唯一上限 ComboMax；`AC-C1-06`（`:641`）Off→`CancelTier=Advanced` 全开。`so-data-layer.md:232`（`EnemyInfoSO.rewardMeritBase`）无模式挂钩系数。反支柱「不存在绕过节奏的破坏性技能」在**进度维度**未封堵。 |

---

## 一、一致性 Issues（Phase 2）

### 🔴 Blocking

- **B1** — 见回归表。C1 悬空引用 `OnBpmChanged`（F2 已改名 `OnMeterChangeAccepted`/`OnMeterChanged`）+ `OnBeatClockResumed` 未定义。
  - **修复**：C1（及 C4）改引 F2 的具名事件；F2 补 `OnBeatClockResumed`（携恢复首拍 DSP 时间）——后者随 OQ-5 一并处理。
- **B2** — 见回归表。F2↔F5 初始化契约矛盾（延后待 ADR）。
- **B3（新）** — **F6 调用 F2 未定义的 `ScheduleBGMTransition()`**：`tde-integration.md:151` 直接调 `_beatClock.ScheduleBGMTransition(predictedEffectiveDspTimeRaw, newBgmRef)`，F2 全文无此方法且反建议走 `IAudioPlayer.PlayScheduled()`（`audio-beat-clock.md:489`）。换片 API 归属与形态两文档不一致。
  - **修复**：F2 补 `ScheduleBGMTransition` 公开契约，或 F6 改走 `IAudioPlayer.PlayScheduled()`；二选一并双向登记。

### ⚠️ Warning

| # | 问题 | 涉及文档 | 摘要 |
|---|------|---------|------|
| W1（部分） | BGM 引用类型 | F4/F2/F6 | F4/F2 已统一 FMOD string 路径（`so-data-layer.md:80,242`；`audio-beat-clock.md:114,477`）；**F6 仍用 `BgmAssetReference`(Addressables)**（`tde-integration.md:131,285`）+ `ScheduleBGMTransition(double, AssetReference)`（`:151`）。 |
| W2 | `IDSPClock` 取时方法名 | F2/F3/F6 | F2 `GetCurrentTime()`(`:55,317`)/`GetCurrentDSPTime()`(`:111`) 自身两名并存；F3/F6 用 `GetDspTime()`（`input-event-system.md:14,41`；`tde-integration.md:92-93`）。同一注入接口须统一方法名。 |
| W3 | Animancer 攻击方法/调用方 | C1/F6 | C1 `Play(attackType, beatDspTime)` 且以 C1 为调用方（`rhythm-core.md:183,576`）；F6 `PlayAttack(node, clip, onComplete)`，调用方为 **C2**（`tde-integration.md:64,132,294`）。签名与调用方均不符。 |
| W4 | `latencyCalibrationOffsetMs` 缓存写者 | F4/F2 | F4 指派 F2 写缓存（`so-data-layer.md:119,256,261`），F2 只赋值自身 `LatencyOffsetMs` 未认领（`audio-beat-clock.md:458`）。F2 OQ-4 D-5 待裁。 |
| W6 | `LookaheadMs` 双重所有权 | F2/C1 | F2 `lookaheadMs` 全局配置（`audio-beat-clock.md:511`）与 C1 `RhythmConfigSO.LookaheadMs`（`rhythm-core.md:596`）并存，无唯一真值源声明。 |
| W7（新） | C1 未消费 F4 `WeaponSO.perfectWindowModifier` | F4/C1 | F4 称「C1 计算 Perfect 窗口时与基准相乘」（`so-data-layer.md:217,346`），C1 窗口公式（`rhythm-core.md:260-320`）全程无武器修正项 → F4 字段为运行时死字段（与 D2 dead-contract 联动）。 |
| W8（新） | 延迟钳位执行方归属冲突 | C1/F3/F5 | C1 说 **FE7**（`rhythm-core.md:251`），F3/F5 说 **FE3**（`input-event-system.md:219`；`save-persistence.md:343`）。校准(FE3)与设置(FE7)为不同系统。建议统一为 FE3。 |
| W9（新） | F6 引用陈旧事件名 `OnBeat` | F6/F2 | F6 依赖表列 `OnBeat`（`tde-integration.md:283,299`），F2 无此事件（仅 `OnBeatExact`/`OnBeatLookahead`/`OnHalfBeat*`）。 |

### ℹ️ Info

- **W5 已解决**：C1/F3 已下推校准范围约束 `|latency_offset_ms| < GoodHalfWindowMs_effective(120ms)`（`rhythm-core.md:251`；`input-event-system.md:219`）。
- F2 未显式暴露公开 `DSPClock` 只读属性（F6 AC-F6-19 依赖，`tde-integration.md:99,543`）。
- Boss BPM 切换调用方 FE2（房间级）vs FE4（Boss 阶段）未交叉说明分工。
- `authoredBpm` 注册范围 [60,240] 超 F6 免限幅带 [96,160]，F6 已优雅限幅（`tde-integration.md:323`）——非硬矛盾，建议 F4 加注记。

---

## 二、游戏设计 Issues（Phase 3）

### 🔴 Blocking

- **D1** — 见回归表。Off 无障碍合约无功勋/元进度/连招深度隔离。
  - **修复**：Off 合约补第 4/5 项「进度（merit + 元解锁）隔离或折算」+「连招深度封顶」，**须先于 C5/FE5 成文锁定**（否则经济/构筑系统建在泄漏前提上）。属前提锁定型阻塞——不阻塞现有 6 GDD 架构，但阻塞下游内容 GDD。
- **3c-1（新）Off 档结构性主导策略** — Off 恒 Perfect + `CancelTier=Advanced` 全开 + 满连击满伤害，对「只想推进解锁」的玩家是零技巧零风险最优路径，直接击穿「节奏即战斗」支柱与反支柱进度维度。与 D1 同根。
  - *（注：上次 3c-1 的「Beat 严格优于 HalfBeat」部分本轮降级——`AC-C1-07a/07b`（`rhythm-core.md:643-644`）证实 Beat 的 skip-toggle 使判定窗开放频率减半，「更易命中但机会更少」属合理准度-吞吐权衡，非主导策略。）*

### ⚠️ Warning

- **D2（重新定性：dead contract，非冲突）** — F4 定义 `AttackNodeSO.quantizationHint`（`so-data-layer.md:147`）、`WeaponSO.defaultQuantizationGranularity`/`perfectWindowModifier`（`:216-217`）供 C1 参考，但 C1 全部 Rules/Formulas 对这三字段**零匹配**——定义但从未被消费的悬空契约。术语已对齐、优先级冲突不存在（下位数据根本不生效）。建议 F4 标注「Reserved / Not yet consumed by C1」+ Open Question，或 C1 显式接入 `perfectWindowModifier`。
- **D3（部分解决）** — 实现层时序原子性已解决（F2 Rule 14 保证合并变更 `OnMeterChangeAccepted`/`OnMeterChanged` 各恰 1 次、小节边界原子生效；F6 EC-03 幂等防护）。**但设计层缺口仍在**：F2/F6 全文无「渐进/斜坡/插值/smooth」，无机制限制「BPM↑ + 拍号缩短 + C1 窗口收紧 + F6 动画加速」四变量同帧同向叠加幅度。
- **D4（部分解决）** — 降级后果已妥善处理（E7 `rhythm-core.md:503-529` 承认 Android BPM≥186 评级坍缩为二元判定，属平台 DSP 能力必要降级）。**但上游预防缺失**：`input-event-system.md:186-195` Android ≤150 仅为建议文字；`so-data-layer.md` `LevelConfigSO.bpm` 的 Odin 校验仅全局 [80,240]，无平台分支——内容作者可无警告地把 Android 关卡配到 240。
- **3b-1（新）核心循环 + Boss 切换注意力预算** — 核心 30 秒循环玩家须同时管理 4-5 个主动系统（踩拍/CancelTier 连招决策/预读前摇/充能/走位），逼近认知过载上界；Boss 切换瞬间再叠加听觉重校准 + 窗口突变 + 动画速突变 + 拍号重构四通道。game-concept §Risks 已记录核心循环负载。建议 FE4 为阶段切换定义 200-500ms「过渡强调窗口」（VFX/UI 高亮 + 判定容忍临时放宽一档）摊薄冲击。

### ℹ️ Info / N/A

- **3a** 单一主资源（merit）无循环竞争 — 无问题。
- **3d 经济平衡 — N/A**：merit 唯一 Source 已定义（`rewardMeritBase` `so-data-layer.md:232`），Sink 在未成文的 FE5（F4 OQ `:595` 已记录缺口）。FE5 迟落地会放大 D1 泄漏伤害。
- **3f/3g 正面确认**：六 GDD 支柱对齐良好，玩家幻想连贯统一（C1「踩对了就该赢」+ F6「角色是由音乐驱动的武器」→ 单一身份「乐感即战斗力」）。

---

## 三、跨系统场景 Issues（Phase 4）

走查场景：5 项回归 + 3 项新发现

### 🔴 Blockers（4/5 回归项未解决）

- **场景1（Boss 边界打断充能）** — F2/F3/C1：证据加重。C1 订阅字面不存在的 `OnBpmChanged`（`rhythm-core.md:388,562,666`；F2 已改名弃用），属悬空接口引用；且 `_pendingInput=null`（`:383`）无 partial credit、无 `RhythmJudgmentEvent`，违背「按键有分量」。
- **场景2（暂停→恢复→再攻击）** — F6/F2/C1：C1 依赖 F2 已延后的 `OnBeatClockResumed`（`rhythm-core.md:118-119`；F2 OQ-5 `:757`）。（= B1 运行时体现）
- **场景3（蓝牙延迟 + 启动竞态）** — FE3/F5/F2/F3/C1：稳态计算正确，但无 `OnInitialized`、仍拉模型旧假设 → 校准可能抛异常/静默丢失 → 全 Miss。（= B2 运行时体现）
- **场景5（战斗中音频设备断开降级）** — F2/F6/F3/TDE：`tde-integration.md` 全文无 `OnAudioUnavailable` 订阅；无系统承接「暂停 TDE + 禁用 F3 输入」。F2 降级序列只暂停自身时钟（`AC-F2-16b`）。「1 帧内暂停游戏」跨系统承诺未兑现。须指派 F6/降级协调器订阅 `OnAudioUnavailable` 同帧 `TDE Pause + F3.DisableAllInput()`。

### ⚠️ Warnings

- **复活「幽灵评级」（新）** — F6/C1：C1 缓冲清空触发器只含「暂停」（Core Rule 8），States/Transitions 表不含 `PlayerDeath`/`RespawnComplete`。复活后 BeatClock 重启，残留 `_pendingInput` 可能对死前遗留输入产生评级。建议 C1 订阅 `PlayerDeath`/`RespawnComplete` 复用 Rule 8 清空方法，补 States 表 Death 触发列。
- **F4-S7 引用不存在的「F5 加载完成事件」（新，场景3 姊妹项）** — F4/F5：F4-S7 要求消费方「等待 F5 加载完成事件后再读取 `latencyCalibrationOffsetMs`/`audioVolumeMaster`」，但 F5 只有 `Initialize()` 方法无任何加载完成事件广播。下游被引导等待一个不存在的机制。建议并入 OQ-4 三文档同轮修订。

### ℹ️ Info（待成文）

- **场景6（UpdateBpm + OnEnd 同帧 → 连招双推进）** — F6 EC-03 已下推 C2 幂等契约（`_lastAdvanceFrame == Time.frameCount`），C2 未成文，保持 Info。
- **Boss BPM+拍号三系统同帧生效链** — F2/F6 已对齐，但 C1 因悬空引用实际收不到通知，C4 未成文，端到端难度协调（「窗口未收紧但令牌密度已提高」）暂不可验证。

---

## 四、GDDs Flagged for Revision

| GDD | 原因 | 类型 | 优先级 |
|-----|------|------|--------|
| **rhythm-core.md (C1)** | 悬空 `OnBpmChanged`（须改引 `OnMeterChangeAccepted`/`OnMeterChanged`）；依赖未定义 `OnBeatClockResumed`；D1 Off 进度隔离；3c-1 主导策略；W3 Animancer 方法；W6 LookaheadMs；场景1 充能丢弃；复活缓冲清空；W7 未消费 perfectWindowModifier | 一致性+设计 | **Blocking** |
| **audio-beat-clock.md (F2)** | B2 初始化（延后待 ADR）；`OnBeatClockResumed` 未定义（OQ-5 延后）；B3 `ScheduleBGMTransition` 未定义；W2 方法名；W4 缓存写者；W6；DSPClock getter | 一致性 | **Blocking** |
| **save-persistence.md (F5)** | B2 缺 `OnInitialized`（延后待 ADR）；F4-S7 引用的加载完成事件不存在 | 一致性 | **Blocking** |
| **tde-integration.md (F6)** | B3 依赖未定义 `ScheduleBGMTransition`；W1 `BgmAssetReference` 类型；W9 陈旧 `OnBeat`；W3 方法/调用方所有权；场景5 降级订阅缺位 | 一致性 | High |
| **so-data-layer.md (F4)** | W1 bgmFilePath 统一；F4-S7 不存在的 F5 事件；D2/W7 perfectWindowModifier 等三字段无消费方；W8 钳位归属；authoredBpm 限幅注记 | 一致性+设计 | Medium |
| **input-event-system.md (F3)** | W2 方法名对齐；W8 钳位归属 FE3/FE7；D4 Android BPM 上限强制点 | 一致性 | Low |

> **本轮仅将 C1（新出现、可立即修的悬空引用）在 systems-index.md 标为 Needs Revision**（用户裁定）。F2 保持「Amended — Pending Re-review」、F5 保持「Needs Revision」现状——其顶层修订项（B2、`OnBeatClockResumed`）为有意延后、待 technical-director ADR 裁定，在各自 OQ/review log 中跟进。

---

## Verdict: **FAIL**

架构生成前须解决（若只做四件）：

1. **修 C1 悬空引用**（可立即执行、与 ADR 无关）：把 C1（及 C4 成文时）的 `OnBpmChanged` 改引 F2 已定义的 `OnMeterChangeAccepted`/`OnMeterChanged`。F2 已完成自己那半，`audio-beat-clock-review-log.md:149` 已记录「C1/F6/C4 须反向引用」，F6 已跟进，C1 未跟进。
2. **B2 + `OnBeatClockResumed`**：待 technical-director 裁定 OQ-4（D-4 Initialize 同步/异步、D-5 LatencyOffsetMs 写者归属）+ OQ-5 后，F2/F5/C1 三文档同轮改为事件驱动（F5 暴露 `OnInitialized`，F2 订阅后再读校准，移除「无需 SEO」假设，F2 补 `OnBeatClockResumed`）。
3. **B3**：F2 补 `ScheduleBGMTransition` 契约或 F6 改走 `IAudioPlayer.PlayScheduled()`，二选一并双向登记；同步统一 W1 BGM 类型（F6 `BgmAssetReference` → FMOD string）。
4. **D1 / 3c-1**：Off 无障碍合约补「进度/连招深度隔离或折算」条款，**先于 C5/FE5 成文锁定**。

### Required actions before re-running `/review-all-gdds`

- **`design/gdd/rhythm-core.md`**：`OnBpmChanged` → `OnMeterChangeAccepted`/`OnMeterChanged`（含 `:350,388,562,666`）；Off 合约补进度/连招深度隔离；对齐 Animancer 方法名与调用方（W3）；LookaheadMs 从 F2 读取（W6）；场景1 充能丢弃改为 partial credit/明确判定；补 States 表 `PlayerDeath`/`RespawnComplete` 缓冲清空触发；消费或删除 F4 perfectWindowModifier 引用（W7）。
- **`design/gdd/audio-beat-clock.md`**：补 `OnBeatClockResumed` 事件（随 OQ-5）；修初始化章节改为订阅 F5 `OnInitialized`、移除「无需 SEO」（随 OQ-4）；补 `ScheduleBGMTransition` 契约或删除（B3）；统一 `IDSPClock` 方法名（W2）；补 `DSPClock` getter。
- **`design/gdd/save-persistence.md`**：新增 `OnInitialized` 事件供下游订阅（随 OQ-4）；与 F4-S7 措辞对齐（要么定义加载完成事件，要么改为同步流程描述）。
- **`design/gdd/tde-integration.md`**：F6 `BgmAssetReference` → FMOD string（W1）；`OnBeat` → `OnBeatExact`/`OnMeterChangeAccepted`（W9）；订阅 `OnAudioUnavailable` 承接降级（场景5）。
- **技术裁定前置**：technical-director 完成 OQ-4（D-4/D-5）+ OQ-5 裁定，是 B2 与 `OnBeatClockResumed` 三文档同轮修订的解锁前提。
