# ADR-D15: 节奏判定窗口缺陷族——延迟补偿抵消、Miss 评级空集、早侧评级失声

## Status
Proposed

## Date
2026-10-21（Proposed）
2026-10-22（修订：对齐 ADR 模板基线；并入 round-2 受控 A/B 实证、unity-specialist 引擎校验、game-designer 两项正式裁决；新增 SPEC-4 / SPEC-5；**重写修正 2**）

> 本 ADR 由苗刀节奏手感切片（`Assets/_VerticalSlice/MiaodaoRhythmSlice/`）第 1 轮试玩失败的根因排查触发。
> 切片是独立实现（不引用生产 C1 代码），故本 ADR 所述缺陷**只存在于生产 GDD/未来生产代码**，
> 切片已在本轮绕开（见「与切片的关系」），不受本 ADR 阻塞。
> 反过来，切片是本 ADR 唯一有实测数据支撑的参考模型——round-1/round-2 的受控 A/B
> （见 Context §实证）是 SPEC-2 从纸面推演升级为已验证缺陷的证据来源。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (6000.3.19f1) |
| **Domain** | Core（判定数学与 GDD 公式一致性；判定路径本身不触及 Unity/FMOD API 表面）|
| **Knowledge Risk** | **LOW** — 本 ADR 的判定代数不依赖任何 Unity API；但落地面与两项 6.3 约定相邻：(a) 新增旋钮进入 `RhythmConfigSO` 时受 6.3 `[SerializeField]` **fields-only** 约束管辖（权威面为 ADR-D4）；(b) 降级标志的 Editor 侧门禁依赖 Odin Validator，而 Odin 禁止出现在 Runtime 程序集。二者均为既有已决约定的消费，非本 ADR 新建。 |
| **Post-Cutoff APIs Used** | None |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`；ADR-D4（SO 字段约定）、ADR-D6（音频中间件 / `MockDSPClock`）、ADR-D8（时钟与输入接口契约） |
| **Verification Required** | 全部 Validation Criteria 均可在 **EditMode 纯 C#** 完成，无需 PlayMode 或真机。<br>**前置门控**：验证路径依赖 `IBeatClock` / `IInputEventBus` 的 mock 实现。两者签名已由 ADR-D8（Accepted）`:104-147` 权威声明，但**代码尚不存在**（`Assets/Scripts/Core/Interfaces/` 目前仅有 `IRandomSource.cs`，无 Mock 类型；ADR-D8 `:26` 自行 defer 到 C1/C2/C4 冲刺，`tests/README.md:80` 列为待办）。故本 ADR 的单测**gated on ADR-D8 Migration Plan 第 6 步交付**。<br>FMOD DSP 时钟在 EditMode 的可 mock 性已由 ADR-D6 `:193`（`MockDSPClock`）划线确认，不构成额外风险。构造注入范式可参照 `Assets/Tests/EditMode/CombatCore/CombatCore_DiSeamSpike_Test.cs`。 |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | **ADR-D3**（节奏量化核心，Accepted）——定义本 ADR 要修正的 Evaluation Algorithm（`:138`/`:145` 公式、`:139-141` 早侧闸门、`:111-131` 调用时序）。本 ADR 是对其算法的**修订**，非新增。<br>**ADR-D8**（Accepted）——验证路径的 mock 接口来源，见 Engine Compatibility 前置门控。<br>**ADR-D4**（Accepted）——新增 SO 字段须遵其 `[SerializeField]` fields-only 约定。 |
| **Enables** | 消解 M0 §2.1 Group A「Miss 评级不可达」的潜在阻塞；使 M0 Group C「无线音频延迟容忍度」判据对高延迟设备**首次可测**（现行生产代码路径下补偿完全抵消，该判据无法验证）。 |
| **Blocks** | 生产 C1 判定代码实装。当前 Pre-Production，生产 C1 尚未实装，故本 ADR 不阻塞任何已完成工作，只约束即将开始的实装。 |
| **Ordering Note** | 修正 4（早侧对称化）逐字改动 `m0-rhythm-feel-acceptance.md` §2.2 已 **Locked** 的不变量复刻（`:51-52`），**须先走 M0 修订流程**（owner: producer）。修正 1/2/3/6 不触及 Locked 内容，可先行。详见 Open Follow-up。 |
| **Related, not blocking** | DT-1（连击需命中确认）——独立缺陷，不同子系统（C1-F4 vs C1-F2/F3），另立 ADR。见「相关但独立」。 |

---

## Context

### Constraints

- **C-a｜三条互相竞争的硬承诺。** 判定窗口预算同时受三条承诺约束，而预算总量 `C = gridIntervalMs/2 − 1` 随 BPM 线性收缩：
  - **P1｜相邻拍不混叠**：任何有效窗口 ≤ `C`。来源 `rhythm-core.md:276-277`（`−1` 的注释明示目的）。这是**正确性不变量**，非手感取舍——混叠意味着同一输入被两个拍点同时声称，`OffsetMs` 的符号与量级双错。
  - **P2｜平台 Perfect 保底**：`PerfectHalfWindowMs_effective ≥ platformMinPerfectMs`（PC 16.7 / iOS 39 / Android 80ms，`:769`）。低于该值时平台 DSP 测量噪声超过窗口本身，Perfect 退化为抽签。
  - **P3｜Miss 可达**：Miss 区间非空（本 ADR 修正 2 引入的承诺）。
- **C-b｜P2 与 P3 的可行性边界数学上同一条。** `P2 可满足 ⟺ platformMinPerfectMs ≤ C`；`P3 可满足 ⟺ platformMinPerfectMs < C`（因 `missBudgetMs = C − PerfectHalfWindowMs_effective`）。P2 失效那一瞬 P3 也恰好失效——不存在「牺牲 P3 以保全 P2」的中间地带。
- **C-c｜禁止修改 TDE 源码 / Runtime 禁引 Odin**（`technical-preferences.md`）。降级标志的编辑期门禁只能落在 Editor 程序集。
- **C-d｜禁止用 `Update()` 轮询做节拍判定**（同上）。本 ADR 不改变 ADR-D3 的事件驱动架构。
- **C-e｜M0 里程碑为 Locked**（`m0-rhythm-feel-acceptance.md:7`），§2.2 逐字复刻了钳位级联与不变量。任何改动不变量的修正须走 M0 修订流程。
- **C-f｜生产 C1 尚未实装。** 无存量代码需迁移；代价集中在文档一致性与尚未编写的单测。

### Requirements

- **R-1** 完美踩拍的玩家，在任意 `latency_offset_s ∈ [−0.2, 0.2]` 下，`|beat_offset_ms|` 必须趋近 0（容限 0.1ms），与延迟值**无关**。
- **R-2** Perfect / Good / Miss 三档在「可行性前沿以下」的任意 (BPM, 平台, 量化模式, perk) 组合中必须**同时非空**；前沿以上的降级必须是**具名的、可被断言的**，而非偶然后果。
- **R-3** 钳位级联在任何参数组合下**不得产生下界 > 上界的区间**（SPEC-2 的 bug 类必须被结构性排除，而非靠挑数值规避）。
- **R-4｜禁止静默吞输入。** 任何被拍点缓冲准入的输入，必须有且仅有一次 `RhythmJudgmentEvent`，且 `OffsetMs` 符号忠实反映早/晚方向。来源：`rhythm-core.md:29` Player Fantasy 用「任何情况下都不会」明令禁止「我输入了，但游戏没有反应」。
- **R-5** 不变量违反必须在**开发期**可见（编辑期门禁 + 开发构建日志），不得仅依赖 QA 试玩发现——SPEC-2 潜伏至今的直接原因就是它在玩家视角完全无症状。
- **R-6** 修正不得削弱既有禁令 `direct_beat_offset_at_input_time`（`registry:539-543`）。

---

### 实证：round-1 vs round-2 受控 A/B

苗刀切片两轮试玩构成一组**唯一变量**的受控实验。唯一改动是 `RhythmJudge.cs:35` 的 `_goodAuthoredMs`：

| | `_goodAuthoredMs` | `goodEff = min(authored, C)` | 可达 Miss 带 | 占网格比 | 试玩实测 |
|---|---|---|---|---|---|
| **round-1** | 120 | `min(120, 124) = 120` | `(120, 125]` = **5ms** | 2% | 「没有办法打出 miss，全部都为 good」 |
| **round-2** | 100 | `min(100, 124) = 100` | `(100, 125]` = **25ms** | 10% | **「能打出 Miss 评级」** |

（120BPM / HalfBeat：`halfBeatMs = 250`，`C = 125 − 1 = 124`；切片用网格最近邻，`|offset|` 结构性封顶 `grid/2 = 125`。）

**这组数据证明了什么**：SPEC-2 的**机制**——「Good 评级天花板 ≥ Miss 区间天花板 ⇒ Miss 不可达」——已被实测确认，且只需把 Good 天花板下压 20ms 即从「整轮试玩 0 次 Miss」翻转为「可稳定打出」。

**这组数据没有证明什么**（诚实附注，三条）：

1. 切片**没有** `LateWindowMs` 状态机。切片的 Miss 天花板是网格最近邻的结构上限 `grid/2`，生产的是 `LateWindowMs`。两者是同一 bug 类的两种实例，数值不可直接套用（见 Migration Plan 第 2 步）。
2. 切片判定用 `Mathf.Abs(offsetMs)` 与**双侧对称**的网格最近邻（`RhythmJudge.cs:102-105`）。**round-2 验证的是一个对称模型**；生产的不对称模型（早侧永不 Miss，见 SPEC-4）**从未被试玩过一次**。
3. 现有反馈未区分测试者打出的 Miss 落在早侧还是晚侧。鉴于切片对称，无法据此推断生产早侧行为。

另有一项**负面实证**支撑 R-5：round-1 中 `RhythmJudge._metronome` 恒为 `null`，兜底分支原本返回 Good，使「节奏判定完全不可用」在玩法层表现为**「每一刀都踩准了」**——`RhythmJudge.cs:70` 注释逐字记录：「熬过了整轮试玩而未被发现」。**不可达的判定档位在玩家视角零症状**，这是把 MissUnreachable 升格为开发期错误的直接依据。

---

### 问题 1（SPEC-1，Critical）：C1-F2 延迟补偿在代数上完全抵消

**链条（每个环节均已核实原文行号）：**

1. `rhythm-core.md:201-213`（C1-F1）：`judgment_time` 取自 F3 的 `chargeStartDspTime` 或 `inputDspTime`，二者均明确是 **F3 原始、未经延迟调整的 DSP 时间戳**。
2. `audio-beat-clock.md:49`（F2 Core Rule 7）：F2 广播给上层的 `BeatEventData.beat_dsp_time` **已经**应用过延迟偏移——`adjusted_beat_time = raw + offset/1000`，处于「玩家感知时间域」。Rule 7 原文明确指示："C1 须在 F3 时间戳上加 `latency_offset_s` 完成坐标系对齐"。
3. `rhythm-core.md:230`（C1-F2 公式）据此写作：
   ```
   beat_offset_ms = (judgment_time + latency_offset_s − beat_dsp_time) × 1000
   ```

**代入步骤 2 的定义展开步骤 3：**

```
beat_offset_ms = (judgment_time + latency_offset_s − (raw_beat_dsp_time + latency_offset_s)) × 1000
               = (judgment_time − raw_beat_dsp_time) × 1000
