# ADR-D10: Bootstrap Sequencing & Latency Injection Topology

## Status
Accepted

## Date
2026-08-08 (proposed) · 2026-08-09 (accepted — boot-inject topology aligned with ADR-D1: `Play()`/`EnableGameplayInput()` stay in `OnSceneLoaded(Single)`; single bootstrap driver; release-active runtime guard)

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (6000.3.19f1) |
| **Domain** | Core (bootstrap / lifecycle) + Audio (BeatClock latency injection) |
| **Knowledge Risk** | LOW-MEDIUM — Relies on Unity's documented "all `Awake()` complete before the first `Start()`" guarantee, plus a single explicit bootstrap driver for cross-component ordering (Script Execution Order is deliberately NOT relied upon as the ordering mechanism). `SceneManager.sceneLoaded` semantics stable since Unity 4. FMOD/DSP behaviour verified via ADR-D6; EasySave 3 synchronous read verified via ADR-D7. No post-cutoff API introduced. |
| **References Consulted** | `docs/architecture/adr-d6-audio-middleware.md`, `docs/architecture/adr-d7-save-persistence-middleware.md`, `docs/architecture/adr-d1-tde-integration.md`, `docs/engine-reference/unity/modules/audio.md`, `docs/engine-reference/unity/current-best-practices.md` |
| **Post-Cutoff APIs Used** | None — this ADR composes existing ADR-D6/D7 APIs and standard `MonoBehaviour` lifecycle (`Awake`/`Start`, `SceneManager.sceneLoaded`) plus a single explicit bootstrap driver that sequences the phases directly (no reliance on `[DefaultExecutionOrder]` / Script Execution Order for ordering). |
| **Verification Required** | 1. PlayMode test confirming `SaveService.Initialize()` completes within the Awake→Start window on all target platforms (esp. mobile, where the ES3 load budget is < 100ms). 2. Confirm the single explicit bootstrap driver reliably orders `SaveService` initialization before `TDELifecycleAdapter.Start()` (Script Execution Order is not relied upon). 3. Confirm the boot-injected `LatencyOffsetMs` is applied inside the first `OnSceneLoaded(Single)` pass, before the first `BeatClock.Play()` and the first `OnBeatExact` broadcast. |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-D6 (BeatClock threading model / DSP clock — Accepted), ADR-D7 (F5 EasySave 3 synchronous read — Accepted), ADR-D1 (TDE integration / F6 `TDELifecycleAdapter` lifecycle authority — Accepted) |
| **Enables** | F2 / F5 / F6 GDD same-round revision (resolves OQ-4 / W-4-01); FE3 Latency Calibration System GDD (defines FE3 as the sole runtime writer contract FE3 must honour when authored) |
| **Blocks** | F2 `audio-beat-clock.md:~425/:455-458` revision, F5 `save-persistence.md:127/221/316` revision, F6 `tde-integration.md` startup-chain revision + new AC-F6-23 — all frozen pending this ADR (per OQ-4 "保留原文，避免半提交") |
| **Ordering Note** | Must be Accepted before the F2/F5/F6 three-document same-round revision pass begins. Does not block ADR-D8 (Combat Director) or ADR-D9 (Combo Graph). |

## Context

### Problem Statement
F2's Round-8 review deferred two interlocking decisions (D-4, D-5) that jointly gate the **"B2b init sequencing"** contract (cross-review risk **W-4-01**, tracked as **OQ-4** in `audio-beat-clock.md:756`). Until both are decided, `audio-beat-clock.md:~425` and the F5/F6 handshake text are deliberately left unmodified to avoid a half-committed spec. Two distinct problems:

- **D-4 — F5 `Initialize()` sync or async?** The F2↔F5 latency handshake (F2 self-pulls the calibration offset in its own `Start()`) is only valid if `SaveService.Initialize()` completes synchronously within the Awake→Start window. qa-lead flagged a *suspicion* that `Initialize()` "may be async" (ES3 via coroutine/Task), which would invalidate the ordering assumption and is the stated reason a startup gate was proposed. No document actually specs `Initialize()` as async, and ADR-D7 gives ES3 no async read path — yet no document explicitly stamps it synchronous either. This ambiguity keeps the sequencing frozen.

