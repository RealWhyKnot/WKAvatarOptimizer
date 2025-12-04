# Changelog

## v2025.12.04.7

### Fixed
- **Critical Threading Issue:** Fixed `UnityException: GetName can only be called from the main thread` errors in shader parsing. Pre-fetch all Unity API calls (`shader.name`, `material.name`, `AssetDatabase.GetAssetPath`) on the main thread before starting background tasks in `ShaderAnalyzer`.
- **Material Property Threading Issue:** Fixed `HasProperty can only be called from the main thread` errors in `CreateFallbackShaderIR` by removing material property copying from the fallback path, as it was called from background threads.
- **KeyNotFoundException:** Fixed crash when looking up failed shader compilations in cache by using `TryGetValue` with fallback to main thread parsing in `ParseAndCacheAllShaders`.
- **DXC Null Reference Handling:** Added comprehensive null checks in `DxcCompiler.CompileToSpirV` with specific error messages for debugging: compiler initialization, blob encoding creation, and compilation result validation.
- **Method Signature Updates:** Updated `ShaderAnalyzer.ParseUniversal` and `UniversalShaderLoader.LoadShader` to accept pre-fetched `shaderName` and `materialName` parameters, ensuring thread-safe shader processing across the entire pipeline.

## v2025.12.04.6

### Fixed
- **Critical Crash Fix:** Corrected the `IDxcUtils` interface definition in `DXC.cs`. Switched from `CreateBlobFromPinned` to `CreateBlob` (Slot 6) to avoid potential memory aliasing issues and ensure stability. Verified VTable alignment for all `IDxcUtils` methods.
- **Native Library Loading:** Enhanced `DxcNative` to first check standard plugin directories (Assets/Packages/Plugins) before falling back to extracting embedded resources to a temp folder. This supports both development environments and distributed builds.
- **SPIR-V Reflection Stability:** Updated `SPIRVReflector` to pin the SPIR-V bytecode array in memory for the lifetime of the reflection module, preventing potential Garbage Collection issues.
- **DXC Configuration:** Introduced `DxcConfiguration` class to encapsulate compiler options (EntryPoint, TargetProfile, Optimization level) and updated `UniversalShaderLoader` to use it.

## v2025.12.04.5

### Fixed
- **Critical GUID Mismatch:** Corrected the `CLSID` and `IID` for `DxcUtils`. The previous values were incorrect, causing `DxcCreateInstance` to fail with `COMException`. The new values match the official `dxcapi.h` from the DirectXShaderCompiler repository.

## v2025.12.04.4

### Changed
- **Native Dependency Embedding:** `dxcompiler.dll` and `dxil.dll` are now embedded directly into `WKAvatarOptimizer.dll` as resources. They are automatically extracted to a temporary directory at runtime and loaded. This simplifies installation (drag-and-drop of a single DLL) and prevents path/loading issues in Unity.

## v2025.12.04.3

### Fixed
- `COMException` in `DxcCreateInstance` by correcting `CLSID` and `IID` GUIDs for `IDxcCompiler3` and `IDxcUtils` in `DXC.cs`.
- `E_INVALIDARG` or crashes in `IDxcCompiler3::Compile` by replacing the incorrect `IDxcBuffer` interface definition with the required `DxcBuffer` struct.
- Enhanced `dxcompiler.dll` and `dxil.dll` loading logic to ensure `dxil.dll` is loaded into the process address space, resolving potential signing/validation failures.

## v2025.12.04.2

### Fixed
- `UnityException: GetAssetPath can only be called from the main thread` by pre-fetching shader paths on the main thread in `ShaderAnalyzer` and passing them to the threaded `UniversalShaderLoader`.
- `DllNotFoundException: dxcompiler.dll` by explicitly loading the DLL using `Kernel32.LoadLibrary` in `DxcNative` static constructor, searching in the local project structure.
- Updated `.csproj` to copy native plugin DLLs (`dxcompiler.dll`, `dxil.dll`) to the build output directory for easier distribution.

## v2025.12.04.1

### Fixed
- Compilation errors in `SPIRVReflect.cs` due to visibility issues with nested types.
- Compilation errors in `ShaderAnalyzer.cs`, `MaterialOptimizer.cs`, `AvatarOptimizer.cs`, and editor scripts by fully removing references to the legacy `ParsedShader` class and updating calls to use `ShaderIR`.
- Fixed logic in `MainEditor.cs` to correctly inspect `ShaderIR` for debug information.
- Resolved visibility issue for `PropertyMapper.CopyMaterialPropertiesToIR`.
- Removed duplicate method in `ComponentOptimizer.cs`.

## v2025.12.04.0

### Added
- Universal Shader Rewriting System integration.
- DXC and SPIRV-Reflect dependencies and C# bindings (`WKAvatarOptimizer/Core/Native/`).
- Shader Intermediate Representation (ShaderIR) definition (`WKAvatarOptimizer/Core/Universal/ShaderIR.cs`).
- `UniversalShaderLoader` for shader source parsing and SPIR-V reflection (`WKAvatarOptimizer/Core/Universal/UniversalShaderLoader.cs`).
- `PropertyMapper` using Levenshtein distance for fuzzy property mapping (`WKAvatarOptimizer/Core/Universal/PropertyMapper.cs`).
- `UniversalAvatar.shader` and `UniversalAvatarCore.hlsl` as the target shader for optimization (`WKAvatarOptimizer/Shaders/`).

### Changed
- Refactored `ShaderAnalyzer` to utilize the new SPIR-V analysis pipeline via `ParseUniversal`.
- Updated `MaterialOptimizer` to use `ShaderIR` for material compatibility checks and to configure the `UniversalAvatar.shader`.
- Updated `MaterialOptimizer` to create texture arrays 1:1 with source materials for the Universal Shader.
- Adapted `AnimationRewriter` for compatibility with the new material structure (removed legacy property rewriting).
- Updated `DownloadDependencies.ps1` to fetch DXC and SPIRV-Reflect.

### Removed
- Legacy regex-based shader parsing logic in `ShaderAnalyzer` (deprecated/bypassed).
- Explicit VRChat budget validation checks in `ComponentOptimizer` (per user request).
