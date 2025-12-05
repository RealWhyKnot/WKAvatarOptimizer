using WKAvatarOptimizer.Core.Universal;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using UnityEditor;
using UnityEngine;
using WKAvatarOptimizer.Core.Util;

namespace WKAvatarOptimizer.Core
{
    public class ShaderAnalyzer
    {
        public class ParserException : System.Exception
        {
            public ParserException(string message) : base(message) { }
        }

        private static Dictionary<(string shaderName, string materialName), ShaderIR> universalShaderCache = new Dictionary<(string shaderName, string materialName), ShaderIR>();
        
        public static void ClearShaderIRCache()
        {
            universalShaderCache.Clear();
        }
        
        public static ShaderIR Parse(Shader shader, Material material)
        {
            if (shader == null) return null;

            string shaderName = shader.name;
            string materialName = material != null ? material.name : "UnknownMaterial";

            var cacheKey = (shaderName, materialName);
            if (universalShaderCache.TryGetValue(cacheKey, out var cachedIR))
            {
                return cachedIR;
            }

            string path = null;
            try {
                path = AssetDatabase.GetAssetPath(shader);
            } catch { }

            ShaderIR ir = ParseUniversal(shader, material, path, shaderName, materialName);
            if (ir != null)
            {
                universalShaderCache[cacheKey] = ir;
            }
            return ir;
        }

        public static List<ShaderIR> ParseAndCacheAllShaders(IEnumerable<(Shader shader, Material material)> shadersAndMaterials, bool overrideAlreadyCached, System.Action<int, int> progressCallback = null)
        {
            var results = new List<ShaderIR>();
            
            // Pre-fetch data on main thread to avoid Unity API calls on background threads
            var loaders = shadersAndMaterials
                .Where(sm => sm.shader != null)
                .Distinct()
                .Select(sm => {
                    var shaderName = sm.shader.name;
                    var materialName = sm.material != null ? sm.material.name : "UnknownMaterial";
                    return (sm.shader, sm.material, shaderName, materialName, path: AssetDatabase.GetAssetPath(sm.shader));
                })
                .Where(data => overrideAlreadyCached || !universalShaderCache.ContainsKey((data.shaderName, data.materialName)))
                .ToArray();

            Profiler.StartSection("ShaderAnalyzer.ParseAndCacheAllShaders()");
            var tasks = loaders.Select(data => Task.Run(() => ParseUniversal(data.shader, data.material, data.path, data.shaderName, data.materialName))).ToArray();
            int done = 0;
            while (done < tasks.Length)
            {
                done = tasks.Count(t => t.IsCompleted);
                progressCallback?.Invoke(done, tasks.Length);
                Thread.Sleep(1);
            }
            Profiler.EndSection();

            foreach (var task in tasks)
            {
                var ir = task.Result;
                if (ir != null)
                {
                    universalShaderCache[(ir.Name, ir.MaterialName)] = ir;
                    results.Add(ir);
                }
            }
            
            // Return results (re-fetching from cache to ensure order/completeness)
            return shadersAndMaterials
                .Where(sm => sm.shader != null)
                .Select(sm => {
                    var shaderName = sm.shader.name;
                    var materialName = sm.material != null ? sm.material.name : "UnknownMaterial";
                    universalShaderCache.TryGetValue((shaderName, materialName), out var ir);
                    return ir;
                })
                .Where(ir => ir != null)
                .ToList();
        }

        public static ShaderIR ParseUniversal(Shader shader, Material material, string shaderPath = null, string shaderName = null, string materialName = null)
        {
            if (shader == null)
            {
                Debug.LogError("[ShaderAnalyzer.ParseUniversal] Shader is null.");
                return null;
            }
            if (material == null)
            {
                Debug.LogError("[ShaderAnalyzer.ParseUniversal] Material is null.");
                return null;
            }

            // Fallback if names not provided (safe only on main thread, but usually provided by ParseAndCacheAllShaders)
            if (string.IsNullOrEmpty(shaderName))
            {
                try { shaderName = shader.name; } catch { shaderName = "UnknownShader"; }
            }
            if (string.IsNullOrEmpty(materialName))
            {
                try { materialName = material.name; } catch { materialName = "UnknownMaterial"; }
            }

            if (string.IsNullOrEmpty(shaderPath))
            {
                try {
                    shaderPath = AssetDatabase.GetAssetPath(shader);
                } catch { }
            }

            if (string.IsNullOrEmpty(shaderPath))
            {
                Debug.LogWarning($"[ShaderAnalyzer.ParseUniversal] Could not get asset path for shader '{shaderName}'. Falling back to dummy source.");
            }

            UniversalShaderLoader loader = new UniversalShaderLoader();
            return loader.LoadShader(shader, material, shaderPath, shaderName, materialName);
        }
    }
}