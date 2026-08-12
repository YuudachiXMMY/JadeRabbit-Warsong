# FMOD for Unity (Firelight Technologies) — Plugin Reference

| Field | Value |
|-------|-------|
| **Publisher** | Firelight Technologies (FMOD) |
| **Version** | 2.02.35（C# 绑定 `VERSION.number = 0x00020235`，Studio 2.02 系列） |
| **Verified with Unity** | 6.3 LTS (6000.3.19f1) |
| **Asset Store** | https://assetstore.unity.com/packages/tools/audio/fmod-for-unity-161631 |
| **Official Docs** | https://fmod.com/docs/2.02/unity |
| **Last Verified** | 2026-08-12 |
| **LLM Knowledge Gap** | MEDIUM — FMOD Studio 2.02 API 表面稳定，但 Unity 6.3 原生插件校验管线变更 + `getDSPClock()` 挂载点（master channel group）易被 LLM 记错 |

---

## M0 Spike 验证结果（2026-08-12）

本项目将 FMOD 列为唯一落在 MVP 关键路径上的 HIGH 引擎风险（`architecture.md` § 风险表 / QQ-07）。
M0 Spike 已消解 API 表面风险；本节记录**编译期二进制确认**（残留项）的验证证据。

| 验证项 | 结论 | 证据来源 |
|--------|------|----------|
| **C# 绑定编译**（Unity 6.3） | ✅ 通过 — 0 error，1 条非阻塞弃用警告 | Editor.log `Reloading assemblies after forced synchronous recompile`；`Library/ScriptAssemblies/FMODUnity.dll` + `FMODUnityEditor.dll` + Resonance dll 已产出 |
| **原生二进制加载**（Editor / macOS arm64） | ✅ 通过 — `Refreshing native plugins … found 7 plugins` / `Preloading 2 native plugins` | Editor.log；macOS bundle 为 universal（`lipo` 确认 `x86_64 arm64`） |
| **全平台原生库就位** | ✅ 就位（见下方原生二进制表） | `Assets/Plugins/FMOD/platforms/*/lib` |
| **FMOD Studio banks 已构建** | ⚠️ **未构建** — `EventManager.UpdateCache()` 抛 `DirectoryNotFoundException` | Editor.log（`EventManager.cs:132`）；缺 `JadeRabbit-Warsong_FMOD/Build/Desktop`；项目内无 `.bank` 文件 |

**唯一弃用警告**：`SettingsEditor.cs(2265): warning CS0618 'TreeViewItem' is obsolete`——FMOD 自带 Editor 代码引用了 Unity 6 已弃用的 `TreeViewItem`，属 FMOD 侧、非阻塞，禁止改 FMOD 源码（见 `technical-preferences.md` Forbidden Patterns），静待 FMOD 官方升级。

**⚠️ 已知非阻塞异常**：FMOD Studio 工程 `JadeRabbit-Warsong_FMOD/JadeRabbit-Warsong_FMOD.fspro` 已存在（含 Metadata），
但**尚未 Build 出 banks**，故 `EventManager.Startup()` 在 Editor 启动时抛 `DirectoryNotFoundException`。
这是 M0 阶段的**预期状态**（尚无音频内容），不阻塞 C# 编译与原生库加载。
**首次接入音频内容前**须在 FMOD Studio 中 Build banks 到 `Build/Desktop`（或据 `FMODStudioSettings` 的 `SourceProjectPath` 重新指向），异常即消除。

> 注：Unity headless **batchmode**（`-batchmode -quit -nographics`）在本机因 license 实体缺失（`Licensing::Client Error: Code 404 … Found 0 entitlement groups`）无法完成完整导入/编译；
> 上述编译与原生插件加载证据取自**交互式 Editor 会话**的 Editor.log。CI 中的编译门控依赖 `game-ci/unity-test-runner`（须配置激活 license），而非本地 batchmode。

---

## Purpose in This Project

FMOD 提供 **DSP 时钟真值**，是节奏系统（F2 BeatClock，ADR-D6）的物理底层：

