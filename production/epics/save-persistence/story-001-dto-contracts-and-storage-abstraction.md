# Story 001: 存档 DTO 契约与 IFileStorage 存储抽象层

> **Epic**: F5 存档/持久化系统
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: [fill before sprint planning]
> **Manifest Version**: 2026-08-12
> **Last Updated**: [set by /dev-story when implementation begins]

## Context

**GDD**: `design/gdd/save-persistence.md`
**Requirement**: `TR-save-005`, `TR-save-008`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-D7: 存档/持久化中间件
**ADR Decision Summary**: 选定 EasySave 3 作为序列化中间件；`SaveService` 通过 `IFileStorage` 抽象访问存储后端，绝不直接调用 `ES3.*` 静态 API（仅 `ES3Storage` 内部可引用 ES3）。

**Engine**: Unity 6.3 LTS (6000.3.19f1) | **Risk**: LOW
**Engine Notes**: 无 post-cutoff Unity API。EasySave 3 为已批准中间件；`ES3.Load<T>(key, filePath, defaultValue, settings)` 签名与 native `ES3.RenameFile()`（ES3.cs:1142）须核对引擎参考。

**Control Manifest Rules (this layer)**:
- Required: 文件放置 — `ES3Storage` → `Assets/Scripts/Core/Persistence/ES3Storage.cs`；`IFileStorage` → `.../IFileStorage.cs`（ADR-D7）
- Required: 使用 native `ES3.RenameFile()`（ES3.cs:1142），不手写三步 I/O（ADR-D7）
- Required: `ES3Cloud` 禁用 — 保持 `ES3Settings.location` 默认 `File`（ADR-D7）
- Forbidden: 在 `ES3Storage` 之外的任何 runtime 代码引用 `ES3.*`（ADR-D7）
- Forbidden: 序列化含 `UnityEngine.Object` 引用的对象 — DTO 必须是纯数据（F5 Rule 6）
- Forbidden: 存档层使用 System.Text.Json / Newtonsoft.Json（ADR-D7）

---

## Acceptance Criteria

*From GDD `design/gdd/save-persistence.md`, scoped to this story:*

本 story 不覆盖行为型 AC（往返/错误由下游 story 验证），交付的是**契约与抽象层**。验收标准为结构性：

- [ ] 定义 5 个 DTO：`PlayerProgressDTO`（F5-D1）、`SlotMetaDTO`（F5-D2）、`LatencyCalibrationDTO`（F5-D3）、`GameSettingsDTO`（F5-D4）、`AccessibilityDTO`（F5-D6），字段类型/值域严格匹配 Formulas 节
- [ ] `PlayerProgressDTO` 字段：`merit:int`、`unlockedWeaponIds:string[]`、`metaUpgradeLevels:MetaUpgradeEntry[]`、`runCount:int`、`totalPlayTimeSeconds:double`、`save_version:int`
- [ ] `SlotMetaDTO` 含 `SlotState` 枚举 `{Empty, Active, Corrupted}`；`slotIndex` 不持久化到文件（读取后注入）
- [ ] `GameSettingsDTO` 默认值 `masterVolume=1.0f, musicVolume=0.8f, sfxVolume=1.0f`；`LatencyCalibrationDTO` 默认 `latencyOffsetMs=0.0f, isCalibrated=false`
- [ ] 每个 DTO 定义 `const int CurrentVersion`（MVP 均为 1）
- [ ] 所有 DTO 为纯数据结构（`struct` 或 `[System.Serializable] class`），无 `UnityEngine.Object` 引用
- [ ] `IFileStorage` 接口定义 6 个方法：`Save<T>` / `Load<T>` / `KeyExists` / `FileExists` / `DeleteFile` / `RenameFile`
- [ ] `ES3Storage : IFileStorage` 生产实现，封装 ES3 API + AES-128 加密 + native `RenameFile`；`RenameFile` 目标已存在时静默覆盖；`DeleteFile` 文件不存在时静默忽略（幂等）
- [ ] `MockFileStorage : IFileStorage` 内存 `Dictionary` 实现，额外提供测试辅助属性 `int SaveCallCount`、`int LoadCallCount`；`Load<T>` 文件不存在返回 `defaultValue`，模拟损坏时抛 `FormatException`

---

## Implementation Notes

