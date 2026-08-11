# Interaction Pattern Library

> **Status**: In Design (initialized — Pre-Production gate prerequisite)
> **Author**: user + ux-designer
> **Last Updated**: 2026-08-11
> **Template**: Interaction Pattern Library
> **Accessibility Tier**: WCAG 2.2 AA + 类型专属补偿(见 `design/accessibility-requirements.md`)

---

## Overview

本库收录《玉兔:破阵》所有可复用的 UI/交互模式,作为各屏 UX spec(`design/ux/*.md`)引用的单一事实来源。目标:同类交互全项目一致,避免每个屏幕各自发明轮子。

**输入前提**(来自 `game-concept.md:61` + `input-event-system.md`):
- **主输入**:键鼠 + 完整手柄(PC / Steam,单人)
- **导航底座**:所有 UI 经 F3 的 UI Action Map 广播 `Navigate` / `Submit` / `Cancel`,UI 系统消费(`input-event-system.md:361`)
- **无障碍**:每个模式条目携带其无障碍要求,统一对齐 `accessibility-requirements.md` 提交的 AA + 类型补偿等级
- ⚠️ 触控 / 移动端等价为**前向兼容保留项**,非确定发行目标(平台冲突见 `accessibility-requirements.md` §9)

本库为**初始化版本**:先编目现有 GDD 中已确立的模式,规格随各屏 UX spec 撰写而细化。新屏若发明新模式,须回填本库(gate-check 交叉引用要求)。

---

## Pattern Catalog

现有 GDD 已确立的交互模式清单(按类别):

| # | 模式名 | 类别 | 已用于 | 来源 |
| -- | ---- | ---- | ---- | ---- |
| P-01 | 手柄/键盘焦点导航 | Navigation | 所有菜单/选择屏 | `input-event-system.md:361`;`art-bible.md:403` |
| P-02 | 三选一卡片选择(Roguelike Reward) | Selection | 心法三选一(P2) | `perk-system.md:273-276` |
| P-03 | 判定反馈弹出(Rating Popup) | Feedback | 战斗 HUD | `art-bible.md:425-433`;`rhythm-core.md:781` |
| P-04 | 节奏指示器(Beat Indicator) | Data Display | 战斗 HUD | `game-concept.md:144, 231` |
| P-05 | 连击计数器(Combo Counter) | Data Display | 战斗 HUD | `combo-system.md:243-245` |
| P-06 | 持续状态标识(Persistent Mode Badge) | Overlay | Off 无障碍标识、建议校准提示 | `rhythm-core.md:54-55`;`game-concept.md:448` |
| P-07 | 暂停覆盖层(Pause Overlay) | Modal | 暂停菜单(P5) | `tde-integration.md:373`;`game-concept.md:454` |
| P-08 | 校准小游戏(Calibration Mini-flow) | Input | 延迟校准(FE3) | `game-concept.md:432-438, 448-449` |
| P-09 | 键位重绑定(Rebind Capture) | Input | 设置菜单(FE7) | `input-event-system.md:361` |
| P-10 | 局末结算(Run Summary) | Data Display | 死亡/通关结算 | `game-concept.md:145, 164` |

---

## Patterns

### P-01 手柄/键盘焦点导航(Gamepad / Keyboard Focus Navigation)

**Category**: Navigation
**Used In**: 主菜单、心法三选一、暂停菜单、设置、结算屏

**Description**:所有非战斗 UI 的通用导航底座。玩家用方向键/摇杆/D-pad 在可交互元素间移动焦点,`Submit` 确认,`Cancel` 返回。这是全项目菜单交互的默认骨架,任何新屏默认继承。

**Specification**:
- 输入:F3 UI Action Map 的 `Navigate`(Vector2)/ `Submit` / `Cancel`;鼠标悬停等价于焦点移动、点击等价于 Submit
- 焦点视觉:石青 `#1685A9` 描边 + BPM 呼吸闪烁(`art-bible.md:403`);任一时刻**必有且仅有一个**可见焦点
- 焦点顺序:符合阅读/空间直觉(左→右、上→下),环绕(wrap)在同一控件组内
- 摇杆死区:圆形幅值死区(`StickDeadzoneProcessor`),防止斜向误触(`input-event-system.md:355`)

**Accessibility**:全键盘/全手柄可完成任意路径,无鼠标专属操作(WCAG 2.1.1);焦点始终可见(WCAG 2.4.7)。对齐 `accessibility-requirements.md` A11Y-4。

