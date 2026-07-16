# ADR-D1: TopDownEngine Integration Architecture

## Status
Proposed

## Date
2026-07-15

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (6000.3.19f1) |
| **Domain** | Core / Integration |
| **Knowledge Risk** | HIGH — TDE 4.x + Unity 6.3 LTS new Input System is post-LLM-cutoff; TDE version compatibility unverified at ADR authoring time |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `docs/engine-reference/unity/plugins/top-down-engine.md`, `docs/architecture/adr-d6-audio-middleware.md` |
| **Post-Cutoff APIs Used** | `MMEventListener<T>` (TDE event subscription); `TopDownEngineEvent` / `MMGameEvent` (TDE event types); `InputSystemManagerEventsBased` (TDE input manager — subclassed, not modified) |
| **Verification Required** | 1. Confirm TDE 4.x package compiles in Unity 6.3 LTS (HIGH — `[SerializeField]` fields-only change may break TDE editor scripts). 2. Verify `MMEventListener<T>` generic subscription API signatures in TDE 4.x (`MMEventStartListening<T>()` / `MMEventStopListening<T>()`). 3. Verify `MMGameEvent.EventName` string values for pause: are they `"Pause"` / `"UnPause"` or a different convention? 4. Verify `TopDownEngineEventTypes.PlayerDeath` and `.Respawn` enum members exist in TDE 4.x. 5. Verify `InputSystemManagerEventsBased` has virtual/overridable methods for Action Map Enable/Disable that can be redirected to F3 without breaking `AbilityInputAuthorization`. 6. Confirm FMOD for Unity RuntimeManager script execution order vs TDELifecycleAdapter. |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-D6 (FMOD Studio 2.x — Accepted; F6 must ensure FMOD RuntimeManager is initialized before calling BeatClock.Play()) |
| **Enables** | ADR-D5 (Animation Pipeline — depends on TDE Animator layer boundary defined here); ADR-D2 (Enemy AI Framework — depends on F6 lifecycle hooks) |
| **Blocks** | F6 GDD authoring — interface contracts must reference this ADR's defined API |
| **Ordering Note** | F6 GDD authored after this ADR is Accepted. ADR-D5 can begin in parallel once TDE Animator layer boundary is confirmed. |

## Context

### Problem Statement

TopDownEngine (TDE) 4.x provides built-in input management via `InputSystemManagerEventsBased`, which autonomously calls `InputActionMap.Enable()` and `Disable()` during character lifecycle events (death, respawn, pause). This conflicts with F3's `InputEventBus` state machine, which assumes exclusive control over Action Map state.

Without a defined integration contract:
- TDE lifecycle events can trigger Action Map state changes that bypass F3's `_isSwitchingMaps` guard, causing the E3 edge case (ghost `LightAttack` on charged input interrupted by death) to fire
- BeatClock (F2) has no defined controller for `Play()` / `Pause()` / `Resume()` / `Stop()` — TDE pause events have no path to F2
- `LevelConfigSO.bpm` and `.timeSignature` (F4) have no injection pathway into `BeatClock.Play()` at scene load
- F3 GDD (E6) requires FMOD to run in async mode (`FMOD_INIT_NORMAL`), but no component enforces this constraint

F6 (the TDE Integration Layer) must mediate between TDE's lifecycle and the Foundation systems (F2, F3, F4, F5).

### Constraints

- **TDE source modification is FORBIDDEN** (per `technical-preferences.md`). All integration via inheritance, event subscription, or prefab modification only.
- **TDE's `AbilityInputAuthorization` must remain functional** — removing `InputSystemManagerEventsBased` entirely would break ability gating. Must subclass instead.
- FMOD must be initialized before `BeatClock.Play()` is called.
- F3 must remain `AllDisabled` until scene load completes and BeatClock is active.
- `BeatClock.Pause()` and `F3.EnableUIInput()` must fire atomically in the same frame.

### Requirements

