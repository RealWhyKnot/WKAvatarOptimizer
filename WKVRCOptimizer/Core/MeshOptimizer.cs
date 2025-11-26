#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using WKVRCOptimizer.Data;
using WKVRCOptimizer.Extensions;
using WKVRCOptimizer.Core.Util;
using UnityEditor;
using System;
using Math = System.Math;
using Object = UnityEngine.Object;
using VRC.SDK3.Avatars.Components;

namespace WKVRCOptimizer.Core
{
    public class MeshOptimizer
    {
        private readonly OptimizationContext context;
        private readonly CacheManager cacheManager;
        private readonly Settings settings;
        private readonly GameObject root;
        private readonly AvatarOptimizer mainInstance;

        private static void _Log(string message) {
            Debug.Log($"[MeshOptimizer] {message}");
        }

        public MeshOptimizer(OptimizationContext context, CacheManager cacheManager, Settings settings, GameObject root, AvatarOptimizer mainInstance)
        {
            _Log("MeshOptimizer constructor called.");
            this.context = context;
            this.cacheManager = cacheManager;
            this.settings = settings;
            this.root = root;
            this.mainInstance = mainInstance;
            _Log("MeshOptimizer constructor finished.");
        }

        private string GetPathToRoot(Transform t) {
            // _Log($"GetPathToRoot(Transform) called for {t?.name ?? "null"}"); // Too verbose, called very frequently
            return t.GetPathToRoot(root.transform);
        }
        private string GetPathToRoot(GameObject obj) {
            // _Log($"GetPathToRoot(GameObject) called for {obj?.name ?? "null"}"); // Too verbose
            return obj.transform.GetPathToRoot(root.transform);
        }
        private string GetPathToRoot(Component c) {
            // _Log($"GetPathToRoot(Component) called for {c?.name ?? "null"}"); // Too verbose
            return c.transform.GetPathToRoot(root.transform);
        }
        private Transform GetTransformFromPath(string path) {
            _Log($"GetTransformFromPath() called for path: {path}");
            Transform result = root.transform.GetTransformFromPath(path);
            _Log($"GetTransformFromPath() finished for path: {path}. Result: {result?.name ?? "null"}");
            return result;
        }

        private Dictionary<Renderer, List<ParticleSystem>> cache_ParticleSystemsUsingRenderer = null;
        public List<ParticleSystem> GetParticleSystemsUsingRenderer(Renderer candidate)
        {
            _Log($"GetParticleSystemsUsingRenderer() called for renderer: {candidate?.name ?? "null"}");
            if (cache_ParticleSystemsUsingRenderer == null)
            {
                _Log("cache_ParticleSystemsUsingRenderer is null, initializing...");
                cache_ParticleSystemsUsingRenderer = new Dictionary<Renderer, List<ParticleSystem>>();
                foreach (var ps in mainInstance.componentOptimizer.GetUsedComponentsInChildren<ParticleSystem>())
                {
                    Renderer renderer = ps.shape.shapeType == ParticleSystemShapeType.SkinnedMeshRenderer ? ps.shape.skinnedMeshRenderer : null;
                    renderer = ps.shape.shapeType == ParticleSystemShapeType.MeshRenderer ? ps.shape.meshRenderer : renderer;
                    if (renderer != null)
                    {
                        if (!cache_ParticleSystemsUsingRenderer.TryGetValue(renderer, out var list))
                        {
                            list = new List<ParticleSystem>();
                            cache_ParticleSystemsUsingRenderer[renderer] = list;
                        }
                        list.Add(ps);
                    }
                }
                _Log($"cache_ParticleSystemsUsingRenderer initialized with {cache_ParticleSystemsUsingRenderer.Count} entries.");
            }
            if (cache_ParticleSystemsUsingRenderer.TryGetValue(candidate, out var result))
            {
                _Log($"GetParticleSystemsUsingRenderer() finished for renderer: {candidate?.name ?? "null"}. Found {result.Count} particle systems from cache.");
                return result;
            }
            var newResult = new List<ParticleSystem>();
            cache_ParticleSystemsUsingRenderer[candidate] = newResult;
            _Log($"GetParticleSystemsUsingRenderer() finished for renderer: {candidate?.name ?? "null"}. Found 0 particle systems, added to cache.");
            return newResult;
        }

        private bool IsBasicCombinableRenderer(Renderer candidate)
        {
            _Log($"IsBasicCombinableRenderer() called for renderer: {candidate?.name ?? "null"}");
            if (candidate == null) {
                _Log($"IsBasicCombinableRenderer() returning false for null candidate.");
                return false;
            }
            if (!(candidate is SkinnedMeshRenderer || candidate is MeshRenderer)) {
                _Log($"IsBasicCombinableRenderer() returning false for {candidate.name}: Not a SkinnedMeshRenderer or MeshRenderer.");
                return false;
            }
            if (candidate.GetComponent<UnityEngine.Cloth>() != null) {
                _Log($"IsBasicCombinableRenderer() returning false for {candidate.name}: Has Cloth component.");
                return false;
            }
            if (candidate is MeshRenderer && (candidate.gameObject.layer == 12 || !settings.MergeStaticMeshesAsSkinned)) {
                _Log($"IsBasicCombinableRenderer() returning false for {candidate.name}: MeshRenderer with layer 12 or MergeStaticMeshesAsSkinned is false.");
                return false;
            }
            if (candidate.GetSharedMesh() == null) {
                _Log($"IsBasicCombinableRenderer() returning false for {candidate.name}: SharedMesh is null.");
                return false;
            }
            if (candidate.GetSharedMesh().subMeshCount == 0) {
                _Log($"IsBasicCombinableRenderer() returning false for {candidate.name}: SharedMesh has no submeshes.");
                return false;
            }
            if (GetParticleSystemsUsingRenderer(candidate).Any(ps => !ps.shape.useMeshMaterialIndex || candidate is MeshRenderer)) {
                _Log($"IsBasicCombinableRenderer() returning false for {candidate.name}: ParticleSystem using renderer with unsupported shape settings.");
                return false;
            }
            // check if any material is null
            if (candidate.sharedMaterials.Any(m => m == null)) {
                _Log($"IsBasicCombinableRenderer() returning false for {candidate.name}: Contains null material.");
                return false;
            }
            // check if any material has a shader that is not supported
            if (candidate.sharedMaterials.Any(m => !AvatarOptimizer.IsMaterialReadyToCombineWithOtherMeshes(m))) {
                _Log($"IsBasicCombinableRenderer() returning false for {candidate.name}: Contains uncombinable material.");
                return false;
            }
            _Log($"IsBasicCombinableRenderer() returning true for {candidate.name}.");
            return true;
        }

        private bool IsShaderToggleCombinableRenderer(Renderer candidate)
        {
            _Log($"IsShaderToggleCombinableRenderer() called for renderer: {candidate?.name ?? "null"}");
            if (!IsBasicCombinableRenderer(candidate)) {
                _Log($"IsShaderToggleCombinableRenderer() returning false for {candidate.name}: Not basic combinable.");
                return false;
            }
            foreach (var slot in MaterialSlot.GetAllSlotsFrom(candidate))
            {
                if (!AvatarOptimizer.IsMaterialReadyToCombineWithOtherMeshes(slot.material)) {
                    _Log($"IsShaderToggleCombinableRenderer() returning false for {candidate.name}: Material slot {slot.index} ({slot.material?.name}) is not ready for combining.");
                    return false;
                }
                if (context.slotSwapMaterials.TryGetValue((GetPathToRoot(slot.renderer), slot.index), out var materials))
                {
                    if (!materials.Any(material => AvatarOptimizer.IsMaterialReadyToCombineWithOtherMeshes(material))) {
                        _Log($"IsShaderToggleCombinableRenderer() returning false for {candidate.name}: Material swap for slot {slot.index} contains uncombinable material.");
                        return false;
                    }
                }
            }
            _Log($"IsShaderToggleCombinableRenderer() returning true for {candidate.name}.");
            return true;
        }

        public bool GetRendererDefaultEnabledState(Renderer r) {
            _Log($"GetRendererDefaultEnabledState() called for renderer: {r?.name ?? "null"}");
            bool result = r.enabled && r.gameObject.activeSelf;
            _Log($"GetRendererDefaultEnabledState() finished for renderer: {r?.name ?? "null"}. Result: {result}");
            return result;
        }

        private bool CanCombineRendererWithBasicMerge(List<Renderer> list, Renderer candidate, bool withNaNimation)
        {
            _Log($"CanCombineRendererWithBasicMerge() called for candidate: {candidate?.name ?? "null"} (withNaNimation: {withNaNimation})");
            if (!IsBasicCombinableRenderer(candidate)) {
                _Log($"CanCombineRendererWithBasicMerge() returning false for {candidate?.name ?? "null"}: Not basic combinable.");
                return false;
            }
            if (list.Any(r => !IsBasicCombinableRenderer(r))) {
                _Log($"CanCombineRendererWithBasicMerge() returning false for {candidate?.name ?? "null"}: List contains non-basic combinable renderer.");
                return false;
            }
            if (list.Count == 1 || list.Skip(1).All(r => RenderersHaveSameAnimationCurves(list[0], r, withNaNimation))) {
                bool result = RenderersHaveSameAnimationCurves(list[0], candidate, withNaNimation);
                _Log($"CanCombineRendererWithBasicMerge() returning {result} for {candidate?.name ?? "null"}: Renderers have same animation curves.");
                return result;
            }
            _Log($"CanCombineRendererWithBasicMerge() returning false for {candidate?.name ?? "null"}.");
            return false;
        }

