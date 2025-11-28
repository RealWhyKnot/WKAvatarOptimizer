using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using WKAvatarOptimizer.Extensions;
using WKAvatarOptimizer.Core.Util;

using BlendableLayer = VRC.SDKBase.VRC_AnimatorLayerControl.BlendableLayer;

namespace WKAvatarOptimizer.Core
{
    public class FXLayerOptimizer
    {
        private readonly OptimizationContext context;
        private readonly GameObject gameObject;
        private readonly AvatarOptimizer optimizer;

        private AnimatorControllerLayer[] cache_GetFXLayerLayers = null;
        private List<List<string>> cache_AnalyzeFXLayerMergeAbility = null;
        private HashSet<int> cache_FindUselessFXLayers = null;
        private HashSet<AnimationClip> cache_GetAllUsedFXLayerAnimationClips = null;
        private bool? cache_DoesFXLayerUseWriteDefaults = null;
        private HashSet<EditorCurveBinding> cache_GetAllUsedFXLayerCurveBindings = null;
        
        private HashSet<string> cache_FindAllGameObjectTogglePaths = null;
        private HashSet<string> cache_FindAllRendererTogglePaths = null;
        private Dictionary<string, HashSet<string>> cache_FindAllAnimatedMaterialProperties = null;
        private Dictionary<(string, int), HashSet<Material>> cache_FindAllMaterialSwapMaterials = null;
        private HashSet<EditorCurveBinding> cache_GetAllMaterialSwapBindingsToRemove = null;
        private Dictionary<EditorCurveBinding, bool> cache_MaterialSwapBindingsToRemove = null;

        public FXLayerOptimizer(OptimizationContext context, GameObject gameObject, AvatarOptimizer optimizer)
        {
            this.context = context;
            this.gameObject = gameObject;
            this.optimizer = optimizer;
        }

        public bool IsHumanoid()
        {
            var rootAnimator = gameObject.GetComponent<Animator>();
            var result = rootAnimator != null && rootAnimator.avatar != null && rootAnimator.avatar.isHuman;
            return result;
        }

        public AnimatorController GetFXLayer()
        {
            var avDescriptor = gameObject.GetComponent<VRCAvatarDescriptor>();
            var baseLayerCount = IsHumanoid() ? 5 : 3;
            if (avDescriptor == null || avDescriptor.baseAnimationLayers.Length != baseLayerCount) {
                return null;
            }
            var result = avDescriptor.baseAnimationLayers[baseLayerCount - 1].animatorController as AnimatorController;
            return result;
        }

        public AnimatorControllerLayer[] GetFXLayerLayers()
        {
            if (cache_GetFXLayerLayers != null) {
                return cache_GetFXLayerLayers;
            }
            var fxLayer = GetFXLayer();
            var result = fxLayer != null ? fxLayer.layers : new AnimatorControllerLayer[0];
            return cache_GetFXLayerLayers = result;
        }

