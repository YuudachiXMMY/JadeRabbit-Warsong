# Session State — 2026-07-21

## Current Focus

- **Epic**: Pre-Production
- **Feature**: C2 连招图系统 GDD — ✅ 全 11 章草拟完成，Status: In Review（2026-08-10，待 /design-review）
- **Task**: 下一步对 C2 跑 `/design-review design/gdd/combo-system.md`；随后 C3 心法 或 C4 战斗导演。**未决**：OQ1 C1 段数 clamp 50→999 须开 follow-up 修订 C1（本 worktree 未改 C1 文档，仅登记 registry）；OQ4 lead-programmer 须为 C1 定义只读 `IComboCounter` 供 C2 单测 mock

---

## Completed This Session

- [x] F3 Input 事件系统 GDD 首次评审（NEEDS REVISION，8 个阻塞项）
- [x] F3 GDD 修订——8 个阻塞项全部落地
- [x] 更新 `design/gdd/systems-index.md` → F3: In Review
- [x] 创建 `design/gdd/reviews/input-event-system-review-log.md`
- [x] F4 SO 数据层 GDD /design-review 完整执行（MAJOR REVISION NEEDED，10 项阻塞）→ 同 session 修订完成
- [x] F4 GDD 第二轮 /design-review（NEEDS REVISION，7 项阻塞）→ 同 session 修订完成
- [x] F5 Save/Persistence GDD 第三轮 /design-review → 同 session 修订完成（13 项编辑）
- [x] F3 GDD 第五轮 /design-review → 同 session 修订完成（10 Blocker）
- [x] `/review-all-gdds full` — Cross-GDD 全面评审（4B/16W/9I CONCERNS）
- [x] **`/architecture-review full` (v1) — 架构评审完成**
  - 69 TR-IDs registered; 2 ADRs (D6, D7); 12 Covered / 5 Partial / 52 Gap
- [x] **`/architecture-review full` (v2) — 含 ADR-D1 + 引擎专家审查**
  - Verdict: **CONCERNS**
  - 3 ADRs reviewed (D1 Proposed, D6 Accepted, D7 Accepted)
  - Traceability Matrix: 14 Covered (20.3%) / 6 Partial (8.7%) / 49 Gap (71.0%)
  - 11 Engine Specialist Findings (4 HIGH, 4 MEDIUM, 3 LOW)
  - 0 Cross-ADR Conflicts, 0 GDD Revision Flags
  - 3 Required ADRs: D5 Animation (P1), D2 Enemy AI (P2), D3 Rhythm Core (P3)
  - Report: docs/architecture/architecture-review-2026-07-15.md (Rev.2 覆盖)
  - Traceability Index: docs/architecture/traceability-index.md (新建)

---

## Files Modified This Session

| 文件 | 操作 |
|------|------|
| `design/gdd/input-event-system.md` | F3 GDD 五轮修订；20 条 AC；Status: Revised — Pending Re-review |
| `design/gdd/so-data-layer.md` | F4 GDD 两轮修订（10+7 项阻塞）；Status: In Review |
| `design/gdd/systems-index.md` | F3/F4/F5 → In Review |
| `design/gdd/reviews/input-event-system-review-log.md` | 新建，首次评审记录 |
| `design/gdd/reviews/so-data-layer-review-log.md` | 新建；首次评审 + 两轮修订记录 |
| `design/gdd/save-persistence.md` | F5 GDD 第三轮修订；Status: In Review |
| `design/gdd/reviews/save-persistence-review-log.md` | 三轮修订记录 |
| `design/registry/entities.yaml` | `enemy_attack_windup_beats` 范围更新 |
| `design/gdd/reviews/gdd-cross-review-2026-07-14.md` | Cross-GDD 评审报告 |
| `docs/architecture/tr-registry.yaml` | **新建** — 69 条 TR-ID 注册（5 系统） |
| `docs/architecture/architecture-review-2026-07-15.md` | **新建** — 架构评审完整报告 |

---

## Next Steps（按优先级）

1. **修复 HIGH 引擎发现**：
   - ADR-D6: `getDSPClock` 参数类型 uint→ulong（编译错误风险）
   - ADR-D7: 验证 `ES3.LoadRawBytes`/`ES3.SaveRaw` API 是否存在（编译错误风险）
   - ADR-D7: 为 F5 DTO 类型添加 `[Preserve]` 属性（IL2CPP stripping 风险）
2. **创建 ADR-D1（TDE Integration Architecture）** — P0 Foundation 阻塞项，解锁 F6 GDD
3. **`/design-review design/gdd/input-event-system.md`** — F3 第六轮重审（建议 `--depth lean`）
4. **创建 ADR-D5（Animation Pipeline）** — P1 Core，覆盖 Animancer + BPM 适配需求
5. **F6 GDD（未开始）** — 须 ADR-D1 完成后方可启动

