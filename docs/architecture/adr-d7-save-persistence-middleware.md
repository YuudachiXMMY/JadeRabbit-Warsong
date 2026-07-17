# ADR-D7: Save/Persistence Middleware — EasySave 3

## Status
Accepted

## Date
2026-07-15

## Last Verified
2026-07-15

## Decision Makers
Jadyn Wu (Game Director) + Claude Code Agents (systems-designer, unity-specialist)

## Summary

F5 存档/持久化系统需要一个 Unity 兼容的序列化中间件，将玩家进度和设置数据写入本地磁盘。决定采用 **EasySave 3（Moodkie, Asset Store）**作为 `IFileStorage` 接口的生产实现，通过 `ES3Storage` 封装类将 EasySave 3 的静态 API 隔离于 `SaveService` 之外，保持中间件可替换性。

---

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (6000.3.19f1) |
| **Domain** | Core / Persistence |
| **Knowledge Risk** | LOW — EasySave 3 is a stable Asset Store plugin; its API (`ES3.Save()`, `ES3.Load()`) has not changed significantly across Unity versions. EasySave 3 v3.x is listed as Unity 6.x compatible by Moodkie. |
| **References Consulted** | `docs/engine-reference/unity/plugins/easysave-3.md`, `design/gdd/save-persistence.md` (F5 GDD), `design/gdd/so-data-layer.md` (F4 GDD — DTO type constraints) |
| **Post-Cutoff APIs Used** | None — `ES3.Save<T>()` / `ES3.Load<T>()` static API unchanged. `System.Text.Json` (Alternative 2) would require Unity 6.3 .NET SDK verification. |
| **Verification Required** | 1. Confirm EasySave 3 v3.x package installs without compile errors under Unity 6.3 LTS (6000.3.19f1). 2. Verify `ES3.Save()` / `ES3.Load()` correctly round-trips `List<MetaUpgradeEntry>` (`[Serializable]` struct array) — the most complex type in the current DTO schema. 3. Confirm EasySave 3 does NOT serialize `[NonSerialized]` fields (critical for `latencyCalibrationOffsetMs` in `PlayerInfoSO`). |

> **Note**: If project upgrades to Unity 7+, re-validate EasySave 3 compatibility and flag this ADR for review.

---

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | None |
| **Enables** | F5 Save/Persistence System implementation (immediate) |
| **Blocks** | F5 implementation start — this ADR moving to Accepted unblocks F5 |
| **Ordering Note** | F5 GDD was authored with `IFileStorage` abstraction specifically to allow this ADR to be deferred; the GDD interface contract is middleware-independent. Accepting this ADR authorizes the `ES3Storage : IFileStorage` implementation class. |

---

## Context

### Problem Statement

F5 存档系统（`save-persistence.md`）需要序列化以下数据类型：

- `PlayerProgressDTO`：`int`、`string[]`、`MetaUpgradeEntry[]`（`[Serializable]` struct）
- `LatencyCalibrationDTO`：`float`、`bool`、`int`
- `GameSettingsDTO`：`float` × 3
- `SlotMetaDTO`：`int`、`long`（UnixTimestamp）、`float`

序列化须满足：版本化迁移、文件损坏恢复、主线程同步调用、不序列化 Unity Object 引用。F5 GDD 已定义 `IFileStorage` 接口隔离中间件；ADR-D7 的唯一决策是：选择哪个库作为生产实现。

### Current State

绿地项目——无现有存档代码，无迁移负担。F5 GDD 在 ADR 完成前已定案 `IFileStorage` 接口合约（中间件无关设计使两者可并行进行），待本 ADR Accepted 后即可开始 `ES3Storage` 实现。

### Constraints

