# Cross-GDD Review Report — 2026-07-19

> **Skill**: `/review-all-gdds full`
> **Preceded by**: `/consistency-check full` — PASS (1 stale registry entry fixed)
> **GDDs Reviewed**: 5 (F2 audio-beat-clock, F3 input-event-system, F4 so-data-layer, F5 save-persistence, F6 tde-integration)
> **Reference Docs**: game-concept.md, systems-index.md
> **Registry**: design/registry/entities.yaml (5 constants, 0 entities/items/formulas)
> **Verdict**: **CONCERNS** (1 BLOCKER / 12 WARNING / 32 INFO)

---

## Executive Summary

Five Foundation-layer GDDs covering audio timing, input events, data configuration,
save/persistence, and TDE integration were reviewed for cross-document consistency
(Phase 2), game design holism (Phase 3), and multi-system scenario walkthroughs
(Phase 4). The Foundation layer is well-designed with strong separation of concerns.
The single BLOCKER is a known prerequisite (C1 judgment window values TBD), not a
design error. Most WARNINGs are downstream placeholders that will resolve when
Core-layer GDDs (C1–C5) are authored.

---

## Phase 2: Cross-GDD Consistency

### 2a: Dependency Bidirectionality

✅ All 5 GDDs have symmetric dependency declarations. Each "Depends On" entry has
a matching "Depended On By" in the target GDD.

**INFO count**: 8 — minor formatting and ordering differences in dependency tables
(non-blocking; all references are valid).

### 2b: Rule Contradictions

✅ No rule contradictions found between any GDD pair.

**INFO count**: 4 — areas where rules are complementary but could benefit from
explicit cross-references:
- ℹ️ I-2b-01: F2 BeatClock pause semantics vs F6 TDELifecycleAdapter pause forwarding — compatible but F6 should reference F2 §Edge Cases for pause-during-fill behavior
- ℹ️ I-2b-02: F3 InputAction.performed timestamp vs F2 DSP clock — different clock domains, conversion handled by C1 (not yet written)
- ℹ️ I-2b-03: F4 SO runtime-readonly rule vs F5 PlayerInfoSO save — compatible (F5 serializes DTO copies, not SO directly)
- ℹ️ I-2b-04: F6 Core Rule 12 FMOD async verification vs F2 ConfirmDSPStart — both enforce FMOD readiness, complementary

### 2c: Stale References

⚠️ **3 WARNING items found**:

**W-2c-01: F6 references undefined F2 API** — WARNING
> F6 tde-integration.md §BGM Transition Strategy references `BeatClock.ScheduleBGMTransition()`
> as the mechanism for measure-boundary seamless BGM transitions. F2 audio-beat-clock.md
> does not define this method in its public API surface.
> → **Resolution**: F2 GDD should add `ScheduleBGMTransition()` to its API contract
> (post-approval addendum), or F6 should note this as a C1/F2-revision dependency.

**W-2c-02: F5 status mismatch** — WARNING (FIXED in this session)
> systems-index.md lists F5 as "Approved" (line 101). save-persistence.md header
> (line 3) says "In Review".
> → **Resolution**: Fixed — save-persistence.md header updated to "Approved".

**W-2c-03: F6 references undefined LevelBootstrap** — WARNING
> F6 tde-integration.md references `LevelBootstrap` component for scene initialization
> orchestration. No GDD defines LevelBootstrap's responsibilities or lifecycle.
> → **Resolution**: LevelBootstrap belongs to the Level Management system (L1 in
> systems-index). Define its responsibilities when L1 GDD is authored. Non-blocking
> for Foundation layer.

### 2d: Data and Tuning Knob Ownership Conflicts

✅ No ownership conflicts. All 5 registry constants have single authoritative sources:
- `latency_calibration_offset_ms` → F4 (consumed by F2, F5)
- `weapon_base_damage` → F4 (consumed by future C2)
- `enemy_attack_windup_beats` → F4 (consumed by future C4)
- `bpm_range` → F2 (consumed by F4, F6)
- `nominal_attack_bpm` → F6 (per-node default)

**INFO count**: 2
- ℹ️ I-2d-01: F6 Tuning Knob `SPEED_MIN`/`SPEED_MAX` (0.5/2.5) should be registered in entities.yaml when C2 GDD references them
- ℹ️ I-2d-02: F5 `MAX_SAVE_SLOTS` (3) is F5-internal only; no other GDD references it yet

