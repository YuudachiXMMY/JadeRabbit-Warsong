# F3 Input 事件系统 (Input Event System)

> **Status**: Approved — Implementation Ready
> **Author**: Jadyn Wu + Claude Code Agents
> **Last Updated**: 2026-07-15 (round-8 revision)
> **Implements Pillar**: 支柱一：节奏即战斗 (Rhythm IS Combat) / 支柱二：武侠手感不可妥协 (Wuxia Feel First)
> **System ID**: F3
> **Layer**: Foundation | **Milestone**: MVP | **Design Order**: 2

---

## Overview

F3 是《玉兔：破阵》的输入管线基础设施——它将玩家的原始硬件输入（键鼠、手柄、触屏）转化为携带精确 DSP 时间戳的语义输入事件，并通过 C# 事件总线广播给下游系统。所有攻击输入的精确时间戳由 F3 在 `InputAction.performed` 回调中通过「同帧双读」策略同步捕获：`IDSPClock.GetDspTime()`（DSP 时钟域；ADR-D6 已选定 FMOD，生产实现为 `FMODDSPClockImpl`；F3 通过与 F2 相同的 `IDSPClock` 接口保持同一时钟域）和 `ctx.time`（游戏时间域，供调试使用）。ChargedAttack 额外在 `Hold.started` 时捕获 `chargeStartDspTime`（充能开始时刻），供 C1 以玩家意图时刻进行节奏判定；`inputDspTime` 记录充能完成时刻。移动持续值按原始分辨率直接传递，不经时间戳精度处理。

F3 不知晓节奏量化模式（Off/HalfBeat/Beat）、BPM 或拍号——它只负责精确捕获并传递。C1 决定如何将 F3 的输入事件对齐到 F2 的拍点网格。控制方案切换（键鼠 ↔ 手柄 ↔ 触屏）对下游系统透明，F3 统一输出相同的 `InputEvent` 结构，下游无需关心设备类型。运行时键位重绑定通过 Unity Input System 原生支持实现，绑定数据由 F5 持久化。

## Player Fantasy

F3 的玩家幻想是**时间戳的忠实性**——而非动画的即时性。

**F3 的实际合约**：C1 的节奏判定忠实反映玩家的真实意图时刻。玩家踩对了，判定就不会偏；玩家踩晚了，就应该被判晚。这一合约要求 F3 以 ≤ 一帧的误差精确记录"按键这个事实"的发生时刻，并将其以 DSP 时间戳的形式传递给 C1。

> 「零感知延迟的出刀感」是 F3 + C1 + F6/Animancer 共同交付的体验，不由 F3 单独负责。F3 只负责「捕获意图时刻」这一环。

**成功时（透明态）**：玩家不会意识到 F3 的存在。踩对了拍子，C1 的判定忠实地告诉他们踩对了。这种「判定可信赖」是让玩家相信自己技巧的基础前提。

**失败时（暴露态）**：玩家会说「我明明踩了那一拍，为什么判定是 Miss」。F3 的时间戳误差（≤ 16.7ms @ 60fps）被 C1 的判定窗口放大——BPM=180 时半拍窗口 167ms，误差占 10%，边界可接受；Android 平台 DSP 缓冲粒度（23-46ms）可能使同一误差增至 28-37%，须在 C1 调参时为移动端设置独立窗口下限。

## Detailed Design

### Core Rules

**1. Unity Input System 唯一接口**
所有玩家输入通过 Unity Input System Package（`com.unity.inputsystem`）处理。禁止使用 Legacy Input Manager（`Input.GetKey()` 等）或在 `Update()` 中轮询输入用于时序判定。

**2. 同帧双读（所有离散输入事件）**
所有非持续值的离散输入在各回调中同步捕获：
```
inputDspTime  = IDSPClock.GetDspTime()   ← DSP 时钟域（与 F2 同一接口）；C1 用于节奏判定
inputGameTime = (float)ctx.time          ← 游戏时间域，调试 / 日志使用（注意：float 精度约 1000s 后降至 1ms 级，仅供开发期诊断）
```
**ChargedAttack 额外捕获**：在 `Hold.started` 回调中记录 `chargeStartDspTime = IDSPClock.GetDspTime()`（充能开始时刻），在 `Hold.performed` 回调中记录 `inputDspTime`（充能完成时刻）。C1 以 `chargeStartDspTime` 作为节奏判定基准。

**线程安全约束**：所有 DSP 时间戳采样必须在 Unity 主线程执行。F3 须将 `InputSystem.settings.updateMode` 设为 `InputSettings.UpdateMode.ProcessEventsInDynamicUpdate`（默认值，确保 `performed` 回调在主线程 PlayerLoop 内触发），不得使用后台线程输入处理模式。

Move 的**持续值**（Vector2 大小）不需要 DSP 时间戳，通过同步查询接口 `GetCurrentMoveInput()` 按帧拉取。

**3. 输入分类规则**

| 输入类型 | Action 类型 | DSP 时间戳 | 广播接口 |
|---------|-----------|----------|---------|
| Attack1（轻攻击） | Button / performed | ✅ `inputDspTime`（按下时刻） | `OnAttackInput` |
| Attack2（重攻击） | Button / performed | ✅ `inputDspTime`（按下时刻） | `OnAttackInput` |
| ChargedAttack（充能开始） | Hold / **started** | ✅ `chargeStartDspTime`（充能意图时刻）→ 存入内部状态 | 内部缓存，不广播 |
| ChargedAttack（充能完成） | Hold / performed | ✅ `inputDspTime`（完成时刻）+ `chargeStartDspTime`（来自 started 缓存） | `OnAttackInput` |
| ChargedAttack（充能完成后松键） | Hold / **canceled**（`_holdPerformed == true`） | — 内部标志抑制（充能已完成，松键为正常结束） | 静默丢失（见 E3b） |
| ChargedAttack（早松开退化） | Hold / **canceled**（`_holdPerformed == false`，非切图引起） | ✅ `inputDspTime` = canceled 时刻；`chargeStartDspTime` 来自 started 缓存 | `OnAttackInput`（attackType = LightAttack） |
| ChargedAttack（切图中断） | Hold / canceled（`_isSwitchingMaps == true`） | — 内部标志抑制 | 静默丢失（见 E3） |
| Dash（闪避/冲刺） | Button / performed | ✅ `inputDspTime` | `OnDashInput` |
| Move 方向变化 | Value / started + canceled | ❌（仅方向值和 isStarted 标志） | `OnMoveChanged` |
| Move 持续值 | Value / 按帧查询 | ❌ | `GetCurrentMoveInput()` |
| Pause | Button / performed | ❌ | `OnPauseInput` |
| UI Navigate/Submit/Cancel | UI Action Map | ❌ | `OnUINavigate` / `OnUISubmit` / `OnUICancel` |

**4. Action Map 架构**
F3 维护两个 Action Map，同一时刻只有一个激活：
- **`Gameplay` Action Map**：战斗期间激活——Attack1、Attack2、ChargedAttack、Dash、Move、Pause
- **`UI` Action Map**：菜单/暂停期间激活——Navigate、Submit、Cancel、Back

**5. Action Map 切换接口（F3 主动管理）**
```
EnableGameplayInput()  → 激活 Gameplay Map，禁用 UI Map
EnableUIInput()        → 激活 UI Map，禁用 Gameplay Map（须设置 _isSwitchingMaps，见下）
DisableAllInput()      → 两个 Map 均禁用（过场动画/死亡动画/场景加载，须设置 _isSwitchingMaps，见下）
```
调用方：F6（场景切换完成后调用 `EnableGameplayInput()`）；暂停系统（暂停调用 `EnableUIInput()`，恢复调用 `EnableGameplayInput()`）。

