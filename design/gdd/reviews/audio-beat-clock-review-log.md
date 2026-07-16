# Review Log: 音频/拍点时钟系统 (F2)

---

## Review — 2026-07-14 — Verdict: APPROVED

Scope signal: M（3 项阻塞均为局部文本修订，约 8 行，无架构变更）
Specialists: game-designer, systems-designer, audio-director, unity-specialist, qa-lead, creative-director
Blocking items: 3 | Recommended: ~20
Prior verdict resolved: Yes — 第五次评审（同日）7 项阻塞全部确认修复

Summary: 第六次全量评审确认第五次评审 7 项阻塞全部到位。新发现 3 项阻塞，全部在本 session 内修订完成后判定为 APPROVED：(1) AC-F2-13 item(2) 将 BPM 切换首拍间隔由 0.333s 修正为 0.500s（= beat_interval_old，非 beat_interval_new；推导：dsp_start_new = T+2.000，旧最后一拍 T+1.500，间隔 = 0.500s）；(2) F2-F7 引入 `adjusted_dsp_now = AudioSettings.dspTime + latency_offset_ms/1000.0`，消除 `dsp_now`（raw）与 `last_beat_time`（adjusted）混用的坐标系不一致（最大误差 ±200ms）；(3) Core Rule 1 明确声明 BeatClock 为 MonoBehaviour 单例，挂载于 DontDestroyOnLoad 专属 GameObject `[BeatClock]`。creative-director 将其余约 20 项 specialist 建议降级为 Recommended（含首拍无预热降级、100ms 阈值移动端、Domain Reload 静态清零、委托 GC 等），均属 F6/P1 或实现层职责。

**3 项阻塞修复摘要：**
- B1: AC-F2-13 item(2)：`0.333s ± 10ms` → `0.500s ± 10ms`，并补充推导公式（dsp_start_new, 旧最后一拍, 间隔 = beat_interval_old）及说明（0.333s 首次出现于新段第 0 拍与第 1 拍之间）
- B2: F2-F7 引入 `adjusted_dsp_now = AudioSettings.dspTime + latency_offset_ms / 1000.0`；公式改为 `clamp((adjusted_dsp_now − last_beat_time) / beat_interval_s, 0.0, 1.0)`；变量表更新 `adjusted_dsp_now` 说明（必须使用调整后值，避免坐标系混用最大 ±200ms 误差）；示例更新
- B3: Core Rule 1 头部新增 BeatClock 类型声明：「`MonoBehaviour` 单例，挂载于 `DontDestroyOnLoad` 专属 GameObject `[BeatClock]`，禁止 Inspector 手动配置，通过 `BeatClock.Instance` 访问」

---

## Review — 2026-07-14 — Verdict: NEEDS REVISION (Revised — Pending Re-review, 5th revision)

Scope signal: M（阻塞项均为局部文档修订，无架构改动）
Specialists: game-designer, systems-designer, audio-director, unity-specialist, qa-lead, creative-director
Blocking items: 7 | Recommended: 12
Prior verdict resolved: Yes — 第四次评审（同日）9 项阻塞全部确认修复

Summary: 第五次全量评审确认第四次 9 项阻塞全部到位。新发现 7 项阻塞，核心问题为：(1) 暂停恢复公式混用原始 DSP 时间与 adjusted_beat_time，在 latency_offset 最大值（±200ms）下引入最大 200ms 相位误差，远超 AC-F2-17 门控；(2) 公式依赖图注释未随第 2 次评审 B1 修复更新（仍写 global_beat_index，应为 time_sig_segment_beat_index），直接误导实现者；(3) BeatStateSnapshot.nextBeatDspTime/nextHalfBeatDspTime 无推导公式；(4) GetConfirmedDSPStart() 零状态 0.0 与 DSP 启动时刻不可区分；(5) timeSamples==0 首帧未处理；(6) AC-F2-13 间隔计数歧义（3 事件=2 间隔，非 3）；(7) AC-F2-17 容差矛盾（[0.38,0.42]=±10ms vs 文字"±1 帧=±16.7ms"）。所有 7 项阻塞在本 session 内修订完成。

