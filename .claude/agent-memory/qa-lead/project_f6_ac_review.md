---
name: F6 TDE Integration Layer — AC Adversarial Review
description: Round 3 review of 22 ACs; 8 Critical + 5 Major + 5 Minor findings; 2 missing ACs still absent (3 consecutive rounds); 3 Player Fantasy structural risks; F6 sprint still blocked
type: project
---

Generated 2026-07-19. Review history: Round 1 (2026-07-18, 18 ACs), Round 2 (2026-07-18, 22 ACs), Round 3 (2026-07-19, 22 ACs).

## Gate Status: STILL BLOCKED — 10 minimum actions required before F6 sprint

## Round 3 Findings

### Critical — Blocking

| ID | AC | Description |
|----|-----|-------------|
| R3-C1 | AC-F6-01 | Test type STILL PlayMode, not EditMode (3 rounds). IL scan requires EditMode; IL2CPP alternate path absent. BLOCKING. |
| R3-C2 | AC-F6-03 | IL-scan/PlayMode Spy contradiction unresolved (3 rounds). Spy mechanism unspecified. Reflection call prohibition undeclared. BLOCKING. |
| R3-C3 | AC-F6-08/09/10 | `Time.frameCount` atomicity assertion unchanged (3 rounds). Proves nothing about atomicity within a single Update pass. BLOCKING. |
| R3-C4 | AC-F6-12 | "依赖 C2 实现" label still present (3 rounds). Test design error — direct PlayAttack() call removes dependency. BLOCKING. |
| R3-C5 | AC-F6-15 | "依赖 C2 实现" still present; "下一帧" contradicts synchronous direct-assignment in ADR-D5. BLOCKING. |
| R3-C6 | AC-F6-18 | 16ms Clip duration measurement protocol absent (2 rounds). Tolerance unjustified against Player Fantasy. BLOCKING. |
| R3-N1 | BGM Transition | `BeatClock.ScheduleBGMTransition()` does not exist in F2 GDD (grep confirmed zero results). No AC covers Boss BPM BGM handoff. Highest-stakes Player Fantasy moment has zero coverage. CRITICAL. |
| R3-N2 | AC-F6-16 | Death-during-attack race (MMEventListener order unguaranteed, EC-04 confirmed) tested with ADVISORY "目视确认". Race requires automated BLOCKING Integration test. "幽灵攻击" prevention is named in Player Fantasy Section B. |

### Major — New in Round 3

| ID | AC | Description |
|----|-----|-------------|
| R3-N3 | AC-F6-04 / AC-F6-21 | AC-F6-04 body still describes mocking sealed FMOD class (impossible). AC-F6-21 provides correct mock path. Overlap + unimplementable body. |
| R3-N4 | AC-F6-19 | Test type still Integration (should be Logic). IDSPClock injection point not specified. Sync-mode mock path unexecutable. |
| R3-N5 | Missing AC-A | Out-of-sequence TDE event defense. Third consecutive round without this AC. |
| R3-N6 | Missing AC-B | LevelConfigSO invalid data (BPM=0, BPM=NaN, BgmAssetReference=null). Third consecutive round. BPM=0 causes silent infinite beat intervals — Player Fantasy collapses with no error. |
| R3-N7 | ADR-D5 Gap | ADR-D5 says UpdateBpm() "NOT called in MVP path." GDD says Option A (mid-attack UpdateBpm()) is active in MVP. No ADR amendment recorded. Programmer following ADR-D5 will not call UpdateBpm() mid-attack — AC-F6-15 fails; Boss BPM feel moment broken. |

### Minor — New in Round 3

| ID | AC | Description |
|----|-----|-------------|
| R3-N8 | AC-F6-02 | Ability name still "如 Dash" (unspecified). "未触发" measurement method absent. Third round. |
| R3-N9 | AC-F6-20 | 3-person sample size meaningless. Questionnaire not templated. "协调/自然" undefined. Minimum 5 persons + questionnaire template required. |
| R3-N10 | AC-F6-21 | PlayMode path duplicates AC-F6-06 (redundant AC overlap). AC-F6-21 should be EditMode only. |
| R3-N11 | None | ConfirmDSPStart three-step protocol (Core Rule 11) has no AC. 93ms drift failure named; zero AC coverage. |
| R3-N12 | AC-F6-22 | ATTACK_LAYER_BLEND_IN field-name reflection rename risk (carry-forward advisory). Assert effect, not field name. |

