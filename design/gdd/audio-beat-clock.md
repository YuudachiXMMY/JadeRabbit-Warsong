# 音频/拍点时钟系统 (Audio Beat Clock System)

> **Status**: Approved (Amended — B1 事件契约补订 OnMeter* + 2026-08-10 blocker fix — BC-6 OnBeatClockPaused/Resumed·IAudioPlayer.Pause/Resume, SC-1 F6 承接, B3 ScheduleBGMTransition, Pending Re-review)
> **Author**: Jadyn Wu + Claude Code Agents
> **Last Updated**: 2026-08-08
> **Implements Pillar**: 支柱一：节奏即战斗 (Rhythm IS Combat)
> **System ID**: F2
> **Layer**: Foundation | **Milestone**: MVP | **Design Order**: 1

---

## Overview

F2 音频/拍点时钟系统是《玉兔：破阵》节奏战斗体系的时序基础设施——它持续将音乐的拍点（BPM + 拍号）广播为高精度时间戳，供所有下游系统使用。所有攻击量化（C1）的吸附网格、战斗导演（C4）的拍槽令牌分配、延迟校准（FE3）的偏移测量，以及 HUD 节奏指示器（P1）的脉冲驱动，均以本系统输出的节拍事件为唯一真值时钟。

本系统对上层系统暴露两种输出：**节拍事件流**（每拍/每半拍触发，附带精确时间戳）和**节拍状态快照**（当前 BPM、拍号、最近拍点时间、距下一拍点距离）。支持运行时 BPM 和拍号切换（Boss 阶段切换所需）。底层音频中间件的选型（Unity Audio DSP vs FMOD Studio 2.x）见 ADR-D6，本 GDD 定义与中间件无关的行为规格。

---

## Player Fantasy

音频/拍点时钟系统本身对玩家不可见——玩家无需知道它的存在。它的成功体现在：每一次踩拍攻击都感觉扎根于现实的音乐节拍中，而非随机的程序误差；Boss 的跳砍恰好在鼓点上落下，让「听音乐防御」成为真实直觉而非运气。

这是**缺席型幻想**：F2 正常工作时，它应当完全透明；它一旦精度失效，玩家不会认为「时钟坏了」，而会认为「游戏手感很烂」，整个节奏体验随之崩溃。

节奏层的直接玩家幻想（「战斗即演奏，演奏即战斗」的沉浸感）属于 C1（节奏量化攻击系统）；F2 是这种幻想得以实现的物理前提。


---

## Detailed Design

### Core Rules

1. **单例真值时钟**: **BeatClock 为 `MonoBehaviour` 单例，挂载于 `DontDestroyOnLoad` 的专属 GameObject（命名为 `[BeatClock]`），禁止在 Inspector 手动配置**。全局唯一一个实例，通过 `BeatClock.Instance` 静态属性访问。所有需要拍点时序的系统从 BeatClock 获取数据，不得自行计算节拍时间。

2. **DSP 时钟基准**: 所有时间戳基于音频 DSP 时钟（生产：FMOD DSP 时钟 via `IDSPClock`，见 ADR-D6；测试 fallback: `MockDSPClock`），不基于 `Time.time` 或 `Time.unscaledTime`，消除帧量化误差（60fps 下 `Update()` 误差高达 16.7ms，超出可接受范围）。

3. **全量网格广播（无过滤）**: F2 始终广播完整节拍网格——每**全拍**和每**半拍**各一次——不知晓上层的量化策略（Off/HalfBeat/Beat）。量化模式选择由 C1 在订阅层实现，不影响 F2 接口。

4. **双接口模式**: 上层系统通过两种方式访问节拍数据：
   - **事件订阅**（异步推送）：`OnBeatLookahead` / `OnHalfBeatLookahead`（拍点前 `LookaheadMs` 广播）和 `OnBeatExact` / `OnHalfBeatExact`（精确到达时刻）
   - **状态查询**（同步拉取）：随时查询 `BeatStateSnapshot`，获取当前 BPM、拍号、小节号、拍内序号、距下一拍/半拍剩余时间

5. **前向调度（Lookahead）**: 前向事件在实际拍点到来前 `LookaheadMs`（默认 100ms，可配置）广播，携带精确的**未来拍点 DSP 时间戳**。动画和特效系统通过 Lookahead 事件提前准备，以拍点时间戳触发，实现零延迟同步。

6. **BPM/拍号切换边界对齐**: 所有 BPM 或拍号的切换请求，等待**当前小节完全结束后**（最后一拍的 Exact 事件广播后）才生效。过渡状态（`PendingBPMChange` / `PendingTimeSignatureChange`）期间继续以当前 BPM/拍号正常运行，不中断节拍流。

7. **玩家延迟偏移内化（两阶段写者，ADR-D10 D-5）**: F2 持有被外部注入的 `LatencyOffsetMs`（默认 0ms），**BeatClock 自身从不加载此值**——它是纯持有者（pure holder），遵循两阶段写者模型：**启动期**由 F6 `TDELifecycleAdapter` 在首个 `OnSceneLoaded(Single)` 内、首次 `BeatClock.Play()` 之前，从 F5 `LatencyCalibrationDTO` 一次性 boot-inject（`isCalibrated=false` 时注入默认 0ms）；**运行期**由 FE3 校准流程作为唯一写者更新（见 Rule 10）。BeatClock **禁止**自 load（registry forbidden pattern `f2_self_load_latency_from_f5`）。所有向上层广播的事件时间戳（`BeatEventData.beat_dsp_time`）均已应用偏移（`adjusted_beat_time = raw + offset/1000`），处于「玩家感知时间域」。**仅消费 F2 时间戳的系统**（P1 HUD、PL2 VFX、C4 战斗导演）无需自行处理延迟补偿。**跨域比较场景**：C1 需将 F3 的原始 DSP 时间戳（`inputDspTime`，未含偏移）与 F2 的已调整时间戳（`beat_dsp_time`，已含偏移）对齐——C1 须在 F3 时间戳上加 `latency_offset_s` 完成坐标系对齐（见 F3 GDD F3-F2 公式）。这不是"上层自行补偿延迟"，而是对齐两个不同时间域的标准操作。

8. **游戏暂停规则（含暂停/恢复事件契约，BC-6）**: 收到暂停信号（F6 经 `Pause()` 方法调用发起）时，停止广播新事件并保存当前进度（小节号、拍内序号、已播放 DSP 时间），状态转入 `Paused`，**在状态翻转之后同帧广播 `OnBeatClockPaused` 事件**。恢复时（F6 经 `Resume()` 方法调用发起）重新锚定 DSP 基准（暂停期间底层 DSP 仍在运行，见 F2-F3 暂停恢复公式），从暂停位置无缝续播，状态转回 `Playing`，**在完成重锚定之后、恢复后首个拍点 Exact 事件广播之前的同帧广播 `OnBeatClockResumed` 事件**，下一个应广播的拍点事件在恢复后按正确时序继续。
   - **事件契约（BC-6，F2 权威定义）**：`OnBeatClockPaused` 与 `OnBeatClockResumed` 均为 C# `event`（签名 `event Action<BeatClockPauseEventData>`，非 `EventHandler<T>`；payload `BeatClockPauseEventData` 为 `readonly struct`，全字段纯值类型，零 GC）。二者**成对广播**：任一次「暂停→恢复」周期各恰好触发 1 次，`OnBeatClockPaused` 必先于其配对的 `OnBeatClockResumed`。
   - **`OnBeatClockResumed` payload 语义**：携带 `resumeFirstBeatDspTimeRaw`（**恢复后首拍的 raw DSP 时间戳**，遵循 Core Rule 13 raw 坐标系，**禁止叠加 offset**）与恢复时刻的 `beatIndexInMeasure`、`measureIndex`。**恢复即「下游可安全重锚」信号**：C1 订阅后重锚拍网格并清空过期 `_pendingInput`；C3 心法冻结栈订阅后恢复计时/解冻（见 Dependencies）。`OnBeatClockPaused` payload 携带暂停时刻的 `beatIndexInMeasure` 与 `fractionalProgressAtPause`（拍内已过去时间，秒），供下游冻结当前状态。
   - **三方唯一信号源**：暂停/恢复由 F6 `TDELifecycleAdapter` 以 `Pause()` / `Resume()` **方法调用**驱动（发起方），F2 在状态翻转时广播这两个**事件**（C1/C3 为订阅方）。三系统共享 F2 这唯一一对暂停/恢复事件，禁止各自定义 `OnGamePaused`/`OnGameResumed` 等并行信号（消解 BC-6 信号机制不一致）。**状态字段必须在事件广播之前完成翻转**（与 Rule 14 一致），保证回调内同步查询 `BeatStateSnapshot.clockState` 读到新值。底层音频播放器的暂停/恢复通过 `IAudioPlayer.Pause()` / `IAudioPlayer.Resume()` 执行（见 Rule 12 接口契约）。

9. **BPM=0 兜底 / 音频不可用降级**：
   - **BPM=0 兜底**：关卡配置 `bpm=0` 时，F2 回退到固定 120 BPM 的内部 DSP 时钟正常运行；HUD 节奏指示器继续工作；记录 Warning 日志。
   - **音频设备不可用降级**：`IAudioPlayer.PlayScheduled()`（生产实现 `FMODAudioPlayerImpl`，见 ADR-D6）失败或音频设备断开时，F2 执行以下降级序列：(1) F2 自身在 1 帧内停止拍点广播并转入 `Paused`（等同于内部 `Pause()` 信号，同帧广播 `OnBeatClockPaused`）——**但 F2 无权暂停 TDE 或禁用 F3 输入**，实际的「1 帧内暂停游戏」由 **F6（`TDELifecycleAdapter` / 降级协调器）订阅本序列步骤 (2) 的 `OnAudioUnavailable` 事件后、同帧执行 `TDE.Pause()` + `F3.DisableAllInput()`** 承接（SC-1）；F2 只负责发事件，暂停执行归 F6；(2) 发出 `OnAudioUnavailable` 事件，UI 层订阅后显示 "Audio Unavailable" Toast、F6 订阅后承接实际暂停（见 SC-1）；(3) 同步发出 `OnVisualMetronomeActivated` 事件，P1（HUD）订阅后启动视觉节拍指示器（屏幕边缘随 `Time.unscaledTime` 闪烁；音频不可用时 FMOD DSP 时钟（经 `IDSPClock.GetCurrentTime()` 读取，生产实现 `FMODDSPClockImpl`）可能冻结，须以墙上时钟 `Time.unscaledTime` 驱动视觉动画）；(4) DSP 时钟在 `Paused` 状态下仍可查询，节奏判定（C1）暂停期间不触发；(5) 设备重连 / 音频恢复时，F2 在**同一帧**广播 **`OnAudioRestored`** 事件（SC-1 恢复对称信号，F6 `TDELifecycleAdapter` 订阅后对称执行 `GameManager.UnPause()` + `InputEventBus.EnableGameplayInput()`，节拍先于输入；见 `tde-integration.md` Rule 14），并从暂停位置调用 `Resume()` 续播（复用 BC-6 的 `OnBeatClockResumed` 首拍重锚信号）。**理由**：「节奏即战斗」支柱要求节奏判定仅在玩家可感知拍点时生效；无音频时不可隐式惩罚玩家（游戏暂停，判定亦暂停）。

10. **DontDestroyOnLoad 单例**: BeatClock 使用 `DontDestroyOnLoad` 模式跨场景存活。房间切换时 F6 调用 `Stop()` 后再 `Play(newClip, newBpm, newTimeSig)` 重置。`Stop()` 将 `global_beat_index` 归零、清除所有 Pending 状态、释放 BGM AudioSource 引用（由 F6 在下一次 `Play()` 前重新注入）。**`LatencyOffsetMs` 不受 `Stop()` 影响**——延迟校准数据为设备级属性，跨房间、跨关卡保持有效。其写者遵循两阶段模型（ADR-D10 D-5）：**启动期**由 F6 gate 一次性 boot-inject，受 `_offsetInjected` 一次性 guard 保护——重入的 `OnSceneLoaded(Single)`（复活/换关）不重复注入、不覆盖运行期 FE3 写入；**运行期唯一写者为 FE3 校准流程**，Rule 10 的单一写者不变量在运行期完整保留。BeatClock 自身**禁止**调用 `SaveService.LoadLatencyCalibration()` 或以任何方式自 load（registry forbidden pattern `f2_self_load_latency_from_f5`）。下游系统不应假定 `global_beat_index` 或 `measure_index` 在跨房间场景中连续。

