# Architecture Review — ADR-D15 @ PR #66

> **Date**: 2026-10-22
> **Mode**: `/architecture-review adr-15 at pr#66` (single-ADR focused)
> **Engine**: Unity 6.3 LTS (6000.3.19f1)
> **Branch / Head**: `review/adr-d15-arch-review` @ `56f7b123`
> **Scope**: ADR-D15 + 13-ADR cross-conflict sweep + 33 in-scope TRs + M0 Locked criteria + vertical-slice evidence
> **Method**: two rounds of multi-agent fan-out (123 raw findings → adversarial verification) + inline follow-up
> **Verdict**: **FAIL** — 7 blocking items (see § 2). Read as "cannot be Accepted as-is", not "the ADR is poor".
> **Session independence**: run in a fresh session, independent of the `/architecture-decision` authoring context, as PR #66 requires.

---

## 0. Verification-layer disclosure (read this first)

| Status | Count | Meaning |
|---|---|---|
| **CONFIRMED** | **77** | survived adversarial verification by 2–3 independent lenses |
| **REFUTED** | **20** | killed by verifiers (the `engine-compat` dimension lost 5 of its 6 — almost pure noise) |
| **STILL-UNVERIFIED** | **26** | **verification layer destroyed by the monthly spend limit — NOT corroborated** |

Two things must be stated plainly:

1. **Round 1's reported "89 refuted" was a counter artifact, not data.** The original survival test was
   `survives = votes.length > 0 && refutes < 2`, so a finding whose verifiers *all errored* (zero valid
   votes) was silently bucketed as "refuted". After re-triage the real distribution is the table above.
2. **The 26 unverified findings are**: all 17 of `gdd-diff-audit`, all 6 of `slice-evidence-integrity`,
   and `registry-tr-consistency` #10/#11/#12. **They are recorded below as unverified claims and are
   excluded from the verdict.**

**Systematic severity recalibration.** Every finding claimed as `Critical` was downgraded to `High` by the
verifiers, with the same rationale recurring across three independent lenses: ADR-D15 is `Status: Proposed`;
production C1 is not implemented (`adr-d15:33`, `:52`); all three M0 sign-offs are "⏳ 待签" (`m0:150-152`);
and stories referencing a `Proposed` ADR are auto-blocked (`docs/CLAUDE.md:13`). The blast radius is therefore
documentation and process governance, caught pre-implementation. **Exactly one substantive Critical survives**
(B-1 below), upheld 3/3.

**Not run.** Phase 5's engine audit was not redone and the skill-mandated `unity-specialist` consultation was
not performed — the monthly spend limit was reached twice and no further agents were spawned. Declared as
not-executed, not as clean.

---

## 1. PR-7 ruling (the item PR #66 formally handed over)

PR #66 deferred the `RhythmJudgmentEvent` field-list contradiction to this review with a prescribed method:
"先确认每个字段至少有一个实际消费方，再以并集或最小集定稿，两处同步".

### 1.1 Ruling

**PR-7's premise does not hold. The contract was already adjudicated on 2026-08-13; ADR-D15 never saw the
existing ruling.**

`design/gdd/rhythm-core.md` §C1-F6 (`:575-615`) declares itself normative:

> `:581` **本节为唯一规范性定义**；其他文档（ADR-D3、Rule 10、Interactions 表）以本节字段列表为准

`:607` records the sync as resolved on **2026-08-13**: "旧 ADR-D3 字段 `BeatDspTime` / `Mode` 已从事件面移除
（降级为 RhythmJudger 内部状态）". `adr-d3:192` explicitly cedes authority to C1-F6, `:193-201` mirrors the six
fields verbatim, and `:204` declares `QuantizationMode` "非事件字段". `:609` fixes the canonical event name as
`OnRhythmJudgment`.

`git diff 31545105..HEAD` confirms C1-F6 and the `adr-d3:192` deferral annotation were **not** introduced by
PR #66 — they are anchored at 2026-08-13. Meanwhile `grep -n "C1-F6" adr-d15` returns **empty**: ADR-D15 never
mentions C1-F6 anywhere in 744 lines.

### 1.2 Final field list — the **minimal set**, which is C1-F6's existing six fields

```csharp
public readonly struct RhythmJudgmentEvent
{
    public RhythmRating  Rating;           // Perfect / Good / Miss
    public AttackType    AttackType;       // LightAttack / HeavyAttack / ChargedAttack (passthrough from F3)
    public CancelTier    CancelTier;       // Advanced / Standard / Closed (filled by C1 per rating, Rule 10)
    public int           ComboCountBefore; // combo count before this judgment
    public int           ComboCountAfter;  // combo count after this judgment (= Before + ΔCombo, C1-F4)
    public float         OffsetMs;         // beat_offset_ms (signed, float); fixed 0 in Off mode
}
```

Broadcast as `event Action<RhythmJudgmentEvent> OnRhythmJudgment`. Normative source: **`design/gdd/rhythm-core.md`
§C1-F6**. Every other document mirrors it.

