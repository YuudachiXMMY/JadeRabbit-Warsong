# Directory Structure

```text
/
├── CLAUDE.md                    # Master configuration
├── .claude/                     # Agent definitions, skills, hooks, rules, docs
├── Assets/                      # Unity project root — ALL compiled code and assets live here
│   ├── Scripts/                 # All C# game code
│   │   ├── Core/                # Core systems (state machine, event bus, singletons)
│   │   ├── Gameplay/            # Gameplay systems (combat, movement, abilities)
│   │   ├── AI/                  # AI systems (behavior trees, pathfinding)
│   │   ├── UI/                  # UI logic (screens, HUD, menus)
│   │   ├── Networking/          # Netcode (if applicable)
│   │   └── Utils/               # Shared utilities and helpers
│   ├── Editor/                  # Editor extensions and custom inspectors (excluded from builds)
│   ├── Prefabs/                 # Prefab assets
│   ├── Scenes/                  # Scene files (.unity)
│   ├── Materials/               # Materials and physical materials
│   ├── Art/                     # Art assets (models, textures, sprites, animations)
│   ├── Audio/                   # Audio clips (SFX, music, ambient)
│   ├── Shaders/                 # Shader files (.shader, .shadergraph)
│   ├── VFX/                     # Visual effects (VFX Graph, particle systems)
│   ├── Data/                    # ScriptableObject configs and data tables
│   ├── Settings/                # URP/HDRP settings, quality profiles
│   ├── Tests/                   # Unity Test Runner tests (NUnit, EditMode/PlayMode)
│   │   ├── EditMode/            # Edit-mode unit tests
│   │   └── PlayMode/            # Play-mode integration tests
│   └── Resources/               # Resources folder (use sparingly — prefer Addressables)
├── Packages/                    # Unity Package Manager manifests
├── ProjectSettings/             # Unity project settings (version-controlled)
├── design/                      # Game design documents (gdd, narrative, levels, balance)
├── docs/                        # Technical documentation (architecture, api, postmortems)
│   └── engine-reference/        # Curated engine API snapshots (version-pinned)
├── tools/                       # Build and pipeline tools (ci, build, asset-pipeline)
├── prototypes/                  # Throwaway prototypes (isolated from Assets/)
└── production/                  # Production management (sprints, milestones, releases)
    ├── session-state/           # Ephemeral session state (active.md — gitignored)
    └── session-logs/            # Session audit trail (gitignored)
```

**Unity 特有约束：**
- 所有 C# 游戏代码**必须**在 `Assets/` 下，否则 Unity 编译器不会编译
- `Editor/` 文件夹中的代码不会打包到最终构建
- `Resources/` 目录中的资产全部打包——优先使用 Addressables 管理资产加载
- `Tests/` 需要对应的 `.asmdef` 引用 `UnityEngine.TestRunner` 和 `UnityEditor.TestRunner`
