# Gate Check: Technical Setup → Pre-Production（重验）

| 字段 | 值 |
|------|-----|
| **日期** | 2026-08-13 |
| **目标阶段** | Pre-Production |
| **门禁** | Technical Setup → Pre-Production（**重验** — 2026-08-12 已通过，Owner 已推进） |
| **Review 模式** | `lean`（四总监 PHASE-GATE 全跑） |
| **检查者** | `/gate-check` skill（ultracode 14-agent 工作流编排） |
| **裁决** | **CONCERNS**（advisory）— 较 2026-08-12 显著改善，零回归 |
| **stage.txt** | 无变更（已是 `Pre-Production`） |

**编排规模**：5 验证（必需工件 / ADR 质量 / 追溯·架构 / UX·a11y·字体·平台 / **burndown 增量**）+ 4 总监（CD/TD/PR/AD PHASE-GATE）+ 5 对抗式 Chain-of-Verification；14/14 agent 完成，0 错误，~956k token，~7 分钟。全部对**实际文件内容**核验。CoV `cov_flags=0`（无一问翻转裁决）。

> **性质说明**：本次为**重验**（re-validation）。2026-08-12 该门裁决 CONCERNS，Owner 裁定接受并推进，`stage.txt` 已设为 Pre-Production。本轮聚焦自那以来的 burndown 增量，检验是否可升级裁决。项目**保持有效处于 Pre-Production**；本裁决不构成新阻塞。

---

## Burndown 增量（重验核心）— 自 2026-08-12 报告的 11 项

| # | 项 | 状态 | 证据 |
|---|-----|------|------|
| 1 | control-manifest.md 缺失（TD-C3） | ✅ **已解** | docs/architecture/control-manifest.md 存在（423 行，ADR-D1..D13 派生层规则，各条注明 `— source: ADR-Dx`） |
| 2 | plugins/fmod.md 缺失（TD-C1） | ✅ **已解** | docs/engine-reference/unity/plugins/fmod.md（9.5k，PR #58 升级）。原生二进制运行时加载 + 目标平台构建留作 Pre-Prod M0 spike（需 Unity Editor），**按设计延后，非门项** |
| 3 | InternalsVisibleTo 倒置（TD-C2） | ✅ **已解** | 全统一 `EditModeTests`（AssemblyInfo.cs:10 + adr-d8:21/318/321/379/402 + architecture.md:111/287/415）；CS0122 照抄陷阱消除（PR #47 / ddc0b61c） |
| 4 | **字体商用授权（AD-C1，最关键）** | ✅ **已解** | font-licensing.md:44 汉仪尚巍手书 🔴 弃用 → 马善政毛笔楷书（Ma Shan Zheng, SIL OFL 1.1）+ 思源宋体（OFL）；font-licenses/ 归档齐（README + OFL-1.1.txt + hanyi-shangwei-NOT-PROCURED.md）；清单勾销「阻塞项已解」（PR #49/#53） |
| 5 | 平台范围定案（CD-C2） | ⚠️ 部分 | 决策已定（game-concept:61/262 = PC Win/macOS + 全手柄，权威），但 technical-preferences 仍列 PC/主机/移动+触控，**文档未对齐**。非阻塞 |
| 6 | Off 支柱豁免行 + C1 CD 签字（CD-C1/C3） | ⚠️ 未做 | game-concept:184 Pillar 1 仍绝对「节奏层不可绕过，不可关闭」无 Off 交叉引用；rhythm-core:35 仍留「CD 未参与本节」注。非阻塞文档一致性 |
| 7 | M0 节奏手感 PASS 标准 + 无线音频 playtest（PR-C2/C3） | 🔵 按设计延后 | game-concept:310 确认延后至 M0 里程碑文档；vertical slice 构建前须锁定 |
| 8 | 生产风险台账 + MVP 6-8 周重估（PR-C5/C1） | ⚠️ 未做 | warning-ledger.md 自标为设计 WARNING 账（「不阻塞架构门」），**非**生产风险台账（无概率/影响/owner/缓解列）。MVP 重估按设计延后至首 slice 后 |
| 9 | 首个单屏 UX spec（HUD） | ⚠️ 未做 | design/ux/ 仍只有 interaction-patterns.md — **Quality Check 非 Required Artifact → 非阻塞**；项目 Next Steps 本就排在 vertical-slice 后 |
| 10 | ADR-D14 Accept + D13-2 TDE 胜利事件名（TD-C4/C5） | ✅ **已解** | adr-d14:4 Accepted（TD+game-designer 双签 2026-08-13，PR #57）；top-down-engine.md:72-105 源码核实 `LevelComplete`（TDE 不自动触发，FE4/FE2/FE5 须 Trigger），解 TR-tde-014 硬门 + story-008（PR #50） |
| 11 | 文档漂移清扫 | ⚠️ 部分 | 已进展：traceability 维护至 Rev.12。残留：D10 Ordering-Note 误标「ADR-D8 (Combat Director)」「ADR-D9」；SerializeField 严重度 architecture.md「MED」vs breaking-changes.md「HIGH」；control-manifest 陈旧于 now-Accepted D14。最低优先级「便利时」 |