**`EnableUIInput()` 和 `DisableAllInput()` 的 `_isSwitchingMaps` 三步时序**（二者均须遵守）：
① set `_isSwitchingMaps = true`；② 调用 `gameplayMap.Disable()`（Unity Input System 在此调用栈内同步触发所有 active interactions 的 `canceled` 回调，回调在 `Disable()` 返回前完成，此时 `_isSwitchingMaps` 仍为 `true`，E3 三路检查走路径①静默丢失）；③ `Disable()` 返回后，set `_isSwitchingMaps = false`。
> ⚠ **`DisableAllInput()` 必须使用相同的三步时序**——过场动画/死亡动画/场景加载调用 `DisableAllInput()` 时，若玩家正在充能（`_holdPerformed == false`），未设置保护将导致 `canceled` 回调走 E3a 路径，向 C1 广播幽灵 LightAttack。`DisableAllInput()` 的实现须在 `gameplayMap.Disable()` 前后包裹 `_isSwitchingMaps = true/.../= false`，与 `EnableUIInput()` 完全对称。

**6. `InputEventBus` 单例**
F3 通过 `InputEventBus`（`MonoBehaviour` 单例，`DontDestroyOnLoad`）暴露所有输入事件。下游系统禁止直接持有 `PlayerControls` 实例，必须通过 `InputEventBus` 订阅。事件类型为 C# `event`，`InputEvent` 系列结构为 `readonly struct`（避免 GC 分配）。

**7. 重绑定接口分工**
F3 提供 `SaveBindingOverridesAsJson()` / `LoadBindingOverridesFromJson(json)` 接口。FE7（游戏设置系统）在启动时从 F5 读取已保存的绑定 JSON，调用 `LoadBindingOverridesFromJson()` 注入；重绑定完成后 FE7 调用 `SaveBindingOverridesAsJson()` 并将结果交 F5 持久化。**F3 本身不依赖 F5**，F3 是 Foundation 层零依赖系统。

**8. 订阅者生命周期合约**
订阅 F3 事件的系统须在 `OnDestroy()` 中解除订阅，防止场景卸载后的悬挂引用。

**9. 方法组而非 Lambda（IL2CPP 零 GC 合规）**
订阅 F3 事件时须使用**方法组**（`inputBus.OnAttackInput += HandleAttack`），禁止使用捕获 lambda（`inputBus.OnAttackInput += e => HandleAttack(e)`）。IL2CPP 构建（Console/Mobile）下，捕获 lambda 在首次订阅时产生堆分配，破坏 Core Rule 6 的零 GC 承诺。此约束在 Switch/Xbox/PlayStation/iOS/Android 平台下为强制要求。

---

### States and Transitions

| 状态 | 描述 |
|------|------|
| `GameplayActive` | Gameplay Action Map 激活；攻击/移动/闪避输入正常广播 |
| `UIActive` | UI Action Map 激活；游戏输入暂停，UI 导航输入广播 |
| `AllDisabled` | 所有 Action Map 禁用；过场动画、死亡动画、场景加载期间 |

**初始状态**：`InputEventBus.Awake()` 将 InputEventBus 置于 `AllDisabled`——所有 Action Map 均禁用。F6 在场景加载完成后调用 `EnableGameplayInput()` 以进入 `GameplayActive`。此设计确保 F3 在 F6 明确授权前不产生任何输入事件，防止 TDE `InputSystemManagerEventsBased` 与 F3 的 Awake 时序竞争导致状态失同步。

| 来源状态 | 触发条件 | 目标状态 |
|---------|---------|---------|
| `AllDisabled` | `EnableGameplayInput()`（F6 场景加载完成） | `GameplayActive` |
| `GameplayActive` | `EnableUIInput()`（暂停/菜单打开） | `UIActive` |
| `UIActive` | `EnableGameplayInput()`（暂停/菜单关闭） | `GameplayActive` |
| `GameplayActive` / `UIActive` | `DisableAllInput()`（过场/死亡/加载） | `AllDisabled` |

---

### Interactions with Other Systems

| 方向 | 系统 | 数据内容 | 接口类型 |
|------|------|---------|---------|
| F3 **→** C1 | 节奏量化攻击系统 | `AttackInputEvent`（attackType, inputDspTime, inputGameTime, device） | 事件订阅（C1 订阅 `OnAttackInput`） |
| F3 **→** F6/TDE | TDE 集成层 | `DashInputEvent`（inputDspTime, inputGameTime, direction）；持续 `GetCurrentMoveInput()` 查询 | 事件订阅 + 同步查询 |
| F3 **→** UI 系统（P1/P2/P5 等） | 各 UI 系统 | UI Action 事件（Navigate, Submit, Cancel） | 事件订阅（UI Action Map） |
| FE7 **→** F3 | 游戏设置系统 | `LoadBindingOverridesFromJson(savedJson)`（启动时注入）；摇杆死区/触屏灵敏度 | 方法调用 + 属性注入 |
| F3 **→** FE7 | 游戏设置系统 | `SaveBindingOverridesAsJson()` 返回 JSON（重绑定完成后） | 方法调用（FE7 主动拉取） |

---

### `InputEvent` 核心结构（设计层接口合约）

```
AttackInputEvent:
  attackType:          AttackType       // LightAttack | HeavyAttack | ChargedAttack
  inputDspTime:        double           // IDSPClock 在 performed/canceled 回调时的 DSP 时间
                                        // ChargedAttack: 充能完成时刻（or canceled 时刻，若退化为 LightAttack）
                                        // LightAttack/HeavyAttack: 按下时刻
  chargeStartDspTime:  double           // ChargedAttack 专用：Hold.started 时刻（玩家意图时刻）
                                        // 普通 LightAttack/HeavyAttack: -1.0（哨兵值，忽略；与 F2 GetConfirmedDSPStart 保持一致）
                                        // ⚠ E3a 早松开退化 LightAttack：> -1.0（缓存的 Hold.started 时刻）
                                        //    C1 处理 attackType=LightAttack 时须先检查此字段：
                                        //    · chargeStartDspTime > -1.0 → 以此为节奏判定基准（玩家按下时刻，支柱二保障）
                                        //    · chargeStartDspTime == -1.0 → 以 inputDspTime 为判定基准（普通轻攻击）
                                        // C1 应以此字段作为 ChargedAttack 的节奏判定基准
  inputGameTime:       float            // ctx.time 调试用（约 1000s 后精度降至 1ms，仅供开发期诊断）
  deviceType:          InputDeviceType  // enum: Keyboard | Gamepad | Touchscreen
  deviceId:            int              // InputDevice.deviceId（设备唯一 ID，纯值类型，无悬挂引用风险）

DashInputEvent:
  direction:       Vector2          // 闪避方向（来自当前 Move 值或设备方向）
  inputDspTime:    double
  inputGameTime:   float
  deviceType:      InputDeviceType
  deviceId:        int

MoveChangedEvent:
  direction:       Vector2   // 新方向（方向变化时广播；方向途中变化不触发，用 GetCurrentMoveInput() 实时拉取）
  isStarted:       bool      // true = 开始移动; false = 停止
  // 注意：MoveChangedEvent 不携带 DSP 时间戳——移动输入不参与节奏判定，无需 DSP 域精度
```

## Formulas

### F3-F1: 同帧双读时间戳精度约束

**公式**:
```
input_dsp_time_error ≤ input_processing_interval_s
```

**变量定义**:

