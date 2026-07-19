# M0 Spike Report — Unity Behavior Package (com.unity.behavior) 兼容性验证

> **Status**: COMPLETED
> **Date**: 2026-07-18
> **Blocks**: ADR-D2 重新评估（Unity Behavior vs Behavior Designer）
> **Engine**: Unity 6.3 LTS (6000.3.19f1)

---

## Executive Summary

| 项目 | 结果 |
|------|------|
| **包版本** | `com.unity.behavior` v1.0.16（正式发布，非 Preview） |
| **最低 Unity** | 6000.0.16f1 — 与项目 6000.3.19f1 兼容 ✅ |
| **编译** | ✅ 零错误（已导入，Package Manager 缓存正常） |
| **Blackboard API** | ✅ `SetVariableValue<T>` / `GetVariable<T>` — 与 ADR-D2 BeatClockBridge 需求匹配 |
| **Action 节点生命周期** | ✅ `OnStart()` / `OnUpdate()` → `Status.Running/Success/Failure` |
| **事件通道** | ✅ `EventChannel<T0..T3>` — 可用于 BeatSnapshot 事件驱动 |
| **子图（Boss 阶段）** | ✅ `RunSubgraph` / `RunSubgraphDynamic` |
| **Observer Abort** | ✅ `IObserverAbort` — 条件变化时主动中断低优先级分支 |
| **BLOCKER** | **0** |
| **WARNING** | **2**（见下文） |

**判定: PASS — Unity Behavior Package 满足 ADR-D2 全部功能需求，且消除第三方兼容性风险。建议重新评估 ADR-D2 决策。**

---

## 1. 包状态 & 兼容性

```json
{
  "name": "com.unity.behavior",
  "version": "1.0.16",
  "unity": "6000.0",
  "unityRelease": "16f1"
}
```

**关键发现**: ADR-D2 于 2026-07-18 基于以下理由排除 Unity Behavior：

> "HIGH post-cutoff risk — `com.unity.behavior` was introduced in Unity 6.0 (post-LLM-cutoff) and has no verified API reference... Package was in preview/experimental state as of Unity 6.0-6.1."

**实际状态**: v1.0.16 是正式发布版本（非 Preview/Experimental），changelog 显示成熟的 bug 修复与功能增量，NOT 破坏性 API 变更。包已在项目中编译通过。

---

## 2. Node 生命周期 — `Node` 基类

**源码位置**: `Library/PackageCache/com.unity.behavior@.../Runtime/Execution/Node.cs`

### 2.1 Status 枚举 ✅ CONFIRMED

```csharp
public enum Status
{
    Uninitialized,  // 未开始
    Running,        // 活跃，等待完成 ← CombatDirectorTokenTask 需要
    Success,        // 成功完成 ← token 获取
    Failure,        // 失败 ← token 超时
    Waiting,        // 等待子节点
    Interrupted,    // 被外部中断（Abort/Restart）
}
```

**对比 BD**: BD 的 `TaskStatus { Inactive, Running, Success, Failure }` → Unity Behavior 多出 `Waiting` 和 `Interrupted`，功能更丰富。

### 2.2 生命周期方法 ✅ CONFIRMED

| 方法 | 调用时机 | 等价 BD API |
|------|---------|------------|
| `OnSetup()` | 图实例初始化时调用一次 | `OnAwake()` |
| `OnStart()` → `Status` | 节点开始运行 | `OnStart()` |
| `OnUpdate()` → `Status` | Running 状态下每帧调用 | `OnUpdate()` → `TaskStatus` |
| `OnEnd()` | 节点停止（Success/Failure/Interrupted） | `OnEnd()` |
| `OnTeardown()` | 图实例释放时调用一次 | N/A (BD 无等价) |

### 2.3 节点内访问 GameObject ✅

```csharp
// Node.cs:87 — 获取挂载 BehaviorGraphAgent 的 GameObject
public GameObject GameObject => Graph.GameObject;
```

