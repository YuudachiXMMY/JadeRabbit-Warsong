---
name: M2 External Playtest — Required Metrics
description: Defined 6 required metrics and thresholds for the M2 concept validation playtest gate
type: project
---

M2 is the validation gate for the core concept (rhythm-action fusion). The GDD planned "5-10 external players A/B testing quantization tiers" but lacked protocol and thresholds.

Required metrics established in QA review (2026-07-13):

| Metric | Method | Threshold |
|---|---|---|
| Replay intent rate | Post-play survey, binary question | >=70% yes |
| Rhythm-action fusion perception | Coded open debrief | >=60% describe rhythm as "core" |
| Unprompted second run attempts | Facilitator observation log | >=30% of participants |
| "Fun despite misses" rate | Survey question | >=50% yes |
| Calibration completion rate | Session observation | 100% complete or skip; 0% abandon |
| First death time | Timer | >3 minutes average |

A/B test note: 3-way split (Off/HalfBeat/Beat) over 5-10 players = 1-3 data points per variant. Insufficient. Recommend 15-20 participants or define a single deciding metric per variant before M2.

No-Go condition: if fewer than 2 of the first 3 metrics pass, the core concept requires redesign before proceeding to Vertical Slice scope.

**Why:** GDD named M2 as the concept validation gate but gave no pass/fail definition — a gate without a threshold is not a gate.

**How to apply:** Before M2 playtesting begins, confirm these thresholds are documented in the playtest protocol file. After M2, report against all 6 metrics.
