# Systems Index: 玉兔：破阵 (Jade Rabbit: Warsong)

> **Status**: Under Review
> **Created**: 2026-07-13
> **Last Updated**: 2026-07-19
> **Source Concept**: design/gdd/game-concept.md

---

## Overview

《玉兔：破阵》是节奏动作 Roguelike：攻击量化到半拍网格执行，评级（Perfect/Good/Miss）决定伤害、连击增长和连招取消权限；敌人攻击强制落拍，使「听音乐防御」成为真实策略；心法三选一构建改变节奏规则的运行时效果。

系统版图由三个关键层构成：**节奏层**（音频时钟 → 量化判定 → 战斗导演）、**战斗层**（连招图 → Hitbox → 武器系统）、**成长层**（心法构筑 → 局外成长）。TDE（TopDownEngine）提供移动/生命/关卡/反馈基础设施，自建 CombatCore 程序集负责节奏量化与连招逻辑，两者通过明确的边界协议共存。所有 UI 和 Polish 系统均订阅同一套节奏事件总线，确保「节拍驱动视效」的视觉一致性。

---

## Systems Enumeration

| # | 系统 | 层级 | 里程碑 | 状态 | 设计文档 | 依赖 |
|---|------|------|--------|------|---------|------|
| F1 | Game Concept | Foundation | — | **Approved** | `design/gdd/game-concept.md` | — |
| F2 | 音频/拍点时钟系统 | Foundation | **MVP** | **Revised (ADR-D10 同轮) — Pending Re-review** | `design/gdd/audio-beat-clock.md` | ADR-D6 (Accepted), ADR-D10 (Accepted) |
| F3 | Input 事件系统 | Foundation | **MVP** | **Approved** | `design/gdd/input-event-system.md` | — |
| F4 | ScriptableObject 数据层（推断） | Foundation | **MVP** | **Approved** | `design/gdd/so-data-layer.md` | — |
| F5 | 存档/持久化系统 | Foundation | **MVP** | **Revised (ADR-D10 同轮) — Pending Re-review** | `design/gdd/save-persistence.md` | ADR-D7 (Accepted), ADR-D10 (Accepted) |
| F6 | TopDownEngine & Animancer 集成层 | Foundation | **MVP** | **Amended (ADR-D10 同轮) — Pending Re-review** | `design/gdd/tde-integration.md` | ADR-D1 (Accepted), ADR-D5 (Accepted), ADR-D10 (Accepted) |
| C1 | 节奏量化攻击系统 | Core | **MVP** | Approved | `design/gdd/rhythm-core.md` | F2, F3 |
| C2 | 连招图系统 | Core | **MVP** | **In Review** | `design/gdd/combo-system.md` | F4, C1 |
| C3 | 心法构筑系统 | Core | **MVP** | **Designed**（pending review） | `design/gdd/perk-system.md` | F4, C1 |
| C4 | 战斗导演 | Core | **MVP** | Not Started | `design/gdd/combat-director.md` | F2 |
| C5 | 武器系统（推断） | Core | **MVP** | Not Started | `design/gdd/weapon-system.md` | F4, C2 |
| C6 | Hitbox/受击判定系统（推断） | Core | **MVP** | Not Started | `design/gdd/hitbox-system.md` | C2, F6 |
| FE3 | 延迟校准系统 | Feature | **MVP** | Not Started | `design/gdd/latency-calibration.md` | F2, F5 |
| FE1 | 敌人 AI 系统 | Feature | **MVP** | Not Started | `design/gdd/enemy-ai.md` | C4, F4 |
| FE4 | Boss 战系统 | Feature | **MVP** | Not Started | `design/gdd/boss-battle.md` | FE1, C4, F2 |
| FE2 | 房间系统（推断） | Feature | **MVP** | Not Started | `design/gdd/room-system.md` | F6, FE1, C3 |
| FE5 | 局外成长系统 | Feature | **MVP** | Not Started | `design/gdd/meta-progression.md` | F5, C3, C5 |
| FE7 | 游戏设置系统（推断） | Feature | **MVP** | Not Started | `design/gdd/settings-system.md` | F5 |
| PL1 | 相机系统（推断） | Feature | **MVP** | Not Started | `design/gdd/camera-system.md` | C1, F6 |
| P6 | 延迟校准 UI（推断） | Presentation | **MVP** | Not Started | `design/gdd/ui-latency-calibration.md` | FE3 |
| P1 | HUD | Presentation | **MVP** | Not Started | `design/gdd/ui-hud.md` | C1, C3, C2, F2 |
| P5 | 暂停菜单 | Presentation | **MVP** | Not Started | `design/gdd/ui-pause-menu.md` | F6 |
| P7 | 主菜单（推断） | Presentation | **MVP** | Not Started | `design/gdd/ui-main-menu.md` | F6, FE5 |
| P2 | 心法选择 UI（推断） | Presentation | **MVP** | Not Started | `design/gdd/ui-perk-selection.md` | C3, FE2 |
| P3 | 局结算界面 | Presentation | **MVP** | Not Started | `design/gdd/ui-run-settlement.md` | FE5 |
| P4 | 局外成长菜单（推断） | Presentation | **MVP** | Not Started | `design/gdd/ui-meta-menu.md` | FE5 |
| P8 | 设置菜单（推断） | Presentation | **MVP** | Not Started | `design/gdd/ui-settings.md` | FE7 |
| PL2 | VFX 与节奏反馈（推断） | Polish | **MVP** | Not Started | `design/gdd/vfx-rhythm-feedback.md` | C1, F6 |
| PL3 | 教程/引导系统（推断） | Polish | **Vertical Slice** | Not Started | `design/gdd/tutorial-onboarding.md` | FE2, P1 |
| FE6 | 程序化房间生成（推断） | Feature | **Alpha** | Not Started | `design/gdd/procedural-room-gen.md` | FE2 |
| PL4 | 无障碍设置（推断） | Polish | **Full Vision** | Not Started | `design/gdd/accessibility-settings.md` | F5 |

