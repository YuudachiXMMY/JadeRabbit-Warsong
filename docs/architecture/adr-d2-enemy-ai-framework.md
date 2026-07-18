# ADR-D2: Enemy AI Framework

## Status
Proposed

## Date
2026-07-18

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (6000.3.19f1) |
| **Domain** | AI / Navigation |
| **Knowledge Risk** | MEDIUM — Behavior Designer (Opsive) is a third-party package; LLM training data reflects an older BD version. Unity Behavior Package (com.unity.behavior) was excluded due to HIGH post-cutoff risk with no verified API reference in engine docs. |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `docs/engine-reference/unity/modules/navigation.md`, `docs/engine-reference/unity/plugins/top-down-engine.md`, `docs/architecture/adr-d1-tde-integration.md` |
| **Post-Cutoff APIs Used** | `CharacterMovement.SetMovement(Vector2)` (TDE 4.5 — verified source exists at `Assets/TopDownEngine/…/CharacterAbilities/CharacterMovement.cs:200`). `BehaviorTree.SetVariableValue()` (Behavior Designer — **unverified, pending BD M0 Spike**). |
| **Verification Required** | 1. **BD M0 Spike (BLOCKING before Accept)**: Import Behavior Designer; confirm `SharedVariable<T>` class hierarchy, `BehaviorTree.SetVariableValue()` / `GetVariable()` signatures, and `[SharedVariable]` attribute usage scope by reading BD source. 2. Confirm BD compiles in Unity 6.3 LTS with no `[SerializeField]` fields-only violations. 3. Confirm `CharacterMovement.SetMovement(Vector2)` on enemy prefab accepts 2D movement vectors without z-axis mapping bugs. 4. Set Script Execution Order: `BeatClockBridge` must execute before BD `BehaviorTree` tick in the same frame. |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-D1 (TDE Integration — Accepted; enemy AI must use TDE lifecycle hooks and health system defined in D1; `TDELifecycleAdapter` is the authoritative controller for scene/death/respawn events that enemy AI must respect) |
| **Enables** | FE1 GDD (`design/gdd/enemy-ai.md` — blocked until this ADR is Accepted) |
| **Blocks** | FE1 GDD authoring and implementation |
| **Ordering Note** | F4 GDD (EnemyInfoSO schema) should be finalized before FE1 GDD authors enemy data contracts, but this ADR can be Accepted independently before F4 GDD is Approved. C4 GDD (CombatDirector) must define the attack token grant protocol that `CombatDirectorTokenTask` depends on; C4 and FE1 GDDs should be authored concurrently with explicit interface alignment. |

## Context

### Problem Statement

FE1 (Enemy AI System) requires an AI behavior framework decision before its GDD can be authored. The game's core mechanic (game-concept.md Rule 4) requires all enemy attacks to be beat-locked via CombatDirector (C4) token requests. This requirement has two architectural implications:

1. Enemy AI must be capable of **asynchronous waiting** — blocking its attack branch until C4 grants a beat-slot token (or timing out). TDE's built-in `AIBrain`/`AIDecision` system uses a polling-based flat state machine that cannot implement clean asynchronous token-wait behavior.

2. Enemy AI must consume beat state data **without coupling directly to BeatClock**. The `beatclock_lifecycle_bypass` forbidden pattern (architecture.yaml) and the `beat_clock_lifecycle` state ownership constraint (owned exclusively by `TDELifecycleAdapter`) mean AI must receive beat data via an injected data bus, not by reading BeatClock directly.

The choice of AI framework determines the entire enemy AI development pipeline (BT authoring tooling, Blackboard data bus, TDE movement integration) and must be made before FE1 GDD specifies any behavioral schemas.

Three viable options were evaluated: Unity Behavior Package (Unity 6 first-party), Behavior Designer (Opsive, already purchased and noted in game-concept.md Open Questions), and a custom C# BT.

### Constraints

- **ADR-D1 boundary**: Enemy characters use TDE `TopDownController` for movement and `Health.Damage()` for damage receipt. AI must not modify TDE source code; integration via subclassing and event subscription only.
- **Beat-lock constraint** (game-concept.md Rule 4 / TR-concept-009): All enemy attack decisions must go through CombatDirector (C4) beat-slot token requests. AI must NOT call `BeatClock` directly.
- **Registry forbidden pattern** `beatclock_lifecycle_bypass`: No system except `TDELifecycleAdapter` may call `BeatClock.Play()` / `Stop()` / `Pause()` / `Resume()`.
- **Registry state constraint** `beat_clock_lifecycle`: Other systems may only read beat state via `BeatStateSnapshot` subscription — not by calling BeatClock lifecycle methods.
- **TDE AIBrain incompatibility**: TDE's built-in `AIAction` and `AIDecision` components depend on `AIBrain`'s `PerformActions()` loop. If `AIBrain` is replaced by BD `BehaviorTree`, TDE's stock AI components cannot be reused. The ADR decision entails building custom BD Action tasks for all AI behaviors.