---

## 3. Blackboard 系统

### 3.1 `BlackboardVariable<T>` 类层次 ✅ CONFIRMED

**源码位置**: `Runtime/Execution/Blackboard/BlackboardVariable.cs`

```
BlackboardVariable (abstract)
  └── BlackboardVariable<DataType> (generic, [Serializable])
        ├── .Value : DataType { get; set; }       ← 触发 OnValueChanged
        ├── .SetValueWithoutNotify(DataType)      ← 静默写入
        ├── .OnValueChanged : event               ← 响应式订阅
        └── implicit operator DataType(...)       ← 隐式类型转换
```

### 3.2 节点内 Blackboard 变量声明模式 ✅ CONFIRMED

```csharp
// 自定义 Action 节点内——字段直接绑定 Blackboard 变量
[SerializeReference] public BlackboardVariable<float> CurrentBPM;
[SerializeReference] public BlackboardVariable<int>   CurrentBeat;
[SerializeReference] public BlackboardVariable<float> TimeToNextBeat;
```

图编辑器自动将这些字段映射到 Blackboard 变量——**无需 `[SharedVariable]` attribute**。

### 3.3 外部写入 API（BeatClockBridge 核心接口）✅ CONFIRMED

**源码位置**: `Runtime/Execution/Components/BehaviorGraphAgent.cs:541`

```csharp
public bool SetVariableValue<TValue>(string variableName, TValue value)
```

**等价 BD API**: `BehaviorTree.SetVariableValue(string, object)` — 签名几乎相同！

**外部读取 API**:
```csharp
public bool GetVariable<TValue>(string variableName, out BlackboardVariable<TValue> variable)
```

### 3.4 `OnValueChanged` 事件（响应式模式）✅ BONUS

```csharp
// BlackboardVariable.cs:44
public event ValueChangedCallback OnValueChanged = delegate { };
```

BD 无等价机制。Unity Behavior 支持 Observer Abort 基于 Blackboard 变量变化主动中断分支——这意味着 `BeatClockBridge` 写入 beat 数据后，BT 可以**主动响应**而非被动轮询。

---

## 4. BehaviorGraphAgent 组件

**源码位置**: `Runtime/Execution/Components/BehaviorGraphAgent.cs`

### 4.1 执行顺序 ✅ CONFIRMED

```csharp
[DefaultExecutionOrder(-50)]
public class BehaviorGraphAgent : MonoBehaviour, ISerializationCallbackReceiver
```

`BeatClockBridge` 须设置 `[DefaultExecutionOrder(-100)]`（或更小），确保 beat 数据写入发生在 BT tick 之前。

### 4.2 生命周期控制 ✅

| 方法 | 功能 |
|------|------|
| `Init()` | 创建图实例 + 应用 Blackboard overrides |
| `Start()` | 开始执行 |
| `End()` | 停止执行 |
| `Restart()` | 重启执行 |
| `Update()` | 每帧 tick（自动 Init + Start） |

**TDE 集成注意**: 敌人死亡时调用 `agent.End()`，重生时调用 `agent.Restart()`。

---

## 5. 自定义 Action 编写模式

从 `WaitAction.cs` 和 `NavigateToTargetAction.cs` 提取的标准模式：

```csharp
using System;
using Unity.Properties;
using UnityEngine;

namespace Unity.Behavior
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Request Attack Token",
        description: "Requests a beat-slot attack token from CombatDirector.",
        story: "[Agent] requests attack token with [TimeoutBeats] beat timeout",
        category: "Action/Combat",
        id: "unique-guid-here")]
    internal partial class CombatDirectorTokenAction : Action
    {
        [SerializeReference] public BlackboardVariable<float> TimeoutBeats;
        [SerializeReference] public BlackboardVariable<float> CurrentBPM;

        private bool  _tokenGranted;
        private float _timeoutTimer;

        protected override Status OnStart()
        {
            _tokenGranted = false;
            _timeoutTimer = 0f;
            // CombatDirector.RequestAttackToken(GameObject);
            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (_tokenGranted) return Status.Success;
            _timeoutTimer += Time.deltaTime;
            float bpm = CurrentBPM != null ? CurrentBPM.Value : 120f;
            if (_timeoutTimer >= TimeoutBeats.Value * (60f / bpm))
                return Status.Failure;
            return Status.Running;
        }

        protected override void OnEnd()
        {
            // Cleanup if needed
        }

        public void OnTokenGranted() => _tokenGranted = true;
    }
}
```