| 变量 | 类型 | 含义 | 值域 |
|------|------|------|------|
| `input_dsp_time` | double | 在 `InputAction.performed` 回调中，经由 `IDSPClock.GetDspTime()`（生产实现：`FMODDSPClockImpl`）采样的 DSP 时间 | ≥ 0 |
| `actual_hardware_time` | double | 硬件产生此输入事件的真实 DSP 时刻（不可观测） | ≥ 0 |
| `input_dsp_time_error` | double | 采样误差 = `input_dsp_time − actual_hardware_time` | 0 ≤ error ≤ `input_processing_interval_s` |
| `input_processing_interval_s` | float | Unity 处理输入事件的间隔（与帧率强相关） | 0.0083–0.0167（8.3ms @ 120fps，16.7ms @ 60fps） |

**示例计算**:

| 平台 | 帧率 | BPM | 半拍窗口 | F3 误差上限 | 误差占比 | 可行性 |
|------|------|-----|---------|-----------|---------|-------|
| PC/Console | 60fps | 120 | 250ms | 16.7ms | 6.7% | ✅ 可接受 |
| PC/Console | 60fps | 180 | 167ms | 16.7ms | 10% | ✅ 边界可接受 |
| PC/Console | 60fps | 240 | 125ms | 16.7ms | 13.4% | ⚠ 须在 C1 调参时留意 |
| Mobile (Android) | 30fps | 120 | 250ms | 33ms + ~35ms DSP粒度 = ~68ms | 27% | ⚠ C1 须设更宽判定窗口 |
| Mobile (Android) | 30fps | 180 | 167ms | ~68ms | ~41% | ❌ 不建议高 BPM 内容；建议 Android 移动端 BPM 上限 ≤ 150 |
| Mobile (Android) 最坏情况 | 30fps | 180 | 167ms | 33ms + 46ms DSP粒度 = 79ms | 47% | ❌ DSP 粒度取上限 46ms；C1 Perfect 窗口须 ≥ 80ms |
| Mobile (iOS) | 30fps | 180 | 167ms | 33ms + ~6ms DSP粒度 = ~39ms | 23% | ⚠ 须宽判定窗口 |

> Android 的 DSP 缓冲粒度（23-46ms，典型值 ~35ms，最坏情况 46ms）使 F3 在 Android 上的实际误差远超 PC 预测值。C1 须为移动平台设置独立的最小 Perfect 半窗口下限（建议 ≥ 80ms on Android，基于最坏情况 33ms + 46ms = 79ms 取整上调；典型情况 ~68ms）。

**对 C1 的约束**:
- PC/Console：`C1 Perfect 半窗口 ≥ 16.7ms`（60fps F3 误差上限）
- Mobile (iOS)：`C1 Perfect 半窗口 ≥ 39ms`
- Mobile (Android)：`C1 Perfect 半窗口 ≥ 80ms`（基于最坏情况 79ms 取整上调）；建议 BPM 上限 ≤ 150

F3 时间戳误差（≤ 16.7ms @ 60fps PC）比 F2 拍点时间戳误差（< 0.1ms）高约 100 倍——PC 上这是系统误差的主要来源；Android 上 DSP 粒度误差叠加后更高，由 C1 调参时分平台吸收。

---

### F3-F2: 输入-DSP 时间戳关联公式（接口合约）

**公式**（由 C1 实现，F3 仅提供数据）:
```
// 对于 LightAttack / HeavyAttack：
beat_offset_ms = (AttackInputEvent.inputDspTime + latency_offset_s − BeatEventData.beat_dsp_time) × 1000

// 对于 ChargedAttack，C1 应使用 chargeStartDspTime（玩家意图时刻）而非 inputDspTime（完成时刻）：
beat_offset_ms = (AttackInputEvent.chargeStartDspTime + latency_offset_s − BeatEventData.beat_dsp_time) × 1000
```

**变量定义**:

| 变量 | 类型 | 含义 | 来源 |
|------|------|------|------|
| `AttackInputEvent.inputDspTime` | double | F3 在 performed/canceled 回调时采样的 DSP 时间（LightAttack/HeavyAttack 的判定时刻） | F3 广播 |
| `AttackInputEvent.chargeStartDspTime` | double | ChargedAttack 专用：Hold.started 时刻（玩家意图时刻，供 C1 判定使用） | F3 广播 |
| `BeatEventData.beat_dsp_time` | double | F2 广播的拍点 DSP 时间（**已含 LatencyOffsetMs 校正，依 F2 Core Rule 7，处于"玩家感知时间域"**；C1 不得使用原始 beat_dsp_time——误用将导致 latency_offset 双重计算） | F2 广播 |
| `latency_offset_s` | double | 延迟校准偏移量（`FE3.LatencyOffsetMs / 1000.0`），由 C1 从 BeatClock 读取。**当前硬限 `[-0.2, +0.2]`（即 ±200ms，与 F2/F5/entities.yaml 一致）**；超出此范围将使 `beat_offset_ms` 系统性偏移至 C1 判定窗口之外，所有攻击均 Miss。FE3 须在校准 UI 强制限制输入范围，具体值域由 FE3 GDD 定案。| FE3 → F2 → C1 |
| `beat_offset_ms` | double | 攻击输入相对于最近拍点的偏移量（毫秒，正 = 晚于拍点，负 = 早于拍点）；输出范围 (−∞, +∞)，由 C1 的判定窗口截断 | C1 计算 |

> ⚠ **参考系说明（重要，与 F2 Core Rule 7 联动）**：F2 的 `beat_dsp_time` 已叠加 LatencyOffsetMs（处于「玩家感知时间域」），F3 的 `inputDspTime` 是原始 DSP 时间（处于「原始 DSP 时间域」），**未叠加偏移**。C1 须在公式中对 F3 时间戳加回 `latency_offset_s` 以对齐两个时间域，否则在 latency_offset_ms ≠ 0 时（如玩家使用无线耳机），所有判定将系统性偏移 latency_offset_ms 毫秒。当 `latency_offset_ms = 0`（默认值），公式退化为直接相减，结果与上一版本一致。此操作与 F2 Core Rule 7 不矛盾——Rule 7 保证仅消费 F2 时间戳的系统无需补偿；C1 的跨域对齐是 F3-F2 接口合约的固有要求。

> ⚠ **前置条件**：此公式仅在 `BeatClock.IsStartConfirmed == true` 后有效。C1 在首个 BeatEvent 到来前不得计算 beat_offset_ms——`beat_dsp_time` 在此前可能为 0 或哨兵值，导致结果发散至万秒级偏差。

**示例计算**（latency_offset_ms = 80ms，无线耳机场景）:
- F2 广播（已加偏移）：`beat_dsp_time = 11.580s`（= 原始拍点 11.500 + 0.080 偏移）
- 玩家按键，F3 回调采样：`inputDspTime = 11.512s`
- C1 计算：`beat_offset_ms = (11.512 + 0.080 − 11.580) × 1000 = +12ms`（正确：晚于拍点 12ms）
- 若 C1 忘记加 latency_offset_s：`(11.512 − 11.580) × 1000 = −68ms`（错误：误判为早于拍点 68ms）

**⚠ C1 字段选择守卫（防止哨兵值灾难）**：C1 须基于 `attackType` 和 `chargeStartDspTime` 值选择判定时刻，遵循以下规则：

| `attackType` | `chargeStartDspTime` 值 | 场景 | 判定时刻 |
|-------------|----------------------|------|---------|
| `ChargedAttack` | > -1.0 | 正常充能完成 | `chargeStartDspTime`（按下时刻） |
| `LightAttack` | > -1.0 | E3a 早松开退化 | `chargeStartDspTime`（按下时刻，非松开时刻） |
| `LightAttack` | == -1.0 | 普通轻攻击 | `inputDspTime`（按下时刻） |
| `HeavyAttack` | == -1.0 | 普通重攻击 | `inputDspTime`（按下时刻） |

