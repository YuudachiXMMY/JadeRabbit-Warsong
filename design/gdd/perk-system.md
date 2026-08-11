# 心法构筑系统 (Perk System)

> **Status**: Revised (2026-08-10 blocker fix) — Pending Re-review
> **Author**: user + design-system agents
> **Last Updated**: 2026-08-10
> **Implements Pillar**: 支柱四「构筑驱动复盘」(Build Depth Over Stat Inflation)；受支柱一「节奏即战斗」护栏约束
> **System ID**: C3 | **Layer**: Core | **Milestone**: MVP
> **Depends on**: F4 (PerkSO schema)、C1 (节奏规则修改点)
> **Creative Director Review (CD-GDD-ALIGN)**: SKIPPED — Lean mode（生产前于 fresh session 跑 /design-review 独立验证）

## Overview

心法构筑系统（Perk System，C3）是《玉兔：破阵》的**局内构筑层**：玩家每清空一间房间，从三张心法卡中选一张（三选一），逐步拼出一套改变本局节奏战斗规则的构筑。心法不是被动的数值堆叠，而是**对节奏规则本身的运行时修改**——「完美判定后拓宽下一击的 Perfect 窗口」比「+15% 伤害」更能定义一局的手感。这正是**支柱四「构筑驱动复盘」**的核心表达：每局因心法组合而感觉不同，而非只是数字更大。

在数据层，每张心法是一个 `PerkSO`（F4-S3 定义），描述其稀有度、效果类型（`PerkEffectType`）、触发条件（`PerkTrigger`）、叠加行为（`StackBehavior`）与数值参数（`PerkParameter` 列表）。C3 拥有 `PerkSO` 的**运行时生命周期**：局内通过 `Instantiate` 克隆原始资产以承载本局叠加状态，局末销毁全部克隆、不写入持久化（遵守 ADR-D4 的克隆规则 D4-2）。C3 通过一个自建的**修改器栈（PerkModifierStack）+ C1 只读查询接口（`IRhythmModifierProvider`）**把「规则修改」注入节奏系统——C1 在计算 effective-window 与评级后果时向 C3 查询聚合修改量，全程不写 SO、不绕过 C1 的运行时窗口重算（遵守 ADR-D3 与 D4-1）。

系统受一条不可逾越的边界约束：**game-concept 规则五「心法设计空间约束」**。心法可以修改评级后果的数值参数，但不得合并评级层级、不得让 Miss 产生正向连击、不得无条件对全评级开放高级取消窗口——节奏精度在任何 build 下都必须保有可感知的价值（支柱一「节奏不可绕过」）。C3 向下游提供三项契约：向 FE2 提供**心法奖励分发**、向 FE5 提供**心法池扩展**接口、向 P1/P2 提供**心法状态与候选数据**。

> *实现约束参见 ADR-D4（SO 数据层：PerkSO 克隆规则、Odin 双模校验、`PerkEffectType` 锁定为 C3 实现冲刺的 gating 交付物）与 ADR-D3（节奏量化核心：`OnRhythmJudgment` 事件、运行时 effective-window 重算路径）。*

## Player Fantasy

**核心幻想：我在为这一局谱一套自己的节奏武学。**

心法三选一的那一刻，玩家不是在挑「哪个数字更大」，而是在决定**这一局我想怎么打**。当一张「Perfect 后下一击自动拓宽判定窗」的心法亮起，玩家眼里看到的不是 +X%，而是一个念头：*「如果我能连上第一个 Perfect，后面整条连段都会变顺」*——心法把一个抽象的构筑选择，变成了一个关于**节奏手感**的承诺。

这套系统服务三种交织的情绪：

- **掌控感（Competence 延伸）**：好的心法奖励的是「我练出来的踩拍精度」，而不是替玩家踩拍。玩家拿到强力心法后会想*「现在我要打得更准去兑现它」*，而非*「现在我可以乱按了」*。心法是精通的放大器，不是精通的替代品。
- **发现感（Discovery）**：玩家主动去找「改变规则」的心法，并在局内撞见协同——「原来『Good 也进连击』配上『高连击拓宽 Perfect 窗』能滚起来」。这种 aha 时刻是复盘冲动的燃料。
- **自我表达（Expression）**：同一把苗刀，激进玩家堆「Perfect 高回报」构筑，稳健玩家堆「Miss 减伤 + Good 补偿」构筑——心法让玩家的节奏偏好外化成一套可辨认的打法。

**锚定的玩家时刻**：Boss 二阶段拍号从 4/4 切到 3/4，玩家心里一紧——但他这局选的心法正好是「拍号切换后 2 拍内判定窗加宽」。他没有慌，反而笑了：*「我为这一刻准备过。」* 那一瞬间，构筑选择、节奏精通、临场决策三者合一——这就是心法系统存在的理由。

**反面幻想（本系统明确不提供）**：心法**不**提供「让我忽略节奏也能满额输出」的解脱感。任何让玩家觉得「拿到这个就不用踩拍了」的心法都违背支柱一，是设计红线（见规则五护栏）。心法带来的爽，永远是「我的精通被放大了」的爽。

## Detailed Design

### Core Rules

