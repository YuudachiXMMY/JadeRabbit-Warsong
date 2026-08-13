# Session State — 2026-07-21

---

## 🔄 HANDOFF — 2026-08-11 · OQ-4/OQ-6 已闭环 · 待独立 /architecture-review

**本 session 结论（`/architecture-decision 处理 OQ-4/OQ-6`）**：无新决策/新 ADR/新 GDD 修订待办——OQ-4 与 OQ-6 已由既有 Accepted ADR **全链路闭环**（决策 + 下游传播均已落地，在已合并 worktree `gdd-blockers-2026-08-10` 完成）。

| 阻塞 | 决策载体 | 状态 |
|---|---|---|
| **OQ-4**（B2b 初始化时序 / D-4 `ISaveService` 同步 seam / D-5 `LatencyOffsetMs` 两阶段写者） | `docs/architecture/adr-d10-bootstrap-sequencing.md` | Accepted 2026-08-09 |
| **OQ-6**（BeatClock 主线程 poll 模型，否决 `OnAudioFilterRead`/`ConcurrentQueue`） | `docs/architecture/adr-d6-audio-middleware.md` §Addendum | Accepted 2026-08-08 |

**下游传播（已同轮落地，勿重复修）**：
- F2 `audio-beat-clock.md`：Rule 7/10（两阶段写者+禁自 load，`:49/:57`）、Rule 12/14（normative 主线程，删 ConcurrentQueue 条件句，`:65/:74`）、依赖章"无 F5 直接依赖"（`:457`）、OQ-4/OQ-6 strikethrough 标已解决（`:755/:757`）。OQ-5 仍开放。
- F5 `save-persistence.md`：Rule 14 `Initialize()` 同步阻塞、Rule 15 `ISaveService` seam（`:88/:90`）、`LoadLatencyCalibration()` 调用方迁至 F6。
- F6 `tde-integration.md`：Rule 13 F5-init gate + boot-inject（`:105`）、**AC-F6-23**（5 子用例，headless via `MockSaveService`，`:599`）。
- registry `architecture.yaml`：`LatencyOffsetMs` 两阶段 stance（`:87`）+ `fe3_forward_stance`（`:89`）+ `ISaveService` 接口（`:278`）+ forbidden pattern `f2_self_load_latency_from_f5`（`:446`）。
- systems-index：F2/F5/F6 均标 `Revised/Amended (ADR-D10 同轮) — Pending Re-review`。

**➡️ 下一步（用户已选：新 session 验证）**：在**全新 Claude Code session** 运行 `/architecture-review`（隔离原则：审查须独立于撰写上下文，禁止与 `/architecture-decision` 同 session）。审查焦点：
1. ADR-D10 D-4/D-5 是否被 tr-registry 追踪，F2/F5/F6 三文档引用一致。
2. ADR-D6 Addendum 主线程模型在 F2 Rule 12/14 完整体现、无残留 `ConcurrentQueue` 条件句。
3. FE3 forward-stance（registry `:89`）纳入校验——未来 FE3 GDD 违反"运行期唯一写者/主线程注入"须报警。
4. F2/F5/F6 三文档 `Pending Re-review` 是否具备升级 Approved 的一致性条件。

**仍开放（未在本次范围）**：OQ-5（`OnBeatClockPaused`/`OnBeatClockResumed` 暂停/恢复事件，原 B2 遗留），卡 `IAudioPlayer.Pause()/Resume()` 接口缺失 + C1/C2 消费确认。

---

## Current Focus

- **Epic**: Pre-Production
- **Feature**: C2 连招图系统 GDD — ✅ 全 11 章草拟完成，Status: In Review（2026-08-10，待 /design-review）
- **Task**: 下一步对 C2 跑 `/design-review design/gdd/combo-system.md`；随后 C3 心法 或 C4 战斗导演。**未决**：OQ1 C1 段数 clamp 50→999 须开 follow-up 修订 C1（本 worktree 未改 C1 文档，仅登记 registry）；OQ4 lead-programmer 须为 C1 定义只读 `IComboCounter` 供 C2 单测 mock

---

## Session Extract — /create-architecture (TD 主笔) 2026-08-12

