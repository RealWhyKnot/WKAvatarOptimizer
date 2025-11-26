using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using VRC.SDK3.Dynamics.Contact.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;
using System.Text.RegularExpressions;
using Array = System.Array;

#if UNITY_EDITOR
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
using UnityEngine.Rendering;
using UnityEngine.Animations;
using UnityEditor;
using UnityEditor.Animations;
using WKVRCOptimizer.Core.Util;
using WKVRCOptimizer.Extensions;
using WKVRCOptimizer.Data;
using WKVRCOptimizer.Core;
using VRC.Dynamics;
using VRC.SDK3.Avatars.Components;

using Math = System.Math;
using Type = System.Type;
using Path = System.IO.Path;
using AnimationPath = System.ValueTuple<string, string, System.Type>;
using BlendableLayer = VRC.SDKBase.VRC_AnimatorLayerControl.BlendableLayer;
#endif

[HelpURL("https://github.com/whyknot/WKVRCOptimizer/blob/main/README.md")]
[AddComponentMenu("WK VRC Optimizer")]
public partial class AvatarOptimizer : MonoBehaviour, VRC.SDKBase.IEditorOnly
{
    public Settings settings = new Settings();
    public bool DoAutoSettings = true;
    public bool ShowExcludedTransforms = false;
    public List<Transform> ExcludeTransforms = new List<Transform>();
    public bool ShowMeshAndMaterialMergePreview = true;
    public bool ShowFXLayerMergeResults = true;
    private bool _ShowFXLayerMergeErrors = false;
    public bool ShowFXLayerMergeErrors { get { return _ShowFXLayerMergeErrors; } set { _ShowFXLayerMergeErrors = value; } }
    public bool ShowDebugInfo = false;
    public bool DebugShowUnparsableMaterials = true;
    public bool DebugShowUnmergableMaterials = true;
    public bool DebugShowUnmergableTextureMaterials = true;
    public bool DebugShowCrunchedTextures = true;
    public bool DebugShowNonBC5NormalMaps = true;
    public bool DebugShowMeshesThatCantMergeNaNimationCausedByAnimations = true;
    public bool DebugShowLockedInMaterials = true;
    public bool DebugShowUnlockedMaterials = true;
    public bool DebugShowPenetrators = true;
    public bool DebugShowMergeableBlendShapes = true;
    public bool DebugShowBoneWeightStats = true;
    public bool DebugShowPhysBoneDependencies = true;
    public bool DebugShowUnusedComponents = true;
    public bool DebugShowAlwaysDisabledGameObjects = true;
    public bool DebugShowMaterialSwaps = true;
    public bool DebugShowAnimatedMaterialPropertyPaths = true;
    public bool DebugShowGameObjectsWithToggle = true;
    public bool DebugShowUnmovingBones = false;

#if UNITY_EDITOR

    public OptimizationContext context;
    private CacheManager cacheManager;
    public MeshOptimizer meshOptimizer;
    public FXLayerOptimizer fxLayerOptimizer;
    public ComponentOptimizer componentOptimizer;
    public MaterialOptimizer materialOptimizer;
    public AnimationRewriter animationRewriter;

    public void EnsureInitializedForEditor()
    {
        _Log("EnsureInitializedForEditor() called.");
        if (context == null)
        {
            _Log("Initializing OptimizationContext.");
            context = new OptimizationContext();
        }
        if (cacheManager == null)
        {
            _Log("Initializing CacheManager.");
            cacheManager = new CacheManager(context, settings, gameObject);
        }
        if (fxLayerOptimizer == null)
        {
            _Log("Initializing FXLayerOptimizer.");
            fxLayerOptimizer = new FXLayerOptimizer(context, settings, gameObject, this);
        }
        if (componentOptimizer == null)
        {
            _Log("Initializing ComponentOptimizer.");
            componentOptimizer = new ComponentOptimizer(context, settings, gameObject, this);
        }
        if (materialOptimizer == null)
        {
            _Log("Initializing MaterialOptimizer.");
            materialOptimizer = new MaterialOptimizer(context, settings, gameObject, this);
        }
        if (meshOptimizer == null)
        {
            _Log("Initializing MeshOptimizer.");
            meshOptimizer = new MeshOptimizer(context, cacheManager, settings, gameObject, this);
        }
        if (animationRewriter == null)
        {
            _Log("Initializing AnimationRewriter.");
            animationRewriter = new AnimationRewriter(context, settings, gameObject, cacheManager, componentOptimizer, fxLayerOptimizer, meshOptimizer, this);
        }
        _Log("EnsureInitializedForEditor() finished.");
    }

