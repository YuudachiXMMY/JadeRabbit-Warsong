# Gate Check: Technical Setup → Pre-Production

| 字段 | 值 |
|------|-----|
| **日期** | 2026-08-12 |
| **目标阶段** | Pre-Production |
| **门禁** | Technical Setup → Pre-Production |
| **Review 模式** | lean(四总监 PHASE-GATE 全跑) |
| **检查者** | `/gate-check` skill(ultracode 11-agent 工作流编排) |
| **裁决** | **CONCERNS**(advisory)|
| **Owner 决策** | 推进到 Pre-Production;裁定接受 GDD-as-spec 覆盖 Foundation 需求;`stage.txt` Concept → Pre-Production |

**编排规模**:6 侦察(工件/ADR/引擎/测试CI/追溯/UX·a11y)+ 4 总监(CD/TD/PR/AD)+ 1 对抗式 Chain-of-Verification;11/11 agent 完成,0 错误,~100 万 token,~10 分钟。全部对**实际文件内容**核验。

---

## Required Artifacts: 12/12 ✅

| # | 工件 | 状态 | 证据 |
|---|------|------|------|
| 1 | 引擎已定 | ✅ | CLAUDE.md:6-12 Unity 6.3 LTS (6000.3.19f1),无 `[CHOOSE]` 占位 |
| 2 | 技术偏好已填 | ✅ | technical-preferences.md 命名约定(L27)+ 性能预算(L37,60/30fps、Draw Calls ≤150/≤100、内存上限) |
| 3 | Art bible §1-4 | ✅ | art-bible.md(1146 行)**全 9 节**,§1 视觉身份宣言 / §2 氛围基调 / §3 形态语言 / §4 配色系统 均有实质内容 |
| 4 | 引擎参考文档 | ✅ | docs/engine-reference/unity/ VERSION.md + deprecated-apis.md + breaking-changes.md + current-best-practices.md + PLUGINS.md + modules/ + plugins/ |
| 5 | 测试框架(EditMode+PlayMode) | ✅ | Assets/Tests/EditMode/EditModeTests.asmdef + PlayMode/PlayModeTests.asmdef(TestRunner + nunit;满足 tests/unit+integration 等价条款) |
| 6 | CI workflow | ✅ | .github/workflows/tests.yml — game-ci/unity-test-runner@v4,editmode+playmode,push+PR,pin 6000.3.19f1 |
| 7 | 示例测试 | ✅ | ExampleEditModeTest.cs / ExamplePlayModeTest.cs 真实 [Test]/[UnityTest];外加 CombatCore_DiSeamSpike_Test.cs(5 个真实 [Test])|
| 8 | 主架构文档 | ✅ | architecture.md(35k,10 节:引擎知识缺口/五层版图/模块所有权/数据流/API 边界/ADR 审计/Required ADRs/架构原则/TD 签字/Open Questions)|
| 9 | 追溯索引 | ✅ | traceability-index.md(8.6k;项目版 requirements-traceability 等价)+ tr-registry.yaml(45k,152 条)|
| 10 | architecture-review 报告 | ✅ | 8 份 architecture-review-*.md(最新 08-12-coverage、08-11-postD8)|
| 11 | accessibility-requirements.md + 已提交 tier | ✅ | Status「Committed」;Committed Tier(L13):WCAG 2.2 Level AA(视觉/交互基线)+ 节奏动作类型专属补偿 |
| 12 | interaction-patterns.md | ✅ | 266 行真实模式库,P-01~P-10(焦点导航/三选一奖励卡/评级弹窗/节拍指示/连招计数/模式徽章/暂停/延迟校准/重绑捕获/结算)|

> 全部含真实内容,无空模板头。art-bible 与测试框架均**超出**本阶段最低门槛。

---

## Quality Checks

