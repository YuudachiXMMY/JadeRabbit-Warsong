# Story 006: 战斗导演全局配置 SO schema

> **Epic**: F4 ScriptableObject 数据层
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: 2026-08-12
> **Last Updated**: —

## Context

**GDD**: `design/gdd/so-data-layer.md`（Overview + F4-S8）
**Requirement**: `TR-datalayer-001`（第 8 类 SO）
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-D4: SO 数据层
**ADR Decision Summary**: SO 运行时只读（D4-1）；`[SerializeField]` 仅字段；集合字段用可序列化 List（非原生 Dictionary，Unity 不序列化 Dictionary）；数值单调性/容量约束经 Odin Validator。

**Engine**: Unity 6.3 LTS (6000.3.19f1) | **Risk**: MEDIUM
**Engine Notes**: Unity 不序列化原生 `Dictionary<,>`——容量表须用 `List<MeterCapacityEntry>` 承载。CombatDirectorConfigSO 是 GDD F4-S8 定义的第 8 类 SO；其数据契约供 C4 战斗导演系统消费（C4 GDD 边界）。

**Control Manifest Rules (this layer)**:
- Required: 集合字段用可序列化 `List<T>`；数值约束用 Odin `[ValidateInput]`（单调性/容量下限）
- Forbidden: 原生 `Dictionary<,>` 作为序列化字段；硬编码导演权重/距离阈值
- Guardrail: Odin 双模式验证；单资产内约束在本 story，跨 SO 约束（starvationCeiling vs EnemyInfoSO.timeoutBeats）在 Story 008

---

## Acceptance Criteria

*From GDD `design/gdd/so-data-layer.md` F4-S8，scoped to this story:*

- [ ] **AC-F4-S8-01**（权重单调递减）: `wNear > wMid > wFar ≥ 0`，违反报 Error
- [ ] **AC-F4-S8-02**（距离阈值关系）: `dMid > dNear`，违反报 Error
- [ ] **AC-F4-S8-03**（每拍号至少一位可出手）: 每个拍号条目 `perBeatCapacity` 至少一位 ≥1（防敌人永久无法出手），违反报 Error
- [ ] **AC-F4-S8-04**（数据结构类型）: `capacityTable` 用 `List<MeterCapacityEntry>` 承载（非原生 Dictionary）

---

## Implementation Notes

*Derived from ADR-D4 Implementation Guidelines + GDD F4-S8:*

- `CombatDirectorConfigSO` 字段：距离权重 `wNear`/`wMid`/`wFar`、距离阈值 `dNear`/`dMid`、容量表 `capacityTable`（`List<MeterCapacityEntry>`）、`starvationCeiling` 等。
- `MeterCapacityEntry`：`[Serializable]`，含拍号标识与 `perBeatCapacity`（数组/列表，per-meter 每拍容量）。
- 单调性/关系校验用 Odin `[ValidateInput]`：wNear>wMid>wFar≥0；dMid>dNear。
- 每拍号容量下限：某拍号 `perBeatCapacity` 全为 0 → 敌人永久无法出手 → 报 Error。
- **CombatDirectorConfigSO 是第 8 类 SO**——注意 AC-F4-01 原文按 7 类描述 grep，Story 007 的 AC-F4-01 检查须同步扩为 8 类，并提示 GDD 作者更正原文数字。
- 跨 SO 约束（`starvationCeiling < 所有 EnemyInfoSO.timeoutBeats`）不在本 story，见 Story 008。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: 程序集与 Group 骨架
- Story 004: EnemyInfoSO schema 本体
- Story 007: 第 8 类纳入 AC-F4-01 grep、重复 id CI 门控
- Story 008: starvationCeiling 跨 SO 约束
- C4：战斗导演运行时逻辑（本 story 只定义数据契约）

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

- **AC-F4-S8-01**（权重单调递减）: `wNear > wMid > wFar ≥ 0`
  - Given: CombatDirectorConfigSO 实例
  - When: 分别设置 (a) wNear=1,wMid=2,wFar=0（非单调）；(b) wNear=3,wMid=2,wFar=2（非严格大于）；(c) wNear=3,wMid=2,wFar=1（合法对照）
  - Then: (a)(b) 校验结果包含至少一条 Error；(c) 不含该项 Error
  - Edge cases: wFar=0 的边界（允许等于 0，不允许小于 0）；wFar=-0.001 应报错

- **AC-F4-S8-02**（距离阈值关系）: `dMid > dNear`
  - Given: CombatDirectorConfigSO 实例
  - When: 分别设置 (a) dNear=5,dMid=5（相等）；(b) dNear=5,dMid=3（反转）；(c) dNear=5,dMid=6（合法对照）
  - Then: (a)(b) 校验结果包含至少一条 Error；(c) 不含该项 Error
  - Edge cases: dMid=dNear+0.001 的极小合法差值应通过

- **AC-F4-S8-03**（每拍号至少一位可出手）: perBeatCapacity 至少一位 ≥1
  - Given: CombatDirectorConfigSO.capacityTable 中某一 MeterCapacityEntry（对应某一拍号）的 perBeatCapacity 数组全部元素为 0
  - When: 执行 Odin Validator 校验
  - Then: 校验结果包含至少一条 Error，指出该拍号条目缺乏可出手容量
  - Edge cases: perBeatCapacity 恰好一个元素为 1、其余为 0 应通过；仅报出违规的那一条目，不误报其他合法拍号条目

- **AC-F4-S8-04**（数据结构类型）: `capacityTable` 用 `List<MeterCapacityEntry>` 承载
  - Given: CombatDirectorConfigSO 类型定义
  - When: 通过反射获取 capacityTable 字段的 FieldType
  - Then: FieldType 等于 `typeof(List<MeterCapacityEntry>)`，不是 `Dictionary<,>` 或其他不可直接被 Unity 序列化的类型
  - Edge cases: 若使用自定义可序列化字典包装类，须与 GDD 作者确认是否符合约束原意；跨 SO 的 starvationCeiling 约束不在本故事范围内，见 Story008

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- `Assets/Tests/EditMode/F4DataLayer/CombatDirectorConfigSchema_test.cs` — 须存在且通过（AC-F4-S8-01..04）

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001（程序集与 Group）
- Unlocks: Story 007（AC-F4-01 纳入第 8 类）、Story 008（starvationCeiling 跨 SO 约束）