        public List<List<string>> AnalyzeFXLayerMergeAbility()
        {
            if (cache_AnalyzeFXLayerMergeAbility != null) {
                return cache_AnalyzeFXLayerMergeAbility;
            }
            var fxLayer = GetFXLayer();
            if (fxLayer == null) {
                return new List<List<string>>();
            }
            var avDescriptor = gameObject.GetComponent<VRCAvatarDescriptor>();

            var fxLayerLayers = GetFXLayerLayers();
            var errorMessages = fxLayerLayers.Select(layer => new List<string>()).ToList();

            for (int i = 0; i < avDescriptor.baseAnimationLayers.Length; i++)
            {
                var controller = avDescriptor.baseAnimationLayers[i].animatorController as AnimatorController;
                if (controller == null)
                    continue;
                var controllerLayers = controller.layers;
                for (int j = 0; j < controllerLayers.Length; j++)
                {
                    var layer = controllerLayers[j];
                    var stateMachine = layer.stateMachine;
                    if (stateMachine == null)
                        continue;
                    foreach (var behaviour in stateMachine.EnumerateAllBehaviours())
                    {
                        if (behaviour is VRCAnimatorLayerControl layerControl)
                        {
                            if (layerControl.layer <= errorMessages.Count && layerControl.playable == BlendableLayer.FX)
                            {
                                var playableName = new string[] { "Base", "Additive", "Gesture", "Action", "FX" }[i];
                                errorMessages[layerControl.layer].Add($"layer control from {playableName} {j} {layer.name}");
                            }
                        }
                    }
                }
            }

            var fxLayerBindings = fxLayerLayers.Select(layer => GetAllCurveBindings(layer.stateMachine)).ToList();
            var uselessLayers = FindUselessFXLayers();
            var fxLayerParameters = fxLayer.parameters;
            var intParams = new HashSet<string>(fxLayerParameters.Where(p => p.type == AnimatorControllerParameterType.Int).Select(p => p.name));
            var intParamsWithNotEqualConditions = new HashSet<string>();

            for (int i = 0; i < fxLayerLayers.Length; i++) {
                if (uselessLayers.Contains(i)) {
                    continue;
                }
                foreach (var condition in fxLayerLayers[i].stateMachine.EnumerateAllTransitions().SelectMany(t => t.conditions)) {
                    if (condition.mode == AnimatorConditionMode.NotEqual && intParams.Contains(condition.parameter)) {
                        intParamsWithNotEqualConditions.Add(condition.parameter);
                    }
                }
            }

            for (int i = 0; i < fxLayerLayers.Length; i++)
            {
                if (uselessLayers.Contains(i))
                {
                    errorMessages[i].Add("useless");
                    continue;
                }
                if (i <= 2)
                {
                    errorMessages[i].Add("MMD compatibility requires the first 3 layers to be kept as is");
                    continue;
                }
                var layer = fxLayerLayers[i];
                if (layer.syncedLayerIndex != -1)
                {
                    errorMessages[i].Add($"synced with layer {layer.syncedLayerIndex}");
                    if (layer.syncedLayerIndex >= 0 && layer.syncedLayerIndex < fxLayerLayers.Length)
                    {
                        errorMessages[layer.syncedLayerIndex].Insert(0, $"layer {i} is synced with this layer");
                    }
                    continue;
                }
                var stateMachine = layer.stateMachine;
                if (stateMachine == null)
                {
                    errorMessages[i].Add("no state machine");
                    continue;
                }
                if (stateMachine.stateMachines.Length != 0)
                {
                    errorMessages[i].Add($"{stateMachine.stateMachines.Length} sub state machines");
                    continue;
                }
                if (stateMachine.EnumerateAllBehaviours().Any())
                {
                    errorMessages[i].Add($"has state machine behaviours");
                    continue;
                }
                if (layer.defaultWeight != 1)
                {
                    errorMessages[i].Add($"default weight {layer.defaultWeight}");
                }
                var states = stateMachine.states;
                if (states.Length == 0)
                {
                    errorMessages[i].Add($"{states.Length} states");
                    continue;
                }

                bool IsStateConvertableToAnimationOrBlendTree(AnimatorState state) {
                    if (state.motion == null) {
                        errorMessages[i].Add($"{state.name} has no motion");
                        return false;
                    }
                    if (state.motion is AnimationClip clip) {
                        if (state.timeParameterActive) {
                            if (!CombineApproximateMotionTimeAnimations) {
                                errorMessages[i].Add($"{state.name} has motion time and motion time combination is disabled");
                                return false;
                            }
                        } else {
                            var bindings = AnimationUtility.GetCurveBindings(clip);
                            foreach (var binding in bindings) {
                                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                                if (curve.keys.Length <= 1)
                                    continue;
                                var firstValue = curve.keys[0].value;
                                for (int j = 1; j < curve.keys.Length; j++) {
                                    if (curve.keys[j].value != firstValue) {
                                        errorMessages[i].Add($"{state.name} has non-constant curve for {binding.propertyName}");
                                        return false;
                                    }
                                }
                            }
                        }
                        if (AnimationUtility.GetObjectReferenceCurveBindings(clip).Any()) {
                            errorMessages[i].Add($"{state.name} has object reference curves");
                            return false;
                        }
                        return true;
                    }
                    if (state.motion is BlendTree blendTree) {
                        if (state.timeParameterActive) {
                            errorMessages[i].Add($"{state.name} is blend tree and has motion time");
                            return false;
                        }
                        if (!state.writeDefaultValues && blendTree.blendType == BlendTreeType.Direct) {
                            errorMessages[i].Add($"{state.name} is direct blend tree and does not have write defaults");
                            return false;
                        }
                        return true;
                    }
                    errorMessages[i].Add($"{state.name} is not null, animation clip or blend tree ???");
                    return false;
                }

                HashSet<EditorCurveBinding> GetAllMotionBindings(AnimatorState state) {
                    return new HashSet<EditorCurveBinding>(state.motion.EnumerateAllClips().SelectMany(c => AnimationUtility.GetCurveBindings(c)));
                }

                if (states.Length == 1) {
                    var state = states[0].state;
                    if (!IsStateConvertableToAnimationOrBlendTree(state))
                        continue;
                    errorMessages[i].Add(state.motion is AnimationClip ? $"motion time" : $"blend tree");
                }

                var param = states.SelectMany(s => s.state.transitions).Concat(stateMachine.anyStateTransitions).SelectMany(t => t.conditions).Select(c => c.parameter).Distinct().ToList();
                var paramLookup = param.ToDictionary(p => p, p => fxLayerParameters.FirstOrDefault(p2 => p2.name == p));
                if (paramLookup.Values.Any(p => p == null)) {
                    errorMessages[i].Add($"didn't find parameter {paramLookup.First(p => p.Value == null).Key}");
                    continue;
                }

                if (states.Length == 2) {
                    int singleTransitionStateIndex = Array.FindIndex(states, s => s.state.transitions.Length == 1);
                    bool usesAnyStateTransitions = false;
                    if (singleTransitionStateIndex == -1)
                    {
                        if (states.Sum(s => s.state.transitions.Length) > 0)
                        {
                            errorMessages[i].Add($"no single transition state");
                            continue;
                        }
                        var anyStateTransitionDestinationIndices = stateMachine.anyStateTransitions.Select(t => Array.FindIndex(states, s => s.state == t.destinationState)).ToList();
                        if (anyStateTransitionDestinationIndices.Any(i => i < 0 || i >= states.Length))
                        {
                            errorMessages[i].Add($"any state transition destination state is not in the states array");
                            continue;
                        }
                        var state0transitions = anyStateTransitionDestinationIndices.Count(i => i == 0);
                        var state1transitions = anyStateTransitionDestinationIndices.Count(i => i == 1);
                        if (state0transitions != 1 && state1transitions != 1)
                        {
                            errorMessages[i].Add($"no single transition state");
                            continue;
                        }
                        singleTransitionStateIndex = state0transitions == 1 ? 1 : 0;
                        usesAnyStateTransitions = true;
                    }
                    else if (stateMachine.anyStateTransitions.Length != 0)
                    {
                        errorMessages[i].Add($"has any state transitions");
                        continue;
                    }
                    var orState = states[singleTransitionStateIndex].state;
                    var andState = states[1 - singleTransitionStateIndex].state;
                    AnimatorStateTransition[] orStateTransitions = orState.transitions;
                    AnimatorStateTransition[] andStateTransitions = andState.transitions;
                    if (usesAnyStateTransitions)
                    {
                        orStateTransitions = stateMachine.anyStateTransitions.Where(t => t.destinationState == andState).ToArray();
                        andStateTransitions = stateMachine.anyStateTransitions.Where(t => t.destinationState == orState).ToArray();
                    }
                    var stateTransitions = singleTransitionStateIndex == 0
                        ? new AnimatorStateTransition[][] { orStateTransitions, andStateTransitions }
                        : new AnimatorStateTransition[][] { andStateTransitions, orStateTransitions };
                    if (orStateTransitions[0].conditions.Length != andStateTransitions.Length)
                    {
                        errorMessages[i].Add($"or state has {orStateTransitions[0].conditions.Length} conditions but and state has {andStateTransitions.Length} transitions");
                        continue;
                    }
                    if (andStateTransitions.Length == 0) {
                        errorMessages[i].Add($"and state has no transitions");
                        continue;
                    }
                    if (andStateTransitions.Any(t => t.conditions.Length != 1)) {
                        errorMessages[i].Add($"a and state transition has multiple conditions");
                        continue;
                    }
                    bool conditionError = false;
                    foreach (var condition in orStateTransitions[0].conditions) {
                        if (condition.mode == AnimatorConditionMode.Equals || condition.mode == AnimatorConditionMode.NotEqual) {
                            errorMessages[i].Add($"a transition condition mode is {condition.mode}");
                            conditionError = true;
                            break;
                        }
                        if (intParamsWithNotEqualConditions.Contains(condition.parameter)) {
                            errorMessages[i].Add($"parameter {condition.parameter} has a not equal condition somewhere");
                            conditionError = true;
                            break;
                        }
                        var inverseConditionMode = condition.mode == AnimatorConditionMode.If ? AnimatorConditionMode.IfNot : AnimatorConditionMode.If;
                        if (condition.mode == AnimatorConditionMode.Greater)
                            inverseConditionMode = AnimatorConditionMode.Less;
                        if (condition.mode == AnimatorConditionMode.Less)
                            inverseConditionMode = AnimatorConditionMode.Greater;
                        if ((condition.mode == AnimatorConditionMode.If || condition.mode == AnimatorConditionMode.IfNot)
                            && !andStateTransitions.Any(t => t.conditions.Any(c => c.parameter == condition.parameter && c.mode == inverseConditionMode))) {
                            errorMessages[i].Add($"condition with parameter {condition.parameter} has no inverse transition");
                            conditionError = true;
                            break;
                        }
                        if (paramLookup[condition.parameter].type == AnimatorControllerParameterType.Float) {
                            errorMessages[i].Add($"parameter {condition.parameter} is float");
                            conditionError = true;
                            break;
                        }
                        bool isInt = paramLookup[condition.parameter].type == AnimatorControllerParameterType.Int;
                        float inverseThreshold = condition.threshold + (isInt ? (condition.mode == AnimatorConditionMode.Greater ? 1 : -1) : 0);
                        if ((condition.mode == AnimatorConditionMode.Greater || condition.mode == AnimatorConditionMode.Less)
                            && !andStateTransitions.Any(t => t.conditions.Any(c => c.parameter == condition.parameter && c.mode == inverseConditionMode && c.threshold == inverseThreshold))) {
                            errorMessages[i].Add($"condition with parameter {condition.parameter} has no inverse transition");
                            conditionError = true;
                            break;
                        }
                    }
                    if (conditionError) {
                        continue;
                    }

                    bool reliesOnWriteDefaults = false;
                    for (int j = 0; j < 2; j++) {
                        var state = states[j].state;
                        var otherState = states[1 - j].state;
                        if (stateTransitions[j].Any(t => t.destinationState != otherState)) {
                            errorMessages[i].Add($"{state} transition destination state is not the other state");
                            break;
                        }
                        if (stateTransitions[j].Any(t => t.hasExitTime && t.exitTime != 0.0f)) {
                            errorMessages[i].Add($"{state} transition has exit time");
                            break;
                        }
                        if (AnimatorOptimizer.IsNullOrEmpty(state.motion)) {
                            if (AnimatorOptimizer.IsNullOrEmpty(otherState.motion)) {
                                errorMessages[i].Add($"both states have no motion or are empty clips");
                                break;
                            } else if (!(otherState.motion is AnimationClip)) {
                                errorMessages[i].Add($"state {j} has no motion or an empty clip but {1 - j} has non animation clip motion");
                                break;
                            } else if (!otherState.writeDefaultValues || !state.writeDefaultValues) {
                                errorMessages[i].Add($"state {j} has no motion or an empty clip but states do not have write defaults");
                                break;
                            } else {
                                reliesOnWriteDefaults = true;
                            }
                            continue;
                        } else if (!IsStateConvertableToAnimationOrBlendTree(state)) {
                            break;
                        }
                    }
                    bool onlyBoolBindings = true;
                    foreach (var binding in fxLayerBindings[i]) {
                        if (!binding.path.EndsWith("m_Enabled") && !binding.path.EndsWith("m_IsActive")) {
                            onlyBoolBindings = false;
                            break;
                        }
                    }
                    if (!reliesOnWriteDefaults) {
                        var bindings0 = GetAllMotionBindings(states[0].state);
                        var bindings1 = GetAllMotionBindings(states[1].state);
                        if (!bindings1.SetEquals(bindings0)) {
                            bindings1.Except(bindings0).Concat(bindings0.Except(bindings1)).ToList()
                                .ForEach(b => errorMessages[i].Add($"{b.path}/{b.propertyName} is not animated in both states"));
                            continue;
                        }
                    }
                    if (reliesOnWriteDefaults && !onlyBoolBindings) {
                        errorMessages[i].Add($"relies on write defaults and animates something other than m_Enabled/m_IsActive");
                    }
                    if (stateTransitions.Any(s => s.Any(t => t.duration != 0.0f)) && !onlyBoolBindings) {
                        errorMessages[i].Add($"transition has non 0 duration and animates something other than m_Enabled/m_IsActive");
                    }
                    errorMessages[i].Add($"toggle");
                }

                if (states.Length > 2) {
                    if (paramLookup.Count != 1 || (paramLookup.Count == 1 && paramLookup.First().Value.type != AnimatorControllerParameterType.Int)) {
                        errorMessages[i].Add($"more than 2 states and not a single int parameter");
                        continue;
                    }
                    if (states.Any(s => s.state.transitions.Length != 0)) {
                        errorMessages[i].Add($"more than 2 states and a state has transitions");
                        continue;
                    }
                    if (intParamsWithNotEqualConditions.Contains(paramLookup.First().Key)) {
                        errorMessages[i].Add($"parameter {paramLookup.First().Key} has a not equal condition somewhere");
                        continue;
                    }
                    if (stateMachine.anyStateTransitions.Length != states.Length) {
                        errorMessages[i].Add($"any state transitions length is not the same as states length");
                        continue;
                    }
                    if (stateMachine.anyStateTransitions.Any(t => t.conditions.Length != 1)) {
                        errorMessages[i].Add($"a transition has multiple conditions");
                        continue;
                    }
                    if (stateMachine.anyStateTransitions.Any(t => (t.hasExitTime && t.exitTime != 0.0f) || t.duration != 0.0f)) {
                        errorMessages[i].Add($"a transition has non 0 exit time or duration");
                        continue;
                    }
                    var thresholdsNeeded = Enumerable.Range(0, states.Length).Select(number => (float)number).ToList();
                    if (thresholdsNeeded.Any(t => !stateMachine.anyStateTransitions.Any(tr => tr.conditions[0].threshold == t && tr.conditions[0].mode == AnimatorConditionMode.Equals))) {
                        errorMessages[i].Add($"missing some transition with correct threshold and condition mode");
                        continue;
                    }
                    if (states.Any(s => s.state.motion == null)) {
                        errorMessages[i].Add($"a state has no motion");
                        continue;
                    }
                    if (states.Any(s => !IsStateConvertableToAnimationOrBlendTree(s.state))) {
                        continue;
                    }
                    var firstBindings = GetAllMotionBindings(states[0].state);
                    bool hadError = false;
                    for (int j = 1; j < states.Length; j++) {
                        var bindings = GetAllMotionBindings(states[j].state);
                        if (!bindings.SetEquals(firstBindings)) {
                            bindings.Except(firstBindings).Concat(firstBindings.Except(bindings)).ToList()
                                .ForEach(b => errorMessages[i].Add($"{b.path}/{b.propertyName} is not animated in all states"));
                            hadError = true;
                            break;
                        }
                    }
                    if (hadError) {
                        continue;
                    }
                    errorMessages[i].Add($"multi toggle");
                }

                for (int j = 0; j < fxLayerLayers.Length; j++) {
                    if (i == j || uselessLayers.Contains(j))
                        continue;
                    foreach (var binding in fxLayerBindings[i]) {
                        if (fxLayerBindings[j].Contains(binding)) {
                            errorMessages[i].Add($"{binding.path} is used in {j} {fxLayerLayers[j].name}");
                        }
                    }
                }
            }
            for (int i = 0; i < errorMessages.Count; i++)
            {
                errorMessages[i] = errorMessages[i].Distinct().ToList();
            }
            return cache_AnalyzeFXLayerMergeAbility = errorMessages;
        }
        
