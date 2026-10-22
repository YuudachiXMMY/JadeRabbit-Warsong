# Epic: C4 战斗导演 (Combat Director)

> **Layer**: Core
> **GDD**: design/gdd/combat-director.md
> **Architecture Module**: C4 战斗导演（CombatCore 程序集）
> **Status**: Ready
> **Stories**: Not yet created — run `/create-stories combat-director`

## Overview

本 epic 实现敌人攻击落拍的中央调度基础设施——game-concept 规则四「敌人强制落拍」的架构强制点（`combat-director.md:12`）。敌人 AI（FE1，经 ADR-D2 的 `CombatDirectorTokenAction` BT 节点）调用 `CombatDirector.RequestAttackToken(enemy)` 入队并阻塞于 `Status.Running`；C4 订阅 F2 `OnBeatLookahead`，在每个拍槽（beat-slot）上以 `EffectiveSlotCapacity`（F-1b，`combat-director.md:112`）为名额上限——该值由 F-1 `CapacityTable[meter][beatIndex]` 经 **BD-2** 按 `bpmRef/bpm` 归一化，使 tokens/sec 不随 BPM 线性上升，且 F-6 授令一律消费归一化后名额而非裸 `CapacityTable`（`:128`/`:230`）——按 `Priority = ThreatWeight(距离分段) + StarvationBonus(线性封顶 BONUS_MAX)` 降序、四键稳定排序（`priority↓ → waitBeats↓ → tReq↑ → enemyInstanceId↑`）选出获令者，并回调 `enemy.OnTokenGranted(targetLandingBeat)`（Core Rule 6，`:35`）；落拍点由 F-5 `SnapToEligible(grantBeat + W, subdivisionMode)` **只向上吸附（ceiling-only，禁止向下）**决定（`:202`），因前摇 W 以拍为单位，落拍是数学必然。**C4 的硬上游是 F2 + F4 + F6 三家**（`systems-index.md:96`）：调度时机源于 F2 拍事件；数据层全部由 **F4** 承载（**[BC-3]**，`combat-director.md:300`）——per-敌的 `attackWindupBeats`(W) 与 `timeoutBeats` 宿于 `EnemyInfoSO`(F4-S5)，全局导演参数（`capacityTable`、威胁权重 `dNear/dMid/wNear/wMid/wFar`、`starvationWeight`、`starvationCeiling`、`subdivisionMode`）与归一化基准 `bpmRef`（`:338`）宿于 `CombatDirectorConfigSO`(F4-S8)，故 C4 story 实装须等 F4 epic 的 story-004（武器/敌人/关卡 SO schema）与 story-006（战斗导演全局配置 SO schema）落地（`so-data-layer/EPIC.md:47`/`:49`）；run-end 边界由 F6 承载。令牌生命周期为显式状态机（`None/Pending/Granted/Consumed/Expired/Cancelled`，非法转移在 Editor 下 `Debug.Assert` 拦截），超时按 `TimeoutBeats`（以拍计）撤销并归还名额（Core Rule 8，`:37`）。架构边界为三条硬约束：C4 是 F2 的**纯下游消费者**（只读 `BeatEventData.beat_dsp_time`，禁用 `AudioSettings.dspTime`，不做延迟补偿，禁止调用任何 BeatClock 生命周期方法 `beatclock_lifecycle_bypass`，Core Rule 10 `:39`）；令牌逻辑**单线程**运行于主线程 Update 轮询（ADR-D6，Core Rule 11 `:40`）；SC-3 **run-end 令牌回收**（Core Rule 12 `:41`）——C4 订阅 F6 广播的纯 C# 域事件 `OnRunEnded(RunOutcome)`（Death 与 Victory 均触发，ADR-D13 取代原 `OnPlayerDeath`，不保留别名；负载不含任何 TopDownEngine 类型，C4 禁止 `using` TopDownEngine），在同一 Update 帧内将全部 `Pending`/`Granted` 令牌 `→ Cancelled → None`、清空 pending queue、释放所有拍槽占用，因 run-end 走 `BeatClock.Stop()` 使 `global_beat_index` 归零（F2 Rule 10，`audio-beat-clock.md:60`）、绝对拍 `targetLandingBeat` 失锚，必须全清而非冻结（区别于 E6 Pause 的冻结-续接，见 E15/E16 `:290`/`:291`）。C4 为纯运行时调度器，不持久化任何状态、无 F5 依赖；对玩家不可见，其成功标准是「隐形」——产出的是「用耳朵听敌人前摇」这一可靠防御直觉（`:16`）。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-D2: 敌人 AI 框架 | 采用 Unity Behavior (`com.unity.behavior` v1.0.16)；D2 锁定的是 BT **门控节点契约**——`CombatDirectorTokenAction` 为攻击唯一门控，`Status.Running/Success/Failure` + `TimeoutBeats`（`adr-d2:315`/`:147`）；`BeatClockBridge`(ExecOrder −100) 先于 `BehaviorGraphAgent`(−50) 写 Blackboard（**该同帧保证已被 E1 判定不成立，见硬门禁 2**）；**令牌授予协议本身 D2 自认未定义并明示下推 C4 GDD**（`adr-d2:279`/`:286`/`:325`）；M0 Spike 2026-07-18 PASS、0 BLOCKER（`adr-d2:18`） | LOW |
| ADR-D6: 音频中间件选型 | 选定 FMOD Studio 2.x；F2 拍事件以 FMODUnity RuntimeManager + Update 轮询派发、全程主线程；C4 令牌逻辑同线程运行，无跨线程队列/同步原语；C4 只读 `beat_dsp_time`，无延迟补偿。FMOD 为 post-cutoff，但风险实体在 F2 侧，C4 自身纯 C# | MEDIUM |
| ADR-D12: 死亡 & run-lifecycle | MVP 死亡 = 换新局；死亡经 F6 单一出口同帧 `Stop()`；C4 SC-3 令牌回收锚定该边界。仅组合已验证机制，无 post-cutoff API（`adr-d12:15`） | LOW |
| ADR-D13: run-end 胜利统一 | 死亡与胜利经同一 `OnRunEnded(RunOutcome)`（落地方式 (a)，单一事件、无别名）；C4 SC-3 对 Death/Victory 对称回收（`adr-d13:81`），闭合 W-F 的 double-`Play()` / 令牌孤儿。唯一未定项 = TDE 关卡完成事件成员名（见残留台账 QQ-02） | MEDIUM |

