# ADR-D14: BPM 变更瞬态取消窗保护（accept→boundary 区间对齐）

## Status
Proposed

## Date
2026-08-12

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (6000.3.19f1) |
| **Domain** | Audio / Core（节拍事件订阅时序；不触及 Unity Audio API 表面）|
| **Knowledge Risk** | LOW — 决策为 CombatCore/BeatClock 自有抽象内的 C# `event` 订阅点重定向，未使用任何 post-cutoff Unity API |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`、`docs/engine-reference/unity/modules/audio.md`（确认 Unity AudioMixer/DSP 层与本决策无接口耦合，BeatClock `OnMeterChangeAccepted`/`OnMeterChanged` 为项目自定义 C# 事件，非 UnityEngine.Audio 事件）|
| **Post-Cutoff APIs Used** | None |
| **Verification Required** | EditMode 单测：注入 mock BeatClock，先后广播 `OnMeterChangeAccepted`（accept 时刻）与 `OnMeterChanged`（boundary 时刻），断言 C2 取消窗有效毫秒宽度在 **accept 时刻即** 取 `max(before, after)`，并持续至 `boundary + adaptationWindowBeats 拍` 后恢复 `cancelWindowMs(bpm_after)`。附加断言：两事件时间戳同源于 `AudioSettings.dspTime` 域；`[TearDown]` 显式取消订阅避免用例间残留 |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-D3（节奏量化核心，`OnMeterChangeAccepted`/`OnMeterChanged` 双事件语义来源）；ADR-D8（`IBeatClock` 消费者只读面已声明 `OnMeterChangeAccepted`/`OnMeterChanged` 两事件）— 二者均 Accepted |
| **Enables** | 消解 warning-ledger SG-5；C2 `combo-system.md` F7 局部修订（ADR Accepted 后落地）|
| **Blocks** | None（C2 实装尚未开工；本 ADR 是实装前的合约澄清，不阻塞其他 ADR）|
| **Ordering Note** | 与 ADR-D3 的 BD-2 三系统缓解合约同族。C1（`rhythm-core.md` C1-F5b）与 C4（`combat-director.md` SlotCapacity）侧**不变更**；本 ADR 仅调整 C2 侧触发点。三系统仍共享 `adaptation_window_beats`（registry 锁定 = 2）|

## Context

### Problem Statement
BD-2「BPM 跃升三系统难度尖峰」的 C2 承接侧存在一个瞬态暴露窗（warning-ledger **SG-5**）：

- 取消窗归一化区间 `[cancelWeight, 1.0]` 的**绝对毫秒宽度** `cancelWindowMs(bpm)` 随 BPM 反比收窄（动画经 Animancer 变速，`combo-system.md` F7 公式）。
- C2 的 Animancer `AnimancerState.Speed` 在 **`OnMeterChangeAccepted`（accept 时刻，F2 一接受 BPM 变更即广播）** 立即按新 `currentBpm` 重算（`combo-system.md` EC9 / AC-C2-33）——取消窗的绝对毫秒宽度**在 accept 时刻即收窄**。
- 但 F7 的 `max(cancelWindowMs(bpm_before), cancelWindowMs(bpm_after))` 宽容保护，当前绑定 **`OnMeterChanged`（boundary 时刻，BPM 实际生效边界）** 才触发。
- 与此同时，C1 判定网格**必须**留在 `OnMeterChanged` 才重锚（`rhythm-core.md` C1-F5b 明确论证：提前重锚会让边界前输入按未来网格判定），因此 accept→boundary 区间内 C1 仍以旧 BPM 网格宽松判定。

净效应：在 `accept → boundary` 区间（非零 latency 下最多 ±200ms），玩家的取消窗已随 Animancer 变速收窄，但 `max()` 宽容尚未生效——**C1 判 Perfect，C2 取消窗却已骤缩到接不上连招**。这是「节奏即战斗」支柱下最刺痛的瞬态：判定说成功、连招却断。

### Constraints
- **C1 侧不可动**：`rhythm-core.md` C1-F5b/:412 已确立 C1 判定网格重锚绑 `OnMeterChanged`（提前重锚会污染边界前判定）。本 ADR 不得要求 C1 改订阅点。
- **不新增 Tuning Knob**：BD-2 的 `adaptationWindowBeats` 已由 C1/C2/C4 三 owner 联合锁定 = 2（2026-08-11，registry `adaptation_window_beats`，单一 owner=`RhythmConfigSO.AdaptationWindowBeats`）。修复须复用此旋钮，不得引入平行常量。
- **边界合约**：C2（CombatCore 程序集）只订阅 BeatClock 的 C# 域事件，不调用其生命周期方法，不 `using` TopDownEngine（`technical-preferences.md` 边界合约）。
- **零 GC**：`OnMeterChangeAccepted` 载荷须为 `readonly struct`，回调内不分配。

