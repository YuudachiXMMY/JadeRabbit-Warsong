# v2 核心系统设计提案

> 用途：对核心系统给出不受 v1 实现约束的推荐设计。v1 做法只作对照，不作基准。
> 关系：03 文档记录「选什么路线」，本文档回答「这条路线具体怎么设计」。二者冲突处以本文档为准（03 已同步修订）。
> 范围：节奏核心、战斗连招、敌人节奏化 AI、架构与数据层、心法框架。
> 日期：2026-07-13

---

## 1. 首要设计决策：判定式、量化式，还是混合

这是整个游戏最上游的设计选择，决定后面所有系统的形态。

### 三种模型

| 模型 | 机制 | 代表作 | 风险 |
|---|---|---|---|
| 判定式（v1 与原 PRD 的默认假设） | 玩家任意时刻出手，按输入与最近拍点的偏差评级，给伤害加成 | 无直接代表 | 高速战斗中玩家难以主动感知 ±0.05s，容易退化成「无视节奏乱按，加成时有时无」，节奏感依赖玩家自觉，表现层永远差半拍 |
| 量化式 | 输入随时接受，动作吸附到最近的拍网格执行；动画时长按 BPM 缩放；敌人、场景、UI 全部在拍上运动 | Hi-Fi Rush、Crypt of the NecroDancer、BPM | 移动若也量化会牺牲动作手感；实现要求动画变速管线 |
| 混合（推荐） | 移动/闪避即时响应（动作手感底线不让步）；攻击吸附到半拍网格执行 + 按输入偏差评级给加成；敌人所有攻击强制落拍 | Hi-Fi Rush 实际就是这种混合（移动自由，攻击量化） | 需要早期试玩定案 |

### 推荐：混合模型

理由：
1. 判定式的核心缺陷是「节奏感不可见」：玩家乱按时画面完全不体现音乐，游戏退化成普通 ARPG 加一个隐藏伤害系数。量化让每一刀都天然踩在拍上，观感即卖点
2. 攻击吸附到半拍（不是整拍）保证最大输入延迟约为 BPM 120 下 250ms 的一半，配合输入缓冲，体感接近普通动作游戏的出招前摇
3. 评级仍然保留（按输入与拍点的偏差），PRD 的 Perfect/Good 加成体系不变，但从「唯一的节奏反馈」降级为「精进奖励」
4. 敌人攻击强制落拍后，「听音乐防御」成为真实策略，支撑 PRD 的节奏前摇需求

### 落地方式（M2 验证关口）

做成一个全局配置开关 `AttackQuantize: Off / HalfBeat / Beat`，M2 试玩时三档对比，用数据定案。整个战斗层按「量化是可配置行为」设计，不硬编码任何一种假设。

---

## 2. 节奏核心 Rhythm Core

### 2.1 时间源

- 唯一真值：`AudioSettings.dspTime` + `AudioSource.PlayScheduled` 排程播放。任何判定、调度禁止使用 `Time.time` / `Time.realtimeSinceStartup` 直接参与
- v1 的 Conductor DSP 思路正确，保留；重写为无状态可测试的结构

### 2.2 TempoMap（替代单一 BPM）

TrackSO 数据结构：

```
TrackSO
├── AudioClip
├── firstBeatOffset: double          首拍偏移（秒）
├── segments: TempoSegment[]         分段变速变拍
│   └── { startBeat, bpm, timeSignature }
└── markers: TrackMarker[]           乐句/段落标记（Boss 阶段切换点等）
```

- 提供纯函数库：`BeatTime(bar, beat, subdivision) ↔ dspTime` 双向换算，覆盖变速段边界，全部进 EditMode 单测
- PRD 的 Boss 阶段拍号 4/4 → 3/4 → 5/8 直接建模为 segments，音乐、判定、AI、UI 四方共享同一数据源，杜绝漂移

### 2.3 输入时间戳管线（修复 v1 最大精度缺陷）

v1 在 Update 里轮询判定，60FPS 下帧量化误差最高 16.7ms，占 Perfect 窗（±50ms）的三分之一。v2：

