# Epic: C1 节奏量化攻击系统

> **Layer**: Core
> **GDD**: design/gdd/rhythm-core.md
> **Architecture Module**: C1 RhythmJudger
> **Status**: Ready
> **Stories**: Not yet created — run `/create-stories rhythm-core`

## Overview

本 epic 实现核心循环的心脏 `RhythmJudger`：把 F3 携精确 DSP 时间戳（`InputAction.performed`，禁 `Update()` 轮询）送来的攻击输入吸附到 F2 广播的**半拍**网格 `OnHalfBeatLookahead` 上执行，按输入与拍点的时间偏差评级为 Perfect / Good / Miss，并以 `event Action<RhythmJudgmentEvent> OnRhythmJudgment` 广播给下游（C2 连招分支权限 / P1 HUD / PL2 节奏反馈 / 伤害倍率应用方）；同时以公开属性 `QuantizationMode Mode` 暴露当前激活模式（`architecture.md:135` Exposes 第 2 项），这是 C5/FE5 功勋折算与 SG-4 采样时刻的唯一采样入口（`rhythm-core.md:744`、AC-C1-21b `:843`）——`Mode` 已从事件面移除、降级为内部状态（`rhythm-core.md:521`），但其**公开面仍是规范接口**，不得当作纯私有字段。架构沿用 ADR-D3 的**节拍锚点 + 单槽预缓冲（last-in-wins）**：输入先入 `_pendingInput`，在拍点 lookahead 触发时以 `beat_dsp_time` 计算偏移并评级，或在拍点过后 `LateWindowMs`（默认 100ms，`adr-d3:223`）内即时评级。三种量化模式中 HalfBeat 为默认；**Beat 模式并不改变 F2 的广播粒度——F2 仍只广播每半拍节点（Core Rule 3 不变），Beat 是 C1 内部的 skip toggle 跳拍降采样**（每隔一次处理 `OnHalfBeatLookahead`，等效全拍网格，`gridIntervalMs = fullBeatMs`，`rhythm-core.md:48`）；Off 为无障碍通道，输入到达即以 `offset = 0` 恒判 Perfect，但 `CancelTier` 强制封顶 **Standard**（不授予 Advanced，Off 合约第 5 条 `rhythm-core.md:58`）。评级决定伤害倍率（1.0 / 0.75 / 0.5）、ΔCombo（+2 / +1 / 不变，Miss 不清零）与取消窗层级；C1 是 `CurrentCombo` 的唯一写者与广播者（`ComboMax = 999`），经 `IComboCounter` 向 C2 只读暴露（C2 绝不回写）。攻击动画侧由 C1 调用 `AnimancerAttackPlayer.Play(attackType, beatDspTime)` 把动画吸附到拍点执行（ADR-D5，`rhythm-core.md:191`/`:743`；`architecture.md:135` Consumes 列 `IAnimancerAttackPlayer`）。窗口宽度在每次 BPM 变更时走运行时钳位级联重算（写 `_effectivePerfect/GoodMs` 运行时字段，绝不回写 SO，ADR-D4 D4-1），含平台保底 `platformMinPerfectMs`（PC 16.7 / iOS 39 / Android 80，运行时 `Application.platform` 赋值而非编译期宏）与 BD-2 BPM 跃升适应窗口。生命周期侧：时钟未确认前不判定（`IsStartConfirmed` 门，Off 豁免）、暂停即清缓冲并关 LateWindow、run-end（Death 与 Victory 均触发 F6 域事件 `OnRunEnded(outcome)`）同帧清空 `_pendingInput` / `_lateWindowActive` 并停止处理拍点，保证无 `RhythmJudgmentEvent` 跨越 run-end 边界（SC-2 / AC-C1-22e）。**不攻击的拍点不产生 Miss**——C1 只判玩家主动发出的攻击，不对沉默施加惩罚。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-D3: Rhythm Quantization Core（**Accepted** 2026-07-19） | `RhythmJudger : MonoBehaviour` 节拍锚点 + 单槽预缓冲主架构；同时声明 `RhythmConfigSO` schema（`adr-d3:217-218`，窗口创作默认值 `:220-223`）；覆盖 `TR-rhythm-001..013`（`traceability-index.md:44`）。Evaluation Algorithm 现被 ADR-D15 修订 | LOW（标准 C# 事件 + MonoBehaviour，无 post-cutoff API） |
| ADR-D8: DI / Deterministic Test Seam Contracts（**Accepted** 2026-08-11，step-1 spike + CI 门控） | 统一拥有 `IBeatClock` / `IInputEventBus` / `IComboCounter` 等 DI 缝隙接口 + 字段注入/属性包装 + `InternalsVisibleTo("EditModeTests")`；覆盖 `TR-rhythm-015`（`traceability-index.md:49`），解锁 80% 单测硬门 | LOW（纯 C# 接口声明，`[SerializeField]` 仅字段约束已处理，`adr-d8:42`） |
| ADR-D12: Death & Run-Lifecycle Semantics（**Accepted** 2026-08-11） | MVP 死亡 = 换新局（D12-2：下一局为新 `LoadSceneMode.Single`，复用 ADR-D1/D10 boot 路径）；死亡边界由 F6 在**同帧**执行 `BeatClock.Stop()` + `InputEventBus.DisableAllInput()` + 广播域事件——**D12 原文的域事件名为 `OnPlayerDeath`**（`adr-d12:60`/`:86`；D12 全文 0 次 `EndRun`），重命名裁决属 D13；TDE 原地复活休眠（D12-3，Inspector 关闭，非源码扩展） | LOW（仅组合已验证机制，无 post-cutoff API） |
| ADR-D13: Run-End Boundary — Victory / Death Unification（**Accepted** 2026-08-12，TD + game-designer 会签） | 死亡与胜利经同一 `EndRun(RunOutcome)` **单一出口 + 每局一次性闭锁 `_runEnded`**；technical-director 钦定**落地方式 (a)**：`OnPlayerDeath` → `OnRunEnded(outcome)`，**单一事件、不保留别名**（`adr-d13:8`/`:11`；`entities.yaml:405`；`tr-registry.yaml:898`）；C1 的 SC-2 清空对 Death / Victory 对称；与 D12 共同覆盖 `TR-rhythm-019` | MEDIUM（时钟/事件机制复用 D12 已验证部分；ADR 原文记「唯一未定项 = F6 侧 TDE 胜利事件名」，`adr-d13:22`）。**注**：该未定项 = VR1，已于 2026-08-13 **resolved**（`entities.yaml:412`/`:421-422`：TDE 4.5 `TopDownEngineEventTypes` 含 `LevelComplete`，无自动派发胜利事件 → 由 FE4/FE2/FE5 显式调 `EndRun(Victory)`，story-008 解 BLOCKED）——对 C1 侧无残留引擎风险，ADR 文本待下轮同步 |
| **ADR-D15: 节奏判定窗口缺陷——延迟补偿抵消 bug 与 Miss 评级空集（🔴 Proposed，2026-10-21）** | 对 ADR-D3 Evaluation Algorithm 的**修订**（Depends On: D3）。**SPEC-1 (Critical)**：删除 C1-F2 公式中代数上完全抵消的重复 `latency_offset_s` 项（F2 Core Rule 7 已在 `beat_dsp_time` 侧加过一次，`rhythm-core.md:239`）；**SPEC-2 (High)**：新增 `MissHeadroomMs` 旋钮（默认 30ms，范围 [15,60]）+ 钳位级联追加一步，打开可达 Miss 区间；**SPEC-3 (Medium)**：统一 latency_offset 量程的三处不一致声明。**Blocks: 生产 C1 判定代码实装** | NONE（纯 C# 规格层代数修正，不触 Unity API） |

