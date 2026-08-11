# Cross-GDD Review Report

> **Date**: 2026-08-10
> **Skill**: `/review-all-gdds` (full — consistency + design theory + scenario walkthrough)
> **GDDs Reviewed**: 9 — F2 audio-beat-clock, F3 input-event-system, F4 so-data-layer, F5 save-persistence, F6 tde-integration, C1 rhythm-core, **C2 combo-system（本轮新增）**, **C3 perk-system（本轮新增）**, **C4 combat-director（本轮新增）**
> **Consistency baseline**: `design/registry/entities.yaml`（6 常量 + 2 公式：combo_multiplier / perk_multiplier / combo_max=999 等）
> **Method**: 三个并行子代理独立执行 Phase 2（一致性）/ Phase 3（设计整体性）/ Phase 4（场景走查），整合后由主线独立复核关键阻塞项（grep 核实）
> **Prior review**: `gdd-cross-review-2026-08-09.md`（FAIL）
> **Verdict**: **FAIL** — 新增 C2/C3/C4 与其上游数据/评级契约存在多处实质断裂（BC-1~BC-6），叠加 Off 战力漏洞（BD-1）与 BPM 三系统尖峰（BD-2）。

---

## 上次阻塞项回归结果

| 项 | 现状 | 依据 |
|---|---|---|
| **B1** C1 悬空 `OnBpmChanged` | ✅ **已解决** | C1 全程改用 `OnMeterChanged`（`rhythm-core.md:354,398,400`），review-log 记录 grep 零残留；主线复核确认 |
| **C4 是否犯 B1** | ✅ **未犯（正例）** | C4 正确用 `OnBeatLookahead`+`OnMeterChanged`（`combat-director.md:73,267`），F2 `:107,109` 已反向登记 C4 |
| **B2** F2↔F5 初始化时序 | ✅ 已由 ADR-D10 解决（前次确认，未复回归） | — |
| **`OnBeatClockResumed`** | 🔴 **仍缺失** | F2 有 `Resume()` 方法（`:93`）但无同名事件；C1 `:120-121` 订阅它、C3 冻结栈无触发源（OQ-5 延后） |
| **B3** `ScheduleBGMTransition` | 🔴 **仍缺失** | F6 `:157` 调用，F6 `:159` 自认「F2 API 缺口 W-2c-01，本轮未解决」；F2 全文无此方法 |

---

## 一、一致性 Issues（Phase 2）

### 🔴 Blocking

- **BC-1 Perfect 伤害倍率三方矛盾（1.0 vs 1.5）** — C2 / C1 / game-concept。
  C2（`combo-system.md:118-126`）用 `rating_multiplier` Perfect=**1.5** 计算全部 final_damage 示例（如 `25×1.5×5.0×2.0=375.0`），并明文将其当作「Boss/精英 HP 曲线压力基准」；C1（`rhythm-core.md:15,129`）与 game-concept（`:419`，值域 [0.5,1.0]）权威值为 **1.0**。错误数字驱动 C2 复合上限，污染整条下游平衡曲线。
  **修复**：C2 F2 全节将 Perfect `rating_multiplier` 改回 1.0，重算示例（37.5/75/…）与复合上限（`25×1.0×5.0×2.0=250`）。

