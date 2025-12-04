# Changelog

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