**净结果**：**6 项已解/按设计延后**（含三大最硬 TD 阻塞 + AD 字体 + D14/TDE 硬门全闭），**5 项残留全为非阻塞**（Quality Check / 文档一致性 / 计划性延后）。**零回归（无 FAIL）。** 12 必需工件全在。

---

## Required Artifacts: 12/12 ✅

| # | 工件 | 状态 | 证据 |
|---|------|------|------|
| 1 | 引擎已定 | ✅ | CLAUDE.md:8 Unity 6.3 LTS (6000.3.19f1)，无 `[CHOOSE]` |
| 2 | 技术偏好已填 | ✅ | technical-preferences.md（155 行）命名约定 + 性能预算 |
| 3 | Art bible §1-4 | ✅ | art-bible.md（1146 行，Status: Complete）§1-4 视觉身份基础齐 |
| 4 | ≥3 Foundation ADR | ✅ | 12 ADR：D10 引导/场景、D7 存档、D4 数据、D3+D6 事件/时基 等，远超 3 |
| 5 | 引擎参考文档 | ✅ | docs/engine-reference/unity/ VERSION + breaking-changes + best-practices + deprecated-apis + modules/ + plugins/ |
| 6 | 测试框架 EditMode+PlayMode | ✅ | EditModeTests.asmdef（TestRunner + nunit）+ PlayModeTests.asmdef |
| 7 | CI workflow | ✅ | .github/workflows/tests.yml — game-ci/unity-test-runner@v4（editmode + playmode） |
| 8 | 示例测试 | ✅ | ExampleEditModeTest.cs:12 [Test]；ExamplePlayModeTest.cs:12 [Test]/:20 [UnityTest] |
| 9 | 主架构文档 | ✅ | architecture.md（439 行，v1.0，10 GDD + ADR-D1..D14，TD 签字 APPROVED WITH CONDITIONS） |
| 10 | 追溯索引 | ✅ | traceability-index.md（Rev.12）+ tr-registry.yaml（153 条 v4） |
| 11 | architecture-review 报告 | ✅ | 7 份，最新 architecture-review-2026-08-12-coverage.md（CONCERNS / 0 阻塞） |
| 12 | a11y tier + interaction-patterns | ✅ | accessibility-requirements.md Status: Committed（WCAG 2.2 AA + 类型专属补偿）；interaction-patterns.md 266 行 10 模式 |

---

## Quality Checks