- **BC-2 C3 `IRhythmModifierProvider` 单向死契约（本轮最严重）** — C3 / C1。
  C3 整个 RhythmRuleModifier / ComboModifier 机制依赖「C1 属性注入 `IRhythmModifierProvider` 并在 effective-window / ΔCombo 计算时查询」（C3-R7 `perk-system.md:68-81`，公式 2 / AC-C3-10 `:146-158,301`）。但 **C1 通篇零处提及 C3 / `IRhythmModifierProvider` / perk delta**：C1 Interactions 表（`rhythm-core.md:180-189`）与上游依赖（`:573-578`）均无 C3；C1-F5 窗口重算（`:368-382`）无 perk delta 项；C1-F4 ΔCombo 表（`:323-334`）固定 +2/+1/0 不叠加 `GetComboDelta`。后果：玩家选「Perfect 后拓宽下一击窗」「Good 也进连击」类心法 → C1 从不查询 C3 → **心法运行时零效果**，直接击穿支柱四「构筑驱动复盘」。附带：即便接线，C3-R7 要求「每次 Evaluate 查询」与 C1「仅 `OnMeterChanged` 重算并缓存」的节奏不一致。
  **修复**：C1 修订须（a）定义 `IRhythmModifierProvider` 契约、（b）在 C1-F3/F5 窗口重算纳入 perk delta、（c）Dependencies 反向列出 C3。或 C3 降级为「等待 C1 接口定案」的阻塞待办。

- **BC-3 C4 读取 F4 未定义的 `EnemyInfoSO` 字段** — C4 / F4。
  C4（`combat-director.md:76,268,303-304`）读取 `TimeoutBeats`、`CapacityTable[meter]`、`D_near/D_mid/W_near/W_mid/W_far`、`subdivisionMode`。F4-S5 EnemyInfoSO schema（`so-data-layer.md:223-233`）**只定义** id/displayName/baseHealth/baseDamage/behaviorProfile/attackWindupBeats/beatPhaseAnchor/rewardMeritBase——上述字段**全部不存在**；且 `CapacityTable` 是 per-拍号数据，语义不属 EnemyInfoSO。违反 F4 规则 4（schema 变更须触发下游合约更新）。
  **修复**：F4 新增 EnemyInfoSO 字段（TimeoutBeats、威胁权重）并为 CapacityTable/subdivisionMode 指定承载 SO（LevelConfigSO 或新建 CombatDirectorConfigSO），双向登记；C4 明确每个 Tuning Knob 的宿主 SO。

- **BC-4 非法拍号** — C4 / C3 / F2 / F4。
  F4（`so-data-layer.md:244-245`）：分子 ∈ {3,4,5}、分母 ∈ {4,8}（5 分子必配 8）→ 合法拍号仅 3/4、4/4、5/8；F2 Boss 序列（`:367`）明定 4/4→3/4→5/8。C4（`combat-director.md:93-104,349,351,399`）反复用 **6/8**（分子 6，非法）；C3（`perk-system.md:33,212`）用 **3/8**（与 F2 权威 3/4 矛盾）。
  **修复**：C4 示例改用 5/8（或其他合法拍号）；C3 的 3/8 改为 3/4 或 5/8。

- **BC-5 `AttackInputType` vs `AttackType` 枚举不匹配（不可编译）** — C2 / F4 / C1。
  F4（`so-data-layer.md:174`）`ComboEdge.requiredInput : AttackInputType {Normal, Heavy, Special}`；C2（`combo-system.md:41,245`）以 `requiredInput == AttackType` 匹配，而 `AttackType`（透传自 F3/C1，`rhythm-core.md:413`）为 {LightAttack, HeavyAttack, ChargedAttack}。取值集不同、无映射，直接比较不可编译。
  **修复**：明确 `AttackInputType ↔ AttackType` 映射函数，或统一枚举。

- **BC-6 `OnBeatClockResumed` 缺失 + 暂停/恢复三系统信号机制不一致** — F2 / C1 / C3 / F6。
  F6 `TDELifecycleAdapter` 以**方法调用** `Pause()/Resume()` 驱动（`tde-integration.md:50-51`，AC-F6-08/09）；C1 Rule 8 订阅**事件** `OnBeatClockResumed`（`rhythm-core.md:120-121`，F2 未定义）；C3 冻结栈依赖未命名的 `OnGamePaused/OnGameResumed`（C3 OQ-C3-7 `perk-system.md:325` 自认阻塞 AC-C3-16）。无任何系统广播三方都能消费的暂停/恢复事件。
  **修复**：F2 补 `OnBeatClockPaused`/`OnBeatClockResumed` 事件（携恢复首拍 raw DSP 时间）+ `IAudioPlayer.Pause()/Resume()`；C1/C3 统一订阅。随 OQ-5 一并处理。