**C3-R1 心法生命周期与克隆**（引用 F4-S3 与 F4「规则 3：PerkSO 运行时克隆规则」）
1. 局开始前、以及局内奖励分发时，C3 对**被玩家选中**的原始 `PerkSO` 调用 `Instantiate()` 克隆，登记入 `_activeClones` 注册表。
2. 克隆承载本局叠加状态（当前 stack 数、触发计数、时限）；原始资产全程只读（ADR-D4 D4-1）。
3. 局末（死亡/通关）C3 `Destroy()` 全部克隆并清空 `_activeClones`，**不写入任何持久化**（D4-2）。
4. **iconSprite 约束（强制）**：`Instantiate` 为浅拷贝，克隆与原始共享同一 `AssetReferenceT<Sprite>`。C3 **禁止**对 `clone.iconSprite` 调用 `ReleaseAsset()`/`LoadAssetAsync()`；图标加载/释放由 P2 在 `original.iconSprite` 上管理。销毁克隆不得触发图标释放。
5. `PerkParameter` 必须为值类型或 string（F4 D4-9）——否则浅拷贝共享引用会污染原始资产。

**C3-R2 心法构筑（PerkLoadout）**：本局已获心法集合 = `PerkLoadout`（克隆列表）。每张携带 `effectType / triggerCondition / stackBehavior / maxStacks / 当前 stack`。

**C3-R3 PerkEffectType 与路由**
- 锁定 5 值枚举（F4 拥有，C3 最终确认）：`DamageModifier / ComboModifier / RhythmRuleModifier / MovementModifier / MetaModifier`。**此锁定是 ADR-D4 解阻 C3 实现冲刺的 gating 交付物。**
- `RhythmRuleModifier` 子类经 `PerkParameter.key` 命名空间细分（C3-owned `RhythmRuleSubtype` 约定，不新增 PerkSO 字段）：`rhythm.window.*`（判定窗）/ `rhythm.cancel.*`（取消层级，受护栏 3 约束）/ `rhythm.meter.*`（拍号适配）。
- C3 `PerkEffectRouter` 按 `effectType` + key 前缀路由到对应 handler。
- **MVP 只作者 Damage/Combo/RhythmRule 三类**；Movement/Meta 枚举锁定但 MVP 不产内容。

**C3-R4 触发模型（PerkTrigger，F4 权威 5 值）**
- `Passive`＝常驻，注册即生效；`maxStacks` 必为 1、`stackBehavior` 必为 `Capped`（F4 Odin 强制）。
- `OnPerfect/OnGood/OnMiss/OnComboEnd` 由订阅 C1 `OnRhythmJudgment(RhythmJudgmentEvent)` 驱动：C3 读 `event.Rating` 映射触发器；`OnComboEnd` 依据连段结束判定（须与 C2 对齐——见 Open Questions）。〔BC-6/W-04：事件名 `OnRhythmJudgment` 为 C1-F6 唯一规范名，载体结构类型名 `RhythmJudgmentEvent`（二者不冲突）；C3 订阅与 C1、C2 三方对齐同一事件/载体。〕

**C3-R5 叠加行为（StackBehavior，F4 权威）**：`Additive`＝每次触发 stack+1（clamp 至 maxStacks），修改量按 stack 线性累积；`Capped`＝达 maxStacks 停止触发；`Replace`＝新触发覆盖旧（刷新时限）。

**C3-R6 修改器栈与聚合（按种类分治）** — 系统核心
C3 维护 `PerkModifierStack`，把全部激活心法的修改量按**目标 × 种类**聚合：
- **加法类（Additive numeric）**：判定窗 ±ms、ΔCombo 增量、硬直 ±值 → **求和**。
- **乘法类（Multiplicative）**：`perk_multiplier`、硬直倍率 → **连乘**。
- **覆盖类（Override）**：如「Miss 伤害倍率设为 0.6」→ **last-applied-wins**（game-concept 边界规则「冲突心法以最后生效为准」）。

**C3-R7 C1 注入接口 `IRhythmModifierProvider`**（只读）
C3 实现，C1 属性注入并在计算 effective-window / 评级后果时查询：

| 查询方法 | 返回 | 种类 | C1 用途 |
|---|---|---|---|
| `GetPerfectWindowDeltaMs()` | `float` | 加法 | 加入 `_effectivePerfectMs` 运行时重算（不写 SO） |
| `GetGoodWindowDeltaMs()` | `float` | 加法 | 加入 `_effectiveGoodMs` |
| `GetComboDelta(RhythmRating)` | `int` | 加法 | 叠加至 `ΔCombo_table`；**Miss 恒 0（护栏 2）** |
| `GetDamageMultiplier()` | `float` | 乘法 | 即 `perk_multiplier` |
| `GetRatingDamageOverride(RhythmRating)` | `float?` | 覆盖 | 单维覆盖评级伤害倍率，`null`=不覆盖 |
| `GetHitstunMultiplier()` | `float` | 乘法 | Perfect 动力学硬直缩放 |

- **查询时机**：C1 在 `OnMeterChanged`（拍号/BPM 生效重锚）与 perk 栈变更（`OnPerkStackChanged`，选/弃心法时）后重算 effective-window 并缓存，重算时查询本 Provider 纳入 perk delta；**绝不写回 `RhythmConfigSO`**（遵守 D4-1 / ADR-D3）。详见 BC-2 契约（C1 `IRhythmModifierProvider` 消费点）。
- **方向**：C1 只依赖 `IRhythmModifierProvider` 抽象，不依赖 C3 具体类型——保持 C1 单向、可测试。

**C3-R8 规则五护栏强制（不可逾越）**：三条护栏 + Miss 不清零，双层强制——
1. **不合并评级**：Odin 外部校验器（CI 阻塞）拒绝「单张心法覆盖某评级全部后果维度」的 PerkSO；至少一维须保留评级差异。
2. **Miss 不增连击**：`GetComboDelta(Miss)` 恒返回 0（运行时 clamp+断言）；校验器拒绝 `OnMiss`+`ComboModifier` 正增益组合。
3. **高级取消不无条件开放**：校验器拒绝对全评级开放 `Advanced` CancelTier 的 `rhythm.cancel.*` 心法。
4. **Miss 不清零连击**：C1 固定，心法无法触及此路径。

