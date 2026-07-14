# Game Concept: 玉兔:破阵 (Jade Rabbit: Warsong)

*Created: 2026-07-13*
*Status: Vision Locked — Pending M0 Spike*
*Source: Synthesized from ClaudeCowork/ v2 restart documents (PRD, system design, rebuild roadmap)*

---

## Elevator Pitch

> 一款中国古风拟人动物题材的节奏动作 Roguelike——你是一位兔子武侠，在充满节拍的朝廷迷宫中，每一刀都必须踩在 BGM 的拍点上才能发挥全威力。音乐不是背景，而是你的武器时钟。
>
> *(EN)* A Chinese-mythology-inspired rhythm-action roguelike where you play as a rabbit martial artist — every sword strike must land on the musical beat to deal full damage. The BGM is not background music; it is your combat system.

---

## World Premise

**架空复合天朝**（Fictional Composite Imperial Court）

世界设定为意象而非还原的虚构天朝，刻意跨朝代综合：

| 来源文化层 | 素材 | 用途 |
| ---- | ---- | ---- |
| 宋院体画 | 工笔鸟兽、留白构图、淡雅色调 | 视觉语言基底 |
| 明补子制度 | 文武官员补子鸟兽阶品 | 敌人等级制与视觉区分 |
| 唐《秦王破阵乐》 | 战阵鼓乐，踩拍破阵 | 节奏战斗的文化合法性 |
| 辛弃疾词风 | 苍凉豪放、以词为武器 | 叙事基调与情感底色 |

> *跨朝代混合为刻意的设定选择，非史实错误。*

**核心视觉分野**（敌我识别规则）

> **有补子 = 体制内 = 敌**；**无补子 / 劳役牲口 = 体制外 = 友或主角**

补子（官服胸背图案）是体制身份的瞬间视觉标志，玩家一眼可读：着补服者皆为需要击破的体制机器。

**主角前提**

> 玉兔本是永生机器底层捣药劳工，放下药杵、拿起苗刀，开始破阵。

玉兔捣药（月兔神话）为隐性锚点，非明线叙事：「药杵」象征压迫，「苗刀」是反抗，形态一换即是阶级翻转。身上无补子，不在等级体制内，故有资格破阵。

**三重主题轴**

1. **阶级**：长生药机器（谁劳作 / 谁送死 / 谁永生）
2. **反战**：破阵乐的反讽（一将功成万骨枯；要破战争机器须自成兵器）
3. **循环**：兔死狗烹（权力用完即弃其工具）

> *世界观细节：`design/worldbuilding/setting-proposal-v2.md`（Canon — 已合并，2026-07-13）*
> *角色选定寓意：`design/worldbuilding/animal-roster.json`（Canon — 已合并，2026-07-13）*

---

## Core Identity

| Aspect | Detail |
| ---- | ---- |
| **Genre** | 节奏动作 Roguelike (Rhythm Action Roguelike) |
| **Platform** | PC (Windows / macOS)，Steam；完整手柄支持 |
| **Target Audience** | 硬核动作玩家 + 国风文化爱好者 + Roguelike 构筑爱好者（见 Player Profile） |
| **Player Count** | 单人 (Single-player) |
| **Session Length** | 单局约 20-30 分钟；完整游戏日约 60-90 分钟 |
| **Monetization** | Premium（对标 $19.99） |
| **Estimated Scope** | Large（12+ 个月，4 人核心团队 / 个人+AI Agent） |
| **Comparable Titles** | Hades（流畅近战 + Roguelike），Dead Cells（动作精度），Hi-Fi Rush（节奏量化战斗） |

---

## Core Fantasy

你是一位通晓音律的武侠。在这座随着鼓点呼吸的朝廷迷宫中，剑气随旋律流动，每一次出招都是一个音符，每一场 Boss 战都是一首乐曲的高潮。

当你踩上 Perfect，金光编钟炸响，连击数在屏幕边缘脉冲——那一刻你不只是在打怪，你在用武器演奏一曲破阵乐。

**这里是唯一一款让你感受到「战斗即演奏，演奏即战斗」的游戏。**

---

## Unique Hook

> "It's like **Hades** (smooth combo combat + roguelike runs), **AND ALSO** your every attack is musically quantized — miss the beat, miss the kill."

