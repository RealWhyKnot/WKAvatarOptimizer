#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using WKVRCOptimizer.Data;
using WKVRCOptimizer.Extensions;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Contact.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;
using VRC.SDK3.Avatars.Components;
using UnityEditor.Animations;
using Object = UnityEngine.Object;
using WKVRCOptimizer.Core.Util;

namespace WKVRCOptimizer.Core
{
    public class ComponentOptimizer
    {
        private readonly OptimizationContext context;
        private readonly Settings settings;
        private readonly GameObject gameObject; // Renamed from root to gameObject to match Main.cs style or stick to root? Main uses gameObject.
        private readonly AvatarOptimizer optimizer;

        // Caches
        private HashSet<Component> cache_FindAllUnusedComponents = null;
        private HashSet<Transform> cache_FindAllMovingTransforms = null;
        private HashSet<Transform> cache_FindAllUnmovingTransforms = null;
        private HashSet<Renderer> cache_FindAllPenetrators = null;
        private Dictionary<VRCPhysBoneBase, HashSet<Object>> cache_FindAllPhysBoneDependencies = null;
        private HashSet<Transform> cache_FindAllAlwaysDisabledGameObjects = null;
        private HashSet<Transform> cache_GetAllExcludedTransforms = null;
        private HashSet<string> cache_GetAllExcludedTransformPaths = null;

        public ComponentOptimizer(OptimizationContext context, Settings settings, GameObject gameObject, AvatarOptimizer optimizer)
        {
            this.context = context;
            this.settings = settings;
            this.gameObject = gameObject;
            this.optimizer = optimizer;
        }

        public void DestroyEditorOnlyGameObjects()
        {
            Debug.Log("[ComponentOptimizer] Destroying EditorOnly GameObjects...");
            var stack = new Stack<Transform>();
            stack.Push(gameObject.transform);
            while (stack.Count > 0)
            {
                var current = stack.Pop();
                if (current.gameObject.CompareTag("EditorOnly"))
                {
                    Debug.Log($"[ComponentOptimizer] Destroying EditorOnly object: {current.name}");
                    Object.DestroyImmediate(current.gameObject);
                    continue;
                }
                foreach (Transform child in current)
                {
                    stack.Push(child);
                }
            }
        }

        public void DestroyUnusedComponents()
        {
            if (!settings.DeleteUnusedComponents)
                return;
            
            Debug.Log("[ComponentOptimizer] Destroying unused components...");
            var list = FindAllUnusedComponents();
            foreach (var component in list)
            {
                if (component == null)
                    continue;
                Debug.Log($"[ComponentOptimizer] Destroying unused component: {component.GetType().Name} on {component.name}");
                if (component is AudioSource audio)
                {
                    var vrcAudioSource = audio.GetComponent<VRCSpatialAudioSource>();
                    if (vrcAudioSource != null)
                    {
                        Object.DestroyImmediate(vrcAudioSource);
                    }
                }
                Object.DestroyImmediate(component);
            }
        }