> **模块自身引擎风险**：`architecture.md:138` C4 行的 **Engine APIs** 栏为「无 post-cutoff」（表头见 `:133`）——C4 是纯 C# 调度器，不直接调用任何 post-cutoff 引擎 API。上表 Engine Risk 一律取各 ADR 头部的 Knowledge Risk 原栏（`adr-d2:15` LOW / `adr-d6:15` MEDIUM / `adr-d12:15` LOW / `adr-d13:22` MEDIUM），风险实体位于 C4 的上游（F2/FMOD、F6/TDE）。
>
> **跨 epic 对账（待 producer 统一口径）**：既有 epic 对同一 ADR 的取值与 ADR 原栏不一致——F2 epic（`audio-beat-clock/EPIC.md:17`）与 F3 epic（`input-event-system/EPIC.md:35`）对 ADR-D6 记 **HIGH**，F6 epic（`tde-integration/EPIC.md:21`）对 ADR-D13 记 **LOW**。本表取 ADR 原栏，差异须由 producer 裁定单一口径并回填既有 epic，否则 `production/epics/index.md` 的「治理 ADR 状态」汇总无法自洽。
>
> **跨切面补充覆盖**（不在 `architecture.md:138` C4 栏所列，但 `traceability-index.md` 明载覆盖 director TR）：**ADR-D10**（Bootstrap Sequencing, Accepted）覆盖 `TR-director-015`（须在 BeatClock ready 后订阅拍事件，`traceability-index.md:50`）；**ADR-D8**（DI/Test Seams, Accepted）承接 C4 的 clock mock DI 缝隙（`traceability-index.md:49` 明列 `TR-director(clock mock)`）——具体形态为 `IBeatClock`「只读消费者面，C1/C4 注入 mock」（`adr-d8:104`）、C4 单测 mock `IDSPClock`（`adr-d8:54`），测试可见性经 `[assembly: InternalsVisibleTo("EditModeTests")]`（`adr-d8:21`/`:318`，闭环记录见 `traceability-index.md:75`）。此即 C4 GDD Open Question 1 所留接口命名问题的归属。

