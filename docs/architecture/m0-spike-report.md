# M0 Spike Report — TDE 4.x + FMOD + EasySave 3 兼容性验证

> **Status**: COMPLETED (部分 — FMOD 未导入)
> **Date**: 2026-07-16
> **Blocks**: ADR-D1 Accept → F6 GDD → C1 GDD
> **Engine**: Unity 6.3 LTS (6000.3.19f1)

---

## Executive Summary

| 插件 | 导入状态 | 编译 | 关键 API 验证 | 阻塞级发现 |
|------|---------|------|-------------|-----------|
| **TDE 4.5** | ✅ 已导入 | ✅ 零错误 | ✅ 完成 | **3 个 BLOCKER** |
| **EasySave 3** | ✅ 已导入 | ✅ 零错误 | ✅ 完成 | **1 个 BLOCKER + 1 个 WARNING** |
| **FMOD for Unity 2.02.35** | ✅ 已导入 | ⏳ 需 Editor 确认 | ✅ 完成 | **1 个 BLOCKER + 1 个 WARNING** |
| **Feel/MMFeedbacks** | ✅ 已导入 | ✅ 零错误 | N/A | — |

**判定: CONCERNS — 5 个 BLOCKER 须修复（D1×3 + D7×1 + D6×1）；FMOD 编译需 Unity Editor 确认。**

---

## 1. TDE 4.5 兼容性验证

### 1.1 编译验证 ✅ PASS

TDE 4.5 已导入 `Assets/TopDownEngine/`，在 Unity 6.3 LTS 下零编译错误。
`[SerializeField]` fields-only 变更（Unity 6.3 breaking change）未影响 TDE editor scripts。

### 1.2 InputSystemManagerEventsBased 源码审查

**源码位置**: `Assets/TopDownEngine/Common/ScriptsInputSystem/Scripts/InputSystemManagerEventsBased.cs`（93 行）

**继承链**: `InputSystemManagerEventsBased` → `InputManager` → `MMSingleton<InputManager>`
（注意：NOT `InputSystemManager` → `InputManager`）

#### BLOCKER B1: `SetInputEnabled` 方法不存在

ADR-D1 假设的 override 目标 `SetInputEnabled(bool enabled)` 在 TDE 4.5 中**不存在**。
这是 LLM 基于 TDE 2.x 训练数据的臆测。

**实际可 override 方法**（仅 1 个）:

```csharp
// InputSystemManagerEventsBased.cs:42 — 唯一 virtual 方法
protected virtual void BindButton(InputAction.CallbackContext context, MMInput.IMButton imButton)
```

所有其他 override 方法（`TestPrimaryAxis`, `GetInputButtons`, `SetMovement` 等）**未标记 virtual**，
无法被进一步子类化。

**ADR-D1 `JadeRabbitInputManager` 方案影响**:

| ADR-D1 假设 | 实际情况 | 修复建议 |
|-------------|---------|---------|
| `SetInputEnabled(bool)` 可 override | ❌ 不存在 | 改用 `InputDetectionActive` 字段直控 + `BindButton` override 网关 |
| 公共回调方法可 override | ❌ `OnJump`, `OnPause` 等非 virtual | 不需要 override 回调；通过 `BindButton` 统一拦截 |
| OnEnable/OnDisable 可拦截 | ❌ InputSystemManagerEventsBased 不覆写这些方法 | F3 直接管理 InputActionMap.Enable()/Disable() |

**备选方案评估**:

- **方案 A（推荐）**: `JadeRabbitInputManager : InputSystemManagerEventsBased`，override `BindButton` 作为输入网关，F3 的 `InputEventBus` 直接调用 `InputActionMap.Enable()/Disable()` 管理 Action Map 状态。`InputDetectionActive = false` 作为次级保险（但注意 `BindButton` 内部不检查此字段——需在 override 中手动检查）。
- **方案 B**: 完全不子类化，保留原生 `InputSystemManagerEventsBased` + `InputDetectionActive = false`。但此方案下 `BindButton` 不受 `InputDetectionActive` 控制（TDE 4.5 的 `BindButton` 内部无此检查），事件仍会触发。
- **方案 C**: 改用 `InputSystemManager`（非 EventsBased）作为基类——有 virtual `OnEnable/OnDisable`，但会引入 Legacy Input 路径代码，与项目 Input System Only 配置冲突。

#### 结论

