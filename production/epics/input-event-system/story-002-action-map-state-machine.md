# Story 002: Action Map 三态状态机

> **Epic**: F3 Input 事件系统
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: L
> **Manifest Version**: 2026-08-12
> **Last Updated**: [set by /dev-story when implementation begins]

## Context

**GDD**: `design/gdd/input-event-system.md`
**Requirement**: `TR-input-004`, `TR-input-014`, `TR-input-010`（F3 侧；E8 完整解决属 F6 `TDELifecycleAdapter`）
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-D1: TDE 集成架构（primary）；ADR-D8: DI 缝隙（次要，控制面/消费面分层）
**ADR Decision Summary**: F3（`InputEventBus`）是所有 Action Map 状态变更的唯一权威。`JadeRabbitInputManager` 继承 TDE `InputSystemManagerEventsBased` 并 override `BindButton` 作为输入门（`CurrentState != GameplayActive` → drop），保留 `AbilityInputAuthorization` 管线。`TDELifecycleAdapter` 是 `EnableGameplayInput()`/`EnableUIInput()`/`DisableAllInput()` 的唯一外部调用者。

**Engine**: Unity 6.3 LTS (6000.3.19f1) | **Risk**: HIGH
**Engine Notes**: M0 Spike（2026-07-16）已验证 `InputSystemManagerEventsBased` 仅暴露一个虚方法 `BindButton(InputAction.CallbackContext, MMInput.IMButton)`；`SetInputEnabled`/`HandleInput` **不存在**。禁止从训练数据臆测 TDE 成员名。`InputActionMap.Enable()/Disable()` 由 F3 直接调用（非经 TDE 输入管理器）。

**Control Manifest Rules (this layer)**:
- Required: `F3 (InputEventBus) is the sole authority over all Action Map state changes`（ADR-D1）；`BeatClock.Pause() 与 F3.EnableUIInput() 必须同帧原子触发`（Resume/EnableGameplayInput 同理，ADR-D1）
- Forbidden: `Never let TDE be the Action Map authority with F3 passively syncing`（`_isSwitchingMaps` 必须在 `gameplayMap.Disable()` 之前置位）；`Never split Action Map authority by lifecycle phase`；`Never remove InputSystemManagerEventsBased entirely`（破坏 `AbilityInputAuthorization`）
- Guardrail: boot 序列 `BeatClock.Play` + `EnableGameplayInput` 总计 < 1ms

---

## Acceptance Criteria

*From GDD `design/gdd/input-event-system.md`, scoped to this story:*

- [ ] AC-F3-20：`InputEventBus.Awake()` 后初始状态为 `AllDisabled`——`EnableGameplayInput()` 显式调用前，任何硬件输入均不触发 `OnAttackInput`/`OnDashInput`/`OnMoveChanged`；调用后恢复广播
- [ ] AC-F3-09：`EnableGameplayInput()`（从 `AllDisabled`）→ Gameplay Map 启用、UI Map 禁用，攻击/移动/闪避输入恢复广播
- [ ] AC-F3-07：`EnableUIInput()`（从 `GameplayActive`）→ Gameplay Map 禁用、UI Map 启用；此后 Gameplay 输入不产生事件，UI 导航输入正常广播
- [ ] AC-F3-21：`EnableGameplayInput()`（从 `UIActive`，暂停→恢复路径）→ Gameplay Map 启用、UI Map 禁用；攻击输入恢复广播，UI Navigate 不再产生事件
- [ ] AC-F3-08：`DisableAllInput()` → 两个 Map 均禁用，任何输入均不产生事件
- [ ] `EnableUIInput()` 与 `DisableAllInput()` 均遵守 `_isSwitchingMaps` 三步时序：① `_isSwitchingMaps = true` → ② `gameplayMap.Disable()`（同步触发 active interaction 的 `canceled`）→ ③ `Disable()` 返回后 `_isSwitchingMaps = false`

---

## Implementation Notes

*Derived from ADR-D1 Decision (Action Map Authority) 与 GDD Core Rule 5：*

