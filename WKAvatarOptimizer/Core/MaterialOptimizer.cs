using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using WKAvatarOptimizer.Core.Universal; // Added
using WKAvatarOptimizer.Data;
using WKAvatarOptimizer.Extensions;
using WKAvatarOptimizer.Core.Util;

namespace WKAvatarOptimizer.Core
{
    public class MaterialOptimizer
    {
        private readonly OptimizationContext context;
        private readonly GameObject gameObject;
        private readonly AvatarOptimizer optimizer;

        private Dictionary<string, Material> cache_GetFirstMaterialOnPath = null;
        private HashSet<Material> cache_FindAllUsedMaterials = null;

        public MaterialOptimizer(OptimizationContext context, GameObject gameObject, AvatarOptimizer optimizer)
        {
            this.context = context;
            this.gameObject = gameObject;
            this.optimizer = optimizer;
        }

        public class MaterialAssetComparer : IEqualityComparer<Material> {
            public bool Equals(Material a, Material b) {
                if (a == b)
                    return true;
                if (a == null || b == null)
                    return false;
                if (a.shader != b.shader)
                    return false;
                if (a.renderQueue != b.renderQueue)
                    return false;
                if (a.doubleSidedGI != b.doubleSidedGI)
                    return false;
                if (a.enableInstancing != b.enableInstancing)
                    return false;
                if (a.globalIlluminationFlags != b.globalIlluminationFlags)
                    return false;
                
                var aKeywords = a.shaderKeywords;
                var bKeywords = b.shaderKeywords;
                if (aKeywords.Length != bKeywords.Length)
                    return false;
                System.Array.Sort(aKeywords);
                System.Array.Sort(bKeywords);
                if (!aKeywords.SequenceEqual(bKeywords))
                    return false;

                for (int i = 0; i < a.shader.passCount; i++) {
                    var lightModeValue = a.shader.FindPassTagValue(i, new ShaderTagId("LightMode"));
                    if (!string.IsNullOrEmpty(lightModeValue.name)) {
                        if (a.GetShaderPassEnabled(lightModeValue.name) != b.GetShaderPassEnabled(lightModeValue.name)) {
                            return false;
                        }
                    }
                }

                string[] aFloats = a.GetPropertyNames(MaterialPropertyType.Float);
                string[] bFloats = b.GetPropertyNames(MaterialPropertyType.Float);
                if (!aFloats.SequenceEqual(bFloats))
                    return false;
                if (!aFloats.Select(x => a.GetFloat(x)).SequenceEqual(bFloats.Select(x => b.GetFloat(x))))
                    return false;

                string[] aInts = a.GetPropertyNames(MaterialPropertyType.Int);
                string[] bInts = b.GetPropertyNames(MaterialPropertyType.Int);
                if (!aInts.SequenceEqual(bInts))
                    return false;
                if (!aInts.Select(x => a.GetInteger(x)).SequenceEqual(bInts.Select(x => b.GetInteger(x))))
                    return false;

                string[] aVectors = a.GetPropertyNames(MaterialPropertyType.Vector);
                string[] bVectors = b.GetPropertyNames(MaterialPropertyType.Vector);
                if (!aVectors.SequenceEqual(bVectors))
                    return false;
                if (!aVectors.Select(x => a.GetVector(x)).SequenceEqual(bVectors.Select(x => b.GetVector(x))))
                    return false;

                string[] aTextures = a.GetPropertyNames(MaterialPropertyType.Texture);
                string[] bTextures = b.GetPropertyNames(MaterialPropertyType.Texture);
                if (!aTextures.SequenceEqual(bTextures))
                    return false;
                if (!aTextures.Select(x => a.GetTexture(x)).SequenceEqual(bTextures.Select(x => b.GetTexture(x))))
                    return false;

                string[] aMatrices = a.GetPropertyNames(MaterialPropertyType.Matrix);
                string[] bMatrices = b.GetPropertyNames(MaterialPropertyType.Matrix);
                if (!aMatrices.SequenceEqual(bMatrices))
                    return false;
                if (!aMatrices.Select(x => a.GetMatrix(x)).SequenceEqual(bMatrices.Select(x => b.GetMatrix(x))))
                    return false;

                return true;
            }

            public int GetHashCode(Material m) {
                int hash = m.shader.GetHashCode() ^ m.renderQueue;
                if (m.HasTexture("_MainTex")) {
                    var tex = m.GetTexture("_MainTex");
                    if (tex != null) {
                        hash ^= tex.GetHashCode();
                    }
                }
                if (m.HasProperty("_Color")) {
                    hash ^= m.GetColor("_Color").GetHashCode();
                }
                return hash;
            }
        }

        public void DeduplicateMaterials()
        {
            var allRenderers = gameObject.GetComponentsInChildren<Renderer>(true);
            var allUsedMaterials = allRenderers.SelectMany(x => x.sharedMaterials).Where(m => m != null).Distinct().ToArray();
            var materialGroups = allUsedMaterials.GroupBy(x => x, new MaterialAssetComparer()).ToList();

            var deduplicatedMaterialLookup = new Dictionary<Material, Material>();
            foreach (var group in materialGroups) {
                Material finalMaterial = group.Key;
                foreach (var mat in group) {
                    deduplicatedMaterialLookup[mat] = finalMaterial;
                }
            }

            foreach (var renderer in allRenderers) {
                var materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++) {
                    if (materials[i] != null && deduplicatedMaterialLookup.TryGetValue(materials[i], out var newMaterial)) {
                        if (materials[i] != newMaterial) {
                            materials[i] = newMaterial;
                            changed = true;
                        }
                    }
                }
                if (changed) {
                    renderer.sharedMaterials = materials;
                }
            }
        }

