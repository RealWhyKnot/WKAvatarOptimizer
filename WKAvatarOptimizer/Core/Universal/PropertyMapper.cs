using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine; // Added for Material
using WKAvatarOptimizer.Core.Native; // To use SpvReflectNative.SpvReflectDescriptorBinding

namespace WKAvatarOptimizer.Core.Universal
{
    public static class PropertyMapper
    {
        public enum TextureRole
        {
            Unknown,
            BaseColor,
            Normal,
            Metallic,
            Roughness, // Potentially separate from Metallic for some shaders
            Shade,
            Ramp,
            Matcap,
            Rim,
            Emission,
            Outline,
            Dissolve,
            Detail
        }

        public enum ParameterRole
        {
            Unknown,
            Float,
            Color,
            Vector,
            Int,
            MetallicStrength,
            Smoothness,
            RimPower,
            RimIntensity,
            OutlineWidth,
            AlphaThreshold,
            Scale,
            Intensity,
            ShadowThreshold,
            ShadowSmoothness
        }

        private static readonly Dictionary<TextureRole, string[]> TextureRoleKeywords = new Dictionary<TextureRole, string[]>
        {
            { TextureRole.BaseColor, new[] { "base", "color", "albedo", "diffuse", "main", "tex0", "texture" } },
            { TextureRole.Normal, new[] { "normal", "bump", "nrm", "nmap", "normalmap" } },
            { TextureRole.Metallic, new[] { "metallic", "metal", "spec", "specular", "ao" } }, // Often metallic/smoothness combined
            { TextureRole.Roughness, new[] { "roughness", "gloss", "smoothness" } }, // Can be separate from metallic or combined
            { TextureRole.Shade, new[] { "shade", "shadow", "dark", "shaded", "shadowmap" } },
            { TextureRole.Ramp, new[] { "ramp", "gradient", "lut", "curve", "shadowramp" } },
            { TextureRole.Matcap, new[] { "matcap", "sphere", "spheremap", "envmap", "spherecap" } },
            { TextureRole.Rim, new[] { "rim", "edge", "rimlight" } },
            { TextureRole.Emission, new[] { "emis", "glow", "light", "self", "emission" } },
            { TextureRole.Outline, new[] { "outline", "stroke", "contour", "border" } },
            { TextureRole.Dissolve, new[] { "dissolve", "fade", "mask", "erode" } },
            { TextureRole.Detail, new[] { "detail", "secondary", "overlay", "micro" } }
        };

        private static readonly Dictionary<ParameterRole, string[]> ParameterRoleKeywords = new Dictionary<ParameterRole, string[]>
        {
            { ParameterRole.MetallicStrength, new[] { "metallic", "metalstrength" } },
            { ParameterRole.Smoothness, new[] { "smoothness", "glossiness", "roughness" } },
            { ParameterRole.RimPower, new[] { "rimpower", "rimintensity" } },
            { ParameterRole.OutlineWidth, new[] { "outlinewidth", "outlinesize" } },
            { ParameterRole.AlphaThreshold, new[] { "alphacutoff", "cutoff", "threshold" } },
            { ParameterRole.Scale, new[] { "scale", "tiling", "uvscale" } },
            { ParameterRole.Intensity, new[] { "intensity", "strength", "amount" } },
            { ParameterRole.ShadowThreshold, new[] { "shadowthreshold", "shadownoise" } },
            { ParameterRole.ShadowSmoothness, new[] { "shadowsmoothness", "shadowfade" } }
        };

        public static TextureRole MapTextureToRole(string textureName)
        {
            return FuzzyMatch(textureName, TextureRoleKeywords);
        }

        public static ParameterRole MapParameterToRole(string parameterName)
        {
            return FuzzyMatch(parameterName, ParameterRoleKeywords);
        }

        private static T FuzzyMatch<T>(string name, Dictionary<T, string[]> roleKeywords) where T : Enum
        {
            string lowerName = name.ToLower();
            double bestScore = 0;
            T bestRole = (T)Enum.Parse(typeof(T), "Unknown"); // Default to Unknown

            foreach (var entry in roleKeywords)
            {
                foreach (var keyword in entry.Value)
                {
                    double similarity = LevenshteinSimilarity(lowerName, keyword);
                    
                    // Boost score if keyword is contained as substring (stronger match)
                    if (lowerName.Contains(keyword) || keyword.Contains(lowerName))
                    {
                        similarity = Math.Max(similarity, 0.8); // High confidence if substring match
                    }

                    if (similarity > bestScore)
                    {
                        bestScore = similarity;
                        bestRole = entry.Key;
                    }
                }
            }

            // Define a confidence threshold
            if (bestScore > 0.6) // Adjustable threshold
            {
                return bestRole;
            }
            return (T)Enum.Parse(typeof(T), "Unknown");
        }

