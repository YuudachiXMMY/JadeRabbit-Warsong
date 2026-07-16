# Technical Preferences

<!-- Populated by /setup-engine. Updated as the user makes decisions throughout development. -->
<!-- All agents reference this file for project-specific standards and conventions. -->

## Engine & Language

- **Engine**: Unity 6.3 LTS (6000.3.19f1)
- **Language**: C#
- **Rendering**: Universal Render Pipeline (URP)
- **Physics**: Unity Physics (Rigidbody-based)

## Input & Platform

<!-- Written by /setup-engine. Read by /ux-design, /ux-review, /test-setup, /team-ui, and /dev-story -->
<!-- to scope interaction specs, test helpers, and implementation to the correct input methods. -->

- **Target Platforms**: PC (Steam / Epic), Console, Mobile (iOS / Android)
- **Input Methods**: Keyboard/Mouse, Gamepad, Touch
- **Primary Input**: Mixed
- **Gamepad Support**: Full
- **Touch Support**: Full
- **Platform Notes**: All UI must support d-pad navigation for console.
  Touch interactions must have non-hover equivalents for mobile.
  PC layout is primary; mobile/console receive adapted variants.

## Naming Conventions

- **Classes**: PascalCase (e.g., `PlayerController`)
- **Public Fields/Properties**: PascalCase (e.g., `MoveSpeed`)
- **Private Fields**: _camelCase (e.g., `_moveSpeed`)
- **Methods**: PascalCase (e.g., `TakeDamage()`)
- **Files**: PascalCase matching class name (e.g., `PlayerController.cs`)
- **Scenes/Prefabs**: PascalCase matching root node (e.g., `PlayerController.prefab`)
- **Constants**: PascalCase or UPPER_SNAKE_CASE

## Performance Budgets

- **Target Framerate**: 60fps (PC / Console), 30fps (Mobile)
- **Frame Budget**: 16.6ms (PC / Console), 33ms (Mobile)
- **Draw Calls**: ≤150 (PC / Console), ≤100 (Mobile)
- **Memory Ceiling**: ≤2GB (PC / Console), ≤512MB (Mobile)

## Testing

- **Framework**: Unity Test Runner (NUnit)
- **Minimum Coverage**: 80%
- **Required Tests**: Balance formulas, gameplay systems, networking (if applicable)

## Forbidden Patterns

<!-- Add patterns that should never appear in this project's codebase -->

- **禁止修改 TDE 源码**: 一切对 TopDownEngine 内部类的直接改动均被禁止。
  理由：TDE 升级会覆盖修改；v1 项目因此留下 582 行覆写陷阱。
  替代方案：继承 TDE 类后 override，或通过 TDE 的事件系统扩展。

- **禁止在 Runtime 代码中引用 Odin 命名空间** (`Sirenix.*`):
  Odin 仅在 Editor 程序集中可用；Runtime 代码引用会导致打包失败或体积膨胀。
  替代方案：在 `#if UNITY_EDITOR` 块内或 `Editor/` 目录下使用 Odin API。

- **禁止用 `Update()` 轮询做节拍判定**:
  60FPS 下 `Update()` 时间戳误差高达 16.7ms，超过目标判定窗口容差。
  替代方案：使用 Input System 的 `InputAction.performed` callback 提供的精确时间戳。
  来源：game-concept.md 技术风险 § 帧量化误差。

- **禁止绑死攻击动画帧数**:
  固定帧数的攻击动画无法随 BPM 变速，破坏节奏手感一致性。
  替代方案：通过 Animancer 的 `AnimancerState.Speed` 属性按当前 BPM 动态缩放动画时长。

## Allowed Libraries / Addons

<!-- Add approved third-party dependencies here -->

- **TopDownEngine** (More Mountains, Asset Store) — 移动系统、生命系统、关卡管理（LevelManager）、
  MMFeedbacks/Feel 反馈基础设施。F6 TDE 集成层的基础。
  **边界合约（强制）**: TDE 负责移动/生命/关卡/反馈；CombatCore 程序集负责节奏量化与连招逻辑。
  **禁止修改 TDE 源码**，只允许通过继承和事件扩展。覆写 TDE 内部类需走 ADR 审批。

- **Animancer** (Kybernetik, Asset Store) — 运行时动画状态管理，用于攻击层动画与 BPM 变速适配。
  替代 Animator Controller 的状态机逻辑（仅攻击层），TDE 的角色 Animator 层保持不变。
  **注意**: Animancer 攻击层与 TDE 原生 Animator 层不可冲突，须在 F6 GDD 接口合约中明确边界。