节奏层不是可选的加分系数，而是攻击量化执行的规则本身。玩家按下攻击，动作吸附到最近的半拍网格上执行，按输入偏差获得 Perfect / Good / Miss 评级，评级直接影响伤害倍率、连击增长和特效表现。

「听音乐防御」也成为真实策略：所有敌人攻击强制落拍，前摇以拍为单位，玩家可以通过音乐预读敌人出手。

---

## Player Experience Analysis (MDA Framework)

### Target Aesthetics（玩家感受）

| Aesthetic | Priority | How We Deliver It |
| ---- | ---- | ---- |
| **Sensation**（感官快感） | 1 | 踩拍瞬间的编钟音效 + 金光特效 + 屏幕脉冲；每一击都有听觉-视觉-触觉三重反馈 |
| **Challenge**（挑战与精通） | 2 | 节奏精度系统提供深不见底的技能天花板；连招树的取消窗口需要练习 |
| **Fantasy**（沉浸幻想） | 3 | 拟人兔武侠 + 中国古代朝廷世界观；BGM 用古典乐器演奏；水墨美术风格 |
| **Discovery**（发现与探索） | 4 | 每局心法组合创造不同的节奏体验；隐藏的技能协同等待玩家发现 |
| **Expression**（自我表达） | 5 | 武器与心法构筑个性化；不同连招风格适配不同节奏偏好 |
| **Narrative**（叙事）| 6 | 氛围叙事（NPC 对白 + 场景）而非主线剧情驱动 |
| **Fellowship**（社交） | N/A | 单机体验，排行榜为 P2 |
| **Submission**（放松） | N/A | 本作主动挑战玩家精度，不提供低压力循环 |

### Key Dynamics（期望涌现行为）

- 玩家自发地「等节奏」而不是「乱按」，理解踩拍才是最优策略
- 玩家在局内主动寻找「改变节奏规则」的心法，而不只是找伤害加成
- 玩家在 Boss 战中用耳朵「听前摇」，形成音画双通道的防御本能
- 玩家分享「这局的心法 build 让我打出了什么变态连击」

### Core Mechanics（核心机制系统）

1. **节奏量化攻击系统** — 攻击输入吸附到半拍网格，按偏差评级（混合模型：移动/闪避即时，攻击量化）
2. **连招图系统** — ScriptableObject 数据驱动的连招树（AttackNodeSO / ComboGraphSO），取消窗口替代后摇
3. **心法构筑系统** — 房间奖励三选一的临时强化（PerkSO），稀有度分级，可修改节奏规则本身
4. **节奏化敌人 AI** — 战斗导演（CombatDirector）令牌 + 拍槽分配，所有敌人攻击强制落拍
5. **局外成长系统** — 功勋（永久货币）驱动的倍率升级 + 武器解锁，PlayerInfoSO 持久化

---

## Player Motivation Profile

### Primary Psychological Needs Served

| Need | How This Game Satisfies It | Strength |
| ---- | ---- | ---- |
| **Competence**（能力感） | 节奏精度有清晰的 Perfect/Good/Miss 分级反馈；连招深度随熟练度显现 | **Core** |
| **Autonomy**（自主感） | 每局三选一心法形成不同的构筑方向；武器选择决定游戏风格 | **Supporting** |
| **Relatedness**（归属感） | 单机为主；中国文化题材对国风爱好者有认同感 | **Minimal** |

### Player Type Appeal (Bartle Taxonomy)

- [x] **Achievers** — 连击数纪录、局外成长、武器解锁；追求「我踩得比任何人都准」
- [x] **Explorers** — 心法协同的发现乐趣；不同节奏量化档位的探索
- [x] **Killers/Competitors** — 高技能天花板；未来排行榜（P2）；Speed Run 潜力
- [ ] **Socializers** — 非本作主力；分享 build 是唯一社交锚点

### Flow State Design

- **Onboarding curve**：前 3 个房间为线性引导房（固定 BPM 100，宽容判定窗）；延迟校准强制首次进行
- **Difficulty scaling**：BPM 随层数阶梯式提升；Boss 二阶段切换拍号（4/4 → 3/4 → 5/8）
- **Feedback clarity**：判定即时文字 + 颜色分级（金/橙/灰）+ early/late 方向提示；连击数实时展示
- **Recovery from failure**：死亡即时结算，功勋保留，下局立刻开始；死亡惩罚低，学习反馈高

---

## Core Loop

### 时刻到时刻（30 秒）

