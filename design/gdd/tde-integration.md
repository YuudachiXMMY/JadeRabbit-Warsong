# F6: TopDownEngine & Animancer 集成层 (TDE Integration Layer)

> **Status**: Approved (2026-08-12 — 死亡/run-end 语义收口 re-review 通过)
> **Author**: Jadyn Wu + Claude Code Agents
> **Last Updated**: 2026-08-12 (ADR-D12 死亡=换新局 + ADR-D13 统一 run-end 边界：单一入口 `EndRun(RunOutcome)` + 一次性闭锁 `_runEnded`、Death/Victory 双路径、`OnRunEnded(outcome)` 取代 `OnPlayerDeath`、RespawnComplete MVP 休眠诊断守卫) ｜ 2026-08-10 (SC-1 — F6 订阅 F2 `OnAudioUnavailable`，同帧 `TDE.Pause()` + `F3.DisableAllInput()` 承接音频断开降级) ｜ 2026-07-19 (R3 — 4 blockers resolved)
> **Implements Pillar**: 支柱二：武侠手感不可妥协 (Wuxia Feel First)
> **System ID**: F6
> **Layer**: Foundation | **Milestone**: MVP | **Design Order**: 5
> **ADR Reference**: `docs/architecture/adr-d1-tde-integration.md` (Accepted, M0 Spike verified 2026-07-16) ｜ `adr-d5-animation-pipeline.md` (Accepted) ｜ `adr-d10-bootstrap-sequencing.md` (Accepted) ｜ `adr-d12-death-run-lifecycle.md` (Accepted 2026-08-11 — 死亡=换新局) ｜ `adr-d13-run-end-victory-boundary.md` (Accepted 2026-08-12 — 统一 run-end 边界)

---

## Overview

F6 TopDownEngine & Animancer 集成层是《玉兔：破阵》的引擎整合地基——它在 TopDownEngine（TDE）提供的移动/生命/关卡/反馈基础设施与项目自建的 Foundation 系统（F2 BeatClock、F3 InputEventBus、F4 LevelConfigSO 数据层）之间建立明确的合约边界，同时定义 Animancer 攻击动画层与 TDE 原生 Animator Controller 层之间的分工协议，使攻击动画时长能随当前 BPM 动态缩放。

从技术架构看，F6 由两个组件构成：**`JadeRabbitInputManager`**（`InputSystemManagerEventsBased` 的继承子类，将所有 Action Map 状态变更代理给 F3，保留 TDE 的 `AbilityInputAuthorization` 管线）和 **`TDELifecycleAdapter`**（DontDestroyOnLoad Manager 上的适配器，是 `BeatClock.Play/Stop/Pause/Resume` 的唯一控制者，并在每次单场景加载后将 `LevelConfigSO` 的 BPM/拍号注入 BeatClock）。架构决策与 M0 Spike 验证结果见 `docs/architecture/adr-d1-tde-integration.md`（Accepted，2026-07-16）。

从玩家体感看，F6 是「武侠手感不可妥协」支柱的物理前提：冲刺的流畅、近战的击打感、攻击动画随 BPM 呼吸的节奏质感，均依赖本层的正确实现。暂停/恢复/死亡/重生的原子化序列确保 BeatClock 与输入状态始终同步——玩家不会在暂停后遭遇「死亡前的幽灵攻击」，也不会在 Boss 阶段切换时感受到节奏断层。

---

## Player Fantasy

F6 的玩家幻想是**天衣无缝的动作应噬**——每一次出招、侧步和冲刺都感觉像是从音乐中自然流出，而非玩家在强行对齐节拍。

**核心感知时刻**：当玩家以 Perfect 评级砍出一刀，攻击动画的速度与当前 BPM 精确吻合——不是「这刀打在了拍子上」，而是「这刀本来就是这首曲子的一个音符」。这种感知来自 Animancer 实时将攻击 Clip 速度缩放至当前 BPM，使节奏加速时动作更凌厉，节奏放缓时动作更沉稳。移动、闪避由 TDE 提供近 10 年打磨的移动系统，手感不需要节奏层「让步」。

**F6 的成败尺度**：玩家不应感知到 F6 本身的存在——他感知到的是「这个角色就是一把由音乐驱动的武器」。每次暂停时音乐骤停与攻击输入同帧失效的精确感；每次复活时新曲律动起来与输入通道同步开放的仪式感——这些是 F6 正常工作时留下的全部感受。F6 一旦失效，玩家不会说「集成层出问题了」，他们会说「这个游戏手感断了」。

**支柱对齐**：支柱二「武侠手感不可妥协」——节奏层叠加在动作手感之上，而非取代它。F6 确保 TDE 的成熟移动系统完整保留，Animancer 仅接管攻击层的速度维度，不破坏 TDE 原生 Animator Controller 的状态机主体。

---

## Detailed Design

### Core Rules

1. **两组件架构（Two-Component Architecture）**
   F6 由两个独立组件构成，职责严格分离：
   - **`JadeRabbitInputManager`**（角色 Prefab 上）：`InputSystemManagerEventsBased` 的继承子类，重写 `BindButton()` 将所有按钮输入代理给 F3 状态机门控，保留 TDE 的 `AbilityInputAuthorization` 管线。
   - **`TDELifecycleAdapter`**（DontDestroyOnLoad Manager 上）：`MonoBehaviour + MMEventListener<TopDownEngineEvent>`，独家控制 BeatClock 生命周期，桥接 TDE 与 F2/F3/F4 之间的所有信号。

2. **TDE 源码修改绝对禁止**
   对 TDE 内部类的任何直接修改均被禁止。所有集成通过继承、事件订阅或 Inspector 配置实现。需要偏离此规则须走 ADR 审批流程。

3. **Action Map 唯一权威**
   F3（`InputEventBus`）是所有 Action Map 状态变更的唯一权威。`JadeRabbitInputManager` 通过继承保留 TDE 的 `AbilityInputAuthorization` 管线，但不直接调用 `InputActionMap.Enable()/Disable()`——这些操作由 F3 独家执行。

4. **BeatClock 生命周期唯一权威**
   `TDELifecycleAdapter` 是 `BeatClock.Play()/Stop()/Pause()/Resume()` 的唯一调用者。C1、C4 等 CombatCore 系统只能订阅 BeatClock 事件，禁止调用其生命周期方法。

5. **FMOD 初始化前置验证**
   `TDELifecycleAdapter.Start()`（非 `Awake()`，避免与 `FMODUnity.RuntimeManager` 脚本执行顺序竞争）验证 FMOD RuntimeManager 已初始化。检查失败时记录 fatal 错误并停止——场景保持冻结，不触发 BeatClock 启动。

6. **Additive 加载过滤与 LevelConfigSO 获取**
   `TDELifecycleAdapter.OnSceneLoaded()` 仅响应 `LoadSceneMode.Single`（整场景切换），忽略 `LoadSceneMode.Additive`（TDE LevelManager 用于房间拼接）。房间内 BPM 变化通过 `BeatClock.RequestBPMChange()` 处理，不重新调用 `Play()`。

   **LevelConfigSO 获取路径（强制）**：`OnSceneLoaded(Single)` 回调提供 `Scene scene` 参数，通过 `scene.GetRootGameObjects()` 遍历该场景根节点，搜索持有 `LevelConfigSO` 的 `LevelBootstrap`（或等效持有组件）；**禁止使用 `FindObjectOfType<LevelBootstrap>()`**——`TDELifecycleAdapter` 是 DontDestroyOnLoad 对象，`FindObjectOfType` 在此上下文的场景边界行为不稳定，可能返回旧场景残留对象。若 `scene.GetRootGameObjects()` 未找到有效 `LevelConfigSO`，记录 fatal 错误 `[F6-FATAL] LevelConfigSO not found in scene — BeatClock start blocked` 并中止启动序列。

7. **Animancer 攻击层边界合约（ADR-D5 PlayController 模型）**
   采用 `HybridAnimancerComponent`（非普通 `AnimancerComponent`）与 TDE Animator Controller 共存：
   - **无攻击时**：`HybridAnimancerComponent` 的 `ControllerState`（`KeepChildrenConnected = true`）使 TDE 的 Animator Controller 完整驱动角色输出（移动、冲刺、死亡、复活）。Animancer 不干预。
   - **攻击开始时**：C2 `ComboPlayer` 调用 `AnimancerAttackPlayer.PlayAttack(attackNode, loadedClip, onComplete)`，Animancer 以当前 BPM 速度播放该 Clip，覆盖 TDE Animator Controller 输出。
   - **攻击结束时**（Clip 完成 OnEnd 或 `StopAttack()` 被调用）：`AnimancerAttackPlayer` 调用 `_animancer.PlayController()`，将控制权归还给 TDE Animator Controller；`onComplete` 回调通知 C2 推进连招图（死亡中断时不触发）。
   - **死亡时**：`AnimancerAttackPlayer` 通过 `MMEventListener<TopDownEngineEvent>` 订阅 `PlayerDeath`，调用 `StopAttack()` → `PlayController()`，确保 TDE 死亡动画立即接管，不存在「幽灵攻击层」。
   - F6 负责在 Character Prefab 上正确配置 `HybridAnimancerComponent` 和 `AnimancerAttackPlayer`，并通过 Inspector 序列化字段暴露引用供 C2 使用。

8. **攻击动画制作 BPM 标准（AuthoredBpm per AttackNodeSO）**
   每个攻击 Clip 以其 `AttackNodeSO.AuthoredBpm`（默认 120 BPM）为制作参照 BPM。运行时速度 = `currentBPM / attackNode.AuthoredBpm`。速度缩放公式见 Formulas 节。

   **运行时零值防护（强制）**：`AnimancerAttackPlayer.PlayAttack()` 首先检查 `attackNode.AuthoredBpm <= 0`；若成立，抛出 `InvalidOperationException` 并中止本次攻击播放，不写入任何 `AnimancerState.Speed`。Odin Validator 仅在 Editor 阶段生效，不覆盖构建产物中未经验证的 SO 资产——故须运行时强制保护，防止 `currentBPM / 0 = Infinity` 产生无声失效。

9. **Root Motion 禁用**
   在 Character Prefab 的 `Animator` 组件上禁用 **Apply Root Motion**（Inspector 取消勾选），或在 `AnimancerAttackPlayer.Awake()` 中通过 `_animancer.Animator.applyRootMotion = false` 一次性设置。TDE 的 `CharacterMovement` 基于 Rigidbody 物理控制位移，Root Motion 会产生位置漂移冲突。

   **Animancer 8.4 优先级说明**：`HybridAnimancerComponent` 遵从 `Animator.applyRootMotion` 属性，无独立的 Root Motion 覆盖开关；在 Inspector 或 `Awake()` 中设置 `Animator.applyRootMotion = false` 即可统一控制。

10. **订阅者生命周期**
    `TDELifecycleAdapter` 在 `OnEnable()` 注册 `MMEventListener<TopDownEngineEvent>` 和 `SceneManager.sceneLoaded`；在 `OnDisable()` 取消注册，防止重复注册。
    **注意**：DontDestroyOnLoad 对象在场景切换时不触发 `OnEnable`/`OnDisable`（因对象未被销毁），因此正常场景切换不存在重复注册风险。但若对该对象调用 `SetActive(false)` 再 `SetActive(true)`，则 `OnEnable`/`OnDisable` 会被触发——须确保此场景下注册/取消注册逻辑的幂等性。

