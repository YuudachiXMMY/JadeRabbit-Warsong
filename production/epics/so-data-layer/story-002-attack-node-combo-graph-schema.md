# Story 002: 攻击节点与连招图 SO schema

> **Epic**: F4 ScriptableObject 数据层
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: 2026-08-12
> **Last Updated**: —

## Context

**GDD**: `design/gdd/so-data-layer.md`
**Requirement**: `TR-datalayer-001`（partial）, `TR-datalayer-006`（animationClip）
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-D4: SO 数据层
**ADR Decision Summary**: SO 运行时只读（D4-1）；`[SerializeField]` 仅字段，无可变属性 setter；AnimationClip 等大资源用 `AssetReferenceT<T>` 惰性引用，加载契约由 C2 拥有（D4-3/D4-4）。

**Engine**: Unity 6.3 LTS (6000.3.19f1) | **Risk**: MEDIUM
**Engine Notes**: Unity 6.3 起 `SerializeField` 仅限字段（不能标注属性）。反射不可变性测试须用 `AssetDatabase.DiscardAllChanges()` 还原，禁用 `EditorUtility.IsDirty()`（无 `Undo.RecordObject` 时对反射写入恒为 false）。

**Control Manifest Rules (this layer)**:
- Required: SO 字段用 `[SerializeField] private` + 只读 public getter；运行时不可变（D4-1）
- Forbidden: 硬编码攻击动画帧数（须经 Animancer `AnimancerState.Speed` 按 BPM 缩放）
- Guardrail: Odin 双模式验证——内联 `[ValidateInput]`（Plan A）+ 外部 `[RegisterValidator]`（Plan B，Story 007/008）

---

## Acceptance Criteria

*From GDD `design/gdd/so-data-layer.md`, scoped to this story:*

- [ ] **AC-F4-02a**（BLOCKING）: 反射遍历 AttackNodeSO 的 float/int/string 可序列化字段，SetValue 写入不同值后再读取——任何字段写入后值≠快照则失败；测试结束调用 `AssetDatabase.DiscardAllChanges()`；禁用 `EditorUtility.IsDirty()` 检测
- [ ] **AC-F4-32**（BLOCKING）: `hitboxDurationNormalized=0.0` 时 Odin Validator 报 Error（须用 `value > 0.0f` 严格比较，非 `[MinValue(0)]`）；`0.01` 须通过
- [ ] **AC-F4-10**: `id=""`、`animationClip=null`、`hitboxStartNormalizedTime=1.1`、`cancelWeight=1.5`、`authoredBpm=0` → 报 5 条 Error
- [ ] **AC-F4-11**: `ComboGraphSO.nodes` 空列表 → 报 Error 阻止构建

---

## Implementation Notes

*Derived from ADR-D4 Implementation Guidelines:*

- `AttackNodeSO` 字段：`id`（string）、`animationClip`（`AssetReferenceT<AnimationClip>`）、`authoredBpm`、`hitboxStartNormalizedTime`、`hitboxDurationNormalized`、`cancelWeight` 等，全部 `[SerializeField] private` + 只读 getter。
- `ComboGraphSO` 含 `nodes`（`List<AttackNodeSO>`）与 `edges`（`List<ComboEdge>`）。
- **`ComboEdge` 用 string ID（GDD Round-6 权威，取代 ADR-D4 Key Interfaces 中过时的 `int fromNodeIndex`）**：字段 `fromNodeId`/`toNodeId`（string），`requiredInput` 为 F3 权威枚举 `AttackType {LightAttack, HeavyAttack, ChargedAttack}`。ComboEdge ID 引用完整性校验在 Story 008（AC-F4-31）。
- `hitboxDurationNormalized` 用自定义 `[ValidateInput]` 严格 `value > 0.0f`，**不用 `[MinValue(0)]`**（会把 0.0 判为合法）。
- 内联验证（Plan A）用于设计师 UX，不阻塞 CI；CI 门控由 Plan B 外部 Validator 承担（Story 007/008）。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: 程序集与 Addressable Group 骨架
- Story 007: 重复 id 检测（AttackNodeSO/ComboGraphSO 各自 AC-F4-26/27）
- Story 008: ComboEdge ID 引用完整性（AC-F4-31）、hitbox 时间窗跨字段和越界（AC-F4-25）
- AnimationClip 实际运行时加载（C2 拥有）

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