### Requirements
- accept→boundary 区间内取消窗有效毫秒宽度不得骤缩到 `cancelWindowMs(bpm_after)`。
- 保护窗尾端与 C1 适应窗口对齐（同在 `boundary + adaptationWindowBeats 拍` 恢复标准宽度），保持三系统「同时回归标准」的语义一致性。
- BPM 无变化（拍号-only 变更 `bpmChanged==false`）时不启用保护窗（与 C1/`rhythm-core.md` 对称）。

## Decision

**将 C2 取消窗 BD-2 保护（F7 的 `max(cancelWindowMs(bpm_before), cancelWindowMs(bpm_after))`）的触发点，从 `OnMeterChanged`（boundary）前移到 `OnMeterChangeAccepted`（accept）。** 即「保护提前」策略（warning-ledger SG-5 方向 B）。

保护窗时域重定义为：

```
保护窗 = [ t_accept , t_boundary + adaptationWindowBeats 拍 ]
```

- **头端**（`t_accept`）：与 Animancer 变速（取消窗绝对毫秒收窄的诱因）同一时刻起效——闭合原暴露窗 `[accept, boundary]`。
- **尾端**（`t_boundary + adaptationWindowBeats 拍`）：与 C1 适应窗口尾端（`rhythm-core.md`「生效边界拍起 2 拍」）**对齐**，三系统同时恢复标准宽度。

保护窗内：`cancelWindowMs_effective = max(cancelWindowMs(bpm_before), cancelWindowMs(bpm_after))`（取较宽者，不骤缩）。
保护窗外：`cancelWindowMs_effective = cancelWindowMs(bpm_after)`（新 BPM 标准宽度）。
`bpmChanged==false`（拍号-only）不启用保护窗。

> **为何头端可早于 C1 而尾端与 C1 对齐（非对称是刻意的）**：C2 的应激源（Animancer Speed）在 accept 即出现，比 C1 的应激源（网格重锚，在 boundary）更早，故 C2 保护须更早介入；两者在 boundary 后同步收敛以维持「适应窗口」语义统一。C2 保护窗因此比 C1 略长（多覆盖 accept→boundary 头段），这正是修复 SG-5 所必需。

### Architecture Diagram

```
时间轴 →   t_accept                         t_boundary            t_boundary + 2拍
             │                                  │                       │
F2 事件:     OnMeterChangeAccepted              OnMeterChanged          │
             │  (currentBpm := bpm_after)       │ (判定网格生效边界)     │
             │                                  │                       │
C2 Animancer ├── Speed 立即按 bpm_after 重算 ───┼───────────────────────┤  (取消窗绝对 ms 已收窄)
             │                                  │                       │
C1 判定网格: [───── 旧 BPM 网格(宽松) ─────────]├── 新网格 + C1 适应窗 ──┤
             │                                  │                       │
── 修复前 ──                                    ├──── C2 max() 宽容 ─────┤   ✗ 暴露窗 = [accept, boundary]
── 修复后 ── ├──────────── C2 max() 宽容保护(本 ADR) ────────────────────┤   ✓ 头端提前到 accept，尾端对齐 C1
```

### Key Interfaces
- C2 订阅 `IBeatClock.OnMeterChangeAccepted`（`event Action<MeterChangeEventData>`，ADR-D8 已声明的消费者只读面）以触发保护窗**起点**并快照 `bpm_before`。
- C2 继续订阅 `IBeatClock.OnMeterChanged` 仅用于计算保护窗**尾端 DSP 时刻**（`t_boundary + adaptationWindowBeats × fullBeatMs`），不再用它作为保护窗起点。
- 无新增公共 API、无新增事件、无新增 SO 字段——纯订阅点重定向 + 保护窗端点公式调整。

## Alternatives Considered

### Alternative 1: 维持现状（保护绑 boundary），文档标注为「可接受瞬态」
- **Description**: 不改代码，将 accept→boundary 的取消窗骤缩记为已知瞬态限制。
- **Pros**: 零改动；C1/C2 触发点统一在 boundary，表面一致。
- **Cons**: 直接违背「节奏即战斗」支柱——最多 ±200ms 内「判 Perfect 却接不上连招」，是玩家最易察觉、最挫败的手感断裂点；BD-2 的 C2 承接名存实亡。
- **Rejection Reason**: 该瞬态恰好落在支柱最敏感处，不可接受。