- **D-5 — Who is the authoritative writer of `BeatClock.LatencyOffsetMs`?** Three declared paths exist with no single authority:
  1. **F2 Core Rule 10 invariant** (`audio-beat-clock.md:57`): the offset is *"仅由 FE3 校准流程更新"* (only updated by the FE3 calibration flow) — a single-writer = FE3 invariant. (Note: **F3 ≠ FE3**. `input-event-system.md` is F3, the input system, which only *reads* the offset. FE3, the Latency Calibration System, has no GDD yet.)
  2. **F5 + so-data-layer** (`save-persistence.md:127/221/316`, `so-data-layer.md:256`): at startup, **F2 (BeatClock) itself** calls `LoadLatencyCalibration()` in `Start()` and self-assigns `LatencyOffsetMs` when `isCalibrated=true` — a *second* writer (the boot-load path) that Rule 10's "only FE3" does not cover.
  3. **The chosen "F6 unified startup gate" direction** (OQ-4): F6 owns the startup gate; cross-review W19 proposed `F5.Load() → FE3 cache → F2.LatencyOffsetMs`, still unresolved.

### Constraints
- ADR-D7 mandates F5 provides a **synchronous, main-thread, blocking** API; ES3 has no async read path; `ES3Cloud` is forbidden. `Initialize()` load budget: < 50ms PC / < 100ms Mobile (splash-screen cost).
- ADR-D6 fixes BeatClock dispatch on the **main thread** (Update-loop polling of the FMOD DSP clock; see ADR-D6 Threading Model Addendum). No audio-thread callback, no cross-thread marshalling.
- F6 `TDELifecycleAdapter` is the **sole controller** of `BeatClock.Play/Stop/Pause/Resume` (F6 Core Rule 4 / ADR-D1). Any boot-time injection into BeatClock must route through, or be ordered by, this adapter.
- F2 Core Rule 10 requires a **single-writer invariant** on `LatencyOffsetMs` to prevent runtime write races (device-level calibration data must not be clobbered mid-session).
- FE3 (Latency Calibration System) has **no GDD yet** — it cannot be relied upon to exist at boot for MVP.

### Requirements
- `SaveService.Initialize()` must have an explicit, testable sync/async stamp.
- The boot-load of the persisted offset must have exactly one declared owner.
- The runtime single-writer invariant (Rule 10) must be preserved for gameplay.
- F2 must no longer carry a hidden, un-owned runtime dependency on F5.
- The startup chain must be deterministically orderable and headless-testable (a mock seam for F5 that F2/F6 can inject).
- `LatencyOffsetMs` must be injected before the first beat broadcast, so no beat is evaluated with a stale `offset=0`.

## Decision

### D-4 — `SaveService.Initialize()` is SYNCHRONOUS (+ `ISaveService` consumer seam)

`SaveService.Initialize()` is formally **synchronous, main-thread, blocking**, returning `void`. It performs all GlobalData + active-slot loads (including `LatencyCalibrationDTO`) and completes before any `Start()`-phase consumer runs, per Unity's guarantee that all `Awake()` complete before the first `Start()`. This ratifies (does not change) F5 Rule 7 / Rule 14 and ADR-D7 §Constraints. **No async path, no coroutine, no `Task`, no timeout/fallback machinery is introduced** — a blocking splash-screen load within the < 50ms PC / < 100ms Mobile budget.

To make the startup gate (below) deterministically testable, a **consumer-facing `ISaveService` interface** is defined as the mock seam for F2/F6:

```csharp
// NEW — consumer-facing seam. Distinct from F5's internal IFileStorage
// (IFileStorage mocks the STORAGE BACKEND for F5's own tests; ISaveService
//  mocks the WHOLE SERVICE for F2/F6 consumer tests). Both coexist.
public interface ISaveService {
    void Initialize();                                  // synchronous, blocking, main thread
    bool IsInitialized { get; }                         // true once state == Idle (Initialize completed)
    LatencyCalibrationDTO LoadLatencyCalibration();     // returns defaults {0f, isCalibrated=false} if absent
    void SaveLatencyCalibration(LatencyCalibrationDTO dto);
    // (additional Load* methods — LoadSettings(), LoadSlotProgress(int), GetActiveSlotIndex() — as F5 already defines)
}

// Production:  SaveService : ISaveService        (singleton, DontDestroyOnLoad)
// Test:        MockSaveService : ISaveService    (returns canned DTOs; no file I/O)
```

### D-5 — Two-phase `LatencyOffsetMs` ownership (boot-inject by F6 gate; FE3 sole runtime writer)

