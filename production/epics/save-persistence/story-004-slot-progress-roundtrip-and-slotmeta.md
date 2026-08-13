# Story 004: 槽位进度往返、多槽位独立与 SlotMeta 同步

> **Epic**: F5 存档/持久化系统
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: M
> **Manifest Version**: 2026-08-12
> **Last Updated**: [set by /dev-story when implementation begins]

## Context

**GDD**: `design/gdd/save-persistence.md`
**Requirement**: `TR-save-002`, `TR-save-004`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-D7: 存档/持久化中间件
**ADR Decision Summary**: 通过 `IFileStorage` 存取 DTO；`SaveSlotProgress` 顺序写入 `slot_n.es3` 与 `meta_n.es3` 两个文件，SlotMetaDTO 随进度同步更新。

**Engine**: Unity 6.3 LTS (6000.3.19f1) | **Risk**: LOW
**Engine Notes**: `DateTimeOffset.UtcNow` 生成 `lastPlayedUtcMs`（Unix 毫秒）。`totalPlayTimeSeconds` 用 `double` 避免大值精度退化（`float` 194 天后每帧增量静默归零）。无 post-cutoff API。

**Control Manifest Rules (this layer)**:
- Required: 经 `IFileStorage` 存取，不直接 `ES3.*`（ADR-D7）
- Forbidden: 序列化含 `UnityEngine.Object` 引用的对象 — DTO 纯数据（F5 Rule 6）
- Guardrail: `SaveService.Save()` 单次 < 5ms PC / < 16ms Mobile；单次存档 < 20ms PC（AC-F5-11 advisory）

---

## Acceptance Criteria

*From GDD `design/gdd/save-persistence.md`, scoped to this story:*

- [ ] **AC-F5-02**：`SaveSlotProgress(0, {merit=500, unlockedWeaponIds=["weapon_miaodao"], metaUpgradeLevels=[{id="upgrade_hp", level=2}], runCount=3})` 后 `LoadSlotProgress(0)` 返回字段与写入值位精度一致
- [ ] **AC-F5-04**：slot 0 写 `merit=100`、slot 1 写 `merit=999` → `LoadSlotProgress(0).merit==100` 且 `LoadSlotProgress(1).merit==999`（互不影响）
- [ ] **AC-F5-05**：slot 0 写 `merit=777` 后销毁重建 `SaveService` 并再次 `Initialize()` → `LoadSlotProgress(0).merit==777`（跨会话持久化）
- [ ] **AC-F5-06**：slot 1 原为 `Empty`，`SaveSlotProgress(1, dto)` 后 `GetAllSlotMeta()[1].slotState==Active`、`meritSnapshot==dto.merit`、`lastPlayedUtcMs` 落在调用前后 ±5000ms 窗口内
- [ ] `SaveSlotProgress` 顺序写入 `slot_n.es3`（PlayerProgressDTO）与 `meta_n.es3`（SlotMetaDTO）两文件
- [ ] `GetAllSlotMeta()` 返回 `SlotMetaDTO[3]`，`slotIndex` 读取后由 SaveService 按文件名注入（不持久化）
- [ ] **AC-F5-11（Advisory / Manual）**：连续 `SaveSlotProgress(0, dto)`（最大 3 武器 ID）至少 3 次预热后，第 4 次 Profiler 采样 < 20ms（PC，目标硬件手动验证，不进 CI）

---

## Implementation Notes

*Derived from ADR-D7 Implementation Guidelines:*