        private bool CombineApproximateMotionTimeAnimations {
            get { return true; }
        }

        private HashSet<(string path, Type type)> GetAllCurveBindings(AnimatorStateMachine stateMachine)
        {
            var result = new HashSet<(string, Type)>();
            if (stateMachine == null) {
                return result;
            }
            foreach (var state in stateMachine.EnumerateAllStates())
            {
                if (state.motion == null)
                    continue;
                foreach (var clip in state.motion.EnumerateAllClips())
                {
                    var bindings = AnimationUtility.GetCurveBindings(clip);
                    foreach (var binding in bindings)
                    {
                        result.Add(($"{binding.path}.{binding.propertyName}", binding.type));
                    }
                    bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
                    foreach (var binding in bindings)
                    {
                        result.Add(($"{binding.path}.{binding.propertyName}", binding.type));
                    }
                }
            }
            return result;
        }

        public bool IsMergeableFXLayer(int layerIndex)
        {
            var errors = AnalyzeFXLayerMergeAbility();
            var nonErrors = new HashSet<string>() {"toggle", "motion time", "useless", "blend tree", "multi toggle"};
            var result = layerIndex < errors.Count && errors[layerIndex].Count == 1 && nonErrors.Contains(errors[layerIndex][0]);
            return result;
        }

