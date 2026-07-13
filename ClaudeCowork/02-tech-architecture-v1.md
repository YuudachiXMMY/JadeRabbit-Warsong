# v1 技术架构与现状档案（Rogue-Like-Game-URP）

> 用途：如实记录 v1 项目的技术现状，作为 v2 重启时的参照与「考古」依据。
> 所有结论均可在 v1 仓库 `/Users/jadynwu/UnityProjects/Rogue-Like-Game-URP` 中按文中路径复核。
> 日期：2026-07-13（基于 main 分支，最后提交 2025-05-29 "start working on light-hard weapons"）

---

## 1. 项目概况

| 项 | 值 |
|---|---|
| Unity 版本 | 6000.0.58f2（Unity 6.0 LTS） |
| 渲染管线 | URP 17.0.4，3D Universal Renderer，PC/Mobile 双套 RP Asset（Assets/Settings/） |
| 输入 | Input System 1.14.2（ProjectSettings 为 Both 模式，新旧并存） |
| 寻路 | AI Navigation 2.0.9（NavMesh） |
| 相机 | Cinemachine 3.1.3 |
| 自定义脚本量 | Assets/Scripts 78 个 .cs + Assets/Scriptables 8 个 .cs |
| 当前主场景 | Assets/Scenes/Far Asian.unity（2025-05-14，TopDownEngine 底座） |
| Build 列表 | 仍全是 TopDownEngine Demo 场景，自制场景未加入（EditorBuildSettings.asset） |
| Git | main 分支，有 origin 远程，标准 Unity .gitignore |

一句话总结：处于反复原型探索阶段。同时存在两套并行角色架构（纯自研分层 FSM 与 TopDownEngine）和三代敌人 AI 实现（Observer → 状态机 → 行为树），多数已购插件未被自定义代码引用。

## 2. 架构演进史（场景考古）

场景命名直接记录了迭代路径：

| 阶段 | 场景 | 架构 |
|---|---|---|
| 一代 | [past]/past/TestScene_O_v2.unity | Observer 模式驱动（PlayerAttackSystem/AimBead） |
| 二代 | [past]/past/TestScene_AISM_v3.unity | 敌人分层状态机（AISM） |
| 三代 | [past]/TestScene/TestScene_AIBT_v1.unity | 自研行为树（AIBT，Boss1BT/Boss2BT） |
| 四代（当前） | Far Asian.unity | TopDownEngine 底座 + CharacterHandleLightHardWeapon |

关键佐证：全部 6 个 [past] 场景引用自研架构的 `Player - Singleton` 预制体；Far Asian 场景中该引用为 0，取而代之的是 TDE 的 LevelManager/TimeManager/SoundManager。即 v1 后期已在从全自研迁移到 TopDownEngine，迁移未完成即停更。

## 3. 依赖清单

### 3.1 Unity 官方包（Packages/manifest.json，节选功能包）

URP 17.0.4、Input System 1.14.2、Cinemachine 3.1.3、AI Navigation 2.0.9、Burst 1.8.24、Mathematics 1.3.2、Timeline 1.8.9、Post Processing 3.5.0、Visual Scripting 1.9.7、2D Pixel Perfect 5.0.3、2D Tilemap Extras 4.1.0、Multiplayer Center 1.0.0、UGUI 2.0.0、Test Framework 1.5.1。

### 3.2 第三方（嵌入包 + Assets/Plugins）与实际使用状态

| 插件 | 位置 | v1 实际使用状态 |
|---|---|---|
| TopDownEngine v4.3（MoreMountains） | Plugins/TopDownEngine | 深度使用：当前主场景底座，被 3 个自定义类继承扩展 |
| Animancer Pro | Packages/com.kybernetik.animancer | 已装，自定义代码未引用（动画走 Animator + OverrideController） |
| Hot Reload（SingularityGroup） | Packages/com.singularitygroup.hotreload | 开发效率工具，正常使用 |
| Behavior Designer + Movement/Tactical/Formations | Plugins/Behavior Designer* | 零使用：自定义代码 grep BehaviorDesigner 0 命中，另写了自研行为树 |
| Pixel Crushers Dialogue System | Plugins/Pixel Crushers | 零接线：无任何自定义集成代码，仅 Editor 图标 |
| Odin Inspector/Serializer（Sirenix） | Plugins/Sirenix | 无 using Sirenix；但 SO 里的嵌套 Dictionary 序列化历史上疑似依赖它 |
| Soap（Obvious.Soap，SO 事件架构） | Plugins/Soap | 零使用：解耦另写了自研 Observer |
| Easy Save 3 | Plugins/Easy Save 3 | 零使用：存档自研 JsonUtility 方案 |
| DOTween（Demigiant） | Plugins/Demigiant | 有 DOTweenSettings.asset，自定义代码未直接引用 |
| MagicaCloth2 | Plugins/MagicaCloth2 | 未接入自定义内容 |
| MotionMatching | Plugins/MotionMatching | 未接入 |
| Toon Pro / Customizable Fantasy Buildings | Plugins/ | 美术着色备用 |
| MeshProfiler | Plugins/MeshProfiler | 编辑器分析工具 |

