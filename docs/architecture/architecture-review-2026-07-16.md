# Architecture Review Report — ADR-D1 (Focused)

> **Date:** 2026-07-16
> **Engine:** Unity 6.3 LTS (6000.3.19f1)
> **GDDs Referenced:** 4 (F1, F2, F3, F4)
> **ADRs Reviewed:** 3 (D1, D6, D7)
> **Mode:** Focused — ADR-D1 acceptance readiness
> **Revision:** 2 (re-verified after ADR-D1 accepted)

---

## Traceability Summary

| Metric | Count |
|--------|-------|
| TR-IDs ADR-D1 claims to address | 6 |
| Correctly matched (Rev.1) | 3 |
| Mismatched (Rev.1) | 3 |
| **Post-fix correctly matched** | **5** (2 new TR-IDs created, 1 pseudo-requirement removed) |

---

## B1 — TR-ID Mismatches (RESOLVED — Rev.1)

ADR-D1 Requirements section referenced 3 incorrect TR-IDs:

| ADR-D1 Requirement | Old (Wrong) TR-ID | Registry Actual Text | Fix |
|---|---|---|---|
| LevelConfigSO → BeatClock injection | TR-datalayer-010 | [NonSerialized] field init timing | Created **TR-datalayer-012**; updated ADR-D1 |
| FMOD async init mode at boot | TR-input-011 | ChargedAttack Hold state machine | Created **TR-input-015**; updated ADR-D1 |
| F6 GDD authoring enablement | TR-beatclock-012 | Subscriber lifecycle contract | Removed — ADR outcome, not GDD requirement |

**Status:** All 3 fixes applied to `tr-registry.yaml` and `adr-d1-tde-integration.md`.

---

## W1 — F2 GDD Dependency Table Stale Text (RESOLVED — Rev.1)

`design/gdd/audio-beat-clock.md` dependency table row for F6→F2 said "BGM AudioSource 引用".
ADR-D6 chose FMOD — updated to "BGM FMOD asset 引用".

Note: F2 GDD formula sections (F2-F7, F2-F3) already correctly reference `FMODDSPClockImpl`
and `IDSPClock.GetCurrentTime()`. Only the dependency summary table was stale.

---

## W2 — TDE Plugin Reference ADR Table (RESOLVED — Rev.1)

`docs/engine-reference/unity/plugins/top-down-engine.md` ADR References table listed
"D3（待创建）" as the TDE integration ADR. Updated to reference ADR-D1. Animation pipeline
ADR updated from "D4（待创建）" to "ADR-D5（待创建）" to match ADR-D1's dependency graph.

---

## W3 — FMOD Init Timing (PENDING)

MEDIUM risk: `FMODUnity.RuntimeManager.IsInitialized` may be `false` during
`TDELifecycleAdapter.Start()` if FMOD initializes asynchronously on first scene load.

**Mitigation defined in ADR-D1:** Add RuntimeManager to Script Execution Order before
TDELifecycleAdapter, or use `WaitUntil(RuntimeManager.IsInitialized)` coroutine.

**Status:** Pending Unity Editor runtime verification. Non-blocking for ADR acceptance.

---

## W4 — F2 GDD Body Text Stale "AudioSource" References (RESOLVED — Rev.2)

Rev.1 fixed F2's dependency TABLE (W1) but missed 4 stale "AudioSource" references in
F2 GDD body text (dependency detail section, lines 444–468):

| Location | Old Text | New Text |
|----------|----------|----------|
| Line 444 | BGM AudioSource 引用和 DSP 起始时间 | BGM FMOD asset 引用和 DSP 起始时间 |
| Line 446 | BGM AudioSource 引用 | BGM FMOD asset 引用 |
| Lines 453-456 | AudioSource / AudioSettings references | FMOD / IAudioPlayer equivalents |
| Line 468 | BGM AudioSource | BGM FMOD asset |

**Status:** All 4 edits applied to `design/gdd/audio-beat-clock.md`.

Note: F2 edge cases section (lines 392, 398, 408) also contains legacy "AudioSource"
language — these describe failure scenarios and should be updated in a future F2 GDD
revision pass, but are not blocking for architecture review.

---

## W5 — F4 GDD LevelConfigSO.bgmClip Stale Type (RESOLVED — Rev.2)

F4 GDD defined `LevelConfigSO.bgmClip` as `AssetReferenceT<AudioClip>`, which is
incompatible with ADR-D6's decision to use FMOD Core API for BGM playback.

