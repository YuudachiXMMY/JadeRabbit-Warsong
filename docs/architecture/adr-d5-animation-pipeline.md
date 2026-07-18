# ADR-D5: Animation Pipeline — Animancer BPM-Adaptive Attack Layer

## Status
Proposed

## Date
2026-07-18

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (6000.3.19f1) |
| **Domain** | Animation |
| **Knowledge Risk** | MEDIUM — Animancer is a third-party package (Kybernetik); Unity 6.3 LTS compatibility verified via installed source (Animancer 8.4.0 at `Packages/com.kybernetik.animancer/`) |
| **References Consulted** | `docs/engine-reference/unity/modules/animation.md`, `docs/engine-reference/unity/VERSION.md`, `docs/architecture/adr-d1-tde-integration.md`, Animancer 8.4.0 source (`Runtime/HybridAnimancerComponent.cs`, `Runtime/Core/Nodes/AnimancerNodeBase.cs`, `Runtime/Core/Events/AnimancerEvent.Sequence.cs`) |
| **Post-Cutoff APIs Used** | `HybridAnimancerComponent` (Kybernetik Animancer 8.4.0 — wraps Unity PlayableGraph API; first-class hybrid Animator Controller coexistence), `AnimancerState.Speed` (inherited from `AnimancerNodeBase`), `AnimancerState.Events.OnEnd` |
| **Verification Required** | 1. ~~Confirm Animancer supports Unity 6000.3.x~~ VERIFIED — Animancer 8.4.0 installed and compiling in project. 2. Verify `HybridAnimancerComponent` Hybrid mode with TDE Animator Controller in PlayMode — specifically that `KeepChildrenConnected = true` prevents `AnimatorControllerPlayable` disconnection when attack layer weight drops to zero. 3. Verify `AnimancerState.Events.OnEnd` fires correctly under `state.Speed > 1.0` (Animancer source confirms threshold-based invocation at `DefaultExecutionOrder(-30000)`, no frame-skip — but integration test needed). |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-D1 (TDE Integration — Accepted; defines TDE Animator layer boundary; this ADR must not conflict with TDE's Animator Controller parameters or Death/Respawn state triggers) |
| **Enables** | C2 Combo System GDD/implementation (ComboPlayer can now call AnimancerAttackPlayer.PlayAttack()); ADR-D2 (Enemy AI Framework — enemy characters will use the same animation pipeline pattern for beat-locked attacks) |
| **Blocks** | C2 Combo System GDD authoring — attack animation playback API contract must be defined before C2 can specify its interface |
| **Ordering Note** | This ADR defines the AnimancerAttackPlayer API that C2 ComboPlayer depends on. ADR-D2 (Enemy AI) may reference this same pattern for enemy attack animations. |

## Context

### Problem Statement

TopDownEngine's Animator Controller manages character state (locomotion, dash, death, respawn) via Mecanim state machine transitions. This controller uses fixed-duration animation clips — a `MiaoSword_LightAttack.anim` authored at 120 BPM cannot be dynamically scaled when the level BPM changes to 100 or 160.

The game's Pillar 2 contract ("Wuxia Feel First") and TR-concept-004 require attack animations to remain visually congruent with the beat grid regardless of current BPM. A fixed-frame attack animation at 120 BPM will become visually early at 160 BPM (attack hits before the beat lands) or late at 80 BPM (attack drags behind the beat), breaking the rhythm-action core loop.

Additionally, the Combo Graph system (C2) requires runtime clip swapping — each `AttackNodeSO` carries its own `AnimationClip`, and the active clip must change per attack node without rebuilding an Animator Override Controller at runtime (which has non-trivial allocation and transition latency).

Without a defined animation pipeline:
- No system knows it owns `AttackNodeSO.animationClip` as a runtime consumer
- BPM changes have no path to modify attack animation speed
- TDE's Animator Controller and Animancer may conflict at the layer level
- `AnimancerState.Speed` constraint in `technical-preferences.md` has no architectural backing

### Constraints

- **TDE source modification is FORBIDDEN** — the Animator Controller managed by TDE (locomotion, death, respawn) must remain untouched.
- **Animancer must not interfere with TDE Animator parameters** — TDE writes float/bool/trigger parameters to drive locomotion states; `HybridAnimancerComponent` wraps the controller in a `ControllerState` with `KeepChildrenConnected = true`, which keeps the `AnimatorControllerPlayable` alive at all times.
- **Zero GC during attack playback** — `AnimancerState.Speed` assignment and `OnEnd` callback must not allocate heap memory per frame (IL2CPP compliance).
- **AttackNodeSO schema change requires GDD propagation** — adding `authoredBpm` to `AttackNodeSO` is a schema change to F4; `design/gdd/so-data-layer.md` must be updated to include this field.
- **AnimationClip loading**: `AttackNodeSO.animationClip` (`AssetReferenceT<AnimationClip>`) requires Addressables pre-load before `AnimancerAttackPlayer.PlayAttack()` is called. The loading contract is deferred to the C2/F6 GDD; this ADR assumes clips are loaded before use.
- **Animancer layer speed constraint**: `AnimancerAttackPlayer` assumes the Animancer layer's own `Speed` is 1.0. If a global slow-motion system sets layer speed to a non-1.0 value, the effective playback speed will be `state.Speed × layer.Speed`, causing BPM desync. Any future slow-motion feature must coordinate with this constraint.

### Requirements

- Must provide BPM-adaptive attack animation speed (TR-concept-004)
- Must coexist with TDE's Animator Controller without breaking TDE locomotion or lifecycle state transitions (death/respawn) defined in ADR-D1
- Must define the owner and API for playing `AttackNodeSO.animationClip` at runtime (satisfy F4's schema consumer gap)
- Must support runtime clip swapping per attack node without allocation
- Must notify the Combo Graph system (C2) when the attack animation completes (OnEnd contract)
- Must gracefully handle death-during-attack: when TDE sends PlayerDeath, the active attack animation must stop cleanly and TDE's death animation must play

## Decision

### Core Architecture

`AnimancerAttackPlayer` is a new `MonoBehaviour` component placed on the Character prefab, working with Animancer's **`HybridAnimancerComponent`** (NOT plain `AnimancerComponent`). `HybridAnimancerComponent` is Animancer 8.4's first-class API for coexisting with an existing Animator Controller:

- It wraps the `RuntimeAnimatorController` inside a `ControllerState`
- It sets `KeepChildrenConnected = true` on the PlayableGraph, preventing the `AnimatorControllerPlayable` from being disconnected when Animancer's weight on it drops to zero
- It exposes `PlayController()` to return control to the Animator Controller

**Layer behavior**:
- When **no attack is playing**: `HybridAnimancerComponent`'s `ControllerState` is active. TDE's Animator Controller fully drives character output (locomotion, dash, death, respawn).
- When **attack starts**: `AnimancerAttackPlayer.PlayAttack()` is called by C2 ComboPlayer. Animancer plays the clip from AttackNodeSO, speed-scaled by the `currentBpm / authoredBpm` formula. The Animancer state overrides the Animator Controller output.
- When **attack ends** (clip reaches OnEnd or `StopAttack()` is called): `_animancer.PlayController()` hands control back to TDE's Animator Controller. The `onComplete` callback notifies C2 to advance the Combo Graph (not fired on death-stop).

### BPM Speed Scaling Formula

```
state.Speed = beatClock.CurrentBpm / attackNode.AuthoredBpm
```

| Variable | Definition | Range |
|----------|-----------|-------|
| `state.Speed` | Animancer playback speed multiplier (1.0 = authored speed) | [0.33, 4.0] practical range |
| `beatClock.CurrentBpm` | Live BPM from BeatClock (F2) — read-only pull | [60, 240] |
| `attackNode.AuthoredBpm` | The BPM at which the animator authored the clip | [60, 240] |

Speed is set **once at attack start** from BeatClock's current value. It is NOT updated mid-attack for MVP (BPM is fixed per level via LevelConfigSO). Post-MVP Boss phase BPM changes should call `AnimancerAttackPlayer.UpdateBpm()` which updates `_currentState.Speed` without interrupting the animation or resetting normalized time.

**Example calculations** (`authoredBpm = 120`):
| Level BPM | state.Speed | Effect |
|-----------|------------|--------|
| 120 | 1.000 | Plays at authored speed |
| 90 | 0.750 | 25% slower — attack stretches to fit slower beat |
| 150 | 1.250 | 25% faster — attack compresses to fit faster beat |
| 180 | 1.500 | 50% faster — high BPM boss rush |

### F4 Schema Change: AttackNodeSO

`AttackNodeSO` gains one new field:

```csharp
[SerializeField]
private float _authoredBpm = 120f;
public float AuthoredBpm => _authoredBpm;
```

In the Editor-only `#if UNITY_EDITOR` block or Odin Validator `RegisterValidator` class:
- `[MinValue(1f)]` — must be non-zero and positive (zero causes divide-by-zero)
- `[MaxValue(240f)]` — practical upper bound
- Tooltip: "The BPM the animator used when authoring this clip. Runtime speed = currentBpm / authoredBpm."

### Death-During-Attack Safety

`TDELifecycleAdapter` (ADR-D1) sends `TopDownEngineEvent(PlayerDeath)`, which calls `BeatClock.Stop()` and `InputEventBus.DisableAllInput()`. `AnimancerAttackPlayer` subscribes to the same event via `MMEventListener<TopDownEngineEvent>` and calls `StopAttack()` in the same frame:

1. Clears `_pendingOnComplete` (suppress onComplete callback — C2 must not advance combo on death)
2. Calls `_animancer.PlayController()` — returns control to TDE's Animator Controller, allowing TDE's death animation to play immediately
3. C2 treats the absence of onComplete callback as a death/cancel signal

The `OnEnable`/`OnDisable` subscription pairs ensure no leaked event listeners on pooled/destroyed characters (TDE's `MMEventManager` holds strong references).

### Architecture Diagram

```
[Character Prefab]
  ├── Animator                          (Unity — TDE controls this)
  │     └── RuntimeAnimatorController  (TDE locomotion: walk/run/dash/death/respawn)
  ├── HybridAnimancerComponent           ← ADR-D5 NEW
  │     ├── ControllerState            (wraps TDE's RuntimeAnimatorController)
  │     ├── KeepChildrenConnected=true (prevents AnimatorControllerPlayable disconnect)
  │     └── PlayController()           (hands back to TDE Animator)
  ├── AnimancerAttackPlayer              ← ADR-D5 NEW (CombatCore assembly)
  │     ├── [Ref] HybridAnimancerComponent
  │     ├── [Ref] BeatClock (F2, read-only CurrentBpm)
  │     └── PlayAttack(node, clip, onComplete)
  │           └→ state.Speed = currentBpm / authoredBpm
  ├── CharacterMovement                  (TDE — untouched)
  ├── JadeRabbitInputManager             (F6 — untouched)
  └── [C2 ComboPlayer]                   calls AnimancerAttackPlayer.PlayAttack()

[BPM Adaptation Data Flow]
  LevelConfigSO.Bpm
    └→ TDELifecycleAdapter.OnSceneLoaded() → BeatClock.Play(bpm)
         └→ BeatClock.CurrentBpm  (read-only property — no subscription needed)
               └→ AnimancerAttackPlayer.PlayAttack()
                     └→ state.Speed = CurrentBpm / AttackNodeSO.AuthoredBpm

[Attack Lifecycle]
  C2 ComboPlayer resolves next AttackNodeSO
    └→ AnimancerAttackPlayer.PlayAttack(node, clip, onComplete)
         └→ _animancer.Play(clip)  ← Animancer overrides TDE Animator
         └→ state.Speed = bpm / authoredBpm
         └→ state.Events.OnEnd = OnAnimationEnd  (method group — zero GC)
              └→ _animancer.PlayController()  ← TDE Animator resumes
              └→ onComplete?.Invoke()  ← C2 advances combo graph

  TDE TopDownEngineEvent(PlayerDeath) during attack:
    └→ AnimancerAttackPlayer.StopAttack()
         └→ _pendingOnComplete = null  (suppress combo advance)
         └→ _animancer.PlayController()  ← TDE plays death animation

[F4 Schema Change]
  AttackNodeSO (existing fields unchanged)
    + float AuthoredBpm = 120f  ← NEW (this ADR)
```

### Key Interfaces

```csharp
// ═══════════════════════════════════════════════════════════════
// AnimancerAttackPlayer
// Location: Assets/Scripts/Core/Combat/AnimancerAttackPlayer.cs
// Assembly: CombatCore (or Core, before CombatCore assembly exists)
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Drives the Animancer attack layer on the Character prefab.
/// Works with HybridAnimancerComponent (NOT plain AnimancerComponent)
/// to coexist with TDE's Animator Controller.
/// Called exclusively by C2 ComboPlayer.
/// </summary>
/// <remarks>
/// ENGINE SPECIALIST VERIFIED (2026-07-18, Animancer 8.4.0):
/// - HybridAnimancerComponent.PlayController() is the correct API to
///   hand control back to TDE's Animator Controller (not Stop()).
/// - Stop() pauses the entire PlayableGraph including the controller.
/// - KeepChildrenConnected=true prevents controller disconnection artifacts.
/// - AnimancerState.Speed is on AnimancerNodeBase (inherited), works correctly.
/// - OnEnd fires reliably at Speed > 1.0 (threshold-based, no frame-skip).
/// </remarks>
public sealed class AnimancerAttackPlayer : MonoBehaviour,
    MMEventListener<TopDownEngineEvent>
{
    [SerializeField] private HybridAnimancerComponent _animancer;
    [SerializeField] private BeatClock _beatClock; // F2 — read CurrentBpm only

    private AnimancerState _currentState;
    private System.Action _pendingOnComplete;
    private float _currentAuthoredBpm;

    /// <summary>
    /// Play an attack clip from AttackNodeSO, speed-scaled to current BPM.
    /// Assumes AnimationClip is already loaded (Addressables handle resolved).
    /// </summary>
    /// <param name="attackNode">The AttackNodeSO for this attack step.</param>
    /// <param name="loadedClip">Pre-loaded AnimationClip (from Addressables).</param>
    /// <param name="onComplete">Called when clip ends naturally (NOT on death-stop).</param>
    public void PlayAttack(AttackNodeSO attackNode, AnimationClip loadedClip,
                           System.Action onComplete)
    {
        _pendingOnComplete = onComplete;
        _currentAuthoredBpm = attackNode.AuthoredBpm;
        _currentState = _animancer.Play(loadedClip);
        _currentState.Speed = _beatClock.CurrentBpm / _currentAuthoredBpm;
        _currentState.Events.OnEnd = OnAnimationEnd; // method group — zero GC
    }

    /// <summary>
    /// Immediately stops the current attack and returns control to TDE's
    /// Animator Controller. Does NOT fire onComplete.
    /// Called on PlayerDeath or external cancel.
    /// </summary>
    public void StopAttack()
    {
        _pendingOnComplete = null;
        _animancer.PlayController(); // hand back to TDE — NOT Stop()
    }

    /// <summary>
    /// Update speed for in-progress attack when BPM changes (post-MVP boss phase).
    /// No-op if no attack is currently playing.
    /// </summary>
    public void UpdateBpm(float newBpm)
    {
        if (_currentState != null && _currentState.IsPlaying)
            _currentState.Speed = newBpm / _currentAuthoredBpm;
    }

    private void OnAnimationEnd()
    {
        var callback = _pendingOnComplete;
        _pendingOnComplete = null;
        _animancer.PlayController(); // return to TDE Animator Controller
        callback?.Invoke();
    }

    private void OnEnable()  => this.MMEventStartListening<TopDownEngineEvent>();
    private void OnDisable() => this.MMEventStopListening<TopDownEngineEvent>();

    public void OnMMEvent(TopDownEngineEvent evt)
    {
        if (evt.EventType == TopDownEngineEventTypes.PlayerDeath)
            StopAttack();
    }
}


// ═══════════════════════════════════════════════════════════════
// AttackNodeSO — schema addition (F4)
// Location: Assets/Scripts/Core/Data/AttackNodeSO.cs
// ═══════════════════════════════════════════════════════════════

// Existing fields (defined in F4 GDD — unchanged):
// [SerializeField] private AssetReferenceT<AnimationClip> _animationClip;
// [SerializeField] private float _hitboxStartNormalizedTime;
// [SerializeField] private float _hitboxDurationNormalized;
// [SerializeField] private CancelWindowType _cancelWindow;
// [SerializeField] private float _cancelWeight;
// [SerializeField] private QuantizationHint _quantizationHint;

// ─── NEW FIELD: ADR-D5 ───────────────────────────────────────
/// <summary>
/// The BPM at which the animator authored this clip.
/// Runtime playback speed = BeatClock.CurrentBpm / AuthoredBpm.
/// Default 120 — change to match the BPM of the metronome
/// used when the clip was created.
/// </summary>
[SerializeField] private float _authoredBpm = 120f;
public float AuthoredBpm => _authoredBpm;

// Odin validation (Editor-only, in RegisterValidator class):
//   [MinValue(1f)]  — zero/negative causes divide-by-zero
//   [MaxValue(240f)] — practical upper bound
// ────────────────────────────────────────────────────────────
```

## Alternatives Considered

### Alternative 1: Unity Native Animator Layer + Avatar Mask
- **Description**: Add a new Animator Layer (Layer 1) to TDE's Animator Controller with an Avatar Mask isolating the upper body. Use `AnimatorOverrideController` to swap clips at runtime. Control speed via `animator.SetFloat("AttackSpeed", bpm / authoredBpm)` per-frame.
- **Pros**: No third-party dependency beyond TDE; purely native Unity Mecanim.
- **Cons**: `AnimatorOverrideController` allocation on every clip swap (or requires a pre-built override controller per weapon). Adding a Layer 1 to TDE's Animator Controller requires modifying TDE's asset — this is in the spirit of "modifying TDE source" and creates a fragile upgrade path. `SetFloat` per-frame is acceptable but less direct than `State.Speed`.
- **Rejection Reason**: Modifying TDE's Animator Controller asset creates an upgrade dependency. Animancer is already an approved library (`technical-preferences.md`) specifically for this use case, and `HybridAnimancerComponent` avoids all AnimatorOverrideController allocations while providing a clean co-existence API.

### Alternative 2: PlayableGraph API (Manual)
- **Description**: Bypass both Animancer and TDE's Animator Controller. Build a custom `PlayableGraph` with `AnimationClipPlayable` nodes, blending between locomotion and attack clips manually.
- **Pros**: Maximum control; no third-party dependency.
- **Cons**: PlayableGraph API is verbose and error-prone (manual graph construction, output connection, weight management, `KeepChildrenConnected` management). Animancer 8.4 is a well-maintained abstraction over this exact API. PlayableGraph lifetime management during scene unload requires careful cleanup.
- **Rejection Reason**: Animancer 8.4 is already installed and solves this exact problem. Building a custom PlayableGraph layer is scope-expanding work with no added benefit — it would be a lower-quality reimplementation of what `HybridAnimancerComponent` already provides.

### Alternative 3: AnimationAdapter Wrapper (Decoupling Layer)
- **Description**: Introduce an `IAnimationPlayer` interface between C2 ComboPlayer and Animancer, allowing the animation backend to be swapped without modifying C2.
- **Pros**: Future-proof — if Animancer is replaced, C2 does not change.
- **Cons**: YAGNI. The project has committed to Animancer as an allowed library. The indirection adds complexity for a hypothetical future.
- **Rejection Reason**: Over-engineering for the current scope. Animancer is the approved library; direct dependency is acceptable. If Animancer is replaced in the future, `AnimancerAttackPlayer` is the single integration point — only one file changes.

## Consequences

### Positive
- Attack animations are BPM-congruent at any level BPM — Pillar 2 is structurally enforced, not just documented
- `HybridAnimancerComponent` provides zero-allocation clip swapping per attack node (no AnimatorOverrideController churn)
- TDE's Animator Controller remains untouched — locomotion, death, and respawn animations are not affected
- `KeepChildrenConnected = true` prevents `AnimatorControllerPlayable` disconnection artifacts between attacks
- `AttackNodeSO.animationClip` has a clear runtime consumer (AnimancerAttackPlayer)
- Death-during-attack is handled cleanly: `PlayController()` hands back to TDE, allowing death animation to play; no ghost attack animation continues
- `PlayController()` (not `Stop()`) ensures TDE's Animator Controller is never paused by Animancer

### Negative
- Adds Animancer as a runtime coupling in CombatCore assembly. TDE upgrades are independent, but Animancer upgrades require re-verification of `HybridAnimancerComponent` behavior with the updated TDE version.
- `authoredBpm` is a new required field on `AttackNodeSO`. All future AttackNodeSO assets must set a valid value. Odin Validator CI gate will fail if any asset has `_authoredBpm` ≤ 0.
- The `loadedClip` parameter in `PlayAttack()` requires the caller (C2) to manage Addressables loading. This creates an implicit pre-condition on every attack call. Failure to pre-load causes a NullReferenceException.
- The BPM speed formula assumes the Animancer layer's own `Speed` property is 1.0. A future slow-motion system that modifies layer speed would cause BPM desync unless coordinated with `AnimancerAttackPlayer`.

### Risks
- **MEDIUM: AnimancerState.OnEnd + combo input same-frame race.** If a combo input arrives on the exact frame the clip ends, both the `OnEnd` callback and the input callback fire in the same frame. `ComboPlayer` (C2) must guard against double-advance in this situation. **Mitigation**: C2 GDD must specify that the combo advance path is idempotent — advancing twice on the same frame produces the same result as advancing once.
- **MEDIUM: Mid-attack BPM change (post-MVP Boss phase).** If a Boss phase change sets a new BPM while an attack is in progress, the attack speed is stale until the next attack. For MVP (fixed BPM per level), this is a non-issue. **Mitigation**: `UpdateBpm()` method is defined but not called in MVP path; Boss phase GDD will specify when to call it.
- **LOW: Animancer version upgrade.** Animancer 8.4 APIs (`HybridAnimancerComponent`, `PlayController()`, `AnimancerState.Speed`, `Events.OnEnd`) are stable public API. Breaking changes in future Animancer versions would require updating `AnimancerAttackPlayer` only — single integration point. **Mitigation**: Pin Animancer version; test before upgrading.
- **LOW: `authoredBpm` default value (120f) causes subtle bugs.** If an animator creates a clip at a BPM other than 120 but forgets to update `authoredBpm`, the attack will play at incorrect speed. **Mitigation**: Odin Validator warning when `authoredBpm` is at default value and the clip name does not contain "120" (advisory, not blocking).

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| game-concept.md (F1) | TR-concept-004: Animancer BPM-adaptive animation speed scaling for attack clips | AnimancerAttackPlayer.PlayAttack() sets state.Speed = currentBpm / authoredBpm at every attack start |
| game-concept.md (F1) | Pillar 2: Wuxia Feel — Animancer variable speed for BPM adaptation, preserving martial arts animation quality | BPM speed-scaling ensures attack clip duration matches beat grid; clip content (poses, weight, impact) is unchanged — only playback rate adapts |
| technical-preferences.md | Forbidden pattern: no hardcoded attack animation frame count; use AnimancerState.Speed | AnimancerAttackPlayer is the sole driver of AnimancerState.Speed for attack clips; no Animator Controller speed multiplier used |
| so-data-layer.md (F4) | AttackNodeSO.animationClip (AssetReferenceT<AnimationClip>) — runtime consumer undefined | AnimancerAttackPlayer consumes the loaded clip via PlayAttack(node, loadedClip, onComplete) |
| audio-beat-clock.md (F2) | BeatClock provides CurrentBpm as read-only pull interface | AnimancerAttackPlayer reads BeatClock.CurrentBpm once per PlayAttack() call — no subscription needed |
| adr-d1-tde-integration.md | TDE Animator layer must not be modified; TDE Death/Respawn events must function | HybridAnimancerComponent operates as overlay; PlayController() returns control to TDE on attack end and death; TDE death animation plays normally |

## Performance Implications
- **CPU**: `AnimancerState.Speed` assignment is a single float write (< 0.01ms). `_animancer.Play(clip)` with `HybridAnimancerComponent` is O(1) — `KeepChildrenConnected = true` avoids the reconnect cost on every attack start. No per-frame work after attack start until OnEnd fires. `PlayController()` is a single weight transfer (~0.01ms).
- **Memory**: `AnimancerState` is pooled by Animancer — no allocation per `Play()` call after warm-up. `OnEnd` callback as method group avoids delegate allocation (IL2CPP compliant). `_pendingOnComplete` cached as field — no boxing.
- **Load Time**: `HybridAnimancerComponent.Awake()` setup (including PlayableGraph creation and `ControllerState` wrapping): < 0.5ms. No change to scene load time.
- **Network**: None (single-player).

## Migration Plan
Greenfield implementation — no existing animation pipeline code to migrate.

Implementation sequence:

1. **F4 Schema Update**:
   - Add `_authoredBpm: float = 120f` field to `AttackNodeSO.cs`
   - Add Odin Attribute validation in `#if UNITY_EDITOR` block: `[MinValue(1f), MaxValue(240f)]`
   - Add non-zero validation to the Odin Validator `RegisterValidator` class
   - Update `design/gdd/so-data-layer.md` F4-S1 AttackNodeSO field table

2. **Character Prefab Setup**:
   - Replace or add `HybridAnimancerComponent` to Character prefab (alongside existing `Animator`)
   - Verify TDE locomotion works normally with `HybridAnimancerComponent` present (no attack triggered)

3. **AnimancerAttackPlayer Implementation**:
   - Implement `AnimancerAttackPlayer.cs` per Key Interfaces section
   - Wire Inspector references: `HybridAnimancerComponent`, `BeatClock`
   - Wire `MMEventListener<TopDownEngineEvent>` subscription for death handling

4. **Integration Testing**:
   - EditMode: Mock BeatClock (CurrentBpm = 120, 160, 80) → verify `state.Speed` values match formula
   - PlayMode: Character prefab — verify TDE locomotion runs normally when no attack is playing (HybridAnimancerComponent present but idle)
   - PlayMode: Trigger attack → verify clip plays at correct speed → verify `PlayController()` resumes TDE locomotion after OnEnd
   - PlayMode: Death during attack → verify no OnEnd callback fires; verify TDE death animation plays; verify no ghost attack frame

## Validation Criteria
1. **Compile**: `AnimancerAttackPlayer.cs` compiles without errors; `AttackNodeSO.cs` with new `_authoredBpm` field compiles without errors
2. **Hybrid mode coexistence**: Character with both TDE Animator Controller and `HybridAnimancerComponent` runs TDE locomotion animations with zero interference when no attack is playing
3. **BPM speed scaling**: At `authoredBpm = 120`:
   - `currentBpm = 120` → `state.Speed = 1.000` (exact authored speed)
   - `currentBpm = 90` → `state.Speed = 0.750` (slower for lower BPM)
   - `currentBpm = 150` → `state.Speed = 1.250` (faster for higher BPM)
4. **OnEnd fires**: After `PlayAttack()`, when clip completes at rated speed, `onComplete` callback is invoked exactly once
5. **PlayController restores TDE**: After `OnEnd` fires and `PlayController()` is called, TDE's Animator Controller output is active — locomotion animation plays
6. **Death cancels attack**: Broadcast `TopDownEngineEvent(PlayerDeath)` during active attack → verify `_animancer.IsPlaying == false` (controller state active); verify `onComplete` was NOT called; verify TDE death animation plays
7. **TDE parameters unaffected**: During and after Animancer attack, TDE Animator Controller parameters (movement speed, dash trigger, etc.) continue to update correctly
8. **Odin validation**: `AttackNodeSO` with `_authoredBpm = 0` → Odin Validator reports Error; `_authoredBpm = 120` → passes validation

## Related Decisions
- `docs/architecture/adr-d1-tde-integration.md` — TDE Integration; TDE Animator Controller layer is NOT modified by this ADR; death event handling pattern replicated
- `design/gdd/so-data-layer.md` — F4 GDD; `AttackNodeSO` gains `authoredBpm` field per this ADR
- `design/gdd/game-concept.md` — Pillar 2 (Wuxia Feel) and TR-concept-004 (BPM animation scaling)
- ADR-D2 (Enemy AI Framework) — enemy characters will use the same AnimancerAttackPlayer pattern for their beat-locked attack animations
