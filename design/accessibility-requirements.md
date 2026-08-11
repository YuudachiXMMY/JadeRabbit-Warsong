# Accessibility Requirements — 玉兔:破阵 (Jade Rabbit: Warsong)

> **Status**: Committed (Pre-Production gate prerequisite)
> **Author**: user + ux-designer
> **Last Updated**: 2026-08-11
> **Template**: Accessibility Requirements
> **Applies to**: All player-facing UI, HUD, and the core rhythm-combat loop

---

## Committed Tier

> **本项目正式提交的无障碍等级:WCAG 2.2 Level AA(视觉/交互基线) + 节奏动作类型专属补偿(genre-specific accommodations)。**

- **基线**:所有屏幕、HUD、菜单满足 WCAG 2.2 **Level AA** 的可适用成功准则(对比度、文字缩放、非颜色依赖、键盘可达、焦点可见)。此基线承接 `design/art/art-bible.md` 已有的 WCAG AA 承诺(正文对比度 > 7:1、色觉无障碍方案),不新增与现有 GDD 冲突的承诺。
- **类型专属补偿**:节奏动作游戏对**运动精度**与**听觉时机**有天然门槛,标准 WCAG 无法覆盖。本作以五项专属机制补偿(见 §4):Off 免节奏通道、判定窗口缩放、视觉/触觉节拍冗余、延迟校准、全键位重映射。
- **不可豁免项**:`design/gdd/rhythm-core.md` 的 **Off 模式五项无障碍合约**、`art-bible.md` 的**判定反馈色觉安全方案**、以及本文 §4 的判定信息三重冗余(颜色 + 形态 + 声音 + 文字兜底),为不可删除的硬性要求。

**等级依据(为何是 AA 而非 A 或 AAA)**:
- 低于 AA(仅 Basic)会与 art-bible 已写入的 WCAG AA 对比度承诺产生文档级分歧,后续 `/ux-review` 会挑出「承诺缩水」。
- AAA 的部分条款(如「无时间限制」1.4.x / 2.2.x)与「攻击必须踩拍」的核心机制**根本冲突**——节奏层是 Anti-Pillar「不可绕过」的立身之本(`game-concept.md:184, 213`)。以 Off 模式作为 AAA 精神的**等价替代通道**,而非在核心循环上追 AAA 字面合规。

---

## Player Fantasy(无障碍视角)

无障碍不是「为残障玩家单独开的降级模式」,而是让**尽可能多的玩家都能体验到「战斗即演奏」的核心快感**。一位手部精细控制受限的玩家,应当仍能通过 Off 通道感受连招流畅度;一位听障玩家,应当仍能通过屏幕边缘的 BPM 呼吸光晕「看见」节拍;一位色盲玩家,应当仍能凭字号与音效分辨 Perfect 与 Miss。核心原则(承接 `art-bible.md:409`):

> **颜色是第一信息层;形态、动画、声音、文字是必须同时存在的冗余信息层。任何核心判定信息不得仅靠单一感官通道传达。**

---

## Scope:MVP vs Polish(PL4)

无障碍需求分两阶段落地,避免 MVP 范围膨胀,同时保证 Pre-Production gate 有明确的「已提交等级」。

| 需求 | MVP(M0-M2) | Polish / Full Vision(PL4) | 依据 |
| ---- | ---- | ---- | ---- |
| Off 免节奏通道 | ✅ 必含(核心循环无障碍) | 沿用 | `rhythm-core.md:49-58`;MVP 定义含延迟校准+基础 HUD |
| 判定反馈色觉安全(颜色+形态+音效+文字) | ✅ 必含 | 沿用 | `art-bible.md:425-433` |
| 延迟校准流程 | ✅ 必含(首次强制) | 增加手动偏移输入 | `game-concept.md:432-438, 448-449` |
| 文字对比度 WCAG AA | ✅ 必含 | 沿用 | `art-bible.md:388` |
| 全键位重映射 UI | ⚠️ F3 底层就绪(`SaveBindingOverridesAsJson`),FE7 重绑定 UI 属 Full Vision | ✅ 完整重绑定面板 | `input-event-system.md:361`;`systems-index.md:53` |
| UI 缩放 / 文字缩放 | ⚠️ 架构预留(`AccessibilityDTO`) | ✅ 完整实现 | `save-persistence.md:261-269` |
| 颜色无障碍模式切换(色盲档) | ⚠️ 架构预留 | ✅ 完整实现 | `save-persistence.md:269`;PL4 |
| 减弱动效(reduced motion) | ⚠️ 架构预留 | ✅ 完整实现 | 本文 §4 |
| 屏幕朗读 / 读屏支持 | ❌ 不承诺(见 Open Questions) | 评估 | 本文 §9 |