        public void DestroyUnusedGameObjects()
        {
            if (settings.DeleteUnusedGameObjects == 0)
                return;

            Debug.Log("[ComponentOptimizer] Destroying unused GameObjects...");
            var used = new HashSet<Transform>();

            var movingTransforms = FindAllMovingTransforms();
            used.UnionWith(movingTransforms);
            used.UnionWith(movingTransforms.Select(t => t != null ? t.parent : null));

            used.Add(gameObject.transform);
            used.UnionWith(gameObject.GetComponentsInChildren<Animator>(true)
                .Select(a => a.transform.Find("Armature")).Where(t => t != null));
            used.UnionWith(gameObject.transform.Cast<Transform>().Where(t => t.name.StartsWithSimple("NaNimation ")));

            foreach (var contact in gameObject.GetComponentsInChildren<ContactBase>(true))
            {
                used.Add(contact.GetRootTransform());
                used.Add(contact.GetRootTransform().parent);
            }

            foreach (var physBone in gameObject.GetComponentsInChildren<VRCPhysBoneBase>(true))
            {
                used.Add(physBone.GetRootTransform());
                used.Add(physBone.GetRootTransform().parent);
                used.UnionWith(physBone.ignoreTransforms);
            }

            foreach (var collider in gameObject.GetComponentsInChildren<VRCPhysBoneColliderBase>(true))
            {
                used.Add(collider.GetRootTransform());
                used.Add(collider.GetRootTransform().parent);
            }

            foreach (var c in gameObject.GetComponentsInChildren<Component>(true).Where(c => c != null && !(c is Transform)))
            {
                used.Add(c.transform);
                if (c.GetType().Name.Contains("Constraint"))
                {
                    used.Add(c.transform.parent);
                }
                used.UnionWith(FindReferencedTransforms(c));
            }

            // the vrc finger colliders depend on their relative position to their parent, so we need to keep their parents around too
            var avDescriptor = gameObject.GetComponent<VRCAvatarDescriptor>();
            var fingerColliders = new List<VRCAvatarDescriptor.ColliderConfig>() {
                avDescriptor.collider_fingerIndexL,
                avDescriptor.collider_fingerIndexR,
                avDescriptor.collider_fingerMiddleL,
                avDescriptor.collider_fingerMiddleR,
                avDescriptor.collider_fingerRingL,
                avDescriptor.collider_fingerRingR,
                avDescriptor.collider_fingerLittleL,
                avDescriptor.collider_fingerLittleR,
            }.Select(c => c.transform).Where(t => t != null);
            used.UnionWith(fingerColliders.Select(c => c.parent).Where(t => t != null));

            used.UnionWith(optimizer.FindAllGameObjectTogglePaths().Select(p => gameObject.transform.GetTransformFromPath(p)).Where(t => t != null));

            foreach (var exclusion in GetAllExcludedTransforms())
            {
                if (exclusion == null)
                    continue;
                used.Add(exclusion);
                var current = exclusion;
                while ((current = current.parent) != null)
                {
                    used.Add(current);
                }
            }

            var queue = new Queue<Transform>();
            queue.Enqueue(gameObject.transform);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (Transform child in current)
                {
                    queue.Enqueue(child);
                }
                if (!used.Contains(current))
                {
                    Debug.Log($"[ComponentOptimizer] Destroying unused GameObject: {current.name}");
                    foreach (var child in current.Cast<Transform>().ToArray())
                    {
                        child.parent = current.parent;
                        child.name = $"{current.name}_{child.name}";
                    }
                    Object.DestroyImmediate(current.gameObject);
                }
            }
        }

        public HashSet<Transform> GetAllExcludedTransforms() {
            if (cache_GetAllExcludedTransforms != null)
                return cache_GetAllExcludedTransforms;
            var allExcludedTransforms = new HashSet<Transform>();
            var hardCodedExclusions = new List<string>() {
                "_VirtualLens_Root",
            }.Select(s => gameObject.transform.GetTransformFromPath(s)).ToList();
            hardCodedExclusions.AddRange(gameObject.transform.GetComponentsInChildren<VRCContactSender>(true)
                .Where(c => c.collisionTags.Any(t => t == "superneko.realkiss.contact.mouth"))
                .Select(c => c.transform.parent)
                .Where(t => t != null)
                .Select(t => t.Cast<Transform>().FirstOrDefault(child => child.TryGetComponent(out SkinnedMeshRenderer _)))
                .Where(t => t != null));
            hardCodedExclusions.AddRange(FindAllPenetrators().Select(p => p.transform));
            foreach (var excludedTransform in optimizer.ExcludeTransforms.Concat(hardCodedExclusions)) {
                if (excludedTransform == null)
                    continue;
                allExcludedTransforms.Add(excludedTransform);
                allExcludedTransforms.UnionWith(excludedTransform.GetAllDescendants());
            }
            return cache_GetAllExcludedTransforms = allExcludedTransforms;
        }

        public HashSet<string> GetAllExcludedTransformPaths() {
            if (cache_GetAllExcludedTransformPaths != null)
                return cache_GetAllExcludedTransformPaths;
            return cache_GetAllExcludedTransformPaths = new HashSet<string>(GetAllExcludedTransforms().Select(t => t.GetPathToRoot(gameObject.transform)));
        }