---

## Open Blockers

- [x] **M0 Spike 完成（部分）**: TDE 4.5 + EasySave 3 源码验证完毕；4 BLOCKER 发现（ADR-D1 ×3 + ADR-D7 ×1）
- [x] **M0 Spike FMOD 已完成**: FMOD 2.02.35 已导入 + API 源码验证完毕；发现 B5（getDSPClock 路径）已修复；编译需 Editor 确认
- [ ] **ADR-D1 BLOCKER ×3**: B1 SetInputEnabled 不存在 / B2 暂停事件类型错误 / B3 Respawn 枚举不存在 → 须修复后 Accept
- [ ] **ADR-D7 BLOCKER ×1**: B4 ES3.Load<T> 参数顺序不匹配（编译错误风险）
- [x] **ADR-D6 Accepted（FMOD Studio 2.x）**: F2 实现阻塞已解除（API 待 FMOD 导入后验证）
- [x] **ADR-D7 Accepted（EasySave 3）**: F5 实现阻塞已解除（B4 参数顺序 + W1 RenameFile 简化待修复）
- [x] **ADR-D1 Proposed（TDE Integration）**: 已写入；M0 Spike 发现 3 BLOCKER 须修复后方可 Accept
- [ ] **BeatAccentMap（5/8 分组）**: 待 C4 GDD 确认需求
- [ ] **F6 GDD 未开始**: ConfirmDSPStart 三步协议须先有 F6 GDD

## Architecture Review Coverage Summary (Rev.5)

| 指标 | 值 |
|------|-----|
| Total TR-IDs | 79 |
| Covered | 28 (35.4%) |
| Partial | 11 (13.9%) |
| Gap | 40 (50.6%) |
| ADRs Reviewed | 5 (D1✅, D2✅, D5✅, D6✅, D7✅) |
| Required ADRs | D3 (P1), D4 (P2), D8 (P2), D9 (P3) |
| Cross-ADR Conflicts | 0 |
| GDD Revision Flags | 0 |
| Engine HIGH Risks | 0 (D2 BD risk eliminated by Unity Behavior revision) |

<!-- CONSISTENCY-CHECK: 2026-07-14 | GDDs checked: 2 | Conflicts found: 0 | Registry: 4 constants registered -->
<!-- CONSISTENCY-CHECK: 2026-07-15 | GDDs checked: 4 | Conflicts found: 0 | Stale entries fixed: 1 | Verdict: PASS -->
<!-- CROSS-GDD-REVIEW: 2026-07-15 | GDDs reviewed: 5 | BLOCKER: 4 | WARNING: 16 | INFO: 9 | Verdict: CONCERNS -->
<!-- ARCH-REVIEW: 2026-07-15 v1 | TRs: 69 | Covered: 12 | Partial: 5 | Gap: 52 | ADRs: 2 | Conflicts: 0 | Verdict: CONCERNS -->
<!-- ARCH-REVIEW: 2026-07-15 v2 | TRs: 69 | Covered: 14 | Partial: 6 | Gap: 49 | ADRs: 3 | Conflicts: 0 | Verdict: CONCERNS -->
<!-- ARCH-REVIEW: 2026-07-18 v3-updated | TRs: 79 | Covered: 22 | Partial: 8 | Gap: 49 | ADRs: 5 (4 Accepted, 1 Proposed) | Conflicts: 0 | Verdict: CONCERNS | D2 BD risk eliminated -->
<!-- ARCH-REVIEW: 2026-07-18 v4 | TRs: 79 | Covered: 22 | Partial: 8 | Gap: 49 | ADRs: 5 (all Accepted) | Conflicts: 0 | Verdict: CONCERNS | 3 new LOW specialist findings -->
<!-- ARCH-REVIEW: 2026-07-18 v5 | TRs: 79 | Covered: 28 | Partial: 11 | Gap: 40 | ADRs: 5 (all Accepted) | Conflicts: 0 | Verdict: CONCERNS | v4 arithmetic corrected -->

## Session Extract — /review-all-gdds 2026-07-15
- Verdict: CONCERNS
- GDDs reviewed: 5 (F1, F2, F3, F4, F5)
- Flagged for revision: F2, F3, F4
- Blocking issues: 4 — B1 F1↔F2 音频降级矛盾(已修复); B2 Merit无消耗汇; B3 Boss分阶段BPM无SO schema; B4 F3-F2公式无强制执行
- Recommended next: 修复 W2/W5（过期 OQ 清理）→ F6 GDD → C1 GDD（须植入 B4 offset AC）
- Report: design/gdd/reviews/gdd-cross-review-2026-07-15.md
- systems-index updated: F2/F3/F4 → Needs Revision

