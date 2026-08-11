# ADR-D2: Enemy AI Framework

## Status
Accepted

## Date
2026-07-18

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (6000.3.19f1) |
| **Domain** | AI / Navigation |
| **Knowledge Risk** | LOW — `com.unity.behavior` is a Unity first-party package. M0 Spike (2026-07-18) verified all key APIs from source code with zero BLOCKERs. No third-party compatibility risk. |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `docs/engine-reference/unity/modules/navigation.md`, `docs/engine-reference/unity/plugins/top-down-engine.md`, `docs/architecture/adr-d1-tde-integration.md`, `docs/architecture/m0-spike-unity-behavior.md` |
| **Post-Cutoff APIs Used** | `BehaviorGraphAgent.SetVariableValue<T>()` (com.unity.behavior v1.0.16 — **verified** at `Library/PackageCache/com.unity.behavior@.../Runtime/Execution/Components/BehaviorGraphAgent.cs:541`). `CharacterMovement.SetMovement(Vector2)` (TDE 4.5 — **verified** at `Assets/TopDownEngine/.../CharacterAbilities/CharacterMovement.cs:200`). |
| **Verification Required** | ✅ COMPLETED (M0 Spike 2026-07-18 PASS — 0 BLOCKER). Runtime validation pending during FE1 GDD playmode tests: confirm `BeatClockBridge` write → BT tick ordering in-editor. |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-D1 (TDE Integration — Accepted; enemy AI must use TDE lifecycle hooks and health system defined in D1; `TDELifecycleAdapter` is the authoritative controller for scene/death/respawn events that enemy AI must respect) |
| **Enables** | FE1 GDD (`design/gdd/enemy-ai.md` — blocked until this ADR is Accepted) |
| **Blocks** | FE1 GDD authoring and implementation |
| **Ordering Note** | F4 GDD (EnemyInfoSO schema) should be finalized before FE1 GDD authors enemy data contracts, but this ADR can be Accepted independently before F4 GDD is Approved. C4 GDD (CombatDirector) must define the attack token grant protocol that `CombatDirectorTokenAction` depends on; C4 and FE1 GDDs should be authored concurrently with explicit interface alignment. |

## Context

### Problem Statement

FE1 (Enemy AI System) requires an AI behavior framework decision before its GDD can be authored. The game's core mechanic (game-concept.md Rule 4) requires all enemy attacks to be beat-locked via CombatDirector (C4) token requests. This requirement has two architectural implications:

1. Enemy AI must be capable of **asynchronous waiting** — blocking its attack branch until C4 grants a beat-slot token (or timing out). TDE's built-in `AIBrain`/`AIDecision` system uses a polling-based flat state machine that cannot implement clean asynchronous token-wait behavior.

2. Enemy AI must consume beat state data **without coupling directly to BeatClock**. The `beatclock_lifecycle_bypass` forbidden pattern (architecture.yaml) and the `beat_clock_lifecycle` state ownership constraint (owned exclusively by `TDELifecycleAdapter`) mean AI must receive beat data via an injected data bus, not by reading BeatClock directly.

The choice of AI framework determines the entire enemy AI development pipeline (BT authoring tooling, Blackboard data bus, TDE movement integration) and must be made before FE1 GDD specifies any behavioral schemas.

Three viable options were evaluated: Unity Behavior Package (Unity 6 first-party), Behavior Designer (Opsive, purchased but unused), and a custom C# BT.

### Constraints

- **ADR-D1 boundary**: Enemy characters use TDE `TopDownController` for movement and `Health.Damage()` for damage receipt. AI must not modify TDE source code; integration via subclassing and event subscription only.
- **Beat-lock constraint** (game-concept.md Rule 4 / TR-concept-009): All enemy attack decisions must go through CombatDirector (C4) beat-slot token requests. AI must NOT call `BeatClock` directly.
- **Registry forbidden pattern** `beatclock_lifecycle_bypass`: No system except `TDELifecycleAdapter` may call `BeatClock.Play()` / `Stop()` / `Pause()` / `Resume()`.
- **Registry state constraint** `beat_clock_lifecycle`: Other systems may only read beat state via `BeatStateSnapshot` subscription — not by calling BeatClock lifecycle methods.
- **TDE AIBrain incompatibility**: TDE's built-in `AIAction` and `AIDecision` components depend on `AIBrain`'s `PerformActions()` loop. If `AIBrain` is replaced by `BehaviorGraphAgent`, TDE's stock AI components cannot be reused. The ADR decision entails building custom Unity Behavior Action nodes for all AI behaviors.