| FMOD 负责 | CombatCore / F2 负责 |
|-----------|---------------------|
| DSP 时钟（样本级精度） | 拍点事件广播 / 量化判定 |
| BGM 事件播放 + 样本级调度 | `LatencyOffsetMs` 补偿 / 拍号切换 |
| Pause/Resume 底层音频 | 生命周期唯一发布权（BeatClock）|
| 音频线程 → 主线程数据推送 | 跨线程边界之后的全部 C# 逻辑 |

- **ADR-D6 已选定 FMOD Studio 2.x**（Accepted 2026-07-14），否决 Unity Audio DSP。
- 生产实现：`FMODDSPClockImpl`（`IDSPClock`）+ `FMODAudioPlayerImpl`（`IAudioPlayer`）；测试用 `MockDSPClock` / `MockAudioPlayer` 注入（DI 缝隙见 ADR-D8）。

---

## API 表面（M0 已验证，HIGH 关注点）

| 用途 | FMOD API | 关键约束 |
|------|----------|----------|
| DSP 时钟真值 | `FMOD.ChannelGroup.getDSPClock()` | **挂 master channel group，不是 `FMOD.System`**；配合 `coreSystem.getMasterChannelGroup()` |
| 采样率 | `coreSystem.getSoftwareFormat()` | 取 FMOD 软件格式采样率；**禁用 `AudioSettings.outputSampleRate`**（headless 下为 0） |
| 样本级调度 | `FMOD.Channel.setDelay()` / `IAudioPlayer.PlayScheduled(double)` | 小节边界无缝换 BGM；raw DSP 坐标系，禁叠 offset（Core Rule 13） |
| 立即停止 | `FMOD_STUDIO_STOP_IMMEDIATE` | `IAudioPlayer.Stop()` 映射；淡出属 F6 BGM 过渡策略 |
| 暂停 / 恢复 | `setPaused(true/false)` | `Pause()` 不释放播放位置，DSP 时钟暂停态仍可查询；仅供**菜单暂停**，禁用于逐击微暂停 |

---

## 线程模型（ADR-D6 Addendum，normative）

- 调度机制钦定为 **FMODUnity `RuntimeManager` + 主线程 `Update()` 轮询** FMOD DSP 时钟。
- **否决 `OnAudioFilterRead`**；**禁止引入 `ConcurrentQueue<BeatEventData>`**（徒增 ~1 帧延迟、零收益）。
- 唯一跨线程边界 = FMOD 音频线程 ↔ 主线程一处，由 `RuntimeManager.Update()` 保证**同帧先于** `BeatClock.Update()` 执行；C# 侧零跨线程、无需同步原语。
- 下游合约：订阅 F2 事件的系统**必须**使用 `BeatEventData.beat_dsp_time` 判定时序，**禁止**在回调触发时读 `AudioSettings.dspTime`。

---

## 原生二进制（platforms/*/lib）

| 平台 | 库 | 架构 / ABI | M0 验证 |
|------|----|-----------|---------|
| macOS | `fmodstudio.bundle` / `fmodstudioL.bundle` / `resonanceaudio.bundle` | universal（`x86_64` + `arm64`） | ✅ Editor 加载确认 |
| Windows | `fmod[L].dll` / `fmodstudio[L].dll` | `arm64` / `x86` / `x86_64` | 就位（未跑构建） |
| Linux | `libfmod[L].so` / `libfmodstudio[L].so` | `x86_64` | 就位 |
| Android | `libfmod[L].so` / `libfmodstudio[L].so` | `armeabi-v7a` / `arm64-v8a` / `x86` | 就位 |
| iOS / tvOS / visionOS | 静态库 + `platform_ios.mm` | Apple 平台 | 就位 |
| HTML5 / UWP | lib | — | 就位 |

- `L` 后缀 = Logging 版本（开发期诊断），非 `L` = Release；由 `FMODStudioSettings` 的 `LoggingLevel` / 构建配置切换。
- 附带 **Resonance Audio** 空间音频插件（`FMODUnityResonance` asmdef）。

