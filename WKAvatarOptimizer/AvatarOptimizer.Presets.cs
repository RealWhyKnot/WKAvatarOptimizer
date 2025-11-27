using System.Collections.Generic;
using System.Linq;
using WKAvatarOptimizer.Data;

public partial class AvatarOptimizer
{
    public bool CanChangeSetting(string fieldName)
    {
        switch (fieldName)
        {
            case nameof(WritePropertiesAsStaticValues):
                return !(MergeSkinnedMeshesWithShaderToggle || settings.MergeDifferentPropertyMaterials);
            case nameof(MergeSkinnedMeshesWithShaderToggle):
            case nameof(MergeSkinnedMeshesWithNaNimation):
            case nameof(MergeStaticMeshesAsSkinned):
                return settings.MergeSkinnedMeshes;
            case nameof(NaNimationAllow3BoneSkinning):
            case nameof(MergeSkinnedMeshesSeparatedByDefaultEnabledState):
                return MergeSkinnedMeshesWithNaNimation;
            case nameof(MergeSameDimensionTextures):
                return settings.MergeDifferentPropertyMaterials;
            case nameof(MergeMainTex):
                return MergeSameDimensionTextures;
            case nameof(CombineApproximateMotionTimeAnimations):
                return settings.OptimizeFXLayer;
            default:
                return true;
        }
    }

    private static Dictionary<string, string> FieldDisplayName = new Dictionary<string, string>() {
        {nameof(ApplyOnUpload), "Apply on Upload"},
        {nameof(WritePropertiesAsStaticValues), "Write Properties as Static Values"},
        {nameof(MergeSkinnedMeshes), "Merge Skinned Meshes"},
        {nameof(MergeSkinnedMeshesWithShaderToggle), "Use Shader Toggles"},
        {nameof(MergeSkinnedMeshesWithNaNimation), "NaNimation Toggles"},
        {nameof(NaNimationAllow3BoneSkinning), "Allow 3 Bone Skinning"},
        {nameof(MergeSkinnedMeshesSeparatedByDefaultEnabledState), "Keep Default Enabled State"},
        {nameof(MergeStaticMeshesAsSkinned), "Merge Static Meshes as Skinned"},
        {nameof(MergeDifferentPropertyMaterials), "Merge Different Property Materials"},
        {nameof(MergeSameDimensionTextures), "Merge Same Dimension Textures"},
        {nameof(MergeMainTex), "Merge MainTex"},
        {nameof(MMDCompatibility), "MMD Compatibility"},
        {nameof(DeleteUnusedComponents), "Delete Unused Components"},
        {nameof(DeleteUnusedGameObjects), "Delete Unused GameObjects"},
        {nameof(OptimizeFXLayer), "Optimize FX Layer"},
        {nameof(CombineApproximateMotionTimeAnimations), "Combine Motion Time Approximation"},
        {nameof(DisablePhysBonesWhenUnused), "Disable Phys Bones When Unused"},
        {nameof(MergeSameRatioBlendShapes), "Merge Same Ratio Blend Shapes"},
        {nameof(UseRingFingerAsFootCollider), "Use Ring Finger as Foot Collider"},
        {nameof(ProfileTimeUsed), "Profile Time Used"},
        {nameof(ShowFXLayerMergeErrors), "Show FX Layer Merge Errors"},
    };

    public static string GetDisplayName(string fieldName)
    {
        if (FieldDisplayName.ContainsKey(fieldName))
        {
            return FieldDisplayName[fieldName];
        }
        return fieldName;
    }

