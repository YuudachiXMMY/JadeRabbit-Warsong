# Story 003: 心法 SO schema 与克隆隔离

> **Epic**: F4 ScriptableObject 数据层
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: 2026-08-12
> **Last Updated**: —

## Context

**GDD**: `design/gdd/so-data-layer.md`
**Requirement**: `TR-datalayer-003`, `TR-datalayer-007`, `TR-datalayer-006`（iconSprite）
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-D4: SO 数据层
**ADR Decision Summary**: `PerkSO` 是唯一可变 SO——局内经 `Instantiate()` 克隆后修改（D4-2）；`PerkParameter` 须为 `[Serializable] struct`（值类型），保证克隆深拷贝隔离（D4-9）；`PerkEffectType` 锁定 5 值枚举，加第 6 值须 ADR 引用否则 CI fail。

**Engine**: Unity 6.3 LTS (6000.3.19f1) | **Risk**: MEDIUM
**Engine Notes**: `ScriptableObject.Instantiate()` 对值类型字段做深拷贝，对引用类型仅浅拷贝——故 `PerkParameter` 必须是 struct。List<struct> 元素不支持 `list[i].field = x` 直接编译，须经局部变量 + 索引器回写。

**Control Manifest Rules (this layer)**:
- Required: `PerkParameter` 为 `[Serializable] struct`（值类型不变式，D4-9）；局内修改经 `Instantiate()` 克隆体，不改原始资产
- Forbidden: `PerkParameter` 用 class（引用类型会导致克隆共享同一实例，破坏隔离）
- Guardrail: Odin 双模式验证（内联 Plan A + 外部 Plan B）

---

## Acceptance Criteria

*From GDD `design/gdd/so-data-layer.md`, scoped to this story:*

- [ ] **AC-F4-03**（BLOCKING）: `PerkParameter.IsValueType` 前置断言；克隆体经"槽位替换"与"struct 字段原地改写回"两路径修改后，原始 `PerkSO.parameters[0].value` 均不变，`ReferenceEquals(original, clone)==false`
- [ ] **AC-F4-04**（BLOCKING）: 3 个原始 PerkSO 各自 `Instantiate()` → 3 克隆体，每个 `ReferenceEquals=false`，id/rarity/effectType 与原始一致，改克隆 `parameters[0].value` 后原始不变
- [ ] **AC-F4-13**: `displayName=""`、`iconSprite=null`、`descriptionTemplate=""` → 报 3 条 Error

---

## Implementation Notes

*Derived from ADR-D4 Implementation Guidelines:*

- `PerkSO` 字段：`id`、`displayName`、`iconSprite`（`AssetReferenceT<Sprite>` 或 Sprite 引用）、`descriptionTemplate`、`rarity`、`effectType`（`PerkEffectType` 5 值枚举）、`parameters`（`List<PerkParameter>` 或 `PerkParameter[]`）。
- **`PerkParameter` 必须声明为 `[Serializable] struct`**（D4-9 值类型不变式）——含 `key`（string）、`value`（float）等。
- 局内 roguelike 修改流程：`Instantiate(perkSO)` 得克隆体后修改克隆体的 parameters，永不改原始磁盘资产。
- List<struct> 回写模式：`var p = clone.parameters[0]; p.value = x; clone.parameters[0] = p;`（不可 `clone.parameters[0].value = x` 直接改，struct 元素编译报错）。
- `PerkEffectType` 枚举锁定 5 值；扩展须走 ADR 引用（Story 007 CI 门控会检测未授权扩展）。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: 程序集与 Addressable Group 骨架
- Story 007: PerkSO 重复 id 检测（AC-F4-24）、PerkEffectType 枚举门控
- iconSprite 实际运行时加载（C3 拥有）

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

- **AC-F4-03**: PerkParameter 为值类型；克隆体与原始体经槽位替换、原地修改两路径均互不影响（BLOCKING）
  - Given: 前置断言 `typeof(PerkParameter).IsValueType == true`；CreateInstance<PerkSO> 创建 original，设置 original.parameters[0].value=1.0f；调用 Instantiate(original) 得到 clone
  - When: 路径 A — `clone.parameters[0] = new PerkParameter { value = 99f }`（槽位替换）；路径 B — 取出 clone.parameters[0] 到局部变量，修改 value=88f 后写回 clone.parameters[0]（原地字段改写回）
  - Then: `ReferenceEquals(original, clone) == false`；执行路径 A 后 original.parameters[0].value 仍为 1.0f；执行路径 B 后 original.parameters[0].value 仍为 1.0f
  - Edge cases: parameters 若为 List<PerkParameter>，须验证写回语义使用显式局部变量+索引器赋值（struct 元素不支持 `list[i].field = x` 直接编译）；parameters 若为数组 PerkParameter[] 需等价验证

- **AC-F4-04**: 3 个原始 PerkSO 各自克隆，ReferenceEquals=false，标识字段一致，克隆互不影响
  - Given: 3 个不同的原始 PerkSO 资产（不同 id/rarity/effectType）
  - When: 对每个原始资产分别调用 Instantiate() 得到 3 个克隆体
  - Then: 每个克隆体与其原始体 ReferenceEquals==false；每个克隆体的 id/rarity/effectType 与对应原始体一致；修改任一克隆体的 parameters[0].value 后，其余克隆体及全部原始体的对应字段均不变
  - Edge cases: 对同一原始体重复调用 Instantiate() 两次得到的两个克隆体互相之间也须隔离（clone1 修改不影响 clone2）

- **AC-F4-13**: `displayName=""`、`iconSprite=null`、`descriptionTemplate=""` → 报 3 条 Error
  - Given: PerkSO 实例，设置 displayName="", iconSprite=null, descriptionTemplate=""
  - When: 执行 Odin Validator 全字段校验
  - Then: 校验结果恰好包含 3 条 Error，分别对应 displayName / iconSprite / descriptionTemplate
  - Edge cases: displayName 为空白字符串（如 " "）是否报错待与 GDD 作者确认，测试暂只覆盖空字符串 ""

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- `Assets/Tests/EditMode/F4DataLayer/PerkSO_CloneImmutabilityTest.cs` — 须存在且通过（AC-F4-03）
- `Assets/Tests/EditMode/F4DataLayer/PerkSO_CloneLifecycleTest.cs` — 须存在且通过（AC-F4-04）
- `Assets/Tests/EditMode/F4DataLayer/PerkSchema_test.cs` — schema 验证（AC-F4-13）

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001（程序集与 Addressable Group）
- Unlocks: Story 004（后续 SO schema 复用 struct/克隆模式约定）、Story 007
