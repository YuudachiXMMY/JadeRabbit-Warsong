# Animancer (Kybernetik) — Plugin Reference

| Field | Value |
|-------|-------|
| **Publisher** | Kybernetik |
| **Version** | 8.4.0 |
| **Verified with Unity** | 6.3 LTS (6000.3.19f1) |
| **Asset Store** | https://kybernetik.com.au/animancer/ |
| **Official Docs** | https://kybernetik.com.au/animancer/docs/ |
| **Last Verified** | 2026-07-13 |
| **LLM Knowledge Gap** | MEDIUM — Animancer 7.x → 8.x 有 API 变更，以官方文档为准 |

---

## Purpose in This Project

Animancer 负责《玉兔：破阵》的**攻击动画层管理**，解决两个核心技术问题：

1. **BPM 变速适配**: 攻击动画须随当前 BPM 动态缩放速度（`AnimancerState.Speed`），
   Animator Controller 的静态状态机无法在运行时灵活调整动画速度。
2. **连招图驱动**: 连招树的节点切换需要精确的动画状态控制和过渡时机，
   Animancer 的 `AnimancerState` API 比 Animator.CrossFade 更可预期。

---

## Layer Architecture（层架构）

本项目使用 **双层动画架构**：

```
Animator Component
├── Layer 0 (TDE BaseLayer): 移动、待机、受击、死亡 — 由 TDE 管理
└── Layer 1 (Animancer AttackLayer): 攻击动画序列 — 由 Animancer 管理
```

**关键约束**: 两层之间的权重切换须通过明确的接口协议，
不允许 TDE 状态机和 Animancer 同时控制同一个骨骼属性（Avatar Mask 分层）。

---

## Core API Patterns（项目使用的核心 API）

### 播放攻击动画并适配 BPM

```csharp
// BeatClock 提供当前 BPM，AttackNode 包含动画 Clip 引用
var state = _animancer.Play(attackNode.Clip, fadeDuration);
state.Speed = BeatClock.NormalizedBpmScale(attackNode.BaseBeats);
// NormalizedBpmScale = currentBpm / attackNode.ReferenceBpm
```

### 监听动画结束（连招节点推进）

```csharp
state.Events.OnEnd = () => _comboGraph.OnAnimationEnd(attackNode);
```

### 切换到 Idle（连招结束或 Miss 超时）

```csharp
_animancer.Play(_idleState, 0.15f);
```

---

## Known Constraints

1. **与 TDE 的 Animator 层冲突**:
   TDE 在 `Character` 类中直接调用 `Animator.SetTrigger()`、`Animator.SetBool()` 等 API。
   当 Animancer 接管 Layer 1 时，须确保 TDE 只操作 Layer 0，否则会互相覆盖。
   **解决方案**: 在 TDE Character 的 `CharacterAnimationParameterInitialization` 中
   配置 Avatar Mask，将攻击相关骨骼排除在 Layer 0 之外。详见 D4 ADR。

2. **Animancer Pro vs Free**:
   确认购买的是 **Animancer Pro**（支持 Layers 和 ControllerTransition）。
   Free 版不支持多层，会破坏双层架构。

3. **Unity 6.3 LTS 兼容性**:
   Animancer 8.x 声称支持 Unity 6，但 Input System 1.11 和新 Animation Rigging 可能有边界情况。
   M0 Spike 时须验证攻击动画层在 Unity 6.3 下的基本播放路径。

4. **不支持 DOTS/Entities**:
   Animancer 基于 GameObject + Animator 架构，不兼容 DOTS ECS。
   本项目不使用 DOTS，无影响。

---

## ADR References

| ADR | 涉及 Animancer 的决策 |
|-----|----------------------|
| D4（待创建） | Animancer 攻击层与 TDE Animator 层冲突解决正式 ADR |
| D3（待创建） | TDE 集成边界合约（含动画层分工） |

---

## Upgrade History

| Date | From | To | Notes |
|------|------|----|-------|
| *(首次集成时填写)* | — | x.x.x | 初始集成 |