## Session Extract — /review-all-gdds 2026-07-15 Rev.2
- Verdict: CONCERNS
- GDDs reviewed: 5 (F1, F2, F3, F4, F5)
- Flagged for revision: F4 (systems-index 状态不一致)
- Rev.1 修复确认: B1 FIXED, W2/W10/W13 FIXED
- Blocking issues: 1 — B1 Merit无消耗汇(FE5 GDD 职责)
- Quick fixes pending: W3(F3 offset注释), W4(systems-index F4状态), W6(F1 base_damage TBD), W8(F5 AC测试数据)
- Downstream constraints: C1(6项), C3(3项), FE5(1 BLOCKER), F6(4项)
- Recommended next: 修复 4 个 Quick Fix → F6 GDD → C1 GDD
- Report: design/gdd/reviews/gdd-cross-review-2026-07-15.md (Rev.2 覆盖)

## Session Extract — M0 Spike 2026-07-16
- Verdict: CONCERNS (5 BLOCKER / 2 WARNING — 全部已修复于 ADR)
- TDE 4.5: 编译 ✅ | B1(SetInputEnabled→BindButton) ✅修复 | B2(MMGameEvent→TopDownEngineEvent) ✅修复 | B3(Respawn→RespawnComplete) ✅修复
- EasySave 3: 编译 ✅ | B4(Load<T> 参数顺序) ✅修复 | W1(RenameFile 简化) ✅修复
- FMOD 2.02.35: 已导入 ✅ | B5(getDSPClock 不在 System 上→改用 MasterChannelGroup) ✅修复 | W2(uint→ulong) ✅修复
- 编译需 Unity Editor 手动确认
- Report: docs/architecture/m0-spike-report.md (Rev.2)
- Recommended next: Unity Editor 编译确认 → ADR-D1 Accept → F6 GDD → C1 GDD

## Session Extract — /architecture-review ADR-D1 2026-07-16
- Verdict: **CONCERNS → PASS (post-fix)**
- 修复 B1: 3 个 TR-ID 错配（datalayer-010→012, input-011→015, beatclock-012 移除）
- 修复 W1: F2 GDD 依赖表 "AudioSource" → "FMOD asset"
- 修复 W2: TDE 插件参考 ADR 表 D3→D1, D4→D5
- W3 PENDING: FMOD init timing 需 Editor 运行时验证
- TR registry: 新增 TR-datalayer-012, TR-input-015（总计 71 条）
- 0 Cross-ADR Conflicts, 1 minor GDD revision flag (resolved)
- Report: docs/architecture/architecture-review-2026-07-16.md
- Recommended next: **Accept ADR-D1** → F6 GDD → ADR-D5 → ADR-D2

## Session Extract — ADR-D1 Accepted + W4/W5 Fixes 2026-07-16
- **ADR-D1 Status: Proposed → Accepted** ✅
- **W4 FIXED**: F2 GDD body text 4 处 "AudioSource" → "FMOD asset"（依赖详情段落）
- **W5 FIXED**: F4 GDD `bgmClip` → `bgmAssetRef`，类型 `AssetReferenceT<AudioClip>` → `string`（4 处）
- Review report Rev.2 written: docs/architecture/architecture-review-2026-07-16.md
- **Verdict upgraded: PASS**
- Remaining non-blocking: W3 FMOD init timing; F2 edge cases (lines 392/398/408) minor cleanup
- Recommended next: **F6 GDD** → ADR-D5 → ADR-D2

## Session Extract — ADR-D2 Written 2026-07-18
- **ADR-D2 Enemy AI Framework: Written (Proposed)** ✅
- Decision: Behavior Designer (Opsive) — 已购入，成熟 BT+Blackboard，替代 TDE AIBrain
- Engine Specialist 验证: 2 HIGH 修复（`[SharedVariable]` MonoBehaviour 误用 → `SetVariableValue()`；`CharacterMovement.SetMovement(Vector2)` 替代 `TopDownController.SetMovement(Vector3)`）
- BLOCKING before Accept: BD M0 Spike（导入 BD → 验证 SharedVariable API + Unity 6.3 LTS 编译）
- Registry: +1 interface contract (combat_director_attack_token) / +2 api_decisions (ai_beat_state_injection, enemy_movement_api) / +1 forbidden_pattern (tde_aibrain_on_bd_enemies)
- File: docs/architecture/adr-d2-enemy-ai-framework.md
- Remaining architecture-level gaps: ADR-D5 (P1 Animation Pipeline) / ADR-D3 (P3 Rhythm Core)