- **交付物**：`docs/architecture/architecture.md` v1.0 — 首次建立主架构蓝图（门禁硬长杆）
- **TD Sign-Off**：APPROVED WITH CONDITIONS（4 条非阻塞 condition：H1 D8 InternalsVisibleTo 命名统一、D3 文本漂移、D14 仍 Proposed、D13-2 胜利事件名待引擎核对）
- **LP-FEASIBILITY**：SKIPPED — Lean 模式（非 PHASE-GATE）
- **综合来源**：10 GDD + 12 ADR(D1-D8,D10,D12,D13 Accepted;D14 Proposed) + traceability Rev.10(152 TR/89 Covered/Core Gap=0) + 引擎知识缺口(FMOD HIGH 为唯一 MVP 关键路径 HIGH 风险，已 M0 消解 API 面)
- **文档结构**：Status / 引擎知识缺口 / 五层版图 / 模块所有权 / 数据流 / API 边界(D8 五接口逐字) / ADR 审计 / Required ADRs / 5 条架构原则 / Open Questions
- **门禁就绪**：架构长杆已具备；pre-production gate 剩余前置 = ADR-D14 转 Accepted(仅阻塞 C2 BD-2，非 C1 关键路径) + /test-setup + /ux-design
- **下一步 ADR（优先序）**：① ADR-D14 转 Accepted ② 核对 TDE 胜利事件名(D13-2) ③ /architecture-decision 延迟校准(FE3)/武器系统(C5)
- **Worktree**：`.claude/worktrees/create-architecture-2026-08-12`（branch `worktree-create-architecture-2026-08-12`）→ draft PR

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

## Session Extract — /review-all-gdds 2026-08-10
- Verdict: FAIL
- GDDs reviewed: 9 (F2/F3/F4/F5/F6 + C1/C2/C3/C4；本轮新增 C2/C3/C4)
- Flagged for revision (marked in systems-index): C1 rhythm-core (Approved→Needs Revision)、C3 perk-system (Designed→Needs Revision)。C2/C4 已是 Needs Revision。F4/F2/F6 在 review log 跟进，索引维持 Pending Re-review/Approved
- Blocking issues: BC-1 (C2 Perfect rating 1.5 vs 权威 1.0 污染平衡); BC-2 (C3 IRhythmModifierProvider 在 C1 无消费点→心法零效果，最严重); BC-3 (C4 读 F4 未定义 EnemyInfoSO 字段); BC-4 (C4 6/8 + C3 3/8 非法拍号); BC-5 (C2 AttackType vs F4 AttackInputType 枚举不可编译); BC-6 (OnBeatClockResumed 缺失+暂停恢复三系统信号不一致); BD-1 (Off 升级为战力主导策略); BD-2 (BPM 跃升 C1/C2/C4 三系统难度尖峰); SC-1 (音频断开降级无承接者); SC-2 (复活幽灵评级)
- 回归: B1 (C1 OnBpmChanged) 已解决; C4 未犯 B1; OnBeatClockResumed/B3 ScheduleBGMTransition 仍缺失
- 可立即修 (与 ADR 无关): BC-1 rating 1.5→1.0; BC-4 非法拍号; BC-7 C1 ComboMax 50→999; W-03/W-04 C3 事件名
- Recommended next: 优先修 BC-2 (C1 定义 IRhythmModifierProvider + C3 同轮)、BC-1、BD-1；机械修订 BC-1/BC-4/BC-7 可先落地
- Report: design/gdd/gdd-cross-review-2026-08-10.md

<!-- CONSISTENCY-CHECK: 2026-08-10 | GDDs checked: 9 | Registry entries: 9 (2 formulas, 7 constants) | Conflicts found: 2 (RESOLVED: rating_multiplier Perfect 1.5→1.0 in combo-system.md + registered — 对应 /review-all-gdds BC-1; TRACKED: combo_max 999 vs rhythm-core ComboMax 50 = BC-7, pending C1 revision) | Missing-registry candidates: 11 | Log: docs/consistency-failures.md -->
<!-- CONSISTENCY-CHECK-FOLLOWUP: rhythm-core.md ComboMax 50→999 (C1 债务=BC-7, 已批准待修订); 建议登记 11 条跨系统事实 (final_damage 总公式, attackAnimSpeed 公式, beat_offset_ms, half_beat 30000/bpm, platformMinPerfectMs, 0.1ms 精度承诺, BPM=0→120 回退, CancelTier 映射, 拍号定义域, 500ms BGM 提前量, 5ms 重锚阈值) -->