**7 项阻塞项修复摘要：**
- B1: 公式依赖图 F2-F8 注释 `global_beat_index` → `time_sig_segment_beat_index`
- B2: F2-F3 暂停恢复公式改用 `last_raw_beat_dsp_time`（F2-F5 偏移前）；新增 ⚠ 时间坐标约束：禁止混用 adjusted_beat_time；要求实现同时存储 raw 和 adjusted 两个 last_beat 时间戳
- B3: BeatStateSnapshot 表后新增 nextBeatDspTime/nextHalfBeatDspTime 显式推导公式（引用 F2-F3/F2-F4/F2-F5），并说明 PendingBPMChange 状态下行为
- B4: GetConfirmedDSPStart() 零状态哨兵 `0.0` → `-1.0`；新增 `bool IsStartConfirmed` 配套属性；AC-F2-19b Given 子句要求先检查 IsStartConfirmed
- B5: F2-F3 BGM 播放规范步骤 3 新增 timeSamples==0 首帧守卫：跳过本帧，下一帧重试（最多 3 帧，超出记录 Warning）
- B6: AC-F2-13 明确"2 个相邻间隔，非 3 个"，消除测试编写歧义
- B7: AC-F2-17 容差 `[0.38, 0.42]`（±10ms）→ `[0.367, 0.433]`（±16.7ms = 1 帧），与文字描述统一

---

## Review — 2026-07-14 — Verdict: NEEDS REVISION (Revised — Pending Re-review)

Scope signal: XL
Specialists: game-designer, systems-designer, audio-director, unity-specialist, qa-lead, creative-director
Blocking items: 6 | Recommended: 9
Prior verdict resolved: No — First review

Summary: F2 GDD 整体结构优秀（8/8 章节、9 条公式、22 条 AC、IDSPClock 依赖注入设计），但发现 1 项数学错误和 5 项规范歧义须在实现前修正。关键发现：F2-F5 延迟补偿公式符号反向（`raw − offset` → `raw + offset`），导致 160ms 补偿变为 160ms 双倍误差；AC-F2-07b 混淆了时间戳值精度（Core Rule 12 承诺）与回调触发时机（ADR-D6 决定）；暂停恢复公式丢失拍内小数进度；无音频时静默继续判定违反支柱 1「节奏即战斗」。所有 6 项阻塞问题已在本次 session 内修订完成，GDD 现为 Revised — Pending Re-review 状态，建议在新 session 运行 /design-review 进行完整再评审。

**6 项阻塞项修复摘要：**
- B1: F2-F5 公式符号 `−` → `+`；变量描述、符号约定文字、示例、AC-F2-04/05 期望值、Dependencies FE3 描述全部更新
- B2: AC-F2-07b 重写为测试 `BeatEventData.beat_dsp_time` 值精度（< 0.1ms），从 ADVISORY 升为 BLOCKING
- B3: F2-F3 暂停恢复公式补充 `fractional_progress_at_pause` 项
- B4: AC-F2-17 补充剩余时间的精确数值定义和可测参考量
- B5: AC-F2-19b 明确测试窗口为 `[dsp_start, dsp_start+2.5s)`（左闭右开），列出具体触发时刻
- B6: Core Rule 9 拆分；音频不可用 → 暂停 + 视觉节拍器；新增 AC-F2-16b；Edge Case 和 Visual/Audio Requirements 同步更新

---

## Review — 2026-07-14 — Verdict: NEEDS REVISION (Revised — Pending Re-review, 2nd revision)

Scope signal: L（从第一次评审的 XL 收窄，剩余问题为局部精度修订，无设计方向改变）
Specialists: game-designer, systems-designer, audio-director, unity-specialist, qa-lead, creative-director
Blocking items: 4 | Recommended: 7
Prior verdict resolved: Yes — 第一次评审（同日）6 项阻塞全部确认修复

Summary: 第二次全量评审确认上次 6 项阻塞项均已到位；新发现 4 项阻塞（B1: F2-F8 相位公式使用 `global_beat_index mod numerator` 在拍号切换后相位错误，须引入 `time_sig_segment_beat_index`；B2: F2-F9 `half_beat_index` 未正式声明；B3: AC-F2-16b 可测性依赖 `IAudioPlayer` 抽象缺失；B4: Core Rule 12 精度承诺为「应」非「必须」，下游无接口合约约束）和 7 项建议项（含 Addressables 异步加载死锁风险、BPM=240 可玩性边界、拍号切换玩家感知等）。所有 4 项阻塞均在本 session 修订完成；creative-director 裁定 BPM 可玩性上限和玩家感知由 C1/C4 GDD 负责，不属于 F2 范畴。GDD 现为 Revised — Pending Re-review 状态，建议 /clear 后新 session 运行第三次完整评审。

