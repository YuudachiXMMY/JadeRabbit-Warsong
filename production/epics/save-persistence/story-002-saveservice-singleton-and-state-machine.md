# Story 002: SaveService 单例骨架与状态机

> **Epic**: F5 存档/持久化系统
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: [fill before sprint planning]
> **Manifest Version**: 2026-08-12
> **Last Updated**: [set by /dev-story when implementation begins]

## Context

**GDD**: `design/gdd/save-persistence.md`
**Requirement**: `TR-save-001`, `TR-save-009`, `TR-save-010`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-D7: 存档/持久化中间件（primary）；ADR-D10: Bootstrap 顺序（secondary）
**ADR Decision Summary**: `SaveService` 是全局唯一持久化网关（`MonoBehaviour` 单例，`DontDestroyOnLoad`）；`Awake()` 仅注册单例 + 捕获主线程 ID + 注入默认 `IFileStorage`，不加载（ADR-D10 Rule 14）。

**Engine**: Unity 6.3 LTS (6000.3.19f1) | **Risk**: LOW
**Engine Notes**: Unity 6 默认关闭 Domain Reload（Enter Play Mode 优化），须实现 `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` 静态重置，否则第二次进入 Play Mode 访问已销毁引用。标准 `MonoBehaviour` 生命周期，无 post-cutoff API。

**Control Manifest Rules (this layer)**:
- Required: Awake 阶段 `SaveService.Awake()` 仅注册单例，不加载（Rule 14）（ADR-D10）
- Required: 消费者面向 `ISaveService` 与 F5 内部 `IFileStorage` 并存，角色不同（ADR-D10）
- Forbidden: 在 `ES3Storage` 之外的 runtime 代码引用 `ES3.*`（ADR-D7）
- Guardrail: `SaveService.Initialize()` bootstrap 阻塞加载 < 50ms PC / < 100ms Mobile（本 story 只建骨架，加载在 003）

---

## Acceptance Criteria

*From GDD `design/gdd/save-persistence.md`, scoped to this story:*

- [ ] `SaveService` 为 `MonoBehaviour` 单例，`Awake()` 中 `_instance = this` + `DontDestroyOnLoad`（Rule 1、Rule 14）
- [ ] `Awake()` 仅完成三项：单例注册、主线程 ID 捕获、注入默认 `IFileStorage`（`InjectStorage(new ES3Storage())`）；**不**调用 `Initialize()`（Rule 14）
- [ ] `_mainThreadId` 通过 `Thread.CurrentThread.ManagedThreadId` 在 `Awake()` 捕获为私有静态字段（Rule 13）；不得用 `IsBackground`/`Name` 判断
- [ ] 实现 `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` 静态重置方法，清零 `_instance`、`_mainThreadId` 及其他静态字段（Rule 12）
- [ ] 状态机定义 5 态 `{Uninitialized, Idle, Saving, Loading, Error}`，初始为 `Uninitialized`
- [ ] `InjectStorage(IFileStorage)` 在 `Initialize()` 读取前调用；`Initialize()` 完成后再调用须抛 `InvalidOperationException`（注入时序错误，Rule 11）
- [ ] **AC-F5-21**：`Uninitialized` 状态下调用只读 API（如 `GetAllSlotMeta()`）抛 `InvalidOperationException`（消息含 "not initialized" 或等效描述）

---

## Implementation Notes

*Derived from ADR-D7 / ADR-D10 Implementation Guidelines:*

- 文件放置：`Assets/Scripts/Core/Persistence/SaveService.cs`（manifest 强制）。
- `Awake()` 顺序：(a) 单例守卫（若 `_instance != null && _instance != this` 则销毁重复实例）→ `_instance = this` + `DontDestroyOnLoad(gameObject)`；(b) `_mainThreadId = Thread.CurrentThread.ManagedThreadId`；(c) `InjectStorage(new ES3Storage())`。
- 静态重置方法必须重置**所有**静态字段，尤其 `_mainThreadId`（否则跨 Play Mode 保留旧线程 ID 导致主线程判定错误）。
- `InjectStorage` 内部：若 `state != Uninitialized` 抛 `InvalidOperationException`（表示 `Initialize()` 已跑，注入太晚）。生产 Awake 注入与测试 `[SetUp]` 注入都在 `Initialize()` 之前，合法。
- 状态转换的其余部分（`Uninitialized→Loading→Idle`、错误转换）在下游 story 落地；本 story 只需状态字段 + `Uninitialized` 守卫 + 只读 API 的 not-initialized 抛异常。
- 定义 `class SaveService : ISaveService`（接口 body 在 story 003 补全成员，本 story 可先声明骨架方法）。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001：DTO 与 `IFileStorage`/`ES3Storage`/`MockFileStorage` 实现（本 story 依赖其存在）
- Story 003：`Initialize()` 实际加载逻辑、`Loading→Idle` 转换、`ISaveService` 成员实现、`MockSaveService`
- Story 006：错误状态转换与 `OnSaveError`
- Story 008：非主线程调用的 `ConcurrentQueue` 缓存 + flush（本 story 仅捕获 `_mainThreadId`）

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

- **AC-1（单例唯一性）**: 只有一个 `SaveService.Instance`
  - Given: 场景中放置两个 `SaveService`
  - When: 二者 `Awake()` 执行
  - Then: `SaveService.Instance` 唯一，重复实例被销毁
  - Edge cases: `DontDestroyOnLoad` 后跨场景切换实例保持不变

- **AC-2（Awake 不加载）**: `Awake()` 后状态仍为 `Uninitialized`
  - Given: `SaveService` 完成 `Awake()`
  - When: 检查 `state` 与 `IsInitialized`
  - Then: `state == Uninitialized`，`IsInitialized == false`（Rule 14：Awake 不 Initialize）
  - Edge cases: `Awake()` 后默认 `IFileStorage` 已注入（非 null）

- **AC-3（Domain Reload 静态重置）**: 二次进入 Play Mode 不残留旧引用
  - Given: 关闭 Domain Reload 的 Play Mode 设置
  - When: 触发 `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` 重置
  - Then: `_instance == null` 且 `_mainThreadId` 归零
  - Edge cases: 第二次进入 Play Mode 后 `SaveService` 能重新 Awake 并正常初始化，不停留在 `Uninitialized`

- **AC-4（注入时序守卫）**: `Initialize()` 后注入抛异常
  - Given: `SaveService` 已 `Initialize()`（状态非 `Uninitialized`）
  - When: 调用 `InjectStorage(new MockFileStorage())`
  - Then: 抛 `InvalidOperationException`
  - Edge cases: `Uninitialized` 状态下注入合法，不抛异常

- **AC-5 (AC-F5-21)（未初始化只读抛异常）**: `Initialize()` 前调用只读 API 抛异常
  - Given: 已 `InjectStorage(MockFileStorage)` 但未 `Initialize()`（`Uninitialized`）
  - When: 调用 `GetAllSlotMeta()`
  - Then: 抛 `InvalidOperationException`，消息含 "not initialized" 或等效描述
  - Edge cases: 其他只读方法（如 `LoadSlotProgress`）在 `Uninitialized` 下同样抛异常

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- `Assets/Tests/EditMode/Save/SaveServiceLifecycleTests.cs` — 单例/Awake/注入守卫/未初始化抛异常，必须通过
- Domain Reload 重置：EditMode 可验证静态方法效果；跨 Play Mode 行为以 PlayMode 测试或 playtest 文档补充

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001（DONE）
- Unlocks: Story 003, Story 008
