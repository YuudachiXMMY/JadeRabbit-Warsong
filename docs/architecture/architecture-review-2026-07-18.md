# Architecture Review Report (v5 — Full)

> **Date:** 2026-07-18
> **Engine:** Unity 6.3 LTS (6000.3.19f1)
> **GDDs Reviewed:** 6 (F1 game-concept, F2 audio-beat-clock, F3 input-event-system, F4 so-data-layer, F5 save-persistence, F6 tde-integration)
> **ADRs Reviewed:** 5 (D1 Accepted, D2 Accepted, D5 Accepted, D6 Accepted, D7 Accepted)
> **Mode:** Full review — all phases
> **Previous:** architecture-review-2026-07-18.md (v4/v3), architecture-review-2026-07-16.md (v2), architecture-review-2026-07-15.md (v1)

---

## Traceability Summary

| Metric | Count | % |
|--------|-------|---|
| Total Requirements | 79 | 100% |
| ✅ Covered | 28 | 35.4% |
| ⚠️ Partial | 11 | 13.9% |
| ❌ Gap | 40 | 50.6% |

**Delta from v4:** +7.6pp (27.8% → 35.4%). No new ADRs or GDD revisions.
v5 corrects v4 arithmetic errors in per-system coverage: v4 carried v3 totals
(22|8|49) without recalculating after TDE TR additions and D5/D7 coverage
remapping. Actual coverage was higher than reported.
v5 engine specialist re-consultation: 8 findings (0 HIGH, 3 MEDIUM, 5 LOW).
3 new findings vs v4; 2 findings elevated from LOW to MEDIUM.

---

## Coverage by System

| System | GDD | Total TRs | ✅ Covered | ⚠️ Partial | ❌ Gap | Primary ADR(s) |
|--------|-----|-----------|-----------|-----------|-------|----------------|
| concept | game-concept.md | 14 | 4 | 2 | 8 | D6, D5, D2 |
| beatclock | audio-beat-clock.md | 17 | 4 | 1 | 12 | D6, D1 |
| input | input-event-system.md | 15 | 4 | 1 | 10 | D1 |
| datalayer | so-data-layer.md | 12 | 1 | 2 | 9 | D1, D5, D7 |
| save | save-persistence.md | 13 | 9 | 3 | 1 | D7 |
| tde | tde-integration.md | 8 | 6 | 2 | 0 | D1, D5, D2 |
| **Total** | | **79** | **28** | **11** | **40** | |

---

## Coverage by ADR

| ADR | Status | TRs Fully Covered | TRs Partially Covered |
|-----|--------|-------------------|----------------------|
| ADR-D6 (Audio Middleware) | Accepted | TR-concept-001, TR-concept-005, TR-beatclock-002, TR-beatclock-010, TR-beatclock-011, TR-input-012 | TR-beatclock-013 |
| ADR-D7 (Save/Persistence) | Accepted | TR-save-001, TR-save-002, TR-save-003, TR-save-005, TR-save-007, TR-save-008, TR-save-010, TR-save-011, TR-save-013 | TR-save-004, TR-save-006, TR-datalayer-009 |
| ADR-D1 (TDE Integration) | Accepted | TR-input-010, TR-input-014, TR-input-015, TR-beatclock-008, TR-datalayer-012, TR-tde-001, TR-tde-002, TR-tde-003, TR-tde-006 | TR-concept-002, TR-input-004, TR-tde-007 |
| ADR-D2 (Enemy AI) | Accepted | TR-concept-009 | TR-concept-010, TR-tde-008 |
| ADR-D5 (Animation Pipeline) | Accepted | TR-concept-004, TR-tde-004, TR-tde-005 | TR-datalayer-004 |

---

## Coverage Gaps

### Foundation Layer Gaps (P2)

| TR-ID | Requirement | Suggested ADR |
|-------|-------------|---------------|
| TR-datalayer-001 through TR-datalayer-012 (9 gaps) | SO schema architecture, AssetReferenceT\<T\> loading, Odin validation, runtime cloning, Addressables integration | ADR-D4 "SO Data Layer Architecture" |

> F4 SO Data Layer has 12 TRs with only 1 Covered (D1) and 2 Partial (D5, D7). 9 gaps remain. Foundation system needed by C2, C3, C5, FE1.

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

**3. _pendingOnComplete Overwrite on Rapid Cancel** (ADR-D5, NEW in v4)
- `_pendingOnComplete` is `System.Action`. If `PlayAttack()` is called back-to-back before
  previous `OnEnd` fires (combo cancel path), the prior callback is silently overwritten.
- **Impact**: Low — cancel path should call `StopAttack()` first (sets `_pendingOnComplete = null`).
- **Resolution**: C2 GDD must specify that `AnimancerAttackPlayer.StopAttack()` is called
  before any new `PlayAttack()` on cancel. Engine Specialist identified.

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
  4. ADR-D5: Animation Pipeline (Accepted ✅) — requires D1 (TDE Animator coexistence)
  5. ADR-D2: Enemy AI Framework (Accepted ✅) — requires D1 (BeatClock lifecycle)