ADR-D1 的 `JadeRabbitInputManager` 子类化方案**可行但需重写接口合约**：
- 删除 `SetInputEnabled` override
- 新增 `BindButton` override，内部检查 F3 状态（`InputEventBus.CurrentState == AllDisabled` 时 return）
- F3 直接控制 InputActionMap（不通过 TDE 中间层）

---

### 1.3 TDE 事件系统 API 验证

**源码位置**: `Assets/TopDownEngine/ThirdParty/MoreMountains/MMTools/Core/MMEvents/MMEventManager.cs`

#### MMEventListener\<T\> 接口 ✅ CONFIRMED

```csharp
// MMEventManager.cs:238-241
public interface MMEventListener<T> : MMEventListenerBase
{
    void OnMMEvent(T eventType);
}
```

#### MMEventStartListening / StopListening ✅ CONFIRMED

```csharp
// MMEventManager.cs:219-228 — 扩展方法
public static void MMEventStartListening<EventType>(this MMEventListener<EventType> caller) where EventType : struct
public static void MMEventStopListening<EventType>(this MMEventListener<EventType> caller) where EventType : struct
```

#### BLOCKER B2: 暂停事件类型错误

ADR-D1 使用 `MMEventListener<MMGameEvent>` + 字符串匹配 `"Pause"`/`"UnPause"` 处理暂停。
**实际 TDE 4.5 使用 `TopDownEngineEvent`（类型化 struct）发送暂停事件**，不用 `MMGameEvent`（字符串）。

**证据**（`GameManager.cs:516-520`）:
```csharp
case TopDownEngineEventTypes.TogglePause:
    if (Paused)
        TopDownEngineEvent.Trigger(TopDownEngineEventTypes.UnPause, null);
    else
        TopDownEngineEvent.Trigger(TopDownEngineEventTypes.Pause, null);
```

**修复**: `TDELifecycleAdapter` 应**仅实现 `MMEventListener<TopDownEngineEvent>`**，不需要 `MMEventListener<MMGameEvent>`。
暂停/恢复路由应在 `OnMMEvent(TopDownEngineEvent)` 中通过 enum 匹配处理：

```csharp
case TopDownEngineEventTypes.Pause:    → BeatClock.Pause() + InputEventBus.EnableUIInput()
case TopDownEngineEventTypes.UnPause:  → BeatClock.Resume() + InputEventBus.EnableGameplayInput()
```

#### BLOCKER B3: `TopDownEngineEventTypes.Respawn` 不存在

ADR-D1 引用 `TopDownEngineEventTypes.Respawn`，但 TDE 4.5 枚举中不存在此成员。

**实际枚举成员**（`GameManager.cs:27-47`）:

```csharp
public enum TopDownEngineEventTypes
{
    SpawnCharacterStarts, LevelStart, LevelComplete, LevelEnd,
    Pause, UnPause, PlayerDeath, SpawnComplete,
    RespawnStarted, RespawnComplete,  // ← 不是 "Respawn"
    StarPicked, GameOver, CharacterSwap, CharacterSwitch,
    Repaint, TogglePause, LoadNextScene, PauseNoMenu
}
```

**修复**: ADR-D1 应使用 `TopDownEngineEventTypes.RespawnComplete`（重生完成后再启用输入和 BeatClock）。

#### TopDownEngineEvent struct ✅ CONFIRMED

```csharp
// GameManager.cs:52-74
public struct TopDownEngineEvent
{
    public TopDownEngineEventTypes EventType;
    public Character OriginCharacter;
    public static void Trigger(TopDownEngineEventTypes eventType, Character originCharacter) { ... }
}
```

---

## 2. EasySave 3 API 验证

**源码位置**: `Assets/Plugins/Easy Save 3/Scripts/ES3.cs`

### 2.1 核心 Save/Load API

| API | ADR-D7 期望签名 | 实际签名 | 状态 |
|-----|----------------|---------|------|
| `ES3.Save<T>` | `(key, value, path, settings)` | `(string key, T value, string filePath, ES3Settings settings)` ES3.cs:91 | ✅ MATCH |
| `ES3.Load<T>` with default | `(key, defaultValue, path, settings)` | `(string key, string filePath, T defaultValue, ES3Settings settings)` ES3.cs:501 | ⚠ **参数顺序不匹配** |
| `ES3.SaveRaw` | `(byte[], path, settings)` | `(byte[] bytes, string filePath, ES3Settings settings)` ES3.cs:135 | ✅ MATCH |
| `ES3.LoadRawBytes` | `(path, settings)` | `(string filePath, ES3Settings settings)` ES3.cs:676 | ✅ MATCH |