```

`latency_offset_s` 在代数上**完全抵消**——结果与其取值无关，等价于从未做过任何延迟补偿。

**物理后果**：一个在**实际听到**拍点的时刻按键的玩家（这正是延迟补偿要服务的目标），其原始 DSP 时间 `judgment_time ≈ raw_beat_dsp_time + L_audio`。代入上式得 `beat_offset_ms ≈ L_audio`。即：**无论玩家踩得多准，只要设备有音频延迟，测得偏移恒等于该延迟值。** 典型蓝牙 100–250ms 下，`latency_offset_s > GoodHalfWindowMs_effective` 时**完美踩拍必然 Miss，无论怎么练**。

**双重补偿指示的落点共 7 处**（原清单 3 处，本次扩充）。前 3 处是公式/伪代码，第 4–5 处是**散文复述**——后者尤其危险：只改公式而留下散文，下一位读者会照散文把 bug 推回来。第 7 处是**可追溯性注册表条目**，发现最晚、危害方式又不同：它是 story 作者的权威输入，错误规格会以「需求」的身份直接进入冲刺。

| # | 位置 | 形态 |
|---|------|------|
| 1 | `rhythm-core.md:230` | C1-F2 公式本体 |
| 2 | `adr-d3:138` | Evaluation Algorithm 伪代码（`TryEvaluateEarly`） |
| 3 | `adr-d3:145` | Evaluation Algorithm 伪代码（`TryEvaluateLate`，与 #2 逐字相同） |
| 4 | **`rhythm-core.md:242`** | **散文**时域说明：「`judgment_time` 来自 F3 原始 DSP 时间戳，须加 `latency_offset_s` 转换到同一时域后再计算差值」 |
| 5 | **`adr-d3:48`** | **散文** Constraints 复述 Rule 7 的跨域对齐指示 |
| 6 | `audio-beat-clock.md:49` | F2 Core Rule 7 指示本体 |
| 7 | **`tr-registry.yaml` TR-rhythm-005** | **TR requirement 文本**逐字复刻旧公式：`"Beat-offset formula (judgment_time + latency_offset_s - beat_dsp_time)x1000; cross-time-domain alignment in perception domain"`。**本次写入阶段的 sanity check 新发现**——不在原 6 处清单内，也不在 GDD Sync Check 的扫描面内（该步只比对 GDD 与 ADR 的命名，不扫 TR 注册表）。 |

> **这第 7 处本身就是 R-e 风险的实证。** 它逃过了：原始缺陷报告（只看 GDD 与 ADR）、Step 5.7 GDD Sync Check（只比对命名）、以及本 ADR 自己的 6 处清单；最终是在写入完成后对**已改文件做反向 grep** 时才暴露。流程含义：**修订一个跨多文档的规格时，必须对旧表述做一次全仓 grep，而不是只核对预先列出的清单**——清单本身是上一轮认知的产物，必然不完整。已写入 Open Follow-up PR-6。

**为何至今没被任何验收用例捕获**——两层失效：

- `AC-C1-12`（`:829`）是唯一直接验证 C1-F2 的 AC，但其断言是「给定 `judgment_time=T`、`latency_offset_s=L`、`beat_dsp_time=B` → 结果 = `(T+L−B)×1000`」。这是**对公式自身的同义反复**，从未代入 Rule 7 定义的跨系统关系 `B = raw_B + L`，因此结构上不可能揭示净效应为零。
- `AC-C1-02b`（`:810`，SPEC-2 侧）更严重：它要求「偏移量 > `GoodHalfWindowMs_effective` 且 < `LateWindowMs` → Miss」。该区间在默认值下为空集，**构造不出满足前提的输入**。AC-C1-12 是无效验证；AC-C1-02b 是**不可满足的验收标准**。

**关键的规格冗余（本次 GDD Sync Check 新发现）**：钳位级联在 `rhythm-core.md` 中存在**两份完整副本**——`:264-288`（C1-F3 规格段）与 **`:380-400`（状态机伪代码段）**。两份各自独立复刻了 E7 步骤 2（`:387-391` 的「二次钳位」）与 Early 侧 `min`（`:393` 的 `_effectiveEarlyMs = min(EarlyWindowMs_authored, _effectiveGoodMs)`），且 `:398-400` 另有一份独立的 Assert 块。**任何级联修订必须同时改两处**，否则实现者会按状态机那份（更接近代码形态、更可能被照抄）写出旧行为。这份冗余本身就是「一串互不知情的补丁」能够累积的结构性原因。

**worked example 的真正问题（本次升级，原判「同义反复」偏轻）**：`:244-255` 示例 1 构造 `judgment_time = beat_dsp_time − 0.015 − 0.030`，展开后 `= raw_beat − 0.015`。即示例所模型化的，是一个相对**「原始调度拍点」**（而非「听到的声音」）下手的玩家。换言之，示例挑中了唯一一个能让双重补偿算出「正确」答案的参考系——这正是它们「通过」的原因，也是比同义反复更深的一层问题：**它们编码了一个错误的玩家模型**。修订时必须把参考系改为「玩家听到声音的时刻」，即 `judgment_time = raw_beat + L_audio`。

`rhythm-core.md:259`「延迟安全约束」描述的正是本 bug 的症状，却归因为「需要 FE7 钳位校准范围」的使用限制。**修正根因后该段落应整体移除**（见修正 3，并注意其无障碍后果已升级）。

### 问题 2（SPEC-2，High）：Miss 评级在默认调参值下为空集

`rhythm-core.md` 规则 3（`:74-76`）：拍点后 `LateWindowMs`（默认 **100ms**）内到达的输入立即评级为 Good 或 Miss；超出者被**丢弃，不产生任何 `RhythmJudgmentEvent`**。评级分界线是 `GoodHalfWindowMs_effective`（authored 默认 **120ms**，`:765`）。

可达 Miss 区间 = `(GoodHalfWindowMs_effective, LateWindowMs]`。默认值代入：`(120, 100]` —— **下界大于上界，区间恒为空，与 BPM 无关**。

钳位级联（`:277`）只在 `C < 120` 时压低 `GoodEff`，即 **BPM > 123.97**（HalfBeat）；要降到 100 以下（让区间转为非空）需 **BPM > 148.5**。M0 §5 主力测试点 BPM **60 / 120** 均 < 148.5——**Miss 在整个 M0 验证范围内恒不可达**，与 round-1 实证吻合。

根因是 **`LateWindowMs_authored(100) < GoodHalfWindowMs_authored(120)` 这两个默认值的相对顺序**，与网格钳位无关；网格钳位只在极高 BPM 下「意外地」开一条窄缝。

**附带发现**：`LateWindowMs` **从未进入钳位级联**——全文仅 `:765` 一处 authored 声明，不存在 `LateWindowMs_effective`。其后果是：Android BPM 200 下 `C = 74` 而 `LateWindowMs = 100 > C`，**现行规格已在违反 P1**（混叠）。因此修正 2 为 Late 侧引入钳位，实质上同时是一项 **P1 bug 修复**，应显式记功。

### 问题 3（SPEC-3，Medium）：量程声明三处不一致（问题 1 的派生物）

| 来源 | 声明的 `latency_offset` 量程 |
|------|------------------------------|
| `rhythm-core.md:238`（变量定义表） | `[-0.2, 0.2]` 秒（±200ms） |
| `rhythm-core.md:259`（延迟安全约束） | 隐式要求 `< GoodHalfWindowMs_effective`（默认 120ms） |
| `audio-beat-clock.md:306`（F2-F5 变量表） | `[-200, 200]` ms（与 `:238` 一致） |
| `docs/registry/architecture.yaml:87`（`latency_offset_ms` state） | `range [-200, 200]`（第 4 个权威源，与 `:238` 一致） |

`:259` 与另外三处矛盾。此矛盾是问题 1 的**症状**：一旦抵消 bug 被修正（完美踩拍偏移恒为 0，与延迟值无关），`:259` 的论证前提消失。

### 问题 4（SPEC-4，**High — 建议优先级 ≥ SPEC-2**）：早侧评级失声，Perfect 结构性不可达

**ADR-D3 伪代码的调用时序（已逐行核实）：**

```
OnHalfBeatLookahead(beat):            // 在 T − LookaheadMs 触发
    _currentBeatDspTime = beat.beat_dsp_time
    _lateWindowExpiry   = now + (LookaheadMs + LateWindowMs)/1000
    _lateWindowActive   = true
    TryEvaluateEarly()                // ← 早侧求值的【唯一】调用点

OnAttackInput(e):
    _pendingInput = e
    if _lateWindowActive: TryEvaluateLate(e)      // ← 只调 Late

TryEvaluateLate(e):
    ...
    if offset < 0 || offset > LateWindowMs: return    // ← 早侧输入在此被挡掉，且【不清缓冲】

Update():
    if _lateWindowActive && now > _lateWindowExpiry:
        _lateWindowActive = false
        _pendingInput     = null                      // ← 静默丢弃