- Define a single Action Map authority that eliminates F3 ↔ TDE race condition (TR-input-010)
- Define boot sequence: `AllDisabled` → `GameplayActive` after scene load (TR-input-014)
- Provide pause/resume signal path from TDE → BeatClock (TR-beatclock-008)
- Establish LevelConfigSO → BeatClock injection via F6 (TR-datalayer-010)
- Enforce FMOD async init mode at boot (TR-input-011)
- Enable F6 GDD authoring with defined component structure (TR-beatclock-012)
- Preserve TDE `AbilityInputAuthorization` functionality

## Decision

F6 consists of **two components** working in concert:

1. **`JadeRabbitInputManager : InputSystemManagerEventsBased`** — Subclass of TDE's input manager that **replaces** (not removes) `InputSystemManagerEventsBased` on character prefabs. Overrides Action Map Enable/Disable methods to delegate exclusively to F3's `InputEventBus`, while preserving TDE's `AbilityInputAuthorization` pipeline through inheritance.

2. **`TDELifecycleAdapter : MonoBehaviour`** — Independent adapter component on the Manager scene's `DontDestroyOnLoad` object. Subscribes to TDE lifecycle events and coordinates BeatClock (F2) + InputEventBus (F3) state transitions.

### Action Map Authority

**F3 (`InputEventBus`) is the sole authority over all Action Map state changes.** `JadeRabbitInputManager` intercepts TDE's internal calls to `InputActionMap.Enable()` / `Disable()` via method override and redirects them through F3's public API (`EnableGameplayInput()`, `EnableUIInput()`, `DisableAllInput()`). This ensures:

- F3's `_isSwitchingMaps` guard is always set before any `gameplayMap.Disable()` call (E3 protection)
- TDE's `AbilityInputAuthorization` continues to function because the subclass preserves the authorization pipeline
- No race condition: F3's state machine is the single source of truth

> **M0 Spike Required**: The specific TDE 4.x methods to override in `JadeRabbitInputManager` depend on TDE source review. If `InputSystemManagerEventsBased` does not expose virtual/overridable methods for Action Map management, the fallback is: keep the component but disable its auto-management via TDE configuration API (e.g., `HandleInput = false`), and have `TDELifecycleAdapter` call F3 directly. This fallback must be verified during M0 Spike.

### BeatClock Lifecycle Authority

`TDELifecycleAdapter` is the **exclusive controller** of `BeatClock.Play()`, `.Pause()`, `.Resume()`, and `.Stop()`. No other system may call these methods. BeatClock internally handles the 3-step BGM play protocol (optimistic pre-anchor + actual start correction) as defined in F2 GDD and ADR-D6.

### LevelConfigSO Injection

`TDELifecycleAdapter` holds an Inspector reference to the scene's `LevelConfigSO`. At scene load, it extracts `.Bpm`, `.TimeSignature`, and `.BgmAssetReference`, and passes them to `BeatClock.Play()`.

### FMOD Init Constraint

`TDELifecycleAdapter.Start()` (not `Awake()` — to avoid Script Execution Order race with `FMODUnity.RuntimeManager`) verifies that FMOD RuntimeManager is initialized and running in async mode. If this check fails, the adapter logs a fatal error and does not proceed — the scene remains in a safe frozen state.

### Boot Sequence (per scene load)

```
SceneManager.sceneLoaded (LoadSceneMode.Single only — additive loads are filtered)
    └→ TDELifecycleAdapter.OnSceneLoaded()
         1. Guard: skip if LoadSceneMode != Single (TDE room additive loads)
         2. Assert FMODUnity.RuntimeManager is initialized (deferred from Start())
         3. Read LevelConfigSO: .Bpm, .TimeSignature, .BgmAssetReference
         4. BeatClock.Play(bgmRef, bpm, timeSig)  ← F2 runs 3-step protocol internally
         5. InputEventBus.EnableGameplayInput()     ← F3: AllDisabled → GameplayActive
```

### Pause / Resume

