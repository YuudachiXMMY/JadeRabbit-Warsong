# C2 连招图系统 (Combo Graph System)

> **Status**: Revised (2026-08-10 blocker fix) — Pending Re-review
> **Author**: Jadyn Wu + Claude Code Agents
> **Last Updated**: 2026-08-10
> **Implements Pillar**: 支柱二：武侠手感不可妥协 (Wuxia Feel First) / 支柱四：构筑驱动复盘 (Build Depth Over Stat Inflation)
> **System ID**: C2
> **Layer**: Core | **Milestone**: MVP | **Design Order**: 8
> **Architecture Reference**: ADR-D3 (RhythmJudgmentEvent), ADR-D5 (Animancer 动画管线), F4 SO 数据层

---

## Overview

C2 连招图系统是《玉兔战歌》战斗核心的**数据驱动连招状态机**。它消费 F4 提供的 ScriptableObject 数据契约（`ComboGraphSO` / `AttackNodeSO` / `ComboEdge`）构建每把武器的连招图，并订阅 C1 节奏核心发布的 `RhythmJudgmentEvent`。玩家每次踩节拍出招时，C1 依据判定评级（Perfect/Good/Miss）赋予该次攻击一个 `CancelTier`（Advanced/Standard/Closed），C2 用它决定当前攻击节点的取消窗口内**哪些连招分支可达**：Perfect 解锁进阶分支（`cancelWindowType=Advanced`），Good 及 Off 模式 Perfect 仅解锁标准分支，Miss 关闭取消。C2 沿连招图的边遍历——以当前节点为 `fromNodeId`、玩家输入为 `requiredInput` 匹配 `ComboEdge`；匹配成功则推进到下一攻击节点，无匹配/超时/Miss 则重置回根节点。连招段数经 `combo_multiplier` 曲线注入伤害公式，攻击动画则通过 Animancer 的 `AnimancerState.Speed` 按当前 BPM 动态变速（ADR-D5），确保连段手感在任意 BPM 下节奏一致。从玩家侧看，这套数据层兑现的是**「踩准节奏、行云流水地串起一套连招」的掌控快感**——分支解锁的深浅直接由玩家的节奏精度决定。

*架构参考*：ADR-D3（RhythmJudgmentEvent 事件契约）、ADR-D5（Animancer 动画管线）、F4 SO 数据层。

## Player Fantasy

**核心幻想：行云流水的武学宗师。** 玩家要感受的不是「背板出招表」，而是**在节奏中即兴串招**的掌控感——每一次踩准节拍，身法便顺势接上，一套连招如水银泻地般连绵不断；每一次抢拍或拖拍，节奏一断，套路也随之收势归零。这份手感直接服务 **支柱二：武侠手感不可妥协**——连招的流畅度不是数值堆出来的，而是玩家亲手用节奏精度「打」出来的。

**直接层（玩家主动感受）：** 玩家在攻击的取消窗口内做分支抉择——是稳妥地接标准分支，还是赌一把 Perfect 去解锁那些只有踩准节拍才够得着的进阶杀招。段数越滚越高、伤害倍率越叠越猛，屏幕反馈（Feel/MMFeedbacks）越来越炽烈，玩家能清晰地「感到自己越打越顺、越打越狠」。这是一种**主动编织、即时兑现**的快感：输入—判定—解锁—接招的循环短到以拍为单位，正反馈几乎贴脸。

**底层（玩家不必察觉）：** 玩家永远不需要知道背后是一张 `ComboGraphSO` 的有向图、`CancelTier` 在门控哪条边、`combo_multiplier` 走的是哪条曲线。这些数据结构越隐形越好——玩家只应体验到「我踩准了，所以这招接上了」的因果直觉，而非「我满足了 edge 匹配条件」的系统感。

**反面幻想（明确排除）：** C2 **不是**「攒够段数放无敌大招、绕过节奏碾压」的连招（对齐 game-concept 反支柱）。进阶取消是**大师级专精的独占奖励**，只对踩准 Perfect 的玩家开放，绝不能退化成无脑连点也能触发的保底机制。段数带来的是节奏张力，不是节奏豁免权。

**参考坐标：** Hi-Fi Rush（节奏与连招同频的即时正反馈）、只狼（招架/连段的「节奏即防御」压迫感）、鬼泣（分支连招的自我表达空间）——三者共同点是：**连招的流畅度由玩家的时机精度决定，而非由按键序列的记忆量决定。**

> **lean 模式说明**：本章未经 `creative-director` 专家过目（lean 复查模式跳过非 D/H 章节的专家召唤）。生产实装前建议补一次人工审阅，校准语气与玩家幻想措辞。

## Detailed Design

### Core Rules

**规则 1｜连招图加载**：武器装备时，C2 加载该武器 `WeaponSO.comboGraph` 指向的 `ComboGraphSO`，将其 `nodes` 构建为 `Dictionary<string, AttackNodeSO>`（键为 node id），并校验 `rootNodeId`。图位置（`_currentNodeId`）初始化为 `rootNodeId`。