**禁止**在 `chargeStartDspTime == -1.0` 时使用 ChargedAttack 分支——误用将产生约 `+session_time_ms + 1s`（FMOD 初始化后第 1 秒内约 1 秒误差，随后数秒至数千秒递增）的发散偏差，导致所有判定系统性崩溃（无法观测的静默错误）。
> **哨兵值对齐说明**：选择 `-1.0` 而非 `0.0`，因为 FMOD DSP 时钟从 0 初始化；会话开始的第一帧内 `GetDspTime()` 可返回 ~0.0，使 `0.0` 哨兵与合法的充能时刻不可区分。`-1.0` 永远不会是合法 DSP 时间（DSP 时钟单调递增且 ≥ 0），与 F2 的 `GetConfirmedDSPStart()` 哨兵约定保持一致。

**说明**: 此公式由 C1（节奏量化攻击系统）实现。F3 在此记录是为了明确接口语义：（1）`inputDspTime` 与 `BeatEventData.beat_dsp_time` 必须来自同一 `IDSPClock` 实现；（2）C1 必须加入 `latency_offset_s` 修正项；（3）ChargedAttack 须用 `chargeStartDspTime` 而非 `inputDspTime`；（4）E3a 退化 LightAttack 须以 `chargeStartDspTime`（非零时）而非 `inputDspTime` 判定。

## Edge Cases

**E1. 快速连击（同帧多次攻击输入）**
同一帧内玩家触发多次 `Attack.performed`（物理上不可能来自同一按键，但可能来自多设备同时按下）。行为：每次 `performed` 回调独立广播一个 `AttackInputEvent`，携带各自的 `inputDspTime`。F3 不去重、不合并——C1 决定如何处理同帧多输入（通常取第一个）。

**E2. 闪避方向缺失（Move 静止时按 Dash）**
玩家静止时触发闪避，`GetCurrentMoveInput()` 返回 `Vector2.zero`。行为：`DashInputEvent.direction = Vector2.zero`。F3 不补默认方向——TDE 集成层（F6）决定零方向时的闪避行为（如向角色朝向冲刺）。

**E3. Action Map 切换期间的充能攻击中断**
`EnableUIInput()` 调用时，F3 按以下**严格时序**执行：① set `_isSwitchingMaps = true`；② 调用 `gameplayMap.Disable()`——**⚠ Unity Input System 在此调用栈内同步触发所有 active interactions 的 `canceled` 回调**（含 ChargedAttack 的 Hold 交互），回调在 `Disable()` 返回前即执行完毕，执行时 `_isSwitchingMaps` 仍为 `true`（此为正确行为）；③ `Disable()` 返回后，set `_isSwitchingMaps = false`。**禁止在步骤②之前清除标志**——过早清除将使 `canceled` 回调看到 `_isSwitchingMaps == false`，E3 三路检查将错误走 E3a 或 E3b 路径。F3 在 `canceled` 回调中按以下**三路优先级**检查：(1) `_isSwitchingMaps == true` → **静默丢失**（切图中断）；(2) `_holdPerformed == true` → **静默丢失**（充能已完成，松键为正常结束，见 E3b）；(3) 否则（`_holdPerformed == false` 且非切图）→ 退化为 LightAttack 广播（见 E3a）。

**调用方合约**：暂停系统（P5）可在调用 `EnableUIInput()` 前查询 `F3.IsChargedAttackHeld`，决定是否先播放充能取消动画。F3 不负责通知中断，调用方拥有完整控制权。

**E3a. 玩家主动中止充能（提前松开按键）**
玩家按住充能键但在 `charged_attack_hold_duration_s` 到达前松开，触发 `Hold.canceled`（此时 `_isSwitchingMaps == false` **且 `_holdPerformed == false`**）。行为：F3 以 canceled 时刻的 `inputDspTime` 和缓存的 `chargeStartDspTime` 广播一个 `AttackInputEvent`（`attackType = LightAttack`）。C1 按轻攻击逻辑处理，以 `chargeStartDspTime`（按下时刻）为节奏判定基准（见 F3-F2 guard table — `LightAttack + chargeStartDspTime > -1.0` 分支）。

> **E3a 仅在充能未完成时触发**（`_holdPerformed == false`）。充能完成后的松键见 E3b——后者静默丢失，不产生 LightAttack。将早松开映射为轻攻击（而非静默取消）的设计理由：保障支柱二（武侠手感），玩家任何按下行为都有可感知的响应；以 `chargeStartDspTime`（按下时刻/意图时刻）而非 `inputDspTime`（松开时刻）为节奏判定锚点，确保节奏精度基于玩家真实意图——**玩家在按下时刻做出节奏承诺，早松键是充能中止而非新的节奏事件；以意图时刻奖励承诺准确性，与 Pillar 2（武侠手感——每一按键都有分量）保持一致。**
>
> **C1/C2 下游说明**：C1 可通过 `attackType == LightAttack && chargeStartDspTime > -1.0` 区分退化 LightAttack 与普通轻攻击（注意：须使用 `> -1.0` 而非 `> 0`——FMOD DSP 时钟从 0 初始化，会话首帧 `GetDspTime()` 可返回 ~0.0，`> 0` 会在此边缘场景误判；`-1.0` 是 F3 的哨兵值，永不为合法 DSP 时间），选择是否使用不同判定路径或跳过连招推进——此为 C1/C2 的设计决策，须在其 GDD 的 Open Questions 中明确。F3 仅负责忠实传递输入事实（按下 + 提前松开 + 双时间戳）。
>
> **触屏平台中间区（`touch_tap_max_duration_s` < 按压时长 < `charged_attack_hold_duration_s`，默认 300ms–500ms）**：在此区间松开，Tap 交互已超时失败（> 0.3s），Hold 交互尚未触发 `performed`（< 0.5s），`canceled` 回调以 `_holdPerformed == false` 触发——走 E3a 路径退化为 LightAttack。此为触屏平台的预期行为：中间区等同于"充能取消"，以按下时刻（`chargeStartDspTime`）而非松开时刻判定节奏。触屏玩家须了解此行为（建议在 FE7 操作提示中说明）。
>
> **触屏 Tap+Hold 双 `canceled` 回调规范**：触屏攻击按键须将 `TapInteraction` 和 `HoldInteraction` 分别绑定到**独立的 `InputAction`**（Attack1/Attack2 用 Tap Action，ChargedAttack 用 Hold Action），而非在同一 Action 上叠加两种 Interaction。这一架构保证：（1）Tap Action 的 `performed` 广播轻攻击（< 300ms 内松开）；（2）Hold Action 的 `started` → `performed`/`canceled` 独立管理充能状态机——Tap Action 超时 `canceled` 不进入 E3a 路径，E3a 仅由 Hold Action 的 `canceled` 触发。若实现者将 Tap 和 Hold 叠加于同一 Action，须在实现 sprint 中验证 Unity Input System 在中间区松开时仅触发 Hold.canceled（而非同时触发 Tap.canceled 和 Hold.canceled），防止双重 LightAttack 广播。

**E3b. ChargedAttack 充能完成后的正常松键**
玩家充能完成（`Hold.performed` 已触发，`OnAttackInput` 已广播 `ChargedAttack`），随后松开按键，触发 `Hold.canceled` 回调。此时 `_isSwitchingMaps == false`，若没有 `_holdPerformed` 保护，F3 将错误走 E3a 路径再次广播 `AttackInputEvent(LightAttack)`，导致 C1 收到幽灵双重输入（每次充能攻击后跟一次不可见的轻攻击）。