踩拍出招 → 判定评级（Perfect/Good/Miss）→ 连击增长 + 伤害结算 → 下一帧出招决策

这个循环必须在没有任何成长系统的情况下也足够有趣——「踩准了好爽」是整个游戏存在的理由。

### 短期目标（5-15 分钟 / 单房间到精英房）

进入房间 → 战斗清场 → 房间奖励（心法三选一）→ 前进下一房间
「我要找到今局最强的心法组合」是这一层的驱动力。

### 单局（20-30 分钟）

入口房 → 战斗房 ×2-3 → 精英/事件房 → Boss 战 → 死亡/通关结算 → 功勋转化
通关的「爽感」和失败后「下次一定」的心态共同撑起复盘冲动。

### 长期成长（多日）

功勋累积 → 修炼永久升级（HP / 攻击 / 冲刺 CD 倍率）→ 解锁新武器与心法池 → 每局构筑广度与强度提升
「我要把所有武器都解锁，找出最强的节奏 build」是长线驱动。

### Retention Hooks

- **Curiosity**：下一个心法池里有什么稀有心法？长枪的节奏感和苗刀有什么不同？
- **Investment**：积累的功勋不会消失；武器解锁是永久的
- **Mastery**：「我今天终于连出 X-X-Y 的 Perfect 链了」——技能精进是可感知的

---

## Game Pillars

### 支柱一：节奏即战斗 (Rhythm IS Combat)

音乐不是背景音轨，而是战斗系统的操作系统。每一次出招都量化到拍点执行——玩家同时完成「听节奏」「判时机」「决策连招」三件事。节奏层不可绕过，不可关闭。

*设计验证题*：讨论「加一个无视拍点也能打出满额伤害的超级技能」——**这根支柱说：不加。** 每一种强力 build 都是节奏精通的回报。

*文化注*：「破阵子」词牌本为唐《秦王破阵乐》军乐舞谱；「踩着拍子破阵」是本作标题的本义，节奏即战斗有逾 1400 年文化先例。

### 支柱二：武侠手感不可妥协 (Wuxia Feel First)

踩拍是门槛，但剑落的重量、刀气的冲击、连段的流畅度是底线。节奏层叠加在扎实的动作手感之上，而非取代它。Animancer 变速动画适配 BPM，保留武侠动作的质感。

*设计验证题*：讨论「为了强化节奏反馈，把攻击动画压到最短」——**这根支柱说：先保手感，** 节奏反馈通过音效/特效强化，动画时长不牺牲。

### 支柱三：华风沉浸 (Chinese Cultural Authenticity)

题材不是皮肤，而是世界观原则。拟人动物、朝廷背景、传统乐器音效、水墨视觉语言——所有感官共同服务于「这是只有这款游戏才有的中国武侠世界」。

*设计验证题*：讨论「把主角换成人类以拓宽受众」——**这根支柱说：坚守差异化。** 兔子武侠 + 中国古典音乐是核心竞争力，泛化等于放弃独特钩子。

### 支柱四：构筑驱动复盘 (Build Depth Over Stat Inflation)

Roguelike 的心法系统不只是数值加减，而是改变玩家与节奏系统的交互规则。「完美判定后下次攻击自动 Perfect」比「+15% 伤害」更重要。每局应该感觉不同，不只是数字更大。

*设计验证题*：「+20% 移速心法」vs「Good 判定也产生连击 +2 的心法」——**这根支柱说：选后者。** 节奏系统深度优先。

### Anti-Pillars（本作明确不做）

- **NOT 轨道音游**：我们是动作游戏，节奏是战斗维度，不是 Osu/Guitar Hero 式的轨道输入界面
- **NOT 多人联机（首发）**：单机体验打磨到位优先；联机、排行榜是 P2 内容
- **NOT 剧情主导叙事**：叙事服务于世界观氛围；游戏的「故事」是玩家的手感成长历程
- **NOT 绕过节奏的无敌技**：不存在让玩家可以忽视节奏系统的破坏性技能

---

## Visual Identity Anchor

**方向名称**：国朝墨彩 (Imperial Ink & Color)

**核心视觉规则**：水墨笔触的环境 + 鲜活的拟人动物角色 + 2:1 等距 3D 构图，像一幅会动的中国宫廷卷轴画。

**三条支撑原则**：