11. **订阅者生命周期合约**: 所有订阅 F2 事件的系统**必须**在自身销毁时（`OnDestroy()` 或等价清理路径）解除订阅。F6 在触发房间卸载前**须先**广播房间卸载预信号，确保非持久化系统在 `BeatClock.Stop()` 之前完成取消订阅。F2 的事件类型为 C# `event`（非 UnityEvent），`BeatEventData` 为 `readonly struct`，避免装箱和 GC 分配。

12. **时间戳精度承诺（非回调触发时机）**: F2 承诺的精度指标是——广播给上层的 DSP 时间戳**值**（`adjusted_beat_time`）与理论计算值误差 < 0.1ms。F2 **不**承诺 C# 事件回调在该时间戳对应的精确时刻触发——回调的实际触发时机受调度机制影响（Update 轮询 / OnAudioFilterRead / FMOD DSP callback），由 ADR-D6 决定。**下游系统接口合约**：订阅 F2 事件的系统**必须**使用事件负载中的 `BeatEventData.beat_dsp_time` 进行时序判定；**禁止**在回调触发时读取 `AudioSettings.dspTime` 作为参考时刻——后者是调度误差点，前者是 F2 精度承诺的实际体现（误差 < 0.1ms）。此合约须在 C1、C4、FE3 各 GDD 的 F2 依赖章节中明确引用。BeatClock 须支持通过 `IDSPClock` 接口注入时钟源，允许测试使用 `MockDSPClock` 进行确定性验证。BeatClock 须同等支持通过 `IAudioPlayer` 接口注入音频播放器（与 `IDSPClock` 对称），允许测试注入 `MockAudioPlayer` 以模拟 `PlayScheduled()` 失败场景（AC-F2-16b 的可测性前提）；`IAudioPlayer` 最低需暴露 `PlayScheduled(double dspTime)`、`Stop()`、`IsPlaying`、以及 **`Pause()` / `Resume()`（BC-6）** 五个成员（`Stop()` 映射到**立即停止**，FMOD 对应 `FMOD_STUDIO_STOP_IMMEDIATE`；渐出 / 淡出逻辑属于 F6 BGM 过渡策略，不属于 `IAudioPlayer` 合约）。**`Pause()` / `Resume()`（BC-6）**：`Pause()` 暂停底层音频播放但**不释放**播放位置（FMOD 对应 `setPaused(true)`，DSP 时钟在暂停态仍可查询）；`Resume()` 从暂停位置续播（`setPaused(false)`）。BeatClock 的 `Pause()`/`Resume()` 方法内部委托这两个成员执行音频层暂停/恢复，再广播 `OnBeatClockPaused`/`OnBeatClockResumed` 事件（见 Rule 8）。测试可通过 `MockAudioPlayer` 注入以验证暂停/恢复调用序列（AC-F2-28 的可测性前提）。BeatClock 须同时暴露以下两个公开接口，供测试代码获取确认后的 DSP 锚点（见 AC-F2-19b）：
- `double GetConfirmedDSPStart()`：返回最近一次 `ConfirmDSPStart()` 写入的 `dsp_start_time` 值；**`Play()` 后、首次 `ConfirmDSPStart()` 调用前返回 `-1.0` 作为零状态标志**（`AudioSettings.dspTime` 始终 ≥ 0，`-1.0` 唯一标识"尚未确认"状态，与 `double.MaxValue` 的 `nextBeatDspTime` 零状态对称设计）。
- `bool IsStartConfirmed`：`ConfirmDSPStart()` 至少调用过一次后为 `true`；消费方在使用 `GetConfirmedDSPStart()` 前**须**先检查此属性，`false` 时不得以返回值构造时序计算（避免以 `-1.0` 作为 DSP 锚点）。

   > **线程模型（ADR-D6 Addendum，2026-08-08，normative — 解决 OQ-6）**：BeatClock 调度已确定为 **FMODUnity RuntimeManager + 主线程 `Update()` 轮询** FMOD DSP 时钟；ADR-D6 明确**否决** `OnAudioFilterRead`。`BeatClock.Update()`（主线程）经 `IDSPClock.GetCurrentTime()` 读取 DSP 时钟后**直接**分发 `DispatchBeatEvent()`——无队列、无跨线程 marshalling；**`ConcurrentQueue<BeatEventData>` 不需要、且禁止引入**（徒增约 1 帧延迟、零收益）。所有 C# 拍点事件分发，以及 `LatencyOffsetMs` 的全部写者（F6 boot-inject 与 FE3 运行期校准，ADR-D10 D-5），均在主线程执行，无需任何同步原语。**下游合约（持久有效，与调度机制无关）**：订阅 F2 事件的系统**必须**使用负载中的 `BeatEventData.beat_dsp_time` 进行时序判定；**禁止**在回调触发时读取 `AudioSettings.dspTime`。PlayMode 线程安全测试仍保留并必需（Validation：1000 次快速 `BeatClock.Update()` → 零 `UnityException`），作为主线程不变量的守卫（非跨线程队列覆盖）。

13. **DSP 时间坐标系强制标注（raw/adjusted 命名规则）**: 所有跨模块传递的 DSP 时间戳字段与事件 payload **必须**在命名上显式标注坐标系后缀——`...Raw`（未应用延迟偏移，用于音频硬件调度调用，如 `IAudioPlayer.PlayScheduled()`、`ConfirmDSPStart()`、BGM 换片）或 `...Adjusted`（已应用 F2-F5 偏移，用于玩家判定/呈现消费，如 C1 判定窗口、P1/PL2 节奏呈现）。**禁止**使用裸 `dspTime` 字段名。理由：raw 与 adjusted 混用是本项目递归出现的缺陷类（F2-F3 暂停恢复公式、F2-F7 HUD 公式已两次修订，见 B2 修订警告），最大引入 ±200ms 坐标系误差。**契约铁律**：音频调度调用一律使用 raw（物理输出延迟在硬件层已自然发生，叠加 offset 将双重应用，产生随玩家校准值漂移的可听接缝）；面向玩家的时间对齐一律使用 adjusted。

14. **BPM/拍号变更事件（双阶段：Accepted → Changed）**: F2 暴露两个变更事件，服务两类不同时序需求。均为 C# `event`（签名 `event Action<MeterChangeEventData>`，非 `EventHandler<T>`，避免值类型 payload 被迫继承 `EventArgs` 而装箱）；payload `MeterChangeEventData` 为 `readonly struct`（字段见下方结构表），全字段纯值类型，零 GC。
    - **`OnMeterChangeAccepted`（接受时广播）**：在 `RequestBPMChange()` / `RequestTimeSignatureChange()` 被接受、状态转入 `PendingBPMChange` / `PendingTimeSignatureChange` 的**同帧**广播。供 F6 立即预加载/预调度目标 BGM——由此获得从接受到边界的**全部提前量**，满足 BGM 播放规范的 500ms 最低提前量约束（若在边界才通知，BPM≥120 时 `beat_interval_s ≤ 0.5s`，提前量数学上不可能满足）；供 C2 立即 `UpdateBpm()` 实现「骤跳瞬间即变凌厉」的体感。payload 携带 `predictedEffectiveDspTimeRaw`——边界生效时刻，接受时即可由 F2-F3 确定性计算。
    - **`OnMeterChanged`（生效时广播）**：在 `PendingBPMChange` / `PendingTimeSignatureChange → Playing` 于小节边界生效的**同帧**广播，顺序上位于**边界拍 `OnBeatExact` 之后、新段第 0 拍调度之前**。供 C1 重锚定量化网格、C4 调整令牌密度、HUD/状态刷新。
    - **合并语义**：同帧 BPM + 拍号合并生效时（Edge Case + AC-F2-15），`OnMeterChangeAccepted` 与 `OnMeterChanged` **各恰好触发 1 次**（不因合并被吞或双发），payload 的 `bpmChanged` / `timeSigChanged` 布尔标志指示本次变更涵盖的维度。此设计同时覆盖 Boss 序列 3/4→5/8 这类**仅拍号变化、BPM 不变**的场景——单纯的 `OnBpmChanged` 会漏掉此事件（F6 BGM 换片、C4 令牌密度将收不到通知）。
    - **状态一致性**：内部状态字段（bpm/numerator/dsp_start 等）**必须**在事件广播**之前**完成翻转，保证事件回调内同步查询 `BeatStateSnapshot` 读到新值。
    - **线程安全与分发顺序**：调度为主线程 `Update()` 轮询（ADR-D6 Addendum，无异步回调、无队列）。本双事件与 `OnBeatExact` 在**同一** `Update()` dispatch pass 内确定性分发以保证跨事件相对顺序（同一 pass 内先分发 Exact 事件、再分发 Meter 事件），并由 AC-F2-25 显式断言此顺序。
    - **消费方反向引用（跟进项）**：F6 GDD § BGM 过渡策略、C1 GDD F2 依赖章节、C4 GDD 须反向引用本双事件——本轮仅在 F2 侧声明契约，F6/C1/C4 文档的反向引用为后续跟进。

---

### States and Transitions

| 状态 | 描述 |
|------|------|
| `Stopped` | 初始状态；关卡卸载后；无节拍广播 |
| `Playing` | BGM 播放中；持续广播 Lookahead + Exact 事件 |
| `Paused` | 游戏暂停；时钟冻结；不广播新事件 |
| `PendingBPMChange` | 已收到 BPM 切换请求；以当前 BPM 继续运行直到小节边界 |
| `PendingTimeSignatureChange` | 已收到拍号切换请求；以当前拍号继续运行直到小节边界 |

| 来源状态 | 触发条件 | 目标状态 |
|---------|---------|---------|
| `Stopped` | `Play(clip, bpm, timeSig)` 调用 | `Playing` |
| `Playing` | 收到 TDE 暂停信号（F6 调用 `Pause()`；状态翻转后广播 `OnBeatClockPaused`，BC-6） | `Paused` |
| `Paused` | 收到 TDE 恢复信号（F6 调用 `Resume()`；重锚定后、首拍 Exact 前广播 `OnBeatClockResumed`，BC-6） | `Playing` |
| `Playing` | `RequestBPMChange(newBpm)`（广播 `OnMeterChangeAccepted`） | `PendingBPMChange` |
| `PendingBPMChange` | 当前小节最后一拍 Exact 事件广播后（广播 `OnMeterChanged`） | `Playing`（新 BPM） |
| `Playing` | `RequestTimeSignatureChange(num, den)`（广播 `OnMeterChangeAccepted`） | `PendingTimeSignatureChange` |
| `PendingTimeSignatureChange` | 当前小节最后一拍 Exact 事件广播后（广播 `OnMeterChanged`） | `Playing`（新拍号） |
| `Playing` / `Paused` | `Stop()` 或关卡卸载 | `Stopped` |

---

### Interactions with Other Systems

| 方向 | 系统 | 数据内容 | 接口类型 |
|------|------|---------|---------|
| F2 **→** C1 | 节奏量化攻击系统 | `OnHalfBeatLookahead`（DSP 时间戳）+ `BeatStateSnapshot` | 事件订阅 + 同步查询 |
| F2 **→** C4 | 战斗导演 | `OnBeatLookahead`（时间戳 + 小节号 + 拍内序号） | 事件订阅 |
| F2 **→** F6 | TDE 集成层 | `OnMeterChangeAccepted`（接受时；`MeterChangeEventData`，含 `predictedEffectiveDspTimeRaw`，供 BGM 预加载/预调度） | 事件订阅 |
| F2 **→** C1 / C4 | 量化 / 战斗导演 | `OnMeterChanged`（生效时；网格重锚定 / 令牌密度调整） | 事件订阅 |
| F2 **→** C1 / C3 | 量化 / 心法 | `OnBeatClockPaused` / `OnBeatClockResumed`（BC-6；恢复携首拍 raw DSP，供 C1 重锚拍网格 + 清空过期 `_pendingInput`、C3 心法冻结栈恢复） | 事件订阅 |
| F2 **→** F6 (SC-1) | TDE 集成层 | `OnAudioUnavailable`（设备断开降级信号，F6 同帧 `GameManager.Pause()`+`DisableAllInput()` 承接）；`OnAudioRestored`（恢复对称信号，F6 同帧 UnPause+EnableGameplayInput） | 事件订阅 |
| FE3 **→** F2 | 延迟校准系统 | `LatencyOffsetMs`（float，毫秒） | 属性注入（可随时重写；FE3 每次校准完成后更新） |
| F2 **→** FE3 | 延迟校准系统 | `GetCurrentDSPTime()`（double，秒） | 同步查询接口 |
| F2 **→** P1 | HUD | `OnBeatExact` + `BeatProgress`（0–1，帧查询） | 事件订阅 + 帧查询 |
| F2 **→** PL2 | VFX 节奏反馈 | `OnBeatLookahead` / `OnHalfBeatLookahead` | 事件订阅 |
| F6 **→** F2 | TDE 集成层 | 暂停/恢复信号；BGM FMOD asset 引用及 DSP 起始时间；实际播放确认时间 | 方法调用（`Play` / `Pause` / `Resume` / `Stop` / `ConfirmDSPStart`） |