合规提示：`Assets/unity3d.txt` 表明多数第三方资产来自资源分享站（CGSOSO），文件本身注明「若商用请务必官网购买」。v2 商用前需全部正版化，详见 03 文档 D3。

## 4. 自定义代码架构

### 4.1 目录树（Assets/Scripts）

```
Scripts/
├── ENUM.cs                          全局枚举（EWeaponID / EPlayerActions / EAttackAreaChildIndex）
├── Enemies/
│   ├── Enemy.cs                     敌人抽象基类（FSM 宿主 + 3 个 PlayerSensor 回调）
│   ├── EnemyPlayerSensor.cs         SphereCollider 触发器传感器（OnPlayerEnter/Exit 事件）
│   ├── EnemyStatus.cs               敌人血量/眩晕（与 Player.cs 逻辑重复）
│   ├── Boss1BT.cs / Boss2BT.cs      行为树版 Boss
│   ├── Boss1TaskPatrol.cs           Boss1 巡逻叶节点
│   ├── AI/                          行为树叶节点（Check×5 / Task×5）
│   ├── EnemyStateMachine/           二代 AISM：Context/Factory/BaseState + 5 状态
│   └── StateMachine/                Boss1 专用状态机（与基类大量重复）
├── Player/
│   ├── Player.cs                    玩家血量/眩晕（简版）
│   ├── PlayerInput.cs               Input System 生成代码（勿手改）
│   ├── MMTopDownEngine/CharacterHandleLightHardWeapon.cs   TDE 能力覆写（582 行）
│   └── PlayerStateMachine/          玩家分层 FSM（主力自研系统）
│       ├── PlayerStateMachine.cs    Context 巨类（417 行，继承 PlayerSubject）
│       ├── PlayerStateFactory.cs / PlayerBaseState.cs / IRootState.cs
│       └── PlayerStates/            Grounded/Jump/Fall/LightAttack/HardAttack/DashAttack（根）
│                                    + Idle/Walk/Dash（子）
├── Weapons/
│   ├── LightWeapon.cs / HardWeapon.cs    TDE Weapon 子类，两文件除类名/菜单名外完全相同
│   └── LightHarComboWeapon.cs            类名 LightHardComboWeapon（TDE ComboWeapon 子类）
├── UI/  AimBead.cs / NoteOnBeat.cs / Music/UISpawnBeat.cs   节奏 UI
└── World/
    ├── Singleton.cs                 线程安全泛型单例基类（质量较高）
    ├── GameManager.cs               总管（Find 玩家、初始化系统、转发存档）
    ├── AIBehaviorTree/              自研行为树框架（Node/Selector/Sequence/Parallel/Conditional/黑板）
    ├── ComboSystem/ComboSystem.cs   连击计数
    ├── ObserverPattern/             IObserver/Subject/PlayerAttackSystem/PlayerNarrationSystem
    ├── RhythmSystem/                Conductor.cs（内含 RhythmManager！）/AudioManager/ScoreManager/
    │                                NoteObject/AudioFadeOut
    ├── DataPersistence/             存档（Manager/FileDataHandler/GameData/IDataPersistence）
    └── Threading/WaitForThreadedTask.cs
```

另有 Assets/Scriptables 下 8 个数据类脚本：EnemyInfoSO、PlayerInfo（存档 DTO）、PlayerInfoSO、PlayerStatusSO、WeaponSO、MiaoDaoSO、QiangSO、WeaponApplyDamage。Items/、NPC/ 目录为空，Assets/Editor 无自定义编辑器代码。

### 4.2 玩家系统（自研主力）

