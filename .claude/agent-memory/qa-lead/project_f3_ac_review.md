---
name: F3 Input Event System — AC Testability Review (Round 8 Adversarial Pass)
description: Round 8 adversarial AC review for F3 (all 20 ACs + coverage gaps); 3 BLOCKER findings confirmed (R8-01 new: updateMode AC missing; R8-05=R7-04: AC-F3-14 isSwitchingMaps access; R8-06=R7-01: AC-F3-03 mock clock unspecified); 9 RECOMMENDED + 3 ADVISORY findings
type: project
---

Reviewed 2026-07-15 (eighth adversarial pass, post-round-7 GDD approval).
Round 7 GDD status: Approved — Implementation Ready.
Prior R7 BLOCKER findings: R7-01 (AC-F3-03 mock clock), R7-02/03 (AC-F3-11 scene isolation + CI), R7-04 (AC-F3-14 access mechanism), R7-05 (AC-F3-18 "非零" false-fail).
R7-05 confirmed resolved in GDD (AC-F3-18 assertion ③ now says "≠ -1.0; 0.0 is valid").
R7-01 and R7-04 still not resolved in AC text → confirmed BLOCKER in Round 8.

## Gate Status: BLOCKED — 3 BLOCKER items must be resolved before F3 implementation sprint

## BLOCKER Findings

**R8-01 (NEW) — Missing AC for Core Rule 2 `updateMode` constraint**
GDD Core Rule 2 mandates `InputSystem.settings.updateMode == ProcessEventsInDynamicUpdate`. Zero ACs test this. A developer silently changing updateMode to ProcessEventsInFixedUpdate or ProcessEventsManually breaks all DSP timestamp semantics with no runtime warning.
Fix: Add AC-F3-21 — EditMode test, after InputEventBus.Awake(), assert `InputSystem.settings.updateMode == InputSettings.UpdateMode.ProcessEventsInDynamicUpdate`. Also assert Debug.LogError emitted if wrong updateMode detected at Awake time.

**R8-05 (= R7-04) — AC-F3-14: `canceled` callback presence not asserted; IL2CPP-unsafe "反射" option**
AC-F3-14 requires asserting `_isSwitchingMaps == true` during the canceled callback's execution. Post-callback reflection will read false (flag cleared in step ③). "或反射" is IL2CPP-unsafe (stripped). Must commit to: [InternalsVisibleTo("Tests")] + `internal bool _lastCanceledDuringSwitching` Spy field recorded inside the canceled callback body. Also: AC does not assert the canceled callback WAS invoked (only that OnAttackInput was NOT). Add Spy assertion for canceled invocation count == 1.
Fix: Remove "或反射"; specify `[InternalsVisibleTo]` + internal Spy field approach. Add canceled callback invocation count assertion.

**R8-06 (= R7-01) — AC-F3-03: Mock clock dual-value strategy unspecified**
`chargeStartDspTime < inputDspTime` (strict less-than) false-fails if mock clock returns same value at Hold.started and Hold.performed. AC must specify: mock clock returns 1.000 at Hold.started and 1.100 at Hold.performed (InputTestFixture advance of 0.15s). Assert chargeStartDspTime == 1.000 AND inputDspTime == 1.100.

## RECOMMENDED Findings (resolve during implementation sprint)

**R8-02 — AC-F3-12: Memory leak path not tested**
AC-F3-12 verifies no NRE after subscriber Destroy. Does NOT verify that a subscriber who forgets OnDestroy unsubscription causes InputEventBus delegate list to hold a GC root (preventing collection). Unity C# events do not auto-clear on Destroy. Add AC-F3-22: EditMode test using WeakReference — subscribe, unsubscribe, GC.Collect(), assert WeakReference.IsAlive == false.

**R8-03 — Missing AC for E1 (rapid attack, same-frame multiple performed)**
No AC verifies that F3 broadcasts N independent events for N same-frame Attack.performed callbacks (no deduplication). AC-F3-13 tests deviceType/deviceId correctness, not broadcast count. Add AC-F3-23.

**R8-04 — AC-F3-04 missing E2 (Dash with Move=zero)**
AC-F3-04 tests Move≠zero → Dash direction matches. E2 (Move=zero → DashInputEvent.direction=Vector2.zero) has no AC. Add AC-F3-24.

