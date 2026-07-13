# CLAUDE.md - 玉兔:破阵 (Jade Rabbit: Warsong) v2

中国古风、拟人动物题材的节奏动作 Roguelike。Unity 6 LTS + URP + TopDownEngine 底座。
核心玩法：Hades 式近战连招 + 音游拍点判定（攻击踩拍获得伤害与连击加成）。

## 必读文档（docs/）

- `docs/01-PRD.md`：产品需求与功能验收标准，一切玩法实现以此为准
- `docs/02-tech-architecture-v1.md`：v1 项目考古档案；第 7 节缺陷清单是 code review 反面检查表
- `docs/03-refactor-recommendations.md`：架构决策（D1 到 D8），未记录的新技术路线不得引入
- `docs/04-rebuild-roadmap.md`：当前里程碑范围；里程碑外的想法进 backlog 不动工
- `docs/05-ai-collaboration-guide.md`：AI 工具分工与工作流
- `docs/06-system-design-v2.md`：核心系统设计（混合量化节奏、连招图、节奏化 AI、心法框架），系统实现以此为准

## 硬性规则

1. 每个子系统只有一条技术路线。引入新插件/新模式前必须先在 docs/03 中补充决策记录（ADR）并获得确认
2. 所有自定义代码在 `JadeRabbitWarsong.*` 命名空间下，按 asmdef 分层：Core / Rhythm / Combat / AI / Data / UI / SaveLoad。禁止把代码放入第三方命名空间（v1 教训）
3. 禁止修改 Assets/Plugins 与 Packages 内的第三方源码，只允许继承/事件扩展
4. 禁止用 GameObject.Find / FindWithTag 获取系统引用；用 Inspector 引用或 Bootstrapper 注入
5. 数值一律进 ScriptableObject，代码里不写魔法数字（v1 的伤害硬编码 10 是反例）；SO 是不可变配置，运行时状态放 runner/service，禁止写回 SO 资产
6. 节奏判定统一秒制且窗口互斥（Perfect ±0.05s / Good ±0.1s），一律使用校准后时间；任何系统不得静态穿透调用节奏单例，只能订阅 BeatScheduler 或调用 JudgeService 纯函数；攻击量化行为读全局配置（AttackQuantize），不硬编码假设
7. SO schema 变更必须附 Editor 迁移脚本，同步更新全部 .asset
8. 不留裸 Debug.Log（用条件编译 Logger）；不留空实现方法与 0 行文件

## 编码规范

- 类型/方法/属性 PascalCase；局部变量 camelCase；私有字段 `m_` 前缀；常量 `c_`；静态字段 `s_`；方法参数 `_arg` 风格延用 v1 规范
- 文件名 = 类名，一文件一顶层类（v1 的 Conductor.cs 内含 RhythmManager 是反例）
- [SerializeField] private 优先于 public 字段；TryGetComponent 优先于 GetComponent
- UI 文本一律 TextMeshPro
- 高频时序（攻击窗口、输入缓冲、拍点回调）用 UniTask + CancellationToken，不新增长生命周期协程
- 攻击动画经 Animancer 代码驱动播放（基础移动层留 TDE Animator），不新建 Animator Controller 连招状态机
- 单元测试：判定窗边界、连招转移表完整性、存档回滚，进 Tests/EditMode

## Unity Editor 操作规则（AI 通道路由）

- 本项目 Editor 自动化主通道：UnitySkills（REST，端口 8090-8100）。UnityMCP 作为备用与特种能力通道（execute_code / unity_reflect / unity_docs / run_tests）
- 同一会话内只用一个通道修改场景；另一通道只读
- 操作场景前先感知（场景摘要/find_gameobjects），不凭记忆猜层级
- 编译闭环强制：改任何 .cs → 刷新 → 等 Domain Reload 完成 → 读 Console 确认零错误 → 才能引用新类型或进 Play 模式
- 破坏性操作（删除资产/批量改场景）先 dryRun 或要求 diff 回执
- Play 模式验收：进入 → 冒烟 → 截图 → 退出，不要留在 Play 模式里改场景

## 提交规范

Conventional Commits：`feat(combat): ...` / `fix(rhythm): ...` / `refactor(ai): ...` / `chore` / `test` / `docs`。
一个功能一个提交；提交前测试全绿 + Console 无错误。未经明确要求不执行 git push。

## 目录结构（目标）

```
Assets/
├── _Project/                  全部自定义内容收拢在此，与插件隔离
│   ├── Scripts/
│   │   ├── Core/              Singleton、Bootstrapper、事件总线、Logger
│   │   ├── Rhythm/            Conductor、TrackSO(TempoMap)、BeatScheduler、JudgeService、校准
│   │   ├── Combat/            ComboRunner、连招图、Hitbox、CombatDirector（TDE 桥接独立放 TDEBridge/）
│   │   ├── AI/                敌人与 Boss 行为（黑板桥接）
│   │   ├── Data/              全部 SO 定义（AttackNodeSO/ComboGraphSO/EnemyInfoSO/TrackSO/PerkSO）
│   │   ├── UI/                HUD、节奏指示器、菜单
│   │   └── SaveLoad/          存档
│   ├── Data/                  .asset 数据实例（按 Weapons/Enemies/Perks/Tracks 分子目录）
│   ├── Prefabs/  Scenes/  Art/  Audio/  Tests/
├── Plugins/                   第三方（TopDownEngine 等），只读
```

## 当前状态

<!-- 每个里程碑更新此节：当前处于 M_，本里程碑出口条件见 docs/04 -->
当前里程碑：M0 工程奠基。
