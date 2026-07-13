# COWORK.md · 玉兔:破阵 (Jade Rabbit: Warsong) v2 · Cowork 协作说明

面向 Claude Cowork（桌面端）会话的项目协作指令。中国古风、拟人动物题材的节奏动作 Roguelike，Unity 6 LTS + URP + TopDownEngine 底座。核心玩法：Hades 式近战连招 + 音游拍点判定（攻击踩拍获得伤害与连击加成）。

与 CLAUDE.md 的关系：`CLAUDE.md` 面向 Claude Code 终端，负责 C# 实现、UnitySkills 主 Editor 通道与 git。**本文件面向 Cowork**，负责设计、规划、研究、数值与 SO 数据，并通过本会话已连接的 **UnityMCP** 做 Editor 实操。两者共享同一套硬性规则（见第 4 节），只是分工不同。

安装位置：与 `CLAUDE.md`、`.claude/agents/` 一并放在新工程仓库根目录。

## 1. 必读文档（docs/）

细节以 docs/ 与 CLAUDE.md 为准，本文件只讲 Cowork 该怎么干。

- `docs/01-PRD.md`：产品需求与功能验收标准，一切玩法与数值以此为准
- `docs/02-tech-architecture-v1.md`：v1 考古档案；第 7 节缺陷清单是评审反面检查表
- `docs/03-refactor-recommendations.md`：架构决策 D1 到 D8，未记录的新技术路线不得引入
- `docs/04-rebuild-roadmap.md`：当前里程碑范围；里程碑外的想法进 backlog 不动工
- `docs/05-ai-collaboration-guide.md`：AI 工具分工与工作流
- `docs/06-system-design-v2.md`：核心系统设计（混合量化节奏、连招图、节奏化 AI、心法框架），系统实现以此为准

## 2. Cowork 在本项目的职责

1. **设计与规划中枢**：维护 PRD、路线图、ADR（docs/03）、known-issues；把关里程碑范围，超范围想法进 backlog
2. **研究调研**：插件版本与授权、TDE / Animancer / Input System 用法、UnityMCP 与 UnitySkills 能力，用 web search + query-docs 服务，结论标来源与日期
3. **数值与 SO 数据设计**：连招表、心法、敌人、曲目，产出「人看的表格 + 机器用的 JSON」，JSON 字段名对齐 C# SO 字段
4. **规格与文档产出**：用 md / docx / xlsx / pptx / pdf 技能交付，沿用 docs/ 现有中文风格与术语
5. **Unity Editor 实操（经 UnityMCP）**：搭白盒场景、配 SO 数据、做 Prefab、跑测试、截图取证（纪律见第 5 节）
6. **评审与质检**：对照 CLAUDE.md 规则与 docs/02 第 7 节缺陷清单做设计与数据评审；里程碑末出质检报告
7. **任务与进度**：用任务清单跟踪多步工作；复现性检查可排成定时任务

## 3. 分工：Cowork vs Claude Code

| 事项 | Cowork（本环境） | Claude Code |
|---|---|---|
| C# 玩法实现 / 重构 | 出方案、伪代码、评审 | 主力编写（asmdef / 命名 / 编译闭环） |
| Unity Editor 通道 | UnityMCP（本会话已连） | UnitySkills 主通道，UnityMCP 备用 |
| 数值与 SO 数据设计 | 主力（表格 + JSON） | 接收并批量导入 |
| 文档 / PRD / ADR / 研究 | 主力 | 引用为准 |
| git 提交 / 推送 | 默认不碰 | 负责（Conventional Commits，不擅自 push） |
| 子代理编排 | 用 Cowork 的任务与 Agent 工具 | `/agents` 的 5 个子代理 |

**关键纪律**：同一时间只让一个通道改同一个 Unity 场景。在 Cowork 用 UnityMCP 动场景时，不要同时在 Claude Code 里对同一场景操作（依据 docs/05 第 1.3 节）。C# 实现优先交给 Claude Code，Cowork 可出骨架与规格但不与其重复落地。

## 4. 硬性规则（产出任何文档、数据、Editor 改动都遵守）

1. 每个子系统只有一条技术路线。引入新插件或新模式前，先在 docs/03 补充 ADR 并获确认，未决策不落地
2. 所有自定义代码在 `JadeRabbitWarsong.*` 命名空间下，按 asmdef 分层：Core / Rhythm / Combat / AI / Data / UI / SaveLoad。任何代码建议或骨架都放对层，不入第三方命名空间
3. 不修改 Assets/Plugins 与 Packages 内第三方源码，只允许继承或事件扩展
4. 不用 GameObject.Find / FindWithTag 获取系统引用，用 Inspector 引用或 Bootstrapper 注入
5. 数值一律进 ScriptableObject，代码里不写魔法数字；SO 是不可变配置，运行态放 runner / service，禁止写回 SO 资产
6. 节奏判定统一秒制且窗口互斥（Perfect ±0.05s / Good ±0.1s），一律用校准后时间；不静态穿透调用节奏单例，只订阅 BeatScheduler 或调用 JudgeService 纯函数；攻击量化读全局配置 AttackQuantize，不硬编码假设
7. SO schema 变更必附 Editor 迁移脚本，同步更新全部 .asset
8. 不留裸 Debug.Log、空实现方法与 0 行文件

