---
name: Jade Rabbit Warsong — Core Design Context
description: Core design decisions, open questions, and system architecture for 玉兔:破阵 (Jade Rabbit: Warsong) — rhythm-action roguelike
type: project
---

Genre: Rhythm Action Roguelike (节奏动作 Roguelike)
Engine: Unity 6.3 LTS, URP, C#
Platform: PC Steam, full gamepad support
Core Fantasy: "战斗即演奏，演奏即战斗" — Combat IS Performance

**5 Core Systems (none have GDDs yet as of 2026-07-13):**
1. 节奏量化攻击系统 (Rhythm Quantization Attack System)
2. 连招图系统 (Combo Graph System — ScriptableObject-driven)
3. 心法构筑系统 (Perk Build System — 3-choice room rewards)
4. 节奏化敌人 AI (Rhythmic Enemy AI — CombatDirector token/slot)
5. 局外成长系统 (Meta-Progression — Merit currency, PlayerInfoSO)

**Unresolved Foundational Decisions (must resolve before system GDDs):**
- Quantization variant: Off / HalfBeat / Beat — deferred to M2 playtesting, but SHOULD be resolved by prototype spike first
- Cancel window type: rhythm-gated vs duration-based vs rating-gated — determines if combo and rhythm are one system or two
- Rating-to-consequence contract: Miss damage value, whether Miss breaks combo, Good combo advance — undefined
- CombatDirector token/slot spec: token count, slot allocation logic, overflow behavior
- Minimum intrinsic Perfect hit feedback (kinetic level, not just UI)

**Why:** These were identified in the first design review (2026-07-13) as blocking all downstream system GDD authoring.

**How to apply:** When helping author any system GDD, always check these 5 decisions first. If any remain unresolved, surface them before drafting that GDD.
