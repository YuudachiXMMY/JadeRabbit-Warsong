# ADR-D13: Run-End Boundary — Victory / Death Unification

## Status
**Accepted**（2026-08-12）— technical-director（时钟/事件机制）+ game-designer（结算语义/胜利判定）**联合签字**。

| 会签方 | 职责域 | 裁决 | 要点 |
|--------|--------|------|------|
| **technical-director** | 时钟/事件机制（D13-1/2/3） | **APPROVE-WITH-CONDITIONS**（条件已并入本 ADR） | 钦定落地方式 **(a)**（`OnPlayerDeath` → `OnRunEnded(RunOutcome)`，单一事件；greenfield 迁移成本≈零，消除双事件漂移病根）。三条机制条件已并入 Decision：**C1** 单一 run-end 入口 + 一次性闭锁（D13-5）；**C2** `RunOutcome` 枚举纯 C# 归属（D13-1）；**C3** VR1 实现前硬门（Verification Required 1 / Open Follow-up）。 |
| **game-designer** | 结算语义 / 胜利判定 / `RunOutcome` 语义（D13-4 FE5 分支） | **APPROVE** | 统一边界与设计支柱正向对齐（保护 merit 经济唯一沉淀点，机制统一 ≠ 体验扁平）；结算经济学 defer FE5、胜利判定 defer FE4/FE2 均认可、不阻塞 Accept。三条 FE5 承接备注见 Open Follow-up。 |

Accepted 后：C1/C4/F6（+ ADR-D1 事件声明源同步）按落地方式 (a) 走一轮胜利路径小修订（`OnPlayerDeath` → `OnRunEnded(outcome)`，Death 与 Victory 对称）。

## Date
2026-08-12（proposed — 消解 cross-review 2026-08-12 的 W-F：胜利/通关换局边界未定义）

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (6000.3.19f1) |
| **Domain** | Core / Integration (lifecycle) + Audio (BeatClock run-boundary stop) |
| **Knowledge Risk** | MEDIUM — 时钟/事件机制复用 ADR-D12 死亡边界（已验证）；唯一未定项 = TDE 是否/以何事件派发「关卡完成/胜利」。TDE `TopDownEngineEventTypes` 的胜利/通关成员名**须核对引擎参考**（参照 ADR-D1 教训：`.Respawn` 不存在 → `.RespawnComplete`）。 |
| **References Consulted** | `docs/architecture/adr-d12-death-run-lifecycle.md`、`docs/architecture/adr-d1-tde-integration.md`、`docs/architecture/adr-d10-bootstrap-sequencing.md`、`design/gdd/game-concept.md`（:164 死亡/通关结算）、`design/gdd/gdd-cross-review-2026-08-12.md`（W-F）、`design/gdd/tde-integration.md`（F6 Rule 15/状态表）、`design/gdd/rhythm-core.md`（C1 SC-2）、`design/gdd/combat-director.md`（C4 SC-3） |
| **Post-Cutoff APIs Used** | None proposed — 复用 ADR-D12 的 `BeatClock.Stop()` + 域事件桥接 + ADR-D10 boot 路径。 |
| **Verification Required** | 1. **核对 TDE 胜利/通关事件**：TDE 4.5 是否以 `TopDownEngineEvent` 派发关卡完成（候选 `LevelComplete` / `GameOver` 类成员，或由 `LevelManager`/`GoalManager` 触发）——若无原生事件，则胜利信号由 FE4（Boss 死亡）/ FE2（房间清场）显式发起。**须在 F6/FE5 实现前对齐引擎参考**。 2. PlayMode：胜利信号 → 断言 `BeatClock.ClockState == Stopped`、`InputEventBus == AllDisabled`、恰一次 `OnRunEnded(Victory)` 广播。 3. PlayMode：胜利后新 `LoadSceneMode.Single` → `globalBeatIndex == 0`、单次 `Play()`（无 double-`Play()`）。 |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-D12（死亡边界 / run-ends-on-death — Accepted；本 ADR 将其泛化为统一 run-end 边界的 `Death` 分支）、ADR-D1（F6 `TDELifecycleAdapter` 是 BeatClock 生命周期唯一控制者 — Accepted）、ADR-D10（`OnSceneLoaded(Single)` boot 路径 + `Stop()` 归零 `global_beat_index` — Accepted） |
| **Enables** | FE5（局结算按 `RunOutcome` 分支：Death vs Victory）、FE4（Boss 击败 → 胜利信号）、C1 SC-2 / C4 SC-3 的胜利路径对称清理 |
| **Extends（非 Supersede）** | ADR-D12：D12 的死亡边界成为本 ADR 统一 `OnRunEnded` 边界的 `outcome == Death` 特例；D12 保持 Accepted、机制不变 |

## Context

