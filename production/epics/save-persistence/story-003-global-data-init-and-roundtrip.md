# Story 003: 全局数据初始化与往返（Initialize + GlobalData + ISaveService seam）

> **Epic**: F5 存档/持久化系统
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: M
> **Manifest Version**: 2026-08-12
> **Last Updated**: [set by /dev-story when implementation begins]

## Context

**GDD**: `design/gdd/save-persistence.md`
**Requirement**: `TR-save-002`, `TR-save-014`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-D10: Bootstrap 顺序（primary）；ADR-D7: 存档/持久化中间件（secondary）
**ADR Decision Summary**: `SaveService.Initialize()` 为同步、主线程、阻塞、返回 `void`（ADR-D10 D-4），在 Awake→Start 窗口内完成全部 GlobalData + 激活槽位加载；对外暴露消费者面向 `ISaveService` 作为 F2/F6 的 mock seam。

**Engine**: Unity 6.3 LTS (6000.3.19f1) | **Risk**: LOW
**Engine Notes**: `Application.persistentDataPath` 跨平台目录（Windows: `%USERPROFILE%/AppData/LocalLow/<CompanyName>/<ProductName>`，须与 ProjectSettings 精确匹配）。Mobile 路径行为待移动端集成测试验证（Open Question 3）。无 post-cutoff API。

**Control Manifest Rules (this layer)**:
- Required: `SaveService.Initialize()` 正式同步、主线程、阻塞、返回 `void`，在任何 `Start()` 阶段消费者运行前完成全部加载（ADR-D10）
- Required: 定义消费者面向 `ISaveService`（`Initialize()`/`IsInitialized`/`LoadLatencyCalibration()`/`SaveLatencyCalibration(dto)`），与 F5 内部 `IFileStorage` 并存（ADR-D10）
- Forbidden: 使用 async 的 `SaveService.Initialize()`（`Task`/coroutine + timeout/fallback）— ES3 无 async 读路径（ADR-D10）
- Forbidden: 在 `ES3Storage` 之外引用 `ES3.*`（ADR-D7）
- Guardrail: `Initialize()` < 50ms PC / < 100ms Mobile（启动闪屏成本）

---

## Acceptance Criteria

*From GDD `design/gdd/save-persistence.md`, scoped to this story:*

- [ ] **AC-F5-01**：磁盘无任何存档文件时 `Initialize()` 完成后 —
  - `LoadSlotProgress(0/1/2)` 均返回默认 DTO（`merit=0, unlockedWeaponIds=[], metaUpgradeLevels=[], runCount=0, totalPlayTimeSeconds=0.0`）
  - `LoadLatencyCalibration()` 返回 `{latencyOffsetMs=0.0f, isCalibrated=false}`
  - `LoadSettings()` 返回 `{masterVolume=1.0f, musicVolume=0.8f, sfxVolume=1.0f}`
  - `LoadAccessibility()` 返回 `AccessibilityDTO` 全默认值
  - `GetAllSlotMeta()` 返回 3 个 `slotState=Empty` 的 SlotMetaDTO
  - `SaveService` 处于 `Idle`，`saves/` 与 `saves/global/` 目录均已创建
- [ ] **AC-F5-03**：`SaveLatencyCalibration({latencyOffsetMs=42.5f, isCalibrated=true})` 后 `LoadLatencyCalibration()` 返回 `latencyOffsetMs==42.5f`（误差 ≤0.001f）、`isCalibrated==true`
- [ ] **AC-F5-15**：`SaveLatencyCalibration({latencyOffsetMs=-200.0f, isCalibrated=true})` 后 `LoadLatencyCalibration()` 返回 `-200.0f`（误差 ≤0.001f）、`isCalibrated==true`
- [ ] `Initialize()` 为同步阻塞、返回 `void`；返回后 `IsInitialized == true`（即 `state == Idle`）
- [ ] `Uninitialized→Loading→Idle` 状态转换正确（全部文件读取成功进 `Idle`）
- [ ] 定义 `ISaveService` 接口 + `MockSaveService : ISaveService`（返回预置 DTO、无文件 I/O，供 F6 消费方测试）
- [ ] `LoadSettings()`/`SaveSettings()`、`LoadAccessibility()`/`SaveAccessibility()` 三全局域读写往返一致