### Requirements

- Support behavior-tree-driven enemies as specified in MVP (game-concept.md: "普通敌人 1 种（行为树驱动）")
- Enable beat-locked attack token requests to CombatDirector without direct rhythm system coupling (TR-concept-009)
- Provide a Blackboard data bus for runtime beat state injection to AI action nodes
- Support MVP scope: 1 enemy type (patrol/chase/beat-locked attack loop) + 1 Boss (phase-based rhythm design with sub-tree switching)
- Integrate with TDE's `CharacterMovement` and `TopDownController` for movement execution (no TDE source modification)
- Framework must compile in Unity 6.3 LTS (6000.3.19f1)

## Decision

**Unity Behavior Package (`com.unity.behavior` v1.0.16)** is adopted as the enemy AI behavior framework for all enemy characters.

Unity Behavior is Unity's first-party hierarchical behavior tree authoring tool. It provides a visual graph editor, a typed `BlackboardVariable<T>` system, `EventChannel<T>` event-driven architecture, `ObserverAbortModifier` for reactive interruption, and `RunSubgraphDynamic` for Boss phase transitions. M0 Spike (2026-07-18) confirmed zero BLOCKERs and full API compatibility with Unity 6.3 LTS.

`BehaviorGraphAgent` replaces TDE `AIBrain` as the high-level behavior controller on all enemy prefabs. TDE's stock `AIAction` and `AIDecision` components are **not used** — all behavior is authored as custom Unity Behavior `Action` nodes.

### Architecture Diagram

```
BeatClock (F2)
    │  BeatStateSnapshot event (each beat)
    ▼
BeatClockBridge : MonoBehaviour       (no Update — writes on OnBeatSnapshot push)
    │  agent.SetVariableValue("CurrentBPM",       snapshot.Bpm)
    │  agent.SetVariableValue("CurrentBeat",      snapshot.Beat)
    │  agent.SetVariableValue("TimeToNextBeat",   snapshot.TimeToNextBeat)
    ▼
BehaviorGraphAgent [DefaultExecutionOrder(-50)]   [on enemy prefab]
    │  Blackboard: CurrentBPM (float), CurrentBeat (int), TimeToNextBeat (float)
    ▼
Unity Behavior Graph (visual editor)
    ├─ Patrol sub-tree (SequenceComposite)
    │      └─ TDESetMovementAction → CharacterMovement.SetMovement(Vector2)
    ├─ Chase sub-tree (SequenceComposite)
    │      └─ TDESetMovementAction → CharacterMovement.SetMovement(playerDir)
    └─ Attack sub-tree (SequenceComposite)
           ├─ CombatDirectorTokenAction  [Running until token / Failure on timeout]
           │      │  on Success: token granted for this beat
           │      ▼
           └─ TDETriggerAbilityAction → CharacterAbility.TriggerAbility()
                                          │
                                          ▼
                                TDE Health.Damage() on target
```

**Beat-data freshness guarantee** (from verified source):
- `BeatClockBridge` has **no `Update()`** — it writes the Blackboard synchronously inside `BeatClock`'s `OnBeatSnapshot` dispatch (event push, not a per-frame tick). A `[DefaultExecutionOrder]` on the Bridge therefore does **not** affect its timing (the attribute only orders `Update`/`FixedUpdate`/etc., which the Bridge does not implement).
- `BehaviorGraphAgent` runs at -50 (`[DefaultExecutionOrder(-50)]` — hardcoded in Unity Behavior source).
- The Blackboard always holds the **most recent beat snapshot** as of the last `OnBeatSnapshot` fire. A BT tick reads that snapshot. Because `BeatClock` fires on beat boundaries (not every frame), on non-beat frames the BT reads the previous beat's values — bounded staleness ≤ one beat interval, acceptable for MVP (BPM fixed per level via `LevelConfigSO`). If a future system needs same-frame beat precision, give `BeatClock` a more-negative `[DefaultExecutionOrder]` than `-50` so its beat-detection `Update` fires before the BT tick.

