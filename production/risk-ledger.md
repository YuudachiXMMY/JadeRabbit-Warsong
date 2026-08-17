# 生产风险台账（Production Risk Ledger）

| 字段 | 值 |
|------|-----|
| **文档类型** | 生产风险台账（Risk Register：概率 / 影响 / owner / 缓解） |
| **Status** | Active（活账本，滚动维护） |
| **Owner** | Producer(台账 owner；各条风险有独立 owner) |
| **建账** | 2026-08-16 |
| **来源** | `/gate-check` pre-production 2026-08-13 残留 burndown #1 / PR-C5（「**最强 CONCERNS 支点**：生产风险台账仍完全未建」`pre-production-2026-08-13.md:101,134`） |
| **复审时机** | 每次 `/gate-check`、每 sprint 计划、vertical slice / 每里程碑后 |

> **与 `warning-ledger.md` 的区别**：`warning-ledger.md` 是**设计一致性 WARNING 账**（自标「不阻塞架构门」，无概率/影响/owner/缓解列），跟踪跨 GDD 复审的悬留设计项。**本台账**是**生产风险台账**——技术 / 进度 / 美术 / 商业风险，带概率、影响、曝险评分、owner、缓解与应急，服务 Producer 的风险管理。门报告明确二者不可互替（`pre-production-2026-08-13.md:30,101`）。

---

## 评分图例

**概率**：高（多半会发生）/ 中（可能发生）/ 低（不太会但需防）
**影响**：高（威胁产品成立 / 关键路径 / 法律）/ 中（拖期或返工）/ 低（可吸收）
**曝险（Exposure）= 概率 × 影响**：🔴 Critical（高×高）/ 🟠 High / 🟡 Medium / 🟢 Low
**状态**：🔴 Open（未缓解）/ 🟠 Mitigating（缓解进行中）/ 🔵 Monitoring（有承接，监控）/ 🟢 Accepted（知情接受）/ ✅ Closed

---

## 1. Critical / High 曝险（关键路径 — 门报告点名的活跃高风险）

| ID | 风险 | 概率 | 影响 | 曝险 | Owner | 缓解措施 | 状态 |
|----|------|------|------|------|--------|----------|------|
| **R-FMOD-01** | FMOD 原生加载 / 目标平台构建：Unity 6.3 原生插件校验管线变更；`getDSPClock` 挂 master channel group 非 `FMOD.System`；F5 DTO 缺 `[Preserve]` 触 IL2CPP stripping；同步模式产生「零偏移」时间戳 | 中 | 高 | 🔴 | TD / engine-programmer | M0 spike 目标平台 build（需 Unity Editor）；F5 DTO 加 `[Preserve]`；强制 `FMOD_INIT_NORMAL`（禁 `SYNCHRONOUS_UPDATE`）；BGM 锚点 `FMOD_CREATESAMPLE`；非 UPM 手动锁版本 | 🟠 Mitigating |
| **R-FEEL-01** | 节奏手感未验证：核心假设「节奏层是奖励而非摩擦」未经外部试玩——「整个产品是否成立的关口」 | 中 | 高 | 🔴 | Producer / game-designer | `production/milestones/m0-rhythm-feel-acceptance.md`（M0 PASS 判据）；vertical slice playtest；`InputAction.performed` 时间戳（禁 Update 轮询） | 🟠 Mitigating |
| **R-AUDIO-01** | 无线音频延迟 100-300ms：不校准则整个判定体系失效；校准体验差会在首分钟劝退玩家 | 高 | 高 | 🔴 | Producer / audio-director / FE3 owner | FE3 延迟校准系统；`LatencyOffsetMs` 两阶段写者（ADR-D10）；校准界面高延迟提示「建议有线」+ 手动偏移；A11Y-6 gate（100ms 延迟校准后 Perfect ≥ 80%） | 🔵 Monitoring |
| **R-TDE-01** | TDE 边界覆写陷阱：v1「582 行覆写陷阱」重演；Animancer 攻击层与 TDE Animator 层叠加冲突修复成本极高 | 中 | 高 | 🟠 | F6 / lead-programmer | 禁改 TDE 源码（technical-preferences 强制）；继承 + 事件扩展；覆写走 ADR；F6 GDD Layer 1 边界合约 | 🔵 Monitoring |
| **R-ART-01** | 美术产能（v1 教训）：v1 美术资源几乎空缺 | 中 | 高 | 🟠 | Art Director | 早期定代理资源规范；美术风格独立验证；art-bible 已完成 §1-9 | 🔵 Monitoring |
| **R-LICENSE-01** | 第三方授权 / 字体商用（v1 教训）：未授权资产 / 版权 BGM 触发下架或索赔 | 低 | 高 | 🟠 | Art Director | font-licensing.md 登记 + OFL 字体替换（汉仪尚巍手书弃用 → 马善政毛笔楷书 + 思源宋体）；font-licenses/ 归档；**任何新捆绑资产进包前须登记** | 🟠 Mitigating |
| **R-MARKET-01** | 细分受众交集不足：节奏 + Roguelike + 中国古风三受众交集须够大以撑销售；竞品稀少双刃 | 中 | 高 | 🟠 | creative-director / Producer | 差异化钩子（兔子武侠 + 中国古典）；竞品对标 Hades / Dead Cells / Hi-Fi Rush；Premium $19.99 定位 | 🔵 Monitoring |

