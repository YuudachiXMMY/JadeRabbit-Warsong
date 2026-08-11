# Architecture Review Report

> **Date**: 2026-08-11
> **Engine**: Unity 6.3 LTS (6000.3.19f1)
> **Mode**: `/architecture-review full`
> **GDDs Reviewed**: 9 系统级（F2/F3/F4/F5/F6 + C1/C2/C3/C4）+ concept
> **ADRs Reviewed**: 8（D1/D2/D3/D4/D5/D6/D7/D10）
> **Verdict**: **CONCERNS**

---

## 基线漂移警示

`tr-registry.yaml`（83 条）与 `traceability-index.md`（Rev.6）此前均停留在 **2026-07-19**，仅覆盖
Foundation + concept，**未纳入 Core 层 C1-C4（63 条新 TR）与 ADR-D10（bootstrap-sequencing）**。
本轮为首次将 Core 层纳入可追溯矩阵，registry 相应追加 63 条 Core TR，索引升级至 Rev.7。

---

## Traceability Summary

总需求：**146**（Foundation+concept 83 + Core 63）

| 系统 | GDD | TR 数 | 主覆盖 ADR | 状态 |
|------|-----|------|-----------|------|
| concept | game-concept | 14 | D2/D3/D4/D5/D6 | 5✅ 4⚠️ 5❌（延后 FE3/C5） |
| beatclock (F2) | audio-beat-clock | 17 | D6/D10（GDD=规范） | GDD 即架构，内部项非 ADR 缺口 |
| input (F3) | input-event-system | 15 | D1（GDD=规范） | 同上 |
| datalayer (F4) | so-data-layer | 12 | **D4 全覆盖** | ✅ |
| save (F5) | save-persistence | 13 | D7/D10（GDD=规范） | 同上 |
| tde (F6) | tde-integration | 12 | D1/D5/D10 | 8✅，D10 补齐初始化时序 |
| **rhythm (C1)** | rhythm-core | 18（新） | **D3 主架构** | 大部覆盖；DI 接口所有权缺口 |
| **combo (C2)** | combo-system | 14（新） | D3/D4/D5 | 状态机=GDD 逻辑；IComboCounter 缺 |
| **perk (C3)** | perk-system | 13（新） | D4 | IRhythmModifierProvider 已接通；RNG/pause 源缺 |
| **director (C4)** | combat-director | 18（新） | D2/D6/D10 | 令牌调度=GDD 逻辑；IDSPClock mock 缺 |
| **合计** | | **146** | | |

Core 层各系统的**跨切面**决策均有 ADR 承接：D3 覆盖 C1 判定；D4 覆盖全部 SO schema；
D5 覆盖 Animancer 变速；D2 覆盖 C4 令牌/AI 框架；D6/D10 覆盖线程与初始化。
C2/C3/C4 无专属 ADR 属**可接受**——连招状态机、令牌调度器为应用层逻辑，
与 beatclock/input/save 一样以 GDD 为规范文档。

---

## 跨 ADR 冲突（Phase 4）

| # | 冲突 | 类型 | 严重度 | 说明 |
|---|------|------|--------|------|
| **A1** | D1 vs D10 | 生命周期方法不一致 | 🔴 BLOCKING（实现前必修） | 同一 `TDELifecycleAdapter` 类，D1 在 `OnEnable()` 订阅 `SceneManager.sceneLoaded`，D10 在 `Start()` 订阅。逐字实现将**重复订阅** → 每次场景加载重复 `BeatClock.Play()`/`EnableGameplayInput()`。须裁定单一实现（建议 `OnEnable`/`OnDisable` 配对，与 `MMEventListener<T>` 一致）。 |
| **A2** | D3（M3）× 全 Core | 接口所有权缺口 | 🟠 HIGH | `IBeatClock`/`IInputEventBus` 被 D3 要求注入，但**未被任何已接受 ADR 拥有**（D3 自标 M3 未决）。C1 GDD 称"字段注入已解决"，但接口本身无归属。连同 C2 `IComboCounter`、C3 `IRandomSource`+pause 事件源、C4 `IDSPClock` mock——**四个 Core 系统共同的确定性测试缝隙缺口**，直接威胁 80% 单测覆盖率硬门。 |
| **A3** | D1 命名过时 | 集成契约漂移 | 🟠 HIGH | D1 代码用 `LevelConfigSO.BgmAssetReference`，D4 已重命名为 `bgmFilePath`（string，非 AssetReference）。逐字实现 D1 将引用不存在的成员。D1 亦用 `OnBeatExact`，与 D2/D3/D5 的实际事件名不符。 |
| **A4** | 全 ADR × F2 | BeatClock 公共事件 schema 无权威定义 | 🟠 HIGH | 五个来源引用不同事件名：`OnBeatExact`/`OnBeatSnapshot`/`OnHalfBeatLookahead`/`OnBeatLookahead`/`OnPaused`/`OnBeatClockResumed`/`OnMeterChanged`/`OnMeterChangeAccepted`。无单一 ADR 或 F2 GDD 权威锁定 BeatClock 公共面——这正是 cross-review 反复出现的悬空引用（B1/B3/OnBeatClockResumed）根因。F2 GDD 正"待复审"，须在同轮锁定。 |
| **A5** | D4 vs D5 | 校验边界不一致 | 🟡 MEDIUM | `AttackNodeSO.authoredBpm`：D4 约束 `[60,240]`，D5 Odin 校验 `[1,240]`。Odin Validator 须择一。 |
| **A6** | D6 文档 | Related Decisions 误标 | 🟢 LOW | D6 把 ADR-D5 称作"Rhythm Core"（实为 D3）。文档更正。 |

