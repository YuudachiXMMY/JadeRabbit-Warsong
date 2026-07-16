---
name: F5 Save/Persistence — AC Testability Review (Four-Pass)
description: Four-pass adversarial review of F5 AC section; Round 4 adds 7 new BLOCKING findings; 2 new ACs required (AC-F5-24, AC-F5-25); GDD rewrite required before F5 stories begin
type: project
---

Reviewed 2026-07-14 (four passes total). Round 4 targeted 9 specific coverage gaps and test executability problems. Full findings below.

## Gate Status: BLOCKED — 7 new BLOCKING items from Round 4; prior CRITICAL items from Rounds 1–3 also still require resolution

F5 implementation stories must not enter Sprint Backlog until all BLOCKING items are resolved.

---

## Round 4 BLOCKING Findings (New)

**R4-B1 — Rule 11 has zero AC coverage**
GDD Rule 11: InjectStorage() after Initialize() must throw InvalidOperationException. No AC covers this path.
New AC required: AC-F5-24. Precondition: Initialize() already called. Action: InjectStorage(new MockFileStorage()). Assertion: InvalidOperationException thrown, state remains Idle.

**R4-B2 — AC-F5-06: Time window direction error in upper bound**
Upper bound `<=调用完成后 UtcNow + 5000` allows timestamps 5 seconds in the future — physically impossible for a synchronous write. This is a logic error in the assertion, not just loose tolerance.
Revised bounds: lower bound -2000ms (for slow CI), upper bound +500ms (covers sync write latency, no future timestamps).