| 检查项 | 状态 | 说明 |
|--------|------|------|
| 架构决策覆盖核心系统（渲染/输入/状态） | ✅ | architecture.md 五层版图；12 ADR 全覆盖 |
| 技术偏好含命名约定 + 性能预算 | ✅ | 均具体化 + Forbidden Patterns + Allowed Libraries |
| 无障碍 tier 已定义并文档化 | ✅ | WCAG 2.2 AA + 类型专属补偿 |
| **12 ADR 全 Accepted**（含 D14 双签核实） | ✅ | 无一 Proposed；D14 转正 2026-08-13 |
| **ADR 环依赖检测** | ✅ | **ACYCLIC**（加入 D13/D14 仍无环）；拓扑序 D6,D7→D1→D5,D2,D10→D3,D4,D8→D12→D13,D14 |
| 引擎版本一致性 | ✅ | 12 ADR 全 6000.3.19f1；grep 零陈旧（无 2022/6000.0） |
| 所有 ADR 有 Engine Compatibility + GDD Requirements 段 | ✅ | 12/12 均含，版本已 stamp |
| 无 deprecated-API 引用 | ✅ | 唯一命中（D4:101 禁用 Resources.Load）为否定式 |
| HIGH RISK 引擎域已处理 | ✅ | FMOD（M0 + fmod.md）/ URP RenderGraph（PL2 延后）/ Input System（D1）/ HTTP2+Netcode（单机 N/A） |
| **Core 层（C1-C4）Gap = 0** | ✅ | 66 TR：59 Covered / 7 Partial / 0 Gap（A2 由 D8 闭合） |
| 追溯矩阵 Foundation 缺口 | ✅（GDD-as-spec 裁定） | 72 TR：26 Covered / 5 Partial / 41 Gap，其中 37 为 GDD-as-spec 期望缺口；真架构缺口仅 **9 个**（tde 4 + concept 5，全 P2/P3） |
| control-manifest 陈旧于 D14 | ⚠️ | 自标 EXCLUDES now-Accepted D14，需 `/create-control-manifest` regen。**非阻塞**（工件已存在，C2/D14 未进实现） |
| architecture.md 追溯 Rev 引用漂移 | ⚠️ | 引 Rev.10/152-TR，索引已 Rev.12/153-TR（差 1 TR）。装饰性漂移，非阻塞 |
| **至少一屏 UX spec 已起草** | ⚠️ | 未达（无 HUD/main-menu）。**Quality Check 非 Required → 非阻塞 CONCERNS** |

---

## Director Panel — 2 READY / 2 CONCERNS（0 NOT READY）

> Escalation：任一 NOT READY → 最低 FAIL；任一 CONCERNS → 最低 CONCERNS；全 READY → 方可 PASS。**两总监 CONCERNS → 裁决封顶且锁定在 CONCERNS。**

### Technical Director — READY ⬆️（CONCERNS→READY）
三大硬 TD 阻塞全闭：control-manifest 存在、InternalsVisibleTo 统一 `EditModeTests`、fmod.md 存在（FMOD 运行时原生加载正确归 M0 spike）。两 ADR 门也清：D14 Accepted 双签、TDE `LevelComplete` 源码核实。架构无环、12 ADR 全 Accepted、Core Gap=0、Foundation 真缺口 ~9（P2/P3 非关键路径）、预算现实（聚合 ≈0.73ms/帧）。
- **TD-N1（concern）**：control-manifest 陈旧于 D14，C2/D14 实现故事撰写前须 regen
- **TD-N4（concern）**：平台范围文档漂移带预算影响 — technical-preferences 仍带移动预算（30fps/512MB/≤100 draw），实现者可能按幻影移动目标 sizing，须对齐
- **TD-N2/N3（minor）**：D10/SerializeField 文档漂移；LP-FEASIBILITY（lean 跳过）建议首个 Production sprint 前补

