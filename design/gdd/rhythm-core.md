# C1 节奏量化攻击系统 (Rhythm Quantization Attack System)

> **Status**: Revised (2026-08-10 blocker fix) — Pending Re-review
> **Author**: Jadyn Wu + Claude Code Agents
> **Last Updated**: 2026-07-21
> **Implements Pillar**: 支柱一：节奏即战斗 (Rhythm IS Combat) / 支柱二：武侠手感不可妥协 (Wuxia Feel First)
> **System ID**: C1
> **Layer**: Core | **Milestone**: MVP | **Design Order**: 6
> **Architecture Reference**: ADR-D3 (Accepted 2026-07-19) — RhythmJudger 架构

---

## Overview

C1 节奏量化攻击系统是《玉兔：破阵》核心循环的心脏——它将玩家的攻击输入（由 F3 携带精确 DSP 时间戳）吸附到 F2 音频时钟广播的半拍网格上执行，按输入与拍点的时间偏差评级为 Perfect / Good / Miss，并将评级结果以 `RhythmJudgmentEvent` 广播给下游系统。评级决定伤害倍率（1.0 / 0.75 / 0.5）、连击增长（+2 / +1 / 不变）、连招取消权限（Perfect 开放高级取消窗口，Miss 关闭）以及 Perfect 动力学变化（冻帧、敌人硬直延长、编钟共鸣音层、摄像机微切）。C1 支持三种量化模式（HalfBeat / Beat / Off），其中 Off 模式为无障碍通道，始终输出 Perfect 评级。**不攻击的拍点不产生 Miss**——C1 仅对玩家主动发出的攻击进行判定，不对沉默施加惩罚。系统架构（`RhythmJudger : MonoBehaviour`，节拍锚点 + 预缓冲单槽模式）见 ADR-D3（Accepted 2026-07-19）；本文档定义设计层行为规格与验收标准。

---

## Player Fantasy

**直接体验层（主要）：每次出刀是一个音符**

玩家按下攻击键的那一刻，他们做出了一个节奏承诺。踩准了——编钟炸响，金光溢出屏幕，敌人被硬直延长的身形印证了这一刀的重量；踩偏了——刀落有声，但世界的回响少了一拍，连招的下一扇门悄悄半阖。

成功的 C1 体验是：**玩家感觉自己在演奏，而不是在踩节奏关卡**。每一个 Perfect 不是"通关判定"，而是"这一刀和 BGM 融为一体了"——它的满足感来自感官共振，而非 UI 数字的确认。

**间接体验层（基础设施）：量化引擎对玩家透明**

C1 正常工作时，玩家不会意识到它的存在。他们只会感受到：踩准有奖励、踩偏有代价、但任何情况下都不会感到"我输入了，但游戏没有反应"。这种可信赖的判定响应是整个节奏体验的基础前提——一旦失信，玩家不会说"判定系统有 bug"，而会说"手感很差"。

**失败态（对比定义成功）：**

> "我明明踩了那一拍，为什么是 Miss？"——这句话的出现意味着 C1 的幻想已经破碎。F3 的时间戳误差、平台 DSP 粒度、延迟校准缺失——任何一个环节的失守都会让玩家对节奏系统失去信任，进而失去整个游戏的核心乐趣。

> **(lean 模式注)**: `creative-director` 未参与本节。正式 art bible 批准前建议重审情感语言的一致性。

---

## Detailed Design

### Core Rules

**规则 1：量化模式——三种攻击执行策略**

| 量化模式 | 触发条件 | 输入处理 | 评级结果 | 动画触发时机 |
|---------|---------|---------|---------|------------|
| **HalfBeat**（默认） | F2 广播每半拍节点 | 预缓冲 → `OnHalfBeatLookahead` 触发时以 beat_dsp_time 计算偏移评级；或后拍点 LateWindow 内即时评级 | Perfect / Good / Miss（按偏移量） | 拍点 DSP 时间（吸附） |
| **Beat** | F2 仍广播每半拍节点（Core Rule 3 不变）；C1 内部 skip toggle 每隔一次 `OnHalfBeatLookahead` 处理，等效全拍网格 | 同 HalfBeat，但 gridIntervalMs = fullBeatMs（间隔加倍，Perfect 更容易） | Perfect / Good / Miss | 拍点 DSP 时间（吸附） |
| **Off**（无障碍） | 无节拍依赖 | 输入到达时立即评级，偏移量强制为 0 | 始终 Perfect（但 `CancelTier` 封顶为 Standard，见 Off 合约第 5 条 + Rule 10） | 输入到达时（即时） |

量化模式由 FE7（游戏设置系统）在运行时注入，战斗中可切换。

> **Off 模式无障碍合约**（五项约束，缺一不可）：
> 1. **入口限制**：Off 模式仅可通过「无障碍设置」面板开启，不出现在「战斗设置」或快捷切换菜单中。
> 2. **持续提示**：Off 模式激活期间，HUD 全程显示「无障碍模式」标识，玩家不可关闭此指示。
> 3. **排行/成就隔离**：Off 模式下的得分、连击数不计入排行榜，不触发需要节奏精准度的成就。
> 4. **功勋折算隔离**：Off 模式下击杀敌人所得的**功勋（merit，永久货币，PlayerInfoSO）**按系数 `meritMultiplierOff` 折算（**已锁定 = 0.5**，owner 联合锁定 2026-08-11，严格开区间 0 < `meritMultiplierOff` < 1，登记 registry `merit_multiplier_off`）。理由：Off 模式恒定 Perfect（零节奏技巧）若以全额功勋计入永久进度，将形成「零技巧最优刷取」漏洞，击穿 game-concept.md:213 反支柱（不得存在绕过节奏的破坏性收益）与「节奏即战斗」支柱。**C1 不直接发放功勋**——C1 仅在 `RhythmJudgmentEvent`/激活模式中暴露当前 `QuantizationMode`，功勋折算由下游功勋结算系统（C5/FE5）读取激活模式后应用于 `EnemyInfoSO.rewardMeritBase`。此约束为 C1 的规范性要求，执行方为 C5/FE5（类比延迟钳制委托给 FE7）。
> 5. **连招深度封顶**：Off 模式下 `CancelTier` 强制为 **Standard**，**不授予 Advanced**（即便评级恒为 Perfect）。理由：game-concept.md:402 明确「高级取消窗口为大师级技巧专属，不可无条件向所有评级开放」——此约束**不可豁免**。Off 模式的 Perfect 是无障碍补偿而非技巧证明，因此评级与连招层级在此**解耦**：Off 玩家可流畅战斗（Standard 取消保证手感），但进阶连招深度（Advanced 取消）仍须通过 HalfBeat/Beat 模式下的真实节奏精准度赢取。
>
> **ADR-D3 实现注意**：ADR-D3 的 `OnAttackInput` 伪代码存在 Off 模式路径缺陷——Off 模式下 `_lateWindowActive` 始终为 false（没有拍点事件），导致 `EmitAndClear(0)` 永远不被调用。实现时须为 Off 模式添加独立分支：输入到达时立即以 `offset=0` 调用 `EmitAndClear(0)`，绕过所有拍点等待逻辑。此修正须同步更新 ADR-D3 伪代码。

---

**规则 2：预拍点缓冲（EarlyWindow）**

玩家在半拍到来前 `EarlyWindowMs`（默认 120ms）内发出的攻击输入，存入单槽缓冲区等待拍点到来后评级。

- 缓冲区存储输入时刻的精确 DSP 时间戳（来自 F3）。
- `OnHalfBeatLookahead` 事件触发时（早于实际拍点 `LookaheadMs`），C1 扫描缓冲区，以事件携带的 `beat_dsp_time`（即将到来的拍点时间）计算偏移量并进行评级。
- 偏移量 = `(输入 DSP 时间 + 延迟校准偏移 − 拍点 DSP 时间) × 1000` (ms)

---

**规则 3：后拍点宽容窗口（LateWindow）**

拍点过后 `LateWindowMs`（默认 100ms）内到达的攻击输入，被立即评级（不等下一拍）：

- 后到达输入的偏移量为正值（晚于拍点）。
- LateWindow 过期后到达的输入忽略，不产生判定，不产生 Miss。

---

**规则 4：单槽缓冲区——后到优先（last-in-wins）策略（设计决定，非 Bug）**

同一半拍窗口内快速双击，单槽采用**后到优先**策略：第二次输入覆盖第一次（若第一次仍未消费），拍点到来后只评级缓冲区中最后一次输入。

> **设计意图**：节奏游戏的核心是精准的单次承诺，而非短时间内的连续敲击。双击行为不是节奏精度的体现。单槽 last-in-wins 策略确保最终落在缓冲区的输入（玩家的最后一次承诺）被评级，只广播一次 `RhythmJudgmentEvent`。

---

**规则 5：不攻击的拍点不产生 Miss**

玩家选择在某个拍点不攻击，C1 不产生任何 `RhythmJudgmentEvent`，也不产生任何惩罚。Miss 仅发生在玩家**发出了攻击**但偏差超出 Good 窗口时。

---

**规则 6：按下时刻（chargeStartDspTime）作为判定锚点——只要 chargeStartDspTime 有效**

只要 `chargeStartDspTime > -1.0`（F3 填充了按下时刻），C1 使用 `chargeStartDspTime` 作为节奏判定基准，无论 `attackType` 是 ChargedAttack（正常充能）还是 LightAttack（E3a 退化场景）。

- 理由：玩家的节奏承诺发生在按下瞬间。充能是否完成是技巧/意志层面的决策，不应影响节奏判定的公正性。
- E3a 退化场景：充能在完成前松开，F3 将 `attackType` 降级为 LightAttack，但 `chargeStartDspTime` 仍保留按下时刻——C1 检测到 `chargeStartDspTime > -1.0`，依然使用 `chargeStartDspTime` 判定（见边界情况 E3a）。
- 普通轻攻击：`chargeStartDspTime == -1.0`（F3 未填充），C1 使用 `inputDspTime`。

---

**规则 7：时钟就绪检查**

C1 在 `BeatClock.IsStartConfirmed == true` 之前不对任何输入进行节奏判定。首拍到来前到达的输入存入缓冲区，待确认后随第一个拍点事件统一处理。

> **Off 模式豁免（无障碍合约）**：Off 模式完全不受 `IsStartConfirmed` 约束。Off 模式下 `OnAttackInput` 到达时，无论时钟是否就绪，立即以 `offset=0` 调用 `EmitAndClear(0)`，不进入缓冲区等待。此豁免确保无障碍用户在 BGM 首拍前的攻击也能立即获得 Perfect 响应，与"无节拍依赖"合约保持一致。

---

**规则 8：暂停立即清除缓冲**

游戏暂停时，C1 立即清除单槽缓冲区并关闭 LateWindow。恢复后不产生暂停前的残留判定（幽灵判定）。

- **暂停时**：`_pendingInput = null`；`_lateWindowActive = false`；停止处理 `OnHalfBeatLookahead`。C1 订阅 F2 的 `OnBeatClockPaused` 事件（F2 权威定义，BC-6），收到后进入静默态。
- **恢复时（BC-6）**：C1 订阅 F2 的规范化事件 **`OnBeatClockResumed`**（**非** `OnBpmChanged`；事件名对齐 F2 权威定义），收到后：① **重锚拍网格**——以事件携带的恢复后第一个拍点 `beat_dsp_time` 为基准重新对齐半拍网格；② **清空过期 `_pendingInput`**——丢弃暂停前残留的任何 pending 输入（防幽灵判定，即使暂停时已清空亦幂等再清一次作为护栏）；③ 以"等待输入"状态重新激活。不重算有效窗口（BPM 未变化），不重置连击数。
- `OnBeatClockResumed` 携带恢复后第一个拍点的 `beat_dsp_time`，C1 用此时间作为重锚网格与下一个 EarlyWindow 的起始参考，确保恢复后首次输入有正确的时间锚点。

