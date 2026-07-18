# F6: TopDownEngine & Animancer 集成层 (TDE Integration Layer)

> **Status**: In Review
> **Author**: Jadyn Wu + Claude Code Agents
> **Last Updated**: 2026-07-18 (Full draft complete)
> **Implements Pillar**: 支柱二：武侠手感不可妥协 (Wuxia Feel First)
> **System ID**: F6
> **Layer**: Foundation | **Milestone**: MVP | **Design Order**: 5
> **ADR Reference**: `docs/architecture/adr-d1-tde-integration.md` (Accepted, M0 Spike verified 2026-07-16)

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

6. **Additive 加载过滤**
   `TDELifecycleAdapter.OnSceneLoaded()` 仅响应 `LoadSceneMode.Single`（整场景切换），忽略 `LoadSceneMode.Additive`（TDE LevelManager 用于房间拼接）。房间内 BPM 变化通过 `BeatClock.RequestBPMChange()` 处理，不重新调用 `Play()`。

7. **Animancer 攻击层边界合约**
   - **Layer 0（TDE Animator Controller — TDE 完整拥有）**：空闲、行走、冲刺、死亡等所有非攻击动画。Animancer 不干预此层。
   - **Layer 1（Animancer Override 层 — Animancer 完整拥有）**：所有攻击动画（轻攻击、重攻击、充能攻击、连招节点）。
   - 攻击**开始**时 Layer 1 权重 = 1.0（全覆盖 TDE Base Layer）；攻击动画**完成**后权重渐变至 0.0（TDE 移动层重新接管）。
   - 层权重过渡由 **C2（连招图系统）** 在攻击状态机中管理；F6 负责在 Prefab 上正确配置层结构并暴露 `AnimancerLayer` 引用。

8. **攻击动画制作 BPM 标准（Nominal BPM = 120）**
   所有攻击 Clip 以 **120 BPM** 为参照制作，即 Clip 时长 = 攻击动作在 120 BPM 节奏下的自然持续时长。运行时速度缩放公式见 Formulas 节。

9. **Root Motion 禁用**
   所有 Animancer 攻击层 State 均设 `applyRootMotion = false`。TDE 的 `CharacterMovement` 基于 Rigidbody 物理控制位移，Root Motion 会产生位置漂移冲突。

10. **订阅者生命周期**
    `TDELifecycleAdapter` 在 `OnEnable()` 注册 `MMEventListener<TopDownEngineEvent>` 和 `SceneManager.sceneLoaded`；在 `OnDisable()` 取消注册，防止 DontDestroyOnLoad 单例跨场景重复注册。

---

### States and Transitions

F6 自身不维护内部状态机，但其事件响应决定了系统的全局操作阶段：

| 操作阶段 | 触发条件 | InputEventBus 状态 | BeatClock 状态 |
|---------|---------|------------------|--------------|
| **未初始化** | 应用启动 / 首次场景加载前 | AllDisabled | Stopped |
| **场景切换中** | SceneManager 加载 Single 场景（进行中） | AllDisabled | Stopped |
| **Gameplay Active** | `OnSceneLoaded(Single)` 完成 | GameplayActive | Playing |
| **已暂停** | `TopDownEngineEvent(Pause)` | UIActive | Paused |
| **玩家死亡** | `TopDownEngineEvent(PlayerDeath)` | AllDisabled | Stopped |
| **复活完成** | `TopDownEngineEvent(RespawnComplete)` | GameplayActive | Playing（重启） |

阶段转换的**原子性保证**：每次 `TDELifecycleAdapter` 响应 TDE 事件时，BeatClock 调用与 InputEventBus 调用在**同一 Update 帧内**完成，消除半途不一致状态。

---

### Interactions with Other Systems

