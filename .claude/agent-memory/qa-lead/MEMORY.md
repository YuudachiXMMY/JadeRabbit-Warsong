# QA Lead Memory Index

- [Project: QA Gate Status](project_qa_gate_status.md) — concept GDD is BLOCKING; no system GDDs may be authored until 4 blocking findings resolved
- [Project: M2 Playtest Requirements](project_m2_playtest.md) — defined 6 required metrics and thresholds for M2 external playtest gate
- [Project: System Test Classifications](project_system_test_types.md) — pre-classified all 5 core systems by story type and required test evidence
- [Project: F4 SO Data Layer AC Review](project_f4_ac_review.md) — 5 BLOCKING-level AC problems found; GDD rewrite required before F4 implementation stories begin
- [Project: F5 Save/Persistence AC Review](project_f5_ac_review.md) — 5 CRITICAL + 6 WARNING (two-pass review); GDD state table gap on VersionTooHigh + GlobalData corruption path; rewrite required before F5 stories begin
- [Project: F3 Input Event System AC Review](project_f3_ac_review.md) — 3 BLOCKER findings (R8-01 new AC-F3-21 updateMode missing; R8-05 AC-F3-14 isSwitchingMaps IL2CPP-unsafe; R8-06 AC-F3-03 mock clock unspecified); round-8 adversarial pass post-round-7 GDD approval
- [Project: F6 TDE Integration Layer AC Generation](project_f6_ac_review.md) — 18 ACs generated (no blockers); AC-F6-02 needs TDE API name confirmation; AC-F6-15 dependency-blocked on C2 sprint
