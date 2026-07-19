# ADR-D3: Rhythm Quantization Core

## Status
Proposed

## Date
2026-07-18

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (6000.3.19f1) |
| **Domain** | Core / C# Game Logic |
| **Knowledge Risk** | LOW — 使用标准 C# 事件和 MonoBehaviour；无 Unity 6.3 post-cutoff API |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`; engine-specialist review 2026-07-18 |
| **Post-Cutoff APIs Used** | None |
| **Verification Required** | (1) `OnHalfBeatLookahead` 事件在主线程触发时 `_pendingInput` 可正确评估；(2) `BeatClock.Paused` 事件在 RhythmJudger 中通过直接事件绑定（非 Update 轮询）触发清除 |

> **Engine Specialist Review (2026-07-18)**: MINOR NOTES — 无 BLOCKING 问题。3 个 MEDIUM 发现已并入本 ADR。
> - M2: Script Execution Order 风险 → 改用直接事件绑定（见 Decision 节）
> - M3: `IBeatClock`/`IInputEventBus` 接口合约缺口 → 推迟至 C1 GDD 解决（见 Key Interfaces 注释）
> - M7: Off 模式 `_pendingInput` 不变量 → 见 Decision 节显式记录

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-D6 (Accepted) — IDSPClock / FMOD DSP 时钟; ADR-D1 (Accepted) — BeatClock 生命周期合约、BeatStateSnapshot; ADR-D5 (Accepted) — AnimancerAttackPlayer API |
| **Enables** | C1 GDD 创作; C2 GDD（ComboGraph 消费 RhythmJudgmentEvent); C4 GDD 设计（combat director 同样消费节拍事件）|
| **Blocks** | C1 实现 sprint; C2 实现 sprint; P1 HUD 评级显示实现; PL2 VFX 节奏反馈实现 |
| **Ordering Note** | 须先于 C1 GDD 完成。C1 GDD 以本 ADR 的 Key Interfaces 为唯一权威，并须解决 IBeatClock/IInputEventBus 接口合约问题（见 M3 说明）。 |

## Context

### Problem Statement

C1（节奏量化攻击系统）是 F2（节拍时钟）和 F3（输入管线）之间的桥接层。截至本 ADR，C1 没有定义：
- 判定评估触发时机（何时计算 `beat_offset_ms`）
- 输入缓冲策略（如何处理提前/滞后按键）
- 判定窗口规格（Perfect/Good/Miss 的毫秒阈值）
- 输出事件 schema（下游 C2、HUD、Feel 的消费接口）

C2（连招图）、HUD（P1）和 Feel/MMFeedbacks（PL2）均消费 C1 的输出事件，无法在接口未定案前进入实现阶段。

### Constraints

- **F2 Core Rule 7**：`BeatEventData.beat_dsp_time` 已含 `LatencyOffsetMs`（处于「玩家感知时间域」）；C1 须将 F3 的原始 `inputDspTime` 加回 `latency_offset_s` 后再与 `beat_dsp_time` 对比（见 F3 GDD F3-F2 公式）
- **F2 Core Rule 12**：C1 须在 `BeatClock.IsStartConfirmed == true` 后方可开始评估
- **Forbidden: beatclock_lifecycle_bypass**：C1 禁止调用 `BeatClock.Play() / Stop() / Pause() / Resume()`
- **ADR-D5 约束**：攻击动画须通过 `AnimancerAttackPlayer.Play(attackType, targetDspTime)` 触发
- **TR-input-013**：平台最小 Perfect 半窗口 — PC/Console ≥ 16.7ms；iOS ≥ 39ms；Android ≥ 80ms
- **F3 哨兵值合约**：`chargeStartDspTime == -1.0` 为哨兵，C1 须以 `> -1.0` 判断而非 `> 0`

### Requirements

- 支持三种量化模式：Off / HalfBeat / Beat
- 输出 `readonly struct` 事件（下游零 GC）：包含 rating、offset_ms、beatDspTime
- ChargedAttack 须以 `chargeStartDspTime` 为判定锚点（F3 合约）
- 平台自适应窗口下限（Start() 注入平台常量）
- `QuantizationMode` 可被 FE7 在运行时注入
- 不攻击的拍点不产生 Miss（无攻击 = 无事件）
- 暂停事件须即时清除输入缓冲，防止 Resume 后幽灵判定