```
TDE MMGameEvent("Pause")   ──→  TDELifecycleAdapter
    └→ BeatClock.Pause()              ← F2: Playing → Paused  (same frame)
       InputEventBus.EnableUIInput()  ← F3: GameplayActive → UIActive (same frame)

TDE MMGameEvent("UnPause") ──→  TDELifecycleAdapter
    └→ BeatClock.Resume()                  ← F2: Paused → Playing  (same frame)
       InputEventBus.EnableGameplayInput() ← F3: UIActive → GameplayActive (same frame)
```

> **⚠ TDE event name verification required**: `"Pause"` / `"UnPause"` are assumed string values. TDE 4.x may use different strings, enum values, or a different event type entirely. This must be verified from TDE 4.x source during M0 Spike.

### Death / Respawn

```
TDE TopDownEngineEvent(PlayerDeath) ──→  TDELifecycleAdapter
    └→ BeatClock.Stop()                ← F2: → Stopped
       InputEventBus.DisableAllInput() ← F3: → AllDisabled

TDE TopDownEngineEvent(Respawn) ──→  TDELifecycleAdapter
    └→ BeatClock.Play(bgmRef, bpm, timeSig) ← F2: → Playing (restart)
       InputEventBus.EnableGameplayInput()   ← F3: → GameplayActive
```

### Architecture Diagram

```
[Unity SceneManager.sceneLoaded]
          │ (LoadSceneMode.Single only)
          ▼
[F6: TDELifecycleAdapter : MonoBehaviour]  ── DontDestroyOnLoad Manager
  ┌───────┴───────────────────────────────────────────────────────┐
  │  implements MMEventListener<MMGameEvent>                      │
  │  implements MMEventListener<TopDownEngineEvent>               │
  │                                                               │
  │  [Inspector refs]                                             │
  │    _inputEventBus : InputEventBus      (F3 singleton)        │
  │    _beatClock     : BeatClock          (F2 singleton)        │
  │    _levelConfig   : LevelConfigSO      (F4 per-scene)       │
  └───────────────────────────────────────────────────────────────┘
          │
          ├── OnSceneLoaded ────────► BeatClock.Play(bpm, timeSig, bgmRef)
          │                           InputEventBus.EnableGameplayInput()
          │
          ├── MMGameEvent("Pause") ──► BeatClock.Pause()
          │                            InputEventBus.EnableUIInput()
          │
          ├── MMGameEvent("UnPause")─► BeatClock.Resume()
          │                            InputEventBus.EnableGameplayInput()
          │
          ├── TopDownEngineEvent     ► BeatClock.Stop()
          │   (PlayerDeath)            InputEventBus.DisableAllInput()
          │
          └── TopDownEngineEvent     ► BeatClock.Play(bgmRef, bpm, timeSig)
              (Respawn)                InputEventBus.EnableGameplayInput()


[Character Prefab — Modified]
  ├── Character                          (TDE — untouched)
  ├── CharacterMovement                  (TDE — untouched)
  ├── [JadeRabbitInputManager]           ← REPLACES InputSystemManagerEventsBased
  │     └── : InputSystemManagerEventsBased (inherits; preserves AbilityInputAuth)
  │           └── Action Map Enable/Disable → delegated to F3.InputEventBus
  └── CharacterAbilities                 (TDE — untouched)


[CombatCore Assembly]  (read-only consumers — never call BeatClock lifecycle methods)
  ├── C1 RhythmCore       ── subscribes to BeatClock.OnBeatExact
  └── C4 CombatDirector   ── subscribes to BeatClock.OnBeatExact
```

### Key Interfaces

