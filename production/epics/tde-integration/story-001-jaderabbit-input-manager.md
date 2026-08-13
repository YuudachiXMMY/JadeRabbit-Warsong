# Story 001: JadeRabbitInputManager — Action Map 代理 + AbilityInputAuthorization 保留

> **Epic**: F6 TopDownEngine & Animancer 集成层
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: S
> **Manifest Version**: 2026-08-12
> **Last Updated**: [set by /dev-story when implementation begins]

## Context

**GDD**: `design/gdd/tde-integration.md`
**Requirement**: `TR-tde-001`（双组件架构之输入半），`TR-tde-002`（TDE 源码禁改，约束）
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-D1: TDE 集成
**ADR Decision Summary**: `JadeRabbitInputManager`（`InputSystemManagerEventsBased` 子类）将所有 Action Map 状态变更代理给 F3，保留 TDE 的 `AbilityInputAuthorization` 管线；一切集成经继承/事件实现，禁改 TDE 源码。

**Engine**: Unity 6.3 LTS (6000.3.19f1) | **Risk**: MEDIUM
**Engine Notes**: TDE `InputSystemManagerEventsBased` / `AbilityInputAuthorization` 为后 LLM-cutoff 的 TDE 插件 API——继承点与保留管线须对照 `docs/engine-reference/unity/plugins/top-down-engine.md`，严禁从训练数据推断成员名。

**Control Manifest Rules (Foundation)**:
- Required: 一切集成经继承 / 事件订阅 / Inspector 配置（禁改 TDE 源码）— ADR-D1/D2
- Forbidden: `JadeRabbitInputManager` 禁止直接调用 `InputActionMap.Enable()/Disable()`——F3（`InputEventBus`）是 Action Map 唯一权威
- Guardrail: 输入代理路径为纯事件转发，无 `Update()` 轮询成本

---

## Acceptance Criteria

*From GDD `design/gdd/tde-integration.md`, scoped to this story:*

- [ ] AC-F6-01：`JadeRabbitInputManager` 不直接操作 Action Map（无 `InputActionMap.Enable()/Disable()` 调用）
- [ ] AC-F6-02：`JadeRabbitInputManager` 保留 TDE `AbilityInputAuthorization` 管线（Ability 持续期攻击输入被拦截，结束后恢复）

---

## Implementation Notes

*Derived from ADR-D1 + GDD Core Rule 1/3:*

- `JadeRabbitInputManager : InputSystemManagerEventsBased`（TDE 类），重写 `BindButton()` 将所有按钮输入代理给 F3 状态机门控。
- 保留 TDE `AbilityInputAuthorization` 管线——不绕过、不直接 Enable/Disable Action Map，这些操作由 F3 独家执行。
- 组件挂在角色 Prefab 上（非 DontDestroyOnLoad Manager，那是 `TDELifecycleAdapter`）。
- 禁改 TDE 源码：仅通过继承 + override 扩展。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002：`TDELifecycleAdapter` 骨架与生命周期控制（双组件的另一半）
- F3 `InputEventBus` 自身实现（属 F3 InputEventBus epic）

---

## QA Test Cases

*Embedded from GDD AC-F6-01 / AC-F6-02（lean 模式：GDD AC 即测试规格）。开发者据此实现，勿在实现期另造测试。*

**AC-1（AC-F6-01，Logic — EditMode 静态 + PlayMode 运行时）**
  - Given：`JadeRabbitInputManager` 类型
  - When：EditMode 经 `System.Reflection` 枚举全部 `MethodInfo`，读取方法 IL（`MethodBody.GetILAsByteArray()`）；或 Roslyn Analyzer 编译期静态检查（推荐，可入 CI）
  - Then：断言不存在对 `InputActionMap.Enable()/Disable()` 的调用（按方法 token 匹配）
  - Edge cases：运行时路径——注入 `IInputEventBus` mock，触发攻击输入后断言 mock 收到 `OnAttackInput` 代理调用，且 mock 未记录任何 `InputActionMap` 直接操作

**AC-2（AC-F6-02，Integration — PlayMode 手动验证）**
  - Given：角色已配置 `JadeRabbitInputManager` + TDE `AbilityInputAuthorization`
  - When：触发需要 `AbilityInputAuthorization` 锁的 TDE Ability（如 Dash）
  - Then：Ability 持续期间攻击 `OnAttackInput` 未触发（输入被 TDE 管线拦截）；Ability 结束后攻击输入经 F3 正常发布；TDE Ability 状态机流程无破坏
  - Edge cases：Ability 期间连续多次攻击输入均被拦截，无泄漏

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- `Assets/Tests/EditMode/F6/JadeRabbitInputManager_test.cs`（AC-01 静态 IL / Roslyn 分析）— 必须存在且通过
- `Assets/Tests/PlayMode/F6/JadeRabbitInputManager_test.cs`（AC-01 运行时代理路径 + AC-02 Ability 拦截）— 必须存在且通过 OR playtest 文档

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: None（F3 `InputEventBus` 接口须先定义；跨 epic 依赖，非本 epic story）
- Unlocks: C2 连招图系统消费输入代理（跨 epic）
