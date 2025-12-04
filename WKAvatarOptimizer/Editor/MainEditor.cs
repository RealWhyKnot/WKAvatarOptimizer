using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using WKAvatarOptimizer.Core;
using WKAvatarOptimizer.Core.Util;
using WKAvatarOptimizer.Extensions;
using VRC.SDK3.Avatars.Components;
using VRC.Dynamics;
using VRC.SDKBase.Validation.Performance;

using Type = System.Type;
using MaterialSlot = WKAvatarOptimizer.Data.MaterialSlot;

namespace WKAvatarOptimizer.Editor
{
    [CustomEditor(typeof(AvatarOptimizer))]
    public class AvatarOptimizerEditor : UnityEditor.Editor
    {
        private static AvatarOptimizer optimizer;
        private static Material nullMaterial = null;
        private static long longestTimeUsed = -2;
        private const int AutoRefreshPreviewTimeout = 500;

        private IEnumerable<Texture2D> GetTexturesFromIR(WKAvatarOptimizer.Core.Universal.ShaderIR ir)
        {
            if (ir == null) yield break;
            if (ir.baseColor.texture != null) yield return ir.baseColor.texture;
            if (ir.normalMap.texture != null) yield return ir.normalMap.texture;
            if (ir.metallicGlossMap.texture != null) yield return ir.metallicGlossMap.texture;
            if (ir.shadeMap.texture != null) yield return ir.shadeMap.texture;
            if (ir.rampTexture.texture != null) yield return ir.rampTexture.texture;
            if (ir.matcapTexture.texture != null) yield return ir.matcapTexture.texture;
            if (ir.matcapTexture2.texture != null) yield return ir.matcapTexture2.texture;
            if (ir.outlineMask.texture != null) yield return ir.outlineMask.texture;
            if (ir.emissionMap.texture != null) yield return ir.emissionMap.texture;
            if (ir.dissolveMask.texture != null) yield return ir.dissolveMask.texture;
            if (ir.detailMap.texture != null) yield return ir.detailMap.texture;
        }

        public override void OnInspectorGUI()
        {
            optimizer = (AvatarOptimizer)target;
            optimizer.EnsureInitializedForEditor();
            OnSelectionChange();
            
            var stopWatch = new System.Diagnostics.Stopwatch();
            stopWatch.Start();
            if (nullMaterial == null)
            {
                nullMaterial = new Material(Shader.Find("Hidden/InternalErrorShader"));
                nullMaterial.name = "(null material slot)";
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
            }

            EditorGUILayout.Space();

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.LabelField("<size=20>WhyKnot's Avatar Optimizer</size>", new GUIStyle(EditorStyles.label) { richText = true, alignment = TextAnchor.LowerCenter });
                
                string buildDate = Assembly.GetExecutingAssembly()
                                          .GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)
                                          .OfType<AssemblyMetadataAttribute>()
                                          .FirstOrDefault(x => x.Key == "BuildDate")?.Value;
        