> **PL4 无障碍设置系统**(`design/gdd/accessibility-settings.md`,Not Started)是本文需求的实现载体。本文定义**要求什么**;PL4 GDD 定义 `AccessibilityDTO` 的具体字段(颜色模式枚举、UI 缩放比例范围、减弱动效开关)与设置面板交互。F5 存档层仅存取原始值(`save-persistence.md:261-269`)。

---

## 4. Requirements by Category

### 4a. 视觉(Visual)— WCAG AA 基线

- **文字对比度**:所有正文/说明文字对比度 ≥ 4.5:1;大号文字 ≥ 3:1。本作正文(深墨 `#2A2820` on 宣纸白 `#F5F0E8`)已达 > 7:1(`art-bible.md:388`)。**禁止**在淡墨 `#7A7466` 上放正文(对比度不足 AA — `art-bible.md:329`)。
- **UI 面板可读性**:墨气浓度 > 65% 的场景(精英/Boss),UI 面板底色自动升至 95% 不透明度(`art-bible.md:405`),保证前景文字始终达标。
- **非颜色依赖**:无任何核心信息仅靠颜色区分(WCAG 1.4.1)。三色语义系统(绛红/石青/金)每一色都配形态或动画冗余(`art-bible.md:409`)。
- **色觉无障碍**:承接 art-bible §五全部方案。判定反馈色觉安全表(`art-bible.md:427-433`)为不可删除要求:
  - Perfect(金):全屏亮度峰值 1 帧 + 最大字号 + 编钟音效 + 「Perfect」文字
  - Good(石青):节奏环轻闪 + 中等字号 + 轻音效 + 「Good」文字
  - Miss(绛红):屏幕边缘绛红晕染 + 中等字号 + 钝音 + 「Miss」文字
  - **三个判定的文字标签是纯文字兜底层,任何情况下不可移除**(`art-bible.md:433`)。
- **禁止绿色语义色**:绿色与石青对红绿色盲玩家易混淆,明确禁止引入(`art-bible.md:355`)。
- **文字缩放**(PL4):UI 支持文字/元素缩放,布局在放大时不裁切、不重叠(WCAG 1.4.4)。架构由 `AccessibilityDTO.uiScale` 预留。

### 4b. 运动 / 输入(Motor / Input)

- **无需精细计时的通道**:**Off 模式**是运动无障碍的核心——攻击输入到达即评 Perfect,不依赖踩拍精度(`rhythm-core.md:49, 111`)。这是本作对 WCAG 2.2.x「时间限制」精神的等价替代。
- **全键位重映射**:F3 输入层已提供 `SaveBindingOverridesAsJson()` / `LoadBindingOverridesFromJson()`(`input-event-system.md:361`);FE7 设置菜单提供重绑定 UI(Full Vision)。所有游戏动作可重映射。
- **圆形死区**:手柄摇杆采用圆形幅值死区(`StickDeadzoneProcessor`),避免轴向偏置影响运动受限玩家(`input-event-system.md:355`)。
- **无强制连打**:核心循环不要求高频连打;连招取消窗口替代后摇,Off 下 `CancelTier` 恒为 Standard 保证流畅手感(`rhythm-core.md:58`)。
- **全键盘/手柄可达**:所有 UI 通过 F3 UI Action Map(Navigate/Submit/Cancel)可达,无鼠标专属操作(WCAG 2.1.1)。焦点顺序逻辑清晰,焦点指示器可见(石青描边 + BPM 呼吸,`art-bible.md:403`)。

### 4c. 听觉(Auditory)

- **视觉节拍冗余**:节拍不得仅靠 BGM 传达。HUD 节奏指示器 + 屏幕边缘 BPM 呼吸光晕提供视觉节拍(`game-concept.md:231` — 「玩家关掉音量也应感受到节奏」)。
- **音频设备异常兜底**:音频不可用时立即暂停(1 帧内),启动视觉节拍指示器作为备用参考,恢复后续播(`game-concept.md:454`)。
- **判定音效非唯一通道**:每个判定评级都有颜色 + 形态 + 文字冗余,听障玩家不依赖编钟/钝音也能读判定(见 §4a)。
- **字幕**(Full Vision):叙事对话系统(推迟到 Full Vision)须提供文字,不依赖语音。MVP 无对话系统,本项不适用。

### 4d. 认知(Cognitive)