**规则 2｜出招输入触发**：C2 订阅 C1 发布的事件 `OnRhythmJudgment`（`event Action<RhythmJudgmentEvent>`；事件名 = `OnRhythmJudgment`、载荷类型名 = `RhythmJudgmentEvent`，二者不冲突，权威定义见 `rhythm-core.md` §C1-F6 W-04 规范化）。玩家每次攻击输入经 C1 判定后，事件携带 `Rating` / `AttackType` / `CancelTier` / `ComboCountBefore` / `ComboCountAfter` / `OffsetMs` 抵达 C2。C2 **只读** 这些字段，不回写。

**规则 3｜分支匹配**：收到事件后，C2 在 `edges` 中查找满足 `fromNodeId == _currentNodeId` **且** `requiredInput` 匹配本次输入（`AttackType`）的 `ComboEdge`。**[BC-5 已解决]** `ComboEdge.requiredInput` 与 C1 `RhythmJudgmentEvent.AttackType`、F4-S1 同为权威 `AttackType {LightAttack, HeavyAttack, ChargedAttack}` 枚举（F3 权威），**直接比较无需映射**（上游 F4 已由 `AttackInputType {Normal,Heavy,Special}` 统一至此，全链无枚举转换）。

**规则 4｜时机门控（取消窗口 + 输入缓冲）**：匹配到的边仅当输入时机落在当前节点动画的取消窗口 `[cancelWeight, 1.0]`（归一化时间）内才可遍历。**输入缓冲**：窗口开启前极短时间（`inputBufferBeats`，随 BPM 换算）内按下的输入被缓冲，窗口一开即触发。缓冲**仅影响连招图遍历**——节奏评级（Perfect/Good/Miss）始终由 C1 用 `InputAction.performed` 真实时间戳判定，缓冲不介入。

**规则 5｜评级门控（CancelTier × cancelWindowType）**：时机通过后，比对目标节点 `cancelWindowType` 与本次 `CancelTier`（序：Advanced > Standard > Closed）：

| 目标节点 cancelWindowType | 通过条件 |
|---|---|
| `None` | 任意非 Closed 评级（仅时机门控）|
| `Standard` | `CancelTier ≥ Standard`（Good、Standard 或 Advanced）|
| `Advanced` | `CancelTier == Advanced`（仅 HalfBeat/Beat 模式的 Perfect）|
| 任意 | `CancelTier == Closed`（Miss）→ 永不通过，触发重置 |

**规则 6｜分支推进**：时机门 + 评级门**均通过** → `_currentNodeId` 推进到 `edge.toNodeId`，经 Animancer 播放其 `animationClip`，`AnimancerState.Speed = currentBpm / node.authoredBpm`（ADR-D5，禁绑死帧数）。

**规则 7｜软重置（仅图位置）**：`_currentNodeId` 回退到 `rootNodeId` 的触发条件：(a) 输入无匹配边；(b) `CancelTier == Closed`（Miss）；(c) 连招保持窗耗尽。**重置只作用于图位置，绝不清零段数**——段数是 C1 的域，Miss 不清零（F6 边界合约）。

**规则 8｜连招保持窗（宽限）**：某攻击节点动画播完且无续接输入时，进入一个极短的「连招保持窗」（`comboHoldBeats`，随 BPM 换算），期间玩家仍可续接连招；窗口内有匹配输入 → 推进；耗尽 → 回根。

**规则 9｜combo_multiplier 馈入**：C2 读取 C1 的 `ComboCountAfter`，经 `combo_multiplier` 曲线（见 Formulas）映射为伤害倍率注入伤害公式。C2 **只读段数、不修改**。

**规则 10｜Off 模式隔离**：Off 模式 Perfect 被 C1 封顶为 `CancelTier.Standard`（C1 规则 10）。故 Off 模式下 `Advanced` 分支永不可达——**进阶取消保持为节奏模式独占**（对齐 game-concept 不可豁免规则：进阶取消 = 大师专精独占）。

**规则 11｜数据容错**（承接 F4 AC-F4-12 迁移）：`rootNodeId` 不在 `nodes` 中 → 回退 `nodes[0]` + `LogError`；`nodes` 为空 → 抛 `InvalidDataException`（加载失败）；无效边 ID（`fromNodeId`/`toNodeId` 不存在）→ 跳过该边 + `Error` 日志（AC-F4-31）。

**规则 12｜BD-2 取消窗适应窗口（BPM 变更宽容期）｜[BD-2]**：C2 收到 F2 的**边界生效事件 `OnMeterChanged`**（生效时广播，与 C1 重锚拍网格所订阅的**同一事件**、锚定 `MeterChangeEventData.predictedEffectiveDspTimeRaw`；**区别于**接受时的 `OnMeterChangeAccepted`——后者仅用于规则 6 的 Animancer 预变速，见 EC9/AC-C2-33）后，进入一个 **适应窗口 = `adaptationWindowBeats` 拍**（Tuning Knob，默认 2，范围 [0,4]）。窗口内，取消窗 `[cancelWeight, 1.0]` 的**绝对毫秒判定宽度**取 `max(变更前窗口ms, 变更后窗口ms)`（即取**较宽者**）——即便攻击动画已由 Animancer 按新 `currentBpm` 变速（规则 6/F4），取消窗的可接受输入时机以毫秒计**不随 BPM 上升而骤缩**；适应窗口结束后恢复新 BPM 标准宽度（见 Formulas F7）。此举与 **C1 判定窗**采用同一 `max(before, after)` 宽容规则、与 **C4 `SlotCapacity` 按每秒令牌速率归一化**共同承接 BD-2「BPM 跃升三系统难度尖峰」，避免玩家犯错代价最大的转阶段瞬间取消窗骤缩（三文档 Formulas 互引 BD-2）。