    private static List<(string name, Dictionary<string, object>)> SettingsPresets = new List<(string name, Dictionary<string, object>)>()
    {
        ("Basic", new Dictionary<string, object>() {
            {nameof(Settings.ApplyOnUpload), true},
            {nameof(Settings.WritePropertiesAsStaticValues), false},
            {nameof(Settings.MergeSkinnedMeshes), true},
            {nameof(Settings.MergeSkinnedMeshesWithShaderToggle), 0},
            {nameof(Settings.MergeSkinnedMeshesWithNaNimation), 0},
            {nameof(Settings.NaNimationAllow3BoneSkinning), false},
            {nameof(Settings.MergeSkinnedMeshesSeparatedByDefaultEnabledState), true},
            {nameof(Settings.MergeStaticMeshesAsSkinned), false},
            {nameof(Settings.MergeDifferentPropertyMaterials), false},
            {nameof(Settings.MergeSameDimensionTextures), false},
            {nameof(Settings.MergeMainTex), false},
            {nameof(Settings.OptimizeFXLayer), true},
            {nameof(Settings.CombineApproximateMotionTimeAnimations), false},
            {nameof(Settings.DisablePhysBonesWhenUnused), true},
            {nameof(Settings.MergeSameRatioBlendShapes), true},
            {nameof(Settings.MMDCompatibility), true},
            {nameof(Settings.DeleteUnusedComponents), true},
            {nameof(Settings.DeleteUnusedGameObjects), 0},
        }),
        ("Shader Toggles", new Dictionary<string, object>() {
            {nameof(Settings.ApplyOnUpload), true},
            {nameof(Settings.WritePropertiesAsStaticValues), true},
            {nameof(Settings.MergeSkinnedMeshes), true},
            {nameof(Settings.MergeSkinnedMeshesWithShaderToggle), 1},
            {nameof(Settings.MergeSkinnedMeshesWithNaNimation), 1},
            {nameof(Settings.NaNimationAllow3BoneSkinning), false},
            {nameof(Settings.MergeSkinnedMeshesSeparatedByDefaultEnabledState), true},
            {nameof(Settings.MergeStaticMeshesAsSkinned), true},
            {nameof(Settings.MergeDifferentPropertyMaterials), true},
            {nameof(Settings.MergeSameDimensionTextures), true},
            {nameof(Settings.MergeMainTex), false},
            {nameof(Settings.OptimizeFXLayer), true},
            {nameof(Settings.CombineApproximateMotionTimeAnimations), false},
            {nameof(Settings.DisablePhysBonesWhenUnused), true},
            {nameof(Settings.MergeSameRatioBlendShapes), true},
            {nameof(Settings.MMDCompatibility), true},
            {nameof(Settings.DeleteUnusedComponents), true},
            {nameof(Settings.DeleteUnusedGameObjects), 0},
        }),
        ("Full", new Dictionary<string, object>() {
            {nameof(Settings.ApplyOnUpload), true},
            {nameof(Settings.WritePropertiesAsStaticValues), true},
            {nameof(Settings.MergeSkinnedMeshes), true},
            {nameof(Settings.MergeSkinnedMeshesWithShaderToggle), 1},
            {nameof(Settings.MergeSkinnedMeshesWithNaNimation), 1},
            {nameof(Settings.NaNimationAllow3BoneSkinning), true},
            {nameof(Settings.MergeSkinnedMeshesSeparatedByDefaultEnabledState), false},
            {nameof(Settings.MergeStaticMeshesAsSkinned), true},
            {nameof(Settings.MergeDifferentPropertyMaterials), true},
            {nameof(Settings.MergeSameDimensionTextures), true},
            {nameof(Settings.MergeMainTex), true},
            {nameof(Settings.OptimizeFXLayer), true},
            {nameof(Settings.CombineApproximateMotionTimeAnimations), true},
            {nameof(Settings.DisablePhysBonesWhenUnused), true},
            {nameof(Settings.MergeSameRatioBlendShapes), true},
            {nameof(Settings.MMDCompatibility), false},
            {nameof(Settings.DeleteUnusedComponents), true},
            {nameof(Settings.DeleteUnusedGameObjects), 1},
        }),
    };

    public List<string> GetPresetNames()
    {
        return SettingsPresets.Select(x => x.name).Where(x => HasCustomShaderSupport || x != "Shader Toggles").ToList();
    }

    public bool IsPresetActive(string presetName)
    {
        var preset = SettingsPresets.Find(x => x.name == presetName).Item2;
        foreach (var entry in preset)
        {
            var field = typeof(Settings).GetField(entry.Key);
            if (typeof(bool) == field.FieldType && !field.GetValue(settings).Equals(entry.Value))
                return false;
            if (typeof(int) == field.FieldType && (int)entry.Value == 1 && (int)field.GetValue(settings) == 0)
                return false;
            if (typeof(int) == field.FieldType && (int)entry.Value == 0 && (int)field.GetValue(settings) == 1)
                return false;
        }
        return true;
    }
    public void SetPreset(string presetName)
    {
        var preset = SettingsPresets.Find(x => x.name == presetName).Item2;
        foreach (var field in preset)
        {
            typeof(Settings).GetField(field.Key).SetValue(settings, field.Value);
        }
        ApplyAutoSettings();
    }

    public static long MaxPolyCountForAutoShaderToggle = 150000;

    public void ApplyAutoSettings()
    {
        DoAutoSettings = false;
        if (settings.DeleteUnusedGameObjects == 2)
        {
            DeleteUnusedGameObjects = !UsesAnyLayerMasks();
        }
        if (settings.MergeSkinnedMeshesWithShaderToggle == 2)
        {
            MergeSkinnedMeshesWithShaderToggle = GetPolyCount() < MaxPolyCountForAutoShaderToggle;
        }
        if (settings.MergeSkinnedMeshesWithNaNimation == 2)
        {
            MergeSkinnedMeshesWithNaNimation = GetPolyCount() < MaxPolyCountForAutoShaderToggle;
        }
    }
}
