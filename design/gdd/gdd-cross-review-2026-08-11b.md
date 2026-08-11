# Cross-GDD Review Report — 2026-08-11b（full 深度复审）

> **上一轮**：`gdd-cross-review-2026-08-11.md`（**PASS** — 验证 2026-08-10 blocker-fix 清零）
> **本轮目的**：`/review-all-gdds` full 模式深度复审（一致性 + 设计整体性 + 跨系统场景走查）
> **审查方式**：三通道并行 —— (2) 跨 GDD 一致性 / (3) 游戏设计整体性 / (4) 跨系统场景走查；三通道结论均已逐条回原文核验
> GDDs Reviewed: 9 系统（F2/F3/F4/F5/F6 + C1/C2/C3/C4）+ game-concept + entities.yaml
> Systems Covered: 节奏战斗全链（Foundation F2-F6 + Core C1-C4）

---

## 执行摘要

Phase 2 一致性判 **CONCERNS**（3 Warning + 6 Info，无 blocking）；Phase 3 设计整体性判 **CONCERNS**（4 Warning）。但 **Phase 4 场景走查在多系统交互边界发现 3 个此前未覆盖的 BLOCKER** —— 这正是场景走查相对逐对静态分析的增量价值。**总判定 FAIL**。

三个 BLOCKER 集中在一个连贯主题：**BeatClock 生命周期在非常规状态转换（冻帧 / 死亡 / 重生）下的跨系统合约缺失或矛盾**。均可通过定向 GDD 补丁 + 跨系统 owner 裁定解决，**非根本性重设计**。

---

## 一、跨系统场景问题（Phase 4）— FAIL

走查场景清单：
- **A** — Perfect 冻帧链（输入→C1判定→C2连招→伤害→PL2冻帧）
- **B** — 死亡/重生语义（F6 原地复活 vs Roguelike 局结算）
- **C** — 死亡/重生令牌孤儿（C4 绝对拍令牌 vs Stop() 归零 global_beat_index）
- **D** — Boss 阶段 BPM 跃升三系统同帧变化
- **E** — 战斗+经济重叠（Off 折算采样时刻）

### 🔴 Blockers

**SG-1　冻帧 × BeatClock 关系全链未定义**
涉及系统：PL2（冻帧）× F2（DSP 时钟）× C1（DSP 判定）× C2（动画归一化取消窗）× F3（冻帧内输入）。
Perfect 触发 33–66ms 冻帧（`rhythm-core.md:771`、`game-concept.md:381,493,519`），owner = PL2（**Not Started**）。BeatClock 生命周期唯一权威 = F6 `TDELifecycleAdapter`（`tde-integration.md:51`），PL2 无权暂停时钟。**无任何 GDD 规定**：冻帧是否 `timeScale=0`、冻帧期间 FMOD/DSP 拍点是否继续、C1（DSP 域判定）与 C2（动画归一化取消窗）在冻帧后如何保持同时域。BPM 240 时半拍 = 125ms，66ms 冻帧 ≈ 半个半拍——若时钟继续走而动画冻结，冻帧后「定格的刀」与鼓点错位，玩家可能「DSP 域踩准了却因 C2 取消窗（动画时间）滞后而接不上连招」，正是 C1/C2 明令禁止的失信体感（`rhythm-core.md:33`）。**失败模式：未定义行为 + 矛盾玩家反馈 + C1/C2 时基竞态。**

**SG-2　死亡语义模型冲突（Roguelike 局结算 vs TDE 原地复活）**
涉及系统：F6 × F2 × F5 × FE5 × game-concept。
F6 状态表（`tde-integration.md:143`）+ Rule 15：`RespawnComplete → BeatClock.Play()重启`（原地复活、重启同关时钟）。game-concept（`:145,164`）：死亡 = 即时局结算、功勋保留、**下一局立刻开始**；F5（`:316`）：局内状态不存档、Roguelike 标准无同局复活。**两套死亡语义从未调和**：按 F6 重启同关时钟则死亡未触发 FE5 局结算、功勋未落盘；按 game-concept 换新局则 F6 的 `RespawnComplete→Play()` 原地重启路径不该在原关发生。二者对「死亡后 BeatClock / 输入 / 存档」的处置互斥。**失败模式：破碎状态转换 / 跨文档矛盾模型。**

