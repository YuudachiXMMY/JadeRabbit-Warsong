# ADR-D15: 节奏判定窗口缺陷——延迟补偿抵消 bug 与 Miss 评级空集

## Status
Proposed

## Date
2026-10-21（Proposed）

> 本 ADR 由苗刀节奏手感切片（`Assets/_VerticalSlice/MiaodaoRhythmSlice/`）第 1 轮试玩失败的根因排查触发。
> 切片是独立实现（不引用生产 C1 代码），故本 ADR 所述两项缺陷**只存在于生产 GDD/未来生产代码**，
> 切片已在本轮绕开（见下文「与切片的关系」），不受本 ADR 阻塞。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (6000.3.19f1) |
| **Domain** | Core（纯 C# 判定数学与 GDD 公式一致性；不触及任何 Unity/FMOD API 表面）|
| **Knowledge Risk** | NONE — 本 ADR 不引入、不依赖任何 Unity API，纯规格层代数修正 |
| **Post-Cutoff APIs Used** | None |
| **Verification Required** | 见下文 Validation Criteria；核心验证方式是 EditMode 纯 C# 单测，注入 mock `IBeatClock`/`IInputEventBus`，不依赖引擎运行时 |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-D3（节奏量化核心，定义本 ADR 要修正的 Evaluation Algorithm，`:138`/`:145` 两处）。ADR-D3 状态为 Accepted，本 ADR 是对其算法的**修订**，非新增。 |
| **Enables** | 消解 M0 §2.1 Group A「Miss 评级不可达」的潜在阻塞；使 M0 Group C「无线音频延迟容忍度」判据对高延迟设备可测（当前该判据在生产代码路径下无法验证，因为补偿完全抵消）。 |
| **Blocks** | 生产 C1 判定代码实装（当前 Pre-Production 阶段尚未实装生产 C1，故本 ADR 不阻塞任何已完成工作，只约束即将开始的实装）。 |
| **Related, not blocking** | DT-1（连击需命中确认的裁决）——独立缺陷，不同子系统（C1-F4 vs C1-F2/F3），另立 ADR，见文末「相关但独立」。 |

## Context

### 问题 1（SPEC-1，Critical）：C1-F2 延迟补偿在代数上完全抵消

**链条（每个环节均已核实原文行号）：**

1. `rhythm-core.md:201-213`（C1-F1）：`judgment_time` 取自 F3 的 `chargeStartDspTime` 或 `inputDspTime`，二者均明确是 **F3 原始、未经延迟调整的 DSP 时间戳**。
2. `audio-beat-clock.md:49`（F2 Core Rule 7）：F2 广播给上层的 `BeatEventData.beat_dsp_time` **已经**应用过延迟偏移——`adjusted_beat_time = raw_beat_dsp_time + latency_offset_s`，处于「玩家感知时间域」。Rule 7 原文明确指示："C1 须在 F3 时间戳上加 `latency_offset_s` 完成坐标系对齐"。
3. `rhythm-core.md:230`（C1-F2 公式）据此写作：
   ```
   beat_offset_ms = (judgment_time + latency_offset_s − beat_dsp_time) × 1000
   ```
   同一公式在 `adr-d3-rhythm-quantization-core.md:138` 与 `:145`（Evaluation Algorithm 伪代码）复刻两次。

**代入步骤 2 的定义展开步骤 3：**

```
beat_offset_ms = (judgment_time + latency_offset_s − (raw_beat_dsp_time + latency_offset_s)) × 1000
               = (judgment_time − raw_beat_dsp_time) × 1000
```

`latency_offset_s` 在代数上**完全抵消**——最终计算结果与 `latency_offset_s` 的取值无关，等价于从未做过任何延迟补偿。

**物理后果**：一个物理上"完美跟上节拍"的玩家（即在他们**实际听到**拍点的时刻按键，这正是无障碍音频延迟补偿要达成的目标），其按键的原始 DSP 时间 `judgment_time ≈ raw_beat_dsp_time + L_audio`（`L_audio` 为设备音频输出延迟，FE3 应测出 `latency_offset_s ≈ L_audio`）。代入上式：