1. **墨彩对比**：背景水墨留白，前景角色色彩饱满——敌我关系一眼可读，战场噪音极低
   - *设计验证*：若一个 UI 元素在水墨背景上「跳」不出来，调整而非调暗背景

2. **三色系统**：绛红 #9E2F2C（危险 / 玩家攻击）、石青 #1685A9（信息 / 节奏指示）、金 #FFD700（奖励 / Perfect 判定）
   - *设计验证*：新增颜色时问「它属于哪个语义？」不属于三色系统的颜色需要充分理由

3. **节拍驱动视效**：所有 UI 动画、特效脉冲、屏幕边缘光晕都随 BPM 呼吸——玩家关掉音量也应感受到节奏

**字体系统**：汉仪尚巍手书（标题 / 武器名 / Boss 名）+ 思源宋体（正文 / HUD 数字）

> *注意*：字体商用授权需在 M0 阶段确认（v1 教训）。

---

## Inspiration and References

| Reference | What We Take From It | What We Do Differently | Why It Matters |
| ---- | ---- | ---- | ---- |
| **Hades** | 流畅近战连招 + 图书馆式 Roguelike 构筑 + 短局时间 | 节奏层替代祝福组合作为「变量轴」；中国古风替代希腊神话 | 验证了流畅动作 + Roguelike 的市场可行性 |
| **Hi-Fi Rush** | 攻击量化到拍点执行（混合模型）+ 全世界随节奏呼吸 | 2D/2.5D isometric vs 3D TPS；更深的 Roguelike 构筑层 | 验证了「量化模型」比「判定模型」更直觉易懂 |
| **Dead Cells** | 高精度动作 + 局内成长 + 极低死亡惩罚 | 节奏维度 + 中国文化；速度感 vs 精度感 | 验证了「容易上手、难以精通」的动作 Roguelike 市场 |
| **Crypt of the NecroDancer** | 节奏驱动整个游戏系统（移动、攻击皆量化）| 我们只量化攻击，移动自由，降低准入门槛 | 证明节奏 + 地牢游戏的概念成立；其学习曲线的陡峭性是我们的警示 |

**非游戏灵感**：
- 中国传统打击乐（编钟、木鱼、丝弦）→ 判定音效音景
- 《少年游》《破阵子》等古词意境 → 场景命名与氛围设计
- 中国皮影戏 + 宋代院体画 → 角色剪影与场景构图参考

---

## Target Player Profile

| Attribute | Detail |
| ---- | ---- |
| **Age range** | 18-35 |
| **Gaming experience** | Mid-core 到 Hardcore |
| **Time availability** | 工作日 30-60 分钟，周末 2-3 小时 |
| **Platform preference** | PC（Steam），手柄玩家比例高 |
| **Current games they play** | Hades、Dead Cells、节奏游戏（Osu、DJMAX）、国风单机（鬼谷八荒） |
| **What they're looking for** | 有深度、有文化特色、重复可玩性强的精品独立游戏 |
| **What would turn them away** | 节奏层感觉像「附加摩擦」而非「核心乐趣」；美术风格不够统一；连招系统深度不足 |

---

## Technical Considerations

| Consideration | Assessment |
| ---- | ---- |
| **Engine** | Unity 6.3 LTS (6000.3.19f1) + URP |
| **Key Technical Challenges** | DSP 时钟精度 + 帧对齐输入采样；Animancer 动画变速适配 BPM；连招图状态机数据化；敌人 AI 攻击令牌与拍槽分配 |
| **Art Style** | 2:1 等距 3D（场景），2D 立绘（对话），水墨 + 国朝色彩 |
| **Art Pipeline Complexity** | Medium-High（3D 角色模型 + 动画 + 水墨环境；MVP 全程用代理资源） |
| **Audio Needs** | Music-heavy + Adaptive（BPM/拍号驱动全战斗系统；DSP 时间轴精度是技术硬需求）；推荐 FMOD Studio 作为音频中间件，M0 定案 |
| **Networking** | None（首发纯单机） |
| **Content Volume（Full Vision）** | 3 种武器 × 各含完整连招树；2+ Boss；20-30 间预设房间；50+ 心法 |
| **Procedural Systems** | MVP：手工线性拼接；M6+：BSP 树 + 权重抽取（WFC 待评估） |

---

## Risks and Open Questions

### Design Risks

