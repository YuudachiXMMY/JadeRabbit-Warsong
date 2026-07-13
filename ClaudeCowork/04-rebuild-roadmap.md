# v2 重建路线图

> 用途：从零重启的里程碑规划。以「验证核心假设优先、单一技术路线、每阶段有明确出口条件」为原则，修正 v1 的探索式开发。
> 周期假设：延用 v1 的 4 人团队盘子；若目前以个人 + AI agents 为主，各里程碑周期可按 1.5 到 2 倍放宽，但顺序与出口条件不变。
> 日期：2026-07-13

---

## M0 工程奠基（1 到 2 周）

目标：立好规矩再写第一行玩法代码。

任务：
1. 新建 Unity 6 LTS + URP 工程，导入正版 TopDownEngine，跑通 TDE 官方 3D demo
2. 建立目录结构（参照 v1 Documentation/project_structure.mdc 修订版）与 asmdef 划分（Core/Rhythm/Combat/AI/Data/UI/SaveLoad）
3. Git + LFS + .gitignore + Conventional Commits；CI 可选（本地 Test Runner 起步）
4. 放入本套文档 + CLAUDE.md + .claude/agents（见 new-project-template/），配置 UnityMCP 与 UnitySkills
5. 回收 v1 资产初始包：Singleton<T>、设计文档、苗刀模型与动画、Tags/Layers 表
6. 建立代理美术规范：所有 MVP 内容用白盒/代理资源

出口条件：空场景 + TDE 角色可移动；测试框架可跑；AI agent 能通过 MCP 读写工程并通过编译闭环。

## M1 战斗垂直切片：苗刀 + 连招（3 到 4 周）

目标：验证「SO 数据驱动连招」在 TDE 底座上的手感。

任务：
1. CombatCore 攻击执行层：Animancer 播放攻击 Clip + ComboRunner 执行连招图 + 输入缓冲（设计见 docs/06 第 3 节，不再覆写 TDE 武器体系）
2. AttackNodeSO / ComboGraphSO 数据结构 + 苗刀连招图数据化（X-X-X / X-Y / Y-Y，v1 数值起步；取消窗替代 backswing 模型）
3. 攻击判定（TDE DamageOnTouch 路线）、伤害读数据（修复 v1 硬编码 10 点）
4. 敌人假人（不动的靶子）+ 伤害数字/受击反馈（MMFeedbacks）
5. EditMode 单测：连招转移表完整性（任意状态 + 任意输入都有定义的结果）

出口条件：手柄与键鼠都能打完整连招，第三击破防表现成立；换一套连招数据不改代码即生效。

## M2 节奏层接入（3 到 4 周，核心假设验证点）

目标：验证「踩拍攻击是否有趣」，这是整个产品成立与否的关口。

任务：
1. Rhythm 程序集（docs/06 第 2 节）：Conductor（dspTime + PlayScheduled）+ TrackSO（TempoMap）+ BeatScheduler（lookahead 调度）+ JudgeService + 输入时间戳管线
2. 判定窗按秒制、互斥实现（Perfect ±0.05s / Good ±0.1s），返回 early/late；单测覆盖边界与 TempoMap 变速换算
3. 延迟校准流程（音频/输入分离偏移，入存档），判定使用校准后时间
4. 攻击量化接入（docs/06 第 1 节）：AttackQuantize 三档配置（Off / HalfBeat / Beat），评级驱动伤害加成与强化分支；连击系统（+2/+2/+1，超时清零）
5. 节奏 HUD：音符轨道、判定准星（含 early/late 提示）、连击数、屏幕边缘拍点脉冲
6. 定制或临时授权 1 首标定好 BPM 的曲目替换 v1 版权曲（用标定小工具打点）
7. 里程碑末：5 到 10 人外部试玩，三档量化模式 A/B，专项回答「不看 UI 只听音乐能否稳定打出 Perfect」「节奏是否真实改变出招决策」