| 检查项 | 状态 | 说明 |
|--------|------|------|
| 架构决策覆盖核心系统(渲染/输入/状态) | ✅ | 12 ADR 覆盖 TDE/AI/节奏/数据/动画/音频/存档/DI/引导/死亡/胜利/取消窗 |
| 技术偏好含命名约定 + 性能预算 | ✅ | 均具体化,含 Forbidden Patterns 与 Allowed Libraries 合约 |
| 无障碍 tier 已定义并文档化 | ✅ | WCAG 2.2 AA + 类型专属补偿;部分具体准则显式延后至 PL4(设计如此,门禁仅需 tier 已定+已文档化)|
| **ADR 环依赖检测** | ✅ | **ACYCLIC**。拓扑序:D6/D7(根)→ D1 → D5/D2 → D10 → D3/D4/D12 → D8 → D13 → D14;无 A→B→A / A→B→C→A 回边。D12 Supersedes / D13 Extends 均为单向,已被 Depends-On 边覆盖 |
| 引擎版本一致性 | ✅ | 12 ADR 全部 `Unity 6.3 LTS (6000.3.19f1)`;grep 零陈旧版本(无 2022 LTS / 6000.0);D9/D11 已文档化为保留号段(architecture.md:13)|
| 所有 ADR 有 Engine Compatibility 段 | ✅ | 12/12 均含,版本已 stamp |
| 所有 ADR 有 GDD Requirements Addressed 段 | ✅ | 12/12 均含 |
| 无 deprecated-API 引用 | ✅ | grep 每条 deprecated-apis.md 条目 → 全部为**否定式引用**(明令禁用/标注不用),无一处提议调用 |
| HIGH RISK 引擎域已处理 | ✅ | URP RenderGraph(延后 PL2)/ SerializeField fields-only(编码硬约束)/ HTTP2 + Netcode 2.x(单机不适用)/ FMOD(唯一 MVP 关键路径 HIGH,ADR-D6 Accepted + 接口隔离 + Native Audio fallback 预授权;残留见 CONCERNS)|
| **追溯矩阵 Foundation 零缺口** | ⚠️ | **争议项** — 见下节。字面未达;按 GDD-as-spec 裁定后仅 9 个真缺口(全 P2/P3),Owner 已裁定接受 |
| 至少一屏 UX spec 已起草 | ⚠️ | 未达。design/ux/ 仅 interaction-patterns.md,无 hud/main-menu/pause。此为 Quality Check 非 Required Artifact → CONCERNS 非阻塞 |

---

## 争议核心 — Foundation 覆盖的「GDD=规范」定义裁定

追溯矩阵 Foundation(F2-F6)= 71 TR:**25 Covered / 5 Partial / 41 Gap**(traceability-index.md:61)。

- 字面读「zero Foundation gaps」→ 违反 → 触发草案 FAIL。
- 但矩阵自身论证:41 个 gap 中 **37 个(beatclock 13 / input 13 / save 11)是「GDD=规范」期望缺口** —— 每条 TR 都有 `gdd:` 字段指向已过 Systems-Design 门禁的权威 GDD(audio-beat-clock / input-event-system / save-persistence,status: active),是系统内部设计规格(如「BeatClock MonoBehaviour 单例 on DontDestroyOnLoad」「DSP 时间戳 <0.1ms 锚定公式」),**非**跨系统架构决策(ADR 才捕获后者)。矩阵仅按 system/layer 聚合,「41 gap」是上卷计数,非 41 条个别待决需求。
- 仅 **9 个真缺口**(tde 4 + concept 5)是真正的架构/未来 GDD 缺口,全为 P2/P3(未来 GDD / post-MVP),无一 Foundation 阻塞或在关键路径上。
- **Core 层(C1-C4)Gap = 0**(66 TR:58 Covered / 8 Partial / 0 Gap),由绿色 CI-gated DI spike(CombatCore_DiSeamSpike_Test.cs)背书。

**Owner 裁定(2026-08-12)**:接受「GDD-as-spec」框架 —— 由已过门禁的 Foundation GDD 权威规定的需求视为已覆盖,不要求 1:1 ADR-per-TR。据此 Foundation 无阻塞缺口,推进 Pre-Production。

> **追踪项**:tde-014(F6 胜利/关卡完成 TDE `TopDownEventTypes` 成员名 UNVERIFIED,D13 VR1 实现前硬门)与 tde-012(Post-MVP→FE1)保留为实现前 gate;concept-011(延迟校准→FE3)、concept-014(多武器 ComboGraph→C5)为未来 GDD。

---

## ADR 审计摘要(12 ADR)

