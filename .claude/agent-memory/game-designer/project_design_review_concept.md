---
name: Design Review — game-concept.md — First Pass (2026-07-13)
description: First design review of game-concept.md for 玉兔:破阵; verdict NEEDS REVISION with 5 blocking items
type: project
---

**Date:** 2026-07-13
**Document reviewed:** design/gdd/game-concept.md
**Verdict:** NEEDS REVISION
**Scope signal:** XL

**Blocking findings (5):**
- GD-01: Core fantasy is a rating-layer, not a unity system — needs kinetic-level Perfect hit spec
- GD-03: 30-second loop self-sufficiency claim unvalidated — no intrinsic satisfaction spec
- GD-05: Cancel window type unspecified — determines whether combo+rhythm are one or two systems
- GD-07: Quantization variant (Off/HalfBeat/Beat) unresolved — MUST be resolved before any system GDD
- GD-10: Rating-to-consequence contract undefined (Miss damage, combo break behavior, Good advance)

**Recommended findings (5):**
- GD-02: Hybrid model (movement free, attacks quantized) creates dual temporal model — needs design justification
- GD-04: Boss 4/4→3/4→5/8 meter change has no player preparation pathway
- GD-06: Rhythm-rule-modifying perks need containment spec (what is modifiable vs invariant)
- GD-08: Primary player target needs sharpening — "hardcore + rhythm + roguelike" is 3 audiences
- GD-09: Latency calibration must distinguish player output latency vs audio-visual sync latency

**Why:** First adversarial review from game-designer perspective. Prior review: none.

**How to apply:** In future sessions, check whether the 5 blocking items were resolved before helping author downstream system GDDs. If user returns to game-concept.md, cite this review to avoid retreading.