出口条件：试玩者能理解并享受踩拍战斗；量化模式三选一定案并写入 ADR；校准流程可用。若手感不成立，停下调玩法（量化档位/判定窗/反馈强度/音乐结构），不进 M3。

## M3 敌人与首个 Boss（4 周）

目标：一场完整的、有节奏语言的 Boss 战。

任务：
1. 敌人 AI 路线定案（重构建议 D2）并实现普通敌人：巡逻/追击/近战/受击眩晕（回收 v1 AISM 的状态划分 + 传感器思路）
2. CombatDirector 战斗导演（docs/06 第 4 节）：攻击令牌 + 拍槽分配，敌人出手强制落拍
3. 乌鸦 Boss（或代理模型）：行为优先级树（回收 v1 Boss1BT 优先级设计），节拍信息经黑板注入，不直接调用节奏系统
4. 敌人攻击节奏前摇提示：前摇以拍为单位，由 BeatScheduler 预告事件驱动（音画双通道）
4. Boss 二阶段：拍号或 BPM 切换（TrackSO 段落），验证 D5 架构支撑力
5. 完美格挡原型（QTE 可推迟）

出口条件：从进房到击杀 Boss 的完整战斗循环可玩，Boss 攻击可被节奏预读。

## M4 局内循环：房间流 + 心法（4 周）

目标：把战斗串成一局 Roguelike。

任务：
1. 房间流：手工房间 + 线性拼接 + 权重抽取（combat 0.6 / event 0.3），入口/Boss 首尾固定，关键路径 5±1
2. 心法系统（docs/06 第 5.4、5.5 节）：PerkSO + IPerkEffect 组合式效果 + StatSheet/Modifier 数值层 + 房间奖励三选一 UI，首批 5 到 8 个心法（伤害/判定窗宽容度/连击保护类）
3. 死亡/通关结算：功勋折算
4. 存档系统接入（回收 v1 骨架 + AES + 修复缺陷清单相关项）

出口条件：MVP 定义（PRD 第 5 节）全部满足，一局 15 到 20 分钟可重复游玩。

## M5 局外成长 + 第二武器（3 到 4 周）

任务：
1. 功勋商店：HP/攻击/冲刺 CD 倍率升级（成本 base × 1.5^(level-1)），PlayerInfoSO 持久化
2. 长枪（Qiang）作为第二武器全量落地：验证「新武器 = 纯数据 + 动画」的扩展性目标
3. 武器解锁流程与武器选择 UI
4. 平衡性首轮：两把武器 DPS/节奏契合度对比表

出口条件：新玩家 3 局内能感知局外成长；第二把武器未改任何连招核心代码。

## M6 内容与打磨期（滚动）

按优先级滚动排入：随机地牢升级（BSP/WFC 评估）、第三武器（剑）、更多敌人与第二 Boss（拍号 5/8 阶段 + QTE）、对话叙事（此时再购入 Dialogue System）、美术正式资源替换代理资源、性能优化（60FPS 达标验证）、Steam 页面与 Demo 版本。

---

## 里程碑横切纪律（每个 M 都执行）

1. 里程碑内不新增技术路线；新想法进 backlog，里程碑评审时统一决策
2. 里程碑末执行：AI agent 死代码/重复代码扫描、测试全绿、known-issues 清单更新
3. 所有数值改动进 SO 数据，不进代码
4. v1 缺陷清单（02 文档第 7 节）作为 code review 的反面检查表

## 与 v1 十二个月计划的对应

v1 implementation_plan.mdc 的四阶段（原型 M1-2 / 核心 M3-6 / 内容 M7-9 / 调优 M10-12）大体仍适用：本路线图 M0-M2 对应其原型期，M3-M5 对应核心期，M6 覆盖内容与调优期。主要修正是把「节奏手感验证」提前为硬性关口（M2 出口），并删除了后端/联机等首发不需要的范围。