## Player Fantasy Structural Risks

- **PF-1**: ScheduleBGMTransition missing from F2 GDD — Boss BPM phase switch changes clock without changing music; peak "this strike is a note" moment breaks silently.
- **PF-2**: ATTACK_LAYER_BLEND_IN=0.05s has no feel-test AC. Tuning Knob footnote incorrectly references AC-F6-19 (FMOD test). Should link to AC-F6-20. Blend seam occurs every attack.
- **PF-3**: AC-F6-14 root motion assertion covers attack phase only. Position pop at PlayController() return (attack-to-locomotion transition) is untested. "Seamless" breaks precisely at transition, not during attack.

## Minimum Blocking Actions Before F6 Sprint

1. AC-F6-01: Change to EditMode + add IL2CPP alternate (Roslyn analyzer or CI rule)
2. AC-F6-03: Split into two ACs; specify Spy mechanism; declare reflection prohibition
3. AC-F6-04: Rewrite to delegate to AC-F6-21 mock path OR delete and roll into AC-F6-21
4. AC-F6-08/09/10: Replace frameCount assertion with final-state + call-count + fault-injection
5. AC-F6-12/15: Remove C2 dependency; call PlayAttack()/UpdateBpm() directly in test; fix "下一帧"→same-frame
6. AC-F6-18: Add Clip duration measurement protocol (Stopwatch or NormalizedTime correlation); justify 16ms threshold
7. AC-F6-16: Change gate to BLOCKING Integration (automated); remove "目视确认" as sole evidence
8. ADR-D5: Record Option A decision formally — amend ADR-D5 to Accepted-Modified before F6 sprint
9. F2 GDD: Add ScheduleBGMTransition() OR explicitly scope Boss BGM transition out of F6 with blocked AC
10. Add Missing AC-A (out-of-sequence events) and Missing AC-B (invalid LevelConfigSO data)

## Round 3 Verdict by AC

| AC | R3 Severity | Primary Issue |
|----|-------------|---------------|
| AC-F6-01 | Critical | Test type wrong (3 rounds); IL2CPP path absent |
| AC-F6-02 | Minor | Ability unspecified; "未触发" measurement absent (3 rounds) |
| AC-F6-03 | Critical | IL/Spy contradiction (3 rounds); reflection blind spot; Spy unspecified |
| AC-F6-04 | Major | Unimplementable sealed-class mock; duplicates AC-F6-21 intent |
| AC-F6-05 | Warning | Spy mechanism unspecified (3 rounds) |
| AC-F6-06 | Pass | — |
| AC-F6-07 | Pass | — |
| AC-F6-08 | Critical | frameCount atomicity (3 rounds); fault-injection absent |
| AC-F6-09 | Critical | Same as AC-F6-08 |
| AC-F6-10 | Critical | Same as AC-F6-08; "未响应" unquantified |
| AC-F6-11 | Warning | Sequence number acquisition unspecified |
| AC-F6-12 | Critical | C2 dependency wrong design (3 rounds) |
| AC-F6-13 | Info | authoredBpm=0 divide-by-zero missing |
| AC-F6-14 | Minor | Root motion assertion misses PlayController() transition pop |
| AC-F6-15 | Critical | C2 dependency + "下一帧" contradiction (3 rounds) |
| AC-F6-16 | Critical | ADVISORY gate for timing race; Player Fantasy named requirement |
| AC-F6-17 | Pass | — |
| AC-F6-18 | Critical | Duration measurement protocol absent (2 rounds) |
| AC-F6-19 | Major | Wrong type (Integration→Logic); IDSPClock injection unspecified |
| AC-F6-20 | Minor | 3-person sample meaningless; questionnaire absent |
| AC-F6-21 | Minor | PlayMode path duplicates AC-F6-06 |
| AC-F6-22 | Minor | Field-name rename risk |

## How to Apply

Round 4 review scope: 10 rewritten/new ACs only. AC-F6-06, 07, 13, 17 confirmed Pass — do not re-review unless changed.
