---
name: Jade Rabbit Warsong — UX Project Context
description: Core game mechanics, HUD requirements, and UX constraints for 玉兔:破阵; use as baseline for all UX decisions
type: project
---

Game: 玉兔:破阵 (Jade Rabbit: Warsong)
Genre: 节奏动作 Roguelike, 2:1 等距3D, PC优先（键鼠+手柄+触控）

**Why:** Every UX decision must serve "rhythm IS combat" — beat precision, combo readability, and judgment feedback are the primary feedback loops.

**How to apply:** When evaluating HUD layout, always ask: can the player read HP + combo count + current beat position without breaking focus on the character and enemies?

## HUD Information Requirements (MVP)
1. HP (current/max)
2. Combo count (real-time, core of rhythm feedback)
3. Rhythm indicator (current beat position, for landing attacks on beat)
4. Skill slots (equipped skills + cooldown state)
5. Judgment feedback (Perfect/Good/Miss text + VFX)
6. Boss HP (boss fights only)

## Visual Constraints (from Art Bible ch.1-6)
- Panel shape: bamboo scroll, 45° chamfer, NO rounded corners, NO circular buttons
- Azure rhythm ring: surrounds player, breathes with BPM (±4px at BPM 80, ±12px at BPM 160)
- Semantic three-color system: Crimson #9E2F2C (danger/attack), Azure #1685A9 (info/rhythm), Gold #FFD700 (reward/Perfect)
- Font: 汉仪尚巍手书 (titles) + 思源宋体 (body/numbers)
- UI panel: Paper-White #F5F0E8 @ 85% opacity; rises to 95% when background ink density > 65%
- Gamepad focus state: Azure border + BPM breathing flicker

## Judgment System
- Perfect (Gold): largest font, full-screen brightness peak 1 frame, bell SFX
- Good (Azure): medium font, rhythm ring light flash
- Miss (Crimson): medium font, screen edge crimson wash + dull SFX
- All three always show text label (colorblind safety fallback)

## Platforms
- PC (primary): keyboard/mouse + gamepad full-feature
- Console: full d-pad navigation required for all UI
- Mobile: touch, no hover equivalents allowed
