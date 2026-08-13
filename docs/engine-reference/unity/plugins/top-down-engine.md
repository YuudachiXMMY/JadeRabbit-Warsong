# TopDownEngine (More Mountains) — Plugin Reference

| Field | Value |
|-------|-------|
| **Publisher** | More Mountains |
| **Version** | 4.5 |
| **Verified with Unity** | 6.3 LTS (6000.3.19f1) |
| **Asset Store** | https://assetstore.unity.com/packages/templates/systems/topdown-engine-89636 |
| **Official Docs** | https://topdown-engine-docs.moremountains.com/ |
| **Last Verified** | 2026-08-13（ADR-D13 VR1：TopDownEngineEvent run-end 事件成员名核对） |
| **LLM Knowledge Gap** | HIGH — LLM 训练数据可能基于 TDE 2.x；当前版本 API 须以官方文档为准 |

---

## Purpose in This Project

TopDownEngine 在《玉兔：破阵》中作为 **F6 TDE & Animancer 集成层**的基础设施提供者：

| TDE 负责 | 自建 CombatCore 程序集负责 |
|----------|--------------------------|
| 移动系统（TopDownController） | 节奏量化攻击（RhythmCore） |
| 生命系统（Health） | 连招图状态机（ComboGraph） |
| 关卡管理（LevelManager） | 心法运行时（PerkRuntime） |
| 反馈基础设施（MMFeedbacks/Feel） | 拍点时序（BeatClock） |
| 相机（Cinemachine 集成） | 伤害结算（DamageCalculator） |

---

## Integration Boundary Protocol（接口合约）

这是项目最重要的架构约束，违反会导致 v1 覆写陷阱重演：

### 允许
- ✅ 继承 TDE 类（如 `class PlayerCharacter : Character`）
- ✅ 订阅 TDE 事件（实现 `MMEventListener<T>` 接口，调用 `this.MMEventStartListening<T>()` / `this.MMEventStopListening<T>()`）
- ✅ 触发 TDE 事件（`MMGameEvent.Trigger("EventName")`）
- ✅ 在 TDE 的 `Ability` 系统内添加自定义 Ability（`CharacterAbility` 子类）

### 禁止
- ❌ 直接修改 TDE 源文件（升级时会被覆盖）
- ❌ 在 CombatCore 程序集中 using TDE 命名空间的内部实现类
- ❌ 绕过 TDE Health 系统做伤害结算（须通过 Health.Damage() 调用）
- ❌ 用 TDE CharacterMovement 控制攻击动画速度（由 Animancer 管理）

---

## Key Systems Used

### TopDownController
角色移动（物理或运动学）。本项目使用 **运动学（Kinematic）模式**，
因为节奏量化攻击需要帧精确的位移控制，物理模拟的不确定性不可接受。

### Character + CharacterAbility
自定义攻击 Ability 继承 `CharacterAbility`，实现 `ProcessAbility()` 和 `Initialization()`。
节奏量化逻辑**不在 Ability 内部**，Ability 只负责触发动画层和发送事件到 CombatCore。

### Health
生命值管理和死亡事件。CombatCore 的 DamageCalculator 通过 `health.Damage()` 扣血，
不直接修改 `health.CurrentHealth`。

`Health.Damage()` 完整签名：

```csharp
virtual void Damage(float damage, GameObject instigator, float flickerDuration,
                    float invincibilityDuration, Vector3 damageDirection,
                    List<TypedDamage> typedDamages = null)
```

### LevelManager
关卡加载、房间切换、死亡重置。F6 GDD 需定义与 LevelManager 的场景管理协议。

### TopDownEngineEvent — 生命周期 / run-end 事件（ADR-D13 VR1 已核对源码）

TDE 4.5 以**类型化 struct** `TopDownEngineEvent`（**非** 字符串 `MMGameEvent`）派发关卡/角色生命周期事件。订阅方实现
`MMEventListener<TopDownEngineEvent>`，在 `OnMMEvent(TopDownEngineEvent e)` 内按 `e.EventType`（`TopDownEngineEventTypes` 枚举）匹配。

**源码真值**（`Assets/TopDownEngine/Common/Scripts/Managers/GameManager.cs:27-47` — 严禁凭训练数据臆断成员名；参照 ADR-D1 `.Respawn`→`.RespawnComplete` 教训）：

