# Traceability Index

> **Last Updated**: 2026-08-11 (Rev.7 · A2 消解验证增量)
> **Source**: `/architecture-review full` → `/architecture-review 验证 A2 消解后`
> **TR Registry**: `docs/architecture/tr-registry.yaml` (146 entries, v2)
> **Verdict this review**: CONCERNS — see `architecture-review-2026-08-11.md`
>
> **A2 消解验证（2026-08-11）**：A2 接口所有权本体已由 **ADR-D8** 声明并归属（五 DI 缝隙接口
> + 规范 pause/resume 源 + Mock 契约；registry `architecture.yaml:297/445/574` + systems-index 依赖已登记）。
> 但 **D8 状态 `Proposed`** —— 引用 Proposed ADR 的故事自动阻塞，故 80% 单测硬门仍锁定，
> D8 → Accepted 后方转 Covered。以下矩阵**保留原 Gap 计数**（未虚增 Covered），Notes 反映归属状态。

---

## Coverage by System

| System | GDD | Total TRs | Covered | Partial | Gap | Notes |
|--------|-----|-----------|---------|---------|-----|-------|
| concept | game-concept.md | 14 | 5 | 4 | 5 | 剩余缺口待 FE3/C5 GDD |
| beatclock | audio-beat-clock.md | 17 | 1 | 3 | 13 | GDD=规范；内部项非 ADR 缺口 |
| input | input-event-system.md | 15 | 2 | 0 | 13 | GDD=规范 |
| datalayer | so-data-layer.md | 12 | 11 | 1 | 0 | **D4 全覆盖** |
| save | save-persistence.md | 13 | 2 | 0 | 11 | GDD=规范 |
| tde | tde-integration.md | 12 | 8 | 0 | 4 | D10 补齐初始化时序 |
| **rhythm (C1)** | rhythm-core.md | 18 | 15 | 2 | 1 | D3 主架构；接口所有权已归 ADR-D8(Proposed)，待 Accept |
| **combo (C2)** | combo-system.md | 14 | 11 | 2 | 1 | D3/D4/D5；IComboCounter 已归 ADR-D8(Proposed) |
| **perk (C3)** | perk-system.md | 13 | 11 | 1 | 1 | D4；IRandomSource/pause 源已归 ADR-D8(Proposed) |
| **director (C4)** | combat-director.md | 18 | 15 | 2 | 1 | D2/D6/D10；IDSPClock mock 已归 ADR-D8(Proposed) |
| **Total** | | **146** | **81** | **15** | **50** | |

> Core 层"Covered"以跨切面 ADR（D2/D3/D4/D5/D6/D10）承接为准；应用层状态机/调度器逻辑
> 以 GDD 为规范文档，不单列 ADR 缺口。各 Core 系统的单一残留 Gap 均归结为 **A2 接口所有权**
> ——已由 **ADR-D8** 声明并归属（D8 状态 `Proposed`）；D8 → Accepted 后方转 Covered、解锁 80% 单测硬门。

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
| ADR-D10 (Bootstrap Sequencing) | Accepted | TR-save (Initialize), TR-beatclock-007 (LatencyOffset), TR-tde (boot order), TR-director-015 |

---

## Coverage by Layer

| Layer | Total TRs | Covered | Partial | Gap | Notes |
|-------|-----------|---------|---------|-----|-------|
| Foundation (F2-F6) | 69 | 24 | 4 | 41 | datalayer/tde 覆盖佳；beatclock/input/save GDD=规范 |
| Core-concept | 14 | 5 | 4 | 5 | D3 覆盖 concept-006/007 |
| **Core (C1-C4)** | 63 | 52 | 7 | 4 | 跨切面 ADR 承接；4 残留 Gap = A2 接口所有权（已归 ADR-D8，待 Accept） |
| Feature | 0 | 0 | 0 | 0 | 尚无 GDD |
| Presentation | 0 | 0 | 0 | 0 | 尚无 GDD |

---

## Gap Priority

### 最高价值缺口 — A2 接口所有权（ADR-D8 已建，`Proposed`，待 Accept）

| 缺口 | 涉及 TR | 建议动作 | 优先级 |
|------|---------|---------|--------|
| DI/测试缝隙接口 ADR 归属 | TR-rhythm-015, TR-combo(count seam), TR-perk(RNG/pause), TR-director(clock mock) | **ADR-D8 已写**：统一拥有 IBeatClock/IInputEventBus/IDSPClock(+mock)/IComboCounter/IRandomSource/规范 pause-resume 事件源。**下一步：`/test-setup` 建 CombatCore EditMode asmdef + `InternalsVisibleTo` + 纯 C# NUnit spike → D8 Accept** | **P1（D8 Proposed，硬门待 Accept 解锁）** |

### 其余架构缺口（需未来 ADR 或 GDD）

| TR-ID | Requirement | Suggested Action | Priority |
|-------|-------------|------------------|----------|
| TR-concept-011 | Delay calibration system | FE3 GDD | P2 |
| TR-concept-014 | Multiple weapon types with ComboGraph | C5 GDD | P2 |
| — | PlayerInfoSO.unlockedPerkIds (C3-OQ1) | F4 schema 补充 | P2 |
| TR-concept-002 | Multi-platform input support | Implementation concern, no ADR | P3 |
| TR-concept-003 | 60fps / 16.6ms frame budget | Project-wide constraint | P3 |

### Notes on beatclock/input/save gaps

beatclock(13)/input(13)/save(11) 的 37 项 Gap 为**预期**——这些系统的 GDD 即规范架构文档，
内部设计无需单独 ADR。

---

## Cross-ADR Conflicts (this review)

见 `architecture-review-2026-08-11.md` Phase 4。摘要：A1 (D1/D10 生命周期 🔴BLOCKING)、
A2 (接口所有权 🟠 → **已由 ADR-D8 消解，D8 `Proposed` 待 Accept**)、A3 (D1 命名过时 🟠)、
A4 (BeatClock 事件 schema 无权威 🟠 → pause/resume schema 已在 F2 GDD + D8 §1/§6 锁定)、
A5 (authoredBpm 边界 🟡)、A6 (D6 文档误标 🟢)。无循环依赖，无预算冲突。

---

## Trend

| Review | Date | ADRs | TRs | Covered | Partial | Gap | Coverage % |
|--------|------|------|-----|---------|---------|-----|-----------|
| v5 | 2026-07-18 | 5 | 79 | 28 | 11 | 40 | 35.4% |
| v6 | 2026-07-19 | 7 | 83 | 29 | 8 | 46 | 34.9% |
| **v7** | **2026-08-11** | **8 (D1-D7, D10)** | **146** | **81** | **15** | **50** | **55.5%** |

v7: 首次纳入 Core 层（+63 TR）与 ADR-D10。绝对覆盖数与百分比因 D2/D3/D4/D5/D10 承接 Core 跨切面
决策而显著上升。残留 Gap 主要为 A2 接口所有权（**ADR-D8 已建、`Proposed`，待 Accept**）+ Foundation
GDD=规范项 + 未写的 Feature 层。

---

## Projected Coverage

| Milestone | ADRs Added | Projected Coverage |
|-----------|------------|-------------------|
| After ADR-D8 **Accepted** (DI seam, P1) | D8 | ~60% |
| After F4 schema + D1/D2/D5 修订 | — | ~62% |
| After FE3/C5 GDDs | — | ~70-75% |
| After all Feature/Presentation GDDs | All | ~85-90% |
