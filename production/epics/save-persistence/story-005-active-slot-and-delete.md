# Story 005: 激活槽位管理与槽位删除

> **Epic**: F5 存档/持久化系统
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: [fill before sprint planning]
> **Manifest Version**: 2026-08-12
> **Last Updated**: [set by /dev-story when implementation begins]

## Context

**GDD**: `design/gdd/save-persistence.md`
**Requirement**: `TR-save-003`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR: N/A — 纯应用逻辑（激活槽位状态机 + 物理删除），无架构决策需要 ADR 治理；GDD Rule 9/10 即规范。文件放置仍遵循 ADR-D7 的 `Core/Persistence/` 约定。
**ADR Decision Summary**: 不适用。激活槽位与删除逻辑由 GDD Rule 9、Rule 10 直接规范。

**Engine**: Unity 6.3 LTS (6000.3.19f1) | **Risk**: LOW
**Engine Notes**: 无引擎特定 API；纯 C# 逻辑 + `IFileStorage.DeleteFile`。

**Control Manifest Rules (this layer)**:
- Required: 经 `IFileStorage` 存取，不直接 `ES3.*`（ADR-D7）
- Guardrail: 常量 `SaveService.MaxSlotCount = 3`，不可运行时修改（Rule 3）

---

## Acceptance Criteria

*From GDD `design/gdd/save-persistence.md`, scoped to this story:*

- [ ] **AC-F5-10**：未选择槽位时 `GetActiveSlotIndex()` 返回 `-1`
- [ ] **AC-F5-13**：`GetActiveSlotIndex()==-1` 时 `SetActiveSlotIndex(1)` 后 `GetActiveSlotIndex()` 返回 `1`
- [ ] **AC-F5-13b**：`SetActiveSlotIndex(3)`（越界）抛 `ArgumentOutOfRangeException`，`GetActiveSlotIndex()` 返回值不变
- [ ] **AC-F5-18**：`SetActiveSlotIndex(-1)` 为合法调用，`GetActiveSlotIndex()` 返回 `-1`，无异常
- [ ] **AC-F5-16**：slot 0 已写入（`merit=500`），`DeleteSlot(0)` 后 `GetAllSlotMeta()[0].slotState==Empty`、`LoadSlotProgress(0).merit==0`、磁盘 `slot_0.es3` 与 `meta_0.es3` 均不存在
- [ ] **AC-F5-17**：`SetActiveSlotIndex(0)` 后 `DeleteSlot(0)` → `GetActiveSlotIndex()` 返回 `-1`（自动调用 `SetActiveSlotIndex(-1)`）
- [ ] **AC-F5-19**：`DeleteSlot(-1)` 与 `DeleteSlot(3)`（越界）均抛 `ArgumentOutOfRangeException`，所有槽位数据不变

---

## Implementation Notes

*Derived from GDD Rule 9 / Rule 10:*

- `SetActiveSlotIndex(int)`：合法值 `[0, MaxSlotCount-1]` 或 `-1`（退出选择）。越界（如 `3`、`-2`）抛 `ArgumentOutOfRangeException`，抛出前不改变当前激活值。
- `GetActiveSlotIndex()` 返回内存中的激活 index，初始 `-1`。激活槽位是否需持久化：GDD 未要求跨会话持久化激活选择（主菜单每次重新选择），保持内存态即可。
- `DeleteSlot(int slotIndex)`：越界抛 `ArgumentOutOfRangeException`；否则 `IFileStorage.DeleteFile(slot_n.es3)` + `DeleteFile(meta_n.es3)`（幂等，文件不存在静默忽略），内存 SlotMeta 重置为 `Empty`（`lastPlayedUtcMs=0`、`meritSnapshot=0` 等默认）。
- 若被删槽位 == 当前激活槽位，删除后自动 `SetActiveSlotIndex(-1)`。
- 删除不可撤销；确认弹窗是 P7 UI 层职责，不在 F5。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 004：`SaveSlotProgress`/`LoadSlotProgress`/`GetAllSlotMeta` 往返（本 story 依赖）
- Story 006：错误模型（删除本身不涉及错误状态）
- P7 UI 层的删除确认弹窗（非 F5 范畴）

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

- **AC-1 (AC-F5-10 / 13 / 18)（激活槽位设置与退出）**:
  - Given: `Initialize()` 完成，`GetActiveSlotIndex()==-1`
  - When: `SetActiveSlotIndex(1)`，再 `SetActiveSlotIndex(-1)`
  - Then: 中间 `GetActiveSlotIndex()==1`；最终 `==-1`，无异常
  - Edge cases: 边界合法值 `0` 与 `MaxSlotCount-1`（2）均可设置

- **AC-2 (AC-F5-13b)（激活越界抛异常）**:
  - Given: `GetActiveSlotIndex()` 为已知值（如 `1`）
  - When: `SetActiveSlotIndex(3)`
  - Then: 抛 `ArgumentOutOfRangeException`，`GetActiveSlotIndex()` 仍为 `1`（不变）
  - Edge cases: `SetActiveSlotIndex(-2)` 同样抛异常且不改变当前值

- **AC-3 (AC-F5-16)（DeleteSlot 清除数据）**:
  - Given: slot 0 已写 `merit=500`，`Idle` 状态
  - When: `DeleteSlot(0)`
  - Then: `GetAllSlotMeta()[0].slotState==Empty`；`LoadSlotProgress(0).merit==0`；`FileExists(slot_0.es3)==false` 且 `FileExists(meta_0.es3)==false`
  - Edge cases: 删除空槽位不抛异常（幂等 DeleteFile）；slot 1/2 数据不受影响

- **AC-4 (AC-F5-17)（删除激活槽位自动退出）**:
  - Given: `SetActiveSlotIndex(0)`，`GetActiveSlotIndex()==0`
  - When: `DeleteSlot(0)`
  - Then: `GetActiveSlotIndex()==-1`
  - Edge cases: 删除非激活槽位（如激活为 1 时删 0）不改变 `GetActiveSlotIndex()`

- **AC-5 (AC-F5-19)（DeleteSlot 越界抛异常）**:
  - Given: `Initialize()` 完成，各槽位数据完好
  - When: `DeleteSlot(-1)` 与 `DeleteSlot(3)`
  - Then: 均抛 `ArgumentOutOfRangeException`；`GetAllSlotMeta()` 返回值与调用前完全相同
  - Edge cases: 越界调用不触碰任何 `IFileStorage.DeleteFile`

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- `Assets/Tests/EditMode/Save/ActiveSlotAndDeleteTests.cs` — AC-F5-10/13/13b/16/17/18/19，必须通过

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 004（DONE）
- Unlocks: None