        public bool RenderersHaveSameAnimationCurves(Renderer a, Renderer b, bool withNaNimation)
        {
            _Log($"RenderersHaveSameAnimationCurves() called for A: {a?.name ?? "null"}, B: {b?.name ?? "null"} (withNaNimation: {withNaNimation})");
            if (cacheManager.cache_withNaNimation == null || cacheManager.cache_withNaNimation != withNaNimation)
            {
                _Log("Cache for animation curves invalidated due to withNaNimation change.");
                cacheManager.cache_withNaNimation = withNaNimation;
                cacheManager.cache_FindAllAnimationClipsAffectingRenderer = null;
                cacheManager.cache_RendererHaveSameAnimationCurves = null;
            }
            if (cacheManager.cache_RendererHaveSameAnimationCurves == null)
                cacheManager.cache_RendererHaveSameAnimationCurves = new Dictionary<(Renderer, Renderer), bool>();
            var aPath = a.transform.GetPathToRoot(root.transform);
            var bPath = b.transform.GetPathToRoot(root.transform);
            if (aPath.CompareTo(bPath) > 0) {
                var temp = aPath;
                aPath = bPath;
                bPath = temp;
                var temp2 = a;
                a = b;
                b = temp2;
            }
            if (cacheManager.cache_RendererHaveSameAnimationCurves.TryGetValue((a, b), out var result)) {
                _Log($"RenderersHaveSameAnimationCurves() finished for A: {a?.name ?? "null"}, B: {b?.name ?? "null"}. Returning cached result: {result}");
                return result;
            }
            _Log($"RenderersHaveSameAnimationCurves() cache miss for A: {a?.name ?? "null"}, B: {b?.name ?? "null"}. Calculating...");

            bool IsRelevantBindingForSkinnedMeshMerge(EditorCurveBinding binding) {
                if(typeof(Renderer).IsAssignableFrom(binding.type) && binding.propertyName.StartsWithSimple("material.")) {
                    Renderer renderer = root.transform.GetTransformFromPath(binding.path)?.GetComponent<Renderer>();
                    if(renderer) {
                        int materialSlotCount = renderer.sharedMaterials.Length;
                        var swaps = mainInstance.fxLayerOptimizer.FindAllMaterialSwapMaterials();
                        string materialProperty = binding.propertyName.Substring("material.".Length).Split(new char[] { '.' }, 2)[0];
                        bool propertyExists = false;
                        for(int i = 0; i < materialSlotCount && !propertyExists; ++i) {
                            if(renderer.sharedMaterials[i] != null && renderer.sharedMaterials[i].HasProperty(materialProperty)) {
                                propertyExists = true;
                                break;
                            }
                            if(swaps.TryGetValue((binding.path, i), out var mats)) {
                                foreach(Material mat in mats) {
                                    if(mat != null && mat.HasProperty(materialProperty)) {
                                        propertyExists = true;
                                        break;
                                    }
                                }
                            }
                        }
                        if(!propertyExists)
                            return false;
                    }
                }
                if (withNaNimation && this.CanUseNaNimationOnMesh(binding.path)) {
                    if (typeof(Renderer).IsAssignableFrom(binding.type))
                        return !binding.propertyName.StartsWithSimple("blendShape.") && binding.propertyName != "m_Enabled";
                } else {
                    if (typeof(Renderer).IsAssignableFrom(binding.type))
                        return !binding.propertyName.StartsWithSimple("blendShape.");
                    if (binding.type == typeof(GameObject) && binding.propertyName == "m_IsActive")
                        return true;
                }
                return false;
            }
            Dictionary<string, HashSet<AnimationClip>> FindAllAnimationClipsAffectingRenderer()
            {
                if (cacheManager.cache_FindAllAnimationClipsAffectingRenderer != null) {
                    _Log("Returning cached animation clips affecting renderer.");
                    return cacheManager.cache_FindAllAnimationClipsAffectingRenderer;
                }
                _Log("Calculating animation clips affecting renderer...");
                cacheManager.cache_FindAllAnimationClipsAffectingRenderer = new Dictionary<string, HashSet<AnimationClip>>();
                foreach (var clip in mainInstance.fxLayerOptimizer.GetAllUsedAnimationClips())
                {
                    foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                    {
                        if (IsRelevantBindingForSkinnedMeshMerge(binding))
                        {
                            if (!cacheManager.cache_FindAllAnimationClipsAffectingRenderer.TryGetValue(binding.path, out var clips))
                            {
                                cacheManager.cache_FindAllAnimationClipsAffectingRenderer[binding.path] = clips = new HashSet<AnimationClip>();
                            }
                            clips.Add(clip);
                        }
                    }
                }
                _Log($"Finished calculating animation clips affecting renderer. Found {cacheManager.cache_FindAllAnimationClipsAffectingRenderer.Count} entries.");
                return cacheManager.cache_FindAllAnimationClipsAffectingRenderer;
            }
            var allAnimationClipsAffectingRenderer = FindAllAnimationClipsAffectingRenderer();
            var aHasClips = allAnimationClipsAffectingRenderer.TryGetValue(aPath, out var aClips);
            var bHasClips = allAnimationClipsAffectingRenderer.TryGetValue(bPath, out var bClips);
            
            if (aHasClips != bHasClips) {
                _Log($"RenderersHaveSameAnimationCurves() for A: {a?.name ?? "null"}, B: {b?.name ?? "null"}. One has clips, other does not. Result: false.");
                return cacheManager.cache_RendererHaveSameAnimationCurves[(a, b)] = false;
            }
            if (!aHasClips) {
                _Log($"RenderersHaveSameAnimationCurves() for A: {a?.name ?? "null"}, B: {b?.name ?? "null"}. Neither has clips. Result: true.");
                return cacheManager.cache_RendererHaveSameAnimationCurves[(a, b)] = true;
            }
            if (!aClips.SetEquals(bClips)) {
                _Log($"RenderersHaveSameAnimationCurves() for A: {a?.name ?? "null"}, B: {b?.name ?? "null"}. Clips sets are different. Result: false.");
                return cacheManager.cache_RendererHaveSameAnimationCurves[(a, b)] = false;
            }
            foreach (var clip in aClips)
            {
                var bindings = AnimationUtility.GetCurveBindings(clip);
                foreach (var binding in bindings)
                {
                    if (!IsRelevantBindingForSkinnedMeshMerge(binding))
                        continue;
                    var otherBinding = binding;
                    if (binding.path == aPath)
                    {
                        otherBinding.path = bPath;
                        if (!bindings.Contains(otherBinding)) {
                            _Log($"RenderersHaveSameAnimationCurves() for A: {a?.name ?? "null"}, B: {b?.name ?? "null"}. Other binding for {binding.propertyName} not found. Result: false.");
                            return cacheManager.cache_RendererHaveSameAnimationCurves[(a, b)] = false;
                        }
                    }
                    else if (binding.path == bPath)
                    {
                        otherBinding.path = aPath;
                        if (!bindings.Contains(otherBinding)) {
                            _Log($"RenderersHaveSameAnimationCurves() for A: {a?.name ?? "null"}, B: {b?.name ?? "null"}. Other binding for {binding.propertyName} not found. Result: false.");
                            return cacheManager.cache_RendererHaveSameAnimationCurves[(a, b)] = false;
                        }
                    }
                    else
                    {
                        continue;
                    }
                    var curve = AnimationUtility.GetEditorCurve(clip, binding);
                    var otherCurve = AnimationUtility.GetEditorCurve(clip, otherBinding);
                    if (curve.keys.Length != otherCurve.keys.Length) {
                        _Log($"RenderersHaveSameAnimationCurves() for A: {a?.name ?? "null"}, B: {b?.name ?? "null"}. Curve key lengths differ for {binding.propertyName}. Result: false.");
                        return cacheManager.cache_RendererHaveSameAnimationCurves[(a, b)] = false;
                    }
                    for (int i = 0; i < curve.keys.Length; ++i)
                    {
                        if (curve.keys[i].value != otherCurve.keys[i].value || curve.keys[i].time != otherCurve.keys[i].time) {
                            _Log($"RenderersHaveSameAnimationCurves() for A: {a?.name ?? "null"}, B: {b?.name ?? "null"}. Curve key values/times differ for {binding.propertyName}. Result: false.");
                            return cacheManager.cache_RendererHaveSameAnimationCurves[(a, b)] = false;
                        }
                    }
                }
            }
            _Log($"RenderersHaveSameAnimationCurves() finished for A: {a?.name ?? "null"}, B: {b?.name ?? "null"}. Result: true.");
            return cacheManager.cache_RendererHaveSameAnimationCurves[(a, b)] = true;
        }

        private bool RenderersHaveSameRootBoneScaleSign(Renderer a, Renderer b)
        {
            _Log($"RenderersHaveSameRootBoneScaleSign() called for A: {a?.name ?? "null"}, B: {b?.name ?? "null"}");
            if (a == null || b == null) {
                _Log($"RenderersHaveSameRootBoneScaleSign() returning true for null renderer. A: {a?.name ?? "null"}, B: {b?.name ?? "null"}.");
                return true;
            }
            var scaleA = a.GetRootBone().lossyScale;
            var scaleB = b.GetRootBone().lossyScale;
            bool result = Mathf.Sign(scaleA.x) == Mathf.Sign(scaleB.x) && Mathf.Sign(scaleA.y) == Mathf.Sign(scaleB.y) && Mathf.Sign(scaleA.z) == Mathf.Sign(scaleB.z);
            _Log($"RenderersHaveSameRootBoneScaleSign() finished for A: {a?.name ?? "null"}, B: {b?.name ?? "null"}. Result: {result}");
            return result;
        }

        public bool CanCombineRendererWith(List<Renderer> list, Renderer candidate)
        {
            _Log($"CanCombineRendererWith() called for candidate: {candidate?.name ?? "null"}");
            if (!settings.MergeSkinnedMeshes) {
                _Log($"CanCombineRendererWith() returning false for {candidate?.name ?? "null"}: MergeSkinnedMeshes is false.");
                return false;
            }
            if (list[0].gameObject.layer != candidate.gameObject.layer) {
                _Log($"CanCombineRendererWith() returning false for {candidate?.name ?? "null"}: Layers differ.");
                return false;
            }
            if (list[0].shadowCastingMode != candidate.shadowCastingMode) {
                _Log($"CanCombineRendererWith() returning false for {candidate?.name ?? "null"}: ShadowCastingModes differ.");
                return false;
            }
            if (list[0].receiveShadows != candidate.receiveShadows) {
                _Log($"CanCombineRendererWith() returning false for {candidate?.name ?? "null"}: ReceiveShadows differ.");
                return false;
            }
            if (!RenderersHaveSameRootBoneScaleSign(list[0], candidate)) {
                _Log($"CanCombineRendererWith() returning false for {candidate?.name ?? "null"}: Root bone scale signs differ.");
                return false;
            }
            bool OneOfParentsHasGameObjectToggleThatTheOthersArentChildrenOf(Transform t, string[] otherPaths)
            {
                while ((t = t.parent) != root.transform)
                {
                    var path = GetPathToRoot(t);
                    if (mainInstance.FindAllGameObjectTogglePaths().Contains(path) && otherPaths.All(p => !p.StartsWithSimple(path)))
                        return true;
                }
                return false;
            }
            if (OneOfParentsHasGameObjectToggleThatTheOthersArentChildrenOf(list[0].transform, new string[] { GetPathToRoot(candidate.transform.parent) })) {
                _Log($"CanCombineRendererWith() returning false for {candidate?.name ?? "null"}: One of parents has GameObject toggle that others aren't children of.");
                return false;
            }
            if (OneOfParentsHasGameObjectToggleThatTheOthersArentChildrenOf(candidate.transform, list.Select(r => GetPathToRoot(r.transform.parent)).ToArray())) {
                _Log($"CanCombineRendererWith() returning false for {candidate?.name ?? "null"}: Candidate's parent has GameObject toggle that others aren't children of.");
                return false;
            }
            if (settings.MergeSkinnedMeshesSeparatedByDefaultEnabledState)
            {
                bool candidateDefaultEnabledState = GetRendererDefaultEnabledState(candidate);
                if (list.Any(r => GetRendererDefaultEnabledState(r) != candidateDefaultEnabledState)) {
                    _Log($"CanCombineRendererWith() returning false for {candidate?.name ?? "null"}: Default enabled states differ.");
                    return false;
                }
            }
            if (CanCombineRendererWithBasicMerge(list, candidate, true)) {
                _Log($"CanCombineRendererWith() returning true for {candidate?.name ?? "null"}: Basic merge possible.");
                return true;
            }
            if (!mainInstance.MergeSkinnedMeshesWithShaderToggle) {
                _Log($"CanCombineRendererWith() returning false for {candidate?.name ?? "null"}: MergeSkinnedMeshesWithShaderToggle is false.");
                return false;
            }
            if (!IsShaderToggleCombinableRenderer(candidate)) {
                _Log($"CanCombineRendererWith() returning false for {candidate?.name ?? "null"}: Not shader toggle combinable.");
                return false;
            }
            if (list.Any(r => !IsShaderToggleCombinableRenderer(r))) {
                _Log($"CanCombineRendererWith() returning false for {candidate?.name ?? "null"}: List contains non-shader toggle combinable renderer.");
                return false;
            }
            _Log($"CanCombineRendererWith() returning true for {candidate?.name ?? "null"}: Shader toggle combinable.");
            return true;
        }

        public List<List<Renderer>> FindPossibleSkinnedMeshMerges()
        {
            _Log("FindPossibleSkinnedMeshMerges() called.");
            context.slotSwapMaterials = mainInstance.FindAllMaterialSwapMaterials();

            var result = new List<List<Renderer>>();
            var skinnedMeshes = root.GetComponentsInChildren<Renderer>(true)
                .Where(r => (r is SkinnedMeshRenderer || r is MeshRenderer) && !r.gameObject.CompareTag("EditorOnly"))
                .Where(r => !mainInstance.GetAllExcludedTransforms().Contains(r.transform))
                .ToList();
            
            _Log($"Found {skinnedMeshes.Count} initial candidate renderers for merging.");

            foreach (var candidate in skinnedMeshes)
            {
                bool merged = false;
                foreach (var list in result)
                {
                    if (CanCombineRendererWith(list, candidate))
                    {
                        list.Add(candidate);
                        merged = true;
                        break;
                    }
                }
                if (!merged)
                {
                    result.Add(new List<Renderer> { candidate });
                }
            }
            _Log($"FindPossibleSkinnedMeshMerges() finished. Found {result.Count} merged lists.");
            return result;
        }