### Alternative 2: 让 C1 判定网格也提前到 accept 重锚（对齐到 accept 而非 boundary）
- **Description**: C1、C2 双双绑 `OnMeterChangeAccepted`，消除跨系统时序差。
- **Pros**: 三系统触发点完全统一在 accept。
- **Cons**: 直接违反 `rhythm-core.md` C1-F5b/:412 的既定合约——accept→boundary 区间内到达的输入本应按**当前仍在播放的旧 BPM 网格**判定，提前重锚会让这些输入错按未来网格量化，制造新的判定错位（比 SG-5 更广的回归）。
- **Rejection Reason**: 以更严重的判定污染换取表面对称，得不偿失；C1 重锚绑 boundary 是硬约束。

### Alternative 3（选定）: 保护提前——C2 保护窗头端移到 accept、尾端对齐 C1
- **Description**: 见 Decision。仅 C2 保护窗起点前移，C1/C4 不动。
- **Pros**: 精确闭合暴露窗；复用既有 `max()` 机制与 `adaptationWindowBeats` 旋钮；C1 硬约束不受影响；尾端与 C1 对齐保持语义统一。
- **Cons**: C2 保护窗与 C1 适应窗口在头端不对称（C2 更早），需在 GDD/ADR 中显式说明以免误读为 bug。
- **Rejection Reason**: 无——此为采纳项。非对称已在 Decision 中论证为刻意且必要。

## Consequences

### Positive
- 消解 SG-5：accept→boundary 区间取消窗不再骤缩，「Perfect 却接不上连招」瞬态消失。
- 零新增旋钮/常量/事件：仅订阅点重定向，维护面最小。
- 三系统 BD-2 缓解在尾端仍同步收敛，语义一致。

### Negative
- C2 保护窗头端早于 C1 适应窗口头端（accept vs boundary），引入一处**刻意的**非对称，须在 `combo-system.md` F7 与 `rhythm-core.md` BD-2 互引处显式标注，避免后续复审误判为不一致。

### Risks
- **风险**：`OnMeterChangeAccepted` 与 `OnMeterChanged` 载荷须都能提供 `bpm_before`/`bpm_after` 与 boundary DSP 时刻，供 C2 计算保护窗尾端。**缓解**：ADR-D8 消费者面已声明两事件；实装前用 EditMode 单测断言载荷字段齐备（见 Verification Required）。
- **风险**：若某帧 `OnMeterChangeAccepted` 广播后、`OnMeterChanged` 尚未到达时发生第二次 BPM 变更（连续跃迁），保护窗端点计算需处理叠加。**缓解**：实装时以「最近一次 accept 的 `bpm_before` 快照 + 最新 boundary」为准，单测覆盖连续变更用例（列入 C2 实装 DoD，非本 ADR 阻塞项）。

**引擎专家校验补充（unity-specialist，2026-08-12；无 BLOCKING，以下为 minor notes）**：C# `event` 订阅点重定向经确认为 Unity 6.3 + Animancer 惯用且优选做法（优于 `UnityEvent`/SO Event Channel，契合零 GC 与边界合约）；`Post-Cutoff APIs = None` 属实。追加以下实装期陷阱：

