# WhyKnot's VRC Optimizer (WKVRCOptimizer)

**WhyKnot's VRC Optimizer** is a modernized, refactored version of the VRChat Avatar Optimizer tool, designed to help creators optimize their VRChat avatars for better performance and easier upload workflows.

This project targets **.NET Standard 2.1** and is compatible with **Unity 2022.3** and **VRChat SDK 3.10.0+**.

## 🔗 Origins & Credits

This project is a refactor and continuation based on the excellent work done by **d4rkc0d3r** on the original **d4rkAvatarOptimizer**.

*   **Original Repository:** [https://github.com/d4rkc0d3r/d4rkAvatarOptimizer](https://github.com/d4rkc0d3r/d4rkAvatarOptimizer)

We gratefully acknowledge d4rkc0d3r's contribution to the VRChat community. This project aims to maintain the core optimization logic while modernizing the codebase structure, resolving namespace conflicts with newer SDKs, and improving maintainability.

## ✨ Features

*   **Shader Analysis:** Identifies and optimizes shader properties.
*   **Texture Optimization:** Analyzes texture compression and format settings.
*   **Animator Optimization:** Merges layers, optimizes state machines, and cleans up unused parameters.
*   **Mesh Merging:** Combines skinned meshes to reduce draw calls.
*   **PhysBone Optimization:** Analyzes and optimizes PhysBone components (compatible with the new split assembly structure in SDK 3.10.0).

## 🛠️ Development Setup

1.  **Dependencies:** Run the PowerShell script to fetch required VRChat SDKs and Unity DLLs:
    ```powershell
    .\DownloadDependencies.ps1
    ```
2.  **Build:** Open the solution in your IDE (VS Code, Visual Studio, Rider) or build via CLI:
    ```bash
    dotnet build WKVRCOptimizer/WKVRCOptimizer.csproj
    ```

## 📝 License

Please refer to the license file included in the repository (inheriting terms from the original d4rkAvatarOptimizer where applicable).
