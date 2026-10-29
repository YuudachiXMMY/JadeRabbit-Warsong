# Traceability Index

> **Last Updated**: 2026-08-13 (Rev.12 · 定向修复 — F5 boot-gate TR 缺口：新增 TR-save-014 承接 D-4 同步 Initialize()/IsInitialized boot 就绪契约，story-003 引用 +014；TR-tde-015 consumes 由 save-010 测试 seam 改指 save-014 运行期契约)
> **Source**: `/architecture-review 补建 story-004 TR 缺口` (coverage mode)
> **TR Registry**: `docs/architecture/tr-registry.yaml` (153 entries, v4)
> **Verdict this review**: CONCERNS（健康 — 0 阻塞项）— see `architecture-review-2026-08-12-coverage.md`
>
> **coverage 增量（2026-08-12）**：上轮 v8 后新增 **ADR-D12（死亡换局，Accepted）/ D13（胜利 run-end 统一，
> Accepted，TD+game-designer 会签）/ D14（BD-2 取消窗瞬态，**Accepted 2026-08-13，TD+game-designer 会签**）**。ADR-D12+D13 闭合上轮两个审查缺口
> **SG-2（死亡边界）+ W-F（胜利边界）**——死亡/胜利经单一 F6 `EndRun(RunOutcome)` 出口统一 run-end。
> 追加 6 条 run-end/BD-2 TR（rhythm-019/director-019/tde-013/tde-014/combo-015/concept-015）。
> **~~风险~~ 解阻塞（2026-08-13）**：D14 已 Accepted → C2 BD-2 故事 Proposed 门控解除（C2 实装未开工）；GDD 文本落地（combo-system 规则12/F7/AC-C2-40 + rhythm-core 互引）为下游步骤。

---

## Coverage by System

| System | GDD | Total TRs | Covered | Partial | Gap | Notes |
|--------|-----|-----------|---------|---------|-----|-------|
| concept | game-concept.md | 15 | 6 | 4 | 5 | +concept-015 run-ends-on-death → D12/D13 Covered |
| beatclock | audio-beat-clock.md | 17 | 1 | 3 | 13 | GDD=规范；内部项非 ADR 缺口 |
| input | input-event-system.md | 15 | 2 | 0 | 13 | GDD=规范 |
| datalayer | so-data-layer.md | 12 | 11 | 1 | 0 | **D4 全覆盖** |
| save | save-persistence.md | 13 | 2 | 0 | 11 | GDD=规范 |
| tde | tde-integration.md | 15 | 10 | 1 | 4 | +tde-013 run-end 边界(D12/D13 Covered) · +tde-014 胜利事件核对(D13 Partial) · +tde-015 F5 boot gate+延迟注入(D10 Covered → story-004) |
| **rhythm (C1)** | rhythm-core.md | 19 | 17 | 2 | 0 | D3 主架构；+rhythm-019 SC-2 run-end 清空 → D12/D13 Covered |
| **combo (C2)** | combo-system.md | 15 | 13 | 2 | 0 | D3/D4/D5；+combo-015 BD-2 适应窗口 → D14 **Accepted(Covered)** |
| **perk (C3)** | perk-system.md | 13 | 12 | 1 | 0 | D4；IRandomSource/pause 源已归 ADR-D8(Accepted) — **Gap 已闭环** |
| **director (C4)** | combat-director.md | 19 | 17 | 2 | 0 | D2/D6/D10；+director-019 SC-3 令牌回收 → D12/D13 Covered |
| **Total** | | **153** | **90** | **17** | **46** | |

> Core 层"Covered"以跨切面 ADR（D2/D3/D4/D5/D6/D10）+ DI 缝隙 ADR-D8 承接为准；应用层状态机/调度器
> 逻辑以 GDD 为规范文档，不单列 ADR 缺口。各 Core 系统上轮的单一残留 Gap（A2 接口所有权）
> 已由 **ADR-D8 (Accepted, step-1 spike + CI 门控)** 归属并本轮转 Covered。Core 四系统 Gap 现为 **0**。

---

## Coverage by ADR

