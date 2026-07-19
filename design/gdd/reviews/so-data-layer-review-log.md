# Review Log — ScriptableObject 数据层 (F4)

---

## Review — 2026-07-19 (Sixth Pass) — Verdict: NEEDS REVISION → Revised In-Session → APPROVED
Scope signal: L
Specialists: game-designer, systems-designer, unity-specialist, qa-lead, creative-director
Blocking items: 6（session 内全部修复）| Recommended: 3（已处理）
Summary: 第六轮全量对抗性重审（4 专家并行 + creative-director 综合）在第五轮 APPROVED 基础上新发现 6 项阻塞。BLOCK-6-01（RhythmRuleModifier key strings 缺少 schema）被 creative-director 裁决降级为 Advisory（正确传递至 C3）；BLOCK-6-02（iconSprite 浅拷贝 unsafe release）降为 Tier C 文档约束。核心修复：① AnimationClip 加载归属从 F6 更正为 C2（ADR-D4 Rule D4-4 对齐）；② authoredBpm Validator 从 [MinValue(1f)] 更正为 [MinValue(60f)]，消除 1-59 静默通过漏洞；③ ComboEdge 身份模型从 int 列表索引改为 string AttackNodeSO.id（rootNodeIndex→rootNodeId，fromNodeIndex→fromNodeId，toNodeIndex→toNodeId）——消除节点重排时的静默连招破坏；④ Rule 3 新增 iconSprite 句柄共享约束文档（C3 约束声明义务）；⑤ AC-F4-02b 从 BLOCKING 降级为 ADVISORY，测试创建职责移交 C2 Story DoD；⑥ AC-F4-06 彻底删除（移至 PR checklist 模板）；⑦ AC-F4-07 删除虚构日志字符串，改为 CI 实现方确认调用路径；⑧ AC-F4-08 Condition 2 主语从 .cs 更正为 .asmdef；⑨ 新增 AC-F4-33（RegisterValidator 必须位于 Editor-only 程序集）；⑩ AC-F4-31/32 从 ADVISORY 重新归位至 BLOCKING，AC-F4-31 更新为 string ID 语义；⑪ Edge Cases 新增 cancelWeight=1.0+Standard 边界情况；⑫ Open Questions 复合效果决策锁定（Option A：C3 组合多 PerkSO 实例，F4 保持单一 effectType）。
Prior verdict resolved: Yes — 第五轮 APPROVED；本轮全量重审新发现阻塞均 session 内修复

---

## Review — 2026-07-15 (Fifth Pass) — Verdict: NEEDS REVISION → Revised In-Session → APPROVED
Scope signal: L
Specialists: game-designer, systems-designer, unity-specialist, qa-lead, creative-director
Blocking items: 7（session 内全部修复）| Recommended: 9（sprint 并行）
Summary: 第五轮全量对抗性重审（4 专家并行 + creative-director 综合）在第四轮 APPROVED 基础上新发现 7 项阻塞，均为 AC 规格 bug 和边界约束案例，无结构变更：BLOCK-R5-01 PerkTrigger 枚举在 F4-S3 表与 Open Questions 末条引用两套不相容候选值（已统一为 F4-S3 权威，Open Questions 改为"扩展候选非当前定义"）；BLOCK-R5-02/05 AC-F4-31/AC-F4-32 标为 ADVISORY 但 GDD 自述对应风险为运行时崩溃和"最危险静默失效路径"——均升级为 BLOCKING；BLOCK-R5-03 hitboxStartNormalizedTime 范围含 1.0 时跨字段约束不可满足（改为开区间 [0.0, 1.0)，附 Odin Error 要求）；BLOCK-R5-04 AC-F4-02a 使用 IsDirty() 产生结构性假阳性（重写为字段快照对比）；BLOCK-R5-06 AC-F4-03 缺少 struct→class 退化检测（追加 IsValueType 前置断言和路径 B 突变测试，第三个审查周期未落地问题本轮修复）；BLOCK-R5-07 AC-F4-08 grep 正则 `^using Sirenix` 会漏报缩进 using（去掉 `^` 锚点）。creative-director 裁决：LevelConfigSO Boss 多阶段 BPM 为 FE2 运行时责任非 F4 schema 问题；RhythmRuleModifier schema 缺口为 C3 设计决策，F4 Open Questions 节显式标注风险。所有 7 项阻塞 session 内修复完毕，F4 状态更新为 Approved。
Prior verdict resolved: Yes — 第四轮 APPROVED；本轮全量重审新发现 7 项 AC 规格阻塞，session 内全部修复