---

## 2. Medium 曝险

| ID | 风险 | 概率 | 影响 | 曝险 | Owner | 缓解措施 | 状态 |
|----|------|------|------|------|--------|----------|------|
| **R-SCHED-01** | MVP 6-8 周估算未验证（PR-C1）：provisional 估算未 re-baseline | 中 | 中 | 🟡 | Producer | bottom-up 42-story 估算已有；首 slice 后 re-baseline；勿硬化为交付日期 | 🔵 Monitoring（按设计延后） |
| **R-LEARN-01** | 学习曲线三层叠加：节奏精度 + 连招树 + 心法构筑压力叠加致新手快速放弃 | 中 | 中 | 🟡 | game-designer / UX | Off 无障碍档 + 容错式判定（Miss 不清零）+ onboarding；挂 M2 试玩 | 🔵 Monitoring |
| **R-FRAME-01** | 帧量化误差：Update 轮询判定在 60FPS 误差达 16.7ms > 判定窗容差 | 低 | 中 | 🟡 | TD / gameplay-programmer | 架构原则强制 `InputAction.performed` 时间戳（`architecture.md:389`）；PC Perfect 半窗 ≥ 16.7ms 下限 | 🟠 Mitigating |
| **R-OFFDOM-01** | Off 模式成为主导刷取策略：恒 Perfect 若全额计入永久进度 → 零技巧最优刷取，击穿反支柱 | 低 | 高 | 🟡 | C1 / C5 / FE5 | 四重隔离（merit ×0.5 + CancelTier 封顶 Standard + 排行/成就隔离 + perk 封顶 1.0）；**阻塞下游合约**待 C5/FE5 实装（`rhythm-core.md:754`）；采样时刻/退出套利子向量见 R-ECONEXP-01 | 🔵 Monitoring |
| **R-ENGINE-01** | Unity 6.3 知识缺口：LLM 训练截止 2025-05，Unity 6 系列 API 大改（DOTS/URP/Input） | 中 | 中 | 🟡 | TD | engine-reference/ 校准文档；API 前交叉核对；grep 零陈旧版本已验证（12 ADR 全 6000.3.19f1） | 🟠 Mitigating |
| **R-COMBO-01** | combo 无 sink 触顶饱和（DT-1）：Miss 不清零 + 无减项，触顶恒 ×5.0，整局倍率与踩准脱钩 | 中 | 中 | 🟡 | game-designer / C2 | 评估 combo sink（Miss 缓衰减）或后移触顶点；挂里程碑（`warning-ledger.md` DT-1） | 🔵 Monitoring |
| **R-TELEGRAPH-01** | 高 BPM telegraph < 人类反应下限（DT-4）：`attackWindupBeats` 以拍计，Boss BPM~200 时 0.5 拍=150ms | 中 | 中 | 🟡 | C4 / FE4 | 高 BPM 设绝对毫秒下限（≥250ms），或高 BPM 阶段只用高前摇敌人；挂里程碑 | 🔵 Monitoring |
| **R-URP-01** | URP RenderGraph：6.3 完全移除 URP Compatibility Mode，旧 URP 渲染代码须迁移 | 中 | 中 | 🟡 | TD / technical-artist | PL2 延后（MVP 后期）；迁移 RenderGraph API | 🔵 Monitoring（延后） |
| **R-PLATDRIFT-01** | 平台范围文档漂移（TD-N4）：technical-preferences 仍带移动预算，实现者按幻影移动目标 sizing | 中 | 低 | 🟡 | CD / TD | 对齐 technical-preferences 到 PC 权威（`game-concept.md:61`）；去移动/触控 + 移动预算 | 🔴 Open |
| **R-TDEEVT-01** | TDE 胜利/通关事件成员名臆断（D13 VR1）：不得凭训练数据臆断（参照 `.Respawn`→`.RespawnComplete` 教训） | 低 | 中 | 🟡 | TD / FE4 / FE5 | `LevelComplete` 源码已核实（`top-down-engine.md:72-105`）；TDE 不自动触发须显式 Trigger | 🟠 Mitigating |
| **R-BEATTIER-01** | Beat 档缺护栏对称性（DT-2）：Beat 放宽判定窗却保留 CancelTier 全开 + perk 全额 + 功勋全额，唯一代价攻击密度减半 → 或成低技巧最优档 | 中 | 中 | 🟡 | game-designer / C2 | M0 Spike PASS 判据纳入「Beat vs HalfBeat DPS 平衡对拍」（`warning-ledger.md:40`） | ⚪ 挂 M0 |
| **R-ATTNBUDGET-01** | 注意力预算 / 听觉双载（DT-3，区别于 R-LEARN-01 学习曲线，此为稳态感官带宽冲突）：C1 用耳定位自身攻击网格、C4 又用同耳听敌人前摇，进攻量化与防御预读挤同一带宽 | 中 | 中 | 🟡 | game-designer / UX | M2 试玩专项测量；必要时防御 telegraph 迁视觉通道（`warning-ledger.md:41`） | ⚪ 挂 M2 |
| **R-PHYS-01** | Unity 6.3 Physics 变更：`autoSyncTransforms` 弃用须显式 `Physics.SyncTransforms()`；transform 未同步致 C6 命中/落空错误（战斗正确性，可能**静默出错**难察觉） | 中 | 中 | 🟡 | TD / gameplay-programmer | hitbox 激活前调 `Physics.SyncTransforms()`；复核 solver 迭代数（`architecture.md:47`） | 🔴 Open |
| **R-ADDR-01** | Addressables 6.2+ 加载失败**抛异常**（非返回 null）；C2 忘预加载致首次攻击帧卡顿 / `PlayAttack()` NRE | 中 | 中 | 🟡 | TD / C2 | 加载点一律 try/catch（ADR-D4）；C2 房间加载预加载全部 `AttackNodeSO.animationClip`、退出释放（`architecture.md:45` / `adr-d4:98,316` / `adr-d5:327`） | 🔵 Monitoring |
| **R-ES3-01** | EasySave 3 v3.x 与 Unity 6.3 LTS 兼容性未 spike 验证：不兼容将阻塞 F5 存档实现（与 R-FMOD-01 平行的第三方中间件兼容风险） | 低 | 高 | 🟡 | F5 owner | M0 Spike 验证 ES3 import + Save/Load 往返；干净回退 `IFileStorage` 切 JsonStorage（ADR-D7 已预授权，`adr-d7:245`） | 🔵 Monitoring |
| **R-ECONEXP-01** | 经济完整性套利（反支柱经济侧，补 R-OFFDOM-01）：SG-4 Off 功勋采样时刻未定义（全程 Off 输出、击杀前切回 HalfBeat 拿全额）+ D13 退出/强杀规避死亡结算 | 中 | 低 | 🟡 | FE5 / C5 / game-designer | FE5 GDD 钉死功勋采样时刻 + 防退出套利边界，列 Blocking DoD（`warning-ledger.md:25` SG-4 / `adr-d13` Open Follow-up） | 🔵 Monitoring |