### Requirements

- Support behavior-tree-driven enemies as specified in MVP (game-concept.md: "普通敌人 1 种（行为树驱动）")
- Enable beat-locked attack token requests to CombatDirector without direct rhythm system coupling (TR-concept-009)
- Provide a Blackboard data bus for runtime beat state injection to AI tasks
- Support MVP scope: 1 enemy type (patrol/chase/beat-locked attack loop) + 1 Boss (phase-based rhythm design with sub-tree switching)
- Integrate with TDE's `CharacterMovement` and `TopDownController` for movement execution (no TDE source modification)
- Framework must compile in Unity 6.3 LTS (6000.3.19f1)

## Decision

**Behavior Designer (Opsive)** is adopted as the enemy AI behavior framework for all enemy characters.

Behavior Designer (BD) is already purchased for this project (game-concept.md Open Questions: "Behavior Designer（已购入但未用）"). It provides a mature visual BT editor, a typed Blackboard (SharedVariable) system, and Action Task APIs compatible with TDE integration. It avoids the HIGH post-cutoff risk of the Unity Behavior Package, and eliminates the development overhead of a custom C# BT for a project that already carries TDE + FMOD + EasySave verification debt.

BD replaces TDE `AIBrain` as the high-level behavior controller on all enemy prefabs. TDE's stock `AIAction` and `AIDecision` components are **not used** — all behavior is authored as custom BD Action Tasks.

### Architecture Diagram

```
BeatClock (F2)
    │  BeatStateSnapshot event (each beat)
    ▼
BeatClockBridge : MonoBehaviour       [on enemy prefab, same GO as BehaviorTree]
    │  BehaviorTree.SetVariableValue("CurrentBPM" / "CurrentBeat" / "TimeToNextBeat")
    ▼
BD Blackboard (SharedVariable store)
    │  read by AI Action Tasks
    ▼
Behavior Designer BehaviorTree
    ├─ Patrol sub-tree
    │      └─ TDESetMovementTask → CharacterMovement.SetMovement(Vector2)
    ├─ Chase sub-tree
    │      └─ TDESetMovementTask → CharacterMovement.SetMovement(playerDir)
    └─ Attack sub-tree
           ├─ CombatDirectorTokenTask  [blocks BT — Running/Success/Failure]
           │      │  on Success: token granted for this beat
           │      ▼
           └─ TDETriggerAbilityTask → CharacterAbility.TriggerAbility()
                                        │
                                        ▼
                              TDE Health.Damage() on target
```

### Key Interfaces

