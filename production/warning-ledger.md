# 残留 WARNING 跟踪台账

> **用途**：`/review-all-gdds` 复审报告是快照，会被后续复审覆盖；本台账是活账本，
> 记录跨复审仍存活、需 owner 决策但**不阻塞架构门**的 WARNING/Info 项。
> 每条记 owner / 决策点 / 状态 / 承接。清账后更新状态并注明 commit。
>
> **来源**：`design/gdd/gdd-cross-review-2026-08-12.md`（PR #29 已合入 main）
> **建账**：2026-08-12
> **最近更新**：2026-08-12

## 状态图例

- 🟢 **Resolved** — 已清账（注 commit）
- 🟡 **Owner 决策** — 无承接，需一次设计裁定
- 🔵 **已承接** — 有 ADR/里程碑/GDD 落点，按承接链推进
- ⚪ **挂里程碑** — 挂 M0/M2 试玩或验证清单

---

## A 组｜已有承接（ADR / GDD 落点）

| 编号 | 缺口 | Owner / 承接 | 决策点 | 状态 |
|---|---|---|---|---|
| **W-F** | 胜利/通关换局边界时钟 `Stop()` 归属未指派 → 胜利不 Stop 则 `global_beat_index` 不归零、下一局 boot 产生 double-Play() + 绝对拍令牌孤儿（同 SG-3） | **ADR-D13**（Accepted, commit a7154d35）+ FE5 经济 GDD | ADR-D13 已裁定统一 `OnRunEnded(outcome)` 边界；结算经济学/胜利判定 defer FE5 + game-designer | 🔵 已承接 |
| **SG-4** | Off 功勋折算「采样时刻」（伤害帧/击杀帧/结算帧）未定义 → 可全程 Off 输出、击杀前切回 HalfBeat 拿全额功勋，重开 D1 反支柱漏洞 | **FE5 / C5**（均 Not Started） | FE5 经济 GDD 创建时把采样时刻钉死，列 Blocking DoD | 🔵 已承接 |

## B 组｜无承接，需 owner 决策

| 编号 | 缺口 | 建议 owner | 决策方向 | 状态 |
|---|---|---|---|---|
| **W-1** | 音频降级 Resume 归属自相矛盾：F2 自调 `Pause()/Resume()`(`audio-beat-clock.md:58,444`) 与 F6「BeatClock 唯一调用者」(`tde-integration.md:51` Rule 4) 冲突；`OnAudioRestored` 后谁 Resume 且保证「节拍先于输入」无归属 | F6 / audio-director + technical-director | 钦定单一 Resume owner（报告建议 F6），F2 仅广播 `OnAudioRestored`；可能升小 ADR | 🟡 Owner 决策 |
| **W-2** | F3↔C2 依赖方向不对称：F3 下游表(`input-event-system.md:318`)把 C2 列为直接下游（订 `OnAttackInput`+`OnDashInput`），但 C2 上游表(`combo-system.md:86`)只列 C1/F4/ADR-D5，攻击 `AttackType` 实经 C1 `RhythmJudgmentEvent` 中转，C2 亦不消费 `DashInputEvent` | F3 owner | 修 F3 下游表 C2 行为「经 C1 间接、不消费 Dash」——纯文档修正 | 🟢 **Resolved**（本轮，见文末 commit） |
| **SG-5** | BPM `accept→boundary` 区间取消窗无 BD-2 保护：C2 在 `OnMeterChangeAccepted` 即变速（取消窗骤缩），但 max 宽容与 C1 重锚在 `OnMeterChanged` 边界才生效 → 瞬态「Perfect 却接不上连招」窗口，非零 latency 下边界最多 ±200ms 错位 | C2 / systems-designer | 评估 accept→boundary 区间是否沿用旧取消窗宽容，或引入过渡保护 | 🟡 Owner 决策 |

## C 组｜游戏设计整体性 DT-1~4（挂里程碑）

| 编号 | 缺口 | 建议 owner | 决策/验证方向 | 状态 |
|---|---|---|---|---|
| **DT-1** | combo 无 sink，触顶后永久饱和：Miss 不清零+无减项，count=148 触顶恒 ×5.0（`combo-system.md:112-114`），整局倍率与踩准脱钩，架空 game-concept 规则五 | game-designer / C2 | 评估 combo sink（Miss 缓衰减）或后移触顶点 | ⚪ 挂里程碑 |
| **DT-2** | Beat 档缺护栏对称性：Beat 档放宽判定窗却保留 CancelTier 全开 + perk 全额 + 功勋全额（天花板 250），唯一代价攻击密度减半 | game-designer | M0 Spike PASS 判据含「Beat vs HalfBeat DPS 平衡对拍」 | ⚪ 挂 M0 |
| **DT-3** | 注意力预算=4 active + 听觉双载：C1 用耳定位自己攻击网格，C4 又用同耳听敌人前摇 → 进攻量化与防御预读挤同一感官带宽 | game-designer / UX | M2 试玩专项测量；必要时防御 telegraph 迁视觉通道 | ⚪ 挂 M2 |
| **DT-4** | 高 BPM telegraph 绝对反应时间未纳入 BD-2：`attackWindupBeats[0.5,8.0]` 以拍计，Boss BPM~200 时 0.5 拍=150ms < 人类反应下限 | C4 / FE4 | 高 BPM 设绝对毫秒下限（≥250ms），或 Boss 高 BPM 阶段只用高前摇敌人 | ⚪ 挂里程碑 |

