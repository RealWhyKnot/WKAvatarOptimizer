using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using System.IO;
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

        public ShaderIR LoadShader(Shader unityShader, Material sourceMaterial, string shaderPath, string shaderName, string materialName)
        {
            if (unityShader == null) throw new ArgumentNullException(nameof(unityShader));
            if (sourceMaterial == null) throw new ArgumentNullException(nameof(sourceMaterial));

            if (shaderPath == null)
            {
                try {
                    shaderPath = AssetDatabase.GetAssetPath(unityShader);
                } catch { }
            }

            if (string.IsNullOrEmpty(shaderName))
            {
                try { shaderName = unityShader.name; } catch { shaderName = "UnknownShader"; }
            }
            if (string.IsNullOrEmpty(materialName))
            {
                try { materialName = sourceMaterial.name; } catch { materialName = "UnknownMaterial"; }
            }

            string hlslSource = GetShaderSource(shaderName, shaderPath);
            
            if (string.IsNullOrEmpty(hlslSource))
            {
                return CreateFallbackShaderIR(shaderName, sourceMaterial, "Could not retrieve shader source.");
            }

            string entryPoint = "main";
            string targetProfile = "ps_6_0";

            byte[] spirvBytecode = null;
            try
            {
                DxcConfiguration config = new DxcConfiguration 
                { 
                    EntryPoint = entryPoint, 
                    TargetProfile = targetProfile,
                    OutputSpirV = true,
                    VulkanLayout = true,
                    Optimization = false
                };
                spirvBytecode = _dxcCompiler.CompileToSpirV(hlslSource, config);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UniversalShaderLoader] DXC compilation failed for shader '{shaderName}': {ex.Message}");
                return CreateFallbackShaderIR(shaderName, sourceMaterial, ex.Message);
            }

            ShaderIR ir = new ShaderIR
            {
                Name = shaderName,
                MaterialName = materialName
            };

            using (SPIRVReflector reflector = new SPIRVReflector(spirvBytecode))
            {
                var bindings = reflector.GetDescriptorBindings();
                PopulateShaderIRFromReflectionAndMaterial(ir, bindings, shaderName, sourceMaterial);
            }

            return ir;
        }

        private string GetShaderSource(string shaderName, string shaderPath)
        {
            if (string.IsNullOrEmpty(shaderPath))
            {
                Debug.LogWarning($"[UniversalShaderLoader] Could not find asset path for shader '{shaderName}'. Cannot read shader source.");
                return null;
            }

            if (shaderPath.EndsWith(".shader", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    string source = File.ReadAllText(shaderPath);
                    if (string.IsNullOrWhiteSpace(source))
                    {
                        Debug.LogWarning($"[UniversalShaderLoader] Shader file '{shaderPath}' is empty.");
                        return null;
                    }
                    return source;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[UniversalShaderLoader] Failed to read shader file '{shaderPath}': {ex.Message}");
                    return null;
                }
            }
            else
            {
                Debug.LogWarning($"[UniversalShaderLoader] Unsupported shader file extension for '{shaderName}' at '{shaderPath}'. Only .shader files are supported. Returning dummy source.");
                 return @"
                    struct PS_INPUT { float4 Position : SV_POSITION; float2 TexCoord : TEXCOORD0; };
                    Texture2D g_texture : register(t0);
                    SamplerState g_sampler : register(s0);
                    float4 main(PS_INPUT Input) : SV_TARGET { return g_texture.Sample(g_sampler, Input.TexCoord); }
                ";
            }
        }

        private ShaderIR CreateFallbackShaderIR(string shaderName, Material sourceMaterial, string errorMessage)
        {
            ShaderIR fallbackIR = new ShaderIR();
            fallbackIR.shadingModel = ShaderIR.ShadingModel.Unlit;
            fallbackIR.blendMode = ShaderIR.BlendMode.Opaque;
            fallbackIR.customNodes.Add(new CustomNode
            {
                name = "CompilationError",
                category = "Fallback",
                description = $"Failed to process shader '{shaderName}': {errorMessage}",
                suggestion = "Check shader syntax or file path."
            });

            return fallbackIR;
        }

        private void PopulateShaderIRFromReflectionAndMaterial(ShaderIR ir, SpvReflectDescriptorBinding[] bindings, string shaderName, Material sourceMaterial)
        {
            PropertyMapper.MapBindingsToShaderIR(ir, bindings, sourceMaterial);

            if (shaderName.Contains("PBR", StringComparison.OrdinalIgnoreCase))
            {
                ir.shadingModel = ShaderIR.ShadingModel.PBR;
            }
            else if (shaderName.Contains("Toon", StringComparison.OrdinalIgnoreCase))
            {
                ir.shadingModel = ShaderIR.ShadingModel.Toon;
            }
            else if (shaderName.Contains("Unlit", StringComparison.OrdinalIgnoreCase))
            {
                ir.shadingModel = ShaderIR.ShadingModel.Unlit;
            }
        }
    }
}