### Key Interfaces

```csharp
/// <summary>
/// Bridge between BeatClock event bus and Unity Behavior Blackboard.
/// Placed on enemy prefab alongside BehaviorGraphAgent component.
/// Writes beat data to the Blackboard synchronously on each BeatClock
/// OnBeatSnapshot event (push, not a per-frame tick). The BT reads the
/// latest snapshot on its next tick. No [DefaultExecutionOrder] needed —
/// the Bridge has no Update() for it to order.
/// </summary>
public class BeatClockBridge : MonoBehaviour
{
    [SerializeField] private BehaviorGraphAgent _agent;

    private void OnEnable()  => BeatClock.OnBeatSnapshot += HandleBeatSnapshot;
    private void OnDisable() => BeatClock.OnBeatSnapshot -= HandleBeatSnapshot;

    private void HandleBeatSnapshot(BeatStateSnapshot snapshot)
    {
        // Write to Unity Behavior Blackboard — verified API signature:
        // BehaviorGraphAgent.SetVariableValue<TValue>(string variableName, TValue value) : bool
        // Source: BehaviorGraphAgent.cs:541
        _agent.SetVariableValue("CurrentBPM",       snapshot.Bpm);
        _agent.SetVariableValue("CurrentBeat",      snapshot.Beat);
        _agent.SetVariableValue("TimeToNextBeat",   snapshot.TimeToNextBeat);
    }
}

/// <summary>
/// Unity Behavior Action node: requests a beat-slot attack token from CombatDirector (C4).
/// Blocks the BT subtree (Status.Running) until token is granted or timeout elapses.
/// Token grant protocol defined by C4 GDD (combat-director.md — not yet authored).
/// MUST be partial class — required by [GeneratePropertyBag] source generator.
/// </summary>
[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Request Attack Token",
    description: "Requests a beat-slot token from CombatDirector. Running until granted or timeout.",
    story: "[Agent] requests attack token (timeout: [TimeoutBeats] beats)",
    category: "Action/Combat",
    id: "a1b2c3d4-e5f6-7890-abcd-ef1234567890")]
internal partial class CombatDirectorTokenAction : Action
{
    // Bound to Blackboard variable in graph editor
    [SerializeReference] public BlackboardVariable<float> TimeoutBeats;
    [SerializeReference] public BlackboardVariable<float> CurrentBPM;

    private bool  _tokenGranted;
    private float _timeoutTimer;

    protected override Status OnStart()
    {
        _tokenGranted = false;
        _timeoutTimer = 0f;
        // Protocol TBD in C4 GDD: CombatDirector.RequestAttackToken(this)
        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (_tokenGranted) return Status.Success;
        _timeoutTimer += Time.deltaTime;
        float bpm = CurrentBPM != null ? CurrentBPM.Value : 120f;
        if (_timeoutTimer >= TimeoutBeats.Value * (60f / bpm))
            return Status.Failure;
        return Status.Running;
    }

    // Called by CombatDirector when token is granted for this enemy's request
    public void OnTokenGranted() => _tokenGranted = true;
}

/// <summary>
/// Unity Behavior Action node: delegates movement to TDE CharacterMovement component.
/// Uses CharacterMovement.SetMovement(Vector2) — verified TDE 4.5 entry point
/// that internally handles 2D/3D coordinate mapping.
/// MUST be partial class — required by [GeneratePropertyBag].
/// </summary>
[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "TDE Set Movement",
    description: "Sets enemy movement direction via TDE CharacterMovement.",
    story: "[Agent] moves in [Direction]",
    category: "Action/TDE",
    id: "b2c3d4e5-f6a7-8901-bcde-f12345678901")]
internal partial class TDESetMovementAction : Action
{
    [SerializeReference] public BlackboardVariable<Vector2> Direction;

    private CharacterMovement _characterMovement;

    protected override void OnSetup()
    {
        _characterMovement = GameObject.GetComponent<CharacterMovement>();
    }

    protected override Status OnUpdate()
    {
        if (_characterMovement == null) return Status.Failure;
        _characterMovement.SetMovement(Direction.Value);
        return Status.Success;
    }
}

/// <summary>
/// Unity Behavior Action node: triggers a named TDE CharacterAbility on the enemy.
/// Used after CombatDirectorTokenAction returns Status.Success.
/// MUST be partial class — required by [GeneratePropertyBag].
/// </summary>
[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "TDE Trigger Ability",
    description: "Triggers a named TDE CharacterAbility on the enemy.",
    story: "[Agent] triggers ability [AbilityName]",
    category: "Action/TDE",
    id: "c3d4e5f6-a7b8-9012-cdef-123456789012")]
internal partial class TDETriggerAbilityAction : Action
{
    [SerializeReference] public BlackboardVariable<string> AbilityName;
    // Calls CharacterAbility.TriggerAbility() — no TDE source modification
}
```