## GDD Requirements

覆盖统计（traceability-index Rev.12，`traceability-index.md:3`）：19 TR 中 **17 Covered / 2 Partial / 0 Gap**，Notes 栏记 `D2/D6/D10`（`:29`）。

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-director-001/002 | 令牌是敌人攻击唯一门控（攻击能力必须置于 `CombatDirectorTokenAction` 之后，无令牌→无 hitbox，规则四强制）；`RequestAttackToken(enemy)` 入队 pending queue、BT 节点阻塞 `Status.Running` | ADR-D2 ✅ |
| TR-director-010 | `BeatClockBridge` `DefaultExecutionOrder −100` 先于 `BehaviorGraphAgent −50` 写 Blackboard | ADR-D2 ⚠️ — requirement 自带 `NOTE: review E1 flags this same-frame guarantee as unsound; pending ADR-D2 revision`（`tr-registry.yaml:1211`，见硬门禁 2） |
| TR-director-006 | `OnTokenGranted(targetLandingBeat)` 回调；落拍 = `grantBeat + W`（W 以拍计 → 落拍为数学必然） | **GDD 即规范**——`traceability-index.md:43` 的 ADR-D2 覆盖清单只列 `TR-director-001/002/010`，不含 006；`adr-d2:286` 将 `RequestAttackToken()`/`OnTokenGranted()` 协议列为 MEDIUM 风险「CombatDirector protocol undefined … which does not exist yet」并缓解为「C4 GDD must define this protocol」，`adr-d2:325` 再次把该协议归属 C4 GDD。规格权威 = C4 GDD Core Rule 6（`:35`）+ F-5（`:202`）+ `tr-registry.yaml:1179` |
| TR-director-008/009 | 纯 F2 下游消费者（只读 `beat_dsp_time`，禁 `AudioSettings.dspTime`，禁 BeatClock 生命周期调用，无延迟补偿）；单线程主线程 Update 轮询，无跨线程队列/同步原语 | ADR-D6 ✅ |
| TR-director-015 | `TimeoutBeats`/`waitBeats` 以拍计数 — 拍事件暂停时冻结、恢复不丢令牌（订阅须在 BeatClock ready 之后） | ADR-D10 ✅ |
| TR-director-019 | SC-3 run-end 令牌回收（对称 C1 SC-2）：订阅 F6 `OnRunEnded(RunOutcome)`（Death **AND** Victory 均触发，取代原 `OnPlayerDeath`），同帧取消全部 Pending+Granted 令牌、清 pending queue、释放拍槽占用；理由 = `Stop()` 归零 `global_beat_index`（F2 Rule 10）致绝对拍令牌失锚→幽灵攻击；C4 不调用任何 BeatClock 生命周期方法 | ADR-D12 + ADR-D13 ✅（requirement 文本自带 `COVERED: ADR-D12 (Accepted) + ADR-D13 (Accepted)`，`tr-registry.yaml:1283`） |
| TR-director-003/004/005/007 | 订阅 `OnBeatLookahead` 按拍槽评估 pending queue；拍槽容量取自 `CapacityTable[meter][beatIndex]`（数据驱动、per-meter，`OnMeterChanged` 重算）；`Priority = ThreatWeight(距离分段) + StarvationBonus(线性、封顶 BONUS_MAX)`，饥饿天花板强制提优；`SnapToEligible` 按 `subdivisionMode`（FullBeat/HalfBeat/DownbeatOnly）**只向上吸附** | **GDD 即规范**（调度器算法层，Formulas F-1/F-1b/F-2/F-3/F-4/F-5/F-6） |
| TR-director-011/012/018 | 令牌状态机六态（`None/Pending/Granted/Consumed/Expired/Cancelled`），非法转移 Editor 下 `Debug.Assert` 拦截；确定性四键稳定排序保证可复现测试；无持久化状态——令牌生命周期不跨存档（无 F5 依赖） | **GDD 即规范**（States and Transitions + F-6 + Dependencies 软依赖表） |
| TR-director-013/014/017 | `BONUS_MAX` 派生值由 Odin Validator 自动重算（禁硬编码缓存）；Odin Validator 5 条约束阻断不合规配置进包；`#if UNITY_EDITOR` 调试 overlay 只读快照，**Runtime 路径禁引 Odin `Sirenix.*`** | **GDD 即规范 + 项目 Forbidden Pattern**（见硬门禁 4） |
| TR-director-016 | CI 静态扫描 `Assets/Scripts/Gameplay/CombatDirector/`，命中禁用模式 `AudioSettings.dspTime` / BeatClock 生命周期调用即失败 | **GDD 即规范**（AC-C4-26 `c4_reads_audiosettings_dsptime` / AC-C4-27 `beatclock_lifecycle_bypass`） |

