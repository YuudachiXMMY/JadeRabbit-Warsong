# Story 001: BeatClock 单例骨架 + DI 测试缝隙

> **Epic**: F2 音频/拍点时钟系统
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: M
> **Manifest Version**: 2026-08-12
> **Last Updated**: [set by /dev-story when implementation begins]

## Context

**GDD**: `design/gdd/audio-beat-clock.md`
**Requirement**: `TR-beatclock-001`, `TR-beatclock-011`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-D8: DI / 测试缝隙（primary）；secondary: ADR-D6（`IDSPClock`/`IAudioPlayer` 生产实现归属）、ADR-D1（`IBeatClock` 只读消费面 vs `IBeatClockLifecycle` 控制面拆分）
**ADR Decision Summary**: `IBeatClock` 声明为只读消费接口（8 事件 + `GetSnapshot()` + `LatencyOffsetMs`/`DSPClock`/`GetBeatIntervalSeconds()`/`IsStartConfirmed`/`GetConfirmedDSPStart()`），生命周期方法归 `IBeatClockLifecycle`（F6 专属）；时钟源经 `IDSPClock`、音频播放器经 `IAudioPlayer` 注入，支持纯 C# NUnit 确定性测试。

**Engine**: Unity 6.3 LTS (6000.3.19f1) | **Risk**: LOW
**Engine Notes**: 纯语言层契约，引擎升级不使本 ADR 失效，除非 `[SerializeField]` 字段注入语义变更。BeatClock 为 `MonoBehaviour`，测试须 `go.AddComponent<BeatClock>()` 实例化（禁 `new BeatClock()`），`[TearDown]` 中 `Object.DestroyImmediate(go)` 防 `DontDestroyOnLoad` 单例跨测试泄漏。

**Control Manifest Rules (this layer)**:
- Required: DI/test seam 模式 = `[SerializeField] MonoBehaviour _ref` + property wrapper + `SetXForTesting(IX)`，包裹于 `#if UNITY_EDITOR || DEVELOPMENT_BUILD`；`??=` 缓存确保注入不被 `Awake()` 覆盖（ADR-D8）
- Required: Runtime 程序集声明 `[assembly: InternalsVisibleTo("EditModeTests")]` —— **实际跑绿的测试 asmdef 名为 `EditModeTests`**（ADR-D8 示例块内出现的 `CombatCore.Tests.EditMode` 仅为泛示例，已 defer 至 `/test-setup`；勿照抄）。写入前须核对当前 EditMode asmdef 名
- Required: `Mock*` 实现置于 `Assets/Tests/EditMode/CombatCore/Mocks/`，仅测试 asmdef 引用，绝不进 Runtime 包
- Forbidden: `beatclock_lifecycle_bypass` —— 消费系统（C1/C4/AI）禁止直接调 BeatClock 生命周期方法（本故事只暴露只读 `IBeatClock`，生命周期方法归 `IBeatClockLifecycle`）
- Guardrail: 事件 payload 全为 `readonly struct`（零堆分配）；`OnBeatExact?.Invoke(data)` 而非 `GetInvocationList()`

---

## Acceptance Criteria

*From GDD `design/gdd/audio-beat-clock.md` Core Rule 1 / Rule 12 + 测试基础设施规范，scoped to this story:*

- [ ] BeatClock 为 `MonoBehaviour` 单例，挂载于 `DontDestroyOnLoad` 的专属 GameObject（命名 `[BeatClock]`），全局唯一实例，`BeatClock.Instance` 静态属性访问，禁止 Inspector 手动配置
- [ ] BeatClock 通过 `IDSPClock` 接口注入时钟源；EditMode 测试可注入 `MockDSPClock`
- [ ] BeatClock 通过 `IAudioPlayer` 接口注入音频播放器（与 `IDSPClock` 对称）；测试可注入 `MockAudioPlayer`；`IAudioPlayer` 最低暴露 `PlayScheduled(double dspTimeRaw)`、`Stop()`（映射 `FMOD_STUDIO_STOP_IMMEDIATE` 立即停止）、`IsPlaying`、`Pause()`、`Resume()` 五个成员
- [ ] `IBeatClock` 只读消费面声明完成（8 事件 + `GetSnapshot()` + `LatencyOffsetMs`/`DSPClock`/`GetBeatIntervalSeconds()`/`IsStartConfirmed`/`GetConfirmedDSPStart()`），**不含**任何生命周期方法
- [ ] `SetBeatClockForTesting` / `SetDSPClockForTesting` / `SetAudioPlayerForTesting` 等注入 setter 包裹于 `#if UNITY_EDITOR || DEVELOPMENT_BUILD`
- [ ] Runtime 程序集含 `[assembly: InternalsVisibleTo("EditModeTests")]`（核对实际 asmdef 名）

