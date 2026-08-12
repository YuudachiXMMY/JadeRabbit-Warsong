# Story 005: 玩家持久化 SO schema 与 F5 DTO 契约

> **Epic**: F4 ScriptableObject 数据层
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: 2026-08-12
> **Last Updated**: —

## Context

**GDD**: `design/gdd/so-data-layer.md`
**Requirement**: `TR-datalayer-009`, `TR-datalayer-010`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-D4: SO 数据层（primary）+ ADR-D7: 存档/持久化（secondary）
**ADR Decision Summary**: `PlayerInfoSO` 是存档消费者契约——只序列化纯数据结构，禁止 UnityEngine.Object 引用（D4-8/EasySave3 约束）；运行时缓存字段用 `[NonSerialized]`，F5 存档层为权威数据源，加载前返回 C# 默认值。

**Engine**: Unity 6.3 LTS (6000.3.19f1) | **Risk**: LOW
**Engine Notes**: EasySave 3 honours `[NonSerialized]`——标注字段不参与序列化往返。F5 存档层用 DTO（PlayerProgressDTO/LatencyCalibrationDTO/GameSettingsDTO 等），版本迁移经 DTO `save_version` 字段 + `MigrateIfNeeded<T>()`。`[NonSerialized]` 字段（latencyCalibrationOffsetMs/audioVolumeMaster）init 时序须等待 F5 加载填充。

**Control Manifest Rules (this layer)**:
- Required: `PlayerInfoSO` 只序列化纯数据（struct/class，无 Unity Object 引用）；运行时缓存字段用 `[NonSerialized]`（D4-8）
- Forbidden: 直接序列化 MonoBehaviour/UnityEngine.Object 引用；直接序列化 SO 本体（EasySave3 约束）
- Guardrail: Odin 双模式验证；F5 DTO 为 latency/volume 权威源，SO 缓存字段不在加载前被读为业务真值

---

## Acceptance Criteria

*From GDD `design/gdd/so-data-layer.md`, scoped to this story:*

- [ ] **AC-F4-20**: PlayerInfoSO 声明 GameObject 等 UnityEngine.Object 子类字段 → 报 Error "含有 Unity Object 引用"
- [ ] **AC-F4-21**: `latencyCalibrationOffsetMs=-201`、`audioVolumeMaster=1.1` → 报 2 条 Error
- [ ] **TR-datalayer-010**（时序约束）: `[NonSerialized]` 字段（latencyCalibrationOffsetMs/audioVolumeMaster）不参与序列化往返；Addressables 加载后返回 C# 默认值直至 F5 填充

---

## Implementation Notes

*Derived from ADR-D4 / ADR-D7 Implementation Guidelines:*

- `PlayerInfoSO` 字段：功勋/武器解锁列表（string[]）、`MetaUpgradeEntry[]`、设置项、`[NonSerialized] latencyCalibrationOffsetMs`、`[NonSerialized] audioVolumeMaster` 等。
- 所有持久化字段须为 EasySave3 兼容纯数据：int/string/string[]/`MetaUpgradeEntry[]`（`[Serializable]` struct/class，无 Unity Object 引用，无 Dictionary）。
- `latencyCalibrationOffsetMs` 合法域含 -200（-201 越界）；`audioVolumeMaster` 合法域含 1.0（1.1 越界）。校验用 Odin `[ValidateInput]`。
- `[NonSerialized]` 缓存字段：加载新实例时返回 C# 默认值（0 / 0f），F5 存档层负责填充为权威值——init 时序须等待 F5 加载完成，勿在加载前当业务真值读取。
- AC-F4-19（存档往返完整性）已迁移到 F5，不在本 story 范围。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: 程序集与 Group 骨架
- Story 008: RegisterValidator CI 门控覆盖 PlayerInfoSO Unity Object 引用（AC-F4-20 补充路径）
- F5：存档往返完整性（AC-F4-19）、DTO 序列化/迁移实现、EasySave3 IO

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

- **AC-F4-20**: PlayerInfoSO 声明 GameObject 等 UnityEngine.Object 子类字段 → 报 Error "含有 Unity Object 引用"
  - Given: 用于测试的 PlayerInfoSO 场景中存在一个类型为 GameObject 的可序列化字段
  - When: 执行反射扫描（遍历 PlayerInfoSO 全部可序列化字段，检查 FieldType 是否派生自 UnityEngine.Object）或等效 Odin Validator 校验
  - Then: 校验结果包含至少一条 Error，错误信息包含"含有 Unity Object 引用"
  - Edge cases: 字段类型为 Sprite/AudioClip 等其他 UnityEngine.Object 子类同样须报错，不限于 GameObject；普通 C# class/struct（非 UnityEngine.Object 派生）字段不应触发此规则

- **AC-F4-21**: `latencyCalibrationOffsetMs=-201`、`audioVolumeMaster=1.1` → 报 2 条 Error
  - Given: PlayerInfoSO 实例，设置 latencyCalibrationOffsetMs=-201, audioVolumeMaster=1.1
  - When: 执行 Odin Validator 全字段校验
  - Then: 校验结果恰好包含 2 条 Error，分别对应 latencyCalibrationOffsetMs / audioVolumeMaster
  - Edge cases: latencyCalibrationOffsetMs=-200（边界值）应通过；audioVolumeMaster=1.0（边界值）应通过；两字段均标注 `[NonSerialized]` 时校验器是否仍对内存中当前值生效，须在实现时确认并在测试注释中说明

- **TR-datalayer-010**: `[NonSerialized]` 字段不参与序列化往返，Addressables 加载后返回 C# 默认值直至 F5 填充
  - Given: PlayerInfoSO 资产，latencyCalibrationOffsetMs 与 audioVolumeMaster 均标注 [NonSerialized]
  - When: 通过 `Addressables.LoadAssetAsync`（或等效 `AssetDatabase.LoadAssetAtPath`）加载得到一个新的运行时实例
  - Then: 新实例的 latencyCalibrationOffsetMs == 0 且 audioVolumeMaster == 0f（C# 默认值），不携带磁盘上曾经写入过的任何非默认值
  - Edge cases: 测试环境若 Addressables 未初始化，改用 AssetDatabase.LoadAssetAtPath 加载新实例作为等效验证路径；须确保断言针对"新加载的独立实例"而非复用同一内存实例（避免测试误判为通过）

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- `Assets/Tests/EditMode/F4DataLayer/PlayerInfoSchema_test.cs` — 须存在且通过（AC-F4-20/21 + TR-datalayer-010 时序）

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001（程序集与 Group）
- Unlocks: Story 008（RegisterValidator 覆盖 AC-F4-20 CI 门控路径）；F5 存档层消费此契约