```

**后果链**：落在 `(T − LookaheadMs, T)` 的输入写入 `_pendingInput`；`TryEvaluateLate` 因 `offset < 0` 立即返回且不消费缓冲；`TryEvaluateEarly` 在本拍点内**永不被再次调用**。按默认值（`LookaheadMs=100`、`LateWindowMs=100`、120BPM）：下一次 lookahead 在 `T+150` 才触发，而 `_lateWindowExpiry` 在 `T+100` 就把缓冲 `= null`。该输入**从未被评级，被静默丢弃**。

**早侧实际可被评级的区间**：只有 arm 时刻（`T − 100`）缓冲中已存在的输入才会经 `TryEvaluateEarly` 求值，且须满足 `offset ≥ −EarlyWindowMs(120)` 且 `offset ≤ 0`，即 `t_in ∈ [T−120, T−100]`——**仅 20ms 薄片**，且其 `|offset| ∈ [100,120]`，全部 > `PerfectHalfWindowMs(50)` 且 ≤ `GoodHalfWindowMs(120)`。

> **早侧 Perfect 结构性不可达；早侧唯一可能的评级是 Good；系统性奖励「打晚」。**

**这不是「早侧宽容」，是「早侧失声」**（本次最重要的认知纠正）：

- 晚侧有**三值**退化梯度：Good → Miss → 丢弃。
- 早侧只有**两值**：Good → 丢弃。中间那一档不存在。
- 而「丢弃」意味着 `EmitAndClear` 不被调用——而它是 `AnimancerAttackPlayer.Play(...)` 的**唯一**调用点（`adr-d3:152-156`，已逐行核实）。故早侧越界的后果是：**无 `RhythmJudgmentEvent`、无攻击动作、无早/晚方向提示**（`accessibility-requirements.md:87` 要求的认知无障碍手段）。

这正是 `rhythm-core.md:29` 用「任何情况下都不会」明令禁止的失败态——「我输入了，但游戏没有反应」。**任何「保留现状即保留宽容」的论证都建立在错误前提上。**

**交叉佐证一**：切片实现刻意绕开了这条路径。`RhythmJudge.cs:17-18` 注释：「切片简化：用『网格最近邻』直接判定（等价于生产的 look-ahead 预缓冲，**避开输入锚定过去拍的 bug**）」——切片作者已察觉此处有问题并绕行，但未升格为规格缺陷登记。

**交叉佐证二（本次 GDD Sync Check 新发现，最重要）：SPEC-4 曾被 GDD 评审察觉，并被降级为非阻塞后遗忘。** `design/gdd/reviews/rhythm-core-review-log.md:14`（第五轮 lean 复查，C1 GDD 正式获批那一轮）逐字记录：

> 「两项推荐修订均为非阻塞：① 补充 **LookaheadMs 与 EarlyWindowMs 时序盲区**说明（建议在 ADR-D3 中锚定评级到 `beat_dsp_time` 而非 event receive time）；…」

「LookaheadMs 与 EarlyWindowMs 时序盲区」正是 SPEC-4。评审者看到了它、起了正确的名字、给出了方向正确的建议（锚定到 `beat_dsp_time`），然后判为**非阻塞**——于是 C1 GDD 带着它获批，该修订至今未落地。

**这项发现改变了 SPEC-4 的性质**：它不是新发现的疏漏，而是一次**已知风险被错误定级**的结果。对流程的直接含义——「时序盲区」类缺陷不适合判非阻塞，因为它们的后果（静默吞输入）在玩家视角无症状，不会在后续评审或试玩中自行冒头。建议把本条写入 M0 §2.1 的风险台账，并作为 `/architecture-review` 的一条检查项：**凡被标记为「时序盲区 / timing blind spot」的推荐修订，不得以非阻塞结案。**

**SPEC-4 是修正 4 的前置依赖**：在 SPEC-4 修复前，早侧 Miss 带在运行时**观测不到**，修正 4 的效果无法验证。

### 问题 5（SPEC-5，Low）：E7 文本缺陷三处

1. **`:645` 方向错误**：「Beat 模式临界 BPM ≈ 93」。`:274` 确认 Beat 模式 `gridIntervalMs = fullBeatMs`（间隔**加倍** ⇒ 更宽松），临界 BPM 应**加倍**为 `60000/(2×81) ≈ 370`。作者把 185.19 **除以** 2 而非乘 2。该句自带的「此场景实际较少出现」缓和语正是它潜伏至今的原因——结论恰好相反：Beat 模式几乎不可能触发坍缩。
2. **`:640` 与 `:644-645` 精度不一致**：`:640` 写「BPM > 187」（源自漏掉 `−1` 的 `halfBeatMs/2 < 80` ⇒ 187.5），`:644` 推导带 `−1` 得 185.19，`:645` 取上整 186。非硬矛盾，但三个数字并存，须统一为公式派生。
3. **`:661-662` 在修正 2 后将变假**：现行文本「坍缩后 `|offset| ≤ PerfectEff` → Perfect；超出 → Miss（Good 区间消失）」在**现行**规格下为真（Android BPM 200：`C=74`、`LateWindowMs=100`，Miss 带 `(74,100]` 非空）——但它**之所以为真，恰因 `LateWindowMs=100 > C=74` 本身违反 P1**。一旦修正 2 把 Late 钳到 `C`，Miss 带归零，前沿以上应为 **Perfect 单一档**，而非 Perfect/Miss 二元。该段须随修正 2 同步改写。

### 与切片的关系

`RhythmJudge.cs` 与 `LatencyCalibration.cs`（本轮已修正，见文件头注释）**不复刻 SPEC-1**：切片判定网格取自 `SliceMetronome` 的原始调度时间（不含延迟调整），判定时只对输入侧减一次校准偏移（`perceived = inputDsp − bias`，`:90-92`），完美踩拍时偏移天然归零。切片亦不复刻 SPEC-4（网格最近邻，双侧对称）。

切片可作为修正 1 与修正 4 的**可运行参考实现**，但不可直接复制：切片无 F2 等价的「预调整时间戳广播」层，也无 LateWindow 状态机。

---

## Decision

### 修正 1：移除 C1-F2 公式中重复的延迟项

公式由 `(judgment_time + latency_offset_s − beat_dsp_time) × 1000` 改为：

```
beat_offset_ms = (judgment_time − beat_dsp_time) × 1000
```

`judgment_time`（F3 原始域）与 `beat_dsp_time`（F2 已调整域，Rule 7）直接相减即完成跨域对齐——调整已由 F2 单侧施加在 `beat_dsp_time` 上，C1 不需要、也不应该再施加第二次。

**须同步修订上表 7 处全部**（含 2 处散文与 1 处 TR 注册表条目），不得只改公式。`:244-255` 两个 worked example 须按「玩家听到声音的时刻」重构参考系。

**实现护栏（unity-specialist 要求，新增）**：减法必须先在 `double` 域完成，只对最终的小量级毫秒差值做 `float` 窄化：

```csharp
// 正确（参照 RhythmJudge.cs:90-92 的切片写法）
double diff     = judgmentTime - beatDspTime;      // double 域
float  offsetMs = (float)(diff * 1000.0);          // 只窄化最终小量级结果

// 禁止：将原始 DSP 时间戳本身转 float 后再相减
```

理由：FMOD DSP 时钟自系统启动累积，长会话后数值变大；若先窄化再相减，约 15–20 分钟后 float 精度即侵蚀到 0.1ms 容限以下（Validation Criteria 的断言门槛）。该护栏应写入 `rhythm-core.md` 实现指引并在 `adr-d3` 伪代码中体现。

**不削弱既有禁令（R-6）**：`registry:539-543` 的 `direct_beat_offset_at_input_time` 禁令**完全有效，继续保留**。该禁令针对的是「在输入时刻用系统时间插值估算偏移」引入的抖动误差；本 ADR 诊断的是**代数抵消**，两类误差互不相关。但该禁令的 `why` 措辞易被误读为「必须加 offset」，须补一句澄清：禁令约束的是**时间源**（必须用 DSP 时间戳，不得用 `Time.realtimeSinceStartup` 插值），不是**项数**。

### 修正 2（**本次重写**）：统一钳位级联，以「剩余预算占比」保证 Miss 结构性非空

> ⚠ **本修正是对用户已批准范围（「三条修正保持不变，只补结构与证据」）的唯一超范围改动。**
> 理由：原修正 2 **在 Android 高 BPM 下复刻了它要修的同一 bug 类**。
> 原级联把天花板压到 `C − MissHeadroomMs`，使 E7 坍缩触发点从 Android BPM ≥ 186 提前到 **BPM > 135.13**
> （两条件数学同一：均为 `C < 110`）。而 E7 步骤 1 又把 `GoodEff` 抬回 `PerfectEff = 80`，于是
> **E7 触发 ⟺ `clamp(100, GoodEff + 30, C)` 的下界 110 > 上界 106.14**——正是 SPEC-2 所批判的
> 「区间下界大于上界」。BPM 140 Android 下 Miss 带天花板仅 26.14ms < 30，`MissHeadroomMs = 30`
> **结构性不可满足**。保留原文将把 SPEC-2 的同类缺陷写进权威文档。

**根因不是优先级，是表达形式**：把三个**绝对毫秒常数**（`PerfectHalfWindowMs`、`platformMinPerfectMs`、`MissHeadroomMs`）塞进一个随 BPM **线性收缩**的预算 `C`。反转是「一串互不知情的补丁」（E7 步骤 1 → `:287` 步骤 1b → 原修正 2 → E7 步骤 2）叠加的必然产物，而非任一条单独写错。

#### 2a. 降级优先序：**P1 > P2 > P3**

- **P1 居首**，因其为正确性不变量（见 C-a），不是手感取舍。与 `rhythm-core.md:659` 既有声明一致。
- **P2 优先于 P3**，因 **P2 是 P3 有意义的前提**：Perfect 窗口低于平台 DSP 噪声底时，Perfect 变成抽签，而 Miss 同样被噪声污染——**随机的档位分配比缺失的档位更坏**。
- 预算内部，**Good 让位于 Miss**：二元 Perfect/Miss 已被 `:661-664` 明文祝福；二元 Perfect/Good 从未被祝福。

但「P3 最先牺牲」的推论**不成立**——由 C-b，P2 与 P3 共享同一条可行性前沿。

#### 2b. 新增旋钮（`:769` Tuning Knobs 表追加两行）

| 旋钮 | 默认 | 范围 | 效果 |
|------|------|------|------|
| `MissHeadroomMs` | 30ms | [15, 60] | Miss 可达区间的**目标**宽度（**上限**语义，非下限）|
| `MissBudgetShare` | 0.5 | (0, 1] | Miss 可占用「剩余预算」的比例。`< 1` 保证三档同时非空；`≤ 1` 保证级联永不反转 |

**关键形式变更**：`MissHeadroomMs` 由「绝对毫秒**下限**」改为「绝对毫秒**上限** + 预算占比约束」：

```
MissHeadroomMs_effective = min(MissHeadroomMs_authored, missBudgetMs × MissBudgetShare)
```

#### 2c. 统一级联（**替换 `:264-288` 与 `:380-400` 两份副本**，并吸收 E7 全部语义）

> ⚠ **两处都必须改**。`rhythm-core.md` 有两份完整的级联副本（见 Context SPEC-2 §规格冗余）：`:264-288` 是规格段，`:380-400` 是状态机伪代码段（含其独立的 E7 二次钳位 `:387-391`、Early `min` `:393`、Assert 块 `:398-400`）。只改规格段会留下一份更像代码、更可能被照抄的旧版本。**建议同时消除这份冗余**：状态机段改为引用 `:264-288`，不再复刻公式。

```
halfBeatMs     = (30.0 / currentBpm) * 1000
fullBeatMs     = halfBeatMs * 2
gridIntervalMs = (quantizationMode == Beat) ? fullBeatMs : halfBeatMs

// ① P1 —— 绝对天花板
C = gridIntervalMs / 2 - 1

// ② P2 —— Perfect 平台保底（吸收原 E7 步骤 2）
PerfectHalfWindowMs_effective = min(PerfectHalfWindowMs_authored + perkPerfectDelta, C)
PerfectHalfWindowMs_effective = max(PerfectHalfWindowMs_effective, platformMinPerfectMs)
if PerfectHalfWindowMs_effective > C:
    PerfectHalfWindowMs_effective = C
    degradation |= PerfectFloorSacrificed

// ③ P3 —— 剩余预算占比（取代「绝对毫秒下限」）
missBudgetMs             = C - PerfectHalfWindowMs_effective
MissHeadroomMs_effective = min(MissHeadroomMs_authored, missBudgetMs * MissBudgetShare)
if MissHeadroomMs_effective < MissHeadroomMs_authored: degradation |= MissHeadroomCompressed
if MissHeadroomMs_effective <= 0:                      degradation |= MissUnreachable

// ④ Good（晚侧）（吸收原 E7 步骤 1）
GoodHalfWindowMs_effective = min(GoodHalfWindowMs_authored + perkGoodDelta,
                                 C - MissHeadroomMs_effective)
GoodHalfWindowMs_effective = max(GoodHalfWindowMs_effective, PerfectHalfWindowMs_effective)

// ⑤ Good（早侧）—— 仅修正 4 需要；EarlyLenienceRatio = 1.0 时与 ④ 恒等
GoodEarlyHalfWindowMs_effective = min(GoodHalfWindowMs_effective * EarlyLenienceRatio,
                                      C - MissHeadroomMs_effective)
GoodEarlyHalfWindowMs_effective = max(GoodEarlyHalfWindowMs_effective,
                                      PerfectHalfWindowMs_effective)

