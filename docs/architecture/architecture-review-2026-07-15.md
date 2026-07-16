# Architecture Review Report

> **Date**: 2026-07-15 (Rev.2 — full review with engine specialist)
> **Engine**: Unity 6.3 LTS (6000.3.19f1)
> **Verdict**: **CONCERNS**
> **Reviewer**: Architecture Review (full)
> **GDDs Reviewed**: 5 (F1, F2, F3, F4, F5)
> **ADRs Reviewed**: 3 (D1 Proposed, D6 Accepted, D7 Accepted)

---

## Summary

69 technical requirements extracted across 5 systems. 3 ADRs cover Foundation-layer
middleware decisions (audio, save, TDE integration). Coverage at 20.3% — expected for
early pre-production with only 3 ADRs. No cross-ADR conflicts. Engine specialist
confirmed architectural soundness with 11 implementation-level findings (4 HIGH).

---

## Traceability Summary

| Metric | Count | % |
|--------|-------|---|
| Total Requirements | 69 | 100% |
| Covered | 14 | 20.3% |
| Partial | 6 | 8.7% |
| Gap | 49 | 71.0% |

**Delta from previous review (v1)**: +2 Covered, +1 Partial, -3 Gap (ADR-D1 added)

---

## Traceability Matrix

### concept (14 TRs)

| TR-ID | Requirement | ADR | Status |
|-------|-------------|-----|--------|
| TR-concept-001 | DSP clock precision < 16.7ms | ADR-D6 | Covered |
| TR-concept-002 | Multi-platform input support | — | Gap |
| TR-concept-003 | 60fps target framerate | — | Gap |
| TR-concept-004 | Animancer BPM-adaptive animation speed | — | Gap |
| TR-concept-005 | FMOD as audio middleware | ADR-D6 | Covered |
| TR-concept-006 | Half-beat grid snap quantization | — | Gap |
| TR-concept-007 | Rating system (P/G/M) multipliers | — | Gap |
| TR-concept-008 | Perfect kinetics effects | — | Gap |
| TR-concept-009 | Enemy beat-lock attacks | ADR-D1 | Partial |
| TR-concept-010 | Perk containment constraints | — | Gap |
| TR-concept-011 | Delay calibration system | — | Gap |
| TR-concept-012 | ScriptableObject data-driven design | — | Gap |
| TR-concept-013 | Roguelike perk cloning lifecycle | — | Gap |
| TR-concept-014 | Multiple weapon types ComboGraph | — | Gap |

### beatclock (17 TRs)

| TR-ID | Requirement | ADR | Status |
|-------|-------------|-----|--------|
| TR-beatclock-001 | MonoBehaviour singleton DontDestroyOnLoad | — | Gap |
| TR-beatclock-002 | DSP clock < 0.1ms accuracy | ADR-D6 | Covered |
| TR-beatclock-003 | Full grid broadcast every beat/half-beat | — | Gap |
| TR-beatclock-004 | Dual interface: events + snapshot | — | Gap |
| TR-beatclock-005 | Lookahead events configurable | — | Gap |
| TR-beatclock-006 | BPM/time-sig change deferred to measure | — | Gap |
| TR-beatclock-007 | Latency offset in all timestamps | — | Gap |
| TR-beatclock-008 | Pause/resume DSP reanchor | ADR-D1 | Covered |
| TR-beatclock-009 | BPM=0 fallback + audio unavailable | — | Gap |
| TR-beatclock-010 | Thread safety: main thread dispatch | ADR-D6 | Covered |
| TR-beatclock-011 | IDSPClock/IAudioPlayer DI interfaces | ADR-D6 | Covered |
| TR-beatclock-012 | Subscriber lifecycle OnDestroy contract | ADR-D1 | Partial |
| TR-beatclock-013 | F6 three-step DSP start protocol | ADR-D6+D1 | Partial |
| TR-beatclock-014 | 5/8 time signature (Post-MVP) | — | Gap |
| TR-beatclock-015 | BeatStateSnapshot readonly struct | — | Gap |
| TR-beatclock-016 | BeatEventData zero GC allocation | — | Gap |
| TR-beatclock-017 | Mobile suspend/resume DSP reanchor | — | Gap |

### input (14 TRs)