运行时断言仅兜底（正常路径已被 CI 拦截）；触发即记 `Error` 并 clamp 回合法值。

**C3-R9 三选一抽取（`PerkRollService`）**：C3 拥有抽取逻辑，P2 拥有 UI，FE2 触发。
1. FE2 房间清场后调用 `RollPerkChoices(count = 3)`。
2. 抽取池 = 已解锁心法池（MVP 全解锁 5 个；FE5 扩展）**减去**已在 `PerkLoadout` 且已满叠的心法（**不出重复**）。
3. 按稀有度加权抽取（权重见 Formulas / Tuning），**无保底**（MVP）。
4. 若可抽池 < 3，返回不足数量（见 Edge Cases）。
5. 返回三个候选**原始 PerkSO 引用**给 P2；玩家选定后 C3 克隆并加入 `PerkLoadout`。

**C3-R10 下游数据契约**
- **P1 HUD**：`GetActivePerkStatus()` → 列表（`displayName`、当前 stack、effectType 类别图标）。
- **P2**：候选 PerkSO（`displayName/rarity/descriptionTemplate`（已用 parameters 解析）/`iconSprite`）。
- **FE5**：`ExpandUnlockedPool(perkId)` 扩展解锁池；跨局持久化需 `PlayerInfoSO.unlockedPerkIds`（F4-S7 缺此字段 → Rule-4 F4 变更，见 Open Questions）。

### States and Transitions

**PerkSO 克隆生命周期**（owner = C3）：

| 状态 | 含义 | 进入条件 | 退出条件 |
|---|---|---|---|
| `Unloaded` | 未加载 | 初始 | Addressables 加载 |
| `Loaded` | 原始资产，只读 | `Addressables.LoadAssetAsync<PerkSO>()` | 被选中 → 克隆 |
| `Cloned` | 本局实例，可写 | 玩家选定 → `Instantiate()` | 局末 |
| `Destroyed` | 已销毁 | 局末 `Destroy(clone)` | — |

**单张心法叠加状态**：`Inactive(stack=0)` → `Active(stack=1..maxStacks)`（触发 +1，`Additive`）→ `Capped(stack=maxStacks)`（`Capped` 停止触发 / `Replace` 刷新时限）。`Passive` 直接进 `Active(stack=1)`。

### Interactions with Other Systems

| 系统 | 方向 | 数据流入 | 数据流出 | 接口所有者 |
|---|---|---|---|---|
| **C1 节奏核心** | 双向（解耦） | 订阅 `OnRhythmJudgment`（触发器来源） | 实现 `IRhythmModifierProvider`（C1 查询聚合修改量）+ 增删心法时广播 `OnPerkStackChanged` 触发 C1 窗口重算 | 接口由 C3 定义，**C1 在 C1-F7 消费（BC-2 已接线）** |
| **F4 SO 数据层** | C3→F4 | `PerkSO` schema（F4-S3）、克隆规则（F4 Rule 3） | 无（只读 schema） | F4 |
| **F2 音频时钟** | 间接 + 暂停/恢复直订〔BC-6〕 | 经 C1（BPM/拍号变化触发窗口重算）；直接订阅 F2 `OnBeatClockPaused`/`OnBeatClockResumed` 冻结/续算修改器栈 | 无 | F2（暂停/恢复事件权威定义） |
| **C2 连招图** | 待对齐 | `OnComboEnd` 判定、`CancelTier` 语义 | `rhythm.cancel.*` 修改量（受护栏 3） | Open Question（与 C2 同轮对齐） |
| **FE2 房间** | FE2→C3 | `RollPerkChoices()` 触发（房间奖励） | 三候选 PerkSO | C3（单向，C3 不依赖 FE2） |
| **FE5 局外成长** | FE5→C3 | `ExpandUnlockedPool(perkId)` | 解锁池状态 | C3；持久化需 F4 变更 |
| **P1 HUD** | C3→P1 | 无 | `GetActivePerkStatus()` | C3 |
| **P2 心法选择 UI** | C3→P2 | 无 | 候选 PerkSO 数据 + 图标（P2 管理 `original.iconSprite` 加载） | 数据 C3 / 图标视觉规格 P2 |

## Formulas

### 公式 1：`perk_multiplier` 聚合（DamageModifier 心法）

`perk_multiplier = clamp( Π_i (1 + v_i × min(s_i, maxStacks_i)) , PERK_MULT_MIN, PERK_MULT_MAX )`

| 变量 | 符号 | 类型 | 范围 | 说明 |
|---|---|---|---|---|
| 单心法每层加成 | v_i | float | [0.02, 0.15] | 第 i 张 DamageModifier 心法每层 % 加成（0.05=+5%/层） |
| 当前叠加数 | s_i | int | [1, maxStacks_i] | 第 i 张心法当前 stack |
| 单心法叠加上限 | maxStacks_i | int | 按稀有度分档 | Common 5 / Uncommon 4 / Rare 3 / Legendary 2 |
| 下限/上限 | PERK_MULT_MIN/MAX | const | 0.5 / 2.0 | 硬 clamp，防数值膨胀（支柱四） |