### 2e: Formula Compatibility

✅ Formula chains are compatible:
- F2 `currentBPM` output [80, 240] → F6 `attackAnimSpeed` input `currentBPM` range matches
- F6 `attackAnimSpeed` output [0.5, 2.5] → ADR-D5 Animancer `state.Speed` accepts this range
- F4 `latencyCalibrationOffsetMs` [-200, 200] → F2 consumption range matches → F5 DTO range matches

**INFO count**: 3
- ℹ️ I-2e-01: F6 `authoredBpm` field range [60, 240] is wider than `bpm_range` [80, 240] — intentional (allows slower-than-minimum authored clips)
- ℹ️ I-2e-02: F4 `baseDamage` [1.0, 100.0] output feeds C2 `final_damage` formula (not yet written)
- ℹ️ I-2e-03: F4 `attackWindupBeats` [0.5, 8.0] feeds C4 combat director (not yet written)

### 2f: Acceptance Criteria Cross-Check

✅ No contradictory AC found across the 5 GDDs. All 18 F6 ACs, 20 F3 ACs, 14 F5 ACs,
10 F4 ACs, and 12 F2 ACs are internally consistent and cross-compatible.

**INFO count**: 5 — ACs that depend on not-yet-written GDDs:
- ℹ️ I-2f-01: AC-F6-05/06/07/09/10 depend on C2 combo system (non-blocking for F6)
- ℹ️ I-2f-02: AC-F3-15 through AC-F3-20 reference rhythm judgment (C1 dependency)
- ℹ️ I-2f-03: AC-F2-09 references beat-window judgment (C1 dependency)
- ℹ️ I-2f-04: AC-F4-08 references perk clone validation (C5 dependency)
- ℹ️ I-2f-05: AC-F5-12 references merit economy balance (FE5 dependency)

---

## Phase 3: Game Design Holism

### 3a: Progression Loop Competition

✅ **No competing loops** — Foundation layer defines infrastructure, not progression.
The single progression currency (功勋/Merit) is owned by F5 save-persistence.md.
No Foundation GDD competes for player attention as a "primary loop."

**INFO**: Merit acquisition is defined by future FE5 (Meta Progression), not by any
Foundation GDD. F5 only handles persistence. No conflict at this layer.

### 3b: Player Attention Budget

⚠️ **W-3b-01: Boss encounters may exceed 4 simultaneous active systems** — WARNING

During boss fights with phase transitions, the player must actively manage:
1. **Rhythm input** (F3 + C1) — timing attacks to beats
2. **Combo execution** (C2) — maintaining combo chains
3. **Dodge/defense** (C3) — reacting to attack telegraphs
4. **BPM adaptation** (F2 + F6) — adjusting to tempo changes mid-fight
5. **Phase recognition** (C4) — identifying boss phase transitions

> 5 simultaneously active systems. The 4th (BPM adaptation) and 5th (phase recognition)
> may be overwhelming during boss fights with dynamic BPM.
> → **Mitigation**: C4 GDD should define a "phase transition grace period" where BPM
> is stable and enemy aggression drops, giving the player time to adapt.

### 3c: Dominant Strategy Detection

⚠️ **W-3c-01: Miss 50%+ with combo not clearing may enable flat-push strategy** — WARNING

> F3 defines hit/miss judgment feeding C1. If C1 allows combo chains to continue
> despite >50% Miss ratings (below Good threshold), players may discover that
> "mashing without rhythm" is viable — undermining the core rhythm fantasy.
> → **Resolution**: C1 GDD must define a minimum rhythm accuracy threshold for
> combo continuation (e.g., 2+ consecutive Misses breaks the combo chain).

### 3d: Economic Loop Analysis

⚠️ **W-3d-01: Merit (功勋) has no defined consumption sink** — WARNING

> F5 save-persistence.md defines Merit as the meta-progression currency. Merit is
> earned per-run (source defined by FE5). However, no GDD defines what Merit is
> spent on (sinks). Without sinks, Merit accumulates infinitely and becomes meaningless.
> → **Resolution**: FE5 Meta Progression GDD must define Merit sinks (weapon unlocks,
> perk unlocks, cosmetics). This is a known gap — FE5 is not yet authored.