| TR-ID | Requirement | ADR | Status |
|-------|-------------|-----|--------|
| TR-input-001 | Unity Input System Package only | — | Gap |
| TR-input-002 | Dual-read strategy DSP + ctx.time | — | Gap |
| TR-input-003 | InputEventBus singleton DontDestroyOnLoad | — | Gap |
| TR-input-004 | Two Action Maps (Gameplay + UI) | ADR-D1 | Partial |
| TR-input-005 | AttackInputEvent chargeStartDspTime | — | Gap |
| TR-input-006 | Zero GC allocation readonly struct events | — | Gap |
| TR-input-007 | Multi-device concurrent support | — | Gap |
| TR-input-008 | Runtime key rebinding JSON persistence | — | Gap |
| TR-input-009 | Subscriber lifecycle unsubscribe | — | Gap |
| TR-input-010 | TDE Action Map conflict resolution | ADR-D1 | Covered |
| TR-input-011 | ChargedAttack Hold state machine | — | Gap |
| TR-input-012 | IDSPClock shared interface with F2 | ADR-D6 | Covered |
| TR-input-013 | Platform-specific timing constraints | — | Gap |
| TR-input-014 | AllDisabled initial state on Awake | ADR-D1 | Covered |

### datalayer (11 TRs)

| TR-ID | Requirement | ADR | Status |
|-------|-------------|-----|--------|
| TR-datalayer-001 | 7 core SO types | — | Gap |
| TR-datalayer-002 | Read-only asset principle | — | Gap |
| TR-datalayer-003 | PerkSO runtime cloning | — | Gap |
| TR-datalayer-004 | Schema change propagation | — | Gap |
| TR-datalayer-005 | Odin dual validation | — | Gap |
| TR-datalayer-006 | AssetReferenceT lazy loading | — | Gap |
| TR-datalayer-007 | PerkParameter deep copy on Instantiate | — | Gap |
| TR-datalayer-008 | ComboEdge index validation | — | Gap |
| TR-datalayer-009 | PlayerInfoSO <> F5 DTO field mapping | ADR-D7 | Partial |
| TR-datalayer-010 | [NonSerialized] init timing (wait F5) | ADR-D1 | Covered |
| TR-datalayer-011 | SO id uniqueness enforcement | — | Gap |

### save (13 TRs)

| TR-ID | Requirement | ADR | Status |
|-------|-------------|-----|--------|
| TR-save-001 | SaveService singleton gateway | — | Gap |
| TR-save-002 | Data domain separation (Slot vs Global) | — | Gap |
| TR-save-003 | 3-slot system management | — | Gap |
| TR-save-004 | Mixed save trigger (auto + immediate) | — | Gap |
| TR-save-005 | DTO pattern (SO->DTO->disk) | ADR-D7 | Covered |
| TR-save-006 | Main thread constraint | ADR-D7 | Partial |
| TR-save-007 | Per-DTO version migration | ADR-D7 | Covered |
| TR-save-008 | IFileStorage abstraction | ADR-D7 | Covered |
| TR-save-009 | Domain Reload static reset | — | Gap |
| TR-save-010 | Awake/Initialize separation for test DI | — | Gap |
| TR-save-011 | OnSaveError event contract | — | Gap |
| TR-save-012 | Two-level error model | — | Gap |
| TR-save-013 | Corrupted file .corrupt backup | ADR-D7 | Covered |

---

## Partial Coverage Details

| TR-ID | Reason | Full Coverage Requires |
|-------|--------|----------------------|
| TR-concept-009 | D1 mentions enemy beat-lock but defers to ADR-D2 | ADR-D2 (Enemy AI Framework) |
| TR-beatclock-012 | D1 mentions OnDestroy unsubscribe in context, not formal spec | F2 implementation-level guarantee |
| TR-beatclock-013 | D6 provides audio APIs + D1 calls Play(), but 3-step protocol defined in F2 GDD | F6 GDD implementation closes this |
| TR-input-004 | D1 resolves TDE conflict but doesn't define dual Action Map architecture itself | F3 implementation-level guarantee |
| TR-datalayer-009 | D7 covers [NonSerialized] isolation but not full SO<>DTO mapping | F5 implementation-level closure |
| TR-save-006 | D7 states main-thread constraint but doesn't architecturally enforce it | F5 Rule 13 thread ID check in SaveService |

---

## Cross-ADR Conflict Detection

**Result: 0 conflicts detected.**

Checked all pairwise combinations (D1-D6, D1-D7, D6-D7) across:
- Data ownership
- Integration contracts
- Performance budgets
- Dependency ordering
- Architecture patterns
- State management

All three ADRs operate in independent domains with clean interface boundaries.

---

## ADR Dependency Order (topologically sorted)

