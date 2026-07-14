---
name: Core System Test Classifications
description: Pre-classified all 5 core systems by story type and required test evidence before system GDDs are authored
type: project
---

Pre-classification established 2026-07-13 during game-concept.md QA review.

| System | Story Type | Gate Level | Test Location | Key Tests |
|---|---|---|---|---|
| Rhythm Quantization | Logic | BLOCKING | tests/unit/rhythm/ | Boundary judgment ratings, BPM transition handling, DSP offset application, half-beat window width at BPM 100 |
| Combo Graph | Logic | BLOCKING | tests/unit/combo/ | State machine transitions, cancel window boundary, invalid transition fallback, terminal node reset |
| Perk Build | Logic + Integration | BLOCKING | tests/unit/perk/ + tests/integration/perk-rhythm/ | Pool sampling (3 options), rarity distribution, rhythm-modifying perk actually changes quantization window |
| Enemy AI (Rhythmic) | Integration | BLOCKING | tests/integration/ai/ + manual playtest | Token assignment on beat slots, attack fires within 1 frame of BPM grid tick, telegraph starts N beats before attack |
| Meta-Progression | Logic | BLOCKING | tests/unit/meta/ | Merit accumulation, upgrade cost deduction, PlayerInfoSO persistence, save/load round-trip |

All systems are Logic or Integration type — all gates are BLOCKING, not advisory.

**Why:** Mis-classifying any of these as Visual/Feel or Config/Data would lower the gate to advisory and allow stories to be marked Done without automated test evidence.

**How to apply:** When reviewing system GDDs or sprint stories, verify test type classification matches this table. If a rhythm or combo story is classified as anything other than Logic, flag it.