> **ADR-D15 文件可见性注记（硬约束）**：`docs/architecture/adr-d15-rhythm-judgment-window-defects.md` **在本 worktree 基线中不存在**——它位于本地 `main` 的 commit `31545105`，尚未推送到 `origin`（`ls docs/architecture/` 最新为 `adr-d14-cancel-window-transient-protection.md`）。上表内容由主代理对该 commit 逐字核实，路径缺失**不是**引用错误。但其后果是硬的：
> - `/create-stories` 在 Step 2 有**ADR existence validation**——读完 epic 的 governing ADR 清单后，若任一 ADR 文件在磁盘上找不到，必须 **"stop immediately"**、不得拆解任何 story（`.claude/skills/create-stories/SKILL.md:53`、`:59`）。**因此在 D15 文件落入基线之前，`/create-stories rhythm-core` 必然空转、产不出任何 story**（见 `## Next Step` 前置条件 0）。
> - `/story-readiness` 与 `/create-control-manifest` 同样无法自动识别尚未入基线的 D15。
>
> **D15 不覆盖的相邻项**：DT-1（连击需命中确认 / combo 无 sink）被 ADR-D15 明确列为「相关但独立，另立 ADR」，不在 D15 范围内；**该独立 ADR 尚不存在**。对应台账为 **R-COMBO-01**（`risk-ledger.md:48`，🟡 / 🔵 Monitoring，owner = game-designer / C2）+ **DT-1**（`warning-ledger.md:39`，⚪ 挂里程碑）。涉及「连击增长是否要求命中确认」「combo sink」的设计改动不得挂在 D15 下推进；C1 侧 ΔCombo 语义（+2 / +1 / 0，Miss 不清零）在 DT-1 独立 ADR 落地前按现 GDD 实装并保留对账点。

## GDD Requirements