## Decision

`RhythmJudger : MonoBehaviour` 挂载于玩家 Prefab，与 `JadeRabbitInputManager`（ADR-D1）和 `HybridAnimancerComponent`（ADR-D5）平级。采用**节拍锚点 + 预缓冲**模式：F3 输入到达时存入单槽缓冲区；每次 `OnHalfBeatLookahead` 事件触发时扫描缓冲区并评级；后拍点延迟窗口（LateWindow）内到达的输入即时评级。

### Architecture Diagram

```
F3.OnAttackInput ──────────────────► RhythmJudger._pendingInput  (单槽缓冲)
                                              │
F2.OnHalfBeatLookahead ───────────────────────┤
    beat.beat_dsp_time = T (实际拍点 DSP 时间)│
    回调在墙上时间 T - LookaheadMs 触发       │
                                              ▼
                                    TryEvaluateEarly(beat)
                                      offset = (jt + latency_s - T)*1000
                                      if offset ∈ [-EarlyWindowMs, 0]:
                                        → emit RhythmJudgmentEvent
                                        → AnimancerAttackPlayer.Play(type, T)
                                        → _pendingInput = null
                                        → _lateWindowActive = false
                                      else: 开启 LateWindow

F3.OnAttackInput (T 之后到达) ───────► TryEvaluateLate(e)
    if _lateWindowActive               offset = (jt + latency_s - T)*1000
    and offset ∈ [0, LateWindowMs]:    → emit RhythmJudgmentEvent（立即触发动画）

BeatClock.OnPaused (直接事件绑定) ──► _lateWindowActive = false; _pendingInput = null

Update() ──────────────────────────► expire LateWindow if Time.realtimeSinceStartup > _lateWindowExpiry
```

### Evaluation Algorithm

```
// 状态字段（均在 RhythmJudger 上）
AttackInputEvent? _pendingInput           // 单槽可空 struct 缓冲
double  _currentBeatDspTime              // OnHalfBeatLookahead 写入
bool    _lateWindowActive
float   _lateWindowExpiry                // Time.realtimeSinceStartup + LookaheadMs + LateWindowMs

// ── 订阅 F2 HalfBeat ──
void OnHalfBeatLookahead(BeatEventData beat):
    if !BeatClock.IsStartConfirmed: return
    _currentBeatDspTime = beat.beat_dsp_time
    _lateWindowExpiry   = Time.realtimeSinceStartup + (LookaheadMs + LateWindowMs) / 1000f
    _lateWindowActive   = true
    TryEvaluateEarly()

// ── 订阅 F3 AttackInput ──
void OnAttackInput(AttackInputEvent e):
    _pendingInput = e                    // 覆盖旧值（单槽，同一半拍内先到优先）
    if _lateWindowActive: TryEvaluateLate(e)

// ── Update 过期检查 ──
void Update():
    if _lateWindowActive && Time.realtimeSinceStartup > _lateWindowExpiry:
        _lateWindowActive = false
        _pendingInput = null             // 丢弃过期的预拍点输入

// ── 直接绑定 BeatClock 暂停事件（非 Update 轮询，避免 SEO 竞争）──
void OnBeatClockPaused():
    _lateWindowActive = false
    _pendingInput = null

// ── 辅助函数 ──
double GetJudgmentTime(AttackInputEvent e):
    return (e.attackType == ChargedAttack && e.chargeStartDspTime > -1.0)
           ? e.chargeStartDspTime        // 充能：以按下时刻为判定锚点
           : e.inputDspTime             // 普通攻击：以 performed 时刻为锚点

void TryEvaluateEarly():
    if !_pendingInput.HasValue: return
    double jt     = GetJudgmentTime(_pendingInput.Value)
    float  offset = (float)((jt + latency_offset_s - _currentBeatDspTime) * 1000)
    if offset < -EarlyWindowMs: return  // 过早（针对下一拍），保留在缓冲
    if offset > 0: return               // 后拍点输入，交由 LateWindow 处理
    EmitAndClear(offset)

void TryEvaluateLate(AttackInputEvent e):
    double jt     = GetJudgmentTime(e)
    float  offset = (float)((jt + latency_offset_s - _currentBeatDspTime) * 1000)
    if offset < 0 || offset > LateWindowMs: return  // 超出 LateWindow
    EmitAndClear(offset)

void EmitAndClear(float offset):
    var rating = Evaluate(Mathf.Abs(offset))
    emit new RhythmJudgmentEvent(
        _pendingInput.Value.attackType, rating, offset,
        _currentBeatDspTime, Mode, CurrentCombo)
    AnimancerAttackPlayer.Play(_pendingInput.Value.attackType, _currentBeatDspTime)
    UpdateCombo(rating)
    _pendingInput = null
    _lateWindowActive = false

RhythmRating Evaluate(float absOffset):
    if absOffset <= PerfectHalfWindowMs: return Perfect
    if absOffset <= GoodHalfWindowMs:    return Good
    return Miss    // 攻击仍执行；伤害倍率 0.5x（由 C2/C6 读取）

// ── QuantizationMode.Off 不变量 ──
// Off 模式下 OnAttackInput 立即消费 _pendingInput：
//   offset = 0, rating = Perfect → EmitAndClear(0)
// _pendingInput 永远不持久化。OnBeatClockPaused 的清除仅对 HalfBeat/Beat 模式有意义。
// 禁止在 Off 模式下添加任何持久化 _pendingInput 的代码路径。

// ── QuantizationMode.Beat ──
// 订阅 OnBeatLookahead 而非 OnHalfBeatLookahead（逻辑相同，网格单位变为全拍）
```