- **强制延迟校准**:首次进入强制执行延迟校准小游戏,消除无线耳机 100-300ms 延迟对判定的破坏(`game-concept.md:294, 438`)。跳过则用 0ms 默认偏移 + HUD「建议校准」提示,不阻断游戏(`game-concept.md:448`)。
- **线性引导房**:前 3 房固定 BPM 100 + 宽容判定窗,降低三层学习压力(节奏精度 + 连招 + 心法)的叠加冲击(`game-concept.md:142, 289`)。
- **即时清晰反馈**:判定即时文字 + 颜色分级 + early/late 方向提示,让玩家理解「我为什么没踩准」(`game-concept.md:144`)。
- **低死亡惩罚**:死亡即时结算、功勋保留、立刻重开,降低挫败(`game-concept.md:145`)。

### 4e. 节奏 / 时机专属(Rhythm-Specific — 类型补偿核心)

这是标准 WCAG 无法覆盖、本作独有的无障碍表面:

1. **Off 免节奏通道**:完整五项合约不可豁免(`rhythm-core.md:53-58`):①入口仅在无障碍设置面板 ②HUD 全程显示「无障碍模式」标识且不可关闭 ③排行/成就隔离 ④功勋折算系数 `meritMultiplierOff = 0.5` ⑤连招深度封顶 Standard。设计意图:让 Off 成为**真无障碍通道而非绕过节奏的最优刷取路径**(`rhythm-core.md:687`)。
2. **判定窗口缩放**:引导房宽容窗 → 后期收窄(`game-concept.md:490` Perfect 窗口 [40ms,100ms] 可调)。为运动/反应受限玩家,PL4 可提供更宽判定档(在 HalfBeat/Beat 内,区别于 Off)。
3. **视觉 + 触觉节拍冗余**:BPM 呼吸视效 + 手柄震动节拍(Feel/NiceVibrations),三通道(听/视/触)任缺其一仍可玩。
4. **延迟校准 + 手动偏移**:自动校准 + 高延迟时允许手动输入偏移值(`game-concept.md:449`)。
5. **减弱动效**(PL4):Perfect 动力学变化含冻帧/摄像机微切(`game-concept.md:377-386`);提供 reduced-motion 档降低闪烁/推拉幅度,防止前庭不适(WCAG 2.3.3)。判定信息在减弱动效下仍靠字号+文字+音效可读。

---

## 5. WCAG 2.2 AA 成功准则映射(可适用项)

| WCAG 准则 | 等级 | 本作落地 | 状态 |
| ---- | ---- | ---- | ---- |
| 1.4.1 使用颜色 | A | 判定/敌我识别均有非颜色冗余(补子形态、动画、字号) | ✅ art-bible |
| 1.4.3 对比度(最小) | AA | 正文 > 7:1;禁淡墨作文字底 | ✅ art-bible |
| 1.4.4 调整文本大小 | AA | UI 缩放(`AccessibilityDTO.uiScale`) | ⚠️ PL4 |
| 1.4.11 非文本对比度 | AA | 焦点描边石青 + BPM 呼吸,UI 面板动态不透明度 | ✅ art-bible |
| 2.1.1 键盘 | A | 全 UI 经 UI Action Map 可达,无鼠标专属 | ✅ F3 |
| 2.2.x 时间可调 | AA/AAA | Off 通道作为「攻击需踩拍」时限的等价替代 | ✅ 类型补偿 |
| 2.3.3 交互动画 | AAA→采纳 | reduced-motion 档 | ⚠️ PL4 |
| 2.4.7 焦点可见 | AA | 石青焦点描边 + BPM 呼吸闪烁 | ✅ art-bible |
| 2.5.1 指针手势 | A | 无多指/路径手势依赖 | ✅ |

> ⚠️ = 架构已在 `AccessibilityDTO` / F3 预留,完整实现属 PL4(Polish)。Pre-Production gate 只要求**等级已提交且已文档化**,不要求全部实现完成。

---

## 6. Dependencies(双向)

本文依赖并被以下系统反向引用:

| 关联系统 | 文件 | 关系 |
| ---- | ---- | ---- |
| 艺术规范(色觉/对比度) | `design/art/art-bible.md` §四/§五 | 视觉无障碍来源;本文承接其 WCAG AA 承诺 |
| 节奏核心(Off 合约) | `design/gdd/rhythm-core.md` | Off 五项合约为本文不可豁免项 |
| 存档持久化(AccessibilityDTO) | `design/gdd/save-persistence.md` PL4→F5 | 无障碍偏好存取载体 |
| 输入事件(重映射/死区) | `design/gdd/input-event-system.md` | 键位重绑定与手柄死区 |
| 无障碍设置系统 | `design/gdd/accessibility-settings.md`(PL4,Not Started) | 本文需求的实现 GDD;定义 DTO 字段与面板 |
| 心法/HUD UI | `design/gdd/perk-system.md`, `combo-system.md` | d-pad 导航 + 焦点可见承接本文 |
| 交互模式库 | `design/ux/interaction-patterns.md` | 每个模式携带本文的无障碍要求 |
| 各屏 UX spec | `design/ux/*.md`(待创建) | 须对齐本文提交的等级(gate-check:172) |