        public HashSet<Component> FindAllUnusedComponents()
        {
            if (cache_FindAllUnusedComponents != null)
                return cache_FindAllUnusedComponents;
            var fxLayer = optimizer.GetFXLayer();
            if (fxLayer == null)
                return new HashSet<Component>();
            
            Debug.Log("[ComponentOptimizer] Finding unused components...");
            var behaviourToggles = new HashSet<string>();
            foreach (var binding in optimizer.fxLayerOptimizer.GetAllUsedFXLayerCurveBindings()) {
                if (typeof(Behaviour).IsAssignableFrom(binding.type) && binding.propertyName == "m_Enabled") {
                    behaviourToggles.Add(binding.path);
                } else if (typeof(Renderer).IsAssignableFrom(binding.type) && binding.propertyName == "m_Enabled") {
                    behaviourToggles.Add(binding.path);
                }
            }

            var alwaysDisabledBehaviours = new HashSet<Component>(gameObject.GetComponentsInChildren<Behaviour>(true)
                .Where(b => b != null && !b.enabled)
                .Where(b => !(b is VRCPhysBoneColliderBase))
                .Where(b => !behaviourToggles.Contains(b.transform.GetPathToRoot(gameObject.transform))));

            alwaysDisabledBehaviours.UnionWith(gameObject.GetComponentsInChildren<Renderer>(true)
                .Where(r => r != null && !r.enabled && !(r is ParticleSystemRenderer))
                .Where(r => !behaviourToggles.Contains(r.transform.GetPathToRoot(gameObject.transform))));

            alwaysDisabledBehaviours.UnionWith(FindAllAlwaysDisabledGameObjects()
                .SelectMany(t => t.GetNonNullComponents().Where(c => !(c is Transform))));
            
            var exclusions = GetAllExcludedTransforms();

            foreach(var entry in FindAllPhysBoneDependencies())
            {
                if (exclusions.Contains(entry.Key.transform))
                    continue;
                var dependencies = entry.Value.Select(d => d as Component).Where(d => d != null);
                if (!entry.Value.Any(d => d is AnimatorController) && dependencies.All(d => alwaysDisabledBehaviours.Contains(d)))
                {
                    alwaysDisabledBehaviours.Add(entry.Key);
                }
            }

            var usedPhysBoneColliders = gameObject.GetComponentsInChildren<VRCPhysBoneBase>(true)
                .Where(pb => !alwaysDisabledBehaviours.Contains(pb) || exclusions.Contains(pb.transform))
                .SelectMany(pb => pb.colliders);

            alwaysDisabledBehaviours.UnionWith(gameObject.GetComponentsInChildren<VRCPhysBoneColliderBase>(true)
                .Where(c => !usedPhysBoneColliders.Contains(c)));

            alwaysDisabledBehaviours.RemoveWhere(c => exclusions.Contains(c.transform) || c.transform == gameObject.transform);

            Debug.Log($"[ComponentOptimizer] Found {alwaysDisabledBehaviours.Count} unused components.");
            return cache_FindAllUnusedComponents = alwaysDisabledBehaviours;
        }