        public HashSet<int> FindUselessFXLayers()
        {
            if (cache_FindUselessFXLayers != null) {
                return cache_FindUselessFXLayers;
            }
            var fxLayer = GetFXLayer();
            if (fxLayer == null) {
                return new HashSet<int>();
            }
            Profiler.StartSection("FindUselessFXLayers()");
            var avDescriptor = gameObject.GetComponent<VRCAvatarDescriptor>();

            var isAffectedByLayerWeightControl = new HashSet<int>();

            for (int i = 0; i < avDescriptor.baseAnimationLayers.Length; i++)
            {
                var controller = avDescriptor.baseAnimationLayers[i].animatorController as AnimatorController;
                if (controller == null)
                    continue;
                var controllerLayers = controller.layers;
                for (int j = 0; j < controllerLayers.Length; j++)
                {
                    var stateMachine = controllerLayers[j].stateMachine;
                    if (stateMachine == null)
                        continue;
                    foreach (var behaviour in stateMachine.EnumerateAllBehaviours())
                    {
                        if (behaviour is VRCAnimatorLayerControl layerControl && layerControl.playable == BlendableLayer.FX)
                        {
                            isAffectedByLayerWeightControl.Add(layerControl.layer);
                        }
                    }
                }
            }

            var uselessLayers = new HashSet<int>();

            var possibleBindingTypes = new Dictionary<string, Type[]>();
            bool IsPossibleBinding(EditorCurveBinding binding)
            {
                if (!possibleBindingTypes.TryGetValue(binding.path, out var possibleTypes))
                {
                    var uniquePossibleTypes = new HashSet<Type>();
                    var transform = this.gameObject.transform.GetTransformFromPath(binding.path);
                    if (transform != null)
                    {
                        uniquePossibleTypes.UnionWith(transform.GetNonNullComponents().Select(c => c.GetType()));
                        uniquePossibleTypes.Add(typeof(GameObject));
                    }
                    possibleTypes = possibleBindingTypes[binding.path] = uniquePossibleTypes.ToArray();
                }
                return possibleTypes.Any(t => binding.type.IsAssignableFrom(t));
            }

            var fxLayerLayers = GetFXLayerLayers();
            int lastNonUselessLayer = fxLayerLayers.Length;
            for (int i = fxLayerLayers.Length - 1; i >= 0; i--)
            {
                if (i <= 2) {
                    // context.Log($"[FXLayer] Skipping analysis for reserved layer {i} ('{fxLayerLayers[i].name}').");
                    break;
                }
                var layer = fxLayerLayers[i];
                if (layer.syncedLayerIndex != -1) {
                    continue;
                }
                bool isNotFirstLayerOrLastNonUselessLayerCanBeFirst = i != 0 ||
                    (lastNonUselessLayer < fxLayerLayers.Length && fxLayerLayers[lastNonUselessLayer].avatarMask == layer.avatarMask
                        && fxLayerLayers[lastNonUselessLayer].defaultWeight == 1 && !isAffectedByLayerWeightControl.Contains(lastNonUselessLayer));
                var stateMachine = layer.stateMachine;
                if (stateMachine == null || (stateMachine.stateMachines.Length == 0 && stateMachine.states.Length == 0))
                {
                    if (isNotFirstLayerOrLastNonUselessLayerCanBeFirst)
                    {
                        uselessLayers.Add(i);
                        context.Log($"[FXLayer] Layer {i} ('{layer.name}') is useless: Empty state machine.");
                    }
                    continue;
                }
                if (i == 0 || stateMachine.EnumerateAllBehaviours().Any())
                {
                    lastNonUselessLayer = i;
                    continue;
                }
                if (layer.defaultWeight == 0 && !isAffectedByLayerWeightControl.Contains(i))
                {
                    uselessLayers.Add(i);
                    context.Log($"[FXLayer] Layer {i} ('{layer.name}') is useless: Weight 0 and no layer weight control.");
                    continue;
                }
                var clips = stateMachine.EnumerateAllStates()
                    .SelectMany(s => s.motion == null ? new AnimationClip[0] : s.motion.EnumerateAllClips()).Distinct().ToList();
                var layerBindings = clips.SelectMany(c => AnimationUtility.GetCurveBindings(c).Concat(AnimationUtility.GetObjectReferenceCurveBindings(c))).Distinct();
                if (layerBindings.All(b => IsMaterialSwapBinding(b) ? ShouldRemoveMaterialSwapBinding(b) : !IsPossibleBinding(b)))
                {
                    uselessLayers.Add(i);
                    context.Log($"[FXLayer] Layer {i} ('{layer.name}') is useless: All bindings are invalid or removed material swaps.");
                    continue;
                }
                lastNonUselessLayer = i;
            }
            for (int i = 0; i < fxLayerLayers.Length; i++)
            {
                if (fxLayerLayers[i].syncedLayerIndex != -1)
                {
                    uselessLayers.Remove(fxLayerLayers[i].syncedLayerIndex);
                }
            }
            Profiler.EndSection();
            return cache_FindUselessFXLayers = uselessLayers;
        }