---

## 7. Acceptance Criteria(可验证)

- [ ] **A11Y-1(等级已提交)**:本文明确声明 WCAG 2.2 AA + 类型补偿为提交等级,gate-check「tier is defined and documented」通过。
- [ ] **A11Y-2(判定三重冗余)**:关闭音量 + 灰度滤镜下,测试者仍能 100% 分辨 Perfect/Good/Miss(靠字号 + 形态 + 文字)。
- [ ] **A11Y-3(Off 通道可达且隔离)**:Off 仅在无障碍设置面板可开;开启后 HUD 持续显示标识且不可关闭;功勋按 0.5 折算;`CancelTier` 封顶 Standard;不进排行/成就。
- [ ] **A11Y-4(键盘/手柄全可达)**:不使用鼠标,仅键盘或手柄可完成:主菜单 → 开局 → 战斗 → 心法三选一 → 暂停 → 设置重绑定 → 退出的完整路径,每个交互元素有可见焦点。
- [ ] **A11Y-5(对比度达标)**:所有正文文字对比度 ≥ 4.5:1(工具实测);墨气 > 65% 场景 UI 面板不透明度自动升至 95%。
- [ ] **A11Y-6(延迟校准保护)**:设置 100ms 人工延迟,完成校准后 Perfect 命中率 ≥ 未延迟时的 80%(`game-concept.md` AC-5)。
- [ ] **A11Y-7(视觉节拍冗余)**:静音状态下,HUD 节奏指示器 + 屏幕边缘光晕仍随 BPM 呼吸,玩家可凭视觉预读敌人落拍。

---

## 8. Verification / Testing

- **MVP gate**:A11Y-1/2/3/4/6/7 为 Pre-Production 可验证项(A11Y-5 部分依赖美术资源,MVP 用代理资源近似)。
- **手段**:灰度 + 静音 + 纯键盘/纯手柄的「三关模拟」是本作无障碍回归的标准夹具(见 A11Y-2/4/7)。
- **工具**:对比度用取色器实测;色盲模拟用 Deuteranopia/Tritanopia/Protanopia 三档滤镜(承接 art-bible 风险对分析)。
- **责任**:`accessibility-specialist` agent 负责合规审查;`unity-ui-specialist` 负责焦点/缩放/动态不透明度实现。

---

## 9. Open Questions

- **平台冲突(需 owner 裁定)**:`game-concept.md:61` 定平台为 **PC(Win/macOS) + 完整手柄**(单平台,单人);但 `technical-preferences.md` 模板写 PC/Console/Mobile + 触控,且多份 GDD 据此写了「d-pad + 触摸等价(跨平台约束)」。本文以 game-concept(愿景权威)为准,主输入定为**键鼠 + 手柄**,触控作为前向兼容的低成本保险保留,但**主机 d-pad 安全区与移动端触控无悬停等价是否为发行目标未定**。建议在 `/setup-engine` 中对齐 `technical-preferences.md` 的 Target Platforms 与 game-concept,消除文档级分歧。
- **屏幕朗读(读屏)范围**:本作重度依赖实时视觉 + 音频判定,全盲读屏支持在节奏动作核心循环上不现实。菜单层读屏是否纳入 Full Vision 待评估(参考:多数动作游戏仅对菜单做读屏)。当前不承诺,不阻塞 gate。
- **判定窗口无障碍档的边界**:「更宽判定档」(§4e-2)如何与量化档位(HalfBeat/Beat)及 Off 通道区分,避免变成第二条战力捷径?须待 M0 Spike 输出判定窗口数据后,由 rhythm-core + 本文联合锁定,先于 PL4 成文。
- **减弱动效与 Perfect 一体感的权衡**:reduced-motion 关掉冻帧/摄像机微切后,Perfect 的「可感知物理现象」(`game-concept.md:377`)如何保留辨识度?需在 PL4 明确 reduced-motion 下 Perfect 的替代强反馈(建议:保留编钟音效 + 最大字号 + 静态金光,去掉运动分量)。