---

### BeatStateSnapshot 快照结构

供上层系统通过同步查询接口获取的只读数据结构（`readonly struct`）。

| 字段 | 类型 | 范围 | 零状态值 | 描述 |
|------|------|------|---------|------|
| `bpm` | float | [0, 240] | 0 | 当前**生效** BPM（非 Pending 中的新 BPM） |
| `numerator` | int | {0, 3, 4, 5} | 0 | 当前拍号分子；零状态 = 0（时钟未启动） |
| `beatProgress` | float | [0.0, 1.0] | 0.0 | 来自 F2-F7；当前拍内进度 |
| `beatIndexInMeasure` | int | [0, numerator−1] | 0 | 来自 F2-F8；0 = 强拍 |
| `measureIndex` | int | [0, +∞) | 0 | 来自 F2-F8；从最近拍号切换起的累计小节数 |
| `globalBeatIndex` | int | [0, +∞) | 0 | 来自 F2-F3；全局拍序号，`Stop()` 时归零 |
| `nextBeatDspTime` | double | [0, +∞) | double.MaxValue | 下一全拍的 `adjusted_beat_time`；零状态 = `double.MaxValue`（表示「无下一拍」） |
| `nextHalfBeatDspTime` | double | [0, +∞) | double.MaxValue | 下一半拍的 `adjusted_beat_time`；零状态 = `double.MaxValue` |
| `clockState` | BeatClockState | enum | Stopped | { Stopped, Playing, Paused, PendingBPMChange, PendingTimeSignatureChange } |

> **AC-F2-12 零状态**：`Stopped` 时所有字段返回零状态值（见上表第 4 列）。消费者可通过 `clockState == Stopped` 判断时钟未运行，无需额外 API 调用。

> **nextBeatDspTime / nextHalfBeatDspTime 推导公式（B3 修订 — 第 5 次评审）**
> ```
> nextBeatDspTime     = dsp_start_time + (n+1) × beat_interval_s + (latency_offset_ms / 1000.0)
> nextHalfBeatDspTime = dsp_start_time + (m+1) × half_beat_interval_s + (latency_offset_ms / 1000.0)
> ```
> 其中 `n` = 当前已广播的最近一拍 `segment_beat_index`，`m` = 当前已广播的最近一半拍 `segment_half_beat_index`。两者均经 F2-F5 偏移调整（即等价于 `F2-F5(F2-F3(n+1))`），与广播给上层的 `adjusted_beat_time` 坐标系一致。
> **`PendingBPMChange` 状态下**：`dsp_start_time`、`beat_interval_s` 均使用**当前（旧）BPM 段**的值，直到小节边界生效前，`nextBeatDspTime` 仍以旧 BPM 计算。

---

### MeterChangeEventData 事件结构

`OnMeterChangeAccepted` 与 `OnMeterChanged`（见 Core Rule 14）共用的 payload（`readonly struct`，全字段纯值类型，零 GC，坐标系遵循 Core Rule 13）。

| 字段 | 类型 | 描述 |
|------|------|------|
| `bpmChanged` | bool | 本次变更是否涉及 BPM（拍号-only 变更时为 `false`） |
| `timeSigChanged` | bool | 本次变更是否涉及拍号（BPM-only 变更时为 `false`） |
| `oldBpm` | float | 变更前生效 BPM，∈ [80, 240] |
| `newBpm` | float | 变更后 BPM（已 clamp，∈ [80, 240]）；拍号-only 变更时 == `oldBpm` |
| `oldNumerator` | int | 变更前拍号分子，∈ {3, 4, 5} |
| `newNumerator` | int | 变更后拍号分子；BPM-only 变更时 == `oldNumerator` |
| `predictedEffectiveDspTimeRaw` | double | 边界生效时刻的**原始** DSP 时间戳（**raw，未应用 latency offset**——遵循 Core Rule 13；接受时由 F2-F3 确定性计算，生效时与实际边界一致）。F6 以此值直接调度 `IAudioPlayer.PlayScheduled()`，禁止叠加 offset |
| `newBeatIntervalS` | double | 变更后的 `beat_interval_s` = `60.0 / newBpm`（拍号-only 变更时 == 旧值） |

> **`OnMeterChangeAccepted` vs `OnMeterChanged` 的字段差异**：两事件 payload 结构相同；`Accepted` 在接受时广播（`predictedEffectiveDspTimeRaw` 为**预测**的边界时刻），`Changed` 在边界生效时广播（同一字段为**已实现**的边界时刻，二者数值一致——F2-F3 边界时刻是确定性的，不存在预测偏差）。

---

### BeatClockPauseEventData 事件结构（BC-6）

`OnBeatClockPaused` 与 `OnBeatClockResumed`（见 Core Rule 8）共用的 payload（`readonly struct`，全字段纯值类型，零 GC，坐标系遵循 Core Rule 13）。

| 字段 | 类型 | 描述 |
|------|------|------|
| `isResume` | bool | `false` = 暂停事件（`OnBeatClockPaused`）；`true` = 恢复事件（`OnBeatClockResumed`）。用于订阅方在单一处理器内区分方向 |
| `resumeFirstBeatDspTimeRaw` | double | **仅恢复事件有效**：恢复后首拍的 **raw** DSP 时间戳（未应用 latency offset，遵循 Core Rule 13）。C1 以此重锚拍网格；暂停事件时为 `-1.0` 零状态哨兵 |
| `beatIndexInMeasure` | int | 事件时刻的小节内拍序号（暂停 = 冻结位置；恢复 = 续接位置，不重置为 0） |
| `measureIndex` | int | 事件时刻的累计小节号 |
| `fractionalProgressAtPause` | double | **仅暂停事件有效**：暂停时刻拍内已过去时间（秒，= F2-F3 暂停恢复公式的 `fractional_progress_at_pause`）；恢复事件时为 `0.0` |

> **成对不变量**：单次「暂停→恢复」周期内，`OnBeatClockPaused`（`isResume=false`）先行，其配对 `OnBeatClockResumed`（`isResume=true`）随后，各恰好 1 次（AC-F2-28）。`resumeFirstBeatDspTimeRaw` 为 raw 坐标——消费方若需玩家感知域时间，须自行叠加 `latency_offset_ms/1000`（对齐 F2-F5），F2 不在此 payload 预应用 offset。

---

## Formulas

> 本节定义 F2 所有内部计算的精确规格。所有时间单位除非特别说明均为**秒（s）**，DSP 时间戳类型为 `double`（Unity 要求，避免 float 精度丢失）。毫秒（ms）仅用于 UI 可读参数的配置输入，内部统一换算为秒。

---

### F2-F1：节拍间隔公式（beat_interval_s）

一拍对应的 DSP 时间长度，是所有其他公式的基础量。

```
beat_interval_s = 60.0 / bpm
```

| 变量 | 类型 | 范围 | 描述 |
|------|------|------|------|
| `bpm` | float | [80, 240]；BPM=0 时回退 120 | 当前每分钟拍数；由关卡配置注入 |
| `beat_interval_s` | double | [0.25, 0.75] | 一拍对应的 DSP 时间长度（秒） |

**输出范围**：[0.25 s, 0.75 s]。BPM=0 兜底规则在配置加载阶段将其替换为 120。

**示例**：BPM=120 → 0.500 s；BPM=180 → 0.333 s

---

### F2-F2：半拍间隔公式（half_beat_interval_s）

攻击量化网格的最小单元。C1 将输入吸附到最近的半拍节点，因此半拍间隔是判定窗口时长的上界参照。

```
half_beat_interval_s = beat_interval_s / 2.0 = 30.0 / bpm
```

| 变量 | 类型 | 范围 | 描述 |
|------|------|------|------|
| `beat_interval_s` | double | [0.25, 0.75] | 来自 F2-F1 的全拍间隔 |
| `half_beat_interval_s` | double | [0.125, 0.375] | 半拍对应的 DSP 时间长度（秒） |

**输出范围**：[0.125 s, 0.375 s]，即 125 ms 到 375 ms。

> **C1 下游约束**：判定窗口（Perfect + Good 各方向）之和 ≤ `half_beat_interval_s`，避免相邻网格节点的判定区域交叠。C1 GDD 须在设定判定窗口时引用此公式的输出。

**示例**：BPM=120 → 0.250 s；BPM=180 → 0.167 s

---

### F2-F3：第 N 拍 DSP 时间戳（nth_beat_dsp_time）

BeatClock 内部调度器用此公式推算每一拍的精确 DSP 到达时刻。

```
nth_beat_dsp_time = dsp_start_time + (n × beat_interval_s)
```

| 变量 | 类型 | 范围 | 描述 |
|------|------|------|------|
| `dsp_start_time` | double | [0, +∞) | BGM 开始播放时刻的 DSP 时间戳；在 `BeatClock.Play()` 时一次性锁定 |
| `n` | int | [0, +∞) | 当前 BPM 段内的局部拍序号（`segment_beat_index`）；BPM 切换时归零，暂停时冻结 |
| `beat_interval_s` | double | [0.25, 0.75] | 来自 F2-F1 |
| `nth_beat_dsp_time` | double | [0, +∞) | 第 n 拍的精确 DSP 到达时刻（秒） |

**BPM 切换**：旧 BPM 运行 `n_old` 拍后切换，新基准：`dsp_start_new = dsp_start_old + (n_old × beat_interval_old)`，`n`（`segment_beat_index`）从 0 重新计算。注意：`global_beat_index`（F2-F8）在 BPM 切换时**不重置**，继续单调递增。

> **F2-F3 / F2-F4 锚点等价性**：BPM 切换生效时，F2 保证调度器处于全拍边界（Core Rule 6）——此时 F2-F4 的 `m_old`（段内半拍序号）必然为偶数（全拍节点满足 m = 2n）。因此 F2-F3 的 `dsp_start_new = dsp_start_old + n_old × beat_interval_old` 与 F2-F4 的 `dsp_start_new = dsp_start_old + m_old × half_beat_interval_old` 在此条件下代数等价，实现时无需额外同步两个锚点。

**暂停恢复**：暂停时记录 `n_paused`（整拍数）和 `fractional_progress_at_pause`（当前拍内已过去时间，单位秒）；恢复时重新锚定：
```
dsp_start = AudioSettings.dspTime − (n_paused × beat_interval_s) − fractional_progress_at_pause
其中 fractional_progress_at_pause = dspTime_at_pause − last_raw_beat_dsp_time
     last_raw_beat_dsp_time = dsp_start_time + n_paused × beat_interval_s   （F2-F3，F2-F5 应用偏移前）
```
此公式保证恢复后下一拍到来的间隔 = 暂停前当前拍的剩余时间（`beat_interval_s − fractional_progress_at_pause`），而非下一整拍。

> **⚠ 时间坐标约束（B2 修订 — 第 5 次评审）**：`fractional_progress_at_pause` **必须**使用原始 DSP 时间（F2-F3 的 `nth_beat_dsp_time`，F2-F5 偏移前）。`dspTime_at_pause`（`AudioSettings.dspTime`）是原始 DSP 时间；`last_raw_beat_dsp_time = dsp_start_time + n × beat_interval_s`（F2-F3，未应用 latency offset）。**禁止**使用 F2-F7 的 `last_beat_time`（`adjusted_beat_time`，已含偏移）——两者差值引入误差 = `latency_offset_ms / 1000`，最大 ±200ms，超出 AC-F2-17 的 ±16.7ms 门控。BeatClock 实现须同时维护 `last_raw_beat_dsp_time`（供暂停恢复公式使用）和 `last_adjusted_beat_dsp_time`（供 F2-F7 HUD 公式使用），两者独立存储。

