# Epics Index

Last Updated: 2026-10-22
Engine: Unity 6.3 LTS (6000.3.19f1)

| Epic | Layer | System | GDD | Stories | Status |
|------|-------|--------|-----|---------|--------|
| F2 音频/拍点时钟系统 | Foundation | F2 BeatClock | design/gdd/audio-beat-clock.md | 14 stories | Ready |
| F3 Input 事件系统 | Foundation | F3 InputEventBus | design/gdd/input-event-system.md | 8 stories | Ready |
| F4 ScriptableObject 数据层 | Foundation | F4 SO 数据层 | design/gdd/so-data-layer.md | 8 stories | Ready |
| F5 存档/持久化系统 | Foundation | F5 SaveService | design/gdd/save-persistence.md | 8 stories | Ready |
| F6 TDE & Animancer 集成层 | Foundation | F6 TDELifecycleAdapter | design/gdd/tde-integration.md | 12 stories | Ready |
| C1 节奏量化攻击系统 | Core | C1 RhythmJudger | design/gdd/rhythm-core.md | Not yet created | Ready |
| C4 战斗导演 | Core | C4 战斗导演（CombatCore） | design/gdd/combat-director.md | Not yet created | Ready |

## Notes

- **审查模式**: lean（PR-EPIC producer 结构门禁本轮跳过）
- **治理 ADR 状态**: F2–F6 全部治理 ADR 均已 Accepted（ADR-D1/D4/D5/D6/D7/D8/D10/D12/D13）；
  Core 层 C1 的治理 ADR 中 **ADR-D15 仍为 `Proposed`**（节奏判定窗口缺陷修正），C4 的 ADR-D2 尚有 E1 同帧保证与
  `OnTokenGranted` 签名两项待修订
- **实装前硬门禁**:
  - F6 TR-tde-014（TDE 胜利/通关事件成员名核对）——胜利桥接相关 story 标 Blocked 直至核对完成
  - **C1 — ADR-D15 文件尚未入基线**：`docs/architecture/adr-d15-rhythm-judgment-window-defects.md` 不在当前
    `origin/main` 上。`/create-stories` 的 ADR existence validation 会在 Step 2 立即中止
    （`.claude/skills/create-stories/SKILL.md:53`/`:59`），故 `/create-stories rhythm-core` 在该文件落盘前必然空转
  - **Core 层共同前置 — M0 手感判据 PASS 未裁定**：`production/milestones/m0-rhythm-feel-acceptance.md:144` 载明
    本判据 PASS 是 `/create-epics layer:core` 的手感前提，而 `:150`–`:152` 三方签字（Producer / game-designer /
    technical-director）均为 ⏳ 待签、D-2 窗口值未回填、D-3 Go/No-Go 无记录。C1/C4 两份 epic 是该前置未闭合下的
    规划产物，**实现冲刺不得在三方签字与 D-3 裁定前启动**
  - **control-manifest 过期**：`docs/architecture/control-manifest.md:5` 的 `Manifest Version: 2026-08-12` 自述
    排除当时仍 Proposed 的 ADR-D14，而 D14 已于 2026-08-13 Accepted。story 创建时即固化该版本号
    （`control-manifest.md:9-10`），故须在任何 `/create-stories` **之前**跑 `/create-control-manifest update`
- **覆盖亮点**: F4 Gap = 0（ADR-D4 全覆盖）；F2/F3/F5 的 Gap 均为「GDD 即规范」预期内部项，非真实 ADR 缺口；
  Core 四系统 Gap 亦为 0（`traceability-index.md:63`），C1/C4 各 19 TR → 17 Covered / 2 Partial
- **本轮未创建的 Core epic**:
  - **C2 连招系统 / C3 心法构筑** — GDD 头部仍为 `Revised (2026-08-10 blocker fix) — Pending Re-review`
    （`design/gdd/combo-system.md:3`、`design/gdd/perk-system.md:3`），与 `design/gdd/systems-index.md` 的
    `Approved` 行冲突；且 `production/warning-ledger.md:33`（SG-5）要求 C2 GDD 在 ADR-D14 Accept 后补文本，
    而 `combo-system.md` 中 D14 命中 0 次。**资格冲突须由 game-designer / Producer 裁定，不代批**
  - **C5 / C6** — GDD 尚未撰写（systems-index 记 Not Started）
- **跨 epic 待统一口径（producer 裁决）**: 同一 ADR 的 Engine Risk 在各 epic 取值不一致——ADR-D6 在
  `audio-beat-clock/EPIC.md:17` 与 `input-event-system/EPIC.md:35` 记 HIGH，C4 epic 取 ADR 原栏的 MEDIUM
  （`adr-d6:15`）；ADR-D13 在 `tde-integration/EPIC.md:21` 记 LOW，C4 epic 取原栏 MEDIUM（`adr-d13:22`）。
  须定单一口径并回填既有 epic
- **上游缺口（影响 Core story 拆分）**: `RhythmConfigSO` 无 epic 承接（GDD 挂 ADR-D4/F4 名下，schema 实声明于
  `adr-d3:217-218`，F4 三处权威清单均不含它）；FE7 游戏设置系统无 epic 却是 `QuantizationMode` 唯一注入方；
  FE3 延迟校准无 epic 却是 `LatencyOffsetMs` 唯一 runtime 写者