> *（推断）= 概念文档未直接命名，但机制上隐含必须存在*

---

## Categories

| 层级 | 描述 | 本项目系统 |
|------|------|-----------|
| **Foundation** | 无依赖的技术地基，所有其他系统的底层假设 | 音频时钟、Input 事件、SO 数据层、存档、TDE 集成 |
| **Core** | 定义核心玩法体验的系统，构成 30 秒循环 | 节奏量化、连招图、心法、战斗导演、武器系统、Hitbox 判定 |
| **Feature** | 构成完整单局游戏流程的系统 | 敌人 AI、房间系统、校准、Boss 战、局外成长、设置系统、相机系统、程序化生成 |
| **Presentation** | 玩家感知和交互的界面层 | HUD、心法选择、结算、升级菜单、暂停、校准 UI、主菜单、设置菜单 |
| **Polish** | 强化感官体验、保证质量的系统 | VFX/节奏反馈、教程引导、无障碍设置 |

---

## Priority Tiers

| 里程碑 | 定义 | 目标节点 | 设计优先级 |
|--------|------|---------|-----------|
| **MVP** | 核心循环可运行、可验收——包含苗刀完整连招 + 1 种敌人 + 1 Boss + 3-5 间房间 + 心法三选一 + 基础 HUD | M0-M2，约 6-8 周 | 最优先 |
| **Vertical Slice** | 支持 2 种武器、10+ 心法、完整引导流程，验证「构筑驱动复盘」 | M0-M4，约 4-5 个月 | 其次 |
| **Alpha** | 随机地牢生成引入，3 种武器 + 2 Boss，机械完整 | M0-M5，约 7-9 个月 | 第三 |
| **Full Vision** | 内容完整 + 打磨 + 发布质量，含无障碍与叙事 | M0-M6，12+ 个月 | 按需 |

---

## Dependency Map

### Foundation Layer（无依赖，优先设计）

1. **F2 音频/拍点时钟系统** — 唯一真值时钟（DSP），节奏判定/量化/调度的物理底层
2. **F3 Input 事件系统** — Input System callback 时间戳管线，消除帧量化误差
3. **F4 ScriptableObject 数据层** — AttackNodeSO / PerkSO / WeaponSO / TrackSO / EnemyInfoSO schema 设计
4. **F5 存档/持久化系统** — PlayerInfoSO 序列化，跨局数据（功勋、解锁、设置）
5. **F6 TopDownEngine & Animancer 集成层** — TDE 边界设计（移动/生命/关卡/反馈）+ Animancer 攻击层配置

### Core Layer（依赖 Foundation）

1. **C1 节奏量化攻击系统** — 依赖：F2（拍点时序）+ F3（输入时间戳）
2. **C4 战斗导演** — 依赖：F2（拍槽分配）
3. **C2 连招图系统** — 依赖：F4（AttackNodeSO/ComboGraphSO schema）+ C1（评级控制取消窗）
4. **C3 心法构筑系统** — 依赖：F4（PerkSO schema）+ C1（心法修改节奏规则）
5. **C5 武器系统** — 依赖：F4（WeaponSO schema）+ C2（连招图运行时容器）
6. **C6 Hitbox/受击判定系统** — 依赖：C2（连招图攻击帧，定义 hitbox 激活时机）+ F6（TDE 碰撞检测基础）

### Feature Layer（依赖 Core）

