# F5 存档/持久化系统 (Save/Persistence System)

> **Status**: Approved
> **Author**: Jadyn Wu + Claude Code Agents
> **Last Updated**: 2026-07-14
> **Implements Pillar**: 支柱四：构筑驱动复盘 (Build Depth Over Stat Inflation)
> **System ID**: F5
> **Layer**: Foundation | **Milestone**: MVP | **Design Order**: 4

---

## Overview

F5 存档/持久化系统是《玉兔：破阵》所有跨局数据的读写基础设施。它负责将运行时 `PlayerInfoSO` 中的玩家进度（功勋、武器解锁）和全局配置数据（音量/键位设置、延迟校准偏移、无障碍偏好）序列化到本地磁盘，并在游戏启动时完成反向加载。

存档触发采用混合模式：局结算事件（死亡 / 通关）自动写入玩家进度，设置类数据（音量、键位映射等）在修改后即时持久化，延迟校准偏移量在校准完成后一次性写入。本系统支持多存档槽位，每个槽位维护独立的玩家进度状态；全局设置（音量、键位、延迟校准）跨槽位共享，绑定于设备而非玩家档案。

F5 通过 EasySave 3（ES3）实现序列化，采用 DTO 模式：运行时 `ScriptableObject` 在存档前先提取为纯数据结构，由 ES3 处理磁盘读写，加载时再反向填充回运行时对象。所有下游系统（FE3 延迟校准、FE7 游戏设置、FE5 局外成长、PL4 无障碍设置）均通过 F5 暴露的统一服务接口存取各自数据域，不直接调用 ES3 API，保持序列化实现对其他模块的透明性。

> *注：序列化中间件的正式选型见 **ADR-D7（Accepted — EasySave 3）**（`docs/architecture/adr-d7-save-persistence-middleware.md`）；F5 接口设计中间件无关，ADR 结论不影响下游接口合约。*

---

## Player Fantasy

F5 对玩家完全不可见。它的成功体现为一种**缺席型幻想**：玩家关掉游戏时，从未需要担心「我的功勋会不会消失」「上次校准的延迟偏移还在不在」。进度的持久感不是玩家主动感受到的特性，而是一种前提性信任——当这份信任存在时玩家不会注意到它，当它被破坏时（存档损坏、进度丢失）将成为游戏体验中最严重的挫折之一。

F5 服务于支柱四（构筑驱动复盘）的底层保障：玩家愿意花时间探索不同武器和心法 build，前提是他们知道每次通关积累的功勋和解锁进度是真实的积累，不会在下次启动游戏时归零。这一层信任不由 F5 创造——而由 F5 的零故障运行来维护。

---

## Detailed Design

### Core Rules

1. **统一存档网关**：`SaveService` 是全局唯一的持久化接口（`MonoBehaviour` 单例，`DontDestroyOnLoad`），所有系统通过 `SaveService.Instance` 读写数据，禁止直接调用 ES3 API 或操作文件系统。

2. **数据域分离**：存档数据分为两类：
   - **槽位数据（SlotData）**：每个存档槽位独立，包含 `PlayerProgressDTO`（功勋、武器解锁、局数、游玩时长）和 `SlotMetaDTO`（最近游玩时间戳、功勋快照、游玩时长，用于主菜单展示）
   - **全局数据（GlobalData）**：设备级共享，不与槽位绑定，三个 DTO **各存独立文件**，各自携带独立 `save_version`：`GameSettingsDTO`（`global/settings.es3`）、`LatencyCalibrationDTO`（`global/calibration.es3`）、`AccessibilityDTO`（`global/accessibility.es3`）

3. **最大槽位数**：3 个（`slot 0, 1, 2`），由常量 `SaveService.MaxSlotCount = 3` 定义，不可运行时修改。多槽位的设计意图是**双场景兼顾**：主要服务家庭/室友共用设备场景（同一台 PC 上多人各自维护独立局外成长线），次要支持同一玩家的多成长线探索（如"纯节奏精度挑战号"——零局外升级状态从头体验）。

4. **激活槽位（Active Slot）**：游戏中有且只有一个激活槽位，在主菜单槽位选择界面设定。`GetActiveSlotIndex()` 返回当前槽位 index；`-1` 表示未选择（主菜单状态）。

5. **混合存档触发规则**：

   | 触发事件 | 写入目标 | 触发方 |
   |---------|---------|-------|
   | 局结算（死亡 / 通关） | 激活槽位 SlotData + SlotMetaDTO | FE5（局外成长系统） |
   | 任何设置更改 | GlobalData → GameSettingsDTO | FE7（游戏设置系统） |
   | 延迟校准完成 | GlobalData → LatencyCalibrationDTO（`global/calibration.es3`）| FE3（延迟校准系统，写入完整 DTO 含 `isCalibrated=true`） |
   | 无障碍设置更改 | GlobalData → AccessibilityDTO | PL4（无障碍设置） |
   | 游戏启动 | 读取所有 GlobalData + 激活槽位 SlotData | `SaveService.Initialize()` |

6. **DTO 模式**：`SaveService` 接受纯数据 DTO（C# `struct` 或 `[System.Serializable] class`），不持有任何 `ScriptableObject` 引用。DTO 提取（SO → DTO）和填充（DTO → SO）由各下游系统负责。

7. **主线程约束**：所有 `ES3.Save()` / `ES3.Load()` 调用须在主线程执行。`SaveService` 提供同步 API；如需异步，由调用方封装协程，`SaveService` 内部不启动任何异步操作。

8. **存档版本号**：每个存档文件内的 DTO 携带独立 `save_version: int`。加载时检测版本，低版本数据执行迁移规则，不可识别的高版本数据视为损坏处理。全局三个文件各自版本独立演化，互不影响。

9. **激活槽位管理**：`GetActiveSlotIndex()` 返回当前激活槽位 index（-1 = 未选择）；`SetActiveSlotIndex(int slotIndex)` 设置激活槽位，合法值为 `[0, MaxSlotCount-1]` 或 `-1`（退出槽位选择状态）。传入越界值（如 `3` 或 `-2`）须抛出 `ArgumentOutOfRangeException`。主菜单（P7）在玩家选择槽位后调用此方法。

10. **槽位删除**：`DeleteSlot(int slotIndex)` 删除指定槽位的全部存档数据（物理删除 `slot_n.es3` 和 `meta_n.es3`），并将内存中对应槽位的 `SlotMetaDTO.slotState` 重置为 `SlotState.Empty`。若被删除槽位正是当前激活槽位，自动调用 `SetActiveSlotIndex(-1)` 退出激活状态。删除操作不可撤销，调用方（P7）须在 UI 层提供确认弹窗。