**示例**：BPM=120，`dsp_start=10.000`，n=3 → `10.000 + 1.500 = 11.500 s`

> **⚠ 实现规范（禁止累加模式）**：调度器**必须**使用锚定形式 `dsp_start_time + n × beat_interval_s` 计算每拍时刻，**禁止**使用迭代累加形式（`accumulator += beat_interval_s` 循环叠加）。迭代累加在 1000 拍后将产生约 0.5ms 累积误差（double 精度下 `beat_interval_s` 的舍入误差逐拍积累），违反 0.1ms 精度承诺。

> **⚠ BGM 播放规范（F6 接口合约）**：`dsp_start_time` 须经过 F6 的**乐观预锚定 + 实际起始校正**两步协议写入——不得仅以 `targetDSPTime` 单次写入作为最终锚点（移动端缓冲边界误差可达 93ms，超出 FE3 校准范围）。F6 须遵循以下三步协议：
>
> 1. **最低提前量**：`PlayScheduled(targetDSPTime)` 调用时须保证 `targetDSPTime ≥ AudioSettings.dspTime + 0.5`（500ms 提前量），确保所有平台缓冲配置下均能成功调度。
> 2. **乐观预锚定**：F6 在 `PlayScheduled(targetDSPTime)` 调用后**立即**调用 `BeatClock.ConfirmDSPStart(targetDSPTime)` 作为初始锚点。此步骤在 `isPlaying` 为 false 时即完成，确保第一拍 Exact 事件以有效 `dsp_start_time` 广播，消除时序竞争（不依赖 `isPlaying` 检测，不存在「第一拍已广播而锚点未确认」的竞争窗口）。
> 3. **实际起始校正（生产路径：FMOD）**：**ADR-D6 已选定 FMOD Studio 2.x**（见 `docs/architecture/adr-d6-audio-middleware.md`），F6 须使用 FMOD `FMODAudioPlayerImpl` 的 `scheduledStartDSPClock / fmodSampleRate` 计算 `actual_start`，获得样本级精度（Android ~0.7ms，vs Unity Native 23-46ms）。`actual_start` 就绪后，若 `|actual_start - targetDSPTime| > 5ms`，F6 调用 `BeatClock.ConfirmDSPStart(actual_start)` 进行第二次重锚定，BeatClock 同步记录 Warning 并更新所有**后续**拍点时间戳（第一拍已广播，不受影响）。偏差 ≤ 5ms 时无需第二次调用。
>
> **fallback 参考（Unity Native，ADR-D6 未选定路径，仅供降级参考）**：若 FMOD 不可用，F6 在 `AudioSource.isPlaying == true` 首帧检查 `AudioSource.timeSamples`；若 `timeSamples == 0`（缓冲区未推进），**本帧跳过步骤 3，下一帧重试**（最多 3 帧；若仍为 0，保持乐观预锚定值并记录 Warning）；`timeSamples > 0` 后，`actual_start = AudioSettings.dspTime - (AudioSource.timeSamples / (double)AudioSettings.outputSampleRate)`（注意：`AudioSettings.outputSampleRate` 在 headless 配置下可能为 0，须防护除零），同样检查 5ms 偏差阈值后决定是否重锚定。
>
> **禁止**使用 `AudioSource.Play()` 后立即读取的 `AudioSettings.dspTime`（缓冲边界延迟 10–93ms，FE3 校准无法消除此系统性偏移）。F6 GDD 接口合约须将上述乐观预锚定 + 实际起始校正协议列为强制约束。

---

### F2-F4：第 M 半拍 DSP 时间戳（nth_half_beat_dsp_time）

```
nth_half_beat_dsp_time = dsp_start_time + (m × half_beat_interval_s)
```

| 变量 | 类型 | 范围 | 描述 |
|------|------|------|------|
| `m` | int | [0, +∞) | **当前 BPM 段内的局部半拍序号**（`segment_half_beat_index`，类比 F2-F3 中的 `n`）；BPM 切换时归零并重新锚定（见下方 BPM 切换说明）；m 为偶数 = 全拍节点，m 为奇数 = 半拍节点 |
| `half_beat_interval_s` | double | [0.125, 0.375] | 来自 F2-F2 |
| `nth_half_beat_dsp_time` | double | [0, +∞) | 第 m 个半拍节点的精确 DSP 时刻 |

`OnBeatExact` 仅在 m 为偶数时触发；`OnHalfBeatExact` 仅在 m 为奇数时触发。两者共用同一调度序列。

**BPM 切换**：旧 BPM 运行 `m_old` 个半拍后切换，新基准：`dsp_start_new = dsp_start_old + (m_old × half_beat_interval_old)`，`m` 从 0 重新计算——与 F2-F3 的 `segment_beat_index (n)` 行为完全对称。若需追踪跨 BPM 段的全局半拍总数，可另设 `global_half_beat_index`（永不重置，类比 F2-F8 的 `global_beat_index`），但 F2-F4 公式中的 `m` 始终为局部段序号。

---

### F2-F5：玩家延迟调整后时间戳（adjusted_beat_time）

F2 广播给所有上层系统的时间戳均已应用此偏移。上层系统无需自行处理延迟补偿。

```
adjusted_beat_time = raw_beat_dsp_time + (latency_offset_ms / 1000.0)
```

| 变量 | 类型 | 范围 | 描述 |
|------|------|------|------|
| `raw_beat_dsp_time` | double | [0, +∞) | 来自 F2-F3 或 F2-F4 的未调整 DSP 时刻 |
| `latency_offset_ms` | float | [-200, 200] | FE3 注入的玩家延迟偏移（毫秒）；**正值 = 声音延迟到达玩家耳朵（无线耳机场景）**——时间戳后移以补偿，使 C1 判定窗口延后匹配玩家实际听到拍点的时刻；负值 = 声音提前到达（低延迟监听/特定硬件）；默认 0 |
| `adjusted_beat_time` | double | [0, +∞) | 广播给上层的校准后时间戳 |

**符号约定**：正值 latency_offset（声音延迟到达玩家耳朵，如无线耳机）→ `adjusted = raw + offset/1000`，时间戳向后移动，C1 判定窗口相应延后——玩家在实际听到拍点时按键可被判为 Perfect（按键时刻 ≈ raw + offset，调整后的参考点 = raw + offset，误差归零）。FE3 的 GDD 须以"玩家听到音频有延迟 = latency_offset 为正值"统一约定，否则补偿方向相反。

**示例**：`raw=11.500 s`，`latency_offset_ms=80` → `11.500 + 0.080 = 11.580 s`

---

### F2-F6：Lookahead 广播触发时间（lookahead_trigger_time）

```
lookahead_trigger_time = adjusted_beat_time − (lookahead_ms / 1000.0)
```

| 变量 | 类型 | 范围 | 描述 |
|------|------|------|------|
| `adjusted_beat_time` | double | [0, +∞) | 来自 F2-F5 的校准后目标拍点时刻 |
| `lookahead_ms` | float | [50, 200] | 提前广播时间量（毫秒）；默认 100 ms |
| `lookahead_trigger_time` | double | [0, +∞) | Lookahead 事件的实际触发 DSP 时刻 |

**上限约束**：`lookahead_ms < half_beat_interval_s × 1000`（BPM=240 时上限 125 ms）。超出时 clamp 至 `half_beat_interval_s × 1000 × 0.8`（秒转毫秒后取 80% 余量，即 BPM=240 时 clamp 至 100ms），防止相邻节点 Lookahead 事件重叠。

**示例**：`adjusted=11.420 s`，`lookahead_ms=100` → 触发时间 11.320 s（携带未来时间戳 11.420 s）

---

### F2-F7：节拍进度公式（beat_progress）

供 P1 HUD 帧查询使用，驱动节奏指示器脉冲动画。

```
adjusted_dsp_now = IDSPClock.GetCurrentTime() + latency_offset_ms / 1000.0
beat_progress = clamp((adjusted_dsp_now − last_beat_time) / beat_interval_s, 0.0, 1.0)
```

| 变量 | 类型 | 范围 | 描述 |
|------|------|------|------|
| `adjusted_dsp_now` | double | [0, +∞) | 当前帧的**延迟调整后** DSP 时间；= `IDSPClock.GetCurrentTime() + latency_offset_ms / 1000.0`（生产实现通过注入的 `FMODDSPClockImpl` 读取 FMOD DSP 时钟，见 ADR-D6）。**必须使用调整后值**：`last_beat_time` 为 `adjusted_beat_time`（含偏移），若以原始 DSP 时间直接相减将引入最大 ±200ms 坐标系误差 |
| `last_beat_time` | double | [0, +∞) | 最近一次全拍 Exact 事件的 `adjusted_beat_time`（F2-F5 输出）；每全拍更新。与 `adjusted_dsp_now` 坐标系一致 |
| `beat_interval_s` | double | [0.25, 0.75] | 来自 F2-F1 |
| `beat_progress` | float | [0.0, 1.0] | 0.0 = 刚经过拍点；1.0 = 下一拍点即将到达 |

clamp 的必要性：暂停恢复后 `adjusted_dsp_now` 可能短暂早于 `last_beat_time`，防止负值或超界。

**初始化**：`last_beat_time` 在 `Play()` 调用时初始化为 `dsp_start_time`（来自 F2-F3）。第一拍 `OnBeatExact` 广播前，若 `dsp_now < dsp_start_time`，公式结果为负数并被 clamp 至 0.0（HUD 保持 0 进度直到首拍到来），不会产生「HUD 卡满格」问题。

**BPM 切换时**：`last_beat_time` 须同步更新为新 BPM 段生效时的拍点时间戳（即切换边界时刻的 `adjusted_beat_time`），否则切换后首帧 `dsp_now − last_beat_time` 可能超过新 `beat_interval_s`，导致 `beat_progress` 被 clamp 卡在 1.0 数帧，HUD 脉冲指示器出现卡顿。

**示例**：BPM=120，`latency_offset_ms=0`，`last_beat_time=11.420 s`（adjusted_beat_time），`AudioSettings.dspTime=11.620 s` → `adjusted_dsp_now=11.620 s`，`(11.620 − 11.420) / 0.500 = 0.40`

---

### F2-F8：小节内拍序号公式（beat_index_in_measure）

**守卫条件**：若 `numerator == 0`（时钟处于 `Stopped` 状态），直接返回 `beat_index_in_measure = 0`，`measure_index = 0`，不执行 mod 运算，避免 C# `DivideByZeroException`。

```
time_sig_segment_beat_index：从最近一次拍号切换（或 BGM 启动）起的局部拍计数；拍号切换时归零，BPM 切换时不归零

beat_index_in_measure = time_sig_segment_beat_index mod numerator   （前提：numerator > 0）
measure_index = time_sig_segment_beat_index / numerator              （整除；前提：numerator > 0）
```

| 变量 | 类型 | 范围 | 描述 |
|------|------|------|------|
| `time_sig_segment_beat_index` | int | [0, +∞) | 从最近一次**拍号切换**（或 BGM 启动）至今的局部拍计数；拍号切换时归零；BPM 切换时**不归零**。`Stop()` 时归零。独立于 `global_beat_index`（永不重置）和 F2-F3 的 `segment_beat_index`（BPM 切换时归零） |
| `global_beat_index` | int | [0, +∞) | 从 BGM 开始的全局拍号，BPM 或拍号切换均**不**重置，`Stop()` 时归零。**仅用于 BeatStateSnapshot.globalBeatIndex 字段输出**——不参与任何内部时间戳公式计算（F2-F3 ～ F2-F5 均使用 `segment_beat_index (n)` 而非此值）；**不用于小节位置计算** |
| `numerator` | int | {3, 4, 5} | 拍号分子（每小节拍数）；4/4→4，3/4→3，5/8→5（八分音符为单位） |
| `beat_index_in_measure` | int | [0, numerator−1] | 0 = 强拍；numerator−1 = 末拍 |
| `measure_index` | int | [0, +∞) | 从最近一次拍号切换（或 BGM 启动）起的累计小节数；拍号切换时归零 |

**示例（无拍号切换）**：4/4，`time_sig_segment_beat_index = 9` → `9 mod 4 = 1`（第 3 小节第 2 拍）

**示例（拍号切换后）**：4/4 运行 8 拍后切换至 3/4，切换时 `time_sig_segment_beat_index` 归零；切换后第 5 拍：`4 mod 3 = 1`（第 2 小节第 2 拍）。若仍用 `global_beat_index = 12 mod 3 = 0`，将错误地报告为强拍——此为本变量存在的根本原因。