覆盖统计（traceability-index Rev.12，`traceability-index.md:3`/`:26`）：19 TR 中 **17 Covered / 2 Partial / 0 Gap**。TR 创建日期：18 条于 2026-08-11（Core 层首次登记，Arch ref ADR-D3），仅 `TR-rhythm-019` 于 2026-08-12 补登（run-end 增量轮次，`tr-registry.yaml:896-901`）。

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-rhythm-001/002/003 | F3 精确 DSP 时间戳判定（禁 `Update()` 帧时间戳）；节拍锚点 + 单槽预缓冲（last-in-wins）架构；`_pendingInput` 存输入 DSP 时间戳、窗口内后到输入覆盖先到 | ADR-D3 ✅ |
| TR-rhythm-004 | 三量化模式（HalfBeat / Beat / Off）由 FE7 运行时注入，战斗中可切换 | ADR-D3 ✅（⚠️ 注入方 FE7 无 epic — 见下方上游依赖表与 G4） |
| **TR-rhythm-005** | Beat-offset 公式 `(judgment_time + latency_offset_s − beat_dsp_time)×1000`；跨时域在「玩家感知时域」对齐 | ADR-D3 → **ADR-D15 SPEC-1 待修正（Proposed）** 🔴 — 现公式中 `latency_offset_s` 代数上完全抵消，完美踩拍玩家在高延迟设备上恒被判 Miss。**不得按现文本实装**（`rhythm-core.md:230`） |
| **TR-rhythm-006** | BPM 变更时重算运行时窗口钳位级联；写 `_effectivePerfect/GoodMs` 运行时字段，绝不回写 SO（ADR-D4 D4-1） | ADR-D3 ✅ → **ADR-D15 SPEC-2 追加一步（Proposed）** 🔴 — 级联须追加 `MissHeadroomMs` 步以保证可达 Miss 区间非空（`rhythm-core.md:277-293`） |
| TR-rhythm-007/008 | 平台 `platformMinPerfectMs`（PC 16.7 / iOS 39 / Android 80）经运行时 `Application.platform` 赋值而非编译期宏；BPM 注册量程 [80,240]，window-invariant `Debug.Assert` 包在 `#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD`（Release 剥离，`tr-registry.yaml:810`） | ADR-D3 ✅ |
| TR-rhythm-009/010/011 | 时钟就绪门（`IsStartConfirmed==true` 前不判定，Off 豁免）；暂停立即清缓冲 + 关 LateWindow，经 `OnBeatClockResumed` 恢复（无幽灵判定）；时钟未确认缓冲输入 2s 超时静默丢弃 + Warning 日志 | ADR-D3 ✅ |
| TR-rhythm-012/013 | `RhythmJudgmentEvent` readonly struct（**Rating / AttackType / CancelTier / ComboCountBefore / ComboCountAfter / OffsetMs**，`tr-registry.yaml:842` 与 GDD C1-F6 唯一规范源 `rhythm-core.md:499-507` 已一致）经 `event Action<RhythmJudgmentEvent> OnRhythmJudgment` 广播；LateWindow 过期以 DSP 时间比较权威判定（`inputDspTime − beatDspTime > LateWindowMs/1000`），帧率无关 | ADR-D3 ✅（⚠️ `architecture.md:135` 字段列表过期 — 见 G5） |
| TR-rhythm-015 | `IBeatClock` / `IInputEventBus` 声明为接口（字段注入 + 属性包装），`SetBeatClockForTesting` 在 `#if` 块内，支持纯 C# NUnit 测试 | ADR-D8 ✅（Accepted 2026-08-11，story auto-block 已解除） |
| TR-rhythm-019 | SC-2 run-end 幽灵评级守卫：订阅 F6 `OnRunEnded(outcome)`（Death **与** Victory 均触发，取代原 `OnPlayerDeath`、不保留别名）+ `OnRespawnComplete`（MVP 休眠）；run-end 同帧清 `_pendingInput=null` / `_lateWindowActive=false`、停止处理 `OnHalfBeatLookahead`；无评级事件跨边界（E10 / AC-C1-22e，`rhythm-core.md:844`） | ADR-D12 + ADR-D13 ✅（`traceability-index.md:51`/`:52`） |
| TR-rhythm-014/016/017/018 | perk delta 仅在低频事件（`OnMeterChanged` / `OnPerkStackChanged`）查询并缓存，判定热路径零 provider 开销；`ComboMax=999` + ΔCombo 钳位 [0,ComboMax] + Miss ΔCombo 硬断言 ==0；E7 窗口坍缩内联逻辑须在 Assert 前执行（Android BPM≥186），接受二元 Perfect/Miss 降级（`tr-registry.yaml:882`）；latency offset 量程 [-0.2,0.2]s，范围钳位委托 FE7（C1 信任 F2 读值不重复钳位） | ⚠️ **索引 "Coverage by ADR" 表未逐条点名** — 见 G5。TR-rhythm-018 另受 **ADR-D15 SPEC-3** 影响（量程三处声明不一致为 SPEC-1 的派生症状） |

**上游依赖与前置**——`systems-index.md:95` 的 Dependency Map 载明 C1 = **F2（拍点时序）+ F3（输入时间戳）+ F6（run-end 生命周期域事件；同行括号内另列「Animancer 攻击动画播放，ADR-D5」）**；`rhythm-core.md:727`–`:732` 的依赖表另把 FE7 标为**必需**、C3 标为**可选**：

