using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
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
            if (!(sources.Any(list => list.Count > 1) || meshToggleCount > 1))
            {
                // Even if just one source, we likely want to optimize if WritePropertiesAsStaticValues is true
                // But the original check was:
                // if (!(settings.WritePropertiesAsStaticValues || sources.Any(list => list.Count > 1) || (meshToggleCount > 1 && settings.MergeSkinnedMeshesWithShaderToggle == 1)))
                // Since WritePropertiesAsStaticValues is true, this check is always false (negated true is false).
                // So we proceed.
            }
            if (!context.fusedAnimatedMaterialProperties.TryGetValue(path, out var usedMaterialProps))
                usedMaterialProps = new HashSet<string>();
            if (mergedMeshIndices == null)
                mergedMeshIndices = sources.Select(s => Enumerable.Range(0, meshToggleCount).ToList()).ToList();
            HashSet<(string name, bool isVector)> defaultAnimatedProperties = null;
            var animatedPropertyOnMeshID = new Dictionary<string, bool[]>();
            context.oldPathToMergedPaths.TryGetValue(path, out var allOriginalMeshPaths);
            var originalMeshPaths = sources.Select(l => l.SelectMany(t => t.paths).Distinct().ToList()).ToList();
            if (allOriginalMeshPaths != null && (sources.Count != 1 || sources[0].Count != 1)) {
                defaultAnimatedProperties = new HashSet<(string name, bool isVector)>();
                for (int i = 0; i < allOriginalMeshPaths.Count; i++) {
                    Dictionary<string, Vector4> defaultValuesForCurrentPath = null;
                    Material defaultMaterialForCurrentPath = GetFirstMaterialOnPath(allOriginalMeshPaths[i][0]);
                    if (!context.animatedMaterialPropertyDefaultValues.TryGetValue(path, out defaultValuesForCurrentPath))
                    {
                        defaultValuesForCurrentPath = new Dictionary<string, Vector4>();
                        context.animatedMaterialPropertyDefaultValues[path] = defaultValuesForCurrentPath;
                    }
                    if (optimizer.fxLayerOptimizer.FindAllAnimatedMaterialProperties().TryGetValue(allOriginalMeshPaths[i][0], out var animatedProps)) {
                        foreach (var prop in animatedProps) {
                            string name = prop;
                            bool isVector = name.EndsWith(".x") || name.EndsWith(".r");
                            if (isVector) {
                                name = name.Substring(0, name.Length - 2);
                            } else if ((name.Length > 2 && name[name.Length - 2] == '.')
                                    || (!isVector && (animatedProps.Contains($"{name}.x") || animatedProps.Contains($"{name}.r")))) {
                                continue;
                            }
                            if (optimizer.meshOptimizer.GetSameAnimatedPropertiesOnMergedMesh(path).Contains(name)) {
                                continue;
                            }
                            defaultAnimatedProperties.Add( ($"WKVRCOptimizer{name}_ArrayIndex{i}", isVector));
                            defaultAnimatedProperties.Add((name, isVector));
                            if (!animatedPropertyOnMeshID.TryGetValue(name, out var animatedOnMesh)) {
                                animatedOnMesh = new bool[allOriginalMeshPaths.Count];
                                animatedPropertyOnMeshID[name] = animatedOnMesh;
                            }
                            animatedOnMesh[i] = true;
                            if (defaultMaterialForCurrentPath != null && defaultMaterialForCurrentPath.HasProperty(name)) 
                            {
                                defaultValuesForCurrentPath[$"WKVRCOptimizer{name}_ArrayIndex{i}"] = isVector
                                    ? defaultMaterialForCurrentPath.GetVector(name)
                                    : new Vector4(defaultMaterialForCurrentPath.GetFloat(name), 0, 0, 0);
                            }
                        }
                    }
                    defaultAnimatedProperties.Add( ($"_IsActiveMesh{i}", false));
                }
            }
            if (!optimizer.fxLayerOptimizer.DoesFXLayerUseWriteDefaults())
                animatedPropertyOnMeshID = null;
            var materials = new Material[sources.Count];
            var parsedShader = new ParsedShader[sources.Count];
            var sanitizedMaterialNames = new string[sources.Count];
            var setShaderKeywords = new List<string>[sources.Count];
            var replace = new Dictionary<string, string>[sources.Count];
            var texturesToMerge = new HashSet<string>[sources.Count];
            var propertyTextureArrayIndex = new Dictionary<string, int>[sources.Count];
            var arrayPropertyValues = new Dictionary<string, (string type, List<string> values)>[sources.Count];
            var texturesToCheckNull = new Dictionary<string, string>[sources.Count];
            var animatedPropertyValues = new Dictionary<string, string>[sources.Count];
            var poiUsedPropertyDefines = new Dictionary<string, bool>[sources.Count];
            var stripShadowVariants = new bool[sources.Count];
            for (int i = 0; i < sources.Count; i++)
            {
                var source = sources[i].Select(t => t.mat).ToList();
                parsedShader[i] = ShaderAnalyzer.Parse(source[0]?.shader);
                if (parsedShader[i] == null || !parsedShader[i].parsedCorrectly)
                {
                    materials[i] = source[0];
                    Debug.LogWarning($"[MaterialOptimizer] Skipping material {source[0]?.name} due to unparsable shader.");
                    continue;
                }
                
                char[] invalidChars = System.IO.Path.GetInvalidFileNameChars()
                    .Append('\'')
                    .ToArray();
                stripShadowVariants[i] = source[0].renderQueue > 2500;
                sanitizedMaterialNames[i] = "s_" + System.IO.Path.GetFileNameWithoutExtension(parsedShader[i].filePath)
                    + " " + string.Join("_", source[0].name.Split(invalidChars, System.StringSplitOptions.RemoveEmptyEntries));
                texturesToMerge[i] = new HashSet<string>();
                propertyTextureArrayIndex[i] = new Dictionary<string, int>();
                arrayPropertyValues[i] = new Dictionary<string, (string type, List<string> values)>();
                poiUsedPropertyDefines[i] = new Dictionary<string, bool>();
                foreach (var mat in source)
                {
                    foreach (var prop in parsedShader[i].properties)
                    {
                        if (!mat.HasProperty(prop.name))
                            continue;
                        switch (prop.type)
                        {
                            case ParsedShader.Property.Type.Float:
                                (string type, List<string> values) propertyArray;
                                if (!arrayPropertyValues[i].TryGetValue(prop.name, out propertyArray))
                                {
                                    propertyArray.type = "float";
                                    propertyArray.values = new List<string>();
                                    arrayPropertyValues[i][prop.name] = propertyArray;
                                }
                                var value = mat.GetFloat(prop.name);
                                value = (prop.hasGammaTag) ? Mathf.GammaToLinearSpace(value) : value;
                                propertyArray.values.Add($"{value}");
                            break;
                            case ParsedShader.Property.Type.Integer:
                                if (!arrayPropertyValues[i].TryGetValue(prop.name, out propertyArray))
                                {
                                    propertyArray.type = "int";
                                    propertyArray.values = new List<string>();
                                    arrayPropertyValues[i][prop.name] = propertyArray;
                                }
                                propertyArray.values.Add("" + mat.GetInteger(prop.name));
                            break;
                            case ParsedShader.Property.Type.Color:
                            case ParsedShader.Property.Type.ColorHDR:
                            case ParsedShader.Property.Type.Vector:
                                if (!arrayPropertyValues[i].TryGetValue(prop.name, out propertyArray))
                                {
                                    propertyArray.type = "float4";
                                    propertyArray.values = new List<string>();
                                    arrayPropertyValues[i][prop.name] = propertyArray;
                                }
                                var col = mat.GetColor(prop.name);
                                col = (prop.type == ParsedShader.Property.Type.Color || prop.hasGammaTag) ? col.linear : col;
                                propertyArray.values.Add($"float4({col.r}, {col.g}, {col.b}, {col.a})");
                                break;
                            case ParsedShader.Property.Type.Texture2D:
                                if (!arrayPropertyValues[i].TryGetValue("arrayIndex" + prop.name, out var textureArray))
                                {
                                    arrayPropertyValues[i]["arrayIndex" + prop.name] = ("float", new List<string>());
                                    arrayPropertyValues[i]["shouldSample" + prop.name] = ("bool", new List<string>());
                                }
                                var tex = mat.GetTexture(prop.name);
                                var tex2D = tex as Texture2D;
                                int index = 0;
                                if (tex2D != null)
                                {
                                    int texArrayIndex = context.textureArrayLists.FindIndex(l => l.Contains(tex2D));
                                    if (texArrayIndex != -1)
                                    {
                                        index = context.textureArrayLists[texArrayIndex].IndexOf(tex2D);
                                        texturesToMerge[i].Add(prop.name);
                                        propertyTextureArrayIndex[i][prop.name] = texArrayIndex;
                                    }
                                }
                                arrayPropertyValues[i]["arrayIndex" + prop.name].values.Add("" + index);
                                arrayPropertyValues[i]["shouldSample" + prop.name].values.Add((tex != null).ToString().ToLowerInvariant());
                                if (!arrayPropertyValues[i].TryGetValue(prop.name + "_TexelSize", out propertyArray))
                                {
                                    propertyArray.type = "float4";
                                    propertyArray.values = new List<string>();
                                    arrayPropertyValues[i][prop.name + "_TexelSize"] = propertyArray;
                                }
                                var texelSize = new Vector2(tex?.width ?? 4, tex?.height ?? 4);
                                propertyArray.values.Add($"float4(1.0 / {texelSize.x}, 1.0 / {texelSize.y}, {texelSize.x}, {texelSize.y})");
                                break;
                        }
                    }
                }

                replace[i] = new Dictionary<string, string>();
                foreach (var tuple in arrayPropertyValues[i].ToList())
                {
                    if (usedMaterialProps.Contains(tuple.Key) && !(meshToggleCount > 1))
                    {
                        arrayPropertyValues[i].Remove(tuple.Key);
                    }
                    else if (tuple.Value.values.All(v => v == tuple.Value.values[0]))
                    {
                        arrayPropertyValues[i].Remove(tuple.Key);
                        replace[i][tuple.Key] = tuple.Value.values[0];
                    }
                }
                
                // if (!settings.WritePropertiesAsStaticValues) block removed as we assume true

                texturesToCheckNull[i] = new Dictionary<string, string>();
                foreach (var prop in parsedShader[i].properties)
                {
                    if (prop.type == ParsedShader.Property.Type.Texture2D)
                    {
                        if (arrayPropertyValues[i].ContainsKey("shouldSample" + prop.name))
                        {
                            texturesToCheckNull[i][prop.name] = prop.defaultValue;
                        }
                    }
                    switch (prop.type)
                    {
                        case ParsedShader.Property.Type.Texture2D:
                        case ParsedShader.Property.Type.Texture2DArray:
                        case ParsedShader.Property.Type.Texture3D:
                        case ParsedShader.Property.Type.TextureCube:
                        case ParsedShader.Property.Type.TextureCubeArray:
                            bool isUsed = arrayPropertyValues[i].ContainsKey($"shouldSample{prop.name}")
                                || source[0].GetTexture(prop.name) != null;
                            poiUsedPropertyDefines[i][$"PROP{prop.name.ToUpper()}"] = isUsed;
                            break;
                    }
                }

                animatedPropertyValues[i] = new Dictionary<string, string>();
                if (meshToggleCount > 1) {
                    foreach (var propName in usedMaterialProps) {
                        if (optimizer.meshOptimizer.GetSameAnimatedPropertiesOnMergedMesh(path).Contains(propName)) {
                            arrayPropertyValues[i].Remove(propName);
                            replace[i].Remove(propName);
                            continue;
                        }
                        if (originalMeshPaths != null) {
                            bool skipProp = true;
                            foreach (var originalPath in originalMeshPaths[i]) {
                                if (optimizer.fxLayerOptimizer.FindAllAnimatedMaterialProperties().TryGetValue(originalPath, out var props)) {
                                    if (props.Contains(propName) || props.Contains(propName + ".x") || props.Contains(propName + ".r")) {
                                        skipProp = false;
                                        break;
                                    }
                                }
                            }
                            if (skipProp)
                                continue;
                        }
                        if (parsedShader[i].propertyTable.TryGetValue(propName, out var prop)) {
                            string type = "float4";
                            if (prop.type == ParsedShader.Property.Type.Float)
                                type = "float";
                            if (prop.type == ParsedShader.Property.Type.Integer)
                                type = "int";
                            animatedPropertyValues[i][propName] = type;
                        }
                    }
                }

                setShaderKeywords[i] = parsedShader[i].shaderFeatureKeyWords.Where(k => source[0].IsKeywordEnabled(k)).ToList();
            }

            var optimizedShader = new ShaderOptimizer.OptimizedShader[sources.Count];
            var basicMergedMeshPaths = allOriginalMeshPaths?.Select(list => string.Join(", ", list)).ToList();
            Profiler.StartSection("ShaderOptimizer.Run()");
            Parallel.For(0, sources.Count, i =>
            {
                if (parsedShader[i] != null && parsedShader[i].parsedCorrectly)
                {
                    optimizedShader[i] = ShaderOptimizer.Run(
                        parsedShader[i],
                        replace[i],
                        meshToggleCount,
                        basicMergedMeshPaths,
                        i == 0 ? defaultAnimatedProperties : null,
                        mergedMeshIndices[i],
                        arrayPropertyValues[i],
                        texturesToCheckNull[i],
                        texturesToMerge[i],
                        animatedPropertyValues[i],
                        setShaderKeywords[i],
                        poiUsedPropertyDefines[i],
                        sanitizedMaterialNames[i],
                        stripShadowVariants[i],
                        animatedPropertyOnMeshID
                    );
                }
            });
            Profiler.EndSection();

            for (int i = 0; i < sources.Count; i++)
            {
                var source = sources[i].Select(t => t.mat).ToList();
                if (parsedShader[i] == null || !parsedShader[i].parsedCorrectly)
                    continue;

                optimizer.DisplayProgressBar($"Optimizing shader {source[0].shader.name} ({i + 1}/{sources.Count})");
                var shaderName = optimizedShader[i].name;
                var shaderFilePath = AssetDatabase.GenerateUniqueAssetPath(context.trashBinPath + shaderName + ".shader");
                var name = System.IO.Path.GetFileNameWithoutExtension(shaderFilePath);
                optimizedShader[i].SetName(name);
                foreach (var opt in optimizedShader[i].files)
                {
                    var filePath = shaderFilePath;
                    if (opt.name != "Shader")
                    {
                        filePath = context.trashBinPath + opt.name;
                    }
                    System.IO.File.WriteAllLines(filePath, opt.lines);
                    context.optimizedMaterialImportPaths.Add(filePath);
                }
                var optimizedMaterial = new Material(Shader.Find("Unlit/Texture"));
                optimizedMaterial.shader = null;
                optimizedMaterial.name = "m_" + name.Substring(2);
                materials[i] = optimizedMaterial;
                context.optimizedMaterials.Add(optimizedMaterial, (optimizedMaterial, source, optimizedShader[i]));
                var arrayList = new List<(string name, Texture2DArray array)>();
                foreach (var texArray in propertyTextureArrayIndex[i])
                {
                    arrayList.Add((texArray.Key, context.textureArrays[texArray.Value]));
                }
                if (arrayList.Count > 0)
                {
                    context.texArrayPropertiesToSet[optimizedMaterial] = arrayList;
                }
            }
            return materials;
        }

        public void SaveOptimizedMaterials()
        {
            Profiler.StartSection("AssetDatabase.ImportAsset()");
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach(var importPath in context.optimizedMaterialImportPaths)
                {
                    AssetDatabase.ImportAsset(importPath);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            Profiler.EndSection();

            int i = 0;
            foreach (var entry in context.optimizedMaterials.Values)
            {
                var mat = entry.target;
                var sources = entry.sources;
                var source = sources[0];
                var optimizedShader = entry.optimizerResult;
                optimizer.DisplayProgressBar($"Loading optimized shader {mat.name} {0.7f + 0.2f * (i / (float)context.optimizedMaterials.Count)}");
                Profiler.StartSection("AssetDatabase.LoadAssetAtPath<Shader>()");
                var shader = AssetDatabase.LoadAssetAtPath<Shader>($"{context.trashBinPath}{optimizedShader.name}.shader");
                Profiler.StartNextSection("mat.shader = shader");
                mat.shader = shader;
                mat.renderQueue = source.renderQueue;
                mat.enableInstancing = source.enableInstancing;
                Profiler.StartNextSection("CopyMaterialProperties");
                for (int j = 0; j < source.shader.passCount; j++) {
                    var lightModeValue = source.shader.FindPassTagValue(j, new ShaderTagId("LightMode"));
                    if (!string.IsNullOrEmpty(lightModeValue.name)) {
                        if (!source.GetShaderPassEnabled(lightModeValue.name)) {
                            mat.SetShaderPassEnabled(lightModeValue.name, false);
                        }
                    }
                }
                var texArrayProperties = new HashSet<string>();
                if (context.texArrayPropertiesToSet.TryGetValue(mat, out var texArrays))
                {
                    foreach (var texArray in texArrays)
                    {
                        string texArrayName = texArray.name;
                        if (texArrayName == "_MainTex")
                        {
                            texArrayName = "_MainTexButNotQuiteSoThatUnityDoesntCry";
                        }
                        mat.SetTexture(texArrayName, texArray.array);
                        mat.SetTextureOffset(texArrayName, source.GetTextureOffset(texArray.name));
                        mat.SetTextureScale(texArrayName, source.GetTextureScale(texArray.name));
                        texArrayProperties.Add(texArrayName);
                    }
                }
                foreach (var prop in optimizedShader.tex2DProperties)
                {
                    if (!source.HasProperty(prop) || texArrayProperties.Contains(prop))
                        continue;
                    var tex = sources.Select(m => m.GetTexture(prop)).FirstOrDefault(t => t != null);
                    mat.SetTexture(prop, tex);
                    mat.SetTextureOffset(prop, source.GetTextureOffset(prop));
                    mat.SetTextureScale(prop, source.GetTextureScale(prop));
                }
                foreach (var prop in optimizedShader.tex3DCubeProperties)
                {
                    if (!source.HasProperty(prop))
                        continue;
                    var tex = sources.Select(m => m.GetTexture(prop)).FirstOrDefault(t => t != null);
                    mat.SetTexture(prop, tex);
                }
                foreach (var prop in optimizedShader.floatProperties)
                {
                    if (!source.HasProperty(prop))
                        continue;
                    mat.SetFloat(prop, source.GetFloat(prop));
                }
                foreach (var prop in optimizedShader.colorProperties)
                {
                    if (!source.HasProperty(prop))
                        continue;
                    mat.SetColor(prop, source.GetColor(prop));
                }
                foreach (var prop in optimizedShader.integerProperties)
                {
                    if (!source.HasProperty(prop))
                        continue;
                    mat.SetInteger(prop, source.GetInt(prop));
                }
                var vrcFallback = source.GetTag("VRCFallback", false, "not_set");
                if (vrcFallback != "not_set")
                {
                    mat.SetOverrideTag("VRCFallback", vrcFallback);
                }
                
                mat.SetFloat("WKVRCOptimizer_Zero", 0.0f);
                
                Profiler.EndSection();
            }

            var skinnedMeshRenderers = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var meshRenderer in skinnedMeshRenderers)
            {
                var mesh = meshRenderer.sharedMesh;
                if (mesh == null)
                    continue;

                var props = new MaterialPropertyBlock();
                meshRenderer.GetPropertyBlock(props);
                int meshCount = props.GetInt("WKVRCOptimizer_CombinedMeshCount");

                if (context.fusedAnimatedMaterialProperties.TryGetValue(meshRenderer.transform.GetPathToRoot(gameObject.transform), out var animatedProperties))
                {
                    foreach (var mat in meshRenderer.sharedMaterials)
                    {
                        if (mat == null)
                            continue;
                        foreach (var animPropName in animatedProperties)
                        {
                            var propName = animPropName;
                            bool isVector = propName.EndsWith(".x") || propName.EndsWith(".r");
                            if (isVector) {
                                propName = propName.Substring(0, propName.Length - 2);
                            } else if (propName.Length > 2 && propName[propName.Length - 2] == '.') {
                                continue;
                            } else if (animatedProperties.Contains($"{propName}.x") || animatedProperties.Contains($"{propName}.r")) {
                                isVector = true;
                            }
                            for (int mID = 0; mID < meshCount; mID++) {
                                var propArrayName = $"WKVRCOptimizer{propName}_ArrayIndex{mID}";
                                if (!mat.HasProperty(propArrayName))
                                    continue;
                                var signal = optimizer.fxLayerOptimizer.DoesFXLayerUseWriteDefaults() ? 0.0f : float.NaN;
                                if (isVector) {
                                    mat.SetVector(propArrayName, new Vector4(signal, signal, signal, signal));
                                } else {
                                    mat.SetFloat(propArrayName, signal);
                                }
                            }
                        }
                        if (optimizer.fxLayerOptimizer.DoesFXLayerUseWriteDefaults() && context.animatedMaterialPropertyDefaultValues.TryGetValue(meshRenderer.transform.GetPathToRoot(gameObject.transform), out var defaultValues))
                        {
                            foreach (var defaultProp in defaultValues)
                            {
                                if (mat.HasFloat(defaultProp.Key))
                                {
                                    mat.SetFloat(defaultProp.Key, defaultProp.Value.x);
                                }
                                else if (mat.HasVector(defaultProp.Key))
                                {
                                    mat.SetVector(defaultProp.Key, defaultProp.Value);
                                }
                            }
                        }
                    }
                }

                if (meshCount > 1)
                {
                    foreach (var mat in meshRenderer.sharedMaterials)
                    {
                        if (mat == null)
                            continue;
                        for (int mID = 0; mID < meshCount; mID++) {
                            var propName = $"_IsActiveMesh{mID}";
                            mat.SetFloat(propName, props.GetFloat(propName));
                        }
                    }
                }
            }

            foreach (var mat in context.optimizedMaterials.Select(o => o.Value.target))
            {
                AssetManager.CreateUniqueAsset(context, mat, mat.name + ".mat");
            }
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
                    for (int i = 0; i < matched.Count; i++)
                    {
                        if (CanCombineMaterialsWith(matched[i], candidate))
                        {
                            matched[i].Add(candidate);
                            foundMatch = true;
                            break;
                        }
                    }
                    if (!foundMatch)
                    {
                        matched.Add(new List<MaterialSlot> { candidate });
                    }
                }
            }
            return matched;
        }

        private bool CanCombineMaterialsWith(List<MaterialSlot> list, MaterialSlot candidate)
        {
            if (list[0].material != candidate.material)
                return false;
            if (optimizer.meshOptimizer.GetParticleSystemsUsingRenderer(candidate.renderer).Any(ps => ps.shape.useMeshMaterialIndex && ps.shape.meshMaterialIndex == candidate.index))
                return false;
            
            return true;
        }
    }
}