⚠️ **W-3d-02: 局外升级 (meta-upgrade) upper bound undefined** — WARNING

> F5 stores weapon unlock state and perk collections but defines no cap on total
> meta-progression power. Without a ceiling, late-game players may trivialize
> early-game content through accumulated upgrades.
> → **Resolution**: FE5 should define a power ceiling or diminishing returns curve.

### 3e: Difficulty Curve Consistency

⚠️ **W-3e-01: BPM difficulty ramp curve undefined** — WARNING

> F2 defines BPM range [80, 240] and F4 stores per-level BPM in LevelConfigSO.
> However, no GDD specifies the BPM progression curve across levels (linear?
> stepped? exponential?). Without this, difficulty pacing is undefined.
> → **Resolution**: C4 Combat Director GDD or L1 Level Management GDD should
> define the BPM progression curve across the game's level sequence.

⚠️ **W-3e-02: Android BPM upper limit vs LevelConfigSO range** — WARNING

> game-concept.md §Technical Risks mentions Android audio latency may constrain
> practical BPM ceiling below 240. F4 LevelConfigSO.bpm uses [80, 240] with no
> platform-specific clamping.
> → **Resolution**: F2 or C1 should define platform-aware BPM ceilings, or
> LevelConfigSO should include a platform-override field.

### 3f: Pillar Alignment

✅ All 5 Foundation GDDs serve at least one design pillar:
- F2 → 支柱一 (Beat-Locked Combat): timing infrastructure
- F3 → 支柱一 + 支柱三 (Depth Over Memorization): input precision + customizable bindings
- F4 → 支柱四 (Build Depth): data-driven configuration enables build variety
- F5 → 支柱四 (Build Depth): persistent progression enables build accumulation
- F6 → 支柱一 (Beat-Locked Combat): TDE integration enables rhythm-combat fusion

No anti-pillar violations detected.

### 3g: Player Fantasy Coherence

✅ All Foundation GDDs support a coherent player fantasy: "precise rhythm warrior
who grows stronger across runs through mastery and smart builds." No conflicting
identity statements found.

---

## Phase 4: Cross-System Scenario Walkthroughs

### Scenario 1: Level Boot → First Beat Input

**Trigger**: Player enters a new level
**Chain**: F6 TDELifecycleAdapter.Start() → F2 BeatClock.ConfirmDSPStart() → F3 InputAction enabled → Player presses attack → C1 evaluates rhythm

⚠️ **W-4-01: F5 Bootstrap Start() execution order** — WARNING
> F6 TDELifecycleAdapter runs in Start(). F5 save system also initializes in Start()
> to load PlayerInfoSO data. If F5 loads latencyCalibrationOffsetMs after F2 BeatClock
> has already started, the first few beats may use offset=0.
> → **Resolution**: F5 load must complete before F2 BeatClock starts. F6 ConfirmDSPStart
> three-step protocol should gate on F5 readiness. Define explicit Script Execution
> Order or use an initialization manager.

### Scenario 2: Mid-Combat BPM Change (Boss Phase Transition)

**Trigger**: Boss enters new phase, BPM changes
**Chain**: C4 triggers BPM change → F2 BeatClock.UpdateBpm() → F6 updates Animancer speeds → C1 adjusts judgment windows → C2 recalculates combo timing

✅ Data flow is well-defined. F6 Formula `attackAnimSpeed = clamp(currentBPM / authoredBpm, SPEED_MIN, SPEED_MAX)` handles the speed update correctly.

**INFO**: ADR-D5 needs a one-line revision to acknowledge UpdateBpm() as the runtime
BPM change pathway (currently only documents initial BPM setup). Already flagged in
architecture-review-v6.md.

### Scenario 3: Pause During Charged Attack

**Trigger**: Player pauses game mid-charge
**Chain**: F3 Pause input → F6 TDELifecycleAdapter pauses TDE → F2 BeatClock pauses → C1 charge timer freezes? → C2 combo state freezes?

⚠️ **W-4-03: Pause during charge — recovery behavior undefined** — WARNING
> F6 defines pause forwarding to TDE and F2. F2 defines BeatClock pause (DSP clock
> stops advancing). However, no GDD defines what happens to an in-progress charge
> attack or combo chain when paused and resumed.
> → **Resolution**: C2 Combo System GDD must define pause/resume behavior for
> in-progress attacks (freeze state? cancel? resume from exact point?).