| 上游 | 必需性 | epic 状态 | 说明 |
|------|--------|-----------|------|
| **F2 BeatClock** | 必需（Off 模式除外） | ✅ `production/epics/audio-beat-clock` | `OnHalfBeatLookahead`（`beat_dsp_time`）/ `IsStartConfirmed` / `LatencyOffsetMs` / `OnMeterChanged` / `OnBeatClockPaused`+`Resumed`（BC-6，事件名对齐 F2 权威，**非** `OnBpmChanged`；`rhythm-core.md:727`） |
| **F3 InputEventBus** | 必需 | ✅ `production/epics/input-event-system` | `OnAttackInput`（`attackType` / `inputDspTime` / `chargeStartDspTime`，哨兵 `-1.0`；`rhythm-core.md:728`） |
| **F6 TDELifecycleAdapter（生命周期面）** | 必需 | ✅ `production/epics/tde-integration` | `OnRunEnded(outcome)`（MVP 活跃，Death+Victory）/ `OnRespawnComplete`（MVP 休眠）；纯 C# 域事件，C1 **禁止直接 `using` TDE（`rhythm-core.md:729`/`:756`）** |
| **F6 / Animancer（`IAnimancerAttackPlayer`）** | 必需 | ✅ `production/epics/tde-integration` | C1 调用 `AnimancerAttackPlayer.Play(attackType, beatDspTime)` 把攻击动画吸附到拍点（`rhythm-core.md:191`/`:743`；`architecture.md:135` Consumes 列 `IAnimancerAttackPlayer`）。**ADR-D5 归 F6 epic 管辖**（`architecture.md:127` F6 行列 ADR-D1/D5/D10/D12/D13；`architecture.md:135` C1 行只列 ADR-D3/D8/D12/D13），C1 仅作调用方——但**调用点的实装义务在 C1 侧**，拆 story 时须显式挂载 |
| **`RhythmConfigSO`（窗口默认值 / `ComboMax` / `AdaptationWindowBeats`）** | **必需** | 🔴 **无上游 epic 承接** | GDD 依赖表把该 SO 记在「**ADR-D4 SO 数据层**」名下（`rhythm-core.md:731`），但 **schema 实际声明在 ADR-D3**（`adr-d3:217-218`，默认值 `:220-223`），且 **F4 侧不承接**：`architecture.md:127` 的 F4 Owns 栏逐字只列 7 类 SO（`AttackNodeSO`/`ComboGraphSO`/`PerkSO`/`WeaponSO`/`EnemyInfoSO`/`LevelConfigSO`/`PlayerInfoSO`）、`design/gdd/so-data-layer.md` 对 `RhythmConfigSO` 命中 **0** 次、`production/epics/so-data-layer/EPIC.md:11` 的 7 类 + `:55` 的第 8 类（`CombatDirectorConfigSO`）共 8 story 亦无此 SO。**结论：本 epic 须自建该 SO 资产与 schema story，不得假定 F4 已交付**；归属冲突见 G4 |
| **FE7 游戏设置系统** | **必需** — 决定评级模式（`rhythm-core.md:730`） | 🔴 **无 epic** | FE7 是唯一的 `QuantizationMode` 注入方，但 `systems-index.md:41` 记 FE7 为 **Not Started**、GDD `design/gdd/settings-system.md` 尚未创建，且 `production/epics/` 下只有 5 个 Foundation epic。**下游风险**：TR-rhythm-004（运行时注入 + 战斗中切换）与 Off 无障碍合约第 1、2 条（入口限制 `rhythm-core.md:54`、持续提示 `:55`）在 C1 侧无法端到端验证——须以 C1 内部默认值 + `#if` 测试注入自测，并在 FE7 epic 创建时回头做联调 story |
| **C3 心法构筑** | 可选（BC-2） | 🟡 无 epic（C3 本轮不符入场资格），**不阻塞** | `IRhythmModifierProvider`（窗口 delta / ΔCombo delta）+ `OnPerkStackChanged`；provider = null 时各查询走默认值，C1 不硬依赖 C3（`rhythm-core.md:732` 逐字）。回退路径已规范化，故标 🟡 而非 🔴 |

**旁证风险**：`LatencyOffsetMs` 的 runtime 唯一写者是 **FE3 延迟校准**，而 `systems-index.md:36` 记 FE3 为 **Not Started**（GDD `latency-calibration.md` 未创建）。TR-rhythm-018 把量程钳位委托给 FE7、信任 F2 读值——在 FE3/FE7 都未落地前，C1 的延迟路径只能以注入值测试。此为 R-AUDIO-01 的解阻前置（`risk-ledger.md:91`）。