- **核心假设未验证**：节奏层与动作手感的融合是否真的让玩家感受到「一体感」而非「附加摩擦」——这是整个产品是否成立的关口，必须在 M2 里程碑的外部试玩中验证
- **学习曲线失控**：节奏精度 + 连招树 + 心法构筑三层学习压力叠加，可能导致新手快速放弃
- **延迟校准体验**：延迟校准流程如果体验不佳，会在游戏首分钟就劝退玩家

### Technical Risks

- **DSP 时钟精度**：无线耳机延迟 100-300ms；不校准则判定体系不成立（v1 未处理）
- **帧量化误差**：v1 在 Update 轮询判定，60FPS 误差高达 16.7ms；v2 必须改为 Input System callback 时间戳
- **动画变速管线**：Animancer 攻击 Clip 时长需适配 BPM，实现复杂度高于普通 AnimationController

### Market Risks

- **细分受众交集**：节奏 + Roguelike + 中国古风三个受众的交集需要足够大以支撑销售目标
- **竞品稀少既是机会也是风险**：市场空白可能是真机会，也可能说明此类型不被主流接受

### Scope Risks

- **美术产能**（v1 教训）：v1 项目美术资源几乎空缺；v2 必须早期定好代理资源规范，美术风格验证独立推进
- **第三方授权**（v1 教训）：v1 使用未授权资产和版权 BGM；v2 必须在 M0 建立授权清单

### Open Questions

- **量化档位定案**（M0 Spike 待定）：Off / HalfBeat / Beat 三档哪个手感最好？M0 原型 Spike 先跑三个候选档位，结果出来前下游 GDD 保持档位无关写法，不预设结论。Spike 的 PASS 条件（验收判定标准）将在 M0 里程碑计划文档中于 Spike 开始前定案，不在本文档预设
- **FMOD 推荐为音频中间件**（M0 定案前）：高精度需求下推荐引入 FMOD Studio；Unity Audio DSP 备选方案保留。须在节奏核心 GDD 中于 M0 正式定案
- **行为树方案**：Behavior Designer（已购入但未用）vs 自研 AIBT vs 其他方案——M0 决策（见 03 文档 D2）
- **世界观合并** ✅ 已关闭：`design/worldbuilding/setting-proposal-v2.md` 已于 2026-07-13 合并为 Canon（见 Dependencies 表格）
- **NPC/敌人动物冲突（待解决）**：`design/worldbuilding/animal-roster.json` 中的 NPC 动物与敌人动物分类有冲突，须在 `design/art/art-bible.md` 修订时对齐（建议 M1 前完成）

---

## MVP Definition

**核心假设**：踩拍攻击 × 武侠连招 × 节奏反馈三合一，是否能让玩家在 15-20 分钟内自发地「再来一局」？

*范围说明*：MVP 验证**支柱一（节奏即战斗）**与**支柱二（武侠手感）**是否融合成立。支柱四（构筑驱动复盘）在 MVP 中仅作最小化展示（5 个心法、最多 2-3 次三选一），完整验证在 **Vertical Slice 阶段**（10+ 心法、5+ 房间）进行，不应以 MVP 的构筑体验代入 Vertical Slice 的设计判断。

**MVP 必须包含**：

1. 苗刀（MiaoDao）——完整轻/重/冲刺连招 + 节奏判定加成
2. 普通敌人 1 种（行为树驱动）+ Boss 1 个（阶段化节奏设计）；MVP Boss 候选：獬豸（御史台，反讽最锋利一战）
3. 手工拼接 3-5 个房间（入口 → 战斗 ×2 → 奖励 → Boss）
4. 心法三选一（3-5 个心法效果，包含至少 1 个改变节奏规则的心法）
5. 死亡/通关结算 + 功勋累积 + 1 项局外升级可选
6. 延迟校准流程 + 存档 + 暂停 + 基础 HUD（血量、连击数、节奏指示器）

**MVP 明确不含**（推迟）：

- 第二、三武器（长枪、剑）
- 随机地牢算法（BSP / WFC）
- 对话叙事系统 / NPC 互动
- 多 Boss、拍号 5/8 阶段
- 皮肤 / 排行榜 / 联机功能

### Scope Tiers