11. **IFileStorage 接口（序列化中间件抽象）**：`SaveService` 通过 `InjectStorage(IFileStorage storage)` 方法注入依赖，不直接调用 `ES3.Save()` / `ES3.Load()` 等静态 API。`Awake()` 内部自动调用 `InjectStorage(new ES3Storage())`（生产路径）；`InjectStorage()` 必须在 `Initialize()` 读取前调用；若在 `Initialize()` 完成之后调用，须抛出 `InvalidOperationException`（注入时序错误）。生产实现为 `ES3Storage : IFileStorage`；测试使用 `MockFileStorage : IFileStorage`（内存 `Dictionary` 实现）。此接口是"中间件无关性"承诺的实现载体——ADR-D7 替换序列化方案时，仅需替换 `IFileStorage` 实现，`SaveService` 不变。

   **测试路径**：Unity Test Runner 在 `[SetUp]`（EditMode）或 `[UnitySetUp]`（PlayMode）中，对已 Awake 的 `SaveService.Instance` 调用 `InjectStorage(new MockFileStorage())` 替换默认实现，**然后**显式调用 `Initialize()`。由于 Rule 14 规定 `Awake()` 不自动调用 `Initialize()`，测试可以在 Awake 完成后、Initialize 之前安全注入 Mock。`MockFileStorage` 须提供以下测试辅助属性（不属于 `IFileStorage` 接口）：`int SaveCallCount`、`int LoadCallCount`（调用计数追踪）。`MockFileStorage.Load<T>()` 在文件不存在时返回 `defaultValue`；在模拟损坏时抛出 `FormatException`。

   ```csharp
   public interface IFileStorage
   {
       void Save<T>(string path, string key, T value);
       T    Load<T>(string path, string key, T defaultValue);
       bool KeyExists(string path, string key);
       bool FileExists(string path);
       void DeleteFile(string path);
       void RenameFile(string srcPath, string dstPath);
   }
   ```

   `RenameFile` 语义：若 `dstPath` 已存在，静默覆盖（最新备份覆盖旧备份）。`DeleteFile` 语义：若文件不存在，静默忽略（幂等操作）。

12. **Domain Reload 静态重置**：`SaveService` 必须实现 `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` 静态重置方法，将 `_instance` 及其他静态字段（含 `_mainThreadId`）清零。Unity 6 默认推荐关闭 Domain Reload（Enter Play Mode 优化），不实现此方法时第二次进入 Play Mode 将访问已销毁的 MonoBehaviour 引用，导致 `SaveService` 永远处于 `Uninitialized` 状态。

13. **主线程 ID 捕获**：`SaveService.Awake()` 中捕获 `Thread.CurrentThread.ManagedThreadId` 并存为私有静态字段 `_mainThreadId`。所有公开方法入口比对当前线程 ID；非主线程调用通过 `ConcurrentQueue` 缓存错误描述（由主线程 `Update()` flush 后以 `Debug.LogError` 输出），不执行任何 `IFileStorage` 操作，不抛出异常，`SaveService` 状态保持不变。禁止使用 `Thread.CurrentThread.IsBackground` 或 `Thread.CurrentThread.Name` 作为主线程判断依据（Unity 中不可靠）。

14. **Awake() 与 Initialize() 分离**：`SaveService.Awake()` 仅完成三项工作：(a) 单例注册（`_instance = this` + `DontDestroyOnLoad`），(b) 主线程 ID 捕获（Rule 13），(c) 注入默认 `IFileStorage`（`InjectStorage(new ES3Storage())`）。`Awake()` **不**调用 `Initialize()`。`Initialize()` 由场景 Bootstrap 脚本或首个需要 F5 数据的下游系统在 `Start()` 中显式调用（推荐方式：场景中放置一个 `SaveServiceBootstrap` MonoBehaviour，其 `Start()` 调用 `SaveService.Instance.Initialize()`）。下游系统（如 F2 BeatClock）须在 `Initialize()` 完成后调用 `LoadX()`，不得在 `Awake()` 中调用 F5 API。Unity 保证所有 `Awake()` 在第一个 `Start()` 前完成，Bootstrap 的 `Start()` 先于其他系统的 `Start()`（通过场景加载顺序或 `DefaultExecutionOrder` attribute 保证）。此分离设计的目的是让测试可以在 `[SetUp]` 中替换 `IFileStorage` 后再调用 `Initialize()`（见 Rule 11 测试路径）。

---

### States and Transitions

| 状态 | 描述 |
|------|------|
| `Uninitialized` | 初始状态；`Initialize()` 调用前 |
| `Idle` | 就绪，可接受读写请求 |
| `Saving` | 写入进行中（同步，通常 < 5ms） |
| `Loading` | 读取进行中（游戏启动时） |
| `Error` | I/O 错误；以默认数据继续运行 |

| 来源状态 | 触发条件 | 目标状态 |
|---------|---------|---------|
| `Uninitialized` | `Initialize()` 调用 | `Loading` |
| `Loading` | 所有文件读取成功 | `Idle` |
| `Loading` | **单槽位文件损坏（GlobalData 正常）** | `Idle`（损坏槽位 `slotState=Corrupted`，触发 `OnSaveError`，但**不进入 Error 状态**） |
| `Loading` | **GlobalData 文件损坏 / 目录创建失败** | `Error` |
| `Loading` | **任何文件 `save_version` > 对应 DTO 的 `CurrentVersion`** | `Error`（触发 `OnSaveError(VersionTooHigh)`，文件不覆盖不删除） |
| `Idle` | 任意 Write 方法调用 | `Saving` |
| `Saving` | 写入完成 | `Idle` |
| `Saving` | 磁盘满 / 权限错误 | `Error` |
| `Error`（WriteFailed） | `AcknowledgeError()` 调用 | `Idle`（内存缓存**保留**上次成功写入的值；写入失败不意味数据损坏，缓存仍可信） |
| `Error`（Corrupted / DirectoryFailed / VersionTooHigh） | `AcknowledgeError()` 调用 | `Idle`（所有内存缓存**重置为默认 DTO 值**，等同首次启动状态；后续 `LoadX()` 返回默认值） |
| `Error` | 任意 Write 方法调用 | — （抛出 `InvalidOperationException`，状态保持 `Error`） |