- **Unity 6.3 LTS 兼容性**：所选库须与 Unity 6000.3.19f1 无冲突
- **纯数据类型约束**（F4 GDD Rule）：DTO 不含 `Dictionary<>`（Unity 不原生序列化），不含 Unity Object 引用（`ScriptableObject`、`MonoBehaviour` 等）
- **`[NonSerialized]` 隔离**：`PlayerInfoSO.latencyCalibrationOffsetMs` 为 `[NonSerialized]` 运行时缓存——序列化库不得将其写入 DTO 往返数据
- **主线程同步**：`SaveService` 仅提供同步 API，库须支持主线程阻塞式读写
- **Asset Store 约束**：EasySave 3 已列入项目 `技术首选项` 允许清单（`technical-preferences.md`），无需额外审批
- **ES3Cloud 禁用**：F5 GDD 明确本项目不启用云备份功能

### Requirements

- 序列化/反序列化所有 F5 DTO 数据类型（含 `[Serializable]` struct 数组）
- 支持文件级版本号检测（供 F5 Rule 8 的迁移逻辑使用）
- 支持文件不存在时返回默认值（`Load<T>(path, key, defaultValue)`）
- 支持原子性文件重命名（`.es3` → `.es3.corrupt` 损坏备份，F5 Edge Case Rule）
- 不序列化 `[NonSerialized]` 字段
- 主线程同步调用延迟 < 5ms（单文件读写，桌面平台；移动端 < 16ms）
- 不依赖网络（离线运行）

---

## Decision

采用 **EasySave 3（Moodkie, Asset Store）** 作为 `IFileStorage` 接口的生产实现。EasySave 3 的静态 API（`ES3.Save<T>()`、`ES3.Load<T>()`）通过 `ES3Storage` 封装类访问，`SaveService` 内部不出现任何 `ES3.*` 调用，保持中间件隔离。

**禁止**：直接在 `SaveService` 之外调用 `ES3.Save()` / `ES3.Load()`——所有下游系统（FE3、FE5、FE7、PL4）须通过 `SaveService` 公开接口读写数据，不直接接触 EasySave 3 API。

**禁止**：启用 `ES3Cloud`（云备份）——本项目明确不使用此功能。

**禁止**：序列化含 Unity Object 引用的对象——DTO 必须是纯数据结构（F4 GDD 约束，F5 Rule 6）。

**存储格式**：EasySave 3 默认使用 AES-128 加密 + 二进制序列化（`ES3Settings.EncryptionType.AES`），提供基本的存档防篡改保护（非安全级加密，仅防止意外损坏）。MVP 阶段使用默认加密模式。

### Architecture

```
[下游系统：FE3 / FE5 / FE7 / PL4]
   │ SaveService.SaveLatencyCalibration(dto) / LoadSlotProgress(slot) / ...
   ▼
[SaveService : MonoBehaviour（DontDestroyOnLoad）]
   │ — 状态机：Uninitialized → Idle → Saving / Loading → Error
   │ — 主线程 ID 校验（Rule 13）
   │ — Domain Reload 重置（Rule 12）
   │ InjectStorage(IFileStorage)
   ▼
[IFileStorage]  ←─── 接口边界（中间件隔离点）
   │
   ├──► [ES3Storage : IFileStorage]（生产路径）
   │       ES3.Save<T>(key, value, path, ES3Settings)
   │       ES3.Load<T>(key, defaultValue, path, ES3Settings)
   │       ES3.FileExists(path) / ES3.DeleteFile(path) / ES3.RenameFile(src, dst)
   │         ↓
   │       [EasySave 3 核心层（Moodkie）]
   │         ↓ AES-128 加密 + 二进制序列化
   │       [Application.persistentDataPath/saves/]
   │         ├── slot_0.es3 / slot_1.es3 / slot_2.es3
   │         ├── meta_0.es3 / meta_1.es3 / meta_2.es3
   │         ├── global/settings.es3
   │         ├── global/calibration.es3
   │         └── global/accessibility.es3
   │
   └──► [MockFileStorage : IFileStorage]（测试路径）
           内存 Dictionary<string, object>；无磁盘 I/O
           SaveCallCount / LoadCallCount 追踪属性
```