---

**规则 9：评级后果合约（只读，与 game-concept.md 保持一致）**

| 评级 | 偏移量 | 伤害倍率 | 连击变化 | 高级取消窗口 | Perfect 动力学 |
|------|--------|---------|---------|------------|--------------|
| **Perfect** | ≤ PerfectHalfWindowMs | 1.0× | +2 | **开放** | ✅ 全触发 |
| **Good** | ≤ GoodHalfWindowMs | 0.75× | +1 | 标准取消窗口 | ✗ |
| **Miss** | > GoodHalfWindowMs | 0.5× | 不变（不归零） | **关闭**高级取消 | ✗ |

> Miss 时连击数停止增长但不清零。此行为不可通过心法覆盖（game-concept.md 规则五合约）。

---

**规则 10：CancelTier — 取消权限分级**

`RhythmJudgmentEvent` 携带 `CancelTier` 字段，由 C1 根据评级自动填充，供 C2 连招图决定允许哪些连招分支转换：

| CancelTier | 对应评级 | C2 权限 |
|-----------|---------|--------|
| **Advanced** | Perfect（**仅 HalfBeat / Beat 模式**） | 开放高级取消窗口（可取消任意招式进入高级连招节点） |
| **Standard** | Good；**或 Off 模式的 Perfect**（见下方 Off 覆盖规则） | 仅允许标准取消（高级连招节点不可达） |
| **Closed** | Miss | 关闭所有额外取消窗口（连招图锁定，只有正常动画结束后才能继续） |

> **设计意图**：CancelTier 将节奏精准度直接映射到战斗可能性。Perfect 解锁最强连招路径，Miss 关闭捷径，形成"踩准节奏 → 获得战术优势"的正反馈循环。CancelTier 是对 C2 暴露的接口合约，不是 UI 概念——P1 HUD 无需直接展示此字段（由 Rating 的视觉反馈隐式传达）。
>
> **Off 模式覆盖（不可豁免）**：Off 模式下评级恒为 Perfect，但 `CancelTier` **强制封顶为 Standard**，不授予 Advanced（见 Off 合约第 5 条）。依据 game-concept.md:402——高级取消窗口为大师级技巧专属，不可无条件向所有评级开放。因此 C1 填充逻辑须在 Off 模式下将 Perfect→Advanced 的默认映射**覆盖**为 Perfect→Standard；评级字段仍为 Perfect（无障碍反馈不变），仅连招层级解耦下调。
>
> **ADR-D3 更新需求**：`RhythmJudgmentEvent` struct 须新增 `CancelTier CancelTier` 字段（`CancelTier` enum: Advanced / Standard / Closed）。C1 在 `EmitAndClear` 时按评级填充：Perfect → Advanced，Good → Standard，Miss → Closed；**例外**：当 `QuantizationMode == Off` 时，Perfect → Standard（Off 覆盖规则）。

---

### States and Transitions

| 状态 | 描述 |
|------|------|
| **静默** | 时钟未启动（`IsStartConfirmed == false`）或游戏暂停；缓冲区为空；不产生任何判定 |
| **等待输入** | 时钟运行；缓冲区为空；LateWindow 关闭 |
| **预拍点缓冲** | 缓冲区持有输入；等待下一半拍到来 |
| **后拍点窗口** | 拍点已过；LateWindow 开启；新到输入立即评级 |

| 来源状态 | 触发条件 | 目标状态 |
|---------|---------|---------|
| **静默** | `IsStartConfirmed` 变为 true | **等待输入** |
| **等待输入** | 攻击输入到达（在 EarlyWindow 内） | **预拍点缓冲** |
| **等待输入** | 半拍到来（缓冲区无输入） | **后拍点窗口** |
| **预拍点缓冲** | 半拍到来，偏移量在窗口内 | **等待输入**（评级后清空） |
| **预拍点缓冲** | 半拍到来，偏移量超出 EarlyWindow（针对下一拍） | **后拍点窗口**（保留缓冲） |
| **后拍点窗口** | 攻击输入到达（在 LateWindow 内） | **等待输入**（评级后清空） |
| **后拍点窗口** | LateWindow 过期 | **等待输入**（丢弃缓冲） |
| 任意状态 | 游戏暂停 | **静默**（清空缓冲） |
| **静默** | 游戏恢复（F2 `OnBeatClockResumed`，BC-6） | **等待输入**（清空过期 pending + 重锚拍网格） |
| 任意状态 | 玩家死亡（F6 `PlayerDeath`，SC-2） | **静默**（清空缓冲） |
| **静默** | 玩家复活完成（F6 `RespawnComplete`，SC-2） | **等待输入**（清空过期评级状态 + 重锚拍网格） |

---

### Interactions with Other Systems

| 方向 | 系统 | 数据内容 | 接口类型 |
|------|------|---------|---------|
| F2 **→** C1 | 音频时钟 | `OnHalfBeatLookahead`（`BeatEventData.beat_dsp_time`）；`OnMeterChanged`（`MeterChangeEventData`，边界生效时重锚定网格）；`BeatClock.IsStartConfirmed` | 事件订阅 + 属性查询 |
| F3 **→** C1 | 输入管线 | `AttackInputEvent`（attackType, inputDspTime, chargeStartDspTime） | 事件订阅（`OnAttackInput`） |
| C1 **→** C2 | 连招图系统 | `RhythmJudgmentEvent`（Rating, AttackType, CancelTier, ComboCountBefore, ComboCountAfter） | 事件广播（C2 订阅） |
| C1 **→** P1 | HUD | `RhythmJudgmentEvent`（Rating, OffsetMs, ComboCountBefore 用于连击显示） | 事件广播 |
| C1 **→** PL2 | VFX 节奏反馈 | `RhythmJudgmentEvent`（Rating=Perfect 触发冻帧/编钟/摄像机微切序列） | 事件广播 |
| C3 **↔** C1 | 心法构筑系统 | C1 消费 C3 实现的 `IRhythmModifierProvider`（窗口 delta / ΔCombo delta，见 C1-F7）；C3 订阅 C1 `OnRhythmJudgment` 作触发器 | 属性注入（C1 查询）+ 事件订阅（C3 侧）；**BC-2** |
| C1 **→** F6/Animancer | 攻击动画 | `AnimancerAttackPlayer.Play(attackType, beatDspTime)` | 方法调用（ADR-D5） |
| FE7 **→** C1 | 游戏设置 | `QuantizationMode`（Off/HalfBeat/Beat；运行时可切换） | 属性注入 |
| FE3/F2 **→** C1 | 延迟校准 | `latency_offset_s`（由 F2 内化后，C1 从 F2 读取；用于跨时间域对齐） | 同步查询（`BeatClock.LatencyOffsetMs`） |

> **M3 设计决策（来自 ADR-D3）**：C1 须决定 `IBeatClock` 和 `IInputEventBus` 是否正式声明为接口（以支持纯 C# 单元测试）还是直接引用单例。推荐方案 A（接口注入）。此决定须在实现 sprint 开始前落地，见 Open Questions。

---

## Formulas

### C1-F1：判定时间锚点选择

**用途**：确定哪个 DSP 时间戳作为节奏判定的基准时刻。

```
if chargeStartDspTime > -1.0:
    judgment_time = chargeStartDspTime   // 玩家按下瞬间（意图时刻）——含正常充能（ChargedAttack）及 E3a 退化场景（LightAttack）
else:
    judgment_time = inputDspTime          // 普通轻攻击（chargeStartDspTime == -1.0，F3 未填充）
```

**变量定义：**

| 变量 | 来源 | 类型 | 说明 |
|------|------|------|------|
| `attackType` | F3 `AttackInputEvent` | enum | LightAttack / **HeavyAttack** / ChargedAttack |
| `chargeStartDspTime` | F3 `AttackInputEvent` | double (s) | 充能按键时刻；哨兵值 -1.0 表示非充能 |
| `inputDspTime` | F3 `AttackInputEvent` | double (s) | 输入最终到达时刻 |
| `judgment_time` | C1 内部 | double (s) | 传入 C1-F2 的锚点时间 |

**哨兵值规则**：`chargeStartDspTime == -1.0` 表示 F3 未填充该字段（仅普通轻攻击场景）。充能中途松开退化（E3a）时，F3 仍填充 `chargeStartDspTime`（按下时刻），故 E3a 场景 `chargeStartDspTime > -1.0`。检测条件为 `> -1.0` 而非 `!= -1.0`，以防浮点精度问题。

---

### C1-F2：节拍偏移量计算

**用途**：将判定锚点时间与最近半拍拍点对齐，得出偏差毫秒数。

```
beat_offset_ms = (judgment_time + latency_offset_s − beat_dsp_time) × 1000
```

**变量定义：**

| 变量 | 来源 | 单位 | 范围 | 说明 |
|------|------|------|------|------|
| `judgment_time` | C1-F1 输出 | s（double） | — | 判定锚点时间 |
| `latency_offset_s` | F2 `BeatClock.LatencyOffsetMs / 1000` | s（float） | [-0.2, 0.2] | 玩家校准的延迟补偿量；注册于实体注册表 `latency_calibration_offset_ms` |
| `beat_dsp_time` | F2 `BeatEventData.beat_dsp_time` | s（double） | — | 已在"玩家感知时域"的半拍拍点时间（F2 Core Rule 7） |
| `beat_offset_ms` | C1 内部 | ms（float） | (-∞, +∞) | 负值=提前拍，正值=晚于拍 |

**时域说明**：`beat_dsp_time` 已由 F2 加入延迟补偿，处于"玩家感知时域"。`judgment_time` 来自 F3 原始 DSP 时间戳，须加 `latency_offset_s` 转换到同一时域后再计算差值。

**示例 1（正延迟校准，BPM=120）：**
- `halfBeatMs = 30/120 × 1000 = 250 ms`
- 玩家设置 +30ms 延迟校准（音频延迟比输入慢 30ms）：`latency_offset_s = 0.030`
- 实际输入提前 15ms 到达：`judgment_time = beat_dsp_time - 0.015 - 0.030`
- `beat_offset_ms = (beat_dsp_time - 0.015 - 0.030 + 0.030 - beat_dsp_time) × 1000 = -15ms`
- `|beat_offset_ms| = 15ms ≤ PerfectHalfWindowMs(50ms)` → **Perfect**

**示例 2（负延迟校准——显示/音频反相，BPM=120）：**
- 玩家设置 -50ms 校准（罕见场景：玩家使用蓝牙音频但视觉比音频快）：`latency_offset_s = -0.050`
- 玩家晚于拍点 20ms 输入：`judgment_time = beat_dsp_time + 0.020`
- `beat_offset_ms = (beat_dsp_time + 0.020 + (-0.050) - beat_dsp_time) × 1000 = -30ms`
- `|beat_offset_ms| = 30ms ≤ PerfectHalfWindowMs(50ms)` → **Perfect**（负偏移量补偿有效）

> **符号约定总结**：`latency_offset_s > 0` 补偿输入系统延迟（常见）；`< 0` 补偿显示超前（罕见）。两种情形公式形式不变，C1 不区分处理。

