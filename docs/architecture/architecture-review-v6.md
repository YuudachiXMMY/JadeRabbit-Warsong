# Architecture Review v6 — Jade Rabbit: Warsong

> **Date**: 2026-07-19
> **Reviewer**: Claude Code Architecture Agent (Opus 4.6)
> **Scope**: 6 GDDs (F1-F6) + 7 ADRs (D1-D7) + TR Registry (79 entries)
> **Previous Review**: v5 — 28 covered (35.4%), 11 partial (13.9%), 40 gap (50.6%)

---

## Phase 2: TR Registry Cross-Check

### GDD Requirements Not Yet in Registry

After cross-referencing all 6 GDDs against the 79 registry entries, the following requirements appear in GDD text but lack corresponding TR-IDs:

| # | GDD Source | Requirement (Missing) | Suggested TR-ID | Priority |
|---|---|---|---|---|
| 1 | F6 `tde-integration.md` Rule 9 | Root Motion must be disabled on Character Prefab Animator | TR-tde-009 | LOW — implementation detail |
| 2 | F6 `tde-integration.md` Rule 10 | Subscriber lifecycle (OnEnable/OnDisable idempotency for DontDestroyOnLoad) | TR-tde-010 | LOW — already covered implicitly by TR-beatclock-012 pattern |
| 3 | F6 `tde-integration.md` Rule 11 | ConfirmDSPStart 3-step protocol with 500ms advance | TR-tde-011 | MEDIUM — cross-system constraint |
| 4 | F6 `tde-integration.md` BGM Transition | BeatClock.ScheduleBGMTransition() API (Post-Approval action item) | TR-tde-012 | LOW — MVP Boss-only |
| 5 | F4 `so-data-layer.md` Rule 6 | Resources.Load() forbidden for F4 SOs (Addressables-only) | — | COVERED by ADR-D4 forbidden pattern `resources_load_for_so` |
| 6 | game-concept.md | BPM ladder scaling per dungeon depth (Boss 4/4 -> 3/4 -> 5/8 phase transitions) | — | COVERED by TR-beatclock-006, TR-beatclock-014 |

**Assessment**: The registry is substantially complete for the 6 written GDDs. Items 1-4 are low-to-medium priority implementation details from F6 that could be appended but are not blocking. No critical gaps found.

### Stale/Deprecated Entries

None identified. All 79 entries remain `status: active` and correspond to current GDD text.

---

## Phase 3: Traceability Matrix

### Legend
- ✅ Covered — ADR explicitly addresses this requirement
- ⚠️ Partial — ADR partially covers or addresses a related aspect
- ❌ Gap — No ADR addresses this requirement

### Concept System (TR-concept-001 through TR-concept-014)

| TR-ID | Requirement Summary | Coverage | ADR(s) |
|-------|---------------------|----------|--------|
| TR-concept-001 | DSP clock precision < 16.7ms | ⚠️ Partial | ADR-D1 (lifecycle coordination) |
| TR-concept-002 | Multi-platform input support | ❌ Gap | — |
| TR-concept-003 | 60fps / 16.6ms frame budget | ❌ Gap | — |
| TR-concept-004 | Animancer BPM-adaptive animation | ✅ Covered | ADR-D5 |
| TR-concept-005 | FMOD as audio middleware | ✅ Covered | ADR-D6 |
| TR-concept-006 | Half-beat grid snap quantization | ✅ Covered | ADR-D3 |
| TR-concept-007 | Rating system (P/G/M) with multipliers | ✅ Covered | ADR-D3 |
| TR-concept-008 | Perfect kinetics effects | ⚠️ Partial | ADR-D3 (rating output defined; freeze/hitstun/camera effects deferred to PL2/C6 GDDs) |
| TR-concept-009 | Enemy beat-lock via CombatDirector | ✅ Covered | ADR-D2 |
| TR-concept-010 | Perk design space containment | ⚠️ Partial | ADR-D3 (mode constraints defined; C3 GDD pending) |
| TR-concept-011 | Delay calibration system | ❌ Gap | — (FE3 GDD not written) |
| TR-concept-012 | ScriptableObject data-driven design | ✅ Covered | ADR-D4 |
| TR-concept-013 | Roguelike per-run perk cloning | ⚠️ Partial | ADR-D4 (clone rules defined; C3 GDD pending) |
| TR-concept-014 | Multiple weapon types with ComboGraph | ❌ Gap | — (C5 GDD not written) |

**Concept Summary**: 5 covered, 4 partial, 5 gap