---

### F2-F9：5/8 拍号特殊处理

> **Scope: Post-MVP（Vertical Slice / Alpha）** — 5/8 拍号仅在 Boss 二阶段切换中使用，MVP 不含此特性。本节为前瞻性基础设施设计，AC-F2-21/22 标记为 ADVISORY，不作为 MVP 验收门控。

> Boss 二阶段切换序列：4/4 → 3/4 → 5/8。5/8 下「拍」的定义与 4/4、3/4 不同。

**核心决策**：在 5/8 拍号下，F2 将**八分音符重定义为计拍单位**。`global_beat_index` 以八分音符为步长递进，`numerator = 5`。BPM 始终定义四分音符速率，切换拍号不改变 BPM。

```
eighth_note_interval_s = beat_interval_s / 2.0   （等同 half_beat_interval_s）
measure_duration_5_8 = 5 × eighth_note_interval_s
```

| 拍号行为 | 4/4 | 3/4 | 5/8 |
|---------|-----|-----|-----|
| beat_interval_s 定义 | 四分音符 | 四分音符 | 四分音符（不变） |
| numerator | 4 | 3 | 5（八分音符为拍） |
| 最小量化网格 | 八分音符 | 八分音符 | 八分音符（同） |
| 小节时长 @BPM=120 | 2.000 s | 1.500 s | 1.250 s |
| 小节时长 @BPM=180 | 1.333 s | 1.000 s | 0.833 s |

**C1 透明性**：C1 始终量化到 `OnHalfBeatExact`（八分音符），5/8 切换对 C1 完全透明。

**节奏分组语义**（2+3 或 3+2）属于 C4（战斗导演）职责，F2 仅提供均匀八分音符网格，不知晓分组模式。若需 BeatAccentMap，在 Tuning Knobs 节中讨论是否作为可选配置加入。

**5/8 小节边界检测**：

定义 `time_sig_segment_half_beat_index`（以下简写 **h**）= 从最近一次拍号切换（或 BGM 启动）至今的局部半拍计数；拍号切换时归零，BPM 切换时**不归零**（区别于 F2-F4 的 `segment_half_beat_index (m)`，后者 BPM 切换时归零）。

```
is_measure_boundary = (h > 0) && (h mod 5 == 0)
```

> **h = 0 为重置哨兵，不作为生效点**：拍号切换或 BGM 启动时 h 被归零，此时 `h mod 5 == 0` 为 true，但 h = 0 表示"刚完成切换/启动"而非"下一个自然边界到达"。Pending 变更须等待 h = 5（第一个自然小节边界）才生效，防止切换请求在 h = 0 时被立即应用（即在同一帧触发）。

BPM 或拍号切换请求在此条件满足时生效。

---

### 公式依赖图

```
F2-F1 (beat_interval_s)
  ├─► F2-F2 (half_beat_interval_s)
  │     └─► F2-F4 (nth_half_beat_dsp_time)
  │           └─► F2-F5 (adjusted_beat_time)
  │                 └─► F2-F6 (lookahead_trigger_time)
  ├─► F2-F3 (nth_beat_dsp_time)
  │     └─► F2-F5
  ├─► F2-F7 (beat_progress)           [HUD 帧查询]
  └─► F2-F9 (5/8 特殊处理)             [measure_duration, boundary]
F2-F8 (beat_index_in_measure)          [依赖 time_sig_segment_beat_index + numerator]
```

---

## Edge Cases

- **如果 BPM 配置为 0 或未配置**：在 `Play()` 调用时检测，立即替换为 120 BPM 并记录 Warning 日志；系统不进入错误状态，以 120 BPM 正常启动。上层系统无感知。

- **如果音频设备不可用（静音/设备断开）**：`AudioSource.PlayScheduled()` 失败时，F2 触发降级序列——F2 自身停拍并转入 `Paused`（1 帧内，同帧广播 `OnBeatClockPaused`），发出 `OnAudioUnavailable` 事件触发 UI 层 Toast 提示，发出 `OnVisualMetronomeActivated` 事件触发 P1 启动视觉节拍指示器。**实际的 TDE 暂停 / F3 禁输入由 F6（降级协调器）订阅 `OnAudioUnavailable` 后同帧承接（`TDE.Pause()+F3.DisableAllInput()`，SC-1）——F2 无权暂停 TDE/禁 F3，只负责发事件**。DSP 时钟在 `Paused` 状态下仍可查询，节奏判定暂停。玩家恢复音频后调用 `Resume()` 可从暂停位置续播。见 Core Rule 9 音频降级规格。

- **如果 BPM 超出安全范围 [80, 240]**：clamp 到最近边界值（< 80 → 80，> 240 → 240），记录 Warning 日志。不抛出异常。

- **如果 `lookahead_ms` 配置超出上限（≥ `half_beat_interval_s × 1000`）**：自动 clamp 至 `half_beat_interval_s × 1000 × 0.8`（秒转毫秒后取 80% 余量），记录 Warning。防止相邻节点 Lookahead 事件在时间轴上重叠。

- **如果首拍或任意拍的 `lookahead_trigger_time` 在调度检测时已为过去时（常见于首拍启动窗口）**：该 Lookahead 事件**立即发出**，携带正确的未来 `beat_dsp_time` 负载（`adjusted_beat_time`，即目标拍点 DSP 时刻），**不跳过**。对应的 Exact 事件仍在其正常时刻触发（若 Exact 时刻亦已过去，立即追发）。单拍单次广播保证（AC-F2-06b）在此路径下仍有效——"立即发出"不计为重复触发。此边界条件在 F6 遵守 500ms 提前量要求（BGM 播放规范第 1 条）后极少出现，但调度器须显式处理以保证鲁棒性。

- **如果在时钟处于 `Stopped` 状态时收到上层查询（`BeatStateSnapshot`）**：返回「零状态快照」（BPM=0，进度=0，小节号=0），不广播事件。C1 和 C4 应在订阅前检查时钟状态。

- **如果在 `Paused` 状态下收到 `RequestBPMChange()` 或 `RequestTimeSignatureChange()`**：切换请求入队（状态进入 `PendingBPMChange`），等到恢复（`Playing`）后继续等待当前小节结束再生效。暂停不重置切换队列。恢复时 DSP 重锚定公式**始终使用暂停前的旧 BPM**（`beat_interval_s_old`），新 BPM 在小节边界才生效。

- **如果同时请求 BPM 切换和拍号切换（同一帧或相邻帧）**：两个请求均入队，在同一个小节边界统一生效（先应用 BPM，同帧再应用拍号），避免触发两次小节边界。记录 Info 日志说明合并处理。

- **如果 `latency_offset_ms` 注入值超出 [-200, 200] 范围**：F2 信任 FE3 的输入（FE3 负责约束）。若异常值导致 `adjusted_beat_time` 为负数，clamp 至 0.0 并记录 Warning（此情况仅在游戏开始后极短时间内可能出现，实践中不影响正常游玩）。

- **如果 BGM AudioSource 意外被外部代码直接停止（非通过 F2 接口）**：F2 检测不到此事件，将继续以 DSP 时钟广播节拍，导致音频与节拍失去同步。**禁止外部代码直接操作 F2 持有的 BGM AudioSource**——所有音频控制必须通过 F2 的 `Play()`/`Stop()`/`Pause()` 接口。此约束由 F6（TDE 集成层）接口合约强制。

- **如果 5/8 小节边界计算出现误差**：所有小节边界判定基于整数 `half_beat_index mod 5` 运算，不存在浮点边界误差。DSP 时间戳用 `double` 保证精度，不需要额外边界保护。

- **如果移动端（iOS/Android）后台切换导致 `AudioSettings.dspTime` 不连续**：iOS 音频会话被系统中断（来电、Siri）或 Android 音频焦点丢失时，`dspTime` 可能跳变或重置。BeatClock 须在 `OnApplicationPause(true)` 事件中同步记录快照 `dsp_at_suspend = AudioSettings.dspTime` 和 `realtime_at_suspend = Time.realtimeSinceStartup`。恢复时（`OnApplicationPause(false)`），以 `expected_dsp = dsp_at_suspend + (Time.realtimeSinceStartup − realtime_at_suspend)` 推算预期 DSP 时间（基于墙上时钟估算，`Time.realtimeSinceStartup` 在后台仍持续递增）。若 `|AudioSettings.dspTime − expected_dsp| > 100ms`，执行**完全重锚定**（等同于 `Stop()→Play()` 重启时钟），而非使用暂停恢复公式续接。记录 Warning 日志。此检测仅在移动端目标平台启用（PC 平台 `dspTime` 连续性可信）。

- **如果 Boss 阶段切换信号在小节中途到达（如第 2 拍 of 4）**：切换请求入队，当前小节剩余拍数正常播放，在最后一拍的 Exact 事件广播后生效。玩家不会感受到节奏在小节中途中断。

- **如果 BPM/拍号变更请求发起时距下一个小节边界不足 500ms**：因 BGM 换片调度须满足 `IAudioPlayer.PlayScheduled()` 的 500ms 最低提前量（见 F2-F3 BGM 播放规范第 1 条），若变更在最近的边界生效将使 F6 无足够提前量完成无缝换片。F2 将该变更**顺延至再下一个小节边界**生效（而非最近的边界）。`OnMeterChangeAccepted` 仍在接受时立即广播，其 `predictedEffectiveDspTimeRaw` 指向**顺延后**的边界时刻；`OnMeterChanged` 在顺延后的边界广播。记录 Info 日志说明顺延。此规则仅影响 BGM 有实际换片需求的变更；纯时钟层 BPM 变更（无 BGM 切换）不受 500ms 约束，仍在最近边界生效。

---

## Dependencies

### 上游依赖（F2 依赖的系统）

F2 是 Foundation 层系统，**编译期无上游依赖**（不 `using` 其他游戏系统的命名空间）。

**运行时依赖**：**无 F5 直接依赖**（ADR-D10 D-5）。BeatClock 不调用 `SaveService.LoadLatencyCalibration()`、不自 load 任何延迟校准数据——它是纯持有者（pure holder）。启动期 `LatencyOffsetMs` 由 F6 `TDELifecycleAdapter` 从 F5 读取后一次性 boot-inject（详见 F6 GDD 启动链路与 F2 Core Rule 7/Rule 10）；`isCalibrated=false`（首次游玩）时注入默认 0ms。此前「F2 在 `Start()` 中自 load」的写法已废止（registry forbidden pattern `f2_self_load_latency_from_f5`）。

---

### 下游依赖（依赖 F2 的系统）

| 下游系统 | 依赖类型 | 从 F2 获取的数据 | 接口 |
|---------|---------|----------------|------|
| **C1** 节奏量化攻击系统 | **硬依赖** | `OnHalfBeatLookahead`（半拍网格时间戳）+ `BeatStateSnapshot` | 事件订阅 + 查询 |
| **C4** 战斗导演 | **硬依赖** | `OnBeatLookahead`（时间戳 + 小节号 + 拍内序号） | 事件订阅 |
| **FE3** 延迟校准系统 | **硬依赖** | `GetCurrentDSPTime()` + 向 F2 注入 `LatencyOffsetMs`（**运行期唯一写者**，ADR-D10 D-5） | 双向：FE3 查询 F2，FE3 向 F2 注入偏移 |
| **FE4** Boss 战系统 | **软依赖** | `RequestBPMChange()` / `RequestTimeSignatureChange()` | 方法调用（通过 C4 间接依赖，但也直接调用切换接口） |
| **P1** HUD | **硬依赖** | `OnBeatExact` + `BeatProgress`（帧查询） | 事件订阅 + 帧查询 |
| **PL2** VFX 节奏反馈 | **软依赖** | `OnBeatLookahead` / `OnHalfBeatLookahead` | 事件订阅（无节拍事件时降级为无节奏动画，不崩溃） |
| **C3** 心法系统 | **软依赖** | `OnBeatClockResumed`（BC-6；心法冻结栈在恢复时解冻/重锚计时） | 事件订阅 |