### States and Transitions

C2 连招状态机（图位置维度，独立于 C1 的评级状态）：

| 状态 | 含义 | 进入 | 转出 |
|---|---|---|---|
| **AtRoot** | 位置 = rootNodeId，无进行中攻击 | 加载完成 / 任意重置 | 匹配根节点出边的输入 → InAttack |
| **InAttack（前摇）** | 攻击动画播放中，尚未到 cancelWeight | 分支推进（规则 6）| 动画到达 cancelWeight → CancelWindowOpen；动画播完未取消 → HoldWindow |
| **CancelWindowOpen** | `[cancelWeight, 1.0]`，接受取消输入 | InAttack 到达 cancelWeight | 有效输入（时机+评级双过）→ InAttack（下一节点）；无效输入（无边/评级不足/Miss）→ Reset；窗口关闭（动画播完）→ HoldWindow |
| **HoldWindow（保持窗）** | 动画播完后的极短宽限 | 取消窗口未取消而关闭 | 匹配续接输入 → InAttack；无匹配输入 → Reset；保持窗耗尽 → Reset |
| **Reset** | 瞬时态，仅清图位置 | 规则 7 任一触发 | 立即 → AtRoot（段数不动）|

**不变量**：任何状态转换都不得写 C1 的 `CurrentCombo`。C2 只读段数、只写 `_currentNodeId`。

### Interactions with Other Systems

| 系统 | 方向 | 数据流 / 接口 | 归属 |
|---|---|---|---|
| **C1 节奏核心** | 上游 | 订阅事件 `OnRhythmJudgment`（载荷 `RhythmJudgmentEvent`：Rating/AttackType/CancelTier/ComboCount/OffsetMs）| C1 拥有评级与段数；C2 拥有图位置 |
| **F4 SO 数据层** | 上游（数据）| 只读消费 `ComboGraphSO`/`AttackNodeSO`/`ComboEdge`，加载期构建节点字典 | F4 拥有 schema，C2 不改数据 |
| **ADR-D5 / Animancer** | 依赖 | 播放 `node.animationClip`，`AnimancerState.Speed = currentBpm / authoredBpm` | C2 驱动攻击层动画（TDE Animator 层不变）|
| **C5 武器系统** | 下游 | `WeaponSO.comboGraph` 引用 ComboGraphSO；换武器时 C2 重载连招图并回根 | C5 定义武器→连招图绑定 |
| **C6 命中判定** | 下游 | C2 在 `node.hitboxStartNormalizedTime` 起、持续 `hitboxDurationNormalized` 发出「攻击节点激活」信号；C6 据此生成命中盒 | C6 拥有命中盒生成 |
| **P1 HUD** | 下游 | 段数由 C1 提供给 HUD 显示；C2 可另发「分支推进/进阶解锁」事件供 HUD 高亮当前连招 | P1 拥有显示层 |
| **Feel / MMFeedbacks** | 下游 | C2 发布分支推进/进阶取消事件，Feel 订阅播放反馈；不在 CombatCore 内直接 using Feel 命名空间（边界隔离）| Feel 拥有反馈序列 |

## Formulas

### F1｜combo_multiplier（连招段数 → 伤害倍率）

`combo_multiplier(count) = min(cap, 1.0 + k × count^p)`

| 变量 | 类型 | 范围 | 含义 |
|---|---|---|---|
| `count` | int | [0, 999] | 当前连招段数（来自 C1 `ComboCountAfter`，只读）|
| `k` | float | [0.005, 0.02]，**取 0.01** | 增速系数（主调参旋钮）|
| `p` | float | **锁定 1.2**（±0.1 手感微调）| 加速指数 |
| `cap` | float | [4.0, 6.0]，**取 5.0** | 硬封顶，防高段发散 |
| 输出 | float | [1.0, cap] | 乘入 final_damage |

**采样表（k=0.01, p=1.2, cap=5.0）**：

| count | 0 | 5 | 10 | 20 | 30 | 50 | 100 | 147 | **148** | 999 |
|---|---|---|---|---|---|---|---|---|---|---|
| combo_multiplier | 1.00 | 1.07 | 1.16 | 1.36 | 1.59 | 2.09 | 3.51 | 4.99 | **5.00** | 5.00 |

**触顶点** count = 148（约 74 次 Perfect，首个使 `min()` 封顶的整数）；148→999 为平台段，倍率恒 5.0。count=147 ≈ 4.99 仍未封顶。count=0 精确等于 1.0（0^1.2=0，无需特判）。

### F2｜C2 对 final_damage 的贡献