**When to Use**:任何含 2+ 可选元素的非战斗屏。
**When NOT to Use**:战斗中的实时输入(走 F3 Gameplay Action Map,非 UI 导航);单按钮确认弹窗(仍需焦点,但无需导航)。

---

### P-02 三选一卡片选择(Three-Choice Reward Card)

**Category**: Selection
**Used In**: 心法三选一(P2 心法选择 UI)

**Description**:Roguelike 房间奖励的核心交互——展示 3 张候选卡,玩家选 1。卡面含名称、稀有度色、描述、图标。选定即应用并进入下一房间。

**Specification**(承 `perk-system.md:276`):
- 卡面字段:`displayName` / `rarity` 色 / `descriptionTemplate` 解析文本 / `iconSprite`
- 稀有度色:遵循三色系统语义,**不得仅靠颜色区分稀有度**——须叠加边框形态或稀有度文字标签(非颜色依赖,WCAG 1.4.1)
- 导航:继承 P-01;3 卡横向排列,`Navigate` 左右切焦点,`Submit` 选定
- 资源:P2 拥有卡面视觉规格,管理 `iconSprite` 加载/释放(`perk-system.md:276`)
- 选定反馈:选中卡放大 + 石青焦点强化;确认后其余两卡淡出

**Accessibility**:稀有度不可仅靠颜色(叠加形态/文字);d-pad + 触摸等价(跨平台保留项);描述文字对比度 AA。

**When to Use**:局内构筑选择(心法、未来武器选择)。
**When NOT to Use**:超过 ~5 个选项的列表(改用可滚动列表模式,待补);二元确认(用弹窗)。

---

### P-03 判定反馈弹出(Rating Popup)

**Category**: Feedback
**Used In**: 战斗 HUD(消费 `RhythmJudgmentEvent`)

**Description**:每次攻击判定后瞬时弹出的 Perfect / Good / Miss 反馈,是「战斗即演奏」一体感的最高频交互。由 P1 HUD 消费 C1 的 `RhythmJudgmentEvent`(字段 `Rating`, `OffsetMs`, `ComboCountAfter` — `rhythm-core.md:781`)。

**Specification**(色觉安全表,`art-bible.md:427-433`,不可删除):

| 判定 | 颜色 | 颜色外冗余信号 | 字号 |
| ---- | ---- | ---- | ---- |
| Perfect | 金 `#FFD700` | 全屏亮度峰值 1 帧 + 编钟音效 + 「Perfect」文字 | 最大 |
| Good | 石青 `#1685A9` | 节奏环轻闪 + 轻音效 + 「Good」文字 | 中 |
| Miss | 绛红 `#9E2F2C` | 屏幕边缘绛红晕染 + 钝音 + 「Miss」文字 | 中 |

- early/late 方向提示:Good/Miss 附方向指示,告知玩家早了还是晚了(`game-concept.md:144`)
- 触觉冗余:手柄震动强度随判定分级(Feel/NiceVibrations)
- 时机:判定即时(< 1 帧延迟),不阻塞后续输入

**Accessibility**:**四重冗余**(颜色 + 形态/位置 + 声音 + 文字标签),文字标签为纯文字兜底不可移除;灰度+静音下仍可 100% 分辨(`accessibility-requirements.md` A11Y-2);reduced-motion 下去掉运动分量但保留字号+文字+音效。

**When to Use**:节奏判定即时反馈。
**When NOT to Use**:非节奏事件(伤害数字、拾取提示走各自模式)。

---

### P-04 节奏指示器(Beat Indicator)

**Category**: Data Display
**Used In**: 战斗 HUD

**Description**:常驻 HUD 的视觉节拍参考,让玩家「看见」拍点、预读敌人落拍。是听觉无障碍的核心视觉冗余——静音也能玩(`game-concept.md:231`)。

**Specification**:
- 随 BGM BPM 呼吸/脉冲;屏幕边缘光晕同步(`art-bible.md` 节拍驱动视效)
- 石青 `#1685A9` 为节奏语义色,始终附 BPM 呼吸动画(任何静止石青块不存在,`art-bible.md:419`)
- 敌人前摇以拍为单位可视化,支撑「听音乐防御」(`game-concept.md:388`)
- 兜底:BPM=0 配置缺失时回退固定 120 BPM 内部时钟,指示器继续工作(`game-concept.md:453`);音频断开时启动为备用参考(`game-concept.md:454`)