1. **FE3 延迟校准系统** — 依赖：F2（音频时钟测量）+ F5（偏移量存档）
2. **FE7 游戏设置系统** — 依赖：F5（设置数据持久化；音量/键位/视觉辅助）
3. **PL1 相机系统** — 依赖：C1（Perfect 触发 camera micro-cut 事件）+ F6（Cinemachine 虚拟相机基础）
4. **FE1 敌人 AI 系统** — 依赖：C4（beat-locked 攻击令牌）+ F4（EnemyInfoSO）
5. **FE4 Boss 战系统** — 依赖：FE1（AI 扩展）+ C4（导演拍号切换）+ F2（阶段切换时序）
6. **FE2 房间系统** — 依赖：F6（TDE LevelManager）+ FE1（房间内有敌人）+ C3（心法奖励分发）
7. **FE5 局外成长系统** — 依赖：F5（存档）+ C3（心法池扩展）+ C5（武器解锁）
8. **FE6 程序化房间生成** — 依赖：FE2（房间系统，Alpha+ 引入）

### Presentation Layer（依赖对应游戏系统）

1. **P6 延迟校准 UI** — 依赖：FE3（校准系统）
2. **P1 HUD** — 依赖：C1（评级显示）+ C2（连击计数）+ C3（心法状态）+ F2（节奏指示器）
3. **P5 暂停菜单** — 依赖：F6（场景暂停）
4. **P7 主菜单** — 依赖：F6（场景管理）+ FE5（武器解锁状态）
5. **P2 心法选择 UI** — 依赖：C3（心法数据）+ FE2（房间奖励触发）
6. **P3 局结算界面** — 依赖：FE5（功勋结算）
7. **P4 局外成长菜单** — 依赖：FE5（升级数据）
8. **P8 设置菜单** — 依赖：FE7（设置系统数据与操作接口）

### Polish Layer（依赖 Presentation + 游戏系统）

1. **PL2 VFX 与节奏反馈** — 依赖：C1（评级事件）+ F6（MMFeedbacks/Feel 基础）
2. **PL3 教程/引导系统** — 依赖：FE2（房间系统）+ P1（HUD 引导提示）
3. **PL4 无障碍设置** — 依赖：F5（设置持久化）

---

## Recommended Design Order

| 顺序 | 系统 | 里程碑 | 层级 | 可并行 | 预估投入 |
|------|------|--------|------|--------|---------|
| 1 | **F2** 音频/拍点时钟系统 | MVP | Foundation | F3、F4、F5（可并行） | M |
| 2 | **F3** Input 事件系统 | MVP | Foundation | F2、F4、F5（可并行） | S |
| 3 | **F4** ScriptableObject 数据层 | MVP | Foundation | F2、F3、F5（可并行） | M |
| 4 | **F5** 存档/持久化系统 | MVP | Foundation | F2、F3、F4（可并行） | S |
| 5 | **F6** TDE & Animancer 集成层 | MVP | Foundation | — | M |
| 6 | **C1** 节奏量化攻击系统 | MVP | Core | C4（可并行） | L |
| 7 | **C4** 战斗导演 | MVP | Core | C1（可并行） | M |
| 8 | **C2** 连招图系统 | MVP | Core | C3（可并行） | L |
| 9 | **C3** 心法构筑系统 | MVP | Core | C2（可并行） | M |
| 10 | **C5** 武器系统 | MVP | Core | C6、PL1（可并行） | S |
| 11 | **C6** Hitbox/受击判定系统 | MVP | Core | C5、PL1（可并行） | S |
| 12 | **PL1** 相机系统 | MVP | Feature | C5、C6（可并行） | S |
| 13 | **FE3** 延迟校准系统 | MVP | Feature | FE7、FE1、FE5（可并行） | S |
| 14 | **FE7** 游戏设置系统 | MVP | Feature | FE3、FE1、FE5（可并行） | S |
| 15 | **FE1** 敌人 AI 系统 | MVP | Feature | FE3、FE7、FE5（可并行） | L |
| 16 | **FE5** 局外成长系统 | MVP | Feature | FE3、FE7（可并行） | M |
| 17 | **FE4** Boss 战系统 | MVP | Feature | — | L |
| 18 | **FE2** 房间系统 | MVP | Feature | — | M |
| 19 | **P6** 延迟校准 UI | MVP | Presentation | P1、P5、P7（可并行） | S |
| 20 | **P1** HUD | MVP | Presentation | P6、P5、P7（可并行） | M |
| 21 | **P5** 暂停菜单 | MVP | Presentation | P6、P1、P7（可并行） | S |
| 22 | **P7** 主菜单 | MVP | Presentation | P6、P1、P5（可并行） | S |
| 23 | **P2** 心法选择 UI | MVP | Presentation | P3、P4、P8（可并行） | S |
| 24 | **P3** 局结算界面 | MVP | Presentation | P2、P4、P8（可并行） | S |
| 25 | **P4** 局外成长菜单 | MVP | Presentation | P2、P3、P8（可并行） | S |
| 26 | **P8** 设置菜单 | MVP | Presentation | P2、P3、P4（可并行） | S |
| 27 | **PL2** VFX 与节奏反馈 | MVP | Polish | — | M |
| 28 | **PL3** 教程/引导系统 | Vertical Slice | Polish | — | M |
| 29 | **FE6** 程序化房间生成 | Alpha | Feature | — | L |
| 30 | **PL4** 无障碍设置 | Full Vision | Polish | — | S |