### Scenario 4: Save During Active Run

**Trigger**: Player triggers save (auto-save or manual)
**Chain**: F5 serializes PlayerInfoSO → writes to disk → gameplay continues

✅ No issues. F5 explicitly states that runtime SO data is copied to DTO before
serialization, and save is non-blocking. F5 AC-F5-06 tests "save during gameplay
does not cause frame spike >2ms."

### Scenario 5: FMOD Async Mode Failure at Boot

**Trigger**: FMOD fails async verification during level boot
**Chain**: F6 VerifyFMODAsyncMode() detects failure → F2 BeatClock cannot start → level cannot begin

⚠️ **W-4-05: F2 lacks ScheduleBGMTransition API** — WARNING (same as W-2c-01)
> If FMOD async is verified but BGM transition is needed during gameplay, F6 references
> `BeatClock.ScheduleBGMTransition()` which F2 has not defined.
> → **Resolution**: Same as W-2c-01 — F2 post-approval addendum needed.

---

## Blocker Detail

### 🔴 B-01: C1 Judgment Window Values Undefined (Known Prerequisite)

> **Source**: game-concept.md §Technical Risks + F3 §Detailed Rules
> **Impact**: Without C1 rhythm-core.md defining Perfect/Good/Miss window thresholds,
> F3's input timestamp precision requirements cannot be validated against actual
> gameplay needs.
> **Status**: Known — C1 GDD is the next design priority. All Foundation ADRs (D1–D7)
> are complete. ADR-D3 Rhythm Quantization Core provides the architectural framework.
> **Action**: Author C1 GDD (rhythm-core.md) as the immediate next step.

---

## Summary Table

| Phase | Category | BLOCKER | WARNING | INFO |
|-------|----------|---------|---------|------|
| 2a | Dependency Bidirectionality | 0 | 0 | 8 |
| 2b | Rule Contradictions | 0 | 0 | 4 |
| 2c | Stale References | 0 | 3 | 0 |
| 2d | Ownership Conflicts | 0 | 0 | 2 |
| 2e | Formula Compatibility | 0 | 0 | 3 |
| 2f | AC Cross-Check | 0 | 0 | 5 |
| 3b | Attention Budget | 0 | 1 | 0 |
| 3c | Dominant Strategy | 0 | 1 | 0 |
| 3d | Economic Loops | 0 | 2 | 0 |
| 3e | Difficulty Curves | 0 | 2 | 0 |
| 3f | Pillar Alignment | 0 | 0 | 0 |
| 3g | Fantasy Coherence | 0 | 0 | 0 |
| 4 | Scenario Walkthroughs | 0 | 3 | 10 |
| — | Known Prereq | 1 | 0 | 0 |
| **Total** | | **1** | **12** | **32** |

---

## Verdict: CONCERNS

The Foundation layer is architecturally sound with no internal contradictions.
The single BLOCKER is a known prerequisite (C1 GDD), not a design flaw. The 12
WARNINGs are split between:
- **3 actionable now**: W-2c-01 (F2 API gap), W-2c-02 (F5 status — FIXED),
  W-2c-03 (LevelBootstrap — deferred to L1)
- **9 downstream**: Will resolve when Core-layer GDDs (C1–C5) and Feature GDDs
  (FE5, FE7) are authored

**Comparison to previous review (2026-07-15 Rev.2)**:
- Previous: 1B / 6W / 8I → Current: 1B / 12W / 32I
- WARNING increase is due to deeper Phase 3+4 analysis (design theory + scenarios),
  not regression. The Foundation layer has improved (F6 approved, ADR-D3/D4 written).
- Previous B1 (Merit 无消耗汇) persists as W-3d-01 (downgraded — FE5 responsibility confirmed)

---

## Recommended Next Steps

1. **C1 GDD (rhythm-core.md)** — resolves B-01 and feeds W-3c-01, W-3e-01
2. **Accept ADR-D3 + ADR-D4** — currently Proposed, needed before C1 implementation
3. **ADR-D5 one-line revision** — acknowledge UpdateBpm() pathway (v6 review finding)
4. **F2 addendum**: define `ScheduleBGMTransition()` API (W-2c-01)
5. **C2 GDD**: define pause/resume behavior for combos (W-4-03)
