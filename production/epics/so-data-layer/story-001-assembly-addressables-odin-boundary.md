# Story 001: 程序集结构与 Addressables/Odin 边界基础设施

> **Epic**: F4 ScriptableObject 数据层
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: M
> **Manifest Version**: 2026-08-12
> **Last Updated**: —

## Context

**GDD**: `design/gdd/so-data-layer.md`
**Requirement**: `TR-datalayer-004`, `TR-datalayer-005`（partial）, `TR-datalayer-012`（partial）
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-D4: SO 数据层
**ADR Decision Summary**: 全部 8 类 SO 归属 Addressable Group（禁止 `Resources/` 加载）；Runtime/Editor 程序集拆分，Odin/Sirenix 命名空间仅限 Editor 程序集；CI 门控 Validator 类（`[RegisterValidator]`）须置于 `includePlatforms:["Editor"]` 的 asmdef。

**Engine**: Unity 6.3 LTS (6000.3.19f1) | **Risk**: MEDIUM
**Engine Notes**: Addressables 工作流在 Unity 6 有变更（内容目录、`AssetReferenceT<T>` 惰性引用）；asmdef `includePlatforms` 语义须验证——空数组表示"全平台"，非 Editor-only。Sirenix 命名空间在打包时不打入包体，Runtime 引用会导致构建失败。

**Control Manifest Rules (this layer)**:
- Required: 全部 SO 资产归属至少一个 Addressable Group；大资源引用用 `AssetReferenceT<T>` 惰性加载（D4-3/D4-5）
- Forbidden: Runtime 代码引用 `Sirenix.*` 命名空间；`Resources/` 目录加载 F4 SO 资产
- Guardrail: Odin 双模式验证——Plan A 内联 `#if UNITY_EDITOR` 属性 + Plan B 外部 `[RegisterValidator]` Editor-only 类（D4-7）

---

## Acceptance Criteria

*From GDD `design/gdd/so-data-layer.md`, scoped to this story:*

- [ ] **AC-F4-08**（BLOCKING）: Runtime 程序集（`Assets/Scripts/`，排除 `Assets/Editor/`）中 `grep -rn "using Sirenix"` 预期 0 条匹配（不用行首锚点，须覆盖 `#if UNITY_EDITOR` 块内缩进的 using）；含 `[RegisterValidator]` 的 .asmdef 须 `includePlatforms:["Editor"]`
- [ ] **AC-F4-33**（BLOCKING，day-1 要求）: `grep -rn 'RegisterValidator' Assets/ --include='*.cs' | grep -v 'Assets/Editor/'` 预期 0 条；所有 RegisterValidator 类须在 Editor-only asmdef
- [ ] **AC-F4-09**: 全部 8 类 SO 资产归属至少一个 Addressable Group；`Assets/Resources/` 中无 F4 SO 资产

---

## Implementation Notes

*Derived from ADR-D4 Implementation Guidelines:*

- 建立 Runtime asmdef（`Assets/Scripts/` 下，如 `JadeRabbit.DataLayer.asmdef`）与独立 Editor asmdef（`Assets/Editor/` 或带 `includePlatforms:["Editor"]`），Editor asmdef 通过 `InternalsVisibleTo` 访问 Runtime internal 成员用于验证。
- 全部 `[RegisterValidator(typeof(XxxSO))]` 外部验证类（Plan B）归属 Editor-only asmdef——这是 day-1 基础设施，后续 Story 007/008 在此之上实现具体验证逻辑。
- 建立 Addressable Group（如 `DataLayer_SO`），后续 SO 资产 story 将资产纳入该组。禁止在 `Assets/Resources/` 下放置任何 F4 SO 资产。
- 大资源引用（AnimationClip、Sprite）用 `AssetReferenceT<T>`，加载契约由 C2 拥有（D4-4）；本 story 只搭建程序集与 Group 骨架。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002-006: 各具体 SO 类型 schema 定义
- Story 007: 全类型重复 ID 与 CI 门控 Validator 具体逻辑
- Story 008: 语义与跨字段 Validator 具体逻辑
- AnimationClip/Sprite 的实际运行时加载（C2/C3 拥有）

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

- **AC-F4-08**: Runtime 程序集禁止 `using Sirenix`；含 `[RegisterValidator]` 的 .asmdef 须 `includePlatforms:["Editor"]`
  - Given: 项目 `Assets/Scripts/` 下全部 .cs 文件（不含 `Assets/Editor/`）与全部 .asmdef 文件
  - When: 执行 `grep -rln "using Sirenix" Assets/Scripts --include=*.cs | grep -v "Assets/Editor/"`；并解析每个含 RegisterValidator 类的 .asmdef 的 `includePlatforms` 字段
  - Then: grep 匹配结果为 0 条；每个含 RegisterValidator 的 .asmdef 的 `includePlatforms` 数组严格等于 `["Editor"]`
  - Edge cases: `#if UNITY_EDITOR` 块内带前导空白缩进的 `using Sirenix.xxx;` 仍须被匹配（不使用 `^` 行首锚点）；.asmdef 中 `includePlatforms` 为空数组（语义为"全平台"）须判定为失败

- **AC-F4-33**: `grep -rn 'RegisterValidator' Assets/ --include='*.cs' | grep -v 'Assets/Editor/'` 预期 0 条
  - Given: `Assets/` 下全部 .cs 文件（不含 `Assets/Editor/`）
  - When: 执行 `grep -rn 'RegisterValidator' Assets/ --include='*.cs' | grep -v 'Assets/Editor/'`
  - Then: 匹配结果为 0 条
  - Edge cases: 类名或注释中含 "RegisterValidator" 子串但非 `[RegisterValidator(...)]` 属性语法（如注释、变量名）造成误报——测试脚本须先排除注释行，仅匹配属性语法

- **AC-F4-09**: 全部 8 类 SO 资产归属至少一个 Addressable Group；`Assets/Resources/` 中无 F4 SO 资产
  - Given: 项目内已导入的全部 8 类 F4 SO 资产（AttackNodeSO/ComboGraphSO/PerkSO/WeaponSO/EnemyInfoSO/LevelConfigSO/PlayerInfoSO/CombatDirectorConfigSO）
  - When: (a) shell 侧执行 `find Assets/Resources -iname "*.asset"` 并交叉比对 8 类 SO 类型；(b) Editor 侧遍历 `AssetDatabase.FindAssets("t:AttackNodeSO t:ComboGraphSO t:PerkSO t:WeaponSO t:EnemyInfoSO t:LevelConfigSO t:PlayerInfoSO t:CombatDirectorConfigSO")` 找到的每个 GUID，调用 `AddressableAssetSettingsDefaultObject.Settings.FindAssetEntry(guid)`
  - Then: (a) 命中 0 条；(b) 对每个 GUID 返回非 null 的 AddressableAssetEntry
  - Edge cases: 资产同时存在于 Resources 且已被 Addressable Group 打包（双重注册）——仍应判定 (a) 失败；SO 资产为未保存的临时内存实例（无 GUID）——测试须先 `AssetDatabase.SaveAssets()` 确保有效 GUID 后再执行 (b)

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- `Assets/Tests/EditMode/F4DataLayer/AssemblyBoundary_test.cs` — 须存在且通过（含 Sirenix grep 检查、RegisterValidator asmdef 检查、Addressable 归属检查）

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: None（day-1 基础设施）
- Unlocks: Story 002, 003, 004, 005, 006（各 SO schema 依赖程序集与 Group 就绪）
