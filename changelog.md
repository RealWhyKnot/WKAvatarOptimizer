# Changelog

## v2025.12.05.7

### Added
- **Verbose Debugging:** Implemented comprehensive verbose logging in `DxcCompiler` to trace the entire compilation process, including argument details, interface GUIDs, and raw pointer values. This is to definitively diagnose any remaining `InvalidCastException` or interface compatibility issues with the embedded `dxcompiler.dll`.

## v2025.12.05.6

### Fixed
- **DXC GUID Resolution:** Resolved an issue where `typeof(Interface).GUID` was unexpectedly returning an empty GUID (`00000000-...`) during runtime, causing `IDxcCompiler3::Compile` to receive an invalid interface request and return `E_NOINTERFACE` (0x80004002). GUIDs for `IDxcOperationResult` and `IDxcResult` are now explicitly defined using string literals, ensuring correct interface requests to `dxcompiler.dll`.

## v2025.12.05.5

### Fixed
- **DXC Interface Compatibility:** Modified `DxcCompiler.CompileToSpirV` to explicitly request the `IDxcOperationResult` interface (GUID `CEDB484A...`) from `IDxcCompiler3::Compile` instead of `IDxcResult`. The embedded `dxcompiler.dll` (v1.8.2505) was returning `E_NOINTERFACE` (0x80004002) when `IDxcResult` (GUID `5834...`) was requested, indicating it does not support the modern result interface or uses a different IID. Requesting the base `IDxcOperationResult` interface ensures compatibility and resolves the `InvalidCastException` / `E_NOINTERFACE` errors.

## v2025.12.05.4

### Added
- **Enhanced Diagnostics:** Refactored DXC compilation process to return a structured `DxcCompileResult` object containing detailed diagnostic information on compilation failures. This ensures that `InvalidCastException` details, `pResultPtr` address, requested GUIDs, and manual `QueryInterface` HRESULTs are always captured and logged in the Unity Editor log, providing comprehensive debugging information.

## v2025.12.05.3

### Fixed
- **DXC Interface Stability:** Refactored `DxcCompiler` to retrieve compilation results using the stable `IDxcOperationResult` interface (GUID `CEDB484A...`) instead of the version-sensitive `IDxcResult` interface. This avoids VTable layout ambiguities found in some `dxcompiler.dll` builds (like the embedded 1.8.2505) which caused `InvalidCastException` or crashes.
- **Diagnostics:** Retained enhanced debug logging for result casting to aid in future troubleshooting if needed.

## v2025.12.05.2

### Added
- **Diagnostics:** Added detailed debug logging to `DxcCompiler` to diagnose persistent `InvalidCastException` failures when retrieving `IDxcResult`. This includes logging the returned pointer address and attempting manual `QueryInterface` calls for `IDxcResult` and `IDxcOperationResult` to identify the actual interface supported by the embedded `dxcompiler.dll`.

## v2025.12.05.1

### Fixed
- **Critical DXC Interface Crash:** Corrected the VTable layout of `IDxcResult` in `DXC.cs`. The previous definition did not match the embedded `dxcompiler.dll` (v1.8.2505) which inherits `IDxcOperationResult`. This mismatch caused `InvalidCastException` and crashes when retrieving compilation results or errors.

## v2025.12.05.0

### Fixed
- **DXC Compilation Crash:** Resolved persistent crashes and `NullReferenceException`s by bypassing `IDxcUtils` and `IDxcLibrary` blob creation methods entirely. `UniversalShaderLoader` now constructs `DxcBuffer` directly from managed memory pointers, ensuring robust shader compilation without relying on unstable blob factory methods in the embedded `dxcompiler.dll`.
- **Threading Stability:** Resolved `UnityException: GetName can only be called from the main thread` by refactoring `ShaderAnalyzer` and `UniversalShaderLoader` to pre-fetch shader and material names on the main thread before passing them to background tasks.

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
- `PropertyMapper` using Levenshtein distance to fuzzy property mapping (`WKAvatarOptimizer/Core/Universal/PropertyMapper.cs`).
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
