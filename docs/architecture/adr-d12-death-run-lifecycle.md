# ADR-D12: Death & Run-Lifecycle Semantics

## Status
Accepted

## Date
2026-08-11 (proposed + accepted — MVP death = new run; TDE in-place respawn dormant; resolves cross-review 2026-08-11b SG-2)

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (6000.3.19f1) |
| **Domain** | Core / Integration (lifecycle) + Audio (BeatClock run-boundary stop) |
| **Knowledge Risk** | LOW — Composes existing, verified mechanisms only: TDE `TopDownEngineEventTypes.PlayerDeath`/`.RespawnComplete` (M0 Spike verified, ADR-D1), `SceneManager.sceneLoaded` (`LoadSceneMode.Single`, stable since Unity 4), and the ADR-D10 boot path. No post-cutoff API introduced; TDE in-place respawn is *disabled* (Inspector config), not extended. |
| **References Consulted** | `docs/architecture/adr-d1-tde-integration.md`, `docs/architecture/adr-d10-bootstrap-sequencing.md`, `design/gdd/rhythm-core.md` (C1 SC-2), `design/gdd/combat-director.md` (C4 SC-3), `design/gdd/reviews/gdd-cross-review-2026-08-11b.md`, `design/gdd/game-concept.md` (:145/:164/:213) |
| **Post-Cutoff APIs Used** | None — reuses the ADR-D1 lifecycle authority and ADR-D10 boot sequence verbatim. |
| **Verification Required** | 1. Confirm TDE in-place respawn is disabled in the Inspector (Character/Health respawn config) so `RespawnComplete` does not fire in normal MVP play. 2. PlayMode test: `PlayerDeath` → assert `BeatClock.ClockState == Stopped`, `InputEventBus.CurrentState == AllDisabled`, and exactly one `OnPlayerDeath` broadcast. 3. PlayMode test: a fresh `LoadSceneMode.Single` reload after death drives a clean `Play()` with `globalBeatIndex == 0` (no double-`Play()`). |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-D1 (TDE integration / F6 `TDELifecycleAdapter` sole BeatClock lifecycle controller — Accepted), ADR-D10 (bootstrap sequencing / `OnSceneLoaded(Single)` boot path + offset boot-inject — Accepted) |
| **Enables** | FE5 (Meta-progression / run settlement) and C5 (Weapon) GDD authoring against a fixed death-boundary contract; F6 `tde-integration.md` death/respawn-node revision (RespawnComplete dormant guard) |
| **Blocks** | None new — F6 state-table revision (`RespawnComplete → Play()` in-place restart) is superseded by this ADR's dormant guard; ADR-D1 death/respawn node already amended to point here. |
| **Supersedes** | The in-place-respawn branch of ADR-D1's Death/Respawn node **for MVP** (ADR-D1 retains the path, dormant, behind the diagnostic guard). |

## Context

### Problem Statement
The cross-review **2026-08-11b (FAIL)** raised **SG-2**: F6's lifecycle state table specced `RespawnComplete → BeatClock.Play()` (TDE-native **in-place respawn**), which contradicts the game's roguelike loop as fixed in the pillar documents:

- **game-concept.md:145 / :164** — a run ends on death (and on victory/clear); there is **no in-same-run revival**; settlement is immediate at run end.
- **F5 `save-persistence.md:16/:193`** — **merit** (功勋) is the process-unique permanent currency (F5:16 infra overview), written into `PlayerProgressDTO` by FE5 `SaveSlotProgress()` **at run settlement** (F5:193).

If `RespawnComplete` silently restarts the clock in place, the player continues the *same* run after dying, which **bypasses run settlement entirely** — merit is never converted/persisted, hollowing out the only permanent progression sink. This is a silent "restart-without-settlement" footgun and a direct conflict with two pillar documents.

### Constraints
- **ADR-D1**: F6 `TDELifecycleAdapter` is the **sole controller** of `BeatClock.Play/Stop/Pause/Resume`. Any death/run-boundary clock action must route through it.
- **ADR-D10**: the boot path (`OnSceneLoaded(Single) → offset guard → Play() → EnableGameplayInput()`) is the single, deterministic way a run's clock starts; `Stop()` zeroes `global_beat_index`.
- **Forbidden pattern (technical-preferences)**: no TDE source modification — in-place respawn must be disabled via **Inspector configuration**, not code edits to TDE.
- **FE3/C5 (settlement)**: FE5 (run settlement) and C5 are **Not Started** — the death boundary must expose a stable hook (`OnPlayerDeath`) they can subscribe to when authored, without further F6 rework.

