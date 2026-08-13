# Story 001: InputEventBus 单例骨架、接口与事件负载结构

> **Epic**: F3 Input 事件系统
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: M
> **Manifest Version**: 2026-08-12
> **Last Updated**: [set by /dev-story when implementation begins]

## Context

**GDD**: `design/gdd/input-event-system.md`
**Requirement**: `TR-input-001`, `TR-input-003`, `TR-input-012`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-D8: DI / 确定性测试缝隙契约（primary）；ADR-D1: TDE 集成（次要，F3 单例归属）；ADR-D6: 音频中间件（次要，`IDSPClock` 生产实现）
**ADR Decision Summary**: ADR-D8 统一声明并拥有 `IInputEventBus` 消费者读取面（`OnAttackInput`/`OnDashInput`/`OnPause`/`GetCurrentMoveInput()`/`IsChargedAttackHeld`），状态控制方法不进消费者面。生产实现 `InputEventBus : IInputEventBus` 为 F3 的 `MonoBehaviour` 单例、`DontDestroyOnLoad`。

**Engine**: Unity 6.3 LTS (6000.3.19f1) | **Risk**: LOW
**Engine Notes**: 纯 C# 接口声明 + `[SerializeField] MonoBehaviour` 字段注入 + 属性包装；无 post-cutoff 引擎 API。Unity 6.3 「SerializeField 仅限字段」约束已由 ADR-D8 字段注入范式处理。

**Control Manifest Rules (this layer)**:
- Required: `F3 (InputEventBus) is the sole authority over all Action Map state changes`（ADR-D1）；事件负载为零 GC `readonly struct`
- Forbidden: `Never expose EnableGameplayInput/EnableUIInput/DisableAllInput on the IInputEventBus consumer read face`（`direct_action_map_enable_disable` 禁令，ADR-D8）；`Never inject concrete classes instead of interfaces`（ADR-D8）
- Guardrail: 事件广播路径零堆分配（GC Alloc = 0B）

---

## Acceptance Criteria

*From GDD `design/gdd/input-event-system.md`, scoped to this story:*

- [ ] `IInputEventBus` 接口按 ADR-D8 §2 声明：`event Action<AttackInputEvent> OnAttackInput` / `event Action<DashInputEvent> OnDashInput` / `event Action OnPause` / `Vector2 GetCurrentMoveInput()` / `bool IsChargedAttackHeld { get; }`；状态方法（`EnableGameplayInput`/`EnableUIInput`/`DisableAllInput`）**不**出现在此消费者接口上
- [ ] `AttackInputEvent`、`DashInputEvent`、`MoveChangedEvent` 均为 `readonly struct`，字段仅含值类型（`InputDeviceType` enum、`int deviceId`、`double`、`float`、`Vector2`、`bool`），无引用类型字段
- [ ] `InputEventBus : MonoBehaviour, IInputEventBus` 单例，`DontDestroyOnLoad`；下游禁止直接持有 `PlayerControls`，只能经 `InputEventBus` 订阅
- [ ] AC-F3-19：第二个 `InputEventBus` 对象创建时，`Awake()` 检测到已存在实例 → 输出含 "duplicate"/"already exists" 的 `Debug.LogError` → 对自身 `Destroy(gameObject)`；仅保留最早实例，不产生双重广播
- [ ] `IDSPClock` 依赖经字段注入（`[SerializeField] MonoBehaviour _dspClockRef` + `Awake` 中 `as IDSPClock` 解析 + `??=` 缓存），生产实现为 `FMODDSPClockImpl`（ADR-D6）；提供 `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 的 `internal void SetDspClockForTesting(IDSPClock)` 测试缝隙
- [ ] Runtime 程序集声明 `[assembly: InternalsVisibleTo("<F3 测试 asmdef>")]`，测试程序集才能访问 `internal` 注入 setter（否则 CS0122）

---

## Implementation Notes

*Derived from ADR-D8 §2 / Implementation Guidelines 与 ADR-D1：*

- 接口文件放 `Assets/Scripts/Core/Interfaces/IInputEventBus.cs`（ADR-D8 §2 钦定路径）。消费者面**只读/订阅**，状态控制方法归 F3 控制面，故意排除（编译期阻断 `direct_action_map_enable_disable`）。
- 字段注入 + 属性包装范式（ADR-D8 Implementation Guidelines）：`[SerializeField] private MonoBehaviour _dspClockRef;` → `private void Awake() { _dspClock ??= _dspClockRef as IDSPClock; }`；`#if UNITY_EDITOR` 的 `OnValidate()` 在拖错组件（未实现 `IDSPClock`）时立即 Inspector 报错。
- 测试缝隙仅 `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 暴露；`??=` 缓存保证 `AddComponent` 触发的 `Awake()` 不覆盖已注入 mock。
- **优先纯 C# 逻辑下沉**：可测判定逻辑尽量放无 MonoBehaviour 依赖的纯 C# 类（`public` 构造注入），不受 `InternalsVisibleTo` 约束；setter 注入只用于必须留在 MonoBehaviour 上的残余字段。
- `InputDeviceType` enum：`Keyboard | Gamepad | Touchscreen`（GDD `InputEvent` 结构定义）。
- 事件字段布局须严格照 GDD `InputEvent 核心结构`：`AttackInputEvent` 含 `attackType`/`inputDspTime`/`chargeStartDspTime`/`inputGameTime`/`deviceType`/`deviceId`。
- ⚠ **asmdef 命名待定**：`InternalsVisibleTo` 目标测试 asmdef 名由 `/test-setup` 钦定；ADR-D8 文本示例用 `CombatCore.Tests.EditMode`，但 F3 属 Foundation 层非 CombatCore，须用 F3 自己的 Runtime/测试 asmdef 配对——实现时以 `/test-setup` 落定的实际名为准，勿照抄 CombatCore 名（防 CS0122）。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002：Action Map 三态状态机与 `EnableGameplayInput/EnableUIInput/DisableAllInput` 控制面方法实现
- Story 003：`performed` 回调内的同帧双读时间戳与 Attack 广播逻辑
- Story 008：零 GC 的 `ProfilerRecorder` 断言测试（本 story 只保证结构为 `readonly struct` 无引用字段，不含性能测量）

---

## QA Test Cases

*Lean 模式无 qa-lead 生成；以下转写自 GDD AC-F3-19 的「验证方法」，实现时对齐这些用例。*

- **AC-1 (AC-F3-19)**: `InputEventBus` 单例重复实例防御
  - Setup: PlayMode，场景内已存在一个 `InputEventBus`
  - When: 手动实例化第二个 `InputEventBus` Prefab
  - Then: ① `Debug.LogError` 日志含 "duplicate" 或 "already exists"；② `Object.FindObjectsOfType<InputEventBus>().Length == 1`；③ 触发攻击输入后 `OnAttackInput` 仅收到一次事件
  - Edge cases: 第二实例在同帧 `Awake` 内自毁；早创建的实例保留

- **编译期结构断言**: `AttackInputEvent`/`DashInputEvent`/`MoveChangedEvent` 为 `readonly struct` 且无引用类型字段（用反射或编译期检查；`MoveChangedEvent` 另须无 `inputDspTime` 字段——见 AC-F3-05）

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- `Assets/Tests/PlayMode/input/inputeventbus_singleton_test.cs` — 单例重复防御测试，必须存在且通过
- 可选：EditMode 编译期结构断言（`readonly struct` / 无引用字段）

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: None
- Unlocks: Story 002（状态机需要单例与接口骨架）、Story 003/005（事件负载结构）
