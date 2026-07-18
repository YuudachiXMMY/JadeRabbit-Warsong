# Architecture Review Report (v3 — Full)

> **Date:** 2026-07-18
> **Engine:** Unity 6.3 LTS (6000.3.19f1)
> **GDDs Reviewed:** 6 (F1 game-concept, F2 audio-beat-clock, F3 input-event-system, F4 so-data-layer, F5 save-persistence, F6 tde-integration)
> **ADRs Reviewed:** 5 (D1 Accepted, D2 Proposed, D5 Proposed, D6 Accepted, D7 Accepted)
> **Mode:** Full review — all phases
> **Previous:** architecture-review-2026-07-15.md (v1, 2 ADRs), architecture-review-2026-07-16.md (v2, 3 ADRs)

---

## Traceability Summary

| Metric | Count | % |
|--------|-------|---|
| Total Requirements | 79 | 100% |
| ✅ Covered | 22 | 27.8% |
| ⚠️ Partial | 8 | 10.1% |
| ❌ Gap | 49 | 62.0% |

**Delta from v2 (2026-07-16):** Coverage rose from 20.3% → 27.8% (+7.5pp).
New ADRs since last review: ADR-D2 (Enemy AI), ADR-D5 (Animation Pipeline).
New TRs added: 8 (F6 TDE system), 2 (input-015, datalayer-012 from D1 fix).
TR Registry now at 79 entries across 6 systems.

---

## Coverage by System

| System | GDD | Total TRs | ✅ Covered | ⚠️ Partial | ❌ Gap | Primary ADR(s) |
|--------|-----|-----------|-----------|-----------|-------|----------------|
| concept | game-concept.md | 14 | 4 | 2 | 8 | D6, D5, D1 |
| beatclock | audio-beat-clock.md | 17 | 6 | 3 | 8 | D6, D1 |
| input | input-event-system.md | 15 | 4 | 1 | 10 | D1 |
| datalayer | so-data-layer.md | 12 | 0 | 3 | 9 | — (none) |
| save | save-persistence.md | 13 | 11 | 1 | 1 | D7 |
| tde | tde-integration.md | 8 | 4 | 2 | 2 | D1, D5 |
| **Total** | | **79** | **22** | **8** | **49** | |

---

## Coverage by ADR

| ADR | Status | TRs Fully Covered | TRs Partially Covered |
|-----|--------|-------------------|----------------------|
| ADR-D6 (Audio Middleware) | Accepted | TR-concept-001, TR-concept-005, TR-beatclock-002, TR-beatclock-010, TR-beatclock-011, TR-input-012 | TR-beatclock-013 |
| ADR-D7 (Save/Persistence) | Accepted | TR-save-001 through TR-save-011, TR-save-013 | TR-save-006 |
| ADR-D1 (TDE Integration) | Accepted | TR-beatclock-008, TR-input-010, TR-input-014, TR-input-015, TR-datalayer-012, TR-tde-001, TR-tde-002, TR-tde-003, TR-tde-006 | TR-concept-002, TR-concept-009, TR-tde-007 |
| ADR-D2 (Enemy AI) | Proposed | TR-concept-009 | TR-concept-010, TR-tde-008 |
| ADR-D5 (Animation Pipeline) | Proposed | TR-concept-004, TR-tde-004, TR-tde-005 | TR-datalayer-004, TR-datalayer-007 |

---

## Coverage Gaps

### Foundation Layer Gaps (P2)

| TR-ID | Requirement | Suggested ADR |
|-------|-------------|---------------|
| TR-datalayer-001 through TR-datalayer-012 (9 gaps) | SO schema architecture, AssetReferenceT\<T\> loading, Odin validation, runtime cloning, Addressables integration | ADR-D4 "SO Data Layer Architecture" |

> F4 SO Data Layer has 12 TRs and ZERO direct ADR coverage. Foundation system needed by C2, C3, C5, FE1.

### Core Layer Gaps (CRITICAL — zero accepted ADRs)

| TR-ID | Requirement | System | Suggested ADR | Priority |
|-------|-------------|--------|---------------|----------|
| TR-concept-006 | Half-beat grid snap quantization (hybrid model) | C1 | **ADR-D3** Rhythm Quantization Core | **P1** |
| TR-concept-007 | Rating system (P/G/M) with damage multipliers (1.0/0.75/0.5) | C1 | ADR-D3 | **P1** |
| TR-beatclock-003 | Beat prediction and lookahead for upcoming beat events | C1 | ADR-D3 | **P1** |
| TR-beatclock-004 | Drift correction algorithm (anchored DSP recalculation) | C1 | ADR-D3 | **P1** |
| TR-beatclock-005 | Subdivision handling (half-beat, quarter-beat grid levels) | C1 | ADR-D3 | **P1** |
| TR-input-001 | Input timestamp → beat-relative offset calculation | C1 | ADR-D3 | **P1** |
| TR-input-002 | Tolerance window configuration (±ms per rating tier) | C1 | ADR-D3 | **P1** |
| TR-concept-008 | Perfect kinetics effects (freeze frame, hitstun ×1.3, camera micro-cut) | C6 | ADR: Hitbox/Hit Response | P3 |
| TR-concept-010+ | Combo graph, cancel windows, combo counter | C2 | ADR-D9: Combo Graph System | P3 |
| TR-concept-011+ | Enemy beat-lock enforcement, attack token scheduling | C4 | ADR-D8: Combat Director | P2 |