        public HashSet<AnimationClip> GetAllUsedFXLayerAnimationClips()
        {
            if (cache_GetAllUsedFXLayerAnimationClips != null) {
                return cache_GetAllUsedFXLayerAnimationClips;
            }
            var fxLayer = GetFXLayer();
            if (fxLayer == null) {
                return new HashSet<AnimationClip>();
            }
            var unusedLayers = FindUselessFXLayers();
            var usedClips = new HashSet<AnimationClip>();
            var fxLayerLayers = GetFXLayerLayers();
            for (int i = 0; i < fxLayerLayers.Length; i++)
            {
                usedClips.UnionWith(fxLayerLayers[i].EnumerateAllMotionOverrides().Select(p => p.motion as AnimationClip).Where(c => c != null));
                var stateMachine = fxLayerLayers[i].stateMachine;
                if (stateMachine == null || unusedLayers.Contains(i))
                    continue;
                foreach (var state in stateMachine.EnumerateAllStates())
                {
                    if (state.motion == null)
                        continue;
                    usedClips.UnionWith(state.motion.EnumerateAllClips());
                }
            }
            return cache_GetAllUsedFXLayerAnimationClips = usedClips;
        }

        public bool DoesFXLayerUseWriteDefaults()
        {
            if (cache_DoesFXLayerUseWriteDefaults != null) {
                return cache_DoesFXLayerUseWriteDefaults.Value;
            }
            var fxLayer = GetFXLayer();
            if (fxLayer == null) {
                return false;
            }
            var fxLayerLayers = GetFXLayerLayers();
            for (int i = 0; i < fxLayerLayers.Length; i++)
            {
                var stateMachine = fxLayerLayers[i].stateMachine;
                if (stateMachine == null || IsMergeableFXLayer(i))
                    continue;
                foreach (var state in stateMachine.EnumerateAllStates())
                {
                    if (state.motion is BlendTree blendTree && blendTree.blendType == BlendTreeType.Direct)
                        continue;
                    if (state.writeDefaultValues)
                    {
                        cache_DoesFXLayerUseWriteDefaults = true;
                        return true;
                    }
                }
            }
            cache_DoesFXLayerUseWriteDefaults = false;
            return false;
        }