**依赖图**：`D6,D7 → D1 → D5 → {D2,D3,D4} → D10`，为 DAG，**无循环依赖**。
性能预算合计 ≈0.73ms/帧，远低于 16.6ms 预算，**无预算冲突**。

### 推荐 ADR 实现顺序（拓扑排序）

1. **Foundation（无依赖）**：D6（Audio Middleware）、D7（Save/Persistence）
2. **依赖 Foundation**：D1（TDE Integration，需 D6）
3. D5（Animation Pipeline，需 D1）
4. **特性层**：D2（Enemy AI，需 D1）、D3（Rhythm Quantization，需 D6/D1/D5）、D4（SO Data Layer，需 D6/D5/D1）
5. **收口**：D10（Bootstrap Sequencing，需 D6/D7/D1）

无未解依赖（所有被依赖 ADR 均 Accepted），无依赖环。

---

## 引擎兼容性（Phase 5）

8/8 ADR 均含完整 Engine Compatibility 段；**无废弃 API 引用、无过时版本、无 post-cutoff API 冲突**。
ADR 纪律优秀（M0 Spike 对已装源码验证 API 契约，已提前捕获 `TopDownEngineEventTypes.Respawn`
不存在、`ES3.RenameFile` 原生可用等）。

### Engine Specialist Findings

| # | 发现 | 严重度 |
|---|------|--------|
| **E1** | **D2 "同帧 beat 数据"保证不成立**：`BeatClockBridge` 无 `Update()`，`[DefaultExecutionOrder(-100)]` 对其事件回调无效；`BeatClock` 默认 order=0，晚于 `BehaviorGraphAgent` 的 `-50` → BT 读到**滞后一帧**的 `CurrentBeat`/`TimeToNextBeat`。MVP 中 BPM 恒定故影响有界，但节拍变量会受影响。修：给 BeatClock 显式更负 order，或删除误导性属性改为"同调用栈即时"（实际更强的保证）。 | 🔴 HIGH |
| **E2** | **D5 "OnEnd 方法组零 GC"断言错误**：`OnAnimationEnd` 为实例方法，方法组转换必分配闭包委托（仅静态方法免分配）。每次攻击一次堆分配，与"零 GC/IL2CPP"矛盾。修：`Awake()` 缓存委托复用，或改写措辞为"每次攻击一次有界分配"。 | 🟠 MEDIUM-HIGH |
| **E3** | **Enter Play Mode（禁用 domain reload）未处理**：`BeatClock`/`SaveService`/`InputEventBus` 单例 + D10 的 `_offsetInjected` 守卫标志会跨 Play session 残留 → "首次 Play 正常、二次 Play 崩"类 bug。修：声明需启用 domain reload，或加 `[RuntimeInitializeOnLoadMethod]` 重置钩子。 | 🟡 MEDIUM |
| **E4** | PerkSO `Instantiate()` 触发 clone 的 `OnEnable()`（当前无逻辑，未来加缓存/订阅是陷阱）；DontDestroyOnLoad 单例未显示重复实例守卫。 | 🟢 LOW |

