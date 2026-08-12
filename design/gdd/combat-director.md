# 战斗导演 (Combat Director)

> **Status**: Approved（SC-3 令牌回收补丁 + SG-3 复审回归，2026-08-12；与 systems-index 一致）
> **Author**: Jadyn Wu + Claude Code Agents
> **Last Updated**: 2026-08-12
> **Implements Pillar**: 支柱一：节奏即战斗 (Rhythm IS Combat)
> **System ID**: C4
> **Layer**: Core | **Milestone**: MVP | **Design Order**: 7

## Overview

C4 战斗导演（Combat Director）是《玉兔：破阵》敌人攻击落拍的**中央调度基础设施**——它是 game-concept 规则四「敌人强制落拍」的架构强制点。所有敌人攻击不得自行决定出手时机：敌人 AI（FE1，经 ADR-D2 定义的 `CombatDirectorTokenAction` 节点）向 C4 发起**攻击令牌请求**，C4 订阅 F2 音频/拍点时钟的拍点事件流，按拍槽（beat-slot）调度策略决定在哪一拍将令牌授予哪个敌人，并在授予时回调敌人的 `OnTokenGranted()`。令牌是攻击的唯一通行证——没有 C4 授权，任何敌人攻击能力都不会触发。C4 因此同时承担两个职责：**落拍保证**（每次攻击的判定帧对齐音乐拍点）与**节奏编排**（把并发敌人的攻击分散到不同拍槽，避免同拍齐射造成的听觉噪音，形成可被玩家「听前摇」预读的攻击潮汐）。

C4 本身对玩家不可见，但它直接产出玩家可感知的战斗质感：敌人的每一次跳砍、突刺都恰好落在鼓点上，使「用耳朵听敌人前摇」从玄学变成可靠的防御直觉，支撑支柱一「节奏即战斗」中「听音乐防御」的核心策略。C4 是 F2 拍点时钟的纯下游消费者——它消费已含延迟偏移的 `BeatEventData.beat_dsp_time` 时间戳，自身不做延迟补偿，也不触碰 BeatClock 生命周期（受 `beatclock_lifecycle_bypass` 约束）。令牌授予协议的接口契约由 ADR-D2（Enemy AI Framework）锁定，本 GDD 定义其调度行为与令牌分配规格；Boss 战（FE4）的拍号切换阶段编排亦由 C4 的令牌密度调整承接。

## Player Fantasy

**核心情感**：「战场是有拍子的，危险是可以被听见的。」

玩家永远不会知道「战斗导演」这个系统的存在——这正是设计的意图。C4 的成功标准是**隐形**：玩家感受到的不是一个调度器，而是一个仿佛「懂音乐」的敌群。当鼓点起、贝斯落，敌人的长枪恰好在这一拍探出，玩家的身体先于理智做出闪避——这种「跟着音乐读战斗」的直觉，是支柱一「节奏即战斗」承诺给玩家的核心体验。

**玩家时刻（锚点）**：一名玩家第一次在混战中被三个敌人包围，起初手忙脚乱。几个小节过去，他忽然「听懂」了——敌人的攻击不是随机砸下来的噪音，而是分散在不同拍点上的、有序的攻击潮汐。他不再盯着每个敌人的动画看，而是**闭着眼睛听鼓点**就能预判下一次威胁从哪个方向、在哪一拍到来。慌乱变成了舞蹈。这一刻，玩家从「看敌人」升级到「听战场」，正是 C4 在幕后编排的结果。

**反面（C4 失败时玩家会感到什么）**：如果没有 C4，敌人各自为战、攻击时机随机，玩家会感到战斗「嘈杂、没有节奏、无法预读」——落拍防御沦为运气游戏，支柱一崩塌。C4 存在的意义，就是把这种潜在的混乱转化为可被玩家掌握的秩序。C4 不提供「你操作它的爽感」，它提供的是**「整个战场都在为你的节奏感服务」的沉浸底座**。

## Detailed Design

### Core Rules

1. **令牌是攻击的唯一通行证**（game-concept 规则四强制点）：敌人的攻击能力在 BT 中必须置于 `CombatDirectorTokenAction` 节点之后；无令牌不得激活任何攻击 hitbox。此规则是「敌人强制落拍」的架构落点。
2. **请求（Request）**：敌人 BT 到达 `CombatDirectorTokenAction` → 调用 `CombatDirector.RequestAttackToken(enemy)`，节点进入 `Status.Running` 阻塞。C4 将请求入队至 pending queue，记录请求时的拍号 `t_req`。
3. **拍边界评估**：C4 订阅 F2 `OnBeatLookahead`（提前若干拍预告即将到来的拍）。每次收到 lookahead，C4 在该拍的**拍槽（beat-slot）**上评估 pending queue，决定本拍授予哪些令牌。
4. **拍槽容量（拍号派生）**：每个拍的可授令名额由当前拍号 + 拍内位置推导——强拍（downbeat）容量高、弱拍容量低（见 Formulas `SlotCapacity`）。`OnMeterChanged` 触发容量表重算。
5. **授令排序（优先级 + 防饥饿）**：pending queue 按 `Priority` 降序授令，名额满即止。`Priority = 威胁权重（距玩家越近越高） + 饥饿补偿（等待拍数 × starvationWeight）`；等待超过 `starvationCeiling` 拍的请求提到最高优先级，强制授令，杜绝远处敌人永久饥饿。
6. **授予（Grant）**：名额内请求 → C4 调用 `enemy.OnTokenGranted(targetLandingBeat)`，节点转 `Status.Success`。敌人前摇 `W` 拍（`enemy_attack_windup_beats`），命中帧落在 `grantBeat + W`——因 W 以拍为单位，**落拍为数学必然**。
7. **目标落地拍（可配置细分）**：默认命中落在整拍；`subdivisionMode`（Tuning Knob）可切换为半拍 / 仅强拍。C4 只在 eligible 目标拍上安排落地，保证 hitbox 激活帧对齐 eligible beat。
8. **超时（Timeout）**：敌人未在 `TimeoutBeats`（每原型可配置）拍内兑现攻击，C4 撤销令牌、归还名额，节点返回 `Status.Failure`，敌人 BT 走 fallback 分支（重新索敌/位移）。
9. **兑现与释放（Consume/Release）**：敌人攻击 hitbox 激活即视为「兑现」，C4 标记令牌 `Consumed` 并释放该拍槽占用；攻击结束后敌人可再次请求。
10. **F2 纯下游消费者**：只读 `BeatEventData.beat_dsp_time`，禁用 `AudioSettings.dspTime`（F2 Core Rule 12/14）；禁止调用 BeatClock 生命周期（`beatclock_lifecycle_bypass`）；无延迟补偿（F2 Core Rule 7）。
11. **单线程**：C4 令牌逻辑运行于主线程 Update 轮询（ADR-D6），无跨线程队列、无同步原语。
12. **run-end 令牌回收（SC-3，对称 C1 SC-2；Death + Victory，ADR-D13）**：C4 订阅 F6 `TDELifecycleAdapter` 广播的域事件 **`OnRunEnded(RunOutcome outcome)`**（CombatCore 可见的纯 C# `event`，负载为纯 C# `enum` `RunOutcome`，MVP 取值 `{Death, Victory}`，**不含任何 TopDownEngine 类型**——C4 与 C1 同受 TDE 边界合约约束，**禁止 `using` TopDownEngine 命名空间**，不直接订阅 TDE 事件；`OnRunEnded` 取代原 `OnPlayerDeath`，ADR-D13 落地方式 (a)，不保留别名）。收到 `OnRunEnded`（**任一 outcome**——`RunOutcome.Death` 或 `RunOutcome.Victory` 均触发）时，在 F6 广播的**同一 Update 帧内**（F6 同帧执行 `BeatClock.Stop()` + `DisableAllInput()` + 广播 `OnRunEnded(outcome)`，见 `tde-integration.md` Rule 15）取消**全部**令牌：所有 `Pending` 与 `Granted` 令牌一律 `→ Cancelled → None`，清空 pending queue，释放所有拍槽占用记录。**理由**：死亡与胜利同为 run-end（ADR-D13 统一 run-end 边界，落地方式 (a)）——两者 MVP 均 = 换新局（ADR-D12/ADR-D13），均走 `BeatClock.Stop()` 路径，F2 Rule 10 将 `global_beat_index` **归零**；Granted 令牌的绝对 `targetLandingBeat`（F-5，全局绝对拍计数）跨 `Stop()→换新局 Play()` 失锚，落拍数学必然性（规则 6）被打破 → 幽灵攻击 / 时机错乱——胜利分支与死亡分支同源失锚，无一幸免。E6 的「冻结-续接」**仅适用于 Pause/Resume 连续路径**（菜单暂停，时钟连续），**不适用于 run-end 的 Stop 不连续路径**（无论 Death 或 Victory）。C4 **不**调用任何 BeatClock 生命周期方法（`beatclock_lifecycle_bypass` 保持，只订阅广播、不回调时钟）。换新局时 C4 为新 Single 场景重建的实例；本清空合约同时保证「持有持久 C4 引用」实现下的干净起点。C4 **不需要**订阅 `OnRespawnComplete`（MVP 该事件不触发，见 ADR-D12；换新局由场景重建承接）。

