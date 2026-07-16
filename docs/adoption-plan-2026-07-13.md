# Adoption Plan

> **Generated**: 2026-07-13（第二次运行 — 声明新插件 Feel / EasySave 3 / DOTween / Hot Reload）
> **Project phase**: Concept
> **Engine**: Unity 6.3 LTS (6000.3.19f1)
> **Third-party plugins declared**: TopDownEngine (TDE), Animancer, Odin Inspector, Odin Validator,
>   Feel (MMFeedbacks), EasySave 3, DOTween, Hot Reload
> **Template version**: v1.0+

按顺序完成以下步骤，每完成一项打勾。随时重新运行 `/adopt` 检查剩余差距。

---

## Step 1: 修复 HIGH 差距

### 1a. 注册已有插件到 Allowed Libraries ✅ 已完成 (2026-07-13)

TopDownEngine、Animancer、Odin Inspector、Odin Validator 已在首次运行时注册。

---

### 1b. 修复 easysave-3.md 标题错误

**问题**: `docs/engine-reference/unity/plugins/easysave-3.md` 文件标题行为
`# TopDownEngine (More Mountains) — Plugin Reference`，但该文件应记录 EasySave 3 (Moodkie)。
内容标题与文件名不符，架构工具在读取插件信息时会混淆插件身份，导致 ADR 的
Engine Compatibility 节引用错误的插件约束。

**修复**: 编辑 `docs/engine-reference/unity/plugins/easysave-3.md` 第 1 行，将标题改为：
```
# EasySave 3 (Moodkie) — Plugin Reference
```

**时间**: 2 分钟
- [x] `easysave-3.md` 标题已更正

---

### 1c. 添加 Feel (MMFeedbacks) 到 Allowed Libraries + 创建插件文档

**问题**: Feel 在 `technical-preferences.md` 中仅作为 TopDownEngine 条目的子功能提及
（"MMFeedbacks/Feel 反馈基础设施"），但 Feel 有独立的 Asset Store 包、独立版本号、
独立的 API 约束（MMFeedbacks 2.x vs 3.x 有 breaking change）。
`/architecture-decision` 为 D3（TDE 集成边界）写 Engine Compatibility 节时对 Feel
自身版本约束盲视，可能产出与 Feel 3.x API 不兼容的架构建议。

**修复 — Allowed Libraries**:
编辑 `.claude/docs/technical-preferences.md`，在 `## Allowed Libraries / Addons` 节
在 TopDownEngine 条目后添加：

```markdown
- **Feel / MMFeedbacks** (More Mountains, Asset Store) — 反馈系统基础设施（视觉、音效、触感反馈序列）。
  作为 TopDownEngine 捆绑包的一部分引入，但版本独立管理。
  **用途**: 所有 Perfect/Good/Miss 反馈序列通过 MMFeedbacks 触发；CombatCore 发布评级事件，Feel 订阅播放。
  **约束**: Feel 3.x 废弃了 `MMFeedback` 基类，改用 `Feedback` 抽象类；确认版本后统一命名空间。
  **仅通过事件订阅使用**，不在 CombatCore 中直接 using Feel 命名空间（保持边界隔离）。
```

**修复 — 插件文档**:
创建 `docs/engine-reference/unity/plugins/feel.md`，包含：版本号、与 Unity 6.3 LTS 兼容状态、
MMFeedbacks 2.x → 3.x 迁移注意事项、与 TDE 的捆绑关系说明。

**时间**: 20 分钟
- [x] `technical-preferences.md` 已添加 Feel 条目
- [x] `docs/engine-reference/unity/plugins/feel.md` 已创建（含版本号）

---

### 1d. 添加 EasySave 3 到 Allowed Libraries

**问题**: `docs/engine-reference/unity/plugins/easysave-3.md` 插件文档已存在，
但 `.claude/docs/technical-preferences.md` 的 Allowed Libraries 节中**没有 EasySave 3 条目**。
首次迁移计划遗漏了此项。缺少条目时，`/architecture-decision` 为 D6（存档方案）写
Engine Compatibility 节时不会加载 ES3 版本约束，可能建议与 ES3 冲突的序列化方案。

**修复**:
编辑 `.claude/docs/technical-preferences.md`，在 `## Allowed Libraries / Addons` 节添加：