| Tier | Content | Features | Timeline |
| ---- | ---- | ---- | ---- |
| **MVP** | 1 武器 + 1 Boss + 3-5 线性房间 + 5 个心法 | 节奏核心 + 连招图 + 基础心法 + 存档 HUD | M0-M2，约 6-8 周 |
| **Vertical Slice** | 2 武器 + 1 Boss + 5±1 房间 + 10+ 心法 | MVP + 节奏化 AI + 房间流 + 局外成长 | M0-M4，约 4-5 个月 |
| **Alpha** | 3 武器 + 2 Boss + 随机地牢 + 30+ 心法 | Vertical Slice + 随机生成 + 第二 Boss | M0-M5，约 7-9 个月 |
| **Full Vision** | 全内容 + 叙事 + 打磨 + 发布版本 | Alpha + 对话系统 + 正式美术 + 性能优化 + Steam 页面 | M0-M6，12+ 个月（团队 4 人 / 个人+AI） |

---

## Detailed Rules

> *概念层规则（高层次）。系统级精确规范见各独立系统 GDD。*

### 规则一：混合量化模型（Hybrid Quantization）

- **移动与闪避**：完全即时，不受节拍限制
- **攻击输入**：吸附到最近的半拍网格节点执行（Half-beat grid snap）
- **量化档位**：M0 Spike 待定；候选档位 Off / HalfBeat / Beat，不同档位代表不同游戏体验
- *理由*：保留武侠动作的流畅感；只量化攻击，最大程度降低准入门槛

### 规则二：评级系统与评级后果合约（Rating + Consequence Contract）

攻击输入与最近拍点的时间偏差决定评级：

| 评级 | 时间偏差 | 伤害倍率 | 连击变化 | 连招取消窗口 | 额外效果 |
| ---- | ---- | ---- | ---- | ---- | ---- |
| **Perfect** | TBD（M0 Spike 定标） | 100% | +2 | 全开（含高级取消） | 动力学变化（见下） |
| **Good** | TBD | 75% | +1 | 标准取消窗口 | — |
| **Miss** | 超出窗口 | 50% | 不增长（不清零） | 关闭高级取消窗口 | — |

**混合模型说明**：
- 门控式：Perfect 解锁隐藏路径与高级取消窗口；Miss 关闭高级选项
- 容错式：Miss 仍造成 50% 伤害；连击数停止增长但不归零，保留已建立的连击价值

### 规则三：Perfect 动力学变化（Perfect Kinetics）

当玩家达成 Perfect 评级，物理世界同步变化——不只是 UI 数字，而是游戏世界本身的反应：

1. **冻帧**：2-3 帧画面定格（约 33-50ms @ 60fps）
2. **敌人硬直**：命中敌人硬直时长 × 1.3
3. **音频共鸣层**：叠加一层编钟共鸣音轨到当前混音中
4. **摄像机微切**：0.1s 轻微推近再弹回（camera micro-cut）

*设计意图*：让 Perfect 从「评级显示」变成「可感知的物理现象」，强化「战斗即演奏」的一体感。

### 规则四：敌人强制落拍（Enemy Beat Lock）

- 所有敌人攻击前摇以拍为单位（1拍、2拍等），由 CombatDirector 令牌分配确保落拍
- 玩家可通过音乐节奏预读敌人出手时机
- 此规则是「听音乐防御」策略的底层保障，不可豁免

### 规则五：心法设计空间约束（Perk Design Space Containment）

心法可以修改评级后果合约中的**数值参数**（如伤害倍率、连击增量、硬直时长），但**不可消除评级层级之间的质差**。以下三条约束是 Anti-Pillar「节奏不可绕过」的具体护栏，供心法系统 GDD 作为不可逾越的设计边界：

1. **评级层级不可合并**：心法不得将一个评级的全部后果整体提升至更高评级（如「Miss 算 Good」）。允许修改单一维度（如「Miss 伤害提升至 60%」），但至少一个后果维度必须保留评级差异，确保踩拍精度在任何 build 下都有可感知的价值。

2. **连击增长不可覆盖**：Miss 时连击不增长是节奏层可见性的底线——玩家必须能从连击数停滞中感知到「我刚才没踩准」。心法可降低 Miss 的伤害惩罚，但不可让 Miss 产生正向连击增长。

3. **高级取消窗口不可无条件覆盖**：Perfect 解锁的高级取消窗口是技能天花板的核心表达，是「精通玩家的专属奖励」。心法不得无条件对全部评级开放此权限，此项保护不可豁免。

---

## Formulas

> *概念层公式（占位）。精确公式、边界值与示例计算见各系统 GDD。*

### 伤害公式（概念层）