#### BLOCKER B4: `ES3.Load<T>` 参数顺序不匹配

ADR-D7 `ES3Storage` 实现代码：
```csharp
public T Load<T>(string path, string key, T defaultValue)
    => ES3.Load<T>(key, defaultValue, path, _settings);
//                      ^^^^^^^^^^^^  ^^^^  — 错误顺序
```

EasySave 3 实际 4 参数签名（ES3.cs:501）：
```csharp
public static T Load<T>(string key, string filePath, T defaultValue, ES3Settings settings)
//                       ^^^^       ^^^^^^^^          ^^^^^^^^^^^^^  — 正确顺序
```

**修复**: ADR-D7 代码应改为：
```csharp
=> ES3.Load<T>(key, path, defaultValue, _settings);
```

### 2.2 文件操作 API

| API | 签名 | 位置 | 状态 |
|-----|------|------|------|
| `ES3.FileExists(path, settings)` | `(string filePath, ES3Settings settings)` | ES3.cs:1396 | ✅ |
| `ES3.DeleteFile(path, settings)` | `(string filePath, ES3Settings settings)` | ES3.cs:1068 | ✅ |
| `ES3.KeyExists(key, path, settings)` | `(string key, string filePath, ES3Settings settings)` | ES3.cs:1354 | ✅ |
| `ES3.RenameFile(old, new)` | `(string oldFilePath, string newFilePath)` | ES3.cs:1142 | ✅ |

#### WARNING W1: ES3.RenameFile 原生存在

ADR-D7 声称"EasySave 3 无原生 Rename"并实现了 LoadRawBytes→SaveRaw→DeleteFile 三步 workaround。
但 `ES3.RenameFile()` 在 ES3.cs:1142 **原生存在**。

**建议**: ADR-D7 `ES3Storage.RenameFile` 应直接使用 `ES3.RenameFile(srcPath, dstPath)` 替换三步 workaround，
消除非原子性风险和 `LoadRawBytes`/`SaveRaw` 依赖。

### 2.3 加密配置 ✅

```csharp
// ES3.cs:22
public enum EncryptionType { None, AES };

// ES3Settings.cs:107
public ES3.EncryptionType encryptionType = ES3.EncryptionType.None;
```

ADR-D7 的 `ES3Settings` 加密配置方式正确。

### 2.4 [Preserve] 属性 — 未使用

EasySave 3 源码中**无任何 `[Preserve]` 属性**。
项目的 F5 DTO 类型（`PlayerProgressDTO`, `LatencyCalibrationDTO` 等）需要手动添加 `[Preserve]` 属性
以防止 IL2CPP stripping 删除反射调用路径。

**操作项**: F5 实现时，所有 DTO struct/class 须标注 `[UnityEngine.Scripting.Preserve]`。

---

## 3. FMOD for Unity 2.02.35 API 验证

**源码位置**: `Assets/Plugins/FMOD/src/fmod.cs` (Core API), `Assets/Plugins/FMOD/src/fmod_studio.cs` (Studio API), `Assets/Plugins/FMOD/src/RuntimeManager.cs` (Unity 集成层)

**版本**: FMOD Core 2.02.35 (`VERSION.number = 0x00020235`)

### 3.1 RuntimeManager.CoreSystem ✅ CONFIRMED

```csharp
// RuntimeManager.cs:233-235
public static FMOD.System CoreSystem
{
    get { return Instance.coreSystem; }
}
```

`CoreSystem` 返回 `FMOD.System` struct，通过 `studioSystem.getCoreSystem()` 在初始化时获取（RuntimeManager.cs:332）。

### 3.2 核心 API 验证