- `PlayerStateMachine : PlayerSubject`：分层 FSM 的 Context。持有 CharacterController、Animator、TrailRenderer、PlayerInput；缓存 Animator 参数 hash；4 组字典承载轻/重/冲刺攻击逐段参数（movementMultiplier/duration/backswing/comboResetTime），Awake 时从当前武器 SO 灌入。
- 分层 FSM：PlayerBaseState（super/sub state + SwitchState/SetSubState）+ PlayerStateFactory（状态实例字典缓存）。根状态实现 IRootState.HandleGravity。攻击连段/后摇/重置全部用协程。
- 连段计数委托给武器 SO：`Ctx.CurrentWeapon.GetLightAttackComboCount(...)`。
- 设计模式：State + Factory + Hierarchical FSM + Observer（状态机本身是被观察主题，攻击状态 EnterState 时 NotifyObservers）。

### 4.3 武器数据模型

- `WeaponSO`（抽象 SO）：武器名/ID、AnimatorOverrideController、轻/重/冲刺的段数与逐段 float 数组（damages/movementMultiplier/duration/backswing/comboResetTime），抽象方法 GetLight/HardAttackComboCount。
- `MiaoDaoSO / QiangSO`：if-else 硬编码「轻重交替连段」转移表（L→L2→L3、L→H1、LL→H2 等），两者逻辑几乎相同。
- `PlayerInfoSO`（局外持久）+ `PlayerStatusSO`（局内运行态）+ `PlayerInfo`（存档 DTO，双向拷贝）。
- `WeaponApplyDamage`：OnTriggerEnter 对 EnemyStatus/Player 调 TakeStunned + TakeDamage(10)，伤害硬编码，且 GameObject.FindGameObjectWithTag("Managers") 取连击系统。

### 4.4 敌人 AI（三代并存）

一代 Observer：无独立敌人脚本，走 PlayerAttackSystem 观察者链。

二代 AISM（Enemies/EnemyStateMachine + StateMachine）：
- `Enemy` 抽象基类集中承载状态回调（EnterIdle/UpdatePatroll/EnterMelee 等），3 个 PlayerSensor（Follow/Melee/RangeAttack 半径）驱动切换，NavMeshAgent 移动，行为计时读 EnemyInfoSO.ActionTimeRandomRanges 随机区间。
- 状态机已退化为扁平（分层代码被注释），EnemyFallState 全部 throw NotImplementedException。
- Boss1 的 NewFactory 与基类逐行重复（复制粘贴）。

三代 AIBT（World/AIBehaviorTree + Enemies/AI）：
- 自研经典行为树：Node（parent/children + 冒泡黑板 GetData/SetData）、NodeState{RUNNING,SUCCESS,FAILURE}、Selector/Sequence/ParallelNode/ConditionalNode、Tree:MonoBehaviour（Update 里 Evaluate）。
- Boss1BT/Boss2BT 优先级树：眩晕 → 技能二 → 技能一 → 近战 → 追击 → 巡逻（Boss2 无巡逻）。
- 叶节点用 Physics.OverlapSphere + LayerMask(1<<3) 检测视野，目标写入黑板 "fovTarget"。
- 注意：技能触发直接调用 `RhythmManager.isPerfectHit()`，节奏系统被硬耦合进 Boss AI。

### 4.5 TopDownEngine 集成（四代，当前方向）

- `CharacterHandleLightHardWeapon : CharacterHandleWeapon`（582 行）：几乎整体覆写 TDE 武器持握能力，支持轻/重双武器 + 连招输入缓冲。
- `LightWeapon / HardWeapon : Weapon`：TDE MeleeWeapon 思路的改名副本（DamageOnTouch 判定区 + WeaponUse 协程），两文件内容除类名外相同。
- `LightHardComboWeapon : ComboWeapon`：多把武器串成连招轮转。文件名 LightHarComboWeapon.cs 与类名不一致。
- 这些类被放进 `MoreMountains.TopDownEngine` 命名空间（污染供应商命名空间）。
- 伤害走 TDE DamageOnTouch/Health，与 4.3 的 WeaponApplyDamage 自研通道并存。

### 4.6 节奏系统