// ⑥ 判定/丢弃边界 —— 下界恒 ≤ 上界
LateWindowMs_effective  = clamp(LateWindowMs_authored,
                                GoodHalfWindowMs_effective + MissHeadroomMs_effective, C)
EarlyWindowMs_effective = clamp(EarlyWindowMs_authored,
                                GoodEarlyHalfWindowMs_effective + MissHeadroomMs_effective, C)
// 若修正 4 未被采纳，⑥ 的 Early 行退回现状：
//     EarlyWindowMs_effective = min(EarlyWindowMs_authored, GoodHalfWindowMs_effective)
```

**无反转性证明**（满足 R-3）。只需证 ④ 的出口恒满足 `GoodEff ≤ C − MH_eff`：

- 情形 1（`min` 胜出）：`GoodEff ≤ C − MH_eff`，直接成立。
- 情形 2（`max` 把 `GoodEff` 抬到 `PerfectEff`）：由 ③，`MH_eff ≤ missBudgetMs × MissBudgetShare ≤ missBudgetMs = C − PerfectEff`（用到 `MissBudgetShare ≤ 1`），故 `PerfectEff + MH_eff ≤ C`。∎

于是 ⑥ 两个 `clamp` 的下界恒 ≤ 上界 `C`。**无反转只依赖 `MissBudgetShare ≤ 1`。**

**附带性质**：`MissBudgetShare < 1` ⇒ 前沿以下**三档恒非空**（④ 的 `max` 只在 `MissBudgetShare > 1` 时才可能触发）。

**数值示例（`MissBudgetShare = 0.5`）：**

| 场景 | `C` | `PerfectEff` | `missBudget` | `MH_eff` | `GoodEff` | Miss 带 |
|---|---|---|---|---|---|---|
| 120BPM / HalfBeat / PC | 124 | 50 | 74 | 30 | 94 | (94, 124] = 30ms |
| 120BPM / HalfBeat / Android | 124 | 80 | 44 | 22 | 102 | (102, 124] = 22ms |
| **140BPM / HalfBeat / Android** | 106.14 | 80 | 26.14 | 13.07 | 93.07 | (93.07, 106.14] = **13.07ms** |
| 200BPM / HalfBeat / Android | 74 | 74 (flag) | 0 | 0 (flag) | 74 | 空 → Perfect 单一档 |

BPM 140 Android 一行即原修正 2 发生反转之处——新形式下三档齐全。

#### 2d. 可观测性：三级分治（满足 R-5）

| 标志 | 语义 | 处置 |
|------|------|------|
| `MissHeadroomCompressed` | Miss 带窄于 authored 目标 | Warning + 遥测，**非阻塞**（预算紧张时的正常降级）|
| `PerfectFloorSacrificed` | `platformMinPerfectMs` 承诺被放弃 | 维持现有 Warning（`:657`），**纳入内容期门禁** |
| `MissUnreachable` | Miss 带为空 | **升格为内容期错误**（原仅 Warning）|

**MissUnreachable 升格的依据**：该状态在玩家视角**完全无症状**。直接实证是 round-1 的 `_metronome == null` 让每刀判 Good 而「熬过了整轮试玩而未被发现」（`RhythmJudge.cs:70`）。处置三路并行：

1. **Odin Validator 编辑期门禁**（限 Editor 程序集，遵 C-c）：对内容 BPM × 目标平台 × 量化模式的笛卡尔积求值级联，`MissUnreachable` 置位即校验失败。
2. **开发构建 `Debug.LogError`**（`#if UNITY_EDITOR || DEVELOPMENT_BUILD`）。
3. **内容侧 BPM 上限公式**，供关卡/音乐作者直接使用：
   ```
   BPM_max(platform, mode) = (mode == Beat ? 60000 : 30000) / (2 × (platformMinPerfectMs + 1))
   ```
   Android / HalfBeat ⇒ **185.19**（与 `:644` 一致）；Android / Beat ⇒ **370.37**（即 SPEC-5 第 1 条的正确值）；PC / HalfBeat ⇒ 848.0。

**对 M0 无阻塞**：BPM 120 / HalfBeat / Android 下 `C = 124`、`platformMin = 80`、`missBudget = 44 > 0`，三档齐全。

#### 2e. `LateWindowMs_authored` 的去向

新级联下 ⑥ 的下界 `GoodEff + MH_eff` 通常 > 100，`LateWindowMs_authored` 几乎恒被下界顶替，已失去 authoring 意义。两个选项：

- **(i)** balance pass 重新 author 为 `≥ GoodHalfWindowMs + MissHeadroomMs`；
- **(ii)**（**推荐**）**取消独立 authored 值，直接定义 `LateWindowMs_effective = C`**。这把生产模型的判定几何收敛到切片**已被 round-2 验证**的形状，一次性消除整类反转来源。副作用：留下 2ms 接缝 `(C, gridIntervalMs/2]`，落其上的输入按现状丢弃，须在 E5 写明。

**连带必须同步的 E5**：`:619` 的权威过期判定用 **authored** 值（`inputDspTime − beatDspTime > LateWindowMs / 1000`）。若引入 `LateWindowMs_effective`，authored=100 / effective=124 时 `offset=110` 的输入按 `:619` 已过期丢弃、按新 effective 又该判 Miss——**直接矛盾**。`:621` 的 DSP 粒度边界处置与 `AC-C1-04` 须一并改为引用 effective 值。（`:623` 已自认 ADR-D3 用 `Time.realtimeSinceStartup` 与本规范矛盾，须同步修正——与修正 1 的时间源澄清同一处。）

### 修正 3：删除量程自相矛盾段落（**本次升级为无障碍关键项**）

`rhythm-core.md:259`「延迟安全约束」整段删除（论证前提已被修正 1 消除）；`:238` 的 `[-0.2, 0.2]` 保留为权威声明，追加交叉引用指向 `audio-beat-clock.md:306` 与 `registry:87`，明确四处为同一量程的不同单位表示。

**为何是无障碍关键项（本次纠正）**：此前假设「保守校准 ⇒ 玩家系统性打早 ⇒ 早侧不对称吸收残余误差」。**符号方向是反的。** 修正 SPEC-1 后，听声下手的玩家 `offset = L_true − L_offset = 残余误差 e`。校准链上四处截断**全部单向压向补偿不足**（`L_offset < L_true`）：

1. `LatencyCalibration.cs:43` 的 `±200ms` clamp，对无线音频典型 250–300ms 不足；
2. 网格最近邻的折叠量程（`LatencyCalibration.cs:9-12` 自述，真实 +180ms 在半拍网格下测得 −70ms）；
3. **`rhythm-core.md:259` 强制的 120ms 上限**；
4. 跳过校准 = `L_offset = 0`。

`L_offset < L_true ⇒ e > 0 ⇒` **判为晚**。残余误差的主导符号是**晚**，不是早。结论有两层：

- 早侧宽容（若有）给错了一侧，对 R-AUDIO-01 目标人群（音频延迟设备用户）**毫无帮助**；
- `:259` 的 120ms 上限是四条「补偿不足」路径中**唯一由规格强加**的那条，删除它直接扩大高延迟设备的可补偿范围——**故修正 3 属无障碍关键项，不是文档清理**。

**对 FE7 义务的改变**：`:259` 原文声明「约束执行由 FE7 校准界面负责」，要求 FE7 对 `|latency_offset_ms| ≥ 120ms` 显示警告并拒绝或钳位。删除后 FE7 的义务边界从 **120ms 放宽到 ±200ms**。须在修订时显式写入，否则 FE7 实装者会沿用旧上限。

### 修正 4（裁决 A）：早侧部分对称化 + FQA 具名合约

**裁决**：现状**两者皆非**——既非「刻意的设计宽容」，也非「纯粹遗漏」，而是**第三种**：**刻意的微决策 + 未审计的宏观后果**。`:285-287` 的注释证明钳位是刻意的（防「BPM 压缩 Good 窗口把已接收输入事后改判 Miss」=时序一致性顾虑），但该理由**狭窄**（针对 BPM 变化瞬间的重判定）而后果**全局**（任何 BPM / 平台 / perk 下早侧永不 Miss），且从未被论证、命名或进入任何验收判据。

**根因与 SPEC-1 同属一类**：`EarlyWindowMs` 是**双职旋钮**——既是缓冲准入闸门，又经 `:287` 间接成为评级上界。语义职责未分离。

**处置：(c) 部分对称化**，三机制：

- **C-1** 把 `:287` 的 `min` 改为带下界的 `clamp`（镜像晚侧），即统一级联 ⑥ 的 Early 行。
- **C-2** 新增 `EarlyLenienceRatio`（见下表），评级改为**带符号比较**；`PerfectHalfWindowMs_effective` **两侧保持严格对称**，不受本旋钮影响。
- **C-3** 把不对称的边界具名为 **FQA 合约**（下文），并新增「禁止静默吞输入」规则。

| 旋钮 | 默认 | 范围 | 效果 |
|------|------|------|------|
| `EarlyLenienceRatio` | **1.0**（完全对称）| [1.0, 1.5] | 早侧 Good 半窗相对晚侧的放宽倍率。权威值归 balance pass；上界 1.5 为建议量级 |

**保序性已核验**：`max(a) ≥ max(b) + c` 形式成立，⑤ 的 `max` 不会破坏 ⑥ 的下界 ≤ 上界。

#### FQA 合约（Forward-Quantization Asymmetry Contract）

> 《玉兔战歌》的节奏判定允许早/晚两侧 **Good** 窗口不等宽，但该不对称受以下三条约束：
>
> 1. **有界且显式。** 不对称只能经单一具名旋钮 `EarlyLenienceRatio` 产生，默认 1.0（完全对称），并有明确上界。**禁止**由钳位级联的副产物隐式产生任何不对称。
> 2. **Perfect 对称。** `PerfectHalfWindowMs_effective` 在早/晚两侧恒等。节奏精度的最高档不设方向偏好。
> 3. **禁止静默吞输入（No Silent Swallow）。** 任何被拍点缓冲准入的输入，必有且仅有一次 `RhythmJudgmentEvent`，其 `OffsetMs` 符号忠实反映早/晚方向。两侧的退化梯度都必须以**可达的 Miss 档**收尾，而非以沉默收尾。被判 Miss 的输入**仍执行攻击动作**（`adr-d3:156` 既有行为，`Evaluate` 注释「攻击仍执行；伤害倍率 0.5x」）。

**合约落位 9 处**：`rhythm-core.md` 规则 3/4 段（`:66`/`:74-87`）、`:290-295` 不变量块、**`:380-400` 状态机副本（含 `:393` Early `min` 与 `:398-400` Assert 块）**、Tuning Knobs 表（`:764-774`）、E5（`:613-623`）、E7（`:638-664`）、`adr-d3` Evaluation Algorithm（`:80`/`:89`/`:139`/`:146`）、**`adr-d3:222-223` `RhythmConfigSO` 字段声明**、`registry` interfaces 段。

#### 新不变量块（替换 `:290-293`，Assert 锚点为 `:295`）

```
PerfectHalfWindowMs_effective ≤ GoodHalfWindowMs_effective      ≤ C
PerfectHalfWindowMs_effective ≤ GoodEarlyHalfWindowMs_effective ≤ C
GoodHalfWindowMs_effective      + MissHeadroomMs_effective ≤ LateWindowMs_effective  ≤ C
GoodEarlyHalfWindowMs_effective + MissHeadroomMs_effective ≤ EarlyWindowMs_effective ≤ C
0 ≤ MissHeadroomMs_effective ≤ C - PerfectHalfWindowMs_effective        其中 C = gridIntervalMs/2 - 1
```

