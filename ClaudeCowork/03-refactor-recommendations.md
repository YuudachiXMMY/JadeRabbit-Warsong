# v2 重构建议与技术选型

> 用途：针对 v1 暴露的问题，给出 v2 重启时的架构决策建议。每条为「决策点 D#：建议 + 理由 + 备选」。
> 原则：每个子系统只保留一条技术路线；未做决策的系统不动工。
> 日期：2026-07-13

---

## D1 角色与战斗底座：TopDownEngine 还是自研 FSM

建议：二选一，且建议选 TopDownEngine 为底座，但只承担移动/生命/关卡/反馈等外围；战斗连招层独立自建 CombatCore（设计见 docs/06 第 3 节），不做深度 TDE 武器覆写。

理由：
- v1 最后阶段（Far Asian 场景）已实际转向 TDE，说明你自己已用脚投票
- TDE 提供了 v1 自研路线缺失的大量周边：Health/DamageOnTouch、关卡管理、对象池、MMFeedbacks 反馈、成就、房间机制，重启时能省 2 到 3 个月
- v1 自研 FSM 真正有价值的部分是「SO 驱动连招参数 + 轻重交替连段」的设计意图；但 v2 不再走 CharacterHandleWeapon/ComboWeapon 覆写路线（v1 的 582 行覆写证明此路阻力大），连招层与 TDE 平行共存，边界见 docs/06 第 3.5 节

代价与对策：
- TDE 学习成本与「框架绑架」风险：将自定义扩展严格放进自己的命名空间与 asmdef（见 D7），禁止修改 TDE 源码，只用继承/事件扩展
- 若后期确认 TDE 阻碍节奏玩法（如输入缓冲精度不足），再评估抽离，届时连招数据层（D4）不受影响

备选：纯自研（完全控制，但需重建 TDE 提供的一切；4 人团队 12 个月周期下不建议）。

## D2 敌人 AI：收敛为一条路线

建议：普通敌人用 Unity 官方 Behavior 包（com.unity.behavior，可视化行为图，Unity 6 原生）或延用 v1 自研行为树；Boss 也走同一套。三选一后删除其余。

理由：
- v1 三代 AI 并存是最大的技术债来源。自研 BT 框架质量尚可（Selector/Sequence/Parallel/黑板齐全）且你完全理解它，是可回收资产
- Behavior Designer 在 v1 零使用，且为非正版来源；除非 v2 购入正版并确有可视化编辑刚需，否则不引入
- Unity 官方 Behavior 包免费、带可视化编辑器、与 Unity 6 集成好，是自研 BT 的现代替代，适合策划参与调树

强制要求：AI 不得直接静态调用 RhythmManager（见 D5），节拍信息通过黑板变量或事件注入。

## D3 插件取舍与正版化清单

v2 新工程从空白开始，插件按需引入，全部走正版渠道（Unity Asset Store / 官网）：

| 插件 | v2 建议 | 说明 |
|---|---|---|
| TopDownEngine | 保留（购正版） | 底座，见 D1 |
| Hot Reload | 保留（按席位订阅） | 开发效率明显 |
| DOTween（免费版够用则免费版） | 保留 | UI/演出补间；v1 前端规范已指定 |
| Odin Inspector | 可选保留 | 若 v2 需要序列化字典/复杂 Inspector；否则用 Unity 6 原生 + 自写 PropertyDrawer |
| Pixel Crushers Dialogue System | 推迟到 F6 叙事里程碑再购入 | v1 零接线证明现阶段用不上 |
| Behavior Designer 全家桶 | 不引入 | 被 D2 方案替代 |
| Soap | 不引入 | 事件解耦用自研轻量事件总线或 ScriptableObject 事件自写（量很小） |
| Easy Save 3 | 二选一：ES3 或 v1 自研存档 | v1 自研方案已验证且可控，建议延用自研 + 把异或换成真加密 |
| Animancer Pro | 保留（购正版，核心动画方案） | v2 攻击动画改为 Animancer 代码驱动播放，支撑精确取消窗与按 BPM 变速，见 docs/06 第 3.2 节 |
| MagicaCloth2 / MotionMatching / Toon Pro 等美术向 | 内容期按需正版购入 | MVP 阶段用不上 |
| 字体（汉仪尚巍手书） | 确认商用授权 | 汉仪字体商用需授权；思源宋体开源可用 |
| BGM 大闹黑风山 | 替换 | 版权曲目，正式曲目需原创或授权，且节奏玩法需要精确 BPM 标定的定制音乐 |

## D4 连招系统数据化（回收 v1 最大教训）

建议：把「连段状态转移表」从代码搬进数据，并升级为连招图 + 取消窗模型（完整设计见 docs/06 第 3 节）。

要点：
- AttackNodeSO（招式）+ ComboGraphSO（转移边：输入 × 条件 → 下一招）；SO 一律不可变配置，运行态全部在 ComboRunner（修正 v1 把连段计数写进 SO 资产的反模式）
- v1 的 backswing/重置窗模型升级为战斗游戏标准的取消窗（Whiff/OnHit/Dash/OnBeat 各类窗口 + 输入缓冲窗）
- 动画执行改用 Animancer 直接播放 Clip，取消窗精确到 normalized time，支持量化模式下按 BPM 变速
- 一把新武器 = 1 个 ComboGraphSO + 一套 Clip，零代码；EditMode 测试保证图无死路
- 好处：策划可调、AI agent 可批量生成/校验配置（配合 UnitySkills 的 ScriptableObject 批量技能）