        public HashSet<EditorCurveBinding> GetAllUsedFXLayerCurveBindings()
        {
            if (cache_GetAllUsedFXLayerCurveBindings != null) {
                return cache_GetAllUsedFXLayerCurveBindings;
            }
            var result = new HashSet<EditorCurveBinding>();
            foreach (var clip in GetAllUsedFXLayerAnimationClips())
            {
                result.UnionWith(AnimationUtility.GetCurveBindings(clip));
                result.UnionWith(AnimationUtility.GetObjectReferenceCurveBindings(clip));
            }
            return cache_GetAllUsedFXLayerCurveBindings = result;
        }

        public HashSet<string> FindAllGameObjectTogglePaths()
        {
            if (cache_FindAllGameObjectTogglePaths != null) {
                return cache_FindAllGameObjectTogglePaths;
            }
            var togglePaths = new HashSet<string>();
            var fxLayer = GetFXLayer();
            if (fxLayer == null) {
                return togglePaths;
            }
            foreach (var binding in GetAllUsedFXLayerCurveBindings())
            {
                if (binding.type == typeof(GameObject) && binding.propertyName == "m_IsActive") {
                    togglePaths.Add(binding.path);
                }
            }
            return cache_FindAllGameObjectTogglePaths = togglePaths;
        }