```csharp
/// <summary>
/// Bridge between BeatClock event bus and BD Blackboard.
/// Placed on enemy prefab alongside BehaviorTree component.
/// Script Execution Order: must execute BEFORE BD BehaviorTree tick.
/// </summary>
public class BeatClockBridge : MonoBehaviour
{
    // Reference to the BD BehaviorTree on the same GameObject
    [SerializeField] private BehaviorTree _behaviorTree;

    private void OnEnable()  => BeatClock.OnBeatSnapshot += HandleBeatSnapshot;
    private void OnDisable() => BeatClock.OnBeatSnapshot -= HandleBeatSnapshot;

    private void HandleBeatSnapshot(BeatStateSnapshot snapshot)
    {
        // Write to BD Blackboard via BehaviorTree.SetVariableValue()
        // NOTE: [SharedVariable] attribute is only valid inside BD Task classes,
        //       NOT on MonoBehaviour fields. Use SetVariableValue() from outside.
        _behaviorTree.SetVariableValue("CurrentBPM",       snapshot.Bpm);
        _behaviorTree.SetVariableValue("CurrentBeat",      snapshot.Beat);
        _behaviorTree.SetVariableValue("TimeToNextBeat",   snapshot.TimeToNextBeat);
    }
}

/// <summary>
/// BD Action Task: requests a beat-slot attack token from CombatDirector (C4).
/// Blocks the BT subtree until token is granted or timeout elapses.
/// Token grant protocol defined by C4 GDD (combat-director.md — not yet authored).
/// </summary>
public class CombatDirectorTokenTask : Action
{
    // Read from Blackboard — timeout in number of beats before cancelling
    public SharedFloat TimeoutBeats;

    // Internal state
    private bool  _tokenRequested;
    private bool  _tokenGranted;
    private float _timeoutTimer;

    public override void OnStart()
    {
        _tokenRequested = false;
        _tokenGranted   = false;
        _timeoutTimer   = 0f;
        // Request: protocol TBD in C4 GDD; expected: CombatDirector.RequestAttackToken(this)
    }

    public override TaskStatus OnUpdate()
    {
        if (_tokenGranted) return TaskStatus.Success;
        _timeoutTimer += Time.deltaTime;
        if (_timeoutTimer >= TimeoutBeats.Value * (60f / CurrentBPMFromBlackboard()))
            return TaskStatus.Failure;
        return TaskStatus.Running;
    }

    // Called by CombatDirector when token is granted for this enemy's request
    public void OnTokenGranted() => _tokenGranted = true;
}

/// <summary>
/// BD Action Task: delegates movement to TDE CharacterMovement component.
/// Uses CharacterMovement.SetMovement(Vector2) — the recommended TDE entry point
/// that internally handles 2D/3D coordinate mapping (avoids TopDownController
/// Vector3 z-axis remapping bug in 2D scenes).
/// </summary>
public class TDESetMovementTask : Action
{
    public SharedVector2 Direction;

    private CharacterMovement _characterMovement;

    public override void OnAwake()
    {
        _characterMovement = GetComponent<CharacterMovement>();
    }

    public override TaskStatus OnUpdate()
    {
        if (_characterMovement == null) return TaskStatus.Failure;
        _characterMovement.SetMovement(Direction.Value);
        return TaskStatus.Success;
    }
}

/// <summary>
/// BD Action Task: triggers a named TDE CharacterAbility on the enemy.
/// Used after CombatDirectorTokenTask returns Success.
/// </summary>
public class TDETriggerAbilityTask : Action
{
    public SharedString AbilityName;  // e.g. "CharacterHandleWeapon"
    // Calls CharacterAbility.TriggerAbility() — no TDE source modification
}
```

### Boundary Contract (TDE AI vs BD AI)

| Concern | Owner | Not Owner |
|---------|-------|-----------|
| High-level behavior decision (patrol/chase/attack) | BD BehaviorTree | ~~TDE AIBrain~~ (not present on enemies) |
| Movement execution | TDE `CharacterMovement.SetMovement()` via `TDESetMovementTask` | ~~TDE AIAction~~ (not used; requires AIBrain) |
| Attack gating (beat-lock) | `CombatDirectorTokenTask` | ~~direct BeatClock read~~ (forbidden) |
| Attack execution | TDE `CharacterAbility.TriggerAbility()` via `TDETriggerAbilityTask` | ~~TDE AIDecision~~ (not used; requires AIBrain) |
| Beat state data | BD Blackboard written by `BeatClockBridge` | ~~BeatClock direct call~~ (forbidden by registry) |
| Death / respawn lifecycle | TDE `Health` + `TDELifecycleAdapter` events | BD BT (listens, does not control) |
| Damage receipt | TDE `Health.Damage()` | ~~direct field write~~ (forbidden by ADR-D1) |

**Explicit rule**: Enemy prefabs do NOT include `AIBrain`, `AIAction`, or `AIDecision` components. BD `BehaviorTree` is the sole high-level AI driver.

## Alternatives Considered

### Alternative A: Unity Behavior Package (com.unity.behavior)

- **Description**: Unity 6 first-party BT authoring tool with built-in Blackboard and visual graph editor. Available as `com.unity.behavior` package in Unity 6.0+.
- **Pros**: No external vendor dependency; Unity-native; no additional licensing cost; likely long-term support trajectory if Unity commits to it.
- **Cons**: HIGH post-cutoff risk — `com.unity.behavior` was introduced in Unity 6.0 (post-LLM-cutoff) and has no verified API reference in `docs/engine-reference/`. Package was in preview/experimental state as of Unity 6.0-6.1. Adding another unverified post-cutoff API on top of TDE + FMOD + EasySave verification debt creates unacceptable spike risk for MVP timeline.
- **Rejection Reason**: Behavior Designer is already purchased and addresses the same requirements without post-cutoff API risk. Unity Behavior Package can be reconsidered for a future project or if BD proves problematic after M0 Spike.