---

## Review — 2026-07-14 (Fourth Pass) — Verdict: APPROVED

Scope signal: L
Specialists: `game-designer`, `systems-designer`, `unity-specialist`, `qa-lead`, `creative-director`
Blocking items: 0 | Recommended: 0
Prior verdict resolved: Yes — 4 BLOCKING-gaps from Third Pass 均以 9 项编辑完成修复

Summary: 第四轮审查以编辑修订形式完成。creative-director 综合判定：F4 schema 结构已成熟，所有阻塞项均为 AC 覆盖缺口，可在当前会话内通过编辑修复，无需第五轮审查。9 项修订内容：① 移除 Overview 表中虚构的「掉落表引用」字段；② 新增 [NonSerialized] 字段初始化时序警告；③ 新增两条 Edge Cases（hitboxDurationNormalized=0.0 及 ComboEdge 索引越界）；④ 将 AC-F4-08 重分类为 BLOCKING 并改写为 grep 可执行检查；⑤ 重写 AC-F4-01 为 grep 类计数检查（替换不存在的 Odin 错误字符串）；⑥ 注释 AC-F4-06 为过程规则而非可测试 AC；⑦ 新增 ADVISORY AC-F4-26 至 AC-F4-32（5 类 SO 重复 ID 检测、ComboEdge 索引越界、hitboxDurationNormalized 开区间）；⑧ 扩充 Open Questions 中 C3 Rule 4 传递项（effectScope、复合效果、PerkTrigger 扩展、参数类型）。所有 game-designer 提出的 PerkSO 设计空间问题均通过 Rule 4 机制合法传递至 C3 GDD。

---

## Review — 2026-07-14 (Third Pass) — Verdict: NEEDS REVISION (4 BLOCKING-gaps)

Scope signal: M (targeted AC audit, not full GDD review)
Specialists: `qa-lead`
Blocking items: 4 | Recommended: 4
Prior verdict resolved: Partially — 17 issues from Reviews 1 & 2 were fixed, but AC-F4-08 and AC-F4-01 rewrites documented in Review 2 were NOT applied to the GDD.

Summary: Third adversarial review of AC section only, targeting 8 specific concerns. Found 4 BLOCKING-gaps and 4 RECOMMENDED issues.

**BLOCKING-gaps (must fix before implementation stories):**

1. **Missing duplicate-ID ACs for 5 SO types** — AC-F4-24 covers PerkSO but AttackNodeSO, ComboGraphSO, WeaponSO, EnemyInfoSO, and LevelConfigSO all carry `id: string, 项目内唯一` constraints with zero enforcement AC. Duplicate WeaponSO.id creates a silent save-file corruption path (PlayerInfoSO.unlockedWeaponIds resolves to wrong asset). Add ADVISORY AC-F4-26 through AC-F4-30, each using RegisterValidator + AssetDatabase.FindAssets, mirroring AC-F4-24 structure.

2. **Missing ComboEdge index-bounds AC** — AC-F4-11 covers empty nodes; AC-F4-12 covers rootNodeIndex. No AC exists for ComboEdge.fromNodeIndex or toNodeIndex exceeding nodes.Count-1. Violation causes IndexOutOfRangeException in C2 (crash). Requires RegisterValidator cross-field check. Add ADVISORY AC-F4-31; C2 GDD must add BLOCKING runtime test.