        private Dictionary<string, bool> cache_CanUseNaNimationOnMesh = null;
        public bool CanUseNaNimationOnMesh(string meshPath)
        {
            _Log($"CanUseNaNimationOnMesh() called for meshPath: {meshPath}");
            if (!mainInstance.MergeSkinnedMeshesWithNaNimation) {
                _Log($"CanUseNaNimationOnMesh() returning false for {meshPath}: MergeSkinnedMeshesWithNaNimation is false.");
                return false;
            }
            if (cache_CanUseNaNimationOnMesh == null)
            {
                _Log("cache_CanUseNaNimationOnMesh is null, initializing...");
                cache_CanUseNaNimationOnMesh = new Dictionary<string, bool>();
            }
            if (cache_CanUseNaNimationOnMesh.TryGetValue(meshPath, out var result))
            {
                _Log($"CanUseNaNimationOnMesh() finished for {meshPath}. Returning cached result: {result}");
                return result;
            }
            _Log($"CanUseNaNimationOnMesh() cache miss for {meshPath}. Calculating...");

            var renderer = root.transform.GetTransformFromPath(meshPath)?.GetComponent<Renderer>();
            if (renderer == null || renderer is SkinnedMeshRenderer == false)
            {
                _Log($"CanUseNaNimationOnMesh() returning false for {meshPath}: Renderer not found or not SkinnedMeshRenderer.");
                cache_CanUseNaNimationOnMesh[meshPath] = false;
                return false;
            }

            var isAnimated = mainInstance.fxLayerOptimizer.GetAllUsedAnimationClips()
                .SelectMany(clip => AnimationUtility.GetCurveBindings(clip))
                .Where(binding => 
                    binding.path == meshPath && 
                    (binding.type == typeof(SkinnedMeshRenderer) || binding.type == typeof(GameObject)))
                .Any(binding => binding.propertyName == "m_Enabled" || binding.propertyName == "m_IsActive");
            
            if (isAnimated)
            {
                _Log($"CanUseNaNimationOnMesh() returning false for {meshPath}: Mesh is animated (m_Enabled or m_IsActive).");
                cache_CanUseNaNimationOnMesh[meshPath] = false;
                return false;
            }
            
            _Log($"CanUseNaNimationOnMesh() returning true for {meshPath}.");
            cache_CanUseNaNimationOnMesh[meshPath] = true;
            return true;
        }

        public void AddAnimationPathChange((string path, string name, Type type) source, (string path, string name, Type type) target)
        {
            _Log($"AddAnimationPathChange() called. Source: {source.path}/{source.name} ({source.type.Name}), Target: {target.path}/{target.name} ({target.type.Name})");
            if (source == target) {
                _Log("Source and target paths are identical, skipping animation path change.");
                return;
            }
            context.newAnimationPaths[source] = target;
            if (settings.MergeStaticMeshesAsSkinned && source.type == typeof(SkinnedMeshRenderer))
            {
                _Log($"Adding additional animation path change for static mesh as SkinnedMeshRenderer: Source: {source.path}/{source.name} ({source.type.Name}), Target: {target.path}/{target.name} ({target.type.Name})");
                source.type = typeof(MeshRenderer);
                context.newAnimationPaths[source] = target;
            }
            _Log("AddAnimationPathChange() finished.");
        }

        public string GenerateUniqueName(string name, HashSet<string> usedNames)
        {
            _Log($"GenerateUniqueName() called for original name: {name}");
            if (usedNames.Add(name))
            {
                _Log($"GenerateUniqueName() finished. Name '{name}' is unique.");
                return name;
            }
            int count = 1;
            string newName;
            while (!usedNames.Add(newName = name + " " + count))
            {
                count++;
            }
            _Log($"GenerateUniqueName() finished. Generated unique name: {newName}");
            return newName;
        }

        private static List<string> ColorPropertyComponents = new List<string> { ".r", ".g", ".b", ".a" };
        private static List<string> VectorPropertyComponents = new List<string> { ".x", ".y", ".z", ".w" };
        private Dictionary<string, HashSet<AnimationClip>> cache_AllAnimationClipsAffectingRendererMaterialProperties = null;
        private Dictionary<(string a, string b), HashSet<string>> cache_FindSameAnimatedMaterialProperties = null;

        private HashSet<string> FindSameAnimatedMaterialProperties(string aPath, string bPath)
        {
            _Log($"FindSameAnimatedMaterialProperties() called for aPath: {aPath}, bPath: {bPath}");
            if (cache_FindSameAnimatedMaterialProperties == null) {
                _Log("cache_FindSameAnimatedMaterialProperties is null, initializing...");
                cache_FindSameAnimatedMaterialProperties = new Dictionary<(string a, string b), HashSet<string>>();
            }
            // Normalize paths for caching
            string normalizedAPath = aPath;
            string normalizedBPath = bPath;
            if (normalizedAPath.CompareTo(normalizedBPath) > 0) {
                var temp = normalizedAPath;
                normalizedAPath = normalizedBPath;
                normalizedBPath = temp;
            }
            if (cache_FindSameAnimatedMaterialProperties.TryGetValue((normalizedAPath, normalizedBPath), out var cachedResult)) {
                _Log($"FindSameAnimatedMaterialProperties() finished for {aPath}, {bPath}. Returning cached result with {cachedResult.Count} properties.");
                return cachedResult;
            }
            _Log($"FindSameAnimatedMaterialProperties() cache miss for {aPath}, {bPath}. Calculating...");

            bool IsRelevantBinding(EditorCurveBinding binding) {
                return typeof(Renderer).IsAssignableFrom(binding.type) && binding.propertyName.StartsWithSimple("material.");
            }
            
            if (cache_AllAnimationClipsAffectingRendererMaterialProperties == null) {
                _Log("cache_AllAnimationClipsAffectingRendererMaterialProperties is null, calculating...");
                cache_AllAnimationClipsAffectingRendererMaterialProperties = new Dictionary<string, HashSet<AnimationClip>>();
                foreach (var clip in mainInstance.fxLayerOptimizer.GetAllUsedAnimationClips()) {
                    foreach (var binding in AnimationUtility.GetCurveBindings(clip)) {
                        if (IsRelevantBinding(binding)) {
                            if (!cache_AllAnimationClipsAffectingRendererMaterialProperties.TryGetValue(binding.path, out var clips)) {
                                cache_AllAnimationClipsAffectingRendererMaterialProperties[binding.path] = clips = new HashSet<AnimationClip>();
                            }
                            clips.Add(clip);
                        }
                    }
                }
                _Log($"cache_AllAnimationClipsAffectingRendererMaterialProperties calculated with {cache_AllAnimationClipsAffectingRendererMaterialProperties.Count} entries.");
            }
            
            var allClips = new HashSet<AnimationClip>();
            if (cache_AllAnimationClipsAffectingRendererMaterialProperties.TryGetValue(aPath, out var aClips)) {
                allClips.UnionWith(aClips);
            }
            if (cache_AllAnimationClipsAffectingRendererMaterialProperties.TryGetValue(bPath, out var bClips)) {
                allClips.UnionWith(bClips);
            }
            _Log($"Found {allClips.Count} animation clips affecting materials for paths {aPath} and {bPath}.");
            
            var result = new HashSet<string>();
            foreach (var clip in allClips) {
                var bindings = AnimationUtility.GetCurveBindings(clip);
                foreach (var binding in bindings) {
                    if (IsRelevantBinding(binding) && (binding.path == aPath || binding.path == bPath)) {
                        result.Add(binding.propertyName);
                    }
                }
            }
            _Log($"Initial set of animated properties: {result.Count}");
            
            foreach (var clip in allClips) {
                var bindings = AnimationUtility.GetCurveBindings(clip);
                var propertiesToRemove = new List<string>();
                foreach (var binding in bindings) {
                    if (IsRelevantBinding(binding) && (binding.path == aPath || binding.path == bPath) && result.Contains(binding.propertyName)) {
                        var otherBinding = binding;
                        otherBinding.path = binding.path == aPath ? bPath : aPath;
                        if (!bindings.Contains(otherBinding)) {
                            propertiesToRemove.Add(binding.propertyName);
                            continue;
                        }
                        var aKeys = AnimationUtility.GetEditorCurve(clip, binding).keys;
                        var bKeys = AnimationUtility.GetEditorCurve(clip, otherBinding).keys;
                        if (aKeys.Length != bKeys.Length) {
                            propertiesToRemove.Add(binding.propertyName);
                            continue;
                        }
                        for (int i = 0; i < aKeys.Length; ++i) {
                            if (aKeys[i].value != bKeys[i].value || aKeys[i].time != bKeys[i].time) {
                                propertiesToRemove.Add(binding.propertyName);
                                break;
                            }
                        }
                    }
                }
                foreach(var prop in propertiesToRemove) {
                    result.Remove(prop);
                }
            }
            _Log($"Animated properties after curve comparison: {result.Count}");
            
            var colorProperties = new HashSet<string>();
            var vectorProperties = new HashSet<string>();
            foreach (var property in result) {
                if (ColorPropertyComponents.Any(c => property.EndsWith(c)))
                    colorProperties.Add(property.Substring(0, property.Length - 2));
                else if (VectorPropertyComponents.Any(c => property.EndsWith(c)))
                    vectorProperties.Add(property.Substring(0, property.Length - 2));
            }
            foreach (var colorProperty in colorProperties) {
                if (ColorPropertyComponents.All(c => result.Contains(colorProperty + c))) {
                    result.Add(colorProperty);
                }
                ColorPropertyComponents.ForEach(c => result.Remove(colorProperty + c));
            }
            foreach (var vectorProperty in vectorProperties) {
                if (VectorPropertyComponents.All(c => result.Contains(vectorProperty + c))) {
                    result.Add(vectorProperty);
                }
                VectorPropertyComponents.ForEach(c => result.Remove(vectorProperty + c));
            }
            var finalResult = new HashSet<string>(result.Select(p => p.Substring("material.".Length)));
            _Log($"FindSameAnimatedMaterialProperties() finished for {aPath}, {bPath}. Final result with {finalResult.Count} properties.");
            return cache_FindSameAnimatedMaterialProperties[(normalizedAPath, normalizedBPath)] = finalResult;
        }

        private Dictionary<string, HashSet<string>> cache_SameAnimatedPropertiesOnMergedMesh = null;
        public HashSet<string> GetSameAnimatedPropertiesOnMergedMesh(string path)
        {
            _Log($"GetSameAnimatedPropertiesOnMergedMesh() called for path: {path}");
            if (cache_SameAnimatedPropertiesOnMergedMesh == null) {
                _Log("cache_SameAnimatedPropertiesOnMergedMesh is null, initializing...");
                cache_SameAnimatedPropertiesOnMergedMesh = new Dictionary<string, HashSet<string>>();
            }
            if (cache_SameAnimatedPropertiesOnMergedMesh.TryGetValue(path, out var result)) {
                _Log($"GetSameAnimatedPropertiesOnMergedMesh() finished for {path}. Returning cached result with {result.Count} properties.");
                return result;
            }
            _Log($"GetSameAnimatedPropertiesOnMergedMesh() cache miss for {path}. Returning empty hashset and adding to cache.");
            var newHashSet = new HashSet<string>();
            cache_SameAnimatedPropertiesOnMergedMesh[path] = newHashSet;
            return newHashSet;
        }