> **BC-6 暂停/恢复事件下游合约**：`OnBeatClockPaused` / `OnBeatClockResumed` 是 F2/C1/C3/F6 四方共享的唯一暂停/恢复信号源（见 Core Rule 8）。**C1**（硬依赖）订阅 `OnBeatClockResumed` 重锚拍网格并清空过期 `_pendingInput`（避免暂停期残留输入在恢复后被误判）；**C3**（软依赖）心法冻结栈订阅同一事件恢复计时。三系统均**不得**各自定义 `OnGamePaused`/`OnGameResumed` 并行事件——统一消费 F2 的这对事件（消解 BC-6 信号机制不一致）。F6 作为 `Pause()`/`Resume()` 方法调用的发起方，同样以此对事件与下游对齐。C1 / C3 / F6 各 GDD 的 F2 依赖章节须反向引用本对事件。

---

### 与 F6（TDE 集成层）的特殊关系

F6 不是 F2 的正式上游依赖，但 F2 的 `Play(bgm, bpm, timeSig)` 接口需要 F6 提供 BGM FMOD asset 引用和 DSP 起始时间：

- **F6 → F2**：BGM FMOD asset 引用；暂停/恢复信号
- **F2 不调用 F6 接口**（单向）

F2 的实例化必须在 F6 完成 BGM 加载之后。F6 GDD 须在接口合约中明确此启动顺序。

**Boss 阶段中途 BPM / 拍号切换的 BGM 处理合约**

`RequestBPMChange()` / `RequestTimeSignatureChange()` 仅负责时钟层节拍计算切换，F2 **不**处理 BGM 的音频内容过渡。BGM 过渡策略由 F6 GDD 定义，须遵守以下约束：

- **禁止**通过音频播放器 pitch 属性（FMOD `Channel.setPitch()` 等）实现 BPM 切换——变速会使编钟等中国传统乐器失调，违反支柱三（华风沉浸）
- 推荐模型：F6 订阅 F2 的 **`OnMeterChangeAccepted`** 事件（接受时广播，见 Core Rule 14），接到通知后立即预加载目标 BGM，并以 payload 的 `predictedEffectiveDspTimeRaw`（**raw 坐标系，禁止叠加 offset**——见 Core Rule 13）通过 `IAudioPlayer.PlayScheduled()` 在小节边界精确对齐执行无缝换片或 Crossfade。接受时通知保证 F6 获得从接受到边界的全部提前量（满足 500ms 最低提前量约束），无需再自行手写 `beat_index_in_measure == numerator-1` 边界轮询
- F2 不持有、不感知 BGM Clip 的切换——时钟层（F2）与音频内容层（F6）职责严格分离
- **`ScheduleBGMTransition()` 方法契约（B3，F6 调用方）**：F2 暴露 `void ScheduleBGMTransition(FMODAudioAssetRef nextBgm, double boundaryDspTimeRaw)` 作为**时钟层边界对齐调度入口**——F6 在收到 `OnMeterChangeAccepted` 后，以此登记一次「在指定小节边界无缝换 BGM 片」的调度请求。**语义**：F2 以 `boundaryDspTimeRaw`（**raw 坐标系**，取自 payload 的 `predictedEffectiveDspTimeRaw`，遵循 Core Rule 13，**禁止叠加 offset**）通过 `IAudioPlayer.PlayScheduled(boundaryDspTimeRaw)` 在小节边界精确对齐启动 `nextBgm`，实现样本级无缝换片。**职责边界**：`nextBgm` 句柄由 **F6 提供**（F2 不选择、不感知素材内容），Crossfade / 淡入淡出策略仍属 F6（见本节 pitch 禁令）；本方法仅封装「边界对齐调度」这一时钟层能力，消解 F6 侧 `ScheduleBGMTransition` 调用的 F2 API 缺口（W-2c-01）。此方法为 `OnMeterChangeAccepted` 订阅路径的等价便捷封装，二者互斥使用（F6 择一，勿双重调度同一换片）。

---

### 双向一致性注记

本文档是以下 GDD 的设计基准，各文档须反向引用 F2：

- **C1 GDD**：须引用 F2-F2（半拍间隔约束）作为判定窗口上界
- **FE3 GDD**：须与 F2-F5 的 `latency_offset` 符号约定对齐（正值 = 玩家听到音频有延迟，如无线耳机；调整后时间戳 = raw + offset/1000）
- **F5 GDD**：`LoadLatencyCalibration()` 的调用方已由 F2 迁移至 F6 `TDELifecycleAdapter`（ADR-D10 D-5）；F5 Dependencies 节须列 **F6** 为启动期消费方，F2 不再是 F5 的下游消费方
- **F6 GDD**：须在接口合约中说明向 F2 提供 BGM FMOD asset 的责任及启动顺序

---

## Tuning Knobs

| 参数 | 默认值 | 安全范围 | 影响 | 说明 |
|------|--------|---------|------|------|
| `bpm`（每个关卡/房间） | 100（入口房） | [80, 240] | 节奏紧迫感；C1 判定窗口绝对宽度随 BPM 提高而缩小 | **数据字段由 F4 `LevelConfigSO.bpm` 定义**；F2 负责运行时 clamp（超出范围 → 边界值、BPM=0 → 120）；BPM=120 是标准参照点；超过 180 时 half_beat_interval 缩至 167ms，需配合 C1 判定窗口同步收紧 |
| `timeSignature`（每个关卡/Boss 阶段） | `4/4` | {3/4, 4/4, 5/8} | 小节长度；影响 C4 令牌分配模式 | Boss 二阶段序列：4/4 → 3/4 → 5/8；切换在小节边界生效 |
| `lookaheadMs`（全局配置） | 100 ms | [50, 200] | 动画/特效提前准备时间 | 下限 50ms：低于此值时部分动画无法完整播放。上限受 BPM 约束（不超过 `half_beat_interval_s × 1000 × 0.8`）；高 BPM 下此参数自动收缩 |
| `defaultFallbackBpm`（全局配置） | 120 | [80, 180] | BPM=0 兜底时的节奏感 | 仅在音频不可用时生效，玩家通常不感知 |
| `latencyOffsetMsDefault`（全局配置） | 0 ms | [-200, 200] | 首次启动（校准前）的初始延迟补偿 | FE3 完成校准后由实测值覆盖；默认 0 ms 适合有线耳机用户 |

**参数间交互**

- **BPM ↑ + lookaheadMs 固定** → `half_beat_interval_s` 缩短，`lookahead_ms` 可能触发自动 clamp → 关卡设计时应显式降低 `lookaheadMs`
- **BPM ↑** → C1 最大判定窗口上限收紧 → C1 GDD 判定窗口需同步调整，否则相邻节点判定区重叠
- **timeSignature = 5/8** → 小节周期缩短（@BPM=120：1.25 s vs 4/4 的 2.0 s）→ C4 令牌分配密度增加

---

## Visual/Audio Requirements

F2 是纯基础设施系统，无直接视觉或音频资产需求。

- **BGM AudioClip**：由关卡配置（LevelConfigSO）提供，F2 不持有也不选择素材
- **音频不可用降级模式**：游戏暂停（节奏判定亦暂停）；F2 发出 `OnAudioUnavailable` + `OnVisualMetronomeActivated` 事件，P1 订阅后渲染视觉节拍指示器（具体样式由 P1 GDD 定义）。DSP 时钟仍可查询供视觉节拍器参考
- **无独立 SFX / VFX**：拍点反馈的视觉/音频呈现由 P1（HUD 脉冲）和 PL2（VFX 节奏反馈）负责

---

## UI Requirements

F2 无运行时 UI。

唯一与 UI 相关的调试需求：
- **编辑器 Debug 面板**（仅 `UNITY_EDITOR` 条件编译）：显示当前 BPM、拍号、`beat_progress`、`global_beat_index`，供开发期调试使用，不打包进正式构建

---

## Acceptance Criteria

> **测试位置**：`Assets/Tests/EditMode/F2/` + `Assets/Tests/PlayMode/F2/`
> **门控级别**：BLOCKING — MVP 门控以 AC-F2-01 ~ AC-F2-20、AC-F2-23 及 AC-F2-24 ~ AC-F2-28（含拆分后的 a/b 子条目；AC-F2-28 = BC-6 暂停/恢复事件）为准，自动化覆盖率 ≥ 80%
> AC-F2-21/22（5/8 拍号，Post-MVP）标记为 ADVISORY，不阻塞 MVP 验收。AC-F2-07b 为手工 QA 证据步骤，CI 中以 MockDSPClock 替代（详见 AC-F2-07b CI 策略注记）
> **可测性要求**：BeatClock 须通过 `IDSPClock` 接口注入时钟源，EditMode 测试使用 `MockDSPClock` 进行确定性验证
>
> **测试基础设施规范**：BeatClock 为 `MonoBehaviour`，`EditMode` 测试须通过 `go.AddComponent<BeatClock>()` 实例化，**禁止**使用 `new BeatClock()`（MonoBehaviour 不可通过构造函数实例化）。每个测试须在 `[TearDown]` 中调用 `Object.DestroyImmediate(go)` 以防 `DontDestroyOnLoad` 单例跨测试泄漏，影响后续测试结果。

---

**AC-F2-01：节拍间隔公式精度** `[EditMode]`
- **Given** BPM = 120
- **When** 调用 `BeatClock.GetBeatIntervalSeconds()`
- **Then** 返回值 ∈ [0.49999, 0.50001]（double 精度，误差 < 0.01ms）

**AC-F2-02：半拍间隔公式精度** `[EditMode]`
- **Given** BPM = 180
- **When** 调用 `BeatClock.GetHalfBeatIntervalSeconds()`
- **Then** 返回值 ∈ [0.16666, 0.16668]（误差 < 0.01ms）

**AC-F2-03：第 N 拍 DSP 时间戳精度** `[EditMode]`
- **Given** `dsp_start = 10.000`，BPM = 120，n = 3
- **When** 计算 `nth_beat_dsp_time`
- **Then** 结果为 11.500（误差 < 0.001ms）

**AC-F2-04：延迟偏移方向正确（正值）** `[EditMode]`
- **Given** `raw_beat_dsp_time = 11.500 s`，`latency_offset_ms = 80`（无线耳机 80ms 延迟）
- **When** F2 计算 `adjusted_beat_time`
- **Then** `adjusted = 11.580 s`（正偏移 → 判定窗口后移；`adjusted = raw + offset/1000 = 11.500 + 0.080`）

**AC-F2-05：延迟偏移方向正确（负值）** `[EditMode]`
- **Given** `raw_beat_dsp_time = 11.500 s`，`latency_offset_ms = −80`（声音提前到达）
- **When** F2 计算 `adjusted_beat_time`
- **Then** `adjusted = 11.420 s`（负偏移 → 判定窗口前移；`adjusted = raw + offset/1000 = 11.500 − 0.080`）

**AC-F2-06a：Lookahead 事件负载时间戳正确** `[EditMode]`
- **Given** `adjusted_beat_time = T`，`lookahead_ms = 100`
- **When** 使用 `MockDSPClock` 注入可控 `dsp_now`，推进至 `T − 0.100` 时触发 `OnBeatLookahead`
- **Then** 事件负载中的 DSP 时间戳 == `T`（误差 < 0.01ms）

**AC-F2-06b：Lookahead 事件不重复触发** `[EditMode]`
- **Given** 同上条件，`OnBeatLookahead` 已在 `dsp_now = T − 0.100` 触发
- **When** `dsp_now` 推进至 `T − 0.095`
- **Then** `OnBeatLookahead` 不再次触发（单拍单次广播）

**AC-F2-07a：Exact 事件时间戳值精度** `[EditMode]`
- **Given** 使用 `MockDSPClock` 注入，`dsp_start = 10.000`，BPM = 120
- **When** `dsp_now` 推进至第 3 拍对应的 DSP 时刻（11.500 s）时，`OnBeatExact` 触发
- **Then** 事件负载中的 DSP 时间戳 == 11.500 s（误差 < 0.1ms）

**AC-F2-07b：Exact 事件时间戳真实 DSP 环境验证** `[PlayMode]` `[BLOCKING]`
- **Given** 真实 `AudioSettings.dspTime` 环境，BPM = 120，`PlayScheduled()` 方式启动 BGM
- **When** 连续采样 8 拍的 `OnBeatExact` 事件，读取每次事件负载中的 `BeatEventData.beat_dsp_time`（而非回调被调用时的 `AudioSettings.dspTime`）
- **Then** 每次 `BeatEventData.beat_dsp_time` 与理论拍点时间（`dsp_start + n × beat_interval_s`）的偏差 **< 0.1ms**；记录实测数据至 `production/qa/evidence/F2-07b-timestamps-[date].csv` 供人工审查。**注**：回调触发时刻（何时调用本回调）与拍点的偏差取决于 ADR-D6 调度机制（Update 轮询最高 ±16.7ms，FMOD DSP callback ±1-2ms），不在本 AC 验收范围——本 AC 仅验证时间戳**值**的精度，即 Core Rule 12 的实际承诺

  > **CI 策略**：headless CI runner 无音频设备，`AudioSettings.dspTime` 返回 0，本 AC **不作为 CI 自动化门控**。CI 中使用 `MockAudioPlayer` + `MockDSPClock` 路径执行（等同 AC-F2-07a EditMode 覆盖）；真实设备 FMOD 精度验证（< 0.1ms）作为手工 QA 证据步骤，存档路径见上方 CSV 要求。