## Session Extract — ADR-D5 Written 2026-07-18
- **ADR-D5 Animation Pipeline — Animancer BPM-Adaptive Attack Layer: Written (Proposed)** ✅
- Decision: HybridAnimancerComponent (Animancer 8.4.0) + state-level Speed scaling (state.Speed = currentBpm / authoredBpm)
- Engine Specialist 验证: 2 修复（M1: AnimancerComponent → HybridAnimancerComponent；M2: Stop() → PlayController()）
- GDD Sync: so-data-layer.md 新增 authoredBpm 字段 + AC-F4-10 更新
- Registry: +1 api_decision (animation_attack_pipeline) / +2 forbidden_patterns (animancer_stop_for_controller_handoff, animancer_layer_speed_for_bpm) / beat_clock_lifecycle referenced_by 补充
- File: docs/architecture/adr-d5-animation-pipeline.md
- Remaining architecture-level gaps: ADR-D3 (P3 Rhythm Core)

## Session Extract — Architecture Review v3 2026-07-18
- **`/architecture-review full` (v3) — 完成** ✅
- Verdict: **CONCERNS** (不是 FAIL：Foundation 良好覆盖；不是 PASS：Core 层零 ADR)
- 5 ADRs reviewed: D1 (Accepted), D2 (Proposed), D5 (Proposed), D6 (Accepted), D7 (Accepted)
- Traceability: 79 TRs total — 22 Covered (27.8%) / 8 Partial (10.1%) / 49 Gap (62.0%)
- Delta from v2: +7.5pp coverage (20.3% → 27.8%)
- Engine Specialist: D2 SharedVariable HIGH risk confirmed (M0 Spike must inspect source)
- Cross-ADR Conflicts: 0 blocking, 2 minor (BPM clamp range, IsPlaying edge case)
- GDD Revision Flags: None
- TR Registry: +8 new entries (TR-tde-001 through TR-tde-008), total 79
- Files written: architecture-review-2026-07-18.md, traceability-index.md (Rev.3), tr-registry.yaml
- **Next priorities**: (1) Complete F6 GDD, (2) BD M0 Spike, (3) ADR-D3 Rhythm Core

## Session Extract — F6 GDD Complete 2026-07-18
- **F6 GDD 全部 8 节完成，Status: In Review** ✅
- 两个组件架构：`JadeRabbitInputManager`（角色 Prefab）+ `TDELifecycleAdapter`（DontDestroyOnLoad Manager）
- 核心规则 10 条：TDE 源码禁改、F3 Action Map 唯一权威、BeatClock 唯一控制者、FMOD 前置验证、Additive 过滤、Animancer Layer 1 边界合约、nominalBPM=120、Root Motion 禁用、订阅者生命周期
- F6-FORMULA-01: `attackAnimSpeed = clamp(currentBPM / 120, 0.5, 2.5)`，Option A 实时更新
- 7 个 Edge Cases、6+6 双向依赖表、5 个 Tuning Knobs、18 条 AC（10 PlayMode 自动化 / 1 EditMode / 5 依赖 C2 / 2 手动）
- Registry: +1 constant (nominal_attack_bpm=120) / bpm_range referenced_by 补充
- systems-index.md: F6 → In Review
- Files: design/gdd/tde-integration.md (Status: In Review), design/registry/entities.yaml, design/gdd/systems-index.md
- 下一步推荐：(1) BD M0 Spike（ADR-D2 Accept 阻断），(2) ADR-D3 Rhythm Core，(3) C1 GDD

## Session Extract — Unity Behavior M0 Spike 2026-07-18
- **Unity Behavior Package (com.unity.behavior) M0 Spike: PASS** ✅
- 版本: v1.0.16（正式发布，非 Preview）— 与 Unity 6.3 LTS 原生兼容
- 0 BLOCKER / 2 WARNING（partial class 要求 + 图编辑器绑定模式）
- API 验证: `BehaviorGraphAgent.SetVariableValue<T>()` ✅ / `Node.Status.Running/Success/Failure` ✅ / `BlackboardVariable<T>` ✅
- 功能超集 vs BD: EventChannel 事件驱动 + ObserverAbort 响应式 + RunSubgraphDynamic Boss 阶段
- **建议: ADR-D2 改选 Unity Behavior Package 替代 Behavior Designer**
- Report: docs/architecture/m0-spike-unity-behavior.md
- 下一步: (1) ADR-D2 修订, (2) ADR-D3 Rhythm Core, (3) C1 GDD

## Session Extract — Architecture Review v3 Update 2026-07-18
- **v3 报告更新** — 反映 ADR-D2 修订（BD → Unity Behavior Package）
- D2 状态: Proposed → **Accepted** ✅
- BD serialization HIGH risk **已消除**（Unity Behavior 为第一方包，M0 Spike 0 BLOCKER）
- TR-tde-008 文本修正: "Behavior Designer" → "Unity Behavior Package (com.unity.behavior)"
- 文件更新: architecture-review-2026-07-18.md, traceability-index.md, tr-registry.yaml
- Blocking Issues 更新: D2 M0 Spike 已完成 → 替换为 D5 Accept
- **下一步**: (1) ~~ADR-D5 Accept~~ DONE, (2) ADR-D3 Rhythm Core, (3) C1 GDD