3. **AC-F4-08 When clause still untestable** — Flagged in Review 2, not fixed. "静态分析扫描" specifies no tool. Rewrite: (a) `grep -rn "^using Sirenix" Assets/Scripts/ --exclude Editor/` → 0 matches; (b) runtime asmdef compiles without Sirenix deps. Two mechanisms, both must pass.

4. **AC-F4-08 misclassified ADVISORY** — Using Sirenix.* in runtime code causes platform build failure (Rule 5 already states this). Reclassify to BLOCKING. This is the same category as a compilation error.

**RECOMMENDED (should fix before GDD is marked reviewed):**

5. **AC-F4-02b CI strategy undefined** — "须在 C2 和 C5 实现后" is a human note with no machine enforcement. Must specify: Option A (defer test file creation to C2+C5 story) or Option B (commit with `[Ignore]` attribute + mandatory unignore in C2/C5 story tracking).

6. **AC-F4-03 mutation doesn't catch struct-to-class regression** — Replacing the entire list slot (`clone.parameters[0] = new PerkParameter{...}`) passes even if PerkParameter is a class. Add second mutation path: `var p = clone.parameters[0]; p.value = 99.0f; clone.parameters[0] = p;` — this fails if PerkParameter becomes a reference type, catching the regression the current test misses.

7. **AC-F4-01 uses fabricated error string, not automatable** — "Unknown SO type" is not a real Odin output. Odin cannot count class definitions. Rewrite as grep: `grep -rl "class [TypeName]" Assets/Scripts/` → exactly 7 matches. Flagged in Review 2, not fixed.

8. **AC-F4-06 is a process rule, not an AC** — "PR描述明确列出" cannot be verified by QA or CI. Move to PR checklist or implement as a CI hook that detects F4 SO file diffs and blocks pending human acknowledgment. Flagged in Review 2, not fixed.

**Pattern alert:** AC-F4-08 (untestable When) and AC-F4-01 (fabricated error string) have been flagged across all three reviews. Two consecutive "flagged but not fixed" cycles on the same ACs. If these are not addressed before any fourth review, escalate to producer.

---

## Review — 2026-07-14 (Re-review) — Verdict: NEEDS REVISION → Revised In-Session

Scope signal: L
Specialists: `game-designer`, `systems-designer`, `unity-specialist`, `qa-lead`, `creative-director`
Blocking items: 7 | Recommended: 3
Summary: 第二轮审查发现 7 项阻塞项：cancelWeight 语义未定义（窗口开启时间点 vs 持续时长歧义，C2 无法安全实现）、PerkParameter 未声明为 struct（Instantiate 浅拷贝污染风险）、attackWindupBeats 范围与实体注册表冲突（GDD [0.5,8.0] vs 注册表 [1,4]）、大型 Unity Object 字段直接引用导致隐式 Bundle 依赖（AnimationClip/AudioClip/Sprite 须改为 AssetReferenceT<T>）、AC-F4-02 引用 Editor-only API 在 PlayMode 无效、AC-F4-03/04 测试的是 C2/C3 行为而非 F4 schema、AC-F4-05 和 AC-F4-19 测试消费系统（C3/F5）不属于 F4 验收范围。本轮同步完成全部修订：cancelWeight 改为「取消窗口开启的归一化时间点」并补充 cancelWindowType=None 交互语义；PerkParameter 加 [Serializable] struct 声明；注册表范围更新至 [0.5,8.0]；三个大型字段改为 AssetReferenceT<T>；AC-F4-02 拆分为 02a（EditMode，立即可测）+ 02b（PlayMode，延迟至 C2+C5）；AC-F4-03/04 改写为 F4 scope EditMode 测试；AC-F4-05/19 降级为 ADVISORY 并注明迁移目标文件；新增 AC-F4-25（hitboxStart + hitboxDuration ≤ 1.0 跨字段约束）；Rule 5 补充 Option A/B 职责边界说明；Open Questions 大幅扩充（C3/C2/FE5 遗留决策、MetaUpgradeEntry.id 唯一性缺口、unlockedWeaponIds cap vs enum size 不一致）。
Prior verdict resolved: Yes — 首轮 10 项阻塞项已在 2026-07-14 第一轮修订中全部解决