### Key Interfaces

```csharp
// IFileStorage — F5 GDD Rule 11 定义的中间件抽象接口
// ADR-D7 不修改此接口；仅授权 ES3Storage 实现
public interface IFileStorage
{
    void Save<T>(string path, string key, T value);
    T    Load<T>(string path, string key, T defaultValue);
    bool KeyExists(string path, string key);
    bool FileExists(string path);
    void DeleteFile(string path);      // 文件不存在时静默忽略（幂等）
    void RenameFile(string srcPath, string dstPath);  // dstPath 存在时静默覆盖
}

// ES3Storage — EasySave 3 的生产实现（本 ADR 授权创建的唯一实现）
// 文件路径：调用方传入相对路径，ES3Storage 前缀 Application.persistentDataPath
public class ES3Storage : IFileStorage
{
    // ES3Settings 在构造器中初始化（AES-128，默认设置）
    // 版本号由 SaveService 写入 DTO 内部字段，不依赖 ES3 的版本机制
    public void Save<T>(string path, string key, T value)
        => ES3.Save(key, value, path, _settings);

    public T Load<T>(string path, string key, T defaultValue)
        => ES3.Load<T>(key, path, defaultValue, _settings);
        // M0 Spike fix (2026-07-16): ES3.cs:501 signature is
        // Load<T>(string key, string filePath, T defaultValue, ES3Settings settings)

    public bool KeyExists(string path, string key)
        => ES3.KeyExists(key, path, _settings);

    public bool FileExists(string path)
        => ES3.FileExists(path, _settings);

    public void DeleteFile(string path)
    {
        if (ES3.FileExists(path, _settings))
            ES3.DeleteFile(path, _settings);
        // 文件不存在：静默忽略（幂等）
    }

    public void RenameFile(string srcPath, string dstPath)
    {
        // M0 Spike fix (2026-07-16): ES3.RenameFile exists natively (ES3.cs:1142)
        ES3.RenameFile(new ES3Settings(srcPath, _settings),
                       new ES3Settings(dstPath, _settings));
    }

    private readonly ES3Settings _settings = new ES3Settings
    {
        encryptionType = ES3.EncryptionType.AES,
        // password 通过 ProjectSettings 或 ScriptableObject 配置
        // MVP 阶段使用默认密码（非安全用途，仅防意外损坏）
    };
}
```

### Implementation Guidelines

1. **`ES3Storage` 放置路径**：`Assets/Scripts/Core/Persistence/ES3Storage.cs`
2. **`SaveService` 放置路径**：`Assets/Scripts/Core/Persistence/SaveService.cs`
3. **IFileStorage 放置路径**：`Assets/Scripts/Core/Persistence/IFileStorage.cs`
4. **禁止在 `ES3Storage` 之外的任何运行时代码引用 `ES3.*`**——Odin Validator 可配置 namespace 禁用规则强制此约束
5. **`RenameFile`**：~~EasySave 3 无原生重命名 API~~ M0 Spike (2026-07-16) 发现 `ES3.RenameFile()` 原生存在（ES3.cs:1142），已改用原生 API
6. **版本迁移**：版本号写入 DTO 内部字段（`save_version: int`），由 `SaveService` 的 `MigrateIfNeeded<T>()` 方法处理；不使用 EasySave 3 的版本机制（与 `IFileStorage` 接口解耦）
7. **ES3Cloud 禁用**：`ES3Settings.location` 保持默认（`File`），不设置 `ES3CloudSettings`

---

## Alternatives Considered

### Alternative 1: System.Text.Json（.NET 内置）