**R8-07 (= R7-02/03) — AC-F3-11: Scene isolation + CI flag not specified in AC text**
"Bare scene" requirement (only InputEventBus + one subscriber, no camera/physics) not written into AC-F3-11. CI pipeline must carry -profiler-enable flag; AC-F3-11 should reference this CI configuration requirement. Without bare scene isolation, other MonoBehaviours contaminate GC measurement.

**R8-08 — AC-F3-02: Timing test cannot distinguish sync call from cached value**
Current test design only proves "not a hardcoded constant." Does not distinguish "sync call in callback" from "cached in Update(), coincidentally fresh." Stronger design: Mock records GetDspTime() invocation count; assert exactly 1 invocation during callback execution.

**R8-09 — AC-F3-19: Log string assertion is brittle**
"Debug.LogError 含 'duplicate' 或 'already exists'" is a substring match. If implementer uses Chinese message or third wording, test fails on correct behavior. Use LogAssert.Expect(LogType.Error, new Regex(...)) or protected virtual OnDuplicateDetected() override pattern.

**R8-11 — AC-F3-06: GetCurrentMoveInput() in AllDisabled state not tested**
No AC tests return value of GetCurrentMoveInput() when Action Map is disabled. If F3 returns cached non-zero value during AllDisabled, F6's movement system may continue moving during cutscenes. AC-F3-06 Step 2: after DisableAllInput(), assert GetCurrentMoveInput() == Vector2.zero.

**R8-12 — AC-F3-18: chargeStartDspTime = 0.0 (session frame 0) boundary not explicitly covered**
AC-F3-18 assertion ③ now says "可为 0.0" but no test variant actually sets mock clock to 0.0 at Hold.started. Add variant B to AC-F3-18: mock clock starts at 0.0, verify chargeStartDspTime == 0.0 and guard table `> -1.0` correctly takes E3a path.

**R8-13 (= R7-06 partial) — Cross-flow chargeStartDspTime residue after ChargedAttack**
No AC tests that after a complete ChargedAttack flow (started→performed→canceled), a subsequent Attack1 LightAttack has chargeStartDspTime == -1.0 (no residue). AC-F3-17 or AC-F3-03 should add this cross-flow step.

## ADVISORY Findings

**R8-10 — AC-F3-05: "编译期验证" is misleading**
Compile-time verification of "field does not exist" is impossible in NUnit (compile error = test suite won't compile). Must use runtime reflection: `typeof(MoveChangedEvent).GetFields().Any(f => f.Name == "inputDspTime")` assert false.

**R8-14 — AC-F3-07/08/09: Can be downgraded to EditMode**
InputTestFixture supports EditMode. Action Map Enable/Disable is fully testable without entering Play mode. Recommend extracting InputMapStateMachine as pure non-MonoBehaviour class; test in EditMode for faster CI.

**R8-15 — AC-F3-20: Does not verify F3 won't self-restore on scene load event**
AC-F3-20 verifies Awake → AllDisabled. Does not verify that SceneManager.sceneLoaded does not trigger auto-EnableGameplayInput(). Supplement: simulate scene load event, assert F3 state remains AllDisabled without explicit EnableGameplayInput() call.

## Resolved Findings (from prior rounds)
- R7-05 — AC-F3-18 assertion ③ "非零" false-fail: RESOLVED (GDD now says "≠ -1.0; 0.0 is valid")
- AC-F3-19 singleton uniqueness — ADDED to GDD
- AC-F3-20 AllDisabled initial state — ADDED to GDD
- E3a guard table > 0 → > -1.0 — LANDED in GDD

## How to Apply

Before F3 implementation sprint: require fixes for R8-01 (new AC-F3-21), R8-05 (AC-F3-14 access mechanism + spy), R8-06 (AC-F3-03 mock clock values). All 3 are hard gates.
During implementation sprint: address R8-02 through R8-13; coordinate with lead programmer on InternalsVisibleTo assembly name, CI -profiler-enable flag, and GetCurrentMoveInput() AllDisabled semantics.
R8-10, R8-14, R8-15 are editorial/architectural; resolve at any time before test implementation begins.