> **延迟安全约束**：`|latency_offset_ms|`（即 `|latency_offset_s × 1000|`）必须满足 `< GoodHalfWindowMs_effective`（默认 120ms）；超出此范围时，即使玩家完美踩拍（`judgment_time ≈ beat_dsp_time`），`beat_offset_ms` 的绝对值也将超过 Good 窗口，始终产生 Miss，直接破坏"踩准有奖励"的核心合约。**约束执行由 FE7 校准界面负责**：当玩家输入的延迟值超过此上限时，FE7 须显示警告并拒绝或钳位输入，禁止超范围值写入 `BeatClock.LatencyOffsetMs`。C1 信任从 F2 读取的 `LatencyOffsetMs` 已由 FE7 验证，不在 C1 层重复钳位。

---

### C1-F3：评级评估（含运行时钳位）

**用途**：将偏移量映射到 Perfect / Good / Miss 评级，并处理 BPM 引发的窗口溢出问题。

**步骤 1 — 运行时钳位（每次 BPM 变化时重算）：**

```
halfBeatMs = (30.0 / currentBpm) × 1000          // 单位 ms
fullBeatMs = halfBeatMs × 2                       // 全拍间距（Beat 模式用）

// 当前量化模式的网格间距：HalfBeat → halfBeatMs；Beat → fullBeatMs（间隔加倍）
gridIntervalMs = (quantizationMode == Beat) ? fullBeatMs : halfBeatMs

// 钳位 Good 窗口，确保不超过网格区间的一半（防止相邻拍点 Good 窗口重叠）
GoodHalfWindowMs_effective  = min(GoodHalfWindowMs_authored,  gridIntervalMs / 2 - 1)

// 钳位 Perfect 窗口，确保不超过有效 Good 窗口
PerfectHalfWindowMs_effective = min(PerfectHalfWindowMs_authored, GoodHalfWindowMs_effective)

// 平台最小 Perfect 窗口（确保玩家在目标平台上可物理打出 Perfect）
PerfectHalfWindowMs_effective = max(PerfectHalfWindowMs_effective, platformMinPerfectMs)

// 步骤 1b — EarlyWindow 钳位（与 GoodHalfWindowMs_effective 对齐）
// 确保 EarlyWindow 内接受的输入不会因 Good 窗口被 BPM 压缩后变成 Miss
EarlyWindowMs_effective = min(EarlyWindowMs_authored, GoodHalfWindowMs_effective)
```

**窗口不等式不变量**（经 E7 坍缩修复后始终成立）：
```
PerfectHalfWindowMs_effective ≤ GoodHalfWindowMs_effective ≤ gridIntervalMs/2 - 1
EarlyWindowMs_effective       ≤ GoodHalfWindowMs_effective
```
违反此不变量意味着实现 Bug——可由 `RhythmJudger` 在 `OnMeterChanged` 末尾 `Assert` 检验。

**平台最小 Perfect 窗口：**

| 平台 | `platformMinPerfectMs` | 依据 |
|------|----------------------|------|
| PC | 16.7 ms | 60FPS 帧时间 |
| iOS | 39 ms | 约 2 帧 @ 60fps + Core Audio DSP 粒度 |
| Android | 80 ms | 约 2 帧 @ 60fps + 平台 DSP 粒度误差（厂商差异大） |

**步骤 2 — 评级判定：**

```
absOffset = abs(beat_offset_ms)

if absOffset ≤ PerfectHalfWindowMs_effective:  rating = Perfect
elif absOffset ≤ GoodHalfWindowMs_effective:   rating = Good
else:                                            rating = Miss
```

**运行时钳位意图**：当 `GoodHalfWindowMs_authored`（策划在 SO 中配置的值）因高 BPM 超出网格区间一半时（`gridIntervalMs/2`），若不钳位则相邻拍点的 Good 窗口重叠，导致同一输入被两个拍点同时吸附。运行时钳位写入 `_effectiveGoodMs` / `_effectivePerfectMs` 运行时字段，不回写 SO（遵守 ADR-D4 D4-1 原则）。

**临界 BPM**：当 `GoodHalfWindowMs_authored = 120ms` 时（HalfBeat 模式，`gridIntervalMs = halfBeatMs`），钳位触发条件为 `halfBeatMs/2 - 1 < 120`，即 `halfBeatMs < 242ms`，对应 BPM > 123.97——**HalfBeat 模式 BPM ≥ 124 时钳位生效**。Beat 模式（`gridIntervalMs = fullBeatMs`）等效临界 BPM ≈ 62，但因网格间距更大，钳位后有效窗口也更宽。

> **Android 高 BPM 冲突**：当 `platformMinPerfectMs(80ms) > GoodHalfWindowMs_effective` 时，Perfect 窗口与 Good 窗口坍缩为同值（见 Edge Cases E7）。

---

### C1-F4：连击计数更新

**用途**：按评级更新连击计数器，向 C2 和 P1 广播最新连击数。

```
baseDelta = ΔCombo_table[rating]
perkDelta = RhythmModifierProvider?.GetComboDelta(rating) ?? 0   // C3 ComboModifier 聚合量（BC-2）；null=无 C3/无心法
ΔCombo    = (rating == Miss) ? baseDelta : baseDelta + perkDelta  // Miss 恒 = baseDelta(=0)，心法不可改写（护栏，见下）
CurrentCombo_new = clamp(CurrentCombo + ΔCombo, 0, ComboMax)
```

**ΔCombo 映射：**

| 评级 | ΔCombo |
|------|--------|
| Perfect | +2 |
| Good | +1 |
| Miss | +0（连击停止增长，不清零） |

**变量定义：**

| 变量 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `CurrentCombo` | int | 0 | 当前连击数（由 C1 持有并广播） |
| `ΔCombo` | int | 见表 | 本次评级对应的连击增量 |
| `ComboMax` | int | **999**（对齐 registry `combo_max`；Q1 以 registry 权威值为准） | 连击上限；写入 RhythmConfigSO。注：`combo_multiplier` 曲线于 count=148 触顶 ×5.0（C2），148→999 为平台段 |
| `CurrentCombo_new` | int | — | 更新后写入 `RhythmJudgmentEvent.ComboCountAfter` |

**Miss 不清零依据**：game-concept.md 规则五合约；此行为不可通过心法（Perk）覆盖。

---

### C1-F5：BPM 变化时的运行时重验证

**用途**：Boss 阶段转换或关卡切换导致 BPM 变化时，C1 须立即重算有效窗口并清理悬挂状态。

```
void OnMeterChanged(MeterChangeEventData data):    // 订阅 F2 的 OnMeterChanged（边界生效事件，见 F2 Core Rule 14）
    float newBpm = data.newBpm                     // MeterChangeEventData.newBpm；拍号-only 变更时 == oldBpm（重算幂等，安全）

    // E8 守卫：无效 BPM（0 / 负值）回退——保持当前 BPM，不重算窗口、不清空缓冲、记录 Warning 后返回
    if newBpm <= 0:
        return

    currentBpm = newBpm
    
    // 1. 重算有效窗口（见 C1-F3）
    halfBeatMs              = (30.0 / newBpm) × 1000
    fullBeatMs              = halfBeatMs × 2
    gridIntervalMs          = (_quantizationMode == Beat) ? fullBeatMs : halfBeatMs
    
    // perk 窗口修正（BC-2）：查询 C3 聚合 window delta，叠加到 authored 值后再进入 clamp 级联；null=无 C3/无心法→delta 0
    perkPerfectDeltaMs      = RhythmModifierProvider?.GetPerfectWindowDeltaMs() ?? 0
    perkGoodDeltaMs         = RhythmModifierProvider?.GetGoodWindowDeltaMs()    ?? 0
    // W-05 级联位点：weapon.perfectWindowModifier（F4 字段）应在此处、perk delta 之后 clamp 之前作为乘数接线，
    //             随 F4/C1 同轮消费（当前 C1 尚无 weapon 上下文来源，暂不接线，见评审 W-05）
    _effectiveGoodMs        = min(GoodHalfWindowMs_authored    + perkGoodDeltaMs,    gridIntervalMs / 2 - 1)
    _effectivePerfectMs     = min(PerfectHalfWindowMs_authored + perkPerfectDeltaMs, _effectiveGoodMs)
    _effectivePerfectMs     = max(_effectivePerfectMs, platformMinPerfectMs)
    
    // E7 内联坍缩——须在 Assert 之前执行（Android BPM ≥ 186 时 platformMinPerfectMs 超过压缩后的
    // _effectiveGoodMs，若不先坍缩则下方 Assert(_effectivePerfectMs <= _effectiveGoodMs) 误报 false）
    if _effectivePerfectMs > _effectiveGoodMs:
        _effectiveGoodMs = _effectivePerfectMs           // 将 Good 提升至 Perfect 平台下限
        // 二次钳位：防止坍缩后窗口超出网格区间，引发相邻拍混叠（见 E7 步骤 2）
        if _effectiveGoodMs > gridIntervalMs / 2 - 1:
            _effectiveGoodMs    = gridIntervalMs / 2 - 1
            _effectivePerfectMs = _effectiveGoodMs
            // 此状态下平台 DSP 精度无法保证 Perfect 可达——记录 Warning 日志
    
    _effectiveEarlyMs   = min(EarlyWindowMs_authored,    _effectiveGoodMs)
    
    // 约束断言——条件编译包裹（Unity 6.3 Release Build 中 Debug.Assert 通过 UNITY_ASSERTIONS 缺失被剥离；
    // #if 包裹是防御性冗余，确保 Development Build 调试期强制执行不变量检查）
    #if UNITY_EDITOR || DEVELOPMENT_BUILD
    Debug.Assert(newBpm >= 80 && newBpm <= 240, "BPM out of registered range [80, 240]")
    Debug.Assert(_effectivePerfectMs <= _effectiveGoodMs)   // E7 坍缩后此不变量始终成立
    Debug.Assert(_effectiveEarlyMs   <= _effectiveGoodMs)
    #endif
    
    // 2. 清理悬挂缓冲状态（BPM 变化后，待评级输入对应旧拍网格，丢弃）
    _pendingInput = null
    _lateWindowActive = false
    _skipToggle = false                    // Beat 模式：重置为"处理"相位，确保 BPM 变化后首个 OnHalfBeatLookahead 始终触发判定窗口开放
```

**调用时机**：订阅 F2 的 `OnMeterChanged`（拍号/BPM 变更**生效**事件，F2 Core Rule 14）。F2 在小节边界翻转内部状态后同帧广播，顺序上位于边界拍 `OnBeatExact` 之后、新段第 0 拍调度之前，早于下一个 `OnHalfBeatLookahead` 事件。

> **BC-2 追加触发**：C1 另订阅 `OnPerkStackChanged`（心法栈增删时由 C3/心法管理器广播）——此时仅执行**步骤 1**（窗口重算，重新查询 `RhythmModifierProvider` 纳入最新 perk delta 并缓存），**不执行步骤 2**（缓冲清空）：选/弃心法发生在房间之间，无进行中攻击输入需丢弃。缓存策略确保判定热路径（`OnHalfBeatLookahead`）零 provider 查询开销——perk delta 只在 meter/perk 变更这两个低频事件时重算一次（BC-2 契约见 C1-F7）。