### Alternative B: Custom C# Behavior Tree

- **Description**: Pure C# hierarchical BT implementation. Game-concept.md names this "v1 自研 AIBT".
- **Pros**: Zero external dependencies; full architectural control over BT node types and Blackboard schema; no licensing concerns.
- **Cons**: Behavior Designer is already purchased — a custom BT is sunk cost duplication. Custom BT requires implementing Blackboard, composites, decorators, serialization, and visual debugging tooling from scratch. For MVP scope (1 enemy type), the overhead is disproportionate.
- **Rejection Reason**: No benefit over Behavior Designer for this project's scope. Custom BT is appropriate only when BT requirements are uniquely constrained (e.g., requiring custom composites that BD does not support) — not the case here.

### Alternative C: TDE AIBrain (Built-in)

- **Description**: TDE's native AI layer: `AIBrain`, `AIAction` implementations (e.g., `AIActionMoveTowardsTarget3D`), `AIDecision` implementations, and `AIState` containers.
- **Pros**: Already part of TDE; zero additional packages; simple integration with TDE's character system; immediate availability.
- **Cons**: TDE AIBrain is a flat state machine, not a hierarchical BT. Beat-locked attack token requests require asynchronous blocking behavior (`Running` / `Success` / `Failure` states) within a single state — TDE `AIDecision` polling cannot implement this cleanly. No built-in Blackboard for beat state injection. Boss phase transitions (sub-tree switching) are architecturally incompatible with AIBrain's flat state model.
- **Rejection Reason**: game-concept.md MVP definition explicitly specifies "行为树驱动" for enemies. TDE AIBrain cannot implement the CombatDirector beat-token wait protocol without significant workarounds that negate its simplicity advantage.

## Consequences

### Positive

- BD's mature SharedVariable Blackboard cleanly implements beat state injection without custom infrastructure
- Visual BT graph editor (BD) accelerates FE1 GDD prototyping iteration for 1 enemy type + Boss
- `CombatDirectorTokenTask` as a first-class BT node enforces beat-lock at the architecture level: no attack fires without C4 token
- BD's hierarchical BT supports Boss phase transitions (sub-tree switching) without flat state machine workarounds
- Purchase is already made — no additional licensing cost at adoption

### Negative

- Behavior Designer is an additional third-party package that must remain compatible with Unity version updates (alongside TDE, FMOD, EasySave, Odin, Animancer, Feel, DOTween)
- BD `BehaviorTree` replaces TDE `AIBrain`, meaning TDE's built-in `AIAction` and `AIDecision` stock components cannot be reused on enemies; all AI tasks must be custom BD Action Tasks
- BD graph asset files may produce git merge conflicts if multiple team members edit the same BT graph simultaneously
- `CombatDirectorTokenTask` interface assumes a C4 token-grant protocol that is TBD until C4 GDD is authored — FE1 GDD cannot fully specify attack behavior until C4 contract is established

### Risks

