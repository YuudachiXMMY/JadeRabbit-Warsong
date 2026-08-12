# Story 011: 延迟偏移两阶段写者（纯持有者）

> **Epic**: F2 音频/拍点时钟系统
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: [fill before sprint planning]
> **Manifest Version**: 2026-08-12
> **Last Updated**: [set by /dev-story when implementation begins]

## Context

**GDD**: `design/gdd/audio-beat-clock.md`
**Requirement**: `TR-beatclock-007`（`LatencyOffsetMs` 注入 · 部分）
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-D10: Bootstrap 顺序（primary）；secondary: ADR-D1（运行时写者 FE3 归属）
**ADR Decision Summary**: `LatencyOffsetMs` 采两阶段写者模型：① F6 bootstrap 期一次性注入初值（从 F5 存档读取，经 bootstrap driver 传入，**禁 BeatClock 自 load**）；② 运行时唯一写者 = FE3 延迟校准系统。BeatClock 是纯持有者/消费者，自身从不读存档、不自改偏移。

**Engine**: Unity 6.3 LTS (6000.3.19f1) | **Risk**: LOW
**Engine Notes**: 纯 C# 字段持有 + property，无引擎特性风险。关键是拓扑约束（谁能写）非算法。

**Control Manifest Rules (this layer)**:
- Required: `LatencyOffsetMs` 由外部两阶段写入（boot 注入 + FE3 运行时 set），BeatClock 仅持有并在公式中消费（F2-F5）
- Forbidden: `f2_self_load_latency_from_f5` —— BeatClock 直接读 F5 存档/自行 load 偏移（registry 注册禁令）
- Guardrail: 运行时改偏移即时影响后续 adjusted 计算，但不重锚 raw（raw 与硬件调度无关于偏移）

---

## Acceptance Criteria

*From GDD `design/gdd/audio-beat-clock.md` Core Rule 7 + Formula F2-F5，scoped to this story:*

- [ ] `LatencyOffsetMs` 暴露为 `IBeatClock` 只读属性 + 受控写入面（boot 注入方法 + FE3 运行时 setter）
- [ ] Boot 期注入：`InjectLatencyOffsetAtBoot(ms)` 由 bootstrap driver 调用一次，设初值
- [ ] 运行时写入：FE3 经受控 setter 更新偏移，后续 adjusted 时间戳（F2-F5）即时反映新值
- [ ] BeatClock 自身**不含**任何读取 F5 存档 / EasySave 的代码路径（`f2_self_load_latency_from_f5` 禁令）
- [ ] 偏移变更只影响 adjusted 坐标，不改 raw 坐标 / 不重锚调度（硬件调度用 raw）

---

## Implementation Notes

*Derived from ADR-D10 + ADR-D1 Implementation Guidelines:*

- 两阶段写者：boot 注入方法与 FE3 runtime setter 是仅有的两个写入点；二者可同签名或分开，但都不在 BeatClock 内部触发（BeatClock 被动接收）。
- **禁自 load**：BeatClock 不 `using` F5/EasySave，不引用 `PlayerInfoSO`。偏移值的持久化与读取归 F5 + bootstrap driver（ADR-D10 D-5：F2 无直接 F5 运行时依赖）。
- F2-F5 消费：`adjusted = raw + LatencyOffsetMs/1000.0`（Story 002 已实现公式，本故事保证 `LatencyOffsetMs` 来源正确、写者拓扑受控）。
- 运行时改偏移不重锚 raw：raw 拍点时刻由 `dsp_start` 决定，与玩家校准无关；只有 adjusted（判定/HUD）随偏移平移。`last_adjusted_beat_dsp_time` 在下次拍点自然重算即可。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: F2-F5 adjusted 公式本体（本故事保证输入来源）
- F5: 偏移的存档序列化（PlayerInfoSO）
- FE3: 延迟校准 UI 与测量算法（本故事只暴露 setter 供其调用）
- F6: bootstrap driver 的调用序列

---

## QA Test Cases

*嵌自 GDD Acceptance Criteria（Given/When/Then），Integration 自动化规格。*

- **AC（Boot 注入 + 运行时改写 + 禁自 load）** `[EditMode]`
  - Given: 新建 BeatClock，未注入偏移
  - When: `InjectLatencyOffsetAtBoot(80)`
  - Then: `LatencyOffsetMs == 80`；F2-F5 adjusted = raw + 0.080
  - When（运行时）: FE3 setter 改为 −40
  - Then: 后续 `GetSnapshot()` 的 adjusted 时间戳即时用 −0.040；raw 时间戳不变
  - Edge cases: BeatClock 源码静态检查/编译隔离——不引用 F5/EasySave 命名空间（`f2_self_load_latency_from_f5` 负向断言，可用程序集引用检查或反射验证无 ES3 依赖）

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- `Assets/Tests/EditMode/F2/LatencyOffsetWriterTests.cs` — 两阶段写入 + 禁自 load 隔离，必须通过（BLOCKING）

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 002（adjusted 公式消费偏移）
- Unlocks: FE3 延迟校准运行时写入、F6 bootstrap 注入序列
