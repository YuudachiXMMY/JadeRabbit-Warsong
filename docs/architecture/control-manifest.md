# Control Manifest

> **Engine**: Unity 6.3 LTS (6000.3.19f1)
> **Last Updated**: 2026-08-12
> **Manifest Version**: 2026-08-12
> **ADRs Covered**: ADR-D1, ADR-D2, ADR-D3, ADR-D4, ADR-D5, ADR-D6, ADR-D7, ADR-D8, ADR-D10, ADR-D12, ADR-D13
> **Status**: Active — regenerate with `/create-control-manifest update` when ADRs change

`Manifest Version` is the date this manifest was generated. Story files embed
this date when created. `/story-readiness` compares a story's embedded version
to this field to detect stories written against stale rules. Always matches
`Last Updated` — they are the same date, serving different consumers.

This manifest is a programmer's quick-reference extracted from all Accepted ADRs,
technical preferences, and engine reference docs. It answers "what do I do?" and
"what must I never do?" — organized by architectural layer. Where ADRs explain
*why*, this manifest tells you *what*. For the reasoning behind each rule, see the
referenced ADR.

> **Excluded**: ADR-D14 (BPM 变更瞬态取消窗保护) is still **Proposed** and is not
> reflected here. Regenerate this manifest when D14 is Accepted.

---

## Foundation Layer Rules

*Applies to: scene management, event architecture, save/load, engine initialisation,
DI / test seams, bootstrap sequencing, death & run-lifecycle*

### Required Patterns

**TDE integration & lifecycle ownership**
- **TDE source modification is FORBIDDEN — all integration via inheritance, event subscription, or prefab modification only** — source: ADR-D1
- **F3 (`InputEventBus`) is the sole authority over all Action Map state changes** — source: ADR-D1
- **`TDELifecycleAdapter` is the exclusive controller of `BeatClock.Play()`/`.Pause()`/`.Resume()`/`.Stop()` — no other system may call these methods** — source: ADR-D1
- **`OnEnable()` / `OnDisable()` pairing is the only authoritative site for `sceneLoaded` and `MMEventListener<TopDownEngineEvent>` subscription — symmetric subscribe/unsubscribe** — source: ADR-D1 / ADR-D10
- **`TDELifecycleAdapter.Start()` (not `Awake()`) performs a read-only FMOD readiness probe (cache `_fmodVerified`); it does not subscribe, does not `Play()`, does not `EnableGameplayInput()`** — source: ADR-D1 / ADR-D10
- **All readiness pre-conditions (FMOD, `ISaveService.IsInitialized`) are handler-body early-return guards inside `OnSceneLoaded()`, never used to gate subscription actions** — source: ADR-D1
- **`SceneManager.sceneLoaded` handler must guard: skip if `LoadSceneMode != Single` (additive loads filtered)** — source: ADR-D1 / ADR-D10
- **`BeatClock.Pause()` and `F3.EnableUIInput()` must fire atomically in the same frame (same for Resume / EnableGameplayInput)** — source: ADR-D1
- **F3 must remain `AllDisabled` until scene load completes and BeatClock is active** — source: ADR-D1
- **FMOD must be initialized before `BeatClock.Play()` is called** — source: ADR-D1
- **Readiness guard must be a release-active runtime guard (NOT `Debug.Assert`) — must fail loudly in shipped builds: `if (!_fmodVerified || !saveService.IsInitialized) { LogFatal(...); return; }`; scene stays safely frozen (no `Play()`, no input)** — source: ADR-D1 / ADR-D10
- **Guard tests must run against the release-config build path (not Debug-only) — `Debug.Assert` is stripped in release builds** — source: ADR-D10