`BeatClock.LatencyOffsetMs` has a **two-phase writer model**:

- **Boot phase (one-time, guaranteed before the first `BeatClock.Play()`):** the F6 `TDELifecycleAdapter` startup coordinator loads the persisted `LatencyCalibrationDTO` from F5 (synchronously, per D-4) and injects it into `BeatClock.LatencyOffsetMs` **exactly once**. The injection is performed inside the first `OnSceneLoaded(LoadSceneMode.Single)` pass — the same method that owns `BeatClock.Play()` and `EnableGameplayInput()` per **ADR-D1** — immediately *before* that first `Play()`, so it is guaranteed to precede the first `OnBeatExact` broadcast and the input enable. **`Play()` / `EnableGameplayInput()` are NOT relocated into `Start()`; they remain in `OnSceneLoaded(Single)` exactly as ADR-D1 specifies.** A one-time guard flag ensures re-entrant `OnSceneLoaded(Single)` passes (respawn / level change) do not re-inject and clobber a runtime FE3 write. If `isCalibrated=false` (first launch / missing file), the injected value is the default `0ms`.
- **Runtime phase (during gameplay):** the **FE3 calibration flow is the sole writer** of `LatencyOffsetMs`. F2 Core Rule 10's single-writer invariant is **preserved for the runtime phase** and re-scoped to say so explicitly.

**F2 no longer self-loads from F5.** The `LoadLatencyCalibration()` call moves out of `BeatClock.Start()` and into the F6 startup coordinator (specifically, the first `OnSceneLoaded(Single)` pass, ahead of `Play()`). This removes the un-owned second writer and F2's hidden runtime dependency on F5; BeatClock becomes a pure holder that is *injected into* (by F6 at boot, by FE3 at runtime), never a loader.

This is consistent with the previously-chosen "F6 unified startup gate" direction and with F6 already being the sole lifecycle controller of BeatClock.

### Bootstrap Sequencing Contract (resolves W-4-01)

The startup chain is deterministically ordered by a **single explicit bootstrap driver** — one component that calls the phases in sequence (see Migration Plan). Script Execution Order is *not* relied upon as the ordering mechanism; the bootstrap driver is the sole guarantor of order:

```
[Awake phase — Unity guarantees all Awake() finish before any Start()]
  SaveService.Awake()            // registers singleton; does NOT load (Rule 14)
  BeatClock.Awake()              // registers singleton; DontDestroyOnLoad [BeatClock]
  TDELifecycleAdapter.Awake()    // registers; holds ISaveService + BeatClock refs

[Start phase — the single bootstrap driver enforces: SaveServiceBootstrap BEFORE TDELifecycleAdapter]
  1. SaveServiceBootstrap.Start():
        saveService.Initialize();                         // D-4: SYNCHRONOUS, blocking — loads LatencyCalibrationDTO
  2. TDELifecycleAdapter.Start():                          // driven after step 1 by the bootstrap driver
        if (!saveService.IsInitialized) FailFast(...);     // runtime guard — release-active (NOT Debug.Assert)
        VerifyFMOD();  VerifyFMODAsyncMode();              // ADR-D6 / F6 Core Rule 12 (existing)
        SceneManager.sceneLoaded += OnSceneLoaded;         // per ADR-D1 — Play()/input live in OnSceneLoaded, NOT here

[First OnSceneLoaded(LoadSceneMode.Single) — ADR-D1 owns Play() / EnableGameplayInput()]
  TDELifecycleAdapter.OnSceneLoaded(scene, Single):
        if (!_offsetInjected) {                            // one-time guard — re-entrant passes skip re-inject
            var dto = saveService.LoadLatencyCalibration();// D-5 BOOT-LOAD (F6 owns it, not F2)
            beatClock.LatencyOffsetMs = dto.latencyOffsetMs;// D-5 BOOT-INJECT (once; default 0 if !isCalibrated)
            _offsetInjected = true;
        }                                                  // ← guaranteed BEFORE the first Play() below
        beatClock.Play(clip, bpm, timeSig);                // ADR-D1 — unchanged location & authority (3-step protocol)
        EnableGameplayInput();                             // ADR-D1 — input enabled after clock start + offset inject

[Runtime]
  FE3 calibration flow → beatClock.LatencyOffsetMs = newOffset;   // D-5: SOLE runtime writer (Rule 10)
```

