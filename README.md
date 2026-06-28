# Unity Editor Tools

A collection of Unity editor utilities and shaders built for real-time interactive 3D installations. Tested on Unity 2021.3 LTS with URP.

---

## Folders

### AnimationBakers

Two editor windows that exist because of a fundamental FBX limitation: **FBX files do not export GameObject active/inactive state or material alpha values**. If your animation pipeline goes through FBX (Maya, Blender, Cinema 4D, etc.), these properties are silently dropped — objects that should appear and disappear stay on, and materials that should fade stay opaque. These bakers let you author those values visually in Unity and write them as proper animation curves into any AnimationClip.

- **VisibilityAnimationBaker** — Define ON/OFF frame ranges per GameObject; bakes `m_IsActive` curves into the clip. Supports multiple ranges per object, reorder/remove, and round-trips from existing clips.
- **MaterialFadeBaker** — Define fade-in/fade-out frame ranges per material; bakes `_Alpha` property curves into the clip. Auto-discovers all materials using the target shader.

Both tools persist their configuration in a ScriptableObject so it survives domain reloads and Unity restarts.

---

### BeamIn

A two-phase cinematic reveal effect for 3D meshes:

1. **Sweep phase** — a noise-edged band sweeps bottom-to-top, revealing a wireframe silhouette with an emissive edge glow
2. **Dissolve phase** — the mesh fades in with a noise dissolve, the real object visible underneath

All shader parameters are driven from `BeamInController.cs` — no need to touch the material. Call `Trigger()` to restart the effect at any time.

Also includes **BarycentricBaker** — an Editor tool that bakes barycentric coordinates into UV2. This is required by the BeamIn shader for per-triangle wireframe edge detection, since Unity does not expose barycentric coordinates natively.

**Files needed together:** `BeamIn.shader`, `BeamInController.cs`, `BeamInControllerEditor.cs`, `BarycentricBaker.cs`

---

### Dissolve

Noise-based dissolve shaders. Designed to run efficiently on mobile and well-suited for AR overlays where objects need to appear or disappear smoothly without hard cuts.

- **StandardDissolve.shader** — Standard PBR dissolve with noise texture and configurable edge color/width
- **BaseTransparent.shader** — Unlit transparent base with a single `_Alpha` property, useful for animated fades driven by animation curves
- **BaseTransparentShaderGUI.cs** — Custom inspector for BaseTransparent
- **DissolveController.cs** — Runtime MonoBehaviour to drive `_DissolveProgress` on a material from script or animation events
- **SwapToDissolveShader.cs** — Editor tool (`Tools > Dissolve > Swap Materials to StandardDissolve`) that batch-swaps all materials on a selected hierarchy to StandardDissolve, skipping duplicates and recording undo

---

### Hologram

A screen-space hologram shader with scanlines, fresnel rim glow, and a randomized glitch effect. Runs efficiently on mobile, making it a good fit for AR applications where you want a non-photorealistic "digital object" look without post-processing.

Includes a custom inspector (`HologramShaderGUI.cs`) with grouped controls.

---

### AssetCleanup

Batch project maintenance tools. All are Editor-only and have no runtime cost.

- **UnusedMaterialCleaner** — Scans all renderers in the open scene, then finds materials in a chosen folder that aren't referenced by any of them. Prompts per-material before deleting.
- **UnusedTextureCleaner** — Finds textures in a chosen folder that aren't referenced by any material in the project. Shows a list before bulk-deleting.
- **DuplicateImageMerger** — Groups images by filename + file size, counts GUID references across all serialized files (`.mat`, `.unity`, `.prefab`, `.anim`, etc.), rewires all references to the most-referenced copy, and deletes the duplicates. Includes a dry-run mode.
- **SelectMissingMaterials** — One-click: selects all GameObjects in the scene whose renderers have a null material slot.
- **ConvertURPToBuiltin** — Converts all URP materials to Built-in Standard, preserving base color, metallic, smoothness, normal map, and emission. Unity's own pipeline converter only goes the other direction (Built-in → URP).

---

### ShadowTools

An Editor window (`Tools > Shadows > Shadow Caster Manager`) for precise control over which objects cast shadows. The workflow: add the objects that *should* cast shadows to the list, then hit Apply — it disables shadows on every renderer in the scene, then re-enables them only on the listed objects and their children. Fully undoable. The list persists across sessions via a ScriptableObject.

Also exposes quick menu items:
- `Tools > Shadows > Disable Cast Shadows on All`
- `Tools > Shadows > Enable/Disable Cast Shadows on Selected + Children`

---

### Utilities

Small single-purpose scripts.

- **DisableAnimationLooping** — Batch-disables the Loop Time flag on every AnimationClip asset in the project. Handles both standalone `.anim` files and clips embedded in FBX imports.
- **ApplyMaterialToTree** — Editor window: pick a root GameObject and a material, applies the material to all renderer slots on every child (including inactive).
- **AnimatorStartDelay** — Delays enabling an Animator by N frames on `OnEnable`. Useful for staggering animation starts on objects that activate at the same time.
- **SkinnedAssetKiller** — Destroys specified child GameObjects via an animation event. Use it at the end of a skinned mesh animation to free the memory once playback is done.
- **SkyWallFadeIn** — Drives a `_DissolveProgress` property on a sky wall mesh renderer. Frame-based timing; call `Trigger()` to start.
- **FreeCam** — Runtime free-fly debug camera. Right-click + drag to look, WASD to move, Shift to sprint. Remove before shipping.

---

## Installation

Each folder is self-contained — copy only what you need.

| File type | Where to put it in your Unity project |
|---|---|
| Editor windows, inspectors, bakers (`*Baker.cs`, `*Editor.cs`, `*GUI.cs`, `*Tools.cs`, `*Cleaner.cs`, `*Merger.cs`) | `Assets/Editor/` |
| Runtime MonoBehaviours | `Assets/Scripts/` (anywhere under Assets) |
| Shaders (`.shader`) | `Assets/Shaders/` (anywhere under Assets) |

**BeamIn** — all four files are needed together. Bake barycentric coordinates with `BarycentricBaker` before the shader will show wireframe edges.

**Dissolve** — `BaseTransparentShaderGUI.cs` is Editor-only; the rest are runtime.

**Hologram** — `HologramShaderGUI.cs` is Editor-only; the shader is runtime.

---

## Requirements

- Unity 2021.3 LTS or later
- Universal Render Pipeline (URP)
