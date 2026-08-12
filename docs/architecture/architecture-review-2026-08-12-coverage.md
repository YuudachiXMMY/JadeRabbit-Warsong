# Architecture Review Report — Coverage Mode

> **Mode**: `/architecture-review 验证 ADR 覆盖度` (coverage / traceability only)
> **Date**: 2026-08-12
> **Engine**: Unity 6.3 LTS (6000.3.19f1)
> **GDDs Reviewed**: 13 · **ADRs Reviewed**: 12 (D1–D8, D10, D12, D13, D14; D9/D11 号段从未创建)
> **Baseline**: Rev.9 / v8 (`architecture-review-2026-08-11-postD8.md`) — 146 TR, 85 Covered / 15 Partial / 46 Gap = 58.2%
> **Verdict**: **CONCERNS** (健康 — 0 架构阻塞项)

---

## 扫描目的

上轮 v8(post-D8)后新增了 **ADR-D12 / D13 / D14** 及一批死亡/run-end/BD-2 语义的 GDD 修订(review-all-gdds 补丁),
而 `tr-registry.yaml` 停在 **v2 / 146 条 / 2026-08-11**,未反映这些新覆盖。coverage 模式聚焦此增量:
新 ADR 覆盖了哪些 GDD 需求、哪些新需求尚未登记 TR、覆盖度如何变化。

---

## 一、三个新 ADR 的覆盖增量

| ADR | 状态 | 新覆盖的 GDD 需求 | 消解的历史审查缺口 |
|-----|------|------------------|-------------------|
| **ADR-D12** 死亡换局 | ✅ Accepted | C1 SC-2 死亡清空 · C4 SC-3 令牌回收 · F6 Rule 15 死亡边界 · run-ends-on-death 结算保证 | cross-review 2026-08-11b **SG-2**(死亡边界) |
| **ADR-D13** 胜利 run-end | ✅ Accepted(TD+game-designer 会签) | 泛化统一 `OnRunEnded(RunOutcome)`;C1/C4/F6 胜利路径对称清理;胜利 double-`Play()` 闭合 | cross-review 2026-08-12 **W-F**(胜利边界) |
| **ADR-D14** BD-2 取消窗瞬态 | ⚠️ **Proposed** | C2 规则12/F7 取消窗适应窗口(`max(before,after)` 触发点前移到 `OnMeterChangeAccepted`)/ AC-C2-40~42 | warning-ledger **SG-5**(承接中) |

> **风险**:ADR-D14 仍 `Proposed` → 引用它的 C2 故事被自动阻塞(`docs/CLAUDE.md`「stories referencing a Proposed ADR are auto-blocked」)。
> C2 实装尚未开工,故不阻塞当前 C1 冲刺;但 C2 进入实现前 D14 须转 `Accepted`。

---

## 二、注册表增量 — 6 条新 TR(本次追加)

GDD 已落地这些需求,但 v2 注册表缺失对应条目。本次 append 6 条(仅新增,不改任何现有 ID),version 2→3:

| TR-ID | 需求摘要 | 来源锚点 | 覆盖 ADR | 判定 |
|-------|---------|---------|---------|------|
| **TR-rhythm-019** | C1 订阅 `OnRunEnded(outcome)`,run-end(Death+Victory)同帧清空 `_pendingInput`/`_lateWindowActive`,防跨边界幽灵评级 | rhythm-core E10/SC-2, AC-C1-22e | D12+D13 | ✅ Covered |
| **TR-director-019** | C4 订阅 `OnRunEnded(outcome)`,run-end 同帧取消全部 Pending/Granted 令牌+清 queue+释放拍槽(绝对拍跨 `Stop()` 失锚防护) | combat-director Rule 12/E16, AC-C4-31 | D12+D13 | ✅ Covered |
| **TR-tde-013** | F6 单一 run-end 入口 `EndRun(RunOutcome)` + 每局一次性闭锁 `_runEnded`;同帧 `Stop()`+`DisableAllInput()`+广播单一域事件;Death/Victory 两路径汇入 | tde-integration Rule 15, AC-F6-25 | D12+D13 | ✅ Covered |
| **TR-tde-014** | F6 胜利/关卡完成事件桥接 —— TDE `TopDownEventTypes` 成员名须核对引擎参考(实现前硬门) | tde-integration Rule 15 Victory 分支;D13 VR1 | D13(部分) | ⚠️ Partial |
| **TR-combo-015** | C2 BD-2 取消窗适应窗口:窗内有效毫秒宽度取 `max(cancelWindowMs(before), cancelWindowMs(after))`,头端前移至 accept | combo-system 规则12/F7, AC-C2-40~42 | D14(Proposed) | ⚠️ Partial |
| **TR-concept-015** | run-ends-on-death/victory:无同局复活 + 死亡/胜利均即时结算(merit 唯一沉淀点不被绕过) | game-concept :145/:164, save-persistence :316 | D12 | ✅ Covered |

> **ID 边界说明**:TR-rhythm-010(暂停清空)、TR-combo-011(规则6 Animancer 变速)是**相邻但语义不同**的既有需求;
> run-end 清空(切断节奏承诺连续性 + `Stop()` 归零)与 BD-2 适应窗口(`max` 宽容期)均为新语义,不复用其 ID。

