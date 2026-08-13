# 字体授权清单与审计（Font Licensing Register）

> **状态**: Active · **审计日期**: 2026-08-13 · **来源**: v1 教训（字体商用授权缺失）
> **负责人**: art-director / producer 会签 · **复审节奏**: 每次新增字体 + Release 前必查

---

## 0. 为什么有这份文档（v1 教训）

上一版项目在字体授权上踩过坑：**把没有商用授权的字体打进了构建**。字体是版权作品，
「装进 Unity 工程能编译」不等于「有权在商业游戏里嵌入/分发」。尤其两类陷阱：

1. **插件 demo 字体**——TopDownEngine / Feel 等插件的 demo 目录里塞满字体（含 Gotham
   这种高价商业字型），它们随插件包进来，但插件的 Asset Store 授权**不覆盖**这些字体的
   再分发。默认不清理，很容易在打包时被扫进构建。
2. **"免费"字体的 personal-use 陷阱**——很多展示体（display font）标"Free"，细看是
   *free for personal use only*，商用需另购。这类字体在结算/宣发阶段最容易翻车。

**铁律**：任何字体在进入构建前，必须在本表里有一行、且 `可商用打包` = ✅ 并附授权依据。
未确认的一律视为**不可打包**。

---

## 1. 风险分级图例

| 标记 | 含义 | 处置 |
| --- | --- | --- |
| ✅ 安全 | 有明确开源/商用授权依据（OFL / Apache / 已购商用授权文件归档） | 可打包 |
| ⚠️ 待核实 | 授权状态不明或疑似 personal-use，尚无归档依据 | **暂不可打包**，需取证 |
| 🔴 高风险 | 已知商业字型 / 明确禁止再分发 | **禁止打包**，必须购授权或替换 |

---

## 2. Table A —— 生产字体（art-bible 指定，游戏实际会用）

来源：`design/art/art-bible.md` § 字体方向。这些是**真正要嵌进游戏**的字型，授权优先级最高。

| 字体 | 用途 | 授权状态 | 依据 / 待办 |
| --- | --- | --- | --- |
| **思源宋体**（Source Han Serif / Noto Serif CJK） | HUD 数字、正文、标签（Bold/Regular/Light） | ✅ 安全 | SIL Open Font License 1.1（Adobe × Google 发布），允许嵌入与商用分发。授权正文已归档 `design/art/font-licenses/OFL-1.1.txt`。**待办**：随实际字体文件下载官方 `OFL.txt` 一并放入 `Assets/Fonts/SourceHanSerif/`（OFL 要求授权文本随字体分发）。 |
| **马善政毛笔楷书**（Ma Shan Zheng）★ 推荐替换 | 标题 / 章节名 / Boss 名（72px、36px）——接替汉仪尚巍手书 | ✅ 安全 | **SIL Open Font License 1.1**，Google 开源字体项目（`fonts.google.com/specimen/Ma+Shan+Zheng`、`github.com/googlefonts/mashanzheng`），个人/企业均可免费商用、可嵌入分发。毛笔楷书墨韵，覆盖《通用规范汉字表》。2026-08-13 联网核实 OFL（来源见 §7）。**注意**：单一字重；若 Boss 名需更重的墨迹冲击力，见下方备选。**最终视觉签核归 art-director。** |
| **汉仪尚巍手书**（HanYi ShangWei）✗ 弃用 | （原：标题 / Boss 名 72px、36px） | 🔴 弃用 | 汉仪为**商业字库**，商用嵌入需购授权且本项目未取得——**决定弃用，替换为马善政毛笔楷书（OFL）**。弃用记录归档 `design/art/font-licenses/hanyi-shangwei-NOT-PROCURED.md`。**这是 v1 教训的直接对应项：不购授权即不得进包。** |

> **备选（供 art-director 视觉取舍）**：
> - **演示佳墨**（汉呈）——更重的墨迹标题体，视觉冲击强；但**授权无法核实**（"演示"系列存在付费款，如演示新手书 ¥98），**取证确认可免费商用前不可采用**，暂标 ⚠️。
> - **霞鹜文楷**（LXGW WenKai）——✅ OFL 1.1、覆盖 1.3 万字含繁体，人文楷体；偏**正文阅读**而非标题墨迹，可作正文/UI 补充或思源宋体的备选，不建议用于 Boss 名冲击场景。

> **归档约定**：所有生产字体的授权凭证（OFL.txt / 汉仪商用授权 PDF / 发票）统一放
> `design/art/font-licenses/`，文件名与字体对应。Release 前 `/launch-checklist` 必须核对齐全。

---

## 3. Table B —— 插件捆绑 demo 字体（当前工程内 53 个文件 / 34 个字型）

