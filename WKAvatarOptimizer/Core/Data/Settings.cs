namespace WKAvatarOptimizer.Data
{
    // [System.Serializable] // Removed to prevent serialization
    public class Settings
    {
        // Defaults that work for "most" things, but will be overridden by smart logic
        public bool ApplyOnUpload = true;
        public bool WritePropertiesAsStaticValues = false;
        public bool MergeSkinnedMeshes = true;
        public int MergeSkinnedMeshesWithShaderToggle = 0;
        public int MergeSkinnedMeshesWithNaNimation = 0;
        public bool NaNimationAllow3BoneSkinning = false;
        public bool MergeSkinnedMeshesSeparatedByDefaultEnabledState = true;
        public bool MergeStaticMeshesAsSkinned = true;
        public bool MergeDifferentPropertyMaterials = false; // Safer default
        public bool MergeSameDimensionTextures = false;
        public bool MergeMainTex = false;
        public bool OptimizeFXLayer = true;
        public bool CombineApproximateMotionTimeAnimations = false;
        public bool DisablePhysBonesWhenUnused = true;
        public bool MergeSameRatioBlendShapes = true;
        public bool MMDCompatibility = true;
        public bool DeleteUnusedComponents = true;
        public int DeleteUnusedGameObjects = 0;
        public bool UseRingFingerAsFootCollider = false;
        public bool ProfileTimeUsed = false;
    }
}