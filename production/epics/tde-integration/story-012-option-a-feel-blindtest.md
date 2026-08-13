# Story 012: Option A 体感盲测

> **Epic**: F6 TopDownEngine & Animancer 集成层
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Visual/Feel
> **Estimate**: S
> **Manifest Version**: 2026-08-12
> **Last Updated**: [set by /dev-story when implementation begins]

## Context

**GDD**: `design/gdd/tde-integration.md`
**Requirement**: `TR-tde-001`（集成体感验收——Option A 变速手感）
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-D5: 动画管线（primary）
**ADR Decision Summary**: BPM 驱动的攻击变速（Option A：`AnimancerState.Speed` 动态缩放）须经体感盲测验收——玩家在不知当前 BPM 的前提下，攻击手感与节奏一致、无脱拍/黏滞感，确认变速方案主观达标。

**Engine**: Unity 6.3 LTS (6000.3.19f1) | **Risk**: LOW（主观验收，非代码逻辑）
**Engine Notes**: 无新 API——依赖 Story 010/011 已接线的变速攻击。体感质量不可自动化（遵 coding-standards「What NOT to Automate」）。

**Control Manifest Rules (Foundation)**:
- Required: 体感验收为 ADVISORY 门（截图/录像 + lead sign-off，非 BLOCKING）
- Guardrail: 盲测在目标帧率下进行（PC 60fps），避免帧率波动污染手感判断
- Forbidden: 不以自动化断言替代主观体感评估

---

## Acceptance Criteria

*From GDD `design/gdd/tde-integration.md`, scoped to this story:*

- [ ] AC-F6-20：Option A 变速攻击经体感盲测——多档 BPM（低/中/高）下攻击手感与节奏一致，无明显脱拍或黏滞；audio-director / creative-director 或指定 lead 签核通过
- [ ] AC-F6-34：3 档 BPM（随机序）每档 10+ 次连续攻击录屏，经审阅确认起手/命中帧贴拍（无脱拍/黏滞）；逐档评语 + 录屏/关键帧截图 + audio-director 或 creative-director 签核，完整存入 `production/qa/evidence/story-012-feel-evidence.md`

---

## Implementation Notes

*Derived from ADR-D5 + coding-standards 体感验收:*

- 前置：Story 009（装配）+ Story 010（接线）+ Story 011（公式）已完成并跑绿。
- 搭建盲测场景：可切换若干档 BPM（如 90/120/160），测试者不可见当前 BPM 数值。
- 记录每档下攻击的主观手感（是否跟拍、是否黏滞/过快），录屏或截图关键帧存 `production/qa/evidence/`。
- lead 签核：由 audio-director 或 creative-director 对「变速手感一致」下结论；若不达标，回退调 Story 011 公式参数（baseBpm/钳制）或与 Option B 对比（若 GDD 留有备选）。

---

## Out of Scope

- Story 010/011：变速接线与公式（本 story 只验收其体感产出）
- 自动化速度断言（已在 Story 010/011 覆盖客观正确性；本 story 只管主观手感）

---

## QA Test Cases

*Embedded from GDD AC-F6-20（lean 模式，Visual/Feel → 手动验证步骤）。*

**Manual check（AC-F6-20，Visual/Feel — 盲测）**
  - Setup：加载盲测场景，隐藏 BPM 数值；准备 3 档 BPM（低/中/高）随机顺序切换
  - Verify：每档下连续攻击 10+ 次，观察攻击起手/命中帧是否贴合节拍音；有无脱拍（攻击慢于拍）或黏滞（攻击拖尾）
  - Pass condition：3 档均被测试者判为「手感跟拍、无明显脱拍/黏滞」，且 audio-director 或 creative-director 签核；证据（录屏/截图 + 签核记录）存入 `production/qa/evidence/story-012-feel-evidence.md`

**Manual check（AC-F6-34，Visual/Feel — 证据留存复核）**
  - Setup：与 AC-F6-20 盲测同批次，额外全程录屏 + 逐档独立记录模板
  - Verify：3 档随机序均有独立录屏、逐档评语、审阅确认贴拍；签核含人/日期
  - Pass condition：`production/qa/evidence/story-012-feel-evidence.md` 存在且含 3 档完整证据链（录屏/截图 + 评语 + 签核）可追溯

---

## Test Evidence

**Story Type**: Visual/Feel
**Required evidence**:
- `production/qa/evidence/story-012-feel-evidence.md`（盲测录屏/截图 + lead 签核）— ADVISORY 门
- `production/qa/evidence/story-012-feel-evidence.md`（新增要求：3 档独立录屏/截图 + 逐档评语 + 签核）— ADVISORY 门

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 009、Story 010、Story 011（变速攻击完整链路就绪）
- Unlocks: None（F6 集成体感验收，epic 收尾）
