# Adoption Plan

> **Generated**: 2026-07-13
> **Project phase**: Concept
> **Engine**: Unity 6.3 LTS (6000.3.19f1)
> **Third-party plugins declared**: TopDownEngine (TDE), Animancer, Odin Inspector, Odin Validator
> **Template version**: v1.0+

Work through these steps in order. Check off each item as you complete it.
Re-run `/adopt` anytime to check remaining gaps.

---

## Step 1: Fix High-Priority Gaps

### 1a. 注册第三方插件到 Allowed Libraries

**问题**: `technical-preferences.md` 的 Allowed Libraries 字段为 "None configured yet"，
但 systems-index.md 已将 TopDownEngine 和 Animancer 定义为 F6（TDE & Animancer 集成层）的
核心基础，且用户已声明 Odin Inspector/Validator 为批准依赖。
未注册时，`/architecture-decision` 的 Engine Compatibility 检查对这些插件的版本约束和 API
冲突完全盲视，产出的 ADR 可能建议与 TDE 冲突的架构模式。

**修复**: 编辑 `.claude/docs/technical-preferences.md`，在 `## Allowed Libraries / Addons` 节
添加以下内容：

```markdown
## Allowed Libraries / Addons

- **TopDownEngine** (More Mountains) — 移动、生命系统、关卡管理、MMFeedbacks/Feel 反馈基础设施。
  F6 集成层边界：TDE 负责移动/生命/关卡/反馈；CombatCore 程序集负责节奏量化与连招逻辑。
  禁止修改 TDE 源码，只用继承/事件扩展。
- **Animancer** (Kybernetik) — 攻击动画层管理，适配 BPM 的动画变速。
  TDE 原生 Animator 层与 Animancer 攻击层不可冲突（见 F6 GDD 接口合约）。
- **Odin Inspector** (Sirenix) — 编辑器检视面板增强，ScriptableObject 可视化编辑。
  仅限 Editor 使用，禁止在 Runtime 代码中引用 Odin 命名空间（打包时剥离）。
- **Odin Validator** (Sirenix) — 项目资源验证，确保 SO 数据完整性。
  仅限 Editor 使用，同上。
```

**时间**: 10 分钟
- [x] `technical-preferences.md` Allowed Libraries 节已填写（2026-07-13）

---

### 1b. 创建第三方插件文档页

**问题**: `docs/engine-reference/unity/PLUGINS.md` 仅收录 Unity 官方包，无 Asset Store 插件。
写 ADR 时 Engine Compatibility 节需要准确的版本信息和已知约束；缺少文档时会写出对 TDE 内部
架构一无所知的 ADR，后期修改成本极高（v1 教训：582 行覆写陷阱）。

**修复**: 在 `docs/engine-reference/unity/plugins/` 下创建三个文件：

```
docs/engine-reference/unity/plugins/top-down-engine.md
docs/engine-reference/unity/plugins/animancer.md
docs/engine-reference/unity/plugins/odin.md
```

每个文件至少包含：版本号、与 Unity 6.3 LTS 的兼容状态、已知约束/禁止事项、
与本项目其他插件的交互边界。运行 `/architecture-decision` 时引擎专家会读取这些文件。

快捷命令（运行后手动填写版本和版本日期）：

```bash
# 先在 Unity Package Manager / Asset Store 中确认已安装版本号，再填入下面的文件
```

**时间**: 30-45 分钟（3 个文件）
- [x] `plugins/top-down-engine.md` 已创建（版本号待填写 — 从 Unity Package Manager 确认后补充）
- [x] `plugins/animancer.md` 已创建（版本号待填写）
- [x] `plugins/odin.md` 已创建（Odin Inspector + Validator 合并，版本号待填写）

---

### 1c. 创建控制清单

**问题**: `docs/architecture/control-manifest.md` 缺失。
`/create-stories` 在生成 Story 时会将清单版本号嵌入 Story 头部；
`/story-done` 会检查该版本是否过时。无清单时这两个安全检查静默失效。

**修复**: 运行：
```
/create-control-manifest
```

此命令会读取 technical-preferences.md 和已有 GDD，生成包含每层
Required / Forbidden / Guardrails 规则的平铺规则表，并写入
`docs/architecture/control-manifest.md`。

> **前置条件**: 先完成 Step 1a（注册 Allowed Libraries），
> 确保清单的 Allowed 节包含 TDE/Animancer/Odin 约束。

**时间**: 30 分钟（含引导流程）
- [ ] `docs/architecture/control-manifest.md` 已创建
- [ ] 清单包含 `Manifest Version:` 日期戳

---

## Step 2: Fix Medium-Priority Gaps

### 2a. 修正 game-concept.md Status 字段

**问题**: `game-concept.md` 头部为 `*Status: Vision Locked — Pending M0 Spike*`，
而 systems-index.md 中 F1 标注为 **Approved**。有效模板值为：
`In Design / Designed / In Review / Approved / Needs Revision`。
不一致会让状态过滤工具（如 `/review-all-gdds`）误判 F1 状态。

**修复**: 编辑 `design/gdd/game-concept.md` 第 4 行，将：
```
*Status: Vision Locked — Pending M0 Spike*
```
改为：
```
**Status**: Approved
```
并在下方保留原 Vision Locked 描述作为注释（可选）。

**时间**: 5 分钟
- [x] `game-concept.md` Status 字段已更新为 `**Status**: Approved`（2026-07-13）

---

### 2b. 创建 Sprint 跟踪文件