**4 项阻塞项修复摘要：**
- B1: F2-F8 引入 `time_sig_segment_beat_index`（拍号切换时归零）替代 `global_beat_index` 用于小节位置计算；保留 `global_beat_index` 仅用于时间戳；更新变量表和反例示例；同步 AC-F2-21 变量名
- B2: F2-F9 正式声明 `time_sig_segment_half_beat_index (h)`，明确归零时机（拍号切换时归零，BPM 切换时不归零），与 F2-F4 `segment_half_beat_index (m)` 区分
- B3: Core Rule 12 新增 `IAudioPlayer` 注入要求，声明最低接口成员（`PlayScheduled(double dspTime)`、`Stop()`、`IsPlaying`），与 `IDSPClock` 对称
- B4: Core Rule 12「应」改为「必须」，新增下游系统接口合约：禁止回调时读 `AudioSettings.dspTime`；要求 C1/C4/FE3 GDD 引用此合约

---

## Review — 2026-07-14 — Verdict: NEEDS REVISION (Revised — Pending Re-review, 3rd revision)

Scope signal: L（剩余问题均为局部精度修订，无设计方向变更；dsp_start_time 平台对齐决策为本次核心架构变更）
Specialists: game-designer, systems-designer, audio-director, unity-specialist, qa-lead, creative-director
Blocking items: 5 | Recommended: 10
Summary: 第三次全量评审确认第二次评审 4 项阻塞全部到位。新发现 5 项阻塞：(1) dsp_start_time 缓冲边界误差在 Android 上可达 93ms，超出 FE3 校准范围，须采用 F6 确认实际播放起始时间（ConfirmDSPStart 接口）的三步协议；(2) BeatStateSnapshot 结构体从未正式定义，字段散落各 AC；(3) 仅 8 拍 AC 不足以验证 500 拍无累积漂移承诺；(4) 首拍 Lookahead 触发时间可能为过去时，行为未定义；(5) AC-F2-16b 错误假设 PlayScheduled() 会抛出异常（实际静默失败）。所有 5 项阻塞在本 session 内修订完成；creative-director 裁定移动端 auto-resume 策略属于 F6 平台适配层职责（不阻塞 F2），BPM=240 可玩性上限由 C1/C4 负责（不阻塞 F2）。建议 /clear 后新 session 运行第四次完整评审。
Prior verdict resolved: Yes — 第二次评审（同日）4 项阻塞全部确认修复

**5 项阻塞修复摘要：**
- B1: F2-F3 BGM 播放规范扩展为三步协议（500ms 提前量 + ConfirmDSPStart() 实际确认 + 5ms 偏差告警）；F6→F2 交互表新增 ConfirmDSPStart 接口；设计决策：方案 A（F6 确认实际起始时间）
- B2: Interactions 节后新增 BeatStateSnapshot 正式字段表（9 字段，含类型/范围/零状态值；clockState、nextBeatDspTime 等补全）
- B3: 新增 AC-F2-07c [EditMode][BLOCKING]：500 拍 MockDSPClock 漂移验证，误差 < 0.1ms；覆盖矩阵同步更新
- B4: Edge Cases 新增首拍 Lookahead 过去时处理规则：立即发出携带正确未来时间戳，不跳过，单拍广播保证仍有效
- B5: AC-F2-16b When 子句更正：通过 IAudioPlayer Mock 失败信号模拟（非 PlayScheduled() 异常），并补注真实 API 静默失败行为

---

## Review — 2026-07-14 — Verdict: NEEDS REVISION (Revised — Pending Re-review, 4th revision)

Scope signal: M
Specialists: game-designer, systems-designer, audio-director, unity-specialist, qa-lead, creative-director
Blocking items: 9 | Recommended: 14
Prior verdict resolved: Yes — 第三次评审 5 项阻塞全部确认修复