```
Foundation (no dependencies):
  1. ADR-D6: Audio Middleware - FMOD Studio 2.x          [Accepted]
  2. ADR-D7: Save Middleware - EasySave 3                [Accepted]

Depends on Foundation:
  3. ADR-D1: TDE Integration Architecture                [Proposed]
     (requires ADR-D6 - dependency satisfied)

Future ADRs (recommended order):
  4. ADR-D5: Animation Pipeline                          [Not Created]
     (requires ADR-D1 - TDE Animator layer boundary)
  5. ADR-D2: Enemy AI Framework                          [Not Created]
     (requires ADR-D1 - F6 lifecycle hooks)
  6. ADR-D3: Rhythm Quantization Core                    [Not Created]
     (requires ADR-D6 - DSP clock; independent of D1)
```

- No dependency cycles
- No unresolved dependencies (D1's dependency on D6 is satisfied)
- ADR-D1 remains Proposed - requires M0 Spike verification before Accept

---

## GDD Revision Flags

**None** - all GDD assumptions are consistent with verified engine behaviour.

All HIGH RISK engine findings are "pending M0 Spike verification", not "confirmed contradictions".

---

## Engine Compatibility Audit

### Version Consistency
All 3 ADRs target Unity 6.3 LTS (6000.3.19f1). Consistent.

### Deprecated API References
None found across all ADRs.

### Post-Cutoff API Audit

| ADR | API | Source | Risk |
|-----|-----|--------|------|
| D6 | FMOD.System.getDSPClock() | FMOD SDK (3rd party) | MEDIUM |
| D6 | FMOD.Channel.setDelay() | FMOD SDK (3rd party) | MEDIUM |
| D1 | MMEventListener<T> | TopDownEngine (3rd party) | HIGH |
| D1 | InputSystemManagerEventsBased | TopDownEngine (3rd party) | HIGH |
| D7 | (none) | - | LOW |

### Breaking Changes Impact

| Breaking Change | Affected ADR | Analysis |
|----------------|-------------|----------|
| [SerializeField] fields-only | D6 (FMOD), D1 (TDE) | Editor scripts may use [SerializeField] on properties -> CS0592 |
| URP Compatibility Mode removed | None | Not relevant to current ADRs |
| Physics.autoSyncTransforms deprecated | None | Not relevant |

### Engine Specialist Findings (11 items)

| # | Finding | Type | Severity | ADR | Action |
|---|---------|------|----------|-----|--------|
| ES-1 | FMOD for Unity requires v2.02+ for Unity 6. Must manually verify per-platform native lib Inspector assignments after import | New | HIGH | D6 | Add to M0 Spike checklist |
| ES-2 | [SerializeField] risk ranking: TDE > FMOD > ES3. TDE CharacterAbility subclasses most likely to use property-level [SerializeField] | Confirm | HIGH | D1 | M0 Spike compile gate |
| ES-3 | FMOD RuntimeManager uses [DefaultExecutionOrder(-100)]. But F3 GDD requires async mode -> IsInitialized may be false in Start(). Must use WaitUntil coroutine | Challenge | MEDIUM | D1 | Promote to required boot sequence implementation |
| ES-4 | SceneManager.sceneLoaded timing unchanged in Unity 6.3 - ADR assumption correct | Confirm | LOW | D1 | No action |
| ES-5 | TDE InputSystemManagerEventsBased internal methods likely non-virtual. Subclass override may fail. M0 Spike must set binary decision gate | New | HIGH | D1 | Document: virtual -> subclass; non-virtual -> HandleInput=false + manual AbilityInputAuth proxy |
| ES-6 | EasySave 3 + IL2CPP: need link.xml preserving EasySave3 assembly. [Preserve] on DTOs alone is insufficient | Confirm+Ext | HIGH | D7 | Add to F5 implementation spec |
| ES-7 | FMODAudioPlayerImpl.IsPlaying: Channel handle invalidated after stop/voice-steal. isPlaying() returns FMOD_ERR_INVALID_HANDLE. Must check _channel.isValid() first | New | MEDIUM | D6 | Fix FMODAudioPlayerImpl implementation |
| ES-8 | Android audio focus loss can reinitialize audio engine, changing FMOD sample rate. Cached Awake() sampleRate becomes stale | New | MEDIUM | D6 | Document as MVP known limitation |
| ES-9 | _fmodVerified retains stale value with Hot Reload + Domain Reload off. Must mark [NonSerialized] or reset in OnEnable() | New | MEDIUM | D1 | Fix TDELifecycleAdapter implementation |
| ES-10 | ES3.LoadRawBytes() / ES3.SaveRaw() are not public EasySave 3 API. RenameFile should use System.IO.File.Move(src, dst, overwrite: true) (.NET 6+) | Confirm | HIGH | D7 | Promote File.Move to primary RenameFile implementation |
| ES-11 | TDELifecycleAdapter._levelConfig is static Inspector reference. Respawn reads same LevelConfigSO - fails if respawn targets different BPM | New | LOW | D1 | Flag for F6 GDD design |

**Specialist Conclusion**: All 3 ADRs are architecturally sound in design intent. Issues found are
implementation-level details and API correctness, not structural design flaws. All blockers resolvable
within M0 Spike scope.

---

## Architecture Document Coverage

`docs/architecture/architecture.md` does not exist. Recommend creating an overall architecture
document in Pre-Production covering all 31 systems from systems-index.md.

---

## Verdict: CONCERNS

### Rationale

- No cross-ADR conflicts
- Engine compatibility issues all flagged with M0 Spike mitigation
- ADR dependency order has no cycles
- Coverage at 20.3% (14/69) - expected for 3 ADRs covering Foundation middleware
- ADR-D1 still Proposed (M0 Spike blocking)
- Core layer requirements (C1 rhythm quantization, C2 combo graph) have no ADR coverage yet

### Why CONCERNS, not FAIL

49 gaps are predominantly **implementation-level requirements** (singleton patterns, struct design,
GC optimization) that don't need independent ADRs - the GDDs already define specifications.
Architectural decisions requiring ADRs are concentrated in 3 future ADRs: D5, D2, D3.
Current 3 ADRs cover all Foundation-layer **architecture-level decisions** with no gaps.

---

## Blocking Issues

1. **ADR-D1 Proposed -> M0 Spike required**: TDE 4.x + Unity 6.3 compile verification,
   FMOD native lib compat verification. D1 cannot be Accepted until M0 Spike completes.
   F6 GDD authoring blocked on D1 acceptance.

---

## Required ADRs (priority order)

| Priority | ADR | Title | Blocking |
|----------|-----|-------|----------|
| 1 | ADR-D5 | Animation Pipeline | Covers TR-concept-004. Required before F6 GDD - defines TDE Animator vs Animancer attack layer boundary |
| 2 | ADR-D2 | Enemy AI Framework | Covers TR-concept-009. Required before FE1 GDD - selects behavior tree framework |
| 3 | ADR-D3 | Rhythm Quantization Core | Covers TR-concept-006/007. Can be designed alongside C1 GDD |

---

## M0 Spike Checklist (consolidated from ADR risks + engine specialist)

- [ ] Import TDE 4.x into Unity 6.3 LTS project; verify zero compile errors
- [ ] Import FMOD for Unity 2.02+ package; verify zero compile errors
- [ ] Manually check each FMOD native library Inspector platform assignment
- [ ] Read TDE `InputSystemManagerEventsBased.cs` source: identify virtual/overridable methods
- [ ] **Binary gate**: virtual methods found -> subclass approach (ADR-D1 as-is); non-virtual -> fallback to HandleInput=false + manual AbilityInputAuth proxy (update ADR-D1)
- [ ] Verify `MMEventListener<T>` API signatures (MMEventStartListening/MMEventStopListening)
- [ ] Verify TDE pause event name strings ("Pause"/"UnPause" or alternative)
- [ ] Verify TopDownEngineEventTypes.PlayerDeath and .Respawn enum members
- [ ] Import EasySave 3 v3.x; verify `ES3.Save<int>()` + `ES3.Load<int>()` roundtrip
- [ ] Verify `List<MetaUpgradeEntry>` serialization fidelity
- [ ] Verify `[NonSerialized]` fields are not serialized by EasySave 3
- [ ] Add `link.xml` preserving EasySave3 assembly for IL2CPP builds
- [ ] FMOD DSP clock 500-beat precision measurement (target: < 0.1ms deviation)
- [ ] FMOD Android actual_start measurement (target: < 5ms deviation)
- [ ] Document all findings in `docs/engine-reference/unity/plugins/`

---

## Next Actions

1. Complete M0 Spike (prerequisite for all implementation)
2. Accept ADR-D1 (after M0 Spike verification)
3. Author ADR-D5 (Animation Pipeline) - `/architecture-decision "Animation Pipeline"`
4. Author ADR-D2 (Enemy AI Framework) - `/architecture-decision "Enemy AI Framework"`
5. Author F6 GDD (TDE Integration Layer) - after D1 Accepted + D5 Accepted
6. Begin C1 GDD (Rhythm Core) - can start after M0 Spike data available