        public void CombineSkinnedMeshes()
        {
            _Log("CombineSkinnedMeshes() started.");
                        try
                        {
                            _Log("Combining skinned meshes...");
                            context.transformFromOldPath = new Dictionary<string, Transform>();            foreach (var t in root.transform.GetAllDescendants())
            {
                context.transformFromOldPath[t.GetPathToRoot(root.transform)] = t;
            }
            _Log($"Populated transformFromOldPath with {context.transformFromOldPath.Count} entries.");
            var avDescriptor = root.GetComponent<VRCAvatarDescriptor>();
            var combinableMeshList = FindPossibleSkinnedMeshMerges();
            _Log($"Found {combinableMeshList.Count} potential groups of combinable meshes.");
            context.oldPathToMergedPaths.Clear();
            context.oldPathToMergedPath.Clear();
            var exclusions = mainInstance.componentOptimizer.GetAllExcludedTransforms();
            
            // Using delegated method
            context.movingParentMap = mainInstance.componentOptimizer.FindMovingParent();
            _Log($"Moving parent map initialized with {context.movingParentMap.Count} entries.");

            context.materialSlotRemap = new Dictionary<(string, int), (string, int)>();
            context.animatedMaterialProperties = mainInstance.fxLayerOptimizer.FindAllAnimatedMaterialProperties();
            context.fusedAnimatedMaterialProperties = context.animatedMaterialProperties.ToDictionary(kvp => kvp.Key, kvp => new HashSet<string>(kvp.Value));
            _Log($"Animated material properties found: {context.animatedMaterialProperties.Count}. Fused properties created.");
            var combinableSkinnedMeshList = combinableMeshList
                .Select(l => l.Select(m => m as SkinnedMeshRenderer).Where(m => m != null).ToList())
                .Where(l => l.Count > 0)
                .Where(l => l[0].sharedMesh != null)
                .Where(l => l.All(m => !exclusions.Contains(m.transform)))
                .ToArray();
            _Log($"Filtered down to {combinableSkinnedMeshList.Length} groups of skinned meshes for actual combining.");
            var originalRootPosition = root.transform.position;
            var originalRootRotation = root.transform.rotation;
            _Log("Temporarily resetting root transform position and rotation to zero.");
            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;
            _Log("Root transform position and rotation temporarily reset for combining.");
            int totalMeshCount = combinableSkinnedMeshList.Sum(l => l.Count);
            int currentMeshCount = 0;
            _Log($"Starting to process {totalMeshCount} individual skinned meshes across {combinableSkinnedMeshList.Length} combined groups.");
            for (int combinedMeshID = 0; combinedMeshID < combinableSkinnedMeshList.Length; combinedMeshID++)
            {
                var combinableSkinnedMeshes = combinableSkinnedMeshList[combinedMeshID];
                _Log($"Processing combined mesh group {combinedMeshID + 1}/{combinableSkinnedMeshList.Length} with {combinableSkinnedMeshes.Count} renderers.");

                var basicMergedMeshes = new List<List<Renderer>>();
                foreach (var renderer in combinableSkinnedMeshes)
                {
                    bool foundMatch = false;
                    foreach (var subList in basicMergedMeshes)
                    {
                        if (CanCombineRendererWithBasicMerge(subList, renderer, false))
                        {
                            subList.Add(renderer);
                            foundMatch = true;
                            break;
                        }
                    }
                                    if (!foundMatch)
                                    {
                                        basicMergedMeshes.Add(new List<Renderer> { renderer });
                                    }
                                }
                                _Log($"Basic merged meshes for group {combinedMeshID + 1}: {basicMergedMeshes.Count} sub-groups.");
                Profiler.StartSection("CombineMeshData");
            _Log("Starting CombineMeshData section.");

                int totalVertexCount = combinableSkinnedMeshes.Sum(m => m.sharedMesh.vertexCount);
            _Log($"Total vertex count for this combined group: {totalVertexCount}");

                var targetBones = new List<Transform>();
                var targetBindPoses = new List<Matrix4x4>();
                var targetBoneIndexMap = new Dictionary<(int meshID, int boneID), int>();
                var transformToTargetIndices = new Dictionary<Transform, HashSet<int>>();

                int AddNewBone(Transform boneTransform, Matrix4x4 bindPose)
                {
                    targetBones.Add(boneTransform);
                    targetBindPoses.Add(bindPose);
                    if (!transformToTargetIndices.TryGetValue(boneTransform, out var existingTransformIndices))
                    {
                        transformToTargetIndices[boneTransform] = existingTransformIndices = new HashSet<int>();
                    }
                    existingTransformIndices.Add(targetBones.Count - 1);
                    return targetBones.Count - 1;
                }
                int GetNewBoneIndex(int boneID, int meshID, Transform boneTransform, Matrix4x4 bindPose)
                {
                    if (targetBoneIndexMap.TryGetValue((meshID, boneID), out int index))
                        return index;
                    if (!transformToTargetIndices.TryGetValue(boneTransform, out var existingTransformIndices))
                    {
                        transformToTargetIndices[boneTransform] = existingTransformIndices = new HashSet<int>();
                    }
                    foreach (var i in existingTransformIndices)
                    {
                        if (targetBones[i] == boneTransform && targetBindPoses[i] == bindPose)
                        {
                            return targetBoneIndexMap[(meshID, boneID)] = i;
                        }
                    }
                    return targetBoneIndexMap[(meshID, boneID)] = AddNewBone(boneTransform, bindPose);
                }

                var hasUvSet = new bool[8] {
                    true,
                    combinableSkinnedMeshes.Any(m => m.sharedMesh.HasVertexAttribute(VertexAttribute.TexCoord1)),
                    combinableSkinnedMeshes.Any(m => m.sharedMesh.HasVertexAttribute(VertexAttribute.TexCoord2)),
                    combinableSkinnedMeshes.Any(m => m.sharedMesh.HasVertexAttribute(VertexAttribute.TexCoord3)),
                    combinableSkinnedMeshes.Any(m => m.sharedMesh.HasVertexAttribute(VertexAttribute.TexCoord4)),
                    combinableSkinnedMeshes.Any(m => m.sharedMesh.HasVertexAttribute(VertexAttribute.TexCoord5)),
                    combinableSkinnedMeshes.Any(m => m.sharedMesh.HasVertexAttribute(VertexAttribute.TexCoord6)),
                    combinableSkinnedMeshes.Any(m => m.sharedMesh.HasVertexAttribute(VertexAttribute.TexCoord7)),
                };
                var targetUv = Enumerable.Range(0, 8).Select(i => hasUvSet[i] ? new List<Vector4>(totalVertexCount) : null).ToArray();
                bool useColor32 = !combinableSkinnedMeshes.Any(m => m.sharedMesh.HasVertexAttribute(VertexAttribute.Color)
                    && m.sharedMesh.GetVertexAttributeFormat(VertexAttribute.Color) != VertexAttributeFormat.UNorm8);
                var targetColor = new List<Color>(useColor32 ? 0 : totalVertexCount);
                var targetColor32 = new List<Color32>(useColor32 ? totalVertexCount : 0);
                var targetVertices = new List<Vector3>(totalVertexCount);
                var targetIndices = new List<int[]>();
                var targetTopology = new List<MeshTopology>();
                var targetNormals = new List<Vector3>(totalVertexCount);
                            var targetTangents = new List<Vector4>(totalVertexCount);
                            var targetWeights = new List<BoneWeight>(totalVertexCount);
                            var targetBounds = combinableSkinnedMeshes[0].localBounds;
                            var targetRootBone = combinableSkinnedMeshes[0].rootBone == null ? combinableSkinnedMeshes[0].transform : combinableSkinnedMeshes[0].rootBone;
                            _Log($"Initialized mesh data lists. Total vertex capacity: {totalVertexCount}.");
                            if (mainInstance.MergeSkinnedMeshesWithNaNimation && basicMergedMeshes.Count > 1)
                            {
                                _Log("Checking for NaNimation compatibility and updating target root bone.");
                                var animator = root.GetComponent<Animator>();                    if (animator != null && animator.isHuman)
                    {
                        var headBone = animator.GetBoneTransform(HumanBodyBones.Head);
                        if (headBone != null && (targetRootBone == headBone || targetRootBone.IsChildOf(headBone)))
                        {
                            targetRootBone = animator.GetBoneTransform(HumanBodyBones.Hips);
                                                    if (targetRootBone == null)
                                                    {
                                                        targetRootBone = root.transform;
                                                    }
                                                    _Log($"Target root bone updated to {targetRootBone.name} due to NaNimation.");                        }
                    }
                }

                var toLocal = targetRootBone.worldToLocalMatrix;

                            AddNewBone(combinableSkinnedMeshes[0].transform, combinableSkinnedMeshes[0].transform.worldToLocalMatrix);
                
                            string newMeshName = combinableSkinnedMeshes[0].name;
                            string newPath = combinableSkinnedMeshes[0].transform.GetPathToRoot(root.transform);
                            _Log($"New combined mesh name: {newMeshName}, path: {newPath}");
                            var basicMergedMeshesList = basicMergedMeshes.SelectMany(list => list.Cast<SkinnedMeshRenderer>()).ToList();
                            var mergedMeshPaths = basicMergedMeshes.Select(list => list.Select(r => r.transform.GetPathToRoot(root.transform)).ToList()).ToList();
                            basicMergedMeshesList.ForEach(r => context.oldPathToMergedPaths[r.transform.GetPathToRoot(root.transform)] = mergedMeshPaths);
                            basicMergedMeshesList.ForEach(r => context.oldPathToMergedPath[r.transform.GetPathToRoot(root.transform)] = newPath);
                            _Log($"Mapped {basicMergedMeshesList.Count} renderers to new merged paths.");
                basicMergedMeshesList.ForEach(r => mainInstance.materialOptimizer.GetFirstMaterialOnPath(r.transform.GetPathToRoot(root.transform)));

                int bindPoseMeshID = -1;

                foreach (SkinnedMeshRenderer skinnedMesh in basicMergedMeshesList)
                {
                    currentMeshCount++;
                    _Log($"Processing individual skinned mesh ({currentMeshCount}/{totalMeshCount}): {skinnedMesh.name}");
                    //mainInstance.DisplayProgressBar($"Combining mesh ({{++currentMeshCount}}/{{totalMeshCount}}) {{skinnedMesh.name}}");

                    bindPoseMeshID++;
                    var blobMeshID = basicMergedMeshes.FindIndex(blob => blob.Contains(skinnedMesh));
                    var currentMeshPath = skinnedMesh.transform.GetPathToRoot(root.transform);
                    var mesh = skinnedMesh.sharedMesh;
                    var bindPoseIDMap = new Dictionary<int, int>();
                    var indexOffset = targetVertices.Count;
                    var sourceVertices = mesh.vertices;
                    var sourceUv = mesh.uv;
                    var sourceNormals = mesh.normals;
                    var sourceTangents = mesh.tangents;
                    var sourceWeights = mesh.boneWeights;
                    var rootBone = skinnedMesh.rootBone == null ? skinnedMesh.transform : skinnedMesh.rootBone;
                    var sourceBones = skinnedMesh.bones;
                    for (int i = 0; i < sourceBones.Length; i++)
                    {
                        if (sourceBones[i] == null)
                            sourceBones[i] = rootBone;
                    }
                    var sourceBindPoses = mesh.bindposes;
                    var bindPoseCount = sourceBindPoses.Length;
                    if (sourceBones.Length != bindPoseCount)
                    {
                        _Log($"Bone count ({sourceBones.Length}) does not match bind pose count ({bindPoseCount}) on {skinnedMesh.name}");
                        bindPoseCount = Math.Min(sourceBones.Length, bindPoseCount);
                    }
                    var aabb = skinnedMesh.localBounds;
                    var m = toLocal * rootBone.localToWorldMatrix;
                    targetBounds.Encapsulate(m.MultiplyPoint3x4(aabb.extents.Multiply(1, 1, 1) + aabb.center));
                    targetBounds.Encapsulate(m.MultiplyPoint3x4(aabb.extents.Multiply(1, 1, -1) + aabb.center));
                    targetBounds.Encapsulate(m.MultiplyPoint3x4(aabb.extents.Multiply(1, -1, 1) + aabb.center));
                    targetBounds.Encapsulate(m.MultiplyPoint3x4(aabb.extents.Multiply(1, -1, -1) + aabb.center));
                    targetBounds.Encapsulate(m.MultiplyPoint3x4(aabb.extents.Multiply(-1, 1, 1) + aabb.center));
                    targetBounds.Encapsulate(m.MultiplyPoint3x4(aabb.extents.Multiply(-1, 1, -1) + aabb.center));
                    targetBounds.Encapsulate(m.MultiplyPoint3x4(aabb.extents.Multiply(-1, -1, 1) + aabb.center));
                    targetBounds.Encapsulate(m.MultiplyPoint3x4(aabb.extents.Multiply(-1, -1, -1) + aabb.center));
                    _Log($"Processed mesh data for {skinnedMesh.name}. Current vertex count: {targetVertices.Count}.");
                    Transform NaNimationBone = null;
                    int NaNimationBoneIndex = -1;
                    if (mainInstance.MergeSkinnedMeshesWithNaNimation && basicMergedMeshes.Count > 1
                            && mainInstance.FindAllRendererTogglePaths().Contains(currentMeshPath)
                            && this.CanUseNaNimationOnMesh(currentMeshPath))
                    {
                        _Log($"Applying NaNimation for {skinnedMesh.name}.");
                        NaNimationBone = new GameObject("NaNimationBone").transform;
                        var pathToRoot = currentMeshPath.Replace('/', '_');
                        var siblingNames = new HashSet<string>(root.transform.Cast<Transform>().Select(t => t.name));
                        var nameCandidate = "NaNimation " + pathToRoot;
                        int i = 1;
                        while (siblingNames.Contains(nameCandidate))
                        {
                            nameCandidate = "NaNimation " + pathToRoot + " " + i++;
                        }
                        NaNimationBone.name = nameCandidate;
                        NaNimationBone.parent = root.transform;
                        NaNimationBone.localPosition = Vector3.zero;
                        NaNimationBone.localRotation = Quaternion.identity;
                        NaNimationBone.localScale = Vector3.one;
                        NaNimationBoneIndex = AddNewBone(NaNimationBone, NaNimationBone.worldToLocalMatrix);
                        string key = "NaNimation";
                        if (mainInstance.MergeSkinnedMeshesWithShaderToggle)
                        {
                            key += $";{blobMeshID};{newPath}";
                        }
                        AddAnimationPathChange((currentMeshPath, "m_IsActive", typeof(GameObject)), (NaNimationBone.GetPathToRoot(root.transform), key, typeof(Transform)));
                        AddAnimationPathChange((currentMeshPath, "m_Enabled", typeof(SkinnedMeshRenderer)), (NaNimationBone.GetPathToRoot(root.transform), key, typeof(Transform)));
                        var curveBinding = EditorCurveBinding.FloatCurve(newPath, typeof(SkinnedMeshRenderer), "m_UpdateWhenOffscreen");
                        context.constantAnimatedValuesToAdd.Add(curveBinding, 0f);
                        targetBounds.Encapsulate(toLocal.MultiplyPoint3x4(avDescriptor.ViewPosition + Vector3.forward * 0.3f + Vector3.up * 0.2f));
                        targetBounds.Encapsulate(toLocal.MultiplyPoint3x4(avDescriptor.ViewPosition + Vector3.forward * 0.3f - Vector3.up * 0.2f));
                        targetBounds.Encapsulate(toLocal.MultiplyPoint3x4(avDescriptor.ViewPosition + Vector3.forward * 0.3f + Vector3.right * 0.2f));
                        targetBounds.Encapsulate(toLocal.MultiplyPoint3x4(avDescriptor.ViewPosition + Vector3.forward * 0.3f - Vector3.right * 0.2f));
                    }
                    else if (basicMergedMeshes.Count > 1 && mainInstance.MergeSkinnedMeshesWithShaderToggle)
                    {
                        _Log($"Applying shader toggle for {skinnedMesh.name}.");
                        AddAnimationPathChange((currentMeshPath, "m_IsActive", typeof(GameObject)),
                                (newPath, "material._IsActiveMesh" + blobMeshID, typeof(SkinnedMeshRenderer)));
                        AddAnimationPathChange((currentMeshPath, "m_Enabled", typeof(SkinnedMeshRenderer)),
                                (newPath, "material._IsActiveMesh" + blobMeshID, typeof(SkinnedMeshRenderer)));
                    }

                    if (sourceWeights.Length != sourceVertices.Length || bindPoseCount == 0)
                    {
                        _Log($"Adjusting bone weights and bind poses for {skinnedMesh.name} due to mismatched lengths or zero bind poses.");
                        var defaultWeight = new BoneWeight
                        {
                            boneIndex0 = 0,
                            boneIndex1 = 0,
                            boneIndex2 = 0,
                            boneIndex3 = 0,
                            weight0 = 1,
                            weight1 = 0,
                            weight2 = 0,
                            weight3 = 0
                        };
                        sourceWeights = Enumerable.Repeat(defaultWeight, sourceVertices.Length).ToArray();
                        sourceBones = new Transform[1] { rootBone.transform };
                        sourceBindPoses = new Matrix4x4[1] { Matrix4x4.identity };
                        context.keepTransforms.Add(rootBone.transform);
                        bindPoseCount = 1;
                    }

                    for (int i = 1; i < 8; i++)
                    {
                        if (!hasUvSet[i])
                            continue;
                        var uvs = new List<Vector4>();
                        mesh.GetUVs(i, uvs);
                        targetUv[i].AddRange(uvs.Count == sourceVertices.Length ? uvs : Enumerable.Repeat(Vector4.zero, sourceVertices.Length));
                    }
                    _Log($"Processed UV sets for {skinnedMesh.name}.");

                    if (mesh.HasVertexAttribute(VertexAttribute.Color))
                    {
                        if (useColor32)
                        {
                            targetColor32.AddRange(mesh.colors32);
                        }
                        else
                        {
                            targetColor.AddRange(mesh.colors);
                        }
                    }
                    else
                    {
                        if (useColor32)
                        {
                            targetColor32.AddRange(Enumerable.Repeat(new Color32(255, 255, 255, 255), sourceVertices.Length));
                        }
                        else
                        {
                            targetColor.AddRange(Enumerable.Repeat(Color.white, sourceVertices.Length));
                        }
                    }
                    _Log($"Processed vertex colors for {skinnedMesh.name}.");

                    sourceUv = sourceUv.Length != sourceVertices.Length ? new Vector2[sourceVertices.Length] : sourceUv;
                    sourceNormals = sourceNormals.Length != sourceVertices.Length ? new Vector3[sourceVertices.Length] : sourceNormals;
                    sourceTangents = sourceTangents.Length != sourceVertices.Length ? new Vector4[sourceVertices.Length] : sourceTangents;

                    if (!context.blendShapesToBake.TryGetValue(skinnedMesh, out var blendShapeIDs))
                    {
                        blendShapeIDs = new List<int>();
                    }
                    _Log($"Found {blendShapeIDs.Count} blend shapes to bake for {skinnedMesh.name}.");

                    foreach (int blendShapeID in blendShapeIDs)
                    {
                        var weight = Mathf.Clamp(skinnedMesh.GetBlendShapeWeight(blendShapeID) / 100f, 0, 1);
                        var deltaVertices = new Vector3[sourceVertices.Length];
                        var deltaNormals = new Vector3[sourceVertices.Length];
                        var deltaTangents = new Vector3[sourceVertices.Length];
                        mesh.GetBlendShapeFrameVertices(blendShapeID, 0, deltaVertices, deltaNormals, deltaTangents);
                        for (int i = 0; i < sourceVertices.Length; i++)
                        {
                            sourceVertices[i] += deltaVertices[i] * weight;
                            sourceNormals[i] += deltaNormals[i] * weight;
                            sourceTangents[i] += (Vector4)(deltaTangents[i] * weight);
                        }
                        _Log($"Applied blend shape baking for {skinnedMesh.name}.");
                    }

                    for (int vertIndex = 0; vertIndex < sourceVertices.Length; vertIndex++)
                    {
                        int GetNewBoneIndexForCurrentMesh(int oldIndex)
                        {
                            oldIndex = oldIndex >= bindPoseCount ? 0 : Math.Max(0, oldIndex);
                            if (!bindPoseIDMap.TryGetValue(oldIndex, out int newIndex))
                            {
                                newIndex = GetNewBoneIndex(oldIndex, bindPoseMeshID, sourceBones[oldIndex], sourceBindPoses[oldIndex]);
                                bindPoseIDMap[oldIndex] = newIndex;
                            }
                            return newIndex;
                        }
                        var boneWeight = sourceWeights[vertIndex];
                        boneWeight.boneIndex0 = GetNewBoneIndexForCurrentMesh(boneWeight.boneIndex0);
                        boneWeight.boneIndex1 = GetNewBoneIndexForCurrentMesh(boneWeight.boneIndex1);
                        boneWeight.boneIndex2 = GetNewBoneIndexForCurrentMesh(boneWeight.boneIndex2);
                        boneWeight.boneIndex3 = GetNewBoneIndexForCurrentMesh(boneWeight.boneIndex3);
                        if (NaNimationBoneIndex != -1)
                        {
                            var sum = boneWeight.weight0 + boneWeight.weight1 + boneWeight.weight2;
                            sum = sum == 0 ? 1 : sum;
                            boneWeight.weight0 /= sum;
                            boneWeight.weight1 /= sum;
                            boneWeight.weight2 /= sum;
                            boneWeight.weight3 = 0;
                            if (boneWeight.weight1 == 0)
                            {
                                boneWeight.boneIndex1 = NaNimationBoneIndex;
                                boneWeight.weight1 = 1e-35f;
                            }
                            else if (boneWeight.weight2 == 0)
                            {
                                boneWeight.boneIndex2 = NaNimationBoneIndex;
                                boneWeight.weight2 = 1e-35f;
                            }
                            else
                            {
                                boneWeight.boneIndex3 = NaNimationBoneIndex;
                                boneWeight.weight3 = 1e-35f;
                            }
                        }
                        targetWeights.Add(boneWeight);
                        targetVertices.Add(sourceVertices[vertIndex]);
                        targetNormals.Add(sourceNormals[vertIndex]);
                        targetTangents.Add(sourceTangents[vertIndex]);
                        targetUv[0].Add(new Vector4(sourceUv[vertIndex].x, sourceUv[vertIndex].y, blobMeshID << 12, 0));
                    }
                    _Log($"Processed vertex data and bone weights for {skinnedMesh.name}.");

                    for (var matID = 0; matID < skinnedMesh.sharedMaterials.Length; matID++)
                    {
                        int clampedSubMeshID = Math.Min(matID, mesh.subMeshCount - 1);
                        int[] indices = mesh.GetIndices(clampedSubMeshID);
                        for (uint i = 0; i < indices.Length; i++)
                        {
                            indices[i] += indexOffset;
                        }
                        context.materialSlotRemap[(newPath, targetIndices.Count)] = (skinnedMesh.transform.GetPathToRoot(root.transform), matID);
                        targetIndices.Add(indices);
                        targetTopology.Add(mesh.GetTopology(clampedSubMeshID));
                    }
                    _Log($"Processed submeshes and material slots for {skinnedMesh.name}.");
                }
                Profiler.EndSection();
                _Log("CombineMeshData section finished.");

                var blendShapeWeights = new Dictionary<string, float>();

                var combinedMesh = new Mesh();
                combinedMesh.indexFormat = targetVertices.Count >= 65536
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16;
                combinedMesh.SetVertices(targetVertices);
                combinedMesh.bindposes = targetBindPoses.ToArray();
                combinedMesh.SetBoneWeights(targetWeights.ToArray());
                bool hasParticleSystemUsingMeshColor = basicMergedMeshesList.Any(r => GetParticleSystemsUsingRenderer(r).Any(ps => ps.shape.useMeshColors));
                if (!useColor32 && (hasParticleSystemUsingMeshColor || targetColor.Any(c => !c.Equals(Color.white))))
                {
                    combinedMesh.colors = targetColor.ToArray();
                }
                else if (useColor32 && (hasParticleSystemUsingMeshColor || targetColor32.Any(c => !c.Equals(new Color32(255, 255, 255, 255)))))
                {
                    combinedMesh.colors32 = targetColor32.ToArray();
                }
                for (int i = 0; i < 8; i++)
                {
                    if (hasUvSet[i] && targetUv[i].Any(uv => !uv.Equals(Vector4.zero)))
                    {
                        combinedMesh.SetUVs(i, targetUv[i]);
                    }
                }
                combinedMesh.bounds = combinableSkinnedMeshes[0].sharedMesh.bounds;
                combinedMesh.SetNormals(targetNormals);
                combinedMesh.SetTangents(targetTangents);
                combinedMesh.subMeshCount = targetIndices.Count;
                combinedMesh.name = newMeshName;
                for (int i = 0; i < targetIndices.Count; i++)
                {
                    combinedMesh.SetIndices(targetIndices[i], targetTopology[i], i);
                }
                _Log($"Combined mesh '{newMeshName}' created with {combinedMesh.vertexCount} vertices and {combinedMesh.subMeshCount} submeshes.");

                Profiler.StartSection("CopyCombinedMeshBlendShapes");
            _Log("Starting CopyCombinedMeshBlendShapes section.");
                var usedBlendShapeNames = new HashSet<string>();
                var blendShapeMeshIDtoNewName = new Dictionary<(int meshID, int blendShapeID), string>();
                var combinableMeshPaths = new HashSet<string>(basicMergedMeshesList.Select(s => s.transform.GetPathToRoot(root.transform)));
                var meshPathToID = basicMergedMeshesList.Select((s, i) => (s.transform.GetPathToRoot(root.transform), i)).ToDictionary(s => s.Item1, s => s.Item2);
                var usedBlendShapesInCombinedMesh = new HashSet<string>(
                    context.usedBlendShapes.Where(s => combinableMeshPaths.Contains(s.Substring(0, s.IndexOf("/blendShape.")))));
                var allMergedBlendShapes = new List<List<(string blendshape, float weight)>>();
                if (settings.MergeSameRatioBlendShapes)
                {
                    allMergedBlendShapes.AddRange(mainInstance.FindMergeableBlendShapes(basicMergedMeshesList));
                    var usedBlendShapesInMergedBlobs = new HashSet<string>(allMergedBlendShapes.SelectMany(s => s).Select(s => s.blendshape));
                    allMergedBlendShapes.AddRange(usedBlendShapesInCombinedMesh.Where(s => !usedBlendShapesInMergedBlobs.Contains(s)).Select(s => new List<(string blendshape, float weight)> { (s, 1) }));
                }
                            else
                            {
                                allMergedBlendShapes.AddRange(usedBlendShapesInCombinedMesh.Select(s => new List<(string blendshape, float weight)> { (s, 1) }));
                            }
                            _Log($"Identified {allMergedBlendShapes.Count} groups of blend shapes to merge.");                var vertexOffset = new List<int>() {0};
                for (int i = 0; i < basicMergedMeshesList.Count - 1; i++)
                {
                    vertexOffset.Add(vertexOffset[i] + basicMergedMeshesList[i].sharedMesh.vertexCount);
                }
                int combinedMeshVertexCount = combinedMesh.vertexCount;
                foreach (var mergedBlendShapes in allMergedBlendShapes)
                {
                    if (mergedBlendShapes.Count == 1)
                    {
                        var path = mergedBlendShapes[0].blendshape.Substring(0, mergedBlendShapes[0].blendshape.IndexOf("/blendShape."));
                        var skinnedMesh = root.transform.GetTransformFromPath(path).GetComponent<SkinnedMeshRenderer>();
                        var mesh = skinnedMesh.sharedMesh;
                        var oldName = mergedBlendShapes[0].blendshape.Substring(path.Length + 12);
                        var name = GenerateUniqueName(oldName, usedBlendShapeNames);
                        var meshID = meshPathToID[path];
                        var blendShapeID = mesh.GetBlendShapeIndex(oldName);
                        if (blendShapeID == -1) {
                            _Log($"Blend shape '{oldName}' not found on mesh at path '{path}', skipping.");
                            continue;
                        }
                        blendShapeMeshIDtoNewName[(meshID, blendShapeID)] = name;
                        blendShapeWeights[name] = skinnedMesh.GetBlendShapeWeight(blendShapeID);
                        AddAnimationPathChange(
                            (path, "blendShape." + oldName, typeof(SkinnedMeshRenderer)),
                            (newPath, "blendShape." + name, typeof(SkinnedMeshRenderer)));
                        for (int j = 0; j < mesh.GetBlendShapeFrameCount(blendShapeID); j++)
                        {
                            int meshVertexCount = mesh.vertexCount;
                            var sourceDeltaVertices = new Vector3[meshVertexCount];
                            var sourceDeltaNormals = new Vector3[meshVertexCount];
                            var sourceDeltaTangents = new Vector3[meshVertexCount];
                            mesh.GetBlendShapeFrameVertices(blendShapeID, j, sourceDeltaVertices, sourceDeltaNormals, sourceDeltaTangents);
                            var targetDeltaVertices = new Vector3[combinedMeshVertexCount];
                            var targetDeltaNormals = new Vector3[combinedMeshVertexCount];
                            var targetDeltaTangents = new Vector3[combinedMeshVertexCount];
                            for (int k = 0; k < meshVertexCount; k++)
                            {
                                int vertIndex = k + vertexOffset[meshID];
                                targetDeltaVertices[vertIndex] = sourceDeltaVertices[k];
                                targetDeltaNormals[vertIndex] = sourceDeltaNormals[k];
                                targetDeltaTangents[vertIndex] = sourceDeltaTangents[k];
                            }
                            var weight = mesh.GetBlendShapeFrameWeight(blendShapeID, j);
                            combinedMesh.AddBlendShapeFrame(name, weight, targetDeltaVertices, targetDeltaNormals, targetDeltaTangents);
                        }
                        _Log($"Merged single blend shape '{oldName}' to '{name}'.");
                    }
                    else
                    {
                        var oldPath = mergedBlendShapes[0].blendshape.Substring(0, mergedBlendShapes[0].blendshape.IndexOf("/blendShape."));
                        var oldName = mergedBlendShapes[0].blendshape.Substring(mergedBlendShapes[0].blendshape.IndexOf("/blendShape.") + 12);
                        var name = GenerateUniqueName(oldName, usedBlendShapeNames);
                        AddAnimationPathChange(
                            (oldPath, "blendShape." + oldName, typeof(SkinnedMeshRenderer)),
                            (newPath, "blendShape." + name, typeof(SkinnedMeshRenderer)));
                        var targetDeltaVertices = new Vector3[combinedMeshVertexCount];
                        var targetDeltaNormals = new Vector3[combinedMeshVertexCount];
                        var targetDeltaTangents = new Vector3[combinedMeshVertexCount];
                        bool first = true;
                        foreach (var toMerge in mergedBlendShapes)
                        {
                            var path = toMerge.blendshape.Substring(0, toMerge.blendshape.IndexOf("/blendShape."));
                            var skinnedMesh = root.transform.GetTransformFromPath(path).GetComponent<SkinnedMeshRenderer>();
                            var mesh = skinnedMesh.sharedMesh;
                                                    var blendShapeID = mesh.GetBlendShapeIndex(toMerge.blendshape.Substring(path.Length + 12));
                                                    if (blendShapeID == -1) {
                                                        _Log($"Blend shape '{toMerge.blendshape}' not found on mesh at path '{path}', skipping part of merge.");
                                                        continue;
                                                    }
                                                    var meshID = meshPathToID[path];
                                                    blendShapeMeshIDtoNewName[(meshID, blendShapeID)] = name;
                                                    if (first)
                                                    {
                                                        blendShapeWeights[name] = skinnedMesh.GetBlendShapeWeight(blendShapeID);
                                                        first = false;
                                                    }
                                                    int meshVertexCount = mesh.vertexCount;
                                                    var sourceDeltaVertices = new Vector3[meshVertexCount];
                                                    var sourceDeltaNormals = new Vector3[meshVertexCount];
                                                    var sourceDeltaTangents = new Vector3[meshVertexCount];
                                                    mesh.GetBlendShapeFrameVertices(blendShapeID, 0, sourceDeltaVertices, sourceDeltaNormals, sourceDeltaTangents);
                                                    for (int k = 0; k < meshVertexCount; k++)
                                                    {
                                                        int vertIndex = k + vertexOffset[meshID];
                                                        targetDeltaVertices[vertIndex] += sourceDeltaVertices[k] * toMerge.weight;
                                                        targetDeltaNormals[vertIndex] += sourceDeltaNormals[k] * toMerge.weight;
                                                        targetDeltaTangents[vertIndex] += sourceDeltaTangents[k] * toMerge.weight;
                                                    }
                                                }
                                                combinedMesh.AddBlendShapeFrame(name, 100, targetDeltaVertices, targetDeltaNormals, targetDeltaTangents);
                                                _Log($"Merged multiple blend shapes into '{name}'.");                    }
                }
                Profiler.EndSection();
                _Log("CopyCombinedMeshBlendShapes section finished.");
                
                var targetRenderer = combinableSkinnedMeshes[0];

                if (avDescriptor.customEyeLookSettings.eyelidType == VRCAvatarDescriptor.EyelidType.Blendshapes
                    && avDescriptor.customEyeLookSettings.eyelidsSkinnedMesh != null)
                {
                    _Log("Adjusting eye look settings.");
                    var eyeLookMeshRenderer = avDescriptor.customEyeLookSettings.eyelidsSkinnedMesh;
                    var ids = avDescriptor.customEyeLookSettings.eyelidsBlendshapes;
                    for (int i = 0; i < ids.Length; i++)
                    {
                        if (ids[i] < 0)
                            continue;
                        for (int meshID = 0; meshID < basicMergedMeshesList.Count; meshID++)
                        {
                            if (basicMergedMeshesList[meshID] == eyeLookMeshRenderer)
                            {
                                avDescriptor.customEyeLookSettings.eyelidsSkinnedMesh = targetRenderer;
                                ids[i] = combinedMesh.GetBlendShapeIndex(blendShapeMeshIDtoNewName[(meshID, ids[i])]);
                            }
                        }
                    }
                    avDescriptor.customEyeLookSettings.eyelidsBlendshapes = ids;
                }

                for (int meshID = 0; meshID < basicMergedMeshesList.Count; meshID++)
                {
                    var oldVisemeMesh = basicMergedMeshesList[meshID];
                    if (avDescriptor.VisemeSkinnedMesh == oldVisemeMesh)
                    {
                        _Log($"Adjusting viseme settings for {oldVisemeMesh.name}.");
                        avDescriptor.VisemeSkinnedMesh = targetRenderer;
                        string CalculateNewBlendShapeName(string blendShapeName) {
                            var blendShapeID = oldVisemeMesh.sharedMesh.GetBlendShapeIndex(blendShapeName ?? "");
                            return blendShapeMeshIDtoNewName.TryGetValue((meshID, blendShapeID), out string newName)
                                ? newName : $"MISSING \"{blendShapeName}\" " ;
                        }
                        avDescriptor.VisemeBlendShapes = avDescriptor.VisemeBlendShapes.Select(CalculateNewBlendShapeName).ToArray();
                        avDescriptor.MouthOpenBlendShapeName = CalculateNewBlendShapeName(avDescriptor.MouthOpenBlendShapeName);
                    }
                }

                var sameAnimatedProperties = GetSameAnimatedPropertiesOnMergedMesh(newPath);
                if (basicMergedMeshes.Count > 1 && mainInstance.MergeSkinnedMeshesWithShaderToggle) {
                    _Log("Processing animated material properties for shader toggle.");
                    var pathA = basicMergedMeshes[0][0].transform.GetPathToRoot(root.transform);
                    sameAnimatedProperties.UnionWith(FindSameAnimatedMaterialProperties(pathA, basicMergedMeshes[1][0].transform.GetPathToRoot(root.transform)));
                    for (int blobMeshID = 2; blobMeshID < basicMergedMeshes.Count; blobMeshID++) {
                        sameAnimatedProperties.IntersectWith(FindSameAnimatedMaterialProperties(pathA, basicMergedMeshes[blobMeshID][0].transform.GetPathToRoot(root.transform)));
                    }
                    _Log($"Found {sameAnimatedProperties.Count} same animated properties.");
                }

                for (int blobMeshID = 0; blobMeshID < basicMergedMeshes.Count && basicMergedMeshes.Count > 1 && mainInstance.MergeSkinnedMeshesWithShaderToggle; blobMeshID++) {
                    var skinnedMesh = basicMergedMeshes[blobMeshID][0];
                    var oldPath = skinnedMesh.transform.GetPathToRoot(root.transform);
                    var properties = new MaterialPropertyBlock();
                    if (targetRenderer.HasPropertyBlock())
                        targetRenderer.GetPropertyBlock(properties);
                    bool isActive = GetRendererDefaultEnabledState(skinnedMesh);
                    properties.SetFloat($"_IsActiveMesh{{blobMeshID}}", isActive ? 1f : 0f);
                    properties.SetInt("WKVRCOptimizer_CombinedMeshCount", basicMergedMeshes.Count);
                    var animatedMaterialPropertiesToAdd = new List<string>();
                    if (context.animatedMaterialProperties.TryGetValue(oldPath, out var animatedProperties)) {
                        foreach (var animPropName in animatedProperties) {
                            var propName = animPropName;
                            bool isVector = propName.EndsWith(".x");
                            bool isColor = propName.EndsWith(".r");
                            if (isVector || isColor) {
                                propName = propName.Substring(0, propName.Length - 2);
                            } else if (propName[propName.Length - 2] == '.') {
                                continue;
                            }
                            if (sameAnimatedProperties.Contains(animPropName)) {
                                continue;
                            }
                            for (int mID = 0; mID < basicMergedMeshes.Count; mID++) {
                                string newPropertyName = $"material.WKVRCOptimizer{{propName}}_ArrayIndex{{mID}}";
                                string path = basicMergedMeshes[mID][0].transform.GetPathToRoot(root.transform);
                                var vectorEnd = isVector ? new [] { ".x", ".y", ".z", ".w" } : isColor ? new [] { ".r", ".g", ".b", ".a" } : new [] { "" };
                                foreach (var component in vectorEnd) {
                                    AddAnimationPathChange(
                                        (path, "material." + propName + component, typeof(SkinnedMeshRenderer)),
                                        (newPath, newPropertyName + component, typeof(SkinnedMeshRenderer)));
                                }
                            }
                            animatedMaterialPropertiesToAdd.Add(animPropName);
                        }
                    }
                    if (animatedMaterialPropertiesToAdd.Count > 0) {
                        if (!context.fusedAnimatedMaterialProperties.TryGetValue(newPath, out animatedProperties)) {
                            context.fusedAnimatedMaterialProperties[newPath] = animatedProperties = new HashSet<string>();
                        }
                        animatedProperties.UnionWith(animatedMaterialPropertiesToAdd);
                    }
                    targetRenderer.SetPropertyBlock(properties);
                }
                _Log("Applied material property blocks for shader toggle if applicable.");

                var materials = basicMergedMeshesList.SelectMany(r => r.sharedMaterials).ToArray();
                var originalMeshSlots = basicMergedMeshesList.SelectMany(r => MaterialSlot.GetAllSlotsFrom(r)).ToList();
                foreach (var renderer in basicMergedMeshesList)
                {
                    foreach (var ps in GetParticleSystemsUsingRenderer(renderer))
                    {
                        var shape = ps.shape;
                        shape.skinnedMeshRenderer = targetRenderer;
                        if (shape.useMeshMaterialIndex)
                        {
                            shape.meshMaterialIndex = originalMeshSlots.FindIndex(s => s.renderer == renderer && s.index == shape.meshMaterialIndex);
                        }
                    }
                }
                targetRenderer.rootBone = targetRootBone;
                targetRenderer.sharedMesh = combinedMesh;
                targetRenderer.sharedMaterials = materials;
                targetRenderer.bones = targetBones.ToArray();
                targetRenderer.localBounds = targetBounds;
                _Log("Updated target renderer properties with combined mesh and materials.");

                foreach (var blendShape in blendShapeWeights)
                {
                    for (int j = 0; j < combinedMesh.blendShapeCount; j++)
                    {
                        if (blendShape.Key == combinedMesh.GetBlendShapeName(j))
                        {
                            targetRenderer.SetBlendShapeWeight(j, blendShape.Value);
                            break;
                        }
                    }
                }
                _Log("Set blend shape weights on target renderer.");

                if (basicMergedMeshes.Count > 1)
                {
                    var go = targetRenderer.gameObject;
                    var children = go.transform.Cast<Transform>().ToList();
                    var componentsToMove = go.GetComponents<Component>().Where(c => !(c is Transform) && !(c is SkinnedMeshRenderer)).ToList();
                    if (children.Count > 0 || componentsToMove.Count > 0)
                    {
                        _Log($"Moving children and components of {go.name} to sub-container.");
                        var subContainer = new GameObject("WKVRCOptimizer_mergeTargetRoot");
                        subContainer.transform.parent = go.transform;
                        subContainer.transform.localPosition = Vector3.zero;
                        subContainer.transform.localRotation = Quaternion.identity;
                        subContainer.transform.localScale = Vector3.one;
                        subContainer.SetActive(targetRenderer.gameObject.activeSelf);
                        context.transformFromOldPath[go.transform.GetPathToRoot(root.transform)] = subContainer.transform;

                        foreach (Transform child in children)
                        {
                            child.parent = subContainer.transform;
                        }

                        foreach (Component comp in componentsToMove)
                        {
                            UnityEditorInternal.ComponentUtility.CopyComponent(comp);
                            UnityEditorInternal.ComponentUtility.PasteComponentAsNew(subContainer);
                            Object.DestroyImmediate(comp);
                        }
                    }
                    else
                    {
                        _Log($"No children or components to move for {go.name}. Adding to pathsToDeleteGameObjectTogglesOn.");
                        context.pathsToDeleteGameObjectTogglesOn.Add(go.transform.GetPathToRoot(root.transform));
                    }

                    if (settings.MergeSkinnedMeshesSeparatedByDefaultEnabledState && !GetRendererDefaultEnabledState(targetRenderer))
                    {
                        _Log($"Adjusting default enabled state for {targetRenderer.name} due to MergeSkinnedMeshesSeparatedByDefaultEnabledState.");
                        targetRenderer.gameObject.SetActive(true);
                        targetRenderer.enabled = false;
                        var curveBinding = EditorCurveBinding.FloatCurve(targetRenderer.transform.GetPathToRoot(root.transform), typeof(SkinnedMeshRenderer), "m_Enabled");
                        context.constantAnimatedValuesToAdd.Add(curveBinding, 1f);
                    }
                    else
                    {
                        targetRenderer.gameObject.SetActive(true);
                        targetRenderer.enabled = true;
                    }
                }

                for (int meshID = 1; meshID < combinableSkinnedMeshes.Count; meshID++)
                {
                    var obj = combinableSkinnedMeshes[meshID].gameObject;
                    _Log($"Destroying skinned mesh renderer and potentially GameObject for {obj.name}.");
                    Object.DestroyImmediate(combinableSkinnedMeshes[meshID]);
                    if (!context.keepTransforms.Contains(obj.transform) && obj.transform.childCount == 0 && obj.GetNonNullComponents().Length == 1)
                        Object.DestroyImmediate(obj);
                }

                Profiler.StartSection("AssetDatabase.SaveAssets ()");
                _Log("Saving assets after combining meshes.");
                AssetDatabase.SaveAssets();
                Profiler.EndSection();
            }
            root.transform.position = originalRootPosition;
            root.transform.rotation = originalRootRotation;
            _Log("Root transform position and rotation restored.");

            // flush particle system cache since we merged meshes
            cache_ParticleSystemsUsingRenderer = null;
            _Log("Particle system cache flushed.");
            _Log("CombineSkinnedMeshes() finished successfully.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[MeshOptimizer] An error occurred during CombineSkinnedMeshes: {e.Message}\n{e.StackTrace}");
                throw; // Re-throw the exception to ensure it's still propagated.
            }
        }
        public HashSet<SkinnedMeshRenderer> FindAllUnusedSkinnedMeshRenderers()
        {
            _Log("FindAllUnusedSkinnedMeshRenderers() called.");
            var togglePaths = mainInstance.fxLayerOptimizer.FindAllGameObjectTogglePaths();
            var unused = new HashSet<SkinnedMeshRenderer>();
            var skinnedMeshRenderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var exclusions = mainInstance.componentOptimizer.GetAllExcludedTransforms();
            foreach (var skinnedMeshRenderer in skinnedMeshRenderers)
            {
                if (skinnedMeshRenderer.gameObject.activeSelf)
                    continue;
                if (togglePaths.Contains(skinnedMeshRenderer.transform.GetPathToRoot(root.transform)))
                    continue;
                if (exclusions.Contains(skinnedMeshRenderer.transform))
                    continue;
                unused.Add(skinnedMeshRenderer);
            }
            _Log($"FindAllUnusedSkinnedMeshRenderers() finished. Found {unused.Count} unused skinned mesh renderers.");
            return unused;
        }