Summary: 第四次全量评审确认第三次评审 5 项阻塞全部到位。新发现 9 项阻塞，核心架构问题为 ConfirmDSPStart 三步协议存在时序竞争条件（`isPlaying==true` 仅在 `targetDSPTime` 时刻才为真，与首拍 Exact 事件同帧，导致"在首拍前确认"不可实现）。决策采用方案 A（乐观预锚定）：F6 在 `PlayScheduled()` 后立即调用 `ConfirmDSPStart(targetDSPTime)` 作为初始锚点，BGM 实际播放后首帧再校正偏差 >5ms 的情况。同时补充 `actual_start` 缓冲量化精度警告（Android 23-46ms、iOS ~6ms）。其余阻塞项涵盖公式初始化缺失、除零防护、变量描述歧义、接口补全、AC 可测性修正等。所有 9 项阻塞均在本 session 内修订完成。14 项推荐项用户选择延后处理（含 7 项前次遗留 + 7 项新发现）。

**9 项阻塞项修复摘要：**
- B-A: F2-F3 BGM 播放规范重写为乐观预锚定协议（方案 A）：F6 在 `PlayScheduled()` 后立即调用 `ConfirmDSPStart(targetDSPTime)`，消除 `isPlaying` 检测竞争；播放后首帧校正偏差 >5ms 时第二次调用重锚定
- B-B: 新增 `actual_start` 缓冲量化精度警告段落：Android `timeSamples` 精度 23-46ms（buffer-granular），iOS ~5.8ms，FMOD 替代方案待 ADR-D6
- B-C: F2-F7 `last_beat_time` 初始化规则：在 `Play()` 调用时初始化为 `dsp_start_time`
- B-D: F2-F8 新增 mod 0 防护：若 `numerator == 0`（Stopped 状态），直接返回 0，跳过 mod 运算
- B-E: F2-F8 `global_beat_index` 描述修正：从"仅用于 F2-F3～F2-F5 时间戳计算"→"仅用于 `BeatStateSnapshot.globalBeatIndex` 字段输出——不参与任何内部时间戳公式计算"
- B-F: Core Rule 12 新增 `GetConfirmedDSPStart()` 公共接口要求，返回确认后的 `dsp_start_time`
- B-G: Edge Cases iOS/Android 暂停恢复公式：定义 `expected_dsp = dsp_at_suspend + (Time.realtimeSinceStartup − realtime_at_suspend)`，明确 `OnApplicationPause` 快照记录时机
- B-H: AC-F2-13 重写为显式 0-indexed 位置枚举；计入当前拍（用户决策），总计 = 4
- B-I: AC-F2-17 容差从 "≈ 0.4" → "∈ [0.38, 0.42]"，附推导公式
- B-J（附带）: AC-F2-19b 引用更新为 `BeatClock.GetConfirmedDSPStart()`（`actual_dsp_start`）

---

## Review — 2026-07-15 — Verdict: APPROVED
Scope signal: L
Specialists: game-designer, systems-designer, audio-director, unity-specialist, qa-lead, creative-director
Blocking items: 4 (全部已在本 session 修订) | Recommended: 5 (IC-1~IC-5 均已应用)
Prior verdict resolved: Yes — 第六次评审（2026-07-14）APPROVED，本次为第七次全量重审

Summary: 第七次全量对抗性评审（5 专家 + creative-director）。creative-director 从约 35 条专家 BLOCKING 发现中提炼出 4 项真实阻塞，其余归类为文档改善或下游系统职责。4 项阻塞全在本 session 修订完成：(1) TB-2 Core Rule 9 视觉节拍器时钟源由 `AudioSettings.dspTime` 改为 `Time.unscaledTime`（音频不可用时 DSP 时钟可能冻结，视觉动画须以墙上时钟驱动）；(2) TB-3 AC-F2-07b 补充 CI 策略注记（headless runner 无音频设备，本 AC 不作为 CI gate，改为 MockDSPClock 替代执行）；(3) TB-4 新增 AC-F2-23 验证 `RequestBPMChange()` 越界 clamp 行为（原有 AC 仅覆盖 `Play()` 路径）；(4) TB-1 修正 F2-F9 `is_measure_boundary` 公式为 `h > 0 && h mod 5 == 0`，修正 AC-F2-21（Post-MVP ADVISORY）。同时应用 5 项推荐项：IC-1 F2-F3/F2-F4 锚点等价性说明；IC-2 ConfirmDSPStart 步骤 3 调换 FMOD/Unity-native 主备层级；IC-3 IAudioPlayer.Stop() 立即停止语义；IC-4 EditMode 测试实例化说明；IC-5 AC-F2-15 双边界检测标准精化。systems-index 同步从 Needs Revision 更新为 Approved。