- `RhythmManager : Singleton<RhythmManager>`（定义在 Conductor.cs 内，类名与文件名不符）：DSP 时钟（songPosition/songPositionInBeats/secPerBeat），判定 isPerfectHit/isGoodHit/isPassHit/isMissHit，窗口 0.12/0.30/0.45 拍且互相嵌套。
- 依赖方：ComboSystem、Boss AI（技能触发）、UISpawnBeat、NoteOnBeat、NoteObject、ScoreManager。
- `AudioManager : Singleton`（混音组 + SoundSettings 动态建源 + 静态 Hit/Miss/Lose；存在双单例缺陷，见第 7 节）。
- `ScoreManager`（static Instance）：节奏分数/血条、四级判定统计、PlayerPrefs 最高分。

### 4.7 存档系统

- 源自 Shaped by Rain Studios 开源范式：DataPersistenceManager（多档位 + 自动存档协程 + sceneLoaded 收集 IDataPersistence）、FileDataHandler（JSON + 异或混淆 + .bak 回滚）、GameData/PlayerInfo。
- SerializableTypes.cs 为 0 行空文件（未完成的可序列化类型支持）。
- FileDataHandler 内嵌 ClassB 协程运行器 hack 与未使用的 WaitForDataToLoad。

### 4.8 基础设施

- `Singleton<T>`：线程安全（Dictionary + lock + DontDestroyOnLoad），强制子类实现 OnEnableCallback/OnApplicationQuitCallback。被 6+ 个管理器继承。
- 自研 Observer：Subject（List<IObserver> + NotifyObservers(EPlayerActions)）；观察者 PlayerAttackSystem（Dictionary<EPlayerActions,Action> 分派 + 协程开关攻击判定区）、PlayerNarrationSystem（空壳）、AimBead。
- GameManager：Awake 用 FindWithTag("Player") 抓引用，IObserver.OnNotify 为空实现。

## 5. 数据资产与场景资源盘点

### 5.1 ScriptableObject 数据

| 资产 | 内容 |
|---|---|
| Scriptables/Player/PlayerInfoSO.asset | weaponList（0 苗刀 / 1 长枪）、hp/attack/dashCD 倍率、关联 PlayerStatusSO |
| Scriptables/Player/PlayerStatusSO.asset | 局内运行态 |
| Scriptables/Weapons/MiaoDaoSO.asset（+ MiaoDao.json） | 苗刀连招参数；残留已废弃的旧字典字段数据 |
| Scriptables/Weapons/QiangSO.asset | 长枪连招参数 |
| Scriptables/Enemies/Boss 1.asset | HP 100 / 伤害 5 / 生命 3 / 移速 1 + 行为随机区间字典 |

### 5.2 美术/音频（大部分目录为空，生产刚起步）

- 主角：Char_Main_Rigged_Published.fbx（Modular Animal Knights 动物骑士包），Player - Singleton.prefab、Dog.prefab
- 武器：苗刀模型/材质/动画（LightAttack×3、HardAttack×3 + OverrideController）、AttackAreas.prefab；长枪无模型
- 敌人：BaseEnemyAnimatorController + Sad Walk.fbx、Boss 数据、Waypoints 测试预制体
- 场景（远东主题）：竹 Bamboo1、寺庙 Temple、石碑 Shibei、大树 Tree_Big、兵器架 WeaponStack 等模型与材质
- UI：UICombo.prefab、UIMusic.prefab；相机组 3DCameras prefab；系统单例 prefab（GameManager/DataPersistenceManager）
- 音频：仅 1 首 BGM 大闹黑风山.mp3，SFX 目录空
- 空目录：NPC、Items、Sprites、Fonts、Shaders、Audio/SFX 等

### 5.3 Tags / Layers（ProjectSettings/TagManager.asset）

- 自定义 Tags：LevelBounds、FX、Enemy、World、NoMask（另代码里用到 Managers、Way Point、Player）
- 自定义 Layers：Water、Obstacles、Ground、Player、ObstaclesDoors、NoPathfinding、Enemies、NoCollisions、Hole、MovingPlatform、FallingPlatform、Projectile、Camera1-4（多为 TopDownEngine 约定）
- Sorting Layers：Background、Ground、Foreground、Characters、Above

## 6. v1 自有设计文档索引（重要遗产）

v1 仓库内已有一套相当完整的设计文档，v2 重启时应直接带走：