| ADR | Status | Covers (代表 TR) |
|-----|--------|-----------------|
| ADR-D1 (TDE Integration) | Accepted | TR-tde-001/002/003/006/007, TR-input-010/014/015, TR-datalayer-012 |
| ADR-D2 (Enemy AI) | Accepted | TR-concept-009, TR-tde-008, TR-director-001/002/010 |
| ADR-D3 (Rhythm Quantization) | Accepted | TR-concept-006/007/008, TR-rhythm-001..013, TR-combo-002 |
| ADR-D4 (SO Data Layer) | Accepted | TR-datalayer-001..012, TR-concept-012, TR-combo-001, TR-perk-001..004 |
| ADR-D5 (Animation Pipeline) | Accepted | TR-concept-004, TR-tde-004/005, TR-combo-006 |
| ADR-D6 (Audio Middleware) | Accepted | TR-concept-005, TR-beatclock-002/010, TR-director-008/009 |
| ADR-D7 (Save/Persistence) | Accepted | TR-save-005/008, TR-datalayer-009 |
| ADR-D8 (DI/Test Seams) | Accepted | TR-rhythm-015, TR-combo(count seam/AC-C2-22), TR-perk(RNG/pause/AC-C3-14), TR-director(clock mock) — 五 DI 缝隙接口 + 规范 pause/resume 源；step-1 spike CI 门控 |
| ADR-D10 (Bootstrap Sequencing) | Accepted | TR-save (Initialize), TR-beatclock-007 (LatencyOffset), TR-tde (boot order), TR-tde-015 (F5 gate+latency inject → story-004), TR-director-015 |
| ADR-D12 (Death & Run-Lifecycle) | Accepted | TR-concept-015, TR-tde-013, TR-rhythm-019, TR-director-019 — 死亡换局边界(run-ends-on-death) |
| ADR-D13 (Run-End Victory Unification) | Accepted (TD+GD 会签) | TR-tde-013/014, TR-rhythm-019, TR-director-019 — 统一 `OnRunEnded(RunOutcome)`,胜利路径对称 |
| ADR-D14 (BD-2 Cancel-Window Transient) | **Accepted** (2026-08-13, TD+GD 会签) | TR-combo-015 — 取消窗适应窗口触发点前移至 `OnMeterChangeAccepted`(C2 BD-2 故事解阻塞；GDD 文本落地为下游步骤) |
| ADR-D15 (Rhythm Judgment Window Defects) | **Proposed** (2026-10-21) | TR-rhythm-005 (偏移公式 `latency_offset_s` 双重补偿代数抵消 → SPEC-1 删除重复项)、TR-rhythm-006 (钳位级联追加 `MissHeadroomMs` 一步 → SPEC-2)、TR-rhythm-013 (`LateWindowMs_effective` 下界钳到 `GoodEff + MissHeadroomMs`)、TR-beatclock-007 (F2 Core Rule 7「C1 须额外相加」指示改写) — ⚠️ **Proposed 门控**：按 `docs/CLAUDE.md`「stories referencing a Proposed ADR are auto-blocked」，C1 生产判定代码实装被阻塞；在本 ADR 转 Accepted 前，ADR-D3 `:138`/`:145` 的既有公式仍为权威。切片实现 (`Assets/_VerticalSlice/`) 不复刻该缺陷，不受此门控约束 |

---

## Coverage by Layer

| Layer | Total TRs | Covered | Partial | Gap | Notes |
|-------|-----------|---------|---------|-----|-------|
| Foundation (F2-F6) | 72 | 26 | 5 | 41 | datalayer/tde 覆盖佳；+tde-013(D12/D13 Covered)/tde-014(D13 Partial)/tde-015(D10 Covered) |
| Core-concept | 15 | 6 | 4 | 5 | D3 覆盖 concept-006/007；+concept-015 run-ends-on-death(D12/D13) |
| **Core (C1-C4)** | 66 | 59 | 7 | 0 | 跨切面 ADR + D8 DI 缝隙 + D12/D13 run-end；+rhythm-019/director-019 Covered、combo-015 **Covered(D14 Accepted)** |
| Feature | 0 | 0 | 0 | 0 | 尚无 GDD |
| Presentation | 0 | 0 | 0 | 0 | 尚无 GDD |

---

## Gap Priority

### A2 接口所有权 — ✅ 已闭环（ADR-D8 Accepted）

| 缺口 | 涉及 TR | 状态 |
|------|---------|------|
| DI/测试缝隙接口 ADR 归属 | TR-rhythm-015, TR-combo(count seam), TR-perk(RNG/pause), TR-director(clock mock) | **✅ 已闭环**：ADR-D8 统一拥有 IBeatClock/IInputEventBus/IDSPClock(+mock)/IComboCounter/IRandomSource + 规范 pause-resume 源；CombatCore EditMode asmdef + `InternalsVisibleTo("EditModeTests")` + 纯 C# NUnit spike（step-1，`IRandomSource` 代表，5 用例）→ D8 Accepted，CI 门控跑绿 |

