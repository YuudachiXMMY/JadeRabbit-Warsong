# 字体授权凭证归档（Font License Archive）

本目录存放**进入构建的字体**的授权凭证，供 Release 前 `/launch-checklist` 与发行法务核查。
权威清单在 `../font-licensing.md`；本目录只放"凭证文件"。

> **铁律**：任何字体进构建前，必须在 `font-licensing.md` 登记为 ✅，且其授权凭证在此目录留档。
> 未留档 = 不可打包（v1 教训：未授权字体被打进商业构建）。

## 归档清单

| 字体 | 授权 | 凭证文件 | 状态 |
| --- | --- | --- | --- |
| 思源宋体 Source Han Serif | SIL OFL 1.1 | `OFL-1.1.txt`（授权正文） | ✅ 正文已归档；**待办**：随实际字体文件补入官方 `OFL.txt`（含 Adobe 版权行） |
| 马善政毛笔楷书 Ma Shan Zheng（汉仪尚巍手书替换） | SIL OFL 1.1 | `OFL-1.1.txt`（授权正文） | ✅ 正文已归档；**待办**：从 Google Fonts / GitHub 下载字体时补入官方 `OFL.txt`（含作者版权行） |
| 霞鹜文楷 LXGW WenKai（正文备选） | SIL OFL 1.1 | `OFL-1.1.txt`（授权正文） | ⏸ 采用后再补官方 `OFL.txt` |
| 汉仪尚巍手书 HanYi ShangWei | 商业字库（未购） | `hanyi-shangwei-NOT-PROCURED.md` | 🔴 **弃用**：未购授权，已替换，记录留档 |

## 归档约定

1. **OFL 字体**：OFL 1.1 的**法律正文对所有 OFL 字体相同**，统一见 `OFL-1.1.txt`；各字体的**版权行 / Reserved Font Name** 见下表。发行打包时，须把每款字体**官方发布包内的 `OFL.txt`**（含其专属版权行）随字体文件一并放入 `Assets/Fonts/<字体>/`——OFL 要求授权文本随字体分发。
2. **商业字体**：购买后把**发票 + 授权书（PDF）**以 `<字体名>-license.pdf` 命名放入本目录，并在 `font-licensing.md` 标 ✅。
3. **弃用字体**：不购授权者，写一份 `<字体名>-NOT-PROCURED.md` 记录决策（弃用/替换），避免日后误用。

### OFL 字体的版权行与 Reserved Font Name

- **思源宋体 Source Han Serif**：`Copyright 2014-2021 Adobe (http://www.adobe.com/)`，Reserved Font Name **"Source"**。
- **马善政毛笔楷书 Ma Shan Zheng**：`Copyright 2018 The Ma Shan Zheng Project Authors (https://github.com/googlefonts/mashanzheng)`。
- **霞鹜文楷 LXGW WenKai**：`Copyright (c) 2021, 落霞孤鹜 (lxgw)`，Reserved Font Name **"霞鹜" / "LXGW"**（基于 FONTWORKS Klee One）。

> 以上版权行以各字体官方发布包内的 `OFL.txt` 为准；此处为归档索引，下载字体时请核对并替换为官方原文。