*Derived from ADR-D7 Implementation Guidelines:*

- 文件放置严格遵守 manifest：`Assets/Scripts/Core/Persistence/` 下的 `IFileStorage.cs`、`ES3Storage.cs`；DTO 建议置于 `Assets/Scripts/Core/Persistence/Dtos/`。
- `ES3Storage` 是**唯一**允许引用 `ES3.*` 的 runtime 类。`Save<T>/Load<T>` 内部映射到 `ES3.Save`/`ES3.Load<T>(key, filePath, defaultValue, settings)`；`RenameFile` 调用 native `ES3.RenameFile()`（ES3.cs:1142），不要手写"读→写→删"三步。
- AES-128 加密通过 `ES3Settings`（`EncryptionType.AES`）配置；`location` 保持默认 `File`，不设置 `ES3CloudSettings`。
- `MockFileStorage` 用 `Dictionary<string, object>`（键为 `path + "::" + key`）模拟磁盘。`SaveCallCount`/`LoadCallCount` 每次调用自增，供 AC-F5-08/09/20 断言写入次数。损坏模拟通过预置标记键使对应 `Load<T>` 抛 `FormatException`。
- `MetaUpgradeEntry` 为 `[Serializable] struct {string id; int level;}`，权威定义在 F4-S7；若 F4 未落地则在 Persistence 域临时定义并加注引用。
- `save_version` 由 `SaveService` 负责读写（story 007），本 story 只定义字段与 `CurrentVersion` 常量，不实现迁移逻辑。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002：`SaveService` 单例/状态机/线程捕获
- Story 003+：任何 `Initialize()`/`LoadX`/`SaveX` 服务方法与往返行为
- Story 007：`MigrateIfNeeded<T>()` 版本迁移逻辑（本 story 仅定义 `save_version` 字段）
- `ISaveService` 消费者 seam（story 003 定义）

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

- **AC-1（DTO 契约完整性）**: 5 个 DTO 字段类型与值域匹配 GDD Formulas
  - Given: 编译后的 Persistence 程序集
  - When: 反射读取各 DTO 字段签名与默认值
  - Then: 字段名/类型/默认值逐一匹配 F5-D1..D6；`GameSettingsDTO` 新实例默认 `1.0/0.8/1.0`，`LatencyCalibrationDTO` 默认 `0.0f/false`
  - Edge cases: `unlockedWeaponIds`/`metaUpgradeLevels` 默认应为空数组而非 null；`totalPlayTimeSeconds` 必须为 `double` 非 `float`

- **AC-2（IFileStorage 往返）**: `MockFileStorage` 写入后读回一致
  - Given: 新建 `MockFileStorage`
  - When: `Save<PlayerProgressDTO>(path,key,dto)` 后 `Load<PlayerProgressDTO>(path,key,default)`
  - Then: 读回 DTO 与写入位一致；`SaveCallCount==1`、`LoadCallCount==1`
  - Edge cases: 未写入即 `Load` 返回传入的 `defaultValue`；`KeyExists`/`FileExists` 对未写入路径返回 false

- **AC-3（RenameFile 覆盖语义）**: 目标存在时静默覆盖
  - Given: `MockFileStorage` 中 `a` 与 `b` 均已存在
  - When: `RenameFile(a, b)`
  - Then: `b` 内容为原 `a` 内容，`a` 不再存在，无异常
  - Edge cases: `DeleteFile` 对不存在路径调用不抛异常（幂等）

- **AC-4（损坏模拟）**: `MockFileStorage` 可模拟解析失败
  - Given: 预置 `slot_0.es3` 为损坏标记
  - When: `Load<PlayerProgressDTO>(...)`
  - Then: 抛出 `FormatException`
  - Edge cases: 损坏标记仅影响目标键，其他键正常读取

> `ES3Storage`（真实 ES3 + 加密 + native rename）属 Integration，须以真实文件路径（临时目录）验证一次往返与加密后不可明文读取；CI 中若 ES3 不可用则以 playtest 文档补充。

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- `Assets/Tests/EditMode/Save/StorageAbstractionTests.cs` — DTO 契约 + `MockFileStorage` 往返/rename/损坏，必须通过
- `ES3Storage` 真实往返：Integration 测试或 playtest 文档

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: None（Foundation 层最先开发）
- Unlocks: Story 002
