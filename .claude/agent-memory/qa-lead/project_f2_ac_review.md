---
name: project-f2-ac-review
description: F2 (Audio/Beat Clock) adversarial AC review — first documented round, covering 2 proposed blocking gate additions (OnBpmChanged, Paused/Resumed events, F5-init gate)
metadata:
  type: project
---

Round 1 (2026-08-08): Adversarial testability review of 3 proposed new F2/F6 event contracts intended to close 2 blocking gaps (F2 GDD `design/gdd/audio-beat-clock.md`, current AC set AC-F2-01~23, matrix at :669-688).

**Proposed additions reviewed**:
- B1: `OnBpmChanged` event at `PendingBPMChange`→`Playing` measure-boundary transition, payload `{newBpm, oldBpm, effectiveDspTime, newBeatIntervalS}`.
- B2a: `OnBeatClockPaused`/`OnBeatClockResumed` paired events; Resumed carries re-anchored `nextBeatDspTime`.
- B2b: F6 `TDELifecycleAdapter` gate ensuring F5 `Initialize()` completes and injects `LatencyOffsetMs` before `BeatClock.Play()` (closes risk W-4-01: F2 reading offset=0 before F5 ready).

**Critical finding (blocks B2b from being written as a valid AC)**: B2b's F6-owned push/gate model directly contradicts F2 GDD's existing Dependencies section (`audio-beat-clock.md` "运行时软依赖"), which states F2 itself pulls `LatencyOffsetMs` via `SaveService.LoadLatencyCalibration()` in its own `Start()`, relying on Unity's Awake-before-Start ordering guarantee. Two competing ownership models (F2 self-pull vs. F6 push-gate) cannot both be true in the GDD. Root cause suspicion: F5's `Initialize()` may be async (ES3 file I/O via coroutine/Task), which would invalidate the Awake/Start ordering assumption — this is why the gate is being proposed. Must resolve ownership model in GDD text before drafting a final AC; until then B2b's AC should be marked DRAFT.

**Other testability gaps found**:
- No `ISaveService`/`IFileStorage` interface abstraction defined anywhere in F2/F5/F6 GDDs for mocking F5 in tests — blocks writing a deterministic AC for the gate. [[project_f5_ac_review]] already tracks F5 GDD as 5 CRITICAL pending rewrite; confirm current status before finalizing.
- No timeout/fallback policy defined for F5 `Initialize()` hanging — could hang the whole game with no documented recovery. Flagged as open question, not yet an AC.
- No symmetric `OnTimeSignatureChanged` event proposed alongside `OnBpmChanged` — asymmetry may be intentional (poll-based via `BeatStateSnapshot`) but is undocumented as a decision.
- AC-F2-17's ±16.7ms tolerance applies only to the frame-quantized `beatProgress` UI value, NOT to DSP timestamp values (which use <10ms in PlayMode or <0.1ms in EditMode/MockDSPClock per Core Rule 12). Don't conflate the two when gating new DSP-timestamp payloads (e.g. B2a's `nextBeatDspTime`).

**Recommended new ACs** (drafted, not yet written to GDD — pending user approval):
- AC-F2-24 (OnBpmChanged, EditMode+MockDSPClock, reuses AC-F2-13's Given scenario)
- AC-F2-25a/b/c (Paused/Resumed pair + idempotency + pairing invariant, EditMode+MockDSPClock)
- AC-F6-23 (recommended home for the F5-init gate AC, not AC-F2-26 — this is TDELifecycleAdapter behavior, matches existing AC-F6-08~11 pattern for lifecycle atomicity; needs MockAudioPlayer + Mock F5 interface, runnable headless per AC-F2-07b's CI strategy since it never touches real AudioSettings.dspTime)

**Why**: User requested adversarial testability audit before these 2 blocking items get written into the GDD as real ACs — goal is to catch spec ambiguity now rather than after implementation starts.
**How to apply**: Before F2/F6 GDD is next revised to add these ACs, resolve the LatencyOffsetMs ownership conflict and the F5 mock-interface gap first; otherwise the new AC text will assert against an interface that doesn't exist yet.
