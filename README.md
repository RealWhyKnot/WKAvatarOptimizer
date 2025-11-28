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

### Invisible Meshes (Current Priority)
**Status:** Under Investigation / Fix Identified
**Symptoms:** Certain meshes (especially those using complex shaders like Poiyomi) become invisible or pink after optimization.
**Root Cause:** 
*   **Hidden Pragmas:** Complex shaders often define their keywords (`#pragma shader_feature`) inside nested `#include` files (e.g., `.cginc`).
*   **Parser Limitation:** The current `ShaderAnalyzer` parses the main shader file but does not recursively scan `#include` files for keyword definitions.
*   **Incorrect Filtering:** The `MaterialOptimizer` currently filters the material's active keywords against the list of keywords *found* by the parser. Since the parser misses the hidden ones, valid keywords are discarded.
*   **Broken Logic:** The `ShaderOptimizer` generates a static shader. It expects to receive a list of keywords to "bake" (inject as `#define KEYWORD 1`). Because the list is empty (due to the filtering above), the defines are missing. Global conditional blocks (like `#ifdef POI_FEATURE` around structs) evaluate to `false`, causing struct mismatches between the Vertex and Fragment shaders, leading to invisible geometry.

**Planned Fix:**
Update `MaterialOptimizer.cs` to stop filtering keywords based on the parser's findings. It should pass **all** enabled keywords from the source material to the `ShaderOptimizer`. This forces the optimizer to define every active keyword, ensuring that hidden `#ifdef` checks in included files resolve correctly.