**问题**: `production/sprint-status.yaml` 缺失。`/sprint-status` 技能只能
回退到 markdown 格式，无法解析结构化数据，看板视图降级。

**修复**: 运行：
```
/sprint-plan update
```

若尚无 Sprint 计划 markdown，可先运行 `/sprint-plan` 启动引导流程。

**时间**: 5-15 分钟
- [ ] `production/sprint-status.yaml` 已创建

---

### 2c. 创建架构可追溯矩阵

**问题**: `docs/architecture/architecture-traceability.md` 缺失。
系统需求到实现的映射没有持久化；每次 `/architecture-review` 都要重建。

**修复**: 此文件由 `/architecture-review` 自动生成（见 Step 3）。
完成 Step 3 后自动解决，无需手动操作。

**时间**: 自动（Step 3 产出）
- [ ] ← 此项在 Step 3 完成后打勾

---

## Step 3: Bootstrap Infrastructure（有序执行）

> 按此顺序执行，每一步依赖上一步的产出。

### 3a. 创建首批 ADR（D1-D8）

**背景**: game-concept.md 的 Next Steps 和 High-Risk Systems 已识别 8 个必须决策：

| ID | 决策 | 依赖 GDD |
|----|------|---------|
| D1 | 音频中间件（FMOD vs Unity Audio DSP） | F2 之前 |
| D2 | 敌人 AI 技术选型（Unity Behavior vs 自研 BT） | FE1 之前 |
| D3 | TDE 集成边界合约 | F6 之前 |
| D4 | Animancer 动画层与 TDE 冲突解决方案 | F6 之前 |
| D5 | ScriptableObject 数据层 schema 冻结策略 | F4 之前 |
| D6 | 存档/持久化方案（PlayerPrefs vs 自定义 JSON） | F5 之前 |
| D7 | 节奏量化模型（M0 Spike 后产出） | C1 之前 |
| D8 | 程序化房间生成方案（BSP vs WFC） | FE6 之前，Alpha+ |

运行：
```
/architecture-decision   ← 引导写 D1
/architecture-decision   ← 写 D2
...（依此类推）
```

**时间**: 每个 ADR 约 20-30 分钟
- [ ] D1（FMOD vs Unity Audio）ADR 已创建，Status: Accepted
- [ ] D2（AI 技术选型）ADR 已创建，Status: Accepted
- [ ] D3（TDE 集成边界）ADR 已创建，Status: Accepted
- [ ] D4（Animancer 层冲突解决）ADR 已创建，Status: Accepted
- [ ] D5（SO schema 冻结策略）ADR 已创建，Status: Accepted
- [ ] D6（存档方案）ADR 已创建，Status: Accepted
- [ ] D7（量化模型——M0 Spike 后）ADR 已创建，Status: Accepted
- [ ] D8（程序化生成——Alpha+，可推迟）ADR 已创建

---

### 3b. 生成 TR 注册表 + 架构可追溯矩阵

`tr-registry.yaml` 已存在（scaffold），但无实际条目。
运行 `/architecture-review` 后，它会读取所有 ADR 和 GDD，
向注册表追加稳定的 TR-ID，并生成架构可追溯矩阵。

```
/architecture-review
```

**时间**: 1 次 session（首次运行较长，因要处理所有文档）
- [ ] `tr-registry.yaml` 已有实际 TR-ID 条目
- [ ] `docs/architecture/architecture-traceability.md` 已生成（解决 Step 2c）

---

### 3c. Sprint 状态文件（如未在 Step 2b 完成）

```
/sprint-plan update
```

- [ ] `production/sprint-status.yaml` 已存在

---

### 3d. 确认阶段文件

`production/stage.txt` 当前为 `Concept`，符合实际情况。
当所有 MVP GDD 完成后，运行：
```
/gate-check pre-production
```
通过后阶段文件将自动更新为 `Pre-Production`。

- [ ] GDD 完成时运行 `/gate-check pre-production` 更新 stage.txt

---

## Step 4: Optional Improvements

### 4a. 配置 Forbidden Patterns

**问题**: `technical-preferences.md` 的 Forbidden Patterns 为 "None configured yet"。
Concept 阶段此项为空属正常状态；在 Pre-Production 写第一批 GDD 之后，
应将以下已知禁止事项添加进去（来自 game-concept.md 和 systems-index.md）：

建议添加：
- 禁止修改 TDE 源码（只用继承/事件扩展）
- 禁止在 Runtime 代码中引用 Odin 命名空间
- 禁止用 Update 轮询做节拍判定（必须用 Input System callback 时间戳）
- 禁止攻击动画绑死帧数（须通过 Animancer 适配 BPM 缩放）

**时间**: 10 分钟（完成 F6 GDD 后填写更准确）
- [x] Forbidden Patterns 已在 technical-preferences.md 配置（4 条，2026-07-13）

---

## What to Expect from Existing Stories

本项目当前无 Story 文件。当运行 `/create-stories` 生成首批 Story 时，
它们将自动包含正确的 TR-ID 和清单版本号。无向后兼容问题。

---

## Re-run

完成 Step 1 和 Step 3 后，运行 `/adopt` 重新检查剩余差距。
新运行将反映当前项目状态。

---

## 当前评审模式

`production/review-mode.txt`: **lean**（Director 仅在阶段门 `/gate-check` 时介入，
跳过各技能内的逐步审查）。如需调整，编辑该文件改为 `full` 或 `solo`。