> **两级错误模型说明**：单槽位损坏属于"可恢复的局部故障"，游戏保持 `Idle` 状态，未损坏的槽位和全局数据正常可用，P7 在对应槽位显示 `Corrupted` 状态标识。GlobalData 损坏、目录失败或高版本存档属于"系统级故障"，进入 `Error` 状态，所有写入被阻断。**`AcknowledgeError()` 的后置行为因错误来源不同**：WriteFailed（I/O 写入失败）时内存缓存**保留**（写入失败不等于数据损坏）；Corrupted / DirectoryFailed / VersionTooHigh 时内存缓存**重置为默认值**（磁盘数据不可信，须从安全基线重启）。

---

### Interactions with Other Systems

| 方向 | 系统 | 数据 | 接口 | 触发时机 |
|------|------|------|------|---------|
| FE5 → F5 | 局外成长系统 | `PlayerProgressDTO`（write） | `SaveSlotProgress(int slotIndex, PlayerProgressDTO)` | 局结算 |
| F5 → FE5 | 局外成长系统 | `PlayerProgressDTO`（read） | `LoadSlotProgress(int slotIndex): PlayerProgressDTO` | 游戏启动 / 槽位激活 |
| FE3 → F5 | 延迟校准系统 | `LatencyCalibrationDTO`（write） | `SaveLatencyCalibration(LatencyCalibrationDTO)` | 校准完成（FE3 构建完整 DTO，含 `isCalibrated=true`） |
| F5 → F2 | 音频/拍点时钟 | `LatencyCalibrationDTO`（read） | `LoadLatencyCalibration(): LatencyCalibrationDTO` | **F2（BeatClock）在 `Start()` 中主动调用**，读取后自行赋值 `LatencyOffsetMs`（若 `isCalibrated=true`） |
| P7 → F5 | 主菜单 | `slotIndex: int` | `SetActiveSlotIndex(int)` | 玩家在主菜单选择存档槽位 |
| P7 → F5 | 主菜单 | `slotIndex: int` | `DeleteSlot(int)` | 玩家确认删除存档槽位后调用 |
| FE7 → F5 | 游戏设置系统 | `GameSettingsDTO`（write） | `SaveSettings(GameSettingsDTO)` | 设置更改 |
| F5 → FE7 | 游戏设置系统 | `GameSettingsDTO`（read） | `LoadSettings(): GameSettingsDTO` | 游戏启动 |
| PL4 → F5 | 无障碍设置 | `AccessibilityDTO`（write） | `SaveAccessibility(AccessibilityDTO)` | 设置更改 |
| F5 → PL4 | 无障碍设置 | `AccessibilityDTO`（read） | `LoadAccessibility(): AccessibilityDTO` | 游戏启动 |
| P7 ← F5 | 主菜单（槽位展示） | `SlotMetaDTO[3]`（read） | `GetAllSlotMeta(): SlotMetaDTO[]` | 主菜单渲染槽位列表 |

---

### OnSaveError 事件契约

`SaveService` 暴露以下事件，供 P7 / HUD 订阅并展示错误 UI：

```csharp
public event Action<SaveErrorData> OnSaveError;

public enum SaveErrorType
{
    Corrupted,        // 文件损坏（IFileStorage 解析失败）
    WriteFailed,      // I/O 写入失败（磁盘满 / 权限拒绝）
    VersionTooHigh,   // 存档版本高于当前 CurrentSaveVersion
    DirectoryFailed,  // saves/ 目录创建失败
}

public readonly struct SaveErrorData
{
    public SaveErrorType ErrorType        { get; }
    public int           SlotIndex        { get; } // -1 = 全局数据
    public int           DetectedVersion  { get; } // VersionTooHigh 时携带检测到的版本号
    public bool          IsRetryable      { get; } // WriteFailed = true；其他 = false
}
```

**P7 / HUD 委托规格（由 F5 强制要求）：**

- **`WriteFailed`**：P7 / HUD **必须**展示持久性非模态通知（不暂停游戏、不中断 Boss 战），明确告知玩家「当前进度将不会被保存」，直到玩家手动确认（`AcknowledgeError()` 调用后）或下次写入成功（`IsRetryable=true` 的恢复路径）。P7 **不得**将此降级为自动消失的 Toast。
- **`VersionTooHigh`**：P7 **必须**在提示中包含 `DetectedVersion` 值，例如：「此存档由版本 [DetectedVersion] 的游戏创建，当前版本支持至版本 [CurrentSaveVersion]，请更新游戏」。
- **`Corrupted` / `DirectoryFailed`**：P7 展示通用错误对话框，告知玩家将以默认数据继续。

---

## Formulas

> F5 的核心计算是**数据序列化契约**——精确定义各 DTO 的字段类型与值域，作为所有下游系统的存档格式规格。以下 DTO 定义是跨系统的不可变接口契约；字段变更须更新 `CurrentSaveVersion` 并在迁移规则中处理。

---

### F5-D1：PlayerProgressDTO（槽位进度数据）

`PlayerProgressDTO` 是每个存档槽位的主数据，由 FE5（局外成长系统）在局结算后调用 `SaveSlotProgress()` 写入。

| 字段 | 类型 | 范围 | 描述 |
|------|------|------|------|
| `merit` | `int` | [0, +∞) | 累计功勋量（永久货币）；始终 ≥ 0 |
| `unlockedWeaponIds` | `string[]` | 长度 [0, N]（N = 游戏全武器数，由 FE5 GDD 约束；MVP 阶段 N=3） | 已解锁武器 ID 列表；空数组 = 仅初始武器（苗刀）可用；F5 只存取原始列表，不对长度做断言 |
| `metaUpgradeLevels` | `MetaUpgradeEntry[]` | 长度 [0, ∞) | 局外升级项数组；`MetaUpgradeEntry` 为 `[Serializable] struct {id: string, level: int}`（定义见 F4-S7）；空数组 = 无局外升级；与 F4 PlayerInfoSO 同名字段一一对应 |
| `runCount` | `int` | [0, +∞) | 总游戏局数（死亡 + 通关均计入） |
| `totalPlayTimeSeconds` | `double` | [0, +∞) | 本槽位累计游玩时长（秒）；使用 `double` 避免大值累加精度退化（`float` 在 194 天后每帧增量静默归零） |
| `save_version` | `int` | == `PlayerProgressDTO.CurrentVersion` | 写入时固定写入当前版本号（MVP = 1），加载时用于版本检测 |