```
final_damage = base_damage × rating_multiplier × combo_multiplier × perk_multiplier
```

| 变量 | 定义 | 范围 | 来源 |
| ---- | ---- | ---- | ---- |
| `base_damage` | 武器基础伤害 | TBD（rhythm-core GDD） | 武器 ScriptableObject |
| `rating_multiplier` | Perfect=1.0 / Good=0.75 / Miss=0.5 | [0.5, 1.0] | 评级系统 |
| `combo_multiplier` | 连击数驱动的乘数 | TBD（combo GDD） | 连招图系统 |
| `perk_multiplier` | 心法叠加倍率 | TBD（perk GDD） | 心法系统 |

### 判定窗口（占位）

```
perfect_window_ms = TBD（M0 Spike 输出）
good_window_ms    = TBD（M0 Spike 输出）
```

*注*：具体时间窗口须通过 M0 量化 Spike 的手感测试确定，不在此文档预设。

### 校准公式（概念层）

```
calibrated_input_time = raw_input_time − player_latency_offset_ms
```

玩家延迟偏移通过延迟校准小游戏测量，首次进入游戏强制执行。

---

## Edge Cases

> *概念层边界情况。系统级边界处理见各系统 GDD。*

| 边界情况 | 处理规则 |
| ---- | ---- |
| **玩家跳过延迟校准** | 使用 0ms 默认偏移；在 HUD 显示「建议校准」提示；不阻止游戏进行 |
| **无线耳机延迟 > 校准范围** | 校准界面注明「检测到高延迟，建议使用有线耳机」；允许手动输入偏移值 |
| **Miss 时连击为 0** | 连击保持 0，不出现负数；Miss 伤害正常结算（50%） |
| **Boss 阶段切换时拍号改变** | 新拍号生效时，进行中的攻击动画按原拍号完成；新判定窗口从下一拍起算 |
| **心法改变节奏规则（如"Miss 也算 Good"）** | 心法参数覆盖评级合约表格对应行；冲突心法以最后生效的为准 |
| **BPM 为 0 或音频静音** | 节拍系统回退为固定 120 BPM 内部时钟；视觉节拍指示器继续工作 |

---

## Dependencies

> *本文档依赖以下资源。双向依赖：各系统 GDD 应反向引用本文件作为设计基准。*

### 必须优先存在的文档

| 依赖项 | 文件路径 | 当前状态 | 阻塞方向 |
| ---- | ---- | ---- | ---- |
| 艺术规范 | `design/art/art-bible.md` | 进行中（第 1-3 节已完成） | 视觉身份、补子视觉规则 |
| 世界观设定 | `design/worldbuilding/setting-proposal-v2.md` | Canon（已合并，2026-07-13） | 叙事基调、敌人设定 |
| 角色动物选定 | `design/worldbuilding/animal-roster.json` | Canon（已合并，2026-07-13） | 敌人设计、NPC 设定 |

### 本文档为以下系统 GDD 的前置基准

| 下游系统 GDD | 文件路径 | 当前状态 |
| ---- | ---- | ---- |
| 节奏核心系统 | `design/gdd/rhythm-core.md` | 未创建 |
| 连招图系统 | `design/gdd/combo-system.md` | 未创建 |
| 心法构筑系统 | `design/gdd/perk-system.md` | 未创建 |
| 敌人 AI 系统 | `design/gdd/enemy-ai.md` | 未创建 |
| 局外成长系统 | `design/gdd/meta-progression.md` | 未创建 |

---

## Tuning Knobs

> *以下为概念层可调参数。精确范围与安全边界见各系统 GDD。*

| 参数 | 当前值 | 安全范围 | 影响的游戏体验 |
| ---- | ---- | ---- | ---- |
| **起始 BPM** | 100 | [80, 120] | 新手引导房间的紧迫感；低于 80 感觉迟缓，高于 120 新手挫败 |
| **BPM 层数递增** | TBD | [5, 20 / 层] | 难度曲线斜率；变化过大会造成突变感 |
| **Perfect 判定窗口** | TBD（M0 Spike） | [40ms, 100ms] | 技能天花板高度；越窄越精英化 |
| **Miss 伤害倍率** | 0.5 | [0.3, 0.7] | 节奏惩罚的严重程度；低于 0.3 劝退休闲玩家 |
| **连击上限** | TBD | [20, 100] | 连击数值的「顶端」感；影响高手天花板与 HUD 可读性 |
| **Perfect 冻帧时长** | 2-3 帧 | [1, 4 帧] | 节奏一体感强度；超过 4 帧影响战斗流畅度 |
| **每局房间数** | 5（MVP） | [4, 8] | 单局时长；直接影响 20-30 分钟目标 |
| **心法三选一刷新池大小** | TBD | [15, 60] | 构筑方向的多样性 vs 重复感 |