```markdown
- **EasySave 3** (Moodkie, Asset Store) — 存档与持久化系统（F5）。
  用于 PlayerInfoSO 序列化（功勋、武器解锁、设置、延迟校准偏移量）。
  **约束**: 禁止直接序列化 MonoBehaviour 引用；只序列化纯数据结构（struct / class，无 Unity Object 引用）。
  ES3 的云备份功能（ES3Cloud）本项目不启用。
  ADR D6 将正式选定 EasySave 3 vs 自定义 JSON 方案。
```

**时间**: 10 分钟
- [x] `technical-preferences.md` 已添加 EasySave 3 条目

---

### 1e. 添加 DOTween 到 Allowed Libraries + 创建插件文档

**问题**: DOTween 未在 Allowed Libraries 注册，无插件文档。
UI 动画（心法选择弹出、结算界面过渡）和节拍驱动视效（HUD 脉冲）如果使用 DOTween，
在无文档约束下可能与 Feel/MMFeedbacks 的反馈序列产生双重驱动冲突（两个系统同时控制同一 Transform）。

**修复 — Allowed Libraries**:
在 `technical-preferences.md` 添加：

```markdown
- **DOTween** (Demigiant, Asset Store — Pro) — UI 动画与补间工具。
  **用途范围**: UI 过渡动画（非节奏关键帧动画）。节奏反馈动画统一使用 Feel/MMFeedbacks，
  不得与 DOTween 在同一 Transform 上双重驱动。
  **约束**: 游戏启动时须调用 `DOTween.Init()` 并设置容量，避免运行时 GC（见 DOTween 最佳实践）。
  禁止在 Update/FixedUpdate 中每帧创建新 Tween（应复用或 Sequence 管理）。
```

**修复 — 插件文档**:
创建 `docs/engine-reference/unity/plugins/dotween.md`，包含版本号、
与 Unity 6.3 LTS 兼容状态、DOTween Pro vs Free 区别、与 Feel 的边界划分规则。

**时间**: 20 分钟
- [x] `technical-preferences.md` 已添加 DOTween 条目
- [x] `docs/engine-reference/unity/plugins/dotween.md` 已创建

---

### 1f. 添加 Hot Reload 到 Allowed Libraries + 创建插件文档

**问题**: Hot Reload 未在 Allowed Libraries 注册，无插件文档。Hot Reload
是开发期专用工具（仅在 Editor 中生效），但如果未明确记录其 Editor-only 约束，
新协作者可能错误地认为它影响 Runtime 行为，或不知道其与 Unity 6 的兼容限制。

**修复 — Allowed Libraries**:
在 `technical-preferences.md` 添加：

```markdown
- **Hot Reload** (CodeFloof, Asset Store) — C# 代码热重载（Editor 开发期提速工具）。
  **仅限开发期 Editor 使用**，不影响任何 Runtime 行为，不打包进构建产物。
  **约束**: 不支持 DOTS/Burst 代码热重载；静态字段在热重载后须手动重置（运行时单例须注意）。
  与 Odin Inspector 同时使用时须注意序列化检视面板刷新时机。
  CI 构建和 Player 构建中完全忽略此插件。
```

**修复 — 插件文档**:
创建 `docs/engine-reference/unity/plugins/hot-reload.md`，包含版本号、Editor-only 明确声明、
已知不支持场景和 Unity 6.3 LTS 兼容状态。

**时间**: 15 分钟
- [x] `technical-preferences.md` 已添加 Hot Reload 条目
- [x] `docs/engine-reference/unity/plugins/hot-reload.md` 已创建

---

### 1g. 创建控制清单（来自首次计划，仍待完成）

**问题**: `docs/architecture/control-manifest.md` 缺失。`/create-stories` 生成 Story 时会将
清单版本号嵌入 Story 头部；`/story-done` 会检查版本是否过时。无清单时两项安全检查静默失效。

**前置条件**: 先完成 1a–1f（确保 Allowed Libraries 完整），再运行此命令。

**修复**:
```
/create-control-manifest
```

**时间**: 30 分钟
- [ ] `docs/architecture/control-manifest.md` 已创建
- [ ] 清单包含 `Manifest Version:` 日期戳

---

## Step 2: 修复 MEDIUM 差距

### 2a. 创建 Sprint 跟踪文件（来自首次计划，仍待完成）

**问题**: `production/sprint-status.yaml` 缺失，`/sprint-status` 降级为 markdown 格式。

**修复**:
```
/sprint-plan update
```