这些**全部位于第三方插件的 demo 目录**，游戏本身不需要，但存在被误打包的风险。
处置原则：**默认全部排除出构建**，仅当某字型被正式选为生产字体时，才走 Table A 流程单独授权。

### 3.1 相对安全（开源授权，但仍不建议随游戏分发——非生产字体）

| 字体 | 授权 | 位置（插件 demo） |
| --- | --- | --- |
| **Lato**（全字重 22 款，含 MMInterface + MMTween 两处副本） | ✅ SIL OFL 1.1 | `TopDownEngine/.../MMInterface/Styles/Mini/Fonts/Lato/`、`.../MMTools/Demos/MMTween/Fonts/Lato/` |
| **Roboto Mono**（Bold/Light/Medium/Regular） | ✅ Apache License 2.0 | `Feel/NiceVibrations/Demo/Common/Fonts/` |
| **Liberation Sans** | ✅ SIL OFL 1.1（Unity TMP 默认字体，随 TextMesh Pro 分发） | `TextMesh Pro/Fonts/LiberationSans.ttf` |
| **UIToolkitDemo_Lato** | ✅ 同 Lato（OFL 1.1） | `Feel/FeelDemos/UIToolkitFeedbacksDemo/Fonts/` |

### 3.2 高风险 —— 已知商业字型，禁止打包

| 字体 | 授权 | 位置 |
| --- | --- | --- |
| **Gotham-Light / Gotham-Ultra** | 🔴 Hoefler & Co / Monotype 商业字型，桌面授权**不含**软件/游戏嵌入，逐产品另购且价格高昂 | `TopDownEngine/Common/Fonts/` |

### 3.3 待核实字体 —— 2026-08-13 逐一联网核实结论

> 核实方法：联网检索作者/字库出处与授权条款（来源见 §7）。第三方字体站的授权标注不可靠，
> 以下结论**以"能否在游戏中商用嵌入"为准**；凡"仅个人用" / "仅桌面授权、嵌入需另购" / "来源冲突"者，
> 一律**不可打包**。提醒：本节字体全部位于插件 demo 目录，构建卫生（§4）已将其排除出包体，
> 此核实为尽职调查与留档。

| 字体 | 核实结论 | 依据 |
| --- | --- | --- |
| **Slash** | 🔴 **确认禁止** | dafont 两款同名 Slash（NA Creative / Din Studio）均为 *Free for Personal Use only*，商用须购授权。**明确禁止商用嵌入。** |
| **Bizon** | 🔴 **视为禁止**（来源冲突） | Fago Studio / Axel Bizon；部分站点标 free commercial，另有站点标 personal-use-only、商用需购。冲突即按不安全处理，商用前须向作者（bonjour@axelbizon.com）书面确认。 |
| **orange kid**（两处副本） | ⚠️ **嵌入需另购** | Typodermic / Ray Larabie 像素体：桌面授权可免费商用，但**在 App/游戏中嵌入不在桌面授权范围**，需单独嵌入授权。另存在 Jayvee Enaguas 同名衍生版（授权不同），须先确认工程内副本来源。嵌入前不可用。 |
| **8-BIT WONDER** | ⚠️ **待读 readme** | Joiro Hatgaya（2001）版权注记 "Freeware, but look at readme file"。聚合站多标可商用，但**权威条款在随附 readme**，工程内副本未带 readme → 未取证前不可打包。 |
| **Big John / bigjohn / BIG JOHN / UIToolkitDemo_BigJohn** | ⚠️ **待作者源确认** | Ion Lucin 作品；聚合站多标个人+商用免费（含 Big John PRO），但第三方标注不可靠，须以作者/字库官方发布为准。多副本同名，需逐一核对版本。确认前不打包。 |
| **pixel** | ⚠️ **无法识别** | 泛用名（`InventoryEngine/Fonts/pixel.ttf`），无稳定作者/字库出处，无法定位授权。默认不可打包。 |

**位置对照**（核实不改变构建处置，均排除出包）：`TopDownEngine/Common/Fonts/`（8-BIT WONDER.TTF / Bizon.otf / Slash.ttf / Big John / orange kid）、`Feel/.../Demo/Common/Fonts/` 与 `Feel/FeelDemos/.../Fonts/`（Big John 副本）、`.../MMTools/Foundation/MMAchievements/Fonts/`（orange kid 副本）、`TopDownEngine/ThirdParty/.../InventoryEngine/Fonts/pixel.ttf`。

**核实净结论**：**Slash 由 ⚠️ 升级为 🔴 确认禁止**；Bizon 因来源冲突同按禁止处理；orange kid / 8-BIT WONDER / Big John / pixel 仍未取得"可游戏嵌入"依据，维持不可打包。无一字型因本次核实转为 ✅。