**关键区别 vs ADR-D2 BD 版本**:
- `Action` 基类来自 `Unity.Behavior` 命名空间（非 `BehaviorDesigner.Runtime.Tasks`）
- 返回 `Status` 枚举（非 `TaskStatus`）
- Blackboard 变量为 `[SerializeReference] public BlackboardVariable<T>`（非 `[SharedVariable]`）
- 需要 `[Serializable, GeneratePropertyBag]` + `[NodeDescription]` attribute

---

## 6. 高级功能验证

### 6.1 Composites（组合节点）✅

| 节点 | 功能 | ADR-D2 需求覆盖 |
|------|------|----------------|
| `SequenceComposite` | 顺序执行 | patrol→chase→attack 流程 |
| `SelectorComposite` | 选择执行（Fallback） | 优先级决策 |
| `ParallelAllComposite` | 并行等待全部完成 | 多条件检查 |
| `SwitchComposite` | enum 值分支 | **Boss 阶段切换** ✅ |
| `RandomComposite` | 随机选择子节点 | 行为多样性 |

### 6.2 Modifiers（装饰器）✅

| 节点 | 功能 |
|------|------|
| `RepeaterModifier` | 循环执行 |
| `TimeoutModifier` | 超时限制 |
| `CooldownModifier` | 冷却期 |
| `ObserverAbortModifier` | **条件变化时中断** ← beat 状态响应 |
| `AbortModifier` | 外部中断 |

### 6.3 Subgraphs（子图）✅ — Boss 阶段设计

- `RunSubgraph`: 静态引用子图——适合固定阶段
- `RunSubgraphDynamic`: 运行时动态切换子图——**Boss 阶段转换** ✅

### 6.4 Event Channel（事件通道）✅

```csharp
// ScriptableObject-based typed event channel
public abstract class EventChannel<T0> : EventChannelBase
{
    public void SendEventMessage(T0 value) { ... }
}
```

支持 `StartOnEvent` 装饰器——BT 子树可在事件触发时启动。可创建 `BeatSnapshotEventChannel` 作为 beat 数据的 ScriptableObject 事件总线。

---

## 7. ADR-D2 架构图映射

### 原 BD 架构 → Unity Behavior 等价

```
ADR-D2 (Behavior Designer)              →  Unity Behavior 等价
─────────────────────────────────────────────────────────────────
BehaviorTree (Component)                 →  BehaviorGraphAgent (Component)
BehaviorTree.SetVariableValue(name, val) →  BehaviorGraphAgent.SetVariableValue<T>(name, val)
SharedVariable<T>                        →  BlackboardVariable<T>
[SharedVariable] attribute               →  [SerializeReference] public BlackboardVariable<T>
Action : BehaviorDesigner.Runtime.Tasks  →  Action : Unity.Behavior.Node
TaskStatus.Running/Success/Failure       →  Node.Status.Running/Success/Failure
OnAwake()                                →  OnSetup()
BeatClockBridge (custom MonoBehaviour)   →  BeatClockBridge (same pattern, targets BehaviorGraphAgent)
Script Execution Order: before BD        →  [DefaultExecutionOrder(-100)] before Agent's -50
```

### 修订后架构图