### States and Transitions

每个令牌请求的生命周期状态机：

| 状态 | 含义 | 进入条件 |
|---|---|---|
| `None` | 无请求 | 初始 / 释放后 |
| `Pending` | 已请求，排队等待授令 | `RequestAttackToken()` 被调用 |
| `Granted` | 已授令，敌人前摇中 | 拍槽评估选中，`OnTokenGranted()` 已回调 |
| `Consumed` | 攻击已兑现 | 攻击 hitbox 激活 |
| `Expired` | 超时撤销 | `TimeoutBeats` 内未兑现 |
| `Cancelled` | 敌人死亡 / 被击退打断 / BT 中断 | 外部事件 |

**合法转移**：

| From → To | 触发 |
|---|---|
| `None → Pending` | 敌人请求令牌 |
| `Pending → Granted` | 拍槽有名额且优先级选中 |
| `Pending → Cancelled` | 排队中敌人死亡 / BT 中断 |
| `Granted → Consumed` | 前摇结束，hitbox 激活 |
| `Granted → Expired` | `TimeoutBeats` 内未激活 hitbox |
| `Granted → Cancelled` | 前摇中被击退 / 硬直打断 / 死亡 |
| `Consumed/Expired/Cancelled → None` | 令牌释放，敌人可重新请求 |

非法转移（如 `Pending → Consumed` 跳过授令）视为契约破坏，Editor 下 `Debug.Assert` 拦截。

### Interactions with Other Systems

| 系统 | 方向 | 数据流 | 接口所有者 |
|---|---|---|---|
| **F2 音频/拍点时钟** | 上游 | C4 订阅 `OnBeatLookahead`（timestamp + 小节号 + 拍内索引）、`OnMeterChanged`；只读 `beat_dsp_time` | F2 拥有事件签名 |
| **FE1 敌人 AI** | 下游 | `CombatDirector.RequestAttackToken(enemy)` → `enemy.OnTokenGranted(targetLandingBeat)`；`BeatClockBridge`（DefaultExecutionOrder −100）先于 `BehaviorGraphAgent`（−50）将拍数据写入 Blackboard | **ADR-D2 锁定**签名，C4 实现 |
| **FE4 Boss** | 下游 | Boss 阶段拍号切换经 `OnMeterChanged` → C4 重算 `SlotCapacity`，以令牌密度承接阶段编排 | C4 拥有密度策略 |
| **F4 SO 数据层** | 上游（数据） | **[BC-3]** `EnemyInfoSO`(F4-S5) 提供 per-敌 `attackWindupBeats`(W)、`timeoutBeats`；全局导演参数（`capacityTable`/威胁权重 `dNear/dMid/wNear/wMid/wFar`/`starvationWeight`/`starvationCeiling`/`subdivisionMode`）由 `CombatDirectorConfigSO`(F4-S8) 承载，**非** `EnemyInfoSO` | F4 拥有 SO schema |

**provisional 标注**：FE1、FE4 GDD 尚未撰写，上述下游接口以 ADR-D2 契约为准，待 FE1 GDD 编写时双向对齐；`SlotCapacity` 与难度系统的耦合（是否叠加难度乘数）留待 Open Questions。

## Formulas

C4 的调度决策由公式链路构成：**F-1 决定每拍名额 → F-1b 按 BPM 归一化名额（[BD-2]）→ F-2/F-3 决定谁该获得名额（合成为 F-4）→ F-6 按名额和优先级选出获令者 → F-5 计算获令者的命中落拍**。以下按此依赖顺序给出规格。所有数值常量均为 Tuning Knob（建议默认值 + 安全范围），与「Tuning Knobs」章节共享。

### F-1　SlotCapacity(meter, beatIndex) — 拍槽容量表

```
SlotCapacity(meter, beatIndex) = CapacityTable[meter][beatIndex]
BeatsPerBar(meter) = length(CapacityTable[meter])          // 数据驱动，不单独配置
```

| 变量 | 类型 | 范围 | 说明 |
|---|---|---|---|
| meter | string (enum) | {"4/4", "3/4", "5/8"}（F4 合法拍号集：分子 ∈{3,4,5}、分母 ∈{4,8}，5 分子必配 8）随 `LevelConfigSO` 拍号集合扩展 | 当前拍号标识，来自 F2 `OnMeterChanged` |
| beatIndex | int | [0, BeatsPerBar(meter) − 1] | 拍在小节内的位置索引（0 = 强拍/downbeat），取自 F2 `BeatEventData` 拍内索引字段 |
| BeatsPerBar(meter) | int | 4/4→4，3/4→3，5/8→5 | 每小节拍位数，**等于** `CapacityTable[meter]` 数组长度，不重复配置 |
| CapacityTable[meter] | int[]（**Tuning Knob**） | 每元素 ∈ [0, 4]（建议安全范围） | 每拍位可授令名额配置表，per-拍号独立维护 |
| SlotCapacity | int（输出） | [0, 4] | 该拍位本次评估可授予的令牌名额数 |

示例默认表（Tuning Knob 建议值）：

| meter | beatIndex 0 | 1 | 2 | 3 | 4 |
|---|---|---|---|---|---|
| 4/4 | 2 | 1 | 1 | 1 | — |
| 3/4 | 2 | 1 | 1 | — | — |
| 5/8 | 2 | 0 | 1 | 1 | 0 |

**输出范围**：非负整数，逐拍位独立、无插值。0 表示该拍位刻意留白（弱拍静默，避免每拍齐射导致听觉噪音饱和）；建议安全范围 [0, 4]。

**示例**：`meter="4/4"`, `beatIndex=2` → `SlotCapacity = CapacityTable["4/4"][2] = 1`，本拍最多授予 1 个令牌。

### F-1b　EffectiveSlotCapacity — 每秒令牌速率归一化（**[BD-2]** BPM 尖峰缓解）

**问题（BD-2，与 C1/C2 联锁）**：`CapacityTable` 仅按拍号给出「每拍」名额，与 BPM 无关。BPM 上升时每秒拍数线性上升，导致真实攻击时间密度（tokens/sec）随 BPM 线性上升——Boss 拍号切换的单一 `OnMeterChanged` 同时收窄 C1 判定窗、缩短 C2 取消窗绝对毫秒、并抬高 C4 攻击密度，三系统同向变难，形成心流反模式的难度尖峰。