- **Odin Inspector** (Sirenix, Asset Store) — 编辑器检视面板增强，ScriptableObject 可视化编辑与验证。
  **仅限 Editor 程序集使用**，Runtime 代码禁止引用 Odin 命名空间（打包时 Odin 不打入包体）。
  ScriptableObject 类的 `[Required]`、`[ValidateInput]` 等 Attribute 仅在 Editor 生效。

- **Odin Validator** (Sirenix, Asset Store) — 项目资源合规性验证，确保 SO 数据字段完整性。
  配合 Odin Inspector 使用；同样仅限 Editor，禁止在 Runtime 代码路径中触发验证逻辑。

- **Feel / MMFeedbacks** (More Mountains, Asset Store) — 反馈系统基础设施（视觉、音效、触感反馈序列）。
  作为 TopDownEngine 捆绑包的一部分引入，但版本独立管理。
  **用途**: 所有 Perfect/Good/Miss 反馈序列通过 MMFeedbacks 触发；CombatCore 发布评级事件，Feel 订阅播放。
  **约束**: Feel 3.x 废弃了 `MMFeedback` 基类，改用 `Feedback` 抽象类；确认版本后统一命名空间。
  **仅通过事件订阅使用**，不在 CombatCore 中直接 using Feel 命名空间（保持边界隔离）。

- **EasySave 3** (Moodkie, Asset Store) — 存档与持久化系统（F5）。
  用于 PlayerInfoSO 序列化（功勋、武器解锁、设置、延迟校准偏移量）。
  **约束**: 禁止直接序列化 MonoBehaviour 引用；只序列化纯数据结构（struct / class，无 Unity Object 引用）。
  ES3 的云备份功能（ES3Cloud）本项目不启用。
  ADR-D7 将正式选定 EasySave 3 vs 自定义 JSON 方案。

- **DOTween** (Demigiant, Asset Store — Pro) — UI 动画与补间工具。
  **用途范围**: UI 过渡动画（非节奏关键帧动画）。节奏反馈动画统一使用 Feel/MMFeedbacks，
  不得与 DOTween 在同一 Transform 上双重驱动。
  **约束**: 游戏启动时须调用 `DOTween.Init()` 并设置容量，避免运行时 GC（见 DOTween 最佳实践）。
  禁止在 Update/FixedUpdate 中每帧创建新 Tween（应复用或 Sequence 管理）。

- **Hot Reload** (CodeFloof, Asset Store) — C# 代码热重载（Editor 开发期提速工具）。
  **仅限开发期 Editor 使用**，不影响任何 Runtime 行为，不打包进构建产物。
  **约束**: 不支持 DOTS/Burst 代码热重载；静态字段在热重载后须手动重置（运行时单例须注意）。
  与 Odin Inspector 同时使用时须注意序列化检视面板刷新时机。
  CI 构建和 Player 构建中完全忽略此插件。

## Architecture Decisions Log

<!-- Quick reference linking to full ADRs in docs/architecture/ -->
- [No ADRs yet — use /architecture-decision to create one]

## Engine Specialists

<!-- Written by /setup-engine when engine is configured. -->
<!-- Read by /code-review, /architecture-decision, /architecture-review, and team skills -->
<!-- to know which specialist to spawn for engine-specific validation. -->

- **Primary**: unity-specialist
- **Language/Code Specialist**: unity-specialist (C# review — primary covers it)
- **Shader Specialist**: unity-shader-specialist (Shader Graph, HLSL, URP/HDRP materials)
- **UI Specialist**: unity-ui-specialist (UI Toolkit UXML/USS, UGUI Canvas, runtime UI)
- **Additional Specialists**: unity-dots-specialist (ECS, Jobs system, Burst compiler), unity-addressables-specialist (asset loading, memory management, content catalogs)
- **Routing Notes**: Invoke primary for architecture and general C# code review. Invoke DOTS specialist for any ECS/Jobs/Burst code. Invoke shader specialist for rendering and visual effects. Invoke UI specialist for all interface implementation. Invoke Addressables specialist for asset management systems.

### File Extension Routing

<!-- Skills use this table to select the right specialist per file type. -->
<!-- If a row says [TO BE CONFIGURED], fall back to Primary for that file type. -->

| File Extension / Type | Specialist to Spawn |
|-----------------------|---------------------|
| Game code (.cs files) | unity-specialist |
| Shader / material files (.shader, .shadergraph, .mat) | unity-shader-specialist |
| UI / screen files (.uxml, .uss, Canvas prefabs) | unity-ui-specialist |
| Scene / prefab / level files (.unity, .prefab) | unity-specialist |
| Native extension / plugin files (.dll, native plugins) | unity-specialist |
| General architecture review | unity-specialist |
