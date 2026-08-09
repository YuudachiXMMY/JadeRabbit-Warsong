# ADR-D6: Audio Middleware — FMOD Studio 2.x

## Status
Accepted

## Date
2026-07-14

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (6000.3.19f1) |
| **Domain** | Audio |
| **Knowledge Risk** | MEDIUM — Post-LLM-cutoff; audio APIs verified against engine-reference docs and unity-specialist review |
| **References Consulted** | `docs/engine-reference/unity/modules/audio.md`, `docs/engine-reference/unity/breaking-changes.md`, `docs/engine-reference/unity/deprecated-apis.md` |
| **Post-Cutoff APIs Used** | `FMOD.ChannelGroup.getDSPClock()` (via master channel group), `FMOD.Channel.setDelay()` — FMOD for Unity 2.02.35; M0 Spike 源码验证 ✅ |
| **Verification Required** | 1. Confirm FMOD for Unity package explicitly lists Unity 6000.3.x as tested version before import. 2. Verify FMOD native plugin binaries built against Unity 6.3 LTS plugin API headers (HIGH-RISK — Unity 6.3 changed native plugin validation pipeline). 3. Confirm `coreSystem.getSoftwareFormat()` sample rate on all target platforms during M0 Spike. |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | None |
| **Enables** | F2 BeatClock implementation (immediate), F6 BGM Management GDD (BGM play protocol must reference FMOD core API) |
| **Blocks** | F2 implementation start — this ADR moving to Accepted unblocks F2 |
| **Ordering Note** | F6 GDD must be authored after this ADR — F6's BGM play protocol references FMOD-specific APIs defined here (`FMODAudioPlayerImpl`, `scheduledStartDSPClock / fmodSampleRate`) |

## Context

### Problem Statement
BeatClock (F2) requires sub-millisecond DSP timestamp precision for beat event scheduling. Unity's native `AudioSettings.dspTime` has buffer-boundary quantization on Android (1024-sample buffers ≈ 23ms steps at 44100 Hz), which exceeds the 5ms threshold in F2's ConfirmDSPStart protocol and generates spurious step-3 correction warnings on Android. The audio middleware choice determines: (a) DSP clock precision, (b) BGM scheduling API, (c) thread model for beat event dispatch, and (d) the foundation for adaptive music in the content phase.

### Constraints
- F2 timestamp precision: < 0.1ms deviation from theoretical beat time over 500 consecutive beats
- F2 thread safety: all C# beat events must dispatch on Unity's main thread
- FMOD licensing: free under USD $200k annual revenue (project is pre-launch indie — free tier applies)
- Platform support: Windows / macOS (PC), iOS, Android, Console (PS5 / Xbox / Switch — NDA-gated console FMOD SDKs required separately from console manufacturer portals)
- Build pipeline: FMOD native libraries must be included per-platform (iOS `.framework`, Android `.so`, PC `.dll`)
- FMOD for Unity is NOT a UPM package — imported as an asset and version-pinned manually outside `Packages/manifest.json`

### Requirements
- Must provide DSP time with sample-level precision (≈ 0.02ms @ 48kHz)
- Must support BGM scheduled playback at a precise absolute DSP clock value
- Must implement `IDSPClock` and `IAudioPlayer` interfaces defined in F2 GDD without modifying `BeatClock.cs`
- Must be main-thread safe for C# event dispatch
- Must not conflict with TopDownEngine or Feel/MMFeedbacks audio subsystems

## Decision

BeatClock (F2) will use **FMOD Studio 2.x** as the audio middleware, replacing Unity's native `AudioSource` / `AudioSettings.dspTime` APIs. The mathematical beat scheduling logic (BPM → beat interval → DSP event dispatch) remains in C# unchanged per F2 GDD. FMOD provides the production implementations of the two interfaces defined in F2.

**Integration Surface**: **FMODUnity RuntimeManager package** (official Firelight Technologies integration for Unity). Do NOT use raw FMOD C# bindings (`fmod.cs` / `fmodstudio.cs` directly). RuntimeManager already calls `FMOD.Studio.System.Update()` each frame via its own hook — **BeatClock must NOT call `Update()` again** (double-update causes command queue corruption).