**输出说明**：`PlayerProgressDTO` 是存档状态快照，不产生派生值。`merit` 和 `unlockedWeaponIds` 的计算逻辑属于 FE5 GDD，F5 只负责存取原始值。

---

### F5-D2：SlotMetaDTO（槽位展示元数据）

用于主菜单（P7）展示槽位列表；随 `SaveSlotProgress()` 同步更新，无需独立调用。

| 字段 | 类型 | 范围 | 描述 |
|------|------|------|------|
| `slotIndex` | `int` | [0, 2] | 槽位编号（读取后由 SaveService 根据文件名注入，不持久化到文件） |
| `slotState` | `SlotState` | `Empty / Active / Corrupted` | 槽位状态枚举（见下方 SlotState 定义）；替代原 `isEmpty: bool`，语义更精确 |
| `lastPlayedUtcMs` | `long` | Unix 毫秒时间戳 | 最近一次局结算时的 UTC 时间戳；`slotState = SlotState.Empty` 时为 0 |
| `meritSnapshot` | `int` | [0, +∞) | 最近存档时的功勋快照（仅用于 UI 展示） |
| `totalPlayTimeSeconds` | `double` | [0, +∞) | 与 `PlayerProgressDTO.totalPlayTimeSeconds` 同步；同为 `double` 类型 |
| `save_version` | `int` | == `SlotMetaDTO.CurrentVersion` | 版本校验字段（MVP = 1） |

#### SlotState 枚举定义

```csharp
public enum SlotState
{
    Empty,      // 本槽位从未写入，P7 显示"新建档案"按钮
    Active,     // 本槽位有有效存档数据，P7 显示进度信息
    Corrupted   // 本槽位存档文件存在但已损坏，P7 显示警告及删除并重新开始选项（F5 不提供恢复 API）
}
```

---

### F5-D3：LatencyCalibrationDTO（延迟校准数据）

全局数据，绑定设备，存储于 `global/calibration.es3`；由 FE3（延迟校准系统）在校准完成后调用 `SaveLatencyCalibration(LatencyCalibrationDTO)` 写入完整 DTO；游戏启动时由 **F2（BeatClock）在 `Start()` 中主动调用 `LoadLatencyCalibration()`** 获取并应用延迟偏移（`isCalibrated=true` 时赋值 `BeatClock.LatencyOffsetMs`）。

| 字段 | 类型 | 范围 | 描述 |
|------|------|------|------|
| `latencyOffsetMs` | `float` | [-200.0, 200.0] | 玩家延迟偏移（毫秒）；正值 = 声音延迟到达，符合 F2-F5 符号约定；默认 0.0 |
| `isCalibrated` | `bool` | — | `true` = 玩家已完成至少一次校准；`false` 时 F2 使用默认 0ms |
| `save_version` | `int` | == `LatencyCalibrationDTO.CurrentVersion` | 版本校验（MVP = 1） |

---

### F5-D4：GameSettingsDTO（游戏设置数据）

全局数据，存储于 `global/settings.es3`，由 FE7（游戏设置系统）写入；音量值在 FE7 写入前已应用音量曲线转换，F5 存取线性 [0, 1] 值。

| 字段 | 类型 | 范围 | 描述 |
|------|------|------|------|
| `masterVolume` | `float` | [0.0, 1.0] | 主音量；0 = 静音，1 = 最大 |
| `musicVolume` | `float` | [0.0, 1.0] | 音乐音量 |
| `sfxVolume` | `float` | [0.0, 1.0] | 音效音量 |
| `save_version` | `int` | == `GameSettingsDTO.CurrentVersion` | 写入时固定写入当前版本号（MVP = 1）；加载时用于版本检测 |

> *注：键位映射（keyBindings）字段推迟至 FE7 GDD 定案后补充；FE7 GDD 须扩展此 DTO 并递增 `save_version`。*

---

### F5-D6：AccessibilityDTO（无障碍设置数据）

全局数据，存储于 `global/accessibility.es3`，由 PL4（无障碍设置系统）写入和读取。

| 字段 | 类型 | 范围 | 描述 |
|------|------|------|------|
| `save_version` | `int` | == `AccessibilityDTO.CurrentVersion` | 版本校验字段（MVP = 1） |

> *注：AccessibilityDTO 的具体业务字段（颜色无障碍模式、UI 缩放比例等）由 PL4 GDD 定义；F5 负责存取原始值，不在本文档中规定字段范围。*

---

### F5-D5：文件路径与存档版本公式

```
全局设置路径    = {Application.persistentDataPath}/saves/global/settings.es3
全局校准路径    = {Application.persistentDataPath}/saves/global/calibration.es3
全局无障碍路径   = {Application.persistentDataPath}/saves/global/accessibility.es3
槽位数据路径    = {Application.persistentDataPath}/saves/slot_{n}.es3     (n ∈ {0, 1, 2})
槽位元数据路径   = {Application.persistentDataPath}/saves/meta_{n}.es3     (n ∈ {0, 1, 2})

存档目录名      = SaveService.SaveDirectory = "saves"（不可变常量，更改会使现有存档不可读）

当前存档版本号 = 每个 DTO 文件独立维护，MVP 均为 1
```

| 变量 | 类型 | 范围 | 描述 |
|------|------|------|------|
| `Application.persistentDataPath` | `string` | OS 决定 | Unity 跨平台持久化目录；**Windows**: `%USERPROFILE%/AppData/LocalLow/<CompanyName>/<ProductName>`（须与 `ProjectSettings.asset` 中 `companyName`/`productName` 精确匹配） |
| `n` | `int` | [0, 2] | 槽位编号 |
| `{DTO}.CurrentVersion`（每个 DTO 独立定义） | `const int` | 正整数，单调递增 | MVP 均为 1；各 DTO Schema 独立演化，变更时各自递增，互不影响 |

**版本迁移规则**：
- 加载文件版本 < 对应 DTO 的 `CurrentVersion`：执行对应迁移函数（`MigrateV{old}To{new}`），迁移后以新版本格式覆盖写入
- 加载文件版本 > 对应 DTO 的 `CurrentVersion`：版本冲突（此设备安装了更旧的游戏版本），报错提示玩家更新游戏；不覆盖文件
- 文件不存在 / 解析失败：以默认数据初始化（新存档状态）

---

## Edge Cases

