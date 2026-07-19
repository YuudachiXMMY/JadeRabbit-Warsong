---
name: F3 Input Event System — AC Testability Review (Round 9 Adversarial Pass)
description: Round 9 adversarial AC review for F3; 3 R8 BLOCKERs still unresolved in document text + 3 new BLOCKERs found (DisableAllInput+ChargedAttack gap, UIActive→GameplayActive T3 untested, AC-F3-03 missing chargeStartDspTime > -1.0 assertion); gate remains BLOCKED with 6 total blockers
type: project
---

Reviewed 2026-07-18 (ninth adversarial pass, post-round-8 GDD approval).
Round 8 GDD status: Approved — Implementation Ready.
Prior R8 BLOCKER findings: R8-01 (AC-F3-21 updateMode), R8-05 (AC-F3-14 isSwitchingMaps access + spy), R8-06 (AC-F3-03 mock clock dual-value).
**R8-01, R8-05, R8-06 are all still unresolved in the current GDD text.**

## Gate Status: BLOCKED — 6 BLOCKER items must be resolved before F3 implementation sprint

---

## BLOCKER Findings (Carried from Round 8)

**R8-01 (CARRIED) — Missing AC for Core Rule 2 `updateMode` constraint**
GDD Core Rule 2 mandates `InputSystem.settings.updateMode == ProcessEventsInDynamicUpdate`. Zero ACs test this. No AC-F3-21 exists in the current document.
Fix: Add AC-F3-21 — EditMode test, after InputEventBus.Awake(), assert `InputSystem.settings.updateMode == InputSettings.UpdateMode.ProcessEventsInDynamicUpdate`. Also assert Debug.LogError emitted if wrong updateMode detected at Awake time.

**R8-05 (CARRIED = R7-04) — AC-F3-14: `canceled` callback presence not asserted; IL2CPP-unsafe "反射" option**
AC-F3-14 still reads "通过测试可观测内部状态（Test Double 或反射）". "或反射" is IL2CPP-unsafe (stripped). [InternalsVisibleTo] + internal spy field approach not specified. Canceled callback invocation count assertion absent.
Fix: Remove "或反射"; specify `[InternalsVisibleTo]` + `internal bool _lastCanceledDuringSwitching` Spy field approach. Add canceled callback invocation count == 1 assertion.

**R8-06 (CARRIED = R7-01) — AC-F3-03: Mock clock dual-value strategy unspecified**
`chargeStartDspTime ≤ inputDspTime` false-fails if mock clock returns same value at Hold.started and Hold.performed. AC must specify: mock clock returns 1.000 at Hold.started and 1.100 at Hold.performed. Assert chargeStartDspTime == 1.000 AND inputDspTime == 1.100.

---

## BLOCKER Findings (New in Round 9)

**R9-01 (NEW) — No AC for `DisableAllInput()` + ChargedAttack → canceled must silent-drop**
Core Rule 5 explicitly documents that `DisableAllInput()` must use the identical `_isSwitchingMaps` three-step pattern as `EnableUIInput()`. The GDD even names the failure mode: "过场动画/死亡动画/场景加载调用 `DisableAllInput()` 时，若玩家正在充能（`_holdPerformed == false`），未设置保护将导致 `canceled` 回调走 E3a 路径，向 C1 广播幽灵 LightAttack." AC-F3-14 tests EnableUIInput()+ChargedAttack. AC-F3-08 tests input events after DisableAllInput() completes — it never holds a ChargedAttack active before calling DisableAllInput(), so the canceled-callback suppression path is never exercised.
Fix: Add AC-F3-21 (or -23 if -21 is taken by R8-01) — same structure as AC-F3-14 but with DisableAllInput() as the trigger. Assertions: ① OnAttackInput not triggered; ② _isSwitchingMaps == true during canceled callback via spy; ③ canceled callback invoked exactly once; ④ _isSwitchingMaps == false after DisableAllInput() returns.

