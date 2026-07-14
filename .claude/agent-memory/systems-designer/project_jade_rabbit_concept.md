---
name: Jade Rabbit Warsong — Concept Review
description: Systems design findings from concept GDD review; 5 blocking decisions unresolved; 8 system GDDs mapped with dependency order
type: project
---

Game: 玉兔:破阵 (Jade Rabbit: Warsong)
Genre: Rhythm Action Roguelike
Concept GDD: design/gdd/game-concept.md
Status on 2026-07-13: Approved concept doc; no formulas written yet; registry empty.

## 8 Required System GDDs (dependency order)

1. Rhythm Quantization (ROOT — must be first)
2. Damage (depends on Rhythm Quantization)
3. Combo (bidirectional dependency with Damage)
4. BPM Progression (depends on Rhythm Quantization)
5. Enemy Beat Allocation (depends on BPM Progression + Rhythm Quantization)
6. Perk System (depends on Combo + Damage)
7. Meta-Progression / Merit (depends on Damage)
8. Boss Phase Design (depends on Enemy Beat Allocation + BPM Progression + Rhythm Quantization 5/8 extension)

## 5 Blocking Design Decisions (unresolved as of 2026-07-13)

1. Perfect/Good window width — fixed-ms or BPM-proportional?
2. Miss = 0 damage or reduced damage?
3. Combo decay trigger — Miss? damage received? timer? death only?
4. Dodge-cancel behavior — cancels pending snap, or both resolve independently?
5. BPM cap — maximum floor BPM value

**Why:** These 5 decisions cannot be derived from the concept doc. They must be resolved by the game-designer before any system GDD can complete its Formulas section.

**How to apply:** At the start of every Rhythm Quantization or Damage GDD authoring session, check whether these decisions have been recorded. Do not author Formulas sections without them.

## Key Formula Risks Identified

- BPM-proportional vs fixed-ms windows: non-monotonic difficulty curve risk if fixed
- Combo unbounded without a cap: exponential damage explosion with multiplicative perks
- Miss = 0 damage: creates a hard cliff that the combo tuner must know about
- 5/8 time signature: half-beat grid has no clean pairing on beat 5 — needs explicit snap rule
- Dodge-cancel creates combo ambiguity: does pending un-executed attack get a Miss rating?