**BGM Asset Pipeline**: BGM is played via **FMOD Core API** (`FMOD.Sound` + `FMOD.Channel.setDelay()`) for sample-accurate DSP clock scheduling. `FMOD.Studio.EventInstance` is used for SFX and ambient audio only. BGM is **NOT** authored as a FMOD Studio bank event — the FMOD Studio authoring tool is not required for MVP.

**Forbidden**: `FMOD.Channel.setPosition(FMOD.TIMEUNIT.DSP)` as a scheduling mechanism (deprecated; use `Channel.setDelay()` with absolute system DSP clock). `FMOD.Studio.EventInstance.getTimelinePosition()` must not be used as a precision clock (buffer-quantization, not sample-level).

### Architecture Diagram

```
[F6 BGM Manager]
   │ FMODDSPClockImpl.GetCurrentTime() → schedule target = now + 0.5s
   │ FMODAudioPlayerImpl.PlayScheduled(targetDspSeconds)
   ▼
[FMODUnity.RuntimeManager]
   │  — calls FMOD.Studio.System.Update() each frame (main thread, auto)
   │  — exposes CoreSystem: FMOD.System
   ▼
[FMOD Core: FMOD.System] ─────────────────── FMOD Audio Thread (internal)
   │ coreSystem.getMasterChannelGroup() → master.getDSPClock() → ulong samples
   │ coreSystem.playSound(sound, paused=true) → Channel
   │ channel.setDelay(startDSPClock, 0, false)  ← absolute SYSTEM DSP clock
   │ channel.setPaused(false)                   ← arm for scheduled start
   ▼
[BeatClock : MonoBehaviour]
  Update() {
    // RuntimeManager.Update() already called before this; FMOD state is fresh
    dspNow = idspClock.GetCurrentTime();        // FMODDSPClockImpl → sample-level
    if (dspNow >= nextBeatDspTime) → DispatchBeatEvent();  // main thread, no queue
  }
   │ C# events (main thread safe)
   ▼
[C1 Rhythm Core] [C4 Combat Director] [P1 HUD] [FE3 Calibration]
```

### Key Interfaces

```csharp
// IDSPClock — FMOD production implementation
// Reads FMOD core DSP clock; converts ulong samples → double seconds
// NOTE: getDSPClock is on IChannelControl (Channel/ChannelGroup), NOT on FMOD.System
// Must read from master channel group — its dspclock IS the system DSP clock
public class FMODDSPClockImpl : IDSPClock {
    public double GetCurrentTime() {
        // getDSPClock lives on ChannelGroup, not System — use master channel group
        FMODUnity.RuntimeManager.CoreSystem.getMasterChannelGroup(out FMOD.ChannelGroup masterGroup);
        masterGroup.getDSPClock(out ulong dspClock, out ulong parentClock);
        // NOTE: use FMOD sample rate, NOT AudioSettings.outputSampleRate (they can differ)
        FMODUnity.RuntimeManager.CoreSystem.getSoftwareFormat(out int sampleRate, out _, out _);
        return (double)dspClock / sampleRate;
    }
}

// IAudioPlayer — FMOD production implementation
// Uses FMOD core API for sample-accurate scheduled BGM playback
public class FMODAudioPlayerImpl : IAudioPlayer {
    private FMOD.Channel _channel;
    private FMOD.Sound   _sound;   // pre-loaded with FMOD_CREATESAMPLE (NOT streaming)

    public void PlayScheduled(double dspTimeSeconds) {
        FMODUnity.RuntimeManager.CoreSystem.getSoftwareFormat(out int sampleRate, out _, out _);
        ulong startDSPClock = (ulong)(dspTimeSeconds * sampleRate);

        FMODUnity.RuntimeManager.CoreSystem.playSound(_sound, default, true, out _channel);
        // setDelay uses absolute SYSTEM DSP clock (not channel DSP clock)
        _channel.setDelay(startDSPClock, 0, false);
        _channel.setPaused(false);  // arm — audio begins at startDSPClock
    }

    public void Stop()               => _channel.stop();
    public bool IsPlaying            { get { _channel.isPlaying(out bool p); return p; } }
}

// actual_start (replaces Unity-Native formula in F6 BGM protocol):
// actual_start_seconds = scheduledStartDSPClock / (double)fmodSampleRate
// FMOD DSP clock at playback start is sample-accurate; no buffer-boundary quantization.
```