        public HashSet<string> FindAllRendererTogglePaths()
        {
            if (cache_FindAllRendererTogglePaths != null) {
                return cache_FindAllRendererTogglePaths;
            }
            var togglePaths = new HashSet<string>();
            foreach (var binding in GetAllUsedFXLayerCurveBindings())
            {
                if (typeof(Renderer).IsAssignableFrom(binding.type) && binding.propertyName == "m_Enabled") {
                    togglePaths.Add(binding.path);
                }
            }
            togglePaths.UnionWith(FindAllGameObjectTogglePaths());
            return cache_FindAllRendererTogglePaths = togglePaths;
        }

        public Dictionary<string, HashSet<string>> FindAllAnimatedMaterialProperties() {
            if (cache_FindAllAnimatedMaterialProperties != null) {
                return cache_FindAllAnimatedMaterialProperties;
            }
            var map = new Dictionary<string, HashSet<string>>();
            var fxLayer = GetFXLayer();
            if (fxLayer == null) {
                return map;
            }
            foreach (var binding in GetAllUsedFXLayerCurveBindings()) {
                if (!binding.propertyName.StartsWithSimple("material.") || !typeof(Renderer).IsAssignableFrom(binding.type))
                    continue;
                if (!map.TryGetValue(binding.path, out var props)) {
                    map[binding.path] = (props = new HashSet<string>());
                }
                var propName = binding.propertyName.Substring(9);
                if (!Regex.IsMatch(propName, @"^[#_a-zA-Z][#_a-zA-Z0-9]*(\.[rgbaxyzw])?$"))
                    continue;
                if (propName.Length > 2 && propName[propName.Length - 2] == '.') {
                    props.Add(propName.Substring(0, propName.Length - 2));
                }
                props.Add(propName);
            }
            return cache_FindAllAnimatedMaterialProperties = map;
        }