11. **ConfirmDSPStart 三步协议（F2 接口合约，强制）**
    `TDELifecycleAdapter` 在调用 `BeatClock.Play(bgmRef, bpm, timeSig)` 前后，**必须**遵循 F2 GDD 规定的三步协议（详见 `audio-beat-clock.md` Core Rule 12）：
    1. **最低提前量**：`targetDSPTime = AudioSettings.dspTime + 0.5`（500ms 提前量），确保所有平台缓冲配置下均能成功调度。
    2. **乐观预锚定**：调用 `BeatClock.Play()` 后**立即**调用 `BeatClock.ConfirmDSPStart(targetDSPTime)` 作为初始锚点，消除「第一拍已广播而锚点未确认」的竞争窗口。
    3. **实际起始校正（FMOD 路径）**：使用 FMOD `scheduledStartDSPClock / fmodSampleRate` 计算 `actual_start`；若 `|actual_start - targetDSPTime| > 5ms`，调用 `BeatClock.ConfirmDSPStart(actual_start)` 重锚定；偏差 ≤ 5ms 时无需第二次调用。
    **违反此协议将导致移动端 DSP 锚点漂移最高 93ms，超出 FE3 校准范围，使节拍判定系统失效。**

12. **FMOD 异步模式强制要求（F3 接口合约）**
    `TDELifecycleAdapter.Start()` 验证 FMOD RuntimeManager 已初始化后，通过**协程**（非同帧二次读取）额外确认 FMOD 以 `FMOD_INIT_NORMAL`（默认异步）模式运行：
    - `Start()` 读取 `_beatClock.DSPClock.GetDspTime()` 为 `t1`，置 `_isVerifyingFMOD = true`，启动协程 `StartCoroutine(VerifyFMODAsyncMode())`。
    - 协程执行 `yield return null`（等待一帧进入下一 Player Loop），再次读取 `GetDspTime()` 为 `t2`。
    - 若 `t2 - t1 > DSP_DELTA_EPSILON`（`const float DSP_DELTA_EPSILON = 0.0001f`）：验证通过，协程内执行正常启动序列（调用待执行的 `BeatClock.Play()` + `EnableGameplayInput()`），置 `_isVerifyingFMOD = false`。
    - 若 `t2 - t1 <= DSP_DELTA_EPSILON`：记录 fatal 错误 `[F6-FATAL] FMOD running in sync mode — BeatClock start blocked`，不调用 `Play()`，游戏保持冻结，置 `_isVerifyingFMOD = false`。

    > `DSP_DELTA_EPSILON = 0.0001f` 防止高帧率（144Hz PC）或低帧率（移动初始帧）设备上 DSP 步进量接近零导致的伪阳性误判。

    **IDSPClock 访问路径**：通过 `_beatClock.DSPClock`（BeatClock 暴露的只读属性）获取，无需额外 Inspector 序列化字段。

    **防重入保护（BeatClock.Play() 双调用竞态）**：`_isVerifyingFMOD` 标志保护协程执行区间——`OnSceneLoaded(Single)` 在 `_isVerifyingFMOD == true` 时**将启动参数缓存到 `_pendingSceneStart`，不执行 `BeatClock.Play()`**；协程验证完成（无论通过或失败）后清除标志；若通过，协程负责处理 `_pendingSceneStart` 中的缓存参数。此机制防止快速场景重载导致 `BeatClock.Play()` 被双调用。

    同步模式下 F3 的 DSP 时间戳将在无 `RuntimeManager.Update()` 的帧内停滞，C1 节拍判定将产生系统性误差。

13. **F5 初始化 gate + `LatencyOffsetMs` boot-inject（ADR-D10 D-4/D-5）**
    `TDELifecycleAdapter` 通过 `ISaveService` seam（生产为 `SaveService.Instance`，测试注入 `MockSaveService`，见 `save-persistence.md` Rule 15）消费 F5，承担启动期延迟偏移的 boot-load + boot-inject 职责：
    - **启动顺序（单一显式 bootstrap driver）**：`SaveServiceBootstrap.Start()` 调用 `saveService.Initialize()`（同步阻塞，D-4）须先于 `TDELifecycleAdapter.Start()` 执行——由**单一显式 bootstrap driver** 保证顺序，**不依赖** Script Execution Order。
    - **release-active 初始化 gate**：`TDELifecycleAdapter.Start()` 开头校验 `if (!_saveService.IsInitialized)` → 记录 fatal `[F6-FATAL] SaveService not initialized before TDELifecycleAdapter.Start()`、置 `enabled = false`、`return`（场景冻结）。此为 **release-active runtime guard，非 `Debug.Assert`**（后者在发布构建被剥离），必须在 shipped build 中响亮失败。
    - **boot-inject（首次 `Play()` 前，一次性）**：首个 `OnSceneLoaded(LoadSceneMode.Single)` 内、进入 Rule 11/Rule 12 的 `Play()` 路径**之前**，以 `_offsetInjected` 一次性 guard 保护执行 `var cal = _saveService.LoadLatencyCalibration(); _beatClock.LatencyOffsetMs = cal.latencyOffsetMs; _offsetInjected = true;`（`isCalibrated=false` 时注入默认 0ms）。重入的 `OnSceneLoaded(Single)`（复活/换关）跳过注入，**不覆盖运行期 FE3 写入**。由于 `Play()` 由 Rule 12 的 FMOD 异步验证协程执行（或缓存至 `_pendingSceneStart`），注入须置于该协程/缓存分支之前，保证顺序「boot-inject → 首个 `Play()` → 首个 `OnBeatExact` → `EnableGameplayInput()`」（Play()/输入仍在 `OnSceneLoaded(Single)`，遵 ADR-D1，未迁至 `Start()`）。
    - **BeatClock 是纯持有者**：F2 不自 load（见 `audio-beat-clock.md` Rule 7/Rule 10、registry forbidden pattern `f2_self_load_latency_from_f5`）；运行期 `LatencyOffsetMs` 唯一写者为 FE3。
    - 验收见 **AC-F6-23**。

14. **音频设备断开降级承接（`OnAudioUnavailable` 订阅，SC-1）**
    `TDELifecycleAdapter` 在 `OnEnable()` 额外订阅 F2 的 **`OnAudioUnavailable`** 事件（音频设备断开 / BGM 不可用时由 F2 广播），作为降级协调器承接 F2 降级序列「1 帧内暂停游戏」的要求——F2 自身仅有权停拍（`BeatClock` 内部 Paused），**无权暂停 TDE 或禁用 F3**（那是本适配器的独家职责，见 Rule 3/Rule 4）。收到事件后，`TDELifecycleAdapter` 在**同一 Update 帧内**原子执行：
    - **`GameManager.Pause()`**（或等效 TDE 暂停 API，经 TDE 事件系统触发，不修改 TDE 源码）——冻结 TDE 移动/物理，玩家无法再移动。
    - **`InputEventBus.DisableAllInput()`**——切断 F3 攻击/输入通道，防止 C1 收到无拍点参照的攻击输入被静默丢弃。

    此举消除「F2 停拍但 TDE 仍可移动、F3 仍 GameplayActive、攻击照发」的半暂停半运行战场，贯彻「无音频不惩罚玩家」意图。

    **恢复对称路径**：若 F2 在设备重连时广播 `OnAudioRestored`（恢复事件的最终命名以 F2 GDD 定义为准；BC-6 已定义的 `OnBeatClockResumed` 可复用于时钟重启信号），`TDELifecycleAdapter` 同帧对称执行 **① `BeatClock.Resume()`（从暂停位置续播，节拍参照先恢复）→ ② `GameManager.UnPause()` → ③ `InputEventBus.EnableGameplayInput()`**，恢复时钟、TDE 运行与输入通道。**`BeatClock.Resume()` 由本适配器独家调用（Rule 4，BeatClock 生命周期唯一调用者），F2 仅广播 `OnAudioRestored` 信号、自身不 Resume**（W-1 裁定，与 `audio-beat-clock.md` Core Rule 9/降级 Edge Case 对称）。恢复顺序遵 EC-05 约束：节拍参照（BeatClock）先于输入通道开放，避免玩家在时钟未就绪时触发攻击导致判定窗口计算错误。

    此降级/恢复响应独立于 `TopDownEngineEvent(Pause/UnPause)`（玩家主动暂停走 UIActive 路径），语义为「无音频不可玩，全局冻结」。验收见 **AC-F6-24**。

15. **统一 run-end 边界域事件桥接（C1/C4 承接，SC-2/SC-3；ADR-D13）**
    **run-end 边界统一（ADR-D13，取代原「仅死亡」边界）**：死亡与胜利/通关**同 class 终结一局**（game-concept.md:164）。F6 以**单一 run-end 出口**同帧停时钟、广播**单一域事件 `OnRunEnded(RunOutcome outcome)`**（`outcome ∈ {Death, Victory}`），下游 C1/C4/FE5 单点订阅，不再按 outcome 分叉两套逻辑。`RunOutcome` 为**纯 C# `enum`（CombatCore 可见，不含任何 TDE 类型）**。**ADR-D13 落地方式 (a)**：`OnRunEnded(Death)` **取代**原 `OnPlayerDeath`（后者已废弃，不保留别名）。
    `TDELifecycleAdapter` 通过 `MMEventListener<TopDownEngineEvent>` 消费 TDE 的死亡事件（`TopDownEngineEvent(PlayerDeath)`）与复活事件（`RespawnComplete`，见状态表）。但 **CombatCore 程序集**（C1 节奏核心所在）受 TDE 边界合约约束 **禁止 `using` TopDownEngine 命名空间**，无法直接订阅 TDE 事件类型。故 F6 承担桥接职责：在**同一 Update 帧内**将 run-end 信号重广播为 CombatCore 可见的域事件 `OnRunEnded(outcome)`（`TDELifecycleAdapter` 上的纯 C# `event`，负载**不含任何 TDE 类型**），供 C1/C4 订阅（`rhythm-core.md` E10 / SC-2、`combat-director.md` Core Rule 12 / SC-3）：
    - **单一 run-end 入口 + 一次性闭锁（ADR-D13 D13-5，TD 条件 C1）**：F6 暴露**单一 run-end 入口方法** `EndRun(RunOutcome outcome)`；D13-1 三步序列（`BeatClock.Stop()` → `InputEventBus.DisableAllInput()` → 广播 `OnRunEnded(outcome)`）全部在此方法内、同一 Update 帧执行。**两条触发路径**——死亡（TDE `PlayerDeath` 桥接）与胜利（见下）——**均汇入 `EndRun()`**，不各自直呼三步。方法内置**每局一次性闭锁** `_runEnded`：首次进入 latch 并执行三步；同局重复触发（无论来自哪个源）**早退 no-op**，保证「恰一次 `OnRunEnded` / 单次 `Stop()`」在多触发源下成立。闭锁随新局 `LoadSceneMode.Single` 重建实例天然复位。
    - **`Death` 分支（`PlayerDeath` 桥接，局终，ADR-D12）**：F6 收 TDE `PlayerDeath` → `EndRun(RunOutcome.Death)` → 同帧 `Stop()` + `DisableAllInput()` + 广播 `OnRunEnded(Death)`。**C1** 订阅后清空 `_pendingInput`、置 `_lateWindowActive = false`、停止处理 `OnHalfBeatLookahead`（SC-2）。**C4**（combat-director）亦订阅，同帧取消全部 `Pending`/`Granted` 令牌（SC-3，对称 C1；见 `combat-director.md` Core Rule 12 / E16）。MVP 死亡 = 换新局：`Stop()` 归零 `global_beat_index`（F2 Rule 10），局结算由 FE5 承接（订阅 `OnRunEnded(outcome)` → merit 折算 + 落盘），随后新 `LoadSceneMode.Single` 加载走 boot 路径重启时钟（见状态表「换新局」行 / ADR-D12）。
    - **`Victory` 分支（胜利/通关，ADR-D13 D13-2；detection defer FE4/FE2/FE5）**：胜利/通关的**判定与发起**属 FE4（Boss 击败）/ FE2（房间清场）/ FE5，**非** F6。桥接方式：若 TDE 原生以 `TopDownEngineEvent` 派发关卡完成 → F6 以既有 `MMEventListener<TopDownEngineEvent>` 桥接（成员名**须核对引擎参考**，ADR-D13 VR1 实现前硬门，见 `plugins/top-down-engine.md`）；若无原生事件 → 由 FE4/FE5 经 CombatCore 侧**显式调用** `EndRun(RunOutcome.Victory)`（不改 TDE 源码）。任一路径同帧 `Stop()` + `DisableAllInput()` + 广播 `OnRunEnded(Victory)`；下游对称——C1 清缓冲、C4 回收令牌（胜利亦走 `Stop()` 归零 `global_beat_index`，绝对拍令牌同源失锚，见 SC-3）。**double-`Play()` 风险闭合**：胜利 `Stop()` 后新 Single 场景 boot 干净起局（ADR-D13 D13-3）。F6 **不**裁定「何为胜利」的游戏语义（defer game-designer/FE5）。
    - **`RespawnComplete`（MVP 休眠 + 诊断守卫，ADR-D12）**：TDE 原地复活在 MVP **不启用**（经 Inspector 关闭，正常流程不触发）。`OnMMEvent(RespawnComplete)` 若被触发：F6 **不**调用 `BeatClock.Play()` 原地重启，改记 fatal 诊断 `[F6-FATAL] RespawnComplete fired but in-place respawn is disabled in MVP` 并返回。`OnRespawnComplete` 域事件桥接**定义保留**（不删，供 Post-MVP checkpoint 复用），但 MVP 路径不广播原地重启语义。死亡恢复由「换新局」boot 路径承接，C1/C4 的 run-end 域清空已足以保证新局干净起点（跨 run-end 边界防幽灵评级 / 令牌回收）。
    - **边界隔离**：C1/C4 只依赖 F6 的域事件抽象（`OnRunEnded(RunOutcome)`，纯 C# 负载），不感知 TDE——等同 F6 桥接 `InputEventBus` 的既有模式（Rule 3/Rule 4）。此为**跨系统场景 G（run-end 幽灵评级 / 令牌孤儿）**的 F6 承接侧合约，与 C1 `rhythm-core.md` 双向合约「F6 ↔ C1（SC-2 run-end 清空）」、C4 SC-3 对称。验收见 **AC-F6-25**。