## Session Extract — ADR-D5 Accepted 2026-07-18
- **ADR-D5 Animation Pipeline — Accepted** ✅ (PlayMode 验证后)
- PlayMode 验证通过: `KeepChildrenConnected=true` ✅ / `OnEnd` at Speed>1.0 ✅ / `PlayController()` handoff ✅ / TDE locomotion unaffected ✅
- 文件更新: adr-d5-animation-pipeline.md (Status→Accepted, Verification Required→全部VERIFIED)
- 架构评审更新: architecture-review-2026-07-18.md (ADR header, Coverage by ADR, Engine Specialist, Verdict, Blocking Issues, Next Steps, Dependency Order)
- Traceability Index 更新: D5 → Accepted
- 5 ADRs 全部 Accepted (D1, D2, D5, D6, D7) ✅
- **下一步**: (1) ADR-D3 Rhythm Quantization Core (P1，解锁 ~20 TRs), (2) C1 GDD

## Session Extract — F6 GDD 修订 2026-07-18
- **F6 tde-integration.md — MAJOR REVISION NEEDED → Revised (Pending Re-review)**
- 8 阻塞项全部解决：
  1. Animancer 架构对齐 ADR-D5 PlayController 模型（Core Rule 7 重写）
  2. NOMINAL_BPM → per-node `AttackNodeSO.AuthoredBpm`（Core Rule 8, Formula, AC-F6-13/18）
  3. Root Motion API 修正：`AnimancerState` → `Animator.applyRootMotion`（Core Rule 9, AC-F6-14）
  4. BGM 过渡策略新增（小节边界无缝换片，BGM 过渡策略章节）
  5. ConfirmDSPStart 三步协议新增（Core Rule 11）
  6. FMOD 异步模式验证新增（Core Rule 12, AC-F6-19）
  7. NOMINAL_BPM 安全范围修正 [60,180] → [96,160]（Tuning Knobs）
  8. AC-F6-12/15/16 重写为 ADR-D5 架构
- 用户选择 Option A（实时 UpdateBpm()），ADR-D5 需补充修订
- 新增 AC-F6-19~22；OQ-02 标记为已解决
- EC-03/04 旧 Layer 引用清理；Dependencies C2 行更新为 AnimancerAttackPlayer API
- systems-index: F6 → Revised — Pending Re-review；ADR-D5 → Accepted
- review log 已创建: design/gdd/reviews/tde-integration-review-log.md
- **下一步**: /clear → /design-review design/gdd/tde-integration.md（F6 re-review）；或推进 ADR-D3 Rhythm Core

## Session Extract — Architecture Review v4 2026-07-18
- **`/architecture-review full` (v4) — 完成** ✅
- Verdict: **CONCERNS** (与 v3 一致 — 无新 ADR，无 GDD 变更)
- 5 ADRs reviewed: D1, D2, D5, D6, D7 — 全部 Accepted
- Traceability: 79 TRs — 22 Covered (27.8%) / 8 Partial (10.1%) / 49 Gap (62.0%)
- v4 新增引擎专家发现 3 个 LOW 风险
- **下一步**: (1) ADR-D3 Rhythm Quantization Core (P1), (2) ADR-D4 SO Data Layer (P2), (3) C1 GDD

## Session Extract — Architecture Review v5 2026-07-18
- **`/architecture-review full` (v5) — 完成** ✅
- Verdict: **CONCERNS** (同 v4 — 无新 ADR，无 GDD 变更)
- v5 修正 v4 算术错误: per-system 行与 Total 行不一致
- Traceability: 79 TRs — **28 Covered (35.4%)** / 11 Partial (13.9%) / 40 Gap (50.6%)
- Delta: +7.6pp (v4 报告 27.8% → v5 实际 35.4%)
- 引擎专家发现沿用 v4（3 LOW 风险，0 HIGH）
- Files updated: architecture-review-2026-07-18.md (v5), traceability-index.md (Rev.5)
- TR registry: 79 entries unchanged
- **下一步**: (1) ADR-D3 Rhythm Quantization Core (P1), (2) ADR-D4 SO Data Layer (P2), (3) C1 GDD