---

## 三、更新后覆盖概览

| 指标 | 上轮 v8 | 本轮 v9(coverage,+6 TR) |
|------|---------|--------------------------|
| Total TR | 146 | **152** |
| Covered | 85 | **89**(+4:rhythm-019 / director-019 / tde-013 / concept-015) |
| Partial | 15 | **17**(+2:tde-014 胜利事件核对 / combo-015 D14-Proposed) |
| Gap | 46 | 46 |
| Coverage % | 58.2% | **~58.6%** |

百分比基本持平,但实质意义在于:**上轮两个最痛的审查缺口(SG-2 死亡边界 + W-F 胜利边界)已由 Accepted ADR(D12/D13)干净闭合**,
死亡/run-end 生命周期从「审查开口」转为「已覆盖 + 已登记」。

---

## 四、残留缺口(需未来 ADR / GDD,均非当前架构阻塞)

### 可行动的覆盖风险
- **ADR-D14 → Accepted**:唯一可行动项。C2 BD-2 故事在 D14 Accepted 前自动阻塞(C2 实装未开工,不阻塞 C1 冲刺)。
- **TDE 胜利事件成员名核对(D13 VR1 实现前硬门)**:F6/FE5 实装前须对齐 `docs/engine-reference/unity/plugins/top-down-engine.md`,
  回填 `entities.yaml pre_implementation_checks`。参照 ADR-D1 `.Respawn`→`.RespawnComplete` 教训,成员名不得凭训练数据臆断。

### FE5 承接项(D12/D13 Open Follow-up;无 GDD,故非缺口计数)
- 结算经济学(胜利 vs 死亡 merit 折算差异 —— game-designer 会签要求「显式设计,不得默认对称」)。
- `RunOutcome` 枚举完整取值(Death / Victory / Retreat / Quit?)—— 类型归属与无 TDE 依赖已由 D13-1 锁定。
- 防退出套利边界(胜利前中途退出 / 强杀进程规避不利死亡结算)—— FE5 Edge Cases。
- 胜利判定语义 + 唯一 fire owner(FE4 Boss 击败 / FE2 房间清场)—— 杜绝两处重复触发胜利(D13-5 闭锁为兜底,owner 唯一化为正解)。

### 上轮既有真实缺口(不变)
- TR-concept-011 延迟校准 → FE3 GDD(P2)
- TR-concept-014 多武器 ComboGraph → C5 GDD(P2)
- PlayerInfoSO.unlockedPerkIds(C3-OQ1)→ F4 schema(P2)
- beatclock(13)/input(13)/save(11)= 37 项:GDD 即规范架构文档,内部设计无需单独 ADR(**预期缺口**)

---

## 五、ADR 依赖有序性(coverage 视角快照)

- **ADR-D12** 依赖 D1 + D10(均 Accepted)✅
- **ADR-D13** 依赖 D12 + D1 + D10(均 Accepted);Extends(非 Supersede)D12 ✅
- **ADR-D14** 依赖 D3 + D8(均 Accepted)✅ —— 依赖链干净,但 **D14 自身 Proposed**,故其下游(C2 BD-2 故事)受 Proposed 门控阻塞。
- 无依赖环;D9/D11 号段从未创建(编号跳空,非悬空依赖)。

---

## 裁决:CONCERNS(健康 — 0 架构阻塞项)

死亡/run-end 生命周期(上轮最大审查缺口 SG-2 + W-F)已由 ADR-D12 + ADR-D13(均 Accepted,D13 联合签字)闭合。
两个非阻塞待办:① 注册表已本轮追加 6 条 TR 反映 run-end/BD-2 覆盖;② ADR-D14 仍 Proposed —— 不阻塞当前 C1 冲刺,
但 C2 实装前须转 Accepted。无跨 ADR 阻塞冲突,无阻塞项进入 pre-production gate。

### Required ADRs(优先级)
1. **ADR-D14 → Accepted**(BD-2 取消窗瞬态,C2 实装前门)—— 已 Proposed,待会签。
2. 无新 Foundation/Core 层 ADR 缺口 —— run-end 生命周期已由 D12/D13 承接。

---

## Trend

| Review | Date | ADRs | TRs | Covered | Partial | Gap | Coverage % |
|--------|------|------|-----|---------|---------|-----|-----------|
| v7 | 2026-08-11 | 8 (D1-D7, D10) | 146 | 81 | 15 | 50 | 55.5% |
| v8 (post-D8) | 2026-08-11 | 9 (全 Accepted) | 146 | 85 | 15 | 46 | 58.2% |
| **v9 (coverage)** | **2026-08-12** | **12 (D1-D8,D10,D12,D13 Accepted; D14 Proposed)** | **152** | **89** | **17** | **46** | **~58.6%** |

v9:ADR-D12/D13 Accepted 闭合死亡+胜利 run-end 边界(SG-2/W-F);D14 Proposed 承接 BD-2 瞬态(SG-5,待会签)。
追加 6 条 run-end/BD-2 TR。**0 跨 ADR blocking conflicts,无阻塞项进入 pre-production gate。**