### BeatClock System (TR-beatclock-001 through TR-beatclock-017)

| TR-ID | Requirement Summary | Coverage | ADR(s) |
|-------|---------------------|----------|--------|
| TR-beatclock-001 | MonoBehaviour singleton on DontDestroyOnLoad | ❌ Gap | — |
| TR-beatclock-002 | DSP clock < 0.1ms accuracy | ✅ Covered | ADR-D6 |
| TR-beatclock-003 | Full grid broadcast (beat + half-beat) | ❌ Gap | — |
| TR-beatclock-004 | Dual interface (event + snapshot) | ❌ Gap | — |
| TR-beatclock-005 | Lookahead events (100ms default) | ❌ Gap | — |
| TR-beatclock-006 | BPM/time-sig change deferred to measure boundary | ❌ Gap | — |
| TR-beatclock-007 | Player latency offset internalized | ❌ Gap | — |
| TR-beatclock-008 | Pause/resume with DSP reanchor | ❌ Gap | — |
| TR-beatclock-009 | BPM=0 fallback + audio unavailable degradation | ❌ Gap | — |
| TR-beatclock-010 | Thread safety via ConcurrentQueue | ⚠️ Partial | ADR-D6 (main-thread RuntimeManager.Update() approach) |
| TR-beatclock-011 | IDSPClock and IAudioPlayer DI | ⚠️ Partial | ADR-D6 (defines FMODDSPClockImpl/FMODAudioPlayerImpl) |
| TR-beatclock-012 | Subscriber lifecycle (mandatory unsubscribe) | ❌ Gap | — |
| TR-beatclock-013 | F6 3-step DSP start protocol | ⚠️ Partial | ADR-D6 (FMOD precision enables the protocol) |
| TR-beatclock-014 | 5/8 time signature support (Post-MVP) | ❌ Gap | — |
| TR-beatclock-015 | BeatStateSnapshot readonly struct | ❌ Gap | — |
| TR-beatclock-016 | BeatEventData readonly struct (zero GC) | ❌ Gap | — |
| TR-beatclock-017 | Mobile suspend/resume DSP reanchor | ❌ Gap | — |

**BeatClock Summary**: 1 covered, 3 partial, 13 gap

### Input System (TR-input-001 through TR-input-015)

| TR-ID | Requirement Summary | Coverage | ADR(s) |
|-------|---------------------|----------|--------|
| TR-input-001 | Unity Input System sole interface | ❌ Gap | — |
| TR-input-002 | Dual-read (DSP + ctx.time) | ❌ Gap | — |
| TR-input-003 | InputEventBus singleton on DontDestroyOnLoad | ❌ Gap | — |
| TR-input-004 | Two Action Maps (Gameplay + UI) | ❌ Gap | — |
| TR-input-005 | AttackInputEvent with chargeStartDspTime | ❌ Gap | — |
| TR-input-006 | Zero GC (readonly struct + method groups) | ❌ Gap | — |
| TR-input-007 | Multi-device concurrent support | ❌ Gap | — |
| TR-input-008 | Runtime key rebinding with JSON | ❌ Gap | — |
| TR-input-009 | Subscriber lifecycle (unsubscribe on OnDestroy) | ❌ Gap | — |
| TR-input-010 | TDE Action Map conflict resolution | ✅ Covered | ADR-D1 |
| TR-input-011 | ChargedAttack Hold interaction state machine | ❌ Gap | — |
| TR-input-012 | IDSPClock shared interface with F2 | ❌ Gap | — |
| TR-input-013 | Platform-specific timing constraints | ❌ Gap | — |
| TR-input-014 | AllDisabled initial state; F6 authorizes input | ✅ Covered | ADR-D1 |
| TR-input-015 | FMOD async init mode enforcement | ❌ Gap | — |

**Input Summary**: 2 covered, 0 partial, 13 gap

### Data Layer (TR-datalayer-001 through TR-datalayer-012)