**输出范围**：[0.5, 2.0]。MVP 现实构筑落 ~1.3–1.6×；上限主要护 Vertical Slice/Full 规模不失控。
**示例**：A（Common +5%/层，4 层）→ 1+0.05×4=1.20；B（Rare +8%/层，2 层）→ 1.16；连乘 1.20×1.16=**1.392**（未触顶）。
> `perk_multiplier` 是 game-concept 伤害公式 `final_damage = base_damage × rating_multiplier × combo_multiplier × perk_multiplier` 的第四项，C3 拥有。C2 的 final_damage 直接消费此值。
>
> **BD-1（Off 独立封顶）**：当 `QuantizationMode == Off` 时，聚合后的 `perk_multiplier` 在上表 clamp 之外**再独立 clamp 至 ≤ 1.0**（「节奏模式独占」护栏，覆盖 `PERK_MULT_MAX`），使无障碍 Off 档不获得任何净伤害增益——堵住「Off 恒 Perfect × 无限制 perk」的战力漏洞（反支柱「不存在绕过节奏的破坏性收益」）。非 Off 档沿用上表 [0.5, 2.0]。断言见 AC-C3-15(b)。

### 公式 2：判定窗拓宽聚合（RhythmRuleModifier，`rhythm.window.*`）

`perfectWindowDelta = clamp( Σ_i d_i , WINDOW_DELTA_MIN, WINDOW_DELTA_MAX )`（ms，加法类）
`effectivePerfectBase = PerfectHalfWindowMs_authored + perfectWindowDelta`

| 变量 | 范围 | 说明 |
|---|---|---|
| 单心法窗口 delta | d_i ∈ [-15, +30] | 单张窗口心法贡献（可负=收窄型高回报心法） |
| 聚合 clamp | [-20, +60] ms | C3 侧聚合硬界 |

- C3 只产出 `perfectWindowDelta`（经 `GetPerfectWindowDeltaMs()` 暴露），交由 **C1 既有 clamp 级联**（`× weapon.perfectWindowModifier` → clamp 到 `[platform_floor, min(tuning_max=100, gridIntervalMs)]`）——C3 **不重写** C1 的 clamp，也不写回 `RhythmConfigSO`。
- **自平衡特性**：高 BPM 下 `gridIntervalMs`（= 30000/bpm 的半拍值）收窄，天然限制拓宽收益。
- **示例**：BPM100，authored 50 + delta 40 = 90ms，落在 [16.7,100] 且 < 栅格 → effective 90ms。BPM180 同样 90ms 会被 C1 高 BPM 栅格 clamp 压低（精确值由 C1 级联决定）。

### 公式 3：线性叠加贡献（C3-R5 形式化）

`EffectiveContribution = v × min(s, maxStacks)`（线性）

| 变量 | 类型 | 范围 | 说明 |
|---|---|---|---|
| 每层基础值 | v | float | 由 effectType 定义 | 每层贡献（ms / % / int，取决于修改目标） |
| 当前叠加 | s | int | [1, maxStacks] | — |

**示例**：+5ms/层 Perfect 窗心法，4 层 → +20ms。DamageModifier 分档收紧、RhythmRule/Combo 可用 schema 全程 [1,10]。

### 公式 4：稀有度加权抽取（无放回，`PerkRollService`）

单槽概率：`P(perk_j) = w(rarity_j) / Σ_{k∈pool} w(rarity_k)`

| 稀有度 | 权重 w |
|---|---|
| Common | 60 |
| Uncommon | 30 |
| Rare | 8 |
| Legendary | 2 |

- **顺序无放回**：抽中一张即移出池并重归一化；池已排除「已满叠且已拥有」的心法（C3-R9，无需特判）。
- **示例**：池 5 张，稀有度 [C,C,U,R,L]，权重和 160；首抽 P(C)=60/160=37.5%（每张）、P(U)=18.75%、P(R)=5%、P(L)=1.25%；抽出后重归一化再抽第 2、3 张。
- ⚠️ **待 playtest**：MVP 仅 5 心法时 60/30/8/2 使 ~75% 抽到 Common；池扩过 MVP 后建议 lean 复检权重（非当前阻塞）。

### 公式 5：评级伤害覆盖边界（Override 类，护栏 1 硬墙）

`0.10 ≤ X_MissOverride ≤ 0.70`（严格 < Good 的 0.75）

| 变量 | 范围 | 说明 |
|---|---|---|
| Miss 伤害覆盖值 | X_MissOverride | [0.10, 0.70] | 覆盖后 Miss 伤害倍率；上限 0.70 留 0.05 余量避 float epsilon |

- 覆盖 Miss 伤害倍率的心法，X 必须 < 0.75（保留 Miss<Good 的评级质差）。
- **且**该心法**不得同时**覆盖 Miss 的连击（硬锁 0）或取消层级（CI 拦截）——满足护栏 1「至少一维保留评级差异」。

## Edge Cases

