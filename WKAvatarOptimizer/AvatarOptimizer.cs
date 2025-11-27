using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Globalization;
using UnityEditor;
using UnityEditor.Animations;
using WKAvatarOptimizer.Core.Util;
using WKAvatarOptimizer.Data;
using WKAvatarOptimizer.Core;
using VRC.Dynamics;
using VRC.SDK3.Avatars.Components;

[HelpURL("https://github.com/whyknot/WKVRCOptimizer/blob/main/README.md")]
[AddComponentMenu("WK Avatar Optimizer")]
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

    public OptimizationContext context;
    private CacheManager cacheManager;
    public MeshOptimizer meshOptimizer;
    public FXLayerOptimizer fxLayerOptimizer;
    public ComponentOptimizer componentOptimizer;
    public MaterialOptimizer materialOptimizer;
    public AnimationRewriter animationRewriter;

    public void EnsureInitializedForEditor()
    {
        if (context == null)
        {
            context = new OptimizationContext();
        }
        if (cacheManager == null)
        {
            cacheManager = new CacheManager(context, settings, gameObject);
        }
        if (fxLayerOptimizer == null)
        {
            fxLayerOptimizer = new FXLayerOptimizer(context, settings, gameObject, this);
        }
        if (componentOptimizer == null)
        {
            componentOptimizer = new ComponentOptimizer(context, settings, gameObject, this);
        }
        if (materialOptimizer == null)
        {
            materialOptimizer = new MaterialOptimizer(context, settings, gameObject, this);
        }
        if (meshOptimizer == null)
        {
            meshOptimizer = new MeshOptimizer(context, cacheManager, settings, gameObject, this);
        }
        if (animationRewriter == null)
        {
            animationRewriter = new AnimationRewriter(context, settings, gameObject, cacheManager, componentOptimizer, fxLayerOptimizer, meshOptimizer, this);
        }
    }

    public void Optimize()
    {
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
            ConvertStaticMeshesToSkinnedMeshes();
            Profiler.StartNextSection("CalculateUsedBlendShapePaths()");
            meshOptimizer.ProcessBlendShapes();
            Profiler.StartNextSection("DeleteAllUnusedSkinnedMeshRenderers()");
            DeleteAllUnusedSkinnedMeshRenderers();
            Profiler.StartNextSection("CombineSkinnedMeshes()");
            DisplayProgressBar("Combining meshes", 0.2f);
            CombineSkinnedMeshes();

            Profiler.StartNextSection("CombineAndOptimizeMaterials()");
            DisplayProgressBar("Optimizing materials", 0.3f);
            materialOptimizer.CombineAndOptimizeMaterials();
            Profiler.StartNextSection("OptimizeMaterialSwapMaterials()");
            materialOptimizer.OptimizeMaterialSwapMaterials();
            Profiler.StartNextSection("OptimizeMaterialsOnNonSkinnedMeshes()");
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
            DisplayProgressBar("Done", 1.0f);
            componentOptimizer.MoveRingFingerColliderToFeet();
            Profiler.StartNextSection("DestroyImmediate(this)");
            DestroyImmediate(this);
            Profiler.EndSection();
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

    public void DisplayProgressBar(string text)
    {
        var titleName = name.EndsWith("(BrokenCopy)") ? name.Substring(0, name.Length - "(BrokenCopy)".Length) : name;
        EditorUtility.DisplayProgressBar("Optimizing " + titleName, text, progressBar);
    }

    private void DisplayProgressBar(string text, float progress)
    {
        progressBar = progress;
        DisplayProgressBar(text);
    }

    public void ClearCaches()
    {
        cacheManager?.ClearCaches();
    }

    public long GetPolyCount()
    {
        EnsureInitializedForEditor();
        long polyCount = meshOptimizer.GetPolyCount();
        return polyCount;
    }

    public static bool IsMaterialReadyToCombineWithOtherMeshes(Material material)
    {
        bool result = material != null && ShaderAnalyzer.Parse(material.shader).CanMerge();
        return result;
    }

    public HashSet<string> FindAllPathsWhereMeshOrGameObjectHasOnlyOnAnimation()
    {
        EnsureInitializedForEditor();
        HashSet<string> paths = animationRewriter.FindAllPathsWhereMeshOrGameObjectHasOnlyOnAnimation();
        return paths;
    }

    public bool UsesAnyLayerMasks()
    {
        var avDescriptor = GetComponent<VRCAvatarDescriptor>();
        if (avDescriptor == null)
        {
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
                return true;
            }
        }
        return false;
    }

    private void DeleteAllUnusedSkinnedMeshRenderers()
    {
        EnsureInitializedForEditor();
        meshOptimizer.DeleteAllUnusedSkinnedMeshRenderers();
    }

    public List<List<Renderer>> FindPossibleSkinnedMeshMerges()
    {
        EnsureInitializedForEditor();
        List<List<Renderer>> merges = meshOptimizer.FindPossibleSkinnedMeshMerges();
        return merges;
    }

    public bool CanUseNaNimationOnMesh(string meshPath)
    {
        EnsureInitializedForEditor();
        bool result = meshOptimizer.CanUseNaNimationOnMesh(meshPath);
        return result;
    }
    
    private void FixAllAnimationPaths()
    {
        EnsureInitializedForEditor();
        animationRewriter.FixAllAnimationPaths();
    }

    public List<List<string>> AnalyzeFXLayerMergeAbility()
    {
        EnsureInitializedForEditor();
        List<List<string>> result = fxLayerOptimizer.AnalyzeFXLayerMergeAbility();
        return result;
    }

    public bool IsMergeableFXLayer(int layerIndex)
    {
        EnsureInitializedForEditor();
        bool result = fxLayerOptimizer.IsMergeableFXLayer(layerIndex);
        return result;
    }

    public HashSet<int> FindUselessFXLayers()
    {
        EnsureInitializedForEditor();
        HashSet<int> layers = fxLayerOptimizer.FindUselessFXLayers();
        return layers;
    }

    public bool DoesFXLayerUseWriteDefaults()
    {
        EnsureInitializedForEditor();
        bool result = fxLayerOptimizer.DoesFXLayerUseWriteDefaults();
        return result;
    }

    public Dictionary<VRCPhysBoneBase, HashSet<Object>> FindAllPhysBoneDependencies()
    {
        EnsureInitializedForEditor();
        Dictionary<VRCPhysBoneBase, HashSet<Object>> dependencies = componentOptimizer.FindAllPhysBoneDependencies();
        return dependencies;
    }

    public Dictionary<string, List<string>> FindAllPhysBonesToDisable()
    {
        EnsureInitializedForEditor();
        Dictionary<string, List<string>> physBones = componentOptimizer.FindAllPhysBonesToDisable();
        return physBones;
    }

    public Dictionary<(string path, int index), HashSet<Material>> FindAllMaterialSwapMaterials()
    {
        EnsureInitializedForEditor();
        Dictionary<(string path, int index), HashSet<Material>> materials = fxLayerOptimizer.FindAllMaterialSwapMaterials();
        return materials;
    }

    private HashSet<EditorCurveBinding> GetAllMaterialSwapBindingsToRemove()
    {
        EnsureInitializedForEditor();
        HashSet<EditorCurveBinding> bindings = fxLayerOptimizer.GetAllMaterialSwapBindingsToRemove();
        return bindings;
    }

    private void OptimizeMaterialSwapMaterials()
    {
        EnsureInitializedForEditor();
        materialOptimizer.OptimizeMaterialSwapMaterials();
    }

    public bool IsHumanoid()
    {
        EnsureInitializedForEditor();
        bool result = fxLayerOptimizer.IsHumanoid();
        return result;
    }

    public AnimatorController GetFXLayer()
    {
        EnsureInitializedForEditor();
        AnimatorController controller = fxLayerOptimizer.GetFXLayer();
        return controller;
    }

    public AnimatorControllerLayer[] GetFXLayerLayers()
    {
        EnsureInitializedForEditor();
        AnimatorControllerLayer[] layers = fxLayerOptimizer.GetFXLayerLayers();
        return layers;
    }

    public void ProcessBlendShapes(OptimizationContext context)
    {
        EnsureInitializedForEditor();
        meshOptimizer.ProcessBlendShapes();
    }

    public HashSet<string> GetUsedBlendShapePaths()
    {
        HashSet<string> blendShapes = new HashSet<string>(context.usedBlendShapes);
        return blendShapes;
    }

    public List<List<(string blendshape, float value)>> FindMergeableBlendShapes(IEnumerable<Renderer> mergedMeshBlob)
    {
        EnsureInitializedForEditor();
        List<List<(string blendshape, float value)>> blendShapes = meshOptimizer.FindMergeableBlendShapes(mergedMeshBlob);
        return blendShapes;
    }

    public Dictionary<string, HashSet<string>> FindAllAnimatedMaterialProperties() {
        EnsureInitializedForEditor();
        Dictionary<string, HashSet<string>> properties = fxLayerOptimizer.FindAllAnimatedMaterialProperties();
        return properties;
    }

    public HashSet<string> FindAllGameObjectTogglePaths()
    {
        EnsureInitializedForEditor();
        HashSet<string> paths = fxLayerOptimizer.FindAllGameObjectTogglePaths();
        return paths;
    }

    public HashSet<string> FindAllRendererTogglePaths()
    {
        EnsureInitializedForEditor();
        HashSet<string> paths = fxLayerOptimizer.FindAllRendererTogglePaths();
        return paths;
    }

    public HashSet<Transform> FindAllAlwaysDisabledGameObjects()
    {
        EnsureInitializedForEditor();
        HashSet<Transform> gameObjects = componentOptimizer.FindAllAlwaysDisabledGameObjects();
        return gameObjects;
    }

    public HashSet<Component> FindAllUnusedComponents()
    {
        EnsureInitializedForEditor();
        HashSet<Component> components = componentOptimizer.FindAllUnusedComponents();
        return components;
    }

    public HashSet<Transform> FindAllUnmovingTransforms()
    {
        EnsureInitializedForEditor();
        HashSet<Transform> transforms = componentOptimizer.FindAllUnmovingTransforms();
        return transforms;
    }

    public List<T> GetNonEditorOnlyComponentsInChildren<T>() where T : Component
    {
        EnsureInitializedForEditor();
        List<T> components = componentOptimizer.GetNonEditorOnlyComponentsInChildren<T>();
        return components;
    }

    public List<T> GetUsedComponentsInChildren<T>() where T : Component
    {
        EnsureInitializedForEditor();
        List<T> components = componentOptimizer.GetUsedComponentsInChildren<T>();
        return components;
    }

    public HashSet<Material> FindAllUsedMaterials()
    {
        EnsureInitializedForEditor();
        HashSet<Material> materials = materialOptimizer.FindAllUsedMaterials();
        return materials;
    }

    private void CombineSkinnedMeshes()
    {
        EnsureInitializedForEditor();
        meshOptimizer.CombineSkinnedMeshes();
    }

    public HashSet<Transform> GetAllExcludedTransforms() {
        EnsureInitializedForEditor();
        HashSet<Transform> transforms = componentOptimizer.GetAllExcludedTransforms();
        return transforms;
    }

    public HashSet<string> GetAllExcludedTransformPaths() {
        EnsureInitializedForEditor();
        HashSet<string> paths = componentOptimizer.GetAllExcludedTransformPaths();
        return paths;
    }

    private void ConvertStaticMeshesToSkinnedMeshes()
    {
        EnsureInitializedForEditor();
        meshOptimizer.ConvertStaticMeshesToSkinnedMeshes();
    }

    public List<List<MaterialSlot>> FindAllMergeAbleMaterials(IEnumerable<Renderer> renderers)
    {
        EnsureInitializedForEditor();
        List<List<MaterialSlot>> materialSlots = materialOptimizer.FindAllMergeAbleMaterials(renderers);
        return materialSlots;
    }

    public bool GetRendererDefaultEnabledState(Renderer r)
    {
        EnsureInitializedForEditor();
        bool result = meshOptimizer.GetRendererDefaultEnabledState(r);
        return result;
    }
    
    public HashSet<Renderer> FindAllPenetrators()
    {
         EnsureInitializedForEditor();
         HashSet<Renderer> penetrators = componentOptimizer.FindAllPenetrators();
         return penetrators;
    }
}