### Art Director — READY ⬆️（CONCERNS→READY）
**头号顾虑字体授权已实证消解**：汉仪尚巍手书弃用 → OFL 马善政毛笔楷书（Google Fonts 核实）+ 思源宋体；font-licenses/ 归档齐；清单勾销「阻塞项已解」。art-bible 全 9 节完整，视觉锚点（国朝墨彩 / 水墨+拟人+2:1 等距 / 绛红 #9E2F2C·石青 #1685A9·金 #FFD700）与 game-concept 逐字一致，字体替换未引入漂移。
- **minor**：AD-ART-BIBLE 签字行仍「Skipped — Lean」，建议记录本次结果；平台范围（影响未来 HUD touch-target/安全区）；HUD spec 按计划在 vertical-slice 后 — 全非阻塞

### Creative Director — CONCERNS（自身 3 项未动）
支柱忠实落入设计层；死亡=换局（D12/D13）忠实服务幻想。CD-C4（D14 保连招流）ADR 面已解，残留手感检查正确归 M0。但自身三项文档一致性未动：
- **CD-C1（concern）**：game-concept:184/213 Pillar 1 绝对陈述无 Off 无障碍豁免交叉引用（Off 合约仅存 rhythm-core:53-58）。设计健全（merit×0.5 + CancelTier=Standard 反刷），加一行豁免指向 5 点合约
- **CD-C2（concern）**：平台范围决策已定（PC 权威）但 technical-preferences 未对齐
- **CD-C3（minor）**：rhythm-core:35「CD 未参与本节」注未退役，须记 C1 header 签字

### Producer — CONCERNS ⬆️（改善但未 READY）
最大顾虑（epics→stories 可执行性）**已消解**：F2-F6 五 epic 全 Ready + 42 story + 逐 story 估算 + AC 齐；F6 胜利硬门 TR-tde-014 闭合、story-008 解锁。依赖排序无环、D14 Accepted、Pre-Production 次序教科书式。
- **PR-C5（concern）**：**生产风险台账仍完全未建** — warning-ledger.md 非风险台账（无概率/影响/owner/缓解）。项目带活跃高风险（FMOD 原生加载 / 节奏手感 spike / 无线音频延迟 / MVP 估算未验证），**Producer 唯一显式请求且全未动**，Pre-Production 早期须立
- **PR-C1（concern）**：MVP 6-8 周 provisional 未重估（bottom-up 42 估算已有，无正式 re-baseline，PR-EPIC lean 跳过）。勿硬化为交付日期，首 slice 后重估
- **PR-C2/C3（concern）**：M0 节奏手感 PASS 标准 + 无线音频 100-300ms playtest 条件未锁（vertical slice 前须锁）

---

## Chain-of-Verification（对抗式）

5 问，5/5 亲验文件（≥2 要求达标）：**verdict unchanged（CONCERNS）。cov_flags=0。**

1. **[TOOL]** 无 CONCERN 实为缺失 Required Artifact？→ 确认。control-manifest 非本门必需且已存在；风险台账/UX spec/平台对齐/Off 行均 Quality Check 或非门项。「zero Foundation gaps」结构上是 Quality Check（封顶 CONCERNS 非 FAIL），已 GDD-as-spec owner 裁定
2. **[TOOL]** D14 Accepted + 依赖图无环？→ 确认。无 Proposed ADR；12 边拓扑序无回边；D14→{D3,D8} 二者均 Accepted 且不反向依赖
3. **[TOOL]** CONCERNS 是否太苛、应 PASS？→ 否。规则要求全 READY 方 PASS，CD+PR 仍 CONCERNS 封顶。单一最强支点 = **生产风险台账完全未建**；但 advisory 非必需 → 封底 CONCERNS 非 FAIL
4. **[TOOL]** 残留 CONCERNS 是否聚合成阻塞？→ 否。各自独立、各有 owner、触发点均在本门**之后**（Feature 层 UX / 首 slice 后）；最坏耦合（平台↔HUD）仍在下游
5. **[TOOL]** 有无真实 FAIL 被软化为 CONCERN？→ 无。唯一有硬阻塞潜质的字体已真实消解；control-manifest/风险台账均非必需工件；无回归、无 NOT READY、无环