        public void OptimizeMaterialSwapMaterials()
        {
            var exclusions = optimizer.componentOptimizer.GetAllExcludedTransforms();
            foreach (var entry in context.slotSwapMaterials)
            {
                var current = gameObject.transform.GetTransformFromPath(entry.Key.path);
                if (exclusions.Contains(current))
                    continue;
                int mergedMeshCount = 1;
                int meshIndex = 0;
                string targetPath = entry.Key.path;
                if (context.oldPathToMergedPaths.TryGetValue(entry.Key.path, out var currentMergedMeshes))
                {
                    mergedMeshCount = currentMergedMeshes.Count;
                    meshIndex = currentMergedMeshes.FindIndex(list => list.Contains(entry.Key.path));
                    targetPath = currentMergedMeshes[0][0];
                }
                if (!context.optimizedSlotSwapMaterials.TryGetValue(entry.Key, out var optimizedMaterialsMap))
                {
                    context.optimizedSlotSwapMaterials[entry.Key] = optimizedMaterialsMap = new Dictionary<Material, Material>();
                }
                foreach (var material in entry.Value)
                {
                    if (!optimizedMaterialsMap.TryGetValue(material, out var optimizedMaterial))
                    {
                        optimizer.DisplayProgressBar("Optimizing swap material: " + material.name);
                        var matWrapper = new List<List<(Material, List<string>)>>() { new List<(Material, List<string>)>() { (material, new List<string> { entry.Key.path } ) } };
                        var mergedMeshIndexWrapper = new List<List<int>>() { new List<int>() { meshIndex } };
                        optimizedMaterialsMap[material] = CreateOptimizedMaterials(matWrapper, mergedMeshCount, targetPath, mergedMeshIndexWrapper)[0];
                    }
                }
            }
        }

        public Material GetFirstMaterialOnPath(string path)
        {
            if (cache_GetFirstMaterialOnPath == null)
                cache_GetFirstMaterialOnPath = new Dictionary<string, Material>();
            if (cache_GetFirstMaterialOnPath.TryGetValue(path, out var mat))
                return mat;
            var renderer = gameObject.transform.GetTransformFromPath(path)?.GetComponent<Renderer>();
            if (renderer == null || renderer.sharedMaterials.Length == 0)
                return cache_GetFirstMaterialOnPath[path] = null;
            return cache_GetFirstMaterialOnPath[path] = renderer.sharedMaterials[0];
        }

        private string GenerateUniqueName(string name, HashSet<string> usedNames)
        {
            if (usedNames.Add(name))
            {
                return name;
            }
            int count = 1;
            while (!usedNames.Add(name + " " + count))
            {
                count++;
            }
            return name + " " + count;
        }

        private Material[] CreateOptimizedMaterials(
            List<List<(Material mat, List<string> paths)>> sources,
            int meshToggleCount,
            string path,
            List<List<int>> mergedMeshIndices = null)
        {
            var materials = new Material[sources.Count];

            for (int i = 0; i < sources.Count; i++)
            {
                var sourceList = sources[i];
                if (sourceList == null || sourceList.Count == 0) continue;

                // 1. Analyze the "Leader" material (first in the list) to determine the Universal Shader configuration
                Material leaderMat = sourceList[0].mat;
                var ir = ShaderAnalyzer.Parse(leaderMat.shader, leaderMat);

                if (ir == null)
                {
                    context.Log($"[MaterialOptimizer] Failed to parse shader for {leaderMat.name}. Skipping optimization for this group.");
                    materials[i] = leaderMat;
                    continue;
                }

                // 2. Create the Universal Material
                // We assume UniversalAvatar.shader is available in the project.
                // Ideally, we find it by GUID or name.
                var universalShader = Shader.Find("WKAvatarOptimizer/UniversalAvatar");
                if (universalShader == null)
                {
                    context.Log("[MaterialOptimizer] Fatal: 'WKAvatarOptimizer/UniversalAvatar' shader not found!");
                    materials[i] = leaderMat;
                    continue;
                }

                var optimizedMaterial = new Material(universalShader);
                optimizedMaterial.name = "m_opt_" + leaderMat.name;
                
                // Apply IR configuration (Keywords, Render State, initial Properties from leader)
                ir.ApplyToMaterial(optimizedMaterial, universalShader);

                // 3. Create Texture Arrays for each texture property defined in IR
                // We must ensure the array size matches sourceList.Count
                // Index j in the array corresponds to sourceList[j]
                
                // Map of IR texture property name -> List of textures for the array
                var textureArraysMap = new Dictionary<string, List<Texture2D>>();

                // Initialize lists
                var textureProps = new[] { 
                    (ir.baseColor, "_BaseMap"), (ir.normalMap, "_NormalMap"), 
                    (ir.metallicGlossMap, "_MetallicGlossMap"), (ir.shadeMap, "_ShadeMap"), 
                    (ir.rampTexture, "_RampTexture"), (ir.matcapTexture, "_MatcapTexture"),
                    (ir.matcapTexture2, "_MatcapTexture2"), (ir.outlineMask, "_OutlineMask"),
                    (ir.emissionMap, "_EmissionMap"), (ir.dissolveMask, "_DissolveMask"),
                    (ir.detailMap, "_DetailMap")
                };

                foreach (var (prop, name) in textureProps)
                {
                    textureArraysMap[name] = new List<Texture2D>();
                }

                // Collect textures
                for (int j = 0; j < sourceList.Count; j++)
                {
                    Material mat = sourceList[j].mat;
                    
                    // Parse each material to get its specific texture assignments using PropertyMapper logic
                    // (Or rely on simple property lookup if we assume same shader family)
                    // For robustness, let's use the property names mapped in the Leader IR, 
                    // assuming the Leader's structure applies to all (which IsShaderSuperset ensures).
                    
                    // We need to find which property on 'mat' corresponds to the IR slot.
                    // Since we don't have a per-material IR here efficiently, we assume 
                    // the property names found in the Leader IR's parsing logic apply.
                    // Wait, PropertyMapper maps based on *names*. 
                    // If Mat A uses _MainTex and Mat B uses _BaseMap, and they were merged...
                    // IsShaderSuperset ensures they are compatible.
                    
                    // We'll use a simplified approach: Ask PropertyMapper what property on 'mat' maps to the role.
                    // Actually, `ir` has the texture *value* from leader. It doesn't store the *property name* it came from.
                    // We need to look up the texture again.
                    
                    // To do this correctly without re-parsing everything:
                    // Iterate all texture properties on `mat`, map them to roles, fill slots.
                    
                    // Helper to get texture for a role from a material
                    Texture2D GetTextureForRole(Material m, PropertyMapper.TextureRole targetRole)
                    {
                        var texNames = m.GetTexturePropertyNames();
                        foreach (var tName in texNames)
                        {
                            if (PropertyMapper.MapTextureToRole(tName) == targetRole)
                            {
                                return m.GetTexture(tName) as Texture2D;
                            }
                        }
                        return null;
                    }

                    textureArraysMap["_BaseMap"].Add(GetTextureForRole(mat, PropertyMapper.TextureRole.BaseColor));
                    textureArraysMap["_NormalMap"].Add(GetTextureForRole(mat, PropertyMapper.TextureRole.Normal));
                    textureArraysMap["_MetallicGlossMap"].Add(GetTextureForRole(mat, PropertyMapper.TextureRole.Metallic) ?? GetTextureForRole(mat, PropertyMapper.TextureRole.Roughness));
                    textureArraysMap["_ShadeMap"].Add(GetTextureForRole(mat, PropertyMapper.TextureRole.Shade));
                    textureArraysMap["_RampTexture"].Add(GetTextureForRole(mat, PropertyMapper.TextureRole.Ramp));
                    textureArraysMap["_MatcapTexture"].Add(GetTextureForRole(mat, PropertyMapper.TextureRole.Matcap));
                    // Secondary matcap logic is complex, simplified for now
                    textureArraysMap["_MatcapTexture2"].Add(null); 
                    textureArraysMap["_OutlineMask"].Add(GetTextureForRole(mat, PropertyMapper.TextureRole.Outline));
                    textureArraysMap["_EmissionMap"].Add(GetTextureForRole(mat, PropertyMapper.TextureRole.Emission));
                    textureArraysMap["_DissolveMask"].Add(GetTextureForRole(mat, PropertyMapper.TextureRole.Dissolve));
                    textureArraysMap["_DetailMap"].Add(GetTextureForRole(mat, PropertyMapper.TextureRole.Detail));
                }

                // Generate and Assign Arrays
                var texArraysToSet = new List<(string name, Texture2DArray array)>();
                foreach (var kvp in textureArraysMap)
                {
                    var textures = kvp.Value;
                    // Only create array if there is at least one non-null texture
                    if (textures.All(t => t == null)) continue;

                    // Fill nulls with a default texture (e.g., white/black/bump) based on role
                    // For simplicity, CombineTextures can handle nulls if we guide it, 
                    // OR we replace nulls here.
                    // Let's replace nulls with the *first non-null* texture to ensure format compatibility,
                    // or a generated default if all else fails.
                    var templateTex = textures.FirstOrDefault(t => t != null);
                    if (templateTex == null) continue; // Should be caught by All(null) check

                    for (int k = 0; k < textures.Count; k++)
                    {
                        if (textures[k] == null)
                        {
                            // Ideally use a specific default (black for emission, normal for bump).
                            // Using templateTex is a safe fallback for format match.
                            textures[k] = templateTex; 
                        }
                    }

                    var texArray = CombineTextures(textures);
                    texArraysToSet.Add((kvp.Key, texArray));
                    // Note: CombineTextures adds to context.textureArrays internally if we used the old flow,
                    // but here we call it directly. We should ensure it's tracked.
                    if (!context.textureArrays.Contains(texArray))
                        context.textureArrays.Add(texArray);
                }

                if (texArraysToSet.Count > 0)
                {
                    context.texArrayPropertiesToSet[optimizedMaterial] = texArraysToSet;
                }

                materials[i] = optimizedMaterial;
                
                // Register with context for saving later
                // We use a dummy ParsedShader or null here because we moved away from it.
                // The SaveOptimizedMaterials method relies on context.optimizedMaterials.
                // We need to adapt SaveOptimizedMaterials or provide a dummy compatible struct.
                // For now, we will null the optimizerResult part and update SaveOptimizedMaterials to handle it.
                context.optimizedMaterials.Add(optimizedMaterial, (optimizedMaterial, sourceList.Select(t => t.mat).ToList(), null));
            }

            return materials;
        }

