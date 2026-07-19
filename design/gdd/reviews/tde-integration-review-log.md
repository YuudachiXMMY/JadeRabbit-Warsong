# Review Log: F6 — TopDownEngine & Animancer 集成层

---

## Review — 2026-07-19 — Verdict: APPROVED
Scope signal: L
Specialists: unity-specialist, qa-lead, systems-designer, game-designer, creative-director
Blocking items: 4 | Recommended: 5
Summary: 第三轮审查发现 3 个引擎级阻塞项（authoredBpm=0 运行时无保护产生 Infinity 静默失效；VerifyFMODAsyncMode() 协程缺 `_isVerifyingFMOD` 防重入保护致 BeatClock.Play() 可被双调用；LevelConfigSO 在 DontDestroyOnLoad 上的获取路径未禁止 FindObjectOfType）及 1 批次 AC 可测试性缺陷（AC-F6-01/03/08-10/12/15/18/21 描述的测试方法不可独立执行）。所有 4 个阻塞项及 5 项推荐修订已在同 session 内完成；Creative Director 确认 4 条验证标准全部达标，无需第四轮全面审查；F6 GDD 直接进入 Approved 状态，C1 GDD 编写解锁。
Prior verdict resolved: Yes — 前两轮各 8/4 个 blocker 均已确认解决

---

## Review — 2026-07-18 — Verdict: MAJOR REVISION NEEDED → Revised (Pending Re-review)
Scope signal: L
Specialists: unity-specialist, qa-lead, game-designer, systems-designer, creative-director
Blocking items: 8 | Recommended: 4
Summary: GDD 的 Animancer 架构（Layer 0/1 权重模型）与已 Accepted 的 ADR-D5 PlayController 模型根本性不一致；NOMINAL_BPM 全局常量设计被替换为 per-node `AttackNodeSO.AuthoredBpm`；Root Motion API 断言目标错误（`AnimancerState` → `Animator`）；BGM 过渡策略、ConfirmDSPStart 协议、FMOD 异步模式验证三处内容完全缺失；NOMINAL_BPM 安全范围数学推导错误（[60,180] → [96,160]）。所有 8 个阻塞项已在同 session 内修订完成；新增 AC-F6-19 至 AC-F6-22 并解决 OQ-02。用户明确选择 Option A（攻击中途实时更新 BPM 速度），并安排 ADR-D5 接受修订说明。
Prior verdict resolved: First review — 修订后待 re-review

---

## Review — 2026-07-18 — Verdict: NEEDS REVISION → Revised (Pending Re-review)
Scope signal: L
Specialists: unity-specialist, qa-lead, game-designer, systems-designer, creative-director
Blocking items: 4 | Recommended: 9
Summary: 第二轮审查发现 4 个新阻塞项：Core Rule 12 `Start()` 单帧生命周期无法跨帧采样 DSP 时间（改为 `VerifyFMODAsyncMode()` 协程 + `yield return null`）；BGM 过渡调用 `IAudioPlayer.PlayScheduled()` 为 F2 内部测试接口悬空引用（改为 `_beatClock.ScheduleBGMTransition()` 公开方法，F2 GDD 待增补）；EC-03 未记录 `UpdateBpm()` + `OnEnd` 同帧竞争及 C2 幂等性合约（已补充竞争警告块与强制接口合约）；AC-F6-19 未指明 IDSPClock 获取路径（明确为 `_beatClock.DSPClock` 只读属性）。所有 4 个阻塞项已在同 session 内修订完成；creative-director 将 2 个 game-designer Critical 项降级为 Recommended（per-node authoredBpm 架构已提供工具，为使用指导问题而非架构缺陷）。用户选择在新 Session 进行正式 re-review。
Prior verdict resolved: Yes — 首次 re-review；8 个历史 blocker 全部确认已解决，发现 4 个新 blocker 并在同 session 修订完成