---

### States and Transitions

F6 自身不维护内部状态机，但其事件响应决定了系统的全局操作阶段：

| 操作阶段 | 触发条件 | InputEventBus 状态 | BeatClock 状态 |
|---------|---------|------------------|--------------|
| **未初始化** | 应用启动 / 首次场景加载前 | AllDisabled | Stopped |
| **场景切换中** | SceneManager 加载 Single 场景（进行中） | AllDisabled | Stopped |
| **Gameplay Active** | `OnSceneLoaded(Single)` 完成 | GameplayActive | Playing |
| **已暂停** | `TopDownEngineEvent(Pause)` | UIActive | Paused |
| **玩家死亡（run-end / Death）** | `TopDownEngineEvent(PlayerDeath)`（MVP `lives=1`，无同局复活）→ `EndRun(Death)` | AllDisabled | Stopped（局终；等待 FE5 结算 + 换新局） |
| **胜利/通关（run-end / Victory，ADR-D13）** | FE4 Boss 击败 / FE2 房间清场 / FE5 显式发起 → `EndRun(Victory)`（TDE 原生关卡完成事件桥接 **或** FE 显式调用；成员名待 VR1 核对） | AllDisabled | Stopped（局终；等待 FE5 结算 + 换新局；对称 Death） |
| **换新局（Roguelike 局重启）** | 新 `LoadSceneMode.Single` 加载（boot 路径，ADR-D1/D10/D12） | GameplayActive | Playing（新局；`global_beat_index` 归零） |
| **复活完成（MVP 不启用）** | `TopDownEngineEvent(RespawnComplete)` — TDE 原地复活在 MVP 经 Inspector 关闭；正常流程不触发。若触发：记 fatal 诊断 `[F6-FATAL] RespawnComplete fired but in-place respawn is disabled in MVP`，**不**原地重启时钟（见 ADR-D12） | 不变 | 不变 |
| **音频降级冻结（SC-1）** | F2 `OnAudioUnavailable` | AllDisabled（`DisableAllInput()`） | Paused（F2 自身停拍，F6 同帧 `GameManager.Pause()`） |
| **音频恢复（SC-1）** | F2 `OnAudioRestored` / `OnBeatClockResumed` | GameplayActive | Playing（`BeatClock.Resume()` → `GameManager.UnPause()`，节拍先于输入；Resume owner=F6，W-1） |

阶段转换的**原子性保证**：每次 `TDELifecycleAdapter` 响应 run-end 信号时，经**单一入口 `EndRun(RunOutcome outcome)`**（ADR-D13 D13-5，含每局一次性闭锁 `_runEnded`），BeatClock 调用与 InputEventBus 调用在**同一 Update 帧内**完成，消除半途不一致状态。run-end 帧内还**同帧广播** CombatCore 域事件 `OnRunEnded(outcome)` 供 C1（SC-2）与 C4（SC-3）承接（防幽灵评级 / 令牌孤儿，见 Rule 15）。**Death（`PlayerDeath` 桥接）与 Victory（FE4/FE2/FE5 发起）两条触发路径均汇入 `EndRun()`，闭锁保证「恰一次 `OnRunEnded` / 单次 `Stop()`」。** **`RespawnComplete`（MVP 休眠，ADR-D12）**：MVP TDE 原地复活不启用；`RespawnComplete` 若触发走 fatal 诊断守卫、**不重广播 `OnRespawnComplete`**（该桥接定义保留供 Post-MVP checkpoint 复用；MVP 死亡恢复走换新局 boot 路径，见 Rule 15 / AC-F6-11）。

---

### Interactions with Other Systems

| 方向 | 系统 | 数据内容 | 接口类型 |
|------|------|---------|---------|
| TDE **→** F6 | TopDownEngine | `TopDownEngineEvent`（Pause/UnPause/PlayerDeath/RespawnComplete） | `MMEventListener<TopDownEngineEvent>` 事件订阅 |
| F2 **→** F6 (SC-1) | BeatClock | `OnAudioUnavailable`（设备断开降级信号）；`OnAudioRestored` / `OnBeatClockResumed`（恢复信号） | 事件订阅；F6 承接降级序列，同帧 `GameManager.Pause()` + `InputEventBus.DisableAllInput()`（恢复时对称 UnPause + `EnableGameplayInput()`） |
| F6 **→** F2 | BeatClock | `Play(bgmRef, bpm, timeSig)` / `Stop()` / `Pause()` / `Resume()` | 直接方法调用（独家控制） |
| F6 **→** F3 | InputEventBus | `EnableGameplayInput()` / `EnableUIInput()` / `DisableAllInput()` | 直接方法调用 |
| F6 **→** C1 (SC-2) | 节奏量化攻击系统 | `OnRunEnded(RunOutcome outcome)`（**Death 与 Victory 均触发**，ADR-D13，取代原 `OnPlayerDeath`）/ `OnRespawnComplete`（**MVP 休眠**——原地复活不启用、不广播，Post-MVP 预留，见 Rule 15 / ADR-D12）（CombatCore 可见域事件，桥接/发起，负载不含 TDE 类型；C1 据此 run-end 时清空过期 `_pendingInput`/评级状态防幽灵评级） | 事件订阅（纯 C# event） |
| F6 **→** C4 (SC-3) | 战斗导演系统 | `OnRunEnded(outcome)`（同一 CombatCore 可见域事件，Death 与 Victory 均触发；C4 据此同帧取消全部 `Pending`/`Granted` 令牌，防绝对拍令牌跨 `Stop()` 失锚 → 幽灵攻击） | 事件订阅（纯 C# event，对称 C1 SC-2） |
| FE4/FE2/FE5 **→** F6 (ADR-D13 D13-2) | 胜利/通关发起方 | 胜利信号 → F6 `EndRun(RunOutcome.Victory)`（TDE 原生关卡完成事件桥接 **或** FE 侧显式调用；成员名待 VR1 核对） | `MMEventListener<TopDownEngineEvent>` 桥接 或 CombatCore 侧显式方法调用 |
| F4 **→** F6 | LevelConfigSO | `Bpm`、`TimeSignature`、`BgmFilePath`（每场景注入） | Inspector 序列化字段 |
| C2 **→** F6 | 连招图系统 | 调用 `AnimancerAttackPlayer.PlayAttack(attackNode, clip, onComplete)` 发起攻击；`StopAttack()` 取消；`UpdateBpm(newBpm)` 中途更新速度 | 访问 F6 在 Character Prefab 上配置的 `AnimancerAttackPlayer` 组件引用 |
| FE2 **→** F6 | 房间系统 | `BeatClock.RequestBPMChange(newBpm)`（房间级 BPM 切换） | 通过 F2 的 `RequestBPMChange()` API（非重新调用 `Play()`） |
| F6 **（通过 TDE）→** C6/FE2 | Hitbox 判定 / 房间系统 | TDE 原生碰撞检测与 `LevelManager` 场景管理基础（F6 正确配置 TDE 后自动提供） | TDE 内置组件（间接） |
| F6 **（通过 Feel）→** PL2 | VFX / 节奏反馈 | MMFeedbacks/Feel 基础设施由 TDE 捆绑提供；PL2 订阅节奏事件发起反馈序列 | MMFeedbacks 组件（间接） |

---

### BGM 过渡策略（BPM 变化时的音频内容处理）

> **职责说明**：F2 GDD 将「BPM 变化时的 BGM 音频内容过渡」明确委托给 F6 定义。F2 仅负责时钟层的节拍计算切换，不持有、不感知 BGM Clip 的切换。

**过渡模式：小节边界无缝换片（Seamless Measure-Boundary Crossfade）**

当 `BeatClock.RequestBPMChange(newBpm)` / `RequestTimeSignatureChange()` 被接受时（通常由 Boss 阶段切换触发），F6 执行以下序列：

1. **接受时预加载 + 预调度**：`TDELifecycleAdapter` 订阅 F2 的 **`OnMeterChangeAccepted`** 事件（接受时广播，见 `audio-beat-clock.md` Core Rule 14）。接到通知后立即通过 FMOD（ADR-D6）预加载目标 BGM 资产（`LevelConfigSO.BgmFilePath` 或 Boss 阶段 SO 中的变体引用）。**关键**：该事件在接受时（而非小节边界）广播，F6 由此获得从接受到边界的**全部提前量**，满足 `PlayScheduled()` 的 500ms 最低提前量约束——无需再手写 `OnBeatExact` + `beat_index_in_measure == numerator - 1` 的边界轮询判定。
2. **以事件负载的 raw 边界时刻精确对齐**：直接使用 `OnMeterChangeAccepted` 负载中的 `predictedEffectiveDspTimeRaw`（**raw 坐标系，禁止叠加 latency offset**——见 F2 Core Rule 13）作为无缝换片的调度目标时刻。此值在接受时即由 F2-F3 确定性给出，与实际小节边界一致。
3. **无缝换片**：通过 `_beatClock.ScheduleBGMTransition(newBgmRef, predictedEffectiveDspTimeRaw)` 精确对齐执行新 BGM 的起始播放（或 Crossfade）。F2 的时钟参数由 F2 在小节边界自行切换并广播 `OnMeterChanged`（生效时），F6 无需同帧手动更新 BeatClock 时钟——职责回归 F2。

    **接口归属（B3，F2 已定义，W-2c-01 已消解）**：`BeatClock.ScheduleBGMTransition(FMODAudioAssetRef nextBgm, double boundaryDspTimeRaw)` 已在 F2 GDD 权威定义（`audio-beat-clock.md` Formulas §BGM 过渡策略，B3）——**参数顺序为 `(nextBgm, boundaryDspTimeRaw)`**，`boundaryDspTimeRaw` 取自 `OnMeterChangeAccepted` 负载的 `predictedEffectiveDspTimeRaw`（raw 坐标系，禁止叠加 offset，遵 F2 Core Rule 13），`nextBgm` 句柄由 F6 提供。此方法与 `OnMeterChangeAccepted` 订阅路径互斥使用（F6 择一，勿双重调度同一换片）。