        public void SaveOptimizedMaterials()
        {
            // Assign deferred texture arrays
            foreach (var entry in context.texArrayPropertiesToSet)
            {
                var mat = entry.Key;
                foreach (var (propName, texArray) in entry.Value)
                {
                    mat.SetTexture(propName, texArray);
                }
            }

            // Save assets
            foreach (var mat in context.optimizedMaterials.Select(o => o.Value.target))
            {
                AssetManager.CreateUniqueAsset(context, mat, mat.name + ".mat");
            }
        }

        private string GetPathToRoot(Component c)
        {
            return c.transform.GetPathToRoot(gameObject.transform);
        }

        public void CreateTextureArrays()
        {
            context.textureArrayLists.Clear();
            context.textureArrays.Clear();
            // Texture array creation is now handled dynamically in CreateOptimizedMaterials
        }

        private Texture2DArray CombineTextures(List<Texture2D> textures)
        {
            Profiler.StartSection("CombineTextures()");
            
            int width = textures.Max(t => t.width);
            int height = textures.Max(t => t.height);
            bool isLinear = IsTextureLinear(textures[0]);
            
            TextureFormat format = textures[0].format;
            if (textures.Any(t => t.format == TextureFormat.DXT5 || t.format == TextureFormat.DXT5Crunched))
            {
                format = TextureFormat.DXT5;
            }
            else if (textures.Any(t => t.format == TextureFormat.BC5))
            {
                format = TextureFormat.BC5;
            }

            var texArray = new Texture2DArray(width, height,
                textures.Count, format, true, isLinear);
            texArray.anisoLevel = textures[0].anisoLevel;
            texArray.wrapMode = textures[0].wrapMode;
            texArray.filterMode = textures[0].filterMode;

            for (int i = 0; i < textures.Count; i++)
            {
                var tex = textures[i];
                bool exactFormatMatch = tex.format == format;
                bool exactSizeMatch = tex.width == width && tex.height == height;

                if (exactFormatMatch && exactSizeMatch)
                {
                    Graphics.CopyTexture(tex, 0, texArray, i);
                }
                else
                {
                    context.Log($"[MaterialOptimizer] Converting texture {tex.name} ({tex.format} {tex.width}x{tex.height}) to match array ({format} {width}x{height}).");
                    
                    RenderTexture tempRT = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, isLinear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB);
                    tempRT.filterMode = FilterMode.Bilinear;
                    
                    Graphics.Blit(tex, tempRT);
                    
                    Texture2D tempTex = new Texture2D(width, height, TextureFormat.RGBA32, true, isLinear);
                    
                    RenderTexture.active = tempRT;
                    tempTex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    tempTex.Apply();
                    RenderTexture.active = null;
                    RenderTexture.ReleaseTemporary(tempRT);
                    
                    EditorUtility.CompressTexture(tempTex, format, TextureCompressionQuality.Best);
                    
                    Graphics.CopyTexture(tempTex, 0, texArray, i);
                    
                    UnityEngine.Object.DestroyImmediate(tempTex);
                }
            }
            Profiler.EndSection();
            texArray.name = $"{texArray.width}x{texArray.height}_{texArray.format}_{(isLinear ? "linear" : "sRGB")}_{texArray.wrapMode}_{texArray.filterMode}_2DArray";
            context.Log($"[MaterialOptimizer] Created Texture2DArray: {texArray.name} (Depth: {texArray.depth}, Mipmap: {texArray.mipmapCount > 1})");
            AssetManager.CreateUniqueAsset(context, texArray, $"{texArray.name}.asset");
            return texArray;
        }

        private bool IsTextureLinear(Texture2D tex)
        {
            if (tex == null)
                return false;
            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex)) as TextureImporter;
            if (importer == null)
                return false;
            return importer.sRGBTexture == false;
        }

        private bool CanCombineTextures(Texture a, Texture b)
        {
            if (a == b)
                return true;
            if (a == null && b is Texture2D)
                return true;
            if (a is Texture2D && b == null)
                return true;
            if (!(a is Texture2D) || !(b is Texture2D))
                return false;
            if (a.texelSize != b.texelSize)
                return false;
            var a2D = a as Texture2D;
            var b2D = b as Texture2D;
            if (a2D.format != b2D.format)
                return false;
            if (a2D.format == TextureFormat.DXT1Crunched || a2D.format == TextureFormat.DXT5Crunched)
                return false;
            if (a2D.mipmapCount != b2D.mipmapCount)
                return false;
            if (a2D.filterMode != b2D.filterMode)
                return false;
            if (a2D.wrapMode != b2D.wrapMode)
                return false;
            if (IsTextureLinear(a2D) != IsTextureLinear(b2D))
                return false;
            return true;
        }

        public void CombineAndOptimizeMaterials()
        {
            var exclusions = optimizer.componentOptimizer.GetAllExcludedTransforms();
            var skinnedMeshRenderers = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(smr => !exclusions.Contains(smr.transform) && smr.sharedMesh != null).ToArray();
            for (int meshRenderIndex = 0; meshRenderIndex < skinnedMeshRenderers.Length; meshRenderIndex++)
            {
                var meshRenderer = skinnedMeshRenderers[meshRenderIndex];
                var mesh = meshRenderer.sharedMesh;
                
                optimizer.DisplayProgressBar($"Combining materials on {meshRenderer.name} ({meshRenderIndex + 1}/{skinnedMeshRenderers.Length})");

                var props = new MaterialPropertyBlock();
                meshRenderer.GetPropertyBlock(props);
                int meshCount = props.GetInt("WKVRCOptimizer_CombinedMeshCount");
                string meshPath = meshRenderer.transform.GetPathToRoot(gameObject.transform);

                var matchedSlots = FindAllMergeAbleMaterials(new [] { meshRenderer });
                var uniqueMatchedSlots = matchedSlots.Select(list => list.Select(slot => list.First(slot2 => slot.material == slot2.material)).Distinct().ToList()).ToList();
                var mergedMeshIndices = new List<List<int>>();

                var sourceVertices = mesh.vertices;
                var hasUvSet = new bool[8] {
                    true,
                    mesh.HasVertexAttribute(VertexAttribute.TexCoord1),
                    mesh.HasVertexAttribute(VertexAttribute.TexCoord2),
                    mesh.HasVertexAttribute(VertexAttribute.TexCoord3),
                    mesh.HasVertexAttribute(VertexAttribute.TexCoord4),
                    mesh.HasVertexAttribute(VertexAttribute.TexCoord5),
                    mesh.HasVertexAttribute(VertexAttribute.TexCoord6),
                    mesh.HasVertexAttribute(VertexAttribute.TexCoord7),
                };
                int highestUsedUvSet = hasUvSet.Select((b, i) => (b, i)).Where(t => t.b).Select(t => t.i).LastOrDefault();
                var sourceUv = Enumerable.Range(0, 8).Select(i => hasUvSet[i] ? new List<Vector4>(sourceVertices.Length) : null).ToArray();
                for(int i = 0; i <= highestUsedUvSet; i++)
                {
                    if (!hasUvSet[i])
                        continue;
                    mesh.GetUVs(i, sourceUv[i]);
                    sourceUv[i] = sourceUv[i].Count != sourceVertices.Length ? Enumerable.Repeat(Vector4.zero, sourceVertices.Length).ToList() : sourceUv[i];
                }
                Color[] sourceColor = null;
                Color32[] sourceColor32 = null;
                var sourceNormals = mesh.normals;
                var sourceTangents = mesh.tangents;
                var sourceWeights = mesh.boneWeights;

                var targetUv = Enumerable.Range(0, 8).Select(i => hasUvSet[i] ? new List<Vector4>(sourceVertices.Length) : null).ToArray();
                List<Color> targetColor = null;
                List<Color32> targetColor32 = null;
                if (mesh.HasVertexAttribute(VertexAttribute.Color))
                {
                    if (mesh.GetVertexAttributeFormat(VertexAttribute.Color) == VertexAttributeFormat.UNorm8)
                    {
                        targetColor32 = new List<Color32>(sourceVertices.Length);
                        sourceColor32 = mesh.colors32;
                    }
                    else
                    {
                        targetColor = new List<Color>(sourceVertices.Length);
                        sourceColor = mesh.colors;
                    }
                }
                var targetVertices = new List<Vector3>(sourceVertices.Length);
                var targetIndices = new List<List<int>>();
                var targetTopology = new List<MeshTopology>();
                var targetNormals = new List<Vector3>(sourceVertices.Length);
                var targetTangents = new List<Vector4>(sourceVertices.Length);
                var targetWeights = new List<BoneWeight>(sourceVertices.Length);

                var targetOldVertexIndex = new List<int>();

                for (int i = 0; i < matchedSlots.Count; i++)
                {
                    var uniqueMeshIndices = new HashSet<int>();
                    var indexList = new List<int>();
                    for (int k = 0; k < matchedSlots[i].Count; k++)
                    {
                        var indexMap = new Dictionary<int, int>();
                        int internalMaterialID = uniqueMatchedSlots[i].Select((slot, index) => (slot, index)).First(t => t.slot.material == matchedSlots[i][k].material).index;
                        int materialSubMeshId = Math.Min(mesh.subMeshCount - 1, matchedSlots[i][k].index);
                        var sourceIndices = mesh.GetIndices(materialSubMeshId);
                        for (int j = 0; j < sourceIndices.Length; j++) {
                            int oldIndex = sourceIndices[j];
                            if (indexMap.TryGetValue(oldIndex, out int newIndex)) {
                                indexList.Add(newIndex);
                            } else {
                                newIndex = targetVertices.Count;
                                indexList.Add(newIndex);
                                indexMap[oldIndex] = newIndex;
                                targetUv[0].Add(new Vector4(sourceUv[0][oldIndex].x, sourceUv[0][oldIndex].y, sourceUv[0][oldIndex].z + internalMaterialID, 0));
                                for (int a = 1; a <= highestUsedUvSet; a++) {
                                    targetUv[a]?.Add(sourceUv[a][oldIndex]);
                                }
                                targetColor?.Add(sourceColor[oldIndex]);
                                targetColor32?.Add(sourceColor32[oldIndex]);
                                targetVertices.Add(sourceVertices[oldIndex]);
                                targetNormals.Add(sourceNormals[oldIndex]);
                                targetTangents.Add(sourceTangents[oldIndex]);
                                targetWeights.Add(sourceWeights[oldIndex]);
                                targetOldVertexIndex.Add(oldIndex);
                                uniqueMeshIndices.Add((int)sourceUv[0][oldIndex].z >> 12);
                            }
                        }
                    }
                    targetIndices.Add(indexList);
                    targetTopology.Add(mesh.GetTopology(Math.Min(matchedSlots[i][0].index, mesh.subMeshCount - 1)));
                    mergedMeshIndices.Add(uniqueMeshIndices.ToList());
                }

                {
                    Mesh newMesh = new Mesh();
                    newMesh.name = mesh.name;
                    newMesh.indexFormat = targetVertices.Count >= 65536
                        ? UnityEngine.Rendering.IndexFormat.UInt32
                        : UnityEngine.Rendering.IndexFormat.UInt16;
                    newMesh.SetVertices(targetVertices);
                    newMesh.bindposes = mesh.bindposes;
                    newMesh.SetBoneWeights(targetWeights.ToArray());
                    bool particleSystemUsesMeshColor = optimizer.meshOptimizer.GetParticleSystemsUsingRenderer(meshRenderer).Any(ps => ps.shape.useMeshColors);
                    if (targetColor != null && (particleSystemUsesMeshColor || targetColor.Any(c => !c.Equals(Color.white))))
                    {
                        newMesh.colors = targetColor.ToArray();
                    }
                    else if (targetColor32 != null && (particleSystemUsesMeshColor || targetColor32.Any(c => !c.Equals(new Color32(255, 255, 255, 255))))) 
                    {
                        newMesh.colors32 = targetColor32.ToArray();
                    }
                    for (int i = 0; i <= highestUsedUvSet; i++)
                    {
                        if (!hasUvSet[i])
                            continue;
                        if (targetUv[i].Any(uv => uv.w != 0))
                        {
                            newMesh.SetUVs(i, targetUv[i]);
                        }
                        else if (targetUv[i].Any(uv => uv.z != 0))
                        {
                            newMesh.SetUVs(i, targetUv[i].Select(uv => new Vector3(uv.x, uv.y, uv.z)).ToArray());
                        }
                        else if (targetUv[i].Any(uv => uv.x != 0 || uv.y != 0))
                        {
                            newMesh.SetUVs(i, targetUv[i].Select(uv => new Vector2(uv.x, uv.y)).ToArray());
                        }
                    }
                    newMesh.bounds = mesh.bounds;
                    newMesh.SetNormals(targetNormals);
                    if (targetTangents.Any(t => t != Vector4.zero))
                        newMesh.SetTangents(targetTangents.Select(t => t == Vector4.zero ? new Vector4(1, 0, 0, 1) : t).ToArray());
                    newMesh.subMeshCount = matchedSlots.Count;
                    for (int i = 0; i < matchedSlots.Count; i++)
                    {
                        newMesh.SetIndices(targetIndices[i].ToArray(), targetTopology[i], i);
                    }

                    int meshVertexCount = mesh.vertexCount;
                    int newMeshVertexCount = newMesh.vertexCount;
                    for (int i = 0; i < mesh.blendShapeCount; i++)
                    {
                        for (int j = 0; j < mesh.GetBlendShapeFrameCount(i); j++)
                        {
                            var sourceDeltaVertices = new Vector3[meshVertexCount];
                            var sourceDeltaNormals = new Vector3[meshVertexCount];
                            var sourceDeltaTangents = new Vector3[meshVertexCount];
                            mesh.GetBlendShapeFrameVertices(i, j, sourceDeltaVertices, sourceDeltaNormals, sourceDeltaTangents);
                            var targetDeltaVertices = new Vector3[newMeshVertexCount];
                            var targetDeltaNormals = new Vector3[newMeshVertexCount];
                            var targetDeltaTangents = new Vector3[newMeshVertexCount];
                            for (int k = 0; k < newMeshVertexCount; k++) {
                                var oldIndex = targetOldVertexIndex[k];
                                targetDeltaVertices[k] = sourceDeltaVertices[oldIndex];
                                targetDeltaNormals[k] = sourceDeltaNormals[oldIndex];
                                targetDeltaTangents[k] = sourceDeltaTangents[oldIndex];
                            }
                            var name = mesh.GetBlendShapeName(i);
                            var weight = mesh.GetBlendShapeFrameWeight(i, j);
                            newMesh.AddBlendShapeFrame(name, weight, targetDeltaVertices, targetDeltaNormals, targetDeltaTangents);
                        }
                    }

                    Profiler.StartSection("Mesh.Optimize()");

                    if(!optimizer.componentOptimizer.IsSPSPenetratorRoot(meshRenderer.transform)) {
                        newMesh.Optimize();
                    }
                    
                    Profiler.EndSection();

                    AssetManager.CreateUniqueAsset(context, newMesh, newMesh.name + ".asset");

                    meshRenderer.sharedMesh = newMesh;
                }

                (string path, int index) GetOriginalSlot((string path, int index) slot) {
                    if (!context.materialSlotRemap.TryGetValue(slot, out var remap)) {
                        Debug.LogWarning($"[MaterialOptimizer] Could not find original material slot for {slot.path}.{slot.index}");
                        context.materialSlotRemap[slot] = remap = slot;
                    }
                    return remap;
                }

                var allSlots = matchedSlots.SelectMany(list => list).ToList();
                var uniqueMatchedMaterials = uniqueMatchedSlots.Select(list => list.Select(slot =>
                    (slot.material, allSlots.Where(slot2 => slot2.material == slot.material).Select(slot2 => GetOriginalSlot((meshPath, slot2.index)).path).ToList())
                ).ToList()).ToList();
                var optimizedMaterials = CreateOptimizedMaterials(uniqueMatchedMaterials, meshCount > 1 ? meshCount : 0, meshPath, mergedMeshIndices);

                for (int i = 0; i < uniqueMatchedMaterials.Count; i++)
                {
                    if (uniqueMatchedMaterials[i].Count != 1 || uniqueMatchedMaterials[i][0].material == null)
                        continue;
                    var originalSlot = GetOriginalSlot((meshPath, matchedSlots[i][0].index));
                    optimizer.meshOptimizer.AddAnimationPathChange((originalSlot.path, "m_Materials.Array.data[" + originalSlot.index + "]", typeof(SkinnedMeshRenderer)),
                        (meshPath, "m_Materials.Array.data[" + i + "]", typeof(SkinnedMeshRenderer)));
                    if (!context.optimizedSlotSwapMaterials.TryGetValue(originalSlot, out var optimizedSwapMaterials))
                    {
                        context.optimizedSlotSwapMaterials[originalSlot] = optimizedSwapMaterials = new Dictionary<Material, Material>();
                    }
                    optimizedSwapMaterials[uniqueMatchedMaterials[i][0].material] = optimizedMaterials[i];
                }

                meshRenderer.sharedMaterials = optimizedMaterials;

                foreach (var ps in optimizer.meshOptimizer.GetParticleSystemsUsingRenderer(meshRenderer))
                {
                    var shape = ps.shape;
                    if (shape.useMeshMaterialIndex)
                    {
                        shape.meshMaterialIndex = uniqueMatchedSlots.FindIndex(l => l.Any(slot => slot.index == shape.meshMaterialIndex));
                    }
                }
            }
        }

        public void OptimizeMaterialsOnNonSkinnedMeshes()
        {
            var meshRenderers = gameObject.GetComponentsInChildren<MeshRenderer>(true);
            var exclusions = optimizer.componentOptimizer.GetAllExcludedTransforms();
            foreach (var meshRenderer in meshRenderers)
            {
                if (exclusions.Contains(meshRenderer.transform) || meshRenderer.GetSharedMesh() == null)
                    continue;
                optimizer.DisplayProgressBar($"Optimizing materials on {meshRenderer.name}");
                var path = meshRenderer.transform.GetPathToRoot(gameObject.transform);
                var mats = meshRenderer.sharedMaterials.Select((material, index) => (material, index)).Where(m => m.material != null).ToList();
                var alreadyOptimizedMaterials = new HashSet<Material>();
                foreach (var (material, index) in mats)
                {
                    if (context.slotSwapMaterials.TryGetValue((path, index), out var matList))
                    {
                        alreadyOptimizedMaterials.UnionWith(matList);
                    }
                }
                var toOptimize = mats.Select(t => t.material).Where(m => !alreadyOptimizedMaterials.Contains(m)).Distinct().ToList();
                var optimizeMaterialWrapper = toOptimize.Select(m => new List<(Material, List<string>)>() { (m, new List<string> { path } ) }).ToList();
                var optimizedMaterialsList = CreateOptimizedMaterials(optimizeMaterialWrapper, 0, meshRenderer.transform.GetPathToRoot(gameObject.transform));
                var optimizedMaterials = toOptimize.Select((mat, index) => (mat, index))
                    .ToDictionary(t => t.mat, t => optimizedMaterialsList[t.index]);
                var finalMaterials = new Material[meshRenderer.sharedMaterials.Length];
                for (int i = 0; i < finalMaterials.Length; i++)
                {
                    Material currentMaterial = meshRenderer.sharedMaterials[i];
                    if (currentMaterial == null) continue;

                    if (optimizedMaterials.TryGetValue(currentMaterial, out var optimizedMaterial))
                    {
                        finalMaterials[i] = optimizedMaterial;
                    }
                    else
                    {
                        if (context.optimizedSlotSwapMaterials.TryGetValue((path, i), out var optimizedSwapMaterialMap))
                        {
                            if (optimizedSwapMaterialMap.TryGetValue(currentMaterial, out var slotOptimizedMaterial))
                            {
                                finalMaterials[i] = slotOptimizedMaterial;
                            } else {
                                finalMaterials[i] = currentMaterial;
                            }
                        } else {
                            finalMaterials[i] = currentMaterial;
                        }
                    }
                }
                meshRenderer.sharedMaterials = finalMaterials;
            }
        }

        public HashSet<Material> FindAllUsedMaterials()
        {
            if (cache_FindAllUsedMaterials != null)
                return cache_FindAllUsedMaterials;
            var materials = new HashSet<Material>();
            foreach (var renderer in optimizer.componentOptimizer.GetUsedComponentsInChildren<Renderer>())
            {
                materials.UnionWith(renderer.sharedMaterials.Where(m => m != null));
            }
            return cache_FindAllUsedMaterials = materials;
        }


        public List<List<MaterialSlot>> FindAllMergeAbleMaterials(IEnumerable<Renderer> renderers)
        {
            var matched = new List<List<MaterialSlot>>();
            foreach (var renderer in renderers)
            {
                foreach (var candidate in MaterialSlot.GetAllSlotsFrom(renderer))
                {
                    bool foundMatch = false;
                    List<string> failureReasons = new List<string>();
                    for (int i = 0; i < matched.Count; i++)
                    {
                        var leader = matched[i][0].material;
                        
                        if (CanCombineMaterialsWith(matched[i], candidate, out string failReason, false))
                        {
                            matched[i].Add(candidate);
                            foundMatch = true;
                            break;
                        }
                        
                        if (IsShaderSuperset(candidate.material.shader, candidate.material, leader.shader, leader, out string promoReason))
                        {
                             if (CanCombineMaterialsWith(matched[i], candidate, out string failReason2, true))
                             {
                                 matched[i].Insert(0, candidate);
                                 foundMatch = true;
                                 context.Log($"[MaterialMerge] Promoted {candidate.material.name} to leader of Group {i} (Superset of previous leader {leader.name}).");
                                 break;
                             }
                             else
                             {
                                 failureReasons.Add($"Group {i} (Promotion Attempt): {failReason2}");
                             }
                        }
                        else
                        {
                             failureReasons.Add($"Group {i} ({leader.name}): {failReason} (And candidate is not a superset: {promoReason})");
                        }
                    }
                    if (!foundMatch)
                    {
                        if (matched.Count > 0)
                        {
                            context.Log($"[MaterialMerge] Started new merge group for {candidate.material.name} on {candidate.renderer.name}. Failed to merge with {matched.Count} existing groups:\n  " + string.Join("\n  ", failureReasons));
                        }
                        matched.Add(new List<MaterialSlot> { candidate });
                    }
                }
            }
            return matched;
        }

        private bool IsShaderSuperset(Shader shaderSuper, Material materialSuper, Shader shaderSub, Material materialSub, out string reason)
        {
            if (shaderSuper == shaderSub && materialSuper == materialSub) { reason = ""; return true; }
            if (shaderSuper == null || shaderSub == null) { reason = "Null shader"; return false; }

            var irSuper = ShaderAnalyzer.Parse(shaderSuper, materialSuper);
            var irSub = ShaderAnalyzer.Parse(shaderSub, materialSub);

            if (irSuper == null) { reason = $"Super Shader '{shaderSuper.name}' parse failed."; return false; }
            if (irSub == null) { reason = $"Sub Shader '{shaderSub.name}' parse failed."; return false; }

            // 1. Check Shading Model Compatibility
            // A more comprehensive shading model can cover a simpler one.
            // Unlit < Toon < PBR < Hybrid
            // PBR cannot be covered by Toon, Toon cannot be covered by Unlit.
            if (irSub.shadingModel == ShaderIR.ShadingModel.PBR && (irSuper.shadingModel != ShaderIR.ShadingModel.PBR && irSuper.shadingModel != ShaderIR.ShadingModel.Hybrid))
            {
                reason = $"Super shader '{irSuper.shadingModel}' cannot cover PBR shading model from '{irSub.shadingModel}'.";
                return false;
            }
            if (irSub.shadingModel == ShaderIR.ShadingModel.Toon && (irSuper.shadingModel != ShaderIR.ShadingModel.Toon && irSuper.shadingModel != ShaderIR.ShadingModel.PBR && irSuper.shadingModel != ShaderIR.ShadingModel.Hybrid))
            {
                reason = $"Super shader '{irSuper.shadingModel}' cannot cover Toon shading model from '{irSub.shadingModel}'.";
                return false;
            }
            if (irSub.shadingModel == ShaderIR.ShadingModel.Unlit && (irSuper.shadingModel != ShaderIR.ShadingModel.Unlit && irSuper.shadingModel != ShaderIR.ShadingModel.Toon && irSuper.shadingModel != ShaderIR.ShadingModel.PBR && irSuper.shadingModel != ShaderIR.ShadingModel.Hybrid))
            {
                reason = $"Super shader '{irSuper.shadingModel}' cannot cover Unlit shading model from '{irSub.shadingModel}'.";
                return false;
            }

            // 2. Check Blend Mode Compatibility
            // Opaque -> Cutout -> Alpha -> Additive/Premultiplied (in increasing order of permissiveness)
            if (irSub.blendMode == ShaderIR.BlendMode.Premultiplied && irSuper.blendMode != ShaderIR.BlendMode.Premultiplied)
            {
                reason = $"Super shader '{irSuper.blendMode}' cannot cover Premultiplied blend mode from '{irSub.blendMode}'.";
                return false;
            }
            if (irSub.blendMode == ShaderIR.BlendMode.Additive && !(irSuper.blendMode == ShaderIR.BlendMode.Additive || irSuper.blendMode == ShaderIR.BlendMode.Premultiplied))
            {
                reason = $"Super shader '{irSuper.blendMode}' cannot cover Additive blend mode from '{irSub.blendMode}'.";
                return false;
            }
            if (irSub.blendMode == ShaderIR.BlendMode.Alpha && !(irSuper.blendMode == ShaderIR.BlendMode.Alpha || irSuper.blendMode == ShaderIR.BlendMode.Additive || irSuper.blendMode == ShaderIR.BlendMode.Premultiplied))
            {
                reason = $"Super shader '{irSuper.blendMode}' cannot cover Alpha blend mode from '{irSub.blendMode}'.";
                return false;
            }
            if (irSub.blendMode == ShaderIR.BlendMode.Cutout && !(irSuper.blendMode == ShaderIR.BlendMode.Cutout || irSuper.blendMode == ShaderIR.BlendMode.Alpha || irSuper.blendMode == ShaderIR.BlendMode.Additive || irSuper.blendMode == ShaderIR.BlendMode.Premultiplied))
            {
                reason = $"Super shader '{irSuper.blendMode}' cannot cover Cutout blend mode from '{irSub.blendMode}'.";
                return false;
            }
            
            // 3. Check Cull Mode Compatibility
            // Off (double-sided) is most permissive. Front/Back are specific.
            if (irSub.cullMode == ShaderIR.CullMode.Off && irSuper.cullMode != ShaderIR.CullMode.Off)
            {
                reason = $"Super shader '{irSuper.cullMode}' cannot cover Off (double-sided) cull mode from '{irSub.cullMode}'.";
                return false;
            }
            // If sub is Front or Back, super must be the same or Off
            if ((irSub.cullMode == ShaderIR.CullMode.Front || irSub.cullMode == ShaderIR.CullMode.Back) && irSuper.cullMode != irSub.cullMode && irSuper.cullMode != ShaderIR.CullMode.Off)
            {
                 reason = $"Super shader '{irSuper.cullMode}' must match sub shader '{irSub.cullMode}' cull mode or be Off.";
                 return false;
            }

            // 4. Compare Texture Slots - Ensure all textures from sub are supported by super.
            // Check if sub-shader uses a texture property type and if the super-shader has a slot for it.
            // This implicitly assumes that if a texture property is set in the IR, the corresponding shader_feature is present.
            if (irSub.baseColor.texture != null && irSuper.baseColor.texture == null) { reason = "Missing BaseColor texture slot."; return false; }
            if (irSub.normalMap.texture != null && irSuper.normalMap.texture == null) { reason = "Missing NormalMap texture slot."; return false; }
            if (irSub.metallicGlossMap.texture != null && irSuper.metallicGlossMap.texture == null) { reason = "Missing MetallicGlossMap texture slot."; return false; }
            if (irSub.shadeMap.texture != null && irSuper.shadeMap.texture == null) { reason = "Missing ShadeMap texture slot."; return false; }
            if (irSub.rampTexture.texture != null && irSuper.rampTexture.texture == null) { reason = "Missing RampTexture texture slot."; return false; }
            if (irSub.matcapTexture.texture != null && irSuper.matcapTexture.texture == null) { reason = "Missing MatcapTexture texture slot."; return false; }
            if (irSub.matcapTexture2.texture != null && irSuper.matcapTexture2.texture == null) { reason = "Missing MatcapTexture2 texture slot."; return false; }
            if (irSub.rimIntensity > 0 && irSuper.rimIntensity == 0) { reason = "Sub shader uses Rim Lighting, but super does not."; return false; } // Assuming 0 intensity means no support
            if (irSub.useOutline && !irSuper.useOutline) { reason = "Sub shader uses Outline, but super does not."; return false; }
            if (irSub.emissionMap.texture != null && irSuper.emissionMap.texture == null) { reason = "Missing EmissionMap texture slot."; return false; }
            if (irSub.dissolveMask.texture != null && irSuper.dissolveMask.texture == null) { reason = "Missing DissolveMask texture slot."; return false; }
            if (irSub.detailMap.texture != null && irSuper.detailMap.texture == null) { reason = "Missing DetailMap texture slot."; return false; }

            // 5. Check Custom Nodes - if sub has unmapped custom nodes, it cannot be a superset of a shader without them.
            // This means we might need a CustomNode comparison logic. For now, a simple check:
            if (irSub.customNodes.Any() && !irSuper.customNodes.Any())
            {
                reason = "Sub shader has custom features not explicitly handled by super shader's IR.";
                return false;
            }
            // More sophisticated logic would involve comparing customNode names/categories.

            reason = "";
            return true;
        }

        private bool CanCombineMaterialsWith(List<MaterialSlot> list, MaterialSlot candidate, out string failureReason, bool skipShaderCheck = false)
        {
            failureReason = "";
            var candidateMat = candidate.material;
            var firstMat = list[0].material;
            if (candidateMat == null || firstMat == null)
            {
                failureReason = "Material is null.";
                return false;
            }
            
            // Get ShaderIR for both materials
            var irFirst = ShaderAnalyzer.Parse(firstMat.shader, firstMat);
            var irCandidate = ShaderAnalyzer.Parse(candidateMat.shader, candidateMat);

            if (irFirst == null) { failureReason = $"First material's shader '{firstMat.shader.name}' parse failed."; return false; }
            if (irCandidate == null) { failureReason = $"Candidate material's shader '{candidateMat.shader.name}' parse failed."; return false; }

            if (!skipShaderCheck)
            {
                if (!IsShaderSuperset(firstMat.shader, firstMat, candidateMat.shader, candidateMat, out string reason))
                {
                    failureReason = $"Different shaders: {reason}";
                    return false;
                }
            }
            
            if (list.Any(slot => slot.GetTopology() != candidate.GetTopology()))
            {
                failureReason = "Different topology.";
                return false;
            }

            bool IsAffectedByMaterialSwap(MaterialSlot slot) =>
                context.slotSwapMaterials.ContainsKey((GetPathToRoot(slot.renderer), slot.index))
                || (context.materialSlotRemap.TryGetValue((GetPathToRoot(slot.renderer), slot.index), out var remap) && context.slotSwapMaterials.ContainsKey(remap));
            
            if (IsAffectedByMaterialSwap(list[0]) || IsAffectedByMaterialSwap(candidate))
            {
                failureReason = "Affected by material swap.";
                return false;
            }

            if (optimizer.meshOptimizer.GetParticleSystemsUsingRenderer(candidate.renderer).Any(ps => ps.shape.useMeshMaterialIndex && ps.shape.meshMaterialIndex == candidate.index))
            {
                failureReason = "Particle system dependency.";
                return false;
            }

            var listMaterials = list.Select(slot => slot.material).ToArray();
            var materialComparer = new MaterialAssetComparer();
            // This comparer is for exact material equality, not for ShaderIR compatibility. Keep for now.
            // For universal shader, we don't care about exact material equality as much as IR compatibility.
            // bool allTheSameAsCandidate = listMaterials.All(mat => materialComparer.Equals(mat, candidateMat));
            
            // If more than one material in the group, and candidate is already in the group, it's fine.
            if (list.Count > 1 && listMaterials.Any(mat => mat == candidateMat))
                return true;

            // --- Start using ShaderIR for comparisons ---

            // Check blend modes
            if (irFirst.blendMode != irCandidate.blendMode)
            {
                failureReason = $"Different blend modes: {irFirst.blendMode} vs {irCandidate.blendMode}.";
                return false;
            }

            // Check cull modes
            if (irFirst.cullMode != irCandidate.cullMode)
            {
                failureReason = $"Different cull modes: {irFirst.cullMode} vs {irCandidate.cullMode}.";
                return false;
            }

            if (firstMat.renderQueue != candidateMat.renderQueue)
            {
                failureReason = "Different render queue.";
                return false;
            }
            if (firstMat.enableInstancing != candidateMat.enableInstancing)
            {
                failureReason = "Different instancing settings.";
                return false;
            }

            // For universal shader, `isVariant` might not be as relevant, as we always aim to reduce variants.
            // VRCFallback tag might still be relevant if we respect it.
            bool hasAnyMaterialVariant = listMaterials.Any(m => m.isVariant) || candidateMat.isVariant;
            if (!hasAnyMaterialVariant && firstMat.GetTag("VRCFallback", false, "None") != candidateMat.GetTag("VRCFallback", false, "None"))
            {
                failureReason = "Different VRCFallback.";
                return false;
            }
            
            // If any custom nodes (unmapped features) are present in either, assume incompatibility unless explicitly handled.
            // For now, if either has unknown/unhandled features, consider them not combinable.
            // This is a conservative check. Can be refined if CustomNode comparison logic is added.
            if (irFirst.customNodes.Any(cn => cn.category != "TextureBinding") || irCandidate.customNodes.Any(cn => cn.category != "TextureBinding")) // Allow unmapped textures for now
            {
                failureReason = "Materials have unhandled custom shader features.";
                return false;
            }

            return true;
        }
    }
}