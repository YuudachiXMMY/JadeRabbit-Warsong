# ADR-D1: TopDownEngine Integration Architecture

## Status
Accepted

## Date
2026-07-15

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (6000.3.19f1) |
| **Domain** | Core / Integration |
| **Knowledge Risk** | MEDIUM — TDE 4.5 compiled successfully in Unity 6.3 LTS (verified M0 Spike 2026-07-16); remaining risk is FMOD native plugin compatibility |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `docs/engine-reference/unity/plugins/top-down-engine.md`, `docs/architecture/adr-d6-audio-middleware.md` |
| **Post-Cutoff APIs Used** | `MMEventListener<T>` (TDE event subscription — verified M0 Spike); `TopDownEngineEvent` (TDE typed lifecycle events — verified); `InputSystemManagerEventsBased` (TDE input manager — subclassed via `BindButton` override) |
| **Verification Required** | 1. ~~Confirm TDE 4.x compiles in Unity 6.3 LTS~~ ✅ VERIFIED (M0 Spike 2026-07-16). 2. ~~Verify `MMEventListener<T>` API signatures~~ ✅ VERIFIED. 3. ~~Verify pause event mechanism~~ ✅ VERIFIED — TDE uses `TopDownEngineEvent` with `TopDownEngineEventTypes.Pause/UnPause` (NOT `MMGameEvent` string). 4. ~~Verify `TopDownEngineEventTypes` enum members~~ ✅ VERIFIED — `.PlayerDeath` exists; `.Respawn` does NOT exist → use `.RespawnComplete`. 5. ~~Verify `InputSystemManagerEventsBased` virtual methods~~ ✅ VERIFIED — only `BindButton` is virtual; `SetInputEnabled` does NOT exist → use `BindButton` override as input gate. 6. Confirm FMOD for Unity RuntimeManager script execution order vs TDELifecycleAdapter (PENDING — FMOD not yet imported). |

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
- Establish LevelConfigSO → BeatClock injection via F6 (TR-datalayer-012)
- Enforce FMOD async init mode at boot (TR-input-015)
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

> **M0 Spike Result (2026-07-16)**: `InputSystemManagerEventsBased` exposes only one virtual method: `BindButton(InputAction.CallbackContext, MMInput.IMButton)` at line 42. `SetInputEnabled` does not exist; `HandleInput` does not exist. The adopted approach is: override `BindButton` to gate input through F3's state machine (`CurrentState != GameplayActive` → drop input). F3 directly calls `InputActionMap.Enable()/Disable()` — not through TDE's input manager. TDE's `AbilityInputAuthorization` is preserved because `JadeRabbitInputManager` inherits the full ability pipeline; only the button callback entry point is gated.

### BeatClock Lifecycle Authority

`TDELifecycleAdapter` is the **exclusive controller** of `BeatClock.Play()`, `.Pause()`, `.Resume()`, and `.Stop()`. No other system may call these methods. BeatClock internally handles the 3-step BGM play protocol (optimistic pre-anchor + actual start correction) as defined in F2 GDD and ADR-D6.

### LevelConfigSO Injection

`TDELifecycleAdapter` holds an Inspector reference to the scene's `LevelConfigSO`. At scene load, it extracts `.Bpm`, `.TimeSignature`, and `.BgmFilePath`, and passes them to `BeatClock.Play()`.

### FMOD Init Constraint

`TDELifecycleAdapter.Start()` (not `Awake()` — to avoid Script Execution Order race with `FMODUnity.RuntimeManager`) performs a **read-only probe** of whether FMOD RuntimeManager is initialized and running in async mode, caching the result into `_fmodVerified`. Per the A1 裁定, `Start()` does not gate proceeding — the acting readiness guard (loud `[F6-FATAL]` log + early return, keeping the scene in a safe frozen state) lives in `OnSceneLoaded()`, alongside the `ISaveService.IsInitialized` check.

### Lifecycle Subscription (A1 裁定 2026-08-11)

**`OnEnable()` / `OnDisable()` 配对是 `sceneLoaded` 与 `MMEventListener<TopDownEngineEvent>` 订阅的唯一权威位置。** 二者对称订阅 / 退订，`Start()` 只做只读就绪探测（缓存 `_fmodVerified`），**不订阅任何事件**。所有就绪前置条件（FMOD、`ISaveService.IsInitialized`）以 **handler 内早退 guard** 形式落在 `OnSceneLoaded()` 体内，而非用于门控订阅动作。

