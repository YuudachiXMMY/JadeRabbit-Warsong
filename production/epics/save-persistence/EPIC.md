# Epic: F5 存档/持久化系统

> **Layer**: Foundation
> **GDD**: design/gdd/save-persistence.md
> **Architecture Module**: F5 SaveService
> **Status**: Ready
> **Stories**: Not yet created — run `/create-stories save-persistence`

## Overview

本 epic 实现所有跨局数据的读写基础设施：将运行时 `PlayerInfoSO` 的玩家进度（功勋、武器解锁）与全局配置（音量/键位、延迟校准偏移、无障碍偏好）序列化到本地磁盘，并在启动时反向加载。存档触发采用混合模式——局结算事件（死亡/通关）自动写玩家进度，设置类数据修改后即时持久化，延迟校准偏移校准完成后一次性写入。支持多存档槽位（进度独立），全局设置跨槽位共享、绑定设备而非玩家档案。通过 EasySave 3（ES3）实现序列化，采用 DTO 模式：运行时 SO 存档前提取为纯数据结构，加载时反向填充。下游（FE3/FE7/FE5/PL4）经 `ISaveService` 统一服务接口存取，不直接调用 ES3 API。状态机为 Uninitialized→Idle→Saving/Loading→Error。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-D7: 存档/持久化中间件 | 选定 EasySave 3；DTO 模式；版本迁移写入 DTO `save_version` 字段由 `SaveService.MigrateIfNeeded<T>()` 处理（不用 ES3 版本机制）；禁 ES3Cloud | LOW |
| ADR-D10: Bootstrap 顺序 | `ISaveService.Initialize()` 同步阻塞 <50ms，bootstrap driver 中优先初始化供下游 `IsInitialized` 消费 | LOW |

## GDD Requirements

覆盖统计（traceability-index Rev.10）：13 TR 中 2 Covered / 0 Partial / 11 Gap。

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-save-005 | EasySave 3 序列化 + DTO 提取/填充模式 | ADR-D7 ✅ |
| TR-save-008 | 版本迁移经 DTO `save_version` 字段（非 ES3 版本机制） | ADR-D7 ✅ |
| （其余 11 项 TR-save-001..013） | 状态机、多槽位、混合触发、`ISaveService`/`IFileStorage` 接口面等应用逻辑 | GDD 即规范（预期内部项，非 ADR 缺口） |

> ⚠️ 11 项 Gap 均为「GDD 即规范」预期内部项——存档服务应用逻辑以 GDD 为规范文档。架构评审（2026-08-12）已确认非真实缺口，不阻塞 story 创建。

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/save-persistence.md` are verified
- All Logic and Integration stories have passing test files in `tests/`
- All Visual/Feel and UI stories have evidence docs with sign-off in `production/qa/evidence/`

## Next Step

Run `/create-stories save-persistence` to break this epic into implementable stories.
