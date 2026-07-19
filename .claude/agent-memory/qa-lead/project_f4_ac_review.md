---
name: F4 SO Data Layer — AC Testability Review (Sixth Pass)
description: Round-6 adversarial review (2026-07-19); 4 BLOCKING findings; 2 new (AC-F4-07 Odin CI infeasibility, AC-F4-08 Cond.2 ambiguous subject); 2 carried (AC-F4-02b DoD unresolved, AC-F4-31/32 section placement)
type: project
---

Reviewed 2026-07-19 (sixth pass, adversarial mode).

## Gate Status: STILL BLOCKED — 4 BLOCKING findings

Round-5 revision resolved 3 prior blockers (AC-F4-02a IsDirty false positive, AC-F4-08 ^
anchor, AC-F4-01 fabricated error string). Two BLOCKING items remain unresolved, and two
new BLOCKING items were found in Round-6.

## What Round-5 RESOLVED (do not re-flag)

- **AC-F4-02a IsDirty false positive**: Current text explicitly prohibits EditorUtility.IsDirty()
  and mandates field snapshot comparison only. RESOLVED.
- **AC-F4-08 `^` anchor**: Current text explicitly states "不使用 `^` 行首锚点". RESOLVED.
- **AC-F4-01 fabricated error string**: grep class-count check no longer references any
  fabricated error string. RESOLVED.

## BLOCKING Findings (Round-6)

**R10-01 — AC-F4-02b DoD strategy still unresolved (5th consecutive review) [BLOCKING]**
"前提条件：须在 C2 和 C5 实现后方可执行" documents a dependency but makes no decision
about CI state at F4 Done gate. Two possible world-states at F4 Done (test file absent, or
present with [Ignore] attribute) produce different CI behaviors, and neither is specified.
Cross-story ownership of test file creation is undefined.
Fix: Choose and document Option A (file not created; AC transferred to C2/C5 story DoD as
explicit BLOCKING item) or Option B (file created with [Ignore("Requires C2+C5 — see C2
Story STORY-XXX")]; C2 story DoD must include removing the attribute). One sentence added
to AC-F4-02b body is sufficient.

**R10-02 — AC-F4-08 Condition 2 subject is ambiguous ("所有 F4 SO 类 .cs 文件的 asmdef 引用") [BLOCKING]**
".cs files" do not have asmdef references — asmdef JSON files do. The phrase "所有 F4 SO 类
.cs 文件的 asmdef 引用" has two parsings: (A) the asmdef containing those .cs files, or
(B) asmdef-reference strings within the .cs files themselves (which do not exist). A
developer implementing CI from this AC may produce a permanently-green false-negative check
by grepping .cs files for asmdef content (finding nothing, always passing).
Fix: Rewrite Condition 2 to specify the target as ".asmdef files whose scope includes F4
SO classes", e.g.: `grep -l 'com.sirenix.odinspector.editor' Assets/Scripts/**/*.asmdef`
→ 0 matches (excluding Assets/Editor/ asmdef files).

**R10-03 — AC-F4-07 Odin Validator has no documented headless CI invocation path [BLOCKING]**
New finding not raised in any prior review. Odin Validator's scan triggers via Editor menu
or [InitializeOnLoadMethod], neither of which is reliably available in Unity -batchmode.
The expected log string "Odin Validator scan complete" is fabricated — not documented as
a stable Odin output string. No -executeMethod target or equivalent CI invocation is
specified. A developer implementing this CI step will either skip it (vacuous pass) or
produce a permanently-green false CI step.
Fix: Choose one option —
  Option A (Recommended): Implement a static Editor method callable via -executeMethod that
  invokes OdinValidatorSingleton.Instance.RunValidation(), captures Error-level results,
  and calls EditorApplication.Exit(1) on errors. Document the class.method name in AC-F4-07.
  Option B: Downgrade AC-F4-07 to a pre-PR manual step; remove CI integration claim.
  Option C: Investigate game-ci/unity-test-runner + [InitializeOnLoadMethod] behavior;
  document invocation flags if viable.

**R10-04 — AC-F4-31 and AC-F4-32 physically located in ADVISORY section despite BLOCKING classification [BLOCKING]**
Both ACs were promoted to BLOCKING in Round-4 but their bodies were never moved from the
ADVISORY section. A developer reading top-down sees the ADVISORY section header first and
may treat them as ADVISORY. A QA tester scanning the BLOCKING section will not find them.
Fix: Move AC-F4-31 and AC-F4-32 bodily into the BLOCKING section. Remove inline *(BLOCKING)*
tags (redundant once in correct section).

## ADVISORY Findings (Round-6)

**R10-05 — AC-F4-05 `cloneCountDuringRun` is an undefined variable in the test body [ADVISORY]**
AC-F4-05 (already ADVISORY, migrated to C3 GDD) references `cloneCountDuringRun` in a
precondition assertion but the Given block does not define where this variable comes from in
the test. Also pre-specifies C3's internal field name `_activeClones` — if C3 uses a different
name, the test fails to compile, not at assertion.
Fix: Add a note that C3 GDD must define implementation details independently, not copy
field names verbatim from F4 GDD.

**R10-06 — AC-F4-09 claims programmatic verification of Addressable Group membership that requires Editor context [ADVISORY]**
"Glob 扫描 Assets/Resources/" (shell-automatable) and "全部 7 类 SO 资产均归属至少一个 Addressable
Group" (requires AddressableAssetSettingsDefaultObject.Settings.FindAssetEntry() — Unity
Editor context) are presented at the same automation level. A developer may satisfy only
the shell check and consider the AC done.
Fix: Split into two explicit sub-checks with labeled automation modes (shell vs. Editor).

## RECOMMENDED (Escalation)

**R10-07 — AC-F4-06 still present as an AC after 5 reviews [ESCALATE TO PRODUCER]**
AC-F4-06 remains in the AC section with a self-contradicting inline note saying it cannot
be auto-verified. Has been flagged in rounds 2, 3, 4, 5, and 6. Round-4 memory said
"escalate to producer if not fixed in next revision." This is now 2 reviews past that
threshold. Fix is one action: delete AC-F4-06 from the GDD; add to .github/pull_request_template.md.

## Required Changes Before F4 Implementation Stories Begin

BLOCKING (all 4 must be fixed before any F4 story enters development):
1. R10-01: Document Option A or B for AC-F4-02b test file creation ownership
2. R10-02: Rewrite AC-F4-08 Condition 2 to target .asmdef files, not .cs files
3. R10-03: Specify Odin Validator CI invocation path in AC-F4-07 (or downgrade to manual)
4. R10-04: Move AC-F4-31 and AC-F4-32 from ADVISORY section to BLOCKING section

ADVISORY (fix before sprint review):
- R10-05: Add note to AC-F4-05 that C3 GDD must define field names independently
- R10-06: Split AC-F4-09 into shell-automatable and Editor-context sub-checks

ESCALATE:
- R10-07: Producer decision on AC-F4-06 removal (5 reviews without action)