**MVP 限制**：MVP 阶段每个关卡 BPM 固定（通过 `LevelConfigSO`），仅 Boss 阶段切换可能触发 BPM 变化。Boss 战 BGM 切换的完整细节（含阶段 SO schema）由 `design/gdd/boss-battle.md`（FE4 GDD）进一步定义；F6 提供基础的换片基础设施，FE4 提供配置数据。

---

## Formulas

### F6-FORMULA-01：攻击动画速度缩放

```
attackAnimSpeed = clamp(currentBPM / authoredBpm, SPEED_MIN, SPEED_MAX)
```

**变量定义**

| 变量 | 类型 | 范围 | 来源 | 说明 |
|------|------|------|------|------|
| `currentBPM` | float | [80, 240] | F2 BeatClock（实时读取） | 当前关卡激活 BPM |
| `authoredBpm` | float | [60, 240] | `AttackNodeSO.AuthoredBpm`（默认 120f） | 攻击 Clip 制作参照 BPM，per-node 可配置。详见 ADR-D5 F4 Schema |
| `SPEED_MIN` | float | 0.5（常量） | F6 Constants | 安全限幅下界，防止极低 BPM 导致动画卡顿 |
| `SPEED_MAX` | float | 2.5（常量） | F6 Constants | 安全限幅上界，防止异常 BPM 输入导致动画闪现 |
| **输出 `attackAnimSpeed`** | float | [0.5, 2.5] | — | 写入 `AnimancerState.Speed`，控制攻击 Clip 播放速率 |

**正常游戏运行输出范围**（`authoredBpm = 120`，`currentBPM ∈ [80, 240]`）：`attackAnimSpeed ∈ [0.667, 2.0]`

安全限幅 [0.5, 2.5] 宽于正常数学范围，仅用于拦截超出 `bpm_range` 注册值的异常输入。限幅触发时记录 Warning：`[F6] attackAnimSpeed clamped from {raw:.3f} to {clamped:.3f}`

**实例计算**（以 authoredBpm=120，0.5 秒攻击 Clip 为基准）

| currentBPM | attackAnimSpeed | 0.5s Clip 实际时长 |
|------------|-----------------|-------------------|
| 80  | 0.667×          | 0.750 s           |
| 100 | 0.833×          | 0.600 s           |
| **120** | **1.000×** | **0.500 s（参照）** |
| 160 | 1.333×          | 0.375 s           |
| 180 | 1.500×          | 0.333 s           |
| 200 | 1.667×          | 0.300 s           |
| 240 | 2.000×          | 0.250 s           |

**更新策略（Option A — 实时更新，via ADR-D5 `UpdateBpm()`）**

速度在 **攻击开始时** 由 `AnimancerAttackPlayer.PlayAttack()` 一次性设置（`state.Speed = currentBPM / authoredBpm`）。当 BPM 在攻击动画中途变化（含 Boss 阶段切换时的 `RequestBPMChange()`），C2 通过订阅 F2 的 **`OnMeterChangeAccepted`** 事件（接受时广播，见 `audio-beat-clock.md` Core Rule 14），以负载的 `newBpm` **立即**调用 `AnimancerAttackPlayer.UpdateBpm(newBpm)` 更新速度——此 API 仅更新 `_currentState.Speed`，不重置 normalized time，不中断动画。选用**接受时**（而非生效时 `OnMeterChanged`）事件，是为实现「骤跳瞬间这一刀即变凌厉」的预告体感（见 EC-03），使动作在玩家听到 BGM 换片前即先行响应。

⚠ **ADR-D5 对齐说明**：ADR-D5 当前描述「MVP 中不在攻击中途更新速度」；本 GDD 选择 Option A（中途更新），须向 ADR-D5 提交修订，明确 `UpdateBpm()` 方法在 MVP 路径中亦被调用。在修订完成前，`UpdateBpm()` 已在 ADR-D5 中定义为合法 API，使用不违反接口合约。

**Option B 定义（备选，未选用）**：`UpdateBpm()` 仅在 `_currentState == null || !_currentState.IsPlaying`（即非攻击进行中）时生效——攻击中途不更新速度，当前攻击以制作时的授权 BPM 比例完成，下一次 `PlayAttack()` 调用时使用新 BPM。若 AC-F6-20 盲测结果显示 Option A 的实时变速对玩家产生负面体感，回退 Option B 的实现成本为在 `UpdateBpm()` 方法体增加一行 `if` 条件（极低）。

---

## Edge Cases

### EC-01：场景加载期间 FMOD 未初始化

**情境**：`TDELifecycleAdapter.Start()` 在 `FMODUnity.RuntimeManager` 完成初始化之前被调用。

**行为**：验证失败 → 记录 fatal 错误 `[F6-FATAL] FMOD RuntimeManager not initialized — BeatClock start blocked` → 不调用 `BeatClock.Play()` → 场景冻结在静止状态（InputEventBus 保持 `AllDisabled`）。不自动重试，需玩家重启关卡。

**为什么不在 `Awake()` 检查**：FMOD `RuntimeManager` 脚本执行顺序未保证，`Awake()` 时可能尚未就绪。`Start()` 在所有 `Awake()` 完成后执行，是最早的可靠检查点。

---

### EC-02：Additive 场景加载（TDE 房间拼接）

**情境**：TDE `LevelManager` 以 `LoadSceneMode.Additive` 加载房间子场景。

**行为**：`TDELifecycleAdapter.OnSceneLoaded()` 检测到 `LoadSceneMode.Additive` → 直接返回，不重新调用 `BeatClock.Play()` → BeatClock 继续运行，节奏不中断。房间内 BPM 变化通过 `BeatClock.RequestBPMChange(newBpm)` 处理（**不**重新调用 `Play()`）。

---

### EC-03：Boss 阶段切换时的 BPM 骤跳（攻击动画中途）

**情境**：玩家正在执行攻击动画（`AnimancerAttackPlayer` 正在播放攻击 Clip），Boss 阶段切换触发 `RequestBPMChange(120 → 200)`。

**行为（Option A 实时更新）**：C2 订阅 BeatClock 的 BPM 变化事件，收到通知后立即调用 `AnimancerAttackPlayer.UpdateBpm(200)`——内部将 `_currentState.Speed = 200 / attackNode.AuthoredBpm = 1.667`，不重置 normalized time。当前攻击动画加速播放，不等待完成。玩家感知：这一刀在节奏加快的瞬间变得更凌厉。

**注意**：动画剩余时长缩短，C2 必须能处理「动画比预期提前完成」的情况。这是 C2 的责任，F6 不补偿。

**⚠ UpdateBpm() + OnEnd 同帧竞争警告**：若 BPM 骤跳事件恰好在攻击 `NormalizedTime > 0.9` 时到达，`UpdateBpm()` 与 `AnimancerState.Events.OnEnd` 可能在**同一帧**触发。此时 C2 的 BPM 事件处理逻辑和 `onComplete` 回调均可能在同帧运行，导致连招图推进信号被同帧发送两次。

**F6 的边界**：F6 不防重入——`AnimancerAttackPlayer.UpdateBpm()` 在 `OnEnd` 已触发后若 `_currentState == null || !_currentState.IsPlaying`（`OnAnimationEnd()` 已完成处理）则空操作，不产生第二次 `onComplete`。注意：ADR-D5 确认 `OnAnimationEnd()` 不将 `_currentState` 置为 null，实际守卫条件以 `!_currentState.IsPlaying` 为主；`_currentState == null` 是初始化未完成的额外兜底保护。

**C2 的强制接口合约**：C2 须保证 `onComplete` 的连招图推进逻辑**幂等**——同帧收到两次相同推进信号等价于收到一次。推荐实现：C2 以 `_lastAdvanceFrame == Time.frameCount` 门控（见 C2 GDD 接口合约节）。此合约须在 C2 GDD 编写时明确接受（B3 项）。

---

### EC-04：死亡事件与攻击动画的竞争

**情境**：玩家在攻击动画执行中途（`AnimancerAttackPlayer` 正在播放攻击 Clip）死亡，`TopDownEngineEvent(PlayerDeath)` 到达。

**行为**：两个独立监听者同帧响应 `PlayerDeath`：
- `TDELifecycleAdapter`：调用 `InputEventBus.DisableAllInput()` + `BeatClock.Stop()`。
- `AnimancerAttackPlayer`（自身 `MMEventListener<TopDownEngineEvent>`）：调用 `StopAttack()` → `_animancer.PlayController()`，攻击 Clip 立即停止，TDE Animator Controller 接管；`onComplete` 回调不触发（`_pendingOnComplete = null`）——C2 无需订阅 `PlayerDeath` 以清理动画状态。

**执行顺序**：两个监听者均在同一帧响应，执行顺序不保证（均为 `MMEventListener` 注册顺序依赖）。两者职责独立：一方停止输入与音乐，另一方停止攻击动画。须通过集成测试（AC-F6-16）验证不存在中间帧死亡动画被攻击层遮盖的情况。

> **胜利/通关对称（ADR-D13）**：`Victory` 分支经 `EndRun(RunOutcome.Victory)` 终结一局，同样 `DisableAllInput()` + `BeatClock.Stop()`。但攻击动画停止此前依赖 `AnimancerAttackPlayer` 自身对 TDE `PlayerDeath` 的监听——**胜利路径不派发 `PlayerDeath`**，故攻击 Clip 不会经该监听自动停止。实现时须由 `EndRun()` 内联调用 `AnimancerAttackPlayer.StopAttack()`（或让 `AnimancerAttackPlayer` 亦订阅 run-end 域信号），保证胜利与死亡的攻击动画收尾对称。此为实现细节，随 D13-2 胜利桥接落地时确认（不改变本 EC 的 Death 竞争语义）。

---

### EC-05：复活时 BeatClock 重启与输入通道竞争（MVP 不启用 — TDE 原地复活关闭，ADR-D12）

> **MVP 处置**：TDE 原地复活在 MVP **不启用**（`RespawnComplete → Play()` 原地重启降级为休眠诊断守卫，见 Rule 15）。死亡恢复走「换新局」路径——新 `LoadSceneMode.Single` 加载经 boot 路径 `OnSceneLoaded(Single) → Play() → EnableGameplayInput()`（ADR-D1/D10），本 EC 描述的「节拍先于输入」顺序约束在 boot 路径已由 ADR-D10 保证，逻辑不丢。以下原地复活序列**仅为 Post-MVP checkpoint 复活预留**。

**情境（Post-MVP 预留）**：`TopDownEngineEvent(RespawnComplete)` 到达，`TDELifecycleAdapter` 需重启 BeatClock 并开放输入。

**行为**：原子序列（同一 Update 帧内）：
1. `BeatClock.Play(bgmRef, bpm, timeSig)`（从新场景 `LevelConfigSO` 读取参数）
2. `InputEventBus.EnableGameplayInput()`

**顺序约束**：BeatClock 必须先于 InputEventBus 开放，确保玩家第一次输入时节拍参照已存在。若顺序颠倒，玩家可能在 BeatClock 未启动时触发攻击，导致判定窗口计算错误。

---

### EC-06：`attackAnimSpeed` 安全限幅触发

**情境**：上游（F2 或 Boss 系统）传入 `currentBPM < 80` 或 `currentBPM > 240`（超出 `bpm_range` 注册范围）。

**行为**：公式计算结果超出 [0.5, 2.5] → `Mathf.Clamp` 截断 → 记录 Warning `[F6] attackAnimSpeed clamped from {raw:.3f} to {clamped:.3f}` → 使用限幅后的值。游戏继续运行，不中断。

---

### EC-07：DontDestroyOnLoad 单例跨场景重复注册