        public void DeleteAllUnusedSkinnedMeshRenderers()
        {
            _Log("DeleteAllUnusedSkinnedMeshRenderers() called.");
            foreach (var skinnedMeshRenderer in FindAllUnusedSkinnedMeshRenderers())
            {
                var obj = skinnedMeshRenderer.gameObject;
                _Log($"Destroying unused SkinnedMeshRenderer: {skinnedMeshRenderer.name} on GameObject: {obj.name}");
                Object.DestroyImmediate(skinnedMeshRenderer);
                if (!context.keepTransforms.Contains(obj.transform) && (obj.transform.childCount == 0 && obj.GetNonNullComponents().Length == 1))
                    Object.DestroyImmediate(obj);
            }
            _Log("DeleteAllUnusedSkinnedMeshRenderers() finished.");
        }

        public void ConvertStaticMeshesToSkinnedMeshes()
        {
            _Log("ConvertStaticMeshesToSkinnedMeshes() called.");
            if (!settings.MergeStaticMeshesAsSkinned) {
                _Log("ConvertStaticMeshesToSkinnedMeshes() skipped: MergeStaticMeshesAsSkinned is false.");
                return;
            }
            _Log("Converting static meshes to skinned meshes...");
            var staticMeshes = root.GetComponentsInChildren<MeshFilter>(true)
                .Where(f => f.sharedMesh != null && f.gameObject.GetComponent<MeshRenderer>() != null)
                .Where(f => f.gameObject.layer != 12)
                .Select(f => f.gameObject).Distinct().ToList();
            var meshesThatGetCombinedWithOtherMeshes = new HashSet<Renderer>(FindPossibleSkinnedMeshMerges().Where(l => l.Count > 1).SelectMany(l => l));

            foreach (var obj in staticMeshes)
            {
                if (!meshesThatGetCombinedWithOtherMeshes.Contains(obj.GetComponent<Renderer>()))
                    continue;
                bool isActive = obj.GetComponent<MeshRenderer>().enabled;
                var mats = obj.GetComponent<MeshRenderer>().sharedMaterials;
                var lightAnchor = obj.GetComponent<MeshRenderer>().probeAnchor;
                var mesh = obj.GetComponent<MeshFilter>().sharedMesh;
                Object.DestroyImmediate(obj.GetComponent<MeshFilter>());
                var skinnedMeshRenderer = obj.AddComponent<SkinnedMeshRenderer>();
                skinnedMeshRenderer.enabled = isActive;
                skinnedMeshRenderer.sharedMesh = mesh;
                skinnedMeshRenderer.sharedMaterials = mats;
                skinnedMeshRenderer.probeAnchor = lightAnchor;
                context.convertedMeshRendererPaths.Add(obj.transform.GetPathToRoot(root.transform));
                _Log($"Converted static mesh renderer {obj.name} to SkinnedMeshRenderer.");
            }
            _Log("ConvertStaticMeshesToSkinnedMeshes() finished.");
        }

