using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using WKAvatarOptimizer.Extensions;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;
using System;
using WKAvatarOptimizer.Core.Util;

using UnityEditor.Animations;

namespace WKAvatarOptimizer.Core
{
    public class AnimationRewriter
    {
        private readonly OptimizationContext context;
        private readonly GameObject root;
        private readonly CacheManager cacheManager;
        private readonly ComponentOptimizer componentOptimizer;
        private readonly FXLayerOptimizer fxLayerOptimizer;
        private readonly MeshOptimizer meshOptimizer;
        private readonly AvatarOptimizer main;

        public AnimationRewriter(OptimizationContext context, GameObject root, CacheManager cacheManager, ComponentOptimizer componentOptimizer, FXLayerOptimizer fxLayerOptimizer, MeshOptimizer meshOptimizer, AvatarOptimizer main)
        {
            this.context = context;
            this.root = root;
            this.cacheManager = cacheManager;
            this.componentOptimizer = componentOptimizer;
            this.fxLayerOptimizer = fxLayerOptimizer;
            this.meshOptimizer = meshOptimizer;
            this.main = main;
        }

        public HashSet<string> FindAllPathsWhereMeshOrGameObjectHasOnlyOnAnimation()
        {
            if (cacheManager.cache_FindAllPathsWhereMeshOrGameObjectHasOnlyOnAnimation == null) {
                cacheManager.cache_FindAllPathsWhereMeshOrGameObjectHasOnlyOnAnimation = new HashSet<string>();
                var goOffPaths = new HashSet<string>();
                var goOnPaths = new HashSet<string>();
                var meshOffPaths = new HashSet<string>();
                var meshOnPaths = new HashSet<string>();
                var fxLayer = fxLayerOptimizer.GetFXLayer();
                var uselessLayers = fxLayerOptimizer.FindUselessFXLayers();
                var fxLayerLayers = fxLayerOptimizer.GetFXLayerLayers();
                for (int i = 0; fxLayer != null && i < fxLayerLayers.Length; i++) {
                    if (fxLayerLayers[i] == null || fxLayerLayers[i].stateMachine == null)
                        continue;
                    if ((uselessLayers.Contains(i) || fxLayerOptimizer.IsMergeableFXLayer(i)))
                        continue;
                    goOffPaths.Clear();
                    goOnPaths.Clear();
                    meshOffPaths.Clear();
                    meshOnPaths.Clear();
                    foreach (var state in fxLayerLayers[i].stateMachine.EnumerateAllStates()) {
                        if (state.motion == null)
                            continue;
                        foreach (var clip in state.motion.EnumerateAllClips()) {
                            foreach (var binding in AnimationUtility.GetCurveBindings(clip)) {
                                if (binding.type == typeof(GameObject) && binding.propertyName == "m_IsActive") {
                                    var curve = AnimationUtility.GetEditorCurve(clip, binding);
                                    foreach (var key in curve.keys) {
                                        if (key.value == 0)
                                            goOffPaths.Add(binding.path);
                                        else if (key.value == 1)
                                            goOnPaths.Add(binding.path);
                                    }
                                } else if (typeof(Renderer).IsAssignableFrom(binding.type) && binding.propertyName == "m_Enabled") {
                                    var curve = AnimationUtility.GetEditorCurve(clip, binding);
                                    foreach (var key in curve.keys) {
                                        if (key.value == 0)
                                            meshOffPaths.Add(binding.path);
                                        else if (key.value == 1)
                                            meshOnPaths.Add(binding.path);
                                    }
                                }
                            }
                        }
                    }
                    cacheManager.cache_FindAllPathsWhereMeshOrGameObjectHasOnlyOnAnimation.UnionWith(goOnPaths.Except(goOffPaths));
                    cacheManager.cache_FindAllPathsWhereMeshOrGameObjectHasOnlyOnAnimation.UnionWith(meshOnPaths.Except(meshOffPaths));
                }
                foreach (var path in cacheManager.cache_FindAllPathsWhereMeshOrGameObjectHasOnlyOnAnimation.ToList()) {
                    var t = root.transform.GetTransformFromPath(path);
                    if (t == null || (t.GetComponent<MeshRenderer>() == null && t.GetComponent<SkinnedMeshRenderer>() == null)) {
                        cacheManager.cache_FindAllPathsWhereMeshOrGameObjectHasOnlyOnAnimation.Remove(path);
                    }
                }
            }
            return cacheManager.cache_FindAllPathsWhereMeshOrGameObjectHasOnlyOnAnimation;
        }