**Chain-of-Verification: 5 questions checked — verdict unchanged (CONCERNS).**

---

## 裁决: CONCERNS（advisory）— 项目保持有效处于 Pre-Production

- **非 PASS**：CD + Producer 仍 CONCERNS（escalation 封顶），各自域内有真实待办（生产风险台账、平台范围对齐、Off 豁免行 + CD 签字）
- **非 FAIL**：零 NOT READY、12/12 必需工件全在、ADR 无环、**零回归**
- **较 2026-08-12**：4-CONCERNS → **2-READY（TD+AD）/ 2-CONCERNS（CD+PR）**，六项 burndown（含三大硬 TD 阻塞 + AD 字体 + D14/TDE 硬门）已闭 — 实质性升级。TD/AD 头号顾虑均实证消解
- **stage.txt**：无变更（已是 Pre-Production）

---

## 残留 Burndown 清单（全 advisory，Pre-Production 深入前按序）

| # | 项 | Owner | 时机 |
|---|-----|-------|------|
| 1 | 立**生产风险台账**（概率/影响/owner/缓解）— warning-ledger 非风险台账 | PR | Pre-Production 早期（**最强 CONCERNS 支点**） |
| 2 | technical-preferences 平台范围对齐 game-concept（PC 权威，去移动/触控 + 移动预算） | CD/TD | UX/故事铺开前 |
| 3 | game-concept 加 Off 支柱豁免行 + rhythm-core:35 记 C1 CD 签字 | CD | 首个玩家向 UX spec 前 |
| 4 | `/create-control-manifest` regen（纳入 now-Accepted D14） | TD | C2/D14 实现故事前 |
| 5 | 锁 M0 节奏手感 PASS 标准（Off/HalfBeat/Beat 窗口 + reward-not-friction 验收）+ playtest 含无线音频 100-300ms 条件 | PR | vertical slice 构建前 |
| 6 | MVP 6-8 周 re-baseline；PR-EPIC 结构性 pass | PR | 首 vertical slice 后 |
| 7 | AD-ART-BIBLE 签字行记录本次结果 | AD | 便利时 |
| 8 | 文档漂移：D10 Ordering-Note 标签、SerializeField 严重度、architecture.md 追溯 Rev 引用 | 各域 | 便利时 |

---

## Next Steps（Pre-Production 序列）

1. `/vertical-slice` — **先建 vertical slice**（Core 实现前验证核心循环手感；含 M0 节奏手感 PASS 标准）
2. playtest → `/playtest-report` — ≥1 场（3+ 推荐），含无线音频条件
3. `/ux-design [screen]` — HUD / main menu / pause 的 UX spec
4. `/create-epics layer:core` → `/create-stories [epic-slug]` — 手感验证后规划 Core 层
5. `/sprint-plan new`

> 备注：Foundation 层（F2-F6）epics/stories 已就绪（42 story），C1 rhythm-core 实现冲刺的 DI 硬门（ADR-D8）已解锁 — 亦为可行的并行/替代路径。

---

<!-- GATE-CHECK: 2026-08-13 | Technical Setup → Pre-Production (RE-VALIDATION) | Required: 12/12 | Directors: TD+AD READY, CD+PR CONCERNS (0 NOT READY) | ADR graph: ACYCLIC | ADRs: 12/12 Accepted (D14 verified) | Engine version: consistent (6000.3.19f1) | Core gap: 0 | Foundation gaps: 9 true (GDD-as-spec ratified) | Burndown: 6 resolved/deferred, 5 non-blocking open, 0 regression | CoV: 5 checked, unchanged | Verdict: CONCERNS (improved from 4-CONCERNS 2026-08-12) | stage.txt: unchanged (already Pre-Production) -->