总公式（game-concept）：`final_damage = base_damage × rating_multiplier × combo_multiplier × perk_multiplier`。C2 只定义 `combo_multiplier`，其余为只读输入。

**边界示例**（苗刀 `base_damage=25.0`，Perfect `rating_multiplier=1.0`，`perk_multiplier=1.0`；`rating_multiplier` 评级倍率 Perfect/Good/Miss = 1.0/0.75/0.5 由 C1 rhythm-core 拥有，C2 只读引用）：

| count | combo_multiplier | final_damage |
|---|---|---|
| 0 | 1.00 | 25×1.0×1.0×1.0 = **25.0** |
| 50 | 2.09 | 25×1.0×2.09×1.0 = **52.25** |
| 999（触顶）| 5.00 | 25×1.0×5.0×1.0 = **125.0** |

**复合上限**（perk 顶格 2.0、count 触顶、Perfect）：`25×1.0×5.0×2.0 = 250.0`（裸 base 的 10×）——供 balance 侧制定 Boss/精英 HP 曲线时作为「极限连招+满配心法」压力基准。（对齐 C1 `rhythm-core.md` 权威 `rating_multiplier`；BC-1 修订）

### F3｜ComboMax（段数上限）

`ComboMax = 999`。combo_multiplier 定义域 `count ∈ [0, 999]`。**跨系统一致（BC-7 已修）**：C1 已将段数 clamp 抬至 999（`rhythm-core.md`，2026-08-10 blocker fix BC-7），与本定义域一致——运行时 combo_multiplier 可达设计上限 ×5.0（原 clamp 50 会将其截到 ×2.09）。注：因 cap 在 count=148 触顶，148 以上仅影响 HUD 连击数显示。

### F4｜Animancer 攻击动画变速（ADR-D5）

`AnimancerState.Speed = currentBpm / node.authoredBpm`

| 变量 | 范围 | 含义 |
|---|---|---|
| `currentBpm` | bpm_range [80, 240] | 当前关卡 BPM |
| `node.authoredBpm` | [60, 240]，默认 120 | 该攻击动画的制作基准 BPM（F4）|

**示例**：currentBpm=180、authoredBpm=120 → Speed=1.5（动画加速 50%）。**禁绑死帧数**——时长随 BPM 连续缩放。

### F5｜取消窗口与缓冲时机（归一化时间 / 拍）

- **取消窗口** = `[cancelWeight, 1.0]`（当前节点动画归一化时间，cancelWeight 来自 F4）。
- **输入缓冲** `bufferSeconds = inputBufferBeats × 60 / currentBpm`；`inputBufferBeats ∈ [0, 0.25]`，建议 0.1。窗口开启前 bufferSeconds 内的输入被缓冲，仅作用于连招图遍历。
- **连招保持窗** `holdSeconds = comboHoldBeats × 60 / currentBpm`；`comboHoldBeats ∈ [0, 0.5]`，建议 0.25。动画播完后此窗内可续接，超时回根。
- **示例**（currentBpm=120）：inputBufferBeats=0.1 → 50ms 缓冲；comboHoldBeats=0.25 → 125ms 保持窗。

### F6｜评级门控约束（perk 输入隔离）

`perk_multiplier` 的输入变量集合**不得包含** `count` 或 `combo_multiplier` 本身（防 combo² 复合爆炸）。任何例外须走 ADR 审批。（详见 Dependencies。）

### F7｜BD-2 取消窗适应窗口（BPM 变更宽容期）｜[BD-2]

取消窗归一化区间 `[cancelWeight, 1.0]` 的**绝对毫秒宽度**随 BPM 变化（动画经 Animancer 变速）：

`cancelWindowMs(bpm) = (1.0 − cancelWeight) × animBaseDurationMs × node.authoredBpm / bpm`

BPM 变更事件 `OnMeterChanged`（BPM: `bpm_before → bpm_after`）后的适应窗口内，取消窗**有效毫秒宽度**取较宽者：

`cancelWindowMs_effective = max( cancelWindowMs(bpm_before), cancelWindowMs(bpm_after) )`，持续 `adaptationWindowBeats` 拍；窗口结束后 `cancelWindowMs_effective = cancelWindowMs(bpm_after)`（恢复新 BPM 标准宽度）。

| 变量 | 类型 | 范围 | 含义 |
|---|---|---|---|
| `adaptationWindowBeats` | int | [0, 4]，**锁定 2**（owner 联合锁定 2026-08-11） | BD-2 适应窗口时长（拍）；0 = 关闭宽容立即用新 BPM 宽度。单一 owner=`RhythmConfigSO.AdaptationWindowBeats`（C1 权威），本名为引用别名、语义等同；registry `adaptation_window_beats` |
| `bpm_before` / `bpm_after` | float | bpm_range [80, 240] | `OnMeterChanged` 变更前/后 BPM |
| `cancelWeight` | float | [0.0, 1.0] | 取消窗开启点（逐节点 authored，F4）|
| `animBaseDurationMs` | float | >0 | 该节点动画在 `authoredBpm` 下的基准毫秒时长（F4）|