- **状态**:11 Accepted(D1,D2,D3,D4,D5,D6,D7,D8,D10,D12,D13)+ 1 Proposed(D14)。无 Superseded。
- **D14 Proposed**:域为 CombatCore/C2 取消窗瞬态保护(Core-adjacent)。两个 quality-check 段(Engine Compat + GDD Requirements)均完整 → **本门禁通过**;但下一门禁需 Accepted。引擎 LOW 风险、spec 完整、unity-specialist 已审无 BLOCKING → 走常规 TD-ADR pass 即可在 C2 sprint 前转正。
- **环检测**:ACYCLIC(见上)。
- **版本一致性**:PASS(见上)。
- **文档漂移(非阻塞)**:D13 正文残留 Proposed-era 措辞(L56/93/110/121-123);D10 Ordering-Note(L27)误标「ADR-D8 (Combat Director)」「ADR-D9 (Combo Graph)」(D8 实为 DI/Test Seams,D9 从未创建;纯散文,不影响依赖图)。

---

## 引擎 API 安全验证

- **(a) Deprecated 使用扫描**:PASS。零提议调用;所有命中均为否定式(如 architecture.md:38 不使用 DOTS;adr-d4:101 禁用 Resources.Load 加载 SO;architecture.md:47 强制 Physics.SyncTransforms())。
- **(b) Post-cutoff API 标注纪律**:PASS。12 ADR 均含 Knowledge Risk + Post-Cutoff APIs Used;post-cutoff 处均 file:line 源码验证(如 BehaviorGraphAgent.SetVariableValue<T> @ BehaviorGraphAgent.cs:541;FMOD getDSPClock @ fmod.cs:2324/2808 —— M0 Spike 修正了「getDSPClock 在 System 上」的 pre-cutoff 误判)。ADR-D1 `.Respawn`→`.RespawnComplete`、`SetInputEnabled`→`BindButton` 教训已文档化且从未复用。
- **HIGH 域逐项**:URP RenderGraph(延后 PL2)✅ / SerializeField fields-only ✅(注:architecture.md §1 归 MEDIUM 表,breaking-changes.md:175 标 HIGH —— 严重度标签不一致,实质已正确覆盖)/ HTTP2 + Netcode 2.x(单机不适用)✅ / FMOD ✅(残留见下)。

---

## Director Panel — 全部 CONCERNS,无一 NOT READY

> Escalation:任一 NOT READY → 最低 FAIL;任一 CONCERNS → 最低 CONCERNS;全 READY → 方可 PASS。**四总监均 CONCERNS → 裁决封顶且锁定在 CONCERNS**(既非 PASS 亦非 FAIL 的域主判断)。

### Creative Director — CONCERNS
支柱忠实落入设计层(C1 header 声明 Implements Pillar 一/二;Off merit×0.5 引 game-concept:213;CancelTier Off-override 引 :402)。死亡=换局(D12/D13)忠实服务幻想;BPM ramp 的 C1-F5b 适配窗是把 flow-state 理论烘进公式层——嘉许。
- **C1**:Off 模式支柱豁免未写入 game-concept.md —— Pillar 1「节奏层不可绕过,不可关闭」与反支柱陈述为绝对,无对 Off 无障碍例外的交叉引用(例外仅存于 accessibility-requirements.md/rhythm-core.md)。设计本身健全(walled-off + merit×0.5 反刷 + CancelTier 封顶 Standard),纯文档一致性。加一行豁免指向 5 点 Off 合约。
- **C2**:平台范围分歧 —— game-concept:61 = PC(Win/macOS)+ 全手柄单平台,technical-preferences 列 PC/主机/移动 + 触控,多份 GDD 按跨平台 d-pad+touch 写。须在 UX spec 铺开前定案,game-concept 为权威。
- **C3(次)**:rhythm-core Player Fantasy 自标「creative-director 未参与本节」;本次已 clear,应在 C1 header 记录签字以退役该注。
- **C4(次,归 TD)**:D14 解析须保连招流,不得引入 hitch(load-bearing for Pillar 2 武侠手感 / Pillar 4 build depth)。