**SG-3　C4 死亡/重生令牌孤儿（依附 SG-2）**
涉及系统：C4 × F2（`Stop/Play` 重置 `global_beat_index`）× F6（重生重启）。
C4 仅有 E6（`combat-director.md:280`）处理「BeatClock 暂停/停止 → 冻结，恢复后**从下一拍继续**」——假设时钟**连续**（Resume 语义）。但死亡→重生走 `Stop()→Play()` 重启：F2 规则 10（`audio-beat-clock.md:60`）与 `:373` 明确 `Stop()` 将 `global_beat_index` **归零**、「下游不应假定其跨房间/重启连续」。C4 的 Granted 令牌 `targetLandingBeat` 用**绝对拍计数**（E4 `:278`、F-5 `:218`），且 C4 Dependencies **不订阅** F6 死亡/重生事件（`:316` 明确「F5 无直接依赖」，上游仅 F2/ADR-D2）。**C1 已获 SC-2 死亡/重生域事件订阅，C4 无对称合约。** 结果：复活后 `global_beat_index` 归零，残留令牌 `targetLandingBeat=22.0` 失锚，落拍「数学必然」（规则 6）被打破 → 幽灵攻击 / 攻击时机错乱。**失败模式：破碎状态转换 / 缺失跨系统合约。**

### ⚠️ Warnings

**SG-4　Off 折算采样时刻未定义，可被奖励操纵**
涉及系统：C1（模式暴露）× FE7（战斗中切档）× C5/FE5（折算执行，未成文）× F5。
量化模式可战斗中途切换（`rhythm-core.md:52`），Off 击杀功勋 `meritMultiplierOff=0.5` 折算读「激活模式」，但采样时刻（造成伤害时 / 击杀帧 / 结算帧）无定义（`:57,728,738`）。玩家可全程 Off 输出、击杀前切回 HalfBeat 拿全额功勋 → **重开 `gdd-cross-review-2026-08-09.md:23` 已封堵的 D1 反支柱漏洞**。执行方 C5/FE5 均 Not Started。

**SG-5　BPM 跃升边界事件语义分叉 + 坐标系混用**
涉及系统：F2（Accepted/Changed 双事件）× C1 × C2 × C4 × F6。
C2 用 `OnMeterChangeAccepted` 提前 `UpdateBpm()` 变速、用 `OnMeterChanged` 做取消窗适应（`combo-system.md` EC9/规则 12），边界前区间存在「动画已缩、取消窗尚未受 `max` 保护」的窗口；C1-F5b 用 raw 的 `predictedEffectiveDspTimeRaw` 定义适应窗口结束时刻（`rhythm-core.md:429`），而判定热路径处于 adjusted 时域，非零 `latency_offset` 下适应窗口边界最多 ±200ms 错位（`audio-beat-clock.md:70` 反复告警的缺陷类）。

### ℹ️ Info

**SG-6　击杀→局内功勋累积路径无成文归属**
C1 将 Off 折算列为「阻塞下游」合约（`rhythm-core.md:738`），但 C5/C6/FE5 均 Not Started（`systems-index.md`）—— SG-4 的执行载体当前不存在，属已知缺口而非当前逻辑矛盾。

---

## 二、跨 GDD 一致性（Phase 2）— CONCERNS（无 blocking）

### ⚠️ Warnings

**W-1　音频降级路径 Resume 归属自相矛盾**
F2 降级路径自调 `Pause()/Resume()`（`audio-beat-clock.md:58,444`），与 F6「`BeatClock.Play/Stop/Pause/Resume` 唯一调用者」（`tde-integration.md:51` Rule 4）冲突；`OnAudioRestored` 后谁负责 `Resume()` 且保证 F6 Rule 14「节拍先于输入」无明确归属（F6 Rule 14/状态表在 `OnAudioRestored` 只执行 `UnPause()` + `EnableGameplayInput()`，从不提 `BeatClock.Resume()`）。**建议**：钦定单一 Resume 归属方（建议 F6），F2 仅广播 `OnAudioRestored`。*（与 SG-1/SG-3 同属 BeatClock 生命周期权威主题。）*

**W-2　F3 ↔ C2 依赖方向不对称**
F3 下游表（`input-event-system.md:318`）声明 C2 直订 `OnAttackInput/OnDashInput`，但 C2 上游表（`combo-system.md:200-205`）不含 F3、攻击信息实际来自 C1 的 `RhythmJudgmentEvent.AttackType`。F3 下游表陈旧。**建议**：删除/修订 F3 下游表 C2 行（改「经 C1 间接」）。

**W-3　systems-index Dependency Map 落后于详细 GDD**
Map（`systems-index.md:94-97`）记 C1 依赖 = F2/F3，但 `rhythm-core.md:713` 将 **F6 列为必需上游**（SC-2）、FE7 必需。另缺 C2→F2/F6、C3→F2。**C1→F6 硬依赖未被 Map 捕获**，会误判 C1 可先于 F6 实现。**建议**：更新 Dependency Map 至少补 C1→F6。