---

## 程序集边界（asmdef）

| asmdef | 类型 | 说明 |
|--------|------|------|
| `FMODUnity` | Runtime | 运行时 API（`RuntimeManager` / `EventInstance` / `fmod.cs` 绑定） |
| `FMODUnityEditor` | Editor | 仅编辑器（Settings、EventManager、Bank 刷新）——不打包 |
| `FMODUnityResonance` / `…Editor` | Runtime / Editor | Resonance 空间音频 |

- CombatCore 通过 `IDSPClock` / `IAudioPlayer` 接口消费 FMOD，**不直接 `using FMODUnity`**（保持边界隔离，与 Feel/DOTween 同原则）。

---

## Integration Rules

1. **DSP 时钟单一真值**：所有节奏时序基于 FMOD DSP 时钟（经 `IDSPClock.GetDspTime()`），禁各自采时；判定禁用 `Update()` 轮询时间戳（用 Input System callback 时间戳）。
2. **禁止用 pitch 变速换 BPM**：FMOD `Channel.setPitch()` 会使编钟等中国传统乐器失调，违反支柱三（华风沉浸）。BPM 切换走拍号事件 + 边界对齐调度。
3. **禁止 `AudioSettings.outputSampleRate` / `AudioSettings.dspTime`**：采样率取 `getSoftwareFormat()`；时序取 payload 的 `beat_dsp_time`。
4. **禁止改 FMOD 源码**：CS0618 等 FMOD 自带警告静待官方升级；扩展经继承 / 事件（同 TDE 边界原则）。
5. **banks 数据流**：FMOD Studio Build banks → `Build/Desktop`（或 `SourceBankPath`）→ `EventManager` 缓存 → `TargetAssetPath: FMODBanks`；`AutomaticEventLoading: 1` 已启用。

---

## Known Constraints

1. **banks 前置**：无已构建 banks 时 `EventManager.Startup()` 抛 `DirectoryNotFoundException`（见 M0 Spike 表）。接入音频内容前必须先 Build banks。
2. **license 与 CI**：headless batchmode 需有效 Unity license 实体；FMOD 编译门控走 `game-ci/unity-test-runner`，本地 batchmode 不作 CI 依据。
3. **非 UPM 包**：FMOD 以 `Assets/Plugins/FMOD` 直接导入（非 Package Manager），**须手动锁版本**；升级须同步 `platforms/*/lib` 原生二进制与 `src/` C# 绑定。
4. **`getDSPClock()` 挂载点**：取自 **master channel group**，不是 `FMOD.System`——LLM 高频记错点。
5. **CI 精度验证降级**：headless runner 无音频设备，`< 0.1ms` 时间戳精度用 `MockDSPClock` 路径覆盖（EditMode），真机 FMOD 精度作手工 QA 证据（见 `audio-beat-clock.md` AC-F2-07b）。

---

## ADR References

| ADR | 涉及 FMOD 的决策 |
|-----|-----------------|
| ADR-D6 Audio Middleware | 选定 FMOD Studio 2.x（否决 Unity Audio DSP）；含线程模型 Addendum（RuntimeManager + 主线程轮询，否决 `OnAudioFilterRead`） |
| ADR-D8 DI/测试缝隙契约 | 归属 `IDSPClock` / `IAudioPlayer` 接口 + Mock 契约，解锁 CombatCore 单测 |
| ADR-D10 Bootstrap Sequencing | `LatencyOffsetMs` 写者（F6 boot-inject / FE3 运行期校准）主线程排序 |

---

## Upgrade History

| Date | From | To | Notes |
|------|------|----|-------|
| 2026-07-16 | — | 2.02.35 | 初始集成（`Assets/Plugins/FMOD`，含全平台原生库 + Resonance） |
| 2026-08-12 | 2.02.35 | 2.02.35 | M0 Spike 编译期二进制确认（本文档）——C# 编译 ✅ / 原生库加载 ✅ / banks 待构建 ⚠️ |