## D5 节奏系统解耦

建议：节奏核心整体重设计，不受 v1 实现约束（完整设计见 docs/06 第 1、2 节）。要点：

- 首要设计决策：攻击采用「量化到拍网格执行 + 按输入偏差评级」的混合模型（Hi-Fi Rush 范式），移动/闪避保持即时；M2 以三档可切换配置试玩定案
- 独立 `Rhythm` 程序集：Conductor（dspTime + PlayScheduled）+ TrackSO（TempoMap 分段 BPM/拍号）+ BeatScheduler（带提前量的拍点调度）+ JudgeService（纯函数判定）
- 输入走 Input System 事件时间戳换算 dsp 时间轴，不在 Update 轮询（消除最高 16.7ms 的帧量化误差）
- 新增 P0 硬需求：音频/输入延迟校准流程，判定一律用校准后时间
- 判定窗统一秒制且互斥（Perfect ±0.05s / Good ±0.1s），修复 v1 拍制嵌套问题
- 战斗、AI、UI 全部订阅 BeatScheduler 或调用 JudgeService，禁止 `RhythmManager.Instance.isPerfectHit()` 式静态穿透

## D6 音频中间件：FMOD 还是 Unity 原生

建议：MVP 期维持 Unity AudioSource + DSP 时钟（v1 已验证够用）；进入内容期（多曲目、动态分层音乐、拍号切换演出）再评估 FMOD。

理由：FMOD 引入构建/授权/学习成本；节奏判定核心是 DSP 时间读取，Unity 原生 `AudioSettings.dspTime` 已可做到帧率无关。v1 PRD 设想的 FMOD + Resonance Audio 属于过度前置。

## D7 工程纪律（v2 第一天就立规矩）

1. 命名空间与程序集：所有自定义代码在 `JadeRabbitWarsong.*` 命名空间下，按 `Core / Rhythm / Combat / AI / Data / UI / SaveLoad` 划 asmdef；禁止把代码放进第三方命名空间
2. 禁止 `GameObject.Find` / `FindWithTag` 获取系统引用：统一用 Inspector 引用、依赖注入入口或场景引导器（Bootstrapper）
3. 单例只保留一个实现（v1 的线程安全 Singleton<T> 可直接回收），禁止类内再写第二个 static Instance
4. SO 数据 schema 变更必须同步迁移 .asset（写 Editor 迁移脚本），避免 v1 的 schema 漂移问题
5. 日志用条件编译包装（`[Conditional("UNITY_EDITOR")]` 或自定义 Logger），不留裸 Debug.Log
6. 命名规范延用 v1 cursor 规则：类型/方法 PascalCase，私有字段 m_ 前缀，常量 c_，静态 s_，参数 _arg；文件名必须等于类名
7. 每个里程碑结束跑一次「死代码/空实现/重复代码」清理（可交给 AI agent 做，见 05 文档）
8. Conventional Commits（feat/fix/refactor/chore + scope），延用 v1 core.md 规范

## D8 v2 推荐技术栈汇总

| 层 | 选型 |
|---|---|
| 引擎 | Unity 6 LTS（6000.0.x 延用）+ URP 17 |
| 玩法底座 | TopDownEngine（正版）：移动/生命/关卡/反馈；战斗连招自建 CombatCore（docs/06） |
| 角色动画 | 基础层 Animator（TDE），攻击层 Animancer Pro 代码驱动播放 Clip（取消窗 + BPM 变速） |
| 敌人 AI | Unity Behavior 包 或 回收 v1 自研 BT（二选一） |
| 节奏核心 | 自研 Rhythm 程序集（TempoMap + BeatScheduler + JudgeService + 延迟校准） |
| 数据 | 不可变 ScriptableObject（AttackNodeSO/ComboGraphSO/EnemyInfoSO/TrackSO/PerkSO），运行态不入 SO，禁止嵌套 Dictionary 序列化 |
| 异步时序 | UniTask（攻击窗/输入缓冲/拍点回调等高频时序，替代协程） |
| 存档 | 回收 v1 自研（JSON + 备份回滚），加密升级为 AES，SerializableTypes 补完或删除 |
| 输入 | Input System（纯新版，关闭 Both 模式） |
| 相机 | Cinemachine 3 |
| 寻路 | AI Navigation（NavMesh） |
| 补间 | DOTween |
| 版本管理 | Git + Unity .gitignore + Git LFS（美术大文件） |
| CI/验证 | Unity Test Framework（EditMode 单测：判定窗/连招表/存档回滚），配合 AI agent 跑测（见 05 文档） |

## 回收清单：v1 直接带走的资产

1. 全套设计文档（02 文档第 6 节索引）
2. Singleton<T>（World/Singleton.cs）
3. 自研行为树框架（World/AIBehaviorTree/，若 D2 选自研路线）
4. 存档系统骨架（World/DataPersistence/，修复第 7 节缺陷后）
5. Conductor DSP 时钟思路（重写为 D5 结构）
6. 玩家分层 FSM 的状态划分设计（作为 TDE 扩展的行为参照）
7. 苗刀全套资产（模型/动画/连招参数数值）
8. 远东场景美术（竹/寺庙/石碑等模型，确认来源授权后）
9. Tags/Layers 表、编码规范、Conventional Commits 规范