---

## Implementation Notes

*Derived from ADR-D10 / ADR-D7 Implementation Guidelines:*

- `Initialize()` 全同步：目录创建（`saves/`、`saves/global/`）→ 顺序加载三个全局文件（`global/settings.es3`、`global/calibration.es3`、`global/accessibility.es3`）→ 加载激活槽位（槽位往返细节在 story 004）→ 置 `Idle`。**无协程、无 Task、无 timeout/fallback**。
- 三个 GlobalData DTO **各存独立文件、各自携带独立 `save_version`**（Rule 2）。文件路径严格按 F5-D5。
- 缺失文件返回默认 DTO（非错误），进 `Idle`（Edge Case：首次启动）。GlobalData 损坏→`Error` 属 story 006 范畴，本 story 仅覆盖 happy path + 缺失默认。
- `ISaveService` 只暴露消费者面（`Initialize/IsInitialized/LoadLatencyCalibration/SaveLatencyCalibration` + 既有 `Load*`），**区别于** story 001 的 `IFileStorage`（存储后端 mock）。`MockSaveService` 供 F6 AC-F6-23 headless 测试。
- 加载/写入经 story 002 已注入的 `IFileStorage`，绝不直接 `ES3.*`。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002：单例/状态机骨架/线程捕获（本 story 依赖）
- Story 004：槽位进度往返、多槽位独立、SlotMeta 同步、激活槽位加载细节
- Story 005：激活槽位管理 API、DeleteSlot
- Story 006：GlobalData 损坏 → `Error`、`OnSaveError`、`.corrupt` 备份
- Story 007：版本迁移

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

- **AC-1 (AC-F5-01)（首次启动默认值）**:
  - Given: `MockFileStorage` 为空（无任何文件）
  - When: `InjectStorage(mock)` 后 `Initialize()` 完成
  - Then: 三槽位 `LoadSlotProgress` 返回全默认 DTO；`LoadLatencyCalibration`=`{0.0f,false}`；`LoadSettings`=`{1.0f,0.8f,1.0f}`；`GetAllSlotMeta()` 三个 `Empty`；`state==Idle`
  - Edge cases: 目录不存在时 `Initialize()` 创建 `saves/`+`saves/global/`；`unlockedWeaponIds`/`metaUpgradeLevels` 为空数组非 null

- **AC-2 (AC-F5-03)（延迟校准往返）**:
  - Given: `Idle` 状态
  - When: `SaveLatencyCalibration({42.5f,true})` 后 `LoadLatencyCalibration()`
  - Then: `latencyOffsetMs==42.5f`（±0.001f），`isCalibrated==true`
  - Edge cases: 写入触发 `Saving→Idle` 转换；`SaveCallCount` 递增 1

- **AC-3 (AC-F5-15)（延迟值域边界往返）**:
  - Given: `Idle` 状态
  - When: `SaveLatencyCalibration({-200.0f,true})` 后 `LoadLatencyCalibration()`
  - Then: `latencyOffsetMs==-200.0f`（±0.001f），`isCalibrated==true`
  - Edge cases: 上边界 `+200.0f` 同样精确往返

- **AC-4（Settings/Accessibility 往返）**:
  - Given: `Idle` 状态
  - When: `SaveSettings({0.5f,0.3f,0.7f})` 后 `LoadSettings()`；`SaveAccessibility(dto)` 后 `LoadAccessibility()`
  - Then: 读回值与写入一致
  - Edge cases: 三全局文件互相独立，写 Settings 不影响 Calibration/Accessibility

- **AC-5（ISaveService seam 可 mock）**:
  - Given: `MockSaveService` 实例
  - When: 消费方调用 `Initialize()` + `LoadLatencyCalibration()`
  - Then: `IsInitialized==true`，返回预置 DTO，无任何文件 I/O
  - Edge cases: `MockSaveService` 与真实 `SaveService` 满足同一 `ISaveService` 契约

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- `Assets/Tests/EditMode/Save/GlobalDataRoundTripTests.cs` — 首次启动默认值 + 三全局域往返 + ISaveService seam，必须通过

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 002（DONE）
- Unlocks: Story 004