### ⚠️ Warning

| # | 问题 | 涉及文档 | 摘要 |
|---|------|---------|------|
| BC-7 | ComboMax 债务持续 | C1/C2/registry | C1 clamp 50（`rhythm-core.md:342,610,673`）vs C2/registry 999。运行时 combo_multiplier 被截到 ×2.09 而非 ×5.0。C2 OQ1（`combo-system.md:313`）已记账。**可立即修**：C1 clamp 50→999。 |
| W-01 | Animancer 攻击播放调用方 | C1/C2/F6 | C2 成文后确认调用方=C2（与 F6 `PlayAttack(node,clip,onComplete)` 一致），**C1 为唯一异常方**（`rhythm-core.md:187` 仍声称自己调 `Play(attackType,beatDspTime)`）。C1 删除/改写该接口声明。 |
| W-03 | C3 陈旧 `OnBpmChanged` | C3/F2 | `perk-system.md:80` 复现 B1 同类悬空引用，应改 `OnMeterChanged`。 |
| W-04 | C3 杜撰事件名 `OnRhythmJudgment` | C3/C2/C1 | C3（`perk-system.md:58,120,222`）订阅 `OnRhythmJudgment`，C2 只称 `RhythmJudgmentEvent`，C1 未命名广播委托 → 三方各说各话。C1 规范化事件名。 |
| W-05 | `perfectWindowModifier` 死契约扩散 | C1/F4/C3 | C1 仍未消费 F4 该字段（`so-data-layer.md:217,346`），C3 却已在其上叠加设计（`perk-system.md:156`）。与 BC-2 一并解决级联顺序。 |
| W-06 | `IDSPClock` 方法名不统一 | F2/F3 | F2 `GetCurrentDSPTime`/`GetCurrentTime`（`:111,317`）vs F3 `GetDspTime`（`:41`）。 |
| W-07 | F2/F4 反向登记缺失 | F2/C2/C4/F4 | F2 Interactions 表（`:104-114`）无 C2 行；F4 下游表（`:316`）无 C4 行。C4 `:267` todo 实际已被 F2 满足（认知过时）。 |

### ℹ️ 遗留（非本轮 3 文档新增，仍开放）
- **B3** `ScheduleBGMTransition`（F6 `:157`，F2 未定义）；**W1** F6 `BgmAssetReference`(Addressables) vs F2/F4 FMOD string；**W9** F6 `:291,308` 陈旧 `OnBeat`（且与 F6 `:201` 自身正确的 `OnMeterChangeAccepted` 描述自相矛盾）。

---

## 二、游戏设计 Issues（Phase 3）

### 🔴 Blocking

- **BD-1（3c-1）Off 模式升级为战力主导策略（C3 加入放大而非缓解）** — C1 / C2 / C3。
  Off 合约（`rhythm-core.md:53-58`）原有三重隔离（排行/成就、连招深度封顶 Standard、功勋折算），但 C3 心法在 Off 下**无任何额外隔离**：`perk-system.md:209` 明写「Off 下 DamageModifier/ComboModifier 仍生效」，OQ-C3-3（`:321`）「是否计入功勋须对齐 Off 隔离契约」至今未决，AC-C3-15(b)（`:311`）被作者标注「暂作阻塞桩不可判 pass/fail」。三文档叠加：Off 玩家 `rating=1.0`（强制 Perfect）× `combo≤5.0`（ΔCombo 正常 +2）× `perk≤2.0`（无限制）= 伤害天花板与踩准玩家几乎相同，仅少 Advanced 分支。已非「功勋漏洞」而是**战力漏洞**，击穿反支柱「不存在绕过节奏的破坏性技能」与支柱一。`meritMultiplierOff`（C1 Q3）与 OQ-C3-3 互相引用对方「待决」= 循环阻塞。
  **修复方向（择一并显式锁定）**：① 为 perk_multiplier 在 Off 下设独立封顶（如 1.0，纳入「节奏模式独占」护栏）；② 明确「无障碍优先于平衡」为刻意决定并写入 game-concept 无障碍章节。**须先于 C5/FE5 成文，且 C1 Q3 与 OQ-C3-3 同批锁定。**