- 使用 Input System action callback 的 `context.time`（事件级时间戳，精度高于帧）
- 每帧采样一对 `(realtimeSinceStartupAsDouble, dspTime)`，滑动平均估计两条时钟的映射关系，把输入事件时间换算到 dsp 时间轴
- 判定入口只接受 dsp 时间戳：`Judge(inputDspTime)`

### 2.4 延迟校准（新增 P0 需求）

无线耳机音频延迟可达 100 到 300ms，不校准则判定体系不成立。这是 v1 与原 PRD 都遗漏的发布级硬需求：

- 首次进入游戏 + 设置页提供校准流程：节拍器响 N 拍，玩家跟拍按键，取中位数估计总延迟
- 音频偏移（听到的比 dsp 晚多少）与输入偏移（按下到系统收到）分开存储，判定用校准后时间
- 校准值入存档，按输出设备变化提示重校

### 2.5 判定服务 JudgeService

- 纯静态函数：`Judge(inputDspTime, tempoMap, calibration) → (grade, signedOffsetMs)`
- 窗口互斥不嵌套（修复 v1）：|offset| ≤ 50ms 为 Perfect，50 到 100ms 为 Good，其余 Miss。数值来自全局 SO 配置，可被心法修改（如「判定窗宽容 +20ms」的心法）
- 返回带符号偏差（early/late），供 UI 显示「早了/晚了」，这是节奏游戏教学的标配

### 2.6 BeatScheduler（带提前量的拍点调度器）

v1 的问题：拍到了才发事件，下游永远在追赶。v2 用 lookahead 模式：

- 调度器持续维护未来 1 到 2 拍的预告：「第 N 拍将发生在 dspTime T」
- 下游 API：
  - `OnBeat / OnBar / OnMarker` 事件（带预告时间，UI 可以做「滚动到位」而非「事后抖动」）
  - `ScheduleAt(beat, callback)`：量化执行的底层（攻击吸附、敌人出手都走这里）
  - `NextQuantizedBeat(inputDspTime, grid)`：输入 → 最近的网格拍
- 世界脉冲：场景呼吸、UI 缩放等表现统一订阅 OnBeat，用预告时间做提前量动画

### 2.7 谱面与工具

- 本作不是音游，不需要逐音符谱面。TempoMap + markers 足够
- 做一个极小的 Editor 标定工具：播放曲目、空格打拍、拟合 BPM 与首拍偏移、可视化波形核对
- 买 vs 造：Koreographer（Asset Store 音乐同步事件的事实标准）可替代自研标定与事件授权；但本方案的 TempoMap 很小且判定/量化逻辑无论如何要自写，建议自研，Koreographer 列为工具链备选

---

## 3. 战斗与连招系统

### 3.1 v1 模型的结构性问题

1. 连段转移用 if-else 写在 SO 子类里（每把武器一份复制粘贴的代码）
2. 逐段参数存成四组平行数组字典，人不可读，改一段要对齐四处
3. 连段计数等运行时状态写进 SO 资产（SO 是资产，Editor 下状态会残留污染）
4. Animator Controller + 每武器一个 OverrideController：时序控制粒度粗，连招状态机在控制器里越长越深，与代码里的 FSM 双头维护

### 3.2 动画执行：Animancer 代码驱动（替代 Animator Controller 路线）

用已有的 Animancer Pro 直接播放 AnimationClip：

- 攻击数据直接引用 Clip，没有控制器状态机可漂移，新武器零控制器工作
- 窗口事件精确到 normalized time：命中窗开/关、取消窗开/关都是数据里的时间区间，运行时按当前 Clip 实际时长换算
- 动态变速：量化模式下，攻击动画按当前 BPM 缩放时长（Hi-Fi Rush 的核心技术），Animancer 的 `State.Speed` 一行搞定，Animator Controller 路线做这件事非常痛苦
- 移动/待机等基础层可继续留在 TDE 的 Animator 里，攻击层用 Animancer Layer 覆盖，两层互不干扰

### 3.3 数据模型：连招图（不可变 SO）