```
beat_offset_ms ≈ (raw_beat_dsp_time + L_audio − raw_beat_dsp_time) × 1000 = L_audio ≈ latency_offset_s
```

即：**无论玩家踩得多准，只要设备有音频延迟，测得偏移恒等于该延迟值，不会被压到 0 附近。** 延迟越大（典型蓝牙 100–250ms），完美玩家被判定的偏移越大——当 `latency_offset_s > GoodHalfWindowMs_effective` 时，**完美踩拍的玩家必然 Miss，无论怎么练都打不出 Perfect/Good**。

**为何这个 bug 至今没被任何验收用例捕获**：`AC-C1-12`（`:829`）是唯一直接验证 C1-F2 公式的验收标准，但其断言是——"给定 `judgment_time=T`、`latency_offset_s=L`、`beat_dsp_time=B` → 结果 = `(T+L−B)×1000`"。这是对公式本身的同义反复验证（代入任意 T/L/B 验证算术正确），**从未代入 F2 Rule 7 所定义的 `B = raw_B + L` 这一跨系统关系**，因此无法揭示净效应为零。`rhythm-core.md:245-255` 的两个「示例」同样只是手工构造 `judgment_time` 反推验证公式自洽，同属同义反复，不构成跨系统验证。

`rhythm-core.md:259`「延迟安全约束」一节描述的正是这个 bug 的症状（"即使玩家完美踩拍……`beat_offset_ms` 的绝对值也将超过 Good 窗口，始终产生 Miss"），但把它归因为"需要 FE7 钳位校准范围"的使用限制，而非识别为底层公式缺陷。**修正根因后，该约束段落应整体移除**——完美踩拍不应再对大延迟值敏感。

### 问题 2（SPEC-2，High）：Miss 评级在默认调参值下为空集（M0 默认 BPM 范围内恒成立）

`rhythm-core.md` 规则 3（`:74-76`）：拍点后 `LateWindowMs`（默认 **100ms**）内到达的输入立即评级为 Good 或 Miss；超出 `LateWindowMs` 的输入被**丢弃，不产生任何 `RhythmJudgmentEvent`**（规则 4 / AC-C1-04）。评级分界线是 `GoodHalfWindowMs_effective`（默认 authored **120ms**，`:764` Tuning Knobs 表）。

可达 Miss 区间 = `(GoodHalfWindowMs_effective, LateWindowMs]`。默认值代入：`(120, 100]` —— **下界大于上界，区间恒为空，与具体 BPM 无关**，只要 `GoodHalfWindowMs_effective` 未被钳位级联压到 100ms 以下。

钳位级联（`:277` `GoodHalfWindowMs_effective = min(GoodAuthored, gridIntervalMs/2 − 1)`）只在 `gridIntervalMs/2 − 1 < 120` 时才会压低 `GoodEff`，即 `halfBeatMs < 242ms` → **BPM > 123.97**（HalfBeat 模式）。`GoodEff` 要降到 100ms 以下（让 Miss 区间转为非空）还需更高 BPM：`gridIntervalMs/2 − 1 < 100` → `halfBeatMs < 202ms` → **BPM > 148.5**。

M0 §5 测试协议（`m0-rhythm-feel-acceptance.md:125`）的主力测试点是 **BPM 60 / 120**，两者均 < 148.5 ——**Miss 在整个 M0 验证范围内恒不可达**，与第 1 轮试玩实证（「没有办法打出 miss，全部都为 good」）完全吻合。此前判断"仅 120BPM 触发"不准确：根因是 **`LateWindowMs_authored(100) < GoodHalfWindowMs_authored(120)` 这两个调参默认值的顺序**，与网格钳位无关；网格钳位只在 BPM > 148.5 时才会"意外地"打开一条很窄的缝。

### 问题 3（SPEC-3，Medium）：量程声明三处不一致（问题 1 的派生物）

| 来源 | 声明的 `latency_offset` 量程 |
|------|------------------------------|
| `rhythm-core.md:238`（变量定义表） | `[-0.2, 0.2]`（秒，即 ±200ms） |
| `rhythm-core.md:259`（延迟安全约束） | 隐式要求 `< GoodHalfWindowMs_effective`（默认 120ms） |
| `audio-beat-clock.md:306`（F2-F5 变量表） | `[-200, 200]`（毫秒，即 ±200ms，与 `:238` 一致） |