> ⚠️ **实装前硬门禁（G1–G5，全部未清）**
>
> **G1 — ADR-D15 仍为 `Proposed`，且文件未入基线（🔴 Critical，同时阻塞 story 拆解与判定代码实装）**
> `docs/CLAUDE.md:13` 原文：*"Never skip `Accepted` — stories referencing a `Proposed` ADR are auto-blocked"*。ADR-D15 自述 **Blocks: 生产 C1 判定代码实装**。因此：
> - **前置 0（阻塞 `/create-stories`）**：D15 文件须先落入本基线。缺失时 `/create-stories` 的 ADR existence validation 会立即中止（`.claude/skills/create-stories/SKILL.md:53`、`:59`），一条 story 都产不出。
> - 凡触及 **C1-F2 偏移公式**（`rhythm-core.md:230`）、**C1-F3 评级钳位级联**（`:277`–`:293`）、或 **`MissHeadroomMs`** 旋钮的 story，**必须标 `Blocked`**，直至 D15 转 `Accepted` 且 GDD 文本落地。
> - 受影响的 GDD 位置（D15 自列）：C1-F2 偏移公式、C1-F3 评级钳位级联、Tuning Knobs、**AC-C1-02b**（`:810`）、**AC-C1-12**（`:829`）。
> - **Miss 评级空集**：D15 SPEC-2 证明可达 Miss 区间代入默认值为空。按 GDD 权威语义该区间**右开**——`(GoodHalfWindowMs_effective, LateWindowMs)`：偏移量须 `> Good_eff` 且 **`< LateWindowMs`**（**不含**精确边界点）才产生 Miss，`== LateWindowMs` 走 AC-C1-04「丢弃、不产生事件、不产生 Miss」（`rhythm-core.md:810`/`:812`/`:621`）。默认值代入 `(120, 100)` ——下界 > 上界，**区间恒空、与 BPM 无关**。⚠️ **ADR-D15 原文记作 `(…, LateWindowMs]`（闭上界），与 AC-C1-02b/AC-C1-04 的右开语义不一致；D15 转 Accepted 时须统一边界写法**，story/测试一律以 GDD 的右开语义为准（空集结论两种写法均成立）。
> - **空集的适用前提（不得过度外推）**：空集结论成立于「**默认旋钮值 + 网格钳位未收窄 Good 窗**」；**仅 BPM > 148.5（ADR-D15 语，该阈值在本基线其他文档中无出处）时网格钳位会意外打开一条窄缝**。第 1 轮试玩实际覆盖的 BPM 120 / 60（`production/qa/evidence/miaodao-slice-playtest-checklist _Jadyn.md:29`）与 M0 判据文档定义的名义测试点 BPM=120（`production/milestones/m0-rhythm-feel-acceptance.md:35`，该文档全文无 BPM 60）均 < 148.5。因此：**在默认旋钮值且 BPM ≤ 148.5 的前提下**，Miss 路径测试在 D15 落地前必然失败（与第 1 轮试玩实证吻合：「没有办法打出miss，全部都为good」，`miaodao-slice-playtest-checklist _Jadyn.md:50`；**被测对象为 `Assets/_VerticalSlice/MiaodaoRhythmSlice/` 切片代码、非生产 C1**，`:6`）。**显式豁免**：`GoodHalfWindowMs` / `LateWindowMs` 安全范围均为 [50,200]（`rhythm-core.md:763`/`:765`）、BPM 注册量程 [80,240]（`tr-registry.yaml:810`），二者都能配出非空 Miss 区间；**TR-rhythm-017 的 E7 窗口坍缩路径（Android BPM≥186，二元 Perfect/Miss 降级为验收语义，`tr-registry.yaml:882`、`rhythm-core.md:319`）不受此门阻塞，今天即可测**。
> - **关联台账 R-AUDIO-01**（`risk-ledger.md:31`，概率高/影响高/🔴，🔵 Monitoring）：其缓解链为「FE3 校准 + `LatencyOffsetMs` 两阶段写者（ADR-D10）+ A11Y-6 门（100ms 延迟校准后 Perfect ≥ 80%）」，而 D15 SPEC-1 证明校准值在现公式中被代数抵消 → 整条缓解链在 C1 侧失效，D15 不落地则 A11Y-6 门不可能通过。
> - **处理先例**：参照 `production/epics/tde-integration/EPIC.md:35` / `:48` / `:54` 对 TR-tde-014 与 Story 008 的 Blocked 处理方式（硬门写入引用块 + story 行标 **Blocked** + 单独注记解阻条件）。
> - **建议同步登记**：`design/registry/entities.yaml` 的 `pre_implementation_checks` 段（`:410`）当前只有一条 `vr1_tde_level_complete_event` 且已 `resolved`（`:412`/`:421-422`，2026-08-13）；D15 门**尚未登记**，应补一条以便 `/story-readiness` 自动拦截。
>
> **G2 — M0 手感判据 PASS 裁定未发生 + 第 1 轮试玩 A 组红线未过（🔴 流程阻塞）**
> `m0-rhythm-feel-acceptance.md:144` 载明：本判据 PASS「亦是 Core 层 epics/stories 规划（`/create-epics layer:core`）的手感前提」。**判据文档本身已 `Locked`**（`:7`「**Locked**（2026-08-16 定案）」，并带机器可读标记 `:154` `<!-- M0-ACCEPTANCE: 2026-08-16 | Locked | … -->`）——**Locked 指「判据定案」，不是「PASS 裁定」**。实际缺口是 PASS 裁定未发生：
> - D-2 窗口值未回填：`design/gdd/game-concept.md:426-427` 仍为 `perfect_window_ms = TBD` / `good_window_ms = TBD`（M0 `:135` 要求回填 ±50 / ±120ms @120BPM）；
> - D-3 Go/No-Go（`:136`「全判据 PASS → 解锁 Core (C1-C4) 实现冲刺」）无记录；
> - 三方签字 `:150`–`:152` 仍为 ⏳ 待签（Producer 判据 owner / game-designer 手感裁定 / technical-director 技术真值 D 组）。
> - **第 1 轮试玩已记录 A 组红线未过**：「除了无障碍模式下，基本打不出perfect」（`miaodao-slice-playtest-checklist _Jadyn.md:48`）、「没有办法打出miss，全部都为good」（`:50`）、手感主观打分 **1/5**（`:52`，目标「中位 ≥4、无一 ≤2」），而 A 组关口要求「手感中位 ≥4 且无 ≤2 · "踩了却 Miss" 零出现 · 再来一局意愿 ≥60%」（`:125`）。**按现有证据，D-3 在复测前应为 No-Go**；窗口默认值 50 / 120ms 须随 D15 一并复裁（`rhythm-core.md:863` 未决项 M0）。
> - **关联台账**：**R-FEEL-01**（`risk-ledger.md:30`，概率中/影响高/🔴，🟠 Mitigating，「整个产品是否成立的关口」）——缓解载体正是本 M0 判据；**R-BEATTIER-01 / DT-2**（`risk-ledger.md:53` ⚪ 挂 M0 / `warning-ledger.md:40`）——Beat 档放宽判定窗却保留 CancelTier 全开 + perk 全额 + 功勋全额，护栏对称性与本门同挂 M0，C1 实装 Beat 档时不得默认其已裁定。
> - **本 epic 因此是前置未满足下的规划产物**：D15 入基线后 story 可以创建，但**实现冲刺不得在 M0 三方签字与 D-3 裁定完成前启动**。
>
> **G3 — control-manifest 过期（🟡 必须先重生成）**
> `docs/architecture/control-manifest.md:5` 的 `Manifest Version: 2026-08-12`，且 `:20`–`:21` 自述 *"Excluded: ADR-D14 … is still **Proposed** and is not reflected here. Regenerate this manifest when D14 is Accepted."*——但 D14 已于 **2026-08-13 Accepted**（`traceability-index.md:53`）。story 文件会内嵌 manifest 版本并由 `/story-readiness` 比对（`control-manifest.md:9`–`:12`），内嵌一个过期版本会让后续 staleness 检查全程假阴/假阳。**实装前须先跑 `/create-control-manifest update`**；D15 Accepted 后须**再跑一次**（D3 Evaluation Algorithm 规则随之变更）。
>
> **G4 — 上游缺口与 owner 未指派（🔴 两条硬缺口 + 2 条 owner 开放项）**
> - **`RhythmConfigSO` 无 epic 承接（🔴）**：GDD 依赖表记在 ADR-D4 / F4 名下（`rhythm-core.md:731`），schema 实际在 `adr-d3:217-218`，而 `architecture.md:127` 的 F4 七类 SO、`so-data-layer.md`（命中 0）、so-data-layer epic 的 8 story 均不含它。**归属冲突须由 `/architecture-review` 或 Producer 裁定**（F4 增量 story vs C1 自建）；在裁定前，本 epic 按「C1 自建」规划并单列 story。
> - **FE7 无 epic（🔴）**：唯一 `QuantizationMode` 注入方缺位，详见上游依赖表。
> - **Off 合约第 3 条「排行/成就隔离」无执行方指派（开放项）**：`rhythm-core.md:56` 原文未指派 owner；GDD 只把**第 4 条功勋折算**的执行方钉给 C5/FE5（`:57`、`:754`「**阻塞下游**合约」、`:866`「Off 合约第 4 条为 C1 规范性要求，执行方 C5/FE5」）；`risk-ledger.md:46` 的 owner 栏「C1 / C5 / FE5」是整条风险的汇总、未逐项分配。**不得默认归 C5/FE5**——须 Producer / FE5 owner 裁定；C1 侧只保证暴露激活 `QuantizationMode` 供其采样。
> - **SG-4 采样时刻未定义（已承接但下游未开工）**：`warning-ledger.md:25`（🔵 已承接，owner = FE5 / C5 均 Not Started），由 TR-rhythm-004「战斗中可切换」直接诱发；关联 **R-ECONEXP-01**（`risk-ledger.md:58`，🟡 / 🔵 Monitoring）。C1 侧可提供缓解钩子（广播模式切换时刻），钉死采样时刻须在 FE5 经济 GDD 并列 Blocking DoD。
>
> **G5 — 文档规范冲突与缺口（🟡 实装取值须以 GDD 为准）**
> - **`architecture.md:135` 的 `RhythmJudgmentEvent` 字段列表已过期**：该行仍写 `AttackType/Rating/OffsetMs/BeatDspTime/Mode/ComboCountBefore`，而 GDD C1-F6 **自述为唯一规范性定义**（`rhythm-core.md:495`）并规范 6 字段 `Rating/AttackType/CancelTier/ComboCountBefore/ComboCountAfter/OffsetMs`（`:499-507`），`BeatDspTime` / `Mode` 已从事件面移除、降级为内部状态（`:521`）。**实装一律以 `rhythm-core.md:499-507` 为准；`architecture.md` §3 待同步。** `TR-rhythm-012`（`tr-registry.yaml:842`）**已与 GDD 一致**，需同步的只有 architecture.md 一处。
> - **2 条 Partial 未逐条标注（不得猜测）**：`traceability-index.md:26` 只给出汇总数 `19 / 17 Covered / 2 Partial / 0 Gap`，全文**没有任何 rhythm TR 被逐条标为 Partial**（该文件唯一的逐条 Partial 标注是 F6 的 `TR-tde-014`，见 `:25`）。其 "Coverage by ADR" 表仅点名 15 条 rhythm TR（ADR-D3 → `TR-rhythm-001..013`，`:44`；ADR-D8 → `TR-rhythm-015`，`:49`；ADR-D12 + ADR-D13 → `TR-rhythm-019`，`:51`/`:52`），余下 `TR-rhythm-014/016/017/018` 不被任何 ADR 行点名。**因此哪 2 条是 Partial 在现有文档中不可确定，本 epic 拒绝猜测**，须由下一次 `/architecture-review`（coverage 模式）逐条回填。此缺口本身不构成 ADR 门禁（story 创建的真实阻塞项是 G1 前置 0），但会让 `/story-readiness` 无法判定这 4 条 TR 的覆盖充分性——拆 story 时须把它们显式归入某个 story 的 TR 清单，避免落空。
>
> **残留台账（10 条，实装期逐条对账，原文与严重度已逐条核实）**：🔴 **R-AUDIO-01**（`risk-ledger.md:31`，见 G1）· 🔴 **R-FEEL-01**（`:30`，见 G2）· 🟡 **R-FRAME-01**（`:45`，帧量化误差，对应 TR-rhythm-001，缓解 = 强制 `InputAction.performed` 时间戳 + PC Perfect 半窗 ≥ 16.7ms 下限，须作为 code-review 硬检查项）· 🟡 **R-OFFDOM-01**（`:46`，C1 侧必须实装两项隔离：`CancelTier` 封顶 Standard + perk_multiplier 封顶 1.0 规范（C1-F5c / BD-1，`rhythm-core.md:489`）；其余两项见 G4）· ⚪ **R-BEATTIER-01 / DT-2**（`:53` / `warning-ledger.md:40`，挂 M0，见 G2）· 🟡 **R-COMBO-01 / DT-1**（`:48` / `warning-ledger.md:39`，ΔCombo 语义待 DT-1 独立 ADR，见 D15 注记）· ⚪ **R-ATTNBUDGET-01 / DT-3**（`:54` / `warning-ledger.md:41`，挂 M2，C1↔C4 共担听觉双载；**本 epic 不设计解法，仅登记**）· 🟡 **R-ECONEXP-01**（`:58`，owner = FE5 / C5 / game-designer）· 🔵 **SG-4**（`warning-ledger.md:25`，见 G4）· 🔵 **W-F**（`warning-ledger.md:24`，已由 ADR-D13 承接；C1 侧经 TR-rhythm-019 / AC-C1-22e 的 Victory 子断言闭环，结算经济学/胜利判定 defer FE5 + game-designer）。