- **Description**: Unity 6.3 基于 .NET 8，`System.Text.Json` 为标准库，无额外依赖；通过自定义 `JsonConverter<T>` 处理不支持的类型；文件以明文 JSON 写入 `Application.persistentDataPath`
- **Pros**: 零 Asset Store 依赖；JSON 可人读，便于调试；.NET 标准库长期稳定；可配合 `System.IO.File.Move()` 实现真正原子性重命名
- **Cons**: 无加密（须自行实现 AES 封装层）；`[Serializable]` struct 的序列化须显式注册 `[JsonSerializable]` source generator 或手写 converter；自定义 converter 代码量约 200-300 行（对比 EasySave 3 零配置）；F5 GDD 定义的文件路径结构须手动管理（EasySave 3 自动处理目录创建）
- **Estimated Effort**: 中等——`IFileStorage` 实现约 150 行，AES 封装约 50 行，测试约 100 行；合计 ~300 行额外基础设施代码
- **Rejection Reason**: 在绿地项目早期阶段，投入 ~300 行序列化基础设施代码是不必要的成本。EasySave 3 已在技术首选项中预审批，`IFileStorage` 接口保证了将来可以无缝替换。

### Alternative 2: Newtonsoft.Json（Json.NET）

- **Description**: Unity 专属 UPM 包（`com.unity.nuget.newtonsoft-json`）；业界最广泛使用的 .NET JSON 库；支持 `[JsonProperty]`、`[JsonIgnore]`（映射 `[NonSerialized]`）等 Attribute；功能与 EasySave 3 部分重叠但更灵活
- **Pros**: 成熟稳定；`[JsonIgnore]` 可直接替代 `[NonSerialized]` 防止运行时缓存字段序列化；比 `System.Text.Json` 更易配置；UPM 包管理规范
- **Cons**: 仍须自行实现 AES 加密层；仍须手动管理文件目录结构；相比 `System.Text.Json` 在 AOT（IL2CPP）环境下有额外配置成本（须生成 `link.xml` 防止代码剥离）；与 EasySave 3 相比仍需更多基础设施代码
- **Estimated Effort**: 中等，约 250 行额外代码
- **Rejection Reason**: 与 Alternative 1 同样的基础设施代码成本；EasySave 3 已预审批，无额外采购阻力。

---

## Consequences

### Positive

- **零基础设施代码**：`ES3Storage` 实现约 60 行，无需自定义加密层、目录管理、错误处理基础设施——EasySave 3 全部内置
- **`[NonSerialized]` 隔离自动保障**：EasySave 3 遵守 .NET 序列化约定，不序列化 `[NonSerialized]` 字段；`PlayerInfoSO.latencyCalibrationOffsetMs` 的隔离约束由库行为自动保障，无需额外代码
- **`List<>` 与 `[Serializable]` struct 支持开箱即用**：`PlayerProgressDTO.metaUpgradeLevels`（`List<MetaUpgradeEntry>`）无需 converter
- **`IFileStorage` 接口保留替换自由**：如将来 EasySave 3 出现兼容性问题（Unity 版本升级、平台限制），仅替换 `ES3Storage` 实现，`SaveService` 和下游系统零改动
- **AES-128 加密开箱即用**：防止存档被意外编辑（非安全用途；加密密钥明文存在包体中）

### Negative

- **Asset Store 依赖**：EasySave 3 非 UPM 包，须手动跟踪版本（`Assets/Plugins/EasySave3/`）；Unity 版本升级时须重新验证兼容性
- ~~**`RenameFile` 非原子性**~~：M0 Spike (2026-07-16) 发现 `ES3.RenameFile()` 原生存在（ES3.cs:1142），已改用原生 API，消除三步 I/O 非原子性风险
- **AES 密钥明文**：MVP 阶段使用硬编码或 `ProjectSettings` 中的密钥，不提供真正的存档安全性（对成本分析：本游戏存档数据无安全价值，加密仅防意外损坏；可接受）
- **ES3Cloud 功能浪费**：购买的功能（云备份）在本项目中不使用
- **调试时文件不可读**：加密二进制格式无法直接查看存档内容；须在 `MockFileStorage` 测试环境中调试，或临时切换 `ES3Settings.encryptionType = None`（不提交）