> ✅ **Gap = 0**——C4 无 untraced 需求（`traceability-index.md:29`）。上表标「GDD 即规范」的调度器/状态机逻辑按 `:32-34` 口径（「Core 层 Covered 以跨切面 ADR（D2/D3/D4/D5/D6/D10）+ DI 缝隙 ADR-D8 承接为准；应用层状态机/调度器逻辑以 GDD 为规范文档，**不单列 ADR 缺口**……Core 四系统 Gap 现为 0」）**仍计入 Covered**，不构成 ADR 缺口——与 F4 epic（Foundation 层唯一 Gap = 0 的系统，`so-data-layer/EPIC.md:30`）口径一致。
>
> ⚠️ **索引文档缺口**：`traceability-index.md:29` 只给出 C4 的 2 条 Partial **汇总计数**，未逐条标注是哪两条 TR；`:63` 的 Core 层汇总同样只记 7 Partial 总数（全文 `Partial` 逐条标注仅见于表头与 `tde-014`）。本 epic **不猜测** Partial 归属——须在 `/create-stories` 前由 `/architecture-review` 回填逐条标注，否则相关 story 的覆盖声明无据。

> ⚠️ **实装前硬门禁**（story 化与实装前须逐条清账）：
>
> 1. **W-F 胜利路径 run-end 触发归属**：`warning-ledger.md:24` 的缺口标题仍写「胜利/通关换局边界时钟 `Stop()` 归属未指派 → 胜利不 Stop 则 `global_beat_index` 不归零、下一局 boot 产生 double-Play() + 绝对拍令牌孤儿（同 SG-3）」。**时钟机制侧已承接**：ADR-D13（Accepted, commit a7154d35）裁定 F6 `EndRun(RunOutcome)` 为唯一 Stop() 执行者（`architecture.md:129`），台账状态为 🔵 已承接。**仍未分配的是胜利信号的唯一 fire owner**——`adr-d13:135` 把「何为胜利 + 唯一 fire owner」defer 给 game-designer / FE4 / FE2，`architecture.md:432`（QQ-05）仍把该项列为 Open（→ FE5）。对 C4 的直接后果：若胜利路径无 owner fire `OnRunEnded(Victory)`，SC-3 在**实机胜利分支**不触发，绝对拍 `targetLandingBeat` 跨 `Stop()` 失锚 → 幽灵攻击（Core Rule 12 `:41` / E16 `:291`）。**但 AC-C4-31 单元用例不受此阻**：其测试以 **mock `OnRunEnded(RunOutcome outcome)` 域事件源 + Spy 令牌状态机**驱动，由测试自身分别发布 `RunOutcome.Death` 与 `RunOutcome.Victory`（`combat-director.md:459`），两组用例均可在无 fire owner 裁决的前提下跑绿。故 story 门禁限定为：**端到端/集成层面的胜利分支验收**待 fire owner 裁决方可声明 Done，AC-C4-31 的 Logic 用例不标 Blocked。
> 2. **TR-director-010 的 E1 隐患（🔴 HIGH，已判定不成立）**：`tr-registry.yaml:1211` requirement 文本自带 `NOTE: review E1 flags this same-frame guarantee as unsound; pending ADR-D2 revision`。E1 原始条目 `architecture-review-2026-08-11.md:81` 定级 **🔴 HIGH** 并已判定 D2 的「同帧 beat 数据」保证**不成立**——`BeatClockBridge` 无 `Update()`，`[DefaultExecutionOrder(-100)]` 对其事件回调无效；`BeatClock` 默认 order=0，晚于 `BehaviorGraphAgent` 的 −50 → BT 读到**滞后一帧**的 `CurrentBeat`/`TimeToNextBeat`；修法为「给 BeatClock 显式更负 order，或删除误导性属性改为『同调用栈即时』」。同报告 `:96` 的 GDD 修订标记直接点名 C4：「TR-director-010 依据 ADR-D2 假设…→ E1：该保证不成立…复审 C4/未来 FE1：AI 决策节点不应依赖『当帧』beat 精度，或等 D2 修订后更新」；`architecture-review-2026-08-11-postD8.md:81` 记该项**仍 pending ADR-D2 revision**，至今未收口。**注意不可混同**：`adr-d2:284` 的 Beat-snapshot staleness（LOW — bounded，刷新粒度 ≤1 拍）是**另一条**风险，其缓解措施与 `adr-d2:313` 的帧计数日志只是 D2 自身的验证手段，**不是 E1 的裁决路径**。该项横跨 C4 与 FE1，不得在 C4 侧单方面假定成立。
> 3. **`OnTokenGranted` 签名冲突（与 E1 同属 ADR-D2 待修订项）**：`adr-d2:172` 的代码草图为**无参** `public void OnTokenGranted() => _tokenGranted = true;`，与 `tr-registry.yaml:1179` / C4 GDD Core Rule 6（`:35`）的 `OnTokenGranted(targetLandingBeat)` **不一致**。实装前须由 ADR-D2 修订或 FE1/C4 会签统一；**story 禁止照抄 D2 的 0 参签名**。
> 4. **TR-director-013/014/017 的 Odin 边界**：Odin（`Sirenix.*`）**仅限 Editor 程序集**，Runtime 代码路径禁止引用（项目 Forbidden Pattern：「Odin 仅在 Editor 程序集中可用；Runtime 代码引用会导致打包失败或体积膨胀」，`.claude/docs/technical-preferences.md:59`）。故 `BONUS_MAX` 派生重算与 5 条约束校验（AC-C4-28，Config-Data/ADVISORY）必须落在 `Editor/` 或 `#if UNITY_EDITOR` 块内；`#if UNITY_EDITOR` 调试 overlay 只读快照，不得把 Odin 类型泄入 CombatCore Runtime 程序集。**拦截语义的出处边界**：TR-director-014（`tr-registry.yaml:1243`）逐字只写「阻止不合规 **`EnemyInfoSO`** 进包」而非运行时兜底；AC-C4-28 本体（`combat-director.md:448`）只列 5 条约束、未点名 SO，其测试方法（`:449`）写作「构造 5 组违反单条约束的 `EnemyInfoSO`/配置表」。而 5 条约束中 (1)(2)(3)(4) 所涉 Knob 实际宿于 `CombatDirectorConfigSO`（`:300`/`:338`）——**校验覆盖面是否须显式扩及 `CombatDirectorConfigSO`，应在 story 层随 TR-director-014 一并明确**（当前 TR 文本未覆盖）。
> 5. **control-manifest 过期——须在 `/create-stories` 之前重生成**：`control-manifest.md:5` 的 `Manifest Version: 2026-08-12`，`:6` 的 `ADRs Covered` 列至 ADR-D13、**不含 ADR-D14**（生成时 D14 仍 Proposed），而 D14 已于 2026-08-13 转 **Accepted**（`traceability-index.md:53`）。`:7` 自述「regenerate with `/create-control-manifest update` when ADRs change」；`:9-10` 明载「`Manifest Version` is the date this manifest was generated. Story files embed **this date when created**. `/story-readiness` compares a story's embedded version to this field」——即版本号在 **story 创建时即固化**，事后比对。因此 `/create-control-manifest update` **必须跑在 `/create-stories` 之前**（而非实装前），否则 C4 story 将带着 2026-08-12 的陈旧版本号生成。（ADR-D15 亦未纳入，见残留台账。）
> 6. **M0 前置未满足**：`m0-rhythm-feel-acceptance.md:144` 载明「本判据 PASS 是 `game-concept.md:310` 量化档位定案 + `:426-430` 窗口值定案的前置，亦是 Core 层 epics/stories 规划（`/create-epics layer:core`）的手感前提」，而 `:150`/`:151`/`:152` 三个签字栏（Producer 判据 owner / game-designer 手感裁定 / technical-director 技术真值 D 组）**均为 ⏳ 待签**。本 epic 在该前置未闭合的状态下起草，**C4 的手感相关调参（`subdivisionMode` 默认值、`bpmRef`、telegraph 时长）不得在 M0 签字前定案**。同文件 `:154` 的 `<!-- M0-ACCEPTANCE: 2026-08-16 | Locked | … -->` 与三个 ⏳ 签字栏并存（锁定 ≠ 签字），语义冲突一并提交 producer 裁决。