---

## 3. Low / Accepted 曝险

| ID | 风险 | 概率 | 影响 | 曝险 | Owner | 缓解措施 | 状态 |
|----|------|------|------|------|--------|----------|------|
| **R-SAVESEC-01** | 存档加密密钥包体内明文：逆向可提取（ADR-D7） | 高 | 低 | 🟢 | security-engineer | MVP 阶段知情接受（存档无安全价值）；发布前若需再评估 | 🟢 Accepted |

---

## 4. 头号三风险详解（触发信号 + 应急预案）

### R-FMOD-01 — FMOD 原生加载 / 目标平台构建
- **为何 Critical**：架构评审确认「唯一真正落在 MVP 关键路径上的 HIGH 风险」（`architecture.md:55`）。
- **已消解部分**：Editor + macOS arm64 原生库加载 ✅（`fmod.md:23`「Refreshing native plugins … found 7 plugins」）；C# 编译 ✅；getDSPClock BLOCKER B5 + [Preserve] 缺失均在 M0 spike 报告记录并给出修复。
- **残留**：FMOD Studio banks 待首次目标平台 Build 验证（`architecture.md:434`）；`[SerializeField]` 属性用法在 6.3 变编译错误须扫。
- **早期信号**：目标平台 build 失败 / getDSPClock 返回异常值 / 设备静音 / 某帧输入 `inputDspTime` 返回重复「零偏移」时间戳。
- **应急预案**：若 6.3 不兼容，MVP 回退 Unity Audio DSP 备选（ADR-D6 Alternative 1，`adr-d6:163`）。
- **来源**：`adr-d6-audio-middleware.md:162-168` / `m0-spike-report.md:216-271` / `input-event-system.md:295,457`。

