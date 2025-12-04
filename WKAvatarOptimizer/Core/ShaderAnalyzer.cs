using WKAvatarOptimizer.Core.Universal;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using UnityEditor;
using UnityEngine;
using WKAvatarOptimizer.Core.Util; // For custom Profiler

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
            
            var cacheKey = (shader.name, material.name);
            if (universalShaderCache.TryGetValue(cacheKey, out var cachedIR))
            {
                return cachedIR;
            }

            ShaderIR ir = ParseUniversal(shader, material);
            if (ir != null)
            {
                universalShaderCache[cacheKey] = ir;
            }
            return ir;
        }

        public static List<ShaderIR> ParseAndCacheAllShaders(IEnumerable<(Shader shader, Material material)> shadersAndMaterials, bool overrideAlreadyCached, System.Action<int, int> progressCallback = null)
        {
            var results = new List<ShaderIR>();
            var loaders = shadersAndMaterials.Distinct()
                .Where(sm => overrideAlreadyCached || !universalShaderCache.ContainsKey((sm.shader.name, sm.material.name)))
                .Select(sm => (sm.shader, sm.material))
                .ToArray();
            
            Profiler.StartSection("ShaderAnalyzer.ParseAndCacheAllShaders()");
            var tasks = loaders.Select(sm => Task.Run(() => ParseUniversal(sm.shader, sm.material))).ToArray();
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
            // Return all results, including those already cached
            return shadersAndMaterials.Select(sm => universalShaderCache[(sm.shader.name, sm.material.name)]).ToList();
        }

        public static ShaderIR ParseUniversal(Shader shader, Material material)
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

            string shaderPath = AssetDatabase.GetAssetPath(shader);
            if (string.IsNullOrEmpty(shaderPath))
            {
                Debug.LogWarning($"[ShaderAnalyzer.ParseUniversal] Could not get asset path for shader '{shader.name}'. Falling back to dummy source.");
            }

            UniversalShaderLoader loader = new UniversalShaderLoader();
            return loader.LoadShader(shader, material);
        }
    }
}