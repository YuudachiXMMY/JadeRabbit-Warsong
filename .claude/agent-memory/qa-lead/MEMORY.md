# QA Lead Memory Index

- [Project: QA Gate Status](project_qa_gate_status.md) — concept GDD is BLOCKING; no system GDDs may be authored until 4 blocking findings resolved
- [Project: M2 Playtest Requirements](project_m2_playtest.md) — defined 6 required metrics and thresholds for M2 external playtest gate
- [Project: System Test Classifications](project_system_test_types.md) — pre-classified all 5 core systems by story type and required test evidence
- [Project: F4 SO Data Layer AC Review](project_f4_ac_review.md) — 5 BLOCKING-level AC problems found; GDD rewrite required before F4 implementation stories begin
- [Project: F5 Save/Persistence AC Review](project_f5_ac_review.md) — 5 CRITICAL + 6 WARNING (two-pass review); GDD state table gap on VersionTooHigh + GlobalData corruption path; rewrite required before F5 stories begin
- [Project: F3 Input Event System AC Review](project_f3_ac_review.md) — 6 BLOCKER findings (3 R8 carried + 3 new: R9-01 DisableAllInput+ChargedAttack gap, R9-02 UIActive→GameplayActive T3 untested, R9-04 AC-F3-03 missing chargeStartDspTime > -1.0); round-9 adversarial pass
- [Project: F6 TDE Integration Layer AC Adversarial Review](project_f6_ac_review.md) — Re-review of 22 ACs: 6 new Critical findings; 10 BLOCKING rewrites required; 2 missing ACs (out-of-sequence event + LevelConfigSO invalid data) still unresolved
