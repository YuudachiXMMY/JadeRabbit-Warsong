# Hot Reload (CodeFloof) — Plugin Reference

| Field | Value |
|-------|-------|
| **Publisher** | The Naughty Cult |
| **Version** | 1.12.x (⚠ 已从 Asset Store 下架，现有授权仍可使用) |
| **Verified with Unity** | 6.3 LTS (6000.3.19f1) |
| **Asset Store** | https://assetstore.unity.com/packages/tools/utilities/hot-reload-edit-code-without-compiling-250972 |
| **Official Docs** | https://hotreload.net/documentation |
| **Last Verified** | 2026-07-13 |
| **LLM Knowledge Gap** | LOW — Editor-only 工具，不影响 Runtime 代码生成 |

---

## Purpose in This Project

Hot Reload 是**纯开发期提速工具**，允许在 Play Mode 中编辑 C# 代码并即时生效，
无需等待 Unity 完整编译循环（通常 5-15 秒）。

**重要：Hot Reload 仅在 Editor 中运行，不影响任何 Runtime 行为，不打包进构建产物。**

---

## Editor-Only 声明

| 构建目标 | Hot Reload 状态 |
|---------|----------------|
| Unity Editor（开发） | 活跃 — 监听代码变更并热应用 |
| Standalone Player 构建 | 完全排除 — 不编译、不打包 |
| CI/CD 构建 | 完全忽略 — 无 Editor 依赖 |
| 移动端构建（iOS/Android） | 完全排除 |

---

## 已知限制

### 不支持热重载的场景

| 场景 | 解决方案 |
|------|---------|
| DOTS ECS `ISystem` 代码 | 须完整重新编译（标准 `IJob`/`IJobFor` + `[BurstCompile]` 可热重载） |
| 静态构造函数 / 静态字段初始化 | 热重载后值不会重置 — 单例模式须注意 |
| 新增 MonoBehaviour 脚本 | 须重新编译才能挂载到 GameObject |
| Assembly Definition 变更 | 须完整重新编译 |
| Shader 修改 | 不在 Hot Reload 范围内 |

### 与其他插件的交互

| 插件 | 注意事项 |
|------|---------|
| **Odin Inspector** | 序列化检视面板在热重载后可能不刷新 — 点击 Inspector 窗口触发刷新 |
| **Animancer** | 运行时状态在热重载后保持，但新增的 AnimancerState 类型须重新编译 |
| **TopDownEngine** | TDE 的 Singleton 基类在热重载后保持引用，一般无问题 |

---

## 最佳实践

1. **单例模式注意**: 使用 `[RuntimeInitializeOnLoadMethod]` 重置静态引用，
   确保热重载后单例不会持有旧实例。
2. **不依赖热重载做测试**: 所有功能必须能通过完整编译验证；
   热重载仅加速迭代，不替代正式编译。
3. **团队规范**: Hot Reload 为可选工具，团队成员可自行选择是否启用。

---

## ADR References

| ADR | 涉及 Hot Reload 的决策 |
|-----|----------------------|
| *(暂无)* | Editor-only 工具，无需架构决策 |

---

## Upgrade History

| Date | From | To | Notes |
|------|------|----|-------|
| *(首次集成时填写)* | — | x.x.x | 初始集成 |