- 三态：`GameplayActive` / `UIActive` / `AllDisabled`（GDD States and Transitions 表）。初始态 `AllDisabled` 在 `Awake()` 设定——防止 TDE `InputSystemManagerEventsBased` 与 F3 的 Awake 时序竞争导致状态失同步。
- `JadeRabbitInputManager : InputSystemManagerEventsBased`，override `BindButton`：`if (CurrentState != GameplayActive) return;` 门控输入，`base.BindButton(...)` 保留 TDE 能力授权管线。**禁止**移除 `InputSystemManagerEventsBased`（破坏 `AbilityInputAuthorization`）。
- `EnableUIInput()` 与 `DisableAllInput()` **必须使用相同的三步 `_isSwitchingMaps` 时序**（GDD Core Rule 5 + E3 ⚠ 警告）——`DisableAllInput()` 若无此保护，玩家充能中过场/死亡会向 C1 广播幽灵 LightAttack。二者与 `EnableUIInput()` 完全对称。
- `_isSwitchingMaps` 必须在 `gameplayMap.Disable()` **之前**置位：Unity Input System 在 `Disable()` 调用栈内同步触发所有 active interaction 的 `canceled` 回调，回调在 `Disable()` 返回前完成。被动同步（先 Disable 后学到）无法保护 E3——这是 ADR-D1 拒绝 Alternative 2 的核心理由。
- `TDELifecycleAdapter` 是三个状态方法的唯一外部调用者（场景加载完成 → `EnableGameplayInput()`；暂停/死亡/过场 → `EnableUIInput()`/`DisableAllInput()`）。F3 不主动通知中断，调用方拥有完整控制权（GDD E3 调用方合约）。
- 暂停/恢复的原子性：`BeatClock.Pause()` 与 `EnableUIInput()` 同帧触发（Resume 同理，manifest Required）。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001：单例、`IInputEventBus` 接口与事件负载结构（本 story 依赖其存在）
- Story 004：`_isSwitchingMaps == true` 分支下 `canceled` 回调的 E3 静默丢失逻辑（本 story 只负责在切换期间正确置位/清除标志；E3 三路检查的验证在 Story 004）
- F6：`TDELifecycleAdapter` 的场景加载/生命周期事件订阅与 FMOD 就绪探测（TR-input-010 完整解决、TR-input-015）

---

## QA Test Cases

*Lean 模式无 qa-lead 生成；以下转写自 GDD 对应 AC 的「验证方法」，实现时对齐这些用例。*

- **AC-1 (AC-F3-20)**: Awake 初始态 AllDisabled
  - Setup: PlayMode，实例化 `InputEventBus` 后**不**调用 `EnableGameplayInput()`
  - When: 发送攻击/移动输入
  - Then: `OnAttackInput`/`OnDashInput`/`OnMoveChanged` 三者均未触发；随后调用 `EnableGameplayInput()` 再发送输入，事件正常广播
- **AC-2 (AC-F3-09)**: AllDisabled → GameplayActive
  - Setup: PlayMode，`AllDisabled` 态
  - When: 调用 `EnableGameplayInput()` 后发送攻击输入
  - Then: `OnAttackInput` 广播（Gameplay Map 启用、UI Map 禁用）
- **AC-3 (AC-F3-07)**: GameplayActive → UIActive
  - Setup: PlayMode，`GameplayActive` 态
  - When: 调用 `EnableUIInput()` 后发送 Gameplay 输入
  - Then: 无 Gameplay 事件广播；UI 导航输入正常广播
- **AC-4 (AC-F3-21)**: UIActive → GameplayActive（暂停恢复）
  - Setup: PlayMode
  - When: (1) `EnableUIInput()` 进入 `UIActive`；(2) 发送攻击输入断言 `OnAttackInput` 未触发；(3) `EnableGameplayInput()`；(4) 再发攻击输入；(5) 发送 UI Navigate
  - Then: 步骤 (4) `OnAttackInput` 触发；步骤 (5) `OnUINavigate` 未触发
- **AC-5 (AC-F3-08)**: DisableAllInput()
  - Setup: PlayMode
  - When: 调用 `DisableAllInput()` 后发送任意输入
  - Then: 任何输入均不产生事件
  - Edge cases: 玩家充能中调用 `DisableAllInput()` 时 `_isSwitchingMaps` 三步时序须生效，不产生幽灵 LightAttack（详细 E3 验证在 Story 004）

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- `Assets/Tests/PlayMode/input/actionmap_statemachine_test.cs` — 覆盖 AC-F3-07/08/09/20/21 的 PlayMode 测试，必须存在且通过

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001（单例与接口骨架必须 DONE）
- Unlocks: Story 003（事件广播需 Gameplay Map 启用）、Story 004（E3 依赖 `_isSwitchingMaps` 三步时序）