**情境**：`TDELifecycleAdapter` 所在的 Manager GameObject 是 `DontDestroyOnLoad` 的，跨场景切换时 `OnSceneLoaded` 事件会被多次调用。

**行为**：`TDELifecycleAdapter` 在 `OnEnable()` 注册 `MMEventListener<TopDownEngineEvent>` 和 `SceneManager.sceneLoaded`；在 `OnDisable()` 取消注册。由于 `DontDestroyOnLoad` 对象不会被销毁，`OnEnable/OnDisable` 不会在场景切换时触发——因此无重复注册风险。测试验证：5 次连续场景切换后，`OnSceneLoaded` 回调次数与场景切换次数一致，不累积。

---

## Dependencies

### F6 依赖的系统（上游）

| 系统 | 系统 ID | 依赖内容 | F6 使用方式 |
|------|---------|---------|------------|
| BeatClock | F2 | `Play(bgmPath, bpm, timeSig)` / `Stop()` / `Pause()` / `Resume()` / `RequestBPMChange()` / `OnBeatExact` 事件；**`OnAudioUnavailable`（SC-1 降级信号）** + `OnAudioRestored`/`OnBeatClockResumed`（恢复信号） | `TDELifecycleAdapter` 独家控制生命周期；C2 通过 `OnBeatExact` 读取 `currentBPM` 更新 `attackAnimSpeed`；**订阅 `OnAudioUnavailable` 同帧 `GameManager.Pause()` + `DisableAllInput()` 承接 F2 降级（SC-1）** |
| InputEventBus | F3 | `EnableGameplayInput()` / `EnableUIInput()` / `DisableAllInput()` | `TDELifecycleAdapter` 在生命周期事件后调用，控制输入通道开关 |
| LevelConfigSO 数据层 | F4 | `LevelConfigSO.Bpm` / `TimeSignature` / `BgmFilePath` | `TDELifecycleAdapter` 在 `OnSceneLoaded(Single)` 时读取，注入 `BeatClock.Play()` |
| TopDownEngine | 外部插件 | `InputSystemManagerEventsBased` / `TopDownEngineEvent` / `MMEventListener<T>` / TDE Animator Controller 基础设施 | `JadeRabbitInputManager` 继承 TDE 输入类；`TDELifecycleAdapter` 订阅 TDE 事件总线 |
| Animancer | 外部插件 | `HybridAnimancerComponent` / `AnimancerState.Speed` / `PlayController()` | F6 在角色 Prefab 上配置 `HybridAnimancerComponent` 和 `AnimancerAttackPlayer`，暴露 `AnimancerAttackPlayer` 引用供 C2 调用 |
| FMOD | 外部插件 | `FMODUnity.RuntimeManager`（初始化状态验证） | `TDELifecycleAdapter.Start()` 验证 FMOD 已初始化后才允许 `BeatClock.Play()` |
| SaveService | F5 | `ISaveService.Initialize()` / `IsInitialized` / `LoadLatencyCalibration()` | 启动期消费（ADR-D10 D-4/D-5）：`Start()` 校验 `IsInitialized`（release-active gate），首个 `OnSceneLoaded(Single)` 内一次性 boot-load `LatencyCalibrationDTO` 并注入 `BeatClock.LatencyOffsetMs`；生产为 `SaveService.Instance`，测试注入 `MockSaveService` |

### 依赖 F6 的系统（下游）

| 系统 | 系统 ID | 期望 F6 提供的内容 | F6 提供方式 |
|------|---------|-----------------|------------|
| CombatCore / 连招图系统 | C2 | `AnimancerAttackPlayer` 引用（`PlayAttack(node, clip, onComplete)` / `StopAttack()` / `UpdateBpm(newBpm)`）；F6-FORMULA-01 公式定义（`authoredBpm` 来自 `AttackNodeSO`） | F6 在 Prefab 上配置 `HybridAnimancerComponent` 和 `AnimancerAttackPlayer` 并通过序列化字段暴露引用；公式在本 GDD Formulas 节中定义 |
| CombatCore / 战斗导演系统 | C4 | `OnRunEnded(RunOutcome outcome)` 域事件（SC-3 run-end 令牌回收，Death + Victory，对称 C1 SC-2）；C4 订阅后同帧取消全部令牌 | F6 经单一入口 `EndRun(outcome)` 广播 CombatCore 可见纯 C# `OnRunEnded(outcome)` 事件（Death 桥接 TDE `PlayerDeath` / Victory 由 FE4/FE2/FE5 发起，负载不含 TDE 类型），供 C4 订阅（`combat-director.md` Core Rule 12 / E16 / AC-C4-31） |
| Hitbox 判定系统 | C6 | TDE 原生碰撞检测基础设施（TDE 提供，F6 正确配置后自动生效） | F6 不直接提供；通过正确继承 TDE 后 TDE 内置 |
| 房间系统 | FE2 | BPM 变更通道（`BeatClock.RequestBPMChange()`，非重新 `Play()`） | 通过 F2 的 `RequestBPMChange()` API；F6 文档化此约束，FE2 遵守 |
| 相机系统 | PL1 | TDE `LevelManager` 场景管理（相机跟随依赖 TDE 关卡基础设施） | F6 正确集成 TDE 后自动提供 |
| 暂停菜单 | P5 | `TopDownEngineEvent(Pause/UnPause)` 信号（TDE 发出，F6 响应） | F6 通过 `TDELifecycleAdapter` 同帧暂停 BeatClock + 切换输入到 UIActive；P5 订阅相同 TDE 事件链的后续处理 |
| VFX 节奏反馈 | PL2 | BeatClock `OnBeatExact` 事件（PL2 订阅节奏事件触发反馈序列） | 通过 F2 BeatClock；F6 正确启动 BeatClock 后 PL2 自动可用 |

### 双向依赖确认

> **F2（BeatClock）GDD** 中须记录：F6 `TDELifecycleAdapter` 是 `BeatClock.Play/Stop/Pause/Resume` 的唯一调用者。
>
> **F2（BeatClock）GDD** 中须记录（SC-1）：F2 广播 `OnAudioUnavailable`（及恢复事件 `OnAudioRestored`/`OnBeatClockResumed`）后，由 F6 `TDELifecycleAdapter`（降级协调器）订阅并承接「1 帧内暂停游戏」——F2 自身只停拍，暂停 TDE（`GameManager.Pause()`）与禁用 F3（`DisableAllInput()`）由 F6 独家执行；恢复时对称 `GameManager.UnPause()` + `EnableGameplayInput()`。
>
> **F3（InputEventBus）GDD** 中须记录：F6 `TDELifecycleAdapter` 在 TDE 生命周期事件后调用 InputEventBus 方法。`JadeRabbitInputManager` 通过继承保留 TDE `AbilityInputAuthorization` 管线，但不直接调用 `InputActionMap.Enable()/Disable()`。
>
> **C2（连招图系统）GDD** 中须记录：C2 从 F6 获取 `AnimancerAttackPlayer` 引用；C2 调用 `PlayAttack(node, clip, onComplete)` / `StopAttack()` / `UpdateBpm(newBpm)` 控制攻击动画；C2 使用 F6-FORMULA-01 公式参数（`authoredBpm` 来自 `AttackNodeSO`）；死亡时动画清理由 `AnimancerAttackPlayer` 自行处理（F6 责任），C2 无需订阅 `PlayerDeath` 用于动画状态清零。
>
> **C1（节奏量化攻击系统）GDD** 中须记录（SC-2 反向登记）：F6 `TDELifecycleAdapter` 经单一入口 `EndRun(RunOutcome outcome)` 同帧广播 CombatCore 可见的域事件 `OnRunEnded(outcome)`（**Death 与 Victory 均触发**，ADR-D13，取代原 `OnPlayerDeath`；纯 C# event，负载不含 TDE 类型）；`OnRespawnComplete`（MVP 休眠，桥接 TDE `RespawnComplete`）保留供 Post-MVP。**C1 订阅 `OnRunEnded`**（而非直接订阅 TDE 事件——CombatCore 禁止 `using` TopDownEngine），run-end（死亡/胜利）时清空过期 `_pendingInput` 与评级状态防幽灵评级（`rhythm-core.md` E10 / 双向合约「F6 ↔ C1（SC-2 run-end 清空）」）。见 Rule 15。
>
> **C4（战斗导演系统）GDD** 中须记录（SC-3 反向登记，对称 C1 SC-2）：F6 广播的 `OnRunEnded(outcome)` 域事件（Death 与 Victory 均触发）亦被 **C4** 订阅；C4 收到后在 F6 广播的同一 Update 帧内取消**全部** `Pending`/`Granted` 令牌（死亡/胜利均走 `BeatClock.Stop()` 归零 `global_beat_index`，绝对拍令牌失锚必致幽灵攻击）。C4 同受 CombatCore 边界合约约束（禁止 `using` TopDownEngine，只订阅纯 C# 域事件），且不调用任何 BeatClock 生命周期方法。见 `combat-director.md` Core Rule 12 / E16 / AC-C4-31，及 Rule 15。
>
> **强制幂等性合约（来自 F6 EC-03 B3 项）**：C2 的 `onComplete` 连招图推进逻辑须**幂等**——在同帧收到两次相同 `onComplete` 时，连招图仅前进一步（推荐以 `_lastAdvanceFrame == Time.frameCount` 门控）。此合约因 `UpdateBpm()` + `OnEnd` 同帧竞争场景而强制要求，C2 GDD 接口合约节须显式接受。

---

## Tuning Knobs

| 旋钮名称 | 变量 / 位置 | 默认值 | 安全范围 | 影响的游戏体验方面 |
|---------|------------|-------|---------|-----------------|
| 攻击动画制作 BPM | `AttackNodeSO.AuthoredBpm` | 120 BPM | [60, 240]（per-node） | 每个攻击 Clip 的制作参照 BPM，影响该节点在各 BPM 下的相对速度感。不同武器/连招节点可各自设定。 |
| 速度缩放下界 | `SPEED_MIN`（F6 Constants） | 0.5 | [0.25, 0.75] | 极低 BPM 时攻击动画最慢播放速度；过低可能导致动画长时间卡住 |
| 速度缩放上界 | `SPEED_MAX`（F6 Constants） | 2.5 | [2.0, 4.0] | 安全防护上界，用于拦截异常 BPM 输入。**注意：正常游戏 BPM [80,240] + 默认 AuthoredBpm=120 时，速度上界为 2.0，此旋钮不影响正常游戏体验。** |
| 攻击淡入时长 | `ATTACK_LAYER_BLEND_IN`（AnimancerAttackPlayer Inspector） | 0.05 s | [0.02, 0.15] | 攻击开始时从 TDE Animator 到 Animancer 攻击 Clip 的切换延迟；起点值为技术默认，**需原型测试验证响应感**（参见 AC-F6-19） |
| 攻击淡出时长 | `ATTACK_LAYER_BLEND_OUT`（AnimancerAttackPlayer Inspector） | 0.1 s | [0.05, 0.2] | 攻击完成后 `PlayController()` 切回 TDE Animator 的过渡时长；影响攻击结束后恢复移动动画的流畅度（由 C2 通过 `UpdateBpm()` 间接控制，F6 提供 Inspector 默认值） |

⚠ **安全范围数学约束（AuthoredBpm）**：`authoredBpm ∈ [96, 160]` 是「恰好不触发限幅」的**边界区间**（非中心安全带）——推导：`SPEED_MAX=2.5` → `240/2.5=96`（`authoredBpm < 96` 时 240 BPM 触发上界限幅）；`SPEED_MIN=0.5` → `80/0.5=160`（`authoredBpm > 160` 时 80 BPM 触发下界限幅）。`authoredBpm` 越靠近中心值 120，与游戏 BPM 范围两端的留余越大；使用 60 或 200 等极端值时须检查具体 BPM 组合下的限幅风险。默认值 120 满足此约束且处于中心位置。