**时间**: 5–15 分钟
- [ ] `production/sprint-status.yaml` 已创建

---

### 2b. 架构可追溯矩阵（Step 3 自动生成）

`docs/architecture/architecture-traceability.md` 将由 `/architecture-review` 自动生成，
无需手动操作。完成 Step 3b 后打勾。

- [ ] ← 此项在 Step 3b 完成后打勾

---

## Step 3: Bootstrap 基础设施（有序执行）

> 按此顺序执行，每一步依赖上一步的产出。

### 3a. 创建首批 ADR（D1–D8）

game-concept.md 和 High-Risk Systems 已识别 8 个架构决策，须在对应 GDD 前完成：

| ID | 决策 | 前置于 |
|----|------|--------|
| D1 | 音频中间件（FMOD vs Unity Audio DSP） | F2 GDD 之前 |
| D2 | 敌人 AI 技术选型（Unity Behavior vs 自研 BT） | FE1 GDD 之前 |
| D3 | TDE 集成边界合约 | F6 GDD 之前 |
| D4 | Animancer 动画层与 TDE 冲突解决方案 | F6 GDD 之前 |
| D5 | ScriptableObject 数据层 schema 冻结策略 | F4 GDD 之前 |
| D6 | 存档/持久化方案（EasySave 3 vs 自定义 JSON） | F5 GDD 之前 |
| D7 | 节奏量化模型（M0 Spike 后产出） | C1 GDD 之前 |
| D8 | 程序化房间生成方案（BSP vs WFC）— Alpha+ 可推迟 | FE6 GDD 之前 |

```
/architecture-decision   ← 引导写 D1
/architecture-decision   ← 写 D2
…（依此类推）
```

**时间**: 每个 ADR 约 20–30 分钟
- [ ] D1（FMOD vs Unity Audio）Status: Accepted
- [ ] D2（AI 技术选型）Status: Accepted
- [ ] D3（TDE 集成边界）Status: Accepted
- [ ] D4（Animancer 层冲突）Status: Accepted
- [ ] D5（SO schema 冻结策略）Status: Accepted
- [ ] D6（存档方案）Status: Accepted
- [ ] D7（量化模型 — M0 Spike 后）Status: Accepted
- [ ] D8（程序化生成 — Alpha+ 可推迟）

---

### 3b. 生成 TR 注册表 + 架构可追溯矩阵

`tr-registry.yaml` 已存在（scaffold，无实际条目）。运行后读取所有 ADR 和 GDD，
追加稳定 TR-ID，并生成架构可追溯矩阵。

```
/architecture-review
```

**时间**: 1 次 session（首次运行较长）
- [ ] `tr-registry.yaml` 已有实际 TR-ID 条目
- [ ] `docs/architecture/architecture-traceability.md` 已生成（解决 Step 2b）

---

### 3c. 确认阶段文件

`production/stage.txt` 当前为 `Concept`，符合实际。所有 MVP GDD 完成后：

```
/gate-check pre-production
```

通过后阶段文件将自动更新为 `Pre-Production`。

- [ ] 所有 MVP GDD 完成后运行 `/gate-check pre-production`

---

## Step 4: 已完成项（首次迁移计划存档）

以下各项已在首次运行（2026-07-13）中完成，无需重复操作：

- [x] **1a**: TopDownEngine、Animancer、Odin Inspector、Odin Validator 注册到 Allowed Libraries
- [x] **1b**: `plugins/top-down-engine.md`、`plugins/animancer.md`、`plugins/odin.md` 已创建
- [x] **1c**: `docs/engine-reference/unity/plugins/easysave-3.md` 插件文档已创建（标题须修复 — 见 Step 1b）
- [x] **2a**: `game-concept.md` Status 字段已更新为 `**Status**: Approved`
- [x] **4a**: Forbidden Patterns 已配置（4 条禁止模式）

---

## What to Expect from Existing Stories

本项目当前无 Story 文件。运行 `/create-stories` 生成首批 Story 时，
它们将自动包含正确的 TR-ID 和清单版本号。无向后兼容问题。

---

## 当前评审模式

`production/review-mode.txt`: **lean**（Director 仅在阶段门 `/gate-check` 时介入，
跳过各技能内的逐步审查）。如需调整，编辑该文件改为 `full` 或 `solo`。

---

## Re-run

完成 Step 1 后重新运行 `/adopt`，验证所有 HIGH 差距已关闭。