## Alternatives Considered

### Alternative 1: Unity Native Audio + Update Polling
- **Description**: `AudioSettings.dspTime` as DSP clock; Update loop polls for beat dispatch; `AudioSource.PlayScheduled()` for BGM.
- **Pros**: Zero additional dependency; simpler build pipeline; aligned with D6 refactor document original recommendation.
- **Cons**: `actual_start` precision limited to buffer boundary (23-46ms on Android), exceeding F2's 5ms threshold and generating spurious step-3 correction warnings; no foundation for adaptive music in content phase.
- **Rejection Reason**: Android actual_start imprecision (23-46ms) would mask real calibration issues in FE3 and generate false-positive log noise throughout development. FMOD eliminates this class of problem entirely.

### Alternative 2: Unity Native Audio + OnAudioFilterRead Scheduling
- **Description**: `AudioSettings.dspTime`; beat detection from `OnAudioFilterRead` (audio thread, ~5.8ms iOS / ~23ms Android intervals); `ConcurrentQueue<BeatEventData>` marshaling to main thread.
- **Pros**: Better callback precision than Update polling without FMOD dependency.
- **Cons**: `ConcurrentQueue` adds ~1 frame delay; same Android `actual_start` imprecision as Alternative 1; F2 GDD's thread-safety warning (Core Rule 12) explicitly warns against this pattern's complexity.
- **Rejection Reason**: Comparable integration complexity to FMOD without FMOD's precision advantages or adaptive music foundation.

## Consequences

### Positive
- Sample-level DSP clock precision (~0.02ms @ 48kHz) satisfies F2's < 0.1ms timestamp promise with a 5× margin
- Android `actual_start` precision: buffer-boundary quantization (23-46ms) → sample-level (~0.7ms with FMOD AAUDIO 32-sample buffer); F2 step-3 校正 retains its 5ms threshold — FMOD means step-3 triggers only on genuine timing anomalies, not buffer noise
- No `ConcurrentQueue` required: `FMODUnity.RuntimeManager.Update()` runs on main thread; BeatClock.Update() polling is fully main-thread-safe
- `IDSPClock` / `IAudioPlayer` abstraction in F2 GDD means `BeatClock.cs` changes zero lines; only the injected implementations differ
- Foundation for adaptive music (BPM / time-signature transitions, parameter automation) via FMOD Studio bank events for SFX and ambient in content phase
- FMOD precision improves FE3 latency calibration measurement baseline (FE3 measures audio round-trip time; lower clock noise = more accurate calibration result)