**注意**：`SPEED_MIN`、`SPEED_MAX` 定义在 F6 专用 `F6Constants` 类中，以 `static readonly float` 暴露。Blend 时长由 C2/AnimancerAttackPlayer 在 Inspector 中配置，F6 提供合理起点值。

---

## Visual/Audio Requirements

F6 自身不产生视觉或音频输出。集成层的正确工作体现为：

- **Audio**: FMOD `RuntimeManager` 初始化成功 + `TDELifecycleAdapter` 调用 `BeatClock.Play()` → 音乐正常在场景加载后响起（由 F2 通过 FMOD 播放；F6 仅控制启动时机）。
- **Visual**: TDE Animator Controller（Layer 0）与 Animancer Override Layer（Layer 1）的正确叠加 → 移动/空闲/死亡动画与攻击动画按设计分层呈现（由 TDE + Animancer 渲染；F6 配置层结构并暴露引用）。
- **调试可视化（Editor Only）**: `TDELifecycleAdapter` 在 Inspector Custom Editor 中显示当前操作阶段（Gameplay Active / Paused / Dead / Uninitialized）和 `currentBPM`，供开发期状态验证。此调试 UI 仅在 `UNITY_EDITOR` 条件编译块中有效，不打入 Runtime 包体。

---

## UI Requirements

F6 无玩家可见 UI。所有生命周期状态变化通过 TDE 事件通知上层 UI 系统（P5 暂停菜单订阅 `TopDownEngineEvent(Pause)` 后自行处理 UI 展示，不经由 F6）。

开发期 Inspector 调试显示见 Visual/Audio Requirements 节。

---

## Acceptance Criteria

### AC-F6-01 — JadeRabbitInputManager 不直接操作 Action Map

**类型**: Logic | **测试方式**: EditMode 自动化测试（静态分析）+ PlayMode 自动化测试（运行时路径）

**静态验证**：EditMode 测试通过 `System.Reflection` 枚举 `JadeRabbitInputManager` 的所有 `MethodInfo`，获取方法 IL（`MethodBody.GetILAsByteArray()`），断言不存在对 `InputActionMap.Enable()` / `Disable()` 的调用（按方法 token 匹配）；或使用 Roslyn Analyzer 在编译期静态检查（推荐，可加入 CI 管线）。

**运行时路径验证**：PlayMode 测试中注入 `IInputEventBus` mock，触发攻击输入后断言 mock 收到调用（`OnAttackInput` 路径经 F3 代理）；mock 不记录任何 `InputActionMap` 直接操作。

**通过条件**：静态断言无直接 `Enable/Disable` 调用 + 运行时 mock 确认代理路径正确。

---

### AC-F6-02 — JadeRabbitInputManager 保留 TDE AbilityInputAuthorization 管线

**类型**: Integration | **测试方式**: PlayMode 手动验证

触发需要 `AbilityInputAuthorization` 锁的 TDE Ability（如 Dash）；Ability 持续期间攻击 `OnAttackInput` 未触发（输入被 TDE 管线拦截）；Ability 结束后攻击输入经 F3 正常发布；TDE Ability 状态机流程无破坏。

---

### AC-F6-26 — JadeRabbitInputManager 输入代理路径无轮询、无越权直调、单次输入恰 1 次代理

**类型**: Integration | **测试方式**: EditMode（反射/静态分析）+ PlayMode（Mock 代理路径）

反射检查 `JadeRabbitInputManager`（含基类覆写链）不声明 `Update()`/`FixedUpdate()`（事件驱动非轮询，Manifest Guardrail）；PlayMode 注入 `IInputEventBus` mock，单次攻击输入后 mock 收到恰 1 次代理调用（转发计数==1），且无 `InputActionMap.Enable()/Disable()` 直调记录（与 AC-F6-01 静态 IL 断言互补）。

---

### AC-F6-03 — TDELifecycleAdapter 是 BeatClock 生命周期唯一调用者

**类型**: Logic | **测试方式**: EditMode 自动化测试（静态）+ PlayMode 自动化测试（运行时 Spy）

**静态验证**：EditMode 测试通过 Reflection 或 Roslyn Analyzer 枚举全部非 `TDELifecycleAdapter` 类型，断言无对 `IBeatClockLifecycle.Play/Stop/Pause/Resume` 方法的调用。

**运行时 Spy 验证**：注入实现 `IBeatClockLifecycle` 的 mock BeatClock，mock 内部记录每次 `Play/Stop/Pause/Resume` 的调用来源类型（`new System.Diagnostics.StackTrace().GetFrame(1).GetMethod().DeclaringType`）；运行完整生命周期序列（加载场景 → 暂停 → 恢复 → 死亡 → 复活）后，断言所有调用来源均为 `TDELifecycleAdapter`。

**通过条件**：静态分析无越权调用 + 运行时 Spy 全部来源为 `TDELifecycleAdapter`。

---

### AC-F6-04 — FMOD 未初始化时 BeatClock 被阻止启动

**类型**: Logic | **测试方式**: PlayMode 自动化测试

模拟 `FMODUnity.RuntimeManager` 未初始化，以 `LoadSceneMode.Single` 加载场景：`BeatClock.Play()` 调用次数 == 0；记录 `[F6-FATAL] FMOD RuntimeManager not initialized — BeatClock start blocked` 错误日志；InputEventBus 状态为 `AllDisabled`。

---

### AC-F6-05 — FMOD 验证在 Start() 执行，而非 Awake()

**类型**: Logic | **测试方式**: PlayMode 自动化测试

通过生命周期阶段 Spy 验证：FMOD 初始化查询首次发生在 `Start()` 阶段（所有 `Awake()` 完成之后）；Awake 阶段无任何 FMOD 查询调用记录；BeatClock 不在 `Awake()` 中被启动。

---

### AC-F6-06 — Single 场景加载后进入 Gameplay Active 状态

**类型**: Integration | **测试方式**: PlayMode 自动化测试

以 `LoadSceneMode.Single` 加载含有效 `LevelConfigSO`（BPM=120，TimeSig=4/4）的场景后：`BeatClock.Play()` 调用恰好 1 次且传入参数与 SO 一致；`InputEventBus.EnableGameplayInput()` 调用恰好 1 次；BeatClock 状态 `Playing`，InputEventBus 状态 `GameplayActive`。

---

### AC-F6-07 — Additive 场景加载不触发 BeatClock 重启

**类型**: Logic | **测试方式**: PlayMode 自动化测试

BeatClock 处于 `Playing`（currentBPM=120）期间以 `LoadSceneMode.Additive` 加载房间子场景后：`BeatClock.Play()` 新增调用次数 == 0；BeatClock 继续 Playing 且 currentBPM 未变化；InputEventBus 状态未变化。

---

### AC-F6-08 — Pause 事件同帧暂停 BeatClock 并切换输入至 UIActive

**类型**: Logic | **测试方式**: PlayMode 自动化测试

从 Gameplay Active 状态发布 `TopDownEngineEvent(Pause)` 后：`BeatClock.Pause()` 与 `InputEventBus.EnableUIInput()` 记录的 `Time.frameCount` 值相同（同一 Update 帧内，单主线程保证）；两个方法各调用恰好 1 次；BeatClock 状态 `Paused`，InputEventBus 状态 `UIActive`。

---

### AC-F6-09 — UnPause 事件同帧恢复 BeatClock 并切换输入至 GameplayActive

**类型**: Logic | **测试方式**: PlayMode 自动化测试

从已暂停状态发布 `TopDownEngineEvent(UnPause)` 后：`BeatClock.Resume()` 与 `InputEventBus.EnableGameplayInput()` 记录的 `Time.frameCount` 值相同（同一 Update 帧内，单主线程保证）；两个方法各调用恰好 1 次；BeatClock 状态 `Playing`，InputEventBus 状态 `GameplayActive`。

---

### AC-F6-29 — Pause/Resume 原子性与幂等性形式化断言

**类型**: Logic | **测试方式**: PlayMode

① Pause 分支内 `BeatClock.Pause()` 与 `InputEventBus` 切换记录同一 `Time.frameCount`（同一回调栈内，补 AC-F6-08 同帧性）；② 已 `Paused` 时再收 Pause，`BeatClock.Pause()` 累计==1（幂等）；③ `Pause→UnPause→Pause` 后 BeatClock 与 InputEventBus 状态一致（无撕裂）。

---

### AC-F6-10 — run-end 信号同帧停止 BeatClock 并禁用所有输入（Death + Victory，ADR-D13）

**类型**: Logic | **测试方式**: PlayMode 自动化测试

**Death 子用例**：从 Gameplay Active 状态发布 `TopDownEngineEvent(PlayerDeath)`（经 `EndRun(RunOutcome.Death)`）后：`BeatClock.Stop()` 与 `InputEventBus.DisableAllInput()` 记录的 `Time.frameCount` 值相同（同一 Update 帧内，单主线程保证），各调用恰好 1 次；run-end 后输入通道不响应攻击指令；BeatClock 状态 `Stopped`，InputEventBus 状态 `AllDisabled`；广播 `OnRunEnded(Death)` 恰 1 次。

**Victory 子用例（ADR-D13）**：从 Gameplay Active 状态经 `EndRun(RunOutcome.Victory)`（模拟 FE4/FE2/FE5 胜利信号或 TDE 关卡完成事件桥接）后：断言与 Death 子用例对称——`Stop()` + `DisableAllInput()` 同帧各 1 次、`Stopped` + `AllDisabled`、广播 `OnRunEnded(Victory)` 恰 1 次。

**多触发源闭锁子用例（D13-5）**：同一局内连续两次触发 run-end（如 TDE `PlayerDeath` 桥接 + FE4 显式 `EndRun(Victory)`，或同源二次到达）→ 断言 `BeatClock.Stop()` 新增调用 == 1（非 2）、`OnRunEnded` 广播恰 1 次（一次性闭锁 `_runEnded` 早退第二次）。

---

### AC-F6-11 — RespawnComplete 在 MVP 不原地重启时钟（休眠诊断守卫，ADR-D12）

**类型**: Logic | **测试方式**: PlayMode 自动化测试

> **裁定变更（ADR-D12）**：MVP 死亡 = 换新局，TDE 原地复活不启用。本 AC 从「验证原地重启」改为「验证休眠守卫」。原地重启的时钟-输入顺序约束由换新局的 boot 路径（AC-F6-06）覆盖。

从玩家死亡（`Stopped` + `AllDisabled`）状态注入 `TopDownEngineEvent(RespawnComplete)` 后：
- `BeatClock.Play()` 新增调用次数 == **0**（不原地重启时钟）；
- 记录 fatal 诊断日志 `[F6-FATAL] RespawnComplete fired but in-place respawn is disabled in MVP`；
- BeatClock 状态保持 `Stopped`、InputEventBus 保持 `AllDisabled`（不被原地复活路径改写）。

**通过条件**：`Play()` 未被调用 + fatal 诊断已记录 + 状态未变。换新局的正常时钟重启由 AC-F6-06（Single 加载 boot 路径）覆盖。

---

### AC-F6-12 — Animancer 攻击时接管控制权，OnEnd 后 PlayController() 归还 TDE

**类型**: Integration | **测试方式**: PlayMode 自动化测试（使用 C2 最小 stub，不依赖完整 C2 实现）

触发轻攻击后：`AnimancerAttackPlayer.PlayAttack()` 被调用，`HybridAnimancerComponent` 覆盖 TDE Animator Controller 输出；攻击 Clip 播放完成后 `AnimancerState.Events.OnEnd` 触发 `_animancer.PlayController()`，TDE Animator Controller 恢复完整驱动（下一帧移动/空闲动画正常响应输入）；`onComplete` 回调被触发恰好 1 次，且发生在 `PlayController()` 调用之后；攻击期间 TDE 移动动画被覆盖（无移动 Clip 混入）；完成后 TDE 动画恢复（无攻击 Clip 残留）。