---

## Acceptance Criteria

> *以下条件由 QA 可独立验证通过或失败。「感觉好」不是合格标准。*

### AC-1：节奏-伤害绑定验证

**条件**：在不使用任何心法的情况下，对同一敌人连续打出 Perfect / Good / Miss 各 5 次，记录每次伤害数字。

**通过标准**：Perfect 伤害 ÷ Good 伤害 = 1.00 ÷ 0.75 = 1.333（误差 ±1%）；Good ÷ Miss = 0.75 ÷ 0.50 = 1.5（误差 ±1%）。

### AC-2：Miss 不清零连击

**条件**：建立连击数 ≥ 10，随后故意打出一次 Miss。

**通过标准**：连击数字在 Miss 后保持不变（不归零）；连击数在下一次 Good/Perfect 后正常 +1 或 +2。

### AC-3：Perfect 动力学变化全部触发

**条件**：对任意敌人打出一次 Perfect 评级。

**通过标准**：以下四个效果必须**同时**触发：(a) 冻帧可见（目测 1-4 帧定格）；(b) 敌人硬直时长明显长于 Good 命中；(c) 音频中可听到额外编钟共鸣层叠加；(d) 摄像机有轻微推近-弹回动作。

### AC-4：敌人攻击落拍验证

**条件**：在 BPM=120 的房间内与普通敌人战斗 3 分钟，录制视频逐帧对齐音乐节拍。

**通过标准**：敌人攻击前摇结束（攻击判定帧）与最近节拍节点的误差 < 50ms（在 60fps 下约 3 帧以内）。

### AC-5：延迟校准保护判定体系

**条件**：设置人工延迟 100ms，完成延迟校准流程，然后以「正确的耳机时序」按攻击。

**通过标准**：校准后的 Perfect 命中率（按主观感受踩拍）不低于未延迟时的 80%；若跳过校准则 Perfect 命中率明显下降。

### AC-6：30 秒核心循环无教程可发现性

**设计意图**：节奏系统必须通过音画线索（节奏指示器、判定即时反馈、BPM 同步视觉脉冲）传递其存在，使新玩家无需文字说明即可发现踩拍机制的存在与奖励差异。

**验证方式**：M2 外部试玩用户研究协议（非自动化 QA 测试）。由制片人在 M2 里程碑计划中定义具体测试方案，包括参与者数量、观察指标与 PASS 标准。音画引导的具体规格（节奏指示器可见性、判定颜色反馈时机）见 rhythm-core.md 和 UX spec。

---

## Next Steps

Pre-Production 管道（按优先级排列）：

- [ ] **M0 Spike（最高优先级）** — 搭建最小节奏量化原型，测试 Off / HalfBeat / Beat 三个量化档位的手感，输出量化判定时间窗口数据；同步定案音频中间件（FMOD vs Unity Audio）
- [ ] **世界观合并** — 评审 `design/worldbuilding/setting-proposal-v2.md`，确认后合并入 canon；对齐 `art-bible.md` 中的 NPC 动物与敌人动物冲突
- [ ] **`/design-system rhythm-core`** — 节奏核心系统 GDD（依赖 M0 Spike 数据）
- [ ] **`/design-system combo-system`** — 连招图系统 GDD
- [ ] **`/design-system perk-system`** — 心法构筑系统 GDD
- [ ] **`/design-system enemy-ai`** — 敌人 AI 系统 GDD
- [ ] **`/design-system meta-progression`** — 局外成长系统 GDD
- [ ] **`/create-architecture`** — 生成主架构蓝图和必须 ADR 列表
- [ ] **`/architecture-decision`（×N）** — 为 D1-D8 架构决策各写一个 ADR（见 ClaudeCowork/03-refactor-recommendations.md）
- [ ] **`/architecture-review`** — 生成 TR 注册表和需求可追溯矩阵
- [ ] **`/gate-check pre-production`** — Pre-Production 阶段门验证，进入 Production 前的最后关口