> 📋 **残留台账（非阻塞架构门，须随 story 推进并挂里程碑）**：
>
> - **DT-4 / R-TELEGRAPH-01**（C4 / FE4，概率中·影响中·曝险 🟡，🔵 Monitoring / ⚪ 挂里程碑）：高 BPM telegraph 低于人类反应下限——`attackWindupBeats` 以拍计（范围 `[0.5, 8.0]`），Boss BPM≈200 时 0.5 拍 = 150ms；该项未纳入 BD-2 协调。承接：高 BPM 下设绝对毫秒下限（≥250ms），或 Boss 高 BPM 阶段只用高前摇敌人（`warning-ledger.md:42` / `risk-ledger.md:49`）。
> - **DT-3 / R-ATTNBUDGET-01**（game-designer / UX，概率中·影响中·曝险 🟡，⚪ 挂 M2）：注意力预算 = 4 active + 听觉双载（稳态感官带宽冲突，区别于 R-LEARN-01 学习曲线）——C1 用耳定位自身攻击网格、C4 又用同一耳听敌人前摇，进攻量化与防御预读挤同一带宽。承接：M2 试玩专项测量；必要时防御 telegraph 迁视觉通道（`warning-ledger.md:41` / `risk-ledger.md:54`）。
> - **E15 / BD-2 三方联锁**（C1 / C2 / C4 三方，已联合锁定 2026-08-11）：C4 侧由 F-1b `EffectiveSlotCapacity` 承接（归一化于 `OnMeterChanged` 生效即刻应用于未来拍，已授令牌不受影响，见 E4/AC-C4-25；`combat-director.md:290`/`:138`）；C1/C2 侧由 2 拍适应窗口承接，单一 owner = `RhythmConfigSO.AdaptationWindowBeats`（registry `adaptation_window_beats`，锁定值 2，`rhythm-core.md:768`）。C4 侧规格完备（AC-C4-30，`:388`）；跨系统一致性须随 C1/C2 修订复核。
> - **QQ-02 / R-TDEEVT-01（TDE 胜利事件成员名）**（TD / FE4 / FE5，Medium，🟠 Mitigating）：`architecture.md:429` 仍把「TDE 胜利/通关事件成员名核对（D13-2 桥接）」列为 Open（解决路径：查 `docs/engine-reference/unity/plugins/` 或安装源码 → 落 ADR-D13）；`risk-ledger.md:52` 则记「`LevelComplete` 源码已核实（`top-down-engine.md:72-105`）；TDE 不自动触发须显式 Trigger」。**两处口径待回填**；C4 胜利分支的**端到端**验收以此与硬门禁 1 的 fire owner 裁决为共同前置（亦见 F6 epic 的 TR-tde-014 硬门，`tde-integration/EPIC.md:35`）。
> - **GDD ↔ ADR-D2 口径冲突（签名权威）**（C4 / TD）：`combat-director.md:14` 与 `:301` 称「令牌授予协议的接口契约 / 接口签名由 ADR-D2 锁定」，而 `adr-d2:279`/`:286`/`:325` 三处自认该协议 **TBD / does not exist yet** 并明示下推 C4 GDD。本 epic 采信 ADR 原文：D2 锁定的仅是 BT 节点契约，令牌协议规格权威为 C4 GDD。该既存冲突须回填（GDD 侧措辞修订或 ADR-D2 修订时一并澄清），与硬门禁 3 的 0 参签名问题同源。
> - **ADR-D15（Proposed）**（C1 / TD）：节奏判定窗口缺陷修正（C1-F2 延迟项代数抵消 + Miss 评级空集）。**范围仅限 C1**（C1-F2 偏移公式 / C1-F3 评级钳位级联 / Tuning Knobs / AC-C1-02b / AC-C1-12），**不触 C4 的 F-1b 归一化或任何 C4 规格**。Status 为 Proposed → 按 `docs/CLAUDE.md:13`「Never skip `Accepted` — stories referencing a `Proposed` ADR are auto-blocked」，**不构成任何覆盖**；**C4 story 不得引用之**。该文件未推送到 origin，当前 worktree 基线中无此路径。不阻塞 C4；但 BD-2 伙伴系统 C1 的公式仍在修订中，C4 story 的 BD-2 跨系统断言须以 C1 修订后文本复核。
> - **FE1 / FE4 provisional**（C4 / FE1 / FE4，非阻塞）：`enemy-ai.md`(FE1) 与 FE4 GDD **尚未撰写**，双向引用未锁定（`combat-director.md:310`/`:311` 均标 ⚠️ provisional）。**接口权威划分**：BT 节点契约（`Status.Running/Success/Failure`、`TimeoutBeats`）以 ADR-D2 为准（`:301`/`adr-d2:315`）；`RequestAttackToken`/`OnTokenGranted` **令牌协议的规格权威为 C4 GDD**（`adr-d2:286`/`:325` 明确下推），FE1 GDD 撰写时按 C4 GDD Core Rule 6（`:35`）+ F-5（`:202`）与本 epic 的 Dependencies 节双向锁定；Boss 拍号切换驱动令牌密度亦待 FE4 GDD 对齐（`:311`）。端到端 AC（AC-C4-04/20/21/22/29）须有 FE1 侧对手件才能跑整。
> - **C4 Open Questions 1/2**（lead-programmer / TD）：OQ1（C4 测试缝隙 DI 注入接口命名——GDD 只约定「必须可注入模拟拍源」，签名属 ADR 范畴）**已由 ADR-D8 承接**（`traceability-index.md:49` 明列 `TR-director(clock mock)`；形态见 `adr-d8:104`/`:54`）。OQ2（静态扫描脚本位置 / 运行器 / CI 挂载点，AC-C4-26/27 的执行载体）**未定**——须在 TR-director-016 相关 story 前由 technical-director 确定 CI 挂载点，否则 AC-C4-26/27 无可执行载体。