- **若可抽池 < 3**（已解锁心法不足或多数已满叠）：`RollPerkChoices` 返回实际可用数量（1 或 2），P2 展示实际张数；若为 0 则**跳过本次奖励**并记 Info 日志，**不阻塞房间前进**。
- **若 `PerkParameter.value` 越界 `[minValue, maxValue]`**：运行时 clamp 入界，记 Warning `"PerkParameter value clamped"`（F4 AC-F4-14，C3 定义为 **BLOCKING**）。
- **若 `PerkSO.id` 重复**：C3 字典后者覆盖前者，记 Error `"Duplicate perk id detected"`（F4 AC-F4-22，**BLOCKING**）。
- **若 `triggerCondition=Passive` 且 `maxStacks>1`**：运行时 clamp `maxStacks=1`，记 Warning（F4 已规定）。
- **若局末 `_activeClones` 非空**：视为克隆泄漏，记 Error 并强制销毁全部（F4 AC-F4-05，**BLOCKING**——局末该注册表必须为空）。
- **若两张 Override 心法覆盖同一评级同一维度**：**last-applied-wins**（应用顺序 = 获取顺序，game-concept 边界规则）。
- **若单张心法试图覆盖某评级全部后果维度**（违护栏 1）：Editor 校验器 CI 阻塞；万一绕过到运行时，聚合层断言失败 → 拒绝该 override、记 Error、退回评级默认值。
- **若 `OnMiss` 心法产生正 `ComboDelta`**（违护栏 2）：`GetComboDelta(Miss)` 强制返回 0，记 Error。
- **若 `rhythm.cancel.*` 心法对全评级开放 `Advanced`**（违护栏 3）：校验器拒绝；运行时忽略该修改、记 Error。
- **若玩家在连段中途获得心法**（mid-run clone）：立即加入 `PerkLoadout`，下一次 `Evaluate` 生效；**不追溯**已结算的判定。
- **若量化档位 = Off**（无障碍，恒 Perfect）〔BD-1〕：`rhythm.window.*` 拓宽无意义（Off 无窗口概念），C1 在 Off 下忽略窗口 delta。`DamageModifier`/`ComboModifier` 仍参与聚合计算，**但聚合后的 `perk_multiplier` 在 `QuantizationMode == Off` 下独立封顶 1.0**（「节奏模式独占」护栏，防 Off 战力漏洞——见公式 1 的 BD-1 注）；`ComboModifier` 的 ΔCombo 仍受 C1 既有 combo≤5.0 封顶约束。**OQ-C3-3 锁定**：Off 下心法增益计入功勋/merit 时同样受 C1 既有 `meritMultiplierOff` Off 折算三重隔离，**不额外绕过**（即 Off 封顶 1.0 后再经 Off 功勋折算，无净放大路径）。
- **若 BPM=0**（关卡配置错误）：C1 回退 120 BPM 内部时钟；C3 窗口 delta 照常经 C1 级联，无特殊处理。
- **若音频设备断开**（游戏暂停）〔BC-6〕：C3 订阅 F2 权威 `OnBeatClockPaused` 冻结 `PerkModifierStack`、不触发；收到 F2 `OnBeatClockResumed` 后续算——克隆叠加状态不受影响（暂停/恢复事件源已锁定为 F2 规范事件，废弃臆造的 `OnGamePaused/OnGameResumed`）。
- **若 Boss 拍号切换（4/4→3/4）且玩家持 `rhythm.meter.*` 心法**：心法在切换后按其定义拍数窗口生效；进行中的攻击按原拍号完成（game-concept 边界），新判定窗从下一拍起算 + perk delta。
- **若 `Additive` 心法已达 `maxStacks` 仍触发**：`Capped` 停止触发；`Additive` clamp 到顶；`Replace` 刷新时限。

## Dependencies

### 上游依赖（C3 依赖，不可缺）

| 依赖 | 类型 | 数据接口 | 状态 |
|---|---|---|---|
| **F4 SO 数据层** | 硬 | `PerkSO` schema（F4-S3）、`PerkParameter`/`PerkRarity`/`PerkEffectType`/`PerkTrigger`/`StackBehavior` 枚举、克隆规则 F4 Rule 3、Odin 双模校验 | Approved |
| **C1 节奏量化攻击** | 硬 | 订阅 `OnRhythmJudgment(RhythmJudgmentEvent{Rating,AttackType,CancelTier,ComboCountBefore,ComboCountAfter,OffsetMs})`；C3 实现 `IRhythmModifierProvider` 供 C1 查询 | **BC-2 已接线**（载体以 C1-F6、消费契约以 C1-F7 为准；C1-F4/F5 已纳入 perk delta）；Off 下聚合 `perk_multiplier` 独立封顶 1.0（**BD-1 已锁定**，见公式 1 BD-1 注） |
| **F2 音频时钟** | 软（间接）+ 暂停/恢复直订〔BC-6〕 | 经 C1；BPM/拍号变化触发 effective-window 重算；直接订阅 F2 权威 `OnBeatClockPaused`/`OnBeatClockResumed` 冻结/续算 `PerkModifierStack` | Revised-Pending |
| **game-concept 规则五** | 设计约束 | 心法设计空间三护栏 + 伤害公式 `perk_multiplier` 项 | Approved（基准） |

### 下游依赖（依赖 C3，须反向引用本文件）

| 下游 | 类型 | C3 提供的接口 | 状态 |
|---|---|---|---|
| **FE2 房间系统** | 硬 | `RollPerkChoices(count)` 心法奖励分发；单向（C3 不依赖 FE2） | Not Started |
| **FE5 局外成长** | 硬 | `ExpandUnlockedPool(perkId)` 心法池扩展；需 `PlayerInfoSO.unlockedPerkIds`（F4-S7 缺 → Rule-4 变更） | Not Started |
| **P1 HUD** | 软 | `GetActivePerkStatus()` 心法状态（displayName/stack/类别） | Not Started |
| **P2 心法选择 UI** | 硬 | 候选 PerkSO 数据；P2 拥有图标视觉规格并管理 `original.iconSprite` 加载 | Not Started |

### 待对齐（同轮修订）

- **C2 连招图**：`OnComboEnd` 触发判定、`CancelTier` 语义、`rhythm.cancel.*` 心法边界须与 C2 同轮对齐（C2 亦 Not Started，可并行）。

> **双向一致性提示**：C1/F4 GDD 修订时应在其 Dependencies 反向列出「被 C3 依赖」；FE2/FE5/P1/P2 创建时须引用本文件为设计基准。