- `SaveSlotProgress(int slotIndex, PlayerProgressDTO dto)`：先写 `slot_n.es3`（进度），再写 `meta_n.es3`（同步构造 SlotMetaDTO：`slotState=Active`、`meritSnapshot=dto.merit`、`lastPlayedUtcMs=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()`、`totalPlayTimeSeconds=dto.totalPlayTimeSeconds`）。
- `LoadSlotProgress(int)`/`GetAllSlotMeta()` 经 `IFileStorage`；`slotIndex` 不写入文件，读取后按 `n` 注入内存 DTO。
- 内存缓存：`Initialize()` 时加载三槽位 SlotMeta 入内存，`GetAllSlotMeta()` 返回缓存快照；写入后同步更新缓存。
- 双文件非原子性写失败（第一个成功、第二个失败）→ `Error`（WriteFailed），不回滚——属 story 006 错误模型范畴，本 story 覆盖 happy path。MVP 接受 <20ms 窗口不一致（Edge Case）。
- AC-F5-11 为 advisory，非 CI 阻塞门；在 story 交付时以 Profiler 手动采样记录到 evidence 文档，不写自动化断言。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 003：`Initialize()`、GlobalData、`ISaveService` seam（本 story 依赖）
- Story 005：`GetActiveSlotIndex`/`SetActiveSlotIndex`、`DeleteSlot`
- Story 006：损坏/写失败错误模型、`OnSaveError`、双文件非原子失败转 `Error`
- Story 007：版本迁移

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

- **AC-1 (AC-F5-02)（槽位进度往返）**:
  - Given: `Idle` 状态
  - When: `SaveSlotProgress(0, {merit=500, unlockedWeaponIds=["weapon_miaodao"], metaUpgradeLevels=[{id="upgrade_hp",level=2}], runCount=3})` 后 `LoadSlotProgress(0)`
  - Then: 返回 DTO 各字段与写入位精度一致（含 `metaUpgradeLevels` 数组元素）
  - Edge cases: 空 `unlockedWeaponIds`/`metaUpgradeLevels` 往返仍为空数组；`totalPlayTimeSeconds` 大值（如 1e7）精度不丢失

- **AC-2 (AC-F5-04)（多槽位独立性）**:
  - Given: `Idle` 状态
  - When: slot 0 写 `merit=100`，slot 1 写 `merit=999`
  - Then: `LoadSlotProgress(0).merit==100` 且 `LoadSlotProgress(1).merit==999`
  - Edge cases: 写 slot 0 不改变 slot 2 的 `Empty` 状态

- **AC-3 (AC-F5-05)（跨会话持久化）**:
  - Given: slot 0 写 `merit=777`
  - When: 销毁并重建 `SaveService`（同一 `IFileStorage` 后端），再 `Initialize()`
  - Then: `LoadSlotProgress(0).merit==777`
  - Edge cases: 用真实 `ES3Storage` + 临时目录时，重建后从磁盘读回一致

- **AC-4 (AC-F5-06)（SlotMeta 随进度同步）**:
  - Given: `GetAllSlotMeta()[1].slotState==Empty`；记录调用前 `DateTimeOffset.UtcNow` 毫秒
  - When: `SaveSlotProgress(1, dto)`
  - Then: `GetAllSlotMeta()[1].slotState==Active`，`meritSnapshot==dto.merit`，`lastPlayedUtcMs ∈ [前记录-5000, 后记录+5000]`
  - Edge cases: `totalPlayTimeSeconds` 同步等于 `dto.totalPlayTimeSeconds`；`slotIndex==1` 被正确注入

- **AC-5 (AC-F5-11)（写入性能，Advisory / Manual）**:
  - Setup: 目标 PC 硬件，`PlayerProgressDTO` 含 3 个武器 ID，`Idle` 状态
  - Verify: 连续 `SaveSlotProgress(0, dto)` ≥3 次预热，Profiler 采样第 4 次耗时
  - Pass condition: 第 4 次 < 20ms；结果记录到 evidence 文档（非 CI 阻塞）

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- `Assets/Tests/EditMode/Save/SlotProgressRoundTripTests.cs` — AC-F5-02/04/06 自动化 + AC-F5-05 跨会话（重建实例），必须通过
- AC-F5-11 性能：`production/qa/evidence/slot-progress-perf-evidence.md`（手动 Profiler 采样，Advisory）

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 003（DONE）
- Unlocks: Story 005, Story 006