---

## 4. 构建卫生（Build Hygiene）—— 如何确保 demo 字体不进包

Unity 只有被**引用**的资产才会进构建。字体进包的三条路径，逐条封堵：

1. **被 Build Settings 中的 Scene 引用** —— 保持插件 demo 场景**不加入** Build Settings。
   生产 UI 只引用 `Assets/Fonts/` 下经 Table A 授权的字体。
2. **在 `Resources/` 目录** —— 本项目无自建 `Assets/Fonts`，也不要把任何 demo 字体拷进
   `Resources/`（Resources 全量打包，见 directory-structure 约束）。
3. **被 Addressable 标记** —— 不要把 demo 字体加入任何 Addressable Group。

**验证手段**：构建后用 Unity 的 *Build Report* / Addressables 分析，或解包检查最终产物中
不含 Gotham 及 3.3 节任何待核实字体。Release 前由 `/launch-checklist` 执行该检查。

> **可选清理**：demo 字体属 TDE/Feel 包内文件，直接删除会在插件升级时被覆盖回来，
> 且可能破坏 demo 场景。**不建议直接删源文件**（与"禁止修改 TDE 源码"精神一致），
> 改为通过"不引用 + 构建产物核查"来隔离。

---

## 5. 验收条件（Acceptance Criteria）

- [x] `design/art/font-licenses/` 归档目录已建立（README + OFL-1.1.txt + 汉仪弃用记录）— 2026-08-13
- [x] **汉仪尚巍手书**：决定弃用，替换为马善政毛笔楷书（OFL 免费商用）— 2026-08-13（阻塞项已解，对应 v1 教训）
- [ ] 马善政毛笔楷书实际字体文件 + 官方 OFL.txt 随构建归档；art-director 完成 Boss 名视觉签核
- [ ] 思源宋体官方 OFL.txt 随字体文件归档到 `Assets/Fonts/SourceHanSerif/`
- [ ] Build Settings 不含任何 TDE/Feel demo 场景
- [ ] 构建产物核查：不含 Gotham 及 §3.2/§3.3 任何字体（Slash/Bizon 已确认禁止）
- [x] `technical-preferences.md` Forbidden Patterns 已加"禁止打包未授权字体"规则
- [ ] Release checklist 纳入字体授权核对项

---

## 6. 复审记录

| 日期 | 审计人 | 变更 |
| --- | --- | --- |
| 2026-08-13 | Claude Code（初版审计） | 建表：Table A 生产字体 2 项（思源宋体✅ / 汉仪尚巍手书🔴）；Table B 插件 demo 字体 34 字型 / 53 文件；标注 Gotham 高风险、6 组待核实 |
| 2026-08-13 | Claude Code（第二轮） | ① 汉仪尚巍手书弃用 → 替换为马善政毛笔楷书（OFL），列备选演示佳墨⚠️/霞鹜文楷✅；② 3.3 六组逐一联网核实：Slash 升 🔴 确认禁止、Bizon 来源冲突按禁止、orange kid 嵌入需另购、8-BIT WONDER/Big John 待作者源确认、pixel 无法识别；③ 建立 `font-licenses/` 归档目录（README+OFL-1.1.txt+汉仪弃用记录）；④ 新增 §7 取证来源 |

---

## 7. 取证来源（2026-08-13 联网核实）

- 马善政毛笔楷书 OFL：Google Fonts `fonts.google.com/specimen/Ma+Shan+Zheng`、`github.com/googlefonts/mashanzheng`；猫啃网 / 100font 等转述 OFL 1.1
- 霞鹜文楷 OFL：`github.com/lxgw/LxgwWenKai`（SIL OFL 1.1，保留名「霞鹜」「LXGW」）
- 演示佳墨：汉呈"演示"系列，未见免费商用官方条款，同系列存在付费款（演示新手书 ¥98）→ 未取证
- Slash：dafont（NA Creative / Din Studio 两款同名）均 Free for Personal Use only
- Bizon：Fago Studio / Axel Bizon（`axelbizon.com`），聚合站来源冲突（free commercial vs personal-use-only）
- orange kid：Typodermic / Ray Larabie，桌面授权免费商用、嵌入 App/游戏需另购；另有 Jayvee Enaguas 同名衍生版
- 8-BIT WONDER：Joiro Hatgaya（2001），版权注记 "Freeware, but look at readme file"
- Big John：Ion Lucin，聚合站标个人+商用免费（含 Big John PRO），需作者源确认

> 说明：第三方字体聚合站的授权标注为自动生成、可能不全，最终授权须以作者/字库官方发布或随附 EULA/readme 为准。