> **锚点更正**：本 ADR 原文引作 `:291-293`。核实后：不变量块在 `:292-293`，而「`RhythmJudger` 须在 `OnMeterChanged` 末尾 `Assert`」的要求在 **`:295`**。
> **另有第二处 Assert 块**：`:398-400`（状态机副本内，已带 `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 包裹，断言 `_effectivePerfectMs <= _effectiveGoodMs` 与 `_effectiveEarlyMs <= _effectiveGoodMs`）。**两处 Assert 都须按新不变量块更新。** 该处既有的条件编译写法（含「Unity 6.3 Release Build 中 `Debug.Assert` 通过 `UNITY_ASSERTIONS` 缺失被剥离」的注释）可直接沿用，无需改动包裹方式。

**修正 4 的两项前置**：(a) **SPEC-4 必须先修**，否则早侧 Miss 带观测不到；(b) **M0 §2.2 修订流程**（C-e）。

### 修正 5（SPEC-4）：早侧求值时序 —— 本 ADR **登记缺陷，不裁决修复路径**

修复方向属 technical-director 决策域：合并为单一 `TryEvaluate(e)`，输入到达即对已 armed 拍点求值，取代「早侧只在 lookahead 时刻扫描一次」。

**硬约束（本 ADR 在此立界）**：**不得**用「把 `EarlyWindowMs_effective` 钳到 `LookaheadMs`」回避。那只是把 20ms 薄片重新描述为「设计意图」，既不恢复早侧 Perfect，也不满足 R-4。

**须 TD 复核的三项**（见 Open Follow-up）：SPEC-4 修复路径与 PL2/P1 表现层调度的关系；带符号评级比较对 `RhythmJudgmentEvent` 结构与下游 C2/PL2/P1 的影响；`LastClampDegradation` 的暴露方式。

### 修正 6：文本与注册表修正（低风险，可与修正 1 同批）

| # | 位置 | 改动 |
|---|------|------|
| 6a | `rhythm-core.md:645` | 「Beat 模式临界 BPM ≈ 93」→ **≈ 370**，并删除「此场景实际较少出现」的误导性缓和语（结论相反）|
| 6b | `rhythm-core.md:640/:644/:645` | 三处 BPM 数字统一为 2d 的 `BPM_max` 公式派生 |
| 6c | `rhythm-core.md:661-662` | 改为「前沿以上退化为 **Perfect 单一档**」（见 SPEC-5 第 3 条）|
| 6d | `adr-d3:115` | 注释 `// 覆盖旧值（单槽，同一半拍内先到优先）` → **后到优先**。「覆盖旧值」本身即 last-in-wins，代码与 `rhythm-core.md:83-87` 规则 4 一致；**错的只是括号里四个字**，属注释笔误，非语义冲突，不需独立裁决 |
| 6e | `docs/registry/architecture.yaml:76` | `interface` 由 `reset on Miss or death` 改为 `not reset on Miss; reset on death only`，并填 `revised:` |
| 6f | `docs/architecture/tr-registry.yaml` **TR-rhythm-013** | requirement 文本逐字复刻了 E5 的 authored 判定（`inputDspTime − beatDspTime > LateWindowMs/1000`）。随 2e 的选项定稿改为 effective 值，并填 `revised:`。**遵 `docs/CLAUDE.md`：只改 requirement 文本，永不重编号。** |
| 6g | `rhythm-core.md:774` | 调参指引「再调 `EarlyWindowMs` / `LateWindowMs` 确定缓冲宽容度」——若采纳 2e(ii)（取消 `LateWindowMs_authored`），此句须改写，并追加三个新旋钮的调参顺序建议 |
| 6h | `adr-d3:222-223` | `RhythmConfigSO` 字段声明须追加 `MissHeadroomMs` / `MissBudgetShare` / `EarlyLenienceRatio`（遵 ADR-D4 的 6.3 `[SerializeField]` fields-only 约定）|
| 6i | `adr-d3:264` | 其 post-MVP 方案 `_lateWindowExpiry = _currentBeatDspTime + LateWindowMs/1000.0` 与 `rhythm-core.md:619` 的权威 DSP 判定一致，但 `:109` 的实际伪代码用 `Time.realtimeSinceStartup`——即 `:623` 自认的矛盾。随修正 1 的时间源澄清一并落地（把 post-MVP 方案提升为规范） |
| **6j** | `tr-registry.yaml` **TR-rhythm-005** | **写入阶段 sanity check 新发现**（SPEC-1 第 7 处站点，见 Context SPEC-1 站点表）。requirement 文本持有旧的双重补偿公式。改为 `(judgment_time - beat_dsp_time)x1000`，并写明「延迟补偿一次且仅一次，位置在 F2 Rule 7」与 double 域相减护栏。**不重编号，填 `revised:`。** |
| **6k** | `tr-registry.yaml` **TR-rhythm-006** | **同上新发现。** 原文「writes runtime fields `_effectivePerfect/GoodMs`」只列 2 个字段，统一级联产出 6 个 effective 字段 + `_lastClampDegradation` 标志位。补齐字段清单、声明「唯一权威 = `rhythm-core.md` C1-F3 步骤 1，任何其他位置不得持有副本」、并记入 P1 > P2 > P3 优先序。 |
| **6l** | `rhythm-core.md:634` + 其后的说明块 | **写入阶段全仓 grep 新发现，且是 6k 的同源站点。** 接口节拍表原写「`GetPerfectWindowDeltaMs`/`GetGoodWindowDeltaMs` … 结果存 `_effectivePerfect/GoodMs`」——把 perk delta 的**缓存**与级联的**输出**混为一谈。perk delta 是级联步骤 ②④ 的**输入**；若照此实现，带 perk 加成的窗口会被直接写进生效字段，**绕过 P1 天花板 `C` 与 P3 Miss 预算**。改为存入 `_cachedPerkPerfectDelta`/`_cachedPerkGoodDelta` 后触发 `ApplyClampCascade(...)`，并补说明块点明 6 个 effective 字段 + degradation 标志位。TR-rhythm-006 的旧文本很可能即抄自本行。 |
| **6m** | `tr-registry.yaml` **TR-rhythm-017** | **同上新发现，且含一处事实错误。** 原文「binary Perfect/Miss degrade accepted」与 SPEC-5 第 3 条（`6c`）矛盾：超过 `BPM_max` 时 P2 的 Perfect 保底吃掉整个天花板 `C`，**Good 与 Miss 同时消失，退化为 Perfect 单一档**，不是 Perfect/Miss 二档。同时「inline logic must run before Assert」已不成立（E7 逻辑被级联步骤 ②④ 吸收，结构上必然先于 Assert）。改写为 `BPM_max` 公式派生 + `MissUnreachable` 定性为内容配置错误（非可接受降级）+ 三路处置。 |

**6e 的性质（已查实）**：这是**注册表无来源误写**，不是「已 Accepted 的 ADR 与 GDD 冲突」。其 `adr:` 字段指向 ADR-D3，但 ADR-D3 全文**无任何 combo reset-on-Miss 语义**（`:152-156` 只捕获 `comboBefore`，`:201` 只声明属性）。GDD 侧有十余处反证（`rhythm-core.md:133/:351/:770`、`combo-system.md:56/:186` 规则 7「重置只作用于图位置，绝不清零段数」、AC-C2-21）。注册表是孤证。**无需触碰 ADR-D3。**

该矛盾潜伏至今的结构性原因正是 SPEC-2 本身——Miss 恒为空集，这条死规格从未被执行触发。本 ADR 让 Miss 可达后，它从纸面矛盾变成**活的行为分歧**，故必须在同批修掉。

### Architecture Diagram

```
时域对齐（修正 1 后）
─────────────────────────────────────────────────────────────────
  F2 BeatClock ──── raw_beat_dsp ──[+ latency_offset_s]──► beat_dsp_time
                                   (F2 单侧施加, Rule 7)      │  玩家感知域
                                                              ▼
  F3 Input ─────── inputDspTime ──────────────────────────► judgment_time
                   (原始 DSP 域, 不再二次调整)                 │
                                                              ▼
                              beat_offset_ms = (judgment_time − beat_dsp_time) × 1000
                                               ↑ double 域相减, 只窄化最终结果

判定窗口预算（统一级联，单侧示意；C = gridIntervalMs/2 − 1）
─────────────────────────────────────────────────────────────────
  0 ├──── Perfect ────┼──── Good ────┼──── Miss ────┤ C ┤ 丢弃
    │   ②  PerfectEff │  ④ GoodEff   │  ③ MH_eff    │   │
    │   ≥ platformMin │              │ = min(auth,  │ ⑥ LateEff
    │     (P2)        │              │  budget×s)   │   (P1 天花板)
    └─────────────────┴──────────────┴──────────────┘
      missBudgetMs = C − PerfectEff ──────────────►
      P3 从【剩余预算】切份额, 故 MH_eff ≤ budget ⇒ 级联永不反转

  早侧（修正 4 后）与晚侧同构, 仅 ⑤ 的 Good 半窗乘 EarlyLenienceRatio(默认 1.0 ⇒ 几何对称)
  Perfect 两侧恒等(FQA 合约 §2); 两侧梯度均以【可达 Miss】收尾(FQA 合约 §3)
```

### Key Interfaces

```csharp
// RhythmConfigSO —— 新增 authored 字段（遵 ADR-D4：6.3 [SerializeField] fields-only）
[SerializeField] private float _missHeadroomMs    = 30f;   // [15, 60]
[SerializeField] private float _missBudgetShare   = 0.5f;  // (0, 1]
[SerializeField] private float _earlyLenienceRatio = 1.0f; // [1.0, 1.5]（修正 4）

// 降级标志 —— 供 EditMode 测试 / 运行时遥测 / Odin Validator 三方读取
// 暴露方式待 TD 裁定（见 Open Follow-up TD-3）
[Flags] public enum ClampDegradation {
    None                   = 0,
    MissHeadroomCompressed = 1 << 0,   // Warning + 遥测, 非阻塞
    PerfectFloorSacrificed = 1 << 1,   // Warning + 内容期门禁
    MissUnreachable        = 1 << 2,   // 内容期 ERROR
}
public ClampDegradation LastClampDegradation { get; }

// RhythmJudgmentEvent —— 载荷结构不变（registry:167-178），但 OffsetMs 的
// 语义被 FQA 合约 §3 强化：符号必须忠实反映早(−)/晚(+)方向。
// 评级比较由 Mathf.Abs(offset) 改为带符号比较（修正 4 C-2）：
//   offset ≥ 0 ? 用 GoodHalfWindowMs_effective : 用 GoodEarlyHalfWindowMs_effective
// Perfect 档两侧共用 PerfectHalfWindowMs_effective（FQA 合约 §2）。

// IBeatClock.LatencyOffsetMs { get } —— 【不可删除】。
// 修正 1 后 C1 不再需要它，但 FE3（校准写入）与 boot 链仍依赖（registry:297 / ADR-D8 五接口）。
```

---

## Alternatives Considered

### Alternative 1：只修 SPEC-1，不动 Miss 区间