**AC-F2-07c：长时间锚定漂移验证（500 拍）** `[EditMode]` `[BLOCKING]`
- **Given** `MockDSPClock`，`dsp_start = 10.000`，BPM = 120（`beat_interval_s = 0.500 s`）
- **When** 调度器推进至第 500 拍的理论时刻（`10.000 + 500 × 0.500 = 260.000 s`），`OnBeatExact` 触发
- **Then** 事件负载 `BeatEventData.beat_dsp_time == 260.000 s`（误差 < 0.1ms）；验证锚定形式（`dsp_start + n × beat_interval_s`）在 500 拍后无累积漂移，不得使用迭代累加实现

**AC-F2-08：BPM=0 回退 120** `[EditMode]`
- **Given** 关卡配置传入 `bpm = 0`
- **When** 调用 `BeatClock.Play(clip, bpm: 0, timeSig: 4/4)`
- **Then** 时钟以 120 BPM 启动；记录一条 Warning 日志；无异常抛出

**AC-F2-09：BPM 超上界 Clamp** `[EditMode]`
- **Given** 传入 `bpm = 300`
- **When** `BeatClock.Play()` 被调用
- **Then** 实际运行 BPM = 240；记录 Warning；`GetBeatIntervalSeconds()` = 0.250 s

**AC-F2-10：BPM 超下界 Clamp** `[EditMode]`
- **Given** 传入 `bpm = 50`
- **When** `BeatClock.Play()` 被调用
- **Then** 实际运行 BPM = 80；记录 Warning；`GetBeatIntervalSeconds()` = 0.750 s

**AC-F2-11：Lookahead_ms 高 BPM 自动 Clamp** `[EditMode]`
- **Given** BPM = 240（`half_beat_interval_s = 0.125 s`），`lookahead_ms = 150`
- **When** F2 计算 lookahead 触发时间
- **Then** `lookahead_ms` 被 clamp 至 100ms（= 0.125 × 800）；记录 Warning；`lookahead_trigger_time = adjusted_beat_time − 0.100`

**AC-F2-12：Stopped 状态快照返回零值** `[EditMode]`
- **Given** `BeatClock` 处于 `Stopped` 状态（未调用 `Play()`）
- **When** 任意系统查询 `BeatStateSnapshot`
- **Then** 返回：`bpm = 0`，`beatProgress = 0`，`measureIndex = 0`，`beatIndexInMeasure = 0`；不广播任何事件

**AC-F2-13：BPM 切换等待小节边界** `[PlayMode]`
- **Given** 时钟以 120 BPM / 4/4 运行；订阅者在 `beatIndexInMeasure == 1`（0-indexed，小节内第 2 拍）的 `OnBeatExact` 回调中调用 `RequestBPMChange(180)`（即在第 2 拍恰好触发时发起切换请求）
- **When** 订阅者统计从本次切换请求发起（**含**触发请求时的当前拍 index=1）直到新 BPM 生效后第一拍（含）的全部 `OnBeatExact` 事件
- **Then** (1) 本小节余下 3 次 `OnBeatExact`（index=1、2、3）；其中 index=1→2 和 index=2→3 的相邻间隔各为 0.500s ± 10ms（共 **2 个间隔**，非 3 个——3 个事件只产生 2 个相邻间隔）；(2) 小节边界后，新 BPM（180）下首拍广播 1 次 `OnBeatExact`，与前一拍间隔为 **0.500s ± 10ms**（= 旧 BPM 的 `beat_interval_old`，非新 BPM 的 `beat_interval_new = 0.333s`——推导：新段基准 `dsp_start_new = T + 4×0.500 = T+2.000`；旧最后一拍 `T+1.500`；间隔 = `2.000−1.500 = 0.500s`；新 BPM 的 inter-beat 间隔 0.333s 首次出现于新段第 0 拍与第 1 拍之间）；(3) 上述事件合计恰好 **4 次**（无多无少）；(4) 切换生效后 `BeatStateSnapshot.bpm == 180`

**AC-F2-14：拍号切换等待小节边界** `[PlayMode]`
- **Given** 时钟以 4/4 运行，处于当前小节第 1 拍
- **When** 调用 `RequestTimeSignatureChange(3, 4)`
- **Then** 测试订阅者计数：切换前小节共收到 4 次 `OnBeatExact`；切换后第 1 小节收到 3 次 `OnBeatExact`；切换后 `BeatStateSnapshot.beatIndexInMeasure` ∈ {0, 1, 2}（最大值 = 2）

**AC-F2-15：同帧 BPM + 拍号切换合并处理** `[PlayMode]`
- **Given** 时钟以 120 BPM / 4/4 运行
- **When** 同一帧调用 `RequestBPMChange(160)` + `RequestTimeSignatureChange(3, 4)`
- **Then** (1) Info 日志包含"BPM and time signature change merged"或等价语义字符串；(2) 切换生效后首拍查询 `BeatStateSnapshot`：`bpm == 160` 且 `numerator == 3`（同时生效）；(3) 切换前后 `OnBeatExact` 事件序列中不出现任何相邻间隔 < 5ms 的情况（正常拍间隔 ≥ 250ms；任意 < 5ms 的相邻间隔即为双重边界触发的特征，不论是否连续出现两次）

**AC-F2-16：暂停停止广播** `[PlayMode]`
- **Given** 时钟处于 `Playing` 状态
- **When** 发送暂停信号
- **Then** 状态变为 `Paused`；暂停后 500ms 内无 `OnBeatExact` / `OnHalfBeatExact` 事件触发

**AC-F2-16b：音频不可用降级行为** `[PlayMode]` `[BLOCKING]`
- **Given** 时钟处于 `Playing` 状态
- **When** `IAudioPlayer` Mock 模拟音频不可用（`MockAudioPlayer.PlayScheduled()` 返回失败信号，或 `IsPlaying` 返回 `false` 并触发不可用通知）；注：真实 `AudioSource.PlayScheduled()` 失败时**不抛出异常**（静默失败），必须通过 `IAudioPlayer` 接口的失败回调或状态轮询检测
- **Then** (1) 时钟在 1 帧内转入 `Paused` 状态；(2) `OnAudioUnavailable` 事件在同帧发出（测试订阅者计数器 == 1）；(3) `OnVisualMetronomeActivated` 事件在同帧发出（计数器 == 1）；(4) 之后 500ms 内无新 `OnBeatExact` 事件广播；(5) 调用 `Resume()` 后时钟从暂停位置正常续播，下一拍时序与 AC-F2-17 一致

**AC-F2-17：恢复后无缝续播** `[PlayMode]`
- **Given** BPM = 120（`beat_interval_s = 0.500 s`），时钟在全拍到来后 0.200 s 处（`fractional_progress_at_pause = 0.200 s`，可通过 `BeatStateSnapshot.beatProgress × beat_interval_s` 验证）暂停 1.5 秒后恢复
- **When** 恢复后下一拍触发
- **Then** (1) 下一拍的 `BeatEventData.beat_dsp_time` 与恢复时刻的间隔 = `beat_interval_s − fractional_progress_at_pause = 0.300 s`（误差 < 10ms）；(2) `beat_index_in_measure` 从暂停位置续接，不重置为 0；(3) 恢复后首帧 `BeatStateSnapshot.beatProgress` ∈ [0.367, 0.433]（基于 `fractional_progress_at_pause / beat_interval_s = 0.200 / 0.500 = 0.40`，允许 ±1 帧 = ±16.7ms 容差，等效 ±0.0334 beatProgress 单位；测试须显式断言此值 ≠ 0.0，以排除「进度被错误重置至 0」的失败模式）

**AC-F2-18：暂停期间 BPM 切换入队** `[PlayMode]`
- **Given** 时钟处于 `Paused` 状态
- **When** 调用 `RequestBPMChange(200)`
- **Then** 状态进入 `PendingBPMChange`；恢复后等当前小节完成再切换；切换前的拍点以旧 BPM 广播

**AC-F2-19a：空订阅者时不抛异常** `[EditMode]`
- **Given** `OnBeatExact` 和 `OnHalfBeatExact` 无任何订阅者
- **When** 使用 `MockDSPClock` 推进至全拍和半拍节点，BeatClock 内部调度触发
- **Then** 不抛出任何异常（空 `event` 防御性调用验证）

**AC-F2-19b：全量网格广播比例正确** `[PlayMode]`
- **Given** BPM = 120（`beat_interval_s = 0.500 s`），注册单个测试订阅者；测试须先断言 `BeatClock.IsStartConfirmed == true`（等待乐观预锚定完成），再调用 `BeatClock.GetConfirmedDSPStart()` 获取 `actual_dsp_start`（不得在 `IsStartConfirmed == false` 时读取，此时返回值为 `-1.0` 无效哨兵）；测试窗口为 `[actual_dsp_start, actual_dsp_start + 2.5 s)`（含左端点，**不含**右端点）
- **When** 统计 `OnBeatExact`（预期触发时刻：`actual_dsp_start + 0, 0.5, 1.0, 1.5, 2.0 s` 各一次）和 `OnHalfBeatExact`（预期：`actual_dsp_start + 0.25, 0.75, 1.25, 1.75, 2.25 s` 各一次）
- **Then** `OnBeatExact` 次数 **== 5**（精确，无 ±1 容忍）；`OnHalfBeatExact` 次数 **== 5**（精确）；两者合计 == 10。`actual_dsp_start + 2.5 s` 处的拍点在窗口外，不计入（右端点排他性须在测试中显式断言）。*注：若使用 MockAudioPlayer，ConfirmDSPStart 由 F6 在 PlayScheduled 后立即调用（乐观预锚定），`GetConfirmedDSPStart()` 立即可用；PlayMode 实测时须在统计前等待一帧确保 ConfirmDSPStart 完成。*

**AC-F2-19c：订阅者生命周期合约验证** `[PlayMode]`
- **Given** 测试订阅者 A 注册 `OnBeatExact`，A 在 `OnDestroy()` 中正确调用取消订阅
- **When** A 的 GameObject 被 `Object.Destroy()` 并等待 1 帧生效后，BeatClock 推进至下一拍点触发 `OnBeatExact`
- **Then** A 的回调**不被调用**（A 内部计数器维持 0）；不抛出任何异常（`NullReferenceException` / `MissingReferenceException`）；BeatClock 正常继续广播后续拍点

**AC-F2-20：节拍进度公式范围** `[EditMode]`
- **Given** BPM = 120，`last_beat_time = 11.420`
- **When** `dsp_now` 从 11.420 递进至 11.920
- **Then** `beat_progress` 从 0.0 线性增至 1.0；任何时刻值 ∈ [0.0, 1.0]（clamp 保证）

**AC-F2-21：5/8 小节边界检测** `[EditMode]` `[ADVISORY — Post-MVP]`
- **Given** 拍号切换至 5/8，`time_sig_segment_half_beat_index (h)` 已在切换时归零
- **When** `h` 依次为 0, 1, 2, 3, 4, 5
- **Then** `is_measure_boundary`（`h > 0 && h mod 5 == 0`）仅在 h = 5 时为 true；**h = 0 时 `is_measure_boundary == false`**（重置哨兵，不触发 Pending 变更生效）；Pending 变更（BPM/拍号切换请求）在 h = 5 时生效，不在 h = 0 时立即应用；小节时长 @BPM=120 = 1.250 s（误差 < 1ms）

**AC-F2-22：5/8 下 C1 接口透明** `[PlayMode]` `[ADVISORY — Post-MVP]`
- **Given** 拍号切换至 5/8，BPM = 120
- **When** 监听 `OnHalfBeatExact` 事件
- **Then** 事件间隔 = 0.250 s（eighth_note_interval_s）；事件频率与 4/4 下的 `OnHalfBeatExact` 相同；C1 无需感知拍号变化

