# Story 006: 两级错误模型与 OnSaveError 事件契约

> **Epic**: F5 存档/持久化系统
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: 2026-08-12
> **Last Updated**: [set by /dev-story when implementation begins]

## Context

**GDD**: `design/gdd/save-persistence.md`
**Requirement**: `TR-save-011`, `TR-save-012`, `TR-save-013`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-D7: 存档/持久化中间件
**ADR Decision Summary**: 通过 `IFileStorage` 捕获存储层异常并映射为 `SaveErrorType`；损坏文件用 native `ES3.RenameFile()` 重命名为 `.corrupt` 备份，不自动删除。

**Engine**: Unity 6.3 LTS (6000.3.19f1) | **Risk**: LOW
**Engine Notes**: `.corrupt` 备份重命名走 `IFileStorage.RenameFile`（生产实现调 native `ES3.RenameFile()` ES3.cs:1142）。无 post-cutoff API。

**Control Manifest Rules (this layer)**:
- Required: 损坏文件用 native `ES3.RenameFile()` 重命名，不手写三步 I/O（ADR-D7）
- Required: 经 `IFileStorage` 存取，不直接 `ES3.*`（ADR-D7）
- Forbidden: 存档层使用 System.Text.Json / Newtonsoft.Json（ADR-D7）

---

## Acceptance Criteria

*From GDD `design/gdd/save-persistence.md`, scoped to this story:*

- [ ] 定义 `event Action<SaveErrorData> OnSaveError`、`enum SaveErrorType {Corrupted, WriteFailed, VersionTooHigh, DirectoryFailed}`、`readonly struct SaveErrorData {ErrorType, SlotIndex, DetectedVersion, IsRetryable}`
- [ ] **AC-F5-07（槽位级 → Idle）**：`slot_0.es3` 解析失败时 `Initialize()` 后 —
  - `state==Idle`（单槽位损坏不进系统级 Error）
  - `OnSaveError` 触发，`ErrorType==Corrupted`、`SlotIndex==0`
  - `slot_0.es3.corrupt` 存在，`slot_0.es3` 原文件不存在
  - `GetAllSlotMeta()[0].slotState==Corrupted`；slot 1/2 正常；`LoadSlotProgress(1/2)` 正常返回
- [ ] **AC-F5-23（系统级 → Error）**：`global/settings.es3` 抛 `FormatException` 时 —
  - `state==Error`；`OnSaveError` 触发，`ErrorType==Corrupted`、`SlotIndex==-1`
  - `AcknowledgeError()` 后 `state==Idle`，`LoadSettings()` 返回默认值 `{1.0f,0.8f,1.0f}`
  - `settings.es3.corrupt` 备份存在
- [ ] **AC-F5-14（写入失败）**：ES3 写入抛 `IOException` 时 `SaveSlotProgress(0, dto)` → `OnSaveError` 触发、`state==Error`；`AcknowledgeError()` 后回 `Idle`；内存缓存**保留**上次写入值（不清除）
- [ ] **AC-F5-20（Error 态 Write 抛异常）**：`Error` 状态下调用任意写入方法抛 `InvalidOperationException`（消息含 "Error state" 或等效）；`SaveCallCount` 不增；`state` 保持 `Error`
- [ ] **AC-F5-22（AcknowledgeError 幂等）**：`Idle` 状态下 `AcknowledgeError()` 无异常，`state` 保持 `Idle`
- [ ] `AcknowledgeError()` 后置行为按来源分叉：WriteFailed → 缓存**保留**；Corrupted/DirectoryFailed/VersionTooHigh → 缓存**重置为默认 DTO**

---

## Implementation Notes

*Derived from ADR-D7 Implementation Guidelines + GDD 两级错误模型:*