- **Description**：仅执行修正 1 的公式去重，保留 `GoodHalfWindowMs_authored=120` / `LateWindowMs_authored=100`。
- **Pros**：改动面最小，不触及 Locked 的 M0 §2.2，无需新旋钮。
- **Cons**：SPEC-2 的根因是两个 authored 默认值的相对顺序，与 SPEC-1 **无代数关联**；修完 SPEC-1 后 Miss 依然恒不可达。
- **Rejection Reason**：round-1 实证已证明 M0 §5 的 BPM 60/120 测试点会继续复现「打不出 Miss」。M0 Group A 判据仍不可测。

### Alternative 2：直接改权威默认值，不引入旋钮

- **Description**：把 `GoodHalfWindowMs_authored` 120→90、`LateWindowMs_authored` 100→130，不新增 `MissHeadroomMs` / `MissBudgetShare`。
- **Pros**：零新概念，改两个数字即可让默认配置下 Miss 可达。
- **Cons**：把本应由 balance pass 决定的具体数值焊死在 ADR 里；没有「为什么是这两个数」的结构性保证；换 BPM、量化模式或平台后可能重新塌缩为空集（Android BPM 140 即复现）。
- **Rejection Reason**：违反 R-3。把「Miss 必须可达」留作手工挑中的两个幸存数字，而非可被单测断言的架构不变量。

### Alternative 3：Miss 预留宽度用硬编码常量（`Good + 20ms`）

- **Description**：不设旋钮，在级联里写死 `LateWindowMs_effective = GoodHalfWindowMs_effective + 20`。
- **Pros**：实现最简，无 SO 字段新增，不受 6.3 `[SerializeField]` 约定影响。
- **Cons**：不符合项目既有 Tuning Knobs 文档规范（`:758` 起）；无法按平台/难度分别调整；且仍是绝对毫秒形式，在高 BPM 下与 Alternative 4 同样反转。
- **Rejection Reason**：绝对毫秒形式是反转的根源（见修正 2 根因分析），换个写法不解决问题。

### Alternative 4：原修正 2 —— `MissHeadroomMs` 作「绝对毫秒下限」

- **Description**：在现有级联后追加两行，把 Good 天花板压到 `C − MissHeadroomMs`，Late 侧 `clamp(authored, GoodEff + MissHeadroomMs, C)`。
- **Pros**：对现有级联改动最小（追加而非替换），保留 E7 原文不动。
- **Cons**：**在 Android BPM > 135.13 时产生下界 > 上界的区间**——复刻 SPEC-2 的同一 bug 类。E7 步骤 1 把 `GoodEff` 抬回 `PerfectEff=80`，而天花板已被压到 `C−30`；两条件数学同一（均为 `C < 110`），故 E7 触发 ⟺ 级联反转。BPM 140 Android 下 Miss 带天花板 26.14ms < 30，`MissHeadroomMs=30` 结构性不可满足。
- **Rejection Reason**：已被代数否决（逐项复算见修正 2 开头的超范围声明）。这是本 ADR 唯一被自身诊断标准推翻的先前方案。

### Alternative 5（修正 4 的备选）：保持早侧现状，把不对称文档化为设计意图

- **Description**：不动 `:287`，只在 GDD 补一段「早侧不判 Miss 是刻意宽容」。
- **Pros**：零代码改动，不触及 Locked 的 M0 §2.2，无需 producer 介入。
- **Cons**：建立在**错误前提**上。早侧越界不是「判 Good 的宽容」而是**静默丢弃**——`EmitAndClear` 不被调用，而它是 `AnimancerAttackPlayer.Play(...)` 的唯一调用点，故后果是无事件、**无攻击动作**、无方向提示。这正是 `rhythm-core.md:29` 明令禁止的失败态。且 A.3 已证残余误差主导符号是「晚」，早侧宽容对目标人群无帮助。
- **Rejection Reason**：违反 R-4，且与 Player Fantasy 的明令禁止直接冲突。「文档化现状」会把一个 Player Fantasy 违规追认为规格。

### Alternative 6（修正 4 的备选）：完全对称化，不设 `EarlyLenienceRatio`

- **Description**：早侧窗口与晚侧严格恒等，不引入比率旋钮。
- **Pros**：概念最简；与已被 round-2 验证的切片几何完全一致；无需 balance pass 定值。
- **Cons**：排除了「前向量化系统中提前出手的手感代价本就不同于延后」这一尚未被证伪的手感假设，且格斗输入缓冲/魂系输入排队的业界先例确实存在非对称处理。一旦 balance pass 发现需要不对称，又得重开 ADR。
- **Rejection Reason**：**非常接近被采纳**。最终选 (c) 是因为 `EarlyLenienceRatio` **默认 1.0 即退化为本方案**——(c) 是本方案的超集，代价仅为一个旋钮，却把「是否需要不对称」留给 balance pass 的实测而非现在的猜测。若 balance pass 结论为「不需要」，把旋钮固定在 1.0 即可，无需再改架构。

### 业界参照（修正 4 的论据基础）

| 参照 | 窗口对称性 | 与本项目的匹配度 |
|------|-----------|-----------------|
| OSU / DDR / Taiko / IIDX | 对称 | **不匹配** —— 按下即消费，无量化执行层；且 `game-concept.md:213` 明确本项目「NOT 轨道音游」|
| 格斗游戏输入缓冲 / 魂系输入排队 | 部分非对称 | **部分匹配** —— 结构同构（缓冲 + 延后执行），但无评级层 |
| Hi-Fi Rush | 未知 | **同族**（`game-concept.md:244` 已列参照），但无可靠公开依据断言其窗口对称性 |
| Crypt of the NecroDancer | 对称且严格 | 反面参照（惩罚性而非奖励性） |
| **苗刀切片（round-2）** | **对称** | **唯一有实测数据的模型** |

结论：没有一个业界参照同时具备「量化执行层 + 评级层」，无法据外部先例论证不对称。**唯一有数据的模型是对称的。**

---

## Consequences

### Positive

- **P-1** 完美踩拍的玩家在任意延迟设备上偏移归零（R-1），M0 Group C「无线音频延迟容忍度」判据**首次变为可测**。
- **P-2** Miss 评级结构性可达（R-2），M0 Group A 判据解除潜在阻塞；round-2 已在切片侧验证机制。
- **P-3 附带修复一项既有 P1 违规**：`LateWindowMs` 从未进入钳位级联，Android BPM 200 下 `authored=100 > C=74` 已在造成相邻拍混叠。修正 2 的 Late 钳位同时是 P1 bug 修复。
- **P-4** 级联反转被**结构性排除**而非靠挑数值规避（R-3），并获得 ~4300 组参数网格的单测覆盖（AC-C1-30）。
- **P-5** 消除一处 Player Fantasy 违规（R-4）：早侧越界从「无任何反应」变为「可达 Miss + 攻击仍执行 + 方向提示」。
- **P-6 无障碍收益**：删除 `:259` 的 120ms 上限，把 FE7 可接受校准范围从 120ms 放宽到 ±200ms，直接服务 R-AUDIO-01 目标人群。
- **P-7** 不变量违反在**开发期**可见（R-5），不再依赖 QA 试玩——这是 SPEC-2 潜伏至今的直接对策。
- **P-8** E7 的全部语义被吸收进统一级联，消除「四个互不知情的补丁叠加」的结构（E7 步骤 1/`:287` 步骤 1b/原修正 2/E7 步骤 2）。
- **P-9** 顺带消除级联的**规格冗余**（`:264-288` 与 `:380-400` 两份完整副本）。该冗余正是补丁能够互不知情地累积的结构性原因——两份副本各自被不同轮次的修订触碰过，没有任何机制保证它们同步。

### Negative

- **N-1** 新增 3 个旋钮（`MissHeadroomMs`、`MissBudgetShare`、`EarlyLenienceRatio`），增加 balance pass 的参数空间与 SO 字段数。
- **N-2** `LateWindowMs_authored` 失去 authoring 意义（见 2e），须在两个选项中择一；选 (ii) 会留下 2ms 丢弃接缝。
- **N-3** 评级比较由 `Mathf.Abs(offset)` 改为带符号比较，`Evaluate()` 签名变化，须 TD 复核对下游 C2/PL2/P1 的影响。
- **N-4** 修正 4 须走 **M0 修订流程**（Locked 文档），引入流程开销与 producer 依赖。
- **N-5 文档改动面大，且存在规格冗余**：`rhythm-core.md`（6 处双重补偿中的 3 处 + **两份级联副本** `:264-288`/`:380-400` + **两处 Assert 块** `:295`/`:398-400` + Tuning Knobs + 调参指引 + E5 + E7 + AC 表）、`adr-d3`（`:48`/`:80`/`:89`/`:109`/`:115`/`:138`/`:139`/`:145`/`:146`/`:222-223`/`:252`/`:264`）、`audio-beat-clock.md:49`、`registry:76`、`tr-registry.yaml` **4 条 TR**（TR-rhythm-005 / 006 / 013 / 017，其中 005/006/017 为写入阶段 sanity check 新发现，见修正 6j/6k/6m）+ `rhythm-core.md:634` 接口节拍表的同源错误（修正 6l）。遗漏任一处散文或任一份副本即可能让 bug 被下一位读者推回。**建议本 ADR 落地时顺手消除级联冗余**（状态机段改为引用规格段），否则下一次级联修订会再次面临同一风险。
- **N-6** 新增 5 条 AC（AC-C1-27~31）+ 改 1 条（AC-C1-19），且 **AC-C1-29 在 SPEC-4 修复前必然失败**——这是刻意的（它是 SPEC-4 的回归哨兵），但会让首次跑测的人困惑，须在 AC 描述里写明。

### Risks

- **R-a｜SPEC-4 修复路径未定，修正 4 效果不可验证。** 缓解：修正 5 已把 SPEC-4 登记为显式前置依赖并立下硬约束（不得用钳位回避）；AC-C1-29 作为哨兵，修复完成即自动转绿。
- **R-b｜`MissBudgetShare` 被 balance pass 误设为 > 1，反转重现。** 缓解：范围声明为 `(0, 1]`；AC-C1-30 的参数网格包含边界；Odin Validator 对 SO 字段范围做编辑期校验。
- **R-c｜ADR-D8 的 mock 接口未交付，全部单测无法运行。** 缓解：已在 Engine Compatibility 显式 gated；ADR-D6 `:193` 的 `MockDSPClock` 先例确认技术路径可行；可参照 `CombatCore_DiSeamSpike_Test.cs` 的构造注入范式。
- **R-d｜float 精度在长会话后侵蚀 0.1ms 断言门槛。** 缓解：修正 1 的实现护栏（double 域相减，只窄化最终结果）已写入 Decision，并在 `rhythm-core.md` 实现指引中落地。
- **R-e｜修订遗漏——三类遗漏风险。** (i) 7 处双重补偿中的 2 处**散文**复述；(ii) 级联的**第二份副本** `:380-400`（更像代码、更可能被实现者照抄）；(iii) **`tr-registry.yaml` 中的规格复刻**——TR requirement 文本是 story 作者的权威输入，持有旧规格时错误会以「需求」身份直接进入冲刺，且不在 GDD Sync Check 的扫描面内。缓解：三份清单均已表格化并标注形态（公式/伪代码/散文/副本/TR 条目）；Migration Plan 第 1 步完成后由独立 reviewer 按表逐项核对；建议同批消除级联冗余。
  **本风险已在本 ADR 自身的写入过程中实现了一次**：(iii) 类的 3 条（TR-rhythm-005/006/017）不在原清单内，是写入完成后对已改文件做反向 grep 才发现的，其中 TR-rhythm-017 还含一处与 SPEC-5 矛盾的事实错误。结论：预先列出的清单是上一轮认知的产物，必然不完整——**全仓 grep 旧表述是不可省的收尾步骤**（见 PR-6）。