## Session Extract — ADR-D3 Rhythm Quantization Core 2026-07-18
- **ADR-D3 Rhythm Quantization Core — 草稿写入 (Status: Proposed)** ✅
- 路径: `docs/architecture/adr-d3-rhythm-quantization-core.md`
- 核心决策: 节拍锚点 + 预缓冲评估（OnHalfBeatLookahead 触发 TryEvaluateEarly；LateWindow 机制处理后缓 OnAttackInput）
- 关键结构: `RhythmJudger : MonoBehaviour`（玩家 Prefab 上），`RhythmJudgmentEvent`（readonly struct，零 GC），`RhythmConfigSO`（SO 可调参数）
- 量化模式: `QuantizationMode` 枚举（Off / HalfBeat / Beat），由 FE7 运行时注入
- 接口 M3 缺口: `IBeatClock` / `IInputEventBus` / `IAnimancerAttackPlayer` 尚未在 Accepted ADR 中声明 → C1 GDD 实现 sprint 前必须解决
- SEO 风险: `OnBeatClockPaused` 必须直接绑定到 BeatClock 事件，禁止 Update 轮询（TDELifecycleAdapter 竞态）
- 引擎专家审查: 3 项 MINOR NOTES（SEO、DelayedCall 替代 Update 过期检查、QuantizationMode.Off 不变量文档）
- 已覆盖 TR: TR-concept-006/007/008, TR-input-013, TR-beatclock-003/004（估计 +6 Covered）
- 架构注册表更新完成:
  - referenced_by: beat_clock_lifecycle ✅, animation_attack_pipeline ✅
  - 新增状态: rhythm_judgment_combo ✅
  - 新增接口: rhythm_judgment_event ✅
  - 新增 API 决定: rhythm_evaluation_trigger ✅
  - 新增禁用模式: direct_beat_offset_at_input_time ✅
- Depends On: ADR-D6, ADR-D1, ADR-D5 (全部 Accepted) ✅
- Blocks: C1 GDD 实现 sprint, C2 GDD, P1 HUD, PL2 VFX
- **下一步**: (1) ADR-D4 SO Data Layer (P2), (2) C1 GDD

## Session Extract — F6 GDD 第三轮 /design-review 2026-07-19
- **F6 TDE 集成层 GDD — APPROVED** ✅
- 路径: `design/gdd/tde-integration.md`
- 第三轮完整审查（5 专家 Agent）：4 阻塞项全部解决
  - B1: authoredBpm=0 运行时 InvalidOperationException 防护（Core Rule 8 + AC-F6-13 测试用例）
  - B2: VerifyFMODAsyncMode() 协程防重入（`_isVerifyingFMOD` 门控 + `DSP_DELTA_EPSILON=0.0001f`）
  - B3: LevelConfigSO 获取路径（Core Rule 6：`scene.GetRootGameObjects()`，禁止 FindObjectOfType）
  - B4: AC 可测试性批次（AC-F6-01/03/08-10/12/15/18/21 全部改写为可独立执行路径）
- 推荐修订（5 项）：Option B 定义、DSP 容差阈值、安全范围表述、EC-03 守卫条件、Root Motion 优先级说明
- CD 裁决：4 条验证标准全部达标，无需第四轮审查
- 待办（Post-Approval）：ADR-D5 正式修订（Option A → MVP scope）；F2 GDD 补充 ScheduleBGMTransition()
- **当前状态**: F1/F2/F3/F5/F6 Approved，F4 In Review；C1 GDD 编写已解锁

## Session Extract — ADR-D4 SO Data Layer 2026-07-19
- **ADR-D4 SO Data Layer — 草稿写入 (Status: Proposed)** ✅
- 路径: `docs/architecture/adr-d4-so-data-layer.md`
- 核心决策: Unity ScriptableObjects + Addressables + Odin Editor Validation（over JSON/CSV, DOTS BlobAssets）
- 7 个 SO 类型: AttackNodeSO, ComboGraphSO, PerkSO, WeaponSO, EnemyInfoSO, LevelConfigSO, PlayerInfoSO
- 关键规则: D4-1 运行时只读; D4-2 PerkSO 唯一可变（Instantiate 克隆）; D4-3 大型 Unity Object ref 用 AssetReferenceT<T>; D4-4 AnimationClip 加载合约归 C2; D4-5/D4-6 Addressables 组策略; D4-7 Odin 双模式验证; D4-8 PlayerInfoSO EasySave 约束; D4-9 PerkParameter 值类型不变量
- 引擎专家: CONCERNS（无 BLOCKING，5 项 CONCERN 全部并入 ADR 解决）
  - C1 AnimationClip 加载合约归 C2（房间加载预解析，退出释放）
  - C2 AttackNodeSO/ComboGraphSO 须同 Addressable Group 避免 Bundle 重复
  - C3 PlayerInfoSO 禁止直接 ES3.Save，交叉引用 ADR-D7
  - C4 PerkParameter 须值类型字段（Instantiate 深拷贝安全）
  - C5 bgmAssetRef → bgmFilePath 字段名重命名（GDD 同步完成 ✅）