裁定理由（`/architecture-review 2026-08-11` A1，BLOCKING D1/D10）：

- **对齐 MoreMountains 惯例**：`MMEventListener<T>` 的 `MMEventStartListening` / `MMEventStopListening` 必须成对置于 `OnEnable` / `OnDisable`；将 `sceneLoaded` 一并放入同一对回调，避免组件 disable→re-enable 时残留悬空委托或重复订阅（`OnBeatExact` 逐拍路径的重复订阅泄漏成本最高）。
- **消解与 ADR-D10 的冲突**：ADR-D10 §Key Interfaces 曾把 `sceneLoaded += OnSceneLoaded` 放在 `Start()` 里，理由是要排在 `SaveServiceBootstrap` 之后。本裁定把该"排序"关切从**订阅时机**转移到 **handler 早退 guard**：`OnSceneLoaded` 体首行 `if (!_fmodVerified || !_saveService.IsInitialized) return;` 保证首帧场景在 SaveService 就绪前不会执行 `Play()` / 注入 / `EnableGameplayInput()`。订阅可在 `OnEnable`（早于 `Start`）安全建立——它只连线不触发。**D1 为生命周期订阅位置的权威 ADR，D10 的 `Start()` 订阅示例以此为准更新。**
- D10 拥有的一次性 offset boot-inject（`_offsetInjected` guard + `LoadLatencyCalibration()`）位置与所有权不变，仍落在 `OnSceneLoaded(Single)` 体内、首个 `Play()` 之前。

### Boot Sequence (per scene load)

```
SceneManager.sceneLoaded (LoadSceneMode.Single only — additive loads are filtered)
    └→ TDELifecycleAdapter.OnSceneLoaded()
         1. Guard: skip if LoadSceneMode != Single (TDE room additive loads)
         2. Assert FMODUnity.RuntimeManager is initialized (deferred from Start())
         3. Read LevelConfigSO: .Bpm, .TimeSignature, .BgmFilePath
         4. BeatClock.Play(bgmPath, bpm, timeSig)  ← F2 runs 3-step protocol internally
         5. InputEventBus.EnableGameplayInput()     ← F3: AllDisabled → GameplayActive
```

### Pause / Resume

```
TDE TopDownEngineEvent(Pause)   ──→  TDELifecycleAdapter
    └→ BeatClock.Pause()              ← F2: Playing → Paused  (same frame)
       InputEventBus.EnableUIInput()  ← F3: GameplayActive → UIActive (same frame)

TDE TopDownEngineEvent(UnPause) ──→  TDELifecycleAdapter
    └→ BeatClock.Resume()                  ← F2: Paused → Playing  (same frame)
       InputEventBus.EnableGameplayInput() ← F3: UIActive → GameplayActive (same frame)
```

> **M0 Spike Verified (2026-07-16)**: TDE 4.5 uses `TopDownEngineEvent` with `TopDownEngineEventTypes.Pause` / `.UnPause` enum values (GameManager.cs:516-520). NOT string-based `MMGameEvent`. `TDELifecycleAdapter` implements only `MMEventListener<TopDownEngineEvent>`.

### Death / Respawn

```
TDE TopDownEngineEvent(PlayerDeath) ──→  TDELifecycleAdapter
    └→ BeatClock.Stop()                ← F2: → Stopped  (run-ending clock stop)
       InputEventBus.DisableAllInput() ← F3: → AllDisabled
       (broadcast OnPlayerDeath — domain event for CombatCore consumers)

TDE TopDownEngineEvent(RespawnComplete) ──→  TDELifecycleAdapter
    └→ [MVP: in-place respawn DISABLED — dormant diagnostic guard]
       log Fatal "[F6-FATAL] RespawnComplete fired but in-place respawn
                   is disabled in MVP"  →  return (NO in-place BeatClock.Play)
```

> **死亡 = 换新局（MVP）**：MVP 采用 run-ends-on-death 语义——`PlayerDeath`（lives=1，无同局复活）停止时钟并禁输入后，由 FE5/C5 承接结算，「下一局」经全新 `LoadSceneMode.Single` 走既有 boot 路径干净起局。TDE 原生 `RespawnComplete → BeatClock.Play()` 的**原地重启路径在 MVP 休眠**（经 TDE Inspector 关闭原地复活；误触发仅记 fatal 诊断、不原地重启时钟）。死亡 = 换新局的完整语义见 **ADR-D12（Death & Run-Lifecycle Semantics）**。