        public List<List<(string blendshape, float value)>> FindMergeableBlendShapes(IEnumerable<Renderer> mergedMeshBlob)
        {
            var avDescriptor = root.GetComponent<VRCAvatarDescriptor>();
            var fxLayer = mainInstance.fxLayerOptimizer.GetFXLayer();
            if (avDescriptor == null || fxLayer == null)
                return new List<List<(string blendshape, float value)>>();
            var exclusions = mainInstance.componentOptimizer.GetAllExcludedTransforms();
            var validPaths = new HashSet<string>();
            var blendShapeNameToID = new Dictionary<string, int>();
            var blendShapeIDToName = new List<string>();
            int GetBlendShapeID(string name)
            {
                if (blendShapeNameToID.TryGetValue(name, out var id))
                    return id;
                id = blendShapeIDToName.Count;
                blendShapeNameToID[name] = id;
                blendShapeIDToName.Add(name);
                return id;
            }
            var ratiosDict = new List<Dictionary<int, float>>() { new Dictionary<int, float>() };
            foreach (var renderer in mergedMeshBlob)
            {
                var skinnedMeshRenderer = renderer as SkinnedMeshRenderer;
                var mesh = skinnedMeshRenderer?.sharedMesh;
                if (mesh == null || exclusions.Contains(skinnedMeshRenderer.transform))
                    continue;
                string path = skinnedMeshRenderer.transform.GetPathToRoot(root.transform) + "/blendShape.";
                for (int i = 0; i < mesh.blendShapeCount; i++)
                {
                    var name = mesh.GetBlendShapeName(i);
                    if (settings.MMDCompatibility && OptimizationContext.MMDBlendShapes.Contains(name))
                        continue;
                    if (mesh.GetBlendShapeFrameCount(i) == 1)
                    {
                        validPaths.Add(path + name);
                        ratiosDict[0][GetBlendShapeID(path + name)] = skinnedMeshRenderer.GetBlendShapeWeight(i);
                    }
                }
            }
            if (validPaths.Count == 0)
                return new List<List<(string blendshape, float value)>>();
            if (avDescriptor.lipSync == VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape
                && avDescriptor.VisemeSkinnedMesh != null)
            {
                var meshRenderer = avDescriptor.VisemeSkinnedMesh;
                string path = meshRenderer.transform.GetPathToRoot(root.transform) + "/blendShape.";
                foreach (var blendShapeName in avDescriptor.VisemeBlendShapes)
                {
                    validPaths.Remove(path + blendShapeName);
                }
            }
            if (avDescriptor.lipSync == VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.JawFlapBlendShape
                && avDescriptor.VisemeSkinnedMesh != null)
            {
                var meshRenderer = avDescriptor.VisemeSkinnedMesh;
                string path = meshRenderer.transform.GetPathToRoot(root.transform) + "/blendShape.";
                validPaths.Remove(path + avDescriptor.MouthOpenBlendShapeName);
            }
            if (avDescriptor.customEyeLookSettings.eyelidType == VRCAvatarDescriptor.EyelidType.Blendshapes
                && avDescriptor.customEyeLookSettings.eyelidsSkinnedMesh != null
                && avDescriptor.customEyeLookSettings.eyelidsSkinnedMesh.sharedMesh != null)
            {
                var meshRenderer = avDescriptor.customEyeLookSettings.eyelidsSkinnedMesh;
                string path = meshRenderer.transform.GetPathToRoot(root.transform) + "/blendShape.";
                foreach (var blendShapeID in avDescriptor.customEyeLookSettings.eyelidsBlendshapes)
                {
                    if (blendShapeID >= 0 && blendShapeID < meshRenderer.sharedMesh.blendShapeCount)
                    {
                        validPaths.Remove(path + meshRenderer.sharedMesh.GetBlendShapeName(blendShapeID));
                    }
                }
            }
            var mergeableBlendShapes = new List<List<(int blendshapeID, float value)>>();
            var hasEntryInMergeableBlendShapes = new HashSet<int>();
            foreach (var clip in mainInstance.fxLayerOptimizer.GetAllUsedAnimationClips())
            {
                var blendShapes = new List<(int blendShapeID, EditorCurveBinding binding)>();
                var keyframes = new HashSet<float>();
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                {
                    if (binding.type != typeof(SkinnedMeshRenderer)
                        || !binding.propertyName.StartsWithSimple("blendShape."))
                        continue;
                    var path = $"{binding.path}/{binding.propertyName}";
                    if (!validPaths.Contains(path))
                        continue;
                    blendShapes.Add((GetBlendShapeID(path), binding));
                    var curve = AnimationUtility.GetEditorCurve(clip, binding);
                    keyframes.UnionWith(curve.keys.Select(x => x.time));
                }
                foreach (var key in keyframes)
                {
                    var blendShapeValues = new Dictionary<int, float>();
                    foreach (var blendShape in blendShapes)
                    {
                        var curve = AnimationUtility.GetEditorCurve(clip, blendShape.binding);
                        blendShapeValues[blendShape.blendShapeID] = curve.Evaluate(key);
                    }
                    NormalizeBlendShapeValues(blendShapeValues);
                    if (!ratiosDict.Any(list => list.SequenceEqual(blendShapeValues)))
                        ratiosDict.Add(blendShapeValues);
                }
                foreach (var blendshape in blendShapes)
                {
                    if (!hasEntryInMergeableBlendShapes.Contains(blendshape.blendShapeID))
                    {
                        hasEntryInMergeableBlendShapes.Add(blendshape.blendShapeID);
                        mergeableBlendShapes.Add(new List<(int blendshapeID, float value)>() { (blendshape.blendShapeID, 1) });
                    }
                }
            }
            var ratiosArray = ratiosDict.Select(x => {
                var array = Enumerable.Repeat(float.NegativeInfinity, blendShapeIDToName.Count).ToArray();
                foreach (var entry in x) {
                    array[entry.Key] = entry.Value;
                }
                return array;
            }).ToArray();
            for (int i = 0; i < mergeableBlendShapes.Count - 1; i++)
            {
                for (int j = i + 1; j < mergeableBlendShapes.Count; j++)
                {
                    var subList = mergeableBlendShapes[i];
                    var candidate = mergeableBlendShapes[j][0].blendshapeID;
                    float value = -1;
                    bool canAddToRatio = true;
                    for (int k = 0; k < ratiosArray.Length; k++) {
                        if (!TryAddBlendShapeToSubList(subList, candidate, ref value, ratiosArray[k])) {
                            canAddToRatio = false;
                            break;
                        }
                    }
                    if (canAddToRatio && value != -1) {
                        subList.Add((candidate, value));
                        NormalizeBlendShapeValues(subList);
                        mergeableBlendShapes.RemoveAt(j);
                        j--;
                    }
                }
            }
            mergeableBlendShapes.RemoveAll(x => x.Count == 1);
            return mergeableBlendShapes.Select(x => x.OrderByDescending(y => y.value).Select(z => (blendShapeIDToName[z.blendshapeID], z.value)).ToList()).ToList();
        }