**内部标志 `_holdPerformed` 管理（双重清除原则）**：
- `Hold.started` 触发时：置 `_holdPerformed = false`（防御性重置：确保下一次充能从干净状态开始）
- `Hold.performed` 触发时：置 `_holdPerformed = true`（充能完成标记）
- `Hold.canceled` 触发时（E3 三路检查之后）：无论走哪条路径，回调末尾都将 `_holdPerformed = false`（消耗后立即重置，防止跨充能残留）

**内部字段 `_chargeStartDspTime` 生命周期（防残留规范）**：
- `Hold.started` 触发时：赋值 `_chargeStartDspTime = IDSPClock.GetDspTime()`（记录充能开始时刻）
- `Hold.performed` 触发时：将 `_chargeStartDspTime` 写入广播的 `AttackInputEvent`，**不清零**（E3b 松键后的 `canceled` 回调负责清零）
- `Hold.canceled` 触发时（E3 三路检查之后）：无论走哪条路径（E3 静默丢失 / E3a 退化 LightAttack / E3b 正常松键），回调末尾统一将 `_chargeStartDspTime = -1.0`（重置为哨兵值）
- **Debug 构建断言**：在广播 `LightAttack`/`HeavyAttack` 的非 E3a 路径上，Assert `_chargeStartDspTime == -1.0`，防止残留值静默污染普通攻击的节奏判定

行为：`canceled` 到达时 `_holdPerformed == true`，F3 静默丢失此事件，不广播任何 `AttackInputEvent`，然后清除标志。

**E4. 场景加载期间的输入事件**
`DisableAllInput()` 激活期间（加载/过场），所有 Action Map 禁用，任何硬件输入均不产生 `InputEvent`。玩家在此期间的按键操作全部丢弃。这是预期行为。

**E5. 运行时重绑定冲突（同键多 Action）**
若玩家将 Attack1 和 Dash 重绑定到同一键，Unity Input System 允许此操作但两个 Action 均会 fire。行为：F3 两个 Action 均正常广播（`AttackInputEvent` + `DashInputEvent`）。解决冲突是 FE7（游戏设置系统）的职责——FE7 在重绑定 UI 中显示冲突警告并防止保存冲突绑定。

**E6. FMOD DSP 时钟架构说明（与 Unity AudioListener 无关）**
FMOD Studio 2.x 在独立音频线程运行，DSP 时钟与 Unity 的 `AudioListener` 完全解耦。**`AudioListener.pause = true` 不影响 FMOD DSP 时钟推进**——`FMODDSPClockImpl.GetDspTime()` 在 `AudioListener.pause` 期间仍返回正常递进的时间戳，F3 无需为此场景防御。

**真实风险——FMOD 同步更新模式（`FMOD_INIT_SYNCHRONOUS_UPDATE`）**：若 FMOD 以同步模式初始化，DSP 时钟只在显式调用 `FMOD.Studio.System.Update()`（即 Unity `RuntimeManager.Update()`）时推进；若某帧未调用，该帧内所有输入的 `inputDspTime` 将返回重复值，产生 "零偏移" 的错误时间戳。行为：C1 可通过检测连续两个 DSP 时间戳完全相同来识别此异常，并丢弃或标记该输入。

**架构约束**：本项目须以默认异步模式（`FMOD_INIT_NORMAL`）初始化 FMOD。F6 GDD 须确认 `FMOD.RuntimeManager` 未被配置为同步更新模式。此约束在 Dependencies 节记录。F3 本身不检测此情况——防御由架构保证，F6 初始化合约负责执行。

**E7. 多设备同时连接（键鼠 + 手柄）**
玩家同时连接键盘和手柄，两个设备均可触发输入。行为：`AttackInputEvent.deviceType` 标识实际触发设备类型，`deviceId` 为设备 ID，下游可按需过滤。F3 不区分"主设备"——多设备同时输入时按各自独立事件广播。

**E8. TDE `InputSystemManagerEventsBased` Action Map 状态冲突**
TopDownEngine 4.x 通过 `InputSystemManagerEventsBased` 在角色死亡/重生等生命周期事件中自行调用 `InputActionMap.Enable()`/`Disable()`，与 F3 的状态机独立运作，可能导致 F3 内部状态与实际 Action Map 状态失同步。

**✅ 已解决（2026-07-18，F6 GDD Core Rule 3）**：F6 GDD 选定**方案 A**——F3（`InputEventBus`）是所有 Action Map 状态变更的唯一权威。`JadeRabbitInputManager`（F6 对 TDE `InputSystemManagerEventsBased` 的继承子类）不直接调用 `InputActionMap.Enable()/Disable()`，而是通过 `JadeRabbitInputManager.BindButton()` 将所有输入请求代理给 F3 状态机门控，保留 TDE 的 `AbilityInputAuthorization` 管线。`TDELifecycleAdapter`（F6 生命周期适配器）在场景加载完成后调用 `F3.EnableGameplayInput()`，在暂停/死亡/过场时调用 `F3.EnableUIInput()` / `F3.DisableAllInput()`——这三个方法是唯一允许的外部调用者（见 F6 GDD Core Rule 3，ADR-D1 Accepted）。

## Dependencies

**上游依赖（F3 需要的）**: 无。F3 是 Foundation 层零依赖系统，仅依赖 Unity Engine 内置服务：
- `com.unity.inputsystem`（Unity Input System Package）
- `IDSPClock`（与 F2 共享接口；ADR-D6 已选定 FMOD，生产实现为 `FMODDSPClockImpl`；**两侧必须使用同一实现，否则 F3-F2 公式失效**）

**下游依赖（依赖 F3 的系统）**:

| 下游系统 | 消费的数据 | 接口 |
|---------|-----------|------|
| C1 节奏量化攻击系统 | `AttackInputEvent`（attackType, inputDspTime, **chargeStartDspTime**）；C1 须应用 latency_offset_s 修正（见 F3-F2 公式） | 订阅 `OnAttackInput` |
| C2 连招图系统（**间接，非 F3 直接下游**） | 不直接订阅 F3——攻击 `AttackType` 经 C1 `RhythmJudgmentEvent` 中转（见上行 C1→C2 链）；C2 连招图 `requiredInput` 仅匹配 `AttackType`，**不消费** `DashInputEvent`。参见 `combo-system.md` 上游依赖表（只列 C1/F4/ADR-D5） | 无直接订阅（经 C1 间接） |
| F6 TDE 集成层 | `DashInputEvent`；`GetCurrentMoveInput()` 持续查询；`MoveChangedEvent`（direction, isStarted）；`EnableGameplayInput()` / `EnableUIInput()` / `DisableAllInput()`（`TDELifecycleAdapter` 为唯一外部调用者，见 E8）| 订阅 + 同步查询 + 状态控制 |
| FE7 游戏设置系统 | `SaveBindingOverridesAsJson()` / `LoadBindingOverridesFromJson(json)` | 方法调用（FE7 主动调用 F3） |
| UI 系统（P1/P2/P5 等） | UI Action 事件（Navigate, Submit, Cancel） | 订阅 UI Action Map 事件 |

**F3 ↔ FE7 ↔ F5 三角关系（边界合约）**:
F3 本身不依赖 F5（存档系统）。键位重绑定数据的持久化通过 FE7 中转：
1. 启动时：FE7 从 F5 读取已保存的绑定 JSON → 调用 `F3.LoadBindingOverridesFromJson(json)`
2. 重绑定后：FE7 调用 `F3.SaveBindingOverridesAsJson()` → 将返回的 JSON 交 F5 持久化

**架构约束（E6 相关）**:
FMOD DSP 时钟不受 `AudioListener.pause` 影响（见 E6）。F3 的时间戳精度依赖 FMOD 以异步模式（`FMOD_INIT_NORMAL`）运行，且 `RuntimeManager.Update()` 在每帧 Unity PlayerLoop 中被正常调用。**F6 GDD 须确认 FMOD 初始化配置**——若以同步模式启动，F3 的 DSP 时间戳将在无 `RuntimeManager.Update()` 的帧内停滞，C1 的节拍判定将产生系统性误差。此为 F6 初始化合约的强制要求，不依赖 F3 的运行时检测。