```

### Unresolved Dependencies

- ⚠️ ADR-D2 references C4 Combat Director for token system — C4 has no ADR yet.
  ADR-D2 can be Accepted for its core decision (Unity Behavior adoption + BeatClockBridge),
  but CombatDirectorTokenAction implementation is blocked until ADR-D8 exists.
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

### Engine Specialist Findings (v5 — Unity Specialist Consultation)

| # | ADR | Risk | Finding |
|---|-----|------|---------|
| 1 | D1 | **MEDIUM** | FMOD `RuntimeManager.IsInitialized` race in `Start()` — async DSP graph construction means `IsInitialized` may be false even after `Awake()`. Recommend promoting `WaitUntil(IsInitialized)` coroutine as canonical boot path. Add Script Execution Order entry for `TDELifecycleAdapter`. |
| 2 | D2 | LOW | `TDESetMovementAction.OnSetup()` CharacterMovement null risk on delayed spawn — mitigated by existing null guard. No ADR change needed. (v4 confirmed) |
| 3 | D5 | LOW | `_pendingOnComplete` overwrite on rapid combo cancel — C2 GDD must specify `StopAttack()` before new `PlayAttack()`. Consider defensive guard in implementation. (v4 confirmed) |
| 4 | D6 | **MEDIUM** | Unity 6.3 native plugin validation pipeline change — FMOD `.bundle`/`.dll` Plugin Inspector settings need explicit verification after import. Pre-import task required in F6 story. (v4 confirmed, elevated) |
| 5 | D6 | **MEDIUM** | **NEW:** `FMODAudioPlayerImpl` holds uninitialized `FMOD.Sound` struct — no `createSound()` call or `Init()` method defined. Calling `playSound()` with uninitialized handle produces `ERR_INVALID_HANDLE`, `getDSPClock` returns zero, beat scheduler fires continuously. F6 GDD must define explicit `Load()` contract on `IAudioPlayer`. |
| 6 | D6 | LOW | **NEW:** `FMODDSPClockImpl.GetCurrentTime()` calls `getMasterChannelGroup()` + `getSoftwareFormat()` every frame (2 P/Invoke crossings at 60fps). Cache both in init — `sampleRate` is constant, `ChannelGroup` is valid for FMOD system lifetime. |
| 7 | D2 | LOW | **NEW:** `CombatDirectorTokenAction.OnUpdate()` uses `Time.deltaTime` accumulator for beat-duration timeout — up to ±16.7ms jitter. Not a forbidden-pattern violation but inconsistent with DSP-clock precision philosophy. Flag for ADR-D8 review. |
| 8 | D7 | LOW | AES default password risk — EasySave 3 default key is well-known. Set project-specific key before F5 ships (one-line change). (Confirmed in ADR) |

**Cross-ADR specialist finding:** `AnimancerAttackPlayer` MMEventListener OnEnable/OnDisable
lifecycle confirmed safe for both pooling and destruction paths. No issue.

### HIGH Risk Findings: None

Previous v3 flagged Behavior Designer `SharedVariable<T>` serialization as HIGH risk.
This risk was **eliminated** by ADR-D2 revision (2026-07-18) — Unity Behavior Package
(com.unity.behavior v1.0.16) replaced Behavior Designer. M0 Spike confirmed 0 BLOCKERs.

---

## Architecture Document Coverage

`docs/architecture/architecture.md` does **not exist**. No architecture document coverage check possible.

**Recommendation**: Create after Core layer ADRs are complete (post-D3). Should map system
layers, data flow between subsystems, and public API boundaries.

---

## Verdict: ⚠️ CONCERNS

| Criterion | Status |
|-----------|--------|
| Foundation requirements covered | ✅ Mostly (save 69% covered, beatclock/input partial, datalayer 1/12 covered) |
| Core requirements covered | ❌ Zero accepted ADRs for Core layer |
| No blocking cross-ADR conflicts | ✅ None |
| Engine consistent | ✅ All 5 ADRs engine-verified, 0 HIGH risks |
| GDD revision needed | ✅ None |

**Why not PASS**: Core layer (C1-C6) has zero ADR coverage. F4 SO Data Layer (Foundation)
has only 1/12 TRs covered. F6 GDD incomplete.

**Why not FAIL**: Foundation layer is well-covered (D6, D7, D1 all Accepted). No blocking
conflicts. No deprecated API usage. Existing ADRs are internally consistent and engine-verified.
All 5 ADRs now Accepted.

---

## Blocking Issues (must resolve before PASS)

1. **ADR-D3 (Rhythm Quantization Core)** — P1 priority. C1 is the linchpin between
   Foundation (F2 clock + F3 input) and all Core combat systems. Without it, ~20 TRs
   from concept/beatclock/input remain ungoverned.
2. **F6 GDD Completion** — 5/8 required sections unwritten. Cannot validate F6 TRs fully.
3. **ADR-D4 (SO Data Layer)** — 12 TRs with only 1 covered. 9 gaps remain. Foundation
   system blocking Core layer stories.

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

**Projected coverage after P1+P2 ADRs**: ~60-65% (from current 35.4%)

---

## Next Steps

1. **Priority**: Write ADR-D3 (Rhythm Quantization Core) — single highest-impact ADR
2. **Then**: Write ADR-D4 (SO Data Layer Architecture)
3. **Then**: Write C1 GDD (Rhythm Quantization) to feed downstream Core ADRs
4. **Ongoing**: Complete F6 GDD remaining sections
