# Story 007: 存档版本迁移（低版本迁移 + 高版本阻断）

> **Epic**: F5 存档/持久化系统
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: [fill before sprint planning]
> **Manifest Version**: 2026-08-12
> **Last Updated**: [set by /dev-story when implementation begins]

## Context

**GDD**: `design/gdd/save-persistence.md`
**Requirement**: `TR-save-007`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-D7: 存档/持久化中间件
**ADR Decision Summary**: 版本迁移写入 DTO 的 `save_version: int` 字段，由 `SaveService.MigrateIfNeeded<T>()` 处理；**不使用** EasySave 3 自带版本机制（与 `IFileStorage` 解耦）。

**Engine**: Unity 6.3 LTS (6000.3.19f1) | **Risk**: LOW
**Engine Notes**: 迁移逻辑为纯 C#，与存储后端无关；不依赖 ES3 版本 API。无 post-cutoff API。

**Control Manifest Rules (this layer)**:
- Required: 版本迁移写入 DTO 字段 `save_version:int`，由 `SaveService.MigrateIfNeeded<T>()` 处理；**不用** ES3 版本机制（ADR-D7）
- Required: 经 `IFileStorage` 存取，不直接 `ES3.*`（ADR-D7）
- Guardrail: 各 DTO `CurrentVersion` 只能向上递增，各自独立演化（Tuning Knobs）

---

## Acceptance Criteria

*From GDD `design/gdd/save-persistence.md`, scoped to this story:*

- [ ] `SaveService.MigrateIfNeeded<T>()` 检测加载文件的 `save_version`，低于 `T.CurrentVersion` 时执行链式迁移函数 `MigrateV{old}To{new}`，迁移后以新版本格式覆盖写入
- [ ] **AC-F5-08（低版本迁移）**：注入 `save_version=0` 的 `slot_0.es3`（含 `merit=250, runCount=5`），`Initialize()` 后 —
  - `LoadSlotProgress(0).merit==250`、`runCount==5`（已知字段被正确迁移，非默认值）
  - `LoadSlotProgress(0).save_version == PlayerProgressDTO.CurrentVersion`（升级至当前版本）
  - `MockFileStorage.SaveCallCount == 1`（迁移触发恰好一次写回）
  - `MockFileStorage.LoadCallCount >= 1`
  - `state == Idle`（迁移成功不触发 Error）
- [ ] **AC-F5-09（高版本阻断）**：注入 `save_version=99` 的存档，`Initialize()` 后 —
  - `OnSaveError` 触发（`SaveErrorType.VersionTooHigh`，`DetectedVersion==99`）
  - `MockFileStorage.SaveCallCount == 0`（`IFileStorage.Save` 未被调用，文件不覆盖不删除）
  - `state == Error`
  - `AcknowledgeError()` 后 `LoadSlotProgress(0)` 返回默认 `PlayerProgressDTO`（`merit=0`）
- [ ] 迁移函数为纯函数（无副作用）；迁移失败按损坏处理（转 Corrupted 分支）

---

## Implementation Notes

*Derived from ADR-D7 Implementation Guidelines:*

- V0 定义（AC-F5-08 注）：文件存在但 `save_version` 缺失或值为 0，是合法历史降级场景。`MigrateV0To1` 保留已知字段（`merit`/`runCount` 等），新增字段填默认。
- `MigrateIfNeeded<T>()` 泛型化：读取后比对 `dto.save_version` 与 `T.CurrentVersion`；`<` 链式迁移（V0→V1→V2…，逐级调用），迁移完成后 `SaveService` 覆盖写回（这是 AC-F5-08 `SaveCallCount==1` 的来源）。
- 高版本（`>`）：不加载数据、不覆盖文件（`SaveCallCount==0`）、`OnSaveError(VersionTooHigh, DetectedVersion=n)`、进 `Error`（状态表 :123）。`AcknowledgeError()` 后缓存重置为默认（复用 story 006 的 Corrupted/VersionTooHigh 分叉）。
- 迁移是纯函数，无副作用；迁移抛异常 → 视为损坏（走 story 006 Corrupted 路径）。
- MVP 各 DTO `CurrentVersion==1`，实际迁移路径仅 V0→V1；框架须支持未来链式扩展。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 006：`OnSaveError`/`SaveErrorType`/`Error` 状态机与 `AcknowledgeError()` 缓存分叉（本 story 复用，仅新增 `VersionTooHigh` 触发点）
- Story 001：DTO `CurrentVersion`/`save_version` 字段定义（本 story 依赖其存在）
- Post-MVP 跳版本部分加载（Open Question 5，不实现）

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

- **AC-1 (AC-F5-08)（低版本迁移）**:
  - Given: `MockFileStorage` 注入 `save_version=0` 的 `slot_0.es3`（`merit=250, runCount=5`）
  - When: `Initialize()`
  - Then: `LoadSlotProgress(0).merit==250` 且 `runCount==5`；`save_version==PlayerProgressDTO.CurrentVersion`；`SaveCallCount==1`；`LoadCallCount>=1`；`state==Idle`
  - Edge cases: V0 缺失的新字段填默认值（如 `metaUpgradeLevels` 为空数组）；迁移后再次 `Initialize()` 不重复写回（已是 V1）

- **AC-2 (AC-F5-09)（高版本阻断）**:
  - Given: `MockFileStorage` 注入 `save_version=99` 的 `slot_0.es3`
  - When: `Initialize()`
  - Then: `OnSaveError(VersionTooHigh, DetectedVersion=99)`；`SaveCallCount==0`；`state==Error`；`AcknowledgeError()` 后 `LoadSlotProgress(0).merit==0`（默认）
  - Edge cases: 文件在高版本阻断后仍存在于磁盘（不删除不覆盖）

- **AC-3（迁移纯函数 / 失败按损坏处理）**:
  - Given: 构造迁移函数中会抛异常的畸形 V0 数据
  - When: `Initialize()`
  - Then: 走 Corrupted 分支（`OnSaveError(Corrupted)`，`.corrupt` 备份），不产生半迁移状态
  - Edge cases: 迁移函数不修改输入、不触碰无关文件

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- `Assets/Tests/EditMode/Save/VersionMigrationTests.cs` — AC-F5-08/09 + 迁移纯函数/失败降级，必须通过

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 006（DONE）
- Unlocks: None