## D 组｜Info 级 / 划归创建时定义

| 编号 | 缺口 | 承接 | 状态 |
|---|---|---|---|
| **SG-1**（画面定格可见机制） | game-concept:381「画面定格」与「不冻玩家动画」措辞张力；可见机制未定义 | PL2 创建时定义（含时钟/动画关系） | 🔵 已承接（PL2 未开工） |
| **I-5** | C1 变量表 `perk_multiplier` 范围 `[1.0,2.0]`(`rhythm-core.md` F5c) 与权威 `[0.5,2.0]` 不一致（漏 0.5 下界） | 已随 PR #29 dad6c485 修（W-A 补丁 I-5） | 🟢 Resolved |
| **I-2** | C4「F2 需反向引用 C4」待办文本仍在（F2 实际早已引用 `audio-beat-clock.md:110/112/489`） | 关闭陈旧待办：核实 F2 已反向引用 C4（:110/:112/:489），combat-director :299/:323 待办(1) 标 ✅ | 🟢 Resolved（本轮） |
| **I-6** | C2 以载体类型名 `RhythmJudgmentEvent` 指代事件，未采用权威事件名 `OnRhythmJudgment`（C1-F6 已调和，无实质歧义） | combat-director 无关；combo-system 规则2(:39)+依赖表(:86) 改「订阅事件 `OnRhythmJudgment`（载荷 `RhythmJudgmentEvent`）」，对齐 rhythm-core §W-04 | 🟢 Resolved（本轮） |
| **C4 头部记账** | `combat-director.md:3` 状态「Revised — Pending Re-review」相对 SC-3 陈旧，与 systems-index「Approved」不一致 | combat-director :3 头部同步为 Approved（+ :5 Last Updated 2026-08-12），与 systems-index:33 一致 | 🟢 Resolved（本轮） |

---

## 处理路线（选定入口：建台账 + 清 W-2）

1. **第 0 步·建台账（本文件）** — 固化 9 条防丢 ✅
2. **第 1 步·清零成本文档项** — W-2（本轮）；后续可顺清 I-2 / I-6 / C4 头部
3. **第 2 步·B 组设计决策** — W-1（audio-director + TD）、SG-5（systems-designer），各起小 ADR
4. **第 3 步·挂里程碑** — DT-1~4 → M0/M2；SG-4 → FE5 DoD；W-F → ADR-D13（已 Accepted）

## 清账记录

- **2026-08-12** — 建账（本文件）；清 W-2（F3 下游表 C2 行改为「经 C1 间接、不消费 Dash」），见 commit 3b31cc23。
- **2026-08-12** — 清 D 组零成本项 I-2 / I-6 / C4 头部（第 1 步收尾）：combat-director :3/:5 头部同步 Approved+2026-08-12、:299/:323 双向引用待办(1) 标 ✅（F2 已反向引用 C4）；combo-system :39/:86 订阅点统一为「事件 `OnRhythmJudgment`（载荷 `RhythmJudgmentEvent`）」对齐 rhythm-core §W-04。见本分支 commit。
- **2026-08-12** — B 组决策稿**已起草待审批**（第 2 步进行中，尚未落 GDD）：**W-1** technical-director 稿——裁定 Resume owner=F6，F2 仅广播 `OnAudioRestored`，两步时序契约 ①`BeatClock.Resume()`→②`EnableGameplayInput()`；判定**无需新 ADR**（Rule 4/ADR-D1/D10 既有边界的措辞对齐，可挂 ADR-D1 Addendum）；开放依赖 OQ-5。**SG-5** systems-designer 稿——裁定采用 (B) 过渡保护「保护提前」：F7 的 `max(before,after)` 触发点从 `OnMeterChanged` 提前到 `OnMeterChangeAccepted`，复用既有机制；建议起小 ADR（拟 ADR-D14，跨系统事件契约需 TD 会签）。**二稿均待 owner 审批后再落 GDD 修订**。