**延迟校准更新时序约束**：`latency_offset` 变更在玩家打开设置菜单期间发生（此时 Action Map 处于 `UIActive`，Gameplay 禁用）。变更在下一次 `EnableGameplayInput()` 调用后生效，确保 F3 广播的时间戳与 C1 读取的偏移量始终来自同一校准快照——不存在战斗中途校准值突变的竞态条件。设置系统（FE7）禁止在 `GameplayActive` 状态下更新 `latency_offset`。

**⚠ ADR-D6 阻塞说明**：F3 的 `IDSPClock` 实现选型依赖 ADR-D6（FMOD vs Unity Audio）。ADR-D6 必须在 F3 进入实现 sprint 之前完成定案。设计层无需等待 ADR-D6，但实现层时钟源必须与 F2 保持一致。

**✅ F6 GDD 决策（E8 已解决）**：F6 GDD（2026-07-18，Core Rule 3）已定案 Option A：F3 为唯一 Action Map 权威，`TDELifecycleAdapter` 是 `EnableGameplayInput()` / `EnableUIInput()` / `DisableAllInput()` 的唯一外部调用者。F3 状态机实现可直接依据此合约。

## Tuning Knobs

| 参数名 | 类型 | 默认值 | 安全范围 | 影响的玩法维度 | 调整方式 |
|--------|------|--------|---------|--------------|---------|
| `gamepad_deadzone_inner` | float | 0.125 | 0.05–0.25 | 手柄摇杆内死区（**圆形幅值死区**，非轴向独立）——过小则漂移，过大则方向迟钝 | FE7 设置菜单 / PlayerInfoSO |
| `gamepad_deadzone_outer` | float | 0.925 | 0.85–0.99 | 手柄摇杆外死区（圆形）——过小则无法满推，过大则边缘灵敏度失真 | FE7 设置菜单 / PlayerInfoSO |
| `touch_tap_max_duration_s` | float | 0.3 | 0.15–0.5 | 触屏轻击最大时长——区分 Tap（轻攻击）与 Hold（充能攻击） | FE7 设置菜单 / PlayerInfoSO |
| `charged_attack_hold_duration_s` | float | 0.5 | 0.3–1.0 | 充能攻击 Hold 阈值——按住多久触发 `Hold.performed`（`chargeStartDspTime` 在 `Hold.started` 时已捕获，C1 使用 start 时刻判定节奏） | FE7 设置菜单 / 由 C1 GDD 最终确定 |

**参数间不变量（必须同时满足，违反时 Debug 构建断言失败，Release 构建输出 `Debug.LogError`）**:
- `gamepad_deadzone_inner < gamepad_deadzone_outer`（违反将导致死区逻辑反转——全部输入被死区吞噬或所有输入均视为满推，角色移动彻底失效）
- `touch_tap_max_duration_s < charged_attack_hold_duration_s`（违反将使轻攻击在触摸端无法触发——任何按压都在到达 Tap 上限前已转为充能）
- 以上不变量由 FE7 在写入设置时前置校验，F3 在 `Awake()` 注入时再次断言。

**说明**:
- 所有调参值通过 FE7（游戏设置系统）持久化到 F5，启动时注入 F3。F3 本身不访问 F5。
- `charged_attack_hold_duration_s` 的设计值由 C1 GDD 根据 BPM 和半拍窗口最终确定；F3 仅负责接收并配置到 Unity Input System 的 Hold Interaction 上。
- **输入缓冲（已移除本表）**：输入缓冲为消费端（C1/C2）职责，不由 F3 实现。F3 的职责是精确捕获并即时广播——在 F3 端引入缓冲会使 `inputDspTime` 与广播时刻解耦，产生误导性的时间戳。请参见 C1 GDD 的输入缓冲设计。
- **死区形状说明**：采用圆形幅值死区（`StickDeadzoneProcessor`），内死区内返回 `Vector2.zero`，超出外死区重映射为满推（1.0）。轴向死区（`AxisDeadzoneProcessor`）会产生斜向偏置，不符合武侠手感要求。

## Visual/Audio Requirements

F3 无直接视觉或音频输出——它只捕获输入并广播事件。所有输入反馈（按键视觉效果、音效）由下游系统负责：C1 判定反馈通过 Feel/MMFeedbacks 反馈序列播放，F3 不参与。

## UI Requirements

F3 通过 UI Action Map 广播 Navigate/Submit/Cancel 事件，UI 系统负责具体画面和交互。FE7 设置菜单中的键位重绑定 UI 不属于 F3 职责——FE7 调用 F3 的 `SaveBindingOverridesAsJson()` / `LoadBindingOverridesFromJson()` 接口，F3 不感知 UI 层。

## Acceptance Criteria

**AC-F3-01: 同帧双读时间戳捕获**
在 `InputAction.performed` 回调中，`AttackInputEvent.inputDspTime` 必须等于回调执行时 `IDSPClock.GetDspTime()` 的返回值，`inputGameTime` 必须等于 `ctx.time`。验证方法：单元测试，注入实现 `IDSPClock` 的 Mock 时钟（可控返回值），触发 Mock InputAction 回调，断言 `inputDspTime == mockClock.GetDspTime()` 且 `inputGameTime == ctx.time`。（IDSPClock 须可通过属性注入或方法注入完成依赖替换——MonoBehaviour 不支持构造函数注入；测试 Setup 须在 Awake 执行前注入 Mock 实现。）

**AC-F3-02: DSP 时间戳采样时序约束**
`inputDspTime` 必须是在 `InputAction.performed` 回调执行时同步调用 `IDSPClock.GetDspTime()` 得到的值——不得提前采样、延迟采样或缓存自上一帧。验证方法：单元测试，注入受控 Mock 时钟，在 performed 回调触发前后将时钟步进不同值，断言 `inputDspTime` 等于回调执行时刻的 `mockClock.GetDspTime()`，而不等于回调前或回调后的时钟值。
> 精度上限（架构约束，非可测项）：F3 时间戳误差上限由帧率决定——60fps 时 ≤ 16.7ms，30fps 时 ≤ 33ms（Android 另叠加 DSP 缓冲粒度）。此约束由 F3-F1 公式记录，不以自动化测试验证（真实硬件时刻不可观测）；C1 调参时须在判定窗口中为此误差留余量。

**AC-F3-03: AttackInputEvent 广播完整性**
Attack1/Attack2 的 `performed` 回调触发时，`OnAttackInput` 必须广播 `AttackInputEvent`（`attackType` 正确、`inputDspTime` = 按下时刻、`inputGameTime` 正确、`deviceType`/`deviceId` 正确、`chargeStartDspTime == -1.0`（哨兵值））。ChargedAttack 的 `Hold.performed` 回调触发时，广播的 `AttackInputEvent` 必须同时包含 `inputDspTime`（充能完成时刻）和 `chargeStartDspTime`（`Hold.started` 缓存的充能开始时刻），且满足：① `chargeStartDspTime > -1.0`（**哨兵值守卫**——确认 `Hold.started` 时确实调用了 `GetDspTime()`，未保留哨兵值；若此断言失败，则 C1 的 guard table 将静默路由至错误分支，导致节拍判定系统性崩溃）；② `chargeStartDspTime ≤ inputDspTime`（若两者相等，表明 Hold 在同一 `InputSystem.Update()` 周期内完成，属正常边界情况——FMOD DSP 粒度在 Android 最坏情况 46ms 内可能使两次采样返回相同值）。验证方法：单元测试，分别模拟三种攻击类型输入，断言各字段值正确（含上述两个 ChargedAttack 专属断言）。