        private HashSet<string> cache_TargetPathHasAnyMaterialSwap = null;
        private bool TargetPathHasAnyMaterialSwap(string path)
        {
            if (cache_TargetPathHasAnyMaterialSwap == null) {
                cache_TargetPathHasAnyMaterialSwap = new HashSet<string>();
                foreach (var oldPath in fxLayerOptimizer.FindAllMaterialSwapMaterials().Keys.Select(key => key.Item1).Distinct()) {
                    var newPath = oldPath;
                    if (context.oldPathToMergedPath.TryGetValue(oldPath, out var mergedPath)) {
                        newPath = mergedPath;
                    }
                    cache_TargetPathHasAnyMaterialSwap.Add(newPath);
                }
            }
            var result = cache_TargetPathHasAnyMaterialSwap.Contains(path);
            return result;
        }

        private EditorCurveBinding FixAnimationBindingPath(EditorCurveBinding binding, ref bool changed)
        {
            var newBinding = binding;
            if (context.transformFromOldPath.TryGetValue(newBinding.path, out var transform))
            {
                if (transform != null)
                {
                    var path = transform.GetPathToRoot(root.transform);
                    if (path.EndsWith("/WKVRCOptimizer_mergeTargetRoot") &&
                        (binding.type == typeof(Transform) || typeof(Renderer).IsAssignableFrom(binding.type)))
                    {
                        path = path.Substring(0, path.Length - "/WKVRCOptimizer_mergeTargetRoot".Length);
                    }
                    changed = changed || path != newBinding.path;
                    newBinding.path = path;
                    if (binding.type == typeof(MeshRenderer) && !transform.TryGetComponent(out MeshRenderer renderer))
                    {
                        newBinding.type = typeof(SkinnedMeshRenderer);
                        changed = true;
                    }
                }
            }
            return newBinding;
        }

        private EditorCurveBinding FixAnimationBinding(EditorCurveBinding binding, ref bool changed)
        {
            var currentPath = (binding.path, binding.propertyName, binding.type);
            var newBinding = binding;
            if (context.newAnimationPaths.TryGetValue(currentPath, out var modifiedPath))        {
                newBinding.path = modifiedPath.Item1;
                newBinding.propertyName = modifiedPath.Item2;
                newBinding.type = modifiedPath.Item3;
                changed = true;
            }
            else if (typeof(Renderer).IsAssignableFrom(binding.type) && context.newAnimationPaths.TryGetValue((binding.path, binding.propertyName, typeof(SkinnedMeshRenderer)), out modifiedPath))        {
                newBinding.path = modifiedPath.Item1;
                newBinding.propertyName = modifiedPath.Item2;
                newBinding.type = modifiedPath.Item3;
                changed = true;
            }
            var result = FixAnimationBindingPath(newBinding, ref changed);
            return result;
        }

        private AnimationCurve ReplaceZeroWithNaN(AnimationCurve curve)
        {
            var newCurve = new AnimationCurve();
            for (int i = 0; i < curve.keys.Length; i++)
            {
                var key = curve.keys[i];
                if (key.value == 0)
                {
                    key.value = float.NaN;
                }
                newCurve.AddKey(key);
            }
            newCurve.preWrapMode = curve.preWrapMode;
            newCurve.postWrapMode = curve.postWrapMode;
            return newCurve;
        }