### Requirements
- Death must **end the run** and **stop the run clock** at a single, testable boundary.
- The death boundary must **broadcast a domain event** (`OnPlayerDeath`) that settlement (FE5/C5) subscribes to.
- Starting the next run must **reuse the existing boot path** — no new lifecycle infrastructure.
- TDE in-place respawn must be **provably inert** in MVP (disabled + a diagnostic guard if it ever fires).
- Downstream token/pending-input recovery (C4 SC-3, C1 SC-2) must key off the same death boundary.

## Decision

**MVP death = new run (run-ends-on-death).** There is no in-same-run revival.

### D12-1 — Death boundary (run-ending)
On `PlayerDeath` (lives = 1, no in-run revival), F6 `TDELifecycleAdapter`, in the **same frame**:
1. `BeatClock.Stop()` — the **run-ending clock-stop point** (also zeroes `global_beat_index` per ADR-D10).
2. `InputEventBus.DisableAllInput()`.
3. Broadcast **`OnPlayerDeath`** — the CombatCore-visible domain event (payload carries no TDE types; consumers never subscribe to TDE events directly, per ADR-D1 boundary isolation).

FE5 / C5 (Not Started) subscribe to `OnPlayerDeath` to perform **merit conversion + persistence + settlement UI**.

### D12-2 — "Next run starts immediately" = fresh scene load (reuse boot path)
The next run is a **new `LoadSceneMode.Single` load** that runs the existing ADR-D1/D10 boot path verbatim:

```
OnSceneLoaded(Single) → offset guard (already-injected → skip) → Play() → EnableGameplayInput()
```

Because the prior `Stop()` already zeroed `global_beat_index` and cleared Pending state, the new `Play()` is a clean run start — no residual clock state, no double-`Play()`. The offset boot-inject `_offsetInjected` one-shot guard (ADR-D10 D-5) makes the re-entrant `OnSceneLoaded(Single)` skip re-injection and preserve any runtime FE3 calibration.

### D12-3 — TDE in-place respawn is DISABLED in MVP (dormant diagnostic guard)
TDE's native `RespawnComplete → BeatClock.Play()` in-place restart is **not enabled** in MVP. TDE in-place respawn is turned off via **Inspector configuration** (Character/Health respawn settings — no TDE source edit). If `RespawnComplete` nonetheless fires (misconfiguration), F6 does **not** restart the clock in place; it logs a fatal diagnostic and returns:

```
[F6-FATAL] RespawnComplete fired but in-place respawn is disabled in MVP
```

(See the ADR-D1 revision: the `RespawnComplete` case is now a dormant diagnostic guard, not a `Play()`.)

