# Story 012: 订阅者生命周期合约 + 零 GC 事件卫生

> **Epic**: F2 音频/拍点时钟系统
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: S
> **Manifest Version**: 2026-08-12
> **Last Updated**: [set by /dev-story when implementation begins]

## Context

**GDD**: `design/gdd/audio-beat-clock.md`
**Requirement**: `TR-beatclock-012`(订阅者生命周期合约), `TR-beatclock-016`(零 GC 事件卫生)
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-D8: DI / 测试缝隙
**ADR Decision Summary**: 订阅者须在自身销毁前退订 BeatClock 事件，避免悬挂委托导致内存泄漏与已销毁对象回调；BeatClock 事件系统保证零堆分配（`readonly struct` payload + 直接 `Invoke`）。

**Engine**: Unity 6.3 LTS (6000.3.19f1) | **Risk**: LOW
**Engine Notes**: Unity 中已 `Destroy` 的 MonoBehaviour 若仍在委托链会触发已销毁对象回调/`MissingReferenceException`；退订合约须明确。`DontDestroyOnLoad` 单例跨场景存活，订阅者跨场景切换时尤须退订。

**Control Manifest Rules (this layer)**:
- Required: 事件 payload 全 `readonly struct`；`OnX?.Invoke(data)` 直接调用（禁 `GetInvocationList()` 分配）
- Forbidden: 广播路径每帧 boxing / LINQ / 闭包分配
- Guardrail: 稳态广播每帧 0 B GC alloc（Profiler 验证）

---

## Acceptance Criteria

*From GDD `design/gdd/audio-beat-clock.md` Core Rule 11 + 性能预算，scoped to this story:*

- [ ] 订阅/退订 API 对称明确（每个事件可 `+=`/`-=`）；文档注明订阅者须在 `OnDisable`/`OnDestroy` 退订
- [ ] 订阅者销毁后不再收到回调（退订生效，无悬挂委托）
- [ ] 未退订即销毁的订阅者不使 BeatClock 广播崩溃（防御式广播或明确契约——按 GDD 定义）
- [ ] 稳态广播（每拍/半拍/前瞻）每帧 0 B 堆分配（`readonly struct` + 直接 `Invoke`）
- [ ] 场景切换（`DontDestroyOnLoad` 存活）后旧场景订阅者已退订，不跨场景悬挂

---

## Implementation Notes

*Derived from ADR-D8 Implementation Guidelines:*

- 事件声明为 `event Action<TStruct>`，`TStruct` 为 `readonly struct`——`Invoke` 不 box、不分配。禁 `GetInvocationList()` 遍历（返回数组，分配）。
- 退订合约写入 `IBeatClock` 事件文档：订阅者在 `OnDisable`/`OnDestroy` 中 `-=`。BeatClock 不主动追踪订阅者生命周期（避免反向强引用），靠合约。
- 零 GC 验证：`Assets/Tests/PlayMode` 用 `Unity.Profiling.Recorder` 或 `Assert.That(GC.GetTotalAllocatedBytes...)` 测稳态广播帧分配为 0。
- 广播路径禁闭包/LINQ；补拍循环用索引 for，不用迭代器。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 004/005: 事件广播本体（本故事约束其内存卫生与退订）
- 各消费系统（C1/C4/FE3）自身的退订实现——本故事定义合约，消费方各自遵守

---

## QA Test Cases

*嵌自 GDD Acceptance Criteria（Given/When/Then），Integration + PlayMode 规格。*

- **AC-F2-19c（零 GC 稳态广播）** `[PlayMode]`
  - Given: BeatClock Playing，N 个订阅者挂接
  - When: 用 Profiler Recorder 采样若干秒稳态广播
  - Then: 每帧广播路径 GC alloc == 0 B
  - Edge cases: 补多拍帧仍 0 B（索引循环非迭代器）

- **AC（退订生效 + 悬挂防护）** `[PlayMode]`
  - Given: 订阅者订阅 `OnBeatExact` 后 `Destroy` 前退订
  - When: 继续广播
  - Then: 已退订订阅者不再收回调；若模拟未退订即销毁，广播不抛 `MissingReferenceException`（按 GDD 契约）
  - Edge cases: 场景切换后旧订阅者已退订，`DontDestroyOnLoad` BeatClock 无跨场景悬挂委托

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- `Assets/Tests/PlayMode/F2/EventHygieneTests.cs` — 零 GC + 退订 + 悬挂防护，必须通过（BLOCKING）

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 004（Exact 广播）、Story 005（Lookahead 广播）
- Unlocks: 所有下游订阅者（C1/C4/FE3/FE4）的安全订阅模式