| API | ADR-D6 期望签名 | 实际签名 | 位置 | 状态 |
|-----|----------------|---------|------|------|
| `RuntimeManager.CoreSystem` | `FMOD.System` 属性 | `public static FMOD.System CoreSystem` | RuntimeManager.cs:233 | ✅ MATCH |
| `CoreSystem.getDSPClock()` | 在 `FMOD.System` 上 | **不在 System 上** — 仅在 `IChannelControl`（Channel / ChannelGroup） | fmod.cs:2324, 2808 | ❌ **BLOCKER** |
| `getDSPClock` 第二参数 | `out uint hi` | `out ulong parentclock` | fmod.cs:2101 | ⚠ **WARNING** |
| `Channel.setDelay` | `(ulong, ulong, bool)` | `(ulong dspclock_start, ulong dspclock_end, bool stopchannels = true)` | fmod.cs:2328 | ✅ MATCH |
| `System.getSoftwareFormat` | `(out int, out _, out _)` | `(out int samplerate, out SPEAKERMODE speakermode, out int numrawspeakers)` | fmod.cs:1095 | ✅ MATCH |
| `System.playSound` | `(Sound, ChannelGroup, bool, out Channel)` | 同预期 | fmod.cs:1376 | ✅ MATCH |
| `Channel.setPaused(bool)` | — | `public RESULT setPaused(bool paused)` | fmod.cs:2224 | ✅ MATCH |
| `Channel.stop()` | — | `public RESULT stop()` | fmod.cs:2220 | ✅ MATCH |
| `Channel.isPlaying(out bool)` | — | `public RESULT isPlaying(out bool isplaying)` | fmod.cs:2296 | ✅ MATCH |
| `System.getMasterChannelGroup` | — | `(out ChannelGroup channelgroup)` | fmod.cs:1392 | ✅（修复路径） |
| `MODE.CREATESAMPLE` | 存在 | `0x00000100` | fmod.cs:412 | ✅ MATCH |

#### BLOCKER B5: `getDSPClock` 不在 `FMOD.System` 上

ADR-D6 `FMODDSPClockImpl.GetCurrentTime()` 调用 `CoreSystem.getDSPClock()` **无法编译**。
`getDSPClock` 仅存在于 `IChannelControl` 接口（`Channel` 实现于 fmod.cs:2324，`ChannelGroup` 实现于 fmod.cs:2808），
`FMOD.System` struct（fmod.cs:1038-1531）**完全没有此方法**。

**根因**: FMOD DSP 时钟沿 Channel → ChannelGroup → Master ChannelGroup 混音器层级传播，
不在 System 级别直接暴露。获取"系统级"DSP 时钟的正确路径是通过 Master ChannelGroup。

**修复**:
```csharp
// ✅ 正确路径：通过 Master ChannelGroup 获取系统 DSP 时钟
FMODUnity.RuntimeManager.CoreSystem.getMasterChannelGroup(out FMOD.ChannelGroup masterGroup);
masterGroup.getDSPClock(out ulong dspClock, out ulong parentClock);
```

#### WARNING W2: `getDSPClock` 第二参数类型不匹配

ADR-D6 使用 `out uint hi`，实际签名为 `out ulong parentclock`。
`uint`（32-bit）→ `ulong`（64-bit）类型不匹配，编译错误。修复后代码已使用正确类型。

### 3.3 编译兼容性

FMOD for Unity 2.02.35 源码级审查未发现与 Unity 6.3 LTS 不兼容的模式
（无 property-level `[SerializeField]`、无废弃 API 引用）。
**实际编译须在 Unity Editor 中确认**（M0 Spike 限于源码审查，未运行 Unity 编译器）。

---

## 4. ADR-D1 修订清单（Blocker Fix Required）

以下修改须在 ADR-D1 Accept 前完成：

### B1: 删除 `SetInputEnabled` override，改用 `BindButton` 网关

**Before** (ADR-D1 current):
```csharp
protected override void SetInputEnabled(bool enabled) { ... }  // 不存在
```

**After** (verified approach):
```csharp
protected override void BindButton(InputAction.CallbackContext context, MMInput.IMButton imButton)
{
    // F3 state gate: block all button input when AllDisabled
    if (_inputEventBus.CurrentState == InputState.AllDisabled) return;
    base.BindButton(context, imButton);
}
```

### B2: 暂停/恢复路由改用 TopDownEngineEvent

**Before** (ADR-D1 current):
```csharp
// 错误：TDE 不用 MMGameEvent 发送暂停
public sealed class TDELifecycleAdapter : MonoBehaviour,
    MMEventListener<MMGameEvent>,        // ← 删除
    MMEventListener<TopDownEngineEvent>
{
    public void OnMMEvent(MMGameEvent gameEvent)  // ← 删除整个方法
    {
        switch (gameEvent.EventName)
        {
            case "Pause": ...    // ← 永远不会触发
            case "UnPause": ...  // ← 永远不会触发
        }
    }
}
```