```csharp
// ═══════════════════════════════════════════════════════════════
// F6 Component 1: JadeRabbitInputManager
// Location: Assets/Scripts/Core/Integration/JadeRabbitInputManager.cs
// Replaces InputSystemManagerEventsBased on Character prefabs
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Subclass of TDE's InputSystemManagerEventsBased that redirects all
/// Action Map Enable/Disable operations to F3's InputEventBus, while
/// preserving TDE's AbilityInputAuthorization pipeline through inheritance.
/// </summary>
/// <remarks>
/// M0 SPIKE REQUIRED: The specific override methods below are derived from
/// TDE 2.x training data. TDE 4.x method signatures must be verified from
/// source before implementation. If the methods are not virtual/overridable,
/// the fallback approach is to use TDE's HandleInput property.
/// </remarks>
public class JadeRabbitInputManager : InputSystemManagerEventsBased
{
    [SerializeField] private InputEventBus _inputEventBus;  // F3

    // Override: redirect Action Map enabling to F3
    // (exact method name TBD after TDE 4.x source review)
    protected override void SetInputEnabled(bool enabled)
    {
        if (enabled)
            _inputEventBus.EnableGameplayInput();
        else
            _inputEventBus.DisableAllInput();
    }
}


// ═══════════════════════════════════════════════════════════════
// F6 Component 2: TDELifecycleAdapter
// Location: Assets/Scripts/Core/Integration/TDELifecycleAdapter.cs
// Sits on DontDestroyOnLoad Manager; bridges TDE events to F2/F3
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Adapter between TDE lifecycle events (pause, death, respawn, scene load)
/// and the project's Foundation systems (BeatClock F2, InputEventBus F3).
/// Exclusive controller of BeatClock.Play/Stop/Pause/Resume.
/// </summary>
public sealed class TDELifecycleAdapter : MonoBehaviour,
    MMEventListener<MMGameEvent>,
    MMEventListener<TopDownEngineEvent>
{
    [SerializeField] private InputEventBus _inputEventBus;  // F3
    [SerializeField] private BeatClock     _beatClock;      // F2
    [SerializeField] private LevelConfigSO _levelConfig;    // F4

    private bool _fmodVerified;

    private void Start()
    {
        // FMOD async mode guard — in Start() to avoid Awake() ordering race
        // with FMODUnity.RuntimeManager (Finding #3 from engine specialist)
        _fmodVerified = FMODUnity.RuntimeManager.IsInitialized;
        if (!_fmodVerified)
        {
            Debug.LogError("[TDELifecycleAdapter] FMOD RuntimeManager not initialized. " +
                           "BeatClock will not start. Check Script Execution Order.");
        }
    }

    private void OnEnable()
    {
        this.MMEventStartListening<MMGameEvent>();
        this.MMEventStartListening<TopDownEngineEvent>();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        this.MMEventStopListening<MMGameEvent>();
        this.MMEventStopListening<TopDownEngineEvent>();
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Guard: skip additive loads (TDE room management)
        if (mode != LoadSceneMode.Single) return;
        if (!_fmodVerified) return;

        _beatClock.Play(
            _levelConfig.BgmAssetReference,
            _levelConfig.Bpm,
            _levelConfig.TimeSignature);
        _inputEventBus.EnableGameplayInput();
    }

    // ⚠ Event name strings ("Pause"/"UnPause") require M0 Spike verification
    // against TDE 4.x source — these are derived from TDE 2.x training data.
    public void OnMMEvent(MMGameEvent gameEvent)
    {
        switch (gameEvent.EventName)
        {
            case "Pause":
                _beatClock.Pause();
                _inputEventBus.EnableUIInput();
                break;
            case "UnPause":
                _beatClock.Resume();
                _inputEventBus.EnableGameplayInput();
                break;
        }
    }

    public void OnMMEvent(TopDownEngineEvent topDownEvent)
    {
        switch (topDownEvent.EventType)
        {
            case TopDownEngineEventTypes.PlayerDeath:
                _beatClock.Stop();
                _inputEventBus.DisableAllInput();
                break;
            case TopDownEngineEventTypes.Respawn:
                _beatClock.Play(
                    _levelConfig.BgmAssetReference,
                    _levelConfig.Bpm,
                    _levelConfig.TimeSignature);
                _inputEventBus.EnableGameplayInput();
                break;
        }
    }
}
```