### D12-4 — Downstream boundary contracts
- **C4 SC-3** (`combat-director.md`): death token reclamation — on `OnPlayerDeath`, C4 clears all outstanding attack-slot tokens (symmetric to C1's SC-2). Landed in the C4 GDD.
- **C1 SC-2** (`rhythm-core.md`): death-domain clear — on `OnPlayerDeath`, C1 clears `_pendingInput` / `_lateWindowActive` (E10 ghost-judgment guard). Landed in the C1 GDD.

Both consumers key off the single `OnPlayerDeath` boundary defined in D12-1.

## Alternatives Considered

### Model A — Death = new run (CHOSEN)
Run ends on death; settlement fires; next run reuses the boot path. Aligns with game-concept.md:145/164 and F5:193; zero new infrastructure.

### Model B — Retain TDE in-place respawn (`RespawnComplete → Play()`) — REJECTED
- **Description**: keep TDE's checkpoint-style in-place revival; clock restarts in place after death.
- **Pros**: uses TDE's ready-made respawn convenience; no scene reload.
- **Cons**: **hollows out merit persistence** — the player continues the same run without hitting run settlement, so merit (the process-unique permanent currency) is never converted/persisted; directly conflicts with game-concept.md:145/164 (no in-run revival, immediate settlement) and F5:193. It is the exact SG-2 defect the cross-review flagged.
- **Rejection Reason**: architecturally incompatible with the roguelike loop's two pillar documents; the convenience saved is not needed for MVP.

## Consequences

### Positive
- **Settlement hook is deterministic**: `PlayerDeath` → `OnPlayerDeath` gives FE5/C5 a single, testable trigger for merit conversion + persistence + settlement UI.
- **Zero new infrastructure**: the next run reuses the ADR-D1/D10 boot path; `Stop()` already provides a clean clock reset. No new lifecycle code paths.
- **Footgun eliminated**: the "silent in-run restart without settlement" path is closed at the architecture level (dormant diagnostic guard, not a live `Play()`).
- **Boundary symmetry**: C1 SC-2 and C4 SC-3 both key off the same `OnPlayerDeath` boundary, so death-domain cleanup is uniform.

### Negative / Trade-offs
- **Gives up TDE's ready-made checkpoint respawn** — acceptable, as MVP does not need in-run revival.
- **Post-MVP**: if in-run checkpoint respawn is later desired, **supersede this ADR** (re-enable the ADR-D1 dormant path) rather than re-editing the three pillar documents' bodies — the death-boundary contract is localized here.

### Risks
- **Misconfiguration risk**: if TDE in-place respawn is left enabled in the Inspector, `RespawnComplete` fires and hits the fatal guard. Mitigated by the Verification-Required Inspector check + the loud `[F6-FATAL]` diagnostic (fail-loud, never silently restarts).

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| tde-integration.md (F6) | State table `RespawnComplete → Play()` in-place restart (SG-2 defect) | Supersedes with run-ends-on-death: `PlayerDeath` stops the clock + broadcasts `OnPlayerDeath`; `RespawnComplete` becomes a dormant diagnostic guard (ADR-D1 revised) |
| rhythm-core.md (C1) | SC-2 death-domain clear (`_pendingInput` / E10 ghost judgment) | Defines the `OnPlayerDeath` boundary C1 subscribes to; C1 GDD already lands the clear logic |
| combat-director.md (C4) | SC-3 death token reclamation (orphan tokens) | Defines the `OnPlayerDeath` boundary C4 subscribes to (symmetric to C1 SC-2); C4 GDD already lands the reclamation |
| game-concept.md | :145/:164 no in-run revival + immediate settlement; :213 anti-pillar (no rhythm-bypassing gain) | Enforces run-ends-on-death so merit settlement is never bypassed |
| save-persistence.md (F5) | :16/:193 merit = process-unique permanent currency (:16), written to `PlayerProgressDTO` by FE5 `SaveSlotProgress()` at run settlement (:193) | Guarantees the settlement boundary (`OnRunEnded(Death)` → FE5) always fires; no in-run restart bypass |

## Migration Plan
Greenfield — no code to migrate. Deliverables this ADR governs:
1. **ADR-D1** `adr-d1-tde-integration.md`: Death/Respawn node revised — `PlayerDeath` broadcasts `OnPlayerDeath`; `RespawnComplete` case is a dormant `[F6-FATAL]` diagnostic guard (done in this batch).
2. **F6** `tde-integration.md`: revise the lifecycle state table so `RespawnComplete` maps to the dormant guard, not `Play()`; document the Inspector setting that disables TDE in-place respawn. (F6 is Needs Revision — apply in its revision pass.)
3. **FE5/C5** (Not Started): when authored, subscribe to `OnPlayerDeath` for merit conversion + persistence + settlement UI.
4. Registry: add the forbidden pattern `f6_respawncomplete_inplace_restart_mvp` (in-place `RespawnComplete → Play()` disabled for MVP) alongside the death-boundary contract.

## Validation Criteria
1. **Death boundary**: PlayMode test — broadcast `PlayerDeath` → assert `BeatClock.ClockState == Stopped`, `InputEventBus.CurrentState == AllDisabled`, exactly one `OnPlayerDeath` broadcast.
2. **Dormant guard**: broadcast `RespawnComplete` → assert **no** `BeatClock.Play()` call and a logged `[F6-FATAL]` (must fire in a release-config path, not Debug-only).
3. **Clean next-run start**: after a death `Stop()`, a fresh `LoadSceneMode.Single` reload → assert `globalBeatIndex == 0` at the first `OnBeatExact`, single `Play()` (no double-`Play()`), offset re-inject skipped by `_offsetInjected` guard.
4. **Downstream symmetry**: on `OnPlayerDeath`, C1 `_pendingInput == null` / `_lateWindowActive == false` (SC-2) and C4 outstanding tokens == 0 (SC-3).

## Open Follow-up
- **Victory/clear also ends a run** (game-concept.md:164). This ADR fixes the **death** boundary only. Recommend FE5 define a unified `RunEnded(outcome: Death | Victory)` abstraction for settlement to subscribe to, with the **victory path's clock `Stop()` ownership** assigned in the F6/FE5 revision (so a victory → new-run transition does not leave the clock `Playing` and cause a double-`Play()` in the next scene). This is economy/loop design and belongs to **game-designer / FE5**, not this ADR.

## Related Decisions
- ADR-D1 (TDE Integration) — F6 `TDELifecycleAdapter` sole BeatClock lifecycle controller; Death/Respawn node revised to point here (RespawnComplete dormant guard)
- ADR-D10 (Bootstrap Sequencing) — the `OnSceneLoaded(Single)` boot path this ADR reuses as the "next run" clock start; `Stop()` zeroes `global_beat_index` (no F6 boot change needed)
- `design/gdd/rhythm-core.md` (C1 SC-2 / E10), `design/gdd/combat-director.md` (C4 SC-3), `design/gdd/reviews/gdd-cross-review-2026-08-11b.md` (SG-2), `design/gdd/game-concept.md` (:145/:164/:213), `design/gdd/save-persistence.md` (:16/:193)