**归一化定义**：`SlotCapacity` 改按「每秒令牌速率」归一化——**目标 tokens/sec 恒定，实际每拍容量 = 目标速率 × 拍时长**。以参考 BPM（`bpmRef`，CapacityTable 的授权基准）锚定目标速率：

```
beatDuration(bpm)        = 60 / bpm                                   // 秒/拍
targetTokenRate[m][i]    = CapacityTable[m][i] / beatDuration(bpmRef) // tokens/sec，恒定
                         = CapacityTable[m][i] × bpmRef / 60

EffectiveSlotCapacity(m, i, bpm)
    = round( targetTokenRate[m][i] × beatDuration(bpm) )
    = round( CapacityTable[m][i] × bpmRef / bpm )
```

即 BPM 相对 `bpmRef` 上升 k 倍时，每拍名额约降为 1/k，使 tokens/sec 恒定、攻击时间密度不随 BPM 线性上升。F-6 授令一律消费 `EffectiveSlotCapacity` 而非原始 `CapacityTable` 值。

| 变量 | 类型 | 范围 | 说明 |
|---|---|---|---|
| bpm | float | 注册表合法 BPM 域 | 当前 BPM，来自 F2 拍事件（C4 纯消费，不做延迟补偿） |
| bpmRef | float（**Tuning Knob**） | 默认 120，安全范围 [60, 200] | CapacityTable 授权基准 BPM；`bpm==bpmRef` 时 `EffectiveSlotCapacity==CapacityTable` |
| EffectiveSlotCapacity | int（输出） | [0, 4] | 归一化后本拍实际可授名额 |

**取整与防饥饿保障**：`round` 四舍五入；为不重新引入密度尖峰，不对个别拍位设下限。BPM 极高致某些拍位归一化到 0 时，防饥饿仍由 F-3 `starvationCeiling` 强制提优 + E10 保证（每拍号 `CapacityTable` 至少一拍位 `≥1`，且 `bpmRef` 处该位 `EffectiveSlotCapacity≥1`）兜底——提优敌人在有界拍数内必获令，不产生真饥饿。

**互引 [BD-2]**：本归一化与 C1（判定窗适应窗口 `rhythm-core.md` Formulas）、C2（取消窗适应窗口 `combo-system.md` Formulas）三方共同承接 BPM 跃升——`OnMeterChanged` 生效时引入 2 拍适应窗口（Tuning Knob，默认 2、范围 [0,4]），窗口内 C1 判定窗 / C2 取消窗毫秒宽度取 `max(变更前, 变更后)`，C4 则由本 `EffectiveSlotCapacity` 保持 tokens/sec 恒定。三文档 Formulas 互相引用 BD-2，**已由 C1/C2/C4 三 owner 联合锁定适应窗口 = 2 拍**（2026-08-11，单一 owner=`RhythmConfigSO.AdaptationWindowBeats`，登记 registry `adaptation_window_beats`；详见 Edge Cases E15 与 AC-C4-30）。

### F-2　ThreatWeight(distance) — 威胁权重（分段阀值）

```
ThreatWeight(distance) =
    W_near   if 0 ≤ distance ≤ D_near
    W_mid    if D_near < distance ≤ D_mid
    W_far    if distance > D_mid
```

| 变量 | 类型 | 范围 | 说明 |
|---|---|---|---|
| distance | float | [0, +∞) 米 | 敌人到玩家的 XZ 平面欧氏距离，忽略高度差 |
| D_near | float（**Tuning Knob**） | 默认 3.0m，安全范围 [1.0, 6.0]m | 近/中分界距离 |
| D_mid | float（**Tuning Knob**） | 默认 8.0m，安全范围 [D_near+1.0, 15.0]m（须 > D_near） | 中/远分界距离 |
| W_near | float（**Tuning Knob**） | 默认 30.0，安全范围 [10, 100] | 近区权重常数 |
| W_mid | float（**Tuning Knob**） | 默认 15.0，安全范围 [5, W_near) | 中区权重常数 |
| W_far | float（**Tuning Knob**） | 默认 5.0，安全范围 [0, W_mid) | 远区权重常数 |
| ThreatWeight | float（输出） | 离散三值 {W_far, W_mid, W_near} | 分段阀值，**非连续**，区间内无插值 |

**约束**：须保证 `W_near > W_mid > W_far ≥ 0`，否则「近处敌人更易获令」的意图失效。

**示例**：`distance=4.5m`，`D_near=3.0`，`D_mid=8.0` → 落入中区 → `ThreatWeight = W_mid = 15.0`。

### F-3　StarvationBonus(waitBeats) — 饥饿补偿（线性 + 封顶哨兵）

```
StarvationBonus(waitBeats) =
    waitBeats × starvationWeight     if waitBeats < starvationCeiling
    BONUS_MAX                        if waitBeats ≥ starvationCeiling

BONUS_MAX = W_near + starvationCeiling × starvationWeight   // 派生哨兵值，非手动填写
```

| 变量 | 类型 | 范围 | 说明 |
|---|---|---|---|
| waitBeats | int | [0, +∞) 拍 | 该请求自 `t_req` 起已排队等待的拍数，每次评估未获令则 +1 |
| starvationWeight | float（**Tuning Knob**） | 默认 2.0（权重/拍），安全范围 [0.5, 10.0] | 每等待一拍的补偿增量 |
| starvationCeiling | int（**Tuning Knob**） | 默认 8 拍，安全范围 [4, 32]。**须显著小于该原型 `TimeoutBeats`**，否则令牌会在强制提优生效前就已超时撤销 | 触发强制提优的等待拍数阈值 |
| BONUS_MAX | float（派生，非 Knob） | = W_near + starvationCeiling × starvationWeight | 封顶哨兵值 |
| StarvationBonus | float（输出） | [0, BONUS_MAX] | 达 ceiling 后不再线性增长，防止数值无界 |

**示例**（默认 `weight=2.0`, `ceiling=8`, `W_near=30` → `BONUS_MAX=46.0`）：`waitBeats=5` → `10.0`；`waitBeats=9`（≥8）→ `46.0`（封顶）。

### F-4　Priority(enemy) — 综合优先级

```
Priority(enemy) = ThreatWeight(distance(enemy)) + StarvationBonus(waitBeats(enemy))
```

强制提优行为已内建于 F-3 的 `BONUS_MAX`，此处只需相加。

**正确性证明**（强制提优请求必胜于任何未提优请求，与距离无关）：
- 未提优请求 Priority 上界：`W_near + (starvationCeiling − 1) × starvationWeight`
- 提优请求 Priority 下界：`W_far + BONUS_MAX = W_far + W_near + starvationCeiling × starvationWeight`
- 差 = `W_far + starvationWeight > 0`（恒成立）→ 提优请求 Priority 恒大于未提优请求，无论距离。

| 变量 | 类型 | 范围 | 说明 |
|---|---|---|---|
| Priority | float（输出） | [W_far, W_near + BONUS_MAX]，默认 [5.0, 106.0] | 用于 F-6 降序排序的标量 |

**示例**（默认值）：A `distance=12m`(far,5.0) + `waitBeats=9`(提优,46.0) = **51.0**；B `distance=2m`(near,30.0) + `waitBeats=7`(14.0) = **44.0**。`51>44`：B 虽更近，A 因等待封顶被强制提优，A 本拍优先。

### F-5　targetLandingBeat = SnapToEligible(grantBeat + W, subdivisionMode)