```csharp
// GameManager.cs:27-47
public enum TopDownEngineEventTypes
{
    SpawnCharacterStarts, LevelStart, LevelComplete, LevelEnd,
    Pause, UnPause, PlayerDeath, SpawnComplete,
    RespawnStarted, RespawnComplete, StarPicked, GameOver,
    CharacterSwap, CharacterSwitch, Repaint, TogglePause,
    LoadNextScene, PauseNoMenu
}
```

`TopDownEngineEvent.Trigger(TopDownEngineEventTypes eventType, Character originCharacter)` 为静态触发入口（`GameManager.cs:52-74`）。

**run-end / 关卡完成相关成员（ADR-D13 D13-2 桥接候选，逐一核对触发者）：**

| 成员 | 源码 | TDE core 是否自动触发 | run-end 语义 |
|------|------|----------------------|--------------|
| `LevelComplete` | 枚举存在（`:31`） | ❌ **不自动触发**（注释「can be triggered when the end of a level is reached」——留给游戏代码 fire） | 可作**胜利/通关**信号，但须 FE4/FE2/FE5 显式 `Trigger` |
| `LevelEnd` | 枚举存在（`:32`） | ✅ 由 `LevelManager.TriggerEndLevelEvents()`（`LevelManager.cs:340`，经 `GotoLevel()` `:329-331` 调用） | 切换关卡/离场时派发；非「玩家胜利」判定 |
| `GameOver` | 枚举存在（`:40`） | ✅ 由 `LevelManager`（`LevelManager.cs:437`，所有生命耗尽时） | **死亡侧** all-lives-lost，非胜利 |
| `PlayerDeath` | 枚举存在（`:35`） | ✅（角色死亡） | ADR-D12 死亡边界，桥接 `EndRun(Death)` |

**VR1 裁决（ADR-D13 Verification Required 1 / TD 会签条件 C3）：**
- TDE **无自动派发的原生「胜利」事件**——`GameOver` 是死亡侧，`LevelEnd` 是场景切换而非胜利判定，`LevelComplete` 存在但 TDE core 不 fire。
- 本 TDE 装机**不含 `GoalManager` 类**（无 `*GoalManager*` 源文件）。
- 结论：**胜利信号须由 FE4（Boss 击败）/ FE2（房间清场）/ FE5 显式发起** → 调 F6 `EndRun(RunOutcome.Victory)`（ADR-D13 D13-2 fallback 分支）。若选择复用 TDE 事件通道，可由游戏代码 `TopDownEngineEvent.Trigger(TopDownEngineEventTypes.LevelComplete, null)` 发起，F6 以既有 `MMEventListener<TopDownEngineEvent>` 桥接——成员名 `LevelComplete` 已核对存在。

### MMFeedbacks / Feel
所有 Perfect/Good/Miss 的视觉和音效反馈通过 MMFeedbacks 触发，
由 CombatCore 评级系统发布事件，Feel 订阅并播放对应反馈序列。

---

## Known Constraints for This Project

1. **Animancer 冲突风险**: TDE 使用 `Animator` 组件管理角色基础动画；
   Animancer 接管攻击动画层。两者共存时须分层（Layer Index 隔离），见 Animancer 插件文档。

2. **MMInput 与 Unity Input System**: TDE 4.x 支持新 Input System，须将默认的
   `InputManager` 替换为 `InputSystemManagerEventsBased`（推荐，尤其多人模式）。
   不得绕过 TDE Input 管理器直接读取 Input Action——
   否则 TDE 的 AbilityInputAuthorization 系统会失效。

3. **LevelManager 单例**: TDE LevelManager 是单例，不可在同一场景中存在两个实例。
   测试场景须注意。

4. **版本升级风险**: 升级 TDE 前须备份并逐项对比 CHANGELOG，
   重点检查 `Character`、`Health`、`CharacterAbility` 基类的 API 变更。

---

## ADR References

| ADR | 涉及 TDE 的决策 |
|-----|----------------|
| ADR-D1 | TDE 集成边界合约：JadeRabbitInputManager + TDELifecycleAdapter（F6 层） |
| ADR-D5（待创建） | Animancer 与 TDE Animator 层冲突解决方案 |
| ADR-D13 | run-end 胜利/死亡统一边界；VR1 已在此文档核对 `TopDownEngineEvent` 成员名（`LevelComplete`/`LevelEnd`/`GameOver`），确认 TDE 无原生自动胜利事件 |

---

## Upgrade History

| Date | From | To | Notes |
|------|------|----|-------|
| *(首次集成时填写)* | — | x.x.x | 初始集成 |
