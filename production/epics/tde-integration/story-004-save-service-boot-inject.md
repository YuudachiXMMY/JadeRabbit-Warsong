# Story 004: F5 ISaveService 门禁 + LatencyOffsetMs boot-inject

> **Epic**: F6 TopDownEngine & Animancer 集成层
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: S
> **Manifest Version**: 2026-08-12
> **Last Updated**: [set by /dev-story when implementation begins]

## Context

**GDD**: `design/gdd/tde-integration.md`
**Requirement**: `TR-tde-015`（F5 存档就绪门禁 + 延迟校准注入）
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-D10: Bootstrap 顺序（primary）；ADR-D1: TDE 集成
**ADR Decision Summary**: boot 序列须等待 F5 `ISaveService` 初始化完成（D-4），随后从 `PlayerInfoSO` 读取 `LatencyOffsetMs` 并在 `BeatClock.Play()` 前一次性注入（D-5）；注入为两阶段写入者，boot 期写、运行期由设置面板改写。

**Engine**: Unity 6.3 LTS (6000.3.19f1) | **Risk**: MEDIUM
**Engine Notes**: 标准 C# 接口 + EasySave3 加载完成回调；无后-cutoff Unity API。ES3 加载完成语义对照 `plugins/` 存档文档（若存在），勿臆测 ES3 成员名。

**Control Manifest Rules (Foundation)**:
- Required: `LatencyOffsetMs` 为两阶段写入者——boot 一次性注入 + 运行期设置面板改写（Manifest F5 boot-inject 规则）
- Required: F5 `ISaveService` 初始化完成为 `Play()` 前置（ADR-D10 D-4）
- Forbidden: 禁止直接序列化 MonoBehaviour 引用（EasySave3 约束，仅纯数据）

---

## Acceptance Criteria

*From GDD `design/gdd/tde-integration.md`, scoped to this story:*

- [ ] AC-F6-23：boot 序列等待 `ISaveService` 就绪后，从 `PlayerInfoSO` 读取 `LatencyOffsetMs` 于 `BeatClock.Play()` 前一次性注入；未就绪时 `Play()` 被阻断
- [ ] AC-F6-27：boot 门禁顺序可断言——`ISaveService` 就绪求值序号先于 FMOD 门禁求值序号；`LatencyOffsetMs` 注入调用序号先于 `BeatClock.Play()` 调用序号；`EnableGameplayInput()` 调用序号晚于 `Play()` 调用序号
- [ ] AC-F6-28：`LatencyOffsetMs` boot 期注入累计恰好 1 次；`ISaveService` 就绪但 `PlayerInfoSO` 为 null 时，`BeatClock.LatencyOffsetMs` 采用默认值 0 且记录恰好 1 条 warning，`BeatClock.Play()` 仍正常放行

---

## Implementation Notes

*Derived from ADR-D10 D-4/D-5 + Control Manifest F5 boot-inject:*

- boot 门禁顺序（合并 Story 003 FMOD 门禁后）：`OnSceneLoaded(Single) → ISaveService 就绪 → FMOD 就绪 → 读取 LatencyOffsetMs 注入 BeatClock → Play() → EnableGameplayInput()`。
- `ISaveService` 就绪经 F5 提供的初始化完成信号/回调等待（跨 epic 接口，F5 提供）。
- `LatencyOffsetMs` 从 `PlayerInfoSO` 读取，注入 BeatClock 的延迟校准字段（一次性，boot 期）；运行期设置面板改写走同一字段的第二阶段写入路径（本 story 只实现 boot 注入）。
- 未就绪 → 阻断 `Play()`，记录 fatal（与 FMOD 门禁一致的中止语义）。

---

## Out of Scope

- 运行期设置面板改写 `LatencyOffsetMs`（第二阶段写入，属设置 UI epic）
- Story 003：FMOD 就绪门禁（并列前置，本 story 只加 F5 门禁 + 注入）
- F5 `ISaveService` / `PlayerInfoSO` 自身实现（属 F5 epic）

---

## QA Test Cases

*Embedded from GDD AC-F6-23（lean 模式）。*

**AC-1（AC-F6-23，Integration — PlayMode）**
  - Given：注入 `ISaveService` mock（`LatencyOffsetMs = 42`）与就绪信号可控
  - When：`ISaveService` 未就绪时触发 boot
  - Then：`BeatClock.Play()` == 0；就绪后 `Play()` == 1，且 BeatClock 延迟校准字段 == 42（注入恰 1 次）
  - Edge cases：`LatencyOffsetMs == 0`（默认，无校准）正常注入不报错；ISaveService 就绪但 PlayerInfoSO 为空 → 采用默认 0 且警告日志

**AC-2（AC-F6-27，Integration — PlayMode）**
  - Given：`MockSaveService` + mock `IFMODRuntimeChecker`，调用序号 Spy 挂 5 处（ISaveService 就绪/FMOD 就绪/LatencyOffsetMs 注入/`Play()`/`EnableGameplayInput()`）
  - When：触发完整 boot 序列（`OnSceneLoaded(Single)`）
  - Then：ISaveService 就绪序号 < FMOD 就绪序号；注入序号 < `Play()` 序号；`Play()` 序号 < `EnableGameplayInput()` 序号
  - Edge cases：任一环节延迟就绪时序号顺序不变

**AC-3（AC-F6-28，Integration — PlayMode）**
  - Given：`MockSaveService`（场景 A：`IsInitialized=true,LatencyOffsetMs=42.5f`；场景 B：`IsInitialized=true,PlayerInfoSO=null`）
  - When：触发 boot 序列
  - Then：A 注入累计==1；B `LatencyOffsetMs==0f`、Warning 恰 1 条、`Play()` 次数==1
  - Edge cases：B 与 AC-F6-23 子用例 4（`IsInitialized==false` fail-fast）走不同分支

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- `Assets/Tests/PlayMode/F6/SaveServiceBootInject_test.cs`（AC-23，注入 ISaveService mock）— 必须存在且通过
- `Assets/Tests/PlayMode/F6/SaveServiceBootInject_test.cs`（新增覆盖 AC-F6-27 调用序号断言 + AC-F6-28 一次性注入/PlayerInfoSO 缺失降级）— 必须存在且通过

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 002（boot 骨架）；跨 epic 需 F5 `ISaveService` / `PlayerInfoSO` 接口就绪
- Unlocks: None