- **如果存档文件不存在（首次启动或新槽位）**：`LoadSlotProgress()` 和 `LoadSettings()` 返回对应 DTO 的默认值（`merit=0`、`unlockedWeaponIds=[]`；`isCalibrated=false`、`latencyOffsetMs=0.0`），不视为错误状态，正常进入 `Idle`。

- **如果存档文件解析失败（数据损坏）**：`SaveService` 捕获 ES3 异常，转入 `Error` 状态，触发 `OnSaveError` 事件，UI 层展示「存档数据损坏，将以默认数据继续」提示；玩家确认后以默认数据进入游戏。**不自动删除损坏的存档文件**——重命名为 `.corrupt` 备份（如 `slot_0.es3.corrupt`），供开发者诊断和技术型玩家备份恢复。

- **如果写入失败（磁盘满 / 权限错误）**：捕获 I/O 异常，转入 `Error` 状态，触发 `OnSaveError` 事件；游戏**继续在内存中运行**（本局进度不丢失），但下次进度将无法持久化。日志记录详细错误信息。恢复：玩家通过 `AcknowledgeError()` 确认后回 `Idle`；如磁盘空间已恢复，下次存档会重新尝试。

- **如果在 `Saving` 状态期间再次调用 Write**：队列第二次写入请求，等 `Saving → Idle` 后立即执行。不抛出异常，不丢弃请求。（同步 API 下实际上不存在并发问题；此规则防止未来切换异步实现时的行为回归。）

- **如果加载到低版本存档（save_version < 对应 DTO 的 CurrentVersion）**：执行对应迁移函数 `MigrateV{old}To{new}`，迁移后以新版本格式覆盖写入。迁移函数须为纯函数（无副作用），迁移失败时视为损坏处理。

- **如果加载到高版本存档（save_version > 对应 DTO 的 CurrentVersion）**：记录 Error 日志，不加载数据，不覆盖文件；触发 `OnSaveError`（`ErrorType = VersionTooHigh`，`DetectedVersion` 携带检测到的版本号）；P7 须展示含版本号的引导提示（「此存档由版本 X 的游戏创建，当前版本支持至版本 Y，请更新游戏」）；游戏使用默认数据。

- **如果 `SaveService.Initialize()` 在 `GlobalData` 加载成功但某槽位文件损坏**：`GlobalData` 和其他完好槽位正常加载；损坏槽位的 `SlotMetaDTO.slotState` 标记为 `SlotState.Corrupted`（P7 显示警告而非"新建档案"按钮），日志记录 Warning，**不**阻塞游戏启动。

- **如果玩家在局内强制关闭游戏（未触发局结算）**：本局进度（当前局内获得但未结算的临时成长）不被持久化——这是 Roguelike 的标准设计；上一次局结算的存档完整保留。FE5 须明确「局内状态不存档」作为设计契约。

- **如果 `Application.persistentDataPath/saves/` 目录不存在**：`SaveService.Initialize()` 时自动创建目录；目录创建失败（权限拒绝）则转入 `Error` 状态。

- **如果 `SaveSlotProgress()` 写入中途失败（双文件非原子性）**：`SaveSlotProgress()` 顺序写入两个文件（`slot_n.es3` 和 `meta_n.es3`）。若第一个文件写入成功、第二个失败（如磁盘满），将出现 SlotData 与 SlotMetaDTO 不一致。此场景下 `SaveService` 转入 `Error` 状态（WriteFailed），不尝试回滚已写入的第一个文件。下次成功写入时两个文件均会被覆盖，一致性自动恢复。MVP 阶段接受此窗口期不一致（同步写入 < 20ms，窗口极小）；Post-MVP 可评估事务性写入（先写临时文件再原子重命名）。

---

## Dependencies

### 上游依赖（F5 依赖的系统）

F5 是 Foundation 层系统，**无构建期上游依赖**，可独立存在并最先开发。

**运行时软依赖（单向读取）**：
- **F2 BeatClock**：F2 在 `Start()` 中主动调用 `SaveService.LoadLatencyCalibration()` 读取延迟偏移并自行初始化。F5 无需持有 F2 引用；依赖方向为 F2 → F5（F2 读取 F5），无循环依赖。须保证 F5 的 `Initialize()` 在 F2 的 `Start()` 之前完成——Rule 14 通过「所有 `Awake()` 在第一个 `Start()` 前完成」的 Unity 引擎契约实现此保证，**不依赖 Script Execution Order 配置**（与 Rule 14 一致；SEO 在跨场景和动态实例化场景下不可靠）。

### 下游依赖（依赖 F5 的系统）

| 下游系统 | 依赖类型 | 从 F5 获取 / 写入 F5 的数据 | 接口 |
|---------|---------|--------------------------|------|
| **FE3** 延迟校准系统 | 硬依赖 | 读取 `latencyOffsetMs`（游戏启动时）；写入 `LatencyCalibrationDTO`（校准完成时） | `LoadLatencyCalibration(): LatencyCalibrationDTO` / `SaveLatencyCalibration(LatencyCalibrationDTO)` |
| **FE7** 游戏设置系统 | 硬依赖 | 读取 `GameSettingsDTO`（游戏启动时）；写入（设置更改时） | `LoadSettings()` / `SaveSettings(dto)` |
| **FE5** 局外成长系统 | 硬依赖 | 读取 `PlayerProgressDTO`（槽位激活时）；写入（局结算时） | `LoadSlotProgress(int)` / `SaveSlotProgress(int, dto)` |
| **PL4** 无障碍设置 | 软依赖 | 读取 `AccessibilityDTO`（游戏启动时）；写入（设置更改时） | `LoadAccessibility()` / `SaveAccessibility(dto)` |
| **P7** 主菜单 | 软依赖 | 读取 `SlotMetaDTO[3]`（UI 展示槽位列表） | `GetAllSlotMeta()` |

### 双向一致性注记

- F2 GDD `latency_offset_ms` 范围 `[-200, 200]` 与 F5-D3 `latencyOffsetMs [-200.0, 200.0]` 一致 ✅
- **F4 GDD**：F4-S7 PlayerInfoSO ↔ F5 DTO 字段映射表已建立（见 F4 GDD「Interactions」节 § F4-S7 ↔ F5 DTO 字段映射表）；`latencyCalibrationOffsetMs` 和 `audioVolumeMaster` 在 F4 侧标注为 `[NonSerialized]` 运行时缓存，权威来源为 F5 Global DTOs ✅
- **F5-D1 metaUpgradeLevels**：已与 F4-S7 `metaUpgradeLevels` 字段对齐（`MetaUpgradeEntry[]`），确保局外升级数据持久化往返完整 ✅
- FE3、FE5、P7 各自 GDD（设计完成后）须回引 F5，声明对 `SaveService` 的依赖