### Architecture Diagram

```
[Unity SceneManager.sceneLoaded]
          │ (LoadSceneMode.Single only)
          ▼
[F6: TDELifecycleAdapter : MonoBehaviour]  ── DontDestroyOnLoad Manager
  ┌───────┴───────────────────────────────────────────────────────┐
  │  implements MMEventListener<TopDownEngineEvent>               │
  │                                                               │
  │  [Inspector refs]                                             │
  │    _inputEventBus : InputEventBus      (F3 singleton)        │
  │    _beatClock     : BeatClock          (F2 singleton)        │
  │    _levelConfig   : LevelConfigSO      (F4 per-scene)       │
  └───────────────────────────────────────────────────────────────┘
          │
          ├── OnSceneLoaded ────────► BeatClock.Play(bpm, timeSig, bgmPath)
          │                           InputEventBus.EnableGameplayInput()
          │
          ├── TopDownEngineEvent     ► BeatClock.Pause()
          │   (Pause)                  InputEventBus.EnableUIInput()
          │
          ├── TopDownEngineEvent     ► BeatClock.Resume()
          │   (UnPause)                InputEventBus.EnableGameplayInput()
          │
          ├── TopDownEngineEvent     ► BeatClock.Stop() (run-ending)
          │   (PlayerDeath)            InputEventBus.DisableAllInput()
          │                            broadcast OnPlayerDeath
          │
          └── TopDownEngineEvent     ► [MVP dormant] log Fatal + return
              (RespawnComplete)        (in-place respawn disabled — see ADR-D12)


[Character Prefab — Modified]
  ├── Character                          (TDE — untouched)
  ├── CharacterMovement                  (TDE — untouched)
  ├── [JadeRabbitInputManager]           ← REPLACES InputSystemManagerEventsBased
  │     └── : InputSystemManagerEventsBased (inherits; preserves AbilityInputAuth)
  │           └── BindButton override → gates input through F3.InputEventBus state
  └── CharacterAbilities                 (TDE — untouched)


[CombatCore Assembly]  (read-only consumers — never call BeatClock lifecycle methods)
  ├── C1 RhythmCore       ── subscribes to IBeatClock.OnHalfBeatLookahead (hard dep) + OnBeatExact  (per ADR-D8 §1)
  └── C4 CombatDirector   ── subscribes to IBeatClock.OnBeatLookahead   (hard dep) + OnMeterChanged (per ADR-D8 §1)
```

### Key Interfaces