| 方向 | 系统 | 数据内容 | 接口类型 |
|------|------|---------|---------|
| TDE **→** F6 | TopDownEngine | `TopDownEngineEvent`（Pause/UnPause/PlayerDeath/RespawnComplete） | `MMEventListener<TopDownEngineEvent>` 事件订阅 |
| F6 **→** F2 | BeatClock | `Play(bgmRef, bpm, timeSig)` / `Stop()` / `Pause()` / `Resume()` | 直接方法调用（独家控制） |
| F6 **→** F3 | InputEventBus | `EnableGameplayInput()` / `EnableUIInput()` / `DisableAllInput()` | 直接方法调用 |
| F4 **→** F6 | LevelConfigSO | `Bpm`、`TimeSignature`、`BgmAssetReference`（每场景注入） | Inspector 序列化字段 |
| C2 **→** F6 | 连招图系统 | 设置 `AnimancerLayer.Weight`（1.0/0.0）与 `AnimancerState.Speed` | 访问 F6 暴露的 `AnimancerLayer` 引用 |
| FE2 **→** F6 | 房间系统 | `BeatClock.RequestBPMChange(newBpm)`（房间级 BPM 切换） | 通过 F2 的 `RequestBPMChange()` API（非重新调用 `Play()`） |
| F6 **（通过 TDE）→** C6/FE2 | Hitbox 判定 / 房间系统 | TDE 原生碰撞检测与 `LevelManager` 场景管理基础（F6 正确配置 TDE 后自动提供） | TDE 内置组件（间接） |
| F6 **（通过 Feel）→** PL2 | VFX / 节奏反馈 | MMFeedbacks/Feel 基础设施由 TDE 捆绑提供；PL2 订阅节奏事件发起反馈序列 | MMFeedbacks 组件（间接） |

---

## Formulas

### F6-FORMULA-01：攻击动画速度缩放

```
attackAnimSpeed = clamp(currentBPM / nominalBPM, SPEED_MIN, SPEED_MAX)
```

**变量定义**

| 变量 | 类型 | 范围 | 来源 | 说明 |
|------|------|------|------|------|
| `currentBPM` | float | [80, 240] | F2 BeatClock（实时） | 当前关卡激活 BPM。**Option A：实时更新**——BPM 变化立即生效，即使处于攻击动画中途 |
| `nominalBPM` | float | 120（常量） | F6 Constants | 所有攻击 Clip 的制作参照 BPM，定义为 `NOMINAL_BPM = 120f` |
| `SPEED_MIN` | float | 0.5（常量） | F6 Constants | 安全限幅下界，防止极低 BPM 导致动画卡顿 |
| `SPEED_MAX` | float | 2.5（常量） | F6 Constants | 安全限幅上界，防止异常 BPM 输入导致动画闪现 |
| **输出 `attackAnimSpeed`** | float | [0.5, 2.5] | — | 写入 `AnimancerState.Speed`，控制攻击 Clip 播放速率 |

**正常游戏运行输出范围**：`currentBPM ∈ [80, 240]` → `attackAnimSpeed ∈ [0.667, 2.0]`

安全限幅 [0.5, 2.5] 宽于数学范围，仅用于拦截超出 `bpm_range` 注册值的异常输入。限幅触发时记录 Warning：`[F6] attackAnimSpeed clamped from {raw:.3f} to {clamped:.3f}`

**实例计算**（以 0.5 秒攻击 Clip 为基准）

| BPM | attackAnimSpeed | 0.5s Clip 实际时长 |
|-----|-----------------|-------------------|
| 80  | 0.667×          | 0.750 s           |
| 100 | 0.833×          | 0.600 s           |
| **120** | **1.000×** | **0.500 s（参照）** |
| 160 | 1.333×          | 0.375 s           |
| 180 | 1.500×          | 0.333 s           |
| 200 | 1.667×          | 0.300 s           |
| 240 | 2.000×          | 0.250 s           |

**更新策略（Option A — 实时更新）**

`C2 连招图系统`在每拍回调（`BeatClock.OnBeat`）中重新计算 `attackAnimSpeed` 并写入 `AnimancerState.Speed`。BPM 变化（含 Boss 阶段切换时的 `RequestBPMChange()`）立即反映到当前正在播放的攻击动画速率，无需等待当前动画完成。

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

**情境**：玩家正在执行攻击动画（Layer 1 权重 = 1.0），Boss 阶段切换触发 `RequestBPMChange(120 → 200)`。