`:259` 与另外两处矛盾（200ms > 120ms）。此矛盾是问题 1 的**症状**，不是独立缺陷——一旦 SPEC-1 的抵消 bug 被修正（完美踩拍时偏移恒为 0，与延迟值无关），`:259` 的论证前提消失，该段落应整体删除，量程统一为 `:238`/F2-F5 已有的 `±200ms`。

### 与切片的关系

`Assets/_VerticalSlice/MiaodaoRhythmSlice/Runtime/RhythmJudge.cs` 与 `LatencyCalibration.cs`（本轮已修正，见文件头注释）**不复刻此缺陷**：切片的判定网格取自 `SliceMetronome` 的原始调度时间（不含任何延迟调整），判定时只对输入侧减一次校准偏移（`perceived = inputDsp − bias`），净效应等价于「只调整一次」，完美踩拍时偏移天然归零。切片可作为本 ADR 修正方案的可运行参考实现，但两者代码结构不同（切片无 F2 等价的"预调整时间戳广播"层），不可直接复制粘贴。

## Decision

### 修正 1：移除 C1-F2 公式中重复的延迟项

将 `rhythm-core.md:230` 与 `adr-d3-rhythm-quantization-core.md:138`/`:145` 的公式由：

```
beat_offset_ms = (judgment_time + latency_offset_s − beat_dsp_time) × 1000
```

改为：

```
beat_offset_ms = (judgment_time − beat_dsp_time) × 1000
```

`judgment_time`（F3 原始域）与 `beat_dsp_time`（F2 已调整域，Rule 7）直接相减即完成跨域对齐——因为调整已经由 F2 单侧施加在 `beat_dsp_time` 上，C1 不需要、也不应该再对 `judgment_time` 施加第二次。F2 Core Rule 7 中"C1 须在 F3 时间戳上加 `latency_offset_s`"的指示一并删除/改写为"C1 直接比较两个时间戳，无需额外调整"。

### 修正 2：新增 `MissHeadroomMs` 旋钮，保证 Miss 区间结构性非空

新增调参旋钮（`:764` Tuning Knobs 表追加一行）：

| 旋钮 | 默认 | 范围 | 效果 |
|------|------|------|------|
| `MissHeadroomMs` | 30ms | [15, 60] | Miss 可达区间的最小保留宽度；越大 Miss 越容易触发，越小越接近当前（近乎不可达）行为 |

钳位级联追加一步（衔接 `:277` 现有步骤，而非替换）：

```
// 新增：为 Miss 预留宽度，必要时反过来压缩 Good（与现有 E7 坍缩护栏同构，只是方向相反——
// E7 是 Perfect 让位给 Good；本步骤是 Good 让位给 Miss 的生存空间）
GoodHalfWindowMs_effective = min(GoodHalfWindowMs_effective, gridIntervalMs / 2 - 1 - MissHeadroomMs)

LateWindowMs_effective = clamp(LateWindowMs_authored,
                                GoodHalfWindowMs_effective + MissHeadroomMs,
                                gridIntervalMs / 2 - 1)
```

此修正**不单独决定** `GoodHalfWindowMs_authored`/`LateWindowMs_authored` 的具体数值该调成多少——那是手感/难度的平衡决策，留给 game-designer 的后续 balance pass。本 ADR 只保证：无论平衡调参取何值，只要满足 `MissHeadroomMs` 范围内的设定，Miss 评级在代码层面结构性可达，不会再因调参默认值的相对顺序而意外归零。

### 修正 3：删除量程自相矛盾的论述段落

`rhythm-core.md:259`「延迟安全约束」整段删除（其论证前提已被修正 1 消除）；`:238` 的 `[-0.2, 0.2]` 保留为权威声明，并在该行追加一条交叉引用指向 `audio-beat-clock.md:306`，明确两处为同一量程的两种单位表示。

## Alternatives Considered