## Stories

Not yet created — run `/create-stories combat-director`

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/combat-director.md` are verified
- All Logic and Integration stories have passing test files in `tests/`
- All Visual/Feel and UI stories have evidence docs with sign-off in `production/qa/evidence/`
- **AC 覆盖面**：AC-C4-01 ~ AC-C4-31 共 **31 条**全部通过——**30 条 BLOCKING**（25 条 Logic/BLOCKING + 5 条 Integration/BLOCKING）+ **1 条 Config-Data/ADVISORY**（AC-C4-28，`design/gdd/combat-director.md:448`，为全文唯一 ADVISORY）
- **AC-C4-29 反支柱回归常驻 CI**：无 `Granted` 令牌时敌人 hitbox 不得激活；此测试失败即视为破坏支柱一的架构强制点，S1 级阻塞
- **AC-C4-26/27 静态扫描跑绿**：C4 源码零命中 `AudioSettings.dspTime` 与 BeatClock 生命周期调用（`beatclock_lifecycle_bypass` 保持）；前置 = Open Question 2 的 CI 挂载点已确定
- **AC-C4-31 双 outcome 对称验证**：以 mock 域事件源发布 `OnRunEnded(RunOutcome.Death)` 与 `OnRunEnded(RunOutcome.Victory)` 两组用例，均断言全令牌 `Cancelled`/`None`、pending queue 空、拍槽占用清零、期间未调用任何 BeatClock 生命周期方法，且事件签名编译期不含 TopDownEngine 命名空间（`:459`）。本条不受 W-F fire owner 阻塞；**端到端/集成层面的胜利分支有效性**另以 fire owner 裁决（硬门禁 1）+ QQ-02 事件名回填为前置
- **AC-C4-30 BD-2 归一化验证**：`bpm == bpmRef` 时 `EffectiveSlotCapacity == CapacityTable`；`bpm == 2×bpmRef` 时按 `round(CapacityTable × bpmRef/bpm)` 且 tokens/sec 恒定（误差仅来自 `round`，≤0.5 token/beat）；归一化到 0 的拍位仍由 `starvationCeiling` 强制提优兜底（`:389`）
- **Odin 边界零泄漏**：CombatCore Runtime 程序集零 `Sirenix.*` 引用（TR-director-013/014/017）
- **TR-director-010 E1 已收口**：**ADR-D2 已完成 E1 修订**（给 `BeatClock` 显式更负执行序，或改写为「同调用栈即时」语义）**并转 Accepted**；`adr-d2:313` 的 FE1 PlayMode 帧计数日志只是 D2 自身的验证手段，**不可单独作为 E1 的收口凭据**（E1 结论为该保证结构上不成立，`architecture-review-2026-08-11.md:81` / `-postD8.md:81`）
- **`OnTokenGranted` 签名已统一**：`adr-d2:172` 的 0 参草图与 `tr-registry.yaml:1179` 的 `OnTokenGranted(targetLandingBeat)` 冲突已由 ADR-D2 修订或 FE1/C4 会签消解
- **覆盖统计可核验**：`traceability-index.md` 已逐条标注 C4 的 2 条 Partial 归属（当前仅有汇总计数）
- **control-manifest 已重生成**：`/create-control-manifest update` 后 `Manifest Version` 覆盖 ADR-D14（及届时 D15 的终态），story 嵌入版本与之一致

## Next Step

运行 `/create-stories combat-director` 将本 epic 拆分为可实现的 story。**但在拆分前须先清账三项**：(1) M0 手感判据三个签字（`m0-rhythm-feel-acceptance.md:150-152`）——该 PASS 是 `/create-epics layer:core` 的声明前置；(2) `traceability-index.md` 回填 C4 的 2 条 Partial 逐条归属；(3) 跑 `/create-control-manifest update`——story 创建即固化 `Manifest Version`（`control-manifest.md:9-10`），不先重生成则 story 必带陈旧版本号。W-F 胜利 fire owner（仅影响端到端胜利分支）、TR-director-010 的 E1 修订、`OnTokenGranted` 签名统一三项可在 story 化后、对应 story 实装前裁决，但届时相关 story 应标 Blocked。