        public HashSet<Transform> FindAllAlwaysDisabledGameObjects()
        {
            if (cache_FindAllAlwaysDisabledGameObjects != null)
                return cache_FindAllAlwaysDisabledGameObjects;
            
            Debug.Log("[ComponentOptimizer] Finding always disabled game objects...");
            var togglePaths = optimizer.FindAllGameObjectTogglePaths();
            var disabledGameObjects = new HashSet<Transform>();
            var queue = new Queue<Transform>();
            var exclusions = GetAllExcludedTransforms();
            queue.Enqueue(gameObject.transform);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (exclusions.Contains(current))
                    continue;
                if (current != gameObject.transform && !current.gameObject.activeSelf && !togglePaths.Contains(current.GetPathToRoot(gameObject.transform)))
                {
                    disabledGameObjects.Add(current);
                    foreach (var child in current.GetAllDescendants())
                    {
                        disabledGameObjects.Add(child);
                    }
                }
                else
                {
                    foreach (Transform child in current)
                    {
                        queue.Enqueue(child);
                    }
                }
            }
            Debug.Log($"[ComponentOptimizer] Found {disabledGameObjects.Count} always disabled game objects.");
            return cache_FindAllAlwaysDisabledGameObjects = disabledGameObjects;
        }
        
        public Dictionary<VRCPhysBoneBase, HashSet<Object>> FindAllPhysBoneDependencies()
        {
            if (cache_FindAllPhysBoneDependencies != null)
                return cache_FindAllPhysBoneDependencies;
            
            Debug.Log("[ComponentOptimizer] Finding PhysBone dependencies...");
            var result = new Dictionary<VRCPhysBoneBase, HashSet<Object>>();
            var physBonePath = new Dictionary<string, VRCPhysBoneBase>();
            var physBones = gameObject.GetComponentsInChildren<VRCPhysBoneBase>(true);
            foreach (var physBone in physBones)
            {
                result.Add(physBone, new HashSet<Object>());
                physBonePath[physBone.transform.GetPathToRoot(gameObject.transform)] = physBone;
            }
            var parameterSuffixes = new string[] { "_IsGrabbed", "_IsPosed", "_Angle", "_Stretch", "_Squish" };
            var avDescriptor = gameObject.GetComponent<VRCAvatarDescriptor>();
            if (avDescriptor != null)
            {
                foreach (var controller in avDescriptor.baseAnimationLayers.Select(l => l.animatorController as AnimatorController).Where(c => c != null))
                {
                    var parameterNames = new HashSet<string>(controller.parameters.Select(p => p.name));
                    foreach (var physBone in physBones)
                    {
                        if (parameterSuffixes.Any(s => parameterNames.Contains(physBone.parameter + s)))
                        {
                            result[physBone].Add(controller);
                        }
                    }
                }
            }
            foreach (var clip in optimizer.fxLayerOptimizer.GetAllUsedAnimationClips())
            {
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                {
                    if (binding.propertyName == "m_Enabled" && typeof(VRCPhysBoneBase).IsAssignableFrom(binding.type) && physBonePath.TryGetValue(binding.path, out var physBone))
                    {
                        result[physBone].Add(clip);
                    }
                }
            }
            var transformToDependency = new Dictionary<Transform, HashSet<Object>>();
            void AddDependency(Transform t, Object obj)
            {
                if (t == null)
                    return;
                if (!transformToDependency.TryGetValue(t, out var dependencies))
                {
                    transformToDependency[t] = dependencies = new HashSet<Object>();
                }
                dependencies.Add(obj);
            }
            foreach (var skinnedMesh in gameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skinnedMesh.bones.Length == 0)
                {
                    AddDependency(skinnedMesh.rootBone, skinnedMesh);
                    continue;
                }
                if (skinnedMesh.sharedMesh == null)
                    continue;
                var meshBones = skinnedMesh.bones;
                var usedBoneIDs = new bool[meshBones.Length];
                var boneWeights = skinnedMesh.sharedMesh.boneWeights;
                int outOfRangeBoneCount = 0;
                void MarkUsedBone(int boneIndex) {
                    if (boneIndex < 0 || boneIndex >= meshBones.Length) {
                        outOfRangeBoneCount++;
                        return;
                    }
                    usedBoneIDs[boneIndex] = true;
                }
                for (int i = 0; i < boneWeights.Length; i++)
                {
                    MarkUsedBone(boneWeights[i].boneIndex0);
                    if (boneWeights[i].weight1 > 0) {
                        MarkUsedBone(boneWeights[i].boneIndex1);
                        if (boneWeights[i].weight2 > 0) {
                            MarkUsedBone(boneWeights[i].boneIndex2);
                            if (boneWeights[i].weight3 > 0) {
                                MarkUsedBone(boneWeights[i].boneIndex3);
                            }
                        }
                    }
                }
                for (int i = 0; i < usedBoneIDs.Length; i++)
                {
                    if (usedBoneIDs[i])
                    {
                        AddDependency(meshBones[i], skinnedMesh);
                    }
                }
                if (outOfRangeBoneCount > 0)
                {
                    Debug.LogWarning($"Skinned mesh renderer {skinnedMesh.transform.GetPathToRoot(gameObject.transform)} has {outOfRangeBoneCount} out of range bone indices");
                }
            }
            foreach (var behavior in gameObject.GetComponentsInChildren<Behaviour>(true)
                .Where(b => b != null && (b.GetType().Name.Contains("Constraint") || b.GetType().FullName.StartsWithSimple("RootMotion.FinalIK"))))
            {
                foreach (var t in FindReferencedTransforms(behavior))
                {
                    AddDependency(t, behavior);
                }
            }
            foreach (var skinnedRenderer in gameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                AddDependency(skinnedRenderer.rootBone, skinnedRenderer);
            }
            foreach (var renderer in gameObject.GetComponentsInChildren<Renderer>(true))
            {
                AddDependency(renderer.probeAnchor, renderer);
                AddDependency(renderer.transform, renderer);
            }
            foreach (var contact in gameObject.GetComponentsInChildren<ContactBase>(true))
            {
                AddDependency(contact.GetRootTransform(), contact);
            }

            var componentTypesToIgnore = new HashSet<string>() {
                "UnityEngine.Transform",
                "nadena.dev.ndmf.multiplatform.components.PortableDynamicBone",
                "nadena.dev.ndmf.multiplatform.components.PortableDynamicBoneCollider",
            };

            foreach (var physBone in physBones)
            {
                var root = physBone.GetRootTransform();
                foreach (Transform current in root.GetAllDescendants().Concat(new Transform[] { root }))
                {
                    if (transformToDependency.TryGetValue(current, out var dependencies))
                    {
                        result[physBone].UnionWith(dependencies);
                    }
                    result[physBone].UnionWith(current.GetNonNullComponents()
                        .Where(c => c != physBone && !componentTypesToIgnore.Contains(c.GetType().FullName)));
                }
            }

            return cache_FindAllPhysBoneDependencies = result;
        }

        public Dictionary<string, List<string>> FindAllPhysBonesToDisable()
        {
            var result = new Dictionary<string, List<string>>();
            if (!settings.DisablePhysBonesWhenUnused)
                return result;
            Debug.Log("[ComponentOptimizer] Finding PhysBones to disable...");
            var physBoneDependencies = FindAllPhysBoneDependencies();
            foreach (var dependencies in physBoneDependencies.Values)
            {
                dependencies.RemoveWhere(o => o == null);
            }
            foreach (var entry in physBoneDependencies)
            {
                if (entry.Key != null && entry.Value.Count(o => !(o is AnimatorController)) == 1 && entry.Value.First(o => !(o is AnimatorController)) is SkinnedMeshRenderer target)
                {
                    var targetPath = target.transform.GetPathToRoot(gameObject.transform);
                    if (!result.TryGetValue(targetPath, out var physBones))
                    {
                        result[targetPath] = physBones = new List<string>();
                    }
                    physBones.Add(entry.Key.transform.GetPathToRoot(gameObject.transform));
                    Debug.Log($"[ComponentOptimizer] Disabling PhysBone {entry.Key.name} when {target.name} is unused.");
                }
            }
            return result;
        }

        private HashSet<Transform> FindAllMovingTransforms()
        {
            if (cache_FindAllMovingTransforms != null)
                return cache_FindAllMovingTransforms;
            
            Debug.Log("[ComponentOptimizer] Finding moving transforms...");
            var avDescriptor = gameObject.GetComponent<VRCAvatarDescriptor>();
            if (avDescriptor == null)
                return new HashSet<Transform>();
            var transforms = new HashSet<Transform>();

            if (avDescriptor.enableEyeLook)
            {
                transforms.Add(avDescriptor.customEyeLookSettings.leftEye);
                transforms.Add(avDescriptor.customEyeLookSettings.rightEye);
            }
            if (avDescriptor.customEyeLookSettings.eyelidType == VRCAvatarDescriptor.EyelidType.Bones)
            {
                transforms.Add(avDescriptor.customEyeLookSettings.lowerLeftEyelid);
                transforms.Add(avDescriptor.customEyeLookSettings.lowerRightEyelid);
                transforms.Add(avDescriptor.customEyeLookSettings.upperLeftEyelid);
                transforms.Add(avDescriptor.customEyeLookSettings.upperRightEyelid);
            }

            if (avDescriptor.lipSync == VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.JawFlapBone)
            {
                transforms.Add(avDescriptor.lipSyncJawBone);
            }

            foreach (var clip in optimizer.fxLayerOptimizer.GetAllUsedAnimationClips())
            {
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                {
                    if (binding.type == typeof(Transform))
                    {
                        transforms.Add(gameObject.transform.GetTransformFromPath(binding.path));
                    }
                }
            }

            var animators = gameObject.GetComponentsInChildren<Animator>(true);
            foreach (var animator in animators)
            {
                if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
                    continue;
                foreach (var boneId in System.Enum.GetValues(typeof(HumanBodyBones)).Cast<HumanBodyBones>())
                {
                    if (boneId < 0 || boneId >= HumanBodyBones.LastBone)
                        continue;
                    transforms.Add(animator.GetBoneTransform(boneId));
                }
            }

            var alwaysDisabledComponents = FindAllUnusedComponents();
            var physBones = gameObject.GetComponentsInChildren<VRCPhysBoneBase>(true)
                .Where(pb => !alwaysDisabledComponents.Contains(pb)).ToList();
            foreach (var physBone in physBones)
            {
                var root = physBone.GetRootTransform();
                var exclusions = new HashSet<Transform>(physBone.ignoreTransforms);
                var stack = new Stack<Transform>();
                stack.Push(root);
                while (stack.Count > 0)
                {
                    var current = stack.Pop();
                    transforms.Add(current);
                    if (exclusions.Contains(current))
                        continue;
                    foreach (Transform child in current)
                    {
                        stack.Push(child);
                    }
                }
            }

            var constraints = gameObject.GetComponentsInChildren<Behaviour>(true)
                .Where(b => b != null && !alwaysDisabledComponents.Contains(b))
                .Where(b => b.GetType().Name.Contains("Constraint")).ToList();
            foreach (var constraint in constraints)
            {
                transforms.Add(constraint.transform);
                if (constraint.GetType().Name.StartsWithSimple("VRC"))
                {
                    using (var so = new SerializedObject(constraint))
                    {
                        var targetTransformProperty = so.FindProperty("TargetTransform");
                        if (targetTransformProperty != null)
                        {
                            var targetTransform = targetTransformProperty.objectReferenceValue as Transform;
                            if (targetTransform != null)
                            {
                                transforms.Add(targetTransform);
                            }
                        }
                    }
                }
            }

            var finalIKScripts = gameObject.GetComponentsInChildren<Behaviour>(true)
                .Where(b => b != null && !alwaysDisabledComponents.Contains(b))
                .Where(b => b.GetType().FullName.StartsWithSimple("RootMotion.FinalIK")).ToList();
            foreach (var finalIKScript in finalIKScripts)
            {
                transforms.UnionWith(FindReferencedTransforms(finalIKScript));
            }

            var headChopType = Type.GetType("VRC.SDK3.Avatars.Components.VRCHeadChop, VRCSDK3A");
            if (headChopType != null) {
                foreach (var headChop in gameObject.GetComponentsInChildren(headChopType, true)) {
                    using (var so = new SerializedObject(headChop)) {
                        var targetBonesProperty = so.FindProperty("targetBones");
                        for (int i = 0; i < targetBonesProperty.arraySize; i++) {
                            var targetBone = targetBonesProperty.GetArrayElementAtIndex(i).FindPropertyRelative("transform").objectReferenceValue as Transform;
                            if (targetBone != null) {
                                transforms.Add(targetBone);
                            }
                        }
                    }
                }
            }

            transforms.UnionWith(gameObject.transform.GetAllDescendants().Where(t => t.localScale != Vector3.one));

            Debug.Log($"[ComponentOptimizer] Found {transforms.Count} moving transforms.");
            return cache_FindAllMovingTransforms = transforms;
        }

        public HashSet<Transform> FindAllUnmovingTransforms()
        {
            if (cache_FindAllUnmovingTransforms != null)
                return cache_FindAllUnmovingTransforms;
            var avDescriptor = gameObject.GetComponent<VRCAvatarDescriptor>();
            if (avDescriptor == null)
                return new HashSet<Transform>();
            var moving = FindAllMovingTransforms();
            return cache_FindAllUnmovingTransforms = new HashSet<Transform>(gameObject.transform.GetAllDescendants().Where(t => !moving.Contains(t)));
        }

        public Dictionary<Transform, Transform> FindMovingParent()
        {
            var nonMovingTransforms = FindAllUnmovingTransforms();
            var result = new Dictionary<Transform, Transform>();
            foreach (var transform in gameObject.transform.GetAllDescendants())
            {
                var movingParent = transform;
                while (nonMovingTransforms.Contains(movingParent))
                {
                    movingParent = movingParent.parent;
                }
                result[transform] = movingParent;
            }
            return result;
        }

        private bool IsDPSPenetratorTipLight(Light light)
        {
            return light.type == LightType.Point && light.renderMode == LightRenderMode.ForceVertex
                && light.color.r < 0.01 && light.color.g < 0.01 && light.color.b < 0.01
                && light.range % 0.1 - 0.09 < 0.001;
        }

        private bool IsDPSPenetratorRoot(Transform t)
        {
            if (t == null)
                return false;
            if (t.GetComponentsInChildren<Light>(true).Count(IsDPSPenetratorTipLight) != 1)
                return false;
            var renderers = t.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length != 1)
                return false;
            if (renderers[0].sharedMaterials.Length == 0)
                return false;
            var material = renderers[0].sharedMaterials[0];
            if (material == null)
                return false;
            return material.HasProperty("_Length");
        }

        private bool IsTPSPenetratorRoot(Transform t)
        {
            if (t == null)
                return false;
            var renderers = t.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length != 1)
                return false;
            if (renderers[0].sharedMaterials.Length == 0)
                return false;
            var material = renderers[0].sharedMaterials[0];
            if (material == null)
                return false;
            return material.HasProperty("_TPSPenetratorEnabled") && material.GetFloat("_TPSPenetratorEnabled") > 0.5f;
        }

        public bool IsSPSPenetratorRoot(Transform t) {
            if(t == null)
                return false;
            var renderers = t.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length != 1)
                return false;
            if(renderers[0].sharedMaterials.Length == 0)
                return false;
            var material = renderers[0].sharedMaterials[0];
            if(material == null)
                return false;
            return material.HasProperty("_SPS_Length");
        }

        public HashSet<Renderer> FindAllPenetrators()
        {
            if (cache_FindAllPenetrators != null)
                return cache_FindAllPenetrators;
            
            Debug.Log("[ComponentOptimizer] Finding penetrators...");
            var penetratorTipLights = gameObject.GetComponentsInChildren<Light>(true)
                .Where(l => IsDPSPenetratorTipLight(l)).ToList();
            var penetrators = new HashSet<Renderer>();
            foreach (var light in penetratorTipLights)
            {
                var candidate = light.transform;
                while (candidate != null && !IsDPSPenetratorRoot(candidate))
                {
                    candidate = candidate.parent;
                }
                if (IsDPSPenetratorRoot(candidate))
                {
                    penetrators.Add(candidate.GetComponentsInChildren<MeshRenderer>(true).First());
                }
            }
            penetrators.UnionWith(gameObject.GetComponentsInChildren<Renderer>(true).Where(m => IsTPSPenetratorRoot(m.transform) || IsSPSPenetratorRoot(m.transform)));
            Debug.Log($"[ComponentOptimizer] Found {penetrators.Count} penetrators.");
            return cache_FindAllPenetrators = penetrators;
        }

        public void MoveRingFingerColliderToFeet()
        {
            if (!settings.UseRingFingerAsFootCollider)
                return;
            Debug.Log("[ComponentOptimizer] Moving Ring Finger Collider to Feet...");
            var avDescriptor = gameObject.GetComponent<VRCAvatarDescriptor>();

            var collider = avDescriptor.collider_footL;
            collider.state = VRCAvatarDescriptor.ColliderConfig.State.Custom;
            collider.height -= collider.radius * 2f;
            var parent = new GameObject("leftFootColliderRoot");
            parent.transform.parent = collider.transform;
            parent.transform.localRotation = collider.rotation;
            parent.transform.localPosition = collider.position + collider.rotation * (-(collider.height * 0.5f) * Vector3.up);
            parent.transform.localScale = Vector3.one;
            var leaf = new GameObject("leftFootColliderLeaf");
            leaf.transform.parent = parent.transform;
            leaf.transform.localPosition = new Vector3(0, collider.height, 0);
            leaf.transform.localRotation = Quaternion.identity;
            leaf.transform.localScale = Vector3.one;
            collider.transform = leaf.transform;
            avDescriptor.collider_fingerRingL = collider;

            collider = avDescriptor.collider_footR;
            collider.state = VRCAvatarDescriptor.ColliderConfig.State.Custom;
            collider.height -= collider.radius * 2f;
            parent = new GameObject("rightFootColliderRoot");
            parent.transform.parent = collider.transform;
            parent.transform.localRotation = collider.rotation;
            parent.transform.localPosition = collider.position + collider.rotation * (-(collider.height * 0.5f) * Vector3.up);
            parent.transform.localScale = Vector3.one;
            leaf = new GameObject("rightFootColliderLeaf");
            leaf.transform.parent = parent.transform;
            leaf.transform.localPosition = new Vector3(0, collider.height, 0);
            leaf.transform.localRotation = Quaternion.identity;
            leaf.transform.localScale = Vector3.one;
            collider.transform = leaf.transform;
            avDescriptor.collider_fingerRingR = collider;

            // disable collider foldout in the inspector because it resets the collider transform
            EditorPrefs.SetBool("VRCSDK3_AvatarDescriptorEditor3_CollidersFoldout", false);
        }

        public List<T> GetNonEditorOnlyComponentsInChildren<T>() where T : Component
        {
            var components = new List<T>();
            var stack = new Stack<Transform>();
            stack.Push(gameObject.transform);
            while (stack.Count > 0)
            {
                var currentTransform = stack.Pop();
                if (currentTransform.gameObject.CompareTag("EditorOnly"))
                    continue;
                components.AddRange(currentTransform.GetComponents<T>());
                foreach (Transform child in currentTransform)
                {
                    stack.Push(child);
                }
            }
            return components;
        }

        public List<T> GetUsedComponentsInChildren<T>() where T : Component
        {
            Profiler.StartSection("GetUsedComponentsInChildren()");
            var result = new List<T>();
            var stack = new Stack<Transform>();
            var alwaysDisabledGameObjects = FindAllAlwaysDisabledGameObjects();
            var unusedComponents = FindAllUnusedComponents();
            if (!settings.DeleteUnusedComponents)
            {
                alwaysDisabledGameObjects = new HashSet<Transform>();
                unusedComponents = new HashSet<Component>();
            }
            stack.Push(gameObject.transform);
            while (stack.Count > 0)
            {
                var currentTransform = stack.Pop();
                if (currentTransform.gameObject.CompareTag("EditorOnly") || alwaysDisabledGameObjects.Contains(currentTransform))
                    continue;
                result.AddRange(currentTransform.GetComponents<T>().Where(c => c != null && !unusedComponents.Contains(c)));
                foreach (Transform child in currentTransform)
                {
                    stack.Push(child);
                }
            }
            Profiler.EndSection();
            return result;
        }

        private HashSet<Transform> FindReferencedTransforms(Component component)
        {
            using (var serializedObject = new SerializedObject(component))
            {
                var visitedIds = new HashSet<long>();
                var iterator = serializedObject.GetIterator();
                var referencedTransforms = new HashSet<Transform>();
                bool enterChildren = true;
                while (iterator.Next(enterChildren))
                {
                    enterChildren = true;
                    if (iterator.propertyType == SerializedPropertyType.ObjectReference && iterator.objectReferenceValue != null)
                    {
                        if (iterator.objectReferenceValue is Transform transform)
                        {
                            referencedTransforms.Add(transform);
                        }
                    }
                    else if (iterator.propertyType == SerializedPropertyType.ManagedReference)
                    {
                        if (!visitedIds.Add(iterator.managedReferenceId))
                        {
                            enterChildren = false;
                        }
                    }
                }
                return referencedTransforms;
            }
        }
    }
}
#endif