---
name: F6 TDE Integration Layer — AC Generation
description: 18 ACs generated for F6 (JadeRabbitInputManager + TDELifecycleAdapter); coverage across 2 components, 5 state transitions, and all 7 edge cases; no BLOCKER findings raised at generation time
type: project
---

Generated 2026-07-18. F6 GDD status: In Design (AC section was [To be designed]).

## Gate Status: AC GENERATED — Ready for GDD insertion and adversarial review

## Coverage Map

| Area | ACs | Test Type |
|------|-----|-----------|
| JadeRabbitInputManager — no direct Action Map calls | AC-F6-01 | PlayMode automated |
| JadeRabbitInputManager — TDE AbilityInputAuthorization preserved | AC-F6-02 | PlayMode manual |
| TDELifecycleAdapter — BeatClock sole caller | AC-F6-03 | PlayMode automated |
| FMOD uninit blocks BeatClock (EC-01) | AC-F6-04 | PlayMode automated |
| FMOD validated in Start() not Awake() | AC-F6-05 | PlayMode automated |
| State: Uninitialized → Gameplay Active (Single load) | AC-F6-06 | PlayMode automated |
| Additive load does not restart BeatClock (EC-02) | AC-F6-07 | PlayMode automated |
| State: Gameplay Active → Paused (atomicity) | AC-F6-08 | PlayMode automated |
| State: Paused → Gameplay Active (atomicity) | AC-F6-09 | PlayMode automated |
| State: Gameplay Active → PlayerDeath (atomicity) | AC-F6-10 | PlayMode automated |
| State: PlayerDeath → RespawnComplete (BeatClock before Input) | AC-F6-11 | PlayMode automated |
| Animancer Layer 1 weight 1.0/0.0 boundary | AC-F6-12 | PlayMode automated |
| Attack speed formula + clamp Warning log | AC-F6-13 | EditMode automated |
| Root Motion disabled on all attack states | AC-F6-14 | PlayMode automated |
| BPM mid-attack update (EC-03, Option A) | AC-F6-15 | PlayMode automated |
| Death vs attack race condition — Layer 1 cleared (EC-04) | AC-F6-16 | PlayMode manual + automated |
| No callback accumulation after 5 scene switches (EC-07) | AC-F6-17 | PlayMode automated |
| Nominal BPM=120 → Speed=1.0 calibration | AC-F6-18 | PlayMode automated |

## Total: 18 ACs (at limit — do not add without removing one)

## Key Design Decisions in ACs

- AC-F6-01 uses IL reflection scan for `InputActionMap.Enable/Disable` callvirt — same IL2CPP-safe pattern debated in F3 review; this one is EditMode compile-time scan, not runtime reflection, so IL2CPP stripping is not a concern.
- AC-F6-03 uses IL scan + Spy runtime log — covers both static guarantee and runtime enforcement.
- AC-F6-04 requires FMOD Test Double injection via [InternalsVisibleTo] — must confirm assembly name with lead programmer before test implementation.
- AC-F6-08/09/10/11 all assert same-frame atomicity via `Time.frameCount` comparison — QA tester must confirm frameCount is available in PlayMode test context.
- AC-F6-16 is split: automated weight assertion + manual visual confirmation — follows ADVISORY gate for Visual/Feel per story type table.
- C2 Layer 1 responsibility is explicit in AC-F6-16 note — failure attribution documented.

## Potential Issues to Flag (not blockers at AC generation time)

- AC-F6-02: "AbilityInputAuthorization" API name should be confirmed against TDE version in use before test implementation — TDE API names are not in LLM training data for this version.
- AC-F6-12: ATTACK_LAYER_BLEND_OUT_MIN/MAX constants need to be defined in F6Constants before this AC can be executed — flag to lead programmer.
- AC-F6-15: C2 must implement `OnBeat` speed recalculation for this AC to be testable — F6 AC cannot be verified until C2 sprint is underway; mark as dependency-blocked until C2 exists.

## How to Apply

Insert all 18 ACs into `design/gdd/tde-integration.md` Acceptance Criteria section.
Run adversarial review pass before F6 implementation sprint begins.
Flag AC-F6-02 and AC-F6-15 as dependency-blocked (TDE API confirmation + C2 availability).
