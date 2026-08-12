# Story 008: 语义与跨字段 Validator（Plan B）

> **Epic**: F4 ScriptableObject 数据层
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: M
> **Manifest Version**: 2026-08-12
> **Last Updated**: —

## Context

**GDD**: `design/gdd/so-data-layer.md`
**Requirement**: `TR-datalayer-008`, `TR-datalayer-002`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-D4: SO 数据层
**ADR Decision Summary**: Plan B 外部 `[RegisterValidator]` Editor-only 类承担语义与跨字段/跨 SO 校验——ComboEdge ID 引用完整性、hitbox 时间窗越界、weaponType 跨 SO 一致性、starvationCeiling 跨 SO 约束；均为 CI 阻塞 Error。

**Engine**: Unity 6.3 LTS (6000.3.19f1) | **Risk**: MEDIUM
**Engine Notes**: 跨字段校验须在外部 RegisterValidator 类中同时读取多个字段（内联单字段 `[ValidateInput]` 无法做跨字段）。跨 SO 校验用 `AssetDatabase.FindAssets("t:XxxSO")` 加载相关资产集比对。浮点边界用 epsilon 容差。

**Control Manifest Rules (this layer)**:
- Required: 跨字段/跨 SO 语义校验经 Plan B 外部 Validator（D4-7）；违反报 CI 阻塞 Error
- Forbidden: Runtime 引用 Sirenix；用内联单字段校验做跨字段逻辑
- Guardrail: 浮点边界用 epsilon；comboGraph 为 null 时一致性校验跳过而非崩溃

---

## Acceptance Criteria

*From GDD `design/gdd/so-data-layer.md`, scoped to this story:*

- [ ] **AC-F4-31**（BLOCKING）: nodes 无 id='miaodao_light999'，某 ComboEdge.fromNodeId='miaodao_light999' → 报 Error 'ComboEdge.fromNodeId 未找到对应 AttackNodeSO（id: miaodao_light999）' 阻止构建
- [ ] **AC-F4-25**: `hitboxStartNormalizedTime=0.8, hitboxDurationNormalized=0.3`（和 1.1）→ 报 Error；对照 `0.7+0.3=1.0` 边界须通过
- [ ] **AC-F4-15**: WeaponSO.weaponType=MiaoDao，comboGraph.weaponType=Spear → 报 Error "weaponType 不匹配"
- [ ] **AC-F4-20**（补充）: PlayerInfoSO Unity Object 引用禁止——RegisterValidator CI 门控覆盖路径
- [ ] **CombatDirectorConfigSO 跨 SO**: `starvationCeiling < 所有 EnemyInfoSO.timeoutBeats`，不满足报 Error

---

## Implementation Notes

*Derived from ADR-D4 Implementation Guidelines:*

- ComboEdge 引用完整性（AC-F4-31）：RegisterValidator 遍历 `ComboGraphSO.nodes` 构建 id `HashSet<string>`，遍历 `edges` 检查 `fromNodeId`/`toNodeId` 均在集合内；缺失报对称格式 Error 阻止构建。
- hitbox 时间窗（AC-F4-25）：外部类同时读 `hitboxStartNormalizedTime` + `hitboxDurationNormalized`，`start+duration>1.0`（epsilon 容差）报 Error；`0.7+0.3=1.0` 边界通过。
- weaponType 一致性（AC-F4-15）：读 `WeaponSO.weaponType` 与 `WeaponSO.comboGraph.weaponType` 比较，不等报 "weaponType 不匹配"；comboGraph 为 null 时跳过（AC-F4-23 已在 Story 004 覆盖 null 报错）。
- PlayerInfoSO Unity Object 引用（AC-F4-20 补充）：若 Story 005 仅以反射 EditMode 测试实现，此处用 RegisterValidator 重新实现同一断言接入 CI 门控——**须与 Story 005 交叉核对，避免同一规则两份漂移实现**。
- starvationCeiling 跨 SO：`AssetDatabase.FindAssets("t:EnemyInfoSO")` 取 timeoutBeats 最小值，`starvationCeiling ≥ min` 报 Error 注明违规资产路径；无 EnemyInfoSO 时跳过。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: ComboEdge/AttackNodeSO schema 本体、hitboxDuration>0 严格校验（AC-F4-32）
- Story 004: WeaponSO comboGraph 非空（AC-F4-23）、单字段枚举合法性
- Story 005: PlayerInfoSO schema 本体与反射扫描 EditMode 测试
- Story 006: CombatDirectorConfigSO 单资产内约束
- Story 007: 重复 id 与 CI 退出码基础设施

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