### Boundary Contract (TDE AI vs Unity Behavior AI)

| Concern | Owner | Not Owner |
|---------|-------|-----------|
| High-level behavior decision (patrol/chase/attack) | Unity Behavior `BehaviorGraphAgent` | ~~TDE AIBrain~~ (not present on enemies) |
| Movement execution | TDE `CharacterMovement.SetMovement()` via `TDESetMovementAction` | ~~TDE AIAction~~ (not used; requires AIBrain) |
| Attack gating (beat-lock) | `CombatDirectorTokenAction` | ~~direct BeatClock read~~ (forbidden) |
| Attack execution | TDE `CharacterAbility.TriggerAbility()` via `TDETriggerAbilityAction` | ~~TDE AIDecision~~ (not used; requires AIBrain) |
| Beat state data | Unity Behavior Blackboard written by `BeatClockBridge` | ~~BeatClock direct call~~ (forbidden by registry) |
| Death / respawn lifecycle | TDE `Health` + `TDELifecycleAdapter` events | `BehaviorGraphAgent` (listens via `End()`/`Restart()`, does not control) |
| Damage receipt | TDE `Health.Damage()` | ~~direct field write~~ (forbidden by ADR-D1) |

**Explicit rule**: Enemy prefabs do NOT include `AIBrain`, `AIAction`, or `AIDecision` components. `BehaviorGraphAgent` is the sole high-level AI driver.

## Alternatives Considered

### Alternative A: Behavior Designer (Opsive)

- **Description**: Third-party BT framework (Asset Store). Already purchased for this project. Provides visual BT editor, SharedVariable Blackboard, and Action Task APIs.
- **Pros**: Mature ecosystem; SharedVariable system for beat state injection; hierarchical BT supports Boss phase transitions; already purchased (no additional cost).
- **Cons**: Third-party package — adds a 9th external dependency alongside TDE, FMOD, EasySave, Odin, Animancer, Feel, DOTween, Hot Reload. Must remain compatible with each Unity LTS update independently. No BD M0 Spike was completed: `BehaviorTree.SetVariableValue()` API was unverified from training data. `[SerializeField]` fields-only breaking change (Unity 6.3 LTS) posed unknown risk to `SharedVariable` serialization. No first-party event channel system (EventChannel equivalent requires custom code or additional plugins).
- **Rejection Reason**: Unity Behavior Package provides equivalent functionality with zero additional vendor dependencies. M0 Spike confirmed Unity Behavior's API is production-ready in Unity 6.3 LTS with 0 BLOCKERs, while BD remained unverified. Reducing third-party dependency count reduces long-term maintenance risk.

### Alternative B: Custom C# Behavior Tree

- **Description**: Pure C# hierarchical BT implementation (game-concept.md: "v1 自研 AIBT").
- **Pros**: Zero external dependencies; full architectural control over BT node types and Blackboard schema; no licensing concerns.
- **Cons**: Unity Behavior is available at no cost — a custom BT is redundant effort. Custom BT requires implementing Blackboard, composites, decorators, serialization, and visual debugging tooling from scratch. For MVP scope (1 enemy type), the overhead is disproportionate.
- **Rejection Reason**: No benefit over Unity Behavior for this project's scope. Custom BT is appropriate only when BT requirements are uniquely constrained — not the case here.

