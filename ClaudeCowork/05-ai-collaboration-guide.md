# AI Agents 与 Skills 协作指南（v2 项目）

> 用途：整理 v2 重启可用的 AI 工具生态，明确 UnitySkills（优先）与 UnityMCP 的分工，附推荐工作流。
> 配套：`new-project-template/` 内有可直接放入新工程的 CLAUDE.md 与 .claude/agents 配置。
> 调研日期：2026-07-13，版本号以当日为准。

---

## 1. 你当前的两大主力工具

### 1.1 UnitySkills（Besty0728/Unity-Skills，你的优先选择）

- 定位：基于 REST API 的 Unity Editor 自动化引擎，以 Claude Skills（SKILL.md 标准）分发。v2.1.x，MIT，活跃维护（最近推送 2026-07）
- 规模：约 733 个 REST 技能（51 个功能模块）+ 20 个 Advisory 咨询模块；基线 Unity 2022.3+，已验证 Unity 6
- 安装：UPM Git URL `https://github.com/Besty0728/Unity-Skills.git?path=/SkillsForUnity` → `Window > UnitySkills > Start Server`（端口 8090-8100）→ `Skill Installer` 一键装入 Claude Code（`~/.claude/skills/`）/ Cursor / Codex
- 关键机制：
  - 三级权限：Approval（逐次批准）/ Auto（默认，高危拦截）/ Bypass；JSONL 审计日志（`Library/UnitySkillsAudit.jsonl`）
  - dryRun 门禁：首次调用未知技能先 `?mode=dryRun` 校验；`?diff=1` 返回场景变更 diff
  - 批量事务：`POST /skills/batch`（≤50 步，`$ref` 跨步引用，transactional 全或无回滚）
  - 分层 schema 省 token：`/skills?brief=1` 先拿目录再按需取 schema

对本项目最有用的技能模块（按 v2 里程碑映射）：

| 里程碑 | 高价值模块 |
|---|---|
| M0 奠基 | Project（Layer/Tag/管线设置）、Package、Validation、Cleaner |
| M1 连招 | Script、ScriptableObject（批量生成连招表 SO + JSON 导入导出）、Component、Prefab、Animator |
| M2 节奏 | Timeline、Audio、UI（HUD 搭建）、Event（UnityEvent 监听）、Console/Debug |
| M3 AI/Boss | NavMesh、Physics（Layer 碰撞矩阵/射线诊断）、GameObject 批量、Smart（空间查询/自动布局） |
| M4-M5 内容 | Scene（多场景/报告导出）、Prefab 变体、Material 批量、Perception（场景摘要/健康检查） |
| 全程 | Test（跑 EditMode/PlayMode）、Workflow（会话级撤销回滚/书签）、Profiler/Optimization、Diagnose |

20 个 Advisory 模块（只读设计咨询，不执行操作）中与 v2 直接相关：architecture、patterns、scriptdesign、asmdef（程序集划分，正好支撑重构建议 D7）、performance、testability、dotween-design、adr（架构决策记录）、project-scout、scene-contracts、yaml-editing。

### 1.2 UnityMCP（CoplayDev/unity-mcp）

- 定位：MCP 协议的 Unity Editor 桥接器。v10.0.0（2026-06），MIT，约 12.4k stars，需 Python 3.10+ 与 uv 边车
- 工具约 47 个，核心分组：
  - 场景/对象：manage_scene、manage_gameobject、find_gameobjects、manage_components、manage_prefabs、manage_camera
  - 脚本：create_script、script_apply_edits（结构化 C# 编辑）、apply_text_edits、validate_script（Roslyn）、find_in_file、get_sha
  - 资产/工程：manage_asset、manage_packages、manage_build、manage_material、manage_graphics、manage_physics
  - 编辑器：manage_editor（Play 模式/Tag/Layer）、read_console、execute_menu_item、refresh_unity、batch_execute、execute_code（Editor 内任意 C#）
  - 数据与验证：manage_scriptable_object（按属性路径读写 SO）、run_tests + get_test_job、manage_profiler
  - 生成（v10 新增，BYO key）：generate_image、generate_model、import_model（Sketchfab）、import_model_file
  - 其他：manage_animation、manage_ui（UI Toolkit）、manage_shader/texture/vfx、manage_probuilder、unity_docs、unity_reflect
- 已知事项：脚本改动触发 Domain Reload，必须等编译完成（read_console / editor_state 轮询）再使用新类型；多 Unity 实例需 set_active_instance；Unity 6.3+ 与官方 AI Assistant 包有 System.Collections.Immutable 冲突记录

### 1.3 两者关系与分工建议

血缘：UnitySkills 本身是 unity-mcp 概念的重构扩展，能力域高度重叠。差异在接入方式与粒度：UnityMCP 走 MCP 协议（47 个粗粒度工具常驻上下文）；UnitySkills 走 Skills + REST（733 个细粒度技能按需加载，token 更省，带审批/审计/事务回滚）。

推荐分工（写进 CLAUDE.md 的路由规则）：

1. C# 源码的阅读/生成/重构：Claude Code 本体的 Read/Edit/Grep 直接做，这是它最强的能力，两个工具都不抢这层。UnitySkills 的 Advisory 模块作为设计规范层叠加
2. Editor 操作（场景/组件/Prefab/材质/播放模式/Console/测试）：主通道二选一。你偏好 UnitySkills 就以它为主通道；UnityMCP 作为备用通道或用其独有能力（execute_code、unity_reflect、unity_docs、Roslyn validate_script、AI 资产生成）
3. 硬性纪律：同一会话内不要让两个通道同时改场景（端口 8090-8100 与 8080 不冲突，但会互相踩状态）；改哪个 GameObject 由哪个通道改到底
4. 编译闭环（两家文档共同要求）：改脚本 → 等 Domain Reload → 读 Console 确认零错误 → 才能操作新类型
5. 验证一律走 Editor 通道：run_tests / 截图 / Console / Profiler 作为 AI 改动的自动验收