**行为（Option A 实时更新）**：`C2` 在下一个 `OnBeat` 回调时读取新 `currentBPM`，计算 `attackAnimSpeed = 200/120 = 1.667`，立即写入 `AnimancerState.Speed`。当前攻击动画加速播放，不等待完成。玩家感知：这一刀在节奏加快的瞬间变得更凌厉。

**注意**：动画剩余时长缩短，C2 必须能处理「动画比预期提前完成」的情况。这是 C2 的责任，F6 不补偿。

---

### EC-04：死亡事件与攻击动画的竞争

**情境**：玩家在攻击动画执行中途（Layer 1 权重 = 1.0）死亡，`TopDownEngineEvent(PlayerDeath)` 到达。

**行为**：`TDELifecycleAdapter` 收到 `PlayerDeath` → 同帧调用 `InputEventBus.DisableAllInput()` + `BeatClock.Stop()` → TDE 原生死亡序列接管角色控制（TDE 播放死亡动画，覆盖 Animator Controller）。

Animancer Layer 1 权重由 **C2 负责清零**（C2 必须在接收到 `PlayerDeath` 事件时将 Layer 1 权重归零）。F6 不直接操作 Layer 权重——这属于 C2 的攻击状态机职责。若 C2 未及时清零，死亡动画会被攻击层遮盖；这是 C2 的缺陷，不是 F6 的缺陷。

---

### EC-05：复活时 BeatClock 重启与输入通道竞争

**情境**：`TopDownEngineEvent(RespawnComplete)` 到达，`TDELifecycleAdapter` 需重启 BeatClock 并开放输入。

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
| BeatClock | F2 | `Play(bgmRef, bpm, timeSig)` / `Stop()` / `Pause()` / `Resume()` / `RequestBPMChange()` / `OnBeat` 事件 | `TDELifecycleAdapter` 独家控制生命周期；C2 通过 `OnBeat` 读取 `currentBPM` 更新 `attackAnimSpeed` |
| InputEventBus | F3 | `EnableGameplayInput()` / `EnableUIInput()` / `DisableAllInput()` | `TDELifecycleAdapter` 在生命周期事件后调用，控制输入通道开关 |
| LevelConfigSO 数据层 | F4 | `LevelConfigSO.Bpm` / `TimeSignature` / `BgmAssetReference` | `TDELifecycleAdapter` 在 `OnSceneLoaded(Single)` 时读取，注入 `BeatClock.Play()` |
| TopDownEngine | 外部插件 | `InputSystemManagerEventsBased` / `TopDownEngineEvent` / `MMEventListener<T>` / TDE Animator Controller 基础设施 | `JadeRabbitInputManager` 继承 TDE 输入类；`TDELifecycleAdapter` 订阅 TDE 事件总线 |
| Animancer | 外部插件 | `HybridAnimancerComponent` / `AnimancerLayer` / `AnimancerState.Speed` | F6 在角色 Prefab 上配置 `HybridAnimancerComponent`，暴露 Layer 1 引用供 C2 使用 |
| FMOD | 外部插件 | `FMODUnity.RuntimeManager`（初始化状态验证） | `TDELifecycleAdapter.Start()` 验证 FMOD 已初始化后才允许 `BeatClock.Play()` |

### 依赖 F6 的系统（下游）

