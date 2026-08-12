# Epics Index

Last Updated: 2026-08-12
Engine: Unity 6.3 LTS (6000.3.19f1)

| Epic | Layer | System | GDD | Stories | Status |
|------|-------|--------|-----|---------|--------|
| F2 音频/拍点时钟系统 | Foundation | F2 BeatClock | design/gdd/audio-beat-clock.md | 14 stories | Ready |
| F3 Input 事件系统 | Foundation | F3 InputEventBus | design/gdd/input-event-system.md | 8 stories | Ready |
| F4 ScriptableObject 数据层 | Foundation | F4 SO 数据层 | design/gdd/so-data-layer.md | 8 stories | Ready |
| F5 存档/持久化系统 | Foundation | F5 SaveService | design/gdd/save-persistence.md | 8 stories | Ready |
| F6 TDE & Animancer 集成层 | Foundation | F6 TDELifecycleAdapter | design/gdd/tde-integration.md | 12 stories | Ready |

## Notes

- **审查模式**: lean（PR-EPIC producer 结构门禁本轮跳过）
- **治理 ADR 状态**: F2–F6 全部治理 ADR 均已 Accepted（ADR-D1/D4/D5/D6/D7/D8/D10/D12/D13）
- **实装前硬门禁**: F6 TR-tde-014（TDE 胜利/通关事件成员名核对）——胜利桥接相关 story 标 Blocked 直至核对完成
- **覆盖亮点**: F4 Gap = 0（ADR-D4 全覆盖）；F2/F3/F5 的 Gap 均为「GDD 即规范」预期内部项，非真实 ADR 缺口