---

### AC-F6-13 — 攻击动画速度缩放公式正确（含 authoredBpm 参数、限幅边界与 Warning 日志）

**类型**: Logic | **测试方式**: EditMode 自动化测试

调用 `F6Constants.CalculateAttackAnimSpeed(currentBPM, authoredBpm)` 断言以下用例（容差 ±0.001）：

| currentBPM | authoredBpm | 期望速度 | Warning 日志 |
|---|---|---|---|
| 120 | 120 | 1.000f | 无 |
| 80 | 120 | 0.667f | 无 |
| 240 | 120 | 2.000f | 无 |
| 120 | 100 | 1.200f | 无 |
| 60 | 120 | 0.500f（下界限幅） | `[F6] attackAnimSpeed clamped from X.XXX to Y.YYY` |
| 300 | 120 | 2.500f（上界限幅） | 同格式 |
| 任意 | **0**（或负值） | **抛出 `InvalidOperationException`，方法中止** | `[F6-FATAL] authoredBpm <= 0 in AttackNodeSO — PlayAttack aborted` |

限幅 Warning 须包含原始值和截断值；非限幅用例断言无 Warning 输出；`authoredBpm <= 0` 用例断言无 `AnimancerState.Speed` 写入且异常被记录。

---

### AC-F6-33 — `F6Constants.CalculateAttackAnimSpeed` 纯函数性断言

**类型**: Logic | **测试方式**: EditMode（含反射/静态分析）

补 AC-F6-13（数值表）/AC-F6-18（集成校准），聚焦函数性质：① 相同 `(currentBPM,authoredBpm)` 多次调用返回逐位一致；② 反射/Roslyn 断言方法体不引用 `Time.*`/单例/静态可变字段；③ 钳制区间内固定 `authoredBpm` 时 `currentBPM` 递增 → 速度单调非减。

---

### AC-F6-14 — Root Motion 在 Animator 组件层面禁用

**类型**: Logic | **测试方式**: PlayMode 自动化测试 / EditMode Prefab 检查

验证 `AnimancerAttackPlayer.Awake()` 执行后 `_animancer.Animator.applyRootMotion == false`；播放轻攻击、重攻击、充能攻击 3 个 Animancer State 期间，角色 `Transform.position` 变化来源为 Rigidbody 驱动，无非物理来源的位置漂移（帧间位移与 `Rigidbody.velocity × Time.deltaTime` 差值 ≤ 0.001m）。

**注意**：断言目标为 `Animator.applyRootMotion`（Unity 组件属性），而非 `AnimancerState`（Animancer 8.x 的 `AnimancerState` 无此属性）。

---

### AC-F6-15 — 攻击动画中途 BPM 骤跳时速度立即更新（Option A，via UpdateBpm()）

**类型**: Integration | **测试方式**: PlayMode 自动化测试（使用 C2 最小 stub，不依赖完整 C2 实现）

BPM=120 时攻击进度 40–60% 处，C2 调用 `AnimancerAttackPlayer.UpdateBpm(200)`（模拟 BPM 骤跳时 C2 的响应）：
- 调用后**下一帧** `_currentState.Speed ≈ 1.667f`（±0.001，等于 `200 / attackNode.AuthoredBpm`，authoredBpm=120）
- `AnimancerState.NormalizedTime` 未被重置（攻击进度单调递增）
- 攻击动画 `IsPlaying == true`（未被中断）

**通过条件**：`UpdateBpm()` 调用后下一帧 Speed 更新，NormalizedTime 保持连续，动画未中断。

---

### AC-F6-16 — 死亡事件期间 AnimancerAttackPlayer 自行停止攻击并归还 TDE 控制权

**类型**: Integration | **测试方式**: PlayMode 自动化测试 + 手动目视确认

攻击动画中途发布 `TopDownEngineEvent(PlayerDeath)`：
- 事件后 1 帧内 `AnimancerAttackPlayer.StopAttack()` 被调用（来自 `AnimancerAttackPlayer` 自身的 `MMEventListener<TopDownEngineEvent>`）
- `StopAttack()` 后 `_animancer.PlayController()` 被调用，TDE Animator Controller 恢复控制
- `onComplete` 回调**未被触发**（`_pendingOnComplete` 已清空）
- 目视确认死亡动画完整播放，无攻击 Clip 残留遮盖

**归属说明**：死亡时停止攻击动画是 F6（`AnimancerAttackPlayer`）责任，无需 C2 参与。本 AC 失败时缺陷归属 F6 的 `AnimancerAttackPlayer` `MMEventListener` 注册逻辑。

---

### AC-F6-17 — 5 次连续场景切换后 OnSceneLoaded 回调不累积

**类型**: Logic | **测试方式**: PlayMode 自动化测试

以 `LoadSceneMode.Single` 连续切换 5 个不同测试场景（每次等待加载完成后再切下一个）：`OnSceneLoaded` Spy 总调用次数 == 5（每次恰好 +1，无累积）；`BeatClock.Play()` 总调用次数 == 5。

---

### AC-F6-18 — currentBPM=120、authoredBpm=120 时 attackAnimSpeed 精确等于 1.0（端到端集成校准）

**类型**: Integration | **测试方式**: PlayMode 自动化测试（使用 C2 最小 stub，不依赖完整 C2 实现）

> 此 AC 补充 AC-F6-13（EditMode 单元测试），验证完整运行时路径：BeatClock → C2 stub → AnimancerAttackPlayer → AnimancerState.Speed。

通过 mock BeatClock 将 `currentBPM` 锁定为 120，`attackNode.AuthoredBpm == 120`（默认值），执行 PlayMode 完整攻击序列后：
- `AnimancerState.Speed == 1.000f`（±0.001 容差，**在 `PlayAttack()` 调用后下一帧读取**）
- 0.5s 标称 Clip 实际播放时长偏差 ≤ 16ms（60FPS 一帧容差，通过 `Time.time` 差值计算）

**测试环境**：PC Editor（60Hz 锁帧）；不要求在移动设备上执行本 AC（移动帧率变化引入的额外计时误差由 AC-F6-19 的 FMOD 异步验证层覆盖）。

**通过条件**：Speed 精度和 Clip 时长偏差均在容差范围内。

---

### AC-F6-19 — FMOD 异步模式验证（协程双帧 DSP 时间递增）

**类型**: Integration | **测试方式**: PlayMode 自动化测试

`IDSPClock` 来源：`_beatClock.DSPClock`（BeatClock 只读属性），无需额外 Inspector 序列化。验证通过 `VerifyFMODAsyncMode()` **协程**实现（非 `Start()` 同帧二次采样）：

- **正常路径**：协程在第 1 帧读取 `t1`，`yield return null` 后第 2 帧读取 `t2`；`t2 > t1`，协程内 `BeatClock.Play()` 正常执行；`BeatClock.Play()` 调用次数 == 1，BeatClock 状态 `Playing`。
- **同步模式注入路径**：注入返回恒定值的 mock `IDSPClock`（`t1 == t2`）；协程检测到 `t2 <= t1`，记录 fatal 日志 `[F6-FATAL] FMOD running in sync mode — BeatClock start blocked`；`BeatClock.Play()` 调用次数 == 0。

**IDSPClock 可测试性**：同 AC-F6-21，`TDELifecycleAdapter` 通过 `BeatClock.DSPClock` 属性访问；测试中注入 mock BeatClock（或 mock IDSPClock 实现）即可覆盖两条路径，无需真实 FMOD 运行时。

**通过条件**：正常 FMOD 协程路径 BeatClock 正常启动；同步模式注入路径 BeatClock 被阻止且记录 fatal 日志。

---

### AC-F6-20 — 攻击中途 BPM 骤跳的玩家体感验证（Option A 原型测试）

**类型**: Visual/Feel | **测试方式**: 人工盲测（原型阶段，至少 3 人参与）

BPM=120 → 200 骤跳时攻击动画中途加速的场景，以盲测问卷评估：
- 至少 2/3 受试者能描述「这一刀在音乐加快时变得更凌厉」（正向感知）
- 无受试者报告「动画跳跃」或「手感断开」
- 至少 2/3 受试者评价攻击加速与 BPM 骤跳感觉「协调」或「自然」

**注意**：此 AC 仅在原型阶段执行（须 C2 + AnimancerAttackPlayer 联调完成），结果写入 `production/qa/evidence/f6-option-a-feel-test-[date].md`。若测试不通过，作为 MVP 范围调整的信号（可回退 Option B）。

---

### AC-F6-34 — Option A 盲测证据留存规范

**类型**: Visual/Feel | **测试方式**: 人工盲测证据审阅（与 AC-F6-20 同批次）

补 AC-F6-20 主观阈值之外的证据可追溯性：① ≥3 档 BPM（如 90/120/160）随机序、每档连续攻击 10+ 次全程录屏；② 录屏经审阅逐档确认起手/命中帧贴拍（无脱拍/黏滞），逐档独立结论；③ 每档评语 + 录屏/关键帧截图 + audio-director 或 creative-director 签核（含姓名/日期）存入 `production/qa/evidence/story-012-feel-evidence.md`。

---

### AC-F6-21 — FMOD RuntimeManager 验证通过 IFMODRuntimeChecker 接口注入（可测试性）

**类型**: Logic | **测试方式**: EditMode 自动化测试

`TDELifecycleAdapter` 通过注入的 `IFMODRuntimeChecker` 接口（而非直接引用 `FMODUnity.RuntimeManager` 静态类）执行初始化状态检查。

**注入路径**：运行时 `TDELifecycleAdapter.Awake()` 中执行 `_fmodChecker ??= new FMODRuntimeChecker()`（`[SerializeField] IFMODRuntimeChecker _fmodChecker` 默认为 null，Awake 时自动填充真实实现）；EditMode/PlayMode 测试通过 Constructor Injection 或直接对 `[SerializeField]` 字段赋值注入 mock 实现，无需真实 FMOD 运行时。

EditMode 测试中注入返回 `false` 的 mock `IFMODRuntimeChecker`：断言 `BeatClock.Play()` 调用次数 == 0 且记录 fatal 错误 `[F6-FATAL] FMOD RuntimeManager not initialized — BeatClock start blocked`——无需真实 FMOD 运行时即可验证 AC-F6-04 的逻辑路径。

**通过条件**：mock false 路径中 BeatClock 被阻止；PlayMode 中真实 FMOD 路径正常启动。

---

### AC-F6-22 — Character Prefab 结构 EditMode 完整性检查

**类型**: Logic | **测试方式**: EditMode 自动化测试

加载 Character Prefab，断言以下 4 项全部通过：
1. `HybridAnimancerComponent` 组件存在，且 `KeepChildrenConnected == true`
2. `AnimancerAttackPlayer` 组件存在，且序列化引用 `_animancer` 已连线（非 null）
3. `Animator.applyRootMotion == false`
4. `AnimancerAttackPlayer` 上 `ATTACK_LAYER_BLEND_IN` 字段值 ∈ [0.02, 0.15]

**通过条件**：全部 4 项断言通过；任意一项失败说明 Prefab 配置不完整，须修复后方可执行 AC-F6-12 至 AC-F6-16。

---

### AC-F6-32 — 攻击层 Root Motion 位移写入隔离 + Avatar Mask 不重叠

**类型**: Logic | **测试方式**: EditMode Prefab 检查 + PlayMode

① `HybridAnimancerComponent` 攻击层 `applyRootMotion` 对 Transform 位移写入为关闭（位移权威归 TDE `CharacterMovement`，与 AC-F6-14 `Animator.applyRootMotion==false` 互补，聚焦 Animancer 攻击层自身开关）；② 攻击层 Avatar Mask 与 TDE 移动/生命层 Mask 重叠骨骼数==0。

---

### AC-F6-23 — F5 初始化 gate + LatencyOffsetMs boot-inject（headless via MockSaveService）