> **为何订阅 `OnMeterChanged` 而非 `OnMeterChangeAccepted`**：F2 暴露两个事件——`OnMeterChangeAccepted`（接受时广播，供 C2 立即 `UpdateBpm()` 体感、F6 预调度 BGM）与 `OnMeterChanged`（小节边界生效时广播，供 C1 重锚定量化网格、C4 调整令牌密度）。C1 的判定网格必须与**实际生效**的拍点对齐，故只订阅 `OnMeterChanged`；不订阅 `OnMeterChangeAccepted`（若在接受时提前重锚，会导致边界前的输入按未来网格判定）。`OnMeterChanged` 对拍号-only 变更（`bpmChanged == false`，`newBpm == oldBpm`）同样触发——此时重算窗口幂等无害，缓冲清空仍适用（小节边界已跨越）。

> **BD-2 适应窗口叠加**：上述步骤 1 算出的 `_effective*Ms` 为**新 BPM 标准宽度**。当 `bpmChanged == true` 且 BPM 上升时，标准宽度会骤缩，恰在 Boss 转阶段最高压时刻放大判定难度（Flow 反模式）。为此 C1 在 `OnMeterChanged` 生效后进入 **适应窗口**（`AdaptationWindowBeats` 拍，默认 2），窗口内以「变更前后较宽者」覆盖标准宽度——详见新增公式 **C1-F5b**。步骤 2（缓冲清空）不受适应窗口影响，仍在 `OnMeterChanged` 当帧立即执行。

---

### C1-F5b：BD-2 BPM 跃升适应窗口宽容期

**用途（BD-2）**：`OnMeterChanged` 生效导致 BPM 上升时，判定窗毫秒宽度会骤缩，与 C2 取消窗缩短、C4 令牌密度上升在同一事件叠加为三系统难度尖峰。C1 侧承接策略为：`OnMeterChanged` 生效后引入 **适应窗口 = `AdaptationWindowBeats` 拍**（默认 2），窗口内判定窗**毫秒宽度取「变更前窗口ms 与 变更后窗口ms 的较宽者」**（`max`），防止 BPM 上升时窗口骤缩；窗口结束后恢复新 BPM 标准宽度。

```
// 在 C1-F5 步骤 1 算出新 BPM 标准宽度 _effective*Ms 之后执行（仅 bpmChanged==true 时启用）
// prev*Ms = 本次 OnMeterChanged 之前生效的（旧 BPM）窗口毫秒宽度，重算前先快照
onMeterChanged(data):
    snapshot prevPerfectMs = _effectivePerfectMs, prevGoodMs = _effectiveGoodMs, prevEarlyMs = _effectiveEarlyMs
    // …（C1-F5 步骤 1：按 newBpm 重算 _effective*Ms 标准宽度）…
    if data.bpmChanged:
        _adaptationEndDspTime = data.predictedEffectiveDspTimeRaw + AdaptationWindowBeats × fullBeatMs / 1000
        // 缓存 prev 与 standard 两组宽度，供窗口内/外取用
        _adaptPerfectMs = max(prevPerfectMs, _effectivePerfectMs)   // 较宽者
        _adaptGoodMs    = max(prevGoodMs,    _effectiveGoodMs)
        _adaptEarlyMs   = max(prevEarlyMs,   _effectiveEarlyMs)

// 判定热路径（C1-F3 步骤 2 取窗口宽度时）——按当前 DSP 时间是否仍在适应窗口内选择宽度：
effectivePerfect = (nowDspTime < _adaptationEndDspTime) ? _adaptPerfectMs : _effectivePerfectMs
effectiveGood    = (nowDspTime < _adaptationEndDspTime) ? _adaptGoodMs    : _effectiveGoodMs
```

**变量定义：**

| 变量 | 来源 | 单位 | 说明 |
|------|------|------|------|
| `AdaptationWindowBeats` | `RhythmConfigSO`（Tuning Knob） | 拍（beat） | 适应窗口时长；默认 **2**，范围 **[0, 4]**；0 = 关闭宽容期（立即恢复标准宽度） |
| `prevPerfectMs` / `prevGoodMs` / `prevEarlyMs` | 重算前快照 | ms | 变更前（旧 BPM）生效的窗口宽度 |
| `_effective*Ms` | C1-F5 步骤 1 | ms | 变更后（新 BPM）标准宽度 |
| `_adapt*Ms` | 本公式 | ms | 窗口内实际采用宽度 = `max(prev, effective)`（较宽者） |
| `_adaptationEndDspTime` | 本公式 | s（DSP） | 适应窗口结束的 DSP 时间 = 边界拍时间 + `AdaptationWindowBeats × fullBeatMs/1000` |
| `predictedEffectiveDspTimeRaw` | F2 `MeterChangeEventData`（权威字段） | s（DSP，raw） | 边界生效时刻的 raw DSP 时间戳，作适应窗口计时基准（BC-6 对齐：C1 不自造 `boundaryBeatDspTime`，直接消费 F2 既有字段；窗口时长比较用 raw 坐标无需 offset） |

**示例（BPM 120 → 180 上升，BD-2 生效，HalfBeat 模式，`GoodHalfWindowMs_authored=120`, `PerfectHalfWindowMs_authored=50`）：**
- 变更前 BPM=120：`halfBeatMs=250`，`gridIntervalMs/2-1=124`；`prevGoodMs = min(120,124)=120ms`，`prevPerfectMs=50ms`。
- 变更后 BPM=180：`halfBeatMs=166.7`，`gridIntervalMs/2-1=82.3`；标准 `_effectiveGoodMs = min(120,82.3)=82.3ms`，`_effectivePerfectMs=min(50,82.3)=50ms`。
- 适应窗口内（生效边界拍起 2 拍 = `2 × fullBeatMs = 2 × 333.3 = 666.7ms`）：`_adaptGoodMs = max(120, 82.3) = 120ms`（沿用较宽的旧窗口，Good 不骤缩）；`_adaptPerfectMs = max(50, 50) = 50ms`。
- 窗口结束后（`nowDspTime ≥ _adaptationEndDspTime`）：恢复新 BPM 标准宽度 `82.3ms / 50ms`。

> **BD-2 三系统互引**：本适应窗口为 **C1** 侧承接。**C2**（`combo-system.md`）取消窗在同一适应窗口内以相同 `max` 规则放宽绝对毫秒（BD-2）；**C4**（`combat-director.md`）`SlotCapacity` 改按「每秒令牌速率」归一化以消除 BPM 上升导致的攻击密度线性上升（BD-2）。三文档 Formulas 互相引用 BD-2；`AdaptationWindowBeats` 由 C1/C2 共享同一 Tuning Knob 语义，**已由 C1/C2/C4 三 owner 联合锁定 = 2 拍**（2026-08-11，登记 registry `adaptation_window_beats`，单一 owner=`RhythmConfigSO.AdaptationWindowBeats`）。

> **拍号-only 变更（`bpmChanged==false`）不启用适应窗口**：BPM 未变则标准宽度不变，无骤缩风险，`_adaptationEndDspTime` 保持过去值（判定热路径直接走标准宽度）。

---

### C1-F5c：Off 模式 perk_multiplier 独立封顶（BD-1）

**用途（BD-1）**：当 `QuantizationMode == Off` 时，Off 恒定 Perfect（`rating=1.0`）× 连击正常增长 × perk_multiplier 无限制 = 战力天花板与踩准玩家几乎相同，已从「功勋漏洞」升级为**战力漏洞**，击穿 game-concept.md:213 反支柱与支柱一。承接策略：Off 分支对**聚合 `perk_multiplier` 独立封顶 1.0**（「节奏模式独占」护栏）——即 Off 下心法产生的 DamageModifier / ComboModifier 聚合后 clamp 到 ≤ 1.0，perk 在 Off 下不产生正向战力增益。

```
// 最终伤害计算中 perk_multiplier 的 Off 分支 clamp（C1 定义规范性护栏；聚合由 C3 提供、乘入由下游伤害系统执行）
perk_multiplier_raw = RhythmModifierProvider?.GetDamageMultiplier() ?? 1.0   // C3 聚合的 DamageModifier/ComboModifier 乘积
if quantizationMode == Off:
    perk_multiplier = min(perk_multiplier_raw, 1.0)      // BD-1 独立封顶：节奏模式独占护栏
else:
    perk_multiplier = perk_multiplier_raw                // HalfBeat/Beat：正常，受 C3 PERK_MULT_MAX(2.0) 约束
// 最终伤害（示意，乘入方为下游伤害系统）：
final_damage = base_damage × rating_multiplier × combo_multiplier × perk_multiplier
```

**变量定义：**

| 变量 | 来源 | 范围 | 说明 |
|------|------|------|------|
| `perk_multiplier_raw` | C3 `IRhythmModifierProvider.GetDamageMultiplier()` | [1.0, 2.0]（PERK_MULT_MAX=2.0，C3） | 心法聚合伤害乘子；null（无心法）= 1.0 |
| `quantizationMode` | FE7 注入 | {Off, HalfBeat, Beat} | 判定 Off 分支 |
| `perk_multiplier` | 本公式 | Off: [?, 1.0]；否则 [1.0, 2.0] | Off 下 clamp 到 ≤1.0 后传下游 |

**边界语义（OQ-C3-3 锁定）**：Off 下心法的 DamageModifier / ComboModifier 聚合后 clamp 到 ≤ 1.0，**且计入功勋时同样受 Off 折算**（`meritMultiplierOff`，见 Off 合约第 4 条）——不额外绕过。此护栏与既有 Off 三重隔离（排行/成就隔离、连招深度封顶 Standard、功勋折算）叠加，构成第四重隔离，共同确保 Off 无法成为绕过节奏的破坏性收益路径。`meritMultiplierOff` 沿用 C1 既有 Off 功勋折算三重隔离，**不新增绕过**。

> **C1 不乘入 perk_multiplier**：与 C1-F7 一致，C1 仅**定义** Off 封顶护栏规范；`GetDamageMultiplier()` 的实际乘入由下游伤害系统（C2/C6）执行，须读取激活 `QuantizationMode` 并在 Off 下应用 `min(·, 1.0)`。C3（`perk-system.md`）AC-C3-15(b) 据此从「阻塞桩」改为可判定：断言 Off 下 `perk_multiplier ≤ 1.0`（BD-1 三文档对齐）。

---

### C1-F6：RhythmJudgmentEvent 规范性字段定义

**用途**：`RhythmJudgmentEvent` 是 C1 向所有下游系统（C2、P1、PL2）广播的核心事件载体。**本节为唯一规范性定义**；其他文档（ADR-D3、Rule 10、Interactions 表）以本节字段列表为准。

```csharp
// C# struct（伪代码；实际 namespace 由 ADR-D3 §4 规定）
public readonly struct RhythmJudgmentEvent
{
    public RhythmRating  Rating;           // Perfect / Good / Miss
    public AttackType    AttackType;       // LightAttack / HeavyAttack / ChargedAttack（透传自 F3）
    public CancelTier    CancelTier;       // Advanced / Standard / Closed（C1 按评级填充，见 Rule 10）
    public int           ComboCountBefore; // 本次判定之前的连击数
    public int           ComboCountAfter;  // 本次判定之后的连击数（= Before + ΔCombo，见 C1-F4）
    public float         OffsetMs;         // beat_offset_ms（ms，float）；Off 模式固定 = 0
}
```

**字段说明：**

