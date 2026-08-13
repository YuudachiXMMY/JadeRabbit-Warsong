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
| **思源宋体**（Source Han Serif / Noto Serif CJK） | HUD 数字、正文、标签（Bold/Regular/Light） | ✅ 安全 | SIL Open Font License 1.1（Adobe × Google 发布），允许嵌入与商用分发。**待办**：把 OFL 授权文本随字体文件一并归档到 `Assets/Fonts/SourceHanSerif/OFL.txt` |
| **汉仪尚巍手书**（HanYi ShangWei） | 标题 / 章节名 / Boss 名（72px、36px） | 🔴 高风险 | 汉仪（HanYi）是**商业字库**，游戏商用嵌入需向汉仪购买**商用授权**（通常按产品/平台计费，且区分买断 vs 年费）。art-bible:870 已注明"商用授权文件一并归档"——**授权文件目前不存在，必须在字型进入构建前取得并归档**，否则替换为可商用的手书体（如免费商用书法字型）。**这是 v1 教训的直接对应项，最高优先级。** |

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

### 3.3 待核实 —— display / 像素字体，多为 personal-use 陷阱，取证前不可打包

| 字体 | 疑点 | 位置 |
| --- | --- | --- |
| **8-BIT WONDER** | ⚠️ 常见标注 freeware/donationware，商用条款不明 | `TopDownEngine/Common/Fonts/8-BIT WONDER.TTF` |
| **Big John / bigjohn / BIG JOHN / UIToolkitDemo_BigJohn** | ⚠️ 存在免费版与商业 PRO 版同名混淆，需确认此副本的具体授权 | `TopDownEngine/Common/Fonts/`、`Feel/.../Demo/Common/Fonts/`、`Feel/FeelDemos/.../Fonts/` |
| **Bizon** | ⚠️ display 字体，授权不明 | `TopDownEngine/Common/Fonts/Bizon.otf` |
| **Slash** | ⚠️ display 字体，授权不明 | `TopDownEngine/Common/Fonts/Slash.ttf` |
| **orange kid**（两处副本） | ⚠️ 疑似 personal-use | `TopDownEngine/Common/Fonts/`、`.../MMTools/Foundation/MMAchievements/Fonts/` |
| **pixel** | ⚠️ 泛用名，具体字型与授权不明 | `TopDownEngine/ThirdParty/.../InventoryEngine/Fonts/pixel.ttf` |

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

- [ ] Table A 每个生产字体都有归档授权凭证于 `design/art/font-licenses/`
- [ ] **汉仪尚巍手书**：已购商用授权并归档，或已替换为可商用字型（阻塞项，对应 v1 教训）
- [ ] 思源宋体 OFL.txt 随字体文件归档
- [ ] Build Settings 不含任何 TDE/Feel demo 场景
- [ ] 构建产物核查：不含 Gotham 及 3.3 节任何字体
- [ ] `technical-preferences.md` Forbidden Patterns 已加"禁止打包未授权字体"规则
- [ ] Release checklist 纳入字体授权核对项

---

## 6. 复审记录

| 日期 | 审计人 | 变更 |
| --- | --- | --- |
| 2026-08-13 | Claude Code（初版审计） | 建表：Table A 生产字体 2 项（思源宋体✅ / 汉仪尚巍手书🔴）；Table B 插件 demo 字体 34 字型 / 53 文件；标注 Gotham 高风险、6 组待核实 |