### Neutral

- EasySave 3 的 `ES3Settings` 为每次调用时传入；`ES3Storage` 在构造器初始化全局设置（AES + 默认路径前缀）
- EasySave 3 的 key-value 文件模型（同一文件内多 key）与 F5 GDD 的「每个 DTO 独立文件」设计兼容；每个 `.es3` 文件内只存一个 key（DTO 整体）

---

## Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| EasySave 3 v3.x 与 Unity 6.3 LTS 不兼容（编译错误） | LOW — Moodkie 维护 Unity 6 兼容性 | HIGH — 阻塞 F5 实现 | M0 Spike：首先验证 ES3 import + `ES3.Save<int>()` 简单测试通过；备选：切换 Alternative 1 |
| `List<MetaUpgradeEntry>`（struct 数组）序列化失真 | LOW — EasySave 3 支持此类型 | HIGH — 玩家进度丢失 | 编写往返测试（AC-F5-01 扩展）：`List<MetaUpgradeEntry>` 保存再加载，逐字段比对 |
| `[NonSerialized]` 字段被意外序列化 | LOW — EasySave 3 遵守 .NET 约定 | MEDIUM — `latencyCalibrationOffsetMs` 写入 DTO，破坏权威来源分离 | AC-F5-XX：专项单元测试：存储 `PlayerInfoSO`（含 `[NonSerialized]` 字段）→ 加载 → 验证 `[NonSerialized]` 字段为默认值 |
| `RenameFile` 三步 I/O 中断导致双份 `.corrupt` 文件 | VERY LOW — 仅在 `.corrupt` 备份时调用 | LOW — 磁盘残留，不影响正常游戏 | `SaveService.Initialize()` 启动时扫描并清理同名多份 `.corrupt` 文件（保留最新） |
| 加密密钥在包体中明文暴露 | MEDIUM — 逆向工程可提取 | LOW — 存档无安全价值；仅影响防改分效果 | 接受此风险（MVP 阶段）；Post-MVP 可切换 `IFileStorage` 为使用 OS Keychain 的实现 |

---

## Performance Implications

| Metric | Expected | Budget | Notes |
|--------|----------|--------|-------|
| CPU（Save 单次调用） | < 2ms（PC）/ < 10ms（Mobile） | < 5ms（PC）/ < 16ms（Mobile） | `SaveService` 在局结算时调用，非帧关键路径 |
| CPU（Load — Initialize） | < 50ms（PC）/ < 100ms（Mobile） | < 200ms | 游戏启动时执行（Splash Screen 期间可接受） |
| 内存（运行时） | ~0（EasySave 3 无常驻内存缓存） | N/A | ES3 读写后不持有 DTO 引用 |
| 磁盘（总存档大小） | < 10KB（全部 DTO） | N/A | DTO 数据量极小；加密后约 2-3× 膨胀，仍 < 30KB |
| 网络 | 无 | N/A | ES3Cloud 禁用 |

---

## Migration Plan

绿地项目——无现有存档代码迁移。

**实现序列**（F5 实现冲刺时按此顺序）：

1. 下载 EasySave 3 v3.x 并导入 `Assets/Plugins/EasySave3/`；记录确切版本号至 `docs/engine-reference/unity/plugins/easysave-3.md`
2. 验证：`ES3.Save<int>("test_key", 42, "test.es3")` + `ES3.Load<int>("test_key", 0, "test.es3")` 返回 `42`（无编译错误）
3. 创建 `IFileStorage.cs`（接口）、`ES3Storage.cs`（实现）、`MockFileStorage.cs`（测试实现）
4. 创建 `SaveService.cs` —— Awake 注入 `ES3Storage`；全状态机实现
5. 写 EditMode 单元测试（`MockFileStorage`）：验证所有 DTO 往返、版本迁移、损坏恢复
6. 集成测试（`ES3Storage`）：PlayMode 中验证文件实际写入磁盘、重启后可读取