- **R-g｜「时序盲区」类缺陷再次被降级为非阻塞。** 已有先例：`rhythm-core-review-log.md:14` 第五轮复查正确识别了 SPEC-4 并命名为「LookaheadMs 与 EarlyWindowMs 时序盲区」，判非阻塞后 C1 GDD 获批，修订至今未落地。缓解：建议作为 `/architecture-review` 检查项——标记为「时序盲区 / timing blind spot」的推荐修订不得以非阻塞结案（见 Open Follow-up PR-4）。
- **R-f｜`:259` 删除后 FE7 沿用旧 120ms 上限。** 缓解：修正 3 要求显式写入义务边界变更。**附带发现**：FE3 延迟校准的 GDD **不存在**（`design/` 下无此文件），残余校准误差的符号与边界**目前无人拥有**。建议本 ADR 在 GDD Requirements 追加一条约束 FE3（见 Open Follow-up GD-3）。

---

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| `rhythm-core.md` | `:29` Player Fantasy——「任何情况下都不会」出现「我输入了，但游戏没有反应」 | 修正 4 的 FQA 合约 §3「禁止静默吞输入」把该禁令提升为可断言的架构不变量（AC-C1-29）；修正 5 登记 SPEC-4 为违反该禁令的具体缺陷 |
| `rhythm-core.md` | `:229-242` C1-F2 偏移公式与时域说明 | 修正 1 去除重复延迟项（**7 处**，含 2 处散文与 1 处 TR 注册表条目）；补 double 域相减护栏 |
| `rhythm-core.md` | `:244-255` worked examples | 修正 1 重构参考系为「玩家听到声音的时刻」，取代现有「原始调度拍点」错误模型 |
| `rhythm-core.md` | `:259` 延迟安全约束 | 修正 3 整段删除；量程统一为 ±200ms（4 个权威源一致）；FE7 义务边界显式变更 |
| `rhythm-core.md` | `:264-295` C1-F3 钳位级联与不变量 | 修正 2 以统一级联 ①–⑥ 整体替换；新 5 行不变量块；Assert 锚点更正为 `:295` |
| `rhythm-core.md` | **`:380-400` 状态机伪代码——级联的第二份完整副本** | 修正 2c 同步替换（含 `:387-391` E7 二次钳位、`:393` Early `min`、`:398-400` Assert 块）；建议改为引用 `:264-288` 以消除冗余 |
| `rhythm-core.md` | `:774` 调参顺序指引 | 修正 6g 随 2e 选项改写，并追加三个新旋钮的调参顺序 |
| `rhythm-core.md` | **`:634` C1-F5 接口节拍表**——perk delta 缓存被写成 effective 窗口字段 | 修正 **6l** 改为存入 delta 缓存后触发 `ApplyClampCascade(...)`；补说明块点明 6 个 effective 字段 + degradation 标志位。**照原文实现会绕过 P1 天花板与 P3 Miss 预算**（与 TR-rhythm-006 同源错误）|
| `design/gdd/reviews/rhythm-core-review-log.md` | `:14` 第五轮复查把「LookaheadMs 与 EarlyWindowMs 时序盲区」判为**非阻塞** | 修正 5 把该盲区正式登记为 SPEC-4（High）。建议追加流程规则：标记为「时序盲区」的推荐修订不得以非阻塞结案 |
| `docs/architecture/tr-registry.yaml` | **TR-rhythm-013** 复刻 authored LateWindow 判定 | 修正 6f 更新 requirement 文本（不重编号，填 `revised:`）|
| `docs/architecture/tr-registry.yaml` | **TR-rhythm-005** 复刻 SPEC-1 旧双重补偿公式（第 7 处站点）| 修正 **6j** 改为 `(judgment_time - beat_dsp_time)x1000` + 单次补偿声明 |
| `docs/architecture/tr-registry.yaml` | **TR-rhythm-006** 级联产出字段清单不全（只列 2 个 effective 字段）| 修正 **6k** 补齐 6 字段 + degradation 标志位 + 唯一权威声明 + P1>P2>P3 |
| `docs/architecture/tr-registry.yaml` | **TR-rhythm-017** 「binary Perfect/Miss degrade」与 SPEC-5 第 3 条矛盾 | 修正 **6m** 改为 Perfect 单一档 + `BPM_max` 派生 + `MissUnreachable` 定性为配置错误 |
| `rhythm-core.md` | `:613-623` E5 LateWindow 过期精度 | 修正 2e 同步 authored → effective，消除与 `:619` 的分叉；`:623` 自认的 ADR-D3 时间源矛盾一并修 |
| `rhythm-core.md` | `:638-662` E7 坍缩护栏 | 修正 2c 吸收 E7 全部语义；修正 6a/6b/6c 修其三处文本缺陷 |
| `rhythm-core.md` | `:769` Tuning Knobs | 追加 `MissHeadroomMs` / `MissBudgetShare` / `EarlyLenienceRatio` 三行 |
| `rhythm-core.md` | `:810` AC-C1-02b（不可满足）、`:829` AC-C1-12（同义反复）、`:840` AC-C1-19（写死 186） | 全部改写；新增 AC-C1-27~31 |
| `audio-beat-clock.md` | `:49` F2 Core Rule 7 跨域对齐指示 | 修正 1 改写为「C1 直接比较两个时间戳，无需额外调整」 |
| `combo-system.md` | `:56` 规则 7 / `:186` EC6——Miss 触发图位置软重置，**绝不清零段数** | 修正 6e 修正 `registry:76` 的无来源误写，消除 Miss 可达后的活性行为分歧 |
| `accessibility-requirements.md` | `:87` 认知无障碍——须提供早/晚方向提示 | 修正 4 FQA §3 保证两侧均产生带符号 `OffsetMs` 事件，方向提示在早侧首次可用 |
| `m0-rhythm-feel-acceptance.md` | Group A（Miss 可测性）、Group C（无线音频延迟容忍度） | 修正 1 使 Group C 可测；修正 2 使 Group A 可测。§4 A2 建议补「静默吞输入 = 0」子项 |

---

## Performance Implications

- **CPU**：可忽略。修正 1 少一次加法。修正 2 的级联只在 `OnMeterChanged`（BPM/模式/perk 变化）时执行，非每帧、非每输入；新增约 6 次 min/max 与 2 次 clamp。修正 4 的带符号比较与现行 `Mathf.Abs` 同量级。
- **Memory**：新增 3 个 `float` authored 字段 + 2 个 `float` effective 字段 + 1 个 `enum` 标志，单实例，可忽略。
- **Load Time**：零影响。
- **Network**：不适用。
- **遥测**：`MissHeadroomCompressed` / `PerfectFloorSacrificed` 的遥测上报须走既有 analytics 批处理，**不得**在 `OnMeterChanged` 同步发网络请求。

---

## Migration Plan

1. 本 ADR Accepted 后，按 GDD Requirements Addressed 表同步修订 `rhythm-core.md` / `adr-d3` / `audio-beat-clock.md` / `registry` / `tr-registry.yaml`。**三项强制核对**（缓解 R-e）：(a) 独立 reviewer 按「7 处双重补偿」表逐项核对；(b) 确认级联的**两份副本**（`:264-288` 与 `:380-400`）与**两处 Assert 块**（`:295` 与 `:398-400`）均已同步，并在同批把状态机段改为引用规格段以消除冗余；(c) **全仓 grep 旧表述收尾**——至少扫 `judgment_time + latency_offset`、`_effectivePerfect/GoodMs`、`binary Perfect/Miss`、`LateWindowMs/1000` 四个字串，命中处若非本 ADR 的诊断引用即为漏改。(c) 是本次新增的步骤，起因是 TR-rhythm-005/006/017 三条漏项正由此发现。
2. game-designer 在 balance pass 中为 `MissHeadroomMs` / `MissBudgetShare` / `EarlyLenienceRatio` 及 `GoodHalfWindowMs_authored` 选定数值，并就 2e 的 (i)/(ii) 择一。
   > 切片临时值 `Good=100` 可作起点参考，但切片用「网格最近邻结构性上限」而非 `LateWindowMs` 状态机，两套机制下数值不可直接套用。
3. **等待 ADR-D8 Migration Plan 第 6 步**交付 `IBeatClock` / `IInputEventBus` 及其 Mock（R-c 的解除条件）。
4. TD 裁定修正 5（SPEC-4）的修复路径与 Open Follow-up 的 TD-1~3。
5. 若采纳修正 4：producer 启动 **M0 §2.2 修订流程**；同时建议 §4 A2 补「静默吞输入 = 0」子项（现行 A2 只覆盖误判 Miss）。
6. C1 实装 sprint 前，先写 AC-C1-27~31 对应的 EditMode 单测（遵「先写测试」规范）。**AC-C1-29 预期失败**，直至 SPEC-4 修复。
7. 实装 `RhythmJudger`，使全部 AC 转绿。
8. 重跑 M0 §5 测试协议 BPM 60/120，确认 Miss 在**两侧**均可被实际触发（修正 4 若采纳），并记录试玩者打出的 Miss 落在哪一侧——这是 round-2 实证未能覆盖的盲区。

---

## Validation Criteria

全部为 EditMode 纯 C#，gated on ADR-D8 第 6 步。现存最高编号为 **AC-C1-26**，故新增自 27 起。

- [ ] **AC-C1-12（改写）** 显式代入 `beat_dsp_time = raw_beat_dsp_time + latency_offset_s` 后，断言 `beat_offset_ms` 与 `latency_offset_s` **无关**（对任意 `latency_offset_s ∈ [−0.2, 0.2]` 成立）。取代现行同义反复版本。
- [ ] **AC-C1-02b（改写）** 前提须可构造：断言默认调参下 Miss 区间非空，并给出具体 BPM 下的数值例。
- [ ] **AC-C1-27** 早侧 Miss 带结构性非空 + 默认几何对称。4 条子断言：(a) `EarlyWindowMs_effective > GoodEarlyHalfWindowMs_effective`；(b) `EarlyLenienceRatio == 1.0` 时 `GoodEarlyHalfWindowMs_effective == GoodHalfWindowMs_effective`；(c) `PerfectHalfWindowMs_effective` 两侧恒等；(d) 早侧 Miss 带宽度 == 晚侧（当 ratio == 1.0）。
- [ ] **AC-C1-28** 早侧 Miss **实际产生**：构造 `offset` 落于早侧 Miss 带的输入 → 产生 `RhythmJudgmentEvent`，`Rating == Miss`，`OffsetMs < 0`，`CancelTier == Closed`，`ΔCombo == 0`（不清零）。含晚侧对照组与两侧后果一致性断言。
- [ ] **AC-C1-29** 禁止静默吞输入：以 1ms 步长扫描 `offset ∈ [−EarlyWindowMs_effective, +LateWindowMs_effective]` 全区间，断言**每一个**输入恰好产生一次 `RhythmJudgmentEvent`，且 `OffsetMs` 符号与构造方向一致。
  > **在 SPEC-4 修复前必然失败——这正是其价值**（R-a 的哨兵）。
