---
name: F6 TDE Integration Layer — AC Adversarial Review
description: Re-review of 22 ACs (AC-F6-01 to AC-F6-22) after GDD revision; 6 Critical new findings; 10 BLOCKING rewrites required; 2 missing ACs still unresolved
type: project
---

Generated 2026-07-18. First adversarial review 2026-07-18 (18 ACs). Re-review 2026-07-18 (22 ACs, post-GDD revision).

## Gate Status: RE-REVIEW COMPLETE — 10 BLOCKING rewrites + 2 missing ACs required before F6 sprint

## What Changed Since Round 1 (18 → 22 ACs)

- AC-F6-01 to AC-F6-18: Round 1 AC set (modified by GDD revision)
- AC-F6-19: New — FMOD async mode verification (2-frame DSP increment)
- AC-F6-20: New — Option A feel test (3-person blind test)
- AC-F6-21: New — IFMODRuntimeChecker interface injection (resolves Round 1 AC-F6-04 FAIL)
- AC-F6-22: New — Character Prefab EditMode integrity check (resolves 1 of 3 missing ACs from Round 1)

## Round 1 Findings Resolution Status

| Round 1 Finding | Status in Round 2 |
|-----------------|-------------------|
| AC-F6-01 EditMode wrong | STILL OPEN — third round still shows PlayMode |
| AC-F6-03 scope too broad + IL/PlayMode contradiction | STILL OPEN |
| AC-F6-04 FMOD sealed class FAIL | PARTIALLY RESOLVED — AC-F6-21 adds correct mechanism, but AC-F6-04 body still describes old unimplementable approach |
| AC-F6-05 Spy mechanism unspecified | STILL OPEN |
| AC-F6-08/09/10 frameCount atomicity | STILL OPEN |
| AC-F6-12 C2 dependency | MISDIAGNOSED — should be "wrong test path design," not "dependency blocked"; can be fixed without C2 |
| AC-F6-15 "40-60% progress" ambiguous | NOT FULLY FIXED — now has "下一帧" assertion that contradicts direct-assignment implementation |
| Missing AC: Prefab config | RESOLVED by AC-F6-22 |
| Missing AC: out-of-sequence event defense | STILL MISSING |
| Missing AC: LevelConfigSO invalid data | STILL MISSING |

## Round 2 Verdict by AC

| AC | Severity | Primary Issue |
|----|----------|---------------|
| AC-F6-01 | Critical | Test type still wrong (PlayMode→EditMode needed); IL2CPP callvirt safety unaddressed |
| AC-F6-02 | Warning | Pass condition unquantified (2 rounds); test Ability not named |
| AC-F6-03 | Critical | IL/PlayMode contradiction (2 rounds); reflection call blind spot; Spy mechanism unspecified |
| AC-F6-04 | Warning | Body still describes unimplementable direct FMOD mock; contradicts AC-F6-21 |
| AC-F6-05 | Warning | Spy mechanism unspecified (2 rounds); lifecycle phase detection unspecified |
| AC-F6-06 | Info | PASS — cross-GDD interface state property needs verification before test impl |
| AC-F6-07 | Info | PASS — suggest "wait 1 frame after additive load before asserting" |
| AC-F6-08 | Critical | frameCount ≠ atomicity (2 rounds); missing listener isolation; missing fault-injection case |
| AC-F6-09 | Critical | Same as AC-F6-08; precondition state setup decoupled from event path |
| AC-F6-10 | Critical | Same as AC-F6-08; "does not respond" assertion method unspecified |
| AC-F6-11 | Warning | Order sequence number acquisition mechanism unspecified; Spy framework absent |
| AC-F6-12 | Critical | C2 dependency is wrong test design — direct PlayAttack() call removes dependency entirely |
| AC-F6-13 | Info | authoredBpm=0 divide-by-zero case missing; log format precision assertion ambiguous |
| AC-F6-14 | Warning | Dual test type overlaps AC-F6-22 item 3; 0.001m position tolerance may cause flaky test |
| AC-F6-15 | Critical | C2 dependency wrong design (same as AC-F6-12); "下一帧" contradicts direct-assignment |
| AC-F6-16 | Warning | EC-04 says "same frame" but AC says "within 1 frame" — inconsistent; Visual gate misclassified |
| AC-F6-17 | Info | PASS — test scene files must exist in BuildSettings; yield strategy should be event-based |
| AC-F6-18 | Critical | Clip duration measurement protocol absent (16ms tolerance meaningless without it); Speed assertion duplicates AC-F6-13; C2 dependency unlabeled |
| AC-F6-19 | Critical | IDSPClock interface source undefined; sync-mode injection mechanism unspecified; headless CI frame stability unverified; should be Logic not Integration |
| AC-F6-20 | Warning | 3-person sample statistically meaningless; "协调/自然" unquantified; questionnaire design not front-loaded |
| AC-F6-21 | Warning | BeatClock substitute strategy in EditMode unspecified; PlayMode path duplicates AC-F6-06 |
| AC-F6-22 | Info | ATTACK_LAYER_BLEND_IN reflection rename risk; dependency propagation state rule undefined |

## Critical Findings New in Round 2 (not in Round 1)

1. AC-F6-12/15: "C2 dependency" is a test design error, not a scheduling constraint — direct API calls remove the dependency
2. AC-F6-18: Duration measurement protocol absent — 16ms tolerance is an arbitrary number without measurement spec
3. AC-F6-19: IDSPClock interface has no defined home assembly or injection mechanism

## Still Missing ACs (2 of 3 from Round 1 unresolved)

- Missing AC-A: Out-of-sequence TDE event defensive behavior (e.g., Pause received while AllDisabled)
- Missing AC-B: LevelConfigSO invalid data handling (BPM=0, BPM=null, BgmAssetReference=null)

## Effectively Executable in F6 Sprint (After Rewrites)

- Without any rewrites: ~12 of 22 ACs independently executable
- After removing C2 dependency from AC-F6-12/15: ~16 of 22 executable
- Remaining C2-blocked: AC-F6-16 (needs Animancer playing during PlayerDeath), AC-F6-20 (needs C2 + AnimancerAttackPlayer integrated)
- AC-F6-19 blocked pending IDSPClock interface definition

## BLOCKING Actions Before F6 Sprint

1. AC-F6-01: Change test type to EditMode; add IL2CPP alternative verification
2. AC-F6-03: Split into EditMode IL scan + PlayMode Spy; specify Spy mechanism; declare reflection call prohibition
3. AC-F6-04: Rewrite body to reference AC-F6-21 mock injection path
4. AC-F6-08/09/10: Remove frameCount atomicity assertion; replace with final-state + call-count assertions
5. AC-F6-12/15: Remove C2 dependency — test via direct PlayAttack()/UpdateBpm() API calls
6. AC-F6-15: Change "下一帧" to "同帧" or justify delayed-frame design
7. AC-F6-18: Add Clip duration measurement protocol (Stopwatch approach recommended)
8. AC-F6-19: Define IDSPClock assembly; specify sync-mode mock injection; change type to Logic
9. Add Missing AC-A: Out-of-sequence TDE event defense
10. Add Missing AC-B: LevelConfigSO invalid data handling

## How to Apply

Round 3 review scope can be narrowed to: 10 rewritten ACs + 2 new ACs only.
Unchanged ACs (AC-F6-06, 07, 13, 17, 22 with minor fixes) do not need re-review.
