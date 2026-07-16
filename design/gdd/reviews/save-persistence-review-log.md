# Review Log: F5 存档/持久化系统 (Save/Persistence System)

---

## Review — 2026-07-14 (Fourth Review) — Verdict: APPROVED
Scope signal: L
Specialists: `systems-designer`, `unity-specialist`, `qa-lead`, `game-designer`, `creative-director`
Blocking items: 5 | Recommended: 2
Summary: 第四轮审查发现 5 项阻塞项并已当场修订：B1 AcknowledgeError 状态机矛盾——状态表拆分为两行（WriteFailed 保留缓存 vs Corrupted/DirectoryFailed/VersionTooHigh 重置为默认值）并更新两级错误模型注记；B2 单一 CurrentSaveVersion 常量与"各 DTO 独立版本"规则冲突——全文替换为 per-DTO 常量（PlayerProgressDTO.CurrentVersion 等），更新状态机、Edge Cases、Tuning Knobs、AC、Open Questions；B3 SlotState.Corrupted 注释暗示 F5 提供恢复 API——改为明确写明仅提供删除选项；B4 RenameFile 碰撞语义缺失——Rule 11 代码块后补充静默覆盖和 DeleteFile 幂等语义；B5 Open Question 4 触发点模糊且未承认与 Rule 3 的矛盾——补充家庭共用场景设计张力说明，触发点改为「MVP Playtest 首次收到家庭共用校准冲突报告」。另同步修订 R3（AC-F5-09 可观察断言替换"文件未修改"）和 R4（AC-F5-22/23 编号顺序修正）。
Prior verdict resolved: Yes — 第三轮 5 项阻塞项已全部解决

---

## Review — 2026-07-14 (Third Review) — Verdict: NEEDS REVISION → Revised In-Session
Scope signal: L
Specialists: `systems-designer`, `unity-specialist`, `qa-lead`, `game-designer`, `creative-director`
Blocking items: 5 | Recommended: 7
Summary: 第三轮审查发现 5 项阻塞项并已当场修订：B1 MonoBehaviour 构造函数注入改为 InjectStorage() 方法注入、B2 totalPlayTimeSeconds 由 float 改为 double（静默精度退化）、B3 状态机建立两级错误模型（槽位损坏→Idle vs 全局损坏→Error）并更新 AC-F5-07、B4 AC-F5-08 删除错误"两次写操作"断言改为可观察行为验证、B5 新增 AC-F5-19 到 AC-F5-22（边界防御）。同步修订 W7（Dependencies 节 SEO 矛盾）和 W10（Open Question 6 幽灵 API 措辞）。建议 /clear 后重新 /design-review 验证。
Prior verdict resolved: Yes — 第二轮 7 项阻塞项已全部解决

---

## Review — 2026-07-14 (Re-review) — Verdict: NEEDS REVISION → Revised In-Session

Scope signal: L
Specialists: `systems-designer`, `unity-specialist`, `qa-lead`, `game-designer`, `creative-director`
Blocking items: 7 | Recommended: 4
Summary: 第二轮审查发现 7 项阻塞项：IFileStorage 接口缺失（ES3 静态 API 不可 mock）、Domain Reload 静态字段重置未覆盖、非主线程 Debug.Log 方案矛盾、OnSaveError 事件契约与 WriteFailed UX 完全缺失、AC-F5-06 引用已废弃字段 `isEmpty`、AC-F5-08 的 V0 schema 未定义导致测试不可执行、AC-F5-12 断言矛盾（exception + LogError 不可同时成立）。本轮审查同步完成修订：补充 Rule 11-14（IFileStorage、Domain Reload reset、主线程捕获、Awake 初始化）、新增 OnSaveError 事件契约节（SaveErrorType/SaveErrorData/P7 委托规格）、修复高版本 Edge Case 触发路径、更正所有 API 名称（SaveLatencyCalibration）、重写 AC-F5-06/08/12、新增 AC-F5-17/18 及 Open Questions 5-6。
Prior verdict resolved: Yes — 首轮 6 项阻塞项已在 2026-07-14 第一轮修订中全部解决

---

## Review — 2026-07-14 — Verdict: MAJOR REVISION NEEDED → Revised

Scope signal: L
Specialists: `systems-designer`, `unity-specialist`, `qa-lead`, `game-designer`, `creative-director`
Blocking items: 6 | Recommended: 7
Summary: F5 文档结构完整（8/8 节），Player Fantasy 定位精准，DTO 分层架构方向正确。但 API 层存在 6 个阻塞项：`SaveLatencyOffset(float)` 无法写入 `isCalibrated` 字段、`SetActiveSlotIndex` 和 `DeleteSlot` API 完全缺失、`global.es3` 三 DTO 共享单一版本号（结构性缺陷）、F2 注入 latencyOffsetMs 的责任方空白、AC-F5-12 使用 OR 导致测试无法写成确定性断言。本轮审查同步完成修订：API 改为 `SaveLatencyCalibration(LatencyCalibrationDTO)`、global 拆分为三个独立文件、补充 Rule 9/10 和 AC-F5-13~16 等。
Prior verdict resolved: N/A — 首次审查
