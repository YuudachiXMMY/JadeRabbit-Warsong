# Story 003: FMOD 就绪门禁 + 异步模式验证

> **Epic**: F6 TopDownEngine & Animancer 集成层
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: 2026-08-12
> **Last Updated**: [set by /dev-story when implementation begins]

## Context

**GDD**: `design/gdd/tde-integration.md`
**Requirement**: `TR-input-015`（FMOD 异步初始化 boot 门禁 — 覆盖就绪门禁与异步模式验证）
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-D10: Bootstrap 顺序（primary）；ADR-D1: TDE 集成
**ADR Decision Summary**: `BeatClock.Play()` 前必须确认 FMOD 系统已就绪；若音频以异步模式加载，须经协程轮询验证 bank 加载完成后方可启动节拍时钟，避免节拍相位与音频起点错位。

**Engine**: Unity 6.3 LTS (6000.3.19f1) | **Risk**: HIGH
**Engine Notes**: FMOD 就绪 API（`Studio.System.getBank` / bank loading state）为第三方插件 API——须对照 `docs/engine-reference/unity/plugins/fmod.md`，严禁从训练数据推断成员名。

**Control Manifest Rules (Foundation)**:
- Required: `BeatClock.Play()` 前 FMOD 就绪为硬前置（ADR-D10 D-1）；异步加载须协程验证完成（ADR-D10 D-2）
- Forbidden: 禁止在 FMOD 未就绪时启动 BeatClock（相位漂移）
- Guardrail: 就绪轮询有超时上限，超时 → fatal 日志而非静默挂起

---

## Acceptance Criteria

*From GDD `design/gdd/tde-integration.md`, scoped to this story:*

- [ ] AC-F6-04：FMOD 未就绪时 `BeatClock.Play()` 被阻断（Play 调用 == 0），就绪后正常启动
- [ ] AC-F6-05：异步模式下经协程轮询 bank 加载状态，加载完成后才 `Play()`
- [ ] AC-F6-19：FMOD 就绪轮询超时（达上限仍未就绪）→ 记录 fatal 日志，不静默挂起
- [ ] AC-F6-21：同步模式下就绪检查一次通过即启动（无多余协程等待帧）

---

## Implementation Notes

*Derived from ADR-D10 D-1/D-2 + GDD EC (FMOD race):*

- boot 序列在 Story 002 的 `OnSceneLoaded(Single)` 内、`Play()` 前插入 FMOD 就绪门禁分支。
- 就绪判定经 `plugins/fmod.md` 记载的 bank 加载状态查询（先读文档确认成员名，勿臆测）。
- 异步：启动协程 `WaitForFMODReady()`，每帧轮询直至就绪或超时（超时上限为 Tuning Knob）；就绪后回调触发 `Play()`。
- 同步：单次检查通过立即 `Play()`，不进协程（避免多等一帧）。
- 超时：达上限记录 `[F6-FATAL] FMOD not ready within timeout — BeatClock start aborted`，不 `Play()`。

---

## Out of Scope

- Story 002：adapter 骨架与 boot 顺序（本 story 只补 FMOD 门禁分支）
- Story 004：F5 ISaveService 门禁 + LatencyOffsetMs 注入
- Story 006：音频运行期降级（SC-1）——本 story 只管启动期就绪

---

## QA Test Cases

*Embedded from GDD AC-F6-04/05/19/21（lean 模式）。*

**AC-1（AC-F6-04，Logic — PlayMode）**
  - Given：FMOD mock 报告未就绪
  - When：触发 boot 序列
  - Then：`BeatClock.Play()` == 0；FMOD 转就绪后 `Play()` == 1
  - Edge cases：就绪状态在轮询首帧即 true（不进协程等待）

**AC-2（AC-F6-05，Logic — PlayMode）**
  - Given：FMOD mock 异步模式，bank 加载需 N 帧
  - When：boot 触发就绪协程
  - Then：加载完成帧之前 `Play()` == 0；完成后同帧或下一帧 `Play()` == 1，参数与 SO 一致
  - Edge cases：加载完成前场景卸载 → 协程安全取消，无悬挂 `Play()`

**AC-3（AC-F6-19，Logic — PlayMode）**
  - Given：FMOD mock 永不就绪
  - When：boot 触发就绪协程，推进至超时上限
  - Then：记录 fatal 日志（`LogAssert.Expect`）；`Play()` == 0；协程终止（不无限轮询）
  - Edge cases：超时上限边界值恰好那一帧就绪 → 视为成功启动

**AC-4（AC-F6-21，Logic — PlayMode）**
  - Given：FMOD mock 同步模式且首检即就绪
  - When：boot 触发
  - Then：`Play()` 于当帧完成，未额外 yield（帧计数 == boot 帧）
  - Edge cases：无

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- `Assets/Tests/PlayMode/F6/FmodReadinessGate_test.cs`（AC-04/05/19/21，注入 FMOD 就绪 mock）— 必须存在且通过

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 002（boot 骨架提供插入点）
- Unlocks: None（与 Story 004 并列增强 boot 门禁）