## Tuning Knobs

| 参数 | 默认值 | 安全范围 | 过高 | 过低 | 影响 |
|---|---|---|---|---|---|
| `PERK_MULT_MAX` | 2.0 | [1.5, 3.0] | 数值膨胀压过节奏精度（违支柱四） | 伤害心法缺乏满足感 | perk_multiplier 上限 |
| `PERK_MULT_MIN` | 0.5 | [0.3, 0.8] | 收窄型心法惩罚过轻 | 负向心法过度惩罚 | perk_multiplier 下限 |
| `WINDOW_DELTA_MAX` | +60 ms | [+30, +80] | 判定过宽，踩拍精度贬值 | 窗口心法收益不明显 | 判定窗拓宽聚合上限 |
| `WINDOW_DELTA_MIN` | -20 ms | [-40, -10] | — | 收窄型高回报心法难成立 | 判定窗收窄聚合下限 |
| 稀有度权重 | 60/30/8/2 | 见下 | Legendary 泛滥失去稀有感 | Rare+ 永不出现 | 三选一构筑天花板 |
| DamageModifier maxStacks | 5/4/3/2（C/U/R/L） | [1,10] per 档 | %叠加压过规则型心法 | 叠加心法乏味 | %货币强度（刻意弱于规则型） |
| 心法三选一刷新池大小 | 见规模档 | [15, 60] | 构筑方向过散、稀释主题 | 重复感强 | 构筑多样性 vs 重复（game-concept） |
| 每次奖励候选数 | 3 | [2, 4] | 选择瘫痪 | 构筑自由度不足 | 三选一的「三」 |
| 单 DamageModifier 每层 v | — | [0.02, 0.15] | 单张过强 | 存在感弱 | 单心法伤害贡献粒度 |
| 单窗口心法 delta d | — | [-15, +30] | 单张压过 C1 clamp | 无感 | 单心法窗口贡献粒度 |
| X_MissOverride 上限 | 0.70 | [0.60, 0.74] | 逼近 Good，评级质差消失（护栏1红线） | Miss 覆盖心法无意义 | Miss 减伤心法的天花板 |

**稀有度权重按规模档**（权重比例固定 60/30/8/2，池大小随里程碑增长）：MVP 池 5（以 Common/Uncommon 为主）；Vertical Slice 10+；Full 50+。⚠️ 池扩后建议 lean 复检权重（Formulas 公式 4 已标）。

**交互警告**：`PERK_MULT_MAX` 与 DamageModifier `maxStacks` 分档相乘——同时调高两者会指数放大 %货币，破坏「规则型 > 数值型」的支柱四意图。调其一即可。

## Visual/Audio Requirements

- **心法获得瞬间**：三选一卡面浮现，选中卡确认反馈用**金 #FFD700**（Perfect/奖励语义色）脉冲，随 BPM 呼吸（国朝墨彩三色系统 + 节拍驱动视效原则）。
- **心法触发反馈**（`OnPerfect`/`OnGood`/...）：经 **PL2 VFX/节奏反馈系统**播放——C3 发布触发事件，Feel/MMFeedbacks 订阅播放（边界隔离，C3 **不** `using` Feel 命名空间）。
- **RhythmRuleModifier 生效**（如判定窗拓宽）须有**可感知视觉暗示**（HUD 判定指示器变化），避免「规则已变但玩家不知」。
- **音频**：Rare/Legendary 心法获得时叠加特殊音效层（编钟共鸣，呼应 Perfect 动力学音景）。
- **详细规格延后**：VFX 触发规格 → PL2 GDD；卡面/图标视觉规格 → P2 GDD + `art-bible.md`。

> 📌 **Asset Spec** — Visual/Audio 要求已定义。art bible 批准后，运行 `/asset-spec system:perk-system` 生成逐资产视觉描述与生成提示。

## UI Requirements

C3 **本身不拥有 UI**，只向 UI 层提供数据。两个 UI 触点：
- **P2 心法选择 UI**：展示 3 候选（`displayName` / `rarity` 色 / `descriptionTemplate` 解析文本 / `iconSprite`）；须支持 d-pad 导航 + 触摸等价（跨平台约束）。P2 拥有卡面视觉规格并管理 `original.iconSprite` 加载/释放。
- **P1 HUD 心法状态**：`GetActivePerkStatus()` → 激活心法列表（类别图标 + 当前 stack）。

> 📌 **UX Flag — 心法构筑系统**：本系统有 UI 需求（P2 选择界面、P1 HUD 心法区）。Phase 4（Pre-Production）应先运行 `/ux-design` 为 P2、P1 心法区各出 UX spec，再写 epics；引用 UI 的 stories 须引 `design/ux/[screen].md` 而非本 GDD。

## Acceptance Criteria

> 每条独立可验证；BLOCKING = 自动化单测（逻辑），ADVISORY = 集成/playtest。

**AC-C3-01（BLOCKING）加法聚合 = SUM**：GIVEN 两张 `rhythm.window.perfect.delta` 克隆 A(v=5ms,s=4→20ms)、B(v=10ms,s=1→10ms)，WHEN 查询 `GetPerfectWindowDeltaMs()`，THEN 返回 30ms（非乘/max）；GIVEN 第三张使 Σ=61ms，THEN clamp 至 60（`WINDOW_DELTA_MAX`）。〔R6/F2/F3〕