1. **只删延迟项（修正 1），不动 Miss 区间（不做修正 2）**——否决。问题 2 的根因是两个 authored 默认值的顺序关系，与问题 1 无代数关联；只修问题 1 不会让 Miss 变得可达，M0 § 5 的 BPM 60/120 测试点仍会复现「打不出 Miss」。
2. **直接改权威默认值（`GoodHalfWindowMs_authored` 120→90, `LateWindowMs_authored` 100→130）而不引入 `MissHeadroomMs` 旋钮**——否决。这是把本该由 balance pass 决定的具体数值直接焊死在 ADR 里，且没有自解释的"为什么是这两个数"的结构性保证；换 BPM 或换量化模式后可能重新塌缩为空集。引入显式旋钮和不变量，把"Miss 必须可达"提升为可被单测断言的架构不变量，而非手工挑中的两个幸存数字。
3. **把 Late 窗口直接设成"大于 Good 多少" 的硬编码常量（如 `+20ms`）而非独立旋钮**——否决。`MissHeadroomMs` 作为具名旋钮更符合项目既有的 Tuning Knobs 文档规范（`:758` 起），且允许未来按平台/难度分别调整。

## Consequences

- **需联动修订的文档**：`rhythm-core.md`（C1-F2 公式 `:230`、两个worked示例 `:245-255`、`:259` 整段删除、Tuning Knobs 表追加 `MissHeadroomMs`、钳位级联伪代码 `:270-293`）；`adr-d3-rhythm-quantization-core.md`（Evaluation Algorithm 伪代码 `:138`/`:145` 同步公式修正，`:223` 附近追加 `MissHeadroomMs` 字段）；`audio-beat-clock.md:49`（Core Rule 7 措辞修正，去掉"C1 须额外相加"的指示）。
- **AC 需要改写或新增**：`AC-C1-12` 改为非同义反复版本——显式代入 `beat_dsp_time = raw_beat_dsp_time + latency_offset_s` 后断言结果与 `latency_offset_s` 无关；新增 AC 断言"完美踩拍 + 任意 `latency_offset_s` ∈ [-200,200] → `|beat_offset_ms| < 1ms`"。`AC-C1-02b` 现有描述不变，但需要补一条前置断言：默认调参值下 Miss 区间非空（给出具体 BPM 下的数值例）。
- **M0 里程碑影响**：`m0-rhythm-feel-acceptance.md` 为 **Locked** 状态，§2.1 判据表仍有三行 ⏳ 待签（既有已知问题，见 session-state）。本 ADR 不修改 Locked 文档本身，但 Group A/C 判据的可测性依赖此修正——建议 technical-director/producer 在本 ADR Accepted 后评估是否需要重开 §2.1 对应判据行的签署状态。
- **性能**：零影响。两处都是纯算术公式调整，运行时计算量不变（修正 1 甚至少一次加法）。
- **破坏性**：C1 尚未进入生产实装（Pre-Production 阶段），无存量代码需要改。唯一需要回溯检查的是切片——已核实切片不受影响（见「与切片的关系」）。

## GDD Requirements Addressed

- `rhythm-core.md` C1-F2（偏移公式）、C1-F3（评级钳位级联）、Tuning Knobs、AC-C1-02b、AC-C1-12。
- `audio-beat-clock.md` F2 Core Rule 7（跨域对齐指示）。
- 间接服务 `m0-rhythm-feel-acceptance.md` Group A（Miss 可测性）与 Group C（无线音频延迟容忍度，完美踩拍不应因延迟值被误判）。

## Performance Implications

无。纯公式修正，不改变调用频率、不新增分配、不引入新的每帧开销。

## Migration Plan

1. 本 ADR Accepted 后，按 Consequences 清单同步修订 `rhythm-core.md` / `adr-d3` / `audio-beat-clock.md`。
2. game-designer 在 balance pass 中为 `MissHeadroomMs` 及联动的 `GoodHalfWindowMs_authored`/`LateWindowMs_authored` 选定具体数值（当前切片临时值 `Good=100` 可作为起点参考，但切片用的是「网格最近邻结构性上限」而非 `LateWindowMs` 状态机，两套机制下的数值不能直接套用）。
3. C1 实装 sprint 开始前，先写 Validation Criteria 中列出的新/改 AC 对应的 EditMode 单测（遵循项目"先写测试"规范），确认通过后再实装 `RhythmJudger`。
4. 实装完成后，重跑 M0 §5 测试协议 BPM 60/120 两个测试点，确认 Miss 评级可被实际触发。