## Alternatives Considered

### Alternative 1: Remove InputSystemManagerEventsBased Entirely
- **Description**: Remove the component from character prefabs; TDELifecycleAdapter handles all input management through F3 directly.
- **Pros**: Simplest conceptual model — one less component on the prefab.
- **Cons**: **Breaks TDE's `AbilityInputAuthorization` system**, which depends on `InputSystemManagerEventsBased` for ability gating. Character abilities would fire without authorization checks.
- **Rejection Reason**: AbilityInputAuthorization is a critical TDE subsystem; bypassing it would require reimplementing ability gating from scratch — contrary to the project's TDE adoption rationale. Subclassing preserves the authorization pipeline while redirecting Action Map control.

### Alternative 2: TDE as Action Map Authority (F3 Passive Sync)
- **Description**: Keep `InputSystemManagerEventsBased` unmodified. F3 subscribes to `InputActionMap.Enable` / `Disable` events and reactively syncs internal state.
- **Pros**: Zero TDE configuration change; TDE lifecycle handling unchanged.
- **Cons**: F3's `_isSwitchingMaps` guard (E3 edge case) must be set **before** `gameplayMap.Disable()` is called. In passive mode, F3 learns about Disable *after* the `canceled` callback has already fired, producing an unguardable ghost attack event. Testing is also harder: F3 state correctness depends on TDE event timing.
- **Rejection Reason**: Architecturally incompatible with F3's E3 ghost-attack guard. F3 must **lead** Action Map changes, not **follow** them.

### Alternative 3: Hybrid Authority (Split by Lifecycle Phase)
- **Description**: F3 manages Gameplay/UI map switches; TDE manages death/respawn via `InputSystemManagerEventsBased`.
- **Pros**: Reduces scope of TDE modification.
- **Cons**: Two authority owners create same-frame conflicts (e.g., player dies during menu transition). The `_isSwitchingMaps` guard cannot reliably wrap all `gameplayMap.Disable()` calls when TDE independently initiates some of them.
- **Rejection Reason**: Split authority is fundamentally incompatible with F3's guard pattern. Every `gameplayMap.Disable()` call must be wrapped by `_isSwitchingMaps`, which only F3 can guarantee.

## Consequences

### Positive
- F3 state machine is always the ground truth — no race conditions with TDE
- E3 ghost-attack edge case is prevented at the architecture level, not just implementation
- TDE `AbilityInputAuthorization` remains fully functional through inheritance
- BeatClock lifecycle has a single exclusive controller; consumers can safely read state
- `LevelConfigSO` injection pathway is defined: F4 → F6 → F2
- FMOD async mode is enforced at startup (loud failure vs silent DSP corruption)
- F6 GDD can now be authored with concrete component structure and interface contracts

### Negative
- `JadeRabbitInputManager` creates a coupling to TDE's `InputSystemManagerEventsBased` inheritance hierarchy. TDE API changes in future versions may require updating the subclass.
- `TDELifecycleAdapter` depends on TDE's `MMEventListener<T>` event system. If TDE changes its event API, F6 must be updated.
- Two F6 components (JadeRabbitInputManager + TDELifecycleAdapter) increases the integration surface vs a single-component design. Clear documentation required to prevent confusion about which component handles which responsibility.
- FMOD init assertion in `Start()` produces a confusing failure mode during development if FMOD is misconfigured. Requires explicit error messages.

