# Architecture Review Report — post-D8-Accept 复跑

> **Date**: 2026-08-11
> **Engine**: Unity 6.3 LTS (6000.3.19f1)
> **GDDs Reviewed**: 9 (game-concept, audio-beat-clock, input-event-system, so-data-layer, save-persistence, tde-integration, rhythm-core, combo-system, perk-system, combat-director)
> **ADRs Reviewed**: 9 (D1, D2, D3, D4, D5, D6, D7, D8, D10 — 全部 Accepted)
> **Mode**: `/architecture-review full`
> **Verdict**: **CONCERNS（健康 — 0 阻塞项）**

本轮为 ADR-D8 Accept、测试框架搭建、UX/a11y 文档补齐后的复跑，验证上一轮
（`architecture-review-2026-08-11.md`）三大遗留阻塞是否清除。

---

## Traceability Summary

| 指标 | 上轮 (v7) | 本轮 (v8) | Δ |
|------|-----------|-----------|---|
| Total requirements | 146 | 146 | — |
| ✅ Covered | 81 | **85** | **+4** |
| ⚠️ Partial | 15 | 15 | 0 |
| ❌ Gap | 50 | **46** | **−4** |
| Coverage | 55.5% | **58.2%** | +2.7pp |

**转正的 4 条**（上轮因 ADR-D8 Proposed 而挂起的 Core DI 缝隙 Gap）：

| 系统 | 缝隙 | 承接 |
|------|------|------|
| C1 rhythm | `IBeatClock` / `IInputEventBus` (TR-rhythm-015) | ADR-D8 §1/§2 |
| C2 combo | `IComboCounter` (AC-C2-22) | ADR-D8 §3 |
| C3 perk | `IRandomSource` / pause 源 (AC-C3-14) | ADR-D8 §4/§6 |
| C4 director | `IDSPClock` mock | ADR-D8 §5 |

完整矩阵见 `traceability-index.md` (Rev.9)。

### Coverage Gaps（剩余 46）

| 分类 | 数量 | 性质 |
|------|------|------|
| beatclock (13) + input (13) + save (11) | 37 | **GDD=规范的结构性预期缺口**——内部实现项，本就无需专属 ADR |
| concept | 5 | 真实缺口，待 FE3/C5 未来 GDD（TR-concept-011/014/002/003 等） |
| tde | 4 | 真实缺口，待 FE1/Post-MVP（TR-tde-012 等） |

Core 四系统（rhythm/combo/perk/director）Gap = **0**。仅 9 条为真实架构/未来 GDD 缺口，
均**非当前实现阻塞**。

---

## Cross-ADR Conflicts — **0 blocking**

### A1 — D1/D10 生命周期冲突（上轮 🔴 BLOCKING）→ ✅ 真实消解

经**单一权威裁定**收口，非"两个矛盾决策各自被 Accept"：
- **D1 确立为订阅唯一权威**（`adr-d1:90–98`）：`OnEnable()`/`OnDisable()` 配对是
  `sceneLoaded` + `MMEventListener<TopDownEngineEvent>` 订阅的唯一权威位置；`Start()` 只做
  只读 FMOD 就绪探测。行 97 明言"D1 为生命周期订阅位置的权威 ADR"。
- **D10 显式让步**（`adr-d10:106–108/139–143`）：订阅归 OnEnable/OnDisable（ADR-D1 权威），
  就绪顺序从"订阅门控"转为 **handler 早退 guard**：`if(!_fmodVerified || !_saveService.IsInitialized) return;`
  （D1:289–294 与 D10:160–164 两处代码逐字一致）。
- 两份 ADR 现给出相同的 `TDELifecycleAdapter` 代码，重复订阅风险消除。

### A2 — DI 接口所有权 → ✅ 已由 ADR-D8 归属

D8 §1–§5 声明五接口，与各生产方 ADR 逐一核对一致，无重复归属/命名分歧：
- `IBeatClock`（只读面，D6/F2）读/控制分面清晰，控制留在 `IBeatClockLifecycle`（D1/F6）
- `IInputEventBus`（读取面，D1/F3），Enable*/DisableAll 留在 D1 控制面
- `IComboCounter`（C1，段数唯一写者 `CurrentCombo{get;private set;}` D3:196）
- `IRandomSource`（`SystemRandomSource`，新接口无历史分歧）
- `IDSPClock.GetDspTime()`（D6，三处命名分歧已收敛 per D8 §5，D6:95–96）

### A3/A4/A5 → ✅ 全部 CLEARED

- A3（D1 命名漂移）：`.BgmFilePath` / `OnBeatExact`/`OnHalfBeatLookahead` per D8 §1
- A4（BeatClock 事件 schema 无权威）：D8 §1/§6 完整公共事件面 + pause 源单发布者
- A5（authoredBpm 边界）：D4:165 与 D5:88 统一 `[60,240]`

### 残留（LOW — 非阻塞文档漂移）

- **D3 未同步 D8 钦定规范名**：仍用 `BeatClock.OnPaused`（adr-d3:91/260）而非 `OnBeatClockPaused`；
  `AttackType` 枚举缺 `HeavyAttack`（D8:263 已标注、Migration §2 登记）。属 D8 权威下的 D3 待办文本同步，不阻塞实现。
- **E1**（D2 `BeatClockBridge(-100)` 先于 BT(-50) 的"同帧 beat"保证）仍标 pending ADR-D2 revision（TR-director-010）。
  引擎时序 advisory，MVP BPM 恒定影响有界，建议随 D2 修订收口。

---

## ADR Dependency Order