**AC-C3-02（BLOCKING）乘法聚合 = PRODUCT + clamp**：GIVEN Perk-A(Common,v=0.05,s=4→1.20)、Perk-B(Rare,v=0.08,s=2→1.16)，WHEN 查询 `GetDamageMultiplier()`，THEN `perk_multiplier == 1.392 ± 0.001`；GIVEN 再顺序叠加两张 Legendary(各 1.30)使原始积 →2.352，THEN 最终返回 `2.0`（`PERK_MULT_MAX` clamp），绝不 2.352。〔R6/F1/F3〕

**AC-C3-03（BLOCKING）覆盖 = last-applied-wins（获取顺序）**：GIVEN Perk-A(Override Miss X=0.20, Room2) 后 Perk-B(同目标 X=0.55, Room5)，WHEN 查询 `GetRatingDamageOverride(Miss)`，THEN 返回 0.55；GIVEN 获取顺序反转，THEN 返回 0.20。〔R6〕

**AC-C3-04（BLOCKING）F5 Miss 覆盖 clamp + 护栏1 联合强制**：GIVEN 作者 X=0.05，THEN clamp 0.10；GIVEN X=0.85，THEN clamp 0.70；GIVEN 单张 PerkSO 同时改 Miss 伤害覆盖 + 正 Miss ComboDelta + 对 Miss 开放 `rhythm.cancel.*` Advanced（违「至少一维保留评级差异」），WHEN Odin CI 校验，THEN Error 阻塞；GIVEN 绕过至运行时，THEN `GetComboDelta(Miss)==0`（硬 clamp）且 override 被拒退回 `null`。〔F5/R8(1)(2)〕

**AC-C3-05（BLOCKING）护栏2 `GetComboDelta(Miss)` 硬锁 0**：GIVEN 强注 `OnMiss` ComboModifier 克隆(v=+5,s=3) 绕过 CI，WHEN 查询 `GetComboDelta(Miss)`，THEN 返回 0 并记 Error；GIVEN 同克隆经 `GetComboDelta(Perfect)` 查询，THEN 亦返回 0（因触发-评级不匹配 R4，与硬锁独立可验）。〔R8(2)/R4/R7〕

**AC-C3-06（BLOCKING，部分 DEFERRED）护栏3 `rhythm.cancel.*` 不无条件开放**：GIVEN 对全评级授 Advanced 的 PerkSO，WHEN Odin CI 校验，THEN Error 阻塞；GIVEN 绕过运行时，THEN 忽略该修改、记 Error、Miss 保留默认 CancelTier〔**运行时半段 DEFERRED：待 C2 CancelTier 消费契约对齐（OQ-C3-2）**〕。〔R8(3)〕

**AC-C3-07（BLOCKING）克隆生命周期 + 状态查询（交叉 F4 AC-F4-05）**：GIVEN `_activeClones` 空，WHEN 玩家选定候选，THEN `Instantiate()` 调用一次、Count+1、克隆非 `ReferenceEquals` 原始、`clone.id==original.id`；GIVEN 局末 3 克隆，WHEN 局末钩子触发，THEN 全部 `Destroy` 且 `_activeClones.Count==0`；GIVEN 模拟泄漏，THEN 强制清理记 Error 并清空；GIVEN 2 活跃克隆，WHEN `GetActivePerkStatus()`，THEN 返回 2 条正确 displayName/stack/effectType（仅数据形状，P1 负责渲染）。〔R1(1-3)/R10/F4 AC-F4-05〕

**AC-C3-08（BLOCKING）iconSprite 禁释放/加载**：GIVEN 原始持句柄 H，WHEN `Instantiate()` 克隆，THEN `ReferenceEquals(clone.iconSprite, original.iconSprite)==true`；GIVEN 克隆局末销毁，THEN 其生命周期内对该句柄 0 次 `ReleaseAsset()`/`LoadAssetAsync()`（mock `IAssetReleaseTracker` 调用计数==0）。〔R1(4)〕

**AC-C3-09（BLOCKING）PerkParameter 值类型 + 枚举锁定/路由**：GIVEN MVP 5 资产，WHEN 反射每个 `PerkParameter.value`，THEN 均满足 `IsValueType || typeof(string)`（F4 D4-9）；GIVEN 反射 `PerkEffectType`，THEN 恰 5 值锁定集，加第 6 值无 ADR 引用则 CI 失败；GIVEN key=`rhythm.window.perfect.delta` 克隆，WHEN `PerkEffectRouter` 路由，THEN 仅落窗口 handler（经 `GetPerfectWindowDeltaMs()` 验证），不泄漏至 cancel/meter handler。〔R1(5)/R3〕

**AC-C3-10（BLOCKING）F2 窗口 delta clamp + 不回写契约**：GIVEN 两克隆 d=25、d=15（Σ=40），THEN `GetPerfectWindowDeltaMs()==40`，C1 `effectivePerfectBase`=50+40=90ms；GIVEN Σ=75，THEN clamp 60；GIVEN Σ=-25，THEN clamp -20；GIVEN 以上全部，THEN `RhythmConfigSO` 序列化字段前后字节相同（快照比对）——C3 从不回写（R7/D4-1/D3）。〔R7/F2/F3〕

**AC-C3-11（BLOCKING）R4 触发门控**：GIVEN Passive 克隆（maxStacks 强制 1），WHEN 任何 `OnRhythmJudgment` 前查询 `GetDamageMultiplier()`，THEN 其贡献已计入（无条件）；GIVEN `OnPerfect` ComboModifier 克隆(v=3,s=1)，WHEN C1 触发 `OnRhythmJudgment(Rating=Good)`，THEN `GetComboDelta(Good)` 不含它；WHEN 触发 `Rating=Perfect`，THEN `GetComboDelta(Perfect)` 含它(=3)。〔R4〕