### Alternative C: TDE AIBrain (Built-in)

- **Description**: TDE's native AI layer: `AIBrain`, `AIAction` implementations, `AIDecision` implementations, and `AIState` containers.
- **Pros**: Already part of TDE; zero additional packages; simple integration with TDE's character system.
- **Cons**: TDE AIBrain is a flat state machine, not a hierarchical BT. Beat-locked attack token requests require asynchronous blocking behavior (`Running` / `Success` / `Failure`) within a single state — TDE `AIDecision` polling cannot implement this cleanly. No Blackboard for beat state injection. Boss phase transitions architecturally incompatible with AIBrain's flat state model.
- **Rejection Reason**: game-concept.md MVP explicitly specifies "行为树驱动" for enemies. TDE AIBrain cannot implement the CombatDirector beat-token wait protocol without significant workarounds.

## Consequences

### Positive

- `BlackboardVariable<T>.OnValueChanged` event enables **reactive** beat state patterns — BT branches can respond immediately to beat changes rather than polling
- `ObserverAbortModifier` allows high-priority branches (e.g., death) to interrupt lower-priority ones (e.g., attack) without polling
- `RunSubgraphDynamic` provides clean Boss phase transitions between sub-behavior graphs
- `CombatDirectorTokenAction` as a first-class BT node enforces beat-lock at the architecture level: no attack fires without C4 token
- Beat state reaches the Blackboard via `BeatClock`'s `OnBeatSnapshot` event push (synchronous write), so the BT always reads the latest beat snapshot without any manual Project Settings execution-order configuration
- No additional licensing cost; eliminates one potential third-party dependency (BD) from the project
- `EventChannel<T>` enables future upgrade: `BeatSnapshotEventChannel` as a Unity-native alternative to `BeatClockBridge` MonoBehaviour polling

### Negative

- All AI behaviors must be custom Unity Behavior `Action` nodes — TDE's stock `AIAction`/`AIDecision` components cannot be reused on enemies
- Custom action nodes must be `partial class` with `[Serializable, GeneratePropertyBag]` and a unique `[NodeDescription(id: "...")]` GUID — more boilerplate than plain C# classes
- `CombatDirectorTokenAction` interface assumes a C4 token-grant protocol TBD until C4 GDD is authored
- BT graph asset files (`*.behavior`) may produce git merge conflicts if multiple team members edit the same graph simultaneously

### Risks

- **Beat-snapshot staleness (LOW — bounded)**: `BeatClockBridge` writes on the `OnBeatSnapshot` push, not a per-frame tick, so between beats the BT reads the last beat's snapshot (≤ one beat interval stale). Acceptable for MVP (fixed BPM per level). Mitigation: AI decision nodes must not assume sub-beat freshness of `CurrentBeat`/`TimeToNextBeat`; if same-frame precision is needed later, give `BeatClock` a more-negative execution order than `-50`. Verify with frame-count log during FE1 PlayMode tests.
- **`partial class` requirement (LOW)**: All custom Action nodes must be `partial class` or `[GeneratePropertyBag]` source generator will fail. Mitigation: established as a coding standard in FE1 GDD implementation notes.
- **CombatDirector protocol undefined (MEDIUM)**: `CombatDirectorTokenAction` depends on C4's `RequestAttackToken(enemy)` / `OnTokenGranted()` interface, which does not exist yet. Mitigation: C4 GDD must define this protocol; FE1 GDD co-authoring required.
- **CharacterMovement API on non-player characters (LOW)**: `CharacterMovement.SetMovement(Vector2)` verified at TDE source `CharacterMovement.cs:200`. Enemy prefabs may require different movement ability setups. Mitigation: confirm `CharacterMovement` component on enemy prefabs during FE1 GDD implementation.

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| game-concept.md | TR-concept-009: "Enemy beat-lock: all enemy attacks forced on-beat with beat-unit windups" | `CombatDirectorTokenAction` is the architectural enforcement point — no enemy attack ability fires without a CombatDirector beat-slot token grant. Beat-lock is structural, not optional. |
| game-concept.md | Rule 4: 所有敌人攻击前摇以拍为单位，由 CombatDirector 令牌分配确保落拍 | `CombatDirectorTokenAction.TimeoutBeats` encodes the windup duration in beats. The action blocks the attack BT subtree until the correct beat arrives. |
| game-concept.md | MVP: "普通敌人 1 种（行为树驱动）" | `BehaviorGraphAgent` + Unity Behavior graph provides the BT authoring infrastructure for FE1's 1 enemy type. |
| game-concept.md | Boss 1 个（阶段化节奏设计）| Unity Behavior's `RunSubgraphDynamic` + `SwitchComposite` supports Boss phase transitions without flat state machine workarounds. |
| game-concept.md | Key Dynamic: "玩家在 Boss 战中用耳朵「听前摇」" | `CombatDirectorTokenAction.TimeoutBeats` enforces a fixed beat-unit windup, making enemy attack timing audibly predictable. |

