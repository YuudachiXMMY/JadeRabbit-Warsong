---
name: Project — 玉兔:破阵 Audio Context
description: Core audio facts for Jade Rabbit Warsong — rhythm-action roguelike where music IS the combat system
type: project
---

BGM is explicitly the combat system, not background music. Every attack quantizes to half-beat grid. All enemy attacks forced to beat. Boss phase 2 changes time signature: 4/4 → 3/4 → 5/8.

Audio pillars: Chinese traditional percussion (编钟, 木鱼, 丝弦). Judgment audio tiered (Perfect/Good/Miss). Latency calibration required before first play.

Open decisions as of 2026-07-13:
- FMOD vs Unity Audio DSP (must decide before M2)
- Whether music is pre-composed or procedurally generated (NOT SPECIFIED in GDD)
- How adaptive audio transitions between BPM values (NOT SPECIFIED)
- What happens to beat grid during time signature changes (NOT SPECIFIED)
- Fallback for latency calibration failure (NOT SPECIFIED)
- Audio budget for 50+ perks (NOT SPECIFIED)
- Mobile audio pipeline (NOT SPECIFIED — iOS/Android latency variance)

**Why:** Audio is Pillar 1 (Rhythm IS Combat). Any gap in audio spec directly blocks system GDD authoring.
**How to apply:** Audio design decisions are blocking dependencies for the rhythm-core system GDD. Flag any new audio gaps immediately.