### Key Interfaces

```csharp
// 输出事件（readonly struct — 零 GC）
public readonly struct RhythmJudgmentEvent
{
    public readonly AttackType      AttackType;
    public readonly RhythmRating    Rating;        // Perfect | Good | Miss
    public readonly float           OffsetMs;      // 有符号，毫秒；负 = 提前，正 = 滞后
    public readonly double          BeatDspTime;   // 参考半拍 DSP 时间（来自 F2）
    public readonly QuantizationMode Mode;
    public readonly int             ComboCountBefore; // 此次判定前的 combo 计数
}

public enum RhythmRating   { Perfect, Good, Miss }
public enum QuantizationMode { Off, HalfBeat, Beat }

// RhythmJudger 公开接口
public class RhythmJudger : MonoBehaviour
{
    public event Action<RhythmJudgmentEvent> OnRhythmJudgment;
    public QuantizationMode Mode { get; set; }   // FE7 在运行时注入
    public int CurrentCombo { get; private set; }

    // 属性注入（MonoBehaviour 不支持构造函数注入）
    // 注意：[SerializeField] 禁止用于以下属性（Unity 6.3 LTS 仅支持字段级序列化）
    // Inspector 通过具体类型配置，接口由 C1 GDD 决定是否正式声明
    public IBeatClock             BeatClock    { private get; set; }
    public IInputEventBus         InputBus     { private get; set; }
    public IAnimancerAttackPlayer AttackPlayer { private get; set; }
    public RhythmConfigSO         Config       { private get; set; }
    // ⚠ M3 缺口：IBeatClock、IInputEventBus 尚未在已接受 ADR 中声明。
    // C1 GDD 须决定：(A) BeatClock 和 InputEventBus 暴露接口（推荐：可测性），
    // 或 (B) RhythmJudger 直接引用单例（简单但不可单元测试）。
    // 此决定须在 C1 实现 sprint 开始前落地。
}

// 调参配置（ScriptableObject）
[CreateAssetMenu(menuName = "JadeRabbit/Config/RhythmConfig", fileName = "RhythmConfigSO")]
public class RhythmConfigSO : ScriptableObject
{
    public float PerfectHalfWindowMs = 50f;   // 创作默认值；Start() 时以平台下限钳位
    public float GoodHalfWindowMs    = 120f;
    public float EarlyWindowMs       = 120f;  // 预拍点缓冲窗口（输入早于拍点最多此值）
    public float LateWindowMs        = 100f;  // 拍点后容忍窗口
    // 平台最小值（Start() 自动注入，不在此处暴露）:
    //   PC/Console: 16.7ms | iOS: 39ms | Android: 80ms
    // 不变量验证（Start() 断言）:
    //   GoodHalfWindowMs < halfBeatDurationMs / 2（防止相邻拍点窗口重叠）
    //   PerfectHalfWindowMs < GoodHalfWindowMs
}
```