```
rawTargetBeat = grantBeat + W

EligibleSet(subdivisionMode, meter) =
    FullBeat      → { n           | n ∈ ℤ }
    HalfBeat      → { n × 0.5     | n ∈ ℤ }
    DownbeatOnly  → { n × BeatsPerBar(meter) | n ∈ ℤ }

targetLandingBeat = min{ e ∈ EligibleSet(subdivisionMode, meter) | e ≥ rawTargetBeat }
```

**吸附方向规则（只向上取整 / ceiling，禁止向下）**：向下吸附会让命中判定帧落在前摇动画尚未播完之时，破坏因果（招式未出完就命中）。向上吸附产生的多余拍数 `Δ = targetLandingBeat − rawTargetBeat` 由前摇动画末尾的姿态保持（telegraph hold）填补——动画层适配属 F6/C2 依赖，本 GDD 只锁定数学落点。

| 变量 | 类型 | 范围 | 说明 |
|---|---|---|---|
| grantBeat | int | [0, +∞) 拍 | 令牌授予时的全局绝对拍计数（整数拍边界，来自 F2） |
| W | float | [0.5, 8.0] 拍（`enemy_attack_windup_beats`，registry-locked；MVP 常见 [1.0, 4.0]） | 敌人前摇拍数，来自 `EnemyInfoSO.attackWindupBeats` |
| subdivisionMode | enum {FullBeat, HalfBeat, DownbeatOnly}（**Tuning Knob**，可 per-关卡/原型） | 默认 FullBeat | 目标落地拍细分粒度 |
| targetLandingBeat | float（输出） | ≥ rawTargetBeat；Δ 上界：FullBeat<1.0、HalfBeat<0.5、DownbeatOnly<BeatsPerBar | 吸附后命中拍 |

**示例**：`grantBeat=20`，`W=1.5`，`meter="4/4"` → `rawTargetBeat=21.5`：FullBeat→22.0(Δ+0.5)；HalfBeat→21.5(Δ0)；DownbeatOnly→24.0(Δ+2.5)。

### F-6　GrantSelection — 授令选择算法

```
function GrantSelection(pendingQueue, meter, beatIndex, currentEvalBeat, subdivisionMode, bpm):
    capacity = EffectiveSlotCapacity(meter, beatIndex, bpm)        // F-1b [BD-2]（归一化后名额，替代裸 F-1 CapacityTable）
    if capacity == 0 or pendingQueue.isEmpty():
        return []

    for request in pendingQueue:
        request.priority = Priority(request)                       // F-4

    sortedQueue = SORT(pendingQueue, by: (
        1. priority        DESCENDING,   // 主键
        2. waitBeats        DESCENDING,   // 并列时等待更久者优先
        3. tReq              ASCENDING,   // 仍并列则先入队者优先（FIFO 兜底）
        4. enemyInstanceId  ASCENDING     // 稳定排序哨兵，保证可复现测试
    ))

    granted = []
    grantedCount = 0
    for request in sortedQueue:
        if grantedCount >= capacity: break
        targetLandingBeat = SnapToEligible(currentEvalBeat + request.enemy.W, subdivisionMode, meter)  // F-5
        GrantToken(request.enemy, targetLandingBeat)                // 状态机 Pending → Granted
        granted.append(request); grantedCount += 1

    for request in pendingQueue where request not in granted:
        request.waitBeats += 1                                     // 未获令请求进入下次评估

    return granted
```

| 变量 | 类型 | 范围 | 说明 |
|---|---|---|---|
| pendingQueue | list\<PendingRequest\> | 0..N | 待评估的令牌请求集合 |
| capacity | int | [0, 4]（F-1b [BD-2] 归一化输出） | 本拍位可授名额（`EffectiveSlotCapacity`，已按 BPM 归一化） |
| granted | list\<PendingRequest\>（输出） | [0, min(capacity, \|pendingQueue\|)] | 本拍实际获令请求，其余保留 `Pending` 进入下一拍 |

**示例**（`beatIndex=2`, `meter="4/4"` → `capacity=1`）：E1 near+wait7=44.0、E2 far+wait9(提优)=51.0、E3 mid+wait2=19.0 → 排序 E2>E1>E3 → 仅授 **E2**；E1/E3 保留 Pending，waitBeats 递增为 8、3。

### 公式层依赖说明

1. F-5 的「Δ 由前摇动画末尾姿态保持填补」尚未在 F6/C2 锁定实现方式，见 Edge Cases / Open Questions 标记的待 C2/F6 对齐项。
2. `BONUS_MAX` 是派生值：策划调整 `W_near`/`starvationCeiling`/`starvationWeight` 任一项时，须在 Odin Validator 中自动重算，禁止硬编码缓存旧值。
3. 上述公式暂仅供 C4 内部消费，暂不登记 `entities.yaml` 的 formulas 区块；待 FE1/FE4 GDD 引用（如 FE4 拍号切换读取 `SlotCapacity`）时复核登记。

## Edge Cases