    public void Optimize()
    {
        _Log("Optimize() started.");
        context = new OptimizationContext();
        cacheManager = new CacheManager(context, settings, gameObject);
        fxLayerOptimizer = new FXLayerOptimizer(context, settings, gameObject, this);
        componentOptimizer = new ComponentOptimizer(context, settings, gameObject, this);
        materialOptimizer = new MaterialOptimizer(context, settings, gameObject, this);
        meshOptimizer = new MeshOptimizer(context, cacheManager, settings, gameObject, this);
        animationRewriter = new AnimationRewriter(context, settings, gameObject, cacheManager, componentOptimizer, fxLayerOptimizer, meshOptimizer, this);

        var oldCulture = Thread.CurrentThread.CurrentCulture;
        var oldUICulture = Thread.CurrentThread.CurrentUICulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
            DisplayProgressBar("Clear TrashBin Folder", 0.01f);
            AssetManager.ClearTrashBin(context);
            Profiler.StartSection("ClearCaches()");
            ClearCaches();
            DisplayProgressBar("Destroying unused components", 0.2f);
            Profiler.StartNextSection("DestroyEditorOnlyGameObjects()");
            componentOptimizer.DestroyEditorOnlyGameObjects();
            Profiler.StartNextSection("DestroyUnusedComponents()");
            componentOptimizer.DestroyUnusedComponents();
            DisplayProgressBar("Removing duplicate materials", 0.05f);
            Profiler.StartNextSection("DeduplicateMaterials()");
            materialOptimizer.DeduplicateMaterials();
            if (WritePropertiesAsStaticValues)
            {
                DisplayProgressBar("Parsing Shaders", 0.05f);
                Profiler.StartNextSection("ParseAndCacheAllShaders()");
                ShaderAnalyzer.ParseAndCacheAllShaders(materialOptimizer.FindAllUsedMaterials().Select(m => m.shader), true,
                    (done, total) => DisplayProgressBar($"Parsing Shaders ({done}/{total})", 0.05f + 0.15f * done / total));
            }
            context.physBonesToDisable = FindAllPhysBonesToDisable();
            Profiler.StartNextSection("ConvertStaticMeshesToSkinnedMeshes()");
            _Log("Converting static meshes to skinned meshes...");
            ConvertStaticMeshesToSkinnedMeshes();
            Profiler.StartNextSection("CalculateUsedBlendShapePaths()");
            _Log("Processing blend shapes...");
            meshOptimizer.ProcessBlendShapes();
            Profiler.StartNextSection("DeleteAllUnusedSkinnedMeshRenderers()");
            _Log("Deleting unused skinned mesh renderers...");
            DeleteAllUnusedSkinnedMeshRenderers();
            Profiler.StartNextSection("CombineSkinnedMeshes()");
            DisplayProgressBar("Combining meshes", 0.2f);
            CombineSkinnedMeshes();

            Profiler.StartNextSection("CombineAndOptimizeMaterials()");
            DisplayProgressBar("Optimizing materials", 0.3f);
            materialOptimizer.CombineAndOptimizeMaterials();
            Profiler.StartNextSection("OptimizeMaterialSwapMaterials()");
            _Log("Optimizing material swap materials...");
            materialOptimizer.OptimizeMaterialSwapMaterials();
            Profiler.StartNextSection("OptimizeMaterialsOnNonSkinnedMeshes()");
            _Log("Optimizing materials on non-skinned meshes...");
            materialOptimizer.OptimizeMaterialsOnNonSkinnedMeshes();
            Profiler.StartNextSection("SaveOptimizedMaterials()");
            DisplayProgressBar("Reload optimized materials", 0.60f);
            materialOptimizer.SaveOptimizedMaterials();
            Profiler.StartNextSection("DestroyUnusedGameObjects()");
            DisplayProgressBar("Destroying unused GameObjects", 0.90f);
            componentOptimizer.DestroyUnusedGameObjects();
            Profiler.StartNextSection("FixAllAnimationPaths()");
            DisplayProgressBar("Fixing animation paths", 0.95f);
            FixAllAnimationPaths();
            Profiler.StartNextSection("MoveRingFingerColliderToFeet()");
            _Log("Moving ring finger collider to feet...");
            DisplayProgressBar("Done", 1.0f);
            componentOptimizer.MoveRingFingerColliderToFeet();
            Profiler.StartNextSection("DestroyImmediate(this)");
            DestroyImmediate(this);
            Profiler.EndSection();
            _Log("Optimize() finished successfully.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[WKVRCOptimizer] An error occurred during optimization: {e.Message}\n{e.StackTrace}");
            throw; // Re-throw the exception to ensure it's still propagated.
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = oldCulture;
            Thread.CurrentThread.CurrentUICulture = oldUICulture;
            EditorUtility.ClearProgressBar();
        }
    }

    public static bool HasCustomShaderSupport { get => EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneWindows64; }
    public bool ApplyOnUpload { get { return settings.ApplyOnUpload; } set { settings.ApplyOnUpload = value; } }
    public bool WritePropertiesAsStaticValues {
        get { return HasCustomShaderSupport && (settings.WritePropertiesAsStaticValues || MergeSkinnedMeshesWithShaderToggle || settings.MergeDifferentPropertyMaterials); }
        set { settings.WritePropertiesAsStaticValues = value; } }
    public bool MergeSkinnedMeshes { get { return settings.MergeSkinnedMeshes; } set { settings.MergeSkinnedMeshes = value; } }
    public bool MergeSkinnedMeshesWithShaderToggle {
        get { return HasCustomShaderSupport && settings.MergeSkinnedMeshes && settings.MergeSkinnedMeshesWithShaderToggle != 0; }
        set { settings.MergeSkinnedMeshesWithShaderToggle = value ? 1 : 0; } }
    public bool MergeSkinnedMeshesWithNaNimation {
        get { return settings.MergeSkinnedMeshes && settings.MergeSkinnedMeshesWithNaNimation != 0; }
        set { settings.MergeSkinnedMeshesWithNaNimation = value ? 1 : 0; } }
    public bool NaNimationAllow3BoneSkinning {
        get { return MergeSkinnedMeshesWithNaNimation && settings.NaNimationAllow3BoneSkinning; }
        set { settings.NaNimationAllow3BoneSkinning = value; } }
    public bool MergeSkinnedMeshesSeparatedByDefaultEnabledState {
        get { return MergeSkinnedMeshesWithNaNimation && settings.MergeSkinnedMeshesSeparatedByDefaultEnabledState; }
        set { settings.MergeSkinnedMeshesSeparatedByDefaultEnabledState = value; } }
    public bool MergeStaticMeshesAsSkinned {
        get { return settings.MergeSkinnedMeshes && settings.MergeStaticMeshesAsSkinned; }
        set { settings.MergeStaticMeshesAsSkinned = value; } }
    public bool MergeDifferentPropertyMaterials {
        get { return HasCustomShaderSupport && settings.MergeDifferentPropertyMaterials; }
        set { settings.MergeDifferentPropertyMaterials = value; } }
    public bool MergeSameDimensionTextures {
        get { return settings.MergeDifferentPropertyMaterials && settings.MergeSameDimensionTextures; }
        set { settings.MergeSameDimensionTextures = value; } }
    public bool MergeMainTex {
        get { return MergeSameDimensionTextures && settings.MergeMainTex; }
        set { settings.MergeMainTex = value; } }
    public bool MMDCompatibility { get { return settings.MMDCompatibility; } set { settings.MMDCompatibility = value; } }
    public bool DeleteUnusedComponents { get { return settings.DeleteUnusedComponents; } set { settings.DeleteUnusedComponents = value; } }
    public bool DeleteUnusedGameObjects { get { return settings.DeleteUnusedGameObjects != 0; } set { settings.DeleteUnusedGameObjects = value ? 1 : 0; } }
    public bool OptimizeFXLayer { get { return settings.OptimizeFXLayer; } set { settings.OptimizeFXLayer = value; } }
    public bool CombineApproximateMotionTimeAnimations {
        get { return settings.OptimizeFXLayer && settings.CombineApproximateMotionTimeAnimations; }
        set { settings.CombineApproximateMotionTimeAnimations = value; } }
    public bool DisablePhysBonesWhenUnused { get { return settings.DisablePhysBonesWhenUnused; } set { settings.DisablePhysBonesWhenUnused = value; } }
    public bool MergeSameRatioBlendShapes { get { return settings.MergeSameRatioBlendShapes; } set { settings.MergeSameRatioBlendShapes = value; } }
    public bool UseRingFingerAsFootCollider { get { return settings.UseRingFingerAsFootCollider; } set { settings.UseRingFingerAsFootCollider = value; } }
    public bool ProfileTimeUsed { get { return settings.ProfileTimeUsed; } set { settings.ProfileTimeUsed = value; } }

    private static float progressBar = 0;

    private static void _Log(string message) {
        Debug.Log($"[WKVRCOptimizer] {message}");
    }

    public void DisplayProgressBar(string text)
    {
        var titleName = name.EndsWith("(BrokenCopy)") ? name.Substring(0, name.Length - "(BrokenCopy)".Length) : name;
        _Log(text);
        EditorUtility.DisplayProgressBar("Optimizing " + titleName, text, progressBar);
    }

    private void DisplayProgressBar(string text, float progress)
    {
        progressBar = progress;
        DisplayProgressBar(text);
    }

    public void ClearCaches()
    {
        _Log("ClearCaches() called.");
        cacheManager?.ClearCaches();
        _Log("ClearCaches() finished.");
    }

    public long GetPolyCount()
    {
        _Log("GetPolyCount() called.");
        EnsureInitializedForEditor();
        long polyCount = meshOptimizer.GetPolyCount();
        _Log($"GetPolyCount() finished. Poly count: {polyCount}");
        return polyCount;
    }

    public static bool IsMaterialReadyToCombineWithOtherMeshes(Material material)
    {
        _Log($"IsMaterialReadyToCombineWithOtherMeshes() called for material: {material?.name ?? "null"}");
        bool result = material != null && ShaderAnalyzer.Parse(material.shader).CanMerge();
        _Log($"IsMaterialReadyToCombineWithOtherMeshes() finished for material: {material?.name ?? "null"}. Result: {result}");
        return result;
    }

    public HashSet<string> FindAllPathsWhereMeshOrGameObjectHasOnlyOnAnimation()
    {
        _Log("FindAllPathsWhereMeshOrGameObjectHasOnlyOnAnimation() called.");
        EnsureInitializedForEditor();
        HashSet<string> paths = animationRewriter.FindAllPathsWhereMeshOrGameObjectHasOnlyOnAnimation();
        _Log($"FindAllPathsWhereMeshOrGameObjectHasOnlyOnAnimation() finished. Found {paths.Count} paths.");
        return paths;
    }

    public bool UsesAnyLayerMasks()
    {
        _Log("UsesAnyLayerMasks() called.");
        var avDescriptor = GetComponent<VRCAvatarDescriptor>();
        if (avDescriptor == null)
        {
            _Log("UsesAnyLayerMasks() finished. No VRCAvatarDescriptor found, returning false.");
            return false;
        }
        var playableLayers = avDescriptor.baseAnimationLayers.Union(avDescriptor.specialAnimationLayers).ToArray();
        foreach (var playableLayer in playableLayers)
        {
            var controller = playableLayer.animatorController as AnimatorController;
            if (controller == null)
                continue;
            if (controller.layers.Any(layer => layer.avatarMask != null))
            {
                _Log("UsesAnyLayerMasks() finished. Found layer mask, returning true.");
                return true;
            }
        }
        _Log("UsesAnyLayerMasks() finished. No layer masks found, returning false.");
        return false;
    }

    private void DeleteAllUnusedSkinnedMeshRenderers()
    {
        _Log("DeleteAllUnusedSkinnedMeshRenderers() called.");
        EnsureInitializedForEditor();
        meshOptimizer.DeleteAllUnusedSkinnedMeshRenderers();
        _Log("DeleteAllUnusedSkinnedMeshRenderers() finished.");
    }

    public List<List<Renderer>> FindPossibleSkinnedMeshMerges()
    {
        _Log("FindPossibleSkinnedMeshMerges() called.");
        EnsureInitializedForEditor();
        List<List<Renderer>> merges = meshOptimizer.FindPossibleSkinnedMeshMerges();
        _Log($"FindPossibleSkinnedMeshMerges() finished. Found {merges.Count} possible merges.");
        return merges;
    }

    public bool CanUseNaNimationOnMesh(string meshPath)
    {
        _Log($"CanUseNaNimationOnMesh() called for meshPath: {meshPath}");
        EnsureInitializedForEditor();
        bool result = meshOptimizer.CanUseNaNimationOnMesh(meshPath);
        _Log($"CanUseNaNimationOnMesh() finished for meshPath: {meshPath}. Result: {result}");
        return result;
    }
    
    private void FixAllAnimationPaths()
    {
        _Log("FixAllAnimationPaths() called.");
        EnsureInitializedForEditor();
        animationRewriter.FixAllAnimationPaths();
        _Log("FixAllAnimationPaths() finished.");
    }

    public List<List<string>> AnalyzeFXLayerMergeAbility()
    {
        _Log("AnalyzeFXLayerMergeAbility() called.");
        EnsureInitializedForEditor();
        List<List<string>> result = fxLayerOptimizer.AnalyzeFXLayerMergeAbility();
        _Log($"AnalyzeFXLayerMergeAbility() finished. Found {result.Count} lists.");
        return result;
    }

    public bool IsMergeableFXLayer(int layerIndex)
    {
        _Log($"IsMergeableFXLayer() called for layerIndex: {layerIndex}");
        EnsureInitializedForEditor();
        bool result = fxLayerOptimizer.IsMergeableFXLayer(layerIndex);
        _Log($"IsMergeableFXLayer() finished for layerIndex: {layerIndex}. Result: {result}");
        return result;
    }

    public HashSet<int> FindUselessFXLayers()
    {
        _Log("FindUselessFXLayers() called.");
        EnsureInitializedForEditor();
        HashSet<int> layers = fxLayerOptimizer.FindUselessFXLayers();
        _Log($"FindUselessFXLayers() finished. Found {layers.Count} useless layers.");
        return layers;
    }

    public bool DoesFXLayerUseWriteDefaults()
    {
        _Log("DoesFXLayerUseWriteDefaults() called.");
        EnsureInitializedForEditor();
        bool result = fxLayerOptimizer.DoesFXLayerUseWriteDefaults();
        _Log($"DoesFXLayerUseWriteDefaults() finished. Result: {result}");
        return result;
    }

    public Dictionary<VRCPhysBoneBase, HashSet<Object>> FindAllPhysBoneDependencies()
    {
        _Log("FindAllPhysBoneDependencies() called.");
        EnsureInitializedForEditor();
        Dictionary<VRCPhysBoneBase, HashSet<Object>> dependencies = componentOptimizer.FindAllPhysBoneDependencies();
        _Log($"FindAllPhysBoneDependencies() finished. Found {dependencies.Count} PhysBone dependencies.");
        return dependencies;
    }

    public Dictionary<string, List<string>> FindAllPhysBonesToDisable()
    {
        _Log("FindAllPhysBonesToDisable() called.");
        EnsureInitializedForEditor();
        Dictionary<string, List<string>> physBones = componentOptimizer.FindAllPhysBonesToDisable();
        _Log($"FindAllPhysBonesToDisable() finished. Found {physBones.Count} PhysBones to disable.");
        return physBones;
    }

    public Dictionary<(string path, int index), HashSet<Material>> FindAllMaterialSwapMaterials()
    {
        _Log("FindAllMaterialSwapMaterials() called.");
        EnsureInitializedForEditor();
        Dictionary<(string path, int index), HashSet<Material>> materials = fxLayerOptimizer.FindAllMaterialSwapMaterials();
        _Log($"FindAllMaterialSwapMaterials() finished. Found {materials.Count} material swap materials.");
        return materials;
    }

    private HashSet<EditorCurveBinding> GetAllMaterialSwapBindingsToRemove()
    {
        _Log("GetAllMaterialSwapBindingsToRemove() called.");
        EnsureInitializedForEditor();
        HashSet<EditorCurveBinding> bindings = fxLayerOptimizer.GetAllMaterialSwapBindingsToRemove();
        _Log($"GetAllMaterialSwapBindingsToRemove() finished. Found {bindings.Count} bindings to remove.");
        return bindings;
    }

    private void OptimizeMaterialSwapMaterials()
    {
        _Log("OptimizeMaterialSwapMaterials() called.");
        EnsureInitializedForEditor();
        materialOptimizer.OptimizeMaterialSwapMaterials();
        _Log("OptimizeMaterialSwapMaterials() finished.");
    }

    public bool IsHumanoid()
    {
        _Log("IsHumanoid() called.");
        EnsureInitializedForEditor();
        bool result = fxLayerOptimizer.IsHumanoid();
        _Log($"IsHumanoid() finished. Result: {result}");
        return result;
    }

    public AnimatorController GetFXLayer()
    {
        _Log("GetFXLayer() called.");
        EnsureInitializedForEditor();
        AnimatorController controller = fxLayerOptimizer.GetFXLayer();
        _Log($"GetFXLayer() finished. Controller: {controller?.name ?? "null"}");
        return controller;
    }

    public AnimatorControllerLayer[] GetFXLayerLayers()
    {
        _Log("GetFXLayerLayers() called.");
        EnsureInitializedForEditor();
        AnimatorControllerLayer[] layers = fxLayerOptimizer.GetFXLayerLayers();
        _Log($"GetFXLayerLayers() finished. Found {layers.Length} layers.");
        return layers;
    }

    public void ProcessBlendShapes(OptimizationContext context)
    {
        _Log("ProcessBlendShapes() called.");
        EnsureInitializedForEditor();
        meshOptimizer.ProcessBlendShapes();
        _Log("ProcessBlendShapes() finished.");
    }

    public HashSet<string> GetUsedBlendShapePaths()
    {
        _Log("GetUsedBlendShapePaths() called.");
        HashSet<string> blendShapes = new HashSet<string>(context.usedBlendShapes);
        _Log($"GetUsedBlendShapePaths() finished. Found {blendShapes.Count} used blend shapes.");
        return blendShapes;
    }

    public List<List<(string blendshape, float value)>> FindMergeableBlendShapes(IEnumerable<Renderer> mergedMeshBlob)
    {
        _Log("FindMergeableBlendShapes() called.");
        EnsureInitializedForEditor();
        List<List<(string blendshape, float value)>> blendShapes = meshOptimizer.FindMergeableBlendShapes(mergedMeshBlob);
        _Log($"FindMergeableBlendShapes() finished. Found {blendShapes.Count} mergeable blend shape groups.");
        return blendShapes;
    }

    public Dictionary<string, HashSet<string>> FindAllAnimatedMaterialProperties() {
        _Log("FindAllAnimatedMaterialProperties() called.");
        EnsureInitializedForEditor();
        Dictionary<string, HashSet<string>> properties = fxLayerOptimizer.FindAllAnimatedMaterialProperties();
        _Log($"FindAllAnimatedMaterialProperties() finished. Found {properties.Count} animated material properties.");
        return properties;
    }

    public HashSet<string> FindAllGameObjectTogglePaths()
    {
        _Log("FindAllGameObjectTogglePaths() called.");
        EnsureInitializedForEditor();
        HashSet<string> paths = fxLayerOptimizer.FindAllGameObjectTogglePaths();
        _Log($"FindAllGameObjectTogglePaths() finished. Found {paths.Count} GameObject toggle paths.");
        return paths;
    }

    public HashSet<string> FindAllRendererTogglePaths()
    {
        _Log("FindAllRendererTogglePaths() called.");
        EnsureInitializedForEditor();
        HashSet<string> paths = fxLayerOptimizer.FindAllRendererTogglePaths();
        _Log($"FindAllRendererTogglePaths() finished. Found {paths.Count} Renderer toggle paths.");
        return paths;
    }

    public HashSet<Transform> FindAllAlwaysDisabledGameObjects()
    {
        _Log("FindAllAlwaysDisabledGameObjects() called.");
        EnsureInitializedForEditor();
        HashSet<Transform> gameObjects = componentOptimizer.FindAllAlwaysDisabledGameObjects();
        _Log($"FindAllAlwaysDisabledGameObjects() finished. Found {gameObjects.Count} always disabled GameObjects.");
        return gameObjects;
    }

    public HashSet<Component> FindAllUnusedComponents()
    {
        _Log("FindAllUnusedComponents() called.");
        EnsureInitializedForEditor();
        HashSet<Component> components = componentOptimizer.FindAllUnusedComponents();
        _Log($"FindAllUnusedComponents() finished. Found {components.Count} unused components.");
        return components;
    }

    public HashSet<Transform> FindAllUnmovingTransforms()
    {
        _Log("FindAllUnmovingTransforms() called.");
        EnsureInitializedForEditor();
        HashSet<Transform> transforms = componentOptimizer.FindAllUnmovingTransforms();
        _Log($"FindAllUnmovingTransforms() finished. Found {transforms.Count} unmoving transforms.");
        return transforms;
    }

    public List<T> GetNonEditorOnlyComponentsInChildren<T>() where T : Component
    {
        _Log($"GetNonEditorOnlyComponentsInChildren<{typeof(T).Name}>() called.");
        EnsureInitializedForEditor();
        List<T> components = componentOptimizer.GetNonEditorOnlyComponentsInChildren<T>();
        _Log($"GetNonEditorOnlyComponentsInChildren<{typeof(T).Name}>() finished. Found {components.Count} components.");
        return components;
    }

    public List<T> GetUsedComponentsInChildren<T>() where T : Component
    {
        _Log($"GetUsedComponentsInChildren<{typeof(T).Name}>() called.");
        EnsureInitializedForEditor();
        List<T> components = componentOptimizer.GetUsedComponentsInChildren<T>();
        _Log($"GetUsedComponentsInChildren<{typeof(T).Name}>() finished. Found {components.Count} components.");
        return components;
    }

    public HashSet<Material> FindAllUsedMaterials()
    {
        _Log("FindAllUsedMaterials() called.");
        EnsureInitializedForEditor();
        HashSet<Material> materials = materialOptimizer.FindAllUsedMaterials();
        _Log($"FindAllUsedMaterials() finished. Found {materials.Count} materials.");
        return materials;
    }

    private void CombineSkinnedMeshes()
    {
        _Log("CombineSkinnedMeshes() called.");
        EnsureInitializedForEditor();
        meshOptimizer.CombineSkinnedMeshes();
        _Log("CombineSkinnedMeshes() finished.");
    }

    public HashSet<Transform> GetAllExcludedTransforms() {
        _Log("GetAllExcludedTransforms() called.");
        EnsureInitializedForEditor();
        HashSet<Transform> transforms = componentOptimizer.GetAllExcludedTransforms();
        _Log($"GetAllExcludedTransforms() finished. Found {transforms.Count} excluded transforms.");
        return transforms;
    }

    public HashSet<string> GetAllExcludedTransformPaths() {
        _Log("GetAllExcludedTransformPaths() called.");
        EnsureInitializedForEditor();
        HashSet<string> paths = componentOptimizer.GetAllExcludedTransformPaths();
        _Log($"GetAllExcludedTransformPaths() finished. Found {paths.Count} excluded transform paths.");
        return paths;
    }

    private void ConvertStaticMeshesToSkinnedMeshes()
    {
        _Log("ConvertStaticMeshesToSkinnedMeshes() called.");
        EnsureInitializedForEditor();
        meshOptimizer.ConvertStaticMeshesToSkinnedMeshes();
        _Log("ConvertStaticMeshesToSkinnedMeshes() finished.");
    }

    public List<List<MaterialSlot>> FindAllMergeAbleMaterials(IEnumerable<Renderer> renderers)
    {
        _Log("FindAllMergeAbleMaterials() called.");
        EnsureInitializedForEditor();
        List<List<MaterialSlot>> materialSlots = materialOptimizer.FindAllMergeAbleMaterials(renderers);
        _Log($"FindAllMergeAbleMaterials() finished. Found {materialSlots.Count} material slot groups.");
        return materialSlots;
    }

    public bool GetRendererDefaultEnabledState(Renderer r)
    {
        _Log($"GetRendererDefaultEnabledState() called for renderer: {r?.name ?? "null"}");
        EnsureInitializedForEditor();
        bool result = meshOptimizer.GetRendererDefaultEnabledState(r);
        _Log($"GetRendererDefaultEnabledState() finished for renderer: {r?.name ?? "null"}. Result: {result}");
        return result;
    }
    
    public HashSet<Renderer> FindAllPenetrators()
    {
         _Log("FindAllPenetrators() called.");
         EnsureInitializedForEditor();
         HashSet<Renderer> penetrators = componentOptimizer.FindAllPenetrators();
         _Log($"FindAllPenetrators() finished. Found {penetrators.Count} penetrators.");
         return penetrators;
    }

#endif
}