## Session Extract — /architecture-review 2026-08-11
- Verdict: CONCERNS
- Requirements: 146 total — 81 covered, 15 partial, 50 gaps (registry v1→v2, 首次纳入 Core C1-C4 共 63 TR)
- New TR-IDs registered: 63 (TR-rhythm 18, TR-combo 14, TR-perk 13, TR-director 18)
- GDD revision flags: combat-director.md (ADVISORY, E1 同帧保证; 本轮未改 systems-index)
- Top ADR gaps: ADR-D8 DI/测试缝隙契约(A2, 最高价值); 修订 D1 命名+生命周期(A1/A3); 修订 D2 同帧保证(E1)
- Blocking-for-impl: A1 D1/D10 订阅生命周期冲突; A2 接口所有权(阻塞80%单测); A4 BeatClock 事件schema无权威
- Pre-gate: tests/ + CI + UX/a11y 全缺 → 需 /test-setup + /ux-design 后方可 gate-check
- Report: docs/architecture/architecture-review-2026-08-11.md (PR #17, branch worktree-arch-review-2026-08-11)

## Session Extract — /architecture-review 验证 A2 消解后 2026-08-11
- Verdict: CONCERNS
- A2 本体已消解：ADR-D8 声明并归属五 DI 接口(IBeatClock/IInputEventBus/IComboCounter/IRandomSource/IDSPClock)+规范 pause/resume 源+Mock 契约；registry(architecture.yaml:297/445/574)+systems-index 依赖已登记；F2 GDD:51-53 pause/resume schema 已锁定(A4 消费侧)
- A2 未完全关闭(残留)：R1 🟠 ADR-D8 仍 Proposed→引用 Proposed ADR 故事自动阻塞→80% 单测硬门仍锁定，须 /test-setup(CombatCore EditMode asmdef + InternalsVisibleTo)+纯 C# NUnit spike → D8 Accept
- 本 session 落地(PR #21, branch worktree-arch-review-a2-verify-2026-08-11)：R3 索引刷新(traceability-index + tr-registry:826 陈旧 A2 文本→已归 D8 Proposed，保留 Gap 计数不虚增)；R2 IDSPClock 命名同步(ADR-D6 6 处 + F2 GDD 6 处 GetCurrentTime/GetCurrentDSPTime→钦定 GetDspTime，消除编译级分歧)
- 未改：历史评审快照 + D8 旧名分歧描述；URP.png(worktree LFS 占位)已排除
- 相邻未决(非本轮)：A1 D1/D10 生命周期二选一(🔴 BLOCKING)；ADR-D3 §3 补 HeavyAttack；D8 Accept 需 /test-setup
- 下一步：/test-setup → D8 Accept 解锁 C1-C4 单测硬门

## Session Extract — /review-all-gdds full 复审 2026-08-11b
- Verdict: **FAIL**（Phase 4 场景走查报 3 BLOCKER；Phase 2/3 均 CONCERNS）
- GDDs reviewed: 9（F2/F3/F4/F5/F6 + C1/C2/C3/C4）+ game-concept + entities.yaml
- Flagged for revision: tde-integration(F6), combat-director(C4), rhythm-core(C1) → systems-index 标 Needs Revision（Approved 10→7）
- Blocking issues: SG-1 冻帧×BeatClock 未定义 / SG-2 死亡语义冲突(F6原地复活 vs Roguelike局结算) / SG-3 C4令牌孤儿(依附SG-2)
- Warnings: W-1 音频恢复Resume归属 / W-2 F3↔C2依赖不对称 / W-3 索引Map缺C1→F6；DT-1 combo无sink触顶饱和 / DT-2 Beat档缺护栏对称 / DT-3 听觉双载 / DT-4 telegraph高BPM压缩；SG-4 Off折算采样 / SG-5 BPM跃升事件分叉
- Recommended next: technical-director 牵头裁定死亡语义(SG-2,SG-3前置)+C4令牌回收+冻帧×时钟合约 → 一轮 ADR/GDD 补丁 → 重跑 /review-all-gdds
- Report: design/gdd/gdd-cross-review-2026-08-11b.md（已 ff origin/main @490cce12）

## Session Extract — /architecture-review post-D8-Accept 2026-08-11
- Verdict: **CONCERNS（健康 — 0 阻塞项）**；上轮三大遗留阻塞全部清除
- 9/9 ADR 现全 Accepted（D8 收口，step-1 spike + CI 门控）→ 引用 Proposed ADR 的故事自动阻塞解除
- Traceability: 146 TR — 85 Covered (58.2%) / 15 Partial / 46 Gap（Δ +4 Covered vs 上轮 81）；4 条 Core DI 缝隙 Gap 转正
- A1 (D1/D10 生命周期，上轮🔴BLOCKING) → 真实消解：D1 订阅唯一权威，D10 让步改 handler 早退 guard，两 ADR 代码逐字一致（非双重 Accept）
- A2/A3/A4/A5 全 CLEARED；跨 ADR blocking conflicts = 0；依赖图 DAG 无环
- 引擎: 9/9 有兼容段 / 0 deprecated / 0 陈旧版本 / 0 post-cutoff 冲突；GDD revision flags 无
- 唯一 HIGH = H1（文档缺陷非阻塞）✅ 已消解 2026-08-13: ADR-D8 三处 InternalsVisibleTo("CombatCore.Tests.EditMode") 与实际跑绿代码 "EditModeTests" 不一致（:318/:379/:402），已统一到编译真值 "EditModeTests" 防 C1-C4 Mock 照抄重触 CS0122。改动 adr-d8-di-test-seams.md(:318/:321/:379/:402) + architecture.md(:111/:287/:415 note)，纯文档零 C# 触碰 → draft PR #47 (branch worktree-fix-d8-internalsvisibleto-cs0122)
- 非阻塞 cleanup: ~~H1~~ ✅ (PR #47) → D3 文本漂移(OnBeatClockPaused/HeavyAttack) → M1 FMOD Editor 编译确认 → E1 D2 同帧保证 → M2 plugins/fmod.md
- Pre-gate 全 ✅（Assets/Tests/EditMode+PlayMode / tests.yml / a11y / interaction-patterns）→ /gate-check pre-production 前置具备
- 写入(worktree arch-review-postD8-2026-08-11): architecture-review-2026-08-11-postD8.md(新) + traceability-index.md(Rev.9) + tr-registry.yaml(TR-rhythm-015 尾注转正)
- 下一步: /gate-check pre-production 或 C1 rhythm-core 实现冲刺（DI 硬门已解锁）

## Session Extract — /review-all-gdds 复审 2026-08-12
- Verdict: **CONCERNS**(3 BLOCKER 核心消解, 无存活 BLOCKER); 复审 c02bdced 补丁
- 编排: ultracode 44-agent(回归验证 SG-1/2/3→独立怀疑者证伪 + 五维扫掠 + 60 候选对抗式验证)
- SG-1/SG-2/SG-3 全 PARTIALLY_RESOLVED: ADR-D12「死亡=换新局」干净裁定跨文档互斥模型; 冻帧时基(F2/C1/C2 对称)/C4 令牌回收(SC-3 对称 C1 SC-2)闭合。唯一 BLOCKER 框候选(C1 未迁移)被对抗面板 3 票否决→WARNING
- 已做定向清理补丁(PR #29 draft, branch worktree-review-all-gdds-rerun-2026-08-12, commit dad6c485):
  - W-A C1 rhythm-core: RespawnComplete 标 MVP 休眠并传播(状态表/E10/deps/双向合约)+AC-C1-22e 改测换新局 boot+I-5 perk[1,2]→[0.5,2]
  - W-B F6 tde-integration: 原子性段+交互表对齐 Rule15(OnRespawnComplete MVP 不广播)
  - W-C ADR-D1: 示例码 _lifecycleEvents.RaisePlayerDeath()→adapter 纯 C# event OnPlayerDeath?.Invoke()+事件声明
  - W-D registry: 新增 forbidden_patterns 段(freeze_pauses_beatclock/freeze_zeroes_player_attack_speed/f6_respawncomplete_inplace_restart_mvp)
  - W-E systems-index: 补 C4→F6 依赖(枚举表+依赖图)+头部 FAIL→CONCERNS+C4 回 Approved(Approved 7→8, Needs Revision 3→2, C1/F6 保留)
- 残留 WARNING(不阻塞架构, 未纳入补丁需 owner 决策): W-F 胜利换局边界时钟 Stop 归属(→FE5, ADR-D12:138 已自认)/W-1 音频 Resume 归属/W-2 F3↔C2 依赖/SG-4 Off 采样时刻/SG-5 BPM 边界取消窗/DT-1~4; SG-1 画面定格可见机制(→PL2)
- Report: design/gdd/gdd-cross-review-2026-08-12.md (在 PR #29, 尚未 merge 到 main)
- 下一步: review/merge PR #29 → C1/F6 死亡语义清理已随补丁完成 → /gate-check pre-production 或 C1 rhythm-core 实现冲刺
- W-F 已起草承接: ADR-D13 Run-End Boundary(Proposed, commit 558b33ea, 加入 PR #29) — 统一 run-end 边界(死亡+胜利经同一 F6 出口 Stop()+OnRunEnded(outcome)), 闭合胜利 double-Play/令牌孤儿; 结算经济学/胜利判定 defer FE5+game-designer; 待 technical-director+game-designer 批准后 C1/C4/F6 小修订
