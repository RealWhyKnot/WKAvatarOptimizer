# Changelog

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