```
AttackNodeSO（一招）
├── clip: AnimationClip
├── damageCoef: float                  伤害系数（乘武器基础值与评级加成）
├── motion: 位移曲线 / root motion 开关
├── armor: 霸体帧区间 / iframe 区间
├── hitWindow: [start, end]            命中判定窗（normalized time）
├── cancelWindows:                     取消窗列表（战斗游戏标准模型，替代 v1 的 backswing）
│   └── { type: Whiff | OnHit | Dash | OnBeat, range: [start, end] }
└── inputBuffer: float                 此招期间接受缓冲输入的窗口

ComboGraphSO（一把武器的连招图）
├── entryTable: { 输入 → 起手 AttackNode }
└── edges: ComboEdge[]
    └── { from, input: Light|Hard|Dash, condition, to }
        condition 可选：requiresGrade ≥ Good / requiresOnBeat / 资源足够 等
```

- 运行态全部在 `ComboRunner`（挂角色上的执行器）：当前节点、缓冲队列、窗口计时。SO 一律只读
- 节奏与连招的结构化耦合点：edge 的 condition 支持评级条件。推荐设计：同一输入配两条边，Perfect 时走「强化版下一段」（更高伤害/带特效的变体），Miss 时走普通版。节奏收益从纯数字（120%）升级为肉眼可见的招式分支，这比 v1/原 PRD 的纯伤害系数反馈强得多
- 完整性由 EditMode 测试保证：遍历图无死路、无不可达节点、每个节点的 cancelWindows 不早于 hitWindow 开启、buffer 窗不长于本招时长

### 3.4 输入缓冲与量化的配合

1. 输入到达 → 打上 dsp 时间戳 → 立即判定评级（用原始输入时间，保证评级反映玩家真实操作）
2. 若在取消窗内：按量化配置吸附到 `NextQuantizedBeat`，由 BeatScheduler 在该拍执行转移
3. 若在缓冲窗内但不在取消窗：入缓冲队列，取消窗开启时按上述流程执行
4. 缓冲队列深度 1（动作游戏惯例），后到覆盖先到

### 3.5 与 TopDownEngine 的边界（修订 03/D1）

v1 用 582 行覆写 CharacterHandleWeapon 证明了「把自定义连招塞进 TDE 武器体系」阻力很大。v2 改为平行共存：

- TDE 负责：移动与物理（CharacterMovement）、Health 与伤害接收、关卡/房间/传送门、对象池、MMFeedbacks 反馈
- 自建 `CombatCore` 程序集负责：输入消费、ComboRunner、Animancer 攻击层、Hitbox 激活（可复用 TDE DamageOnTouch 作为判定载体，或自建轻量 Hitbox，二选一进 ADR）
- 不继承 CharacterHandleWeapon / ComboWeapon / Weapon；角色 Prefab 上 TDE Character 组件与 ComboRunner 并列

---

## 4. 敌人与 Boss 的节奏化 AI

行为树路线维持 03/D2（Unity Behavior 或回收自研 BT）。在其上加两个结构，让「敌人在拍上」成为系统保证而非每个 AI 自觉：

### 4.1 战斗导演 CombatDirector（攻击令牌 + 拍槽）

- 敌人 AI 决定「想攻击」，但不直接出手；向导演申请攻击令牌
- 导演按拍网格分配：令牌绑定具体的拍（第 N 拍出手），同拍最多 1 到 2 个敌人出手，控制围攻压力（Hades 的 token 思路 + 拍点化）
- AI 拿到令牌后进入前摇，前摇长度以拍为单位（如 2 拍），命中帧精确落拍

### 4.2 前摇即节奏教学

- telegraph 特效/音效由 BeatScheduler 的预告事件驱动，与音乐严格对齐
- 玩家「听到重音就闪避」成为可靠策略，实现 PRD 的节奏前摇需求
- Boss 阶段切换 = TempoMap marker 触发：换 segment（拍号/BPM）+ 行为树切子树 + 演出，一个事件源同步三方

### 4.3 与黑板的接口

行为树只读黑板变量（currentBeat、nextAttackSlot、playerLastGrade 等），由 Rhythm/Director 写入。行为树内不出现对节奏系统的直接调用，保证 AI 可以离线单测。

---

## 5. 架构与数据层

### 5.1 异步：UniTask 替代协程

v1 大量用协程管理攻击窗口/连段重置等高频短时序，存在泄漏与调试困难。v2 引入 UniTask（Cysharp，免费 OSS，事实标准）：

