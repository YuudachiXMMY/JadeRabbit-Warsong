# Epic: F4 ScriptableObject 数据层

> **Layer**: Foundation
> **GDD**: design/gdd/so-data-layer.md
> **Architecture Module**: F4 SO 数据层
> **Status**: Ready
> **Stories**: 8 stories（5 Logic / 3 Integration）

## Overview

本 epic 实现数据契约基础设施：定义所有跨系统游戏数据的存储 schema 与编辑器接口，将硬编码参数转变为可热修改的资产文件。维护 7 类核心 ScriptableObject——`AttackNodeSO`（`AuthoredBpm`/`animationClip`）、`ComboGraphSO`（含 `ComboEdge`）、`PerkSO`（含 `PerkParameter`，仅值类型/string）、`WeaponSO`、`EnemyInfoSO`、`LevelConfigSO`（`_bgmFilePath`）、`PlayerInfoSO`（`[NonSerialized] latencyCalibrationOffsetMs`）。每个 schema 是对应系统（C2/C3/C5/FE1/F2/F5/C4）的数据契约，schema 变更须触发下游 GDD 接口合约更新。本 epic 定义各 SO 类型的字段规范、分类体系、Roguelike 局内克隆规则与 Editor 验证策略。SO 存档方案见 F5，Addressables 加载策略见 F6。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-D4: SO 数据层 | 7 类 SO schema 全覆盖；`PerkEffectType` 锁定 5 值枚举，加第 6 值须 ADR 引用否则 CI fail；`[SerializeField]` 仅字段 | MEDIUM |
| ADR-D7: 存档/持久化 | `PlayerInfoSO` 作为存档消费者契约；版本迁移写入 DTO `save_version` 字段 | LOW |

## GDD Requirements

覆盖统计（traceability-index Rev.10）：12 TR 中 11 Covered / 1 Partial / **0 Gap**（D4 全覆盖）。

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-datalayer-001..012 | 7 类 SO schema 字段规范、克隆规则、Editor 验证、枚举门控 | ADR-D4 ✅ 全覆盖 |
| TR-datalayer-009 | `PlayerInfoSO` 存档消费者字段 | ADR-D7 ✅ |
| TR-datalayer-012 | `AssetReferenceT<T>` Addressables 加载契约 | ADR-D1 ✅（Partial） |

> ✅ **无 untraced 需求**——F4 是 Foundation 层唯一 Gap = 0 的系统，ADR-D4 提供完整架构承接。

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/so-data-layer.md` are verified
- All Logic and Integration stories have passing test files in `tests/`
- All Visual/Feel and UI stories have evidence docs with sign-off in `production/qa/evidence/`

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | 程序集结构与 Addressables/Odin 边界基础设施 | Integration | Ready | ADR-D4 |
| 002 | 攻击节点与连招图 SO schema | Logic | Ready | ADR-D4 |
| 003 | 心法 SO schema 与克隆隔离 | Logic | Ready | ADR-D4 |
| 004 | 武器/敌人/关卡 SO schema | Logic | Ready | ADR-D4 |
| 005 | 玩家持久化 SO schema 与 F5 DTO 契约 | Logic | Ready | ADR-D4, ADR-D7 |
| 006 | 战斗导演全局配置 SO schema | Logic | Ready | ADR-D4 |
| 007 | 全类型重复 ID 与 CI 门控 Validator（Plan B） | Integration | Ready | ADR-D4 |
| 008 | 语义与跨字段 Validator（Plan B） | Integration | Ready | ADR-D4 |

依赖顺序：001 → {002, 003, 005, 006} → 004 → {007, 008}。逐个按 `Depends on:` 字段推进。

> **注**: Story 006 引入第 8 类 SO（CombatDirectorConfigSO），Story 007 的 AC-F4-01 grep 须由 7 类扩为 8 类——验收时提示 GDD 作者更正原文数字。

## Next Step

Run `/story-readiness production/epics/so-data-layer/story-001-assembly-addressables-odin-boundary.md` then `/dev-story` to begin implementation.