**AC-F2-23：RequestBPMChange() 越界 Clamp** `[EditMode]`
- **Given** 时钟以 120 BPM / 4/4 运行（`Playing` 状态）
- **When** 调用 `RequestBPMChange(300)`（超出上界 240）
- **Then** Pending 的目标 BPM 被 clamp 至 240；记录 Warning 日志；切换在当前小节结束后生效；生效后 `BeatStateSnapshot.bpm == 240`；`GetBeatIntervalSeconds() == 0.250 s`（误差 < 0.01ms）

**AC-F2-24：OnMeterChangeAccepted 接受时广播与负载** `[EditMode]` `[BLOCKING]`
- **Given** `MockDSPClock` 注入，BPM=120 / 4/4 运行（`dsp_start=10.000`）；测试订阅 `OnMeterChangeAccepted`
- **When** 调用 `RequestBPMChange(180)`
- **Then** (1) 事件**恰好触发 1 次**，且在 `RequestBPMChange()` 返回的同一帧（`Time.frameCount` 相同 / EditMode 同一 tick）；(2) payload `bpmChanged == true`、`timeSigChanged == false`、`oldBpm == 120`、`newBpm == 180`、`oldNumerator == newNumerator == 4`；(3) `predictedEffectiveDspTimeRaw` 等于接受时刻当前小节边界的 **raw** DSP 时刻（F2-F3 计算，误差 < 0.1ms），且为 raw（在 `latency_offset_ms != 0` 场景下断言其**不含** offset）；(4) 此刻 `BeatStateSnapshot.bpm` 仍为 120（尚未生效）

**AC-F2-25：OnMeterChanged 生效时广播、顺序与 raw 坐标** `[EditMode]` `[BLOCKING]`
- **Given** 复用 AC-F2-13 场景（`MockDSPClock`，BPM=120 / 4/4）；在 `beatIndexInMeasure == 3`（末拍）的 `OnBeatExact` 回调中调用 `RequestBPMChange(180)`；同时订阅 `OnMeterChanged` 与 `OnBeatExact`，各记录单调递增的全局调用序号
- **When** `MockDSPClock` 推进跨越小节边界，进入新 BPM 段
- **Then** (1) `OnMeterChanged` **恰好触发 1 次**；(2) 顺序（同一主线程 `Update()` dispatch pass 内确定性保证，ADR-D6 Addendum）：其调用序号 **>** 触发切换的末拍 `OnBeatExact`，且 **<** 新 BPM 段第 0 拍 `OnBeatExact`（保证收到 `OnMeterChanged` 时新段尚无 Exact 先行广播）；(3) payload `newBpm == 180`、`newBeatIntervalS == 60.0/180`（误差 < 0.001ms），且与生效后 `GetBeatIntervalSeconds()` 一致；(4) `predictedEffectiveDspTimeRaw` 为 **raw**，与 AC-F2-13 结论中的边界时刻（旧末拍 raw DSP 时刻）相等，误差 < 0.1ms（非 adjusted）；(5) 回调内查询 `BeatStateSnapshot.bpm` 已 == 180（状态先于广播翻转）

**AC-F2-26：合并 BPM+拍号变更下双事件各触发一次** `[EditMode]` `[BLOCKING]`
- **Given** 复用 AC-F2-15 场景（120 BPM / 4/4）；订阅 `OnMeterChangeAccepted` 与 `OnMeterChanged`
- **When** 同一帧调用 `RequestBPMChange(160)` + `RequestTimeSignatureChange(3, 4)`，随后推进至小节边界生效
- **Then** (1) `OnMeterChangeAccepted` **恰好 1 次**（非 0、非 2），payload `bpmChanged == true && timeSigChanged == true`、`newBpm == 160`、`newNumerator == 3`；(2) `OnMeterChanged` **恰好 1 次**（非 0、非 2），payload 同上；(3) 二者均不因 BPM 与拍号合并而被吞或双发。**（拍号-only 变体，Post-MVP）**：3/4→5/8 仅拍号变更时，两事件仍各 1 次，`bpmChanged == false`、`timeSigChanged == true`、`newBpm == oldBpm`

**AC-F2-27：新事件空订阅者与订阅者生命周期防御** `[EditMode]` + `[PlayMode]` `[BLOCKING]`
- **Given** `OnMeterChangeAccepted` / `OnMeterChanged` 无任何订阅者（EditMode 子用例）；另有一个在 `OnDestroy()` 中正确退订的订阅者 A（PlayMode 子用例）
- **When** 触发一次 BPM 变更全程（接受 → 生效）；随后 A 的 GameObject `Object.Destroy()` 并等 1 帧后再触发一次变更
- **Then** (1) 空订阅者路径不抛任何异常（镜像 AC-F2-19a）；(2) A 销毁后其回调**不被调用**（内部计数器维持 0），不抛 `MissingReferenceException` / `NullReferenceException`（镜像 AC-F2-19c）；(3) BeatClock 正常继续广播后续事件

**AC-F2-28：暂停/恢复事件成对广播与 raw 首拍锚点（BC-6）** `[PlayMode]` `[BLOCKING]`
- **Given** 时钟处于 `Playing`（BPM=120，`beat_interval_s=0.500 s`），注入 `MockAudioPlayer`；订阅 `OnBeatClockPaused` 与 `OnBeatClockResumed`，各记录单调递增的全局调用序号
- **When** 在整拍到来后 0.200 s 处调用 `BeatClock.Pause()`，暂停 1.5 s 后调用 `BeatClock.Resume()`
- **Then** (1) `OnBeatClockPaused` **恰好触发 1 次**，且在转入 `Paused` 的同帧（其序号在恢复后首个 `OnBeatExact` 之前），payload `isResume==false`、`fractionalProgressAtPause ∈ [0.183, 0.217]`（0.200±1 帧）；(2) `OnBeatClockResumed` **恰好触发 1 次**，序号 **>** `OnBeatClockPaused`、且 **<** 恢复后首个 `OnBeatExact`（重锚定后、首拍前广播）；(3) `OnBeatClockResumed` payload `isResume==true`、`resumeFirstBeatDspTimeRaw` 为 **raw** 坐标（`latency_offset_ms != 0` 时断言其**不含** offset），等于恢复后首拍的 F2-F3 raw DSP 时刻（误差 < 0.1ms）；(4) 回调内查询 `BeatStateSnapshot.clockState` 已翻转为 `Playing`（状态先于广播翻转）；(5) 全程 paused 计数 == resumed 计数 == 1（成对完整）；(6) `MockAudioPlayer` 记录到 `Pause()` 与 `Resume()` 各被调用 1 次（BeatClock 委托 `IAudioPlayer` 执行音频层暂停/恢复）
- **空订阅者防御（EditMode 子用例）**：`OnBeatClockPaused` / `OnBeatClockResumed` 无任何订阅者时，触发一次「暂停→恢复」周期不抛任何异常（镜像 AC-F2-19a / AC-F2-27）

---

**覆盖矩阵**

| AC | 环境 | 测试类 | 门控 |
|----|------|--------|------|
| F2-01 ~ F2-05、F2-20 | EditMode | `BeatClockFormulasTests` | BLOCKING |
| F2-06a/b | EditMode | `BeatClockLookaheadTests` | BLOCKING |
| F2-07a | EditMode | `BeatClockExactTimestampTests` | BLOCKING |
| F2-07b | PlayMode | `BeatClockExactTimestampTests` | **BLOCKING**（时间戳值误差 < 0.1ms） |
| F2-07c | EditMode | `BeatClockExactTimestampTests` | **BLOCKING**（500 拍漂移 < 0.1ms） |
| F2-08 ~ F2-12 | EditMode | `BeatClockEdgeCaseTests` | BLOCKING |
| F2-13 ~ F2-15 | PlayMode | `BeatClockTransitionTests` | BLOCKING |
| F2-16 | PlayMode | `BeatClockPauseResumeTests` | BLOCKING |
| F2-16b | PlayMode | `BeatClockAudioFallbackTests` | BLOCKING |
| F2-17 ~ F2-18 | PlayMode | `BeatClockPauseResumeTests` | BLOCKING |
| F2-19a | EditMode | `BeatClockBroadcastTests` | BLOCKING |
| F2-19b | PlayMode | `BeatClockBroadcastTests` | BLOCKING |
| F2-19c | PlayMode | `BeatClockLifecycleTests` | BLOCKING |
| F2-21 | EditMode | `BeatClock58TimeSigTests` | ADVISORY (Post-MVP) |
| F2-22 | PlayMode | `BeatClock58TimeSigTests` | ADVISORY (Post-MVP) |
| F2-23 | EditMode | `BeatClockEdgeCaseTests` | BLOCKING |
| F2-24 | EditMode | `BeatClockMeterChangeEventTests` | BLOCKING |
| F2-25 | EditMode | `BeatClockMeterChangeEventTests` | BLOCKING |
| F2-26 | EditMode | `BeatClockMeterChangeEventTests` | BLOCKING |
| F2-27 | EditMode + PlayMode | `BeatClockMeterChangeEventTests` / `BeatClockLifecycleTests` | BLOCKING |
| F2-28 | PlayMode + EditMode | `BeatClockPauseResumeTests` | BLOCKING（BC-6 暂停/恢复事件成对性 + raw 首拍锚点） |

---

## Open Questions

| # | 问题 | 阻塞项 | 负责人 | 解决路径 |
|---|------|--------|--------|---------|
| OQ-1 | M0 Spike：量化档位（Off / HalfBeat / Beat）的判定窗口数据（Perfect/Good 各占 half_beat_interval 的比例）尚未实测 | 阻塞 C1 GDD Formulas 节 | C1 设计者 | M0 Spike 完成后更新 C1 GDD；F2 公式 F2-F2 提供上界，C1 须在范围内设定 |
| ~~OQ-2~~ | ~~ADR-D6（FMOD vs Unity Audio）~~ | **已解决** — ADR-D6 Accepted (2026-07-14)，选定 FMOD Studio 2.x | — | `docs/architecture/adr-d6-audio-middleware.md`；F2 实现阻塞已解除 |
| OQ-3 | BeatAccentMap（5/8 节奏分组 2+3 或 3+2）是否作为 F2 可选配置 | 不阻塞 F2；影响 C4 设计 | C4 设计者 | 待 C4 GDD 明确需求后决定是否在 F2 Tuning Knobs 增加 `accentPattern` 参数 |
| ~~OQ-4~~ | **B2b 初始化时序**（W-4-01）。**已解决** — ADR-D10 Accepted (2026-08-09)：D-4 定 `SaveService.Initialize()` 同步阻塞 + `ISaveService` 消费 seam；D-5 定 `LatencyOffsetMs` 两阶段写者（F6 gate 一次性 boot-inject + FE3 运行期唯一写者），F2 不再自 load。Rule 7/10 已按两阶段模型修订，依赖文案同轮更新 | — | technical-director | `docs/architecture/adr-d10-bootstrap-sequencing.md`；F5 `:127/:221/:316` + F6 启动链路（AC-F6-23）已同轮修订 |
| ~~OQ-5~~ | **B2a 暂停/恢复事件**（BC-6）。**已解决**（cross-review 2026-08-10 blocker fix）：F2 权威定义 `OnBeatClockPaused` / `OnBeatClockResumed`（Core Rule 8 + `BeatClockPauseEventData` 结构），补 `IAudioPlayer.Pause()/Resume()`（Rule 12），恢复事件携 `resumeFirstBeatDspTimeRaw`（raw 首拍锚点）；C1 重锚拍网格+清空过期 `_pendingInput`、C3 冻结栈恢复统一订阅此对事件（Dependencies BC-6 合约）；AC-F2-28 覆盖成对性 | — | audio（IAudioPlayer 契约） | 本文件已落定；C1/C3/F6 反向引用为同轮跟进（见各文件任务清单） |
| ~~OQ-6~~ | **ADR-D6 线程模型回填**。**已解决** — ADR-D6 Threading Model Addendum (2026-08-08)：调度确定为 FMODUnity RuntimeManager + 主线程 `Update()` 轮询，否决 `OnAudioFilterRead`；`ConcurrentQueue` 不需要且禁止引入。Core Rule 12 `:65` / Rule 14 的条件句已删除并改为 normative 主线程模型；AC-F2-25 断言主线程单 dispatch pass 确定性顺序 | — | technical-director + ADR-D6 Addendum | `docs/architecture/adr-d6-audio-middleware.md` § Addendum；F2 Rule 12/14 已同轮回填 |