### Problem Statement（W-F，cross-review 2026-08-12）
ADR-D12 干净裁定了**死亡**换局边界（死亡 → F6 `Stop()` + `DisableAllInput()` + 广播 `OnPlayerDeath` → FE5 结算 → 新 Single 场景 boot）。但 **game-concept.md:164 明确「死亡/通关结算」——胜利/通关同样终结一局**，且 MVP 含 1 Boss（獬豸）+ 通关结算，故**胜利是 MVP 路径**。

胜利路径存在与死亡**同 class** 的两个状态转换风险，当前无 owner：

1. **时钟 `Stop()` 归属未定 → double-`Play()`**：若胜利不经 `Stop()`，`global_beat_index` 不归零、BeatClock 保持 `Playing`；下一局 `LoadSceneMode.Single` 的 boot 路径又调 `Play()` → 时钟被双启动 / 残留拍序。ADR-D12:138「Open Follow-up」已自认此缺口并划归 FE5。
2. **C4 绝对拍令牌孤儿**：胜利 `Stop()` 归零 `global_beat_index` 后，C4 残留 `Granted` 令牌的绝对 `targetLandingBeat` 失锚（与 SG-3 死亡场景完全同源）——但 C4 SC-3 当前**仅**锚 `OnPlayerDeath`，胜利路径无对称回收。

### Constraints
- **ADR-D1**：`BeatClock.Play/Stop/Pause/Resume` 唯一控制者 = F6 `TDELifecycleAdapter`；任何 run-boundary 时钟动作须经它。
- **ADR-D12 / 禁改 TDE 源码**：胜利信号的桥接须走 F6 的 `MMEventListener<TopDownEngineEvent>` 既有模式或由 FE 层显式发起，不改 TDE 源码。
- **边界隔离**：CombatCore（C1/C4）禁 `using` TopDownEngine，只订阅 F6 桥接的纯 C# 域事件（负载不含 TDE 类型）。
- **职责边界**：**时钟/事件机制**属 technical-director / F6；**「胜利」判定语义 + 结算经济学（胜利是否额外 merit、结算差异）** 属 game-designer / FE5——本 ADR 只裁定前者，后者明确 defer。

### Requirements
- 胜利与死亡须在**同一 run-end 边界**停时钟、广播域事件，供 FE5 结算 + C1/C4 清理订阅。
- 下游（C1 SC-2 / C4 SC-3）须以**单一** run-end 边界 key，避免胜利/死亡两套漂移的清理逻辑。
- 下一局复用既有 boot 路径，零新基建。

## Decision（Proposed）

**统一 run-end 边界：死亡与胜利经同一出口停时钟、广播同一域事件，按 `RunOutcome` 分支。**

### D13-1 — 统一 run-end 边界（泛化 ADR-D12 D12-1）
在 `PlayerDeath` **或** 胜利/通关信号到达时，F6 `TDELifecycleAdapter` 在**同一 Update 帧内**：
1. `BeatClock.Stop()` — run-ending 停时钟（归零 `global_beat_index`，ADR-D10）。
2. `InputEventBus.DisableAllInput()`。
3. 广播 **`OnRunEnded(RunOutcome outcome)`** — CombatCore 可见的纯 C# 事件（负载不含 TDE 类型），`outcome ∈ {Death, Victory}`。

**`RunOutcome` 枚举归属（TD 条件 C2，机制域已钉死）**：`RunOutcome` 是**纯 C# `enum`，定义在 CombatCore 可见的域程序集**，**不得引用任何 TopDownEngine 类型**。F6 桥接层负责把 TDE 事件 / FE 层显式调用翻译为该枚举值。枚举的**取值集合**（是否含 Victory 之外，如 Retreat/Quit）仍 defer game-designer/FE5，但**类型归属 + 无 TDE 依赖**属机制域，此处锁定（与 ADR-D1 边界隔离一致）。

**兼容性 / 落地方式（technical-director 钦定 (a)）**：ADR-D12 的 `OnPlayerDeath` 等价于 `OnRunEnded(Death)`。**已选 (a)**：以 `OnRunEnded(outcome)` **取代** `OnPlayerDeath`，C1/C4/F6/FE5（+ ADR-D1 事件声明源）全部改订单一 `OnRunEnded`。理由：当前全 greenfield（无代码，仅 GDD/ADR 文本待改），(a) 迁移成本≈零，而 (b) 的双事件别名正是本轮 cross-review 反复消解的「改一处漏一处」漂移病根。**(b)（保留 `OnPlayerDeath` 别名）已否决**。