- 零 GC、可取消（CancellationToken 与角色生命周期绑定）、可 await dsp 时间
- 规则：高频时序（窗口、缓冲、调度回调）一律 UniTask；场景级一次性流程可留协程

### 5.2 事件：一个通道，不再手写 Observer

- 二选一并写进 ADR：轻量泛型静态事件总线 `EventBus<T>`（struct 事件，如 `BeatEvent`、`JudgeEvent`、`RoomClearedEvent`），或 SO 事件通道资产
- 推荐前者：本作事件量小、类型明确，静态总线 + struct 无装箱最简单；SO 通道的优势（Inspector 接线）在纯代码工作流下价值有限
- v1 的自研 IObserver/Subject 与 Soap 都不再引入

### 5.3 SO 纪律

- SO 一律是不可变配置；运行时状态放 runner/service；存档走独立 DTO
- 禁止嵌套 Dictionary 序列化（v1 的 schema 漂移源头）；键值需求用 `List<Entry> + 运行时字典化`

### 5.4 数值：StatSheet + Modifier（支撑心法）

- 每个可成长数值（攻击、HP、判定窗宽容、移速等）是一个 Stat：`final = (base + Σ加法) × Π乘法`
- Modifier 带来源标记（哪个心法/装备给的），可卸载、可在 UI 上归因显示
- 局外功勋升级 = 永久 Modifier；局内心法 = 局内生命周期 Modifier，结算时清空

### 5.5 心法 Perk 框架

```
PerkSO
├── 元数据：名称/描述/稀有度（普通70 / 稀有25 / 传说5）/图标
└── effects: List<IPerkEffect>（SerializeReference 多态序列化）
     ├── StatModifierEffect（改数值）
     ├── OnJudgeEffect（按评级触发：如 Perfect 回气力）
     ├── OnHitEffect / OnRoomClearEffect / OnDamagedEffect
     └── ComboGraphOverrideEffect（传说级：替换/追加连招边，兑现「改变武器特性」）
```

- 钩子事件全部来自 5.2 的事件总线，效果类不引用具体系统
- 新心法 = 组合既有效果 + 填数据；只有全新效果类型才写代码
- 三选一抽取：按稀有度权重 + 已持有去重 + 保底规则（连续 N 次无稀有则提权），规则数值全在 SO

### 5.6 程序集划分（细化 03/D7）

```
JadeRabbitWarsong.Core      事件总线、Stat、Singleton、Logger、UniTask 扩展
JadeRabbitWarsong.Rhythm    Conductor、TempoMap、JudgeService、BeatScheduler、校准
JadeRabbitWarsong.Combat    ComboRunner、AttackNodeSO/ComboGraphSO、Hitbox、CombatDirector
JadeRabbitWarsong.AI        行为树节点/黑板桥接
JadeRabbitWarsong.Data      全部 SO 定义与 DTO
JadeRabbitWarsong.UI        HUD、节奏指示器、校准界面
JadeRabbitWarsong.SaveLoad  存档
```

依赖方向单向向下：UI/AI/Combat → Rhythm → Core；Data 被所有层引用；禁止反向。Rhythm 与 Combat 不引用 TDE（TDE 桥接放独立的 `JadeRabbitWarsong.TDEBridge`），保证核心系统可脱离 TDE 单测。

---

## 6. 对路线图与验收的影响

- M1：按 3.2/3.3 实现攻击执行与连招图（不再重写 CharacterHandleWeapon）
- M2：按第 1、2 节实现节奏核心，新增两项出口条件：延迟校准流程可用；量化模式三档试玩定案
- M3：按第 4 节实现 CombatDirector 与拍点令牌
- 测试基线新增：TempoMap 换算、判定窗互斥边界、连招图完整性、量化吸附正确性（输入时间 → 执行拍）
- 04 路线图与 CLAUDE.md 已同步修订

## 7. 参考对象

- Hi-Fi Rush：混合量化模型、动画 BPM 缩放、世界脉冲（本方案的主要范式来源）
- Crypt of the NecroDancer / BPM: Bullets Per Minute：强量化的两个极端样本
- Hades：攻击令牌/围攻控制、房间奖励结构
- Koreographer 文档：音乐事件同步的工具链参照
