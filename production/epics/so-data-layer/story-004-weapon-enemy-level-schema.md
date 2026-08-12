# Story 004: 武器/敌人/关卡 SO schema

> **Epic**: F4 ScriptableObject 数据层
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: 2026-08-12
> **Last Updated**: —

## Context

**GDD**: `design/gdd/so-data-layer.md`
**Requirement**: `TR-datalayer-001`（partial）, `TR-datalayer-012`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-D4: SO 数据层
**ADR Decision Summary**: SO 运行时只读（D4-1）；`[SerializeField]` 仅字段；`LevelConfigSO` 的 BGM 路径字段为 `bgmFilePath`（string 路径，取代旧 `bgmAssetRef`）；数值字段经 Odin Validator 约束合法域。

**Engine**: Unity 6.3 LTS (6000.3.19f1) | **Risk**: MEDIUM
**Engine Notes**: `bgmAssetRef` 已重命名为 `bgmFilePath`（string 相对路径，供音频系统解析）。WeaponSO 引用 ComboGraphSO 用直接 SO 引用（同 bundle，D4-6），非 AssetReference。

**Control Manifest Rules (this layer)**:
- Required: SO 字段 `[SerializeField] private` + 只读 getter；数值字段用 Odin `[ValidateInput]` 约束合法域
- Forbidden: 硬编码敌人/关卡数值（须数据驱动）；`Resources/` 加载
- Guardrail: Odin 双模式验证（Plan A 内联 + Plan B 外部）

---

## Acceptance Criteria

*From GDD `design/gdd/so-data-layer.md`, scoped to this story:*

- [ ] **AC-F4-16**: EnemyInfoSO `baseHealth=0`、`baseDamage=1000`、`attackWindupBeats=0.4`、`rewardMeritBase=101` → 报 4 条 Error
- [ ] **AC-F4-17**: LevelConfigSO `bpm=79` 报 Error；`bpm=80` 通过
- [ ] **AC-F4-18**: `timeSignatureNumerator=5, Denominator=4` 报 Error；改 `Denominator=8` 通过
- [ ] **AC-F4-23**: WeaponSO `comboGraph=null` 报 Error
- [ ] **AC-F4-15**（内联注记；跨字段一致性完整校验在 Story 008）: WeaponSO.weaponType 与 comboGraph.weaponType 均为合法枚举取值

---

## Implementation Notes

*Derived from ADR-D4 Implementation Guidelines:*

- `WeaponSO` 字段：`id`、`weaponType`（`WeaponType` 枚举）、`comboGraph`（ComboGraphSO 直接引用，同 bundle）、其余武器参数。`comboGraph` 非空校验用内联 `[Required]`/`[ValidateInput]`。
- `EnemyInfoSO` 字段：`id`、`baseHealth`（>0）、`baseDamage`（合法上限，1000 越界）、`attackWindupBeats`（须为拍相关合法值，0.4 越界）、`rewardMeritBase`（合法上限，101 越界）等。各数值域阈值以 GDD F4-S 字段规范为准。
- `LevelConfigSO` 字段：`id`、`bpm`（≥80）、`timeSignatureNumerator`/`timeSignatureDenominator`（合法拍号组合，5/4 非法、5/8 合法）、`bgmFilePath`（**string 路径，非 AssetReference**）等。
- 本 story 只做单资产内 schema 校验；WeaponSO.weaponType 与 comboGraph.weaponType 的跨字段一致性在 Story 008（AC-F4-15）。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: ComboGraphSO schema 本体（本 story 只引用）
- Story 007: 重复 id 检测（WeaponSO/EnemyInfoSO/LevelConfigSO 各自 AC-F4-28/29/30）
- Story 008: WeaponSO↔comboGraph weaponType 跨字段一致性（AC-F4-15）

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

- **AC-F4-16**: EnemyInfoSO `baseHealth=0`、`baseDamage=1000`、`attackWindupBeats=0.4`、`rewardMeritBase=101` → 报 4 条 Error
  - Given: EnemyInfoSO 实例，设置 baseHealth=0, baseDamage=1000, attackWindupBeats=0.4, rewardMeritBase=101
  - When: 执行 Odin Validator 全字段校验
  - Then: 校验结果恰好包含 4 条 Error，分别对应 baseHealth / baseDamage / attackWindupBeats / rewardMeritBase
  - Edge cases: baseHealth=1（最小合法边界）应通过；baseDamage=999 作为通过对照、1000 作为失败对照；attackWindupBeats 合法下限边界待与 GDD 作者确认后补充对照组

- **AC-F4-17**: LevelConfigSO `bpm=79` 报 Error；`bpm=80` 通过
  - Given: LevelConfigSO 实例，其余必填字段合法
  - When: 分别设置 bpm=79 和 bpm=80，各自执行 Odin Validator 校验
  - Then: bpm=79 时校验结果包含至少一条 Error（字段路径 bpm）；bpm=80 时不含 bpm 相关 Error
  - Edge cases: 浮点精度边界（79.999999 与 80.000001）；bpm 为负值或 0 同样须报错

- **AC-F4-18**: `timeSignatureNumerator=5, Denominator=4` 报 Error；改 `Denominator=8` 通过
  - Given: LevelConfigSO 实例，设置 timeSignatureNumerator=5
  - When: 分别设置 timeSignatureDenominator=4 和 8，各自执行 Odin Validator 校验
  - Then: Denominator=4 时校验结果包含至少一条 Error；Denominator=8 时不含该 Error
  - Edge cases: 测试至少覆盖 4/4（合法基准）、5/4（非法，本 AC 用例）、5/8（合法，本 AC 用例）三组组合，完整合法组合枚举须与 GDD 作者确认

- **AC-F4-23**: WeaponSO `comboGraph=null` 报 Error
  - Given: WeaponSO 实例，设置 comboGraph=null
  - When: 执行 Odin Validator 校验
  - Then: 校验结果包含至少一条 Error，字段路径为 comboGraph
  - Edge cases: comboGraph 指向一个 nodes 为空的 ComboGraphSO（触发 AC-F4-11 的独立错误）——两条错误应同时出现，互不覆盖

- **AC-F4-15**（Story004 范围：字段类型存在性检查；跨字段一致性完整校验见 Story008）: WeaponSO.weaponType 与 comboGraph.weaponType 均为合法枚举取值
  - Given: WeaponSO 实例，weaponType 与其 comboGraph 引用的 ComboGraphSO.weaponType 均已赋值为 WeaponType 枚举的合法成员
  - When: 执行 Odin Validator 的 schema 级字段校验（不含跨字段比较逻辑）
  - Then: 两个字段均通过各自独立的"必须为已定义枚举值"校验，不产生 Error
  - Edge cases: 本故事范围内不校验 weaponType != comboGraph.weaponType 的不一致场景——该断言在 Story008 的 AC-F4-15 测试规格中实现，Story004 不得重复实现该跨字段逻辑

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- `Assets/Tests/EditMode/F4DataLayer/WeaponEnemyLevelSchema_test.cs` — 须存在且通过（AC-F4-16/17/18/23/15）

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001（程序集与 Group）、Story 003（复用 struct/schema 约定）、Story 002（WeaponSO 引用 ComboGraphSO）
- Unlocks: Story 007、Story 008