| 字段 | 类型 | 单位 | 取值范围 | 备注 |
|------|------|------|---------|------|
| `Rating` | `RhythmRating` enum | — | Perfect / Good / Miss | 由 C1-F3 步骤 2 产生 |
| `AttackType` | `AttackType` enum | — | LightAttack / HeavyAttack / ChargedAttack | 直通 F3 `AttackInputEvent.attackType`；Off 模式同样透传 |
| `CancelTier` | `CancelTier` enum | — | Advanced / Standard / Closed | Perfect→Advanced, Good→Standard, Miss→Closed（Rule 10） |
| `ComboCountBefore` | int | — | [0, ComboMax] | 判定前连击数，供 P1 显示连击增量动画 |
| `ComboCountAfter` | int | — | [0, ComboMax] | 判定后连击数 = clamp(Before + ΔCombo, 0, ComboMax)（C1-F4） |
| `OffsetMs` | float | ms | (−∞, +∞)；Off=0 | 负值=提前拍，正值=晚于拍；Off 模式强制 0；P1 HUD 偏移量指示器数据源 |

> **ADR-D3 同步需求**：`CancelTier`、`ComboCountAfter` 字段须同步加入 ADR-D3 `RhythmJudgmentEvent` struct 定义（ADR-D3 §3 当前版本缺失）；`AttackType` 须确认包含 `HeavyAttack` 值（当前 ADR-D3 §3 仅列 LightAttack / ChargedAttack）。

> **广播事件名（W-04 规范化）**：C1 以事件 **`OnRhythmJudgment`**（`event Action<RhythmJudgmentEvent>`）广播上述载体。**此为唯一规范事件名**——C3 订阅 `OnRhythmJudgment`（一致）；C2 文中「订阅 `RhythmJudgmentEvent`」指订阅该事件、消费同名载体结构（载体类型名 = `RhythmJudgmentEvent`，事件名 = `OnRhythmJudgment`，二者不冲突）。ADR-D3 §3 须登记事件名 `OnRhythmJudgment`。

---

### C1-F7：`IRhythmModifierProvider` 消费契约（BC-2）

**用途**：定义 C1 消费 C3 心法运行时修改量的**唯一契约**，解决 BC-2「C3 整个 RhythmRuleModifier / ComboModifier 机制在 C1 无消费点 → 心法运行时零效果」。C3 实现本接口（C3-R7），C1 通过属性注入持有 `RhythmModifierProvider`（`IRhythmModifierProvider?`，**可空**——无 C3 / 无激活心法时为 null，各查询走 `?? 默认值`）。C1 只依赖抽象、不依赖 C3 具体类型（保持单向、可测）。

```csharp
// C1 只读消费；实现方 = C3 PerkModifierStack（namespace 由 ADR-D3/D4 规定）
public interface IRhythmModifierProvider
{
    float  GetPerfectWindowDeltaMs();               // 加法：并入 _effectivePerfectMs 重算（C1-F5）
    float  GetGoodWindowDeltaMs();                  // 加法：并入 _effectiveGoodMs 重算（C1-F5）
    int    GetComboDelta(RhythmRating rating);      // 加法：叠加至 ΔCombo（C1-F4）；Miss 恒 0（护栏）
    float  GetDamageMultiplier();                   // 乘法：= perk_multiplier（C1 不乘入，由下游伤害系统消费）
    float? GetRatingDamageOverride(RhythmRating r); // 覆盖：评级伤害倍率单维覆盖，null=不覆盖
    float  GetHitstunMultiplier();                  // 乘法：Perfect 动力学硬直缩放
}
```

**C1 各查询点与节拍：**

| 方法 | 消费方 & 点位 | 查询节拍 |
|---|---|---|
| `GetPerfectWindowDeltaMs` / `GetGoodWindowDeltaMs` | **C1** — C1-F5 窗口重算 | **缓存**：仅 `OnMeterChanged` / `OnPerkStackChanged` 时各查询一次，结果存 `_effectivePerfect/GoodMs` |
| `GetComboDelta(rating)` | **C1** — C1-F4 ΔCombo | **每次判定**（轻量整型，随评级传参） |
| `GetDamageMultiplier` / `GetRatingDamageOverride` | 下游伤害系统（C2/C6）— **非 C1** | 消费方自持节拍；C1 仅广播 Rating |
| `GetHitstunMultiplier` | PL2 / Perfect 动力学反馈 — 具体归属随 PL2 GDD | 消费方自持节拍 |

> **窗口 delta 为何缓存而非每次 Evaluate 查询（BC-2 节拍收敛）**：C3-R7 原表述「计算 effective-window 后查询」易被误读为每击查询。实际 perk 栈仅在房间之间选/弃心法时变化（Roguelike 流程），故 C1 在两个低频事件（meter 生效、perk 栈变更）后重算并缓存，判定热路径（`OnHalfBeatLookahead`）零 provider 查询开销。此为对「C3-R7 每次 Evaluate vs C1 仅 OnMeterChanged 缓存」冲突的收敛决定——工程正确解，不改变体感。ΔCombo 例外走每判定查询（整型、无重算成本）。

> **Off 模式接线（BD-1 已裁定）**：owner 裁定采用「Off perk 封顶」（见跨评审 BD-1 修复方向①）。Off 模式下 `RhythmModifierProvider` 各查询**仍可执行**（无障碍反馈不变），但其 `GetDamageMultiplier()` 贡献的**聚合 `perk_multiplier` 在 Off 分支独立封顶 1.0**（「节奏模式独占」护栏，见 **C1-F5c**）——即 Off 下心法不产生正向战力增益。窗口 delta（`GetPerfectWindowDeltaMs` 等）在 Off 下无实际效果（Off 偏移量恒 0，评级恒 Perfect），ΔCombo delta 正常累积但受 ComboMax 约束。C1 Q3（`meritMultiplierOff`）与 C3 OQ-C3-3 据此同批锁定：Off 功勋沿用既有折算三重隔离，不新增绕过。**AC-C1-26 从阻塞桩改为可判定**：断言 Off 下 `perk_multiplier ≤ 1.0`。

---

## Edge Cases

### E1：量化模式战斗中途切换（Off ↔ HalfBeat/Beat）

**场景**：玩家在战斗中途通过游戏设置将量化模式从 HalfBeat 切换为 Off（或反向）。

**处理规则**：
- 模式切换立即生效，下一次攻击输入使用新模式评级。
- 切换时清空 `_pendingInput` 缓冲区（旧模式下存入的待评级输入视为无效）。
- 切换时不产生任何 `RhythmJudgmentEvent`（切换本身不触发判定）。
- LateWindow 若已开启，切换后立即关闭（丢弃旧窗口）。

---

### E2：时钟未就绪时的输入处理（`IsStartConfirmed == false`）

**场景**：玩家在 BGM 首拍到来前（时钟未确认阶段）立即攻击。

**处理规则**（来自 Core Rule 7）：
- 输入存入 `_pendingInput` 缓冲区（单槽逻辑同样适用）。
- 等待 `IsStartConfirmed` 变为 true 后，随第一个 `OnHalfBeatLookahead` 事件统一处理。
- 若输入等待超过 2 秒仍无时钟确认：**静默丢弃**缓冲区输入，不产生任何 `RhythmJudgmentEvent`，记录 `Warning` 日志（防止无限等待）。

> **为何有 2s 超时**：F2 若因音频系统错误未能确认时钟，玩家攻击不应被永久挂起；2s 为安全回退阈值。静默丢弃而非输出 Miss——F2 故障时玩家未做出任何节奏承诺，不应受惩。

---

### E3：充能攻击中途松开退化为轻攻击（E3a）

**场景**：玩家开始充能（按下按键），充能完成前松开——F3 降级为轻攻击，`inputDspTime` 为松开时刻，`chargeStartDspTime` 仍填充按下时刻（非 -1.0）。

**处理规则**（C1-F1 的推论，与 F3 guard table 一致）：
- F3 在 E3a 场景中仍填充 `chargeStartDspTime`（按下时刻），仅将 `attackType` 降级为 LightAttack。
- C1-F1 检测到 `chargeStartDspTime > -1.0`，取 `chargeStartDspTime`（按下时刻）作为判定锚点（见 F3 GDD Section F3-F2 guard table）。
- 判定结果取决于按键时刻与拍点的偏差：玩家若踩准时机按下，即使充能中途松开也可获得 Perfect。

> **设计意图**：玩家的节奏承诺在按下瞬间发生，松开是技巧选择而非节奏决策。用按下时刻判定确保节奏精准度被正确奖励，不因充能时长或松开时机而扭曲。此立场与 Rule 6（充能完成场景）保持一致：两种场景均以"按下时刻"为锚点，行为对称。

---

### E4：同一半拍双击（单槽覆盖行为）

**场景**：玩家在同一 EarlyWindow 内快速双击攻击键。

**处理规则**（来自 Core Rule 4）：
- 第一次输入进入 `_pendingInput` 缓冲区。
- 第二次输入到达时，`_pendingInput` 已有内容但尚未评级——第二次输入覆盖第一次。
- 拍点到来后只评级第二次输入（覆盖后唯一剩余的内容）。
- 无论哪次输入被评级，只广播一次 `RhythmJudgmentEvent`。

> **预期玩家体验**：玩家会感觉"只有一刀落地"，第一刀的 VFX/SFX 正常触发（来自 F3/F6），但节奏判定只有一次。

---

### E5：LateWindow 过期精度（30fps 移动端）

**场景**：移动端运行在 30fps（帧时间 33.3ms），`LateWindowMs = 100ms` 约覆盖 3 帧。输入恰好在第 3 帧末尾到达，但 LateWindow 在当帧已过期。

**处理规则**：
- C1 使用 DSP 时间戳（来自 F3 Input System callback），不依赖 `Update()` 帧时间。
- **LateWindow 过期的权威判定方式为 DSP 时间比较**：`inputDspTime - beatDspTime > LateWindowMs / 1000`。此为 GDD 层的规范性定义。
- 因此 30fps 帧率不影响 LateWindow 的判定精度；边界误差来自 DSP 粒度（Android 最大 ~40ms），不来自帧率。
- 在 DSP 粒度误差范围内恰好落在 LateWindow 边界的输入：视为**丢弃**（不产生判定，不产生 Miss）。

> **ADR-D3 一致性注意**：ADR-D3 当前伪代码使用 `Time.realtimeSinceStartup` 作为 LateWindow 到期判断依据，与本规范矛盾。实现时须改为 DSP 时间比较，ADR-D3 须同步修正。

---

### E6：游戏暂停时的幽灵判定（Ghost Judgment）

**场景**：玩家暂停游戏时，`_pendingInput` 缓冲区或 LateWindow 已激活。

**处理规则**（来自 Core Rule 8）：
- 暂停立即：清空 `_pendingInput`、关闭 `_lateWindowActive`、停止对 `OnHalfBeatLookahead` 的处理。
- 恢复后：C1 以"等待输入"状态重新激活，不产生暂停前的残留判定。
- **幽灵判定定义**：暂停时缓存的输入在恢复后仍被评级的现象——这在本系统中被明确禁止。

---

### E7：Android 高 BPM 时 Perfect/Good 窗口坍缩

**场景**：Android 平台（`platformMinPerfectMs = 80ms`），BPM > 187（`halfBeatMs/2 ≈ 80ms`），运行时钳位后 `GoodHalfWindowMs_effective` 已缩减到 ≤ 80ms，低于平台 Perfect 最小值。

**临界 BPM 计算**（HalfBeat 模式，`gridIntervalMs = halfBeatMs`）：
- 坍缩条件：`GoodHalfWindowMs_effective < platformMinPerfectMs(80ms)`
- `gridIntervalMs/2 - 1 < 80` → `halfBeatMs < 162ms` → `BPM > 185.19`
- **HalfBeat 模式 BPM ≥ 186 时**，在 Android 上 `PerfectHalfWindowMs_effective > GoodHalfWindowMs_effective`，触发坍缩。（Beat 模式临界 BPM ≈ 93，但 Beat 模式内容 BPM 通常较低，此场景实际较少出现。）