## Alternatives Considered

### Alternative 1: 输入锚点评估（Input-Anchored）
- **描述**: F3 输入到达时，C1 立即查询 `BeatStateSnapshot.nearest_half_beat_dsp_time` 计算偏差并评级
- **优点**: 无缓冲，实现最简单
- **拒绝原因**: `nearest_half_beat_dsp_time` 指向*过去*拍点——在下一拍点到来的 5ms 前按键，偏移计算为 `-(halfBeatDuration - 5ms)`，被判为 Miss。玩家明明踩得很准，判定系统却认为踩晚了。破坏核心支柱「节拍即奖励」的信任基础。

### Alternative 2: 双窗口混合（Dual-Window）
- **描述**: 提前输入缓冲等拍点确认；延迟输入立即与上一拍点对比
- **优点**: 两个方向均有最优精度
- **拒绝原因**: 与本 ADR 所选方案语义等价，但引入更多状态分支。保留为 post-MVP 精调选项；若所选方案出现覆盖不足再升级。

### Alternative 3: RhythmJudger 为 DontDestroyOnLoad 单例
- **描述**: C1 挂在持久化 Manager GameObject 上，类似 InputEventBus
- **优点**: 全局访问无需 FindObjectOfType
- **拒绝原因**: C1 判定是玩家中心的（combo 计数、攻击状态机）。与玩家 Prefab 同生命周期可简化 Reset 逻辑（死亡 → Prefab 销毁 → combo 归零）；单例化需要跨场景同步 combo 状态，额外复杂。

## Consequences

### Positive
- 预拍点 EarlyWindowMs 内的输入正确评级为 Perfect/Good，维护玩家对判定系统的信任
- `QuantizationMode.Off` 提供无障碍通道（节奏障碍玩家可正常游戏）
- 单槽缓冲 O(1)，零 GC，确定性行为
- `IBeatClock`/`IInputEventBus`/`IAnimancerAttackPlayer` 接口注入支持纯 C# 单元测试（Mock 注入，无需 PlayMode）

### Negative
- 单槽缓冲：同一半拍内快速双击仅评估第一次输入（第二次覆盖）。C1 GDD 须将此记录为设计约束，非 Bug。
- 攻击动画调度时间为 `_currentBeatDspTime`（拍点 DSP 时间），而非输入到达时间。后拍点输入立即触发动画。玩家感受到攻击被「吸附」到拍点，这是量化机制的设计意图。