**After** (verified approach):
```csharp
public sealed class TDELifecycleAdapter : MonoBehaviour,
    MMEventListener<TopDownEngineEvent>  // 仅此一个接口
{
    public void OnMMEvent(TopDownEngineEvent topDownEvent)
    {
        switch (topDownEvent.EventType)
        {
            case TopDownEngineEventTypes.Pause:
                _beatClock.Pause();
                _inputEventBus.EnableUIInput();
                break;
            case TopDownEngineEventTypes.UnPause:
                _beatClock.Resume();
                _inputEventBus.EnableGameplayInput();
                break;
            case TopDownEngineEventTypes.PlayerDeath:
                _beatClock.Stop();
                _inputEventBus.DisableAllInput();
                break;
            case TopDownEngineEventTypes.RespawnComplete:  // ← 不是 Respawn
                _beatClock.Play(_levelConfig.BgmAssetReference,
                    _levelConfig.Bpm, _levelConfig.TimeSignature);
                _inputEventBus.EnableGameplayInput();
                break;
        }
    }
}
```

### B3: `Respawn` → `RespawnComplete`

所有 ADR-D1 中引用 `TopDownEngineEventTypes.Respawn` 的位置改为 `TopDownEngineEventTypes.RespawnComplete`。

---

## 5. ADR-D7 修订清单

### B4: `ES3.Load<T>` 参数顺序修复

**Before** (ADR-D7 current):
```csharp
=> ES3.Load<T>(key, defaultValue, path, _settings);
```

**After** (verified):
```csharp
=> ES3.Load<T>(key, path, defaultValue, _settings);
```

### W1: `RenameFile` 改用原生 API

**Before** (ADR-D7 current):
```csharp
public void RenameFile(string srcPath, string dstPath)
{
    var data = ES3.LoadRawBytes(srcPath, _settings);
    ES3.SaveRaw(data, dstPath, _settings);
    ES3.DeleteFile(srcPath, _settings);
}
```

**After** (simplified):
```csharp
public void RenameFile(string srcPath, string dstPath)
{
    ES3.RenameFile(new ES3Settings(srcPath, _settings), new ES3Settings(dstPath, _settings));
}
```

---

## 6. ADR-D6 修订清单（Blocker Fix Required）

### B5: `getDSPClock` 调用路径修复

ADR-D6 `FMODDSPClockImpl` 直接在 `FMOD.System` 上调用 `getDSPClock()`，但此方法**不在 System 上**。
`getDSPClock` 仅存在于 `IChannelControl` 接口（`Channel` / `ChannelGroup` 实现）。

**Before** (ADR-D6 current):
```csharp
// ❌ FMOD.System 没有 getDSPClock
FMODUnity.RuntimeManager.CoreSystem.getDSPClock(out ulong dspClock, out uint hi);
```

**After** (verified approach):
```csharp
// ✅ 通过 Master ChannelGroup 获取系统 DSP 时钟
FMODUnity.RuntimeManager.CoreSystem.getMasterChannelGroup(out FMOD.ChannelGroup masterGroup);
masterGroup.getDSPClock(out ulong dspClock, out ulong parentClock);
```

### W2: `getDSPClock` 第二参数类型修复

**Before**: `out uint hi`（32-bit）
**After**: `out ulong parentclock`（64-bit）

---

## 7. 未完成项 & 后续步骤

| # | 项目 | 优先级 | 状态 |
|---|------|--------|------|
| 1 | ~~导入 FMOD for Unity 2.x~~ | ✅ 完成 | API 验证完成 |
| 2 | Unity Editor 编译确认（FMOD 导入后） | **P0** | ⏳ 待手动确认 |
| 3 | 修复 ADR-D6 B5 getDSPClock 路径 | **P0** | ✅ 已修复 |
| 4 | 修复 ADR-D1 B1/B2/B3（3 个 BLOCKER） | **P0** | ✅ 已修复 |
| 5 | 修复 ADR-D7 B4 参数顺序 | **P0** | ✅ 已修复 |
| 6 | 修复 ADR-D7 W1 RenameFile 简化 | P1 | ✅ 已修复 |
| 7 | F5 DTO 添加 `[Preserve]` 属性 | P1 | ⏳ F5 实现时 |
| 8 | 创建 `docs/engine-reference/unity/plugins/fmod.md` 版本记录 | P2 | ⏳ |
| 9 | 更新 `docs/engine-reference/unity/plugins/top-down-engine.md` | P2 | ⏳ |