**R9-02 (NEW) — T3 transition (UIActive → GameplayActive) has no AC; 2 of 4 state machine transitions untested**
States table defines 4 transitions. AC-F3-09 covers T1 (AllDisabled→GameplayActive). AC-F3-07 covers T2 (GameplayActive→UIActive). T3 (UIActive→GameplayActive) has no AC. T4 (GameplayActive/UIActive→AllDisabled) via DisableAllInput() is partially covered by AC-F3-08 but with no UIActive precondition specified. A defect where EnableGameplayInput() silently fails from UIActive (but works from AllDisabled) would pass all current ACs.
Fix: Add AC-F3-22 — PlayMode test, precondition UIActive, call EnableGameplayInput(), assert: Gameplay Map active, UI Map disabled, attack input fires OnAttackInput, UI Navigate input does NOT fire OnUINavigate.

**R9-04 (NEW) — AC-F3-03 does not assert `chargeStartDspTime > -1.0` on ChargedAttack path**
AC-F3-03 asserts `chargeStartDspTime ≤ inputDspTime` for ChargedAttack. A defective implementation that fails to cache Hold.started and leaves chargeStartDspTime at -1.0 would satisfy `chargeStartDspTime ≤ inputDspTime` (since -1.0 < any valid DSP time), while silently routing C1's guard table to the wrong path. The GDD explicitly documents this as a "静默错误" that causes "所有判定系统性崩溃." The -1.0 sentinel check must be an explicit AC assertion.
Fix: Add assertion ④ to AC-F3-03 ChargedAttack path: `chargeStartDspTime > -1.0`（非哨兵值，证明 Hold.started 的 DSP 时间戳已正确缓存）.

---

## RECOMMENDED Findings (Round 9 new)

**R9-05 (NEW) — AC-F3-11: ProfilerRecorder self-validation same-frame flush hazard**
Self-validation does `_ = new object()` then immediately asserts `recorder.CurrentValue > 0`. In Unity, ProfilerRecorder.CurrentValue may be 0 in the same frame as the allocation (flush happens at frame boundary). AC must use `yield return null` then `recorder.LastValue` for both the self-validation step and the main zero-alloc assertion.

**R9-06 (NEW) — AC-F3-19: Same-frame Destroy race not exercised**
AC-F3-19 sends input after the duplicate instance is destroyed (post-frame). The adversarial scenario is input arriving in the same frame as both Awake() calls, before Destroy() executes. Duplicate's Action Maps may still be subscribed during Destroy deferral. AC must specify same-frame input timing and assert duplicate's Action Maps are disabled before Destroy() resolves.

---

## RECOMMENDED Findings (Carried from Round 8, still open)

**R8-02 — AC-F3-12: Memory leak path not tested**
AC-F3-12 verifies no NRE after subscriber Destroy. Does NOT verify that a subscriber who forgets OnDestroy unsubscription causes InputEventBus delegate list to hold a GC root. Add AC-F3-22 (or -23): EditMode test using WeakReference — subscribe, unsubscribe, GC.Collect(), assert WeakReference.IsAlive == false.

**R8-03 — Missing AC for E1 (rapid attack, same-frame multiple performed)**
No AC verifies that F3 broadcasts N independent events for N same-frame Attack.performed callbacks (no deduplication). Add AC.

**R8-04 — AC-F3-04 missing E2 (Dash with Move=zero)**
E2 (Move=zero → DashInputEvent.direction=Vector2.zero) has no AC.

**R8-07 — AC-F3-11: Scene isolation + CI flag not specified in AC text**
"Bare scene" requirement not written into AC-F3-11. CI pipeline must carry -profiler-enable flag.

**R8-08 — AC-F3-02: Timing test cannot distinguish sync call from cached value**
Stronger design: Mock records GetDspTime() invocation count; assert exactly 1 invocation during callback execution.