---

## Review — 2026-07-14 — Verdict: MAJOR REVISION NEEDED

Scope signal: XL
Specialists: game-designer, systems-designer, unity-specialist, qa-lead, creative-director
Blocking items: 10 | Recommended: 8
Prior verdict resolved: N/A — First review

Summary: F4 的字段架构整体合理，但 schema 被设计为「数据存储层」而非「数据表达层」，导致多个关键字段缺乏表达节奏游戏设计空间所需的语义粒度。ComboGraphSO 仅为线性列表而非真正的图结构（缺少边表，C2 无法实现分支连招），PerkSO 效果语法不足以表达条件触发规则（Pillar 4 构筑驱动设计空间受限），WeaponSO 缺少节奏层字段（武器间无节奏差异化），EnemyInfoSO 的攻击前摇字段精度不足（整数拍数无法表达半拍节奏）。技术层面，AttackNodeSO 以帧为单位的 Hitbox 字段在 Animancer 变速下语义失效（须改为归一化时间），PlayerInfoSO 的 `Dictionary<string,int>` 不受 Unity 原生序列化支持（须改为 `List<MetaUpgradeEntry>`），两项验收标准存在永真测试问题（AC-F4-02 误用 Editor-only API；AC-F4-05 误用对 SO 无效的 FindObjectsOfType），多项 BLOCKING AC 实为消费系统（C2/C3）行为测试而非 F4 schema 自身验证。

**本次修订已完成全部 10 项阻塞**（2026-07-14 同会话内修订）：

| 阻塞项 | 修订摘要 |
|--------|---------|
| ComboGraphSO 边表缺失 | 新增 `edges: List<ComboEdge>`（含 fromNodeIndex/toNodeIndex/requiredInput）及 ComboEdge 字段规格与设计约束 |
| PerkSO 效果语法不足 | 新增 `triggerCondition: PerkTrigger`、`maxStacks: int[1,10]`、`stackBehavior: StackBehavior` 三个顶层字段 |
| WeaponSO 无节奏字段 | 新增 `defaultQuantizationGranularity: QuantizationHint`、`perfectWindowModifier: float[0.5,2.0]` |
| EnemyInfoSO 精度不足 | `attackWindupBeats` 从 `int[1,4]` 改为 `float[0.5,8.0]`；新增 `beatPhaseAnchor: BeatPhaseAnchor` |
| AttackNodeSO 帧数字段 | `hitboxStartFrame int` → `hitboxStartNormalizedTime float[0,1]`；`hitboxDurationFrames int` → `hitboxDurationNormalized float` |
| Odin Rule 5 架构错误 | 修正为 `#if UNITY_EDITOR` 包裹方案，并补充 RegisterValidator 外部验证类替代方案 |
| PlayerInfoSO 序列化 | `Dictionary<string,int>` → `List<MetaUpgradeEntry>`（`[Serializable]` struct） |
| AC-F4-02 永真测试 | 改为 PlayMode 反射快照比对，覆盖真实游戏代码路径 |
| AC-F4-05 错误 API | 改为 C3 `_activeClones` 注册表验证，修正 FindObjectsOfType 误用 |
| AC-F4-22 编号冲突 | BLOCKING AC-F4-22 降为 ADVISORY（注明迁移至 C3 GDD）；原 ADVISORY AC-F4-22 重编号为 AC-F4-24 并将 Warning 升级为 Error |

**待 C3/C2/FE1 GDD 撰写时须引用的 F4 Open Questions（修订后仍悬而未决）**：
- `PerkEffectType` 枚举完整值 → 待 C3 GDD 定案
- `WeaponSO.animatorLayerName` 命名规范 → 待 F6/C5 GDD 联合定义
- Addressables Group 分组策略 → 待 F6 GDD
- `EnemyBehaviorProfile` 枚举扩展性 → 待 FE1 GDD