## 2. 推荐工作流模式

### 2.1 标准开发循环（单功能）

1. 计划：引用 PRD 条目 + 架构文档，让 agent 先给实现方案（不动代码）
2. 写码：Claude Code 编辑 C#，遵循 CLAUDE.md 规范
3. 编译闭环：刷新 → 等编译 → Console 零错误
4. 接线：Editor 通道挂组件/建 Prefab/配 SO 数据
5. 验证：跑相关单测 + 进 Play 模式冒烟 + 截图回执
6. 提交：Conventional Commit，一功能一提交

### 2.2 批量数据生产（连招表/心法/敌人配置）

用 UnitySkills 的 ScriptableObject 模块（JSON 导入导出 + 批量赋值）：设计表格（CSV/JSON）→ agent 校验 schema → 批量生成 SO → 单测验证数据完整性（如连招表无死路）。

### 2.3 场景理解优先

让 agent 操作场景前先跑 Perception 类技能（场景摘要/健康检查/上下文导出）或 UnityMCP 的 editor_state/find_gameobjects，禁止凭记忆猜测场景结构（v1 硬编码 Find 的教训同样适用于 AI）。

### 2.4 里程碑级质检（每个 M 末尾）

Cleaner（无用资产/丢失脚本）→ Validation（工程校验）→ Test 全量 → Profiler 快照留档 → 死代码/重复代码扫描（Claude Code 直接 grep 分析）→ 更新 known-issues。

## 3. 子代理（Subagents）建议

`new-project-template/.claude/agents/` 提供了 5 个开箱定义：

| Agent | 职责 | 关键约束 |
|---|---|---|
| gameplay-programmer | C# 玩法编码 | 遵循 asmdef/命名规范；改完必须走编译闭环 |
| unity-editor-operator | 场景/Prefab/SO/组件操作 | 只走选定的 Editor 通道；操作前先感知场景 |
| code-reviewer | 审查 diff | 以 v1 缺陷清单为反面检查表；只读 |
| game-data-designer | 数值与 SO 数据 | 只产出数据与表格，不改代码 |
| qa-runner | 测试与验收 | 跑测试/Play 冒烟/截图/Console 取证 |

用法：Claude Code 中 `/agents` 可查看；复杂任务让主会话调度（如「实现心法三选一」→ programmer 写码 → editor-operator 接线 → qa-runner 验收 → reviewer 审查）。

## 4. 其他值得关注的工具（按相关度）

| 资源 | 说明 | 来源 |
|---|---|---|
| Unity AI（官方，Open Beta） | Unity 6+ 内置 Assistant（工程感知 agent + Plan Mode + Skills）；Pro 内含，Personal $10/月 | unity.com/features/ai |
| Unity 官方 MCP Server | com.unity.ai.assistant 2.0+ 自带 MCP 桥；注意与 CoplayDev 包在 Unity 6.3 的 DLL 冲突记录 | docs.unity3d.com（unity-mcp-overview） |
| IvanMurzak/Unity-MCP | 另一主流 Unity MCP（约 3.5k stars）：任意 C# 方法一行特性变 MCP 工具 | github.com/IvanMurzak/Unity-MCP |
| CoderGamester/mcp-unity | 轻量 Unity Editor MCP（约 1.8k stars） | github.com/codergamester/mcp-unity |
| devdavv/unity-ai-workflow | AI-first Unity 6.2+ 工作流模板（rules/agents/skills/命令），子代理模式现成参考 | github.com/devdavv/unity-ai-workflow |
| Donchitos/Claude-Code-Game-Studios | 49 agents + 72 skills 的「AI 游戏工作室」编排（引擎无关，借鉴编排思路） | github.com/Donchitos/Claude-Code-Game-Studios |
| Coplay（商业版） | CoplayDev 的 Unity 内嵌 Copilot + 86 工具 MCP | coplay.dev |
| Context7 MCP | 拉取最新库文档（TDE/DOTween/Input System 版本差异查询） | 本会话已接入同类 query-docs 服务 |

## 5. 风险与注意事项

1. 不要同时启用 CoplayDev unity-mcp 与 Unity 官方 AI Assistant 包（Unity 6.3+ DLL 冲突有官方 FAQ 记录）
2. UnitySkills 新装默认 Auto 权限：个人项目可接受；若担心误操作，调回 Approval 并利用 Workflow 模块的会话回滚
3. AI 生成资产（UnityMCP v10 的 generate_model/image）产出的版权与质量都需人工把关，MVP 白盒期够用，正式资源仍走美术管线
4. 任何 agent 的场景改动要求 diff 或截图回执，养成「AI 操作必留证据」的习惯（两个工具都支持）
5. Unity 版本升级（如 6.1/6.2）前先确认两个桥接包的兼容性公告

## 来源

- https://github.com/Besty0728/Unity-Skills （README、SETUP_GUIDE、skills 目录，2026-07）
- https://github.com/CoplayDev/unity-mcp 与 https://coplaydev.github.io/unity-mcp/ （工具参考 2026-06-29、releases v10.0.0）
- 表格内各仓库/官网链接