### 其余架构缺口（需未来 ADR 或 GDD — 均非当前实现阻塞）

| TR-ID | Requirement | Suggested Action | Priority |
|-------|-------------|------------------|----------|
| TR-concept-011 | Delay calibration system | FE3 GDD | P2 |
| TR-concept-014 | Multiple weapon types with ComboGraph | C5 GDD | P2 |
| — | PlayerInfoSO.unlockedPerkIds (C3-OQ1) | F4 schema 补充 | P2 |
| TR-tde-012 | (Post-MVP TDE 扩展) | FE1 GDD | P3 |
| TR-concept-002 | Multi-platform input support | Implementation concern, no ADR | P3 |
| TR-concept-003 | 60fps / 16.6ms frame budget | Project-wide constraint | P3 |

### Notes on beatclock/input/save gaps

beatclock(13)/input(13)/save(11) 的 37 项 Gap 为**预期**——这些系统的 GDD 即规范架构文档，
内部设计无需单独 ADR。仅 concept(5) + tde(4) = 9 条为真实架构/未来 GDD 缺口。

---

## Cross-ADR Conflicts (this review)

见 `architecture-review-2026-08-11-postD8.md` Phase 4。摘要（**0 blocking**）：
- **A1** (D1/D10 生命周期，上轮 🔴 BLOCKING) → ✅ **真实消解**：D1 裁定为订阅唯一权威
  （OnEnable/OnDisable 对称），D10 让步并将就绪顺序改为 handler 早退 guard；两 ADR 现给出逐字一致代码。
- **A2** (接口所有权) → ✅ 已由 ADR-D8 (Accepted) 归属，无重复归属/命名分歧。
- **A3** (D1 命名过时) → ✅ 消解（`.BgmFilePath` / `OnBeatExact`/`OnHalfBeatLookahead` per D8 §1）。
- **A4** (BeatClock 事件 schema) → ✅ D8 §1/§6 权威化。
- **A5** (authoredBpm 边界) → ✅ D4:165 与 D5:88 统一 [60,240]。
- 残留 LOW（非阻塞文档漂移）：D3 未同步 `OnBeatClockPaused` 规范名 + `AttackType.HeavyAttack`
  （D8 Migration §2 已登记）；E1（D2 同帧 beat 保证）仍 advisory。
- 无循环依赖，无预算冲突（合计 ≈0.73ms/帧 ≪ 16.6ms）。

---

## Trend

| Review | Date | ADRs | TRs | Covered | Partial | Gap | Coverage % |
|--------|------|------|-----|---------|---------|-----|-----------|
| v5 | 2026-07-18 | 5 | 79 | 28 | 11 | 40 | 35.4% |
| v6 | 2026-07-19 | 7 | 83 | 29 | 8 | 46 | 34.9% |
| v7 | 2026-08-11 | 8 (D1-D7, D10) | 146 | 81 | 15 | 50 | 55.5% |
| **v8 (post-D8)** | **2026-08-11** | **9 (全 Accepted)** | **146** | **85** | **15** | **46** | **58.2%** |
| **v9 (coverage)** | **2026-08-12** | **12 (D1-D8,D10,D12,D13 Accepted; D14 Proposed)** | **152** | **89** | **17** | **46** | **~58.6%** |

v8: 全部 9 ADR Accepted（D8 收口）；A1 唯一裁定消解、A2 接口所有权由 D8 归属并经 step-1 spike +
CI 门控落地；4 条 Core DI 缝隙 Gap 转 Covered（+4）。残留 Gap 主要为 Foundation GDD=规范项（37）
+ concept/tde 真实缺口（9，待未来 GDD）。**0 跨 ADR blocking conflicts，无阻塞项进入 pre-production gate。**

---

## Projected Coverage

| Milestone | ADRs Added | Projected Coverage |
|-----------|------------|-------------------|
| ~~After ADR-D8 Accepted (DI seam)~~ | ~~D8~~ | ✅ 达成 58.2% |
| After F4 schema + D1/D2/D5 修订 | — | ~62% |
| After FE3/C5 GDDs | — | ~70-75% |
| After all Feature/Presentation GDDs | All | ~85-90% |
