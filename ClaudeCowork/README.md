# 《玉兔:破阵》v2 重启文档包

基于 v1 项目（Rogue-Like-Game-URP）的完整总结与重启规划。生成日期 2026-07-13。

## 文档导航

| 文件 | 内容 | 读者 |
|---|---|---|
| 01-PRD.md | 产品需求文档 v2.0：玩法定义、功能需求（含 v1 验证状态标注）、MVP 定义、风险 | 所有人，一切实现的依据 |
| 02-tech-architecture-v1.md | v1 技术现状档案：依赖清单、代码架构详解、三代 AI 演进史、缺陷清单（12 项）、可回收资产 | 开发者，重启前必读 |
| 03-refactor-recommendations.md | 8 个架构决策（D1 到 D8）：TDE 底座、AI 收敛、插件取舍与正版化、连招数据化、节奏解耦等 | 开发者，动工前逐条确认 |
| 04-rebuild-roadmap.md | M0 到 M6 里程碑路线图，每阶段任务与出口条件 | 所有人 |
| 05-ai-collaboration-guide.md | UnitySkills / UnityMCP 能力清单与分工、AI 工作流、子代理说明、其他工具 | 开发者 |
| 06-system-design-v2.md | 核心系统推荐设计（不受 v1 约束）：混合量化节奏模型、延迟校准、连招图 + 取消窗、Animancer 动画层、节奏化 AI、心法框架 | 开发者，实现依据 |
| new-project-template/ | 可直接放入新工程的 AI 协作配置 | 新工程仓库 |

## new-project-template 安装方法

新建 Unity 工程后：

1. 把 `new-project-template/CLAUDE.md` 复制到新工程仓库根目录
2. 把 `new-project-template/claude-agents/` 里的 5 个文件复制到新工程的 `.claude/agents/` 目录（目录不存在就创建；本文档包里叫 claude-agents 只是因为导出时点开头目录受保护）
3. 把本文档包的 01 到 06 号文档复制到新工程 `docs/` 目录（CLAUDE.md 中的引用路径按 `docs/` 写好了）
4. 安装 UnitySkills（Unity 内 UPM + Skill Installer 装入 Claude Code）与 UnityMCP，详见 05 文档第 1 节
5. 在 Claude Code 中运行 `/agents` 确认 5 个子代理加载成功

## 阅读顺序建议

第一遍：01（想做什么）→ 04（怎么分步做）→ 03（关键决策）→ 06（系统怎么设计）。
动工前：02 的第 6 节（v1 可回收资产）与第 7 节（缺陷清单）+ 05（配好 AI 工作流）。

## 与 v1 仓库的关系

v1 仓库（/Users/jadynwu/UnityProjects/Rogue-Like-Game-URP）保留为只读参照，不再继续开发。文中所有 v1 代码引用均可在该仓库按路径复核。v1 自有设计文档（.cursor/rules 与 Documentation/）的索引见 02 文档第 6 节。