### Risks
- **HIGH: TDE 4.x compatibility with Unity 6.3 LTS.** TDE may not support Unity 6.3 LTS. The `[SerializeField]` fields-only change may break TDE editor scripts. **Mitigation**: M0 Spike first task — import TDE, compile, verify zero errors. If TDE doesn't compile, entire F6 architecture is blocked.
- **HIGH: `InputSystemManagerEventsBased` override API unknown.** The subclass approach depends on TDE 4.x exposing virtual/overridable methods for Action Map Enable/Disable. If these methods are non-virtual, the subclass cannot intercept them. **Mitigation**: M0 Spike — read TDE 4.x `InputSystemManagerEventsBased.cs` source; if non-virtual, fallback to `HandleInput = false` + direct F3 calls from TDELifecycleAdapter (effectively Alternative 1 + manual AbilityInputAuthorization reimplementation).
- **HIGH: TDE event name strings.** `"Pause"` / `"UnPause"` are assumed from TDE 2.x training data. TDE 4.x may use different strings, typed enums, or a separate event type. **Mitigation**: M0 Spike — verify pause event names from TDE 4.x source or documentation. Extract string constants into a static helper class to centralize breakage.
- **MEDIUM: FMOD RuntimeManager init timing.** `RuntimeManager.IsInitialized` may be `false` during `Start()` on the first scene load if FMOD initializes asynchronously. **Mitigation**: Add `RuntimeManager` to Script Execution Order before `TDELifecycleAdapter`, or use a `WaitUntil(RuntimeManager.IsInitialized)` coroutine in the boot sequence.
- **MEDIUM: Additive scene load double-fire.** `SceneManager.sceneLoaded` fires for every scene load including additive. TDE's `LevelManager` may use additive loading for room management. **Mitigation**: ADR specifies `LoadSceneMode.Single` guard in `OnSceneLoaded()`. Room-level BPM changes (if needed) use `BeatClock.RequestBPMChange()` instead of re-calling `Play()`.
- **LOW: `MMGameEvent` string key fragility.** String comparison for event routing can break silently. **Mitigation**: F6 implementation should use extracted constants verified against TDE source, not inline string literals.

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| input-event-system.md (F3) | E8: TDE `InputSystemManagerEventsBased` Action Map state conflict | `JadeRabbitInputManager` subclass overrides Action Map management to delegate to F3; F3 is sole authority |
| input-event-system.md (F3) | AC-F3-20: `InputEventBus.Awake()` initial state `AllDisabled`; F6 enables on scene load | Boot sequence: `OnSceneLoaded()` calls `EnableGameplayInput()` after `BeatClock.Play()` |
| audio-beat-clock.md (F2) | Pause/resume signal from TDE | `TDELifecycleAdapter.OnMMEvent(Pause)` calls `BeatClock.Pause()` in same frame as `EnableUIInput()` |
| audio-beat-clock.md (F2) | F6 → F2: BGM AudioSource ref; pause/resume signals | F6 calls `BeatClock.Play/Pause/Resume/Stop` exclusively; 3-step BGM protocol internal to BeatClock |
| audio-beat-clock.md (F2) | Core Rule 11: subscriber lifecycle; F6 pre-signal for room unload | F6 can broadcast room-unload pre-signal before `BeatClock.Stop()`; subscribers unsubscribe in `OnDestroy()` |
| so-data-layer.md (F4) | TR-datalayer-010: LevelConfigSO BPM/time-sig consumed by F2 | TDELifecycleAdapter reads `LevelConfigSO` and passes `.Bpm`, `.TimeSignature`, `.BgmAssetReference` to `BeatClock.Play()` |
| input-event-system.md (F3) | E6 / DeferredAC-F3-16: FMOD async mode (`FMOD_INIT_NORMAL`) | `TDELifecycleAdapter.Start()` asserts FMOD RuntimeManager initialized; async mode enforcement deferred to F6 GDD detail |
| game-concept.md (F1) | Enemy AI beat-locked attacks require TDE character lifecycle | F6 lifecycle hooks (death/respawn) defined; CombatDirector token integration deferred to ADR-D2 |

## Performance Implications
- **CPU**: `TDELifecycleAdapter` is fully event-driven — no `Update()` cost. `OnMMEvent` callbacks fire only on pause/unpause/death/respawn (rare events). `JadeRabbitInputManager` adds negligible overhead to TDE's existing input processing path. Estimated contribution: < 0.01ms/frame average.
- **Memory**: Two `MonoBehaviour` components with serialized references. No runtime allocation after initialization.
- **Load Time**: `Start()` FMOD check: < 0.01ms. Scene load boot sequence: < 1ms total (BeatClock.Play + EnableGameplayInput).
- **Network**: None.