- GDD 同步: F4 GDD 3 处 bgmAssetRef → bgmFilePath 已更新 ✅
- 架构注册表更新完成:
  - 新增 api_decisions: so_data_container, so_large_asset_ref, perk_clone_strategy ✅
  - 新增接口: animationclip_load_contract, easysave_so_boundary ✅
  - 新增禁用模式: resources_load_for_so, easysave_direct_so_serialization, perkparameter_ref_type_fields ✅
- Depends On: ADR-D6 (bgmFilePath 格式), ADR-D5 (AssetReferenceT<AnimationClip>), ADR-D1 (TDE 边界)
- Enables: C2 GDD, C3 GDD, C5 GDD, FE1 GDD（4 个 GDD 现可开始设计）
- 全部 7 个预生产 ADR（D1-D7）现已完成 ✅
- **下一步**: (1) C1 GDD（rhythm-core.md）— 所有先决 ADR 已齐全, (2) /architecture-review（新 session）

## Session Extract — Architecture Review v6 (2026-07-19)
- **`/architecture-review full` (v6) 完成** ✅
- Verdict: **CONCERNS**（非 FAIL）
- 7 ADRs reviewed (D1-D7); 83 TR-IDs (新增 TR-tde-009~012)
- Traceability: 29 Covered (34.9%) / 8 Partial (9.6%) / 46 Gap (55.4%)
- 0 Cross-ADR Conflicts; 0 Engine Incompatibilities
- 1 MEDIUM tension: D5 vs F6 UpdateBpm() — 需 D5 一行修订
- ADR-D3 + ADR-D4 仍为 Proposed（唯一阻塞项）
- 写入文件:
  - `docs/architecture/architecture-review-v6.md` ✅
  - `docs/architecture/traceability-index.md` 更新至 Rev.6 ✅
  - `docs/architecture/tr-registry.yaml` +4 entries (83 total) ✅
- **下一步**: (1) Accept ADR-D3 + D4, (2) 修订 ADR-D5 UpdateBpm(), (3) C1 GDD rhythm-core.md

<!-- CONSISTENCY-CHECK: 2026-07-19 | GDDs checked: 5 | Stale registry: 1 (nominal_attack_bpm) | Conflicts: 0 | Registry updated: entities.yaml -->

## Session Extract — /review-all-gdds 2026-07-19
- Verdict: **CONCERNS** (1B / 12W / 32I)
- GDDs reviewed: 5 (F2, F3, F4, F5, F6)
- BLOCKER: 1 — B-01 C1 judgment window values undefined (known prerequisite)
- WARNING: 12 — 3 actionable now (F2 ScheduleBGMTransition API, F5 status FIXED, LevelBootstrap deferred), 9 downstream (C1-C5/FE5 responsibility)
- INFO: 32 — formatting, cross-refs, downstream AC dependencies
- F5 status fix: save-persistence.md "In Review" → "Approved" ✅
- Registry fix: nominal_attack_bpm notes updated for per-node authoredBpm ✅
- Comparison to 2026-07-15 Rev.2: WARNING increase from deeper Phase 3+4 analysis, not regression
- Report: design/gdd/reviews/gdd-cross-review-2026-07-19.md
- **下一步**: (1) C1 GDD rhythm-core.md, (2) Accept ADR-D3 + D4, (3) ADR-D5 UpdateBpm() 修订

<!-- CROSS-GDD-REVIEW: 2026-07-19 | GDDs reviewed: 5 | BLOCKER: 1 | WARNING: 12 | INFO: 32 | Verdict: CONCERNS -->

## Session Extract — /design-review rhythm-core.md Round 2 — 2026-07-21
- Verdict: **NEEDS REVISION** → **Revised Round 2, Pending Re-review**
- 3 blocking items resolved, 5 recommended revisions applied
- Key fixes:
  - B1: C1-F1 条件改为 `chargeStartDspTime > -1.0`（去掉 attackType 判断，与 F3 guard table 对齐）
  - B2: C1-F3/F5/E7 引入 `gridIntervalMs` 抽象（HalfBeat=halfBeatMs, Beat=fullBeatMs）
  - B3: AC-C1-07 重写为 BPM=120 具体可测断言
  - R1-R5: Rule 2 Lookahead 语义、AC-C1-03 移除伤害倍率、M3 宏 DEVELOPMENT_BUILD、Assert 条件编译、Rule 9/10 顺序修正
- systems-index: C1 → "Revised Round 2 — Pending Re-review" ✅
- review-log: 第二轮条目已追加 ✅

## Session Extract — /design-review rhythm-core.md Round 3 — 2026-07-21
- Verdict: **NEEDS REVISION** → **Revised Round 3, Pending Re-review**
- 4 blocking items resolved, 4 recommended revisions applied
- Key fixes:
  - P1: C1-F5 内联 E7 二步坍缩逻辑（Assert 之前），修正 Android BPM ≥ 186 时 Assert 误报
  - P2: Rule 1 Beat 行"触发条件"改为 C1 skip toggle；AC-C1-07 重写为含初始相位和可测断言的具体描述
  - P3: AC-C1-06 补充 `CancelTier=Advanced` 断言
  - P4: 新增 C1-F6 节——RhythmJudgmentEvent 规范性字段定义（含 ADR-D3 同步需求）
  - N/H: C1-F1 变量表 HeavyAttack 遗漏修复；C1-F3 不变量措辞改为"经 E7 坍缩修复后始终成立"