### Feature Layer Gaps (P3-P4)

| System | Suggested ADR | Priority |
|--------|---------------|----------|
| FE3 Latency Calibration | ADR: Latency Calibration Architecture | P3 |
| PL1 Camera System | ADR: Camera & Cinemachine Integration | P4 |

---

## Cross-ADR Conflicts

### Blocking Conflicts: 0

### Minor Inconsistencies (non-blocking)

**1. BPM Speed Clamp Range** (ADR-D5 vs F6 GDD)
- F6 GDD F6-FORMULA-01 defines: `SPEED_MIN=0.5, SPEED_MAX=2.5`
- ADR-D5 implements `state.Speed = currentBpm / authoredBpm` without explicit clamp in the ADR text
- **Impact**: None — ADR-D5 implements F6-FORMULA-01 which defines the clamp. Story must include enforcement.
- **Resolution**: Implementation story for D5 must explicitly enforce clamp.

**2. UpdateBpm() / IsPlaying Same-Frame Edge Case** (ADR-D5)
- `AnimancerState.IsPlaying` returns false for a state at `OnEnd` but not yet removed
- If `UpdateBpm()` fires same-frame as `OnEnd`, speed update is silently dropped
- **Impact**: Post-MVP minor — attack is already finishing, no gameplay consequence
- **Resolution**: Add to ADR-D5 risks section. Engine Specialist confirmed.

---

## ADR Dependency Order

### Recommended Implementation Order (topologically sorted)

```
Foundation (no dependencies):
  1. ADR-D6: Audio Middleware (Accepted ✅)
  2. ADR-D7: Save/Persistence Middleware (Accepted ✅)

Integration (depends on Foundation):
  3. ADR-D1: TDE Integration (Accepted ✅) — requires D6 (FMOD init check)

Feature (depends on Integration):
  4. ADR-D5: Animation Pipeline (Proposed) — requires D1 (TDE Animator coexistence)
  5. ADR-D2: Enemy AI Framework (Proposed) — requires D1 (BeatClock lifecycle)
```

### Unresolved Dependencies

- ⚠️ ADR-D2 references C4 Combat Director for token system — C4 has no ADR yet.
  ADR-D2 can be Accepted for its core decision (BD adoption + BeatClockBridge), but
  CombatDirectorTokenTask implementation is blocked until ADR-D8 exists.
- ⚠️ ADR-D5 references F4 SO data layer (`authoredBpm` field on AttackNodeSO) — F4 has no ADR.
  Non-blocking: the field addition is self-contained and doesn't depend on F4's broader architecture.

### Dependency Cycles: None detected.

---

## GDD Revision Flags

**None — all GDD assumptions are consistent with verified engine behaviour.**

No GDD makes assumptions contradicted by accepted ADRs or engine reference docs.

**Note**: F6 GDD (`design/gdd/tde-integration.md`) is INCOMPLETE — 5/8 required sections
say "[To be designed]". This is an authoring gap, not a revision flag. Systems index
should be updated from "Not Started" to "In Design" once acknowledged.

---

## Engine Compatibility Issues

### Engine Audit Results

| Metric | Value |
|--------|-------|
| Engine | Unity 6.3 LTS (6000.3.19f1) |
| ADRs with Engine Compatibility section | 5/5 |
| Deprecated API references | 0 |
| Stale version references | 0 |
| Post-cutoff API conflicts | 0 |

### Engine Specialist Findings (Unity Specialist Consultation)

| ADR | Specialist Verdict | Engine Issue |
|-----|-------------------|--------------|
| D1 | **Confirmed Accepted** | `Start()` FMOD check is correct — MonoBehaviour lifecycle order unchanged in 6.3 |
| D2 | **⚠️ Block Accept** | BD `SharedVariable<T>` serialization MUST be inspected: Unity 6.3 `[SerializeField] fields-only` change silently ignores `[SerializeField]` on properties. If BD uses property-backed serialization, Inspector-assigned defaults (e.g. `TimeoutBeats`) become zero at runtime. **M0 Spike must specifically inspect SharedVariable source.** |
| D5 | **Confirmed Proposed** | Add `UpdateBpm()`/`IsPlaying` same-frame edge case to risks. Confirm PlayableGraph `SetTimeUpdateMode()` during PlayMode integration test. |
| D6 | **Confirmed Accepted** | FMOD native plugin importers worth verifying platform config in Plugin Inspector (LOW risk) |
| D7 | **Confirmed Accepted** | No Unity 6.3 issues |