| # | 边界情况 | 明确行为 |
|---|---|---|
| E1 | 敌人在 `Pending`（排队中）死亡或 BT 被中断 | C4 将其请求移出 pending queue，令牌 `Pending → Cancelled → None`，不占用任何拍槽名额。 |
| E2 | 敌人在 `Granted`（前摇中）被击退/硬直打断/死亡 | 令牌 `Granted → Cancelled`，C4 立即释放该拍槽占用记录；已预约的 `targetLandingBeat` 名额归还，可被同拍其他 pending 请求补位。 |
| E3 | 授令后 `TimeoutBeats` 拍内未激活 hitbox | 令牌 `Granted → Expired`，`CombatDirectorTokenAction` 返回 `Status.Failure`，敌人 BT 走 fallback 分支（重新索敌/位移），随后可重新请求。 |
| E4 | 前摇中途拍号切换（`OnMeterChanged`） | 已授令牌**保留原 `targetLandingBeat`**（绝对拍计数不受拍号变化影响），继续按原拍命中，不重算。仅**未来拍**的 `SlotCapacity` 按新拍号容量表评估。 |
| E5 | 前摇中途 BPM 变化（tempo ramp） | C4 不作任何处理——`beat_dsp_time` 由 F2 重算，C4 作为纯消费者按新时间戳触发；绝对拍序号不变，命中仍落在 `targetLandingBeat`（F2 Core Rule 7）。 |
| E6 | BeatClock 被暂停/停止（TDE 生命周期，`TDELifecycleAdapter` 所有） | F2 停止派发拍点事件 → C4 停止拍槽评估；`waitBeats` 与 `TimeoutBeats` 均以拍计数，无拍事件即冻结，所有 `Pending`/`Granted` 令牌原样保留。恢复后从下一拍继续，无令牌丢失。C4 **不**调用任何生命周期方法（`beatclock_lifecycle_bypass`）。 |
| E7 | 同一帧多个敌人同时请求令牌 | 全部入队，`t_req` 相同；F-6 tie-break 依次按 `waitBeats` → `t_req` → `enemyInstanceId` 裁决，排序稳定可复现。 |
| E8 | 请求在该目标拍的 lookahead 已处理后才到达 | 该请求不参与当前拍评估，`waitBeats=0` 保留至下一拍评估。不回溯已结算的拍。 |
| E9 | 敌人已持有 `Granted` 令牌时再次调用 `RequestAttackToken` | **拒绝**重复请求：一个敌人同一时刻至多持有一个令牌。Editor 下 `Debug.Assert` 拦截；Runtime 直接忽略重复请求，返回既有令牌句柄。 |
| E10 | 强制提优敌人遇所有 eligible 拍名额持续占满 | 保持最高优先级直到出现空名额即授令。Odin Validator 强制约束：每种拍号的 `CapacityTable` 至少一个拍位 `capacity ≥ 1`，保证提优敌人在有界拍数内必获令，杜绝真饥饿。 |
| E11 | `TimeoutBeats < ceil(W) + maxSnapΔ`（配置错误：令牌必在命中前超时） | 属数据非法：Odin Validator 校验规则 `TimeoutBeats ≥ ceil(W) + maxSnapΔ(subdivisionMode, meter)`，不满足则该 `EnemyInfoSO` 校验失败，禁止进包。Runtime 不做补偿。 |
| E12 | `CapacityTable` 全零（拍号无任何可授拍位） | 属数据非法：Odin Validator 拒绝（见 E10 约束），阻止「敌人永远无法出手」的死配置。 |
| E13 | 场上零敌人 / pending queue 为空 | F-6 直接返回空集，C4 空转，无副作用。 |
| E14 | 极端配置：`W=8.0` 拍 @ `BPM=80` → 前摇约 6 秒 | 合法（W、BPM 均在注册表范围内），非错误。超长前摇的 telegraph 表现由 F6/C2 动画层负责，C4 只保证落拍正确。 |
| E15 **[BD-2]** | Boss 拍号切换致 BPM 大幅跃升（如 `OnMeterChanged` 伴随 `bpm` 由 `bpmRef` 升至 ~2×） | C4 用 `EffectiveSlotCapacity`（F-1b）按 `bpmRef/bpm` 归一化每拍名额，tokens/sec 保持恒定，攻击时间密度**不**随 BPM 线性上升；避免与 C1 判定窗收窄、C2 取消窗毫秒缩短叠加成难度尖峰。归一化于 `OnMeterChanged` 生效即刻应用于未来拍（已授令牌不受影响，见 E4）。C1/C2 侧另有 2 拍适应窗口（`max` 宽度规则）承接，三系统 BD-2 联锁。 |
| E16 **[SC-3]** | run-end（F6 桥接域事件 `OnRunEnded(outcome)`，Death 与 Victory 均触发，ADR-D13，取代原 `OnPlayerDeath`；MVP 均 = 换新局，ADR-D12/ADR-D13） | C4 取消**全部** `Pending`+`Granted` 令牌（`→ Cancelled → None`），清空 pending queue，释放所有拍槽占用。**区别于 E6**：E6 是 Pause 冻结续接（`BeatClock` 连续，`global_beat_index` 不变）；E16 是 run-end（Death 或 Victory）= `BeatClock.Stop()`、`global_beat_index` 归零（F2 Rule 10）、绝对拍令牌 `targetLandingBeat` 失锚，必须**全清**而非冻结（否则换新局后残留令牌落拍错乱 → 幽灵攻击）——胜利分支与死亡分支同源失锚。执行于 F6 广播 `OnRunEnded(outcome)` 的同帧（F6 同帧 `Stop()` + `DisableAllInput()`）。C4 不调用任何 BeatClock 生命周期方法。见 Core Rule 12 / AC-C4-31。 |

## Dependencies

### 上游（C4 依赖）

| 系统 | 依赖内容 | 双向引用状态 |
|---|---|---|
| **F2 音频/拍点时钟** | 订阅 `OnBeatLookahead`（timestamp + 小节号 + 拍内索引）、`OnMeterChanged`；只读 `BeatEventData.beat_dsp_time`。C4 的全部调度时机源于 F2 拍事件。 | ✅ F2 已反向引用 C4（`audio-beat-clock.md:110` F2→C4 lookahead、`:112` F2→C1/C4 `OnMeterChanged`、`:489` 硬依赖表） |
| **F4 SO 数据层** | **[BC-3]** per-敌数据由 `EnemyInfoSO`(F4-S5) 承载：`attackWindupBeats`(W)、`timeoutBeats`。全局导演参数由 **`CombatDirectorConfigSO`(F4-S8)** 承载：`capacityTable`、威胁权重（`dNear/dMid/wNear/wMid/wFar`）、`starvationWeight`、`starvationCeiling`、`subdivisionMode`。`CapacityTable` 属 per-拍号全局数据，语义不归 `EnemyInfoSO`。 | F4 拥有 SO schema（F4-S5 `EnemyInfoSO` + F4-S8 `CombatDirectorConfigSO`）；C4 为消费方 |
| **ADR-D2 敌人 AI 框架** | `CombatDirectorTokenAction` BT Action 节点契约（`Status.Running/Success/Failure`、`TimeoutBeats`）；`BeatClockBridge`（ExecOrder −100）先于 `BehaviorGraphAgent`（−50）写 Blackboard。**接口签名由 ADR-D2 锁定**。 | ✅ ADR-D2 已点名 C4（须与 FE1 并行撰写） |
| **ADR-D6 F2 派发模型** | F2 拍事件为主线程 Update 轮询派发；C4 令牌逻辑运行同线程，无跨线程队列/同步原语。 | ✅ 架构约束，已在 Core Rule 11 落地 |
| **ADR-D10 引导时序** | C4 须在 BeatClock ready 之后订阅拍事件；bootstrap 顺序保证不读取未初始化的拍状态。 | ✅ 架构约束 |
| **F6 TDE 集成层** | 订阅 F6 桥接的域事件 **`OnRunEnded(RunOutcome outcome)`**（CombatCore 可见纯 C# `event`，负载为纯 C# `enum` `RunOutcome{Death,Victory}`，不含 TDE 类型；取代原 `OnPlayerDeath`，ADR-D13）；据此同帧取消全部令牌（SC-3 run-end 回收，Death + Victory 均触发，见 Core Rule 12 / E16）。C4 禁止 `using` TopDownEngine，只订阅纯 C# 桥接事件，不调用任何 BeatClock 生命周期方法。 | ✅ F6 Rule 15 反向登记 C4 为 `OnRunEnded` 订阅方（对称 C1 SC-2）；见 `tde-integration.md` 交互表 F6→C4 行 |

### 下游（依赖 C4）

| 系统 | 依赖内容 | 双向引用状态 |
|---|---|---|
| **FE1 敌人 AI** | 经 `CombatDirector.RequestAttackToken(enemy)` / `enemy.OnTokenGranted(targetLandingBeat)` 获取攻击时机；无令牌不得攻击（规则四）。 | ⚠️ FE1 GDD 尚未撰写——须与本 GDD 并行对齐接口（ADR-D2 要求） |
| **FE4 Boss** | Boss 阶段拍号切换经 `OnMeterChanged` 驱动 C4 重算 `SlotCapacity`，以令牌密度承接阶段编排。 | ⚠️ FE4 GDD 尚未撰写——provisional |

### 同级/关联（软依赖）

| 系统 | 关系 |
|---|---|
| **C2 / F6（动画层，Animancer + TDE 集成）** | F-5 的吸附延迟 `Δ` 由前摇动画末尾的 telegraph hold 填补；attack 动画时长须随 BPM 经 Animancer `Speed` 缩放。实现方式待 C2/F6 对齐（见 Open Questions）。 |
| **game-concept 规则四（敌人强制落拍）** | C4 是规则四的架构强制点；本 GDD 的存在即为满足该顶层设计约束。 |
| **F5 存档/持久化** | 无直接依赖——C4 为纯运行时调度器，不持久化任何状态（令牌生命周期不跨存档、不跨局）。死亡换局的 merit 结算由 FE5 承接（订阅局终信号 → 折算落盘，ADR-D12），非 C4 职责。 |

> **上游变更登记（SC-3，2026-08-11；ADR-D13 迁移，2026-08-12）**：C4 现有上游含 **F6 run-end 域事件 `OnRunEnded(RunOutcome outcome)`**（Death 与 Victory 均触发，ADR-D13 取代原 `OnPlayerDeath`；见「上游」表 F6 行）。SG-3 复审所指「C4 上游仅 F2/ADR-D2、不订阅死亡事件」的旧表述已被本轮补丁消解——C4 现有对称 C1 SC-2 的 run-end 令牌回收合约。