**确认正确**：D6 FMOD DSP 时钟正确避开 `AudioSettings.dspTime`（Android 上 23-46ms 量化）改读
`getSoftwareFormat()`；D3 `RhythmJudgmentEvent` readonly struct 确为零 GC；无 ADR 使用 Rigidbody/Physics
故物理 breaking-changes 无影响。建议补一份 `com.unity.behavior` 引擎参考文档，避免后续 ADR 重新推导 API。

---

## GDD 修订标记（Phase 5b：架构 → 设计反馈）

| GDD | 假设 | 引擎现实 | 建议动作 |
|-----|------|---------|---------|
| combat-director.md (C4) | TR-director-010 依据 ADR-D2 假设 `BeatClockBridge(-100)` 先于 BT(-50) 写入 Blackboard，BT 节点读到当帧 beat | E1：该保证不成立，BT 读到滞后一帧 | 复审 C4/未来 FE1：AI 决策节点不应依赖"当帧" beat 精度，或等 D2 修订后更新 |

其余 GDD 假设与已验证引擎行为一致。此标记为 **ADVISORY**（MVP 中 BPM 恒定，影响有界）——
建议随 D2 修订同轮更新。**本轮不改 systems-index 状态**（用户 08-10 裁定仅标 C1；此项 advisory 不触发状态变更）。

---

## Architecture Document Coverage（Phase 6）

`docs/architecture/architecture.md` 不存在——无孤儿架构可查。系统总账以 `systems-index.md` 为准。

---

## Verdict: CONCERNS

Foundation 与 Core 层的跨切面决策均有 ADR 承接，无循环依赖、无预算超支、无废弃 API。
但存在**须在对应系统进入实现前解决**的问题，故非 PASS；这些问题均为同轮 ADR 修订/新增可解，
非架构级根本矛盾，故非 FAIL。

### Blocking-for-implementation（进入实现前必修）

1. **A1** — D1/D10 `TDELifecycleAdapter` 订阅生命周期二选一（阻塞 F6 编码）
2. **A2** — 新增 **DI/测试缝隙 ADR**：统一拥有 `IBeatClock`/`IInputEventBus`/`IDSPClock`(+mock)/`IComboCounter`/`IRandomSource`/规范 pause-resume 事件源（阻塞 C1-C4 单测，威胁 80% 覆盖率硬门）
3. **A4** — F2 GDD 复审时锁定 BeatClock 公共事件 schema，消除 `OnBeatExact`/`OnBeatClockResumed` 等悬空引用（阻塞 C1/C4 订阅编译）
4. **A3** — 修订 D1 过时命名（`BgmAssetReference`→`bgmFilePath`，`OnBeatExact`→规范名）
5. **E1** — 修正 D2 同帧保证（改属性或改措辞）

### Required ADRs（按价值排序）

1. **ADR-D8（新）DI / 确定性测试缝隙契约** — 最高价值，一份 ADR 解 A2 + 四个 Core 系统的 OQ（C1-M3 / C2-OQ4 / C3-OQ6/7 / C4-OQ1）
2. **修订 ADR-D1** — 命名同步 + 生命周期裁定（A1/A3）
3. **修订 ADR-D2** — 同帧保证更正（E1）+ EnemyInfoSO→Blackboard→TokenAction 布线定义
4. **修订 ADR-D5** — 零 GC 措辞更正（E2）
5. **F4 schema 补充** — `PlayerInfoSO.unlockedPerkIds`（C3-OQ1）+ authoredBpm 校验边界统一（A5）

---

## Phase 9 — Pre-gate 清单（Glob 核查）

| 项 | 状态 | 需运行 |
|----|------|--------|
| `tests/unit/` `tests/integration/`（及 `Assets/Tests/`） | ❌ 全缺 | `/test-setup` |
| CI `.github/workflows/tests.yml` | ❌ 缺 | `/test-setup` |
| `design/ux/accessibility-requirements.md` | ❌ 缺 | `/ux-design` |
| `design/ux/interaction-patterns.md` | ❌ 缺 | `/ux-design` |
| `docs/architecture/control-manifest.md` | ❌ 缺 | `/create-control-manifest`（可选） |

**因所有 pre-gate 项均 ❌，不提供 `/gate-check pre-production` 选项**——须先补齐测试基建与 UX/无障碍文档。

### Rerun trigger
每写完一份新/修订 ADR 后重跑 `/architecture-review`，验证覆盖率提升、冲突消解。