**示例**（`cancelWeight=0.5`, `animBaseDurationMs=1000`, `authoredBpm=120`；Boss 转阶段 `bpm_before=120 → bpm_after=180`）：`cancelWindowMs(120)=500ms`、`cancelWindowMs(180)≈333ms`；适应窗口内取 `max(500, 333)=500ms`（不骤缩至 333ms），2 拍后恢复 333ms。

**BD-2 三文档互引**：本公式与 **C1 `rhythm-core.md` 判定窗**的同款 `max(before, after)` 宽容规则、**C4 `combat-director.md` `SlotCapacity` 每秒令牌速率归一化**共同缓解「BPM 跃升三系统难度尖峰」（Flow 反模式）。三者 Formulas 相互引用 BD-2。

## Edge Cases

| # | 情形 | 明确行为 |
|---|---|---|
| EC1 | `nodes` 列表为空 | 抛 `InvalidDataException`，连招图加载失败，武器无法装备（承接 F4）|
| EC2 | `rootNodeId` 不在 `nodes` 中 | 回退 `nodes[0]` + `LogError`（AC-F4-12 迁移）|
| EC3 | 无效边 ID（`fromNodeId`/`toNodeId` 不存在）| 加载期跳过该边 + `Error` 日志（AC-F4-31）|
| EC4 | `hitboxDurationNormalized == 0.0` | C2 记 `Warning`；动画照播但无命中窗（承接 F4 AC-F4-32 静默失效）|
| EC5 | 取消窗口外 + 非保持窗的输入 | 先回根（图位置归 rootNodeId），再按根节点出边匹配该输入：匹配则视为 AtRoot 新起手并推进，无匹配则忽略该输入 |
| EC6 | 取消窗口内打出 Miss | `CancelTier=Closed` → 评级门永不过 → 软重置回根（**段数不清零**）|
| EC7 | 段数已达 ComboMax=999 | C1 clamp 生效；combo_multiplier 早在 count=148 触顶恒 5.0，段数不再增长 |
| EC8 | 连招中途换武器（C5）| C2 重载新连招图，图位置回根，进行中攻击动画收势中断 |
| EC9 | 连招中途 BPM 变更（F2 `OnMeterChangeAccepted`）| Animancer Speed 实时按新 currentBpm 重算；取消窗口用归一化时间不受影响；buffer/hold 秒数换算改用新 BPM |
| EC10 | Off 模式打出 Perfect | CancelTier 封顶 Standard → Advanced 分支不可达（规则 10 隔离），标准分支正常 |
| EC11 | 同节点多条同 `fromNodeId`+`requiredInput` 边 | 取 `edges` 中第一条匹配 + `LogWarning`（数据应保唯一，F4 校验兜底）|
| EC12 | 自环边（`fromNodeId==toNodeId`）| F4 已禁止；C2 加载期若遇到则跳过 + `Error` |
| EC13 | 连招图含非自环环路（A→B→A）| 允许（连招可循环），正常遍历，无特殊处理 |
| EC14 | 输入缓冲期内连续多次输入 | 缓冲窗内保留**最后一次**输入（覆盖），窗口开启时用它匹配 |
| EC15 | Perfect 冻帧期间的取消窗（SG-1）| 冻帧为**表现层**效果，**不冻结玩家攻击 Animancer 时钟**：取消窗归一化时间照常随 BPM 锁定的攻击动画推进（规则 6/F4），与 C1 的 DSP 判定保持**同一时域**，冻帧后**不产生取消窗滞后**。**禁止**冻帧对玩家攻击层 `AnimancerState.Speed` 置 0（否则每次 Perfect 累加 33–66ms「动画 vs 音乐」漂移，多次后节奏体验崩溃、破坏连招接续）。登记 registry forbidden pattern `freeze_zeroes_player_attack_speed`。与 C1 E11 / F2 冻帧 Edge Case 对称（SG-1 三系统同时基合约）|

## Dependencies

**上游（C2 依赖）：**

| 系统 | 依赖内容 | 状态 |
|---|---|---|
| C1 rhythm-core | `RhythmJudgmentEvent`（评级/CancelTier/段数）；C1 段数 clamp 已抬至 999（BC-7 已修，与 ComboMax 一致）| 已批准 |
| F4 so-data-layer | `ComboGraphSO`/`AttackNodeSO`/`ComboEdge` schema | 已批准 |
| F2 audio-beat-clock | `currentBpm` 来源、`OnMeterChangeAccepted` BPM 变更事件 | 已批准 |
| ADR-D3 / ADR-D5 | RhythmJudgmentEvent 契约 / Animancer 管线 | 已接受 |

**下游（依赖 C2）：**

| 系统 | 期望 C2 提供 | 状态 |
|---|---|---|
| C5 武器系统 | `WeaponSO.comboGraph` 绑定，换武器重载 | 未设计 |
| C6 命中判定 | 攻击节点激活信号 + 命中盒时机 | 未设计 |
| P1 HUD | 段数显示（源自 C1）+ C2 分支推进事件（可选高亮）| 未设计 |
| Feel/MMFeedbacks | 分支推进/进阶取消事件订阅 | 基础设施 |
| C3 心法系统（perk）| `perk_multiplier` **输入不得含 count / combo_multiplier**（防 combo² 爆炸，例外走 ADR）| 未设计 |