### Risks
- **窗口重叠**: `GoodHalfWindowMs > halfBeatDurationMs / 2` 时，相邻拍点的 Good 窗口重叠，单个输入可归属两个拍点。缓解：`Start()` 断言 `GoodHalfWindowMs < 60000f / currentBpm / 2`；BPM 变更时重新验证。
- **Android 80ms 下限**: 120 BPM 时半拍 = 250ms，PerfectHalfWindowMs 钳位至 80ms（32%）。C1 GDD 须为 Android 设置更宽的 GoodHalfWindowMs（建议 ≥ 150ms），确保 Perfect/Good 两档有实质区别。
- **LateWindow 过期精度**（M2）: `Time.realtimeSinceStartup` 在 30fps 移动端引入最多 33ms 过期误差。MVP 可接受；post-MVP 可切换至 DSP 时钟计算过期（`_lateWindowExpiry = _currentBeatDspTime + LateWindowMs/1000.0`，在 DSP 域比较）。
- **`OnBeatClockPaused` 绑定方式**（M2 修复）: `RhythmJudger` 须在 `Start()` 中直接绑定 `BeatClock.OnPaused += OnBeatClockPaused`，而非在 `Update()` 中轮询暂停状态。直接事件绑定消除 Script Execution Order 竞争（TDELifecycleAdapter 触发暂停 → BeatClock 广播 → RhythmJudger 同帧清除，而非等到下一 Update）。

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|---|---|---|
| game-concept.md | TR-concept-006: Half-beat grid snap (hybrid model) | HalfBeat 模式在 OnHalfBeatLookahead 评估；Off 模式绕过量化；Beat 模式订阅 OnBeatLookahead |
| game-concept.md | TR-concept-007: Perfect/Good/Miss 评级，伤害倍率 1.0/0.75/0.5 | RhythmJudgmentEvent.Rating 驱动 C2/C6 伤害倍率；本 ADR 定义评级计算逻辑 |
| game-concept.md | TR-concept-008: Perfect 动态效果（freeze frame, hitstun ×1.3, camera micro-cut）| OnRhythmJudgment 订阅者（PL2 VFX、相机 PL1）接收 Rating；本 ADR 定义事件 schema |
| input-event-system.md | TR-input-013: Android DSP 粒度 23-46ms 影响 C1 窗口 | Start() 注入平台最小窗口常量（PC 16.7ms / iOS 39ms / Android 80ms）|
| audio-beat-clock.md | TR-beatclock-003: 全量网格广播，模式无关 | C1 订阅 OnHalfBeatLookahead（F2 无条件广播，模式选择在 C1 订阅层）|
| audio-beat-clock.md | TR-beatclock-004: BeatStateSnapshot 同步查询 | Off 模式使用 BeatStateSnapshot 实现即时评估；IsStartConfirmed 前置检查 |

## Performance Implications
- **CPU**: ~0.02ms/半拍（单次可空比较 + 1 次事件分发）
- **Memory**: `AttackInputEvent?` 可空 struct 字段在 RhythmJudger 上约 80 字节（堆上 MonoBehaviour 字段）
- **GC**: 热路径零 GC — `RhythmJudgmentEvent` 为 `readonly struct`；所有字段为值类型（float/double/int/enum）；无装箱

## Migration Plan
N/A — C1 为全新系统，无存量实现。

## Validation Criteria
- **[Unit]** 输入偏移 `|offset| ≤ PerfectHalfWindowMs` → rating = Perfect（注入 MockBeatClock + MockInputBus）
- **[Unit]** 输入偏移 ∈ `(PerfectHalfWindowMs, GoodHalfWindowMs]` → rating = Good
- **[Unit]** 输入偏移 > `GoodHalfWindowMs` → rating = Miss，且 `AnimancerAttackPlayer.Play()` 仍被调用
- **[Unit]** OnHalfBeatLookahead 时缓冲区无输入 → 无 `RhythmJudgmentEvent` 发出
- **[Unit]** Off 模式：OnAttackInput 立即发出 rating = Perfect，不等待任何 BeatClock 事件
- **[Unit]** ChargedAttack 以 `chargeStartDspTime` 为判定锚点（`chargeStartDspTime > -1.0`）
- **[Unit]** OnBeatClockPaused 后 `_pendingInput == null` 且 `_lateWindowActive == false`；Resume 后不产生幽灵判定
- **[Unit]** Android 平台：`Start()` 钳位 `PerfectHalfWindowMs ≥ 80ms`
- **[Unit]** Windows 窗口不变量：`Start()` 断言 `GoodHalfWindowMs < halfBeatDurationMs / 2` at 120 BPM 通过
- **[PlayMode]** LateWindow 内后拍点到达的输入正确评级并触发动画

## Related Decisions
- ADR-D1 — TDE Integration：BeatClock 生命周期由 TDELifecycleAdapter 独占控制（beatclock_lifecycle_bypass 禁止模式）
- ADR-D5 — Animation Pipeline：AnimancerAttackPlayer API 由 RhythmJudger 调用，攻击层动画从此处驱动
- ADR-D6 — Audio Middleware：IDSPClock 接口；FMOD 为 BeatClock DSP 时钟来源
- ADR-D2 — Enemy AI Framework：Unity Behavior BeatClockBridge 向 AI Blackboard 注入节拍状态，AI 不直接消费 OnRhythmJudgment（AI 通过 F2 事件而非 C1 输出感知节拍）