- **BD-2（3e-1）BPM 跃升三系统难度尖峰（Flow 反模式）** — C1 / C2 / C4。
  Boss 转阶段单一 `OnMeterChanged` 事件同时触发三系统同向变难，且无任一公式含对其他系统难度贡献的补偿：① C1 判定窗随 BPM 收窄（`rhythm-core.md:259-316`，BPM≥124 即钳位）；② C2 取消窗归一化时间不变但绝对毫秒缩短（`combo-system.md:134`）；③ C4 `SlotCapacity` 只按拍号不含 BPM，真实攻击时间密度随 BPM 线性上升（`combat-director.md:87`）。三重压力叠加于玩家犯错代价最大的时刻，与 Csikszentmihalyi 心流「锯齿形张力-释放」相悖。
  **修复方向**：① CapacityTable 按「每秒令牌速率」归一化；② `OnMeterChanged` 后引入数拍「适应窗口」宽容期；③ 明确为刻意戏剧张力点并在三文档 Formulas 互相引用 + 定义该时刻专属失败代价保护。**须在 FE4 Boss GDD 启动前由 C1/C2/C4 owner 联合锁定。**

### ⚠️ Warning

- **BD-3（3d-1）连击计数只增不减 + 早熟封顶** — C1/C2。C1 全文无 `CurrentCombo` 清零/衰减路径（Miss 不清零 `rhythm-core.md:133`，C2 软重置只作用图位置 `combo-system.md:56`）；combo_multiplier 在 count=148（约 74 次 Perfect，清两三房即达）锁死 5.0（`combo-system.md:108-112,130`）→ 连击沦为一次性早期解锁而非贯穿全局的持续技巧回报。**修复**：定义清零/衰减触发（受击/换房/超时衰减），或改对数曲线永不真封顶。
- **BD-4（3c-2）数值心法碾压规则心法** — C3。单张 Common DamageModifier 叠满即 (1+0.15)^5≈2.01 打满 PERK_MULT_MAX=2.0（`perk-system.md:137-140`）；RhythmRuleModifier 无法直接贡献 perk_multiplier → 与支柱四「规则型>数值型」意图结构性张力（C3 Tuning `:259` 已自我预警）。**建议**：MVP 首批数值心法 v 落中低段（0.05-0.08），或 P2 UI 展示规则心法「等效伤害贡献」。非硬阻塞。
- **BD-5（3b-1 复核）注意力预算** — C1/C2/C3/C4。C3 确认被动自动生效（`perk-system.md:56-58`，未加剧过载，好消息），但核心循环仍 4 层并发（C1 踩拍 + C2 分支决策 + C4 听拍防御 + 走位/闪避），逼近过载阈值（>4）。**建议** Vertical Slice 真人测试 + 知会 ux-designer 核 P1 HUD 视觉负荷。

### ℹ️ 正面确认
- **3g** 四系统 Player Fantasy 高度一致：C1「每刀是音符」+ C2「行云流水武学宗师」+ C3「为本局谱节奏武学」+ C4「战场有拍子、危险可听见」→ 单一身份「武学=音乐演奏」。
- **3a** 无资源发放权冲突：C1 不发功勋（`:57`）、C2 只读段数（`:60,78`）、C3 只供 perk_multiplier、C4 不触玩家资源。
- **防 combo² 护栏成立**：perk_multiplier 输入不含 count/combo_multiplier 的护栏在 C2 F6（`:152`）、C3（`:144`）、registry 三处一致；ComboModifier 与 DamageModifier 为不同 effectType，无复合爆炸、无奖励双计（但受 BC-2 影响 ComboModifier 目前同样无 C1 消费点）。