        // Levenshtein Distance implementation
        public static double LevenshteinSimilarity(string s1, string s2)
        {
            if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2)) return 0;
            if (s1 == s2) return 1;

            int n = s1.Length;
            int m = s2.Length;
            int[,] d = new int[n + 1, m + 1];

            // Initialize the distance matrix
            for (int i = 0; i <= n; i++) d[i, 0] = i;
            for (int j = 0; j <= m; j++) d[0, j] = j;

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (s2[j - 1] == s1[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1,      // Deletion
                                 d[i, j - 1] + 1),      // Insertion
                        d[i - 1, j - 1] + cost);        // Substitution
                }
            }

            // Return similarity, not distance
            return 1.0 - ((double)d[n, m] / Math.Max(n, m));
        }

        // Helper to populate ShaderIR based on reflected bindings and current material properties
        public static void MapBindingsToShaderIR(ShaderIR ir, SpvReflectDescriptorBinding[] bindings, Material sourceMaterial)
        {
            foreach (var binding in bindings)
            {
                string name = Marshal.PtrToStringAnsi(binding.name);
                if (string.IsNullOrEmpty(name)) continue;

                if (binding.descriptor_type == SpvReflectDescriptorType.SPV_REFLECT_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER ||
                    binding.descriptor_type == SpvReflectDescriptorType.SPV_REFLECT_DESCRIPTOR_TYPE_SAMPLED_IMAGE)
                {
                    TextureRole role = MapTextureToRole(name);
                    Texture2D texture = null;
                    if (sourceMaterial.HasProperty(name))
                    {
                         texture = sourceMaterial.GetTexture(name) as Texture2D;
                    }
                    // Fallback to _MainTex for BaseColor if original property not found
                    if (role == TextureRole.BaseColor && texture == null && sourceMaterial.HasProperty("_MainTex"))
                    {
                        texture = sourceMaterial.GetTexture("_MainTex") as Texture2D;
                    }

                    switch (role)
                    {
                        case TextureRole.BaseColor: ir.baseColor.texture = texture; break;
                        case TextureRole.Normal: ir.normalMap.texture = texture; break;
                        case TextureRole.Metallic: 
                        case TextureRole.Roughness: // Often combined, refine later
                            ir.metallicGlossMap.texture = texture; 
                            break;
                        case TextureRole.Shade: ir.shadeMap.texture = texture; break;
                        case TextureRole.Ramp: ir.rampTexture.texture = texture; break;
                        case TextureRole.Matcap: ir.matcapTexture.texture = texture; break;
                        case TextureRole.Rim: // Rim textures are less common, often just color
                            ir.customNodes.Add(new CustomNode { name = name, category = "TextureBinding", description = $"Rim texture detected: {name}" });
                            break;
                        case TextureRole.Emission: ir.emissionMap.texture = texture; break;
                        case TextureRole.Outline: ir.outlineMask.texture = texture; break;
                        case TextureRole.Dissolve: ir.dissolveMask.texture = texture; break;
                        case TextureRole.Detail: ir.detailMap.texture = texture; break;
                        default:
                            ir.customNodes.Add(new CustomNode
                            {
                                name = name,
                                category = "TextureBinding",
                                description = $"Unmapped texture binding detected: {name}",
                                suggestion = "Consider adding this texture role to ShaderIR or PropertyMapper."
                            });
                            break;
                    }
                }
                // TODO: Handle Uniform Buffers for other parameters
            }

            // Copy explicit material properties for colors/scalars (to override or supplement reflected data)
            CopyMaterialPropertiesToIR(ir, sourceMaterial);
        }

        public static void CopyMaterialPropertiesToIR(ShaderIR ir, Material sourceMaterial)
        {
            // Textures (already handled by reflection and mapping, but ensure scale/offset)
            if (sourceMaterial.HasProperty("_MainTex_ST"))
            {
                Vector4 st = sourceMaterial.GetVector("_MainTex_ST");
                ir.baseColor.scale = new UnityEngine.Vector2(st.x, st.y);
                ir.baseColor.offset = new UnityEngine.Vector2(st.z, st.w);
            }

            // Colors
            if (sourceMaterial.HasProperty("_Color")) ir.baseColor.color = sourceMaterial.GetColor("_Color");
            if (sourceMaterial.HasProperty("_ShadeColor")) ir.shadeColor = sourceMaterial.GetColor("_ShadeColor");
            if (sourceMaterial.HasProperty("_RimColor")) ir.rimColor = sourceMaterial.GetColor("_RimColor");
            if (sourceMaterial.HasProperty("_OutlineColor")) ir.outlineColor = sourceMaterial.GetColor("_OutlineColor");
            if (sourceMaterial.HasProperty("_EmissionColor")) ir.emissionColor = sourceMaterial.GetColor("_EmissionColor");
            if (sourceMaterial.HasProperty("_MatcapColor")) ir.matcapColor = sourceMaterial.GetColor("_MatcapColor");

            // Floats
            if (sourceMaterial.HasProperty("_BumpScale")) ir.normalScale = sourceMaterial.GetFloat("_BumpScale");
            if (sourceMaterial.HasProperty("_Metallic")) ir.metallicStrength = sourceMaterial.GetFloat("_Metallic");
            if (sourceMaterial.HasProperty("_Glossiness")) ir.smoothness = sourceMaterial.GetFloat("_Glossiness"); // Standard
            if (sourceMaterial.HasProperty("_Smoothness")) ir.smoothness = sourceMaterial.GetFloat("_Smoothness"); // URP/HDRP
            if (sourceMaterial.HasProperty("_RimPower")) ir.rimPower = sourceMaterial.GetFloat("_RimPower");
            if (sourceMaterial.HasProperty("_RimIntensity")) ir.rimIntensity = sourceMaterial.GetFloat("_RimIntensity");
            if (sourceMaterial.HasProperty("_OutlineWidth")) ir.outlineWidth = sourceMaterial.GetFloat("_OutlineWidth");
            if (sourceMaterial.HasProperty("_EmissionStrength")) ir.emissionIntensity = sourceMaterial.GetFloat("_EmissionStrength");
            if (sourceMaterial.HasProperty("_DissolveAmount")) ir.dissolveAmount = sourceMaterial.GetFloat("_DissolveAmount");
            if (sourceMaterial.HasProperty("_ShadowThreshold")) ir.shadowThreshold = sourceMaterial.GetFloat("_ShadowThreshold");
            if (sourceMaterial.HasProperty("_ShadowSmoothness")) ir.shadowSmooth = sourceMaterial.GetFloat("_ShadowSmoothness");
            if (sourceMaterial.HasProperty("_DetailNormalMapScale")) ir.detailScale = sourceMaterial.GetFloat("_DetailNormalMapScale");

            // Bools/Ints
            if (sourceMaterial.HasProperty("_UseOutline")) ir.useOutline = sourceMaterial.GetFloat("_UseOutline") > 0.5f;
            if (sourceMaterial.HasProperty("_UseMatcapSecond")) ir.useMatcapSecond = sourceMaterial.GetFloat("_UseMatcapSecond") > 0.5f;
            if (sourceMaterial.HasProperty("_OutlineScreenSpace")) ir.outlineScreenSpace = sourceMaterial.GetFloat("_OutlineScreenSpace") > 0.5f;

            // Infer blend mode from material properties
            if (sourceMaterial.HasProperty("_Mode")) // Unity Standard/URP
            {
                var mode = (int)sourceMaterial.GetFloat("_Mode");
                switch (mode)
                {
                    case 0: ir.blendMode = ShaderIR.BlendMode.Opaque; break; // Opaque
                    case 1: ir.blendMode = ShaderIR.BlendMode.Cutout; break; // Cutout
                    case 2: ir.blendMode = ShaderIR.BlendMode.Alpha; break;   // Fade
                    case 3: ir.blendMode = ShaderIR.BlendMode.Alpha; break;   // Transparent (often same as fade)
                }
            }
            else if (sourceMaterial.HasProperty("_BlendMode")) // Poiyomi/LilToon (common naming)
            {
                var mode = (int)sourceMaterial.GetFloat("_BlendMode");
                switch (mode)
                {
                    case 0: ir.blendMode = ShaderIR.BlendMode.Opaque; break;
                    case 1: ir.blendMode = ShaderIR.BlendMode.Cutout; break;
                    case 2: ir.blendMode = ShaderIR.BlendMode.Alpha; break; // Alpha/Fade
                    case 3: ir.blendMode = ShaderIR.BlendMode.Additive; break;
                    case 4: ir.blendMode = ShaderIR.BlendMode.Premultiplied; break;
                }
            }

            // Infer cull mode
            if (sourceMaterial.HasProperty("_Cull"))
            {
                var cull = (int)sourceMaterial.GetFloat("_Cull");
                switch (cull)
                {
                    case 0: ir.cullMode = ShaderIR.CullMode.Off; break;
                    case 1: ir.cullMode = ShaderIR.CullMode.Front; break;
                    case 2: ir.cullMode = ShaderIR.CullMode.Back; break;
                }
            }
        }
    }
}
