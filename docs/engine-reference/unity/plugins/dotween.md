# DOTween Pro (Demigiant) — Plugin Reference

| Field | Value |
|-------|-------|
| **Publisher** | Demigiant |
| **Version** | 1.3.030 |
| **Verified with Unity** | 6.3 LTS (6000.3.19f1) |
| **Asset Store** | https://assetstore.unity.com/packages/tools/visual-scripting/dotween-pro-32416 |
| **Official Docs** | http://dotween.demigiant.com/documentation.php |
| **Last Verified** | 2026-07-13 |
| **LLM Knowledge Gap** | LOW — DOTween API 稳定，LLM 训练数据基本可靠；注意 Unity 6 弃用 API 的适配 |

---

## Purpose in This Project

DOTween 专用于 **UI 过渡动画**，与 Feel/MMFeedbacks 的职责严格分离：

| DOTween 负责 | Feel/MMFeedbacks 负责 |
|-------------|---------------------|
| UI 面板弹入/弹出过渡 | Perfect/Good/Miss 节奏反馈 |
| 心法选择界面动画 | 击杀/连击视觉反馈 |
| 结算界面数值滚动 | 屏幕震动、色差效果 |
| 菜单切换淡入淡出 | HUD 脉冲（节奏驱动） |

---

## DOTween Pro vs Free

本项目使用 **DOTween Pro**，Pro 版额外提供：
- DOTween Animation 组件（可视化编辑器）
- DOTween Path 路径动画
- TextMeshPro 集成

Free 版足以覆盖代码驱动的补间需求，但 Pro 的可视化工具对 UI 设计迭代更高效。

---

## Integration Rules

### 与 Feel 的边界划分

**核心规则：同一 Transform 上不得同时运行 DOTween 和 Feel 动画。**

| 场景 | 使用 |
|------|------|
| UI 元素过渡（非节奏相关） | DOTween |
| 节奏反馈（Perfect/Good/Miss） | Feel |
| HUD 脉冲（跟拍） | Feel |
| 菜单/弹窗出入场 | DOTween |

### 初始化要求

游戏启动时须在入口场景调用：

```csharp
DOTween.Init(recycleAllByDefault: true, useSafeMode: false, logBehaviour: LogBehaviour.ErrorsOnly);
DOTween.SetTweensCapacity(maxTweeners: 200, maxSequences: 50);
```

- `recycleAllByDefault: true` — 复用 Tween 实例，减少 GC
- `useSafeMode: false` — 生产模式关闭安全检查，提升性能
- 容量根据 UI 复杂度调整

### 禁止模式

- ❌ 在 `Update()` / `FixedUpdate()` 中每帧创建新 Tween
- ❌ 在节奏关键帧动画中使用 DOTween（使用 Feel）
- ❌ 与 Feel 在同一 Transform 上双重驱动
- ❌ 不调用 `Kill()` 就销毁持有 Tween 的 GameObject

---

## Known Constraints

1. **GC 压力**: 未复用的 Tween 会产生 GC；启用 `recycleAllByDefault` 并在不用时 `Kill()`。
2. **TimeScale 敏感**: DOTween 默认跟随 `Time.timeScale`；暂停菜单时须使用
   `.SetUpdate(UpdateType.Normal, isIndependentUpdate: true)` 使暂停菜单动画不受影响。
3. **Unity 6 兼容性**: DOTween 使用反射访问部分 Unity 内部 API；
   升级 Unity 后须运行 DOTween Utility Panel 的 "Setup DOTween..." 重新配置。

---

## ADR References

| ADR | 涉及 DOTween 的决策 |
|-----|-------------------|
| *(暂无)* | DOTween 用途限定于 UI 动画，无需独立 ADR |

---

## Upgrade History

| Date | From | To | Notes |
|------|------|----|-------|
| *(首次集成时填写)* | — | x.x.x | 初始集成 |