### Negative
- Additional native library dependency per platform (FMOD `.dll` / `.framework` / `.so`); build pipeline must include FMOD binaries for each target
- Console builds (PS5 / Xbox / Switch) require NDA-gated FMOD console SDK binaries obtained separately from console manufacturer developer portals — adds lead time for console certification
- FMOD for Unity is NOT a UPM package; version must be manually tracked and pinned outside `Packages/manifest.json`
- FMOD Studio authoring tool required for SFX / adaptive music authoring in content phase (Post-MVP); BGM for MVP uses raw audio files via core API — Studio tool not needed for MVP
- Feel / MMFeedbacks `MMF_AudioSource` feedbacks bypass FMOD entirely (play through Unity's audio output); on consoles where all audio routes through FMOD this may cause inaudible SFX — requires `MMF_FMODEvent` replacement (Post-MVP task, before console certification)

### Risks
- **HIGH: Unity 6.3 LTS native plugin compatibility unverified.** Unity 6.3 changed the native plugin validation pipeline. FMOD native binaries must be compiled against Unity 6000.3 plugin API headers. If the available FMOD for Unity package predates this, compilation may fail or native plugin registration may fail at runtime. **Mitigation**: M0 Spike first task — import FMOD package, run a test build, verify no errors. If incompatible, fall back to Alternative 1 for MVP.
- **HIGH: Unity 6.3 `[SerializeField]` fields-only change.** FMOD for Unity's editor scripts may use `[SerializeField]` on properties (now a compile error in Unity 6.3). **Mitigation**: Verify FMOD Settings inspector on import; use a FMOD Unity package that explicitly targets Unity 6.3+.
- **MEDIUM: FMOD Studio ↔ Unity package version lock.** FMOD Studio authoring tool and Unity integration package must be the same major.minor version. Mismatched versions cause silent bank deserialization failures. **Mitigation**: Document exact versions in `docs/engine-reference/unity/plugins/fmod.md` on first import; update tool and package together as a single versioned upgrade.
- **MEDIUM: FMOD sample rate vs Unity sample rate.** FMOD may initialize at a different sample rate than `AudioSettings.outputSampleRate` (they are independent audio systems). Using `AudioSettings.outputSampleRate` as the DSP clock divisor in `FMODDSPClockImpl` produces a clock that drifts from wall clock time. **Mitigation**: Always call `coreSystem.getSoftwareFormat(out int sampleRate, ...)` — never `AudioSettings.outputSampleRate` — in DSP clock conversion. Unit-test round-trip correctness.
- **LOW: BGM streaming latency.** If BGM is loaded as a streaming sound (`FMOD_CREATESTREAM`) rather than fully loaded in memory (`FMOD_CREATESAMPLE`), FMOD buffers asynchronously before playback begins, adding variable pre-buffer latency that invalidates the 500ms pre-anchor assumption. **Mitigation**: All BGM anchor clips must be loaded with `FMOD_CREATESAMPLE` (in-memory). Streaming is acceptable for ambient / background tracks that are not rhythm-critical.
- **LOW: Feel / MMFeedbacks audio routing.** See Negative consequences above. **Mitigation**: Defer to Post-MVP; document as a console certification prerequisite.

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| audio-beat-clock.md (F2) | Core Rule 2: DSP 时钟基准，误差 < 0.1ms | FMOD `getDSPClock()` is sample-accurate (~0.02ms @ 48kHz); satisfies < 0.1ms with 5× margin |
| audio-beat-clock.md (F2) | Core Rule 12: 调度机制由 ADR-D6 决定（Update 轮询 / OnAudioFilterRead / FMOD DSP callback） | Establishes **FMODUnity RuntimeManager + Update-loop polling** as scheduling mechanism; fully main-thread safe, no `ConcurrentQueue` |
| audio-beat-clock.md (F2) | Core Rule 12: ⚠ 线程安全约束（ADR-D6 必要前提）| `RuntimeManager.Update()` runs on main thread before BeatClock.Update(); all C# event dispatch is on main thread; thread-safety constraint satisfied without ConcurrentQueue |
| audio-beat-clock.md (F2) | F6 BGM play protocol: actual_start 公式精度受限于 Android 缓冲区边界 (23-46ms) | FMOD eliminates buffer-boundary quantization: `actual_start = scheduledStartDSPClock / fmodSampleRate` is sample-level accurate |
| audio-beat-clock.md (F2) | IDSPClock / IAudioPlayer 依赖注入接口 | Production implementations: `FMODDSPClockImpl : IDSPClock`, `FMODAudioPlayerImpl : IAudioPlayer` (class names and API defined above) |

## Performance Implications
- **CPU**: FMOD audio processing runs on FMOD's internal audio thread (no main-thread impact). `RuntimeManager.Update()` main-thread cost: ~0.05ms/frame. BeatClock.Update() DSP clock read: ~0.01ms/frame. Total F2 contribution: < 0.2ms/frame — within Foundation system budget.
- **Memory**: FMOD runtime: ~2-5MB. BGM audio data (`FMOD_CREATESAMPLE`): depends on track length and format; use Vorbis compression (streaming disabled for rhythm-critical BGM). FMOD bank assets (SFX): loaded via Addressables on demand.
- **Load Time**: FMOD System initialization: ~50ms at application startup (acceptable during splash screen).
- **Network**: None.

## Migration Plan
Greenfield project — no existing audio code to migrate. Integration sequence for M0 Spike:

1. Download FMOD for Unity integration package from Firelight Technologies website; verify package explicitly lists Unity 6000.3.x as tested version
2. Import into `Assets/Plugins/FMOD/`; configure FMOD Settings (output format, sample rate)
3. Create `docs/engine-reference/unity/plugins/fmod.md` documenting exact FMOD Studio version and Unity package version — never upgrade independently
4. Implement `FMODDSPClockImpl` and `FMODAudioPlayerImpl` in `Assets/Scripts/Core/Audio/`
5. Write EditMode unit tests for both implementations using `MockDSPClock` (test DSP time math, not FMOD runtime — tests must be deterministic)
6. Inject into BeatClock via Inspector reference (same pattern as `MockDSPClock` in tests)
7. Run M0 Spike precision measurement: 500-beat sequence, measure `actual_start` deviation on Android (target: < 5ms vs 23-46ms Unity Native baseline)

**Post-MVP tasks** (not blocking MVP):
- Replace Feel/MMFeedbacks `MMF_AudioSource` feedbacks with `MMF_FMODEvent` (required before console certification)
- Author FMOD Studio bank for SFX when adaptive music features are added in content phase
- Obtain console-specific FMOD SDKs (NDA-gated, per console manufacturer portal)

## Validation Criteria
1. **Build**: FMOD for Unity package imports without compile errors in Unity 6.3 LTS (6000.3.19f1) — zero missing native library warnings, no `[SerializeField]` compile errors
2. **DSP clock precision**: `FMODDSPClockImpl.GetCurrentTime()` EditMode test — 500 consecutive DSP clock samples match expected beat intervals within < 0.1ms (MockDSPClock, deterministic)
3. **Android actual_start accuracy**: M0 Spike measurement — `actual_start` deviation from `scheduledDSPTime` < 5ms on Android (vs 23-46ms Unity Native baseline)
4. **Thread safety**: 1000 rapid BeatClock.Update() calls in PlayMode produce zero `UnityException` or cross-thread errors
5. **Scheduled playback**: `FMODAudioPlayerImpl` — audio begins within 2ms of `scheduledStartDSPClock` on PC (verified via FMOD DSP clock read immediately after playback starts in a PlayMode test)

## Related Decisions
- `design/gdd/audio-beat-clock.md` — F2 GDD; consumer of `IDSPClock` / `IAudioPlayer` interfaces; F2-F7 formula updated to use `IDSPClock.GetCurrentTime()` per this ADR
- D1 (TDE integration): TDE handles movement/lifecycle audio at the MMFeedbacks level; FMOD handles all audio output — no conflict at the architecture level; Feel/MMFeedbacks audio feedback types require Post-MVP migration to `MMF_FMODEvent`
- D5 (Rhythm Core design): BeatScheduler in CombatCore uses `IDSPClock` — `FMODDSPClockImpl` is the production implementation
- ADR-D2 (Enemy AI): No dependency; AI systems access beat data via BeatClock's C# events, not via audio APIs
- ADR-D10 (Bootstrap Sequencing & Latency Injection Topology): consumes the main-thread threading model formalized in the Addendum below; the F6 boot-inject of `LatencyOffsetMs` runs on the same main thread as BeatClock dispatch

---

## Addendum — BeatClock Threading Model (2026-08-08)

**Status**: Accepted addendum to ADR-D6 (Accepted). Clarifying/normative only — does not reverse the original decision; it makes the thread model implicit in the Decision body explicit and normative so that F2 GDD Core Rule 12 / Rule 14 can drop their now-stale conditional clauses. Resolves F2 review **OQ-6**.

### Why this addendum

ADR-D6's Decision selected **FMODUnity RuntimeManager + main-thread Update-loop polling** of the FMOD DSP clock and explicitly **rejected `OnAudioFilterRead`** (Alternative 2). Consequently the audio-thread-callback marshalling machinery (`ConcurrentQueue<BeatEventData>`) is **not** part of this architecture. However, F2 GDD Core Rule 12 (`audio-beat-clock.md:65`) and Rule 14 (`:74`) still carry a **conditional** thread-safety clause — *"若 ADR-D6 选用 `OnAudioFilterRead`…须通过 `ConcurrentQueue<BeatEventData>`…"* — whose premise ADR-D6 has now falsified. That conditional is stale (flagged as OQ-6). This addendum states the authoritative thread model so those clauses can be simplified.

### Normative thread model

1. **Two threads, one dispatch thread.** FMOD runs its audio mixing on its **own internal audio thread** (see Architecture Diagram: `FMOD Audio Thread (internal)`). All BeatClock logic and **all C# beat event dispatch run exclusively on the Unity main thread.** There is **no** beat callback on the FMOD audio thread and **no** use of Unity's `OnAudioFilterRead` audio thread.

2. **Poll, don't call back.** `BeatClock.Update()` (main thread) reads the DSP clock via `IDSPClock.GetCurrentTime()` and dispatches `DispatchBeatEvent()` **directly, with no queue and no cross-thread marshalling.** `ConcurrentQueue<BeatEventData>` is **NOT required and must not be introduced** under this architecture — it would add ~1 frame of latency for zero benefit.

3. **Frame ordering guarantee.** `FMODUnity.RuntimeManager.Update()` runs (via its own hook) **before** `BeatClock.Update()` within the same frame, so the DSP clock BeatClock reads is fresh. If a Script Execution Order dependency is needed to guarantee this, it must be documented in the F2/bootstrap setup (see ADR-D10 Bootstrap Sequencing Contract).

4. **Double-update prohibition.** `BeatClock` (and any other system) **must NOT** call `FMOD.Studio.System.Update()` — RuntimeManager already calls it once per frame; a second call corrupts FMOD's command queue. (Restates Decision §Integration Surface as a thread-model invariant.)

5. **DSP clock read contract.** The DSP clock is read on the **main thread** from the **master channel group** (`getMasterChannelGroup().getDSPClock(out ulong dspClock, …)`), returning a **`ulong`** sample counter maintained by FMOD's audio thread. Conversion to `double` seconds uses `coreSystem.getSoftwareFormat()` sample rate (never `AudioSettings.outputSampleRate`). No lock is required for this read because BeatClock never writes the DSP clock — it only reads FMOD's monotonic counter.

6. **Latency-offset injection is main-thread.** All writers of `BeatClock.LatencyOffsetMs` (F6 boot-inject and FE3 runtime calibration, per ADR-D10 D-5) execute on the main thread, coherent with main-thread dispatch. No synchronization primitive is required.

### Downstream contract changes (F2 GDD, to apply in the same-round revision)

- **Rule 12 (`:65`)**: replace the conditional `OnAudioFilterRead` / `ConcurrentQueue` clause with a normative statement: *dispatch is main-thread Update-loop polling (ADR-D6 Addendum); no ConcurrentQueue.* Keep the enduring downstream contract (subscribers must read `BeatEventData.beat_dsp_time`; forbidden to read `AudioSettings.dspTime` in the callback).
- **Rule 14 (`:74`)**: remove the "若 ADR-D6 调度机制最终确定为异步回调" sub-clause — the premise is closed.
- **PlayMode thread-safety test** remains valid and required (Validation Criteria #4: 1000 rapid `BeatClock.Update()` calls → zero `UnityException`), now as a guard that the main-thread invariant holds, not as coverage of a queue.

### Non-goals / boundaries

- This addendum governs **beat-event dispatch timing on the audio↔main-thread boundary** only. It does **not** change **input** timing: input judgement uses F3's `InputAction.performed` callback timestamp (per game-concept.md technical risk / CLAUDE.md forbidden pattern), which is a separate clock from F2's Update-loop dispatch. F2 broadcasts a precise DSP timestamp *value* (< 0.1ms); the value's precision is independent of when the C# callback fires (F2 Core Rule 12).