---

## Tuning Knobs

| 旋钮 | 常量名 / 位置 | 默认值 | 安全范围 | 影响的游戏方面 |
|------|-------------|-------|---------|-------------|
| 最大存档槽位数 | `SaveService.MaxSlotCount` | `3` | [1, 5] | 玩家可维护的独立存档数；增加会提升重玩可访问性，减少节省主菜单 UI 空间 |
| 存档版本号 | 各 DTO 内 `CurrentVersion` 常量 | 均为 `1`（MVP） | 只能向上递增（永不减少）；各 DTO 独立递增，互不影响 | DTO Schema 迁移控制；每次字段变更必须递增对应 DTO 版本号 |
| 延迟校准偏移范围 | `LatencyCalibrationDTO.latencyOffsetMs` | `[-200.0, 200.0]` | [-500, 500]（理论），实际由 FE3 GDD 锁定 | 可补偿的最大音频延迟；超出范围的硬件可能无法正确校准 |
| 音量默认值 | `GameSettingsDTO` 各字段默认值 | `masterVolume=1.0, musicVolume=0.8, sfxVolume=1.0` | [0.0, 1.0] | 首次启动时的音量感知；过高 sfxVolume 在首次启动时可能过于嘈杂 |

> *注：MVP 阶段无额外可调参数。`SaveDirectory = "saves"` 为不可变常量，定义于 F5-D5 文件路径节，不属于可调旋钮。AccessibilityDTO 字段（颜色无障碍模式、UI 缩放等）属于 PL4 GDD 范畴，F5 仅存取原始值，不在本文档中定义其有效范围。*

---

## Visual/Audio Requirements

F5 是纯持久化基础设施，**无直接视觉或音频输出需求**。

唯一涉及用户感知的输出是错误通知（`OnSaveError` 事件），由 UI 层（P7 主菜单或系统弹窗组件）订阅并显示错误对话框。错误弹窗的视觉样式属于 P7 / PL4 GDD 范畴，不由 F5 定义。

---

## UI Requirements

无 Runtime UI 需求。

开发期可选在 Unity Editor 调试面板（Inspector 扩展或 EditorWindow）展示当前 `SaveService` 状态、各槽位数据和存档文件路径——该工具属于 Editor-only 实现（`Assets/Editor/` 目录），不计入 MVP 需求，不打包进游戏构建。

---

## Acceptance Criteria

### 功能性

**AC-F5-01 — 初始化（首次启动）**
- 给定：磁盘上不存在任何存档文件
- 当：`SaveService.Initialize()` 调用完成
- 则：
  - `LoadSlotProgress(0)`、`LoadSlotProgress(1)`、`LoadSlotProgress(2)` 均返回 `{merit=0, unlockedWeaponIds=[], metaUpgradeLevels=[], runCount=0, totalPlayTimeSeconds=0.0}`
  - `LoadLatencyCalibration()` 返回 `{latencyOffsetMs=0.0f, isCalibrated=false}`
  - `LoadSettings()` 返回 `{masterVolume=1.0f, musicVolume=0.8f, sfxVolume=1.0f}`
  - `LoadAccessibility()` 返回 `AccessibilityDTO` 的全默认值（具体字段由 PL4 GDD 定义后补充）
  - `GetAllSlotMeta()` 返回三个 `slotState=SlotState.Empty` 的 SlotMetaDTO
  - `SaveService` 处于 `Idle` 状态，`saves/` 和 `saves/global/` 目录均已创建

**AC-F5-02 — 槽位进度往返（写入 → 读取）**
- 给定：`SaveService` 处于 `Idle` 状态
- 当：调用 `SaveSlotProgress(0, dto)` 写入 `{merit=500, unlockedWeaponIds=["weapon_miaodao"], metaUpgradeLevels=[{id="upgrade_hp", level=2}], runCount=3}`，再调用 `LoadSlotProgress(0)`
- 则：返回的 DTO 字段与写入值完全一致（位精度）

**AC-F5-03 — 全局数据往返（延迟校准）**
- 给定：`SaveService` 处于 `Idle` 状态
- 当：调用 `SaveLatencyCalibration(new LatencyCalibrationDTO { latencyOffsetMs = 42.5f, isCalibrated = true })`，再调用 `LoadLatencyCalibration()`
- 则：返回的 DTO 满足 `latencyOffsetMs == 42.5f`（允许 ≤ 0.001f 精度误差），`isCalibrated == true`

**AC-F5-04 — 多槽位独立性**
- 给定：`SaveService` 处于 `Idle` 状态
- 当：向 slot 0 写入 `merit=100`，向 slot 1 写入 `merit=999`
- 则：`LoadSlotProgress(0).merit == 100` 且 `LoadSlotProgress(1).merit == 999`（互不影响）

**AC-F5-05 — 跨会话持久化**
- 给定：向 slot 0 写入 `merit=777` 后，`SaveService` 销毁并重新实例化（模拟游戏重启）
- 当：`SaveService.Initialize()` 再次调用完成
- 则：`LoadSlotProgress(0).merit == 777`

**AC-F5-06 — SlotMetaDTO 随进度同步**
- 给定：slot 1 无存档（`GetAllSlotMeta()[1].slotState == SlotState.Empty`）
- 当：调用 `SaveSlotProgress(1, dto)` 写入数据
- 则：`GetAllSlotMeta()[1].slotState == SlotState.Active`，`meritSnapshot == dto.merit`，`lastPlayedUtcMs` 满足：(a) `>= 调用 SaveSlotProgress 前记录的 DateTimeOffset.UtcNow 毫秒值 - 5000`，(b) `<= 调用完成后记录的 DateTimeOffset.UtcNow 毫秒值 + 5000`（即测试执行时刻前后 5 秒以内）