**双向登记提醒**：C5/C6/P1/C3 的 GDD 撰写时须回引 C2（当前均未设计，届时补双向引用）。

## Tuning Knobs

| 旋钮 | 安全范围 | 默认 | 影响的玩法维度 |
|---|---|---|---|
| `k` | [0.005, 0.02] | 0.01 | combo 倍率增速；偏低连招无成长感，偏高曲线过陡、过早触顶 |
| `p` | [1.1, 1.3] | 1.2 | 加速曲率；<1.0 变递减违背「加速」，>1.5 中段陡拐破坏「温和」|
| `cap` | [4.0, 6.0] | 5.0 | combo 倍率上限；**支柱四杠杆**——偏高稀释 build 权重、需重核 Boss HP |
| `ComboMax` | 固定 | 999 | 段数上限/combo_multiplier 定义域/HUD 连击天花板 |
| `inputBufferBeats` | [0, 0.25] | 0.1 | 取消输入缓冲宽容；偏高会让玩家误以为能抢拍 |
| `comboHoldBeats` | [0, 0.5] | 0.25 | 连招保持窗宽容；偏高削弱「节奏断=连招断」张力 |
| `adaptationWindowBeats`（BD-2）| [0, 4] | **2**（owner 联合锁定 2026-08-11） | BPM 变更后取消窗 `max` 宽容期时长（拍）；偏高延长「旧手感」惯性、偏低回归 BPM 尖峰。与 C1 判定窗适应窗口同值对齐、单一 owner=`RhythmConfigSO.AdaptationWindowBeats`（本名为引用别名）；C1/C2/C4 已联合锁定，registry `adaptation_window_beats`（BD-2）|
| `cancelWeight`（逐节点，F4）| [0.0, 1.0] | 逐招 authored | 取消窗口开启点，决定连招节奏松紧 |
| `cancelWindowType`（逐节点，F4）| enum | 逐招 authored | 分支评级门（None/Standard/Advanced）|

## Visual/Audio Requirements

C2 只**发布事件**，视觉/音效由 Feel/MMFeedbacks 与音频系统订阅播放（边界隔离，CombatCore 不直接 using Feel）。对美术/音频团队的需求：

- **分支推进反馈**：每次成功续接，反馈强度随段数递增（屏震/闪光/拖尾渐强），兑现「越打越顺、越打越狠」的直接层幻想。
- **进阶取消解锁（Perfect 独占）**：Advanced 分支触发时需一记**区别于标准分支的「大师级」视听华彩**，让玩家清晰感知「我踩准了 Perfect，接上了别人接不到的招」。
- **软重置反馈**：连招断裂/回根时给**收势感**的克制反馈（收招气力泄尽），不可用惩罚性巨响——失误已是惩罚，避免二次挫败。
- **攻击动画**：逐节点 authored clip（F4），经 Animancer 按 BPM 变速（ADR-D5，禁绑死帧数）。
- **音频对拍**：攻击音效须与 F2 节拍时钟对齐；连招里程碑（如触顶 5.0）可选叠加 stinger。

## UI Requirements

- **HUD 连击计数器**：显示当前段数（数据源 C1），上限 999。纯显示、无交互——满足主机 d-pad / 移动端触控无悬停等价（展示元素本身无需导航）。
- **当前连招 / 分支高亮**（可选增强）：订阅 C2 分支推进事件，高亮玩家当前所处连招路径，帮助玩家建立「我在这套连招的哪一段」的空间感。
- **进阶取消可用提示**：当 Perfect 开启 Advanced 分支时给**克制的可用线索**（微光/图标），telegraph 而不越俎代庖替玩家出招。
- **跨平台**：计数器与提示为显示层，主机/移动端按可读性缩放文字；不引入任何需悬停或指针精确点击的元素。

## Acceptance Criteria

门控级别：**BLOCKING** = 自动化单测/集成测试必过方可标记 Done；**ADVISORY** = 建议手动验证。

### 加载与数据容错（Logic — BLOCKING）

| # | 验收条件 | 门控 |
|---|---|---|
| AC-C2-01 | 装备武器时，C2 加载 `WeaponSO.comboGraph` 指向的 `ComboGraphSO`，`nodes` 构建为 `Dictionary<string, AttackNodeSO>`，键数 == `nodes.Count` | BLOCKING |
| AC-C2-02 | 加载后 `_currentNodeId == rootNodeId` | BLOCKING |
| AC-C2-03 | `nodes` 为空 → 抛 `InvalidDataException`，武器装备失败（EC1） | BLOCKING |
| AC-C2-04 | `rootNodeId` 不在 `nodes` 中 → `_currentNodeId` 回退 `nodes[0]` 且发 `LogError`（EC2/AC-F4-12） | BLOCKING |
| AC-C2-05 | 边 `fromNodeId`/`toNodeId` 不存在于 `nodes` → 加载期跳过该边 + `Error` 日志（EC3/AC-F4-31） | BLOCKING |
| AC-C2-06 | 自环边（`fromNodeId==toNodeId`）加载期被跳过 + `Error`（EC12） | BLOCKING |