### D13-2 — 胜利触发（defer detection，桥接对称死亡）
「胜利/通关」的**判定与发起**属 FE4（Boss 击败）/ FE2（房间/`LevelManager` 清场）/ FE5：
- 若 TDE 原生以 `TopDownEngineEvent`（如关卡完成成员）派发 → F6 以既有 `MMEventListener<TopDownEngineEvent>` 桥接（对称 `PlayerDeath`），**成员名须核对引擎参考**（Verification Required 1）。
- 若无原生事件 → 由 FE4/FE5 经 CombatCore 侧显式调用 F6 的 run-end 入口（不改 TDE 源码）。

本 ADR **不**裁定「何为胜利」的游戏语义——留给 game-designer / FE5。

### D13-3 — 下一局 = 新 Single 场景 boot（复用 D12-2）
胜利后「下一局」= 新 `LoadSceneMode.Single` 走既有 ADR-D1/D10 boot 路径。因 D13-1 的 `Stop()` 已归零 `global_beat_index` 并清 Pending，新 `Play()` 干净起局——**double-`Play()` 风险闭合**（W-F 风险 1 消解）。

### D13-4 — 下游边界合约（对称扩展 C1 SC-2 / C4 SC-3）
- **C4 SC-3**：改订 `OnRunEnded`（**Death 与 Victory 均触发**）→ 同帧取消全部 `Pending`/`Granted` 令牌。理由与死亡完全同源：胜利亦走 `Stop()`→归零 `global_beat_index`→绝对拍令牌失锚（W-F 风险 2 消解）。
- **C1 SC-2**：改订 `OnRunEnded` → 清 `_pendingInput` / `_lateWindowActive`。两种 outcome 均换新局重建 C1 实例，清空为幂等兜底。
- **FE5**：订阅 `OnRunEnded(outcome)`，按 `outcome` 分支执行 merit 折算/落盘 + 结算 UI（Death vs Victory 的结算差异由 FE5 定）。

### D13-5 — 单一 run-end 入口 + 一次性闭锁（TD 条件 C1，胜利多触发源机制补丁）
胜利路径与死亡不同：它有**两个潜在触发源**（D13-2 的 TDE 原生事件桥接 **或** FE4/FE5 显式调用）。若 Boss 死亡同时触发 TDE 事件与 FE4 显式调用，`OnRunEnded` 会重复广播、`Stop()` 会被二次调用。故：
- F6 `TDELifecycleAdapter` 暴露**单一 run-end 入口方法**（如 `EndRun(RunOutcome outcome)`），D13-1 的三步序列（`Stop()` → `DisableAllInput()` → 广播 `OnRunEnded(outcome)`）全部在此方法内执行；两条触发路径（TDE 事件桥接 / FE 显式调用）**均汇入该唯一方法**，不各自直呼三步。
- 该方法内置**每局一次性闭锁标志**（run-scoped latch，如 `_runEnded`）：首次进入 latch 并执行三步；重复触发（同局第二次到达，无论来自哪个源）**早退 no-op**，保证 Validation「恰一次 `OnRunEnded` / 单次 `Stop()`」在多触发源下成立。闭锁随新局 `LoadSceneMode.Single` 重建实例天然复位（死亡路径本无双源，此闭锁对 Death 分支幂等无害）。
- 属**纯机制**补丁，不涉结算语义。落地归 F6（`tde-integration.md` Rule 15 修订）。

## Alternatives Considered

### Model A — 统一 run-end 边界（CHOSEN / Proposed）
死亡与胜利经同一 F6 出口 + `OnRunEnded(outcome)`。复用 D12 机制，零新基建，C1/C4/FE5 单点订阅。

### Model B — 独立胜利路径（与死亡并列、逻辑复制）— REJECTED
- **Cons**：C4/C1 需维护死亡/胜利两套几乎相同的清理，易漂移（正是 cross-review 反复出现的「改一处漏一处」病根）。

### Model C — 完全留给 FE5 创建时处理 — REJECTED（就技术机制而言）
- **Cons**：把 MVP 已存在的 double-`Play()` / 令牌孤儿状态转换风险**悬空**到 FE5 开工之前；且时钟/事件机制属 F6/technical-director 域，非经济设计。**结算经济学**留给 FE5 合理，但**时钟边界**不应等。

## Consequences

### Positive
- W-F 的两个 MVP 状态转换风险（double-`Play()` / 令牌孤儿）在架构层闭合。
- 死亡/胜利单一边界，C1 SC-2 / C4 SC-3 清理逻辑不再按 outcome 分叉。
- FE5 获得确定的 `OnRunEnded(outcome)` 单一结算触发。

### Negative / Trade-offs
- 需 C1/C4/F6 一轮小修订（订阅从 `OnPlayerDeath` 迁至 `OnRunEnded`）——**须在本 ADR Accepted 后**进行（Proposed 期间不改这些 GDD）。
- 引入 `RunOutcome` 枚举，其语义（是否含 Victory 之外的 outcome，如 Retreat/Quit）留待 FE5 定，可能后续扩展。