**R4-B3 — AC-F5-12: "Wait 1 frame flush" undefined for PlayMode**
`yield return null` does not guarantee SaveService.Update() has executed before assertion (violates Rule 14 no-SEO constraint). No test engineer can write a deterministic test from current AC text.
Required additions: (1) Use `yield return new WaitUntil(() => errorLogReceived, timeout: 1.0f)` with `Application.logMessageReceived` callback. (2) SaveService must be mounted in scene (not pure C# object). (3) WaitUntil timeout = 1.0f seconds, timeout = test failure.

**R4-B4 — AC-F5-08: V0 "field missing" cannot be simulated via MockFileStorage**
MockFileStorage uses in-memory Dictionary; `KeyExists()` can distinguish "key absent" vs "key = 0" but injecting save_version=0 creates a key with value 0, not an absent key. These are different JSON states in production ES3Storage.
AC must be scoped to "field present, value = 0" only. "Field absent" scenario requires real ES3 file integration test (PlayMode), cannot be covered by MockFileStorage EditMode test.

**R4-B5 — AC-F5-09: "file not modified" assertion is inoperable + state assertion still missing**
"文件未被修改或覆盖" cannot be translated to assertion code. GDD must specify: SaveCallCount == 0 (recommended, via MockFileStorage). Additionally, AC-F5-09 still has no SaveService state assertion despite state table row (Loading | VersionTooHigh | Error) existing on GDD line 106. AC and state table are inconsistent — AC must assert SaveService is in Error state.

**R4-B6 — WriteFailed retry path has no AC coverage**
GDD Edge Cases: "如磁盘空间已恢复，下次存档会重新尝试" and OnSaveError.IsRetryable=true for WriteFailed. AC-F5-14 stops at AcknowledgeError() → Idle. No AC verifies the subsequent Save() call succeeds.
New AC required: AC-F5-25. After AcknowledgeError(), simulate storage recovery (clear IOException injection), call SaveSlotProgress(0, dto), assert: write succeeds, state returns to Idle, LoadSlotProgress(0) returns new value.

**R4-B7 — AC-F5-05: PlayMode teardown steps undefined; EditMode execution is unreliable**
Object.Destroy() has frame-end delay — not reliable in EditMode (no frame concept). Static _instance is not cleared automatically. DontDestroyOnLoad scene lifecycle differs in EditMode.
AC-F5-05 must be marked as PlayMode [UnityTest] with 6-step implementation spec: (1) write merit=777, (2) yield null, (3) Object.Destroy(instance), (4) yield null, (5) clear _instance via reflection or public ResetForTesting() method (must be formally defined in GDD or TestHelper spec), (6) instantiate new SaveService, InjectStorage, Initialize.

---

## Round 4 ADVISORY Findings

**R4-A1 — AC-F5-22/23 document order inverted**
AC-F5-23 (GlobalData corruption) appears before AC-F5-22 (AcknowledgeError idempotency) in the GDD. AC-F5-22 describes Idle-state no-op behavior; AC-F5-23 demonstrates the full Error→Idle transition. Reading in physical order, a test engineer encountering AC-F5-22 first may mistake it for "verify reset behavior" rather than "verify no-op behavior". Recommend reordering: AC-F5-22 → AC-F5-23 → (new AC-F5-24, AC-F5-25), and add explicit clarification to AC-F5-22 that AcknowledgeError() in non-Error state is a no-op (does NOT reset memory cache).

**R4-A2 — DeleteSlot-during-Saving has no GDD Edge Case entry**
GDD explains Write-during-Saving behavior (queue, no exception, no discard). DeleteSlot-during-Saving (destructive operation) is not covered. Since GDD states the Write-during-Saving rule "prevents regression if async is introduced later," DeleteSlot-during-Saving needs a parallel one-line statement in Edge Cases. Not an AC gap — a GDD prose gap. Add: "DeleteSlot in Saving state: [sync API: unreachable / async implementation: Delete must be queued and serialized with Write operations]."

---

## Prior Rounds — Still Valid BLOCKING Items

All Round 1–3 CRITICAL and WARNING items that were not fully resolved remain valid. Key unresolved items as of Round 4:

- **C1 (R1/R2)**: AC-F5-05 DontDestroyOnLoad teardown undefined → superseded and expanded by R4-B7 above
- **C2 (R1/R2)**: AC-F5-07 MockFileStorage "parse failure" interface undefined; must split into AC-F5-07a (EditMode) and AC-F5-07b (PlayMode) — still unresolved in current GDD
- **C3 (R1/R2)**: AC-F5-09 state assertion missing → partially addressed in state table but not in AC → superseded by R4-B5
- **C4 (R1/R2)**: AC-F5-11 must split into 11a (Stopwatch, EditMode, BLOCKING) and 11b (Profiler, manual, ADVISORY) — current GDD still lists as single manual/advisory AC
- **W1 (R2)**: AC-F5-01 totalPlayTimeSeconds=0.0 literal should be 0.0d (double) — check all AC float/double literals
- **W2 (R2)**: AC-F5-02 "位精度" for double totalPlayTimeSeconds field is unverified assumption; AC omits totalPlayTimeSeconds from roundtrip assertion
- **W4 (R2)**: AC-F5-08 V0 detection strategy unspecified → now superseded by R4-B4 (scope limitation documented)
- **W6 (R2)**: AC-F5-16 DeleteSlot only checks merit=0; must verify unlockedWeaponIds=[] and metaUpgradeLevels=[] also reset
- **I4 (R2)**: AC-F5-22 only covers Idle-state idempotency; Loading-state behavior undefined
- **B7-B10 (R1)**: GameSettingsDTO and LatencyCalibrationDTO cross-session persistence ACs still missing (AC-F5-05 only covers PlayerProgressDTO slot data)

---

## Summary Counts

| Round | New BLOCKINGs | New Advisories | New ACs Required |
|-------|--------------|----------------|-----------------|
| Round 1 | 5 CRITICAL | 4 WARNING + 4 INFO | 4 missing ACs |
| Round 2 | 6 WARNING (deepened) | 4 INFO | 1 new AC (AC-F5-23) |
| Round 3 (cross-review) | 0 new | 0 new | 0 new |
| Round 4 | 7 BLOCKING | 2 ADVISORY | 2 new ACs (AC-F5-24, AC-F5-25) |

**Why:** Each round revealed that prior "fixes" addressed the surface symptom without resolving the underlying test executability gap. Root cause pattern: ACs are written from the designer's perspective (what the system does) not the test engineer's (how to verify it). Recommend co-authoring future ACs with a pseudocode implementation sketch.

**How to apply:** F5 implementation stories must not enter Sprint Backlog until all Round 4 BLOCKING items (R4-B1 through R4-B7) are resolved, plus the still-valid prior-round items above. When GDD author delivers AC revisions, QA must verify each BLOCKING item with a "test implementation sketch" (pseudocode) before accepting the revision.