        private void NormalizeBlendShapeValues(List<(int blendshape, float value)> blendShapeValues)
        {
            var maxValue = blendShapeValues.Max(x => x.value);
            if (maxValue == 0 || maxValue == 1)
                return;
            for (int i = 0; i < blendShapeValues.Count; i++)
            {
                blendShapeValues[i] = (blendShapeValues[i].blendshape, blendShapeValues[i].value / maxValue);
            }
        }

        private void NormalizeBlendShapeValues(Dictionary<int, float> blendShapeValues)
        {
            if (blendShapeValues.Count == 0) return;
            var maxValue = blendShapeValues.Max(x => x.Value);
            if (maxValue == 0 || maxValue == 1)
                return;
            foreach (var key in blendShapeValues.Keys.ToList())
            {
                blendShapeValues[key] /= maxValue;
            }
        }

        private bool TryAddBlendShapeToSubList(List<(int blendshapeID, float value)> subList, int blendshapeID, ref float value, float[] ratioToCheckAgainst)
        {
            int intersectionCount = 0;
            float intersectionMax = 0;
            int subListCount = subList.Count;
            for (int i = 0; i < subListCount; i++)
            {
                float ratioValue = ratioToCheckAgainst[subList[i].blendshapeID];
                if (ratioValue != float.NegativeInfinity)
                {
                    intersectionCount++;
                    intersectionMax = Mathf.Max(intersectionMax, ratioValue);
                }
                else if (intersectionCount > 0)
                {
                    return false;
                }
            }
            float candidateValue = ratioToCheckAgainst[blendshapeID];
            bool hasCandidate = candidateValue != float.NegativeInfinity;
            if (intersectionCount == 0 && !hasCandidate)
                return true;
            if (intersectionCount != subListCount || !hasCandidate)
                return false;
            if (intersectionMax == 0)
                return candidateValue == 0;
            if (candidateValue == 0)
                return false;
            for (int i = 0; i < subListCount; i++)
            {
                var match = ratioToCheckAgainst[subList[i].blendshapeID];
                if (Mathf.Abs(subList[i].value - match / intersectionMax) > 0.01f)
                    return false;
            }
            if (value < 0)
                value = candidateValue / intersectionMax;
            else if (Mathf.Abs(value - candidateValue / intersectionMax) > 0.01f)
                return false;
            return true;
        }