## 5. Unity Editor 实操纪律（Cowork 用 UnityMCP 时）

本会话的 Editor 通道 = UnityMCP。规则对齐 unity-editor-operator 子代理与 docs/05：

1. **感知先行**：动场景前先用 find_gameobjects / manage_scene 读场景摘要，不凭记忆猜层级路径
2. **多实例**：多个 Unity 实例先 set_active_instance 或按调用传 unity_instance
3. **编译闭环（强制）**：用 create_script / script_apply_edits 改任何 .cs 后，refresh_unity → 轮询 editor_state 的 isCompiling 或 read_console 确认零错误 → 才能引用新类型或进 Play
4. **门禁**：破坏性或批量操作先 dryRun 或给 diff / 确认；批量走 batch_execute
5. **SO 数据**：用 manage_scriptable_object 按属性路径读写，字段名对齐 C#
6. **Prefab 优先**：可复用对象做成 Prefab 再实例化；对象命名遵循规范，不留「GameObject (1)」这类默认名
7. **验收取证**：run_tests + get_test_job 跑测试；manage_editor 进出 Play 冒烟并截图，不留在 Play 模式里改场景；read_console 巡检 Error / Warning。每次 Editor 改动附截图或对象清单
8. **失败处理**：调用失败不盲目重试，先读 read_console 与错误信息，报告后再定方案
9. **资源纪律**：只做白盒或代理资源；AI 生成资产（generate_model / generate_image）版权与质量需人工把关，正式资源走美术管线

## 6. 数值与 SO 数据设计约定

1. **双交付**：Markdown 表格（含设计意图）+ JSON（字段名 = 对应 SO 的 C# 字段）
2. **连招表完整性**：任意状态 × 任意输入都有定义的转移结果（含回到待机），不允许死路；产出后附转移矩阵自查
3. **基准数值（来自 docs/01，勿擅改）**：Perfect ±0.05s = 120% 伤害，Good ±0.1s = 110%；连击 Perfect/Good +2、Pass +1，2s 清零；苗刀 X-X-X（1.2×3 第三击破防）、X-Y（1.5+2.0 击退）、Y-Y（2.5×2 霸体）；心法稀有度 普通 70% / 稀有 25% / 传说 5%；局外升级成本 base × 1.5^(level-1)；Boss 阶段拍号 4/4 到 3/4 到 5/8。与 PRD 冲突的数值必须显式标注
4. **TrackSO**：BPM、首拍偏移、拍号变化点必须实际听音标定，不许拍脑袋
5. **交付后**：由 UnityMCP 批量导入为 .asset，跑数据完整性测试

## 7. 常用 Cowork 工作流（可直接说给我触发）

| 触发语 | 我会做什么 | 产出 |
|---|---|---|
| 更新路线图 / PRD / ADR | 读对应 docs/ → 增量修订，保留原风格 | 修订后的 md |
| 设计苗刀连招表 | 出 Markdown 表 + ComboGraph JSON + 转移矩阵自查 | 表 + JSON，可选 UnityMCP 生成 .asset |
| 生成心法 / 敌人 / 曲目数据 | 按 SO 字段设计 | 表 + JSON |
| 里程碑质检报告 | 编译状态 + run_tests + 截图 + 死代码 / 重复扫描 + 更新 known-issues | md 或 docx |
| 研究某插件 / 工具版本 | web search + query-docs | 结论 + 是否需新 ADR |
| 搭 M_ 白盒场景 | 感知场景 → UnityMCP 搭建 / 配 SO / 做 Prefab | 场景 + 截图取证 |
| 评审这段设计 / 数据 | 对照 CLAUDE.md 规则 + docs/02 缺陷清单 | 分级问题（BLOCKER / WARNING / SUGGESTION） |
| 把某项检查排成自动任务 | 建定时任务 | 每日 / 每周自动运行 |

## 8. 交付与风格约定

1. 文档默认中文，沿用 docs/ 现有风格与术语；范围以里程碑为界，里程碑外想法进 backlog
2. 长文档与表格用对应技能产出（docx / xlsx / pptx / pdf），完成后以文件卡片呈现
3. 默认不做 git push；破坏性或批量操作先确认
4. 研究类结论标注来源与日期（工具版本会变）

## 9. 当前状态

当前里程碑：M0 工程奠基（出口条件见 docs/04）。每进入一个新里程碑更新此节。