### Technical Director — CONCERNS(READY to ENTER with tracked concerns)
Foundation ADR 全 Accepted;Core Gap=0;接口逐字定义;DI spike 绿且 CI-gated;性能预算现实(聚合 ≈0.73ms/帧,~23× 余量)。
- **C1**:**FMOD 残留 HIGH 未闭** —— docs/engine-reference/unity/plugins/fmod.md **缺失**;原生二进制运行时加载 + 目标平台构建未验证(CI 中 asmdef 编译仅证 C# 面)。设为**首个 M0 pre-production spike**,门禁 F2 实现故事。Unity Native Audio fallback 预授权。
- **C2**:**InternalsVisibleTo 命名倒置** —— 编译现实用 `EditModeTests`(AssemblyInfo.cs),但 architecture.md §5.1 + ADR-D8 声明「CombatCore.Tests.EditMode」为权威。**C1-C4 编码前**统一为 `EditModeTests`,否则 Core 程序员照抄文档触 CS0122。
- **C3**:**docs/architecture/control-manifest.md 缺失** —— 故事撰写(PR-EPIC/QL-STORY-READY)嵌入其版本,/story-done 查其陈旧。故事撰写升温前先 `/create-control-manifest`。
- **C4**:D14 Proposed(不阻塞进入;C2 sprint 前转正)。
- **C5**:D13-2 TDE 胜利事件成员名未核,top-down-engine.md 零文档 —— F6 胜利桥/FE4/FE5 实现前硬门(重演 D1 `.Respawn` 教训)。
- **C6**:LP-FEASIBILITY 被跳过(lean)—— 首个 Production sprint 前建议补跑。

### Producer — CONCERNS(OPTIMISTIC on scope)
依赖排序强(无环,Foundation→Core→...→Polish);Pre-Production 次序(control-manifest → vertical-slice → playtest → ux-design → epics → stories → sprint-plan)= 教科书式风险前置,全力认可。
- **C1**:MVP 6-8 周乐观且未验证(27 系统仅 10 有 GDD;零玩法代码;v2 重启无速度数据)。视为 provisional,在 PR-EPIC + 首个 vertical-slice sprint 后 re-baseline;勿硬化为交付日期。
- **C2**:vertical slice **构建前**先锁 M0 节奏手感 PASS 标准(Off/HalfBeat/Beat 窗口值 + 「reward not friction」验收线),否则 playtest 主观不可行动。
- **C3**:playtest 协议须含**无线音频(100-300ms 延迟)真机条件** —— 该风险可使判定系统对大量玩家失效。
- **C4**:17/27 MVP 系统无 GDD —— 前两 sprint 无碍,但 epics→stories 前须先补 Feature/Presentation 层 GDD。
- **C5**:立**真正的生产风险台账**(概率/影响/owner/缓解)—— warning-ledger.md 是设计 WARNING 账,非生产风险台账。
- **C6**:D14 Proposed;刷新陈旧 systems-index Next Steps(D2 显示 pending 实为 Accepted)。

### Art Director — CONCERNS
art-bible(1147 行,全 9 节)**超出**本阶段所需;与 game-concept 视觉锚点逐字一致(水墨+拟人+2:1 等距;绛红 #9E2F2C/石青 #1685A9/金 #FFD700;汉仪尚巍手书+思源宋体)。§8 资源规范可直接投产;§7.4 HUD 微布局「待原型验证」为纪律性延后。
- **C1**:**字体商用授权** —— 汉仪尚巍手书(标题/武器名/Boss 名)在 game-concept:235 与 art-bible:870 均标「M0 阶段确认」,显式为 v1 教训(v1 曾裸用未授权资产)。M0 spike 2026-07-16 关闭时无字体授权在案记录。真实字体文件用于任何 UI/排版稿前须验证/购买并登记一行;工程期先用占位字体。
- **C2(次)**:art-bible AD-ART-BIBLE 签字未在案(header 显示 Skipped — Lean,政策一致)—— 本次 AD-PHASE-GATE 实为首次实质艺术方向质量过;建议在 art-bible sign-off 行记录本次结果。

---

## Chain-of-Verification(对抗式)

5 问,≥2 带工具动作(已亲验 SKILL.md 分类 + tr-registry.yaml 的 gdd: 字段):**verdict revised from FAIL to CONCERNS**。

1. **[TOOL]** 「zero Foundation gaps」在门禁定义中是 Required Artifact 还是 Quality Check? → 读 SKILL.md:它在 **Quality Checks** 下(非 Required Artifact);Required 的 Foundation-ADR 门槛「至少 3 个」被 12 Accepted ADR **超额 4×** 满足。FAIL 保留给「关键阻塞」。
2. **[TOOL]** 37 个 beatclock/input/save「gap」是待决架构还是 GDD 全规定的内部设计项? → grep tr-registry.yaml:每条均有 `gdd:` 指向 active 权威 GDD;无 per-TR 覆盖列,「41 gap」是聚合上卷。→ GDD-as-spec 成立。
3. 门禁总监机制是否真产 FAIL? → 否。四总监均 CONCERNS,无 NOT READY;追溯域主 TD 未将 Foundation-gap 计入 blocker。机制封底 CONCERNS。
4. 六个追溯「阻塞」是六个独立硬阻塞还是一个定义问题被计六次? → 一个根问题(per-system 分解 + 聚合)。
5. FAIL 可由文档解决还是深层设计问题? → 可解/定义性。Core Gap=0 + 绿 DI spike + 无环 ADR 图 + 版本一致;仅 9 个 P2/P3 未来缺口。最小 3 步路径:(1) Owner 裁定 GDD-as-spec 满足覆盖;(2) 修 InternalsVisibleTo + D10 标签;(3) FMOD/D13-2 保持追踪。

**Chain-of-Verification: 5 questions checked — verdict revised from FAIL to CONCERNS.**

---

## 裁决: CONCERNS(advisory)

- **非 PASS**:四总监均 CONCERNS(escalation 封顶)+ 真实待办(Foundation 覆盖定义、无单屏 UX spec、多处文档漂移)。
- **非 FAIL**:零 NOT READY、12 必需工件全过、无关键阻塞;字面 Foundation-gap FAIL 经证据下调。
- **Owner 决策**:**推进到 Pre-Production**,接受 CONCERNS 为跟踪 burndown 项,裁定接受 GDD-as-spec 覆盖 Foundation 需求。`production/stage.txt` Concept → Pre-Production。

---

## Burndown 清单(进入 Pre-Production 后,按优先序)

| # | 项 | Owner | 时机 |
|---|-----|-------|------|
| 1 | `/create-control-manifest`(control-manifest.md 缺失)| TD | 故事撰写前置(**第一步**)|
| 2 | FMOD M0 spike:原生二进制运行时加载 + 目标平台构建;补 plugins/fmod.md | TD | 门禁 F2 实现故事;首个 spike |
| 3 | 修 InternalsVisibleTo 三处(ADR-D8 L318/321/379 + architecture.md §5.1)→ `EditModeTests` | TD | C1-C4 编码前 |
| 4 | 字体商用授权确认/购买 + 登记 | AD | 真实排版稿前 |
| 5 | 平台范围定案(PC-only vs 跨平台),game-concept 为权威 | CD | UX spec 铺开前 |
| 6 | game-concept 加 Off 模式支柱豁免行 + C1 header 记 CD 签字 | CD | 文档一致性 |
| 7 | 锁 M0 节奏手感 PASS 标准 + playtest 含无线音频条件 | PR | vertical slice 构建前 |
| 8 | 立生产风险台账;re-baseline 6-8 周 MVP 估算 | PR | PR-EPIC + 首 slice 后 |
| 9 | 起草首个单屏 UX spec(HUD,per interaction-patterns:265)| UX | Pre-Production UI 深入前 |
| 10 | D14 转 Accepted;D13-2 TDE 胜利事件名核对 | TD | C2 / FE5 故事前 |
| 11 | 清扫文档漂移:D13 正文措辞、D10 Ordering-Note 标签、SerializeField 严重度标签、traceability Rev.10/v9 标签、m0-spike FMOD 导入措辞、design/CLAUDE.md a11y 路径 | 各域 | 便利时 |

---

## Next Steps(Pre-Production 序列)

1. `/create-control-manifest` — 从 Accepted ADR 生成层规则 manifest(epics 前置)
2. `/vertical-slice` — **先建 vertical slice**(epics/stories 前验证手感)
3. playtest → `/playtest-report` — ≥1 场(3+ 推荐)
4. `/ux-design [screen]` — HUD / main menu / pause 的 UX spec
5. `/create-epics layer:foundation` → `layer:core` — 手感验证后再规划
6. `/create-stories [epic-slug]` 逐 epic
7. `/sprint-plan new`

---

<!-- GATE-CHECK: 2026-08-12 | Technical Setup → Pre-Production | Required: 12/12 | Directors: CD/TD/PR/AD all CONCERNS (0 NOT READY) | ADR graph: ACYCLIC | Engine version: consistent (6000.3.19f1) | Foundation gaps: GDD-as-spec ratified by owner | CoV: FAIL→CONCERNS | Verdict: CONCERNS (owner advanced) | stage.txt: Concept→Pre-Production -->