        private HashSet<AnimationClip> cache_GetAllUsedAnimationClips = null;
        public HashSet<AnimationClip> GetAllUsedAnimationClips()
        {
            if (cache_GetAllUsedAnimationClips != null) {
                return cache_GetAllUsedAnimationClips;
            }
            var usedClips = new HashSet<AnimationClip>();
            var avDescriptor = gameObject.GetComponent<VRCAvatarDescriptor>();
            if (avDescriptor == null) {
                return usedClips;
            }
            var fxLayer = GetFXLayer();
            foreach (var layer in avDescriptor.baseAnimationLayers)
            {
                var controller = layer.animatorController as AnimatorController;
                if (controller == null || controller == fxLayer)
                    continue;
                usedClips.UnionWith(controller.animationClips);
            }
            foreach (var layer in avDescriptor.specialAnimationLayers)
            {
                var controller = layer.animatorController as AnimatorController;
                if (controller == null)
                    continue;
                usedClips.UnionWith(controller.animationClips);
            }
            usedClips.UnionWith(GetAllUsedFXLayerAnimationClips());
            return cache_GetAllUsedAnimationClips = usedClips;
        }

        public Dictionary<(string path, int index), HashSet<Material>> FindAllMaterialSwapMaterials()
        {
            if (cache_FindAllMaterialSwapMaterials != null) {
                return cache_FindAllMaterialSwapMaterials;
            }
            var result = new Dictionary<(string path, int index), HashSet<Material>>();
            foreach (var clip in GetAllUsedAnimationClips())
            {
                foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                {
                    if (!IsMaterialSwapBinding(binding))
                        continue;
                    int start = binding.propertyName.IndexOf('[') + 1;
                    int end = binding.propertyName.IndexOf(']') - start;
                    int slot = int.Parse(binding.propertyName.Substring(start, end));
                    if (ShouldRemoveMaterialSwapBinding(binding))
                        continue;
                    var index = (binding.path, slot);
                    var curve = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                    var curveMaterials = curve.Select(c => c.value as Material).Where(m => m != null).Distinct().ToList();
                    if (!result.TryGetValue(index, out var materials))
                    {
                        result[index] = materials = new HashSet<Material>();
                    }
                    materials.UnionWith(curveMaterials);
                }
            }
            return cache_FindAllMaterialSwapMaterials = result;
        }

        public HashSet<EditorCurveBinding> GetAllMaterialSwapBindingsToRemove()
        {
            if (cache_GetAllMaterialSwapBindingsToRemove != null) {
                return cache_GetAllMaterialSwapBindingsToRemove;
            }
            var result = new HashSet<EditorCurveBinding>();
            if (cache_MaterialSwapBindingsToRemove != null)
            {
                foreach (var entry in cache_MaterialSwapBindingsToRemove)
                {
                    if (entry.Value)
                        result.Add(entry.Key);
                }
            }
            return cache_GetAllMaterialSwapBindingsToRemove = result;
        }

        private bool IsMaterialSwapBinding(EditorCurveBinding binding)
        {
            var result = typeof(Renderer).IsAssignableFrom(binding.type)
                && binding.propertyName.StartsWithSimple("m_Materials.Array.data[");
            return result;
        }

        private bool ShouldRemoveMaterialSwapBinding(EditorCurveBinding binding)
        {
            if (cache_MaterialSwapBindingsToRemove == null)
                cache_MaterialSwapBindingsToRemove = new Dictionary<EditorCurveBinding, bool>();
            if (cache_MaterialSwapBindingsToRemove.TryGetValue(binding, out var result)) {
                return result;
            }
            int slot = int.Parse(binding.propertyName.Substring("m_Materials.Array.data[".Length, binding.propertyName.Length - "m_Materials.Array.data[]".Length));
            var renderer = gameObject.transform.GetTransformFromPath(binding.path)?.GetComponent<Renderer>();
            result = renderer == null || renderer.sharedMaterials.Length <= slot;
            cache_MaterialSwapBindingsToRemove[binding] = result;
            return result;
        }
    }
}