- **两级模型**：槽位级故障（单槽位损坏）→ 保持 `Idle`，损坏槽位 `slotState=Corrupted`，`OnSaveError(Corrupted, SlotIndex=n)`，其他槽位/全局正常。系统级故障（GlobalData 损坏 / 目录创建失败 / 高版本）→ `Error` 状态，阻断所有写入。
- 损坏处理：捕获 `IFileStorage.Load` 抛出的 `FormatException`（Mock）/ ES3 解析异常（生产），`RenameFile(x.es3, x.es3.corrupt)` 备份原文件（不删除，供诊断）。
- `SaveErrorData.IsRetryable`：`WriteFailed=true`，其余 `false`。`DetectedVersion` 仅 `VersionTooHigh` 携带（story 007 触发）。
- `AcknowledgeError()` 分叉（状态表 :127-128）：`WriteFailed` 时内存缓存保留（写失败≠数据损坏，缓存仍可信）；`Corrupted/DirectoryFailed/VersionTooHigh` 时缓存重置为默认 DTO（磁盘不可信，从安全基线重启）。
- `Error` 态任意 Write → `InvalidOperationException`，不触碰 `IFileStorage.Save`（状态表 :129）。
- 双文件非原子失败（story 004 提及）：`slot_n.es3` 成功、`meta_n.es3` 失败 → `Error`（WriteFailed），不回滚。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 003/004：happy-path 加载与往返（本 story 在其上叠加错误分支）
- Story 007：`VersionTooHigh` 的迁移/高版本检测触发（本 story 定义 `SaveErrorType.VersionTooHigh` 枚举与 Error 转换，检测逻辑在 007）
- Story 008：主线程约束错误（独立于两级 I/O 错误模型）
- P7/HUD 错误 UI 展示（`WriteFailed` 非模态通知等，非 F5 范畴）

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

- **AC-1 (AC-F5-07)（槽位级损坏 → Idle）**:
  - Given: `MockFileStorage` 令 `slot_0.es3` 的 `Load<T>` 抛 `FormatException`
  - When: `Initialize()`
  - Then: `state==Idle`；`OnSaveError(Corrupted, SlotIndex=0)` 触发；`slot_0.es3.corrupt` 存在且 `slot_0.es3` 不存在；`GetAllSlotMeta()[0].slotState==Corrupted`；`LoadSlotProgress(1/2)` 正常
  - Edge cases: 多个槽位同时损坏各自独立标 `Corrupted`，仍不进 `Error`

- **AC-2 (AC-F5-23)（GlobalData 损坏 → Error）**:
  - Given: `MockFileStorage` 令 `global/settings.es3` 的 `Load<T>` 抛 `FormatException`
  - When: `Initialize()`
  - Then: `state==Error`；`OnSaveError(Corrupted, SlotIndex=-1)`；`settings.es3.corrupt` 存在；`AcknowledgeError()` 后 `state==Idle` 且 `LoadSettings()` 返回 `{1.0f,0.8f,1.0f}`
  - Edge cases: 目录创建失败 → `OnSaveError(DirectoryFailed)`，同为系统级 Error

- **AC-3 (AC-F5-14)（写入失败保留缓存）**:
  - Given: 先成功 `SaveSlotProgress(0, {merit=300})`；再令 `IFileStorage.Save` 抛 `IOException`
  - When: `SaveSlotProgress(0, {merit=999})`
  - Then: `OnSaveError(WriteFailed, IsRetryable=true)` 触发，`state==Error`；`AcknowledgeError()` 后 `Idle`，`LoadSlotProgress(0).merit==300`（上次成功值保留，未被清除）
  - Edge cases: `AcknowledgeError()` 对 WriteFailed 不重置缓存

- **AC-4 (AC-F5-20)（Error 态 Write 抛异常）**:
  - Given: 通过 mock 触发 `Error` 状态
  - When: `SaveSlotProgress(0, dto)`
  - Then: 抛 `InvalidOperationException`（含 "Error state" 或等效）；`SaveCallCount` 不增；`state==Error`
  - Edge cases: 其他写入方法（`SaveSettings` 等）在 `Error` 态同样抛异常

- **AC-5 (AC-F5-22)（AcknowledgeError 幂等）**:
  - Given: `Idle` 状态（非 Error）
  - When: `AcknowledgeError()`
  - Then: 无异常，`state==Idle`
  - Edge cases: 连续多次 `AcknowledgeError()` 在 `Idle` 下均无副作用

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- `Assets/Tests/EditMode/Save/ErrorModelTests.cs` — AC-F5-07/14/20/22/23（两级模型 + 缓存分叉 + .corrupt 备份），必须通过

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 004（DONE）
- Unlocks: Story 007