### Risks
- **TDE 胜利事件不确定**（Verification Required 1）：若 TDE 无原生关卡完成事件，须 FE4/FE5 显式发起；不改 TDE 源码约束下须走继承/事件扩展。

## GDD Requirements Addressed

| GDD / 系统 | Requirement | 本 ADR 如何处理 |
|------------|-------------|-----------------|
| game-concept.md:164 | 死亡/通关**均**结算、终结一局 | 统一 run-end 边界覆盖 Victory 分支 |
| combat-director.md (C4) | SC-3 令牌回收当前仅锚死亡 | D13-4：C4 改订 `OnRunEnded`，胜利对称回收（待 Accept 后修订） |
| rhythm-core.md (C1) | SC-2 死亡清空 | D13-4：C1 改订 `OnRunEnded`（待 Accept 后修订） |
| tde-integration.md (F6) | Rule 15 仅定义死亡边界 | D13-1：F6 泛化为 run-end 边界（待 Accept 后修订状态表/Rule 15/交互表） |
| meta-progression (FE5, Not Started) | 局结算 | D13-4：FE5 订阅 `OnRunEnded(outcome)`，创建时承接 |

## Open Follow-up（Accepted 后仍 defer 的项）

### 时钟/事件机制域（technical-director）
- **TDE 胜利事件核对（TD 条件 C3，实现前硬门）**：TDE 是否原生以 `TopDownEngineEvent` 派发关卡完成（`LevelManager`/`GoalManager` 触发或成员名）**须在 F6/FE5 实现前对齐引擎参考并回填 `docs/engine-reference/unity/plugins/top-down-engine.md`**。参照 ADR-D1 `.Respawn`→`.RespawnComplete` 教训，成员名不得凭训练数据臆断。已登记为 registry `pre_implementation_checks` 校验项（见 entities.yaml）。不阻塞本 ADR Accept，但阻塞 D13-2 胜利桥接实现。

### 结算语义域（game-designer / FE5）
- **结算经济学**：胜利 vs 死亡的 merit 折算差异（胜利是否额外 merit / 通关奖励）——game-designer / FE5。**game-designer 会签备注**：胜利/死亡 merit 折算差异须**显式设计**，不得默认对称；若无差异，也须写明「经设计确认的对称」而非遗漏。
- **防退出套利边界（game-designer 会签备注）**：既然死亡结算不可绕过（ADR-D12 已闭合），FE5 落地时须同步确认「胜利前中途退出 / 强杀进程」不被玩家用于规避一次不利的死亡结算。属经济完整性的姊妹缺口（非本 ADR 时钟机制范畴），须在 FE5 GDD Edge Cases 明确处理方式（哪怕结论为「MVP 单机不设防」）。
- **`RunOutcome` 枚举**的完整取值（Death / Victory / 其它如 Retreat/Quit？）——FE5（类型归属与无 TDE 依赖已由 D13-1 锁定）。
- **胜利判定语义 + 唯一 fire owner（game-designer 会签备注）**：「何为胜利」（击败 Boss = 胜利？清空最后一房 = 胜利？）须在 FE4 遭遇设计前对齐，并回填 `RunOutcome` 触发点调用清单——确保 FE4/FE2 中**唯一 owner** fire 胜利信号，杜绝「两处都尝试触发胜利」的重复广播（D13-5 闭锁为兜底，但 owner 唯一化是正解）——game-designer / FE4 / FE2。

## Validation Criteria
1. **胜利边界**：PlayMode — 发胜利信号 → `BeatClock.ClockState == Stopped`、`InputEventBus == AllDisabled`、恰一次 `OnRunEnded(Victory)`。
2. **干净下一局**：胜利 `Stop()` 后新 `LoadSceneMode.Single` → 首个 `OnBeatExact` 时 `globalBeatIndex == 0`、单次 `Play()`（无 double-`Play()`）。
3. **下游对称**：`OnRunEnded(Victory)` → C1 `_pendingInput == null`（SC-2）且 C4 未决令牌 == 0（SC-3）——与 `OnRunEnded(Death)` 断言一致。

## Related Decisions
- ADR-D12（Death & Run-Lifecycle）— 本 ADR 泛化其死亡边界为统一 run-end 的 `Death` 分支；D12 的 `OnPlayerDeath` = `OnRunEnded(Death)`
- ADR-D1（TDE Integration）— F6 `TDELifecycleAdapter` 唯一 BeatClock 生命周期控制者，桥接 TDE 事件
- ADR-D10（Bootstrap Sequencing）— 胜利后「下一局」复用的 boot 路径；`Stop()` 归零 `global_beat_index`
- `design/gdd/gdd-cross-review-2026-08-12.md`（W-F）、`design/gdd/game-concept.md`（:164）