- **AC-F4-31**: nodes 中无 id='miaodao_light999'，某 ComboEdge.fromNodeId='miaodao_light999' → 报 Error 阻止构建（BLOCKING）
  - Given: ComboGraphSO 实例，nodes 列表中不存在 id='miaodao_light999' 的 AttackNodeSO；某一 ComboEdge.fromNodeId 被设置为 'miaodao_light999'
  - When: RegisterValidator 遍历 nodes 构建 id 集合（HashSet<string>），再遍历全部 edges 检查 fromNodeId 与 toNodeId 是否均存在于该集合
  - Then: 校验结果包含一条 Error，错误信息为 "ComboEdge.fromNodeId 未找到对应 AttackNodeSO（id: miaodao_light999）"，该 Error 阻止构建
  - Edge cases: toNodeId 同样引用不存在的 id 时须产生对称格式的独立 Error；同一 edge 的 fromNodeId 和 toNodeId 同时无效时应各报 1 条，共 2 条 Error，不合并

- **AC-F4-25**: `hitboxStartNormalizedTime=0.8, hitboxDurationNormalized=0.3`（和 1.1）→ 报 Error；对照 `0.7+0.3=1.0` 边界须通过
  - Given: AttackNodeSO 实例，(a) hitboxStartNormalizedTime=0.8, hitboxDurationNormalized=0.3（和 1.1，越界）；(b) hitboxStartNormalizedTime=0.7, hitboxDurationNormalized=0.3（和恰好 1.0，边界合法）
  - When: RegisterValidator 在外部类中同时读取两字段，计算 start + duration
  - Then: 场景 (a) 校验结果包含至少一条 Error；场景 (b) 不含该项 Error
  - Edge cases: start+duration=1.0000001（浮点误差）须使用容差比较（如 epsilon）避免合法边界值被误判越界；start=0,duration=1.0（完整覆盖动画）应视为合法边界

- **AC-F4-15**: WeaponSO.weaponType=MiaoDao，comboGraph.weaponType=Spear → 报 Error "weaponType 不匹配"
  - Given: WeaponSO 实例，weaponType=MiaoDao；其 comboGraph 引用的 ComboGraphSO.weaponType=Spear
  - When: RegisterValidator 读取 WeaponSO.weaponType 与 WeaponSO.comboGraph.weaponType 并比较是否相等
  - Then: 校验结果包含至少一条 Error，错误信息为 "weaponType 不匹配"
  - Edge cases: 两者相等时应通过；comboGraph 为 null 时（AC-F4-23 场景）本条一致性校验应跳过而非因空引用异常崩溃，两条错误各自独立产生

- **AC-F4-20**（Story008 补充：RegisterValidator 覆盖路径）: PlayerInfoSO 禁止 UnityEngine.Object 引用字段
  - Given: 若 Story005 阶段该规则仅以反射扫描 EditMode 测试形式存在（未接入 RegisterValidator CI 门控），本条在 Story008 中通过 RegisterValidator 重新实现同一断言
  - When: RegisterValidator 对 PlayerInfoSO 类型执行字段扫描，检查是否存在派生自 UnityEngine.Object 的可序列化字段
  - Then: 若存在此类字段，校验结果包含至少一条 Error，内容与 Story005 AC-F4-20 一致
  - Edge cases: 若 Story005 已将该逻辑实现为 RegisterValidator，本条须在实现前与 Story005 交叉核对，避免同一规则维护两份互相漂移的实现

- **CombatDirectorConfigSO-CrossSO**: `starvationCeiling < 所有 EnemyInfoSO.timeoutBeats`
  - Given: CombatDirectorConfigSO 实例，starvationCeiling=20；项目中存在至少一个 EnemyInfoSO 资产，timeoutBeats=15（小于 starvationCeiling，构成违规）
  - When: RegisterValidator 通过 `AssetDatabase.FindAssets("t:EnemyInfoSO")` 加载全部 EnemyInfoSO 资产，取 timeoutBeats 最小值与 starvationCeiling 比较
  - Then: 校验结果包含至少一条 Error，错误信息注明违规的 EnemyInfoSO 资产路径及其 timeoutBeats 值
  - Edge cases: 项目中不存在任何 EnemyInfoSO 资产时该校验应跳过而非报错或抛异常；多个 EnemyInfoSO 违反此约束时须逐条列出

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- `Assets/Tests/EditMode/F4DataLayer/SemanticCrossFieldValidator_test.cs` — 须存在且通过（AC-F4-31/25/15/20 + starvationCeiling 跨 SO）

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 002（ComboEdge/AttackNode）、Story 003、Story 004（WeaponSO）、Story 005（PlayerInfoSO）、Story 006（CombatDirectorConfigSO）；Story 001（Editor asmdef）
- Unlocks: None（CI 门控终点之一）