| TR-ID | Requirement Summary | Coverage | ADR(s) |
|-------|---------------------|----------|--------|
| TR-datalayer-001 | 7 core SO types | ✅ Covered | ADR-D4 |
| TR-datalayer-002 | Read-only asset principle | ✅ Covered | ADR-D4 |
| TR-datalayer-003 | PerkSO runtime cloning | ✅ Covered | ADR-D4 |
| TR-datalayer-004 | Schema change propagation contract | ✅ Covered | ADR-D4 |
| TR-datalayer-005 | Odin dual validation (A + B) | ✅ Covered | ADR-D4 |
| TR-datalayer-006 | AssetReferenceT<T> lazy loading | ✅ Covered | ADR-D4 |
| TR-datalayer-007 | PerkParameter [Serializable] struct | ✅ Covered | ADR-D4 |
| TR-datalayer-008 | ComboEdge index validation | ✅ Covered | ADR-D4 |
| TR-datalayer-009 | PlayerInfoSO <-> F5 DTO field mapping | ⚠️ Partial | ADR-D7 (partial — defines DTO pattern; field mapping in F4 GDD) |
| TR-datalayer-010 | [NonSerialized] field init timing | ✅ Covered | ADR-D4 |
| TR-datalayer-011 | SO id uniqueness enforcement | ✅ Covered | ADR-D4 |
| TR-datalayer-012 | LevelConfigSO BPM injection to F2 via F6 | ✅ Covered | ADR-D4 |

**Data Layer Summary**: 11 covered, 1 partial, 0 gap

### Save/Persistence (TR-save-001 through TR-save-013)

| TR-ID | Requirement Summary | Coverage | ADR(s) |
|-------|---------------------|----------|--------|
| TR-save-001 | SaveService singleton on DontDestroyOnLoad | ❌ Gap | — |
| TR-save-002 | SlotData vs GlobalData domain separation | ❌ Gap | — |
| TR-save-003 | 3-slot system | ❌ Gap | — |
| TR-save-004 | Mixed save trigger (auto + immediate) | ❌ Gap | — |
| TR-save-005 | DTO pattern (SO -> DTO -> disk) | ✅ Covered | ADR-D7 |
| TR-save-006 | Main thread constraint | ❌ Gap | — |
| TR-save-007 | Per-DTO save_version with chain migration | ❌ Gap | — |
| TR-save-008 | IFileStorage interface abstraction | ✅ Covered | ADR-D7 |
| TR-save-009 | Domain Reload static reset | ❌ Gap | — |
| TR-save-010 | Awake/Initialize separation for test injection | ❌ Gap | — |
| TR-save-011 | OnSaveError event contract | ❌ Gap | — |
| TR-save-012 | Two-level error model (slot vs system) | ❌ Gap | — |
| TR-save-013 | Corrupted file backup (.corrupt rename) | ❌ Gap | — |

**Save Summary**: 2 covered, 0 partial, 11 gap

### TDE Integration (TR-tde-001 through TR-tde-008)

| TR-ID | Requirement Summary | Coverage | ADR(s) |
|-------|---------------------|----------|--------|
| TR-tde-001 | Two-component architecture | ✅ Covered | ADR-D1 |
| TR-tde-002 | TDE source MUST NOT be modified | ✅ Covered | ADR-D1 |
| TR-tde-003 | Boot sequence (Scene -> LevelConfig -> BeatClock -> Input) | ✅ Covered | ADR-D1 |
| TR-tde-004 | Animancer attack layer via HybridAnimancerComponent | ✅ Covered | ADR-D1, ADR-D5 |
| TR-tde-005 | Attack animation speed scaled by BPM | ✅ Covered | ADR-D5 |
| TR-tde-006 | TDELifecycleAdapter exclusive BeatClock caller | ✅ Covered | ADR-D1 |
| TR-tde-007 | Pause/Resume atomic coordination | ✅ Covered | ADR-D1 |
| TR-tde-008 | Enemy AI uses Unity Behavior (no coexistence with TDE AIBrain) | ✅ Covered | ADR-D2 |

**TDE Summary**: 8 covered, 0 partial, 0 gap

---

### Traceability Summary

| System | Entries | Covered | Partial | Gap |
|--------|---------|---------|---------|-----|
| concept | 14 | 5 | 4 | 5 |
| beatclock | 17 | 1 | 3 | 13 |
| input | 15 | 2 | 0 | 13 |
| datalayer | 12 | 11 | 1 | 0 |
| save | 13 | 2 | 0 | 11 |
| tde | 8 | 8 | 0 | 0 |
| **TOTAL** | **79** | **29 (36.7%)** | **8 (10.1%)** | **42 (53.2%)** |

**Delta from v5**: +1 covered (29 vs 28), -3 partial (8 vs 11), +2 gap (42 vs 40). The increase in gap count reflects ADR-D4's conversion of 3 former "partial" items to "covered" while the registry grew with new TDE entries.

---

## Phase 4: Cross-ADR Conflict Detection

### Conflict Analysis