**AC-F5-07 — 槽位文件损坏处理（两级模型：槽位级 → Idle）**
- 给定：通过 `MockFileStorage` 模拟 `slot_0.es3` 解析失败（或测试环境中手动将文件替换为无效字节）
- 当：`SaveService.Initialize()` 调用
- 则：
  - 断言①：`Initialize()` 完成后 `SaveService` 状态为 **`Idle`**（单槽位损坏不触发系统级 Error 状态）
  - 断言②：`OnSaveError` 事件已触发，`SaveErrorData.ErrorType == SaveErrorType.Corrupted`，`SaveErrorData.SlotIndex == 0`
  - 断言③：`slot_0.es3.corrupt` 文件存在，`slot_0.es3` 原文件不存在
  - 断言④：`GetAllSlotMeta()[0].slotState == SlotState.Corrupted`
  - 断言⑤：slot 1、slot 2 的 SlotMeta 正常加载，`slotState` 不受影响
  - 断言⑥：`LoadSlotProgress(1)` 和 `LoadSlotProgress(2)` 正常返回数据（未进入 Error 状态，其他槽位读取不受阻断）

**AC-F5-08 — 版本迁移（低版本存档）**
> *注：MVP 版本为 V1，V0 定义为"文件存在但 `save_version` 字段缺失或值为 0"，是合法的历史降级场景（如测试环境手动构造的存档）。*
> *`MockFileStorage` 须提供 `int SaveCallCount` 和 `int LoadCallCount` 追踪属性（测试辅助属性，不属于 `IFileStorage` 接口）。*
- 给定：通过 `MockFileStorage` 注入一个 `save_version = 0` 的 `slot_0.es3` 文件（含已知字段 `merit = 250, runCount = 5`）
- 当：`SaveService.Initialize()` 加载此文件
- 则：
  - `LoadSlotProgress(0).merit == 250`，`LoadSlotProgress(0).runCount == 5`（V0 存档已知字段值被正确迁移，不为默认值）
  - `LoadSlotProgress(0).save_version == PlayerProgressDTO.CurrentVersion`（数据已升级至当前版本）
  - `MockFileStorage.SaveCallCount == 1`（迁移触发恰好一次写入，即 V1 覆盖写入）
  - `MockFileStorage.LoadCallCount >= 1`（至少一次读取）
  - `SaveService` 处于 `Idle` 状态（迁移成功不触发 Error 状态）

**AC-F5-09 — 高版本存档阻断**
- 给定：构造 `save_version = 99`（未知版本）的存档文件
- 当：`SaveService.Initialize()` 加载此文件
- 则：`OnSaveError` 事件触发（附带 `SaveErrorType.VersionTooHigh`）；`MockFileStorage.SaveCallCount == 0`（`IFileStorage.Save` 未被调用）；`SaveService` 进入 `Error` 状态；`LoadSlotProgress(0)` 在 `AcknowledgeError()` 后返回默认 `PlayerProgressDTO`（`merit=0`）

**AC-F5-10 — 激活槽位管理**
- 给定：`SaveService` 初始化完成
- 当：调用 `GetActiveSlotIndex()`（未选择槽位时）
- 则：返回 `-1`

### 性能

**AC-F5-11 — 写入性能（单次存档 < 20ms）** `[Manual / Advisory]`
- 给定：`SaveService` 处于 `Idle` 状态，`PlayerProgressDTO` 包含最大字段（3 个武器 ID）
- 当：调用 `SaveSlotProgress(0, dto)`（PC 目标平台）
- 则：连续调用 `SaveSlotProgress(0, dto)` 至少 3 次（JIT 预热），第 4 次调用的 Unity Profiler 采样时间 < 20ms
- *注：Unity Profiler 采样不可在 CI 自动化，须在目标硬件上手动验证。*

**AC-F5-12 — 主线程约束验证**
> *须为 PlayMode 测试（需要真实线程创建）；`Debug.Log` 不可在非主线程调用，错误日志通过 `ConcurrentQueue` + 主线程 flush 输出。*
- 给定：`SaveService` 初始化完成，`_mainThreadId` 已在 `Awake()` 中捕获
- 当：从后台线程（`Task.Run()` 或 `new Thread()` 创建）调用 `SaveSlotProgress(0, dto)`，等待调用返回后，主线程继续运行至少 1 帧（等待错误队列 flush）
- 则：
  - 断言①：后台线程调用不抛出任何异常（调用栈不中断）
  - 断言②：等待 1 帧 flush 后，Unity 控制台记录至少一条 `LogType.Error` 级别日志，消息包含 "main thread"（日志由主线程 `Update()` 输出，不在后台线程中触发）
  - 断言③：`LoadSlotProgress(0)` 返回值与调用前相同（`IFileStorage.Save` 未被调用）
  - 断言④：`SaveService` 状态保持 `Idle`，不转入 `Error`

---

**AC-F5-13 — 激活槽位正向设置**
- 给定：`SaveService` 初始化完成，`GetActiveSlotIndex()` 返回 `-1`
- 当：调用 `SetActiveSlotIndex(1)`
- 则：`GetActiveSlotIndex()` 返回 `1`

**AC-F5-13b — 激活槽位越界验证**
- 给定：`SaveService` 初始化完成
- 当：调用 `SetActiveSlotIndex(3)`（越界）
- 则：抛出 `ArgumentOutOfRangeException`，`GetActiveSlotIndex()` 返回值不变

**AC-F5-14 — 写入失败（I/O 异常）**
- 给定：通过 mock/stub 令 ES3 写入操作抛出 `IOException`
- 当：调用 `SaveSlotProgress(0, dto)`
- 则：`OnSaveError` 事件触发，`SaveService` 转入 `Error` 状态；调用 `AcknowledgeError()` 后恢复 `Idle`；内存中上次写入的进度数据（`LoadSlotProgress(0)` 的缓存值）未被清除

**AC-F5-15 — 延迟校准值域边界往返**
- 给定：`SaveService` 处于 `Idle` 状态
- 当：调用 `SaveLatencyCalibration(new LatencyCalibrationDTO { latencyOffsetMs = -200.0f, isCalibrated = true })`，再调用 `LoadLatencyCalibration()`
- 则：返回值 `latencyOffsetMs == -200.0f`（误差 ≤ 0.001f），`isCalibrated == true`

**AC-F5-16 — DeleteSlot 清除数据**
- 给定：slot 0 已写入存档（`merit=500`），`SaveService` 处于 `Idle` 状态
- 当：调用 `DeleteSlot(0)`
- 则：`GetAllSlotMeta()[0].slotState == SlotState.Empty`；`LoadSlotProgress(0).merit == 0`（返回默认值）；磁盘上 `slot_0.es3` 和 `meta_0.es3` 均不存在

**AC-F5-17 — DeleteSlot 删除激活槽位自动退出激活状态**
- 给定：`SetActiveSlotIndex(0)` 后 `GetActiveSlotIndex()` 返回 `0`
- 当：调用 `DeleteSlot(0)`
- 则：`GetActiveSlotIndex()` 返回 `-1`（自动调用 `SetActiveSlotIndex(-1)`）