### 1.3 Per-field consumer evidence (PR-7's required check)

| Field | Per-judgment consumer (file:line) |
|---|---|
| `Rating` | `combo-system.md:39`; `docs/registry/architecture.yaml:173`; Feel/MMFeedbacks rating-feedback sequences |
| `AttackType` | `combo-system.md:39` (`requiredInput` edge matching); `perk-system.md:224` |
| `CancelTier` | `combo-system.md:45-52` rule 5 cancel gating; `:186` EC6; `:190` EC10; AC-C2-10/11/12/35 |
| `ComboCountBefore` | `rhythm-core.md:600` (P1 combo-increment animation); `registry:173`; `perk-system.md:224` |
| `ComboCountAfter` | `combo-system.md:60` rule 9 `combo_multiplier`; `:102`; AC-C2-30 |
| `OffsetMs` | `rhythm-core.md:605` (P1 HUD offset indicator); `accessibility-requirements.md:87` early/late direction cue |

### 1.4 Why `BeatDspTime` and `Mode` stay off the event face

- **`BeatDspTime` — zero consumers repo-wide.** A full sweep of `BeatDspTime|beatDspTime|beat_dsp_time` across
  `design/gdd`, `docs/architecture`, `docs/registry` and `Assets/_VerticalSlice` returns only three classes of
  hit: (a) F2's upstream `BeatEventData.BeatDspTime` — a *different* struct; (b) `_currentBeatDspTime`, which is
  RhythmJudger's own private state (`adr-d3:101`, `:108`, `:142`, `:149`); (c) the argument of
  `AnimancerAttackPlayer.Play(attackType, beatDspTime)`, which C1 passes from that private state
  (`rhythm-core.md:191`, `:869`). **No subscriber of `RhythmJudgmentEvent` reads such a field.**
- **`Mode` — real consumption need, but a non-event path already exists.** `rhythm-core.md:575` does require
  downstream damage (C2/C6) to "读取激活 `QuantizationMode` 并在 Off 下应用 `min(·, 1.0)`". But
  `architecture.md:135` already lists `QuantizationMode Mode` in RhythmJudger's **Exposes** column — a read-only
  property, not an event field. The need is met without widening the payload.
- **`CancelTier` must be on the face** because it is not derivable from `Rating` alone: the Off-mode exception
  ("恒 Perfect 但 CancelTier 封顶 Standard", C1 Rule 10 / `combo-system.md:62`) makes it depend on `Mode` too.
  That asymmetry is precisely why `CancelTier` is on the payload and `Mode` need not be.
- **`ComboCountAfter` must be on the face** because SPEC-2's Miss semantics (`After == Before`, no reset) are
  only observable from the event itself when both values are broadcast — `adr-d15:734` makes this point itself,
  and AC-C1-28 asserts `ΔCombo == 0`.
- **Implementation is already aligned**: `Assets/_VerticalSlice/MiaodaoRhythmSlice/Runtime/RhythmTypes.cs:21-31`
  carries `CancelTier` / `ComboBefore` / `ComboAfter` and has no `BeatDspTime` / `Mode`.

### 1.5 Sync action list — six stale sites, four of which PR-7 did not register

| File:line | Current state | Registered by PR-7? |
|---|---|---|
| `docs/registry/architecture.yaml:177` | stale 6 fields with `BeatDspTime`/`Mode`, missing `CancelTier`/`ComboCountAfter` | ✅ — but `adr-d15:731` cites `:176`, off by one (`:176` is the `adr:` line) |
| `docs/architecture/tr-registry.yaml:842` TR-rhythm-012 | **already matches C1-F6 — no change needed** | ✅ registered as a contradiction party; it is in fact the correct party |
| `docs/architecture/architecture.md:135` | Engine-APIs column still lists the stale 6 fields | ❌ |
| `docs/architecture/architecture.md:193` | data-flow diagram shows `{Rating,OffsetMs,ComboCountBefore}` (3 fields) | ❌ |
| `docs/architecture/adr-d3-...:58` | Requirements section still lists `beatDspTime` as a required event field, contradicting the 6-field mirror at `:193-201` in the same file | ❌ |
| `design/gdd/rhythm-core.md:936` | "C1 须在 `RhythmJudgmentEvent` 中提供 P1 所需的全部数据：`Rating`, `OffsetMs`, `ComboCountAfter`" — **omits `ComboCountBefore`, contradicting `:600`'s own "供 P1 显示连击增量动画"** | ❌ |

**Additional finding (inline).** `adr-d3:204` describes `Mode` as "RhythmJudger.Mode **内部状态**用", while
`architecture.md:135` places `QuantizationMode Mode` in the **Exposes** column and `rhythm-core.md:575` requires
downstream code to read it. Unify all three to: "非事件字段，经 `RhythmJudger.Mode` **公开只读属性**暴露".

