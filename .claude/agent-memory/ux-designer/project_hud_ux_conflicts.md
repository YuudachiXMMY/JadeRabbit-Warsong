---
name: HUD UX Conflicts — Art Bible Ch.7 Alignment Review
description: Open UX conflicts identified during art bible ch.7 pre-review; must resolve before authoring HUD spec
type: project
---

Review date: 2026-07-13
Status: Open — pending art-director + UX joint review before ch.7 authoring

**Why:** These conflicts were identified before ch.7 was authored. Resolving them first prevents rework.

**How to apply:** Before writing design/ux/hud.md or contributing to art-bible ch.7, confirm resolution status of all four conflicts below.

## C-01 — HP Bar Location (HIGH)
Bamboo scroll HP bar placed at screen edge requires large eye travel away from character.
Proposed fix: Character-proximate short HP bar (1.5× character width), retaining chamfer language but not full 4:1 bamboo proportion.
Needs: Art Director sign-off on breaking full bamboo proportion for HP bar.

## C-02 — Rhythm Ring as Sole Beat Indicator (HIGH)
Azure rhythm ring alone cannot carry both "current beat position" and "judgment window width" for new players while they manage combos + dodge + enemies.
Reference: GDD explicitly cites Crypt of the NecroDancer's steep learning curve as a warning.
Proposed fix: Add a fixed-position auxiliary beat line (e.g., thin azure line + moving cursor, bottom-center screen) to offload beat-position reading from the ring. Ring retains "judgment window width" via gap angle.
Needs: Art Director confirmation that auxiliary beat line fits art bible visual language.

## C-03 — BPM Breathing on Numeric Containers (HIGH)
BPM scale animation on digit containers (HP number, combo count, cooldown timers) increases read time at high BPM (160 BPM = very fast pulse). Violates Fitts's Law for time-critical readout.
Resolution: Explicitly exclude HP value, combo number, cooldown timers from BPM scale animation. BPM breathing applies only to: rhythm ring, screen edge halo, interactive-object azure border, gamepad focus border.

## C-04 — Bamboo 4:1 Ratio for Skill Cards (MEDIUM)
Horizontal 4:1 bamboo proportion compresses content area when three skill cards are displayed side by side, especially at 720p and mobile.
Proposed fix: Allow vertical bamboo variant (1:1.4 ratio) for skill cards. Chamfer language retained.
Needs: Art Director sign-off on vertical bamboo variant.