        public void ProcessBlendShapes()
        {
            foreach (var skinnedMeshRenderer in mainInstance.GetUsedComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh = skinnedMeshRenderer.sharedMesh;
                if (mesh == null)
                    continue;
                var blendShapeIDsToBake = new List<int>();
                context.blendShapesToBake[skinnedMeshRenderer] = blendShapeIDsToBake;
                string path = skinnedMeshRenderer.transform.GetPathToRoot(root.transform) + "/blendShape.";
                for (int i = 0; i < mesh.blendShapeCount; i++)
                {
                    var name = mesh.GetBlendShapeName(i);
                    if (settings.MMDCompatibility && OptimizationContext.MMDBlendShapes.Contains(name))
                    {
                        context.usedBlendShapes.Add(path + name);
                        continue;
                    }
                    if (skinnedMeshRenderer.GetBlendShapeWeight(i) != 0 && !context.usedBlendShapes.Contains(path + name))
                    {
                        if (mesh.GetBlendShapeFrameCount(i) > 1)
                        {
                            context.usedBlendShapes.Add(path + name);
                        }
                        else
                        {
                            blendShapeIDsToBake.Add(i);
                        }
                    }
                }
            }
        }

        public long GetPolyCount()
        {
            long polyCount = 0;
            foreach (var renderer in mainInstance.GetUsedComponentsInChildren<Renderer>())
            {
                if (!(renderer is SkinnedMeshRenderer || renderer is MeshRenderer))
                    continue;
                var mesh = renderer.GetSharedMesh();
                if (mesh == null)
                    continue;
                polyCount += Enumerable.Range(0, mesh.subMeshCount).Sum(i => mesh.GetIndexCount(i) / 3);
            }
            return polyCount;
        }
    }
}
#endif