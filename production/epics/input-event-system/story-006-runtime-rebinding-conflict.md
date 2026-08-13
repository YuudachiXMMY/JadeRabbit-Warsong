# Story 006: 运行时重绑定 JSON 往返与冲突双广播

> **Epic**: F3 Input 事件系统
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: 2026-08-12
> **Last Updated**: [set by /dev-story when implementation begins]

## Context

**GDD**: `design/gdd/input-event-system.md`
**Requirement**: `TR-input-008`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-D1: TDE 集成架构（primary，Input System 原生重绑定与 TDE 边界）；ADR-D8: DI 缝隙（次要，绑定数据持久化经接口，实际存储归 F5）
**ADR Decision Summary**: 键位重绑定经 Unity Input System 原生 `PerformInteractiveRebinding` / `SaveBindingOverridesAsJson` / `LoadBindingOverridesFromJson`；F3 仅负责重绑定流程与绑定覆盖 JSON 的序列化/反序列化往返，持久化写盘由 F5（EasySave）承接。冲突绑定不静默丢弃——双广播由下游裁决。

**Engine**: Unity 6.3 LTS (6000.3.19f1) | **Risk**: MEDIUM
**Engine Notes**: `InputActionRebindingExtensions.SaveBindingOverridesAsJson(InputActionAsset)` 返回覆盖 JSON 字符串；`LoadBindingOverridesFromJson(asset, json)` 还原。重绑定后须 `Enable()` 受影响 Map。冲突检测用 `InputBinding.effectivePath` 比对；Unity Input System **不**自动拒绝重复绑定——须 F3 显式检测。禁止从训练数据臆测 API 名，以 Unity 6.3 Input System 1.x 实际签名为准。

**Control Manifest Rules (this layer)**:
- Required: 重绑定经 Input System 原生 API；绑定覆盖 JSON 往返无损（save→load 幂等）
- Forbidden: 冲突绑定静默丢弃（须双广播交下游裁决）；F3 直接写盘（持久化归 F5）
- Guardrail: 重绑定 JSON 往返路径无每帧分配（仅在重绑定操作时触发）

---

## Acceptance Criteria

*From GDD `design/gdd/input-event-system.md`, scoped to this story:*

- [ ] AC-F3-10：运行时重绑定后，`SaveBindingOverridesAsJson` 产出的 JSON 经 `LoadBindingOverridesFromJson` 还原，绑定路径完全一致（往返无损）；还原后触发输入按新绑定广播事件
- [ ] AC-F3-15：两个 Action 绑定到同一物理键（冲突）时，按键触发**两个** Action 各自广播（不静默丢弃其一），下游负责裁决优先级；冲突可被检测并上报（供 UI 提示）
- [ ] AC-F3-22：绑定 JSON `save→load→save` 二次导出与首次等价（幂等）；空 override 往返返回等价空状态；`ImportBindingOverrides()` 全程无文件 I/O

---

## Implementation Notes

*Derived from GDD Core Rule 6（控制方案透明化）与 ADR-D1 边界：*

- **JSON 往返**：重绑定完成后调 `SaveBindingOverridesAsJson(inputActionAsset)` 得覆盖 JSON；还原经 `LoadBindingOverridesFromJson(inputActionAsset, json)`。F3 提供 `string ExportBindingOverrides()` / `void ImportBindingOverrides(string json)` 包装，**不**直接文件 I/O——JSON 字符串交 F5（EasySave `PlayerInfoSO`）持久化。往返须幂等：save→load→save 产出等价 JSON。
- **冲突双广播（AC-F3-15）**：两 Action 绑同一物理键时，Input System 会对两 Action 各自触发回调，F3 **不去重**——各自广播对应事件（如 Attack + Dash 同键 → `OnAttackInput` 与 `OnDashInput` 各一次）。优先级裁决是下游（C1/C2/玩法层）职责，非 F3。
- **冲突检测上报**：F3 提供冲突检测（`InputBinding.effectivePath` 比对同 Map 内绑定），返回冲突对供 UI（F4/设置界面）提示，但**不**因冲突拒绝绑定或静默丢弃。
- **控制方案透明**：F3 不知晓具体键位语义；重绑定对下游透明（下游订阅语义事件 `OnAttackInput` 等，不关心物理键）。切换键鼠/手柄方案时下游无感知。
- **测试隔离**：JSON 往返测试用独立 `InputActionAsset` 副本（不污染项目全局 asset）；重绑定用编程方式设置 override（非交互式 `PerformInteractiveRebinding`，后者需真实设备事件）。纯 JSON 序列化逻辑尽量下沉纯 C#。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 003：事件广播基础设施（本 story 复用其广播路径于冲突双广播）
- F5：绑定覆盖 JSON 的实际磁盘持久化（EasySave `PlayerInfoSO` 序列化）——本 story 只做内存往返
- F4/设置 UI：冲突提示的界面呈现（本 story 只提供检测 API）
- 交互式重绑定 UI 流程（`PerformInteractiveRebinding` 的"按任意键"UX）——归 F4

---

## QA Test Cases

*Lean 模式无 qa-lead 生成；以下转写自 GDD 对应 AC 的「验证方法」，实现时对齐这些用例。*

- **AC-1 (AC-F3-10)**: 重绑定 JSON 往返无损
  - Given: 独立 `InputActionAsset` 副本，编程方式设置一组绑定 override
  - When: `ExportBindingOverrides()` → 清除 override → `ImportBindingOverrides(json)`
  - Then: 还原后绑定 `effectivePath` 与原始完全一致；触发输入按新绑定广播事件
  - Edge cases: save→load→save 幂等（二次 JSON 与首次等价）；空 override 往返返回等价空状态
- **AC-2 (AC-F3-15)**: 冲突绑定双广播
  - Given: 两 Action（如 Attack、Dash）绑定到同一物理键
  - When: 触发该物理键
  - Then: 两 Action 各自广播（`OnAttackInput` 与 `OnDashInput` 各一次），无一被静默丢弃
  - Edge cases: 冲突检测 API 正确返回冲突绑定对；F3 不拒绝绑定、不改变广播行为
- **AC-3 (AC-F3-22)**: JSON 往返幂等 + 空状态 + 无 I/O
  - Given: 独立 `InputActionAsset` 副本
  - When: (1) 设 override→`ExportBindingOverrides()` 得 J1→`ImportBindingOverrides(J1)`→再 `ExportBindingOverrides()` 得 J2；(2) 未设 override 时导出/导入
  - Then: (1) J1==J2；(2) 空状态往返仍为空集；全程无 `System.IO.File.*` 调用
  - Edge cases: 导入含未知/过期 Action 路径的 JSON 不抛异常，忽略无法解析条目并记 Warning（若 GDD 未定义此行为，标注待确认）

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- `Assets/Tests/EditMode/input/rebinding_conflict_test.cs` — 覆盖 AC-F3-10/15 的 EditMode 单元测试，必须存在且通过
- `Assets/Tests/EditMode/input/rebinding_conflict_test.cs`（新增覆盖 AC-F3-22 幂等/空状态/无 I/O）— 必须存在且通过

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 003（事件广播路径）
- Unlocks: None