**处理规则**：
```
// 步骤 1：一次坍缩——将 Good 提升到 Perfect 的平台下限
if PerfectHalfWindowMs_effective > GoodHalfWindowMs_effective:
    GoodHalfWindowMs_effective = PerfectHalfWindowMs_effective   // = platformMinPerfectMs(80ms)

// 步骤 2：二次钳位——防止坍缩后窗口超出网格区间，引发相邻拍重叠
//   (在极高 BPM 下 platformMinPerfectMs 可能超过 gridIntervalMs/2 - 1)
if GoodHalfWindowMs_effective > gridIntervalMs / 2 - 1:
    GoodHalfWindowMs_effective    = gridIntervalMs / 2 - 1
    PerfectHalfWindowMs_effective = GoodHalfWindowMs_effective
    // 此时平台 DSP 精度无法保证 Perfect 可达——记录 Warning 日志
    // 设计上接受此降级：优先保证相邻拍不混叠，而非强行保留 platformMinPerfectMs 承诺
```
- 坍缩后效果：`|offset| ≤ PerfectHalfWindowMs_effective` → Perfect；超出 → Miss（Good 区间消失）。
- 此状态下不产生 Good 评级——C1 只输出 Perfect 或 Miss。

> **设计影响**：Android 高 BPM（≥186）时，评级变为二元（Perfect/Miss），中间缓冲区消失。此为平台 DSP 能力限制的必要降级，比强制报错或不可打出 Perfect 更合理。需在 QA 阶段验证此 BPM 范围的实际游戏内手感。

---

### E8：BPM = 0 或无效 BPM 的回退

**场景**：F2 广播 BPM=0（启动竞态、音频系统错误）或负值。

**处理规则**：
- C1 在 `OnMeterChanged` 中检查：`if data.newBpm <= 0 → 保持当前 BPM 不变，记录警告日志`。
- 不更新任何运行时窗口，不清空缓冲区（状态延续到下一个有效 BPM）。
- `IsStartConfirmed == false` 的保护（Core Rule 7）确保 BPM=0 时不产生任何判定。

---

### E9：Off 模式下的连击与后果

**场景**：量化模式 = Off，始终输出 Perfect——连击数是否无限增长？

**处理规则**：
- Off 模式下每次攻击均触发 `RhythmJudgmentEvent { Rating = Perfect, CancelTier = Standard }`（`CancelTier` 封顶为 Standard，**非** Advanced——见 Off 合约第 5 条 + Rule 10 Off 覆盖规则）。
- 连击公式（C1-F4）正常执行：`CurrentCombo += 2`，受 ComboMax 上限约束。
- 伤害倍率 = 1.0×；**标准**取消窗口开放（高级连招节点不可达，须在 HalfBeat/Beat 模式下以真实节奏精准度赢取）。
- 功勋结算：Off 模式击杀所得功勋由下游（C5/FE5）按 `meritMultiplierOff` 折算（Off 合约第 4 条）；C1 不直接发放功勋，仅暴露激活 `QuantizationMode` 供结算方读取。
- **设计意图**：Off 模式是无障碍通道，玩家始终获得最佳评级与流畅手感（Standard 取消），但通过「连招深度封顶 + 功勋折算 + 排行/成就隔离」三重隔离，防止 Off 模式成为绕过节奏技巧的最优刷取路径（保护 game-concept.md:213 反支柱与 :402 不可豁免约束）；ComboMax 上限另防止无意义的连击数膨胀。

---

### E10：玩家复活/重生时的幽灵评级（Respawn Ghost Judgment，SC-2）

**场景**：玩家死亡时 `_pendingInput` 缓冲区可能持有死前的陈旧输入（或 LateWindow 仍激活）。F6 死亡序列（EC-04，`tde-integration.md`）不经过「游戏暂停」路径，故 Rule 8 的暂停清空**不触发**；复活后 `Play()` 恢复，首个 `OnHalfBeatLookahead` 会用新拍网格评级死前陈旧输入 → 产生「幽灵评级」，错误推进连招。

**处理规则（SC-2）**：
- C1 订阅 F6 的 **`OnPlayerDeath`** 与 **`OnRespawnComplete`** 域事件（F6 `TDELifecycleAdapter` 桥接 TDE `PlayerDeath`/`RespawnComplete` 而来的 CombatCore 可见 C# event，负载不含 TDE 类型——C1 不直接订阅 TDE 事件，遵边界隔离；见 `tde-integration.md` Rule 15），复用 Rule 8 的清空逻辑：`_pendingInput = null`；`_lateWindowActive = false`；停止处理 `OnHalfBeatLookahead`（死亡起）。
- **复活完成时**：清空任何过期评级状态（残留 `_pendingInput`、未消费的 LateWindow），以「等待输入」状态重新激活；重锚拍网格（复用 BC-6 恢复逻辑，若复活伴随时钟恢复）。
- 复活后首个 `OnHalfBeatLookahead` **不得**评级任何死亡前的输入——所有跨越死亡边界的 pending 输入一律丢弃，不产生 `RhythmJudgmentEvent`。
- **幽灵评级定义**：死亡前缓存的输入在复活后仍被评级的现象——本系统明确禁止（与 E6 幽灵判定同源，触发路径不同：E6 由暂停触发，E10 由死亡/复活触发）。

> **设计意图**：死亡切断玩家的节奏承诺连续性；复活是一个全新的战斗上下文。任何残留输入都不代表玩家在复活后的意图，必须清除以防连招/评级被陈旧输入污染。此约束与 SC-2 跨系统场景 G（死亡复活幽灵评级）对称，须在 F6 死亡/复活序列 GDD 中记录 C1 的订阅方（双向合约）。

---

## Dependencies

### 上游依赖（C1 消费）

| 系统 | 数据 | 接口 | 必需性 |
|------|------|------|--------|
| **F2 音频时钟** | `OnHalfBeatLookahead`（`BeatEventData.beat_dsp_time`）、`IsStartConfirmed`、`LatencyOffsetMs`、`OnMeterChanged`（`MeterChangeEventData`，边界生效事件，F2 Core Rule 14）、**`OnBeatClockPaused` / `OnBeatClockResumed`**（暂停/恢复事件，携恢复首拍 `beat_dsp_time`，BC-6；事件名对齐 F2 权威，**非** `OnBpmChanged`） | 事件订阅 + 属性查询 | **必需** — 无 F2 则 C1 无法运行（Off 模式除外） |
| **F3 输入管线** | `AttackInputEvent`（`attackType`, `inputDspTime`, `chargeStartDspTime`） | 事件订阅（`OnAttackInput`） | **必需** — 无 F3 则 C1 无输入来源 |
| **F6 生命周期适配** | `OnPlayerDeath` / `OnRespawnComplete`（F6 桥接域事件，CombatCore 可见，负载不含 TDE 类型，SC-2；见 `tde-integration.md` Rule 15） | 事件订阅（纯 C# event） | **必需** — 复活时清空过期 `_pendingInput` 与评级状态，防幽灵评级（E10） |
| **FE7 游戏设置** | `QuantizationMode`（Off / HalfBeat / Beat） | 属性注入（运行时可切换） | **必需** — 决定评级模式 |
| **ADR-D4 SO 数据层** | `RhythmConfigSO`（窗口默认值、ComboMax） | ScriptableObject 读取 | **必需** — 策划配置数据来源 |
| **C3 心法构筑系统** | `IRhythmModifierProvider`（`GetPerfectWindowDeltaMs` / `GetGoodWindowDeltaMs` / `GetComboDelta`；见 C1-F7）+ `OnPerkStackChanged` 重算触发 | 属性注入（C1 查询抽象接口） | **可选** — 无 C3/无心法时 provider = null，各查询走默认值；C1 不硬依赖 C3（BC-2） |

### 下游被依赖（C1 广播）

| 系统 | 消费数据 | 用途 |
|------|---------|------|
| **C2 连招图** | `RhythmJudgmentEvent`（Rating, AttackType, ComboCountAfter） | 决定连招分支权限（Perfect → 高级取消窗口） |
| **TBD — 伤害倍率应用系统**（待 C2/C6 GDD 确认） | `RhythmJudgmentEvent`（Rating → 伤害倍率 1.0/0.75/0.5） | 乘入最终伤害公式 |
| **P1 HUD** | `RhythmJudgmentEvent`（Rating, OffsetMs, ComboCountAfter） | 显示评级文字、偏移量指示器、连击数 |
| **PL2 VFX 节奏反馈** | `RhythmJudgmentEvent`（Rating = Perfect） | 触发冻帧、编钟共鸣音层、摄像机微切 |
| **PL2 音频反馈** | `RhythmJudgmentEvent`（Rating） | 区分 Perfect/Good/Miss 音效层 |
| **F6/Animancer** | `AnimancerAttackPlayer.Play(attackType, beatDspTime)` | 攻击动画吸附到拍点执行（ADR-D5） |
| **C5/FE5 功勋结算系统** | C1 暴露的激活 `QuantizationMode`（Off 判定） | 读取激活模式，Off 时对 `EnemyInfoSO.rewardMeritBase` 应用 `meritMultiplierOff` 折算（Off 合约第 4 条，C1 不直接发放功勋） |

### 双向合约

- **F2 ↔ C1**：F2 的 `beat_dsp_time` 已在"玩家感知时域"（F2 Core Rule 7）。C1 须对 F3 时间戳加 `latency_offset_s` 后再比较（C1-F2）。若 F2 修改时域约定，C1 的 beat_offset_ms 公式失效。
- **F2 ↔ C1（BC-6 暂停/恢复）**：F2 权威定义 `OnBeatClockPaused` / `OnBeatClockResumed`（携恢复首拍 `beat_dsp_time`）+ `IAudioPlayer.Pause()/Resume()`。C1 订阅两事件：恢复时重锚拍网格并清空过期 `_pendingInput`（Rule 8 + E6）。C1 使用规范化事件名 `OnBeatClockResumed`（**非** `OnBpmChanged`）。F2 GDD 须对称登记 C1 为 `OnBeatClockResumed` 订阅方。
- **F6 ↔ C1（SC-2 复活清空）**：C1 订阅 F6 桥接域事件 `OnPlayerDeath` / `OnRespawnComplete`（CombatCore 可见 C# event，F6 由 TDE `PlayerDeath`/`RespawnComplete` 同帧重广播；C1 禁止直接 `using` TDE），复活时清空过期 `_pendingInput` 与评级状态（E10），防复活幽灵评级。F6 生命周期 GDD 已对称登记 C1（`tde-integration.md` Rule 15 + 双向依赖确认 + AC-F6-25）。此为**跨系统场景 G** 承接合约，F6 广播侧（AC-F6-25）+ C1 消费侧（AC-C1-22e）闭环。
- **F3 ↔ C1**：F3 的 `chargeStartDspTime` 哨兵值为 -1.0（F3 Core Rule 8）。C1 以 `> -1.0` 检测。若 F3 修改哨兵值约定，C1-F1 失效。
- **P1 ↔ C1**：P1（HUD）消费 `RhythmJudgmentEvent` 中的 `Rating` 和 `ComboCountAfter` 展示反馈。P1 须在 Miss 时显示连击数**停止增长**的视觉区分（非清零）。若 C1 修改 Miss ΔCombo 行为，P1 的 Miss 动效设计需同步更新。
- **充能攻击 ↔ PL2**：充能攻击触发 `AttackInputEvent` 后，F3 会向 PL2（VFX 反馈系统）发送预确认信号（pre-confirmation）以触发充能特效。C1 的最终评级（Perfect/Good/Miss）叠加在 PL2 的预确认特效之上；充能特效不等待 C1 评级完成。若 C1 评级为 Miss，PL2 须弱化（非取消）已播放的预确认特效。此时序合约须在 F3 / PL2 GDD 中对称记录。
- **C5/FE5 ↔ C1（Off 隔离合约）**：C1 声明 Off 模式功勋折算的**规范性要求**（Off 合约第 4 条），但**执行方为 C5/FE5**——C1 仅暴露激活 `QuantizationMode`，不感知功勋数值。C5/FE5 功勋结算 GDD **必须**对称记录：Off 模式击杀按 `meritMultiplierOff ∈ (0,1)` 折算 `rewardMeritBase`，且此约束先于 C5/FE5 经济曲线锁定（见 Q3）。若 C5/FE5 未实现此折算，Off 模式将击穿 game-concept.md:213 反支柱（零技巧最优刷取）。此为**阻塞下游**合约。