---

## 三、跨系统场景 Issues（Phase 4）

走查 7 场景：A Perfect 踩拍推进连招 / B 暂停恢复再攻击 / C 心法改规则+踩拍 / D Boss 拍号切换正在连招 / E 连招上限 clamp 债务 / F 音频断开降级 / G 死亡复活幽灵评级。除印证 BC-1/BC-2/BC-6/BC-7/BD-2 外，新增：

### 🔴 Blockers
- **SC-1 音频设备断开降级无承接者** — F2/F6/F3/C1。F2:55 降级序列要求「1 帧内暂停游戏」，但 F2 无权暂停 TDE/禁用 F3（那是 F6 `TDELifecycleAdapter` 独家职责 `tde-integration.md:50-51`），F6 只订阅 `TopDownEngineEvent`+`sceneLoaded`、**不订阅 `OnAudioUnavailable`**。→ 设备断开后 F2 自身 Paused 停拍，但 TDE 仍跑（可移动）、F3 仍 GameplayActive（攻击照发）、C1 收攻击无拍点静默丢弃 → 战场半暂停半运行，与「无音频不惩罚玩家」意图相悖。**修复**：指派 F6/降级协调器订阅 `OnAudioUnavailable` 同帧 `TDE.Pause() + F3.DisableAllInput()`。
- **SC-2 复活「幽灵评级」** — F6/C1。C1 缓冲清空只在「游戏暂停」触发（Rule 8 `rhythm-core.md:119,173`）；F6 死亡序列（EC-04 `tde-integration.md:247-253`）不清 C1 `_pendingInput`，C1 不订阅 PlayerDeath/RespawnComplete → 复活 `Play()` 后首个 `OnHalfBeatLookahead` 用新拍网格评级死前陈旧输入 → 幽灵评级/连招推进。**修复**：C1 订阅 PlayerDeath/RespawnComplete 复用 Rule 8 清空，补 States 表 Death 触发列。

### ⚠️ Warnings
- **SC-3 accept/boundary 发散窗** — F2/C2/C1/F6/C4。C2 在 `OnMeterChangeAccepted`（接受时）立即 `UpdateBpm()` 变速（`combo-system.md:166,291`），C1 在 `OnMeterChanged`（生效时）才重锚网格（`rhythm-core.md:400`）。中间最长一整小节：动画按新 BPM、拍点/判定网格仍旧 BPM → 三者失步，违反 ADR-D5「动画对齐拍网格」。虽是 F2 有意的「骤跳预告体感」，但四系统响应时刻（C2/F6-BGM 在 accept，C1/C4 在 boundary）未做一致性协调。
- **SC-4 Boss BPM 切换丢弃充能缓冲** — F2/C1。C1-F5 `OnMeterChanged` 无条件 `_pendingInput=null`（`rhythm-core.md:393`），无 partial credit → 玩家按键零响应，恰在 Boss 高压时刻，破坏支柱二「每一按键有分量」。

### ℹ️ Info
- **敌我同强拍共振** — C4 令牌落拍与 C1 玩家判定同为 F2 已调整时域消费者，数据流无冲突；强拍 Perfect + 敌人落拍的音频/预读清晰度需 playtest 验证（非契约问题）。
- **5/8 拍号缺 CapacityTable** — C4 未给 3/4、5/8 容量表，Boss 阶段编排落地时缺配置（并入 BC-4 修复）。

---

## 四、GDDs Flagged for Revision

