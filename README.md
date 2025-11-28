# WhyKnot's Avatar Optimizer (WKAvatarOptimizer)

This project is a highly aggressive and automated avatar optimizer for VRChat, refactored and enhanced from the original `d4rkAvatarOptimizer`. It focuses on maximizing performance by merging meshes, materials, and textures, reducing draw calls, and optimizing VRAM usage, with zero configuration required.

**Original Repository:** [https://github.com/d4rkc0d3r/d4rkAvatarOptimizer](https://github.com/d4rkc0d3r/d4rkAvatarOptimizer)

---

## How It Works

The optimizer runs a sequence of automated passes on your avatar during the build process.

### 1. Texture Optimization
**Goal:** Reduce VRAM usage and download size while preparing textures for merging.
*   **Analysis:** Scans all materials on the avatar to identify used textures.
*   **Format Conversion:** Automatically converts textures to efficient formats:
    *   **Normal Maps:** Converted to **BC5** (high quality, optimized for normals).
    *   **Textures with Alpha:** Converted to **DXT5** (standard compressed with alpha).
    *   **Opaque Textures:** Converted to **DXT1** (standard compressed, no alpha).
    *   *Note:* Crunch compression is disabled to allow textures to be merged into Texture Arrays (Unity does not support Texture Arrays with crunched textures).
*   **Settings:** Enforces **Mipmaps** enabled and sets **Kaiser Filter** for better downscaling quality.

### 2. Component Cleanup
**Goal:** Remove unnecessary overhead from the hierarchy.
*   **Editor-Only Removal:** Destroys GameObjects and Components tagged with `EditorOnly` or known to be useless at runtime.
*   **Unused Components:** Removes components like `Cloth`, `RigidBody`, `Joint`, `Collider`, `AudioSource`, `Light`, and `ParticleSystem` if they are disabled or effectively doing nothing.
*   **Unused GameObjects:** Recursively deletes GameObjects that have no active components and are not part of the armature hierarchy.

### 3. Material Deduplication
**Goal:** Consolidate identical materials to share instances.
*   **Scan:** Finds all materials on all renderers.
*   **Compare:** Checks materials for equality based on Shader, RenderQueue, Keywords, and Properties (Textures, Floats, Colors).
*   **Replace:** If duplicates are found, replaces them with a single shared material instance.

### 4. Shader Parsing & Compatibility
**Goal:** Understand shader properties to enable intelligent merging.
*   **Parsing:** Parses the source code of every shader used on the avatar. It identifies properties, passes, keywords, and specific requirements (like `ifex` logic).
*   **Robust Keyword Handling:** Ensures that shader features controlled by `#ifdef` directives are correctly evaluated and baked into the optimized shader, preventing visual glitches or invisible meshes caused by incorrect keyword definitions.
*   **Universal Shader Check:** A robust compatibility check allows merging materials even if they use **different shaders**, provided they:
    *   **Superset Logic:** The "leader" shader (the one defining the final merged material) must contain all properties (by name and type) and passes (by LightMode tag) present in the "candidate" shader. This ensures no data is lost during the merge.
    *   **Promotion:** If a candidate shader is found to be a *superset* of the current group's leader shader, the candidate will be promoted to be the new group leader. This allows groups to dynamically find the most comprehensive shader definition for optimal merging.

### 5. Mesh Merging
**Goal:** Drastically reduce Draw Calls by combining multiple meshes into one.
*   **Skinned Mesh Merging:**
    *   Identifies groups of `SkinnedMeshRenderer`s that can be safely combined.
    *   **Compatibility Checks:** Ensures meshes share the same:
        *   Root bone scale sign.
        *   Shadow casting/receiving settings.
        *   Layer.
        *   **Parent Toggles:** Verifies that meshes aren't controlled by conflicting parent `GameObject` toggles.
        *   **Default State:** Ensures default enabled/disabled states match.
    *   **Baking:** Bakes the current pose and blendshapes into the mesh data.
    *   **Atlas Support:** Encodes a "Material ID" into the UVs of the merged mesh, allowing the shader to select the correct texture from a Texture Array.
*   **Static Mesh Conversion:** Converts `MeshRenderer` (static) objects into `SkinnedMeshRenderer`s so they can be merged into the main skinned mesh system.

### 6. Material Merging & Texture Arrays (Atlasing)
**Goal:** Allow merged meshes to use a single material slot, further reducing Draw Calls.
*   **Grouping:** Groups materials that are compatible (same shader or compatible superset).
*   **Texture Arrays:**
    *   Scans compatible materials for textures that can be stacked.
    *   Creates **Texture2DArrays** containing all textures for a specific property (e.g., `_MainTex` array for 10 different shirts).
    *   This is superior to traditional atlasing as it avoids UV remapping issues and bleeding.
    *   *Note:* Textures in Crunched format are skipped for Texture Array creation to avoid Unity limitations, as the Texture Optimizer now defaults to non-crunched DXT formats for compatibility.
*   **Property Packing:**
    *   For non-texture properties (Colors, Floats), values are packed into arrays or encoded constants.
    *   The optimized shader reads these values using the encoded "Material ID" from the mesh vertex data.
*   **Shader Generation:**
    *   Generates a new "Optimized" shader based on the original.
    *   Injects logic to sample from Texture Arrays and Property Arrays based on the instance ID.

### 7. Animator & FX Layer Optimization
**Goal:** Clean up the Animator Controller and reduce CPU overhead.
*   **Useless Layer Removal:**
    *   Analyzes the FX Animator Controller.
    *   Identifies layers that have 0 weight and are never touched by layer control behaviours.
    *   Removes layers that animate invalid or missing objects.
*   **Optimization:**
    *   Attempts to merge compatible layers to reduce layer count.
    *   Converts boolean parameters to floats where efficient.
*   **Path Fixing:** Rewrites animation paths to point to the new locations of merged meshes (e.g., moving a toggle from `Shirt/Mesh` to `BodyMerged/Material_IsActive_Shirt`).

### 8. Bone & PhysBone Optimization
*   **PhysBone Disabling:** Identifies PhysBones that affect transforms that have been merged or removed and disables them.
*   **Unused Bones:** Can identify and merge bones that are not animated and have no weights (though this is conservative to avoid breaking IK).

---

## Usage

1.  Add the `WK Avatar Optimizer` component to your avatar root.
2.  (Optional) Add transforms to the "Exclude Transforms" list if you need specific objects to remain separate.
3.  Build and Upload! The optimization happens automatically during the SDK build process.

---

## Known Issues & Debugging

### Invisible Meshes (Current Priority - Debugging Grayscale Textures)
**Status:** Ongoing Investigation
**Symptoms:**
*   **Initial:** Meshes are completely invisible after optimization.
*   **After Fix 1 (Keyword Injection):** Meshes become visible (can be toggled on/off) but their textures appear grayscale or broken, particularly when in Play Mode in Unity. Exiting Play Mode shows textures correctly.

**Root Causes & Diagnosis:**

1.  **Invisible Meshes (Addressed):**
    *   **Original Problem:** Complex shaders (like Poiyomi) often define keywords (`#pragma shader_feature`) within nested `#include` files. The `ShaderAnalyzer` historically missed these, leading to `MaterialOptimizer` filtering out valid keywords. Consequently, `ShaderOptimizer` received an empty keyword list and failed to inject `#define` statements into the generated shader. This resulted in conditional code blocks (`#ifdef`) evaluating incorrectly and breaking shader logic, causing geometry to be invisible.
    *   **Fix Implemented:** `MaterialOptimizer.cs` was updated to pass *all* enabled keywords from the material to the `ShaderOptimizer`, ensuring that `#define KEYWORD 1` statements are always injected for active keywords, regardless of whether `ShaderAnalyzer` initially detected them. This should resolve the core "invisibility" issue.

2.  **Grayscale/Broken Textures in Play Mode (Under Investigation):**
    *   **Symptom Detail:** This issue specifically manifests in Play Mode. Textures appear desaturated or incorrect, but render correctly outside of Play Mode. This strongly suggests a runtime material/shader property issue or an interaction with Unity's internal rendering pipeline.
    *   **Initial Analysis:**
        *   **Texture Arrays:** The optimizer aggressively converts textures into `Texture2DArray`s for merging. If `_MainTex` (or other textures) are grayscale, it could be due to:
            *   Incorrect binding of the `Texture2DArray` to the material.
            *   Wrong texture format or sRGB settings for the array.
            *   Issues with UV coordinates not correctly sampling the array slice.
            *   Shader sampling logic for the `Texture2DArray` might be flawed during runtime.
        *   **Mipmap Generation:** Lack of proper mipmaps, or mipmap generation issues, can sometimes cause blurry or desaturated textures at runtime, especially at different viewing distances.
    *   **Fixes Implemented (for investigation):**
        *   **Force Mipmaps:** `MaterialOptimizer.cs` was updated to explicitly force mipmap generation for all `Texture2DArray`s created, regardless of the source texture's `mipmapCount`.
        *   **Enhanced Logging:**
            *   `MaterialOptimizer.cs` now logs the *exact* list of keywords being collected from the material (`shaderKeywords`).
            *   `MaterialOptimizer.cs` now logs when a texture is added to the `texturesToMerge` list, confirming its eligibility for `Texture2DArray` conversion.
            *   `TextureOptimizer.cs` now logs the original `mipmapEnabled` state, `mipmapCount`, and `format` for each texture processed.
            *   (Previous attempt to add debug logs to `ShaderOptimizer.cs` about injected keywords was seemingly not reflected in previous `TrashBin` output, possibly due to build mismatch).

**Next Steps for Debugging:**
The provided Unity Editor log (`TrashBin/output.txt`, if available) should be meticulously checked for any shader compilation warnings or errors, or runtime messages related to materials or textures. The new, detailed logs generated by `WKAvatarOptimizer_Log.txt` must be examined to verify:
*   That the correct keywords are indeed being passed and injected into the generated shader.
*   That textures intended for `Texture2DArray`s are correctly identified and processed.
*   That mipmaps are being generated as expected.

Further investigation will focus on the interaction between the generated `Texture2DArray`s, material properties, and runtime shader behavior, especially regarding `sRGB` conversion and texture sampling.