**AC-C3-12（BLOCKING）R5 三叠加行为**：(a) Additive v=0.05,maxStacks=10,s=3 → `EffectiveContribution=0.15`；(b) Capped v=0.08,Rare(maxStacks=3)，s=3 后第 4 次触发 no-op → 贡献留 0.24；(c) Replace Rank1 d=+10ms/30s，到期前 Rank2 重选 d=+25ms → 贡献变 25ms（非 35、非 s=2）且计时刷新 30s。〔R5/F3〕

**AC-C3-13（BLOCKING，gap 标记）F4 加权无放回抽取**：GIVEN 池 [C,C,U,R,L] 权重和 160，WHEN 抽槽 1，THEN P(每 C)=37.5%、P(U)=18.75%、P(R)=5%、P(L)=1.25%；GIVEN 槽 1 移除一 Common，WHEN 抽槽 2，THEN 分母重归一化为剩余 4（和=100）；THEN 三候选 id 两两互异。〔**gap：`PerkRollService` 无可注入随机源，依 coding-standards 确定性规则无法成真单测，见 OQ-C3-6**〕〔F4/R9〕

**AC-C3-14（BLOCKING）R9 排除/无保底/池<3**：GIVEN Perk-A 已满叠、Perk-B 已拥有未满叠（5 资产），WHEN `RollPerkChoices(3)`，THEN 池仅排除 A、结果无 A、无重复 id、可含 B；GIVEN 池减至 2，THEN 返回恰 2（不填充）；GIVEN 池=0，THEN 空结果、Info 日志、房间前进不阻塞；GIVEN 注入永不越 Legendary 带的 RNG stub 连抽 20 次，THEN 0 Legendary（无保底）。〔R9〕

**AC-C3-15（(a) ADVISORY 集成 / (b) BLOCKING 单测）mid-run 非追溯 + Off perk 封顶**：(a) GIVEN 已结算判定 + 连段中途获心法，THEN 新克隆仅影响下一次 `Evaluate`，不追溯重算已结算判定；**(b)〔BD-1，可判定断言，替代原阻塞桩〕** GIVEN 量化=Off 且活跃 DamageModifier 克隆使非 Off 档 `perk_multiplier` 本应 = 1.6，WHEN 查询 `GetDamageMultiplier()`，THEN Off 封顶生效返回 **`perk_multiplier ≤ 1.0`**（精确 == 1.0）；且 `GetPerfectWindowDeltaMs()=40ms` 的窗口 delta 被 C1 在 Off 下完全忽略；计入功勋时受 `meritMultiplierOff` Off 折算、无额外绕过（OQ-C3-3 锁定）。〔R6/F1/BD-1〕

**AC-C3-16（ADVISORY，集成）暂停冻结**〔BC-6〕：GIVEN 2 活跃克隆带进行中计时器，WHEN F2 广播 `OnBeatClockPaused`，THEN `PerkModifierStack` 冻结——暂停窗内注入的判定事件被证明忽略、不入队、不在恢复时双计；WHEN F2 广播 `OnBeatClockResumed`，THEN stack 数/计时器与暂停前一致。〔事件源已锁定为 F2 权威 `OnBeatClockPaused`/`OnBeatClockResumed`，OQ-C3-7 已解阻〕

## Open Questions

| # | 问题 | Owner | 目标解决 |
|---|---|---|---|
| OQ-C3-1 | `PlayerInfoSO.unlockedPerkIds` 缺失（F4-S7）→ FE5 心法池扩展需此字段，属 **Rule-4 F4 schema 变更** | F4 / technical-director | 先于 FE5 设计 |
| OQ-C3-2 | `OnComboEnd` 触发精确定义（连段结束 vs 连击归零判定）须与 C2 同轮对齐 | C3 + C2 | C2 设计同轮 |
| OQ-C3-3 | ~~Off 量化档下心法数值增益是否计入功勋/merit~~ **已锁定〔BD-1〕**：Off 下聚合 `perk_multiplier` 独立封顶 1.0；计入功勋时同样受 C1 `meritMultiplierOff` Off 折算三重隔离、不额外绕过（见公式 1 BD-1 注 + Edge Case「Off」条 + AC-C3-15(b)） | RESOLVED（BD-1 owner 决策） | 已随 2026-08-10 阻塞修订锁定 |
| OQ-C3-4 | `RhythmRuleSubtype` 是否升为 PerkSO 一等公民枚举字段（当前用 `PerkParameter.key` 命名空间）——升级则 Rule-4 F4 变更 | C3 + F4 | Vertical Slice 前评估 |
| OQ-C3-5 | `PerkEffectType` 最终锁定确认（本 GDD 锁定 5 值）——ADR-D4 gating，须 architecture-review 确认后解阻 C3 实现冲刺 | architecture-review | 实现冲刺前 |
| OQ-C3-6 | `PerkRollService` 无可注入随机源接口（如 `IRandomSource`）——依 coding-standards 确定性规则，AC-C3-13 加权抽取无法成真单测；同 C1 `ITimeProvider` 缺陷类 | C3 + architecture | 实现冲刺前（须先于 C3 单测） |
| OQ-C3-7 | ~~暂停/恢复事件源未命名（哪个系统发 `OnGamePaused`/`OnGameResumed`）~~ **已锁定〔BC-6〕**：C3 订阅 F2 权威 `OnBeatClockPaused`/`OnBeatClockResumed`（携恢复首拍 raw DSP 时间）——解阻 AC-C3-16 自动化测试 | RESOLVED（F2/C1/C3 同轮） | 已随 2026-08-10 阻塞修订锁定 |
