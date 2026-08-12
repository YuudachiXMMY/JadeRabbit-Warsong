# Story 007: 全类型重复 ID 与 CI 门控 Validator（Plan B）

> **Epic**: F4 ScriptableObject 数据层
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: M
> **Manifest Version**: 2026-08-12
> **Last Updated**: —

## Context

**GDD**: `design/gdd/so-data-layer.md`
**Requirement**: `TR-datalayer-011`, `TR-datalayer-005`（primary）
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-D4: SO 数据层
**ADR Decision Summary**: Odin 双模式验证——Plan B 为外部 `[RegisterValidator(typeof(XxxSO))]` Editor-only 类，作为 CI 阻塞驱动；每类 SO 的 id 全局唯一性（按类型隔离）经 `AssetDatabase.FindAssets("t:XxxSO")` 全量比对；任何 Error 级条目导致 CI 非零退出码。

**Engine**: Unity 6.3 LTS (6000.3.19f1) | **Risk**: MEDIUM
**Engine Notes**: `[RegisterValidator]` 类须在 Editor-only asmdef（Story 001 建立）。CI 通过 `-batchmode -executeMethod` 调用专用 Editor 静态入口执行全量 Validator 扫描，据 Error 调 `EditorApplication.Exit(非零码)`——具体入口方法签名待实现阶段确定（此前 QA 审查标记为未定义，见 R10-03）。

**Control Manifest Rules (this layer)**:
- Required: Plan B 外部 Validator 为 CI 阻塞门控（D4-7）；重复 id 报 Error（非 Warning）并注明冲突资产路径
- Forbidden: Runtime 引用 Sirenix；Validator 类置于 Runtime asmdef
- Guardrail: CI 任何 Error 级 → 非零退出码；Warning 级不阻断

---

## Acceptance Criteria

*From GDD `design/gdd/so-data-layer.md`, scoped to this story:*

- [ ] **AC-F4-24**: 两个 PerkSO id 均 "perk_haste" → 报 Error（非 Warning），注明两冲突资产路径
- [ ] **AC-F4-26/27/28/29/30**: AttackNodeSO/ComboGraphSO/WeaponSO/EnemyInfoSO/LevelConfigSO 各自重复 id → RegisterValidator 用 `AssetDatabase.FindAssets("t:XxxSO")` 比较所有 id → 报 Error 注明冲突资产路径
- [ ] **AC-F4-01**: grep 统计 SO 类文件数量——**须同步扩为 8 类**（含第 8 类 CombatDirectorConfigSO），验收时提示 GDD 作者更正原文 7 → 8
- [ ] **AC-F4-07**: CI 执行 Odin Validator，任何 Error 级条目导致非零退出码

---

## Implementation Notes

*Derived from ADR-D4 Implementation Guidelines:*

- 为每类 SO 实现 `[RegisterValidator(typeof(XxxSO))]` 外部 Editor-only 类（Plan B），在 Story 001 建立的 Editor asmdef 中。
- 重复 id 检测：`AssetDatabase.FindAssets("t:XxxSO")` 加载全部同类型资产，按 id 分组，重复组报 **Error（非 Warning）**，消息含全部冲突资产路径。id 唯一性**按类型隔离**（不同类型 id 相同不误报）。
- AC-F4-01：SO 类文件统计须覆盖 8 类（AttackNodeSO/ComboGraphSO/PerkSO/WeaponSO/EnemyInfoSO/LevelConfigSO/PlayerInfoSO/CombatDirectorConfigSO）。命中数 <8 报缺失类名，>8 报多余文件。
- AC-F4-07：CI `-batchmode -executeMethod <入口>` 全量扫描，收集 Error 后 `EditorApplication.Exit(非零)`；仅 Warning → 退出 0。入口方法签名待实现阶段确定。
- 附：PerkEffectType 枚举门控（第 6 值须 ADR 引用否则 CI fail）可在此 CI 驱动内一并实现。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: Editor asmdef 与 RegisterValidator 基础设施骨架
- Story 002-006: 各 SO schema 本体与内联 Plan A 校验
- Story 008: 语义/跨字段 Validator（ComboEdge 引用、hitbox 时间窗、weaponType 一致性、跨 SO starvationCeiling）

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