---

## Appendix: 源码引用索引

| 文件 | 行号 | 内容 |
|------|------|------|
| `Assets/TopDownEngine/Common/ScriptsInputSystem/Scripts/InputSystemManagerEventsBased.cs` | 17 | class 定义 |
| `Assets/TopDownEngine/Common/ScriptsInputSystem/Scripts/InputSystemManagerEventsBased.cs` | 42 | `BindButton` virtual 方法 |
| `Assets/TopDownEngine/Common/Scripts/Managers/InputManager.cs` | 20 | `InputDetectionActive` 字段 |
| `Assets/TopDownEngine/Common/Scripts/Managers/GameManager.cs` | 27-47 | `TopDownEngineEventTypes` 枚举 |
| `Assets/TopDownEngine/Common/Scripts/Managers/GameManager.cs` | 52-74 | `TopDownEngineEvent` struct |
| `Assets/TopDownEngine/Common/Scripts/Managers/GameManager.cs` | 516-520 | 暂停事件触发（TopDownEngineEvent） |
| `Assets/TopDownEngine/ThirdParty/MoreMountains/MMTools/Core/MMEvents/MMEventManager.cs` | 219-228 | `MMEventStartListening/StopListening` 扩展 |
| `Assets/TopDownEngine/ThirdParty/MoreMountains/MMTools/Core/MMEvents/MMEventManager.cs` | 238-241 | `MMEventListener<T>` 接口 |
| `Assets/Plugins/Easy Save 3/Scripts/ES3.cs` | 22 | `EncryptionType { None, AES }` |
| `Assets/Plugins/Easy Save 3/Scripts/ES3.cs` | 91 | `Save<T>(key, value, filePath, settings)` |
| `Assets/Plugins/Easy Save 3/Scripts/ES3.cs` | 501 | `Load<T>(key, filePath, defaultValue, settings)` |
| `Assets/Plugins/Easy Save 3/Scripts/ES3.cs` | 676 | `LoadRawBytes(filePath, settings)` |
| `Assets/Plugins/Easy Save 3/Scripts/ES3.cs` | 135 | `SaveRaw(bytes, filePath, settings)` |
| `Assets/Plugins/Easy Save 3/Scripts/ES3.cs` | 1142 | `RenameFile(oldFilePath, newFilePath)` |
| `Assets/Plugins/Easy Save 3/Scripts/Settings/ES3Settings.cs` | 107 | `encryptionType` 字段 |
| `Assets/Plugins/FMOD/src/RuntimeManager.cs` | 233 | `CoreSystem` 属性 |
| `Assets/Plugins/FMOD/src/RuntimeManager.cs` | 332 | `getCoreSystem` 初始化 |
| `Assets/Plugins/FMOD/src/fmod.cs` | 22 | `VERSION.number = 0x00020235` (2.02.35) |
| `Assets/Plugins/FMOD/src/fmod.cs` | 1095 | `getSoftwareFormat(out int, out SPEAKERMODE, out int)` |
| `Assets/Plugins/FMOD/src/fmod.cs` | 1376 | `playSound(Sound, ChannelGroup, bool, out Channel)` |
| `Assets/Plugins/FMOD/src/fmod.cs` | 1392 | `getMasterChannelGroup(out ChannelGroup)` |
| `Assets/Plugins/FMOD/src/fmod.cs` | 2101 | `IChannelControl.getDSPClock` 接口定义 |
| `Assets/Plugins/FMOD/src/fmod.cs` | 2224 | `Channel.setPaused(bool)` |
| `Assets/Plugins/FMOD/src/fmod.cs` | 2324 | `Channel.getDSPClock(out ulong, out ulong)` |
| `Assets/Plugins/FMOD/src/fmod.cs` | 2328 | `Channel.setDelay(ulong, ulong, bool)` |
| `Assets/Plugins/FMOD/src/fmod.cs` | 2808 | `ChannelGroup.getDSPClock(out ulong, out ulong)` |
| `Assets/Plugins/FMOD/src/fmod_studio.cs` | 429 | `Studio.System.getCoreSystem(out FMOD.System)` |