| 系统 | 系统 ID | 期望 F6 提供的内容 | F6 提供方式 |
|------|---------|-----------------|------------|
| CombatCore / 连招图系统 | C2 | `AnimancerLayer` 引用（Layer 1 权重控制）；`attackAnimSpeed` 公式（由 C2 实现写入 `AnimancerState.Speed`） | F6 在 Prefab 上配置层结构并通过序列化字段暴露引用；公式在本 GDD Section D 中定义 |
| Hitbox 判定系统 | C6 | TDE 原生碰撞检测基础设施（TDE 提供，F6 正确配置后自动生效） | F6 不直接提供；通过正确继承 TDE 后 TDE 内置 |
| 房间系统 | FE2 | BPM 变更通道（`BeatClock.RequestBPMChange()`，非重新 `Play()`） | 通过 F2 的 `RequestBPMChange()` API；F6 文档化此约束，FE2 遵守 |
| 相机系统 | PL1 | TDE `LevelManager` 场景管理（相机跟随依赖 TDE 关卡基础设施） | F6 正确集成 TDE 后自动提供 |
| 暂停菜单 | P5 | `TopDownEngineEvent(Pause/UnPause)` 信号（TDE 发出，F6 响应） | F6 通过 `TDELifecycleAdapter` 同帧暂停 BeatClock + 切换输入到 UIActive；P5 订阅相同 TDE 事件链的后续处理 |
| VFX 节奏反馈 | PL2 | BeatClock `OnBeat` 事件（PL2 订阅节奏事件触发反馈序列） | 通过 F2 BeatClock；F6 正确启动 BeatClock 后 PL2 自动可用 |

### 双向依赖确认

> **F2（BeatClock）GDD** 中须记录：F6 `TDELifecycleAdapter` 是 `BeatClock.Play/Stop/Pause/Resume` 的唯一调用者。
>
> **F3（InputEventBus）GDD** 中须记录：F6 `TDELifecycleAdapter` 在 TDE 生命周期事件后调用 InputEventBus 方法。`JadeRabbitInputManager` 通过继承保留 TDE `AbilityInputAuthorization` 管线，但不直接调用 `InputActionMap.Enable()/Disable()`。
>
> **C2（连招图系统）GDD** 中须记录：C2 从 F6 获取 `AnimancerLayer` 引用并管理 Layer 1 权重；C2 使用 F6-FORMULA-01 计算并写入 `AnimancerState.Speed`；C2 在 `PlayerDeath` 时须清零 Layer 1 权重。

---

## Tuning Knobs

| 旋钮名称 | 变量 | 默认值 | 安全范围 | 影响的游戏体验方面 |
|---------|------|-------|---------|-----------------|
| 攻击动画参照 BPM | `NOMINAL_BPM` | 120 BPM | [60, 180] | 攻击动画"基准节奏感"：调低使动画在低 BPM 时更流畅，调高使动画在低 BPM 时拖曳感更强 |
| 速度缩放下界 | `SPEED_MIN` | 0.5 | [0.25, 0.75] | 极低 BPM 时攻击动画最慢播放速度；过低可能导致动画长时间卡住 |
| 速度缩放上界 | `SPEED_MAX` | 2.5 | [2.0, 4.0] | 极高 BPM 时攻击动画最快播放速度；过高可能导致动画闪现 |
| Animancer 层过渡时长 | `ATTACK_LAYER_BLEND_IN` | 0.05 s | [0.02, 0.15] | 攻击开始时 Layer 1 权重从 0→1 的淡入时长；过短有硬切感，过长有延迟感（由 C2 调整，F6 设默认值） |
| Animancer 层退出时长 | `ATTACK_LAYER_BLEND_OUT` | 0.1 s | [0.05, 0.2] | 攻击完成后 Layer 1 权重从 1→0 的淡出时长；影响攻击结束后恢复移动动画的流畅度（由 C2 调整，F6 设默认值） |

**注意**：`NOMINAL_BPM`、`SPEED_MIN`、`SPEED_MAX` 定义在 F6 专用 `F6Constants` 类中，以 `static readonly float` 暴露。其余两个 Blend 时长由 C2 连招图系统通过 F6 暴露的 `AnimancerLayer` 引用在运行时设置——F6 仅提供合理的 Inspector 默认值。

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

**类型**: Logic | **测试方式**: PlayMode 自动化测试

IL 扫描 `JadeRabbitInputManager` 所有方法体，断言不存在对 `InputActionMap.Enable()` / `Disable()` 的 `callvirt`。运行时验证攻击输入经 F3 `InputEventBus` 发布，调用栈包含 `BindButton()` → F3 代理路径，不含直接 Action Map 操作。

**通过条件**: IL 无直接调用 + 运行时输入事件经 F3 正确发布。

