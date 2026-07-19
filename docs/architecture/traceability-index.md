# Traceability Index

> **Last Updated**: 2026-07-19 (Rev.6)
> **Source**: `/architecture-review full`
> **TR Registry**: `docs/architecture/tr-registry.yaml` (83 entries, v1)

---

## Coverage by System

| System | GDD | Total TRs | Covered | Partial | Gap |
|--------|-----|-----------|---------|---------|-----|
| concept | game-concept.md | 14 | 5 | 4 | 5 |
| beatclock | audio-beat-clock.md | 17 | 1 | 3 | 13 |
| input | input-event-system.md | 15 | 2 | 0 | 13 |
| datalayer | so-data-layer.md | 12 | 11 | 1 | 0 |
| save | save-persistence.md | 13 | 2 | 0 | 11 |
| tde | tde-integration.md | 12 | 8 | 0 | 4 |
| **Total** | | **83** | **29** | **8** | **46** |

---

## Coverage by ADR

| ADR | Status | TRs Covered | TRs Partial |
|-----|--------|-------------|-------------|
| ADR-D1 (TDE Integration) | Accepted | TR-tde-001, TR-tde-002, TR-tde-003, TR-tde-006, TR-tde-007, TR-input-010, TR-input-014 | TR-concept-001 |
| ADR-D2 (Enemy AI) | Accepted | TR-concept-009, TR-tde-008 | — |
| ADR-D3 (Rhythm Quantization) | **Accepted** | TR-concept-006, TR-concept-007 | TR-concept-008, TR-concept-010 |
| ADR-D4 (SO Data Layer) | **Accepted** | TR-datalayer-001 through 008, TR-datalayer-010, TR-datalayer-011, TR-datalayer-012, TR-concept-012 | TR-concept-013, TR-datalayer-009 |
| ADR-D5 (Animation Pipeline) | Accepted | TR-concept-004, TR-tde-004, TR-tde-005 | — |
| ADR-D6 (Audio Middleware) | Accepted | TR-concept-005, TR-beatclock-002 | TR-beatclock-010, TR-beatclock-011, TR-beatclock-013 |
| ADR-D7 (Save/Persistence) | Accepted | TR-save-005, TR-save-008 | TR-datalayer-009 |

---

## Coverage by Layer

| Layer | Total TRs | Covered | Partial | Gap | Notes |
|-------|-----------|---------|---------|-----|-------|
| Foundation (F2-F6) | 69 | 24 | 4 | 41 | datalayer fully covered; tde fully covered; beatclock/input/save gaps expected (GDD = spec) |
| Core (concept) | 14 | 5 | 4 | 5 | ADR-D3 covers concept-006/007; remaining gaps await C3/C5/FE3 GDDs |
| Feature | 0 | 0 | 0 | 0 | No Feature-layer GDDs designed yet |
| Presentation | 0 | 0 | 0 | 0 | No Presentation-layer GDDs designed yet |

---

## Gap Priority

### Critical path: Proposed ADRs — RESOLVED

All ADRs now Accepted (D1–D7). No remaining Proposed blockers.

| ADR | Status | Covers | Unblocks |
|-----|--------|--------|----------|
| ADR-D3 (Rhythm Quantization Core) | **Accepted** (2026-07-19) | TR-concept-006/007 | C1 GDD, C2 implementation |
| ADR-D4 (SO Data Layer) | **Accepted** (2026-07-19) | 11 datalayer TRs | C2, C3, C5, FE1 GDDs |

### Remaining architecture gaps (need future ADRs or GDDs)

| TR-ID | Requirement | Suggested Action | Priority |
|-------|-------------|------------------|----------|
| TR-concept-011 | Delay calibration system | FE3 GDD | P2 |
| TR-concept-014 | Multiple weapon types with ComboGraph | C5 GDD | P2 |
| TR-concept-002 | Multi-platform input support | Implementation concern, no ADR needed | P3 |
| TR-concept-003 | 60fps / 16.6ms frame budget | Project-wide constraint, not single-ADR scope | P3 |

### Notes on beatclock/input/save gaps

The 37 gaps in beatclock (13), input (13), and save (11) systems are **expected** — these systems have Approved GDDs that serve as the normative architecture specification. No separate ADR is needed for their internal design; the GDD is the architecture doc.

---

## Trend

| Review | Date | ADRs | TRs | Covered | Partial | Gap | Coverage % |
|--------|------|------|-----|---------|---------|-----|-----------|
| v1 | 2026-07-15 | 2 (D6, D7) | 69 | 14 | 6 | 49 | 20.3% |
| v2 | 2026-07-16 | 3 (D1, D6, D7) | 71 | 14 | 6 | 51 | 19.7%* |
| v3 | 2026-07-18 | 5 (D1-D2, D5-D7) | 79 | 22 | 8 | 49 | 27.8% |
| v4 | 2026-07-18 | 5 (D1-D2, D5-D7) | 79 | 22 | 8 | 49 | 27.8%* |
| v5 | 2026-07-18 | 5 (D1-D2, D5-D7) | 79 | 28 | 11 | 40 | 35.4% |
| **v6** | **2026-07-19** | **7 (D1-D7)** | **83** | **29** | **8** | **46** | **34.9%** |

*v2 was a focused ADR-D1 review, not a full recount; total rose to 71 due to 2 new TRs.
v4 re-ran with fresh engine specialist; no new ADRs or GDD changes → same coverage.
v6: +2 ADRs (D3, D4), +4 TRs (tde-009~012). Coverage % dipped slightly due to new TRs added to denominator; absolute covered count rose. ADR-D4 converts 10 datalayer gaps to covered. D3+D4 accepted 2026-07-19.

---

## Projected Coverage

| Milestone | ADRs Added | Projected Coverage |
|-----------|------------|-------------------|
| After ADR-D3 (P1) | D3 | ~50% |
| After ADR-D3 + D4 (P1+P2) | D3, D4 | ~60-65% |
| After all P1-P3 ADRs | D3, D4, D8, D9 | ~75-80% |
| After all planned ADRs | All | ~85-90% |

Remaining 10-15% will be Feature/Presentation layer TRs that emerge when those GDDs are written.