**双向引用待办**：（1）✅ 已完成（2026-08-12，I-2 清账）——F2 `audio-beat-clock.md` 已反向引用 C4 为 `OnMeterChanged`/lookahead 消费者（:110/:112/:489）；（2）FE1、FE4 GDD 编写时以 ADR-D2 契约与本节接口双向锁定（待 FE1/FE4 开工）。

## Tuning Knobs

| Knob | 默认值 | 安全范围 | 影响的玩法维度 | 数据归属 |
|---|---|---|---|---|
| `CapacityTable[meter]`（每拍位数组） | 4/4:[2,1,1,1]；3/4:[2,1,1]；5/8:[2,0,1,1,0]（3+2 分组，强拍在 0/3，待 playtest 校准） | 每元素 ∈ [0,4]；每拍号至少一位 ≥1 | **攻击密度与节奏留白**——决定每拍最多几个攻击落地，强拍高/弱拍低塑造攻击潮汐；调高→压力增、可预读性降 | `CombatDirectorConfigSO`（F4-S8）**[BC-3]** |
| `D_near` | 3.0m | [1.0, 6.0]m | **威胁近区边界**——多近算「贴脸高威胁」；调小→仅极近敌抢优先级 | `CombatDirectorConfigSO`（F4-S8）**[BC-3]** |
| `D_mid` | 8.0m | [D_near+1.0, 15.0]m（须 > D_near） | **威胁中/远区边界**——远敌何时降为最低威胁 | `CombatDirectorConfigSO`（F4-S8）**[BC-3]** |
| `W_near` | 30.0 | [10, 100] | **近区威胁权重**——贴脸敌抢令强度；同时影响 `BONUS_MAX` 派生值 | `CombatDirectorConfigSO`（F4-S8）**[BC-3]** |
| `W_mid` | 15.0 | [5, W_near) | **中区威胁权重** | `CombatDirectorConfigSO`（F4-S8）**[BC-3]** |
| `W_far` | 5.0 | [0, W_mid) | **远区威胁权重**——远敌基础出手意愿 | `CombatDirectorConfigSO`（F4-S8）**[BC-3]** |
| `starvationWeight` | 2.0 /拍 | [0.5, 10.0] | **饥饿补偿斜率**——远处敌人多快「熬」出出手机会；调高→战场更平均、无被冷落敌 | `CombatDirectorConfigSO`（F4-S8）**[BC-3]** |
| `starvationCeiling` | 8 拍 | [4, 32]；**须 < 该原型 `TimeoutBeats`** | **强制提优阈值**——等待多少拍后无条件抢到最高优先级；防永久饥饿 | `CombatDirectorConfigSO`（F4-S8）**[BC-3]** |
| `subdivisionMode` | FullBeat | {FullBeat, HalfBeat, DownbeatOnly} | **命中落拍细分粒度**——攻击可落在整拍/半拍/仅强拍；越细→节奏越密、预读越难 | `CombatDirectorConfigSO`（F4-S8）**[BC-3]**（可 per-关卡覆写） |
| `bpmRef`（**[BD-2]**） | 120 | [60, 200] | **归一化基准 BPM**——`CapacityTable` 授权基准；F-1b 按 `bpmRef/bpm` 归一化每拍名额，令 tokens/sec 恒定，消除 BPM 上升的攻击密度尖峰 | `CombatDirectorConfigSO`（F4-S8）**[BC-3]** |
| `TimeoutBeats` | 每原型配置（如轻型 2、重型 4） | ≥ `ceil(W) + maxSnapΔ`（见 E11） | **令牌耐心**——授令后多少拍未兑现则撤销；调小→敌人更「守时」，被打断即放弃 | `EnemyInfoSO`（F4-S5）**[BC-3]** |
| `W`（`attackWindupBeats`，只读引用） | 每原型配置 | [0.5, 8.0] 拍（MVP 常见 [1.0, 4.0]） | **前摇拍数**——命中距授令多少拍，决定 telegraph 时长与可读窗口 | `EnemyInfoSO`（F4-S5）**[BC-3]**（注册表 locked，C4 不改） |

**派生值提醒**：`BONUS_MAX = W_near + starvationCeiling × starvationWeight` 非独立 Knob，随上述三项自动重算（Odin Validator 强制，禁止硬编码缓存）。

**校验约束（Odin Validator）**：(1) `W_near > W_mid > W_far ≥ 0`；(2) `D_mid > D_near`；(3) 每拍号 `CapacityTable` 至少一位 `≥1`；(4) `starvationCeiling < TimeoutBeats`；(5) `TimeoutBeats ≥ ceil(W) + maxSnapΔ`。

## Visual/Audio Requirements

C4 本身不产出任何直接的视觉或音频。C4 是纯调度基础设施，唯一输出是令牌授予事件（`enemy.OnTokenGranted(targetLandingBeat)`）。所有玩家可感知的表现——敌人前摇动画、预警音效、落拍闪光——归 F6（TDE 反馈 / MMFeedbacks）与 C2 拥有，由它们订阅敌人状态而非订阅 C4。

- **无直接资产需求**：C4 不需要任何 SFX、VFX、动画资产。
- **落拍表现的时序权威**：C4 授予的 `targetLandingBeat` 是 F6/C2 播放预警表现的权威时间锚点——表现层据此对齐落拍闪光与预警动画，但表现资产本身不由 C4 定义。表现层通过订阅敌人状态（Granted → 前摇开始）触发，`targetLandingBeat` 对应的 `beat_dsp_time` 由 F2 提供。
- **可选调试可视化**（非发行需求，Editor-only）：调试叠层可绘制当前拍槽占用、pending 队列优先级、令牌状态，仅供调优。详见 UI Requirements。

## UI Requirements

C4 无任何玩家可见 UI。它是敌人 AI 与 F2 拍钟之间的后台调度器，玩家永不直接交互。

- **无玩家 UI**：无 HUD 元素、无菜单、无提示、无 d-pad/触摸交互面。
- **调试叠层（Editor-only，可选，ADVISORY）**：为调优提供一个 `#if UNITY_EDITOR` 调试面板，显示：
  1. 每拍槽当前容量 `SlotCapacity(meter, beatIndex)` 与实时占用；
  2. pending 队列（enemy、`Priority`、`waitBeats`、`tReq`、排序后位次）；
  3. 已授令牌及其 `targetLandingBeat` 与剩余 `TimeoutBeats`。
- **约束**：禁止在 Runtime 代码路径引用 Odin `Sirenix.*` 命名空间——调试面板须置于 `Editor/` 程序集或 `#if UNITY_EDITOR` 块内，不打包进构建。调试叠层的读取不得修改 C4 内部状态（只读快照）。

## Acceptance Criteria

> 证据类型/门槛遵循项目 Story Type 表：Logic=自动化单元测试(BLOCKING, `tests/unit/combat-director/`)；Integration=集成测试或有据可查的 playtest(BLOCKING, `tests/integration/combat-director/`)；Config/Data=smoke check(ADVISORY)。

### 落拍正确性（F-5 SnapToEligible）

- **AC-C4-01**（Logic/BLOCKING）：`SnapToEligible` 只向上吸附，从不向下。
  测试：`rawTargetBeat=21.3`，`FullBeat` → 断言 `targetLandingBeat=22.0`，永不返回 `21.0`。
- **AC-C4-02**（Logic/BLOCKING）：三种 `subdivisionMode` 的 `EligibleSet` 吸附结果正确。
  测试：复用 GDD 示例 `grantBeat=20, W=1.5, meter="4/4"` → `rawTargetBeat=21.5`：`FullBeat→22.0`、`HalfBeat→21.5`、`DownbeatOnly→24.0`，三分支各自断言。
