# Traceability Index

> **Last Updated**: 2026-07-15 (Rev.2)
> **Source**: `/architecture-review full`
> **TR Registry**: `docs/architecture/tr-registry.yaml` (69 entries, v1)

---

## Coverage by System

| System | GDD | Total TRs | Covered | Partial | Gap |
|--------|-----|-----------|---------|---------|-----|
| concept | game-concept.md | 14 | 2 | 1 | 11 |
| beatclock | audio-beat-clock.md | 17 | 4 | 2 | 11 |
| input | input-event-system.md | 14 | 3 | 1 | 10 |
| datalayer | so-data-layer.md | 11 | 1 | 1 | 9 |
| save | save-persistence.md | 13 | 4 | 1 | 8 |
| **Total** | | **69** | **14** | **6** | **49** |

---

## Coverage by ADR

| ADR | Status | TRs Covered | TRs Partial |
|-----|--------|-------------|-------------|
| ADR-D6 (Audio Middleware) | Accepted | TR-concept-001, TR-concept-005, TR-beatclock-002, TR-beatclock-010, TR-beatclock-011, TR-input-012 | TR-beatclock-013 |
| ADR-D7 (Save Middleware) | Accepted | TR-save-005, TR-save-007, TR-save-008, TR-save-013 | TR-datalayer-009, TR-save-006 |
| ADR-D1 (TDE Integration) | Proposed | TR-beatclock-008, TR-input-010, TR-input-014, TR-datalayer-010 | TR-concept-009, TR-beatclock-012, TR-input-004 |

---

## Coverage by Layer

| Layer | Total TRs | Covered | Partial | Gap | Notes |
|-------|-----------|---------|---------|-----|-------|
| Foundation (F1-F6) | 55 | 12 | 5 | 38 | Most gaps are implementation-level (GDD-specified), not architecture-level |
| Core (C1-C6) | 14* | 2* | 1* | 11* | *concept TRs that map to Core systems |
| Feature | 0 | 0 | 0 | 0 | No Feature-layer GDDs designed yet |
| Presentation | 0 | 0 | 0 | 0 | No Presentation-layer GDDs designed yet |

---

## Gap Priority

### Architecture-level gaps (need new ADRs)

| TR-ID | Requirement | Suggested ADR | Priority |
|-------|-------------|---------------|----------|
| TR-concept-004 | Animancer BPM-adaptive speed | ADR-D5 Animation Pipeline | 1 |
| TR-concept-006 | Half-beat grid snap | ADR-D3 Rhythm Quantization Core | 3 |
| TR-concept-007 | Rating system multipliers | ADR-D3 | 3 |
| TR-concept-009 | Enemy beat-lock (partial) | ADR-D2 Enemy AI Framework | 2 |

### Implementation-level gaps (covered by GDD specs, no ADR needed)

49 total gaps minus 4 architecture-level = **45 implementation-level gaps**.
These will be closed by story implementation following GDD specifications.

---

## Review History

| Date | Reviewer | GDDs | ADRs | Covered | Partial | Gap | Verdict |
|------|----------|------|------|---------|---------|-----|---------|
| 2026-07-15 v1 | /architecture-review | 5 | 2 | 12 | 5 | 52 | CONCERNS |
| 2026-07-15 v2 | /architecture-review full | 5 | 3 | 14 | 6 | 49 | CONCERNS |
