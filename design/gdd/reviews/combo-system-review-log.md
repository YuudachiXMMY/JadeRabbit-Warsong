# Review Log: C2 连招图系统 (combo-system.md)

---

## Review — 2026-08-10 — Verdict: MAJOR REVISION NEEDED

Scope signal: L
Specialists: game-designer, systems-designer, qa-lead, gameplay-programmer, unity-specialist, creative-director（full 模式对抗式审查）
Blocking items: 13 | Recommended: 6
Prior verdict resolved: First review（枚举不匹配曾于 gdd-cross-review-2026-07-15.md W1 flag，属跨文档回归）

### Summary

C2 首次 full 审查。结构完整（8/8 章节），架构方向正确，公式数值（combo_multiplier 采样表、148 触顶）、F6 perk 输入隔离、段数软重置边界合约均经复核通过——非推倒重来。但存在约 13 项阻塞，其中两项与已 Accepted 的 ADR 正面矛盾：① AC-C2-33/EC9（连招中途实时重算 Speed）撞 ADR-D5 的 MVP 不改 Speed 约束；② AnimationClip 预加载/释放合约整章缺席，违背 ADR-D4 Rule D4-4 指派给 C2 的所有权。另有 systems-designer 核实的硬性事实错误——F2 示例 rating_multiplier=1.5 越界 [0.5,1.0]，且该错误数字被明文用作 Boss HP 调参基准，污染下游平衡。最威胁核心幻想的是 P3 双时钟未证明对齐：节奏 Perfect 的输入可能因动画未到 cancelWeight 被 EC5 静默回根，而 Feel 反馈仍显示成功，直接击穿手感支柱二。枚举 AttackType vs AttackInputType 不匹配无法编译（三周前回归）。qa-lead 标出一批 AC 错位/不可测/flaky（AC-22/17/37/04）及缺 IAttackPlaybackProvider 测试接缝。creative-director 裁决：先答 P3 时钟对齐设计题 → technical-director 裁 P2a → C2 自闭 P1/P4/P2b/P2c/P5 → 复审；一旦 P3 证明只是 authoring 约束，将向 NEEDS REVISION 收敛。专家分歧记录：gameplay-programmer F6 修正「buffer 固定/窗口缩放」误设——二者均与 currentBpm 成反比、纯 BPM 变化不扭曲比例，真正风险重定位为亚帧地板缺失 + 全局/逐节点粒度不一致。用户选择另开会话修订。

---