---

### AC-F6-02 — JadeRabbitInputManager 保留 TDE AbilityInputAuthorization 管线

**类型**: Integration | **测试方式**: PlayMode 手动验证

触发需要 `AbilityInputAuthorization` 锁的 TDE Ability（如 Dash）；Ability 持续期间攻击 `OnAttackInput` 未触发（输入被 TDE 管线拦截）；Ability 结束后攻击输入经 F3 正常发布；TDE Ability 状态机流程无破坏。

---

### AC-F6-03 — TDELifecycleAdapter 是 BeatClock 生命周期唯一调用者

**类型**: Logic | **测试方式**: PlayMode 自动化测试

IL 扫描全部非 `TDELifecycleAdapter` 类型（CombatCore、FE2、C2 等程序集均在内），断言无对 `BeatClock.Play/Stop/Pause/Resume` 的调用。运行时 Spy 记录所有生命周期调用来源，全部为 `TDELifecycleAdapter`。

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

从 Gameplay Active 状态发布 `TopDownEngineEvent(Pause)` 后：`BeatClock.Pause()` 与 `InputEventBus.EnableUIInput()` 调用的 `Time.frameCount` 相同（同一帧）；两个方法各调用恰好 1 次；BeatClock 状态 `Paused`，InputEventBus 状态 `UIActive`。

---

### AC-F6-09 — UnPause 事件同帧恢复 BeatClock 并切换输入至 GameplayActive

**类型**: Logic | **测试方式**: PlayMode 自动化测试

从已暂停状态发布 `TopDownEngineEvent(UnPause)` 后：`BeatClock.Resume()` 与 `InputEventBus.EnableGameplayInput()` 调用的 `Time.frameCount` 相同；两个方法各调用恰好 1 次；BeatClock 状态 `Playing`，InputEventBus 状态 `GameplayActive`。

---

### AC-F6-10 — PlayerDeath 事件同帧停止 BeatClock 并禁用所有输入

**类型**: Logic | **测试方式**: PlayMode 自动化测试

从 Gameplay Active 状态发布 `TopDownEngineEvent(PlayerDeath)` 后：`BeatClock.Stop()` 与 `InputEventBus.DisableAllInput()` 调用的 `Time.frameCount` 相同（同一帧）；死亡后输入通道不响应攻击指令；BeatClock 状态 `Stopped`，InputEventBus 状态 `AllDisabled`。

---

### AC-F6-11 — RespawnComplete 事件重启 BeatClock 先于输入通道开放

**类型**: Logic | **测试方式**: PlayMode 自动化测试

从玩家死亡状态发布 `TopDownEngineEvent(RespawnComplete)` 后：`BeatClock.Play()` 调用顺序序号 < `EnableGameplayInput()` 调用顺序序号（BeatClock 先启动）；两次调用 `Time.frameCount` 相同（同一帧原子完成）；最终状态 Playing + GameplayActive。

---

### AC-F6-12 — Animancer Layer 1 攻击时权重 1.0，完成后渐变至 0.0

**类型**: Integration | **测试方式**: PlayMode 自动化测试（依赖 C2 实现）

触发轻攻击，淡入完成后 `AnimancerLayer.Weight == 1.0f`（±0.01 容差）；攻击完成后 Weight 经渐变降至 0.0f，相邻帧差值不发生瞬跳（差值 ≤ `ATTACK_LAYER_BLEND_OUT / Time.fixedDeltaTime` 上界）；归零后 Layer 0 重新完整驱动角色动画。

---

### AC-F6-13 — 攻击动画速度缩放公式正确（含限幅边界与 Warning 日志）

**类型**: Logic | **测试方式**: EditMode 自动化测试

调用 `F6Constants.CalculateAttackAnimSpeed(currentBPM)` 断言以下用例（容差 ±0.001）：

| currentBPM | 期望速度 | Warning 日志 |
|---|---|---|
| 120 | 1.000f | 无 |
| 80 | 0.667f | 无 |
| 240 | 2.000f | 无 |
| 60 | 0.500f（下界限幅） | `[F6] attackAnimSpeed clamped from X.XXX to Y.YYY` |
| 300 | 2.500f（上界限幅） | 同格式 |