Because `Initialize()` is synchronous (D-4), step 1 fully completes before step 2 reads — no async wait, no timeout policy needed. Play/input ordering is unchanged from ADR-D1 (both in `OnSceneLoaded(Single)`); this ADR only inserts the one-time boot-inject ahead of the first `Play()` in that same method. The gate's only failure surface is misordering, which the single bootstrap driver + the release-active `IsInitialized` runtime guard make deterministic and headless-testable.

### Key Interfaces

```csharp
// F6 startup coordinator (inside TDELifecycleAdapter, or a helper it owns):
public class TDELifecycleAdapter : MonoBehaviour {
    [SerializeField] private BeatClock _beatClock;      // injected (or BeatClock.Instance)
    private ISaveService _saveService;                  // injected — D-4 seam (prod: SaveService.Instance)
    private bool _offsetInjected;                        // one-time boot-inject guard

    // Start(): FMOD guard + sceneLoaded subscription ONLY.
    // Play()/EnableGameplayInput() are NOT called here — they live in OnSceneLoaded(Single) per ADR-D1.
    private void Start() {
        // Release-active runtime guard (NOT Debug.Assert — must fail loudly in shipped builds):
        if (!_saveService.IsInitialized) {
            Debug.LogError("[F6-FATAL] SaveService not initialized before TDELifecycleAdapter.Start() " +
                           "— the bootstrap driver did not order SaveServiceBootstrap first.");
            enabled = false;                                       // do not proceed; scene stays in a safe frozen state
            return;
        }
        // ... FMOD readiness gate (existing AC-F6-04 / AC-F6-21) ...
        SceneManager.sceneLoaded += OnSceneLoaded;                // ADR-D1 lifecycle wiring
    }

    // OnSceneLoaded(Single): unchanged ADR-D1 authority over Play()/input,
    // with the one-time boot-inject inserted ahead of the FIRST Play().
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        if (mode != LoadSceneMode.Single) return;                 // ADR-D1 additive-load filter
        if (!_offsetInjected) {
            var cal = _saveService.LoadLatencyCalibration();      // D-5 boot-load (F6 owns it, not F2)
            _beatClock.LatencyOffsetMs = cal.latencyOffsetMs;     // D-5 boot-inject (once; default 0 if !isCalibrated)
            _offsetInjected = true;                               // guaranteed before the first Play() below
        }
        _beatClock.Play(/* clip, bpm, timeSig from SO */);        // ADR-D1 — unchanged location & authority
        EnableGameplayInput();                                    // ADR-D1 — after clock start + offset inject
    }
}
```

## Alternatives Considered

### Alternative 1: Asynchronous `Initialize()` (rejected for D-4)
- **Description**: `SaveService.Initialize()` returns a `Task`/coroutine; F6 gate awaits completion with a timeout/fallback policy.
- **Pros**: Would formally cover the "hung Initialize" edge case; non-blocking splash.
- **Cons**: Directly contradicts ADR-D7 §Constraints ("主线程同步阻塞") and §Requirements; ES3 has no async read path; would force a new timeout/fallback subsystem and revise ADR-D7. No measured need — ES3 GlobalData load is < 50ms PC / < 100ms Mobile.
- **Rejection Reason**: Solves a suspected problem that the evidence (ADR-D7, ES3 API) shows does not exist; adds complexity and an ADR conflict for zero benefit.

### Alternative 2: FE3 is the sole writer for both boot and runtime (rejected for D-5)
- **Description**: An FE3 bootstrap loads from F5 and injects at boot *and* FE3 owns runtime writes; F6 gate only orders FE3.
- **Pros**: Purest single-writer model — one owner, no phase split.
- **Cons**: FE3 (Latency Calibration System) has **no GDD** and no guaranteed boot-time presence for MVP; the boot-load would have no concrete owner until FE3 is authored, re-blocking the very sequencing this ADR must unblock.
- **Rejection Reason**: Cannot deliver an MVP-ready boot path; would keep OQ-4 frozen on an unwritten system.

### Alternative 3: Keep F2 self-load, only amend Rule 10 (rejected for D-5)
- **Description**: F2 continues to call `LoadLatencyCalibration()` in `BeatClock.Start()`; Rule 10 is reworded to "boot: F2 loads from F5; runtime: FE3 only."
- **Pros**: Smallest textual change.
- **Cons**: Retains two writers of `LatencyOffsetMs` and F2's hidden runtime dependency on F5; leaves the boot-load owner as F2 (a system whose GDD claims zero runtime save dependency), perpetuating the cross-review W19 "no single authority" finding.
- **Rejection Reason**: Weakens the single-writer invariant and does not centralize the startup gate the project already chose (F6-owned).