## Stories

Not yet created — run `/create-stories rhythm-core`

> **前置**：`/create-stories` 在 ADR-D15 文件入基线前会立即中止（G1 前置 0，`.claude/skills/create-stories/SKILL.md:53`/`:59`）。
>
> **拆 story 时的强制约束**（来自上方硬门禁 G1–G5）：
> 1. 触及 C1-F2 偏移公式 / C1-F3 评级钳位级联 / `MissHeadroomMs` 的 story 一律标 **Blocked**（ADR-D15 `Proposed`，`docs/CLAUDE.md:13`）。
> 2. `TR-rhythm-014/016/017/018` 必须显式归入某个 story 的 TR 清单——索引未逐条点名 ADR，若不显式挂载会落空。
> 3. **`RhythmConfigSO` 资产与 schema 须单列一条 story，不得假定 F4 已交付**（G4；schema 取 `adr-d3:217-223`，旋钮取 `rhythm-core.md:762-769`）。
> 4. FE7 无 epic ⇒ 量化模式注入与 Off 合约第 1、2 条只能以 C1 内部默认值 + `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 注入自测，须单列一条「FE7 联调」待办 story 或 Open Issue。
> 5. **`AnimancerAttackPlayer.Play(attackType, beatDspTime)` 调用点须显式挂载到某个 story**（`rhythm-core.md:191`/`:743`；ADR-D5 归 F6 epic 管辖，C1 为调用方），不得因 ADR-D5 不在本 epic 的 Governing ADRs 而遗漏。
> 6. **`QuantizationMode Mode` 公开属性须作为规范接口实装并测试**（`architecture.md:135` Exposes 第 2 项；AC-C1-21b `rhythm-core.md:843`），不得退化为私有字段。
> 7. Miss 路径测试：**在默认旋钮值且 BPM ≤ 148.5 的前提下**，Miss 区间恒空，相关 story 在 D15 落地前不可宣称 Done；**TR-rhythm-017 的 E7 坍缩路径（Android BPM≥186，二元 Perfect/Miss）不受此门阻塞**。

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/rhythm-core.md` are verified
- All Logic and Integration stories have passing test files in `tests/`
- All Visual/Feel and UI stories have evidence docs with sign-off in `production/qa/evidence/`
- **ADR-D15 文件已入基线且已转 `Accepted`**，且其 SPEC-1/2/3 已落地到 GDD 的 C1-F2 偏移公式、C1-F3 评级钳位级联、Tuning Knobs、AC-C1-02b、AC-C1-12（解除 G1 Blocked 的前置）
- **Miss 可达性回归通过**：在**注册量程内下界 BPM=80**（`tr-registry.yaml:810`）与 **M0 名义测试点 BPM=120**（`m0-rhythm-feel-acceptance.md:35`）下，可达 Miss 区间 `(GoodHalfWindowMs_effective, LateWindowMs)`（右开，`rhythm-core.md:810`/`:812`）非空且能实测产出 Miss 评级事件；`MissHeadroomMs` 默认 30ms / 范围 [15,60] 已登记为旋钮（闭环第 1 轮试玩「没有办法打出miss，全部都为good」实证，`production/qa/evidence/miaodao-slice-playtest-checklist _Jadyn.md:50`，被测对象为 vertical slice 切片而非生产 C1，`:6`）
- **延迟补偿有效性回归通过**：注入 ±100ms `LatencyOffsetMs` 后，完美踩拍输入仍判 Perfect（证明 SPEC-1 的代数抵消已消除）
- **M0 手感判据 PASS 且三方签字完成**（Producer / game-designer / technical-director，`m0-rhythm-feel-acceptance.md:150-152`），D-2 窗口值已回填至 `game-concept.md:426-427`、D-3 Go/No-Go 已裁定；第 1 轮试玩 A 组红线项（Perfect 可达性、手感评分、"踩了却 Miss" 零出现）已复测通过（`:125`）
- **`/create-control-manifest update` 已重生成 manifest**（纳入 ADR-D14，并在 D15 Accepted 后再生成一次），所有 story 内嵌版本与之一致
- 运行时钳位只写 `_effectivePerfect/GoodMs` 运行时字段、**绝不回写 SO**（ADR-D4 D4-1）有断言覆盖；window-invariant `Debug.Assert` 确认包在 `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 内（Release 剥离）
- `platformMinPerfectMs` 经运行时 `Application.platform` 赋值（**非**编译期宏），且 `SetPlatformMinForTesting` 测试注入路径可用；R-FRAME-01 的「禁 `Update()` 轮询、强制 `InputAction.performed` 时间戳」已作为 code-review 硬检查项通过
- `RhythmJudgmentEvent` 以 GDD C1-F6 的 **6 字段**实装（`rhythm-core.md:499-507`），**不按 `architecture.md:135` 的过期字段列表实装**；`architecture.md` §3 的同步已提单（G5）
- `QuantizationMode Mode` 公开属性可被 C5/FE5 采样，`RhythmConfigSO` 资产已创建并通过 Validator
- SC-2 run-end 守卫对 **Death 与 Victory 双路径**均通过（AC-C1-22e 含对称 Victory 子断言），确认无 `RhythmJudgmentEvent` 跨越 run-end 边界
- Off 模式 C1 侧两重隔离已实装并测试：`CancelTier` 封顶 Standard、perk_multiplier 封顶 1.0 规范；功勋折算（执行方 C5/FE5，`rhythm-core.md:866`）已登记为**阻塞下游**合约；排行/成就隔离的执行方已由 Producer 指派（G4）
- 单元测试覆盖率 ≥ 80%（technical-preferences 硬门；ADR-D8 Accepted 已解锁该门）
- 残留台账 **10 条**（R-AUDIO-01 / R-FEEL-01 / R-FRAME-01 / R-OFFDOM-01 / R-BEATTIER-01+DT-2 / R-COMBO-01+DT-1 / R-ATTNBUDGET-01+DT-3 / R-ECONEXP-01 / SG-4 / W-F）在 epic 关闭时逐条复核状态，🔴 两条（R-AUDIO-01 / R-FEEL-01）须有明确进展记录

## Next Step

**先清前置 0，否则 `/create-stories` 会空转**：把 `docs/architecture/adr-d15-rhythm-judgment-window-defects.md`（本地 `main` 的 commit `31545105`）推送到 `origin` 并落入本基线——该文件缺失时 `/create-stories` 的 ADR existence validation 会在 Step 2 立即中止、不拆解任何 story（`.claude/skills/create-stories/SKILL.md:53`/`:59`）。

前置 0 完成后，运行 `/create-stories rhythm-core` 把本 epic 拆为可实现 story——拆分时须遵守 `## Stories` 下的 7 条强制约束。

**在启动实现冲刺之前，再清三道门**：
1. 把 ADR-D15 从 `Proposed` 推到 `Accepted`，随后落地 GDD 文本修订（含 Miss 区间边界写法统一为右开）；
2. 完成 M0 判据的三方签字 + D-2 窗口值回填 + D-3 Go/No-Go 裁定，并复测第 1 轮试玩的 A 组红线项（`production/milestones/m0-rhythm-feel-acceptance.md`、`production/qa/evidence/miaodao-slice-playtest-checklist _Jadyn.md`）；
3. 跑 `/create-control-manifest update` 重生成 manifest（纳入 D14；D15 Accepted 后再跑一次）。
