# Unity 6.3 LTS — Breaking Changes

**Last verified:** 2026-07-13

This document tracks breaking API changes and behavioral differences between Unity 2022 LTS
(likely in model training) and Unity 6.3 LTS (current version). Organized by risk level.

## HIGH RISK — Will Break Existing Code

### Entities/DOTS API Complete Overhaul
**Versions:** Entities 1.0+ (Unity 6.0+)

```csharp
// ❌ OLD (pre-Unity 6, GameObjectEntity pattern)
public class HealthComponent : ComponentData {
    public float Value;
}

// ✅ NEW (Unity 6+, IComponentData)
public struct HealthComponent : IComponentData {
    public float Value;
}

// ❌ OLD: ComponentSystem
public class DamageSystem : ComponentSystem { }

// ✅ NEW: ISystem (unmanaged, Burst-compatible)
public partial struct DamageSystem : ISystem {
    public void OnCreate(ref SystemState state) { }
    public void OnUpdate(ref SystemState state) { }
}
```

**Migration:** Follow Unity's ECS migration guide. Major architectural changes required.

---

### Input System — Legacy Input Deprecated
**Versions:** Unity 6.0+

```csharp
// ❌ OLD: Input class (deprecated)
if (Input.GetKeyDown(KeyCode.Space)) { }

// ✅ NEW: Input System package
using UnityEngine.InputSystem;
if (Keyboard.current.spaceKey.wasPressedThisFrame) { }
```

**Migration:** Install Input System package, replace all `Input.*` calls with new API.

---

### URP/HDRP Renderer Feature API Changes
**Versions:** Unity 6.0+

```csharp
// ❌ OLD: ScriptableRenderPass.Execute signature
public override void Execute(ScriptableRenderContext context, ref RenderingData data)

// ✅ NEW: Uses RenderGraph API
public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
```

**Migration:** Update custom render passes to use RenderGraph API.

---

## MEDIUM RISK — Behavioral Changes

### Addressables — Asset Loading Returns
**Versions:** Unity 6.2+

Asset loading failures now throw exceptions by default instead of returning null.
Add proper exception handling or use `TryLoad` variants.

```csharp
// ❌ OLD: Silent null on failure
var handle = Addressables.LoadAssetAsync<Sprite>("key");
var sprite = handle.Result; // null if failed

// ✅ NEW: Throws on failure, use try/catch or TryLoad
try {
    var handle = Addressables.LoadAssetAsync<Sprite>("key");
    var sprite = await handle.Task;
} catch (Exception e) {
    Debug.LogError($"Failed to load: {e}");
}
```

---

### Physics — Default Solver Iterations Changed
**Versions:** Unity 6.0+

Default solver iterations increased for better stability.
Check `Physics.defaultSolverIterations` if you rely on old behavior.

---

## LOW RISK — Deprecations (Still Functional)

### UGUI (Legacy UI)
**Status:** Deprecated but supported
**Replacement:** UI Toolkit

UGUI still works but UI Toolkit is recommended for new projects.

---

### Legacy Particle System
**Status:** Deprecated
**Replacement:** Visual Effect Graph (VFX Graph)

---

### Old Animation System
**Status:** Deprecated
**Replacement:** Animator Controller (Mecanim)

---

## Platform-Specific Breaking Changes

### WebGL
- **Unity 6.0+**: WebGPU is now the default (WebGL 2.0 fallback available)
- Update shaders for WebGPU compatibility

### Android
- **Unity 6.0+**: Minimum API level raised to 24 (Android 7.0)

### iOS
- **Unity 6.0+**: Minimum deployment target raised to iOS 13

---

## Migration Checklist

When upgrading from 2022 LTS to Unity 6.3 LTS:

- [ ] Audit all DOTS/ECS code (complete rewrite likely needed)
- [ ] Replace `Input` class with Input System package
- [ ] Update custom render passes to RenderGraph API
- [ ] Add exception handling to Addressables calls
- [ ] Test physics behavior (solver iterations changed)
- [ ] Consider migrating UGUI to UI Toolkit for new UI
- [ ] Update WebGL shaders for WebGPU
- [ ] Verify minimum platform versions (Android/iOS)

---

---

## Unity 6.3 LTS — Specific Breaking Changes (6.1 → 6.3)

### HIGH: URP Compatibility Mode Removed
**Version:** 6.3 LTS

URP Compatibility Mode (deprecated in 6.0) is now **completely removed**. The
`RenderGraphSettings.enableRenderCompatibilityMode` property is now read-only (returns `false`).

```csharp
// ❌ No longer works — Compatibility Mode is stripped from code by default
RenderGraphSettings.enableRenderCompatibilityMode = true; // compile error

// ✅ Must use Render Graph API
public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData) { }
```

If you have Compatibility Mode code, add `URP_COMPATIBILITY_MODE` scripting define to Player
Settings to re-enable it — but this is not supported for shipping. Migrate to Render Graph.

---

### HIGH: `[SerializeField]` Now Restricted to Fields Only

```csharp
// ❌ Compile error in 6.3 — attribute applied to a property
[SerializeField]
public int Health { get; set; }  // ERROR: CS0592

// ✅ Correct usage — field only
[SerializeField]
private int _health;
```

---

### MEDIUM: `Physics.autoSyncTransforms` Deprecated

```csharp
// ❌ Deprecated in 6.3
Physics.autoSyncTransforms = false;

// ✅ Use explicit sync
Physics.SyncTransforms();
```

---

### MEDIUM: `UnityWebRequest` Defaults to HTTP/2

HTTP/2 is now the default protocol on Android, Linux, macOS, PS4, PS5, UWP, and Windows.
If your server does not support HTTP/2, you may see connection failures. Test your endpoints
and downgrade explicitly if needed.

---

### MEDIUM: Netcode for GameObjects 1.x Deprecated

```csharp
// ❌ Netcode 1.x API (deprecated)
using Unity.Netcode;
// older NetworkVariable patterns

// ✅ Migrate to Netcode 2.x — see official migration guide
// https://docs-multiplayer.unity3d.com/netcode/current/upgrade-guide/
```

---

### LOW: `AccessibilityNode.selected` → `AccessibilityNode.invoked`

```csharp
// ❌ Deprecated (will be obsolete in future release)
node.selected = true;

// ✅ New name
node.invoked = true;
```

---

### LOW: Android Adaptive Icons Required

Round and legacy Android icons are deprecated. Use **Adaptive Icons** only.
`PlayerSettings.Android.androidIsGame` is obsolete — use the **App Category** Player setting instead.

---

### LOW: Search Index Manager Removed

`Windows > Search > Index Manager` has been removed.
Go to `Preferences > Search > Indexing` instead.

---

**Sources:**
- https://docs.unity3d.com/6000.0/Documentation/Manual/upgrade-guides.html
- https://docs.unity3d.com/Packages/com.unity.entities@1.3/manual/upgrade-guide.html
- https://docs.unity3d.com/6000.3/Documentation/Manual/UpgradeGuideUnity63.html
- https://discussions.unity.com/t/planned-breaking-changes-in-unity-6-3/1646418