- **AC-F4-02a**: 反射快照比较验证字段只读契约（BLOCKING）
  - Given: 已从 AssetDatabase 加载一个 AttackNodeSO 资产实例；已通过反射枚举其全部 float/int/string 可序列化字段并记录当前值作为快照 snapshot
  - When: 对每个快照字段使用 FieldInfo.SetValue 写入与当前值不同的测试值，随后立即通过 FieldInfo.GetValue 读回该字段
  - Then: 读回值须等于快照原值方为通过；只要有一个字段读回值 ≠ 快照原值即判定测试失败并报出字段名列表；测试结束时无条件（try/finally）调用 `AssetDatabase.DiscardAllChanges()` 还原磁盘资产，禁止使用 `EditorUtility.IsDirty()` 作为判定依据（该 API 在无 `Undo.RecordObject` 时对反射写入恒为 false）
  - Edge cases: enum 字段（底层 int）须纳入 int 类别一并测试；字符串字段写入 null 而非新字符串的场景；断言失败时也必须执行 DiscardAllChanges，避免脏资产污染后续测试

- **AC-F4-32**: `hitboxDurationNormalized=0.0` 须报 Error（严格 `value > 0.0f` 比较，非 `[MinValue(0)]`）；0.01 须通过（BLOCKING）
  - Given: AttackNodeSO 实例，其余必填字段均为合法值
  - When: 设置 hitboxDurationNormalized=0.0f，触发 Odin Validator 校验（自定义 ValidateInput，非 `[MinValue(0)]` 特性）
  - Then: 校验结果包含至少一条 Error，字段路径为 hitboxDurationNormalized
  - Edge cases: 对照组 hitboxDurationNormalized=0.01f 须无 Error；负值（如 -0.1）同样须报 Error；测试须反射断言字段未标注 `[MinValue]`，以证明校验器未采用会把 0.0 判定为合法的宽松特性

- **AC-F4-10**: `id=""`、`animationClip=null`、`hitboxStartNormalizedTime=1.1`、`cancelWeight=1.5`、`authoredBpm=0` → 报 5 条 Error
  - Given: AttackNodeSO 实例，设置 id="", animationClip=null, hitboxStartNormalizedTime=1.1, cancelWeight=1.5, authoredBpm=0
  - When: 执行 Odin Validator 全字段校验
  - Then: 校验结果恰好包含 5 条 Error，分别对应 id / animationClip / hitboxStartNormalizedTime / cancelWeight / authoredBpm
  - Edge cases: 单独修正任一非法字段为合法值后 Error 计数应降为 4（隔离性验证）；hitboxStartNormalizedTime 边界值 1.0 合法，1.000001 报错

- **AC-F4-11**: `ComboGraphSO.nodes` 空列表 → 报 Error 阻止构建
  - Given: ComboGraphSO 实例，nodes 为空 List<AttackNodeSO>（Count==0）
  - When: 执行 Odin Validator 校验
  - Then: 校验结果包含至少一条 Error，字段路径为 nodes，错误信息指出节点列表不可为空
  - Edge cases: nodes 含 1 个非 null 元素时该项校验通过；nodes 字段为 null（而非空列表）同样须报错，且校验器不因空引用异常崩溃

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- `Assets/Tests/EditMode/F4DataLayer/SO_ImmutabilityTest.cs` — 须存在且通过（AC-F4-02a 反射不可变性）
- `Assets/Tests/EditMode/F4DataLayer/AttackNodeSchema_test.cs` — schema 验证（AC-F4-32/10/11）

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001（程序集与 Addressable Group）
- Unlocks: Story 004（WeaponSO 引用 ComboGraphSO）、Story 007、Story 008
