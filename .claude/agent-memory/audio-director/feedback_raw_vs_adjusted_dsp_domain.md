---
name: feedback-raw-vs-adjusted-dsp-domain
description: Recurring defect pattern in this project — mixing raw (unadjusted) and latency-adjusted DSP timestamps across F2/F6 boundary. Check every new F2 event/API for this before approving.
metadata:
  type: feedback
---

This project has a recurring defect class: confusing F2's two DSP time coordinate
systems — **raw** (`nth_beat_dsp_time`, F2-F3/F2-F4, used for actual audio
scheduling / `PlayScheduled` anchors) vs **adjusted** (`adjusted_beat_time`,
F2-F5 = raw + `latency_offset_ms/1000`, used only for gameplay-facing judgment
and presentation timestamps consumed by C1/P1/PL2/C4).

**Instance 1 (caught during original F2 review, "B2 修订 — 第 5 次评审", see
`design/gdd/audio-beat-clock.md:207`)**: pause/resume reanchor formula almost
used the adjusted `last_beat_time` instead of the raw
`last_raw_beat_dsp_time` — would have introduced up to ±200ms error (the full
`latency_offset_ms` range), breaking the ±16.7ms AC-F2-17 gate. F2 now
explicitly maintains both `last_raw_beat_dsp_time` and
`last_adjusted_beat_dsp_time` as separate stored values to prevent this.

**Instance 2 (found in F2 focused re-review, 2026-08-08, adversarial audit of
proposed patch B1)**: the proposed `OnBpmChanged` event was specified to carry
`effectiveDspTime` as the **adjusted** (latency-shifted) timestamp, intended
for F6 to feed directly into `ScheduleBGMTransition(dspTime, bgmRef)` →
`IAudioPlayer.PlayScheduled(dspTime)`. This is wrong for the same reason as
instance 1, but in a new place: `PlayScheduled` schedules physical FMOD audio
output, which must stay in the **raw** DSP coordinate system (same domain as
the outgoing BGM segment's own anchor, `dsp_start_time` confirmed via
`ConfirmDSPStart`, never offset by `latency_offset_ms` anywhere in the F6
three-step BGM-start protocol). Using adjusted time here double-applies the
latency offset to the audio scheduling call, producing an audible seam glitch
up to ±200ms between the old and new BGM segments at every BPM transition.

**Why:** `latency_offset_ms` models the player's *subjective/output-hardware*
delay (e.g. wireless headphones) — it exists so gameplay-facing timestamp
consumers (C1 judgment windows, P1/PL2 presentation cues) match what the
player actually perceives. It is **not** a correction to apply to the
program's own internal audio scheduling calls — those already run in the
real (raw) hardware DSP clock domain, and the physical output-path delay the
offset models already happens for free at the hardware/OS/Bluetooth level.
Any F2/F6 API that takes a `dspTime` and feeds it to `PlayScheduled`,
`ConfirmDSPStart`, or an FMOD scheduling clock parameter must be raw. Any API
whose payload is consumed by C1/P1/PL2/C4/HUD for judgment or display timing
must be adjusted.

**How to apply:** Whenever reviewing a new F2 event, public method, or
struct field that carries a DSP timestamp, explicitly ask "is this fed into
an audio scheduling call (`PlayScheduled`/`ConfirmDSPStart`/FMOD clock), or
consumed by a gameplay/presentation system for judgment/display?" and verify
the field is named and documented to make the domain unambiguous (prefer
explicit suffixes like `DspTimeRaw` / `DspTimeAdjusted` over a bare
`dspTime`/`effectiveDspTime` name, which is exactly what let this recur).
Related: [[project_jade_rabbit_audio]].