| Location | Old | New |
|----------|-----|-----|
| Line 240 (field definition) | `bgmClip : AssetReferenceT<AudioClip>` | `bgmAssetRef : string`（FMOD StreamingAssets 路径） |
| Line 49 (dependency table) | BGM AudioClip 引用 | BGM FMOD asset 引用 |
| Line 79 (Rule 6 Addressables) | `LevelConfigSO.bgmClip` as `AssetReferenceT<T>` | `LevelConfigSO.bgmAssetRef` as `string`（FMOD 路径，非 Addressables） |
| Line 571 (open question) | `bgmClip` as `AssetReferenceT<T>` | `bgmAssetRef` as FMOD 资产路径 |

**Status:** All 4 references updated in `design/gdd/so-data-layer.md`.

---

## Cross-ADR Conflict Detection

| Pair | Result |
|------|--------|
| D1 vs D6 | No conflicts — clean separation (D1 controls lifecycle, D6 defines audio layer) |
| D1 vs D7 | No conflicts — independent domains |
| D6 vs D7 | No conflicts — independent domains |

### ADR Dependency Order (topologically sorted)

```
Foundation (no dependencies):
  1. ADR-D6: Audio Middleware — FMOD Studio 2.x         [Accepted]
  2. ADR-D7: Save/Persistence Middleware — EasySave 3   [Accepted]

Depends on Foundation:
  3. ADR-D1: TDE Integration Architecture               [Accepted ✅]

Feature layer (not yet created):
  4. ADR-D5: Animation Pipeline                         (requires D1)
  5. ADR-D2: Enemy AI Framework                         (requires D1)
```

No cycles. No unresolved dependencies.

---

## Engine Compatibility

| Check | Result |
|-------|--------|
| Version consistency (D1/D6/D7) | All reference Unity 6.3 LTS (6000.3.19f1) ✅ |
| `[SerializeField]` fields-only (6.3 breaking) | D1 code uses fields only — compliant ✅ |
| Deprecated API references | None — uses new Input System, not legacy `Input.*` ✅ |
| Post-cutoff API verification | TDE 4.5 APIs verified by M0 Spike (2026-07-16) ✅ |
| `SceneManager.sceneLoaded` | Valid in Unity 6.3 LTS ✅ |
| FMOD for Unity 2.02.35 | Imported; Editor compilation pending ⚠️ |

**No engine compatibility issues found.**

---

## GDD Revision Flags

W1, W4, W5 — all resolved. No remaining GDD assumptions conflict with verified
engine behaviour or accepted ADRs.

---

## Verdict: **PASS**

ADR-D1 has been accepted. All issues from Rev.1 (B1, W1, W2) remain resolved.
Rev.2 found and fixed 2 additional cross-document stale references (W4, W5).

- Decision is well-reasoned with 3 alternatives considered and rejected
- All 3 HIGH risks resolved by M0 Spike with source-level evidence
- Interface contracts are clearly defined with code examples
- 8 validation criteria are testable
- No cross-ADR conflicts
- Engine compatibility verified
- All GDD references now consistent with ADR-D6 (FMOD) decision

### Remaining Items (non-blocking)

- W3: FMOD init timing — verify in Unity Editor during F6 implementation
- FMOD native plugin compilation — confirm in Unity Editor
- F2 edge cases section (lines 392, 398, 408) — minor "AudioSource" language cleanup

### Recommended Next Steps

1. ~~Accept ADR-D1~~ — **Done** (Status: Accepted)
2. **Author F6 GDD** — ADR-D1 provides the component structure and interface contracts
3. **Create ADR-D5** (Animation Pipeline) and **ADR-D2** (Enemy AI) — unblocked by D1

---

## Files Modified by This Review

| File | Change | Rev |
|------|--------|-----|
| `docs/architecture/tr-registry.yaml` | Added TR-datalayer-012, TR-input-015 | 1 |
| `docs/architecture/adr-d1-tde-integration.md` | Fixed 3 TR-ID refs; "AudioSource"→"FMOD asset"; Status→Accepted | 1+2 |
| `design/gdd/audio-beat-clock.md` | Dependency table + body text: "AudioSource"→"FMOD asset" (5 edits) | 1+2 |
| `docs/engine-reference/unity/plugins/top-down-engine.md` | ADR references: D3→D1, D4→D5 | 1 |
| `design/gdd/so-data-layer.md` | `bgmClip`→`bgmAssetRef`, type `AssetReferenceT<AudioClip>`→`string` (4 edits) | 2 |
| `docs/architecture/architecture-review-2026-07-16.md` | This report (Rev.2) | 2 |
