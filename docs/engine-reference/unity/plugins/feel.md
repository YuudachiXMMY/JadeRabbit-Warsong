# Feel / MMFeedbacks (More Mountains) — Plugin Reference

| Field | Value |
|-------|-------|
| **Publisher** | More Mountains |
| **Version** | 5.9.1 |
| **Verified with Unity** | 6.3 LTS (6000.3.19f1) |
| **Asset Store** | https://assetstore.unity.com/packages/tools/particles-effects/feel-205984 |
| **Official Docs** | https://feel-docs.moremountains.com/ |
| **Last Verified** | 2026-07-13 |
| **LLM Knowledge Gap** | MEDIUM — Feel 3.x→5.x 多次迁移涉及基类和组件重命名，LLM 可能引用旧 API |

---

## Purpose in This Project

Feel 提供反馈序列基础设施，驱动所有玩家操作的视觉/音效/触感反馈：

| Feel 负责 | CombatCore 负责 |
|-----------|----------------|
| Perfect/Good/Miss 视觉反馈序列 | 评级事件发布（RatingEvent） |
| 屏幕震动、色差、时间缩放 | 伤害计算触发 |
| 击杀/连击里程碑反馈 | 连招状态管理 |
| HUD 脉冲动画 | 拍点时序（BeatClock） |

---

## Relationship with TopDownEngine

Feel 随 TopDownEngine 捆绑引入，但有**独立版本号**和**独立升级路径**。
TDE 内部已集成 MMFeedbacks 用于角色受击、死亡、拾取等反馈。
本项目在此基础上，额外为节奏评级系统添加自定义反馈序列。

---

## MMFeedbacks 2.x → 3.x → 4.x 迁移注意事项

| 版本 | 基类 | 命名空间 |
|------|------|---------|
| 2.x | `MMFeedback` | `MoreMountains.Feedbacks` |
| 3.x+ | `MMF_Feedback`（重命名） | `MoreMountains.Feedbacks` |
| 4.x | `MMF_Feedback` | `MoreMountains.Feedbacks` |

- 3.x 废弃了 `MMFeedback` 基类，改用 `MMF_Feedback`
- 4.x 维持 3.x 命名，但内部优化了序列化和编辑器性能
- **本项目统一使用 `MMF_Feedback` 基类**，禁止引用已废弃的 `MMFeedback`

---

## Integration Rules

1. **事件驱动**: CombatCore 发布评级事件 → Feel 订阅播放反馈序列。
   CombatCore 程序集**不得** `using MoreMountains.Feedbacks`。
2. **反馈序列配置**: 通过 ScriptableObject 或 Prefab 配置反馈序列，不在代码中硬编码。
3. **与 DOTween 边界**: 同一 Transform 上不得同时运行 Feel 反馈和 DOTween 补间。
   Feel 用于节奏反馈，DOTween 用于 UI 过渡。

---

## Known Constraints

1. **GC Allocation**: `MMFeedbacks.PlayFeedbacks()` 在首次调用时会分配内存；
   建议在场景加载时预热（Initialization）常用反馈序列。
2. **时间缩放**: Feel 的 TimeScale feedback 与 Unity `Time.timeScale` 直接交互；
   BeatClock 使用 `AudioSettings.dspTime` 独立于 timeScale，不受影响。
3. **Cinemachine 依赖**: CameraShake feedback（`MMF_CinemachineImpulse`）依赖 Cinemachine Impulse；
   确保场景中有 CinemachineImpulseListener。Cinemachine 相关反馈位于
   `MoreMountains.FeedbacksForThirdParty` 命名空间。

---

## ADR References

| ADR | 涉及 Feel 的决策 |
|-----|-----------------|
| D1（待创建） | 音频中间件选型（FMOD vs Unity Audio DSP）— 影响 Feel AudioSource feedback |
| D3（待创建） | TDE 集成边界合约 — 包含 Feel 作为 TDE 捆绑组件的边界 |

---

## Upgrade History

| Date | From | To | Notes |
|------|------|----|-------|
| *(首次集成时填写)* | — | x.x.x | 随 TDE 捆绑引入 |