### R-FEEL-01 — 节奏手感未验证
- **为何 Critical**：「核心假设『节奏层与动作手感的融合让玩家感受一体感而非附加摩擦』——这是整个产品是否成立的关口，必须在 M2 里程碑外部试玩中验证」（`game-concept.md:288` / `systems-index.md:189`）。
- **早期信号**：playtest 手感 Likert 中位数 < 4 / 出现「我明明踩了那一拍却 Miss」（`rhythm-core.md:33`）/「再来一局」自发率低 / 测试者说「手感很差」。
- **应急预案**：放宽 Perfect/Good 窗后复测；默认档 HalfBeat→Beat；强化 Perfect Kinetics 反馈（音效/特效，不改动画时长——支柱二）。
- **主缓解载体**：`production/milestones/m0-rhythm-feel-acceptance.md`（M0 PASS 判据）。

### R-AUDIO-01 — 无线音频延迟
- **为何 Critical**：「无线耳机延迟 100-300ms；不校准则判定体系不成立（v1 未处理）」（`game-concept.md:294`）；「延迟校准体验差会在首分钟劝退玩家」（`game-concept.md:290`）。
- **技术护栏**：`|latency_offset_ms| < GoodHalfWindowMs_effective`（默认 120ms），超出始终 Miss，破坏「踩准有奖励」合约（`rhythm-core.md:259`）；`latency_offset_ms` ∈ [-200,200]（`audio-beat-clock.md:306`）；禁读 `AudioSettings.dspTime`（缓冲边界延迟 10-93ms 校准无法消除，`audio-beat-clock.md:273`）。
- **早期信号**：BT 耳机 playtest 判定系统性偏移 / 校准流程 > 60s / 有人因校准放弃。
- **应急预案**：校准界面提示「检测到高延迟，建议使用有线耳机」+ 允许手动输入偏移（`game-concept.md:449`）。
- **依赖缺口**：FE3 延迟校准 GDD 尚未创建（`systems-index.md:36` Not Started）——列为 R-AUDIO-01 的解阻前置。

---

## 5. 复审与清账

- **复审节奏**：每次 `/gate-check` / 每 sprint 计划 / 每里程碑（尤其 vertical slice 后——头号三风险 R-FMOD-01 / R-FEEL-01 / R-AUDIO-01 正是 vertical slice 会暴露的）。
- **清账规则**：风险关闭须注明证据（PR / commit / 验证报告），状态转 ✅ Closed 并保留历史行。
- **升降级**：概率/影响变化时更新曝险色标，并在变更记录留痕。

## 变更记录

- **2026-08-16** — 建账（本文件）。纳入 24 条风险：门报告点名的 4 条活跃高风险（R-FMOD-01 / R-FEEL-01 / R-AUDIO-01 / R-SCHED-01）+ v1 两教训（R-ART-01 美术产能 / R-LICENSE-01 授权）+ 各 ADR Risks 段与 systems-index High-Risk 表条目。经对抗式验证补收 6 条（R-BEATTIER-01 Beat 欠代价 / R-ATTNBUDGET-01 听觉双载 / R-PHYS-01 Physics 同步 / R-ADDR-01 Addressables 加载 / R-ES3-01 EasySave 兼容 / R-ECONEXP-01 经济套利）。来源见各条引用。对应 gate PR-C5「最强 CONCERNS 支点」。

<!-- RISK-LEDGER: 2026-08-16 | Active | 24 risks | Critical: R-FMOD-01/R-FEEL-01/R-AUDIO-01 | Source: gate PR-C5 | Columns: 概率/影响/owner/缓解(+曝险/触发/应急/状态) | Distinct from warning-ledger (design WARNING账) -->