### ℹ️ Info

- **I-1**：`combo_max` 50→999 债务**已清**（`rhythm-core.md` 现写 `ComboMax=999`，:347/750/815），与 C2/registry 一致 —— registry `combo_max` 注记的「C1 仍 clamp 50」follow-up 待更新（仅更注解，无需改 GDD）。
- **I-2**：C4「F2 需反向引用 C4」待办（`combat-director.md:297,318`）已过期，F2 已引用（`audio-beat-clock.md:110,112,487`），可关闭。
- **I-3**：C1/C3 引用的 `game-concept.md:213/:402` 行号漂移（实为 `:216` 反支柱 / `:404` 规则 3），建议改用小节锚点而非行号。
- **I-4**：`WeaponSO.perfectWindowModifier`（`so-data-layer.md:218`）声明被 C1 消费，但 C1（`rhythm-core.md:376-377` W-05）标注「当前无 weapon 上下文来源、暂不接线」—— 已知跟踪缺口，待 C5/C1 同轮闭合。
- **I-5**：C1-F5c 变量表（`rhythm-core.md:482,484`）记 `perk_multiplier` 范围 `[1.0,2.0]`，应对齐注册表/C3 钦定的 `[0.5,2.0]`（C1 漏记 0.5 下界）。
- **I-6**：C2 措辞「订阅 `RhythmJudgmentEvent`」（`:39`）vs 权威事件名 `OnRhythmJudgment`（C1-F6 `:522`），C1-F6 已调和事件名/载体名，无实质歧义、可澄清。

**已验证一致（背书）**：final_damage 四项链（base 25 × rating × combo[1,5] × perk[0.5,2.0]，极限积 250）、AdaptationWindowBeats 单一归属（BD-2 范本）、IDSPClock 全库统一 `GetDspTime()`、暂停/恢复事件源 `OnBeatClockPaused/Resumed`、BC-3 归属迁移、latency/bpm_range/windup/timeout 数值域 —— 均一致。

---

## 三、游戏设计整体性（Phase 3）— CONCERNS

### ⚠️ Warnings

**DT-1（3d）combo 无 sink，触顶后永久饱和**
Miss 不清零（`rhythm-core.md:133`）、无任何减项，`combo_multiplier` 在 count=148 触顶 ×5.0（`combo-system.md:112-114`），148→999 纯平台段，通常头一两间房达成 → **此后整局倍率恒 ×5.0，与后续踩准与否无关**。「维持连击」的核心动作幻想（C2 Player Fantasy `:21`）数值上不成立（C2 图位置软重置回根但段数不动）。更严重：game-concept 规则五护栏依赖「combo 增长维度保留评级差异」，触顶态该维度已饱和、**护栏意图被架空**（唯余 Advanced 取消一个未饱和支点）。**建议**：评估 combo sink（Miss 缓衰减而非归零）或后移触顶点；显式确认触顶态护栏支点是否足够。

**DT-2（3c）Beat 档缺护栏对称性**
Off 档五重隔离（入口限制 + HUD 标识 + 排行成就隔离 + 功勋×0.5 + CancelTier/perk 封顶，天花板压到 125）；Beat 档放宽判定窗（Perfect 更易，`gridIntervalMs=fullBeatMs`）却保留 CancelTier 全开 + perk 全额 2.0 + 功勋/排行全额（天花板 250），唯一「代价」是攻击密度减半。是否支配取决于**尚未验证**的平衡假设，而量化档定案本身是 M0 Spike 待定。**建议**：M0 Spike PASS 判据须含「Beat vs HalfBeat DPS 平衡对拍」，否则给 Beat 补对称护栏。

**DT-3（3b）注意力预算 = 4 active + 听觉通道进攻/防御双载**
并发主动系统 = 4（踩拍出招 C1 / 取消窗选连招 C2 / 听前摇防御 C4+TDE / 走位 TDE），处过载临界。C3 心法被正确设计为战斗中被动、房间之间才主动（良好）。更深隐患：C1 要玩家用耳朵定位**自己**攻击拍网格，C4 又要用**同一耳朵**听**敌人**前摇（`combat-director.md:20-22`）—— 进攻量化与防御预读挤在同一感官带宽。**建议**：M2 试玩专项测量「同时听拍出招 + 听前摇防御」；必要时把防御 telegraph 迁到视觉通道（已有节拍驱动视效基建）。

