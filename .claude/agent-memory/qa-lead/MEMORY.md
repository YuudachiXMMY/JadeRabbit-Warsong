# QA Lead Memory Index

- [Project: QA Gate Status](project_qa_gate_status.md) — concept GDD is BLOCKING; no system GDDs may be authored until 4 blocking findings resolved
- [Project: M2 Playtest Requirements](project_m2_playtest.md) — defined 6 required metrics and thresholds for M2 external playtest gate
- [Project: System Test Classifications](project_system_test_types.md) — pre-classified all 5 core systems by story type and required test evidence
- [Project: F4 SO Data Layer AC Review](project_f4_ac_review.md) — Round-6 (2026-07-19): 4 BLOCKING (R10-01 AC-F4-02b DoD unresolved, R10-02 AC-F4-08 Cond2 ambiguous .cs/.asmdef, R10-03 AC-F4-07 no CI invocation path, R10-04 AC-F4-31/32 in wrong section); escalate AC-F4-06 to producer
- [Project: F5 Save/Persistence AC Review](project_f5_ac_review.md) — 5 CRITICAL + 6 WARNING (two-pass review); GDD state table gap on VersionTooHigh + GlobalData corruption path; rewrite required before F5 stories begin
- [Project: F3 Input Event System AC Review](project_f3_ac_review.md) — 6 BLOCKER findings (3 R8 carried + 3 new: R9-01 DisableAllInput+ChargedAttack gap, R9-02 UIActive→GameplayActive T3 untested, R9-04 AC-F3-03 missing chargeStartDspTime > -1.0); round-9 adversarial pass
- [Project: F6 TDE Integration Layer AC Adversarial Review](project_f6_ac_review.md) — Round 3 (2026-07-19): 8 Critical + 5 Major + 5 Minor; 2 missing ACs absent (3 rounds); 3 Player Fantasy risks; F6 sprint blocked; 10 minimum actions required
- [Project: C1 Rhythm Quantization System AC Adversarial Review](project_c1_ac_review.md) — Round 4 (2026-07-21): 17 BLOCKING (13 carried + 4 new: B-18a LateWindowMs边界点矛盾, B-18 skip-toggle纯事件不可验证, B-19a Off×IsStartConfirmed盲区, B-19b BPM变化skip-toggle相位未定义); C1 sprint blocked
- [Project: F2 Beat Clock AC Review](project_f2_ac_review.md) — Round 1 (2026-08-08): B2b gate conflicts with F2's existing self-pull LatencyOffsetMs model; no ISaveService mock interface exists; drafted AC-F2-24/25a-c + recommended AC-F6-23 (not F2) for the gate