**R8-09 — AC-F3-19: Log string assertion is brittle**
"含 'duplicate' 或 'already exists'" is a substring match. Use LogAssert.Expect with Regex or protected virtual OnDuplicateDetected() pattern.

**R8-11 — AC-F3-06: GetCurrentMoveInput() in AllDisabled state not tested**
After DisableAllInput(), assert GetCurrentMoveInput() == Vector2.zero.

**R8-12 — AC-F3-18: chargeStartDspTime = 0.0 boundary not explicitly covered**
Add variant B to AC-F3-18: mock clock starts at 0.0, verify guard table `> -1.0` correctly takes E3a path.

**R8-13 — Cross-flow chargeStartDspTime residue after ChargedAttack**
No AC tests that after a complete ChargedAttack flow, a subsequent Attack1 LightAttack has chargeStartDspTime == -1.0 (no residue).

---

## ADVISORY Findings (Carried from Round 8, still open)

**R8-10 — AC-F3-05: "编译期验证" is misleading**
Must use runtime reflection: `typeof(MoveChangedEvent).GetFields().Any(f => f.Name == "inputDspTime")` assert false.

**R8-14 — AC-F3-07/08/09: Can be downgraded to EditMode**
InputTestFixture supports EditMode for Action Map state tests.

**R8-15 — AC-F3-20: Does not verify F3 won't self-restore on scene load event**
Supplement: simulate scene load event, assert F3 state remains AllDisabled without explicit EnableGameplayInput() call.

---

## Advisory Finding (New in Round 9)

**R9-03 (NEW) — Open Question #2 already answered in Tuning Knobs; stale open question**
OQ-2 asks whether F3 handles input buffering, "待 C1 GDD 确认." Tuning Knobs section definitively answers: F3 does not buffer, this is C1/C2 responsibility, with explicit rationale. Close OQ-2 with resolution note to prevent downstream readers from treating it as a genuine blocker.

---

## Resolved Findings (from prior rounds)
- R7-05 — AC-F3-18 assertion ③ "非零" false-fail: RESOLVED
- AC-F3-19 singleton uniqueness — ADDED to GDD
- AC-F3-20 AllDisabled initial state — ADDED to GDD
- E3a guard table > 0 → > -1.0 — LANDED in GDD
- BLOCK-R8-01 (8th review design layer): DisableAllInput() three-step _isSwitchingMaps protection — ADDED to Core Rule 5 (design). **AC not yet added — this is R9-01.**
- BLOCK-R8-02 (8th review design layer): Touchscreen Tap+Hold dual canceled architecture — ADDED to E3a.
- BLOCK-R8-03 (8th review design layer): latency_offset_s value domain — ADDED to F3-F2.
- BLOCK-R8-04 (8th review design layer): AC-F3-03 assertion `<` → `≤` — LANDED in GDD.

## How to Apply

Before F3 implementation sprint: require fixes for all 6 BLOCKERs:
- R8-01: New AC-F3-21 for updateMode
- R8-05: AC-F3-14 access mechanism + spy field + count assertion
- R8-06: AC-F3-03 mock clock explicit dual values
- R9-01: New AC for DisableAllInput()+ChargedAttack silent-drop (symmetric to AC-F3-14)
- R9-02: New AC-F3-22 for UIActive→GameplayActive T3 transition
- R9-04: Add chargeStartDspTime > -1.0 assertion to AC-F3-03 ChargedAttack path

During implementation sprint: address RECOMMENDED findings. R8-02 through R8-13, R9-05, R9-06.
ADVISORY: R8-10, R8-14, R8-15, R9-03 at any time before test implementation begins.

AC numbering note: R8-01 claims -21; R9-01 needs the next available number. If -21 is DisableAllInput+ChargedAttack (per R9-01) and updateMode (per R8-01) takes -22 instead, that is acceptable — coordinate with GDD author on final numbering before writing.