**AC-F3-04: DashInputEvent 广播完整性**
Dash 的 `performed` 回调触发时，`OnDashInput` 事件必须广播一个 `DashInputEvent`，`direction` 取自当前 Move 值。验证方法：单元测试，设置 Move 值后触发 Dash，断言 `direction` 与 Move 值一致。

**AC-F3-05: MoveChangedEvent 广播正确性**
Move Action 的 `started` 回调广播 `MoveChangedEvent`（`isStarted = true`，携带方向值），`canceled` 回调广播 `MoveChangedEvent`（`isStarted = false`）。`MoveChangedEvent` 结构不包含任何 DSP 时间戳字段——移动输入不参与节奏判定。验证方法：单元测试，模拟 Move started/canceled，断言 `isStarted` 标志和 `direction` 正确；编译期验证 `MoveChangedEvent` 结构无 `inputDspTime` 字段。

**AC-F3-06: GetCurrentMoveInput() 同步查询**
`GetCurrentMoveInput()` 在每帧调用时返回当前 Move Action 的 `ReadValue<Vector2>()`。验证方法：单元测试，设置 Move 值，断言查询结果匹配。

**AC-F3-07: Action Map 状态机——GameplayActive → UIActive**
调用 `EnableUIInput()` 后，Gameplay Map 禁用、UI Map 启用。此后攻击/移动/闪避输入不产生事件，UI 导航输入正常广播。验证方法：PlayMode 测试，切换到 UI Map 后发送 Gameplay 输入，断言无事件广播。

**AC-F3-08: Action Map 状态机——DisableAllInput()**
调用 `DisableAllInput()` 后，所有 Map 禁用，任何输入均不产生事件。验证方法：PlayMode 测试，禁用全部后发送输入，断言无事件广播。

**AC-F3-09: Action Map 状态机——AllDisabled → GameplayActive**
调用 `EnableGameplayInput()` 后，Gameplay Map 启用、UI Map 禁用。攻击/移动/闪避输入恢复广播。验证方法：PlayMode 测试，从 AllDisabled 恢复后发送攻击输入，断言事件广播。

**AC-F3-21: Action Map 状态机——UIActive → GameplayActive（暂停恢复流程）**
从 `UIActive` 状态调用 `EnableGameplayInput()` 后，Gameplay Map 启用、UI Map 禁用；攻击/移动/闪避输入恢复广播，UI 导航输入不再产生事件。验证方法：PlayMode 测试，(1) 先调用 `EnableUIInput()` 进入 `UIActive`；(2) 发送攻击输入，断言 `OnAttackInput` 未触发；(3) 调用 `EnableGameplayInput()`；(4) 再次发送攻击输入，断言 `OnAttackInput` 触发；(5) 发送 UI Navigate 输入，断言 `OnUINavigate` 未触发。此测试覆盖最高频的状态转换路径（暂停→恢复），与 AC-F3-07（GameplayActive→UIActive）形成对称覆盖。

**AC-F3-10: 重绑定 JSON 往返**
`SaveBindingOverridesAsJson()` 返回的 JSON 传入 `LoadBindingOverridesFromJson()` 后，所有绑定覆盖恢复原状。验证方法：单元测试，修改一个绑定，保存 JSON，重新加载，断言绑定一致。

**AC-F3-22: 重绑定 JSON 往返幂等性 + 空状态往返 + 无文件 I/O**
`ExportBindingOverrides()`→`ImportBindingOverrides()`→再 `ExportBindingOverrides()` 产出 JSON 与首次等价（save→load→save 幂等，二次导出内容一致）；无覆盖时空状态往返仍为等价空集（不产生虚假条目）；`ImportBindingOverrides()` 全程无文件 I/O（磁盘持久化归 F5）。验证：(1) 设 override→导出 J1→导入→再导出 J2，断言 J1==J2；(2) 空 override 往返仍为空集；(3) Mock 文件系统/反射断言 `System.IO.File.*` 未被调用。

**AC-F3-11: 零 GC 分配**
`InputEvent` 系列结构（`AttackInputEvent`、`DashInputEvent`、`MoveChangedEvent`）为 `readonly struct`，不含引用类型字段（`deviceType: InputDeviceType` 为 enum，`deviceId: int` 为值类型），事件广播路径无堆分配。验证方法：PlayMode 测试，使用 `ProfilerRecorder`（`ProfilerCategory.Memory`）在 100 帧连续输入期间记录 GC 分配，断言 InputEventBus 广播路径的 GC Alloc = 0B。**前置条件（两步，缺一不可）**：① 测试 Setup 中须显式调用 `Profiler.enabled = true` 并验证 `recorder.IsRunning == true`——CI `-batchmode` 下 Profiler 默认未激活，未满足时测试必须 Fail 而非 Skip；② **Marker 名称自验证**：Setup 中先执行一段已知会分配内存的代码路径（如 `_ = new object()`），断言 `recorder.CurrentValue > 0`——若断言失败，测试必须 Fail 并输出 `"ProfilerRecorder marker名称可能与当前Unity版本不符；候选名称：'GC.Alloc'（点号）或 'GC Alloc'（空格），请在Unity 6.3 LTS实机验证"`，禁止 Skip 或继续执行主断言（防止 marker 名称错误导致的静默假阳性）。（注意：订阅者须使用方法组而非捕获 lambda，以保证 IL2CPP 构建下零 GC 承诺成立。）

**AC-F3-12: 订阅者生命周期安全**
场景卸载后，已销毁的 MonoBehaviour 订阅者不会导致 `NullReferenceException`。验证方法：PlayMode 测试，订阅事件 → 销毁 GameObject → 触发输入，断言无异常。

**AC-F3-25: 广播循环异常隔离 + `event Action<TInputEvent>` 精确泛型无装箱**
(1) 广播列表存在已 `Destroy` 但未对称退订的订阅者，其委托调用抛异常（如 `MissingReferenceException`）时广播循环不中断，其后订阅者全部收到事件（漏收==0）；(2) `event Action<AttackInputEvent>`（及 `DashInputEvent`/`MoveChangedEvent`）声明与 `Invoke` 路径中 `readonly struct` 负载与委托泛型精确匹配（非 `Action<object>` 或经 object 中转），无隐式装箱（IL2CPP/Mono 均验证）。验证：(1) 注册 3 订阅者，中间者未退订即 `Destroy` 且触发抛异常，断言其余 2 者各收 1 次；(2) 反射检查 event 泛型参数 + IL 层确认无 `box` 指令（或 `ProfilerRecorder` 交叉验证该路径 0 分配）。

**AC-F3-13: 多设备并发（deviceType / deviceId 正确性）**
同时连接键盘和手柄时，两个设备的输入均独立广播 `InputEvent`，`deviceType` 正确标识设备类型（`Keyboard` / `Gamepad`），`deviceId` 为对应 `InputDevice.deviceId` 的整数值。验证方法：PlayMode 测试，使用 `InputTestFixture`（`InputSystem.AddDevice<Keyboard>()` + `InputSystem.AddDevice<Gamepad>()`）在同帧从两设备触发输入，断言收到两个独立事件且 `deviceType` / `deviceId` 字段均正确。

**AC-F3-23: F3 对设备无仲裁（交替触发广播次数守恒 + 无「活动设备」锁定状态）**
键鼠与手柄交替触发同一 Action，F3 不锁定活动设备、不因切换丢弃/合并——广播次数恰等于物理触发次数。反射/静态断言 `InputEventBus`（及状态机类）内无「当前活动设备」持久化字段或锁定逻辑。验证：(1) 键盘 3 次+手柄 3 次+交替 4 次，总广播==10；(2) 反射断言无设备锁定语义字段。