- **DSP 时钟域同源**：`OnMeterChangeAccepted`/`OnMeterChanged` 载荷的时间戳字段**必须同为 `AudioSettings.dspTime` 域**（禁止混入 `Time.time` 采样，否则 `t_accept → t_boundary + 2拍` 窗口引入帧率相关系统误差，恰好复现本 ADR 欲消解的问题类别，且违反 `technical-preferences.md`「禁止用 Update() 轮询做节拍判定」）。EditMode 单测须断言两事件时间戳同源。
- **Animancer 变速与已播放归一化时间的交互**：`AnimancerState.Speed` 仅影响**后续**帧的时间推进，不回溯重缩放已播放的 `NormalizedTime`。若 accept 发生于攻击动画播放中段，`cancelWindowMs` 的收窄基准应**查询 Animancer 运行时状态**（`AnimancerState.Time`/`NormalizedTime` × `Speed` 结合剩余归一化进度），而非从动画总时长静态套公式——否则「accept 时刻立即收窄」的假设在动画中段不完全成立。列入 C2 实装 DoD。
- **多订阅者回调顺序无依赖（显式声明）**：C1/C2/C4 对 `OnMeterChangeAccepted`/`OnMeterChanged` 的订阅回调**彼此无读写依赖**，C# 多播委托的调用顺序（取决于各系统 `OnEnable()` 顺序，Unity 默认不保证）**不影响正确性**——三者各自独立计算，本 ADR 不引入跨订阅者共享状态。
- **EditMode 测试订阅隔离**：mock BeatClock 注入的测试用例须在 `[TearDown]` 显式 `-=` 取消订阅，避免连续 BPM 变更用例间的事件残留污染（Unity Test Runner 同域连跑不保证每例 domain reload）。

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| `combo-system.md` | **规则 12**（Detailed Rules，BD-2 取消窗适应窗口主契约，当前明写「C2 收到边界生效事件 `OnMeterChanged` 后进入适应窗口……区别于接受时的 `OnMeterChangeAccepted`——后者仅用于规则 6 Animancer 预变速」）；F7（Formulas，`max(before, after)` 公式）；EC9（accept 时刻 Animancer 变速）；AC-C2-40/41/42 | 将 BD-2 保护触发点从 `OnMeterChanged` 前移到 `OnMeterChangeAccepted`，保护窗 = [accept, boundary+2拍]。ADR Accepted 后落 GDD：**规则 12** 触发事件与「区别于 Accepted」措辞改写（Accepted 现同时驱动 Animancer 变速 **与** BD-2 保护起点）、F7 触发点描述、AC-C2-40 触发事件、新增一条 accept→boundary 覆盖断言 |
| `rhythm-core.md` | C1-F5b BD-2 适应窗口（绑 `OnMeterChanged`，:412 重锚合约）；「BD-2 三系统互引」 | 确认 C1 侧**不变**；ADR Accepted 后在 C1 BD-2 互引注记补一句「C2 保护窗头端前移至 accept、尾端仍与本适应窗口对齐（见 ADR-D14）」，以固化刻意的非对称 |
| `combat-director.md` | C4 SlotCapacity 每秒令牌速率归一化（BD-2） | 不受影响；C4 侧无改动，仅 BD-2 互引保持一致 |

## Performance Implications
- **CPU**: 可忽略。多一个 `OnMeterChangeAccepted` 订阅回调（每次 BPM 变更触发 1 次，非热路径）；取消窗宽度查询仍为 O(1)。
- **Memory**: 零新增分配（事件载荷为 `readonly struct`；保护窗端点为既有字段复用）。
- **Load Time**: 无影响。
- **Network**: N/A（单机）。

## Migration Plan
C2（`combo-system.md` 对应实装）尚未开工，无存量代码迁移。落地顺序：
1. 本 ADR Accepted。
2. `combo-system.md` 局部修订：**规则 12** 触发事件 `OnMeterChanged → OnMeterChangeAccepted` 并改写「区别于 Accepted」措辞（Accepted 现同时驱动规则 6 变速 **与** BD-2 保护起点，尾端仍锚 `OnMeterChanged` 的 `predictedEffectiveDspTimeRaw` + 2拍）、F7 触发点与保护窗端点公式、AC-C2-40 触发事件更新、新增 accept→boundary 覆盖 AC；EC9 补一句「取消窗绝对毫秒宽度在 accept 收窄，故 BD-2 保护同步在 accept 起效」。
3. `rhythm-core.md` BD-2 互引注记补充非对称说明（一句）。
4. C2 实装时按 F7 修订版编码，EditMode 单测覆盖 Verification Required + 连续 BPM 变更用例。

## Validation Criteria
- EditMode 单测：`OnMeterChangeAccepted`（120→180）广播后**立即**断言取消窗有效毫秒宽度 == `max(cancelWindowMs(120), cancelWindowMs(180))`（取较宽者），且此值维持至 `boundary + 2拍` 后恢复 `cancelWindowMs(180)`。
- 回归断言：`bpmChanged==false` 时保护窗不启用。
- 复审断言：`combo-system.md` F7 与 `rhythm-core.md` BD-2 互引对「C2 头端=accept、尾端对齐 C1」表述一致，无悬空。

## Related Decisions
- ADR-D3（节奏量化核心，`OnMeterChangeAccepted`/`OnMeterChanged` 双事件语义）
- ADR-D8（DI 测试缝，`IBeatClock` 消费者只读面事件声明）
- `design/gdd/combo-system.md` F7 / EC9 / AC-C2-40~42（C2 承接侧）
- `design/gdd/rhythm-core.md` C1-F5b / BD-2 三系统互引（C1 承接侧，本 ADR 不改）
- `design/gdd/combat-director.md` SlotCapacity BD-2（C4 承接侧，本 ADR 不改）
- `production/warning-ledger.md` SG-5（本 ADR 承接项）
