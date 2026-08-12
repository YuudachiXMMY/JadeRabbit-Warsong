# Cross-GDD Review Report — 2026-08-12（复审 / 回归验证 c02bdced 补丁）

> **上一轮**：`gdd-cross-review-2026-08-11b.md`（**FAIL** — 3 跨系统 BLOCKER：SG-1 冻帧×时钟 / SG-2 死亡语义 / SG-3 C4 令牌孤儿）
> **本轮**：`/review-all-gdds` full 复审，验证 commit `c02bdced`（ADR-D12 + F6/C1/C2/C4/systems-index 补丁）是否消解 3 BLOCKER，并全量重扫是否引入新问题
> **审查方式**：44-agent 编排——3 BLOCKER「回归验证→独立怀疑者证伪」+ 五维扫掠（结构/语义一致性、设计整体性、场景走查、补丁完整性）+ 60 候选对抗式验证（BLOCKER 3 票 / WARNING 1 票）。结论逐条回原文核验；SG-2/SG-3 消解与「C1 死亡模型传播」crux 由主审独立 grep 复核。
> GDDs Reviewed: 9 系统（F2-F6 + C1-C4）+ game-concept + systems-index + entities.yaml + ADR-D12 + ADR-D1
> Systems Covered: 节奏战斗全链（Foundation F2-F6 + Core C1-C4）

---

## 执行摘要

**三个原始 BLOCKER 的核心矛盾全部消解，降为 WARNING/INFO；对抗式验证未放行任何存活 BLOCKER**（唯一被框成 BLOCKER 的「C1 未迁移」候选被 3 票一致否决降为 WARNING）。补丁真正做对了最难的部分——**死亡语义的跨文档互斥模型被 ADR-D12 干净裁定**（死亡=换新局，RespawnComplete 休眠），F6/C4/ADR-D1 权威侧全部对齐；冻帧×时钟的时基竞态在 F2/C1/C2 间逐字对称闭合；C4 令牌回收合约（SC-3）对称 C1 SC-2 且双向登记。

但补丁是**「改对了主决策、遗漏了传播与清理」**：死亡语义裁定未传导到 C1、F6 自身留有旧措辞残留、systems-index 未同步新依赖、承诺的 registry 交付物未落地，并留下一个与死亡边界对称的**胜利/通关换局缺口**。这些均为 WARNING，不阻塞架构，但 **C1/F6 的死亡语义清理应在其实现前完成**（二者本就 Needs Revision）。上一轮未触碰的 5 个 Warning（W-1/W-2/SG-4/SG-5/DT-1~4）如实仍在。

**总判定：CONCERNS。**

---

## 一、回归验证：3 BLOCKER（全部 PARTIALLY_RESOLVED — 核心消解）

| BLOCKER | 结论 | 核心矛盾（已消解） | 残留（均 WARNING/INFO） |
|---|---|---|---|
| **SG-1 冻帧×时钟** | ✅ 核心消解 | F2:470 +「冻帧不通过 F2、禁 timeScale=0、持续广播拍点」；C1 E11(:705-714)「冻帧不暂停 DSP、不清 `_pendingInput`、C1 对冻帧无感知」；C2 EC15(:195)「不冻结玩家攻击 Animancer 时钟、禁 `AnimancerState.Speed=0`」三文档对称写死 → C1(DSP) 与 C2(动画取消窗) 恒同时基，原「踩准却接不上连招」竞态数学杜绝 | 「画面定格」可见机制未定义（owner PL2 未开工）；F6 是 `AnimancerState.Speed` 真 owner 却无冻帧护栏；game-concept:381「画面定格」与「不冻玩家动画」措辞张力 |
| **SG-2 死亡语义** | ✅ 核心消解 | ADR-D12 裁定死亡=换新局；F6 Rule15(:126-128)/状态表(:142-144)/EC-05(:280)/AC-F6-11(:478) + C4 + ADR-D1 权威侧全部对齐 game-concept:145/164 与 F5；上一轮 F6↔game-concept 互斥模型消除 | **C1 未传播（W-A）**、**F6 内部残留（W-B）**、ADR-D12 路径/行号引用瑕疵 |
| **SG-3 C4 令牌孤儿** | ✅ 核心消解 | C4 Rule12(:41)/E16(:291，显式区分 E6 暂停续接 vs E16 死亡全清) 订阅 F6 `OnPlayerDeath` 同帧清全部 Pending/Granted 令牌，对称 C1 SC-2；F6 侧(:161/:329/:348) 交互表+下游表+双向确认块反登记完整 | **systems-index 漏登 C4→F6（W-E）**、**胜利边界同类缺口（W-F）** |

---

## 二、跨系统场景问题（Phase 4）

走查场景：S-A 死亡→换新局全流程 / S-B C4 令牌回收 / S-C Perfect 冻帧链 / S-D Boss BPM 跃升 / S-E Off 折算采样。