**Run-end boundary (single exit)**
- **`EndRun(RunOutcome outcome)` is the single run-end entry point; the three-step sequence `BeatClock.Stop()` → `InputEventBus.DisableAllInput()` → broadcast `OnRunEnded(outcome)` executes entirely inside this method** — source: ADR-D1 / ADR-D13
- **Both trigger paths (TDE event bridge / explicit FE-layer call) must funnel into this single `EndRun` method, not each calling the three steps directly** — source: ADR-D13
- **`EndRun` carries a run-scoped one-time latch (`_runEnded`): first entry latches and executes; a repeat trigger within the same run early-returns as a no-op; the latch resets when a new run's `LoadSceneMode.Single` rebuilds the instance** — source: ADR-D1 / ADR-D13
- **All three run-end steps (`Stop()`, `DisableAllInput()`, broadcast) must execute within the same frame/Update** — source: ADR-D12 / ADR-D13
- **On run end, `BeatClock.ClockState == Stopped` and `InputEventBus.CurrentState == AllDisabled` must both hold at/before the broadcast (atomic with the domain event)** — source: ADR-D12 / ADR-D13
- **`RunOutcome` is a pure C# `enum` (`{Death, Victory}`), defined in a CombatCore-visible domain assembly, and must not reference any TopDownEngine types; F6's bridging layer translates TDE events / FE-layer calls into this enum** — source: ADR-D13
- **`OnRunEnded(outcome)` replaces `OnPlayerDeath` entirely — C1/C4/F6/FE5 (+ ADR-D1's event declaration source) all re-subscribe to the single `OnRunEnded`** — source: ADR-D13
- **On `PlayerDeath` (lives = 1, no in-run revival), in the same frame: `BeatClock.Stop()` (also zeroes `global_beat_index` per ADR-D10) → `InputEventBus.DisableAllInput()` → broadcast the domain event (payload carries no TDE types)** — source: ADR-D12
- **Next run = a new `LoadSceneMode.Single` that runs the existing boot path verbatim: `OnSceneLoaded(Single) → offset guard (already-injected → skip) → Play() → EnableGameplayInput()`; prior `Stop()` already zeroed `global_beat_index`, so the new `Play()` is a clean start (no double-`Play()`)** — source: ADR-D12 / ADR-D13
- **TDE in-place respawn is disabled in MVP via Inspector config (Character/Health respawn settings — no TDE source edit). If `RespawnComplete` fires anyway, F6 must log `[F6-FATAL] RespawnComplete fired but in-place respawn is disabled in MVP` and return — must NOT call `BeatClock.Play()` in place** — source: ADR-D1 / ADR-D12
- **Victory/clear determination & initiation belongs to FE4 (Boss defeat) / FE2 (room/`LevelManager` clear) / FE5; F6 bridges native TDE level-complete via the existing `MMEventListener<TopDownEngineEvent>` pattern, or FE4/FE5 explicitly invoke F6's run-end entry point** — source: ADR-D13

**Bootstrap sequencing (ADR-D10)**
- **Startup chain is deterministically ordered by a single explicit bootstrap driver (one component calls each phase in order); Script Execution Order is NOT used as the ordering mechanism** — source: ADR-D10
- **Awake phase: `SaveService.Awake()` registers the singleton only, does not load (Rule 14); `BeatClock.Awake()` registers singleton + `DontDestroyOnLoad`; `TDELifecycleAdapter.Awake()` registers and holds refs** — source: ADR-D10
- **Start phase: bootstrap driver forces `SaveServiceBootstrap.Start()` (calls `saveService.Initialize()`) before `TDELifecycleAdapter.Start()`** — source: ADR-D10
- **`SaveService.Initialize()` is formally synchronous, main-thread, blocking, returns `void`; performs all GlobalData + active-slot loading (incl. `LatencyCalibrationDTO`) before any `Start()`-phase consumer runs** — source: ADR-D10
- **`BeatClock.LatencyOffsetMs` two-phase writer model: at boot, F6 `TDELifecycleAdapter` loads & injects `LatencyOffsetMs` exactly once from F5 in the first `OnSceneLoaded(Single)` before the first `Play()`; at runtime, the FE3 calibration flow is the sole writer** — source: ADR-D10
- **A one-time guard flag ensures re-entrant `OnSceneLoaded(Single)` calls (respawn / new run) do not re-inject and overwrite the runtime FE3 write** — source: ADR-D10
- **F2 (BeatClock) must not self-load latency from F5 — the `LoadLatencyCalibration()` call site moves out of `BeatClock.Start()` into the F6 startup coordinator** (registry forbidden pattern: `f2_self_load_latency`) — source: ADR-D10

**DI / test seams (ADR-D8)**
- **MonoBehaviour does not support constructor injection. Test injection uses: `[SerializeField] private MonoBehaviour _ref;` + property wrapper `private IX X => _ref as IX;` + a `SetXForTesting(IX)` setter inside `#if UNITY_EDITOR || DEVELOPMENT_BUILD`** — source: ADR-D8
- **Test-injection setters are exposed only inside `#if UNITY_EDITOR || DEVELOPMENT_BUILD` — no test seam in release builds** — source: ADR-D8
- **Runtime assemblies must declare `[assembly: InternalsVisibleTo("<test-assembly>")]` for the setter-injection seam, or tests fail to compile (`CS0122`). Verify the exact test-assembly name against the running EditMode asmdef before copying this attribute** — source: ADR-D8
- **Prefer pure C# class constructor injection: pure logic (RNG draw, DSP scheduling, rating judgment) should sink into MonoBehaviour-free pure C# classes with `public` constructors (inject mocks directly, run EditMode NUnit, no `InternalsVisibleTo` needed). Setter injection is only for residual fields that must stay on a MonoBehaviour** — source: ADR-D8
- **`Mock*` implementations (`MockDSPClock`/`MockRandomSource`/`MockBeatClock`) live in `Assets/Tests/EditMode/CombatCore/Mocks/`, referenced by the test asmdef (never in the Runtime package)** — source: ADR-D8
- **`#if UNITY_EDITOR` `OnValidate()` validates interface types at edit time — a wrong (non-implementing) component errors in the Inspector immediately, not as a runtime NRE** — source: ADR-D8
- **`??=` caching guarantees injection is not overwritten by `Awake()` (`AddComponent` triggers `Awake` synchronously)** — source: ADR-D8

**Save / persistence (ADR-D7)**
- **File placement: `ES3Storage` → `Assets/Scripts/Core/Persistence/ES3Storage.cs`; `SaveService` → `.../SaveService.cs`; `IFileStorage` → `.../IFileStorage.cs`** — source: ADR-D7
- **Version migration: write the version into a DTO field (`save_version: int`), handled by `SaveService.MigrateIfNeeded<T>()`; do NOT use EasySave 3's version mechanism (decoupled from `IFileStorage`)** — source: ADR-D7
- **ES3Cloud disabled: keep `ES3Settings.location` default (`File`); do not set `ES3CloudSettings`** — source: ADR-D7
- **Use native `ES3.RenameFile()` (ES3.cs:1142), not a hand-written three-step I/O** — source: ADR-D7
- **Define the consumer-facing `ISaveService` interface as F2/F6's mock seam (`Initialize()`, `IsInitialized { get; }`, `LoadLatencyCalibration()`, `SaveLatencyCalibration(dto)`); it coexists with F5-internal `IFileStorage` (different roles)** — source: ADR-D10

**Audio integration (BeatClock ↔ FMOD, ADR-D6)**
- **BeatClock integration surface uses the FMODUnity RuntimeManager package (official). Do NOT use raw FMOD C# bindings (`fmod.cs`/`fmodstudio.cs`)** — source: ADR-D6
- **BeatClock must not call `FMOD.Studio.System.Update()` — RuntimeManager already calls it once per frame in its own hook (a second call corrupts the command queue)** — source: ADR-D6
- **DSP clock read contract: convert using `coreSystem.getSoftwareFormat()` sample rate; never use `AudioSettings.outputSampleRate` as the DSP-clock divisor** — source: ADR-D6 / ADR-D8
- **Thread model (normative): all BeatClock logic and all C# beat-event dispatch run only on the Unity main thread; no beat callback on the FMOD audio thread, no `OnAudioFilterRead`. `BeatClock.Update()` dispatches `DispatchBeatEvent()` directly — no queue, no cross-thread marshalling. `ConcurrentQueue<BeatEventData>` must not be introduced** — source: ADR-D6
- **Frame ordering: `FMODUnity.RuntimeManager.Update()` must run before `BeatClock.Update()` in the same frame (document any Script Execution Order dependency in F2/bootstrap setup)** — source: ADR-D6
- **BGM anchor clips load via `FMOD_CREATESAMPLE` (in-memory); streaming is only for non-rhythm-critical ambient/background tracks** — source: ADR-D6

**SO data layer — foundation rules (ADR-D4)**
- **Rule D4-1 (runtime read-only): all SO assets are read-only at runtime. No runtime code path may mutate an SO asset's serialized fields. A system needing in-run temp data must clone the target SO first, then modify the clone** — source: ADR-D4
- **Rule D4-5 (Addressables loading, no `Resources/`): all 7 SO asset classes belong to an Addressable Group and load via the Addressables API** — source: ADR-D4
- **Rule D4-7 (Odin dual-mode validation): Plan A (Inspector inline attributes, guarded by `#if UNITY_EDITOR` around `using Sirenix.OdinInspector;`) gives designers live feedback but does NOT drive CI blocking; Plan B (external Validator class, `[RegisterValidator(typeof(XxxSO))]`) is the sole CI-blocking driver and must be implemented** — source: ADR-D4
- **Rule D4-8 (PlayerInfoSO EasySave 3 serialization): `PlayerInfoSO` fields allow only pure data types (`int`/`float`/`string`/`List<T>`/`[Serializable]` struct — no `UnityEngine.Object` subclass refs, no `Dictionary`). F5 extracts a DTO from the SO and serializes the DTO. `latencyCalibrationOffsetMs` and `audioVolumeMaster` must be `[NonSerialized]`** — source: ADR-D4

### Forbidden Approaches

**TDE / input authority**
- **Never remove `InputSystemManagerEventsBased` entirely** — breaks TDE's `AbilityInputAuthorization`; character abilities would fire without authorization checks — source: ADR-D1
- **Never let TDE be the Action Map authority with F3 passively syncing** — F3's `_isSwitchingMaps` guard must be set BEFORE `gameplayMap.Disable()`; passive sync learns of Disable after the `canceled` callback fired, producing an unguardable ghost attack (E3) — source: ADR-D1
- **Never split Action Map authority by lifecycle phase** — two authority owners create same-frame conflicts the `_isSwitchingMaps` guard cannot wrap — source: ADR-D1
- **Never modify TDE source to disable in-place respawn** — must be Inspector config only (TDE upgrades overwrite source edits) — source: ADR-D12
- **Never let `RespawnComplete` restart the clock in place** (`RespawnComplete → BeatClock.Play()`) — the player continues the same run without hitting run settlement, so merit (the process-unique permanent currency) is never converted/persisted (SG-2 defect) — source: ADR-D12 (registry: `f6_respawncomplete_inplace_restart_mvp`)
- **Never bridge victory signals by modifying TDE source** — go through F6's existing `MMEventListener<TopDownEngineEvent>` pattern or an explicit FE-layer call — source: ADR-D13
- **Never maintain two independent (death vs. victory) run-end paths with duplicated cleanup** (Model B, rejected) — prone to "fix one spot, miss the other" drift — source: ADR-D13
- **Never keep `OnPlayerDeath` as a parallel alias alongside `OnRunEnded`** (Model (b), rejected) — dual-event aliasing is the root cause of drift — source: ADR-D13
- **Never guess TDE event/member names from training data** — verify against the engine reference before implementation (`.Respawn` → `.RespawnComplete` lesson) — source: ADR-D13

**DI / interface design**
- **Never let each system define shared interfaces separately** (Alt 1, D8) — dispersed definition is exactly the "no single ownership" defect being cured — source: ADR-D8
- **Never inject concrete classes instead of interfaces** (virtual methods / sealed mock subclass, Alt 2) — cannot achieve pure C# deterministic tests; interfaces are the only clean way to isolate FMOD/TDE deps — source: ADR-D8
- **Never fold lifecycle methods into `IBeatClock`** (single fat interface, Alt 3) — a consumer could then call `Pause()`, so `beatclock_lifecycle_bypass` cannot be blocked at compile time — source: ADR-D8
- **Never expose `Play/Stop/Pause/Resume` on the `IBeatClock` consumer face** — they belong to a separate `IBeatClockLifecycle` (F6-only) — source: ADR-D8
- **Never expose `EnableGameplayInput/EnableUIInput/DisableAllInput` on the `IInputEventBus` consumer read face** (`direct_action_map_enable_disable` ban) — belongs to F3's control face — source: ADR-D8
- **Never have C1/C3/F6 (or any system) define parallel `OnGamePaused`/`OnGameResumed` events** — F2 (BeatClock) is the sole publisher — source: ADR-D8
- **Never introduce a DI container (VContainer/Zenject) without technical-director approval** — neither is in `technical-preferences.md` Allowed Libraries — source: ADR-D8

**Bootstrap / save**
- **Never use an async `SaveService.Initialize()`** (`Task`/coroutine + timeout/fallback, Alt 1) — directly violates ADR-D7 §Constraints (main-thread synchronous blocking); ES3 has no async read path — source: ADR-D10
- **Never make FE3 the sole boot+runtime writer** (Alt 2) — FE3 has no GDD and may not exist at startup in MVP — source: ADR-D10
- **Never call `ES3.Save()`/`ES3.Load()` outside `SaveService`** — all downstream (FE3/FE5/FE7/PL4) reads/writes via `SaveService`'s public interface — source: ADR-D7
- **Never reference `ES3.*` in any runtime code outside `ES3Storage`** — enforced by an Odin Validator namespace-ban rule — source: ADR-D7
- **Never enable `ES3Cloud`** — this project explicitly does not use cloud backup — source: ADR-D7
- **Never serialize objects containing `UnityEngine.Object` references** — DTOs must be pure data (F4/F5 Rule 6) — source: ADR-D7
- **Never use System.Text.Json or Newtonsoft.Json** for the save layer — ~300 lines of unnecessary serialization infra; EasySave 3 is pre-approved in technical preferences — source: ADR-D7

**SO / audio**
- **Never replace the SO data layer with runtime JSON/CSV files** — no Inspector integration, no type safety, runtime parse cost, no editor validation — source: ADR-D4
- **Never store read-only game data in DOTS BlobAssets** — incompatible with MonoBehaviour-based TDE; manual alloc/free; Odin does not support BlobAsset — source: ADR-D4
- **Never `using Sirenix.*` in any Runtime code** — non-editor platform build failure (CS0592 / TypeLoadException) — source: ADR-D4
- **Never pass a `PlayerInfoSO` instance directly to EasySave 3** (`ES3.Save("key", playerInfoSO)` is banned) — source: ADR-D4
- **Never use Unity Native Audio + Update Polling as middleware** — `actual_start` precision limited by buffer boundaries (Android 23–46ms), exceeding F2's 5ms threshold — source: ADR-D6
- **Never use Unity Native Audio + OnAudioFilterRead scheduling** — `ConcurrentQueue` adds ~1 frame latency; same Android precision problem — source: ADR-D6
- **Never use `FMOD.Channel.setPosition(FMOD.TIMEUNIT.DSP)` as a scheduling mechanism** (deprecated; use `Channel.setDelay()` with the absolute system DSP clock) — source: ADR-D6
- **Never use `FMOD.Studio.EventInstance.getTimelinePosition()` as a precision clock** (buffer quantization, not sample-accurate) — source: ADR-D6

### Performance Guardrails
- **`TDELifecycleAdapter`**: fully event-driven, no `Update()` cost; < 0.01ms/frame average — source: ADR-D1
- **Scene-load boot sequence**: `Start()` FMOD check < 0.01ms; boot sequence (`BeatClock.Play` + `EnableGameplayInput`) < 1ms total — source: ADR-D1
- **`SaveService.Initialize()`** (bootstrap blocking load): < 50ms PC / < 100ms Mobile (splash-screen, one-time) — source: ADR-D7 / ADR-D10
- **`SaveService.Save()`** (single call): < 5ms PC / < 16ms Mobile — source: ADR-D7
- **Total save size**: < 10KB raw DTO; < 30KB after ~2–3× encryption inflation — source: ADR-D7
- **`LatencyOffsetMs` boot-inject**: zero per-frame cost — source: ADR-D10
- **F2 timestamp precision**: < 0.1ms deviation vs. theoretical beat time over 500 consecutive beats — source: ADR-D6
- **F2 main-thread total**: `RuntimeManager.Update()` ~0.05ms + `BeatClock.Update()` DSP read ~0.01ms; F2 total < 0.2ms/frame — source: ADR-D6
- **FMOD runtime memory**: ~2–5MB; startup init ~50ms (splash phase); Android `actual_start` target < 5ms — source: ADR-D6
- **SO runtime field read**: < 0.01ms; Addressables async load off the main-thread frame budget — source: ADR-D4

### Engine API Constraints
- **`InputSystemManagerEventsBased` exposes only ONE virtual method: `BindButton(InputAction.CallbackContext, MMInput.IMButton)` (line 42). `SetInputEnabled` and `HandleInput` do NOT exist** — source: ADR-D1
- **TDE uses `TopDownEngineEvent` with `TopDownEngineEventTypes.Pause`/`.UnPause` (GameManager.cs:516-520) — NOT string comparison; use typed `MMEventListener<TopDownEngineEvent>`, not string-based `MMGameEvent`** — source: ADR-D1
- **`TopDownEngineEventTypes.PlayerDeath` exists; `.Respawn` does NOT — use `.RespawnComplete`** (GameManager.cs:38) — source: ADR-D1
- **TDE 4.5 compiles with zero errors in Unity 6.3 LTS** (verified M0 Spike 2026-07-16) — source: ADR-D1
- **`ES3.Load<T>()` real signature: `Load<T>(string key, string filePath, T defaultValue, ES3Settings settings)`** (M0 Spike fix 2026-07-16) — source: ADR-D7
- **EasySave 3 honours .NET `[NonSerialized]` — does not serialize `[NonSerialized]` fields** — source: ADR-D7
- **`FMOD.ChannelGroup.getDSPClock()` lives on `IChannelControl` (Channel/ChannelGroup), NOT on `FMOD.System` — read it from the master channel group; pair with `FMOD.Channel.setDelay()`** (FMOD for Unity 2.02.35) — source: ADR-D6
- **FMOD for Unity is NOT a UPM package** — imported as an asset; version-lock manually outside `Packages/manifest.json` — source: ADR-D6
- **Unity 6.3 changed the native-plugin validation pipeline (HIGH-RISK)** — FMOD native binaries must be compiled against the Unity 6000.3 plugin API headers; confirm FMOD for Unity lists 6000.3.x as tested before import — source: ADR-D6
- **`BeatClock.Stop()` zeroes `global_beat_index`** (per ADR-D10) — this is what makes the next `Play()` clean and prevents double-`Play()` — source: ADR-D12 / ADR-D13
- **Bootstrap relies on Unity's documented guarantee "all `Awake()` complete before the first `Start()`"; Script Execution Order is deliberately NOT relied on as an ordering mechanism** — source: ADR-D10
- **No post-cutoff Unity APIs used across the Foundation ADRs** — standard `MonoBehaviour` lifecycle + `SceneManager.sceneLoaded` + a single explicit bootstrap driver — source: ADR-D7 / ADR-D8 / ADR-D10

---

## Core Layer Rules

*Applies to: BeatClock consumption, rhythm quantization / judgment, combo system,
attack-slot tokens, main player systems*

### Required Patterns

**BeatClock consumer contract**
- **`CombatCore` assembly components are read-only consumers of BeatClock — never call BeatClock lifecycle methods** — source: ADR-D1
- **`IBeatClock` consumer face is read-only: event subscription (`OnBeatLookahead`/`OnHalfBeatLookahead`/`OnBeatExact`/`OnHalfBeatExact`/`OnMeterChangeAccepted`/`OnMeterChanged`/`OnBeatClockPaused`/`OnBeatClockResumed`) + synchronous queries (`GetSnapshot()`/`LatencyOffsetMs`/`DSPClock`/`GetBeatIntervalSeconds()`/`IsStartConfirmed`/`GetConfirmedDSPStart()`); no lifecycle methods** — source: ADR-D8
- **C1 RhythmCore subscribes to `IBeatClock.OnHalfBeatLookahead` (hard dep) + `OnBeatExact`; C4 CombatDirector subscribes to `IBeatClock.OnBeatLookahead` (hard dep) + `OnMeterChanged`** — source: ADR-D1
- **`BeatEventData.BeatDspTime` already includes the latency offset ("player-perceived time domain") — downstream must use it for timing judgment; never read `AudioSettings.dspTime`** — source: ADR-D8
- **`BeatClockPauseEventData.ResumeFirstBeatDspTimeRaw` (resume only) is the offset-free first-beat raw DSP (Core Rule 13); pause sentinel `-1.0`** — source: ADR-D8

**Rhythm judgment (C1, ADR-D3)**
- **C1 must add F3's raw `inputDspTime` back to `latency_offset_s` before comparing to `beat_dsp_time`** (F2 Core Rule 7) — source: ADR-D3
- **C1 may only begin evaluating after `BeatClock.IsStartConfirmed == true`** (F2 Core Rule 12) — source: ADR-D3
- **Attack animations must be triggered via `AnimancerAttackPlayer.Play(attackType, targetDspTime)`** — source: ADR-D3
- **F3 sentinel contract: `chargeStartDspTime == -1.0` is the sentinel; C1 must test `> -1.0`, not `> 0`** — source: ADR-D3
- **Pause events must immediately clear the input buffer to prevent ghost judgment after Resume** — source: ADR-D3
- **`RhythmJudger` must bind `BeatClock.OnPaused += OnBeatClockPaused` directly in `Start()`, never poll pause state in `Update()`** (eliminates Script Execution Order race) — source: ADR-D3
- **No attack on a beat produces no Miss (no attack = no event)** — source: ADR-D3
- **`Start()` asserts `GoodHalfWindowMs < halfBeatDurationMs / 2` and `PerfectHalfWindowMs < GoodHalfWindowMs`; Android clamps `PerfectHalfWindowMs ≥ 80ms`** — source: ADR-D3
- **Property injection instead of constructor injection** (`IBeatClock`/`IInputEventBus`/`IAnimancerAttackPlayer`/`RhythmConfigSO` via public setters — MonoBehaviour cannot use constructor injection) — source: ADR-D3

**Combo & random (ADR-D8)**
- **`IComboCounter` is C1's read-only face (`CurrentCombo`, `ComboMax = 999`); C2 reads only, never writes back** — source: ADR-D8
- **`IRandomSource` minimal sufficient method set: `Next(int maxExclusive)`, `NextDouble()`, `Reseed(int seed)`, `Seed { get; }` — deliberately excludes `Next(min,max)`/`NextFloat()`/`NextBool()` to avoid interface bloat** — source: ADR-D8
- **`IDSPClock`'s canonical single method name is `GetDspTime()`, replacing `GetCurrentTime()`/`GetCurrentDSPTime()`** — source: ADR-D8

**Pause/resume + run-end cleanup**
- **Canonical pause/resume source: F2 (BeatClock) is the sole publisher; `OnBeatClockPaused`/`OnBeatClockResumed` are the only signal source; subscribers are locked to C1 (hard — re-anchor beat grid + clear stale `_pendingInput` on resume) / C3 (soft — freeze/resume `PerkModifierStack`) / F6 (SC-1 audio-degrade recovery)** — source: ADR-D8
- **Paired invariant (AC-F2-28): exactly one each per pause→resume cycle; `OnBeatClockPaused` (`isResume=false`) precedes the paired `OnBeatClockResumed` (`isResume=true`)** — source: ADR-D8
- **Zero-GC broadcast: beat events must use `OnBeatExact?.Invoke(data)`; `GetInvocationList()` is banned (heap-allocs `Delegate[]` per call). Subscribe once in `OnEnable`/`Awake`; no per-frame lambda subscribe/unsubscribe** — source: ADR-D8
- **C1 SC-2: on the death/run-end boundary (re-subscribed to `OnRunEnded`), C1 clears `_pendingInput` / `_lateWindowActive` (E10 ghost-judgment guard); idempotent safety net** — source: ADR-D12 / ADR-D13
- **C4 SC-3: on the death/run-end boundary (re-subscribed to `OnRunEnded`, both Death and Victory), C4 cancels all outstanding `Pending`/`Granted` attack-slot tokens in the same frame — `Stop()` zeroes `global_beat_index`, orphaning absolute-beat tokens** — source: ADR-D12 / ADR-D13
- **All death/run-end cleanup (C1 clear, C4 token cancellation) must occur in the same frame as `BeatClock.Stop()`** — source: ADR-D12 / ADR-D13
- **Boundary isolation: CombatCore (C1/C4) is forbidden to `using TopDownEngine`; it only subscribes to F6-bridged pure C# domain events whose payload contains no TDE types** — source: ADR-D12 / ADR-D13

**AnimationClip loading (C2, ADR-D4)**
- **Rule D4-4: `AttackNodeSO.animationClip: AssetReferenceT<AnimationClip>` load lifecycle is owned by C2 — C2 calls `LoadAssetAsync<AnimationClip>()` for all `AttackNodeSO.animationClip` of the current `ComboGraphSO` at room load, then passes the loaded clip to `AnimancerAttackPlayer.Play(loadedClip)`; C2 calls `Addressables.Release(handle)` on room exit; wrap `LoadAssetAsync` in try/catch (Unity 6.2+ throws on load failure)** — source: ADR-D4

### Forbidden Approaches
- **C1 must never call `BeatClock.Play()`/`Stop()`/`Pause()`/`Resume()`** (`beatclock_lifecycle_bypass`) — source: ADR-D3
- **Never use input-anchored evaluation** (evaluate immediately on input arrival against `nearest_half_beat_dsp_time`) — that points to a *past* beat, so an input 5ms before the next beat scores Miss even though timed correctly; breaks the "beat = reward" pillar — source: ADR-D3
- **Never make `RhythmJudger` a `DontDestroyOnLoad` singleton** — judgment is player-centric (combo count, attack state); a singleton would need cross-scene combo-state sync — source: ADR-D3
- **Never add any code path that persists `_pendingInput` in `QuantizationMode.Off`** — Off mode must consume input immediately (`EmitAndClear(0)`); `_pendingInput` never persists — source: ADR-D3
- **Never let the `IComboCounter` consumer (C2) write back `CurrentCombo`** — owned & broadcast by C1 (registry `rhythm_judgment_combo`) — source: ADR-D8
- **Never use `GetInvocationList()` for beat-event broadcast** — heap-allocates `Delegate[]` per call, breaking zero-GC — source: ADR-D8
- **Never use a captured-local lambda for per-frame dynamic beat-event subscribe/unsubscribe** — closure allocation + repeat-subscription leak — source: ADR-D8
- **Never let TDE in-place respawn continue the same run after death** — bypasses run settlement, hollowing out the only permanent progression sink (merit) — source: ADR-D12
- **Never maintain two divergent (death vs. victory) cleanup implementations in C1/C4** (Model B, rejected) — source: ADR-D13

### Performance Guardrails
- **RhythmJudger CPU**: ~0.02ms/half-beat (one nullable compare + one event dispatch) — source: ADR-D3
- **RhythmJudger memory**: `AttackInputEvent?` nullable struct field ~80 bytes — source: ADR-D3
- **RhythmJudger GC**: zero-GC hot path — `RhythmJudgmentEvent` is a `readonly struct`, all value-type fields, no boxing — source: ADR-D3
- **Interface calls (D8)**: +0ms/frame (devirtualized; events already exist); payloads are `readonly struct` (stack, zero heap); does not change the 0.2ms beat-clock budget — source: ADR-D8

### Engine API Constraints
- **Platform minimum Perfect half-window: PC/Console ≥ 16.7ms; iOS ≥ 39ms; Android ≥ 80ms** (`Start()` injects platform constants, not exposed in the SO) — source: ADR-D3
- **`[SerializeField]` must not be used on `IBeatClock`/`IInputEventBus`/`IAnimancerAttackPlayer`/`RhythmConfigSO` properties** — Unity 6.3 LTS supports field-level serialization only — source: ADR-D3
- **`Time.realtimeSinceStartup` introduces up to 33ms LateWindow-expiry error at 30fps mobile** — do not use it as a rhythm clock — source: ADR-D3
- **No post-cutoff Unity APIs in the Core ADRs** — standard C# events + `interface` + `System.Random` (or equivalent PRNG) + MonoBehaviour lifecycle — source: ADR-D3 / ADR-D8

---

## Feature Layer Rules

*Applies to: enemy AI, Perk system, secondary SO grouping, secondary mechanics*

### Required Patterns

**Enemy AI (ADR-D2)**
- **Enemy prefabs do NOT include `AIBrain`/`AIAction`/`AIDecision` components — `BehaviorGraphAgent` (Unity Behavior) is the sole high-level AI driver** — source: ADR-D2
- **All enemy attack decisions must go through CombatDirector (C4) beat-slot token requests — AI must NOT call `BeatClock` directly; beat state arrives only via the Blackboard written by `BeatClockBridge`** — source: ADR-D2
- **AI must not modify TDE source code; integration via subclassing and event subscription only** — source: ADR-D2
- **`BeatClockBridge` must have no `Update()` — it writes to the Blackboard synchronously inside `BeatClock`'s `OnBeatSnapshot` dispatch (event push, not per-frame tick)** — source: ADR-D2
- **AI decision nodes must not assume sub-beat freshness of `CurrentBeat`/`TimeToNextBeat`** (bounded staleness ≤ one beat interval) — source: ADR-D2
- **All custom Unity Behavior Action nodes must be `partial class` with `[Serializable, GeneratePropertyBag]` and a unique `[NodeDescription(id: "...")]` GUID**, or the source generator fails — source: ADR-D2
- **Damage receipt only via TDE `Health.Damage()` — never a direct field write** — source: ADR-D2
- **Death/respawn lifecycle is owned by TDE `Health` + `TDELifecycleAdapter`; `BehaviorGraphAgent` listens via `End()`/`Restart()` but does not control lifecycle** — source: ADR-D2

**Perk & SO grouping (ADR-D4)**
- **Rule D4-2 (PerkSO is the only mutable pattern): `PerkSO` is the only one of 7 SO classes needing runtime modification. C3 clones via `Instantiate(originalPerkSO)`; the clone is valid for the run and destroyed by C3 at run end; `PlayerInfoSO.unlockedPerkIds` holds original-asset refs, never clones** — source: ADR-D4
- **Rule D4-6 (AttackNodeSO co-bundle): `AttackNodeSO` assets must share the same Addressable Group as their `ComboGraphSO` to avoid bundle duplication / implicit cross-bundle deps** — source: ADR-D4
- **Rule D4-9 (PerkParameter value-type invariant): all `PerkParameter` fields must be value types or string (`float`/`int`/`enum`/`string`). `Instantiate(perkSO)` deep-copies serialized data but shallow-copies non-serialized reference-type fields — a clone would share the reference and its edits would pollute the original asset** — source: ADR-D4
- **`authoredBpm` field validation: `[MinValue(1f)]` (non-zero positive; zero → divide-by-zero) and `[MaxValue(240f)]`** — source: ADR-D5

### Forbidden Approaches
- **Never use Behavior Designer (Opsive)** — adds a 9th third-party dependency; no M0 Spike verification; unknown `[SerializeField]` fields-only breaking-change risk under Unity 6.3; no first-party event-channel equivalent — source: ADR-D2
- **Never build a custom C# behavior tree** — Unity Behavior is available at no cost; a custom BT is redundant for MVP scope (1 enemy type) — source: ADR-D2
- **Never use TDE's built-in `AIBrain` (`AIAction`/`AIDecision`/`AIState`) for enemy AI** — it is a flat state machine; cannot cleanly implement the async blocking `Running`/`Success`/`Failure` token-wait needed for beat-locked attacks, has no Blackboard for beat-state injection, incompatible with Boss phase transitions — source: ADR-D2
- **Never read `BeatClock` directly from AI** — beat state must arrive only via the Blackboard written by `BeatClockBridge` — source: ADR-D2
- **Never add any C# reference-type field to `PerkParameter`** (class, array object, interface ref) — source: ADR-D4
- **Never use a Unity-native Animator Layer + Avatar Mask for the attack layer** — requires editing TDE's Animator Controller asset (violates "no TDE source mod" spirit); `AnimatorOverrideController` allocates on every clip switch — source: ADR-D5
- **Never use the manual PlayableGraph API approach** — verbose and error-prone; Animancer 8.4 is a mature wrapper — source: ADR-D5
- **Never introduce an `IAnimationPlayer` decoupling adapter around Animancer** — YAGNI; Animancer is a committed library and `AnimancerAttackPlayer` is the single integration point — source: ADR-D5

### Performance Guardrails
- **Unity Behavior BT evaluation**: O(active nodes)/tick; for MVP (≤5 concurrent enemies) < 0.5ms/frame — source: ADR-D2
- **`BeatClockBridge.HandleBeatSnapshot()`**: fires at most 4–8×/second per enemy; negligible — source: ADR-D2
- **`BehaviorGraphAgent` memory**: a unique graph copy per enemy via `BehaviorGraph.AcquireInstance()`; ~50–80KB/enemy at runtime — source: ADR-D2
- **SO memory (F4)**: 7 SO main-data classes are lightweight; `AssetReferenceT<AnimationClip>` occupies memory only after C2 preload, released on room exit — source: ADR-D4

### Engine API Constraints
- **`com.unity.behavior` v1.0.16** (Unity first-party) — M0 Spike (2026-07-18) verified all key APIs, zero BLOCKERs — source: ADR-D2
- **`BehaviorGraphAgent.SetVariableValue<T>()`** verified at `.../BehaviorGraphAgent.cs:541` — source: ADR-D2
- **`CharacterMovement.SetMovement(Vector2)`** verified TDE 4.5 at `CharacterAbilities/CharacterMovement.cs:200` — source: ADR-D2
- **`BehaviorGraphAgent` runs at `[DefaultExecutionOrder(-50)]`** (hardcoded, not configurable); `BeatClockBridge` has no `Update()`, so the attribute does not affect its timing — source: ADR-D2
- **SO asset ID naming**: `AttackNodeSO`: `{weaponType}_{attackType}_{index}`; `ComboGraphSO`: `{weaponType}_comboGraph`; `PerkSO`: `perk_{effectKeyword}`; `WeaponSO`: `weapon_{weaponType}`; `EnemyInfoSO`: `enemy_{role}`; `LevelConfigSO`: `level_{areaId}_{roomId}` — source: ADR-D4

---

## Presentation Layer Rules

*Applies to: rendering, audio (FMOD), UI, VFX, shaders, animation (Animancer)*

### Required Patterns

**Animation pipeline (Animancer, ADR-D5)**
- **Core architecture: use `HybridAnimancerComponent` (NOT plain `AnimancerComponent`)** — Animancer 8.4 primary API for coexisting with an existing Animator Controller: wrap the `RuntimeAnimatorController` in a `ControllerState`; set `KeepChildrenConnected = true` so `AnimatorControllerPlayable` is not disconnected when Animancer weight drops to zero; expose `PlayController()` to hand control back — source: ADR-D5
- **When no attack is playing, the `ControllerState` is active and TDE's Animator Controller fully drives the character (locomotion, dash, death, respawn)** — source: ADR-D5
- **BPM speed-scale formula: `state.Speed = beatClock.CurrentBpm / attackNode.AuthoredBpm`. Speed is set once at attack start; in MVP it is not updated mid-attack (BPM fixed per level via `LevelConfigSO`). Post-MVP Boss-phase BPM change calls `AnimancerAttackPlayer.UpdateBpm()` to update `_currentState.Speed` without interrupting the animation or resetting normalized time** — source: ADR-D5
- **Death interrupts attack safely: `AnimancerAttackPlayer` subscribes to `TopDownEngineEvent(PlayerDeath)` and, in the same frame, calls `StopAttack()` — clears `_pendingOnComplete` (suppress onComplete so C2 does not advance combo on death) and calls `_animancer.PlayController()` to return control to the TDE Animator Controller so the TDE death animation plays immediately; C2 treats a missing onComplete as a death/cancel signal** — source: ADR-D5
- **Same-frame race protection (C2 obligation): if combo input arrives on the same frame the clip ends, `OnEnd` and the input callback both fire that frame — C2's combo-advance path must be idempotent (a same-frame double-advance must equal a single advance)** — source: ADR-D5
- **`OnEnable`/`OnDisable` subscription pairing — no lingering listeners on pooled/destroyed characters (TDE's `MMEventManager` holds strong refs)** — source: ADR-D5
- **The `OnEnd` delegate must be cached in `Awake()` (`_onAnimationEnd = OnAnimationEnd`) and reused per attack — zero allocation (IL2CPP-compliant)** — source: ADR-D5
- **Do not hard-code attack animation frame counts (tech-preferences cross-ref) — drive via `AnimancerState.Speed`** — source: ADR-D5

**Asset references & audio playback**
- **Rule D4-3 (large Unity Object refs must use `AssetReferenceT<T>`): `AnimationClip`, `Sprite`, etc. must use `AssetReferenceT<T>` (Addressables lazy ref), not direct refs (a direct ref locks the asset into the same bundle, triggering an implicit full pull — one `ComboGraphSO` could implicitly pull 20 `AnimationClip`s). Exception: `List<AttackNodeSO>` (small metadata SOs) keep direct refs in `ComboGraphSO`** — source: ADR-D4
- **BGM plays via FMOD Core API (`FMOD.Sound` + `FMOD.Channel.setDelay()`) for sample-accurate DSP scheduling; `FMOD.Studio.EventInstance` is used only for SFX and ambient; BGM is NOT authored as an FMOD Studio bank event in MVP** — source: ADR-D6
- **`BeatClock.Stop()` at the run boundary is an Audio-domain concern — it must fire at the single death/run-end boundary, not scattered across audio-triggering code** — source: ADR-D12 / ADR-D13
- **FE5 settlement UI subscribes to `OnRunEnded(outcome)` to drive merit conversion + persistence + settlement UI, branching UI/economy by `outcome` (Death vs Victory)** — source: ADR-D12 / ADR-D13
- **Victory vs. death merit/UI differences must be explicit design, not default symmetry — if there is no difference, document it as an intentional decision (game-designer sign-off)** — source: ADR-D13

### Forbidden Approaches
- **Never let `AnimancerAttackPlayer` interfere with TDE Animator parameters** — TDE writes float/bool/trigger params to drive locomotion; keep `AnimatorControllerPlayable` resident via `ControllerState` + `KeepChildrenConnected=true`; never modify TDE's Animator Controller asset — source: ADR-D5
- **Never return control with `Stop()` instead of `PlayController()`** — `Stop()` pauses the entire PlayableGraph (including the controller); `PlayController()` is the correct hand-back API — source: ADR-D5
- **Never allocate GC during attack playback** — `AnimancerState.Speed` assignment and `OnEnd` callback must not allocate per frame (IL2CPP-compliant) — source: ADR-D5
- **Never assume the Animancer layer's own Speed stays 1.0 without coordination** — a future global slow-motion system setting layer speed ≠ 1.0 makes effective speed `state.Speed × layer.Speed`, desyncing BPM — source: ADR-D5
- **Never author BGM as an FMOD Studio bank event (MVP)** — source: ADR-D6
- **Never use `AudioSettings.outputSampleRate` as the DSP-clock divisor** (`unity_samplerate_in_fmod_dsp_clock` ban) — use `CoreSystem.getSoftwareFormat(out int sampleRate, …)` — source: ADR-D6 / ADR-D8
- **Never read `AudioSettings.dspTime` downstream** — use `BeatEventData.BeatDspTime` (player-perceived time domain) — source: ADR-D8

### Performance Guardrails
- **`AnimancerState.Speed` assignment**: single float write < 0.01ms; `_animancer.Play(clip)` (`HybridAnimancerComponent`, `KeepChildrenConnected=true`) is O(1); `PlayController()` single weight transfer ~0.01ms — source: ADR-D5
- **Animation load**: `HybridAnimancerComponent.Awake()` (PlayableGraph creation + `ControllerState` wrap) < 0.5ms; does not affect scene load — source: ADR-D5
- **FMOD processing**: runs on FMOD's internal audio thread — no main-thread / render-budget impact — source: ADR-D6

### Engine API Constraints
- **`HybridAnimancerComponent`** (Animancer 8.4.0) — verified: `KeepChildrenConnected=true` prevents `AnimatorControllerPlayable` disconnect; TDE locomotion unaffected during/after Animancer attack; `PlayController()` hand-back confirmed — source: ADR-D5
- **`AnimancerState.Speed`** (from `AnimancerNodeBase`) — verified working; practical range `[0.33, 4.0]`; `CurrentBpm`/`AuthoredBpm` range `[60, 240]` — source: ADR-D5
- **`AnimancerState.Events.OnEnd`** — verified to fire reliably at `state.Speed > 1.0` (threshold-based, no skipped-frame, verified 2026-07-18) — source: ADR-D5
- **`PlayController()` is the correct hand-back API (not `Stop()`)** — source: ADR-D5
- **`AssetReferenceT<T>`** (Addressables 2.x, stable, Unity 6.3 verified); Addressables load failure now throws (Unity 6.2+) — wrap all `LoadAssetAsync` in try/catch — source: ADR-D4

---

## Global Rules (All Layers)

### Naming Conventions
| Element | Convention | Example |
|---------|-----------|---------|
| Classes | PascalCase | `PlayerController` |
| Public fields / properties | PascalCase | `MoveSpeed` |
| Private fields | _camelCase | `_moveSpeed` |
| Methods | PascalCase | `TakeDamage()` |
| Files | PascalCase matching class name | `PlayerController.cs` |
| Scenes / Prefabs | PascalCase matching root node | `PlayerController.prefab` |
| Constants | PascalCase or UPPER_SNAKE_CASE | `MaxHealth` / `MAX_HEALTH` |

### Performance Budgets
| Target | Value |
|--------|-------|
| Framerate | 60fps (PC / Console), 30fps (Mobile) |
| Frame budget | 16.6ms (PC / Console), 33ms (Mobile) |
| Draw calls | ≤150 (PC / Console), ≤100 (Mobile) |
| Memory ceiling | ≤2GB (PC / Console), ≤512MB (Mobile) |

### Approved Libraries / Addons
- **TopDownEngine** (More Mountains) — movement, health, LevelManager, MMFeedbacks/Feel infrastructure; F6 integration base. **Boundary**: TDE owns movement/health/level/feedback; CombatCore owns rhythm quantization + combo logic. Extend by inheritance & events only.
- **Animancer** (Kybernetik) — runtime animation state management for the BPM-adaptive attack layer; replaces Animator Controller state-machine logic for the attack layer only (TDE's Animator layers stay unchanged).
- **Odin Inspector** (Sirenix) — **Editor-assembly only**; Runtime code must never reference `Sirenix.*`. SO validation attributes (`[Required]`, `[ValidateInput]`) work in Editor only.
- **Odin Validator** (Sirenix) — project asset compliance validation; **Editor-only**, never trigger validation in a Runtime code path.
- **Feel / MMFeedbacks** (More Mountains) — feedback infrastructure; all Perfect/Good/Miss feedback via MMFeedbacks (CombatCore publishes rating events, Feel subscribes). Use via event subscription only; do not `using` Feel in CombatCore. Feel 3.x uses the `Feedback` abstract class (not the deprecated `MMFeedback` base).
- **EasySave 3** (Moodkie) — save/persistence (F5); serialize pure data DTOs only, never MonoBehaviour refs. ES3Cloud disabled.
- **DOTween** (Demigiant, Pro) — UI transition tweens only (non-rhythm-critical). Call `DOTween.Init()` with a set capacity at startup; do not create new tweens every frame in Update/FixedUpdate; do not double-drive a Transform already driven by Feel.
- **Hot Reload** (CodeFloof) — Editor dev-time only; excluded from all builds; reset static fields manually after a reload.

### Forbidden Patterns (technical-preferences.md)
- **Never modify TDE source** — TDE upgrades overwrite edits (v1 left 582 lines of override traps). Inherit + override, or extend via TDE's event system. Overriding a TDE internal class requires an ADR.
- **Never reference the Odin namespace (`Sirenix.*`) in Runtime code** — Odin is Editor-only; a Runtime reference breaks builds / bloats binaries. Use inside `#if UNITY_EDITOR` blocks or `Editor/` directories.
- **Never use `Update()` polling for beat/timing judgment** — `Update()` timestamp error reaches 16.7ms at 60FPS, exceeding the target judgment window. Use the Input System's `InputAction.performed` callback's precise timestamp.
- **Never hard-code attack animation frame counts** — fixed frames cannot rescale with BPM. Scale animation duration via Animancer's `AnimancerState.Speed`.

### Forbidden APIs (Unity 6.3 LTS)
> Deprecated or unverified-post-cutoff. Source: `docs/engine-reference/unity/deprecated-apis.md`.

**Input** (use the New Input System)
- `Input.GetKey()` / `Input.GetKeyDown()` — use `Keyboard.current[Key.X].isPressed` / `.wasPressedThisFrame`
- `Input.GetMouseButton()` — use `Mouse.current.leftButton.isPressed`
- `Input.GetAxis()` — use `InputAction` callbacks
- `Input.mousePosition` — use `Mouse.current.position.ReadValue()`

**UI** (UI Toolkit preferred for new UI)
- `Canvas` (UGUI) / `Text` / `Image` components — legacy; prefer `UIDocument` / `Label` / `VisualElement`

**DOTS / Entities** (Entities 1.0+ rewrite)
- `ComponentSystem` / `JobComponentSystem` — use unmanaged `ISystem` + `IJobEntity`
- `GameObjectEntity` — use pure ECS workflow
- `EntityManager.CreateEntity()` (old signature) — use the explicit-archetype overload
- `ComponentDataFromEntity<T>` — use `ComponentLookup<T>`

**Rendering** (SRP / RenderGraph)
- `CommandBuffer.DrawMesh()` — use the RenderGraph API
- `OnPreRender()` / `OnPostRender()` — use `RenderPipelineManager` callbacks
- `Camera.SetReplacementShader()` — use a custom render pass

**Physics**
- `Physics.RaycastAll()` — GC allocs; use `Physics.RaycastNonAlloc()`
- `Rigidbody.velocity` (direct write) — use `Rigidbody.AddForce()`
- `Physics.autoSyncTransforms` — deprecated in 6.3; use explicit `Physics.SyncTransforms()`

**Asset Loading**
- `Resources.Load()` / synchronous asset loading — use `Addressables.LoadAssetAsync()`

**Animation** (legacy)
- `Animation` component / `Animation.Play()` — use Animator Controller / `Animator.Play()`

**Particles**
- Legacy Particle System — use Visual Effect Graph

**Scripting / Platform**
- `WWW` — use `UnityWebRequest`
- `Application.LoadLevel()` — use `SceneManager.LoadScene()`
- WebGL 1.0 target — use WebGL 2.0 / WebGPU

**Unity 6.3-specific deprecations**
- `AccessibilityNode.selected` — use `.invoked`
- `PlayerSettings.Android.androidIsGame` — use the App Category player setting
- Round / legacy Android icons — use Adaptive Icons only
- Netcode for GameObjects 1.x — migrate to 2.x
- Multiplayer Widgets package — use Unity Building Blocks
- Facebook Instant Games platform — use the Web build target
- Unity Magic Leap XR Plugin — discontinued

### Cross-Cutting Constraints
- **BeatClock lifecycle (`Play`/`Stop`/`Pause`/`Resume`) has exactly one owner: F6 `TDELifecycleAdapter`.** Every other system is a read-only consumer (subscribe to events / read snapshots). Registry forbidden pattern: `beatclock_lifecycle_bypass`.
- **CombatCore assembly must not `using TopDownEngine`.** It only subscribes to F6-bridged pure C# domain events whose payload carries no TDE types.
- **F2 (BeatClock) is the sole publisher of pause/resume events** (`OnBeatClockPaused`/`OnBeatClockResumed`). No system may define parallel `OnGamePaused`/`OnGameResumed` events. Registry: `f2_self_load_latency` (F2 must not self-load latency from F5).
- **All timing judgment reads the player-perceived time domain** (`BeatEventData.BeatDspTime`, offset-included). Never read `AudioSettings.dspTime`; never use `AudioSettings.outputSampleRate` as the DSP divisor.
- **Single run-end exit** `EndRun(RunOutcome)` with a one-time latch; death and victory funnel through the same F6 boundary (`Stop()` → `DisableAllInput()` → `OnRunEnded(outcome)`), same frame. Registry: `f6_respawncomplete_inplace_restart_mvp` (in-place respawn disabled in MVP).
- **`[SerializeField]` on fields only** (Unity 6.3 LTS) — lint-enforced; never on properties.
- **Zero-GC hot paths** (beat broadcast, judgment, attack playback): `?.Invoke(data)` not `GetInvocationList()`; cached delegates, no per-frame closures/boxing (IL2CPP-compliant).
- **Verify TDE / plugin event & member names against the engine reference** before use — never guess from training data (`.Respawn` → `.RespawnComplete` lesson).
