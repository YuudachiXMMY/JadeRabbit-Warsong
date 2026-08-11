# Cross-Review 2026-08-10 — Owner 联合决策简报

> **来源**：`design/gdd/gdd-cross-review-2026-08-10.md`（FAIL）遗留的三项须 owner 裁定项。
> **状态**：机械快修（BC-1/BC-4/BC-7/W-03）与 BC-2 配对修订已在 commit `71d73c80` 落地；本简报覆盖**无法由单人拍板**的三项。
> **用途**：供 C1/C2/C3/C4 owner + creative/technical-director 联合裁定。裁定后回填各 GDD 并重跑 `/review-all-gdds`。

---

## BD-1 — Off 模式战力隔离（最高优先级，循环阻塞）

**问题**：Off 无障碍模式恒定 Perfect（零节奏技巧），而 C3 心法在 Off 下**无额外隔离**（`perk-system.md:209`「Off 下 DamageModifier/ComboModifier 仍生效」）。三系统叠加：Off 玩家 `rating=1.0` × `combo≤5.0`（ΔCombo 正常）× `perk≤2.0`（无限制）≈ 踩准玩家战力，仅缺 Advanced 分支。已从「功勋漏洞」升级为**战力漏洞**，击穿反支柱「不存在绕过节奏的破坏性技能」。C1 Q3（`meritMultiplierOff`）与 C3 OQ-C3-3 互指对方「待决」= 循环阻塞。

**已就位的接线**：C1-F7 已定义 provider 消费点，且 Off 分支明确留给本裁定（`AC-C1-26` 为阻塞桩）。**只差一个方向决策即可接线**。

| 选项 | 机制 | 代价 | 适用取向 |
|---|---|---|---|
| **A（推荐）perk 在 Off 下封顶** | Off 模式 C1 短路 `RhythmModifierProvider`（或对 `perk_multiplier` 设独立上限 1.0），纳入「节奏模式独占」护栏 | Off 玩家失去 perk 战力增益（保留规则型体验的可读性设计需另议） | 守「节奏即战斗」支柱与反支柱；隔离一致（连招深度已封顶 Standard，功勋已折算） |
| **B 明确「无障碍优先于平衡」** | Off 下 perk 全额生效，写入 game-concept 无障碍章节作为**刻意决定** | 承认 Off 是高战力低技巧路径；须接受排行/成就隔离足以防刷 | 无障碍立场优先，接受平衡让步 |

**决定者**：creative-director（支柱裁决）+ C1/C3 owner。**须先于 C5/FE5 经济 GDD**（Off 合约第 4 条）。**C1 Q3 与 C3 OQ-C3-3 必须同批锁定**（否则循环阻塞不解）。

---

## BD-2 — BPM 跃升三系统难度尖峰（Flow 反模式）

**问题**：Boss 转阶段单一 `OnMeterChanged` 同时令三系统同向变难，无任一公式含跨系统补偿：① C1 判定窗随 BPM 收窄（BPM≥124 钳位）；② C2 取消窗绝对毫秒缩短；③ C4 `SlotCapacity` 只按拍号、真实攻击密度随 BPM 线性上升。三重压力叠加于犯错代价最大时刻，违背心流「张力-释放」锯齿。

| 选项 | 机制 | 代价 |
|---|---|---|
| **A（推荐）容量按秒率归一化 + 适应窗口** | ① C4 `CapacityTable` 改「每秒令牌速率」归一化（抵消 BPM 密度线性上升）；② `OnMeterChanged` 后引入数拍「适应窗口」宽容期（C1 判定窗渐进收窄而非瞬切） | 需 C1+C4 各改一处公式；适应窗口须定义时长 tuning knob |
| **B 明确为刻意戏剧张力点** | 三文档 Formulas 互相引用，定义该时刻**专属失败代价保护**（如该 2 拍内 Miss 不掉连击/不扣血），把尖峰变成「设计过的高潮」 | 承认尖峰存在，靠代价保护兜底；须验证真人体感 |

**决定者**：C1/C2/C4 owner 联合。**须先于 FE4 Boss GDD 启动**（否则 Boss 编排落在未定义的难度曲线上）。

---

## BC-6 — 暂停/恢复三系统信号机制不一致（`OnBeatClockResumed` 缺失）

**问题**：F6 `TDELifecycleAdapter` 以**方法** `Pause()/Resume()` 驱动；C1 Rule 8 订阅**事件** `OnBeatClockResumed`（F2 未定义）；C3 冻结栈依赖未命名的 `OnGamePaused/OnGameResumed`（C3 OQ-C3-7 自认阻塞 AC-C3-16）。无任何系统广播三方都能消费的暂停/恢复事件。

**这是接口补全，非平衡决策**——但须 F2 owner 拍板事件契约形状，且随 C1 Q5（`OnBeatClockResumed` 语义）一并处理。

**建议方案**：F2 补 `OnBeatClockPaused` / `OnBeatClockResumed` 事件（后者携恢复首拍 raw DSP 时间）+ `IAudioPlayer.Pause()/Resume()`；C1/C3 统一订阅 `OnBeatClockResumed`；F6 `Resume()` 内部触发 F2 恢复即广播。裁定后三文档（F2/C1/C3）同轮修订，随 OQ-5 落地。

**决定者**：F2 owner + technical-director（生命周期契约）。关联：SC-1（降级承接者订阅 `OnAudioUnavailable`）可一并纳入 F6 生命周期协调器职责。

---

## 裁定后动作清单

1. BD-1 裁定 → 回填 C1-F7 Off 分支 + `AC-C1-26` 解桩 + C3 `:209`/OQ-C3-3 + C1 Q3 `meritMultiplierOff`
2. BD-2 裁定 → 回填 C1/C2/C4 Formulas 互引 + 适应窗口 tuning knob（若选 A）
3. BC-6 裁定 → F2 补事件 + C1/C3 订阅统一（随 OQ-5）
4. 三项闭环后重跑 `/review-all-gdds` 验证 FAIL→PASS