### ⚠️ Warnings

**W-F　胜利/通关换局边界未定义（与死亡边界对称的缺口）**
涉及系统：C4 × F2 × F6 × FE5 × game-concept。
game-concept:164 明确「死亡/**通关**结算」都终结一局，MVP 含 1 Boss + 通关结算，故胜利是 MVP 路径。补丁只闭合了**死亡**边界；**胜利路径的时钟 `Stop()` 归属未指派** → 若胜利不 `Stop()`，`global_beat_index` 不归零、下一局 boot `Play()` 产生 **double-Play()**，且残留绝对拍令牌孤儿（与 SG-3 同类）。ADR-D12:138「Open Follow-up」已自认此缺口并划归 **FE5（Not Started）**，建议 FE5 定义统一 `RunEnded(Death | Victory)` 抽象并在 F6/FE5 修订中指派胜利路径时钟 `Stop()` 归属。**失败模式：破碎状态转换（潜在，MVP 胜利路径）。**

**SG-5（遗留）　BPM `accept→boundary` 区间取消窗无 BD-2 保护**
C2 在 `OnMeterChangeAccepted` 即 `UpdateBpm()` 变速（取消窗 ms 骤缩），但 `max` 宽容与 C1 判定重锚均在 `OnMeterChanged` 边界才生效，产生瞬态「Perfect 却接不上连招」窗口；C1-F5b 用 raw `predictedEffectiveDspTimeRaw`(:429) 定义适应窗口结束，非零 `latency_offset` 下边界最多 ±200ms 错位。上一轮 Warning，补丁未触碰。

### ℹ️ Info

**SG-4（遗留）　Off 功勋折算「采样时刻」未定义，可被切档操纵**
量化模式可战斗中途切换（`rhythm-core.md:52`），Off 击杀功勋 `meritMultiplierOff=0.5` 折算读「激活模式」，但采样时刻（伤害帧/击杀帧/结算帧）无定义。玩家可全程 Off 输出、击杀前切回 HalfBeat 拿全额功勋 → 重开 D1 反支柱漏洞。执行方 C5/FE5 均 Not Started（已知缺口，非当前逻辑矛盾）。FE5 经济 GDD 创建时须把采样时刻钉死并列为 Blocking DoD。

---

## 三、跨 GDD 一致性（Phase 2）— CONCERNS

### ⚠️ Warnings

**W-A　C1 死亡模型滞后（C1 ↔ F6/ADR-D12 分歧）** *（补丁引入，coupled，应在 C1 实现前清理）*
`rhythm-core.md` 状态表(:175-176)、E10(:691-701)、依赖表(:726/:746-747)、AC-C1-22e(:841) 仍建模 `RespawnComplete → 原地复活重锚拍网格`。ADR-D12 后该事件在 MVP **休眠、永不触发**（Inspector 关闭；死亡=换新局重建 C1 新实例）→ C1 复活恢复成死代码，**AC-C1-22e 复活段测 MVP 不可达路径**（F6 收到 RespawnComplete 走 fatal 守卫、不再广播 `OnRespawnComplete`）。**死亡清空侧（`OnPlayerDeath`）完好**（状态表 :175、E10 :696、AC-C1-22e 死亡段 :841 均正确）；仅复活侧陈旧。**非运行期互斥**（权威模型已单一为「换新局」），故 WARNING 非 BLOCKER。**须**在 C1 修订中把 RespawnComplete 标为 MVP 休眠、AC-C1-22e 改测换新局 boot 路径。

**W-B　F6 补丁内部残留（自相矛盾措辞）** *（补丁引入）*
`tde-integration.md` 状态转换原子性段(:148)+ 交互表 F6→C1 行(:160) 仍主张「同帧重广播 `OnPlayerDeath` / **`OnRespawnComplete`**」，与 F6 自己的 Rule15(:127「MVP 路径不广播原地重启语义」)/状态表(:144)/AC-F6-11(:478) 互斥。权威行为清晰正确，措辞未清理干净——须把 `OnRespawnComplete` 的 MVP 休眠状态在这两处对齐。

**W-E　systems-index 漏登 C4→F6 + 枚举表整体陈旧** *（补丁引入 + 遗留 W-3 同类）*
补丁新增 C4→F6（`OnPlayerDeath` SC-3）硬依赖，却未登记进 Dependency Map(:95) 或 Systems Enumeration 依赖列(:32)；且枚举表 依赖列相对已更新的 Dependency Map 散文整体陈旧（C1 :29 亦缺 F6，虽 Map :94 已补）。W-3 的补丁只改了散文没改枚举表。

**W-1（遗留）　音频降级 Resume 归属自相矛盾**
F2 降级路径自调 `Pause()/Resume()`(`audio-beat-clock.md:58,444`)，与 F6「`BeatClock.Play/Stop/Pause/Resume` 唯一调用者」(`tde-integration.md:51` Rule 4) 冲突；`OnAudioRestored` 后谁负责 `Resume()` 且保证「节拍先于输入」无明确归属。建议：钦定单一 Resume 归属方（建议 F6），F2 仅广播 `OnAudioRestored`。补丁未触碰。

**W-2（遗留）　F3 ↔ C2 依赖方向不对称**
F3 下游表(`input-event-system.md:318`)声明 C2 直订 `OnAttackInput/OnDashInput`，但 C2 上游表(`combo-system.md:200-205`)不含 F3、攻击信息实际来自 C1 的 `RhythmJudgmentEvent.AttackType`。F3 下游表陈旧。建议删除/修订 F3 下游表 C2 行（改「经 C1 间接」）。补丁未触碰。

### ℹ️ Info

- **I-5（未修）**：C1 变量表 `perk_multiplier` 范围 `[1.0,2.0]`(`rhythm-core.md` F5c) 仍与权威/注册表/C3 钦定的 `[0.5,2.0]` 不一致（漏记 0.5 下界）。
- **I-2（未闭合）**：C4「F2 需反向引用 C4」待办文本仍在（F2 实际早已引用 `audio-beat-clock.md:110/112/487`），可关闭。
- **I-6（未闭合）**：C2 仍以载体类型名 `RhythmJudgmentEvent` 指代事件，未采用权威事件名 `OnRhythmJudgment`（C1-F6 已调和，无实质歧义）。
- **INFO（ADR-D12 引用瑕疵）**：ADR-D12 两处路径引用 `docs/game-concept.md`（实为 `design/gdd/game-concept.md`）与 `design/gdd/reviews/…`；对 F5:316 的内容归属错位（:316 实为「局内状态不存档」，merit 结算落盘原文在 F5:16/:193/:197）。行号 `:145/:164/:213` 内容正确。
- **INFO（C4 头部记账）**：`combat-director.md:3` 状态「Revised (2026-08-10) — Pending Re-review」相对 8-11 的 SC-3 陈旧，且与 systems-index「Needs Revision」不一致。
- **INFO（冻帧时长漂移）**：补丁「33–66ms(2–4 帧)」vs game-concept:381 默认「2-3 帧(33-50ms)」；66ms=4 帧仍在 AC(:519)[1,4] 帧区间内，非越界。

**已验证一致（背书）**：死亡=换新局在 ADR-D12/F6 Rule15/状态表/AC-F6-11/C4/ADR-D1 权威侧全对齐；C4 SC-3 双向登记完整对称 C1 SC-2；冻帧时基合约 F2/C1/C2 三向对称；final_damage 四项链（base 25 × rating[0.5,1.0] × combo[1,5] × perk[0.5,2.0]）、adaptation_window_beats 单一 owner、BD-2 三系统协调、latency/bpm_range/windup 数值域——均一致。

---

## 四、补丁完整性（Phase 5 专项）

### ⚠️ Warnings

**W-C　ADR-D1 示例代码缺陷**
补丁给 `adr-d1-tde-integration.md` 新增的 Death/Respawn 节示例代码含 `_lifecycleEvents.RaisePlayerDeath()`，引用未声明字段/未定义方法，且与 F6 AC-F6-25「域事件挂在 `TDELifecycleAdapter` 上」的命名不一致——C1-C4 若照抄会踩编译/命名坑（呼应 architecture-review H1 的 InternalsVisibleTo 照抄风险）。

**W-D　registry 交付物未落地**
ADR-D12 迁移计划项4 承诺登记 forbidden pattern `f6_respawncomplete_inplace_restart_mvp`；F2:470 / C2:195 亦声称登记 `freeze_pauses_beatclock` / `freeze_zeroes_player_attack_speed`。`design/registry/entities.yaml` **无 `forbidden_patterns` 段、无任一条目**——commit 未触碰 registry，追溯链断裂（旧键 `f2_self_load_latency_from_f5` 亦同样缺失）。

---

## 五、游戏设计整体性（Phase 3）— CONCERNS（与上轮一致，全部 WARNING）

**DT-1　combo 无 sink，触顶后永久饱和**：Miss 不清零 + 无减项，`count=148` 触顶恒 ×5.0（`combo-system.md:112-114`），此后整局倍率与踩准脱钩，架空 game-concept 规则五护栏。建议评估 combo sink（Miss 缓衰减）或后移触顶点。

**DT-2　Beat 档缺护栏对称性**：Off 五重隔离；Beat 档放宽判定窗（更易 Perfect）却保留 CancelTier 全开 + perk 全额 + 功勋/排行全额（天花板 250），唯一代价攻击密度减半。建议 M0 Spike PASS 判据含「Beat vs HalfBeat DPS 平衡对拍」。

**DT-3　注意力预算 = 4 active + 听觉双载**：并发主动系统 = 4（C1 踩拍/C2 取消窗/C4+TDE 听前摇/走位）；C1 用耳朵定位自己攻击拍网格，C4 又用同一耳朵听敌人前摇——进攻量化与防御预读挤同一感官带宽。建议 M2 试玩专项测量；必要时防御 telegraph 迁视觉通道。

**DT-4　高 BPM telegraph 绝对反应时间未纳入 BD-2**：`attackWindupBeats [0.5,8.0]` 以拍计，Boss BPM ~200 时 0.5 拍 = 150ms < 人类反应下限。建议 C4/FE4 给高 BPM 下设绝对毫秒下限（≥250ms），或 Boss 高 BPM 阶段只用高前摇敌人。

> **背书（仍成立）**：3a 进程循环无竞争（merit 唯一永久货币、唯一来源，死亡=换新局强化 merit 经济）；3f 支柱对齐强（无反支柱违反、Off 五重隔离、规则五三护栏双层强制）；3g 玩家幻想高度一致；3e-BD2 BPM 三系统协调典范。**DT-5**：merit 防刷执行方 FE5/C5 仍 Not Started，死亡=换新局使 FE5 结算合约更关键。**DT-6**：支柱三「华风沉浸」不被 9 个机械系统推进，悬于 art-bible/PL2/audio-director 资产层。

---

## 六、GDD 修订标记

| GDD | 原因 | 类型 | 优先级 |
|-----|------|------|--------|
| `rhythm-core.md` (C1) | W-A 死亡模型滞后（RespawnComplete 休眠未传播 + AC-C1-22e 测死路径）；I-5 perk 下界；SG-4 Off 采样声明 | 一致性 | **实现前**（非阻塞架构） |
| `tde-integration.md` (F6) | W-B 内部 RespawnComplete 措辞残留；W-F 胜利边界（与 FE5） | 一致性 | **实现前** |
| `combat-director.md` (C4) | SG-3 **已解**；残留 W-F 胜利边界（划归 FE5）+ 头部状态陈旧 | 场景/记账 | Warning |
| `systems-index.md` | W-E 漏登 C4→F6 + 枚举表陈旧；头部仍标 FAIL | 一致性 | Warning |
| `adr-d1-tde-integration.md` | W-C 示例代码字段/命名缺陷 | 补丁完整性 | Warning |
| `entities.yaml` | W-D `forbidden_patterns` 段缺失 | 补丁完整性 | Warning |
| *(PL2 未创建)* | SG-1「画面定格」可见机制须含时钟/动画关系 | 场景 | 创建时 |
| *(FE5 未创建)* | W-F 胜利边界 + SG-4/DT-5 merit 折算执行 | 场景/设计 | 创建时 Blocking DoD |

---

## 七、判定：CONCERNS

**无存活 BLOCKER。** 3 个原始跨系统 BLOCKER 的核心矛盾已由 ADR-D12 及配套补丁真实消解，44-agent 对抗式验证确认无一回退为 blocking（唯一 BLOCKER 框候选被 3 票一致否决）。存在一组 coupled WARNING（死亡语义传播/清理未完成）+ 上轮未修复的 Warning。

**架构可以推进。** 建议先做一轮**小范围定向清理补丁**：
1. **C1** RespawnComplete 休眠传播 + AC-C1-22e 改测换新局 boot 路径 + I-5 perk 下界 [0.5,2.0]（W-A / I-5）
2. **F6** 原子性段(:148)/交互表(:160) 措辞对齐 Rule15（OnRespawnComplete MVP 不广播）（W-B）
3. **systems-index** 同步 C4→F6 依赖 + 枚举表补 F6 + 头部 CONCERNS（W-E）
4. **registry** 新增 `forbidden_patterns` 段并落地 3 个禁用模式（W-D）
5. **ADR-D1** 修正示例代码字段/命名（W-C）

完成后 C1/C4/F6 可回 Approved。**胜利边界（W-F）与 SG-4 执行归属随 FE5 经济 GDD 创建时作为 Blocking DoD 承接**；SG-1「画面定格」可见机制随 PL2 创建时定义。

**Production 前待验证项（Warning 级，不阻塞架构）**：W-1 音频 Resume 归属 / W-2 F3↔C2 依赖 / SG-5 BPM 边界取消窗 / DT-1 combo 饱和 / DT-2 Beat 护栏对称 / DT-3 听觉双载 / DT-4 telegraph 毫秒下限。

---

*报告生成：`/review-all-gdds` full 复审（ultracode 编排，44 agent，回归验证 + 五维扫掠 + 对抗式验证），结论逐条回原文核验，SG-2/SG-3 消解与 C1 传播 crux 经主审独立复核。*