- **AC-F4-24**: 两个 PerkSO id 均 "perk_haste" → 报 Error（非 Warning），注明两冲突资产路径
  - Given: 两个不同路径的 PerkSO 资产，id 均为 "perk_haste"
  - When: RegisterValidator 通过 `AssetDatabase.FindAssets("t:PerkSO")` 加载全部 PerkSO 资产，按 id 分组查找重复
  - Then: 校验结果包含一条 severity=Error（非 Warning）的结果，错误信息中注明两个冲突资产的完整路径
  - Edge cases: 3 个及以上资产共享同一 id 时须列出全部冲突路径；id 大小写不同（如 "Perk_Haste" vs "perk_haste"）是否算重复待与 GDD 作者确认，测试暂按大小写敏感处理

- **AC-F4-26/27/28/29/30**: AttackNodeSO/ComboGraphSO/WeaponSO/EnemyInfoSO/LevelConfigSO 各自重复 id → 报 Error 注明冲突资产路径
  - Given: 针对 5 种类型分别构造两个同类型且 id 相同的资产场景（AttackNodeSO、ComboGraphSO、WeaponSO、EnemyInfoSO、LevelConfigSO 各一组，共 5 组独立测试）
  - When: 对应类型的 RegisterValidator 通过 `AssetDatabase.FindAssets("t:XxxSO")` 加载该类型全部资产并按 id 分组查找重复
  - Then: 每组测试的校验结果均包含至少一条 Error，注明冲突资产路径；5 个类型的重复 id 检查须相互独立实现
  - Edge cases: 5 类 SO 中任意两个不同类型的资产 id 相同不应触发跨类型误报——id 唯一性约束按类型隔离，非全局唯一

- **AC-F4-01**: grep 统计 SO 类文件数量（须同步扩为 8 类，含 CombatDirectorConfigSO）
  - Given: `Assets/Scripts/` 下全部 F4 相关 SO 类文件（AttackNodeSO/ComboGraphSO/PerkSO/WeaponSO/EnemyInfoSO/LevelConfigSO/PlayerInfoSO/CombatDirectorConfigSO）
  - When: 执行 `grep -rl "class.*: ScriptableObject" Assets/Scripts --include=*.cs` 并统计命中的目标类文件数
  - Then: 命中数量 == 8（原 AC-F4-01 文本按 7 类描述，因 CombatDirectorConfigSO 为 Story006 新增第 8 类 SO，本测试规格须按 8 类执行，验收时须提示 GDD 作者同步更正原文数字）
  - Edge cases: 命中数 < 8 时报出缺失类名；命中数 > 8（存在未预期的 SO 类文件）同样视为门控失败并列出多余文件

- **AC-F4-07**: CI 执行 Odin Validator，任何 Error 级条目导致非零退出码
  - Given: 项目中存在至少一个已知违规资产（如 AC-F4-24 场景的重复 id PerkSO）
  - When: CI 流水线通过 `-batchmode -executeMethod` 调用专用 Editor 静态入口方法执行 Odin Validator 全量扫描，捕获 Error 级结果并据此调用 `EditorApplication.Exit(非零码)`
  - Then: CI 进程退出码非 0；日志中包含每条 Error 的资产路径与错误描述
  - Edge cases: 项目中无任何 Error 时退出码须为 0；仅存在 Warning 级结果时退出码仍须为 0（不阻断构建）；本条依赖具体的 `-executeMethod` 入口方法名，需在实现阶段明确方法签名后回填本测试（该 CI 调用路径此前在 QA 审查中被标记为未定义，见 R10-03）

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- `Assets/Tests/EditMode/F4DataLayer/DuplicateIdValidator_test.cs` — 须存在且通过（AC-F4-24/26/27/28/29/30）
- `Assets/Tests/EditMode/F4DataLayer/SoClassCount_test.cs` — 8 类计数（AC-F4-01）
- CI 门控证据：`tests.yml` 中 Validator 扫描步骤 + 已知违规资产触发非零退出码的运行记录（AC-F4-07）

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 002, 003, 004, 005, 006（全部 SO schema 就绪，含各自 id 字段）；Story 001（Editor asmdef）
- Unlocks: None（CI 门控终点之一）
