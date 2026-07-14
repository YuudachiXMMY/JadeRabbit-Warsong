# TopDownEngine (More Mountains) — Plugin Reference

| Field | Value |
|-------|-------|
| **Publisher** | More Mountains |
| **Version** | 4.5 |
| **Verified with Unity** | 6.3 LTS (6000.3.19f1) |
| **Asset Store** | https://assetstore.unity.com/packages/templates/systems/topdown-engine-89636 |
| **Official Docs** | https://topdown-engine-docs.moremountains.com/ |
| **Last Verified** | 2026-07-13 |
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
- ✅ 订阅 TDE 事件（`MMEventManager.AddListener<MMGameEvent>()`）
- ✅ 实现 TDE 接口（`IDamageable`、`IPickable` 等）
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

### LevelManager
关卡加载、房间切换、死亡重置。F6 GDD 需定义与 LevelManager 的场景管理协议。

### MMFeedbacks / Feel
所有 Perfect/Good/Miss 的视觉和音效反馈通过 MMFeedbacks 触发，
由 CombatCore 评级系统发布事件，Feel 订阅并播放对应反馈序列。

---

## Known Constraints for This Project

1. **Animancer 冲突风险**: TDE 使用 `Animator` 组件管理角色基础动画；
   Animancer 接管攻击动画层。两者共存时须分层（Layer Index 隔离），见 Animancer 插件文档。

2. **MMInput 与 Unity Input System**: TDE 3.x 支持新 Input System，但配置须通过
   TDE 的 InputManager 适配器。不得绕过 TDE InputManager 直接读取 Input Action——
   否则 TDE 的 AbilityInputAuthorization 系统会失效。

3. **LevelManager 单例**: TDE LevelManager 是单例，不可在同一场景中存在两个实例。
   测试场景须注意。

4. **版本升级风险**: 升级 TDE 前须备份并逐项对比 CHANGELOG，
   重点检查 `Character`、`Health`、`CharacterAbility` 基类的 API 变更。

---

## ADR References

| ADR | 涉及 TDE 的决策 |
|-----|----------------|
| D3（待创建） | TDE 集成边界合约的正式 ADR |
| D4（待创建） | Animancer 与 TDE Animator 层冲突解决方案 |

---

## Upgrade History

| Date | From | To | Notes |
|------|------|----|-------|
| *(首次集成时填写)* | — | x.x.x | 初始集成 |