## Consequences

### Positive
- OQ-4 / W-4-01 unblocked; F2/F5/F6 can be revised in one coherent pass citing this ADR.
- `LatencyOffsetMs` gains a single boot owner (F6 gate) and a single runtime owner (FE3); Rule 10's invariant is preserved and made precise.
- BeatClock becomes a pure injected holder — no F5 dependency, cleaner testability.
- Deterministic, headless-testable startup: the `ISaveService` seam + release-active `IsInitialized` runtime guard enable AC-F6-23 with a `MockSaveService`, no real file I/O.
- No new async/timeout subsystem; no ADR-D7 conflict.
- No beat is ever evaluated with a stale `offset=0` (injection ordered before first broadcast and before input enable).

### Negative
- F6 `TDELifecycleAdapter` gains a data-loading responsibility (reading `LatencyCalibrationDTO`) in addition to lifecycle control — a mild broadening of its role, mitigated by the `ISaveService` abstraction.
- Introduces a single-bootstrap-driver ordering dependency (SaveService bootstrap before TDELifecycleAdapter) that the driver must own explicitly — a latent misconfiguration surface if the driver is bypassed.
- A new `ISaveService` interface to define and keep in sync with F5's concrete `SaveService`.

### Risks
- **MEDIUM: Bootstrap-driver bypass.** If a scene wires `SaveServiceBootstrap` and `TDELifecycleAdapter` without routing them through the single bootstrap driver, the gate could read before load. **Mitigation**: a single explicit bootstrap driver is the *committed* ordering mechanism (Script Execution Order is not relied upon); it calls the phases in sequence, and the release-active `if (!_saveService.IsInitialized) FailFast()` runtime guard (NOT `Debug.Assert`, which is stripped from shipped builds) converts any bypass into a loud, testable failure. Cover with a PlayMode AC.
- **LOW: FE3 contract drift.** FE3's future GDD must honour "sole runtime writer." **Mitigation**: register the ownership stance in `architecture.yaml` so `/architecture-review` flags any FE3 GDD that violates it.
- **LOW: `Initialize()` exceeds the Awake→Start budget on a slow platform.** **Mitigation**: the release-active `IsInitialized` runtime guard converts a silent race into a loud, testable failure; if a platform genuinely exceeds budget, that is a separate perf issue surfaced by Verification Required #1, not a correctness gap.

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| save-persistence.md (F5) | Rule 7 / Rule 14: synchronous API, Awake/Initialize separation | Ratifies `Initialize()` as explicitly synchronous/blocking (D-4); no change to existing rules, formalizes the implicit stamp that kept OQ-4 frozen |
| save-persistence.md (F5) | `:127/:221/:316` `LoadLatencyCalibration()` caller | Moves the caller from F2 `BeatClock.Start()` to the F6 `TDELifecycleAdapter` startup coordinator (D-5); F5 interactions table updated accordingly |
| audio-beat-clock.md (F2) | Core Rule 7 / Rule 10: `LatencyOffsetMs` injection + single-writer invariant | Re-scopes Rule 10 to a two-phase model: boot-inject by F6 (once), FE3 sole runtime writer; removes F2 self-load; preserves the runtime invariant (D-5) |
| audio-beat-clock.md (F2) | `:425/:455-458` dependency text (frozen) | Unfreezes: F2 declares no runtime F5 dependency (injected by F6); dependency text revised in the same-round pass |
| tde-integration.md (F6) | Startup chain + lifecycle authority (Core Rule 4) | Inserts the one-time boot-load + boot-inject into the first `OnSceneLoaded(Single)` pass, immediately before the first `Play()`; `Play()` / `EnableGameplayInput()` remain in `OnSceneLoaded(Single)` per ADR-D1 (NOT moved to `Start()`). `Start()` gains only the release-active `IsInitialized` runtime guard. New **AC-F6-23** (F5-init gate + offset inject, headless via `MockSaveService`) |
| so-data-layer.md | `latencyCalibrationOffsetMs` `[NonSerialized]` field, F5→SO one-way fill | Unchanged; source remains F5's `LatencyCalibrationDTO`; the *consumer* of the loaded value at boot is now F6, not F2 |