---

## Implementation Notes

*Derived from ADR-D8 Implementation Guidelines:*

- BeatClock 为 MonoBehaviour，不能构造注入 → 用 `[SerializeField] MonoBehaviour _dspClockRef` + property wrapper `private IDSPClock _dspClock; void Awake() { _dspClock ??= _dspClockRef as IDSPClock; }`；`IAudioPlayer` 同构。`??=` 缓存保证 `AddComponent` 触发的 `Awake` 不覆盖已注入的测试 mock。
- `IDSPClock` 唯一方法钦定为 `double GetDspTime()`（ADR-D8 §5，取代 `GetCurrentTime()`/`GetCurrentDSPTime()`）。生产实现 `FMODDSPClockImpl` 归 ADR-D6，本故事不实现生产类，仅声明接口 + Mock。
- `IBeatClock` 消费面与 `IBeatClockLifecycle`（`Play`/`Stop`/`Pause`/`Resume`/`ConfirmDSPStart`，F6 专属，ADR-D1）严格拆分：消费者（C1/C4/HUD）只见 `IBeatClock`，编译期无法触及生命周期方法。
- `MockDSPClock` / `MockAudioPlayer` 为纯 C# 类（非 MonoBehaviour），置于 `Assets/Tests/EditMode/CombatCore/Mocks/`。`MockDSPClock` 支持注入可控 `dsp_now`（供确定性推进）；`MockAudioPlayer` 记录 `PlayScheduled`/`Stop`/`Pause`/`Resume` 调用序列并可模拟 `PlayScheduled()` 失败。
- 接口声明置于 `Assets/Scripts/Core/Interfaces/`（ADR-D8 新增目录）。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: 间隔/时间戳公式实现
- Story 003: `Play()`/`Stop()`/`ConfirmDSPStart()` 生命周期方法体（本故事仅声明 `IBeatClockLifecycle` 接口签名）
- Story 004: 调度器与事件广播逻辑（本故事仅声明事件签名）
- 生产 `FMODDSPClockImpl` / `FMODAudioPlayerImpl`（ADR-D6 归属，非本 epic 手写目标）

---

## QA Test Cases

*嵌自 GDD 测试基础设施规范（AC 章节前言）+ Core Rule 1/12。Logic/Integration 自动化规格。*

- **AC-1（单例 + DontDestroyOnLoad）**
  - Given: 场景中无 BeatClock 实例
  - When: 通过 bootstrap 创建 BeatClock（`AddComponent<BeatClock>()`）
  - Then: `BeatClock.Instance != null`；GameObject 名为 `[BeatClock]` 且处于 `DontDestroyOnLoad` 场景；再次创建不产生第二实例
  - Edge cases: 重复 `AddComponent` 时第二实例应自毁或断言；`[TearDown]` 中 `DestroyImmediate` 后 `Instance` 归 null

- **AC-2（IDSPClock 注入）**
  - Given: `MockDSPClock` 实例
  - When: `beatClock.SetDSPClockForTesting(mockDspClock)` 后查询 `beatClock.DSPClock.GetDspTime()`
  - Then: 返回 mock 注入值，不触及真实 FMOD
  - Edge cases: 未注入时生产路径应回退真实 `IDSPClock`（`Awake` 中 `??=`）

- **AC-3（IAudioPlayer 五成员契约）**
  - Given: `MockAudioPlayer` 注入
  - When: 反射/编译检查 `IAudioPlayer` 成员
  - Then: 暴露且仅暴露 `PlayScheduled(double)`、`Stop()`、`IsPlaying`、`Pause()`、`Resume()`；`Stop()` 语义为立即停止（渐出属 F6，不在契约内）

- **AC-4（IBeatClock 无生命周期方法）**
  - Given: `IBeatClock` 接口定义
  - When: 编译期检查
  - Then: `IBeatClock` 不含 `Play`/`Stop`/`Pause`/`Resume`/`ConfirmDSPStart`；这些方法仅存在于 `IBeatClockLifecycle`
  - Pass condition: 消费者代码引用 `IBeatClock` 时无法调用生命周期方法（编译失败即证隔离成立）

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- `Assets/Tests/EditMode/F2/BeatClockSeamTests.cs` — 单例/注入/接口隔离，必须通过
- 若需真实 FMOD 注入烟测 → `production/qa/evidence/` 补充（非阻塞）

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: None（epic 第一个故事）
- Unlocks: Story 002（公式）、Story 003（生命周期）、Story 004（调度器）—— 全部依赖本故事的 DI 缝隙与单例骨架