### HIGH Risk Finding (Verdict-Changing)

**ADR-D2: Behavior Designer SharedVariable Serialization**

Unity 6.3 LTS `[SerializeField] fields-only` breaking change:
- `[SerializeField]` on **properties** is now silently ignored (no compile error, no warning)
- If BD's `SharedVariable<T>` base class uses property-backed serialization internally,
  all Inspector-assigned default values revert to type-default (0/null/false) at runtime
- **Specific risk**: `CombatDirectorTokenTask.TimeoutBeats` — if its default was Inspector-assigned
  and backed by a property, it becomes 0, making every enemy attack time out immediately

**Required M0 Spike action**: Open `SharedVariable.cs` and `SharedFloat.cs` in BD source →
look for `[SerializeField]` decorating a property (not a field). If found = breaking bug.

---

## Architecture Document Coverage

`docs/architecture/architecture.md` does **not exist**. No architecture document coverage check possible.

**Recommendation**: Create after Core layer ADRs are complete (post-D3). Should map system
layers, data flow between subsystems, and public API boundaries.

---

## Verdict: ⚠️ CONCERNS

| Criterion | Status |
|-----------|--------|
| Foundation requirements covered | ✅ Mostly (save 100%, beatclock/input partial, datalayer 0%) |
| Core requirements covered | ❌ Zero accepted ADRs for Core layer |
| No blocking cross-ADR conflicts | ✅ None |
| Engine consistent | ⚠️ D2 has unverified HIGH risk (BD serialization) |
| GDD revision needed | ✅ None |

**Why not PASS**: Core layer (C1-C6) has zero ADR coverage. F4 SO Data Layer (Foundation)
also has zero coverage. ADR-D2 has unverified engine risk. F6 GDD incomplete.

**Why not FAIL**: Foundation layer is well-covered (D6, D7, D1 all Accepted). No blocking
conflicts. No deprecated API usage. Existing ADRs are internally consistent and engine-verified.

---

## Blocking Issues (must resolve before PASS)

1. **ADR-D3 (Rhythm Quantization Core)** — P1 priority. C1 is the linchpin between
   Foundation (F2 clock + F3 input) and all Core combat systems. Without it, ~20 TRs
   from concept/beatclock/input remain ungoverned.
2. **ADR-D2 BD M0 Spike** — Must inspect `SharedVariable.cs` source for `[SerializeField]`
   property vs. field pattern before Accept. Silent data loss risk.
3. **F6 GDD Completion** — 5/8 required sections unwritten. Cannot validate F6 TRs fully.
4. **ADR-D4 (SO Data Layer)** — 12 TRs with zero coverage. Foundation system blocking
   Core layer stories.

---

## Required ADRs (prioritized)

| Priority | ADR | System | Layer | Unlocks | Estimated TRs Covered |
|----------|-----|--------|-------|---------|----------------------|
| **P1** | ADR-D3: Rhythm Quantization Core | C1 | Core | C2, C3, C4, C5, C6, all Feature/Presentation | ~20 |
| **P2** | ADR-D4: SO Data Layer Architecture | F4 | Foundation | C2, C3, C5, FE1 | ~9 |
| **P2** | ADR-D8: Combat Director | C4 | Core | FE1 (enemy AI token), FE4 (Boss) | ~5 |
| **P3** | ADR-D9: Combo Graph System | C2 | Core | C5 (weapons), C6 (hitbox) | ~5 |
| **P3** | ADR: Latency Calibration | FE3 | Feature | P6 (calibration UI) | ~3 |
| **P4** | ADR: Hitbox/Hit Response | C6 | Core | PL2 (VFX feedback) | ~4 |
| **P4** | ADR: Camera Integration | PL1 | Feature | — | ~2 |

**Projected coverage after P1+P2 ADRs**: ~55-60% (from current 27.8%)

---

## Next Steps

1. **Immediate**: Complete F6 GDD (Edge Cases, Dependencies, Tuning Knobs, Acceptance Criteria)
2. **Immediate**: Run BD M0 Spike — import Behavior Designer → inspect `SharedVariable.cs`
   for `[SerializeField]` usage → verify compile in Unity 6.3 LTS
3. **Priority**: Write ADR-D3 (Rhythm Quantization Core) — single highest-impact ADR
4. **Then**: Write ADR-D4 (SO Data Layer Architecture)
5. **Accept**: ADR-D5 after PlayMode validation; ADR-D2 after BD M0 Spike passes
