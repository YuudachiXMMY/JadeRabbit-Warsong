# Story 008: 零 GC 分配与订阅者生命周期安全

> **Epic**: F3 Input 事件系统
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: [fill before sprint planning]
> **Manifest Version**: 2026-08-12
> **Last Updated**: [set by /dev-story when implementation begins]

## Context

**GDD**: `design/gdd/input-event-system.md`
**Requirement**: `TR-input-006`, `TR-input-009`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-D8: DI / 确定性测试缝隙契约（primary，事件负载 struct + 订阅面）；ADR-D1: TDE 集成（次要，广播路径归属）
**ADR Decision Summary**: 事件负载为 `readonly struct`（值类型，无装箱）；订阅经方法组（非捕获 lambda）避免闭包分配；`OnDestroy`/`OnDisable` 中对称退订，防止已销毁订阅者被调用（悬空委托）导致的 `MissingReferenceException` 与内存泄漏。

**Engine**: Unity 6.3 LTS (6000.3.19f1) | **Risk**: MEDIUM
**Engine Notes**: `ProfilerRecorder`（`ProfilerCategory.Memory`, "GC Allocated In Frame"）用于断言广播路径零分配。IL2CPP（移动/主机构建）下 `readonly struct` event payload 不装箱；捕获 lambda 与 `Action` 多次 `+=` 会分配。C# event 触发（`Delegate.Invoke`）本身不分配（除非 struct 参数被隐式装箱为 object——须确保 `Action<T>` 泛型精确匹配 struct 类型）。

**Control Manifest Rules (this layer)**:
- Required: 事件负载 `readonly struct`；方法组订阅；订阅者 `OnDestroy`/`OnDisable` 对称退订
- Forbidden: 广播路径堆分配（捕获 lambda、装箱、每帧 `new`）；退订缺失致悬空委托
- Guardrail: 广播路径 `GC Allocated In Frame == 0B`（`ProfilerRecorder` 断言）

---

## Acceptance Criteria

*From GDD `design/gdd/input-event-system.md`, scoped to this story:*

- [ ] AC-F3-11：连续触发 N 次输入事件广播（Attack/Dash/Move），`ProfilerRecorder("GC Allocated In Frame")` 测得广播路径堆分配为 0B（稳态帧，排除首帧 JIT/初始化分配）
- [ ] AC-F3-12：订阅者在 `Destroy` 后不再收到事件；订阅者未退订即被销毁时，广播不抛 `MissingReferenceException`/`NullReferenceException`（对称退订生效或空引用防御）；无委托链泄漏（订阅者可被 GC 回收）

---

## Implementation Notes

*Derived from GDD 非功能需求（性能/生命周期）与 ADR-D8 §2：*

- **零 GC 广播**：事件负载全为 `readonly struct`（Story 001 建立）；`event Action<AttackInputEvent>` 泛型精确匹配 struct 类型，避免隐式装箱为 `object`。订阅方用方法组（`bus.OnAttackInput += HandleAttack;`），**禁止**捕获 lambda（`+= e => { ... }` 分配闭包）。广播路径不 `new` 引用类型、不 LINQ、不字符串拼接。
- **零 GC 测量**：测试用 `ProfilerRecorder(ProfilerCategory.Memory, "GC Allocated In Frame")`，先跑若干预热帧（排除首帧 JIT/委托首次分配），再在稳态连续广播 N 次，断言 recorder 采样 == 0。测量须在 Player 或 IL2CPP 构建语义下有意义（Editor Mono 分配特征不同——注明测量环境）。
- **对称退订**：订阅者 MonoBehaviour 在 `OnEnable`/`Awake` 订阅，在 `OnDisable`/`OnDestroy` 对称 `-=` 退订。F3 侧广播前可选空引用防御（但根因治理靠订阅者对称退订，不靠 F3 兜底吞异常——吞异常违背"不静默失败"）。
- **生命周期安全（AC-F3-12）**：验证 `Destroy` 后的订阅者不再被调用；未退订即销毁时广播不抛异常且委托链不泄漏（用弱引用探测或 `GC.Collect` 后确认订阅者被回收）。
- **与 Story 001 关系**：Story 001 保证结构为 `readonly struct` 无引用字段（静态/编译期）；本 story 保证**运行时**广播路径零分配 + 生命周期安全（动态/Profiler 测量）。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001：`readonly struct` 结构定义与编译期无引用字段断言（本 story 做运行时分配测量）
- Story 003/005/007：具体事件的广播逻辑（本 story 对已实现广播路径做零 GC + 生命周期验证）

---

## QA Test Cases

*Lean 模式无 qa-lead 生成；以下转写自 GDD 对应 AC 的「验证方法」，实现时对齐这些用例。*

- **AC-1 (AC-F3-11)**: 广播路径零 GC
  - Setup: PlayMode（IL2CPP 语义优先），`GameplayActive`，`ProfilerRecorder("GC Allocated In Frame")`，方法组订阅者
  - When: 预热若干帧后，稳态连续触发 N 次 Attack/Dash/Move 广播
  - Then: recorder 采样堆分配 == 0B（稳态帧）
  - Edge cases: 排除首帧 JIT/委托首次分配；注明 Editor Mono vs 构建 IL2CPP 测量差异
- **AC-2 (AC-F3-12)**: 订阅者生命周期安全
  - Setup: PlayMode，一个订阅了 `OnAttackInput` 的 MonoBehaviour 订阅者
  - When: (1) `Destroy` 订阅者（已对称退订）后触发广播；(2) 另一订阅者未退订即 `Destroy` 后触发广播
  - Then: (1) 已销毁订阅者不再收到事件；(2) 广播不抛 `MissingReferenceException`/`NullReferenceException`；委托链无泄漏（订阅者可被 GC 回收）
  - Edge cases: 未退订销毁不得使后续订阅者漏收事件（异常不得中断广播循环）

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- `Assets/Tests/PlayMode/input/zero_gc_lifecycle_test.cs` — 覆盖 AC-F3-11/12 的 PlayMode 测试（`ProfilerRecorder` 零分配断言 + 生命周期安全），必须存在且通过

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 003（广播路径须存在方可测量零 GC）；隐含依赖 Story 001（`readonly struct` 负载）
- Unlocks: None