## Migration Plan
Greenfield project — no existing F6 code to migrate.

Implementation sequence:

1. **M0 Spike (prerequisite)**:
   - Import TDE 4.x into Unity 6.3 LTS project; verify compilation
   - Read `InputSystemManagerEventsBased.cs` source: identify virtual/overridable methods for Action Map management
   - Verify `MMEventListener<T>` API signatures
   - Verify pause/death event name strings
   - Document findings in `docs/engine-reference/unity/plugins/top-down-engine.md`

2. **F6 Component Implementation**:
   - Implement `JadeRabbitInputManager : InputSystemManagerEventsBased` in `Assets/Scripts/Core/Integration/`
   - Implement `TDELifecycleAdapter` in `Assets/Scripts/Core/Integration/`
   - Replace `InputSystemManagerEventsBased` with `JadeRabbitInputManager` on PlayerCharacter prefab
   - Verify character movement and abilities work with the subclass

3. **Testing**:
   - EditMode unit test: `TDELifecycleAdapter` event routing with mock F2/F3 (deterministic)
   - PlayMode integration: pause event → assert BeatClock paused + InputEventBus in UIActive
   - PlayMode integration: death event during charged attack → assert zero ghost LightAttack events
   - PlayMode: character prefab with `JadeRabbitInputManager` — movement, dash, abilities all functional

4. **F6 GDD Authoring**: This ADR is the prerequisite.

## Validation Criteria
1. **Compile**: Both `JadeRabbitInputManager.cs` and `TDELifecycleAdapter.cs` compile without errors in Unity 6.3 LTS
2. **Prefab integrity**: PlayerCharacter prefab with `JadeRabbitInputManager` (replacing `InputSystemManagerEventsBased`) — character moves, dashes, and uses abilities normally
3. **Boot sequence**: After `OnSceneLoaded(Single)`, `InputEventBus.CurrentState == GameplayActive` AND `BeatClock.ClockState == Playing`
4. **Pause routing**: Broadcast TDE pause event → assert `BeatClock.ClockState == Paused` AND `InputEventBus.CurrentState == UIActive` (same-frame check)
5. **Death routing**: Broadcast `TopDownEngineEvent(PlayerDeath)` → assert `BeatClock.ClockState == Stopped` AND `InputEventBus.CurrentState == AllDisabled`
6. **E3 ghost-attack guard**: Simulate charged attack in progress → broadcast PlayerDeath → assert zero `LightAttack` events broadcast (the `_isSwitchingMaps` guard must be active)
7. **Additive load filter**: Load a scene additively → assert `OnSceneLoaded` does NOT re-trigger BeatClock.Play() or EnableGameplayInput()
8. **FMOD init guard**: Misconfigure FMOD (e.g., remove FMOD listener) → assert `TDELifecycleAdapter` logs error and does not call `BeatClock.Play()`

## Related Decisions
- `design/gdd/audio-beat-clock.md` — F2 GDD; BeatClock controlled exclusively by TDELifecycleAdapter
- `design/gdd/input-event-system.md` — F3 GDD; E8 conflict resolved by JadeRabbitInputManager subclass; E6 FMOD constraint enforced by TDELifecycleAdapter
- `design/gdd/so-data-layer.md` — F4 GDD; LevelConfigSO injection pathway through TDELifecycleAdapter
- `docs/architecture/adr-d6-audio-middleware.md` — ADR-D6; FMOD init must complete before TDELifecycleAdapter.Start()
- ADR-D5 (Animation Pipeline) — Animancer attack layer boundary with TDE Animator layer; depends on this ADR
- ADR-D2 (Enemy AI Framework) — CombatDirector token management on death/respawn; depends on this ADR