---

## Tuning Knobs

| 旋钮名称 | 字段 / SO 路径 | 类型 | 默认值 | 安全范围 | 影响的游戏体验 |
|---------|---------------|------|--------|---------|--------------|
| `PerfectHalfWindowMs` | `RhythmConfigSO.PerfectHalfWindowMs` | float | 50 ms | [16, 100] | 缩小 → Perfect 更难打出，高手挑战感↑；放大 → 更宽容，新手友好 |
| `GoodHalfWindowMs` | `RhythmConfigSO.GoodHalfWindowMs` | float | 120 ms | [50, 200] | 缩小 → Miss 更易出现，惩罚感↑；放大 → 整体更宽松。受运行时钳位约束（C1-F3），BPM>125 时自动缩减 |
| `EarlyWindowMs` | `RhythmConfigSO.EarlyWindowMs` | float | 120 ms | [50, 200] | 缩小 → 玩家必须更精确地等待拍点，"抢拍"惩罚↑；放大 → 提前输入更被容忍 |
| `LateWindowMs` | `RhythmConfigSO.LateWindowMs` | float | 100 ms | [50, 200] | 缩小 → 慢半拍的玩家更容易被丢弃；放大 → 后拍容忍度↑ |
| `ComboMax` | `RhythmConfigSO.ComboMax` | int | **999**（对齐 registry `combo_max`） | [148, 999] | 影响连击系统的心理上限和 UI 显示设计；下界 148 = `combo_multiplier` 触顶点（低于此会提前截断倍率），上界 999 = registry 权威平台段 |
| `LookaheadMs` | `RhythmConfigSO.LookaheadMs` | float | 来自 ADR-D3 | [50, 200] | F2 提前广播拍点事件的时间量；C1 用它确定 EarlyWindow 起始边界 |
| `AdaptationWindowBeats`（BD-2） | `RhythmConfigSO.AdaptationWindowBeats` | int | **2**（owner 联合锁定 2026-08-11，对齐 registry `adaptation_window_beats`） | [0, 4] | BPM 上升后判定窗宽容期时长（拍）；窗口内窗口宽度取变更前后较宽者（C1-F5b），防 BPM 跃升三系统难度尖峰。缩小 → 更快恢复新 BPM 标准难度（张力↑）；0 = 关闭宽容期；放大 → 过渡更平缓。**单一 owner = 本字段（PascalCase 权威）**，C2 camelCase `adaptationWindowBeats` 为引用别名、语义等同；C1/C2/C4 三 owner 已联合锁定 2 拍（先于 FE4 Boss GDD） |
| `platformMinPerfectMs` | 平台配置（非 SO） | float | PC=16.7, iOS=39, Android=80 | [0, 120] | 平台保底 Perfect 窗口；过小则该平台玩家物理上无法打出 Perfect |
| `ΔCombo` 表 | `RhythmConfigSO.DeltaCombo_*` | int×3 | Perfect=+2, Good=+1, Miss=0 | Perfect∈[1,5], Good∈[0,3], Miss=[0,0]（硬编码，不可负） | 调整连击增速；提高 Perfect 奖励可强化节奏激励循环。Miss ΔCombo **不可为负**——Miss 不清零是 game-concept.md 规则五合约，实现层须断言 `ΔCombo_Miss == 0` |
| `meritMultiplierOff` | C5/FE5 经济配置（**非 C1 SO**，C1 仅暴露激活模式） | float | **0.5**（owner 联合锁定 2026-08-11，对齐 registry `merit_multiplier_off`） | (0, 1) — 严格开区间，不可为 0（否则完全剥夺无障碍玩家进度）或 ≥1（否则失去隔离意义） | Off 模式功勋折算系数；过高则「零技巧刷取」漏洞复现，过低则惩罚无障碍用户。已锁定 0.5（占位转正）；C5/FE5 经济 GDD 创建时须对称记录并将 owner 迁移至经济 GDD（Off 合约第 4 条为 C1 规范性要求，执行方 C5/FE5） |

**调参指南**：
- 先调 `PerfectHalfWindowMs` 和 `GoodHalfWindowMs` 确定基础手感，再调 `EarlyWindowMs` / `LateWindowMs` 确定缓冲宽容度。
- BPM 跨度大的关卡（80–240）必须在最高 BPM 下测试运行时钳位后的实际有效窗口。
- `ComboMax` 最终值 = **999**（Q1 已解决：C2/C3 设计完成，以 registry `combo_max=999` 为权威；`combo_multiplier` 于 count=148 触顶，故 148 以上纯为连击数平台段）。