        private static readonly Dictionary<char, char> otherVectorOrColorComponent = new Dictionary<char, char> {
            { 'x', 'r' }, { 'y', 'g' }, { 'z', 'b' }, { 'w', 'a' },
            { 'r', 'x' }, { 'g', 'y' }, { 'b', 'z' }, { 'a', 'w' },
        };
        Dictionary<string, Dictionary<Type, HashSet<string>>> cache_IsAnimatableBinding = null;
        public bool IsAnimatableBinding(EditorCurveBinding binding) {
            if (cache_IsAnimatableBinding == null)
                cache_IsAnimatableBinding = new Dictionary<string, Dictionary<Type, HashSet<string>>>();

            if (!cache_IsAnimatableBinding.TryGetValue(binding.path, out var animatableBindings)) {
                animatableBindings = new Dictionary<Type, HashSet<string>>();
                GameObject targetObject = root.transform.GetTransformFromPath(binding.path)?.gameObject;
                if (targetObject != null) {
                    foreach (var animatableBinding in AnimationUtility.GetAnimatableBindings(targetObject, root)) {
                        var name = animatableBinding.propertyName;
                        var type = animatableBinding.type;
                        if (!animatableBindings.TryGetValue(type, out var animatableProperties)) {
                            animatableProperties = new HashSet<string>();
                            animatableBindings[type] = animatableProperties;
                        }
                        animatableProperties.Add(name);
                        if (name.Length > 2 && name[name.Length - 2] == '.' && otherVectorOrColorComponent.TryGetValue(name[name.Length - 1], out var otherComponent)) {
                            animatableProperties.Add(name.Substring(0, name.Length - 1) + otherComponent);
                        }
                    }
                    if (!animatableBindings.ContainsKey(typeof(GameObject))) {
                        animatableBindings[typeof(GameObject)] = new HashSet<string>();
                    }
                    animatableBindings[typeof(GameObject)].Add("ComponentExists");
                    foreach (var component in targetObject.GetNonNullComponents()) {
                        var componentType = component.GetType();
                        if (!animatableBindings.ContainsKey(componentType)) {
                            animatableBindings[componentType] = new HashSet<string>();
                        }
                        animatableBindings[componentType].Add("ComponentExists");
                    }
                    if (targetObject.TryGetComponent(out VRC.SDK3.Avatars.Components.VRCStation station)) {
                        var boxColliderType = typeof(BoxCollider);
                        if (!animatableBindings.ContainsKey(boxColliderType)) {
                            animatableBindings[boxColliderType] = new HashSet<string>();
                        }
                        animatableBindings[boxColliderType].UnionWith(new string[] {
                            "ComponentExists", "m_IsTrigger", "m_Enabled",
                            "m_Center.x", "m_Center.y", "m_Center.z",
                            "m_Size.x", "m_Size.y", "m_Size.z"
                        });
                    }
                }
                cache_IsAnimatableBinding[binding.path] = animatableBindings;
            }
            if (componentOptimizer.GetAllExcludedTransformPaths().Contains(binding.path)) {
                return true;
            }
            if (animatableBindings.Count == 0) {
                return false;
            }
            if (binding.propertyName.StartsWithSimple("material.") && TargetPathHasAnyMaterialSwap(binding.path)) {
                return true;
            }
            foreach (var kvp in animatableBindings) {
                if (binding.type.IsAssignableFrom(kvp.Key) && (!typeof(Renderer).IsAssignableFrom(binding.type) || kvp.Value.Contains(binding.propertyName))) {
                    return true;
                }
            }
            return false;
        }
        