**Accessibility**:纯视觉通道即可传达节拍(听障可玩);对齐 A11Y-7。
**When to Use**:所有战斗屏。
**When NOT to Use**:非战斗屏(菜单不显示节奏指示器,但 UI 动画仍可随 BPM 呼吸)。

---

### P-05 连击计数器(Combo Counter)

**Category**: Data Display
**Used In**: 战斗 HUD

**Description**:显示当前连击段数的纯展示元素,上限 999(数据源 C1,`combo-system.md:245`)。连击停滞是玩家感知「没踩准」的可见信号(承 Miss 不增长契约)。

**Specification**:
- 纯显示、无交互——满足 d-pad/触控无悬停等价(展示元素本身无需导航,`combo-system.md:245`)
- 数值上限 999;高连击时屏幕边缘脉冲强化(`game-concept.md:75`)
- 思源宋体数字(`game-concept.md:233`)

**Accessibility**:纯数字 + 高对比;连击停滞作为节奏可见性底线(`game-concept.md:400`)。
**When to Use**:战斗 HUD。
**When NOT to Use**:非战斗上下文。

---

### P-06 持续状态标识(Persistent Mode Badge)

**Category**: Overlay
**Used In**: Off 无障碍模式标识、「建议校准」提示

**Description**:常驻屏幕的不可关闭/建议性状态徽标,告知玩家当前处于某种特殊模式或有待办建议。

**Specification**:
- **Off 无障碍标识**(不可关闭,`rhythm-core.md:54-55`):Off 模式激活期间 HUD 全程显示「无障碍模式」标识,玩家不可关闭——这是 Off 五项合约第 2 条(持续提示),不可豁免
- **建议校准提示**(可忽略,`game-concept.md:448`):跳过延迟校准时 HUD 显示「建议校准」,不阻断游戏,玩家完成校准后消失
- 位置:HUD 边角固定,不遮挡战斗核心视区
- 视觉:低调但可读(对比度 AA),不与判定反馈争夺注意力

**Accessibility**:Off 标识为无障碍合约硬性要求;文字标签清晰,不仅靠图标。
**When to Use**:需要持续告知的模式状态或建议。
**When NOT to Use**:瞬时事件(用弹出/toast,待补)。

---

### P-07 暂停覆盖层(Pause Overlay)

**Category**: Modal
**Used In**: 暂停菜单(P5)

**Description**:游戏暂停时的模态覆盖层。P5 订阅 TDE 的 `TopDownEngineEvent(Pause)` 后自行处理 UI 展示(不经由 F6,`tde-integration.md:373`)。

**Specification**:
- 触发:玩家主动暂停,或音频设备异常自动暂停(1 帧内,`game-concept.md:454`)
- 暂停期间节奏判定暂停;覆盖层含继续/设置/退出等选项(继承 P-01 导航)
- 音频异常暂停:附 UI 提示通知玩家,启动视觉节拍指示器作为备用参考,恢复音频后从暂停位置续播(`game-concept.md:454`)
- 背景:游戏画面暗化但不完全遮挡(保留上下文)

**Accessibility**:全手柄/键盘可操作;自动暂停提示有明确文字说明。
**When to Use**:需要中断游戏并保留状态的场景。
**When NOT to Use**:局末结算(用 P-10,不可恢复游戏)。

---

### P-08 校准小游戏(Latency Calibration Mini-flow)

**Category**: Input
**Used In**: 延迟校准(FE3),首次进入强制

**Description**:测量玩家音频/输入延迟偏移的引导式小交互。首次进入游戏强制执行,消除无线耳机 100-300ms 延迟对判定体系的破坏(`game-concept.md:294, 438`)。是玩家的**第一分钟体验**,体验不佳会直接劝退(`game-concept.md:293`)。

**Specification**:
- 玩家跟随节拍输入,系统采集偏差,计算 `player_latency_offset_ms`(`game-concept.md:435`)
- 校准公式:`calibrated_input_time = raw_input_time − player_latency_offset_ms`
- 高延迟兜底:检测到 > 校准范围时提示「建议使用有线耳机」,允许**手动输入偏移值**(`game-concept.md:449`)
- 可跳过:跳过用 0ms 默认 + 持续「建议校准」标识(P-06),不阻断(`game-concept.md:448`)
- 偏移量持久化:经 F5 存 `LatencyCalibrationDTO`(`save-persistence.md`)