- systems-index: C1 → "Revised Round 3 — Pending Re-review" ✅
- review-log: 第三轮条目已追加 ✅
- **下一步**: /clear → /design-review design/gdd/rhythm-core.md（第四轮全深度重审）

## Session Extract — /design-review rhythm-core.md Round 4 — 2026-07-21
- Verdict: **NEEDS REVISION** → **Revised Round 4, Pending Re-review**
- 7 blocking items resolved, 1 recommended revision applied (U4)
- Key fixes:
  - P0: C1-F2 新增延迟安全约束（|latency_offset_ms| < GoodHalfWindowMs_effective）；FE7 层负责边界拦截
  - P1: Rule 7 新增 Off 模式豁免条款（立即 EmitAndClear(0)，完全绕过 IsStartConfirmed 检查）
  - P2: AC-C1-02b 边界点改为严格小于 < LateWindowMs；精确边界点明确适用 AC-C1-04
  - P3: C1-F5 末尾加 `_skipToggle = false`；AC-C1-15 补充 Beat 模式额外断言
  - P4: AC-C1-19 明确极高 BPM 下 platformMinPerfectMs 承诺被放弃，Warning 须含具体值
  - P5: AC-C1-07 拆分为 07a（正向）和 07b（负向）两个子用例
  - P6: Tuning Knobs 新增 platformMinPerfectMs 实现指引（Application.platform + 测试注入方案）
  - U4: C1-F5 Debug.Assert 注释修正（因果方向原先反向）
- creative-director 裁定：激励真空（Rule 5+9）为可接受设计选择，不列阻塞
- systems-index: C1 → "Revised Round 4 — Pending Re-review" ✅
- review-log: 第四轮条目已追加 ✅
- **下一步**: /clear → /design-review design/gdd/rhythm-core.md（第五轮，lean mode 复查即可）

<!-- STATUS -->
Epic: Pre-Production
Feature: C2 GDD combo-system.md — ✅ 草拟完成 (In Review, 11/11 章 + 39 AC)
Task: 对 C2 跑 /design-review；follow-up 修 C1 clamp 50→999 (OQ1)
<!-- /STATUS -->

## Session Extract — /review-all-gdds 2026-08-08
- Verdict: FAIL
- GDDs reviewed: 6 (F2, F3, C1, F4, F5, F6)
- Flagged for revision (marked in systems-index): F2 audio-beat-clock, F5 save-persistence (Blocking)
- Blocking issues: B1 (F2 未定义 OnBpmChanged/OnBeatClockResumed 事件); B2 (F2↔F5 初始化时序/未初始化读取矛盾); D1 (Off 模式未隔离 merit/进度, 击穿反支柱, 须先于 C5/FE5 锁定)
- Recommended next: /design-review design/gdd/audio-beat-clock.md 修复 F2 两个 Blocking, 再修 F5, 然后重跑 /review-all-gdds
- Report: design/gdd/gdd-cross-review-2026-08-08.md

## Session Extract — /review-all-gdds 2026-08-09
- Verdict: FAIL
- GDDs reviewed: 6 (F2, F3, C1, F4, F5, F6)
- Flagged for revision (marked in systems-index): C1 rhythm-core (Approved→Needs Revision)。F2 保持 Amended-Pending、F5 保持 Needs Revision（用户裁定仅标 C1）
- Blocking issues: B1 (C1 悬空引用 OnBpmChanged, F2 已改名 OnMeterChangeAccepted/OnMeterChanged; OnBeatClockResumed 未定义-OQ5 延后); B2 (F2↔F5 初始化时序, F5 无 OnInitialized-OQ4 延后待 tech-director ADR); B3 (F6 调 F2 未定义 ScheduleBGMTransition); D1 (Off 无障碍合约仍无 merit/进度隔离, 完全未解决); 3c-1 (Off 档结构性主导策略)
- 可立即修 (与 ADR 无关): C1 的 OnBpmChanged → OnMeterChangeAccepted/OnMeterChanged (F2/F6 已就位, C1/C4 未跟进)
- Recommended next: /design-review design/gdd/rhythm-core.md 修 C1 悬空引用+Off 隔离; 并推进 technical-director 裁定 OQ-4(D-4/D-5)+OQ-5 解锁 B2 三文档同轮修订
- Report: design/gdd/gdd-cross-review-2026-08-09.md
