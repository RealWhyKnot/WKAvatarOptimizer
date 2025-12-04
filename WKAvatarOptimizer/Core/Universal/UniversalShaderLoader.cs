using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor; // Required for AssetDatabase
using System.IO;   // Required for File.ReadAllText
using WKAvatarOptimizer.Core.Native;

namespace WKAvatarOptimizer.Core.Universal
{
    public class UniversalShaderLoader
    {
        private DxcCompiler _dxcCompiler;

        public UniversalShaderLoader()
        {
            _dxcCompiler = new DxcCompiler();
        }

        public ShaderIR LoadShader(Shader unityShader, Material sourceMaterial)
        {
            if (unityShader == null)
            {
                throw new ArgumentNullException(nameof(unityShader));
            }
            if (sourceMaterial == null)
            {
                throw new ArgumentNullException(nameof(sourceMaterial));
            }

            string hlslSource = GetShaderSource(unityShader);
            
            if (string.IsNullOrEmpty(hlslSource))
            {
                return CreateFallbackShaderIR(unityShader, sourceMaterial, "Could not retrieve shader source.");
            }

            // For simplicity, hardcode entry point and profile for now.
            // These would ideally be determined by parsing the .shader file or more advanced reflection.
            string entryPoint = "main"; // Default entry point for pixel shaders
            string targetProfile = "ps_6_0"; // Pixel Shader 6.0 - broad compatibility

            byte[] spirvBytecode = null;
            try
            {
                spirvBytecode = _dxcCompiler.CompileToSpirV(hlslSource, entryPoint, targetProfile);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UniversalShaderLoader] DXC compilation failed for shader '{unityShader.name}': {ex.Message}");
                // Fallback to a basic ShaderIR or throw, depending on desired behavior
                return CreateFallbackShaderIR(unityShader, sourceMaterial, ex.Message);
            }

            ShaderIR ir = new ShaderIR
            {
                Name = unityShader.name,
                MaterialName = sourceMaterial.name
            };
            using (SPIRVReflector reflector = new SPIRVReflector(spirvBytecode))
            {
                // Extract semantic information using SPIRVReflector
                var bindings = reflector.GetDescriptorBindings();

                // Populate ShaderIR from bindings and other semantic data using PropertyMapper
                PopulateShaderIRFromReflectionAndMaterial(ir, bindings, unityShader, sourceMaterial);
            }

            return ir;
        }

        private string GetShaderSource(Shader unityShader)
        {
            // Get the asset path of the shader
            string shaderPath = AssetDatabase.GetAssetPath(unityShader);

            if (string.IsNullOrEmpty(shaderPath))
            {
                Debug.LogError($"[UniversalShaderLoader] Could not find asset path for shader '{unityShader.name}'. " +
                               "Cannot read shader source. Is it a built-in Unity shader or an asset not in the project?");
                return null;
            }
            
            // Unity's built-in shaders and some complex custom shaders might not expose their raw text directly.
            // For .shader files, we can read them directly.
            // For other types (e.g., .shadersubgraph, built-in), this approach might fail.
            if (shaderPath.EndsWith(".shader", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    return File.ReadAllText(shaderPath);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[UniversalShaderLoader] Failed to read shader file '{shaderPath}': {ex.Message}");
                    return null;
                }
            }
            else
            {
                Debug.LogWarning($"[UniversalShaderLoader] Unsupported shader file extension for '{unityShader.name}' at '{shaderPath}'. " +
                                 "Only .shader files are directly supported for source reading with this method. Returning dummy source.");
                // Fallback for non-.shader files, or built-in shaders where source is inaccessible
                 return @"
                    // Dummy HLSL source for testing DXC compilation when actual source is unavailable
                    struct PS_INPUT
                    {
                        float4 Position : SV_POSITION;
                        float2 TexCoord : TEXCOORD0;
                    };

                    Texture2D    g_texture    : register(t0);
                    SamplerState g_sampler    : register(s0);

                    float4 main(PS_INPUT Input) : SV_TARGET
                    {
                        return g_texture.Sample(g_sampler, Input.TexCoord);
                    }
                ";
            }
        }

        private ShaderIR CreateFallbackShaderIR(Shader unityShader, Material sourceMaterial, string errorMessage)
        {
            ShaderIR fallbackIR = new ShaderIR();
            fallbackIR.shadingModel = ShaderIR.ShadingModel.Unlit;
            fallbackIR.blendMode = ShaderIR.BlendMode.Opaque;
            fallbackIR.customNodes.Add(new CustomNode
            {
                name = "CompilationError",
                category = "Fallback",
                description = $"Failed to process shader '{unityShader.name}': {errorMessage}",
                suggestion = "Check shader syntax, file path, or if it's a built-in shader without accessible source."
            });

            // Populate other fallback properties directly from the material as a last resort
            PropertyMapper.CopyMaterialPropertiesToIR(fallbackIR, sourceMaterial);

            return fallbackIR;
        }

        // Renamed from PopulateShaderIR to better reflect its purpose and use PropertyMapper
        private void PopulateShaderIRFromReflectionAndMaterial(ShaderIR ir, SpvReflectDescriptorBinding[] bindings, Shader unityShader, Material sourceMaterial)
        {
            // Use PropertyMapper to map reflected bindings and material properties to ShaderIR
            PropertyMapper.MapBindingsToShaderIR(ir, bindings, sourceMaterial);

            // Additional inference based on shader name (heuristic) - can be refined or moved to PropertyMapper
            if (unityShader.name.Contains("PBR", StringComparison.OrdinalIgnoreCase))
            {
                ir.shadingModel = ShaderIR.ShadingModel.PBR;
            }
            else if (unityShader.name.Contains("Toon", StringComparison.OrdinalIgnoreCase))
            {
                ir.shadingModel = ShaderIR.ShadingModel.Toon;
            }
            else if (unityShader.name.Contains("Unlit", StringComparison.OrdinalIgnoreCase))
            {
                ir.shadingModel = ShaderIR.ShadingModel.Unlit;
            }
        }
    }
}