**AC-F3-24: 多设备并发广播路径零 GC**
两虚拟设备（`InputTestFixture` 的 Keyboard+Gamepad）同会话交替触发 N 次后，`ProfilerRecorder(ProfilerCategory.Memory,"GC Allocated In Frame")` 稳态帧（排除首帧 JIT）堆分配==0B（复用 AC-F3-11 零 GC 协议，聚焦多设备交替路径，`deviceType`/`deviceId` 解析与事件构造不引入额外分配）。验证：同 AC-F3-11 前置（Profiler 激活 + marker 自验证），预热后稳态交替触发 N 次断言零分配。

**AC-F3-14: ChargedAttack 切图中断静默丢失（E3）**
`EnableUIInput()` 调用导致 Gameplay Map 禁用期间，若 ChargedAttack 正在 Hold 中，`canceled` 回调必须静默丢失（不广播任何 `AttackInputEvent`）。验证方法：单元测试，启动 ChargedAttack Hold → 调用 `EnableUIInput()` → 断言：① `OnAttackInput` 未触发；② 通过测试可观测内部状态（Test Double 或反射）确认 `_isSwitchingMaps` 在 `canceled` 回调执行期间为 `true`（即 E3 三路检查走路径①，而非路径②或③）——此断言确保是标志抑制机制生效，而非其他原因（如 Action Map 在回调前已禁用）导致静默，防止不同 bug 产生相同可观测输出。

**AC-F3-15: 重绑定冲突双广播（E5）**
同键绑定 Attack1 和 Dash 时，单次按键同时触发 `AttackInputEvent`（`attackType = LightAttack`）和 `DashInputEvent`，两者均广播到各自接口。验证方法：单元测试，手动设置冲突绑定，触发按键，断言 `OnAttackInput` 和 `OnDashInput` 各收到一次事件。

**AC-F3-17: ChargedAttack 充能完成后松键不产生幽灵 LightAttack（E3b）**
`Hold.performed` 触发后玩家松开按键，触发 `Hold.canceled`。F3 必须静默丢失此 `canceled` 事件——`OnAttackInput` 不得因此广播任何 `AttackInputEvent`。验证方法：单元测试，(1) 触发 `Hold.started` 和 `Hold.performed` 回调（ChargedAttack 正常完成）；(2) 随即触发 `Hold.canceled` 回调（`_isSwitchingMaps == false`）；断言 `OnAttackInput` 总共仅触发一次（ChargedAttack），`_holdPerformed` 在 `canceled` 末尾已清除为 `false`。同时验证：若再次进行一次完整充能流程，第二次 `Hold.performed` 正常广播（`_holdPerformed` 已被正确重置，无残留）。

**AC-F3-18: E3a 早松开退化 LightAttack 完整路径**
玩家触发 `Hold.started`（`_chargeStartDspTime` 被记录为非零值），在 `Hold.performed` 触发前松开按键（`_isSwitchingMaps == false`，`_holdPerformed == false`），触发 `Hold.canceled`，走 E3a 路径。验证方法：单元测试，(1) 触发 `Hold.started`，记录 `expectedChargeStart = mockClock.GetDspTime()`；(2) 步进时钟；(3) 触发 `Hold.canceled`（跳过 `Hold.performed`，`_isSwitchingMaps == false`，`_holdPerformed == false`）；断言：① `OnAttackInput` 恰好触发 1 次；② `attackType == LightAttack`；③ `chargeStartDspTime == expectedChargeStart`（非哨兵值，即 ≠ -1.0；注：0.0 是合法的首帧 FMOD 时间，guard table 以 `> -1.0` 判断而非 `> 0`）；④ `inputDspTime == Hold.canceled 时刻的 IDSPClock.GetDspTime()` 返回值；⑤ 回调结束后 `_chargeStartDspTime` 已重置为哨兵值 `-1.0`（通过后续断言验证下一次普通 LightAttack 的 `chargeStartDspTime == -1.0`）。

**~~AC-F3-16~~（已移至 Deferred ACs 节）**

> 此 AC 测试的是 P5（暂停菜单）的调用时序，而非 F3 本身的行为。P5 GDD 未创建前无法实现。详见文末「Deferred ACs」节。

**AC-F3-19: `InputEventBus` 单例重复实例防御**
若场景加载或 Prefab 实例化导致第二个 `InputEventBus` 对象创建，`Awake()` 必须检测到已存在的实例，输出 `Debug.LogError`，并对自身调用 `Destroy(gameObject)`——仅保留最早创建的实例，不产生双重事件广播。验证方法：PlayMode 测试，手动实例化第二个 `InputEventBus` Prefab，断言：① `Debug.LogError` 日志含 "duplicate" 或 "already exists" 字样；② `Object.FindObjectsOfType<InputEventBus>().Length == 1`；③ 触发攻击输入后 `OnAttackInput` 仅收到一次事件。

**AC-F3-20: `InputEventBus.Awake()` 初始状态为 `AllDisabled`**
`InputEventBus` 初始化完成后，所有 Action Map 均处于禁用状态；在 `EnableGameplayInput()` 显式调用之前，任何硬件输入均不触发 `OnAttackInput`、`OnDashInput` 或 `OnMoveChanged`。验证方法：PlayMode 测试，实例化 `InputEventBus` 后不调用 `EnableGameplayInput()`，直接发送攻击/移动输入，断言所有三个事件均未触发；随后调用 `EnableGameplayInput()`，再次发送输入，断言事件正常广播（确认恢复路径可通）。

## Open Questions

1. **触屏虚拟手柄 Action Map 归属**: 触屏虚拟摇杆/按钮是否需要独立的 `Touch` Action Map，还是复用 `Gameplay` Map 的 Touch Control Scheme？待 FE7（游戏设置系统）GDD 确认。
2. **输入缓冲归属**: `input_buffer_window_s` 是由 F3 在广播前缓冲（延迟发送），还是完全交给 C1/C2 在接收端实现？当前 F3 预设值为 0（不缓冲），待 C1 GDD 确认。
3. **ChargedAttack Hold 时长最终值**: F3 预设 `charged_attack_hold_duration_s = 0.5s`，C1 GDD 可能根据 BPM 和半拍窗口调整此值。
4. **MoveChangedEvent 方向中途变化对 C2 的影响**: `MoveChangedEvent` 当前设计中，方向途中变化（如从左变为右）不触发新事件，须由 C2 通过 `GetCurrentMoveInput()` 轮询感知。若 C2 的连招图需要区分"停止后重新移动"与"方向瞬间反转"，F3 当前接口可能不足——须在 C2 GDD 中确认。

## Deferred ACs

以下 AC 因依赖尚未设计的系统而延期，不阻塞 F3 实现 sprint：

**DeferredAC-F3-16: FMOD 初始化模式验证（E6 架构约束）**
⚠ 原前提（`AudioListener.pause` 冻结 DSP 时钟）已更新——FMOD 运行在独立音频线程，`AudioListener.pause` 不影响 FMOD DSP 时钟（见 E6）。本 AC 重新定位为 FMOD 同步模式保护：
- **F6 初始化合约**：FMOD `RuntimeManager` 必须以默认异步模式（`FMOD_INIT_NORMAL`）初始化——禁止使用 `FMOD_INIT_SYNCHRONOUS_UPDATE`。
- **集成测试**（延期至 F6 GDD 完成）：验证 F6 初始化后，连续两帧的 `IDSPClock.GetDspTime()` 返回值递增（而非相等），确认 FMOD 处于异步更新模式。Owner: F6 GDD 作者。