**回滚计划**：若 EasySave 3 验证失败（步骤 2 失败），直接替换 `ES3Storage` 为 `JsonStorage`（`System.Text.Json` 实现）——`SaveService`、`IFileStorage`、`MockFileStorage` 均不变，接口合约完全保留。

---

## Validation Criteria

- [ ] **编译通过**：EasySave 3 导入后 Unity 6.3 LTS 零编译错误，零 `MissingMethodException`
- [ ] **基础往返**：`ES3Storage.Save<T>` + `Load<T>` 对所有 F5 DTO 数据类型（`int`, `float`, `bool`, `string`, `MetaUpgradeEntry[]`）正确往返（误差 ≤ float 精度）
- [ ] **`[NonSerialized]` 隔离**：存储含 `[NonSerialized]` 字段的对象后重新加载，`[NonSerialized]` 字段值为 C# 默认值（`0` / `false`）
- [ ] **文件隔离**：`slot_0.es3` 的写入不影响 `global/settings.es3` 的内容（EditMode 测试）
- [ ] **损坏恢复**：`Load<T>()` 在文件内容为随机字节时抛出 `FormatException`；`SaveService` 捕获后进入 `Error` 状态、触发 `OnSaveError` 事件（PlayMode 测试）
- [ ] **主线程性能**：单次 `SaveSlotProgress()` 调用在 PC 上 < 5ms（Profiler 测量，Release 模式）

---

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Satisfies It |
|-------------|--------|-------------|--------------------------|
| `design/gdd/save-persistence.md` (F5) | F5 存档系统 | Rule 1：`SaveService` 是唯一存档网关，所有系统通过其接口读写数据 | `ES3Storage` 封装 EasySave 3，`SaveService` 调用 `IFileStorage` 而非直接调用 `ES3.*` |
| `design/gdd/save-persistence.md` (F5) | F5 存档系统 | Rule 11：`IFileStorage` 接口——ADR-D7 替换序列化方案时仅需替换 `IFileStorage` 实现，`SaveService` 不变 | 本 ADR 授权 `ES3Storage : IFileStorage`；接口边界保证未来可替换性 |
| `design/gdd/save-persistence.md` (F5) | F5 存档系统 | Rule 8：存档版本号——加载时检测版本，低版本数据执行迁移 | 版本号写入 DTO 内部字段（`save_version: int`）；EasySave 3 正确序列化 int 字段；`SaveService.MigrateIfNeeded<T>()` 负责迁移逻辑，与 ES3 API 无耦合 |
| `design/gdd/save-persistence.md` (F5) | F5 存档系统 | Rule 11 + Edge Case：损坏文件重命名为 `.es3.corrupt` | `ES3Storage.RenameFile()` 实现此约束（三步 I/O；风险见 Risks 节） |
| `design/gdd/so-data-layer.md` (F4) | F4 SO 数据层 | Rule：禁止序列化 Unity Object 引用；`[NonSerialized]` 字段（`latencyCalibrationOffsetMs`）不参与 DTO 往返 | EasySave 3 遵守 .NET 序列化约定，不序列化 `[NonSerialized]` 字段；验证标准见 Validation Criteria |

---

## Related

- `design/gdd/save-persistence.md` — F5 GDD；`IFileStorage` 接口定义来源；序列化中间件的下游消费方
- `design/gdd/so-data-layer.md` — F4 GDD；DTO 数据类型约束来源（`[NonSerialized]`、禁止 Unity Object 引用）
- `ADR-D6` — 音频中间件；同类 Asset Store 依赖决策，可参考其验证流程
- `docs/engine-reference/unity/plugins/easysave-3.md` — EasySave 3 引擎参考文档（需在 M0 Spike 导入后补充版本信息）
