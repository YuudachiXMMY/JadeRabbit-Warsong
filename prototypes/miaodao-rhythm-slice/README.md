# 苗刀节奏手感垂直切片 (Miaodao Rhythm Slice)

> **类型**: `/vertical-slice`（手感优先切片） · **日期**: 2026-08-16 · **状态**: 首轮脚手架完成，待 Editor 试玩
> **验证风险**: R-FEEL-01（节奏手感未验证 — #1 Critical） · **判据**: `production/milestones/m0-rhythm-feel-acceptance.md`

## 验证问题（可证伪）

> 新玩家能否在 **≤3 分钟**内、无引导下走完 [进房+校准 → 踩拍苗刀轻/重/冲刺连招击败 1 个落拍敌人 → 结算再来一局] 循环，并把踩拍层体验为**「奖励」而非「附加摩擦」**（对齐已锁 M0 A 组：手感 Likert 中位 ≥4 且无一 ≤2 / "我踩了却 Miss"零出现 / 自发再来一局 ≥60%）——同时，我们能否以代表性手感质量在合理工期内构建此循环？

## 代码位置（Unity 编译约束）

- **代码/场景**: `Assets/_VerticalSlice/MiaodaoRhythmSlice/`（独立 asmdef，不被 `CombatCore.asmdef` 引用，可整目录删除）
- **文档/REPORT**: 本目录 `prototypes/miaodao-rhythm-slice/`
- 理由：Unity 只编译 `Assets/` 下的 C#，仓库根 `prototypes/` 里的脚本**不会被编译**——故代码必须落 `Assets/`，仅文档留此。

## 如何运行（最小化手工）

1. 拉本分支，用 Unity 6.3 LTS 打开本 worktree 工程，等编译完成。
2. 菜单栏 **`VerticalSlice / Build Miaodao Rhythm Scene`** —— 一键程序化生成并接线整个场景（玩家/敌人/地面/相机/HUD/系统），保存到 `Assets/_VerticalSlice/MiaodaoRhythmSlice/MiaodaoRhythmSlice.unity`。
3. 按 **Play**。（之后可用 `VerticalSlice / Open Slice Scene` 直接打开。）

## 操作

| 键 | 作用 |
|---|---|
| WASD / 左摇杆 | 移动（**不量化**，自由） |
| J / 手柄 X | 轻攻击（踩拍判定） |
| K / 手柄 Y | 重攻击 |
| L / 手柄 B | 冲刺攻击 |
| Tab | 循环量化档 Off → HalfBeat → Beat（测反支柱护栏 B） |
| `[` / `]` | BPM −10 / +10（测攻击时长随 BPM 缩放） |
| Enter | 校准阶段跳过校准 |
| R | 结算后再来一局 |

## 循环三段

- **START**：进房 → "跟拍敲 J 键 ×6" 延迟校准 → 首拍起、HUD 节拍指示出现
- **CHALLENGE**：落拍敌人在强拍前摇→攻击（听音乐预判躲避）；踩半拍网格出苗刀连招；Perfect 触发 **Perfect Kinetics**（冻帧 + 编钟共鸣 + 相机微切 + 敌硬直 ×1.3）；连击累积
- **RESOLUTION**：敌亡(Victory)/玩家倒下(Death) → 单出口结算面板（Perfect%/最大连击/评级）→ R 再来一局

## 本切片验证什么、不验证什么

- ✅ **验证**：踩拍苗刀战斗的**手感**是否「奖励而非摩擦」（R-FEEL-01）；判定窗手感、Perfect Kinetics 爽度、Off vs HalfBeat 差异、BPM 变速一致性、延迟校准雏形有效性。
- ❌ **不验证**：FMOD/TDE/Animancer/Unity Behavior 的**生产集成可行性**（那已由 ADR + 2026-07-16 技术 Spike 纸面去险；若手感 PASS 可后续再跑"集成切片"）。

## 忠实实现的 GDD 契约

C1-F2 偏移公式、C1-F3 评级钳位级联（Perfect±50/Good±120ms@120BPM）、规则 9 后果表（1.0/0.75/0.5 · +2/+1/+0 · Advanced/Standard/Closed · Perfect→Kinetics）、Off 恒 Perfect 但 CancelTier 封顶 Standard、C2 combo_multiplier=min(5.0, 1+0.01·count^1.2)、攻击时长按 BPM 缩放（禁绑死帧）、Miss 不清零段数、EC15 冻帧不冻玩家挥击时钟（`freeze_zeroes_player_attack_speed` 禁令）。

## 手感优先的替身（非生产）

| 生产系统 | 切片替身 |
|---|---|
| F2 FMOD BeatClock | `SliceMetronome`：`AudioSettings.dspTime` + `PlayScheduled` + 程序化 click 音（零 FMOD bank） |
| Animancer 攻击动画 | `MiaodaoSwing`：代码驱动弧线挥击，时长 = base × authoredBpm/currentBpm |
| P1 UI Toolkit HUD | `RhythmHUD`：IMGUI/OnGUI（零资产） |
| F3 InputEventBus | `SliceInput`：代码内建 InputAction，`ctx.time` 回投 dsp 域 |
| FE1 敌人 (Unity Behavior) | `BeatEnemy`：极简拍驱前摇→攻击 |
| FE3 延迟校准 / PL2 Kinetics | 就地设计雏形（无 GDD，findings 反哺未来 GDD） |

## 试玩后（Phase 5 五问 → REPORT）

试玩后我会逐一问：① 是否无引导走完全环？② 到"第一个真正在玩"的时刻用了多久？③ 是否感到「战斗即演奏」的奖励感（具体到哪一刻）？④ 什么卡住/困惑/出戏？⑤ 作为开发者，这个质量对全量游戏是否可达？→ PROCEED / PIVOT / KILL。结果写入本目录 `REPORT.md`。

## 已知非阻塞小瑕疵（首轮）

- 敌人第一次攻击落在第 2 小节（idx 0 强拍不预告），首拍无攻击——节奏铺垫，非 bug。
- 命中闪白与前摇变色同帧可能叠色，纯美术、不影响功能。
- 无 Unity 环境无法预编译；首跑可能有需迭代的编译/接线错误——按 Phase 4 循环修。