| # | ADR Pair | Conflict Type | Finding |
|---|----------|---------------|---------|
| 1 | D5 vs F6 GDD | Interface contract tension | F6 GDD selects Option A (mid-attack BPM speed update via `UpdateBpm()`), but ADR-D5 states "MVP does not update speed mid-attack". F6 GDD acknowledges this and flags it as a pending ADR-D5 revision. **MEDIUM risk — requires D5 revision to align.** |
| 2 | D3 vs D5 | Dependency conflict | ADR-D3 lists "ADR-D5 (Accepted)" in depends-on but also states `AnimancerAttackPlayer.Play(attackType, targetDspTime)` — ADR-D5 actual API is `PlayAttack(attackNode, loadedClip, onComplete)`. **LOW risk — parameter mismatch is descriptive, not normative. C1 GDD will reconcile.** |
| 3 | D4 vs D7 | Shared resource boundary | Both ADRs define how PlayerInfoSO data flows to F5. ADR-D4 adds the `easysave_so_boundary` interface contract which explicitly delineates the DTO boundary. **No conflict — complementary.** |

### Data Ownership Conflicts

None. `architecture.yaml` state_ownership entries have no duplicate owners:
- `action_map_state` → f3-input-event-bus-only
- `beat_clock_lifecycle` → f6-tde-lifecycle-adapter-only (write), f2-beat-clock (read)
- `rhythm_judgment_combo` → c1-rhythm-judger-only

### Performance Budget Conflicts

Only 1 budget registered (beat-clock: 0.2ms). No conflicts possible with a single entry. **Note**: Remaining systems (combat, AI, rendering) have no registered budgets yet. This is expected — budgets are registered when ADRs claim them.

### ADR Dependency Order (Topological Sort)

```
Tier 0 (Foundation — no dependencies):
  ADR-D6 (Audio Middleware)        [Accepted]
  ADR-D7 (Save Persistence)       [Accepted]
  ADR-D1 (TDE Integration)        [Accepted]

Tier 1 (depends on Tier 0):
  ADR-D5 (Animation Pipeline)     [Accepted]  → depends on D1
  ADR-D2 (Enemy AI Framework)     [Accepted]  → depends on D1

Tier 2 (depends on Tier 0 + Tier 1):
  ADR-D3 (Rhythm Quantization)    [PROPOSED]  → depends on D1, D5, D6
  ADR-D4 (SO Data Layer)          [PROPOSED]  → depends on D1, D5, D6
```

**Implementation order** (must respect Accepted-first constraint):
1. ADR-D6 (Foundation)
2. ADR-D7 (Foundation)
3. ADR-D1 (Foundation)
4. ADR-D5 (depends on D1)
5. ADR-D2 (depends on D1)
6. ADR-D3 (depends on D1, D5, D6) — **BLOCKED until Accepted**
7. ADR-D4 (depends on D1, D5, D6) — **BLOCKED until Accepted**

---

## Phase 5: Engine Compatibility

### Version Consistency

| ADR | Engine Version | Status |
|-----|---------------|--------|
| D1 | Unity 6.3 LTS (6000.3.19f1) | ✅ Consistent |
| D2 | Unity 6.3 LTS (6000.3.19f1) | ✅ Consistent |
| D3 | Unity 6.3 LTS (6000.3.19f1) | ✅ Consistent |
| D4 | Unity 6.3 LTS (6000.3.19f1) | ✅ Consistent |
| D5 | Unity 6.3 LTS (6000.3.19f1) | ✅ Consistent |
| D6 | Unity 6.3 LTS (6000.3.19f1) | ✅ Consistent |
| D7 | Unity 6.3 LTS (6000.3.19f1) | ✅ Consistent |

All 7 ADRs reference the same engine version. No inconsistencies.

### Post-Cutoff API Usage

| ADR | Post-Cutoff APIs | Verification Status |
|-----|-----------------|---------------------|
| D1 | MMEventListener<T>, InputSystemManagerEventsBased | ✅ Verified (M0 Spike) |
| D2 | BehaviorGraphAgent.SetVariableValue<T>() | ✅ Verified (M0 Spike) |
| D3 | None | N/A |
| D4 | AssetReferenceT<T>, [NonSerialized] on SO | ✅ Verified via engine-reference |
| D5 | HybridAnimancerComponent, AnimancerState.Speed | ✅ Verified (PlayMode) |
| D6 | FMOD getDSPClock(), Channel.setDelay() | ✅ Verified (M0 Spike source check) |
| D7 | None | N/A |

### Deprecated API Usage

