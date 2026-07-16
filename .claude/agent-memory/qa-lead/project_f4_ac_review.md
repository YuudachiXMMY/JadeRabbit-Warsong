---
name: F4 SO Data Layer — AC Testability Review (Fourth Pass)
description: Fourth adversarial review; 4 BLOCKING findings (R9-01 through R9-05); AC-F4-08 re-escalated to BLOCKING for wrong grep anchor; AC-F4-31 and AC-F4-32 promoted to BLOCKING; AC-F4-02a identified as structural false positive
type: project
---

Reviewed 2026-07-15 (fourth pass, adversarial mode).

## Gate Status: STILL BLOCKED — 4 BLOCKING findings

Three issues from prior reviews (AC-F4-08 untestable, AC-F4-01 fabricated error string,
AC-F4-06 not an AC) remain unresolved across four consecutive reviews. Fourth pass also
identified two new BLOCKING items (AC-F4-31 classification and AC-F4-02a false positive).

## BLOCKING Findings (Fourth Pass)

**R9-01 — AC-F4-31 classification is ADVISORY for a crash-path defect [BLOCKING]**
ComboEdge index out-of-range causes `IndexOutOfRangeException` in C2 at runtime. The GDD
Edge Cases prose says "Error and blocks build" but the AC gate says ADVISORY. GDD language
mismatch. Odin Validator runs only against existing assets; nodes shrinkage after edge
creation can produce a valid-then-invalid index without re-triggering Validator. Promote
AC-F4-31 to BLOCKING. C2 runtime test remains a separate additional requirement.

**R9-02 — AC-F4-32 classification is ADVISORY for the worst silent failure [BLOCKING]**
`hitboxDurationNormalized = 0.0` causes a weapon to fire animation with zero hit frames
— no crash, no log, weapon completely inert. GDD itself calls this "最危险的静默失效路径."
The Validator `[ValidateInput]` check (`value > 0.0f`) has no cross-field dependency and
is straightforward to implement. Promote AC-F4-32 to BLOCKING.

**R9-03 — AC-F4-02a IsDirty check is a structural false positive [BLOCKING]**
`EditorUtility.IsDirty(asset)` returns false after raw reflection `SetValue` because Unity
dirty-tracking is only set via Undo-registered paths or `EditorUtility.SetDirty()`. The
test will always pass regardless of whether unintended public setters exist — it cannot
detect what it claims to detect. Rewrite AC-F4-02a as a static check:
`grep -n "public.*{ get; set; }" Assets/Scripts/F4DataLayer/AttackNodeSO.cs` → 0 results.
Remove the `EditorUtility.IsDirty` assertion entirely.

**R9-05 — AC-F4-08 grep `^` anchor misses indented Sirenix imports [BLOCKING]**
`grep -rn "^using Sirenix"` anchors to line start. C# files using namespace blocks or
`#if UNITY_EDITOR` guards with indented body write `    using Sirenix.OdinInspector;`
(leading whitespace). The `^` anchor does not match these — false negatives on indented
unguarded imports. Replace with a two-pass CI script or Roslyn analyzer: find all
`using Sirenix` (no anchor); for each match, walk up to confirm inside `#if UNITY_EDITOR`.
This was RECOMMENDED in third review; escalated to BLOCKING because the specified mechanism
is actively wrong (produces false negatives on real violation patterns).

## RECOMMENDED Findings (Fourth Pass)

**R9-04 — AC-F4-06 still present as AC after 3 removal flags [RECOMMENDED]**
GDD adds a note "(过程规则 — 非 QA 可自动验证项)" inside the AC record — this is
contradictory. AC that cannot be verified is not an AC. Delete from AC section; move to
PR checklist template. This item has been flagged in passes 2, 3, and 4. If not fixed in
next revision, escalate to producer as documentation process failure.

**R9-06 — AC-F4-02b DoD strategy unresolved for third consecutive review [RECOMMENDED]**
"须在 C2 和 C5 实现后方可执行" still has no explicit handling at F4 Done gate. GDD author
must choose: Option A (test file not created during F4; AC transferred to C2 story) or
Option B (file created with `[Ignore("Requires C2+C5")]`; C2 story must remove attribute).
Without this decision, CI state at F4 Done is undefined.

**R9-07 — PerkParameter.key has no validity AC [RECOMMENDED]**
`PerkParameter.key` is a free string with no enumeration constraint. A misspelled key
(`"damageMultiplyer"`) passes all Validator checks but silently has no effect in C3.
Needs a Validator AC that checks each PerkSO's parameter keys are from the permitted set
for its effectType. Blocked on C3 GDD finalising PerkEffectType — add to C3 GDD Open
Questions; flag as deferred ADVISORY AC.

**R9-08 — ComboGraph cycle depth upper bound is non-binding [RECOMMENDED]**
"建议 100 步" is a comment-level recommendation. No AC anywhere requires C2 to implement
a specific constant, test the circuit-breaker at that depth, or verify it logs an error
rather than crashing. Must be promoted to a BLOCKING AC in C2 GDD: define the constant,
test it fires, test it logs Error and aborts traversal.

## Recurring Unresolved Items (Across All Four Reviews)

- AC-F4-08: untestable / wrong mechanism — flagged rounds 2, 3, 4
- AC-F4-06: not an AC, should be PR checklist — flagged rounds 2, 3, 4
- AC-F4-01: fabricated "Unknown SO type" error string — flagged rounds 2, 3 (still
  present; the grep rewrite was documented in round 3 memory but not applied to GDD)
- AC-F4-02b: DoD strategy unresolved — flagged rounds 3, 4

**If fifth review is required, escalate AC-F4-08, AC-F4-06, and AC-F4-01 to producer.**

## Summary of Required Changes Before F4 Implementation Stories

BLOCKING (must fix before implementation):
1. Promote AC-F4-31 to BLOCKING
2. Promote AC-F4-32 to BLOCKING
3. Rewrite AC-F4-02a — remove IsDirty assertion; replace with grep/Roslyn static check
4. Rewrite AC-F4-08 grep to remove `^` anchor; specify two-pass script or Roslyn analyzer
5. (Carried from prior reviews) Rewrite AC-F4-08 to BLOCKING gate level
6. (Carried from prior reviews) Rewrite AC-F4-01 as grep class-count check

RECOMMENDED (fix before sprint review):
- Delete AC-F4-06 from AC section → PR template
- Document AC-F4-02b DoD handling choice (Option A or B)
- Add PerkParameter.key validity AC (deferred pending C3 GDD)
- Add C2 GDD cycle-depth BLOCKING AC requirement
