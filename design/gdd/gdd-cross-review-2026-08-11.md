# Cross-GDD Review Report — 2026-08-11

> **上一轮**：`gdd-cross-review-2026-08-10.md`（**FAIL** — 11 阻塞项：BC-1~BC-7、BD-1、BD-2、SC-1、SC-2 + B3）
> **本轮目的**：验证 2026-08-10 FAIL 阻塞集经 blocker-fix pass 后是否清零（FAIL→PASS）
> **审查方式**：两条独立通道并行对撞——(A) 一致性 + 阻塞清除核验；(B) 设计整体 + 场景走查（对抗式，逐项验证 BD-1 数学 / BD-2 密度归一化）
> GDDs Reviewed: 9 系统（F2/F3/F4/F5/F6/C1/C2/C3/C4）+ game-concept + entities.yaml
> Systems Covered: 节奏战斗全链（Foundation F2-F6 + Core C1-C4）

---

## 执行摘要

初次复审：**10/11 原始阻塞项完全清除**，唯 **SC-2 仅清一半**（C1 消费侧完整，F6 广播侧未反向登记 C1、未暴露 CombatCore 可见的死亡/复活域事件——因 C1 禁止 `using` TDE，契约当前不可实现），加 3 处 consumer/caller 侧 warning。判 **FAIL（1 阻塞 + 3 warning）**。

**本 session 已修复全部 4 项**（owner 授权「直接修 F6」）：
- 🔴 SC-2/F6：F6 新增 **Rule 15 死亡/复活域事件桥接** + `OnPlayerDeath`/`OnRespawnComplete`（纯 C# event，负载不含 TDE 类型）+ Interactions 表 F6→C1 行 + 双向依赖确认登记 C1 + **AC-F6-25**；C1 侧订阅目标精确到该域事件，双向合约字字对齐。
- ⚠️ C2 clamp-50 陈旧债务（3 处）→ 更新为 BC-7 已解决。
- ⚠️ C2 规则 12 事件混淆 → 澄清订阅 `OnMeterChanged`（边界生效，区别于 `OnMeterChangeAccepted`）。
- ⚠️ F6 `ScheduleBGMTransition` 参数序反 + 陈旧「F2 缺口未解决」文本 → 对齐 F2 权威签名 `(nextBgm, boundaryDspTimeRaw)`；F2 补 `OnAudioRestored` 定义 + Interactions 行。

**修复后 Verdict：PASS。**

---

## 阻塞清除表（初次复审结果）

| 阻塞项 | 状态 | 证据（file:line） |
|--------|------|------------------|
| BC-1 Perfect 倍率 1.0 | ✅ 清除 | game-concept:419 / rhythm-core:129 / combo-system:120,126 |
| BC-2 `IRhythmModifierProvider` 双向接线 | ✅ 清除 | C1-F7 定义并查询；C3 实现 + 广播 `OnPerkStackChanged` |
| BC-3 `CombatDirectorConfigSO`(F4-S8) | ✅ 清除 | so-data-layer:267-287 定义全字段；combat-director 依赖/Tuning 改指 F4-S8；EnemyInfoSO 只留 windup/timeout |
| BC-4 合法拍号 | ✅ 清除 | C4={4/4,3/4,5/8}；C3=4/4→3/4；无活引用 6/8 或 3/8 |
| BC-5 `AttackType` 统一 | ✅ 清除 | so-data-layer:175 / combo-system:41,269 直接比较无映射；旧枚举仅废弃注记 |
| BC-6 暂停/恢复事件 | ✅ 清除 | F2:51-54 定义两事件 + Pause/Resume + struct；C1 Rule8 重锚 + 清；C3 事件名对齐 |
| BC-7 ComboMax 50→999 | ✅ 清除 | rhythm-core:330/347/750；C2 陈旧注记已本轮更新 |
| BD-1 Off perk 封顶 1.0 | ✅ 清除 | C1-F5c:463-488；C3 OQ-C3-3 解除；AC 可判定；仅限 Off |
| BD-2 适应窗口 + 每秒令牌归一 | ✅ 清除 | C1-F5b / C2-F7 `max(before,after)`；C4 `tokens/sec=Cap×bpmRef/60` |
| SC-1 音频降级承接 | ✅ 清除 | F2:58 广播 `OnAudioUnavailable`；F6 Rule14 同帧 Pause+DisableAllInput |
| **SC-2 复活幽灵评级** | ⚠️→✅ 本轮修复 | 初次：C1 侧完整、F6 侧未反向登记（PARTIAL）→ 修复后 F6 Rule15 桥接 + AC-F6-25 闭环 |
| B3 `ScheduleBGMTransition` | ✅ 清除 | audio-beat-clock:513 定义；F6 调用侧参数序本轮修正 |

---

## 设计层核验（对抗式，通道 B）