        private AnimationClip FixAnimationClipPaths(AnimationClip clip)
        {
            if (clip.name == "WKVRCOptimizer_MergedLayers_Constants") {
                return clip;
            }
            var newClip = UnityEngine.Object.Instantiate(clip);
            newClip.ClearCurves();
            newClip.name = clip.name;
            bool changed = false;
            float lastUsedKeyframeTime = -1;
            float lastUnusedKeyframeTime = -1;
            void SetFloatCurve(AnimationClip clipToSet, EditorCurveBinding bindingToSet, AnimationCurve curveToSet) {
                if (IsAnimatableBinding(bindingToSet)) {
                    lastUsedKeyframeTime = Mathf.Max(curveToSet.length > 0 ? curveToSet.keys[curveToSet.length - 1].time : 0, lastUsedKeyframeTime);
                    AnimationUtility.SetEditorCurve(clipToSet, bindingToSet, curveToSet);
                } else {
                    lastUnusedKeyframeTime = Mathf.Max(curveToSet.length > 0 ? curveToSet.keys[curveToSet.length - 1].time : 0, lastUnusedKeyframeTime);
                    changed = true;
                }
            }
            void SetObjectReferenceCurve(AnimationClip clipToSet, EditorCurveBinding bindingToSet, ObjectReferenceKeyframe[] curveToSet) {
                if (IsAnimatableBinding(bindingToSet)) {
                    lastUsedKeyframeTime = Mathf.Max(curveToSet.Length > 0 ? curveToSet[curveToSet.Length - 1].time : 0, lastUsedKeyframeTime);
                    AnimationUtility.SetObjectReferenceCurve(clipToSet, bindingToSet, curveToSet);
                } else {
                    lastUnusedKeyframeTime = Mathf.Max(curveToSet.Length > 0 ? curveToSet[curveToSet.Length - 1].time : 0, lastUnusedKeyframeTime);
                    changed = true;
                }
            }
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                var fixedBinding = FixAnimationBinding(binding, ref changed);
                if (binding.type == typeof(GameObject) && binding.propertyName == "m_IsActive" && !context.pathsToDeleteGameObjectTogglesOn.Contains(binding.path))
                {
                    SetFloatCurve(newClip, FixAnimationBindingPath(binding, ref changed), curve);
                }
                if (fixedBinding.propertyName.StartsWithSimple("NaNimation")) {
                    var shaderToggleInfo = fixedBinding.propertyName.Substring("NaNimation".Length);
                    var propertyNames = new string[] { "m_LocalScale.x", "m_LocalScale.y", "m_LocalScale.z" };
                    var NaNCurve = ReplaceZeroWithNaN(curve);
                    for (int i = 0; i < propertyNames.Length; i++) {
                        fixedBinding.propertyName = propertyNames[i];
                        SetFloatCurve(newClip, fixedBinding, NaNCurve);
                    }
                    if (shaderToggleInfo.Length > 0) {
                        shaderToggleInfo = shaderToggleInfo.Substring(1);
                        var semicolonIndex = shaderToggleInfo.IndexOf(';');
                        fixedBinding.path = shaderToggleInfo.Substring(semicolonIndex + 1);
                        fixedBinding.propertyName = $"material._IsActiveMesh{shaderToggleInfo.Substring(0, semicolonIndex)}";
                        fixedBinding.type = typeof(SkinnedMeshRenderer);
                        SetFloatCurve(newClip, FixAnimationBindingPath(fixedBinding, ref changed), curve);
                    }
                } else {
                    SetFloatCurve(newClip, fixedBinding, curve);
                    if (fixedBinding.propertyName.StartsWithSimple($"material.WKVRCOptimizer")) {
                        var otherBinding = fixedBinding;
                        var match = Regex.Match(fixedBinding.propertyName, @"material\.WKVRCOptimizer(.+)_ArrayIndex\d+(\.[a-z])?");
                        otherBinding.propertyName = $"material.{match.Groups[1].Value}{match.Groups[2].Value}";
                        SetFloatCurve(newClip, otherBinding, curve);
                    }
                }
                bool addPhysBoneCurves = (binding.type == typeof(SkinnedMeshRenderer) && binding.propertyName == "m_Enabled")
                    || (binding.type == typeof(GameObject) && binding.propertyName == "m_IsActive");
                if (addPhysBoneCurves && context.physBonesToDisable.TryGetValue(binding.path, out var physBonePaths))
                {
                    var physBoneBinding = binding;
                    physBoneBinding.propertyName = "m_Enabled";
                    physBoneBinding.type = typeof(VRCPhysBone);
                    foreach (var physBonePath in physBonePaths)
                    {
                        physBoneBinding.path = physBonePath;
                        SetFloatCurve(newClip, FixAnimationBindingPath(physBoneBinding, ref changed), curve);
                        changed = true;
                    }
                }
            }
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                var curve = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                if (fxLayerOptimizer.GetAllMaterialSwapBindingsToRemove().Contains(binding))
                {
                    lastUnusedKeyframeTime = Mathf.Max(curve.Length > 0 ? curve[^1].time : 0, lastUnusedKeyframeTime);
                    changed = true;
                    continue;
                }
                for (int i = 0; i < curve.Length; i++)
                {
                    var oldMat = curve[i].value as Material;
                    if (oldMat == null)
                        continue;
                    if (!int.TryParse(binding.propertyName.Substring(binding.propertyName.LastIndexOf('[') + 1).TrimEnd(']'), out int index))
                        continue;
                    if (context.optimizedSlotSwapMaterials.TryGetValue((binding.path, index), out var newMats))
                    {
                        if (newMats.TryGetValue(oldMat, out var newMat))
                        {
                            curve[i].value = newMat;
                            changed = true;
                        }
                    }
                }
                var newBinding = FixAnimationBinding(binding, ref changed);
                SetObjectReferenceCurve(newClip, newBinding, curve);
            }
            if (lastUnusedKeyframeTime > lastUsedKeyframeTime && lastUnusedKeyframeTime > -1) {
                var dummyBinding = EditorCurveBinding.FloatCurve("ThisHopefullyDoesntExist", typeof(GameObject), "m_IsActive");
                var dummyCurve = AnimationCurve.Constant(0, lastUnusedKeyframeTime, 1);
                AnimationUtility.SetEditorCurve(newClip, dummyBinding, dummyCurve);
                changed = true;
                if (lastUsedKeyframeTime == -1) {
                    if (cacheManager.cache_DummyAnimationClipOfLength == null) {
                        cacheManager.cache_DummyAnimationClipOfLength = new Dictionary<float, AnimationClip>();
                    }
                    if (!cacheManager.cache_DummyAnimationClipOfLength.TryGetValue(lastUnusedKeyframeTime, out var dummyClip)) {
                        newClip.name = $"DummyClip_{lastUnusedKeyframeTime}";
                        AssetManager.CreateUniqueAsset(context, newClip, newClip.name + ".anim");
                        cacheManager.cache_DummyAnimationClipOfLength[lastUnusedKeyframeTime] = dummyClip = newClip;
                    }
                    return dummyClip;
                }
            }
            if (changed)
            {
                AssetManager.CreateUniqueAsset(context, newClip, newClip.name + ".anim");
                return newClip;
            }
            return clip;
        }

        private Motion FixMotion(Motion motion, Dictionary<Motion, Motion> fixedMotions, string assetPath)
        {
            if (motion == null) {
                return null;
            }
            if (fixedMotions.TryGetValue(motion, out var fixedMotionValue)) {
                return fixedMotionValue;
            }
            if (motion is BlendTree oldTree)
            {
                var newTree = new BlendTree();
                newTree.name = oldTree.name;
                newTree.blendType = oldTree.blendType;
                newTree.blendParameter = oldTree.blendParameter;
                newTree.blendParameterY = oldTree.blendParameterY;
                newTree.minThreshold = oldTree.minThreshold;
                newTree.maxThreshold = oldTree.maxThreshold;
                newTree.useAutomaticThresholds = oldTree.useAutomaticThresholds;
                var childNodes = oldTree.children;
                for (int j = 0; j < childNodes.Length; j++)
                {
                    childNodes[j].motion = FixMotion(childNodes[j].motion, fixedMotions, assetPath);
                }
                newTree.children = childNodes;
                fixedMotions[motion] = newTree;
                newTree.hideFlags = HideFlags.HideInHierarchy;
                AnimatorOptimizer.CopyNormalizedBlendValuesProperty(oldTree, newTree);
                Profiler.StartSection("AssetDatabase.AddObjectToAsset()");
                AssetDatabase.AddObjectToAsset(newTree, assetPath);
                Profiler.EndSection();
                return newTree;
            }
            return motion;
        }
        
        public void FixAllAnimationPaths()
        {
            var avDescriptor = root.GetComponent<VRCAvatarDescriptor>();
            if (avDescriptor == null) {
                return;
            }
            
            int totalControllerCount = avDescriptor.baseAnimationLayers.Length + avDescriptor.specialAnimationLayers.Length;
            var layerCopyPaths = new string[totalControllerCount];
            var optimizedControllers = new AnimatorController[totalControllerCount];

            var fxLayersToMerge = new List<int>();
            var fxLayersToDestroy = new List<int>();
            var fxLayerMap = new Dictionary<int, int>();
            if (fxLayerOptimizer.GetFXLayer() != null)
            {
                var nonErrors = new HashSet<string>() {"toggle", "motion time", "blend tree", "multi toggle"};
                var errors = fxLayerOptimizer.AnalyzeFXLayerMergeAbility();
                var uselessLayers = fxLayerOptimizer.FindUselessFXLayers();
                int currentLayer = 0;
                for (int i = 0; i < fxLayerOptimizer.GetFXLayerLayers().Length; i++)
                {
                    fxLayerMap[i] = currentLayer;
                    if (uselessLayers.Contains(i))
                    {
                        fxLayersToDestroy.Add(i);
                        continue;
                    }
                    if (errors[i].All(e => nonErrors.Contains(e)))
                    {
                        fxLayersToMerge.Add(i);
                        continue;
                    }
                    currentLayer++;
                }
                if (fxLayersToMerge.Count < 2 && fxLayersToDestroy.Count == 0)
                {
                    fxLayersToMerge.Clear();
                    fxLayerMap.Clear();
                }
            }

            Profiler.StartSection("AnimatorOptimizer.Run()");
            for (int i = 0; i < avDescriptor.baseAnimationLayers.Length; i++)
            {
                var controller = avDescriptor.baseAnimationLayers[i].animatorController as AnimatorController;
                if (controller == null)
                    continue;
                layerCopyPaths[i] = $"{context.trashBinPath}BaseAnimationLayer{i}{controller.name}(OptimizedCopy).controller";
                optimizedControllers[i] = controller == fxLayerOptimizer.GetFXLayer()
                    ? AnimatorOptimizer.Run(controller, layerCopyPaths[i], fxLayerMap, fxLayersToMerge, fxLayersToDestroy, context.constantAnimatedValuesToAdd.Select(kvp => (kvp.Key, kvp.Value)).ToList())
                    : AnimatorOptimizer.Copy(controller, layerCopyPaths[i], fxLayerMap);
                optimizedControllers[i].name = $"BaseAnimationLayer{i}{controller.name}(OptimizedCopy)";
                avDescriptor.baseAnimationLayers[i].animatorController = optimizedControllers[i];
            }
            for (int i = 0; i < avDescriptor.specialAnimationLayers.Length; i++)
            {
                var controller = avDescriptor.specialAnimationLayers[i].animatorController as AnimatorController;
                if (controller == null)
                    continue;
                var index = i + avDescriptor.baseAnimationLayers.Length;
                layerCopyPaths[index] = $"{context.trashBinPath}SpecialAnimationLayer{index}{controller.name}(OptimizedCopy).controller";
                optimizedControllers[index] = AnimatorOptimizer.Copy(controller, layerCopyPaths[index], fxLayerMap);
                optimizedControllers[index].name = $"SpecialAnimationLayer{index}{controller.name}(OptimizedCopy)";
                avDescriptor.specialAnimationLayers[i].animatorController = optimizedControllers[index];
            }
            Profiler.EndSection();

            var animations = new HashSet<AnimationClip>();
            for (int i = 0; i < optimizedControllers.Length; i++)
            {
                if (optimizedControllers[i] == null)
                    continue;
                animations.UnionWith(optimizedControllers[i].animationClips);
            }

            var fixedMotions = new Dictionary<Motion, Motion>();
            foreach (var clip in animations)
            {
                fixedMotions[clip] = FixAnimationClipPaths(clip);
            }
            
            for (int i = 0; i < optimizedControllers.Length; i++)
            {
                var newController = optimizedControllers[i];
                if (newController == null)
                    continue;

                foreach (var state in newController.EnumerateAllStates())
                {
                    state.motion = FixMotion(state.motion, fixedMotions, layerCopyPaths[i]);
                }

                var layers = newController.layers;
                var syncedLayerIndices = layers.Select((layer, index) => (layer, index)).Where(p => p.layer != null && p.layer.syncedLayerIndex >= 0).Select(p => p.index).ToArray();
                foreach (var syncedLayerIndex in syncedLayerIndices)
                {
                    var syncedLayer = layers[syncedLayerIndex];
                    foreach (var stateMotionPair in syncedLayer.EnumerateAllMotionOverrides())
                    {
                        syncedLayer.SetOverrideMotion(stateMotionPair.state, FixMotion(stateMotionPair.motion, fixedMotions, layerCopyPaths[i]));
                    }
                }
                if (syncedLayerIndices.Length > 0)
                {
                    newController.layers = layers;
                }

                {
                    foreach (var behavior in newController.layers.SelectMany(layer => layer.stateMachine.EnumerateAllBehaviours()))
                    {
                        if (behavior is VRC.SDKBase.VRC_AnimatorPlayAudio playAudio)
                        {
                            var path = playAudio.SourcePath ?? "";
                            if (context.transformFromOldPath.TryGetValue(path, out var transform) && transform != null)
                            {
                                playAudio.SourcePath = transform.GetPathToRoot(root.transform);
                            }
                        }
                    }
                }
            }
            Profiler.StartSection("AssetDatabase.SaveAssets()");
            AssetDatabase.SaveAssets();
            Profiler.EndSection();
        }
    }
}