- [ ] **AC-C1-30** 钳位级联永不反转：在 BPM × 平台 × 量化模式 × perk × `MissBudgetShare` 的约 4300 组参数网格上，断言新 5 行不变量块全部成立，且不存在下界 > 上界的区间。同时作为原修正 2 两行的回归测试（BPM 140 / Android 一例必须通过）。
- [ ] **AC-C1-31** 降级前沿谓词等价：断言 `MissUnreachable` 置位 ⟺ `BPM > BPM_max(platform, mode)`。**断言对象必须是 `LastClampDegradation` 标志位，而非日志文本。**
- [ ] **AC-C1-19（改写，3 处）** (a) 写死的「Android BPM ≥ 186」改为 `BPM_max` 公式派生；(b) 结论改为「前沿以下三档非空；前沿以上退化为 **Perfect 单一档**」；(c) 断言对象改为标志位。
- [ ] **回归** `AC-C1-13` / `AC-C1-15b` 等既有钳位级联 AC 在统一级联下仍成立。**注意**：统一级联是**替换**而非追加，E7 原有两步已被吸收，故涉及 E7 的既有 AC 须逐条复核而非假定不变。
- [ ] **AC-C1-04 / `:621`（改写）** DSP 粒度边界处置改为引用 `LateWindowMs_effective`（随 2e 的选项定稿）。
- [ ] **浮点护栏** 模拟长会话（DSP 时钟累积至 ~20 分钟量级）后，完美踩拍断言 `|beat_offset_ms| < 0.1ms` 仍成立。

---

## Related Decisions

- **ADR-D3**（节奏量化核心，Accepted）——本 ADR 修订其 Evaluation Algorithm（公式 `:138`/`:145`、Constraints 散文 `:48`、注释 `:115`），并经修正 5 登记其调用时序缺陷（SPEC-4）。整体架构（单槽预缓冲 + LateWindow 状态机）保留；SPEC-4 的修复**可能**改变 `TryEvaluateEarly`/`TryEvaluateLate` 的合并方式，待 TD 裁定。
- **ADR-D4**（Accepted）——新增 SO 字段须遵其 6.3 `[SerializeField]` fields-only 约定。
- **ADR-D6**（音频中间件，Accepted）——**无需变更**。FMOD DSP 时钟调度与本 ADR 的纯算法修正无耦合；其 `:193` 的 `MockDSPClock` 为本 ADR 的 EditMode 验证路径提供先例。
- **ADR-D8**（时钟与输入接口契约，Accepted）——`:104-147` 为验证路径提供 mock 接口权威签名；`IBeatClock.LatencyOffsetMs { get }` **不可删除**（FE3 与 boot 链仍依赖）；`:212-213` 已禁止下游直读 `AudioSettings.dspTime`，故 `AudioSettings.dspTime` 的 6.3 域差异对本 ADR **不适用**。
- **ADR-D13**（Run-End Boundary，Proposed）——无直接耦合。
- `docs/registry/architecture.yaml`——`:76` 由修正 6e 修正；`:539-543` 的 `direct_beat_offset_at_input_time` 禁令由修正 1 补措辞澄清（**不削弱**）；`:87` 为量程第 4 个权威源。

### 相关但独立（不在本 ADR 范围内）

- **DT-1（连击命中确认）**：警告台账既有项，经苗刀切片 round-1 首次获得实证（「可以在没有怪的时候打空气积累倍率」）。裁决倾向：连击递增**应**要求命中确认。但修复需 C1 新增对 C6（命中检测，当前 `rhythm-core.md:723-732` 依赖表中不存在）的上游依赖，并把连击提交从「判定时同步」改为「命中确认后异步」（`combo-system.md:90`），属架构级改动且涉及不同子系统（C1-F4 vs C1-F2/F3），需独立 ADR。本轮试玩的规避方式是编排（敌人尽快入场）。
- **DT-5（原记为矛盾，本次降级）**：`adr-d3:115` 的「先到优先」四字与规则 4 的后到优先冲突——但伪代码本体 `_pendingInput = e  // 覆盖旧值` 实现的就是 last-in-wins，与规则 4 **一致**。这是注释笔误，已归入修正 6d，**不需要独立裁决**，且不得按「先到优先」反向修改代码（那会真正违背规则 4 的设计意图）。

---

## Open Follow-up

### technical-director 域（签署前置）

- **TD-1** SPEC-4 的架构修复路径（建议：合并为单一 `TryEvaluate(e)`，输入到达即对已 armed 拍点求值），及其与 PL2/P1 表现层调度的关系。**硬约束**：不得用「把 `EarlyWindowMs_effective` 钳到 `LookaheadMs`」回避。
- **TD-2** 带符号评级比较对 `RhythmJudgmentEvent` 结构与下游 C2/PL2/P1 的影响（`registry:167-178` 载荷结构本身不变，但 `OffsetMs` 语义被强化）。
- **TD-3** `LastClampDegradation` 的暴露方式——需同时满足 EditMode 测试、运行时遥测、Odin Validator 编辑期门禁三方读取，且 Runtime 程序集禁引 Odin（C-c）。
- **TD-4** 2e 的 `LateWindowMs` 两选项择一（推荐 (ii)：`LateWindowMs_effective = C`，收敛到切片已验证几何）。

### game-designer 域

- **GD-1** `MissHeadroomMs` / `MissBudgetShare` / `EarlyLenienceRatio` / `GoodHalfWindowMs_authored` 的权威数值（balance pass）。本 ADR 只保证结构不变量，不预判手感数值。
- **GD-2** `EarlyLenienceRatio` 是否最终固定为 1.0（完全对称）——需 M0 修订后的双侧试玩数据支撑，特别是**区分试玩者打出的 Miss 落在哪一侧**（round-2 的盲区）。
- **GD-3｜FE3 规格缺口**：FE3 延迟校准的 GDD **不存在**（`design/` 下无此文件）。残余校准误差的符号与边界目前**无人拥有**，而修正 3 已证其主导符号为「晚」且四条路径单向压向补偿不足。建议本 ADR 在 GDD Requirements 追加一条约束 FE3，或另开 `/design-system fe3-latency-calibration`。

### producer 域（流程阻塞）

- **PR-1** `m0-rhythm-feel-acceptance.md` 为 **Locked**，其 §2.2（`:51-52`）逐字复刻了 `EarlyWindowMs_effective = min(...)` 与 `Early_eff ≤ Good_eff`。**采纳修正 4 须走 M0 修订流程。** 修正 1/2/3/6 不触及 Locked 内容，可先行。
- **PR-2** 建议 M0 §4 A2 判据补「静默吞输入 = 0」子项——现行 A2 只覆盖「误判 Miss」，不覆盖「无任何反应」，而后者是 SPEC-4 的实际表现。
- **PR-3** 本 ADR Accepted 后，评估是否需重开 §2.1 对应判据行（Group A / Group C）的签署状态。
- **PR-4｜流程改进建议**：`rhythm-core-review-log.md:14` 显示 SPEC-4 曾被第五轮复查正确识别并命名，却判为**非阻塞**，随后 C1 GDD 获批、修订遗忘。建议在 `/architecture-review` 增设一条规则：**标记为「时序盲区 / timing blind spot」的推荐修订不得以非阻塞结案**，须转为具名缺陷条目或 ADR。理由：此类缺陷的后果（静默吞输入）在玩家视角无症状，不会在后续评审或试玩中自行冒头——round-1 的 `_metronome == null` 是同一失效模式的实证。
- **PR-5** 把 SPEC-4 的这段「已知风险被错误定级」历史记入 M0 §2.1 风险台账，作为定级标准的参考案例。

- **PR-7｜`RhythmJudgmentEvent` 字段清单在两处权威源互相矛盾（本次 sanity check 发现，不属本 ADR 诊断范围，未改动任何一边）**。
  - `docs/registry/architecture.yaml:176` — `readonly struct RhythmJudgmentEvent { AttackType, Rating, OffsetMs, BeatDspTime, Mode, ComboCountBefore }`
  - `tr-registry.yaml` **TR-rhythm-012** — `RhythmJudgmentEvent readonly struct (Rating, AttackType, CancelTier, ComboCountBefore/After, OffsetMs)`

  前者有 `BeatDspTime` / `Mode` 而无 `CancelTier` / `ComboCountAfter`；后者恰好相反。两份都是 story 作者会直接引用的权威源，取谁决定了 C1 广播面的实际形状。**本 ADR 刻意不裁决**：判定哪份权威需要核对 C1 的全部下游消费方（C2 取消图需要 `CancelTier`；Feel/MMFeedbacks 订阅需要 `Rating`；`BeatDspTime`/`Mode` 的消费方尚未确认是否存在），这是一次独立的接口合约审查，与本 ADR 的窗口缺陷族无因果关系。移交 `/architecture-review`：**先确认每个字段至少有一个实际消费方，再以并集或最小集定稿，两处同步**。注意 `ComboCountBefore/After` 与 SPEC-2 的 Miss 语义相关——Miss 下 `After == Before`（不清零），若最终只保留 `ComboCountBefore` 则下游无法从事件本身观测到这一点。

- **PR-6｜`/architecture-decision` Step 5.7 的扫描面偏窄（本次写入实证）**。现行 GDD Sync Check 只比对 GDD 与 ADR 的**命名**不一致，不扫 `tr-registry.yaml`。结果：TR-rhythm-005（SPEC-1 第 7 处站点）、TR-rhythm-006（级联字段清单不全）、TR-rhythm-017（含与 SPEC-5 矛盾的事实错误）三条全部逃过 Step 5.7，直到写入后反向 grep 才暴露。建议两条改进：(a) Step 5.7 的扫描面扩至 `docs/architecture/tr-registry.yaml` 与 `docs/registry/architecture.yaml`，不限于命名、也比对**规格内容**；(b) 增设写入后收尾步骤——**对被修订的旧表述做一次全仓 grep**，而非只核对 ADR 预先列出的清单。理由：清单是上一轮认知的产物，结构上不可能包含本轮才识别出的站点；而 TR 条目的危害路径最短（直接作为「需求」进入 story）。
  **另：ADR-D15 引入的新规格尚无 TR 条目**（三旋钮 `MissHeadroomMs`/`MissBudgetShare`/`EarlyLenienceRatio`、Miss 结构性非空、`ClampDegradation` 三标志位、FQA 合约三条）。按 `docs/CLAUDE.md`，TR 注册表的**新增**由 `/architecture-review` Phase 8 拥有；且本 ADR 当前为 `Proposed`，提前建立 `status: active` 的 TR 会让 story 误判为可实现。故本次只修订已有条目的过期/错误文本，**新增留给 ADR Accepted 后的 `/architecture-review`**。

### 本次调查的已知盲区（诚实登记）

- `combo-system.md` 的取消图消费逻辑已核实（`:56` 规则 7 / `:186` EC6：Closed → 图位置软重置，不清段数），故 Miss 的实际代价为「丢连招分支进度 + 关取消窗」，属中等量级。该结论**加强**修正 4：Miss 既非毁灭性惩罚，便无理由为早侧单设豁免。
- round-2 实证未区分 Miss 落在早侧还是晚侧（见 Context §实证附注 3）。
- 不对称生产模型**从未被试玩过一次**（同上，附注 2）。