限幅 Warning 须包含原始值和截断值；非限幅用例断言无 Warning 输出。

---

### AC-F6-14 — Root Motion 在所有 Animancer 攻击状态上禁用

**类型**: Logic | **测试方式**: PlayMode 自动化测试

播放轻攻击、重攻击、充能攻击 3 个 Animancer State，每个 State `ApplyRootMotion == false`（3 个全部）；攻击期间角色 `Transform.position` 变化来源为 Rigidbody 驱动，Root Motion 提取值未叠加（无非物理来源的位置漂移）。

---

### AC-F6-15 — 攻击动画中途 BPM 骤跳时速度立即更新（Option A 实时）

**类型**: Integration | **测试方式**: PlayMode 自动化测试（依赖 C2 实现）

BPM=120 时攻击进度 40-60% 处调用 `BeatClock.RequestBPMChange(200)`，下一个 `OnBeat` 回调完成后：`AnimancerState.Speed ≈ 1.667f`（±0.001）；Speed 更新发生在 `OnBeat` 回调所在帧（不延迟到动画结束）；攻击动画继续播放未被中断。

---

### AC-F6-16 — 死亡事件期间攻击动画不遮盖死亡动画（Layer 1 权重清零验证）

**类型**: Integration | **测试方式**: PlayMode 自动化测试 + 手动目视确认（依赖 C2 实现）

攻击动画中途发布 `TopDownEngineEvent(PlayerDeath)`：事件后 1 帧内 Layer 1 Weight 开始下降（< 1.0f）；死亡动画播放期间 Layer 1 Weight == 0.0f；目视确认死亡动画完整可见，无攻击层遮盖。

**归属说明**：Layer 1 清零是 C2 责任。本 AC 失败时缺陷归属 C2，须同步核查 F6 已正确暴露 `AnimancerLayer` 引用和 `PlayerDeath` 事件通路。

---

### AC-F6-17 — 5 次连续场景切换后 OnSceneLoaded 回调不累积

**类型**: Logic | **测试方式**: PlayMode 自动化测试

以 `LoadSceneMode.Single` 连续切换 5 个不同测试场景（每次等待加载完成后再切下一个）：`OnSceneLoaded` Spy 总调用次数 == 5（每次恰好 +1，无累积）；`BeatClock.Play()` 总调用次数 == 5。

---

### AC-F6-18 — currentBPM=120 时 attackAnimSpeed 精确等于 1.0（参照 BPM 校准）

**类型**: Integration | **测试方式**: PlayMode 自动化测试

BeatClock `currentBPM` 锁定为 120（`NOMINAL_BPM`），触发攻击后在 `OnBeat` 回调完成时：`AnimancerState.Speed == 1.000f`（±0.001 容差）；0.5s 标称 Clip 实际播放时长偏差 ≤ 16ms（60FPS 一帧容差）。

---

**依赖说明（执行阻断）**：
- AC-F6-12 / AC-F6-15 / AC-F6-16：需 C2 连招图系统先实现 Layer 1 权重管理和 `OnBeat` 速度写入后方可执行
- AC-F6-04：需与 lead programmer 确认程序集内部访问权限（`[InternalsVisibleTo]` 目标）

---

## Open Questions

- **OQ-01（W3 继承自 ADR-D1，PENDING）**: `TDELifecycleAdapter.Start()` 中的 FMOD 初始化时序在 Unity Editor 运行时是否稳定？需导入 FMOD 插件后实机运行验证。（ADR-D1 W3 — 非阻断，但须在 F6 实现 Sprint 开始前确认）
- **OQ-02（待 C2 GDD 确认）**: C2 连招图系统在 `BeatClock.OnBeat` 回调中写入 `AnimancerState.Speed` 的具体条件：每拍无条件更新，还是仅在 Layer 1 处于激活状态（Weight > 0）时更新？F6 定义了公式，C2 负责实现——须在 C2 GDD 接口契约节中明确。