**DT-4（3e）敌人 telegraph 高 BPM 实时压缩未纳入 BD-2 协调**
BD-2 只保护判定窗与令牌**密度**，不保护单个敌人前摇的绝对反应时间。`attackWindupBeats [0.5,8.0]` 以拍计，Boss BPM 升至 ~200（`combat-director.md` E15 `:289`）时 0.5 拍前摇 = 150ms，低于人类反应下限 → 高 BPM 下低前摇敌人不可预读，击穿「听音乐防御」支柱。**建议**：C4/FE4 给高 BPM 下 `attackWindupBeats` 设绝对毫秒下限（如实时时长 ≥ 250ms），或 Boss 高 BPM 阶段只用高前摇敌人。

### ℹ️ Info / 背书

- **DT-5**：merit 折算执行方 = FE5（Not Started），防刷护栏当前仅存于规格、无实现载体（与 SG-4 同源）。**建议**：FE5 经济 GDD 创建时把 `meritMultiplierOff=0.5` 执行列为 BLOCKING DoD 并迁移 owner。
- **DT-6**：支柱三「华风沉浸」不被这 9 个机械系统中任何一个推进，全部委托 art-bible/PL2/audio-director —— 差异化钩子悬于未开工资产层，须警惕。
- **PASS**：3a 进程循环无竞争（merit 唯一永久货币、唯一来源）、3f 支柱对齐强（无反支柱违反、Off 五重隔离严密、规则五三护栏 Odin CI + 运行时断言双层强制）、3g 玩家幻想高度一致（五系统「通晓音律的武侠」同频）、3e-BD2 BPM 三系统协调堪称典范。

---

## 四、GDD 修订标记

| GDD | 原因 | 类型 | 优先级 |
|-----|------|------|--------|
| `tde-integration.md` (F6) | SG-2 死亡语义模型；W-1 音频恢复 Resume 归属 | 场景/一致性 | **Blocking** |
| `combat-director.md` (C4) | SG-3 死亡/重生令牌回收合约缺失 | 场景 | **Blocking** |
| `rhythm-core.md` (C1) | SG-1 冻帧判定合约；SG-4 Off 采样时刻；I-5 perk 下界 | 场景/一致性 | **Blocking**（耦合） |
| `combo-system.md` (C2) | SG-5 事件分叉；SG-1 取消窗解耦 | 场景 | Warning |
| `audio-beat-clock.md` (F2) | W-1 恢复归属；SG-1 冻帧期时钟行为 | 一致性 | Warning |
| `input-event-system.md` (F3) | W-2 下游依赖表陈旧 | 一致性 | Warning |
| `systems-index.md` | W-3 Dependency Map 缺 C1→F6 等硬依赖 | 一致性 | Warning |
| *(PL2 未创建)* | SG-1 冻帧规格须含时钟关系 | 场景 | 创建时 Blocking |
| *(FE5 未创建)* | SG-4/DT-5 merit 折算执行 | 设计 | 创建时 Blocking DoD |

> 本轮仅将 3 个 Blocking GDD（F6/C4/C1）在 systems-index 标为 **Needs Revision**（经 owner 批准）；Warning 项留注不改状态。

---

## 五、判定：FAIL

**3 个跨系统场景 BLOCKER** 须在架构/实现开始前解决：

1. **裁定死亡语义（SG-2，SG-3 前置）**：MVP 是「死亡 = 换新局（新 Single 场景加载走 boot 路径 `Play()`）」还是「TDE 原地复活重启时钟」？F6 须明确 `RespawnComplete` 在 Roguelike 中的角色，并与 F5 局结算 / FE5 对齐。
2. **给 C4 补死亡/重生令牌回收合约（SG-3）**：C4 订阅 F6 `OnPlayerDeath`（清空/取消全部 Granted 令牌），对称于 C1 的 SC-2。
3. **定义冻帧 × 时钟合约（SG-1）**：钦定冻帧是否暂停 BeatClock/FMOD、冻帧期 C1 判定与 C2 取消窗如何保持同时域、冻帧内输入如何处理 —— 写入 F2/C1/C2（及未来 PL2）。

上述多为跨系统 owner 裁定 + 定向补丁，建议由 technical-director 牵头，一轮 ADR/GDD 补丁即可消解，随后重跑 `/review-all-gdds` 复核。

**同时建议（Warning 级，不阻塞架构，但应在 Production 前记录为待验证项）**：
- M0 Spike 判据补「Beat vs HalfBeat DPS 平衡对拍」（DT-2）
- M2 试玩专项测量听觉通道进攻/防御双载（DT-3）
- 复评 combo 触顶饱和对规则五护栏的架空（DT-1）
- FE4 Boss GDD 前约束高 BPM telegraph 绝对毫秒下限（DT-4）

---

*报告生成：`/review-all-gdds` full 模式，三通道（一致性 / 设计整体性 / 场景走查）并行，结论逐条回原文核验。*