### 分支匹配与门控（Logic — BLOCKING）

| # | 验收条件 | 门控 |
|---|---|---|
| AC-C2-07 | 收到 `RhythmJudgmentEvent` 后，仅在 `edges` 中匹配 `fromNodeId==_currentNodeId` 且 `requiredInput==AttackType` 的边（规则 3） | BLOCKING |
| AC-C2-08 | 输入时机在 `[cancelWeight, 1.0]` 归一化窗口外且非缓冲/保持窗 → 不遍历该边（规则 4） | BLOCKING |
| AC-C2-09 | `cancelWindowType==None` 目标节点：任意非 Closed 评级通过（规则 5 表行 1） | BLOCKING |
| AC-C2-10 | `cancelWindowType==Standard` 目标节点：`CancelTier ∈ {Standard, Advanced}` 通过，Good 满足 Standard | BLOCKING |
| AC-C2-11 | `cancelWindowType==Advanced` 目标节点：仅 `CancelTier==Advanced`（HalfBeat/Beat 模式 Perfect）通过 | BLOCKING |
| AC-C2-12 | `CancelTier==Closed`（Miss）→ 任意 `cancelWindowType` 均不通过，触发软重置（规则 5 表末行） | BLOCKING |
| AC-C2-13 | 时机门 + 评级门均通过 → `_currentNodeId` 推进到 `edge.toNodeId`（规则 6） | BLOCKING |
| AC-C2-14 | 同 `fromNodeId`+`requiredInput` 多条边 → 取 `edges` 第一条匹配 + `LogWarning`（EC11，确定性） | BLOCKING |

### 输入缓冲与保持窗（Integration — BLOCKING）

| # | 验收条件 | 门控 |
|---|---|---|
| AC-C2-15 | 窗口开启前 `bufferSeconds = inputBufferBeats × 60 / currentBpm` 内的输入被缓冲，窗口一开即触发（规则 4/F5） | BLOCKING |
| AC-C2-16 | 缓冲窗内多次输入 → 仅保留最后一次用于匹配（EC14） | BLOCKING |
| AC-C2-17 | 输入缓冲**不介入** C1 评级：Perfect/Good/Miss 仍由 C1 的 `InputAction.performed` 真实时间戳判定（规则 4 约束） | BLOCKING |
| AC-C2-18 | 动画播完未取消 → 进入保持窗 `holdSeconds = comboHoldBeats × 60 / currentBpm`，窗内匹配输入可续接（规则 8） | BLOCKING |
| AC-C2-19 | 保持窗耗尽无匹配输入 → 软重置回根（规则 7c/8） | BLOCKING |

### 软重置与段数隔离（Logic — BLOCKING，F6 边界合约）

| # | 验收条件 | 门控 |
|---|---|---|
| AC-C2-20 | 软重置（无匹配边 / Miss / 保持窗耗尽任一）→ `_currentNodeId = rootNodeId`（规则 7） | BLOCKING |
| AC-C2-21 | **不变量**：任何软重置/状态转换均不写 C1 `CurrentCombo`；Miss 后段数不清零（F6 合约/规则 7/EC6） | BLOCKING |
| AC-C2-22 | C2 从不调用任何修改 C1 段数的 API（代码级：CombatCore 内无对 C1 count 写入的调用路径） | BLOCKING |
| AC-C2-23 | 取消窗口外非保持窗输入：先回根，再按根出边匹配（匹配则新起手，无匹配则忽略）（EC5） | BLOCKING |

### combo_multiplier 公式（Logic — BLOCKING，精确断言）

| # | 验收条件（k=0.01, p=1.2, cap=5.0） | 门控 |
|---|---|---|
| AC-C2-24 | `combo_multiplier(0) == 1.0`（0^1.2=0，无需特判） | BLOCKING |
| AC-C2-25 | `combo_multiplier(50) ≈ 2.093`（±0.005） | BLOCKING |
| AC-C2-26 | `combo_multiplier(147) ≈ 4.988`（±0.005），**未封顶** | BLOCKING |
| AC-C2-27 | `combo_multiplier(148) == 5.0`（首个被 `min()` 封顶的整数） | BLOCKING |
| AC-C2-28 | `combo_multiplier(999) == 5.0`（平台段） | BLOCKING |
| AC-C2-29 | 输出恒 `∈ [1.0, cap]`，对 `count ∈ [0, 999]` 全域单调不减 | BLOCKING |
| AC-C2-30 | C2 读取 C1 `ComboCountAfter` 计算倍率，**不回写**段数（规则 9） | BLOCKING |

### Animancer 变速（Integration — BLOCKING）

| # | 验收条件 | 门控 |
|---|---|---|
| AC-C2-31 | 分支推进时 `AnimancerState.Speed == currentBpm / node.authoredBpm`（ADR-D5/F4） | BLOCKING |
| AC-C2-32 | currentBpm=180、authoredBpm=120 → Speed==1.5（F4 示例） | BLOCKING |
| AC-C2-33 | 连招中途 BPM 变更（`OnMeterChangeAccepted`）→ Speed 按新 currentBpm 实时重算；归一化取消窗口不受影响（EC9） | BLOCKING |
| AC-C2-34 | 攻击时长不绑死帧数——同一节点在两个不同 BPM 下播放实际时长之比 == BPM 反比（禁绑死帧数验证） | BLOCKING |