- **BD-1（Off 战力主导）→ RESOLVED**。伤害 = `base × rating × combo × perk`。Off 上限 = `1.0 × 5.0 × 1.0 = 5.0×base`（perk 封顶 1.0，rhythm-core:463-488）；踩准上限 = `1.0 × 5.0 × 2.0 = 10.0×base`（perk 至 PERK_MULT_MAX 2.0）。**踩准恰为 Off 的 2×**，外加 Advanced 分支 + 功勋折算 `meritMultiplierOff∈(0,1)`。封顶**仅限 Off**（else 分支保 [0.5,2.0]），Off 保留为无障碍通道（5×base 可玩，不再战力主导）。反支柱「不存在绕过节奏的破坏性技能」闭合。
- **BD-2（BPM 跃升三系统尖峰）→ RESOLVED**。(a) 窗口平滑：120→180 时 C1 Good 标准 `min(120,82.3)=82.3ms`，适应窗内取 `max(120,82.3)=120ms` 不骤缩（rhythm-core:451-455）；C2 取消窗同规则。(b) 密度归一：`EffectiveSlotCapacity=round(Cap×bpmRef/bpm)` → `tokens/sec=Cap×bpmRef/60` **恒定、BPM 无关**（代数确认，combat-director:117-135）。`AdaptationWindowBeats=2` 三文档一致，已标注须先于 FE4 由 owner 联合锁定。
- **SC-1 → RESOLVED**。设备断开：F2 同帧内部 Pause + 广播 `OnAudioUnavailable`；F6 同 Update 帧原子 `GameManager.Pause()`+`DisableAllInput()`——无半暂停半运行战场。
- **SC-2 → RESOLVED（修复后）**。死亡→复活→首拍：C1 订阅 F6 桥接域事件 `OnPlayerDeath`/`OnRespawnComplete`，跨死亡边界 pending 输入双清，复活后首个 lookahead 不评级陈旧输入。

**设计整体（Phase 3）**：Off 封顶后主导路径明确为踩准节奏（2× perk + Advanced + 满功勋），`max(before,after)` 不可被玩家操纵（BPM 变更由 Boss/FE4 驱动，非玩家触发，窗口限 2 拍）。BD-3/BD-4/BD-5 仍为非阻塞 WARNING，未升级。BD-2 将难度杠杆由「密度」转为「精度」（高 BPM Boss 阶段密度 BPM 无关，靠绝对毫秒窗收紧 + 编排施压）——刻意取舍，FE4 须知悉勿依赖密度。

**场景走查（Phase 4）**：S-A Boss 拍号切换 = WARNING（SC-3 accept/boundary 发散 + SC-4 边界丢充能仍非阻塞，BD-2 不加剧）；S-B 暂停恢复 = INFO；S-C 音频断开 = PASS；S-D 死亡复活 = PASS（修复后）；S-E Off vs 踩准 = PASS（5×base vs 10×base）。

---

## GDDs Flagged for Revision（本轮已在 session 内修复）

| GDD | 原因 | 类型 | 处置 |
|-----|------|------|------|
| tde-integration.md (F6) | SC-2 未反向登记 C1（Blocking）；B3 参数序 + 陈旧文本 | 一致性 | ✅ 已修（Rule15 + AC-F6-25 + 表/登记 + 参数序 + 接口归属重写） |
| combo-system.md (C2) | clamp-50 陈旧债务（3 处）；Rule12 事件混淆 | 一致性 | ✅ 已修 |
| audio-beat-clock.md (F2) | SC-1 `OnAudioRestored` 未定义；Interactions 缺 F2→F6 行 | 一致性 | ✅ 已修 |
| rhythm-core.md (C1) | 订阅目标精确到 F6 域事件（双向对齐） | 一致性 | ✅ 已修 |

---

## 残余非阻塞项（留待后续，不阻断架构）

- ℹ️ `AdaptationWindowBeats` 双处定义 + 大小写漂移（C1 PascalCase / C2 camelCase），语义已互引对齐；建议单一 owner（RhythmConfigSO）。**须先于 FE4 Boss GDD 由 owner 联合锁定值**。
- ℹ️ `meritMultiplierOff` 具体值须先于 C5/FE5 经济曲线锁定（C1 Q3）。
- ⚠️ BD-3（连击只增不减）、BD-4（数值心法碾压规则心法）、BD-5（注意力预算 4 层并发）、SC-3/SC-4（accept/boundary 发散窗、Boss 切换丢充能）——WARNING，Vertical Slice 真人测试验证。
- ℹ️ W-05 `weapon.perfectWindowModifier` 仍未接线（已知延后，非假接线，待武器上下文）。

---

## Verdict: **PASS**

无阻塞项。所有 11 项 2026-08-10 阻塞项 + B3 已清除（SC-2 于本 session 补齐 F6 广播侧闭环）。设计层 BD-1/BD-2/SC-1/SC-2 的意图与数学经对抗式核验成立。残余为 WARNING/INFO 级，不阻断架构生成。

**可进入**：`/gate-check`（Systems Design 阶段闸门）或 `/create-architecture`。

### 先决（非阻塞，架构前建议）
- owner 联合锁定 `AdaptationWindowBeats`（C1/C2/C4）与 `meritMultiplierOff`（C1/C5-FE5），二者先于 FE4/C5 落地。
