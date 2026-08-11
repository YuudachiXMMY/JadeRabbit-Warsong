# Test Infrastructure

**Engine**: Unity 6.3 LTS (6000.3.19f1)
**Test Framework**: Unity Test Framework (UTF) + NUnit
**CI**: `.github/workflows/tests.yml`
**Setup date**: 2026-08-11

## Directory Layout

Unity 只编译 `Assets/` 下的 C# 代码，因此**编译型测试代码**与**文档型脚手架**分开放置：

```
Assets/Tests/            # 编译型测试代码（Unity 硬约束：必须在 Assets/ 下）
  EditMode/              # 纯逻辑单元测试（公式、状态机、数据校验）— 不进入 Play 模式
    EditModeTests.asmdef
    ExampleEditModeTest.cs
  PlayMode/              # 集成测试（跨系统、物理、协程）— 运行于真实场景
    PlayModeTests.asmdef
    ExamplePlayModeTest.cs

tests/                   # 文档型脚手架（非编译）
  smoke/                 # /smoke-check 读取的关键路径清单
  evidence/              # 截图日志与人工签核记录
  README.md              # 本文件
```

> **注意**: 项目 `coding-standards.md` 的证据矩阵引用 `tests/unit/`、`tests/integration/`
> 等路径。在 Unity 中，实际的自动化单元/集成测试代码位于 `Assets/Tests/EditMode`
> 与 `Assets/Tests/PlayMode`（Unity 编译器要求）。`tests/` 仅承载文档与冒烟清单。

## Running Tests

### 编辑器内

```
Window → General → Test Runner → EditMode / PlayMode → Run All
```

Unity Test Framework 在 Unity 2019+ 默认内置，无需额外安装。

### 命令行（本地无头运行）

```bash
"/Applications/Unity/Hub/Editor/6000.3.19f1/Unity.app/Contents/MacOS/Unity" \
  -runTests -batchmode -projectPath . \
  -testPlatform EditMode -testResults ./test-results/editmode.xml
```

将 `-testPlatform` 改为 `PlayMode` 运行集成测试。

## Test Naming

- **Files**: `[System]_[Feature]_Test.cs`（示例文件为通用 `Example*Test.cs`）
- **Methods**: `test_[scenario]_[expected]`
- **示例**: `Combat_Damage_Test.cs` → `test_base_attack_returns_expected_damage()`

## Story Type → Test Evidence

| Story Type | Required Evidence | Location |
|---|---|---|
| Logic（公式、AI、状态机） | 自动化单元测试 — 必须通过 | `Assets/Tests/EditMode/[System]/` |
| Integration（多系统） | 集成测试 OR 记录在案的 playtest | `Assets/Tests/PlayMode/[System]/` |
| Visual/Feel（动画、VFX、手感） | 截图 + lead 签核 | `tests/evidence/` |
| UI（菜单、HUD、屏幕） | 人工走查文档 OR 交互测试 | `tests/evidence/` |
| Config/Data（数值调优） | 冒烟检查通过 | `production/qa/smoke-*.md` |

## CI

测试在每次 push 到 `main` 与每个 PR 上自动运行。测试套件失败将阻塞合并。

> **前置**: Unity CI 需要在 GitHub 仓库 Secrets 中配置 `UNITY_LICENSE`
> （首次 CI 运行前手动添加）。参见 game-ci 文档。

## ADR-D8 缝隙测试待办

当 `CombatCore` 程序集落地后，完成纯 C# NUnit 缝隙测试 spike：

1. 在 `Assets/Tests/EditMode/EditModeTests.asmdef` 的 `references` 中加入 `"CombatCore"`
2. 在 CombatCore 侧添加 `[assembly: InternalsVisibleTo("EditModeTests")]`
3. 为五个 DI 接口（IBeatClock/IInputEventBus/IComboCounter/IRandomSource/IDSPClock）编写 Mock 契约测试
4. 通过后将 ADR-D8 从 Proposed 推进至 Accepted，解锁 C1-C4 的 80% 单测硬门