**Accessibility**:提供手动偏移输入作为无法完成节拍校准玩家的替代;流程清晰、可跳过、低压力。
**When to Use**:首次启动;设置中重新校准。
**When NOT to Use**:每局开始(校准偏移持久化,不重复)。

---

### P-09 键位重绑定(Rebind Capture)

**Category**: Input
**Used In**: 设置菜单(FE7)

**Description**:让玩家自定义任意动作按键的重绑定交互。FE7 调用 F3 的 `SaveBindingOverridesAsJson()` / `LoadBindingOverridesFromJson()`,F3 不感知 UI 层(`input-event-system.md:361`)。

**Specification**:
- 玩家选中一个动作 → 进入「捕获」态 → 按下新键 → 确认覆盖
- 冲突处理:新键已绑定其他动作时提示冲突,让玩家决定交换或取消(规格待 FE7 UX spec 细化)
- 持久化:覆盖以 JSON 存 `GameSettingsDTO`(F5)
- 支持键鼠与手柄两套绑定方案

**Accessibility**:全键位可重映射是运动无障碍核心(`accessibility-requirements.md` §4b);捕获态有清晰视觉/文字提示。
**When to Use**:设置菜单输入选项。
**When NOT to Use**:游戏内实时(仅设置菜单)。

---

### P-10 局末结算(Run Summary)

**Category**: Data Display
**Used In**: 死亡/通关结算屏

**Description**:单局结束(死亡或通关)后的结算展示。呈现本局成绩 + 功勋转化,承接「下次一定」的复盘冲动(`game-concept.md:145, 164`)。

**Specification**:
- 展示:本局统计(连击纪录、判定分布等)+ 功勋累积/转化
- 死亡惩罚低:功勋保留,下局立刻可开始(`game-concept.md:145`)
- 导航:继承 P-01;主要动作「再来一局」焦点默认高亮
- Off 模式:功勋按 `meritMultiplierOff = 0.5` 折算展示(`rhythm-core.md:755`),结算屏须让 Off 玩家清楚看到折算(与排行/成就隔离一致)

**Accessibility**:全手柄/键盘可操作;数据以文字+数字呈现,不仅靠图表颜色。
**When to Use**:单局结束。
**When NOT to Use**:局内暂停(用 P-07)。

---

## Gaps & Patterns Needed

现有 GDD 尚未确立、但下游 UX spec 可能需要的模式(留待各屏 spec 撰写时回填):

- **可滚动列表 / 网格**:武器解锁、心法图鉴等超过 ~5 项的选择(P-02 仅覆盖固定 3 卡)
- **瞬时通知(Toast)**:非持续的一次性提示(区别于 P-06 常驻标识)
- **二元确认弹窗(Confirm Dialog)**:退出/覆盖存档等破坏性操作确认
- **滑块/数值设置控件**:音量、UI 缩放、判定窗口档、手动延迟偏移(设置菜单)
- **加载/等待态**:如有场景加载等待(MVP 线性拼接房间,可能极简)
- **主菜单导航结构**:开始/继续/设置/退出(待主菜单 UX spec)

> **一致性提醒**:上述任一模式在首个使用它的 UX spec 中定稿后,须回填本库,避免各屏各自发明(gate-check 交叉引用要求)。

---

## Open Questions

- **平台与触控**:触控/移动端等价目前作为前向兼容保留项,是否为发行目标未定——见 `accessibility-requirements.md` §9 平台冲突。若确认纯 PC+手柄,可移除各模式的「触摸等价」注记以简化。
- **稀有度色 vs 非颜色依赖**(P-02):稀有度的非颜色冗余(边框形态 or 文字标签)具体形式待 P2 心法选择 UX spec 定稿,需 art-director 参与。
- **HUD 布局整体规格**:P-03/04/05/06 均为 HUD 元素,其空间排布、优先级、墨气浓度下的动态不透明度(`art-bible.md:405`)属 HUD 设计范畴——建议下一步 `/ux-design hud` 产出 `design/ux/hud.md`,本库仅定义单个元素的交互规格。
- **结算屏 Off 折算呈现**(P-10):Off 功勋 0.5 折算如何在结算屏清晰呈现而不让无障碍玩家感到「被惩罚」,需 UX 措辞打磨。
