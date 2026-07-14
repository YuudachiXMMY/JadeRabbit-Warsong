# Odin Inspector & Validator (Sirenix) — Plugin Reference

| Field | Value |
|-------|-------|
| **Publisher** | Sirenix |
| **Products** | Odin Inspector + Serializer / Odin Validator |
| **Version** | 4.0.2.1 |
| **Verified with Unity** | 6.3 LTS (6000.3.19f1) |
| **Asset Store** | https://assetstore.unity.com/packages/tools/utilities/odin-inspector-and-serializer-89041 |
| **Official Docs** | https://odininspector.com/documentation |
| **Last Verified** | 2026-07-13 |
| **LLM Knowledge Gap** | LOW — Odin API 较稳定，但 Unity 6 序列化新特性可能影响 `[OdinSerialize]` 行为 |

---

## Purpose in This Project

### Odin Inspector
- **ScriptableObject 可视化编辑**: AttackNodeSO、PerkSO、WeaponSO、TrackSO、EnemyInfoSO
  等 SO 资源在 Inspector 中使用 Odin Attribute 增强可读性和编辑安全性。
- **编辑器工作流加速**: 列表排序、内联预览、条件显示字段、自定义按钮——
  减少手动脚本的编辑器工具开发时间。

### Odin Validator
- **SO 数据完整性验证**: 确保所有 ScriptableObject 的必填字段在进入 PlayMode 或打包前已填写。
- **项目合规扫描**: 批量检查动画 Clip 引用是否存在、BPM 范围是否合法等。

---

## Critical Constraint: Editor-Only（最重要约束）

```
Odin Inspector 和 Odin Validator 仅在 Unity Editor 中可用。
Runtime 代码（打包后运行的代码）中禁止引用 Sirenix.* 命名空间。
```

**违反此约束的后果**: 打包时 Odin 的 Editor-only DLL 不会被包含，
导致 `TypeLoadException` 或编译失败。

### 正确使用模式

```csharp
// ✅ 正确 — 仅在 Editor 程序集（Editor/ 目录或 .asmdef Editor-only）中使用
#if UNITY_EDITOR
using Sirenix.OdinInspector;
#endif

// ✅ 正确 — Runtime ScriptableObject 只用标准 [SerializeField]
[SerializeField] private float _baseDamage;

// ✅ 正确 — Odin Attribute 在 Runtime 类上只用 #if 保护
#if UNITY_EDITOR
[InfoBox("伤害基础值，由 WeaponSO 配置", InfoMessageType.Info)]
#endif
[SerializeField] private float _baseDamage;

// ❌ 错误 — Runtime 代码直接 using Odin
using Sirenix.OdinInspector; // 会在打包时报错
```

---

## Recommended Attribute Usage（本项目推荐用法）

### 数据 ScriptableObject（AttackNodeSO 等）

```csharp
// 必填字段验证
[Required("必须指定攻击动画 Clip")]
public AnimationClip AttackClip;

// 范围限制 + 即时反馈
[Range(0.1f, 4f), SuffixLabel("beats")]
public float DurationBeats = 1f;

// 枚举说明
[EnumToggleButtons]
public AttackRating MinimumRatingToCancel;

// 列表内联预览
[InlineEditor]
public List<AttackNodeSO> ComboChain;
```

### Odin Validator 规则建议

在 Validator 配置中添加以下规则：
- SO 内所有 `[Required]` 字段不为 null
- `DurationBeats` 在 [0.1, 8] 范围内
- `AnimationClip` 引用实际存在（非 Missing Reference）

---

## Known Constraints

1. **Odin Serializer vs Unity Serializer**:
   如果使用 `[OdinSerialize]`（Odin 序列化器，支持接口、字典等），
   序列化数据格式与 Unity 原生不同，版本升级时可能出现反序列化问题。
   **建议**: MVP 阶段优先使用 Unity 原生 `[SerializeField]` + Odin 仅作 Attribute 标注；
   仅在确实需要序列化字典/接口时引入 `[OdinSerialize]`。

2. **Unity 6.3 LTS 中的 `[SerializeField]` 行为变更**:
   Unity 6.3 LTS 强制执行 `SerializeField` 字段必须显式标注（不再序列化公共字段）。
   Odin Validator 的某些自动检测规则可能与此冲突，首次配置时须验证。

3. **Odin Validator 误报**:
   Validator 对未赋值的 Optional 字段可能误报警告。须在 Validator 配置中将
   可选引用字段标记为 `[AssetsOnly]` 或配置例外规则。

---

## ADR References

无专项 ADR（Odin 是工具层，不影响游戏系统架构决策）。
若 Odin Serializer 被引入运行时，须补充 D-Serializer ADR。

---

## Upgrade History

| Date | From | To | Notes |
|------|------|----|-------|
| *(首次集成时填写)* | — | x.x.x | 初始集成 |