**AC-F5-18 — SetActiveSlotIndex(-1) 为合法调用**
- 给定：`SaveService` 初始化完成，`SetActiveSlotIndex(1)` 后 `GetActiveSlotIndex()` 返回 `1`
- 当：调用 `SetActiveSlotIndex(-1)`
- 则：`GetActiveSlotIndex()` 返回 `-1`，无异常抛出

**AC-F5-19 — DeleteSlot 越界验证**
- 给定：`SaveService` 初始化完成，各槽位数据完好
- 当：分别调用 `DeleteSlot(-1)` 和 `DeleteSlot(3)`（均越界）
- 则：均抛出 `ArgumentOutOfRangeException`；所有槽位数据不变（`GetAllSlotMeta()` 返回值与调用前相同）

**AC-F5-20 — Error 状态下 Write 方法抛出异常**
- 给定：通过 mock 令写入操作抛出 `IOException`，触发 `SaveService` 进入 `Error` 状态
- 当：在 `Error` 状态下调用任意写入方法（如 `SaveSlotProgress(0, dto)`）
- 则：抛出 `InvalidOperationException`（消息须包含 "Error state" 或等效描述）；`MockFileStorage.SaveCallCount` 未增加（`IFileStorage.Save` 未被调用）；`SaveService` 状态保持 `Error`

**AC-F5-21 — Initialize() 前调用只读 API 抛出异常**
- 给定：`SaveService` 实例已通过 `InjectStorage(MockFileStorage)` 注入，但 `Initialize()` 尚未调用（处于 `Uninitialized` 状态）
- 当：调用 `GetAllSlotMeta()`
- 则：抛出 `InvalidOperationException`（消息须包含 "not initialized" 或等效描述）

**AC-F5-22 — AcknowledgeError() 幂等性**
- 给定：`SaveService` 处于 `Idle` 状态（非 Error 状态）
- 当：调用 `AcknowledgeError()`
- 则：无异常抛出，`SaveService` 状态保持 `Idle`

**AC-F5-23 — GlobalData 损坏处理（两级模型：系统级 → Error）**
- 给定：通过 `MockFileStorage` 模拟 `global/settings.es3` 解析失败（`Load<T>()` 抛出 `FormatException`）
- 当：`SaveService.Initialize()` 调用
- 则：
  - 断言①：`SaveService` 状态为 **`Error`**（GlobalData 损坏触发系统级 Error 状态）
  - 断言②：`OnSaveError` 事件已触发，`SaveErrorData.ErrorType == SaveErrorType.Corrupted`，`SaveErrorData.SlotIndex == -1`（全局数据）
  - 断言③：调用 `AcknowledgeError()` 后，`SaveService` 状态转为 `Idle`
  - 断言④：`AcknowledgeError()` 后 `LoadSettings()` 返回 `GameSettingsDTO` 默认值（`masterVolume=1.0f, musicVolume=0.8f, sfxVolume=1.0f`）
  - 断言⑤：`settings.es3.corrupt` 备份文件存在

---

## Open Questions

1. ~~**ADR-D7（已解决）— EasySave 3 vs 自定义 JSON**~~：ADR-D7 Accepted (2026-07-14)，选定 EasySave 3。F5 接口设计已确保中间件无关；`ES3.Save()` / `ES3.Load()` 调用点限于 `ES3FileStorage : IFileStorage` 实现类内部。见 `docs/architecture/adr-d7-save-persistence-middleware.md`。

2. **`keyBindings` 字段格式**：`GameSettingsDTO.keyBindings` 的数据结构（按键名称映射、序列化格式）推迟至 FE7（游戏设置系统）GDD 设计阶段确定；FE7 GDD 须扩展 F5-D4 DTO 并递增 `GameSettingsDTO.CurrentVersion`。

3. **Mobile 路径验证**：`Application.persistentDataPath` 在 iOS（`Application.persistentDataPath` → `NSDocumentsDirectory`）和 Android（外部存储权限策略）的具体路径和权限行为须在移动端集成测试中验证，当前仅 PC 路径已确认。

4. **多用户设备下的延迟校准覆盖（已知局限，与 Rule 3 存在设计张力）**：当前设计将延迟校准数据绑定于设备（全局共享），而 Rule 3 明确将"家庭多人共用一台设备"列为 3 槽位设计的首要理由。这与"校准数据未按槽位隔离"直接矛盾——若 A、B 两名家庭成员使用不同音频设备，后者的校准数据将覆盖前者。MVP 阶段接受此局限（研发资源约束下的有意取舍；P7 须在槽位切换时提示玩家重新校准，以 UX 手段缓解体验落差）。如未来需要支持 per-slot 校准，接口 `SaveLatencyCalibration()` 可扩展为 `SaveLatencyCalibration(int slotIndex, LatencyCalibrationDTO)` 而无需修改 F5 整体结构。若 **MVP Playtest 首次收到家庭共用校准冲突报告**，须立即实现 per-slot 校准，不可推迟至 Post-MVP。

5. **Post-MVP：高版本存档的部分加载（Post-MVP Partial Load）**：当前迁移策略为链式升级（V1→V2→V3），要求每一版本的 migrator 均存在。若未来出现"仅新增字段"的纯加法式 schema 变更（如新增一个可为默认值的字段），可评估是否允许跳版本直接部分加载——即对未知字段填充默认值而非强制执行完整迁移链。此决策须在 Post-MVP 阶段的 ADR 中正式评估，MVP 阶段维持链式迁移策略不变。

6. **Mobile 关卡内断点续存（Mid-Run Checkpoint）**：移动端玩家可能在关卡进行中被打断（来电、切换 App），需要支持关卡内状态的快照保存与恢复。此功能的设计决策属于 FE5（Session / Run 系统）GDD 的职责范畴，不属于 F5 基础设施层。F5 的 `IFileStorage` 接口和文件路径方案在架构上可扩展以支持此用例，但具体 API（如 `SaveRunSnapshot` / `LoadRunSnapshot` 及对应 `RunSnapshotDTO`）须由 FE5 GDD 定义后，作为 F5 的扩展需求正式补充到本文档中——不可在 FE5 实现阶段直接调用未在 F5 GDD 中定义的接口。**FE5 GDD 必须回答移动端中断恢复问题，不可推迟至 Polish 阶段。**