### Off 模式隔离与边界（Logic — BLOCKING）

| # | 验收条件 | 门控 |
|---|---|---|
| AC-C2-35 | Off 模式 Perfect → `CancelTier` 封顶 Standard → `Advanced` 分支永不可达（规则 10/EC10） | BLOCKING |
| AC-C2-36 | Off 模式标准分支（`cancelWindowType≤Standard`）正常可达（隔离不误伤标准连招） | BLOCKING |
| AC-C2-37 | `perk_multiplier` 输入集合不含 `count`/`combo_multiplier`（F6，防 combo² 爆炸；测试：喂入 perk 计算的参数快照断言无该二者） | BLOCKING |

### BD-2 取消窗适应窗口（Logic — BLOCKING）

| # | 验收条件（规则 12 / F7） | 门控 |
|---|---|---|
| AC-C2-40 | `OnMeterChanged`（BPM 上升 120→180、`adaptationWindowBeats=2`）后适应窗口内，取消窗有效毫秒宽度 == `max(cancelWindowMs(120), cancelWindowMs(180))` == `cancelWindowMs(120)`（**取较宽者，不骤缩**）（BD-2/规则 12/F7）| BLOCKING |
| AC-C2-41 | 适应窗口经过 `adaptationWindowBeats` 拍后，取消窗有效毫秒宽度恢复 == `cancelWindowMs(bpm_after)`（新 BPM 标准宽度）（BD-2/F7）| BLOCKING |
| AC-C2-42 | `adaptationWindowBeats=0` → 无宽容期，变更后立即使用 `cancelWindowMs(bpm_after)`（边界值，BD-2）| BLOCKING |

### 命中盒时机与反馈信号（Integration — ADVISORY）

| # | 验收条件 | 门控 |
|---|---|---|
| AC-C2-38 | 分支推进后，C2 在 `hitboxStartNormalizedTime` 起、持续 `hitboxDurationNormalized` 发「攻击节点激活」信号给 C6；`hitboxDurationNormalized==0` → `Warning` 且无命中窗（EC4/AC-F4-32） | ADVISORY |
| AC-C2-39 | 进阶取消触发时发布可供 Feel 订阅的「进阶解锁」事件，且区别于标准分支推进事件（Visual/Audio 需求，手动验证华彩反馈） | ADVISORY |

## Open Questions

| # | 问题 | 状态 / 归属 |
|---|---|---|
| OQ1 | **C1 段数 clamp 50→999 同步（✅ 已解决 2026-08-10，BC-7）**：C1 rhythm-core.md 已将 `CurrentCombo`/段数 clamp 抬至 999（BC-7 blocker fix），与 registry `ComboMax=999` 及 combo_multiplier 定义域 `[0,999]` 一致——运行时倍率可达 ×5.0。原下游债务已清，本项仅存档。 |
| OQ2 | **count>999 无防御**：combo_multiplier 的 `min(cap, …)` 已保证 count 超 999 时倍率仍恒 5.0，故公式侧无需额外 clamp；ComboMax=999 由 C1 clamp 保证段数不越界。若未来 C1 clamp 与 registry ComboMax 脱钩，须回看此处。**状态**：设计上安全，仅记录假设。 |
| OQ3 | **EC5 回根语义已定**：取消窗口外输入统一「先回根、再按根出边匹配」（已写入 EC5/AC-C2-23）。此举让「断连后立刻起新招」无缝，但也意味着窗口外的误触会静默重置图位置——需 playtest 确认不会造成「我以为在连招中却被打回根」的挫败。**状态**：已决策，待手感验证。 |
| OQ4 | **测试接缝缺口（AC-C2-06/22 等）**：AC-C2-22（"C2 从不写 C1 段数"）与依赖注入验证需要 C1 暴露可 mock 的 `IComboCounter`（或等价只读接口）供 CombatCore 单测隔离。当前 C1 GDD 未定义该接口契约。**归属**：lead-programmer 在 C1/C2 实装前定义只读段数接口，供 AC-C2-17/22/30/37 的单测 mock。 |
| OQ5 | **cancelWeight/authoredBpm 逐招 authoring 规范未定**：F4 提供字段，但「一把武器每个连招节点该给什么 cancelWeight / authoredBpm」属逐招手感调参，须在 C5 武器系统 + 动画产线联合确定 authoring 指南。**归属**：C5 + 动画/technical-artist，MVP 阶段先用默认（authoredBpm=120）跑通。 |
| OQ6 | **进阶取消华彩的具体表现**（AC-C2-39 ADVISORY）：Advanced 分支「大师级视听华彩」的具体 Feel 序列 / stinger 设计留给生产阶段 audio-director + Feel 实装；本 GDD 只定义事件契约与语义要求。**归属**：audio-director / VFX，生产期。 |