- **BD Unity 6.3 LTS compatibility (HIGH — must verify before Accept)**: Behavior Designer has not yet been imported into this project. Unity 6.3 LTS's `[SerializeField]` fields-only breaking change may affect BD's `SharedVariable` serialization. **Mitigation**: BD M0 Spike (see Verification Required) must be completed before this ADR can be Accepted. Import BD, confirm compilation, read source for SharedVariable hierarchy.
- **`BehaviorTree.SetVariableValue()` signature drift (MEDIUM)**: BD's Blackboard write API may differ from LLM training data. **Mitigation**: BD M0 Spike confirms actual method signature before `BeatClockBridge` code is written.
- **Script Execution Order (MEDIUM)**: If BD `BehaviorTree` ticks before `BeatClockBridge` writes the latest beat data in the same frame, AI reads a one-frame-stale beat snapshot. **Mitigation**: Set Script Execution Order in Project Settings so `BeatClockBridge` executes before BD `BehaviorTree`. Document the ordering in FE1 GDD's implementation notes.
- **CombatDirector protocol undefined (MEDIUM)**: `CombatDirectorTokenTask` depends on C4's token grant interface, which does not exist yet. **Mitigation**: C4 GDD (combat-director.md) must define the `RequestAttackToken(enemy)` / `OnTokenGranted()` protocol to match the interface contract declared in this ADR. Flag as joint authorship requirement in FE1 GDD.
- **CharacterMovement API on non-player characters (LOW)**: `CharacterMovement.SetMovement(Vector2)` is the recommended TDE movement entry point, verified at `CharacterMovement.cs:200`. However, enemy characters may require different movement ability setups than player characters. **Mitigation**: Confirm `CharacterMovement` component exists on enemy prefabs during FE1 GDD implementation; fallback to `TopDownController.SetMovement(new Vector3(dir.x, 0f, dir.y))` if not.

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| game-concept.md | TR-concept-009: "Enemy beat-lock: all enemy attacks forced on-beat with beat-unit windups" | `CombatDirectorTokenTask` is the architectural enforcement point — no enemy attack ability fires without a CombatDirector beat-slot token grant. Beat-lock is structural, not optional. |
| game-concept.md | Rule 4: 所有敌人攻击前摇以拍为单位，由 CombatDirector 令牌分配确保落拍 | `CombatDirectorTokenTask.TimeoutBeats` encodes the windup duration in beats. The task blocks the attack BT subtree until the correct beat arrives. |
| game-concept.md | MVP: "普通敌人 1 种（行为树驱动）" | BD `BehaviorTree` provides the BT authoring infrastructure for FE1's 1 enemy type. |
| game-concept.md | Boss 1 个（阶段化节奏设计）| BD's hierarchical BT + sub-tree switching supports Boss phase transitions without flat state machine workarounds. |
| game-concept.md | Key Dynamic: "玩家在 Boss 战中用耳朵「听前摇」" | `CombatDirectorTokenTask.TimeoutBeats` enforces a fixed beat-unit windup, making enemy attack timing audibly predictable. |

## Performance Implications

- **CPU**: BD BT evaluation is O(active nodes) per tick. For MVP (≤5 concurrent enemies), estimated overhead <0.5ms/frame. Well within remaining frame budget. `BeatClockBridge.HandleBeatSnapshot()` fires at most 4–8 times/second (per enemy, per BPM); negligible cost.
- **Memory**: BD `BehaviorTree` asset + per-instance Blackboard per enemy. Estimate ~50KB per enemy at runtime. Acceptable for MVP scope.
- **Load Time**: BD graph assets loaded at scene load. Negligible for MVP (3–5 hand-crafted rooms).
- **Network**: N/A (single-player).

## Migration Plan

No existing AI code to migrate. FE1 GDD is the first enemy AI specification document. This ADR establishes the framework contracts before FE1 GDD authoring begins.

## Validation Criteria

1. Behavior Designer imports into Unity 6.3 LTS (6000.3.19f1) with zero compile errors (BD M0 Spike gate)
2. `SharedVariable<T>` class hierarchy confirmed by source inspection; `BehaviorTree.SetVariableValue()` signature matches implementation in `BeatClockBridge`
3. `BeatClockBridge.HandleBeatSnapshot()` fires and updates all three Blackboard values within the same frame as each BeatClock beat event
4. BD `BehaviorTree` on enemy prefab evaluates correctly with no TDE `AIBrain` component present (no NullReferenceException at play-mode entry)
5. `CombatDirectorTokenTask` returns `Running` while waiting for a token, `Success` on grant, `Failure` on timeout
6. Enemy attack ability (`CharacterAbility.TriggerAbility()`) never fires without a preceding `CombatDirectorTokenTask` `Success` result
7. `TDESetMovementTask` moves enemy using `CharacterMovement.SetMovement(Vector2)` — enemy moves in all 4 directions without z-axis mapping issues
8. Script Execution Order: `BeatClockBridge` executes before BD `BehaviorTree` tick in the same Update frame (verified by adding frame-count log to both)

## Related Decisions

- **ADR-D1** (`adr-d1-tde-integration.md`): TDE lifecycle hooks and `TDELifecycleAdapter` that enemy AI must observe for death/respawn events
- **ADR-D6** (`adr-d6-audio-middleware.md`): FMOD — upstream source for BeatClock's DSP timestamps that ultimately feed `BeatClockBridge`
- **C4 GDD** (`design/gdd/combat-director.md` — not yet authored): defines the `RequestAttackToken()` / `OnTokenGranted()` protocol that `CombatDirectorTokenTask` depends on; must be co-authored with FE1 GDD
- **FE1 GDD** (`design/gdd/enemy-ai.md` — blocked until this ADR is Accepted): specifies enemy behavior schemas, BT graph structure, and EnemyInfoSO integration