- **AC-C4-03**（Logic/BLOCKING）：`rawTargetBeat` 恰好落在 eligible 集合上时 Δ=0，不额外推迟。
  测试：`FullBeat`，`rawTargetBeat=22.0`（整数）→ 断言 `targetLandingBeat=22.0`。
- **AC-C4-04**（Integration/BLOCKING）：端到端落拍数学必然性——命中时刻与 F2 广播的 `targetLandingBeat` 对应 `beat_dsp_time` 严格一致，跨 BPM 无漂移。
  测试：注入模拟拍点事件流（参照 F2 `IDSPClock`/`MockDSPClock` 确定性范式）驱动 500 拍，在多组 BPM 下授令，断言 `enemy.OnTokenGranted` 收到的 `targetLandingBeat` 与后续对应 `OnBeatLookahead`/`OnBeatExact` 的 `beat_dsp_time` 严格对应，误差 < 0.1ms（沿用 F2 精度承诺）。

### 拍槽容量约束（F-1 SlotCapacity）

- **AC-C4-05**（Logic/BLOCKING）：单拍授令数不超过 `SlotCapacity(meter, beatIndex)`。
  测试：`meter="4/4", beatIndex=2, capacity=1`，pending 队列 3 个请求 → 断言 `GrantSelection` 返回长度恰为 1。
- **AC-C4-06**（Logic/BLOCKING）：`capacity=0` 的拍位不授令（弱拍静默）。
  测试：`meter="5/8", beatIndex=1`（`CapacityTable=0`）→ 断言无论 pending 队列非空与否，`granted` 恒为空集。
- **AC-C4-07**（Logic/BLOCKING）：`OnMeterChanged` 后，后续评估使用新拍号的 `CapacityTable`；已授令牌不受影响（与 AC-C4-25 联动）。
  测试：授予 1 个 `4/4` 令牌后触发 `OnMeterChanged("5/8")` → 断言下一次评估用 `CapacityTable["5/8"]` 计算 capacity，已授令牌 `targetLandingBeat` 保持不变。
- **AC-C4-30**（Logic/BLOCKING，**[BD-2]**）：`EffectiveSlotCapacity`（F-1b）按每秒令牌速率归一化，攻击时间密度不随 BPM 线性上升。
  测试：(a) `bpm == bpmRef`（=120）时断言 `EffectiveSlotCapacity(meter,i,bpm) == CapacityTable[meter][i]`（归一化在基准处恒等）；(b) `bpm == 2×bpmRef`（=240）时断言 `EffectiveSlotCapacity == round(CapacityTable[meter][i] × bpmRef/bpm)`（如 table=2 → 1），且实测 tokens/sec = `EffectiveSlotCapacity / (60/bpm)` 相对 `bpmRef` 处保持恒定（误差仅来自 `round` 取整，≤ 0.5 token/beat）；(c) 断言即便某拍位归一化到 0，防饥饿仍由 F-3 `starvationCeiling` 强制提优兜底（提优敌人在有界拍数内获令）。

### 优先级与防饥饿（F-2/F-3/F-4）

- **AC-C4-08**（Logic/BLOCKING）：`ThreatWeight` 分段阀值边界精确归属。
  测试：`distance ∈ {2.999, 3.0, 3.001, 7.999, 8.0, 8.001}`（`D_near=3.0, D_mid=8.0`）→ 断言分别返回 `{W_near, W_near, W_mid, W_mid, W_mid, W_far}`（边界点归入更近区间，即 `≤D_near`/`≤D_mid` 为闭区间）。
- **AC-C4-09**（Logic/BLOCKING）：`StarvationBonus` 线性区间取值正确。
  测试：`waitBeats=5, weight=2.0` → 断言 `StarvationBonus=10.0`。
- **AC-C4-10**（Logic/BLOCKING）：`StarvationBonus` 封顶边界精确点与 `BONUS_MAX` 派生重算联动。
  测试：`waitBeats=7`（< ceiling=8）→ 未封顶值；`waitBeats=8`（==ceiling）→ 断言恰好等于 `BONUS_MAX`；修改 `W_near`/`starvationCeiling`/`starvationWeight` 后断言 `BONUS_MAX` 自动重算，不得读到缓存旧值。
- **AC-C4-11**（Logic/BLOCKING）：`Priority = ThreatWeight + StarvationBonus` 求和正确。
  测试：`distance` 落中区(15.0) + `waitBeats=5`(10.0) → 断言 `Priority=25.0`。
- **AC-C4-12**（Logic/BLOCKING）：强制提优请求必胜证明的具体数值复现（与距离无关）。
  测试：复用 GDD 示例 A(`distance=12m, waitBeats=9`)=51.0，B(`distance=2m, waitBeats=7`)=44.0 → 断言 `Priority(A) > Priority(B)` 且 `GrantSelection` 在同拍容量=1 时选中 A。

### 授令选择与 tie-break 可复现性（F-6 GrantSelection）

- **AC-C4-13**（Logic/BLOCKING）：四级排序键依序生效（`priority↓ → waitBeats↓ → tReq↑ → enemyInstanceId↑`）。
  测试：构造 `priority` 相同的两请求，用 `waitBeats` 打破平局；再构造 `priority`+`waitBeats` 都相同的两请求，用 `tReq` 打破；再构造前三键全同的两请求，用 `enemyInstanceId` 打破——四组独立用例逐一断言胜出者。
- **AC-C4-14**（Logic/BLOCKING）：相同输入多次运行 `GrantSelection` 结果确定可复现（无隐藏随机性/迭代顺序依赖）。
  测试：固定 pending 队列（含并列 `priority`）运行 100 次，断言 100 次 `granted` 列表逐元素相同。
- **AC-C4-15**（Logic/BLOCKING）：capacity 满后未获令请求保持 `Pending`，`waitBeats+1`。
  测试：复用 GDD 示例（`capacity=1`，E1/E2/E3 排序 E2>E1>E3）→ 断言 E1/E3 仍为 `Pending` 且 `waitBeats` 分别从 7→8、2→3。
- **AC-C4-16**（Logic/BLOCKING）：E13 空队列 / 零敌人时 `GrantSelection` 返回空集，无副作用。
  测试：`pendingQueue=[]` → 断言返回 `[]`，且不抛异常、不修改任何外部状态。

### 令牌状态机（合法/非法转移）

- **AC-C4-17**（Logic/BLOCKING）：6 条合法转移逐条覆盖（`None→Pending→{Granted,Cancelled}`、`Granted→{Consumed,Expired,Cancelled}`、终态`→None`）。
  测试：为每条转移编写独立用例，驱动触发条件后断言状态字段更新为目标态。
- **AC-C4-18**（Logic/BLOCKING）：非法转移（如 `Pending→Consumed` 跳过授令）在 Editor 下被 `Debug.Assert` 拦截。
  测试：EditMode 测试直接调用跳过授令的内部转移路径，用 `LogAssert.Expect(LogType.Assert, ...)` 断言触发 assert。
- **AC-C4-19**（Logic/BLOCKING）：`Consumed`/`Expired`/`Cancelled` → `None` 释放后敌人可重新请求并成功进入 `Pending`。
  测试：令牌经 `Consumed`/`Expired`/`Cancelled` 任一路径回到 `None` 后，调用 `RequestAttackToken` → 断言成功转入 `Pending`（非重复请求拒绝路径）。

### 超时与打断边界