## Performance Implications

- **CPU**: Unity Behavior BT evaluation is O(active nodes) per tick. For MVP (≤5 concurrent enemies), estimated overhead <0.5ms/frame. `BeatClockBridge.HandleBeatSnapshot()` fires at most 4–8 times/second (per enemy, per BPM); negligible cost.
- **Memory**: `BehaviorGraphAgent` instantiates a unique graph copy per enemy via `BehaviorGraph.AcquireInstance()`. Estimate ~50–80KB per enemy at runtime. Acceptable for MVP scope.
- **Load Time**: Unity Behavior graph assets (`.behavior`) loaded at scene load. Negligible for MVP (3–5 hand-crafted rooms).
- **Network**: N/A (single-player).

## Migration Plan

No existing AI code to migrate. FE1 GDD is the first enemy AI specification document. This ADR establishes the framework contracts before FE1 GDD authoring begins.

## Validation Criteria

1. `BehaviorGraphAgent` initializes on enemy prefab entry to Play mode with zero NullReferenceException (no TDE `AIBrain` component required)
2. `BeatClockBridge.HandleBeatSnapshot()` writes all three Blackboard values synchronously when `BeatClock` fires `OnBeatSnapshot`; a subsequent `BehaviorGraphAgent.Update()` tick reads the latest snapshot (verified by frame-count log: the Blackboard value on tick N equals the snapshot from the most recent `OnBeatSnapshot` at or before frame N)
3. `BehaviorGraphAgent.SetVariableValue<float>("CurrentBPM", value)` returns `true` — Blackboard variable name matches graph definition
4. `CombatDirectorTokenAction` returns `Status.Running` while waiting for token, `Status.Success` on grant, `Status.Failure` on timeout
5. Enemy attack ability (`CharacterAbility.TriggerAbility()`) never fires without a preceding `CombatDirectorTokenAction` `Status.Success` result
6. `TDESetMovementAction` moves enemy in all 4 directions using `CharacterMovement.SetMovement(Vector2)` without z-axis mapping issues
7. `RunSubgraphDynamic` switches Boss behavior sub-graph at runtime without NullReferenceException or graph restart artifacts

## Related Decisions

- **ADR-D1** (`adr-d1-tde-integration.md`): TDE lifecycle hooks and `TDELifecycleAdapter` that enemy AI must observe for death/respawn events; `BehaviorGraphAgent.End()` / `Restart()` called by `TDELifecycleAdapter` on PlayerDeath / RespawnComplete
- **ADR-D6** (`adr-d6-audio-middleware.md`): FMOD — upstream source for BeatClock's DSP timestamps that ultimately feed `BeatClockBridge`
- **M0 Spike Report** (`docs/architecture/m0-spike-unity-behavior.md`): Unity Behavior API verification — source of all confirmed API signatures in this ADR
- **C4 GDD** (`design/gdd/combat-director.md` — not yet authored): defines the `RequestAttackToken()` / `OnTokenGranted()` protocol that `CombatDirectorTokenAction` depends on; must be co-authored with FE1 GDD
- **FE1 GDD** (`design/gdd/enemy-ai.md` — unblocked by this ADR): specifies enemy behavior schemas, BT graph structure, and EnemyInfoSO integration