None detected. All ADRs explicitly avoid:
- Legacy Input Manager (replaced by Input System)
- Resources.Load() (replaced by Addressables)
- AnimatorOverrideController for BPM adaptation (replaced by Animancer state speed)

### Unity 6.3 Breaking Change Compliance

| Breaking Change | Impact | ADR Coverage |
|----------------|--------|--------------|
| `[SerializeField]` fields-only | Affects all SO class definitions | ADR-D4 explicitly flags this in Verification Required |
| URP Compatibility Mode removed | No ADR references legacy rendering mode | N/A — not applicable |
| HTTP/2 default | No ADR uses networking | N/A |
| Netcode 2.x | Project is single-player | N/A |
| Input System default | ADR-D1 + F3 GDD enforce Input System only | ✅ Aligned |

### Missing Engine Compatibility Sections

None. All 7 ADRs contain the required Engine Compatibility section with fields for Engine, Knowledge Risk, References Consulted, Post-Cutoff APIs, and Verification Required.

---

## Phase 5b: GDD Design Revision Flags

| # | GDD | Assumption | Engine Reality | Severity |
|---|-----|-----------|----------------|----------|
| 1 | F2 (audio-beat-clock) | Mentions `AudioSettings.dspTime` and `AudioSource.PlayScheduled()` as fallback/alternate paths | ADR-D6 definitively selects FMOD; these Unity Audio APIs are explicitly rejected. F2 GDD still references them in edge case / degradation text. | LOW — descriptive context, not normative. F2 core rules use IDSPClock abstraction correctly. |
| 2 | F6 (tde-integration) | UpdateBpm() mid-attack (Option A) | ADR-D5 says MVP does not update mid-attack | MEDIUM — requires ADR-D5 revision or F6 GDD deferral to post-MVP |
| 3 | F4 (so-data-layer) | `bgmFilePath: string` for FMOD Core API | ADR-D6 confirms this approach (StreamingAssets relative path) | ✅ No conflict |

No GDD makes assumptions that fundamentally conflict with verified engine reality. The F6/D5 UpdateBpm tension is an interface versioning issue, not an engine compatibility failure.

---

## Phase 6: Architecture Document Coverage

### `docs/architecture/architecture.md`

**Status: ABSENT** — The file does not exist. This is expected at this project stage (Pre-Production). The architecture registry (`docs/registry/architecture.yaml`) serves as the current authoritative source for cross-ADR contracts.

### Systems Index to ADR Mapping

| System (from systems-index) | ADR Coverage | Notes |
|---|---|---|
| F2 Audio/Beat Clock | ADR-D6 (middleware only) | Needs dedicated BeatClock ADR for internal architecture (F2 GDD is the authority) |
| F3 Input Event System | ADR-D1 (partial — TDE bridge only) | Needs dedicated Input ADR or relies on GDD as spec |
| F4 SO Data Layer | ADR-D4 (Proposed) | Full coverage |
| F5 Save/Persistence | ADR-D7 (middleware only) | Needs dedicated Save arch ADR or relies on GDD as spec |
| F6 TDE Integration | ADR-D1 + D5 | Full coverage |
| C1 Rhythm Quantization | ADR-D3 (Proposed) | Full coverage |
| FE1 Enemy AI | ADR-D2 | Full coverage (framework selection) |

---

## Phase 7: Review Verdict

### Coverage Gaps Analysis — Blocking vs Acceptable

**BLOCKING Gaps (Foundation/Core layer, no GDD-pending excuse):**

The 13 beatclock gaps and 13 input gaps are NOT truly unaddressed — they are covered by **approved GDDs** (F2 and F3) which serve as the normative specification. These GDDs have `Status: Approved` and are implementation-ready. The "gap" in ADR coverage means no ADR was written for their internal architecture (BeatClock internals, InputEventBus internals), because the GDD *is* the architecture spec for these systems.

**True architectural gaps (no ADR AND no GDD):**
- None at Foundation layer
- C1 Rhythm Core: ADR-D3 exists (Proposed) — awaiting acceptance
- Save internals: ADR-D7 covers middleware; F5 GDD covers architecture

**Expected gaps (GDDs not yet written):**
- TR-concept-002 (multi-platform input) — Feature-layer concern, addressed during implementation
- TR-concept-003 (frame budget) — Project-wide concern, not single-ADR scope
- TR-concept-011 (delay calibration) — FE3 GDD not written yet
- TR-concept-014 (weapon types) — C5 GDD not written yet
- TR-save-001 through TR-save-013 (11 gaps) — F5 GDD is Approved and serves as spec; ADR-D7 covers middleware choice