```csharp
// ═══════════════════════════════════════════════════════════════
// F6 Component 1: JadeRabbitInputManager
// Location: Assets/Scripts/Core/Integration/JadeRabbitInputManager.cs
// Replaces InputSystemManagerEventsBased on Character prefabs
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Subclass of TDE's InputSystemManagerEventsBased that gates all button
/// input through F3's InputEventBus state. When F3 is in AllDisabled state,
/// all button callbacks are silently dropped. This preserves TDE's
/// AbilityInputAuthorization pipeline through inheritance while giving F3
/// exclusive authority over input flow.
/// </summary>
/// <remarks>
/// M0 SPIKE VERIFIED (2026-07-16): InputSystemManagerEventsBased has only
/// one virtual method: BindButton(). SetInputEnabled does NOT exist in TDE 4.5.
/// OnJump/OnPause/etc are non-virtual public callbacks — cannot be overridden.
/// Strategy: override BindButton as universal input gate; F3 manages
/// InputActionMap.Enable()/Disable() directly (not through TDE).
/// </remarks>
public class JadeRabbitInputManager : InputSystemManagerEventsBased
{
    [SerializeField] private InputEventBus _inputEventBus;  // F3

    // Override the sole virtual method in InputSystemManagerEventsBased
    // Source: InputSystemManagerEventsBased.cs:42
    protected override void BindButton(InputAction.CallbackContext context,
                                       MMInput.IMButton imButton)
    {
        // F3 state gate: block all button input when AllDisabled or UIActive
        if (_inputEventBus.CurrentState != InputState.GameplayActive)
            return;

        base.BindButton(context, imButton);
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
/// <remarks>
/// M0 SPIKE VERIFIED (2026-07-16): TDE 4.5 sends ALL lifecycle events
/// (including pause/unpause) as TopDownEngineEvent with typed enum — NOT as
/// MMGameEvent with string comparison. Only MMEventListener&lt;TopDownEngineEvent&gt;
/// is needed. TopDownEngineEventTypes.Respawn does NOT exist; use RespawnComplete.
/// </remarks>
public sealed class TDELifecycleAdapter : MonoBehaviour,
    MMEventListener<TopDownEngineEvent>  // single interface — all lifecycle events
{
    [SerializeField] private InputEventBus _inputEventBus;  // F3
    [SerializeField] private BeatClock     _beatClock;      // F2
    [SerializeField] private LevelConfigSO _levelConfig;    // F4
    private ISaveService _saveService;                      // F5 — D-4 seam (ADR-D10; prod: SaveService.Instance)

    private bool _fmodVerified;

    // F6 domain events — pure C# events ON this adapter (payload carries NO TDE types;
    // CombatCore consumers subscribe here, never to TDE events directly). See
    // tde-integration.md Rule 15 / AC-F6-25. OnRespawnComplete is MVP-dormant (ADR-D12).
    public event System.Action OnPlayerDeath;
    public event System.Action OnRespawnComplete;   // Post-MVP checkpoint only — NOT raised in MVP

    // A1 裁定 (2026-08-11): Start() ONLY does read-only readiness probing.
    // It does NOT subscribe — subscription lives in OnEnable/OnDisable (below).
    private void Start()
    {
        // FMOD async mode guard — in Start() to avoid Awake() ordering race
        // with FMODUnity.RuntimeManager (Finding #3 from engine specialist).
        // Cached only; the acting early-return is inside OnSceneLoaded.
        _fmodVerified = FMODUnity.RuntimeManager.IsInitialized;
        if (!_fmodVerified)
        {
            Debug.LogError("[TDELifecycleAdapter] FMOD RuntimeManager not initialized. " +
                           "BeatClock will not start. Check Script Execution Order.");
        }
    }

    // A1 裁定: OnEnable/OnDisable is the sole authoritative subscription site
    // for BOTH the MMEventListener and SceneManager.sceneLoaded (symmetric pairing).
    private void OnEnable()
    {
        this.MMEventStartListening<TopDownEngineEvent>();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        this.MMEventStopListening<TopDownEngineEvent>();
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Guard: skip additive loads (TDE room management)
        if (mode != LoadSceneMode.Single) return;
        // A1 裁定: readiness guards are handler-body early-returns, NOT subscription gates.
        // FMOD (ADR-D6) + SaveService (ADR-D10) must both be ready before any Play()/input.
        // Release-active runtime guard (NOT Debug.Assert — must fail loudly in shipped builds;
        // matches ADR-D10 sample + the "Assert ... initialized" step in the Boot Sequence above):
        if (!_fmodVerified || !_saveService.IsInitialized)
        {
            Debug.LogError("[F6-FATAL] FMOD or SaveService not ready before first scene Play() " +
                           "— the bootstrap driver did not order SaveServiceBootstrap first.");
            return;                                            // scene stays in a safe frozen state; no Play()/input
        }

        // ADR-D10 owns the one-time offset boot-inject here (before the first Play()).
        _beatClock.Play(
            _levelConfig.BgmFilePath,
            _levelConfig.Bpm,
            _levelConfig.TimeSignature);
        _inputEventBus.EnableGameplayInput();
    }

    // M0 Spike verified: TDE 4.5 uses TopDownEngineEvent for ALL lifecycle
    // events including pause (GameManager.cs:516-520). Enum values confirmed
    // from GameManager.cs:27-47.
    public void OnMMEvent(TopDownEngineEvent topDownEvent)
    {
        switch (topDownEvent.EventType)
        {
            case TopDownEngineEventTypes.Pause:
                _beatClock.Pause();
                _inputEventBus.EnableUIInput();
                break;
            case TopDownEngineEventTypes.UnPause:
                _beatClock.Resume();
                _inputEventBus.EnableGameplayInput();
                break;
            case TopDownEngineEventTypes.PlayerDeath:
                // Run-ends-on-death (MVP). See ADR-D12.
                _beatClock.Stop();                 // run-ending clock stop
                _inputEventBus.DisableAllInput();
                OnPlayerDeath?.Invoke();           // broadcast OnPlayerDeath (pure C# event on this adapter; F6 AC-F6-25)
                break;
            case TopDownEngineEventTypes.RespawnComplete:
                // MVP: in-place respawn is DISABLED (death = new run, ADR-D12).
                // TDE in-place respawn should be turned off in the Inspector; if this
                // event still fires, do NOT restart the clock in place — a fresh
                // LoadSceneMode.Single reload drives the next run via OnSceneLoaded.
                Debug.LogError(
                    "[F6-FATAL] RespawnComplete fired but in-place respawn is disabled in MVP");
                return;  // dormant diagnostic guard — no in-place BeatClock.Play()
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
- ~~**HIGH: TDE 4.x compatibility with Unity 6.3 LTS.**~~ ✅ RESOLVED (M0 Spike 2026-07-16): TDE 4.5 compiles with zero errors in Unity 6.3 LTS.
- ~~**HIGH: `InputSystemManagerEventsBased` override API unknown.**~~ ✅ RESOLVED (M0 Spike 2026-07-16): Only `BindButton` is virtual. Adopted approach: override `BindButton` as input gate; F3 manages InputActionMap directly. `SetInputEnabled` and `HandleInput` do not exist in TDE 4.5.
- ~~**HIGH: TDE event name strings.**~~ ✅ RESOLVED (M0 Spike 2026-07-16): TDE 4.5 uses `TopDownEngineEvent` with typed enum `TopDownEngineEventTypes.Pause/UnPause` (GameManager.cs:516-520). No string comparison needed. `Respawn` → `RespawnComplete` (GameManager.cs:38).
- **MEDIUM: FMOD RuntimeManager init timing.** `RuntimeManager.IsInitialized` may be `false` during `Start()` on the first scene load if FMOD initializes asynchronously. **Mitigation**: Add `RuntimeManager` to Script Execution Order before `TDELifecycleAdapter`, or use a `WaitUntil(RuntimeManager.IsInitialized)` coroutine in the boot sequence.
- **MEDIUM: Additive scene load double-fire.** `SceneManager.sceneLoaded` fires for every scene load including additive. TDE's `LevelManager` may use additive loading for room management. **Mitigation**: ADR specifies `LoadSceneMode.Single` guard in `OnSceneLoaded()`. Room-level BPM changes (if needed) use `BeatClock.RequestBPMChange()` instead of re-calling `Play()`.
- **LOW: `MMGameEvent` string key fragility.** String comparison for event routing can break silently. **Mitigation**: F6 implementation should use extracted constants verified against TDE source, not inline string literals.

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| input-event-system.md (F3) | E8: TDE `InputSystemManagerEventsBased` Action Map state conflict | `JadeRabbitInputManager` subclass overrides Action Map management to delegate to F3; F3 is sole authority |
| input-event-system.md (F3) | AC-F3-20: `InputEventBus.Awake()` initial state `AllDisabled`; F6 enables on scene load | Boot sequence: `OnSceneLoaded()` calls `EnableGameplayInput()` after `BeatClock.Play()` |
| audio-beat-clock.md (F2) | Pause/resume signal from TDE | `TDELifecycleAdapter.OnMMEvent(Pause)` calls `BeatClock.Pause()` in same frame as `EnableUIInput()` |
| audio-beat-clock.md (F2) | F6 → F2: BGM FMOD asset ref; pause/resume signals | F6 calls `BeatClock.Play/Pause/Resume/Stop` exclusively; 3-step BGM protocol internal to BeatClock |
| audio-beat-clock.md (F2) | Core Rule 11: subscriber lifecycle; F6 pre-signal for room unload | F6 can broadcast room-unload pre-signal before `BeatClock.Stop()`; subscribers unsubscribe in `OnDestroy()` |
| so-data-layer.md (F4) | TR-datalayer-012: LevelConfigSO BPM/time-sig consumed by F2 | TDELifecycleAdapter reads `LevelConfigSO` and passes `.Bpm`, `.TimeSignature`, `.BgmFilePath` to `BeatClock.Play()` |
| input-event-system.md (F3) | E6 / DeferredAC-F3-16: FMOD async mode (`FMOD_INIT_NORMAL`) | `TDELifecycleAdapter.Start()` probes + caches FMOD RuntimeManager init state; the acting readiness guard runs in `OnSceneLoaded()` (A1 裁定); async mode enforcement deferred to F6 GDD detail |
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