                if (!string.IsNullOrEmpty(buildDate))
                {
                    EditorGUILayout.LabelField("v" + buildDate, EditorStyles.centeredGreyMiniLabel);
                }
                else
                {
                    EditorGUILayout.LabelField("vdev", EditorStyles.centeredGreyMiniLabel);
                }
                EditorGUILayout.Space();
            }

            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Exit play mode to use the optimizer.", MessageType.Info);
                return;
            }

            Profiler.enabled = optimizer.ProfileTimeUsed;
            Profiler.Reset();

            Profiler.StartSection("Validate");
            bool validationResult = Validate();
            Profiler.EndSection();

            GUI.enabled = validationResult;
            GUI.enabled = true;

            EditorGUILayout.HelpBox("Optimizations will happen on build / play mode", MessageType.Info);
            if (longestTimeUsed > AutoRefreshPreviewTimeout)
            {
                EditorGUILayout.HelpBox("Preview auto refresh is disabled because it took " + longestTimeUsed + "ms which is longer than the threshold of " + AutoRefreshPreviewTimeout + "ms to refresh.\n" +
                    "The preview might still be refreshed manually by clicking the refresh button.", MessageType.Info);
                if (GUILayout.Button("Refresh Preview"))
                {
                    longestTimeUsed = 0;
                    ClearUICaches();
                }
            }

            Profiler.StartSection("Show Perf Rank Change");
            var exclusions = optimizer.GetAllExcludedTransforms();
            var particleSystemCount = optimizer.GetNonEditorOnlyComponentsInChildren<ParticleSystem>().Count;
            var trailRendererCount = optimizer.GetNonEditorOnlyComponentsInChildren<TrailRenderer>().Count;
            var skinnedMeshes = optimizer.GetNonEditorOnlyComponentsInChildren<SkinnedMeshRenderer>();
            int meshCount = optimizer.GetNonEditorOnlyComponentsInChildren<MeshRenderer>().Count;
            int totalMaterialCount = optimizer.GetNonEditorOnlyComponentsInChildren<Renderer>()
                .Sum(r => r.GetSharedMesh() == null ? 0 : r.GetSharedMesh().subMeshCount) + particleSystemCount + trailRendererCount;
            var totalBlendShapePaths = new HashSet<string>(skinnedMeshes.SelectMany(r => {
                if (r.sharedMesh == null)
                    return new string[0];
                return Enumerable.Range(0, r.sharedMesh.blendShapeCount)
                    .Select(i => r.transform.GetPathToRoot(optimizer.transform) + "/blendShape." + r.sharedMesh.GetBlendShapeName(i));
            }));
            int optimizedSkinnedMeshCount = 0;
            int optimizedMeshCount = 0;
            int optimizedTotalMaterialCount = 0;
            foreach (var matched in MergedMaterialPreview)
            {
                var renderers = matched.SelectMany(m => m).Select(slot => slot.renderer).Distinct().ToArray();
                if (renderers.Any(r => r is SkinnedMeshRenderer) || renderers.Length > 1)
                {
                    optimizedSkinnedMeshCount++;
                    if (exclusions.Contains(renderers[0].transform))
                        optimizedTotalMaterialCount += renderers[0].GetSharedMesh().subMeshCount;
                    else
                        optimizedTotalMaterialCount += matched.Count;
                }
                else if (renderers[0] is MeshRenderer)
                {
                    optimizedMeshCount++;
                    var mesh = renderers[0].GetSharedMesh();
                    optimizedTotalMaterialCount += mesh == null ? 0 : mesh.subMeshCount;
                }
                else
                {
                    optimizedTotalMaterialCount += 1;
                }
            }
            PerfRankChangeLabel("Skinned Mesh Renderers", skinnedMeshes.Count, optimizedSkinnedMeshCount, PerformanceCategory.SkinnedMeshCount);
            PerfRankChangeLabel("Mesh Renderers", meshCount, optimizedMeshCount, PerformanceCategory.MeshCount);
            PerfRankChangeLabel("Material Slots", totalMaterialCount, optimizedTotalMaterialCount, PerformanceCategory.MaterialCount);
            
            if (optimizer.GetFXLayer() != null)
            {
                var nonErrors = new HashSet<string>() {"toggle", "motion time", "blend tree", "multi toggle"};
                var mergedLayerCount = optimizer.AnalyzeFXLayerMergeAbility().Count(list => list.All(e => nonErrors.Contains(e)));
                var layerCount = optimizer.GetFXLayerLayers().Length;
                var optimizedLayerCount = mergedLayerCount > 1 ? layerCount - mergedLayerCount + 1 : layerCount;
                optimizedLayerCount -= optimizer.FindUselessFXLayers().Count;
                PerfRankChangeLabel("FX Layers", layerCount, optimizedLayerCount, PerformanceCategory.FXLayerCount);
            }
            PerfRankChangeLabel("Blend Shapes", totalBlendShapePaths.Count, KeptBlendShapePaths.Count, PerformanceCategory.BlendShapeCount);
            Profiler.EndSection();

            EditorGUILayout.Separator();

            if (optimizer.ExcludeTransforms == null)
                optimizer.ExcludeTransforms = new List<Transform>();
            
            if (Foldout("Exclusions (" + optimizer.ExcludeTransforms.Count + ")", ref optimizer.ShowExcludedTransforms))
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    DynamicTransformList(optimizer, nameof(optimizer.ExcludeTransforms));
                }
            }

            EditorGUILayout.Separator();

            if (Foldout("Show Mesh & Material Merge Preview", ref optimizer.ShowMeshAndMaterialMergePreview))
            {
                Profiler.StartSection("Show Merge Preview");
                foreach (var matched in MergedMaterialPreview)
                {
                    for (int i = 0; i < matched.Count; i++)
                    {
                        using var horizontalScope = new EditorGUILayout.HorizontalScope();
                        using (new EditorGUILayout.VerticalScope())
                        {
                            for (int j = 0; j < matched[i].Count; j++)
                            {
                                int indent = j == 0 ? 0 : 1;
                                DrawMatchedMaterialSlot(matched[i][j], indent);
                            }
                        }
                        var materials = matched[i].Select(slot => slot.material).Distinct().ToArray();
                        var buttonContent = new GUIContent("S", "Selects this group of " + materials.Length + " materials");
                        if (GUILayout.Button(buttonContent, GUILayout.Width(20)))
                        {
                            Selection.objects = materials;
                        }
                    }
                    EditorGUILayout.Space(8);
                }
                Profiler.EndSection();
            }

            EditorGUILayout.Separator();

            if (optimizer.GetFXLayer() != null)
            {
                if (Foldout("Show FX Layer Merge Result", ref optimizer.ShowFXLayerMergeResults))
                {
                    Profiler.StartSection("Show FX Layer Merge Errors");
                    
                    optimizer.ShowFXLayerMergeErrors = EditorGUILayout.ToggleLeft("Show Details", optimizer.ShowFXLayerMergeErrors);
                    
                    var errorMessages = optimizer.AnalyzeFXLayerMergeAbility();
                    var uselessLayers = optimizer.FindUselessFXLayers();
                    var fxLayer = optimizer.GetFXLayer();
                    var fxLayerLayers = optimizer.GetFXLayerLayers();
                    var nonErrors = new HashSet<string>() {"toggle", "motion time", "useless", "blend tree", "multi toggle"};
                    for (int i = 0; i < errorMessages.Count; i++)
                    {
                        var perfRating = PerformanceRating.VeryPoor;
                        if (errorMessages[i].Count == 1 && (errorMessages[i][0] == "toggle" || errorMessages[i][0] == "multi toggle" || errorMessages[i][0] == "blend tree"))
                            perfRating = PerformanceRating.Good;
                        else if (errorMessages[i].Count == 1 && errorMessages[i][0] == "motion time")
                            perfRating = PerformanceRating.Medium;
                        if (uselessLayers.Contains(i))
                            perfRating = PerformanceRating.Excellent;

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField(new GUIContent(GetPerformanceIconForRating(perfRating)), GUILayout.Width(20));
                            EditorGUILayout.LabelField(new GUIContent(i + fxLayerLayers[i].name, string.Join("\n", errorMessages[i])));
                        }
                        if (optimizer.ShowFXLayerMergeErrors)
                        {
                            using (new EditorGUI.IndentLevelScope(2))
                            {
                                foreach (var error in errorMessages[i].Where(e => !nonErrors.Contains(e)))
                                {
                                    EditorGUILayout.LabelField(error);
                                }
                            }
                        }
                    }
                    Profiler.EndSection();
                }
            }

            EditorGUILayout.Separator();

            if (Foldout("Debug Info", ref optimizer.ShowDebugInfo))
            {
                
                optimizer.ProfileTimeUsed = EditorGUILayout.ToggleLeft("Profile Time Used", optimizer.ProfileTimeUsed);
                
                EditorGUI.indentLevel++;
                if (Foldout("Unparsable Materials", ref optimizer.DebugShowUnparsableMaterials))
                {
                    Profiler.StartSection("Unparsable Materials");
                    var list = optimizer.GetUsedComponentsInChildren<Renderer>()
                        .SelectMany(r => r.sharedMaterials).Distinct()
                        .Select(mat => (mat, ShaderAnalyzer.Parse(mat?.shader, mat)))
                        .Where(t => t.Item2 == null)
                        .Select(t => t.mat).ToArray();
                    if (list.Length > 0)
                    {
                        EditorGUILayout.HelpBox($"Found {list.Length} materials with unparsable shaders.", MessageType.Info);
                        DrawDebugList(list);
                    }
                    Profiler.EndSection();
                }
                
                if (Foldout("Crunched Textures", ref optimizer.DebugShowCrunchedTextures))
                {
                    Profiler.StartSection("Crunched Textures");
                    DrawDebugList(CrunchedTextures);
                    Profiler.EndSection();
                }
                if (Foldout("NonBC5 Normal Maps", ref optimizer.DebugShowNonBC5NormalMaps))
                {
                    Profiler.StartSection("NonBC5 Normal Maps");
                    DrawDebugList(NonBC5NormalMaps);
                    Profiler.EndSection();
                }
                if (Foldout("Unmergable NaNimation by Animations", ref optimizer.DebugShowMeshesThatCantMergeNaNimationCausedByAnimations))
                {
                    Profiler.StartSection("Unmergable NaNimation by Animations");
                    DrawDebugList(CantMergeNaNimationBecauseOfWDONAnimations);
                    Profiler.EndSection();
                }
                
                
                if (Foldout("Locked in Materials", ref optimizer.DebugShowLockedInMaterials))
                {
                    Profiler.StartSection("Locked in Materials");
                    var list = optimizer.GetUsedComponentsInChildren<Renderer>()
                        .SelectMany(r => r.sharedMaterials).Distinct()
                        .Where(mat => IsLockedIn(mat)).ToArray();
                    DrawDebugList(list);
                    Profiler.EndSection();
                }
                if (Foldout("Unlocked Materials", ref optimizer.DebugShowUnlockedMaterials))
                {
                    Profiler.StartSection("Unlocked Materials");
                    var list = optimizer.GetUsedComponentsInChildren<Renderer>()
                        .SelectMany(r => r.sharedMaterials).Distinct()
                        .Where(mat => CanLockIn(mat) && !IsLockedIn(mat)).ToArray();
                    DrawDebugList(list);
                    Profiler.EndSection();
                }
                
                if (optimizer.FindAllPenetrators().Count > 0 && Foldout("Penetrators", ref optimizer.DebugShowPenetrators))
                {
                    Profiler.StartSection("Penetrators");
                    DrawDebugList(optimizer.FindAllPenetrators().ToArray());
                    Profiler.EndSection();
                }
                
                if (Foldout("Same Ratio Blend Shapes", ref optimizer.DebugShowMergeableBlendShapes))
                {
                    Profiler.StartSection("Same Ratio Blend Shapes");
                    foreach (var list in MergeableBlendShapes)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.Space(15, false);
                            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
                            {
                                foreach (var ratio in list)
                                {
                                    using (new EditorGUILayout.HorizontalScope())
                                    {
                                        EditorGUILayout.LabelField((ratio.value * 100).ToString("F1").Replace(".0", ""), GUILayout.Width(60));
                                        EditorGUILayout.LabelField(ratio.blendshape.Replace("/blendShape.", "."));
                                    }
                                }
                            }
                        }
                        EditorGUILayout.Separator();
                    }
                    Profiler.EndSection();
                }
                if (Foldout("Mesh Bone Weight Stats", ref optimizer.DebugShowBoneWeightStats))
                {
                    Profiler.StartSection("Mesh Bone Weight Stats");
                    var statsList = optimizer.GetUsedComponentsInChildren<SkinnedMeshRenderer>()
                        .Select(r => r.sharedMesh).Distinct()
                        .Select(mesh => (mesh, GetMeshBoneWeightStats(mesh)))
                        .OrderByDescending(t => t.Item2[3].count)
                        .ThenByDescending(t => t.Item2[2].count)
                        .ThenByDescending(t => t.Item2[1].count)
                        .ThenByDescending(t => t.Item2[0].count)
                        .ToArray();
                    foreach (var tuple in statsList)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.Space(15, false);
                            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
                            {
                                EditorGUILayout.ObjectField(tuple.mesh, typeof(Mesh), false);
                                var stats = tuple.Item2;
                                if (stats[0].count == 0)
                                {
                                    EditorGUILayout.LabelField("No bone weights");
                                }
                                else
                                {
                                    var entryWidth = GUILayout.Width(60f);
                                    using (new EditorGUILayout.HorizontalScope())
                                    {
                                        EditorGUILayout.LabelField("Index", entryWidth);
                                        EditorGUILayout.LabelField("Count", entryWidth);
                                        EditorGUILayout.LabelField("Max", entryWidth);
                                        EditorGUILayout.LabelField("Median", entryWidth);
                                    }
                                    for (int i = 0; i < 4; i++)
                                    {
                                        if (stats[i].count == 0)
                                            continue;

                                        using (new EditorGUILayout.HorizontalScope())
                                        {
                                            EditorGUILayout.LabelField(i.ToString(), entryWidth);
                                            EditorGUILayout.LabelField(stats[i].count.ToString(), entryWidth);
                                            EditorGUILayout.LabelField(stats[i].maxValue.ToString("F2"), entryWidth);
                                            EditorGUILayout.LabelField(stats[i].medianValue.ToString("F2"), entryWidth);
                                        }
                                    }
                                }
                            }
                        }
                        EditorGUILayout.Separator();
                    }
                    Profiler.EndSection();
                }
                if (Foldout("Phys Bone Dependencies", ref optimizer.DebugShowPhysBoneDependencies))
                {
                    Profiler.StartSection("Phys Bone Dependencies");
                    foreach (var pair in optimizer.FindAllPhysBoneDependencies())
                    {
                        if (pair.Key.gameObject.CompareTag("EditorOnly"))
                            continue;
                        EditorGUILayout.ObjectField(pair.Key, typeof(VRCPhysBoneBase), true);
                        using (new EditorGUI.IndentLevelScope())
                        {
                            DrawDebugList(pair.Value.ToArray());
                        }
                    }
                }
                if (Foldout("Unused Components", ref optimizer.DebugShowUnusedComponents))
                {
                    Profiler.StartSection("Unused Components");
                    DrawDebugList(optimizer.FindAllUnusedComponents().ToArray());
                    Profiler.EndSection();
                }
                if (Foldout("Always Disabled Game Objects", ref optimizer.DebugShowAlwaysDisabledGameObjects))
                {
                    Profiler.StartSection("Always Disabled Game Objects");
                    DrawDebugList(optimizer.FindAllAlwaysDisabledGameObjects().ToArray());
                    Profiler.EndSection();
                }
                if (Foldout("Material Swaps", ref optimizer.DebugShowMaterialSwaps))
                {
                    Profiler.StartSection("Material Swaps");
                    var map = optimizer.FindAllMaterialSwapMaterials();
                    foreach (var pair in map)
                    {
                        EditorGUILayout.LabelField(pair.Key.path + " -> " + pair.Key.index);
                        using (new EditorGUI.IndentLevelScope())
                        {
                            DrawDebugList(pair.Value.ToArray());
                        }
                    }
                    if (map.Count == 0)
                    {
                        EditorGUILayout.LabelField("---");
                    }
                    Profiler.EndSection();
                }
                if (Foldout("Animated Material Property Paths", ref optimizer.DebugShowAnimatedMaterialPropertyPaths))
                {
                    Profiler.StartSection("Animated Material Property Paths");
                    DrawDebugList(AnimatedMaterialPropertyPaths);
                    Profiler.EndSection();
                }
                if (Foldout("Game Objects with Toggle Animation", ref optimizer.DebugShowGameObjectsWithToggle))
                {
                    Profiler.StartSection("Game Objects with Toggle Animation");
                    DrawDebugList(GameObjectsWithToggleAnimations);
                    Profiler.EndSection();
                }
                if (Foldout("Unmoving Bones", ref optimizer.DebugShowUnmovingBones))
                {
                    Profiler.StartSection("Unmoving Bones");
                    DrawDebugList(UnmovingBones);
                    Profiler.EndSection();
                }
                EditorGUI.indentLevel--;
            }
            if (optimizer.ProfileTimeUsed)
            {
                EditorGUILayout.Separator();
                var timeUsed = Profiler.FormatTimeUsed().Take(6).ToArray();
                foreach (var time in timeUsed)
                {
                    EditorGUILayout.LabelField(time);
                }
            }
            stopWatch.Stop();
            if (stopWatch.ElapsedMilliseconds > longestTimeUsed && stopWatch.ElapsedMilliseconds > AutoRefreshPreviewTimeout)
            {
                longestTimeUsed = longestTimeUsed < 0 ? longestTimeUsed + 1 : stopWatch.ElapsedMilliseconds;
            }
        }

        private bool Validate()
        {
            var avDescriptor = optimizer.GetComponent<VRCAvatarDescriptor>();

            if (avDescriptor == null)
            {
                EditorGUILayout.HelpBox("No VRCAvatarDescriptor found on the root object.", MessageType.Error);
                return false;
            }

            var isHumanoid = optimizer.IsHumanoid();
            if (avDescriptor.baseAnimationLayers == null || avDescriptor.baseAnimationLayers.Length != (isHumanoid ? 5 : 3))
            {
                if (isHumanoid)
                {
                    EditorGUILayout.HelpBox("Humanoid rig but playable base layer count in the avatar descriptor is not 5.\n" +
                        "Try to reimport the avatar fbx.", MessageType.Error);
                }
                else
                {
                    EditorGUILayout.HelpBox("Generic rig but playable base layer count in the avatar descriptor is not 3.\n" +
                        "Try to reimport the avatar fbx.", MessageType.Error);
                }
                return false;
            }

            if (optimizer.name.EndsWith("(OptimizedCopy)"))
            {
                EditorGUILayout.HelpBox("Put the optimizer on the original avatar, not the optimized copy.", MessageType.Error);
                return false;
            }

            if (avDescriptor.lipSync == VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape
                && avDescriptor.VisemeSkinnedMesh != null)
            {
                var meshRenderer = avDescriptor.VisemeSkinnedMesh;
                if (optimizer.GetComponentsInChildren<SkinnedMeshRenderer>(true).All(r => r != meshRenderer))
                {
                    EditorGUILayout.HelpBox("Viseme SkinnedMeshRenderer is not a child of the avatar root.", MessageType.Error);
                }
            }

            if (avDescriptor.customEyeLookSettings.eyelidType == VRCAvatarDescriptor.EyelidType.Blendshapes
                && avDescriptor.customEyeLookSettings.eyelidsSkinnedMesh != null)
            {
                var meshRenderer = avDescriptor.customEyeLookSettings.eyelidsSkinnedMesh;
                if (optimizer.GetComponentsInChildren<SkinnedMeshRenderer>(true).All(r => r != meshRenderer))
                {
                    EditorGUILayout.HelpBox("Eyelid SkinnedMeshRenderer is not a child of the avatar root.", MessageType.Error);
                }
            }

            if (Object.FindObjectsOfType<VRCAvatarDescriptor>().Any(av => av != null && av.name.EndsWith("(OptimizedCopy)")))
            {
                EditorGUILayout.HelpBox("Optimized copy of some avatar is present in the scene.\n" +
                    "Its assets will be deleted when creating a new optimized copy.", MessageType.Error);
            }

            if (Object.FindObjectsOfType<VRCAvatarDescriptor>().Any(av => av != null && av.name.EndsWith("(BrokenCopy)")))
            {
                EditorGUILayout.HelpBox("Seems like the last optimization attempt failed.\n" +
                    "You can try to delete the broken copy and try again with different settings or adding parts to the exclusion list.\n" +
                    "Click this message to find or create a bug report on github.", MessageType.Error);
                if (Event.current.type == EventType.MouseDown && GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                    Application.OpenURL("https://github.com/whyknot/WKVRCOptimizer/issues");
            }

            var exclusions = optimizer.GetAllExcludedTransforms();

            var animatorsExcludingRoot = optimizer.GetComponentsInChildren<Animator>(true)
                .Where(a => a.gameObject != optimizer.gameObject)
                .Where(a => !exclusions.Contains(a.transform))
                .Where(a => a.runtimeAnimatorController != null)
                .ToArray();

            if (animatorsExcludingRoot.Length > 0)
            {
                EditorGUILayout.HelpBox("Some animators exist that are not on the root object.\n" +
                    "The optimizer only supports animators in the custom playable layers in the avatar descriptor.\n" +
                    "If the optimized copy is broken, try to add the animators to the exclusion list.", MessageType.Warning);
                if (GUILayout.Button("Auto add extra animators to exclusion list"))
                {
                    foreach (var animator in animatorsExcludingRoot)
                    {
                        optimizer.ExcludeTransforms.Add(animator.transform);
                    }
                    optimizer.ShowExcludedTransforms = true;
                    ClearUICaches();
                }
            }

            if (NonBC5NormalMaps.Length > 0)
            {
                EditorGUILayout.HelpBox("Some normal maps are not BC5 compressed.\n" +
                    "BC5 compressed normal maps are highest quality for the same VRAM size as the other compression options.\n" +
                    "Check the Debug Info foldout for a full list or click the button to automatically change them all to BC5.", MessageType.Info);
                if (GUILayout.Button("Convert all (" + NonBC5NormalMaps.Length + ") normal maps to BC5"))
                {
                    foreach (var tex in NonBC5NormalMaps)
                    {
                        var path = AssetDatabase.GetAssetPath(tex);
                        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                        var platformSettings = importer.GetPlatformTextureSettings("Standalone");
                        platformSettings.resizeAlgorithm = TextureResizeAlgorithm.Bilinear;
                        platformSettings.overridden = true;
                        platformSettings.format = TextureImporterFormat.BC5;
                        platformSettings.maxTextureSize = Mathf.Max(tex.width, tex.height);
                        importer.SetPlatformTextureSettings(platformSettings);
                        importer.SaveAndReimport();
                    }
                    ClearUICaches();
                }
            }

            if (CantMergeNaNimationBecauseOfWDONAnimations.Length > 0)
            {
                 EditorGUILayout.HelpBox("Some meshes are missing the corresponding on or off toggle animation. This is likely due to a WD ON workflow.\n" +
                    "This means they can\'t be merged with NaNimation and switching to a WD OFF workflow would help reduce mesh count further.\n" +
                    "Check the Debug Info foldout for a full list at:\n\"Unmergable NaNimation by Animations\"", MessageType.Info);
            }

            bool hasExtraMaterialSlots = optimizer.GetNonEditorOnlyComponentsInChildren<Renderer>()
                .Where(r => !exclusions.Contains(r.transform))
                .Where(r => r.GetSharedMesh() != null)
                .Any(r => r.sharedMaterials.Length > r.GetSharedMesh().subMeshCount);

            if (hasExtraMaterialSlots)
            {
                EditorGUILayout.HelpBox("Some renderers have more material slots than sub meshes.\n" + 
                    "Those extra materials & polys are not counted by VRChats performance system. " + 
                    "After optimizing those extra slots and polys will get baked as real ones.\n" + 
                    "You should expect your poly count to increase, this is working as intended!", MessageType.Info);
            }

            var furyType = Type.GetType("VF.Model.VRCFury, VRCFury");
            if (furyType != null && optimizer.GetComponentsInChildren(furyType, true).Any())
            {
                EditorGUILayout.HelpBox("VRCFury is used on the avatar. This means the perf rank change and merge result previews can be inaccurate as the optimizer does not take VRCFury into account for those.\n" +
                    "To test in editor built a VRCFury test avatar and use the optimizer on that.\n" +
                    "For uploading, simply upload the avatar. The optimizer runs automatically in the build pipeline, ensuring proper order with VRCFury.", MessageType.Warning);
                return false;
            }

            #if MODULAR_AVATAR_EXISTS
            if (optimizer.GetComponentsInChildren<nadena.dev.modular_avatar.core.AvatarTagComponent>(true).Any())
            {
                EditorGUILayout.HelpBox("Modular Avatar is used on the avatar. This means the perf rank change and merge result previews " + 
                    "can be inaccurate as the optimizer does not take Modular Avatar into account for those.\n" +
                    "To test in editor use \"Manual bake avatar\" before clicking the optimize button.\n" +
                    "For uploading, simply upload the avatar. The optimizer runs automatically in the build pipeline, ensuring proper order with Modular Avatar.", MessageType.Warning);
                return false;
            }
            #endif

            return true;
        }

        private void AssignNewAvatarIDIfEmpty()
        {
            var avDescriptor = optimizer.GetComponent<VRCAvatarDescriptor>();
            if (avDescriptor == null)
                return;
            if (!optimizer.TryGetComponent<VRC.Core.PipelineManager>(out var pm))
            {
                pm = optimizer.gameObject.AddComponent<VRC.Core.PipelineManager>();
            }
            if (!string.IsNullOrEmpty(pm.blueprintId))
                return;
            pm.AssignId(VRC.Core.PipelineManager.ContentType.avatar);
        }

        private AvatarOptimizer lastSelected = null;
        private List<List<List<MaterialSlot>>> mergedMaterialPreviewCache = null;
        private Transform[] unmovingBonesCache = null;
        private GameObject[] gameObjectsWithToggleAnimationsCache = null;
        private Texture2D[] crunchedTexturesCache = null;
        private Texture2D[] nonBC5NormalMapsCache = null;
        private Renderer[] cantMergeNaNimationBecauseOfWDONAnimationsCache = null;
        private string[] animatedMaterialPropertyPathsCache = null;
        private HashSet<string> keptBlendShapePathsCache = null;
        private List<List<(string blendshape, float value)>> mergeableBlendShapesCache = null;
        private Dictionary<Mesh, (int count, float maxValue, float medianValue)[]> meshBoneWeightStatsCache = null;

        private void ClearUICaches()
        {
            if (longestTimeUsed > AutoRefreshPreviewTimeout)
                return;
            mergedMaterialPreviewCache = null;
            unmovingBonesCache = null;
            gameObjectsWithToggleAnimationsCache = null;
            crunchedTexturesCache = null;
            nonBC5NormalMapsCache = null;
            cantMergeNaNimationBecauseOfWDONAnimationsCache = null;
            animatedMaterialPropertyPathsCache = null;
            keptBlendShapePathsCache = null;
            mergeableBlendShapesCache = null;
            optimizer.ClearCaches();
        }

        private void OnSelectionChange()
        {
            if (lastSelected == optimizer)
                return;
            if (longestTimeUsed > 0)
                longestTimeUsed = 0;
            lastSelected = optimizer;
            ClearUICaches();
            
            ShaderAnalyzer.ParseAndCacheAllShaders(optimizer.FindAllUsedMaterials().Select(m => (m.shader, m)), false);
        }

        private (int count, float maxValue, float medianValue)[] GetMeshBoneWeightStats(Mesh mesh)
        {
            if (meshBoneWeightStatsCache == null)
                meshBoneWeightStatsCache = new Dictionary<Mesh, (int count, float maxValue, float medianValue)[]>();
            if (mesh == null)
                return new (int count, float maxValue, float medianValue)[4];
            if (!meshBoneWeightStatsCache.TryGetValue(mesh, out var stats))
            {
                stats = new (int count, float maxValue, float medianValue)[4];
                var nonZeroWeights = new List<float>[4]
                {
                    new List<float>(),
                    new List<float>(),
                    new List<float>(),
                    new List<float>(),
                };
                var boneWeights = mesh.boneWeights;
                for (int i = 0; i < boneWeights.Length; i++)
                {
                    var weight = boneWeights[i];
                    if (weight.weight0 > 0)
                        nonZeroWeights[0].Add(weight.weight0);
                    if (weight.weight1 > 0)
                        nonZeroWeights[1].Add(weight.weight1);
                    if (weight.weight2 > 0)
                        nonZeroWeights[2].Add(weight.weight2);
                    if (weight.weight3 > 0)
                        nonZeroWeights[3].Add(weight.weight3);
                }
                for (int i = 0; i < 4; i++)
                {
                    stats[i].count = nonZeroWeights[i].Count;
                    if (stats[i].count == 0)
                    {
                        stats[i].maxValue = 0;
                        stats[i].medianValue = 0;
                        continue;
                    }
                    nonZeroWeights[i].Sort();
                    stats[i].maxValue = nonZeroWeights[i][stats[i].count - 1];
                    stats[i].medianValue = nonZeroWeights[i][stats[i].count / 2];
                }
                meshBoneWeightStatsCache.Add(mesh, stats);
            }
            return stats;
        }

        private List<List<List<MaterialSlot>>> MergedMaterialPreview
        {
            get
            {
                if (mergedMaterialPreviewCache == null)
                {
                    mergedMaterialPreviewCache = new List<List<List<MaterialSlot>>>();
                    var matchedSkinnedMeshes = optimizer.FindPossibleSkinnedMeshMerges();
                    foreach (var mergedMeshes in matchedSkinnedMeshes)
                    {
                        var matched = optimizer.FindAllMergeAbleMaterials(mergedMeshes);
                        mergedMaterialPreviewCache.Add(matched);
                    }
                }
                return mergedMaterialPreviewCache;
            }
        }

        private List<List<(string blendshape, float value)>> MergeableBlendShapes
        {
            get
            {
                if (mergeableBlendShapesCache == null)
                {
                    mergeableBlendShapesCache = new List<List<(string blendshape, float value)>>();
                    
                    foreach (var matched in MergedMaterialPreview)
                    {
                        var renderers = matched.SelectMany(m => m).Select(slot => slot.renderer).Distinct().ToArray();
                        var mergedBlendShapes = optimizer.FindMergeableBlendShapes(renderers);
                        mergeableBlendShapesCache.AddRange(mergedBlendShapes);
                    }
                }
                return mergeableBlendShapesCache;
            }
        }

        private Renderer[] CantMergeNaNimationBecauseOfWDONAnimations
        {
            get
            {
                if (cantMergeNaNimationBecauseOfWDONAnimationsCache != null)
                    return cantMergeNaNimationBecauseOfWDONAnimationsCache;
                return cantMergeNaNimationBecauseOfWDONAnimationsCache =
                    optimizer.FindAllPathsWhereMeshOrGameObjectHasOnlyOnAnimation()
                        .Select(p => optimizer.transform.GetTransformFromPath(p))
                        .Where(t => t != null)
                        .Select(t => t.GetComponent<Renderer>())
                        .Where(r => r != null && !optimizer.GetRendererDefaultEnabledState(r))
                        .ToArray();
            }
        }

        private HashSet<string> KeptBlendShapePaths
        {
            get
            {
                if (keptBlendShapePathsCache == null)
                {
                    optimizer.ProcessBlendShapes();

                    var skinnedMeshes = optimizer.GetUsedComponentsInChildren<SkinnedMeshRenderer>();
                    keptBlendShapePathsCache = new HashSet<string>(skinnedMeshes.SelectMany(r => {
                        if (r.sharedMesh == null)
                            return new string[0];
                        return Enumerable.Range(0, r.sharedMesh.blendShapeCount)
                            .Select(i => r.transform.GetPathToRoot(optimizer.transform) + "/blendShape." + r.sharedMesh.GetBlendShapeName(i));
                    }));

                    foreach (var list in MergeableBlendShapes)
                    {
                        for (int i = 1; i < list.Count; i++)
                            keptBlendShapePathsCache.Remove(list[i].blendshape);
                    }

                    keptBlendShapePathsCache.IntersectWith(optimizer.GetUsedBlendShapePaths());
                }
                return keptBlendShapePathsCache;
            }
        }

        private Transform[] UnmovingBones
        {
            get
            {
                if (unmovingBonesCache == null)
                {
                    var bones = new HashSet<Transform>();
                    var unmoving = optimizer.FindAllUnmovingTransforms();
                    optimizer.GetUsedComponentsInChildren<SkinnedMeshRenderer>().ToList().ForEach(
                        r => bones.UnionWith(r.bones.Where(b => unmoving.Contains(b))));
                    unmovingBonesCache = bones.ToArray();
                }
                return unmovingBonesCache;
            }
        }

        private GameObject[] GameObjectsWithToggleAnimations
        {
            get
            {
                if (gameObjectsWithToggleAnimationsCache == null)
                {
                    gameObjectsWithToggleAnimationsCache =
                        optimizer.FindAllGameObjectTogglePaths()
                        .Select(p => optimizer.transform.GetTransformFromPath(p)?.gameObject)
                        .Where(obj => obj != null).ToArray();
                }
                return gameObjectsWithToggleAnimationsCache;
            }
        }

        private Texture2D[] CrunchedTextures
        {
            get
            {
                if (crunchedTexturesCache == null)
                {
                    var exclusions = optimizer.GetAllExcludedTransforms();
                    var tuple = optimizer.GetUsedComponentsInChildren<Renderer>()
                        .Where(r => !exclusions.Contains(r.transform))
                        .SelectMany(r => r.sharedMaterials).Distinct()
                        .Select(mat => (mat, ShaderAnalyzer.Parse(mat?.shader, mat)))
                        .Where(t => t.Item2 != null).ToArray();
                    var textures = new HashSet<Texture2D>();
                    foreach (var (mat, ir) in tuple)
                    {
                        foreach (var tex in GetTexturesFromIR(ir))
                        {
                            if (tex != null && (tex.format == TextureFormat.DXT1Crunched || tex.format == TextureFormat.DXT5Crunched))
                                textures.Add(tex);
                        }
                    }
                    crunchedTexturesCache = textures.ToArray();
                }
                return crunchedTexturesCache;
            }
        }

        private Texture2D[] NonBC5NormalMaps
        {
            get
            {
                if (nonBC5NormalMapsCache == null)
                {
                    var exclusions = optimizer.GetAllExcludedTransforms();
                    var renderers = optimizer.GetUsedComponentsInChildren<Renderer>();
                    var textures = new HashSet<Texture2D>();
                    var materials = renderers
                        .Where(r => !exclusions.Contains(r.transform))
                        .SelectMany(r => r.sharedMaterials)
                        .Where(mat => mat != null && mat.shader != null)
                        .Distinct();
                    foreach (var material in materials)
                    {
                        var ir = ShaderAnalyzer.Parse(material.shader, material);
                        if (ir == null || ir.normalMap.texture == null)
                            continue;
                        
                        var tex = ir.normalMap.texture;
                        if (tex != null && tex.format != TextureFormat.BC5)
                        {
                            var assetImporter = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex)) as TextureImporter;
                            if (assetImporter != null && assetImporter.textureType == TextureImporterType.NormalMap)
                            {
                                textures.Add(tex);
                            }
                        }
                    }
                    nonBC5NormalMapsCache = textures.ToArray();
                }
                return nonBC5NormalMapsCache;
            }
        }

        private string[] AnimatedMaterialPropertyPaths
        {
            get
            {
                if (animatedMaterialPropertyPathsCache == null)
                {
                    animatedMaterialPropertyPathsCache = optimizer.FindAllAnimatedMaterialProperties()
                        .SelectMany(kv => kv.Value.Select(prop => $"{kv.Key}.{prop}")).ToArray();
                }
                return animatedMaterialPropertyPathsCache;
            }
        }

        public bool CanLockIn(Material material)
        {
            if (material == null)
                return false;
            if (material.HasProperty("_ShaderOptimizer"))
                return true;
            if (material.HasProperty("_ShaderOptimizerEnabled"))
                return true;
            if (material.HasProperty("__Baked"))
                return true;
            return false;
        }

        public bool IsLockedIn(Material material)
        {
            if (material == null)
                return false;
            if (material.HasProperty("_ShaderOptimizer") && material.GetInt("_ShaderOptimizer") == 1)
                return true;
            if (material.HasProperty("_ShaderOptimizerEnabled") && material.GetInt("_ShaderOptimizerEnabled") == 1)
                return true;
            if (material.HasProperty("__Baked") && material.GetInt("__Baked") == 1)
                return true;
            return false;
        }

        private bool Button(string label)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(15 * EditorGUI.indentLevel);
                return GUILayout.Button(label);
            }
        }

        private bool Foldout(string label, ref bool value)
        {
            var content = new GUIContent(label);
            bool output = EditorGUILayout.Foldout(value, content, true);
            if (value != output)
            {
                EditorUtility.SetDirty(optimizer);
            }
            return value = output;
        }

        private void DrawMatchedMaterialSlot(MaterialSlot slot, int indent)
        {
            indent *= 15;
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(indent);
                EditorGUILayout.ObjectField(slot.renderer, typeof(Renderer), true, GUILayout.Width(EditorGUIUtility.currentViewWidth / 2 - 20 - (indent)));
                int originalIndent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 0;
                EditorGUILayout.ObjectField(slot.material, typeof(Material), false);
                EditorGUI.indentLevel = originalIndent;
            }
        }

        public void DrawDebugList<T>(T[] array) where T : Object
        {
            foreach (var obj in array)
            {
                EditorGUILayout.ObjectField(obj, typeof(T), true);
            }
            if (array.Length == 0)
            {
                EditorGUILayout.LabelField("---");
            }
            else if (Button("Select All"))
            {
                if (typeof(Component).IsAssignableFrom(typeof(T)))
                {
                    Selection.objects = array.Select(o => (o as Component).gameObject).ToArray();
                }
                else
                {
                    Selection.objects = array;
                }
            }
        }

        public void DrawDebugList(string[] array)
        {
            foreach (var obj in array)
            {
                EditorGUILayout.LabelField(obj);
            }
            if (array.Length == 0)
            {
                EditorGUILayout.LabelField("---");
            }
        }

        private void DynamicTransformList(Object obj, string propertyPath)
        {
            using (var serializedObject = new SerializedObject(obj))
            {
                SerializedProperty listProperty = serializedObject.FindProperty(propertyPath);

                listProperty.InsertArrayElementAtIndex(listProperty.arraySize);
                SerializedProperty newElement = listProperty.GetArrayElementAtIndex(listProperty.arraySize - 1);
                newElement.objectReferenceValue = null;

                for (int i = 0; i < listProperty.arraySize; i++)
                {
                    SerializedProperty element = listProperty.GetArrayElementAtIndex(i);
                    Transform output = null;

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        output = EditorGUILayout.ObjectField(element.objectReferenceValue, typeof(Transform), true) as Transform;

                        if (i == listProperty.arraySize - 1)
                        {
                            GUILayout.Space(23);
                        }
                        else if (GUILayout.Button("X", GUILayout.Width(20)))
                        {
                            output = null;
                        }
                    }

                    if (element.objectReferenceValue != output)
                    {
                        ClearUICaches();
                    }

                    if (output != null && optimizer.transform.GetPathToRoot(output) == null)
                    {
                        output = null;
                    }

                    element.objectReferenceValue = output;
                }

                for (int i = listProperty.arraySize - 1; i >= 0; i--)
                {
                    SerializedProperty element = listProperty.GetArrayElementAtIndex(i);
                    if (element.objectReferenceValue == null)
                    {
                        listProperty.DeleteArrayElementAtIndex(i);
                    }
                }

                serializedObject.ApplyModifiedProperties();
            }
        }

        static Texture _perfIcon_Excellent;
        static Texture _perfIcon_Good;
        static Texture _perfIcon_Medium;
        static Texture _perfIcon_Poor;
        static Texture _perfIcon_VeryPoor;

        private Texture GetPerformanceIconForRating(PerformanceRating value)
        {
            if (_perfIcon_Excellent == null)
                _perfIcon_Excellent = Resources.Load<Texture>("PerformanceIcons/Perf_Great_32");
            if (_perfIcon_Good == null)
                _perfIcon_Good = Resources.Load<Texture>("PerformanceIcons/Perf_Good_32");
            if (_perfIcon_Medium == null)
                _perfIcon_Medium = Resources.Load<Texture>("PerformanceIcons/Perf_Medium_32");
            if (_perfIcon_Poor == null)
                _perfIcon_Poor = Resources.Load<Texture>("PerformanceIcons/Perf_Poor_32");
            if (_perfIcon_VeryPoor == null)
                _perfIcon_VeryPoor = Resources.Load<Texture>("PerformanceIcons/Perf_Horrible_32");

            switch (value)
            {
                case PerformanceRating.Excellent:
                    return _perfIcon_Excellent;
                case PerformanceRating.Good:
                    return _perfIcon_Good;
                case PerformanceRating.Medium:
                    return _perfIcon_Medium;
                case PerformanceRating.Poor:
                    return _perfIcon_Poor;
                default:
                    return _perfIcon_VeryPoor;
            }
        }

        PerformanceRating GetPerfRank(int count, int[] perfLevels)
        {
            int level = 0;
            while(level < perfLevels.Length && count > perfLevels[level])
            {
                level++;
            }
            level++;
            return (PerformanceRating)level;
        }

        enum PerformanceCategory
        {
            SkinnedMeshCount,
            MeshCount,
            MaterialCount,
            FXLayerCount,
            BlendShapeCount,
        }

        static Dictionary<PerformanceCategory, int[]> _perfLevelsWindows = new Dictionary<PerformanceCategory, int[]>()
        {
            { PerformanceCategory.SkinnedMeshCount, new int[] {1, 2, 8, 16, int.MaxValue} },
            { PerformanceCategory.MeshCount, new int[] {4, 8, 16, 24, int.MaxValue} },
            { PerformanceCategory.MaterialCount, new int[] {4, 8, 16, 32, int.MaxValue} },
            { PerformanceCategory.FXLayerCount, new int[] {4, 8, 16, 32, int.MaxValue} },
            { PerformanceCategory.BlendShapeCount, new int[] {32, 48, 64, 128, int.MaxValue} },
        };
        static Dictionary<PerformanceCategory, int[]> _perfLevelsAndroid = new Dictionary<PerformanceCategory, int[]>()
        {
            { PerformanceCategory.SkinnedMeshCount, new int[] {1, 1, 2, 2, int.MaxValue} },
            { PerformanceCategory.MeshCount, new int[] {1, 1, 2, 2, int.MaxValue} },
            { PerformanceCategory.MaterialCount, new int[] {1, 1, 2, 4, int.MaxValue} },
            { PerformanceCategory.FXLayerCount, new int[] {2, 4, 8, 16, int.MaxValue} },
            { PerformanceCategory.BlendShapeCount, new int[] {24, 32, 48, 64, int.MaxValue} },
        };

        private void PerfRankChangeLabel(string label, int oldValue, int newValue, PerformanceCategory category)
        {
            var oldRating = PerformanceRating.VeryPoor;
            var newRating = PerformanceRating.VeryPoor;
            var perfLevels = AvatarOptimizer.HasCustomShaderSupport ? _perfLevelsWindows : _perfLevelsAndroid;
            if (perfLevels.ContainsKey(category))
            {
                oldRating = GetPerfRank(oldValue, perfLevels[category]);
                newRating = GetPerfRank(newValue, perfLevels[category]);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(new GUIContent(GetPerformanceIconForRating(oldRating)), GUILayout.Width(20));
                EditorGUILayout.LabelField(oldValue.ToString(), GUILayout.Width(25));
                EditorGUILayout.LabelField("->", GUILayout.Width(20));
                EditorGUILayout.LabelField(new GUIContent(GetPerformanceIconForRating(newRating)), GUILayout.Width(20));
                EditorGUILayout.LabelField(newValue.ToString(), GUILayout.Width(25));
                EditorGUILayout.LabelField(label);
            }
        }
    }
}