### Suggested New ADRs

| Priority | Suggested ADR | Addresses | Blocking? |
|----------|--------------|-----------|-----------|
| HIGH | Accept ADR-D3 (Rhythm Quantization Core) | TR-concept-006/007/008/010 + C1/C2 GDD unblocking | YES — blocks C1, C2 implementation |
| HIGH | Accept ADR-D4 (SO Data Layer) | TR-datalayer-001 through 012 | YES — blocks C2, C3, C5, FE1 implementation |
| LOW | ADR for F2 BeatClock Internals (optional) | TR-beatclock-001 through 017 | NO — F2 GDD (Approved) is sufficient |
| LOW | ADR for F3 Input Internals (optional) | TR-input-001 through 015 | NO — F3 GDD (Approved) is sufficient |

### Cross-ADR Conflicts

| # | Severity | Description | Action Required |
|---|----------|-------------|-----------------|
| 1 | MEDIUM | D5 vs F6 GDD: UpdateBpm() mid-attack divergence | Revise ADR-D5 to acknowledge UpdateBpm() in MVP path (or F6 GDD defers to post-MVP) |
| 2 | LOW | D3 API reference mismatch (descriptive) | Reconcile in C1 GDD authoring — no ADR change needed |

### ADR Status Flags

| ADR | Status | Action Needed |
|-----|--------|---------------|
| D1 | Accepted | None |
| D2 | Accepted | None |
| D3 | **Proposed** | Move to Accepted — blocks C1/C2 GDD and implementation sprints |
| D4 | **Proposed** | Move to Accepted — blocks C2/C3/C5/FE1 GDD and implementation sprints |
| D5 | Accepted | Minor revision needed (UpdateBpm() MVP path acknowledgment) |
| D6 | Accepted | None |
| D7 | Accepted | None |

### Engine Compatibility Issues

None. All 7 ADRs are engine-version-consistent, use verified post-cutoff APIs, and avoid deprecated patterns.

---

## Final Verdict: CONCERNS

**Rationale:**

1. **ADR-D3 and ADR-D4 remain Proposed** — These are the only two ADRs that block downstream GDD authoring (C1, C2, C3, C5, FE1). Until they move to Accepted, no Core-layer implementation can begin. This is the primary architectural blocker.

2. **D5/F6 UpdateBpm() tension** — A minor interface version mismatch that requires a one-line ADR-D5 revision to resolve.

3. **Traceability coverage at 36.7% covered** is expected and acceptable given that only 6 of 31 systems have GDDs written. The Foundation+TDE layers are well-covered (19/20 entries covered by ADRs); the remaining gaps are in systems where the Approved GDD serves as the normative spec (F2, F3, F5).

**The architecture is sound. No fundamental conflicts, no engine incompatibilities, no data ownership races.** The verdict is CONCERNS (not FAIL) solely because of the Proposed ADR status blocking downstream work.

### Recommended Actions (Priority Order)

1. **Accept ADR-D3** (Rhythm Quantization Core) — unblocks C1 GDD authoring
2. **Accept ADR-D4** (SO Data Layer) — unblocks C2, C3, C5, FE1 GDD authoring
3. **Revise ADR-D5** — add one sentence acknowledging `UpdateBpm()` is callable in MVP per F6 GDD
4. **Begin Core-layer GDD authoring** — C1 (rhythm-core) is the next critical path item

---

## Appendix: ADR Implementation Dependency Graph

```
                    ┌─────────┐
                    │  ADR-D6 │ (Audio Middleware — Accepted)
                    │  ADR-D7 │ (Save Persistence — Accepted)
                    │  ADR-D1 │ (TDE Integration — Accepted)
                    └────┬────┘
                         │
              ┌──────────┼──────────┐
              ▼                     ▼
        ┌─────────┐          ┌─────────┐
        │  ADR-D5 │          │  ADR-D2 │
        │(Animation│          │(Enemy AI│
        │ Accepted)│          │Accepted)│
        └────┬────┘          └─────────┘
             │
    ┌────────┼────────┐
    ▼                 ▼
┌─────────┐    ┌─────────┐
│  ADR-D3 │    │  ADR-D4 │
│(Rhythm  │    │(SO Data │
│PROPOSED)│    │PROPOSED)│
└─────────┘    └─────────┘
     │              │
     ▼              ▼
  C1 GDD        C2/C3/C5/FE1 GDDs
```

---

*End of Architecture Review v6*
