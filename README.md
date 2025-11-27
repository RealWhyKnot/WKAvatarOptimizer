# WhyKnot's Avatar Optimizer (WKAvatarOptimizer)

This project is a refactor and continuation based on the excellent work done by **d4rkc0d3r** on the original **d4rkAvatarOptimizer**.

*   **Original Repository:** [https://github.com/d4rkc0d3r/d4rkAvatarOptimizer](https://github.com/d4rkc0d3r/d4rkAvatarOptimizer)

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
    dotnet build WKAvatarOptimizer/WKAvatarOptimizer.csproj
    ```