# Smoke Test: Critical Paths

**Purpose**: 在任何 QA 交接前，用 15 分钟内跑完这 10-15 项检查。
**Run via**: `/smoke-check`（读取本文件）
**Update**: 每当新核心系统实现时，追加对应条目。

## Core Stability（始终运行）

1. 游戏启动到主菜单无崩溃
2. 可从主菜单开始新游戏 / 新会话
3. 主菜单响应所有输入不卡死（键鼠 / 手柄 d-pad / 触摸）

## Core Mechanic（每 sprint 更新）

<!-- 每个 sprint 的主要机制实现后在此追加 -->
<!-- 示例: "玩家可移动、攻击，攻击判定随 BPM 变速保持节奏手感" -->
4. [主机制 — 首个核心系统（节奏判定 / 连招）实现后更新]

## Data Integrity

5. 存档完成无错误（EasySave 3 存档系统实现后）
6. 读档恢复正确状态（含延迟校准偏移量、功勋、武器解锁）

## Performance

7. 目标硬件无可见掉帧（PC/Console 60fps，Mobile 30fps）
8. 核心循环运行 5 分钟无内存增长（核心循环实现后）
