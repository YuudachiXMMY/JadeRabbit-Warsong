# EasySave 3 (Moodkie) — Plugin Reference

| Field | Value |
|-------|-------|
| **Publisher** | Moodkie |
| **Version** | 3.5.26 |
| **Verified with Unity** | 6.3 LTS (6000.3.19f1) |
| **Asset Store** | https://assetstore.unity.com/packages/tools/input-management/easy-save-the-complete-save-data-serializer-system-768 (URL resolves; category slug "input-management" is legacy — actual category: Tools > Utilities) |
| **Official Docs** | https://docs.moodkie.com/easy-save-3/ |
| **Last Verified** | 2026-07-13 |
| **LLM Knowledge Gap** | LOW — ES3 API 稳定，核心序列化接口变化不大 |

---

## Purpose in This Project

EasySave 3 负责**存档与持久化**（对应系统 F5），用于 PlayerInfoSO 序列化：

| EasySave 3 负责 | 不负责 |
|-----------------|-------|
| 功勋（Merit）存档 | MonoBehaviour 状态序列化 |
| 武器解锁进度 | 场景运行时状态快照 |
| 玩家设置（音量、按键映射等） | 云端同步（ES3Cloud 不启用） |
| 延迟校准偏移量 | Unity Object 引用序列化 |

---

## Integration Rules

### 序列化边界

**核心规则：只序列化纯数据结构，禁止直接序列化 MonoBehaviour 引用。**

| 允许序列化 | 禁止序列化 |
|-----------|-----------|
| `struct` / `class`（纯数据） | `MonoBehaviour` 引用 |
| 基本类型、集合、字典 | `ScriptableObject` 实例引用 |
| 自定义序列化类型（标记 `[System.Serializable]`） | Unity Object（Texture、Mesh 等） |

### 存档数据流

```
PlayerInfoSO (Runtime) → 纯数据 DTO → ES3.Save() → 本地文件
本地文件 → ES3.Load() → 纯数据 DTO → PlayerInfoSO (Runtime)
```

- PlayerInfoSO 为 ScriptableObject，运行时持有游戏状态
- 存档时提取纯数据到 DTO（Data Transfer Object），再交给 ES3 序列化
- 加载时反向填充

### 禁止功能

- ❌ **ES3Cloud**（云备份）— 本项目不启用
- ❌ 直接序列化 MonoBehaviour 或 Unity Object 引用
- ❌ 在热路径（每帧）中调用 ES3.Save/Load — 仅在存档点触发

---

## Known Constraints

1. **文件格式**: ES3 默认使用自有二进制格式；调试时可切换为 JSON 格式
   （`ES3Settings.Format = ES3.Format.JSON`），但生产构建建议使用默认格式以提升性能。
2. **加密**: ES3 支持 AES 加密存档；若需防篡改可启用，但会增加 I/O 开销。
3. **WebGL 兼容**: ES3 在 WebGL 上使用 PlayerPrefs 作为后端而非文件系统 —
   若未来支持 Web 平台须注意存储大小限制。
4. **线程安全**: ES3 的文件操作非线程安全，所有 Save/Load 调用须在主线程执行。

---

## ADR References

| ADR | 涉及 EasySave 3 的决策 |
|-----|----------------------|
| D6（待创建） | 存档/持久化方案选型（EasySave 3 vs 自定义 JSON） |

---

## Upgrade History

| Date | From | To | Notes |
|------|------|----|-------|
| *(首次集成时填写)* | — | x.x.x | 初始集成 |