```
BeatClock (F2)
    │  BeatStateSnapshot event (each beat)
    ▼
BeatClockBridge : MonoBehaviour  [DefaultExecutionOrder(-100)]
    │  agent.SetVariableValue("CurrentBPM", snapshot.Bpm)
    │  agent.SetVariableValue("CurrentBeat", snapshot.Beat)
    │  agent.SetVariableValue("TimeToNextBeat", snapshot.TimeToNextBeat)
    ▼
BehaviorGraphAgent [DefaultExecutionOrder(-50)] — on enemy prefab
    │  Blackboard: CurrentBPM, CurrentBeat, TimeToNextBeat
    ▼
Behavior Graph (visual editor)
    ├─ Patrol sub-tree (Sequence)
    │      └─ TDESetMovementAction → CharacterMovement.SetMovement(Vector2)
    ├─ Chase sub-tree (Sequence)
    │      └─ TDESetMovementAction → CharacterMovement.SetMovement(playerDir)
    └─ Attack sub-tree (Sequence)
           ├─ CombatDirectorTokenAction [returns Running until token granted]
           └─ TDETriggerAbilityAction → CharacterAbility.TriggerAbility()
```

---

## 8. Warnings

### W1: `[GeneratePropertyBag]` 要求 `partial` class

所有自定义 Action 节点必须声明为 `partial class`（如示例中 `internal partial class NavigateToTargetAction`）。
`[GeneratePropertyBag]` 使用 source generator 生成序列化代码，非 partial 会编译失败。

**影响**: ADR-D2 示例代码中的 `CombatDirectorTokenTask`、`TDESetMovementTask`、`TDETriggerAbilityTask` 须改为 `partial class`。

### W2: 图编辑器中变量绑定

Unity Behavior 的 Blackboard 变量绑定发生在**图编辑器 UI** 中，而非代码中的 attribute 标注。
这意味着：
- 节点声明 `[SerializeReference] public BlackboardVariable<float> CurrentBPM;` 字段
- 在图编辑器中将该字段拖拽绑定到 Blackboard 的 "CurrentBPM" 变量
- **或者**从外部通过 `BehaviorGraphAgent.SetVariableValue<float>("CurrentBPM", value)` 直接写入

`BeatClockBridge` 使用 `SetVariableValue` 外部写入模式，完全绕过图编辑器绑定，无问题。

---

## 9. ADR-D2 对比评估

| 评估维度 | Behavior Designer (Opsive) | Unity Behavior (com.unity.behavior) |
|----------|--------------------------|-------------------------------------|
| **包状态** | 第三方 Asset Store | Unity 第一方 Package Manager |
| **版本** | 未验证（未导入） | v1.0.16 ✅ 已验证 |
| **编译** | ⏳ 未验证 | ✅ 零错误 |
| **API 风险** | MEDIUM — LLM 训练数据可能过时 | ✅ 源码已完整验证 |
| **Unity 版本兼容** | 须 M0 Spike 验证 | ✅ 原生兼容 Unity 6.3 |
| **Blackboard 写入** | `BehaviorTree.SetVariableValue()` | `BehaviorGraphAgent.SetVariableValue<T>()` |
| **Async 等待** | `TaskStatus.Running` | `Node.Status.Running` |
| **Boss 阶段** | BT 子树手动切换 | `RunSubgraphDynamic` + `SwitchComposite` |
| **事件驱动** | 无原生事件系统 | `EventChannel<T>` + `StartOnEvent` |
| **响应式 Abort** | 无内建 | `ObserverAbortModifier` + `OnValueChanged` |
| **许可证** | Asset Store 商业许可 | Unity Package（项目自带） |
| **长期维护** | Opsive 商业公司 | Unity Technologies |
| **额外依赖** | +1 第三方包 | 0（已含在引擎中） |
| **学习资源** | 社区文档 | 官方文档 + Demo 项目 |

---

## 10. 结论 & 建议

### 判定: PASS — 建议 ADR-D2 改选 Unity Behavior Package

**理由**:

1. **风险消除**: ADR-D2 的 3 个 HIGH 风险（BD 编译兼容性、SharedVariable API 偏移、Unity 6.3 SerializeField 变更）全部不适用——Unity Behavior 是第一方包，天然兼容。

2. **功能超集**: Unity Behavior 提供 BD 的全部核心功能（BT、Blackboard、异步等待）**加上** EventChannel 事件驱动、ObserverAbort 响应式中断、RunSubgraphDynamic 动态子图——这些在 BD 中需要额外插件或自定义代码。

3. **零额外成本**: 不需要导入 BD（第三方包）。减少项目依赖（当前已有 TDE + FMOD + EasySave + Odin + Animancer + Feel + DOTween + Hot Reload = 8 个第三方包）。

4. **API 已验证**: 本 spike 已从源码确认所有关键 API 签名，无 BLOCKER。

### 后续步骤

1. **ADR-D2 修订**: 将 Decision 从 "Behavior Designer" 改为 "Unity Behavior Package"；原 BD 方案降级为 Alternative
2. 更新 `BeatClockBridge` 接口代码（`BehaviorTree` → `BehaviorGraphAgent`）
3. 更新自定义 Task 基类（BD `Action` → Unity Behavior `Action`，`partial class`）
4. 移除 BD 购买为决策依据的论点
5. 考虑 `EventChannel<BeatStateSnapshot>` 作为 BeatClockBridge 的替代方案（更 Unity-native）

---

## Appendix: 源码引用索引

| 文件 | 行号 | 内容 |
|------|------|------|
| `com.unity.behavior@.../package.json` | 3 | version: "1.0.16" |
| `com.unity.behavior@.../Runtime/Execution/Node.cs` | 17-31 | `Node.Status` 枚举 |
| `com.unity.behavior@.../Runtime/Execution/Node.cs` | 153-166 | `OnSetup` / `OnStart` / `OnUpdate` / `OnEnd` / `OnTeardown` |
| `com.unity.behavior@.../Runtime/Execution/Nodes/Action.cs` | 12 | `Action : Node` |
| `com.unity.behavior@.../Runtime/Execution/Blackboard/BlackboardVariable.cs` | 128 | `BlackboardVariable<DataType>` 泛型类 |
| `com.unity.behavior@.../Runtime/Execution/Blackboard/BlackboardVariable.cs` | 44 | `OnValueChanged` 事件 |
| `com.unity.behavior@.../Runtime/Execution/Blackboard/Blackboard.cs` | 144 | `SetVariableValue<TValue>(string name, TValue value)` |
| `com.unity.behavior@.../Runtime/Execution/Components/BehaviorGraphAgent.cs` | 46 | `[DefaultExecutionOrder(-50)]` |
| `com.unity.behavior@.../Runtime/Execution/Components/BehaviorGraphAgent.cs` | 384 | `GetVariable<TValue>(string, out BlackboardVariable<TValue>)` |
| `com.unity.behavior@.../Runtime/Execution/Components/BehaviorGraphAgent.cs` | 541 | `SetVariableValue<TValue>(string, TValue)` |
| `com.unity.behavior@.../Runtime/Execution/Nodes/Actions/Delay/WaitAction.cs` | 1-41 | 自定义 Action 编写范例 |
| `com.unity.behavior@.../Runtime/Execution/Nodes/Actions/Navigation/NavigateToTargetAction.cs` | 1-214 | 复杂 Action 范例（Running 状态） |
| `com.unity.behavior@.../Runtime/Execution/Nodes/Events/EventChannel.cs` | 57-123 | `EventChannel` 基类 |
| `com.unity.behavior@.../Runtime/Execution/Nodes/Events/EventChannel.cs` | 128 | `EventChannel<T0>` 泛型 |
| `com.unity.behavior@.../Runtime/Execution/Nodes/Composite.cs` | 26 | `Composite : Node, IParent` |
| `com.unity.behavior@.../Runtime/Execution/Attributes/NodeDescriptionAttribute.cs` | 9 | `[NodeDescription]` attribute |