> **`platformMinPerfectMs` 实现指引**（"平台配置，非 SO"）：**推荐实现方式**：`RhythmJudger.Awake()` 或 `Start()` 中使用 `Application.platform` 运行时判断赋值（`RuntimePlatform.Android / IPhonePlayer / WindowsPlayer` 等），而非 `#if UNITY_ANDROID` 等编译期宏——宏在 Editor Play Mode 下始终走 PC 分支，无法验证 Android/iOS 路径。在 `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 块内暴露 `SetPlatformMinForTesting(float valueMs)` 注入方法，与 M3 接口注入方案对称，确保 Editor 内可单独覆盖测试各平台保底窗口。

---

## Visual/Audio Requirements

> **(lean 模式注)**: `creative-director` / `audio-director` 未参与本节。正式 art bible 批准前建议重审。

- **Perfect 动力学序列**（由 PL1/PL2 消费 `RhythmJudgmentEvent`）：
  - 冻帧：2–4 帧（33–66ms @ 60fps）
  - 编钟共鸣音层：叠加在当前 BGM 上
  - 摄像机微切：轻微 zoom-in 或 shake
  - 敌人硬直延长
- **Good**：标准打击反馈（无额外动力学增强）
- **Miss**：削弱打击感（反馈减淡、无编钟音层）
- C1 本身不直接控制视觉/音效——仅广播 Rating，PL1/PL2 订阅实现

---

## UI Requirements

- C1 本身无 UI 元素。评级显示和连击数 UI 由 P1（HUD）系统消费 `RhythmJudgmentEvent` 实现。
- C1 须在 `RhythmJudgmentEvent` 中提供 P1 所需的全部数据：`Rating`, `OffsetMs`, `ComboCountAfter`。

---

## Acceptance Criteria

### 核心判定（Core Rules 1–5）

- **AC-C1-01**：HalfBeat 模式下，输入在 EarlyWindow 内到达 → 拍点到来时评级，偏移量 ≤ PerfectHalfWindowMs → Perfect。
- **AC-C1-02**：HalfBeat 模式下，输入在 LateWindow 内到达 → 立即评级，正偏移量（晚于拍点）≤ GoodHalfWindowMs_effective → Good。
- **AC-C1-02b**：HalfBeat 模式下，输入在 LateWindow 末尾（偏移量 > GoodHalfWindowMs_effective 且 **< LateWindowMs**，不含精确边界点）到达 → Miss（不丢弃，产生 Miss `RhythmJudgmentEvent`）。精确边界点（偏移量 == LateWindowMs）适用 AC-C1-04（丢弃，不产生事件）。
- **AC-C1-03**：输入偏移量 > `GoodHalfWindowMs_effective` → C1 广播 `RhythmJudgmentEvent { Rating=Miss, CancelTier=Closed }`，`ComboCountAfter == ComboCountBefore`（连击不增长、不清零）。伤害倍率 0.5× 由下游伤害系统按 Rating 字段应用，不在 C1 层验证。
- **AC-C1-04**：LateWindow 过期后到达的输入 → 不产生 `RhythmJudgmentEvent`，不产生 Miss。
- **AC-C1-05**：不攻击的拍点 → 无 `RhythmJudgmentEvent` 广播，无惩罚。
- **AC-C1-06**：Off 模式下，任何攻击输入 → 广播 `RhythmJudgmentEvent { Rating=Perfect, CancelTier=Standard }`，偏移量 = 0，即时触发。**断言 `CancelTier == Standard`（非 Advanced）**——验证 Off 覆盖规则生效（Off 合约第 5 条 + Rule 10）：即便评级为 Perfect，Off 模式亦不授予 Advanced 高级取消。
- **AC-C1-07**：Beat 模式，BPM=120 时，`gridIntervalMs = 500ms`（全拍间距，HalfBeat 的 2 倍）；`GoodHalfWindowMs_effective` 上界钳位 = `gridIntervalMs/2 - 1 = 249ms`；给定 `GoodHalfWindowMs_authored=120ms`，有效值 = 120ms（未触发钳位）；偏移量 ≤ `PerfectHalfWindowMs_effective` → Perfect，≤ 120ms → Good，> 120ms → Miss。**架构说明**：F2 在 Beat 模式下仍每 250ms 广播 `OnHalfBeatLookahead`（F2 Core Rule 3 不变，F2 不感知 C1 量化模式）；C1 内部维护 skip toggle，初始为"处理"相位，逐次交替处理/跳过，等效全拍判定网格。
  - **AC-C1-07a（正向 — 判定窗口开放验证）**：Beat 模式 BPM=120，在第 1 次 `OnHalfBeatLookahead` 的 EarlyWindow 内注入攻击输入 → 事件触发后产生 `RhythmJudgmentEvent`（"处理"相位，窗口开放）；在第 3 次事件 EarlyWindow 内同样注入 → 再产生一次事件。4 次事件共产生恰好 2 次 `RhythmJudgmentEvent`。
  - **AC-C1-07b（负向 — skip 验证）**：Beat 模式 BPM=120，在第 2 次 `OnHalfBeatLookahead` 的 EarlyWindow 内注入攻击输入 → 第 2 次事件触发后**不**产生 `RhythmJudgmentEvent`（"跳过"相位，输入留存缓冲区）；第 3 次事件到来后，缓冲的输入被评级，产生 1 次 `RhythmJudgmentEvent`。

### 缓冲区与时钟（Core Rules 4, 7, 8）

- **AC-C1-08**：同一半拍 EarlyWindow 内双击 → 只产生一次 `RhythmJudgmentEvent`（第二次输入覆盖第一次）。
- **AC-C1-09**：`IsStartConfirmed == false` 时输入到达 → 缓冲，首拍确认后统一评级。
- **AC-C1-10**：游戏暂停 → 缓冲区清空、LateWindow 关闭；恢复后无幽灵判定。
- **AC-C1-10b（BC-6 恢复重锚 + 清空）**：**BPM=120**，暂停前 `_pendingInput` 持有一条陈旧输入。触发 F2 `OnBeatClockResumed`（携恢复首拍 `beat_dsp_time`）→ **断言**：① `_pendingInput == null`（过期 pending 已清空，即使暂停已清亦幂等）；② C1 以恢复首拍 `beat_dsp_time` 重锚半拍网格（`halfBeatMs==250ms`，下一 EarlyWindow 起点对齐恢复首拍）；③ 恢复后首个 `OnHalfBeatLookahead` **不**评级任何暂停前输入（无 `RhythmJudgmentEvent`）；④ 连击数未被恢复重置。**事件名断言**：C1 订阅的恢复事件名为 `OnBeatClockResumed`，**非** `OnBpmChanged`（W-03/BC-6 规范化）。

### 公式验证（C1-F1~F5）

- **AC-C1-11**：`chargeStartDspTime > -1.0` → 判定使用 `chargeStartDspTime`（无论 `attackType` 为 ChargedAttack 或 LightAttack）；`chargeStartDspTime == -1.0`（普通轻攻击）→ 使用 `inputDspTime`。
- **AC-C1-12**：`beat_offset_ms` 计算：给定 `judgment_time=T`, `latency_offset_s=L`, `beat_dsp_time=B` → 结果 = `(T+L−B)×1000`（正负号正确）。
- **AC-C1-13**：BPM=160 时，`GoodHalfWindowMs_authored=120` → 运行时钳位为 `min(120, 93.75-1) = 92.75ms`。
- **AC-C1-14**：Perfect → ComboCount += 2；Good → += 1；Miss → += 0（不归零）。ComboCount 不超过 ComboMax（**999**，对齐 registry `combo_max`）。
- **AC-C1-15**：`OnMeterChanged` 事件（`data.bpmChanged==true`，新 BPM 有效）→ 有效窗口重算 + `_pendingInput` 清空 + `_lateWindowActive` 关闭 + `_skipToggle` 重置为"处理"相位。**Beat 模式额外断言**：事件后立即注入攻击输入，触发一次 `OnHalfBeatLookahead` → 应产生 `RhythmJudgmentEvent`（skip toggle 已重置为处理相位，首拍不被跳过）。**拍号-only 变更断言**：`OnMeterChanged` 携 `data.bpmChanged==false, data.newBpm==oldBpm` → `currentBpm` 不变、窗口重算幂等（值不变）、缓冲仍清空。
- **AC-C1-15b（BD-2 适应窗口宽容期）**：`AdaptationWindowBeats=2`，HalfBeat 模式，`GoodHalfWindowMs_authored=120`。**从 BPM=120 跃升至 BPM=180**（`OnMeterChanged` 携 `bpmChanged==true`）：变更前 `_effectiveGoodMs==120`（BPM=120 未钳位），变更后标准 `_effectiveGoodMs==82.3`（BPM=180 钳位 `gridIntervalMs/2-1`）。**断言**：在适应窗口内（生效边界拍起 `2×fullBeatMs==2×333.3==666.7ms` 内）判定采用 `_adaptGoodMs == max(120, 82.3) == 120ms`（沿用较宽旧窗口，不骤缩）；窗口结束后（`nowDspTime ≥ _adaptationEndDspTime`）判定采用标准 `82.3ms`。**反向断言（BPM 下降 180→120，窗口不应收窄）**：`_adaptGoodMs == max(82.3, 120) == 120ms`，同样取较宽者。**`AdaptationWindowBeats==0` 对照**：无宽容期，`OnMeterChanged` 后立即采用标准 `82.3ms`。

### 边界情况（E1–E10）

- **AC-C1-16**：量化模式战斗中切换 → 缓冲区清空，下一输入使用新模式评级。
- **AC-C1-17**：时钟未确认 2s 超时 → 静默丢弃缓冲区输入，不产生任何 `RhythmJudgmentEvent`，记录 `Warning` 日志。
- **AC-C1-18**：E3a 退化充能（`attackType=LightAttack`, `chargeStartDspTime > -1.0`）→ 使用 `chargeStartDspTime`（按下时刻）判定，与 F3 guard table 一致。
- **AC-C1-19**：Android BPM ≥ 186 → Perfect/Good 窗口坍缩，只产生 Perfect 或 Miss（Good 评级消失）；BPM 极高时（如 BPM=240 HalfBeat 模式，`gridIntervalMs/2-1=61.5ms < platformMinPerfectMs=80ms`）触发二次钳位，记录 Warning 日志。**此时 `platformMinPerfectMs=80ms` 承诺被设计放弃**：`PerfectHalfWindowMs_effective` 降至 `gridIntervalMs/2-1`（如 61.5ms），该平台该 BPM 下 Perfect 物理可达性无法保证。Warning 日志须包含当前 BPM 值和实际有效 Perfect 窗口值。
- **AC-C1-20**：`OnMeterChanged`（`data.newBpm == 0`）→ BPM 不更新，窗口不变，记录警告日志。
- **AC-C1-21**：Off 模式连击 → 每次 +2，受 ComboMax 约束；每次事件 `CancelTier == Standard`（连招深度封顶断言，见 AC-C1-06）。
- **AC-C1-21b（功勋隔离契约）**：Off 模式激活期间，C1 暴露的激活 `QuantizationMode == Off`，供下游功勋结算系统（C5/FE5）读取。**C1 层可验证**：C1 广播/暴露的模式状态在 Off 下恒为 `Off`（结算方据此应用 `meritMultiplierOff` 折算）。折算金额本身的验收属 C5/FE5 集成 AC（C1 不发放功勋）。
- **AC-C1-22e（SC-2 复活幽灵评级防护）**：**BPM=120**，玩家死亡瞬间 `_pendingInput` 持有一条死前陈旧输入（未评级）。触发 F6 `PlayerDeath` → **断言**：`_pendingInput == null`、`_lateWindowActive == false`、停止处理 `OnHalfBeatLookahead`。随后触发 F6 `RespawnComplete` → C1 以「等待输入」状态重新激活，重锚拍网格（`halfBeatMs==250ms`）；**复活后首个 `OnHalfBeatLookahead` 不产生任何 `RhythmJudgmentEvent`**（死前输入已丢弃，连击数不因幽灵评级推进）。**对照**：复活后玩家发出的**新**输入按新拍网格正常评级（证明清空不影响正常流程）。

### 评级后果合约

> **集成验收说明（AC-C1-22/23/24）**：以下三项 AC 依赖下游系统（TBD 伤害系统、C2 连招图、PL2 VFX）的 GDD 完成后方可独立验收。当前阶段可验证 `RhythmJudgmentEvent` 字段正确性（Rating、CancelTier、ComboCountAfter），后果效果留待集成测试。

- **AC-C1-22**：`RhythmJudgmentEvent { Rating=Perfect, CancelTier=Advanced }` 正确广播；Perfect 动力学（冻帧/编钟/摄像机微切）由 PL2 订阅验证（集成 AC）。
- **AC-C1-23**：`RhythmJudgmentEvent { Rating=Good, CancelTier=Standard }` 正确广播；标准取消权限由 C2 订阅验证（集成 AC）。
- **AC-C1-24**：`RhythmJudgmentEvent { Rating=Miss, CancelTier=Closed, ComboCountAfter==ComboCountBefore }` 正确广播；连击不变且不清零（C1 层可独立验证）。
- **AC-C1-25（BLOCKING，BC-2 perk delta 消费）**：注入 mock `IRhythmModifierProvider`，令 `GetPerfectWindowDeltaMs()==+20`、`GetComboDelta(Perfect)==+3`。GIVEN `OnPerkStackChanged` 触发后，THEN `_effectivePerfectMs` == clamp(authored+20, …)（窗口重算含 delta）；WHEN Perfect 判定，THEN `ComboCountAfter == Before + 2 + 3`。GIVEN provider == null（无心法），THEN 窗口/ΔCombo 退化为无 perk 基线（`?? 0`）。**Miss 护栏**：provider `GetComboDelta(Miss)` 无论返回何值，Miss 的 ΔCombo 恒 = 0（C1-F4 短路，不查询 perkDelta）。
- **AC-C1-26（BD-1 Off perk 封顶——可判定）**：owner 裁定「Off perk 封顶」。注入 mock `IRhythmModifierProvider`，令 `GetDamageMultiplier() == 1.8`（模拟满栈数值心法）。**BPM=120，`QuantizationMode == Off`**：任一攻击输入 → 广播 `RhythmJudgmentEvent { Rating=Perfect, CancelTier=Standard }`；**断言下游可见的聚合 `perk_multiplier == min(1.8, 1.0) == 1.0`**（C1-F5c Off 分支 clamp）。对照组 `QuantizationMode == HalfBeat`、同一 mock、Perfect 判定 → `perk_multiplier == 1.8`（未封顶，受 C3 PERK_MULT_MAX=2.0 约束）。**功勋隔离**：Off 下功勋仍按 `meritMultiplierOff` 折算（不因 perk 封顶而绕过，AC-C1-21b）。此断言与 C3 AC-C3-15(b)「Off 下 `perk_multiplier ≤ 1.0`」对齐。

---

## Open Questions

| ID | 问题 | 阻塞 | 建议解决时间 |
|----|------|------|------------|
| **M3** | **已解决**：`IBeatClock` / `IInputEventBus` 正式声明为接口，采用**字段注入 + 属性包装**方案：① `[SerializeField] private MonoBehaviour _beatClockRef` 持有 Unity 组件引用；② `private IBeatClock BeatClock => _beatClockRef as IBeatClock`（属性包装）；③ 单元测试中通过 `SetBeatClockForTesting(IBeatClock mock)` 方法注入 mock，setter 仅在 `#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD` 块内暴露。此方案满足 Unity 6.3 LTS 的 `[SerializeField]` 字段限制，同时支持纯 C# NUnit 单元测试。ADR-D3 M3 注记须同步更新。 | 已解决 | ✅ 2026-07-21 |
| **M0** | M0 Spike 窗口值验证（PerfectHalfWindowMs=50, GoodHalfWindowMs=120）——这些是估算值，需原型阶段实测确认 | 原型验证 | M0 Spike |
| **Q1** | **已解决**：`ComboMax` = **999**（对齐 registry `combo_max`；C2/C3 已成文，`combo_multiplier` 于 count=148 触顶 ×5.0，148→999 为平台段），写入 RhythmConfigSO | C2/C3 GDD | ✅ 2026-08-10 |
| **Q2** | E2 时钟未确认 2s 超时值——是否需要平台差异化（移动端启动更慢）？ | 平台测试 | 移动端原型 |
| **Q3** | ~~`meritMultiplierOff` 最终值（Off 模式功勋折算系数，占位建议 0.5）~~ **已锁定〔owner 联合锁定 2026-08-11〕= 0.5**（安全范围 (0,1) 开区间，登记 registry `merit_multiplier_off`）。由无障碍公平性与经济防刷两方权衡确定：Off 已有连招深度封顶 Standard + perk 封顶 1.0 + 排行/成就隔离三重护栏，功勋折算为第四重，0.5（半额）为均衡点。Off 合约第 4 条为 C1 规范性要求，执行方 C5/FE5；C5/FE5 经济 GDD 创建时须对称记录并将 owner 迁移。 | RESOLVED（owner 联合锁定，先于 C5/FE5 落地） | ✅ 2026-08-11 |