---

## 2. Blocking issues (must resolve before PASS)

### B-1 — The only surviving Critical: `BPM_max` omits the Perfect-knob branch

From cascade step ②, `PerfectEff = min(max(min(A+δ, C), m), C)`, hence
`MissUnreachable ⟺ C ≤ max(A+δ, m)`, where `A = PerfectHalfWindowMs_authored`,
`δ = perkPerfectDelta`, `m = platformMinPerfectMs`, `C = gridIntervalMs/2 − 1`.

`adr-d15:362-364` encodes only `C < m`:

```
BPM_max = (mode==Beat ? 60000 : 30000) / (2 × (platformMinPerfectMs + 1))
```

That is the **PerfectFloorSacrificed** threshold, not the `MissUnreachable` threshold. Real values
(HalfBeat, `C = 15000/BPM − 1`):

| Case | True BPM_max |
|---|---|
| PC / iOS, default A=50 | **294.12** |
| `perkPerfectDelta = +20` | **211.27** (inside the registered `[80, 240]` content range) |
| Perfect knob at range top, A=100 | **148.51** |
| perk full stack +60 (A'=110) | **135.14**; Android identical at **135.14**, *not* 185.19 |

Separate arithmetic error: `30000 / (2 × 17.7) = 847.46`, not `848.0` — appears at `adr-d15:364`,
`rhythm-core.md:372`, `rhythm-core.md:757`.

Knock-on failures:
- `adr-d15:683` AC-C1-31's equivalence `MissUnreachable ⟺ BPM > BPM_max` is false in the ⟹ direction and wrong
  at the boundary (should be ≥). The **landed GDD form is correct** (`rhythm-core.md:1026` states
  `⟺ MissHeadroomMs_effective <= 0`) — only the ADR's BPM-phrased restatement is wrong.
- `adr-d15:358`'s Odin Validator cartesian product and the landed AC-C1-30 (`rhythm-core.md:1019-1020`) both omit
  the `PerfectHalfWindowMs_authored` and `perkPerfectDelta` dimensions, so the grid cannot detect the failure.

**Verifier correction worth preserving.** `rhythm-core.md:752-757` is **not** a defect site: E7's condition at
`:754` is explicitly `platformMinPerfectMs > C`, i.e. PerfectFloorSacrificed, for which the formula *is* correct,
and `:757` "PC 永不触发" is correct within that scope. Only `:787` plus the `848.0 → 847.46` fix are needed there.

**Resolution.**
```
BPM_max_MissUnreachable = (mode==Beat ? 60000 : 30000)
                        / (2 × (max(PerfectHalfWindowMs_authored + perkPerfectDelta, platformMinPerfectMs) + 1))
```
State the equivalence as `⟺ BPM ≥ BPM_max`; give the two thresholds distinct names
(`BPM_max_MissUnreachable` vs `BPM_max_PerfectFloor`); add the two missing dimensions to the Odin Validator grid
and to AC-C1-30/31.

### B-2 — ADR-D3 ⇄ ADR-D15 authority inversion (blocking cross-ADR conflict)

`adr-d3:4`'s Status is now a **mixed value outside the template's three-value enum**:

> `Accepted (2026-07-19) · **Amended 2026-10-22 by ADR-D15**（SPEC-1 双重延迟补偿、SPEC-2 统一钳位级联与三新旋钮、
> SPEC-4 静默吞输入登记、LateWindow 过期判定改 DSP 域。… 该 ADR 当前 `Proposed`——本文件的修订段落随其
> Accepted 后方可据以实现。）`

Dependency topology (from every ADR's `Depends On` field):

| Layer | ADRs |
|---|---|
| L0 (Foundation, no deps) | D6, D7 |
| L1 | D1 |
| L2 | D5, D2, D10 |
| L3 | **D3**, D4, D12 |
| L4 | D8, D13 |
| L5 (Feature) | D14, **D15** |

No literal cycle exists, and D15's three declared dependencies (D3, D8, D4) are all `Accepted`. But D3's
Decision / Evaluation Algorithm body has been rewritten to the corrected algorithm whose authority is the
`Proposed` D15 — so an **L3 `Accepted` ADR now textually depends on an L5 `Proposed` ADR**
(D15 → D3 → authority-of → D15).

Two consequences:
1. **No document in the chain currently holds a self-consistent, approved algorithm.** D3's body is the corrected
   version but unapproved; the pre-D15 version survives nowhere authoritative.
2. The Status field that story auto-blocking keys on (`docs/CLAUDE.md:13`, exact enum match) is **broken**, so the
   auto-block safety net silently stops protecting D3-referencing stories.

### B-3 — `control-manifest.md` received zero propagation; it is the 9th and most dangerous SPEC-1 site

The file is **not in PR #66's 8-file diff** (`git diff --name-only 31545105..HEAD | grep -c control-manifest` → 0),
and ADR-D15 never mentions it (grep for `control-manifest` / `Manifest` in D15 → empty). Yet as the project's
"flat programmer rules sheet" it still mandates the SPEC-1 double compensation, in two rules four lines apart:

- `:178` — "**`BeatEventData.BeatDspTime` already includes the latency offset ("player-perceived time domain") —
  downstream must use it for timing judgment; never read `AudioSettings.dspTime`**" — source: ADR-D8
- `:182` — "**C1 must add F3's raw `inputDspTime` back to `latency_offset_s` before comparing to `beat_dsp_time`**
  (F2 Core Rule 7)" — source: ADR-D3

`:189` additionally carries the pre-SPEC-2 clamp model ("`Start()` asserts `GoodHalfWindowMs < halfBeatDurationMs / 2`
and `PerfectHalfWindowMs < GoodHalfWindowMs`; Android clamps `PerfectHalfWindowMs ≥ 80ms`"). A grep for
`D15|MissBudgetShare|MissHeadroom|EarlyLenience|ClampDegradation` across the file returns **zero hits**.

The operational danger is the version stamp: `:4-5` read `Last Updated: 2026-08-12` / `Manifest Version: 2026-08-12`,
and `:9-12` explains that story files embed that version while `/story-done` checks it for staleness.
**Any C1 story written against the current manifest version will carry the known-wrong formula into implementation.**

`docs/architecture/architecture.md` (`:8` `Last Updated: 2026-08-12`) likewise has zero D15 propagation.

This independently corroborates round-1 CONFIRMED finding `repo-wide-grep #1` (claimed Critical, effective High)
by a second path, and adds the `:189` clamp-model angle and the story-version-embedding angle.

### B-4 — AC-C1-29 deadlocks against CI policy

`adr-d15:666` (Migration Plan step 6) requires: "C1 实装 sprint 前，先写 AC-C1-27~31 对应的 EditMode 单测
（遵「先写测试」规范）。**AC-C1-29 预期失败**，直至 SPEC-4 修复。"

But `.github/workflows/tests.yml` runs editmode then playmode via `game-ci/unity-test-runner@v4` on both
`push: branches:[main]` and `pull_request: branches:[main]`, with **no `continue-on-error` anywhere**; and
`.claude/docs/coding-standards.md` states "Never disable or skip failing tests to make CI pass — fix the underlying
issue" plus "No merge if tests fail".

Together: a deliberately-failing test permanently reds main, and skipping it violates the standard.
**ADR-D15 never addresses this institutional deadlock.**

Resolution options: (a) `[Ignore("Blocked by SPEC-4, ADR-D15")]` with a stated expiry and a tracking item;
(b) fold the SPEC-4 fix into the same sprint so AC-C1-29 never enters main red; (c) land it as an
explicitly-quarantined advisory test outside the blocking suite.

### B-5 — TR-beatclock-010 directly contradicts ADR-D6

`tr-registry.yaml:228-234`, TR-beatclock-010 (`status: active`, `created: 2026-07-15`, `revised: ""`):

> `requirement: "Thread safety: audio thread events queued to main thread via ConcurrentQueue"`

Versus `adr-d6:230`:

> "`ConcurrentQueue<BeatEventData>` is **NOT required and must not be introduced** under this architecture"

Corroborated by `control-manifest.md:135` ("Never use Unity Native Audio + OnAudioFilterRead scheduling —
`ConcurrentQueue` adds ~1 frame latency — source: ADR-D6") and `adr-d6:80` (`// main thread, no queue`).
Any story citing TR-beatclock-010 is steered toward an expressly forbidden implementation.

*(Correction of record: I earlier attributed this conflict to ADR-D8. Verified against `adr-d6:139-150` and
`:224-238` — the counterparty is ADR-D6.)*

### B-6 — M0 Locked blast radius is understated

`adr-d15:34`'s Ordering Note claims only: "修正 4（早侧对称化）逐字改动 `m0-rhythm-feel-acceptance.md` §2.2 已
**Locked** 的不变量复刻（`:51-52`），**须先走 M0 修订流程**（owner: producer）。修正 1/2/3/6 不触及 Locked 内容，可先行。"

In fact **修正 2 falsifies six further Locked sites**, and the note's own `:51-52` target is the wrong one:

| M0 site | What 修正 2 falsifies |
|---|---|
| `:48` | the old Good line `min(GoodHalfWindowMs_authored=120, gridIntervalMs/2 − 1)` — missing the `− MissHeadroomMs_effective` term entirely |
| `:49` | the companion formulas |
| `:40` | HalfBeat row "250ms \| ±50ms \| **±120ms** \| 120ms \| 100ms" |
| `:56` | the "BPM ≥ 124" critical-BPM bullet |
| `:154` | footer `<!-- M0-ACCEPTANCE: … Windows@120BPM: Perfect±50ms/Good±120ms …` |
| `:45` | stale authority anchor "权威 `rhythm-core.md:277-293`" — the cascade moved to `:301-357` |

**Verifier correction worth preserving.** The *invariants* at `:51-52` still hold under the new cascade. What 修正 2
falsifies verbatim is the list above. So this is an **incomplete blast radius**, not a false statement about
`:51-52`. Note also that ADR C-e (`:50`) is worded to gate only "任何改动不变量的修正", giving `:34` a textual
(if unconvincing) escape hatch that the original finding did not address.

The structurally certain statement — as opposed to the ADR's illustration — is:

> `Good_eff @ 120BPM / HalfBeat / PC = 124 − MissHeadroomMs_effective`

which is strictly below the Locked 120 for any `MissHeadroomMs_effective > 4`, while cascade step ③ requires it
`> 0` for P3 (Miss reachability). **The "94ms effective Good half-window" figure circulating from this ADR is the
ADR's own numeric example (`:337` "数值示例（`MissBudgetShare = 0.5`）"), not a decided balance value —
`:718-719` GD-1 explicitly defers the authoritative numbers to the balance pass. Do not quote 94ms as a conclusion.**

Supporting fact: `Assets/_VerticalSlice/MiaodaoRhythmSlice/Runtime/RhythmJudge.cs:35` sets
`_goodAuthoredMs = 100f`, written by the pre-PR commit `31545105`. **The slice already deviated from the Locked
±120 before PR #66**; the cascade replacement is the second such deviation. All three M0 sign-offs remain
"⏳ 待签" (`m0:150-152`), which is what keeps this at High rather than Critical.

### B-7 — PR-7 ruling not yet landed

Section 1.5's six stale sites remain unsynchronized. Until they are, two mutually exclusive "authoritative"
field lists are live in files that story authors cite directly.

---

## 3. Other CONFIRMED findings (non-blocking, still require fixes)

| # | Finding | Evidence |
|---|---|---|
| 3.1 | **8th SPEC-1 site**: Core Rule 2 prose still carries the cancelled double compensation — `偏移量 = (输入 DSP 时间 + 延迟校准偏移 − 拍点 DSP 时间) × 1000`. The C1-F2 body (`:230`) was fixed; the prose was not. | `rhythm-core.md:70` |
| 3.2 | **修正 4's C-2 signed comparison never landed**: still absolute-value based. | `rhythm-core.md:395-398` (`absOffset = abs(...)`); `adr-d3:174` (`if absOffset <= …`) |
| 3.3 | **Four `status: active` TRs now encode a `Proposed` spec.** `/story-readiness` only validates "TR-ID exists and is active", so they pass readiness while resting on an unapproved ADR. | TR-rhythm-005 / -006 / -013 / -017, all `revised: 2026-10-22` |
| 3.4 | **`adr-d10:27` self-contradiction**: "Does not block ADR-D8 (Combat Director) or ADR-D9 (Combo Graph)" — but `adr-d8:53` lists D10 as `Depends On`; D8 is "DI & Test Seams", not Combat Director; and **ADR-D9 does not exist**. | `adr-d10:27` vs `adr-d8:53` |
| 3.5 | **Undocumented ADR numbering gap**: 13 ADRs exist (D1–D8, D10, D12, D13, D14, D15). **D9 and D11 do not exist and the gap is nowhere explained.** | `ls docs/architecture/adr-*` |
| 3.6 | **`adr-d15:697` records ADR-D13 as `Proposed`** — it is `Accepted` (TD + game-designer co-signed). | `adr-d15:697` vs `adr-d13:4` |
| 3.7 | **TR-rhythm-018's text is invalidated**: C1 no longer consumes the latency value (`rhythm-core.md:253`), yet `rhythm-core.md:853` still lists `LatencyOffsetMs` as C1-consumed F2 data, contradicting `:193` and `:253`. | TR-rhythm-018; `rhythm-core.md:853` |
| 3.8 | **TR-rhythm-006 overcounts**: claims "three unsynchronized copies existed"; D15 documents only two (`:128`), and `rhythm-core.md:466-470` shows the state-machine copy was already eliminated (it calls `ApplyClampCascade(newBpm, _quantizationMode)`). | TR-rhythm-006 |
| 3.9 | **`traceability-index.md` was stale**: rhythm recorded 19/17/2/0; registry total recorded as 153 entries when the file actually holds **154** (the `save` row omits TR-save-014); `Last Updated: 2026-08-13`. | fixed by this review — see § 7 |

### Unverified claims (26) — recorded, not concluded

- `gdd-diff-audit` (17 claims) — line-by-line audit of PR #66's +357 lines in `rhythm-core.md`. Entire verification
  layer lost to the spend limit.
- `slice-evidence-integrity` (6 claims) — whether `Assets/_VerticalSlice/MiaodaoRhythmSlice/` matches the landed
  cascade. Entire verification layer lost.
- `registry-tr-consistency` #10/#11/#12 — three registry-consistency claims whose verifiers errored.

These are the first thing to re-run when agent budget is available again.

---

## 4. Traceability summary

In-scope requirements for this review: **33 TRs** (rhythm + the beatclock/registry items D15 touches).

| Status | Count |
|---|---|
| ✅ Covered | 17 |
| ⚠️ Partial | 12 |
| ❌ Gap | 4 |

| Gating fact | Count |
|---|---|
| In-scope TRs covered **only** by the `Proposed` ADR-D15 | **8** — TR-rhythm-005 / -006 / -008 / -013 / -014 / -017, TR-beatclock-005 / -007 |
| New D15 specs that had **no TR ID at all** | **10** — assigned TR-rhythm-020 … -029 by this review (§ 7) |
| **Total requirements gated by ADR-D15's single `Proposed` status** | **18** |

Next free sequence numbers after this review: rhythm **030**, combo **016**, beatclock **018**.

### Coverage gaps (no ADR exists) — 4

These are pre-existing and unrelated to D15; all sit in GDD-is-the-spec territory (F2/F3 internal state-machine
and scheduler logic), consistent with the standing note in `traceability-index.md` that application-layer logic is
specified by GDD rather than by a dedicated ADR. No new ADR is recommended for them in this review.

---

## 5. Cross-ADR conflicts

| # | Parties | Type | Status |
|---|---|---|---|
| B-2 | ADR-D3 ⇄ ADR-D15 | Dependency / Status integrity | **BLOCKING** |
| B-5 | TR-beatclock-010 ⇄ ADR-D6 | Integration contract | **BLOCKING** |
| 3.4 | ADR-D10 ⇄ ADR-D8 | Dependency ordering | Non-blocking (text error) |
| 3.6 | ADR-D15 ⇄ ADR-D13 | Status misrecord | Non-blocking (text error) |

### Recommended ADR implementation order (topologically sorted)

```
Foundation (no dependencies):
  1. ADR-D6  Audio Middleware                  [Accepted]
  2. ADR-D7  Save/Persistence Middleware       [Accepted]
L1:
  3. ADR-D1  TDE Integration                   [Accepted]   (requires D6)
L2:
  4. ADR-D5  Animation Pipeline                [Accepted]
  5. ADR-D2  Enemy AI Framework                [Accepted]
  6. ADR-D10 Bootstrap Sequencing              [Accepted]   (requires D6, D7, D1)
L3:
  7. ADR-D3  Rhythm Quantization Core          [Accepted — BUT see B-2]
  8. ADR-D4  SO Data Layer                     [Accepted]
  9. ADR-D12 Death & Run Lifecycle             [Accepted]
L4:
 10. ADR-D8  DI & Test Seams                   [Accepted]   (requires D3, D6, D1, D10)
 11. ADR-D13 Run-End Victory Boundary          [Accepted]
L5 (Feature):
 12. ADR-D14 Cancel-Window Transient Protection[Accepted]
 13. ADR-D15 Rhythm Judgment Window Defects    [PROPOSED]   (requires D3, D8, D4)
```

⚠️ **D15 sits in the deepest Feature layer, not Foundation or Core.** On the skill's stated FAIL criterion
("Critical gaps in *Foundation/Core layer* requirements"), this argues *against* FAIL. The FAIL verdict rests on
the blocking **conflict** limb of the criterion (B-2, B-5), not on the coverage limb.

⚠️ **Authority inversion, not a literal cycle**: D15 → D3 → authority-of → D15. See B-2.

---

## 6. Engine compatibility

**Phase 5's audit was not redone and the `unity-specialist` consultation was not performed** — the monthly spend
limit was reached twice and no further agents were spawned. The `engine-compat` fan-out dimension that did run lost
5 of its 6 findings to refutation (`engine-compat-02` was 3/3 refuted), so it is reported as noise rather than signal.

What can be confirmed inline, from reading D15 directly, is that its own engine handling is sound:

- `:16-25` Engine Compatibility marks **Knowledge Risk LOW** on the correct grounds — the judgment algebra touches
  no Unity API.
- It correctly routes the Unity 6.3 `[SerializeField]` **fields-only** constraint to ADR-D4 as the authority face,
  rather than restating it.
- `:48` C-c, `:358`, `:504` and `:713` TD-3 confine the degradation-flag editor gating to Editor assemblies,
  honouring the project's "Runtime 禁引 Odin (`Sirenix.*`)" prohibition.
- `:25` and `:674` place all Validation Criteria in **EditMode pure C#**, gated on ADR-D8's mock seams — consistent
  with this project's actual `Assets/Tests/EditMode|PlayMode` layout.
- `:695` correctly declares ADR-D6 unaffected (FMOD DSP scheduling is orthogonal to a pure algorithmic fix) and
  cites `adr-d6:193`'s `MockDSPClock` as precedent for the verification path.
- The `double`-precision guardrail is stated explicitly: subtract in `double`, narrow only the final small ms delta
  to `float`; converting raw DSP timestamps to `float` before subtracting is forbidden.

**No deprecated-API references and no stale engine-version references were found in ADR-D15.**

---

## 7. GDD revision flags (Architecture → Design feedback)

| GDD | Assumption | Reality (from ADR/engine reference) | Action |
|---|---|---|---|
| `design/gdd/rhythm-core.md` | `:70` Core Rule 2 prose still adds `延迟校准偏移` to the input timestamp | ADR-D15 SPEC-1: F2 Core Rule 7 already applied it; adding it again algebraically cancels | **Needs Revision** |
| `design/gdd/rhythm-core.md` | `:395-398` evaluates on `absOffset` | ADR-D15 修正 4 C-2 requires a signed comparison for the FQA contract | **Needs Revision** |
| `design/gdd/rhythm-core.md` | `:853` lists `LatencyOffsetMs` as C1-consumed F2 data | contradicted by `:193` and `:253` — C1 no longer consumes it | **Needs Revision** |
| `design/gdd/rhythm-core.md` | `:936` states P1's required data is `Rating, OffsetMs, ComboCountAfter` | contradicts `:600`'s own "`ComboCountBefore` 供 P1 显示连击增量动画" | **Needs Revision** |

`design/gdd/systems-index.md` has been updated to mark **C1 rhythm-core** as `Needs Revision` (exact string, no
parentheticals, per Phase 5b).

---

## 8. Architecture document coverage (Phase 6)

`docs/architecture/architecture.md` (`:8` `Last Updated: 2026-08-12`) carries **zero** ADR-D15 propagation:

- `:135` C1 RhythmJudger row — Engine APIs column still lists the stale `RhythmJudgmentEvent` 6-field shape
  (`AttackType/Rating/OffsetMs/BeatDspTime/Mode/ComboCountBefore`) and the ADR attribution reads `(ADR-D3/D8/D12/D13)`
  with no D15.
- `:191`/`:193` data-flow diagram — the payload is drawn as `{Rating,OffsetMs,ComboCountBefore}`.
- No mention anywhere of `MissHeadroomMs`, `MissBudgetShare`, `EarlyLenienceRatio` or `ClampDegradation`.

No orphaned architecture (architecture entries with no corresponding GDD) was found. No systems-index system is
missing from the architecture layers.

---

## 9. Completeness critique (run inline, after the critic agent failed twice)

Angles the fan-out never covered, checked by hand:

| Angle | Result |
|---|---|
| **ADR template conformance** | **Complete.** All 14 sections present: Status, Date, Engine Compatibility, ADR Dependencies, Context, Decision, Alternatives Considered, Consequences, GDD Requirements Addressed, Performance Implications, Migration Plan, Validation Criteria, Related Decisions, Open Follow-up. Satisfies `docs/CLAUDE.md`'s required-section list. |
| **Performance budget** | **Covered and correct** (`:648-656`): CPU negligible (cascade runs on `OnMeterChanged`, not per-frame/per-input); Memory +3 authored floats, +2 effective floats, +1 enum flag, single instance; Load Time zero; Network N/A. Notably it also forbids synchronous network telemetry inside `OnMeterChanged`, routing it through the existing analytics batcher. |
| **Accessibility** | **Covered — and the ADR's strongest section.** `:377-391` corrects a sign-direction error in the prior reasoning (all four calibration-chain truncations push *toward under-compensation*, not over-), and on that basis upgrades 修正 3 from documentation cleanup to an accessibility-critical item: deleting `rhythm-core.md:259`'s 120ms cap widens FE7's acceptable calibration range from 120ms to ±200ms (`:593` P-6), directly serving R-AUDIO-01. `:643` wires it to `accessibility-requirements.md:87`. |
| **Localization** | **N/A** — no player-facing strings; the three new knobs are authoring-time config. |
| **Save / persistence** | **N/A** — `MissHeadroomMs` / `MissBudgetShare` / `EarlyLenienceRatio` live in `RhythmConfigSO` (authored content), not `PlayerInfoSO` (player state). No ADR-D7/EasySave surface. |
| **TDE no-source-modification** | **Honoured** — `:48` C-c; D15 touches no TDE internals. |
| **Odin-in-Runtime prohibition** | **Honoured** — see § 6. |
| **Test architecture placement** | **Correct** — EditMode pure C#, gated on ADR-D8's seams. |
| **Feel / MMFeedbacks boundary** | **Respected** — rating events are published by C1 and subscribed by Feel; D15 introduces no `using` of the Feel namespace into CombatCore. |

**Conclusion of the critique: ADR-D15 is structurally thorough and unusually self-aware. Its defects are almost
entirely in *propagation* rather than in the ADR itself — the one exception is B-1's mathematics.**

---

## 10. Verdict: **FAIL**

Triggered by the skill's "blocking cross-ADR conflicts detected" limb:

- **B-2** — ADR-D3's Status field is outside the template enum; an L3 `Accepted` ADR's body now depends on an L5
  `Proposed` ADR; the story auto-blocking mechanism that keys on exact Status strings is broken. This is a real
  blocking conflict and is independent of whether any code has been written.
- **B-5** — TR-beatclock-010 (`status: active`) mandates an implementation that ADR-D6 expressly forbids.
- **B-1** — a registered Validation Criterion (AC-C1-31) asserts a false equivalence, and the `BPM_max` formula
  would be implemented wrong.

**Read FAIL as "cannot be Accepted as-is", not "this ADR is poor."** D15 sits in the L5 Feature layer rather than
Foundation/Core, which argues against FAIL on the coverage limb; and its argument density, self-awareness about
project constraints, and self-correction on the accessibility sign direction are all well above the repository
average. B-2 through B-7 are overwhelmingly mechanical textual fixes.

### Shortest path to clearing FAIL

| # | Action | Files |
|---|---|---|
| 1 | Fix B-1: add the Perfect branch to `BPM_max`; `848.0 → 847.46`; equivalence to `≥`; name the two thresholds distinctly; add both missing dimensions to the Validator grid and AC-C1-30/31 | `adr-d15` (`:358`, `:362-364`, `:682-683`), `rhythm-core.md` (`:366-372`, `:787`, `:1019-1020`, `:1026`) |
| 2 | Fix B-2: restore `adr-d3:4`'s Status to the bare enum value `Accepted`, move "Amended by D15" into a body Addendum — or revert D3's revised passages until D15 is Accepted | `adr-d3` |
| 3 | Fix B-3: propagate to the control manifest — rewrite `:182`, update `:189`, add the three knobs and `ClampDegradation`, bump `Manifest Version` to 2026-10-22; then sync `architecture.md:135`/`:191`/`:193` | `control-manifest.md`, `architecture.md` |
| 4 | Fix B-4: give AC-C1-29 an explicit mechanism so it never enters main red | `adr-d15:666`, test plan |
| 5 | Fix B-5: rewrite TR-beatclock-010 per `adr-d6:230`, set `revised: 2026-10-22` | `tr-registry.yaml` — **done by this review** |
| 6 | Fix B-6: widen `adr-d15:34`'s Ordering Note to `m0:40/:45/:48/:49/:56/:154` and route through the producer's M0 revision process | `adr-d15:34`, `m0-rhythm-feel-acceptance.md` |
| 7 | Land the PR-7 ruling across § 1.5's six sites (TR-rhythm-012 needs no change) | `registry/architecture.yaml:177`, `architecture.md:135`/`:193`, `adr-d3:58`, `rhythm-core.md:936` |
| 8 | Re-run the 26 lost verifications (`gdd-diff-audit`, `slice-evidence-integrity`, `registry-tr-consistency` #10-12) when agent budget allows | — |

### Required ADRs

**None.** No new ADR is recommended. The four remaining coverage gaps are GDD-is-the-spec application-layer logic,
consistent with the standing convention recorded in `traceability-index.md`.

---

## 11. Phase 9 pre-gate checklist

| Item | Status |
|---|---|
| `tests/unit` | ❌ — **the skill's path list is engine-agnostic; this Unity project uses `Assets/Tests/EditMode`, which exists. Not a real gap.** |
| `tests/integration` | ❌ — same; `Assets/Tests/PlayMode` exists. Not a real gap. |
| `.github/workflows/tests.yml` | ✅ |
| `design/accessibility-requirements.md` | ✅ |
| `design/ux/interaction-patterns.md` | ✅ |
| `docs/architecture/traceability-index.md` | ✅ |
| `docs/architecture/architecture.md` | ✅ |
| `design/gdd/systems-index.md` | ✅ |
| `production/session-state/active.md` | ✅ |
| `production/review-mode.txt` | ✅ (`lean`) |
| `docs/consistency-failures.md` | ❌ — per skill rules this file is **not created**, so no Reflexion Log entry was written for this review |

**`/gate-check` should not be offered yet** — the verdict is FAIL and seven blocking items are open.

---

## 12. Process improvements proposed by this review

- **PR-4**: findings of the class "timing blind spot" should not be closeable as non-blocking. B-4 (the AC-C1-29/CI
  deadlock) is exactly this class and would have been missed by a purely document-scoped review.
- **PR-6**: widen Step 5.7's scan surface to include `tr-registry.yaml` and `docs/registry/architecture.yaml`, and
  add a post-write whole-repo grep. B-3 (`control-manifest.md`) and B-5 (TR-beatclock-010) both escaped because the
  authoring flow scanned only GDDs and ADRs.
- **New (from B-2)**: add a mechanical check that every ADR's `Status:` field matches the three-value enum exactly.
  The story auto-blocking safety net depends on it and failed silently here.
- **New (from B-3)**: when an ADR revises a rule that appears in `control-manifest.md`, the manifest must be in the
  same changeset — its `Manifest Version` is embedded in story files, so a stale manifest actively propagates known
  defects into implementation.