| GDD | 主要原因 | 类型 | 优先级 |
|-----|---------|------|--------|
| **rhythm-core.md (C1)** | BC-2 定义 IRhythmModifierProvider；BC-6 OnBeatClockResumed；BC-7 clamp 50→999；W-01 Animancer 异常方；W-05 perfectWindowModifier；BD-1 Off 战力隔离；BD-3 连击清零；SC-2 复活清空 | 一致性+设计 | **Blocking** |
| **combo-system.md (C2)** | BC-1 rating 1.5→1.0；BC-5 枚举映射；BC-7 ComboMax 对齐；W-01；W-04；BD-3；SC-3 | 一致性+设计 | **Blocking** |
| **perk-system.md (C3)** | BC-2 死契约；BC-4 3/8 非法；W-03 OnBpmChanged；W-04 OnRhythmJudgment；BD-1 Off；BD-4 数值碾压 | 一致性+设计 | **Blocking** |
| **combat-director.md (C4)** | BC-3 EnemyInfoSO 缺字段；BC-4 6/8 非法；BD-2 BPM 尖峰；W-07 删除过时 todo | 一致性+设计 | **Blocking** |
| **so-data-layer.md (F4)** | BC-3 补 EnemyInfoSO 字段 + 指定 CapacityTable 宿主 SO；BC-5 枚举映射；W-05；W-07 补 C4 下游行 | 一致性 | High |
| **audio-beat-clock.md (F2)** | BC-6 OnBeatClockResumed + IAudioPlayer.Pause/Resume；B3 ScheduleBGMTransition；W-06 方法名；W-07 补 C2 行；SC-1 | 一致性 | High |
| **tde-integration.md (F6)** | B3；W1 BGM 类型 FMOD string；W9 OnBeat；SC-1 订阅 OnAudioUnavailable | 一致性 | Medium |

> **本轮 systems-index 更新**：将 C1 rhythm-core、C3 perk-system 标为 Needs Revision（C2/C4 已是）。F4/F2/F6 的残余项在各自 review log 跟进，索引状态维持 Pending Re-review / Approved。

---

## Verdict: **FAIL**

架构生成前必须解决：一致性 6 项（BC-1~BC-6）+ 设计 2 项（BD-1、BD-2）+ 场景 2 项（SC-1、SC-2）。

**最威胁核心的三项**：
1. **BC-2** — C3 心法整体无 C1 消费点，核心卖点（改规则的心法）运行时零效果，击穿支柱四。
2. **BC-1** — Perfect 倍率 1.5 越界并被当作 Boss HP 基准，污染整条平衡曲线。
3. **BD-1** — Off 从功勋漏洞升级为战力漏洞，击穿反支柱。

### 可立即修（与 ADR 无关的机械修订）
- BC-1：C2 Perfect `rating_multiplier` 1.5→1.0，重算示例与复合上限。
- BC-4：C4 `6/8`→`5/8`，C3 `3/8`→`3/4`/`5/8`。
- BC-7：C1 `ComboMax` clamp 50→999。
- W-03/W-04：C3 事件名 `OnBpmChanged`→`OnMeterChanged`、`OnRhythmJudgment` 对齐 C1 规范化名。

### 需 owner 联合决策 / 跨文档同轮修订
- BC-2：C1 定义 `IRhythmModifierProvider` + 窗口重算纳入 perk delta + 反向登记 C3（C1/C3 同轮）。
- BC-3：F4 补 EnemyInfoSO 字段 + 指定 CapacityTable 宿主 SO（F4/C4 同轮）。
- BC-5：定义 `AttackInputType ↔ AttackType` 映射（C2/F4）。
- BC-6：F2 补 `OnBeatClockResumed`/`IAudioPlayer.Pause·Resume`（F2/C1/C3 同轮，随 OQ-5）。
- BD-1：Off perk 封顶 or 明确无障碍优先；C1 Q3 与 OQ-C3-3 同批锁定，先于 C5/FE5。
- BD-2：C1/C2/C4 联合定义 BPM 跃升承接策略，先于 FE4 Boss GDD。
- SC-1：F6 订阅 `OnAudioUnavailable` 承接降级。