拓扑排序（DAG，无环、无未决依赖——全部指向 Accepted）：

```
Foundation:  D6 (Audio), D7 (Save)
      ↓
      D1 (TDE Integration ← D6)
      ↓
      D5 (Animation ← D1)
      ↓
依赖层:  D2 (← D1) · D3 (← D6,D1,D5) · D4 (← D6,D5,D1) · D10 (← D6,D7,D1)
      ↓
收口:    D8 (← D3,D6,D1,D10)
```

推荐实现顺序：`D6, D7 → D1 → D5 → {D2, D3, D4, D10} → D8`。未决依赖 0，依赖环 0。

---

## Engine Compatibility

| 项目 | 结果 |
|------|------|
| 引擎版本一致性 | ✅ VERSION.md 与 9 ADR + 5 插件文档"Verified with Unity"字段一致，无漂移 |
| 含 Engine Compatibility 段的 ADR | **9/9**（无盲区） |
| Deprecated API 引用 | **0**（对照 deprecated-apis.md 逐条检索无命中） |
| 陈旧版本假设 | 0（TDE 4.5 / Animancer 8.4.0 / FMOD 2.02.35 / ES3 3.5.26 / Feel 5.9.1 一致） |
| Post-Cutoff API 冲突 | 0（D2 Unity Behavior + D6 FMOD 均有 M0 Spike 源码级验证 PASS） |

### Engine Specialist Findings

**H1 (HIGH — 文档缺陷，非引擎/实现阻塞)**：ADR-D8 内部自相矛盾——`InternalsVisibleTo` 目标程序集名。
Implementation Guidelines (`adr-d8:318`)、Risk 表 (`:379`)、Migration Plan §6 (`:402`) 三处写
`CombatCore.Tests.EditMode`，但 Spike Verification 表 (`:21`) 与**实际已跑绿代码**
（`AssemblyInfo.cs:10` = `InternalsVisibleTo("EditModeTests")`；`EditModeTests.asmdef` 引用 `CombatCore`）
用的是 `EditModeTests`。代码库不存在 `CombatCore.Tests.EditMode` 程序集。实际代码正确编译、CI 跑绿、
不阻塞进度，但若 C1-C4 后续 Mock 照抄 Migration/Risk 字面文本会重触 CS0122。
**建议**：三处统一改为 `EditModeTests`。

**M1 (MEDIUM)**：ADR-D6 FMOD 原生插件 Unity Editor 编译确认（P0）仍开放（`m0-spike-report.md:433`）。
getDSPClock BLOCKER B5 已在 D6 正文修复（`adr-d6:92–99`，`getMasterChannelGroup()→getDSPClock(out ulong)`
签名与 M0 报告一致）；仍开放项为真机 Editor 侧原生插件编译确认——运行时验证欠账，非架构阻塞。

**M2 (MEDIUM/P2)**：`plugins/fmod.md` + `plugins/unity-behavior.md` 缺失（已知欠账，源材料已具备，回填成本低）。

**LOW**：无新增（`[SerializeField]` 属性误用 CS0592 扫描 0 命中；`FindObjectOfType`/`SendMessage`
仅 adr-d3:241 一处注释说明避免该反模式，非实际调用）。

### GDD Revision Flags

无。H1 是 ADR 内部文档矛盾（非 GDD 基于错误引擎行为写规则）；M1 涉及的 FMOD DSP 时钟行为，
`audio-beat-clock.md` 已通过 `IDSPClock.GetDspTime()` 抽象解耦，未对错误签名做假设。

---

## Pre-Gate Checklist（Unity 正确解读）

| 项目 | 状态 |
|------|------|
| `Assets/Tests/EditMode` + `Assets/Tests/PlayMode`（含 CombatCore DI seam spike） | ✅（skill 通用 `tests/unit` 路径 N/A，Unity 等价物就位） |
| `.github/workflows/tests.yml`（game-ci editmode+playmode） | ✅ |
| `design/accessibility-requirements.md` | ✅ |
| `design/ux/interaction-patterns.md` | ✅ |

---

## Verdict: CONCERNS（健康 — 无阻塞项）

名义上仍 CONCERNS（存在 15 Partial + 9 真实 Gap 待未来 GDD），但**这是首次达成**：
9/9 ADR Accepted、跨 ADR blocking conflicts=0、A1 唯一裁定真实消解、全部 pre-gate 项就位。
上轮三大遗留阻塞（D8 Proposed / A1 生命周期 / pre-gate 缺失）**全部清除**。
**无任何项阻挡进入 pre-production gate**；剩余为 pre-production 阶段本就预期的未来 GDD 缺口
与文档级 cleanup。

### 非阻塞 Cleanup（优先级排序）

1. **H1** — 修正 ADR-D8 三处 `InternalsVisibleTo` 目标名 → `EditModeTests`（防 C1-C4 Mock 照抄重触 CS0122）
2. **D3 文本漂移** — 随下次 D3 修订补 `OnBeatClockPaused` 规范名 + `AttackType.HeavyAttack`
3. **M1** — Unity Editor 侧 FMOD 原生插件编译确认（运行时验证，需真机 Editor）
4. **E1** — 随 ADR-D2 修订收口"同帧 beat"保证
5. **M2** — 回填 `plugins/fmod.md` + `unity-behavior.md`（P2）

### 后续路径

`/gate-check pre-production` 的前置条件已全部具备（ADR 全 Accepted + 测试框架 + CI + UX/a11y）。
可推进 gate-check；或推进 C1 rhythm-core 实现冲刺（DI 缝隙硬门已解锁）。