## Performance Implications
- **CPU**: `Initialize()` blocking load < 50ms PC / < 100ms Mobile (one-time, splash). Boot-inject is a single field assignment. Zero per-frame cost.
- **Memory**: One `ISaveService` reference held by `TDELifecycleAdapter`; negligible.
- **Load Time**: No change vs current — the ES3 GlobalData load already happens at startup; this ADR only relocates *who* reads the calibration result.
- **Network**: None.

## Migration Plan
Greenfield — no code to migrate. The deliverable this ADR unblocks is the **F2/F5/F6 three-document same-round revision**:

1. **F5** `save-persistence.md`: stamp `Initialize()` explicitly synchronous; add `ISaveService` interface (consumer seam) alongside existing `IFileStorage`; move the documented `LoadLatencyCalibration()` caller from F2 to F6 (`:127/:221/:316`).
2. **F2** `audio-beat-clock.md`: re-scope Rule 10 to the two-phase writer model; remove the F2 self-load from Rule 7 / dependency text (`:425/:455-458`); F2 declares no runtime F5 dependency.
3. **F6** `tde-integration.md`: insert the one-time boot-load + boot-inject into the first `OnSceneLoaded(Single)` pass, immediately before the first `Play()` — do NOT move `Play()` / `EnableGameplayInput()` into `Start()`; they stay in `OnSceneLoaded(Single)` exactly as ADR-D1 specifies. `Start()` gains only the release-active `IsInitialized` runtime guard + `sceneLoaded` subscription. Add **AC-F6-23**; require a single explicit bootstrap driver (not Script Execution Order) to order SaveService bootstrap before TDELifecycleAdapter.
4. Update `audio-beat-clock.md` OQ-4 to Resolved (cite this ADR); update the F2 review log.
5. Registry: add the `LatencyOffsetMs` two-phase ownership stance, the `ISaveService` interface contract, and the "F2 must not self-load latency from F5" forbidden pattern to `docs/registry/architecture.yaml`.

**Note on execution isolation**: the F2 GDD currently has uncommitted Round-8 working-tree edits; the three-document revision must be performed against that working copy (not a fresh branch) to avoid dropping the D-1/D-2/D-3 changes.

## Validation Criteria
1. **D-4 sync**: an EditMode/PlayMode test asserts `SaveService.IsInitialized == true` immediately after `Initialize()` returns (no yield), and that a consumer calling `LoadLatencyCalibration()` in the next `Start()` receives the loaded DTO.
2. **D-5 boot-inject ordering (AC-F6-23)**: a PlayMode test with `MockSaveService` returning `{latencyOffsetMs=42.5f, isCalibrated=true}` asserts `BeatClock.LatencyOffsetMs == 42.5f` *before* the first `BeatClock.Play()` (which stays in `OnSceneLoaded(Single)` per ADR-D1), hence *before* the first `OnBeatExact` broadcast and *before* gameplay input is enabled.
3. **D-5 runtime single-writer**: a test asserts that after boot, only an FE3-path write changes `LatencyOffsetMs` (BeatClock exposes no self-load path; `LoadLatencyCalibration()` is not called from `BeatClock`).
4. **Ordering guard**: a test with a deliberately un-initialized `MockSaveService` triggers the release-active `IsInitialized` runtime guard (fail-fast: logs `[F6-FATAL]` and disables the adapter), proving the gate is not silently skipped. The test must run against a release-config build path (not Debug-only), since the guard must fire in shipped builds where `Debug.Assert` would be stripped.
5. **First-launch default**: with `MockSaveService` returning `{0f, isCalibrated=false}`, `BeatClock.LatencyOffsetMs == 0f` and no error is raised.

## Related Decisions
- ADR-D6 (Audio Middleware) — establishes the main-thread BeatClock threading model this ADR's boot-inject relies on (see ADR-D6 **Threading Model Addendum**, 2026-08-08)
- ADR-D7 (Save Persistence Middleware) — establishes F5's synchronous EasySave 3 read that D-4 ratifies
- ADR-D1 (TDE Integration) — establishes F6 `TDELifecycleAdapter` as sole BeatClock lifecycle controller
- `design/gdd/audio-beat-clock.md` OQ-4 / Core Rule 7 / Core Rule 10; `design/gdd/reviews/audio-beat-clock-review-log.md` (2026-08-08, D-4/D-5 deferral); `design/gdd/reviews/gdd-cross-review-2026-07-19.md` W-4-01
