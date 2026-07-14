---
name: QA Gate Status — Concept GDD
description: game-concept.md has BLOCKING QA findings; system GDD authoring is gated until resolved
type: project
---

As of 2026-07-13, the approved concept GDD (design/gdd/game-concept.md) has BLOCKING QA findings
that must be resolved before any system GDD can be authored.

## Round 1 Findings (initial QA review, 2026-07-13)

1. Zero acceptance criteria and zero edge cases present (violates 8-section GDD standard)
2. MVP success criterion "players want to play again in 15-20 min" is not testable — must be rewritten as 3 concrete ACs (replay intent rate >=70%, unprompted second run >=30%, fusion perception >=60%)
3. "Wuxia feel" pillar language has no measurable proxy — needs frame-level benchmarks for hitstop, cancel windows, and hit response latency
4. Five rhythm quantization edge cases are unspecified: consecutive miss cascade, BPM transition mid-combat, perk selection timeout, latency calibration failure state, quantization at BPM extremes

## Round 2 Findings (adversarial AC testability review, 2026-07-13)

Reviewed AC-1 through AC-6 in the Acceptance Criteria section that was subsequently added.
4 new BLOCKING issues identified; 4 MAJOR issues identified.

### BLOCKING — must resolve before system GDDs begin

**B1 — AC-3(b): "明显长于 Good 命中" undefined**
- "明显" has no measurable threshold; no measurement method specified
- GDD Rule 3 already states stun_duration × 1.3 — AC must reference this
- Required fix: rewrite to "stun_duration(Perfect) ÷ stun_duration(Good) ∈ [1.287, 1.313], measured via Debug log"

**B2 — AC-3(c): subjective audio perception**
- "可听到额外编钟共鸣层" is a subjective listener test with no reproducible measurement
- Required fix: replace with objective measurement — FMOD Event Listener log or Debug API recording bell_resonance_layer event trigger

**B3 — AC-5: entire criterion is untestable**
- "Perfect 命中率（按主观感受踩拍）" is self-contradictory — subjective feel cannot produce a pass/fail rate
- "明显下降" has no threshold (by how much is "obvious"?)
- "正确的耳机时序" description is conceptually confused when artificial delay is introduced
- Required fix: complete rewrite using Input Simulator to inject fixed-timing inputs, measure rating output, compare calibrated vs uncalibrated at 100ms delay; define thresholds (calibrated ≥80% Perfect rate, uncalibrated ≤50% of calibrated rate)

**B4 — AC-缺失-A: M0 Spike has no acceptance criteria**
- GDD references "M0 Spike 待定" for quantization tier selection (Off/HalfBeat/Beat) throughout
- No AC exists defining how the Spike result is judged — currently a unilateral design call, not a data-driven decision
- Required fix: add AC-M0 with: participant count (5 internal testers × 2 runs × 3 tiers), Perfect rate target range [40%, 70%], subjective score threshold ≥3.5/5, "feels like friction" rate <40%

### MAJOR — must resolve before M2 external playtest gate

**M1 — AC-1: input control gap**
- No test tooling requirement specified for producing guaranteed Perfect/Good/Miss inputs
- combo_multiplier not controlled (changes as combo grows during test sequence)
- Required fix: require Input Simulator or Debug override; lock combo counter at fixed value; add variance check (std dev < 0.5)

**M2 — AC-6: misclassified as QA work**
- Recruiting players, observing behavior, interpreting "spontaneous" vs guided actions is UX research, not QA
- Required fix: split into AC-6a (beat indicator visual legibility — QA-executable) and AC-6b (core loop discoverability — UX/design-owner, ADVISORY gate)

**M3 — No AC covers perk system**
- Perk system is one of 3 core systems but has zero acceptance criteria in concept GDD
- Required: AC-P1 (damage-modifier perk applies correct multiplier) and AC-P2 (rhythm-rule-modifying perk changes judgment window)

**M4 — No AC covers meta-progression persistence**
- Merits are MVP-required but no AC verifies persistence across sessions or correct deduction on upgrade purchase
- Required: AC-M1 (merits survive death and reload) and AC-M2 (upgrade cost correctly deducted)

### MINOR

- AC-2: should note Input Simulator or explicit timeout method to guarantee Miss trigger (not blocking)
- AC-4: should use sample count (≥20 attacks) instead of clock time (3 min) to ensure adequate coverage

## Current Gate Status

**BLOCKED — 4 BLOCKING issues unresolved**

No system GDD may be authored until B1–B4 are resolved in game-concept.md.
M1–M4 must be resolved before M2 external playtest gate.

**Why:** Untestable concept ACs propagate to system GDDs. Systems will be implemented
against unmeasurable success criteria, making QA sign-off on any story impossible.

**How to apply:** When game-designer requests /design-system for any system,
verify all 4 BLOCKING issues in game-concept.md are resolved first.
If not resolved, block and cite B1–B4 above.
