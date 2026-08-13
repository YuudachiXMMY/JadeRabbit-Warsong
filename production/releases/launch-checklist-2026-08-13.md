# Launch Checklist: JadeRabbit-Warsong（玉兔·战歌）

Target Launch: **TBD（pre-production 草稿）**
Generated: 2026-08-13

> ⚠️ 项目当前处于 pre-production（`Assets/Scripts` 仅 Core/Shims，无可发布构建）。本清单为
> **前置基线**，多数硬件/商店/运维项目前不适用，标注为 N/A（未到阶段）。
> **字体授权核对**为本次重点纳入项，含 1 个阻塞项，与 `design/art/font-licensing.md` 验收条件对齐。

---

## 1. 代码就绪 Code Readiness

### Build Health
- [ ] 所有目标平台干净构建（PC/Console/Mobile） — **N/A（无构建）**
- [ ] 零编译警告
- [ ] 单元测试全绿（EditMode + PlayMode，CI `tests.yml` 已就位）
- [ ] 集成测试全绿
- [ ] 性能基准达标（60fps PC/Console，30fps Mobile；见 `technical-preferences.md` 预算）
- [ ] 无内存泄漏（soak test 验证）
- [ ] 构建体积在平台上限内
- [ ] 构建版本号正确并在源码打 tag

### Code Quality（当前实测 2026-08-13）
- [x] TODO = **0**（Assets/Scripts）
- [x] FIXME = **0**
- [x] HACK = **0**
- [x] 生产代码无 Debug.Log / Console.WriteLine（当前 0）
- [ ] 无硬编码 dev/test 值（localhost、测试凭证、debug flag）
- [ ] 所有 feature flag 设为生产值
- [ ] 崩溃上报接入并验证

### Security
- [ ] 源码无暴露的 API key / 凭证
- [ ] 存档加密（EasySave 3，PlayerInfoSO；ES3Cloud 不启用）
- [ ] 网络通信 TLS/DTLS（如启用联网）
- [ ] 输入校验（服务端，如联机）

---

## 2. 内容就绪 Content Readiness

### Assets
- [ ] 全部 placeholder 美术替换为终稿
- [ ] 全部 placeholder 音频替换为终稿（Feel/MMFeedbacks 反馈序列）
- [ ] 音频 mix 定版并经 audio-director 签核
- [ ] 无缺失/损坏资产引用
- [ ] 资产命名规范执行（PascalCase）

### Text, Localization & Fonts ⭐（字体授权核对重点）
- [ ] 玩家可见文本全部校对
- [ ] 无硬编码字符串（全部外化以支持本地化）
- [ ] 各语言 UI 文本适配（text fitting 通过）
- [ ] **字体覆盖度：所有支持语言的字形完整（CJK 覆盖）**
- [ ] **🔴 阻塞：汉仪尚巍手书 商用授权已购并归档，或已替换为可商用手书体**
      （v1 教训直接对应项，`design/art/font-licensing.md` Table A）
- [ ] 思源宋体 OFL.txt 随字体归档至 `Assets/Fonts/SourceHanSerif/OFL.txt`
- [ ] 授权凭证目录 `design/art/font-licenses/` 已建立并齐全（**当前不存在**）
- [ ] 构建产物核查：**不含 Gotham-Light/Ultra**（TDE demo，🔴 商业字型）
- [ ] 构建产物核查：不含 3.3 节 6 组待核实字体
      （8-BIT WONDER / Big John / Bizon / Slash / orange kid / pixel）
- [ ] Build Settings 不含任何 TDE/Feel demo 场景（防误引用 demo 字体）
- [ ] Credits 完整准确（含字体授权归属）

### Game Content
- [ ] 所有关卡可从头玩到尾
- [ ] 教程流程完整并经新玩家测试
- [ ] 成就/奖杯实现并测试
- [ ] 存/读档对所有游戏状态正确
- [ ] 难度平衡并测试
- [ ] 结算/Credits 序列完整

---

## 3. QA Quality Assurance

- [ ] 全量回归通过
- [ ] 零 S1（Critical）缺陷
- [ ] 零 S2（Major）缺陷（或有据例外）
- [ ] Soak test 通过（8h+）
- [ ] 关键路径在各平台测试
- [ ] 边界场景（满存储 / 无网络 / 挂起恢复）
- [ ] 无障碍最低标准（重映射、文本缩放、色盲模式；见 `accessibility-requirements`）
- [ ] 分级证书（ESRB / PEGI / 区域）

---

## 4. 商店与发行 Store & Legal

### Legal（字体授权归此闭环）
- [ ] EULA 定版法务签核
- [ ] 隐私政策发布并链接
- [ ] **第三方授权归属完整——含字体授权（思源宋体 OFL / 汉仪商用凭证）**
- [ ] 音乐/音频授权核实
- [ ] 商标/IP 清权确认
- [ ] GDPR/CCPA 合规

### Store Pages
- [ ] 商店文案 / 截图 / 预告 / 主视觉定版
- [ ] 各区定价配置

---

## 5. 基础设施 Infrastructure — **N/A（单机为主，未到阶段）**

## 6. 社区与市场 Community & Marketing — 待 pre-launch 展开

## 7. 运营 Operations — 待 pre-launch 展开

---

## Go / No-Go Decision

**Overall Status: NOT READY（pre-production；字体授权含 1 阻塞项）**

### Blocking Items
1. **🔴 汉仪尚巍手书未授权** — 商用嵌入需向汉仪购买商用授权并归档，或替换为可商用手书体。
   无授权不可打包。
2. `design/art/font-licenses/` 凭证目录未建立，Table A 授权依据无归档。

### Conditional Items（有据可放行）
- Gotham 及 3.3 待核实字体：只要不被引用且构建产物核查通过即可放行
  （无需删源文件，避免破坏 TDE demo / 升级覆盖）。

### Sign-Offs Required
- [ ] Creative Director / Art Director — 字体视觉 + 授权
- [ ] Technical Director — 构建产物字体核查
- [ ] QA Lead — 质量与测试覆盖
- [ ] Producer — 排期与整体就绪
- [ ] Release Manager — 构建与部署就绪

---

## 字体授权核对交叉引用

| 项目 | 状态 | 依据 |
| --- | --- | --- |
| 思源宋体（Source Han Serif） | ✅ 安全 | SIL OFL 1.1；待归档 OFL.txt |
| 汉仪尚巍手书 | 🔴 高风险（阻塞） | 商业字库，需购授权或替换 |
| Gotham-Light/Ultra | 🔴 禁止打包 | Hoefler/Monotype 商业字型（TDE demo） |
| 8-BIT WONDER / Big John / Bizon / Slash / orange kid / pixel | ⚠️ 待核实 | personal-use 陷阱，取证前不可打包 |

> 完整登记表与构建卫生策略见 `design/art/font-licensing.md`。Release 前每次 `/launch-checklist` 必查。

---

## 生成记录

| 日期 | 生成方式 | 备注 |
| --- | --- | --- |
| 2026-08-13 | `/launch-checklist 纳入字体授权核对` | 前置基线草稿；字体授权贯通内容就绪/Legal/Go-No-Go 三处 |