- **AC-C4-20**（Logic/BLOCKING，E3）：`TimeoutBeats` 内未激活 hitbox → `Granted→Expired`，`CombatDirectorTokenAction` 返回 `Status.Failure`。
  测试：授令后推进 `TimeoutBeats` 拍且不触发兑现 → 断言状态为 `Expired`，节点返回 `Status.Failure`，名额已归还（下一评估 capacity 恢复）。
- **AC-C4-21**（Integration/BLOCKING，E1）：`Pending` 中死亡/BT 中断 → `Cancelled`，不占用任何拍槽名额。
  测试：请求处于 `Pending` 时触发死亡事件 → 断言状态为 `Cancelled`，且该拍评估 capacity 计数不含此请求。
- **AC-C4-22**（Integration/BLOCKING，E2）：`Granted` 中被击退/硬直打断/死亡 → `Cancelled`，立即释放拍槽占用，同拍可被其他 pending 请求补位。
  测试：授令后（前摇中）触发击退 → 断言状态为 `Cancelled` 且该拍槽名额立即可被同拍另一 pending 请求获取（同拍二次评估验证补位成功）。
- **AC-C4-23**（Logic/BLOCKING，E9）：敌人已持有 `Granted` 令牌时再次调用 `RequestAttackToken` 被拒绝，返回既有令牌句柄，不产生第二个 `Pending` 记录。
  测试：先授令一次，再对同一 `enemy` 调用 `RequestAttackToken` → 断言 pending queue 长度不变，返回值等于既有令牌句柄；EditMode 下额外断言触发 `Debug.Assert`。
- **AC-C4-24**（Integration/BLOCKING，E6）：BeatClock 暂停 → C4 冻结（`waitBeats`/`TimeoutBeats` 计数不前进），所有 `Pending`/`Granted` 原样保留；恢复后从下一拍继续，无令牌丢失，且此期间 C4 不调用任何 BeatClock 生命周期方法。
  测试：授令若干 `Pending`/`Granted` 令牌后模拟 F2 暂停（停止派发拍事件）→ 推进模拟时间 → 断言所有令牌状态与 `waitBeats` 不变；恢复派发后断言从暂停点后的下一拍继续评估，无令牌被静默丢弃。
- **AC-C4-25**（Logic/BLOCKING，E4）：前摇中途 `OnMeterChanged` 触发时，已授令牌保留原 `targetLandingBeat`（绝对拍计数不受影响），不重算；仅未来拍的 `SlotCapacity` 用新拍号表。
  测试：授令 `targetLandingBeat=22.0` 后触发 `OnMeterChanged("5/8")` → 断言该令牌 `targetLandingBeat` 仍为 `22.0`（与 AC-C4-07 为同一场景的两个断言面，可合并测试方法）。

### F2 消费边界（架构约束，静态扫描）

- **AC-C4-26**（Logic/BLOCKING）：C4 源码禁止读取 `AudioSettings.dspTime`，只读 `BeatEventData.beat_dsp_time`。
  测试：CI 静态代码扫描（registry forbidden pattern，命名建议 `c4_reads_audiosettings_dsptime`，与 F2 GDD 已有的 `f2_self_load_latency_from_f5` 命名惯例一致）扫描 `Assets/Scripts/Gameplay/CombatDirector/` 下全部 `.cs` 文件，命中 `AudioSettings.dspTime` 即判定失败。
- **AC-C4-27**（Logic/BLOCKING）：C4 源码禁止调用 BeatClock 生命周期方法（`Play()`/`Stop()`/`Pause()` 等，`beatclock_lifecycle_bypass`）。
  测试：同上静态扫描机制，命中 BeatClock 生命周期方法调用即判定失败。

### 数据校验（Config/Data，ADVISORY）

- **AC-C4-28**（Config-Data/ADVISORY）：Odin Validator 5 条约束均生效——(1) `W_near > W_mid > W_far ≥ 0`；(2) `D_mid > D_near`；(3) 每拍号 `CapacityTable` 至少一位 `≥1`（对应 E10/E12）；(4) `starvationCeiling < TimeoutBeats`；(5) `TimeoutBeats ≥ ceil(W) + maxSnapΔ(subdivisionMode, meter)`（对应 E11）。
  测试：smoke check——分别构造 5 组违反单条约束的 `EnemyInfoSO`/配置表，断言 Odin Validator 各自报错拦截；1 组全合规配置断言通过。

### 反支柱回归（game-concept 规则四强制点）

- **AC-C4-29**（Integration/BLOCKING）：无令牌不得攻击——任何敌人在未持有 `Granted` 状态令牌时，其攻击 hitbox 不得激活。
  测试：集成测试直接尝试绕过 `CombatDirectorTokenAction` 触发敌人攻击能力（模拟 BT 误配置或直接调用底层攻击方法）→ 断言 hitbox 未激活 / 攻击方法在无 `Granted` 令牌时提前返回失败。此为回归测试，须纳入 CI 常驻套件，任何未来改动导致此测试失败即视为破坏支柱一的架构强制点，S1 级阻塞。

### run-end 令牌回收（SC-3，对称 C1 SC-2；Death + Victory，ADR-D13）

- **AC-C4-31**（Logic/BLOCKING，E16/SC-3/Core Rule 12）：发布 F6 `OnRunEnded(outcome)` 域事件后（任一 `RunOutcome`——`Death` 或 `Victory`），C4 取消全部令牌且不触碰时钟生命周期。
  测试：注入 mock `OnRunEnded(RunOutcome outcome)` 域事件源 + Spy 令牌状态机；预置若干 `Pending` 与 `Granted` 令牌（含 `targetLandingBeat` 绝对拍值），分别发布 `OnRunEnded(RunOutcome.Death)` 与 `OnRunEnded(RunOutcome.Victory)` 两组用例，各自断言：(a) 全部 `Pending`/`Granted` 令牌转 `Cancelled`/`None`；(b) pending queue 为空；(c) 所有拍槽占用清零；(d) 此期间 C4 未调用任何 BeatClock 生命周期方法（`beatclock_lifecycle_bypass` 静态扫描保持通过，AC-C4-27 联动）；(e) 域事件订阅编译期断言——事件签名仅引用 CombatCore 可见类型（**不含 TopDownEngine 命名空间**，与 AC-C4-26/27 边界约束同源）。与 C1 `AC-C1-22e`（复活/死亡清空 pending）、F6 `AC-F6-25`（run-end 桥接广播，C4 为订阅方之一）配对，形成 SC-2/SC-3 完整闭环。**区别于 AC-C4-24**（E6 Pause 冻结续接、令牌保留）：本 AC 验证 run-end Stop 路径（Death 与 Victory 均适用）的**全清**语义，二者不可混淆。

## Open Questions

1. **C4 测试接缝的 DI 注入接口命名** — AC-C4 系列单元测试需要注入模拟拍钟（参照 F2 `MockDSPClock` / `IDSPClock` 范式）。C4 应暴露的依赖注入接口的名称与形态待 `lead-programmer` 在架构阶段确认。GDD 层面只约定"必须可注入模拟拍源以支撑确定性测试"，不锁定接口签名——接口签名属 ADR 范畴。
2. **静态扫描脚本位置与 CI 挂载点** — AC-C4-26 / AC-C4-27（`c4_reads_audiosettings_dsptime`、`beatclock_lifecycle_bypass` 禁用模式扫描）需要一个静态代码扫描脚本。脚本位置、运行器、CI 挂载点待 `technical-director` / CI 配置确认。命名惯例已与 F2 现有 forbidden pattern 对齐。
3. **闭区间边界归属（已存档，无需裁定）** — F-2 `ThreatWeight` 的 `≤D_near` / `≤D_mid` 闭区间边界归入更近区间，此为公式直接推导（非独立设计决策），已在 AC-C4-08 固化为测试点。此处仅存档说明来源。