> *投入估算：S = 1 次 session，M = 2-3 次 session，L = 4+ 次 session*

---

## Circular Dependencies

**无循环依赖。**

检查记录：
- C2（连招图）→ C5（武器）：单向，C5 依赖 C2，C2 不依赖 C5
- C2（连招图）→ C6（Hitbox）：单向，C6 依赖 C2，C2 不依赖 C6
- FE1（AI）→ FE2（房间）：单向，FE2 依赖 FE1，FE1 不依赖 FE2
- C3（心法）→ FE2（房间）：单向，FE2 依赖 C3，C3 不依赖 FE2
- FE7（设置）→ P8（设置菜单）：单向，P8 依赖 FE7，FE7 不依赖 P8

---

## High-Risk Systems

| 系统 | 风险类型 | 风险描述 | 缓解措施 |
|------|---------|---------|---------|
| **F2 音频/拍点时钟** | 技术 + 决策 | FMOD vs Unity Audio DSP 未定案；无线耳机延迟 100-300ms 可能让整个判定体系失效 | M0 Spike：三档量化模式手感测试定案；定案后在 F2 GDD 中锁定音频中间件选择 |
| **C1 节奏量化攻击** | 设计 + 技术 | M0 Spike 输出（判定窗口精确值）是 C1 GDD 的必要输入；核心假设「节奏层是奖励而非摩擦」未经外部验证 | M0 Spike 先跑三个候选档位（Off/HalfBeat/Beat）；GDD 以量化档位无关方式写，保留配置开关 |
| **F6 TDE 集成层** | 技术 | TDE/自研边界设计不当会导致 v1 的「582 行覆写陷阱」重演；Animancer 攻击层与 TDE Animator 层的叠加如果冲突，修复成本极高 | F6 GDD 需明确「TDE 负责什么 / CombatCore 负责什么」的接口合约；禁止修改 TDE 源码，只用继承/事件扩展 |
| **FE3 延迟校准** | 体验 | 校准流程是游戏前 2 分钟的开门体验；体验差将在首分钟劝退玩家（PRD 明确风险） | 早期用户测试（5 人定性测试）验证校准流程可理解性；设计多种校准模式（自动/手动输入/跳过+提示） |
| **FE1 敌人 AI** | 技术决策 | Unity Behavior 包 vs v1 自研行为树的选型 ADR 未定案；选型影响整个 AI 开发管线 | D2 ADR 必须在 FE1 GDD 前完成；AI 不得直接调用节奏系统（通过黑板注入） |

---

## Progress Tracker

| 指标 | 数量 |
|------|------|
| 识别的系统总数 | 31 |
| 设计文档已开始 | 6 |
| 设计文档已审查 | 6 |
| 设计文档已 Approved | 3 |
| 设计文档 Needs Revision | 3 |
| MVP 系统已设计 | 7 / 27 |
| Vertical Slice 系统已设计 | 0 / 1 |
| Alpha 系统已设计 | 0 / 1 |
| Full Vision 系统已设计 | 0 / 1 |

---

## Status Legend

| 状态 | 含义 |
|------|------|
| **Not Started** | GDD 未创建 |
| **In Progress** | GDD 骨架已创建，章节填写中 |
| **In Review** | GDD 完成，等待 `/design-review` |
| **Revised — Pending Re-review** | 阻塞项已处理，建议重新审查 |
| **Approved** | `/design-review` 判定 APPROVED，可进入实现 |
| **Needs Revision** | 审查返回 NEEDS REVISION，见 review log |

---

## Next Steps

- [ ] 完成 **M0 Spike**：三档量化模式手感测试，输出 F2 + C1 所需的判定窗口数据，定案 FMOD vs Unity Audio
- [ ] 定案 **D2 ADR**（FE1 AI 技术选型）：Unity Behavior pkg vs v1 自研 BT，先于 FE1 GDD 完成
- [ ] 开始第一批 GDD（按设计顺序 1-5 并行，均无依赖）：
  - `/design-system audio-beat-clock` (F2) — 节奏层的物理底层
  - `/design-system input-event-system` (F3)
  - `/design-system so-data-layer` (F4)
- [ ] 每个 GDD 完成后运行 `/design-review design/gdd/[system].md` 验证质量
- [ ] 所有 MVP GDD 完成后运行 `/gate-check pre-production` 进入 Production 阶段