**类型**: Integration | **测试方式**: PlayMode 自动化测试（注入 `MockSaveService : ISaveService`，无真实文件 I/O）

覆盖 ADR-D10 D-4/D-5 启动链路。5 项子用例：

1. **boot-inject 顺序（D-5）**：`MockSaveService` 返回 `{latencyOffsetMs=42.5f, isCalibrated=true}`。断言 `BeatClock.LatencyOffsetMs == 42.5f` **早于**首次 `BeatClock.Play()`（`Play()` 仍在 `OnSceneLoaded(Single)`，遵 ADR-D1）——即早于首个 `OnBeatExact` 广播、早于 `EnableGameplayInput()`。
2. **首次启动默认（D-5）**：`MockSaveService` 返回 `{0f, isCalibrated=false}`，断言 `BeatClock.LatencyOffsetMs == 0f` 且不抛错。
3. **一次性 guard（D-5）**：连续两次 `OnSceneLoaded(Single)`（模拟复活/换关），断言 `LoadLatencyCalibration()` 仅被调用 1 次，`_offsetInjected` 阻止重复注入（不覆盖运行期 FE3 写入）。
4. **release-active 初始化 gate（D-4）**：注入未初始化的 `MockSaveService`（`IsInitialized == false`），断言 `TDELifecycleAdapter.Start()` fail-fast——记录 `[F6-FATAL] SaveService not initialized...`、`enabled == false`、`BeatClock.Play()` 调用次数 == 0。**须在 release-config 构建路径执行**（该 guard 非 `Debug.Assert`，须在发布构建生效）。
5. **同步契约（D-4）**：断言 `saveService.Initialize()` 返回后 `IsInitialized == true`（无 yield），后续 `Start()` 内消费方能立即读到已加载 DTO。

**通过条件**：5 项子用例全部通过。`ISaveService` seam 使全部子用例 headless（无真实 ES3 文件 I/O）。

---

### AC-F6-27 — boot 门禁与注入的调用序号顺序断言

**类型**: Integration | **测试方式**: PlayMode（调用序号 Spy + `MockSaveService` + mock FMOD 门禁）

用全局单调递增调用序号 Spy 断言：① `ISaveService` 就绪求值序号 < FMOD 就绪门禁（AC-F6-21）序号；② `LatencyOffsetMs` 注入序号 < `BeatClock.Play()` 序号；③ `InputEventBus.EnableGameplayInput()` 序号 > `Play()` 序号。任一逆序判违规。

---

### AC-F6-28 — LatencyOffsetMs boot 一次性注入 + PlayerInfoSO 缺失降级

**类型**: Integration | **测试方式**: PlayMode（`MockSaveService`，无真实文件 I/O）

① `MockSaveService` 返回有效值时，boot 生命周期内注入 `BeatClock` 累计==1；② `IsInitialized==true` 但 `PlayerInfoSO==null` 时 `BeatClock.LatencyOffsetMs` 取默认 `0f` + 恰 1 条 Warning（非 Fatal）+ `Play()` 仍放行（调用==1），与 AC-F6-23「服务未初始化 fail-fast」明确区分。

---

### AC-F6-24 — 音频设备断开时 F6 同帧暂停 TDE 并禁用所有输入（SC-1）

**类型**: Logic | **测试方式**: PlayMode 自动化测试（注入 mock F2 事件源 + Spy TDE 暂停/InputEventBus）

覆盖 SC-1 降级承接链路。从 Gameplay Active 状态发布 F2 `OnAudioUnavailable` 事件后：

1. **同帧原子性**：`GameManager.Pause()`（或等效 TDE 暂停触发，经 Spy 捕获）与 `InputEventBus.DisableAllInput()` 记录的 `Time.frameCount` 值相同（同一 Update 帧内，单主线程保证）；两者各调用恰好 1 次。
2. **战场全冻结**：事件后 TDE 移动被冻结（模拟移动输入，角色 `Transform.position` 无变化）；输入通道对攻击指令无响应（`InputEventBus` 状态 `AllDisabled`）——不存在「TDE 仍可移动 / F3 仍 GameplayActive」的半运行状态。
3. **恢复对称路径**：随后发布 F2 `OnAudioRestored`（或 `OnBeatClockResumed`）后，`BeatClock.Resume()`、`GameManager.UnPause()` 与 `InputEventBus.EnableGameplayInput()` 各调用恰好 1 次；**`BeatClock.Resume()` 调用来源为 `TDELifecycleAdapter`（W-1：Resume owner=F6，F2 事件源 Spy 断言 F2 自身未调用任何 BeatClock 生命周期方法）**；调用顺序序号 `Resume()` < `EnableGameplayInput()`（节拍参照先于输入开放，遵 EC-05）；最终状态回到 GameplayActive。

**通过条件**：以上 3 项全部通过。降级与恢复均通过 mock F2 事件源触发，无需真实音频设备断开。

---

### AC-F6-30 — 音频降级/恢复交接幂等性

**类型**: Logic | **测试方式**: PlayMode（mock F2 事件源）

① 连续两次 `OnAudioUnavailable`（未恢复）降级标志单一、内部时基切换每丢失周期至多 1 次；② 非降级态收 `OnAudioRestored` 无副作用；③ 全程 `BeatClock.Stop()` 累计==0（降级≠结束）。

---

### AC-F6-31 — 降级期节拍判定连续性 + 恢复后相位收敛

**类型**: Logic | **测试方式**: PlayMode（mock F2 + 可控内部时基）

① `OnAudioUnavailable` 后 BeatClock 保持 `Playing`、节拍序号持续单调递增无中断；② `OnAudioRestored` 重对齐后节拍相位与 DSP 相位偏差 ≤ 判定窗口容差（复用 F3 帧率容差基准），多周期（≥3）测量无残留永久偏移。

---

### AC-F6-25 — run-end 域事件同帧广播供 C1/C4 承接（SC-2/SC-3，Death + Victory，ADR-D13）

**类型**: Logic | **测试方式**: PlayMode 自动化测试（注入 mock `TopDownEngineEvent` 源 / 显式 `EndRun()` 调用 + Spy C1/C4 域事件订阅者）

覆盖 SC-2/SC-3 桥接链路。从 Gameplay Active 状态经 `EndRun(outcome)` 触发 run-end（Death 与 Victory），随后注入 `TopDownEngineEvent(RespawnComplete)`：

1. **Death 桥接**：F6 收 TDE `PlayerDeath` → `EndRun(RunOutcome.Death)` → 同帧广播 `OnRunEnded(Death)`（Spy 捕获恰好 1 次），且与 `BeatClock.Stop()` + `InputEventBus.DisableAllInput()` 记录的 `Time.frameCount` 相同（同一 Update 帧）。域事件负载类型（`RunOutcome`）**不含任何 TopDownEngine 命名空间类型**（编译期断言：事件签名仅引用 CombatCore 可见类型）。**C4 亦为该事件订阅方**（SC-3，令牌回收侧闭环见 C4 `AC-C4-31`）。
2. **Victory 桥接（ADR-D13）**：经 `EndRun(RunOutcome.Victory)`（模拟 FE4/FE2/FE5 胜利发起或 TDE 关卡完成事件桥接）→ 同帧广播 `OnRunEnded(Victory)`（Spy 恰 1 次），断言与 Death 子用例对称（`Stop()` + `DisableAllInput()` 同帧、负载不泄漏 TDE 类型、C1/C4 均收到）。
3. **多触发源闭锁（D13-5）**：同局连续两次 run-end 触发 → `OnRunEnded` 广播恰 1 次、`Stop()` 新增调用 == 1（一次性闭锁 `_runEnded` 早退第二次）。
4. **RespawnComplete 桥接（MVP 休眠，ADR-D12）**：`OnRespawnComplete` 域事件桥接定义保留供 Post-MVP，但 MVP 路径下注入 `RespawnComplete` **不**触发原地 `BeatClock.Play()` 重启（`Play()` 新增调用 == 0），改记 fatal 诊断（与 AC-F6-11 一致）。本 AC 在 MVP 仅断言桥接**签名可编译、事件不泄漏 TDE 类型**，不断言原地重启同帧行为。
5. **C1/C4 可订阅性**：Spy 订阅者仅通过 CombatCore 可见的 `TDELifecycleAdapter.OnRunEnded`（+ `OnRespawnComplete`）C# event 注册成功，无需引用 TDE 类型——证明 C1/C4 的边界隔离订阅路径可落地。

**通过条件**：以上 5 项全部通过。桥接均通过 mock TDE 事件源 / 显式 `EndRun()` 触发。与 C1 `AC-C1-22e`（run-end 清空 pending，Death + Victory）、C4 `AC-C4-31`（run-end 令牌回收）配对，形成 SC-2/SC-3 的 F6 广播侧 + C1/C4 消费侧完整闭环。

---

**依赖说明（执行阻断）**：
- AC-F6-12 / AC-F6-15：需 C2 最小 stub 实现（提供可执行 `PlayAttack()` / `UpdateBpm()` 调用的测试替身），不依赖完整 C2 连招图逻辑
- AC-F6-04 / AC-F6-21：需与 lead programmer 确认 `IFMODRuntimeChecker` 接口定义及程序集内部访问权限（`[InternalsVisibleTo]` 目标）
- AC-F6-20：仅在原型阶段执行，须 C2 + AnimancerAttackPlayer 联调完成后安排人工盲测
- AC-F6-23：需 F5 提供 `ISaveService` 消费者 seam + `MockSaveService` 测试替身（见 `save-persistence.md` Rule 15），并与 lead programmer 确认单一显式 bootstrap driver 的落地组件；子用例 4 须在 release-config 构建路径执行（fail-fast guard 非 Debug-only）
- AC-F6-24（SC-1）：需 F2 GDD 正式定义 `OnAudioUnavailable` 降级事件及恢复事件（`OnAudioRestored` / 复用 `OnBeatClockResumed`）；测试注入 mock F2 事件源 + Spy TDE 暂停 API（`GameManager.Pause/UnPause`）与 `InputEventBus`，无需真实音频设备断开
- AC-F6-25（SC-2/SC-3，ADR-D13）：需与 lead programmer 确认 `TDELifecycleAdapter` 桥接域事件 `OnRunEnded(RunOutcome)`（+ `OnRespawnComplete`）与单一入口 `EndRun(RunOutcome)` 的程序集可见性（CombatCore 可引用，不引入 TDE 类型泄漏；`RunOutcome` 为 CombatCore 可见纯 C# enum）；测试注入 mock `TopDownEngineEvent` 源 / 显式 `EndRun()` 调用 + Spy C1/C4 订阅者，无需真实 TDE 死亡/胜利流程。**胜利桥接的 TDE 关卡完成事件成员名须先经 ADR-D13 VR1 核对**（实现前硬门）。与 C1 `AC-C1-22e`、C4 `AC-C4-31` 配对验证 SC-2/SC-3 闭环

---

## Open Questions

- **OQ-01（W3 继承自 ADR-D1，PENDING）**: `TDELifecycleAdapter.Start()` 中的 FMOD 初始化时序在 Unity Editor 运行时是否稳定？需导入 FMOD 插件后实机运行验证。（ADR-D1 W3 — 非阻断，但须在 F6 实现 Sprint 开始前确认）
- **OQ-02（已解决 2026-07-18）**: C2 采用 Option A（实时更新）：BPM 变化时 C2 订阅 BeatClock 的 BPM 变化事件，立即调用 `AnimancerAttackPlayer.UpdateBpm(newBpm)` 更新 `_currentState.Speed`，不重置 normalized time，不中断动画。速度更新条件为「BPM 变化时」（非每拍），更新不依赖 AnimancerAttackPlayer 当前是否正在播放攻击动画（`_currentState == null` 时 `UpdateBpm()` 空操作）。解决方案已写入 F6 GDD Formulas 节及 ADR-D5 修订说明；C2 GDD 接口契约须记录此调用约定。
