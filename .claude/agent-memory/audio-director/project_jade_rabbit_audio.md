---
name: Project — 玉兔:破阵 Audio Context
description: Core audio facts for Jade Rabbit Warsong — rhythm-action roguelike where music IS the combat system
type: project
---

BGM is explicitly the combat system, not background music. Every attack quantizes to half-beat grid. All enemy attacks forced to beat. Boss phase 2 changes time signature: 4/4 → 3/4 → 5/8.

Audio pillars: Chinese traditional percussion (编钟, 木鱼, 丝弦). Judgment audio tiered (Perfect/Good/Miss). Latency calibration required before first play.

Status as of 2026-08-08:
- ADR-D6 resolved: FMOD Studio 2.x selected as audio middleware (was open as of 2026-07-13, now Accepted 2026-07-14).
- F2 (audio-beat-clock.md, Approved) defines the DSP beat clock; F6 (tde-integration.md) defines TDE/Animancer/FMOD integration and owns BGM transition strategy (§BGM 过渡策略, seamless measure-boundary crossfade, no pitch-shifting per pillar 3 编钟/华风 constraint).
- Cross-system BPM-change BGM transition is still an open seam: F6 references `BeatClock.ScheduleBGMTransition(dspTime, bgmRef)` as a method F2 must add (Post-Approval Action Item, not yet in F2 GDD). See [[feedback_raw_vs_adjusted_dsp_domain]] for the specific defect pattern found here in the F2 focused re-review (2026-08-08).
- Boss phase time signature sequence: 4/4 → 3/4 → 5/8 (Post-MVP, ADVISORY gating only in F2 AC-F2-21/22).
- Mobile audio pipeline risk is now partially specified: F2 Edge Case (:412) covers `AudioSettings.dspTime` jump/full-reanchor on iOS/Android session interruption, but does NOT specify what happens to the actual FMOD BGM channel content during that interruption — still an open gap as of 2026-08-08.
- Latency calibration (FE3) now has a defined sign convention and formula (F2-F5): positive offset = sound arrives late at player's ear (e.g. wireless headphones), `adjusted = raw + offset/1000`.

**Why:** Audio is Pillar 1 (Rhythm IS Combat). Any gap in audio spec directly blocks system GDD authoring.
**How to apply:** Audio design decisions are blocking dependencies for the rhythm-core system GDD. Flag any new audio gaps immediately. Before citing FMOD-vs-Unity-Audio as open, verify against `docs/architecture/adr-d6-audio-middleware.md` — it was resolved 2026-07-14.