## Validation Criteria

- [ ] 新单测：`beat_dsp_time = raw_beat_dsp_time + latency_offset_s` 代入后，`beat_offset_ms` 与 `latency_offset_s` 无关（对任意 `latency_offset_s ∈ [-200, 200]` 成立）。
- [ ] 新单测：完美踩拍（`judgment_time == raw_beat_dsp_time`，任意非零 `latency_offset_s`）→ `|beat_offset_ms| < 0.1ms`（浮点误差容限）。
- [ ] 新单测：默认调参值（`GoodHalfWindowMs_authored=?`、`LateWindowMs_authored=?`，balance pass 定稿后的值）在 BPM 60 与 120（HalfBeat 模式）下，`GoodHalfWindowMs_effective < LateWindowMs_effective`，Miss 区间宽度 `≥ MissHeadroomMs`。
- [ ] 回归：`AC-C1-13`/`AC-C1-15b` 等既有钳位级联验收标准在新增步骤后仍然成立（新步骤是追加，非替换，不应破坏既有断言）。
- [ ] `RhythmJudger` 在 `OnMeterChanged` 末尾的既有不变量 Assert（`:291-293`）追加 `GoodHalfWindowMs_effective + MissHeadroomMs ≤ LateWindowMs_effective ≤ gridIntervalMs/2 - 1`。

## Related Decisions

- **ADR-D3**（节奏量化核心，Accepted）——本 ADR 修订其 Evaluation Algorithm 两处公式，不改变其整体架构（单槽预缓冲 + LateWindow 状态机设计保留）。
- **ADR-D6**（音频中间件，Accepted）——无需变更；FMOD DSP 时钟调度机制与本 ADR 的纯算法修正无耦合。

### 相关但独立（不在本 ADR 范围内）

- **DT-1（连击命中确认）**：警告台账既有项，经苗刀切片第 1 轮试玩首次获得实证（「可以在没有怪的时候打空气积累倍率」）。裁决：连击递增**应该**要求命中确认，而非仅凭判定本身递增——理由见 `combo-system.md:27/29/110-114`（现状允许零风险线性堆到 5× 伤害上限，且业界参照 Hi-Fi Rush/Sekiro/DMC 均为命中门控）。**但本次不在此 ADR 处理**：修复需要 C1 新增对 C6（命中检测，当前 `rhythm-core.md:723-732` 依赖表中不存在）的上游依赖，并把连击提交从"判定时同步"改为"命中确认后异步"（命中确认发生于 `hitboxStartNormalizedTime` 之后，`combo-system.md:90`），属架构级改动，涉及不同子系统（C1-F4 连击提交时序，而非本 ADR 处理的 C1-F2/F3 判定窗口），需要独立 ADR。本轮试玩的规避方式是试玩编排（敌人尽快入场，不留交战前空档），不涉及代码改动。

## Open Questions（待 game-designer / technical-director 会签时裁决）

1. **Early 侧是否也应该能触发 Miss？** 现有不变量 `EarlyWindowMs_effective = min(EarlyWindowMs_authored, GoodHalfWindowMs_effective)`（`:287`）使提前输入的 `|offset|` 结构性上限钳位在 `GoodHalfWindowMs_effective` 以内，**由设计构造**令 Early 侧永远不可能产生 Miss——与 Late 侧（本 ADR 修正后可达）形成不对称。这可能是刻意的设计意图（"提前出手不该比稍晚出手受更重的惩罚"），也可能是未被注意到的遗留。本 ADR 不改动 Early 侧不变量，留待会签时明确是否需要对称化。
2. **`GoodHalfWindowMs_authored`/`LateWindowMs_authored` 的最终数值**——本 ADR 只保证结构不变量，具体数值是 balance pass 的手感决策，不在 ADR 范围内预判。
