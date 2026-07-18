# Traceability Index

> **Last Updated**: 2026-07-18 (Rev.3)
> **Source**: `/architecture-review full`
> **TR Registry**: `docs/architecture/tr-registry.yaml` (79 entries, v1)

---

## Coverage by System

| System | GDD | Total TRs | Covered | Partial | Gap |
|--------|-----|-----------|---------|---------|-----|
| concept | game-concept.md | 14 | 4 | 2 | 8 |
| beatclock | audio-beat-clock.md | 17 | 6 | 3 | 8 |
| input | input-event-system.md | 15 | 4 | 1 | 10 |
| datalayer | so-data-layer.md | 12 | 0 | 3 | 9 |
| save | save-persistence.md | 13 | 11 | 1 | 1 |
| tde | tde-integration.md | 8 | 4 | 2 | 2 |
| **Total** | | **79** | **22** | **8** | **49** |

---

## Coverage by ADR

| ADR | Status | TRs Covered | TRs Partial |
|-----|--------|-------------|-------------|
| ADR-D6 (Audio Middleware) | Accepted | TR-concept-001, TR-concept-005, TR-beatclock-002, TR-beatclock-010, TR-beatclock-011, TR-input-012 | TR-beatclock-013 |
| ADR-D7 (Save/Persistence) | Accepted | TR-save-001 thru TR-save-011, TR-save-013 | TR-save-006 |
| ADR-D1 (TDE Integration) | Accepted | TR-beatclock-008, TR-input-010, TR-input-014, TR-input-015, TR-datalayer-012, TR-tde-001, TR-tde-002, TR-tde-003, TR-tde-006 | TR-concept-002, TR-concept-009, TR-tde-007 |
| ADR-D2 (Enemy AI) | Proposed | TR-concept-009 | TR-concept-010, TR-tde-008 |
| ADR-D5 (Animation Pipeline) | Proposed | TR-concept-004, TR-tde-004, TR-tde-005 | TR-datalayer-004, TR-datalayer-007 |

---

## Coverage by Layer

| Layer | Total TRs | Covered | Partial | Gap | Notes |
|-------|-----------|---------|---------|-----|-------|
| Foundation (F1-F6) | 66 | 22 | 8 | 36 | save system well-covered; datalayer zero coverage |
| Core (C1-C6) | 13* | 0* | 2* | 11* | *concept/beatclock/input TRs mapping to Core systems; no Core ADR exists |
| Feature | 0 | 0 | 0 | 0 | No Feature-layer GDDs designed yet |
| Presentation | 0 | 0 | 0 | 0 | No Presentation-layer GDDs designed yet |

---

## Gap Priority

### Architecture-level gaps (need new ADRs)

| TR-ID | Requirement | Suggested ADR | Priority |
|-------|-------------|---------------|----------|
| TR-concept-006 | Half-beat grid snap quantization | ADR-D3 Rhythm Quantization Core | **P1** |
| TR-concept-007 | Rating system (P/G/M) with multipliers | ADR-D3 | **P1** |
| TR-beatclock-003 | Beat prediction and lookahead | ADR-D3 | **P1** |
| TR-beatclock-004 | Drift correction (anchored DSP) | ADR-D3 | **P1** |
| TR-beatclock-005 | Subdivision handling (half/quarter-beat) | ADR-D3 | **P1** |
| TR-input-001 | Input timestamp → beat-relative offset | ADR-D3 | **P1** |
| TR-input-002 | Tolerance window per rating tier | ADR-D3 | **P1** |
| TR-datalayer-* (9 gaps) | SO schema, Addressables, Odin validation | ADR-D4 SO Data Layer | **P2** |
| TR-concept-011+ | Enemy beat-lock, token scheduling | ADR-D8 Combat Director | **P2** |
| TR-concept-008 | Perfect kinetics effects | ADR: Hitbox/Hit Response | P3 |
| TR-concept-010+ | Combo graph, cancel windows | ADR-D9 Combo System | P3 |

### Implementation-level gaps (covered by stories, not ADRs)

| TR-ID | Requirement | Notes |
|-------|-------------|-------|
| TR-save-006 | Slot metadata caching strategy | D7 partially covers; story-level detail |
| TR-tde-007 | Pause menu integration with BeatClock | D1 partially covers; implementation story |

---

## Trend

| Review | Date | ADRs | TRs | Covered | Partial | Gap | Coverage % |
|--------|------|------|-----|---------|---------|-----|-----------|
| v1 | 2026-07-15 | 2 (D6, D7) | 69 | 14 | 6 | 49 | 20.3% |
| v2 | 2026-07-16 | 3 (D1, D6, D7) | 71 | 14 | 6 | 51 | 19.7%* |
| **v3** | **2026-07-18** | **5 (D1-D2, D5-D7)** | **79** | **22** | **8** | **49** | **27.8%** |

*v2 was a focused ADR-D1 review, not a full recount; total rose to 71 due to 2 new TRs.

---

## Projected Coverage

| Milestone | ADRs Added | Projected Coverage |
|-----------|------------|-------------------|
| After ADR-D3 (P1) | D3 | ~45% |
| After ADR-D3 + D4 (P1+P2) | D3, D4 | ~55-60% |
| After all P1-P3 ADRs | D3, D4, D8, D9 | ~70-75% |
| After all planned ADRs | All | ~85-90% |

Remaining 10-15% will be Feature/Presentation layer TRs that emerge when those GDDs are written.