| 文档 | 路径 | 内容 |
|---|---|---|
| 原始需求 | Documentation/project-demand.md | 六大核心功能与优先级 |
| 正式 PRD v1.1 | .cursor/rules/project_requriement_document.mdc | 市场/竞品/画像/连招表/数值公式/视觉语言 |
| 技术选型 | .cursor/rules/tech_stack_document.mdc | TDE/DOTS Physics/Animancer/FMOD/BSP+WFC/Addressables 设想 |
| 应用流程 | .cursor/rules/app_flow_document.mdc | 全流程 mermaid + 节奏伪代码 + Boss 阶段设计 |
| 前端规范 | .cursor/rules/frontend_guidelines_document.mdc | UI 色彩/字体/HUD/判定反馈/手柄 |
| 后端设想 | .cursor/rules/backend_guidelines_document.mdc | 微服务愿景（与单机阶段不符，v2 缓行） |
| 实施计划 | .cursor/rules/implementation_plan.mdc | 12 个月 4 阶段 + 分工 + 风险 |
| 编码规范 | .cursor/rules/cursor_unity_project_rules.mdc + core.md | 命名法（m_/c_/s_/_arg）、AI 行为准则、Conventional Commits |
| 目录规范 | Documentation/project_structure.mdc | Assets 目标目录树 |
| AI 分工笔记 | Documentation/tree.txt | v1 时代的多模型协作策略 |

## 7. 已知缺陷清单（v2 引入任何 v1 代码前必查）

| # | 缺陷 | 位置 |
|---|---|---|
| 1 | PlayerInfoSO.SetHighestCombos 疑似无限递归（ContainsKey 分支内以相同参数自调用） | Scriptables/Player/ScriptableObjects/PlayerInfoSO.cs |
| 2 | AudioManager.OnEnableCallback/OnApplicationQuitCallback 自调用造成无限递归；且 Singleton<T> 与自建 static Instance 双单例冲突 | World/RhythmSystem/AudioManager.cs |
| 3 | EnemyFallState 全方法 throw NotImplementedException，进入即崩 | Enemies/EnemyStateMachine/EnemyStates/ |
| 4 | SO schema 漂移：WeaponSO.cs 已注释掉旧字典字段，但 MiaoDaoSO.asset/QiangSO.asset 仍残留旧数据；EnemyInfoSO 的嵌套 Dictionary 依赖非 Unity 原生序列化，反序列化失败会在 FSM/AIBT 高频索引处抛 NRE/KeyNotFound | Scriptables/ |
| 5 | 伤害硬编码 10 点，未读武器数据 | Scriptables/Weapons/WeaponApplyDamage.cs |
| 6 | DataPersistenceManager 双单例残留（注释掉的 static instance 死代码） | World/DataPersistence/ |
| 7 | 判定窗嵌套：isGoodHit 包含 Perfect 区间，语义依赖调用顺序 | World/RhythmSystem/Conductor.cs |
| 8 | 大量硬编码 Find：`GameObject.Find("Player - Singleton/AttackAreas")`、Find("ComboNumber")、Find("UIMusic")、FindWithTag("Managers") 等，场景结构强耦合 | 多处 |
| 9 | 复制粘贴重复：LightWeapon=HardWeapon、Player.cs≈EnemyStatus.cs、Boss1.NewFactory=Enemy.NewFactory、Boss1BT≈Boss2BT | 多处 |
| 10 | 命名问题：Conductor.cs 内含 RhythmManager 类、LightHarComboWeapon.cs 拼写、_fovRagne/tiggerVFX/MelleDelay 等拼写错误 | 多处 |
| 11 | 命名空间缺失：自定义代码几乎全在全局命名空间，TDE 派生类反而塞进 MoreMountains.TopDownEngine | 全局 |
| 12 | 空实现残留：GameManager.OnNotify、PlayerNarrationSystem、PlayerAttackSystem.HandleJump/HandleDashAttack、SerializableTypes.cs（0 行） | 多处 |

## 8. 代码质量总评

值得保留的资产：玩家分层 FSM 的整体设计（super/sub state + 协程连段 + SO 驱动连招参数）、自研行为树框架（结构规范可扩展）、线程安全 Singleton、存档系统骨架、以及全套设计文档。

核心问题：方案收敛不足（每个子系统都有 2 到 3 套实现共存）、已购插件与自研轮子大面积重复、硬编码查找与跨系统静态耦合、边角 Bug 与拼写混乱。属于「设计意图丰富、工程纪律不足」的原型级代码库。这也是 v2 选择从零重启而非继续迁移的合理依据。
